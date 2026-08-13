using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 큐브 인식 중 진행바. 인식 로직이 붙기 전까지는 시간으로 채우고,
    // 실제 인식이 붙으면 Report(0~1) 를 호출해 주면 된다.
    public class ScanProgressController : MonoBehaviour
    {
        [SerializeField] RectTransform fill;      // ProgressTrack/ProgressFill
        [SerializeField] RectTransform star;     // 채움 끝을 따라가는 별
        [SerializeField] TMP_Text label;          // ProgressText
        [SerializeField] SpriteProgressBar progressAnim;   // 반짝이는 로딩바 (216장 시퀀스)
        [SerializeField] string message = "큐브를 인식하고 있어요";
        [SerializeField] float seconds = 3f;      // 인식이 붙기 전 임시 진행 시간
        [SerializeField] string nextScreen = "CubeFound";
        [SerializeField] bool autoAdvance = true;

        float t;
        float external = -1f;                     // Report 로 값이 들어오면 그걸 쓴다
        bool done;

        void OnEnable()
        {
            t = 0f; done = false; external = -1f;
            if (label != null) label.text = message;
            Draw(0f);
        }

        // 실제 인식률이 생기면 이걸 호출한다 (0~1)
        public void Report(float ratio01)
        {
            external = Mathf.Clamp01(ratio01);
        }

        void Update()
        {
            if (done) return;

            float p;
            if (external >= 0f) p = external;
            else
            {
                t += Time.deltaTime;
                p = seconds <= 0f ? 1f : Mathf.Clamp01(t / seconds);
            }

            Draw(p);

            if (p >= 1f)
            {
                done = true;
                if (autoAdvance && !string.IsNullOrEmpty(nextScreen))
                    ScreenManager.Instance?.Show(nextScreen);
            }
        }

        void Draw(float p)
        {
            if (progressAnim != null) progressAnim.SetProgress(p);
            if (fill == null) return;
            // 트랙 폭을 부모에서 받아 0~100% 로 채운다
            var parent = fill.parent as RectTransform;
            float w = parent != null ? parent.rect.width : fill.rect.width;
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = new Vector2(0f, 0f);
            fill.sizeDelta = new Vector2(w * p, 0f);

            // 별은 채움 끝에 붙어 같이 움직인다
            if (star != null)
            {
                star.anchorMin = star.anchorMax = new Vector2(0f, 0.5f);
                star.pivot = new Vector2(0.5f, 0.5f);
                star.anchoredPosition = new Vector2(w * p, 0f);
            }
        }
    }
}
