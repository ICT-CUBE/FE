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
        [SerializeField] ARSessionController session;   // 전후면 전환용 (ARManagers)

        [Header("소환 화면")]
        [SerializeField] Button btnShutter;
        [SerializeField] Button btnCamFlip;
        [SerializeField] Button btnGallery;
        [SerializeField] Button btnFramePick;
        [SerializeField] Image frameOverlay;        // 촬영 화면 전체를 덮는 프레임

        [Header("프레임 선택 시트 (시안: 썸네일 80×120, 간격 96, 라벨 y=754.5)")]
        [SerializeField] GameObject sheetFramePicker;
        [SerializeField] Button btnFrameCancel;     // 취소 — 열기 전 프레임으로 되돌리고 닫는다
        [SerializeField] Button btnFrameApply;      // 프레임 적용하기 — 고른 채로 닫는다
        [SerializeField] RectTransform frameList;
        [SerializeField] Sprite checkMark;
        [SerializeField] float thumbW = 80f;
        [SerializeField] float thumbH = 120f;
        [SerializeField] float thumbGap = 16f;      // 96 - 80
        [SerializeField] List<FrameItem> frames = new List<FrameItem>();

        // 시안 '현실 공간 소환 프레임 촬영' — 프레임을 고르면 화면이 통째로 이 상태로 바뀐다.
        // 카메라가 (20,76) 372×559 r16 카드 안으로 들어가고, 네비바가 사라지고,
        // 하단에 60×90 프레임 스트립이 상주하며, 셔터가 72 → 52 로 줄어든다.
        [Header("프레임 촬영 상태 (시안: 스트립 60×90, 간격 76, 라벨 y=764.4)")]
        [SerializeField] GameObject frameShootUI;    // 이 상태 전용 묶음
        [SerializeField] GameObject[] plainOnly;     // 기본 촬영 상태에서만 보이는 것들
        [SerializeField] Image frameArt;             // 카드 안에 깔리는 프레임 그림
        [SerializeField] RectTransform frameStrip;
        [SerializeField] Button btnFrameClose;       // ∨ — 프레임 해제하고 기본 촬영으로
        [SerializeField] Button btnShutter2;
        [SerializeField] Button btnCamFlip2;
        [SerializeField] float stripW = 60f;
        [SerializeField] float stripH = 90f;
        [SerializeField] float stripGap = 16f;       // 76 - 60
        [SerializeField] float stripCheck = 10f;     // 시안 체크 원 지름 (시트는 20)

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
        [SerializeField] Button btnResummon;      // 이전 — 이어서 촬영
        [SerializeField] Button btnSummonExit;    // 다음 — 오늘 찍은 사진들
        [SerializeField] string todayScreen = "TodayPhotos";
        [SerializeField] string summonScreen = "ARSummon";

        int selectedFrame;
        int frameBeforeSheet;   // 시트를 열 때의 선택 — 취소하면 여기로 되돌린다

        void OnEnable()
        {
            Hook(btnShutter, Shoot);
            Hook(btnCamFlip, FlipCamera);
            Hook(btnGallery, () => Go(todayScreen));
            Hook(btnFramePick, OpenFrameSheet);
            Hook(btnFrameCancel, CancelFrameSheet);
            Hook(btnFrameApply, CloseFrameSheet);
            Hook(btnShutter2, Shoot);
            Hook(btnCamFlip2, FlipCamera);
            Hook(btnFrameClose, () => ApplyFrame(0));
            Hook(btnExitOpen, () => SetActive(modalExit, true));
            Hook(btnExitKeep, () => SetActive(modalExit, false));
            Hook(btnExitConfirm, FinishToReport);

            SetActive(modalExit, false);
            SetActive(sheetFramePicker, false);
            ApplyFrame(selectedFrame);
            LoadServerFrames();
        }

        // ── 예절샷 프레임을 서버 목록으로 대체 (2026-08-25 배선) ──
        // 씬의 frames 는 로컬 더미였다. 서버 GET …/album/yezul-frames 가 11개(실URL)를 주므로
        // '적용 안 함' 칸(시안의 첫 칸)만 남기고 서버 것으로 갈아끼운다.
        // 실패하면 씬 목록 그대로 — 되던 화면을 깨뜨리지 않는다.
        bool serverFramesLoaded;

        void LoadServerFrames()
        {
            if (serverFramesLoaded) return;

            avatarSrc.LoadYezulFrames(list =>
            {
                if (list == null || list.Count == 0) return;

                var items = new List<FrameItem>();
                items.Add(frames.Count > 0 ? frames[0] : new FrameItem());   // '적용 안 함'

                int remain = list.Count;
                foreach (var f in list)
                {
                    var item = new FrameItem { label = f.title ?? "", overlay = null };
                    items.Add(item);
                    ImageCache.Load(f.imageUrl, tex =>
                    {
                        if (tex != null)
                            item.overlay = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                                         new Vector2(0.5f, 0.5f), 100f);
                        if (--remain > 0) return;

                        // 전부 도착했을 때만 갈아끼운다 — 반쯤 빈 목록이 순간적으로 보이지 않게.
                        serverFramesLoaded = true;
                        frames = items;
                        selectedFrame = Mathf.Clamp(selectedFrame, 0, frames.Count - 1);
                        if (sheetFramePicker != null && sheetFramePicker.activeSelf) BuildFrameList();
                        Debug.Log($"[Avatar] 예절샷 프레임 {list.Count}개 로드 (씬 더미 대체)");
                    });
                }
            },
            err => Debug.LogWarning($"[Avatar] 프레임 목록 실패: {err} — 씬 프레임 유지"));
        }

        void OnDisable()
        {
            Unhook(btnShutter); Unhook(btnCamFlip); Unhook(btnGallery); Unhook(btnFramePick);
            Unhook(btnFrameCancel); Unhook(btnFrameApply);
            Unhook(btnShutter2); Unhook(btnCamFlip2); Unhook(btnFrameClose);
            Unhook(btnExitOpen); Unhook(btnExitKeep); Unhook(btnExitConfirm);
        }

        // ⚠ 리포트(Screen_SummonReport)의 버튼들은 Screen_ARSummon 이 꺼진 뒤에 눌린다.
        //   OnEnable 에 걸면 화면이 꺼지는 순간 OnDisable 이 리스너를 떼어버려 죽는다.
        //   그래서 Start 에서 한 번만 걸고 OnDestroy 에서 푼다.
        void Start()
        {
            Hook(btnViewAll, () => Go(todayScreen));
            Hook(btnResummon, () => Go(summonScreen));      // 이전 = 이어서 촬영
            Hook(btnSummonExit, () => Go(todayScreen));     // 다음 = 오늘 찍은 사진들
        }

        void OnDestroy()
        {
            Unhook(btnViewAll); Unhook(btnResummon); Unhook(btnSummonExit);
        }

        // ── 촬영 ──
        void Shoot()
        {
            if (capture == null)
            {
                Debug.LogWarning("[ARSummon] CaptureService 가 없음");
                return;
            }
            // 캔버스를 통째로 끄면 프레임까지 꺼져서 사진에 안 씌워진다.
            // 조작 UI 만 감추고, 프레임(FrameArt)·카드 마스크는 남긴다.
            capture.Capture(UploadShot, ShotHiddenUi());
        }

        // ── 서버 업로드 (2026-08-25 배선) ──
        // CaptureService 는 파일이 원본이다 → 그 파일 바이트를 그대로 올린다.
        // 실패해도 로컬 파일·화면 흐름은 그대로다(리포트/오늘 사진은 로컬 기준으로 계속 돈다).
        readonly IAvatarSource avatarSrc = new ApiAvatarSource();

        void UploadShot(CaptureService.Entry e)
        {
            if (e == null || string.IsNullOrEmpty(e.FilePath)) return;
            int sid = ARFlowController.SummonSessionId;
            if (sid <= 0)
            {
                Debug.LogWarning("[Avatar] 소환 세션 id 가 없어 업로드를 건너뜀 (생성 실패했거나 아직 응답 전)");
                return;
            }
            byte[] png;
            try { png = System.IO.File.ReadAllBytes(e.FilePath); }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Avatar] 캡처 파일을 못 읽음: {ex.Message}");
                return;
            }
            avatarSrc.UploadPicture(sid, png, e.TakenAt,
                () => Debug.Log($"[Avatar] 캡처 업로드 완료 (세션 {sid}, {png.Length}B)"),
                err => Debug.LogWarning($"[Avatar] 캡처 업로드 실패: {err}"));
        }

        GameObject[] ShotHiddenUi()
        {
            var list = new List<GameObject>();
            if (plainOnly != null)
                foreach (var g in plainOnly) if (g != null) list.Add(g);
            if (btnFrameClose != null) list.Add(btnFrameClose.gameObject);
            if (btnShutter2 != null) list.Add(btnShutter2.gameObject);
            if (btnCamFlip2 != null) list.Add(btnCamFlip2.gameObject);
            if (frameStrip != null) list.Add(frameStrip.gameObject);   // 썸네일·라벨 통째로
            return list.ToArray();
        }

        void FlipCamera()
        {
            if (session == null)
            {
                Debug.LogError("[ARSummon] session 이 비어 있음 — 인스펙터에서 ARManagers 를 넣을 것");
                return;
            }
            session.FlipCamera();
        }

        // ── 프레임 선택 ──
        void OpenFrameSheet()
        {
            frameBeforeSheet = selectedFrame;
            BuildFrameList();
            SetActive(sheetFramePicker, true);
        }

        // 시안: 썸네일을 누르면 바로 미리보기가 걸리고, 취소하면 열기 전으로 되돌아간다
        void CancelFrameSheet()
        {
            ApplyFrame(frameBeforeSheet);
            SetActive(sheetFramePicker, false);
        }

        void CloseFrameSheet() => SetActive(sheetFramePicker, false);

        void BuildFrameList() => BuildCells(frameList, thumbW, thumbH, thumbGap, 20f);

        // 같은 목록을 시트(80×120)와 촬영 스트립(60×90) 두 곳에 그린다.
        // 라벨은 두 시안 모두 칸 아래 11.5, 12px, text/secondary 로 같다.
        void BuildCells(RectTransform root, float w, float h, float gap, float checkSize)
        {
            if (root == null) return;

            for (int c = root.childCount - 1; c >= 0; c--) DestroyImmediate(root.GetChild(c).gameObject);

            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i];
                var cell = new GameObject("Frame" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                cell.transform.SetParent(root, false);

                var rt = cell.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(i * (w + gap), 0f);
                rt.sizeDelta = new Vector2(w, h);

                var img = cell.GetComponent<Image>();
                img.sprite = f.overlay;
                // 프레임이 없는 항목은 빈 테두리로 (시안 첫 칸)
                img.color = f.overlay != null ? Color.white : new Color(1f, 1f, 1f, 0f);

                if (i == selectedFrame)
                {
                    // 시안: 고른 칸 위에 흰 판을 덮는다 (fill="white" fill-opacity="0.1", 시트·스트립 동일)
                    var sel = new GameObject("Selected", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    sel.transform.SetParent(cell.transform, false);
                    var srt = sel.GetComponent<RectTransform>();
                    srt.anchorMin = Vector2.zero;
                    srt.anchorMax = Vector2.one;
                    srt.offsetMin = Vector2.zero;
                    srt.offsetMax = Vector2.zero;
                    var si = sel.GetComponent<Image>();
                    si.color = new Color(1f, 1f, 1f, 0.1f);
                    si.raycastTarget = false;
                }

                if (i == selectedFrame && checkMark != null)
                {
                    var chk = new GameObject("Check", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    chk.transform.SetParent(cell.transform, false);
                    var crt = chk.GetComponent<RectTransform>();
                    crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
                    crt.pivot = new Vector2(0.5f, 0.5f);
                    crt.anchoredPosition = Vector2.zero;
                    crt.sizeDelta = new Vector2(checkSize, checkSize);
                    chk.GetComponent<Image>().sprite = checkMark;
                    chk.GetComponent<Image>().raycastTarget = false;
                }

                var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                label.transform.SetParent(cell.transform, false);
                var lrt = label.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 0f);
                lrt.pivot = new Vector2(0.5f, 1f);
                lrt.anchoredPosition = new Vector2(0f, -11.5f);   // 시트 743→754.5 · 스트립 753→764.4
                lrt.sizeDelta = new Vector2(0f, 18f);
                var t = label.GetComponent<TextMeshProUGUI>();
                if (reportShotCount != null && reportShotCount.font != null)
                { t.font = reportShotCount.font; t.fontSharedMaterial = reportShotCount.fontSharedMaterial; }
                t.text = f.label;
                t.fontSize = 12f;                       // 시안 실측 («적용 안 함» 잉크 46.73)
                t.alignment = TextAlignmentOptions.Top;
                t.color = DesignTokens.Text.Secondary;
                t.raycastTarget = false;

                int idx = i;
                cell.GetComponent<Button>().onClick.AddListener(() =>
                {
                    ApplyFrame(idx);
                    BuildFrameList();
                });
            }

            root.sizeDelta = new Vector2(frames.Count * (w + gap) - gap, root.sizeDelta.y);
        }

        void ApplyFrame(int idx)
        {
            selectedFrame = Mathf.Clamp(idx, 0, Mathf.Max(0, frames.Count - 1));
            var sp = (frames.Count > 0) ? frames[selectedFrame].overlay : null;

            // 프레임이 걸리면 '프레임 촬영' 상태로, 풀면 기본 촬영으로 (시안 두 장이 이 두 상태다).
            bool shoot = sp != null;

            if (plainOnly != null)
                foreach (var g in plainOnly) if (g != null) g.SetActive(!shoot);
            SetActive(frameShootUI, shoot);

            // 전체화면 오버레이는 프레임 촬영 상태에선 카드 안(frameArt)이 대신한다
            if (frameOverlay != null) { frameOverlay.sprite = null; frameOverlay.enabled = false; }
            if (frameArt != null) { frameArt.sprite = sp; frameArt.enabled = shoot; }

            if (shoot) BuildCells(frameStrip, stripW, stripH, stripGap, stripCheck);
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
                // 촬영본은 화면 전체 비율이라 178×270 카드에 그냥 넣으면 눌린다 → 중앙 크롭
                UiUtil.FitCover(raw, tex);
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
