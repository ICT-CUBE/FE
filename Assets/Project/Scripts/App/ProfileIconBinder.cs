using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 유저 프로필 아이콘을 마이페이지에서 고른 프로필(ProfileStore.AvatarIndex)로 맞춘다.
    // (2026-08-27 — 홈·허브 상단, 마켓 홈이 토끼 그림으로 하드코딩돼 있어 화면마다 제각각이었다)
    // 붙는 곳: Screen_Home/TopBar/ProfileIcon · Screen_Hub/TopBar/ProfileIcon · Screen_Market/…/ProfileImg
    // ⚠ 커뮤니티 글쓴이·마켓 판매자 아이콘은 '남의 프로필'이라 안 붙인다.
    [RequireComponent(typeof(Image))]
    public class ProfileIconBinder : MonoBehaviour
    {
        [SerializeField] Sprite[] sprites;   // 0 남성형 · 1 여성형 · 2 동물형 (ProfileStore 순서)

        void OnEnable()
        {
            var img = GetComponent<Image>();
            int i = ProfileStore.AvatarIndex;
            if (img != null && sprites != null && i >= 0 && i < sprites.Length && sprites[i] != null)
                img.sprite = sprites[i];
        }
    }
}
