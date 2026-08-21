using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace ARZone
{
    // 내가 쓴 글 / 댓글 단 글 (시안 '내가 쓴 글.svg' · '댓글 단 글.svg', Task4). 헤더(< + 제목) +
    // 커뮤니티 글 카드 목록. 칩·검색·정렬 없음, 카드 자체에도 ⋮ 가 없다(3x PNG·SVG 대조 —
    // task-4-report.md 참고). 화면 두 개가 이 컨트롤러 하나를 mode 로만 갈라 쓴다(중복 스크립트 방지).
    //
    // 카드는 CommunityController 와 CommunityCardBuilder.Build() 를 공유한다(복붙 아님) —
    // 카드 크기(373×223)·내부 배치는 커뮤니티 카드와 완전히 같지만 세로 피치만 239(간격16, 커뮤니티는
    // 243/간격20)다 — 시안 badge rect y (내가 쓴 글: 146.5/385.5/624.5/863.5, 댓글 단 글: 146.5/385.5)
    // 로 두 화면 다 정확히 확인(간격 정확히 239.0, 오차 없음). ⋮ 가 없어 제목 폭도 340(body 와 같은 폭,
    // 커뮤니티는 ⋮ 자리를 비우느라 310) — 실측으로 확정하지 못해 판단한 값이라 report 에 명시했다.
    //
    // MyPosts = post.mine. MyComments = post.replies 중 author=="나" 인 댓글이 있는 글
    // (MockCommunitySource.AddComment 가 내 댓글 author 를 "나" 로만 채우는 관례 — MyController.HasMyComment
    // 와 동일 기준, Task1).
    public class MyPostListController : MonoBehaviour
    {
        public enum FilterMode { MyPosts, MyComments }

        [Header("모드")]
        [SerializeField] FilterMode mode = FilterMode.MyPosts;

        [Header("구성 (CommunityController 와 같은 카드 자산을 공유)")]
        [SerializeField] RectTransform postList;
        [SerializeField] RectTransform scrollContent;   // Content — 카드 수만큼 스크롤 길이를 늘린다
        [SerializeField] TMP_Text fontSource;
        [SerializeField] Sprite cardSprite;        // bg_round16 — 카드 배경(불투명, 그라디언트 아님)
        [SerializeField] Sprite badgeSprite;       // bg_round8  — 게시판 뱃지 배경
        [SerializeField] Sprite heartSprite;       // ic_heart.svg — 좋아요 누른 상태
        [SerializeField] Sprite heartLineSprite;   // ic_heart_line.png — 기본 상태
        [SerializeField] Sprite commentSprite;     // ic_comment.png

        [Header("배치 (시안 실측 — 카드 크기는 커뮤니티와 같고 피치·제목폭만 다르다)")]
        [SerializeField] float cardW = 373f;
        [SerializeField] float cardH = 223f;
        [SerializeField] float pitchY = 239f;   // cardH + 간격16 (커뮤니티는 243=cardH+20)
        [SerializeField] float titleW = 340f;   // ⋮ 없어서 body 와 같은 폭 (커뮤니티는 310)

        [Header("스크롤")]
        [SerializeField] float contentPad = 130f;

        const string MyCommentAuthor = "나";

        TextMeshProUGUI ruler;
        readonly List<CommunityPost> filtered = new List<CommunityPost>();

        void OnEnable()
        {
            var store = CommunityStore.Instance;
            if (store != null)
            {
                store.OnChanged += Build;
                Build();
            }
            else
            {
                // Unity 는 오브젝트 간 Awake 순서를 보장하지 않는다 — CommunityController/MyController 와
                // 같은 가드(CommunityStore 는 보통 이 화면이 열리기 전에 이미 Awake 가 끝나 있다).
                Debug.LogError("[MyPostList] OnEnable 시점에 CommunityStore.Instance 가 없다 — 목록을 못 그림");
            }
        }

        void OnDisable()
        {
            if (CommunityStore.Instance != null) CommunityStore.Instance.OnChanged -= Build;
        }

        void Build()
        {
            if (postList == null) { Debug.LogError("[MyPostList] postList 가 비어 있다"); return; }
            var store = CommunityStore.Instance;
            if (store == null) { Debug.LogError("[MyPostList] Build: Store 없음"); return; }

            // store.Posts 는 커뮤니티 화면의 칩·검색으로 걸러진 캐시다 — 마이 화면은 전량에서
            // mine/댓글 기준으로 직접 걸러야 하므로 LoadAll 로 따로 받는다(C1, 최종 리뷰).
            store.LoadAll(all =>
            {
                filtered.Clear();
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        var p = all[i];
                        bool include = mode == FilterMode.MyPosts ? p.mine : HasMyComment(p);
                        if (include) filtered.Add(p);
                    }
                }

                var sprites = new CommunityCardBuilder.Sprites
                {
                    card = cardSprite, badge = badgeSprite, heart = heartSprite,
                    heartLine = heartLineSprite, comment = commentSprite, more = null,
                };
                // showMore=false — 이 화면 카드엔 ⋮ 가 없다(onMoreClick 콜백도 그래서 null).
                float listH = CommunityCardBuilder.Build(postList, filtered, fontSource, sprites,
                    cardW, cardH, pitchY, false, titleW, ref ruler, transform, null);

                Debug.Log($"[MyPostList] {mode} 카드 {filtered.Count}장 생성 (postList={postList.name})");

                if (scrollContent != null)
                {
                    float top = -postList.anchoredPosition.y;
                    var s2 = scrollContent.sizeDelta;
                    scrollContent.sizeDelta = new Vector2(s2.x, top + listH + contentPad);
                }
            });
        }

        static bool HasMyComment(CommunityPost p)
        {
            if (p.replies == null) return false;
            for (int i = 0; i < p.replies.Count; i++)
                if (p.replies[i].author == MyCommentAuthor) return true;
            return false;
        }
    }
}
