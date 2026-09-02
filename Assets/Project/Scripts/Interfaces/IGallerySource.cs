using System;
using System.Collections.Generic;

namespace ARZone
{
    // 갤러리 데이터의 유일한 접점. ICommunitySource 와 같은 관례를 쓴다.
    //
    // 콜백인 이유: UnityWebRequest 가 코루틴이라 콜백이 그대로 맞고,
    // Mock 은 즉시 done 을 부르면 되므로 호출부가 동일하다.
    // fail 은 네트워크 오류를 토스트로 띄울 자리다 — Mock 은 부르지 않는다.
    //
    // 화면(GalleryController)은 이 인터페이스만 보므로 서버가 붙어도 안 바뀐다.
    public interface IGallerySource
    {
        // page/size 는 서버 스펙 그대로 열어둔다. 지금 GalleryStore 는 size 를 크게 잡아
        // 한 번에 전량을 받는다 — 시안에 '더 보기'·로딩 표시가 없어서다(설계서 9-3).
        void LoadItems(GalleryCategory category, int page, int size,
                       Action<List<GalleryItem>, bool> done, Action<string> fail);

        // 서버에 일괄 삭제가 없다. 한 장씩 부른다.
        void DeleteItem(GalleryItem item, Action done, Action<string> fail);

        // AR 에서 다시 경험하기. STAGE_CAPTURE / TRACK_CAPTURE 에서만 유효하고
        // 그 외 타입은 서버가 400 을 준다(실측).
        void LoadArExperience(GalleryItem item,
                              Action<GalleryArExperienceItemDto> done, Action<string> fail);
    }
}
