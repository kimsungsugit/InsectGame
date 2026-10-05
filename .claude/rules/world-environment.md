# 낮·밤과 날씨 규칙

하루 12분의 시계와 다섯 날씨(맑음·비·안개·센바람·눈)가 **하늘과 조명, 곤충의 출현, 곤충의 컨디션**을 바꾼다.
수치는 코드가 단일 출처다(`WorldSkyRules`·`InsectHabits`·`FieldSpawnRules`·`WeatherForecast`) — 여기엔 구조와, 어기면 조용히 깨지는 약속만 적는다.

```
GameClock ──DayPhaseChanged──▶ InsectSpawner(갈아입기) · WorldClockHUD(알림)
WeatherSystem ──WeatherChanged/Blend01──▶ WeatherEffects(입자) · SubAreaEnvironment(하늘) · WorldClockHUD
        └▶ WorldStateProvider.GetWorldState(regionId) ──▶ InsectSpawner(후보·배수·보너스 슬롯) · InsectBattleController(전투 보정) · 습격
```

## 날씨는 세계에 하나, 보이는 건 지역마다 다르다

`WeatherSystem`이 세계 날씨 하나를 돌리고(`WeatherForecast`가 다음·지속을 정한다), **지역 기후**가 보이는 날씨를 바꾼다 —
설산(frostline·mountain)은 비 → 눈, 사막·화산(dunes·emberfall)은 눈 → 센바람(`WeatherForecast.EffectiveIn`).

- **스폰·전투·조명·HUD는 반드시 `GetWorldState(regionId)`(또는 `EffectiveIn`)를 읽는다.** 인자 없는 `GetWorldState()`는 세계 날씨라
  설산에서 "비"로 판정해 눈 입자는 내리는데 곤충은 비 성향으로 움직이는 어긋남이 난다. 섬은 지역이 없어 세계 날씨를 그대로 쓴다.
- `WeatherType`은 **값 순서를 바꾸지 않는다.** `InsectSpawnCondition.allowedWeather`가 정수로 직렬화된다(`Snow`는 맨 뒤에 붙였다).
- 처음 3분은 맑음이다(`FirstClearSeconds`) — 튜토리얼 첫 장면이 비·안개에 묻히지 않게. 저장하지 않는다.
- 검수·꿈·연출이 붙잡을 때는 `GameClock.SetTime01(v, hold)`·`WeatherSystem.SetWeather(w, hold, instant)`를 쓰고 끝나면 `Release()`.

## 하늘 — `SubAreaEnvironment`가 단독으로 쥔다

리전 대기(`RegionAtmosphere`)·서브에리어 프로필의 lerp는 **보정 전 `baseState`**만 만들고, `WriteFinal`이 그 위에 하늘(`WorldSkyRules`)을
얹어 RenderSettings·라이트·카메라 배경에 한 번에 쓴다. **전환 스냅샷은 RenderSettings가 아니라 `baseState`에서 뜬다** — 최종값에서 뜨면
리전 이동 도중 하늘 보정이 두 번 걸린다.

- 보정이 걸리는 곳은 **메인 필드와 나의 섬뿐**이다. 동굴·방(실내)은 안 걸린다. **`DreamPrologueState.Active`(꿈)도 안 건다** — 꿈은 늘 맑은 한낮이다.
  드나들 때는 0.5초에 걸쳐 풀리고 걸린다.
- 섬은 카메라가 SolidColor라 **카메라 배경이 곧 하늘**이다 — 배경색도 보정한다.
- 밤이 어두워 게임이 안 보이면 실패다. 환경광 휘도 바닥(위 0.36·옆 0.29·아래 0.16)을 코드가 강제한다 — `MoonIntensity`와 함께 실기기 밝기로 조정한다.
- 안개 날씨는 11m(카메라~캐릭터)에서 투과율 하한을 지킨다(`RegionAtmosphere`의 `MaxWeatherFogDensity`·하한 상수, 섬은 Exp라 따로). 안개 모드는
  `RegionAtmosphere.FieldFogMode` 상수로만 적는다(`FieldFog_RuntimeUsesTheSharedModeConstant`).
- 스카이박스는 씬의 `Default-Skybox`를 **복제한 머티리얼 인스턴스**로 틴트한다 — 에셋을 고치지 않는다. 날씨 입자는 `Sprites/Default`(Always Included).

## 곤충 성향 — `InsectHabits` + `InsectHabitTable`

종마다 **활동 시간대**(주행성·야행성·무관)·**좋아하는/싫어하는 날씨**·**기질**(온순·습격형)을 한 줄씩 든다. 표에 없는 종은 중립이다.

- **새 곤충을 추가하면 `InsectHabitTable`에 한 줄을 넣는다.** 안 넣어도 게임은 돌지만(중립) `InsectHabitsTests`가 "모든 종이 성향을 가진다"를 잡는다.
- **등급은 시간·날씨와 무관하다.** 등급을 먼저 전역 표로 굴리는 구조(`FieldSpawnRules.PickRarity`)는 그대로고, 성향 배수는 **같은 등급 안에서 종을 고를 때만** 곱한다.
  그래서 `progression_sim`의 리전 income은 바뀌지 않는다.
- **배수는 0이 되지 않는다**(`MinSpawnMultiplier`~`MaxSpawnMultiplier`로 clamp) — 풀이 비면 스폰이 영영 멈춘다. 후보가 비는 시간·날씨 조합이 없다는 걸 테스트가 센다.
- 바람 속성 곤충은 센바람을 좋아하고(부트스트랩이 나비·벌·나방을 거의 다 바람 속성으로 만든다), 설산·산 서식종은 눈을 좋아한다.
  "날아다니는 종은 비를 싫어함"은 종별 예외로만 건다 — 한 날씨에 풀 대부분이 불리해지면 그 날씨가 곤충 없는 시간이 된다.
- 스폰은 `SpawnWeightMultiplier`, 전투 보정은 `BattleStatMultiplier`(아래 「전투 보정」), 습격은 `IsAmbusher`를 쓴다.

## 전투 보정 — `BattleEnvironment`

야생 전투에서 **내 곤충과 적이 각자의 성향으로** ATK·DEF를 유리 ×1.10 / 불리 ×0.90 곱한다(HP는 그대로 — HP바가 튀지 않게).
밤에 야행성 팀을 데려가는 전략이 이걸로 생긴다.

- **제외**: 수문장·NPC 대결(보스 포함)·꿈 챔피언전(샌드박스)·레이드·실내 서브에리어. `BattleEnvironment.Applies`가 단일 출처다.
- **전투를 시작할 때의 하늘로 끝까지 잰다** — 교체로 들어온 곤충도 시작 시점 상태로 잰다(한 전투 안에서 두 하늘이 섞이지 않게).
- `InsectBattleStats.ApplyEnvironment`는 **처음 값을 기준으로 곱한다**(두 번 불러도 겹치지 않는다). 의상·아이템·버프 보너스(`AttackBonus`/`DefenseBonus`)와 섞지 않는다 — 그쪽은 매 턴 다시 계산된다.
- `BeginBattleCommon`이 `PlayerEnvironment`/`EnemyEnvironment`를 `None`으로 리셋한다. 안 하면 다음 전투가 이전 보정 칩을 물려받는다(샌드박스 표지와 같은 계열).
- 전투 화면 칩 문구(`Reason`)는 실제로 배수를 만든 쪽만 적는다 — "밤 · 야행성", "비 · 비를 싫어함".
- 도주 확률의 단일 출처는 `BattleEscapeRules.Chance`다(전투 [도망]과 습격 창의 [도망치기]가 함께 쓴다).

## 스폰 — 보너스 슬롯과 갈아입기

- **보너스 슬롯**(`FieldSpawnRules.BonusSlots`): 밤 +1, 비·안개 +1, 합 최대 +2. `InsectSpawner.RegionCap`이 **보너스를 먼저 더한 합에 오염 감소를 얹는다.**
  반대 순서면 황폐한 리전(8칸→2칸)이 밤·비에 오히려 3~4칸으로 돌아온다. `blight_lint`가 이 경로를 본다.
- **보너스가 걷혀도 눈앞의 곤충은 사라지지 않는다.** `EnsureSlotCount`는 몸이 선 슬롯을 빼지 않고, 상한 초과 슬롯은 수명이 다했을 때 다시 굴리지 않고 비워서 정리한다.
- **갈아입기**: `DayPhaseChanged`가 오면 새 시간대에 어울리지 않는 종의 **만료만** 5~60초로 당긴다(`FieldPopulation.PullExpiryForward`) — 지우거나 바꾸지 않는다.
  교체는 평소 순환이 하므로 플레이어 25m 안 규칙(`CanRotate`)과 스토리 포획 목표종 면제가 그대로 지켜진다. **날씨가 바뀔 때는 갈아입기를 하지 않는다**(시간대만).
- 서브에리어(`TickSubArea`)는 시간·날씨를 보지 않는다.
- **나의 섬도 시간·날씨를 탄다**(세계 날씨) — 밤·비·안개면 같은 보너스 수(`BonusSlots`)만큼 손님 곤충이 찾아오고, 방목 곤충은 성향대로 활발하거나 쉰다.
  규칙은 `rules/island.md` 「손님 곤충」·「방목 곤충의 기분」.

## 습격 — 싸움을 거는 곤충

깨어 있는 습격형(`InsectHabits.IsAmbusher`)은 플레이어를 알아채면 달아나는 대신 **멈칫 → 다가와 → 닿으면 「습격!」 창**을 띄운다.
창에는 [싸우기]·[도망치기] 둘뿐이다. 수치는 `AmbushRules`(순수)가 단일 출처다.

```
InsectEntity(알아챔) ──AmbushGate──▶ CaptureInputController.EvaluateAmbushGate ──▶ AmbushRules.Check(전역 칸은 프레임마다 한 번)
   │ 허가 → 멈칫 → 다가가기(일정 간격으로 길을 잰다)
   ├─AmbushStarted──▶ 잡기 버튼 위 경고 "덤벼든다!"
   └─AmbushReached──▶ (판정 재확인) CaptureChoiceUI.ShowAmbush ──▶ [싸우기] 기존 [B] 배틀·레이드 / [도망치기] BattleEscapeRules.Chance
```

| 자리 | 하는 일 |
|---|---|
| `AmbushRules` | 거절 조건·쿨다운·거리·속도·다가갈 길 고르기·"왜 덤벼들었나" 한 줄 |
| `InsectEntity` | 알아챔·멈칫·접근·닿기·물러나기(AI). 판정은 static 훅 `AmbushGate`로 묻는다 — 풀 객체라 AutoWire가 없다. null이면 습격이 없다 |
| `CaptureInputController` | 훅을 OnEnable/OnDisable에서 세우고 지운다. 교전이 끝난 시각을 적어 쿨다운을 잰다. 닿으면 창을 연다 |
| `CaptureChoiceUI` | 습격 창(분기·그리기). 레이드 술어 `IsRaidRarity`와 `CanFight`를 판정과 함께 쓴다 |

- **성향은 판정하는 그 순간의 상태로 본다** — 플레이어가 있는 리전의 `GetWorldState(regionId)`. 스폰 때 굴린 값이 아니라서 밤이 되면 서 있던 사마귀가 그 자리에서 사나워진다. 스폰 쪽은 바꾸지 않았다.
- **거절 조건**(`AmbushRules.Check` — 순서가 우선순위): 잠잠함(온순·제 시간 아님·싫은 날씨) → 수문장 → 이미 붙잡힘 → 꿈 → 서브에리어(동굴·방은 실내라 시간·날씨를 안 보고, 나의 섬은 집이다 — 밤·비에 찾아오는 손님 곤충도 덤벼들지 않는다) →
  플레이어가 다른 교전 중(선택 창·미니게임·전투·레이드) → 개체 쿨다운 → 전역 쿨다운 → 싸울 곤충 없음 → 멈춤 → 모달.
  **다가오던 곤충은 멈춤·모달(대화·메뉴)에서만 기다리고 나머지에서는 물러난다** — 습격 창·전투도 플레이어를 멈추지만 「교전 중」이 먼저 걸려,
  옆에서 기다렸다가 전투가 끝나자마자 덮치지 않는다. 물러나기는 놓침 도주와 같은 길이다(스포너가 같은 개체를 눈 밖 다른 자리로 옮긴다).
- **싸울 곤충이 없으면 습격하지 않는다**(`AmbushRules.CanFight` — 1v1은 기절 안 한 출전 곤충 하나, 영웅·전설은 5칸이 찬 팀). 이 창엔 그냥 닫는 길이 없어서
  빼면 첫 파트너가 없는 튜토리얼 초반에 닫을 수 없는 창이 뜬다. 판정과 창이 같은 술어를 쓴다 — 레이드 등급은 `CaptureChoiceUI.IsRaidRarity` 하나다.
- **쿨다운**: 전역 60초(습격 교전 — 창에서 이어진 전투·레이드까지 — 이 끝난 뒤) · 다른 교전 뒤 8초(씬이 막 열렸을 때도) · 개체 45초(붙잡혔다 풀려났거나 습격을 접은 몸).
  60초의 근거는 `AmbushRules` 주석이다 — 밤에 쉬지 않고 걸으면 깨어 있는 습격형을 분당 약 1회 만난다(표·로스터·밀도로 잰 값, 기기 실측 아님).
- **도망 규칙의 단일 출처는 `BattleEscapeRules.Chance`**(battle-dev) — 선두 출전 곤충(배틀팀 앞 칸부터 기절 안 한 첫 곤충) 레벨 대 상대 레벨. **한 번만** 굴린다.
  성공하면 창이 닫히고 곤충은 물러난다. 실패하면 「도망치지 못했다!」를 1.1초 보여 준 뒤 싸움으로 넘어간다(1v1은 출전 곤충 고르기, 영웅·전설은 레이드).
- **ESC는 닫기가 아니라 도망치기다.** ESC가 오는 길이 셋(창 자신·`CaptureInputController`·`PlayerMovement`의 모달 ESC)이라 전부 `CaptureChoiceUI.CloseModal`/`RequestClose`로
  모으고 한 프레임에 한 동작만 받는다. **바깥에서 `Hide()`를 직접 부르지 말 것** — 도망 판정을 우회한다. 창이 뜬 뒤 0.45초는 입력을 받지 않는다(걷던 손가락이 버튼을 누르지 않게).
- 포획 버튼이 없다 — 덤벼드는 곤충을 미니게임으로 잡지 않는다. 이기면 전투의 포획 기회가 있다. 다가오는 중에는 [E]로 먼저 말을 걸 수 있다(`CanBeEngaged`의 뜻은 그대로다).
- **곤충은 몸 콜라이더가 없다** — 다가오는 길을 일정 간격으로 몸 높이 스피어캐스트로 재고(도주와 같은 측정), 울타리 난간(Ignore Raycast 층)도 본다.
  막혀서 못 가거나, 너무 멀어지거나, 너무 오래 쫓으면 접는다. 붙잡힌 몸은 지금 자리를 기준으로 삼는다(`RebaseHere`) — 안 그러면 경계 포즈가 스폰 자리로 순간이동시킨다.
- 연출(다가오는 동안 붉은 기운·`!` 표식)은 아직 없다 — 모양·색은 visual-dev 영역이라 `InsectEntity.IsAmbushing`을 읽을 자리로 열어 두었다. 지금은 경계 포즈와 경고 문구뿐이다.
- **습격은 어두울 때만 일어난다**(`InsectHabits.IsAmbushHour` — 저녁·밤). 요청이 "**밤에** 싸움을 거는 곤충"이다. 밤에는 궁합이 유리한 습격형(대부분 야행성)이,
  저녁에는 좋아하는 날씨일 때만 일찍 깬 습격형(해 질 녘 말벌 포함)이 덤벼든다. **아침·낮에는 아무도 싸움을 걸지 않는다.**
  처음엔 시간대 제한 없이 궁합만 봐서 주행성 말벌·사마귀가 한낮에 깨었고 맑은 낮(필드 한 칸의 4.6%)이 맑은 밤(4.1%)만큼, 정원·숲은 오히려 낮이 더 사나웠다(2026-10-03).
- **진짜 DB에서 언제 깨나**(`AmbushRulesTests`가 로그 `[Ambush]`로 남긴다): 밤 평균 약 8%(비·안개 밤 10% 남짓), 저녁은 좋아하는 날씨일 때만, 아침·낮 0.
  `meadow`·`hollow`·`frostline`·`canopy` 풀에는 습격형이 없다 — 시작 리전(초원)은 밤에도 안전하다. 습격을 늘리려면 표(`InsectHabitTable`)와 리전 풀을 고친다.

## HUD — `WorldClockHUD`

우측 열 맨 아래의 칩(시간대·시각·날씨)과 변화 알림. 필드 위에 그리는 HUD라 `FieldHudInput.RegisterBlockingRect`(rules/ui-layout.md)와
`DreamPrologueState.Active` 숨김(rules/dream-prologue.md 표)을 지킨다. 날씨는 **현재 리전 기준**이고, 보이는 날씨가 안 바뀌면 알리지 않는다.
시각은 10분 단위 내림이다(반올림하면 20:55가 "저녁 21:00"으로 보여 시간대 이름과 어긋난다). **섬에서도 보인다**(섬 하늘도 낮·밤·날씨를 따른다) —
지역이 없어 세계 날씨를 쓰고, 모바일에서는 섬 HUD 열과 안 겹치는 자리를 따로 잡는다(`WorldClockRules.IslandMobileChip`). 동굴·방에서는 시각만 보인다.

전투 화면은 각 HP 카드 바로 아래에 보정 칩(`BattleScreenUI.Environment.cs`)을 그린다 — 유리 민트·불리 코랄, 보정이 없으면 자리도 비우지 않는다.

## 검증

```
-testPlatform PlayMode -testFilter InsectGame.Tests.WeatherForecastTests
-testPlatform PlayMode -testFilter InsectGame.Tests.WorldSkyRulesTests
-testPlatform PlayMode -testFilter InsectGame.Tests.InsectHabitsTests
-testPlatform PlayMode -testFilter InsectGame.Tests.WorldClockHudTests
-testPlatform PlayMode -testFilter InsectGame.Tests.AmbushRulesTests
```

화면은 `LiveSceneCapture`로 본다(`rules/testing.md`) — **기본값은 정오·맑음으로 고정된다**(안 그러면 게임 시계가 도는 대로 매번 다른 조명으로 찍혀 전후 비교가 깨진다).
`-captureHour <0~24>` `-captureWeather <clear|rain|fog|wind|snow>`로 고정하고, 게임이 스스로 돌리는 값으로 찍으려면 `natural`을 준다.
두 인자는 **쉼표 목록**도 받는다(촬영 순서대로) — Unity 한 번에 여러 조합을 찍는다(`-captureTimes 12,20,28 -captureHour 12,12,0 -captureWeather snow,rain,rain`).
**첫 촬영은 10초 이후, 간격은 6초 이상.** 촬영 시각은 플레이모드 진입부터 재는데 부트스트랩이 한 프레임에 수 초를 써서, 그보다 이르면
날씨를 건 바로 그 틱에 찍혀 하늘도 입자도 맑음 그대로 나온다(2026-10-03에 "입자가 안 나온다"로 착각했다 — 로그의 `[WX]` 줄이
층마다 켜짐·살아 있는 입자 수를 적으니 그걸 먼저 볼 것). 밤 별은 낮은 구도(`-captureOffset 0,1.2,-6 -captureLook 0,3,8`)로 본다.
`FieldDesignTour`는 `-tourHour`·`-tourWeather`.

입자에서 실제로 고친 것 둘: 눈은 공중 전체 높이에서 생긴다(판에서만 뿌리면 시야까지 내려오는 데 5초가 넘었다), 비는 카메라 둘레 4m를 비운
고리에서 뿌린다(렌즈 코앞을 지나는 빗방울이 화면을 가르는 빛줄기가 됐다).

```
python -X utf8 .claude/scripts/blight_lint.py     # 보너스 슬롯이 오염 상한을 못 풀었는지
python -X utf8 .claude/scripts/data_lint.py
python -X utf8 .claude/scripts/verify_coverage.py
```
