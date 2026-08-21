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
        // CaptureScreenshotIntoRenderTexture 는 백버퍼를 그대로 넣어서 플랫폼에 따라 상하가 뒤집힌다.
        // 2026-08-18 실기(안드로이드)에서 뒤집혀 나와서 기본값을 끔으로 바꿨다.
        // ⚠ 씬에 이미 직렬화된 값이 코드 기본값을 이긴다 — 인스펙터에서도 꺼야 한다.
        [SerializeField] bool flipCapture = false;

        bool capturing;                            // 셔터 연타로 코루틴이 겹치는 것 방지

        readonly List<Entry> captures = new List<Entry>();
        public IReadOnlyList<Entry> Captures => captures;
        public int Count => captures.Count;

        // 촬영분은 PNG 로 디스크에 남는다. 메모리의 Texture2D 는 '캐시'일 뿐이다.
        //   - 앱이 백그라운드에 갔다 오면 안드로이드가 그래픽 컨텍스트를 날려서 런타임 생성
        //     텍스처가 하얗게 변한다(에셋이 아니라 유니티가 복구해 줄 원본이 없다).
        //   - 앱을 껐다 켜면 리스트가 비어 사진이 사라진 것처럼 보인다.
        // 둘 다 '파일이 원본'으로 두면 해결된다.
        void Awake() => LoadFromDisk();

        void OnApplicationPause(bool paused) { if (!paused) RefreshTextures(); }

        void OnApplicationFocus(bool focused) { if (focused) RefreshTextures(); }

        // 오늘 찍은 것만 되살린다 (화면 이름이 '오늘 찍은 사진들' · '오늘의 AR 소환 리포트' 다)
        void LoadFromDisk()
        {
            if (captures.Count > 0) return;
            string today = DateTime.Now.ToString("yyyyMMdd");
            string[] files;
            try { files = Directory.GetFiles(SaveDir, $"ARZONE_{today}_*.png"); }
            catch (Exception e) { Debug.LogWarning("[Capture] 저장 폴더를 못 읽음: " + e.Message); return; }

            Array.Sort(files, StringComparer.Ordinal);
            foreach (var f in files)
            {
                var e = new Entry { FilePath = f, TakenAt = File.GetLastWriteTime(f) };
                if (LoadInto(e)) captures.Add(e);
            }
            if (captures.Count > 0) Debug.Log($"[Capture] 디스크에서 {captures.Count}장 복원");
        }

        // 같은 Texture2D 객체에 다시 채운다 — 이미 물려 있는 RawImage 참조가 그대로 산다.
        void RefreshTextures()
        {
            foreach (var e in captures) LoadInto(e);
        }

        bool LoadInto(Entry e)
        {
            if (e == null || string.IsNullOrEmpty(e.FilePath) || !File.Exists(e.FilePath)) return false;
            try
            {
                if (e.Texture == null) e.Texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                return e.Texture.LoadImage(File.ReadAllBytes(e.FilePath));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Capture] {Path.GetFileName(e.FilePath)} 읽기 실패: {ex.Message}");
                return false;
            }
        }

        string SaveDir
        {
            get
            {
                var dir = Path.Combine(Application.persistentDataPath, folderName);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        // hide 를 주면 그것만 감춘다. 안 주면 UI 캔버스를 통째로 끈다.
        // ⚠ AR 소환처럼 **사진에 남아야 하는 UI(프레임)** 가 있으면 반드시 hide 를 줘야 한다.
        //   캔버스를 끄면 프레임까지 같이 꺼져서 사진에 안 씌워진다(2026-08-18 실기).
        public void Capture(Action<Entry> onDone = null, GameObject[] hide = null)
        {
            if (capturing) { Debug.Log("[Capture] 이미 촬영 중 — 무시"); return; }
            StartCoroutine(CaptureRoutine(onDone, hide));
        }

        IEnumerator CaptureRoutine(Action<Entry> onDone, GameObject[] hide)
        {
            capturing = true;

            bool useCanvas = hide == null || hide.Length == 0;
            bool restored = uiCanvas != null && uiCanvas.enabled;
            bool[] was = null;

            if (useCanvas)
            {
                if (uiCanvas != null) uiCanvas.enabled = false;
            }
            else
            {
                was = new bool[hide.Length];
                for (int i = 0; i < hide.Length; i++)
                {
                    if (hide[i] == null) continue;
                    was[i] = hide[i].activeSelf;
                    if (was[i]) hide[i].SetActive(false);
                }
            }

            yield return new WaitForEndOfFrame();

            var tex = Grab();

            if (useCanvas)
            {
                if (uiCanvas != null) uiCanvas.enabled = restored;
            }
            else
            {
                for (int i = 0; i < hide.Length; i++)
                    if (hide[i] != null && was[i]) hide[i].SetActive(true);
            }
            capturing = false;

            if (tex == null)
            {
                Debug.LogError("[Capture] 캡처 실패 — 텍스처를 못 만들었다");
                onDone?.Invoke(null);
                yield break;
            }

            // 파일명은 밀리초까지 — 초 단위면 연속 촬영 때 앞 파일을 덮어쓴다
            var now = DateTime.Now;
            var path = Path.Combine(SaveDir, $"ARZONE_{now:yyyyMMdd_HHmmss_fff}.png");
            try { File.WriteAllBytes(path, tex.EncodeToPNG()); }
            catch (Exception e) { Debug.LogError("[Capture] 파일 저장 실패: " + e.Message); path = null; }

            var entry = new Entry { Texture = tex, FilePath = path, TakenAt = now };
            captures.Add(entry);
            Debug.Log($"[Capture] {tex.width}x{tex.height} 저장 → {path} (총 {captures.Count}장)");
            onDone?.Invoke(entry);
        }

        // 화면을 Texture2D 로 가져온다.
        // ⚠ `ScreenCapture.CaptureScreenshotAsTexture()` 는 URP + AR 조합에서 null 을 돌려주는 기기가 있다
        //   (2026-08-18 실기: 0장 + 리포트에 자리표시자만 하얗게). 그래서 백버퍼를 RenderTexture 로 받아
        //   ReadPixels 하는 경로를 기본으로 쓰고, 그게 안 되면 예전 방식으로 한 번 더 시도한다.
        //   RT/텍스처를 둘 다 sRGB 로 두는 게 중요하다 — 이 프로젝트는 Linear 색공간이라
        //   여기를 어긋나게 잡으면 사진이 허옇게 뜬다.
        Texture2D Grab()
        {
            int w = Screen.width, h = Screen.height;
            if (w <= 0 || h <= 0) return null;

            RenderTexture shot = null, flipped = null;
            var prev = RenderTexture.active;
            try
            {
                shot = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(shot);

                var src = shot;
                if (flipCapture)
                {
                    flipped = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(shot, flipped, new Vector2(1f, -1f), new Vector2(0f, 1f));
                    src = flipped;
                }

                RenderTexture.active = src;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                return tex;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Capture] RenderTexture 경로 실패, 예전 방식으로 재시도: " + e.Message);
                try { return ScreenCapture.CaptureScreenshotAsTexture(); }
                catch (Exception e2) { Debug.LogError("[Capture] 둘 다 실패: " + e2.Message); return null; }
            }
            finally
            {
                RenderTexture.active = prev;
                if (flipped != null) RenderTexture.ReleaseTemporary(flipped);
                if (shot != null) RenderTexture.ReleaseTemporary(shot);
            }
        }

        bool Usable(Entry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.FilePath) || !File.Exists(entry.FilePath))
            {
                Debug.LogWarning("[Capture] 내보낼 파일이 없음");
                return false;
            }
            return true;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // 파일을 MediaStore 에 넣고 content:// Uri 를 돌려준다.
        // 공유(ACTION_SEND)도 content:// 가 있어야 한다 — file:// 는 API24+ 에서 예외가 난다.
        // FileProvider 를 쓰려면 매니페스트를 손대야 해서, 이미 있는 MediaStore 경로를 재사용한다.
        AndroidJavaObject InsertToMediaStore(Entry entry)
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
            var item = resolver.Call<AndroidJavaObject>("insert", external, values);
            if (item == null) { Debug.LogError("[Capture] MediaStore insert 실패"); return null; }

            using var os = resolver.Call<AndroidJavaObject>("openOutputStream", item);
            os.Call("write", File.ReadAllBytes(entry.FilePath));
            os.Call("flush");
            os.Call("close");
            return item;
        }
#endif

        // 폰 갤러리로 내보내기. Android 10+ 의 MediaStore 를 쓴다.
        public bool SaveToPhoneGallery(Entry entry)
        {
            if (!Usable(entry)) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var item = InsertToMediaStore(entry);
                return item != null;
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

        // 시안 '사진 상세' 좌측 버튼 — OS 기본 공유 시트를 띄운다 (ACTION_SEND).
        public bool ShareToOS(Entry entry)
        {
            if (!Usable(entry)) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var item = InsertToMediaStore(entry);
                if (item == null) return false;

                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var cls = new AndroidJavaClass("android.content.Intent");
                using var intent = new AndroidJavaObject("android.content.Intent");
                intent.Call<AndroidJavaObject>("setAction", cls.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "image/png");
                intent.Call<AndroidJavaObject>("putExtra", cls.GetStatic<string>("EXTRA_STREAM"), item);
                intent.Call<AndroidJavaObject>("addFlags", cls.GetStatic<int>("FLAG_GRANT_READ_URI_PERMISSION"));
                using var chooser = cls.CallStatic<AndroidJavaObject>("createChooser", intent, "공유");
                activity.Call("startActivity", chooser);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Capture] 공유 실패: " + e.Message);
                return false;
            }
#else
            Debug.Log("[Capture] 에디터에서는 OS 공유 시트를 건너뜀: " + entry.FilePath);
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