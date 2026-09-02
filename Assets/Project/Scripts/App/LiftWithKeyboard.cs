using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 키보드가 올라오면 이 오브젝트(와 targets)를 키보드 높이만큼 위로 올린다 (2026-08-31 유저 지시:
    // "입력 필드 하단에 있는 건 다 키보드와 같이 위로"). 하단 고정 입력바·바텀시트 루트에 붙인다.
    // 높이 측정은 TalkDetailController 에서 검증된 방식 그대로다(창의 보이는 영역으로 실측,
    // TouchScreenKeyboard.area 는 기기별로 엉터리라 폴백으로만).
    public class LiftWithKeyboard : MonoBehaviour
    {
        [Tooltip("비우면 이 오브젝트 자신만 올린다")]
        [SerializeField] List<RectTransform> targets = new List<RectTransform>();

        Canvas canvas;
        float lift;
        readonly Dictionary<RectTransform, Vector2> basePos = new Dictionary<RectTransform, Vector2>();

        void OnEnable()
        {
            basePos.Clear();
            lift = 0f;
            if (targets.Count == 0) targets.Add((RectTransform)transform);
            foreach (var t in targets)
                if (t != null) basePos[t] = t.anchoredPosition;
        }

        void OnDisable()
        {
            // 화면을 떠날 땐 원위치로 — 다음에 켜질 때 기준이 어긋나지 않게
            foreach (var kv in basePos)
                if (kv.Key != null) kv.Key.anchoredPosition = kv.Value;
        }

        void LateUpdate()
        {
            float h = KeyboardUnits();
            if (Mathf.Abs(h - lift) < 0.5f) return;
            lift = h;
            foreach (var kv in basePos)
                if (kv.Key != null) kv.Key.anchoredPosition = kv.Value + new Vector2(0f, h);
        }

        float KeyboardUnits()
        {
            float px;
#if UNITY_ANDROID && !UNITY_EDITOR
            px = AndroidKeyboardPx();
            if (px <= 1f && TouchScreenKeyboard.visible) px = TouchScreenKeyboard.area.height;
#else
            if (!TouchScreenKeyboard.visible) return 0f;
            px = TouchScreenKeyboard.area.height;
#endif
            // 내비게이션 바 잔여값(화면 15% 미만)은 키보드가 아니다. 60% 초과는 오측정.
            if (px < Screen.height * 0.15f) return 0f;
            float max = Screen.height * 0.6f;
            if (px > max) px = max;

            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            float sf = (canvas != null && canvas.scaleFactor > 0.001f) ? canvas.scaleFactor : 1f;
            return px / sf;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static float AndroidKeyboardPx()
        {
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var window = activity.Call<AndroidJavaObject>("getWindow");
                using var decor = window.Call<AndroidJavaObject>("getDecorView");
                using var rect = new AndroidJavaObject("android.graphics.Rect");
                decor.Call("getWindowVisibleDisplayFrame", rect);
                int visibleBottom = rect.Get<int>("bottom");
                int full = decor.Call<int>("getHeight");
                return Mathf.Max(0, full - visibleBottom);
            }
            catch (System.Exception)
            {
                return 0f;
            }
        }
#endif
    }
}
