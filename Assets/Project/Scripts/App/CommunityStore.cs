using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 현재 소스를 들고, 화면들이 같은 목록을 보게 한다.
    // 목록이 바뀌면 OnChanged 로 알려 목록·상세가 다시 그리게 한다.
    public class CommunityStore : MonoBehaviour
    {
        public enum SourceKind { Mock }   // Api 가 생기면 여기에 추가한다

        [SerializeField] SourceKind source = SourceKind.Mock;
        [SerializeField] CommunitySeed seed;

        public static CommunityStore Instance { get; private set; }

        public event Action OnChanged;

        ICommunitySource src;
        readonly List<CommunityPost> cache = new List<CommunityPost>();

        public List<CommunityPost> Posts => cache;
        public string Board { get; private set; } = "전체";
        public string Query { get; private set; } = "";

        void Awake()
        {
            Instance = this;
            // 시드가 통째로 안 읽히면 커뮤니티·마이가 전부 0 이 된다. 어디서 끊겼는지 바로 보이게 남긴다.
            if (seed == null)
                Debug.LogError("[Community] seed 가 null — ScreenManager 인스펙터에 CommunitySeed 를 넣을 것");
            else
                Debug.Log($"[Community] 시드 {seed.posts.Count}글 로드 " +
                          $"(내 글 {seed.posts.FindAll(p => p.mine).Count})");
            src = new MockCommunitySource(seed);   // source 가 늘어나면 여기서 분기한다
            Load(Board, Query);
        }

        public void Load(string board, string query)
        {
            Board = board; Query = query;
            src.LoadPosts(board, query, CommunitySort.Latest, list =>
            {
                cache.Clear(); cache.AddRange(list);
                OnChanged?.Invoke();
            }, err => Debug.LogError("[Community] 목록 실패: " + err));
        }

        // 마이 계열 화면(MyController·MyPostListController)은 커뮤니티의 칩·검색 상태와 무관하게
        // 전량을 봐야 한다. Posts 는 Load(board, query) 로 걸러진 캐시라 그대로 쓰면 오염된다
        // (커뮤니티에서 "인증게시판" 칩을 누른 채 마이로 나가면 내가 쓴 글 숫자·목록이 비어 보이는 버그).
        // ICommunitySource 는 콜백 방식이라 호출부도 콜백으로 받는다 — Mock 은 즉시 부르지만
        // 동기라고 가정하지 않는다(나중에 ApiCommunitySource 가 붙을 자리).
        public void LoadAll(Action<List<CommunityPost>> done) =>
            src.LoadPosts("전체", "", CommunitySort.Latest, done,
                          err => Debug.LogError("[Community] 전체 목록 실패: " + err));

        public void Create(CommunityPost draft) =>
            src.CreatePost(draft, _ => Load(Board, Query),
                           err => Debug.LogError("[Community] 등록 실패: " + err));

        public void ToggleLike(string id) =>
            src.ToggleLike(id, (n, on) => OnChanged?.Invoke(),
                           err => Debug.LogError("[Community] 좋아요 실패: " + err));

        public void AddComment(string id, string body) =>
            src.AddComment(id, body, _ => OnChanged?.Invoke(),
                           err => Debug.LogError("[Community] 댓글 실패: " + err));

        public void Delete(string id) =>
            src.DeletePost(id, () => Load(Board, Query),
                           err => Debug.LogError("[Community] 삭제 실패: " + err));

        public void Report(string id) =>
            src.ReportPost(id, () => Toast.Show("신고가 접수되었습니다."),
                           err => Debug.LogError("[Community] 신고 실패: " + err));

        public CommunityPost Find(string id)
        {
            foreach (var p in cache) if (p.id == id) return p;
            return null;
        }
    }
}
