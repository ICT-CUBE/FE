using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 가사 화면. LRC 형식 텍스트를 읽어 재생 시간에 맞춰 현재 줄을 강조하고 따라 스크롤한다.
    // 가사 파일은 Resources/Lyrics/<곡키>.txt 에 두고, 한 줄에 [mm:ss.xx] 뒤에 가사를 적는다.
    // 타임스탬프가 없는 줄은 순서대로 표시만 되고 싱크는 안 된다.
    public class LyricsController : MonoBehaviour
    {
        public struct Line
        {
            public float time;      // 초. 음수면 싱크 없음
            public string text;
        }

        [Header("상단 미니 플레이어")]
        [SerializeField] TMP_Text trackTitle;
        [SerializeField] TMP_Text trackArtist;

        [Header("가사 목록")]
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] float firstLineY = 4f;       // 박스 높이를 lineGap 으로 키웠으므로 글자 중심은 시안과 같아진다
        [SerializeField] float lineGap = 50f;         // 시안 실측
        [SerializeField] float lineX = 21.57f;        // 시안: 절대 41.57 - 좌여백 20
        [SerializeField] TMP_FontAsset font;

        [Header("데이터")]
        [SerializeField] string resourceKey = "Lyrics/mantra";   // Resources 경로(확장자 없이)
        [SerializeField] TextAsset lyricsAsset;                  // 직접 물려도 된다

        static readonly Regex Stamp = new Regex(@"^\s*\[(\d+):(\d+(?:[.:]\d+)?)\]\s*(.*)$");

        readonly List<Line> lines = new List<Line>();
        readonly List<TMP_Text> views = new List<TMP_Text>();
        int active = -1;

        // 어느 곡인지 — 여는 쪽에서 정해둔다
        public static string PendingKey;

        void OnEnable()
        {
            if (!string.IsNullOrEmpty(PendingKey)) resourceKey = PendingKey;
            Load();
            Build();
            Highlight(0);
        }

        void Load()
        {
            lines.Clear();
            var asset = lyricsAsset;
            if (asset == null && !string.IsNullOrEmpty(resourceKey))
                asset = Resources.Load<TextAsset>(resourceKey);

            if (asset == null)
            {
                Debug.LogWarning($"[Lyrics] 가사 파일 없음: Resources/{resourceKey}.txt");
                return;
            }

            foreach (var raw in asset.text.Split('\n'))
            {
                var s = raw.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(s) || s.TrimStart().StartsWith("#")) continue;

                var m = Stamp.Match(s);
                if (m.Success)
                {
                    float mm = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    float ss = float.Parse(m.Groups[2].Value.Replace(':', '.'), CultureInfo.InvariantCulture);
                    lines.Add(new Line { time = mm * 60f + ss, text = m.Groups[3].Value });
                }
                else
                {
                    lines.Add(new Line { time = -1f, text = s });
                }
            }
            lines.Sort((a, b) => (a.time < 0 || b.time < 0) ? 0 : a.time.CompareTo(b.time));
        }

        void Build()
        {
            if (content == null) return;
            UiUtil.ClearChildren(content);
            views.Clear();

            for (int i = 0; i < lines.Count; i++)
            {
                var go = new GameObject("Line" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                go.transform.SetParent(content, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.offsetMin = new Vector2(lineX, rt.offsetMin.y);
                rt.offsetMax = new Vector2(-lineX, rt.offsetMax.y);
                rt.anchoredPosition = new Vector2(lineX, -(firstLineY + lineGap * i));
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, lineGap);   // 24px 글자가 30 박스를 넘어 윗줄이 잘렸다

                var t = go.GetComponent<TextMeshProUGUI>();
                var src = font != null ? null : trackTitle;
                if (font != null) t.font = font;
                else if (src != null && src.font != null)
                { t.font = src.font; t.fontSharedMaterial = src.fontSharedMaterial; }
                t.text = lines[i].text;
                t.fontSize = 24f;                       // 시안 실측(글리프 높이 22.69)
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.raycastTarget = false;
                t.color = DesignTokens.Text.Tertiary;
                views.Add(t);
            }

            content.sizeDelta = new Vector2(content.sizeDelta.x, firstLineY + lineGap * Mathf.Max(1, lines.Count));
        }

        // 재생 시간(초)을 받아 현재 줄을 갱신한다. 플레이어가 매 프레임 호출한다.
        public void Sync(float seconds)
        {
            int idx = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].time < 0f) continue;
                if (lines[i].time <= seconds) idx = i; else break;
            }
            if (idx >= 0 && idx != active) Highlight(idx);
        }

        void Highlight(int idx)
        {
            if (views.Count == 0) return;
            idx = Mathf.Clamp(idx, 0, views.Count - 1);
            for (int i = 0; i < views.Count; i++)
                views[i].color = i == idx ? DesignTokens.Text.Primary : DesignTokens.Text.Tertiary;
            active = idx;
            ScrollTo(idx);
        }

        // 현재 줄이 화면 위쪽 1/3 지점에 오도록 (시안: 진행되면 위로 밀린다)
        void ScrollTo(int idx)
        {
            if (scroll == null || content == null) return;
            float viewH = scroll.viewport != null ? scroll.viewport.rect.height : 600f;
            float total = content.rect.height;
            if (total <= viewH) return;

            float target = firstLineY + lineGap * idx - viewH / 3f;
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(target / (total - viewH));
        }

        public void SetTrack(string title, string artist)
        {
            if (trackTitle != null) trackTitle.text = title;
            if (trackArtist != null) trackArtist.text = artist;
        }
    }
}
