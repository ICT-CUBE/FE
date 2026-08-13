using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 공간 스캔 중 표시되는 애니메이션.
    // 디자이너가 준 PNG 시퀀스(scan_000~149)를 프레임 단위로 재생한다.
    // ScanIndicator(Image) 에 붙인다.
    [RequireComponent(typeof(Image))]
    public class ScanIndicatorAnimator : MonoBehaviour
    {
        [SerializeField] Sprite[] frames;        // scan_000 ~ scan_149 순서대로
        [SerializeField] float fps = 30f;
        [SerializeField] bool loop = true;

        Image img;
        float t;
        int index;

        void Awake()
        {
            img = GetComponent<Image>();
            // 시퀀스에 여백이 포함돼 있으므로 원본 비율 그대로 그린다
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
        }

        void OnEnable()
        {
            t = 0f;
            index = 0;
            Apply();
        }

        void Update()
        {
            if (frames == null || frames.Length == 0 || fps <= 0f) return;

            t += Time.deltaTime;
            float step = 1f / fps;
            while (t >= step)
            {
                t -= step;
                index++;
                if (index >= frames.Length)
                {
                    if (!loop) { index = frames.Length - 1; break; }
                    index = 0;
                }
            }
            Apply();
        }

        void Apply()
        {
            if (img == null || frames == null || frames.Length == 0) return;
            var s = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
            if (s != null && img.sprite != s) img.sprite = s;
        }

#if UNITY_EDITOR
        // 인스펙터 우클릭 → '시퀀스 자동 연결' 로 150장을 한 번에 채운다.
        [ContextMenu("시퀀스 자동 연결")]
        void AutoFill()
        {
            const string dir = "Assets/Project/Sprites/ScanSequence";
            var list = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; ; i++)
            {
                var path = $"{dir}/scan_{i:000}.png";
                var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s == null) break;
                list.Add(s);
            }
            frames = list.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ScanIndicator] {frames.Length}장 연결됨");
        }
#endif
    }
}
