using UnityEngine;

namespace ARZone
{
    // VRM 아바타를 목표 지점까지 수평 이동시킨다.
    // Animator 는 선택 사항 — 없으면 이동만 하고 애니메이션은 건너뛴다.
    public class VrmAvatarController : MonoBehaviour, IAvatarController
    {
        [Header("이동")]
        [SerializeField] float moveSpeed = 0.35f;      // m/s
        [SerializeField] float turnSpeed = 540f;       // deg/s
        [SerializeField] float arriveDistance = 0.02f;

        [Header("애니메이션 (선택)")]
        [SerializeField] Animator animator;
        [SerializeField] string speedParam = "Speed";

        Vector3 target;
        bool walking;
        bool hasSpeedParam;
        int speedHash;

        public Transform Transform => transform;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            speedHash = Animator.StringToHash(speedParam);
            hasSpeedParam = HasParameter(animator, speedParam);
            target = transform.position;
        }

        public void WalkTo(Vector3 worldTarget)
        {
            target = worldTarget;
            target.y = transform.position.y;   // 수평 이동만
            walking = true;
        }

        // 소환 직후 사용자(카메라)를 바라보게 한다.
        public void FaceTo(Vector3 worldPoint)
        {
            var dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        void Update()
        {
            float speed = 0f;

            if (walking)
            {
                var to = target - transform.position;
                to.y = 0f;

                if (to.magnitude <= arriveDistance)
                {
                    walking = false;
                }
                else
                {
                    var dir = to.normalized;
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        Quaternion.LookRotation(dir),
                        turnSpeed * Time.deltaTime);
                    transform.position += dir * moveSpeed * Time.deltaTime;
                    speed = moveSpeed;
                }
            }

            if (hasSpeedParam) animator.SetFloat(speedHash, speed);
        }

        // 없는 파라미터에 SetFloat 하면 콘솔이 매 프레임 경고로 도배된다.
        static bool HasParameter(Animator a, string name)
        {
            if (a == null || string.IsNullOrEmpty(name)) return false;
            foreach (var p in a.parameters)
                if (p.name == name) return true;
            return false;
        }
    }
}