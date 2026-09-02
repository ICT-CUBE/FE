using System;

namespace ARZone
{
    // 마켓(market) 도메인 응답. 서버 JSON 모양 그대로.

    [Serializable]
    public class TradeCubeDto
    {
        public int id;
        public string serialNumber;
        public string productName;
        public string artistName;
    }

    [Serializable]
    public class TradeTimelineDto
    {
        public string status;     // PAID | SENT | SHIPPING | DELIVERED | COMPLETED
        public string label;      // "결제 완료" 등 — 서버가 한국어로 준다
        public string time;       // null 가능 (미완료 단계)
        public bool completed;
    }

    [Serializable]
    public class TradeDto
    {
        public int id;
        public string paidTime;           // null 가능
        public int price;
        public string productImageUrl;    // null 가능
        public TradeCubeDto cube;
        public TradeTimelineDto[] timeline;
    }

    [Serializable]
    public class TradeDetailDto
    {
        public TradeDto trade;
    }

    [Serializable]
    public class TradeCertificateDto
    {
        public int id;
        public int tradeId;
        public string ownerNickname;      // null 가능
        public string completedTime;
        public string network;
        public string tokenStandard;
        public string tokenId;
        public string contractAddress;
        public string txHash;
        public string metadataUrl;
    }

    [Serializable]
    public class TradeCompleteDto
    {
        public TradeCertificateDto certificate;
    }

    // POST …/trades 요청 바디 (204 로 응답 본문이 없다)
    [Serializable]
    public class TradeCreateReq
    {
        public int sellListingId;
        public string buyerAddress;
        public string buyerPhoneNumber;
        public int totalPrice;
        public string paidTime;
    }
}
