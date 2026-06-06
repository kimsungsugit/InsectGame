# 곤충게임 (Insect Game)

Unity 6 (6000.3.10f1) 기반 곤충 수집/배틀 게임. 싱글플레이어 + Firebase 클라우드 저장.
약 35,000 LOC, 115개 파일, 7개 모듈.

## 아키텍처 개요

### 부트스트랩 패턴
`PlaySceneBootstrap`가 65개 시스템을 순차 생성/연결하는 허브 역할.
각 시스템은 `AutoWire()` 메서드로 의존성을 주입받음. DI 프레임워크 없이 수동 와이어링.

### 초기화 순서
```
1. Core Entities (Player, Camera, Light, Ground)
2. Auth & Cloud (AuthManager → CloudSaveManager)
3. World Systems (GameClock → WeatherSystem → WorldStateProvider)
4. Data & Spawning (InsectDatabase → InsectSpawner)
5. Player State (Progress, Collection, Candy, Currency, Items)
6. Dex System
7. Battle System (1v1 + Raid)
8. Capture System (Controller + Minigame)
9. UI Systems (전체)
10. Training, Region, Quest, Gacha, Shop
```

### 의존성 그래프
```
AuthManager → CloudSaveManager
GameClock → WeatherSystem → WorldStateProvider → InsectSpawner
InsectDatabase → InsectSpawner, CaptureController, BattleController
PlayerInsectCollection → BattleTeamManager → BattleController, RaidController
CaptureController → CaptureMinigameController → CaptureChoiceUI
InsectBattleController → BattleScreenUI
RaidBattleController → RaidBattleUI
```

## 프로젝트 구조

- `Assets/Scripts/Core/` - 핵심 로직 (Bootstrap, Player 시스템, 싱글턴 매니저)
- `Assets/Scripts/Battle/` - 1v1 턴배틀 + 5v1 레이드
- `Assets/Scripts/Capture/` - 포획 컨트롤러 + 3단계 미니게임
- `Assets/Scripts/Data/` - ScriptableObject 데이터 모델
- `Assets/Scripts/Dex/` - 도감 (발견/포획 기록)
- `Assets/Scripts/Spawning/` - 월드 스폰 + 오브젝트 풀
- `Assets/Scripts/UI/` - 모든 UI 컨트롤러
- `Assets/Editor/` - 에디터 확장
- `Assets/Tests/EditMode/` - NUnit 유닛 테스트

## 핵심 시스템 상세

### 배틀 시스템
- **1v1 턴제**: InsectBattleController
  - 데미지 = max(1, round((basePower + Level×2) × clamp(1+atkBonus, 0.3, 3.0)))
  - 방어 보정 = clamp(attackerAtk / defenderDef, 0.5, 2.5)
  - 기본공격 = Attack × 0.7
  - 도주확률 = clamp(0.5 + levelDiff×0.05, 0.1, 0.9)
  - 스킬: 쿨다운제, 효과타입(Damage/BuffAttack/DebuffAttack)
- **레이드 (5v1)**: RaidBattleController
  - 보스 스탯: HP×5, ATK×1.5, DEF×1.3
  - 유나이트 게이지: 최대100, 공격시+12+dmg×0.15, AOE시+18
  - 유나이트 발동조건: 게이지≥100 + 생존2마리이상
  - 보스 AOE: 3턴마다, 전체 데미지×2/3

### 포획 시스템
- **포획률**: base(0.6) - rarity×0.08 - difficulty×0.4 + levelMod + itemBonus + timingBonus(0.15)
- **미니게임 3단계**: 속도×1.0→1.15→1.32, 존×1.0→0.85→0.68
- 레어도별 속도가산: +0.5/레어등급

### 스폰 시스템
- InsectSpawner: 최대20마리, 리전별 최소5마리
- WorldState(시간+날씨) 기반 후보 필터링
- 레벨 계산: pow(roll, power) 가중치 (서브에리어 power=2.0, 메인필드=3.5)
- 60m 이상 거리 자동 디스폰, 8초마다 스폰포인트 재배치

### 세이브 시스템
- 로컬: JsonUtility → 7개 JSON 파일 (persistentDataPath)
- 클라우드: Firestore REST API, 120초 자동저장
- 파일: player_progress/insects/candies/currency/items/battle_team/dex_save.json

### 스탯 시스템 (IV)
- IV 범위: 0~15 (HP/ATK/DEF 각각)
- 등급: S(≥90%) A(≥70%) B(≥50%) C(≥30%) D(<30%)
- IV롤: pow(random, rarityPower) - 레전더리일수록 낮은 IV 편향
- HP = baseHp + ivHp×2 + level×3
- ATK = baseAtk + ivAtk + level×2
- DEF = baseDef + ivDef + level

### UI 흐름
```
MainMenu → PlayScene
  ├→ 필드 탐험 (PlayerMovement + HUD)
  ├→ CaptureChoiceUI (곤충 접근 시)
  │   ├→ [E] 미니게임 포획
  │   ├→ [B] 1v1 배틀
  │   └→ [R] 레이드 (Epic/Legendary만)
  ├→ DexScreenUI (도감)
  ├→ CollectionUI (보유 곤충)
  ├→ ShopUI / GachaUI
  └→ SettingsPanel
```

## 코딩 컨벤션

- 네임스페이스: `InsectGame.{Module}` (Core/Data/Battle/Capture/Dex/Spawning/UI)
- PascalCase 클래스/메서드, camelCase private 필드
- `[SerializeField] private` + `[Header("섹션")]`
- 이벤트: `public event Action<T>`, 구독/해제는 OnEnable/OnDisable
- Singleton: `public static T Instance` (AudioManager, AuthManager 등 9개)
- null 가드 early return 패턴
- 오브젝트 풀: SimpleObjectPool.Get()/Return()

## 규칙

- `.meta` 파일 수정 금지
- **audit 자동 플로우**: 사용자 작업 완료 후 라운드 결과 보고 직후 `.claude/audit-progress.md` Uncovered ≥ 1이면 audit skill 자동 실행 (.claude/rules/audit-flow.md). `/audit` 명시 호출도 가능. 거부: "audit 안 해" 한 마디.
- `Library/`, `Logs/`, `UserSettings/` 무시
- MonoBehaviour 생성자 사용 금지 (Awake/Start 사용)
- public 필드 직접 노출 지양

## 알려진 아키텍처 이슈

- PlaySceneBootstrap 모놀리스 (4,987줄, 65개 시스템)
- BattleScreenUI/RaidBattleUI 모놀리스 (각 2,900줄+)
- 싱글턴 9개 (테스트 어려움)
- FindFirstObjectByType 72회, GameObject.Find 486회 (성능)
- 명시적 상태머신 없음 (암시적 bool 플래그)
