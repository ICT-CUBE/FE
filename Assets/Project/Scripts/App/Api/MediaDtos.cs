using System;

namespace ARZone
{
    // stage / track / album 도메인 응답. 서버 JSON 모양 그대로.

    [Serializable]
    public class StageListItemDto
    {
        public int id;
        public string title;            // "Mantra MV"
        public string description;
        public string thumbnailUrl;     // null 가능
        public string recordedAt;
        public bool isTitleVideo;
    }

    [Serializable]
    public class StageListDto
    {
        public StageListItemDto[] stages;
    }

    [Serializable]
    public class AlbumTrackDto
    {
        public int id;
        public int trackNumber;
        public string title;            // "like JENNIE"
        public string artistName;
        public int durationSeconds;
        public bool isTitleTrack;
    }

    [Serializable]
    public class AlbumInnerDto
    {
        public int id;
        public string title;
        public string artistName;
        public int trackCount;
        public int trackTotalSeconds;
        public string coverImageUrl;
        public string releaseDate;
        public AlbumTrackDto[] tracks;
    }

    [Serializable]
    public class AlbumDto
    {
        public AlbumInnerDto album;
    }

    // 상세 응답 (2026-08-26 스트리밍 전환) — 실측:
    //   GET /stages/{id} = {"stage":{"id":1,"videoUrl":"https://…/mv_mantra.mp4","durationSeconds":136}}
    //   GET /tracks/{id} = {"track":{"id":1,"audioUrl":"https://…/song_intro.mp3","durationSeconds":98,"lyrics":[…]}}
    //   (lyrics 는 앱이 로컬 LRC 를 쓰므로 받지 않는다 — JsonUtility 는 없는 필드를 무시한다)
    [Serializable]
    public class StageDetailItemDto { public int id; public string videoUrl; public int durationSeconds; }
    [Serializable]
    public class StageDetailDto { public StageDetailItemDto stage; }
    [Serializable]
    public class TrackDetailItemDto { public int id; public string audioUrl; public int durationSeconds; }
    [Serializable]
    public class TrackDetailDto { public TrackDetailItemDto track; }

    // 세션 생성 응답 — {"stageSession":{"id":4,"viewMode":"AR"}} / {"trackSession":{…}}
    [Serializable]
    public class StageSessionInnerDto { public int id; public string viewMode; }
    [Serializable]
    public class StageSessionCreateDto { public StageSessionInnerDto stageSession; }
    [Serializable]
    public class TrackSessionInnerDto { public int id; public string playMode; }
    [Serializable]
    public class TrackSessionCreateDto { public TrackSessionInnerDto trackSession; }

    // 요청 바디
    [Serializable]
    public class StageSessionReq { public string viewMode; }     // NORMAL | AR
    [Serializable]
    public class TrackSessionReq { public string playMode; }     // NORMAL | SPECIAL
    [Serializable]
    public class MediaSessionSaveReq
    {
        public string status;               // COMPLETED | CANCELLED
        public int lastPositionSeconds;
        public int viewedSeconds;
    }
}
