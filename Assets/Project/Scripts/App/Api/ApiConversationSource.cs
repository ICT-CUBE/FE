using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 서버로 도는 대화 구현.
    public class ApiConversationSource : IConversationSource
    {
        const string Sessions = "api/v1/users/me/conversation-sessions";
        const string Messages = "api/v1/users/me/conversation-messages";

        static string Wire(ConversationType t) => t == ConversationType.Voice ? "VOICE" : "TEXT";

        static ConvMessage ToModel(ConvMessageDto d) => d == null ? null : new ConvMessage
        {
            id = d.id,
            mine = d.senderType == "USER",
            text = d.content ?? "",
            timeText = ConvTime.ForBubble(d.sentAt),
            sequence = d.sequenceNumber,
            imageUrls = d.imageUrls,
            audioUrl = string.IsNullOrEmpty(d.audioUrl) ? null : d.audioUrl,
        };

        public void LoadSessions(ConversationType type, int page, int size,
                                 Action<List<ConvSession>, bool> done, Action<string> fail)
        {
            // ⚠ conversationType 을 빼면 400 INVALID_REQUEST 다(스펙상 required).
            string url = $"{Sessions}?conversationType={Wire(type)}&page={page}&size={size}";

            ApiClient.Get(url, json =>
            {
                ConvSessionListDto dto;
                try { dto = JsonUtility.FromJson<ConvSessionListDto>(json); }
                catch (Exception e)
                {
                    Debug.LogError($"[Api] 대화 목록 파싱 실패: {e.Message}\n{json}");
                    fail?.Invoke("응답을 읽지 못했어요"); return;
                }

                var list = new List<ConvSession>();
                if (dto != null && dto.conversationSessions != null)
                {
                    foreach (var s in dto.conversationSessions)
                    {
                        var lm = s.lastMessage;
                        list.Add(new ConvSession
                        {
                            id = s.id,
                            type = s.conversationType == "VOICE" ? ConversationType.Voice : ConversationType.Text,
                            title = s.conversationTitle,          // null 그대로 둔다 — 화면이 판단한다
                            preview = lm != null ? (lm.content ?? "") : "",
                            timeText = lm != null ? ConvTime.ForList(lm.sentAt) : "",
                            audioUrl = s.audioUrl,
                            audioSeconds = s.audioDurationSeconds,
                        });
                    }
                }
                bool hasNext = dto != null && dto.pagination != null && dto.pagination.hasNext;
                Debug.Log($"[Conv] 세션 {list.Count}건 ({Wire(type)}, hasNext={hasNext})");
                done?.Invoke(list, hasNext);
            }, fail);
        }

        public void LoadMessages(int sessionId, Action<List<ConvMessage>> done, Action<string> fail)
        {
            if (sessionId <= 0) { fail?.Invoke("대화가 없어요"); return; }

            ApiClient.Get($"{Sessions}/{sessionId}", json =>
            {
                try
                {
                    var dto = JsonUtility.FromJson<ConvSessionDetailDto>(json);
                    var list = new List<ConvMessage>();
                    if (dto != null && dto.conversationSession != null && dto.conversationSession.messages != null)
                        foreach (var m in dto.conversationSession.messages) list.Add(ToModel(m));

                    // ★ 서버는 sequenceNumber '내림차순'으로 준다(실측: [6,5,4,3,2,1]).
                    //   그리고 유저 메시지와 아바타 답변의 sentAt 이 초 단위로 같아서
                    //   시각으로 정렬하면 순서가 뒤집힌다 → 반드시 sequenceNumber 로 오름차순 정렬.
                    list.Sort((a, b) => a.sequence.CompareTo(b.sequence));

                    Debug.Log($"[Conv] 세션 {sessionId} 메시지 {list.Count}건");
                    done?.Invoke(list);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Api] 대화 상세 파싱 실패: {e.Message}\n{json}");
                    fail?.Invoke("응답을 읽지 못했어요");
                }
            }, fail);
        }

        public void CreateSession(ConversationType type, Action<int> done, Action<string> fail)
        {
            string body = JsonUtility.ToJson(new ConvCreateReq { conversationType = Wire(type) });
            ApiClient.Post(Sessions, body, json =>
            {
                try
                {
                    var dto = JsonUtility.FromJson<ConvSessionCreateDto>(json);
                    if (dto == null || dto.conversationSession == null || dto.conversationSession.id <= 0)
                    { fail?.Invoke("대화를 시작하지 못했어요"); return; }
                    Debug.Log($"[Conv] 새 세션 id={dto.conversationSession.id}");
                    done?.Invoke(dto.conversationSession.id);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Api] 세션 생성 파싱 실패: {e.Message}\n{json}");
                    fail?.Invoke("대화를 시작하지 못했어요");
                }
            }, fail);
        }

        public void DeleteSession(int sessionId, Action done, Action<string> fail)
        {
            if (sessionId <= 0) { fail?.Invoke("대화가 없어요"); return; }
            ApiClient.Delete($"{Sessions}/{sessionId}", _ => done?.Invoke(), fail);
        }

        // 세션 종료 — 서버가 NOT_ACTIVE + 종료시각을 저장하고, 제목이 없으면 GPT 제목을 만든다.
        // 오디오는 앱에 녹음이 없어 안 보낸다(빈 값 파트 = null 로 읽힘, 2026-08-26 실측 204).
        public void EndSession(int sessionId, Action done, Action<string> fail)
        {
            if (sessionId <= 0) { fail?.Invoke("대화가 없어요"); return; }
            ApiClient.PatchForm($"{Sessions}/{sessionId}",
                new Dictionary<string, string> { { "audioDurationSeconds", "" } },
                _ => done?.Invoke(), fail);
        }

        public void SendMessage(int sessionId, ConversationType type, string content, string[] imageUrls,
                                Action<ConvMessage, ConvMessage> done, Action<string> fail)
        {
            if (sessionId <= 0) { fail?.Invoke("대화가 없어요"); return; }

            var req = new ConvSendReq
            {
                conversationType = type == ConversationType.Voice ? "VOICE" : "TEXT",
                content = content ?? "",
                imageUrls = imageUrls ?? new string[0],
            };
            ApiClient.Post($"{Sessions}/{sessionId}/messages", JsonUtility.ToJson(req), json =>
            {
                try
                {
                    var dto = JsonUtility.FromJson<ConvSendResultDto>(json);
                    if (dto == null) { fail?.Invoke("응답을 읽지 못했어요"); return; }
                    done?.Invoke(ToModel(dto.userMessage), ToModel(dto.avatarMessage));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Api] 메시지 응답 파싱 실패: {e.Message}\n{json}");
                    fail?.Invoke("응답을 읽지 못했어요");
                }
            }, fail);
        }

        public void LoadArchive(Action<List<ConvSaved>> done, Action<string> fail)
        {
            ApiClient.Get("api/v1/users/me/conversation-archive", json =>
            {
                try
                {
                    var dto = JsonUtility.FromJson<ConvArchiveDto>(json);
                    var list = new List<ConvSaved>();
                    if (dto != null && dto.savedTexts != null)
                        foreach (var t in dto.savedTexts)
                            list.Add(new ConvSaved
                            {
                                id = t.id,
                                text = t.content ?? "",
                                sentAt = t.sentAt,
                                dateText = ConvTime.ForArchive(t.sentAt),
                            });
                    Debug.Log($"[Conv] 보관함 {list.Count}건");
                    done?.Invoke(list);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Api] 보관함 파싱 실패: {e.Message}\n{json}");
                    fail?.Invoke("응답을 읽지 못했어요");
                }
            }, fail);
        }

        public void Unsave(int messageId, Action done, Action<string> fail)
        {
            if (messageId <= 0) { fail?.Invoke("항목이 없어요"); return; }
            ApiClient.Delete($"{Messages}/{messageId}/unsave", _ => done?.Invoke(), fail);
        }

        public void ReportMessage(int messageId, string reason, string detail, Action done, Action<string> fail)
        {
            if (messageId <= 0) { fail?.Invoke("메시지가 없어요"); return; }
            var body = JsonUtility.ToJson(new ConvReportReq { reason = reason, detailReason = detail ?? "" });
            ApiClient.Post($"{Messages}/{messageId}/reports", body, _ => done?.Invoke(), fail);
        }

        public void SaveMessage(int messageId, Action<bool> done, Action<string> fail)
        {
            if (messageId <= 0) { fail?.Invoke("메시지가 없어요"); return; }
            ApiClient.Patch($"{Messages}/{messageId}/save", null, json =>
            {
                try
                {
                    var dto = JsonUtility.FromJson<ConvSaveDto>(json);
                    done?.Invoke(dto != null && dto.conversationMessage != null && dto.conversationMessage.isTextSaved);
                }
                catch (Exception)
                {
                    done?.Invoke(true);   // 200 이 왔으면 보관은 된 것이다
                }
            }, fail);
        }
    }
}
