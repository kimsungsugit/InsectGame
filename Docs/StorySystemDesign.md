# 스토리 시스템 설계

곤충게임에 스토리 시스템을 신규 구축하기 위한 구조 프레임워크. **스토리 내용(플롯·대사)이
아니라 그걸 담는 그릇(데이터 모델·세이브·트리거·연동)의 청사진.** game-designer 설계.

기존 시스템(퀘스트/대화/리전)을 갈아엎지 않고 그 위에 **관찰(구독)만 하는 가산 레이어**로 얹는다.

## 설계 결정

| # | 결정 | 근거 |
|---|---|---|
| 1. 데이터 모델 | **JSON (Resources)** | InsectLore.json 선례(`Resources.Load<TextAsset>`+`JsonUtility`). 검증 가능성이 결정타 — 스토리 비트는 `lines[]`·`choices[]` 중첩 구조라 quest_lint의 C# 배열 정규식(`[^}]*`, 리터럴만 동작)으로는 파싱 불가. JSON은 파이썬 `json.load`로 **네이티브 파싱** → 퀘스트 코드 배열보다 오히려 견고. `.asset`(SO)은 GUID YAML이라 내용 검증 불가. 데이터 편집에 재컴파일 불필요, 신규 싱글턴 0개 |
| 2. 진행/세이브 | **신규 `story_progress.json` + 클라우드 동기** | 지배적 7-JSON 패턴(save-system.md). `seenBeatIds`는 무한 증가 집합이라 JSON 리스트가 자연스럽다(퀘스트 PlayerPrefs CSV는 레거시). SaveScope uid 격리. 클라우드 동기(타 기기 재관람 방지) — `GameSaveData.storyProgress` + Firestore 직렬화 |
| 3. 트리거 | **기존 이벤트 재사용 + StoryDirector 평가** | 새 이벤트 배선 없이 퀘스트가 이미 쓰는 `RegionChanged`/`BattleEnded`/`SubAreaChanged`/`TeamChanged`/`QuestCompleted` 재구독. `StoryDirector`(MonoBehaviour, AutoWire, **싱글턴 아님**)가 비트별 트리거 평가. 트리거는 **닫힌 enum** → 하네스가 param 대상 존재를 검증 가능 |
| 4. 대화 연동 | **스토리 전용 대사 별도 + NpcDialogueDatabase 앰비언트 유지** | NpcDialogueDatabase는 결정적 절차 배경 대사(분기·상태 없음). 플롯을 붙이면 그 계약이 깨지고 per-player 상태를 못 담는다. 스토리 대사는 비트 JSON의 `lines[]`에 저작, **기존 NpcDialogueUI 모달로 렌더**(렌더러 재사용, 데이터 출처만 신규). 비트는 `speakerNpcId`로 이름/초상만 참조 |
| 5. 퀘스트 연동 | **독립 트랙, 단방향 관찰만** | `QuestType.StoryBeat` 추가는 스토리를 퀘스트 5지점 배선 + quest_lint에 얽어맨다(결합). 스토리 완료는 "카운트 도달"이 아니라 "비트 열람"이라 수명주기가 다르다. 비트는 `trigger.type=QuestComplete`로 퀘스트 상태를 **관찰(게이팅)**만. 역방향(비트가 퀘스트 주입)은 하드코딩 선형 prereq라 위험 → 배제 |

## 데이터 모델

```
StoryBeat: beatId, chapterId, order, prerequisiteBeatId(옵션),
  trigger { type(enum), param(string) },
  speakerNpcId(옵션),
  lines[] { speaker, text },
  choices[](옵션, 분기) { text, nextBeatId },
  onComplete { rewardCandy, rewardExp, rewardItemId+Count,
               rewardInsectId+DisplayName+Level,   // 퀘스트 보상 필드 재사용
               unlockQuestId(옵션) },
  oneShot(기본 true)
StoryList { beats[] }   // JsonUtility 래퍼 (InsectLoreList와 동형)

trigger.type enum: RegionEnter / QuestComplete / LevelReach / CaptureInsect /
                   BattleWin / SubAreaEnter / Immediate
```

## 흐름

Bootstrap 10단계(퀘스트/리전 뒤): `StoryDirector` 생성 → `AutoWire(region, battle, progress,
collection, quest)` → `RegisterReloadable` → `story_progress.json` 로드.
`StoryService.EnsureCache()`가 `Story.json` → `Dictionary<beatId, StoryBeat>`.
Director가 이벤트 구독; 매 이벤트에서 미열람·prereq충족·트리거일치 비트를 찾아
`StoryBeatTriggered(beat)` 발화 → `NpcDialogueUI`가 `lines` 렌더 → 닫으면 seen 마킹 +
`onComplete` 지급 + 저장 + 즉시 `SaveToCloud()`(퀘스트 보상 패턴 동일).

## 신규 파일

| 파일 | 내용 |
|---|---|
| `Assets/Resources/Story.json` | 비트 데이터 |
| `Assets/Scripts/Story/StoryBeat.cs` | StoryBeat/Line/Choice/Trigger/List (`InsectGame.Story`) |
| `Assets/Scripts/Story/StoryService.cs` | static 로더 (InsectLoreService 복제) |
| `Assets/Scripts/Story/StoryDirector.cs` | 트리거 평가·진행·ICloudReloadable |
| `Assets/Scripts/Story/StoryProgressData.cs` | `[Serializable] { List<string> seenBeatIds; string activeChapterId }` |

## 기존 파일 수정 지점

- `PlaySceneBootstrap.cs`: Director 생성 + AutoWire + RegisterReloadable (10단계)
- `GameConstants.cs`: `SaveFiles.StoryProgress`
- `CloudSaveManager.cs`: `GameSaveData.storyProgress` + Firestore 직렬화 (data-architect 경계)
- `NpcDialogueUI.cs`: 저작 대사 시퀀스 렌더 진입점 (ui-dev 경계)

## Phase 5 하네스 (스토리 시스템 구축 후 작성)

**`story_lint.py`** (quest_lint 형제. `game_facts.story_beats()`는 정규식이 아니라 `json.load` — 퀘스트보다 견고):
1. beatId 유일성
2. prerequisiteBeatId 무결성 (끊김/자기참조/순환 — quest_lint `_prereq_cycle` 재사용)
3. **트리거 param 대상 존재**: RegionEnter→region_pools / QuestComplete→quest_defs / CaptureInsect→all_insect_ids / LevelReach→정수 범위
4. **분기 도달성**: `choices[].nextBeatId` 존재 + 고아/미도달 비트 없음
5. `onComplete` 보상 ID 존재 (data_lint 곤충/아이템 추출 공유) + `unlockQuestId`→퀘스트 존재
6. **트리거 배선 정합** — q_team 회귀의 스토리 등가물: JSON이 쓰는 각 `trigger.type`이
   `StoryDirector` 평가 switch + 이벤트 구독에 존재하는지 교차검사. 누락 시 비트 영구 미발화

**add-story 스킬** 등록 지점: (1) `Story.json`에 비트 append, (2) **신규 trigger.type이면**
StoryDirector 평가 case + 이벤트 구독 추가 — **비트를 영구 정지시킬 유일 지점(퀘스트 5지점의
스토리판)**, (3) `story_lint` FAIL=0 확인.

## 핵심 급소

하네스의 급소는 퀘스트와 동형이다 — **신규 `trigger.type`의 Director 배선 누락(검사 6)이
유일한 영구 정지 벡터.** 트리거 타입을 JSON에 추가하고 StoryDirector 평가 switch나 이벤트
구독을 빠뜨리면, 그 타입을 쓰는 비트가 영영 발화하지 않는다. quest_lint의 "QuestType↔진행
배선" 검사와 정확히 같은 구조.
