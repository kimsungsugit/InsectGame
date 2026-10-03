# 나의 섬 규칙

개인 섬 하우징. 기획·구조의 배경은 `Docs/IslandDesign.md`, 수치는 코드(`GameConstants.Island`·`IslandCatalog`)가 단일 출처다.

## 섬은 「분리 서브에리어」로 탄다 — 별개 상태를 만들지 않는다

"분리된 공간에 있다"는 판정이 전부 `RegionManager.CurrentSubArea != null`에 걸려 있다(플레이어 접지·끼임 복구,
스포너 필드 틱, 미니맵·지도, 환경광, 오염 VFX). 섬을 별개 상태로 두면 그 전부를 따로 고쳐야 하고, 빠뜨리면
섬을 오갈 때마다 `RegionChanged`가 다시 울려 **방문 퀘스트·스토리 RegionEnter·BGM이 왕복마다 재발화**한다.

그래서 섬은 `SubAreaData.detached = true`인 합성 구역(id `player_island`)이고
`RegionManager.EnterDetachedSubArea`로만 들어간다. 그 대가로 **`detached`를 걸러야 하는 자리**가 생긴다:

| 자리 | 안 거르면 |
|---|---|
| `SubAreaWorldBuilder.OnSubAreaChanged` | 섬에 들어갈 때 (2000,·,2000)에 빈 방을 짓고 25m 자동 이탈이 돈다 |
| `TutorialQuestManager.OnSubAreaChanged` | 섬 방문이 `q_subarea`(숨겨진 장소)를 깬다 |
| `PlayerMovement.RecoverToSafePosition` | 끼임 복구가 (2000,·,2000) 허공으로 보낸다 |
| `SubAreaEnvironment` 프로필 switch | 섬이 동굴 기본 조명으로 뜬다 |

`SubAreaChanged`를 새로 구독하는 코드를 쓴다면 섬도 그 이벤트를 탄다는 걸 기억할 것.

## 모양과 콜라이더는 나뉘어 있다

`IslandTerrainBuilder`·`IslandObjectBuilder`(visual-dev)는 **콜라이더를 남기지 않는다.** 밟는 땅·경계 벽·물건 차단은
`IslandWorldBuilder`가 따로 단다. 플레이어 접지와 클릭-이동이 콜라이더를 보므로, 장식에 콜라이더가 남으면
지붕 위로 걸어 올라가거나 모래톱·바다로 걸어 나간다.

머티리얼은 `IslandMaterialCache`로만 만든다(색이 같으면 같은 머티리얼 — 섬 전체가 수십 개로 끝난다).
`new Material`·`Shader.Find` 금지: 빌드에 변형이 남는 키워드 조합은 `SceneryMaterials`가 쥐고 있다.

오브젝트 이름을 `Region_`·`Scenery_`·`Path_`·`SubArea_`·`Boundary_` 등으로 시작하지 않는다 —
`SubAreaWorldBuilder.HideMainWorld`가 그 접두어를 끈다(본 마을 나루터 `IslandDock`이 그 대상이 될 뻔했다).

## 물건을 늘릴 때 — 3곳

1. `IslandCatalog.Build()`에 한 줄(가격은 코인·다이아 중 **하나만**).
2. `IslandObjectBuilder`의 switch에 모델. 빠뜨리면 **상자 모양 폴백으로 조용히 놓인다** — `IslandCatalogTests.EveryObject_HasItsOwnModel`이 잡는다.
3. id 형식 `^[a-z][a-z0-9_]{1,31}$`. 서버(`functions/island.js`)가 그 형식만 받는다 — 어긋나면 공개할 때 그 물건이 빠진다.

효과(`IslandEffectKind`)를 늘리면 `IslandYield.CollectEffects`와 `IslandUiKit.EffectText` 두 switch를 함께.
생산 설비를 늘리면 `IslandYieldTests`의 대역 검사와 `economy_sim` 신호 6·7이 최대 수입을 다시 잰다.

## 세이브 — 정산을 먼저, 저장을 지급보다 먼저

- **생산에 영향을 주는 변경 전에 `Settle()`** — 안 그러면 방금 놓은 온실이 지난 여덟 시간에도 있었던 것처럼 계산된다.
  `IslandManager`의 TryPlace·Store·TryRelease·Unrelease가 전부 그렇게 한다. 새 변경 경로를 만들면 따라 할 것.
- 수확은 **저장 → 지급** 순서다. 반대면 지급 뒤 저장 전에 죽었을 때 같은 수확물을 다시 받는다.
- `Awake`에서 저장하지 않는다. 로그인 전이라 전역 경로에 island.json을 만들어 버린다.
- 방목 목록은 `instanceId`다. 보유 목록에서 사라진 id는 **읽을 때 걸러내고**, 지우는 건 사용자가 곤충 창에서
  조작하는 시점에만 한다 — 부트 중에 지우면 로그인 전 빈 목록을 보고 전부 지운다.
- 카탈로그가 모르는 물건 id는 **지우지 않는다**(더 새 버전에서 산 물건). 그리기·효과·겹침 검사가 건너뛸 뿐이다.

## 다이아는 늘릴 수 없다

`firestore.rules`가 클라이언트의 다이아 증가를 거부한다 — 로컬 다이아가 클라우드보다 커지면 **세이브 업로드 전체가 막힌다.**
그래서 되팔기 환급도, 퀘스트·수확의 다이아 보상도 없다. 회수한 물건은 보관함으로 돌아간다.
다이아 결제 뒤에는 소유권을 적고 `SaveToCloud()`를 한 번 더 부른다(`SpendGems`가 부른 저장은 소유권을 적기 전 스냅샷이다).

## 공유 — 서버 없이도 섬은 돈다

방문·좋아요는 Cloud Function `socialPvpApi`의 액션(`publishIsland`·`getIsland`·`likeIsland`·`getMyIsland`·`deleteIsland`)이고
저장은 `islands/{uid}`다. 규격은 `Docs/SocialPvp.md` 「섬 공유」. 타인 문서는 규칙상 클라이언트가 직접 못 읽는다.

**운영에는 `socialPvpApi`가 배포돼 있지 않다**(2026-10-02 확인 — 함수 0건, 주소 404. 같은 날 배포하지 않기로 정했다).
"방문이 안 된다"·"`s_island_visit`이 안 깨진다"는 보고는 버그가 아니라 이 상태다. 섬만 따로 켤 수 없다 —
배포하면 친구·PvP·월드 채널이 함께 켜진다(`Docs/SocialPvp.md` 「배포」).

- 받은 스냅샷은 **남이 만든 데이터**다 — `IslandSaveRules.SanitizeSnapshot`이 모르는 물건·경계 밖·겹침을 버린다.
- 올리는 건 `BuildSnapshot`의 축약본뿐이다(instanceId·재화 없음).
- 계정 삭제는 Auth 계정을 지우기 **전에** `deleteIsland`를 부른다(`AuthManager.DeleteAccountCoroutine`) — 지운 뒤엔 토큰이 없다.

## 화면

- `IslandHudUI`는 **비모달**이다 → 매 OnGUI `FieldHudInput.RegisterBlockingRect`(rules/ui-layout.md).
  모바일에서는 좌하단 사분면(가상 조이스틱 시작 영역)에 버튼을 두지 않는다.
- 자리는 `IslandHudLayout`(순수 계산)이 정하고 시각·날씨 칩(`WorldClockHUD`)이 같은 계산으로 피해 간다. **가로 모바일은 단축 바 왼쪽 두 칸 판**
  (높이를 단축 바와 같게) — 세로 열(7줄)을 단축 바 아래에 세우면 화면 85%까지 내려와 우하단 잡기 버튼을 덮는다(밤·비 손님 곤충 때문에
  섬에서도 잡기 버튼이 뜬다). 수확·좋아요 토스트는 **가운데 무대**(`HudStage` — 포획 결과·퀘스트 완료와 한 자리에서 차례로 선다,
  rules/ui-layout.md). `HudOverlapSweepTests`가 섬 화면(내 섬·남의 섬)의 모든 HUD 쌍을 화면 18장에서 잰다.
- `IslandEditUI`는 **모달**이다. 지면 탭이 「칸 고르기」인데 같은 탭을 `PlayerMovement`가 클릭-이동으로도 읽기 때문이다.
  그 위에 뜨는 안내 배너는 자기 자리를 `FieldHudInput`에 등록하고, 꾸미기 화면이 그걸 보고 탭을 양보한다
  (OnGUI 호출 순서가 정해져 있지 않아 먼저 도는 쪽이 탭을 먹는다).
- 섬 화면은 이벤트를 구독하지 않고 상태를 읽는다 — UI 루트가 꺼졌다 켜질 때 구독이 사라지는 계열(subscription_lint)을 피한다.
- 안내 배너(`IslandGuideUI`)는 ✕나 12초 경과로 사라진다. **닫는 것은 표시만이다** — 단계는 실제 행동으로만 넘어가고
  (다시 보기만 ✕가 끝낸다: `EndGuideReplay`), 다음 단계·섬 재진입 때 다시 뜬다. 모바일 필드에서는 화면 가운데 줄(캐릭터 발 아래)에
  놓는다 — 좌상단은 미니맵·퀘스트 칩 자리라 그리기 순서가 정해지지 않은 IMGUI에서 배너가 그 밑에 깔렸다(2026-10-02).
  **검수 fixture에 없는 HUD와의 겹침은 캡처로 안 보인다** — `island-ui`의 안내 장면에 진짜 미니맵과 퀘스트 칩 자리 대역을 함께 띄우는 이유다.

## 손님 곤충 — 밤·비·안개에 찾아오는 야생 곤충

규칙은 `IslandGuestRules`(순수), 기록·몸은 `IslandWorldBuilder.Guests.cs`. **필드 스폰의 순수 함수를 그대로 다시 쓴다** — 등급표·레벨·재생 지연·보너스 수를
섬에서 새로 만들지 않는다(`FieldSpawnRules`). **섬 수입과는 무관하다**(사용자 결정 — 섬 생산 보너스 없음, `economy_sim` 신호 6·7은 그대로다).

| 무엇 | 규칙 | 출처 |
|---|---|---|
| 수 | 밤 +1, 비·안개 +1, 최대 2 — 세계 날씨(섬은 지역이 없다) | `FieldSpawnRules.BonusSlots` |
| 후보 | **해금된 리전**(`RegionManager.IsRegionAccessible`) 풀의 합집합 ∩ 지금 시간·날씨에 나오는 종. 비면 합집합 전체 | `InsectSpawner.CopyFieldRegionTables` |
| 등급 | 전역 표로 먼저(부스트 없음). **희귀까지** — 영웅·전설은 후보에서 빼고 그 몫(4%)은 기존 대체 규칙대로 희귀로 내려온다(희귀 11% → 15%) | `FieldSpawnRules.PickRarity` |
| 종 | 그 등급 안에서 `spawnWeight × InsectHabits.SpawnWeightMultiplier` | `FieldSpawnRules.PickWeighted` |
| 레벨 | 그 종이 사는 해금 리전 중 **가장 낮은** 리전의 대역 | `FieldSpawnRules.RollFieldLevel` |
| 재생 | 잡기·이기기·놓침 뒤 60~120초, 수명 4~7분, 조건이 막 시작되면 도착을 5~60초에 흩는다 | `RespawnDelay`·`Lifetime`·`PhaseSwapDelay` |

- **진짜 야생 개체다** — `InsectEntity.Initialize`(야생 경로)로 세운다. 그래서 잡기 버튼 → 포획 선택 창(미니게임·배틀) → 포획 보상·도감 등록·퀘스트 통지가
  필드와 같은 길을 탄다. 방목 곤충(`BuildForBattle` — AI 없음·포획 불가)과 섞지 말 것.
- **레벨 대역을 왜 그 종의 가장 낮은 해금 리전에서 잡나**: 가장 높은 해금 리전을 쓰면 섬이 고레벨 사냥터가 되고, 가장 낮은 리전(초원)을 쓰면 뒤 리전 종을
  Lv.1~10으로 거저 준다. 그 종을 필드에서 처음 만나는 곳의 레벨이면 둘 다 아니다.
- **영웅·전설을 왜 빼나**: 레이드로만 맞서는 등급이라(`CaptureChoiceUI.IsRaidRarity`) 집에서 이동 없이 밤마다 레이드(이기면 확정 포획)를 받는 길이 생긴다.
  섬은 곤충을 1배로 두는 곳인데 전설 야생 몸은 1.9배라 가구 사이에서 어색하고, 섬 ↔ 레이드 아레나 왕복은 아직 기기에서 보지 않았다.
- **떠나는 규칙**: 조건이 끝났거나(아침이 오고 맑아져 수가 줄었다) 수명이 다했고, 붙잡히지·놀라지 않았고, **화면 밖**일 때 조용히 떠난다(`InsectEntity.Recall` —
  게임플레이 퇴장이 아니다). 섬은 한 변이 15~33m라 필드의 "25m 밖" 대신 카메라 화면으로 잰다. 들어설 때도 화면 밖 칸(다 보이면 가장 먼 칸)에, 플레이어에서 3칸 이상 떨어져 선다.
- **도주는 섬의 빈 칸을 벗어나지 않는다**(`InsectEntity.SetFleeArea` + `IslandGuestRules.FreeRun`) — 물리 측정은 벽·건물만 보고 "빈 칸"을 모른다.
  놓침은 평소처럼 사라지고 다음 손님은 재생 지연 뒤다(필드의 "같은 개체를 눈 밖으로 옮김"은 섬이 작아 옮길 눈 밖이 없다).
- **기록이 개체다** — 섬을 나가도 기록은 남고(몸만 거둔다) 돌아오면 같은 손님이다. 섬을 드나들어 다시 굴리지 못한다. 떠나 있는 동안 조건이 끝난 손님은
  이미 떠난 것으로 친다. **세션 간에는 저장하지 않는다**(필드 개체군과 같다).
- 손님 몸은 섬 `root` 밖의 작은 풀(`IslandGuestBodies`, 최대 2)에 둔다 — 섬을 다시 지을 때(`DestroyWorld`) 같이 부서지지 않게. 배치가 바뀌어 손님이 선 칸이
  막히면 그 몸만 거두고 다음 틱에 다른 빈 칸에 선다.

### 어디서 막나

| 자리 | 막는 것 |
|---|---|
| `IslandGuestRules.CanHostGuests` | 남의 섬 구경(`Visit` — 스냅샷)·꿈 섬(`DreamMode`)·꾸미기 중(`IsEditing`)에는 들어서지 않는다 |
| `IslandWorldBuilder.TickGuests` | 내 섬이 아니면(`Visit`·꿈) 내 섬의 손님 기록을 건드리지 않는다 — 세우지도 떠나보내지도 |
| `CaptureInputController` | 꾸미기 화면(`IslandEditUI`)이 모달이라 그동안 [E]·잡기 버튼이 막힌다(`ModalUIRegistry.IsAnyOpen`) |
| `AmbushRules` | 서브에리어 거절 — 섬 손님은 습격하지 않는다(습격형이어도 온순한 곤충처럼 군다) |

**섬은 어느 리전도 아니다**: 섬에 있는 동안 `RegionManager.CurrentRegion`은 섬으로 떠나기 전 리전이다(sticky). 그래서 그 값을 읽으면 섬 손님을 잡을 때
지역 의뢰(`QuestRegionGate`)와 리전 게이트 스토리 비트(`requiredRegionId`)가 그 리전의 포획으로 센다. **"행동이 어느 리전에서 일어났나"는 `RegionManager.ActionRegionId`를 읽는다**
(섬에서는 null, 동굴은 그 리전 — `RegionActionRegionTests`). `TutorialQuestManager.CountsHere`와 `StoryDirector.RegionGateSatisfied`가 그렇게 한다.
리전 기준으로 행동을 세는 코드를 새로 쓴다면 `CurrentRegion`이 아니라 이걸 읽을 것.

## 방목 곤충의 기분 — 시간·날씨

`IslandInsectMood`(순수)가 성향(`InsectHabits`)을 활동도 0~1로 바꾸고, `IslandInsectWalker`가 쉬는 시간·걷는 속도·나는 높이에 곱한다. 야행성은 밤에 활발하고 낮엔
대부분 쉰다(오래 쉬고 느리고, 나는 종은 땅에 낮게 내려앉는다). 주행성은 반대, 싫은 날씨엔 덜 움직이고 좋은 날씨엔 활발하다. **시간·날씨를 안 타는 종은 예전 배회 그대로다**
(보통 활동도 0.6 = 쉬기 1.2~4.5초 · 속도 ×1). 몸의 위치·속도·쉬는 시간만 바꾼다 — 날갯짓 같은 모델 애니메이션은 visual-dev 영역이다.
섬 수입과 무관하고, 남의 섬·꿈 섬에서도 같다(꿈은 늘 맑은 한낮이라 자연히 낮의 기분이다).

## 검증

```
python -X utf8 .claude/scripts/economy_sim.py      # 신호 6·7: 섬 수입 대역 (손님·기분은 바꾸지 않는다)
python -X utf8 .claude/scripts/quest_lint.py       # 섬 퀘스트 7개(QuestType 6종)
-testPlatform PlayMode -testFilter InsectGame.Tests.IslandGuestRulesTests
-testPlatform PlayMode -testFilter InsectGame.Tests.IslandInsectMoodTests
```

- 화면: QA 빌드 `-battleScenario island-ui`(데스크톱 1280×720 + 세로 720×1280). 저장을 건드리지 않는 메모리 fixture다.
- 모양: `Assets/Editor/IslandModelCapture.cs` — PlayScene 없이 지형 3단계와 물건 28종을 찍는다.

```
"$UNITY_EDITOR_PATH" -batchmode -projectPath "C:/Project/곤충게임" -logFile .claude/cache/island-model.log \
  -executeMethod InsectGame.EditorTools.IslandModelCapture.Run -islandOut .claude/cache/island-model
```
