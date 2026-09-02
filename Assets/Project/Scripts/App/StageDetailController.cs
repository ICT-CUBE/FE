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
        [Header("재생/일시정지 아이콘 (2026-08-30)")]
        [SerializeField] Sprite icoPlay;           // ic_play_tri
        [SerializeField] Sprite icoPause;          // ic_pause_bar
        [SerializeField] Image progressFill;
        [SerializeField] RenderTexture renderTexture;

        [Header("추천 동영상")]
        [SerializeField] GameObject rowPrefab;     // VideoRow 프리팹
        [SerializeField] Transform recommendContainer;

        VideoPlayer player;
        StageData data;

        // ★ 플레이어는 화면 오브젝트가 아니라 상시 살아있는 호스트에 둔다 (2026-08-30) —
        //   화면 루트에 붙였더니 전체화면(Screen_StagePlayer)으로 넘어가는 순간 화면과 함께
        //   꺼져서 재생이 죽었다. 전체화면(StagePlayerInfo)도 이 공유 플레이어를 쓴다.
        static VideoPlayer shared;
        public static VideoPlayer Shared => shared;

        // 같은 무대로 되돌아오면(전체화면↔상세) 소스를 다시 세팅하지 않는다 — 재생이 안 끊기게
        static StageData loadedStage;

        void Awake()
        {
            if (shared == null)
            {
                var host = new GameObject("StageVideoHost");
                shared = host.AddComponent<VideoPlayer>();
            }
            player = shared;
            player.playOnAwake = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = renderTexture;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.errorReceived -= OnVideoError;
            player.errorReceived += OnVideoError;
        }

        // 무대 흐름(상세·전체화면)을 완전히 벗어날 때만 멈추고 세션을 닫는다.
        // OnDisable 에서 하면 전체화면으로 '확대'하는 순간에도 꺼져버린다.
        bool subscribed;

        void TrySubscribe()
        {
            if (subscribed || ScreenManager.Instance == null) return;
            ScreenManager.Instance.ScreenChanged += OnScreenChanged;
            subscribed = true;
        }

        void OnScreenChanged(string id)
        {
            if (id == "StageDetail" || id == "StagePlayer") return;
            if (player != null && player.isPlaying) player.Pause();
            ApiMediaSource.EndStage(player != null ? (float)player.time : 0f,
                                    player != null ? (float)player.time : 0f);
            loadedStage = null;   // 다음 진입 땐 처음부터 세팅한다
        }

        void OnDestroy()
        {
            if (player != null) player.errorReceived -= OnVideoError;
            if (subscribed && ScreenManager.Instance != null)
                ScreenManager.Instance.ScreenChanged -= OnScreenChanged;
        }

        // 서버 URL 재생이 기기에서 실패하면(코덱·네트워크) 로컬 클립으로 되돌린다.
        // 08-26 스트리밍 전환이 진입 즉시 소스를 URL 로 바꾸는 구조라, 이 복귀가 없으면
        // URL 이 안 트일 때 화면이 빈 채로 남는다 (2026-08-28 실기: 무대 영상 흰 화면).
        void OnVideoError(VideoPlayer vp, string message)
        {
            bool canFallback = data != null && data.clip != null && vp != null;
            Debug.LogWarning($"[StageDetail] 영상 재생 실패: {message}" +
                             (canFallback ? " — 로컬 클립으로 복귀" : " (로컬 클립도 없음)"));
            if (!canFallback) return;
            vp.Stop();
            vp.source = VideoSource.VideoClip;
            vp.clip = data.clip;
            vp.Prepare();
            vp.Play();
        }

        void OnEnable()
        {
            if (btnPlay != null) btnPlay.onClick.AddListener(TogglePlay);
            else Debug.LogWarning("[StageDetail] btnPlay 참조가 비어 있음");
            TrySubscribe();
            EnsureTapCatcher();
            Refresh();
            SetControls(true);
        }

        void OnDisable()
        {
            if (btnPlay != null) btnPlay.onClick.RemoveListener(TogglePlay);
            // 멈춤·세션 마감은 OnScreenChanged 가 처리한다 — 전체화면으로 확대할 땐 계속 재생돼야 한다
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

            // 전체화면↔상세를 오간 것뿐이면 소스를 다시 세팅하지 않는다 — 재생이 안 끊기게
            bool sameStage = loadedStage == data && HasSource;

            // RenderTexture 는 비워지기 전까지 흰색이라, 재생 전에는 감춰서
            // 뒤에 깔린 포스터(Player 의 Image)가 보이게 한다.
            if (!sameStage) ShowVideoSurface(false);

            if (player != null && !sameStage)
            {
                loadedStage = data;
                pendingPlay = false;
                player.Stop();
                player.source = UnityEngine.Video.VideoSource.VideoClip;   // 이전 URL 전환 흔적 초기화
                player.clip = data.clip;
                if (data.clip != null) player.Prepare();

                // 서버 스트리밍 (2026-08-26 전환) — URL 이 잡히면 소스를 URL 로 바꾼다.
                // 목록·상세가 캐시돼 있으면 콜백이 동기라 재생 전에 끝난다. 실패 시 로컬 유지.
                var d = data;
                ApiMediaSource.GetStageVideoUrl(d.title, url =>
                {
                    if (string.IsNullOrEmpty(url) || player == null || data != d) return;
                    if (player.isPlaying) return;                  // 이미 로컬로 트는 중 — 끊지 않는다
                    player.Stop();
                    player.source = UnityEngine.Video.VideoSource.Url;
                    player.url = url;
                    player.Prepare();
                    Debug.Log($"[StageDetail] '{d.title}' 서버 영상으로 전환");
                    if (pendingPlay) PlayNow();   // 소스 없던 사이 눌린 재생을 이제 실행
                });
            }
            UpdateProgress();

            BuildRecommend();
        }

        // URL 소스로 전환되면 clip 은 null 이다 — 둘 중 하나만 있으면 재생 가능.
        bool HasSource => player != null &&
                          (player.clip != null || !string.IsNullOrEmpty(player.url));

        bool pendingPlay;   // 소스가 아직 없을 때 눌린 재생 — 서버 URL 이 잡히면 바로 재생한다

        // ── 재생 컨트롤 표시 (2026-08-30 유저 지시) ──
        // 재생 중엔 3초 뒤 재생/일시정지 버튼을 숨기고, 화면(영상 영역)을 탭하면 토글한다.
        float hideAt = -1f;

        void SetControls(bool on)
        {
            if (btnPlay != null && btnPlay.gameObject.activeSelf != on)
                btnPlay.gameObject.SetActive(on);
            hideAt = on && player != null && player.isPlaying ? Time.time + 3f : -1f;
        }

        void ToggleControls()
            => SetControls(btnPlay != null && !btnPlay.gameObject.activeSelf);

        // 재생 중엔 일시정지 아이콘, 멈춰 있으면 재생 아이콘
        void SyncIcon()
        {
            if (btnPlay == null || btnPlay.image == null || icoPlay == null || icoPause == null) return;
            var want = player != null && player.isPlaying ? icoPause : icoPlay;
            if (btnPlay.image.sprite != want) btnPlay.image.sprite = want;
        }

        // 영상 영역(포스터가 붙은 부모) 탭 → 컨트롤 토글. 씬에 버튼이 없어 코드로 붙인다
        // (기존 관례: StageListController 행 버튼도 코드로 붙인다).
        void EnsureTapCatcher()
        {
            if (videoSurface == null || videoSurface.transform.parent == null) return;
            var go = videoSurface.transform.parent.gameObject;
            var b = go.GetComponent<Button>();
            if (b == null)
            {
                b = go.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
            }
            b.onClick.RemoveListener(ToggleControls);
            b.onClick.AddListener(ToggleControls);
        }

        void TogglePlay()
        {
            if (!HasSource)
            {
                // 로컬 클립이 없는 무대는 서버 URL 이 오기 전까지 소스가 없다.
                // 예전엔 여기서 조용히 버려서 "재생 버튼이 안 먹는" 걸로 보였다 (2026-08-30 실기).
                pendingPlay = true;
                Debug.Log("[StageDetail] 재생 눌림 — 영상 소스 대기 중(로컬 클립 없음, 서버 응답 대기)");
                return;
            }
            if (player.isPlaying) { player.Pause(); SetControls(true); SyncIcon(); }
            else PlayNow();
        }

        void PlayNow()
        {
            pendingPlay = false;
            player.Play();
            SetControls(true);   // 재생 시작 — 3초 뒤 자동 숨김 예약
            SyncIcon();
            // 서버 무대 세션 — 상세 화면 인라인 재생 = NORMAL (2026-08-25 배선)
            ApiMediaSource.BeginStage(data != null ? data.title : "", ar: false);
        }

        void Update()
        {
            if (HasSource) UpdateProgress();
            // 첫 프레임이 실제로 그려진 뒤에만 영상 면을 보여준다
            ShowVideoSurface(player != null && player.frame > 0);
            SyncIcon();
            if (hideAt > 0f && Time.time >= hideAt && player != null && player.isPlaying)
                SetControls(false);
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
            // player.length 는 클립·URL 어느 소스든 Prepare 후 실제 길이를 준다
            double len = (player != null) ? player.length : 0d;
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