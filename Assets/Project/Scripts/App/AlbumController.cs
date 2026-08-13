using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 앨범 화면. 트랙 목록은 TrackRow 프리팹으로 런타임에 만든다.
    // 백엔드 API 가 오기 전까지는 아래 더미 목록을 쓴다(시안 문구 그대로).
    public class AlbumController : MonoBehaviour
    {
        [System.Serializable]
        public class Track
        {
            public string title;
            public string artist;
            public string duration;      // 음원이 없을 때만 쓰는 시안 값
            public string clipKey;       // Resources/Audio/<clipKey>.mp3 — 있으면 길이를 파일에서 읽는다
            public Track(string t, string a, string d, string k = "")
            { title = t; artist = a; duration = d; clipKey = k; }

            AudioClip cached; bool tried;
            public AudioClip Clip
            {
                get
                {
                    if (!tried) { tried = true; if (!string.IsNullOrEmpty(clipKey)) cached = Resources.Load<AudioClip>("Audio/" + clipKey); }
                    return cached;
                }
            }

            // 음원이 있으면 실제 길이 — 재생바와 어긋나지 않는다
            public string Length
            {
                get
                {
                    var c = Clip;
                    if (c == null) return duration;
                    int s = Mathf.RoundToInt(c.length);
                    return (s / 60) + ":" + (s % 60).ToString("00");
                }
            }
        }

        [Header("헤더")]
        [SerializeField] TMP_Text albumTitle;
        [SerializeField] TMP_Text albumMeta;

        [Header("목록")]
        [SerializeField] RectTransform trackList;
        [SerializeField] GameObject rowPrefab;
        [SerializeField] float rowHeight = 89f;   // 앨범.svg 실측

        [Header("버튼")]
        [SerializeField] Button btnPlayAll;
        [SerializeField] string detailScreen = "MusicDetail";

        [Header("데이터 (임시 — API 오면 교체)")]
        [SerializeField] string title = "Ruby";
        [SerializeField] string year = "2025";   // 시안 형식: "15 Tracks · 41 min · 2025"
        [SerializeField]
        List<Track> tracks = new List<Track>
        {
            // 음원이 있는 9곡은 clipKey 로 길이를 파일에서 읽는다.
            // 나머지 6곡의 duration 은 시안 앨범.svg 에서 읽은 값이고,
            // Love Hangover / ZEN / Filter 는 시안에서 스크롤 밖이라 값이 없다(음원도 아직 없음).
            new Track("Intro : JANE (with FKJ)", "Jennie", "1:38", "intro_jane"),
            new Track("like JENNIE", "Jennie", "2:03"),
            new Track("start a war", "Jennie", "2:45", "start_a_war"),
            new Track("Handlebars (feat. Dua Lipa)", "Jennie", "3:04", "handlebars"),
            new Track("with the IE (way up)", "Jennie", "2:43", "with_the_ie"),
            new Track("ExtraL (feat. Doechii)", "Jennie", "2:47"),
            new Track("Mantra", "Jennie", "2:47"),
            new Track("Love Hangover (feat. Dominic Fike)", "Jennie", ""),
            new Track("ZEN", "Jennie", ""),
            new Track("Damn Right (feat. Childish Gambino & Kali Uchis)", "Jennie", "", "damn_right"),
            new Track("F.T.S.", "Jennie", "", "fts"),
            new Track("Filter", "Jennie", ""),
            new Track("Seoul City", "Jennie", "", "seoul_city"),
            new Track("Starlight", "Jennie", "", "starlight"),
            new Track("twin", "Jennie", "", "twin"),
        };

        // 지금 재생 중인 트랙 (아이콘을 웨이브폼으로 표시). -1 이면 없음.
        int playing = -1;

        void OnEnable()
        {
            if (btnPlayAll != null) btnPlayAll.onClick.AddListener(OnPlayAll);
            Refresh();
        }

        void OnDisable()
        {
            if (btnPlayAll != null) btnPlayAll.onClick.RemoveListener(OnPlayAll);
        }

        void Refresh()
        {
            if (albumTitle != null) albumTitle.text = title;
            if (albumMeta != null) albumMeta.text = BuildMeta();
            Build();
        }

        void Build()
        {
            if (trackList == null || rowPrefab == null) return;

            // Destroy 는 프레임 끝에 처리돼 옛 행과 새 행이 겹친다(재생중 아이콘이 엉뚱한 줄에 보였음)
            for (int k = trackList.childCount - 1; k >= 0; k--)
                DestroyImmediate(trackList.GetChild(k).gameObject);

            for (int i = 0; i < tracks.Count; i++)
            {
                var row = Instantiate(rowPrefab, trackList);
                row.SetActive(true);
                row.name = "TrackRow" + i;

                var rt = row.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                // stretch 앵커에선 sizeDelta.x 가 '부모폭 + 이 값' 이다.
                // 프리팹의 372 를 그대로 두면 폭이 744 가 되어 글자가 왼쪽으로 밀려 잘린다.
                rt.sizeDelta = new Vector2(0f, rowHeight);
                rt.anchoredPosition = new Vector2(0f, -rowHeight * i);

                var t = tracks[i];
                Set<TMP_Text>(row, "Title", x => x.text = t.title);
                Set<TMP_Text>(row, "Artist", x => x.text = t.artist);
                Set<TMP_Text>(row, "Duration", x => x.text = t.Length);

                // 시안: 재생 중인 행만 파형, 나머지는 트랙 번호 — 둘이 같은 자리(행 기준 x 12.25 중심)
                bool isPlaying = i == playing;
                Set<Image>(row, "Icon", x =>
                {
                    var c = x.color;
                    x.color = new Color(c.r, c.g, c.b, isPlaying ? 1f : 0f);
                });
                var num = row.transform.Find("Number");
                if (num != null)
                {
                    num.gameObject.SetActive(!isPlaying);
                    var nt = num.GetComponent<TMP_Text>();
                    if (nt != null) nt.text = (i + 1).ToString();
                }

                // 마지막 행은 구분선 없음 (시안)
                bool last = i == tracks.Count - 1;
                var div = row.transform.Find("Divider");
                if (div != null) div.gameObject.SetActive(!last);

                int index = i;
                var btn = row.GetComponent<Button>();
                if (btn == null) btn = row.AddComponent<Button>();
                btn.onClick.AddListener(() => OnTrack(index));
            }

            // 컨테이너 높이를 실제 행 수에 맞춘다
            trackList.sizeDelta = new Vector2(trackList.sizeDelta.x, rowHeight * tracks.Count);
        }

        void OnPlayAll() => OnTrack(0);

        // 시안 형식 "15 Tracks · 41 min · 2025" — 곡 수와 분은 실제 값에서 낸다.
        // 음원이 아직 없는 곡은 시안에 적힌 길이를 쓰고, 그것도 없으면 합계에서 빠진다.
        string BuildMeta()
        {
            int sec = 0, unknown = 0;
            foreach (var t in tracks)
            {
                int s = TrackSeconds(t);
                if (s > 0) sec += s; else unknown++;
            }
            string min = (sec / 60) + " min";
            if (unknown > 0) min += "+";     // 길이를 모르는 곡이 있으면 '이상' 표시
            return tracks.Count + " Tracks · " + min + " · " + year;
        }

        static int TrackSeconds(Track t)
        {
            var c = t.Clip;
            if (c != null) return Mathf.RoundToInt(c.length);
            var parts = (t.duration ?? "").Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int s))
                return m * 60 + s;
            return 0;
        }

        void OnTrack(int index)
        {
            playing = index;
            Build();
            // 트랙을 고르면 상세(플레이어)로 넘긴다
            var t = tracks[index];
            // 목록 전체를 넘겨야 '다음 곡'과 이전/다음 이동이 맞는다
            var list = new List<MusicDetailController.Track>();
            foreach (var x in tracks)
                list.Add(new MusicDetailController.Track { title = x.title, artist = x.artist, clipKey = x.clipKey,
                                                          lyricsKey = string.IsNullOrEmpty(x.clipKey) ? "" : "Lyrics/" + x.clipKey });
            MusicDetailController.PendingTracks = list;
            MusicDetailController.PendingIndex = index;
            MusicDetailController.PendingTitle = t.title;
            MusicDetailController.PendingArtist = t.artist;
            MusicDetailController.PendingClipKey = t.clipKey;
            if (ScreenManager.Instance != null && !string.IsNullOrEmpty(detailScreen))
                ScreenManager.Instance.Show(detailScreen);
        }

        static void Set<T>(GameObject root, string child, System.Action<T> apply) where T : Component
        {
            var tr = root.transform.Find(child);
            if (tr == null) return;
            var c = tr.GetComponent<T>();
            if (c != null) apply(c);
        }
    }
}
