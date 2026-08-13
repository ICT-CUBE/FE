using UnityEngine;

namespace ARZone
{
    // 흐름: 인식(Mock/Vuforia) → 아바타 소환 → 더블탭한 지점으로 걷기.
    // 인식기·입력·아바타를 모두 인터페이스로 다루므로 구현체만 교체하면 확장된다.
    public class SummonController : MonoBehaviour
    {
        [Header("구현체 연결 (인스펙터에서 드래그)")]
        [SerializeField] MonoBehaviour detectorSource; // IObjectDetector
        [SerializeField] MonoBehaviour inputSource;    // IPointerInput

        [Header("아바타")]
        [SerializeField] GameObject avatarPrefab;      // 비워두면 임시 스틱맨 사용
        [SerializeField] float figureHeight = 0.12f;   // 스틱맨 전용 높이(m)
        [SerializeField] float spawnDistance = 0.4f;   // 카메라 앞 거리(m)
        [SerializeField] float spawnDrop = 0.08f;      // 시선보다 아래로 내리는 양(m)

        IObjectDetector detector;
        IPointerInput input;
        IAvatarController avatar;

        void Awake()
        {
            detector = Resolve<IObjectDetector>(detectorSource);
            input = Resolve<IPointerInput>(inputSource);
            if (detector == null) Debug.LogError("IObjectDetector 구현체를 찾지 못했습니다.");
            if (input == null) Debug.LogError("IPointerInput 구현체를 찾지 못했습니다.");
        }

        void OnEnable()
        {
            if (detector != null) detector.OnDetected += HandleDetected;
            if (input != null) input.OnDoubleTap += HandleDoubleTap;
        }

        void OnDisable()
        {
            if (detector != null) detector.OnDetected -= HandleDetected;
            if (input != null) input.OnDoubleTap -= HandleDoubleTap;
        }

        void HandleDetected(Pose pose)
        {
            if (avatar != null) Destroy(avatar.Transform.gameObject);

            var cam = Camera.main;
            Vector3 spawnPos = cam != null
                ? cam.transform.position + cam.transform.forward * spawnDistance + Vector3.down * spawnDrop
                : pose.position;

            if (avatarPrefab != null)
            {
                var go = Instantiate(avatarPrefab, spawnPos, Quaternion.identity);
                avatar = go.GetComponent<IAvatarController>();
                if (avatar == null)
                    Debug.LogError("avatarPrefab 에 IAvatarController 구현체가 없습니다.");
                else if (avatar is VrmAvatarController vrm && cam != null)
                    vrm.FaceTo(cam.transform.position);   // 소환 직후 사용자를 바라보게
            }
            else
            {
                var go = StickFigureFactory.Create(figureHeight);
                go.transform.position = spawnPos;
                avatar = go.GetComponent<IAvatarController>();
            }

            Debug.Log($"[소환] 위치={spawnPos}");
        }

        void HandleDoubleTap(Vector2 screenPos)
        {
            if (avatar == null) { Debug.Log("아직 소환 안 됨 — 먼저 인식(탭)하세요."); return; }
            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(screenPos);
            float planeY = avatar.Transform.position.y;
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return;

            float dist = (planeY - ray.origin.y) / ray.direction.y;
            if (dist <= 0f) return;

            avatar.WalkTo(ray.origin + ray.direction * dist);
            Debug.Log("걷기 시작");
        }

        static T Resolve<T>(MonoBehaviour source) where T : class
        {
            if (source is T t) return t;
            if (source != null)
            {
                var c = source.GetComponent<T>();
                if (c != null) return c;
            }
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is T found) return found;
            return null;
        }
    }
}