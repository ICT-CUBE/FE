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
            if (!TouchScreenKeyboard.visible) return 0f;

            float px = TouchScreenKeyboard.area.height;
#if UNITY_ANDROID && !UNITY_EDITOR
            // 기기·안드로이드 버전에 따라 area 가 0 으로 오는 경우가 있다.
            // 그럴 땐 창의 '보이는 영역' 으로 직접 잰다.
            if (px <= 1f) px = AndroidKeyboardPx();
#endif
            if (px <= 1f) return 0f;

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

        void ApplyLift(float h)
        {
            foreach (var kv in basePos)
                if (kv.Key != null) kv.Key.anchoredPosition = kv.Value + new Vector2(0f, h);

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
            RememberBase(inputBar);
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
            art.offsetMin = new Vector2(47.5f, 8f);   // + 버튼 자리 (시안 placeholder 잉크 x68.56)
            art.offsetMax = new Vector2(-60f, -8f);   // 오른쪽은 전송 버튼 자리

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(area.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var tt = textGo.GetComponent<TextMeshProUGUI>();
            ApplyFont(tt);
            tt.fontSize = 14f;
            // 시안 입력바는 밝은 회색(#F4F5F7) 이라 글씨가 검정이다
            tt.color = new Color(0.019608f, 0.019608f, 0.019608f, 1f);
            tt.alignment = TextAlignmentOptions.MidlineLeft;
            tt.richText = false;

            var field = inputBar.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = art;
            field.textComponent = tt;
            field.placeholder = placeholder;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 500;
            field.onSubmit.AddListener(_ => OnSend());
            field.onValueChanged.AddListener(_ => SyncChips());
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
                                          m.mine ? DesignTokens.Bg.Glass : DesignTokens.Action.Primary,
                                          Image.Type.Sliced);
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
                var tt = UiUtil.MakeText(root.transform, "Time", DesignTokens.Type.Caption1,
                                         DesignTokens.Text.Primary,
                                         m.mine ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineLeft);
                tt.fontSize = 12f;                       // 시안 실측 (토큰에 12 가 없다)
                tt.textWrappingMode = TextWrappingModes.NoWrap;
                tt.text = m.time;
                var trt = (RectTransform)tt.transform;
                trt.anchorMin = trt.anchorMax = new Vector2(m.mine ? 0f : 0f, 1f);
                trt.pivot = new Vector2(0f, 1f);
                float ty = (m.mine ? 0f : 30f) + h - 13.55f - 6f;   // 하단에서 13.55 위(잉크), 12px 박스 보정
                trt.anchoredPosition = new Vector2(m.mine ? 20.39f : bubbleLeftX + w + 8.39f, -ty);
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
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.Log("[TalkDetail] 입력이 비어 있어 전송하지 않음");
                return;
            }

            messages.Add(new Message(true, text.Trim(), Now()));
            if (input != null) input.text = "";
            chipPicked = -1;
            SyncChips();
            Build();

            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(WaitReply());
        }

        // 이미지 선택 시트에서 고른 사진을 대화에 올린다.
        public void SendImages(IList<Texture2D> images)
        {
            if (images == null || images.Count == 0) return;
            foreach (var img in images)
                if (img != null) messages.Add(new Message(img, Now()));
            Build();

            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(WaitReply());
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
