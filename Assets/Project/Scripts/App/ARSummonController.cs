using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // AR 소환: 현실 공간에 아바타를 세우고 프레임을 씌워 촬영한다.
    // 촬영분은 CaptureService 가 들고 있고, 종료하면 리포트로 넘긴다.
    public class ARSummonController : MonoBehaviour
    {
        [System.Serializable]
        public class FrameItem
        {
            public string label = "적용 안 함";
            public Sprite overlay;      // 비우면 '프레임 없음'
        }

        [Header("서브")]
        [SerializeField] CaptureService capture;

        [Header("소환 화면")]
        [SerializeField] Button btnShutter;
        [SerializeField] Button btnCamFlip;
        [SerializeField] Button btnGallery;
        [SerializeField] Button btnFramePick;
        [SerializeField] Image frameOverlay;        // 촬영 화면 전체를 덮는 프레임

        [Header("프레임 선택 시트 (시안: 썸네일 80×120, 간격 96, 라벨 y=754.5)")]
        [SerializeField] GameObject sheetFramePicker;
        [SerializeField] RectTransform frameList;
        [SerializeField] Sprite checkMark;
        [SerializeField] float thumbW = 80f;
        [SerializeField] float thumbH = 120f;
        [SerializeField] float thumbGap = 16f;      // 96 - 80
        [SerializeField] List<FrameItem> frames = new List<FrameItem>();

        [Header("종료 → 리포트")]
        [SerializeField] GameObject modalExit;
        [SerializeField] Button btnExitOpen;
        [SerializeField] Button btnExitKeep;
        [SerializeField] Button btnExitConfirm;
        [SerializeField] string reportScreen = "SummonReport";

        [Header("리포트")]
        [SerializeField] TMP_Text reportShotCount;
        [SerializeField] Image reportPhotoLeft;
        [SerializeField] Image reportPhotoRight;
        [SerializeField] RawImage reportRawLeft;
        [SerializeField] RawImage reportRawRight;
        [SerializeField] Button btnViewAll;
        [SerializeField] string todayScreen = "TodayPhotos";

        int selectedFrame;
        readonly List<GameObject> frameViews = new List<GameObject>();

        void OnEnable()
        {
            Hook(btnShutter, Shoot);
            Hook(btnCamFlip, FlipCamera);
            Hook(btnGallery, () => Go(todayScreen));
            Hook(btnFramePick, OpenFrameSheet);
            Hook(btnExitOpen, () => SetActive(modalExit, true));
            Hook(btnExitKeep, () => SetActive(modalExit, false));
            Hook(btnExitConfirm, FinishToReport);
            Hook(btnViewAll, () => Go(todayScreen));

            SetActive(modalExit, false);
            SetActive(sheetFramePicker, false);
            ApplyFrame(selectedFrame);
        }

        void OnDisable()
        {
            Unhook(btnShutter); Unhook(btnCamFlip); Unhook(btnGallery); Unhook(btnFramePick);
            Unhook(btnExitOpen); Unhook(btnExitKeep); Unhook(btnExitConfirm); Unhook(btnViewAll);
        }

        // ── 촬영 ──
        void Shoot()
        {
            if (capture == null)
            {
                Debug.LogWarning("[ARSummon] CaptureService 가 없음");
                return;
            }
            capture.Capture();
        }

        void FlipCamera()
        {
            // ARFoundation 의 카메라 전환은 ARSessionController 가 들고 있는 리그에서 처리한다.
            Debug.Log("[ARSummon] 카메라 전환 — ARCameraManager.requestedFacingDirection 연결 필요");
        }

        // ── 프레임 선택 ──
        void OpenFrameSheet()
        {
            BuildFrameList();
            SetActive(sheetFramePicker, true);
        }

        void BuildFrameList()
        {
            if (frameList == null) return;

            foreach (var v in frameViews) if (v != null) Destroy(v);
            frameViews.Clear();

            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i];
                var cell = new GameObject("Frame" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                cell.transform.SetParent(frameList, false);

                var rt = cell.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(i * (thumbW + thumbGap), 0f);
                rt.sizeDelta = new Vector2(thumbW, thumbH);

                var img = cell.GetComponent<Image>();
                img.sprite = f.overlay;
                // 프레임이 없는 항목은 빈 테두리로 (시안 첫 칸)
                img.color = f.overlay != null ? Color.white : new Color(1f, 1f, 1f, 0f);

                if (i == selectedFrame && checkMark != null)
                {
                    var chk = new GameObject("Check", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    chk.transform.SetParent(cell.transform, false);
                    var crt = chk.GetComponent<RectTransform>();
                    crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
                    crt.pivot = new Vector2(0.5f, 0.5f);
                    crt.anchoredPosition = Vector2.zero;
                    crt.sizeDelta = new Vector2(20f, 20f);
                    chk.GetComponent<Image>().sprite = checkMark;
                    chk.GetComponent<Image>().raycastTarget = false;
                }

                var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                label.transform.SetParent(cell.transform, false);
                var lrt = label.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 0f);
                lrt.pivot = new Vector2(0.5f, 1f);
                lrt.anchoredPosition = new Vector2(0f, -11.5f);   // 시안: 썸네일 하단 743 → 라벨 754.5
                lrt.sizeDelta = new Vector2(0f, 18f);
                var t = label.GetComponent<TextMeshProUGUI>();
                if (reportShotCount != null && reportShotCount.font != null)
                { t.font = reportShotCount.font; t.fontSharedMaterial = reportShotCount.fontSharedMaterial; }
                t.text = f.label;
                t.fontSize = 13f;
                t.alignment = TextAlignmentOptions.Top;
                t.color = DesignTokens.Text.Secondary;
                t.raycastTarget = false;

                int idx = i;
                cell.GetComponent<Button>().onClick.AddListener(() => { ApplyFrame(idx); BuildFrameList(); });
                frameViews.Add(cell);
            }

            frameList.sizeDelta = new Vector2(frames.Count * (thumbW + thumbGap) - thumbGap, frameList.sizeDelta.y);
        }

        void ApplyFrame(int idx)
        {
            selectedFrame = Mathf.Clamp(idx, 0, Mathf.Max(0, frames.Count - 1));
            if (frameOverlay == null) return;
            var sp = (frames.Count > 0) ? frames[selectedFrame].overlay : null;
            frameOverlay.sprite = sp;
            frameOverlay.enabled = sp != null;
        }

        // ── 종료 → 리포트 ──
        void FinishToReport()
        {
            SetActive(modalExit, false);
            FillReport();
            Go(reportScreen);
        }

        void FillReport()
        {
            int n = capture != null ? capture.Captures.Count : 0;
            if (reportShotCount != null)
                reportShotCount.text = $"촬영 및 저장된 사진 수 <b>{n}장</b>";

            SetPhoto(reportRawLeft, reportPhotoLeft, n > 0 ? capture.Captures[n - 1].Texture : null);
            SetPhoto(reportRawRight, reportPhotoRight, n > 1 ? capture.Captures[n - 2].Texture : null);
        }

        static void SetPhoto(RawImage raw, Image img, Texture tex)
        {
            if (raw != null)
            {
                raw.texture = tex;
                raw.color = new Color(1f, 1f, 1f, tex != null ? 1f : 0f);
            }
            if (img != null) img.color = new Color(1f, 1f, 1f, tex != null ? 0f : 1f);
        }

        static void Go(string id)
        {
            if (ScreenManager.Instance != null && !string.IsNullOrEmpty(id))
                ScreenManager.Instance.Show(id);
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        void Hook(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b != null) b.onClick.AddListener(a);
        }

        void Unhook(Button b)
        {
            if (b != null) b.onClick.RemoveAllListeners();
        }
    }
}
