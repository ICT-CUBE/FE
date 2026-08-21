using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 커뮤니티 글 상세 (시안 '커뮤니티 글 상세.svg'). 목록(CommunityController.OnCardClick)이
    // Target 에 글 id 를 넣고 ScreenManager.Show("CommunityPost") 로 이 화면을 연다.
    //
    // 실측(Content 기준 절대좌표 — p_community_post_screen.py 와 report 의 대조표 참고):
    //   헤더 BtnBack(19,67)24x24 · NavTitle(0,64.61) stretch-40 center Headline3(18 SemiBold)
    //   작성자 AuthorRing(19.5,121.5)45x45 · AuthorIcon(20,122)44x44 · LabelAuthor(80.61,127.14) 15 SemiBold ·
    //          LabelTime(80.94,151.74) 12 Regular #727886 · BtnMore(370,132)24x24 장식만(Task10)
    //   제목·본문은 줄바꿈(전문 표시, 말줄임 없음)이라 실제 줄 수만큼 높이가 늘어난다 — 그 밑의
    //   사진/구분선/좋아요줄이 전부 그만큼 밀려 내려가야 해서 Refresh() 가 매번 다시 배치한다.
    //   제목 Title1(18 Bold) 흰색 · 본문 Body4(14 Regular) 흰색(목록 카드는 Secondary 회색인데
    //   상세는 흰색 — 3x PNG fill=white 로 확인, 카드와 다른 게 맞다).
    //   사진 없으면 PhotoScroll 을 끈다(지금 시드 3개 전부 없음 — 자리는 시안대로 만들어 두고 끈다).
    //   좋아요/댓글: 카운트 0 이면 시안이 "좋아요"/"댓글" 단어(15 SemiBold), 1 이상이면 숫자만
    //   (14, SemiBold) — 메인 시안(seed1, 0/0)과 '...댓글' 시안(seed3, 2111/34) 대조로 확인한 두 상태.
    //
    // Task 7 — 댓글 목록 + 입력바 (시안 '커뮤니티 글 상세 댓글.svg'):
    //   댓글 행(아바타36×36+작성자·시간 한 줄+본문)은 씬에 안 두고 BuildCommentSection/MakeCommentRow 가
    //   코드로 쌓는다(사진 타일과 같은 방식). 댓글이 없으면 "아직 댓글이 없습니다."(시안 '...글 상세.svg').
    //   입력바(CommentInput)는 두 시안에서 y 가 다르지만(779.5 vs 813.5) '댓글' 시안 쪽 배경 스트립만
    //   화면 맨 아래(893)에 정확히 닿아서(오프셋 0) 그쪽을 기준으로 스크롤 밖(Screen 루트 직계)에 고정했다
    //   — 자세한 근거는 task-7-report.md.
    public class CommunityPostController : MonoBehaviour
    {
        // 목록이 여기에 글 id 를 넣고 이 화면을 연다.
        public static string Target;

        [Header("헤더")]
        [SerializeField] RectTransform content;      // PostScroll/Viewport/Content — 스크롤 길이 갱신용
        [SerializeField] TMP_Text navTitle;
        [SerializeField] Image authorIcon;
        [SerializeField] TMP_Text labelAuthor;
        [SerializeField] TMP_Text labelTime;
        [SerializeField] Image btnMore;               // ⋮ → Popup_PostMenu(Task10)

        [Header("본문")]
        [SerializeField] TMP_Text labelPostTitle;
        [SerializeField] TMP_Text labelPostBody;

        [Header("사진")]
        [SerializeField] RectTransform photoScroll;   // PhotoScroll 루트 — 사진 없으면 통째로 끈다
        [SerializeField] RectTransform photoContent;  // PhotoScroll/Viewport/Content — 타일을 채운다
        [SerializeField] Sprite photoTileMask;         // bg_round8 — GalleryController 와 같은 라운드 마스크 패턴

        [Header("구분선 · 좋아요 · 댓글")]
        [SerializeField] RectTransform divider;
        [SerializeField] Image btnLike;               // 하트 아이콘 자체가 버튼(브리프 네이밍)
        [SerializeField] TMP_Text labelLike;
        [SerializeField] RectTransform btnComment;     // 댓글 아이콘(장식 — 목록/입력은 Task 7)
        [SerializeField] TMP_Text labelComment;
        [SerializeField] Sprite heartSprite;           // ic_heart.svg — 채워진 보라(liked=true)
        [SerializeField] Sprite heartLineSprite;       // ic_heart_line.png — 기본 외곽선(liked=false)

        [Header("댓글 목록 (Task 7)")]
        [SerializeField] RectTransform divider2;       // 좋아요줄 밑, "댓글" 헤더 위의 굵은(2px) 구분선
        [SerializeField] TMP_Text labelCommentHeader;  // "댓글"
        [SerializeField] RectTransform commentList;    // 댓글 행을 코드로 채우는 컨테이너 (목록 카드·사진 타일과 같은 방식)
        [SerializeField] TMP_Text labelEmptyComments;  // "아직 댓글이 없습니다." (시안 '커뮤니티 글 상세.svg')
        [SerializeField] Sprite[] myAvatarPool;        // 남성/여성/동물 순(AvatarNameBinder.profiles 와 동일 순서) —
                                                        // AddComment 가 만든 내 댓글은 authorIcon 이 비어 있어 대신 쓴다

        [Header("댓글 입력 (Task 7)")]
        [SerializeField] RectTransform commentInputBar;   // TMP_InputField 를 여기 붙인다(TalkDetailController.EnsureInput 과 동일 패턴)
        [SerializeField] TMP_Text commentHint;             // "댓글을 입력하세요" — placeholder 로 재활용
        [SerializeField] Image btnSend;                     // 전송 아이콘 자체가 버튼(BtnLike 와 같은 패턴)

        [Header("배치 (댓글, 시안 실측)")]
        [SerializeField] float gapRowDivider2 = 24f;       // 좋아요줄 아이콘 밑 → 구분선2 (rowY 기준, Task6 의 rowY=442 로 역산해 24 — gapBodyPhotos 와 동일값)
        [SerializeField] float divider2H = 2f;             // 구분선1(1px)보다 굵다 — 시안 실측
        [SerializeField] float gapDivider2Header = 23.27f; // 구분선2 → "댓글" 헤더 (두 시안 모두 동일하게 측정됨)
        [SerializeField] float headerH = 20f;
        [SerializeField] float gapHeaderContent = 23.5f;   // 헤더 → 첫 콘텐츠(빈 문구 or 첫 댓글 행)
        [SerializeField] float emptyLabelH = 20f;
        [SerializeField] float avatarSize = 36f;           // 포스트 헤더(44)보다 작다
        [SerializeField] float commentIndent = 48.56f;     // marginL 로부터 작성자/시간/본문 시작 x (아바타36 + 간격12.56)
        [SerializeField] float gapAvatarAuthor = 5.14f;    // 포스트 헤더(AuthorIcon→LabelAuthor)와 동일값 재사용
        [SerializeField] float authorTimeGap = 11.5f;      // 작성자 뒤 → 시간 시작(댓글 행은 한 줄에 나란히, 포스트 헤더와 다른 레이아웃)
        [SerializeField] float gapAvatarBody = 38.38f;     // 아바타 top → 본문 top
        [SerializeField] float commentRowGap = 56.7f;      // 행 간(본문 바닥→다음 아바타 top). 시안 실측:
                                                            // avatar1=595, body1 top=633.38(=595+gapAvatarBody38.38),
                                                            // body1 h=79.92 → body1 bottom=713.30, avatar2=770.00
                                                            // → 770.00-713.30=56.70. (구값 20.7 은 MakeCommentRow 의
                                                            // 덧셈 체인에 없는 -36(아바타 높이) 항이 섞여 역산된 오류값 —
                                                            // 리뷰로 발견, Task7 산출물에서 수정))

        [Header("배치 (시안 실측)")]
        [SerializeField] float contentW = 372f;
        [SerializeField] float marginL = 20f;
        [SerializeField] float titleY = 204.90f;
        [SerializeField] float gapTitleBody = 23.37f;
        [SerializeField] float gapBodyPhotos = 24f;    // 사진 있을 때, 그리고 없을 때(자리 접힘)도 재사용
        [SerializeField] float photoH = 90f;
        [SerializeField] float photoGap = 8f;
        [SerializeField] float gapPhotosDivider = 16f;
        [SerializeField] float gapDividerRow = 19.5f;
        [SerializeField] float bottomPad = 40f;

        const float IconSize = 18f;
        const float GapAfterIcon = 5.6f;   // 목록 카드(CommunityController)와 같은 상수 재사용
        const float GapGroup = 19f;

        Button btnLikeButton;
        Button btnSendButton;
        Button btnMoreButton;

        // ⋮ 팝업 위치 (시안 '커뮤니티 글 상세.svg' ⋮ 팝업 열린 상태 실측): 팝업 top-left 는 BtnMore
        // top-left 로부터 (-110, +12) — 목록 카드 쪽 오프셋(-121,+28)과 다르다(화면마다 실측이 다름 —
        // task-10-report.md 대조표 참고).
        static readonly Vector2 MorePopupOffset = new Vector2(-110f, 12f);
        TMP_InputField commentInput;   // EnsureInput() 이 commentInputBar 에 붙인다 (TalkDetailController.EnsureInput 패턴)
        TextMeshProUGUI ruler;   // 목록과 동일한 패턴 — 화면 밖 텍스트로 폭을 잰다

        void Awake()
        {
            if (btnLike != null)
            {
                btnLikeButton = btnLike.GetComponent<Button>();
                if (btnLikeButton == null) btnLikeButton = btnLike.gameObject.AddComponent<Button>();
                btnLikeButton.targetGraphic = btnLike;
                btnLikeButton.transition = Selectable.Transition.None;
                btnLikeButton.onClick.AddListener(OnLikeClick);
            }
            if (btnSend != null)
            {
                btnSendButton = btnSend.GetComponent<Button>();
                if (btnSendButton == null) btnSendButton = btnSend.gameObject.AddComponent<Button>();
                btnSendButton.targetGraphic = btnSend;
                btnSendButton.transition = Selectable.Transition.None;
                btnSendButton.onClick.AddListener(OnSendComment);
            }
            if (btnMore != null)
            {
                btnMore.raycastTarget = true;   // 씬에 장식만으로 있던 Image — 눌리게 켠다(함정)
                btnMoreButton = btnMore.GetComponent<Button>();
                if (btnMoreButton == null) btnMoreButton = btnMore.gameObject.AddComponent<Button>();
                btnMoreButton.targetGraphic = btnMore;
                btnMoreButton.transition = Selectable.Transition.None;
                btnMoreButton.onClick.AddListener(OnMoreClick);
            }
        }

        void OnEnable()
        {
            EnsureInput();
            var store = CommunityStore.Instance;
            if (store != null)
            {
                store.OnChanged += Refresh;
                Refresh();
            }
            else
            {
                // Unity 는 오브젝트 간 Awake 순서를 보장하지 않는다 (CommunityController 와 동일 가드).
                Debug.LogError("[CommunityPost] OnEnable 시점에 CommunityStore.Instance 가 없다 — 글을 못 그림");
            }
        }

        void OnDisable()
        {
            if (CommunityStore.Instance != null) CommunityStore.Instance.OnChanged -= Refresh;
        }

        void OnLikeClick()
        {
            if (string.IsNullOrEmpty(Target)) return;
            var store = CommunityStore.Instance;
            if (store == null) { Debug.LogError("[CommunityPost] OnLikeClick: Store 없음"); return; }
            store.ToggleLike(Target);   // ToggleLike 가 OnChanged 를 쏴서 Refresh() 가 다시 그린다
        }

        void OnMoreClick()
        {
            if (string.IsNullOrEmpty(Target)) return;
            var store = CommunityStore.Instance;
            if (store == null) { Debug.LogError("[CommunityPost] OnMoreClick: Store 없음"); return; }
            var p = store.Find(Target);
            if (p == null) { Debug.LogError($"[CommunityPost] OnMoreClick: id='{Target}' 글을 못 찾음"); return; }
            if (PostMenuPopup.Instance == null) { Debug.LogError("[CommunityPost] OnMoreClick: PostMenuPopup.Instance 없음"); return; }
            Vector2 anchor = PostMenuPopup.CanvasTopLeft((RectTransform)btnMore.transform) + MorePopupOffset;
            PostMenuPopup.Instance.Open(Target, p.mine, anchor);
        }

        void Refresh()
        {
            var store = CommunityStore.Instance;
            if (store == null) { Debug.LogError("[CommunityPost] Refresh: Store 없음"); return; }
            if (string.IsNullOrEmpty(Target)) { Debug.LogError("[CommunityPost] Refresh: Target 이 비어 있다"); return; }
            var p = store.Find(Target);
            if (p == null)
            {
                // 지금 상세로 열려 있던 글이 방금 삭제됐다(PostMenuPopup.OnConfirmDelete → Delete →
                // Load → OnChanged → 여기 Refresh). 옛 내용을 그대로 띄워둔 채 갇히는 대신 목록으로
                // 돌려보낸다 — 목록에서 지울 때는 이 화면이 열려 있지 않아 원래도 문제가 없었다
                // (최종 리뷰 I1). OnEnable/OnDisable 이 OnChanged 구독을 관리하므로 이 Refresh 는
                // 이 화면이 실제로 열려 있을 때만 불린다.
                if (ScreenManager.Instance != null) ScreenManager.Instance.Show("Community");
                else Debug.LogError($"[CommunityPost] id='{Target}' 글을 못 찾음 — ScreenManager 도 없어 목록으로 못 돌아감");
                return;
            }

            if (navTitle != null) navTitle.text = p.board;
            if (authorIcon != null && p.authorIcon != null) authorIcon.sprite = p.authorIcon;
            if (labelAuthor != null) labelAuthor.text = p.author;
            if (labelTime != null) labelTime.text = p.timeText;

            // 제목 — 줄바꿈, 말줄임 없음(글 전문)
            float titleH = 24f;
            if (labelPostTitle != null)
            {
                labelPostTitle.textWrappingMode = TextWrappingModes.Normal;
                labelPostTitle.overflowMode = TextOverflowModes.Overflow;
                labelPostTitle.text = p.title;
                titleH = MeasureHeight(labelPostTitle, contentW);
                UiUtil.Place(labelPostTitle, marginL, titleY, contentW, titleH);
            }
            float bodyY = titleY + titleH + gapTitleBody;

            // 본문 — 줄바꿈, 말줄임 없음(글 전문)
            float bodyH = 40f;
            if (labelPostBody != null)
            {
                labelPostBody.textWrappingMode = TextWrappingModes.Normal;
                labelPostBody.overflowMode = TextOverflowModes.Overflow;
                labelPostBody.text = p.body;
                UiUtil.Place(labelPostBody, marginL, bodyY, contentW, 10f);
                bodyH = MeasureHeight(labelPostBody, contentW);
                UiUtil.Place(labelPostBody, marginL, bodyY, contentW, bodyH);
            }

            bool hasPhotos = p.photos != null && p.photos.Count > 0;
            float photoY = bodyY + bodyH + gapBodyPhotos;
            float dividerY;
            if (hasPhotos)
            {
                UiUtil.SetActive(photoScroll != null ? photoScroll.gameObject : null, true);
                if (photoScroll != null) UiUtil.Place(photoScroll, marginL, photoY, contentW, photoH);
                BuildPhotos(p);
                dividerY = photoY + photoH + gapPhotosDivider;
            }
            else
            {
                UiUtil.SetActive(photoScroll != null ? photoScroll.gameObject : null, false);
                dividerY = photoY;   // 사진 자리(90+16)를 통째로 접고 본문 밑 갭을 그대로 재사용
            }

            if (divider != null) UiUtil.Place(divider, marginL, dividerY, contentW, 1f);

            float rowY = dividerY + gapDividerRow;
            RefreshLikeRow(p, rowY);

            float commentsBottom = BuildCommentSection(p, rowY);

            if (content != null)
            {
                float bottom = commentsBottom + bottomPad;
                content.sizeDelta = new Vector2(content.sizeDelta.x, bottom);
            }
        }

        void RefreshLikeRow(CommunityPost p, float y)
        {
            if (btnLike != null)
            {
                UiUtil.Place(btnLike, marginL, y, IconSize, IconSize);
                btnLike.sprite = p.liked ? heartSprite : heartLineSprite;
            }

            float likeX = marginL + IconSize + GapAfterIcon;
            float likeW = 0f;
            if (labelLike != null)
            {
                bool likeIsCount = p.likes > 0;
                labelLike.text = likeIsCount ? p.likes.ToString() : "좋아요";
                labelLike.fontSize = likeIsCount ? 14f : 15f;
                labelLike.textWrappingMode = TextWrappingModes.NoWrap;
                UiUtil.Place(labelLike, likeX, y, 120f, IconSize);
                likeW = MeasureWidth(labelLike.text, labelLike);
            }

            float commentX = likeX + likeW + GapGroup;
            if (btnComment != null) UiUtil.Place(btnComment, commentX, y, IconSize, IconSize);

            if (labelComment != null)
            {
                bool commentIsCount = p.comments > 0;
                labelComment.text = commentIsCount ? p.comments.ToString() : "댓글";
                labelComment.fontSize = commentIsCount ? 14f : 15f;
                labelComment.textWrappingMode = TextWrappingModes.NoWrap;
                UiUtil.Place(labelComment, commentX + IconSize + GapAfterIcon, y, 120f, IconSize);
            }
        }

        // 좋아요줄 밑에 구분선2 · "댓글" 헤더 · (댓글 있으면 목록 / 없으면 빈 문구) 를 쌓는다.
        // 댓글 행 자체는 씬에 안 두고 여기서 코드로 만든다(목록 카드·사진 타일과 같은 방식).
        // 반환값 = 댓글 섹션이 끝나는 y (여기에 bottomPad 를 더해 content.sizeDelta 를 정한다).
        float BuildCommentSection(CommunityPost p, float rowY)
        {
            float divider2Y = rowY + IconSize + gapRowDivider2;
            if (divider2 != null) UiUtil.Place(divider2, marginL, divider2Y, contentW, divider2H);

            float headerY = divider2Y + divider2H + gapDivider2Header;
            if (labelCommentHeader != null) UiUtil.Place(labelCommentHeader, marginL, headerY, 120f, headerH);

            float listY = headerY + headerH + gapHeaderContent;
            var replies = p.replies;
            int n = replies == null ? 0 : replies.Count;

            if (n == 0)
            {
                UiUtil.SetActive(labelEmptyComments != null ? labelEmptyComments.gameObject : null, true);
                if (commentList != null)
                {
                    UiUtil.ClearChildren(commentList);
                    UiUtil.Place(commentList, marginL, listY, contentW, 0f);
                }
                if (labelEmptyComments != null) UiUtil.Place(labelEmptyComments, marginL, listY, contentW, emptyLabelH);
                return listY + emptyLabelH;
            }

            UiUtil.SetActive(labelEmptyComments != null ? labelEmptyComments.gameObject : null, false);
            if (commentList == null) return listY;

            UiUtil.ClearChildren(commentList);
            UiUtil.Place(commentList, marginL, listY, contentW, 10f);

            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                y = MakeCommentRow(replies[i], y);
                if (i < n - 1) y += commentRowGap;
            }
            commentList.sizeDelta = new Vector2(contentW, y);
            return listY + y;
        }

        // 댓글 행 하나(아바타 36×36 + 작성자·시간 한 줄 + 본문). 본문은 줄바꿈해서 실제 높이만큼 다음 행이 밀린다.
        float MakeCommentRow(CommunityComment c, float y)
        {
            var avatar = UiUtil.MakeImage(commentList, "Avatar", ResolveAvatar(c), Color.white);
            UiUtil.Place(avatar, 0f, y, avatarSize, avatarSize);

            var author = UiUtil.MakeText(commentList, "Author", new TypeStyle(15f, FontWeight.SemiBold, 140, -1f),
                                         DesignTokens.Text.Primary, TextAlignmentOptions.MidlineLeft);
            author.text = c.author;
            author.textWrappingMode = TextWrappingModes.NoWrap;
            author.overflowMode = TextOverflowModes.Overflow;
            UiUtil.Place(author, commentIndent, y + gapAvatarAuthor, 200f, 18f);
            float authorW = MeasureWidth(author.text, author);

            var time = UiUtil.MakeText(commentList, "Time", new TypeStyle(12f, FontWeight.Regular, 140, -0.5f),
                                       DesignTokens.Text.Tertiary, TextAlignmentOptions.MidlineLeft);
            time.text = c.timeText;
            time.textWrappingMode = TextWrappingModes.NoWrap;
            time.overflowMode = TextOverflowModes.Overflow;
            UiUtil.Place(time, commentIndent + authorW + authorTimeGap, y + gapAvatarAuthor, 150f, 18f);

            var body = UiUtil.MakeText(commentList, "Body", DesignTokens.Type.Body4,
                                       DesignTokens.Text.Primary, TextAlignmentOptions.TopLeft);
            body.text = c.body;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;
            float bodyW = contentW - commentIndent;
            UiUtil.Place(body, commentIndent, y + gapAvatarBody, bodyW, 10f);
            float bodyH = MeasureHeight(body, bodyW);
            UiUtil.Place(body, commentIndent, y + gapAvatarBody, bodyW, bodyH);

            return y + gapAvatarBody + bodyH;
        }

        // AddComment 가 만드는 내 댓글은 author="나" 뿐이고 authorIcon 이 비어 있다(MockCommunitySource, Task2).
        // 회원가입 때 고른 내 아바타(AvatarNameBinder.SelectedProfile — 남/여/동물 순, 브리프가 준 3종과 동일 순서)를
        // 대신 쓴다. 새 스프라이트를 만들지 않고 이미 있는 상태를 재사용하는 것이라 "지어내기" 가 아니다.
        Sprite ResolveAvatar(CommunityComment c)
        {
            if (c.authorIcon != null) return c.authorIcon;
            if (myAvatarPool == null || myAvatarPool.Length == 0) return null;
            int idx = AvatarNameBinder.SelectedProfile;
            if (idx < 0 || idx >= myAvatarPool.Length) idx = 0;
            return myAvatarPool[idx];
        }

        // 시안 입력바는 배경 + placeholder 뿐이라 글자를 칠 수 없다.
        // TalkDetailController.EnsureInput 과 같은 방식으로 코드에서 진짜 입력칸을 붙인다.
        void EnsureInput()
        {
            if (commentInput != null || commentInputBar == null) return;

            // mkui 로 만든 Image 는 raycastTarget 이 기본 0 이라 탭이 안 먹는다 — 켜야 한다.
            var bg = commentInputBar.GetComponent<Image>();
            if (bg != null) bg.raycastTarget = true;

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(commentInputBar, false);
            var art = area.GetComponent<RectTransform>();
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(17f, 0f);    // 시안 placeholder x37 - 입력바 x20 = 17
            art.offsetMax = new Vector2(-37f, 0f);   // 오른쪽은 전송 버튼 자리

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(area.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var tt = textGo.GetComponent<TextMeshProUGUI>();
            new TypeStyle(16f, FontWeight.Regular, 140, -1f).Apply(tt);
            tt.color = DesignTokens.Text.Primary;   // 입력된 값은 흰색(placeholder 만 회색 #727886)
            tt.alignment = TextAlignmentOptions.MidlineLeft;
            tt.richText = false;

            var field = commentInputBar.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = art;
            field.textComponent = tt;
            field.placeholder = commentHint;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 300;
            field.onSubmit.AddListener(_ => OnSendComment());
            commentInput = field;
        }

        void OnSendComment()
        {
            if (string.IsNullOrEmpty(Target)) return;
            if (commentInput == null && commentInputBar != null) commentInput = commentInputBar.GetComponent<TMP_InputField>();
            if (commentInput == null)
            {
                Debug.LogError("[CommunityPost] 댓글 입력 필드를 못 찾음 — commentInputBar 가 인스펙터에 물려 있는지 확인");
                return;
            }

            string text = commentInput.text;
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.Log("[CommunityPost] 댓글 입력이 비어 있어 등록하지 않음");
                return;
            }

            var store = CommunityStore.Instance;
            if (store == null) { Debug.LogError("[CommunityPost] OnSendComment: Store 없음"); return; }

            // AddComment 가 OnChanged 를 쏴서 Refresh() 가 다시 불려 목록·좋아요줄 댓글 수가 같이 갱신된다.
            store.AddComment(Target, text.Trim());
            commentInput.text = "";
            Toast.Show("댓글이 등록되었습니다.", ToastBottom);
        }

        // 시안 '커뮤니티 글 상세 댓글.svg' 에 등록 토스트가 떠 있는 상태가 그대로 있다 —
        // 박스 (20,723) 372×46 → 화면 바닥까지 893-723-46=124. 이 화면엔 CommentInput 이 고정으로
        // 깔려 있어 Toast 의 기본값(58)을 쓰면 입력바에 가린다(ARMediaController.ToastBottom=130 과
        // 같은 이유·비슷한 값 — "하단에 고정 UI 있는 화면" 관례).
        const float ToastBottom = 124f;

        // GalleryController.Build() 의 "타일에 Mask(라운드 스프라이트) 걸고 사진은 자식으로" 패턴 그대로.
        void BuildPhotos(CommunityPost p)
        {
            if (photoContent == null) return;
            UiUtil.ClearChildren(photoContent);
            int n = p.photos.Count;
            for (int i = 0; i < n; i++)
            {
                var tile = UiUtil.MakeImage(photoContent, "Photo" + i, photoTileMask, Color.white, Image.Type.Sliced, false);
                UiUtil.Place(tile, i * (photoH + photoGap), 0f, photoH, photoH);
                var mask = tile.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;

                var photoImg = UiUtil.MakeImage(tile.transform, "PhotoImg", p.photos[i], Color.white);
                var prt = (RectTransform)photoImg.transform;
                prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
                prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            }
            photoContent.sizeDelta = new Vector2(n * photoH + Mathf.Max(0, n - 1) * photoGap, photoH);
        }

        // 줄바꿈 켠 텍스트의 실제 렌더 높이(줄 수만큼). ForceMeshUpdate 전에 폭을 먼저 고정해야 한다.
        float MeasureHeight(TMP_Text t, float width)
        {
            var rt = (RectTransform)t.transform;
            rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
            t.ForceMeshUpdate();
            return t.preferredHeight;
        }

        // CommunityController.MeasureWidth 와 동일 패턴(화면 밖 ruler) — GetPreferredValues(s,0,0) 은
        // 폭 0 으로 줄바꿈시켜 글자 몇 개 폭만 돌려주는 함정이 있어 쓰지 않는다.
        float MeasureWidth(string s, TMP_Text like)
        {
            if (ruler == null)
            {
                ruler = UiUtil.MakeText(transform, "__ruler", DesignTokens.Type.Caption1, new Color(0f, 0f, 0f, 0f));
                ruler.textWrappingMode = TextWrappingModes.NoWrap;
                ruler.overflowMode = TextOverflowModes.Overflow;
                UiUtil.Place(ruler, 0f, 4000f, 4000f, 100f);
            }
            ruler.font = like.font;
            ruler.fontSharedMaterial = like.fontSharedMaterial;
            ruler.fontSize = like.fontSize;
            ruler.fontWeight = like.fontWeight;
            ruler.text = s;
            ruler.ForceMeshUpdate();
            return ruler.preferredWidth;
        }
    }
}
