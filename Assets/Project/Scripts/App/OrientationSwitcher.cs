using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 이 오브젝트가 켜져 있는 동안만 가로 화면으로 전환한다.
    // Canvas 기준 해상도도 같이 뒤집어야 UI 크기가 유지된다.
    // Screen_StagePlayer 루트에 붙인다.
    public class OrientationSwitcher : MonoBehaviour
    {
        [SerializeField] CanvasScaler scaler;   // 비워두면 부모 Canvas 에서 찾음
        [SerializeField] ScreenOrientation target = ScreenOrientation.LandscapeLeft;

        ScreenOrientation prevOrientation;
        Vector2 prevReference;
        float prevMatch;
        bool applied;

        void Awake()
        {
            if (scaler == null) scaler = GetComponentInParent<CanvasScaler>();
        }

        void OnEnable()
        {
            prevOrientation = Screen.orientation;
            Screen.orientation = target;

            if (scaler == null) return;

            prevReference = scaler.referenceResolution;
            prevMatch = scaler.matchWidthOrHeight;

            // 480×1040 → 1040×480 으로 뒤집고, 높이 기준으로 맞춘다.
            scaler.referenceResolution = new Vector2(prevReference.y, prevReference.x);
            scaler.matchWidthOrHeight = 1f;

            applied = true;
        }

        void OnDisable()
        {
            if (!applied) return;

            Screen.orientation = prevOrientation;

            if (scaler != null)
            {
                scaler.referenceResolution = prevReference;
                scaler.matchWidthOrHeight = prevMatch;
            }

            applied = false;
        }
    }
}