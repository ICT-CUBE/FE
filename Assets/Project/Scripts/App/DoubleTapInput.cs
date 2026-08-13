using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARZone
{
    // 터치(폰) 또는 마우스 클릭(에디터)의 더블탭을 감지하고 그 화면 좌표를 전달.
    public class DoubleTapInput : MonoBehaviour, IPointerInput
    {
        public event Action<Vector2> OnDoubleTap;

        [SerializeField] float maxInterval = 0.3f; // 두 탭 사이 최대 간격(초)
        float lastTapTime = -10f;

        void Update()
        {
            if (!TapDownThisFrame()) return;

            float now = Time.time;
            if (now - lastTapTime <= maxInterval)
            {
                OnDoubleTap?.Invoke(PointerPosition());
                lastTapTime = -10f; // 트리플 방지
            }
            else lastTapTime = now;
        }

        static bool TapDownThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            return false;
#else
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
            if (Input.GetMouseButtonDown(0)) return true;
            return false;
#endif
        }

        static Vector2 PointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null) return ts.primaryTouch.position.ReadValue();
            var mouse = Mouse.current;
            if (mouse != null) return mouse.position.ReadValue();
            return Vector2.zero;
#else
            if (Input.touchCount > 0) return Input.GetTouch(0).position;
            return (Vector2)Input.mousePosition;
#endif
        }
    }
}