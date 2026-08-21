using System;
using System.Collections.Generic;

namespace ARZone
{
    // 커뮤니티 데이터의 유일한 접점.
    // 지금은 MockCommunitySource(메모리), 나중에 ApiCommunitySource(UnityWebRequest) 로 갈아끼운다.
    // 화면 코드는 이 인터페이스만 보므로 서버가 붙어도 안 바뀐다.
    //
    // 콜백 방식인 이유: UnityWebRequest 가 코루틴이라 콜백이 그대로 맞고,
    // Mock 은 즉시 done 을 부르면 되므로 호출부가 동일하다.
    // fail 은 나중에 네트워크 오류를 토스트로 띄울 자리다 — Mock 은 부르지 않는다.
    public interface ICommunitySource
    {
        void LoadPosts(string board, string query, CommunitySort sort,
                       Action<List<CommunityPost>> done, Action<string> fail);
        void CreatePost(CommunityPost draft, Action<CommunityPost> done, Action<string> fail);
        void ToggleLike(string postId, Action<int, bool> done, Action<string> fail);
        void AddComment(string postId, string body, Action<CommunityComment> done, Action<string> fail);
        void DeletePost(string postId, Action done, Action<string> fail);
        void ReportPost(string postId, Action done, Action<string> fail);
    }
}
