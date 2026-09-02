using System;

namespace ARZone
{
    // auth 도메인 (2026-08-30 서버 배포). 서버 JSON 모양 그대로.

    [Serializable]
    public class AuthLoginReq
    {
        public string email;
        public string password;
    }

    [Serializable]
    public class AuthSignupReq
    {
        public string email;
        public string password;
        public string nickname;
        public string profileCharacterCode;   // "profile_001" ~ "profile_003"
    }

    [Serializable]
    public class AuthUserDto
    {
        public int id;
    }

    [Serializable]
    public class AuthTokenDto
    {
        public string accessToken;
        public string tokenType;      // "Bearer"
        public int expiresIn;         // 초
        public string expiresAt;      // KST, offset 없는 ISO-8601
    }

    // 회원가입(201)·로그인(200) 공통 응답
    [Serializable]
    public class AuthDto
    {
        public AuthUserDto user;
        public AuthTokenDto auth;
    }
}
