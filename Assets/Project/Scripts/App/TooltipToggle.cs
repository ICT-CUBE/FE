using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace ARZone
{
    // '?' 아이콘에 붙여서, 누르면 설명 툴팁을 켜고 끈다.
    // 화면에 들어올 때는 항상 닫힌 상태로 시작하고,
    // 툴팁이나 툴팁 밖 아무 데나 누르면 닫힌다.
    //
    // 부모(IntimacyCard)에도 버튼이 붙어 있지만, 이 컴포넌트가
    // IPointerClickHandler 를 직접 구현하므로 클릭이 여기서 소비된다
    // → '?' 를 눌러도 화면 이동이 같이 일어나지 않는다.
    [RequireComponent(typeof(Image))]
    public class TooltipToggle : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] GameObject tooltip;          // 켜고 끌 대상
        [SerializeField] float autoHideSeconds = 0f;  // 0이면 자동으로 안 닫힘

        // 아이콘이 16x16 이라 손가락엔 좁다. 씬에서 Image 의 Raycast Padding 을
        // -8 로 넣어 탭 영역만 32x32 로 넓혔고, 여기 값도 그것과 맞춰야 한다.
        const float HitPadding = 8f;

        RectTransform selfRect;
        Canvas canvas;
        float hideAt;

        void Awake()
        {
            selfRect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
        }

        // 화면이 다시 켜질 때마다 닫힌 상태로 되돌린다.
        // (유니티가 저장할 때 켜둔 채로 남겨도 런타임에선 항상 꺼짐)
        void OnEnable() => Close();

        public void OnPointerClick(PointerEventData e)
        {
            if (tooltip == null) return;
            if (tooltip.activeSelf) Close();
            else Open();
        }

        void Open()
        {
            UiUtil.SetActive(tooltip, true);
            hideAt = autoHideSeconds > 0f ? Time.unscaledTime + autoHideSeconds : 0f;
        }

        void Close()
        {
            UiUtil.SetActive(tooltip, false);
            hideAt = 0f;
        }

        void Update()
        {
            if (tooltip == null || !tooltip.activeSelf) return;

            if (hideAt > 0f && Time.unscaledTime >= hideAt) { Close(); return; }

            // 툴팁 밖을 누르면 닫기. '?' 위를 누른 건 클릭 처리에 맡긴다.
            var p = Pointer.current;
            if (p == null || !p.press.wasPressedThisFrame) return;

            Vector2 pos = p.position.ReadValue();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;

            if (OverSelf(pos, cam)) return;

            Close();
        }

        // '?' 의 탭 영역(아이콘 + 패딩) 안인지.
        bool OverSelf(Vector2 pos, Camera cam)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(selfRect, pos, cam, out var local))
                return false;

            Rect r = selfRect.rect;
            r.xMin -= HitPadding; r.xMax += HitPadding;
            r.yMin -= HitPadding; r.yMax += HitPadding;
            return r.Contains(local);
        }
    }
}
