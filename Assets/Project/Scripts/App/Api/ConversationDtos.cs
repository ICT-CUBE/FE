using System;

namespace ARZone
{
    // 대화(conversation) 도메인 응답. 서버 JSON 모양 그대로.
    // ⚠ JsonUtility 는 enum 을 이름으로 못 읽는다 → conversationType/senderType 은 string 으로 받는다.

    [Serializable]
    public class ConvLastMessageDto
    {
        public int id;
        public string content;      // null 가능
        public string sentAt;       // "2026-08-24T11:45:09" (offset 없는 KST)
    }

    [Serializable]
    public class ConvSessionDto
    {
        public int id;
        public string conversationType;     // TEXT | VOICE
        public string conversationTitle;    // ★ null 로 온다 (새로 만든 세션엔 제목이 없다)
        public string audioUrl;             // VOICE 전용, null 가능
        public int audioDurationSeconds;
        public ConvLastMessageDto lastMessage;
    }

    [Serializable]
    public class ConvSessionListDto
    {
        public ConvSessionDto[] conversationSessions;
        public GalleryPaginationDto pagination;   // page/size/hasNext — 갤러리와 같은 모양이라 재사용
    }

    [Serializable]
    public class ConvMessageDto
    {
        public int id;
        public string senderType;       // USER | AVATAR
        public string content;
        public string audioUrl;         // 아바타 답변 TTS 파일 (VOICE, null 가능) — 2026-08-31 스펙 추가분
        public string[] imageUrls;      // 전송 응답에만 온다. 상세 조회엔 없다.
        public int sequenceNumber;
        public string sentAt;
    }

    [Serializable]
    public class ConvSessionDetailInnerDto
    {
        public int id;
        public ConvMessageDto[] messages;
    }

    [Serializable]
    public class ConvSessionDetailDto
    {
        public ConvSessionDetailInnerDto conversationSession;
    }

    [Serializable]
    public class ConvSessionCreateInnerDto
    {
        public int id;
        public string conversationType;
    }

    [Serializable]
    public class ConvSessionCreateDto
    {
        public ConvSessionCreateInnerDto conversationSession;
    }

    // POST …/messages 는 유저 메시지와 아바타 답변을 '한 번에' 돌려준다.
    // 아바타 응답은 고정 문구가 아니라 실제로 매번 다르게 생성된다(실측 확인).
    [Serializable]
    public class ConvSendResultDto
    {
        public ConvMessageDto userMessage;
        public ConvMessageDto avatarMessage;
    }

    [Serializable]
    public class ConvSaveInnerDto
    {
        public int id;
        public bool isTextSaved;
    }

    [Serializable]
    public class ConvSaveDto
    {
        public ConvSaveInnerDto conversationMessage;
    }

    // 대화 보관함 — GET …/conversation-archive
    [Serializable]
    public class ConvSavedTextDto
    {
        public int id;
        public int conversationSessionId;
        public string content;
        public int sequenceNumber;
        public string sentAt;
    }

    [Serializable]
    public class ConvArchiveDto
    {
        public ConvSavedTextDto[] savedTexts;
    }

    // 오류 신고 — POST …/conversation-messages/{id}/reports
    [Serializable]
    public class ConvReportReq
    {
        public string reason;         // INCORRECT_PERSONA | HALLUCINATION | INAPPROPRIATE_EXPRESSION
        public string detailReason;   // null 가능
    }

    // 보낼 때 쓰는 요청 바디 (JsonUtility.ToJson 으로 직렬화한다)
    [Serializable]
    public class ConvCreateReq
    {
        public string conversationType;
    }

    [Serializable]
    public class ConvSendReq
    {
        public string conversationType;   // "TEXT" | "VOICE" — 2026-08-31 서버 배포에서 필수가 됨
        public string content;
        public string[] imageUrls;
    }
}
