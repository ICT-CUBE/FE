using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARZone
{
    // 음성 대화 홈 (시안 '음성 대화').
    //   아바타 카드 · 새 음성 대화 시작 · 대화 주제 추천 3장 · 최근 음성 대화 목록
    // 실제 음성 기능(STT/TTS)은 AI 인터페이스가 붙은 뒤에 온다. 지금은 화면과 이동만 맞춘다.
    public class VoiceHomeController : MonoBehaviour
    {
        [System.Serializable]
        public class Session
        {
            public string title;
            public string length;    // 04:24
            public string when;      // 어제 / 3일 전
            public int id;           // 서버 세션 id (삭제에 쓴다). 0 = 로컬 더미
        }

        // 서버 VOICE 세션 → 화면 모델 (2026-08-25 배선, 유저 결정 "서버로 연결")
        // 제목 null 은 비운다(텍스트 목록과 같은 결정). length 는 audioDurationSeconds 를 mm:ss 로.
        public static Session FromServer(ConvSession s) => new Session
        {
            id = s.id,
            title = s.title ?? "",
            length = $"{s.audioSeconds / 60:00}:{s.audioSeconds % 60:00}",
            when = s.timeText,
        };

        [Header("이동")]
        [SerializeField] Button btnStart;
        [SerializeField] string talkScreen = "VoiceTalk";
        [SerializeField] Button btnAll;              // 전체 보기
        [SerializeField] string listScreen = "VoiceList";

        [Header("주제")]
        [SerializeField] RectTransform topicRow;
        [SerializeField] Sprite cardSprite;
        [SerializeField] Sprite[] topicIcons;        // ☕️ 🎵 ⭐️ (폰트에 없어 이미지로)          // bg_round12
        [SerializeField] TMP_Text fontSource;        // 폰트를 물려받을 씬 라벨

        [Header("최근 음성 대화")]
        [SerializeField] RectTransform recentList;
        [SerializeField] Sprite playIcon;
        [SerializeField] Sprite waveSprite;          // ic_wave_row (유저 제공 'sound line')
        [SerializeField] float rowPitch = 75f;       // 시안 668.86 → 743.91
        [SerializeField] List<Session> sessions = new List<Session>();

        // API 가 오기 전까지 쓰는 시안 목업. 씬에 빈 배열로 직렬화돼도 목록이 비지 않게 여기서 채운다.
        public static List<Session> Mock => new List<Session>
        {
            new Session { title = "저녁 메뉴 추천과 토론토 여행 마침의 아쉬움", length = "04:24", when = "어제" },
            new Session { title = "지난 콘서트에 대한 감상평",                 length = "09:53", when = "3일 전" },
            new Session { title = "좋아하는 음악과 강아지 이야기",             length = "16:09", when = "5일 전" },
        };

        // '전체 음성 대화' 목록 (시안 순서 그대로)
        public static List<Session> MockAll => new List<Session>
        {
            new Session { title = "저녁 메뉴 추천과 토론토 여행 마침의 아쉬움", length = "04:24", when = "어제" },
            new Session { title = "자연과의 만남",                             length = "09:15", when = "어제" },
            new Session { title = "운동과 건강 관리",                          length = "10:30", when = "어제" },
            new Session { title = "가족과의 소중한 시간",                      length = "16:00", when = "3일 전" },
            new Session { title = "영화 리뷰와 추천",                          length = "17:45", when = "3일 전" },
            new Session { title = "책 읽는 즐거움",                            length = "12:15", when = "3일 전" },
            new Session { title = "지난 콘서트에 대한 감상평",                 length = "09:53", when = "3일 전" },
            new Session { title = "새로운 레시피 도전",                        length = "18:20", when = "3일 전" },
            new Session { title = "여행의 추억과 친구들",                      length = "14:32", when = "4일 전" },
            new Session { title = "오트밀과 단호박 조합의 재발견",             length = "1:03",  when = "5일 전" },
        };

        void OnEnable()
        {
            if (btnStart != null) btnStart.onClick.AddListener(GoTalk);
            if (btnAll != null) btnAll.onClick.AddListener(GoList);
            VoiceTopicRow.Build(topicRow, cardSprite, null, OnTopic, fontSource, topicIcons);

            // 서버 VOICE 목록으로 대체. 서버에 세션이 0건이면 목록이 빈다 — 유저 결정.
            var store = ConversationStore.Instance;
            if (store != null)
            {
                store.OnSessionsChanged += RebuildFromStore;
                store.LoadSessions(ConversationType.Voice);
            }
            BuildRecent();
        }

        void RebuildFromStore()
        {
            var store = ConversationStore.Instance;
            if (store == null || store.Type != ConversationType.Voice) return;
            sessions = new List<Session>();
            foreach (var s in store.Sessions) sessions.Add(FromServer(s));
            BuildRecent();
        }

        void OnDisable()
        {
            if (btnStart != null) btnStart.onClick.RemoveListener(GoTalk);
            if (btnAll != null) btnAll.onClick.RemoveListener(GoList);
            if (ConversationStore.Instance != null)
                ConversationStore.Instance.OnSessionsChanged -= RebuildFromStore;
        }

        void GoList()
        {
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(listScreen);
        }

        void OnTopic(int i)
        {
            VoiceTalkController.PendingTopic = i;
            GoTalk();
        }

        void GoTalk()
        {
            if (ScreenManager.Instance != null) ScreenManager.Instance.Show(talkScreen);
            else Debug.LogError("[VoiceHome] ScreenManager 가 씬에 없음");
        }

        void BuildRecent()
        {
            if (recentList == null) return;
            UiUtil.ClearChildren(recentList);
            if (sessions == null) sessions = new List<Session>();   // 서버 0건이면 빈 목록 그대로 (유저 결정)
            float rowW = recentList.sizeDelta.x > 1f ? recentList.sizeDelta.x : 372f;   // rect 는 아직 0

            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                var row = UiUtil.MakeNode(recentList, "Row" + i);
                UiUtil.Place(row, 0f, i * rowPitch, rowW, rowPitch);

                // 시안 실측 (행 top 기준): 재생 (19.33,-31.5) 10.67×12 · 제목 잉크 y -19.86
                //   파형 (47.5,-44.5) 115×11 · 길이 (174.66,-45.68) · 날짜 우측 (335.57,-32.5)
                var play = UiUtil.MakeImage(row, "Play", playIcon, DesignTokens.Text.Primary);
                UiUtil.Place(play, 19.33f, 31.5f, 10.67f, 12f);

                var title = UiUtil.MakeText(row.transform, "Title", DesignTokens.Type.Body3,
                                            DesignTokens.Text.Primary, TextAlignmentOptions.MidlineLeft);
                title.fontSize = 14f;
                title.text = s.title;
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.overflowMode = TextOverflowModes.Ellipsis;
                UiUtil.Place(title, 48.31f, 14.6f, 280f, 22f);
                CopyFont(title);

                if (waveSprite != null)
                {
                    var wave = UiUtil.MakeImage(row, "Wave", waveSprite, Color.white);
                    UiUtil.Place(wave, 47.5f, 44.5f, 115f, 11f);
                }

                var len = UiUtil.MakeText(row.transform, "Length", DesignTokens.Type.Caption1,
                                          DesignTokens.Text.Secondary, TextAlignmentOptions.MidlineLeft);
                len.fontSize = 12f;
                len.text = s.length;
                UiUtil.Place(len, 174.66f, 41f, 80f, 18f);
                CopyFont(len);

                var when = UiUtil.MakeText(row.transform, "When", DesignTokens.Type.Caption1,
                                           DesignTokens.Text.Primary, TextAlignmentOptions.MidlineRight);
                when.fontSize = 12f;
                when.text = s.when;
                UiUtil.Place(when, 255.57f, 27.8f, 100f, 18f);
                CopyFont(when);

                if (i < sessions.Count - 1)
                {
                    var line = UiUtil.MakeImage(row, "Line", null, DesignTokens.Bg.Surface);
                    UiUtil.Place(line, 16f, rowPitch - 1f, rowW - 32f, 1f);
                }
            }
        }

        void CopyFont(TMP_Text t)
        {
            if (fontSource == null || fontSource.font == null || t == null) return;
            t.font = fontSource.font;
            t.fontSharedMaterial = fontSource.fontSharedMaterial;
        }
    }
}
