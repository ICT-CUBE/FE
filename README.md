# ARZONE-App (FE)

CUBE 프로젝트의 스마트폰 앱. Unity 6000.3.17f1 · URP · Android(IL2CPP).

## clone 후 반드시 할 것 — Vuforia 패키지 넣기

`Packages/manifest.json` 이 Vuforia SDK 를 로컬 파일로 참조한다:

```json
"com.ptc.vuforia.engine": "file:com.ptc.vuforia.engine-11.4.4.tgz"
```

이 `.tgz` 는 **138MB 라 깃허브 100MB 제한을 넘어서 리포에 없다.** 팀에서 따로 공유한
`com.ptc.vuforia.engine-11.4.4.tgz` 를 `Packages/` 폴더에 넣고 나서 Unity 로 열 것.
없이 열면 패키지 해석에 실패한다.

## 브랜치

| 브랜치 | 용도 |
|---|---|
| `main` | 릴리스. 직접 커밋하지 않는다. |
| `dev` | 통합 브랜치. **작업은 여기로 PR 을 보낸다.** |
| `feature/*` | 기능 단위 작업. `dev` 에서 따고 `dev` 로 PR. |

## 구조

```
Assets/Project/
  Scenes/Phone.unity      모든 화면이 한 씬에 Screen_* 루트로 들어 있다
  Scripts/
    Core/                 DesignTokens · Typography 등 디자인 토큰
    Interfaces/           인터페이스 격리 계층
    Detectors/            인식(Vuforia / Mock)
    App/                  화면 컨트롤러 · UI 유틸
  Sprites/  Resources/  Fonts/  Avatars/  Prefabs/
```

네임스페이스는 `ARZone` (폴더명의 하이픈은 C# 네임스페이스에 못 쓴다).

## 화면 전환

`ScreenManager.Show("<이름>")` 이 `Screen_<이름>` 루트를 켜고 나머지를 끈다.
버튼에는 `ScreenButton` 컴포넌트의 `targetScreen` 으로 배선한다.

## 디자인 토큰

색·타이포는 `DesignTokens` / `Typography` 에 정의돼 있고, 씬에서는 `TokenStyle`
컴포넌트로 골라 쓴다. 색을 손으로 넣지 않는 것이 원칙이다.

> ⚠ 유니티는 Linear 색공간, 피그마는 sRGB 라 **같은 알파라도 결과가 다르다.**
> 반투명(유리) 면은 시안에서 합성이 끝난 색을 불투명하게 칠한다 — `GlassGradient.cs` 주석 참고.
