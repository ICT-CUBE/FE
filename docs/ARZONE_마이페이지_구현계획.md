# 마이페이지 구현 계획

> 태스크 단위로 진행. 전역 제약은 `~/Desktop/CUBE/tools/.sdd/global.md` 와 **동일**하다
> (시안 준수 · 에셋 안 만듦 · 삭제 금지 · 깃 안 건드림 · `.cs.meta` 손으로 · Linear↔sRGB · 좌표 규칙).
> 다른 점은 **시안 경로**뿐: `~/Desktop/CUBE/sian84/{svg,png}` (8장).

**목표:** 시안 8장대로 마이페이지 묶음을 만들고, 이미 만들어진 커뮤니티·로그인 화면에 잇는다.

**시안:** `한이음_CUBE_E5N4-84`(SVG) / `-85`(PNG) → `~/Desktop/CUBE/sian84/{svg,png}`

---

## 유저가 정해준 것 (2026-08-15)

| 질문 | 답 |
|---|---|
| 진입점 | **프로필 아이콘**을 누르면 들어간다 |
| `내 큐브 보관함`·`거래 결제 및 정산 내역` | 갈 화면이 없다 → **배선 없이 그냥 둔다** |
| 설정 토글 | **로컬 저장**(`PlayerPrefs`) |
| 로그아웃 / 탈퇴 | 로그아웃 → **`Login`**, 탈퇴 → **`EmailSignup`** |

---

## 만들 것

| 시안 | 오브젝트 | screen id |
|---|---|---|
| `마이.svg` | `Screen_My` | `My` |
| `마이 프로필 편집.svg` | `Screen_MyProfileEdit` | `MyProfileEdit` |
| `설정.svg` | `Screen_MySettings` | `MySettings` |
| `내가 쓴 글.svg` | `Screen_MyPosts` | `MyPosts` |
| `댓글 단 글.svg` | `Screen_MyComments` | `MyComments` |
| `로그아웃 확인.svg` | `Modal_Logout` | (Canvas 직계 오버레이) |
| `탈퇴 확인.svg` | `Modal_Withdraw` | ˝ |
| `마이 토스트 모음.svg` | — | 기존 `Toast.Show()` |

## 이미 있는 것 (재사용 — 새로 만들지 마라)

```
toggle_on.png / toggle_off.png     Sprites/UI · 108×60(=36×20 @3x)   설정 토글
avatar_male_circle.png    07ab9283bde7000f70b4ddae4167ce6a          프로필 3종
avatar_female_circle.png  c75d94606ccc4a78058bef01e227a24c
avatar_animal_circle.png  750b8c4be3edf0038bf56bc11796044d
ic_arrow_left.svg / ic_arrow_right.svg / bg_round16.svg / bg_round12.svg
Modal_ArchiveDelete                씬 · 확인 모달의 복제 원본
CommunityController                커뮤니티 카드 생성 로직 (내가 쓴 글·댓글 단 글이 같은 카드다)
CommunityStore.Instance            .Posts · .OnChanged · .Find(id)
Toast.Show(string, float bottom = 58f)
PlayerPrefs 선례                    App/LoginModeSwitcher.cs
```

**배선 대상 (전부 지금 죽어 있다 — `ScreenButton` 이 없다)**
```
Canvas/Screen_Home/TopBar/ProfileIcon                                 44×44
Canvas/Screen_Hub/TopBar/ProfileIcon                                  44×44
Canvas/Screen_Community/CommunityScroll/Viewport/Content/ProfileIcon   44×44
Canvas/Screen_Market/MarketScroll/Viewport/Content/ProfileImg          56×56
```

---

## Task 1: `Screen_My` (마이페이지)

헤더(`<` · "마이페이지" · 설정 톱니) · 프로필 줄(아바타 · 닉네임 · 이메일 · `편집` 알약) ·
메뉴 카드 1장 안에 6행 + 구분선 2개:
`내 큐브 보관함 / 인벤토리 >` · `거래 결제 및 정산 내역 >` — 구분선 —
`내가 쓴 글  4 >` · `댓글 단 글  2 >` — 구분선 — `탈퇴하기`(빨강) · `로그아웃`

- 행 아이콘 6개는 시안에서 확인하고 **프로젝트에 있는 것만 쓴다. 없으면 만들지 말고 BLOCKED 로 보고.**
- `내 큐브 보관함` · `거래 결제 및 정산 내역` 은 **배선하지 않는다**(유저 지시).
- `내가 쓴 글` · `댓글 단 글` 의 숫자는 `CommunityStore` 에서 센다(4·2 는 시안 목업값).
- 톱니 → `MySettings`, `편집` → `MyProfileEdit`, 두 목록 행 → `MyPosts`/`MyComments`,
  `탈퇴하기` → `Modal_Withdraw`, `로그아웃` → `Modal_Logout`.
- **진입점 배선**: 위 프로필 아이콘 4곳에 `ScreenButton(targetScreen: My)`.

## Task 2: `Screen_MyProfileEdit` (프로필 편집)

`<` · "프로필 편집" · `프로필 캐릭터 선택`(아바타 3개, 선택된 것에 **보라 링 + 체크 뱃지**) ·
`닉네임` 입력(오른쪽 `X` 지우기) · 하단 `변경사항 저장하기`(보라, 폭 넓은 버튼).

- 아바타 3종은 위 GUID 그대로. 선택 상태는 `Screen_AvatarName` 의 `AvatarNameBinder` 방식을 참고.
- 저장하면 `Screen_My` 의 아바타·닉네임에 반영되고 시안의 토스트를 띄운다(문구는 `마이 토스트 모음.svg` 에서 읽어라).

## Task 3: `Screen_MySettings` (설정)

⚠ **시안 프레임이 412×1022 다 — 스크롤 화면이다.** 다른 화면과 같은
`~Scroll`(ScrollRect) → `Viewport`(RectMask2D) → `Content` 구조로.

섹션 6개(제목 + 카드): `알림 및 정보 설정` / `아바타 알림` / `미션 및 콘텐츠` / `마켓 거래` /
`커뮤니티` / `서비스 정보`. 토글 7개, 마지막 섹션은 `이용약관 및 에스크로 운영 정책 >` 과
`현재 빌드 버전 정보  v1.4.0-release`.

- 토글은 `toggle_on.png`/`toggle_off.png` 를 갈아끼운다. **시안의 기본 on/off 상태를 그대로** 읽어 넣어라.
- **`PlayerPrefs` 로 로컬 저장**(유저 지시). 키 이름은 `arzone.settings.<항목>` 처럼 접두사를 붙인다.
  `App/LoginModeSwitcher.cs` 의 사용 방식을 따라라.
- `이용약관…` 은 갈 화면이 없다 → **배선하지 않는다.**

## Task 4: `Screen_MyPosts` · `Screen_MyComments`

둘 다 `<` + 제목 + **커뮤니티 글 카드 목록**이다. 칩·검색·정렬·⋮ 가 **없다.**

- **`CommunityController` 의 카드 생성 로직을 재사용**해라. 복붙하지 말고 공유하는 형태로 —
  다만 `CommunityController` 를 깨지 않는 선에서. 어떻게 했는지 리포트에 근거를 적어라.
- `MyPosts` = `mine == true` 인 글. `MyComments` = 내가 댓글을 단 글
  (`replies` 에 작성자가 "나" 인 댓글이 있는 글).
- 카드를 누르면 커뮤니티 상세(`CommunityPost`)로 간다 — `CommunityPostController.Target` 에 id.

## Task 5: `Modal_Logout` · `Modal_Withdraw`

**`Modal_ArchiveDelete` 를 복제**하고 문구만 바꾼다(커뮤니티 `Modal_PostDelete` 와 같은 방식).
문구·버튼 라벨은 `로그아웃 확인.svg` · `탈퇴 확인.svg` 에서 **읽은 그대로**.

⚠ 복제 시 원본이 **접힌 여러 줄 YAML 스칼라**일 수 있다 — `[^\n]*` 로 치환하면 뒷줄이 남는다.
(커뮤니티에서 실제로 Critical 이 났다.)

- 로그아웃 확정 → `ScreenManager.Show("Login")`
- 탈퇴 확정 → `ScreenManager.Show("EmailSignup")`
- ⚠ **모달·팝업의 MonoBehaviour 를 꺼진 오브젝트에 붙이지 마라.** 유니티가 `Awake` 를 안 부른다
  (커뮤니티에서 ⋮ 메뉴가 통째로 죽었던 Critical). 싱글턴이 필요하면 `Canvas/ScreenManager` 에 붙인다.

## Task 6: 토스트 + 전체 흐름 점검

`마이 토스트 모음.svg` 의 문구를 **읽어서** 해당 동작에 연결한다. **지어내지 마라.**
전체 흐름: 프로필 아이콘 → 마이페이지 → 편집/설정/내가 쓴 글/댓글 단 글/탈퇴/로그아웃 →
각 화면에서 뒤로. 끊긴 곳을 목록으로.

---

## 검증 (태스크마다)

1. 컴파일 에러 0 (새 `.cs` 는 csproj 에 없으니 rsp 재생성. `$SP=/tmp/arzone_build`)
2. `python3 arb.py verify` → `중복 [] · 잘림 0 · 미해결참조 0`
3. 실측 대조표 (글자 크기는 잉크 폭 역산, 굵기는 **획두께/폰트크기 비율** —
   SemiBold·Bold 군 0.111~0.125 / Regular 군 0.071~0.083)


## ⚠⚠ 반복된 실수 — `fill-opacity` 가 있다고 유리 그라디언트가 아니다

**두 번 똑같이 났다** (커뮤니티 글 카드, 설정 1번 카드). 시안 SVG 에 `fill-opacity="0.5"` 가 보이면
"유리다 → `GlassGradient`" 로 넘어가는데, **대부분은 그냥 평평한 반투명 면**이다.

**반드시 3x PNG 로 카드 네 모서리를 재서 평평한지 확인할 것.**
- 평평하면: 시안 합성값을 **불투명하게** 칠한다. `#0F0D17` α0.5 over `#050505` = **`#0A090E`**(10,9,14).
- 그라디언트면: 네 모서리 값이 크게 벌어진다(예: 24.8 → 5.2). 그때만 `GlassGradient`.

`GlassGradient` 는 **아바타 홈 친밀도 카드의 대각 그라디언트 전용**이고,
마켓 카드에 쓴 것도 유저가 "홈 카드처럼 유리로" 라고 **지시했기 때문**이다. 기본값이 아니다.

## 커뮤니티에서 데인 것 — 반복하지 말 것

- **다른 화면 방식을 가져올 때 그 화면 시안을 반드시 다시 잰다.** (카드에 유리 그라디언트를 잘못 옮겨 붙였다)
- **시안 프레임의 주석을 먼저 읽는다.** "각 버튼이 클릭된 상태" 를 못 보고 눌림 배경을 상시로 넣었다.
- **아이콘은 캔버스 대비 내용 비율을 확인한다.** `ic_send.png` 는 108² 중 86²(79.6%)라 박스를 그만큼 키워야 했다.
- **`[SerializeField]` 를 고치면 씬 직렬화값도 같이 고친다.** 유니티는 씬 값을 쓴다.
- **에이전트가 "시안에 없다/에셋이 없다"고 뺀 것은 절반이 틀렸다.** 그런 보고는 직접 확인한다.
