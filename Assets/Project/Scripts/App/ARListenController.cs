using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // AR 감상. 상세에서 고른 곡을 이어받아, 아바타 위로 가사 세 줄과 하단 미니 플레이어를 보여준다.
    // 가사는 Resources/Lyrics/<곡키>.txt(LRC) 에서 읽는다 — 파일이 비어 있으면 줄은 비워둔다.
    public class ARListenController : MonoBehaviour
    {
        // 시안: 위에서부터 현재 줄(흰색) + 다음 두 줄(회색)
        [Header("가사 세 줄")]
        [SerializeField] TMP_Text lineNow;
        [SerializeField] TMP_Text lineNext;
        [SerializeField] TMP_Text lineNext2;

        [Header("미니 플레이어")]
        [SerializeField] TMP_Text miniTitle;
        [SerializeField] Button btnPlayPause;
        [SerializeField] Image iconPlayPause;
        [SerializeField] Sprite icoPlay;
        [SerializeField] Sprite icoPause;
        [SerializeField] RectTransform progressFill;
        [SerializeField] float trackWidth = 340f;      // 시안 실측
        [SerializeField] TMP_Text timeNow;
        [SerializeField] TMP_Text timeTotal;
        [SerializeField] SeekBar seekBar;     // MiniTrack 에 붙인다

        [Header("우측 버튼")]
        [SerializeField] Button btnDownload;
        [SerializeField] Button btnCapture;
        [SerializeField] CaptureService capture;

        // 감상 리포트로 넘길 값 — '머문 시간' 기준(일시정지 중에도 센다, 유저 지시)
        public static float LastWatchSeconds;
        public static int LastShotCount;

        bool finishedToReport;   // 곡을 끝까지 들어 리포트로 넘어갔는가 (세션 COMPLETED 판정용)

        [Header("종료 처리")]
        [SerializeField] string reportScreen = "ListenReport";   // 곡이 끝나면 감상 리포트로

        [Header("곡 (상세에서 넘어온다)")]
        [SerializeField] string title = "";
        [SerializeField] string artist = "";
        [SerializeField] string lyricsKey = "";      // 비우면 clipKey 로 유추
        [SerializeField] string clipKey = "";     // Resources/Audio/<clipKey>.mp3

        static readonly Regex Stamp = new Regex(@"^\s*\[(\d+):(\d+(?:[.:]\d+)?)\]\s*(.*)$");
        readonly List<(float time, string text)> lines = new List<(float, string)>();

        AudioSource audioSrc;
        int active = -1;
        float dwell;            // 이 화면에 머문 시간
        bool wasPlaying;        // 곡 종료 감지
        bool pausedByUser;

        void Awake()
        {
            audioSrc = GetComponent<AudioSource>();
            if (audioSrc == null) audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
        }

        void OnEnable()
        {
            // 상세가 넘긴 곡 정보를 그대로 쓴다
            if (!string.IsNullOrEmpty(MusicDetailController.PendingTitle))
            {
                title = MusicDetailController.PendingTitle;
                artist = MusicDetailController.PendingArtist;
            }
            if (!string.IsNullOrEmpty(MusicDetailController.PendingClipKey))
                clipKey = MusicDetailController.PendingClipKey;

            // ★ 일반 감상은 이제 화면을 떠나도 계속 재생된다(AudioSource 를 MusicPlayer 가
            //   소유하도록 바꿨다 — 가사 보기에서 노래가 끊기던 문제 때문).
            //   여기서 멈춰주지 않으면 AR 감상 음원과 겹쳐서 두 곡이 동시에 난다.
            if (MusicPlayer.Instance != null) MusicPlayer.Instance.Pause();

            // 일반 감상에서 듣던 그 자리에서 이어 재생한다 (총 시간·재생바가 여기서 나온다)
            if (audioSrc != null)
            {
                var c = string.IsNullOrEmpty(clipKey) ? null : Resources.Load<AudioClip>("Audio/" + clipKey);
                if (c == null && !string.IsNullOrEmpty(clipKey))
                    Debug.LogWarning($"[ARListen] Resources/Audio/{clipKey} 를 못 찾음");
                audioSrc.clip = c;
                if (c != null)
                {
                    float at = MusicDetailController.PendingTime;
                    audioSrc.time = at >= 0f ? Mathf.Clamp(at, 0f, c.length - 0.05f) : 0f;
                    if (MusicDetailController.PendingPlaying) audioSrc.Play();
                }
                MusicDetailController.PendingTime = -1f;
                MusicDetailController.PendingPlaying = false;

                // 서버 스트리밍 (2026-08-26 전환) — 도착하면 재생 위치를 지킨 채 갈아끼운다.
                // 실패하면 로컬 그대로. 로컬이 아예 없던 곡도 서버 음원이 오면 재생 가능해진다.
                ApiMediaSource.GetTrackAudio(clipKey, clip =>
                {
                    if (clip == null || audioSrc == null || audioSrc.clip == clip) return;
                    if (!isActiveAndEnabled) return;              // 화면을 떠났으면 손대지 않는다
                    float at = audioSrc.clip != null ? audioSrc.time : 0f;
                    bool playing = audioSrc.isPlaying;
                    audioSrc.clip = clip;
                    audioSrc.time = Mathf.Clamp(at, 0f, clip.length - 0.05f);
                    if (playing) audioSrc.Play();
                    Debug.Log($"[ARListen] '{clipKey}' 서버 음원으로 전환");
                });

                // ★ 립싱크 아바타 (2026-08-27 유저 요청 "가사 맞춰서 입모양") —
                // 카메라 앞 1.2m 에 아바타를 세우고, 가사 줄이 활성인 동안 음량으로 입을 움직인다.
                // ★ 바로 배치하면 안 된다 — OnEnable 은 ScreenChanged(ARFlow 가 세션을 켜는 시점)보다
                //   먼저 돌아서, Camera.main 이 아직 UI 카메라다. 그 앞에 놓으면 AR 리그가 생긴 뒤
                //   카메라(원점 시작)에서 안 보인다 (2026-08-28 실기: 카메라는 뜨는데 아바타가 없음).
                StartCoroutine(CoPlaceLipSyncAvatar());
            }
            if (!string.IsNullOrEmpty(LyricsController.PendingKey))
                lyricsKey = LyricsController.PendingKey;
            // 넘어온 게 없으면 곡 키로 유추 (예전엔 만트라 가사가 고정으로 떴다)
            if (string.IsNullOrEmpty(lyricsKey) && !string.IsNullOrEmpty(clipKey))
                lyricsKey = "Lyrics/" + clipKey;

            // 서버 트랙 세션 — AR 감상 = 특별 재생(SPECIAL) (2026-08-25 배선)
            finishedToReport = false;
            ApiMediaSource.BeginTrack(clipKey, special: true);

            if (miniTitle != null) miniTitle.text = title;

            if (btnPlayPause != null) btnPlayPause.onClick.AddListener(TogglePlay);
            if (btnCapture != null) btnCapture.onClick.AddListener(Shoot);
            if (btnDownload != null) btnDownload.onClick.AddListener(Shoot);

            LoadLyrics();
            active = -1;
            ShowLines(0);

            pausedByUser = false;
            wasPlaying = audioSrc != null && audioSrc.isPlaying;
            dwell = 0f;
            if (seekBar != null) seekBar.Seeked += SeekTo;
        }

        // 카메라 앞에 아바타를 세우고 립싱크를 건다. AR 세션/아바타가 없으면 조용히 넘어간다
        // (에디터·권한 거부 등 — 가사·음악 흐름은 그대로 돌아야 한다).
        // AR 세션(리그·카메라)이 생길 때까지 기다렸다가 배치한다. 5초가 지나면
        // (에디터 등 AR 이 없는 환경) 그냥 배치해 기존 동작을 유지한다.
        System.Collections.IEnumerator CoPlaceLipSyncAvatar()
        {
            var session = FindFirstObjectByType<ARSessionController>();
            float t0 = Time.realtimeSinceStartup;
            while (session != null && !session.IsRunning && Time.realtimeSinceStartup - t0 < 5f)
                yield return null;
            yield return null;   // AR 카메라가 첫 포즈를 잡을 한 프레임 여유
            PlaceLipSyncAvatar();
        }

        void PlaceLipSyncAvatar()
        {
            var session = FindFirstObjectByType<ARSessionController>();
            var cam = Camera.main;
            if (session == null || cam == null) return;

            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            // 폰을 가슴 높이(~1.2m)로 든다고 보고 바닥 근처에 세운다. 마주 보게 돌린다.
            Vector3 pos = cam.transform.position + fwd * 1.2f + Vector3.down * 1.2f;
            session.PlaceAvatar(new Pose(pos, Quaternion.LookRotation(-fwd)));
            session.ShowAvatar(true);

            var avatar = session.Avatar;
            if (avatar == null) return;
            var lip = avatar.GetComponent<AvatarLipSync>();
            if (lip == null) lip = avatar.gameObject.AddComponent<AvatarLipSync>();
            AvatarBlink.Ensure(avatar.gameObject);
            lip.source = audioSrc;
            lip.gate = () => active >= 0 && active < lines.Count
                             && !string.IsNullOrWhiteSpace(lines[active].text);
        }

        void OnDisable()
        {
            var s = FindFirstObjectByType<ARSessionController>();
            if (s != null) s.ShowAvatar(false);               // 감상을 떠나면 아바타를 감춘다
            if (btnPlayPause != null) btnPlayPause.onClick.RemoveListener(TogglePlay);
            if (btnCapture != null) btnCapture.onClick.RemoveListener(Shoot);
            if (btnDownload != null) btnDownload.onClick.RemoveListener(Shoot);
            if (seekBar != null) seekBar.Seeked -= SeekTo;
            if (audioSrc != null)
            {
                MusicDetailController.PendingClipKey = clipKey;
                MusicDetailController.PendingTime = audioSrc.time;
                // 모드 변경은 이어 재생하지 않는다 — 멈춘 채 돌아가고 유저가 다시 플레이 (2026-08-28 정책)
                MusicDetailController.PendingPlaying = false;
                audioSrc.Pause();
            }
            // 끝까지 들어 리포트로 간 것만 COMPLETED, 도중 이탈(모드 복귀 포함)은 CANCELLED
            ApiMediaSource.EndTrack(special: true,
                audioSrc != null ? audioSrc.time : 0f, LastWatchSeconds,
                cancelled: !finishedToReport);
        }

        void LoadLyrics()
        {
            lines.Clear();
            var asset = Resources.Load<TextAsset>(lyricsKey);
            if (asset == null) return;

            foreach (var raw in asset.text.Split('\n'))
            {
                var s = raw.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(s) || s.TrimStart().StartsWith("#")) continue;
                var m = Stamp.Match(s);
                if (!m.Success) continue;      // AR 감상은 싱크된 줄만 쓴다
                float mm = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                float ss = float.Parse(m.Groups[2].Value.Replace(':', '.'), CultureInfo.InvariantCulture);
                lines.Add((mm * 60f + ss, m.Groups[3].Value));
            }
            lines.Sort((a, b) => a.time.CompareTo(b.time));
        }

        void SeekTo(float p)
        {
            if (audioSrc == null || audioSrc.clip == null) return;
            audioSrc.time = Mathf.Clamp(p * audioSrc.clip.length, 0f, audioSrc.clip.length - 0.05f);
            wasPlaying = audioSrc.isPlaying;
        }

        void Update()
        {
            dwell += Time.deltaTime;
            float len = (audioSrc != null && audioSrc.clip != null) ? audioSrc.clip.length : 0f;
            float now = (audioSrc != null) ? audioSrc.time : 0f;

            // 곡이 끝나면(사용자가 멈춘 게 아니면) 감상 리포트로
            bool nowPlaying = audioSrc != null && audioSrc.isPlaying;
            if (wasPlaying && !nowPlaying && !pausedByUser && audioSrc != null && audioSrc.clip != null)
            {
                wasPlaying = false;
                LastWatchSeconds = dwell;
                LastShotCount = capture != null ? capture.Count : 0;
                finishedToReport = true;   // 자연 종료 — 세션을 COMPLETED 로 닫는 유일한 경로
                if (ScreenManager.Instance != null && !string.IsNullOrEmpty(reportScreen))
                    ScreenManager.Instance.Show(reportScreen);
                return;
            }
            wasPlaying = nowPlaying;

            if (progressFill != null)
            {
                float p = len > 0.01f ? Mathf.Clamp01(now / len) : 0f;
                var s = progressFill.sizeDelta;
                progressFill.sizeDelta = new Vector2(trackWidth * p, s.y);
            }
            if (timeNow != null) timeNow.text = Fmt(now);
            if (timeTotal != null) timeTotal.text = Fmt(len);

            if (iconPlayPause != null)
            {
                bool playing = audioSrc != null && audioSrc.isPlaying;
                var want = playing ? icoPause : icoPlay;
                if (want != null && iconPlayPause.sprite != want) iconPlayPause.sprite = want;
            }

            // 현재 줄 갱신
            int idx = -1;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].time <= now) idx = i; else break;
            if (idx != active) { active = idx; ShowLines(idx); }
        }

        // 시안: 위 세 줄 — 이전/현재/다음. 현재만 밝게.
        void ShowLines(int idx)
        {
            Put(lineNow, At(idx), DesignTokens.Text.Primary);
            Put(lineNext, At(idx + 1), DesignTokens.Text.Tertiary);
            Put(lineNext2, At(idx + 2), DesignTokens.Text.Tertiary);
        }

        string At(int i) => (i >= 0 && i < lines.Count) ? lines[i].text : "";

        static void Put(TMP_Text t, string s, Color c)
        {
            if (t == null || s == null) return;
            t.text = s;
            t.color = c;
        }

        void TogglePlay()
        {
            if (audioSrc == null || audioSrc.clip == null)
            {
                Debug.LogWarning("[ARListen] 음원이 없음 — AudioClip 을 물려주세요");
                return;
            }
            pausedByUser = audioSrc.isPlaying;
            if (audioSrc.isPlaying) audioSrc.Pause(); else audioSrc.Play();
            wasPlaying = audioSrc.isPlaying;
        }

        void Shoot()
        {
            if (capture != null) capture.Capture(e =>
            {
                // 서버 트랙 캡처 (2026-08-25 배선) — 파일 바이트 + 재생 위치
                if (e == null || string.IsNullOrEmpty(e.FilePath)) return;
                try
                {
                    var png = System.IO.File.ReadAllBytes(e.FilePath);
                    ApiMediaSource.UploadTrackCapture(png, audioSrc != null ? audioSrc.time : 0f, e.TakenAt);
                }
                catch (System.Exception ex) { Debug.LogWarning($"[Media] 캡처 파일을 못 읽음: {ex.Message}"); }
            });
            else Debug.LogWarning("[ARListen] CaptureService 가 없음");
        }

        static string Fmt(float sec)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(sec));
            return $"{s / 60:00}:{s % 60:00}";
        }
    }
}
