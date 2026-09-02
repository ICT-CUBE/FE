using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 화면 하단에 잠깐 떴다 사라지는 알림.
    // 시안: (20,789) 372x46, #181A20 + #242831 테두리, 가운데 글자.
    //
    // UI 는 런타임에 만든다(씬에 미리 둘 필요 없음).
    // 쓰는 쪽: Toast.Show("JENNIE 친밀도 5P 획득!");
    // ⚠ 이모지 금지 — Pretendard 에 글리프가 없어 네모로 깨진다 (✨ 실기 확인, 2026-08-26)
    public class Toast : MonoBehaviour
    {
        [SerializeField] Sprite background;   // bg_round8 등 9-slice
        [SerializeField] float seconds = 2f;

        static Toast instance;

        RectTransform box;
        TMP_Text label;
        CanvasGroup group;
        Coroutine running;

        void Awake()
        {
            instance = this;
            Build();
        }

        void OnDestroy() { if (instance == this) instance = null; }

        // bottom = 화면 아래에서 띄울 높이. 시안 기본 58, 하단 버튼이 있는 화면은 125.
        public static void Show(string message, float bottom = 58f)
        {
            if (instance == null) return;
            instance.Play(message, bottom);
        }

        void Build()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsLastSibling();          // 항상 맨 위

            box = (RectTransform)go.transform;
            box.anchorMin = new Vector2(0, 0);
            box.anchorMax = new Vector2(1, 0);
            box.pivot = new Vector2(0.5f, 0);
            box.sizeDelta = new Vector2(-40, 46);     // 좌우 20 여백 → 372
            box.anchoredPosition = new Vector2(0, 58); // 시안 y789 → 아래에서 58

            var img = go.GetComponent<Image>();
            img.sprite = background;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.094118f, 0.101961f, 0.125490f, 1f);   // #181A20
            img.raycastTarget = false;

            var t = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            t.transform.SetParent(go.transform, false);
            var rt = (RectTransform)t.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            label = t.GetComponent<TMP_Text>();
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            DesignTokens.Type.Body1.Apply(label);
            label.color = DesignTokens.Text.Primary;

            group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        void Play(string message, float bottom)
        {
            if (label == null) return;
            if (box != null)
            {
                box.anchoredPosition = new Vector2(0, bottom);
                // 토스트는 Awake 에 만들어져 형제 순서가 앞이라, 뒤에 오는 시트·모달에 가린다.
                // (사진 선택 시트 위에 떠야 하는 '최대 5장' 안내가 안 보였다)
                box.SetAsLastSibling();
            }
            label.text = message;
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Fade());
        }

        IEnumerator Fade()
        {
            yield return Tween(0f, 1f, 0.18f);
            yield return new WaitForSeconds(seconds);
            yield return Tween(1f, 0f, 0.25f);
            running = null;
        }

        IEnumerator Tween(float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            group.alpha = to;
        }
    }
}
