using System;

namespace ARZone
{
    // 마켓 거래의 유일한 접점. 서버에 있는 3개만 다룬다(대전제 — 있는 것만 붙인다).
    //   GET   …/trades/{id}           주문 상세(타임라인)
    //   PATCH …/trades/{id}/complete  구매 확정 → 블록체인 증명서
    //   POST  …/trades                거래 생성 (204, 본문 없음)
    public interface IMarketSource
    {
        void LoadTrade(int tradeId, Action<TradeDto> done, Action<string> fail);
        void CompleteTrade(int tradeId, Action<TradeCertificateDto> done, Action<string> fail);
        void CreateTrade(TradeCreateReq req, Action done, Action<string> fail);
    }
}
