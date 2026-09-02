using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 아바타 3D 무대(AvataStage) 하나를 여러 화면이 나눠 쓴다 (2026-08-27).
    //   Home·VoiceHome  = 앉아 턱 괴기   (시안 sian48/_home_avatar · sian92/음성 대화 카드)
    //   VoiceTalk        = 서서 팔 벌리기 (시안 sian92/음성 대화 시작 — 손바닥 위로)
    //   Avatar           = 서서 팔 내리기 (시안 sian48/아바타 — 무대에 선 자세)
    // AvartaCamera 가 RT_Avatar 로 찍고, 각 화면의 RawImage 가 그 텍스처를 보여준다.
    // 화면이 한 번에 하나만 켜지므로 무대 하나로 충분하다 — 화면 전환 때 포즈만 갈아입힌다.
    //
    // 앉은 포즈의 카메라 구도는 씬에 잡혀 있는 그대로 쓰고(홈에서 이미 승인된 구도),
    // 선 포즈는 아바타 바운드에 맞춰 자동 프레이밍한다.
    // 관절은 "자식 본이 향할 목표 방향" FromToRotation — 리그 축 부호를 몰라도 되는 방식.
    // (AvatarSitPose 는 이 디렉터의 단일 포즈판이라 씬에서 꺼뒀다 — 파일은 참고용으로 남김)
    public class AvatarPoseDirector : MonoBehaviour
    {
        Animator anim;
        Transform avatarRoot;
        Camera cam;
        Vector3 camPos; Quaternion camRot;                  // 씬의 앉은 포즈 구도
        readonly Dictionary<Transform, Quaternion> bind = new Dictionary<Transform, Quaternion>();
        Vector3 bindRootPos;
        string current;

        void Start()
        {
            var stage = GameObject.Find("AvataStage");
            // ⚠ includeInactive 금지 — 꺼둔 옛 avatar_main 인스턴스가 먼저 잡혀서
            //   숨은 아바타에 포즈가 가고 카메라도 거기로 갔다(실기: 키 0.20 로그).
            anim = null;
            if (stage != null)
                foreach (var a in stage.GetComponentsInChildren<Animator>(false))
                    if (a.isHuman) { anim = a; break; }
            if (anim == null || !anim.isHuman)
            {
                Debug.LogWarning("[PoseDirector] AvataStage 밑에 휴머노이드 아바타가 없다");
                enabled = false; return;
            }
            avatarRoot = anim.transform;
            AvatarBlink.Ensure(avatarRoot.gameObject);   // 홈·음성 화면 무대 아바타도 깜빡인다
            foreach (var t in avatarRoot.GetComponentsInChildren<Transform>(true))
                bind[t] = t.localRotation;
            bindRootPos = avatarRoot.localPosition;

            var camGo = GameObject.Find("AvartaCamera");
            cam = camGo != null ? camGo.GetComponent<Camera>() : null;
            if (cam != null) { camPos = cam.transform.position; camRot = cam.transform.rotation; }

            if (ScreenManager.Instance != null)
            {
                ScreenManager.Instance.ScreenChanged += Apply;
                Apply(ScreenManager.Instance.Current);
            }
        }

        void OnDestroy()
        {
            if (ScreenManager.Instance != null) ScreenManager.Instance.ScreenChanged -= Apply;
        }

        void Apply(string id)
        {
            string pose = id switch
            {
                // 홈은 시안(_home_avatar)이 앉은 자세지만 2026-08-28 유저 지시로 스탠딩.
                // 홈은 시안대로 앉은 포즈 (2026-08-31 유저 재확정 — 08-30 "서있게" 를 되돌림)
                "Home" => "sit", "VoiceHome" => "sit",
                "VoiceTalk" => "open", "VoiceLoading" => "open",
                "Avatar" => "stand",
                _ => null,                                  // 안 보이는 화면에선 마지막 포즈 유지
            };
            if (pose == null || pose == current) return;
            current = pose;

            RestoreBind();
            switch (pose)
            {
                // sit: 시안(음성 대화.svg = 홈 카드와 동일 아트)이 상반신 클로즈업이라
                //      씬 고정 구도 대신 뼈 기반으로 잡는다 — 모델을 바꿔도 구도가 유지된다 (2026-08-31)
                case "sit": PoseSit(); FrameSitClose(); break;
                // stand(아바타 홈): 시안 실측 — 키 486/893=54%, 발 y650(바닥에서 27.2%)
                case "stand": PoseStand(); FrameStanding(1.08f, 0.272f); break;
                // open(음성대화): 시안(음성 대화 시작.svg)은 아바타가 표시 영역을 거의 채운다 — 크게
                case "open": PoseOpen(); FrameStanding(0.80f, 0.05f); break;
            }
            Debug.Log($"[PoseDirector] {id} → {pose}, cam={(cam != null ? cam.transform.position.ToString("F2") : "없음")}");
        }

        void RestoreBind()
        {
            foreach (var kv in bind)
                if (kv.Key != null) kv.Key.localRotation = kv.Value;
            avatarRoot.localPosition = bindRootPos;
        }

        void RestoreCamera()
        {
            if (cam == null) return;
            cam.transform.SetPositionAndRotation(camPos, camRot);
        }

        // 선 포즈용 자동 프레이밍 — 씬 구도의 시선 방향은 유지한 채 전신이 들어오는 거리로 물러난다.
        // ⚠ 스킨 메시 bounds 는 갱신 타이밍에 따라 발 근처 조각만 준다(실기: 발만 크게 나옴) →
        //   본 위치로 전신 박스를 직접 만든다(포즈 직후 바로 유효 — 코루틴 불필요).
        //   본은 살·머리카락 두께가 없으니 키 비례로 부풀린다.
        // 앉아 턱괴기 상반신 클로즈업 (시안 '음성 대화.svg' — 머리·가슴이 화면을 채운다).
        // 뼈 기준이라 아바타 모델이 바뀌어도 같은 구도가 나온다.
        void FrameSitClose()
        {
            if (cam == null) { return; }
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (head == null || hips == null) { RestoreCamera(); return; }

            float torso = Vector3.Distance(head.position, hips.position);
            // 중심을 머리 쪽으로 — 시안은 얼굴이 상단 중앙, 아래는 테이블
            Vector3 center = Vector3.Lerp(head.position, hips.position, 0.38f);
            float half = torso * 0.95f;   // 머리카락·손 여유 포함 반높이
            Vector3 f = camRot * Vector3.forward;
            float dist = half / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            cam.transform.SetPositionAndRotation(center - f * dist, camRot);
            Debug.Log($"[PoseDirector] 앉은 클로즈업: torso {torso:F2} → 카메라 {cam.transform.position:F2}");
        }

        void FrameStanding(float margin, float feetAnchor = 0.02f)
        {
            if (cam == null) return;
            var pts = new List<Vector3>();
            foreach (var hb in new[] { HumanBodyBones.Head, HumanBodyBones.Hips, HumanBodyBones.Spine,
                                       HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
                                       HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
                                       HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg })
            {
                var t = anim.GetBoneTransform(hb);
                if (t != null) pts.Add(t.position);
            }
            if (pts.Count == 0) return;
            var b = new Bounds(pts[0], Vector3.zero);
            foreach (var p in pts) b.Encapsulate(p);
            float h = Mathf.Max(b.size.y, 0.2f);
            b.Expand(new Vector3(h * 0.35f, h * 0.35f, h * 0.35f));   // 정수리·머리카락·손끝 여유

            Vector3 f = camRot * Vector3.forward;
            float half = Mathf.Max(b.extents.y, b.extents.x) * margin;
            float dist = half / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 pos = b.center - f * (dist + b.extents.z);

            // 발 앵커 — 발(박스 바닥)이 프레임 아래에서 feetAnchor 지점에 오도록 카메라를 올린다.
            // 화면마다 시안의 발 위치가 달라 파라미터로 받는다 (아바타 홈 0.272 = 시안 y650 실측).
            float H = (dist + b.extents.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            pos.y += (0.5f - feetAnchor) * 2f * H - b.extents.y;

            cam.transform.SetPositionAndRotation(pos, camRot);
            Debug.Log($"[PoseDirector] 프레이밍: 박스중심 {b.center:F2} 키 {b.size.y:F2} → 카메라 {cam.transform.position:F2}");
        }

        // ── 포즈들 ──

        void PoseSit()
        {
            float footBefore = FootY();
            Vector3 fwd = avatarRoot.forward, right = avatarRoot.right, up = Vector3.up;

            Bend(HumanBodyBones.Spine, HumanBodyBones.Neck, up + fwd * 0.25f + right * 0.06f);

            Bend(HumanBodyBones.LeftUpperLeg,  HumanBodyBones.LeftLowerLeg,  -right * 0.55f + fwd * 0.55f - up * 0.55f);
            Bend(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,  right * 0.55f + fwd * 0.55f - up * 0.55f);
            Bend(HumanBodyBones.LeftLowerLeg,  HumanBodyBones.LeftFoot,   right * 0.90f + fwd * 0.15f - up * 0.30f);
            Bend(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, -right * 0.90f + fwd * 0.00f - up * 0.30f);
            Bend(HumanBodyBones.LeftFoot,  HumanBodyBones.LeftToes,  fwd * 0.85f - right * 0.15f - up * 0.35f);
            Bend(HumanBodyBones.RightFoot, HumanBodyBones.RightToes, right * 0.70f + fwd * 0.50f - up * 0.30f);

            Bend(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, -up * 0.50f + fwd * 0.55f + right * 0.45f);
            Bend(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,      up * 0.92f - right * 0.20f);
            Bend(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,   -up * 0.80f + fwd * 0.40f - right * 0.10f);
            Bend(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,       -up * 0.45f + fwd * 0.75f + right * 0.15f);

            Bend(HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, up * 0.90f + fwd * 0.10f - right * 0.10f);
            Bend(HumanBodyBones.LeftHand,  HumanBodyBones.LeftMiddleProximal,  fwd * 0.55f - up * 0.75f);
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            var rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (head != null && rHand != null)
                PalmTo(true, (head.position - rHand.position).normalized);
            PalmTo(false, -up);
            CurlFingers(true, 22f, 30f, 20f);
            CurlFingers(false, 30f, 38f, 25f);

            if (head != null)
                head.rotation = Quaternion.FromToRotation(
                    head.up, (up + right * 0.48f + fwd * 0.10f).normalized) * head.rotation;

            avatarRoot.position += Vector3.up * (footBefore - FootY());
        }

        void PoseStand()
        {
            Vector3 fwd = avatarRoot.forward, right = avatarRoot.right, up = Vector3.up;
            // 팔만 자연스럽게 내린다 — 다리는 바인드 그대로(곧게 선 자세)
            Bend(HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm,  (-up - right * 0.15f).normalized);
            Bend(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, (-up + right * 0.15f).normalized);
            Bend(HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand,  (-up - right * 0.10f + fwd * 0.08f).normalized);
            Bend(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, (-up + right * 0.10f + fwd * 0.08f).normalized);
            CurlFingers(true, 12f, 16f, 10f);
            CurlFingers(false, 12f, 16f, 10f);
        }

        void PoseOpen()
        {
            // 시안(음성 대화 시작.svg 실물 확인): 위팔은 몸에 붙여 아래로, 팔꿈치에서 앞·바깥으로
            // 꺾어 허벅지 높이에서 손바닥을 위로 — 팔을 벌리기보다 '내밀어 얹는' 환영 자세.
            Vector3 fwd = avatarRoot.forward, right = avatarRoot.right, up = Vector3.up;
            Bend(HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm,  -up * 0.90f + fwd * 0.10f - right * 0.28f);
            Bend(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, -up * 0.90f + fwd * 0.10f + right * 0.28f);
            Bend(HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand,  fwd * 0.70f - up * 0.55f - right * 0.35f);
            Bend(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, fwd * 0.70f - up * 0.55f + right * 0.35f);
            Bend(HumanBodyBones.LeftHand,  HumanBodyBones.LeftMiddleProximal,  fwd * 0.90f - right * 0.30f - up * 0.10f);
            Bend(HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, fwd * 0.90f + right * 0.30f - up * 0.10f);
            PalmTo(true, up); PalmTo(false, up);
            CurlFingers(true, 5f, 7f, 4f);
            CurlFingers(false, 5f, 7f, 4f);
        }

        // ── 공통 유틸 (AvatarSitPose 에서 가져옴) ──

        void Bend(HumanBodyBones parent, HumanBodyBones child, Vector3 dir)
        {
            var p = anim.GetBoneTransform(parent);
            var c = anim.GetBoneTransform(child);
            if (p == null || c == null) return;
            Vector3 cur = (c.position - p.position).normalized;
            p.rotation = Quaternion.FromToRotation(cur, dir.normalized) * p.rotation;
        }

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

        void PalmTo(bool rightHand, Vector3 target)
        {
            var hand = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            var mid  = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Vector3 palm = PalmNormal(rightHand);
            if (hand == null || mid == null || palm == Vector3.zero) return;
            Vector3 axis = (mid.position - hand.position).normalized;
            Vector3 a = Vector3.ProjectOnPlane(palm, axis);
            Vector3 b = Vector3.ProjectOnPlane(target, axis);
            if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return;
            hand.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(a, b, axis), axis) * hand.rotation;
        }

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
                CurlJoint(b3, HumanBodyBones.LastBone, palm, distal);
            }
        }

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
