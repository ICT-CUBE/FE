using UnityEngine;

namespace ARZone
{
    // AR 소환 아바타 V 사인 포즈 (2026-08-31 유저: "ar소환에서 제니 v하고 있는 것처럼").
    // AvatarPoseDirector 는 홈 무대(AvataStage) 전용이라, AR 에 소환된 인스턴스에 쓰는
    // 독립 유틸로 뺐다 — 같은 FromToRotation 방식(리그 축 부호 몰라도 됨).
    public static class AvatarVSign
    {
        public static void Apply(GameObject go)
        {
            if (go == null) return;
            var anim = go.GetComponentInChildren<Animator>(true);
            if (anim == null || !anim.isHuman) return;

            var root = anim.transform;
            Vector3 fwd = root.forward, right = root.right, up = Vector3.up;

            // 오른팔: 팔꿈치를 옆·위로 올리고 손이 머리 옆에 오게, 손바닥은 앞(카메라)으로
            Bend(anim, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
                 fwd * 0.30f + right * 0.80f + up * 0.10f);
            Bend(anim, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                 up * 0.95f + fwd * 0.20f - right * 0.10f);
            PalmTo(anim, true, fwd);

            // 손가락: 검지·중지 펴기, 약지·소지 말기 = V
            Curl(anim, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal, 3f, 4f, 2f);
            Curl(anim, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal, 3f, 4f, 2f);
            Curl(anim, HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal, 78f, 88f, 60f);
            Curl(anim, HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal, 82f, 90f, 62f);
            Curl(anim, HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal, 45f, 55f, 40f);

            // 왼팔은 자연스럽게 내림
            Bend(anim, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, (-up - right * 0.15f).normalized);
            Bend(anim, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, (-up - right * 0.10f + fwd * 0.08f).normalized);

            // 머리 살짝 기울임
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
                head.rotation = Quaternion.FromToRotation(head.up, (up - right * 0.12f).normalized) * head.rotation;
        }

        static void Bend(Animator anim, HumanBodyBones parent, HumanBodyBones child, Vector3 dir)
        {
            var p = anim.GetBoneTransform(parent);
            var c = anim.GetBoneTransform(child);
            if (p == null || c == null) return;
            Vector3 cur = (c.position - p.position).normalized;
            p.rotation = Quaternion.FromToRotation(cur, dir.normalized) * p.rotation;
        }

        static Vector3 PalmNormal(Animator anim, bool rightHand)
        {
            var hand = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            var idx = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            var mid = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            var pky = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            if (hand == null || idx == null || mid == null || pky == null) return Vector3.zero;
            Vector3 palm = Vector3.Cross((mid.position - hand.position).normalized,
                                         (pky.position - idx.position).normalized).normalized;
            return rightHand ? -palm : palm;
        }

        static void PalmTo(Animator anim, bool rightHand, Vector3 target)
        {
            var hand = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            var mid = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Vector3 palm = PalmNormal(anim, rightHand);
            if (hand == null || mid == null || palm == Vector3.zero) return;
            Vector3 axis = (mid.position - hand.position).normalized;
            Vector3 a = Vector3.ProjectOnPlane(palm, axis);
            Vector3 b = Vector3.ProjectOnPlane(target, axis);
            if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return;
            hand.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(a, b, axis), axis) * hand.rotation;
        }

        static void Curl(Animator anim, HumanBodyBones b1, HumanBodyBones b2, HumanBodyBones b3,
                         float a1, float a2, float a3)
        {
            Vector3 palm = PalmNormal(anim, true);
            if (palm == Vector3.zero) return;
            CurlJoint(anim, b1, palm, a1);
            CurlJoint(anim, b2, palm, a2);
            CurlJoint(anim, b3, palm, a3);
        }

        static void CurlJoint(Animator anim, HumanBodyBones bone, Vector3 palm, float deg)
        {
            var t = anim.GetBoneTransform(bone);
            if (t == null || t.childCount == 0) return;
            Vector3 dir = (t.GetChild(0).position - t.position).normalized;
            Vector3 axis = Vector3.Cross(dir, -palm).normalized;
            if (axis.sqrMagnitude < 1e-6f) return;
            t.rotation = Quaternion.AngleAxis(deg, axis) * t.rotation;
        }
    }
}
