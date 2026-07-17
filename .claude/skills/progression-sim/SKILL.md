---
name: progression-sim
description: Lv1→50 장기 진행 곡선과 캔디/EXP 수급 데드존을 시뮬레이션하고 비관적 위험 신호를 자동 적재합니다
argument-hint: "[--target-level=50] [--rarity=Common|Uncommon|Rare|Epic|Legendary] [--base-xp=8] [--base-candy=3] [--avg-battle-sec=30] [--team-size=6]"
---

# Lv1→50 진행 시뮬

곤충 레벨 곡선(`InsectLevelCurve.cs`), 보상 배율(`InsectRewardCalculator.cs`), 레이드 ×3(`RaidBattleController.cs`), 튜토리얼 보상 합(`TutorialQuestManager.cs`)을 결정론적으로 합산해 장기 진행 시 발생하는 데드존/병목/격차를 사전 검출합니다.
**원칙: FAIL 1건이라도 있으면 PASS 보고 금지. 스크립트 종료 코드 1.**

## Phase 1: 입력 수집

### 코드 자동 추출 (스크립트 상수)
- `MAX_LEVEL = 50`, `BASE_XP=20, XP_GROWTH=1.12`, `BASE_CANDY=4, CANDY_GROWTH=1.14` (`InsectLevelCurve.cs`)
수치 사본은 여기 없다 — `game_facts`가 실행 시점에 코드에서 읽는다.

- 등급별 보상 배율 ← `InsectRewardCalculator.GetRarityMultiplier()`
- 레이드 보상 배율 ← `RaidBattleController`의 `RewardCandy`/`RewardExp` 계산식
- 튜토리얼 보상 총합 ← `TutorialQuestManager`의 `rewardCandy`/`rewardExp` 대입 전량

> 한때 튜토리얼 사본이 `261 / 500`이었다. 실제는 **336 / 475**다.
> 추출 실패 시 exit 2 — 낡은 값으로 시뮬을 돌리지 않는다.

### 사용자 입력 (선택)
| 인자 | 기본값 | 설명 |
|---|---|---|
| `--target-level` | 50 | 목표 곤충 레벨 |
| `--rarity` | Common | 곤충 레어도 |
| `--base-xp` | 8 | 기본 곤충 1마리당 EXP (배율 적용 전) |
| `--base-candy` | 3 | 기본 곤충 1마리당 캔디 |
| `--avg-battle-sec` | 30 | 평균 전투 소요 시간 (h 환산용) |
| `--team-size` | 6 | 동시 육성 팀 크기 |

데이터 부족 영역(`--base-xp`, `--avg-battle-sec`)은 사용자 입력 의존. 정확도가 입력값에 묶임.

## Phase 2: 시뮬 실행

```bash
python .claude/scripts/progression_sim.py --target-level 50 --rarity Common
```

옵션 조합 권장:
- 베이스 라인: `--rarity Common`
- 최단 경로: `--rarity Legendary --base-xp 8`
- 후반 검증: `--target-level 50 --avg-battle-sec 45`

## Phase 3: 출력 — 위험 신호 표 (FAIL/WARN/PASS)

스크립트가 자동 출력. 7개 항목:

| 항목 | 임계값 | 의미 |
|---|---|---|
| 캔디만 경로 전투 수 | < 4,000 | 캔디만으로 Lv50 도달 시 4,000+ 전투면 동기 부족 |
| Lv35-50 / Lv1-20 EXP 평균비 | <= 10x | 후반 데드존 (레벨업 1회당 시간이 초반의 10배 이상) |
| Common vs Legendary 진행속도 격차 | < 2.0x | 레어도 편향 시 진행속도 차이 200%+ |
| 튜토리얼 보상 / 전체 누적 | >= 5% | 튜토리얼이 전체 진행의 5% 미만이면 동기 부족 |
| 훈련 EXP 기여 | > 0 | `TrainingManager`가 EXP 0 = 캔디 낭비 인식 |
| 추정 플레이 시간 | < 80h | `--avg-battle-sec`이 반영. Lv50까지 80h+면 이탈 위험 |
| 팀 동시 캔디 비용 | < 100,000 | `--team-size`가 반영. 6마리 동시 육성 비용 검증 |

## Phase 4: 비관적 권장사항 (FAIL/WARN별)

| 위험 신호 | 권장 조정 |
|---|---|
| 캔디 전투 수 FAIL | `CANDY_GROWTH` 1.14 → 1.10 (Lv50 비용 17,495 → ~9,000) 또는 `MAX_LEVEL` 40 |
| 데드존 WARN | `XP_GROWTH` 1.12 → 1.08 (후반 격차 완화) |
| 레어도 격차 WARN | `RARITY_MULT[Legendary]` 2.8 → 2.0 (격차 200% → 150%) |
| 튜토리얼 비중 WARN | TutorialQuestManager 보상 +5배 또는 신규 데일리 퀘스트 추가 |
| 훈련 EXP 0 WARN | `TrainingManager.cs`에 expReward 필드 추가 검토 |

조정 후 재시뮬 → FAIL 0/WARN 최소화 확인.

## Phase 5: 에이전트 위임 가이드

| 영역 | 주담당 | 부수 |
|---|---|---|
| `InsectLevelCurve.cs` 곡선 파라미터 변경 | data-architect | game-designer |
| `InsectRewardCalculator.cs` 배율 변경 | game-designer | data-architect |
| `RaidBattleController.cs` 레이드 배수 | battle-dev | game-designer |
| `TutorialQuestManager.cs` 퀘스트 보상 | game-designer | — |
| `TrainingManager.cs` EXP 추가 | game-designer | data-architect |

코드 수정 후 → 사용자 메모리 룰에 따라 `/verify` 호출.

## 가정 / 한계

- IV(개체값) 미적용 — 실제 전투 시간 ±15% 편차
- 평균 전투 시간/포획 시도가 사용자 입력 의존
- 사용자 행동 균등 가정 (실제는 초반 폭주 후반 정체)
- 이벤트/시즌 보상 미반영
- 텔레메트리 수집 후 재검증 필수 — 시뮬은 디자인 의사결정 보조

## 관련 스킬

- `/balance-sim battle` — 단일 전투 데미지 분포
- `/economy-sim` — 통화 수입·지출 균형
- `/gacha-sim` — 가챠 박스 기댓값
- `/verify` — 코드 수정 후 8항목 검증
