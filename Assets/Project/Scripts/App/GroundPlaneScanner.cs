using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ARZone
{
    // ARCore 가 바닥 평면을 안정적으로 찾는 순간을 알려준다. ARScan → ARPlace 전환용.
    // 타이머가 아니라 실제 평면 검출이다. 평면이 끊기면 처음부터 다시 센다.
    public class GroundPlaneScanner : MonoBehaviour
    {
        public event Action PlaneFound;

        [SerializeField] float requiredStableSeconds = 0.5f;  // 이만큼 연속으로 잡혀야 인정
        [SerializeField] float minPlaneArea = 0.2f;           // 너무 작은 조각은 무시 (m²)

        ARSessionController session;
        float stableSince = -1f;
        bool reported;

        public void Begin(ARSessionController s)
        {
            session = s;
            reported = false;
            stableSince = -1f;
        }

        public void End()
        {
            session = null;
            stableSince = -1f;
        }

        void Update()
        {
            if (session == null || reported) return;

            var pm = session.PlaneManager;
            if (pm == null || !pm.enabled) return;

            bool ok = false;
            foreach (var plane in pm.trackables)
            {
                // 위를 향한 수평면만 바닥으로 인정 (벽·천장·책상 밑면 제외)
                if (plane.alignment != PlaneAlignment.HorizontalUp) continue;

                var s = plane.size;
                if (s.x * s.y < minPlaneArea) continue;

                ok = true;
                break;
            }

            if (!ok) { stableSince = -1f; return; }

            if (stableSince < 0f) stableSince = Time.time;
            if (Time.time - stableSince >= requiredStableSeconds)
            {
                reported = true;
                PlaneFound?.Invoke();
            }
        }

        void OnDisable() => End();
    }
}