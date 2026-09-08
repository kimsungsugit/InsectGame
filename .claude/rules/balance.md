---
description: 밸런스 수치 변경 시 체크리스트 및 참조 기준점
---

# 밸런스 관련 규칙

수치 변경 시 연쇄 영향을 확인하세요.
정확한 공식은 소스 코드(GameConstants, 각 Controller)가 단일 출처입니다.
아래는 빠른 참조용 기준점이며, 코드와 불일치 시 코드가 우선합니다.

## 배틀 기준점
- 데미지: (skill.power + Lv×`LevelDamageScale`) × atkMult × 상성 × 자속 × defRatio
- atkMult 범위: 0.3~3.0, defRatio 범위: `MinAtkDefRatio`~`MaxAtkDefRatio`
- HP: baseHp + ivHp×2 + Lv×`HpPerLevel`
- 기본공격: ATK × 0.7
- 도주: 50% ± 5%/레벨차 (10%~90%)

### 전투 길이 3종은 함께 움직인다

`GameConstants.Battle`의 `LevelDamageScale` · `MaxAtkDefRatio` · `HpPerLevel` 세 값이
전투가 몇 턴인지를 정한다. **하나만 바꾸면 다른 쪽이 곧바로 지배한다** — 여기 숫자를 적지
않는 이유다(코드가 단일 출처).

옛 조합(레벨항 ×2 · 비율 상한 2.5 · HP +3/Lv)은 데미지가 HP보다 빨리 자라서
**양쪽이 서로를 한두 턴에 지웠다.** 실측(Uncommon 플레이어 vs 동레벨 Epic): Lv22·Lv28·Lv42
전부 내가 2턴에 잡고 **1턴에 죽었고**, 수문장전도 같았다. 스킬 위력만 낮추는 시도는
**실패한다** — 지배 항이 위력이 아니라 비율 상한과 레벨항이기 때문이다(2026-08-30에
위력표를 절반으로 낮춰 봤더니 1턴 킬이 그대로였다).

`RaidBossHpMultiplier`는 **이 세 값에서 파생된 값**이다. 셋 중 하나라도 건드리면
보스 HP 배율을 다시 계산할 것 — 안 그러면 레이드 길이가 두 배로 늘거나 반토막 난다
(8.5 → 4.5 재산출이 그 예다).

### 범용 훈련기 위력 상한

훈련·기술 디스크로 아무 곤충이나 배우는 범용기(`tr_*`)의 위력은 **종족 storm(42) 아래**로 둔다.
전용기(60~78)와 최상위 종족기의 자리를 범용기가 빼앗으면 성장 곡선이 무너진다.
요구 레벨은 `TrainingMethod`가 아니라 **`InsectSkill.requiredLevel`**이 정한다 — 방식 단위로만
잠그던 시절 "극한 훈련"이 곤충 Lv6에 위력 55·65·75를 한꺼번에 열었다.

## 포획 기준점
- 기본 60%, 레어도당 -8%, 난이도×0.4 감소
- 레벨 보정: 높으면 +2%/lv, 낮으면 -3%/lv. **레벨 차는 ±5로 clamp**
  (미니게임·전투 두 경로가 같은 상한을 쓴다 — `MaximumLevelDelta`)
- 퍼펙트 타이밍: +15%
- 등급별 최저 보장(floor): C .30 / U .22 / R .14 / E .08 / L .04

### base를 내리려면 floor를 함께 봐야 한다

기본값을 낮추는 변경은 **단독으로는 무조건 틀린다.** floor가 base 60% 전제로 잡혀 있어서,
난이도 0.5 동레벨 기준 현재는 전 등급이 floor보다 위라(C .40 … L .08 vs floor .30 … .04)
공식이 지배하지만, base를 0.35로 내리면 **전 등급이 floor에 닿아** 난이도·레벨·base 항이
전부 죽고 포획률이 등급별 상수가 된다.

실제로 그런 너프가 2026-07-17에 있었고(`CaptureController` + `GameplayTuningProfile`을
같은 값으로 함께 .35/.45/.10), 08-03 리팩터가 상수를 추출하며 통째로 원복했다. 그 커밋이
floor를 같이 도입했기 때문에 **단순 복원은 되돌리기가 아니라 다른 결함**이 된다 —
2026-08-17에 확인 후 현행(.60/.40/.15) 유지로 결정했다.

`CaptureChanceCalculatorTests`의 `GameplayTuningProfile_NewAssetDefaults_MatchCaptureFormulaDefaults`는
프로파일과 공식의 **일치**만 본다. 두 값이 같다고 옳은 값인 건 아니다(위 원복을 이 테스트가 가렸다).

상한이 필요한 이유: 메인 필드는 리전이 아무리 높아도 **항상 Lv.1부터** 스폰한다
(`InsectSpawner` 지수 분포). 상한이 없으면 고레벨 플레이어가 저레벨 전설을 만났을 때
레벨 보정만으로 등급·난이도 페널티가 전부 상쇄돼 포획이 사실상 보장된다.

## 보상 기준점
- 배율: C1.0/U1.2/R1.5/E2.0/L2.8
- 레이드: ×3
- 가챠 천장: 같은 상자 `GachaBoxManager.PityLegendaryPulls`(80)연째 Legendary 확정. 상자별 카운터는
  계정 스코프 PlayerPrefs + 클라우드(기기 간 **max** 병합). 없을 땐 브론즈 100연 전설 0개가 60%였다.
- 가챠 샤이니 `GachaBoxManager.ShinyChance`(2%) — 필드 1%보다 높아야 상자만의 매력이 선다. 골드 보너스
  캔디 45~80은 Lv30 레벨업비의 절반 이상이어야 한다(gacha_sim 신호 5). **OpenBox의 `case "box_X":`
  세 개를 접지 말 것** — 추출기가 그 뒤의 Random.Range를 읽는다.

## 성장 기준점
- 곤충 캔디 곡선: `InsectLevelCurve.GetCandyCost` = base 4 × 1.125^(L-1). **판정은 `progression_sim`**
  (Lv50까지 현실 진행 4,000전투 미만). 14%였을 때 5,759전투로 FAIL, 12.5%로 3,339. 13%는 4,028로
  경계다 — 기울기를 올릴 땐 시뮬을 먼저 돌린다.

## 변경 시 체크리스트
1. GameConstants 상수 변경 → 전체 시스템 영향, `/impact-analysis` 실행
2. 데미지 공식 변경 → 1v1 + 레이드 양쪽 확인, `/balance-sim battle` 실행
3. 포획률 변경 → 미니게임 난이도와 균형, `/balance-sim capture` 실행
4. 보상 변경 → 경제 밸런스 (레벨업 비용과 비교)
5. IV 공식 변경 → 스탯 범위 + 등급 분포 재계산
6. 변경 후 관련 EditMode 테스트 실행 필수 (`/test`)
