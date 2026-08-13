using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

namespace ARZone
{
    // 스테이지 상세. 선택된 항목으로 정보·인라인 플레이어·추천 목록을 채운다.
    public class StageDetailController : MonoBehaviour
    {
        [Header("정보")]
        [SerializeField] TMP_Text infoTitle;
        [SerializeField] TMP_Text infoSub;
        [SerializeField] TMP_Text infoDate;

        [Header("인라인 플레이어")]
        [SerializeField] RawImage videoSurface;    // Player 밑에 새로 만든 RawImage
        [SerializeField] Button btnPlay;
        [SerializeField] TMP_Text timeText;        // "1:14 / 3:42"
        [SerializeField] Image progressFill;
        [SerializeField] RenderTexture renderTexture;

        [Header("추천 동영상")]
        [SerializeField] GameObject rowPrefab;     // VideoRow 프리팹
        [SerializeField] Transform recommendContainer;

        VideoPlayer player;
        StageData data;

        void Awake()
        {
            player = GetComponent<VideoPlayer>();
            if (player == null) player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = renderTexture;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
        }

        void OnEnable()
        {
            if (btnPlay != null) btnPlay.onClick.AddListener(TogglePlay);
            Refresh();
        }

        void OnDisable()
        {
            if (btnPlay != null) btnPlay.onClick.RemoveListener(TogglePlay);
            if (player != null) player.Pause();
        }

        void Refresh()
        {
            if (StageCatalog.Instance == null) { Debug.LogError("[StageDetail] StageCatalog 없음"); return; }

            data = StageCatalog.Instance.Selected;
            if (data == null) { Debug.LogWarning("[StageDetail] 선택된 스테이지가 없음"); return; }

            if (infoTitle != null) infoTitle.text = data.title;
            if (infoSub != null) infoSub.text = data.sub;
            if (infoDate != null) infoDate.text = data.date;

            if (videoSurface != null && renderTexture != null) videoSurface.texture = renderTexture;

            // RenderTexture 는 비워지기 전까지 흰색이라, 재생 전에는 감춰서
            // 뒤에 깔린 포스터(Player 의 Image)가 보이게 한다.
            ShowVideoSurface(false);

            if (player != null)
            {
                player.Stop();
                player.clip = data.clip;
                if (data.clip != null) player.Prepare();
            }
            UpdateProgress();

            BuildRecommend();
        }

        void TogglePlay()
        {
            if (player == null || player.clip == null) return;
            if (player.isPlaying) player.Pause(); else player.Play();
        }

        void Update()
        {
            if (player != null && player.clip != null) UpdateProgress();
            // 첫 프레임이 실제로 그려진 뒤에만 영상 면을 보여준다
            ShowVideoSurface(player != null && player.frame > 0);
        }

        void ShowVideoSurface(bool on)
        {
            if (videoSurface == null) return;
            var c = videoSurface.color;
            float a = on ? 1f : 0f;
            if (!Mathf.Approximately(c.a, a)) videoSurface.color = new Color(c.r, c.g, c.b, a);
        }

        void UpdateProgress()
        {
            double len = (player != null && player.clip != null) ? player.clip.length : 0d;
            double now = (player != null) ? player.time : 0d;

            if (timeText != null) timeText.text = $"{Fmt(now)} / {Fmt(len)}";
            if (progressFill != null)
            {
                var rt = progressFill.rectTransform;
                float p = len > 0.001d ? Mathf.Clamp01((float)(now / len)) : 0f;
                rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
                rt.anchorMax = new Vector2(p, rt.anchorMax.y);
                rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
                rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
            }
        }

        static string Fmt(double sec)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt((float)sec));
            return $"{s / 60}:{s % 60:00}";
        }

        void BuildRecommend()
        {
            if (rowPrefab == null || recommendContainer == null) return;

            EnsureLayout(recommendContainer, 36f);   // 시안: 행 80, 다음 행까지 116 → 간격 36

            UiUtil.ClearChildren(recommendContainer);

            foreach (var s in StageCatalog.Instance.Recommend(data))
            {
                var row = Instantiate(rowPrefab, recommendContainer);
                row.SetActive(true);

                Set<TMP_Text>(row, "Title", t => t.text = s.title);
                Set<TMP_Text>(row, "Desc", t => t.text = s.sub);
                Set<TMP_Text>(row, "Date", t => t.text = s.date);
                if (s.thumb != null) Set<Image>(row, "Thumb", i => i.sprite = s.thumb);

                // 시안: 제목+부제 그룹이 행(80) 세로 중앙. 제목이 2줄이면 부제가 밀린다.

                UiUtil.CenterTitleDesc(row, "Title", "Desc", 14.5f);


                var btn = row.GetComponent<Button>();
                if (btn == null) btn = row.AddComponent<Button>();
                var captured = s;
                btn.onClick.AddListener(() =>
                {
                    StageCatalog.Instance.Select(captured);
                    Refresh();       // 화면 이동 없이 그 자리에서 교체
                });
            }
        }

        // Content 에 레이아웃이 없으면 코드로 붙인다. (씬에서 비워둔 상태)
        static void EnsureLayout(Transform content, float spacing)
        {
            if (content == null) return;

            var v = content.GetComponent<VerticalLayoutGroup>();
            if (v == null) v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlWidth = true;
            v.childControlHeight = false;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperCenter;

            var f = content.GetComponent<ContentSizeFitter>();
            if (f == null) f = content.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        static void Set<T>(GameObject root, string child, System.Action<T> apply) where T : Component
        {
            var t = root.transform.Find(child);
            if (t == null) return;
            var c = t.GetComponent<T>();
            if (c != null) apply(c);
        }
    }
}