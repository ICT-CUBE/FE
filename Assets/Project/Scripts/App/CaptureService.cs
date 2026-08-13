using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ARZone
{
    // AR 화면 캡처 → 앱 내부 저장 → (선택) 폰 갤러리 저장.
    // UI 를 한 프레임 숨기고 찍으므로 카메라 영상 + 무대만 담긴다.
    public class CaptureService : MonoBehaviour
    {
        public class Entry
        {
            public Texture2D Texture;
            public string FilePath;
            public DateTime TakenAt;
        }

        [SerializeField] Canvas uiCanvas;          // 캡처 순간 잠깐 끌 UI 캔버스
        [SerializeField] string folderName = "ARZONE";

        readonly List<Entry> captures = new List<Entry>();
        public IReadOnlyList<Entry> Captures => captures;
        public int Count => captures.Count;

        string SaveDir
        {
            get
            {
                var dir = Path.Combine(Application.persistentDataPath, folderName);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public void Capture(Action<Entry> onDone = null) => StartCoroutine(CaptureRoutine(onDone));

        IEnumerator CaptureRoutine(Action<Entry> onDone)
        {
            bool restored = uiCanvas != null && uiCanvas.enabled;
            if (uiCanvas != null) uiCanvas.enabled = false;

            yield return new WaitForEndOfFrame();

            Texture2D tex = null;
            try { tex = ScreenCapture.CaptureScreenshotAsTexture(); }
            catch (Exception e) { Debug.LogError("[Capture] 캡처 실패: " + e.Message); }

            if (uiCanvas != null) uiCanvas.enabled = restored;

            if (tex == null) { onDone?.Invoke(null); yield break; }

            var now = DateTime.Now;
            var name = $"ARZONE_{now:yyyyMMdd_HHmmss}.png";
            var path = Path.Combine(SaveDir, name);
            try { File.WriteAllBytes(path, tex.EncodeToPNG()); }
            catch (Exception e) { Debug.LogError("[Capture] 파일 저장 실패: " + e.Message); path = null; }

            var entry = new Entry { Texture = tex, FilePath = path, TakenAt = now };
            captures.Add(entry);
            onDone?.Invoke(entry);
        }

        // 폰 갤러리로 내보내기. Android 10+ 의 MediaStore 를 쓴다.
        public bool SaveToPhoneGallery(Entry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.FilePath) || !File.Exists(entry.FilePath))
            {
                Debug.LogWarning("[Capture] 갤러리에 저장할 파일이 없음");
                return false;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using var values = new AndroidJavaObject("android.content.ContentValues");
                values.Call("put", "_display_name", Path.GetFileName(entry.FilePath));
                values.Call("put", "mime_type", "image/png");
                values.Call("put", "relative_path", "Pictures/" + folderName);

                using var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media");
                using var external = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
                using var item = resolver.Call<AndroidJavaObject>("insert", external, values);
                if (item == null) { Debug.LogError("[Capture] MediaStore insert 실패"); return false; }

                using var os = resolver.Call<AndroidJavaObject>("openOutputStream", item);
                os.Call("write", File.ReadAllBytes(entry.FilePath));
                os.Call("flush");
                os.Call("close");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Capture] 갤러리 저장 실패: " + e.Message);
                return false;
            }
#else
            Debug.Log("[Capture] 에디터에서는 갤러리 저장을 건너뜀: " + entry.FilePath);
            return true;
#endif
        }

        public void Clear()
        {
            foreach (var e in captures)
                if (e.Texture != null) Destroy(e.Texture);
            captures.Clear();
        }
    }
}