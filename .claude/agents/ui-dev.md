---
name: ui-dev
description: 2D IMGUI(OnGUI) 화면 담당 — 화면 흐름과 전환, Rect 좌표와 레이아웃, GUIStyle 캐싱, 키 안내, 이벤트 구독 바인딩, IModalUI 스택. 무엇을 어디에 그리는가가 문제일 때 PROACTIVELY 위임. 예 - 배틀 화면 버튼이 겹친다 / OnGUI에서 매 프레임 new GUIStyle이 생긴다 / ESC로 패널이 안 닫힌다 / 슬롯 배치가 틀어졌다. 3D 메시·머티리얼·파티클·색상값은 visual-dev 영역이므로 손대지 않는다.
tools:
  - Read
  - Edit
  - Write
  - Glob
  - Grep
  - Bash
---

# UI 에이전트

## 담당 파일

### UI 모듈 (전체)
- `Assets/Scripts/UI/MainMenuManager.cs` - 메인 메뉴 (Start/Settings/Exit)
- `Assets/Scripts/UI/BattleScreenUI.cs` - 1v1 배틀 화면 (모놀리스, Phase 상태머신) ※배틀 로직은 battle-dev, 시각연출은 visual-dev
- `Assets/Scripts/UI/RaidBattleUI.cs` - 레이드 화면 상태기계 (Phase 전이·입력·컨트롤러 이벤트) ※배틀 로직은 battle-dev, 시각연출은 visual-dev
- `Assets/Scripts/UI/RaidBattleUI.Draw.cs` - 위의 렌더 절반 partial (GUIStyle 캐시 + Draw* 전부) ※AOE·유나이트 이펙트는 visual-dev
- `Assets/Scripts/UI/CaptureChoiceUI.cs` - 포획/배틀 선택 허브 (11개 의존성) ※포획 로직은 capture-dev
- `Assets/Scripts/UI/CapturePopupUI.cs` - 포획 팝업 UI
- `Assets/Scripts/UI/PlayUIConfig.cs` + `PlayUIRefs.cs` - UI 설정/참조
- `Assets/Scripts/UI/PlayerStatusHUD.cs` - 상태 HUD
- `Assets/Scripts/UI/KeyGuideHUD.cs` - 키 안내 HUD
- `Assets/Scripts/UI/TrainingUI.cs` - 훈련 UI
- `Assets/Scripts/UI/CollectionUI.cs` - 보유 곤충 UI
- `Assets/Scripts/UI/BattleTeamUI.cs` - 팀 편성 UI ※배틀 로직은 battle-dev
- `Assets/Scripts/UI/HospitalUI.cs` - 병원 치료·아이템 대상 선택 UI
- `Assets/Scripts/UI/InventoryUI.cs` - 가방(보유 아이템 목록·사용) UI
- `Assets/Scripts/UI/RegionMapUI.cs` - 지역 맵 UI
- `Assets/Scripts/UI/SettingsPanel.cs` - 설정 패널
- `Assets/Scripts/UI/AccountSettingsUI.cs` - 계정/오프닝 다시 보기 패널
- `Assets/Scripts/UI/LoginUI.cs` - 로그인 화면
- `Assets/Scripts/UI/CashShopUI.cs` - 캐시샵 화면
- `Assets/Scripts/UI/CharacterOutfitUI.cs` - 의상 UI
- `Assets/Scripts/UI/QuickAccessBarUI.cs` - 퀵액세스 바
- `Assets/Scripts/UI/SocialPvpUI.cs` - 소셜 PvP 로비·스킬 선택 UI
- `Assets/Scripts/UI/TutorialQuestUI.cs` - 튜토리얼 퀘스트 UI
- `Assets/Scripts/UI/GuidedTutorialController.cs` - 첫 몇 단계 강제 가이드 오버레이(코치 배너+시작 프리즈) ※퀘스트 이벤트는 TutorialQuestManager(Core)
- `Assets/Scripts/UI/WorldLobbyUI.cs` - 월드 로비
- `Assets/Scripts/UI/CharacterPortraitRenderer.cs` - 통합 캐릭터 포트레이트 렌더러
- `Assets/Scripts/UI/InsectVisual.cs` - 곤충 그림 단일 진입점(3D 썸네일 or 2D 폴백 판단) ※렌더는 InsectModelPreviewRenderer(visual-dev)
- `Assets/Scripts/UI/UIShapes.cs` - 2D 폴백 도형 원시요소(원·캡슐·실루엣) ※색은 UITheme 토큰
- `Assets/Scripts/UI/UIHelper.cs` - UI 유틸리티
- `Assets/Scripts/UI/UIScale.cs` - 1920×1080 기준 가상 좌표계 / GUI.matrix 자동 스케일링
- `Assets/Scripts/UI/UISafeLayout.cs` - 세이프에어리어 + 세로 마진 배치 하네스 (패널 Rect의 단일 출처, `rules/ui-layout.md`)
- `Assets/Scripts/UI/UISurface.cs` - 둥근 카드·그림자·호버 공용 서피스 (전 화면 표면 처리의 단일 출처). 색은 UITheme 토큰에서만 받는다
- `Assets/Scripts/UI/QuestListLayout.cs` - 퀘스트 목록 아코디언 가변 높이 순수 계산
- `Assets/Scripts/UI/UIDirectScroll.cs` - IMGUI 목록 휠·터치 드래그 직접 스크롤
- `Assets/Scripts/UI/UITheme.cs` - UI 테마/스타일
- `Assets/Scripts/UI/UITween.cs` - UI 트윈 애니메이션
- `Assets/Scripts/UI/InsectBrowseSort.cs` - 보유 곤충 정렬 순수부(등급/레벨/CP/최근)
- `Assets/Scripts/UI/StoryJournalUI.cs` - 스토리 저널 챕터 탭·다시 읽기 렌더
- `Assets/Scripts/UI/AccountLinkUI.cs` - 게스트→정식 계정 연동 화면
- `Assets/Scripts/UI/SaveConflictUI.cs` - 로컬/클라우드 세이브 충돌 선택 모달 ※세이브 구조는 data-architect
- `Assets/Scripts/UI/WorldFieldMultiplayerUI.cs` - 필드 멀티 초대·친구 목록 (픽셀 좌표계 — `UISafeLayout.Px` 사용)
- `Assets/Scripts/UI/WorldInteractionController.cs` - 월드 오브젝트 상호작용 프롬프트
- `Assets/Scripts/UI/MinimapUI.cs` - 미니맵 HUD
- `Assets/Scripts/UI/SafeArea.cs` - `Screen.safeArea` 픽셀 인셋 (프레임당 1회 캐시). `UISafeLayout`의 입력원
- `Assets/Scripts/UI/SafeAreaPanel.cs` - uGUI RectTransform 세이프에어리어 적용 컴포넌트
- `Assets/Scripts/UI/VirtualJoystickUI.cs` - 모바일 가상 조이스틱 ※`ui_layout_lint` 면제 대상(조작 영역이라 마진을 주면 좁아진다)
- `Assets/Scripts/UI/PlayerHintOverlay.cs` - 필드 안내 문구(이동 잠금·리전 레벨 부족) ※상태는 PlayerMovement가 소유, 여기선 그리기만
- `Assets/Scripts/UI/BattleEffectTextOverlay.cs` - 전투 문구 오버레이 ※목록은 BattleArenaController가 소유
- `Assets/Scripts/UI/FieldHudInput.cs` - 필드 HUD 터치 좌표 변환 ※`ui_layout_lint` 면제 대상(배치가 아니라 입력)
- `Assets/Scripts/Dex/DexBrowseLayout.cs` - 도감 순환 선택·그리드 열 수/높이 순수 계산 (도감 탭과 보유 탭이 공유)

### 오프닝 UI
- `Assets/Scripts/Opening/OpeningSceneController.cs` - 오프닝 타임라인 입력·IMGUI 렌더링·화면 전환

### Core UI 컨트롤러
- `Assets/Scripts/Core/PlayerCurrencyUIController.cs` - 재화 UI
- `Assets/Scripts/Core/PlayerProgressUIController.cs` - 진행도 UI
- `Assets/Scripts/Core/PlayerInsectLevelUpUIController.cs` - 레벨업 UI
- `Assets/Scripts/Core/PlayerInsectSelectionUIController.cs` - 곤충 선택 UI
- `Assets/Scripts/Core/PlayerItemInventoryGridUIController.cs` - 아이템 그리드 UI
- `Assets/Scripts/Core/ItemRarityTuningUIController.cs` - 레어도 튜닝 UI
- `Assets/Scripts/Core/ItemInventoryGridItem.cs` - 그리드 아이템 위젯
- `Assets/Scripts/Core/ShopUIController.cs` - 샵 UI 컨트롤러
- `Assets/Scripts/UI/NpcDialogueUI.cs` - NPC 대화 모달 (레이아웃/렌더) ※대사 내용은 game-designer
- `Assets/Scripts/Core/QuestRewardFormatter.cs` - 퀘스트 보상 표시 문자열 조립 (배너·목록 공용) ※보상 수치 자체는 game-designer

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
