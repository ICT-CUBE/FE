using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 입력 필드를 누르면 테두리와 커서를 보라색으로 바꾼다.
    // 시안: 포커스 #A855F7 / 평소 #242831 (InputField.prefab 의 FieldBorder)
    // InputField 프리팹 루트에 붙이면 자식에서 알아서 찾는다.
    [RequireComponent(typeof(TMP_InputField))]
    public class InputFocusHighlight : MonoBehaviour
    {
        [SerializeField] Image border;          // 비우면 FieldBorder 를 자동으로 찾는다
        [SerializeField] Color focused = new Color(0.658824f, 0.333333f, 0.968627f, 1f); // #A855F7
        [SerializeField] Color normal = new Color(0.141176f, 0.156863f, 0.192157f, 1f);  // #242831

        TMP_InputField field;

        void Awake()
        {
            field = GetComponent<TMP_InputField>();
            if (border == null)
            {
                var t = transform.Find("FieldBorder");
                if (t != null) border = t.GetComponent<Image>();
            }
        }

        void OnEnable()
        {
            field.onSelect.AddListener(OnSelect);
            field.onDeselect.AddListener(OnDeselect);
            Apply(false);
        }

        void OnDisable()
        {
            field.onSelect.RemoveListener(OnSelect);
            field.onDeselect.RemoveListener(OnDeselect);
        }

        void OnSelect(string _) => Apply(true);
        void OnDeselect(string _) => Apply(false);

        void Apply(bool on)
        {
            if (border != null) border.color = on ? focused : normal;

            // 커서(캐럿)도 같은 색으로. TMP 는 캐럿 색을 필드에서 직접 잡는다.
            field.caretColor = focused;
            field.customCaretColor = true;
        }
    }
}
