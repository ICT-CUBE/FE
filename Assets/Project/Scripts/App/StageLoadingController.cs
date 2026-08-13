using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 무대 로딩 화면. 큐브 시퀀스가 도는 동안 진행바를 채우고, 다 차면 AR 무대로 넘긴다.
    // '중단하기' 를 누르면 되돌아간다.
    public class StageLoadingController : MonoBehaviour
    {
        [SerializeField] TMP_Text title;
        [SerializeField] RectTransform progressFill;
        [SerializeField] float trackWidth = 322f;      // 시안 실측
        [SerializeField] SpriteProgressBar progressAnim;   // 반짝이는 로딩바 (216장 시퀀스)
        [SerializeField] float seconds = 3f;
        [SerializeField] Button btnCancel;
        [SerializeField] string nextScreen = "ARGuide";

        // 어느 항목을 불러오는지 — 호출 쪽에서 정해둔다
        public static string PendingTitle = "251220MMA";

        float t;

        void OnEnable()
        {
            if (btnCancel != null) btnCancel.onClick.AddListener(OnCancel);
            if (title != null) title.text = PendingTitle;
            t = 0f;
            Apply(0f);
        }

        void OnDisable()
        {
            if (btnCancel != null) btnCancel.onClick.RemoveListener(OnCancel);
        }

        void Update()
        {
            if (seconds <= 0f) return;
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / seconds);
            Apply(p);

            if (p >= 1f && ScreenManager.Instance != null)
            {
                // 무대는 다시 배치해야 하므로 AR 가이드부터 (시안: '현재 공간을 스캔한 후')
                ARGuideMode.Pending = ARGuideMode.Mode.StageAR;
                ScreenManager.Instance.Show(nextScreen);
            }
        }

        void Apply(float p)
        {
            if (progressAnim != null) progressAnim.SetProgress(p);
            if (progressFill == null) return;
            var s = progressFill.sizeDelta;
            progressFill.sizeDelta = new Vector2(trackWidth * p, s.y);
        }

        void OnCancel()
        {
            if (ScreenManager.Instance != null) ScreenManager.Instance.Back();
        }
    }
}
