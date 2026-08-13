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
        [SerializeField] Button btnCapBack;
        [SerializeField] Button btnCapRetry;         // 다시 시도 — 다시 촬영하러 무대로
        [SerializeField] Button btnCapConfirm;       // 저장 — 저장 위치 시트
        [SerializeField] Button btnCapSave;          // 사진 위 다운로드 아이콘
        [SerializeField] Button btnCapShare;         // 사진 위 공유 아이콘
        [SerializeField] GameObject sheetSaveCapture;
        [SerializeField] Button btnSaveApp;
        [SerializeField] Button btnSavePhone;

        // 캡처 상세의 'AR 다시 경험' → 확인 모달 → 스캔부터 다시
        [Header("AR 다시 경험")]
        [SerializeField] Button btnCapReplay;
        [SerializeField] GameObject modalARReplay;
        [SerializeField] Button btnReplayCancel;
        [SerializeField] Button btnReplayStart;
        [SerializeField] string guideScreen = "ARGuide";

        const string StageId = "ARStage";
        const string ReportId = "ARReport";
        const string DetailId = "CaptureDetail";

        float watchSeconds;
        bool counting;
        bool subscribed;
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
            Hook(btnCapBack, OnCapBack);
            Hook(btnCapRetry, OnCapRetry);
            Hook(btnCapConfirm, OnCapSaveSheet);
            Hook(btnCapSave, OnCapSaveSheet);
            Hook(btnCapShare, OnCapShare);
            Hook(btnCapReplay, OnCapReplay);
            Hook(btnReplayCancel, CloseReplayModal);
            Hook(btnReplayStart, OnReplayStart);
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
            Unhook(btnCapBack, OnCapBack);
            Unhook(btnCapRetry, OnCapRetry);
            Unhook(btnCapConfirm, OnCapSaveSheet);
            Unhook(btnCapSave, OnCapSaveSheet);
            Unhook(btnCapShare, OnCapShare);
            Unhook(btnCapReplay, OnCapReplay);
            Unhook(btnReplayCancel, CloseReplayModal);
            Unhook(btnReplayStart, OnReplayStart);

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
                stagePlayer.Play();
            }
            else if (id == ReportId)
            {
                stagePlayer.Pause();
                if (report != null && capture != null)
                    report.Populate(stageTitle, capture.Captures, watchSeconds);
            }
            else if (id != DetailId)
            {
                // AR 흐름을 완전히 벗어남 — 초기화
                stagePlayer.Stop();
                stagePlayer.Unbind();
                watchSeconds = 0f;
                if (capture != null) capture.Clear();
            }
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

        // 다시 재생 — 재스캔 없이 배치 유지한 채 처음부터
        void OnReplay()
        {
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(StageId);
            if (stagePlayer != null) stagePlayer.Restart();
        }

        void OnShutter()
        {
            if (capture == null) return;
            capture.Capture(_ => StartCoroutine(FlashRoutine()));
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

        void OnCardClicked(CaptureService.Entry e)
        {
            selected = e;
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
        void OnReplayStart()
        {
            SetActive(modalARReplay, false);
            ARGuideMode.Pending = ARGuideMode.Mode.StageAR;
            if (ScreenManager.Instance != null && !string.IsNullOrEmpty(guideScreen))
                ScreenManager.Instance.Show(guideScreen);
        }

        void OnCapShare() => Debug.Log("[ARMedia] 공유는 아직 미구현 — 네이티브 공유 필요");

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