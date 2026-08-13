using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 온보딩 3장을 좌우로 넘기고 하단 점을 바꾼다.
    // 시안(온보딩 1~3): 활성 점 13x8 알약 #6FE8F5 / 비활성 8x8 #242831
    public class OnboardingSlider : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("페이지별 문구")]
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subText;
        [SerializeField] List<string> titles = new List<string>();
        [SerializeField] List<string> subs = new List<string>();

        [Header("페이지별 이미지 (없으면 비워둔다)")]
        [SerializeField] Image imageArea;
        [SerializeField] List<Sprite> images = new List<Sprite>();

        [Header("점 — 통짜 인디케이터 (시안 372x8 안에 점 3개가 다 있다)")]
        [SerializeField] Image dotBar;                       // Dots
        [SerializeField] List<Sprite> dotBarStates = new List<Sprite>();   // 1/2/3 페이지용

        [Header("점 — 개별 방식(안 쓰면 비워둔다)")]
        [SerializeField] List<Image> dots = new List<Image>();
        [SerializeField] Color dotOn = new Color(0.435294f, 0.909804f, 0.960784f, 1f); // #6FE8F5
        [SerializeField] Color dotOff = new Color(0.141176f, 0.156863f, 0.192157f, 1f); // #242831
        [SerializeField] Vector2 dotOnSize = new Vector2(13f, 8f);
        [SerializeField] Vector2 dotOffSize = new Vector2(8f, 8f);

        [Header("넘김 판정")]
        [SerializeField] float threshold = 60f;   // 이만큼 끌어야 페이지가 넘어간다

        int page;
        float startX;

        void OnEnable() { page = 0; Redraw(); }

        public void OnBeginDrag(PointerEventData e) => startX = e.position.x;
        public void OnDrag(PointerEventData e) { }

        public void OnEndDrag(PointerEventData e)
        {
            float dx = e.position.x - startX;
            if (Mathf.Abs(dx) < threshold) return;

            int last = Mathf.Max(0, PageCount - 1);
            page = Mathf.Clamp(page + (dx < 0 ? 1 : -1), 0, last);
            Redraw();
        }

        int PageCount
        {
            get
            {
                int n = dotBar != null ? dotBarStates.Count : dots.Count;
                if (titles.Count > n) n = titles.Count;
                return n;
            }
        }

        void Redraw()
        {
            if (title != null && page < titles.Count) title.text = titles[page];
            if (subText != null && page < subs.Count) subText.text = subs[page];
            if (imageArea != null && page < images.Count && images[page] != null)
                imageArea.sprite = images[page];

            // 통짜 인디케이터가 있으면 그것만 갈아끼운다
            if (dotBar != null && page < dotBarStates.Count && dotBarStates[page] != null)
            {
                dotBar.sprite = dotBarStates[page];
            }
            else
            {
                for (int i = 0; i < dots.Count; i++)
                {
                    if (dots[i] == null) continue;
                    bool on = i == page;
                    dots[i].color = on ? dotOn : dotOff;
                    var rt = dots[i].rectTransform;
                    rt.sizeDelta = on ? dotOnSize : dotOffSize;
                }
                Layout();
            }
        }

        // 점 크기가 달라지므로 간격을 다시 잡는다 (시안 x = 0 / 21 / 37 기준의 간격 8)
        void Layout()
        {
            const float gap = 8f;
            float x = 0f;
            for (int i = 0; i < dots.Count; i++)
            {
                if (dots[i] == null) continue;
                var rt = dots[i].rectTransform;
                rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
                x += rt.sizeDelta.x + gap;
            }
        }
    }
}
