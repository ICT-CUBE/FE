using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 무대 재생 · 촬영 · 리포트 · 캡처 상세를 묶는다.
    // ARFlowController 는 진입/스캔/배치까지, 여기는 그 이후를 담당.
    public class ARMediaController : MonoBehaviour
    {
        [Header("서브 컨트롤러")]
        [SerializeField] ARSessionController session;
        [SerializeField] ARStagePlayer stagePlayer;
        [SerializeField] CaptureService capture;
        [SerializeField] ARReportController report;
        // 음악 감상 리포트(Screen_ListenReport)도 같은 컨트롤러를 쓴다.
        // 여기에 물려야 사진 카드의 저장/공유가 시트로 연결된다.
        [SerializeField] ARReportController listenReport;

        [Header("무대 화면")]
        [SerializeField] Button btnPlayPause;
        [SerializeField] Button btnShutter;
        [SerializeField] Image flashOverlay;
        [SerializeField] float flashSeconds = 0.18f;
        [SerializeField] Sprite icoPlay;    // ic_play
        [SerializeField] Sprite icoPause;   // ic_pause

        [Header("일시정지 모달")]
        [SerializeField] GameObject modalPerformancePause;
        [SerializeField] Button btnPauseResume;
        [SerializeField] Button btnPauseRestart;
        [SerializeField] Button btnPauseExit;

        [Header("리포트")]
        [SerializeField] Button btnReportReplay;
        [SerializeField] Button btnReportBack;
        [SerializeField] string stageTitle = "SOLO";

        [Header("캡처 상세 / 저장 시트")]
        [SerializeField] RawImage capturePhoto;
        [SerializeField] TMP_Text captureTitle;
        [SerializeField] TMP_Text captureMeta;
        [SerializeField] TMP_Text captureShotText;   // "촬영 및 저장된 사진 수 4장"
        [SerializeField] TMP_Text captureTimeText;   // "관람 시간 10:49"
        // BtnCapBack 은 씬의 ScreenButton(back)이 처리한다. 여기서도 훅을 걸면 Back 이 두 번
        // 실행돼 한 화면을 건너뛴다 (2026-08-28 실기: 갤러리→상세→뒤로가 홈까지 빠졌다).
        [SerializeField] Button btnCapRetry;         // 다시 시도 — 다시 촬영하러 무대로
        [SerializeField] Button btnCapConfirm;       // 저장 — 저장 위치 시트
        [SerializeField] Button btnCapSave;          // 사진 위 다운로드 아이콘
        [SerializeField] Button btnCapShare;         // 사진 위 공유 아이콘
        [SerializeField] GameObject sheetSaveCapture;
        [SerializeField] Button btnSaveApp;
        [SerializeField] Button btnSavePhone;

        [Header("사진 상세 (시안 '사진 상세')")]
        [SerializeField] string photoDetailScreen = "PhotoDetail";
        [SerializeField] RawImage photoDetailImage;
        [SerializeField] Button btnPhotoShare;   // 좌 64² — 시안 화살표가 저장 시트로 간다
        [SerializeField] Button btnPhotoSave;    // 우 64² — 캡처 상세와 같은 관례(다운로드=저장 시트)

        // 캡처 상세의 'AR 다시 경험' → 확인 모달 → 스캔부터 다시
        [Header("AR 다시 경험")]
        [SerializeField] Button btnCapReplay;
        [SerializeField] GameObject modalARReplay;
        [SerializeField] Button btnReplayCancel;
        [SerializeField] Button btnReplayStart;
        [SerializeField] string guideScreen = "ARGuide";

        // 감상 리포트(ListenReport) 하단 — 씬의 BtnReplay/BtnExit 는 배선이 없어 죽은 버튼이었다 (2026-08-28 실기)
        [Header("감상 리포트")]
        [SerializeField] Button btnListenReplay;     // 다시 재생 → AR 감상 처음부터
        [SerializeField] Button btnListenExit;       // 나가기 → 음악 상세
        [SerializeField] string musicScreen = "MusicDetail";

        const string StageId = "ARStage";
        const string ReportId = "ARReport";
        const string DetailId = "CaptureDetail";

        float watchSeconds;
        bool counting;
        bool subscribed;
        int shotsAtStageStart;   // 이번 관람이 시작될 때의 촬영 장수
        bool counted;
        CaptureService.Entry selected;

        void OnEnable()
        {
            Hook(btnPlayPause, OnPlayPause);
            Hook(btnShutter, OnShutter);
            Hook(btnPauseResume, OnResume);
            Hook(btnPauseRestart, OnRestart);
            Hook(btnPauseExit, OnExitToReport);
            Hook(btnReportReplay, OnReplay);
            Hook(btnReportBack, OnCapBack);
            Hook(btnSaveApp, OnSaveApp);
            Hook(btnSavePhone, OnSavePhone);
            Hook(btnCapRetry, OnCapRetry);
            Hook(btnCapConfirm, OnCapSaveSheet);
            Hook(btnCapSave, OnCapSaveSheet);
            Hook(btnCapShare, OnCapShare);
            Hook(btnPhotoShare, OnPhotoShare);     // 좌 — OS 기본 공유 시트
            Hook(btnPhotoSave, OnCapSaveSheet);    // 우 — 저장 위치 시트
            Hook(btnCapReplay, OnCapReplay);
            Hook(btnReplayCancel, CloseReplayModal);
            Hook(btnReplayStart, OnReplayStart);
            Hook(btnListenReplay, OnListenReplay);
            Hook(btnListenExit, OnListenExit);
            SetActive(modalARReplay, false);

            if (stagePlayer != null) stagePlayer.Finished += OnVideoFinished;
            if (report != null)
            {
                report.CardClicked += OnCardClicked;
                report.SaveClicked += OnCardSaveClicked;
            }
            if (listenReport != null)
            {
                listenReport.CardClicked += OnCardClicked;
                listenReport.SaveClicked += OnCardSaveClicked;
            }

            SetFlash(0f);
            TrySubscribe();
        }

        // ARFlowController 와 같은 이유로 Start 에서 한 번 더 구독한다.
        // 이 컴포넌트는 ARFlowController 보다 아래에 있어야 세션이 먼저 만들어진 뒤 실행된다.
        void Start()
        {
            TrySubscribe();
            if (subscribed) OnScreenChanged(ScreenManager.Instance.Current);
            else Debug.LogError("[ARMedia] ScreenManager 를 찾지 못함 — 씬에 ScreenManager 가 있는지 확인");
        }

        void TrySubscribe()
        {
            if (subscribed || ScreenManager.Instance == null) return;
            ScreenManager.Instance.ScreenChanged += OnScreenChanged;
            subscribed = true;
        }

        void OnDisable()
        {
            Unhook(btnPlayPause, OnPlayPause);
            Unhook(btnShutter, OnShutter);
            Unhook(btnPauseResume, OnResume);
            Unhook(btnPauseRestart, OnRestart);
            Unhook(btnPauseExit, OnExitToReport);
            Unhook(btnReportReplay, OnReplay);
            Unhook(btnReportBack, OnCapBack);
            Unhook(btnSaveApp, OnSaveApp);
            Unhook(btnSavePhone, OnSavePhone);
            Unhook(btnCapRetry, OnCapRetry);
            Unhook(btnCapConfirm, OnCapSaveSheet);
            Unhook(btnCapSave, OnCapSaveSheet);
            Unhook(btnCapShare, OnCapShare);
            Unhook(btnPhotoShare, OnPhotoShare);
            Unhook(btnPhotoSave, OnCapSaveSheet);
            Unhook(btnCapReplay, OnCapReplay);
            Unhook(btnReplayCancel, CloseReplayModal);
            Unhook(btnReplayStart, OnReplayStart);
            Unhook(btnListenReplay, OnListenReplay);
            Unhook(btnListenExit, OnListenExit);

            if (stagePlayer != null) stagePlayer.Finished -= OnVideoFinished;
            if (report != null)
            {
                report.CardClicked -= OnCardClicked;
                report.SaveClicked -= OnCardSaveClicked;
            }
            if (listenReport != null)
            {
                listenReport.CardClicked -= OnCardClicked;
                listenReport.SaveClicked -= OnCardSaveClicked;
            }

            if (subscribed && ScreenManager.Instance != null)
                ScreenManager.Instance.ScreenChanged -= OnScreenChanged;
            subscribed = false;
        }

        void Update()
        {
            if (counting) watchSeconds += Time.deltaTime;
            SyncPlayIcon();
        }

        // 재생 상태에 맞춰 버튼 아이콘을 바꾼다.
        // 아이콘에 배경 원이 포함돼 있어 스프라이트 교체로 처리한다.
        void SyncPlayIcon()
        {
            if (btnPlayPause == null) return;
            // 영상이 아직 안 물렸으면 건드리지 않는다.
            // IsPlaying 이 false 라 매 프레임 세모(재생)로 덮어써서, 기본 상태가 일시정지가 아니었다.
            if (stagePlayer == null || !stagePlayer.HasVideo) return;

            var img = btnPlayPause.GetComponent<Image>();
            if (img == null) return;

            bool playing = stagePlayer != null && stagePlayer.IsPlaying;
            var want = playing ? icoPause : icoPlay;
            if (want != null && img.sprite != want) img.sprite = want;
        }

        void OnScreenChanged(string id)
        {
            if (stagePlayer == null) return;

            counting = id == StageId;

            if (id == StageId)
            {
                if (session != null && session.Video != null) stagePlayer.Bind(session.Video);
                if (!stagePlayer.HasVideo)
                    Debug.LogWarning("[ARMedia] VideoPlayer 를 못 찾음 — ARRig 가 생성됐는지 확인");
                if (!counted) { shotsAtStageStart = capture != null ? capture.Count : 0; counted = true; }
                // 서버 스트리밍 (2026-08-26 전환) — 무대 상세를 거쳐 왔으면 URL 이 캐시돼 있어
                // 콜백이 동기로 끝나 Play 전에 소스가 바뀐다. 실패/미캐시면 프리팹 로컬 클립 그대로.
                var sel0 = StageCatalog.Instance != null ? StageCatalog.Instance.Selected : null;
                // ★ 선택한 무대의 로컬 클립을 먼저 꽂는다 — ARRig 프리팹엔 기본 클립 하나만
                //   박혀 있어서, 서버 매칭이 안 되는 무대(서버에 없는 곡·미로그인)는 어떤
                //   무대를 골라도 같은 영상이 나왔다 (2026-08-30 실기).
                {
                    var vp0 = session != null ? session.Video : null;
                    if (vp0 != null && sel0 != null && sel0.clip != null
                        && !vp0.isPlaying && vp0.clip != sel0.clip)
                    {
                        vp0.Stop();
                        vp0.source = UnityEngine.Video.VideoSource.VideoClip;
                        vp0.clip = sel0.clip;
                        vp0.Prepare();
                        Debug.Log($"[ARMedia] 무대 '{sel0.title}' 로컬 클립 장착");
                    }
                }
                ApiMediaSource.GetStageVideoUrl(sel0 != null ? sel0.title : stageTitle, url =>
                {
                    var vp = session != null ? session.Video : null;
                    if (string.IsNullOrEmpty(url) || vp == null || vp.isPlaying) return;
                    // URL 재생 실패(코덱·네트워크) 시 프리팹 로컬 클립으로 되돌린다 — 흰 화면 방지
                    vp.errorReceived -= OnStageVideoError;
                    vp.errorReceived += OnStageVideoError;
                    vp.source = UnityEngine.Video.VideoSource.Url;
                    vp.url = url;
                    Debug.Log("[ARMedia] 무대 서버 영상으로 전환");
                });
                stagePlayer.Play();
                // 서버 무대 세션 (2026-08-25 배선) — 어느 무대인지는 카탈로그의 선택값으로 잇는다
                var sel = StageCatalog.Instance != null ? StageCatalog.Instance.Selected : null;
                ApiMediaSource.BeginStage(sel != null ? sel.title : stageTitle, ar: true);
            }
            else if (id == ReportId)
            {
                stagePlayer.Pause();
                ApiMediaSource.EndStage((float)stagePlayer.Time, watchSeconds);
                // 이번 관람에서 찍은 것만 리포트에 넣는다 (앞서 AR 소환에서 찍어둔 건 제외)
                if (report != null && capture != null)
                    report.Populate(stageTitle, ShotsThisSession(), watchSeconds);
            }
            else if (id != DetailId)
            {
                // AR 관람 흐름을 완전히 벗어남 — 재생만 정리한다.
                // ⚠ 예전엔 여기서 capture.Clear() 로 촬영분을 통째로 지웠다.
                //   그런데 이 분기에는 AR 소환 흐름(ARSummon/SummonReport)과 '오늘 찍은 사진들'
                //   까지 전부 걸려서, 촬영하자마자 리포트로 넘어가는 순간 사진이 다 날아갔다
                //   (2026-08-18 실기: 리포트가 하얗고 오늘 찍은 사진들이 0장).
                //   촬영분은 하루치 자산이고 CaptureService 가 디스크로도 들고 있으므로 지우지 않는다.
                // 리포트를 안 거치고 나간 경우의 세션 마감 (이미 닫혔으면 안에서 무시된다)
                ApiMediaSource.EndStage((float)stagePlayer.Time, watchSeconds);
                stagePlayer.Stop();
                stagePlayer.Unbind();
                watchSeconds = 0f;
                counted = false;
            }
        }

        // 이번 관람 세션에서 찍은 것만 잘라낸다
        IReadOnlyList<CaptureService.Entry> ShotsThisSession()
        {
            var all = capture.Captures;
            int from = Mathf.Clamp(shotsAtStageStart, 0, all.Count);
            var slice = new List<CaptureService.Entry>(all.Count - from);
            for (int i = from; i < all.Count; i++) slice.Add(all[i]);
            return slice;
        }

        void OnPlayPause()
        {
            if (stagePlayer == null) return;
            if (stagePlayer.IsPlaying)
            {
                stagePlayer.Pause();
                SetActive(modalPerformancePause, true);
            }
            else stagePlayer.Play();
        }

        void OnResume()
        {
            SetActive(modalPerformancePause, false);
            if (stagePlayer != null) stagePlayer.Play();
        }

        void OnRestart()
        {
            SetActive(modalPerformancePause, false);
            if (stagePlayer != null) stagePlayer.Restart();
        }

        void OnExitToReport()
        {
            SetActive(modalPerformancePause, false);
            if (stagePlayer != null) stagePlayer.Pause();
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(ReportId);
        }

        void OnVideoFinished()
        {
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(ReportId);
        }

        // 감상 리포트 '다시 재생' — AR 감상으로 돌아가 처음부터. 같은 모드 재진입이라 자동재생 OK
        // (모드 '변경' 시 일시정지 정책과 충돌하지 않는다)
        void OnListenReplay()
        {
            MusicDetailController.PendingTime = 0f;
            MusicDetailController.PendingPlaying = true;
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show("ARListen");
        }

        // 감상 리포트 '나가기' — 감상을 끝내고 음악 상세로
        void OnListenExit()
        {
            if (ScreenManager.Instance != null && !string.IsNullOrEmpty(musicScreen))
                ScreenManager.Instance.Show(musicScreen);
        }

        // 서버 URL 재생 실패 → 프리팹 로컬 클립으로 복귀 (VideoPlayer.clip 은 URL 전환 후에도 남아 있다)
        void OnStageVideoError(UnityEngine.Video.VideoPlayer vp, string message)
        {
            bool canFallback = vp != null && vp.clip != null;
            Debug.LogWarning($"[ARMedia] 무대 영상 재생 실패: {message}" +
                             (canFallback ? " — 로컬 클립으로 복귀" : " (로컬 클립도 없음)"));
            if (!canFallback) return;
            vp.Stop();
            vp.source = UnityEngine.Video.VideoSource.VideoClip;
            vp.Prepare();
            vp.Play();
        }

        // 다시 재생 — 재스캔 없이 배치 유지한 채 처음부터
        void OnReplay()
        {
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(StageId);
            if (stagePlayer != null) stagePlayer.Restart();
        }

        void OnShutter()
        {
            if (capture == null) return;
            capture.Capture(e =>
            {
                StartCoroutine(FlashRoutine());
                // 서버 무대 캡처 (2026-08-25 배선) — 파일이 원본이라 그 바이트를 그대로 올린다
                if (e != null && !string.IsNullOrEmpty(e.FilePath))
                {
                    try
                    {
                        var png = System.IO.File.ReadAllBytes(e.FilePath);
                        ApiMediaSource.UploadStageCapture(png, (float)(stagePlayer != null ? stagePlayer.Time : 0d), e.TakenAt);
                    }
                    catch (System.Exception ex) { Debug.LogWarning($"[Media] 캡처 파일을 못 읽음: {ex.Message}"); }
                }
            });
        }

        System.Collections.IEnumerator FlashRoutine()
        {
            if (flashOverlay == null) yield break;
            flashOverlay.gameObject.SetActive(true);
            float t = 0f;
            while (t < flashSeconds)
            {
                t += Time.deltaTime;
                SetFlash(1f - t / flashSeconds);
                yield return null;
            }
            SetFlash(0f);
            flashOverlay.gameObject.SetActive(false);
        }

        void SetFlash(float a)
        {
            if (flashOverlay == null) return;
            var c = flashOverlay.color;
            flashOverlay.color = new Color(c.r, c.g, c.b, a);
        }

        // '오늘 찍은 사진들' 등 다른 화면에서도 같은 사진 상세를 연다
        public void ShowCaptureDetail(CaptureService.Entry e) => OnCardClicked(e);

        // 시안 '사진 상세' — 캡처 상세(캡처 상세.svg)와는 다른 화면이다.
        // 제목·장수·관람시간 없이 사진 전체(0,106 412×618) + 공유·다운로드 64² 만 있다.
        public void ShowPhotoDetail(CaptureService.Entry e)
        {
            selected = e;
            UiUtil.FitCover(photoDetailImage, e?.Texture);
            if (ScreenManager.Instance != null && !string.IsNullOrEmpty(photoDetailScreen))
                ScreenManager.Instance.Show(photoDetailScreen);
        }

        void OnCardClicked(CaptureService.Entry e)
        {
            selected = e;
            GalleryController.CurrentDetail = null;   // 이 상세는 갤러리가 아니라 AR 흐름에서 열렸다
            // 갤러리가 앨범 사진을 열며 숨겼을 수 있다 — AR 촬영 상세는 무대 캡처라 항상 보인다
            if (btnCapReplay != null && !btnCapReplay.gameObject.activeSelf)
                btnCapReplay.gameObject.SetActive(true);
            UiUtil.FitCover(capturePhoto, e?.Texture);   // 늘어나지 않게 중앙 크롭
            if (captureTitle != null) captureTitle.text = $"{stageTitle} 무대 캡처";
            if (captureMeta != null && e != null)
                captureMeta.text = $"{e.TakenAt:yyyy.MM.dd} · AR 스냅샷";

            // 사진 수·관람 시간은 리포트와 같은 실제 값을 쓴다 (씬에 박아둔 값 금지)
            int shots = capture != null ? capture.Captures.Count : 0;
            if (captureShotText != null)
                captureShotText.text = $"촬영 및 저장된 사진 수 <b>{shots}장</b>";
            if (captureTimeText != null)
                captureTimeText.text = $"관람 시간 <b>{ARReportController.FormatTime(watchSeconds)}</b>";

            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(DetailId);
        }

        void OnCapBack()
        {
            if (ScreenManager.Instance != null) ScreenManager.Instance.Back();
        }

        // 다시 시도 — 배치는 그대로 두고 무대로 돌아가 다시 촬영한다
        void OnCapRetry()
        {
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(StageId);
        }

        void OnCapSaveSheet()
        {
            SetActive(sheetSaveCapture, true);
        }

        // 시안 'ar 다시 경험 클릭' → 확인 모달 '무대를 다시 경험해볼까요?'
        void OnCapReplay() => SetActive(modalARReplay, true);
        void CloseReplayModal() => SetActive(modalARReplay, false);

        // '시작하기' → 공간 스캔부터 다시 (AR 모드 가이드로)
        // 갤러리에서 연 상세면 어느 무대였는지 서버(ar-experience)에 묻고 그 무대를 선택해 둔다.
        // 화면 전환은 응답을 기다리지 않는다 — 스캔·배치(최소 1.8초) 동안 응답이 도착해
        // ARStage 진입 시점(StageCatalog.Selected 를 읽는다)엔 선택이 끝나 있다. 늦으면 로그만.
        void OnReplayStart()
        {
            SetActive(modalARReplay, false);
            var item = GalleryController.CurrentDetail;
            if (item != null && GalleryStore.Instance != null)
                GalleryStore.Instance.LoadArExperience(item, ApplyReplayTarget, null);
            ARGuideMode.Pending = ARGuideMode.Mode.StageAR;
            if (ScreenManager.Instance != null && !string.IsNullOrEmpty(guideScreen))
                ScreenManager.Instance.Show(guideScreen);
        }

        // 서버가 알려준 원본 콘텐츠를 무대 선택에 반영한다.
        // TRACK 재경험은 감상(ARListen) 흐름이라 무대 배치 시안과 안 맞는다 — 라우팅하지 않고 로그만.
        void ApplyReplayTarget(GalleryArExperienceItemDto dto)
        {
            if (dto == null) return;
            if (dto.contentType != "STAGE" || dto.stage == null)
            {
                Debug.LogWarning($"[ARMedia] AR재경험 {dto.contentType} 은 무대 흐름만 지원 — 현재 선택된 무대로 진행");
                return;
            }
            var cat = StageCatalog.Instance;
            if (cat == null) { Debug.LogWarning("[ARMedia] StageCatalog 없음 — 무대 선택 생략"); return; }
            foreach (var s in cat.All)
            {
                if (!ApiMediaSource.TitleMatch(s.title, dto.stage.title) &&
                    !ApiMediaSource.TitleMatch(s.title + " " + s.sub, dto.stage.title)) continue;
                cat.Select(s);
                Debug.Log($"[ARMedia] AR재경험: 무대 '{s.title}' 선택 (서버 '{dto.stage.title}')");
                return;
            }
            Debug.LogWarning($"[ARMedia] AR재경험: 서버 무대 '{dto.stage.title}' 와 맞는 로컬 무대 없음 — 현재 선택 유지");
        }

        void OnCapShare() => Debug.Log("[ARMedia] 공유는 아직 미구현 — 네이티브 공유 필요");

        // 시안 주석: "공유 : OS 기본 공유 시트를 호출"
        void OnPhotoShare()
        {
            if (capture == null || selected == null) return;
            if (!capture.ShareToOS(selected))
                Toast.Show("공유할 수 없어요.", ToastBottom);
        }

        void OnCardSaveClicked(CaptureService.Entry e)
        {
            selected = e;
            SetActive(sheetSaveCapture, true);
        }

        // 앱 내 Gallery 는 아직 미구현 — 파일은 이미 persistentDataPath 에 쌓여 있다.
        void OnSaveApp()
        {
            SetActive(sheetSaveCapture, false);
            // 파일은 이미 persistentDataPath 에 쌓여 있다. 앱 내 Gallery 화면만 아직 없음.
            Toast.Show("ARZONE Gallery에 저장되었어요.", ToastBottom);
        }

        void OnSavePhone()
        {
            SetActive(sheetSaveCapture, false);
            bool ok = capture != null && capture.SaveToPhoneGallery(selected);
            Toast.Show(ok ? "기기 갤러리에 저장되었어요." : "저장에 실패했어요. 다시 시도해 주세요.", ToastBottom);
        }

        // 리포트·캡처 상세는 하단에 버튼이 있어 토스트를 그 위로 띄운다(시안 uY 125).
        const float ToastBottom = 130f;   // 시안 48 사진 저장 확인 토스트 (20,717) 372×46

        static void Hook(Button b, UnityEngine.Events.UnityAction a) { if (b != null) b.onClick.AddListener(a); }
        static void Unhook(Button b, UnityEngine.Events.UnityAction a) { if (b != null) b.onClick.RemoveListener(a); }
        static void SetActive(GameObject go, bool on) { if (go != null && go.activeSelf != on) go.SetActive(on); }
    }
}