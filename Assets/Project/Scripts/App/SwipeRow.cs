using UnityEngine;
using UnityEngine.EventSystems;

namespace ARZone
{
    // 행을 왼쪽으로 밀면 삭제 버튼이 드러난다. 짧게 누르면 열기.
    public class SwipeRow : MonoBehaviour, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        RectTransform face;
        float width;
        System.Action onClick;
        bool opened;
        bool dragged;

        public void Setup(RectTransform target, float w, System.Action click)
        {
            face = target; width = w; onClick = click;
        }

        public void OnDrag(PointerEventData e)
        {
            if (face == null) return;
            dragged = true;
            float x = Mathf.Clamp(face.anchoredPosition.x + e.delta.x, -width, 0f);
            face.anchoredPosition = new Vector2(x, face.anchoredPosition.y);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (face == null) return;
            opened = face.anchoredPosition.x < -width * 0.5f;
            face.anchoredPosition = new Vector2(opened ? -width : 0f, face.anchoredPosition.y);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (dragged) { dragged = false; return; }
            if (opened)
            {
                opened = false;
                if (face != null) face.anchoredPosition = new Vector2(0f, face.anchoredPosition.y);
                return;
            }
            onClick?.Invoke();
        }
    }
}
