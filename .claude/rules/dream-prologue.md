---
description: 「챔피언의 꿈」 프롤로그 — 꿈 밖을 건드리지 않는 약속, 표지(DreamPrologueState)를 읽어야 하는 자리, 샌드박스 전투
---

# 「챔피언의 꿈」 프롤로그 규칙

새 계정이 처음 필드에 서면(첫 퀘스트 `q_move`가 진행 0으로 활성) 1.5초 뒤 한 번 도는 연출이다. 설정의 「챔피언의 꿈 다시 보기」로
언제든 다시 본다. 흐름은 **경기장 입장 영상 → 챔피언전 → 챔피언의 섬 → 깨어남**이다. 지휘는 `DreamPrologueDirector`(World/),
저작 데이터는 `DreamPrologueData`, 시작 조건·표지는 `DreamPrologueState`, 도입 영상 재생기는 `DreamIntroVideo`.

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
| `PlayerStatusHUD`·`QuickAccessBarUI`·`KeyGuideHUD`·`MinimapUI`·`TutorialQuestUI`·`WorldClockHUD` | 필드 HUD를 숨긴다 |
| `SubAreaEnvironment`·`WeatherEffects` | 낮밤·날씨 하늘 보정과 날씨 입자를 끈다 — 꿈은 늘 맑은 한낮이다(`rules/world-environment.md`) |
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
- **결과 화면은 샌드박스만 저절로 닫힌다**(4초 — `BattleResultRules.ShouldAutoClose`). 다른 전투는 탭·Space/Enter로만 닫힌다. 지휘자가 결과 화면이
  닫히는 것(`IsBattleActive`)을 신호로 섬에 넘어가고 상한이 14초라, 이 예외를 지우면 결과 화면이 떠 있는 채로 섬이 열린다(`Screen_DreamChampionResult_ClosesByItself`).

## 섬

꾸며진 섬은 **남의 섬 방문(`EnterVisit`)의 스냅샷**이다. 새 지형 코드가 없고 저장도 안 건드린다. `IslandWorldBuilder.DreamMode`가 켜지면
섬 HUD가 숨고 방문 통지(퀘스트)가 가지 않는다. 배치(`DreamPrologueData.IslandObjects`)를 고치면 겹침·경계·입구→분수 통로가 테스트로 잡힌다 —
**정리(`SanitizeSnapshot`)에서 물건이 조용히 빠지는 것**이 실제 위험이다(분수 없는 분수 광장).

## 도입 영상 — 경기장 입장 8초

처음엔 검은 화면에 글자 카드뿐이었다(3.6초). 게임을 켜고 처음 보는 연출이라 **영상으로 바꿨다**: 선수 입장 터널을 걷는 챔피언의
뒷모습 → 흰 섬광 → 환호하는 관중석 → 서치라이트 아래 마주 선 파트너와 상대 → 맞대결 번쩍(6.1초) → 암전. 이어서 기존 글자 카드
(「챔피언 결정전」·이름·vs 상대)가 2.4초 뜨고 챔피언전이 열린다.

| 파일 | 만드는 스크립트 |
|---|---|
| `StreamingAssets/Video/dream_arena.mp4` (1280×720·24fps·무음·H.264 Main) | `python -X utf8 Tools/Video/dream_arena.py` |
| `Resources/Audio/Dream/dream_arena.wav` (44.1kHz 스테레오·8초) | `python -X utf8 Tools/Video/dream_arena_audio.py` |

- **오프닝 프롤로그와 같은 방식이다 — 무음 영상 + 별도 음원을 한 시계로 튼다.** 영상이 못 떠도 시계·소리가 어긋나지 않고,
  건너뛰기가 시계 하나로 끝난다. 시각표(`T_TOTAL`·`TUNNEL_END`·`FACEOFF`…)의 단일 출처는 영상 스크립트이고 음원 스크립트가 모듈로 읽는다.
  게임 쪽 `DreamPrologueData.IntroSeconds`는 그 길이의 사본이라 `DreamIntroVideoTests`가 mp4·wav **헤더**로 맞춰 본다(디코더 불필요).
- **영상이 못 뜨는 모든 길이 글자 카드로 모인다.** `DreamIntroVideo`의 `Failed`/`Ended` 둘뿐이다 — 파일 없음·디코더 오류·준비 3초 초과·
  재생 중 1.2초 멈춤. 꿈은 영상 없이도 선다(이때는 예전처럼 페이드 0.8초 → 카드 2.8초).
- **세로 화면은 16:9 마스터를 가운데 cover-crop한다**(약 32%만 보인다 — 가로 폭의 56%가 아니다). 맞대결 쌍(뿔 ↔ 머리)을
  **화면 한가운데**에 두었다. 챔피언은 곁가지라 세로에선 잘려도 이야기가 선다. 배치를 바꾸면 세로 크롭부터 확인할 것.
- 영상에 글자를 굽지 않는다(카드는 게임이 얹는다). 재생 중 월드 BGM은 `SetBgmDuck`으로 낮추고 `Stop`이 원복한다 —
  `Finish`가 `Stop`을 부르므로 어떤 종료 길이든 더킹이 남지 않는다.
- 실기기 확인 대상: 디코더(준비 시간·첫 프레임)·소리 길이·세로 크롭. 배치모드·PlayMode 러너는 영상을 못 튼다(`rules/testing.md`).
  Windows QA 빌드는 튼다 — `-battleScenario dream-island`의 첫 장면이 실제 영상이다.

## 종료는 한 곳을 지난다

`DreamPrologueDirector.Finish`가 표지·섬·프리즈·BGM·도입 영상·"봤다" 기록을 한꺼번에 정리한다. OnDisable/OnDestroy도 같은 곳을 지난다.
끝까지 보았거나 건너뛰었을 때만 "봤다"(`PlayerPrefs`, 계정 스코프)를 적는다 — 오류·씬 재로드로 끊긴 것은 다음에 다시 보여 준다.

## 검증

```
# 규칙·데이터·샌드박스·실제 DB (36건) + 실제 PlayScene 통합 (1건, 약 45초) + 도입 영상 파일·헤더 (9건)
-testPlatform PlayMode -testFilter InsectGame.Tests.DreamPrologueTests
-testPlatform PlayMode -testFilter InsectGame.Tests.DreamPrologueIntegrationTests
-testPlatform PlayMode -testFilter InsectGame.Tests.DreamIntroVideoTests
```

화면은 QA 빌드로 본다(IMGUI라 배치 캡처에 안 잡힌다): `-battleScenario dream-island`(도입 **영상**·카드·섬·안내·깨어남), `dream-battle`(챔피언전 안내·결과).
`dream-island`의 `00-video-*`는 **진짜 영상을 실시간으로 틀어** 영상 시각(1.2·2.6·3.5·4.8·6.15·6.5초)에 찍는다 — README 첫 줄이
"played for real"인지 확인할 것(디코더가 없으면 "did NOT play"로 나오고 카드만 찍힌다). 출력 폴더는 **비어 있어야** 한다.
가로(1280×720)와 세로(720×1280)를 둘 다 찍을 것 — 세로에서는 전투 HP 카드가 폭을 다 써서 안내 위치가 갈린다(2026-10-03).
