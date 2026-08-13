using UnityEngine;

namespace ARZone
{
    // 친밀도 현황의 혜택 카드 (대화수 증가 / 신규 기능 / 특별 멘트)
    [System.Serializable]
    public class BenefitData
    {
        public Sprite icon;
        public string title = "대화수 증가";
        public string desc = "10회 → 30회";
    }

    public enum BadgeCategory { Mission, Event }

    // 뱃지 도감 한 칸
    [System.Serializable]
    public class BadgeData
    {
        public Sprite icon;
        public string name = "BORN PINK 마스터";
        [TextArea] public string desc;
        public string typeLabel = "일일 미션";
        public string acquiredDate = "2026년 07월 10일";
        public BadgeCategory category = BadgeCategory.Mission;
        public bool unlocked;
    }
}