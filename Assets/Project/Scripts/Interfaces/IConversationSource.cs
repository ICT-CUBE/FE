using System;
using System.Collections.Generic;

namespace ARZone
{
    // 대화 데이터의 유일한 접점. IGallerySource / ICommunitySource 와 같은 콜백 관례.
    public interface IConversationSource
    {
        // ⚠ conversationType 은 서버 스펙상 **필수**다(빼면 400). 그래서 인자로 받는다.
        void LoadSessions(ConversationType type, int page, int size,
                          Action<List<ConvSession>, bool> done, Action<string> fail);

        void LoadMessages(int sessionId, Action<List<ConvMessage>> done, Action<string> fail);

        void CreateSession(ConversationType type, Action<int> done, Action<string> fail);

        void DeleteSession(int sessionId, Action done, Action<string> fail);

        // 세션 종료(NOT_ACTIVE + 종료시각). 서버는 제목이 없으면 첫 메시지 2개로 GPT 제목을 만든다.
        // 멱등이고, 종료 후에도 메시지 전송은 된다(실측) — 화면을 나갈 때마다 불러도 안전.
        void EndSession(int sessionId, Action done, Action<string> fail);

        // 서버가 유저 메시지와 아바타 답변을 한 번에 돌려준다.
        // type: 세션 종류(TEXT/VOICE) — 2026-08-31 서버 배포부터 요청 바디에 필수.
        void SendMessage(int sessionId, ConversationType type, string content, string[] imageUrls,
                         Action<ConvMessage, ConvMessage> done, Action<string> fail);

        // 대화 텍스트 보관(보관함으로). 결과로 보관 여부가 온다.
        void SaveMessage(int messageId, Action<bool> done, Action<string> fail);

        // 보관함 목록 / 보관 해제 / 아바타 답변 오류 신고
        void LoadArchive(Action<List<ConvSaved>> done, Action<string> fail);
        void Unsave(int messageId, Action done, Action<string> fail);
        void ReportMessage(int messageId, string reason, string detail, Action done, Action<string> fail);
    }
}
