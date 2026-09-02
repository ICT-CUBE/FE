using System;
using System.Collections.Generic;

namespace ARZone
{
    // 서버 없이 도는 메모리 구현. MockCommunitySource / MockGallerySource 와 같은 관례 —
    // 모든 콜백을 즉시 부르고 fail 은 절대 부르지 않는다.
    //
    // 값은 실서버에서 실제로 받아본 모양을 그대로 흉내낸다. Mock 만 예쁘게 만들면
    // 서버로 바꿨을 때 화면이 깨진다(예: 서버는 conversationTitle 을 null 로 준다).
    public class MockConversationSource : IConversationSource
    {
        class Row { public ConvSession s; public List<ConvMessage> msgs; }

        readonly Dictionary<int, Row> rows = new Dictionary<int, Row>();
        int nextSession = 100;
        int nextMessage = 1000;
        int nextSeq = 1;

        public MockConversationSource()
        {
            Add(1, "오늘의 인사", new[]
            {
                (true,  "안녕! 오늘 뭐 할까?"),
                (false, "좋아! 오늘은 First Light를 들어볼까?"),
            });
            Add(2, null, new[]      // ★ 제목이 null 인 경우 — 서버가 실제로 이렇게 준다
            {
                (true,  "오늘 무대 어땠어?"),
                (false, "무대 최고였지! 너도 봤어?"),
            });
        }

        void Add(int id, string title, (bool mine, string text)[] texts)
        {
            var msgs = new List<ConvMessage>();
            foreach (var (mine, text) in texts)
                msgs.Add(new ConvMessage
                {
                    id = nextMessage++, mine = mine, text = text,
                    timeText = "오후 12:25", sequence = nextSeq++,
                });
            rows[id] = new Row
            {
                s = new ConvSession
                {
                    id = id, type = ConversationType.Text, title = title,
                    preview = msgs[msgs.Count - 1].text, timeText = "오후 12:25",
                },
                msgs = msgs,
            };
        }

        public void LoadSessions(ConversationType type, int page, int size,
                                 Action<List<ConvSession>, bool> done, Action<string> fail)
        {
            var all = new List<ConvSession>();
            foreach (var r in rows.Values) if (r.s.type == type) all.Add(r.s);
            all.Sort((a, b) => b.id.CompareTo(a.id));      // 서버처럼 최신 먼저

            int from = Math.Max(0, page * size);
            int count = Math.Max(0, Math.Min(all.Count - from, size));
            var pageList = count > 0 ? all.GetRange(from, count) : new List<ConvSession>();
            done?.Invoke(pageList, from + count < all.Count);
        }

        public void LoadMessages(int sessionId, Action<List<ConvMessage>> done, Action<string> fail)
            => done?.Invoke(rows.TryGetValue(sessionId, out var r)
                            ? new List<ConvMessage>(r.msgs) : new List<ConvMessage>());

        public void CreateSession(ConversationType type, Action<int> done, Action<string> fail)
        {
            int id = nextSession++;
            rows[id] = new Row
            {
                s = new ConvSession { id = id, type = type, title = null, preview = "", timeText = "방금" },
                msgs = new List<ConvMessage>(),
            };
            done?.Invoke(id);
        }

        public void DeleteSession(int sessionId, Action done, Action<string> fail)
        {
            rows.Remove(sessionId);
            done?.Invoke();
        }

        public void EndSession(int sessionId, Action done, Action<string> fail)
        {
            done?.Invoke();          // 목은 종료 개념이 없다
        }

        public void SendMessage(int sessionId, ConversationType type, string content, string[] imageUrls,
                                Action<ConvMessage, ConvMessage> done, Action<string> fail)
        {
            if (!rows.TryGetValue(sessionId, out var r)) { fail?.Invoke("대화가 없어요"); return; }

            var mine = new ConvMessage { id = nextMessage++, mine = true, text = content,
                                         timeText = "방금", sequence = nextSeq++, imageUrls = imageUrls };
            var reply = new ConvMessage { id = nextMessage++, mine = false,
                                          text = "(Mock) " + content + " — 그렇구나!",
                                          timeText = "방금", sequence = nextSeq++ };
            r.msgs.Add(mine); r.msgs.Add(reply);
            r.s.preview = reply.text; r.s.timeText = "방금";
            done?.Invoke(mine, reply);
        }

        public void SaveMessage(int messageId, Action<bool> done, Action<string> fail) => done?.Invoke(true);

        public void LoadArchive(Action<List<ConvSaved>> done, Action<string> fail)
            => done?.Invoke(new List<ConvSaved>
            {
                new ConvSaved { id = 4, text = "몰라", sentAt = "2025-03-10T12:00:12", dateText = "2025. 03. 10" },
                new ConvSaved { id = 2, text = "좋아! 오늘은 like JENNIE 들어볼까?", sentAt = "2025-03-10T12:00:12", dateText = "2025. 03. 10" },
            });

        public void Unsave(int messageId, Action done, Action<string> fail) => done?.Invoke();

        public void ReportMessage(int messageId, string reason, string detail, Action done, Action<string> fail)
            => done?.Invoke();
    }
}
