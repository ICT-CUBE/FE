using UnityEngine;

namespace ARZone
{
    // 서버 주소·타임아웃을 한 곳에 모은다.
    // 코드에 URL 을 박지 않는 이유: 배포 서버가 바뀌거나 로컬 백엔드로 붙어볼 때
    // .cs 를 고치면 리컴파일이 돌지만, 여기 값만 바꾸면 즉시 바뀐다.
    //
    // 쓰는 쪽: ApiConfig.Url("api/v1/users/me/gallery")
    public static class ApiConfig
    {
        // 한이음 배포 서버. Swagger UI 는 https://hanium.azurewebsites.net/docs
        public const string BaseUrl = "https://hanium.azurewebsites.net";

        // AR 흐름 중에 무한 대기하면 앱이 멈춘 것처럼 보인다. 10초면 충분하다
        // (/health 실측 0.16s, 갤러리 조회 0.3s 대).
        public const int TimeoutSeconds = 10;

        // 이미지는 본문보다 크니 넉넉하게.
        public const int ImageTimeoutSeconds = 20;

        // 음원(mp3 2~4MB) 다운로드용. 데모장 네트워크가 느려도 끊기지 않게.
        public const int MediaTimeoutSeconds = 60;

        // 로그인이 구현되면 여기에 액세스 토큰을 담는다.
        // 지금 서버는 인증이 없다(openapi.json 의 securitySchemes 가 비어 있고
        // 사용자를 id=1 로 고정해 응답한다). ApiClient 가 비어 있지 않을 때만 헤더를 붙인다.
        public static string AccessToken = "";

        // 2026-08-30 서버 auth 배포: 로그인/회원가입 외 모든 API 가 Bearer 토큰을 요구한다.
        // 로그인 성공 시 저장해 앱을 껐다 켜도 유지한다. ApiClient 가 요청 전에 LoadSavedToken 을 부른다.
        const string TokenKey = "arzone.access_token";

        public static void SaveToken(string token)
        {
            AccessToken = token ?? "";
            PlayerPrefs.SetString(TokenKey, AccessToken);
            PlayerPrefs.Save();
        }

        public static void LoadSavedToken()
        {
            if (string.IsNullOrEmpty(AccessToken))
                AccessToken = PlayerPrefs.GetString(TokenKey, "");
        }

        // 명세서 공통사항: "시간 정보는 한국시간(KST) 기준의 offset 없는 ISO 8601"
        // 예) 2026-08-12T17:20:00 — 백엔드가 별도 변환 없이 한국시간으로 취급한다.
        // ⚠ "o"/"s" 서식이나 ToUniversalTime() 을 쓰면 Z·오프셋이 붙어 서버가 다르게 읽는다.
        public static string Iso(System.DateTime t)
            => t.ToString("yyyy-MM-dd'T'HH:mm:ss",
                          System.Globalization.CultureInfo.InvariantCulture);

        // 경로 앞에 / 가 있든 없든 같은 URL 이 나오게 한다.
        // 명세서 CSV 에 "api/v1/..." 과 "/api/v1/..." 이 섞여 있어서 실수하기 쉽다.
        public static string Url(string path)
        {
            if (string.IsNullOrEmpty(path)) return BaseUrl;
            return path.StartsWith("http") ? path
                 : BaseUrl + (path.StartsWith("/") ? path : "/" + path);
        }
    }
}
