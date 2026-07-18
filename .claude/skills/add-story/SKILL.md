---
name: add-story
description: 새 스토리 비트를 추가하고 트리거 배선(신규 type 시 StoryDirector 2곳) 누락을 강제 검증합니다
argument-hint: "<beatId> <trigger.type> <설명>"
---

# 새 스토리 비트 추가

스토리 비트는 `Assets/Resources/Story.json`에 **JSON**으로 정의된다(설계:
`Docs/StorySystemDesign.md`). 진행은 `story_progress.json` + 클라우드 동기.
기존 시스템(퀘스트/대화/리전)을 관찰(구독)만 하는 가산 레이어다.

**원칙: 신규 trigger.type이면 StoryDirector 2곳(switch case + 이벤트 발화 지점)을 등록해야
하고, 빠지면 그 타입 비트가 영영 발화하지 않는다. 마무리에 `story_lint.py`를 반드시 실행하고
FAIL 0을 확인한다.**

## Phase 1: 비트 정의 (Story.json)

`beats[]` 배열에 항목 추가. 필드(전체는 `Docs/StorySystemDesign.md`):

```json
{
  "beatId": "ch1_xxx", "chapterId": "ch1", "order": N,
  "prerequisiteBeatId": "앞 비트 또는 \"\"",
  "requiredRegionId": "리전 ID 또는 \"\" (무param 트리거 리전 잠금)",
  "trigger": { "type": "RegionEnter", "param": "pond" },
  "speakerNpcId": "village_elder",
  "lines": [ { "speaker": "이름", "text": "대사" } ],
  "choices": [],
  "onComplete": { "rewardCandy": 5, "rewardExp": 0, "rewardItemId": "",
    "rewardInsectId": "", "unlockQuestId": "" },
  "oneShot": true
}
```

**trigger.type (기존 8종)**: RegionEnter, QuestComplete, LevelReach, CaptureInsect,
BattleWin, SubAreaEnter, Immediate, NpcTalk. `param`은 그 타입의 대상 ID:
- RegionEnter/SubAreaEnter → 리전 ID (meadow/pond/…)
- QuestComplete → questId (q_approach 등)
- CaptureInsect → 곤충 ID (비우면 아무 포획)
- LevelReach → 정수 레벨
- NpcTalk → 스토리 NPC ID (village_elder/catcher_rival/ruins_scholar). WorldInteractionController가
  스토리 NPC 대화 시 `StoryDirector.OnNpcTalked`로 발화(구독 아닌 직접 진입점)

## Phase 2: 트리거 판정 — 배선 범위가 갈린다

### 기존 trigger.type이면 → Story.json 1곳만

7종 중 하나면 JSON에 비트만 추가. StoryDirector가 이미 그 타입을 처리한다.
**단 prerequisiteBeatId / choices.nextBeatId 배선 주의** (체인·분기 끊김).

### 새 trigger.type이면 → StoryDirector 2곳 (누락 = 영구 미발화)

| # | 등록 지점 | 파일 | 누락 시 |
|---|---|---|---|
| 1 | `const string TriggerX = "Y"` + `EvaluateTriggers` switch에 `case TriggerX:` | `StoryDirector.cs` | 비트 미발화 |
| 2 | 그 트리거를 발화할 **이벤트 구독 + 핸들러**가 `EvaluateTriggers(TriggerX, param)` 호출 | `StoryDirector.cs` `SubscribeEvents`/핸들러 | 비트 미발화 |

**이게 스토리 하네스의 급소다** — 퀘스트의 q_team 회귀(Notify 호출부/이벤트 구독 누락)와
정확히 같은 구조. trigger.type을 JSON에 넣고 StoryDirector 배선을 빠뜨리면, 그 타입을 쓰는
비트가 트리거를 못 받아 영영 안 열린다. 새 이벤트 소스가 필요하면 그 이벤트를 발화하는
시스템(RegionManager/BattleController 등)에서 이벤트를 노출하고 StoryDirector가 구독해야 한다.

## 발화 함정 — 기존 trigger.type에서도 밟는다 (실측)

Phase 2가 "기존 7종이면 JSON 1곳만"이라 해도, **어느 트리거를 고르고 대사를 어떻게 쓰는지**가
발화 정확성을 가른다. 아래 넷은 story_lint가 못 잡는 런타임 함정이다(전부 코드 실측).

### 1. 스파인은 재발화 트리거로 — QuestComplete-게이트는 기존 유저를 정지시킨다

`QuestComplete`는 그 퀘스트가 **완료되는 순간 딱 한 번** 발화한다(`StoryDirector.OnQuestCompleted`).
**이미 그 퀘스트를 끝낸 세이브에는 영영 다시 안 온다.** 따라서 리전 진행의 **스파인(주 서사
체인)을 tutorial `QuestComplete`에 걸면, 튜토리얼을 마친 기존 유저는 그 뒤 캠페인 전체를 못
본다** — 스파인이 그 지점에서 영구 정지한다.

- **스파인**: `RegionEnter`/`SubAreaEnter`/`BattleWin`/`CaptureInsect`/`LevelReach` 같은
  **재발화 트리거**로 걸고, prereq 체인의 뿌리는 `Immediate` 비트(전원 발화)에 둔다.
- **`QuestComplete`**: 놓쳐도 캠페인이 안 끊기는 **선택 플레이버 비트**에만. 신규 유저용
  튜토리얼 통합 연출엔 좋지만, 뒤 비트가 이걸 prereq로 삼으면 안 된다.

(실제로 겪음: pond 스파인 오프너가 `q_guardian1` 완료에 걸려, 가디언을 이미 잡은 유저는
ch1_intro 다음이 통째로 잠겼다. 게이트를 `ch1_intro`(Immediate)로 재배선해 해결.)

### 2. `order` 필드는 발화 순서에 안 쓰인다 — 순서는 prereq로만

`StoryService.AllBeats()`는 `Dictionary.Values`를 반환한다(삽입순 보장 없음). 엔진은 매
이벤트에서 **적격(미열람·prereq충족·param일치) 비트 중 처음 하나만** 발화한다. `order`는 순수
문서용 메타라 **발화 순서를 결정하지 않는다.** 순서가 중요하면 `prerequisiteBeatId`로 엮어라.

### 3. 무param 트리거(BattleWin / 빈 CaptureInsect)는 리전을 못 실어 늦발화 얼룩 — `requiredRegionId`로 잠가라

`QuestComplete`/`RegionEnter`/`SubAreaEnter`는 param으로 특정 대상만 무니 충돌이 없다. 그러나
`BattleWin`(param 강제 공백)과 **param 빈 `CaptureInsect`**(아무 포획)는 위치 무관 발화라, prereq가
누적형이면 **초원에서 안 잡고 습지로 간 유저에게 초원 비트가 습지에서 늦발화**한다(정지는 아니나
문맥 어긋남).

**해법(엔진 지원)**: 리전 플레이버 무param 비트엔 **`requiredRegionId`**를 채운다 — StoryDirector
`RegionGateSatisfied`가 현재 리전과 대조해 그 리전에서만 발화. 비우면 글로벌(위치 무관)이라 얼룩에
취약 → **story_lint 검사 7이 WARN**. 스킵 시 조용히 미발화하므로 **leaf 비트여야 안전**(어떤 비트의
prereq도 아닐 것).

**남는 순서 주의**: 같은 리전·같은 트리거의 무param 비트가 둘 이상이면 그들 사이 순서는 여전히
Dictionary 비결정 — 그럴 땐 prereq로 한 번에 하나만 적격이 되게 엮어라.

### 4. `CaptureInsect`는 포획뿐 아니라 레벨업에도 발화 — 대사를 범용으로

`CaptureInsect`의 소스 `PlayerInsectCollection.InsectUpdated`는 **포획 + 캔디 레벨업** 양쪽에서
발화한다(`InsectUpdated?.Invoke` 3곳: 포획 1 + 레벨업/수정 2). 따라서 param 빈 `CaptureInsect`
비트가 **레벨업 순간에 열릴 수 있다.** 대사를 "방금 그 포획은…"처럼 특정 행동에 못박지 말고
"네 수집이 늘 때마다…"처럼 **범용으로** 써라.

## Phase 3: 검증 — 반드시 실행

```
python -X utf8 .claude/scripts/story_lint.py
```

7검사가 전부 PASS여야 한다(FAIL 0, WARN 0):
- beatId 중복 / prerequisite 무결성(끊김·순환) / 트리거 param 대상 존재 /
  분기 도달성(choices.nextBeatId) / onComplete 보상·unlock ID 존재 /
  **트리거 배선 정합(JSON↔StoryDirector)** / **requiredRegionId 정합(리전 게이트)**

특히 **트리거 배선 정합**이 FAIL이면 Phase 2의 StoryDirector 등록이 누락된 것 —
"switch case 없음" / "이벤트 발화 지점 없음" 진단으로 해당 지점을 채운다.

보상 곤충/아이템 ID·unlockQuestId 오타는 런타임엔 조용히 실패한다 — story_lint가 배포 전에 잡는다.
`ci_check`에도 포함돼 세션 밖 편집(Codex CLI 등)의 결함도 CI가 잡는다.

컴파일 확인(StoryDirector.cs 수정 시)은 `/test`(PlayMode).

## Phase 4: 에이전트 위임

| 작업 | 담당 |
|---|---|
| 스토리 비트 내용·대사·분기 설계 | game-designer |
| 새 trigger.type의 StoryDirector 배선 | 해당 이벤트 시스템 담당(battle-dev/capture-dev 등) + game-designer |
| StoryBeat 데이터 모델 필드 추가 | data-architect |
| NpcDialogueUI 렌더 변경 | ui-dev |
