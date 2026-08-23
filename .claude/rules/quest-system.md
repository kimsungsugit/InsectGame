---
description: 퀘스트 데이터 정의 위치·추가 절차·Notify 배선 규칙 (퀘스트 수정 시 필독)
---

# 퀘스트 시스템 규칙

## 정의 위치 — 코드 하드코딩 (SO/JSON 아님)

퀘스트는 `TutorialQuestManager.Initialize()`의 `allQuests = new TutorialQuest[] { … }`
배열에 C# 코드로 정의된다. `.asset`도 `.json`도 아니다. 데이터 모델은
`TutorialQuestData.cs`의 `TutorialQuest` 클래스 + `QuestType` enum.

퀘스트 진행은 PlayerPrefs에 저장된다 — `save-system.md`의 "퀘스트 세이브" 참조.

## 추가 절차 — 다지점 등록 (누락 시 퀘스트 영구 정지)

새 퀘스트의 목표가 **기존 QuestType**로 표현되면 배열 1곳만 수정한다(단 선형 체인이라
`prerequisiteQuestId` 재배선 주의).

**새 목표 타입**이면 5곳을 모두 건드려야 한다. 하나라도 빠지면 그 퀘스트는
`IncrementProgress`에 영영 도달 못 해 **영구 정지**한다:

1. `QuestType` enum 추가 — `TutorialQuestData.cs`
2. 퀘스트 배열 항목 + prerequisite 체인 배선 — `TutorialQuestManager.cs`
3. `Notify___()` 메서드 + **실제 게임플레이 호출부 삽입**. 진행 트리거가 배틀/포획/UI 등
   어느 시스템에서 발생하는지 찾아 그곳에서 `TutorialQuestManager.Instance.Notify___()`를 부른다.
4. **이벤트 기반이면** `SubscribeEvents`/`UnsubscribeEvents`에 핸들러 등록 +
   핸들러가 `NotifyAction(QuestType.X)` 호출
5. 캔디/EXP/아이템/곤충 외 **새 보상 종류**면 데이터 모델 필드 + `CompleteQuest` 로직

### q_team 회귀 — 실제로 겪은 사고

`SetTeam` 퀘스트가 `OnTeamChanged` 핸들러는 있었으나 `SubscribeEvents`에
`battleTeamManager.TeamChanged += OnTeamChanged`가 없어 **영구 정지**했다
(`TutorialQuestManager.cs:322` 주석이 방어 흔적). 이벤트 기반 QuestType은 3번(호출부)이
아니라 4번(구독 등록)이 누락 지점이다.

## 검증 — 반드시 quest_lint 실행

퀘스트를 수정하면 반드시:

```
python -X utf8 .claude/scripts/quest_lint.py
```

10검사: questId 중복 / prerequisite 무결성(끊김·순환) / 보상 곤충 ID 존재 / 보상 아이템 ID 존재 /
보스 대결 보상 아이템 ID 존재 / **QuestType↔진행 배선**(q_team류 정지 검출) / 대화 리전키 정합성 /
서브 퀘스트 정합(반복은 Side 전용) / 팀 자동 편성 경로 / **prereq 방향**(배열 앞을 가리켜야
소급 완료가 안전 — 뒤를 가리키면 아직 할 차례인 퀘스트를 보상 없이 삼킨다).
`ci_check`에도 포함돼 세션 밖 편집(Codex CLI 등)의 결함도 CI가 잡는다.

보상 ID(`rewardInsectId`/`rewardItemId`)는 존재하지 않는 값을 물어도 런타임엔 `LogWarning`만
찍고 조용히 실패한다 — quest_lint가 그 오타를 배포 전에 잡는다.

## 에이전트 위임

| 영역 | 주담당 | 부수 |
|---|---|---|
| `TutorialQuestManager`/`TutorialQuestData` 퀘스트 로직·목표·보상 | game-designer | data-architect |
| `TutorialQuestUI` 렌더링 | ui-dev | — |
| 새 QuestType의 게임플레이 호출부 삽입 | 해당 시스템 담당(battle-dev/capture-dev 등) | game-designer |
