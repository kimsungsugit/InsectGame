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
| `ClampHeight(desired)` / `ClampWidth(desired)` | 내용 길이에 따라 자라는 크기의 상한 |
| `Overflows(desired)` | 스크롤이 필요한지 판단 |
| `Content` | 전체화면 UI의 콘텐츠 영역(`DexScreenUI`) |
| `ContentLeft` / `ContentWidth` | x만 필요할 때(가로 마진 24 고정판) |
| `MarginY` | 현재 화면의 세로 마진값 자체가 필요할 때 |
| `UISafeLayout.Px.*` | `UIScale.Begin()`을 쓰지 않는 픽셀 좌표계 UI |

`UIScale.Begin()` 안이면 그냥 `UISafeLayout.*`, 픽셀 좌표로 그리면 `UISafeLayout.Px.*`.
두 파사드는 같은 순수 계산부(`Compute`/`ClampSize`/`CenterStart`)를 공유한다.

**`UISafeLayout.ContentWidth`와 `UIScale.ContentWidth(margin)`은 중복이 아니다.**
앞쪽은 가로 마진 `MarginX`(24)로 **고정된 판**이고, 뒤쪽은 마진을 인자로 받는 **일반판**이다
(`CaptureMinigameController`는 28, `AccountSettingsUI`는 0을 넘긴다). 24가 맞으면 하네스 쪽을,
다른 마진이 필요하면 `UIScale` 쪽을 쓴다. 이 구분을 안 적어둬서 한동안 "같은 질문에 답이 둘"로
보였다 — 표에 없던 `ContentLeft`/`ContentWidth`/`MarginY`/`ClampWidth`를 여기 올린 이유다.

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

## `GUILayout.BeginArea`는 중첩하지 않는다 — 한 화면에 하나

Unity는 Area 중첩을 지원하지 않는다("Areas cannot be nested"). 그런데 이 저장소의 화면은
`OnGUI`가 패널 영역을 열고(`GUILayout.BeginArea(contentArea)`) 그리기 헬퍼가 그 안에서
또 여는 형태로 어긋나기 쉽다 — **호출이 서로 다른 메서드에 흩어져 있어 눈에 안 띈다.**

```csharp
// Before — 스크롤 콘텐츠 좌표계를 직접 리셋하려다 Area를 중첩했다
Rect viewport = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
position = GUI.BeginScrollView(viewport, position, new Rect(0f, 0f, contentWidth, contentHeight));
GUILayout.BeginArea(new Rect(0f, 0f, contentWidth, contentHeight));   // ← 바깥에 이미 Area가 열려 있다
DrawTabContent();
GUILayout.EndArea();
GUI.EndScrollView();

// After — 레이아웃 스크롤뷰가 좌표계·콘텐츠 높이·뷰포트를 스스로 관리한다
position = GUILayout.BeginScrollView(position, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
GUILayout.BeginVertical();
DrawTabContent();
GUILayout.EndVertical();
GUILayout.EndScrollView();
```

**증상이 조용하다**: 컴파일도 되고 예외도 없고 바깥 위젯(탭 버튼·헤더)은 멀쩡히 그려진다.
안쪽 내용만 통째로 사라진다. 2026-08-08에 한 커밋이 두 화면에 같은 구조를 심어
`CashShopUI`(상점 3탭 전부 빈칸)와 `SocialPvpUI`(친구·랭크전·배틀 3탭)가 함께 죽었다.

터치 드래그(`UIDirectScroll`)가 화면 좌표 뷰포트를 요구하는 게 원래 `GUI.BeginScrollView`를
쓴 이유였다. 레이아웃 스크롤뷰는 자기 `Rect`를 돌려주지 않으므로 `EndScrollView` 직후
`GUILayoutUtility.GetLastRect()`로 재서 **다음 프레임에 쓴다** — 한 프레임 늦지만 패널 크기는
매 프레임 바뀌지 않는다. 콘텐츠 높이도 같은 방법으로 안쪽 `BeginVertical`/`EndVertical`에서 잰다.

`ui_layout_lint.py`가 **파일당 `GUILayout.BeginArea` 2회 이상**을 중첩 의심으로 잡는다.
순차로 두 번 여는 합법 코드가 생기면 스크립트의 `NESTED_AREA_EXEMPT`에 근거와 함께 올린다.

## 면제 파일 (`EXEMPT_FILES`)

- `UISafeLayout.cs` / `UIScale.cs` / `SafeArea.cs` / `SafeAreaPanel.cs` — 하네스 자신
- `FieldHudInput.cs` — 터치 좌표 변환이지 배치가 아님
- `VirtualJoystickUI.cs` — 조이스틱 시작 가능 영역(입력 데드존)이다. 마진을 주면 조작 영역이 좁아진다
- `OpeningSceneController.cs` — shortSide 비율(16~28px) 자체 마진 체계가 있고 `CalculateSkipButtonRect`가
  `OpeningSequenceTests`로 고정돼 있다

면제를 늘리려면 이 목록과 이 문서를 함께 고친다. 스크립트에만 추가하지 않는다.

## 래핑 텍스트를 고정 높이 상자에 그리지 않는다 — `UIHelper.LabelFit`

`wordWrap = true` 스타일을 **고정 높이** Rect에 `GUI.Label`로 그리면, 줄바꿈이 일어나는
순간 넘치는 줄이 통째로 잘린다. 상자는 그대로 두고 **글자를 줄여 맞춘다**:

```csharp
// Before — 두 줄이 되면 아랫줄이 사라진다
GUI.Label(new Rect(x, y, w, 84f), data.description, descStyle);

// After
UIHelper.LabelFit(new Rect(x, y, w, 84f), data.description, descStyle);
```

`LabelFit`은 들어갈 때까지 폰트를 한 단계씩 줄이고 `UIHelper.MinReadableFontSize`(18)에서
멈춘다. 결과는 (텍스트, 폭, 높이, 기준 폰트)로 캐시되므로 적중 시 측정이 없다.
호출부의 공유 스타일은 그려진 뒤 폰트 크기가 원복된다.

**`wordWrap`을 끈 라벨은 세로가 아니라 가로로 잘린다.** 줄바꿈이 없으면 높이는 폰트 크기와
무관하게 늘 한 줄이라 세로 검사만으로는 축소가 절대 발동하지 않는다. `LabelFit`은 이 경우
`CalcSize().x`도 함께 보므로 그대로 쓰면 된다 — 두 방향 모두 이 헬퍼 하나로 덮인다.
(가운데 정렬이면 앞뒤가 같이 잘려 더 나쁘다. `RegionMapUI`의 `Label()` 헬퍼가 지도 핀을
1줄로 고정하려고 wordWrap을 전역으로 꺼서 지역 설명까지 이 경로를 탄다.)

**상자를 키우는 쪽이 맞는 자리라면** `UIHelper.MeasureWrappedHeight(style, text, width)`로
필요한 높이를 받아 레이아웃을 늘린다(`TutorialQuestUI`의 퀘스트 설명이 그 형태다).
스크롤 목록처럼 아래 요소가 밀려도 되는 곳에서만 쓴다 — 고정 패널에서 상자를 키우면
그 아래가 전부 밀려 회귀 범위가 커진다.

한국어는 같은 뜻을 더 긴 글자수로 쓰고 모바일에선 기준 폰트가 커져서, 데스크톱 Game View에서
멀쩡하던 라벨이 기기에서 잘리는 일이 반복됐다(도감 설명·아이템 설명·보유 곤충 설명·NPC 대사·
팀 슬롯 이름·가이드 배너).

### 리터럴 문구는 `literal_fit_lint.py`가 따로 잡는다

`text_fit_lint`는 **길이를 데이터가 정하는** 자리만 본다. 그래서
`GUI.Label(rect, "고정 문구", style)`처럼 리터럴을 쓰는 자리는 아무도 검사하지 않았고,
**36pt를 28px 상자에** 그리던 곳이 실제로 있었다(2026-08-08에 손으로 둘 찾아 고쳤다).

```
python -X utf8 .claude/scripts/literal_fit_lint.py
```

스타일의 `fontSize`와 Rect 높이를 소스에서 뽑아 대조한다. 한글 줄높이 ≈ `fontSize × 1.35`
(`DexScreenUI.LineH` / `TutorialQuestUI.RowH`와 같은 계산)보다 상자가 **10% 이상** 낮으면 FAIL이다.
그보다 작은 차이는 폰트 패딩에 묻혀 실제로 안 잘릴 수 있어 정보로만 센다 — 전부를 기준으로
삼으면 `text_fit_lint`가 247건에서 31건으로 좁혀졌던 실수를 되풀이한다.

**고치는 방법은 `UIHelper.LabelFit`이다.** 상자를 키우지 않는다 — 아래 요소가 전부 밀려
회귀 범위가 커지고, IMGUI는 배치모드로 캡처할 수 없어 결과를 눈으로 확인할 수도 없다
(`rules/testing.md`). 상자를 키우는 건 아래가 밀려도 되는 자리(스크롤 목록 등)에서
이웃 y를 검산한 뒤에만. 2026-08-17에 심각 31건을 전부 `LabelFit`으로 바꿨다.

### `text_fit_lint.py`가 잡는다

```
python -X utf8 .claude/scripts/text_fit_lint.py
```

**텍스트 길이를 데이터가 정하는데 상자가 고정**인 자리만 잡는다(`.description`/`.displayName`/
`lines[…]` 등). 처음엔 "래핑인데 두 줄이 안 들어감"으로 잡아 봤더니 **247건**이 나왔다 —
한 줄 라벨은 원래 높이가 fontSize의 1.2배쯤이라 정상 라벨을 전부 잡는다. 텍스트 출처를
좁히고 나서야 31건이 됐고, 그 31곳이 전부 진짜였다. `ci_check.py`의 `REPO_CHECKS`에 있다.

## 필드 위에 그리는 버튼은 `FieldHudInput.RegisterBlockingRect`에 등록한다

모달이 아닌 상태에서 월드 위에 겹쳐 그리는 버튼(퀵액세스 바, 잡기 버튼, 멀티플레이 패널 등)은
**매 `OnGUI`마다 자기 Rect를 등록해야 한다.** 안 하면 그 버튼을 누른 탭이 **월드 클릭-이동으로
새어** 캐릭터가 버튼 아래 지점으로 걸어간다.

왜 IMGUI만 이 문제를 겪나: `PlayerMovement`는 `Input.GetMouseButtonDown(0)`을 `Update`에서
**따로 폴링**한다. 그 시점에 ①탭한 프레임엔 아직 모달이 안 열려 `ModalUIRegistry.IsAnyOpen()`이
false고 ②IMGUI는 EventSystem을 안 거쳐 `pointerOverUI`도 false다. 남은 방어선이 이 등록 하나뿐이다.

```csharp
Rect barRect = new Rect(startX - 14, y - 8, totalW + 28, btnH + 16);
FieldHudInput.RegisterBlockingRect(barRect);   // 그리기 직전
GUI.DrawTexture(barRect, Texture2D.whiteTexture);
```

**픽셀 좌표계라면 `UIScale.Scale`로 나눠서 넘긴다.** `RegisterBlockingRect`는 가상 좌표를 받고
`IsScreenPointOverHud`가 화면 좌표를 `Scale`로 나눠 비교한다. `UIScale.Begin()`을 쓰지 않는
화면(`UISafeLayout.Px` 사용)이 그대로 넘기면 스케일이 1이 아닌 기기에서 엉뚱한 영역이 막힌다
(`WorldFieldMultiplayerUI.BlockFieldClicks`가 그 변환의 본보기다).

**네 번 났다.** 2026-08-17에 `QuickAccessBarUI`(메뉴를 열 때마다)와 `WorldFieldMultiplayerUI`
("3:3 대전"을 누르면 도전과 동시에 캐릭터가 상대 뒤로 걸어감)가 P0이었고, 2026-08-23에
`TutorialQuestUI`(퀘스트 칩·✕·목표 행)와 `SubAreaWorldBuilder`(진입/퇴장 버튼)가 또 나왔다.
마지막 것이 **이 게임에서 가장 큰 필드 버튼**이다(620×100, 하단 중앙) — 누르면 서브에리어로
들어가면서 동시에 **옛 월드 좌표를 향한 클릭-이동**이 걸린다.

`TutorialQuestUI`의 목표 행은 그중 새 코드였다. **규칙이 여기 적혀 있는데도 같은 함정을
그대로 밟았다** — 필드에 버튼을 새로 그릴 때 이 절을 다시 읽을 것.

검사기를 두지 않은 이유: "필드 위 비모달 버튼"을 정적으로 판정하려면 모달 여부·그리기 조건을
따라가야 해서 오탐이 크다. 전체 화면 모달은 등록하면 **안 되고**(모달 중에는 애초에 클릭-이동이
막힌다), 그 구분이 소스 패턴으로는 안 드러난다. 실제로 "IModalUI가 아닌데 버튼이 있고 등록이
없다"로 규칙을 짜 보면 네 건 중 **하나만** 잡고(나머지 셋은 IModalUI를 함께 구현한다) 로그인·
미니게임·오프닝이 거짓양성으로 걸린다.

### 대신 전수는 손으로 한다 — 이 표를 뽑아서 본다

**`GUI.Button`만 세면 안 된다.** 이 저장소의 HUD 절반은 버튼 위젯을 쓰지 않고
`Event.current` + `Rect.Contains`로 직접 히트테스트한다(`PlayerStatusHUD`의 닫힘 탭,
`TutorialQuestUI`의 퀘스트 칩). 버튼 문자열로만 훑다가 **가장 큰 HUD인 상태 패널을
통째로 놓쳤다**(2026-08-23). 두 관용구를 함께 센다:

```
for f in $(grep -rl "GUI.Button(\|UISurface.Button(\|EventType.MouseDown" --include=*.cs Assets/Scripts); do
  printf "%-50s 입력%-3s 등록%-3s 프리즈%-3s IModal%s\n" "${f#Assets/Scripts/}" \
    "$(grep -c "GUI.Button(\|UISurface.Button(\|EventType.MouseDown" "$f")" \
    "$(grep -c "RegisterBlockingRect" "$f")" \
    "$(grep -c "SetFrozen(true)" "$f")" \
    "$(grep -c ", IModalUI" "$f")"
done
```

읽는 법: **프리즈를 걸고 그리는 화면과 전체화면 모달은 안전하다**(그 상태에서는 클릭-이동이
이미 막힌다). 위험한 것은 **플레이어가 자유롭게 움직이는 동안 그려지는 것**뿐이다 —
로그인·오프닝(월드 없음), 미니게임·포획 선택(프리즈)은 제외하고 남는 것을 본다.

`evt.Use()`는 방어가 **아니다.** 그건 IMGUI 안에서만 유효한데 `PlayerMovement`는
`Input.GetMouseButtonDown(0)`을 Update에서 따로 폴링한다 — IMGUI 밖이라 소비 여부를 모른다.

2026-08-23 기준 등록된 파일: `CaptureInputController`, `WorldInteractionController`,
`QuickAccessBarUI`, `WorldFieldMultiplayerUI`, `TutorialQuestUI`, `SubAreaWorldBuilder`,
`MinimapUI`·`PlayerStatusHUD`(버튼 위젯은 없지만 불투명 패널이라 같이 막는다).

## 구독은 `OnEnable`에서 되살린다 — `subscription_lint.py`가 잡는다

UI 컴포넌트가 `OnDisable`에서 `-=`로 해지한 이벤트는 **반드시 `OnEnable`에서 다시 `+=`** 한다.
`AutoWire`는 Bootstrap에서 **한 번만** 불리므로, 거기서만 구독하면 되살아나지 못한다.

이게 왜 UI 규칙이냐면, `OpeningReplayCoordinator`가 오프닝 다시보기 중
`playUiRoot.SetActive(false/true)`로 **UI 루트를 통째로 껐다 켜기 때문**이다. 그 한 번에
UI 루트 아래 41개 컴포넌트의 `OnDisable`이 발화한다.

같은 계열로 네 번 났고 그중 하나는 P0였다:

| 파일 | 증상 |
|---|---|
| `HospitalUI` | `InsectUpdated` 해지만 있고 재구독 없음 (2026-07-19) |
| `BattleScreenUI` | `OnEnable`이 빈 메서드 → **다시보기 후 배틀 화면이 영구히 안 열림** (P0, 2026-08-03) |
| `RaidBattleUI` | 같은 형태 |
| `RegionMapUI` | `OnEnable` 자체가 없어 레이드 보스 마커 소실 |

고치는 형태는 하나다 — 구독을 `Subscribe___()`로 빼고 `AutoWire`와 `OnEnable`이 함께 부른다.
`-=` 뒤 `+=`라 중복 구독이 되지 않는다.

**`ApplyReplayBlock`의 줄 순서도 의미를 갖는다**: `BattleScreenUI`/`RaidBattleUI`의 `OnDisable`에
`if (Time.timeScale < 0.99f) Time.timeScale = 1f` 슬로우모션 복구가 있어서, `SetActive(false)`보다
`Time.timeScale = 0f`를 먼저 두면 오프닝 뒤에서 월드가 계속 돈다.

## 검증

```
python -X utf8 .claude/scripts/ui_layout_lint.py
python -X utf8 .claude/scripts/subscription_lint.py
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
