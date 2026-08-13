using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 진행률(0~1) 로 PNG 시퀀스에서 프레임을 골라 그린다.
    //
    // 반짝이는 로딩바(216장) 는 0→100% 가 통째로 구워져 있다. fps 로 돌리면 실제 로딩 시간과
    // 어긋나므로, 컨트롤러가 이미 계산해 둔 p 를 그대로 받아 프레임을 고른다
    // (ScanProgressController.Draw / StageLoadingController.Apply 가 호출한다).
    // → 로딩 시간이 바뀌어도 자동으로 딱 한 바퀴가 된다.
    [RequireComponent(typeof(Image))]
    public class SpriteProgressBar : MonoBehaviour
    {
        [SerializeField] Sprite[] frames;

        Image img;

        void Awake() { Bind(); }

        void OnEnable()
        {
            Bind();
            SetProgress(0f);
        }

        void Bind()
        {
            if (img != null) return;
            img = GetComponent<Image>();
            // 시퀀스 프레임에 트랙과 글로우 여백이 같이 들어 있어 원본 비율 그대로 그린다
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
        }

        public void SetProgress(float p)
        {
            if (frames == null || frames.Length == 0) return;
            Bind();
            int i = Mathf.RoundToInt(Mathf.Clamp01(p) * (frames.Length - 1));
            var s = frames[Mathf.Clamp(i, 0, frames.Length - 1)];
            if (s != null && img.sprite != s) img.sprite = s;
        }
    }
}
