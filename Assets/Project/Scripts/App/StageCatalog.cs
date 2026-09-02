using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 스테이지 목록 보관소 + 현재 선택된 항목.
    // 지금은 인스펙터에 넣은 임시 데이터. 백엔드 API 나오면 Load() 만 갈아끼우면 된다.
    public class StageCatalog : MonoBehaviour
    {
        static StageCatalog instance;

        // Awake 순서는 오브젝트 간에 보장되지 않는다.
        // 다른 컴포넌트가 OnEnable 에서 부르면 아직 null 일 수 있어, 없으면 직접 찾는다.
        public static StageCatalog Instance
        {
            get
            {
                if (instance == null)
                    instance = FindAnyObjectByType<StageCatalog>(FindObjectsInactive.Include);
                return instance;
            }
            private set => instance = value;
        }

        [SerializeField] List<StageData> stages = new List<StageData>();

        public IReadOnlyList<StageData> All => stages;
        public StageData Selected { get; private set; }

        // 서버 목록 적용·썸네일 도착 때 알린다 — 목록 화면이 구독해 다시 그린다
        public event System.Action OnChanged;

        // 인스펙터에 넣어둔 로컬 목록 원본. 서버 목록으로 갈아탄 뒤에도
        // 재생 클립·썸네일 폴백은 여기서 제목 대조로 이어붙인다(클립은 서버가 안 준다).
        List<StageData> localSeed;
        List<StageData> svMv, svConcert;   // videoType 별 서버 목록 (null = 아직/실패 → 로컬 유지)

        void Awake()
        {
            Instance = this;
            localSeed = new List<StageData>(stages);
        }

        // 표시 목록을 서버로 갈아끼운다 (2026-08-30 유저 지시 — 하드코딩 대신 서버).
        // 서버가 videoType(MUSIC_VIDEO/CONCERT_VIDEO)으로 나눠 주므로 탭 분류도 서버 기준이다.
        // 실패(미로그인 포함)한 쪽 탭은 로컬 목록 그대로 — 데모가 서버에 안 죽는다.
        public void LoadFromServer()
        {
            ApiMediaSource.LoadStages(false, items => Apply(items, StageCategory.MusicVideo));
            ApiMediaSource.LoadStages(true,  items => Apply(items, StageCategory.Concert));
        }

        void Apply(List<StageListItemDto> items, StageCategory cat)
        {
            if (items == null) return;   // 이 탭은 로컬 유지
            var l = new List<StageData>(items.Count);
            foreach (var it in items)
            {
                StageData local = null;
                foreach (var s in localSeed)
                    if (ApiMediaSource.TitleMatch(s.title, it.title)) { local = s; break; }

                var d = new StageData
                {
                    title = it.title,
                    // 시안 포맷("Ruby · 2025", "Single · 2026")은 로컬 시드 sub 에 있다 — 그게 우선.
                    // 서버 description 은 "…뮤직비디오" 같은 설명문이라 시안과 다르다 (2026-08-30 유저 지시).
                    sub = local != null && !string.IsNullOrEmpty(local.sub) ? local.sub
                          : SubOf(it, cat),
                    date = FmtDate(it.recordedAt, local != null ? local.date : ""),
                    category = cat,
                    thumb = local != null ? local.thumb : null,   // 서버 썸네일 오면 교체
                    clip = local != null ? local.clip : null,     // 없으면 서버 스트리밍 전용
                    featured = it.isTitleVideo,
                };
                l.Add(d);

                if (!string.IsNullOrEmpty(it.thumbnailUrl) && it.thumbnailUrl.StartsWith("http"))
                    ImageCache.Load(it.thumbnailUrl, tex =>
                    {
                        if (tex == null) return;
                        d.thumb = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                                new Vector2(0.5f, 0.5f));
                        OnChanged?.Invoke();
                    });
            }
            if (cat == StageCategory.Concert) svConcert = l; else svMv = l;
            Rebuild();
        }

        // 탭별로 서버 목록이 있으면 서버, 없으면 로컬 시드로 합쳐 표시 목록을 만든다
        void Rebuild()
        {
            var fresh = new List<StageData>();
            fresh.AddRange(svMv ?? SeedOf(StageCategory.MusicVideo));
            fresh.AddRange(svConcert ?? SeedOf(StageCategory.Concert));
            stages = fresh;
            if (Selected != null)
                foreach (var s in fresh)
                    if (ApiMediaSource.TitleMatch(s.title, Selected.title)) { Selected = s; break; }
            Debug.Log($"[Stage] 목록 갱신 — 뮤비 {(svMv != null ? "서버" : "로컬")} · " +
                      $"콘서트 {(svConcert != null ? "서버" : "로컬")} · 총 {fresh.Count}개");
            OnChanged?.Invoke();
        }

        List<StageData> SeedOf(StageCategory cat)
        {
            var r = new List<StageData>();
            foreach (var s in localSeed) if ((s.category & cat) != 0) r.Add(s);
            return r;
        }

        // 로컬 시드에 없는 서버 전용 곡의 부제.
        // 뮤비는 전부 Ruby 앨범 수록곡이라 "Ruby · 연도"(시드와 같은 표기), 콘서트는 서버 설명 그대로.
        // 앨범명이 서버에 없어서 만든 규칙이다 — 틀린 곡이 있으면 시드에 추가해 덮으면 된다.
        static string SubOf(StageListItemDto it, StageCategory cat)
        {
            string year = FmtYear(it.recordedAt);
            if (cat == StageCategory.MusicVideo && !string.IsNullOrEmpty(year))
                return $"Ruby · {year}";
            return it.description ?? "";
        }

        static string FmtYear(string iso)
        {
            return !string.IsNullOrEmpty(iso)
                   && System.DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
                                               System.Globalization.DateTimeStyles.None, out var t)
                ? t.Year.ToString() : "";
        }

        // "2024-10-11T13:00:00" → "2024. 10. 11" (씬 표기와 동일)
        static string FmtDate(string iso, string fallback)
        {
            if (!string.IsNullOrEmpty(iso)
                && System.DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
                                            System.Globalization.DateTimeStyles.None, out var t))
                return t.ToString("yyyy. MM. dd");
            return fallback;
        }

        public void Select(StageData s) => Selected = s;

        // 플래그라 한 무대가 여러 탭에 들어갈 수 있다
        public List<StageData> Filter(StageCategory c)
        {
            var r = new List<StageData>();
            foreach (var s in stages) if ((s.category & c) != 0) r.Add(s);
            return r;
        }

        // 같은 카테고리의 다른 항목들 (상세의 '추천 동영상')
        public List<StageData> Recommend(StageData except, int max = 4)
        {
            var r = new List<StageData>();
            foreach (var s in stages)
            {
                if (s == except) continue;
                r.Add(s);
                if (r.Count >= max) break;
            }
            return r;
        }
    }
}