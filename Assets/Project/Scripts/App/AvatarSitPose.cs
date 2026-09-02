using UnityEngine;

namespace ARZone
{
    // 홈 화면 '앉은 자세' — 시안 sian48/_home_avatar.png 재현 (2026-08-26).
    //   양반다리(발목 교차, 무릎 양옆) · 오른팔꿈치를 오른무릎에 얹고 턱 괴기 ·
    //   왼손은 발목 앞에 내려놓기 · 고개를 오른쪽으로 살짝 기울임 · 상체 약간 앞으로.
    //
    // 프로젝트에 애니메이션 에셋이 전무해서, 휴머노이드 본을 코드로 돌려 정적 자세를 만든다.
    // 관절마다 "자식 본이 향할 목표 방향"을 주고 FromToRotation 으로 돌린다 —
    // 리그의 로컬 축 부호를 몰라도 되는 방법이다. 마지막에 발 높이를 원래 바닥에 재정렬.
    //
    // 손가락 말기: 손바닥 법선을 본 '위치'로 역산한다 — cross(중지 방향, 검지→새끼)는
    // 오른손에서 손등, 왼손에서 손바닥을 향한다(자세와 무관한 손의 카이랄리티) —
    // 그 법선 쪽으로 마디를 차례로 꺾으면 리그 축 부호 없이 주먹을 쥘 수 있다.
    //
    // ⚠ 홈/아바타홈 씬 인스턴스에만 붙일 것 — AR 소환은 서 있어야 하므로 프리팹 원본엔 붙지 않는다.
    // ⚠ 플레이 모드에서만 적용된다(씬 뷰의 T포즈는 정상).
    [RequireComponent(typeof(Animator))]
    public class AvatarSitPose : MonoBehaviour
    {
        [SerializeField, Tooltip("자세를 잡은 뒤 루트를 추가로 옮길 값(m). 소파/바닥 위치 미세조정용")]
        Vector3 nudge = Vector3.zero;

        Animator anim;

        void Start()
        {
            anim = GetComponent<Animator>();
            if (anim == null || !anim.isHuman)
            {
                Debug.LogWarning("[SitPose] 휴머노이드 Animator 가 없어 자세를 못 잡는다");
                return;
            }

            float footBefore = FootY();
            // 모델이 Y180 으로 돌아 있으므로 fwd = 카메라 쪽, right = 아바타의 오른손 쪽
            Vector3 fwd = transform.forward, right = transform.right, up = Vector3.up;

            // 상체 — 앞으로 숙이고 턱 괸 쪽(오른쪽)으로도 살짝 기울임 (시안: 꼿꼿하지 않다)
            Bend(HumanBodyBones.Spine, HumanBodyBones.Neck, up + fwd * 0.25f + right * 0.10f);

            // 다리 — 양반다리: 무릎은 옆이 아니라 앞쪽으로(시안은 컴팩트하다 — 벌리면 쩍벌이 된다),
            // 정강이를 안쪽으로 접어 발목이 몸 앞 중앙에서 교차. 시안은 왼발이 앞(위)으로 겹친다.
            // 다리 사이 구멍이 안 보이게: 무릎을 낮추고(허벅지 더 눕힘) 발목을 골반 쪽으로
            // 바짝 당겨(정강이 뒤로 접힘) 허벅지와 정강이가 맞닿게 한다.
            Bend(HumanBodyBones.LeftUpperLeg,  HumanBodyBones.LeftLowerLeg,
                 -right * 0.55f + fwd * 0.55f - up * 0.55f);
            Bend(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
                  right * 0.55f + fwd * 0.55f - up * 0.55f);
            Bend(HumanBodyBones.LeftLowerLeg,  HumanBodyBones.LeftFoot,
                  right * 0.90f + fwd * 0.15f - up * 0.30f);
            Bend(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
                 -right * 0.90f + fwd * 0.00f - up * 0.30f);

            // 발끝 — 시안: 앞발(왼)은 카메라 쪽으로 눕고, 뒷발(오른)은 반대편 허벅지 밑으로 눕는다
            Bend(HumanBodyBones.LeftFoot,  HumanBodyBones.LeftToes,
                  fwd * 0.85f - right * 0.15f - up * 0.35f);
            Bend(HumanBodyBones.RightFoot, HumanBodyBones.RightToes,
                  right * 0.70f + fwd * 0.50f - up * 0.30f);

            // 오른팔 — 팔꿈치를 오른무릎 '위'까지 확실히 뻗어 얹고, 손을 올려 턱을 괸다
            // (시안: 팔이 몸에 붙어 있지 않고 무릎-팔꿈치-손-뺨이 한 기둥이 된다)
            Bend(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
                 -up * 0.55f + fwd * 0.45f + right * 0.45f);
            Bend(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                  up * 0.90f - right * 0.28f - fwd * 0.10f);

            // 왼팔 — 아래로 내려 손을 발목 앞에
            Bend(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
                 -up * 0.80f + fwd * 0.40f - right * 0.10f);
            Bend(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                 -up * 0.45f + fwd * 0.75f + right * 0.15f);

            // 손목/손가락 — 손끝 방향을 잡고, 손바닥이 볼 곳을 지정한 뒤, 손가락을 만다.
            // (FromToRotation 은 손끝 방향만 잡고 손바닥 롤은 방치라 — 명시하지 않으면
            //  손바닥이 바깥을 본 채 말려서 이상해진다)
            Bend(HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal,
                  up * 0.90f + fwd * 0.10f - right * 0.10f);          // 손끝이 뺨을 따라 위로
            Bend(HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal,
                  fwd * 0.55f - up * 0.75f);                          // 손끝이 발목 위로 늘어짐
            var headT = anim.GetBoneTransform(HumanBodyBones.Head);
            var rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (headT != null && rHand != null)
                PalmTo(true, (headT.position - rHand.position).normalized);  // 턱 괸 손: 손바닥이 뺨을
            PalmTo(false, -up);                                              // 내린 손: 손바닥이 바닥을
            CurlFingers(true,  22f, 30f, 20f);   // 오른손(턱): 가볍게
            CurlFingers(false, 30f, 38f, 25f);   // 왼손(발목): 자연스럽게 늘어진 정도

            // 고개 — 괸 손 쪽으로 확실히 기울여 얹는다 (시안: 살짝이 아니라 한눈에 보이게 기운다)
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
                head.rotation = Quaternion.FromToRotation(
                    head.up, (up + right * 0.48f + fwd * 0.10f).normalized) * head.rotation;

            // 발이 원래 바닥 높이로 돌아오게 루트를 내린다 — 내려간 양이 곧 '앉은 깊이'
            float footAfter = FootY();
            transform.position += Vector3.up * (footBefore - footAfter) + nudge;
        }

        // parent 본을 돌려 (child − parent) 방향이 dir 이 되게 한다
        void Bend(HumanBodyBones parent, HumanBodyBones child, Vector3 dir)
        {
            var p = anim.GetBoneTransform(parent);
            var c = anim.GetBoneTransform(child);
            if (p == null || c == null) return;
            Vector3 cur = (c.position - p.position).normalized;
            p.rotation = Quaternion.FromToRotation(cur, dir.normalized) * p.rotation;
        }

        // 손바닥 법선 — cross(중지, 검지→새끼)는 오른손에서 손등, 왼손에서 손바닥을 향한다
        // (자세와 무관한 손의 카이랄리티). 실패하면 zero.
        Vector3 PalmNormal(bool rightHand)
        {
            var hand = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            var idx  = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            var mid  = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            var pky  = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            if (hand == null || idx == null || mid == null || pky == null) return Vector3.zero;
            Vector3 palm = Vector3.Cross((mid.position - hand.position).normalized,
                                         (pky.position - idx.position).normalized).normalized;
            return rightHand ? -palm : palm;
        }

        // 손끝 축은 그대로 두고 손목을 굴려 손바닥이 target 쪽을 보게 한다
        void PalmTo(bool rightHand, Vector3 target)
        {
            var hand = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            var mid  = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Vector3 palm = PalmNormal(rightHand);
            if (hand == null || mid == null || palm == Vector3.zero) return;
            Vector3 axis = (mid.position - hand.position).normalized;          // 손끝 방향(회전축)
            Vector3 a = Vector3.ProjectOnPlane(palm, axis);
            Vector3 b = Vector3.ProjectOnPlane(target, axis);
            if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return;
            hand.rotation = Quaternion.AngleAxis(
                Vector3.SignedAngle(a, b, axis), axis) * hand.rotation;
        }

        // 엄지 제외 네 손가락을 손바닥 쪽으로 만다. 각도는 마디(기절/중절/말절) 순.
        void CurlFingers(bool rightHand, float proximal, float intermediate, float distal)
        {
            Vector3 palm = PalmNormal(rightHand);
            if (palm == Vector3.zero) return;

            var fingers = rightHand
                ? new[] { (HumanBodyBones.RightIndexProximal,  HumanBodyBones.RightIndexIntermediate,  HumanBodyBones.RightIndexDistal),
                          (HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal),
                          (HumanBodyBones.RightRingProximal,   HumanBodyBones.RightRingIntermediate,   HumanBodyBones.RightRingDistal),
                          (HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal) }
                : new[] { (HumanBodyBones.LeftIndexProximal,  HumanBodyBones.LeftIndexIntermediate,  HumanBodyBones.LeftIndexDistal),
                          (HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal),
                          (HumanBodyBones.LeftRingProximal,   HumanBodyBones.LeftRingIntermediate,   HumanBodyBones.LeftRingDistal),
                          (HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal) };

            foreach (var (b1, b2, b3) in fingers)
            {
                CurlJoint(b1, b2, palm, proximal);
                CurlJoint(b2, b3, palm, intermediate);
                CurlJoint(b3, HumanBodyBones.LastBone, palm, distal);   // 말절은 축을 물려받아 회전
            }
        }

        // 마디를 손바닥 법선 쪽으로 deg 만큼 꺾는다. child 가 없으면(말절) 부모 마디 방향을 축 삼는다.
        void CurlJoint(HumanBodyBones bone, HumanBodyBones child, Vector3 palm, float deg)
        {
            var b = anim.GetBoneTransform(bone);
            if (b == null) return;
            Vector3 dir;
            if (child != HumanBodyBones.LastBone && anim.GetBoneTransform(child) != null)
                dir = (anim.GetBoneTransform(child).position - b.position).normalized;
            else if (b.parent != null)
                dir = (b.position - b.parent.position).normalized;
            else return;
            Vector3 axis = Vector3.Cross(dir, palm);
            if (axis.sqrMagnitude < 1e-6f) return;
            b.rotation = Quaternion.AngleAxis(deg, axis.normalized) * b.rotation;
        }

        float FootY()
        {
            var l = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            var r = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            float y = float.MaxValue;
            if (l != null) y = Mathf.Min(y, l.position.y);
            if (r != null) y = Mathf.Min(y, r.position.y);
            return y == float.MaxValue ? 0f : y;
        }
    }
}
