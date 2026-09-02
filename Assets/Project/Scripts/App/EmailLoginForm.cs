using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 이메일 로그인 폼. 형식이 맞으면 에러를 숨기고 체크 아이콘을 켠다.
    // 이 화면엔 검증이 아예 없어서 "형식이 아닙니다" 가 계속 떠 있었다.
    public class EmailLoginForm : MonoBehaviour
    {
        [SerializeField] TMP_InputField email;
        [SerializeField] TMP_InputField password;
        [SerializeField] TMP_Text errEmail;        // "올바른 이메일 형식이 아닙니다."
        [SerializeField] Image emailCheck;         // 이메일 칸 우측 체크
        [SerializeField] Button loginButton;
        // 2026-08-30 서버 auth 배포 — 로그인 성공 시에만 넘어간다.
        // (씬의 BtnLogin ScreenButton 은 꺼뒀다 — 켜져 있으면 실패해도 무조건 이동한다)
        [SerializeField] string nextScreen = "Home";   // 로그인하면 홈으로 (2026-08-30 유저 지시)

        bool busy;   // 응답 전 재클릭 방지

        void OnEnable()
        {
            if (email != null) email.onValueChanged.AddListener(_ => Refresh());
            if (password != null) password.onValueChanged.AddListener(_ => Refresh());
            if (loginButton != null) loginButton.onClick.AddListener(OnLogin);
            Refresh();
        }

        void OnDisable()
        {
            if (email != null) email.onValueChanged.RemoveAllListeners();
            if (password != null) password.onValueChanged.RemoveAllListeners();
            if (loginButton != null) loginButton.onClick.RemoveListener(OnLogin);
        }

        void OnLogin()
        {
            if (busy) return;
            busy = true;
            ApiAuth.Login(email != null ? email.text : "", password != null ? password.text : "",
                _ =>
                {
                    busy = false;
                    LoginModeSwitcher.HasSignedUp = true;   // 로그인 성공 = 가입 이력 확정
                    if (ScreenManager.Instance != null && !string.IsNullOrEmpty(nextScreen))
                        ScreenManager.Instance.Show(nextScreen);
                },
                err => { busy = false; Toast.Show(err); });
        }

        void Refresh()
        {
            string e = email != null ? email.text : "";
            string p = password != null ? password.text : "";

            var r = Validators.Email(e);
            bool empty = string.IsNullOrEmpty(e);
            bool ok = r.IsValid;

            // 비어 있을 땐 에러도 체크도 보이지 않는다
            if (errEmail != null)
            {
                errEmail.gameObject.SetActive(r.ShowError);
                if (!string.IsNullOrEmpty(r.Message)) errEmail.text = r.Message;
            }
            if (emailCheck != null) emailCheck.gameObject.SetActive(ok);

            if (loginButton != null)
                loginButton.interactable = ok && !string.IsNullOrEmpty(p);
        }
    }
}
