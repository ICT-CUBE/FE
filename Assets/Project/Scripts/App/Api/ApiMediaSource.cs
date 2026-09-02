using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // stage / track 재생 세션·캡처 기록 (2026-08-25 배선).
    //
    // 화면에는 아무것도 안 보이는 'DB 기록용' 호출들이라 Store/이벤트 없이 정적으로 둔다.
    // 전부 실패해도 화면 흐름은 그대로다 — 로그만 남긴다.
    //
    // ── 식별자 매칭 ──
    // 서버 스테이지/트랙 id 와 앱 로컬 데이터(무대 제목, clipKey)를 잇는 유일한 지점.
    //   무대:  씬 제목 "Mantra" ↔ 서버 "Mantra MV"        → 정규화 후 포함 비교
    //   트랙:  clipKey "like_jennie" ↔ 서버 "like JENNIE"  → 정규화 후 포함 비교
    //   (정규화 = 소문자 + 영숫자만. "Intro : JANE (with FKJ)" ⊃ "intro_jane" 도 잡힌다)
    public static class ApiMediaSource
    {
        // 지금 열려 있는 서버 세션 id 들. 0 = 없음.
        public static int StageSessionId;        // AR/일반 무대 관람
        public static int TrackNormalSessionId;  // 일반 감상 (음악 상세)
        public static int TrackSpecialSessionId; // AR 감상 (특별 재생)

        static List<StageListItemDto> stages;    // 서버 목록 캐시 (앱 켜져 있는 동안)
        static List<AlbumTrackDto> tracks;

        static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in s.ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        // 로컬↔서버 제목 대조를 밖에서도 쓴다 — AR재경험(갤러리→무대 선택)이 같은 규칙이어야
        // 세션 기록과 스트리밍이 고르는 무대가 어긋나지 않는다.
        public static bool TitleMatch(string local, string server) => Match(local, server);

        static bool Match(string local, string server)
        {
            string a = Norm(local), b = Norm(server);
            if (a.Length == 0 || b.Length == 0) return false;
            if (a.Contains(b) || b.Contains(a)) return true;
            // 서버 무대 제목은 "Love Hangover MV" 처럼 MV 가 붙는다. 로컬이
            // "JENNIE & Dominic Fike - Love Hangover" 같은 긴 표기면 서로 포함이 안 되므로
            // MV 를 떼고 한 번 더 본다 (실측: 이 규칙까지 넣어야 스테이지가 다 잡힌다).
            if (b.EndsWith("mv"))
            {
                string b2 = b.Substring(0, b.Length - 2);
                if (b2.Length > 0 && (a.Contains(b2) || b2.Contains(a))) return true;
            }
            return false;
        }

        // ── 목록 캐시 ──

        // videoType 별 서버 목록 (2026-08-30: GET /stages?videoType=MUSIC_VIDEO|CONCERT_VIDEO).
        // 목록 화면이 표시용(썸네일·제목·설명·연도)으로 쓴다. 실패·미로그인이면 null 이 간다.
        static List<StageListItemDto> stagesMv, stagesConcert;

        public static void LoadStages(bool concert, Action<List<StageListItemDto>> done)
        {
            var cached = concert ? stagesConcert : stagesMv;
            if (cached != null) { done?.Invoke(cached); return; }

            string type = concert ? "CONCERT_VIDEO" : "MUSIC_VIDEO";
            ApiClient.Get($"api/v1/stages?videoType={type}", json =>
            {
                try
                {
                    var l = new List<StageListItemDto>(JsonUtility.FromJson<StageListDto>(json).stages);
                    if (concert) stagesConcert = l; else stagesMv = l;
                    // 제목 매칭(세션·스트리밍)용 합본 — 어느 탭 무대든 찾을 수 있어야 한다
                    stages = new List<StageListItemDto>();
                    if (stagesMv != null) stages.AddRange(stagesMv);
                    if (stagesConcert != null) stages.AddRange(stagesConcert);
                }
                catch (Exception e) { Debug.LogWarning($"[Media] 스테이지 목록 파싱 실패: {e.Message}"); }
                done?.Invoke(concert ? stagesConcert : stagesMv);
            }, err => { Debug.LogWarning($"[Media] 스테이지 목록({type}) 실패: {err}"); done?.Invoke(null); });
        }

        static void EnsureStages(Action done)
        {
            if (stages != null) { done(); return; }
            LoadStages(false, _ => LoadStages(true, _2 => done()));
        }

        static void EnsureTracks(Action done)
        {
            if (tracks != null) { done(); return; }
            ApiClient.Get("api/v1/album", json =>
            {
                try { tracks = new List<AlbumTrackDto>(JsonUtility.FromJson<AlbumDto>(json).album.tracks); }
                catch (Exception e) { Debug.LogWarning($"[Media] 앨범 파싱 실패: {e.Message}"); }
                done();
            }, err => { Debug.LogWarning($"[Media] 앨범 실패: {err}"); done(); });
        }

        // ── 스트리밍 (2026-08-26 전환 확정) ──
        // 실패하면 done(null) — 호출부는 로컬 클립을 그대로 쓴다 (서버 우선 + 로컬 폴백).

        static readonly Dictionary<int, string> stageVideoUrls = new Dictionary<int, string>();
        static readonly Dictionary<int, string> trackAudioUrls = new Dictionary<int, string>();
        // 서버 음원 클립 캐시. streamAudio 클립이라 곡당 압축 크기(~3MB)만 차지한다 —
        // 전 곡(15) 담아도 ~45MB 라 내보내지 않는다 (내보내려면 재생 중 여부 추적이 필요해 오히려 위험).
        static readonly Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>();

        // 무대 영상 URL. localTitle = 씬 StageData.title. 목록·상세 캐시가 있으면 동기 콜백.
        public static void GetStageVideoUrl(string localTitle, Action<string> done)
        {
            EnsureStages(() =>
            {
                var hit = stages?.Find(s => Match(localTitle, s.title));
                if (hit == null) { done(null); return; }
                if (stageVideoUrls.TryGetValue(hit.id, out var cached)) { done(cached); return; }
                ApiClient.Get($"api/v1/stages/{hit.id}", json =>
                {
                    try
                    {
                        var d = JsonUtility.FromJson<StageDetailDto>(json).stage;
                        stageVideoUrls[hit.id] = string.IsNullOrEmpty(d.videoUrl) ? null : d.videoUrl;
                        done(stageVideoUrls[hit.id]);
                    }
                    catch (Exception e)
                    { Debug.LogWarning($"[Media] 무대 상세 파싱 실패: {e.Message}"); done(null); }
                }, err => { Debug.LogWarning($"[Media] 무대 {hit.id} 상세 실패: {err}"); done(null); });
            });
        }

        // 트랙 서버 음원. clipKey 로 서버 트랙을 찾아 mp3 를 받아 클립으로 준다.
        public static void GetTrackAudio(string clipKey, Action<AudioClip> done)
        {
            if (string.IsNullOrEmpty(clipKey)) { done(null); return; }
            if (audioClips.TryGetValue(clipKey, out var hitClip)) { done(hitClip); return; }

            EnsureTracks(() =>
            {
                var hit = tracks?.Find(t => Match(clipKey, t.title));
                if (hit == null)
                {
                    Debug.LogWarning($"[Media] clipKey '{clipKey}' 에 맞는 서버 트랙이 없다 — 로컬 재생 유지");
                    done(null); return;
                }
                if (trackAudioUrls.TryGetValue(hit.id, out var url)) { Fetch(url); return; }
                ApiClient.Get($"api/v1/tracks/{hit.id}", json =>
                {
                    try
                    {
                        var d = JsonUtility.FromJson<TrackDetailDto>(json).track;
                        trackAudioUrls[hit.id] = string.IsNullOrEmpty(d.audioUrl) ? null : d.audioUrl;
                        Fetch(trackAudioUrls[hit.id]);
                    }
                    catch (Exception e)
                    { Debug.LogWarning($"[Media] 트랙 상세 파싱 실패: {e.Message}"); done(null); }
                }, err => { Debug.LogWarning($"[Media] 트랙 {hit.id} 상세 실패: {err}"); done(null); });

                void Fetch(string u)
                {
                    if (string.IsNullOrEmpty(u)) { done(null); return; }
                    ApiClient.GetAudioClip(u, clip =>
                    {
                        audioClips[clipKey] = clip;
                        Debug.Log($"[Media] 서버 음원 수신 '{clipKey}' ({clip.length:0}s)");
                        done(clip);
                    }, _ => done(null));
                }
            });
        }

        // ── 무대 세션 ──

        // 무대 관람이 시작될 때. localTitle = 씬 StageData.title ("Mantra" 등)
        public static void BeginStage(string localTitle, bool ar)
        {
            if (StageSessionId > 0) return;      // 이미 열려 있으면 그대로 (중복 생성 방지)

            EnsureStages(() =>
            {
                var hit = stages?.Find(s => Match(localTitle, s.title));
                if (hit == null)
                {
                    Debug.LogWarning($"[Media] '{localTitle}' 에 맞는 서버 스테이지가 없다 — 세션 기록 생략");
                    return;
                }
                var body = JsonUtility.ToJson(new StageSessionReq { viewMode = ar ? "AR" : "NORMAL" });
                ApiClient.Post($"api/v1/stages/{hit.id}/sessions", body, json =>
                {
                    try
                    {
                        StageSessionId = JsonUtility.FromJson<StageSessionCreateDto>(json).stageSession.id;
                        Debug.Log($"[Media] 무대 세션 시작 id={StageSessionId} ({hit.title}, {(ar ? "AR" : "NORMAL")})");
                    }
                    catch (Exception e) { Debug.LogWarning($"[Media] 무대 세션 응답 파싱 실패: {e.Message}"); }
                }, err => Debug.LogWarning($"[Media] 무대 세션 생성 실패: {err}"));
            });
        }

        public static void EndStage(float lastPositionSec, float viewedSec)
        {
            if (StageSessionId <= 0) return;
            int sid = StageSessionId;
            StageSessionId = 0;
            var body = JsonUtility.ToJson(new MediaSessionSaveReq
            {
                status = "COMPLETED",
                lastPositionSeconds = Mathf.Max(0, (int)lastPositionSec),
                viewedSeconds = Mathf.Max(0, (int)viewedSec),
            });
            ApiClient.Patch($"api/v1/users/me/stage-sessions/{sid}", body,
                _ => Debug.Log($"[Media] 무대 세션 종료 id={sid}"),
                err => Debug.LogWarning($"[Media] 무대 세션 종료 실패: {err}"));
        }

        public static void UploadStageCapture(byte[] png, float positionSec, DateTime at)
        {
            if (StageSessionId <= 0)
            {
                Debug.LogWarning("[Media] 무대 세션이 없어 캡처 업로드 생략");
                return;
            }
            var fields = new Dictionary<string, string>
            {
                { "capturedPositionSeconds", Mathf.Max(0, (int)positionSec).ToString() },
                { "capturedAt", ApiConfig.Iso(at) },
            };
            ApiClient.Upload($"api/v1/users/me/stage-sessions/{StageSessionId}/captures",
                             "image", "capture.png", png, "image/png", fields,
                             _ => Debug.Log("[Media] 무대 캡처 업로드 완료"),
                             err => Debug.LogWarning($"[Media] 무대 캡처 업로드 실패: {err}"));
        }

        // ── 트랙 세션 ──

        // special = true 면 AR 감상(특별 재생), false 면 일반 감상.
        public static void BeginTrack(string clipKey, bool special)
        {
            int cur = special ? TrackSpecialSessionId : TrackNormalSessionId;
            if (cur > 0) return;

            EnsureTracks(() =>
            {
                var hit = tracks?.Find(t => Match(clipKey, t.title));
                if (hit == null)
                {
                    Debug.LogWarning($"[Media] clipKey '{clipKey}' 에 맞는 서버 트랙이 없다 — 세션 기록 생략");
                    return;
                }
                var body = JsonUtility.ToJson(new TrackSessionReq { playMode = special ? "SPECIAL" : "NORMAL" });
                ApiClient.Post($"api/v1/tracks/{hit.id}/sessions", body, json =>
                {
                    try
                    {
                        int sid = JsonUtility.FromJson<TrackSessionCreateDto>(json).trackSession.id;
                        if (special) TrackSpecialSessionId = sid; else TrackNormalSessionId = sid;
                        Debug.Log($"[Media] 트랙 세션 시작 id={sid} ({hit.title}, {(special ? "SPECIAL" : "NORMAL")})");
                    }
                    catch (Exception e) { Debug.LogWarning($"[Media] 트랙 세션 응답 파싱 실패: {e.Message}"); }
                }, err => Debug.LogWarning($"[Media] 트랙 세션 생성 실패: {err}"));
            });
        }

        // cancelled: 감상 도중 모드 전환(NORMAL↔SPECIAL) 등으로 끊긴 세션은 CANCELLED 로 보낸다.
        // (2026-08-28 백엔드 정책: 모드 변경 시 기존 세션은 취소하고 새 세션을 만든다)
        public static void EndTrack(bool special, float lastPositionSec, float viewedSec,
                                    bool cancelled = false)
        {
            int sid = special ? TrackSpecialSessionId : TrackNormalSessionId;
            if (sid <= 0) return;
            if (special) TrackSpecialSessionId = 0; else TrackNormalSessionId = 0;

            var body = JsonUtility.ToJson(new MediaSessionSaveReq
            {
                status = cancelled ? "CANCELLED" : "COMPLETED",
                lastPositionSeconds = Mathf.Max(0, (int)lastPositionSec),
                viewedSeconds = Mathf.Max(0, (int)viewedSec),
            });
            ApiClient.Patch($"api/v1/users/me/track-sessions/{sid}", body,
                _ => Debug.Log($"[Media] 트랙 세션 종료 id={sid}"),
                err => Debug.LogWarning($"[Media] 트랙 세션 종료 실패: {err}"));
        }

        public static void UploadTrackCapture(byte[] png, float positionSec, DateTime at)
        {
            int sid = TrackSpecialSessionId;     // 캡처는 AR 감상(특별 재생)에서만 나온다
            if (sid <= 0)
            {
                Debug.LogWarning("[Media] 트랙 세션이 없어 캡처 업로드 생략");
                return;
            }
            var fields = new Dictionary<string, string>
            {
                { "capturedPositionSeconds", Mathf.Max(0, (int)positionSec).ToString() },
                { "capturedAt", ApiConfig.Iso(at) },
            };
            ApiClient.Upload($"api/v1/users/me/track-sessions/{sid}/captures",
                             "image", "capture.png", png, "image/png", fields,
                             _ => Debug.Log("[Media] 트랙 캡처 업로드 완료"),
                             err => Debug.LogWarning($"[Media] 트랙 캡처 업로드 실패: {err}"));
        }
    }
}
