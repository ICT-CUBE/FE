using UnityEngine;

namespace ARZone
{
    // 갤러리 앱 모델. 서버 DTO(GalleryItemDto)와 일부러 분리했다 —
    // 화면은 이것만 보고, 서버 필드가 바뀌면 변환 함수 한 곳만 고친다.

    // 시안 갤러리 탭 4개와 순서까지 같다(전체 / 캡처 / 앨범 / 콘서트 스틸컷).
    // GalleryController.tabLabels 의 인덱스가 그대로 이 값이 된다.
    public enum GalleryCategory { All = 0, Capture = 1, Album = 2, Concert = 3 }

    // 서버 GallerySourceType 과 1:1. Unknown 은 서버가 새 타입을 추가했을 때
    // 앱이 조용히 죽지 않게 하는 자리다(로그를 남기고 건너뛴다).
    public enum GallerySourceType { Unknown = 0, StageCapture, TrackCapture, InteractionCapture, AlbumImage }

    public class GalleryItem
    {
        public int id;
        public GallerySourceType sourceType;
        public GalleryCategory category;
        public string imageUrl;
        public string displayDate;      // "2026-08-07"

        // 받아둔 사진. 아직 안 왔으면 null 이고, 그동안 타일은 정사각 자리표시자로 그린다.
        public Texture2D texture;

        // ★ 서버 id 는 타입이 다르면 겹친다. 실측:
        //   {"id":1,"sourceType":"INTERACTION_CAPTURE"} / {"id":1,"sourceType":"TRACK_CAPTURE"}
        //   {"id":1,"sourceType":"STAGE_CAPTURE"}  ← id 만으로 식별하면 세 장이 한 장이 된다.
        // 명세서에도 "프론트는 sourceType 과 조합해 항목을 식별한다" 고 적혀 있고,
        // 삭제·AR재경험 경로가 /{sourceType}/{id} 인 것도 같은 이유다.
        public string Key => ServerSourceType + ":" + id;

        // 경로에 그대로 넣는 서버 표기.
        public string ServerSourceType => ToServerString(sourceType);

        public static string ToServerString(GallerySourceType t)
        {
            switch (t)
            {
                case GallerySourceType.StageCapture:       return "STAGE_CAPTURE";
                case GallerySourceType.TrackCapture:       return "TRACK_CAPTURE";
                case GallerySourceType.InteractionCapture: return "INTERACTION_CAPTURE";
                case GallerySourceType.AlbumImage:         return "ALBUM_IMAGE";
                default:                                   return "";
            }
        }

        // JsonUtility 가 enum 을 이름으로 못 읽으므로 여기서 손으로 변환한다.
        public static GallerySourceType ParseSourceType(string s)
        {
            switch (s)
            {
                case "STAGE_CAPTURE":       return GallerySourceType.StageCapture;
                case "TRACK_CAPTURE":       return GallerySourceType.TrackCapture;
                case "INTERACTION_CAPTURE": return GallerySourceType.InteractionCapture;
                case "ALBUM_IMAGE":         return GallerySourceType.AlbumImage;
                default:                    return GallerySourceType.Unknown;
            }
        }

        public static GalleryCategory ParseCategory(string s)
        {
            switch (s)
            {
                case "CAPTURE":           return GalleryCategory.Capture;
                case "ALBUM":             return GalleryCategory.Album;
                case "CONCERT_STILLCUT":  return GalleryCategory.Concert;
                default:                  return GalleryCategory.All;
            }
        }

        // 조회 query 에 넣는 값. All 이면 서버 기본값과 같아 생략해도 되지만
        // 요청 URL 이 로그에 그대로 남는 편이 디버깅에 낫다.
        public static string ToServerCategory(GalleryCategory c)
        {
            switch (c)
            {
                case GalleryCategory.Capture: return "CAPTURE";
                case GalleryCategory.Album:   return "ALBUM";
                case GalleryCategory.Concert: return "CONCERT_STILLCUT";
                default:                      return "ALL";
            }
        }
    }
}
