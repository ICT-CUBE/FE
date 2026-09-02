using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 프로필 편집 (시안 '마이 프로필 편집.svg'). 아바타 3종(남성형/여성형/동물형, AvatarNameBinder.profiles
    // 와 같은 순서) 중 하나를 고르고 닉네임을 바꾼 뒤 저장한다.
    //
    // 선택 표시는 이 프로젝트의 기존 관례를 그대로 따른다 — Screen_Nickname 의 NicknameFormBinder 가
    // 자식 "Ring" 을 선택 시에만 켜는 방식과 동일하게, 여기서는 avatarRings/avatarChecks 를 선택된
    // 인덱스에서만 켠다(둘 다 시안에 있는 요소라 오브젝트를 새로 만들지 않고 활성/비활성만 토글).
    //
    // 닉네임 입력칸은 시안이 placeholder 뿐이라 CommunityWriteController.MakeInput() 과 같은 방식으로
    // 런타임에 진짜 TMP_InputField 를 붙인다. X 지우기 버튼은 텍스트가 있을 때만 보인다.
    //
    // 저장은 서버 API 가 없어 ProfileStore(PlayerPrefs) 에 남기고, Screen_My 로 돌아가면
    // MyController.OnEnable() 이 같은 저장소를 읽어 아바타·닉네임을 반영한다(LoginModeSwitcher 와
    // 같은 PlayerPrefs 관례 — CURRENT STATE 문서에서 유저가 못박음).
    public class MyProfileEditController : MonoBehaviour
    {
        [Header("아바타 3종 (0 남성형 · 1 여성형 · 2 동물형)")]
        [SerializeField] Button[] avatarButtons;
        // 링은 항상 보인다 — 선택이면 보라(+체크 배지, PNG 안에 포함), 아니면 회색.
        // (예전엔 링 GameObject 를 켜고 끄고 체크 배지를 따로 뒀지만, 새 에셋 avatar_ring_on/off.png
        //  가 체크 배지를 포함하고 있어 스프라이트만 바꾸면 된다 — avatarChecks 는 더 이상 안 쓴다,
        //  씬에서 Check1~3 은 지우지 않고 m_IsActive: 0 으로 꺼뒀다.)
        [SerializeField] Image[] avatarRingImages;
        [SerializeField] Sprite ringOnSprite;
        [SerializeField] Sprite ringOffSprite;
        [SerializeField] GameObject[] avatarDims;    // 미선택일 때만 켜는 딤(회색 처리)

        [Header("닉네임")]
        [SerializeField] RectTransform nicknameField;  // TMP_InputField 를 코드로 붙일 대상(=탭 영역)
        [SerializeField] TMP_Text nicknameHint;         // 스타일 템플릿 + placeholder (시안엔 빈 상태가 없어 문구 없음)
        [SerializeField] Button clearButton;            // "X" — 텍스트가 있을 때만 보인다

        [Header("저장")]
        [SerializeField] Button saveButton;

        TMP_InputField nicknameInput;
        int selected = -1;

        void Awake()
        {
            if (avatarButtons != null)
            {
                for (int i = 0; i < avatarButtons.Length; i++)
                {
                    if (avatarButtons[i] == null) continue;
                    int idx = i;   // 클로저 캡처 주의 (NicknameFormBinder 와 같은 이유)
                    avatarButtons[i].onClick.AddListener(() => SelectAvatar(idx));
                }
            }
            if (clearButton != null) clearButton.onClick.AddListener(ClearNickname);
            if (saveButton != null) saveButton.onClick.AddListener(Save);
        }

        void OnEnable()
        {
            EnsureInput();

            int max = (avatarButtons != null ? avatarButtons.Length : 1) - 1;
            selected = Mathf.Clamp(ProfileStore.AvatarIndex, 0, Mathf.Max(0, max));
            if (nicknameInput != null) nicknameInput.text = ProfileStore.Nickname;

            RefreshSelection();
            RefreshClear();
        }

        // 시안 입력칸은 placeholder 뿐이라 글자를 칠 수 없다 — CommunityWriteController.MakeInput() 과
        // 같은 방식으로 코드에서 진짜 입력칸을 붙인다.
        void EnsureInput()
        {
            if (nicknameInput != null || nicknameField == null) return;

            // mkui 가 만든 Image 는 raycastTarget 이 기본 0 이라 탭이 안 먹는다 — 켜야 한다.
            var bg = nicknameField.GetComponent<Image>();
            if (bg != null) bg.raycastTarget = true;

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(nicknameField, false);
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = Vector2.zero; art.offsetMax = Vector2.zero;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(area.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            var tt = textGo.GetComponent<TextMeshProUGUI>();
            if (nicknameHint != null)
            {
                tt.font = nicknameHint.font;
                tt.fontSharedMaterial = nicknameHint.fontSharedMaterial;
                tt.fontSize = nicknameHint.fontSize;
                tt.fontWeight = nicknameHint.fontWeight;
                tt.characterSpacing = nicknameHint.characterSpacing;
                tt.lineSpacing = nicknameHint.lineSpacing;
                tt.alignment = nicknameHint.alignment;
            }
            tt.color = DesignTokens.Text.Primary;      // 입력된 값은 흰색(placeholder 만 회색)
            tt.richText = false;
            tt.textWrappingMode = TextWrappingModes.NoWrap;

            nicknameInput = nicknameField.gameObject.AddComponent<TMP_InputField>();
            nicknameInput.textViewport = art;
            nicknameInput.textComponent = tt;
            InputFieldTuner.Tune(nicknameInput);   // ⚠ textComponent 배선 뒤에 불러야 캐럿(|)이 생긴다
            nicknameInput.placeholder = nicknameHint;
            nicknameInput.lineType = TMP_InputField.LineType.SingleLine;
            nicknameInput.onValueChanged.AddListener(_ => RefreshClear());
        }

        void SelectAvatar(int idx)
        {
            selected = idx;
            RefreshSelection();
        }

        void RefreshSelection()
        {
            int ringCount = avatarRingImages != null ? avatarRingImages.Length : 0;
            for (int i = 0; i < ringCount; i++)
            {
                if (avatarRingImages[i] == null) continue;
                avatarRingImages[i].gameObject.SetActive(true);   // 링은 항상 보인다
                avatarRingImages[i].sprite = (i == selected) ? ringOnSprite : ringOffSprite;
            }

            int dimCount = avatarDims != null ? avatarDims.Length : 0;
            for (int i = 0; i < dimCount; i++)
                if (avatarDims[i] != null) avatarDims[i].SetActive(i != selected);   // 딤은 미선택일 때만
        }

        void ClearNickname()
        {
            if (nicknameInput != null) nicknameInput.text = "";
            RefreshClear();
        }

        void RefreshClear()
        {
            bool has = nicknameInput != null && !string.IsNullOrEmpty(nicknameInput.text);
            if (clearButton != null) clearButton.gameObject.SetActive(has);
        }

        void Save()
        {
            if (selected >= 0) ProfileStore.AvatarIndex = selected;
            if (nicknameInput != null) ProfileStore.Nickname = nicknameInput.text;

            // Screen_My 로 돌아가면 MyController.OnEnable() 이 ProfileStore 를 다시 읽어 반영한다.
            // (CommunityWriteController 가 등록 후 목록으로 돌아가 그 화면 기준 bottom 으로 토스트를
            // 띄우는 것과 같은 순서 — 화면 전환 다음에 Toast).
            if (ScreenManager.Instance != null) ScreenManager.Instance.Back();
            else Debug.LogError("[MyProfileEdit] 저장: ScreenManager.Instance 가 없다");

            // 시안 '마이 토스트 모음.svg' 문구 그대로. bottom=130 은 그 시안에서 실측한 값
            // (pill y=717~763, 캔버스 893 기준 893-763=130 — 기본값 58 은 이 시안의 실제 위치가 아니다,
            // Task6 브리프: "마이 계열은 탭바가 없어도 시안에서 y 를 재서 맞는 bottom 을 넣어라").
            Toast.Show("프로필이 저장되었습니다.", 130f);
        }
    }
}
