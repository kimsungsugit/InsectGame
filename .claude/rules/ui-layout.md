# UI 레이아웃 규칙 — 세로 마진 하네스 + 공통 서피스

두 하네스가 역할을 나눈다. **어디에 놓을지는 `UISafeLayout`, 어떤 질감으로 칠할지는 `UISurface`.**
둘 다 우회하지 않는다.

## 표면은 `UISurface`다 — 각진 사각형을 직접 그리지 않는다

패널·카드·행 배경을 `GUI.DrawTexture(rect, Texture2D.whiteTexture)`로 직접 칠하지 않는다.
`Assets/Scripts/UI/UISurface.cs`가 그림자 + 둥근 모서리 + 테두리를 한 번에 준다.

```csharp
// Before — 각진 사각형
GUI.color = SomeBgCol;
GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);
GUI.color = Color.white;

// After
UISurface.Card(new Rect(px, py, pw, ph), SomeBgCol, UITheme.Instance.surfaceBorder);
```

| 호출 | 쓰는 자리 |
|---|---|
| `Card(rect, bg, border)` | 패널·카드·목록 행 |
| `Card(rect)` | 테마 기본색 카드 |
| `Rounded(rect, color[, radius])` | 헤더 바, 배지 바탕 |
| `Flat(rect, color)` | **얇은 것** — 구분선·진행바·액센트 스트라이프 |
| `Button(rect, label, bg, style[, selected])` | 호버 반응이 필요한 버튼 |
| `Chip(rect, text, bg, textColor)` | 등급·속성·보상 배지 |
| `StatRow(rect, label, value, lc, vc)` | 라벨/값 한 줄 |
| `Header(rect, title, subtitle[, closeLabel])` | 액센트 헤더 + 닫기 (닫힘 여부 반환) |
| `ScrollAffordance(viewport, scroll, contentH, accent)` | 스크롤 위치 표시 |
| `Dim(alpha)` | 모달 뒤 딤 (**가상 좌표계 전용**, 픽셀이면 `UIHelper.DrawDimOverlay`) |

**헤더 바를 패널 위에 겹칠 때는 3px 안쪽으로 물린다** — 그러지 않으면 각진 헤더가 둥근
패널 모서리를 뚫고 나온다: `Rounded(new Rect(px + 3f, py + 3f, pw - 6f, headerH), headerBg)`.

**얇은 것(짧은 변이 대략 8px 이하)은 `Flat`으로 각지게 둔다.** 둥근 배경은 9-slice라
테두리 폭이 `반경 + 4`인데, 짧은 변이 그보다 작으면 슬라이스가 겹쳐 뭉개진다.
높이만이 아니라 **폭**도 해당한다 — 세로로 긴 5~7px 등급 레일이 같은 경우다.

둥근 패널 위에 얇은 바를 얹을 때는 **긴 축을 반경만큼 물린다** — 각진 바는 둥근 모서리를
그대로 뚫고 나온다: `Flat(new Rect(px + UITheme.Radius.Card, py + 3f, pw - UITheme.Radius.Card * 2f, 8f), col)`
(`HospitalUI`의 적십자 스트라이프가 그 형태다).

`Rounded`는 **알파만 호출부 것을 살리고 RGB는 흰색으로 고정한다.** 텍스처가 이미 색을 굽고
있어 RGB까지 곱하면 이중 착색이 되고, 흰색을 통째로 대입하면 패널 페이드에서 배경만 빠진다.
(`UIHelper.DrawTinted`가 전면 곱셈인 건 그쪽이 **흰** 텍스처를 그리기 때문이다 — 규칙이 다르다.)

## 색은 `UITheme` 토큰에서만 받는다

화면 파일에 `static readonly Color`를 새로 늘리지 않는다. `surfaceBase`/`surfaceCard`/
`surfaceRaised`/`surfaceBorder`/`surfaceShadow`, `accentCoral`/`accentAmber`/`accentMint`,
`textPrimary`/`textSecondary`/`textMuted`가 있다. 간격은 `UITheme.Space.*`, 반경은 `UITheme.Radius.*`.

(도감은 한때 밝은 크림/코랄 팔레트 47색을 자기 파일에 박아두어 혼자 다른 앱처럼 보였다.
지금은 전부 토큰 파생이다 — `DexScreenUI`의 색 블록이 그 형태의 본보기다.)

## 배치의 단일 출처는 `UISafeLayout`이다

패널의 **y와 height를 직접 계산하지 않는다.** `Assets/Scripts/UI/UISafeLayout.cs`가 주는
`Rect`를 받아 쓴다. 세이프에어리어(노치·제스처바)와 세로 마진이 이미 빠진 좌표다.

```csharp
Rect panel = UISafeLayout.CenteredPanel(960f, 940f);   // 높이는 안전 영역 안으로 자동 clamp
float px = panel.x, py = panel.y, pw = panel.width, ph = panel.height;
```

세로 마진 = **화면 높이 × 3% (24~64px clamp)**. 세이프에어리어 위에 추가로 얹는다.
인셋이 0인 데스크톱에서도 가장자리에 붙지 않고, 인셋이 있는 기기에서는 그만큼 더 안쪽으로 들어간다.

가로 마진은 24px 고정이다 — 세로만 늘렸다. 잘림은 세로에서 났고 가로 폭은 기존 레이아웃을 유지한다.

## API

| 호출 | 쓰는 자리 |
|---|---|
| `CenteredPanel(w, h)` | 화면 중앙 모달 |
| `AnchoredPanel(w, h, HAlign)` | 좌/우 정렬 패널 (`CollectionUI`처럼 우측에 붙는 것) |
| `TopPanel(w, h[, HAlign])` | 상단 앵커 배너·HUD |
| `BottomPanel(w, h[, HAlign])` | 하단 앵커 바·버튼 |
| `ContentTop` / `ContentBottom` / `ContentHeight` | y만 필요할 때(폭은 호출부가 이미 정함) |
| `CenteredY(h)` / `BottomY(h)` | 위와 같음 — 세로 좌표 하나만 |
| `ClampHeight(desired)` | 내용 길이에 따라 자라는 높이의 상한 |
| `Overflows(desired)` | 스크롤이 필요한지 판단 |
| `Content` | 전체화면 UI의 콘텐츠 영역(`DexScreenUI`) |
| `UISafeLayout.Px.*` | `UIScale.Begin()`을 쓰지 않는 픽셀 좌표계 UI |

`UIScale.Begin()` 안이면 그냥 `UISafeLayout.*`, 픽셀 좌표로 그리면 `UISafeLayout.Px.*`.
두 파사드는 같은 순수 계산부(`Compute`/`ClampSize`/`CenterStart`)를 공유한다.

## 금지 관용구 — `ui_layout_lint.py`가 잡는다

| 금지 | 대신 |
|---|---|
| `(VirtualScreenHeight - panelH) * 0.5f` | `CenteredPanel` / `CenteredY` |
| `float y = VirtualScreenHeight - h - 16f` | `BottomY(h)` / `BottomPanel` |
| `VirtualSafeTop + 20f` | `ContentTop` |
| `VirtualScreenHeight - VirtualSafeTop - VirtualSafeBottom - 24f` | `ContentHeight` / `ClampHeight` |
| `float panelH = 1000f;` (안전 영역 확인 없이) | `ClampHeight(1000f)` 또는 하네스 Rect |

`Screen.height`/`SafeArea.*` 픽셀 버전도 동일하게 금지. 라인에 `UISafeLayout`이 있으면 검사에서 면제된다.

**비율 배치(`VirtualScreenHeight * 0.08f`)는 금지가 아니다** — 아레나·연출 좌표가 그렇게 잡혀 있고
잘림의 원인이 아니다. 다만 그 자리에 고정 높이 패널을 놓는다면 `Mathf.Clamp(비율값, ContentTop, ContentBottom - h)`로 가둔다.

## 면제 파일 (`EXEMPT_FILES`)

- `UISafeLayout.cs` / `UIScale.cs` / `SafeArea.cs` / `SafeAreaPanel.cs` — 하네스 자신
- `FieldHudInput.cs` — 터치 좌표 변환이지 배치가 아님
- `VirtualJoystickUI.cs` — 조이스틱 시작 가능 영역(입력 데드존)이다. 마진을 주면 조작 영역이 좁아진다
- `OpeningSceneController.cs` — shortSide 비율(16~28px) 자체 마진 체계가 있고 `CalculateSkipButtonRect`가
  `OpeningSequenceTests`로 고정돼 있다

면제를 늘리려면 이 목록과 이 문서를 함께 고친다. 스크립트에만 추가하지 않는다.

## 검증

```
python -X utf8 .claude/scripts/ui_layout_lint.py
```

`ci_check.py`의 `REPO_CHECKS`에도 들어 있어 세션 밖 편집(Codex CLI 등)도 CI가 잡는다.

순수 계산부는 `Assets/Tests/EditMode/UISafeLayoutTests.cs`가 검증한다 — 마진 clamp 경계,
비대칭 인셋 중앙 정렬, 하단 앵커, 그리고 인셋이 없을 때 옛 `HospitalUI` 3줄 관용구와 같은 값이 나오는지(회귀 방지).

## 내용이 넘칠 때

`ClampHeight`는 패널을 줄일 뿐 내용을 줄이지 않는다. 넘치는 화면은 둘 중 하나로 처리한다.

1. **목록·그리드**: `UIDirectScroll` + `GUI.BeginScrollView`. 뷰포트를 패널 높이에서 파생시키면
   패널이 줄어들 때 자동으로 스크롤로 넘어간다 (`DexScreenUI.cs:841-854`가 모범).
2. **고정 개수 행**(팀 5슬롯 등): 남은 높이에 맞춰 행 높이를 줄인다. 하한은 `UIScale.MinTouchHeight`.
   5줄짜리에 스크롤을 붙이는 것보다 낫다 (`BattleTeamUI.DrawTeamPanel`).

패널 높이에서 파생되는 뷰포트는 `Mathf.Max(1f, ...)`로 감싼다 — 안전 영역이 극단적으로 좁으면 음수가 된다.
