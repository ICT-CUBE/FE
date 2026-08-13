using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 큐브 목록 (시안 '큐브 상세.svg').
    // 카드 실측(썸네일 top-left 기준): 썸네일 177×177 · 하트 +(143.17,144.82)
    //   판매자 12 Regular #3DD9A3 +(20.26,184.75) · 제목 14 Regular +(0.59,205.03)
    //   상태 12 Medium #727886 +(0.47,229.75) · 가격 16 Medium +(0.28,253.76)
    // 열 x 0·194 (화면 20.5·214.5), 행 피치 302
    public class MarketListController : MonoBehaviour
    {
        [System.Serializable]
        public class Item
        {
            public string seller;     // uddystar_142
            public string title;      // RUBY CUBE #001
            public string state;      // 미개봉 새상품
            public string price;      // 12,500원
            public Sprite thumb;
        }

        [SerializeField] RectTransform grid;
        [SerializeField] TMP_Text fontSource;
        [SerializeField] Sprite thumbSprite;      // bg_round12 (썸네일 마스크 — 꽉 찬 라운드 사각)
        // ⚠ 테두리는 **stroke 스프라이트**여야 한다. 꽉 찬 bg_round12 를 쓰면 사진을 통째로 덮는다
        //   (마스크·사진보다 뒤에 그려지므로 사진만 안 보이고 하트·글자는 보인다).
        [SerializeField] Sprite thumbStrokeSprite; // bg_round12_stroke
        [SerializeField] Sprite heartSprite;      // ic_heart
        [SerializeField] Sprite verifiedSprite;   // ic_verified — 판매자 앞 초록 방패체크
        [SerializeField] string detailScreen = "MarketCubeDetail";

        [SerializeField] float cardW = 177f;
        [SerializeField] float gapX = 17f;
        [SerializeField] float pitchY = 302f;

        [SerializeField] List<Item> items = new List<Item>();

        static readonly Color Seller = new Color32(0x3D, 0xD9, 0xA3, 0xFF);
        static readonly Color State  = new Color32(0x72, 0x78, 0x86, 0xFF);
        static readonly Color Line   = new Color32(0x18, 0x1A, 0x20, 0xFF);
        static readonly Color HeartOn = new Color32(0xA8, 0x55, 0xF7, 0xFF);   // 눌린 하트

        // OnEnable 마다 다시 그리므로 좋아요 상태는 여기 남겨 둔다.
        // ⚠ 정렬하면 인덱스가 바뀌므로 **Item 참조**로 들고 있어야 한다.
        readonly HashSet<Item> liked = new HashSet<Item>();
        readonly List<Item> view = new List<Item>();

        void OnEnable() => Build();

        // 화면 라벨이 '좋아요 순' 이다 — 좋아요 누른 것이 위로 올라온다(같은 순위면 원래 순서 유지).
        void SortView()
        {
            view.Clear();
            if (items == null) return;
            foreach (var it in items) if (liked.Contains(it)) view.Add(it);
            foreach (var it in items) if (!liked.Contains(it)) view.Add(it);
        }

        void Build()
        {
            if (grid == null) { Debug.LogError("[MarketList] grid 가 비어 있다"); return; }
            UiUtil.ClearChildren(grid);
            if (items == null || items.Count == 0) return;
            SortView();

            for (int i = 0; i < view.Count; i++)
            {
                var it = view[i];
                int col = i % 2, row = i / 2;
                var card = UiUtil.MakeNode(grid, "Item" + i);
                UiUtil.Place(card, col * (cardW + gapX), row * pitchY, cardW, 271f);

                // 카드 전체를 누를 수 있게 하는 투명 판.
                // 원래는 썸네일(Thumb)에 Button 을 붙였는데 그 Image 는 raycastTarget 이 꺼져 있어서
                // (UiUtil.MakeImage 의 raycast 기본값 false) 눌러도 아무 반응이 없었다.
                // 맨 앞(=맨 뒤에 그려짐)에 두면 위의 그림·글씨는 전부 raycastTarget 이 꺼져 있어 그대로 통과하고,
                // 하트만 뒤에 만들어져 자기 자리에서 탭을 먼저 가져간다.
                var hit = UiUtil.MakeImage(card, "Hit", null, new Color(0f, 0f, 0f, 0f),
                                           Image.Type.Simple, true);
                var hrt = (RectTransform)hit.transform;
                hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
                hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;

                // 썸네일 — 라운드에 맞춰 자른다
                var mask = UiUtil.MakeImage(card, "Thumb", thumbSprite, Color.white, Image.Type.Sliced);
                UiUtil.Place(mask, 0f, 0f, cardW, cardW);
                var mk = mask.gameObject.AddComponent<Mask>();
                mk.showMaskGraphic = false;
                if (it.thumb != null)
                {
                    var img = UiUtil.MakeImage(mask.transform, "Img", it.thumb, Color.white);
                    var irt = (RectTransform)img.transform;
                    irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
                    irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                }
                if (thumbStrokeSprite != null)
                {
                    var line = UiUtil.MakeImage(card, "ThumbLine", thumbStrokeSprite, Line, Image.Type.Sliced);
                    UiUtil.Place(line, 0f, 0f, cardW, cardW);
                }

                if (heartSprite != null)
                {
                    // 누르면 보라 하트로 (유저 지시). 썸네일 버튼보다 뒤에 만들어야 탭을 먼저 받는다.
                    var h = UiUtil.MakeImage(card, "Heart", heartSprite, Color.white, Image.Type.Simple, true);
                    UiUtil.Place(h, 143.17f, 144.82f, 16.67f, 16.67f);
                    var self = it;                                // 클로저 캡처 주의
                    h.color = liked.Contains(self) ? HeartOn : Color.white;
                    var hb = h.gameObject.AddComponent<Button>();
                    hb.targetGraphic = h;
                    hb.transition = Selectable.Transition.None;
                    hb.onClick.AddListener(() =>
                    {
                        if (!liked.Add(self)) liked.Remove(self);
                        Build();          // '좋아요 순' 이라 누르는 즉시 다시 정렬한다
                    });
                }

                if (verifiedSprite != null)
                {
                    var v = UiUtil.MakeImage(card, "Verified", verifiedSprite, Color.white);
                    // 시안 획 중심선 (22.67,359.83) 10.67×13.34 + stroke 1.5 → 겉 (21.92,359.08) 12.17×14.84.
                    // ic_verified.png 는 48² 중 내용 36×44(75.00%×91.67%) 라 박스는 16.2².
                    UiUtil.Place(v, -0.61f, 185.91f, 16.2f, 16.2f);
                }
                var se = Label(card, "Seller", it.seller, 12f, Seller, 20.26f, 184.75f, cardW - 24f, 19f);
                var ti = Label(card, "Title",  it.title,  14f, DesignTokens.Text.Primary, 0.59f, 205.03f, cardW, 23f);
                var st = Label(card, "State",  it.state,  12f, State, 0.47f, 229.75f, cardW, 19f);
                var pr = Label(card, "Price",  it.price,  16f, DesignTokens.Text.Primary, 0.28f, 253.76f, cardW, 26f);

                var btn = hit.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(Go);
            }

            int rows = Mathf.CeilToInt(view.Count / 2f);
            grid.sizeDelta = new Vector2(grid.sizeDelta.x, Mathf.Max(0f, rows * pitchY - (pitchY - 271f)));
        }

        TMP_Text Label(RectTransform parent, string name, string text, float size, Color col,
                       float x, float y, float w, float h)
        {
            var t = UiUtil.MakeText(parent.transform, name, DesignTokens.Type.Body4, col,
                                    TextAlignmentOptions.MidlineLeft);
            t.fontSize = size;
            t.text = text;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            UiUtil.Place(t, x, y, w, h);
            if (fontSource != null && fontSource.font != null)
            {
                t.font = fontSource.font;
                t.fontSharedMaterial = fontSource.fontSharedMaterial;
            }
            return t;
        }

        void Go()
        {
            if (ScreenManager.Instance != null && !string.IsNullOrEmpty(detailScreen))
                ScreenManager.Instance.Show(detailScreen);
        }
    }
}
