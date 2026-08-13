using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 뱃지 도감. 격자 생성 + 필터 칩 + 뱃지 탭 시 상세 시트.
    // Screen_BadgeCollection 에 붙인다.
    public class BadgeCollectionController : MonoBehaviour
    {
        [Header("상단")]
        [SerializeField] Image featuredIcon;
        [SerializeField] TMP_Text featuredTitle;
        [SerializeField] TMP_Text featuredSub;
        [SerializeField] TMP_Text countText;

        [Header("필터")]
        [SerializeField] Button chipAll;
        [SerializeField] Button chipMission;
        [SerializeField] Button chipEvent;
        [SerializeField] Color chipOn = new Color(0.486f, 0.227f, 0.929f, 1f);   // ActionPrimary
        [SerializeField] Color chipOff = new Color(1f, 1f, 1f, 0.08f);

        [Header("격자")]
        [SerializeField] GameObject cellPrefab;   // BadgeCell
        [SerializeField] Transform grid;          // BadgeScroll/Viewport/Content

        [Header("상세 시트")]
        [SerializeField] GameObject detailRoot;   // Sheet_BadgeDetail
        [SerializeField] Button detailDim;
        [SerializeField] TMP_Text detailType;
        [SerializeField] TMP_Text detailName;
        [SerializeField] TMP_Text detailDesc;
        [SerializeField] TMP_Text detailDate;

        BadgeCategory? filter = null;

        void Awake() => UiUtil.SetActive(detailRoot, false);

        void OnEnable()
        {
            if (chipAll != null) chipAll.onClick.AddListener(ShowAll);
            if (chipMission != null) chipMission.onClick.AddListener(ShowMission);
            if (chipEvent != null) chipEvent.onClick.AddListener(ShowEvent);
            if (detailDim != null) detailDim.onClick.AddListener(CloseDetail);
            Refresh();
        }

        void OnDisable()
        {
            if (chipAll != null) chipAll.onClick.RemoveListener(ShowAll);
            if (chipMission != null) chipMission.onClick.RemoveListener(ShowMission);
            if (chipEvent != null) chipEvent.onClick.RemoveListener(ShowEvent);
            if (detailDim != null) detailDim.onClick.RemoveListener(CloseDetail);
            UiUtil.SetActive(detailRoot, false);
        }

        void ShowAll() { filter = null; Refresh(); }
        void ShowMission() { filter = BadgeCategory.Mission; Refresh(); }
        void ShowEvent() { filter = BadgeCategory.Event; Refresh(); }

        void Refresh()
        {
            var c = MissionCatalog.Instance;
            if (c == null) { Debug.LogError("[BadgeCollection] MissionCatalog 이 씬에 없음"); return; }

            Tint(chipAll, filter == null);
            Tint(chipMission, filter == BadgeCategory.Mission);
            Tint(chipEvent, filter == BadgeCategory.Event);

            if (countText != null) countText.text = $"{c.UnlockedCount} / {c.badges.Count}";

            // 대표 뱃지 = 해금된 것 중 마지막
            BadgeData top = null;
            foreach (var b in c.badges) if (b.unlocked) top = b;
            if (top != null)
            {
                if (featuredIcon != null && top.icon != null) featuredIcon.sprite = top.icon;
                if (featuredTitle != null) featuredTitle.text = top.name;
                if (featuredSub != null) featuredSub.text = top.desc;
            }

            BuildGrid(c.FilterBadges(filter));
        }

        void Tint(Button b, bool on)
        {
            if (b == null) return;
            var img = b.GetComponent<Image>();
            if (img != null) img.color = on ? chipOn : chipOff;
        }

        void BuildGrid(List<BadgeData> list)
        {
            if (cellPrefab == null || grid == null) return;
            UiUtil.ClearChildren(grid);

            foreach (var b in list)
            {
                var go = Instantiate(cellPrefab, grid);
                go.SetActive(true);
                UiUtil.SetText(go, "BadgeName", b.name);
                UiUtil.SetSprite(go, "BadgeIcon", b.icon);
                UiUtil.SetActive(go, "LockIcon", !b.unlocked);

                // 잠긴 뱃지는 흐리게
                var icon = go.transform.Find("BadgeIcon");
                if (icon != null)
                {
                    var im = icon.GetComponent<Image>();
                    if (im != null) im.color = new Color(1f, 1f, 1f, b.unlocked ? 1f : 0.25f);
                }

                var bg = go.GetComponent<Image>();
                if (bg == null) continue;
                var btn = bg.GetComponent<Button>() ?? bg.gameObject.AddComponent<Button>();
                var captured = b;
                btn.onClick.AddListener(() => OpenDetail(captured));
            }
        }

        void OpenDetail(BadgeData b)
        {
            if (b == null || !b.unlocked) return;      // 잠긴 건 열지 않는다
            MissionCatalog.Instance.SelectBadge(b);

            if (detailType != null) detailType.text = b.typeLabel;
            if (detailName != null) detailName.text = b.name;
            if (detailDesc != null) detailDesc.text = b.desc;
            if (detailDate != null) detailDate.text = $"획득 일시 : {b.acquiredDate}";

            UiUtil.SetActive(detailRoot, true);
        }

        void CloseDetail() => UiUtil.SetActive(detailRoot, false);
    }
}