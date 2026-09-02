# 커뮤니티 구현 계획

> **에이전트로 실행할 때:** `superpowers:subagent-driven-development` 또는
> `superpowers:executing-plans` 를 써서 태스크 단위로 진행할 것. 체크박스로 진척을 추적한다.

**목표:** ARZONE 폰 앱에 커뮤니티(게시판 2종 · 글쓰기 · 상세 · 댓글 · 사진첨부 · 신고/삭제)를
시안 그대로 만들고, 나중에 서버를 붙일 이음매까지 둔다.

**접근:** 화면은 `Phone.unity` 한 씬에 `Screen_*` 루트로 넣고 `tools/mkui.py` 로 생성한다.
반복 요소(글 카드·댓글 행·사진 그리드)는 씬에 두지 않고 컨트롤러가 코드로 만든다.
데이터는 `ICommunitySource` 인터페이스 뒤에 두고 지금은 `MockCommunitySource` 만 구현한다.

**기술:** Unity 6000.3.17f1 · URP · C# (네임스페이스 `ARZone`) · TextMeshPro ·
씬 편집은 `tools/arb.py` · `tools/mkui.py` · 실측은 `tools/svgmeasure.py`

**설계 문서:** `~/Desktop/CUBE/docs/ARZONE_커뮤니티_설계.md` — 화면 분해·데이터 모델·API 이음매의 근거.

---

## 전역 제약 (모든 태스크에 공통)

이 프로젝트에는 자동화 테스트가 없다. **태스크마다의 검증은 아래 3종이다.**

1. **컴파일** — 유니티 없이 Roslyn 으로 돌린다
   ```bash
   S=/Applications/Unity/Hub/Editor/6000.3.17f1/Unity.app/Contents/Resources/Scripting
   "$S/NetCoreRuntime/dotnet" "$S/DotNetSdkRoslyn/csc.dll" @<스크래치패드>/cs.rsp 2>&1 | grep ": error"
   ```
   `cs.rsp` 는 `Assembly-CSharp.csproj` 의 `<HintPath>`/`<Compile>` 로 만든다.
   **새로 만든 .cs 는 csproj 에 없으므로 rsp 에 직접 추가해야 검증된다.**
2. **씬 무결성** — `python3 tools/arb.py verify` 가 `중복 [] · 잘림 0 · 미해결참조 0` 이어야 한다
3. **실측 대조** — 넣은 좌표·크기·글자를 시안 수치와 표로 대조해 보여준다

### ⚠ 새 `.cs` 를 만들면 `.cs.meta` 도 손으로 써야 한다

유니티가 꺼져 있으면 `.meta` 가 생성되지 않는다. **`.meta` 가 없으면 GUID 가 없어서 씬에서
그 스크립트를 참조할 수 없다** (컴포넌트 배선이 통째로 막힌다). 새 `.cs` 를 만들 때마다:

```bash
cd ~/Desktop/ARZONE-App/Assets/Project/Scripts
python3 - <<'EOF'
import os, re, uuid, glob
tpl = open('App/GlassGradient.cs.meta').read()   # MonoImporter 템플릿
for p in sorted(glob.glob('App/*.cs') + glob.glob('Interfaces/*.cs') + glob.glob('Core/*.cs')):
    m = p + '.meta'
    if os.path.exists(m): continue
    g = uuid.uuid4().hex
    open(m, 'w').write(re.sub(r'^guid: \w+$', 'guid: ' + g, tpl, count=1, flags=re.M))
    print(os.path.basename(p), g)
EOF
```
만든 GUID 는 리포트에 적는다. 끝나면 프로젝트 전체 GUID 중복 검사도 돌린다.

### 지켜야 할 규칙 (유저가 못박은 것)

- **시안대로만.** 눈대중 금지. 시안에 없는 문구·수치·동작을 지어내지 않는다.
  시안에 없는데 필요해 보이면 **채우지 말고 물어본다.**
- **에셋을 만들지 않는다.** 없으면 요청한다. 시안 SVG 에서 `<path>` 를 떼어내는 것도 '만드는 것'이다.
  이미 프로젝트에 있는 에셋을 **재사용**하는 건 괜찮다.
- **오브젝트를 지우지 않는다.** 빼야 하면 `m_IsActive: 0` 으로 끈다.
- **깃을 건드리지 않는다.** `git`/`gh` 명령을 실행하지 않는다. 커밋은 유저가 한다.
- **유니티가 켜져 있으면 씬을 저장하지 않는다.** `arb.save()` 가 막는다 — 유저에게 닫아달라고 한다.

### 색·타이포

- 색은 `TokenStyle.color` 로 고른다. 값을 직접 넣는 건 토큰에 없는 색일 때만.
- **유니티는 Linear, 피그마는 sRGB 라 같은 알파도 결과가 2배 가까이 다르다.**
  반투명 면은 **시안에서 합성이 끝난 색을 불투명하게** 칠한다. 유리 카드는 `GlassGradient` 를 붙인다
  (`from #232323` → `to #050608`, `alpha 1`, 축 `t = 0.334 + 0.471·u + 0.457·v`).
- 글자 크기는 PIL 로 잉크 폭을 역산해 맞춘다. 굵기는 잉크 bbox 의 정규화 평균 밝기로 가른다
  (Regular 0.24 / Medium 0.29 / SemiBold 0.32 / Bold 0.37).

### 시안 — 시작할 때 한 번 풀어 둔다

SVG 는 `~/Downloads/한이음_CUBE_E5N4-76.zip`, PNG(3x) 는 `-77.zip` 이다.
스크래치패드는 세션마다 날아가므로 **`/Users/peachee/Desktop/CUBE/sian76/` 에 푼다.** 모든 태스크가 이 경로를 쓴다.

```bash
mkdir -p /Users/peachee/Desktop/CUBE/sian76 && cd ~/Downloads && python3 - <<'EOF'
import zipfile, unicodedata, os
base = '/Users/peachee/Desktop/CUBE/sian76'
for z, tag in [('한이음_CUBE_E5N4-76.zip', 'svg'), ('한이음_CUBE_E5N4-77.zip', 'png')]:
    zf = zipfile.ZipFile(z); dd = os.path.join(base, tag); os.makedirs(dd, exist_ok=True)
    for n in zf.namelist():
        try: nn = unicodedata.normalize('NFC', n.encode('cp437').decode('utf-8'))
        except Exception: nn = n
        if nn.endswith('/'): continue
        open(os.path.join(dd, os.path.basename(nn).replace('/', '_')), 'wb').write(zf.read(n))
    print(tag, len(os.listdir(dd)))
EOF
```

기대: `svg 10` · `png 10`. 이후 시안 경로는 `/Users/peachee/Desktop/CUBE/sian76/svg/<이름>.svg` 로 쓴다.
(`unzip` 은 한글 파일명에서 `Illegal byte sequence` 로 실패한다 — 반드시 위 파이썬으로 푼다.)

- **테두리처럼 SVG export 가 흘리는 게 있다.** 카드 가장자리가 의심되면 `png/` 의 3x PNG 픽셀을 직접 잰다.

### 스크래치패드

컴파일용 `cs.rsp` 등 임시 파일은 세션 스크래치패드에 둔다. 아래에서 `$SP` 는 그 경로다.

```bash
SP=$(ls -d /private/tmp/claude-*/-Users-peachee-Desktop-CUBE/*/scratchpad 2>/dev/null | head -1)
[ -z "$SP" ] && SP=/tmp/arzone_build && mkdir -p "$SP"
echo "$SP"
```

### 좌표

```
중심 = anchoredPosition + (0.5 − pivot) × size
```
- 시안 절대좌표를 그대로 쓰려면 앵커·피벗을 **좌상단 (0,1)** 으로 통일하고 `pos = (x, −y)`.
- **코드가 회전·스케일시키는 오브젝트는 pivot 을 (0.5,0.5)** 로 둔다(안 그러면 모서리를 축으로 돈다).

---

## 파일 구조

### 새로 만드는 스크립트 (`Assets/Project/Scripts/`)

| 파일 | 책임 |
|---|---|
| `Interfaces/ICommunitySource.cs` | 글 읽기/쓰기/좋아요/댓글/삭제/신고의 유일한 접점 |
| `App/CommunityModels.cs` | `CommunityPost` · `CommunityComment` · `CommunitySort` |
| `App/CommunitySeed.cs` | 목업 글 데이터 ScriptableObject |
| `App/MockCommunitySource.cs` | 메모리 구현. 시드에서 읽고 즉시 콜백 |
| `App/CommunityStore.cs` | 소스 보유 · 목록 캐시 · `OnChanged` 이벤트 |
| `App/CommunityController.cs` | 목록 화면 |
| `App/CommunityPostController.cs` | 상세 화면 |
| `App/CommunityWriteController.cs` | 글쓰기 화면 |
| `App/CommunityPhotoSheet.cs` | 사진 선택 바텀시트 |
| `App/PostMenuPopup.cs` | ⋮ 메뉴 + 삭제 확인 모달 연결 |

### 새로 만드는 씬 오브젝트 (`Assets/Project/Scenes/Phone.unity`)

`Screen_Community` · `Screen_CommunityWrite` · `Screen_CommunityPost` ·
`Sheet_CommunityPhoto` · `Popup_PostMenu` · `Modal_PostDelete`

### 새로 넣는 에셋 (유저 제공, 2026-08-14)

| 원본 | 넣을 곳 |
|---|---|
| `~/Downloads/한이음_CUBE_E5N4-82.zip` 의 `icon.png` | `Sprites/Icons/ic_photo_attach.png` |
| `~/Downloads/icon-12.png` | `Sprites/Icons/ic_comment.png` |

### 손대는 기존 파일

- `Phone.unity` — `Screen_Home/Hub/Market` 의 `TabBar/TabFriend` 에 `ScreenButton` 추가

---

## Task 1: 아이콘 2개 임포트

유저가 내보내 준 아이콘을 프로젝트에 넣는다. 유니티가 꺼져 있어도 씬에서 참조할 수 있도록
`.meta` 를 고정 GUID 로 직접 쓴다.

**Files:**
- Create: `Assets/Project/Sprites/Icons/ic_comment.png` (+ `.meta`)
- Create: `Assets/Project/Sprites/Icons/ic_photo_attach.png` (+ `.meta`)

**Interfaces:**
- Produces: 두 스프라이트의 GUID. 이후 태스크에서 `m_Sprite: {fileID: 21300000, guid: <guid>, type: 3}` 로 참조한다.

- [ ] **Step 1: 원본을 프로젝트로 복사**

```bash
SRC=~/Downloads
DST=~/Desktop/ARZONE-App/Assets/Project/Sprites/Icons
cp "$SRC/icon-12.png" "$DST/ic_comment.png"
python3 - <<'EOF'
import zipfile, os
z = zipfile.ZipFile(os.path.expanduser('~/Downloads/한이음_CUBE_E5N4-82.zip'))
dst = os.path.expanduser('~/Desktop/ARZONE-App/Assets/Project/Sprites/Icons/ic_photo_attach.png')
open(dst, 'wb').write(z.read(z.namelist()[0]))
print('ok', os.path.getsize(dst))
EOF
```

- [ ] **Step 2: 두 파일이 60×60 · 알파 있음인지 확인**

```bash
python3 - <<'EOF'
from PIL import Image
import os
d = os.path.expanduser('~/Desktop/ARZONE-App/Assets/Project/Sprites/Icons')
for f in ['ic_comment.png', 'ic_photo_attach.png']:
    im = Image.open(os.path.join(d, f)).convert('RGBA')
    print(f, im.size, 'alpha', im.split()[3].getextrema())
EOF
```

기대: 둘 다 `(60, 60) alpha (0, 255)`.

- [ ] **Step 3: `.meta` 를 기존 아이콘 것과 같은 설정으로 쓴다**

기존 `ic_copy.png.meta` 를 원본으로 삼고 `guid` 만 새로 만든다.

```bash
cd ~/Desktop/ARZONE-App/Assets/Project/Sprites/Icons
python3 - <<'EOF'
import re, uuid, os
base = open('ic_copy.png.meta').read()
for name in ['ic_comment', 'ic_photo_attach']:
    g = uuid.uuid4().hex
    t = re.sub(r'^guid: \w+$', 'guid: ' + g, base, count=1, flags=re.M)
    open(name + '.png.meta', 'w').write(t)
    print(name, g)
EOF
```

**출력된 두 GUID 를 적어둔다. Task 5·8 에서 쓴다.**

- [ ] **Step 4: 검증**

```bash
cd ~/Desktop/ARZONE-App/Assets/Project/Sprites/Icons && ls -la ic_comment.png* ic_photo_attach.png*
grep -h "^guid:" ic_comment.png.meta ic_photo_attach.png.meta
```

기대: 4개 파일이 있고 GUID 두 개가 서로 다르며 프로젝트 내 다른 GUID 와 겹치지 않는다.

```bash
cd ~/Desktop/ARZONE-App/Assets && for g in $(grep -h "^guid:" Project/Sprites/Icons/ic_comment.png.meta Project/Sprites/Icons/ic_photo_attach.png.meta | awk '{print $2}'); do echo "$g -> $(grep -rl "guid: $g" --include='*.meta' . | wc -l)"; done
```

기대: 각각 `1` (자기 자신만).

---

## Task 2: 데이터 모델 · 인터페이스 · Mock 소스 · Store

코드만 만든다. 씬은 건드리지 않는다. 이 태스크가 끝나면 화면 없이도 데이터가 돈다.

**Files:**
- Create: `Assets/Project/Scripts/App/CommunityModels.cs`
- Create: `Assets/Project/Scripts/Interfaces/ICommunitySource.cs`
- Create: `Assets/Project/Scripts/App/CommunitySeed.cs`
- Create: `Assets/Project/Scripts/App/MockCommunitySource.cs`
- Create: `Assets/Project/Scripts/App/CommunityStore.cs`

**Interfaces:**
- Produces:
  - `CommunityPost { string id, board, title, body, author, timeText; int likes, comments; bool liked, mine; Sprite authorIcon; List<Sprite> photos; List<CommunityComment> replies; }`
  - `CommunityComment { string author, timeText, body; Sprite authorIcon; }`
  - `enum CommunitySort { Latest }`
  - `ICommunitySource` — 아래 Step 2 의 시그니처 그대로
  - `CommunityStore.Instance` · `.Posts` · `.OnChanged` · `.Load(board, query)` · `.Create(draft)` ·
    `.ToggleLike(id)` · `.AddComment(id, body)` · `.Delete(id)` · `.Report(id)` · `.Find(id)`

- [ ] **Step 1: 모델**

`Assets/Project/Scripts/App/CommunityModels.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 커뮤니티 글 한 개. 서버가 붙으면 JSON 을 여기에 매핑한다.
    [Serializable]
    public class CommunityPost
    {
        public string id;
        public string board;        // "자유게시판" | "인증게시판"
        public string title;
        public string body;
        public string author;
        public string timeText;     // "방금 전" · "2시간 전" — 서버가 주면 그대로 쓴다
        public int    likes;
        public int    comments;
        public bool   liked;        // 내가 누른 상태
        public bool   mine;         // 내 글이면 ⋮ 에 삭제하기가 뜬다
        public Sprite authorIcon;
        public List<Sprite> photos = new List<Sprite>();
        public List<CommunityComment> replies = new List<CommunityComment>();
    }

    [Serializable]
    public class CommunityComment
    {
        public string author;
        public string timeText;
        public string body;
        public Sprite authorIcon;
    }

    // 시안에는 '최신순' 하나뿐이다. 드롭다운이 열린 시안이 없어 다른 항목을 지어내지 않는다.
    public enum CommunitySort { Latest }
}
```

- [ ] **Step 2: 인터페이스**

`Assets/Project/Scripts/Interfaces/ICommunitySource.cs`

```csharp
using System;
using System.Collections.Generic;

namespace ARZone
{
    // 커뮤니티 데이터의 유일한 접점.
    // 지금은 MockCommunitySource(메모리), 나중에 ApiCommunitySource(UnityWebRequest) 로 갈아끼운다.
    // 화면 코드는 이 인터페이스만 보므로 서버가 붙어도 안 바뀐다.
    //
    // 콜백 방식인 이유: UnityWebRequest 가 코루틴이라 콜백이 그대로 맞고,
    // Mock 은 즉시 done 을 부르면 되므로 호출부가 동일하다.
    // fail 은 나중에 네트워크 오류를 토스트로 띄울 자리다 — Mock 은 부르지 않는다.
    public interface ICommunitySource
    {
        void LoadPosts(string board, string query, CommunitySort sort,
                       Action<List<CommunityPost>> done, Action<string> fail);
        void CreatePost(CommunityPost draft, Action<CommunityPost> done, Action<string> fail);
        void ToggleLike(string postId, Action<int, bool> done, Action<string> fail);
        void AddComment(string postId, string body, Action<CommunityComment> done, Action<string> fail);
        void DeletePost(string postId, Action done, Action<string> fail);
        void ReportPost(string postId, Action done, Action<string> fail);
    }
}
```

- [ ] **Step 3: 시드 ScriptableObject**

`Assets/Project/Scripts/App/CommunitySeed.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 목업 글 데이터. 인스펙터에서 편집한다.
    // 작성자 아이콘은 회원가입에서 고르는 아바타 3종을 돌려 쓴다
    // (avatar_male_circle · avatar_female_circle · avatar_animal_circle).
    [CreateAssetMenu(fileName = "CommunitySeed", menuName = "ARZone/Community Seed")]
    public class CommunitySeed : ScriptableObject
    {
        public List<CommunityPost> posts = new List<CommunityPost>();
    }
}
```

- [ ] **Step 4: Mock 구현**

`Assets/Project/Scripts/App/MockCommunitySource.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 서버 없이 도는 메모리 구현. 시드에서 초기 목록을 복사해 들고 있는다.
    // 모든 콜백을 즉시 부른다 — fail 은 절대 부르지 않는다.
    public class MockCommunitySource : ICommunitySource
    {
        readonly List<CommunityPost> posts = new List<CommunityPost>();
        int nextId = 1;

        public MockCommunitySource(CommunitySeed seed)
        {
            if (seed == null) return;
            foreach (var p in seed.posts) posts.Add(Copy(p));
        }

        // ⚠ 시드는 ScriptableObject 에셋이다. 참조로 담으면 플레이 중의 좋아요·댓글이
        // 에셋 파일을 영구히 오염시킨다(플레이를 멈춰도 안 돌아온다). 반드시 깊은 복사한다.
        CommunityPost Copy(CommunityPost s)
        {
            var p = new CommunityPost
            {
                id        = string.IsNullOrEmpty(s.id) ? "seed" + (nextId++) : s.id,
                board     = s.board,
                title     = s.title,
                body      = s.body,
                author    = s.author,
                timeText  = s.timeText,
                likes     = s.likes,
                comments  = s.comments,
                liked     = s.liked,
                mine      = s.mine,
                authorIcon = s.authorIcon,
            };
            if (s.photos != null) p.photos.AddRange(s.photos);   // Sprite 는 에셋이라 참조 공유해도 된다
            if (s.replies != null)
                foreach (var c in s.replies)
                    p.replies.Add(new CommunityComment
                    {
                        author = c.author, timeText = c.timeText,
                        body = c.body, authorIcon = c.authorIcon,
                    });
            return p;
        }

        public void LoadPosts(string board, string query, CommunitySort sort,
                              Action<List<CommunityPost>> done, Action<string> fail)
        {
            var list = new List<CommunityPost>();
            foreach (var p in posts)
            {
                if (!string.IsNullOrEmpty(board) && board != "전체" && p.board != board) continue;
                if (!string.IsNullOrEmpty(query)
                    && (p.title == null || p.title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    && (p.body == null || p.body.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                list.Add(p);
            }
            // 목록은 이미 최신순으로 들고 있다(새 글은 맨 앞에 넣는다). CommunitySort 는 Latest 하나뿐.
            done?.Invoke(list);
        }

        public void CreatePost(CommunityPost draft, Action<CommunityPost> done, Action<string> fail)
        {
            draft.id = "local" + (nextId++);
            draft.timeText = "방금 전";
            draft.mine = true;
            posts.Insert(0, draft);
            done?.Invoke(draft);
        }

        public void ToggleLike(string postId, Action<int, bool> done, Action<string> fail)
        {
            var p = Find(postId);
            if (p == null) return;
            p.liked = !p.liked;
            p.likes += p.liked ? 1 : -1;
            if (p.likes < 0) p.likes = 0;
            done?.Invoke(p.likes, p.liked);
        }

        public void AddComment(string postId, string body, Action<CommunityComment> done, Action<string> fail)
        {
            var p = Find(postId);
            if (p == null) return;
            var c = new CommunityComment { author = "나", timeText = "방금 전", body = body };
            p.replies.Add(c);
            p.comments = p.replies.Count;
            done?.Invoke(c);
        }

        public void DeletePost(string postId, Action done, Action<string> fail)
        {
            var p = Find(postId);
            if (p != null) posts.Remove(p);
            done?.Invoke();
        }

        public void ReportPost(string postId, Action done, Action<string> fail)
        {
            // 목업에선 아무것도 안 한다. 서버가 붙으면 여기서 POST 한다.
            done?.Invoke();
        }

        CommunityPost Find(string id)
        {
            foreach (var p in posts) if (p.id == id) return p;
            return null;
        }
    }
}
```

- [ ] **Step 5: Store**

`Assets/Project/Scripts/App/CommunityStore.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARZone
{
    // 현재 소스를 들고, 화면들이 같은 목록을 보게 한다.
    // 목록이 바뀌면 OnChanged 로 알려 목록·상세가 다시 그리게 한다.
    public class CommunityStore : MonoBehaviour
    {
        public enum SourceKind { Mock }   // Api 가 생기면 여기에 추가한다

        [SerializeField] SourceKind source = SourceKind.Mock;
        [SerializeField] CommunitySeed seed;

        public static CommunityStore Instance { get; private set; }

        public event Action OnChanged;

        ICommunitySource src;
        readonly List<CommunityPost> cache = new List<CommunityPost>();

        public List<CommunityPost> Posts => cache;
        public string Board { get; private set; } = "전체";
        public string Query { get; private set; } = "";

        void Awake()
        {
            Instance = this;
            src = new MockCommunitySource(seed);   // source 가 늘어나면 여기서 분기한다
            Load(Board, Query);
        }

        public void Load(string board, string query)
        {
            Board = board; Query = query;
            src.LoadPosts(board, query, CommunitySort.Latest, list =>
            {
                cache.Clear(); cache.AddRange(list);
                OnChanged?.Invoke();
            }, err => Debug.LogError("[Community] 목록 실패: " + err));
        }

        public void Create(CommunityPost draft) =>
            src.CreatePost(draft, _ => Load(Board, Query),
                           err => Debug.LogError("[Community] 등록 실패: " + err));

        public void ToggleLike(string id) =>
            src.ToggleLike(id, (n, on) => OnChanged?.Invoke(),
                           err => Debug.LogError("[Community] 좋아요 실패: " + err));

        public void AddComment(string id, string body) =>
            src.AddComment(id, body, _ => OnChanged?.Invoke(),
                           err => Debug.LogError("[Community] 댓글 실패: " + err));

        public void Delete(string id) =>
            src.DeletePost(id, () => Load(Board, Query),
                           err => Debug.LogError("[Community] 삭제 실패: " + err));

        public void Report(string id) =>
            src.ReportPost(id, () => Toast.Show("신고가 접수되었습니다."),
                           err => Debug.LogError("[Community] 신고 실패: " + err));

        public CommunityPost Find(string id)
        {
            foreach (var p in cache) if (p.id == id) return p;
            return null;
        }
    }
}
```

- [ ] **Step 6: 컴파일 검증**

`cs.rsp` 를 새로 만들어(새 .cs 5개가 csproj 에 없으므로 반드시 다시 생성) 돌린다.

```bash
SP=<스크래치패드>
cd ~/Desktop/ARZONE-App && python3 - <<'EOF'
import re, html, os
sp = os.environ['SP']
t = open('Assembly-CSharp.csproj', encoding='utf-8').read()
refs = [html.unescape(m) for m in re.findall(r'<HintPath>(.*?)</HintPath>', t)]
srcs = [html.unescape(m).replace('\\', '/') for m in re.findall(r'<Compile Include="(.*?)"', t)]
have = set(srcs)
for root, _, fs in os.walk('Assets/Project/Scripts'):
    for f in fs:
        if f.endswith('.cs'):
            p = os.path.join(root, f)
            if p not in have: srcs.append(p)
with open(os.path.join(sp, 'cs.rsp'), 'w') as o:
    o.write('-target:library\n-nostdlib+\n-out:%s/out.dll\n' % sp)
    for r in refs: o.write('-r:"%s"\n' % r.replace('\\', '/'))
    for s in srcs: o.write('"%s"\n' % s)
print('srcs', len(srcs))
EOF
S=/Applications/Unity/Hub/Editor/6000.3.17f1/Unity.app/Contents/Resources/Scripting
"$S/NetCoreRuntime/dotnet" "$S/DotNetSdkRoslyn/csc.dll" @$SP/cs.rsp 2>&1 | grep ": error"
```

기대: 아무것도 출력되지 않는다(에러 0).
`Toast.Show` 를 못 찾는다는 에러가 나면 `Toast.cs` 의 시그니처가
`public static void Show(string message, float bottom = 58f)` 인지 확인한다.

- [ ] **Step 7: 유저에게 시드 에셋 생성을 요청**

`CommunitySeed` 는 `[CreateAssetMenu]` 라 **유니티 에디터에서 만들어야 한다**(스크립트로 못 만든다).
유저에게 이렇게 요청한다:

> 유니티에서 `Assets/Project/Resources` 우클릭 → Create → ARZone → Community Seed 로
> `CommunitySeed.asset` 을 만들고, 씬의 `CommunityStore` 컴포넌트 `seed` 칸에 물려주세요.
> 목업 글 내용은 제가 시안 문구 그대로 채워 넣겠습니다.

---

## Task 3: Screen_Community 껍데기 + 탭바 배선

빈 화면을 만들고 탭에서 들어갈 수 있게 한다. **이 태스크가 끝나면 탭을 눌러 커뮤니티로 이동한다.**

**Files:**
- Create: `tools/p_community_screen.py`
- Modify: `Assets/Project/Scenes/Phone.unity`

**Interfaces:**
- Consumes: 없음
- Produces: 씬에 `Screen_Community` 루트(+ `Bg` · `TabBar` · `BtnARBg` · `BtnAR`).
  `ScreenManager` 에 `Community` 로 등록된다.

- [ ] **Step 1: 현재 탭 배선 상태를 찍어 둔다**

```bash
cd ~/Desktop/CUBE/tools && python3 - <<'EOF'
import arb, re
arb.load_guids(); d = arb.Doc(arb.SCENE)
SB = [g for g, n in arb.GUIDS.items() if n == 'ScreenButton'][0]
for scr in ['Screen_Home', 'Screen_Hub', 'Screen_Market']:
    go = next((g for g in d.find('TabFriend')
               if d.path_of(d.rt_of(g)) == f'Canvas/{scr}/TabBar/TabFriend'), None)
    has = any(f'guid: {SB}' in d.body(c) for c in d.comps(go)) if go else None
    print(scr, 'TabFriend go', go, 'ScreenButton', has)
EOF
```

기대: 세 화면 모두 `ScreenButton False` (지금 죽어 있다).

- [ ] **Step 2: 화면 생성 스크립트를 쓴다**

`tools/p_community_screen.py`. `Screen_Market` 의 `Bg` · `TabBar` · `BtnARBg` · `BtnAR` 을
`clone_tree` 로 그대로 복제하고, 탭바의 활성 표시만 가운데(친구)로 옮긴다.

```python
#!/usr/bin/env python3
"""Screen_Community 껍데기 + TabFriend 배선."""
import arb, mkui

d = arb.Doc(arb.SCENE)
b = mkui.Builder(d, first_id=1910000000)

# ⚠ 같은 이름이 화면마다 있으므로 반드시 경로로 집는다. d.find()[0] 을 쓰면 엉뚱한 화면 것이 걸린다.
def pick(name, screen):
    return next(g for g in d.find(name)
                if d.path_of(d.rt_of(g)) == f'Canvas/{screen}/{name}')

src = {n: pick(n, 'Screen_Market') for n in ['Bg', 'TabBar', 'BtnARBg', 'BtnAR']}

nodes = [mkui.node('Bg', 'clone', (0, 0, 0, 0), src=src['Bg']),
         mkui.node('TabBar', 'clone', (20, -42, 216, 72), src=src['TabBar']),
         mkui.node('BtnARBg', 'clone', (-20.5, 42.5, 71, 71), src=src['BtnARBg']),
         mkui.node('BtnAR', 'clone', (-20.5, 42.5, 71, 71), src=src['BtnAR'])]
b.screen('Screen_Community', 'Community', nodes)
b.commit('Screen_Community 껍데기')
arb.verify()
```

⚠ `clone_tree` 는 `m_Father` 만 바꾸고 부모의 `m_Children` 에 등록하지 않는다.
`b.screen()` 이 자식 등록을 해주지만, 그 뒤에 수동으로 넣는 블록이 있으면 직접 등록해야 한다.

⚠ 복제한 `TabBar` 안의 `TabHomeBg`(선택된 탭의 보라 원)는 홈 자리에 있다. 시안은
**가운데(친구)** 가 활성이므로 x 를 옮겨야 한다 — 정확한 값은 다음 스텝에서 실측한다.

- [ ] **Step 3: 탭바 활성 위치를 시안에서 실측**

```bash
cd ~/Desktop/CUBE/tools && python3 svgmeasure.py "<시안폴더>/커뮤니티.svg" --all | grep -iE "72[0-9]|7[3-9][0-9]|8[0-5][0-9]"
```

탭바는 화면 하단 y≈779~851 구간이다. 보라 원의 x·크기를 읽어 `TabHomeBg` 의
`anchoredPosition` 을 그 값으로 바꾼다. **읽은 값을 표로 적어 대조해 보여준다.**

- [ ] **Step 4: TabFriend 에 ScreenButton 을 붙인다 (홈·허브·마켓·커뮤니티 4곳)**

`Screen_Market/TabMarket` 의 `ScreenButton` 블록을 원본으로 복제하고
`targetScreen: Community`, `back: 0` 으로 바꾼다. `Button` 컴포넌트가 없으면 같이 붙인다
(`guid: 4e29b1a8efbd4b44bb3f3716e73f07ff`).

- [ ] **Step 5: 검증**

```bash
cd ~/Desktop/CUBE/tools && python3 arb.py verify && python3 arb.py tree Screen_Community 2
```

기대: `중복 [] · 잘림 0 · 미해결참조 0`, 트리에 `Bg` · `TabBar` · `BtnARBg` · `BtnAR` 이 보인다.

- [ ] **Step 6: 유저 확인**

> 유니티에서 홈·허브·마켓의 가운데 탭을 눌러 커뮤니티로 넘어가는지, 탭바 보라 원이
> 가운데로 왔는지 봐주세요.

---

## Task 4: 목록 화면 정적 부분 (헤더 · 칩 · 검색바 · 정렬 · 빈 상태)

글 카드를 뺀 나머지를 시안대로 놓는다.

**Files:**
- Create: `tools/p_community_head.py`
- Modify: `Assets/Project/Scenes/Phone.unity`

**Interfaces:**
- Consumes: Task 3 의 `Screen_Community` 루트
- Produces: `LabelCommunity` · `ProfileIcon` · `BtnWrite`+`LabelWrite` ·
  `ChipAll`/`ChipFree`/`ChipCert` (+ 각 Label) · `SearchBar`+`IconSearch`+`LabelSearchHint` ·
  `BtnSort`+`LabelSort`+`IconSortArrow` · `PostList`(빈 컨테이너) · `LabelEmpty`(꺼둔 상태)

- [ ] **Step 1: 시안 실측**

```bash
cd ~/Desktop/CUBE/tools
python3 svgmeasure.py "<시안폴더>/커뮤니티.svg" --all > /tmp/community_measure.txt
head -60 /tmp/community_measure.txt
```

읽어야 할 값: 제목 "커뮤니티" 잉크 bbox · 프로필 원 · `새 글 쓰기` 버튼 박스와 테두리 ·
칩 3개의 박스와 글자 · 검색바 박스·돋보기·placeholder · `최신순` 글자와 화살표.

**글자 크기·굵기는 잉크 폭/밀도로 역산한다.** 표로 정리해 보여준다.

- [ ] **Step 2: 검색결과 없음 화면도 같이 잰다**

```bash
python3 svgmeasure.py "<시안폴더>/커뮤니티 검색결과 없음.svg" --all | head -40
```

빈 상태 문구와 그 위치를 읽는다. **시안에 없는 문구를 지어내지 않는다** — 읽은 그대로 쓴다.

- [ ] **Step 3: `p_community_head.py` 로 배치**

`mkui.Builder.make` 로 노드를 만든다. 앵커·피벗은 좌상단 `(0,1)`, `rect=(x, -y, w, h)`.
색은 `TokenStyle` 토큰으로, 유리 면(검색바 등)은 시안 합성값으로 계산해 넣는다.

- [ ] **Step 4: 검증 — 실측 대조표**

넣은 값과 시안 값을 나란히 출력한다.

```bash
cd ~/Desktop/CUBE/tools && python3 arb.py tree Screen_Community 2
python3 arb.py verify
```

기대: `중복 [] · 잘림 0 · 미해결참조 0`. 각 요소의 좌표·크기가 실측값과 ±0.5px 안쪽.

- [ ] **Step 5: 유저 확인**

> 커뮤니티 목록의 헤더·칩·검색바·정렬 줄이 시안과 맞는지 봐주세요. 글 카드는 다음 태스크입니다.

---

## Task 5: 글 카드 렌더링 (CommunityController)

카드를 코드로 생성하고 Store 와 연결한다. **이 태스크가 끝나면 목록에 목업 글이 뜬다.**

**Files:**
- Create: `Assets/Project/Scripts/App/CommunityController.cs`
- Modify: `Assets/Project/Scenes/Phone.unity` (Screen_Community 에 컴포넌트 부착 + 배선)

**Interfaces:**
- Consumes: `CommunityStore.Instance` · `.Posts` · `.OnChanged` · `.Load(board, query)` (Task 2),
  `ic_comment.png` GUID (Task 1), `PostList` 컨테이너 (Task 4)
- Produces: `CommunityController.SetBoard(string)` · `.SetQuery(string)` — 칩과 검색바가 부른다.
  카드를 누르면 `ScreenManager.Show("CommunityPost")` 로 넘어가고, 대상 글 id 를
  `CommunityPostController.Target` 에 넣는다.

- [ ] **Step 1: 카드 내부 실측**

카드 top-left 기준 상대좌표로 잰다: 게시판 뱃지 · `⋮` · 제목 · 본문 2줄 · `작성자 · 시간` ·
하트+숫자 · 말풍선+숫자. 카드 자체의 크기·간격도 잰다.

- [ ] **Step 2: 컨트롤러를 쓴다**

`MarketController` 와 같은 모양으로 만든다 — `OnEnable` 에서 `Build()`,
`UiUtil.ClearChildren` → `UiUtil.MakeNode`/`MakeImage`/`MakeText` 로 카드 생성.
유리 카드이므로 `bg.gameObject.AddComponent<GlassGradient>()` 를 붙인다.
본문은 2줄에서 말줄임(`m_overflowMode` Ellipsis).

- [ ] **Step 3: 컴파일 검증**

Task 2 Step 6 과 같은 방법. 기대: 에러 0.

- [ ] **Step 4: 씬 배선**

`Screen_Community` 에 `CommunityController` 를 붙이고 `postList` · `fontSource` ·
`cardSprite`(bg_round16) · `heartSprite` · `commentSprite` · `moreSprite` 를 물린다.

⚠ **`CommunityStore` 는 `Canvas/ScreenManager` 오브젝트에 붙인다.** 화면 루트에 붙이면
`ScreenManager` 가 화면을 끌 때 같이 꺼져서 상세·글쓰기에서 접근할 수 없다.
`ScreenManager` 는 항상 켜져 있는 유일한 오브젝트다.

- [ ] **Step 5: 검증**

```bash
cd ~/Desktop/CUBE/tools && python3 arb.py verify
```

그리고 유저에게:

> 커뮤니티 목록에 목업 글 카드가 뜨는지, 카드 안 요소 위치가 시안과 맞는지 봐주세요.
> 콘솔에 `[Community]` 로 시작하는 에러가 없어야 합니다.

---

## Task 6: Screen_CommunityPost (글 상세) — 본문까지

**Files:**
- Create: `tools/p_community_post.py`
- Create: `Assets/Project/Scripts/App/CommunityPostController.cs`
- Modify: `Assets/Project/Scenes/Phone.unity`

**Interfaces:**
- Consumes: `CommunityStore.Instance.Find(id)` (Task 2)
- Produces: `CommunityPostController.Target` (static string, 목록이 넣는다) ·
  화면 오브젝트 `BtnBack` · `NavTitle` · `AuthorIcon` · `LabelAuthor` · `LabelTime` · `BtnMore` ·
  `LabelPostTitle` · `LabelPostBody` · `PhotoScroll` · `BtnLike`+`LabelLike` · `BtnComment`+`LabelComment`

- [ ] **Step 1: 시안 실측** — `커뮤니티 글 상세.svg`
- [ ] **Step 2: 화면 생성** (`p_community_post.py`, `mkui`)
- [ ] **Step 3: 컨트롤러** — `Target` 으로 글을 찾아 채우고, 사진이 없으면 `PhotoScroll` 을 끈다
- [ ] **Step 4: 좋아요 토글** — `CommunityStore.ToggleLike(id)` 호출 후 하트·숫자 갱신
- [ ] **Step 5: 컴파일 + `arb.py verify`**
- [ ] **Step 6: 유저 확인** — 목록에서 카드를 눌러 상세로 가고, 좋아요가 눌리는지

---

## Task 7: 댓글 목록 + 입력바

**Files:**
- Modify: `Assets/Project/Scripts/App/CommunityPostController.cs`
- Modify: `Assets/Project/Scenes/Phone.unity`

**Interfaces:**
- Consumes: `CommunityStore.AddComment(id, body)` (Task 2)
- Produces: `CommentList` 컨테이너 · `CommentInput`(TMP_InputField) · `BtnSend`

- [ ] **Step 1: 실측** — `커뮤니티 글 상세 댓글.svg` 의 댓글 행과 하단 입력바
- [ ] **Step 2: 댓글 행을 코드로 생성** — 없으면 "아직 댓글이 없습니다."(시안 문구 그대로)
- [ ] **Step 3: 입력바 배선** — 전송하면 `AddComment` → 입력 비우기 → `Toast.Show("댓글이 등록되었습니다.")`
  ⚠ `mkui` 가 만든 Image 는 `raycastTarget 0` 이라 탭이 안 먹는다. 입력 필드는 코드로 켠다.
- [ ] **Step 4: 컴파일 + `arb.py verify`**
- [ ] **Step 5: 유저 확인** — 댓글을 달면 목록에 붙고 상세·목록의 댓글 수가 같이 오르는지

---

## Task 8: Screen_CommunityWrite (글쓰기)

**Files:**
- Create: `tools/p_community_write.py`
- Create: `Assets/Project/Scripts/App/CommunityWriteController.cs`
- Modify: `Assets/Project/Scenes/Phone.unity`

**Interfaces:**
- Consumes: `CommunityStore.Create(draft)` (Task 2), `ic_photo_attach.png` GUID (Task 1)
- Produces: `CommunityWriteController.Attach(List<Sprite>)` — 사진 시트가 첨부 결과를 돌려줄 때 부른다

- [ ] **Step 1: 실측** — `커뮤니티 글쓰기.svg` · `글쓰기-1.svg` · `글 입력안한 상태_토스트 표시_.svg`
- [ ] **Step 2: 화면 생성** — 헤더(`<` · 글쓰기 · 등록) · 게시판 칩 2개 · 제목+밑줄 · 내용 · 사진 줄
- [ ] **Step 3: 등록 활성 조건** — **제목과 내용이 둘 다 채워져야 활성.** 비활성일 때 누르면
      시안의 토스트 문구를 그대로 띄운다(문구는 Step 1 에서 읽은 것을 쓴다)
- [ ] **Step 4: 등록 동작** — `CommunityStore.Create` → `ScreenManager.Show("Community")` →
      `Toast.Show("게시글이 등록되었습니다.")`
- [ ] **Step 5: 컴파일 + `arb.py verify`**
- [ ] **Step 6: 유저 확인** — 글을 써서 등록하면 목록 맨 위에 뜨는지

---

## Task 9: Sheet_CommunityPhoto (사진 선택)

**Files:**
- Create: `tools/p_community_photo.py`
- Create: `Assets/Project/Scripts/App/CommunityPhotoSheet.cs`
- Modify: `Assets/Project/Scenes/Phone.unity`

**Interfaces:**
- Consumes: `CommunityWriteController.Attach(List<Sprite>)` (Task 8), `CaptureService` (기존)
- Produces: 없음 (말단)

⚠ **기존 `Sheet_ImagePicker` / `ImagePickerSheet.cs` 는 건드리지 않는다.**
그쪽은 `TalkDetailController` 를 직접 참조하고 있어 공용화하면 대화 흐름에 회귀가 난다.
레이아웃 수치는 `ImagePickerSheet.cs` 주석의 실측값을 **참고만** 하고, 번호 뱃지·`(n/5)` 제목·
`첨부` 버튼은 이 시안에서 새로 잰다.

- [ ] **Step 1: 실측** — `커뮤니티 사잔첨부.svg` · `커뮤니티 사잔첨부 사진 선택.svg`
      (그리드 열 수·카드 크기·간격, 번호 뱃지 원 크기와 위치, 헤더 3요소)
- [ ] **Step 2: 시트 생성** (`mkui`, `Sheet_MarketAddress` 를 구조 참고)
- [ ] **Step 3: 컨트롤러** — 최대 5장, 고른 순서대로 1..5 번호, 제목에 `(n/5)` 반영,
      `첨부` 는 1장 이상일 때만 활성
- [ ] **Step 4: 컴파일 + `arb.py verify`**
- [ ] **Step 5: 유저 확인** — 글쓰기에서 사진 줄을 눌러 시트가 뜨고, 5장까지 번호가 붙는지

---

## Task 10: Popup_PostMenu + Modal_PostDelete

**Files:**
- Create: `tools/p_community_menu.py`
- Create: `Assets/Project/Scripts/App/PostMenuPopup.cs`
- Modify: `Assets/Project/Scenes/Phone.unity`

**Interfaces:**
- Consumes: `CommunityStore.Delete(id)` · `.Report(id)` (Task 2)
- Produces: `PostMenuPopup.Open(string postId, bool mine, Vector2 anchor)` —
  목록 카드와 상세의 `⋮` 가 부른다

- [ ] **Step 1: 실측** — `커뮤니티.svg` / `커뮤니티 글 상세.svg` 에 열려 있는 ⋮ 팝업
      (박스·모서리 반지름·두 줄의 아이콘과 글자·`삭제하기` 빨강)
- [ ] **Step 2: 팝업 생성** — 아이콘은 기존 `ic_report.svg` · `ic_trash.svg` 재사용
- [ ] **Step 3: `Modal_PostDelete` 생성** — **`Modal_ArchiveDelete` 를 통째로 복제**하고
      문구만 바꾼다:
      `Title` = `게시글을 삭제하시겠습니까?` · `Sub` = `삭제한 게시글은 복구할 수 없습니다.`
      구조·좌표는 원본 그대로 (`Card` (20,−311) 372×272 · 버튼 163×53 / 164×54).
      ⚠ 복제하면 `m_IsActive: 0` 이 딸려온다 — 모달은 꺼진 상태가 정상이므로 그대로 둔다.
      ⚠ fileID 가 겹치지 않게 새 id 로 만들고 `arb.py verify` 로 `중복 []` 을 확인한다.
- [ ] **Step 4: 배선** — `삭제하기` → 모달 → `삭제` → `CommunityStore.Delete(id)`,
      `신고하기` → `CommunityStore.Report(id)` (토스트가 뜬다)
- [ ] **Step 5: 컴파일 + `arb.py verify`**
- [ ] **Step 6: 유저 확인** — 내 글의 ⋮ 에 삭제하기가 뜨고, 삭제 확인 모달 문구가 맞는지

---

## Task 11: 검색 · 게시판 필터 · 빈 상태 연결

지금까지 만든 조각을 실제로 동작시킨다.

**Files:**
- Modify: `Assets/Project/Scripts/App/CommunityController.cs`
- Modify: `Assets/Project/Scenes/Phone.unity`

**Interfaces:**
- Consumes: `CommunityController.SetBoard(string)` · `.SetQuery(string)` (Task 5),
  `LabelEmpty` (Task 4)
- Produces: 없음 (말단)

- [ ] **Step 1: 칩 배선** — `전체`/`자유게시판`/`인증게시판` → `SetBoard`, 선택된 칩만 보라 알약
- [ ] **Step 2: 검색바 배선** — 입력이 바뀌면 `SetQuery`
      ⚠ `mkui` 가 만든 Image 는 `raycastTarget 0` 이다. 입력 필드는 코드로 켠다.
- [ ] **Step 3: 빈 상태** — 결과가 0건이면 `LabelEmpty` 를 켜고 `PostList` 를 비운다
- [ ] **Step 4: 컴파일 + `arb.py verify`**
- [ ] **Step 5: 전체 흐름 확인**

> 탭 → 목록 → 칩 전환 → 검색(결과 있음/없음) → 새 글 쓰기 → 사진 첨부 → 등록 →
> 목록 맨 위 확인 → 카드 눌러 상세 → 좋아요 → 댓글 → ⋮ → 삭제까지 한 번에 돌려봐 주세요.

---

## 남은 것 (이번 계획에 없음)

- **서버 연동** — `ApiCommunitySource` 는 서버 스펙(엔드포인트·필드명·페이지네이션·인증)이
  나온 뒤에 만든다. 그때 화면 코드는 손대지 않는다.
- **이미지 업로드** — 사진은 기기 캡처에서 고르는 데까지. 업로드는 API 나온 뒤.
- **무한 스크롤/페이지네이션** — `LoadPosts` 에 인자를 추가하는 형태로 확장한다.
- **신고 후 동작** — 시안이 없어 토스트로 뒀다. 다른 게 필요하면 Task 10 Step 4 만 바꾼다.
