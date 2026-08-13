using UnityEngine;
using TMPro;

namespace ARZone
{
    // AR 흐름의 상단 제목은 진입 경로에 따라 달라진다.
    //   첫 인식(온보딩)      → "Scan AR"
    //   영상에서 AR 모드 진입 → "AR 모드"   (가이드 ~ 공연 멈춤까지 계속)
    // 화면마다 이 컴포넌트를 붙이고 NavTitle 을 물려두면 켜질 때 알아서 맞춘다.
    public class ARModeTitle : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] string firstScanTitle = "Scan AR";
        [SerializeField] string stageArTitle = "AR 모드";

        void OnEnable()
        {
            if (label == null) return;
            label.text = ARGuideMode.Pending == ARGuideMode.Mode.StageAR
                       ? stageArTitle : firstScanTitle;
        }
    }
}
