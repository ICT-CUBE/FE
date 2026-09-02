using System;

namespace ARZone
{
    // GET api/v1/users/me/home 응답. 서버 JSON 모양 그대로.
    // 필드는 public 이어야 JsonUtility 가 채운다.

    [Serializable]
    public class HomeUserDto
    {
        public int id;
        public string profileCharacterImgUrl;   // 실제로는 "CHAR_MINA" 같은 코드가 온다(URL 아님)
    }

    [Serializable]
    public class HomeCubeOwnershipDto
    {
        public int id;
        public string avatarNickname;           // null 가능
        public string acquiredAt;               // "2026-08-07T09:20:00" (offset 없는 KST)
    }

    [Serializable]
    public class HomeCubeDto
    {
        public int id;
        public string artistName;
    }

    [Serializable]
    public class HomeLevelDto
    {
        public int id;
        public int level;
        public string levelName;                // Intro | Friend | Close | Special (DB enum 그대로)
        public int levelXp;                     // 명세: "현재 레벨을 완료하는 데 필요한 최대 XP"
    }

    [Serializable]
    public class HomeAffinityDto
    {
        public int id;
        public int currentXp;
        public HomeLevelDto level;
    }

    [Serializable]
    public class HomeResponseDto
    {
        public HomeUserDto user;
        public HomeCubeOwnershipDto cubeOwnership;
        public HomeCubeDto cube;
        public HomeAffinityDto affinity;
    }
}
