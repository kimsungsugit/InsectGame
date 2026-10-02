---
name: capture-dev
description: 포획·스폰 담당 — CaptureController 분기, 3단계 미니게임, 근접·레이캐스트 트리거, InsectSpawner 스폰/디스폰/풀, 월드 상태(시간·날씨) 필터, 서브에리어 진입·이탈. 필드에서 곤충을 만나 잡기까지의 흐름이 문제일 때 PROACTIVELY 위임. 예 - 미니게임 실패인데 곤충이 안 사라진다 / 리전에 곤충이 안 뜬다 / 동굴에서 못 나온다 / 포획률이 이상하다. InsectEntity에서는 스폰·풀·월드 배치만 담당하고 BuildModel 프로시저럴 모델은 visual-dev 영역.
tools:
  - Read
  - Edit
  - Write
  - Glob
  - Grep
  - Bash
---

# 포획 시스템 에이전트

## 담당 파일
- `Assets/Scripts/Capture/CaptureController.cs` - 포획률 계산 핵심
- `Assets/Scripts/Capture/CaptureChanceCalculator.cs` - 포획 성공 확률 순수 계산
- `Assets/Scripts/Capture/CaptureMinigameController.cs` - 3단계 타이밍 미니게임
- `Assets/Scripts/Capture/CaptureMinigameProbability.cs` - 미니게임 콤보·타이밍 보너스 변환
- `Assets/Scripts/Capture/CaptureInputController.cs` - 포획 입력
- `Assets/Scripts/Capture/CaptureProximityTrigger.cs` - 근접 감지 (8m 반경)
- `Assets/Scripts/Capture/CaptureRaycastTrigger.cs` - 레이캐스트 감지
- `Assets/Scripts/Capture/CaptureTriggerModeController.cs` - 감지 모드 전환
- `Assets/Scripts/Capture/CaptureFeedbackController.cs` - 포획 연출
- `Assets/Scripts/Capture/CaptureTriggerOptionsUI.cs` - 트리거 설정 UI
- `Assets/Scripts/Spawning/InsectSpawner.cs` - 필드 개체군 (리전별 슬롯 기록 · 시간 기반 재생 · 45m 실체화/55m 회수 · 서브에리어 슬롯)
- `Assets/Scripts/Spawning/FieldSpawnRules.cs` - 필드 스폰 순수 규칙 (전역 등급표 · 등급 대체 · 레어 부스트 · 레벨 · 슬롯 수 · 재생/수명)
- `Assets/Scripts/Spawning/FieldPopulation.cs` - 슬롯 기록부 + 상태 전이 (게임플레이 퇴장 vs 거리 회수)
- `Assets/Scripts/Spawning/FleePath.cs` - 도주 방향·거리 순수 판정 (벽 관통 방지)
- `Assets/Scripts/Spawning/SpawnPoint.cs` - 리전/서브에리어 스폰 표 (풀 · 레벨 대역) — 자리는 정하지 않음
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
- `Assets/Scripts/Core/PlayerStartPlacement.cs` - 플레이어 시작 위치·방향 계산
- `Assets/Scripts/Core/WorldInteractionTypes.cs` - 월드 상호작용 종류 정의 ※프롬프트 UI는 ui-dev
- `Assets/Scripts/UI/CaptureChoiceUI.cs` - 포획/배틀/레이드 선택 허브 ※UI 레이아웃은 ui-dev
- `Assets/Scripts/NPC/NpcManager.cs` - NPC 스폰/디스폰/배치
- `Assets/Scripts/NPC/CatcherKidNpc.cs` - 잡기 아이 NPC (곤충 가로채기 로직) ※모델은 visual-dev
- `Assets/Scripts/NPC/NpcCatchRules.cs` - NPC 곤충 가로채기 규칙
- `Assets/Scripts/NPC/VillagerNpc.cs` - 마을 주민 NPC 개체/상호작용 ※모델은 visual-dev
- `Assets/Scripts/Core/IslandWorldBuilder.cs` - 나의 섬 드나들기(분리 서브에리어 진입·이탈·복귀 좌표), 밟는 땅·경계·물건 차단 콜라이더, 방목 곤충 세우기, 꾸미기 미리보기·카메라, 본 마을 나루터 ※모양은 visual-dev(`IslandTerrainBuilder`·`IslandObjectBuilder`)
- `Assets/Scripts/Core/IslandInsectWalker.cs` - 섬 방목 곤충의 배회(빈 칸 사이 이동, 건물 통과 금지)
- `Assets/Tests/EditMode/IslandIntegrationTests.cs` - 실제 PlayScene에서 섬 진입·이동·구경·퇴장(서브에리어 빌더 비개입, sticky 해제, 끼임 복구 좌표, 복귀 자리)

## 핵심 공식

### 포획률
```
core = 0.6 - rarity×0.08 - difficulty×0.4 + levelMod
chance = clamp01(max(core, rarityFloor) + activeItem + outfit + captureItem + timingBonus + comboBonus)
levelMod: 플레이어≥곤충 → +0.02/lv, 미만 → -0.03/lv
timingBonus: |timing-0.5|≤0.15이면 +0.15
comboBonus: 미니게임 성공 1회당 +0.05 (최대 3회, +0.15)
rarityFloor: Common30% / Uncommon22% / Rare14% / Epic8% / Legendary4%
```

### 미니게임 난이도
```
속도 = (1.4 + rarity×0.5) × phaseMultiplier (1.0→1.15→1.32)
존크기 = (0.35 - rarity×0.05) × phaseMultiplier (1.0→0.85→0.68)
커서 가속: 1 + |pos-0.5|×0.6 (가장자리에서 빨라짐)
```

### 필드 스폰 (FieldSpawnRules가 단일 출처)
```
등급 = 전역 표 C .60 / U .25 / R .11 / E .035 / L .005 (리전과 무관, 레어 부스트는 R+에만 ×)
  → 풀에 없는 등급은 가까운 아래 → 위로 대체 (안전망)
종 = 그 등급의 (리전 풀 ∩ 시간·날씨 후보) 안에서 spawnWeight 가중
레벨(메인) = 리전 대역 [requiredLevel, +GetRegionLevelRange] 안 pow(random, 1.5), 등급 보정 없음
레벨(서브에리어) = min~max 균등
슬롯 = 설 수 있는 땅 / 550㎡ (8~40) → BlightPolicy.MaxActiveFor
재생 60~120s (서브에리어 45s) · 수명 4~7분(플레이어 25m 밖에서만 교체) · 새 개체는 플레이어 20m 밖
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
- 기록(슬롯)이 개체다 — 멀어지면 몸만 풀로 돌리고(Recall) 기록은 남긴다. 리전 이동으로 리롤하지 않는다
- 사라지는 방식 둘: 게임플레이 퇴장(Despawn 콜백 → 재생 지연) vs 스포너 회수(Recall → 개체 유지)
- 포획 아이템은 speedMult/zoneMult/timeMult/captureBonus 4가지 효과
