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
        public static void FitCover(RawImage img, Texture tex)
        {
            if (img == null) return;
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