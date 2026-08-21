using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 서버 없이 도는 메모리 구현. 시드에서 초기 목록을 복사해 들고 있는다.
    // 모든 콜백을 즉시 부른다 — fail 은 절대 부르지 않는다.
    public class MockCommunitySource : ICommunitySource
    {
        readonly List<CommunityPost> posts = new List<CommunityPost>();
        int nextId = 1;

        public MockCommunitySource(CommunitySeed seed)
        {
            if (seed == null) return;
            foreach (var p in seed.posts) posts.Add(Copy(p));
        }

        public void LoadPosts(string board, string query, CommunitySort sort,
                              Action<List<CommunityPost>> done, Action<string> fail)
        {
            var list = new List<CommunityPost>();
            foreach (var p in posts)
            {
                if (!string.IsNullOrEmpty(board) && board != "전체" && p.board != board) continue;
                if (!string.IsNullOrEmpty(query)
                    && (p.title == null || p.title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    && (p.body == null || p.body.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                list.Add(p);
            }
            // 목록은 이미 최신순으로 들고 있다(새 글은 맨 앞에 넣는다). CommunitySort 는 Latest 하나뿐.
            done?.Invoke(list);
        }

        public void CreatePost(CommunityPost draft, Action<CommunityPost> done, Action<string> fail)
        {
            draft.id = "local" + (nextId++);
            draft.timeText = "방금 전";
            draft.mine = true;
            posts.Insert(0, draft);
            done?.Invoke(draft);
        }

        public void ToggleLike(string postId, Action<int, bool> done, Action<string> fail)
        {
            var p = Find(postId);
            if (p == null) return;
            p.liked = !p.liked;
            p.likes += p.liked ? 1 : -1;
            if (p.likes < 0) p.likes = 0;
            done?.Invoke(p.likes, p.liked);
        }

        public void AddComment(string postId, string body, Action<CommunityComment> done, Action<string> fail)
        {
            var p = Find(postId);
            if (p == null) return;
            var c = new CommunityComment { author = "나", timeText = "방금 전", body = body };
            p.replies.Add(c);
            p.comments = p.replies.Count;
            done?.Invoke(c);
        }

        public void DeletePost(string postId, Action done, Action<string> fail)
        {
            var p = Find(postId);
            if (p != null) posts.Remove(p);
            done?.Invoke();
        }

        public void ReportPost(string postId, Action done, Action<string> fail)
        {
            // 목업에선 아무것도 안 한다. 서버가 붙으면 여기서 POST 한다.
            done?.Invoke();
        }

        // ⚠ 시드는 ScriptableObject 에셋이다. 참조로 담으면 플레이 중의 좋아요·댓글이
        // 에셋 파일을 영구히 오염시킨다(플레이를 멈춰도 안 돌아온다). 반드시 깊은 복사한다.
        CommunityPost Copy(CommunityPost s)
        {
            var p = new CommunityPost
            {
                id        = string.IsNullOrEmpty(s.id) ? "seed" + (nextId++) : s.id,
                board     = s.board,
                title     = s.title,
                body      = s.body,
                author    = s.author,
                timeText  = s.timeText,
                likes     = s.likes,
                comments  = s.comments,
                liked     = s.liked,
                mine      = s.mine,
                authorIcon = s.authorIcon,
            };
            if (s.photos != null) p.photos.AddRange(s.photos);   // Sprite 는 에셋이라 참조 공유해도 된다
            if (s.replies != null)
                foreach (var c in s.replies)
                    p.replies.Add(new CommunityComment
                    {
                        author = c.author, timeText = c.timeText,
                        body = c.body, authorIcon = c.authorIcon,
                    });
            return p;
        }

        CommunityPost Find(string id)
        {
            foreach (var p in posts) if (p.id == id) return p;
            return null;
        }
    }
}
