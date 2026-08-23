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
`rules/ui-layout.md`(배치는 `UISafeLayout`, 표면·색은 `UISurface`+`UITheme` 토큰 경유 강제),
`rules/save-system.md`(세이브 필드 추가),
`rules/scriptable-objects.md`(SO 생성),
`rules/quest-system.md`(퀘스트 추가 시 다지점 등록 — 빠뜨리면 영구 정지),
`rules/testing.md`(테스트 필수 기준),
`rules/agent-coordination.md`(공유 파일 수정 경계).

## 검사기 — 어느 스크립트가 어느 규칙을 강제하나

전부 `.claude/scripts/`에 있고 `ci_check.py`가 한 번에 돌린다(세션 밖 편집도 CI가 잡는다).
**규칙의 단일 출처는 문서이고 스크립트는 그걸 강제할 뿐이다** — 규칙을 바꾸려면 문서를 고친다.

| 검사기 | 강제하는 규칙 | 단일 출처 |
|---|---|---|
| `quest_lint.py` | questId 중복·prerequisite 무결성·QuestType↔진행 배선 등 10검사 | `rules/quest-system.md` |
| `ui_layout_lint.py` | 패널 y·height 직접 계산 금지 (`UISafeLayout` 경유) | `rules/ui-layout.md` |
| `subscription_lint.py` | `OnDisable`에서 해지한 구독을 `OnEnable`에서 되살릴 것 | `rules/ui-layout.md` |
| `data_lint.py` | 곤충·아이템·리전 데이터 정합(ID 유일성, 참조 무결, 풀 배정) | 코드(`InsectDatabase` 등)와 스크립트 자신 |
| `story_lint.py` | 스토리 비트 트리거·보상·리전키 정합 | 코드(`StoryBeat`)와 스크립트 자신 |
| `dex_grant_lint.py` | 곤충을 지급하면 도감에도 올릴 것(`AddCapturedInsect`↔`RegisterCapture`) | 코드(`DexController`)와 스크립트 자신 |
| `blight_lint.py` | 명부회 오염 거점 — 보스·귀환종·비트·재도전 예외·스폰 하한·퀘스트 달성 가능성 19검사 | 코드(`RegionData` 거점 필드)와 스크립트 자신 |
| `singleton_lint.py` | 싱글턴이 `OnDestroy`에서 `Instance`를 비울 것 | 코드(`*Manager.cs`)와 스크립트 자신 |
| `sync_codex.py` | `.claude` ↔ `.codex` 미러 동기 | 스크립트 자신 |
| `verify_coverage.py` | 모든 `.cs`에 담당 에이전트가 있을 것 | `rules/agent-coordination.md` + `agents/*.md` |

`data_lint`·`story_lint`·`dex_grant_lint`·`singleton_lint`·`blight_lint`는 대응 규칙 문서 없이 코드를 직접 읽는다 — 데이터 스키마와
호출 배선이 곧 규칙이라 문서 사본을 두면 썩기 때문이다. 나머지는 문서를 파싱하거나 문서에 적힌 규칙을 구현한다.

`singleton_lint`도 같은 계열이다. 매니저 9종이 `public static T Instance`를 드는데 전부
`World/` 아래 **자식**으로 생성돼 씬 스코프이고, 로그아웃·계정삭제가 씬을 재로드하면 실제로
파기된다. 그때 static을 안 비우면 `Instance != null`(Unity의 파괴 검사)과 `Instance?.`(진짜 null
검사)가 **서로 다른 답**을 내고 후자는 파기된 객체로 호출이 들어간다. 저장소에 `Instance?.`가
19곳 있다. 2026-08-23에 9종 중 8종이 안 비우고 있었다.

`blight_lint`도 그 계열이다. 오염 거점은 실패가 **전부 무증상**이다 — 예외도 경고도 안 나고
거점이 그냥 영원히 안 무너지거나, 정화해도 아무것도 안 돌아온다. 급소 둘: ①`CanBossDuel`의
오염 예외가 사라지면 **이미 하수를 이긴 세이브(2막 진행자 대부분)에는 기능이 아예 안 보인다**
(이긴 보스에겐 재도전이 막힌다). ②`BlightPolicy.MinActive`가 0으로 내려가면 그 리전의
포획·전투 비트가 전부 도달 불가가 되어 **캠페인이 영구 정지한다**. 둘 다 소스 grep으로 고정했다.

`dex_grant_lint`는 **증상이 조용한** 결함을 겨냥한다. 곤충 지급 경로 6곳(포획·전투·레이드·가챠·
튜토리얼 보상·스토리 보상)이 각자 도감 등록을 따로 불러야 하는데, 빠뜨려도 예외도 경고도 없고
곤충은 멀쩡히 손에 들어온다. 2026-08-17에 `TutorialQuestManager`(첫 파트너가 영원히 미발견)와
`StoryDirector` **두 곳이 동시에** 빠져 있었다.

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
