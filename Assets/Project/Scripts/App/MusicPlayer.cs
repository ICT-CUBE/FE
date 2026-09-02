using UnityEngine;

namespace ARZone
{
    // 음악 재생을 '화면 밖'에서 소유한다.
    //
    // ★ 왜 필요한가 (2026-08-24 버그):
    //   AudioSource 가 Screen_MusicDetail 에 붙어 있었다. ScreenManager 는 화면을
    //   SetActive(false) 로 끄는데, **유니티는 GameObject 가 비활성화되면 그 AudioSource
    //   재생을 멈춘다.** 그래서 '가사 보기' 로 넘어가는 순간 노래가 끊겼고,
    //   가사 화면에서는 오디오에 손댈 방법이 아예 없었다.
    //   OnDisable 의 Pause() 를 지워도 안 고쳐진다 — 소유자를 옮겨야 한다.
    //
    // 씬에 미리 넣어둘 필요 없다. 처음 쓸 때 스스로 만들어지고 DontDestroyOnLoad 로 살아남는다.
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }

        AudioSource src;

        // 가사 화면이 제목을 보여주려면 지금 무슨 곡인지 알아야 한다.
        // 재생을 시작하는 쪽(MusicDetailController)이 채운다.
        public string TrackTitle { get; private set; } = "";
        public string TrackArtist { get; private set; } = "";

        public AudioSource Source => src;
        public AudioClip Clip => src != null ? src.clip : null;
        public bool IsPlaying => src != null && src.isPlaying;
        public float Length => (src != null && src.clip != null) ? src.clip.length : 0f;

        public float Time
        {
            get => src != null ? src.time : 0f;
            set
            {
                if (src == null || src.clip == null) return;
                src.time = Mathf.Clamp(value, 0f, src.clip.length - 0.05f);
            }
        }

        // 아직 없으면 만들어서 돌려준다. 호출부가 씬 배선을 신경 쓰지 않아도 되게.
        public static MusicPlayer EnsureInstance()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("[MusicPlayer]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<MusicPlayer>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            src = GetComponent<AudioSource>();
            if (src == null) src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
        }

        public void SetTrack(string title, string artist)
        {
            TrackTitle = title ?? "";
            TrackArtist = artist ?? "";
        }

        // 같은 클립이면 재생 위치를 건드리지 않는다 —
        // 가사 화면을 들렀다 오는 것만으로 곡이 처음으로 돌아가면 안 된다.
        public void SetClip(AudioClip clip)
        {
            if (src == null || src.clip == clip) return;
            src.clip = clip;
            src.time = 0f;
        }

        public void Play()   { if (src != null && src.clip != null) src.Play(); }
        public void Pause()  { if (src != null) src.Pause(); }
        public void Stop()   { if (src != null) src.Stop(); }
        public void Toggle() { if (IsPlaying) Pause(); else Play(); }
    }
}
