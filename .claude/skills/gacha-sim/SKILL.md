---
name: gacha-sim
description: 가챠 박스 기댓값/천장/픽업률을 몬테카를로(10000회)로 분포까지 검증하고 UI-코드 가격·확률 정합성을 자동 점검합니다
argument-hint: "[--box=bronze|silver|gold|all] [--pulls=100] [--trials=10000] [--seed=42]"
---

# 가챠 박스 시뮬

`GachaBoxManager`의 확률 분포로 몬테카를로 시뮬레이션 + `CashShopUI`의 표시값과 정본의
정합성을 자동 검증합니다.
**원칙: FAIL 1건이라도 있으면 PASS 보고 금지.**

## Phase 1: 입력 수집

### 코드 자동 추출 — 수치 사본은 여기 없다

확률·가격·픽업률·캔디 보너스·전용 풀 크기·보상 배율은 전부 `game_facts`가 실행 시점에
코드에서 읽는다. 이 문서에 숫자를 적어두지 않는다.

| 사실 | 출처 |
|---|---|
| 등급 확률 | `GachaBoxManager`의 `Bronze/Silver/GoldThresholds` → `C=a, U=b−a, R=c−b, E=d−c, L=100−d` |
| 박스 가격(정본) | `CashShopManager.shopItems[].gemPrice` |
| 박스 가격(표시) | `CashShopUI`의 박스 카드 가격 인자 |
| 픽업 확률 | `GachaBoxManager.PickRandomInsect()`의 boxId 분기 |
| 캔디 보너스 | `OpenBox()`의 `switch(boxId)` 안 `Random.Range` |
| 전용 풀 | `gachaExclusives` |
| 보상 배율 | `InsectRewardCalculator.GetRarityMultiplier()` |

> 한때 이 자리에 "골드 1200젬: … L5%"라는 사본이 있었다. 실제는 750젬 / L**45%**였다.
> 그 사본 위에서 돌린 시뮬은 존재하지 않는 게임을 시뮬레이션했고, "천장 부재" 판정과
> "골드 Legendary 5%→7% 상향" 권고는 정반대로 무의미했다. 그래서 숫자를 없앴다.

**추출 실패 시 exit 2** — 낡은 값으로 조용히 시뮬을 돌리지 않는다. 시뮬 결과가 아니라
추출기를 먼저 고칠 것.

### 사용자 입력
| 인자 | 기본값 | 설명 |
|---|---|---|
| `--box` | bronze | 시뮬할 박스 (또는 `all`) |
| `--pulls` | 100 | 1회 시도당 가챠 횟수 |
| `--trials` | 10000 | 몬테카를로 반복 횟수 |
| `--seed` | 42 | RNG 시드 (재현성) |

## Phase 2: 시뮬 실행

```bash
python .claude/scripts/gacha_sim.py --box bronze --pulls 100
python .claude/scripts/gacha_sim.py --box all --trials 50000
python .claude/scripts/gacha_sim.py --box gold --pulls 10 --trials 100000
```

권장 시나리오:
- **천장 부재 검증**: `--box bronze --pulls 100`
- **고래 한계점**: `--box gold --pulls 30`
- **실버 함정 비교**: `--box all`

## Phase 3: 출력 — 분포 표 + 위험 신호 표

### 3-A. 몬테카를로 분포
박스별 5%/50%/95% 분위 + 평균 + 0개 시나리오 비율.

### 3-B. 박스 간 가성비 비교 표

박스별 가격당 가중 EV(Common=1, Uncommon=2, Rare=4, Epic=10, Legendary=25 가중치)와 브론즈 대비 효율 비율 출력. 차상위 박스의 효율이 차하위 미만이면 함정.

### 3-C. 위험 신호 (9개 항목)

| 항목 | 임계값 | 의미 |
|---|---|---|
| 천장 부재: N연차 Legendary 0개 | < 50% | 100연차 후에도 60%+ 확률로 0개면 운게임 |
| CashShopUI 가격 vs Manager 정본 | 0건 불일치 | UI 표시값이 실제 차감과 다르면 결제 분쟁 |
| UI 확률 텍스트 vs 코드 정합성 | 0건 불일치 | 텍스트 하드코딩 동기화 누락 |
| 실버 가성비 (브론즈 대비) | 기댓값 +60%+ | 가격 +60% 비싼데 기댓값 < 60%면 함정 |
| 골드 가성비 (실버 대비) | 기댓값 +30%+ | 가격 +50% 비싼데 기댓값 < 30%면 차상위 매력 부족 |
| 박스 가성비 역전 | 역전 0건 | 차상위 박스의 가격당 EV가 차하위 미만이면 명백한 함정 |
| 골드 캔디보너스 / Lv30 레벨업비 | >= 50% | 보너스가 1회 레벨업 비용의 절반도 안 되면 형식적 |
| Epic 전용곤충 N연차 평균 중복 | < 15 | 풀 4종이라 중복 누적 → 가챠 가치 하락 |
| 가챠 샤이니 적용 | 필드 1% 동일 | 가챠 0%면 필드 대비 격차 |

## Phase 4: 비관적 권장사항 (FAIL/WARN별)

| 위험 신호 | 권장 조정 |
|---|---|
| **천장 부재 FAIL** | `GachaBoxManager`에 pity counter 도입 (예: 50연차 보장 Epic+, 100연차 보장 Legendary) |
| **UI 가격 불일치 FAIL** | `CashShopUI.cs:313-324` 하드코딩 제거 → `GachaBoxConfig` SO에서 단일 소스 로드 |
| **UI 확률 텍스트 불일치 FAIL** | 동일 — `GachaBoxConfig` SO에 확률 필드 추가 |
| **실버 가성비 WARN** | 실버 가격 인하 또는 픽업률 +5% 상향 |
| **골드 가성비 WARN** | 골드 EV 증가가 가격 증가 대비 부족 — Legendary % 상향 또는 가격 인하 |
| **박스 가성비 역전 WARN** | 명백한 디자인 결함 — 차상위 박스의 효율이 차하위 미만. 가격/확률 재조정 필수 (예: 골드 Legendary 5%→7%, 실버 3%→2.5%) |
| **골드 캔디보너스 WARN** | 골드 캔디 [20-50] → [50-150] 또는 별도 보상 (가챠 티켓 등) |
| **가챠 샤이니 WARN** | `GachaResult`에 샤이니 필드 추가, `InsectEntity`처럼 1% 적용 |

## Phase 5: 에이전트 위임 가이드

| 영역 | 주담당 | 부수 |
|---|---|---|
| `GachaBoxManager.cs` 확률/풀/천장 로직 | game-designer | data-architect |
| `CashShopUI.cs` 하드코딩 제거 | ui-dev | data-architect |
| `GachaBoxConfig` SO 신설 (단일 소스) | data-architect | game-designer |
| `GachaResult`에 isShiny 필드 추가 | data-architect | battle-dev (필드 샤이니와 일관성) |
| `CashShopManager.cs` 가격 정의 | game-designer | data-architect |

코드 수정 후 `/verify` 호출 → 사용자 메모리 룰 적용. 특히 가격/확률 변경 시 `gacha-sim` 재실행으로 정합성 재검증.

## 가정 / 한계

- 천장(pity) 미구현 가정 — 실제 코드와 일치
- 곤충 풀 균등 무작위 가정 (가중치 데이터 미확인)
- IV 미적용 — 곤충별 스탯 편차 무시
- 시드 고정으로 재현성 보장 (`--seed`로 변경)
- UI 정합성은 정규식 기반 — 텍스트 포맷 변경 시 false negative 가능
- 텔레메트리 수집 후 재검증 필수

## 관련 스킬

- `/balance-sim battle` — 단일 전투 데미지
- `/progression-sim` — Lv1→50 캔디/EXP 곡선
- `/economy-sim` — 통화 수입·지출 균형
- `/verify` — 코드 수정 후 8항목 검증
