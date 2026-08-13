using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARZone
{
    // 피규어/Vuforia 없이 "인식됨"을 흉내내는 가짜 검출기.
    // 화면을 한 번 탭(또는 마우스 클릭)하면 인식된 것으로 처리 → 소환 트리거.
    // 피규어 완성되면 이 자리를 VuforiaImageDetector 로 교체.
    public class MockObjectDetector : MonoBehaviour, IObjectDetector
    {
        public event Action<Pose> OnDetected;
        public bool HasDetected { get; private set; }
        public Pose DetectedPose { get; private set; }

        void Update()
        {
            if (HasDetected) return;
            if (!TapDownThisFrame()) return;

            var cam = Camera.main;
            DetectedPose = cam != null
                ? new Pose(cam.transform.position + cam.transform.forward * 0.4f, Quaternion.identity)
                : new Pose(Vector3.zero, Quaternion.identity);
            HasDetected = true;
            OnDetected?.Invoke(DetectedPose);
            Debug.Log("[Mock] 인식됨(가짜) → 소환");
        }

        public void ResetDetection() => HasDetected = false;

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
    }
}