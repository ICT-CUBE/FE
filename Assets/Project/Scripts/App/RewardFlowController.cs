using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 보상받기 → 레벨업 또는 한정 뱃지 수여 화면으로 분기.
    // 항상 켜져 있는 오브젝트에 붙인다.
    public class RewardFlowController : MonoBehaviour
    {
        [SerializeField] MissionListController missionList;

        [Header("레벨업 화면")]
        [SerializeField] TMP_Text fromLevel;
        [SerializeField] TMP_Text toLevel;
        [SerializeField] TMP_Text levelUpDesc;
        [SerializeField] Button btnLevelUpConfirm;

        [Header("뱃지 수여 화면")]
        [SerializeField] TMP_Text badgeNameLabel;   // BadgeNameChip 안 라벨
        [SerializeField] TMP_Text badgeDesc;
        [SerializeField] Button btnClaim;

        [SerializeField] string returnScreen = "Mission";
        [SerializeField] string levelResultScreen = "LevelUpResult";

        const string LevelUpId = "LevelUp";
        const string BadgeRewardId = "BadgeReward";

        void OnEnable()
        {
            if (missionList != null) missionList.ActionClicked += OnAction;
            if (btnLevelUpConfirm != null) btnLevelUpConfirm.onClick.AddListener(GoResult);
            if (btnClaim != null) btnClaim.onClick.AddListener(Back);
        }

        void OnDisable()
        {
            if (missionList != null) missionList.ActionClicked -= OnAction;
            if (btnLevelUpConfirm != null) btnLevelUpConfirm.onClick.RemoveListener(GoResult);
            if (btnClaim != null) btnClaim.onClick.RemoveListener(Back);
        }

        void OnAction(MissionData m)
        {
            if (m == null || ScreenManager.Instance == null) return;

            // 아직 못 받는 미션이면 그냥 해당 화면으로 보낸다
            if (!m.claimable)
            {
                if (!string.IsNullOrEmpty(m.goScreen)) ScreenManager.Instance.Show(m.goScreen);
                return;
            }

            var c = MissionCatalog.Instance;
            if (c != null)
            {
                c.exp += m.reward;
                if (c.exp >= c.expMax) c.exp = c.expMax;    // 레벨업 연출은 화면에서
            }
            m.claimable = false;
            m.claimed = true;
            if (missionList != null) missionList.Refresh();   // 버튼을 '완료됨' 으로
            // ✨ 이모지는 Pretendard 에 글리프가 없어 네모로 깨진다(2026-08-26 실기) — 뺐다
            Toast.Show($"JENNIE 친밀도 {m.reward}P 획득!");

            // 2026-08-30 유저 지시: 보상받기 → '완료됨'으로 바뀌고 화면 이동 없이 그대로 둔다.
            // (레벨업·뱃지 연출 화면으로 넘기던 것을 뺐다 — ShowBadge/ShowLevelUp 은 남겨둠)
        }

        void ShowLevelUp(MissionCatalog c)
        {
            if (c != null)
            {
                // 다음 레벨 이름은 levelNames 를 단일 출처로 쓴다(nextLevelTitle 은 예비값).
                // 시안은 "Lv.3 Close → Lv.4 Special" — '해금!' 은 붙이지 않는다.
                string nextName = (c.levelNames != null && c.level < c.levelNames.Count)
                                ? c.levelNames[c.level] : c.nextLevelTitle;
                if (fromLevel != null) fromLevel.text = $"Lv.{c.level} {c.levelTitle}";
                if (toLevel != null) toLevel.text = $"Lv.{c.level + 1} {nextName}";
            }
            if (levelUpDesc != null)
                levelUpDesc.text = "함께한 시간이 쌓여 새로운 레벨에 도달했어요.";

            ScreenManager.Instance.Show(LevelUpId);
        }

        void ShowBadge(MissionData m)
        {
            if (badgeNameLabel != null) badgeNameLabel.text = $"{m.title} 뱃지";
            if (badgeDesc != null)
                badgeDesc.text = "JENNIE의 특별한 순간을\n함께한 증표가 발급되었어요!";

            ScreenManager.Instance.Show(BadgeRewardId);
        }

        // 레벨업 축하 화면의 '확인' → 도달한 레벨의 결과 화면으로.
        // 여기서 실제로 레벨을 올린다.
        void GoResult()
        {
            var c = MissionCatalog.Instance;
            if (c != null && c.exp >= c.expMax)
            {
                c.level += 1;
                c.exp = 0;
                if (c.levelNames != null && c.level - 1 < c.levelNames.Count)
                    c.levelTitle = c.levelNames[c.level - 1];
            }
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(levelResultScreen);
        }

        void Back()
        {
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(returnScreen);
        }
    }
}