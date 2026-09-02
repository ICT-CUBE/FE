using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 텍스트 대화 보관함. '텍스트 대화 보관함.svg' 실측 그대로.
    //
    //   카드    x=21 w=371, 첫 카드 y=160.5, 카드 간격 17
    //           접힘 96 / 펼침 213(본문 4줄 기준) — 높이는 줄 수로 계산한다
    //           배경 #0F0D17@50%(Bg.Glass) r16, 테두리 펼침 #A855F7 / 접힘 #181A20
    //   날짜    카드 +(20.97, 25.18)  13px #727886
    //   본문    카드 +(19.77, 59.32)  14px, 폭 328, 줄 간격 21(=14×150%)
    //           접힘이면 1줄 말줄임
    //   더보기  ⋮ 24×24, 중심 = 카드 +(341.5, 29.5)
    //   삭제 팝오버  카드 +(228, 30.5) 124×56 #181A20, 휴지통 + '삭제하기' #FF5B6F
    //   이동 버튼    카드 +(220, 156) 131×37 pill, 테두리 #242831, 라벨 #A855F7 16px
    //
    // 카드가 스크롤 Content 안에 들어가므로 x 는 20, y 는 160 을 빼서 넣는다(스크롤 상단 여백).
    public class TalkArchiveController : MonoBehaviour
    {
        [System.Serializable]
        public class Item
        {
            public string date;
            [TextArea(2, 6)] public string text;
            public int id;               // 서버 메시지 id — 보관 해제(unsave)에 쓴다
            public Item(string d, string t) { date = d; text = t; }
        }

        [Header("상단")]
        [SerializeField] TMP_Text labelCount;

        // 정렬 토글 — 누르면 최신 순 ↔ 오래된 순. 셰브론은 180° 돌려 '^' 로 쓴다.
        [Header("정렬")]
        [SerializeField] TMP_Text labelSort;        // BtnSort ("최신 순")
        [SerializeField] Button btnSort;            // 글자 쪽 히트
        [SerializeField] Button btnSortIcon;        // 화살표 쪽 히트
        [SerializeField] RectTransform sortChevron; // SortChevron
        [SerializeField] string sortDescText = "최신 순";
        [SerializeField] string sortAscText = "오래된 순";
        bool newestFirst = true;

        [Header("목록")]
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] Sprite cardBg;        // bg_round16
        [SerializeField] Sprite cardBorder;    // bg_round16_stroke
        [SerializeField] Sprite pillStroke;    // bg_pill_stroke
        [SerializeField] Sprite pillFill;      // bg_pill
        [SerializeField] Sprite moreIcon;      // ic_more_vert
        [SerializeField] Sprite trashIcon;     // ic_trash

        [Header("이동 / 삭제")]
        [SerializeField] string detailScreen = "TalkDetail";
        [SerializeField] GameObject modalDelete;      // Modal_ArchiveDelete
        [SerializeField] Button btnDeleteCancel;
        [SerializeField] Button btnDeleteConfirm;
        // 목록 화면과 같은 모달을 공유한다(시안 좌표 동일) → 열 때 문구를 시안대로 바꾼다
        [SerializeField] TMP_Text modalTitle;
        [SerializeField] TMP_Text modalSub;

        // ★ SVG 실측값은 '획 중심선' 이라 프레임보다 0.5씩 안쪽이다.
        //   Figma 프레임: 목록 컨테이너 width 372 · gap 16 (spacing/4).
        //   중심선 371/17 을 그대로 쓰면 카드가 x=1 에서 시작해 오른쪽 끝(372)이
        //   뷰포트 경계에 딱 붙어 테두리가 잘려 보인다 → 프레임값 0/372/16 으로.
        [Header("시안 실측 (Figma 프레임 기준)")]
        [SerializeField] float cardX = 0f;
        [SerializeField] float cardW = 372f;
        [SerializeField] float firstY = 0f;
        [SerializeField] float cardGap = 16f;
        [SerializeField] float dateY = 25.18f;
        [SerializeField] float bodyY = 59.32f;
        [SerializeField] float bodyX = 19.77f;
        [SerializeField] float bodyW = 328f;
        [SerializeField] float lineH = 21f;
        [SerializeField] float collapsedPad = 15.68f;   // 본문 1줄 아래 여백 → 96
        [SerializeField] float buttonGap = 12.68f;
        [SerializeField] float buttonH = 37f;
        [SerializeField] float bottomPad = 20f;

        // 2026-08-25: 시안 더미 → 서버 보관함(GET conversation-archive) 으로 대체.
        List<Item> items = new List<Item>();

        int expanded = -1;     // 유저 지시: 처음엔 전부 한 줄, 누른 카드만 펼친다
        int menuOpen = -1;
        int pendingDelete = -1;
        readonly List<GameObject> views = new List<GameObject>();

        void OnEnable()
        {
            if (btnDeleteCancel != null) btnDeleteCancel.onClick.AddListener(CloseDelete);
            if (btnDeleteConfirm != null) btnDeleteConfirm.onClick.AddListener(ConfirmDelete);
            if (btnSort != null) btnSort.onClick.AddListener(ToggleSort);
            if (btnSortIcon != null) btnSortIcon.onClick.AddListener(ToggleSort);
            menuOpen = -1;
            CloseDelete();

            var store = ConversationStore.Instance;
            if (store == null)
            {
                Debug.LogError("[Conv] ConversationStore 가 없다 — Canvas/ScreenManager 에 붙일 것");
                ApplySort(); Build();
                return;
            }
            store.LoadArchive(list =>
            {
                items.Clear();
                foreach (var s in list)
                {
                    var it = new Item(s.dateText, s.text) { id = s.id };
                    items.Add(it);
                }
                expanded = -1;
                ApplySort();
                Build();
            });
            ApplySort();
            Build();
        }

        void OnDisable()
        {
            if (btnDeleteCancel != null) btnDeleteCancel.onClick.RemoveAllListeners();
            if (btnDeleteConfirm != null) btnDeleteConfirm.onClick.RemoveAllListeners();
            if (btnSort != null) btnSort.onClick.RemoveAllListeners();
            if (btnSortIcon != null) btnSortIcon.onClick.RemoveAllListeners();
        }

        void ToggleSort()
        {
            newestFirst = !newestFirst;
            expanded = -1;
            menuOpen = -1;
            ApplySort();
            Build();
        }

        // 날짜 문자열("2026. 06. 10")을 숫자로 바꿔 정렬한다. 형식이 다르면 원래 순서를 유지한다.
        void ApplySort()
        {
            items.Sort((a, b) =>
            {
                int ka = DateKey(a.date), kb = DateKey(b.date);
                return newestFirst ? kb.CompareTo(ka) : ka.CompareTo(kb);
            });

            if (labelSort != null) labelSort.text = newestFirst ? sortDescText : sortAscText;
            // 시안 셰브론은 아래 방향(∨). 오래된 순이면 뒤집어 '^' 로.
            if (sortChevron != null)
                sortChevron.localEulerAngles = new Vector3(0f, 0f, newestFirst ? 0f : 180f);
        }

        static int DateKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            var nums = System.Text.RegularExpressions.Regex.Matches(s, @"\d+");
            int y = nums.Count > 0 ? int.Parse(nums[0].Value) : 0;
            int m = nums.Count > 1 ? int.Parse(nums[1].Value) : 0;
            int d = nums.Count > 2 ? int.Parse(nums[2].Value) : 0;
            return y * 10000 + m * 100 + d;
        }

        // 시안 '텍스트 대화 보관함 대화 삭제' — ⋮ → 삭제하기 는 곧바로 지우지 않고 확인 모달을 띄운다
        void AskDelete(int index)
        {
            pendingDelete = index;
            menuOpen = -1;
            if (modalTitle != null) modalTitle.text = "이 대화를 보관함에서 삭제하시겠습니까?";
            if (modalSub != null) modalSub.text = "보관만 해제되며, 대화 내용은 삭제되지 않아요.";
            Build();
            if (modalDelete != null)
            {
                modalDelete.SetActive(true);
                modalDelete.transform.SetAsLastSibling();
            }
        }

        void CloseDelete()
        {
            pendingDelete = -1;
            if (modalDelete != null) modalDelete.SetActive(false);
        }

        void ConfirmDelete()
        {
            if (pendingDelete >= 0 && pendingDelete < items.Count)
            {
                var it = items[pendingDelete];
                // 서버 보관 해제(DELETE …/unsave). 성공하면 화면에서도 뺀다.
                if (ConversationStore.Instance != null && it.id > 0)
                    ConversationStore.Instance.Unsave(it.id, () =>
                    {
                        items.Remove(it);
                        if (expanded >= items.Count) expanded = items.Count - 1;
                        Build();
                    });
                else
                {
                    items.RemoveAt(pendingDelete);
                    if (expanded >= items.Count) expanded = items.Count - 1;
                }
            }
            CloseDelete();
            Build();
        }

        void Build()
        {
            foreach (var v in views) if (v != null) DestroyImmediate(v);
            views.Clear();
            if (content == null) return;

            if (labelCount != null) labelCount.text = items.Count + "개의 대화";

            float y = firstY;
            for (int i = 0; i < items.Count; i++)
            {
                float h = MakeCard(i, y);
                y += h + cardGap;
            }
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(0f, y - cardGap));
        }

        float MakeCard(int index, float y)
        {
            var item = items[index];
            bool open = index == expanded;

            var card = UiUtil.MakeNode(content, "Card" + index);
            var bg = UiUtil.MakeImage(card, "Bg", cardBg, DesignTokens.Bg.Glass, Image.Type.Sliced, true);
            var brt = (RectTransform)bg.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

            int idx = index;
            var pick = bg.gameObject.AddComponent<Button>();
            pick.targetGraphic = bg;
            pick.transition = Selectable.Transition.None;
            pick.onClick.AddListener(() =>
            {
                expanded = (expanded == idx) ? -1 : idx;
                menuOpen = -1;
                Build();
            });

            var border = UiUtil.MakeImage(card, "Border", cardBorder,
                                          open ? DesignTokens.Action.Primary : DesignTokens.Stroke.Ultralight,
                                          Image.Type.Sliced);
            var rrt = (RectTransform)border.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

            var date = UiUtil.MakeText(card, "Date", DesignTokens.Type.Caption1, DesignTokens.Text.Tertiary);
            date.text = item.date;
            UiUtil.Place(date, 20.97f, dateY, 200f, 18f);

            var body = UiUtil.MakeText(card, "Body", DesignTokens.Type.Body3, DesignTokens.Text.Primary);
            body.text = item.text;
            UiUtil.Place(body, bodyX, bodyY, bodyW, lineH);

            // 줄 수는 실제 렌더 폭으로 계산한다 — 접힘은 1줄, 펼침은 전부
            int lines = open ? Mathf.Max(1, Mathf.RoundToInt(body.GetPreferredValues(item.text, bodyW, 0f).y / lineH)) : 1;
            body.overflowMode = open ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            body.enableWordWrapping = open;
            ((RectTransform)body.transform).sizeDelta = new Vector2(bodyW, lines * lineH);

            float h = open
                ? bodyY + lines * lineH + buttonGap + buttonH + bottomPad
                : bodyY + lineH + collapsedPad;

            if (open)
            {
                float by = bodyY + lines * lineH + buttonGap;
                var btnBg = UiUtil.MakeImage(card, "BtnGoBg", pillFill, DesignTokens.Bg.Glass, Image.Type.Sliced, true);
                UiUtil.Place(btnBg, cardW - 20f - 131f, by, 131f, buttonH);
                var btnLine = UiUtil.MakeImage(card, "BtnGoBorder", pillStroke, DesignTokens.Stroke.Regular, Image.Type.Sliced);
                UiUtil.Place(btnLine, cardW - 20f - 131f, by, 131f, buttonH);
                var btnLabel = UiUtil.MakeText(card, "BtnGoLabel", DesignTokens.Type.Body2,
                                               DesignTokens.Text.Point, TextAlignmentOptions.Center);
                btnLabel.text = "대화로 이동하기";
                UiUtil.Place(btnLabel, cardW - 20f - 131f, by, 131f, buttonH);

                var go = btnBg.gameObject.AddComponent<Button>();
                go.targetGraphic = btnBg;
                go.transition = Selectable.Transition.None;
                go.onClick.AddListener(() =>
                {
                    if (ScreenManager.Instance != null) ScreenManager.Instance.Show(detailScreen);
                });
            }

            // ⋮ 더보기 — 중심 (341.5, 29.5)
            var more = UiUtil.MakeImage(card, "More", moreIcon, DesignTokens.Text.Primary);
            UiUtil.Place(more, 341.5f - 12f, 29.5f - 12f, 24f, 24f);
            var moreHit = UiUtil.MakeHit(card, "MoreHit", () =>
            {
                menuOpen = (menuOpen == idx) ? -1 : idx;
                Build();
            });
            UiUtil.Place(moreHit, 341.5f - 20f, 29.5f - 20f, 40f, 40f);

            if (menuOpen == index) MakeDeleteMenu(card, index);

            UiUtil.Place(card, cardX, y, cardW, h);
            views.Add(card.gameObject);
            return h;
        }

        void MakeDeleteMenu(RectTransform card, int index)
        {
            var menu = UiUtil.MakeImage(card, "DeleteMenu", cardBg, DesignTokens.Bg.HighSurface,
                                        Image.Type.Sliced, true);
            UiUtil.Place(menu, 228f, 30.5f, 124f, 56f);

            var icon = UiUtil.MakeImage(menu.transform, "Trash", trashIcon, Color.white);
            UiUtil.Place(icon, 14f, 14f, 24f, 24f);

            // 시안 잉크 (295.56,211.67) 59.52x15.89 → 18px(Headline3). 16px 로 두면 폭이 10% 짧다
            var label = UiUtil.MakeText(menu.transform, "Label", DesignTokens.Type.Headline3,
                                        DesignTokens.Primitive.Status.Red, TextAlignmentOptions.MidlineLeft);
            label.text = "삭제하기";
            UiUtil.Place(label, 46.5f, 0f, 70f, 56f);

            int idx = index;
            var b = menu.gameObject.AddComponent<Button>();
            b.targetGraphic = menu;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => AskDelete(idx));
        }
    }
}
