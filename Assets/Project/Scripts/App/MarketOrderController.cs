using UnityEngine;
using UnityEngine.UI;

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
            Hook(btnPayConfirm, () => { Show(modalPay, false); Show(modalDone, true); });
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
