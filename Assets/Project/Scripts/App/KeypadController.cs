using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 커스텀 숫자 키패드. 3열 × 4행 칸을 나눠서 키 프리팹을 배치한다.
    // OS 키보드를 안 쓰는 이유는 목업이 전용 패드를 요구하기 때문.
    public class KeypadController : MonoBehaviour
    {
        [SerializeField] GameObject keyPrefab;
        [SerializeField] RectTransform container;
        [SerializeField] Vector2 spacing = new Vector2(8f, 8f);

        // 라벨이 빈 항목은 빈 칸으로 남긴다. "<" 는 지우기.
        static readonly (string label, string sub)[] Keys =
        {
            ("1", ""),     ("2", "ABC"), ("3", "DEF"),
            ("4", "GHI"),  ("5", "JKL"), ("6", "MNO"),
            ("7", "PQRS"), ("8", "TUV"), ("9", "WXYZ"),
            ("",  ""),     ("0", ""),    ("<", ""),
        };

        // 눌린 키를 알린다. 숫자는 "0"~"9", 지우기는 "<".
        public event Action<string> OnKey;

        void Start() => Build();

        public void Build()
        {
            if (keyPrefab == null || container == null)
            {
                Debug.LogError("[Keypad] keyPrefab / container 연결 필요");
                return;
            }

            UiUtil.ClearChildren(container);

            for (int i = 0; i < Keys.Length; i++)
            {
                if (string.IsNullOrEmpty(Keys[i].label)) continue;

                int col = i % 3, row = i / 3;

                var key = Instantiate(keyPrefab, container);
                key.SetActive(true);
                key.name = $"Key_{Keys[i].label}";

                // 3열 × 4행 셀을 통째로 차지하고 spacing 만큼 안쪽으로 들인다.
                var rt = (RectTransform)key.transform;
                rt.anchorMin = new Vector2(col / 3f, 1f - (row + 1) / 4f);
                rt.anchorMax = new Vector2((col + 1) / 3f, 1f - row / 4f);
                rt.offsetMin = new Vector2(spacing.x * 0.5f, spacing.y * 0.5f);
                rt.offsetMax = new Vector2(-spacing.x * 0.5f, -spacing.y * 0.5f);

                Fill(key, Keys[i].label, Keys[i].sub);
            }
        }

        void Fill(GameObject key, string label, string sub)
        {
            var lb = key.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (lb != null) lb.text = label;

            var sb = key.transform.Find("SubLabel")?.GetComponent<TMP_Text>();
            if (sb != null) sb.text = sub;

            var btn = key.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(() => OnKey?.Invoke(label));
        }
    }
}