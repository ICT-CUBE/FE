using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 텍스트 대화 상세. 말풍선을 코드로 쌓는다(시안 실측).
    //   아바타 말풍선 x=80, #A855F7, 최대폭 300 / 내 말풍선 우측끝 393, #0F0D17
    //   아바타 프로필 45×45 @ x19.5, 이름 x80.36
    // 응답 문구는 AI 인터페이스가 오기 전까지 시안 목업만 쓴다.
    public class TalkDetailController : MonoBehaviour
    {
        [System.Serializable]
        public class Message
        {
            public bool mine;
            public string text;
            public string time;
            public Texture2D image;      // 이미지 전송이면 여기에 담긴다 (text 는 비운다)
            public Message(bool m, string t, string tm) { mine = m; text = t; time = tm; }
            public Message(Texture2D img, string tm) { mine = true; text = ""; time = tm; image = img; }
        }

        [Header("상단")]
        [SerializeField] TMP_Text navTitle;
        [SerializeField] Button btnSearch;
        [SerializeField] Button btnArchive;
        [SerializeField] string searchScreen = "TalkSearch";
        [SerializeField] string archiveScreen = "TalkArchive";

        [Header("목록")]
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] Sprite avatarSprite;
        [SerializeField] string avatarName = "젠득이";

        [Header("입력")]
        [SerializeField] Button btnSend;
        [SerializeField] TMP_InputField input;
        [SerializeField] TMP_Text inputHint;
        [SerializeField] RectTransform inputBar;   // 여기에 InputField 를 만들어 붙인다

        [Header("응답 준비 중 (점 3개 시퀀스)")]
        [SerializeField] Image typingDots;
        [SerializeField] Sprite[] typingFrames;
        [SerializeField] float typingFps = 30f;
        [SerializeField] float replyDelay = 1.2f;

        [Header("추천 칩 (시안 '첫대화')")]
        [SerializeField] RectTransform chipBar;          // 입력바 위 (20.5,713.5) 높이 37
        [SerializeField] Sprite pillFill;                // bg_pill
        [SerializeField] Sprite pillStroke;              // bg_pill_stroke
        [SerializeField]
        List<string> suggestions = new List<string> { "오늘 무대 어땠어?", "요즘 뭐해?", "저녁 메뉴 추천해줘" };

        [Header("말풍선 액션 (북마크/재생성/신고)")]
        [SerializeField] Sprite circleSprite;            // circle.svg
        [SerializeField] Sprite icScrap;
        [SerializeField] Sprite icRegen;
        [SerializeField] Sprite icReport;
        [SerializeField] ReportSheetController reportSheet;

        [Header("말풍선 (시안 '텍스트 대화 상세')")]
        [SerializeField] Sprite bubbleThemSprite;         // bubble_them — 좌상 r8 / 나머지 r16
        [SerializeField] Sprite bubbleMineSprite;         // bubble_mine — 우상 r8 / 나머지 r16
        [SerializeField] Sprite circleStrokeSprite;       // circle_stroke — 프로필 테두리
        // 시안 내 말풍선 글자색 #F4F4F4 — 기본 토큰(TextSecondary=#D3D5DD)과 달라 값으로 둔다
        [SerializeField] Color mineTextColor = new Color(0.956863f, 0.956863f, 0.956863f, 1f);

        [Header("시안 실측")]
        [SerializeField] float bubbleMaxW = 300f;
        // 시안 '사진 첨부-1' 실측: 말풍선 (232,330) 160×158 — 사실상 정사각
        [SerializeField] float imageBubbleW = 160f;
        [SerializeField] float imageBubbleMaxH = 160f;
        [SerializeField] float bubbleLeftX = 80f;
        [SerializeField] float bubbleRightEdge = 393f;
        [SerializeField] float bubblePadX = 17f;          // 글자 좌우 여백 (193 = 158.93 + 34)
        [SerializeField] float bubblePadY = 20f;          // 글자 상하 여백 (54 = 13.92 + 40)
        [SerializeField] float gap = 12f;
        // 첫 말풍선이 뷰포트 맨 위에 닿으면 아바타 위쪽이 마스크에 잘린다 → 위 여백.
        // 스크롤 상단도 같은 값만큼 올려서 말풍선의 실제 화면 위치는 시안 그대로 유지한다.
        [SerializeField] float topPad = 10f;
        [SerializeField] float chipH = 37f;              // 시안 (20.5,713.5) 143×37
        [SerializeField] float chipGap = 9f;
        [SerializeField] float chipExtra = 33f;          // 라벨폭에 더할 좌우 여백 (16.5×2)
        [SerializeField] float actionGap = 12f;          // 말풍선 아래 액션 행까지
        [SerializeField] float actionSize = 32f;
        [SerializeField] float actionPitch = 40f;

        [Header("데이터 (임시)")]
        [SerializeField] List<Message> messages = new List<Message>();

        readonly List<GameObject> views = new List<GameObject>();
        readonly List<Image> chipBgs = new List<Image>();
        readonly List<TMP_Text> chipLabels = new List<TMP_Text>();
        int chipPicked = -1;
        Coroutine typing;

        // 코드로 만든 TMP 는 폰트가 비어 TMP 기본(한글 없음)으로 떨어진다 → 씬 라벨에서 물려받는다
        void ApplyFont(TMP_Text t)
        {
            var src = navTitle != null ? navTitle : inputHint;
            if (src == null || src.font == null) return;
            t.font = src.font;
            t.fontSharedMaterial = src.fontSharedMaterial;
        }

        // ── OS 키보드가 올라오면 입력바·칩·목록을 그만큼 위로 밀어 올린다 ──
        // TouchScreenKeyboard.area 는 픽셀이라 Canvas.scaleFactor 로 나눠 캔버스 유닛으로 바꾼다.
        Canvas canvas;
        float lift;                       // 지금 적용된 올림 높이(유닛)
        readonly Dictionary<RectTransform, Vector2> basePos = new Dictionary<RectTransform, Vector2>();
        Vector2 scrollBaseSize, scrollBasePos;

        void RememberBase(RectTransform rt)
        {
            if (rt != null && !basePos.ContainsKey(rt)) basePos[rt] = rt.anchoredPosition;
        }

        float KeyboardUnits()
        {
            float px;
#if UNITY_ANDROID && !UNITY_EDITOR
            // ⚠ `TouchScreenKeyboard.visible` 로 막으면 안 된다 — TMP_InputField 는 키보드를 자체
            //   관리해서 이 값이 안 켜지는 기기가 있다(2026-08-21 실기: 안드로이드는
            //   'Keyboard Visible = True' 를 보내는데 입력바가 전혀 안 올라가 키보드에 겹쳤다).
            //   창의 '보이는 영역' 은 키보드가 없으면 어차피 0 이라 게이트가 필요 없다.
            // ⚠ `TouchScreenKeyboard.area` 는 기기에 따라 **화면 전체 rect** 를 돌려준다 → 폴백으로만.
            px = AndroidKeyboardPx();
            if (px <= 1f && TouchScreenKeyboard.visible) px = TouchScreenKeyboard.area.height;
#else
            if (!TouchScreenKeyboard.visible) return 0f;
            px = TouchScreenKeyboard.area.height;
#endif
            // 키보드가 내려가도 네비게이션 바(42px ≈ 16유닛) 가 남아 입력바가 16 떠 있었다
            // (2026-08-21 로그: 384.5 → 16 → 0). 화면의 15% 미만이면 키보드가 아니다.
            if (px < Screen.height * 0.15f) return 0f;

            // 키보드가 화면의 60% 를 넘을 수는 없다 — 넘으면 잘못 잰 값이므로 버린다.
            float max = Screen.height * 0.6f;
            if (px > max)
            {
                Debug.LogWarning($"[TalkDetail] 키보드 높이 {px}px 는 화면({Screen.height})의 60% 초과 — {max} 로 자름");
                px = max;
            }

            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            float sf = (canvas != null && canvas.scaleFactor > 0.001f) ? canvas.scaleFactor : 1f;
            return px / sf;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static float AndroidKeyboardPx()
        {
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var window = activity.Call<AndroidJavaObject>("getWindow");
                using var decor = window.Call<AndroidJavaObject>("getDecorView");
                using var rect = new AndroidJavaObject("android.graphics.Rect");
                decor.Call("getWindowVisibleDisplayFrame", rect);
                int visibleBottom = rect.Get<int>("bottom");
                int full = decor.Call<int>("getHeight");
                return Mathf.Max(0, full - visibleBottom);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TalkDetail] 키보드 높이 측정 실패: " + e.Message);
                return 0f;
            }
        }
#endif

        // ★ 입력바의 y 는 여기 한 군데에서만 정한다.
        //   예전엔 LayoutBar 가 `-(barBottom-h)`(리프트 무시), ApplyLift 가 `base+lift`(높이 무시) 로
        //   서로 덮어써서, 사진을 붙이면 바가 키보드 밑으로 내려가고 전송 아이콘만 따로 놀았다.
        void PlaceBar()
        {
            if (inputBar == null) return;
            // 입력바는 **하단 앵커 · pivot(0,0)** 이라 높이를 키우면 알아서 위로 자란다.
            // 예전엔 상단 앵커라 화면 비율이 바뀌면(캔버스가 893 보다 길면) 바가 붕 떴다.
            inputBar.anchoredPosition = new Vector2(inputBar.anchoredPosition.x, barBottom + lift);
        }

        void ApplyLift(float h)
        {
            foreach (var kv in basePos)
                if (kv.Key != null) kv.Key.anchoredPosition = kv.Value + new Vector2(0f, h);
            PlaceBar();   // 바는 높이가 변하므로 basePos 로 못 놓는다

            // 목록은 아래쪽을 그만큼 줄여서 마지막 말풍선이 키보드에 가리지 않게 한다
            var srt = scroll != null ? (RectTransform)scroll.transform : null;
            if (srt != null)
            {
                srt.sizeDelta = scrollBaseSize + new Vector2(0f, -h);
                srt.anchoredPosition = scrollBasePos + new Vector2(0f, h * 0.5f);
            }
        }

        void LateUpdate()
        {
            float h = KeyboardUnits();
            if (Mathf.Abs(h - lift) < 0.5f) return;
            Debug.Log($"[TalkDetail] 키보드 리프트 {lift:0.#} → {h:0.#} 유닛 (화면 {Screen.height}px)");
            lift = h;
            ApplyLift(h);
        }

        void OnEnable()
        {
            if (btnSend != null) btnSend.onClick.AddListener(OnSend);
            if (btnSearch != null) btnSearch.onClick.AddListener(() => Go(searchScreen));
            if (btnArchive != null) btnArchive.onClick.AddListener(() => Go(archiveScreen));
            if (navTitle != null) navTitle.text = avatarName;
            EnsureInput();

            // 키보드 대응 기준값 (화면에 들어올 때의 위치)
            // ⚠ inputBar 는 넣지 않는다 — 높이가 변해서 basePos 로는 못 놓는다(PlaceBar 가 담당)
            RememberBase(inputHint != null ? (RectTransform)inputHint.transform : null);
            RememberBase(btnSend != null ? (RectTransform)btnSend.transform : null);
            RememberBase(chipBar);
            if (scroll != null && scrollBaseSize == Vector2.zero)
            {
                var srt = (RectTransform)scroll.transform;
                scrollBaseSize = srt.sizeDelta;
                scrollBasePos = srt.anchoredPosition;
            }
            lift = 0f;
            ApplyLift(0f);

            ShowTyping(false);
            BuildChips();
            Build();
        }

        void OnDisable()
        {
            if (btnSend != null) btnSend.onClick.RemoveAllListeners();
            if (btnSearch != null) btnSearch.onClick.RemoveAllListeners();
            if (btnArchive != null) btnArchive.onClick.RemoveAllListeners();
            if (typing != null) { StopCoroutine(typing); typing = null; }
        }

        // 시안 입력바는 이미지 + 힌트뿐이라 글자를 칠 수 없다.
        // TMP_InputField 는 직렬화 필드가 많아 씬에 손으로 만들면 빠뜨리기 쉬워서 코드로 붙인다.
        void EnsureInput()
        {
            if (input != null || inputBar == null) return;

            // 힌트 라벨을 placeholder 로 재활용한다 (시안 위치 그대로)
            var placeholder = inputHint;

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(inputBar, false);
            var art = area.GetComponent<RectTransform>();
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            // Figma 간격 스펙: 안쪽 여백 사방 16 · + ↔ 글 8 · 글 ↔ 전송 8
            //   바 20~392 → + 36..72 · 글 80..332 · 전송 340..376
            art.offsetMin = new Vector2(60f, 16f);    // 80 - 20
            art.offsetMax = new Vector2(-60f, -16f);  // 332 - 392

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(area.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var tt = textGo.GetComponent<TextMeshProUGUI>();
            ApplyFont(tt);
            tt.fontSize = 14f;
            // 개정판 시안은 입력바가 어둡다(#181A20) → 글씨는 흰색.
            // (예전엔 밝은 회색 바라 검정이었고, 바만 어둡게 바꿨더니 글자가 안 보였다)
            tt.color = DesignTokens.Text.Primary;
            tt.alignment = TextAlignmentOptions.MidlineLeft;
            tt.richText = false;
            // ★ 이걸 안 끄면 투명한 글자 판이 첨부 트레이 위를 덮어 × 클릭을 먹는다.
            //   (2026-08-21 실기: × 를 눌러도 onClick 로그조차 안 찍혔다)
            //   입력 포커스는 InputBar 자체 Image 가 받으므로 글자는 레이캐스트를 안 받아도 된다.
            tt.raycastTarget = false;
            textArea = art;

            var field = inputBar.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = art;
            field.textComponent = tt;
            field.placeholder = placeholder;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 500;
            field.onSubmit.AddListener(_ => OnSend());
            field.onValueChanged.AddListener(_ => { SyncChips(); LayoutBar(); });
            input = field;
        }

        // ── 추천 칩 ──────────────────────────────────────
        // 시안 '첫대화': (20.5,713.5) 143×37 / 172.5 98 / 279.5 153 — 간격 9, 3번째가 화면 밖까지
        // 나가므로 가로로 넘길 수 있게 만든다. 누르면 라벨 #727886→#A855F7, 테두리 #181A20→#242831,
        // 그리고 입력창에 문구가 들어간다(시안 '첫대화-1').
        void BuildChips()
        {
            if (chipBar == null) return;
            chipBgs.Clear();
            chipLabels.Clear();
            for (int i = chipBar.childCount - 1; i >= 0; i--) DestroyImmediate(chipBar.GetChild(i).gameObject);

            if (chipBar.GetComponent<RectMask2D>() == null) chipBar.gameObject.AddComponent<RectMask2D>();
            var row = UiUtil.MakeNode(chipBar, "Row");
            var scroll = chipBar.GetComponent<ScrollRect>() ?? chipBar.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = chipBar;
            scroll.content = row;
            scroll.scrollSensitivity = 0f;

            float x = 0f;
            for (int i = 0; i < suggestions.Count; i++)
            {
                string word = suggestions[i];
                float w = Mathf.Round(MeasureWidth(word) + chipExtra);

                var chip = UiUtil.MakeNode(row, "Chip" + i);
                UiUtil.Place(chip, x, 0f, w, chipH);

                var bg = UiUtil.MakeImage(chip, "Bg", pillFill, DesignTokens.Bg.Glass, Image.Type.Sliced, true);
                Stretch(bg.transform);
                var line = UiUtil.MakeImage(chip, "Border", pillStroke, DesignTokens.Stroke.Ultralight, Image.Type.Sliced);
                Stretch(line.transform);

                var label = UiUtil.MakeText(chip, "Label", DesignTokens.Type.Body2,
                                            DesignTokens.Text.Tertiary, TextAlignmentOptions.Center);
                label.text = word;
                label.textWrappingMode = TextWrappingModes.NoWrap;   // 칩은 한 줄 고정
                label.overflowMode = TextOverflowModes.Overflow;
                Stretch(label.transform);

                int k = i;
                var b = bg.gameObject.AddComponent<Button>();
                b.targetGraphic = bg;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => PickChip(k));

                chipBgs.Add(line);
                chipLabels.Add(label);
                x += w + chipGap;
            }
            UiUtil.Place(row, 0f, 0f, Mathf.Max(x - chipGap, chipBar.rect.width), chipH);
            SyncChips();
        }

        void PickChip(int i)
        {
            chipPicked = i;
            if (input != null) input.text = suggestions[i];
            SyncChips();
        }

        void SyncChips()
        {
            // 추천 칩은 **내가 보낸 대화가 하나도 없을 때만** 보인다 (유저 지시 2026-08-21).
            // 시안도 '첫대화' 프레임에만 있고 사진 첨부 프레임엔 없다.
            if (chipBar != null)
            {
                bool sentAnything = false;
                for (int i = 0; i < messages.Count; i++)
                    if (messages[i].mine) { sentAnything = true; break; }
                if (chipBar.gameObject.activeSelf == sentAnything)
                    chipBar.gameObject.SetActive(!sentAnything);
            }

            for (int i = 0; i < chipBgs.Count; i++)
            {
                bool on = i == chipPicked;
                if (chipBgs[i] != null)
                    chipBgs[i].color = on ? DesignTokens.Stroke.Regular : DesignTokens.Stroke.Ultralight;
                if (chipLabels[i] != null)
                    chipLabels[i].color = on ? DesignTokens.Text.Point : DesignTokens.Text.Tertiary;
            }
            // 시안: 입력창이 비어 있으면 #242831, 문구가 들어가면 #181A20
            if (inputBar != null)
            {
                var img = inputBar.GetComponent<Image>();
                bool filled = input != null && !string.IsNullOrEmpty(input.text);
                if (img != null) img.color = filled ? DesignTokens.Bg.HighSurface : DesignTokens.Stroke.Regular;
            }
        }

        static void Stretch(Transform t)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // 칩 폭 = 글자 폭 + 좌우 여백. 글자 폭은 줄바꿈을 끄고 재야 한다.
        // (GetPreferredValues(s, 0, 0) 은 폭 0 으로 줄바꿈시켜서 글자 몇 개 폭만 돌려준다 — 칩이 잘렸던 원인)
        TextMeshProUGUI ruler;
        float MeasureWidth(string s)
        {
            if (ruler == null)
            {
                ruler = UiUtil.MakeText(transform, "__ruler", DesignTokens.Type.Body2, new Color(0f, 0f, 0f, 0f));
                ruler.textWrappingMode = TextWrappingModes.NoWrap;
                ruler.overflowMode = TextOverflowModes.Overflow;
                UiUtil.Place(ruler, 0f, 4000f, 4000f, 100f);
            }
            ruler.text = s;
            ruler.ForceMeshUpdate();
            return ruler.preferredWidth;
        }

        void Build()
        {
            if (content == null) return;
            foreach (var v in views) if (v != null) DestroyImmediate(v);
            views.Clear();

            int lastTheirs = -1;
            for (int i = 0; i < messages.Count; i++) if (!messages[i].mine) lastTheirs = i;

            // 첫 말풍선이 뷰포트 맨 위에 붙으면 아바타 위쪽이 마스크에 잘린다 → 위 여백을 준다
            float y = topPad;
            for (int i = 0; i < messages.Count; i++)
            {
                float h = MakeBubble(messages[i], y, i == lastTheirs);
                y += h + gap;
            }
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(0f, y - gap));
            Bottom();
        }

        // 말풍선 하나를 만들고 높이를 돌려준다
        // 말풍선 하나를 만들고 높이를 돌려준다.
        //
        // 시안 '텍스트 대화 상세.svg' 실측 —
        //   아바타 : 좌 x=80, #A855F7, 좌상 r8 / 나머지 r16, 글자 white 좌측정렬
        //   내 것  : 우 끝 393, #0F0D17, 우상 r8 / 나머지 r16, 글자 #F4F4F4 우측정렬
        //   폭 = 글자폭 + 34 (최대 300) / 높이 = 글자높이 + 40  → 1줄 54, 2줄 76 이 그대로 나온다
        //   글자 여백 좌우 17 · 상하 20, 세로는 가운데
        //   프로필 (19.5,131.5) 45 원 + 테두리 #242831 / 이름 (80.36,136.27) → 말풍선은 그 아래 30
        float MakeBubble(Message m, float y, bool lastReply)
        {
            var root = new GameObject(m.mine ? "Mine" : "Theirs", typeof(RectTransform));
            root.transform.SetParent(content, false);
            var rrt = root.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;
            rrt.anchoredPosition = new Vector2(0f, -y);

            var bubble = UiUtil.MakeImage(root.transform, "Bubble",
                                          m.mine ? bubbleMineSprite : bubbleThemSprite,
                                          m.mine ? BubbleGlassTop : DesignTokens.Action.Primary,
                                          Image.Type.Sliced);
            // 내 말풍선은 유리 — 시안 3x PNG 실측: 채움이 위 (12,12,19) → 아래 (19,19,26) 로
            // 아주 미세하게 밝아진다(평면색이 아니다). Linear/sRGB 차이를 피하려고
            // '시안에서 합성이 끝난 색' 을 알파 1 로 칠한다 (홈 카드와 같은 방식).
            if (m.mine)
                bubble.gameObject.AddComponent<GlassGradient>()
                      .Set(BubbleGlassTop, BubbleGlassBottom, 0f, 0f, 1f);
            var brt = (RectTransform)bubble.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(m.mine ? 1f : 0f, 1f);
            brt.pivot = new Vector2(m.mine ? 1f : 0f, 1f);

            float w, h;
            if (m.image != null)
            {
                // 이미지 말풍선 — 시안이 없어 말풍선 스프라이트 모양대로 잘라 넣고 원본 비율을 지킨다
                w = Mathf.Min(bubbleMaxW, imageBubbleW);
                float ratio = m.image.height / (float)Mathf.Max(1, m.image.width);
                h = Mathf.Min(w * ratio, imageBubbleMaxH);

                var maskImg = UiUtil.MakeImage(bubble.transform, "PhotoMask",
                                               m.mine ? bubbleMineSprite : bubbleThemSprite,
                                               Color.white, Image.Type.Sliced);
                var mrt = (RectTransform)maskImg.transform;
                mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
                mrt.offsetMin = Vector2.zero; mrt.offsetMax = Vector2.zero;
                var mk = maskImg.gameObject.AddComponent<Mask>();
                mk.showMaskGraphic = false;

                var photo = new GameObject("Photo", typeof(RectTransform), typeof(RawImage));
                photo.transform.SetParent(maskImg.transform, false);
                var prt = (RectTransform)photo.transform;
                prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                var raw = photo.GetComponent<RawImage>();
                raw.raycastTarget = false;
                UiUtil.FitCover(raw, m.image);
            }
            else
            {
                var t = UiUtil.MakeText(bubble.transform, "Text", DesignTokens.Type.Body3,
                                        m.mine ? mineTextColor : DesignTokens.Text.Primary,
                                        m.mine ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
                t.text = m.text;
                var lrt = (RectTransform)t.transform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(bubblePadX, bubblePadY);
                lrt.offsetMax = new Vector2(-bubblePadX, -bubblePadY);

                // 글자수에 맞춰 말풍선이 줄었다 늘었다 한다
                w = Mathf.Min(bubbleMaxW, MeasureBubbleWidth(t, m.text) + bubblePadX * 2f);
                h = t.GetPreferredValues(m.text, w - bubblePadX * 2f, 0f).y + bubblePadY * 2f;
            }
            brt.sizeDelta = new Vector2(w, h);
            brt.anchoredPosition = new Vector2(m.mine ? -(412f - bubbleRightEdge) : bubbleLeftX, 0f);

            // 시각 — 시안 48: 말풍선 바깥쪽, 하단에서 13.55 위. 12px white
            //   아바타는 말풍선 오른쪽 +8.39, 내 것은 화면 왼쪽 여백(20.39)에 붙는다
            if (!string.IsNullOrEmpty(m.time))
            {
                // 내 말풍선은 오른쪽에 붙으므로 시각도 그 '왼쪽' 에 오른쪽정렬로 붙어야 한다.
                // 예전엔 화면 왼쪽 여백(20.39)에 좌측정렬로 놔서 시각만 화면 끝에 떨어져 있었다.
                var tt = UiUtil.MakeText(root.transform, "Time", DesignTokens.Type.Caption1,
                                         DesignTokens.Text.Primary,
                                         m.mine ? TextAlignmentOptions.MidlineRight
                                                : TextAlignmentOptions.MidlineLeft);
                tt.fontSize = 12f;                       // 시안 실측 (토큰에 12 가 없다)
                tt.textWrappingMode = TextWrappingModes.NoWrap;
                tt.text = m.time;
                var trt = (RectTransform)tt.transform;
                // ★ 말풍선과 **같은 앵커 공간**에 놓아야 한다.
                //   말풍선은 내 것일 때 오른쪽 앵커에 -(412-bubbleRightEdge) 로 놓이는데,
                //   부모(root)는 412 가 아니라 372 라서 왼쪽 앵커 절대값으로 계산하면 어긋난다
                //   (2026-08-21 실기: 시각이 이미지 말풍선 안으로 들어가 겹쳤다).
                trt.anchorMin = trt.anchorMax = new Vector2(m.mine ? 1f : 0f, 1f);
                trt.pivot = new Vector2(m.mine ? 1f : 0f, 1f);
                float ty = (m.mine ? 0f : 30f) + h - 13.55f - 6f;   // 하단에서 13.55 위(잉크), 12px 박스 보정
                trt.anchoredPosition = new Vector2(
                    m.mine ? -(412f - bubbleRightEdge) - w - 8.39f    // 말풍선 왼쪽 모서리에서 8.39 왼쪽
                           : bubbleLeftX + w + 8.39f, -ty);
                trt.sizeDelta = new Vector2(70f, 16f);
            }

            float total = h;

            // 상대 말풍선엔 프로필과 이름이 붙는다
            if (!m.mine)
            {
                var nt = UiUtil.MakeText(root.transform, "Name", DesignTokens.Type.Caption1,
                                         DesignTokens.Text.Primary);
                nt.text = avatarName;
                UiUtil.Place(nt, 80.36f, 0f, 200f, 20f);

                brt.anchoredPosition = new Vector2(bubbleLeftX, -30f);
                total = h + 30f;

                // 프로필 — 시안은 원형으로 잘려 있다. Mask 안에 사진, 그 위에 1px 테두리.
                var maskImg = UiUtil.MakeImage(root.transform, "AvatarMask", circleSprite, Color.white);
                UiUtil.Place(maskImg, 19.5f, -4.77f, 45f, 45f);
                var mask = maskImg.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var fi = UiUtil.MakeImage(maskImg.transform, "Avatar", avatarSprite,
                                          avatarSprite != null ? Color.white : DesignTokens.Bg.HighSurface);
                var frt = (RectTransform)fi.transform;
                frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
                frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
                var ring = UiUtil.MakeImage(root.transform, "AvatarRing", circleStrokeSprite,
                                            DesignTokens.Stroke.Regular);
                UiUtil.Place(ring, 19.5f, -4.77f, 45f, 45f);

                // 액션 행 — 시안: 말풍선 아래 12, 32×32 원, 간격 8(피치 40)
                //   마지막 답변엔 북마크/재생성/신고 3개, 그 앞엔 북마크만
                total += actionGap + actionSize;
                float ay = 30f + h + actionGap;
                MakeAction(rrt, bubbleLeftX, ay, icScrap, null);
                if (lastReply)
                {
                    MakeAction(rrt, bubbleLeftX + actionPitch, ay, icRegen, null);
                    MakeAction(rrt, bubbleLeftX + actionPitch * 2f, ay, icReport, OpenReport);
                }
            }

            rrt.sizeDelta = new Vector2(0f, total);
            views.Add(root);
            return total;
        }

        // 줄바꿈을 끄고 한 줄 폭을 잰다. 그 폭이 최대폭을 넘으면 최대폭에서 접힌다.
        static float MeasureBubbleWidth(TMP_Text t, string s)
        {
            var keep = t.textWrappingMode;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.ForceMeshUpdate();
            float w = t.preferredWidth;
            t.textWrappingMode = keep;
            return w;
        }

        void MakeAction(RectTransform parent, float x, float y, Sprite icon, System.Action onClick)
        {
            var bg = UiUtil.MakeImage(parent, "Action", circleSprite, DesignTokens.Primitive.Violet._900,
                                      Image.Type.Simple, true);
            UiUtil.Place(bg, x, y, actionSize, actionSize);
            var glyph = UiUtil.MakeImage(bg.transform, "Icon", icon, DesignTokens.Text.Primary);
            UiUtil.Place(glyph, (actionSize - 24f) * 0.5f, (actionSize - 24f) * 0.5f, 24f, 24f);

            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() =>
            {
                // 시안: 누른 버튼은 violet/800 (#3A2B67)
                bg.color = bg.color == DesignTokens.Primitive.Violet._900
                    ? DesignTokens.Primitive.Violet._800
                    : DesignTokens.Primitive.Violet._900;
                if (onClick != null) onClick();
            });
        }

        void OpenReport()
        {
            if (reportSheet != null) reportSheet.Open();
            else Debug.LogWarning("[TalkDetail] reportSheet 미연결 — Sheet_Report 를 인스펙터에 물릴 것");
        }

        void OnSend()
        {
            // 씬 참조가 비어 있으면 입력바에서 직접 찾는다 (EnsureInput 이 아직 안 돈 경우)
            if (input == null && inputBar != null) input = inputBar.GetComponent<TMP_InputField>();
            if (input == null)
            {
                Debug.LogError("[TalkDetail] 입력 필드를 못 찾음 — inputBar 가 인스펙터에 물려 있는지 확인");
                return;
            }

            string text = input.text;
            bool hasText = !string.IsNullOrWhiteSpace(text);
            if (!hasText && pending.Count == 0)
            {
                Debug.Log("[TalkDetail] 입력도 첨부도 없어 전송하지 않음");
                return;
            }

            // 사진을 먼저 올리고 그 뒤에 글을 올린다 — 한 번의 전송으로 같이 나간다.
            foreach (var img in pending)
                if (img != null) messages.Add(new Message(img, Now()));
            pending.Clear();
            AttachmentsChanged?.Invoke();
            BuildTray();
            LayoutBar();

            if (hasText) messages.Add(new Message(true, text.Trim(), Now()));
            if (input != null) input.text = "";
            chipPicked = -1;
            SyncChips();
            Build();

            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(WaitReply());
        }

        // ── 입력창 자동 높이 · 첨부 트레이 (시안 개정판 '사진과 글 첨부', 3x PNG 실측) ──
        //   바        x 20 · 폭 372 · 바닥 838.5 · 기본 높이 67 · **max 280**(디자이너 주석)
        //   썸네일    76×76, 피치 84(간격 8), 첫 것 x 36 · 바 상단에서 15 아래
        //   × 배지    24²(잉크 원 20), 썸네일 우상단에서 (48,4)
        //   +/전송    바 바닥에서 중심 33.5 위
        [Header("첨부 트레이 (시안 실측)")]
        [SerializeField] RectTransform attachTray;     // 뷰포트 (RectMask2D 로 바 밖을 자른다)
        [SerializeField] RectTransform attachContent;  // 썸네일 셀이 들어가는 곳 (가로로 길어진다)
        [SerializeField] RectTransform btnPlus;        // 바의 자식이라 높이가 바뀌면 다시 앉혀야 한다
        [SerializeField] Sprite attachRemoveSprite;    // ic_attach_remove (× 배지)
        [SerializeField] float barBottom = 54.5f;   // 화면 '아래'에서 (하단 앵커라 비율에 안 흔들린다)
        [SerializeField] float barMinH = 67f;
        [SerializeField] float barMaxH = 280f;
        [SerializeField] float thumbSize = 76f;
        [SerializeField] float thumbPitch = 84f;
        [SerializeField] float trayTopPad = 15f;
        [SerializeField] float removeSize = 24f;
        // 시안 두 상태를 각각 재보니 **모양이 바뀐다** (디자이너 주석 "바로 모양이 바뀌면서").
        //   빈 상태(67)  = 알약 r33.5   ← bg_pill67
        //   늘어난 상태  = r16          ← bg_round16 (사방 9슬라이스라 세로로 늘려도 안 깨진다)
        [SerializeField] Sprite barPillSprite;
        [SerializeField] Sprite barRoundSprite;

        Image inputBarImage;
        RectTransform textArea;   // EnsureInput 이 만든 입력 영역 — 상태에 따라 위·아래를 비운다

        readonly List<GameObject> trayViews = new List<GameObject>();
        float lineH = 21f;   // 한 줄 높이(14px × 140%) — 실제 폰트에서 다시 잰다
        float appliedH = -1f;   // 지금 적용된 바 높이 (같으면 rect 를 안 건드린다)

        // 내 말풍선 유리 — 시안 '첫대화.png'(3x) 세로 실측값 (배경 #0F1115 위에서 합성이 끝난 색)
        static readonly Color BubbleGlassTop    = new Color32(12, 12, 19, 255);
        static readonly Color BubbleGlassBottom = new Color32(19, 19, 26, 255);

        void BuildTray()
        {
            var root = attachContent != null ? attachContent : attachTray;
            if (root == null) return;

            for (int i = root.childCount - 1; i >= 0; i--)
                DestroyImmediate(root.GetChild(i).gameObject);
            trayViews.Clear();

            for (int i = 0; i < pending.Count; i++)
            {
                var tex = pending[i];
                var cell = new GameObject("Attach" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                cell.transform.SetParent(root, false);
                var rt = cell.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(i * thumbPitch, 0f);
                rt.sizeDelta = new Vector2(thumbSize, thumbSize);
                UiUtil.FitCover(cell.GetComponent<RawImage>(), tex);

                // × 배지 — 썸네일 우상단
                var x = new GameObject("Remove", typeof(RectTransform), typeof(CanvasRenderer),
                                       typeof(Image), typeof(Button));
                x.transform.SetParent(cell.transform, false);
                var xrt = x.GetComponent<RectTransform>();
                xrt.anchorMin = xrt.anchorMax = new Vector2(0f, 1f);
                xrt.pivot = new Vector2(0f, 1f);
                xrt.anchoredPosition = new Vector2(thumbSize - removeSize - 4f, -4f);
                xrt.sizeDelta = new Vector2(removeSize, removeSize);
                x.GetComponent<Image>().sprite = attachRemoveSprite;
                var captured = tex;
                var xbtn = x.GetComponent<Button>();
                xbtn.targetGraphic = x.GetComponent<Image>();   // 없으면 클릭 판정이 안 잡히는 기기가 있다
                xbtn.onClick.AddListener(() =>
                {
                    Debug.Log($"[TalkDetail] 첨부 × 클릭 (남은 {pending.Count}장)");
                    RemoveAttachment(captured);
                });

                trayViews.Add(cell);
            }

            // 내용 폭은 셀 수만큼 (5장이면 412 > 뷰포트 356 이라 가로로 밀린다 — 시안도 5번째가 잘려 있다)
            root.sizeDelta = new Vector2(
                Mathf.Max(0f, pending.Count * thumbPitch - (thumbPitch - thumbSize)), thumbSize);
            if (root != attachTray) root.anchoredPosition = new Vector2(0f, 0f);
            if (attachTray != null) attachTray.gameObject.SetActive(pending.Count > 0);
        }

        // 첨부·글 양에 따라 바를 늘린다. 바닥은 고정이고 위로 자란다.
        //
        // 시안 '사진과 글 첨부'(3x PNG) 한 장에서 역산한 구성 — 값의 출처를 남긴다:
        //   바 584~838.5(=254.5) · 썸네일 599~675 · 글 690~775(4줄) · + 787~823
        //   ⇒ 위여백 15 + [썸네일 76 + 15] + 글높이 + 12 + +줄 36 + 아래여백 15.5 = 254.5 ✓
        // 한 줄이고 첨부도 없으면 시안 기본형(67, 글과 + 가 같은 줄)이다.
        // 디자이너 주석: "두줄이상일 때는 모양이 바뀌면서 계속 길어져요 … 높이 max 280".
        void LayoutBar()
        {
            if (inputBar == null) return;

            float textH = 0f;
            if (input != null && input.textComponent != null)
            {
                var tc = input.textComponent;
                textH = tc.preferredHeight;
                lineH = Mathf.Max(lineH, tc.fontSize * 1.4f);
            }
            bool multiline = textH > lineH * 1.6f;
            bool stacked = pending.Count > 0 || multiline;

            float h;
            if (!stacked) h = barMinH;
            else
            {
                h = trayTopPad;                                   // 위여백 15
                if (pending.Count > 0) h += thumbSize + trayTopPad;  // 썸네일 76 + 15
                h += Mathf.Max(textH, lineH) + 12f;                // 글 + 아래 간격
                h += 36f + 15.5f;                                  // + 줄 + 아래여백
            }
            h = Mathf.Clamp(h, barMinH, barMaxH);

            // ⚠ inputBar 는 TMP_InputField 가 붙어 있는 바로 그 오브젝트다.
            //   글자 칠 때마다 rect 를 건드리면 입력이 끊긴다 → 높이가 실제로 바뀔 때만 손댄다.
            if (Mathf.Abs(h - appliedH) < 0.5f) return;
            appliedH = h;

            // 짧을 땐 알약, 늘어나면 r16 — 시안 두 상태를 각각 실측한 값이다.
            if (inputBarImage == null) inputBarImage = inputBar.GetComponent<Image>();
            if (inputBarImage != null)
            {
                var want = stacked ? barRoundSprite : barPillSprite;
                if (want != null && inputBarImage.sprite != want) inputBarImage.sprite = want;
            }

            inputBar.sizeDelta = new Vector2(inputBar.sizeDelta.x, h);
            PlaceBar();

            if (attachTray != null)
                attachTray.anchoredPosition = new Vector2(attachTray.anchoredPosition.x, -trayTopPad);

            // + 는 바의 자식이라 바가 자라면 같이 올라간다 → 바닥 기준으로 다시 앉힌다.
            // 시안: + 박스 상단이 절대 787 (중심 805, 바닥 838.5 에서 33.5 위)
            // + 는 바의 **하단 앵커** 자식이라 바가 자라도 알아서 바닥에 붙어 있다 (코드로 안 옮긴다)

            // 글 영역은 트레이·+ 줄과 자리를 나눠 쓴다 (시안 구성: 트레이 → 글 → + 줄)
            if (textArea != null)
            {
                float top = pending.Count > 0 ? trayTopPad + thumbSize + trayTopPad : 16f;
                float bottom = stacked ? 16f + 36f + 12f : 16f;
                // 쌓인 상태에선 + 가 아래로 내려가므로 글이 전체 폭을 쓴다.
                //   시안 실측: 글 좌단 36~38.67 (바 20 기준 로컬 16) — 기본 상태의 80 이 아니다.
                float side = stacked ? 16f : 60f;
                textArea.offsetMin = new Vector2(side, bottom);
                textArea.offsetMax = new Vector2(stacked ? -16f : -60f, -top);
            }
        }

        // ── 전송 대기 첨부 (사진 + 글을 같이 보내기 위한 것) ──────────────
        // 예전엔 시트에서 고르는 즉시 말풍선이 올라가서 글과 같이 보낼 수가 없었다.
        // 이제 고른 사진은 '대기' 로만 들고 있다가 전송을 누를 때 글과 함께 나간다(유저 요청 2026-08-18).
        readonly List<Texture2D> pending = new List<Texture2D>();
        public IReadOnlyList<Texture2D> Pending => pending;
        public event System.Action AttachmentsChanged;   // 첨부 트레이 UI 가 구독한다
        public const int MaxAttachments = 5;             // 시안 '사진 선택 (n/5)'

        // 이미지 선택 시트에서 고른 사진을 '첨부' 로 담는다. 실제 전송은 전송 버튼에서.
        public void SendImages(IList<Texture2D> images)
        {
            if (images == null || images.Count == 0) return;
            foreach (var img in images)
            {
                if (img == null || pending.Contains(img)) continue;
                // 상한은 ImagePickerSheet 가 '고르는 순간' 에 막는다 — 여기선 조용히 자른다.
                if (pending.Count >= MaxAttachments) break;
                pending.Add(img);
            }
            AttachmentsChanged?.Invoke();
            BuildTray();
            LayoutBar();
        }

        public void RemoveAttachment(Texture2D img)
        {
            if (img == null || !pending.Remove(img)) return;
            AttachmentsChanged?.Invoke();
            BuildTray();
            LayoutBar();
        }

        // 실제 응답은 AI 인터페이스가 붙은 뒤에 온다. 지금은 표시만 한다.
        IEnumerator WaitReply()
        {
            ShowTyping(true);
            float t = 0f;
            int frame = 0;
            while (t < replyDelay)
            {
                t += Time.deltaTime;
                if (typingDots != null && typingFrames != null && typingFrames.Length > 0)
                {
                    frame = Mathf.FloorToInt(t * typingFps) % typingFrames.Length;
                    typingDots.sprite = typingFrames[frame];
                }
                yield return null;
            }
            ShowTyping(false);
            Debug.Log("[TalkDetail] 응답 대기 — AI 인터페이스 연결 필요");
            typing = null;
        }

        void ShowTyping(bool on)
        {
            if (typingDots != null) typingDots.gameObject.SetActive(on);
        }

        void Bottom()
        {
            if (scroll != null) scroll.verticalNormalizedPosition = 0f;
        }

        static string Now()
        {
            var d = System.DateTime.Now;
            string ap = d.Hour < 12 ? "오전" : "오후";
            int h = d.Hour % 12; if (h == 0) h = 12;
            return $"{ap} {h}:{d.Minute:00}";
        }

        static void Go(string id)
        {
            if (ScreenManager.Instance != null && !string.IsNullOrEmpty(id))
                ScreenManager.Instance.Show(id);
        }
    }
}
