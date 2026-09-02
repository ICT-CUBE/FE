using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ARZone
{
    // 서버와 말하는 유일한 곳. 도메인이 늘어도 이 파일은 안 늘어난다.
    //
    // 책임은 딱 다섯 가지다:
    //   1) base URL 조립          2) UnityWebRequest 생성·전송·해제
    //   3) 타임아웃                4) 상태코드 판정 + {code,message} 파싱
    //   5) 성공 시 원문 JSON 을 done 으로 전달
    // 캐시·재시도·토큰 갱신은 안 한다(지금 필요 없다).
    //
    // 호출부가 MonoBehaviour 가 아니어도 되게 코루틴 러너를 스스로 들고 있는다.
    // ICommunitySource 와 같은 done/fail 콜백 관례를 쓴다.
    public static class ApiClient
    {
        // 서버가 주는 에러 본문. 명세서 공통사항 — 400 은 전부 INVALID_REQUEST,
        // 500 은 전부 INTERNAL_SERVER_ERROR 로 통일해서 온다.
        [Serializable]
        class ErrorBody
        {
            public string code;
            public string message;
        }

        // 코루틴을 돌릴 주인. 화면이 꺼져도 요청이 끊기면 안 되므로
        // 씬에 붙지 않은 독립 오브젝트로 만들고 DontDestroyOnLoad 한다.
        class Runner : MonoBehaviour { }

        static Runner runner;

        static Runner Host
        {
            get
            {
                if (runner == null)
                {
                    var go = new GameObject("[ApiRunner]");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    runner = go.AddComponent<Runner>();
                }
                return runner;
            }
        }

        public static void Get(string path, Action<string> done, Action<string> fail)
            => Host.StartCoroutine(Send(UnityWebRequest.kHttpVerbGET, path, null, done, fail));

        public static void Delete(string path, Action<string> done, Action<string> fail)
            => Host.StartCoroutine(Send(UnityWebRequest.kHttpVerbDELETE, path, null, done, fail));

        public static void Post(string path, string json, Action<string> done, Action<string> fail)
            => Host.StartCoroutine(Send(UnityWebRequest.kHttpVerbPOST, path, json, done, fail));

        public static void Patch(string path, string json, Action<string> done, Action<string> fail)
            => Host.StartCoroutine(Send("PATCH", path, json, done, fail));

        // multipart PATCH (2026-08-26 대화 세션 종료용). Unity 의 MultipartFormDataSection 은
        // 빈 값을 거부해서 본문을 손으로 만든다 — 서버(FastAPI)는 빈 값 파트를 null 로 읽는다(실측 204).
        public static void PatchForm(string path, Dictionary<string, string> fields,
                                     Action<string> done, Action<string> fail)
        {
            string boundary = "----arzone-form";
            var sb = new System.Text.StringBuilder();
            if (fields != null)
                foreach (var kv in fields)
                    sb.Append("--").Append(boundary).Append("\r\n")
                      .Append("Content-Disposition: form-data; name=\"").Append(kv.Key).Append("\"\r\n\r\n")
                      .Append(kv.Value ?? "").Append("\r\n");
            sb.Append("--").Append(boundary).Append("--\r\n");
            Host.StartCoroutine(Send("PATCH", path, sb.ToString(), done, fail,
                                     "multipart/form-data; boundary=" + boundary));
        }

        static IEnumerator Send(string method, string path, string json,
                                Action<string> done, Action<string> fail,
                                string contentType = "application/json")
        {
            string url = ApiConfig.Url(path);
            float t0 = Time.realtimeSinceStartup;

            // UnityWebRequest 는 IDisposable 이다. using 을 빼면 네이티브 버퍼가 남는다.
            using (var req = new UnityWebRequest(url, method))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = ApiConfig.TimeoutSeconds;

                if (!string.IsNullOrEmpty(json))
                {
                    // UploadHandlerRaw 는 바이트를 받는다. UTF8 로 보내야 한글이 안 깨진다.
                    var body = System.Text.Encoding.UTF8.GetBytes(json);
                    req.uploadHandler = new UploadHandlerRaw(body);
                    req.SetRequestHeader("Content-Type", contentType);
                }
                req.SetRequestHeader("Accept", "application/json");

                // 2026-08-30: 서버가 auth/* 외 전부 Bearer 토큰을 요구한다. 저장된 토큰을 복원해 붙인다.
                ApiConfig.LoadSavedToken();
                if (!string.IsNullOrEmpty(ApiConfig.AccessToken))
                    req.SetRequestHeader("Authorization", "Bearer " + ApiConfig.AccessToken);

                yield return req.SendWebRequest();

                float ms = (Time.realtimeSinceStartup - t0) * 1000f;
                long code = req.responseCode;
                string text = req.downloadHandler != null ? req.downloadHandler.text : "";

                // 서버에 닿지도 못한 경우(비행기모드·DNS 실패·타임아웃).
                // responseCode 가 0 이면 응답 자체가 없었다는 뜻이다.
                if (req.result == UnityWebRequest.Result.ConnectionError || code == 0)
                {
                    Debug.LogWarning($"[Api] {method} {path} → 연결 실패 ({ms:F0}ms): {req.error}");
                    fail?.Invoke("네트워크에 연결할 수 없어요");
                    yield break;
                }

                if (code >= 400)
                {
                    // 본문이 {code,message} 면 그 message 를 쓰고, 아니면 상태코드만 알린다.
                    string msg = ParseError(text, code);
                    Debug.LogWarning($"[Api] {method} {path} → {code} ({ms:F0}ms) {text}");
                    if (code == 401) HandleExpiredToken();
                    fail?.Invoke(msg);
                    yield break;
                }

                Debug.Log($"[Api] {method} {path} → {code} ({ms:F0}ms, {text.Length}B)");
                done?.Invoke(text);
            }
        }

        // 401 = 저장된 토큰이 서버에서 만료·무효 (2026-08-31 실기: 자동 로그인 후 홈 401).
        // 토큰을 지우고 로그인 화면으로 보낸다 — 안 지우면 LoginModeSwitcher 가 다음 실행에도
        // 자동 로그인해서 화면마다 401 이 계속 뜬다. 병렬 요청이 한꺼번에 401 나도 한 번만 움직인다.
        static float lastAuthKick = -999f;

        static void HandleExpiredToken()
        {
            ApiConfig.SaveToken("");
            if (Time.realtimeSinceStartup - lastAuthKick < 3f) return;
            lastAuthKick = Time.realtimeSinceStartup;

            var sm = ScreenManager.Instance;
            if (sm == null || sm.Current == "Login") return;
            sm.Show("Login");
            Toast.Show("로그인이 만료되었어요. 다시 로그인해 주세요.");
        }

        static string ParseError(string text, long code)
        {
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    var e = JsonUtility.FromJson<ErrorBody>(text);
                    if (e != null && !string.IsNullOrEmpty(e.message)) return e.message;
                }
                catch (Exception)
                {
                    // 에러 본문이 JSON 이 아닐 수도 있다(게이트웨이가 HTML 을 주는 경우 등).
                    // 여기서 또 예외를 내면 원래 에러가 묻히므로 조용히 넘어간다.
                }
            }
            if (code == 404) return "찾을 수 없어요";
            if (code >= 500) return "잠시 후 다시 시도해 주세요";
            return "요청을 처리하지 못했어요";
        }

        // 파일 업로드(multipart/form-data). 캡처 저장 3종이 전부 이 방식이다.
        //   POST …/stage-sessions/{id}/captures            image · capturedPositionSeconds · capturedAt
        //   POST …/track-sessions/{id}/captures            같음
        //   POST …/avatar-interaction-sessions/{id}/pictures  image · picturedAt
        //
        // fields 는 image 외의 텍스트 필드다. 파일은 fileField/fileName/bytes 로 준다.
        // ⚠ Content-Type 을 손으로 넣지 말 것 — boundary 가 빠져서 서버가 못 읽는다.
        //   UnityWebRequest.Post(url, sections) 가 알아서 붙인다.
        public static void Upload(string path, string fileField, string fileName, byte[] bytes,
                                  string contentType, Dictionary<string, string> fields,
                                  Action<string> done, Action<string> fail)
            => Host.StartCoroutine(SendUpload(path, fileField, fileName, bytes, contentType, fields, done, fail));

        static IEnumerator SendUpload(string path, string fileField, string fileName, byte[] bytes,
                                      string contentType, Dictionary<string, string> fields,
                                      Action<string> done, Action<string> fail)
        {
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogWarning($"[Api] UPLOAD {path} → 보낼 파일이 비어 있다");
                fail?.Invoke("보낼 사진이 없어요");
                yield break;
            }

            string url = ApiConfig.Url(path);
            float t0 = Time.realtimeSinceStartup;

            var sections = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection(fileField, bytes, fileName, contentType),
            };
            if (fields != null)
                foreach (var kv in fields)
                    sections.Add(new MultipartFormDataSection(kv.Key, kv.Value));

            using (var req = UnityWebRequest.Post(url, sections))
            {
                req.timeout = ApiConfig.ImageTimeoutSeconds;
                ApiConfig.LoadSavedToken();
                if (!string.IsNullOrEmpty(ApiConfig.AccessToken))
                    req.SetRequestHeader("Authorization", "Bearer " + ApiConfig.AccessToken);

                yield return req.SendWebRequest();

                float ms = (Time.realtimeSinceStartup - t0) * 1000f;
                long code = req.responseCode;
                string text = req.downloadHandler != null ? req.downloadHandler.text : "";

                if (req.result == UnityWebRequest.Result.ConnectionError || code == 0)
                {
                    Debug.LogWarning($"[Api] UPLOAD {path} → 연결 실패 ({ms:F0}ms): {req.error}");
                    fail?.Invoke("네트워크에 연결할 수 없어요");
                    yield break;
                }

                if (code >= 400)
                {
                    Debug.LogWarning($"[Api] UPLOAD {path} ({bytes.Length}B) → {code} ({ms:F0}ms) {text}");
                    if (code == 401) HandleExpiredToken();
                    fail?.Invoke(ParseError(text, code));
                    yield break;
                }

                Debug.Log($"[Api] UPLOAD {path} ({bytes.Length}B) → {code} ({ms:F0}ms)");
                done?.Invoke(text);
            }
        }

        // 이미지 1장 받기. 갤러리 말고도 마켓·커뮤니티·홈이 쓰므로 여기 둔다.
        // 캐시는 ImageCache 가 맡는다 — 여기는 받아오기만 한다.
        public static void GetTexture(string url, Action<Texture2D> done, Action<string> fail)
            => Host.StartCoroutine(SendTexture(url, done, fail));

        // 음원 1곡 받기 (2026-08-26 스트리밍 전환). mp3 를 스트림 클립으로 만든다.
        // ⚠ streamAudio 클립은 다운로드 핸들러의 압축 데이터를 재생 중에 디코드한다 —
        //   요청을 Dispose 하면 클립이 죽으므로 성공 시엔 요청을 살려둔 채 넘긴다.
        //   (호출부 = ApiMediaSource 가 클립과 함께 캐시해 앱이 사는 동안 유지)
        public static void GetAudioClip(string url, Action<AudioClip> done, Action<string> fail)
            => Host.StartCoroutine(SendAudio(url, done, fail));

        static IEnumerator SendAudio(string url, Action<AudioClip> done, Action<string> fail)
        {
            AudioType type = url.EndsWith(".wav") ? AudioType.WAV
                           : url.EndsWith(".ogg") ? AudioType.OGGVORBIS
                           : AudioType.MPEG;                       // 서버 실측: 전부 .mp3
            var req = UnityWebRequestMultimedia.GetAudioClip(url, type);
            ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = true;  // 디코드 전체(곡당 ~16MB) 대신 압축(~3MB)만 메모리에
            req.timeout = ApiConfig.MediaTimeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string err = req.error;
                req.Dispose();
                Debug.LogWarning($"[Api] AUDIO {url} → 실패: {err}");
                fail?.Invoke(err);
                yield break;
            }

            AudioClip clip = null;
            try { clip = DownloadHandlerAudioClip.GetContent(req); }
            catch (Exception e) { Debug.LogWarning($"[Api] AUDIO 디코드 실패: {e.Message}"); }
            if (clip == null) { req.Dispose(); fail?.Invoke("음원을 읽지 못했어요"); yield break; }
            done?.Invoke(clip);                                    // req 는 의도적으로 살려둔다 (위 주석)
        }

        static IEnumerator SendTexture(string url, Action<Texture2D> done, Action<string> fail)
        {
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                req.timeout = ApiConfig.ImageTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Api] IMG {url} → 실패: {req.error}");
                    fail?.Invoke(req.error);
                    yield break;
                }

                var tex = DownloadHandlerTexture.GetContent(req);
                if (tex == null)
                {
                    fail?.Invoke("이미지를 읽지 못했어요");
                    yield break;
                }
                done?.Invoke(tex);
            }
        }
    }
}
