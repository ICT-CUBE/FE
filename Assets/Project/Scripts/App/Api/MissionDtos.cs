using System;

namespace ARZone
{
    // 미션 목록 — GET /api/v1/users/me/missions
    [Serializable]
    public class MissionRewardDto
    {
        public string rewardType;     // AFFINITY_POINT | BADGE …
        public int rewardCount;
    }

    [Serializable]
    public class MissionItemDto
    {
        public int id;
        public string title;
        public string description;    // null 가능
        public string missionType;    // DAILY | EVENT
        public int targetCount;
        public int currentCount;
        public string status;         // IN_PROGRESS | COMPLETED | REWARDED …
        public bool rewardClaimed;
        public string completedAt;
        public string claimedAt;
        public MissionRewardDto reward;
        public string startAt;
        public string endAt;
    }

    [Serializable]
    public class MissionsLevelDto
    {
        public int level;
        public string levelName;
        public int levelXp;
    }

    [Serializable]
    public class MissionsAffinityDto
    {
        public int currentXp;
        public int todayEarnedXp;
        public MissionsLevelDto level;
    }

    [Serializable]
    public class MissionsDto
    {
        public MissionsAffinityDto affinity;
        public MissionItemDto[] dailyMissions;
        public MissionItemDto[] eventMissions;
    }
}
