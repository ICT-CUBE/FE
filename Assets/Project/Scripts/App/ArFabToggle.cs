using UnityEngine;
using UnityEngine.UI;

namespace ARZone
{
    // 허브 우하단 AR 버튼. 누르면 'Scan AR' 로 펼쳐지고(ar-1 시안), 다시 누르면 접힌다.
    // 펼친 상태에서 Scan AR 을 누르면 AR 가이드로 이동.
    public class ArFabToggle : MonoBehaviour
    {
        [SerializeField] Button btnAr;          // Screen_Hub/BtnAR
        // 시안(ar / ar-1)의 두 상태를 스프라이트 한 장씩으로 받았다.
        [SerializeField] Image  btnArBg;            // BtnAR 의 Image
        [SerializeField] Sprite bgCollapsed;        // btn_ar_on    — 보라 + 큐브
        [SerializeField] Sprite bgExpanded;         // btn_ar_close — 보라 + 흰 X
        [SerializeField] GameObject expanded;   // Screen_Hub/FabExpanded (dim + Scan AR 버튼)
        [SerializeField] Button btnScanAr;      // FabExpanded/BtnScanAr
        [SerializeField] string guideScreen = "ARGuide";

        void OnEnable()
        {
            Collapse();
            if (btnAr != null) btnAr.onClick.AddListener(Toggle);
            if (btnScanAr != null) btnScanAr.onClick.AddListener(GoGuide);
        }

        void OnDisable()
        {
            if (btnAr != null) btnAr.onClick.RemoveListener(Toggle);
            if (btnScanAr != null) btnScanAr.onClick.RemoveListener(GoGuide);
        }

        void Toggle()
        {
            bool on = expanded != null && !expanded.activeSelf;
            if (expanded != null) expanded.SetActive(on);
            Paint(on);
        }

        void Collapse()
        {
            if (expanded != null) expanded.SetActive(false);
            Paint(false);
        }

        // 사용자가 AR 을 켠 상태 = 흰 X 버튼
        void Paint(bool on)
        {
            if (btnArBg == null) return;
            var b = on ? bgExpanded : bgCollapsed;
            if (b != null) btnArBg.sprite = b;
        }

        void GoGuide()
        {
            Collapse();
            // ★ Pending 은 static 이라 한 번 StageAR 이 되면 계속 남는다.
            //   허브의 Scan AR 은 '첫 인식' 흐름이므로 반드시 되돌려 놓는다.
            //   (안 그러면 무대 준비 모달을 건너뛰고 바로 무대로 가버린다)
            ARGuideMode.Pending = ARGuideMode.Mode.FirstScan;
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(guideScreen);
        }
    }
}
