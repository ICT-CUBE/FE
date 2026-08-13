using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // '사진 선택' 바텀시트 (시안 '사진 첨부.png' — 4-67/68 로 개정됐다).
    //
    // 시안 실측 (412×893 기준, PNG 1236 → ÷3)
    //   시트 top 171 · 핸들 (184,179) 44×4
    //   뒤로 잉크 (27,213.3) · 제목 '사진 선택' 18 SemiBold 가운데 잉크 y214
    //   '전송' 16px #727886 잉크 (364.3,215.3)
    //   그리드 3열 **120×120**, x 20·146·272 (가로 간격 6), 첫 행 y269 (세로 피치 128 = 간격 8)
    //   체크 원 **19×19** at 카드 +(10.5,10.5) — 흰 채움 + #727886 테두리 (4-70 SVG 실측)
    //   ※ 하단 전송 버튼은 없어졌다 — 헤더 우측 '전송' 이 그 역할.
    //
    // 사진은 AR 캡처(CaptureService)를 쓴다. 캡처가 없으면 시안처럼 '사진' 플레이스홀더를 깐다.
    public class ImagePickerSheet : MonoBehaviour
    {
        [Header("구성")]
        [SerializeField] GameObject sheetRoot;          // 비우면 자기 자신
        [SerializeField] RectTransform content;         // 스크롤 Content
        [SerializeField] Button btnCancel;
        [SerializeField] Button btnSend;
        [SerializeField] TMP_Text labelSend;      // 헤더 '전송' — 고른 게 있으면 보라로 켠다

        [Header("데이터")]
        [SerializeField] CaptureService captureSource;
        [SerializeField] TalkDetailController talk;
        [SerializeField] int placeholderCount = 15;     // 캡처가 없을 때 보여줄 빈 카드 수 (3열 × 5줄)

        [Header("스프라이트")]
        [SerializeField] Sprite cardSprite;             // bg_round12
        [SerializeField] Sprite cardStrokeSprite;       // bg_round12_stroke
        [SerializeField] Sprite circleSprite;           // circle
        [SerializeField] Sprite circleStrokeSprite;     // circle_stroke

        [Header("배치 (시안)")]
        [SerializeField] int columns = 3;
        [SerializeField] float cardSize = 120f;
        [SerializeField] float gapX = 6f;
        [SerializeField] float gapY = 8f;
        [SerializeField] float checkSize = 19f;
        [SerializeField] float checkInset = 10.5f;
        // 카드를 뷰포트 경계에 딱 붙이면 선택 테두리(#A855F7)가 Mask 에 잘린다.
        // 스크롤을 이만큼 넓혀 두고 카드는 안쪽으로 밀어 넣는다(화면상 위치는 시안 그대로).
        [SerializeField] float pad = 3f;

        // 개정 시안은 어두운 시트(#0F1115) 다. 사진이 없을 때의 카드 색은 시안에 없어 Bg.Surface 를 쓴다.
        static readonly Color CardBg      = new Color32(0x18, 0x1A, 0x20, 0xFF);
        static readonly Color CardLine    = new Color32(0x24, 0x28, 0x31, 0xFF);
        static readonly Color CardBgSel   = new Color32(0x24, 0x28, 0x31, 0xFF);
        static readonly Color CardLineSel = new Color32(0xA8, 0x55, 0xF7, 0xFF);
        static readonly Color CheckLine   = new Color32(0x72, 0x78, 0x86, 0xFF);   // 미선택 원 테두리
        static readonly Color PhotoText   = new Color32(0xD3, 0xD5, 0xDD, 0xFF);
        static readonly Color SendOff     = new Color32(0x72, 0x78, 0x86, 0xFF);   // 시안 기본
        static readonly Color SendOn      = new Color32(0xA8, 0x55, 0xF7, 0xFF);   // 고른 게 있을 때

        class Card
        {
            public CaptureService.Entry entry;
            public Image bg, line, check, checkRing, checkRingLine;
            public TMP_Text number;
        }

        readonly List<Card> cards = new List<Card>();
        readonly List<int> picked = new List<int>();     // 선택 순서 (시안의 원 안 숫자)

        GameObject Root => sheetRoot != null ? sheetRoot : gameObject;

        void OnEnable()
        {
            picked.Clear();
            Build();
            Refresh();
        }

        void Awake()
        {
            if (btnCancel != null) btnCancel.onClick.AddListener(Close);
            if (btnSend != null) btnSend.onClick.AddListener(Send);
        }

        public void Close() => Root.SetActive(false);

        void Build()
        {
            if (content == null) { Debug.LogError("[ImagePicker] content 가 비어 있다"); return; }
            UiUtil.ClearChildren(content);
            cards.Clear();

            var list = captureSource != null ? captureSource.Captures : null;
            int n = (list != null && list.Count > 0) ? list.Count : placeholderCount;

            float pitchX = cardSize + gapX, pitchY = cardSize + gapY;
            for (int i = 0; i < n; i++)
            {
                var e = (list != null && i < list.Count) ? list[i] : null;
                int col = i % columns, row = i / columns;

                var cardGo = UiUtil.MakeNode(content, "Card" + i);
                UiUtil.Place(cardGo, pad + col * pitchX, pad + row * pitchY, cardSize, cardSize);

                var bg = UiUtil.MakeImage(cardGo, "Bg", cardSprite, CardBg, Image.Type.Sliced, true);
                Stretch(bg.transform);

                // 사진 — 말풍선처럼 카드 라운드에 맞춰 잘라낸다
                if (e != null && e.Texture != null)
                {
                    var maskImg = UiUtil.MakeImage(cardGo, "Mask", cardSprite, Color.white, Image.Type.Sliced);
                    Stretch(maskImg.transform);
                    var mask = maskImg.gameObject.AddComponent<Mask>();
                    mask.showMaskGraphic = false;

                    var thumb = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage));
                    thumb.transform.SetParent(maskImg.transform, false);
                    Stretch(thumb.transform);
                    var raw = thumb.GetComponent<RawImage>();
                    raw.raycastTarget = false;
                    UiUtil.FitCover(raw, e.Texture);
                }
                else
                {
                    var ph = UiUtil.MakeText(cardGo.transform, "Photo", DesignTokens.Type.Body2,
                                             PhotoText, TextAlignmentOptions.Center);
                    ph.text = "사진";
                    Stretch(ph.transform);
                }

                var line = UiUtil.MakeImage(cardGo, "Line", cardStrokeSprite, CardLine, Image.Type.Sliced);
                Stretch(line.transform);

                // 체크 — 미선택은 흰 원 + 회색 테두리, 선택은 보라 원 + 순번 (시안)
                var ring = UiUtil.MakeImage(cardGo, "CheckRing", circleSprite, Color.white);
                UiUtil.Place(ring, checkInset, checkInset, checkSize, checkSize);
                var ringLine = UiUtil.MakeImage(cardGo, "CheckRingLine", circleStrokeSprite, CheckLine);
                UiUtil.Place(ringLine, checkInset, checkInset, checkSize, checkSize);
                var chk = UiUtil.MakeImage(cardGo, "Check", circleSprite, CardLineSel);
                UiUtil.Place(chk, checkInset, checkInset, checkSize, checkSize);
                var num = UiUtil.MakeText(chk.transform, "No", DesignTokens.Type.Body2,
                                          Color.white, TextAlignmentOptions.Center);
                num.fontSize = 12f;                       // 시안 숫자 잉크 높이 8.48 → 12px
                Stretch(num.transform);

                int k = i;
                var hit = cardGo.gameObject.AddComponent<Button>();
                hit.targetGraphic = bg;
                hit.transition = Selectable.Transition.None;
                hit.onClick.AddListener(() => Toggle(k));

                cards.Add(new Card { entry = e, bg = bg, line = line, check = chk,
                                     checkRing = ring, checkRingLine = ringLine, number = num });
            }

            int rows = Mathf.CeilToInt(n / (float)columns);
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(0f, pad * 2f + rows * pitchY - gapY));
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }

        static void Stretch(Transform t)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        void Toggle(int i)
        {
            if (picked.Contains(i)) picked.Remove(i);
            else picked.Add(i);
            Refresh();
        }

        void Refresh()
        {
            if (labelSend != null) labelSend.color = picked.Count > 0 ? SendOn : SendOff;

            for (int i = 0; i < cards.Count; i++)
            {
                int order = picked.IndexOf(i);
                bool on = order >= 0;
                var c = cards[i];
                if (c.bg != null)   c.bg.color = on ? CardBgSel : CardBg;
                if (c.line != null) c.line.color = on ? CardLineSel : CardLine;
                if (c.check != null)     c.check.gameObject.SetActive(on);
                if (c.checkRing != null)     c.checkRing.gameObject.SetActive(!on);
                if (c.checkRingLine != null) c.checkRingLine.gameObject.SetActive(!on);
                if (c.number != null)    c.number.text = on ? (order + 1).ToString() : "";
            }
        }

        void Send()
        {
            if (picked.Count == 0) return;

            var textures = new List<Texture2D>();
            foreach (int i in picked)
                if (i < cards.Count && cards[i].entry != null && cards[i].entry.Texture != null)
                    textures.Add(cards[i].entry.Texture);

            if (talk != null && textures.Count > 0) talk.SendImages(textures);
            else if (textures.Count == 0)
                Debug.Log("[ImagePicker] 고른 카드에 사진이 없다 — 캡처가 아직 없는 플레이스홀더");

            Close();
        }
    }
}
