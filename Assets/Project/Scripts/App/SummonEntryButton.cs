using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 아바타 홈 가운데 'AR 소환'.
    // 유저 지시 플로우: 바로 촬영 화면이 아니라 주변 공간 스캔 → 바닥면 인식 성공 → 현실 공간 소환.
    // 그래서 ScreenButton 대신 이걸 쓴다 (진입 경로를 ARGuideMode 에 남겨야 한다).
    [RequireComponent(typeof(Image))]
    public class SummonEntryButton : MonoBehaviour
    {
        [SerializeField] string scanScreen = "ARScan";

        void Start()
        {
            var btn = GetComponent<Button>();
            if (btn == null) btn = gameObject.AddComponent<Button>();
            btn.onClick.AddListener(Go);
        }

        void Go()
        {
            ARGuideMode.Pending = ARGuideMode.Mode.Summon;
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(scanScreen);
            else Debug.LogError("[SummonEntry] ScreenManager 가 씬에 없음");
        }
    }
}
