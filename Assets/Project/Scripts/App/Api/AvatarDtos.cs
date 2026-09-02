using System;

namespace ARZone
{
    // 아바타 소환(avatar) 도메인 응답. 서버 JSON 모양 그대로.

    // POST …/avatar-interaction-sessions → {"avatarInteractionSession":{"id":4}}
    [Serializable]
    public class AvatarSessionItemDto
    {
        public int id;
    }

    [Serializable]
    public class AvatarSessionCreateDto
    {
        public AvatarSessionItemDto avatarInteractionSession;
    }

    // 예절샷 프레임. imageUrl 은 지금 "album_image_memory_cube_001" 같은 코드가 온다(URL 아님).
    [Serializable]
    public class YezulFrameDto
    {
        public int id;
        public string title;      // null 가능
        public string imageUrl;
    }

    [Serializable]
    public class YezulFramesDto
    {
        public YezulFrameDto[] yezulFrames;
    }
}
