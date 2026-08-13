using UnityEngine;

namespace ARZone
{
    // 미션 한 개의 데이터. 나중에 서버에서 받아올 형태.
    [System.Serializable]
    public class MissionData
    {
        public string title;      // "앱 출석하기"
        public string desc;       // "AR ZONE 접속"
        public int reward;        // 20
        public int current;       // 1 (현재 진행)
        public int total;         // 1 (목표)
        public bool claimable;    // 보상받기 가능?
        public bool claimed;      // 보상을 이미 받았나 (버튼 '완료됨')
        public Sprite icon;       // 완료 상태 아이콘 (보라 채움)
        public Sprite iconOff;    // 미완료 상태 아이콘 (흰 선 → 회색 틴트)

        [Header("상세 시트용")]
        [TextArea] public string guide;              // 미션 안내 본문
        public string typeLabel = "일일 미션";        // 시트 상단 칩
        public string rewardName = "친밀도 경험치";   // 보상 이름
        public string goScreen;                      // '미션 수행하러 가기' 대상 화면 id
        public bool badgeReward;                     // 보상이 한정 뱃지인가 (레벨업 vs 뱃지수여 분기)

        public MissionData() { }

        public MissionData(string title, string desc, int reward, int current, int total, bool claimable, Sprite icon = null)
        {
            this.title = title; this.desc = desc; this.reward = reward;
            this.current = current; this.total = total; this.claimable = claimable;
            this.icon = icon;
        }
    }
}