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

        [Header("저장")]
        [SerializeField] GameObject sheetSave;
        [SerializeField] CaptureService capture;

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

            foreach (var t in tiles) if (t != null) Destroy(t);
            tiles.Clear();

            for (int i = 0; i < Count; i++)
            {
                int col = i % 2;
                int row = i / 2;

                var tile = new GameObject("Photo" + i, typeof(RectTransform), typeof(CanvasRenderer),
                                          typeof(RawImage), typeof(Button));
                tile.transform.SetParent(content, false);

                var rt = tile.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(col * (tileW + gap), -row * (tileH + gap));
                rt.sizeDelta = new Vector2(tileW, tileH);

                tile.GetComponent<RawImage>().texture = capture.Captures[i].Texture;

                var chk = new GameObject("Check", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                chk.transform.SetParent(tile.transform, false);
                var crt = chk.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0f, 1f);
                crt.anchorMax = new Vector2(0f, 1f);
                crt.pivot = new Vector2(0f, 1f);
                crt.anchoredPosition = new Vector2(10.5f, -10.5f);
                crt.sizeDelta = new Vector2(20f, 20f);
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
            Debug.Log($"[TodayPhotos] 사진 상세 — {index}번");
        }
    }
}
