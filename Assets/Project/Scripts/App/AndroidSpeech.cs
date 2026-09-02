using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ARZone
{
    // 안드로이드 내장 음성 인식(SpeechRecognizer) 브리지 (2026-08-30 유저 지시).
    // 서버에 STT API 가 없어(백엔드 문의) 폰에서 말→텍스트를 만들고, 텍스트는 기존 대화 API 로 보낸다.
    // 플러그인 없이 JNI 로 직접 부른다. 인식기 호출은 안드로이드 UI 스레드에서,
    // 결과 콜백은 큐로 받아 유니티 메인 스레드(Update)에서 되돌려준다.
    public class AndroidSpeech : MonoBehaviour
    {
        static AndroidSpeech inst;

        public static bool IsListening { get; private set; }

        Action<string> onResult;
        Action<string> onError;
        // 듣기 '세대' 번호. 안드로이드(특히 삼성)는 인식기를 destroy 하거나 결과를 준 뒤에도
        // onError 를 한 번 더 쏘는데, 그게 다음 듣기의 콜백으로 들어가 "두 번째 시도마다 오류"가
        // 됐다 (2026-08-31 실기). 리스너마다 자기 세대를 기억해 두고, 현재 세대가 아니면 버린다.
        int gen;
        int retriesLeft;   // 듣기 1회당 일시 오류(BUSY/CLIENT/11) 자동 재시도 잔여 횟수
        readonly Queue<Action> queue = new Queue<Action>();
#if UNITY_ANDROID
        AndroidJavaObject recognizer;
#endif

        // 듣기 시작. 결과(인식된 문장) 또는 실패 사유 중 하나만 온다.
        public static void Listen(Action<string> result, Action<string> error)
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                error?.Invoke("음성 인식은 안드로이드 기기에서만 동작해요");
                return;
            }
            if (inst == null)
            {
                var go = new GameObject("AndroidSpeech");
                DontDestroyOnLoad(go);
                inst = go.AddComponent<AndroidSpeech>();
            }
            inst.StartListen(result, error);
        }

        // 듣기를 끝낸다(말 다 함) — 그때까지 들은 것으로 결과 콜백이 온다.
        public static void StopListen()
        {
#if UNITY_ANDROID
            if (inst == null || inst.recognizer == null) return;
            var activity = Activity();
            var rec = inst.recognizer;
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                try { rec.Call("stopListening"); } catch (Exception) { }
            }));
#endif
        }

        void StartListen(Action<string> result, Action<string> error)
        {
            onResult = result;
            onError = error;
            gen++;
            retriesLeft = 2;
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                var cb = new PermissionCallbacks();
                cb.PermissionGranted += _ => Begin();
                cb.PermissionDenied += _ => Fail("마이크 권한이 필요해요");
                cb.PermissionDeniedAndDontAskAgain += _ => Fail("설정에서 마이크 권한을 켜주세요");
                Permission.RequestUserPermission(Permission.Microphone, cb);
                return;
            }
            Begin();
#else
            Fail("음성 인식은 안드로이드 기기에서만 동작해요");
#endif
        }

#if UNITY_ANDROID
        static AndroidJavaObject Activity()
            => new AndroidJavaClass("com.unity3d.player.UnityPlayer")
               .GetStatic<AndroidJavaObject>("currentActivity");

        // ⚠ 인식기는 한 번만 만들어 재사용한다 (2026-08-31 실기 결론).
        //   듣기마다 destroy→create 를 하면 삼성 인식 서비스와의 연결이 매번 끊겨
        //   다음 듣기가 오류 5(CLIENT)/11(SERVER_DISCONNECTED)로 튕긴다 — "두 번째마다 오류"의 진짜 원인.
        //   재사용 중엔 cancel→startListening 만 부르고, 오류가 난 인식기만 부수고 새로 만든다.
        void Begin()
        {
            IsListening = true;
            var activity = Activity();
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                try
                {
                    var srClass = new AndroidJavaClass("android.speech.SpeechRecognizer");
                    if (!srClass.CallStatic<bool>("isRecognitionAvailable", activity))
                    {
                        Post(() => Fail("이 기기엔 음성 인식 서비스가 없어요"));
                        return;
                    }
                    if (recognizer == null)
                    {
                        recognizer = srClass.CallStatic<AndroidJavaObject>("createSpeechRecognizer", activity);
                        recognizer.Call("setRecognitionListener", new RecListener(this, gen));
                    }
                    else
                    {
                        // 직전 세션 잔여물 정리 — cancel 은 연결을 유지한 채 콜백만 멈춘다
                        try { recognizer.Call("cancel"); } catch (Exception) { }
                        recognizer.Call("setRecognitionListener", new RecListener(this, gen));
                    }

                    var intent = new AndroidJavaObject("android.content.Intent",
                                                       "android.speech.action.RECOGNIZE_SPEECH");
                    intent.Call<AndroidJavaObject>("putExtra",
                        "android.speech.extra.LANGUAGE_MODEL", "free_form");
                    intent.Call<AndroidJavaObject>("putExtra",
                        "android.speech.extra.LANGUAGE", "ko-KR");
                    recognizer.Call("startListening", intent);
                }
                catch (Exception e)
                {
                    Post(() => Fail("음성 인식 시작 실패: " + e.Message));
                }
            }));
        }

        // 오류가 난 인식기는 재사용하지 않는다 — 부수고 나서 null 로 두면 다음 Begin 이 새로 만든다.
        void TearDownRecognizer()
        {
            var rec = recognizer;
            recognizer = null;
            if (rec == null) return;
            var activity = Activity();
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                try { rec.Call("destroy"); } catch (Exception) { }
                try { rec.Dispose(); } catch (Exception) { }
            }));
        }
#endif

        // 일시 오류 뒤 재시도 — 죽은 인식기를 치우고, 서비스가 재연결할 시간을 주고 새로 만든다.
        // gen 을 올려서 방금 튕긴 리스너를 지난 세대로 만든 뒤 Begin (콜백은 그대로 유지).
        IEnumerator CoRetry()
        {
#if UNITY_ANDROID
            TearDownRecognizer();
#endif
            yield return new WaitForSeconds(0.6f);
            if (onResult == null) yield break;   // 그 사이 듣기가 끝났거나 취소됨
            gen++;
#if UNITY_ANDROID
            Begin();
#endif
        }

        void Post(Action a) { lock (queue) queue.Enqueue(a); }

        void Update()
        {
            while (true)
            {
                Action a = null;
                lock (queue) { if (queue.Count > 0) a = queue.Dequeue(); }
                if (a == null) break;
                a();
            }
        }

        void Fail(string msg)
        {
            IsListening = false;
            Debug.LogWarning($"[Speech] 실패(세대 {gen}): {msg}");   // 실기 진단용 — 토스트만으론 로그에 안 남는다
            var cb = onError; onError = null; onResult = null;
            cb?.Invoke(msg);
        }

        void Result(string text)
        {
            IsListening = false;
            Debug.Log($"[Speech] 인식(세대 {gen}): {text}");
            var cb = onResult; onResult = null; onError = null;
            cb?.Invoke(text);
        }

#if UNITY_ANDROID
        class RecListener : AndroidJavaProxy
        {
            readonly AndroidSpeech host;
            readonly int myGen;   // 이 리스너가 속한 듣기 세대 — 지나간 세대의 콜백은 버린다
            public RecListener(AndroidSpeech h, int g) : base("android.speech.RecognitionListener")
            { host = h; myGen = g; }

            public void onResults(AndroidJavaObject bundle)
            {
                string text = null;
                try
                {
                    var list = bundle.Call<AndroidJavaObject>("getStringArrayList", "results_recognition");
                    if (list != null && list.Call<int>("size") > 0) text = list.Call<string>("get", 0);
                }
                catch (Exception) { }
                var t = text;
                host.Post(() =>
                {
                    if (myGen != host.gen) return;   // 옛 인식기의 늦은 결과 — 무시
                    if (string.IsNullOrEmpty(t)) host.Fail("못 알아들었어요. 다시 말해 주세요");
                    else host.Result(t);
                });
            }

            public void onError(int error) => host.Post(() =>
            {
                if (myGen != host.gen) return;       // destroy·종료가 쏘는 늦은 에러 — 무시
                // 일시 오류는 토스트 대신 잠깐 뒤 한 번 자동 재시도한다 (사용자 잘못이 아님):
                //   8 BUSY · 5 CLIENT — 서비스가 직전 세션을 아직 안 놓음
                //   11 SERVER_DISCONNECTED — 삼성 실기: 인식 직후 바로 다시 부르면 재연결 전이라 튕김
                //     (2026-08-31 로그: 세대2·4 가 전부 11, 다음 시도는 성공)
                if ((error == 8 || error == 5 || error == 11) && host.retriesLeft > 0)
                {
                    host.retriesLeft--;
                    Debug.Log($"[Speech] 일시 오류({error}) — 0.6초 뒤 자동 재시도 (잔여 {host.retriesLeft})");
                    host.StartCoroutine(host.CoRetry());
                    return;
                }
                host.Fail(Msg(error));
            });

            static string Msg(int e)
            {
                switch (e)
                {
                    case 6: return "말소리가 들리지 않았어요";          // SPEECH_TIMEOUT
                    case 7: return "못 알아들었어요. 다시 말해 주세요"; // NO_MATCH
                    case 9: return "마이크 권한이 필요해요";            // INSUFFICIENT_PERMISSIONS
                    default: return $"음성 인식 오류 ({e})";
                }
            }

            // 나머지 리스너 콜백 — 쓰지 않지만 프록시라 서명은 있어야 한다
            public void onReadyForSpeech(AndroidJavaObject p) { }
            public void onBeginningOfSpeech() { }
            public void onRmsChanged(float rmsdB) { }
            public void onBufferReceived(byte[] buffer) { }
            public void onEndOfSpeech() { }
            public void onPartialResults(AndroidJavaObject partialResults) { }
            public void onEvent(int eventType, AndroidJavaObject @params) { }
        }
#endif
    }
}
