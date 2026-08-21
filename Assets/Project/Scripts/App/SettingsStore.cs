using UnityEngine;

namespace ARZone
{
    // 설정 화면(토글 7개) 로컬 저장. 서버 API 가 없어 PlayerPrefs 에 남긴다
    // (ProfileStore/LoginModeSwitcher.HasSignedUp 과 같은 관례).
    //
    // ProfileStore 는 필드가 2개뿐이라 프로퍼티로 하나씩 뺐지만, 여기는 토글이 7개라
    // 항목마다 프로퍼티를 만드는 대신 범용 Get/Set(key, default) 로 뺐다 — 키 접두사(arzone.settings.)는
    // 브리프가 지정한 규칙 그대로다. MySettingsController 가 각 토글의 키 문자열을 인스펙터에서 물고 있다.
    public static class SettingsStore
    {
        const string Prefix = "arzone.settings.";

        public static bool Get(string key, bool defaultValue)
            => PlayerPrefs.GetInt(Prefix + key, defaultValue ? 1 : 0) == 1;

        public static void Set(string key, bool value)
        {
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
