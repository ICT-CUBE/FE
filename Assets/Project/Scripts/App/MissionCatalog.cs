using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 친밀도·혜택·뱃지 데이터 보관소 + 현재 선택 항목.
    // 지금은 인스펙터 임시 데이터. 백엔드 나오면 여기만 갈아끼운다.
    public class MissionCatalog : MonoBehaviour
    {
        public static MissionCatalog Instance { get; private set; }

        [Header("친밀도")]
        public int level = 3;
        public string levelTitle = "Close";
        public string nextLevelTitle = "베프";
        public int exp = 1245;
        public int expMax = 2000;

        [Header("목록")]
        public List<BenefitData> benefits = new List<BenefitData>();
        public List<MissionData> todayActivities = new List<MissionData>();
        public List<MissionData> levelChanges = new List<MissionData>();
        public List<BadgeData> badges = new List<BadgeData>();

        [Header("레벨 타임라인")]
        // 와프의 Lv.1 Intro / Lv.2 Friend / Lv.3 Close / Lv.4 Special
        public List<string> levelNames = new List<string> { "Intro", "Friend", "Close", "Special" };
        // 다음 레벨에서 열리는 혜택 (해금 보상 3칸)
        public List<BenefitData> nextUnlocks = new List<BenefitData>();

        public MissionData SelectedMission { get; private set; }
        public BadgeData SelectedBadge { get; private set; }

        public float Ratio => expMax > 0 ? Mathf.Clamp01((float)exp / expMax) : 0f;
        public int Remain => Mathf.Max(0, expMax - exp);

        void Awake() => Instance = this;

        public void SelectMission(MissionData m) => SelectedMission = m;
        public void SelectBadge(BadgeData b) => SelectedBadge = b;

        public List<BadgeData> FilterBadges(BadgeCategory? c)
        {
            if (c == null) return new List<BadgeData>(badges);
            var r = new List<BadgeData>();
            foreach (var b in badges) if (b.category == c.Value) r.Add(b);
            return r;
        }

        public int UnlockedCount
        {
            get { int n = 0; foreach (var b in badges) if (b.unlocked) n++; return n; }
        }
    }
}