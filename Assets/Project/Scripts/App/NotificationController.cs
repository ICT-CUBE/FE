using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 알림 화면. 탭 필터 · 읽음 처리 · 모두 읽음 · 카드 탭 시 이동.
    // 목록 데이터는 여기 인스펙터에 둔다(백엔드 나오면 Load 만 교체).
    public class NotificationController : MonoBehaviour
    {
        [Header("목록")]
        [SerializeField] GameObject rowPrefab;      // NotifRow
        [SerializeField] Transform content;         // NotifScroll/Viewport/Content

        // 시안 카드 높이는 본문 줄 수를 따라간다: 1줄 103 · 2줄 124 · 3줄 145.
        // 시안 배경은 #999999→#060914 대각 그라디언트 @20% 라 9-slice 로는 못 만든다.
        // 높이별 PNG(글자 없는 배경만)를 받으면 여기에 물릴 것 — 비어 있으면 프리팹 배경을 쓴다.
        [Header("카드 배경 (높이별, 없으면 프리팹 배경)")]
        [SerializeField] Sprite cardBg1;            // 본문 1줄 (372x103)
        [SerializeField] Sprite cardBg2;            // 본문 2줄 (372x124)

        const float RowBase = 103f;                 // 1줄 카드 높이
        const float RowLine = 21f;                  // 줄이 하나 늘 때마다

        [Header("탭")]
        [SerializeField] Button tabAll;
        [SerializeField] Button tabMission;
        [SerializeField] Button tabEvent;
        [SerializeField] Button tabMarket;
        [SerializeField] RectTransform tabIndicator;
        [SerializeField] Color tabOn = Color.white;
        [SerializeField] Color tabOff = new Color(0.447f, 0.471f, 0.525f, 1f);

        [Header("기타")]
        [SerializeField] Button btnReadAll;
        [SerializeField] GameObject bellDot;        // (구) 별도 점 오브젝트
        [SerializeField] NotifyDot bellButton;      // 홈 종 버튼 (점 포함 스프라이트 교체)

        [Header("데이터 (임시)")]
        [SerializeField] List<NotificationData> items = new List<NotificationData>();

        NotifCategory? filter = null;

        public bool HasUnread
        {
            get { foreach (var n in items) if (!n.read) return true; return false; }
        }

        void OnEnable()
        {
            Hook(tabAll, ShowAll);
            Hook(tabMission, ShowMission);
            Hook(tabEvent, ShowEvent);
            Hook(tabMarket, ShowMarket);
            Hook(btnReadAll, ReadAll);
            Refresh();
        }

        void OnDisable()
        {
            Unhook(tabAll, ShowAll);
            Unhook(tabMission, ShowMission);
            Unhook(tabEvent, ShowEvent);
            Unhook(tabMarket, ShowMarket);
            Unhook(btnReadAll, ReadAll);
            SyncBellDot();
        }

        void ShowAll() { filter = null; Refresh(); }
        void ShowMission() { filter = NotifCategory.Mission; Refresh(); }
        void ShowEvent() { filter = NotifCategory.Event; Refresh(); }
        void ShowMarket() { filter = NotifCategory.Market; Refresh(); }

        void ReadAll()
        {
            foreach (var n in items) n.read = true;
            Refresh();
        }

        void Refresh()
        {
            SyncTabs();
            Build();
            SyncBellDot();
        }

        void SyncTabs()
        {
            SetTab(tabAll, filter == null);
            SetTab(tabMission, filter == NotifCategory.Mission);
            SetTab(tabEvent, filter == NotifCategory.Event);
            SetTab(tabMarket, filter == NotifCategory.Market);

            // 밑줄을 선택된 탭 아래로 옮긴다
            var sel = filter == null ? tabAll
                    : filter == NotifCategory.Mission ? tabMission
                    : filter == NotifCategory.Event ? tabEvent : tabMarket;
            if (tabIndicator != null && sel != null)
            {
                var rt = (RectTransform)sel.transform;
                tabIndicator.anchoredPosition = new Vector2(
                    rt.anchoredPosition.x, tabIndicator.anchoredPosition.y);
                tabIndicator.sizeDelta = new Vector2(rt.rect.width, tabIndicator.sizeDelta.y);
            }
        }

        void SetTab(Button b, bool on)
        {
            if (b == null) return;
            var t = b.GetComponentInChildren<TMP_Text>();
            if (t != null) t.color = on ? tabOn : tabOff;
        }

        void Build()
        {
            if (rowPrefab == null || content == null)
            {
                Debug.LogError("[Notification] rowPrefab / content 연결 필요");
                return;
            }

            UiUtil.EnsureVertical(content, 20f);   // 시안 48: 카드 181(103)→304 → 간격 20
            UiUtil.ClearChildren(content);

            foreach (var n in items)
            {
                if (filter != null && n.category != filter.Value) continue;

                var row = Instantiate(rowPrefab, content);
                row.SetActive(true);
                UiUtil.SetText(row, "NotifTitle", n.title);
                UiUtil.SetText(row, "NotifBody", n.body);
                UiUtil.SetText(row, "TimeText", n.time);
                UiUtil.SetSprite(row, "Icon", n.icon);   // 새 아이콘은 32x32 박스까지 구워져 있다
                UiUtil.SetActive(row, "UnreadDot", !n.read);

                // 본문 줄 수에 맞춰 카드 높이와 배경을 고른다
                int lines = string.IsNullOrEmpty(n.body) ? 1 : n.body.Split('\n').Length;
                var rowRt = row.transform as RectTransform;
                if (rowRt != null)
                    rowRt.sizeDelta = new Vector2(rowRt.sizeDelta.x, RowBase + RowLine * (lines - 1));

                // 루트엔 Image 가 없고 배경은 자식 Bg 에 있다. 루트 기준으로 찾으면
                // 전부 건너뛰어 클릭이 아예 안 먹었다.
                var bg = row.GetComponent<Image>();
                if (bg == null)
                {
                    var t = row.transform.Find("Bg");
                    if (t != null) bg = t.GetComponent<Image>();
                }
                if (bg == null) continue;

                // 높이별 배경이 들어오면 그때 교체한다. 없으면 프리팹의 9-slice 를 그대로 둔다.
                var cardSpr = lines <= 1 ? cardBg1 : cardBg2;
                if (cardSpr != null) { bg.sprite = cardSpr; bg.type = Image.Type.Simple; }

                var btn = row.GetComponent<Button>() ?? row.AddComponent<Button>();
                btn.targetGraphic = bg;
                var captured = n;
                btn.onClick.AddListener(() => Open(captured));
            }
        }

        // 카드 탭 = 읽음 처리 + (대상 화면이 있으면) 이동
        void Open(NotificationData n)
        {
            n.read = true;
            SyncBellDot();

            if (!string.IsNullOrEmpty(n.targetScreen) && ScreenManager.Instance != null)
                ScreenManager.Instance.Show(n.targetScreen);
            else
                Build();   // 이동하지 않으면 그 자리에서 점만 지운다
        }

        void SyncBellDot()
        {
            UiUtil.SetActive(bellDot, HasUnread);
            if (bellButton != null) bellButton.Set(HasUnread);
        }

        static void Hook(Button b, UnityEngine.Events.UnityAction a) { if (b != null) b.onClick.AddListener(a); }
        static void Unhook(Button b, UnityEngine.Events.UnityAction a) { if (b != null) b.onClick.RemoveListener(a); }
    }
}
