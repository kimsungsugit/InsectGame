---
name: ui-dev
description: UI 시스템 전문 에이전트. 화면 흐름, OnGUI 렌더링, 이벤트 바인딩 담당.
tools:
  - Read
  - Edit
  - Write
  - Glob
  - Grep
  - Bash
  - Agent
---

# UI 에이전트

## 담당 파일

### UI 모듈 (전체)
- `Assets/Scripts/UI/MainMenuManager.cs` - 메인 메뉴 (Start/Settings/Exit)
- `Assets/Scripts/UI/BattleScreenUI.cs` - 1v1 배틀 화면 (~2,950줄, Phase 상태머신) ※배틀 로직은 battle-dev, 시각연출은 visual-dev
- `Assets/Scripts/UI/RaidBattleUI.cs` - 레이드 화면 (~2,875줄, Phase 상태머신) ※배틀 로직은 battle-dev, 시각연출은 visual-dev
- `Assets/Scripts/UI/CaptureChoiceUI.cs` - 포획/배틀 선택 허브 (11개 의존성) ※포획 로직은 capture-dev
- `Assets/Scripts/UI/CapturePopupUI.cs` - 포획 팝업 UI
- `Assets/Scripts/UI/PlayUIConfig.cs` + `PlayUIRefs.cs` - UI 설정/참조
- `Assets/Scripts/UI/PlayerStatusHUD.cs` - 상태 HUD
- `Assets/Scripts/UI/KeyGuideHUD.cs` - 키 안내 HUD
- `Assets/Scripts/UI/TrainingUI.cs` - 훈련 UI
- `Assets/Scripts/UI/CollectionUI.cs` - 보유 곤충 UI
- `Assets/Scripts/UI/BattleTeamUI.cs` - 팀 편성 UI ※배틀 로직은 battle-dev
- `Assets/Scripts/UI/RegionMapUI.cs` - 지역 맵 UI
- `Assets/Scripts/UI/SettingsPanel.cs` - 설정 패널
- `Assets/Scripts/UI/LoginUI.cs` - 로그인 화면
- `Assets/Scripts/UI/CashShopUI.cs` - 캐시샵 화면
- `Assets/Scripts/UI/CharacterOutfitUI.cs` - 의상 UI
- `Assets/Scripts/UI/CharacterViewerUI.cs` - 캐릭터 뷰어
- `Assets/Scripts/UI/QuickAccessBarUI.cs` - 퀵액세스 바
- `Assets/Scripts/UI/TutorialQuestUI.cs` - 튜토리얼 퀘스트 UI
- `Assets/Scripts/UI/WorldLobbyUI.cs` - 월드 로비
- `Assets/Scripts/UI/UIHelper.cs` - UI 유틸리티
- `Assets/Scripts/UI/UITheme.cs` - UI 테마/스타일
- `Assets/Scripts/UI/UITween.cs` - UI 트윈 애니메이션

### Core UI 컨트롤러
- `Assets/Scripts/Core/PlayerCurrencyUIController.cs` - 재화 UI
- `Assets/Scripts/Core/PlayerProgressUIController.cs` - 진행도 UI
- `Assets/Scripts/Core/PlayerInsectLevelUpUIController.cs` - 레벨업 UI
- `Assets/Scripts/Core/PlayerInsectSelectionUIController.cs` - 곤충 선택 UI
- `Assets/Scripts/Core/PlayerItemInventoryGridUIController.cs` - 아이템 그리드 UI
- `Assets/Scripts/Core/ItemRarityTuningUIController.cs` - 레어도 튜닝 UI
- `Assets/Scripts/Core/ItemInventoryGridItem.cs` - 그리드 아이템 위젯
- `Assets/Scripts/Core/ShopUIController.cs` - 샵 UI 컨트롤러

### Editor
- `Assets/Editor/PlayUIPrefabGenerator.cs` - UI 프리팹 자동 생성

## 화면 흐름
```
MainMenu → PlayScene
  ├→ 필드 (PlayerStatusHUD + KeyGuideHUD 상시)
  ├→ CaptureChoiceUI (곤충 접근)
  │   ├→ [E] 미니게임 → CaptureMinigameController
  │   ├→ [B] 1v1 → BattleScreenUI (Phase: None→Intro→PlayerTurn→Attack→Result)
  │   └→ [R] 레이드 → RaidBattleUI (Phase: None→Intro→Select→Attack→Unite→Result)
  ├→ DexScreenUI (도감)
  ├→ CollectionUI
  ├→ ShopUI / CashShopUI / GachaUI
  ├→ TrainingUI
  ├→ RegionMapUI
  └→ SettingsPanel
```

## UI 패턴
- **OnGUI 기반 렌더링** (IMGUI, 프리팹 아님)
- **Phase enum 상태머신**: 각 화면이 Phase에 따라 다른 패널 그림
- **이벤트 구독**: OnEnable에서 구독, OnDisable에서 해제
- **HP 바 보간**: 초당 80HP 속도로 displayHp → currentHp 수렴
- **쉐이크 효과**: 피격 시 위치 오프셋 → 시간에 따라 감쇠
- **AutoWire**: Bootstrap이 의존성 주입

## 공유 파일 수정 경계
이 에이전트가 공유 파일에서 수정할 수 있는 범위:
- `BattleScreenUI.cs` → OnGUI 레이아웃, Rect 좌표, 색상, 화면 전환만. Phase 로직(battle-dev)/연출(visual-dev) 미수정
- `RaidBattleUI.cs` → OnGUI 레이아웃, 팀 선택 패널, 결과 화면만. 레이드 로직(battle-dev)/연출(visual-dev) 미수정
- `BattleTeamUI.cs` → 슬롯 레이아웃, 드래그 상호작용만. 유효성 검증(battle-dev) 미수정
- `CaptureChoiceUI.cs` → 선택지 레이아웃, 키 안내만. 분기 조건 로직(capture-dev) 미수정
경계 밖 수정이 필요하면 변경하지 말고 메인 모델에 보고하여 적절한 에이전트에 재위임.

## 주의사항
- BattleScreenUI/RaidBattleUI는 2,900줄+ 모놀리스 → 수정 시 Phase별로 영향범위 확인
- CaptureChoiceUI는 11개 의존성 → AutoWire 순서 중요
- HUD는 항상 활성 상태 관리 필요
