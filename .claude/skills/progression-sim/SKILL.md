---
name: progression-sim
description: 이원 레벨(트레이너 EXP·선형 / 곤충 캔디·지수) 진행 곡선을 시뮬레이션하고 병목·격차를 검출합니다
argument-hint: "[--target-level=50] [--rarity=Common|Uncommon|Rare|Epic|Legendary] [--avg-battle-sec=30] [--team-size=auto]"
---

# 진행 곡선 시뮬 — 이원 레벨

이 게임엔 **분리된 두 레벨 시스템**이 있다. 시뮬은 이걸 따로 모델링한다.

| 시스템 | 곡선 | 성장 재화 | 출처 |
|---|---|---|---|
| **트레이너 레벨** | 선형 `max(floor, base+(lv-1)*growth)` | 배틀/포획/레이드/튜토리얼 EXP | `PlayerProgressController` |
| **곤충 레벨** | 지수 `base*1.14^(lv-1)` | 캔디만 (`TryLevelUpWithCandy`) | `InsectLevelCurve` |

> **곤충 XP 곡선(`GetXpToNextLevel`, 20*1.12^)은 진행 경로가 아니다.** 곤충 XP 시스템
> (`GainXp`/`currentXp`)은 코드·UI에 배선만 돼 있고 어떤 게임플레이도 곤충에 XP를 주지
> 않는다(dead 배선). 옛 시뮬은 이 곡선을 진행 경로로 오인해 5개 신호가 전부 오탐이었다.

**원칙: FAIL 1건이라도 있으면 PASS 보고 금지. 종료 코드 1.**

## Phase 1: 입력 — 수치 사본은 여기 없다

전부 `game_facts`가 실행 시점에 코드에서 읽는다. 추출 실패 시 exit 2.

| 사실 | 출처 |
|---|---|
| 트레이너 곡선 (base/growth/floor/max) | `PlayerProgressController.GetXpToNextLevel` |
| 곤충 캔디 곡선 (base/growth) | `InsectLevelCurve.GetCandyCost` |
| 등급별 전투 보상 (exp/candy base) | `PlaySceneBootstrap` switch(rarity) |
| 등급 배율 | `InsectRewardCalculator.GetRarityMultiplier` |
| 팀 최대 슬롯 | `GameConstants.Battle.MaxTeamSlots` |
| 레이드 배율 | `RaidBattleController` RewardCandy 계산식 |
| 튜토리얼 보상 | `TutorialQuestManager` |

전투당 실제 보상 = 등급별 base × 등급 배율. 예: Legendary 캔디 = 6 × 2.8 = 16.8.

| 인자 | 기본값 | 설명 |
|---|---|---|
| `--target-level` | 곤충 max(코드) | 목표 곤충 레벨 |
| `--rarity` | Common | 주로 잡는 적 등급 (최악=Common, 최선=Legendary) |
| `--avg-battle-sec` | 30 | 평균 전투 시간 |
| `--team-size` | MaxTeamSlots(코드) | 동시 육성 팀 크기 |

## Phase 2: 실행

```bash
python .claude/scripts/progression_sim.py --rarity Common     # 최악(전투당 캔디 최소)
python .claude/scripts/progression_sim.py --rarity Rare       # 중간 시나리오
```

## Phase 3: 위험 신호

| 항목 | 임계값 | 의미 |
|---|---|---|
| 곤충 Lv50 캔디 전투 수 (**현실 진행**) | < 4,000 | 곤충 레벨이 리전 진행과 동기화된다는 가정. 비싼 후반 레벨(전체 캔디의 84%가 Lv36+)이 고레어 엔드리전 income으로 벌린다. 리전 스폰가중(`GetWeightedRandom`)에서 등급 분포를 코드로 읽어 계산 |
| 팀 N마리 캔디 비용 | < 100,000 | MaxTeamSlots 반영 |
| 트레이너 EXP 후반/초반비 | <= 5x | 트레이너는 **선형**이라 완만해야 정상. 곤충 캔디(지수)는 이 검사 대상 아님 — 지수가 설계 |
| 튜토리얼 캔디 / 곤충 Lv1→10 비용 | >= 5% | 초반 부양 강도. **엔드게임 단일 곤충 평생 캔디가 아니라 초반 대비** |

신호1은 **현실 진행**(리전 동기화)을 판정한다. "Common 고정·배틀+포획만"(~8,700전투)은 출력에
참고 상한으로만 병기하고 **FAIL 트리거로 쓰지 않는다** — 시뮬 자신이 그걸 "최악"이라 부르면서
그걸로 FAIL을 내면 자기가 인정한 극단으로 거짓 경보를 울리는 셈이다. 실제 등급 분포는 진행에
따라 오르고(유적: Common 0%, R+E+L 77%, ~7캔디/전투) 레이드(×3)·가챠·튜토리얼이 더해져
현실 전투 수(~3,100)는 임계값 아래다.

## Phase 4: 권장 조정 (FAIL/WARN별)

수치는 출력의 실측을 보고 정할 것 — 문서에 박아두면 썩는다.

| 위험 신호 | 권장 조정 방향 |
|---|---|
| 곤충 캔디 전투 수 FAIL | `InsectLevelCurve` 캔디 성장률↓ 또는 max 레벨↓, 또는 캔디 소스 보강(포획 보너스 등) |
| 팀 캔디 FAIL | 캔디 곡선 완화 또는 캔디 수급 상향 |
| 트레이너 곡선비 WARN | `PlayerProgressController` growth 조정 (선형인데 비가 크면 floor/base 재검토) |
| 튜토리얼 초반 부양 WARN | `TutorialQuestManager` 초반 캔디 보상 상향 |

## Phase 5: 에이전트 위임

| 영역 | 주담당 |
|---|---|
| `InsectLevelCurve` 곤충 캔디 곡선 | data-architect / game-designer |
| `PlayerProgressController` 트레이너 곡선 | data-architect / game-designer |
| `PlaySceneBootstrap` 등급별 보상 | game-designer |
| `TutorialQuestManager` 보상 | game-designer |

## 가정 / 한계

- 신호1(판정)은 **현실 진행**: 곤충 레벨 L의 캔디를 그 시점 리전(requiredLevel<=L 중 최상위)의
  스폰가중 기대 캔디로 벌어들인다고 본다. 리전 income은 `RegionDefinitions` 풀 + 로스터
  spawnWeight를 코드에서 읽어 산출(`InsectSpawner.GetWeightedRandom` 무아이템과 동일 가중).
- 캔디는 전역 단일 풀(`PlayerCandyInventory`)이라 종 무관 합산 — 종별 캔디 아님.
- 레이드(×3)·가챠 박스(5~50)·튜토리얼(336)은 별도라 현실 전투 수를 더 낮춘다. 현실 수치도
  그 의미에서 상한. 레어스폰 아이템/의상 보너스도 미반영(있으면 고레어↑ → income↑).
- 곤충별 candyReward는 `PlaySceneBootstrap` 등급별 하드코딩을 읽는다(InsectDatabase .asset의
  개체별 편차 미반영).
- 텔레메트리 수집 후 재검증 필수 — 시뮬은 디자인 의사결정 보조.

## 관련 스킬

- `/economy-sim` — 통화 수입·지출 균형
- `/gacha-sim` — 가챠 박스 기댓값
- `/verify` — 코드 수정 후 검증
