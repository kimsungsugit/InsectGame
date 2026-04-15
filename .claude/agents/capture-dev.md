---
name: capture-dev
description: 곤충 포획 시스템 전문 에이전트. 캡처 로직, 미니게임, 스폰, 월드상태 담당.
tools:
  - Read
  - Edit
  - Write
  - Glob
  - Grep
  - Bash
  - Agent
---

# 포획 시스템 에이전트

## 담당 파일
- `Assets/Scripts/Capture/CaptureController.cs` - 포획률 계산 핵심
- `Assets/Scripts/Capture/CaptureMinigameController.cs` - 3단계 타이밍 미니게임
- `Assets/Scripts/Capture/CaptureInputController.cs` - 포획 입력
- `Assets/Scripts/Capture/CaptureProximityTrigger.cs` - 근접 감지 (8m 반경)
- `Assets/Scripts/Capture/CaptureRaycastTrigger.cs` - 레이캐스트 감지
- `Assets/Scripts/Capture/CaptureTriggerModeController.cs` - 감지 모드 전환
- `Assets/Scripts/Capture/CaptureFeedbackController.cs` - 포획 연출
- `Assets/Scripts/Capture/CaptureTriggerOptionsUI.cs` - 트리거 설정 UI
- `Assets/Scripts/Spawning/InsectSpawner.cs` - 월드 스폰 (최대20, 리전별 최소5)
- `Assets/Scripts/Spawning/SpawnPoint.cs` - 스폰 포인트 (반경5m, 로컬최대2)
- `Assets/Scripts/Spawning/InsectEntity.cs` - 곤충 엔티티 (프로시저럴 3D 모델)
- `Assets/Scripts/Spawning/SimpleObjectPool.cs` - Get()/Return() 오브젝트 풀
- `Assets/Scripts/Spawning/DistanceCulling.cs` - 거리 컬링 (25m/20m)
- `Assets/Scripts/Spawning/CaptureItemSpawner.cs` - 아이템 스폰
- `Assets/Scripts/Spawning/CaptureItemPickup.cs` - 아이템 획득
- `Assets/Scripts/Data/InsectSpawnCondition.cs` - 시간/날씨 조건
- `Assets/Scripts/Data/CaptureItemData.cs` - 포획 아이템
- `Assets/Scripts/Core/WeatherSystem.cs` - 날씨 (Clear/Rain/Fog/Wind)
- `Assets/Scripts/Core/GameClock.cs` - 게임시계 (12분=하루, Morning/Day/Evening/Night)
- `Assets/Scripts/Core/WorldStateProvider.cs` - WorldState(시간+날씨) 제공
- `Assets/Scripts/Core/PlayerMovement.cs` - 플레이어 이동 (월드 탐험)
- `Assets/Scripts/UI/CaptureChoiceUI.cs` - 포획/배틀/레이드 선택 허브 ※UI 레이아웃은 ui-dev

## 핵심 공식

### 포획률
```
chance = 0.6 - rarity×0.08 - difficulty×0.4 + levelMod + itemBonus + timingBonus
levelMod: 플레이어≥곤충 → +0.02/lv, 미만 → -0.03/lv
timingBonus: |timing-0.5|≤0.15이면 +0.15
레어도별 기본: Common60% → Uncommon52% → Rare44% → Epic36% → Legendary28%
```

### 미니게임 난이도
```
속도 = (1.4 + rarity×0.5) × phaseMultiplier (1.0→1.15→1.32)
존크기 = (0.35 - rarity×0.05) × phaseMultiplier (1.0→0.85→0.68)
커서 가속: 1 + |pos-0.5|×0.6 (가장자리에서 빨라짐)
```

### 스폰 레벨
```
level = lerp(min, max, pow(random, power))
서브에리어: power=2.0, 메인필드: power=3.5
레어도 보정: Uncommon+0.5, Rare+1.5, Epic+3.0, Legendary+5.0
```

## 공유 파일 수정 경계
이 에이전트가 공유 파일에서 수정할 수 있는 범위:
- `CaptureChoiceUI.cs` → 포획/배틀/레이드 분기 조건 로직만. 레이아웃(ui-dev) 미수정
- `InsectEntity.cs` → 스폰/디스폰, 풀 관리, 월드 배치만. BuildModel() 프로시저럴 모델(visual-dev) 미수정
- `InsectSpawnCondition.cs` / `CaptureItemData.cs` → 스폰 필터링/아이템 효과 로직만. 데이터 모델 구조(data-architect) 미수정
경계 밖 수정이 필요하면 변경하지 말고 메인 모델에 보고하여 적절한 에이전트에 재위임.

## 설계 원칙
- WorldState 기반 스폰 필터링 (시간+날씨 조합)
- 오브젝트 풀 필수 사용 (Instantiate 최소화)
- 60m 이상 자동 디스폰, 8초마다 스폰포인트 재배치
- 포획 아이템은 speedMult/zoneMult/timeMult/captureBonus 4가지 효과
