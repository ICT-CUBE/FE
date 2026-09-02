using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 구매 확정 완료 (시안 '구매 확정' / '구매 확정 펼침').
    // 블록체인 증명 정보 바를 누르면 아래 패널이 펼쳐진다.
    // 펼침 행 실측: 라벨 Regular 13 #727886 · 값 Medium 16 흰색 우측정렬 (둘이 같은 베이스라인).
    public class MarketCertController : MonoBehaviour
    {
        [System.Serializable]
        public class Row
        {
            public string label;
            public string value;
            public bool copy;      // 값 오른쪽 복사 아이콘 (시안 '컨트랙트' 행)
            public string chip;    // 값 오른쪽 보라 칩 글자 (시안 'TxHash' 행 = Explore)
        }

        [SerializeField] Button btnChain;          // 블록체인 증명 정보 바
        [SerializeField] GameObject chainPanel;
        [SerializeField] RectTransform chainRows;
        [SerializeField] RectTransform arrow;      // 펼치면 뒤집는다
        [SerializeField] TMP_Text fontSource;
        [SerializeField] Button btnClose;
        [SerializeField] string closeScreen = "Market";

        // 펼치면 내용이 893 을 넘어서 스크롤이 필요하다.
        // 스크롤 뷰포트는 y 96 부터라 Content 높이 = (마지막 요소 아래) - 96 + 여백 20.
        //   접힘 — 바 아래끝 825 → 749   /   펼침 — 패널 아래끝 1101 → 1025
        [Header("펼침 스크롤")]
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform scrollContent;
        [SerializeField] float contentClosed = 749f;
        [SerializeField] float contentOpen = 1025f;
        [Header("행 (시안 실측)")]
        // 시안 '구매 확정 펼침.svg' 의 행 간격은 일정하지 않다 — 37.74·38.00·38.24·39.15·40.00.
        // 시안 라벨 베이스라인(패널 845 기준 882.04/919.78/957.78/996.02/1035.17/1075.17) 에서
        // BaselineIn(13, 19) = 14.118 을 뺀 값. TxHash·Metadata 는 디센더가 없어 잉크 아래끝 = 베이스라인이라 정확하다.
        [SerializeField]
        List<float> rowTops = new List<float> { 22.92f, 60.66f, 98.66f, 136.90f, 176.05f, 216.05f };
        [SerializeField] Sprite copyIcon;          // ic_copy (유저 제공 icon-8.png)
        [SerializeField] Sprite chipBg;            // bg_round8
        [SerializeField]
        List<Row> rows = new List<Row>
        {
            new Row { label = "네트워크",  value = "Polygon Mainnet" },
            new Row { label = "토큰 표준", value = "ERC-721" },
            new Row { label = "토큰 ID",  value = "#31" },
            new Row { label = "컨트랙트",  value = "0s3a82f9c41", copy = true },
            new Row { label = "TxHash",  value = "0s3a82f6f8b12", chip = "Explore" },
            new Row { label = "Metadata", value = "IPFS Encrypted" },
        };

        // 값 오른쪽 붙임물 (패널 좌상단 기준 x, 시안 실측)
        // y 는 패널 좌상단 기준이다 — 행 기준으로 두면 rowTops 를 손볼 때 같이 밀린다.
        const float CopyX = 335.90f, CopyY = 134.90f, CopySize = 20.19f;  // 잉크 18.17 / icon-8.png 내용비 90%
        const float ChipX = 296f, ChipY = 172f, ChipW = 60f, ChipH = 26f;
        const float ValueRight = 355f;                     // 붙임물이 없는 행의 값 오른쪽 끝
        const float Gap = 9.5f;                            // 값과 붙임물 사이

        const float LabelH = 19f, ValueH = 23f;
        // 라벨(13)과 값(16)은 시안에서 같은 베이스라인이다. 크기가 다르면 가운데 정렬만으로는 안 맞는다
        // → 값 박스를 이만큼 올린다. Pretendard SDF: pointSize 90 / ascent 85.6494 / descent -21.7090.
        static float ValueDy => BaselineIn(DesignTokens.Type.Caption1.Size, LabelH)
                              - BaselineIn(DesignTokens.Type.Body2.Size, ValueH);

        // TMP 가운데 정렬에서 박스 위끝부터 베이스라인까지의 거리.
        // 줄 상자의 중심이 박스 중심에 오므로 baseline = h/2 + (ascent-descent)/2 × size.
        static float BaselineIn(float size, float h)
        {
            const float Half = (85.649414f - 21.708984f) / 2f / 90f;   // 0.355225
            return h * 0.5f + Half * size;
        }

        static readonly Color Lab = new Color32(0x72, 0x78, 0x86, 0xFF);
        static readonly Color ChipFill = new Color32(0x7C, 0x3A, 0xED, 0xFF);
        static readonly Color ChipText = new Color32(0xDD, 0xD6, 0xFE, 0xFF);
        bool open;

        void OnEnable()
        {
            if (btnChain != null) btnChain.onClick.AddListener(Toggle);
            if (btnClose != null) btnClose.onClick.AddListener(Close);
            open = false;

            // 2026-08-24: 구매 확정(PATCH …/complete)이 돌려준 진짜 증명서가 있으면
            // 목값(Polygon Mainnet / #31 / 0s3a82… — 시안에서 베낀 것)을 그걸로 대체한다.
            // 라벨·복사 아이콘·Explore 칩 구성은 씬 그대로다. 확정을 안 거쳐 왔으면 목값이 남는다.
            var cert = MarketOrderDetailController.LastCertificate;
            if (cert != null && rows.Count >= 6)
            {
                rows[0].value = cert.network;
                rows[1].value = cert.tokenStandard;
                rows[2].value = cert.tokenId;
                rows[3].value = cert.contractAddress;
                rows[4].value = cert.txHash;
                rows[5].value = cert.metadataUrl;
                Debug.Log($"[Market] 증명서 표시 — trade {cert.tradeId}, {cert.network} {cert.tokenId}");
            }

            Build();
            Apply();
        }

        void OnDisable()
        {
            if (btnChain != null) btnChain.onClick.RemoveListener(Toggle);
            if (btnClose != null) btnClose.onClick.RemoveListener(Close);
        }

        void Build()
        {
            if (chainRows == null) return;
            UiUtil.ClearChildren(chainRows);
            // ※ rect.width 는 OnEnable 에 0 이라 sizeDelta 를 쓴다
            float w = chainRows.sizeDelta.x > 1f ? chainRows.sizeDelta.x : 372f;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                // rowTops 가 모자라면 마지막 간격(40)을 이어 쓴다
                float top = i < rowTops.Count ? rowTops[i]
                          : rowTops.Count > 0 ? rowTops[rowTops.Count - 1] + 40f * (i - rowTops.Count + 1)
                          : 22.92f + 40f * i;
                var row = UiUtil.MakeNode(chainRows, "Row" + i);
                UiUtil.Place(row, 0f, top, w, 26f);
                Label(row, "Label", r.label, DesignTokens.Type.Caption1, Lab, 17f, 0f, 160f, LabelH,
                      TextAlignmentOptions.MidlineLeft);

                // 값 오른쪽 끝은 붙임물이 있으면 그 앞에서 끊는다
                float right = ValueRight;
                if (r.copy) right = CopyX - Gap;
                else if (!string.IsNullOrEmpty(r.chip)) right = ChipX - Gap;
                Label(row, "Value", r.value, DesignTokens.Type.Body2, DesignTokens.Text.Primary,
                      180f, ValueDy, right - 180f, ValueH, TextAlignmentOptions.MidlineRight);

                if (r.copy) MakeCopy(row, r.value, top);
                else if (!string.IsNullOrEmpty(r.chip)) MakeChip(row, r.chip, top);
            }
        }

        // 시안 복사 아이콘: 잉크 18.17² 가 (356.92,980.92) → 박스 20.19² @ 패널기준 (335.90,134.90)
        void MakeCopy(RectTransform row, string value, float rowTop)
        {
            if (copyIcon == null) return;
            var img = UiUtil.MakeImage(row, "Copy", copyIcon, Color.white, Image.Type.Simple, true);
            UiUtil.Place(img, CopyX, CopyY - rowTop, CopySize, CopySize);
            // 아무 반응 없는 아이콘을 두지 않는다
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => GUIUtility.systemCopyBuffer = value);
        }

        // 시안 칩: (316,1017) 60×26 r8 #7C3AED · 글자 Regular 13 #DDD6FE 가운데
        void MakeChip(RectTransform row, string text, float rowTop)
        {
            var bg = UiUtil.MakeImage(row, "Chip", chipBg, ChipFill, Image.Type.Sliced, false);
            UiUtil.Place(bg, ChipX, ChipY - rowTop, ChipW, ChipH);
            Label((RectTransform)bg.transform, "ChipLabel", text, DesignTokens.Type.Caption1, ChipText,
                  0f, 0f, ChipW, ChipH, TextAlignmentOptions.Center);
        }

        void Toggle() { open = !open; Apply(); }

        void Apply()
        {
            if (chainPanel != null) chainPanel.SetActive(open);
            if (chainRows != null) chainRows.gameObject.SetActive(open);
            if (arrow != null) arrow.localRotation = Quaternion.Euler(0f, 0f, open ? 180f : 0f);

            if (scrollContent != null)
            {
                // ※ rect.height 는 OnEnable 에 0 이라 sizeDelta 로 넣는다
                var s = scrollContent.sizeDelta;
                scrollContent.sizeDelta = new Vector2(s.x, open ? contentOpen : contentClosed);

                // 접을 때 스크롤이 아래에 남아 있으면 빈 화면이 보인다.
                // verticalNormalizedPosition 은 레이아웃 전(OnEnable)에는 기준 rect 가 0 이라 못 믿는다 →
                // Content 가 좌상단 앵커/피벗이라 anchoredPosition 을 0 으로 두면 그게 맨 위다.
                if (!open) scrollContent.anchoredPosition = Vector2.zero;
            }
            if (!open && scroll != null) scroll.StopMovement();
        }

        void Close()
        {
            if (ScreenManager.Instance != null && !string.IsNullOrEmpty(closeScreen))
                ScreenManager.Instance.Show(closeScreen);
        }

        void Label(RectTransform p, string n, string s, TypeStyle style, Color c,
                   float x, float y, float w, float h, TextAlignmentOptions al)
        {
            var t = UiUtil.MakeText(p.transform, n, style, c, al);
            t.text = s;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UiUtil.Place(t, x, y, w, h);
            // TypeStyle 이 Resources 에서 굵기에 맞는 SDF 를 물려준다.
            // fontSource 로 덮으면 라벨(Regular)·값(Medium)·칩이 전부 한 굵기가 되므로 못 찾았을 때만 쓴다.
            if (t.font == null && fontSource != null && fontSource.font != null)
            {
                t.font = fontSource.font;
                t.fontSharedMaterial = fontSource.fontSharedMaterial;
            }
        }
    }
}
