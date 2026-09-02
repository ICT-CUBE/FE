# ARZONE 커뮤니티 설계

2026-08-14 · 시안 `한이음_CUBE_E5N4-76`(SVG) / `-77`(PNG) 10장 기준

---

## 1. 무엇을 만드나

게시판 두 개(자유게시판·인증게시판)를 가진 커뮤니티. 글을 쓰고, 목록에서 찾아보고,
열어서 좋아요·댓글을 달고, 남의 글은 신고하고 내 글은 지운다.

진입점은 **탭바 가운데 사람 아이콘(`TabFriend`)**. 지금 이 탭에는 `ScreenButton` 이 없어서
눌러도 아무 일도 일어나지 않는다. 여기에 배선한다.

---

## 2. 화면 분해

시안은 10장이지만 실제로 만들 오브젝트는 **화면 3 + 시트 1 + 팝업 1** 이다.
나머지는 같은 화면의 다른 상태다.

| 만들 것 | 대응 시안 | 비고 |
|---|---|---|
| `Screen_Community` | 커뮤니티 · 커뮤니티-1 · 검색결과 없음 | 목록 |
| `Screen_CommunityWrite` | 글쓰기 · 글쓰기-1 · 글 입력안한 상태(토스트) | 글쓰기 |
| `Screen_CommunityPost` | 글 상세 · 글 상세 댓글 | 상세 |
| `Sheet_CommunityPhoto` | 사잔첨부 · 사잔첨부 사진 선택 | 사진 선택 바텀시트 |
| `Popup_PostMenu` | ⋮ 메뉴 (신고하기 / 삭제하기) | 목록·상세 공용 |

- **검색결과 없음**은 별도 화면이 아니라 목록의 빈 상태다.
- **토스트 2종**("게시글이 등록되었습니다." / "댓글이 등록되었습니다.")은 기존 `Toast.Show()` 를 쓴다.

### 2.1 Screen_Community (목록)

- 헤더: 제목 "커뮤니티" · 프로필 원 · `새 글 쓰기` 아웃라인 버튼
- 게시판 칩: `전체` / `자유게시판` / `인증게시판` (선택된 것만 보라 알약)
- 검색바: "검색어를 입력하세요" + 돋보기
- 정렬: 우측 `최신순 ⌄`
- 글 카드 리스트 (스크롤)
  - 게시판 뱃지 · 우상단 `⋮`
  - 제목(1줄, 넘치면 말줄임) · 본문(2줄 말줄임)
  - `작성자 · 시간`
  - `♡ 좋아요수` `💬 댓글수`
- 하단: 탭바(가운데 활성) + AR 버튼

카드 배경은 마켓 카드와 같은 유리다 → `GlassGradient` 재사용.

### 2.2 Screen_CommunityWrite (글쓰기)

- 헤더: `<` · "글쓰기" · 우측 `등록`
- 게시판 선택 칩: `자유게시판` / `인증게시판` (전체 없음 — 반드시 하나 고른다)
- 제목 입력 + 밑줄, 내용 입력(멀티라인)
- 하단: `🖼 사진 (n/5장)` — 누르면 사진 선택 시트

**등록 활성 조건: 제목과 내용이 둘 다 비어 있지 않을 것.** (시안 본문에 명시돼 있다)
비활성 상태에서 누르면 토스트로 안내한다.

### 2.3 Screen_CommunityPost (글 상세)

- 헤더: `<` · 게시판 이름
- 작성자 프로필 · 닉네임 · 시간 · 우측 `⋮`
- 제목 · 본문(전문, 말줄임 없음)
- 사진 가로 스크롤 (있을 때만)
- 구분선 → `♡ 좋아요수` `💬 댓글수` → 구분선
- "댓글" 헤더 + 댓글 목록 (없으면 "아직 댓글이 없습니다.")
- 화면 하단 고정 입력바: "댓글을 입력하세요" + 전송 버튼

좋아요는 토글이고, 누르면 하트가 채워지며 숫자가 오른다.

### 2.4 Sheet_CommunityPhoto (사진 선택)

- 핸들 · `<` · 제목 `사진 선택 (n/5)` · 우측 `첨부`
- 3열 사진 그리드, 각 카드 좌상단에 선택 뱃지
  - 미선택: 흰 테두리 빈 원
  - 선택: 보라 원 + **고른 순서 번호(1~5)**
- 최대 5장

**기존 `Sheet_ImagePicker`(텍스트 대화용)는 건드리지 않고 새로 만든다.** 그쪽은
`TalkDetailController` 를 직접 참조하고 있어서 공용화하려면 그 참조를 걷어내야 하는데,
대화 흐름에 회귀를 낼 이유가 없다. 레이아웃 수치는 그쪽 주석의 실측값을 참고하되
번호 뱃지·(n/5) 제목·`첨부` 버튼은 이 시안대로 새로 잰다.

### 2.5 Popup_PostMenu (⋮ 메뉴)

`신고하기` / `삭제하기`(빨강) 두 줄짜리 팝업. 누른 `⋮` 위치 근처에 뜬다.
목록 카드와 상세 양쪽에서 같은 팝업을 재사용하고, 대상 글 id 만 갈아끼운다.

### 2.6 Modal_PostDelete (삭제 확인)

`삭제하기` 를 누르면 확인 모달을 띄운다. **기존 `Modal_ArchiveDelete` 를 복제**해서 문구만 바꾼다.

| | 원본 (대화 아카이브) | 커뮤니티 |
|---|---|---|
| Title | 대화를 삭제하시겠습니까? | **게시글을 삭제하시겠습니까?** |
| Sub | 삭제한 대화는 복구할 수 없습니다. | **삭제한 게시글은 복구할 수 없습니다.** |
| 버튼 | 취소 / 삭제(빨강) | 그대로 |

구조도 그대로 쓴다: `Dim` · `Card` · `CardBorder` · `IconWarn`(느낌표 원) ·
`Title` · `Sub` · `BtnCancel`+`LabelCancel` · `BtnConfirm`+`LabelConfirm`.
카드는 (20,−311) 372×272, 버튼은 163×53 / 164×54.

---

## 3. 데이터

마켓은 컨트롤러가 목업 리스트를 직접 들고 있지만, 커뮤니티는 화면 간에 상태가 오간다:

- 글쓰기에서 등록한 글이 **목록 맨 위에** 나타난다
- 상세에서 누른 좋아요·단 댓글이 **목록 카드의 숫자에** 반영된다

그래서 공유 저장소가 하나 필요하다.

```csharp
[Serializable] public class CommunityPost {
    public string id;
    public string board;          // "자유게시판" | "인증게시판"
    public string title;
    public string body;
    public string author;
    public string timeText;       // "방금 전" · "2시간 전"
    public int    likes;
    public int    comments;
    public bool   liked;          // 내가 누른 상태
    public bool   mine;           // 내 글이면 ⋮ 에 삭제하기가 뜬다
    public List<Sprite> photos;
    public List<CommunityComment> replies;
}

[Serializable] public class CommunityComment {
    public string author, timeText, body;
}
```

---

## 4. API 연결을 위한 이음매

나중에 서버를 붙일 자리를 지금 만들어 둔다. 프로젝트의 기존 `Interfaces/` 격리 계층
(`IObjectDetector` 가 Mock ↔ Vuforia 를 갈아끼우는 방식)과 같은 모양으로 간다.

```csharp
// Interfaces/ICommunitySource.cs
public interface ICommunitySource
{
    void LoadPosts(string board, string query, CommunitySort sort,
                   Action<List<CommunityPost>> done, Action<string> fail);
    // CommunitySort 는 지금 Latest 하나만 둔다. 드롭다운이 열린 시안이 없어서
    // 다른 항목을 지어내지 않는다(§8-2). 항목이 정해지면 enum 에 추가만 하면 된다.
    void CreatePost(CommunityPost draft, Action<CommunityPost> done, Action<string> fail);
    void ToggleLike(string postId, Action<int, bool> done, Action<string> fail);
    void AddComment(string postId, string body, Action<CommunityComment> done, Action<string> fail);
    void DeletePost(string postId, Action done, Action<string> fail);
    void ReportPost(string postId, Action done, Action<string> fail);
}
```

- **콜백 방식**으로 둔다. `UnityWebRequest` 는 코루틴이라 콜백이 그대로 맞고,
  지금 Mock 은 즉시 콜백을 부르면 되므로 호출부 코드가 서버 붙일 때 안 바뀐다.
- `fail` 을 따로 둬서 나중에 네트워크 오류를 토스트로 띄울 자리를 남긴다. Mock 은 부르지 않는다.

구현체 둘:

| 구현체 | 지금 | 나중 |
|---|---|---|
| `MockCommunitySource` | 메모리 리스트. 시드 데이터는 `CommunitySeed`(ScriptableObject) 에서 읽는다 | 서버 없이 시연할 때 계속 쓴다 |
| `ApiCommunitySource` | 없음 | `UnityWebRequest` 로 REST 호출. 이 파일만 새로 쓰면 된다 |

`CommunityStore`(씬 MonoBehaviour)가 현재 소스를 들고 캐시된 목록을 관리하고,
바뀌면 `OnChanged` 이벤트로 목록·상세를 다시 그리게 한다.
소스 교체는 **인스펙터에서 드롭다운 하나** 로 끝나게 한다.

> 서버 스펙(엔드포인트·필드명·페이지네이션·인증)이 아직 없다. 나오면 `ApiCommunitySource`
> 하나만 새로 쓰고 `CommunityPost` 의 필드명을 서버 JSON 에 맞춰 매핑한다. 그때까지
> 화면 코드는 `ICommunitySource` 만 보고 있으므로 손댈 곳이 없다.

---

## 5. 컨트롤러

| 스크립트 | 하는 일 |
|---|---|
| `CommunityController` | 목록. 게시판 필터 · 검색 · 정렬 · 카드 생성 · 빈 상태 |
| `CommunityWriteController` | 글쓰기. 게시판 선택 · 등록 활성 조건 · 사진 n/5 · 등록 후 토스트 |
| `CommunityPostController` | 상세. 좋아요 토글 · 댓글 추가 · 사진 가로 스크롤 |
| `CommunityPhotoSheet` | 사진 선택. 3열 그리드 · 번호 뱃지 · 최대 5 · 첨부 |
| `PostMenuPopup` | ⋮ 메뉴. 신고 / 삭제, 대상 글 id 를 받아 동작 |
| `CommunityStore` | 소스 보유 · 목록 캐시 · 변경 이벤트 |

카드·댓글 행은 마켓과 같이 **코드로 생성**한다(`UiUtil.MakeImage` / `MakeText`).
씬에는 컨테이너와 컨트롤러만 둔다.

---

## 6. 에셋

새로 받은 것 (2026-08-14, 유저 제공 · 60×60 흰색 투명배경):

| 파일 | 쓰임 | 넣을 곳 |
|---|---|---|
| `한이음_CUBE_E5N4-82.zip` 의 `icon.png` | 글쓰기 하단 사진 첨부 아이콘 | `Sprites/Icons/ic_photo_attach.png` |
| `icon-12.png` | 목록 카드·상세의 댓글 말풍선 | `Sprites/Icons/ic_comment.png` |

이미 있어서 재사용하는 것: `ic_heart.svg` · `ic_more_vert.svg` · `ic_report.svg` ·
`ic_trash.svg` · `ic_send.png` · `ic_search.svg` · `bg_round16` / `bg_round12` · `circle`

---

## 7. 안 하는 것

- **서버 연동 자체.** 이번엔 이음매(`ICommunitySource`)와 Mock 구현까지만.
- **이미지 업로드.** 사진은 기기 캡처(`CaptureService`)에서 고르는 것까지고, 업로드는 API 나오면.
- **기존 `Sheet_ImagePicker` 공용화.** 대화 흐름 회귀를 피해 새로 만든다.
- **무한 스크롤/페이지네이션.** 서버 스펙이 정해지면 `LoadPosts` 에 인자를 추가한다.

---

## 8. 유저가 정해준 것 (2026-08-14)

1. **작성자 프로필** — 회원가입에서 고르는 아바타 3개를 그대로 돌려 쓴다.
   `Screen_AvatarName` 의 `AvatarNameBinder.profiles` 에 물려 있는 것과 같은 스프라이트:
   `avatar_male_circle.png` · `avatar_female_circle.png` · `avatar_animal_circle.png`
2. **정렬** — **최신순 하나만.** 드롭다운은 만들지 않는다.
3. **삭제 확인** — §2.6. `Modal_ArchiveDelete` 복제 + 문구 교체.

### 아직 안 정해진 것

- **신고 후 동작** — 시안에 다음 화면이 없다. 일단 `Toast.Show("신고가 접수되었습니다.")` 로 두고,
  다른 게 필요하면 나중에 바꾼다. 서버가 붙으면 `ICommunitySource.ReportPost` 가 실제로 호출된다.
