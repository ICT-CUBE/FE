using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 비밀번호 입력창의 보기/가리기 토글.
    // InputField 인스턴스의 IconRight(Image + Button)에 붙이고 대상 입력창을 연결한다.
    [RequireComponent(typeof(Button))]
    public class PasswordToggle : MonoBehaviour
    {
        [SerializeField] TMP_InputField input;   // 대상 입력창 (보통 부모)
        [SerializeField] Image icon;             // 비워두면 자기 자신의 Image
        [SerializeField] Sprite eyeOn;           // 보이는 상태 아이콘
        [SerializeField] Sprite eyeOff;          // 가려진 상태 아이콘

        bool visible;

        void Awake()
        {
            if (icon == null) icon = GetComponent<Image>();
            if (input == null) input = GetComponentInParent<TMP_InputField>();
            GetComponent<Button>().onClick.AddListener(Toggle);
            Apply();
        }

        void Toggle()
        {
            visible = !visible;
            Apply();
        }

        void Apply()
        {
            if (input == null) return;

            input.contentType = visible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;

            // contentType 만 바꾸면 화면 글자가 갱신되지 않는다. 반드시 호출해야 함.
            input.ForceLabelUpdate();

            if (icon != null) icon.sprite = visible ? eyeOn : eyeOff;
        }
    }
}