using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 커뮤니티 글쓰기 (시안 '커뮤니티 글쓰기.svg' · '-1.svg' · '글 입력안한 상태_토스트 표시_.svg').
    // Screen_Community 의 BtnWrite(ScreenButton) 가 이 화면을 연다.
    //
    // 구조: 다른 커뮤니티 화면과 같은 WriteScroll(ScrollRect)→Viewport→Content 안에 헤더까지 넣는다.
    // 단, 사진 줄(PhotoDivider/PhotoRow)은 스크롤 밖(화면 루트 직계)에 고정했다 — '커뮤니티 글쓰기.svg'와
    // '-1.svg'는 본문 길이가 다른데도(placeholder 1줄 vs 실제 2줄) 사진 줄의 y 가 소수점까지 완전히
    // 동일(divider 788, 아이콘 815.67 등)해서, 본문 흐름에 안 얹혀 있고 화면에 고정돼 있다고 판단했다
    // (Task7 CommentInputBar 와 같은 근거·같은 결론).
    //
    // 등록 활성 조건(시안 '글 입력안한 상태_토스트 표시_.svg'의 디자이너 메모 "제목과 내용 둘 다 채워야만
    // 등록 글자가 활성화 돼요!"): 제목·내용이 둘 다 비어있지 않아야 한다. 버튼 자체는 항상 눌리고,
    // 비활성 상태에서 누르면 시안 그대로의 토스트를 띄운다. 색만 회색(TextTertiary)↔보라(ActionPrimary)로
    // 바뀐다(시안 두 상태 대조로 확인).
    public class CommunityWriteController : MonoBehaviour
    {
        [Header("헤더")]
        [SerializeField] TMP_Text btnRegisterLabel;   // 텍스트 자체가 버튼(BtnLike 와 같은 패턴)

        [Header("게시판 칩 (Screen_Community 의 ChipFree/ChipCert 구성 재사용 — '전체' 없음)")]
        [SerializeField] Image chipFreeBg;
        [SerializeField] TMP_Text chipFreeLabel;
        [SerializeField] Image chipCertBg;
        [SerializeField] TMP_Text chipCertLabel;

        [Header("제목")]
        [SerializeField] RectTransform titleField;    // TMP_InputField 를 코드로 붙일 대상(=탭 영역)
        [SerializeField] TMP_Text titleHint;          // "제목을 입력하세요" placeholder

        [Header("내용")]
        [SerializeField] RectTransform bodyField;
        [SerializeField] TMP_Text bodyHint;           // "내용을 입력하세요" placeholder

        [Header("사진 줄 (스크롤 밖 고정 — Task9 Sheet_CommunityPhoto 가 여기 Attach() 를 부른다)")]
        [SerializeField] RectTransform photoRow;       // 탭 영역 전체(아이콘+라벨의 부모)
        [SerializeField] TMP_Text labelPhotoCount;     // "사진 (n/5장)"
        // photoSheet 를 열던 경로는 최종 리뷰에서 제거했다(I+1, 이중 배선 정리) — PhotoRow 를 여는
        // 배선은 OpenSheetButton(씬 컴포넌트, sheet: Sheet_CommunityPhoto)만 남긴다. 그쪽은
        // SetAsLastSibling() 까지 해서 다른 오버레이 뒤에 깔리지 않고, 프로젝트 표준(Sheet_* 오버레이는
        // OpenSheetButton 이 연다)과도 맞는다.

        const string BoardFree = "자유게시판";
        const string BoardCert = "인증게시판";

        // 시안 '글 입력안한 상태_토스트 표시_.svg' 박스 (20,727)372x46 → 바닥까지 893-727-46=120
        // (이 화면엔 사진 줄이 화면 하단에 고정으로 깔려 있어 Toast 기본값(58)을 쓰면 가려진다 —
        //  CommunityPostController.ToastBottom 과 같은 이유).
        const float ToastBottomOnWrite = 120f;

        // 등록 성공 토스트는 목록 화면(Community)으로 이동한 뒤에 뜬다 — 그 화면 시안
        // '커뮤니티-1.svg' 의 토스트 박스 (20,713)372x46 → 바닥까지 893-713-46=134
        // (Screen_Community 는 하단에 TabBar 가 있어 기본값(58)과 다르다).
        const float ToastBottomOnList = 134f;

        const int MaxPhotos = 5;

        TMP_InputField titleInput, bodyInput;
        string board = BoardFree;
        List<Sprite> photos = new List<Sprite>();

        void Awake()
        {
            HookButton(btnRegisterLabel, OnRegisterClick);
            HookButton(chipFreeBg, () => SetBoard(BoardFree));
            HookButton(chipCertBg, () => SetBoard(BoardCert));
        }

        void OnEnable()
        {
            EnsureInputs();
            // 매번 새로 여는 글쓰기 화면 — 이전에 쓰던 내용을 남기지 않는다(초안 저장은 이 태스크 범위 밖).
            if (titleInput != null) titleInput.text = "";
            if (bodyInput != null) bodyInput.text = "";
            photos.Clear();
            RefreshPhotoLabel();
            SetBoard(BoardFree);
            RefreshRegisterVisual();
        }

        void HookButton(Graphic g, UnityEngine.Events.UnityAction onClick)
        {
            if (g == null) return;
            g.raycastTarget = true;
            var btn = g.GetComponent<Button>();
            if (btn == null) btn = g.gameObject.AddComponent<Button>();
            btn.targetGraphic = g;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(onClick);
        }

        // 시안 입력칸은 placeholder 뿐이라 글자를 칠 수 없다 — CommunityPostController.EnsureInput 과
        // 같은 방식으로 코드에서 진짜 입력칸을 붙인다. 제목/본문 둘 다 hint 와 필드가 같은 자리(같은
        // rect)라 TextArea 를 필드 전체에 꽉 채우면(anchor 0~1, offset 0) 그대로 placeholder 위치와 맞는다.
        void EnsureInputs()
        {
            if (titleInput == null)
            {
                titleInput = MakeInput(titleField, titleHint, TMP_InputField.LineType.SingleLine, 0);
                // 화면이 다시 열릴 때마다(OnEnable) EnsureInputs() 가 또 불리므로, 리스너는 필드를
                // 처음 만들 때 한 번만 건다(안 그러면 열 때마다 중복 등록된다).
                if (titleInput != null) titleInput.onValueChanged.AddListener(_ => RefreshRegisterVisual());
            }
            if (bodyInput == null)
            {
                bodyInput = MakeInput(bodyField, bodyHint, TMP_InputField.LineType.MultiLineNewline, 0);
                if (bodyInput != null) bodyInput.onValueChanged.AddListener(_ => RefreshRegisterVisual());
            }
        }

        TMP_InputField MakeInput(RectTransform box, TMP_Text hint, TMP_InputField.LineType lineType, int charLimit)
        {
            if (box == null) return null;

            // mkui 가 만든 Image 는 raycastTarget 이 기본 0 이라 탭이 안 먹는다 — 켜야 한다.
            var bg = box.GetComponent<Image>();
            if (bg != null) bg.raycastTarget = true;

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(box, false);
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = Vector2.zero; art.offsetMax = Vector2.zero;

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
                tt.fontWeight = hint.fontWeight;
                tt.characterSpacing = hint.characterSpacing;
                tt.lineSpacing = hint.lineSpacing;
                tt.alignment = hint.alignment;
            }
            tt.color = DesignTokens.Text.Primary;   // 입력된 값은 흰색(placeholder 만 회색 TextTertiary)
            tt.richText = false;
            tt.textWrappingMode = lineType == TMP_InputField.LineType.MultiLineNewline
                ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;

            var field = box.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = art;
            field.textComponent = tt;
            InputFieldTuner.Tune(field);   // ⚠ textComponent 배선 뒤에 불러야 캐럿(|)이 생긴다
            field.placeholder = hint;
            field.lineType = lineType;
            if (charLimit > 0) field.characterLimit = charLimit;
            return field;
        }

        void SetBoard(string b)
        {
            board = b;
            bool free = board == BoardFree;
            var selBg = DesignTokens.Action.Pressed;
            var unselBg = new Color(1f, 1f, 1f, 0f);   // Screen_Community 의 비선택 칩과 동일(투명 = 배경 없음)

            if (chipFreeBg != null) chipFreeBg.color = free ? selBg : unselBg;
            if (chipFreeLabel != null) chipFreeLabel.color = free ? DesignTokens.Text.Primary : DesignTokens.Text.Tertiary;
            if (chipCertBg != null) chipCertBg.color = !free ? selBg : unselBg;
            if (chipCertLabel != null) chipCertLabel.color = !free ? DesignTokens.Text.Primary : DesignTokens.Text.Tertiary;
        }

        bool IsValid()
        {
            string title = titleInput != null ? titleInput.text : "";
            string body = bodyInput != null ? bodyInput.text : "";
            return !string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(body);
        }

        void RefreshRegisterVisual()
        {
            if (btnRegisterLabel == null) return;
            // #A855F7 — BtnWrite 의 "새 글 쓰기"(Screen_Community, p_community_head.py)와 같은
            // 컨벤션으로 Text.Point 를 쓴다(Action.Primary 와 Default 모드에서 값은 같다).
            btnRegisterLabel.color = IsValid() ? DesignTokens.Text.Point : DesignTokens.Text.Tertiary;
        }

        void OnRegisterClick()
        {
            if (!IsValid())
            {
                // 시안 '글 입력안한 상태_토스트 표시_.svg' 문구 그대로.
                Toast.Show("제목과 내용을 모두 입력해주세요.", ToastBottomOnWrite);
                return;
            }

            var store = CommunityStore.Instance;
            if (store == null) { Debug.LogError("[CommunityWrite] 등록: CommunityStore.Instance 가 없다"); return; }

            var draft = new CommunityPost
            {
                board = board,
                title = titleInput.text.Trim(),
                body = bodyInput.text.Trim(),
            };
            if (photos.Count > 0) draft.photos.AddRange(photos);

            // Create() 가 id·timeText="방금 전"·mine=true 를 채우고 목록 맨 앞에 넣은 뒤 다시 읽는다.
            store.Create(draft);

            if (ScreenManager.Instance != null) ScreenManager.Instance.Show("Community");
            else Debug.LogError("[CommunityWrite] 등록: ScreenManager.Instance 가 없다");

            // 시안 '커뮤니티-1.svg' 문구 그대로.
            Toast.Show("게시글이 등록되었습니다.", ToastBottomOnList);
        }

        // Task9 의 사진 선택 시트가 첨부 결과를 돌려줄 때 부른다.
        public void Attach(List<Sprite> newPhotos)
        {
            photos = newPhotos ?? new List<Sprite>();
            if (photos.Count > MaxPhotos) photos = photos.GetRange(0, MaxPhotos);
            RefreshPhotoLabel();
        }

        void RefreshPhotoLabel()
        {
            if (labelPhotoCount != null) labelPhotoCount.text = $"사진 ({photos.Count}/{MaxPhotos}장)";
        }
    }
}
