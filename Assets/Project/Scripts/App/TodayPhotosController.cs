using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 오늘 찍은 사진들. 갤러리와 달리 시안이 2열 고정 격자다(178×267, 간격 16, 첫 행 y=126).
    // 선택 모드 3단계: 선택 → N개 선택됨 → 저장.
    public class TodayPhotosController : MonoBehaviour
    {
        [Header("상단")]
        [SerializeField] TMP_Text navTitle;
        [SerializeField] TMP_Text btnActionLabel;    // 선택 / 취소
        [SerializeField] Button btnAction;
        [SerializeField] Button btnBack;

        [Header("격자 (시안 실측)")]
        [SerializeField] RectTransform content;
        [SerializeField] float tileW = 178f;
        [SerializeField] float tileH = 267f;
        [SerializeField] float gap = 16f;
        [SerializeField] Sprite checkOff;
        [SerializeField] Sprite checkOn;
        // 시안 사진 카드는 모서리 r8 이다 (path M20 134C20 129.582 …).
        // RawImage 는 스프라이트로 못 깎으므로 이 스프라이트를 Mask 로 씌워 오려낸다.
        [SerializeField] Sprite tileMask;

        [Header("저장")]
        [SerializeField] GameObject sheetSave;
        [SerializeField] CaptureService capture;
        [SerializeField] ARMediaController media;    // 사진 상세(Screen_CaptureDetail) 를 여는 쪽

        bool picking;
        readonly HashSet<int> selected = new HashSet<int>();
        readonly List<GameObject> tiles = new List<GameObject>();

        void OnEnable()
        {
            if (btnAction != null) btnAction.onClick.AddListener(OnAction);
            if (sheetSave != null) sheetSave.SetActive(false);
            picking = false;
            selected.Clear();
            Refresh();
        }

        void OnDisable()
        {
            if (btnAction != null) btnAction.onClick.RemoveListener(OnAction);
        }

        int Count => capture != null ? capture.Captures.Count : 0;

        // 선택 → (고른 게 있으면) 저장 → 시트
        void OnAction()
        {
            if (picking && selected.Count > 0)
            {
                if (sheetSave != null) sheetSave.SetActive(true);
                return;
            }
            picking = !picking;
            if (!picking) selected.Clear();
            Refresh();
        }

        void Refresh()
        {
            if (navTitle != null)
                navTitle.text = picking && selected.Count > 0
                    ? $"{selected.Count}개 선택됨"
                    : $"오늘 찍은 사진들 ({Count}장)";

            if (btnActionLabel != null)
            {
                btnActionLabel.text = !picking ? "선택" : (selected.Count > 0 ? "저장" : "취소");
                btnActionLabel.color = (picking && selected.Count > 0)
                    ? DesignTokens.Text.Point : DesignTokens.Text.Secondary;
            }
            Build();
        }

        void Build()
        {
            if (content == null) return;

            // Destroy 는 프레임 끝 처리라 같은 프레임에 새 타일을 깔면 옛 타일이 위에 겹친다
            foreach (var t in tiles) if (t != null) DestroyImmediate(t);
            tiles.Clear();

            for (int i = 0; i < Count; i++)
            {
                int col = i % 2;
                int row = i / 2;

                // 타일 = 라운드 마스크(Image+Mask) + 그 안에 사진(RawImage).
                // Mask 는 그래픽을 스텐실에 그려 자식을 오려내므로 알파가 1 이어야 한다
                // (showMaskGraphic 을 꺼도 알파 0 이면 자식이 통째로 사라진다).
                var tile = new GameObject("Photo" + i, typeof(RectTransform), typeof(CanvasRenderer),
                                          typeof(Image), typeof(Mask), typeof(Button));
                tile.transform.SetParent(content, false);

                var rt = tile.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(col * (tileW + gap), -row * (tileH + gap));
                rt.sizeDelta = new Vector2(tileW, tileH);

                var frame = tile.GetComponent<Image>();
                frame.sprite = tileMask;
                frame.type = Image.Type.Sliced;
                frame.color = Color.white;
                tile.GetComponent<Mask>().showMaskGraphic = false;

                var photo = new GameObject("Img", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                photo.transform.SetParent(tile.transform, false);
                var prt = photo.GetComponent<RectTransform>();
                // stretch 로 두면 이 프레임엔 rect 가 0 이라 FitCover 가 비율을 못 잰다 → 크기를 직접 준다
                prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f);
                prt.pivot = new Vector2(0f, 1f);
                prt.anchoredPosition = Vector2.zero;
                prt.sizeDelta = new Vector2(tileW, tileH);
                var raw = photo.GetComponent<RawImage>();
                raw.raycastTarget = false;
                // 촬영본은 화면 전체 비율(1080×2400 등)이라 카드(178×267)에 그냥 넣으면 눌린다
                UiUtil.FitCover(raw, capture.Captures[i].Texture);

                var chk = new GameObject("Check", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                chk.transform.SetParent(tile.transform, false);
                var crt = chk.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0f, 1f);
                crt.anchorMax = new Vector2(0f, 1f);
                crt.pivot = new Vector2(0f, 1f);
                // 시안: 원 지름 20, 타일 좌상단에서 중심 (20,20).
                // 스프라이트 잉크가 캔버스의 60/72 라 박스를 24 로 줘야 원이 20 으로 그려진다.
                crt.anchoredPosition = new Vector2(8f, -8f);
                crt.sizeDelta = new Vector2(24f, 24f);
                var ci = chk.GetComponent<Image>();
                ci.raycastTarget = false;
                ci.sprite = selected.Contains(i) ? checkOn : checkOff;
                chk.SetActive(picking);

                int idx = i;
                tile.GetComponent<Button>().onClick.AddListener(() => OnTile(idx));
                tiles.Add(tile);
            }

            int rows = Mathf.CeilToInt(Count / 2f);
            content.sizeDelta = new Vector2(content.sizeDelta.x,
                                            Mathf.Max(0f, rows * (tileH + gap) - gap));
        }

        void OnTile(int index)
        {
            if (picking)
            {
                if (!selected.Remove(index)) selected.Add(index);
                Refresh();
                return;
            }
            // 사진 상세는 ARMediaController 가 이미 다 만들어 뒀다 (Screen_CaptureDetail).
            if (media == null)
            {
                Debug.LogError("[TodayPhotos] media 가 비어 있음 — 인스펙터에서 ARManagers 를 넣을 것");
                return;
            }
            if (capture == null || index < 0 || index >= capture.Captures.Count) return;
            media.ShowPhotoDetail(capture.Captures[index]);   // 시안 '사진 상세'
        }
    }
}
