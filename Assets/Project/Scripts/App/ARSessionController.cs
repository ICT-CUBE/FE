using UnityEngine;
using UnityEngine.Video;
using UnityEngine.XR.ARFoundation;

namespace ARZone
{
    // ARFoundation(ARCore) 세션을 런타임에 띄우고 내린다.
    // 무대(StagePrefab)는 배치 시점에 따로 생성해 월드에 남긴다.
    // 피규어 인식(Vuforia)과는 절대 동시에 켜지 않는다 — 카메라를 서로 뺏는다.
    public class ARSessionController : MonoBehaviour
    {
        [SerializeField] GameObject arRigPrefab;   // AR Session + XR Origin
        [SerializeField] GameObject stagePrefab;   // StageGrid + StageScreen
        [SerializeField] Camera uiCamera;          // 평소 쓰는 Main Camera. AR 중엔 끈다.

        [Header("무대 크기 — 놓는 거리에 맞춰 화면 안에 들어오게")]
        [SerializeField, Range(0.3f, 1f)] float viewFitRatio = 0.75f;  // 화면의 몇 %까지 채울지
        [SerializeField] float minStageScale = 0.1f;
        [SerializeField] float maxStageScale = 1f;

        GameObject rig;
        GameObject stage;
        AudioListener uiListener;

        public bool IsRunning => rig != null;
        public bool StagePlaced { get; private set; }

        public ARPlaneManager PlaneManager { get; private set; }
        public ARRaycastManager RaycastManager { get; private set; }
        public Transform Stage => stage != null ? stage.transform : null;
        public VideoPlayer Video { get; private set; }
        public GameObject StageGrid { get; private set; }

        public void StartSession()
        {
            if (rig != null) return;
            if (arRigPrefab == null) { Debug.LogError("[ARSession] ARRig 프리팹이 비어 있음"); return; }

            if (uiCamera != null)
            {
                uiCamera.enabled = false;
                uiListener = uiCamera.GetComponent<AudioListener>();
                if (uiListener != null) uiListener.enabled = false;   // AudioListener 중복 경고 방지
            }

            rig = Instantiate(arRigPrefab);
            rig.SetActive(true);

            PlaneManager = rig.GetComponentInChildren<ARPlaneManager>(true);
            RaycastManager = rig.GetComponentInChildren<ARRaycastManager>(true);
            // 바닥만 찾는다. 벽·책상 같은 수직면/위쪽면은 무시.
            if (PlaneManager != null)
                PlaneManager.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal;
            if (PlaneManager == null) Debug.LogError("[ARSession] XR Origin 에 ARPlaneManager 가 없음");
            if (RaycastManager == null) Debug.LogError("[ARSession] XR Origin 에 ARRaycastManager 가 없음");
        }

        public void StopSession()
        {
            if (stage != null) Destroy(stage);
            if (rig != null) Destroy(rig);
            rig = null; stage = null;
            PlaneManager = null; RaycastManager = null; Video = null; StageGrid = null;
            StagePlaced = false;

            if (uiCamera != null) uiCamera.enabled = true;
            if (uiListener != null) uiListener.enabled = true;
            uiListener = null;
        }

        // 평면 탐색 on/off. 배치가 끝나면 꺼서 CPU/배터리를 아낀다.
        public void SetScanning(bool on)
        {
            if (PlaneManager != null) PlaneManager.enabled = on;
        }

        // 무대를 그 자세로 놓는다. 없으면 만들고, 있으면 옮긴다.
        public void PlaceStage(Pose pose)
        {
            if (stagePrefab == null) { Debug.LogError("[ARSession] StagePrefab 이 비어 있음"); return; }

            if (stage == null)
            {
                // 평면 hit pose 의 회전을 그대로 쓰면 무대가 90도 눕는다.
                // (바닥 평면은 +Z 가 위를 향하는 자세로 온다)
                // 항상 수직으로 세우고, 사용자 쪽을 바라보게만 한다.
                stage = Instantiate(stagePrefab, pose.position, Quaternion.identity);

                var cam = Camera.main;
                if (cam != null)
                {
                    var toCam = cam.transform.position - pose.position;
                    toCam.y = 0f;
                    if (toCam.sqrMagnitude > 0.0001f)
                        stage.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                }

                var grid = stage.transform.Find("StageGrid");
                var scr = stage.transform.Find("StageScreen");

                // 프리팹 값이 어긋나 있어도 최종 자세를 코드에서 못 박는다.
                // 그리드는 바닥에 눕고(X 90), 스크린은 무대 정면을 보고 선다(회전 없음).
                if (grid != null) grid.localRotation = Quaternion.Euler(90f, 0f, 0f);
                if (scr != null) scr.localRotation = Quaternion.identity;

                Debug.Log($"[ARSession] 무대={stage.transform.eulerAngles} " +
                          $"그리드={(grid != null ? grid.localEulerAngles.ToString() : "없음")} " +
                          $"스크린={(scr != null ? scr.localEulerAngles.ToString() : "없음")} " +
                          $"pose={pose.rotation.eulerAngles} cam={(Camera.main != null)}");

                Video = stage.GetComponentInChildren<VideoPlayer>(true);
                if (grid != null) StageGrid = grid.gameObject;
                if (Video == null) Debug.LogError("[ARSession] StagePrefab 안에 VideoPlayer 가 없음");

                FitStageToView();
            }
            else
            {
                // 회전은 사용자가 손으로 돌린 값을 유지하고 위치만 옮긴다
                stage.transform.position = pose.position;
            }
        }

        // 무대가 카메라 화각 밖으로 넘치지 않게 균등 축소한다.
        // 프리팹은 실제 크기(그리드 3m, 스크린 2m)라서 가까이 놓으면 화면을 벗어난다.
        public void FitStageToView()
        {
            if (stage == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            stage.transform.localScale = Vector3.one;
            var b = WorldBounds(stage);
            if (b.size.x <= 0.0001f || b.size.y <= 0.0001f) return;

            float dist = Mathf.Max(0.3f, Vector3.Distance(cam.transform.position, stage.transform.position));
            float halfH = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * cam.aspect;

            float s = Mathf.Min((2f * halfW * viewFitRatio) / b.size.x,
                                (2f * halfH * viewFitRatio) / b.size.y);
            s = Mathf.Clamp(s, minStageScale, maxStageScale);
            stage.transform.localScale = Vector3.one * s;

            Debug.Log($"[ARSession] 무대 스케일 {s:0.00} (거리 {dist:0.00}m, 원본 {b.size.x:0.00}×{b.size.y:0.00}m)");
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // 배치 확정 — 더 이상 따라다니지 않게 한다.
        public void ConfirmPlacement()
        {
            StagePlaced = true;
            FitStageToView();   // 드래그로 옮겼을 수 있으니 최종 위치에서 한 번 더
            SetScanning(false);
            //ShowGrid(false);
        }

        public void ShowGrid(bool on)
        {
            if (StageGrid != null) StageGrid.SetActive(on);
        }

        void OnDestroy() => StopSession();
    }
}