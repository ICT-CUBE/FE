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
        [SerializeField] Button btnCapBack;
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
            Hook(btnCapBack, OnCapBack);
            Hook(btnCapRetry, OnCapRetry);
            Hook(btnCapConfirm, OnCapSaveSheet);
            Hook(btnCapSave, OnCapSaveSheet);
            Hook(btnCapShare, OnCapShare);
            Hook(btnPhotoShare, OnPhotoShare);     // 좌 — OS 기본 공유 시트
            Hook(btnPhotoSave, OnCapSaveSheet);    // 우 — 저장 위치 시트
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
            Unhook(btnPhotoShare, OnPhotoShare);
            Unhook(btnPhotoSave, OnCapSaveSheet);
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
                if (!counted) { shotsAtStageStart = capture != null ? capture.Count : 0; counted = true; }
                stagePlayer.Play();
            }
            else if (id == ReportId)
            {
                stagePlayer.Pause();
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