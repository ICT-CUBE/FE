using System;
using UnityEngine;

namespace ARZone
{
    // 더블탭 등 포인터 입력. 폰=터치, 에디터=마우스, 향후 Quest=핸드로 교체.
    // 더블탭한 화면 좌표(Vector2)를 전달 → 그 지점으로 걸어가기에 사용.
    public interface IPointerInput
    {
        event Action<Vector2> OnDoubleTap;
    }
}