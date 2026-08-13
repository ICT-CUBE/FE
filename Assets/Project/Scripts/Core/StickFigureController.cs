using UnityEngine;

namespace ARZone
{
    // 스틱맨 본체: 목표 지점으로 걸어가며 팔·다리 스윙. 걷지 않을 땐 카메라를 바라봄.
    // ※ 임시 데모용. 나중에 아바타로 교체 시 이 "걷기 명령" 부분은 재사용됨.
    public class StickFigureController : MonoBehaviour, IAvatarController
    {
        [SerializeField] float speed = 0.12f;
        [SerializeField] float swingDegrees = 35f;
        [SerializeField] float swingSpeed = 9f;

        public Transform Transform => transform;

        Transform legL, legR, armL, armR;
        Quaternion baseLegL, baseLegR, baseArmL, baseArmR;
        Vector3 destination;
        bool walking;
        float phase;

        public void Init(Transform legL, Transform legR, Transform armL, Transform armR)
        {
            this.legL = legL; this.legR = legR; this.armL = armL; this.armR = armR;
            baseLegL = legL.localRotation; baseLegR = legR.localRotation;
            baseArmL = armL.localRotation; baseArmR = armR.localRotation;
        }

        public void WalkTo(Vector3 worldTarget)
        {
            transform.SetParent(null, true);
            destination = worldTarget;
            walking = true;
        }

        void Update()
        {
            if (!walking) return;

            Vector3 pos = transform.position;
            Vector3 dir = destination - pos;
            Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);

            if (dir.magnitude < 0.02f) { walking = false; ResetPose(); return; }

            transform.position = Vector3.MoveTowards(pos, destination, speed * Time.deltaTime);
            if (flatDir.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(flatDir.normalized, Vector3.up), 8f * Time.deltaTime);

            phase += Time.deltaTime * swingSpeed;
            float s = Mathf.Sin(phase) * swingDegrees;
            legL.localRotation = baseLegL * Quaternion.Euler(s, 0, 0);
            legR.localRotation = baseLegR * Quaternion.Euler(-s, 0, 0);
            armL.localRotation = baseArmL * Quaternion.Euler(-s, 0, 0);
            armR.localRotation = baseArmR * Quaternion.Euler(s, 0, 0);
        }

        void LateUpdate()
        {
            if (walking) return;
            var cam = Camera.main;
            Vector3 toCam = cam != null ? (cam.transform.position - transform.position) : -transform.forward;
            toCam.y = 0f;
            transform.rotation = toCam.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(toCam.normalized, Vector3.up)
                : Quaternion.identity;
        }

        void ResetPose()
        {
            legL.localRotation = baseLegL; legR.localRotation = baseLegR;
            armL.localRotation = baseArmL; armR.localRotation = baseArmR;
        }
    }
}