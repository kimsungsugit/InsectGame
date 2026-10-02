---
description: 테스트 프레임워크, 컨벤션, 필수 기준
---

# 테스트 규칙

## 프레임워크
- NUnit (`using NUnit.Framework`)
- 테스트 파일 위치: `Assets/Tests/EditMode/`

## 러너는 PlayMode다 (폴더 이름에 속지 말 것)

폴더 이름은 `EditMode`지만 **EditMode 러너로는 0건이 잡힌다.** 이 프로젝트엔
`.asmdef`가 하나도 없어서 테스트가 별도 에디터 테스트 어셈블리가 아니라
런타임 어셈블리(`Assembly-CSharp`)로 컴파일되고, EditMode 러너는 그걸 보지 못한다.

```
-testPlatform PlayMode -testFilter InsectGame.Tests
```

`-testPlatform EditMode`를 쓰면 **0건을 실행하고 "성공"이라 보고한다.** 실행 개수를
반드시 확인할 것 — 2026-08-23 기준 `[Test]` 메서드 **592개**, 러너가 실제 실행하는 케이스 **704개**다
(`[TestCase]` 파라미터화가 여러 케이스로 펼쳐진다). 단일 출처는 코드다 —
`grep -c "\[Test\]" Assets/Tests/EditMode/*.cs`의 합과 TestResults.xml의 `total`.
문서에 박아둔 숫자는 늘 낡는다(실제로 62로 적혀 있다가 147까지 벌어져 있었고, 547/628도 곧 낡았다).
0건 보고는 통과가 아니라 실패다.

**`-runTests`에 `-quit`를 같이 붙이지 말 것.** 붙이면 Unity가 테스트를 시작하기 전에 종료하는데
**exit 0에 `Exiting batchmode successfully now!`까지 찍어** 성공처럼 보이고, `TestResults.xml`은
아예 쓰이지 않는다. 이전 실행의 파일이 남아 있으면 그 낡은 `total`을 이번 결과로 착각하기 딱 좋다
(2026-08-03에 실제로 그렇게 254/254를 잘못 읽었다). 테스트 러너가 스스로 종료하므로 `-quit`은 불필요하다.

그래서 결과는 **두 가지를 함께** 봐야 한다 — `total`뿐 아니라 `TestResults.xml`의 **mtime이
이번 실행 시각인지**. 확실히 하려면 실행 전에 기존 파일을 치워 없는 상태에서 시작한다.

EditMode 러너를 되살리려면 `Assets/Scripts`·`Assets/Editor`·`Assets/Tests`에 asmdef를
도입해야 한다(asmdef는 `Assembly-CSharp`를 참조할 수 없어 게임 코드 쪽도 함께 필요).
출시 후 별건.

## 테스트 파일은 반드시 `#if UNITY_EDITOR`로 감쌀 것 (안 그러면 APK/AAB 빌드가 깨진다)

`.asmdef`가 없어 테스트가 `Assembly-CSharp`(런타임 어셈블리)로 컴파일되므로, 가드 없이 두면
테스트의 `nunit.framework` 참조가 IL2CPP 플레이어(APK/AAB) 빌드로 **새어 나가 링크가 실패한다**
(`Mono.Cecil.AssemblyResolutionException: Failed to resolve assembly: 'nunit.framework'`).

그래서 모든 EditMode 테스트 `.cs`는 **첫 줄 `#if UNITY_EDITOR`, 마지막 줄 `#endif`로 파일
전체를 감싼다.** 에디터 PlayMode 러너에선 `UNITY_EDITOR`가 정의돼 전부 그대로 돌고, 기기 빌드에선
통째로 제외된다. 기존 파일엔 이미 이 가드가 있으니 **새 테스트 추가 시 빠뜨리지 말 것** —
2026-07 실제로 새 테스트 4개가 가드 누락으로 APK 빌드 링크를 멈췄다(그 4개만으로 전체 빌드 실패).

## 컨벤션
- 클래스: `[TestFixture]` 어트리뷰트
- 메서드: `[Test]` 어트리뷰트
- 네이밍: `MethodOrProperty_Condition_ExpectedResult` (예: `Player_MaxIV_Is15`)
- 네임스페이스: `InsectGame.Tests`

## Assert 패턴
- `Assert.AreEqual(expected, actual)` - 값 비교
- `Assert.IsTrue()` / `Assert.IsFalse()` - 불리언
- `Assert.IsNotNull()` - null 체크
- `Assert.Greater()` / `Assert.GreaterOrEqual()` - 범위 검증

## 테스트 필수 기준
다음 변경 시 반드시 테스트를 추가하거나 갱신:
- **수치 공식 변경**: 데미지, 포획률, IV, 스탯 계산 등 수학 공식
- **데이터 모델 변경**: 세이브/로드에 영향을 주는 필드 추가/삭제
- **GameConstants 상수 변경**: 밸런스에 영향을 주는 상수
- **새 시스템 추가**: 핵심 로직에 대한 단위 테스트 (UI 제외)

## 테스트 제외 대상
- OnGUI 렌더링 코드 (IMGUI는 렌더 루프 없이 검증 불가)
- MonoBehaviour 생명주기 의존 로직 (`[UnityTest]` + `yield`가 필요. 현재 테스트는 전부
  씬 없이 도는 순수 로직 `[Test]`다)
- 외부 서비스 호출 (Firebase, Firestore)

## 3D 화면은 눈으로 확인한다 — `LiveSceneCapture`

위 제외 대상 중 **월드에 보이는 것**(모델·애니메이션·지형·배치·연출)은 테스트 대신
실제 화면을 찍어서 본다. `Assets/Editor/LiveSceneCapture.cs`가 배치모드로 PlayScene을
띄우고 카메라를 렌더해 PNG로 남긴다.

```
"$UNITY_EDITOR_PATH" -batchmode -projectPath "C:/Project/곤충게임" \
  -logFile .claude/cache/capture.log \
  -executeMethod InsectGame.EditorTools.LiveSceneCapture.Run \
  -captureOut .claude/cache/capture -captureTimes 2.0,4.0 \
  -captureSize 900x700 -captureOffset 0,1.2,-2.6 -captureLook 0,0.85,0
```

인자는 전부 선택이다(`-captureTarget`은 따라갈 오브젝트 이름, 기본 `Player`,
`none`이면 게임 카메라 구도 그대로). 결과는 로그의 `[CAPTURE]` 줄로 확인하고,
종료 코드는 요청한 장수를 다 찍었을 때만 0이다.

**정지 화면으로는 애니메이션을 못 본다** — 여러 시각을 찍어 픽셀 차분을 낸다.
플레이어 idle 호흡을 이 방법으로 확인했다(2초 간격 3장, 차이가 플레이어 영역에만 몰림).

### 필드 전체는 `FieldDesignTour`로 한 번에 돈다

`LiveSceneCapture`는 한 실행에 한 자리다. 지형·건물·NPC 옷처럼 **월드 전체에 걸친 변경**은
`Assets/Editor/FieldDesignTour.cs`가 실제 PlayScene을 띄워 본 마을·리전 13곳·전초기지·서브에리어 26곳
(필드 입구 + 내부)·NPC 전원을 한 실행(약 3분, 370장)에 찍는다. 장소마다 `game`(실제 게임 카메라)·
`wide`(조감)·`eye`(눈높이) 세 구도다 — 게임 카메라는 (0,9,-6) 고각이라 세로로 선 것의 모양이 거의 안 보인다.

```
"$UNITY_EDITOR_PATH" -batchmode -projectPath "C:/Project/곤충게임" -logFile .claude/cache/tour.log \
  -executeMethod InsectGame.EditorTools.FieldDesignTour.Run -tourOut .claude/cache/tour \
  [-tourOnly world,village,regions,outposts,subareas,npcs,sky] [-tourFilter dunes,canopy]
```

전후를 같은 도구로 찍어야 비교가 된다(조명·시각·구도가 같다). `sky`는 공중·거대 렌더러를 로그로 센다 —
2026-09-28에 하늘의 회색 원반(납작한 구름)과 리전을 덮던 올리브색 돔(1.5배 확장 전 좌표의 "먼 산")을 이걸로 찾았다.
NPC는 컬링과 주변 소품을 피해 먼 무대로 같은 프레임 안에 옮겨 찍고 되돌린다.

**NPC가 새까만 실루엣으로 찍히면 역광이 아니라 환경광 결함을 의심할 것.** 이 씬엔 라이팅 데이터가 없어
Skybox 환경광 프로브가 0이었다 — 역광 면이 완전 검정이 되는 건 그 때문이었고 지금은 Trilight로 고쳤다.

### 걸어서 못 나가야 하는 경계는 실제 씬에서 한 바퀴 잰다 — `FieldFenceIntegrationTests`

초원은 목장 울타리(기둥 + 가로대)로 빈틈없이 둘러 **그려지는데**, 가로대가 합친 메시라 콜라이더가 없어서 기둥 사이
46칸 전부(칸당 6.5m)를 걸어 나갈 수 있었다. 겹친 습지 쪽은 마지막 기둥과 습지 잠금 원 사이에 3.6m 틈도 있었다(2026-10-02 기기 보고).
그림과 통행이 따로 노는 결함은 캡처(막힌 것처럼 보인다)로도 순수 로직 테스트(콜라이더가 없다)로도 안 잡힌다.

`FieldFenceIntegrationTests`가 실제 PlayScene에서 울타리 줄을 0.2° 간격으로 돌며 게임의 진짜 이동 차단
(`PlayerMovement.IsBlockedPosition`)을 불러 열린 구간을 센다 — **통로 하나만** 열려 있어야 한다. 다른 리전의 원 안은
잠긴 것으로 친다(이 PC의 PlayerPrefs에 해금 기록이 있어도 새 게임 상태를 재도록). 열린 구간은 로그의 `[FENCE]` 줄에 남는다.

차단은 난간 칸마다 얇은 `BoxCollider`다(`RegionTerrainBuilder.AddRailBlocker`). **레이어가 Ignore Raycast(2)다** —
겹침 검사(`OverlapSphere`, 기본 전 레이어)에만 걸리고 레이·구 캐스트(카메라 차폐·탭 이동·곤충 탭·접지)는 건너뛴다.
보이지 않는 벽을 기본 레이어에 세우면 남쪽 울타리 앞에서 카메라가 3.5m로 당겨진다.

### 대사가 실제로 뜨는지는 `StoryBeatWalkthrough`로 본다


캡처 도구의 첫 번째 한계(IMGUI 미포착)가 정확히 스토리 대사를 덮는다 — 비트가 발화하면
`NpcDialogueUI`가 `OnGUI`로 그리므로 **화면으로는 확인할 방법이 없다.** 그런데
`story_lint`도 못 본다: 그쪽은 Story.json을 **정적으로** 읽어 게이트·참조 무결성만 본다.
발화는 트리거 이벤트·prereq 열람·리전 게이트·우선순위 비교·`pendingBeatId` 잠금·
미뤄 둔 트리거 큐가 **런타임에** 맞물린 결과라, 하나만 어긋나도 **대사가 그냥 안 뜬다**
(예외도 경고도 없다).

그래서 대사창 대신 **발화 자체**를 본다. `Assets/Editor/StoryBeatWalkthrough.cs`가
`StoryBeatTriggered`를 구독한 채 게임의 실제 진입점(`OnNpcTalked`·`AddCapturedInsect`·
`BattleEnded`·`CleanseByBoss`)을 순서대로 두드리고, 뜬 대사는 `NpcDialogueUI.CloseModal`로
닫는다(닫지 않으면 `DrainPendingTriggers`가 모달 가드에 막혀 **다음 비트가 영영 안 온다**).

```
"$UNITY_EDITOR_PATH" -batchmode -projectPath "C:/Project/곤충게임" \
  -logFile .claude/cache/story-walk.log \
  -executeMethod InsectGame.EditorTools.StoryBeatWalkthrough.Run \
  -walkOut .claude/cache/story-walk.md [-walkRegion mountain] [-walkMode campaign] [-walkChoice last]
```

거점 목록을 박아두지 않는다 — `RegionData.HasBlightSite`를 런타임에 훑으므로 거점을
늘리면 걸음도 저절로 는다. 종료 코드는 모든 걸음이 통과했을 때만 0이고, 보고서에
**실제 발화 순서**가 남는다. 선행 비트만 `CompleteBeat`로 채우고(검증 대상이 아니다)
무엇을 채웠는지 보고서에 적는다.

**모드가 둘이다 — 기본값만 돌리면 본편은 한 걸음도 안 걷는다.**

| `-walkMode` | 걷는 것 | 두드리는 트리거 |
|---|---|---|
| `blight` (기본) | 오염 거점 아크 `bl_*` | `NpcTalk` · `CaptureInsect` · `BattleWin` · `RegionCleansed` |
| `campaign` | 1막 본편 + 꽃밭 | 위 + **`SubAreaEnter`** · **`GuardianDefeat`** |
| `town` | 마을 이야기 `town_*` + 지역 의뢰 `s_town_*` + 따라가기 | `NpcTalk` · `CaptureInsect` · `BattleWin` + **퀘스트 통지**(`NotifyCapture`) |

**`town`은 스토리만 보지 않는다** — 이 연작의 결함은 퀘스트·따라가기와의 이음매에서 조용히 나서, 한 편마다
리전 밖 의뢰 행동이 **안 세어지는지**, 의뢰 완료가 매듭의 `requiredQuestId`를 여는지, 따라가기가 `[의뢰]` →
`?`·"알리기" → (대기면) 자동 해제로 바뀌는지를 함께 본다. 배치엔 로그인이 없어 퀘스트 세션이 꺼져 있으므로
세션·선행 퀘스트·리전 해금을 인메모리로 채우고 보고서의 「미리 채운」 칸에 적는다(`quest:`·`region:` 접두).

**선택지가 뜨면 도구가 고른다** — 기본은 첫 항목, `-walkChoice last`면 마지막 항목. 선택 결과는
`Immediate` leaf라 고르는 순간 큐 맨 앞에서 뜬다(`StoryBible.md` 6장 「선택지 규칙」). 최종장
`fin_unnamed`의 거절·수락 양쪽이 실제로 뜨는지는 `campaign`을 **두 번** 돌려야 본다.

`SubAreaEnter`와 `GuardianDefeat`는 **본편에서 가장 많이 쓰는 두 트리거인데 오래 사각지대였다** —
거점 아크가 둘 다 안 쓰는 탓에 구동부 자체가 없었다. 2026-08-26에 붙였다.

- **서브에리어는 세 걸음이다** — 근접 → `[E]` 진입 → 이탈. `RequestEnterSubArea`는
  `nearbySubArea`가 차 있어야 하고 그건 `RegionManager.Update`가 **위치로만** 채운다.
  **이탈을 빠뜨리면 그 뒤가 전부 죽는다**: 진입이 sticky를 켜고 플레이어를 (2000,0,2000)으로
  옮기는데, sticky 동안 `Update`가 위치 판정을 건너뛰어 다음 리전 이동이 영영 성립하지 않는다.
- **`GuardianDefeat`는 리전당 일생 1회다**(`DefeatGuardian`의 idempotent 가드). 두 번째
  실행에서 `gd_*`가 "발화 없음"으로 잡히면 결함이 아니라 격파 기록이 남아서다 —
  그래서 `ResetProgress`가 `InsectGame.DefeatedGuardians`도 함께 지운다.
- **걷는 순서가 곧 여운 체인 순서다.** 여운은 같은 NPC의 직전 여운을 prereq로 물기 때문에
  연못(ch2) → 숲(ch3) → 습지(ch4) → 산(ch5) → 유적(ch6) 차례로 걸어야 하나씩 열린다.

읽을 때 헷갈리는 것: **한 걸음에 시도가 2회로 찍히는 게 정상일 수 있다.** 같은 트리거에
본편 비트가 함께 자격을 가지면 챕터 우선순위가 이겨 그쪽이 먼저 나간다(산에서 포획하면
`ch5_thesis`가 `bl_mountain_sign`보다 먼저, 유적에서 이기면 `ch6_approach`가 먼저다).
플레이어도 실제로 두 번 해야 한다 — 결함이 아니라 저작 순서다.

### 스토리 영상(mp4)은 기기로만 본다

`StoryVideoDirector`의 재생은 `VideoPlayer` 디코더에 달려 있어 **배치모드·PlayMode 러너로는
검증할 수 없다**(디코더가 없거나 렌더 대상이 없다). 테스트(`StoryVideoLibraryTests`)는 저작(길이
상한·자막 큐·금칙)만 고정하고, `story_lint` 검사 25가 ID·switch·파일 배치를 본다. 실제 재생·
건너뛰기·조작 복구는 Android 기기에서 확인한다 — 절차는 `Docs/StoryVideos.md`.

### 반투명·발광이 빌드에서 사는지는 QA 빌드로 잰다 — `-battleScenario materials`

에디터와 배치 캡처에는 셰이더 변형이 전부 있어서 **절대 틀리지 않는다.** 결함은 플레이어 빌드가 Standard의
`shader_feature`(반투명 `_ALPHABLEND_ON`·발광 `_EMISSION`)를 걸러낼 때만 난다 — 2026-09-29까지 빌드엔 그 변형이 0개라
물·얼음·유리·안개가 불투명, 등불·발광 소품이 무발광으로 그려졌다. `BattleVisualCaptureBuilder.Build`로 QA 빌드를 만든 뒤:

```
Builds/Windows/BattleVisualQA/BattleVisualQA.exe -battleCaptureOut <새 빈 폴더> -battleScenario materials   -screen-fullscreen 0 -screen-width 1280 -screen-height 720
```

줄무늬 벽 앞 구의 (r−b) 편차와 발광 휘도차를 재서 README에 PASS/FAIL을 적는다(안개 Exp2 판 포함, 종료 코드 0/5).
빌드 로그의 `[ShaderVariants]` 줄이 패스별 변형 수를 키워드별로 센다. 고치는 곳은 `SceneryMaterials.BuildKeepers`
(Resources 머티리얼) — **Standard를 Always Included에 넣으면 이 방법이 무력해진다**(그 목록은 머티리얼 키워드를 안 본다).

### 한계 셋 (전부 실측)

- **IMGUI는 안 잡힌다.** `OnGUI`는 카메라를 거치지 않는다 — 상점·대화창·배틀 UI·HUD는
  이 도구로 검증할 수 없다. 필요하면 스탠드얼론 빌드에서 `ScreenCapture`를 써야 한다.
  이미 그렇게 찍는 검수 시나리오가 있다(`BattleVisualCaptureBuilder.Build` → `BattleVisualQA.exe
  -battleCaptureOut <빈 폴더> -battleScenario <이름>`, 데스크톱 1280×720과 세로 720×1280 둘 다 찍을 것):
  `insect-ui`(도감·보유 곤충·퀵바), `field-ui`(필드 HUD·포획 선택/채집망/출전/성공·실패 팝업·배틀팀·훈련소 —
  훈련소는 실제 부트스트랩 생성 함수로 방식·기술을 만들어 **가격이 실값**이다),
  `outfit`(의상 창·캐시샵·캐릭터 생성), `island-ui`(나의 섬 — HUD·꾸미기·상점·곤충·방문·가이드. 진짜 `IslandWorldBuilder`가
  섬을 짓는다), `badge`, `map`, `story`. 전부 저장을 부르지 않는 메모리 fixture다.
  **검수 빌드는 실제 게임과 같은 저장 폴더를 쓴다** — fixture가 재화를 건드리는 동작(구매·수확)을 부르면 이 PC의 진짜
  세이브를 덮는다. 지갑·캔디는 차감 즉시 파일에 쓰므로 부르지 않고, 섬 매니저는 `PersistenceEnabled`를 끈다.
- **`ScreenCapture.CaptureScreenshot`은 배치모드에서 조용히 실패한다**(게임뷰가 없다).
  그래서 이 도구는 카메라 → `RenderTexture` → `ReadPixels` 경로를 쓴다.
- **Unity 에디터를 열어두면 이 도구가 못 돈다.** 프로젝트가 `Temp/UnityLockfile`로 잠겨
  두 번째 인스턴스가 뜨지 않는다. 배치모드 검증 중에는 에디터를 닫아 둔다.
