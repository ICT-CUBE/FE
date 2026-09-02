using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 서버로 도는 갤러리 구현. ApiClient 로 보내고 DTO 를 앱 모델로 옮긴다.
    // 여기 말고는 어디에도 갤러리 URL 이 없어야 한다.
    public class ApiGallerySource : IGallerySource
    {
        const string Root = "api/v1/users/me/gallery";

        public void LoadItems(GalleryCategory category, int page, int size,
                              Action<List<GalleryItem>, bool> done, Action<string> fail)
        {
            string url = $"{Root}?category={GalleryItem.ToServerCategory(category)}&page={page}&size={size}";

            ApiClient.Get(url, json =>
            {
                GalleryResponseDto dto;
                try
                {
                    dto = JsonUtility.FromJson<GalleryResponseDto>(json);
                }
                catch (Exception e)
                {
                    // 서버가 200 을 줬는데 우리가 못 읽은 것 — 앱 쪽 문제다.
                    // 원문을 남겨야 어디가 어긋났는지 대조할 수 있다.
                    Debug.LogError($"[Api] 갤러리 응답 파싱 실패: {e.Message}\n{json}");
                    fail?.Invoke("응답을 읽지 못했어요");
                    return;
                }

                var list = new List<GalleryItem>();
                if (dto != null && dto.galleryItems != null)
                {
                    foreach (var d in dto.galleryItems)
                    {
                        var t = GalleryItem.ParseSourceType(d.sourceType);
                        if (t == GallerySourceType.Unknown)
                        {
                            // 서버가 새 타입을 추가한 경우. 그리다가 죽느니 건너뛰고 알린다.
                            Debug.LogWarning($"[Api] 모르는 sourceType '{d.sourceType}' (id={d.id}) — 건너뜀");
                            continue;
                        }
                        list.Add(new GalleryItem
                        {
                            id = d.id,
                            sourceType = t,
                            category = GalleryItem.ParseCategory(d.category),
                            imageUrl = d.imageUrl,
                            displayDate = d.displayDate,
                        });
                    }
                }

                bool hasNext = dto != null && dto.pagination != null && dto.pagination.hasNext;
                Debug.Log($"[Api] 갤러리 {list.Count}건 (category={category}, hasNext={hasNext})");
                done?.Invoke(list, hasNext);
            }, fail);
        }

        public void DeleteItem(GalleryItem item, Action done, Action<string> fail)
        {
            if (item == null) { fail?.Invoke("항목이 없어요"); return; }

            // 서버는 ALBUM_IMAGE 삭제를 400 으로 막는다(GalleryDeletableSourceType 에 없다).
            // ★ 그래도 여기서 미리 걸러내지 않는다 — 유저 결정으로 화면은 시안대로 가고,
            //   실패는 ApiClient 가 [Api] 로그에 남긴다(설계서 7-3-1).
            string url = $"{Root}/{item.ServerSourceType}/{item.id}";
            ApiClient.Delete(url, _ => done?.Invoke(), fail);
        }

        public void LoadArExperience(GalleryItem item,
                                     Action<GalleryArExperienceItemDto> done, Action<string> fail)
        {
            if (item == null) { fail?.Invoke("항목이 없어요"); return; }

            string url = $"{Root}/{item.ServerSourceType}/{item.id}/ar-experience";
            ApiClient.Get(url, json =>
            {
                try
                {
                    var dto = JsonUtility.FromJson<GalleryArExperienceDto>(json);
                    if (dto == null || dto.galleryItem == null)
                    {
                        fail?.Invoke("응답을 읽지 못했어요");
                        return;
                    }
                    done?.Invoke(dto.galleryItem);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Api] AR재경험 응답 파싱 실패: {e.Message}\n{json}");
                    fail?.Invoke("응답을 읽지 못했어요");
                }
            }, fail);
        }
    }
}
