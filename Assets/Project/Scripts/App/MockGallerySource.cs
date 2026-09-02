using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 서버 없이 도는 메모리 구현. MockCommunitySource 와 같은 관례 —
    // 모든 콜백을 즉시 부르고 fail 은 절대 부르지 않는다.
    //
    // 쓰는 자리: 서버가 내려갔거나 비행기에서 화면 작업을 이어갈 때.
    // GalleryStore.source 를 Mock 으로 돌려도 화면이 똑같이 돌아야 한다 —
    // 여기서 깨지면 화면 코드가 서버를 알고 있다는 뜻이고, 그건 설계가 샌 것이다.
    public class MockGallerySource : IGallerySource
    {
        readonly List<GalleryItem> items = new List<GalleryItem>();

        // 씬에 넣어둔 더미 스프라이트로 목록을 만든다.
        // 서버 응답과 같은 구성이 되게 캡처 3종 → 앨범 → 스틸컷 순으로 타입을 돌린다.
        public MockGallerySource(Sprite[] sprites)
        {
            if (sprites == null) return;

            var order = new[]
            {
                GallerySourceType.StageCapture,
                GallerySourceType.TrackCapture,
                GallerySourceType.InteractionCapture,
                GallerySourceType.AlbumImage,
            };

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                var t = order[i % order.Length];
                items.Add(new GalleryItem
                {
                    id = i + 1,
                    sourceType = t,
                    category = t == GallerySourceType.AlbumImage
                        ? (i % (order.Length * 2) == 3 ? GalleryCategory.Album : GalleryCategory.Concert)
                        : GalleryCategory.Capture,
                    imageUrl = "",                    // Mock 은 파일에서 바로 텍스처를 준다
                    displayDate = "2026-08-07",
                    texture = sprites[i].texture,
                });
            }
        }

        public void LoadItems(GalleryCategory category, int page, int size,
                              Action<List<GalleryItem>, bool> done, Action<string> fail)
        {
            var list = new List<GalleryItem>();
            foreach (var it in items)
                if (category == GalleryCategory.All || it.category == category) list.Add(it);

            // 페이징도 흉내 낸다 — 호출부가 Mock 을 '동기이고 전량' 이라고 가정하면
            // 서버로 바꿨을 때 깨진다.
            int from = Mathf.Max(0, page * size);
            int count = Mathf.Clamp(list.Count - from, 0, size);
            var pageList = count > 0 ? list.GetRange(from, count) : new List<GalleryItem>();
            bool hasNext = from + count < list.Count;

            done?.Invoke(pageList, hasNext);
        }

        public void DeleteItem(GalleryItem item, Action done, Action<string> fail)
        {
            if (item != null) items.RemoveAll(x => x.Key == item.Key);
            done?.Invoke();
        }

        public void LoadArExperience(GalleryItem item,
                                     Action<GalleryArExperienceItemDto> done, Action<string> fail)
        {
            done?.Invoke(new GalleryArExperienceItemDto
            {
                contentType = "STAGE",
                requiredMode = "AR",
                startPositionSeconds = 0,
                stage = new GalleryStageDto { id = 1, title = "Mock Stage", videoUrl = "", durationSeconds = 210 },
            });
        }
    }
}
