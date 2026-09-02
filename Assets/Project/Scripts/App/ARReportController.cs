using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 관람 리포트 화면을 채운다. 캡처 카드는 런타임에 만든다.
    public class ARReportController : MonoBehaviour
    {
        public event Action<CaptureService.Entry> CardClicked;   // 카드 탭 → 캡처 상세
        public event Action<CaptureService.Entry> SaveClicked;   // 카드의 저장 버튼 → 시트

        [Header("문구 (화면마다 다르다 — AR 관람 / 음악 감상)")]
        [SerializeField] string doneSubFormat = "{0} AR 공연 관람이 성공적으로 끝났어요!";
        [SerializeField] string shotFormat = "촬영 및 저장된 사진 수 <b>{0}장</b>";
        [SerializeField] string timeFormat = "관람 시간 <b>{0}</b>";

        [Header("텍스트")]
        [SerializeField] TMP_Text doneSubText;      // "SOLO AR 공연 관람이 …"
        [SerializeField] TMP_Text shotCountText;    // "촬영 및 저장된 사진 수 4장"
        [SerializeField] TMP_Text watchTimeText;    // "관람 시간 10:49"

        // 시안 '오늘의 AR 관람 리포트': 사진 (20,323)/(20,526) 372x187 → 한 페이지에 2장,
        // 좌우로 넘긴다. 페이지 도트 3개 (183.5/204.5/220.5, y729) 활성 13x8 #6FE8F5 / 비활성 8x8 #242831.
        [Header("캐러셀 (한 페이지 = 사진 2장)")]
        [SerializeField] ScrollRect momentsScroll;
        [SerializeField] RectTransform content;
        [SerializeField] GameObject captureCardPrefab;
        [SerializeField] int perPage = 2;
        [SerializeField] float cardW = 372f;
        [SerializeField] float cardH = 187f;
        [SerializeField] float cardGapY = 16f;      // 526 - (323 + 187)
        [SerializeField] float snapSpeed = 12f;

        // 음악 감상 리포트(Screen_ListenReport)는 외부에서 Populate 를 불러주는 주체가 없다.
        // 이 인스턴스만 켜질 때 스스로 채운다. (AR 관람 리포트는 ARMediaController 가 부른다)
        [Header("스스로 채우기 — 음악 감상 리포트")]
        [SerializeField] bool selfPopulate;
        [SerializeField] CaptureService captureSource;

        [Header("페이지 도트")]
        [SerializeField] GameObject pageDots;
        [SerializeField] Image[] dots;
        [SerializeField] Color dotOn = new Color(0.4353f, 0.9098f, 0.9608f, 1f);   // #6FE8F5
        [SerializeField] Color dotOff = new Color(0.1412f, 0.1569f, 0.1922f, 1f);  // #242831
        [SerializeField] float dotOnW = 13f;
        [SerializeField] float dotOffW = 8f;

        readonly List<GameObject> spawned = new List<GameObject>();
        int pages;
        int targetPage = -1;

        void Awake() => EnsureContentLayout();

        static readonly CaptureService.Entry[] None = new CaptureService.Entry[0];

        System.Collections.IEnumerator CoRefitThumbs()
        {
            yield return null;
            foreach (var go in spawned)
            {
                if (go == null) continue;
                var th = FindChild<RawImage>(go.transform, "Thumb");
                if (th != null && th.texture != null) UiUtil.FitCover(th, th.texture);
            }
        }

        void OnEnable()
        {
            if (momentsScroll != null) momentsScroll.onValueChanged.AddListener(OnScrolled);

            if (selfPopulate)
            {
                var list = captureSource != null ? captureSource.Captures : (System.Collections.Generic.IReadOnlyList<CaptureService.Entry>)None;
                Populate("", list, ARListenController.LastWatchSeconds);
            }
            else StartCoroutine(CoRefitThumbs());   // 화면 꺼진 채 Populate 된 경우의 크롭 보정
        }

        void OnDisable()
        {
            if (momentsScroll != null) momentsScroll.onValueChanged.RemoveListener(OnScrolled);
        }

        // Content 는 씬에서 비워뒀다. 페이지를 직접 배치하므로 레이아웃 컴포넌트는 쓰지 않는다
        // (HorizontalLayoutGroup + ContentSizeFitter 를 쓰면 페이지 폭이 뷰포트와 어긋난다).
        void EnsureContentLayout()
        {
            if (content == null) return;

            // ★ Destroy 는 프레임 끝 처리라, 같은 프레임에 카드를 배치해도
            //   살아있는 레이아웃 그룹이 프레임 끝에 다시 가로로 재배치해 버린다
            //   (페이지 배치가 통째로 무시돼 사진이 화면 밖으로 나갔다) → 즉시 제거.
            var h = content.GetComponent<HorizontalLayoutGroup>();
            if (h != null) DestroyImmediate(h);
            var f = content.GetComponent<ContentSizeFitter>();
            if (f != null) DestroyImmediate(f);

            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;

            if (momentsScroll != null)
            {
                momentsScroll.horizontal = true;
                momentsScroll.vertical = false;
                momentsScroll.movementType = ScrollRect.MovementType.Clamped;
            }
        }

        public void Populate(string stageTitle, IReadOnlyList<CaptureService.Entry> captures, float watchSeconds)
        {
            // 시안: 라벨은 보통, 값만 굵게
            if (doneSubText != null) doneSubText.text = string.Format(doneSubFormat, stageTitle);
            if (shotCountText != null) shotCountText.text = string.Format(shotFormat, captures.Count);
            if (watchTimeText != null) watchTimeText.text = string.Format(timeFormat, FormatTime(watchSeconds));

            BuildCards(captures);
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        void BuildCards(IReadOnlyList<CaptureService.Entry> captures)
        {
            // Destroy 는 프레임 끝 처리라 새 카드와 겹친다
            foreach (var go in spawned) if (go != null) DestroyImmediate(go);
            spawned.Clear();
            targetPage = -1;

            if (content == null || captureCardPrefab == null)
            {
                Debug.LogError("[Report] content 또는 captureCardPrefab 이 비어 있음");
                return;
            }
            if (captures.Count == 0)
                Debug.LogWarning("[Report] 캡처가 0장 — AR 무대에서 촬영해야 사진이 뜬다");

            pages = Mathf.CeilToInt(captures.Count / (float)perPage);
            content.sizeDelta = new Vector2(cardW * pages, 0f);

            for (int i = 0; i < captures.Count; i++)
            {
                var e = captures[i];
                var card = Instantiate(captureCardPrefab, content);
                card.SetActive(true);
                spawned.Add(card);

                // 페이지 안 세로 배치: 0번째 y 0, 1번째 y -(187+16)
                int page = i / perPage, slot = i % perPage;
                var rt = (RectTransform)card.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(cardW, cardH);
                rt.anchoredPosition = new Vector2(page * cardW, -slot * (cardH + cardGapY));

                var thumb = FindChild<RawImage>(card.transform, "Thumb");
                if (thumb == null) Debug.LogError($"[Report] 카드{i} 에 Thumb(RawImage) 가 없다");
                else
                {
                    // 그대로 대입하면 카드 비율로 늘어나 찌그러진다 — 비율 유지한 채 크롭(FitCover)
                    UiUtil.FitCover(thumb, e.Texture);
                    thumb.color = Color.white;                 // 알파가 0 으로 남아 있으면 안 보인다
                    thumb.gameObject.SetActive(true);
                    if (e.Texture == null) Debug.LogWarning($"[Report] 카드{i} 텍스처가 null");
                }

                // 카드 전체를 누르면 상세로
                var bg = card.GetComponent<Image>() ?? card.GetComponentInChildren<Image>();
                if (bg != null)
                {
                    var btn = bg.GetComponent<Button>() ?? bg.gameObject.AddComponent<Button>();
                    var captured = e;
                    btn.onClick.AddListener(() => CardClicked?.Invoke(captured));
                }

                Bind(card, "BtnSave", () => SaveClicked?.Invoke(e));
                Bind(card, "BtnShare", () => Debug.Log("[Report] 공유는 아직 미구현"));
            }

            if (momentsScroll != null) momentsScroll.horizontalNormalizedPosition = 0f;
            // Thumb 는 stretch 자식이라 이 프레임엔 rect 가 0 일 수 있고, 그러면 FitCover 가
            // 크롭 비율을 못 정한다. 레이아웃이 잡힌 다음 프레임에 한 번 더 맞춘다.
            if (isActiveAndEnabled) StartCoroutine(CoRefitThumbs());
            Debug.Log($"[Report] 카드 {spawned.Count}장 / {pages}페이지 배치 (content {content.sizeDelta.x}x{content.rect.height})");
            DumpCards();
            UpdateDots(0f);
            if (pageDots != null) pageDots.SetActive(pages > 1);
        }

        // 사진이 안 보인다는 보고가 있어, 카드가 실제로 어디에 어떤 상태로 놓였는지 남긴다
        void DumpCards()
        {
            var corners = new Vector3[4];
            for (int i = 0; i < spawned.Count && i < 4; i++)
            {
                var card = spawned[i];
                if (card == null) continue;
                var rt = (RectTransform)card.transform;
                rt.GetWorldCorners(corners);
                var th = FindChild<RawImage>(card.transform, "Thumb");
                Debug.Log($"[Report] 카드{i} active={card.activeInHierarchy} " +
                          $"pos={rt.anchoredPosition} size={rt.sizeDelta} " +
                          $"화면좌하={corners[0]} 우상={corners[2]} " +
                          $"thumb={(th == null ? "없음" : (th.texture == null ? "tex null" : th.texture.width + "x" + th.texture.height))} " +
                          $"thumbA={(th == null ? -1f : th.color.a)}");
            }
        }

        // 손을 떼면 가장 가까운 페이지로 붙인다 (유니티 ScrollRect 엔 스냅이 없다)
        void Update()
        {
            if (momentsScroll == null || pages <= 1) return;
            if (momentsScroll.velocity.sqrMagnitude > 400f) { targetPage = -1; return; }

            float pos = momentsScroll.horizontalNormalizedPosition;
            if (targetPage < 0) targetPage = Mathf.Clamp(Mathf.RoundToInt(pos * (pages - 1)), 0, pages - 1);

            float want = targetPage / (float)(pages - 1);
            if (Mathf.Abs(pos - want) < 0.001f) return;
            momentsScroll.velocity = Vector2.zero;
            momentsScroll.horizontalNormalizedPosition = Mathf.Lerp(pos, want, Time.deltaTime * snapSpeed);
        }

        static void Bind(GameObject root, string childName, Action action)
        {
            var t = root.transform.Find(childName);
            if (t == null) return;
            var b = t.GetComponent<Button>();
            if (b == null) return;
            b.onClick.AddListener(() => action());
        }

        static T FindChild<T>(Transform root, string name) where T : Component
        {
            var t = root.Find(name);
            return t != null ? t.GetComponent<T>() : null;
        }

        void OnScrolled(Vector2 v)
        {
            targetPage = -1;                  // 손으로 움직였으면 스냅 목표를 다시 잡는다
            UpdateDots(v.x);
        }

        // 시안: 활성 13x8 알약 #6FE8F5, 비활성 8x8 #242831. 간격 8, 좌측 183.5 고정.
        void UpdateDots(float normalized)
        {
            if (dots == null || dots.Length == 0) return;
            int n = Mathf.Min(Mathf.Max(pages, 1), dots.Length);
            int active = n <= 1 ? 0 : Mathf.Clamp(Mathf.RoundToInt(normalized * (n - 1)), 0, n - 1);

            float x = 0f;
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                bool used = i < n;
                dots[i].gameObject.SetActive(used);
                if (!used) continue;

                bool on = i == active;
                dots[i].color = on ? dotOn : dotOff;
                var rt = dots[i].rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(on ? dotOnW : dotOffW, 8f);
                rt.anchoredPosition = new Vector2(x, 0f);
                x += (on ? dotOnW : dotOffW) + 8f;
            }
        }
    }
}