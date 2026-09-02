using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 주문 상세 조회 (시안 '주문 상세 조회.svg').
    // 단계 원 57² @ x44.5, 첫 y328.5, 피치 90 · 세로선 x73 (357 ~ 마지막 원 중심)
    // 제목 18 잉크 +8.62 · 시각 12 #D3D5DD 잉크 +39.4 · 아이콘 30² 원 중앙
    public class MarketOrderDetailController : MonoBehaviour
    {
        [System.Serializable]
        public class Step
        {
            public string title;
            public string time;
            public Sprite icon;
        }

        [SerializeField] RectTransform timeline;     // 카드 안 (44.5,328.5) 기준 컨테이너
        [SerializeField] TMP_Text fontSource;
        // ⚠ ic_step_ring_* 는 **원+테두리+아이콘이 통째로 구워진 배지**라 단계별로 못 쓴다
        //   (셋 다 '결제 완료' 글리프다). 원과 테두리를 따로 그리고 단계 글리프만 얹는다.
        [SerializeField] Sprite circleSprite;        // circle.svg — 원 채움
        [SerializeField] Sprite circleStrokeSprite;  // circle_stroke.svg — 원 테두리
        [SerializeField] int currentStep = 4;        // 0-based, 이 단계까지 완료
        [SerializeField] float pitch = 90f;
        [SerializeField] float ring = 57f;
        [SerializeField] List<Step> steps = new List<Step>();

        [Header("구매 확정")]
        [SerializeField] GameObject modalConfirm;
        [SerializeField] Button btnConfirmOpen;
        [SerializeField] Button btnConfirmCancel;
        [SerializeField] Button btnConfirmYes;
        [SerializeField] string doneScreen = "MarketPurchaseDone";

        // 2026-08-24: 목 타임라인 → 서버(GET …/trades/{id})로 대체.
        // 내 거래 내역 목록은 서버 목록 API 가 없어(백엔드 미구현) 그대로다 →
        // 어느 거래를 보여줄지는 이 값이 정한다.
        // 2026-08-25 시드 개편: 거래 2·3 삭제, 거래 1 = DELIVERED(구매 확정 가능) → 1 로.
        [SerializeField] int tradeId = 1;
        [Header("주문 정보 라벨 (서버 값으로 채움 — 비워두면 안 건드린다)")]
        [SerializeField] TMP_Text labelProduct;    // cube.productName
        [SerializeField] TMP_Text labelPrice;      // price ("42,000원")

        // 구매 확정이 돌려주는 블록체인 증명서. 증명서 화면(MarketCertController)이 읽는다.
        public static TradeCertificateDto LastCertificate;

        IMarketSource src;

        static readonly Color Sub  = new Color32(0xD3, 0xD5, 0xDD, 0xFF);
        static readonly Color Off  = new Color32(0x72, 0x78, 0x86, 0xFF);
        static readonly Color LineOn  = new Color32(0x7C, 0x3A, 0xED, 0xFF);
        static readonly Color LineOff = new Color32(0x24, 0x28, 0x31, 0xFF);
        static readonly Color RingDone = new Color32(0x12, 0x0D, 0x22, 0xFF);
        static readonly Color RingNow  = new Color32(0x3A, 0x2B, 0x67, 0xFF);
        static readonly Color RingTodo = new Color32(0x24, 0x28, 0x31, 0xFF);
        static readonly Color RingOn   = new Color32(0xA8, 0x55, 0xF7, 0xFF);
        static readonly Color RingOff  = new Color32(0x72, 0x78, 0x86, 0xFF);

        void OnEnable()
        {
            if (btnConfirmOpen != null) btnConfirmOpen.onClick.AddListener(OpenConfirm);
            if (btnConfirmCancel != null) btnConfirmCancel.onClick.AddListener(CloseConfirm);
            if (btnConfirmYes != null) btnConfirmYes.onClick.AddListener(ConfirmYes);
            CloseConfirm();
            Build();                              // 먼저 씬 값으로 그린다 — 서버가 늦어도 화면이 비지 않게

            if (src == null) src = new ApiMarketSource();
            src.LoadTrade(tradeId, Apply, err =>
            {
                // 실패하면 씬(목) 값이 그대로 남는다. 로그에만 진실을 남긴다.
                Debug.LogWarning($"[Market] 거래 {tradeId} 조회 실패: {err} (씬 값 유지)");
            });
        }

        // 서버 타임라인으로 목값을 대체한다.
        // ★ Step.icon(배지 스프라이트)은 서버가 안 주므로 씬 것을 유지하고,
        //   글자(title)·시각(time)·진행 단계(currentStep)만 갈아끼운다.
        void Apply(TradeDto trade)
        {
            if (trade == null || trade.timeline == null) return;

            int n = Mathf.Min(steps.Count, trade.timeline.Length);
            if (steps.Count != trade.timeline.Length)
                Debug.LogWarning($"[Market] 씬 단계 {steps.Count}개 vs 서버 {trade.timeline.Length}개 — 앞 {n}개만 반영");

            int firstIncomplete = trade.timeline.Length;
            for (int i = 0; i < n; i++)
            {
                var t = trade.timeline[i];
                steps[i].title = t.label;                       // 서버가 한국어 라벨을 준다
                steps[i].time = FmtTime(t.time);
                if (!t.completed && firstIncomplete > i) firstIncomplete = i;
            }
            currentStep = firstIncomplete;                      // 이 단계까지 완료(0-based)

            if (labelProduct != null && trade.cube != null) labelProduct.text = trade.cube.productName;
            if (labelPrice != null) labelPrice.text = $"{trade.price:N0}원";
            Build();
        }

        // "2026-08-07T15:00:00" → "2026.08.07 15:00" (씬 목값과 같은 표기)
        static string FmtTime(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "";
            return System.DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
                                            System.Globalization.DateTimeStyles.None, out var t)
                ? t.ToString("yyyy.MM.dd HH:mm") : "";
        }

        void OnDisable()
        {
            if (btnConfirmOpen != null) btnConfirmOpen.onClick.RemoveListener(OpenConfirm);
            if (btnConfirmCancel != null) btnConfirmCancel.onClick.RemoveListener(CloseConfirm);
            if (btnConfirmYes != null) btnConfirmYes.onClick.RemoveListener(ConfirmYes);
        }

        void OpenConfirm()
        {
            if (modalConfirm == null) return;
            modalConfirm.SetActive(true);
            modalConfirm.transform.SetAsLastSibling();
        }

        void CloseConfirm()
        {
            if (modalConfirm != null) modalConfirm.SetActive(false);
        }

        bool confirming;   // 응답 전 재클릭 방지

        // 2026-08-28 유저 지시: 성공해야만 완료 화면으로 간다.
        // (예전엔 실패해도 진행했는데, 그러면 LastCertificate 가 이전 거래 값인 채
        //  증명서 화면이 읽어서 남의 증명서가 뜬다)
        void ConfirmYes()
        {
            if (confirming) return;
            CloseConfirm();
            if (src == null) src = new ApiMarketSource();
            confirming = true;
            src.CompleteTrade(tradeId,
                cert =>
                {
                    confirming = false;
                    LastCertificate = cert;     // 증명서 화면이 이 값을 읽는다
                    if (ScreenManager.Instance != null && !string.IsNullOrEmpty(doneScreen))
                        ScreenManager.Instance.Show(doneScreen);
                },
                err =>
                {
                    confirming = false;
                    Toast.Show(err);
                    Debug.LogWarning($"[Market] 구매 확정 실패: {err}");
                });
        }

        void Build()
        {
            if (timeline == null) { Debug.LogError("[OrderDetail] timeline 이 비어 있다"); return; }
            UiUtil.ClearChildren(timeline);
            if (steps == null || steps.Count == 0) return;

            float half = ring * 0.5f;
            float lineX = half - 0.5f;                       // 원 중심
            float total = (steps.Count - 1) * pitch;
            var bg = UiUtil.MakeImage(timeline, "LineBg", null, LineOff);
            UiUtil.Place(bg, lineX, half, 1f, total);
            if (currentStep > 0)
            {
                var on = UiUtil.MakeImage(timeline, "LineOn", null, LineOn);
                UiUtil.Place(on, lineX, half, 1f, Mathf.Min(currentStep, steps.Count - 1) * pitch);
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                bool done = i < currentStep;
                bool now = i == currentStep;
                var row = UiUtil.MakeNode(timeline, "Step" + i);
                UiUtil.Place(row, 0f, i * pitch, 330f, ring);

                // 유저가 준 '거래 진행도' 배지는 **원+테두리+글리프가 상태별로 통째로** 구워져 있다
                // (174² 내용 100%). 따로 원을 그리거나 글리프를 얹으면 겹친다 — 한 장만 그린다.
                if (s.icon != null)
                {
                    var badge = UiUtil.MakeImage(row, "Badge", s.icon, Color.white);
                    UiUtil.Place(badge, 0f, 0f, ring, ring);
                }

                var t = UiUtil.MakeText(row.transform, "Title", DesignTokens.Type.Body4,
                                        done || now ? DesignTokens.Text.Primary : Off,
                                        TextAlignmentOptions.MidlineLeft);
                t.fontSize = 18f; t.text = s.title;
                UiUtil.Place(t, 86f, 4.6f, 240f, 29f);
                Copy(t);

                if (!string.IsNullOrEmpty(s.time))
                {
                    var tm = UiUtil.MakeText(row.transform, "Time", DesignTokens.Type.Body4, Sub,
                                             TextAlignmentOptions.MidlineLeft);
                    tm.fontSize = 12f; tm.text = s.time;
                    UiUtil.Place(tm, 86f, 35.2f, 240f, 19f);
                    Copy(tm);
                }
            }
            timeline.sizeDelta = new Vector2(timeline.sizeDelta.x, total + ring);
        }

        void Copy(TMP_Text t)
        {
            if (fontSource == null || fontSource.font == null) return;
            t.font = fontSource.font;
            t.fontSharedMaterial = fontSource.fontSharedMaterial;
        }
    }
}
