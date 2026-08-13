using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 텍스트 대화 검색. '텍스트 대화 검색.svg' 실측 그대로 배치한다.
    //
    //   검색바      (20.5, 72.5) 327×55
    //   최근 검색   라벨 (20.53,152.27) / 전체 삭제 우측끝 390.4
    //   칩          첫 행 y=190, 높이 36, 행간 48, 가로 간격 8, 좌 20
    //               폭 = 라벨폭 + 50  (좌여백 12.5 + 라벨 + 13.5 + X 8 + 우여백 16)
    //   구분선      마지막 칩 행 아래 32  (시안 2행 → y=306)
    //   결과 라벨   구분선 + 36.27       (시안 342.27)
    //   결과 첫 행  구분선 + 85.91       (시안 391.91), 행 간격 83
    //               본문 14px, 날짜 = 본문 + 25.77 (13px #727886), 키워드는 #A855F7 하이라이트
    //
    // 검색 대상은 AI/백엔드가 붙기 전까지 아래 lines 더미다. 결과는 실제로 부분일치 검색해서 만든다.
    public class TalkSearchController : MonoBehaviour
    {
        [System.Serializable]
        public class Line
        {
            public string text;
            public string date;
            public Line(string t, string d) { text = t; date = d; }
        }

        [Header("상단")]
        [SerializeField] RectTransform searchBar;
        [SerializeField] TMP_Text searchHint;      // placeholder 로 재활용
        [SerializeField] Button btnCancel;
        [SerializeField] TMP_Text labelRecent;
        [SerializeField] Button btnClearAll;
        [SerializeField] TMP_Text labelClearAll;

        [Header("칩 / 결과")]
        [SerializeField] RectTransform chipList;   // 칩을 담는 빈 컨테이너
        [SerializeField] ScrollRect resultScroll;
        [SerializeField] RectTransform content;
        [SerializeField] Sprite pillSprite;        // bg_pill (9-slice)
        [SerializeField] Sprite chipCloseSprite;   // ic_chip_close

        [Header("시안 실측")]
        [SerializeField] float chipFirstY = 190f;
        [SerializeField] float chipH = 36f;
        [SerializeField] float rowPitch = 48f;
        [SerializeField] float chipGapX = 8f;
        [SerializeField] float chipLeft = 20f;
        [SerializeField] float chipPadL = 12.5f;
        [SerializeField] float chipExtra = 50f;    // 라벨폭에 더할 값
        [SerializeField] float dividerGap = 32f;
        [SerializeField] float labelOffset = 36.27f;
        [SerializeField] float firstRowOffset = 85.91f;
        [SerializeField] float resultPitch = 83f;
        [SerializeField] float dateOffset = 25.77f;

        [Header("데이터 (임시 — 백엔드 전까지 더미)")]
        [SerializeField] List<string> recent = new List<string>
        { "콘서트", "추천 노래", "뭐먹지", "여행가고 싶지", "대박인디?" };

        [SerializeField]
        List<Line> lines = new List<Line>
        {
            new Line("맞아, 콘서트 현장 에너지는 진짜 말로 표현 못해...", "2026. 08. 05"),
            new Line("여름밤에 열리는 콘서트는 정말 잊을 수 없는 경험이야.", "2026. 01. 05"),
            new Line("콘서트 후의 여운은 한참동안 계속되곤 해.", "2025. 12. 12"),
            new Line("콘서트의 마지막 곡은 항상 마음을 울리더라.", "2025. 11. 30"),
            new Line("이번 콘서트는 정말 대단했어, 친구들과 함께여서 더 좋았어.", "2025. 10. 22"),
            new Line("콘서트에서 느낀 감정은 언제나 특별해.", "2025. 09. 15"),
        };

        // 하이라이트 색 — 상자는 반투명이라야 글씨가 안 묻힌다
        [SerializeField] string markColor = "#7C3AEDB3";   // #7C3AED 70%
        [SerializeField] string hitColor = "#FFFFFF";      // 하이라이트 위 글씨

        TMP_InputField input;
        string query = "";
        readonly List<GameObject> chipViews = new List<GameObject>();
        readonly List<GameObject> rowViews = new List<GameObject>();

        void OnEnable()
        {
            EnsureInput();
            if (btnCancel != null) btnCancel.onClick.AddListener(Cancel);
            if (btnClearAll != null) btnClearAll.onClick.AddListener(ClearAll);
            query = input != null ? input.text : "";
            Rebuild();
        }

        void OnDisable()
        {
            if (btnCancel != null) btnCancel.onClick.RemoveAllListeners();
            if (btnClearAll != null) btnClearAll.onClick.RemoveAllListeners();
        }

        // 시안 검색바는 이미지 + 힌트뿐이라 글자를 칠 수 없다. TMP_InputField 는 직렬화 필드가
        // 많아 씬에 손으로 만들면 빠뜨리기 쉬워서 코드로 붙인다 (TalkDetail 과 같은 방식).
        void EnsureInput()
        {
            if (input != null || searchBar == null) return;

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(searchBar, false);
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(52f, 6f);    // 돋보기(x43,16) 오른쪽부터
            art.offsetMax = new Vector2(-16f, -6f);

            var tt = UiUtil.MakeText(area.transform, "Text", DesignTokens.Type.Body1,
                                     DesignTokens.Text.Primary, TextAlignmentOptions.MidlineLeft);
            var trt = (RectTransform)tt.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            tt.richText = false;

            var field = searchBar.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = art;
            field.textComponent = tt;
            field.placeholder = searchHint;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 50;
            field.onValueChanged.AddListener(OnQueryChanged);
            input = field;
        }

        void OnQueryChanged(string v)
        {
            query = v;
            BuildResults();
        }

        void Cancel()
        {
            if (ScreenManager.Instance != null) ScreenManager.Instance.Back();
        }

        void ClearAll()
        {
            recent.Clear();
            Rebuild();
        }

        void Rebuild()
        {
            BuildChips();
            BuildResults();
        }

        // ── 최근 검색 칩 ──────────────────────────────────
        void BuildChips()
        {
            foreach (var v in chipViews) if (v != null) DestroyImmediate(v);
            chipViews.Clear();
            if (chipList == null) return;

            // 칩은 시안 절대좌표로 놓는다 → 컨테이너를 화면 전체로 펴 둔다(빈 컨테이너, 알파 0)
            chipList.anchorMin = Vector2.zero;
            chipList.anchorMax = Vector2.one;
            chipList.pivot = new Vector2(0.5f, 0.5f);
            chipList.offsetMin = Vector2.zero;
            chipList.offsetMax = Vector2.zero;
            var holder = chipList.GetComponent<Image>();
            if (holder != null) { holder.raycastTarget = false; var c = holder.color; c.a = 0f; holder.color = c; }

            bool any = recent.Count > 0;
            if (labelRecent != null) labelRecent.gameObject.SetActive(any);
            if (labelClearAll != null) labelClearAll.gameObject.SetActive(any);

            float x = chipLeft, y = chipFirstY, lastBottom = chipFirstY + chipH;
            for (int i = 0; i < recent.Count; i++)
            {
                string word = recent[i];
                float w = ChipWidth(word);
                if (x > chipLeft && x + w > 412f - chipLeft)   // 다음 행으로
                {
                    x = chipLeft;
                    y += rowPitch;
                }
                MakeChip(word, x, y, w);
                lastBottom = y + chipH;
                x += w + chipGapX;
            }

            // 구분선 아래(결과 스크롤)를 칩 행 수에 맞춰 내린다 — 시안 2행이면 274+32 = 306
            SetScrollTop(any ? lastBottom + dividerGap : 152f);
        }

        // stretch 앵커의 상단 여백만 바꾼다. 하단 여백·좌우는 씬 값 그대로 둔다.
        void SetScrollTop(float top)
        {
            if (resultScroll == null) return;
            var rt = (RectTransform)resultScroll.transform;
            float bottom = -rt.sizeDelta.y * 0.5f + rt.anchoredPosition.y;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, -(top + bottom));
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(top - bottom) * 0.5f);
        }

        float ChipWidth(string word)
        {
            float labelW = MeasureWidth(word, DesignTokens.Type.Body3);
            return Mathf.Round(labelW + chipExtra);
        }

        void MakeChip(string word, float x, float y, float w)
        {
            var chip = UiUtil.MakeNode(chipList, "Chip_" + word);
            UiUtil.Place(chip, x, y, w, chipH);

            var bg = UiUtil.MakeImage(chip, "Bg", pillSprite, DesignTokens.Bg.HighSurface,
                                      Image.Type.Sliced, true);
            var brt = (RectTransform)bg.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var pick = bg.gameObject.AddComponent<Button>();
            pick.targetGraphic = bg;
            pick.transition = Selectable.Transition.None;
            string w2 = word;
            pick.onClick.AddListener(() => { if (input != null) input.text = w2; });

            var label = UiUtil.MakeText(chip, "Label", DesignTokens.Type.Body3,
                                        DesignTokens.Text.Primary, TextAlignmentOptions.MidlineLeft);
            label.text = word;
            label.textWrappingMode = TextWrappingModes.NoWrap;   // 칩은 한 줄 고정
            label.overflowMode = TextOverflowModes.Overflow;
            UiUtil.Place(label, chipPadL, 0f, w - chipPadL - 30f, chipH);

            // 시안: X 획이 (칩폭-24, 중앙) 에서 8×8. 잘라낸 아이콘은 16×16 박스 안에 8×8 획이라
            // 8×8 로 넣으면 획이 절반(4×4)으로 작아진다 → 같은 중심에 16×16 으로 놓는다.
            var close = UiUtil.MakeImage(chip, "Close", chipCloseSprite, DesignTokens.Text.Primary,
                                         Image.Type.Simple, true);
            UiUtil.Place(close, w - 28f, (chipH - 16f) * 0.5f, 16f, 16f);
            var del = UiUtil.MakeHit(chip, "CloseHit", () => { recent.Remove(w2); Rebuild(); });
            UiUtil.Place(del, w - 32f, 2f, 32f, chipH - 4f);

            chipViews.Add(chip.gameObject);
        }

        // ── 검색 결과 ────────────────────────────────────
        void BuildResults()
        {
            foreach (var v in rowViews) if (v != null) DestroyImmediate(v);
            rowViews.Clear();
            if (content == null) return;

            string q = (query ?? "").Trim();
            var hits = new List<Line>();
            if (q.Length > 0)
                foreach (var l in lines)
                    if (l.text.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0) hits.Add(l);

            if (hits.Count == 0)
            {
                content.sizeDelta = new Vector2(content.sizeDelta.x, 0f);
                return;
            }

            var divider = UiUtil.MakeImage(content, "Divider", null, DesignTokens.Stroke.Regular);
            UiUtil.Place(divider, 20f, 0f, 372f, 1f);
            rowViews.Add(divider.gameObject);

            var head = UiUtil.MakeText(content, "LabelResult", DesignTokens.Type.Body1,
                                       DesignTokens.Text.Primary, TextAlignmentOptions.TopLeft);
            head.text = "대화 내 검색 결과";
            UiUtil.Place(head, 20.91f, labelOffset, 300f, 22f);
            rowViews.Add(head.gameObject);

            for (int i = 0; i < hits.Count; i++)
            {
                float y = firstRowOffset + i * resultPitch;

                var body = UiUtil.MakeText(content, "Row" + i, DesignTokens.Type.Body3,
                                           DesignTokens.Text.Primary, TextAlignmentOptions.TopLeft);
                body.richText = true;
                body.text = Highlight(hits[i].text, q);
                body.overflowMode = TextOverflowModes.Ellipsis;
                UiUtil.Place(body, 20.9f, y, 372f - 20.9f, 21f);
                rowViews.Add(body.gameObject);

                var date = UiUtil.MakeText(content, "Date" + i, DesignTokens.Type.Caption1,
                                           DesignTokens.Text.Tertiary, TextAlignmentOptions.TopLeft);
                date.text = hits[i].date;
                UiUtil.Place(date, 20.85f, y + dateOffset, 200f, 18f);
                rowViews.Add(date.gameObject);
            }

            content.sizeDelta = new Vector2(content.sizeDelta.x,
                                            firstRowOffset + hits.Count * resultPitch);
            if (resultScroll != null) resultScroll.verticalNormalizedPosition = 1f;
        }

        // 시안의 키워드 배경(#A855F7 20px 사각형) = TMP 의 <mark>
        string Highlight(string text, string q)
        {
            string safe = text.Replace("<", "‹").Replace(">", "›");
            if (string.IsNullOrEmpty(q)) return safe;

            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (true)
            {
                int at = safe.IndexOf(q, i, System.StringComparison.OrdinalIgnoreCase);
                if (at < 0) { sb.Append(safe.Substring(i)); break; }
                sb.Append(safe, i, at - i);
                // 시안: 상자 #7C3AED · 글씨 #DDD6FE.
                // TMP 의 <mark> 는 글자 '위에' 그려지는 경우가 있어 불투명하면 글씨가 통째로 묻힌다.
                // 알파를 낮춰 두면 어느 쪽으로 그려지든 글씨가 보인다.
                sb.Append("<mark=").Append(markColor).Append(">")
                  .Append("<color=").Append(hitColor).Append(">")
                  .Append(safe, at, q.Length)
                  .Append("</color></mark>");
                i = at + q.Length;
            }
            return sb.ToString();
        }

        // 칩 폭 = 글자 폭 + 50(시안). 글자 폭은 줄바꿈을 끄고 재야 한다.
        // (GetPreferredValues(s, 0, 0) 은 폭 0 으로 줄바꿈시켜서 글자 몇 개 폭만 돌려준다 — 칩이 잘렸던 원인)
        TextMeshProUGUI ruler;
        float MeasureWidth(string s, TypeStyle style)
        {
            if (ruler == null)
            {
                ruler = UiUtil.MakeText(transform, "__ruler", style, new Color(0f, 0f, 0f, 0f));
                ruler.textWrappingMode = TextWrappingModes.NoWrap;
                ruler.overflowMode = TextOverflowModes.Overflow;
                UiUtil.Place(ruler, 0f, 4000f, 4000f, 100f);   // 화면 밖
            }
            style.Apply(ruler);
            ruler.text = s;
            ruler.ForceMeshUpdate();
            return ruler.preferredWidth;
        }
    }
}
