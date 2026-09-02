using System;
using UnityEngine;

namespace ARZone
{
    // 로그인·회원가입 (2026-08-30 서버 auth 배포).
    // 이 둘만 토큰 없이 부를 수 있고, 성공하면 액세스 토큰을 저장한다 —
    // 이후 모든 요청에는 ApiClient 가 Bearer 헤더로 붙인다.
    public static class ApiAuth
    {
        public static void Login(string email, string password,
                                 Action<AuthDto> done, Action<string> fail)
            => Post("api/v1/auth/login/email",
                    JsonUtility.ToJson(new AuthLoginReq { email = email, password = password }),
                    done, fail);

        public static void Signup(string email, string password, string nickname,
                                  string profileCharacterCode,
                                  Action<AuthDto> done, Action<string> fail)
            => Post("api/v1/auth/signup",
                    JsonUtility.ToJson(new AuthSignupReq
                    {
                        email = email, password = password,
                        nickname = nickname, profileCharacterCode = profileCharacterCode,
                    }),
                    done, fail);

        static void Post(string path, string body, Action<AuthDto> done, Action<string> fail)
        {
            ApiClient.Post(path, body, json =>
            {
                try
                {
                    var dto = JsonUtility.FromJson<AuthDto>(json);
                    if (dto == null || dto.auth == null || string.IsNullOrEmpty(dto.auth.accessToken))
                    {
                        fail?.Invoke("응답을 읽지 못했어요");
                        return;
                    }
                    ApiConfig.SaveToken(dto.auth.accessToken);
                    Debug.Log($"[Auth] 사용자 {(dto.user != null ? dto.user.id : 0)} 인증 — 토큰 저장 (만료 {dto.auth.expiresAt})");
                    done?.Invoke(dto);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Auth] 응답 파싱 실패: {e.Message}\n{json}");
                    fail?.Invoke("응답을 읽지 못했어요");
                }
            }, fail);
        }
    }
}
