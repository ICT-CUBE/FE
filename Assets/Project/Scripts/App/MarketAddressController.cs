using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 배송지 정보 변경 시트 (시안 '정보 변경 바텀시트').
    // 시안엔 값이 '그려져' 있을 뿐이라 씬에도 TMP 라벨만 있었다 → 실제로 칠 수 있게 만든다.
    // TMP_InputField 는 직렬화 필드가 많아 씬에 손으로 만들면 빠뜨리기 쉬워서 코드로 붙인다
    // (대화 입력바 TalkDetailController.EnsureInput 과 같은 방식).
    //
    // 씬에 있던 값 라벨은 placeholder 로 재활용한다. 시안 색이 그대로 근거다:
    //   이름        #727886(TextTertiary) → 빈 칸 + 안내문
    //   연락처·주소 흰색                  → 실제 값 (라벨에 적힌 문자열을 초기값으로 쓴다)
    // 입력 글자는 시안대로 흰색이라 placeholder 의 회색과 자연스럽게 갈린다.
    public class MarketAddressController : MonoBehaviour
    {
        [Header("필드 배경 / 값 라벨(=placeholder)")]
        [SerializeField] RectTransform boxName;
        [SerializeField] TMP_Text hintName;
        [SerializeField] RectTransform boxPhone;
        [SerializeField] TMP_Text hintPhone;
        [SerializeField] RectTransform boxAddr;
        [SerializeField] TMP_Text hintAddr;

        [Header("그 외")]
        [SerializeField] Button btnClearName;   // 이름 칸 오른쪽 X
        [SerializeField] TMP_Text counter;      // 주소 칸 우하단 (시안 '56 / 200')
        [SerializeField] Button btnCancel;
        [SerializeField] Button btnNext;
        [SerializeField] GameObject sheet;      // 닫을 대상 (비면 이 오브젝트)

        [Header("주문 확인 화면에 반영")]
        [SerializeField] TMP_Text orderName;
        [SerializeField] TMP_Text orderPhone;
        [SerializeField] TMP_Text orderAddr;

        [Header("글자 수 제한")]
        [SerializeField] int nameLimit = 30;
        [SerializeField] int phoneLimit = 20;
        [SerializeField] int addrLimit = 200;

        TMP_InputField inName, inPhone, inAddr;
        string savedName = "", savedPhone = "", savedAddr = "";
        bool seeded;

        void OnEnable()
        {
            inName = EnsureInput(boxName, hintName, nameLimit, TouchScreenKeyboardType.Default);
            inPhone = EnsureInput(boxPhone, hintPhone, phoneLimit, TouchScreenKeyboardType.PhonePad);
            inAddr = EnsureInput(boxAddr, hintAddr, addrLimit, TouchScreenKeyboardType.Default);

            // 처음 열 때만 시안 값을 읽는다. 두 번째부터는 마지막으로 저장한 값을 보여준다.
            if (!seeded)
            {
                savedPhone = hintPhone != null ? hintPhone.text : "";
                savedAddr = hintAddr != null ? hintAddr.text : "";
                seeded = true;
            }
            Set(inName, savedName);
            Set(inPhone, savedPhone);
            Set(inAddr, savedAddr);
            OnAddrChanged(savedAddr);

            if (inAddr != null) inAddr.onValueChanged.AddListener(OnAddrChanged);
            Hook(btnClearName, ClearName);
            Hook(btnCancel, Close);
            Hook(btnNext, ApplyAndClose);
        }

        void OnDisable()
        {
            if (inAddr != null) inAddr.onValueChanged.RemoveListener(OnAddrChanged);
            Unhook(btnClearName); Unhook(btnCancel); Unhook(btnNext);
        }

        // 시안 입력칸은 이미지 + 값 라벨뿐이라 글자를 칠 수 없다. 여기서 진짜 입력칸을 만든다.
        TMP_InputField EnsureInput(RectTransform box, TMP_Text hint, int limit, TouchScreenKeyboardType kb)
        {
            if (box == null) return null;
            var exist = box.GetComponent<TMP_InputField>();
            if (exist != null) return exist;

            // 배경 Image 가 raycastTarget 이 아니면 탭이 안 먹는다 (mkui 는 기본 0 으로 만든다)
            var bg = box.GetComponent<Image>();
            if (bg != null) bg.raycastTarget = true;

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(box, false);
            var art = (RectTransform)area.transform;
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0f, 1f);
            // 값 라벨과 필드 배경이 둘 다 좌상단 앵커/피벗이라 차이를 그대로 쓰면 자리가 맞는다.
            // ※ rect.width 는 OnEnable 에 0 이라 sizeDelta 를 쓴다.
            if (hint != null)
            {
                art.anchoredPosition = hint.rectTransform.anchoredPosition - box.anchoredPosition;
                art.sizeDelta = hint.rectTransform.sizeDelta;
            }
            else
            {
                art.anchoredPosition = new Vector2(16.2f, -16.5f);
                art.sizeDelta = new Vector2(Mathf.Max(0f, box.sizeDelta.x - 71f), 22f);
            }

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(area.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            var tt = textGo.GetComponent<TextMeshProUGUI>();
            if (hint != null)
            {
                tt.font = hint.font;
                tt.fontSharedMaterial = hint.fontSharedMaterial;
                tt.fontSize = hint.fontSize;
                tt.alignment = hint.alignment;
            }
            else
            {
                tt.fontSize = 14f;
                tt.alignment = TextAlignmentOptions.MidlineLeft;
            }
            tt.color = DesignTokens.Text.Primary;   // 시안: 입력된 값은 흰색
            tt.richText = false;
            tt.textWrappingMode = TextWrappingModes.NoWrap;

            var field = box.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = art;
            field.textComponent = tt;
            field.placeholder = hint;
            // 주소도 시안은 한 줄이다(잉크 높이 15.83 = 14px 한글 한 줄). 길면 가로로 밀린다.
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = limit;
            field.keyboardType = kb;
            field.restoreOriginalTextOnEscape = false;
            return field;
        }

        void ClearName()
        {
            Set(inName, "");
            if (inName != null) inName.ActivateInputField();
        }

        void OnAddrChanged(string s)
        {
            if (counter == null) return;
            counter.text = (s != null ? s.Length : 0) + " / " + addrLimit;
        }

        void ApplyAndClose()
        {
            if (inName != null) savedName = inName.text;
            if (inPhone != null) savedPhone = inPhone.text;
            if (inAddr != null) savedAddr = inAddr.text;
            // 비워 둔 칸은 주문 확인 화면의 기존 값을 지우지 않는다
            Put(orderName, savedName);
            Put(orderPhone, savedPhone);
            Put(orderAddr, savedAddr);
            Close();
        }

        void Close()
        {
            var go = sheet != null ? sheet : gameObject;
            go.SetActive(false);
        }

        static void Put(TMP_Text t, string v)
        {
            if (t != null && !string.IsNullOrEmpty(v)) t.text = v;
        }

        static void Set(TMP_InputField f, string v)
        {
            if (f != null) f.SetTextWithoutNotify(v ?? "");
        }

        static void Hook(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null) b.onClick.AddListener(a);
        }

        static void Unhook(Button b)
        {
            if (b != null) b.onClick.RemoveAllListeners();
        }
    }
}
