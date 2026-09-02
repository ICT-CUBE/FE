using System;

namespace ARZone
{
    // 서버 JSON 모양 그대로. 앱 모델(GalleryItem)과 분리해 둔다 —
    // 서버가 필드 이름을 바꿔도 변환 함수 한 곳만 고치면 화면 코드는 안 바뀐다.
    //
    // ★ JsonUtility 는 enum 을 '이름' 이 아니라 '숫자' 로 매핑한다.
    //   "CAPTURE" 같은 문자열은 enum 필드에 못 넣는다 → 전부 string 으로 받고
    //   앱 enum 으로는 손으로 변환한다(GalleryItem.ToCategory).
    //
    // 필드는 반드시 public 이어야 JsonUtility 가 채운다.

    [Serializable]
    public class GalleryItemDto
    {
        public int id;
        public string sourceType;    // STAGE_CAPTURE | TRACK_CAPTURE | INTERACTION_CAPTURE | ALBUM_IMAGE
        public string category;      // CAPTURE | ALBUM | CONCERT_STILLCUT
        public string imageUrl;
        public string displayDate;   // "2026-08-07" (date, offset 없음)
    }

    [Serializable]
    public class GalleryPaginationDto
    {
        public int page;
        public int size;
        public bool hasNext;
    }

    [Serializable]
    public class GalleryResponseDto
    {
        public GalleryItemDto[] galleryItems;
        public GalleryPaginationDto pagination;
    }

    // AR 에서 다시 경험하기 — STAGE_CAPTURE / TRACK_CAPTURE 에서만 온다.
    // startPositionSeconds 는 서버 스펙상 null 이 가능하다(int|null).
    // JsonUtility 는 null 을 0 으로 만드는데, 여기서는 '처음부터 재생' 과 같은 뜻이라
    // 0 으로 떨어져도 동작이 달라지지 않는다. 구분이 필요해지면 string 으로 바꿀 것.
    [Serializable]
    public class GalleryStageDto
    {
        public int id;
        public string title;
        public string videoUrl;
        public int durationSeconds;
    }

    [Serializable]
    public class GalleryTrackDto
    {
        public int id;
        public string title;
        public string audioUrl;
        public int durationSeconds;
    }

    [Serializable]
    public class GalleryArExperienceItemDto
    {
        public string contentType;    // STAGE | TRACK
        public string requiredMode;   // AR | SPECIAL
        public int startPositionSeconds;
        public GalleryStageDto stage;
        public GalleryTrackDto track;
    }

    [Serializable]
    public class GalleryArExperienceDto
    {
        public GalleryArExperienceItemDto galleryItem;
    }
}
