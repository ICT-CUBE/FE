using System;
using System.Collections;
using UnityEngine;
using Vuforia;

namespace ARZone
{
    // 실물 피규어(큐브) Vuforia Model Target 인식기 (2026-08-26).
    //
    // 데이터셋: Assets/StreamingAssets/Vuforia/avatar.{dat,xml}
    //   — MTG(Model Target Generator)에서 피규어인식용_8cm_40만.fbx 로 생성. 타깃 이름 "avatar",
    //     실물 8cm 스케일 반영, Guide View 1개(정면), 평점 5/5.
    //
    // IObjectDetector 관례(SummonController 시절 그대로): 최초 1회 인식 → OnDetected(월드 포즈).
    // ⚠ AR Foundation(ARCore)과 카메라를 동시에 못 쓴다 — 이 컴포넌트가 살아 있는 동안
    //   ARSession 이 돌면 안 된다. 온보딩(AR 진입 전)에서만 쓰는 전제다.
    // ⚠ 이 코드베이스는 async/await 를 안 쓴다 — Vuforia 의 Task API 는 코루틴으로 기다린다.
    public class VuforiaCubeDetector : MonoBehaviour, IObjectDetector
    {
        [SerializeField] string dataSetPath = "Vuforia/avatar.xml";  // StreamingAssets 기준
        [SerializeField] string targetName = "avatar";
        // 2026-08-30 실기: 사진·다리 잘린 피규어도 한 프레임 스치면 통과했다 → 연속 관측 요구.
        // 2026-08-31 강화: 그래도 너무 쉽게 통과해 관측 '품질' 검사를 추가했다 —
        //   ① 최근 holdSeconds 동안 끊김 없이 TRACKED+NORMAL
        //   ② 그동안 대상의 월드 포즈가 안 흔들릴 것 (부분 형상 매칭은 구속점이 적어 떨린다)
        //   ③ 카메라가 조금은 움직였을 것 (시차 없이 정면 한 장으로 버티는 매칭 차단)
        // ⚠ 정직한 한계: 온전한 형상을 안정적으로 비추면 여전히 통과할 수 있다 —
        //   Vuforia 에 위조 판별 기능이 없어 '어렵게' 만드는 것까지가 가능한 전부다.
        // ⚠ 튜닝값은 전부 const — SerializeField 로 두면 씬에 박힌 옛 값이 코드 기본값을
        //   덮어써서(실기 2026-08-31 저녁: 꺼놓은 시차 검사가 3cm/6° 로 살아 있고 유지시간도
        //   1.5s 로 돌았다) 튜닝이 전혀 반영되지 않았다. 씬은 이 값들을 모른다.
        const float holdSeconds = 2.5f;
        // 거리 흔들림 경계 5cm: 정품 창 1.4~4.9cm · 잘린/사진 5.7~91cm (2026-08-31 실측 분포)
        const float maxPosJitterM = 0.05f;   // 관측 창 안 '카메라↔대상 거리' 흔들림 허용(m)
        // 2026-08-31 저녁 유저 결정: "옆으로 움직이는 걸 빼야할듯" — 시차 요구 제거 (0 = 검사 안 함).
        // 사진 방어는 거리흔들림 + 다리 픽셀 검증이 맡는다. 다시 켜려면 0.03 / 6 으로.
        const float minCamMoveM = 0f;        // 관측 창 안 카메라 최소 이동(m), 0=끔
        const float minViewAngleDeg = 0f;    // 보는 각도 최소 변화(도), 0=끔

        // ★ 월드 단위 보정 (2026-08-31 저녁 실측): 이 씬의 Vuforia 월드는 1유닛=1cm 다.
        //   증거 — ① 손으로 폰을 움직인 창의 '이동'이 253~1185(cm 표기) = 실제 2.5~11.8cm
        //   ② 발끝(원점)~머리(+0.076 '미터') 오프셋이 화면에서 1~3px 로 붕괴(단위가 100배 작음)
        //   ③ 데이터셋 xml 의 가이드 카메라는 미터(0.04/0.119)지만 런타임 포즈는 cm 로 온다.
        const float WorldUnitsPerMeter = 100f;

        // ⑤ 전체(다리) 검증 — "피규어 전체 인식" (2026-08-31 유저 확정).
        //   다리 잘린 피규어는 기하로 구분이 안 된다(정품만큼 안정 추적됨, 실측 결론).
        //   대신 판정 직전에 화면을 캡처해 '다리가 있어야 할 자리'의 색을 그 옆 배경과 비교한다 —
        //   다리가 잘렸으면 그 자리에 배경이 그대로 보여서 색 차이가 거의 없다.
        //   또한 머리~발끝이 모두 화면 안에 있어야 판정한다(전체를 비추게 강제).
        const float figureHeightM = 0.08f;   // 실물 높이 (MTG: 8cm 스케일, 원점=발바닥 — xml EntryPoint 로 확인)
        const float figureWidthM = 0.03f;    // 다리 좌우 폭 추정치
        const float legZoneTop = 0.35f;      // 바닥~이 비율까지를 다리 구역으로 본다
        const float minLegDiff = 0.08f;      // 다리 vs 배경 색 차이 최소값(0~1) — 실측 후 재조정

        public event Action<Pose> OnDetected;
        public bool HasDetected { get; private set; }
        public Pose DetectedPose { get; private set; }

        // 지금 추적 중인가(잡혔다 놓치면 false 로 돌아온다) — UI 가 진행 연출에 쓴다
        public bool IsTracking { get; private set; }

        // 스캔 UI 실시간 표시용 (2026-08-31 "인식 잘 되고 있는지 알 수 있게")
        public float HoldProgress { get; private set; }      // 연속 관측이 얼마나 쌓였나 (0~1)
        public float ParallaxProgress { get; private set; }  // 시차(옆으로 움직이기)가 얼마나 찼나 (0~1)
        // 가이드 실루엣 — 스캔 UI 가 프레임 안에 직접 그린다.
        // ⚠ GuideViewMode 를 NoGuideView 로 끄면 Vuforia 가 텍스처 자체를 안 만든다(실기: 실루엣
        //   안 뜸). 그래서 내장 모드(GuideView2D)는 켜두고, 텍스처를 얻은 뒤 내장 오버레이
        //   오브젝트만 숨긴다 — Vuforia 가 되살려도 Update 에서 다시 숨긴다.
        Texture guideImage;
        GuideView2DBehaviour builtinGuide;
        public Texture GuideImage
        {
            get
            {
                if (guideImage == null && target != null)
                {
                    try
                    {
                        var gv = target.GetGuideView(Mathf.Max(0, target.GetActiveGuideViewIndex()));
                        if (gv != null) guideImage = gv.Image;
                    }
                    catch (Exception) { }
                    if (guideImage == null)
                    {
                        if (builtinGuide == null)
                            builtinGuide = UnityEngine.Object.FindFirstObjectByType<GuideView2DBehaviour>(FindObjectsInactive.Include);
                        if (builtinGuide != null)
                        {
                            var r = builtinGuide.GetComponentInChildren<Renderer>(true);
                            if (r != null && r.sharedMaterial != null) guideImage = r.sharedMaterial.mainTexture;
                        }
                    }
                }
                return guideImage;
            }
        }

        // 추적 중 실루엣이 따라갈 대상 위치 (없으면 null) — 스캔 UI 가 화면 좌표로 투영해 쓴다
        public Transform TargetTransform => target != null ? target.transform : null;

        ModelTargetBehaviour target;
        Coroutine starting;
        bool solid;          // 지금 프레임에 실제 관측 중인가
        bool parallaxHinted; // 시차 안내 토스트는 스캔당 1회
        float lastWaitLog;
        bool verifying;      // 다리 검증 캡처가 도는 중 (중복 진입 방지)
        float lastLegToast;  // 다리/화면 안내 토스트 최소 간격용

        // 최근 관측 샘플 (holdSeconds 창) — 품질 검사용.
        // ⚠ 전부 '피규어 기준 상대좌표'로 기록한다 (2026-08-31 저녁 실기):
        //   이 씬의 Vuforia 는 카메라가 월드 원점에 고정이고 대상이 움직이는 좌표계라,
        //   월드 절대좌표로 재면 폰을 움직이는 것 자체가 '대상 흔들림 12~27cm'로 잡혀
        //   정품도 무한 리셋됐다. camLocal(대상 로컬에서 본 카메라 위치)은 좌표계와 무관하다.
        struct Sample { public float t; public Vector3 camLocal; }
        readonly System.Collections.Generic.List<Sample> window = new System.Collections.Generic.List<Sample>();

        public void ResetDetection()
        {
            HasDetected = false;
            IsTracking = false;
            solid = false;
            verifying = false;
            HoldProgress = 0f;
            ParallaxProgress = 0f;
            window.Clear();
        }

        // 인식 시작. 카메라(VuforiaBehaviour 가 붙은 ARCamera)는 호출부가 켠다.
        // ⚠ 엔진은 여기서 초기화한다 — 앱 시작 자동 초기화(delayedInitialization=0)로 두면
        //   뷰포리아 초기화가 카메라를 잡아(InitOnCameraReady) ARCore 가 카메라를 못 얻고
        //   AR 소환이 통째로 죽는다(2026-08-27 실기: 아바타 안 뜸). 설정은 지연 초기화로 바꿨다.
        public void Begin()
        {
            if (VuforiaApplication.Instance != null && !VuforiaApplication.Instance.IsInitialized)
                VuforiaApplication.Instance.Initialize();
            if (starting == null && target == null)
                starting = StartCoroutine(CreateTarget());
            if (target != null) target.enabled = true;
        }

        // 인식 중단 + 엔진 해제 — 카메라를 돌려줘야 ARCore(AR 소환)가 쓸 수 있다.
        // Deinit 하면 타깃도 죽으므로 버리고, 다음 Begin 에서 다시 만든다.
        public void End()
        {
            IsTracking = false;
            if (target != null)
            {
                target.OnTargetStatusChanged -= OnStatus;
                target = null;
            }
            if (starting != null) { StopCoroutine(starting); starting = null; }
            if (VuforiaApplication.Instance != null && VuforiaApplication.Instance.IsInitialized)
                VuforiaApplication.Instance.Deinit();
        }

        IEnumerator CreateTarget()
        {
            // 엔진 초기화 대기 (delayedInitialization=0 이라 앱 시작 때 스스로 초기화된다)
            float wait = 0f;
            while (VuforiaApplication.Instance == null || !VuforiaApplication.Instance.IsInitialized)
            {
                wait += Time.deltaTime;
                if (wait > 15f)
                {
                    Debug.LogWarning("[CubeScan] Vuforia 초기화 15초 초과 — 인식 불가(라이선스/권한 확인)");
                    starting = null;
                    yield break;
                }
                yield return null;
            }
            while (VuforiaBehaviour.Instance == null)   // ARCamera 가 켜져야 생긴다
                yield return null;

            var task = VuforiaBehaviour.Instance.ObserverFactory
                           .CreateModelTargetAsync(dataSetPath, targetName);
            while (!task.IsCompleted) yield return null;

            target = task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion ? task.Result : null;
            starting = null;
            if (target == null)
            {
                Debug.LogWarning($"[CubeScan] Model Target 생성 실패 ({dataSetPath})");
                yield break;
            }
            target.OnTargetStatusChanged += OnStatus;

            // 가이드 실루엣 — Vuforia 내장 오버레이(GuideView2D)는 추적되면 강제로 숨고 크기도
            // 못 맞춘다("가이드 없어지게 하지 마 / 프레임 안에 딱" 2026-08-31 유저 지시).
            // 그래서 내장 오버레이는 끄고, 실루엣 텍스처만 받아 스캔 UI 가 프레임 안에
            // 인식이 끝날 때까지 계속 그린다 (CubeScanController.EnsureGuideUi).
            try { target.GuideViewMode = ModelTargetBehaviour.GuideViewDisplayMode.GuideView2D; }
            catch (Exception e) { Debug.LogWarning("[CubeScan] 가이드뷰 설정 실패: " + e.Message); }

            Debug.Log($"[CubeScan] Model Target 준비 완료 '{targetName}'");
        }

        void OnStatus(ObserverBehaviour b, TargetStatus s)
        {
            // EXTENDED_TRACKED 는 '한 번 잡았던 자리를 장치 추적으로 이어가는' 상태라
            // 실물이 안 보여도 유지된다 — 최초 인식의 근거로 쓰면 안 된다.
            // StatusInfo 도 NORMAL 만 인정한다(INITIALIZING 등 흔들리는 관측 제외).
            bool now = s.Status == Status.TRACKED && s.StatusInfo == StatusInfo.NORMAL;
            IsTracking = now;
            if (!now) { window.Clear(); HoldProgress = 0f; ParallaxProgress = 0f; }   // 끊기면 처음부터
            solid = now;
        }

        // 성공 판정은 매 프레임 잰다 — OnStatus 는 상태가 '바뀔 때'만 오기 때문이다.
        void Update()
        {
            // 내장 가이드 오버레이 숨기기 — 우리 UI(프레임 안 실루엣)가 대신 그린다.
            // ⚠ 오브젝트를 끄면 Vuforia 가 실루엣 텍스처를 안 만들어서 '렌더러만' 끈다.
            //   또 Vuforia 가 오버레이를 지웠다 새로 만들기도 해서(초반 가이드 2개 실기)
            //   참조가 죽으면 다시 찾아 매 프레임 눕힌다 — 처음부터 화면엔 안 보인다.
            if (builtinGuide == null)
                builtinGuide = UnityEngine.Object.FindFirstObjectByType<GuideView2DBehaviour>(FindObjectsInactive.Include);
            if (builtinGuide != null)
                foreach (var r in builtinGuide.GetComponentsInChildren<Renderer>(true))
                    if (r.enabled) r.enabled = false;

            if (HasDetected || !solid || target == null) return;

            var cam = Camera.main;
            if (cam == null) return;
            // ⚠ InverseTransformPoint 금지 — 데이터셋 내부 스케일이 미터가 아니라(실측: 발끝~머리
            //   8cm 가 화면 9px 로 붕괴, 이동 1385cm 등 괴물 값) 거리·각도가 전부 오염된다.
            //   회전만 적용하고 거리는 월드 미터 그대로 쓴다.
            var tr = target.transform;
            window.Add(new Sample
            {
                t = Time.time,
                // ÷WorldUnitsPerMeter: cm 월드를 실제 미터로 정규화 — 임계값(m)이 그대로 맞는다
                camLocal = Quaternion.Inverse(tr.rotation) * (cam.transform.position - tr.position)
                           / WorldUnitsPerMeter,
            });
            // holdSeconds 보다 오래된 샘플은 버린다 (굴러가는 창)
            while (window.Count > 0 && Time.time - window[0].t > holdSeconds) window.RemoveAt(0);

            // 진행 표시 갱신 — 창이 다 차기 전에도 매 프레임 UI 에 준다
            float span = window.Count > 0 ? Time.time - window[0].t : 0f;
            HoldProgress = Mathf.Clamp01(span / holdSeconds);
            {
                float ang = 0f;
                Vector3 d0 = window[0].camLocal.normalized;
                foreach (var w in window)
                    if (w.camLocal.sqrMagnitude > 1e-6f)
                        ang = Mathf.Max(ang, Vector3.Angle(d0, w.camLocal.normalized));
                ParallaxProgress = minViewAngleDeg > 0f ? Mathf.Clamp01(ang / minViewAngleDeg) : 1f;
            }

            if (window.Count < 2 || Time.time - window[0].t < holdSeconds - 0.05f) return;

            // ② 거리 흔들림 — 폰을 대상 주위로 돌려도 대상까지의 '거리'는 손떨림 몇 cm 안에서
            //   유지된다. 사진·부분 형상은 포즈 추정이 튀어 거리가 크게 널뛴다(실측 5.7~91cm).
            //   트래킹 딸꾹질 1~2프레임이 창 전체를 죽이지 않게, 중앙값에서 먼 15% 는 버리고 잰다.
            var dists = new System.Collections.Generic.List<float>(window.Count);
            foreach (var w in window) dists.Add(w.camLocal.magnitude);
            var sorted = new System.Collections.Generic.List<float>(dists);
            sorted.Sort();
            float median = sorted[sorted.Count / 2];
            var devs = new System.Collections.Generic.List<float>(dists);
            devs.Sort((a, b) => Mathf.Abs(a - median).CompareTo(Mathf.Abs(b - median)));
            int keep = Mathf.Max(2, Mathf.FloorToInt(devs.Count * 0.85f));
            float dMin = devs[0], dMax = devs[0];
            for (int i = 0; i < keep; i++) { dMin = Mathf.Min(dMin, devs[i]); dMax = Mathf.Max(dMax, devs[i]); }
            float posJitter = dMax - dMin;

            // ③ 카메라 실이동 + ④ 보는 각도 변화 — 전부 대상 로컬 기준이라 좌표계와 무관.
            Vector3 cMin = window[0].camLocal, cMax = window[0].camLocal;
            float viewAngle = 0f;
            Vector3 dir0 = window[0].camLocal.normalized;
            foreach (var w in window)
            {
                cMin = Vector3.Min(cMin, w.camLocal); cMax = Vector3.Max(cMax, w.camLocal);
                if (w.camLocal.sqrMagnitude < 1e-6f) continue;
                float a = Vector3.Angle(dir0, w.camLocal.normalized);
                if (a > viewAngle) viewAngle = a;
            }
            float camMove = (cMax - cMin).magnitude;

            if (posJitter > maxPosJitterM)
            {
                // 거리가 널뛰는 매칭(사진·부분 형상 의심) — 창을 비우고 처음부터
                Debug.Log($"[CubeScan] 관측 불안정 — 거리 흔들림 {posJitter * 100f:0.0}cm → 다시 잰다");
                window.Clear();
                return;
            }
            // ③④ 시차 확보까지 대기 — 폰을 대상 주위로 조금 움직여야 찬다.
            // 조용히 기다리면 유저는 '안 된다'로 느낀다 → 안내 토스트 1회 + 주기 로그.
            if ((minCamMoveM > 0f && camMove < minCamMoveM) ||
                (minViewAngleDeg > 0f && viewAngle < minViewAngleDeg))
            {
                if (!parallaxHinted)
                {
                    parallaxHinted = true;
                    Toast.Show("피규어를 비춘 채 폰을 천천히 옆으로 움직여 주세요");
                }
                if (Time.time - lastWaitLog > 1.5f)
                {
                    lastWaitLog = Time.time;
                    Debug.Log($"[CubeScan] 시차 대기 — 이동 {camMove * 100f:0.0}cm/{minCamMoveM * 100f:0}cm · " +
                              $"시야각 {viewAngle:0.0}°/{minViewAngleDeg:0}°");
                }
                return;
            }

            // 기하 검사는 전부 통과 — 마지막으로 다리(전체) 픽셀 검증을 하고 확정한다.
            if (!verifying)
            {
                verifying = true;
                StartCoroutine(CoVerifyWholeFigure(posJitter, camMove, viewAngle));
            }
        }

        // ⑤ 전체(다리) 검증 — 프레임 끝에 화면을 캡처해 다리 구역 vs 옆 배경 색을 비교한다.
        //   Vuforia 는 가림에 강하도록 설계돼 상반신만 보여도 전체 포즈를 추정한다(다리 잘림 =
        //   가림으로 취급). 그래서 데이터셋에 전체 형상을 넣어도 부분 실물이 통과한다 —
        //   '그 자리에 진짜 다리 픽셀이 있나'는 우리가 직접 봐야 한다.
        IEnumerator CoVerifyWholeFigure(float posJitter, float camMove, float viewAngle)
        {
            yield return new WaitForEndOfFrame();

            var cam = Camera.main;
            if (HasDetected || !solid || target == null || cam == null)
            { verifying = false; yield break; }

            var t = target.transform;
            float H = figureHeightM, W = figureWidthM;

            // 머리끝~발끝이 화면 안에 있어야 한다 (전체를 비추게 강제).
            // WorldAt: 회전만 적용한 월드 미터 오프셋 — 데이터셋 스케일에 오염되지 않는다.
            Vector3 headW = WorldAt(t, new Vector3(0f, H * 0.95f, 0f));
            Vector3 footW = WorldAt(t, Vector3.zero);
            Vector3 headS = cam.WorldToScreenPoint(headW);
            Vector3 footS = cam.WorldToScreenPoint(footW);
            if (headS.z <= 0f || footS.z <= 0f ||
                !OnScreen(headS) || !OnScreen(footS))
            {
                if (Time.time - lastLegToast > 2.5f)
                {
                    lastLegToast = Time.time;
                    Toast.Show("피규어 전체가 화면에 들어오게 비춰 주세요");
                }
                window.Clear();
                verifying = false;
                yield break;
            }

            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            float diff; float meanDiff;
            try
            {
                // 다리 구역 3높이 × 좌우/중앙 3점 vs 같은 높이의 몸통 바깥(배경) 2점.
                // ⚠ 판정은 '최대값' — 다리는 가늘어서 샘플 일부(특히 가운데 = 두 다리 사이)는
                //   배경에 떨어진다. 평균은 그만큼 희석돼 정품도 떨어졌다(실측 0.046~0.067).
                //   다리가 잘렸으면 '모든' 점이 배경이라 최대값도 낮다.
                float[] hs = { H * legZoneTop * 0.25f, H * legZoneTop * 0.55f, H * legZoneTop * 0.85f };
                float[] xs = { -W * 0.22f, 0f, W * 0.22f };
                float sum = 0f; int n = 0; diff = 0f;
                foreach (float h in hs)
                {
                    Color bg = (SampleAt(shot, cam, t, new Vector3(-W * 1.6f, h, 0f)) +
                                SampleAt(shot, cam, t, new Vector3(W * 1.6f, h, 0f))) * 0.5f;
                    foreach (float x in xs)
                    {
                        Color leg = SampleAt(shot, cam, t, new Vector3(x, h, 0f));
                        float d = (Mathf.Abs(leg.r - bg.r) + Mathf.Abs(leg.g - bg.g) + Mathf.Abs(leg.b - bg.b)) / 3f;
                        if (d > diff) diff = d;
                        sum += d; n++;
                    }
                }
                meanDiff = n > 0 ? sum / n : 1f;
            }
            finally { Destroy(shot); }

            if (diff < minLegDiff)
            {
                Debug.Log($"[CubeScan] 다리 검증 실패 — 다리 자리가 배경과 같음 (최대 {diff:0.000} 평균 {meanDiff:0.000} < {minLegDiff:0.000}) · " +
                          $"발끝화면({footS.x:0},{footS.y:0}) 머리화면({headS.x:0},{headS.y:0})");
                if (Time.time - lastLegToast > 2.5f)
                {
                    lastLegToast = Time.time;
                    Toast.Show("피규어 전체(다리까지)가 보이는지 확인해 주세요");
                }
                window.Clear();
                verifying = false;
                yield break;
            }

            HasDetected = true;
            DetectedPose = new Pose(t.position, t.rotation);
            Debug.Log($"[CubeScan] 피규어 인식 확정 — 연속 {holdSeconds:0.0}s · " +
                      $"거리흔들림 {posJitter * 100f:0.0}cm · " +
                      $"카메라 이동 {camMove * 100f:0.0}cm · 시야각 {viewAngle:0.0}° · " +
                      $"다리색차 최대 {diff:0.000} 평균 {meanDiff:0.000} · " +
                      $"발끝화면({footS.x:0},{footS.y:0}) 머리화면({headS.x:0},{headS.y:0})");
            verifying = false;
            OnDetected?.Invoke(DetectedPose);
        }

        static bool OnScreen(Vector3 s)
            => s.x >= 8f && s.x <= Screen.width - 8f && s.y >= 8f && s.y <= Screen.height - 8f;

        // 회전만 적용한 오프셋 — 미터 입력을 cm 월드로 변환해 얹는다 (TransformPoint 는 못 쓴다)
        static Vector3 WorldAt(Transform t, Vector3 localMeters)
            => t.position + t.rotation * (localMeters * WorldUnitsPerMeter);

        // 대상 기준 오프셋(월드 미터)의 한 점을 화면에 투영해 3×3 픽셀 평균색을 읽는다
        static Color SampleAt(Texture2D shot, Camera cam, Transform t, Vector3 local)
        {
            Vector3 s = cam.WorldToScreenPoint(WorldAt(t, local));
            int px = Mathf.Clamp(Mathf.RoundToInt(s.x), 1, shot.width - 2);
            int py = Mathf.Clamp(Mathf.RoundToInt(s.y), 1, shot.height - 2);
            Color acc = Color.black;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    acc += shot.GetPixel(px + dx, py + dy);
            return acc / 9f;
        }

        void OnDestroy()
        {
            if (target != null) target.OnTargetStatusChanged -= OnStatus;
        }
    }
}
