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

        void OnEnable()
        {
            if (email != null) email.onValueChanged.AddListener(_ => Refresh());
            if (password != null) password.onValueChanged.AddListener(_ => Refresh());
            Refresh();
        }

        void OnDisable()
        {
            if (email != null) email.onValueChanged.RemoveAllListeners();
            if (password != null) password.onValueChanged.RemoveAllListeners();
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
