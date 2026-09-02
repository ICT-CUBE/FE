using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 미션 프리팹을 데이터 개수만큼 생성해 목록을 채운다.
    // 지금은 임시 데이터로 테스트. 나중에 서버 데이터로 교체.
    public class MissionListController : MonoBehaviour
    {
        // 카드 본체 탭 = 상세 시트, 버튼 탭 = 보상받기/바로가기
        public event Action<MissionData> CardClicked;
        public event Action<MissionData> ActionClicked;

        [SerializeField] GameObject cardPrefab;   // MissionCard 프리팹
        [SerializeField] Transform container;      // 카드들이 들어갈 부모
        [SerializeField] NotifyDot missionButton;  // 홈 미션 버튼 (점 포함 스프라이트 교체)

        [Header("버튼 스프라이트 (디자이너 완성본, 글자 포함)")]
        [SerializeField] Sprite btnClaim;   // 보상받기
        [SerializeField] Sprite btnDone;    // 완료됨
        [SerializeField] Sprite btnGo;      // 바로가기

        [Header("목록 아래로 따라올 것")]
        [SerializeField] RectTransform eventLabel;
        [SerializeField] RectTransform eventDeadline;   // '종료까지 N일 남음'
        [SerializeField] RectTransform eventCard;
        [SerializeField] RectTransform scrollContent;

        [Header("미션 아이콘 (완료 / 미완료)")]
        [SerializeField] Sprite icoVisitOn,   icoVisitOff;    // 홈 화면 방문
        [SerializeField] Sprite icoTalkOn,    icoTalkOff;     // 아바타와 대화
        [SerializeField] Sprite icoPlayOn,    icoPlayOff;     // 음원 재생
        [SerializeField] Sprite icoCameraOn,  icoCameraOff;   // 아바타 현실 소환
        [SerializeField] Sprite icoGalleryOn, icoGalleryOff;  // 갤러리 둘러보기
        [SerializeField] Sprite icoWriteOn,   icoWriteOff;    // 커뮤니티 글 업로드

        void Start()
        {
            // 2026-08-25: 와이어프레임 더미 → 서버(GET /users/me/missions) 로 대체.
            // 실패하면 화면이 비지 않게 기존 더미를 그대로 쓴다.
            ApiClient.Get("api/v1/users/me/missions", json =>
            {
                try
                {
                    var dto = JsonUtility.FromJson<MissionsDto>(json);
                    var list = new List<MissionData>();
                    AppendServer(list, dto != null ? dto.dailyMissions : null, "일일 미션");
                    AppendServer(list, dto != null ? dto.eventMissions : null, "이벤트 미션");
                    if (list.Count == 0) { PopulateFallback(); return; }
                    Debug.Log($"[Mission] 서버 미션 {list.Count}개");
                    Populate(list);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Api] 미션 파싱 실패: {e.Message}\n{json}");
                    PopulateFallback();
                }
            },
            err => { Debug.LogWarning($"[Mission] 목록 실패: {err} — 로컬 더미 유지"); PopulateFallback(); });
        }

        // 아이콘은 서버가 안 준다 — 씬에 물린 스프라이트를 제목으로 잇는다
        // (서버 시드 제목이 와이어프레임과 같다: "홈 화면 방문하기" 등).
        void AppendServer(List<MissionData> outList, MissionItemDto[] src, string typeLabel)
        {
            if (src == null) return;
            foreach (var d in src)
            {
                Sprite on = null, off = null;
                var t = d.title ?? "";
                if (t.Contains("홈 화면"))            { on = icoVisitOn;   off = icoVisitOff; }
                else if (t.Contains("대화"))          { on = icoTalkOn;    off = icoTalkOff; }
                else if (t.Contains("음원") || t.Contains("재생")) { on = icoPlayOn; off = icoPlayOff; }
                else if (t.Contains("소환"))          { on = icoCameraOn;  off = icoCameraOff; }
                else if (t.Contains("갤러리"))        { on = icoGalleryOn; off = icoGalleryOff; }
                else if (t.Contains("커뮤니티") || t.Contains("업로드")) { on = icoWriteOn; off = icoWriteOff; }

                int rewardCount = d.reward != null ? d.reward.rewardCount : 0;
                var m = Make(t, d.description ?? "", rewardCount, d.currentCount, d.targetCount, on, off);
                m.claimable = d.status == "COMPLETED" && !d.rewardClaimed;
                m.claimed = d.rewardClaimed;
                m.typeLabel = typeLabel;
                outList.Add(m);
            }
        }

        void PopulateFallback()
        {
            var missions = new List<MissionData>
            {
                Make("홈 화면 방문하기",       "AR ZONE 접속",        5,  1, 1, icoVisitOn,   icoVisitOff),
                Make("아바타와 대화하기",      "텍스트 또는 음성 대화", 3,  3, 3, icoTalkOn,    icoTalkOff),
                Make("음원 재생하기",          "앨범에서 노래 한 곡 재생", 5, 0, 1, icoPlayOn,  icoPlayOff),
                Make("아바타 현실 소환하기",    "AR 카메라 3회 사용",   30, 0, 3, icoCameraOn,  icoCameraOff),
                Make("갤러리 둘러보기",        "아무 사진 5회 열람",    20, 1, 5, icoGalleryOn, icoGalleryOff),
                Make("커뮤니티 글 업로드하기",  "게시물 1회 업로드",     10, 0, 1, icoWriteOn,   icoWriteOff),
            };
            missions[1].claimed = true;
            Populate(missions);
        }

        // 받을 보상이 남았거나 아직 못 끝낸 미션이 있으면 홈에 점을 띄운다.
        public bool HasPending
        {
            get
            {
                if (current == null) return false;
                foreach (var m in current)
                    if (m.claimable || m.current < m.total) return true;
                return false;
            }
        }

        List<MissionData> current;

        void OnDisable() => SyncDot();

        void SyncDot()
        {
            if (missionButton != null) missionButton.Set(HasPending);
        }

        // 보상을 받은 뒤 등 목록만 다시 그릴 때
        public void Refresh() { if (current != null) Populate(current); }

        // 진행이 다 찼으면 보상받기 가능
        static MissionData Make(string title, string desc, int reward, int cur, int total,
                                Sprite on, Sprite off)
        {
            var m = new MissionData(title, desc, reward, cur, total, cur >= total, on);
            m.iconOff = off;
            m.goScreen = GoScreenFor(title, desc);
            return m;
        }

        // '바로가기' / 상세 시트의 '미션 수행하러 가기' 대상 화면 (2026-08-26 배선).
        // 서버·더미 미션 제목 둘 다 이 키워드를 지난다(아이콘 매칭과 같은 방식).
        static string GoScreenFor(string title) => GoScreenFor(title, "");

        // 제목에서 못 찾으면 설명문까지 본다 — 서버 미션 문구가 더미와 달라도 걸리게 (2026-08-31).
        // '무대/공연'을 '재생'보다 먼저 봐야 "무대 5곡 시청" 이 앨범으로 새지 않는다.
        static string GoScreenFor(string title, string desc)
        {
            string r = Match(title ?? "");
            return !string.IsNullOrEmpty(r) ? r : Match(desc ?? "");

            static string Match(string t)
            {
                if (t.Contains("홈"))                              return "Home";
                if (t.Contains("대화"))                            return "TalkList";
                if (t.Contains("무대") || t.Contains("공연")
                    || t.Contains("시청"))                          return "StageList";
                if (t.Contains("음원") || t.Contains("음악")
                    || t.Contains("노래") || t.Contains("앨범")
                    || t.Contains("감상") || t.Contains("재생"))    return "Album";
                if (t.Contains("소환"))                            return "SummonScan";
                if (t.Contains("갤러리") || t.Contains("사진"))     return "Gallery";
                if (t.Contains("커뮤니티") || t.Contains("업로드")
                    || t.Contains("글"))                            return "Community";
                return "";
            }
        }

        public void Populate(List<MissionData> missions)
        {
            if (cardPrefab == null || container == null)
            {
                Debug.LogError("[MissionList] cardPrefab / container 연결 필요");
                return;
            }

            current = missions;
            UiUtil.ClearChildren(container);

            foreach (var m in missions)
            {
                var card = Instantiate(cardPrefab, container);
                card.SetActive(true);
                FillCard(card, m);

                var captured = m;

                // 카드 전체 = 상세 시트
                var cardImg = card.GetComponent<Image>();
                if (cardImg != null)
                {
                    var cb = cardImg.GetComponent<Button>() ?? cardImg.gameObject.AddComponent<Button>();
                    cb.onClick.AddListener(() => CardClicked?.Invoke(captured));
                }

                // 오른쪽 버튼 = 보상받기 / 바로가기
                var btnT = card.transform.Find("Button");
                if (btnT != null)
                {
                    var b = btnT.GetComponent<Button>();
                    if (b != null) b.onClick.AddListener(() => ActionClicked?.Invoke(captured));
                }
            }

            SyncDot();
            LayoutBelowList();
        }

        // 미션 개수가 시안(6개)과 다르면 이벤트 섹션이 붕 뜬다.
        // 시안 간격(목록 끝 → 라벨 31, → 카드 74.5)을 유지하며 따라오게 한다.
        void LayoutBelowList()
        {
            var list = container as RectTransform;
            if (list == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            float bottom = -list.anchoredPosition.y + list.rect.height;
            if (eventLabel != null)
                eventLabel.anchoredPosition = new Vector2(eventLabel.anchoredPosition.x, -(bottom + 31f));
            if (eventDeadline != null)
                eventDeadline.anchoredPosition = new Vector2(eventDeadline.anchoredPosition.x, -(bottom + 35f));
            if (eventCard != null)
                eventCard.anchoredPosition = new Vector2(eventCard.anchoredPosition.x, -(bottom + 74.5f));
            if (scrollContent != null && eventCard != null)
                scrollContent.sizeDelta = new Vector2(scrollContent.sizeDelta.x,
                                                      bottom + 74.5f + eventCard.rect.height + 20f);
        }

        // 카드 하나에 데이터 채우기 (자식 이름으로 찾음)
        void FillCard(GameObject card, MissionData m)
        {
            SetText(card, "MissionTitle", m.title);
            SetText(card, "MissionDesc", m.desc);
            SetText(card, "MissionReward", $"{m.reward}");   // 아이콘은 RewardIcon 이 담당
            SetText(card, "MissionProgress", $"{m.current}/{m.total}");

            // 와프: 링 + 글리프. 완료면 보라, 미완료면 회색.
            bool done = m.current >= m.total;
            var accent = done ? new Color(0.658824f, 0.333333f, 0.968627f, 1f)   // #A855F7
                              : new Color(0.447059f, 0.470588f, 0.525490f, 1f);  // neutral/400
            var ringT = card.transform.Find("Icon");
            if (ringT != null)
            {
                var ring = ringT.GetComponent<Image>();
                if (ring != null) ring.color = accent;

                var glyphT = ringT.Find("IconGlyph");
                var glyph = glyphT != null ? glyphT.GetComponent<Image>() : null;
                if (glyph != null)
                {
                    var s2 = done ? m.icon : (m.iconOff != null ? m.iconOff : m.icon);
                    if (s2 != null)
                    {
                        glyph.sprite = s2;
                        // on 아이콘은 색이 들어있어 흰색 틴트, off 는 흰 선이라 회색으로
                        glyph.color = done ? Color.white : accent;
                    }
                }
            }

            // 진행바는 데이터 비율만큼 왼쪽부터 채운다
            var fillT = card.transform.Find("ProgressTrack/ProgressFill");
            var fillImg = fillT != null ? fillT.GetComponent<Image>() : null;
            if (fillImg != null)
                fillImg.fillAmount = m.total > 0 ? Mathf.Clamp01(m.current / (float)m.total) : 0f;

            // 시안 3상태. 디자이너 스프라이트에 글자가 포함돼 있어 라벨은 끈다.
            var btn = card.transform.Find("Button");
            if (btn != null)
            {
                var img = btn.GetComponent<Image>();
                if (img != null)
                {
                    var spr = m.claimed ? btnDone : (m.claimable ? btnClaim : btnGo);
                    if (spr != null)
                    {
                        img.sprite = spr;
                        img.type = Image.Type.Simple;
                        img.color = Color.white;
                    }
                }
                var label = btn.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.gameObject.SetActive(false);
            }
        }

        static void SetText(GameObject card, string childName, string value)
        {
            var t = card.transform.Find(childName);
            if (t != null)
            {
                var txt = t.GetComponent<TMP_Text>();
                if (txt != null) txt.text = value;
            }
        }
    }
}