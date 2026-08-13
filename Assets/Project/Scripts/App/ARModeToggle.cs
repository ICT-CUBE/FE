using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 스테이지 상세의 'AR 모드' 토글.
    // 이미 무대를 배치해뒀으면 재스캔 없이 바로 무대로, 아니면 공간 인식부터.
    [RequireComponent(typeof(Toggle))]
    public class ARModeToggle : MonoBehaviour
    {
        [SerializeField] ARSessionController session;
        [SerializeField] string guideScreen = "ARGuide";
        [SerializeField] string stageScreen = "ARStage";

        Toggle toggle;

        void Awake() => toggle = GetComponent<Toggle>();

        void OnEnable()
        {
            toggle.SetIsOnWithoutNotify(false);
            toggle.onValueChanged.AddListener(OnChanged);
        }

        void OnDisable() => toggle.onValueChanged.RemoveListener(OnChanged);

        void OnChanged(bool on)
        {
            if (!on || ScreenManager.Instance == null) return;

            // 가이드 화면이 '스테이지에서 왔다'는 걸 알아야 시안(ar모드 가이드) 배치로 뜬다
            ARGuideMode.Pending = ARGuideMode.Mode.StageAR;
            // 유저 지시(2026-08-09): 이미 배치돼 있어도 '주변 공간 스캔 → 바닥면 인식' 부터 다시 간다
            ScreenManager.Instance.Show(guideScreen);
        }
    }
}