using UnityEngine;

namespace ARZone
{
    // AR 가이드 화면은 진입 경로에 따라 두 얼굴을 가진다.
    //   FirstScan — 첫 인식(온보딩). 바닥 사진 + '올바른 인식 환경' 칩.
    //   StageAR   — 스테이지 상세의 AR 모드 토글로 진입. 영상 정보 + '?' 툴팁.
    //   Summon    — 아바타 홈의 'AR 소환'. 화면 구성은 FirstScan 과 같고 배치 뒤 갈 곳만 다르다.
    // 시안(첫 인식-가이드 / ar모드 가이드)이 팁·권한행 y 까지 다르므로 좌표도 같이 스왑한다.
    public class ARGuideMode : MonoBehaviour
    {
        public enum Mode { FirstScan, StageAR, Summon }

        // 화면을 열기 직전에 진입 경로가 여기 값을 정해둔다(기본 = 온보딩).
        public static Mode Pending = Mode.FirstScan;

        [Header("모드 전용 요소")]
        [SerializeField] GameObject[] firstScanOnly;   // 초록칩 등
        [SerializeField] GameObject[] stageArOnly;     // 영상 정보 블록

        [Header("모드별 y (시안 실측)")]
        [SerializeField] RectTransform tipList;
        [SerializeField] float tipYFirst = -362f;
        [SerializeField] float tipYStage = -521f;
        [SerializeField] RectTransform permRow;
        [SerializeField] float permYFirst = -498f;
        [SerializeField] float permYStage = -657f;

        [Header("툴팁")]
        [SerializeField] GameObject tooltip;           // '?' 를 눌러야 열린다
        [SerializeField] UnityEngine.UI.Button btnHelp;   // VideoInfo/BtnGuideHelp
        // 툴팁 뒤에 까는 투명 판. 다른 곳을 누르면 툴팁이 닫힌다.
        // (레거시 Input 대신 UI 레이캐스트를 쓴다 — 이 프로젝트는 새 Input System 이다)
        [SerializeField] UnityEngine.UI.Button tooltipDim;

        // StageAR 로 들어왔을 때 보여줄 영상 정보. 씬에 "ZEN" 이 글자로 박혀 있어
        // 어느 영상에서 들어와도 ZEN 이 떴다 → 지금 고른 스테이지에서 채운다.
        [Header("영상 정보 (StageAR 전용)")]
        [SerializeField] TMPro.TMP_Text vidTitle;
        [SerializeField] TMPro.TMP_Text vidSub;
        // AR 모드에선 안내 그래픽 대신 고른 곡의 썸네일을 띄운다
        [SerializeField] UnityEngine.UI.Image guideArt;
        [SerializeField] GameObject guidePlaceholder;
        [SerializeField] Sprite guideArtDefault;      // 첫 인식용 원래 그래픽

        void OnEnable()
        {
            if (btnHelp != null) btnHelp.onClick.AddListener(ToggleTooltip);
            if (tooltipDim != null) tooltipDim.onClick.AddListener(CloseTooltip);
            Apply(Pending);
        }

        void OnDisable()
        {
            if (btnHelp != null) btnHelp.onClick.RemoveListener(ToggleTooltip);
            if (tooltipDim != null) tooltipDim.onClick.RemoveListener(CloseTooltip);
        }

        public void Apply(Mode m)
        {
            bool stage = m == Mode.StageAR;

            foreach (var g in firstScanOnly) if (g != null) g.SetActive(!stage);
            foreach (var g in stageArOnly)   if (g != null) g.SetActive(stage);

            Move(tipList, stage ? tipYStage : tipYFirst);
            Move(permRow, stage ? permYStage : permYFirst);

            ShowTooltip(false);

            if (stage) FillVideoInfo(); else RestoreGuideArt();
        }

        void FillVideoInfo()
        {
            var cat = StageCatalog.Instance;
            var d = cat != null ? cat.Selected : null;
            if (d == null)
            {
                Debug.LogWarning("[ARGuide] 선택된 스테이지가 없어 영상 정보를 못 채움");
                return;
            }
            if (vidTitle != null) vidTitle.text = d.title;
            if (vidSub != null) vidSub.text = d.sub;

            // AR 모드에선 안내 그래픽 대신 고른 곡의 썸네일
            if (guideArt != null && d.thumb != null) guideArt.sprite = d.thumb;
            if (guidePlaceholder != null) guidePlaceholder.SetActive(false);
        }

        // 첫 인식으로 돌아가면 원래 안내 그래픽으로 되돌린다
        void RestoreGuideArt()
        {
            if (guideArt != null && guideArtDefault != null) guideArt.sprite = guideArtDefault;
            if (guidePlaceholder != null) guidePlaceholder.SetActive(true);
        }

        // '?' 버튼에 연결
        public void ToggleTooltip()
        {
            if (tooltip != null) ShowTooltip(!tooltip.activeSelf);
        }

        public void CloseTooltip() => ShowTooltip(false);

        void ShowTooltip(bool on)
        {
            if (tooltip != null) tooltip.SetActive(on);
            if (tooltipDim != null) tooltipDim.gameObject.SetActive(on);
        }

        static void Move(RectTransform rt, float y)
        {
            if (rt == null) return;
            var p = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(p.x, y);
        }
    }
}
