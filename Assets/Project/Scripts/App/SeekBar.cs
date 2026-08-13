using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ARZone
{
    // 진행바를 누르거나 끌어서 재생 위치를 옮긴다.
    // 트랙이 4px 로 얇아 손가락으로 못 집으므로, Image 의 raycastPadding 을
    // 음수로 줘서 히트 영역만 위아래로 넓혀 쓴다(오브젝트를 늘리면 바가 두꺼워진다).
    public class SeekBar : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        // 0~1 비율. 컨트롤러가 받아서 audioSrc.time 을 옮긴다.
        public event Action<float> Seeked;

        [SerializeField] RectTransform track;   // 비우면 자기 자신

        RectTransform Rect => track != null ? track : (RectTransform)transform;

        void Apply(PointerEventData e)
        {
            var rt = Rect;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt, e.position, e.pressEventCamera, out var local)) return;
            float w = rt.rect.width;
            if (w < 0.01f) return;
            Seeked?.Invoke(Mathf.Clamp01((local.x - rt.rect.xMin) / w));
        }

        public void OnPointerDown(PointerEventData e) => Apply(e);
        public void OnDrag(PointerEventData e) => Apply(e);
    }
}
