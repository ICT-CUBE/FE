using UnityEngine;

namespace ARZone
{
    // 스틱맨을 프리미티브(구/캡슐)로 코드 생성 + StickFigureController 부착.
    // height = 발끝~머리 전체 높이(m).
    public static class StickFigureFactory
    {
        public static GameObject Create(float height)
        {
            Color color = DesignTokens.Action.Primary; // 토큰: violet/500
            var root = new GameObject("StickFigure");

            float headR    = height * 0.16f;
            float thick    = height * 0.03f;
            float legLen   = height * 0.38f;
            float torsoLen = height * 0.40f;
            float armLen   = height * 0.32f;

            float hipY      = legLen;
            float shoulderY = hipY + torsoLen;
            float headCY    = shoulderY + headR * 1.1f;

            AddSphere(root.transform, "Head", new Vector3(0, headCY, 0), headR * 2f, color);
            AddLimb(root.transform, "Torso", new Vector3(0, shoulderY, 0), 0f, torsoLen, thick, color);
            Transform armL = AddLimb(root.transform, "ArmL", new Vector3(0, shoulderY, 0), 40f,  armLen, thick, color);
            Transform armR = AddLimb(root.transform, "ArmR", new Vector3(0, shoulderY, 0), -40f, armLen, thick, color);
            Transform legL = AddLimb(root.transform, "LegL", new Vector3(0, hipY, 0),      12f,  legLen, thick, color);
            Transform legR = AddLimb(root.transform, "LegR", new Vector3(0, hipY, 0),      -12f, legLen, thick, color);

            var ctrl = root.AddComponent<StickFigureController>();
            ctrl.Init(legL, legR, armL, armR);
            return root;
        }

        static void AddSphere(Transform parent, string name, Vector3 localPos, float diameter, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            StripCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * diameter;
            SetColor(go, c);
        }

        static Transform AddLimb(Transform parent, string name, Vector3 pivotPos, float zAngle, float length, float thick, Color c)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = pivotPos;
            pivot.transform.localRotation = Quaternion.Euler(0, 0, zAngle);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            StripCollider(cap);
            cap.transform.SetParent(pivot.transform, false);
            cap.transform.localPosition = new Vector3(0, -length * 0.5f, 0);
            cap.transform.localScale = new Vector3(thick, length * 0.5f, thick);
            SetColor(cap, c);
            return pivot.transform;
        }

        static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        static void SetColor(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            // URP에서 기본 머티리얼이 안 보일 수 있어 URP 셰이더를 명시
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.color = c;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            r.material = mat;
        }
    }
}