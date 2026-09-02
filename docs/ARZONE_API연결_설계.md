# ARZONE API 연결 — 설계

> 작성 2026-08-21 · 대상 서버 `https://hanium.azurewebsites.net`
> 명세서 원본: `~/Desktop/CUBE/api/inner/Private & Shared/API 명세서_v5/` (Notion export)
> 기계 판독용 스펙: `~/Desktop/CUBE/api/openapi.json` (서버에서 직접 받은 것)
>
> **아직 코드는 한 줄도 안 썼다.** 이 문서는 "왜 이렇게 붙이기로 했는가" 를 남기는 설계서다.
> 유저 승인 후 구현에 들어간다.

---

## 0. ★ 이 작업의 대전제 (2026-08-21 유저 지시)

> **"시안에는 있는데 백엔드에 구현 안 된 게 많다. 시간상 어쩔 수 없이 선택한 거고,
> 데모에서 안 눌러볼 기능이다. 시안대로 하고 나머지는 그냥 패스하면 된다.
> 지금 api 에 있는 것만 붙이면 된다."**

이 문서의 모든 판단은 여기서 나온다. 구체적으로:

1. **붙이는 건 서버에 실제로 있는 24개뿐이다.** 없는 API 를 위해 아무것도 만들지 않는다.
2. **화면은 시안대로 둔다.** 백엔드가 없다고 버튼을 끄거나 기능을 빼지 않는다 —
   지금 Mock·로컬로 도는 것은 **그대로 돌게 놔둔다**(커뮤니티·마이·마켓 조회 등).
3. **서버 규칙과 시안이 충돌하면 시안이 이긴다.** 데모에서 안 밟을 경로에 방어 코드를 쌓지 않는다.
   대신 **로그에는 진실을 남긴다**(7-3-1).
4. 그래서 이 문서에서 "⚠" 로 표시한 것들은 **버그가 아니라 의식적으로 감수한 것**이다.

---

## 0-1. 30초 요약

- 서버는 **살아 있고 인증이 없다.** 지금 당장 붙일 수 있는 엔드포인트는 **24개**.
- 앱에는 **네트워크 코드가 한 줄도 없다**(`UnityWebRequest` 검색 0건). HTTP 계층을 새로 짠다.
- 24개를 한꺼번에 붙이지 않는다. **갤러리 3개로 뼈대를 세우고** 같은 틀로 나머지를 찍어낸다.
- 구조는 **`ApiClient` 코어 하나 + 도메인별 인터페이스**. 기존 `ICommunitySource` 관례를 그대로 잇는다.
- ⚠ **서버 DB의 `imageUrl`·`videoUrl` 이 진짜 URL이 아니다.** 백엔드 확인 없이는 사진을 못 띄운다. → 9장.

---

## 1. 서버를 직접 때려서 확인한 것

명세서(Notion)만 믿지 않고 실제로 호출해서 대조했다. 명세서와 실제가 어긋나는 곳이 있었기 때문이다.

| 항목 | 확인값 | 확인 방법 |
|---|---|---|
| Base URL | `https://hanium.azurewebsites.net` | 유저 제공 Swagger UI `/docs` |
| 스펙 | `/openapi.json` → **OpenAPI 3.1 · FastAPI · 46KB · 스키마 65개** | `curl` |
| 엔드포인트 | **24개** (+ `/`, `/health`, `/health/db`) | `openapi.json` 파싱 |
| 인증 | **없음.** `security` 없음, `securitySchemes` 빈 배열 | `openapi.json` |
| 응답 속도 | `/health` 0.16s | `curl -w %{time_total}` |
| 에러 형식 | `{"code": "...", "message": "..."}` | 실제 404/400 응답 |

실제 응답 3종(그대로 옮김):

```jsonc
// GET /health
{"status":"ok","environment":"production"}

// GET /api/v1/users/me/home
{"user":{"id":1,"profileCharacterImgUrl":"CHAR_MINA"},
 "cubeOwnership":{"id":1,"avatarNickname":"루미","acquiredAt":"2026-08-07T09:20:00"},
 "cube":{"id":1,"artistName":"ARZONE"},
 "affinity":{"id":1,"currentXp":120,"level":{"id":2,"level":2,"levelName":"Friend","levelXp":100}}}

// GET /api/v1/users/me/gallery/STAGE_CAPTURE/99999/ar-experience   → 404
{"code":"GALLERY_ITEM_NOT_FOUND","message":"갤러리 항목을 찾을 수 없습니다."}
```

### 붙일 수 있는 24개 (= 명세서 CSV의 `백엔드 완료` 와 정확히 일치)

| 도메인 | 개수 | 엔드포인트 |
|---|---|---|
| home | 1 | `GET users/me/home` |
| gallery | 3 | `GET users/me/gallery` · `GET …/{sourceType}/{id}/ar-experience` · `DELETE …/{sourceType}/{id}` |
| conversation | 7 | 세션 목록·상세·생성·삭제·상태저장 · 메시지 응답 · 텍스트 보관 |
| avatar | 4 | 소환세션 생성·상태저장 · 캡처저장 · 예절샷 프레임 조회 |
| stage | 3 | 세션 생성 · 세션 저장 · 캡처 저장 |
| track | 3 | 세션 생성 · 상태 저장 · 캡처 저장 |
| market | 3 | 거래 생성 · 주문 상세 · 구매 확정 |

명세서 CSV 총 **71개 중 `완료` 24 · `시작 전` 47**(엔드포인트 칸이 빈 것 26개).

**나머지 47개는 백엔드 `시작 전`이다.** 특히 커뮤니티 8개·마이페이지 5개·마켓 조회 계열은
명세서에 **엔드포인트 칸조차 비어 있다**(MoSCoW `Could`/`Won't have`).
→ 이미 만들어둔 `ICommunitySource` 는 **당분간 Mock 그대로 간다.** 이번 작업 범위 밖이다.

---

## 2. 앱 쪽 현재 상태

- `Assets/Project/Scripts/` 전체에 **`UnityWebRequest` 0건, `HttpClient` 0건, `async` 0건.** 완전 백지다.
- 다만 **이음매 관례는 이미 있다** — `Interfaces/ICommunitySource.cs`:

```csharp
// 콜백 방식인 이유: UnityWebRequest 가 코루틴이라 콜백이 그대로 맞고,
// Mock 은 즉시 done 을 부르면 되므로 호출부가 동일하다.
void LoadPosts(string board, string query, CommunitySort sort,
               Action<List<CommunityPost>> done, Action<string> fail);
```

`CommunityStore` 가 `MonoBehaviour` 싱글턴(= `Canvas/ScreenManager` 에 부착)으로 소스를 들고,
`OnChanged` 이벤트로 화면들이 다시 그린다. **이번에 만드는 것도 이 관례를 그대로 따른다.**
새 관례를 하나 더 만들면 읽는 사람이 두 가지를 배워야 한다.

- JSON 라이브러리: `com.unity.nuget.newtonsoft-json 3.2.1` 이 `packages-lock.json` 에 **있다.**
  단 `manifest.json` 에는 없다 — **AR Foundation 계열이 끌어온 간접 의존성**이다.

---

## 3. 결정 1 — 갤러리 3개부터 붙인다

**대안과 비교한 결과다.**

| 후보 | 왜 안 골랐나 |
|---|---|
| 24개 한 번에 | 전송·에러·이미지로딩 패턴이 검증되기 전에 24곳에 복붙된다. 틀렸을 때 24곳을 고쳐야 한다. |
| 홈 1개부터 | 가장 작지만 **딱 화면 하나 배선**이라 뼈대만 세우고 끝난다. 목록 페이징·이미지 로딩 문제를 안 건드려서 다음 슬라이스에서 똑같은 고민이 또 나온다. |
| AR 소환 4개부터 | 앱의 핵심 흐름이고 `multipart` 업로드까지 한 번에 간다. 하지만 **첫 슬라이스치고 무겁고**, `CaptureService` 재설계가 딸려와서 실패 시 되돌릴 범위가 크다. |
| 대화 7개부터 | 도메인이 가장 크다. 첫 슬라이스로 적합하지 않다. |

**갤러리를 고른 이유:**
1. **읽기 위주**라 실패해도 서버 데이터가 안 망가진다(삭제 1개만 쓰기).
2. `Screen_Gallery` 는 **이미 완성돼 있다** — 화면을 새로 만들 필요가 없어 API 배선에만 집중할 수 있다.
3. **`imageUrl` → `Texture` 로딩**이라는 새 문제를 여기서 한 번만 풀면 마켓·커뮤니티·홈이 전부 재사용한다.
4. **페이징**(`page`/`size`/`hasNext`)이 들어 있어 목록형 화면의 표준을 여기서 정할 수 있다.
5. 3개 안에 **GET·GET(path param)·DELETE** 가 다 들어 있어 `ApiClient` 의 메서드 4종 중 3종이 검증된다.

---

## 4. 결정 2 — 구조: `ApiClient` 코어 하나 + 도메인별 인터페이스

### 검토한 3가지

**A. 도메인별 인터페이스 + 코루틴 콜백** (= `ICommunitySource` 방식)
- 장: 기존 관례와 동일. Mock 으로 갈아끼울 수 있어 서버가 죽어도 화면 작업이 계속된다. 의존성 0.
- 단: 도메인 7개면 인터페이스 7개. **전송 코드가 구현체마다 복붙될 위험.**

**B. 단일 `ApiClient` 정적 클래스, 화면이 직접 호출**
- 장: 코드가 절반. 전송·에러 처리가 한 곳.
- 단: **Mock 자리가 없다.** 서버가 내려가면 갤러리 화면 작업 자체가 멈춘다. 화면 코드가 URL·JSON 을 알게 된다.

**C. `async/await` + Newtonsoft**
- 단: 이 코드베이스에 `async` 가 **한 줄도 없다.** 씬 전환 중 응답이 오면 파괴된 오브젝트를 건드려 죽고,
  `async void` 는 예외를 조용히 삼킨다. 안전하게 하려면 취소 토큰 배관을 직접 깔아야 한다.
  유저가 Unity/C# 을 처음 익히는 중이라 **새 개념 스택을 하나 더 쌓는 비용이 크다.**

### 결정: A + B 를 합친다 (둘은 배타적이지 않다)

```
┌──────────────────────────────────────────────────┐
│ GalleryController  (화면)                        │  ← URL·JSON 을 모른다
└───────────────┬──────────────────────────────────┘
                │ IGallerySource (done / fail 콜백)
        ┌───────┴────────┐
        │                │
┌───────▼──────┐  ┌──────▼─────────┐
│ApiGallerySrc │  │MockGallerySrc  │   ← 서버 없이도 화면 작업 가능
└───────┬──────┘  └────────────────┘
        │ ApiClient.Get/Post/Patch/Delete/Upload
┌───────▼──────────────────────────────────────────┐
│ ApiClient  ← UnityWebRequest · 타임아웃 · 에러파싱 │   딱 하나. 도메인이 늘어도 안 늘어난다
└──────────────────────────────────────────────────┘
```

- **전송 배관은 `ApiClient` 한 군데에만 있다** → B의 장점(복붙 없음)
- **화면은 인터페이스만 본다** → A의 장점(Mock 교체 가능, 화면 코드 불변)
- 도메인이 7개로 늘어도 `ApiClient` 는 그대로다. 늘어나는 건 얇은 구현체뿐이다.

### `ApiClient` 가 책임지는 것 (이것만)
1. base URL 조립 (`ApiConfig.BaseUrl` + 경로)
2. `UnityWebRequest` 생성·전송·해제 (`using`)
3. 타임아웃 (기본 10초. AR 흐름 중 무한 대기 금지)
4. 상태코드 판정 + 에러 본문 `{code, message}` 파싱 → `fail(message)` 로 전달
5. 성공 시 원문 JSON 문자열을 `done` 으로 전달

**책임지지 않는 것:** 재시도·캐시·토큰. 지금 필요 없다(YAGNI). 토큰은 로그인이 구현되면 4-2번에 한 줄 추가할 자리만 남겨둔다.

---

## 5. 결정 3 — JSON 은 `JsonUtility`(유니티 내장)

**Newtonsoft 를 안 쓰는 이유:** `manifest.json` 이 아니라 `packages-lock.json` 에만 있는
**간접 의존성**이다. AR Foundation 버전이 바뀌면 소리 없이 사라지고, 그러면 앱 전체가 컴파일 실패한다.
명시적으로 `manifest.json` 에 추가할 수도 있지만 — 이 응답들은 전부 평범한 객체·배열이라 **내장으로 충분하다.**
쓰지 않아도 되는 의존성을 추가하지 않는다.

**`JsonUtility` 의 제약과 대응:**

| 제약 | 대응 |
|---|---|
| **enum 을 이름이 아니라 숫자로 매핑한다.** `"CAPTURE"` 는 파싱 실패 | DTO 는 `string` 으로 받고 앱 enum 으로 **손으로 변환**. 모르는 값이 오면 경고 로그 + 건너뛴다 |
| 최상위 배열 불가 | 이 서버 응답은 **전부 객체**라 해당 없음 (`{galleryItems:[...], pagination:{...}}`) |
| `null` → 값 타입은 0 | `startPositionSeconds` 가 `int|null` 이다. **0과 null 을 구분해야 하면** DTO 에 `string` 으로 받아 파싱 |
| `private` 필드는 `[SerializeField]` 필요 | DTO 필드는 전부 `public` (DTO 는 전송용 구조체일 뿐) |

**DTO 는 서버 JSON 모양 그대로 만들고, 앱 모델과 분리한다.** 서버가 필드 이름을 바꿔도
변환 함수 한 곳만 고치면 되고, 화면 코드는 안 바뀐다.

---

## 6. 결정 4 — 갤러리는 서버 목록으로 **완전 대체**

지금 `GalleryController.photos` 는 **씬에 박아둔 더미 `Sprite` 목록**이고,
실기에서 찍은 사진은 `CaptureService` 가 **로컬 PNG 파일**로 따로 들고 있다.

**대체를 고른 이유:** 갤러리의 진실은 서버 하나여야 삭제·AR재경험이 서버 규칙과 정확히 맞는다.
로컬과 병합하면 **로컬 항목은 서버 `id` 가 없어서** 삭제·AR재경험을 예외 처리해야 하는데,
그건 유저가 못박은 **"꼼수 금지 — 조건문으로 비껴가지 말 것"** 에 정면으로 걸린다.

**⚠ 감수하는 부작용 (유저에게 이미 고지함):**
캡처 업로드 API(`stage`/`track`/`avatar` 도메인)는 **이번 슬라이스에 없다.**
그래서 이 슬라이스가 끝난 시점에는 **방금 찍은 사진이 갤러리에 안 보인다.**
`오늘 찍은 사진들`(`TodayPhotos`) 화면은 로컬 `CaptureService` 를 계속 쓰므로 거기서는 보인다.
→ 다음 슬라이스(캡처 업로드)에서 해소된다.

---

## 7. 갤러리 슬라이스 상세 설계

### 7-1. 새로 만드는 파일

| 파일 | 역할 | 줄 수(예상) |
|---|---|---|
| `App/Api/ApiConfig.cs` | base URL·타임아웃. `ScriptableObject` 로 빼 인스펙터에서 서버 교체 | ~25 |
| `App/Api/ApiClient.cs` | `UnityWebRequest` 전송 코루틴. Get/Post/Patch/Delete/Upload | ~140 |
| `App/Api/GalleryDtos.cs` | 서버 JSON 모양 그대로의 `[Serializable]` 구조체 | ~60 |
| `Interfaces/IGallerySource.cs` | 갤러리 데이터의 유일한 접점 | ~20 |
| `App/Api/ApiGallerySource.cs` | `ApiClient` 로 3개 호출 + DTO→앱모델 변환 | ~110 |
| `App/MockGallerySource.cs` | 서버 없이 도는 메모리 구현(기존 더미 `photos` 를 여기로) | ~50 |
| `App/GalleryStore.cs` | 소스 보유 + 캐시 + `OnChanged`. `CommunityStore` 와 동일 구조 | ~90 |

**`.cs.meta` 를 7개 손으로 만들어야 한다** — 유니티가 꺼져 있으면 자동 생성이 안 되고,
GUID 가 없으면 씬에서 스크립트를 못 쓴다. (`docs/HANDOFF.md` 2장)

### 7-2. 항목 키 — ★ `sourceType + id` 복합키

서버 응답에서 **`id` 가 겹친다.** 실측:

```
{"id":1,"sourceType":"INTERACTION_CAPTURE",...}
{"id":1,"sourceType":"TRACK_CAPTURE",...}
{"id":1,"sourceType":"STAGE_CAPTURE",...}
```

명세서에도 명시돼 있다 — *"`id`는 원본 테이블의 id를 그대로 반환하며, 프론트는 `sourceType`과 조합해 항목을 식별한다."*
삭제·AR재경험 경로가 `/{sourceType}/{galleryItemId}` 인 것도 같은 이유다.

→ **앱 내부 키를 `int id` 로 잡으면 사진 3장이 한 장으로 뭉개진다.**
`GalleryItem` 은 `sourceType`(string) + `id`(int) 를 **둘 다** 들고, 선택 상태도 이 조합으로 관리한다.
(지금 `GalleryController` 는 `HashSet<int> selected` 로 **리스트 인덱스**를 쓴다 → 복합키로 교체)

### 7-3. 서버 규칙 vs 현재 화면 — 어긋나는 곳

| | 서버 | 현재 `GalleryController` |
|---|---|---|
| 삭제 가능 | 캡처 3종만 (`GalleryDeletableSourceType`). **`ALBUM_IMAGE` 불가** | 아무 타일이나 체크·삭제됨 |
| AR 재경험 | `STAGE_CAPTURE`·`TRACK_CAPTURE` 만. **`INTERACTION_CAPTURE` 불가** | 해당 기능 미배선 |
| 탭 | `ALL` `CAPTURE` `ALBUM` `CONCERT_STILLCUT` | `Category` enum 4개 — **정확히 일치** ✅ |
| 목록 | 페이징(`page`/`size`/`hasNext`) | 전량 표시 |

### 7-3-1. ★ 결정 — `ALBUM_IMAGE` 도 체크박스를 띄운다 (**시안대로**, 2026-08-21 유저 지시)

`sian48/갤러리 편집*.svg` 6장을 실측한 결과다.

| 시안 | 상태 | 빈 체크원 | 선택(보라) |
|---|---|---|---|
| `갤러리.svg` | 전체 탭, 편집 꺼짐 | 0 | — |
| `갤러리 편집.svg` | 편집 켜짐 | **8** (타일 8개 **전부**) | 0 |
| `갤러리 편집-1.svg` | 2장 선택됨 | 6 | 2 |
| `갤러리 편집-2.svg` | 삭제 확인 모달 | 6 | 2 |

실측값 (지금 `GalleryController` 와 **이미 일치**한다 — 좌표는 안 건드린다):
- 2열 마소너리 — 좌열 `x 20~198` · 우열 `x 214~392` · **폭 178 · 간격 16 · 첫 행 y 176**
- 체크원 — 중심 `(40,196)` `r 9.5` + stroke → 타일 좌상단에서 **(20,20), 지름 20**
- 빈 체크 `fill=white stroke=#727886` → 선택 시 보라 `#A855F7`
- 탭 pill `(20,133) #7C3AED`, 첫 탭 글자만 흰색 → **「전체」 탭 상태**
- 우상단: 편집 `#A855F7` → 취소 `#9CA0AD` → 삭제 `#FF5B6F`

**시안은 「전체」 탭 편집 모드에서 8장 전부에 체크박스를 그린다.** 앨범/캡처 구분이 없다.
서버 규칙과 충돌한다는 점을 유저에게 보고했고, **유저 결정은 "시안대로"** 다.
→ **타입으로 체크박스를 걸러내지 않는다.** 앨범 사진도 선택된다.

**그래서 서버 거부를 정직하게 처리해야 한다.** 실측한 거부 응답:

```
DELETE /api/v1/users/me/gallery/ALBUM_IMAGE/1        → 400 {"code":"INVALID_REQUEST", ...}
GET    …/gallery/INTERACTION_CAPTURE/1/ar-experience  → 400 {"code":"INVALID_REQUEST", ...}
```

동작 규칙 (2026-08-21 유저 결정):
- 선택한 N장을 **각각 DELETE 로 보낸다**(서버에 일괄 삭제 API 가 없다).
- **화면은 시안대로 동작한다** — 고른 것을 목록에서 빼고 시안 토스트
  (`갤러리 삭제 토스트.svg` = 「사진이 삭제되었어요」)를 띄운다. 실패해도 같다.
- **실패는 `[Api]` 로그에 남긴다.** 화면은 시안대로 가되 로그에는 진실을 적는다.
- 갤러리에 다시 들어오면 서버가 진실이므로 **앨범 사진은 다시 보인다**(서버가 안 지웠으니까).
  0장으로 붕 뜨거나 목록이 깨지지 않는다 — 자기 복구된다.

> ⚠ 이 동작은 **"안 지워졌는데 지워졌다고 말하는 것"** 이다. 그 점을 유저에게 보고했고,
> **데모에서 눌러볼 기능이 아니라서 시안대로 간다**는 결정을 받았다(2026-08-21). 0장 참고.

### 7-4. 마소너리 레이아웃 문제

시안 갤러리는 **2열 마소너리**(열 폭 178, 간격 16) — 타일 높이가 **사진 비율**로 정해진다.
현재 코드는 `Sprite` 에서 비율을 읽는다:

```csharp
float ratio = p.image.rect.height / p.image.rect.width;
float h = columnWidth * ratio;
```

**서버는 이미지 폭·높이를 안 준다.** 즉 **이미지를 다 받아야 레이아웃이 정해진다.**

대응: 2단계로 그린다.
1. 목록 응답이 오면 **정사각(1:1) 자리표시자**로 즉시 배치한다 → 화면이 비어 보이지 않는다
2. 이미지가 도착할 때마다 그 타일의 높이를 실제 비율로 고치고 **그 열 아래를 다시 앉힌다**

전부 받은 뒤 한 번에 그리는 방법도 있지만, 20장을 다 기다리면 화면이 오래 빈다.

### 7-5. 이미지 로딩 — `ImageCache`

`imageUrl` → `Texture2D` 는 **갤러리 말고도 마켓·커뮤니티·홈이 전부 쓴다.** 처음부터 공용으로 만든다.
- URL 별로 **중복 요청 방지**(같은 URL 이 동시에 여러 번 요청되면 하나만 나가고 콜백을 모아둔다)
- 받은 `Texture2D` 를 메모리 캐시. 씬 전환에도 살아 있게 `DontDestroyOnLoad`
- ⚠ **`RawImage` 는 늘려 그린다** → 기존 `UiUtil.FitCover` 를 그대로 쓴다 (`HANDOFF` 4장)

### 7-6. 데이터 흐름

```
Screen_Gallery 켜짐
  └ GalleryController.OnEnable
      └ GalleryStore.Load(category)
          └ IGallerySource.LoadItems(category, page, size, done, fail)
              └ ApiGallerySource → ApiClient.Get("api/v1/users/me/gallery?...")
                  └ UnityWebRequest ──▶ 서버
                                    ◀── 200 {galleryItems, pagination}
              └ DTO 파싱 → List<GalleryItem> 변환
          └ 캐시 갱신 → OnChanged 발생
      └ GalleryController.Refresh() → 자리표시자로 타일 배치
          └ 타일마다 ImageCache.Load(url, tex => 높이 재계산 + 재배치)
```

---

## 8. 실패 처리

| 상황 | 동작 |
|---|---|
| 네트워크 없음 / 타임아웃 | `fail("네트워크에 연결할 수 없어요")` → `Toast.Show` + 목록은 **이전 캐시 유지**(빈 화면 금지) |
| 400 `INVALID_REQUEST` | 앱 버그다. `Debug.LogError` 로 요청 URL 을 남긴다. 토스트는 띄우지 않는다(유저가 할 게 없다) |
| 404 `GALLERY_ITEM_NOT_FOUND` | 이미 지워진 항목. 조용히 목록에서 빼고 다시 그린다 |
| 500 | `fail("잠시 후 다시 시도해 주세요")` |
| 이미지 1장 실패 | 그 타일만 자리표시자로 남긴다. 목록 전체를 죽이지 않는다 |

**모든 실패는 `[Api]` 접두 로그를 남긴다.** `HANDOFF` 5장 원칙 —
"안 된다"는 신고에 먼저 카운트·로그를 찍을 수 있어야 한다.

```
[Api] GET api/v1/users/me/gallery?category=ALL&page=0&size=20 → 200 (0.31s, 5건)
[Api] GET ... → 실패: Cannot connect to destination host
```

---

## 9. ★ 백엔드에 물어봐야 할 것 (막혀 있는 것)

### 9-1. `imageUrl` 이 URL 이 아니다 — **이게 안 풀리면 사진을 못 띄운다**

명세서 예시는 `https://cdn.example.com/stage-captures/capture-701.jpg` 인데 **실제 응답은:**

```
"imageUrl": "interaction_picture_001"
"imageUrl": "album_image_first_light_001"
```

AR 재경험 응답의 `videoUrl` 도 마찬가지다 — `"www.video_1.com"`. 스킴도 없다.
**시드 데이터가 더미인 건지, 이미지 호스팅이 아직 안 붙은 건지** 확인 필요.
붙는다면 **절대 URL 인지 상대 경로인지**(상대면 base URL 을 앱이 조립해야 한다).

### 9-2. 캡처 업로드는 `multipart/form-data` 다 (다음 슬라이스용 메모)

| 엔드포인트 | 폼 필드 |
|---|---|
| `POST …/stage-sessions/{id}/captures` | `image`(파일) · `capturedPositionSeconds` · `capturedAt` |
| `POST …/track-sessions/{id}/captures` | 〃 |
| `POST …/avatar-interaction-sessions/{id}/pictures` | `image`(파일) · `picturedAt` |

`CaptureService` 는 **로컬 PNG 파일이 원본**인 구조라(백그라운드 복귀 시 텍스처가 날아가서 그렇게 짰다)
파일 바이트를 그대로 올리면 된다. 시간 값은 **offset 없는 KST ISO 8601**(`2026-08-12T17:20:00`).

### 9-3. 유저에게 물어볼 것

**2026-08-21 — 셋 다 유저가 결정했다. 열린 것 없음.**

| # | 질문 | 결정 |
|---|---|---|
| 1 | `ALBUM_IMAGE` 타일 처리 | **시안대로.** 체크박스 전부 그린다 (7-3-1) |
| 2 | 삭제 실패 토스트 문구 | **실패해도 성공 토스트만.** 시안에 있는 것 하나로 간다 (7-3-1) |
| 3 | 목록 페이징 | **한 번에 전량** — `size=100` 한 번. 시안에 '더 보기'·로딩 표시가 없다. 페이징 UI 를 안 만든다 |
| 4 | 갤러리 → 상세 화면 | **이번엔 안 건드린다.** `detailScreen` 은 `CaptureDetail` 그대로 (`HANDOFF` 미해결 7번으로 남긴다) |

**남은 것은 9-1(`imageUrl`) 하나뿐이고, 그건 백엔드 손에 달려 있다.**

---

## 10. 검증 방법

씬을 건드리기 전에 **코드만으로** 확인한다.

1. **컴파일** — `HANDOFF` 2장 절차. 새 `.cs` 7개를 `rsp` 에 직접 추가해야 잡힌다.
2. **서버 대조** — `curl` 로 받은 실제 JSON 을 파일로 저장해두고, DTO 파싱 결과와 필드별로 대조한다.
3. **실기** — `adb logcat -s Unity:V | grep "\[Api\]"` 로 요청·응답·소요시간을 눈으로 본다.
4. **Mock 대조** — `GalleryStore.source` 를 `Mock` 으로 돌려도 화면이 똑같이 도는지 확인.
   여기서 깨지면 화면 코드가 서버를 알고 있다는 뜻이고, 그건 설계가 샌 것이다.

---

## 11. 이후 슬라이스 순서 (제안)

| 순서 | 도메인 | 이번 슬라이스에서 얻는 것을 어떻게 재사용하나 |
|---|---|---|
| 1 | **gallery 3** | `ApiClient` · `ImageCache` · 페이징 · 에러 처리 표준 |
| 2 | **home 1** | 배선만. `ApiClient` 그대로 |
| 3 | **avatar 4** | `multipart` 업로드가 처음 등장. `CaptureService` 와 접합 |
| 4 | **stage 3 · track 3** | avatar 와 업로드 구조가 동일 — 복사 수준 |
| 5 | **conversation 7** | 가장 큼. 실시간 응답 대기 UI 필요 |
| 6 | **market 3** | 주문/구매확정. 화면 완성돼 있음 |
| — | 커뮤니티·마이 | **백엔드 미구현.** `ICommunitySource` Mock 유지 |

---

## 12. 구현 현황 (2026-08-21)

**갤러리 슬라이스 코드 완료. 컴파일 0 에러. 씬은 안 건드렸다 — 인스펙터 배선만 남았다.**

### 만든 파일 (전부 신규, `.cs.meta` 도 손으로 생성)

| 파일 | guid |
|---|---|
| `App/Api/` (폴더) | `60aa69cc0ed74a8daf42636dd405a60b` |
| `App/Api/ApiConfig.cs` | `7140e919f95f402383bdba3f31fbb117` |
| `App/Api/ApiClient.cs` | `a78ab5e5bb304e48b09513a6d96131d7` |
| `App/Api/ImageCache.cs` | `61951f45a1074820a78bb9258396b1b6` |
| `App/Api/GalleryDtos.cs` | `9519d8eeaede4ca0a131100d5df9c50d` |
| `App/Api/ApiGallerySource.cs` | `d3c36cd45ebc4ab0ad691c4672c36f56` |
| `App/GalleryModels.cs` | `159c449cb25546d68c15bb78d8c39143` |
| `App/MockGallerySource.cs` | `e48a6add8a614f5987f0dd8fe03818c2` |
| `App/GalleryStore.cs` | `bc6526cb37d14d97aa2e6ee11014908d` |
| `Interfaces/IGallerySource.cs` | `a5397d3c45804b67bc1c4e3387500b05` |

`ImageCache` 는 설계서 7-1 목록에 없던 것이다 — 7-5 에서 필요하다고 적어놓고 파일 목록에 빠뜨렸다.

### 고친 파일
- `App/GalleryController.cs` — 데이터 출처를 씬 더미 목록 → `GalleryStore` 로.
  **좌표·색·시안 근거 주석은 전부 그대로다.** 바뀐 것:
  - `List<Photo> photos`(씬 직렬화) 제거 → `GalleryStore.Items`
  - `HashSet<int> selected`(리스트 인덱스) → **`HashSet<string>`(`sourceType:id` 복합키)**
  - 사진 자식이 `Image`(Sprite) → **`RawImage`**(서버는 `Texture2D`)
  - `Build()` 가 배치까지 하던 것을 **`Build()`(생성) + `Relayout()`(배치)** 로 분리.
    사진이 한 장 도착할 때마다 `Relayout()` 만 부른다 — 타일 20개를 매번 다시 만들지 않는다.
  - 체크 토글이 `Refresh()`(전체 재생성)를 부르던 것 → **체크 스프라이트만 교체.**
    안 그러면 받아둔 사진이 매 탭마다 깜빡인다.

### 검증한 것
- **컴파일** — `Assembly-CSharp.rsp` 에 새 9개를 추가해 통과. 에러 0 (경고 4개는 전부 원래 있던 것).
- **DTO 필드명 대조** — 실서버 응답 키와 `[Serializable]` 필드를 기계로 1:1 대조. 전부 일치.
  (`JsonUtility` 는 이름이 하나만 달라도 조용히 `null`/`0` 을 넣어서 눈으로 보면 놓친다.)
  `GalleryArExperienceItemDto.stage`/`track` 은 **서로 배타적**인 게 맞다 — 실측으로 확인:
  `STAGE_CAPTURE/1` → `contentType:"STAGE"` + `stage` 만, `TRACK_CAPTURE/1` → `"TRACK"` + `track` 만.
- **서버 거부 동작** — `DELETE /gallery/ALBUM_IMAGE/1` → 400, `INTERACTION_CAPTURE` AR재경험 → 400.

### ▶ 남은 것 — 유저가 유니티에서 할 배선
1. `Canvas/ScreenManager` 에 **`GalleryStore` 컴포넌트 추가** (`CommunityStore` 옆).
   `Source` 는 기본 `Api` 그대로 두면 된다.
2. (선택) Mock 으로 화면 작업하고 싶으면 `Mock Photos` 에 **`gallery_photo_1~8`** 을 넣는다.
   원래 `GalleryController.photos` 에 들어 있던 바로 그 8장이다
   (`Assets/Project/Sprites/Images/gallery_photo_[1-8].png`).
3. `GalleryController` 의 나머지 인스펙터 필드는 **건드릴 필요 없다** — 이름·타입이 그대로다.
   `photos` 칸만 사라진다.

### ⚠ 배선 후 실기에서 확인할 것
- `adb logcat -s Unity:V | grep "\[Api\]\|\[Gallery\]"` 에 요청·응답·건수가 찍히는지.
  `[Gallery] 소스 = Api` → `[Api] GET api/v1/users/me/gallery... → 200` → `[Api] 갤러리 5건`
- **사진은 안 뜬다** — 9-1 때문이다(`imageUrl` 이 URL 이 아님). 타일은 어두운 자리표시자로 뜨고
  **전부 정사각**이 된다(비율을 이미지에서 읽는데 이미지가 없으니까). 이건 예상된 상태지 버그가 아니다.
- `Screen_CaptureDetail` 의 **제목(`CapTitle`)은 안 바뀐다.** 서버 갤러리 응답에 제목 필드가 없어서
  지어내지 않고 그대로 뒀다. 날짜(`CapMeta`)만 `displayDate` 로 채운다.

---

## 12-2. 홈 슬라이스 (2026-08-21)

**코드 완료. 컴파일 0 에러. 씬 안 건드림.**

### 만든 파일

| 파일 | guid |
|---|---|
| `App/Api/HomeDtos.cs` | `392dae437817463c8788fe482fe5e574` |
| `App/Api/ApiHomeSource.cs` | `8e50f94dd90a465d9fd8553dcb36984d` (`MockHomeSource` 도 같은 파일) |
| `App/HomeController.cs` | `5d95006f929c40cfb7a18a687ad59ab9` |
| `Interfaces/IHomeSource.cs` | `f1ee35bdf913486fae1c25975306b01f` |

**Store 를 안 만들었다.** 홈은 화면 하나가 전부라 공유할 상대가 없다
(`GalleryStore`·`CommunityStore` 는 여러 화면이 같은 목록을 봐서 필요했다).

### 고친 파일 — `App/IntimacyBar.cs`

씬(=시안) 문구와 코드가 어긋나 있었다. **이 함수가 한 번도 불린 적이 없어서**
(홈이 씬 고정값으로 떠 있었다) 안 드러났던 것이다.

| | 씬/시안 실측 | 예전 코드 |
|---|---|---|
| `LevelText` | `Close` — **레벨 이름만** | `Lv.3 Close` ❌ |
| `ProgressText` | `Lv.4 까지 755 남았어요` | 같음 ✅ |
| `ProgressValue` | `1,245 / 2,000` | **안 채웠다** ❌ |

→ 씬 기준으로 고치고 `progressValue` 필드를 추가했다(인스펙터 드래그 1개 필요).

### 배선한 것

| 씬 오브젝트 | 서버 필드 |
|---|---|
| `SpeechBubble/SpeckerName` | `cubeOwnership.avatarNickname` ("루미") |
| `TopBar/DdayText` | `cubeOwnership.acquiredAt` 부터 오늘까지 **경과 일수** |
| `IntimacyCard` (IntimacyBar) | `affinity.level.level` · `levelName` · `currentXp` · `level.levelXp` |

### ⚠ 배선 안 한 것 — `user.profileCharacterImgUrl`

실제 값이 **`"CHAR_MINA"`** 다. URL 이 아니라 코드다.
`TopBar/ProfileIcon` 은 지금 `profile_avatar_rabbit.png` 인데, `CHAR_MINA` 가
어느 스프라이트인지 **근거가 없어서 매핑을 지어내지 않았다.**
→ 캐릭터 코드 목록과 대응 스프라이트를 받으면 붙인다.

### ★ 서버 데이터가 명세와 모순된다 — 친밀도 XP

명세서: `affinity.level.levelXp` = **"현재 레벨을 완료하는 데 필요한 최대 XP"**

실측: `currentXp = 120` · `levelXp = 100` → **진행률 120%.** 이미 넘긴 값이다.
남은 XP 를 계산하면 `100 - 120 = -20` 이라 `"Lv.3 까지 -20 남았어요"` 가 된다.

`currentXp` 가 **누적 총 XP** 이고 `levelXp` 가 **그 레벨의 시작 기준치**라면 앞뒤가 맞는데,
그러면 다음 레벨 기준치를 서버가 안 주므로 **진행바를 그릴 수가 없다.**

→ 지금은 **있는 값 그대로 쓰되 바는 100% 로 채우고 남은 XP 는 0 으로 막고,
`[Home]` 경고 로그를 남긴다.** 지어내지 않는다. 백엔드에 확인 필요:
*시드 데이터 문제인가, 아니면 `currentXp` 가 누적치인가? 후자면 다음 레벨 기준치도 필요하다.*

### 검증
- 컴파일 0 에러.
- **DTO 필드명 대조 6개 전부 일치** (`HomeResponseDto` / `user` / `cubeOwnership` / `cube` / `affinity` / `affinity.level`).
- D-day 검산: `acquiredAt=2026-08-07` → 오늘(2026-08-21) 기준 **14일**.

### ▶ 유니티에서 할 배선
1. `Canvas/Screen_Home` 에 **`HomeController` 추가** → 인스펙터에 3개 드래그
   `Speaker Name` ← `SpeechBubble/SpeckerName` · `Dday Text` ← `TopBar/DdayText` ·
   `Intimacy` ← `IntimacyCard`(IntimacyBar 컴포넌트)
2. `IntimacyCard` 의 **`IntimacyBar` 에 `Progress Value` ← `ProgressValue`** 드래그 (새 칸)

---

## 12-3. avatar 슬라이스 (2026-08-21) — ★ 서버 버그 발견

**소스 계층 완료. 컴파일 0 에러. 씬·AR 흐름은 안 건드렸다** (이유는 아래).

### 만든 파일

| 파일 | guid |
|---|---|
| `App/Api/AvatarDtos.cs` | `7cdc0dbb5ebf4a9aab752252c00efd81` |
| `App/Api/ApiAvatarSource.cs` | `d087b4e597c5402bbda4485ffd512e8c` (`MockAvatarSource` 도 같은 파일) |
| `Interfaces/IAvatarSource.cs` | `5ee0bf68df124793a88209222177d096` |

### 고친 파일
- `App/Api/ApiClient.cs` — **`Upload()` 추가**(multipart/form-data). 캡처 저장 3종이 전부 이 방식이다.
  ⚠ `Content-Type` 을 손으로 넣으면 **boundary 가 빠져** 서버가 못 읽는다.
  `UnityWebRequest.Post(url, sections)` 가 알아서 붙이므로 건드리지 않는다.
- `App/Api/ApiConfig.cs` — **`Iso(DateTime)` 추가.** 명세서 공통사항인
  *offset 없는 KST ISO 8601*(`2026-08-12T17:20:00`)을 만든다.
  ⚠ `"o"`/`"s"` 서식이나 `ToUniversalTime()` 을 쓰면 `Z`·오프셋이 붙어 서버가 다르게 읽는다.

### 서버 실측 — 4개 중 3개는 된다

| 엔드포인트 | 결과 |
|---|---|
| `POST …/avatar-interaction-sessions` | ✅ 201 `{"avatarInteractionSession":{"id":4}}` |
| `PATCH …/avatar-interaction-sessions/{id}` | ✅ 204. Body 없음(서버가 `ended_at` 을 스스로 찍는다). 두 번 부르면 409 `ALREADY_COMPLETED` — 명세대로 |
| `GET …/album/yezul-frames` | ✅ 200. 프레임 **1개** (`{"id":3,"title":"예줄 프레임","imageUrl":"album_image_memory_cube_001"}`) |
| `POST …/avatar-interaction-sessions/{id}/pictures` | ❌ **500 INTERNAL_SERVER_ERROR** |

**DTO 필드명 대조 4개 전부 일치.**

### ★★ 캡처 업로드가 서버에서 500 이다 — 앱 문제가 아니다

요청 모양이 맞다는 걸 **검증 응답으로 역산**했다. 서버가 단계별로 다른 코드를 준다:

| 보낸 것 | 응답 | 뜻 |
|---|---|---|
| `picturedAt` 빼고 | **400** `INVALID_REQUEST` | 필드 검증이 돈다 |
| `image` 빼고 | **400** `INVALID_REQUEST` | 파일 검증이 돈다 |
| 없는 세션 id | **404** `AVATAR_INTERACTION_SESSION_NOT_FOUND` | 세션 조회가 돈다 |
| **정상 요청** | **500** | 검증·조회를 다 통과한 뒤 **저장 단계에서 터진다** |

**3회 재현**(세션 6·7·8 전부 500). 갤러리는 5건 그대로 — 아무것도 저장 안 됐다.

→ **9-1 (`imageUrl` 이 URL 이 아님) 과 같은 뿌리로 보인다: 이미지 저장소가 서버에 안 붙어 있다.**
백엔드에 물어볼 것: *캡처 업로드 500 — 스토리지(S3/Blob 등) 설정이 빠진 건가?*

### ▶ 그래서 AR 흐름·프레임 시트는 배선하지 않았다

의도적으로 멈춘 것이다. 이유:

1. **캡처 업로드가 서버에서 죽는다.** 배선해봐야 실기에서 500 만 본다.
2. **예절샷 프레임도 못 쓴다.** 서버가 주는 `imageUrl` 이 `"album_image_memory_cube_001"` 이라
   프레임 그림을 못 받는다. 지금 `ARSummonController.frames` 는 **로컬 스프라이트로 잘 돌고 있다** —
   이걸 서버 목록으로 바꾸면 **되던 화면이 빈 목록이 된다.**
3. 세션 생성/종료는 **화면에 아무것도 안 보인다**(DB 기록용). 데모 가치가 0인데
   `ARFlowController` 를 건드리는 위험만 진다.

**대전제 2번 — "백엔드가 없다고 되던 걸 빼지 않는다" 의 반대 방향도 같다:
서버가 깨졌다고 되던 화면을 깨뜨리지 않는다.**

소스 계층(`IAvatarSource`)은 **다 만들어뒀다.** 백엔드가 고치면 `ARSummonController` /
`ARFlowController` 에 호출 3줄만 넣으면 된다:
- AR 소환 진입 → `CreateSession(id => sessionId = id)`
- 셔터 → `CaptureService.Capture` 콜백에서 `UploadPicture(sessionId, File.ReadAllBytes(entry.FilePath), entry.TakenAt)`
- 소환 종료/리포트 → `EndSession(sessionId)`

---

## 12-4. conversation 7 + market 슬라이스 (2026-08-24)

**코드 완료 · 컴파일 0 에러 · DTO 전 항목 실서버 대조 통과.**

### ★ 작업 규칙 추가 (유저, 2026-08-24)
> **"API 연결에서 내 맘대로 추가하거나 빼지 않는다. 목값을 서버값으로 대체하는 것만 한다."**
> 상세는 메모리 `feedback-work-rules` 7번. AR 쪽 변경(MusicPlayer.Pause 등)은 **현 상태 동결**.

### conversation (7개)
- 신규: `ConversationDtos` `ConversationModels` `ApiConversationSource` `MockConversationSource`
  `ConversationStore` `IConversationSource`
- 수정: `TalkListController`(목 3건 → 서버 목록) · `TalkDetailController`(목 메시지 → 서버, 전송 → 서버)
- 함정 실측: 목록 `conversationType` **필수**(빼면 400) · 메시지 **내림차순**으로 옴 + 유저/아바타
  `sentAt` 이 초 단위로 같음 → **sequenceNumber 로 정렬** · 아바타 응답은 실제 LLM(매번 다름) ·
  `conversationTitle` null 가능(유저 결정: **빈 채로 둔다**)
- 서버에 없는 것(감수): 대화 사진 첨부(업로드 경로 없음 → 로컬 표시만) · 안읽음/🖤(필드 없음 → 항상 false)

### market (3개 중 2개 배선)
- 신규: `MarketDtos` `ApiMarketSource`(+Mock) `IMarketSource`
- `MarketOrderDetailController` — 목 타임라인 → `GET /trades/{id}`. 아이콘(배지)은 서버가 안 주므로
  씬 유지, 라벨·시각·진행단계만 대체. `tradeId` 기본 3(시드의 진행 중 거래).
  실패 시 씬 값 유지 + 로그. 구매 확정 버튼 → `PATCH /complete`, 증명서를 `LastCertificate` 에 보관.
  화면은 시안대로 진행하고 실패는 로그(갤러리 삭제와 같은 결정).
- `MarketCertController` — 증명서 목값 6행을 `LastCertificate` 로 대체(라벨·복사·칩 구성은 씬 유지).
- **`POST /trades`(거래 생성)는 배선 안 함** — 대체할 목값이 없다(버튼이 화면 전환만 했고,
  `sellListingId` 가 앱 어디에도 없다 — 마켓 목록 API 미구현). 문의 7번.
- ★ 시드 한계(문의 7번): 판매 상품 3개 전부 거래 걸림(생성 409) · DELIVERED 거래 없음(확정 409).
  **지금 시드로는 구매 확정이 항상 실패한다** — 화면은 시안대로 돌고 로그에만 남는다.

### 남은 것
- stage 3 · track 3 — 세션 생성 Body 불명(문의 6번)으로 시작 불가
- avatar 흐름 배선 — 업로드 500(문의 1번)으로 보류
- 유니티 배선: `ScreenManager` 에 `GalleryStore`+`ConversationStore` 추가,
  `Screen_Home` 에 `HomeController` 추가(드래그 4개). 12·12-2장 참고.

---

## 13. 지키는 규칙 (`docs/HANDOFF.md` 1장)

- **씬은 안 건드린다.** 이번 작업은 `.cs` 신설이 전부다. 인스펙터 배선이 필요해지면 **먼저 묻고**,
  씬을 열기 전에 **"유니티 껐어?"** 를 소리내어 묻는다.
- **에셋을 만들지 않는다.** 자리표시자가 필요하면 기존 에셋을 재사용하거나 요청한다.
- **깃은 건드리지 않는다.** 이 문서도 커밋하지 않는다 — 명령어가 필요하면 적어만 준다.
- **꼼수 금지.** 서버 응답이 이상하면(9-1) 임의값으로 때우지 않고 **막혔다고 말한다.**
