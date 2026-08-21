using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 마이페이지 (시안 '마이.svg'). 헤더 + 프로필 줄 + 카드(6행).
    //
    // '내가 쓴 글' / '댓글 단 글' 숫자는 시안의 4·2(목업값)를 하드코딩하지 않고 CommunityStore 에서
    // 직접 센다 — 내가 쓴 글 = post.mine, 댓글 단 글 = post.replies 중 author=="나" 가 하나라도 있는 글 수
    // (MockCommunitySource.AddComment 가 내 댓글에 author="나" 만 채우는 관례, Task2).
    //
    // 뒤로가기(BtnBack)는 ScreenManager 가 이미 있어 실제로 배선했다(씬에서 ScreenButton back=true
    // 로 직접 처리 — 이 컴포넌트가 관여하지 않는다).
    // 편집→MyProfileEdit(Task2) · 톱니→MySettings(Task3) · 내가 쓴 글→MyPosts · 댓글 단 글→
    // MyComments(Task4) 는 실제로 배선했다(Awake, 스텁 아님).
    //
    // 탈퇴하기→Modal_Withdraw · 로그아웃→Modal_Logout(Task5): 두 모달 다 Modal_ArchiveDelete 복제
    // (tools/p_my_modals.py). 모달 자신에게 싱글턴 MonoBehaviour 를 붙이지 않는다 — 모달은 Canvas
    // 직계로 계속 꺼진 채 있다가(Task10 Popup_PostMenu 와 같은 패턴) MyController(=Screen_My, 반드시
    // Awake 됨)가 SetActive 만 토글하고 버튼 리스너도 여기서 전부 건다(브리프가 못박은 "꺼진 오브젝트에
    // MonoBehaviour 붙이지 말 것" Critical 회피 — 더 단순한 쪽을 택하라는 지시 그대로). 취소/Dim 탭은
    // 모달을 닫기만 하고, 확정은 닫은 뒤 ScreenManager.Show 로 이어진다.
    //
    // '내 큐브 보관함 / 인벤토리' · '거래 결제 및 정산 내역' 두 행은 유저 지시로 배선하지 않는다(갈 화면 없음).
    //
    // 탈퇴 확정 토스트("탈퇴 처리 되었습니다.")는 Task6 에서 연결했다 — '마이 토스트 모음.svg' 에 이
    // 문구와 프로필 저장 토스트 2개만 있다(로그아웃 토스트는 시안에 없어 넣지 않았다).
    public class MyController : MonoBehaviour
    {
        [Header("숫자 (CommunityStore 에서 센다 — 시안 4·2 는 목업값)")]
        [SerializeField] TMP_Text labelMyPostsCount;
        [SerializeField] TMP_Text labelMyCommentsCount;

        [Header("프로필 (ProfileStore=PlayerPrefs 에서 읽는다 — Task2 MyProfileEditController 가 저장)")]
        [SerializeField] Image avatarImage;
        [SerializeField] TMP_Text labelNickname;
        [SerializeField] Sprite[] avatarSprites;   // 0 남성형 · 1 여성형 · 2 동물형 (AvatarNameBinder.profiles 순서)

        [Header("배선 (전부 실배선)")]
        [SerializeField] Button btnSettings;      // 톱니 → MySettings
        [SerializeField] Button btnEditProfile;   // 편집 → MyProfileEdit
        [SerializeField] Button btnRowMyPosts;    // 내가 쓴 글 → MyPosts
        [SerializeField] Button btnRowMyComments; // 댓글 단 글 → MyComments
        [SerializeField] Button btnWithdraw;      // 탈퇴하기 → Modal_Withdraw 열기
        [SerializeField] Button btnLogout;        // 로그아웃 → Modal_Logout 열기

        [Header("로그아웃 확인 모달 (Modal_Logout, Task5)")]
        [SerializeField] GameObject modalLogout;
        [SerializeField] Button btnLogoutDim;      // Dim 바깥 탭 → 닫기
        [SerializeField] Button btnLogoutCancel;   // 취소 → 닫기
        [SerializeField] Button btnLogoutConfirm;  // 로그아웃 하기 → 닫고 ScreenManager.Show("Login")

        [Header("탈퇴 확인 모달 (Modal_Withdraw, Task5)")]
        [SerializeField] GameObject modalWithdraw;
        [SerializeField] Button btnWithdrawDim;     // Dim 바깥 탭 → 닫기
        [SerializeField] Button btnWithdrawCancel;  // 취소 → 닫기
        [SerializeField] Button btnWithdrawConfirm; // 탈퇴하기 → 닫고 ScreenManager.Show("Login")

        const string MyCommentAuthor = "나";

        void Awake()
        {
            if (btnEditProfile != null)
                btnEditProfile.onClick.AddListener(() => ScreenManager.Instance?.Show("MyProfileEdit"));
            if (btnSettings != null)
                btnSettings.onClick.AddListener(() => ScreenManager.Instance?.Show("MySettings"));
            if (btnRowMyPosts != null)
                btnRowMyPosts.onClick.AddListener(() => ScreenManager.Instance?.Show("MyPosts"));
            if (btnRowMyComments != null)
                btnRowMyComments.onClick.AddListener(() => ScreenManager.Instance?.Show("MyComments"));

            if (btnLogout != null) btnLogout.onClick.AddListener(() => OpenModal(modalLogout));
            if (btnLogoutDim != null) btnLogoutDim.onClick.AddListener(() => CloseModal(modalLogout));
            if (btnLogoutCancel != null) btnLogoutCancel.onClick.AddListener(() => CloseModal(modalLogout));
            if (btnLogoutConfirm != null) btnLogoutConfirm.onClick.AddListener(ConfirmLogout);

            if (btnWithdraw != null) btnWithdraw.onClick.AddListener(() => OpenModal(modalWithdraw));
            if (btnWithdrawDim != null) btnWithdrawDim.onClick.AddListener(() => CloseModal(modalWithdraw));
            if (btnWithdrawCancel != null) btnWithdrawCancel.onClick.AddListener(() => CloseModal(modalWithdraw));
            if (btnWithdrawConfirm != null) btnWithdrawConfirm.onClick.AddListener(ConfirmWithdraw);
        }

        // TalkArchiveController.AskDelete/PostMenuPopup.Open 과 같은 관례: SetAsLastSibling 으로
        // 다른 오버레이보다 위에 그려지게 한다.
        static void OpenModal(GameObject modal)
        {
            if (modal == null) return;
            modal.SetActive(true);
            modal.transform.SetAsLastSibling();
        }

        static void CloseModal(GameObject modal)
        {
            if (modal != null) modal.SetActive(false);
        }

        void ConfirmLogout()
        {
            CloseModal(modalLogout);
            ScreenManager.Instance?.Show("Login");
        }

        void ConfirmWithdraw()
        {
            CloseModal(modalWithdraw);
            // 탈퇴하면 계정이 없어지므로 가입 이력도 지운다 — 그래야 Login 화면의 이메일 버튼이
            // LoginModeSwitcher 에 의해 '이메일로 회원가입'(→EmailSignup)으로 되돌아간다.
            LoginModeSwitcher.HasSignedUp = false;
            ScreenManager.Instance?.Show("Login");

            // 시안 '마이 토스트 모음.svg' 문구 그대로. bottom=188 은 그 시안에서 실측한 값
            // (pill y=659~705, 캔버스 893 기준 893-705=188). Toast 는 ScreenManager 오브젝트에 붙어
            // 화면 전환과 무관하게 계속 떠 있으므로(Task6) Login 으로 넘어간 뒤에도 정상 표시된다.
            Toast.Show("탈퇴 처리 되었습니다.", 188f);
        }

        void OnEnable()
        {
            var store = CommunityStore.Instance;
            if (store != null)
            {
                store.OnChanged += RefreshCounts;
                RefreshCounts();
            }
            else
            {
                // 유니티는 오브젝트 간 Awake 순서를 보장하지 않는다 — CommunityStore 는 보통 Canvas 아래
                // 항상 켜져 있는 오브젝트에 있어 이 화면이 열리기 전에 이미 Awake 가 끝나 있지만, 혹시
                // 순서가 꼬이면 조용히 넘어가지 않고 에러로 남긴다(CommunityController 와 같은 관례).
                Debug.LogError("[My] OnEnable 시점에 CommunityStore.Instance 가 없다 — 숫자를 못 셈");
            }

            RefreshProfile();
        }

        // MyProfileEditController.Save() 가 ProfileStore(PlayerPrefs) 에 남긴 값을 반영한다.
        // 이 화면이 다시 열릴 때마다(뒤로가기 포함) OnEnable 이 불리므로 매번 다시 읽는다.
        void RefreshProfile()
        {
            int idx = ProfileStore.AvatarIndex;
            if (avatarImage != null && avatarSprites != null && idx >= 0 && idx < avatarSprites.Length
                && avatarSprites[idx] != null)
                avatarImage.sprite = avatarSprites[idx];

            if (labelNickname != null) labelNickname.text = ProfileStore.Nickname;
        }

        void OnDisable()
        {
            if (CommunityStore.Instance != null) CommunityStore.Instance.OnChanged -= RefreshCounts;
        }

        void RefreshCounts()
        {
            var store = CommunityStore.Instance;
            if (store == null) return;

            // store.Posts 는 커뮤니티 화면의 칩·검색으로 걸러진 캐시다 — 마이 화면은 전량을 봐야
            // 하므로 LoadAll 로 따로 받는다(C1, 최종 리뷰).
            store.LoadAll(posts =>
            {
                int written = 0, commented = 0;
                if (posts != null)
                {
                    for (int i = 0; i < posts.Count; i++)
                    {
                        var p = posts[i];
                        if (p.mine) written++;
                        if (HasMyComment(p)) commented++;
                    }
                }
                if (labelMyPostsCount != null) labelMyPostsCount.text = written.ToString();
                if (labelMyCommentsCount != null) labelMyCommentsCount.text = commented.ToString();
            });
        }

        static bool HasMyComment(CommunityPost p)
        {
            if (p.replies == null) return false;
            for (int i = 0; i < p.replies.Count; i++)
                if (p.replies[i].author == MyCommentAuthor) return true;
            return false;
        }

    }
}
