using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 목업 글 데이터. 인스펙터에서 편집한다.
    // 작성자 아이콘은 회원가입에서 고르는 아바타 3종을 돌려 쓴다
    // (avatar_male_circle · avatar_female_circle · avatar_animal_circle).
    [CreateAssetMenu(fileName = "CommunitySeed", menuName = "ARZone/Community Seed")]
    public class CommunitySeed : ScriptableObject
    {
        public List<CommunityPost> posts = new List<CommunityPost>();
    }
}
