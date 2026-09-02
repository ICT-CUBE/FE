using System;

namespace ARZone
{
    // 홈 정보의 유일한 접점. IGallerySource / ICommunitySource 와 같은 콜백 관례.
    //
    // 홈은 화면 하나가 전부라 별도 Store 를 두지 않는다 —
    // HomeController 가 화면이 켜질 때 직접 부른다(공유할 상대가 없다).
    public interface IHomeSource
    {
        void LoadHome(Action<HomeResponseDto> done, Action<string> fail);
    }
}
