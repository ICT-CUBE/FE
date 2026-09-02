using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 온보딩 큐브인증 — Screen_Scan 의 가짜 3초 타이머를 진짜 Vuforia 인식으로 바꾼다 (2026-08-26).
    //
    // 시안(큐브인증 프레임): 카메라 화면 위에 스캔 프레임 + "큐브를 인식하고 있어요" 진행바,
    // 인식되면 큐브인증성공("RUBY CUBE를 찾았어요!") 으로 — 그 화면 전환은 기존
    // ScanProgressController 가 Report(1) 을 받으면 스스로 한다(nextScreen=CubeFound).
    //
    // 화면이 켜지면: Bg(불투명 목업 배경)를 끄고 → Vuforia ARCamera 를 켜고 → 인식 시작.
    //   진행바는 인식 전까지 85% 로 천천히 기어가고(찾는 중 연출), 인식되면 100%.
    // 화면을 떠나면 전부 원상복구 — ARCamera 를 꺼야 나중에 ARCore(AR 소환)가 카메라를 쓴다.
    //
    // 씬 배선(유니티에서): Screen_Scan 에 이 컴포넌트 + VuforiaCubeDetector 를 붙이고
    //   progress(같은 화면의 ScanProgressController) · vuforiaCamera(꺼진 ARCamera GO) · bg(Bg) 드래그.
    public class CubeScanController : MonoBehaviour
    {
        [SerializeField] ScanProgressController progress;
        [SerializeField] VuforiaCubeDetector detector;
        [SerializeField] GameObject vuforiaCamera;   // Vuforia ARCamera (기본 꺼짐)
        [SerializeField] GameObject bg;              // Screen_Scan/Bg — 카메라가 보이게 끈다
        [SerializeField, Tooltip("인식 전 진행바가 기어가서 멈추는 지점")]
        float searchCeiling = 0.85f;
        [SerializeField, Tooltip("기어가는 속도(초당 비율). 클수록 빨리 참")]
        float searchSpeed = 0.35f;

        float p;
        bool found;
        RawImage guideUi;   // 프레임 안 실루엣 — 인식이 끝날 때까지 계속 띄운다 (2026-08-31 유저 지시)

        void OnEnable()
        {
            p = 0f; found = false;
            if (guideUi != null) guideUi.gameObject.SetActive(true);
            if (bg != null) bg.SetActive(false);
            if (progress != null) progress.Report(0f);         // 가짜 타이머 무력화 (external 모드)
            if (vuforiaCamera != null) vuforiaCamera.SetActive(true);
            if (detector != null)
            {
                detector.ResetDetection();
                detector.OnDetected += OnFound;
                detector.Begin();
            }
            else Debug.LogWarning("[CubeScan] detector 미배선 — 진행바가 끝까지 안 찬다");
        }

        void OnDisable()
        {
            if (detector != null)
            {
                detector.OnDetected -= OnFound;
                detector.End();
            }
            if (vuforiaCamera != null) vuforiaCamera.SetActive(false);
            if (bg != null) bg.SetActive(true);
        }

        // 실루엣 텍스처는 Model Target 이 늦게 준비되므로(비동기 생성) 될 때까지 매 프레임 확인.
        // ScanFrame(321×328) 안에 비율 유지로 딱 맞춘다 — Vuforia 기본 오버레이 대체.
        void EnsureGuideUi()
        {
            if (guideUi != null || detector == null || detector.GuideImage == null) return;
            var frame = transform.Find("ScanFrame") as RectTransform;
            if (frame == null) return;

            var go = new GameObject("GuideSilhouette", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(frame, false);
            var tex = detector.GuideImage;
            const float pad = 20f;
            // 가이드 텍스처는 화면 전체 렌더라 실루엣이 한가운데 일부다 — 전체를 프레임에
            // 맞추면 실루엣이 겁나 작아진다 (2026-08-31 실기). 가운데(가로 50%×세로 60%)만
            // 잘라서 크게 그린다. uvRect 의 -높이는 상하 반전(렌더텍스처 뒤집힘) 겸용.
            const float uvW = 0.5f, uvH = 0.6f;
            float bw = frame.rect.width - pad * 2f, bh = frame.rect.height - pad * 2f;
            float ta = tex.height > 0 ? (tex.width * uvW) / (tex.height * uvH) : 1f;
            float w = bw, h = w / ta;
            if (h > bh) { h = bh; w = h * ta; }
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);

            guideUi = go.GetComponent<RawImage>();
            guideUi.texture = Snapshot(tex);   // 원본은 Vuforia 가 실시간 갱신해 크기·내용이 출렁인다 — 1회 복사로 고정
            guideUi.uvRect = new Rect((1f - uvW) * 0.5f, 0.5f + uvH * 0.5f, uvW, -uvH);
            guideUi.color = new Color(1f, 1f, 1f, 0.9f);
            guideUi.raycastTarget = false;
        }

        // Vuforia 가이드 텍스처는 SDK 가 계속 다시 그린다(실기: 실루엣이 작아졌다 커졌다).
        // 첫 프레임에 정지 사본을 떠서 그걸 그린다.
        static Texture Snapshot(Texture src)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t2 = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            t2.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            t2.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t2;
        }

        void Update()
        {
            EnsureGuideUi();
            if (found || progress == null) return;

            // 진짜 진행 표시 (2026-08-31 "인식 잘 되고 있는지 알 수 있게"):
            // 추적을 못 잡으면 바닥(10%)으로 내려가고, 잡히면 연속 관측(50%)+시차(35%)만큼 찬다.
            // 차오르다 뚝 떨어지면 '놓쳤구나'를 사용자가 바로 안다 — 기존 85% 기어가기 연출은
            // 인식과 무관해서 아무 정보도 못 줬다.
            float target;
            if (detector != null && detector.IsTracking)
                target = 0.15f + 0.5f * detector.HoldProgress + 0.35f * detector.ParallaxProgress;
            else if (detector != null)
                target = 0.1f;
            else
                target = searchCeiling;   // 디텍터 미배선 폴백 — 기존 연출 유지

            // 바는 올라가기만 한다 (2026-08-31 유저: "다운은 하지 말자" — 떨어지면 짜침).
            // 관측이 리셋돼도 그 자리에서 멈춰 있다가, 다시 쌓이면 이어서 차오른다.
            if (target > p)
                p = Mathf.MoveTowards(p, Mathf.Min(target, 0.97f), searchSpeed * 2.5f * Time.deltaTime);
            progress.Report(p);
        }


        void OnFound(Pose pose)
        {
            found = true;
            if (guideUi != null) guideUi.gameObject.SetActive(false);   // 인식 끝 — 그때만 내린다
            if (progress != null) progress.Report(1f);         // → ScanProgressController 가 CubeFound 로
        }
    }
}
