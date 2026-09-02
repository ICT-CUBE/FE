using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 마켓 홈 (시안 '마켓.svg').
    // 카드 실측(카드 top-left 기준): 썸네일 +(19,12) 140×140 · 제목 14 Regular +162.71
    //                               부제 12 Medium #727886 +183.24 · 가격 14 Medium #C4B5FD +211.63
    // 카드 178×250 r16 #0F0D17, 열 x 0·194(화면 20·214), 행 피치 266.5
    public class MarketController : MonoBehaviour
    {
        [System.Serializable]
        public class Cube
        {
            public string title;      // RUBY CUBE
            public string sub;        // 제니 (JENNIE)
            public string price;      // 제공가 12,500원
            public Sprite thumb;
        }

        [Header("구성")]
        [SerializeField] RectTransform grid;
        [SerializeField] TMP_Text fontSource;
        [SerializeField] Sprite cardSprite;        // bg_round16
        [SerializeField] string detailScreen = "MarketCubeList";

        [Header("배치 (시안)")]
        [SerializeField] float cardW = 178f;
        [SerializeField] float cardH = 250f;
        [SerializeField] float gapX = 16f;
        [SerializeField] float pitchY = 266.5f;

        [Header("스크롤")]
        [SerializeField] RectTransform scrollContent;   // 카드가 늘어나면 여기 높이도 늘린다
        [SerializeField] float contentPad = 130f;       // 마지막 줄이 떠 있는 탭바 위로 올라올 만큼

        [Header("데이터 (API 오면 소스만 교체)")]
        [SerializeField] List<Cube> cubes = new List<Cube>();

        // 카드 유리 채움. 실제 색은 GlassGradient 가 정점 색으로 덮으므로 이 값은 그게 없을 때의 대비값이다.
        // (#111113 = 시안 유리 그라디언트의 중간 합성값. 자세한 건 GlassGradient.cs 주석)
        static readonly Color CardBg   = new Color32(0x11, 0x11, 0x13, 0xFF);
        static readonly Color SubText  = new Color32(0x72, 0x78, 0x86, 0xFF);
        static readonly Color PriceCol = new Color32(0xC4, 0xB5, 0xFD, 0xFF);

        void OnEnable() => Build();

        void Build()
        {
            if (grid == null) { Debug.LogError("[Market] grid 가 비어 있다"); return; }
            UiUtil.ClearChildren(grid);
            if (cubes == null || cubes.Count == 0) return;

            for (int i = 0; i < cubes.Count; i++)
            {
                var c = cubes[i];
                int col = i % 2, row = i / 2;
                var card = UiUtil.MakeNode(grid, "Cube" + i);
                UiUtil.Place(card, col * (cardW + gapX), row * pitchY, cardW, cardH);

                var bg = UiUtil.MakeImage(card, "Bg", cardSprite, CardBg, Image.Type.Sliced, true);
                GlassEdgeGlow.Attach(card.transform, GlassGlowBoot.Round16);   // 빛나는 부분 테두리
                var brt = (RectTransform)bg.transform;
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

                bg.gameObject.AddComponent<GlassGradient>();   // 시안 유리 = 좌상 밝고 우하 어두운 대각 그라디언트

                if (c.thumb != null)
                {
                    var th = UiUtil.MakeImage(card, "Thumb", c.thumb, Color.white);
                    UiUtil.Place(th, 19f, 12f, 140f, 140f);
                }

                var t = UiUtil.MakeText(card.transform, "Title", DesignTokens.Type.Body4,
                                        DesignTokens.Text.Primary, TextAlignmentOptions.MidlineLeft);
                t.fontSize = 14f; t.text = c.title;
                UiUtil.Place(t, 17f, 162.71f, cardW - 34f, 23f);
                CopyFont(t);

                var s = UiUtil.MakeText(card.transform, "Sub", DesignTokens.Type.Body4, SubText,
                                        TextAlignmentOptions.MidlineLeft);
                s.fontSize = 12f; s.text = c.sub;
                UiUtil.Place(s, 16f, 183.24f, cardW - 32f, 19f);
                CopyFont(s);

                var p = UiUtil.MakeText(card.transform, "Price", DesignTokens.Type.Body4, PriceCol,
                                        TextAlignmentOptions.MidlineLeft);
                p.fontSize = 14f; p.text = c.price;
                UiUtil.Place(p, 16f, 211.63f, cardW - 32f, 23f);
                CopyFont(p);

                var btn = bg.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(Go);
            }

            Debug.Log($"[Market] 카드 {cubes.Count}장 생성 (grid={grid.name})");
            int rows = Mathf.CeilToInt(cubes.Count / 2f);
            float gridH = Mathf.Max(0f, rows * pitchY - (pitchY - cardH));
            grid.sizeDelta = new Vector2(grid.sizeDelta.x, gridH);

            // 카드 수에 따라 스크롤 길이를 맞춘다. grid 는 Content 자식이라 그 y 만큼이 위 여백이다.
            if (scrollContent != null)
            {
                float top = -grid.anchoredPosition.y;
                var s2 = scrollContent.sizeDelta;
                scrollContent.sizeDelta = new Vector2(s2.x, top + gridH + contentPad);
            }
        }

        void Go()
        {
            // 유저가 "큐브 눌러도 상세로 안 간다" 고 함. 코드·씬 배선은 정상이라 실기 로그로 좁힌다.
            Debug.Log($"[Market] 카드 클릭 · detailScreen='{detailScreen}' · SM={(ScreenManager.Instance != null)}");
            if (ScreenManager.Instance != null && !string.IsNullOrEmpty(detailScreen))
                ScreenManager.Instance.Show(detailScreen);
        }

        void CopyFont(TMP_Text t)
        {
            if (fontSource == null || fontSource.font == null || t == null) return;
            t.font = fontSource.font;
            t.fontSharedMaterial = fontSource.fontSharedMaterial;
        }
    }
}
