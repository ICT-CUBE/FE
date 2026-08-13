using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 미션 카드를 탭하면 상세 시트를 채워서 연다.
    // 시트 자체가 비활성이라 이 컴포넌트는 항상 켜져 있는 오브젝트에 붙인다.
    public class MissionDetailSheet : MonoBehaviour
    {
        [SerializeField] MissionListController missionList;

        [Header("시트")]
        [SerializeField] GameObject sheetRoot;      // Sheet_MissionDetail
        [SerializeField] Button dimButton;          // Dim (눌러서 닫기)
        [SerializeField] TMP_Text typeLabel;        // TypeChip 안 라벨
        [SerializeField] TMP_Text detailTitle;
        [SerializeField] TMP_Text detailGuide;
        [SerializeField] TMP_Text rewardName;
        [SerializeField] TMP_Text rewardValue;
        [SerializeField] Button btnGoMission;

        MissionData current;

        void Awake() => UiUtil.SetActive(sheetRoot, false);

        void OnEnable()
        {
            if (missionList != null) missionList.CardClicked += Open;
            if (btnGoMission != null) btnGoMission.onClick.AddListener(OnGo);
            if (dimButton != null) dimButton.onClick.AddListener(Close);
        }

        void OnDisable()
        {
            if (missionList != null) missionList.CardClicked -= Open;
            if (btnGoMission != null) btnGoMission.onClick.RemoveListener(OnGo);
            if (dimButton != null) dimButton.onClick.RemoveListener(Close);
        }

        public void Open(MissionData m)
        {
            current = m;
            if (m == null) return;

            if (typeLabel != null) typeLabel.text = m.typeLabel;
            if (detailTitle != null) detailTitle.text = m.title;
            if (detailGuide != null) detailGuide.text = string.IsNullOrEmpty(m.guide) ? m.desc : m.guide;
            if (rewardName != null) rewardName.text = m.rewardName;
            if (rewardValue != null) rewardValue.text = $"{m.reward}";   // 점은 RewardIcon 이 담당

            UiUtil.SetActive(sheetRoot, true);
        }

        public void Close() => UiUtil.SetActive(sheetRoot, false);

        void OnGo()
        {
            Close();
            if (current == null || string.IsNullOrEmpty(current.goScreen)) return;
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(current.goScreen);
        }
    }
}