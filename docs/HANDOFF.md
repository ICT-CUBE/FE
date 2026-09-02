# ARZONE — 작업 인계 문서

> 이 파일 하나만 읽으면 다른 계정·다른 기기에서도 그대로 이어받을 수 있게 쓴 것이다.
> 계정 메모리(`~/.claude/.../memory/`)는 옮겨오지 못하므로 **여기가 원본**이다.
> 마지막 갱신: 2026-08-25

---

## 0. 30초 요약

**ARZONE Special Edition AR Album CUBE** — AR 팬덤 앨범 앱 (한이음 드림업, ~2026-10-30).
폰 먼저, 이후 Meta Quest 3, AR 글래스는 열어둠. 유저는 **AR 리드 겸 프론트 전체 담당**(1인).

- 진짜 프로젝트: `~/Desktop/ARZONE-App` (Unity 6000.3.17f1, URP, Android/IL2CPP)
- 씬: `Assets/Project/Scenes/Phone.unity` · 네임스페이스 `ARZone` · 캔버스 기준 **412×893**
- 작업 도구·문서: `~/Desktop/CUBE/` (`tools/`, `docs/`, `sian48/`, `sian90/`)
- adb: `/Applications/Unity/Hub/Editor/6000.3.17f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`

---

## 1. 유저가 못박은 규칙 — 예외 없이 지킬 것

1. **CUBE 프로젝트 밖 파일은 건드리지 않는다.** 예외는 `~/Downloads`(디자이너 에셋 확인용)뿐.
2. **구도 변경 금지.** 시안에 없는 오브젝트는 **끄는 것(`m_IsActive: 0`)은 OK, 삭제는 금지.**
   새로 만들거나 옮기는 건 **먼저 묻는다.** "물어볼 만큼 큰 건 아니다" 라는 판단 자체가 내 몫이 아니다.
3. **무조건 시안(와이어프레임)대로.** 눈대중 금지. 실측해서 숫자로 맞추고, 맞췄으면 숫자로 대조해 보여준다.
4. **꼼수 금지.** 하드코딩·조건문 회피 금지. 못 하겠으면 **못 한다고 말한다.** 근사치로 때우지 않는다.
5. **에셋은 내가 만들지 않는다.** 시안 SVG 에서 `<path>` 를 떼어내는 것도 '만드는 것'이다.
   → 없으면 **"이건 예외로 제가 만들까요?"** 하고 **묻는다.** 대답 없으면 안 만든다.
   (`bg_round*`·`bg_pill*` 같은 자체 생성 계열은 물어보면 대개 허락된다. 기존 에셋 재사용은 자유.)
6. **깃은 절대 건드리지 않는다.** `git`·`gh` 실행 금지. 명령어만 적어주고 실행은 유저가 한다.

### 분업
- **유저** — 유니티 에디터 작업(오브젝트 생성, 컴포넌트 부착, 인스펙터 참조 연결, 프리팹 생성)
- **Claude** — `.cs` 직접 작성/수정 + **씬/프리팹 YAML 직접 편집**(위치·크기·앵커·피벗·정렬·문구·토큰·스프라이트·`.meta`)
- **★ 씬을 건드리기 전에 반드시 "유니티 껐어?" 라고 소리내어 묻고 답을 기다린다.**
  `ps` 확인만으론 부족하다 — 저장 안 한 변경이 있으면 나중에 유니티가 덮어쓴다.
- `.cs` 도 유니티가 켜져 있으면 먼저 묻는다(저장 시 리컴파일 → 플레이 중이면 끊긴다).
- 유저는 Unity/3D 개념이 처음이다. **한 번에 하나씩**, 작게. 안 급한 데서 막히면 건너뛰라고 말해줄 것.

---

## 2. 도구 (`~/Desktop/CUBE/tools/`)

| 파일 | 용도 |
|---|---|
| `arb.py` | 씬/프리팹 YAML 조회·편집. `verify` · `tree <이름> [깊이]` · `find` · `body <id>` · `comp <이름>` |
| `dumpscreen.py` | 화면 하나를 **스프라이트 이름·TMP 문구·앵커/피벗까지** 트리로 출력 (`arb.py tree` 보다 자세함) |
| `svgmeasure.py` | 시안 SVG 실측(transform 반영). **`--all` 없으면 12px 미만이 걸러진다** |
| `fontsize.py` | 시안 글자 bbox 폭으로 폰트 크기 역산 (Figma SVG 는 글자가 path 라 크기가 없다) |
| `renderscreen.py` | 씬을 PIL 로 대충 그려봄 (유니티 없이 좌표 확인) |
| `vrminfo.py` / `vrmparts.py` | VRM 삼각형·머티리얼·모프타겟·본·텍스처 해상도 조사 |
| `mkui.py` `iconcut.py` `mkglass.py` `band.py` `diffsian.py` `cmpscreen.py` | 화면 생성·아이콘 컷·유리 재도색·SVG 밴드 렌더·시안 비교 |
| `p_*.py` | 지금까지 실행한 씬 패치 스크립트 (무엇을 왜 고쳤는지 주석에 남아 있음) |

`arb.save()` 가드: **유니티 켜져 있으면 거부** · 블록 수 감소하면 거부 · 백업 `Phone.unity.bak<최대+1>` 자동.
편집 후엔 항상 `python3 arb.py verify` → `중복 0 · 잘림 0 · 미해결참조 0` 확인.

### 컴파일 검증 (유니티 없이)
```bash
cd ~/Desktop/ARZONE-App
RSP=$(ls -t Library/Bee/artifacts/*.dag/Assembly-CSharp.rsp | head -1)
grep -v VuforiaLicense.cs "$RSP" > /tmp/ac2.rsp
S=/Applications/Unity/Hub/Editor/6000.3.17f1/Unity.app/Contents/Resources/Scripting
"$S/NetCoreRuntime/dotnet" "$S/DotNetSdkRoslyn/csc.dll" @/tmp/ac2.rsp -out:/tmp/ac.dll
```
`Assets/VuforiaLicense.cs` CS2001 은 원래 있던 것 — 걸러내고 볼 것.
**새로 만든 `.cs` 는 rsp 에 없으니 직접 추가**해야 검증된다.
**새 `.cs` 마다 `.cs.meta` 를 손으로 만들어야 한다** (유니티가 꺼져 있으면 안 생기고, GUID 가 없으면 씬에서 못 쓴다).

---

## 3. 시안 실측

- 상시 폴더: **`sian48/`**(구판 SVG) · **`sian90/`**(2026-08-21 개정판, 3x PNG) · `sian76/`(커뮤니티) · `sian84/`(마이)
- **개정판이 우선.** 같은 화면인데 값이 다른 실제 사례: 입력창 채움 `#242831`(구) → `#181A20`(개정판)
- **SVG 가 있으면 SVG 우선** — PNG 밴드 실측은 1~5px 틀린다. 단 **유리 테두리는 SVG 에 없고 PNG 에만 있다.**
- `svgmeasure.py` 는 **`fill-opacity` 를 안 찍는다** — 원본 SVG 를 직접 열어 확인할 것.
- **stroke 있는 도형은 bbox 보다 stroke 폭만큼 크다** (셔터 `r33.5 stroke5` → 잉크 67, 실제 72).
- SVG 를 눈으로 보려면 `qlmanage -t -s 1100 -o <폴더> <파일>`. **정사각 썸네일이라 세로가 잘리니** viewBox 를 정사각으로 바꾼 사본을 만들어 렌더할 것.
- zip 한글 파일명이 깨지면 `ditto -x -k` 로 풀 것 (`unzip` 은 실패).

### 폰트 크기 역산
Figma SVG 는 글자가 path 다. `fontsize.py` 로 잉크 **폭**과 **높이**를 같이 맞춘다(높이가 더 정확).
토큰: 12=없음(값 지정) · 13=Caption1 · 14=Body3/4 · 16=Body1/2 · 18=Headline3 · 20=Headline2 · 24=Headline1

---

## 4. 함정 모음 — "파일엔 값이 있는데 화면이 이상하다"

### 좌표·앵커
- **`중심 = anchoredPosition + (0.5 − pivot) × size`**
- **stretch 앵커에서 `sizeDelta.x` 는 '부모폭 + 값'** — 372 를 그대로 두면 744 가 된다.
- **짝이 되는 요소는 앵커 공간을 맞춘다.** 말풍선은 우측 앵커인데 타임스탬프를 좌측 절대값으로 놨더니 부모가 412 가 아니라 372 라서 말풍선 안으로 파고들었다.
- **위치를 정하는 함수가 둘이면 서로 덮어쓴다.** `LayoutBar`(높이 반영) vs `ApplyLift`(리프트 반영) → **한 함수로 합칠 것**.
- **화면 아래에 붙는 것은 하단 앵커로.** 상단 앵커 + 절대 y 는 화면 비율이 다르면 붕 뜬다.

### 직렬화
- **★ `[SerializeField]` 는 씬 값이 코드 기본값을 이긴다.** 좌표계를 바꾸면 **씬 값도 같이** 고쳐야 한다.
  (`barBottom` 을 코드만 838.5→54.5 로 고쳤다가 입력창이 화면 밖으로 날아갔다.)
- **`\uXXXX` 는 큰따옴표 안에서만 풀린다** — 고 **믿었는데 틀렸다.** 유니티 파서는 따옴표 없이도 푼다(씬에 226줄이 그렇게 있고 정상 동작). 표준 YAML 로는 따옴표가 맞지만, **이걸 원인으로 단정하지 말 것.**
- **컴포넌트를 뗄 때 블록을 고아로 남기면 씬이 안 열린다.** `m_Component` 에서만 빼지 말고, 다시 붙이고 `m_Enabled: 0` 으로.
- **stripped 프리팹 인스턴스**의 값은 `1001 PrefabInstance` 의 `m_Modifications` 에 있다. `propertyPath` 만 보고 일괄 치환하면 다른 대상까지 덮어쓴다 — **`target` 으로 구분**.
- **블록을 복제하면 fileID 가 겹친다** → 프리팹이 통째로 안 보인다. 새 id 로 직접 쓰고 `verify`.

### UI
- **런타임 생성 텍스트는 `raycastTarget` 을 꺼라.** `UiUtil.MakeText()` 는 꺼주지만 `new GameObject` 는 기본 true. 투명한 글자 판이 버튼 클릭을 통째로 먹는다.
- **`Mask` 그래픽의 알파가 0 이면 자식이 통째로 사라진다.** 알파를 껐다 켜는 자리표시자는 **자식으로 분리**.
- **알약 스프라이트를 세로로 늘리면 캡이 타원.** `bg_pill*` 은 `SpriteBorder y=0,w=0`(가로만). 높이가 변하면 `bg_round*`(사방).
- **`TokenStyle.color ≠ 0` 은 `Image.color` 를 매번 덮어쓴다.** 투명 판을 만들었는데 네모가 보이면 이것.
- **캔버스 형제 순서 = 그리기 순서.** 토스트가 시트에 가리면 `SetAsLastSibling()`.
- **꺼진 오브젝트의 컴포넌트는 `Awake` 가 안 불린다.**
- **구운 PNG 위에 라벨을 또 얹지 말 것.** 글자·아이콘이 스프라이트에 들어있는지 열어볼 것.
- **에셋 파일명을 믿지 말고 열어볼 것.** `btn_summon_gallery.png` 안에 전후면 글리프가 들어있었다.
- **PNG 캔버스의 투명 여백**이 정렬을 망친다. 잉크 bbox 를 재서 `rect = 원하는크기 × 캔버스/잉크` 로 역산.
- **`Button` 컴포넌트가 아예 없는지** 먼저 볼 것. 코드로 `AddComponent<Button>` 하면 `targetGraphic` 이 안 잡힌다.
- **다른 화면에서 눌리는 버튼**은 `OnEnable` 에 걸면 안 된다 — 화면이 꺼지며 `OnDisable → RemoveAllListeners` 로 죽는다. `Start()`/`OnDestroy()` 로.

### 런타임/기기
- **런타임 생성 `Texture2D` 는 백그라운드 복귀 시 내용이 날아간다**(하얘짐). **파일을 원본**으로 둘 것.
- **`ScreenCapture.CaptureScreenshotAsTexture()` 가 URP+AR 에서 null** 을 준다 → `CaptureScreenshotIntoRenderTexture` + `ReadPixels`. RT/Texture2D 를 **둘 다 sRGB** 로(프로젝트가 Linear 색공간).
- **`RawImage` 는 늘려 그린다** → `UiUtil.FitCover`. 단 **stretch 자식은 그 프레임에 `rect` 가 0** 이라 비율을 못 잰다.
- **`TouchScreenKeyboard.visible` 로 막지 말 것** — TMP_InputField 는 키보드를 자체 관리해서 안 켜지는 기기가 있다. `getWindowVisibleDisplayFrame` 으로 직접 재고, **화면의 15% 미만(네비바)·60% 초과는 버린다.**
- **`onValueChanged` 마다 InputField 가 붙은 rect 를 리사이즈하면 입력이 끊긴다.** 값이 실제로 바뀔 때만.
- **ARCore 는 전면 카메라를 Augmented Faces 로만 준다** — 평면 인식과 동시 사용 불가.

### 유리(glass)
Figma 의 `효과: glass` = **backdrop-blur** 인데 **Unity UI 에는 없다.** 이 프로젝트는 blur 없이 색으로 낸다.
- **유니티는 Linear, 피그마는 sRGB** — 같은 α 라도 결과가 다르다. α0.5 는 거의 같고, **α0.2 는 유니티가 2배 밝다.**
- 그래서 **시안에서 합성이 끝난 색을 알파 1 로** 칠한다 → `GlassGradient`(BaseMeshEffect, 정점 색).
- **유리 = 그라디언트 채움 + 1px 테두리**이고 **Figma SVG export 가 테두리를 흘린다.** 3x PNG 로 재야 한다.
- 알파는 **스프라이트가 아니라 `Image.color`** 로 준다(SVG 에 `fill-opacity` 를 구우면 0.2×0.2=0.04 가 된다).
- 디자이너가 유리를 **단독 export 하면 불투명 회색 판**으로 굳는다 — 그 PNG 값을 그대로 쓰지 말 것.

---

## 5. 디버깅 원칙

1. **"안 된다"는 신고엔 먼저 카운트·로그를 찍는다.** 하얀 사진은 `Count == 0`(자리표시자)이었고, 키보드는 **경고가 안 찍힌 것**이 단서였다.
2. **정적 분석으로 안 잡히면 추측하지 말고 로그.** 입력창 모양을 추측으로 바꿨다가 되레 망가뜨렸다.
3. **로그가 안 보이면 앱을 재시작해 확인.** 시작 시 찍히는 `[Community] 시드 N글 로드` 한 줄이면 로깅 자체가 되는지 갈린다.
4. **버퍼에 남은 다른 앱 로그를 자기 것으로 오해하지 말 것.** `adb logcat -c` 후 재시작.
5. 고치기 전에 **(1) 현재 값을 찍어보고 (2) 정말 틀렸는지 확인하고 (3) 최소 범위로** 고친다.

```bash
ADB=/Applications/Unity/Hub/Editor/6000.3.17f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
$ADB logcat -d -t 600 -s Unity:V | grep -aE "TalkDetail|Capture|ARFlow|ARSession|Community"
```

---

## 6. 현재 상태 (2026-08-25)

씬 `Phone.unity.bak547` · `verify` 클린(`blocks 8402 · GO 1709`) · 컴파일 0 에러.
기기 `samsung SM-S948N`. ⚠ 패키지 ID 는 여전히 URP 템플릿 기본값.

### ★ 백엔드 연결 — 39개 중 32개 완료 (설계·근거: `docs/ARZONE_API연결_설계.md`)
- 서버 `https://hanium.azurewebsites.net` · **2026-08-25 재배포로 대격변**: 업로드/이미지URL/XP/닉네임 전부 정상.
  스펙 사본 `~/Desktop/CUBE/api/openapi.json`. 문의 잔여: `docs/백엔드_문의사항.md` 7·8·9번.
- 구조: `ApiClient`(전송 1곳) + 도메인 인터페이스(+Mock). JSON 은 JsonUtility(enum 은 string 으로).
- 연결됨: 홈(닉네임 젠득이·D-day·XP·프로필사진) · 갤러리 3(상세는 FitContain 원본비율) ·
  대화 7+보관함/해제/신고 · 아바타 소환 4(세션·캡처업로드·서버프레임 11) ·
  무대/트랙 세션·캡처(제목·clipKey ↔ 서버 id 정규화 매칭, `ApiMediaSource`) ·
  미션 목록 · 큐브 인증(⚠ 시드 큐브1 이미 소유라 항상 409) · 마켓 주문상세/구매확정.
- **음성 대화(VOICE)도 서버 연결됨**(2026-08-25 유저 결정 "서버로 연결") — 홈 최근목록·전체목록·삭제.
  ⚠ 서버 VOICE 시드 0건이라 지금은 목록이 빈다 → 문의 10번(시드 요청). 앱엔 녹음이 없어 못 채운다.
- 미연결: 마켓 거래생성(시드 문제·문의7) · 판매등록 3(화면 없음, 배선 금지 확정).
- ★ 08-26 밤 **대화 세션 종료 PATCH 연동**(32번째): 알고 보니 단순 상태 저장이 아니라
  **"세션 닫기 + 제목 없으면 첫 메시지 2개로 GPT 제목 생성"** 이었다(실측: "날씨 맞춤 노래 추천하기").
  안 부르면 앱에서 만든 대화가 목록에 무제목으로 남는다. 멱등·종료 후 전송 가능(실측) →
  `TalkDetailController.OnDisable → ConversationStore.CloseCurrent()`. multipart 라
  `ApiClient.PatchForm`(수제 본문 — Unity 폼 클래스는 빈 값 거부) 신설. 실기 미확인.
- ★ 08-26 **스트리밍 전환 완료** (유저 지시 "ㄱㄱ"): GET /stages/{id}·/tracks/{id} 연동.
  방식 = **로컬 즉시 재생 + 서버 도착 시 같은 위치로 교체, 실패는 로컬 유지**(로그만).
  음원: `ApiClient.GetAudioClip`(streamAudio, ⚠요청을 살려둬야 클립이 산다 — 캐시가 보관) →
  `ApiMediaSource.GetTrackAudio(clipKey)` 전곡 캐시(~3MB/곡). 음악상세·AR감상 교체 배선.
  영상: `ApiMediaSource.GetStageVideoUrl(title)` → VideoPlayer 소스를 URL 로(무대상세·AR무대).
  상세를 거쳐 오면 URL 캐시가 동기 콜백이라 Play 전에 바뀐다. StageDetail 의 clip-null
  가드는 `HasSource`(clip∨url)로, 길이는 `player.length` 로 바꿨다. **실기 미확인.**
- ★ 08-25 저녁 마켓 시드 개편 감지: **거래 2·3 삭제, 거래 1 = DELIVERED**(문의7 절반 해결).
  `MarketOrderDetailController.tradeId` 기본값 3→**1**(씬엔 직렬화 안 돼 있어 코드만 고침).
  **구매 확정이 이제 실제로 성공한다** — 단 ⚠ **한 번 확정하면 되돌릴 API 가 없어 시드가 소모된다.**
  데모 리허설에서 아껴 쓸 것. 홈·갤러리·앨범·트랙·미션·대화 시드는 변동 없음 확인.
  VOICE 시드(문의10)·닉네임 PATCH(문의8)는 여전히 미해결(각각 0건·404 재확인).
- 08-26 00시 쓰기 경로 스모크 테스트(전부 앱과 동일 Body, 시험 데이터 즉시 정리): 아바타 세션
  생성/종료 204 · 무대/트랙 세션 생성/종료 204 · 대화 생성→메시지→삭제 OK(**아바타 답장이
  진짜 AI 생성으로 옴** — 시드 문구 아님) · 캡처 업로드 204→갤러리 반영→삭제 204. 전부 정상.
- 시험 데이터를 서버에 만들면 **반드시 정리**(사진 DELETE·세션 종료). `.env` 는 **절대 읽지 않는다**.
- 로컬 백엔드: `~/Desktop/CUBE/backend`(be/api-v3, docker compose). Azure 키가 한 글자 유실돼
  업로드만 로컬 500 — 파일로 재수령 대기. 데모는 배포 서버 기준.

### 유리 발광 테두리 (2026-08-25 저녁) — ★실기 확인 대기★
- 유저 레퍼런스: **좌상단 모서리만 빛나고 우상단·좌하단은 사라지는 부분 테두리** (backdrop-blur 아님!).
- `GlassEdgeGlow`(BaseMeshEffect, 정점 알파 = TL·(1-u)(1-v)+BR·(u·v), 기본 TL 0.55/BR 0.18/Focus 1.6)
  + `GlassGlowBoot`(ScreenManager 에 씬 배선, bak548) 가 화면 열릴 때 `GlowBorder` 자식을 얹는다.
- 대상: 탑바 미션/알림 버튼(홈·허브·아바타홈, r12) · 아바타홈 원형버튼 3(circle) · 미션 친밀도 카드 ·
  허브 4분할 · 스테이지/음악상세 세그먼트(pill44) · 주문상세/구매확정 카드 · 런타임 카드
  (마켓4·커뮤니티·알림·음성주제 — 각 빌더에 Attach 호출).
- **하단 탭바 5곳**: 두 번 방향 바꿈 — ①구운 알약 교체(너무 밝음) ②틴트 0.4(여전히 "불투명") →
  ③ **유리는 씬 원본으로 되돌리고, `tabbar_pill_glow`(Resources/UI, 디자이너 -5 export)에서
  가장자리 6px 만 런타임에 발라낸 링**을 GlowBorder 로 얹음(`GlassGlowBoot.MakeRing`).
  ④ (08-25 실기: **모서리 호만 삐죽 나옴**) 원인 — 이 PNG(648×216=정확히 3x)는 알약 직선 변이
  캔버스 경계와 일치해 **투명 픽셀이 모서리 구석 4곳뿐** → MakeRing 시드 부족으로 직선 변 전멸(2.2%만 생존).
  **캔버스 밖=투명 시드**(경계 불투명 픽셀 dist=1)를 추가해 온전한 링으로 고침. 재실기 대기.
- 홈 프로필: 서버 PNG 네 귀퉁이가 불투명 검정(에셋 문제) → `HomeController.CircleSprite` 가
  정사각 크롭+내접원 밖 투명 처리. 검은 테 남으면 반지름 축소. 백엔드에 투명 PNG 재업로드 요청할 것.
- 친밀도 진행바: 유저 zip('반짝이는 로딩창' 216프레임, Resources/ProgressBar + 실측 채움표) →
  재생 연출 없이 **바 띠(y26~66, 유니티 하단원점)를 비율 지점까지 크롭**해 채움으로(halo 잘림, 픽셀 정확).
- 숫자(730/800) 위치는 시안과 일치 확인(우끝 336, 바와 같은 줄) — 안 바꿈.

### 뷰포리아 피규어 인식 (08-26 착수)
- 유저가 **Tripo AI 로 실물 피규어를 스캔한 FBX** 를 줌(`~/Downloads/피규어인식용.fbx`, 133만tri, 텍스처 없음).
  **실물 키 8cm**(유저 실측). 원본은 1유닛≈1m 로 읽혀 98cm — 스케일 보정본을 만들었다:
  - `~/Desktop/CUBE/vuforia/피규어인식용_8cm.fbx` — UnitScaleFactor 패치, **정확히 8.00cm**
  - `…_8cm_40만.fbx` — Blender 헤드리스 데시메이트(133만→40만tri, MTG 권장치).
    ⚠ Blender 는 크기를 **노드 스케일 8.1724**로 보존한다(unit 은 1.0) — 이중 패치 금지. 8.00cm 검증 완료.
- ✅ MTG 데이터셋 생성 완료(`~/Vuforia/ModelTargets/avatar/dataset/`, 타깃 "avatar", 39.8만 폴리곤,
  Guide View 1개 **평점 5/5**, 8cm 스케일 확인) → `Assets/StreamingAssets/Vuforia/avatar.{dat,xml}` 복사됨.
- ✅ 라이선스 키는 **`Assets/Resources/VuforiaConfiguration.asset` 에 이미 있다** (VuforiaLicense.cs 불필요).
- ✅ 코드 완료(08-26): `Detectors/VuforiaCubeDetector`(IObjectDetector, ModelTarget 런타임 생성,
  TRACKED→OnDetected) + `App/CubeScanController`(Screen_Scan 의 가짜 3초 타이머를 진짜 인식으로 —
  Bg 끄고 ARCamera 켜고, 진행바는 85%까지 기다가 인식 시 100%→CubeFound 자동 전환).
  ⚠ Screen_MarketLoading 도 ScanProgressController 를 쓰지만 **그쪽은 가짜 타이머 유지**(안 건드림).
- ▶ 유니티 배선(유저): ①메뉴 GameObject→Vuforia Engine→**AR Camera** 추가 후 **꺼두기**
  ②Screen_Scan 에 CubeScanController+VuforiaCubeDetector 붙이고 progress/detector/ARCamera/Bg 드래그.
- 뷰포리아 11.4.4 tgz 패키지 설치돼 있음. AR Foundation 과 카메라 동시 사용 금지(기존 규칙) —
  온보딩(AR 진입 전)에서만 쓰는 전제고, 화면 이탈 시 ARCamera 를 끈다.

### 08-27 밤 — 대화 화면 좌표 대수술 + 프로필 통일 (bak553)
- ★★ **대화 말풍선 행의 root 는 372 폭(화면-40, 좌 20 오프셋)인데 좌표 상수는 화면 기준이었다**
  → 전부 20px 오른쪽 밀림 + 아바타 시각이 뷰포트 마스크(392)에 잘림("오후 3:4").
  말풍선·시각·이름·프로필·액션 전부 -20 보정, 아바타 말풍선 접기 한도는 뷰포트 기준(243.6).
  ⚠ **말풍선 x 는 두 번 설정된다** — 이름 줄 붙일 때 재설정(598행)이 보정을 덮어써서 한 번 헛돎.
  이름 x 는 `bubbleLeftX - 20` 으로 말풍선과 같은 변수로 묶음(유저 지시: 이름 시작점 = 말풍선 왼쪽).
- 대화 버그 3: 새 대화 404(서버가 빈 세션 상세를 404로 — 문의 11, 앱은 조회 생략 우회) ·
  보관 버튼 onClick null → SaveText+토스트 배선 · 검색이 더미 6줄만 뒤짐 → 서버 전 세션 메시지 로드.
  목록 무제목 → "새 대화" 표시 + 세션 종료(GPT 제목) 후 목록 자동 갱신.
- **프로필 아이콘 통일**: 홈·허브 상단, 마켓 홈이 토끼로 하드코딩 → `ProfileIconBinder` 신설
  (ProfileStore.AvatarIndex 연동, 남/여/동물 서클). 커뮤니티 글쓴이·마켓 판매자는 남의 프로필이라 유지.
  대화 목록의 "Face"는 프로필이 아니라 스와이프 앞면 카드다(착각 주의).

### 08-27 저녁 — AR 감상 립싱크 아바타 (실기 미확인)
- 유저 요청 "음원에서 AR버튼 → 가사 맞춰 입 움직이는 아바타". `AvatarLipSync.cs` 신설:
  AudioSource RMS 로 VRM 'aa' 모프 구동(UniVRM10 ExpressionKey), **가사 줄 활성일 때만**(LRC gate).
  `ARListenController.PlaceLipSyncAvatar()` — 진입 시 카메라 앞 1.2m·바닥에 아바타 배치
  (ARSessionController.PlaceAvatar 재사용), 이탈 시 ShowAvatar(false). 씬 수정 없음.
- 아바타홈 프레이밍 확정: 발앵커 0.02 + stand 여백 1.30(말풍선 안 가리게).

### 08-27 실기 디버깅 3라운드 (bak550~552) — 전부 로그로 잡음
- **디렉터가 엉뚱한 Animator**: `GetComponentInChildren(true)` 가 꺼둔 옛 avatar_main 을 먼저 잡아
  포즈가 숨은 아바타에 가고 카메라도 따라감(로그 "키 0.20" 이 증거) → **활성+isHuman 만** 잡게 수정.
- **옷 절반이 흰색**: vroid 는 검정 옷을 "흰 텍스처×검정 baseColorFactor"로 만들었는데
  **shadeColorFactor 에 그 곱이 빠져** 그늘 쪽이 연보라 흰색으로 렌더됨(MToon 스펙상 정상 동작).
  VRM 재질 4개(자켓·신발·스커트·타이) shade 에 곱 적용 패치(원본 tools/orig/avatar_vroid.vrm.orig).
- **아바타홈 진행**: AvatarStage 밑에 원래 `AvatarSurface`(RT용 빈 RawImage, **알파 0 함정**)와
  AvatarStill(일러스트)이 있었다 — Surface 에 RT+알파1, Still 끔, 홀쭉함은 uvRect (0.313,0,0.374,1)
  (자리 182×486 vs RT 정사각 → 가로 크롭으로 비율 복원).
- 리포트 백버튼 무한루프 = `back:1` 히스토리 모드 → `targetScreen: Avatar` 로 확정 이동.
- AR 소환 아바타 미표시 = avatarPrefab 이 옛 guid → vroid 로 교체(**루트 fileID 가 동일**해서 guid 만 갈면 됨).

### 08-27 아바타 VRM 전면 확산 + 잡건 (bak549, 실기 미확인)
- **`AvatarPoseDirector` 신설**(ScreenManager 에 부착) — AvataStage 무대 하나를 화면별 포즈로 재사용:
  Home·VoiceHome=앉아 턱괴기 / VoiceTalk·VoiceLoading=서서 팔벌리기(sian92) / Avatar=서서 팔내리기.
  본 바인드 캐시→복원→재포즈, 앉은 구도는 씬 카메라 그대로·선 포즈는 바운드 자동 프레이밍.
  **AvatarSitPose 는 씬에서 꺼둠**(파일은 남김). 시퀀스 216장 글로우 제거는 **두 벌 다** 완료
  (Resources/ProgressBar + Sprites/ProgressSequence, 원본 tools/orig/).
- 씬: ①Screen_Avatar/AvatarStage → RawImage(RT_Avatar) ②VoiceTalk/VoiceAvatar → RawImage(uv 가로 0.625 크롭)
  ③VoiceHome/VoiceCard 에 Mask+자식 VoiceCardAvatar RawImage(uv 세로 0.30~0.945)
  ④알림 BtnReadAll = bg_pill_stroke + 보라(#A855F7, TokenStyle 10) — 어두운 알약이라 안 보였던 것
  ⑤교체된 Image 들의 TokenStyle 은 m_Enabled:0. 미션 토스트 ✨ 제거(폰트에 글리프 없음)·
  미션 바로가기 goScreen 매핑 신설(홈/TalkList/Album/SummonScan/Gallery/Community).

### 아바타 교체 진행 중 (08-26)
- **`vroid 버전.vrm` 검사 통과** → `Assets/Project/Avatars/avatar_vroid.vrm` 로 복사해 둠.
  실측: 40,285tri · **스키닝 있음**(조인트 144) · 휴머노이드 본 54 · **모프 456 + 표정 프리셋 14**
  (blink/립싱크 aa~oh 포함) · **springBone 1.0** · **MToon 1.0** 17머티리얼(=드로우콜 17,
  URP 셰이더 `vrmc_materials_mtoon_urp` 패키지에 있음) · 키 1.062m. UniVRM 0.131.2 = VRM 1.0 지원.
- 기존 참조 3곳: ①`ARSessionController.avatarPrefab` ②`SummonController.avatarPrefab`(죽은 경로)
  ③씬 `AvataStage` 밑 프리팹 인스턴스(오버라이드 65개 전부 **레이어 3 "AvartarStage"** 지정).
- **유니티에서 유저가 할 일**: 새 프리팹 루트를 ①에 드래그, AvataStage 밑에 새로 놓고
  위치 0,0,0 · **회전 Y 180** · **스케일 1.32**(기존 표시 키 1.40m 유지 = 0.979×1.43÷1.062),
  **레이어 AvartarStage(자식 포함 Yes)**, 기존 avatar_main 인스턴스는 **끄기**(삭제 말고).
  AR 소환은 avatarHeight 로 자동 스케일이라 그대로. → 유저가 배선 완료(08-26 확인).
- **T포즈 문제**: 새 모델은 리깅이 제대로라 애니메이션이 없으면 T포즈로 선다(프로젝트에
  .anim/.controller 가 전무 — 옛 아바타는 스키닝 없는 조각상이라 티가 안 났던 것).
  → **`AvatarSitPose.cs` 신설**(휴머노이드 본을 FromToRotation 으로 돌려 정적 '앉은 자세',
  발 높이를 바닥에 재정렬). **아바타홈 인스턴스에만 붙일 것**(AR 소환은 서 있어야 함).
  플레이 모드에서만 적용. 조정 필드: kneeSpread(무릎 벌림)·armOut(팔 띄움)·nudge(위치 미세조정).
- `~/Downloads/env.txt` — 로컬 백엔드 키로 보임. **절대 읽지 말 것(규칙 8)** — 유저가 직접 .env 에 넣는다.

### 음악/가사 (2026-08-24~25)
- **`MusicPlayer`**(DontDestroyOnLoad)가 AudioSource 소유 — 화면 꺼져도 재생 유지(가사보기 끊김 해결).
- 가사 화면: 실제 줄높이 배치(+시안 간격16 역산) · 재생버튼(Button 신설, bak545) ·
  일시정지 아이콘 `ic_pause_lyrics.png`(96², 유저 제공) + 박스 24² 시안 정합(bak546) ·
  스크롤 초기화 · `Sync` 매 프레임.
- 가사 싱크 **690/719줄(96%)** — love_hangover 만 35/64(2:16 이후 가사 원문 오류, 올바른 가사 필요).
  도구 `tools/lrcsync.py` · 팀원 번들 `가사싱크_팀원용.zip`(윈도우 대응).

### 대화 화면 (2026-08-24)
- 서버 전환: sequenceNumber 정렬(내림차순으로 옴!) · conversationType 필수 · 제목 null 은 비움 ·
  타임스탬프 잘림(아바타 말풍선 최대폭에서 시각폭 확보) · 타이핑점 위치/크기/즉시프레임 수정.
- 사진 첨부는 로컬 전용(업로드 경로 없음) · 안읽음점/🖤 는 서버 필드 없어 항상 false.

### 미해결 (우선순위)
1. ~~아바타 스킨 웨이트 없음~~ → **08-26 해소**: `avatar_vroid.vrm` 스키닝·표정·springBone 전부 있음. 위 "아바타 교체 진행 중" 참고.
2. 음성 대화 VOICE 서버 전환 여부 — 유저 결정 대기.
3. 말풍선 1px 테두리 · 갤러리→PhotoDetail 전환(미결 유지) · 프레임시트 가로스크롤 없음(시안대로).
4. Vuforia Model Target: Basic 무료로 진행 확정(학술용, 워터마크 감수). 큐브 3D 모델 대기. Guide View 가 외곽선 정합 UI.

### 참고 문서
- `docs/ARZONE_API연결_설계.md` — API 연결의 모든 결정과 근거
- `docs/백엔드_문의사항.md` — 백엔드 전달용(1·2·3·6 해결됨 표시)
- `docs/커뮤니티_진행원장.md` — 커뮤니티·마이 결정 전부
