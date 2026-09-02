using UnityEngine;

namespace ARZone
{
    // 홈 무대 고폴리곤 테스트 (2026-08-31 임시, "홈화면에도 심어봐").
    // 앱 시작 때 AvataStage 의 vroid 아바타를 끄고 189만 모델을 같은 자리에 세운다.
    // AfterSceneLoad 는 씬 Awake 뒤·Start 전에 돌므로 AvatarPoseDirector.Start 가
    // 하이폴리의 휴머노이드 Animator 를 그대로 잡아 포즈·자동 프레이밍이 먹는다.
    // 측정 끝나면 Enabled = false 로 — 원래 아바타는 끄기만 해서 스위치 끄면 그대로 돌아온다.
    static class HipolyHomeTest
    {
        static readonly bool Enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Enabled) return;

            var stage = GameObject.Find("AvataStage");
            if (stage == null) { Debug.LogWarning("[FPS] AvataStage 없음 — 홈 고폴리 테스트 생략"); return; }

            Animator old = null;
            foreach (var a in stage.GetComponentsInChildren<Animator>(false))
                if (a.isHuman) { old = a; break; }
            if (old == null) { Debug.LogWarning("[FPS] 홈 무대 아바타 없음 — 생략"); return; }

            var hp = Resources.Load<GameObject>("Avatars/avatar_hipoly");
            if (hp == null) { Debug.LogWarning("[FPS] avatar_hipoly 못 찾음 — 생략"); return; }

            var t = old.transform;
            var inst = Object.Instantiate(hp, t.parent);
            inst.transform.localPosition = t.localPosition;
            inst.transform.localRotation = t.localRotation;
            inst.transform.localScale = t.localScale;
            // 무대는 전용 카메라(AvartaCamera→RT_Avatar)가 레이어로 골라 찍는다 — 안 맞추면 안 보인다.
            SetLayerRecursive(inst, old.gameObject.layer);
            old.gameObject.SetActive(false);
            inst.SetActive(true);
            ARSessionController.FixSkinnedBounds(inst);   // 컬링 오판으로 사라지는 것 방지
            AvatarBlink.Ensure(inst);
            Debug.Log("[FPS] 홈 무대 아바타를 고폴리(189만tri)로 교체 — 테스트");
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }
    }
}
