using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 유리 카드의 '빛나는 부분 테두리' (2026-08-25 유저 레퍼런스).
    //
    // 레퍼런스: 좌상단 모서리가 가장 밝게 빛나고, 우상단·좌하단으로 갈수록 테두리가
    // 거의 사라지며, 우하단에 약한 반사가 남는 모양 — 좌상단에서 빛이 닿는 유리 가장자리다.
    //
    // 테두리 스트로크 스프라이트(bg_round16_stroke 등)가 있는 Image 에 붙이면
    // 정점 알파를 위치별로 바꿔 그 모양을 낸다:
    //   밝기(u,v) = topLeft·(1-u)(1-v) + bottomRight·(u·v)
    //   (u,v = rect 안 0~1, v 는 위→아래. 좌상 = topLeft, 우하 = bottomRight,
    //    우상·좌하 = 두 항이 모두 0 이라 자연히 사라진다)
    //
    // GlassGradient 처럼 에셋이 아니라 코드다 — 스프라이트·씬은 그대로다.
    [RequireComponent(typeof(Graphic))]
    public class GlassEdgeGlow : BaseMeshEffect
    {
        [Header("모서리 밝기 (알파)")]
        [SerializeField, Range(0f, 1f)] float topLeft = 0.55f;      // 레퍼런스: 좌상단이 주광
        [SerializeField, Range(0f, 1f)] float bottomRight = 0.18f;  // 우하단의 약한 반사
        [Tooltip("빛 퍼짐. 1보다 크면 모서리에 더 몰린다")]
        [SerializeField, Range(0.5f, 4f)] float focus = 1.6f;

        static readonly List<UIVertex> buf = new List<UIVertex>();

        // 대상 위에 발광 테두리 자식("GlowBorder")을 만들어 붙인다. 여러 번 불려도 1개만 생긴다.
        // sliced=false 는 원형(circle_stroke)처럼 9-slice 없이 통째로 늘릴 스프라이트용.
        public static GlassEdgeGlow Attach(Transform host, Sprite stroke, bool sliced = true)
        {
            if (host == null || stroke == null) return null;
            var t = host.Find("GlowBorder");
            GameObject go;
            if (t == null)
            {
                go = new GameObject("GlowBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(host, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }
            else go = t.gameObject;
            go.transform.SetAsLastSibling();          // 내용 위에 가장자리만 얹는다

            var img = go.GetComponent<Image>();
            img.sprite = stroke;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;

            var glow = go.GetComponent<GlassEdgeGlow>();
            if (glow == null) glow = go.AddComponent<GlassEdgeGlow>();
            return glow;
        }

        public void Set(float tl, float br, float f)
        {
            topLeft = tl; bottomRight = br; focus = f;
            var g = GetComponent<Graphic>();
            if (g != null) g.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            buf.Clear();
            vh.GetUIVertexStream(buf);
            if (buf.Count == 0) return;

            // rect 는 9-slice 메시의 실제 정점 범위로 잡는다 (RectTransform 과 같지만 안전하게)
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var v in buf)
            {
                if (v.position.x < minX) minX = v.position.x;
                if (v.position.x > maxX) maxX = v.position.x;
                if (v.position.y < minY) minY = v.position.y;
                if (v.position.y > maxY) maxY = v.position.y;
            }
            float w = Mathf.Max(0.001f, maxX - minX);
            float h = Mathf.Max(0.001f, maxY - minY);

            for (int i = 0; i < buf.Count; i++)
            {
                var v = buf[i];
                float u = (v.position.x - minX) / w;
                float t = 1f - (v.position.y - minY) / h;      // v: 위 0 → 아래 1

                float glow = topLeft * Mathf.Pow((1f - u) * (1f - t), focus)
                           + bottomRight * Mathf.Pow(u * t, focus);

                var c = v.color;
                c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * Mathf.Clamp01(glow)), 0, 255);
                v.color = c;
                buf[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(buf);
        }
    }
}
