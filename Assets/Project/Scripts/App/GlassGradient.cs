using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 유리(glass) 면의 그라디언트를 UI 메시의 '정점 색'으로 낸다.
    //
    // 왜 스프라이트가 아니라 코드인가:
    //   시안의 유리는 blur 가 아니라 #999999 → #060914 선형 그라디언트 + fill-opacity 0.2 다.
    //   그런데 그라디언트는 9-slice 가 안 되므로 스프라이트로 하면 카드 크기마다 SVG 가 하나씩 필요하다
    //   (372×516 · 372×515 · 372×256 · 373×72 · 178×250 …). 정점 색이면 크기와 무관하게 같은 축이 나온다.
    //
    // 기본값의 근거 — 홈 '아바타 홈' IntimacyCard(카드 (20,626) 372×133, 시안 홈.svg paint6)에서
    // 그 그라디언트 축이 카드 위를 지나는 구간을 실측한 값:
    //   좌상 0.334 · 우상 0.805 · 좌하 0.791 · 우하 1.262(→1 로 잘림)
    //   ⇒ t ≈ 0.334 + 0.471·u + 0.457·v   (u,v = 카드 안 0~1 좌표, v 는 위→아래)
    // 그래서 이 컴포넌트를 그냥 붙이면 어떤 크기의 카드든 홈 카드와 같은 유리가 된다.
    //
    // ★ from/to 는 유리의 원색이 아니라 **시안에서 합성이 끝난 결과색**이고, alpha 는 1 이다.
    //   유니티는 Linear 색공간(m_ActiveColorSpace: 1), 피그마는 sRGB 라 **같은 α0.2 라도 결과가 2배 가까이 다르다**
    //   (α0.2 유리를 그대로 깔면 좌상 47.4 — 시안은 24.8). 반투명으로 두는 한 이 차이는 못 없앤다.
    //   그래서 배경(#050505) 위에서의 시안 합성값을 그대로 불투명하게 칠한다:
    //     S(t) = 0.2·lerp(#999999,#060914,t) + 0.8·#050505
    //     S(0)=#232323 · S(0.334)=#19191A(좌상) · S(1)=#050608(우하)
    //   S 는 t 에 대해 선형이라 **양 끝만 넣으면 중간은 정확히 맞는다.** 카드가 놓이는 배경이 #050505 가
    //   아닌 곳에 쓰려면 그 배경으로 S 를 다시 계산해 넣을 것.
    //
    // ⚠ 정점 색은 Graphic.color 를 **덮어쓴다**. 그래서 Image.color / TokenStyle 값은 이 컴포넌트가
    //   켜져 있는 동안 화면에 영향이 없다(꺼졌을 때의 대비값으로만 남는다).
    [RequireComponent(typeof(Graphic))]
    public class GlassGradient : BaseMeshEffect
    {
        [SerializeField] Color from = new Color32(0x23, 0x23, 0x23, 0xFF);   // S(0)
        [SerializeField] Color to = new Color32(0x05, 0x06, 0x08, 0xFF);     // S(1)
        [SerializeField, Range(0f, 1f)] float alpha = 1f;

        [Header("축 (홈 IntimacyCard 실측)")]
        [SerializeField] float t0 = 0.334f;   // 좌상단의 t
        [SerializeField] float du = 0.471f;   // 가로로 1 갈 때 t 증가량
        [SerializeField] float dv = 0.457f;   // 세로로(아래로) 1 갈 때 t 증가량

        static readonly List<UIVertex> buf = new List<UIVertex>();

        // 런타임에 만드는 것(대화 말풍선 등)에 값을 넣어준다.
        // 축은 t = t0 + du·u + dv·v (u,v 는 rect 안 0~1, v 는 위→아래).
        public void Set(Color a, Color b, float axisT0, float axisDu, float axisDv)
        {
            from = a; to = b; alpha = 1f;
            t0 = axisT0; du = axisDu; dv = axisDv;
            var g = GetComponent<Graphic>();
            if (g != null) g.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null || vh.currentVertCount == 0) return;

            var rect = ((RectTransform)transform).rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            vh.GetUIVertexStream(buf);
            for (int i = 0; i < buf.Count; i++)
            {
                var v = buf[i];
                float u = Mathf.Clamp01((v.position.x - rect.xMin) / rect.width);
                float d = Mathf.Clamp01((rect.yMax - v.position.y) / rect.height);
                float t = Mathf.Clamp01(t0 + du * u + dv * d);
                var c = Color.Lerp(from, to, t);
                c.a = alpha;
                v.color = c;
                buf[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(buf);
            buf.Clear();
        }
    }
}
