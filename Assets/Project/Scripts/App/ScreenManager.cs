using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 화면 전환 관리자. 한 씬 안의 여러 화면(패널)을 켜고 끈다.
    // 뒤로가기(스택) 지원. 어디서든 ScreenManager.Instance.Show("Mission") 로 전환.
    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance { get; private set; }

        [SerializeField] List<Screen> screens = new List<Screen>();
        [SerializeField] string startScreen = "Home";

        // 2026-08-30 유저 지시: 뒤로가기는 히스토리 스택이 아니라 화면마다 정해진 '부모 화면'으로
        // 간다. 스택은 탭 이동(홈↔커뮤니티↔마켓…)이 그대로 쌓여 뒤로가 무한히 되감기는 문제가
        // 있었다. 부모가 하나로 고정돼 있으니 루프가 생길 수 없다.
        // 씬의 ScreenButton 이동 그래프를 전수 추출해 만든 맵이다 — 새 화면을 만들면 여기 추가할 것.
        static readonly Dictionary<string, string> BackMap = new Dictionary<string, string>
        {
            // 로그인·가입
            { "EmailLogin", "Login" }, { "EmailSignup", "Login" },
            { "PasswordReset", "EmailLogin" }, { "ResetSent", "EmailLogin" },
            { "Nickname", "EmailSignup" }, { "SignupDone", "Login" },
            { "Permission", "Nickname" }, { "Onboarding", "Login" },
            // 온보딩 큐브 인증
            { "Scan", "Home" }, { "ScanFail", "Scan" },
            { "CodeInput", "Scan" }, { "CodeFail", "CodeInput" }, { "CubeFound", "Scan" },
            // 홈에서 갈라지는 탭·상단
            { "Hub", "Home" }, { "Mission", "Home" }, { "Notification", "Home" },
            { "IntimacyDetail", "Home" },
            { "Community", "Home" }, { "Market", "Home" }, { "My", "Home" },
            // 미션 연출
            { "LevelUp", "Mission" }, { "LevelUpResult", "Mission" },
            { "BadgeReward", "Mission" }, { "BadgeCollection", "My" },
            // 커뮤니티
            { "CommunityPost", "Community" }, { "CommunityWrite", "Community" },
            // 마켓
            { "MarketCubeList", "Market" }, { "MarketCubeDetail", "MarketCubeList" },
            { "MarketOrder", "MarketCubeDetail" }, { "MarketMyTrades", "Market" },
            { "MarketOrderDetail", "MarketMyTrades" }, { "MarketPurchaseDone", "Market" },
            { "MarketLoading", "Market" },
            // 마이
            { "MyPosts", "My" }, { "MyComments", "My" },
            { "MyProfileEdit", "My" }, { "MySettings", "My" },
            // 아바타·대화
            { "Avatar", "Home" }, { "AvatarName", "Avatar" },
            { "TalkList", "Avatar" }, { "TalkDetail", "TalkList" },
            { "TalkArchive", "TalkList" }, { "TalkSearch", "TalkList" },
            { "VoiceHome", "Avatar" }, { "VoiceList", "VoiceHome" },
            { "VoiceTalk", "VoiceHome" }, { "VoiceLoading", "VoiceHome" },
            // 앨범·감상
            { "Album", "Hub" }, { "MusicDetail", "Album" }, { "Lyrics", "MusicDetail" },
            { "ARListen", "MusicDetail" }, { "ListenReport", "MusicDetail" },
            // 무대
            { "StageList", "Hub" }, { "StageDetail", "StageList" },
            { "StagePlayer", "StageDetail" }, { "StageLoading", "StageDetail" },
            { "ARGuide", "StageDetail" }, { "ARScan", "ARGuide" }, { "ARPlace", "ARScan" },
            { "ARStage", "StageDetail" }, { "ARReport", "StageDetail" },
            // AR 소환
            { "ARSummon", "Avatar" }, { "SummonScan", "Avatar" },
            { "SummonPlace", "SummonScan" }, { "SummonReport", "ARSummon" },
        };

        // 진입 경로가 여러 개라 부모를 하나로 못 박는 화면 — 직전 화면을 '한 번만' 기억했다 쓴다.
        // 한 번 쓰고 지우므로 (그리고 위 맵이 비순환이므로) 루프에 빠질 수 없다.
        static readonly HashSet<string> RememberFrom = new HashSet<string>
        { "CaptureDetail", "PhotoDetail", "Gallery", "TodayPhotos" };

        readonly Dictionary<string, string> rememberedFrom = new Dictionary<string, string>();
        string current;

        // 지금 켜져 있는 화면 id
        public string Current => current;
        // 화면이 바뀔 때마다 새 id 를 알려준다. (AR 흐름 제어에 사용)
        public event System.Action<string> ScreenChanged;

        [System.Serializable]
        public class Screen
        {
            public string id;         // "Home", "Mission" ...
            public GameObject root;    // 그 화면의 최상위 오브젝트
        }

        void Awake()
        {
            Instance = this;
            foreach (var s in screens)
                if (s.root != null) s.root.SetActive(s.id == startScreen);
            current = startScreen;
        }

        void Start()
        {
            // Awake 시점엔 구독자가 아직 없으므로 한 프레임 뒤에 최초 상태를 알린다.
            ScreenChanged?.Invoke(current);
        }

        // 새 화면으로 이동
        public void Show(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("[ScreenManager] 빈 화면 id 로 Show 호출됨 — 무시함");
                return;
            }
            if (id == current) return;
            if (RememberFrom.Contains(id) && !string.IsNullOrEmpty(current))
                rememberedFrom[id] = current;
            Activate(id);
        }

        // 뒤로가기 — 화면마다 정해진 부모로 간다 (히스토리 스택 아님)
        public void Back()
        {
            string target;
            if (rememberedFrom.TryGetValue(current, out target))
                rememberedFrom.Remove(current);           // 한 번 쓰고 버린다
            else if (!BackMap.TryGetValue(current, out target))
                target = null;

            if (string.IsNullOrEmpty(target) || target == current)
            {
                // 최상위(Home·Login 등) — 뒤로 갈 곳이 없다. 먹통 방지로 홈만 예외 처리.
                if (current != startScreen && current != "Home" && current != "Login")
                    Activate(startScreen);
                return;
            }
            Activate(target);
        }

        void Activate(string id)
        {
            bool found = false;
            foreach (var s in screens)
            {
                if (s.root == null) continue;
                bool on = s.id == id;
                s.root.SetActive(on);
                if (on) found = true;
            }
            if (!found)
                Debug.LogError($"[ScreenManager] '{id}' 에 해당하는 화면이 없음 — 전부 꺼졌다");

            current = id;
            ScreenChanged?.Invoke(id);
        }
    }
}