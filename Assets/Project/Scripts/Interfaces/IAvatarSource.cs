using System;
using System.Collections.Generic;

namespace ARZone
{
    // 아바타 AR 소환 도메인의 유일한 접점.
    // 서버 4개: 세션 생성 / 세션 종료 / 캡처 저장(multipart) / 예절샷 프레임 조회
    public interface IAvatarSource
    {
        // POST …/avatar-interaction-sessions → 201 {avatarInteractionSession:{id}}
        void CreateSession(Action<int> done, Action<string> fail);

        // PATCH …/avatar-interaction-sessions/{id} → 204. 요청 Body 없다 —
        // 서버가 스스로 status=COMPLETED 와 ended_at(서버 현재 KST)을 기록한다.
        // 이미 종료된 세션이면 409 AVATAR_INTERACTION_SESSION_ALREADY_COMPLETED.
        void EndSession(int sessionId, Action done, Action<string> fail);

        // POST …/avatar-interaction-sessions/{id}/pictures → 204 (multipart)
        void UploadPicture(int sessionId, byte[] png, DateTime picturedAt,
                           Action done, Action<string> fail);

        // GET …/album/yezul-frames → 200 {yezulFrames:[{id,title,imageUrl}]}
        void LoadYezulFrames(Action<List<YezulFrameDto>> done, Action<string> fail);
    }
}
