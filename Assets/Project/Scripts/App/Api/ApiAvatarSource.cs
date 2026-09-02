using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 서버로 도는 아바타 소환 구현.
    public class ApiAvatarSource : IAvatarSource
    {
        const string Sessions = "api/v1/users/me/avatar-interaction-sessions";
        const string Frames   = "api/v1/users/me/album/yezul-frames";

        public void CreateSession(Action<int> done, Action<string> fail)
        {
            ApiClient.Post(Sessions, null, json =>
            {
                try
                {
                    var dto = JsonUtility.FromJson<AvatarSessionCreateDto>(json);
                    if (dto == null || dto.avatarInteractionSession == null || dto.avatarInteractionSession.id <= 0)
                    {
                        Debug.LogError($"[Api] 소환 세션 id 를 못 읽었다: {json}");
                        fail?.Invoke("세션을 시작하지 못했어요");
                        return;
                    }
                    Debug.Log($"[Avatar] 소환 세션 시작 id={dto.avatarInteractionSession.id}");
                    done?.Invoke(dto.avatarInteractionSession.id);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Api] 소환 세션 응답 파싱 실패: {e.Message}\n{json}");
                    fail?.Invoke("세션을 시작하지 못했어요");
                }
            }, fail);
        }

        public void EndSession(int sessionId, Action done, Action<string> fail)
        {
            if (sessionId <= 0) { fail?.Invoke("세션이 없어요"); return; }
            // Body 없음 — 명세서 "요청 Body 없이 호출한다".
            ApiClient.Patch($"{Sessions}/{sessionId}", null, _ =>
            {
                Debug.Log($"[Avatar] 소환 세션 종료 id={sessionId}");
                done?.Invoke();
            }, fail);
        }

        public void UploadPicture(int sessionId, byte[] png, DateTime picturedAt,
                                  Action done, Action<string> fail)
        {
            if (sessionId <= 0) { fail?.Invoke("세션이 없어요"); return; }

            var fields = new Dictionary<string, string>
            {
                // offset 없는 KST. "o"/"s" 서식을 쓰면 Z 가 붙어 서버가 다르게 읽는다.
                { "picturedAt", ApiConfig.Iso(picturedAt) },
            };

            ApiClient.Upload($"{Sessions}/{sessionId}/pictures",
                             "image", $"capture_{ApiConfig.Iso(picturedAt).Replace(":", "")}.png",
                             png, "image/png", fields,
                             _ => done?.Invoke(), fail);
        }

        public void LoadYezulFrames(Action<List<YezulFrameDto>> done, Action<string> fail)
        {
            ApiClient.Get(Frames, json =>
            {
                try
                {
                    var dto = JsonUtility.FromJson<YezulFramesDto>(json);
                    var list = new List<YezulFrameDto>();
                    if (dto != null && dto.yezulFrames != null) list.AddRange(dto.yezulFrames);
                    Debug.Log($"[Avatar] 예절샷 프레임 {list.Count}개");
                    done?.Invoke(list);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Api] 예절샷 프레임 파싱 실패: {e.Message}\n{json}");
                    fail?.Invoke("응답을 읽지 못했어요");
                }
            }, fail);
        }
    }

    // 서버 없이 도는 구현. 실서버 실측값을 그대로 쓴다.
    public class MockAvatarSource : IAvatarSource
    {
        int next = 100;

        public void CreateSession(Action<int> done, Action<string> fail) => done?.Invoke(next++);
        public void EndSession(int sessionId, Action done, Action<string> fail) => done?.Invoke();
        public void UploadPicture(int sessionId, byte[] png, DateTime picturedAt,
                                  Action done, Action<string> fail) => done?.Invoke();

        public void LoadYezulFrames(Action<List<YezulFrameDto>> done, Action<string> fail)
            => done?.Invoke(new List<YezulFrameDto>
            {
                new YezulFrameDto { id = 3, title = "예줄 프레임", imageUrl = "album_image_memory_cube_001" },
            });
    }
}
