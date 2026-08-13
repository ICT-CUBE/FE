using System;
using UnityEngine;

namespace ARZone
{
    // 대상을 1회 인식해 월드 포즈를 알려주는 검출기.
    // 폰=Vuforia, 에디터/피규어 전=Mock, 향후 GoogleAR=별도 구현으로 "교체"만.
    public interface IObjectDetector
    {
        event Action<Pose> OnDetected;   // 최초 1회 인식 성공 시 포즈 전달
        bool HasDetected { get; }
        Pose DetectedPose { get; }
        void ResetDetection();           // 다시 인식하고 싶을 때 초기화
    }
}