# 곤충게임 (Insect Game)

Unity 6 (6000.3.10f1) 기반 곤충 수집/배틀 게임. 싱글플레이어 + Firebase 클라우드 저장.

> 이 문서는 **지도**다 — 무엇이 어디에 있고 어떻게 이어지는지만 담는다.
> 수치·공식은 **코드**가, 컨벤션·절차는 `.claude/rules/*.md`가, 편집 강제는
> `.claude/settings.json`이 단일 출처다. 여기에 사본을 두지 않는다.

## 아키텍처 개요

### 부트스트랩 패턴
`PlaySceneBootstrap`가 전체 시스템을 순차 생성/연결하는 허브 역할.
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

## 핵심 시스템 진입점

**수치·공식의 단일 출처는 코드다** (`GameConstants`, 각 Controller).
빠른 참조 기준점은 `.claude/rules/balance.md`에 있으나, **코드와 불일치 시 코드가 옳다**.

| 시스템 | 진입점 | 비고 |
|---|---|---|
| 배틀 1v1 | `InsectBattleController` | 턴제, 스킬 쿨다운, 도주 판정 |
| 레이드 5v1 | `RaidBattleController` | 보스 스탯 배율, 유나이트 게이지, 주기적 AOE |
| 포획 | `CaptureController` → `CaptureMinigameController` | 3단계 난이도 미니게임 |
| 스폰 | `InsectSpawner` | WorldState(시간+날씨) 후보 필터링, 거리 컬링 |
| 세이브 | `PlayerProgressSaveService`, `CloudSaveManager` | 로컬 7개 JSON + Firestore. 규칙은 `rules/save-system.md` |
| 스탯/IV | `PlayerInsectData` | IV 0~15(HP/ATK/DEF), 등급 S~D |

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

`.claude/rules/unity-csharp.md`가 단일 출처다(규칙 문서는 자동 로드됨).
네임스페이스 `InsectGame.{Module}`, 네이밍, `[SerializeField] private`,
AutoWire·이벤트·오브젝트 풀 패턴, 금지 사항이 전부 거기 있다.

관련: `rules/architecture.md`(의존성 방향·Bootstrap 등록),
`rules/save-system.md`(세이브 필드 추가),
`rules/scriptable-objects.md`(SO 생성),
`rules/testing.md`(테스트 필수 기준),
`rules/agent-coordination.md`(공유 파일 수정 경계).

## 규칙

- **audit 자동 플로우**: 사용자 작업 완료 후 라운드 결과 보고 직후 `.claude/audit-progress.md` Uncovered ≥ 1이면 audit skill 자동 실행 (.claude/rules/audit-flow.md). `/audit` 명시 호출도 가능. 거부: "audit 안 해" 한 마디.
- `Library/`, `Logs/`, `UserSettings/` 무시
- `.meta` 등 편집 금지 대상은 `.claude/settings.json`의 `deny`가 강제한다 — 문서가 아니라 설정이 단일 출처.

## 알려진 아키텍처 이슈

- **모놀리스 3종**: `PlaySceneBootstrap`, `BattleScreenUI`, `RaidBattleUI`.
  줄 수는 적지 않는다 — 편집 시 `warn_monolith` 훅이 실제 값을 세어 보고한다.
- 싱글턴 다수 (테스트 어려움). 신규 추가는 architect 에이전트 상담.
- `FindFirstObjectByType` 다용 — AutoWire 캐싱으로 대체 권장.
- 명시적 상태머신 없음 (암시적 bool 플래그).
