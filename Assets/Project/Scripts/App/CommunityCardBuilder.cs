using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 커뮤니티 글 카드 한 장을 만드는 로직 — CommunityController.Build() 안에 있던 for 루프를
    // 그대로(한 글자도 안 바꾸고) 옮겨온 것이다(Task 4). CommunityController 와
    // MyPostListController(내가 쓴 글·댓글 단 글, Task4)가 이 클래스를 공유한다 — 카드를 복붙하지
    // 않기 위해서다(Task4 브리프 "복붙하지 말고 공유하는 형태로").
    //
    // CommunityController 쪽은 동작이 전혀 바뀌지 않았다 — Build() 가 하던 걸 그대로 여기로 옮기고
    // Build() 는 이 메서드를 호출하도록만 바꿨다(순수 이동, 리뷰 끝난 커뮤니티 화면 검증 근거는
    // task-4-report.md 의 "카드 로직을 어떻게 공유했는지" 절 참고).
    //
    // showMore=false(내가 쓴 글·댓글 단 글) 일 땐 ⋮ 버튼 자체를 만들지 않는다 — 시안(sian84)에 카드
    // ⋮ 가 없다(3x PNG 대조, report 참고). titleW 도 그만큼 넓어진다(⋮ 자리를 안 비워도 되므로
    // 커뮤니티 310 → 340, body 와 같은 폭).
    public static class CommunityCardBuilder
    {
        public const float IconSize = 18f;
        public const float GapAfterIcon = 5.6f;   // 아이콘 오른쪽 끝 → 숫자 시작
        public const float GapGroup = 19f;        // 하트 숫자 끝 → 댓글 아이콘 시작

        // 시안 실측: 카드는 평평한 #0F0D17 α0.5 (그라디언트 없음 — 3x PNG 4모서리 8~11 로 균일).
        // #050505 위 sRGB 합성 = 0.5×(15,13,23) + 0.5×(5,5,5) = (10,9,14) → 불투명으로 칠한다.
        public static readonly Color CardBg = new Color32(0x0A, 0x09, 0x0E, 0xFF);

        public struct Sprites
        {
            public Sprite card;        // bg_round16 — 카드 배경(유리 아님, 불투명)
            public Sprite badge;       // bg_round8  — 게시판 뱃지 배경
            public Sprite heart;       // ic_heart.svg — 좋아요 누른 상태
            public Sprite heartLine;   // ic_heart_line.png — 기본 상태
            public Sprite comment;     // ic_comment.png
            public Sprite more;        // ic_more_vert.svg — showMore=false 면 안 씀(null 가능)
        }

        // 카드를 postList 밑에 채우고 postList.sizeDelta 를 갱신한 뒤, 리스트 전체 높이를 돌려준다
        // (호출부가 scrollContent.sizeDelta 를 마저 계산한다 — CommunityController/MyPostListController
        // 둘 다 이 값 + contentPad 로 스크롤 길이를 정하는 동일 패턴이라 여기서 안 건드린다).
        //
        // ruler: 폭을 재는 화면 밖 텍스트(MeasureWidth). 호출부가 자기 필드를 ref 로 넘겨 계속 재사용한다
        // (CommunityController/CommunityPostController 의 기존 관례와 동일 — 인스턴스마다 하나씩).
        public static float Build(
            RectTransform postList, List<CommunityPost> posts, TMP_Text fontSource, Sprites sp,
            float cardW, float cardH, float pitchY, bool showMore, float titleW,
            ref TextMeshProUGUI ruler, Transform rulerParent,
            Action<string, bool, RectTransform> onMoreClick)
        {
            UiUtil.ClearChildren(postList);
            int n = posts == null ? 0 : posts.Count;

            for (int i = 0; i < n; i++)
            {
                var p = posts[i];
                var card = UiUtil.MakeNode(postList, "Post" + i);
                UiUtil.Place(card, 0f, i * pitchY, cardW, cardH);

                var bg = UiUtil.MakeImage(card, "Bg", sp.card, CardBg, Image.Type.Sliced, true);
                GlassEdgeGlow.Attach(card.transform, GlassGlowBoot.Round16);   // 빛나는 부분 테두리
                var brt = (RectTransform)bg.transform;
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

                // 게시판 뱃지
                var badgeBg = UiUtil.MakeImage(card.transform, "BadgeBg", sp.badge, DesignTokens.Stroke.Regular, Image.Type.Sliced);
                UiUtil.Place(badgeBg, 16.5f, 24.5f, 72f, 25f);

                var badgeLabel = UiUtil.MakeText(card.transform, "BadgeLabel", DesignTokens.Type.Caption1,
                                                 DesignTokens.Text.Secondary, TextAlignmentOptions.Center);
                badgeLabel.text = p.board;
                UiUtil.Place(badgeLabel, 16.5f, 24.5f, 72f, 25f);
                CopyFont(fontSource, badgeLabel);

                // ⋮ → Popup_PostMenu. 목록 카드에만 있다(showMore=false 인 화면엔 아예 안 만든다).
                if (showMore)
                {
                    var more = UiUtil.MakeImage(card.transform, "BtnMore", sp.more, DesignTokens.Text.Primary);
                    UiUtil.Place(more, 335f, 25f, 24f, 24f);
                    more.raycastTarget = true;
                    var moreBtn = more.gameObject.AddComponent<Button>();
                    moreBtn.targetGraphic = more;
                    moreBtn.transition = Selectable.Transition.None;
                    string moreId = p.id;
                    bool moreMine = p.mine;
                    var moreRect = (RectTransform)more.transform;
                    moreBtn.onClick.AddListener(() => onMoreClick?.Invoke(moreId, moreMine, moreRect));
                }

                // 제목 (1줄 말줄임) — titleW: ⋮ 있으면 310(커뮤니티), 없으면 340(body 와 같은 폭)
                var title = UiUtil.MakeText(card.transform, "Title", DesignTokens.Type.Title1,
                                            DesignTokens.Text.Primary, TextAlignmentOptions.TopLeft);
                title.text = p.title;
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.overflowMode = TextOverflowModes.Ellipsis;
                UiUtil.Place(title, 16.5f, 69.5f, titleW, 24f);
                CopyFont(fontSource, title);

                // 본문 (2줄 말줄임)
                var body = UiUtil.MakeText(card.transform, "Body", DesignTokens.Type.Body4,
                                           DesignTokens.Text.Secondary, TextAlignmentOptions.TopLeft);
                body.text = p.body;
                body.overflowMode = TextOverflowModes.Ellipsis;
                body.maxVisibleLines = 2;
                UiUtil.Place(body, 16.5f, 100.9f, 340f, 40f);
                CopyFont(fontSource, body);

                // 작성자 · 시간
                var meta = UiUtil.MakeText(card.transform, "Meta", DesignTokens.Type.Caption1,
                                           DesignTokens.Text.Tertiary, TextAlignmentOptions.TopLeft);
                // 내 글은 저장 시점 author 가 아니라 현재 프로필 닉네임을 보여준다 —
                // 프로필을 바꾸면 옛 글에도 즉시 반영 (2026-08-31 "커뮤니티 프로필 연동 안 됨")
                meta.text = (p.mine ? ProfileStore.Nickname : p.author) + " · " + p.timeText;
                meta.textWrappingMode = TextWrappingModes.NoWrap;
                meta.overflowMode = TextOverflowModes.Ellipsis;
                UiUtil.Place(meta, 16.5f, 148.74f, 310f, 19f);
                CopyFont(fontSource, meta);

                // 하트 + 숫자
                var heartIcon = UiUtil.MakeImage(card.transform, "IconHeart",
                                                 p.liked ? sp.heart : sp.heartLine, Color.white);
                UiUtil.Place(heartIcon, 17.07f, 180f, IconSize, IconSize);

                var heartCount = UiUtil.MakeText(card.transform, "LabelLike", DesignTokens.Type.Caption1,
                                                 DesignTokens.Text.Primary, TextAlignmentOptions.MidlineLeft);
                heartCount.text = p.likes.ToString();
                heartCount.fontSize = 12f;
                heartCount.textWrappingMode = TextWrappingModes.NoWrap;
                float heartX = 17.07f + IconSize + GapAfterIcon;
                UiUtil.Place(heartCount, heartX, 180f, 80f, IconSize);
                CopyFont(fontSource, heartCount);
                float heartCountW = MeasureWidth(heartCount.text, heartCount, ref ruler, rulerParent);

                // 말풍선 + 숫자 (하트 숫자 폭만큼 뒤로 밀린다)
                float commentX = heartX + heartCountW + GapGroup;
                var commentIcon = UiUtil.MakeImage(card.transform, "IconComment", sp.comment, Color.white);
                UiUtil.Place(commentIcon, commentX, 180f, IconSize, IconSize);

                var commentCount = UiUtil.MakeText(card.transform, "LabelComment", DesignTokens.Type.Caption1,
                                                   DesignTokens.Text.Primary, TextAlignmentOptions.MidlineLeft);
                commentCount.text = p.comments.ToString();
                commentCount.fontSize = 12f;
                commentCount.textWrappingMode = TextWrappingModes.NoWrap;
                UiUtil.Place(commentCount, commentX + IconSize + GapAfterIcon, 180f, 80f, IconSize);
                CopyFont(fontSource, commentCount);

                // 카드 클릭 → 상세 (CommunityPostController.Target 설정 후 ScreenManager 로 이동)
                var btn = bg.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.None;
                string id = p.id;
                btn.onClick.AddListener(() => OnCardClick(id));
            }

            float listH = n > 0 ? (n * pitchY) - (pitchY - cardH) : 0f;
            postList.sizeDelta = new Vector2(postList.sizeDelta.x, listH);
            return listH;
        }

        public static void OnCardClick(string id)
        {
            CommunityPostController.Target = id;
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show("CommunityPost");
            else Debug.LogError("[CommunityCardBuilder] 카드 클릭: ScreenManager.Instance 가 없다");
        }

        // 줄바꿈을 끄고 잰 텍스트 폭. GetPreferredValues(s, 0, 0) 은 폭 0 으로 줄바꿈시켜 글자 몇 개
        // 폭만 돌려주는 함정이 있어(TalkSearchController.MeasureWidth 주석 참고) 화면 밖 ruler 텍스트로 잰다.
        static float MeasureWidth(string s, TMP_Text like, ref TextMeshProUGUI ruler, Transform rulerParent)
        {
            if (ruler == null)
            {
                ruler = UiUtil.MakeText(rulerParent, "__ruler", DesignTokens.Type.Caption1, new Color(0f, 0f, 0f, 0f));
                ruler.textWrappingMode = TextWrappingModes.NoWrap;
                ruler.overflowMode = TextOverflowModes.Overflow;
                UiUtil.Place(ruler, 0f, 4000f, 4000f, 100f);   // 화면 밖
            }
            ruler.font = like.font;
            ruler.fontSharedMaterial = like.fontSharedMaterial;
            ruler.fontSize = like.fontSize;
            ruler.fontWeight = like.fontWeight;
            ruler.text = s;
            ruler.ForceMeshUpdate();
            return ruler.preferredWidth;
        }

        static void CopyFont(TMP_Text fontSource, TMP_Text t)
        {
            if (fontSource == null || fontSource.font == null || t == null) return;
            t.font = fontSource.font;
            t.fontSharedMaterial = fontSource.fontSharedMaterial;
        }
    }
}
