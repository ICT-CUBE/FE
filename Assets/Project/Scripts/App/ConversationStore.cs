using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 대화 목록/메시지를 한 곳에 모아둔다. CommunityStore·GalleryStore 와 같은 구조.
    // 붙이는 곳: Canvas/ScreenManager
    public class ConversationStore : MonoBehaviour
    {
        public enum SourceKind { Api, Mock }

        [Header("데이터 출처")]
        [SerializeField] SourceKind source = SourceKind.Api;

        // 시안 목록에 '더 보기'·페이징 UI 가 없다 → 한 번에 넉넉히 받는다(갤러리와 같은 판단).
        [SerializeField] int pageSize = 100;

        public static ConversationStore Instance { get; private set; }

        public event Action OnSessionsChanged;
        public event Action OnMessagesChanged;

        IConversationSource src;
        readonly List<ConvSession> sessions = new List<ConvSession>();
        readonly List<ConvMessage> messages = new List<ConvMessage>();

        public List<ConvSession> Sessions => sessions;
        public List<ConvMessage> Messages => messages;

        public ConversationType Type { get; private set; } = ConversationType.Text;
        public int CurrentSessionId { get; private set; }
        public bool Sending { get; private set; }      // 아바타 답변을 기다리는 중

        void Awake()
        {
            Instance = this;
            src = source == SourceKind.Mock
                ? (IConversationSource)new MockConversationSource()
                : new ApiConversationSource();
            Debug.Log($"[Conv] 소스 = {source}");
        }

        public void LoadSessions(ConversationType type)
        {
            Type = type;
            src.LoadSessions(type, 0, pageSize, (list, hasNext) =>
            {
                sessions.Clear(); sessions.AddRange(list);
                if (hasNext)
                    Debug.LogWarning($"[Conv] pageSize({pageSize}) 를 넘는 대화가 있다 — 뒷장이 안 보인다");
                OnSessionsChanged?.Invoke();
            },
            err =>
            {
                // 실패해도 캐시를 비우지 않는다 — 빈 목록보다 직전 목록이 낫다.
                Debug.LogWarning($"[Conv] 목록 실패: {err} (직전 {sessions.Count}건 유지)");
                Toast.Show(err);
                OnSessionsChanged?.Invoke();
            });
        }

        public void OpenSession(int sessionId)
        {
            CurrentSessionId = sessionId;
            messages.Clear();
            OnMessagesChanged?.Invoke();          // 먼저 비워서 이전 대화가 남지 않게 한다

            src.LoadMessages(sessionId, list =>
            {
                messages.Clear(); messages.AddRange(list);
                OnMessagesChanged?.Invoke();
            },
            err =>
            {
                Debug.LogWarning($"[Conv] 메시지 실패: {err}");
                Toast.Show(err);
            });
        }

        public void NewSession(ConversationType type, Action<int> done)
        {
            src.CreateSession(type, id =>
            {
                CurrentSessionId = id;
                Type = type;   // 메시지 전송이 세션 종류를 같이 보내야 한다 (2026-08-31 서버 스키마)
                messages.Clear();
                OnMessagesChanged?.Invoke();
                done?.Invoke(id);
            },
            err => { Debug.LogWarning($"[Conv] 세션 생성 실패: {err}"); Toast.Show(err); });
        }

        // 대화 화면을 떠날 때 부른다 — 서버가 세션을 닫고 제목 없는 대화에 GPT 제목을 붙인다.
        // 멱등이고 종료 후에도 메시지 전송이 되므로(실측) CurrentSessionId 는 지우지 않는다
        // (보관함 갔다 돌아와 이어 말하는 흐름이 살아 있어야 한다).
        public void CloseCurrent()
        {
            if (CurrentSessionId <= 0) return;
            int sid = CurrentSessionId;
            src.EndSession(sid,
                () =>
                {
                    Debug.Log($"[Conv] 세션 {sid} 종료(제목 생성 요청)");
                    // GPT 제목은 종료 응답과 함께 만들어진다(실측) — 목록이 "제목 없음"으로
                    // 남지 않게 바로 다시 읽는다(목록 화면이 열려 있으면 즉시 갱신된다).
                    LoadSessions(Type);
                },
                err => Debug.LogWarning($"[Conv] 세션 {sid} 종료 실패: {err}"));
        }

        public void Delete(int sessionId, Action done = null)
        {
            src.DeleteSession(sessionId, () =>
            {
                sessions.RemoveAll(s => s.id == sessionId);
                OnSessionsChanged?.Invoke();
                done?.Invoke();
            },
            err => { Debug.LogWarning($"[Conv] 삭제 실패: {err}"); Toast.Show(err); });
        }

        // 보낸 뒤 아바타 답변이 올 때까지 시간이 걸린다(서버가 생성한다).
        // 그 사이 화면이 '보내는 중' 을 표시할 수 있게 Sending 을 열어둔다.
        public void Send(string content, string[] imageUrls = null, Action done = null)
        {
            if (CurrentSessionId <= 0) { Toast.Show("대화를 먼저 시작해 주세요"); return; }
            if (Sending) return;                     // 연타로 두 번 보내지 않게

            // 전송을 누르는 즉시 내 말풍선부터 띄운다 (2026-08-30 유저 지시 —
            // 예전엔 아바타 답변과 함께 한꺼번에 떠서 몇 초간 아무 반응이 없어 보였다).
            // 서버 응답이 오면 서버가 준 진짜 메시지로 갈아끼운다.
            var optimistic = new ConvMessage
            {
                id = 0, mine = true, text = content,
                timeText = ConvTime.ForBubble(ApiConfig.Iso(System.DateTime.Now)),
                sequence = (messages.Count > 0 ? messages[messages.Count - 1].sequence : 0) + 1,
                imageUrls = imageUrls,
            };
            messages.Add(optimistic);
            OnMessagesChanged?.Invoke();

            Sending = true;
            src.SendMessage(CurrentSessionId, Type, content, imageUrls, (mine, reply) =>
            {
                Sending = false;
                messages.Remove(optimistic);
                if (mine != null) messages.Add(mine);
                if (reply != null) messages.Add(reply);
                messages.Sort((a, b) => a.sequence.CompareTo(b.sequence));
                OnMessagesChanged?.Invoke();
                done?.Invoke();
            },
            err =>
            {
                Sending = false;
                messages.Remove(optimistic);   // 실패 — 임시 말풍선을 거두고 알린다
                Debug.LogWarning($"[Conv] 전송 실패: {err}");
                Toast.Show(err);
                OnMessagesChanged?.Invoke();
            });
        }

        public void LoadArchive(Action<List<ConvSaved>> done)
            => src.LoadArchive(done, err => { Debug.LogWarning($"[Conv] 보관함 실패: {err}"); Toast.Show(err); });

        public void Unsave(int messageId, Action done = null)
            => src.Unsave(messageId, () => done?.Invoke(),
                          err => { Debug.LogWarning($"[Conv] 보관 해제 실패: {err}"); Toast.Show(err); });

        public void ReportMessage(int messageId, string reason, string detail, Action done = null)
            => src.ReportMessage(messageId, reason, detail, () => done?.Invoke(),
                                 err => { Debug.LogWarning($"[Conv] 신고 실패: {err}"); Toast.Show(err); });

        public void SaveText(int messageId, Action<bool> done = null)
            => src.SaveMessage(messageId, ok =>
               {
                   done?.Invoke(ok);
               },
               err => { Debug.LogWarning($"[Conv] 보관 실패: {err}"); Toast.Show(err); });
    }
}
