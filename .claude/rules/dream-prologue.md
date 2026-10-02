---
description: 「챔피언의 꿈」 프롤로그 — 꿈 밖을 건드리지 않는 약속, 표지(DreamPrologueState)를 읽어야 하는 자리, 샌드박스 전투
---

# 「챔피언의 꿈」 프롤로그 규칙

새 계정이 처음 필드에 서면(첫 퀘스트 `q_move`가 진행 0으로 활성) 1.5초 뒤 한 번 도는 연출이다. 설정의 「챔피언의 꿈 다시 보기」로
언제든 다시 본다. 흐름은 **챔피언전 → 챔피언의 섬 → 깨어남**이다. 지휘는 `DreamPrologueDirector`(World/), 저작 데이터는
`DreamPrologueData`, 시작 조건·표지는 `DreamPrologueState`.

## 약속 하나: 꿈이 끝났을 때 이 계정의 어떤 기록도 시작 전과 같다

꿈속의 전투·걸음·곤충은 퀘스트·스토리·재화·도감·컬렉션·섬 어디에도 남지 않는다. `DreamPrologueIntegrationTests`가 실제 PlayScene에서
꿈을 끝까지 돌리고 그 전후를 비교한다 — **이 테스트가 이 규칙의 단일 출처다.** 실패는 전부 조용하다(예외도 경고도 없이 챔피언전 승리가
"첫 전투!"를 깨거나, 섬 걷기가 "첫 걸음!"을 채운다).

## `DreamPrologueState.Active`를 읽어야 하는 자리

켠 쪽(디렉터)이 끌 길이 여럿이라(끝까지·건너뛰기·오류·씬 재로드) 이벤트가 아니라 정적 표지 하나다. 읽는 자리:

| 자리 | 하는 일 |
|---|---|
| `TutorialQuestManager` (`NotifyAction`·`NotifyCapture`·`Update`) | 꿈속의 행동·걸음을 세지 않는다 |
| `StoryObjectiveTracker.TryAutoStartFirstObjective` | 꿈속에서 본 어르신을 향해 자동 주행하지 않는다 |
| `PlayerStatusHUD`·`QuickAccessBarUI`·`KeyGuideHUD`·`MinimapUI`·`TutorialQuestUI` | 필드 HUD를 숨긴다 |
| `AccountSettingsUI` | 설정 버튼을 숨긴다 |
| `OpeningReplayCoordinator.CanReplay` | 꿈 도중에 오프닝 다시보기를 막는다 |

**필드 위에 그리는 HUD를 새로 만들면 이 표지를 확인할 것** — 안 하면 꿈속에 그 HUD가 비치고, 버튼이면 꿈 밖 메뉴가 열린다.
(모달 레지스트리로 대신 못 하는 이유: 모달이 열려 있으면 `PlayerMovement`가 이동 입력을 버린다. 섬 단계는 걸어 다녀야 한다.)

## 샌드박스 전투

`InsectBattleController.StartSandbox`가 연다. 월드 개체 없이 데이터만으로 붙고, **보상·도감·HP 저장·포획 롤·`DuelEnded`가 전부 없다.**
규칙(`SandboxBattleRules`)은 공식이 낸 피해에 배율과 상하한만 건다 — 반드시 이기고(내 곤충 HP 하한 30%) 3~4번째 행동에 끝난다.

- **`BattleEnded`는 그대로 쏜다**(결과 화면이 그 신호로 뜬다). 대신 구독자 중 이야기를 움직이는 쪽(`StoryDirector.OnBattleEnded`)이
  `IsSandbox`를 보고 물러난다. 퀘스트는 위 표지로 막힌다. **`BattleEnded`를 듣는 새 코드를 쓰면 샌드박스를 어떻게 다룰지 정할 것.**
- `IsSandbox`는 **다음 전투를 시작할 때까지 값을 지킨다**(`BattleEnded` 시점에도 유효). `BeginBattleCommon`이 매번 끈다 —
  표지가 남으면 이후 모든 전투가 보상 0·`BattleWin` 없음이 된다(`NextOrdinaryBattle_DoesNotInheritTheSandboxFlag`).
- 실제 DB로 길이를 잰다(`RealSandboxBattle_WinsInThreeToFourActions_WithoutFalling`) — 곤충 능력치·기술이 바뀌어도 규칙이 길이를 지킨다.

## 섬

꾸며진 섬은 **남의 섬 방문(`EnterVisit`)의 스냅샷**이다. 새 지형 코드가 없고 저장도 안 건드린다. `IslandWorldBuilder.DreamMode`가 켜지면
섬 HUD가 숨고 방문 통지(퀘스트)가 가지 않는다. 배치(`DreamPrologueData.IslandObjects`)를 고치면 겹침·경계·입구→분수 통로가 테스트로 잡힌다 —
**정리(`SanitizeSnapshot`)에서 물건이 조용히 빠지는 것**이 실제 위험이다(분수 없는 분수 광장).

## 종료는 한 곳을 지난다

`DreamPrologueDirector.Finish`가 표지·섬·프리즈·BGM·"봤다" 기록을 한꺼번에 정리한다. OnDisable/OnDestroy도 같은 곳을 지난다.
끝까지 보았거나 건너뛰었을 때만 "봤다"(`PlayerPrefs`, 계정 스코프)를 적는다 — 오류·씬 재로드로 끊긴 것은 다음에 다시 보여 준다.

## 검증

```
# 규칙·데이터·샌드박스·실제 DB (36건) + 실제 PlayScene 통합 (1건, 약 45초)
-testPlatform PlayMode -testFilter InsectGame.Tests.DreamPrologueTests
-testPlatform PlayMode -testFilter InsectGame.Tests.DreamPrologueIntegrationTests
```

화면은 QA 빌드로 본다(IMGUI라 배치 캡처에 안 잡힌다): `-battleScenario dream-island`(도입·섬·안내·깨어남), `dream-battle`(챔피언전 안내·결과).
가로(1280×720)와 세로(720×1280)를 둘 다 찍을 것 — 세로에서는 전투 HP 카드가 폭을 다 써서 안내 위치가 갈린다(2026-10-03).
