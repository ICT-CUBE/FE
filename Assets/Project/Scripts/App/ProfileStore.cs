using UnityEngine;

namespace ARZone
{
    // 프로필(아바타·닉네임) 로컬 저장. 서버 API 가 없어 PlayerPrefs 에 남긴다
    // (LoginModeSwitcher.HasSignedUp 과 같은 관례). MyProfileEditController(쓰기) 와
    // MyController(읽기) 가 같은 키를 쓰기 위해 한곳에 모았다.
    public static class ProfileStore
    {
        const string KeyAvatar = "arzone.profile.avatar";
        const string KeyNickname = "arzone.profile.nickname";

        // 기본값은 Screen_My 가 Task1 에서부터 갖고 있던 값과 같다
        // (avatar_animal_circle.png = 동물형(index 2) · 닉네임 "DORI").
        public const int DefaultAvatar = 2;      // 0 남성형 · 1 여성형 · 2 동물형 (AvatarNameBinder.profiles 순서)
        public const string DefaultNickname = "DORI";

        public static int AvatarIndex
        {
            get => PlayerPrefs.GetInt(KeyAvatar, DefaultAvatar);
            set { PlayerPrefs.SetInt(KeyAvatar, value); PlayerPrefs.Save(); }
        }

        public static string Nickname
        {
            get => PlayerPrefs.GetString(KeyNickname, DefaultNickname);
            set { PlayerPrefs.SetString(KeyNickname, value); PlayerPrefs.Save(); }
        }
    }
}
