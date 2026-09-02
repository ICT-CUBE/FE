using System;
using System.Globalization;

namespace ARZone
{
    // 대화 앱 모델. 서버 DTO 와 분리한다 — 화면은 이것만 본다.

    public enum ConversationType { Text, Voice }

    public class ConvSession
    {
        public int id;
        public ConversationType type;
        public string title;        // 서버가 null 을 줄 수 있다 (새 세션)
        public string preview;      // 마지막 메시지 내용
        public string timeText;     // "오후 12:25" / "어제" / "3일 전"
        public string audioUrl;
        public int audioSeconds;

        // ⚠ 서버가 주지 않는 값들. 시안 목록엔 '안읽음 점'과 미리보기 끝 🖤 가 있는데
        //    conversation-sessions 응답에 대응 필드가 없다 → 항상 false 다.
        //    지어내지 않고 자리만 남겨둔다. 서버가 생기면 여기만 채우면 화면은 그대로 돈다.
        public bool unread;
        public bool heart;
    }

    public class ConvMessage
    {
        public int id;
        public bool mine;           // senderType == USER
        public string text;
        public string timeText;
        public int sequence;
        public string[] imageUrls;
        public string audioUrl;     // 아바타 답변 TTS (VOICE 전용, 없으면 null)
    }

    // 보관함(저장한 대화 텍스트) 한 장
    public class ConvSaved
    {
        public int id;                 // 보관 해제(unsave)에 쓰는 메시지 id
        public string text;
        public string sentAt;          // 정렬용 원본
        public string dateText;        // "2026. 06. 10" (시안 표기)
    }

    public static class ConvTime
    {
        // 서버는 offset 없는 KST ISO 8601 을 준다("2026-08-24T11:45:09").
        // 기기도 KST 라 DateTime.Parse 를 로컬로 읽으면 그대로 맞는다.
        public static bool TryParse(string iso, out DateTime t)
            => DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out t);

        // 보관함 카드 날짜 표기 (시안: "2026. 06. 10")
        public static string ForArchive(string iso)
            => TryParse(iso, out var t) ? $"{t.Year}. {t.Month:00}. {t.Day:00}" : "";

        // 대화 목록 시안 표기: 오늘이면 시각, 어제면 '어제', 그 전이면 'N일 전'.
        public static string ForList(string iso)
        {
            if (!TryParse(iso, out var t)) return "";
            var days = (DateTime.Now.Date - t.Date).Days;
            if (days <= 0) return Clock(t);
            if (days == 1) return "어제";
            return $"{days}일 전";
        }

        // 말풍선 옆 시각 표기: "오후 12:25"
        public static string ForBubble(string iso)
            => TryParse(iso, out var t) ? Clock(t) : "";

        static string Clock(DateTime t)
        {
            int h = t.Hour;
            string ap = h < 12 ? "오전" : "오후";
            int h12 = h % 12; if (h12 == 0) h12 = 12;
            return $"{ap} {h12}:{t.Minute:00}";
        }
    }
}
