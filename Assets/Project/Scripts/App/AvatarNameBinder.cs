using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 큐브 인식 완료 화면(Screen_AvatarName).
    // 닉네임 글자수 카운터와, 앞 화면(닉네임 설정)에서 고른 프로필을 반영한다.
    public class AvatarNameBinder : MonoBehaviour
    {
        [SerializeField] TMP_InputField nickname;
        [SerializeField] TMP_Text counterText;    // "0 / 10"
        [SerializeField] TMP_Text errText;        // 한글/영문/숫자 안내 (빨강)
        [SerializeField] Image avatarImage;       // 원형 마스크 안의 사진
        [SerializeField] Sprite[] profiles;       // 남성형 / 여성형 / 동물형 순서

        // 닉네임 설정 화면에서 고른 값을 여기에 담아 두고 이 화면에서 읽는다
        public static int SelectedProfile = -1;

        void OnEnable()
        {
            if (nickname != null) nickname.onValueChanged.AddListener(_ => Refresh());
            ApplyProfile();
            Refresh();
        }

        void OnDisable()
        {
            if (nickname != null) nickname.onValueChanged.RemoveAllListeners();
        }

        void ApplyProfile()
        {
            if (avatarImage == null || profiles == null) return;
            if (SelectedProfile < 0 || SelectedProfile >= profiles.Length) return;
            if (profiles[SelectedProfile] == null) return;
            avatarImage.sprite = profiles[SelectedProfile];
        }

        void Refresh()
        {
            string text = nickname != null ? nickname.text : "";

            if (counterText != null)
                counterText.text = $"{text.Length} / {Validators.NicknameMaxLength}";

            // 시안대로 안내 문구는 항상 빨강이다
            if (errText != null) errText.color = DesignTokens.Primitive.Status.Red;
        }
    }
}
