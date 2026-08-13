using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 화면 전환 관리자. 한 씬 안의 여러 화면(패널)을 켜고 끈다.
    // 뒤로가기(스택) 지원. 어디서든 ScreenManager.Instance.Show("Mission") 로 전환.
    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager Instance { get; private set; }

        [SerializeField] List<Screen> screens = new List<Screen>();
        [SerializeField] string startScreen = "Home";

        readonly Stack<string> history = new Stack<string>();
        string current;

        // 지금 켜져 있는 화면 id
        public string Current => current;
        // 화면이 바뀔 때마다 새 id 를 알려준다. (AR 흐름 제어에 사용)
        public event System.Action<string> ScreenChanged;

        [System.Serializable]
        public class Screen
        {
            public string id;         // "Home", "Mission" ...
            public GameObject root;    // 그 화면의 최상위 오브젝트
        }

        void Awake()
        {
            Instance = this;
            foreach (var s in screens)
                if (s.root != null) s.root.SetActive(s.id == startScreen);
            current = startScreen;
            history.Clear();
        }

        void Start()
        {
            // Awake 시점엔 구독자가 아직 없으므로 한 프레임 뒤에 최초 상태를 알린다.
            ScreenChanged?.Invoke(current);
        }

        // 새 화면으로 이동 (뒤로가기 기록됨)
        public void Show(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("[ScreenManager] 빈 화면 id 로 Show 호출됨 — 무시함");
                return;
            }
            if (id == current) return;
            if (!string.IsNullOrEmpty(current)) history.Push(current);
            Activate(id);
        }

        // 뒤로가기
        public void Back()
        {
            // 기록이 없으면(그 화면으로 직접 들어온 경우) 시작 화면으로 — 먹통이 되지 않게
            if (history.Count == 0)
            {
                if (current != startScreen) Activate(startScreen);
                return;
            }
            Activate(history.Pop());
        }

        void Activate(string id)
        {
            bool found = false;
            foreach (var s in screens)
            {
                if (s.root == null) continue;
                bool on = s.id == id;
                s.root.SetActive(on);
                if (on) found = true;
            }
            if (!found)
                Debug.LogError($"[ScreenManager] '{id}' 에 해당하는 화면이 없음 — 전부 꺼졌다");

            current = id;
            ScreenChanged?.Invoke(id);
        }
    }
}