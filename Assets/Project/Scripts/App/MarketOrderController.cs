using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 주문 확인 → 결제 모달 → 결제 완료 모달 (시안 '주문 확인' / '-1' / '-2').
    public class MarketOrderController : MonoBehaviour
    {
        [Header("결제 모달")]
        [SerializeField] GameObject modalPay;
        [SerializeField] Button btnPayOpen;        // 안전 에스크로로 결제 진행
        [SerializeField] Button btnPayCancel;      // 취소
        [SerializeField] Button btnPayConfirm;     // 결제하기

        [Header("거래 생성 (2026-08-28 배선)")]
        // 어느 판매 상품을 사는지는 판매 상품 목록 API 가 없어 시드 값으로 고정한다(문의 7·12).
        [SerializeField] int sellListingId = 1;
        [SerializeField] TMP_Text orderPhone;      // 주문 확인 화면 연락처 라벨 (배송지 시트가 갱신)
        [SerializeField] TMP_Text orderAddr;       // 주소 라벨
        [SerializeField] TMP_Text labelTotal;      // 총 결제 금액 "12,740원" — 숫자만 떼어 보낸다

        [Header("완료 모달")]
        [SerializeField] GameObject modalDone;
        [SerializeField] Button btnKeepShopping;   // 쇼핑 계속하기
        [SerializeField] Button btnGoMyTrade;      // 내 거래로 이동
        [SerializeField] string shopScreen = "Market";
        [SerializeField] string myTradeScreen = "MarketMyTrades";

        void OnEnable()
        {
            Hook(btnPayOpen, () => Show(modalPay, true));
            Hook(btnPayCancel, () => Show(modalPay, false));
            Hook(btnPayConfirm, PayConfirm);
            Hook(btnKeepShopping, () => { Show(modalDone, false); Go(shopScreen); });
            Hook(btnGoMyTrade, () => { Show(modalDone, false); Go(myTradeScreen); });
            Show(modalPay, false);
            Show(modalDone, false);
        }

        void OnDisable()
        {
            Unhook(btnPayOpen); Unhook(btnPayCancel); Unhook(btnPayConfirm);
            Unhook(btnKeepShopping); Unhook(btnGoMyTrade);
        }

        IMarketSource src;
        bool paying;   // 응답 전 재클릭 방지

        // 결제하기 — 거래 생성(POST /trades)이 성공해야 완료 모달을 띄운다 (2026-08-28 유저 지시).
        // ⚠ 지금 서버 시드엔 거래 안 걸린 판매 상품이 없어 409 가 난다(문의 7·12) — 그때는 토스트만.
        void PayConfirm()
        {
            if (paying) return;
            paying = true;
            if (src == null) src = new ApiMarketSource();
            src.CreateTrade(new TradeCreateReq
            {
                sellListingId = sellListingId,
                buyerAddress = orderAddr != null ? orderAddr.text : "",
                buyerPhoneNumber = orderPhone != null ? orderPhone.text : "",
                totalPrice = ParseWon(labelTotal),
                paidTime = ApiConfig.Iso(System.DateTime.Now),
            },
            () => { paying = false; Show(modalPay, false); Show(modalDone, true); },
            err =>
            {
                paying = false;
                Toast.Show(err);
                Debug.LogWarning($"[MarketOrder] 거래 생성 실패: {err}");
            });
        }

        // "12,740원" → 12740. 숫자가 하나도 없으면 0.
        static int ParseWon(TMP_Text t)
        {
            if (t == null || string.IsNullOrEmpty(t.text)) return 0;
            int v = 0;
            foreach (var c in t.text)
                if (c >= '0' && c <= '9') v = v * 10 + (c - '0');
            return v;
        }

        static void Show(GameObject go, bool on)
        {
            if (go == null) return;
            if (go.activeSelf != on) go.SetActive(on);
            if (on) go.transform.SetAsLastSibling();
        }

        void Go(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(id);
            else Debug.LogError("[MarketOrder] ScreenManager 가 씬에 없음");
        }

        void Hook(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null) b.onClick.AddListener(a);
        }

        static void Unhook(Button b)
        {
            if (b != null) b.onClick.RemoveAllListeners();
        }
    }
}
