using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 이번 주 친밀도 막대 그래프.
    // 컨테이너 가로를 7등분해서 요일 칸을 만들고 프리팹을 하나씩 배치한다.
    public class IntimacyGraphController : MonoBehaviour
    {
        [SerializeField] GameObject barPrefab;    // GraphBar 프리팹
        [SerializeField] RectTransform container; // BarsContainer
        [SerializeField] int maxValue = 30;       // Y축 최대치

        static readonly string[] DayNames = { "월", "화", "수", "목", "금", "토", "일" };

        // 막대 위아래로 값/요일 텍스트가 차지하는 높이
        const float ReservedHeight = 50f;

        void Start()
        {
            // 임시 데이터 (나중에 서버에서 받아옴)
            SetData(new[] { 10, 8, 15, 12, 0, 0, 0 }, todayIndex: 3);
        }

        public void SetData(IReadOnlyList<int> values, int todayIndex)
        {
            if (barPrefab == null || container == null)
            {
                Debug.LogError("[IntimacyGraph] barPrefab / container 연결 필요");
                return;
            }

            UiUtil.ClearChildren(container);

            float barArea = Mathf.Max(0f, container.rect.height - ReservedHeight);

            for (int i = 0; i < DayNames.Length; i++)
            {
                var bar = Instantiate(barPrefab, container);
                bar.SetActive(true);
                bar.name = $"Bar_{DayNames[i]}";

                // 컨테이너를 7등분해서 i번째 칸을 통째로 차지시킨다.
                var rt = (RectTransform)bar.transform;
                rt.anchorMin = new Vector2(i / 7f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 7f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                int v = i < values.Count ? values[i] : 0;
                Apply(bar, v, DayNames[i], i == todayIndex, barArea);
            }
        }

        void Apply(GameObject bar, int value, string day, bool isToday, float barArea)
        {
            float fillHeight = 0f;
            var fill = bar.transform.Find("Fill") as RectTransform;
            if (fill != null)
            {
                float ratio = maxValue > 0 ? Mathf.Clamp01((float)value / maxValue) : 0f;
                fillHeight = barArea * ratio;
                fill.sizeDelta = new Vector2(fill.sizeDelta.x, fillHeight);

                var img = fill.GetComponent<Image>();
                if (img != null) img.color = DesignTokens.Action.Primary;
            }

            var valTx = bar.transform.Find("ValueText")?.GetComponent<TMP_Text>();
            if (valTx != null)
            {
                valTx.text = value > 0 ? $"+{value}" : "";
                // 막대 꼭대기 바로 위로 올린다 (막대 밑동 26 + 막대 높이 + 여백 4)
                ((RectTransform)valTx.transform).anchoredPosition =
                    new Vector2(0f, 26f + fillHeight + 4f);
            }

            var dayTx = bar.transform.Find("DayText")?.GetComponent<TMP_Text>();
            if (dayTx != null)
            {
                dayTx.text = day;
                // 오늘 요일만 강조 (목업에서 '목'이 보라색)
                dayTx.color = isToday ? DesignTokens.Text.Point : DesignTokens.Text.Tertiary;
            }
        }
    }
}