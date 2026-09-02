using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 유리 카드/버튼의 '빛나는 부분 테두리' 일괄 부착 (2026-08-25 유저 지시 목록).
    // 붙는 곳: Canvas/ScreenManager. 화면이 열릴 때 그 화면의 대상들에 GlowBorder 를 얹는다.
    // 전부 런타임 생성 — 씬 오브젝트 자체는 안 바뀐다.
    //
    // ⚠ 하단 탭바(홈·커뮤니티·마켓, 216×72 r36 알약)는 맞는 스트로크 에셋이 없어 뺐다
    //   (있는 알약 스트로크는 r22 까지 — 세로로 늘리면 캡이 타원이 된다). 에셋을 받으면 넣는다.
    public class GlassGlowBoot : MonoBehaviour
    {
        [Header("스트로크 스프라이트 (라운드별)")]
        [SerializeField] Sprite round8;      // bg_round8_stroke
        [SerializeField] Sprite round12;     // bg_round12_stroke — 탑바 미션/알림 버튼(44², r12)
        [SerializeField] Sprite round16;     // bg_round16_stroke — 카드류
        [SerializeField] Sprite pill44;      // bg_pill44_stroke — 세그먼트(46 알약, 4% 오차)
        [SerializeField] Sprite circle;      // circle_stroke — 원형 버튼

        // 런타임 빌더(마켓/커뮤니티/알림/음성주제 카드)가 가져다 쓴다
        public static Sprite Round8, Round12, Round16, Pill44, Circle;

        Sprite pillRing;    // tabbar_pill_glow 에서 발라낸 테두리 링 (지연 생성)

        // 알약 텍스처에서 '알파 경계에서 ringPx 안쪽까지' 만 남기고 속을 비운 링 스프라이트를 만든다.
        // (에셋 파일을 만드는 게 아니라 메모리에서 가공 — 프로필 원형 크롭과 같은 방식)
        static Sprite MakeRing(Texture2D src, int ringPx)
        {
            if (src == null) { Debug.LogWarning("[Glow] tabbar_pill_glow 리소스 없음"); return null; }
            int w = src.width, h = src.height;
            Color[] px;
            try { px = src.GetPixels(); }
            catch (UnityEngine.UnityException e)
            { Debug.LogWarning($"[Glow] 텍스처 읽기 불가(임포트 Read/Write 꺼짐?): {e.Message}"); return null; }

            // 투명 픽셀로부터의 거리(체스판 근사)를 ringPx 까지 전파해 경계 띠를 찾는다
            var dist = new int[w * h];
            for (int i = 0; i < dist.Length; i++) dist[i] = px[i].a < 0.02f ? 0 : int.MaxValue;
            // 캔버스 밖도 투명으로 간주한다 — tabbar_pill_glow 는 알약 직선 변이 캔버스 경계와
            // 일치해서(투명 픽셀은 모서리 구석 4곳뿐) 이 시드가 없으면 모서리 호만 남는다.
            for (int x = 0; x < w; x++)
            {
                if (dist[x] == int.MaxValue) dist[x] = 1;                          // 위 변
                if (dist[(h - 1) * w + x] == int.MaxValue) dist[(h - 1) * w + x] = 1;  // 아래 변
            }
            for (int y = 0; y < h; y++)
            {
                if (dist[y * w] == int.MaxValue) dist[y * w] = 1;                  // 왼 변
                if (dist[y * w + w - 1] == int.MaxValue) dist[y * w + w - 1] = 1;  // 오른 변
            }
            for (int pass = 0; pass < ringPx; pass++)
            {
                bool changed = false;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (dist[i] != int.MaxValue) continue;
                        int best = int.MaxValue;
                        if (x > 0 && dist[i - 1] < best) best = dist[i - 1];
                        if (x < w - 1 && dist[i + 1] < best) best = dist[i + 1];
                        if (y > 0 && dist[i - w] < best) best = dist[i - w];
                        if (y < h - 1 && dist[i + w] < best) best = dist[i + w];
                        if (best != int.MaxValue) { dist[i] = best + 1; changed = true; }
                    }
                if (!changed) break;
            }
            for (int i = 0; i < px.Length; i++)
                if (dist[i] == int.MaxValue || dist[i] > ringPx) px[i].a = 0f;   // 속 비우기

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        // 화면 id → (오브젝트 이름, 종류). 이름은 화면 안에서 유일한 것만 쓴다.
        enum K { R8, R12, R16, P44, Cir }
        static readonly Dictionary<string, (string name, K kind)[]> Targets =
            new Dictionary<string, (string, K)[]>
        {
            { "Home",    new[]{ ("BtnMission", K.R12), ("BtnBell", K.R12) } },
            { "Hub",     new[]{ ("BtnMission", K.R12), ("BtnBell", K.R12) } },
            { "Avatar",  new[]{ ("BtnMission", K.R12), ("BtnBell", K.R12),
                                ("BtnTalkText", K.Cir), ("BtnARSummonGo", K.Cir), ("BtnTalkVoice", K.Cir) } },
            { "Mission", new[]{ ("IntimacyCardBg", K.R16) } },
            { "StageList", new[]{ ("FilterTabs", K.P44) } },
            { "MusicDetail", new[]{ ("SegBg", K.P44) } },
            { "MarketOrderDetail", new[]{ ("CardTimeline", K.R16) } },
            { "MarketPurchaseDone", new[]{ ("CertCard", K.R16), ("ChainRows", K.R16),
                                           ("ChainBar", K.R16), ("ChainPanel", K.R16) } },
        };

        void Awake()
        {
            Round8 = round8; Round12 = round12; Round16 = round16; Pill44 = pill44; Circle = circle;
        }

        void Start()
        {
            if (ScreenManager.Instance == null) { Debug.LogError("[Glow] ScreenManager 없음"); return; }
            ScreenManager.Instance.ScreenChanged += OnScreen;
            OnScreen(ScreenManager.Instance.Current);
        }

        void OnDestroy()
        {
            if (ScreenManager.Instance != null) ScreenManager.Instance.ScreenChanged -= OnScreen;
        }

        Sprite Of(K k) => k switch
        {
            K.R8 => round8, K.R12 => round12, K.R16 => round16, K.P44 => pill44, _ => circle,
        };

        void OnScreen(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var canvas = transform.parent;
            var screen = canvas != null ? canvas.Find("Screen_" + id) : null;
            if (screen == null) return;

            if (Targets.TryGetValue(id, out var list))
                foreach (var (name, kind) in list)
                {
                    var t = FindDeep(screen, name);
                    if (t != null) GlassEdgeGlow.Attach(t, Of(kind), kind != K.Cir);
                    else Debug.LogWarning($"[Glow] {id} 에서 '{name}' 을 못 찾음");
                }

            // 하단 탭바 5곳 — 유리(씬 원본 α0.2)는 그대로 두고, 디자이너 알약(tabbar_pill_glow)에서
            // '가장자리 몇 픽셀만' 런타임에 발라낸 링을 GlowBorder 로 얹는다 (2026-08-25).
            // 처음엔 알약을 통째로 교체했는데 채움이 밝아 아이콘이 파묻혔다("불투명하다") →
            // 채움은 버리고 테두리만 쓰는 방식으로 바꿨다. r36 캡 모양은 에셋에서 오니 왜곡이 없다.
            if (id == "Home" || id == "Community" || id == "Market" || id == "Hub" || id == "Mission")
            {
                var bar = FindDeep(screen, "TabBar");
                if (bar != null)
                {
                    if (pillRing == null) pillRing = MakeRing(Resources.Load<Texture2D>("UI/tabbar_pill_glow"), 6);
                    if (pillRing != null) GlassEdgeGlow.Attach(bar, pillRing, sliced: false);
                }
            }

            // 허브 4분할 카드(ar홈) — 프리팹 인스턴스라 이름 대신 CardGrid 자식 전부
            if (id == "Hub")
            {
                var grid = FindDeep(screen, "CardGrid");
                if (grid != null)
                    for (int i = 0; i < grid.childCount; i++)
                        GlassEdgeGlow.Attach(grid.GetChild(i), round16);
            }
        }

        // 꺼진 자식까지 포함해 이름으로 찾는다
        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
