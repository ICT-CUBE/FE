using UnityEngine;
using UnityEngine.EventSystems;

namespace ARZone
{
    // 바텀시트를 아래로 쓸어내리면 닫는다. (유저 지시: "신고 취소하고 싶으면 시트를 내리면 됨")
    // 시트 상단의 핸들(시안 44×4)이 이 제스처를 가리키는 표식이다.
    //
    // 붙이는 곳 = 시트 배경 Graphic (Sheet_*/SheetBg)
    // closeTarget = 끌 시트 루트 (없으면 부모)
    //
    // 주의: 지금은 시트가 손가락을 따라 내려가지 않고, 놓는 순간 임계값을 넘었으면 닫힌다.
    //       따라 내려가게 하려면 Dim 을 뺀 나머지 자식을 컨테이너 하나로 묶어야 하는데,
    //       그건 구도 변경이라 손대지 않았다.
    [RequireComponent(typeof(UnityEngine.UI.Graphic))]
    public class SheetSwipeToClose : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] GameObject closeTarget;
        [SerializeField] bool back;                   // 켜면 SetActive 대신 뒤로가기 (가사처럼 '화면'인 시트)
        [SerializeField] float closeDistance = 80f;   // 이만큼 아래로 끌면 닫는다

        float startY;
        bool dragging;

        public void OnBeginDrag(PointerEventData e)
        {
            startY = e.position.y;
            dragging = true;
        }

        public void OnDrag(PointerEventData e) { }

        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging) return;
            dragging = false;

            // 화면 해상도가 캔버스 기준(893)과 다르므로 캔버스 단위로 환산해서 잰다
            float scale = Screen.height > 0 ? 893f / Screen.height : 1f;
            float dragged = (startY - e.position.y) * scale;
            if (dragged < closeDistance) return;

            if (back)
            {
                if (ScreenManager.Instance != null) ScreenManager.Instance.Back();
                else Debug.LogError("[SheetSwipeToClose] ScreenManager 가 씬에 없음");
                return;
            }
            var t = closeTarget != null ? closeTarget
                  : transform.parent != null ? transform.parent.gameObject : null;
            if (t != null) t.SetActive(false);
        }
    }
}
