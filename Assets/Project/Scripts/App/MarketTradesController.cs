using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 내 거래 내역 (시안 '내 거래 내역.svg').
    // 행(리스트 top = 138 기준): 칩 (140,0) h26 r8 · 썸네일 (36,3.5) 88² r8
    //   날짜 12 #D3D5DD +4.64 · 칩글자 13 +2.43 · 제목 14 +37.10 · 가격 18 +69.08 · 구분선 +119
    // 행 피치 143
    public class MarketTradesController : MonoBehaviour
    {
        [System.Serializable]
        public class Trade
        {
            public string state;      // 거래 진행 중 / 거래 완료 / 거래 취소
            public bool active;       // 진행 중이면 보라 칩
            public string date;       // 2026.08.06
            public string title;      // JENNIE RUBY CUBE #012
            public string price;      // 12,500원
            public Sprite thumb;
        }

        [SerializeField] RectTransform list;
        [SerializeField] TMP_Text fontSource;
        [SerializeField] Sprite chipSprite;      // bg_round8
        [SerializeField] Sprite thumbSprite;     // bg_round8
        [SerializeField] string detailScreen = "MarketOrderDetail";
        [SerializeField] float pitch = 143f;
        [SerializeField] List<Trade> trades = new List<Trade>();

        static readonly Color ChipOn   = new Color32(0x7C, 0x3A, 0xED, 0xFF);
        static readonly Color ChipOff  = new Color32(0x24, 0x28, 0x31, 0xFF);
        static readonly Color ChipTxtOn = new Color32(0xDD, 0xD6, 0xFE, 0xFF);
        static readonly Color Sub      = new Color32(0xD3, 0xD5, 0xDD, 0xFF);
        static readonly Color Divider  = new Color32(0x24, 0x28, 0x31, 0xFF);

        void OnEnable() => Build();

        void Build()
        {
            if (list == null) { Debug.LogError("[MarketTrades] list 가 비어 있다"); return; }
            UiUtil.ClearChildren(list);
            if (trades == null || trades.Count == 0) return;

            float w = list.sizeDelta.x > 1f ? list.sizeDelta.x : 412f;
            for (int i = 0; i < trades.Count; i++)
            {
                var tr = trades[i];
                var row = UiUtil.MakeNode(list, "Trade" + i);
                UiUtil.Place(row, 0f, i * pitch, w, pitch);

                // 행 전체를 누를 수 있게 하는 투명 판.
                // 원래는 썸네일에 Button 을 붙였는데 그 Image 는 raycastTarget 이 꺼져 있어
                // (UiUtil.MakeImage 의 raycast 기본값 false) 눌러도 아무 반응이 없었다.
                // 맨 앞 자식이라 뒤에 그려지고, 위의 그림·글씨는 raycastTarget 이 꺼져 있어 탭이 그대로 통과한다.
                var hit = UiUtil.MakeImage(row, "Hit", null, new Color(0f, 0f, 0f, 0f),
                                           Image.Type.Simple, true);
                var hrt = (RectTransform)hit.transform;
                hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
                hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;

                var th = UiUtil.MakeImage(row, "Thumb", thumbSprite, Color.white, Image.Type.Sliced);
                UiUtil.Place(th, 36f, 3.5f, 88f, 88f);
                var mk = th.gameObject.AddComponent<Mask>();
                mk.showMaskGraphic = false;
                if (tr.thumb != null)
                {
                    var img = UiUtil.MakeImage(th.transform, "Img", tr.thumb, Color.white);
                    var irt = (RectTransform)img.transform;
                    irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
                    irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                }

                var chipTxt = Label(row, "ChipText", tr.state, 13f,
                                    tr.active ? ChipTxtOn : Sub, 148.6f, 2.43f, 200f, 22f);
                chipTxt.ForceMeshUpdate();
                float cw = Mathf.Round(chipTxt.preferredWidth) + 17.5f;   // 시안: 잉크 61.74 → 칩 79
                var chip = UiUtil.MakeImage(row, "Chip", chipSprite,
                                            tr.active ? ChipOn : ChipOff, Image.Type.Sliced);
                UiUtil.Place(chip, 140f, 0f, cw, 26f);
                chip.transform.SetSiblingIndex(chipTxt.transform.GetSiblingIndex());
                UiUtil.Place(chipTxt, 148.6f, 2.43f, cw - 17f, 22f);

                Label(row, "Date",  tr.date,  12f, Sub, 232f, 4.64f, 143f, 19f).alignment = TextAlignmentOptions.MidlineRight;
                Label(row, "Title", tr.title, 14f, DesignTokens.Text.Primary, 140.57f, 37.1f, 240f, 23f);
                Label(row, "Price", tr.price, 18f, Sub, 140.84f, 69.08f, 240f, 29f);

                if (i < trades.Count - 1)
                {
                    var dv = UiUtil.MakeImage(row, "Divider", null, Divider);
                    UiUtil.Place(dv, 20f, 119f, w - 40f, 1f);
                }

                var btn = hit.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(Go);
            }
            list.sizeDelta = new Vector2(list.sizeDelta.x, trades.Count * pitch);
        }

        TMP_Text Label(RectTransform parent, string name, string text, float size, Color col,
                       float x, float y, float w, float h)
        {
            var t = UiUtil.MakeText(parent.transform, name, DesignTokens.Type.Body4, col,
                                    TextAlignmentOptions.MidlineLeft);
            t.fontSize = size; t.text = text;
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
