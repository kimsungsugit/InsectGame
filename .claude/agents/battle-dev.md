---
name: battle-dev
description: 배틀 시스템 구현 담당 — 1v1 턴 진행(InsectBattleController), 레이드·유나이트(RaidBattleController), 스킬 효과와 쿨다운, 속성 상성, 전투 스탯 계산. 전투 중 동작이 틀렸을 때 PROACTIVELY 위임. 예 - 패배 후 곤충이 필드에 남는다 / 유나이트 게이지가 안 찬다 / 데미지 표시가 실제와 다르다 / 기절 후 교체가 안 된다. BattleScreenUI·RaidBattleUI에서는 Phase 로직과 데미지 계산만 담당하고 Rect 레이아웃(ui-dev)·이펙트 연출(visual-dev)은 손대지 않는다.
tools:
  - Read
  - Edit
  - Write
  - Glob
  - Grep
  - Bash
---

# 배틀 시스템 에이전트

## 담당 파일

### Battle 모듈 (전체)
- `Assets/Scripts/Battle/InsectBattleController.cs` - 1v1 턴제 배틀 로직
- `Assets/Scripts/Battle/BattleCaptureChanceCalculator.cs` - 1v1 승리 후 포획 확률·롤 판정
- `Assets/Scripts/Battle/SandboxBattleRules.cs` - 샌드박스(챔피언의 꿈) 전투 규칙 — 피해 배율·HP 하한·3~4턴 길이. 컨트롤러 쪽 `StartSandbox`/`IsSandbox`가 이걸 건다
- `Assets/Scripts/Battle/InsectBattleStats.cs` - 스탯 계산/데미지 적용
- `Assets/Scripts/Battle/InsectBattleUIController.cs` - 배틀 UI 브릿지
- `Assets/Scripts/Battle/RaidBattleController.cs` - 5v1 레이드
- `Assets/Scripts/Battle/BattleArenaController.cs` - 배틀 아레나 ※비주얼은 visual-dev
- `Assets/Scripts/UI/BattleScreenUI.Impact.cs` - 1v1 연출용 타격 정보(HitCue: 세기·마무리·빗나감)를 전투 결과에서 만든다 ※연출은 visual-dev
- `Assets/Scripts/UI/RaidBattleUI.Impact.cs` - 레이드 타격 정보·보스 예고 호출·3D 좌표 변환 ※숫자 배치는 ui-dev, 연출은 visual-dev
- `Assets/Scripts/Battle/RaidRoundResolver.cs` - 레이드 동시 라운드 판정(순수 정적). 1v1과 달리 버프 만료가 없다 — 의도된 divergence, 상한은 `MaxBuffStacks`
- `Assets/Scripts/Battle/RaidRoundModels.cs` - 레이드 라운드 결과 모델(순수 데이터). 슬롯 피해 배열에 세터를 만들지 말 것 — 컨트롤러가 따로 합산해 이중 가산이 된다
- `Assets/Editor/LedgerDuelProbe.cs` - 장부 압박이 실제 보스전에서 도는지 배치모드 확인 ※순수부는 테스트가 잡고, **배선 누락은 무증상**이라 이쪽이 필요하다
- `Assets/Scripts/Battle/RaidBossForms.cs` - 체력에 따라 모습을 바꾸는 레이드 보스 표(순수). 바뀌는 건 속성·기술(`CombatData`)뿐, 정체(`Data`)는 그대로 — 이겨도 잡히는 건 원래 종이고 스토리도 원래 종으로 센다
- `Assets/Scripts/Battle/LedgerPressure.cs` - 명부회 보스전 「장부」 압박 순수 계산부 ※임계값(누가 얼마나 빨리 적는가)은 `NpcBossDuels` 표가 든다 — 여기에 인물 ID를 두지 않는다
- `Assets/Scripts/Battle/RaidSupportPlanner.cs` - 비-리더 팀원의 스킬 선택 AI(순수 정적, **난수 미사용** — 동점은 최저 인덱스라 결정론 테스트가 성립한다)
- `Assets/Scripts/Battle/BattleEnvironment.cs` - 낮·밤·날씨 전투 보정 순수부(`BattleEnvironmentNote` 배수·칩 문구, `Applies` 적용 범위). 배수는 `InsectHabits.BattleStatMultiplier`가 단일 출처 ※칩 그리기는 ui-dev
- `Assets/Scripts/Battle/BattleEscapeRules.cs` - 도주 확률 순수 규칙(`Chance`) — 1v1 `TryEscape`와 습격 곤충 도망(capture-dev)이 함께 읽는다
- `Assets/Scripts/Battle/BattleReadPacing.cs` - 읽는 단계(진입 인트로·컷인·상대 교체·그림자 변신·수문장 등장)의 배속 하한(`ClockScale`·`StagingSeconds`)과 결과 화면 닫기 규칙(`BattleResultRules` — 0.6초 잠금·뗄 때 닫기·꿈 챔피언전만 4초 자동). 순수
- `Assets/Scripts/Battle/ElementMatchup.cs` - 상성 등급 `Matchup`(Super·Good·Neutral·Weak·Resisted)과 `Describe(배수)`. 경계 1.05/0.95는 「효과가 굉장했다」 문구와 같은 값. 순수 ※아이용 문구는 ui-dev
- `Assets/Scripts/Battle/SignatureSkills.cs` - 전용기 판별(`IsSignature(owner, skill)`·`IsUnlocked`) — 1v1 표지·레이드 보스 기술 고르기·전용기 연출이 같은 기준. 순수
- `Assets/Scripts/Battle/BattleKind.cs` - 1v1 전투 종류(`BattleKind`·`BattleKinds.Classify`)와 전투 곡 고르기(`BattleMusic` — 수문장 레이드·간부·라온). 순수 ※곡 등록은 visual-dev
- `Assets/Scripts/UI/BattleScreenUI.Flow.cs` - 1v1 화면 흐름 partial — 결과 닫기(`ResultCanClose`·`ResultShownSeconds`)·읽는 단계 시계·진입 인트로(`IntroProgress`·`EntryIntroProgress`)·`CurrentBattleKind` ※그리기는 ui-dev
- `Assets/Scripts/UI/RaidBattleUI.Flow.cs` - 레이드 화면 흐름 partial — 결과 닫기(실제 시간)·인트로/변신 시계·`BossTransformStagingSeconds`·`GuardianIntroClockScale` ※그리기는 ui-dev
- `Assets/Scripts/NPC/NpcDuelController.cs` - 곤충잡이 아이 1v1 대결(듀얼 진입·보상) ※아이 상태·상대 배정은 capture-dev

### Core 배틀 관련
- `Assets/Scripts/Core/BattleTeamManager.cs` - 5슬롯 팀 관리
- `Assets/Scripts/Core/PlayerInsectCombatPower.cs` - 전투력 계산

### Data 배틀 관련 (data-architect 공유)
- `Assets/Scripts/Data/InsectSkill.cs` - 스킬 정의 (Damage/BuffAttack/DebuffAttack)
- `Assets/Scripts/Data/InsectLearnableSkill.cs` - 레벨업 스킬 습득
- `Assets/Scripts/Data/InsectElement.cs` - 11개 속성 타입

### 배틀 UI (주담당: ui-dev, 배틀 로직 담당)
- `Assets/Scripts/UI/BattleScreenUI.cs` - 1v1 배틀 화면 (Phase: None→Intro→PlayerTurn→PlayerAttack→EnemyAttack→SwapSelect→Result)
- `Assets/Scripts/UI/RaidBattleUI.cs` - 레이드 화면 (Phase: None→Intro→SelectInsect→SelectSkill→PlayerAttack→BossAttack→UniteAttack→Result)
- `Assets/Scripts/UI/BattleTeamUI.cs` - 팀 편성 UI ※UI 레이아웃은 ui-dev

## 핵심 공식

### 데미지
```
damage = max(1, round((basePower + Level×2) × clamp(1+atkBonus, 0.3, 3.0)))
finalDmg = round(damage × clamp(atkStat/defStat, 0.5, 2.5))
기본공격 = Attack × 0.7
```

### 레이드 보스
```
HP×5, ATK×1.5, DEF×1.3
유나이트: 게이지100 충전 → 전원 합동공격 (×1.5 보너스)
게이지 생성: 공격+12+dmg×0.15, AOE+18, 단일+10
보스 AOE: 3턴마다, dmg×2/3 전체
```

### 도주
```
escapeChance = clamp(0.5 + (playerLv-enemyLv)×0.05, 0.1, 0.9)   // BattleEscapeRules.Chance
```

### 읽는 단계 배속 하한 · 결과 닫기 (BattleReadPacing)
```
단계 시계 += DeltaTime × ClockScale(길이, 최소 실제 초, 배속)   // = clamp01(길이 / (최소 × 배속)), 1배속이면 1
진입 인트로 1.4초(실제 시간) → [컷인 3.2 (실제 ≥2.0) | 평범한 인트로 나머지 0.6]
상대 교체 1.2 (≥1.0) · 그림자 변신 1.5 (≥1.0) · 수문장 레이드 등장 2.6 (≥2.0) · 일반 레이드 인트로 2.0 (≥1.4)
결과: 0.6초 잠금 → 탭·클릭·Space/Enter → 손을 뗄 때 닫음(0.5초 넘게 누르면 그냥 닫음). 자동 닫힘은 꿈 챔피언전(4초)만
```

### 낮·밤·날씨 보정 (야생 실외 전투만)
```
ATK·DEF × InsectHabits.BattleStatMultiplier(habit, GetWorldState(regionId))   // 1.10 / 1.00 / 0.90, HP 제외
제외: 수문장 · NPC 대결 · 샌드박스 · 레이드 · 실내 서브에리어(섬은 적용, 세계 날씨)
표지: InsectBattleController.PlayerEnvironment / EnemyEnvironment — BeginBattleCommon이 매번 None으로 끈다
```

### 1v1 승리 포획
```
levelDelta = clamp(playerLv-enemyLv, -5, 5)
levelModifier = levelDelta>=0 ? levelDelta×0.02 : levelDelta×0.03
captureChance = clamp(0.90 - rarityIndex×0.07 - clamp01(captureDifficulty)×0.50
                      + levelModifier + max(0,itemBonus) + max(0,outfitBonus),
                      0.10, 0.95)
```

## 공유 파일 수정 경계
이 에이전트가 공유 파일에서 수정할 수 있는 범위:
- `BattleScreenUI.cs` → Phase 로직, 데미지 표시 계산, 턴 진행 코드만. 레이아웃(ui-dev)/연출(visual-dev) 미수정
- `RaidBattleUI.cs` → 레이드 Phase 로직, 유나이트 게이지, 보스 턴만. 레이아웃/연출 미수정
- `BattleTeamUI.cs` → 팀 유효성 검증, 전투력 표시 로직만. 슬롯 레이아웃(ui-dev) 미수정
- `InsectSkill.cs` / `InsectLearnableSkill.cs` / `InsectElement.cs` → 효과 로직만. 데이터 모델 구조(data-architect) 미수정
- `BattleArenaController.cs` → 아레나 상태, 전투 환경 설정만. 시각 연출(visual-dev) 미수정
경계 밖 수정이 필요하면 변경하지 말고 메인 모델에 보고하여 적절한 에이전트에 재위임.

## 설계 원칙
- `Assets/Scripts/Battle/BattleRandomSource.cs` — 연출과 분리된 전투 난수
- 이벤트 기반: BattleUpdated, BattleEnded, PlayerFainted
- UI는 이벤트 구독으로만 갱신 (직접 참조 X)
- 스탯은 InsectBattleStats에 집중, UI 모놀리스에서 읽기만
- GameConstants.Battle 상수 활용
- 스킬 쿨다운은 턴 단위, 버프/디버프는 duration 턴 후 해제

### 전투 지속시간 회귀 테스트
- `Assets/Tests/EditMode/BattlePacingTests.cs` - 일반전 라운드 분포 및 보스 계수 분리
- `Assets/Tests/EditMode/BattleEnvironmentTests.cs` - 낮·밤·날씨 보정(노트·적용 범위·능력치·컨트롤러 배선·다음 전투 상속 금지)과 도주 규칙
