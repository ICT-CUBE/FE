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
            }
            if (!string.IsNullOrEmpty(LyricsController.PendingKey))
                lyricsKey = LyricsController.PendingKey;
            // 넘어온 게 없으면 곡 키로 유추 (예전엔 만트라 가사가 고정으로 떴다)
            if (string.IsNullOrEmpty(lyricsKey) && !string.IsNullOrEmpty(clipKey))
                lyricsKey = "Lyrics/" + clipKey;

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

        void OnDisable()
        {
            if (btnPlayPause != null) btnPlayPause.onClick.RemoveListener(TogglePlay);
            if (btnCapture != null) btnCapture.onClick.RemoveListener(Shoot);
            if (btnDownload != null) btnDownload.onClick.RemoveListener(Shoot);
            if (seekBar != null) seekBar.Seeked -= SeekTo;
            if (audioSrc != null)
            {
                MusicDetailController.PendingClipKey = clipKey;
                MusicDetailController.PendingTime = audioSrc.time;
                MusicDetailController.PendingPlaying = audioSrc.isPlaying;
                audioSrc.Pause();
            }
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
            if (capture != null) capture.Capture();
            else Debug.LogWarning("[ARListen] CaptureService 가 없음");
        }

        static string Fmt(float sec)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(sec));
            return $"{s / 60:00}:{s % 60:00}";
        }
    }
}
