using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 탭바의 선택 상태를 아이콘으로 표시하고, 누르면 화면을 전환한다.
    // 탭바가 화면마다 따로 있으므로 각 화면에서 activeIndex 만 다르게 지정한다.
    public class TabBarController : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            public Image icon;
            public Sprite on;
            public Sprite off;
            public string targetScreen;   // 비우면 이동하지 않음
        }

        [SerializeField] List<Tab> tabs = new List<Tab>();
        [SerializeField] int activeIndex = 0;   // 이 화면에서 켜져 있을 탭

        [Header("통짜 PNG 방식 (아이콘이 배경에 구워진 경우)")]
        [SerializeField] Image pill;              // 탭바 배경
        [SerializeField] List<Sprite> pillStates; // 탭 순서대로 활성 상태 이미지

        void OnEnable() => Apply();

        void Start()
        {
            foreach (var t in tabs)
            {
                if (t.icon == null || string.IsNullOrEmpty(t.targetScreen)) continue;

                var btn = t.icon.GetComponent<Button>();
                if (btn == null) btn = t.icon.gameObject.AddComponent<Button>();

                string id = t.targetScreen;                  // 클로저 캡처 주의
                btn.onClick.AddListener(() => ScreenManager.Instance?.Show(id));
            }
        }

        void Apply()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                var t = tabs[i];
                if (t.icon == null) continue;

                var s = (i == activeIndex) ? t.on : t.off;
                if (s != null) t.icon.sprite = s;
            }

            // 아이콘이 배경에 구워진 에셋이면 배경 자체를 교체한다
            if (pill != null && pillStates != null &&
                activeIndex >= 0 && activeIndex < pillStates.Count && pillStates[activeIndex] != null)
                pill.sprite = pillStates[activeIndex];
        }
    }
}