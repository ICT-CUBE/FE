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
    //
    // 2026-08-24 수정 — 실기에서 나온 문제 넷:
    //   ① 진입하면 노래가 멈춤     → AudioSource 소유자를 MusicPlayer 로 옮김
    //   ② 재생 버튼이 안 먹음      → 버튼이 아예 배선돼 있지 않았다(Image 만 있었다)
    //   ③ 긴 줄이 서로 겹침        → 고정 50px 피치 → 실제 줄 높이 누적으로
    //   ④ 제목이 'Mantra' 고정     → SetTrack() 을 부르는 곳이 없었다
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
        [SerializeField] Button btnPlay;         // BtnLyricPlay — Button 컴포넌트를 붙여서 연결할 것
        [SerializeField] Image iconPlay;         // BtnLyricPlay 의 Image
        [SerializeField] Sprite icoPlay;         // ic_play_tri.png
        [SerializeField] Sprite icoPause;        // ic_pause.svg (비워두면 아이콘을 안 바꾼다)

        [Header("가사 목록")]
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] float firstLineY = 4f;       // 첫 줄 위 여백
        [SerializeField] float lineGap = 50f;         // 시안 실측 — '한 줄짜리' 가사의 줄 간격
        [SerializeField] float lineX = 21.57f;        // 시안: 절대 41.57 - 좌여백 20
        [SerializeField] TMP_FontAsset font;

        [Header("데이터")]
        [SerializeField] string resourceKey = "";     // Resources 경로(확장자 없이)
        [SerializeField] TextAsset lyricsAsset;       // 직접 물려도 된다

        static readonly Regex Stamp = new Regex(@"^\s*\[(\d+):(\d+(?:[.:]\d+)?)\]\s*(.*)$");

        readonly List<Line> lines = new List<Line>();
        readonly List<TMP_Text> views = new List<TMP_Text>();
        readonly List<float> tops = new List<float>();     // 각 줄의 상단 y(양수, 아래로 증가)
        readonly List<float> heights = new List<float>();
        int active = -1;
        float builtWidth = -1f;                            // 어떤 폭으로 배치했는지

        // 어느 곡인지 — 여는 쪽에서 정해둔다
        public static string PendingKey;

        void OnEnable()
        {
            if (!string.IsNullOrEmpty(PendingKey)) resourceKey = PendingKey;

            if (btnPlay != null) btnPlay.onClick.AddListener(TogglePlay);

            var p = MusicPlayer.Instance;
            if (p != null)
            {
                if (trackTitle != null && !string.IsNullOrEmpty(p.TrackTitle)) trackTitle.text = p.TrackTitle;
                if (trackArtist != null && !string.IsNullOrEmpty(p.TrackArtist)) trackArtist.text = p.TrackArtist;
            }

            // ★ Build() 보다 먼저 비운다. 안 그러면 '이전 곡'의 강조 줄 번호가 그대로 남아
            //   새 곡에서 엉뚱한 줄을 강조하고 그리로 스크롤한다.
            active = -1;

            Load();
            Build();
            Sync(p != null ? p.Time : 0f);      // 들어오자마자 현재 위치에 맞춰 놓는다
        }

        void OnDisable()
        {
            if (btnPlay != null) btnPlay.onClick.RemoveListener(TogglePlay);
        }

        void TogglePlay()
        {
            var p = MusicPlayer.Instance;
            if (p == null || p.Clip == null)
            {
                Debug.LogWarning("[Lyrics] 재생할 음원이 없다 — 음악 상세에서 곡을 고른 뒤 들어와야 한다");
                return;
            }
            p.Toggle();
        }

        void Update()
        {
            var p = MusicPlayer.Instance;
            if (p == null) return;

            Sync(p.Time);

            if (iconPlay != null && icoPause != null)
            {
                var want = p.IsPlaying ? icoPause : icoPlay;
                if (want != null && iconPlay.sprite != want) iconPlay.sprite = want;
            }

            // 화면이 켜진 첫 프레임엔 rect 폭이 0이라 줄바꿈 계산이 틀린다.
            // 폭이 확정되면 한 번 다시 배치한다.
            float w = Width();
            if (w > 1f && !Mathf.Approximately(w, builtWidth))
            {
                Layout();
                // 다시 배치하면 content 높이가 바뀐다 → 스크롤 비율이 가리키는 곳도 달라진다.
                // 강조 중인 줄이 있으면 그 줄로, 없으면 맨 위로 다시 맞춘다.
                if (active >= 0) ScrollTo(active); else ScrollTop();
            }
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

            SortByTime();
        }

        // ★ 예전엔 List.Sort 에 "한쪽이라도 시간이 없으면 0(같음)" 을 돌려주는 비교자를 썼다.
        //   그건 앞뒤가 안 맞는 규칙이라(A=B, B=C 인데 A≠C) 줄 순서가 뒤섞일 수 있고,
        //   .NET 은 이런 비교자를 만나면 예외를 던지기도 한다.
        //   → 시간이 있는 줄만 시간순으로 재배치하고, 없는 줄은 있던 자리에 그대로 둔다.
        void SortByTime()
        {
            var stampedIdx = new List<int>();
            var stamped = new List<Line>();
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].time >= 0f) { stampedIdx.Add(i); stamped.Add(lines[i]); }

            stamped.Sort((a, b) => a.time.CompareTo(b.time));
            for (int k = 0; k < stampedIdx.Count; k++) lines[stampedIdx[k]] = stamped[k];
        }

        float Width()
        {
            float w = content != null ? content.rect.width : 0f;
            return w - lineX * 2f;
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

                var t = go.GetComponent<TextMeshProUGUI>();
                var src = font != null ? null : trackTitle;
                if (font != null) t.font = font;
                else if (src != null && src.font != null)
                { t.font = src.font; t.fontSharedMaterial = src.fontSharedMaterial; }
                t.text = lines[i].text;
                t.fontSize = 24f;                       // 시안 실측(글리프 높이 22.69)
                t.alignment = TextAlignmentOptions.TopLeft;
                t.textWrappingMode = TextWrappingModes.Normal;
                t.raycastTarget = false;
                t.color = DesignTokens.Text.Tertiary;
                views.Add(t);
            }

            Layout();

            // ★ 스크롤을 맨 위로 되돌린다.
            //   ScrollRect 는 '비율'(verticalNormalizedPosition)을 들고 있어서, 줄만 갈아끼우면
            //   이전 곡에서 내려가 있던 비율이 그대로 남는다. 새 곡이 더 짧으면 그 비율이
            //   엉뚱한 중간을 가리킨다 → "다른 곡 듣다 돌아오면 가사가 중간부터 나온다".
            //   곡이 이미 진행 중이면 바로 뒤에 오는 Sync() 가 알맞은 줄로 다시 옮겨준다.
            ScrollTop();
        }

        void ScrollTop()
        {
            if (scroll == null) return;
            scroll.StopMovement();          // 관성이 남아 있으면 되돌려놔도 다시 흘러간다
            scroll.verticalNormalizedPosition = 1f;
        }

        // ★ 줄마다 실제로 차지하는 높이를 재서 누적으로 쌓는다.
        //   예전엔 모든 줄을 50px 간격에 놓고 박스 높이도 50 으로 고정해서,
        //   두세 줄로 접히는 긴 가사가 아랫줄을 파고들었다(실기에서 글자가 겹쳐 보인 원인).
        void Layout()
        {
            if (content == null || views.Count == 0) return;

            float w = Width();
            if (w <= 1f) return;            // 아직 폭이 안 잡혔다 — Update 에서 다시 부른다
            builtWidth = w;

            tops.Clear(); heights.Clear();
            float y = firstLineY;

            // ★ 시안 실측(가사 진행.svg): 줄 피치 50 = 글자상자(≈34) + 간격 16.
            //   접힌 줄(2줄 이상)에도 같은 '간격 16'이 붙어야 한다 — 실제 높이만 잡으면
            //   다음 가사와 붙어 보인다. 간격은 하드코딩하지 않고 시안 피치에서 역산한다:
            //   간격 = lineGap(50) − 한 줄 글자상자 높이.
            float singleH = views.Count > 0 ? views[0].GetPreferredValues("가", w, 0f).y : 0f;
            float lineSpace = Mathf.Max(0f, lineGap - singleH);

            for (int i = 0; i < views.Count; i++)
            {
                var t = views[i];
                // 이 폭에서 이 글자가 실제로 몇 픽셀을 쓰는지 물어본다(줄바꿈 반영).
                float h = t.GetPreferredValues(t.text, w, 0f).y + lineSpace;
                h = Mathf.Max(h, lineGap);   // 한 줄짜리는 정확히 시안 피치 50 이 된다

                var rt = t.rectTransform;
                rt.anchoredPosition = new Vector2(lineX, -y);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);

                tops.Add(y); heights.Add(h);
                y += h;
            }

            content.sizeDelta = new Vector2(content.sizeDelta.x, y + firstLineY);
        }

        // 재생 시간(초)을 받아 현재 줄을 갱신한다.
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
            if (idx < 0 || idx >= tops.Count) return;

            float viewH = scroll.viewport != null ? scroll.viewport.rect.height : 600f;
            float total = content.rect.height;
            if (total <= viewH) return;

            float target = tops[idx] - viewH / 3f;
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(target / (total - viewH));
        }

        public void SetTrack(string title, string artist)
        {
            if (trackTitle != null) trackTitle.text = title;
            if (trackArtist != null) trackArtist.text = artist;
        }
    }
}
