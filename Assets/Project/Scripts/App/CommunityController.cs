using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 커뮤니티 목록 (시안 '커뮤니티.svg' · '커뮤니티-1.svg'). MarketController 와 같은 모양.
    //
    // 카드 실측(카드 top-left 기준, 카드 373×223 · 세로 피치 243(=간격20)):
    //   뱃지(배경+글자) (16.5,24.5) 72×25 r8(시안 rx7.5) · 글자 Caption1(13 Regular) 가운데
    //   ⋮ (335,25) 24×24 — 시안에서 점 3개 잉크폭이 아이콘 원본(24×24) 그대로라 스케일 없이 그 크기
    //   제목 (16.5,69.5) 310×24 1줄 말줄임 · Title1(18 Bold) — 폭은 ⋮ 와 안 겹치게 335-16.5-8.5(gap) 로 계산
    //   본문 (16.5,100.9) 340×40 2줄 말줄임 · Body4(14 Regular)
    //   작성자·시간 (16.5,148.74) 310×19 · Caption1(13 Regular) #727886
    //   하트 (17.07,180) 18×18 · 카운트 12px Regular, 말풍선 아이콘도 18×18 — 두 아이콘 다
    //   ic_heart_line.png/ic_comment.png 원본(60×60) 잉크폭 역산 스케일이 정확히 0.3 로 일치
    //   그룹 순서: 하트아이콘 → gap5.6 → 카운트 → gap19(그룹간) → 댓글아이콘 → gap5.6 → 카운트
    //
    // 색은 전부 DesignTokens 값 그대로(뱃지 배경 #242831=Stroke.Regular, 뱃지·본문 글자 #D3D5DD=Text.Secondary,
    // 작성자·시간 #727886=Text.Tertiary) — 3x PNG 픽셀 실측과 정확히 일치해 새 hex 를 만들지 않았다.
    public class CommunityController : MonoBehaviour
    {
        [Header("구성")]
        [SerializeField] RectTransform postList;
        [SerializeField] RectTransform scrollContent;   // Content — 카드 수만큼 스크롤 길이를 늘린다 (Market 패턴)
        [SerializeField] TMP_Text fontSource;
        [SerializeField] Sprite cardSprite;        // bg_round16 — 카드 배경(유리)
        [SerializeField] Sprite badgeSprite;       // bg_round8  — 게시판 뱃지 배경(시안 rx7.5 실측, r16 카드용 cardSprite 로는 모서리가 과하게 둥글어져 못 씀)
        [SerializeField] Sprite heartSprite;       // ic_heart.svg — 좋아요 누른 상태(채워진 보라 하트)
        [SerializeField] Sprite heartLineSprite;   // ic_heart_line.png — 기본 상태(흰 아웃라인). 시안 4장 전부 이 모양이라 이게 기본값
        [SerializeField] Sprite commentSprite;     // ic_comment.png
        [SerializeField] Sprite moreSprite;        // ic_more_vert.svg

        [Header("배치 (시안 실측)")]
        [SerializeField] float cardW = 373f;
        [SerializeField] float cardH = 223f;
        [SerializeField] float pitchY = 243f;      // cardH + 간격20

        [Header("스크롤")]
        [SerializeField] float contentPad = 130f;  // 마지막 카드가 탭바 위로 올라올 만큼(Market 과 동일 관례)

        // ── Task 11: 게시판 칩 · 검색바 · 빈 상태 ──────────────
        // 칩 배경/라벨의 TokenStyle.color 는 씬에서 None(0) 으로 죽여 뒀다 — 여기서 절대색으로 직접
        // 칠한다(global.md "TokenStyle.color 가 None 이 아니면 Image.color 를 덮어쓴다" 함정 회피,
        // CommunityWriteController 의 칩과 같은 관례 — "색은 컨트롤러가 관리(color=none)한다").
        [Header("칩 (Task 11 — 시안 실측: 선택 15 Bold 흰색 / 비선택 16 Regular #727886)")]
        [SerializeField] Image chipAllBg;
        [SerializeField] TMP_Text chipAllLabel;
        [SerializeField] Image chipFreeBg;
        [SerializeField] TMP_Text chipFreeLabel;
        [SerializeField] Image chipCertBg;
        [SerializeField] TMP_Text chipCertLabel;

        [Header("검색바 (Task 11)")]
        [SerializeField] RectTransform searchBar;   // TMP_InputField 를 코드로 붙일 대상(=탭 영역)
        [SerializeField] TMP_Text searchHint;       // "검색어를 입력하세요" placeholder 로 재활용

        [Header("빈 상태 (Task 11 — Task4 산출물, 시안 '커뮤니티 검색결과 없음.svg')")]
        [SerializeField] GameObject emptyCard;
        [SerializeField] GameObject labelEmpty;

        const string BoardAll = "전체";
        const string BoardFree = "자유게시판";
        const string BoardCert = "인증게시판";

        // 선택/비선택 칩 라벨 크기 — p_community_head.py 가 실측해 baked 값(전체=15 Bold, 자유/인증=16
        // Regular)을 그대로 재사용한다(별도 svg 로 "자유게시판 선택" 상태가 없어 대칭 적용).
        const float ChipSelectedSize = 15f;
        const float ChipUnselectedSize = 16f;
        static readonly Color ChipUnselectedBg = new Color(1f, 1f, 1f, 0f);   // 비선택 = 배경 없음(alpha0)

        // 카드 한 장의 배치·상수(IconSize/GapAfterIcon/GapGroup/CardBg)는 Task4 에서
        // CommunityCardBuilder 로 옮겼다(내가 쓴 글·댓글 단 글 화면과 공유하기 위해서 — 이 파일의
        // 동작은 바뀌지 않았다, Build() 참고).

        TextMeshProUGUI ruler;   // 숫자 폭을 재는 화면 밖 텍스트 — CommunityCardBuilder.Build() 에 ref 로 넘긴다
        TMP_InputField searchInput;

        void Awake()
        {
            HookChip(chipAllBg, BoardAll);
            HookChip(chipFreeBg, BoardFree);
            HookChip(chipCertBg, BoardCert);
        }

        [Header("정렬 (2026-08-31 — 화살표 아래 = 오래된순)")]
        [SerializeField] GameObject sortRoot;      // BtnSort (씬엔 Button 이 없어 코드로 붙인다)
        [SerializeField] TMP_Text sortLabel;       // "최신순" / "오래된순"
        [SerializeField] RectTransform sortArrow;  // IconSortArrow — 오래된순이면 180도 뒤집는다

        void EnsureSortButton()
        {
            if (sortRoot == null) return;
            var b = sortRoot.GetComponent<UnityEngine.UI.Button>();
            if (b == null)
            {
                b = sortRoot.AddComponent<UnityEngine.UI.Button>();
                b.transition = UnityEngine.UI.Selectable.Transition.None;
                var img = sortRoot.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.raycastTarget = true;
            }
            b.onClick.RemoveListener(ToggleSort);
            b.onClick.AddListener(ToggleSort);
            SyncSortUi();
        }

        void ToggleSort()
        {
            var store = CommunityStore.Instance;
            if (store == null) return;
            store.SetSort(store.Sort == CommunitySort.Latest ? CommunitySort.Oldest
                                                             : CommunitySort.Latest);
            SyncSortUi();
        }

        void SyncSortUi()
        {
            var store = CommunityStore.Instance;
            bool oldest = store != null && store.Sort == CommunitySort.Oldest;
            if (sortLabel != null)
            {
                // '오래된순'(4글자)이 시안 폭(45, 3글자 기준)에서 두 줄로 감겼다 —
                // 한 줄 고정 + 글자폭에 맞춰 라벨·화살표·루트를 다시 앉힌다. 오른쪽 끝은
                // 시안 실측(326.95+63.4=390.35)에 고정한다.
                sortLabel.text = oldest ? "오래된순" : "최신순";
                sortLabel.textWrappingMode = TextWrappingModes.NoWrap;
                sortLabel.ForceMeshUpdate();
                float w = Mathf.Ceil(sortLabel.preferredWidth);
                var lr = sortLabel.rectTransform;
                lr.sizeDelta = new Vector2(w, lr.sizeDelta.y);
                float arrowW = sortArrow != null ? sortArrow.sizeDelta.x : 16.7f;
                if (sortArrow != null)
                {
                    // 씬 피벗이 좌상단이라 180도 회전 시 아이콘이 왼쪽 위로 튕겨 나갔다 —
                    // 중심 피벗으로 바꾸고 같은 자리(씬 실측 y -2.925, 라벨 오른쪽 +1.7)에 앉힌다.
                    sortArrow.pivot = new Vector2(0.5f, 0.5f);
                    sortArrow.anchoredPosition = new Vector2(w + 1.7f + arrowW * 0.5f,
                                                             -2.925f - sortArrow.sizeDelta.y * 0.5f);
                }
                if (sortRoot != null)
                {
                    var rr = (RectTransform)sortRoot.transform;
                    float total = w + 1.7f + arrowW;
                    rr.sizeDelta = new Vector2(total, rr.sizeDelta.y);
                    rr.anchoredPosition = new Vector2(390.35f - total, rr.anchoredPosition.y);
                }
            }
            if (sortArrow != null)
                sortArrow.localEulerAngles = new Vector3(0f, 0f, oldest ? 180f : 0f);
        }

        void OnEnable()
        {
            EnsureSearchInput();
            EnsureSortButton();
            var store = CommunityStore.Instance;
            if (store != null)
            {
                store.OnChanged += Build;
                Build();
            }
            else
            {
                // 유니티는 오브젝트 간 Awake 순서를 보장하지 않는다. CommunityStore 는 Canvas/ScreenManager
                // (항상 켜져 있음)에 있어 보통은 이 화면이 열리기 전에 이미 Awake 가 끝나 있지만,
                // 혹시 순서가 꼬이면 조용히 넘어가지 않고 에러를 남긴다.
                Debug.LogError("[Community] OnEnable 시점에 CommunityStore.Instance 가 없다 — 목록을 못 그림");
            }
        }

        void OnDisable()
        {
            if (CommunityStore.Instance != null) CommunityStore.Instance.OnChanged -= Build;
        }

        // 칩·검색바(Task 11)가 부른다.
        public void SetBoard(string board)
        {
            var store = CommunityStore.Instance;
            if (store == null) { Debug.LogError("[Community] SetBoard: Store 없음"); return; }
            store.Load(board, store.Query);
        }

        public void SetQuery(string query)
        {
            var store = CommunityStore.Instance;
            if (store == null) { Debug.LogError("[Community] SetQuery: Store 없음"); return; }
            store.Load(store.Board, query);
        }

        // ── 칩 배선 (Task 11) ────────────────────────────────
        void HookChip(Image bg, string board)
        {
            if (bg == null) return;
            bg.raycastTarget = true;   // mkui 가 만든 Image 는 raycastTarget 0 — 코드로 켜야 탭이 먹는다.
            var btn = bg.GetComponent<Button>();
            if (btn == null) btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => SetBoard(board));
        }

        // Build() 가 매번 store.Board 기준으로 다시 그린다 — 화면이 꺼졌다 켜져도(탭 전환) 항상
        // 실제 선택 상태와 맞는다(TokenStyle.OnEnable 재적용 순서에 기대지 않는 이유).
        void RefreshChips(string board)
        {
            SetChipVisual(chipAllBg, chipAllLabel, board == BoardAll);
            SetChipVisual(chipFreeBg, chipFreeLabel, board == BoardFree);
            SetChipVisual(chipCertBg, chipCertLabel, board == BoardCert);
        }

        void SetChipVisual(Image bg, TMP_Text label, bool selected)
        {
            if (bg != null) bg.color = selected ? DesignTokens.Action.Pressed : ChipUnselectedBg;
            if (label == null) return;
            label.fontSize = selected ? ChipSelectedSize : ChipUnselectedSize;
            var font = Fonts.ForWeight(selected ? FontWeight.Bold : FontWeight.Regular);
            if (font != null) { label.font = font; label.fontSharedMaterial = font.material; }
            label.color = selected ? DesignTokens.Text.Primary : DesignTokens.Text.Tertiary;
        }

        // ── 검색바 배선 (Task 11) ────────────────────────────
        // 시안 검색바는 이미지 + placeholder 뿐이라 글자를 칠 수 없다 — CommunityPostController.EnsureInput /
        // CommunityWriteController.MakeInput 과 같은 방식으로 코드에서 진짜 입력칸을 붙인다.
        // TMP_InputField 는 placeholder 를 물려주면 입력이 있을 때 자동으로 숨긴다(별도 코드 불필요).
        void EnsureSearchInput()
        {
            if (searchInput != null || searchBar == null) return;

            var bg = searchBar.GetComponent<Image>();
            if (bg != null) bg.raycastTarget = true;   // mkui 함정 — 코드로 켜야 탭이 먹는다.

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(searchBar, false);
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            // LabelSearchHint 실측(52.22,0,300,55) 그대로 — 오른쪽 여백 371-52.22-300=18.78
            art.offsetMin = new Vector2(52.22f, 6f);
            art.offsetMax = new Vector2(-18.78f, -6f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(area.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            var tt = textGo.GetComponent<TextMeshProUGUI>();
            if (searchHint != null)
            {
                tt.font = searchHint.font;
                tt.fontSharedMaterial = searchHint.fontSharedMaterial;
                tt.fontSize = searchHint.fontSize;
                tt.fontWeight = searchHint.fontWeight;
                tt.characterSpacing = searchHint.characterSpacing;
                tt.lineSpacing = searchHint.lineSpacing;
                tt.alignment = searchHint.alignment;
            }
            tt.color = DesignTokens.Text.Primary;   // 입력된 값은 흰색(placeholder 만 회색 TextTertiary)
            tt.richText = false;
            tt.textWrappingMode = TextWrappingModes.NoWrap;

            var field = searchBar.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = art;
            field.textComponent = tt;
            InputFieldTuner.Tune(field);   // ⚠ textComponent 배선 뒤에 불러야 캐럿(|)이 생긴다
            field.placeholder = searchHint;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 50;
            field.onValueChanged.AddListener(SetQuery);
            searchInput = field;
        }

        // 시안(sian76 커뮤니티)에서 카드 제목은 ⋮ 자리(335 - gap8.5)를 비켜 310 폭이다.
        // 내가 쓴 글·댓글 단 글(sian84, ⋮ 없음)은 MyPostListController 가 CommunityCardBuilder.Build() 를
        // 340(body 와 같은 폭)으로 부른다 — task-4-report.md 참고.
        const float TitleWWithMore = 310f;

        void Build()
        {
            if (postList == null) { Debug.LogError("[Community] postList 가 비어 있다"); return; }
            var store = CommunityStore.Instance;
            if (store == null) { Debug.LogError("[Community] Build: Store 없음"); return; }

            var posts = store.Posts;
            int n = posts == null ? 0 : posts.Count;

            RefreshChips(store.Board);

            // 빈 상태 (Task 11) — 결과 0건이면 EmptyCard·LabelEmpty 를 둘 다 켜고, 있으면 둘 다 끈다.
            // 문구는 시안 '커뮤니티 검색결과 없음.svg' 그대로(Task4 산출물).
            UiUtil.SetActive(emptyCard, n == 0);
            UiUtil.SetActive(labelEmpty, n == 0);

            // 카드 한 장의 배치는 CommunityCardBuilder(Task4, MyPostListController 와 공유)가 만든다 —
            // 아래 호출은 예전 인라인 for 루프와 완전히 같은 값을 그대로 넘긴다(순수 이동, 동작 불변).
            var sprites = new CommunityCardBuilder.Sprites
            {
                card = cardSprite, badge = badgeSprite, heart = heartSprite,
                heartLine = heartLineSprite, comment = commentSprite, more = moreSprite,
            };
            float listH = CommunityCardBuilder.Build(postList, posts, fontSource, sprites,
                cardW, cardH, pitchY, true, TitleWWithMore, ref ruler, transform, OnMoreClick);

            Debug.Log($"[Community] 카드 {n}장 생성 (postList={postList.name})");

            if (scrollContent != null)
            {
                float top = -postList.anchoredPosition.y;
                var s2 = scrollContent.sizeDelta;
                scrollContent.sizeDelta = new Vector2(s2.x, top + listH + contentPad);
            }
        }

        // ⋮ 팝업 위치 (시안 '커뮤니티.svg' ⋮ 팝업 열린 상태 실측): 팝업 top-left 는 이 화면의 ⋮ 아이콘
        // top-left 로부터 (-121, +28) — 상세(BtnMore)의 오프셋(-110,+12)과 다르다(화면마다 실측이
        // 다름 — task-10-report.md 대조표 참고). CanvasTopLeft() 로 지금 이 카드의 실제 화면 위치
        // (스크롤로 계속 바뀐다)를 구한 뒤 오프셋을 더한다.
        static readonly Vector2 MorePopupOffset = new Vector2(-121f, 28f);

        void OnMoreClick(string id, bool mine, RectTransform moreRect)
        {
            if (PostMenuPopup.Instance == null) { Debug.LogError("[Community] OnMoreClick: PostMenuPopup.Instance 없음"); return; }
            Vector2 anchor = PostMenuPopup.CanvasTopLeft(moreRect) + MorePopupOffset;
            PostMenuPopup.Instance.Open(id, mine, anchor);
        }
    }
}
