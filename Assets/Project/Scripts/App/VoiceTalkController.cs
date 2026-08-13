using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 음성 통화 화면 (시안 '음성 대화 시작' / '유저 발화 인식 중' / '아바타 대답').
    // 한 화면의 상태 전환이다 — 마이크를 누르면 '듣고 있어요...' 로 바뀌고, 다시 누르면 대답으로 돌아온다.
    // STT/TTS 는 AI 인터페이스가 붙은 뒤에 온다.
    public class VoiceTalkController : MonoBehaviour
    {
        [Header("대사")]
        [SerializeField] TMP_Text line;
        [SerializeField] string greeting = "응? 불렀어? 나랑 이야기하고 싶었구나!\n오늘 하루 어땠어?";

        [Header("듣는 중")]
        [SerializeField] GameObject listenTitle;
        [SerializeField] GameObject listenSub;
        [SerializeField] GameObject listenWave;      // ic_wave_listen (유저 제공 'sound line-2')

        [Header("주제")]
        [SerializeField] RectTransform topicRow;
        [SerializeField] GameObject topicLabel;      // '대화 주제 추천' — 듣는 중엔 파형이 그 자리에 온다
        [SerializeField] Sprite cardSprite;
        [SerializeField] Sprite[] topicIcons;        // ☕️ 🎵 ⭐️ (폰트에 없어 이미지로)
        [SerializeField] TMP_Text fontSource;

        [Header("컨트롤")]
        [SerializeField] Button btnMic;
        [SerializeField] Button btnSpeaker;
        [SerializeField] Button btnEnd;
        [SerializeField] GameObject modalExit;       // 음성 대화 종료 확인 (없으면 바로 뒤로)
        [SerializeField] Button btnExitCancel;       // 취소
        [SerializeField] Button btnExitConfirm;      // 대화 종료
        [SerializeField] string afterExit = "VoiceHome";

        // 홈에서 주제를 골라 들어오면 그 주제로 시작한다
        public static int PendingTopic = -1;

        bool listening;

        void OnEnable()
        {
            if (btnMic != null) btnMic.onClick.AddListener(ToggleListen);
            if (btnEnd != null) btnEnd.onClick.AddListener(End);
            if (btnExitCancel != null) btnExitCancel.onClick.AddListener(CloseExit);
            if (btnExitConfirm != null) btnExitConfirm.onClick.AddListener(ConfirmExit);
            if (modalExit != null) modalExit.SetActive(false);
            VoiceTopicRow.Build(topicRow, cardSprite, null, OnTopic, fontSource, topicIcons);

            listening = false;
            Apply();

            if (PendingTopic >= 0)
            {
                OnTopic(PendingTopic);
                PendingTopic = -1;
            }
            else if (line != null) line.text = greeting;
        }

        void OnDisable()
        {
            if (btnMic != null) btnMic.onClick.RemoveListener(ToggleListen);
            if (btnEnd != null) btnEnd.onClick.RemoveListener(End);
            if (btnExitCancel != null) btnExitCancel.onClick.RemoveListener(CloseExit);
            if (btnExitConfirm != null) btnExitConfirm.onClick.RemoveListener(ConfirmExit);
        }

        void CloseExit() { if (modalExit != null) modalExit.SetActive(false); }

        void ConfirmExit()
        {
            if (modalExit != null) modalExit.SetActive(false);
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(afterExit);
        }

        void OnTopic(int i)
        {
            var list = VoiceTopicRow.Default;
            if (line != null && i >= 0 && i < list.Count) line.text = list[i].desc;
        }

        void ToggleListen()
        {
            listening = !listening;
            Apply();
        }

        // 듣는 중에는 주제 카드 대신 안내가 보인다 (시안)
        void Apply()
        {
            if (listenTitle != null) listenTitle.SetActive(listening);
            if (listenSub != null) listenSub.SetActive(listening);
            if (listenWave != null) listenWave.SetActive(listening);
            if (topicRow != null) topicRow.gameObject.SetActive(!listening);
            if (topicLabel != null) topicLabel.SetActive(!listening);
            if (line != null) line.gameObject.SetActive(!listening);
        }

        void End()
        {
            if (modalExit != null) { modalExit.SetActive(true); return; }
            if (ScreenManager.Instance != null) ScreenManager.Instance.Back();
        }
    }
}
