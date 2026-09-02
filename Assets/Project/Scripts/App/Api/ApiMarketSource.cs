using System;
using UnityEngine;

namespace ARZone
{
    // 서버로 도는 마켓 구현.
    public class ApiMarketSource : IMarketSource
    {
        const string Trades = "api/v1/users/me/trades";

        public void LoadTrade(int tradeId, Action<TradeDto> done, Action<string> fail)
        {
            if (tradeId <= 0) { fail?.Invoke("거래가 없어요"); return; }
            ApiClient.Get($"{Trades}/{tradeId}", json =>
            {
                try
                {
                    var dto = JsonUtility.FromJson<TradeDetailDto>(json);
                    if (dto == null || dto.trade == null) { fail?.Invoke("응답을 읽지 못했어요"); return; }
                    Debug.Log($"[Market] 거래 {dto.trade.id} 로드 — 타임라인 {dto.trade.timeline?.Length ?? 0}단계");
                    done?.Invoke(dto.trade);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Api] 거래 상세 파싱 실패: {e.Message}\n{json}");
                    fail?.Invoke("응답을 읽지 못했어요");
                }
            }, fail);
        }

        public void CompleteTrade(int tradeId, Action<TradeCertificateDto> done, Action<string> fail)
        {
            if (tradeId <= 0) { fail?.Invoke("거래가 없어요"); return; }
            ApiClient.Patch($"{Trades}/{tradeId}/complete", null, json =>
            {
                try
                {
                    var dto = JsonUtility.FromJson<TradeCompleteDto>(json);
                    if (dto == null || dto.certificate == null) { fail?.Invoke("응답을 읽지 못했어요"); return; }
                    Debug.Log($"[Market] 구매 확정 — 증명서 tokenId={dto.certificate.tokenId}");
                    done?.Invoke(dto.certificate);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Api] 구매 확정 파싱 실패: {e.Message}\n{json}");
                    fail?.Invoke("응답을 읽지 못했어요");
                }
            }, fail);
        }

        public void CreateTrade(TradeCreateReq req, Action done, Action<string> fail)
        {
            if (req == null) { fail?.Invoke("주문 정보가 없어요"); return; }
            // 204 No Content — 만들어진 거래의 id 를 돌려주지 않는다(서버 스펙).
            ApiClient.Post(Trades, JsonUtility.ToJson(req), _ => done?.Invoke(), fail);
        }
    }

    // 서버 없이 도는 구현. 실서버 시드(trade 2) 값을 그대로 흉내낸다.
    public class MockMarketSource : IMarketSource
    {
        public void LoadTrade(int tradeId, Action<TradeDto> done, Action<string> fail)
        {
            done?.Invoke(new TradeDto
            {
                id = tradeId, paidTime = "2026-08-07T14:35:00", price = 42000,
                cube = new TradeCubeDto { id = 2, serialNumber = "ARZ-CUBE-0002",
                                          productName = "arzone Cube B", artistName = "ARZONE" },
                timeline = new[]
                {
                    new TradeTimelineDto { status="PAID",      label="결제 완료",  time="2026-08-07T14:35:00", completed=true },
                    new TradeTimelineDto { status="SENT",      label="판매자 발송", time="2026-08-07T15:00:00", completed=true },
                    new TradeTimelineDto { status="SHIPPING",  label="배송 중",    time="2026-08-07T15:30:00", completed=true },
                    new TradeTimelineDto { status="DELIVERED", label="배송 완료",  time=null,                  completed=false },
                    new TradeTimelineDto { status="COMPLETED", label="구매 확정",  time=null,                  completed=false },
                },
            });
        }

        public void CompleteTrade(int tradeId, Action<TradeCertificateDto> done, Action<string> fail)
        {
            done?.Invoke(new TradeCertificateDto
            {
                id = 1, tradeId = tradeId, completedTime = "2026-08-24T12:00:00",
                network = "Polygon Mainnet", tokenStandard = "ERC-721", tokenId = "#31",
                contractAddress = "0s3a82f9c41", txHash = "0s3a82f6f8b12", metadataUrl = "IPFS Encrypted",
            });
        }

        public void CreateTrade(TradeCreateReq req, Action done, Action<string> fail) => done?.Invoke();
    }
}
