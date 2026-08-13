using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ARZone
{
    // 상품 사진 좌우 스와이프 + 아래 인디케이터 (시안 '판매 중 큐브 상세').
    //
    // 시안 인디케이터 실측: 활성 (179.5,386) 13×8 알약 #6FE8F5 · 나머지 8×8 원 #242831,
    //   x 179.5 부터 간격 8 로 이어 붙는다(총 폭 45). 활성이 몇 번째든 총 폭은 같다.
    //   8 높이 알약/원은 `bg_pill8`(10×8 rx4, 9슬라이스) 하나로 둘 다 그린다.
    [RequireComponent(typeof(Image))]
    public class ImageCarousel : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] Image target;              // 비우면 자기 자신
        [SerializeField] Sprite[] photos;
        [SerializeField] RectTransform[] dots;

        [Header("인디케이터 (시안 실측)")]
        [SerializeField] float dotY = 386f;
        [SerializeField] float dotStartX = 179.5f;
        [SerializeField] float dotGap = 8f;
        [SerializeField] float dotSize = 8f;
        [SerializeField] float dotActiveW = 13f;
        [SerializeField] float swipeThreshold = 40f;   // 캔버스 단위

        static readonly Color On  = new Color32(0x6F, 0xE8, 0xF5, 0xFF);
        static readonly Color Off = new Color32(0x24, 0x28, 0x31, 0xFF);

        int index;
        float startX;

        void OnEnable()
        {
            if (target == null) target = GetComponent<Image>();
            index = 0;
            Apply();
        }

        public void OnBeginDrag(PointerEventData e) { startX = e.position.x; }

        public void OnEndDrag(PointerEventData e)
        {
            if (photos == null || photos.Length <= 1) return;
            // 화면 해상도가 캔버스 기준(412)과 다르므로 캔버스 단위로 환산해서 잰다
            float scale = Screen.width > 0 ? 412f / Screen.width : 1f;
            float dx = (e.position.x - startX) * scale;
            if (Mathf.Abs(dx) < swipeThreshold) return;
            index = Mathf.Clamp(index + (dx < 0 ? 1 : -1), 0, photos.Length - 1);
            Apply();
        }

        void Apply()
        {
            if (target != null && photos != null && photos.Length > 0)
            {
                var s = photos[Mathf.Clamp(index, 0, photos.Length - 1)];
                if (s != null) target.sprite = s;
            }
            if (dots == null) return;

            float x = dotStartX;
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                bool on = i == index;
                float w = on ? dotActiveW : dotSize;
                UiUtil.Place(dots[i], x, dotY, w, dotSize);
                var img = dots[i].GetComponent<Image>();
                if (img != null) img.color = on ? On : Off;
                x += w + dotGap;
            }
        }
    }
}
