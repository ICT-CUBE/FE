using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // Stage 목록 화면. 큰 카드 + 아래 목록을 데이터로 채우고, MV/콘서트 탭을 전환한다.
    public class StageListController : MonoBehaviour
    {
        [Header("큰 카드")]
        [SerializeField] Button featuredCard;      // FeaturedCard (Button 필요)
        [SerializeField] Image featuredThumb;
        [SerializeField] TMP_Text featuredTitle;
        [SerializeField] TMP_Text featuredSub;

        [Header("목록")]
        [SerializeField] GameObject rowPrefab;     // VideoRow 프리팹
        [SerializeField] Transform container;      // VideoScroll/Viewport/Content

        [Header("탭")]
        [SerializeField] Button tabMV;
        [SerializeField] Button tabConcert;
        [SerializeField] Image tabMVBg;
        [SerializeField] Image tabConcertBg;
        [SerializeField] Color tabOn = Color.white;
        [SerializeField] Color tabOff = new Color(1f, 1f, 1f, 0.08f);
        // 탭은 색이 아니라 스프라이트가 상태를 표현한다 (tab_pill_on=보라 / tab_pill_off=어두움).
        // 색만 바꾸던 예전 방식은 콘서트 선택 시 보라 알약이 사라졌다 (2026-08-31 실기).
        [SerializeField] Sprite pillOn;
        [SerializeField] Sprite pillOff;

        [SerializeField] string detailScreen = "StageDetail";

        StageCategory current = StageCategory.MusicVideo;

        void OnEnable()
        {
            if (tabMV != null) tabMV.onClick.AddListener(ShowMV);
            if (tabConcert != null) tabConcert.onClick.AddListener(ShowConcert);
            if (featuredCard != null) featuredCard.onClick.AddListener(OpenFeatured);
            // 2026-08-30: 목록을 서버(썸네일·제목·설명·연도)로 채운다. 실패하면 로컬 그대로.
            if (StageCatalog.Instance != null)
            {
                StageCatalog.Instance.OnChanged += Refresh;
                StageCatalog.Instance.LoadFromServer();
            }
            Refresh();
        }

        void OnDisable()
        {
            if (tabMV != null) tabMV.onClick.RemoveListener(ShowMV);
            if (tabConcert != null) tabConcert.onClick.RemoveListener(ShowConcert);
            if (featuredCard != null) featuredCard.onClick.RemoveListener(OpenFeatured);
            if (StageCatalog.Instance != null) StageCatalog.Instance.OnChanged -= Refresh;
        }

        void ShowMV() { current = StageCategory.MusicVideo; Refresh(); }
        void ShowConcert() { current = StageCategory.Concert; Refresh(); }

        void Refresh()
        {
            if (StageCatalog.Instance == null)
            {
                Debug.LogError("[StageList] StageCatalog 이 씬에 없음");
                return;
            }

            bool mv = current == StageCategory.MusicVideo;
            if (pillOn != null && pillOff != null)
            {
                if (tabMVBg != null) { tabMVBg.sprite = mv ? pillOn : pillOff; tabMVBg.color = Color.white; }
                if (tabConcertBg != null) { tabConcertBg.sprite = mv ? pillOff : pillOn; tabConcertBg.color = Color.white; }
            }
            else
            {
                if (tabMVBg != null) tabMVBg.color = mv ? tabOn : tabOff;
                if (tabConcertBg != null) tabConcertBg.color = mv ? tabOff : tabOn;
            }

            var list = StageCatalog.Instance.Filter(current);

            // 큰 카드 = featured 표시된 첫 항목, 없으면 목록 첫 항목
            StageData top = null;
            foreach (var s in list) if (s.featured) { top = s; break; }
            if (top == null && list.Count > 0) top = list[0];

            if (top != null)
            {
                if (featuredThumb != null && top.thumb != null) featuredThumb.sprite = top.thumb;
                if (featuredTitle != null)
                {
                    featuredTitle.text = top.title;
                    featuredTitle.textWrappingMode = TextWrappingModes.NoWrap;
                    featuredTitle.overflowMode = TextOverflowModes.Ellipsis;
                }
                if (featuredSub != null) featuredSub.text = top.sub;
            }
            if (featuredCard != null) featuredCard.gameObject.SetActive(top != null);

            BuildRows(list, top);
        }

        void BuildRows(List<StageData> list, StageData skip)
        {
            if (rowPrefab == null || container == null)
            {
                Debug.LogError("[StageList] rowPrefab / container 연결 필요");
                return;
            }

            EnsureLayout(container, 36f);   // 시안: 행 높이 80, 다음 행까지 116 → 간격 36

            UiUtil.ClearChildren(container);

            foreach (var s in list)
            {
                if (s == skip) continue;    // 큰 카드로 이미 보여준 항목은 목록에서 제외

                var row = Instantiate(rowPrefab, container);
                row.SetActive(true);

                // 2026-08-30 유저 지시: 긴 제목도 한 줄 + 말줄임 (두 줄로 감기지 않게)
                Set<TMP_Text>(row, "Title", t =>
                {
                    t.text = s.title;
                    t.textWrappingMode = TextWrappingModes.NoWrap;
                    t.overflowMode = TextOverflowModes.Ellipsis;
                });
                Set<TMP_Text>(row, "Desc", t => t.text = s.sub);
                Set<TMP_Text>(row, "Date", t => t.text = s.date);
                if (s.thumb != null) Set<Image>(row, "Thumb", i => i.sprite = s.thumb);

                // 시안: 제목+부제 그룹이 행(80) 세로 중앙. 제목이 2줄이면 부제가 밀린다.

                UiUtil.CenterTitleDesc(row, "Title", "Desc", 14.5f);


                var btn = row.GetComponent<Button>();
                if (btn == null) btn = row.AddComponent<Button>();
                var captured = s;
                btn.onClick.AddListener(() => Open(captured));
            }
        }

        void OpenFeatured()
        {
            if (StageCatalog.Instance == null) return;
            var list = StageCatalog.Instance.Filter(current);
            StageData top = null;
            foreach (var s in list) if (s.featured) { top = s; break; }
            if (top == null && list.Count > 0) top = list[0];
            if (top != null) Open(top);
        }

        void Open(StageData s)
        {
            StageCatalog.Instance.Select(s);
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(detailScreen);
        }

        // Content 에 레이아웃이 없으면 코드로 붙인다. (씬에서 비워둔 상태)
        static void EnsureLayout(Transform content, float spacing)
        {
            if (content == null) return;

            var v = content.GetComponent<VerticalLayoutGroup>();
            if (v == null) v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlWidth = true;
            v.childControlHeight = false;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperCenter;

            var f = content.GetComponent<ContentSizeFitter>();
            if (f == null) f = content.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        static void Set<T>(GameObject root, string child, System.Action<T> apply) where T : Component
        {
            var t = root.transform.Find(child);
            if (t == null) return;
            var c = t.GetComponent<T>();
            if (c != null) apply(c);
        }
    }
}