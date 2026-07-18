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
  "trigger": { "type": "RegionEnter", "param": "pond" },
  "speakerNpcId": "village_elder",
  "lines": [ { "speaker": "이름", "text": "대사" } ],
  "choices": [],
  "onComplete": { "rewardCandy": 5, "rewardExp": 0, "rewardItemId": "",
    "rewardInsectId": "", "unlockQuestId": "" },
  "oneShot": true
}
```

**trigger.type (기존 7종)**: RegionEnter, QuestComplete, LevelReach, CaptureInsect,
BattleWin, SubAreaEnter, Immediate. `param`은 그 타입의 대상 ID:
- RegionEnter/SubAreaEnter → 리전 ID (meadow/pond/…)
- QuestComplete → questId (q_approach 등)
- CaptureInsect → 곤충 ID (비우면 아무 포획)
- LevelReach → 정수 레벨

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

## Phase 3: 검증 — 반드시 실행

```
python -X utf8 .claude/scripts/story_lint.py
```

6검사가 전부 PASS여야 한다:
- beatId 중복 / prerequisite 무결성(끊김·순환) / 트리거 param 대상 존재 /
  분기 도달성(choices.nextBeatId) / onComplete 보상·unlock ID 존재 /
  **트리거 배선 정합(JSON↔StoryDirector)**

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
