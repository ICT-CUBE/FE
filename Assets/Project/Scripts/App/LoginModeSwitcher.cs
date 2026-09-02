using System.Collections;
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

        [Header("자동 로그인 (2026-08-30)")]
        // 저장된 액세스 토큰이 있으면 로그인 화면을 건너뛴다 — 앱을 껐다 켜도 로그인 유지.
        [SerializeField] string autoSkipScreen = "Home";   // 자동 로그인도 홈으로 (2026-08-30)

        void OnEnable()
        {
            ApiConfig.LoadSavedToken();
            if (!string.IsNullOrEmpty(ApiConfig.AccessToken) && !string.IsNullOrEmpty(autoSkipScreen))
            {
                // ★ 여기서 바로 Show() 하면 안 된다 — ScreenManager 가 시작 화면을 켜는
                //   순회 '도중'에 이 OnEnable 이 불려서, 옮겨간 화면을 순회의 나머지가
                //   도로 꺼버린다 (2026-08-30 실기: 빌드가 까만 화면에서 안 벗어남).
                //   초기화가 끝난 다음 프레임에 이동한다.
                StartCoroutine(SkipNextFrame());
                return;
            }

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

        IEnumerator SkipNextFrame()
        {
            yield return null;   // ScreenManager 초기 활성화 순회가 끝난 뒤
            Debug.Log("[Auth] 저장된 토큰 있음 — 로그인 화면 건너뜀");
            ScreenManager.Instance?.Show(autoSkipScreen);
        }
    }
}