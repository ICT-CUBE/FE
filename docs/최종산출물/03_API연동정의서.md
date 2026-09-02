# ARZONE — 프론트엔드 API 연동 정의서

> 대상 서버 `https://hanium.azurewebsites.net` (FastAPI · OpenAPI 3.1) · 작성 2026-08-31
> 프론트 연동 계층: `Assets/Project/Scripts/App/Api/` (19파일) + 인터페이스/스토어

## 1. 공통 규약

| 항목 | 값 |
|---|---|
| Base URL | `https://hanium.azurewebsites.net` (`ApiConfig`) |
| 타임아웃 | 일반 10s · 이미지/업로드 20s · 음원 60s |
| Content-Type | `application/json` (UTF-8) / 업로드는 multipart/form-data |
| 공통 헤더 | `Accept: application/json` · `Authorization: Bearer {token}` |
| 시각 포맷 | `yyyy-MM-dd'T'HH:mm:ss` — offset 없는 KST (서버 규약) |
| 에러 바디 | `{ code, message }` — 400=INVALID_REQUEST · 500=INTERNAL_SERVER_ERROR 통일 |

### 인증 흐름
- `POST /auth/signup` · `POST /auth/login/email` 둘만 무토큰. 성공 시 accessToken을
  PlayerPrefs(`arzone.access_token`)에 저장 → 앱 재실행에도 유지(자동 로그인).
- 모든 요청 직전 토큰 복원 후 Bearer 부착 (JSON·업로드 동일).
- **401 전역 처리**: 토큰 삭제 → 로그인 화면 이동 + "로그인이 만료되었어요" 토스트.
  병렬 요청 다중 401은 3초 디바운스로 1회만 동작.
- 만료 시 재로그인 UX로 확정.

### 전송 계층 구조
```
화면(Controller) → Store/도메인 인터페이스(IXxxSource) → ApiXxxSource → ApiClient(전송 유일 창구)
                                    ↘ MockXxxSource (서버 불능/오프라인 개발용)
```
- **ApiClient 하나만 URL·헤더·에러 파싱을 안다.** 도메인이 늘어도 전송 코드는 안 늘어난다.
- 콜백(코루틴) 방식 — 코드베이스에 async/await 미도입(설계 결정, 05_주요결정사항 참조).
- multipart PATCH는 Unity 폼 클래스가 빈 값을 거부해 boundary `----arzone-form` 수제 조립.
- `GetAudioClip`(스트리밍)은 성공 시 요청을 의도적으로 유지 — streamAudio 클립이 다운로드
  버퍼에 의존(곡당 ~3MB 상주, 전곡 캐시).

## 2. 엔드포인트 전수 (프론트가 실제 호출하는 것)

### 2-1. 인증
| 메서드 | 경로 | 요청 | 응답 핵심 | 사용처 |
|---|---|---|---|---|
| POST | /api/v1/auth/signup | email·password·nickname·profileCharacterCode | user.id · auth.accessToken | 회원가입(닉네임 화면) |
| POST | /api/v1/auth/login/email | email·password | 〃 | 이메일 로그인 |

### 2-2. 홈
| GET | /api/v1/users/me/home | — | user·cubeOwnership(acquiredAt)·cube(artistName)·affinity(level·xp) | 홈 화면 · 허브 탑바(D-day/with) |
|---|---|---|---|---|

### 2-3. 갤러리
| 메서드 | 경로 | 비고 |
|---|---|---|
| GET | /users/me/gallery?category=&page=&size= | ALL/CAPTURE/ALBUM/CONCERT_STILLCUT · 페이징 |
| DELETE | /users/me/gallery/{sourceType}/{id} | STAGE_/TRACK_/INTERACTION_CAPTURE·ALBUM_IMAGE |
| GET | /users/me/gallery/{sourceType}/{id}/ar-experience | AR 재경험 — contentType·시작 위치·stage/track 정보 |

### 2-4. 대화 (텍스트·음성 공용)
| 메서드 | 경로 | 비고 |
|---|---|---|
| GET | /users/me/conversation-sessions?conversationType=&page=&size= | TEXT\|VOICE 필수 · 목록/검색 |
| GET | /users/me/conversation-sessions/{id} | 메시지 전체(sequenceNumber 오름차순 재정렬은 앱이) |
| POST | /users/me/conversation-sessions | {conversationType} → 새 세션 id |
| POST | /users/me/conversation-sessions/{id}/messages | {conversationType, content, imageUrls} → userMessage + **avatarMessage(AI 생성, audioUrl=TTS)** |
| PATCH(multipart) | /users/me/conversation-sessions/{id} | 세션 종료 → 서버가 GPT 제목 생성. 화면 이탈 시 자동 호출 |
| DELETE | /users/me/conversation-sessions/{id} | 삭제 |
| GET | /users/me/conversation-archive | 보관한 텍스트 목록 |
| PATCH | /users/me/conversation-messages/{id}/save | 보관 |
| DELETE | /users/me/conversation-messages/{id}/unsave | 보관 해제 |
| POST | /users/me/conversation-messages/{id}/reports | {reason: INCORRECT_PERSONA\|HALLUCINATION\|INAPPROPRIATE_EXPRESSION, detailReason} |

### 2-5. 아바타 AR 소환
| 메서드 | 경로 | 비고 |
|---|---|---|
| POST | /users/me/avatar-interaction-sessions | 소환 세션 시작 |
| PATCH | /users/me/avatar-interaction-sessions/{id} | 종료 |
| POST(업로드) | /users/me/avatar-interaction-sessions/{id}/pictures | image(png)+picturedAt |
| GET | /users/me/album/yezul-frames | 예절샷 프레임 목록 |

### 2-6. 무대·음원 (스트리밍 + 세션 기록)
| 메서드 | 경로 | 비고 |
|---|---|---|
| GET | /stages?videoType= | MUSIC_VIDEO\|CONCERT_VIDEO — 목록·썸네일 |
| GET | /stages/{id} | videoUrl(스트리밍) |
| POST | /stages/{id}/sessions | {viewMode: AR\|NORMAL} |
| PATCH | /users/me/stage-sessions/{id} | {status, lastPositionSeconds, viewedSeconds} |
| POST(업로드) | /users/me/stage-sessions/{id}/captures | AR 무대 캡처 |
| GET | /album | 트랙 목록(id 매칭용) |
| GET | /tracks/{id} | audioUrl(스트리밍) |
| POST | /tracks/{id}/sessions | {playMode: SPECIAL(AR)\|NORMAL} |
| PATCH | /users/me/track-sessions/{id} | status에 **CANCELLED**(감상 중 모드 전환) 포함 |
| POST(업로드) | /users/me/track-sessions/{id}/captures | AR 감상 캡처(SPECIAL만) |

### 2-7. 기타
| 메서드 | 경로 | 비고 |
|---|---|---|
| POST | /cubes/verification | {cubeId, ownershipCode} → cubeOwnershipId (온보딩 코드 입력) |
| GET | /users/me/missions | 일일/이벤트 미션 + 친밀도 |
| GET | /users/me/trades/{id} | 주문 상세(타임라인) |
| PATCH | /users/me/trades/{id}/complete | 구매 확정 → 블록체인(NFT) 증명서 |
| POST | /users/me/trades | 거래 생성(204) |

### 2-8. 바이너리
| 헬퍼 | 대상 | 사용처 |
|---|---|---|
| GetTexture(+ImageCache) | imageUrl | 갤러리·무대 썸네일·예절샷 프레임 (중복요청 병합+캐시) |
| GetAudioClip(streamAudio) | audioUrl | 음원 전곡 스트리밍 · 음성대화 TTS |

## 3. 안정성 설계
| 항목 | 방식 |
|---|---|
| 무대·음원 재생 | 로컬 즉시 재생 → 서버 스트림 도착 시 같은 위치로 무봉 교체 — 네트워크 불안에도 재생이 끊기지 않음 |
| 이미지 | ImageCache — 동일 URL 중복 요청 병합 + 메모리 캐시 |
| 세션 기록 | 화면 흐름과 분리(실패해도 UX 영향 없음, 로그로 추적) |
| 토큰 만료 | 401 전역 감지 → 자동 로그아웃·재로그인 유도 |

## 4. 구현 참고
1. JsonUtility 제약으로 enum은 전부 string 수신 후 수동 변환. nullable int는 0으로 수신.
2. 이미지/음원 GET에는 토큰을 붙이지 않는다(공개 URL — Azure Blob).
3. 콘텐츠 매칭은 제목 정규화 기반(로컬 시드 ↔ 서버 id 연결).
