using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 음성 통화 화면 — 4단계 상태 전환 (2026-08-31 개정 시안 '음성 대화 *'.svg 실측 반영):
    //   Idle    아바타 인사(위, 흰색) + 주제 카드 패널
    //   Listen  '듣고 있어요...' + 파형 (안드로이드 STT 가 듣는 중)
    //   Spoken  내 말이 아바타 아래 알약(46.5,-682.5 319×60 #181A20)에 뜬다
    //   Reply   내 말이 위로 올라가고(회색 이전 대사 아래 흰 글씨), 아바타 답변이
    //           아래 패널(46.5,-569.5 319×171 #181A20, 테두리 #242831)에 뜬다
    // STT 는 AndroidSpeech(내장 인식기), 답변은 대화 API. TTS 는 백엔드 API 대기(문의 16).
    public class VoiceTalkController : MonoBehaviour
    {
        [Header("대사")]
        [SerializeField] TMP_Text line;
        [SerializeField] string greeting = "응? 불렀어? 나랑 이야기하고 싶었구나!\n오늘 하루 어때?";

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

        [Header("코드 생성 UI 스프라이트")]
        [SerializeField] Sprite panelSprite;   // bg_round16 — 답변 패널(라운드 사각, 시안 r16)

        // 홈에서 주제를 골라 들어오면 그 주제로 시작한다
        public static int PendingTopic = -1;

        // 2026-08-31 유저 확정 레이아웃: 상단 line = 아바타 말(인사→응답으로 교체, 겹침 금지),
        // 하단 네모 상자 = 내 말(글 길이에 맞춰 크기 조절). 별도 답변 패널은 쓰지 않는다.
        enum Stage { Idle, Listen, Spoken }

        Stage stage = Stage.Idle;
        bool sessionOpen;
        string lastSpeech = "";        // 마지막 내 발화 — 하단 상자에 유지

        // ── 코드로 만드는 요소들 (씬에 없어서 — 기존 관례) ──
        Image userPill;       // 아바타 아래 내 말 상자 (라운드 사각)
        TMP_Text userPillText;
        Transform voicePanel; // 씬의 주제 패널 배경 — 발화·응답 상태에선 시안에 없다

        void OnEnable()
        {
            if (btnMic != null) btnMic.onClick.AddListener(ToggleListen);
            if (btnEnd != null) btnEnd.onClick.AddListener(End);
            if (btnExitCancel != null) btnExitCancel.onClick.AddListener(CloseExit);
            if (btnExitConfirm != null) btnExitConfirm.onClick.AddListener(ConfirmExit);
            if (modalExit != null) modalExit.SetActive(false);
            VoiceTopicRow.Build(topicRow, cardSprite, null, OnTopic, fontSource, topicIcons);
            voicePanel = transform.Find("VoicePanel");

            stage = Stage.Idle;
            if (line != null) { line.text = greeting; line.color = Color.white; }
            Apply();

            if (PendingTopic >= 0)
            {
                OnTopic(PendingTopic);
                PendingTopic = -1;
            }
        }

        void OnDisable()
        {
            if (ttsSource != null) ttsSource.Stop();   // 화면을 떠나면 TTS 도 멈춘다
            if (btnMic != null) btnMic.onClick.RemoveListener(ToggleListen);
            if (btnEnd != null) btnEnd.onClick.RemoveListener(End);
            if (btnExitCancel != null) btnExitCancel.onClick.RemoveListener(CloseExit);
            if (btnExitConfirm != null) btnExitConfirm.onClick.RemoveListener(ConfirmExit);

            // 화면을 떠나면 세션을 닫는다(서버가 GPT 제목을 붙인다) — 텍스트 대화와 같은 관례
            if (sessionOpen)
            {
                var store = ConversationStore.Instance;
                if (store != null) store.CloseCurrent();
                sessionOpen = false;
            }
        }

        void CloseExit() { if (modalExit != null) modalExit.SetActive(false); }

        void ConfirmExit()
        {
            if (modalExit != null) modalExit.SetActive(false);
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(afterExit);
        }

        // ── 유저 입력 → 서버 → 답변 ──

        void OnTopic(int i)
        {
            var list = VoiceTopicRow.Default;
            if (i < 0 || i >= list.Count) return;
            Speak(VoiceTopicRow.StripEmoji(list[i].title));
        }

        // 마이크 버튼 — 안드로이드 내장 음성 인식 (2026-08-30 유저 지시)
        void ToggleListen()
        {
            if (stage != Stage.Listen)
            {
                stage = Stage.Listen;
                Apply();
                AndroidSpeech.Listen(Speak, err =>
                {
                    stage = Stage.Idle;
                    Apply();
                    Toast.Show(err);
                });
            }
            else
            {
                AndroidSpeech.StopListen();   // 결과/실패 콜백이 마무리한다
            }
        }

        // 내 발화 확정 → 하단 상자 표시 + 서버 전송. 답이 오면 상단 대사를 응답으로 교체.
        void Speak(string text)
        {
            if (string.IsNullOrEmpty(text)) { stage = Stage.Idle; Apply(); return; }
            lastSpeech = text;
            stage = Stage.Spoken;
            Apply();
            ShowUserSpeech(text);

            var store = ConversationStore.Instance;
            if (store == null) return;
            if (!sessionOpen)
            {
                store.NewSession(ConversationType.Voice, _ =>
                {
                    sessionOpen = true;
                    SendToAvatar(store, text);
                });
                return;
            }
            SendToAvatar(store, text);
        }

        void SendToAvatar(ConversationStore store, string content)
        {
            store.Send(content, null, () =>
            {
                var msgs = store.Messages;
                for (int i = msgs.Count - 1; i >= 0; i--)
                    if (!msgs[i].mine)
                    {
                        ShowReply(msgs[i].text);
                        PlayReplyAudio(msgs[i].audioUrl);
                        return;
                    }
            });
        }

        // ── 아바타 답변 TTS 재생 (2026-08-31, 서버가 avatarMessage.audioUrl 을 주기 시작) ──
        AudioSource ttsSource;

        void PlayReplyAudio(string url)
        {
            if (string.IsNullOrEmpty(url)) return;   // 서버가 아직 안 주면 텍스트만 (기존 동작)
            ApiClient.GetAudioClip(url, clip =>
            {
                if (this == null || !isActiveAndEnabled || clip == null) return;
                if (ttsSource == null) ttsSource = gameObject.AddComponent<AudioSource>();
                ttsSource.Stop();
                ttsSource.clip = clip;
                ttsSource.Play();
                HookLipSync();
            }, err => Debug.LogWarning("[Voice] TTS 재생 실패: " + err));
        }

        // 무대 아바타 입을 TTS 소리에 맞춰 움직인다 (AR감상의 AvatarLipSync 재사용)
        void HookLipSync()
        {
            var stage = GameObject.Find("AvataStage");
            if (stage == null) return;
            foreach (var a in stage.GetComponentsInChildren<Animator>(false))
                if (a.isHuman)
                {
                    var lip = a.GetComponent<AvatarLipSync>();
                    if (lip == null) lip = a.gameObject.AddComponent<AvatarLipSync>();
                    lip.source = ttsSource;
                    lip.gate = null;
                    return;
                }
        }

        // 응답 도착 — 상단 대사를 응답으로 '교체'한다 (이전 인사를 남기면 글자가 겹친다)
        void ShowReply(string reply)
        {
            if (line != null) { line.text = reply ?? ""; line.color = Color.white; }
        }

        // ── 상태별 표시 ──
        void Apply()
        {
            bool listen = stage == Stage.Listen;
            bool idle = stage == Stage.Idle;

            if (listenTitle != null) listenTitle.SetActive(listen);
            if (listenSub != null) listenSub.SetActive(listen);
            if (listenWave != null) listenWave.SetActive(listen);

            // 주제 카드·패널은 Idle 에만 (개정 시안: 발화·답변 화면엔 없다)
            if (topicRow != null) topicRow.gameObject.SetActive(idle);
            if (topicLabel != null) topicLabel.SetActive(idle);
            if (voicePanel != null) voicePanel.gameObject.SetActive(idle || listen);

            if (line != null) line.gameObject.SetActive(!listen);
            if (userPill != null) userPill.gameObject.SetActive(stage == Stage.Spoken);
        }

        // ── 코드 생성 UI (시안 실측값) ──

        void EnsureUserPill()
        {
            if (userPill != null) { userPill.gameObject.SetActive(true); return; }
            // 시안은 캡슐(타원)이 아니라 라운드 사각(r16) — 2026-08-31 유저 지적
            userPill = UiUtil.MakeImage((RectTransform)transform, "UserSpeech",
                                        panelSprite,
                                        new Color(0x18 / 255f, 0x1A / 255f, 0x20 / 255f, 1f),
                                        Image.Type.Sliced, false);
            UiUtil.Place(userPill, 46.5f, 682.5f, 319f, 60f);   // 시안 '유저 발화 입력됨' 기본값

            userPillText = MakeCenteredText(userPill.transform, 13f);
        }

        // 하단 내 말 상자 — 글 길이에 맞춰 크기를 조절한다 (2026-08-31 유저 지시).
        // 짧으면 좁게(최소 120), 최대 폭은 시안 상자(319). 넘치면 줄바꿈하고 위로 자란다
        // (바닥은 시안대로 -742.5 고정 — 아래 마이크 버튼과의 간격 유지).
        void ShowUserSpeech(string text)
        {
            EnsureUserPill();
            if (userPillText == null || string.IsNullOrEmpty(text))
            {
                if (userPill != null) userPill.gameObject.SetActive(false);
                return;
            }
            userPillText.text = text;

            const float maxW = 319f, minW = 120f, padX = 24f, padY = 12f, bottomY = 742.5f;
            Vector2 pref = userPillText.GetPreferredValues(text, maxW - padX * 2f, 0f);
            float w = Mathf.Clamp(pref.x + padX * 2f, minW, maxW);
            float h = Mathf.Max(60f, pref.y + padY * 2f);
            UiUtil.Place(userPill, (412f - w) * 0.5f, bottomY - h, w, h);
            userPill.gameObject.SetActive(true);
        }

        TMP_Text MakeCenteredText(Transform parent, float size)
        {
            var t = UiUtil.MakeText(parent, "Text", DesignTokens.Type.Body3,
                                    Color.white, TextAlignmentOptions.Center);
            var tr = t.rectTransform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(12f, 0f); tr.offsetMax = new Vector2(-12f, 0f);
            t.fontSize = size;
            CopyFont(t);
            return t;
        }

        void CopyFont(TMP_Text t)
        {
            if (fontSource == null || fontSource.font == null || t == null) return;
            t.font = fontSource.font;
            t.fontSharedMaterial = fontSource.fontSharedMaterial;
        }

        void End()
        {
            if (modalExit != null) { modalExit.SetActive(true); return; }
            if (ScreenManager.Instance != null) ScreenManager.Instance.Back();
        }
    }
}
