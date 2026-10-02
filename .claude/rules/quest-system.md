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
5. 캔디/EXP/아이템/곤충/코인/섬 물건 외 **새 보상 종류**면 데이터 모델 필드 + 지급(`GrantRewards`) +
   표시(`QuestRewardFormatter`) — 지급과 표시가 **같은 술어**를 써야 "보이는데 안 주는" 어긋남이 없다.
   **다이아는 보상으로 줄 수 없다**(서버 규칙이 클라이언트의 다이아 증가를 거부한다 — `rules/island.md`).

### q_team 회귀 — 실제로 겪은 사고

`SetTeam` 퀘스트가 `OnTeamChanged` 핸들러는 있었으나 `SubscribeEvents`에
`battleTeamManager.TeamChanged += OnTeamChanged`가 없어 **영구 정지**했다
(`TutorialQuestManager.cs:322` 주석이 방어 흔적). 이벤트 기반 QuestType은 3번(호출부)이
아니라 4번(구독 등록)이 누락 지점이다.

## 스토리 체인은 "하는 일"만 — 창 열기 과제는 「둘러보기」 서브다

수문장까지의 스토리 체인은 이동 → 어르신 → 포획 1 → 포획 3 → 레벨업 → 전투 1 → 전투 3 → 희귀 포획 → 수문장이다.
창을 한 번 열면 끝나는 과제(`q_collection`·`q_dex`·`q_equip`·`q_item`·`q_training`·`q_team`)는 **배열 자리는 그대로 두고
`category = Side`로** 돌렸다(제목 접두 `[둘러보기]`). 예전엔 수문장 전 14개 중 7개가 그런 과제였고 첫 전투가 9번째였다(2026-10-02).

- **스토리 퀘스트의 상대 순서를 바꾸지 않는다.** 소급 완료(`BackfillSkippedStoryQuests`)가 "배열 순서 = 완료 순서"에
  기대므로, 뒤에 있던 퀘스트를 앞으로 당기면 기존 세이브에서 **아직 할 차례인 그 퀘스트가 보상 없이 삼켜진다.**
  빼는 것(Side로 돌리기)은 안전하다 — 남은 것들의 순서가 그대로라서다.
- **스토리의 선행은 스토리여야 한다.** 서브 과제를 선행으로 물면 그 과제를 안 한 사람의 본편이 멈춘다
  (`TutorialQuestOrderTests.RealChain_EveryStoryPrerequisite_IsAStoryQuest`).
- 저장된 활성 퀘스트가 스토리가 아니면 다음 스토리로 넘긴다(`ActiveQuestNeedsReselect`) — 「둘러보기」 과제가
  활성인 채 저장된 세이브가 그 경우다.

## 미리 세기 — 차례가 오기 전에 한 일도 센다

포획·전투·레이드·레벨업은 **아직 활성이 아닌 스토리 퀘스트에도** 진행이 쌓인다(`BankUpcomingStoryProgress`).
예전엔 활성 퀘스트만 세어서, 다른 퀘스트를 하는 동안 잡은 세 마리가 "3마리 포획" 차례에 0으로 돌아갔다.
이제 "총 N번"이다 — 포획 1을 끝내면 포획 3은 1/3에서 시작하고, 차례가 왔을 때 이미 채웠으면 그 자리에서 완료한다
(`ReconcileBankedProgress`).

- 대상은 `TutorialQuestOrder.IsBankable`이 정한다. **`CleanseBlight`는 넣지 않는다** — 「하나 무너뜨리기」와 「하나 더」가
  이어져 있어 미리 세면 첫 정화 하나가 둘을 한꺼번에 깬다. 방문·이동·대화처럼 **그 순간의 행동이어야 하는 것**도 아니다.
- 새 QuestType을 늘릴 때 "몇 번 했는가"를 세는 종류라면 `IsBankable`에 넣을지 정할 것(기본은 안 넣는다).
- 조용히 올린다 — 진행 이벤트를 쏘면 아직 안 뜬 퀘스트의 알림이 뜬다.

## 지역 의뢰 — `requiredRegionId` (Side 전용)

서브 퀘스트에 `requiredRegionId`를 채우면 **①그 리전이 열려야 목록에 뜨고 ②그 리전 안(서브에리어 포함)에서 한
행동만 센다.** 판정은 `QuestRegionGate`(순수)가 하고, 해금 여부는 `RegionManager.IsRegionAccessible`을 읽는다.
마을 이야기(`Docs/StoryBible.md` 13장)의 짝이다 — 매듭 비트가 `requiredQuestId`로 이 퀘스트의 완료를 **관찰**한다.

- **Story 퀘스트에 달지 않는다.** 선형 체인(`ActivateNextQuest`·`NotifyAction`의 활성 퀘스트 경로)은 이 필드를 안 본다 —
  적어 둔 한정이 거짓이 된다.
- **위치가 있는 목표만** — Capture·CaptureRare·CaptureRarity·Battle·RaidBattle·VisitSubArea·NpcDuel.
- **스토리가 물면 1회형이어야 한다.** 반복 서브는 완료 기록이 안 남아 `requiredQuestId` 게이트가 영영 안 열린다(story_lint 29).
- 목록 UI의 "미해금"도 `IsSideUnlocked`를 쓴다. prereq만 보면 잠긴 리전의 의뢰가 0/5 진행 중으로 뜬다.

## 검증 — 반드시 quest_lint 실행

퀘스트를 수정하면 반드시:

```
python -X utf8 .claude/scripts/quest_lint.py
```

12검사: questId 중복 / prerequisite 무결성(끊김·순환) / 보상 곤충 ID 존재 / 보상 아이템 ID 존재 /
보스 대결 보상 아이템 ID 존재 / **QuestType↔진행 배선**(q_team류 정지 검출) / 대화 리전키 정합성 /
서브 퀘스트 정합(반복은 Side 전용) / 팀 자동 편성 경로 / **prereq 방향**(배열 앞을 가리켜야
소급 완료가 안전 — 뒤를 가리키면 아직 할 차례인 퀘스트를 보상 없이 삼킨다) /
**지역 의뢰 정합**(리전 실재 · Side 전용 · 위치 있는 목표 — 리전 ID 오타면 의뢰가 목록에서 잠긴 채 남는다) /
**questId 구분자 금지**(`,`·`:`·공백 — `QuestSaveMerge`의 세이브 병합 포맷이 그 문자를 구분자로
쓴다. 든 ID는 병합에서 조용히 버려지고 write-back이 손실을 영구화한다).
`ci_check`에도 포함돼 세션 밖 편집(Codex CLI 등)의 결함도 CI가 잡는다.

보상 ID(`rewardInsectId`/`rewardItemId`)는 존재하지 않는 값을 물어도 런타임엔 `LogWarning`만
찍고 조용히 실패한다 — quest_lint가 그 오타를 배포 전에 잡는다.

## 에이전트 위임

| 영역 | 주담당 | 부수 |
|---|---|---|
| `TutorialQuestManager`/`TutorialQuestData` 퀘스트 로직·목표·보상 | game-designer | data-architect |
| `TutorialQuestUI` 렌더링 | ui-dev | — |
| 새 QuestType의 게임플레이 호출부 삽입 | 해당 시스템 담당(battle-dev/capture-dev 등) | game-designer |
