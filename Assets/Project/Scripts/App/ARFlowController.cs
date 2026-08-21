using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // AR 흐름 제어: 공간 인식 → 배치 → 무대 준비 → 콘텐츠 선택 → 재생.
    // 한 번 배치한 무대는 유지된다. 그래서 Hub/StageList/상세를 오가는 동안에도
    // AR 세션을 내리지 않는다. AR 영역을 완전히 벗어날 때만 정리한다.
    public class ARFlowController : MonoBehaviour
    {
        [Header("서브 컨트롤러")]
        [SerializeField] ARSessionController session;
        [SerializeField] GroundPlaneScanner scanner;
        [SerializeField] StagePlacementController placement;

        [Header("오버레이 (ScreenManager 미등록)")]
        [SerializeField] GameObject modalStageReady;
        [SerializeField] GameObject modalPerformancePause;
        [SerializeField] GameObject sheetSaveCapture;

        [Header("코드로 붙이는 버튼")]
        [SerializeField] Button btnPlaceConfirm;
        [SerializeField] Button btnSummonPlaceConfirm;   // Screen_SummonPlace 의 배치 완료
        [SerializeField] Button btnStageStart;     // 시작하기 (무대 준비 모달)
        [SerializeField] Button btnHintClose;      // 제스처 힌트 칩 닫기

        // AR 세션은 화면을 오가는 동안 살아 있다. 그래서 재진입하면 평면이 '이미' 잡혀 있어
        // OnPlaneFound 가 즉시 떨어지고, 스캔 안내가 한 프레임 만에 지나가 버린다.
        // 최소 이 시간만큼은 스캔 화면을 보여준다.
        [SerializeField] float minScanSeconds = 1.8f;

        [Header("기타")]
        [SerializeField] GameObject hintChip;
        // 시작하기 후 갈 화면은 진입 경로에 따라 다르다.
        //   첫 인식(Scan AR) — 무대만 놓고 AR 홈으로 돌아간다
        //   영상에서 들어온 AR 모드 — 그 영상을 바로 재생한다
        [SerializeField] string afterReadyFirstScan = "Hub";
        [SerializeField] string afterReadyStageAR = "ARStage";
        // AR 소환(아바타 홈)은 무대 준비 모달 없이 바로 촬영 화면으로 간다 (유저 지시 플로우)
        [SerializeField] string afterPlaceSummon = "ARSummon";


        const string GuideId = "ARGuide";
        const string ScanId = "ARScan";
        const string PlaceId = "ARPlace";
        const string StageId = "ARStage";
        const string ReportId = "ARReport";
        const string DetailId = "CaptureDetail";
        const string SummonId = "ARSummon";
        // AR 소환은 스캔·인식 성공 화면을 따로 쓴다 (유저 지시 — 기존 Scan AR 화면을 돌려쓰지 않는다)
        const string SummonScanId = "SummonScan";
        const string SummonPlaceId = "SummonPlace";

        // 카메라가 실제로 필요한 화면
        static readonly HashSet<string> CameraScreens = new HashSet<string>
        { ScanId, PlaceId, StageId, SummonId, SummonScanId, SummonPlaceId };

        // 세션을 유지해야 하는 화면 (무대를 살려둬야 하는 구간)
        static readonly HashSet<string> KeepAlive = new HashSet<string>
        { GuideId, ScanId, PlaceId, StageId, ReportId, DetailId, SummonId,
          SummonScanId, SummonPlaceId,
          "Hub", "StageList", "StageDetail" };

        bool subscribed;
        float scanShownAt = -1f;   // ARScan 이 열린 시각
        bool planePending;         // 최소 시간 전에 평면이 잡힌 경우

        void Awake()
        {
            SetActive(modalStageReady, false);
            SetActive(modalPerformancePause, false);
            SetActive(sheetSaveCapture, false);
        }

        void OnEnable()
        {
            if (btnPlaceConfirm != null) btnPlaceConfirm.onClick.AddListener(OnPlaceConfirm);
            if (btnSummonPlaceConfirm != null) btnSummonPlaceConfirm.onClick.AddListener(OnPlaceConfirm);
            if (btnStageStart != null) btnStageStart.onClick.AddListener(OnStageStart);
            if (btnHintClose != null) btnHintClose.onClick.AddListener(OnHintClose);
            if (scanner != null) scanner.PlaneFound += OnPlaneFound;
            TrySubscribe();
        }

        void Start()
        {
            TrySubscribe();
            if (subscribed) OnScreenChanged(ScreenManager.Instance.Current);
            else Debug.LogError("[ARFlow] ScreenManager 를 찾지 못함");
        }

        void TrySubscribe()
        {
            if (subscribed || ScreenManager.Instance == null) return;
            ScreenManager.Instance.ScreenChanged += OnScreenChanged;
            subscribed = true;
        }

        void OnDisable()
        {
            if (btnPlaceConfirm != null) btnPlaceConfirm.onClick.RemoveListener(OnPlaceConfirm);
            if (btnStageStart != null) btnStageStart.onClick.RemoveListener(OnStageStart);
            if (btnHintClose != null) btnHintClose.onClick.RemoveListener(OnHintClose);
            if (scanner != null) scanner.PlaneFound -= OnPlaneFound;

            if (subscribed && ScreenManager.Instance != null)
                ScreenManager.Instance.ScreenChanged -= OnScreenChanged;
            subscribed = false;
        }

        void OnScreenChanged(string id)
        {
            if (session == null)
            {
                Debug.LogError("[ARFlow] Session 이 비어 있음 — 인스펙터에서 ARManagers 를 넣을 것");
                return;
            }

            // AR 영역을 완전히 벗어나면 정리 (피규어 인식 화면 포함 — Vuforia 와 카메라 충돌 방지)
            if (!KeepAlive.Contains(id))
            {
                if (session.IsRunning)
                {
                    if (scanner != null) scanner.End();
                    if (placement != null) placement.End();
                    session.StopSession();
                }
                return;
            }

            if (CameraScreens.Contains(id) && !session.IsRunning) session.StartSession();

            // AR 소환(아바타 홈)으로 들어온 구간에서는 무대를 놓지도, 보여주지도 않는다 (유저 지시 2026-08-18).
            // 스테이지 흐름은 그대로 — 이미 놓아둔 무대는 지우지 않고 감추기만 해서 돌아오면 살아 있다.
            bool summon = IsSummonScreen(id);

            switch (id)
            {
                case ScanId:
                case SummonScanId:
                    session.SetScanning(true);
                    session.ShowGrid(true);
                    // 스캔 중에는 아무것도 놓지 않는다 (소환은 인식 성공 뒤부터)
                    if (placement != null && !summon) placement.Begin(session);
                    if (scanner != null) scanner.Begin(session);
                    scanShownAt = Time.time;
                    planePending = false;
                    break;

                case PlaceId:
                case SummonPlaceId:
                    if (scanner != null) scanner.End();
                    session.SetScanning(true);
                    session.ShowGrid(true);
                    // 소환 흐름은 무대가 아니라 아바타를 놓는다
                    if (placement != null)
                        placement.Begin(session, summon ? StagePlacementController.Target.Avatar
                                                       : StagePlacementController.Target.Stage);
                    SetActive(hintChip, true);
                    break;

                case SummonId:
                    // 촬영 화면에선 더 이상 따라다니지 않는다 (아바타는 그 자리에 남는다)
                    if (placement != null) placement.End();
                    break;

                case StageId:
                    if (placement != null) placement.End();
                    session.ConfirmPlacement();
                    break;
            }

            session.ShowStage(!summon);
            session.ShowAvatar(summon);
        }

        // 지금 열려 있는 스캔 화면 → 그 짝이 되는 인식 성공 화면
        static string PlaceFor(string scanId) => scanId == SummonScanId ? SummonPlaceId : PlaceId;

        static bool IsScanScreen(string id) => id == ScanId || id == SummonScanId;

        // AR 소환 흐름의 화면들 (무대 없이 돈다)
        static bool IsSummonScreen(string id) =>
            id == SummonScanId || id == SummonPlaceId || id == SummonId;

        void OnPlaneFound()
        {
            var sm = ScreenManager.Instance;
            if (sm == null || !IsScanScreen(sm.Current)) return;

            // 너무 빨리 잡혔으면 안내를 읽을 시간을 준 뒤 넘어간다
            if (Time.time - scanShownAt < minScanSeconds) { planePending = true; return; }
            sm.Show(PlaceFor(sm.Current));
        }

        void Update()
        {
            if (!planePending) return;
            if (Time.time - scanShownAt < minScanSeconds) return;
            planePending = false;
            var sm = ScreenManager.Instance;
            if (sm != null && IsScanScreen(sm.Current)) sm.Show(PlaceFor(sm.Current));
        }


        void OnPlaceConfirm()
        {
            // 배치 완료를 눌러도 모달이 안 뜬다는 보고가 있어 클릭이 실제로 오는지 남긴다.
            Debug.Log($"[ARFlow] 배치완료 클릭 — 모달={(modalStageReady != null ? modalStageReady.name : "null")} " +
                      $"현재화면={(ScreenManager.Instance != null ? ScreenManager.Instance.Current : "?")}");

            // '무대 준비가 끝났어요' 모달은 첫 인식(Scan AR)에서만 띄운다.
            // AR 모드는 이미 무대를 고르고 들어온 흐름이라 바로 무대로 간다 (유저 지시).
            bool fromStage = ARGuideMode.Pending == ARGuideMode.Mode.StageAR;
            bool fromSummon = ARGuideMode.Pending == ARGuideMode.Mode.Summon;

            // UI 를 먼저 처리한다. 아래 세션 호출에서 예외가 나면 화면이 아예 안 넘어가고
            // AR 만 계속 돌아가는 상태가 됐었다(배치 완료를 눌러도 반응 없음).
            if (fromStage || fromSummon)
            {
                SetActive(modalStageReady, false);
                if (ScreenManager.Instance != null)
                    ScreenManager.Instance.Show(fromSummon ? afterPlaceSummon : afterReadyStageAR);
            }
            else SetActive(modalStageReady, true);

            if (placement != null) placement.End();
            if (session != null) session.ConfirmPlacement();
        }

        // 시작하기 → 모달 닫기. 무대는 그대로 남고, 갈 화면만 진입 경로로 갈린다.
        void OnStageStart()
        {
            SetActive(modalStageReady, false);
            bool fromStage = ARGuideMode.Pending == ARGuideMode.Mode.StageAR;
            string next = fromStage ? afterReadyStageAR : afterReadyFirstScan;
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(next);
        }

        void OnHintClose() => SetActive(hintChip, false);

        static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }
}