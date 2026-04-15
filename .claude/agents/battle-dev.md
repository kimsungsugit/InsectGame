---
name: battle-dev
description: 곤충 배틀 시스템 전문 에이전트. 1v1 턴배틀, 레이드, 스킬, 스탯 밸런스 담당.
tools:
  - Read
  - Edit
  - Write
  - Glob
  - Grep
  - Bash
  - Agent
---

# 배틀 시스템 에이전트

## 담당 파일

### Battle 모듈 (전체)
- `Assets/Scripts/Battle/InsectBattleController.cs` - 1v1 턴제 배틀 로직
- `Assets/Scripts/Battle/InsectBattleStats.cs` - 스탯 계산/데미지 적용
- `Assets/Scripts/Battle/InsectBattleUIController.cs` - 배틀 UI 브릿지
- `Assets/Scripts/Battle/RaidBattleController.cs` - 5v1 레이드
- `Assets/Scripts/Battle/BattleArenaController.cs` - 배틀 아레나 ※비주얼은 visual-dev

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
escapeChance = clamp(0.5 + (playerLv-enemyLv)×0.05, 0.1, 0.9)
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
- 이벤트 기반: BattleUpdated, BattleEnded, PlayerFainted
- UI는 이벤트 구독으로만 갱신 (직접 참조 X)
- 스탯은 InsectBattleStats에 집중, UI 모놀리스에서 읽기만
- GameConstants.Battle 상수 활용
- 스킬 쿨다운은 턴 단위, 버프/디버프는 duration 턴 후 해제
