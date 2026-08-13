using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARZone
{
    // 배치 단계: 화면 중앙(또는 터치 지점)으로 레이캐스트해 무대를 바닥에 올리고,
    // 두 손가락으로 회전시킨다.
    public class StagePlacementController : MonoBehaviour
    {
        [SerializeField] float rotateSpeed = 1f;

        readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        ARSessionController session;
        float prevAngle = float.NaN;
        bool active;

        public void Begin(ARSessionController s)
        {
            session = s;
            prevAngle = float.NaN;
            active = session != null;
        }

        public void End()
        {
            active = false;
            session = null;
            prevAngle = float.NaN;
        }

        void Update()
        {
            if (!active || session == null || session.RaycastManager == null) return;

            // 두 손가락이면 회전만 하고 위치는 건드리지 않는다
            if (TryGetTwoTouches(out var a, out var b))
            {
                Rotate(a, b);
                return;
            }
            prevAngle = float.NaN;

            Vector2 screenPoint = TryGetTouch(out var t)
                ? t
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            if (!session.RaycastManager.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon))
                return;

            // 벽·천장에 붙지 않도록 '위를 향한 수평면' 만 채택한다.
            // hits 는 가까운 순이라, 그냥 hits[0] 을 쓰면 방 안에서는 벽이 먼저 걸린다.
            foreach (var h in hits)
            {
                var plane = session.PlaneManager != null
                    ? session.PlaneManager.GetPlane(h.trackableId)
                    : null;
                if (plane != null && plane.alignment != PlaneAlignment.HorizontalUp) continue;

                session.PlaceStage(h.pose);
                break;
            }
        }

        void Rotate(Vector2 a, Vector2 b)
        {
            var stage = session.Stage;
            if (stage == null) return;

            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            if (!float.IsNaN(prevAngle))
            {
                float delta = Mathf.DeltaAngle(prevAngle, angle);
                stage.Rotate(0f, -delta * rotateSpeed, 0f, Space.World);
            }
            prevAngle = angle;
        }

        static bool TryGetTouch(out Vector2 pos)
        {
            pos = default;
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts == null) return false;
            foreach (var t in ts.touches)
                if (t.press.isPressed) { pos = t.position.ReadValue(); return true; }
            return false;
#else
            if (Input.touchCount < 1) return false;
            pos = Input.GetTouch(0).position;
            return true;
#endif
        }

        static bool TryGetTwoTouches(out Vector2 a, out Vector2 b)
        {
            a = default; b = default;
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts == null) return false;
            int n = 0;
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed) continue;
                if (n == 0) a = t.position.ReadValue();
                else { b = t.position.ReadValue(); return true; }
                n++;
            }
            return false;
#else
            if (Input.touchCount < 2) return false;
            a = Input.GetTouch(0).position;
            b = Input.GetTouch(1).position;
            return true;
#endif
        }
    }
}