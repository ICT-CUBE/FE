using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 회원가입 화면의 입력 검증을 UI에 연결한다.
    // 규칙은 Validators 에 있고, 여기서는 표시만 담당한다.
    public class SignupFormBinder : MonoBehaviour
    {
        [Header("입력")]
        [SerializeField] TMP_InputField email;
        [SerializeField] TMP_InputField password;
        [SerializeField] TMP_InputField passwordConfirm;
        [SerializeField] Toggle agree;

        [Header("표시")]
        [SerializeField] TMP_Text errorEmail;
        [SerializeField] Image emailStatusIcon;
        [SerializeField] Sprite checkOff;
        [SerializeField] Sprite checkOn;

        [Header("다음 버튼")]
        [SerializeField] Button next;
        [SerializeField] Image nextBackground;

        // 실제 가입 POST 는 닉네임 화면(NicknameFormBinder)에서 나간다 —
        // 서버 요청 바디에 nickname·profileCharacterCode 가 필수라 여기선 보낼 수 없다.
        // 닉네임 화면이 읽어 가도록 입력값을 잠깐 들고 있는다.
        public static string PendingEmail = "";
        public static string PendingPassword = "";

        void OnEnable()
        {
            if (email != null) email.onValueChanged.AddListener(_ => Refresh());
            if (password != null) password.onValueChanged.AddListener(_ => Refresh());
            if (passwordConfirm != null) passwordConfirm.onValueChanged.AddListener(_ => Refresh());
            if (agree != null) agree.onValueChanged.AddListener(_ => Refresh());
            if (next != null) next.onClick.AddListener(Stash);
            Refresh();
        }

        void OnDisable()
        {
            if (email != null) email.onValueChanged.RemoveAllListeners();
            if (password != null) password.onValueChanged.RemoveAllListeners();
            if (passwordConfirm != null) passwordConfirm.onValueChanged.RemoveAllListeners();
            if (agree != null) agree.onValueChanged.RemoveAllListeners();
            if (next != null) next.onClick.RemoveListener(Stash);
        }

        void Stash()
        {
            PendingEmail = email != null ? email.text : "";
            PendingPassword = password != null ? password.text : "";
        }

        void Refresh()
        {
            var e  = Validators.Email(email != null ? email.text : "");
            var p  = Validators.Password(password != null ? password.text : "");
            var pc = Validators.PasswordConfirm(
                        password != null ? password.text : "",
                        passwordConfirm != null ? passwordConfirm.text : "");

            // 이메일 에러 문구 — 아직 안 친 상태(Empty)면 숨긴다
            if (errorEmail != null)
            {
                errorEmail.gameObject.SetActive(e.ShowError);
                if (e.ShowError) errorEmail.text = e.Message;
            }

            // 이메일 우측 체크 아이콘
            if (emailStatusIcon != null)
            {
                var s = e.IsValid ? checkOn : checkOff;
                if (s != null) emailStatusIcon.sprite = s;
            }

            bool ok = e.IsValid && p.IsValid && pc.IsValid && (agree == null || agree.isOn);

            if (next != null) next.interactable = ok;
            if (nextBackground != null)
                nextBackground.color = ok ? DesignTokens.Action.Primary : DesignTokens.Bg.LowSurface;
        }
    }
}