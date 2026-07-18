---
name: add-quest
description: 새 퀘스트를 추가하고 다지점 등록(새 목표 타입 시 5곳) 누락을 강제 검증합니다
argument-hint: "<questId> <QuestType> <목표설명>"
---

# 새 퀘스트 추가

퀘스트는 `TutorialQuestManager.Initialize()`의 `allQuests` 배열에 **C# 하드코딩**이다
(SO/JSON 아님). 진행은 PlayerPrefs에 저장(`rules/save-system.md`).

**원칙: 새 목표 타입이면 5지점을 모두 등록해야 하고, 하나라도 빠지면 그 퀘스트는
`IncrementProgress`에 영영 도달 못 해 영구 정지한다. 마무리에 `quest_lint.py`를 반드시
실행하고 FAIL 0을 확인한다.**

## Phase 1: 필요 정보

| 필드 | 설명 | 예시 |
|---|---|---|
| questId | 고유 ID (q_ 접두) | `q_visit_ruins` |
| type | QuestType (기존 15종 또는 신규) | `VisitRegion` |
| title / description / hint | 표시 문구 | "유적 탐험" / … |
| targetCount | 목표 횟수 | 1 |
| prerequisiteQuestId | 선행 퀘스트 (선형 체인) | `q_battle10` |
| 보상 | rewardCandy/rewardExp, rewardItemId+Count, rewardInsectId+DisplayName+Level | 택1+ |

**기존 QuestType 목록**: Movement, Capture, ViewCollection, LevelUp, UseItem, Battle,
Training, SetTeam, RaidBattle, DefeatGuardian, VisitRegion, VisitSubArea, OpenDex,
EquipSkill, CaptureRare. (코드가 단일 출처 — `python .claude/scripts/game_facts.py`의
quest_types로 확인)

## Phase 2: 목표 타입 판정 — 등록 범위가 갈린다

### 기존 QuestType로 표현되면 → 배열 1곳만

`TutorialQuestManager.cs`의 `allQuests` 배열에 `new TutorialQuest { … }` 항목 추가.
**단 선형 체인이라 중간 삽입 시 `prerequisiteQuestId` 재배선 주의** — 뒤 퀘스트의 선행이
새 퀘스트를 가리키게.

### 새 목표 타입이면 → 5지점 전부 (누락 = 영구 정지)

| # | 등록 지점 | 파일 | 누락 시 |
|---|---|---|---|
| 1 | `QuestType` enum 값 추가 | `TutorialQuestData.cs` | 컴파일 에러 |
| 2 | 배열 항목 + prerequisite 배선 | `TutorialQuestManager.cs` | 퀘스트 없음 |
| 3 | `Notify___()` 메서드 + **게임플레이 호출부** | `TutorialQuestManager.cs` + 트리거 시스템 | **영구 정지** |
| 4 | 이벤트 기반이면 `SubscribeEvents`/`Unsubscribe` 등록 | `TutorialQuestManager.cs` | **영구 정지** |
| 5 | 새 보상 종류면 모델 필드 + `CompleteQuest` 로직 | `TutorialQuestData.cs` + `TutorialQuestManager.cs` | 보상 미지급 |

**3번이 핵심.** 목표가 배틀/포획/UI 등 어느 게임플레이에서 달성되는지 찾아, 그 시스템 코드에서
`TutorialQuestManager.Instance.Notify___()`를 호출해야 진행된다. 메서드 정의만 하고 호출부를
안 심으면 퀘스트가 멈춘다.

**4번 — q_team 회귀.** 진행 트리거가 이벤트(TeamChanged/BattleEnded/RegionChanged 등)이면
`SubscribeEvents`에 `event += OnXxx` 등록 + 핸들러가 `NotifyAction(QuestType.X)` 호출.
실제로 SetTeam이 이 등록 누락으로 영구 정지한 사고가 있었다(`rules/quest-system.md`).

## Phase 3: 검증 — 반드시 실행

```
python -X utf8 .claude/scripts/quest_lint.py
```

6검사가 전부 PASS여야 한다:
- questId 중복 / prerequisite 무결성(끊김·순환) / 보상 곤충 ID 존재 /
  보상 아이템 ID 존재 / **QuestType↔진행 배선** / 대화 리전키 정합성

특히 **QuestType↔진행 배선**이 FAIL이면 3·4번(호출부/구독)이 누락된 것 — 퀘스트가 정지한다.
진단 문구("이벤트 미등록" / "게임플레이 호출부 없음")를 보고 해당 지점을 채운다.

보상 ID(`rewardInsectId`/`rewardItemId`)는 오타여도 런타임엔 LogWarning만 찍고 조용히
실패한다 — quest_lint가 배포 전에 잡는다.

컴파일 확인은 `/test`(PlayMode) 또는 `/build-check`.

## Phase 4: 에이전트 위임

| 작업 | 담당 |
|---|---|
| 퀘스트 목표·보상·난이도 설계 | game-designer |
| 새 QuestType의 게임플레이 호출부 삽입 | 해당 시스템 담당 (battle-dev/capture-dev/ui-dev) |
| 데이터 모델 필드 추가 | data-architect |

퀘스트는 `agent-coordination.md`상 game-designer 주담당이나, 3번(호출부)은 트리거 시스템
담당에게 위임해야 경계가 맞는다.
