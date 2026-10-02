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

- 받은 스냅샷은 **남이 만든 데이터**다 — `IslandSaveRules.SanitizeSnapshot`이 모르는 물건·경계 밖·겹침을 버린다.
- 올리는 건 `BuildSnapshot`의 축약본뿐이다(instanceId·재화 없음).
- 계정 삭제는 Auth 계정을 지우기 **전에** `deleteIsland`를 부른다(`AuthManager.DeleteAccountCoroutine`) — 지운 뒤엔 토큰이 없다.

## 화면

- `IslandHudUI`는 **비모달**이다 → 매 OnGUI `FieldHudInput.RegisterBlockingRect`(rules/ui-layout.md).
  모바일에서는 좌하단 사분면(가상 조이스틱 시작 영역)에 버튼을 두지 않는다.
- `IslandEditUI`는 **모달**이다. 지면 탭이 「칸 고르기」인데 같은 탭을 `PlayerMovement`가 클릭-이동으로도 읽기 때문이다.
  그 위에 뜨는 안내 배너는 자기 자리를 `FieldHudInput`에 등록하고, 꾸미기 화면이 그걸 보고 탭을 양보한다
  (OnGUI 호출 순서가 정해져 있지 않아 먼저 도는 쪽이 탭을 먹는다).
- 섬 화면은 이벤트를 구독하지 않고 상태를 읽는다 — UI 루트가 꺼졌다 켜질 때 구독이 사라지는 계열(subscription_lint)을 피한다.

## 검증

```
python -X utf8 .claude/scripts/economy_sim.py      # 신호 6·7: 섬 수입 대역
python -X utf8 .claude/scripts/quest_lint.py       # 섬 퀘스트 7개(QuestType 6종)
```

- 화면: QA 빌드 `-battleScenario island-ui`(데스크톱 1280×720 + 세로 720×1280). 저장을 건드리지 않는 메모리 fixture다.
- 모양: `Assets/Editor/IslandModelCapture.cs` — PlayScene 없이 지형 3단계와 물건 28종을 찍는다.

```
"$UNITY_EDITOR_PATH" -batchmode -projectPath "C:/Project/곤충게임" -logFile .claude/cache/island-model.log \
  -executeMethod InsectGame.EditorTools.IslandModelCapture.Run -islandOut .claude/cache/island-model
```
