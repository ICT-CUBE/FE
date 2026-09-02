using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 프리팹 자식을 이름으로 찾아 채우는 공용 헬퍼.
    public static class UiUtil
    {
        public static void SetText(GameObject root, string child, string value)
        {
            var t = root.transform.Find(child);
            if (t == null) return;
            var x = t.GetComponent<TMP_Text>();
            if (x != null) x.text = value;
        }

        // 스프라이트가 없을 때 흰 사각형이 남지 않도록, 프리팹에선 알파 0 으로 두고
        // 데이터가 들어올 때 여기서 다시 보이게 한다.
        public static void SetSprite(GameObject root, string child, Sprite s)
        {
            if (s == null) return;
            var t = root.transform.Find(child);
            if (t == null) return;
            var i = t.GetComponent<Image>();
            if (i == null) return;
            i.sprite = s;
            var c = i.color;
            if (c.a < 1f) { c.a = 1f; i.color = c; }
        }

        public static void SetActive(GameObject root, string child, bool on)
        {
            var t = root.transform.Find(child);
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        public static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        // Destroy 는 프레임 끝에야 실제로 지워진다. 그 사이에 Instantiate 하면 옛 행과 새 행이
        // 같은 레이아웃에 함께 잡혀 글씨가 겹쳐 보인다. 부모에서 먼저 떼어내야 즉시 빠진다.
        // 누르는 '순간'(PointerDown)에 실행되는 버튼 배선 (2026-08-31).
        // 키보드가 열린 채 전송을 탭하면 pointer-down 순간 입력 포커스가 풀려 키보드가 닫히고,
        // 리프트가 풀린 버튼이 아래로 이동해 pointer-up 이 빗나가 onClick 이 무산됐다 —
        // 그래서 전송류 버튼은 down 시점에 실행한다. 재호출해도 중복 등록되지 않는다.
        public static void OnPointerDown(GameObject go, UnityEngine.Events.UnityAction act)
        {
            if (go == null || act == null) return;
            var trig = go.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trig == null) trig = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            trig.triggers.RemoveAll(e => e.eventID == UnityEngine.EventSystems.EventTriggerType.PointerDown);
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            entry.callback.AddListener(_ => act());
            trig.triggers.Add(entry);
        }

        public static void ClearChildren(Transform content)
        {
            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var c = content.GetChild(i);
                c.SetParent(null, false);
                Object.Destroy(c.gameObject);
            }
        }

        // 목록 행의 제목+부제를 한 덩어리로 묶어 행 높이 가운데에 놓는다.
        // 시안(상세-1)에서 제목이 2줄인 행은 제목이 위로, 부제가 아래로 밀려 있고
        // 두 경우 모두 그룹 중심이 행 중앙(40)에 온다. 제목 줄 수를 몰라도 맞도록 계산한다.
        public static void CenterTitleDesc(GameObject row, string titleName, string descName, float gap)
        {
            var t = row.transform.Find(titleName);
            var d = row.transform.Find(descName);
            if (t == null || d == null) return;
            var tt = t.GetComponent<TMP_Text>();
            var dt = d.GetComponent<TMP_Text>();
            if (tt == null || dt == null) return;

            tt.ForceMeshUpdate();
            dt.ForceMeshUpdate();
            float th = Mathf.Max(tt.preferredHeight, ((RectTransform)t).sizeDelta.y);
            float dh = ((RectTransform)d).sizeDelta.y;
            float rowH = ((RectTransform)row.transform).sizeDelta.y;

            // 제목 박스가 줄 수만큼 늘어나야 부제를 덮지 않는다
            var trt = (RectTransform)t;
            trt.sizeDelta = new Vector2(trt.sizeDelta.x, th);

            float top = (rowH - (th + gap + dh)) * 0.5f;
            trt.anchoredPosition = new Vector2(trt.anchoredPosition.x, -top);
            var drt = (RectTransform)d;
            drt.anchoredPosition = new Vector2(drt.anchoredPosition.x, -(top + th + gap));
        }

        // 씬에서 비워둔 컨테이너에 세로 레이아웃을 붙인다.
        public static void EnsureVertical(Transform content, float spacing)
        {
            if (content == null) return;
            var v = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlWidth = true;
            v.childControlHeight = false;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperCenter;

            var f = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // ── 런타임 UI 생성 ────────────────────────────────────
        // 시안 좌표(좌상단 원점)를 그대로 넣는다. 앵커/피벗을 좌상단으로 맞추므로
        // Figma Dev Mode 의 x/y/w/h 를 환산 없이 쓸 수 있다.
        public static RectTransform Place(Component c, float x, float y, float w, float h)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform MakeNode(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            return rt;
        }

        // ★ 코드로 만든 TMP 는 font 가 비어 있어 TMP 기본 폰트(한글 없음)로 떨어진다 → 한글이 □□□.
        //   TypeStyle.Apply 가 Resources/Fonts 의 Pretendard SDF 를 물려주므로 반드시 이걸로 만들 것.
        public static TextMeshProUGUI MakeText(Transform parent, string name, TypeStyle style, Color color,
                                               TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            style.Apply(t);
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;   // 라벨이 버튼 클릭을 먹지 않게 (2026-08-06 에 하루 날린 함정)
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            return t;
        }

        public static Image MakeImage(Transform parent, string name, Sprite sprite, Color color,
                                      Image.Type type = Image.Type.Simple, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>();
            i.sprite = sprite;
            i.color = color;
            i.type = type;
            i.raycastTarget = raycast;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            return i;
        }

        // RawImage 는 텍스처를 무조건 늘려서 채운다(비율 유지 옵션이 없다).
        // 시안처럼 '꽉 채우고 넘치는 쪽을 잘라내기' 를 하려면 uvRect 로 중앙을 잘라야 한다.
        // ── 사진 상세: 원본 비율 유지(contain) ──
        // FitCover(크롭)와 달리 사진을 자르지 않고, 박스 안에 통째로 들어가게 rect 를 줄인다.
        //
        // ★ 켜지지 않은 화면에서도 동작해야 한다 — 갤러리는 상세 화면을 Show() 하기 '전에'
        //   사진을 넣는데, 그때 stretch 앵커의 rect.width 는 0 이다(FitCover 가 일찍 반환해
        //   상세가 빈 화면으로 뜨던 원인). 그래서 rect 대신 앵커+sizeDelta 로 박스를 계산한다
        //   (캔버스 기준폭 412 는 이 프로젝트 상수 — MakeBubble 등에서 이미 쓴다).
        //
        // 같은 RawImage 를 FitCover 흐름(캡처 상세)과 번갈아 쓰므로, 처음 본 rect 를 기억해
        // 두 함수 모두 그 기준에서 출발한다 — 이전 호출이 줄여놓은 rect 가 새지 않는다.
        static readonly System.Collections.Generic.Dictionary<int, (Vector2 size, Vector2 pos)>
            baseRects = new System.Collections.Generic.Dictionary<int, (Vector2, Vector2)>();

        static (Vector2 size, Vector2 pos) EnsureBase(RawImage img)
        {
            int id = img.GetInstanceID();
            if (!baseRects.TryGetValue(id, out var b))
            {
                b = (img.rectTransform.sizeDelta, img.rectTransform.anchoredPosition);
                baseRects[id] = b;
            }
            return b;
        }

        public static void FitContain(RawImage img, Texture tex)
        {
            if (img == null) return;
            var rt = img.rectTransform;
            var (bSize, bPos) = EnsureBase(img);

            img.texture = tex;
            img.uvRect = new Rect(0f, 0f, 1f, 1f);
            if (tex == null || tex.width <= 0 || tex.height <= 0)
            {
                rt.sizeDelta = bSize; rt.anchoredPosition = bPos;
                return;
            }

            bool stretchX = rt.anchorMax.x - rt.anchorMin.x > 0.99f;
            float boxW = stretchX ? 412f + bSize.x : bSize.x;
            float boxH = bSize.y;

            float k = Mathf.Min(boxW / tex.width, boxH / tex.height);
            float w = tex.width * k, h = tex.height * k;

            rt.sizeDelta = new Vector2(stretchX ? w - 412f : w, h);
            // 세로는 박스 가운데에 놓는다 (pivot y=1 기준: 남는 높이의 절반만큼 내린다)
            rt.anchoredPosition = new Vector2(bPos.x, bPos.y - (boxH - h) * 0.5f);
        }

        public static void FitCover(RawImage img, Texture tex)
        {
            if (img == null) return;
            // FitContain 이 줄여놓은 rect 가 남아 있으면 원래대로 되돌린 뒤 크롭한다
            if (baseRects.TryGetValue(img.GetInstanceID(), out var b))
            { img.rectTransform.sizeDelta = b.size; img.rectTransform.anchoredPosition = b.pos; }
            img.texture = tex;
            if (tex == null) { img.uvRect = new Rect(0f, 0f, 1f, 1f); return; }

            var r = img.rectTransform.rect;
            if (r.width < 0.01f || r.height < 0.01f) return;
            float boxAspect = r.width / r.height;
            float texAspect = (float)tex.width / tex.height;

            if (texAspect > boxAspect)          // 텍스처가 더 넓다 → 좌우를 자른다
            {
                float w = boxAspect / texAspect;
                img.uvRect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            else                                // 더 길다 → 위아래를 자른다
            {
                float h = texAspect / boxAspect;
                img.uvRect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
            }
        }

        // 클릭을 받는 투명 판. 배경 이미지와 히트영역을 분리할 때 쓴다.
        public static Button MakeHit(Transform parent, string name, System.Action onClick)
        {
            var img = MakeImage(parent, name, null, new Color(1f, 1f, 1f, 0f), Image.Type.Simple, true);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.None;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }
    }
}