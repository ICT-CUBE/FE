using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 가입 이력에 따라 로그인 화면 이메일 버튼의 문구와 이동 대상을 바꾼다.
    // ※ 이 버튼에는 ScreenButton 을 붙이지 말 것. 클릭 처리를 여기서 한다.
    public class LoginModeSwitcher : MonoBehaviour
    {
        const string Key = "arzone.signed_up";

        [SerializeField] Button emailButton;
        [SerializeField] TMP_Text emailButtonLabel;

        [Header("가입 이력 없을 때")]
        [SerializeField] string beforeText = "이메일로 회원가입";
        [SerializeField] string beforeScreen = "EmailSignup";

        [Header("가입 이력 있을 때")]
        [SerializeField] string afterText = "이메일로 로그인";
        [SerializeField] string afterScreen = "EmailLogin";

        public static bool HasSignedUp
        {
            get => PlayerPrefs.GetInt(Key, 0) == 1;
            set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        void OnEnable()
        {
            bool signed = HasSignedUp;
            string target = signed ? afterScreen : beforeScreen;

            if (emailButtonLabel != null)
                emailButtonLabel.text = signed ? afterText : beforeText;

            if (emailButton != null)
            {
                // 화면이 다시 켜질 때마다 중복 등록되지 않게 먼저 비운다
                emailButton.onClick.RemoveAllListeners();
                emailButton.onClick.AddListener(() => ScreenManager.Instance?.Show(target));
            }
        }
    }
}