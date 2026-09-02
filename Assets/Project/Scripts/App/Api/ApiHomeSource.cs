using System;
using UnityEngine;

namespace ARZone
{
    // 서버로 도는 홈 구현.
    public class ApiHomeSource : IHomeSource
    {
        const string Path = "api/v1/users/me/home";

        public void LoadHome(Action<HomeResponseDto> done, Action<string> fail)
        {
            ApiClient.Get(Path, json =>
            {
                try
                {
                    var dto = JsonUtility.FromJson<HomeResponseDto>(json);
                    if (dto == null) { fail?.Invoke("응답을 읽지 못했어요"); return; }
                    done?.Invoke(dto);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Api] 홈 응답 파싱 실패: {e.Message}\n{json}");
                    fail?.Invoke("응답을 읽지 못했어요");
                }
            }, fail);
        }
    }

    // 서버 없이 도는 구현. 실서버 실측값을 그대로 쓴다 —
    // Mock 과 Api 가 다른 모양을 주면 화면이 Mock 에서만 맞는 일이 생긴다.
    public class MockHomeSource : IHomeSource
    {
        public void LoadHome(Action<HomeResponseDto> done, Action<string> fail)
        {
            done?.Invoke(new HomeResponseDto
            {
                user = new HomeUserDto { id = 1, profileCharacterImgUrl = "CHAR_MINA" },
                cubeOwnership = new HomeCubeOwnershipDto
                {
                    id = 1,
                    avatarNickname = "루미",
                    acquiredAt = "2026-08-07T09:20:00",
                },
                cube = new HomeCubeDto { id = 1, artistName = "ARZONE" },
                affinity = new HomeAffinityDto
                {
                    id = 1,
                    currentXp = 120,
                    level = new HomeLevelDto { id = 2, level = 2, levelName = "Friend", levelXp = 100 },
                },
            });
        }
    }
}
