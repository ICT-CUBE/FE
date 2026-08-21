using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // Sheet_CommunityPhoto — 커뮤니티 글쓰기의 '사진 선택' 바텀시트.
    // 시안 '커뮤니티 사잔첨부 사진 선택.svg'(4/5 선택 상태) · '커뮤니티 사잔첨부.svg'(파일명은 '사진
    // 첨부' 지만 실제로는 같은 시트의 0/5 선택 상태 — 헤더 '첨부' 비활성 회색을 여기서 실측했다).
    //
    // Sheet_ImagePicker/ImagePickerSheet.cs 와 구조는 비슷하지만(3열 그리드·체크 원) Task9 지시대로
    // 완전히 새로 만들었다(그쪽은 TalkDetailController 를 직접 참조해서 공용화하면 회귀가 난다).
    // 레이아웃 수치는 참고만 하고 이 시안에서 다시 쟀는데, 그리드(3열 120×120, 간격 6/8, 첫 행 y269)와
    // 체크 원(19×19, inset 10.5)은 재측정값이 참고값과 사실상 일치했다. 다만 이 시안은 선택된 카드에
    // 테두리 강조가 없다(번호 배지만 바뀐다) — 그래서 카드 Bg/Line 은 선택 여부와 무관하게 항상 같다.
    //
    // 실측 (412×893 기준, 3x PNG)
    //   시트 top 171 · 그랩 (184,179) 44×4 · '<' 24×24 박스 (19,210)
    //   제목 잉크 (150.73,214.17) 109.01×17.91 "사진 선택 (4/5)" → 18 SemiBold(Headline3).
    //     ink y214 로 기존 ImagePickerSheet 의 '사진 선택' 제목값과 사실상 일치 — 별개로 재측정해도
    //     같은 값으로 수렴해서 확정.
    //   '첨부' 잉크 (363.89,215.20) 26.64×14.19 — 폭만으로는 굵기가 안 갈린다(Pretendard 한글 음절은
    //     굵기가 달라도 자간폭이 동일). 같은 시안(다이얼로그 위쪽, 딤 처리된 글쓰기 헤더)에 있는
    //     '등록'(Task8 에서 15 SemiBold 로 확정, 잉크폭 26.56)과 폭이 사실상 같고 같은 "헤더 우측
    //     액션 라벨" 역할이라 15 SemiBold 로 확정. 활성 #A855F7 / 비활성 #727886 — 활성은 '사진 선택
    //     (4/5)' 파일, 비활성은 '사진 선택 (0/5)' 상태를 담은 '커뮤니티 사잔첨부.svg'에서 실측했다
    //     (파일명과 달리 이 파일도 시트가 열린 상태였다 — 둘 다 같은 시트의 다른 선택 상태였다).
    //   구분선 (20,254) 372×1 #242831(Stroke.Regular).
    //   그리드 3열 120×120, x=20/146/272(간격 6), 첫 행 y269, 세로 피치 128(간격 8).
    //   체크 원 19×19 at 카드 +(10.5,10.5). 미선택 흰 채움+#727886 테두리, 선택 #A855F7 채움+#7C3AED
    //   테두리(circle.svg/circle_stroke.svg 재사용 — ImagePickerSheet 와 동일 자산, 크기도 일치해서
    //   그대로 채택). 번호 잉크 높이 8.48~8.72 → 12px(ImagePickerSheet 와 동일 결론).
    //
    // 사진은 AR 캡처(CaptureService)를 쓴다. 캡처가 없으면 ImagePickerSheet 와 동일하게 '사진'
    // 플레이스홀더 카드를 깐다 — 이 시안엔 빈 카드 예시가 없어 그 처리를 그대로 따랐다.
    public class CommunityPhotoSheet : MonoBehaviour
    {
        [Header("구성")]
        [SerializeField] GameObject sheetRoot;          // 비우면 자기 자신
        [SerializeField] RectTransform content;         // 스크롤 Content
        [SerializeField] Button btnBack;                // '<' — 그냥 닫기
        [SerializeField] Button btnAttach;               // '첨부' — 1장 이상일 때만 활성
        [SerializeField] TMP_Text titleLabel;            // "사진 선택 (n/5)"
        [SerializeField] TMP_Text attachLabel;           // '첨부' 글자 — 활성/비활성 색 토글

        [Header("데이터")]
        [SerializeField] CaptureService captureSource;
        [SerializeField] CommunityWriteController writeController;
        [SerializeField] int placeholderCount = 15;      // 캡처가 없을 때 보여줄 빈 카드 수 (3열 × 5줄)

        [Header("스프라이트")]
        [SerializeField] Sprite cardSprite;              // bg_round8 (최종 리뷰 I4 — 시안 반지름 8, bg_round12 아님)
        [SerializeField] Sprite cardStrokeSprite;        // 더 안 쓴다(최종 리뷰 I5 — 시안 카드에 테두리가 없다). 필드는
                                                          // 씬 배선이 남아 있어도 무해해서 그대로 둔다.
        [SerializeField] Sprite circleSprite;            // circle
        [SerializeField] Sprite circleStrokeSprite;      // circle_stroke

        [Header("배치 (시안)")]
        [SerializeField] int columns = 3;
        [SerializeField] float cardSize = 120f;
        [SerializeField] float gapX = 6f;
        [SerializeField] float gapY = 8f;
        [SerializeField] float checkSize = 19f;
        [SerializeField] float checkInset = 10.5f;

        const int MaxPhotos = 5;

        static readonly Color CardBg    = DesignTokens.Bg.HighSurface;   // #181A20 — 빈 카드(캡처 없을 때)
        static readonly Color CheckLine = DesignTokens.Text.Tertiary;    // #727886 — 미선택 원 테두리
        static readonly Color SelFill   = DesignTokens.Action.Primary;   // #A855F7 — 선택 원 채움
        static readonly Color SelLine   = DesignTokens.Action.Pressed;   // #7C3AED — 선택 원 테두리
        static readonly Color PhotoText = DesignTokens.Text.Secondary;   // #D3D5DD — '사진' 플레이스홀더

        class Card
        {
            public CaptureService.Entry entry;
            public Image ring, ringLine, check, checkLine;
            public TMP_Text number;
        }

        readonly List<Card> cards = new List<Card>();
        readonly List<int> picked = new List<int>();     // 선택 순서 (원 안 숫자)

        GameObject Root => sheetRoot != null ? sheetRoot : gameObject;

        void Awake()
        {
            if (btnBack != null) btnBack.onClick.AddListener(Close);
            if (btnAttach != null) btnAttach.onClick.AddListener(OnAttach);
        }

        void OnEnable()
        {
            picked.Clear();
            Build();
            Refresh();
        }

        public void Close() => Root.SetActive(false);

        void Build()
        {
            if (content == null) { Debug.LogError("[CommunityPhoto] content 가 비어 있다"); return; }
            UiUtil.ClearChildren(content);
            cards.Clear();

            var list = captureSource != null ? captureSource.Captures : null;
            int n = (list != null && list.Count > 0) ? list.Count : placeholderCount;

            float pitchX = cardSize + gapX, pitchY = cardSize + gapY;
            for (int i = 0; i < n; i++)
            {
                var e = (list != null && i < list.Count) ? list[i] : null;
                int col = i % columns, row = i / columns;

                var cardGo = UiUtil.MakeNode(content, "Card" + i);
                UiUtil.Place(cardGo, col * pitchX, row * pitchY, cardSize, cardSize);

                var bg = UiUtil.MakeImage(cardGo, "Bg", cardSprite, CardBg, Image.Type.Sliced, true);
                Stretch(bg.transform);

                // 사진 — 카드 라운드에 맞춰 잘라낸다 (ImagePickerSheet 와 동일 방식)
                if (e != null && e.Texture != null)
                {
                    var maskImg = UiUtil.MakeImage(cardGo, "Mask", cardSprite, Color.white, Image.Type.Sliced);
                    Stretch(maskImg.transform);
                    var mask = maskImg.gameObject.AddComponent<Mask>();
                    mask.showMaskGraphic = false;

                    var thumb = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage));
                    thumb.transform.SetParent(maskImg.transform, false);
                    Stretch(thumb.transform);
                    var raw = thumb.GetComponent<RawImage>();
                    raw.raycastTarget = false;
                    UiUtil.FitCover(raw, e.Texture);
                }
                else
                {
                    var ph = UiUtil.MakeText(cardGo.transform, "Photo", DesignTokens.Type.Body2,
                                             PhotoText, TextAlignmentOptions.Center);
                    ph.text = "사진";
                    Stretch(ph.transform);
                }

                // 카드 테두리는 그리지 않는다 — 시안 카드 path 는 fill=url(#pattern) 뿐이고 stroke 가
                // 없다(최종 리뷰 I5). cardStrokeSprite 는 더 안 쓴다.

                // 체크 — 미선택은 흰 원 + 회색 테두리, 선택은 보라 원 + 보라(진) 테두리 + 순번 (시안)
                var ring = UiUtil.MakeImage(cardGo, "CheckRing", circleSprite, Color.white);
                UiUtil.Place(ring, checkInset, checkInset, checkSize, checkSize);
                var ringLine = UiUtil.MakeImage(cardGo, "CheckRingLine", circleStrokeSprite, CheckLine);
                UiUtil.Place(ringLine, checkInset, checkInset, checkSize, checkSize);
                var chk = UiUtil.MakeImage(cardGo, "Check", circleSprite, SelFill);
                UiUtil.Place(chk, checkInset, checkInset, checkSize, checkSize);
                var chkLine = UiUtil.MakeImage(cardGo, "CheckLine", circleStrokeSprite, SelLine);
                UiUtil.Place(chkLine, checkInset, checkInset, checkSize, checkSize);
                // Alert1 = 12 SemiBold — 시안 획두께/폰트크기 비율 0.128(SemiBold~Bold)과 크기(잉크 높이
                // 8.48~8.72 → 12px) 둘 다 맞는 기성 토큰이다(최종 리뷰 I8 — Body2+수동 12px 덮어쓰기는
                // 굵기가 Medium 으로 남는 버그였다).
                var num = UiUtil.MakeText(chk.transform, "No", DesignTokens.Type.Alert1,
                                          Color.white, TextAlignmentOptions.Center);
                Stretch(num.transform);

                int k = i;
                var hit = cardGo.gameObject.AddComponent<Button>();
                hit.targetGraphic = bg;
                hit.transition = Selectable.Transition.None;
                hit.onClick.AddListener(() => Toggle(k));

                cards.Add(new Card { entry = e, ring = ring, ringLine = ringLine,
                                     check = chk, checkLine = chkLine, number = num });
            }

            int rows = Mathf.CeilToInt(n / (float)columns);
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(0f, rows * pitchY - gapY));
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }

        static void Stretch(Transform t)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        void Toggle(int i)
        {
            if (picked.Contains(i)) picked.Remove(i);
            else if (picked.Count < MaxPhotos) picked.Add(i);   // 최대 5장 — 그 이상은 탭을 그냥 무시
            Refresh();
        }

        void Refresh()
        {
            if (titleLabel != null) titleLabel.text = $"사진 선택 ({picked.Count}/{MaxPhotos})";

            bool canAttach = picked.Count > 0;
            // Text.Point — CommunityWriteController 의 등록 라벨과 같은 컨벤션(Default 모드에서
            // Action.Primary 와 값이 같다).
            if (attachLabel != null) attachLabel.color = canAttach ? DesignTokens.Text.Point : DesignTokens.Text.Tertiary;
            if (btnAttach != null) btnAttach.interactable = canAttach;

            for (int i = 0; i < cards.Count; i++)
            {
                int order = picked.IndexOf(i);
                bool on = order >= 0;
                var c = cards[i];
                if (c.ring != null)      c.ring.gameObject.SetActive(!on);
                if (c.ringLine != null)  c.ringLine.gameObject.SetActive(!on);
                if (c.check != null)     c.check.gameObject.SetActive(on);
                if (c.checkLine != null) c.checkLine.gameObject.SetActive(on);
                if (c.number != null)    c.number.text = on ? (order + 1).ToString() : "";
            }
        }

        void OnAttach()
        {
            if (picked.Count == 0) return;   // 버튼이 비활성이라 정상 경로로는 안 옴 — 방어적 가드

            var sprites = new List<Sprite>();
            foreach (int i in picked)
            {
                if (i >= cards.Count || cards[i].entry == null || cards[i].entry.Texture == null) continue;
                var tex = cards[i].entry.Texture;
                sprites.Add(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)));
            }

            if (sprites.Count == 0)
                Debug.Log("[CommunityPhoto] 고른 카드에 사진이 없다 — 캡처가 아직 없는 플레이스홀더");

            if (writeController != null) writeController.Attach(sprites);
            else Debug.LogError("[CommunityPhoto] writeController 가 비어 있다");

            Close();
        }
    }
}
