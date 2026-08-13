using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 음악 상세(일반 감상). 재생·진행바·세그먼트 토글·다음 곡·가사 보기.
    // 음원이 아직 없어도 동작하도록, 클립이 없으면 길이 0 으로 두고 조작만 살려둔다.
    public class MusicDetailController : MonoBehaviour
    {
        [System.Serializable]
        public class Track
        {
            public string title = "Mantra";
            public string artist = "Jennie";
            public AudioClip clip;
            public string clipKey;               // Resources/Audio/<clipKey>.mp3

            AudioClip cached; bool tried;
            public AudioClip Clip
            {
                get
                {
                    if (clip != null) return clip;
                    if (!tried) { tried = true; if (!string.IsNullOrEmpty(clipKey)) cached = Resources.Load<AudioClip>("Audio/" + clipKey); }
                    return cached;
                }
            }
            // 비워두면 clipKey 로 유추한다 (Resources/Lyrics/<clipKey>.txt).
            // 예전엔 기본값이 mantra 라 어떤 곡을 골라도 만트라 가사가 떴다.
            public string lyricsKey = "";
            public string LyricsKey =>
                !string.IsNullOrEmpty(lyricsKey) ? lyricsKey
                : (!string.IsNullOrEmpty(clipKey) ? "Lyrics/" + clipKey : "");
        }

        [Header("세그먼트 (일반 / AR 감상)")]
        [SerializeField] RectTransform segPill;
        [SerializeField] TMP_Text labelNormal;
        [SerializeField] TMP_Text labelAR;
        [SerializeField] float pillXNormal = 82f;     // 시안 실측
        [SerializeField] float pillXAR = 204f;        // 배경 (78,56) 257 안쪽 여백 4
        [SerializeField] string arScreen = "ARListen";

        [Header("곡 정보")]
        [SerializeField] TMP_Text trackTitle;
        [SerializeField] TMP_Text trackArtist;
        [SerializeField] TMP_Text labelNextTitle;

        [Header("진행바 (시안: 트랙 20~392, 손잡이 12)")]
        [SerializeField] RectTransform progressFill;
        [SerializeField] RectTransform progressKnob;
        [SerializeField] float trackLeft = 20f;
        [SerializeField] float trackWidth = 372f;
        [SerializeField] TMP_Text timeNow;
        [SerializeField] TMP_Text timeTotal;
        [SerializeField] SeekBar seekBar;     // ProgressTrack 에 붙인다 — 눌러/끌어서 위치 이동

        [Header("버튼")]
        [SerializeField] Button btnPlayPause;
        [SerializeField] Button btnPrev;
        [SerializeField] Button btnNext;
        [SerializeField] Button btnLyrics;
        [SerializeField] Button btnNextCard;
        [SerializeField] Button btnSegNormal;
        [SerializeField] Button btnSegAR;
        [SerializeField] Image iconPlayPause;
        [SerializeField] Sprite icoPlay;
        [SerializeField] Sprite icoPause;

        [Header("데이터 (임시 — 음원 들어오면 clip 만 채우면 된다)")]
        [SerializeField] List<Track> tracks = new List<Track>();
        [SerializeField] int index;

        // 앨범에서 고른 트랙 번호
        public static int PendingIndex = -1;
        public static string PendingTitle, PendingArtist;
        // 앨범이 목록 전체를 넘긴다 (다음 곡 표시·이전/다음 이동에 필요)
        public static List<Track> PendingTracks;
        // 일반 감상 ↔ AR 감상 이어듣기 — 화면을 떠나는 쪽이 채우고 도착하는 쪽이 비운다
        public static string PendingClipKey;
        public static float PendingTime = -1f;
        public static bool PendingPlaying;

        AudioSource audioSrc;
        bool wasPlaying;        // 곡이 끝나 멈춘 것인지 판별용
        bool pausedByUser;      // 사용자가 일시정지한 것이면 다음 곡으로 넘기지 않는다
        bool autoPlayNext;      // 자동 넘김이면 Bind() 뒤에 바로 재생

        void Awake()
        {
            audioSrc = GetComponent<AudioSource>();
            if (audioSrc == null) audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
        }

        void OnEnable()
        {
            Hook(btnPlayPause, TogglePlay);
            Hook(btnPrev, Prev);
            Hook(btnNext, Next);
            Hook(btnNextCard, Next);
            Hook(btnLyrics, OpenLyrics);
            Hook(btnSegNormal, () => SetSegment(false));
            Hook(btnSegAR, () => SetSegment(true));
            if (seekBar != null) seekBar.Seeked += SeekTo;
            SetSegment(false);
            // 앨범에서 고른 곡. 상세의 트랙 목록이 비어 있어도 제목/아티스트는 그대로 받는다.
            if (PendingTracks != null && PendingTracks.Count > 0)
            {
                tracks = PendingTracks;
                PendingTracks = null;
            }
            if (PendingIndex >= 0)
            {
                index = PendingIndex;
                PendingIndex = -1;
                if (!string.IsNullOrEmpty(PendingTitle))
                {
                    while (tracks.Count <= index) tracks.Add(new Track());
                    tracks[index].title = PendingTitle;
                    tracks[index].artist = PendingArtist;
                    PendingTitle = PendingArtist = null;
                }
            }
            Bind();
        }

        void OnDisable()
        {
            Unhook(btnPlayPause); Unhook(btnPrev); Unhook(btnNext);
            Unhook(btnNextCard); Unhook(btnLyrics); Unhook(btnSegNormal); Unhook(btnSegAR);
            if (seekBar != null) seekBar.Seeked -= SeekTo;
            if (audioSrc != null) audioSrc.Pause();
        }

        void Bind()
        {
            if (tracks.Count == 0) return;
            index = Mathf.Clamp(index, 0, tracks.Count - 1);
            var t = tracks[index];

            if (trackTitle != null) trackTitle.text = t.title;
            if (trackArtist != null) trackArtist.text = t.artist;
            if (labelNextTitle != null)
                labelNextTitle.text = tracks[(index + 1) % tracks.Count].title;

            if (audioSrc != null)
            {
                audioSrc.clip = t.Clip;
                // AR 감상에서 돌아온 경우 그 위치에서 이어 재생한다
                if (PendingTime >= 0f && audioSrc.clip != null)
                {
                    audioSrc.time = Mathf.Clamp(PendingTime, 0f, audioSrc.clip.length - 0.05f);
                    if (PendingPlaying) audioSrc.Play();
                }
                else audioSrc.time = 0f;
                PendingTime = -1f; PendingPlaying = false;

                // 자동으로 넘어온 곡은 이어서 재생한다
                if (autoPlayNext && audioSrc.clip != null) audioSrc.Play();
            }
            autoPlayNext = false;
            pausedByUser = false;
            wasPlaying = audioSrc != null && audioSrc.isPlaying;
            Apply(0f);
        }

        void Update()
        {
            float len = (audioSrc != null && audioSrc.clip != null) ? audioSrc.clip.length : 0f;
            float now = (audioSrc != null) ? audioSrc.time : 0f;
            Apply(len > 0.01f ? now / len : 0f);

            // 곡 종료 감지 — 재생 중이었는데 사용자가 멈추지도 않았는데 멈췄다면 끝난 것
            bool nowPlaying = audioSrc != null && audioSrc.isPlaying;
            if (wasPlaying && !nowPlaying && !pausedByUser && audioSrc != null && audioSrc.clip != null)
            {
                autoPlayNext = true;
                Next();
                return;
            }
            wasPlaying = nowPlaying;

            if (iconPlayPause != null)
            {
                bool playing = audioSrc != null && audioSrc.isPlaying;
                var want = playing ? icoPause : icoPlay;
                if (want != null && iconPlayPause.sprite != want) iconPlayPause.sprite = want;
            }
            if (timeNow != null) timeNow.text = Fmt(now);
            if (timeTotal != null) timeTotal.text = Fmt(len);
        }

        void Apply(float p)
        {
            p = Mathf.Clamp01(p);
            if (progressFill != null)
            {
                var s = progressFill.sizeDelta;
                progressFill.sizeDelta = new Vector2(trackWidth * p, s.y);
            }
            if (progressKnob != null)
            {
                var a = progressKnob.anchoredPosition;
                progressKnob.anchoredPosition = new Vector2(trackLeft + trackWidth * p - 6f, a.y);
            }
        }

        // 진행바를 눌러/끌어서 재생 위치 이동
        void SeekTo(float p)
        {
            if (audioSrc == null || audioSrc.clip == null) return;
            audioSrc.time = Mathf.Clamp(p * audioSrc.clip.length, 0f, audioSrc.clip.length - 0.05f);
            Apply(p);
            wasPlaying = audioSrc.isPlaying;      // 곡 종료 판정이 오작동하지 않게
        }

        void TogglePlay()
        {
            if (audioSrc == null) return;
            if (audioSrc.clip == null)
            {
                Debug.LogWarning("[MusicDetail] 음원이 없음 — Track.clip 에 AudioClip 을 넣어주세요");
                return;
            }
            pausedByUser = audioSrc.isPlaying;      // 지금 멈추는 것이면 '사용자가 멈춤'
            if (audioSrc.isPlaying) audioSrc.Pause(); else audioSrc.Play();
            wasPlaying = audioSrc.isPlaying;
        }

        void Prev()
        {
            if (tracks.Count == 0) { Debug.LogWarning("[MusicDetail] 트랙 목록이 비어 이전 곡으로 못 감 — 앨범에서 진입해야 목록이 넘어온다"); return; }
            index = (index - 1 + tracks.Count) % tracks.Count;
            Bind();
        }

        void Next()
        {
            if (tracks.Count == 0) { Debug.LogWarning("[MusicDetail] 트랙 목록이 비어 다음 곡으로 못 감 — 앨범에서 진입해야 목록이 넘어온다"); return; }
            index = (index + 1) % tracks.Count;
            Bind();
        }

        void OpenLyrics()
        {
            if (tracks.Count > 0) LyricsController.PendingKey = tracks[index].LyricsKey;
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show("Lyrics");
        }

        // 시안: 선택된 쪽만 알약 배경 + 흰 글씨
        void SetSegment(bool ar)
        {
            if (segPill != null)
            {
                var a = segPill.anchoredPosition;
                segPill.anchoredPosition = new Vector2(ar ? pillXAR : pillXNormal, a.y);
            }
            if (labelNormal != null) labelNormal.color = ar ? DesignTokens.Text.Tertiary : DesignTokens.Text.Primary;
            if (labelAR != null) labelAR.color = ar ? DesignTokens.Text.Primary : DesignTokens.Text.Tertiary;

            if (ar && ScreenManager.Instance != null && !string.IsNullOrEmpty(arScreen))
            {
                if (tracks.Count > 0)
                {
                    var cur = tracks[Mathf.Clamp(index, 0, tracks.Count - 1)];
                    PendingTitle = cur.title;
                    PendingArtist = cur.artist;
                    PendingClipKey = cur.clipKey;
                    LyricsController.PendingKey = cur.LyricsKey;
                }
                PendingTime = audioSrc != null ? audioSrc.time : 0f;
                PendingPlaying = audioSrc != null && audioSrc.isPlaying;
                ScreenManager.Instance.Show(arScreen);
            }
        }

        static string Fmt(float sec)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(sec));
            return $"{s / 60:00}:{s % 60:00}";
        }

        void Hook(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null) b.onClick.AddListener(a);
        }

        void Unhook(Button b)
        {
            if (b != null) b.onClick.RemoveAllListeners();
        }
    }
}
