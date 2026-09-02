using UnityEngine;
using TMPro;

namespace ARZone
{
    // 허브 상단의 D-day("131 Days")와 "with Jennie"가 씬 목값으로 남아 있었다 —
    // 홈과 같은 서버 값(cubeOwnership.acquiredAt · cube.artistName)으로 채운다 (2026-08-30 유저 지시).
    // 실패하면 씬 값 유지(홈과 같은 관례).
    public class HubTopBinder : MonoBehaviour
    {
        [SerializeField] TMP_Text ddayText;   // "131 Days"
        [SerializeField] TMP_Text withText;   // "with Jennie"

        readonly IHomeSource src = new ApiHomeSource();

        void OnEnable()
        {
            UnifyWithHomeStyle();
            src.LoadHome(Bind, err => Debug.LogWarning($"[Hub] 홈 조회 실패: {err} (씬 값 유지)"));
        }

        // 홈 탑바와 같은 글씨로 통일 (2026-08-31 유저: "홈이랑 아바타홈 탑바 글씨가 좀 달라").
        // 홈(Screen_Home/TopBar/DdayText) 씬 실측값: Pretendard-Medium 16px · weight 500 ·
        // 자간 -1 · 숫자줄 #936AFC(시안 홈.svg 실측 보라) · with 줄 흰색.
        // 허브 씬엔 SemiBold #A855F7 16px + Regular 13px 로 들어가 있어 두 화면이 달라 보였다.
        void UnifyWithHomeStyle()
        {
            var med = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium SDF");
            Style(ddayText, med, new Color32(0x93, 0x6A, 0xFC, 0xFF));
            Style(withText, med, Color.white);
        }

        static void Style(TMP_Text t, TMP_FontAsset font, Color color)
        {
            if (t == null) return;
            if (font != null) t.font = font;
            t.fontWeight = FontWeight.Medium;
            t.fontSize = 16f;
            t.characterSpacing = -1f;
            t.color = color;
        }

        void Bind(HomeResponseDto dto)
        {
            if (dto == null || this == null) return;

            if (ddayText != null && dto.cubeOwnership != null
                && System.DateTime.TryParse(dto.cubeOwnership.acquiredAt,
                       System.Globalization.CultureInfo.InvariantCulture,
                       System.Globalization.DateTimeStyles.None, out var t))
            {
                int days = Mathf.Max(0, (int)(System.DateTime.Now.Date - t.Date).TotalDays);
                ddayText.text = $"{days} Days";
            }
            if (withText != null && dto.cube != null && !string.IsNullOrEmpty(dto.cube.artistName))
                withText.text = "with " + dto.cube.artistName;
        }
    }
}
