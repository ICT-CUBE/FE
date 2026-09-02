using UnityEngine;

namespace ARZone
{
    // FPS 실측 프로브 (2026-08-31, 고폴리곤 아바타 성능 테스트용).
    // 5초마다 평균/최저 FPS 를 logcat 에 남긴다 — adb logcat 에서 "[FPS]" 로 걸러 본다.
    // 씬에 안 넣는다 — 앱 시작 때 스스로 떠서 화면 전환과 무관하게 계속 잰다.
    public class FpsProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("FpsProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<FpsProbe>();
            Debug.Log($"[FPS] GPU: {SystemInfo.graphicsDeviceName} / RAM {SystemInfo.systemMemorySize}MB / {SystemInfo.processorType}");
        }

        int frames;
        float elapsed;
        float worst = float.MaxValue;   // 구간 내 최악 프레임(순간 끊김 감지)

        void Update()
        {
            frames++;
            elapsed += Time.unscaledDeltaTime;
            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            if (fps < worst) worst = fps;

            if (elapsed >= 5f)
            {
                string screen = ScreenManager.Instance != null ? ScreenManager.Instance.Current : "?";
                Debug.Log($"[FPS] {screen} 평균 {frames / elapsed:0.0} 최저 {worst:0.0}");
                frames = 0; elapsed = 0f; worst = float.MaxValue;
            }
        }
    }
}
