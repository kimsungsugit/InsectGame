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
행동의 리전은 `RegionManager.ActionRegionId`다(`CurrentRegion`이 아니다) — 나의 섬은 어느 리전도 아니라서 섬 손님 곤충 포획이 떠나기 전 리전의 의뢰로 세어지면 안 된다(`rules/island.md`).
마을 이야기(`Docs/StoryBible.md` 13장)의 짝이다 — 매듭 비트가 `requiredQuestId`로 이 퀘스트의 완료를 **관찰**한다.

- **Story 퀘스트에 달지 않는다.** 선형 체인(`ActivateNextQuest`·`NotifyAction`의 활성 퀘스트 경로)은 이 필드를 안 본다 —
  적어 둔 한정이 거짓이 된다.
- **위치가 있는 목표만** — Capture·CaptureRare·CaptureRarity·Battle·RaidBattle·VisitSubArea·NpcDuel.
- **스토리가 물면 1회형이어야 한다.** 반복 서브는 완료 기록이 안 남아 `requiredQuestId` 게이트가 영영 안 열린다(story_lint 29).
- 목록 UI의 "미해금"도 `IsSideUnlocked`를 쓴다. prereq만 보면 잠긴 리전의 의뢰가 0/5 진행 중으로 뜬다.

## 조건부 퀘스트 — `CaptureTrait` · `BattleFeat` (Side 전용)

"그냥 N마리"가 아니라 **잡은 개체의 성질**(몸길이·속성·이로치)이나 **이긴 전투의 모습**(상대 등급·속성·레벨 차·내 행동 수·남은 HP·연승)이
조건과 맞아야 센다. 새 QuestType을 조건마다 늘리지 않고 **타입 둘 + 조건 필드**로 푼다 — 조건 하나를 더하는 데 5지점 등록이 안 든다.

```
CaptureController / 전투 포획 ──CaptureFacts──▶ NotifyCapture(CaptureFacts) ─┐
InsectBattleController(승리 지점) ──BattleFacts──▶ NotifyBattleFeat ─────────┤ QuestTraitRules.Matches(순수)
                                                                              └▶ 맞는 활성 서브를 먼저 다 모은 뒤 한꺼번에 올린다
```

- **판정은 `QuestTraitRules`(순수)가 단일 출처**다. 조건 필드(`TutorialQuest`)의 0·None·false·Common은 "조건 없음", 채운 것은 **전부** 맞아야 한다(AND).
  몸길이는 **소수 첫째 자리로 반올림해** 잰다(화면 `SizeLabel`과 같은 값 — 39.96이 "40.0mm"로 보이는데 40 이상에 안 세어지면 버그다),
  배율은 부동소수점 오차(롤 90 = 1.2가 1.1999999)를 ε로 흡수한다. **개체를 저장 못 한 포획은 크기를 모른다**(`sizeKnown`) — 중간값이 크기 조건을 채우면 안 된다.
- **병렬이다.** 서브는 다중 활성이라 한 번의 포획·승리가 맞는 조건 전부를 함께 올린다(큰 물 곤충 한 마리 = 큰 곤충 + 물가 + 영웅 채집단).
- **맞는 퀘스트를 먼저 다 모은 뒤 올린다**(`traitTargets`). 배열을 훑으며 바로 올리면 앞 화가 끝나 뒷 화가 열리는 순간 같은 행동이 뒷 화까지 채운다 —
  외전이 "한 걸음씩"이 아니게 된다(`OneWin_CannotAdvanceTwoChaptersOfTheSameTale`).
- **전투 통지는 이긴 지점에서 직접 부른다**(`InsectBattleController` — 야생·수문장·곤충잡이 대결 승리가 모두 지나는 한 곳, 샌드박스 제외). 레이드는 별개
  컨트롤러라 세지 않는다. 통지가 던져도 전투 종료는 계속된다(try/catch) — `battleEnded`는 켜졌는데 `BattleEnded`가 안 울리면 결과 화면이 멈춘다.
  **내 행동 수**는 차례가 넘어간 입력만 센다(스킬·기본 공격·기절로 건너뛴 차례·실패한 도주). 거절된 입력은 안 센다.
- **연승(`resetOnLoss`)**: 이긴 전투는 진행을 올리고, **지거나 도망치면**(`OnBattleEnded(false)`) 진행만 0으로 되돌린다 — 목표·반복 횟수는 그대로,
  진행 이벤트도 쏘지 않는다. 연승이 끊겨도 **조건이 안 맞는 승리는 건드리지 않는다**(다른 조건이 걸린 연승일 때).

### 조건 필드

| 필드 | 쓰는 타입 | 뜻 |
|---|---|---|
| `minSizeMm` · `maxSizeMm` | CaptureTrait | 몸길이(mm) 이상·이하 — 둘 다 주면 구간 |
| `minSizeRatio` · `maxSizeRatio` | CaptureTrait | 같은 종 평균 대비 배율(0.75~1.25) — 종마다 평균이 달라 몸길이와 따로 둔다 |
| `requireShiny` | CaptureTrait | 이로치(색다른 곤충)만 |
| `requiredElement` | 둘 다 | 잡은·맞선 곤충의 주속성 **또는** 부속성 |
| `minRarity` | 둘 다 | 등급 **이상**(CaptureRarity의 `requiredRarity`는 정확히 그 등급 — 다르다) |
| `minLevelEdge` | BattleFeat | 상대 레벨 − **싸우는 내 곤충** 레벨 ≥ N (트레이너 레벨이 아니다) |
| `maxTurns` | BattleFeat | 내 행동 수 ≤ N |
| `minHpPercent` | BattleFeat | 끝났을 때 내 곤충 HP% ≥ N (정수 퍼센트로 비교) |
| `resetOnLoss` | BattleFeat | 연승 |

**엉뚱한 타입의 필드는 조용히 무시된다** — `CaptureTrait`에 `maxTurns`를 쓰면 그 조건이 사라져 저작보다 쉬워진다. 검사 13이 잡는다.

### 기준값은 실측으로 정한다

몸길이는 종 기준(`ApplySizeProfile`: 등급 기준 22/30/42/58/78mm × 종 해시 0.7~1.3)에 개체 편차 0.75~1.25가 곱해진다. 필드 포획 한 번당
**40mm 이상 약 11% · 60mm 이상 약 2.6% · 80mm 이상 약 0.4% · 20mm 이하 약 25% · 16mm 이하 약 7.5%**(등급표 60/25/11/3.5/0.5%, 종 균등 가정).
기준을 손으로 바꾸면 분포 밖의 값이 조용히 들어온다 — `QuestTraitTests.RealDb_EveryTraitQuest_IsReachableAtSensibleOdds`가 실제 곤충 DB와 진짜 판정으로
퀘스트마다 "평균 몇 번 만나야 찬다"를 재서 450번을 넘으면 실패한다(결과표는 로그의 `[QuestTrait]`).
**한계:** 행동 수·HP·레벨 차는 전투가 정해서 DB로 잴 수 없다 — 그 숫자(3번 이내, 70% 등)는 **실측이 아니라 추정**이다. 기기에서 전투 길이를 본 뒤 조정할 것.

### 조건을 새로 만들려면 (필드 하나 + 판정 한 줄)

1. `TutorialQuest`에 필드(기본값 = 조건 없음) → 2. `QuestTraitRules.Matches`와 `HasCondition`에 한 줄 → 3. 입력이 모자라면 `CaptureFacts`/`BattleFacts`와
   `From`, 그 값을 아는 지점(포획·전투 컨트롤러)에서 채우기 → 4. `game_facts.py`의 `TRAIT_*_FIELDS`에 이름 추가(안 하면 검사 13이 새 필드를 못 본다) →
5. `QuestTraitTests`에 경계 테스트. **새 QuestType은 필요 없다.**

### 외전 — 본편 옆을 나란히 가는 연작

`[외전]` 접두 서브·1회·선행 사슬이다(어르신의 낡은 일기 4화 · 라온의 도전장 4화 · 세라의 표본 조사 3화). 본편이나 다른 외전, 반복 서브와 **동시에** 진행된다.

- **스토리 비트·주민이 아니라 퀘스트 글만이다.** 마을 이야기(13장)와 달리 NPC 일곱 곳 등록·`Story.json` 비트가 없다 — 이야기를 비트로 올리려면 그쪽 절차를 따를 것.
- **라온의 말은 직접 하는 말이 아니라 남겨 둔 도전장이다.** 라온이 전력에서 빠지는 구간(ch10~ch12)에 이 목록을 여는 사람에게 그의 목소리가 새로 들리지 않게 한다.
- **세라 연작은 `q_blight_first` 뒤**에 연다 — 세라는 숲에서 합류하므로 만나기 전에 이름이 목록에 뜨면 안 된다. 어르신은 처음부터 곁에 있어 `q_capture3`.
- 주인공 쪽 어휘는 「거둬들이다」를 쓰지 않는다(`StoryBible.md` 2장) — 만나다·맞이하다·기록하다.
- **반복형을 선행으로 물지 않는다.** 반복 서브는 `completedQuests`에 안 들어가 그 뒤가 영영 안 열린다(`RealTable_NoQuestRequiresARepeatableOne`).

## 검증 — 반드시 quest_lint 실행

퀘스트를 수정하면 반드시:

```
python -X utf8 .claude/scripts/quest_lint.py
```

13검사: questId 중복 / prerequisite 무결성(끊김·순환) / 보상 곤충 ID 존재 / 보상 아이템 ID 존재 /
보스 대결 보상 아이템 ID 존재 / **QuestType↔진행 배선**(q_team류 정지 검출) / 대화 리전키 정합성 /
서브 퀘스트 정합(반복은 Side 전용) / 팀 자동 편성 경로 / **prereq 방향**(배열 앞을 가리켜야
소급 완료가 안전 — 뒤를 가리키면 아직 할 차례인 퀘스트를 보상 없이 삼킨다) /
**지역 의뢰 정합**(리전 실재 · Side 전용 · 위치 있는 목표 — 리전 ID 오타면 의뢰가 목록에서 잠긴 채 남는다) /
**questId 구분자 금지**(`,`·`:`·공백 — `QuestSaveMerge`의 세이브 병합 포맷이 그 문자를 구분자로
쓴다. 든 ID는 병합에서 조용히 버려지고 write-back이 손실을 영구화한다) /
**조건부 퀘스트 정합**(Side 전용 · 조건 있음 · 타입에 맞는 필드 · 하한≤상한 — 전부 무증상이다: 조건이 없으면 무엇이든 세고,
엉뚱한 타입의 필드는 조용히 무시되고, Story에 달면 영구 정지한다).
`ci_check`에도 포함돼 세션 밖 편집(Codex CLI 등)의 결함도 CI가 잡는다.

보상 ID(`rewardInsectId`/`rewardItemId`)는 존재하지 않는 값을 물어도 런타임엔 `LogWarning`만
찍고 조용히 실패한다 — quest_lint가 그 오타를 배포 전에 잡는다.

## 에이전트 위임

| 영역 | 주담당 | 부수 |
|---|---|---|
| `TutorialQuestManager`/`TutorialQuestData` 퀘스트 로직·목표·보상 | game-designer | data-architect |
| `TutorialQuestUI` 렌더링 | ui-dev | — |
| 새 QuestType의 게임플레이 호출부 삽입 | 해당 시스템 담당(battle-dev/capture-dev 등) | game-designer |
| `QuestTraitRules` 조건 판정·기준값, 조건부 퀘스트와 외전 저작 | game-designer | data-architect |
| 조건부 통지의 입력(`CaptureFacts`/`BattleFacts`를 채우는 지점) | capture-dev(포획) · battle-dev(전투 승리 지점·행동 수) | game-designer |
