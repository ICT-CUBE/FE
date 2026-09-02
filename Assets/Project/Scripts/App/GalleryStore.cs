using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 현재 소스를 들고, 갤러리 목록을 한 곳에 모아둔다.
    // CommunityStore 와 같은 구조다 — 목록이 바뀌면 OnChanged 로 알린다.
    //
    // 붙이는 곳: Canvas/ScreenManager (CommunityStore 옆).
    public class GalleryStore : MonoBehaviour
    {
        public enum SourceKind { Api, Mock }

        [Header("데이터 출처")]
        [SerializeField] SourceKind source = SourceKind.Api;

        [Tooltip("Mock 일 때 쓸 더미 사진. Api 면 안 쓴다.")]
        [SerializeField] Sprite[] mockPhotos;

        // 시안에 '더 보기' 버튼도 로딩 표시도 없다 → 한 번에 전량을 받는다(설계서 9-3).
        // 사진이 이보다 많아지면 hasNext 가 true 로 오고 경고를 남긴다.
        [SerializeField] int pageSize = 100;

        public static GalleryStore Instance { get; private set; }

        public event Action OnChanged;

        IGallerySource src;
        readonly List<GalleryItem> cache = new List<GalleryItem>();

        public List<GalleryItem> Items => cache;
        public GalleryCategory Category { get; private set; } = GalleryCategory.All;
        public bool Loading { get; private set; }
        public string LastError { get; private set; }

        void Awake()
        {
            Instance = this;
            src = source == SourceKind.Mock
                ? (IGallerySource)new MockGallerySource(mockPhotos)
                : new ApiGallerySource();
            Debug.Log($"[Gallery] 소스 = {source}");
        }

        public void Load(GalleryCategory category)
        {
            Category = category;
            Loading = true;
            LastError = null;

            src.LoadItems(category, 0, pageSize, (list, hasNext) =>
            {
                Loading = false;
                cache.Clear();
                cache.AddRange(list);

                if (hasNext)
                    Debug.LogWarning($"[Gallery] pageSize({pageSize})를 넘는 사진이 있다 — " +
                                     "전량 방식이라 뒷장이 안 보인다. 페이징이 필요해진 시점.");

                OnChanged?.Invoke();
            },
            err =>
            {
                Loading = false;
                LastError = err;
                // ★ 실패해도 cache 를 비우지 않는다. 빈 화면보다 직전 목록이 낫다.
                Debug.LogWarning($"[Gallery] 목록 실패: {err} (직전 {cache.Count}건 유지)");
                Toast.Show(err);
                OnChanged?.Invoke();
            });
        }

        // 여러 장 삭제. 서버에 일괄 삭제가 없어 한 장씩 보내고 전부 끝나면 done 을 부른다.
        //
        // ★ 화면은 시안대로 간다(설계서 7-3-1) — 실패한 것도 목록에서 뺀다.
        //   앨범 사진은 서버가 400 으로 막지만 데모에서 밟지 않는 경로라 유저가 그렇게 정했다.
        //   진실은 ApiClient 의 [Api] 로그에 남고, 갤러리에 다시 들어오면 서버 기준으로 복구된다.
        public void Delete(List<GalleryItem> targets, Action done)
        {
            if (targets == null || targets.Count == 0) { done?.Invoke(); return; }

            int remain = targets.Count;
            int failed = 0;

            foreach (var item in targets)
            {
                src.DeleteItem(item,
                    () => Step(false),
                    _  => Step(true));
            }

            void Step(bool isFail)
            {
                if (isFail) failed++;
                if (--remain > 0) return;

                if (failed > 0)
                    Debug.LogWarning($"[Gallery] 삭제 {targets.Count}건 중 {failed}건 실패 " +
                                     "(화면은 시안대로 전부 지운 것처럼 보인다 — 재진입하면 서버 기준으로 복구된다)");

                foreach (var item in targets) cache.RemoveAll(x => x.Key == item.Key);
                OnChanged?.Invoke();
                done?.Invoke();
            }
        }

        public void LoadArExperience(GalleryItem item,
                                     Action<GalleryArExperienceItemDto> done, Action<string> fail)
            => src.LoadArExperience(item, done, fail ?? (err => Debug.LogWarning("[Gallery] AR재경험 실패: " + err)));
    }
}
