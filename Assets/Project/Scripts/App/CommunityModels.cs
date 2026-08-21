using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 커뮤니티 글 한 개. 서버가 붙으면 JSON 을 여기에 매핑한다.
    [Serializable]
    public class CommunityPost
    {
        public string id;
        public string board;        // "자유게시판" | "인증게시판"
        public string title;
        public string body;
        public string author;
        public string timeText;     // "방금 전" · "2시간 전" — 서버가 주면 그대로 쓴다
        public int    likes;
        public int    comments;
        public bool   liked;        // 내가 누른 상태
        public bool   mine;         // 내 글이면 ⋮ 에 삭제하기가 뜬다
        public Sprite authorIcon;
        public List<Sprite> photos = new List<Sprite>();
        public List<CommunityComment> replies = new List<CommunityComment>();
    }

    [Serializable]
    public class CommunityComment
    {
        public string author;
        public string timeText;
        public string body;
        public Sprite authorIcon;
    }

    // 시안에는 '최신순' 하나뿐이다. 드롭다운이 열린 시안이 없어 다른 항목을 지어내지 않는다.
    public enum CommunitySort { Latest }
}
