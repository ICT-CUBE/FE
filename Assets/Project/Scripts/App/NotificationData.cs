using UnityEngine;

namespace ARZone
{
    public enum NotifCategory { Mission, Event, Market, Avatar }

    // 알림 한 건. 나중에 서버에서 받아올 형태.
    [System.Serializable]
    public class NotificationData
    {
        public string title = "제니가 답장을 보냈어요";
        [TextArea] public string body = "\"오늘 하루 어땠어?\"";
        public string time = "5분 전";
        public NotifCategory category = NotifCategory.Avatar;
        public Sprite icon;
        public bool read;

        // 탭했을 때 이동할 화면 id. 비우면 읽음 처리만 하고 이동하지 않는다.
        // (아바타 대화·이벤트 상세·거래 상세는 아직 화면이 없어 비워둔다)
        public string targetScreen;
    }
}
