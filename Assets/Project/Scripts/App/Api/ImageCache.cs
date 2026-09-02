using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // URL → Texture2D. 갤러리 말고도 마켓·커뮤니티·홈이 전부 쓸 것이라 처음부터 공용으로 둔다.
    //
    // 두 가지를 한다:
    //   1) 같은 URL 을 두 번 안 받는다 (받아둔 것은 즉시 돌려준다)
    //   2) 같은 URL 이 동시에 여러 번 요청되면 요청은 한 번만 나가고 콜백을 모아뒀다 한꺼번에 부른다
    //      — 갤러리 타일 20개가 같은 앨범 자켓을 쓰면 요청 20개가 나가는 걸 막는다.
    //
    // ⚠ 런타임 Texture2D 는 안드로이드가 백그라운드에서 그래픽 컨텍스트를 날리면 하얘진다.
    //   여기 것들은 '원본이 서버' 라 다시 받으면 되지만, 그러려면 캐시를 비워야 한다
    //   → Clear() 를 열어뒀다. (로컬 캡처는 CaptureService 가 파일을 원본으로 들고 있다.)
    public static class ImageCache
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, List<Action<Texture2D>>> waiting =
            new Dictionary<string, List<Action<Texture2D>>>();

        // 받은 적 있으면 그 자리에서 돌려준다(없으면 null). 레이아웃을 미리 잡을 때 쓴다.
        public static Texture2D Peek(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            return cache.TryGetValue(url, out var t) ? t : null;
        }

        // 실패하면 done(null) 을 부른다 — 호출부가 자리표시자를 유지할 수 있게.
        public static void Load(string url, Action<Texture2D> done)
        {
            if (done == null) return;

            if (string.IsNullOrEmpty(url)) { done(null); return; }

            if (cache.TryGetValue(url, out var hit)) { done(hit); return; }

            // 이미 나가 있는 요청이면 줄만 선다.
            if (waiting.TryGetValue(url, out var queue)) { queue.Add(done); return; }

            waiting[url] = new List<Action<Texture2D>> { done };

            ApiClient.GetTexture(url,
                tex =>
                {
                    cache[url] = tex;
                    Flush(url, tex);
                },
                _ =>
                {
                    // 실패는 캐시하지 않는다 — 다음에 다시 시도할 수 있게.
                    Flush(url, null);
                });
        }

        static void Flush(string url, Texture2D tex)
        {
            if (!waiting.TryGetValue(url, out var queue)) return;
            waiting.Remove(url);
            foreach (var cb in queue) cb?.Invoke(tex);
        }

        public static void Clear()
        {
            cache.Clear();
            waiting.Clear();
        }
    }
}
