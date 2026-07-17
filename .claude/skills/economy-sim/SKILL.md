---
name: economy-sim
description: 캔디/젬/코인 수입·지출 균형을 일·주 단위로 시뮬레이션해 데드 화폐, 병목, P2W 격차를 자동 검출합니다
argument-hint: "[--days=14] [--profile=ftp|mid|whale] [--gacha-per-week=2] [--captures-per-day=20] [--battles-per-day=10] [--raids-per-week=3] [--avg-candy-per-event=5] [--daily-candy-income=auto] [--team-size=6] [--current-level=25]"
---

# 경제 균형 시뮬

캔디(F2P)·젬(P2P)·코인(베이직 의상) 3종 통화의 수입·지출을 모델링하고, 코드 grep으로 데드 화폐(발행 경로 0건) / 캔디↔젬 교환 부재 / 의상 P2W 격차를 자동 점검합니다.
**원칙: FAIL 1건이라도 있으면 PASS 보고 금지.**

## Phase 1: 입력 수집

### 코드 자동 추출
- `CashShopManager.cs:44-65` 캐시샵 가격: 2,000원/150젬, 5,000원/400젬, 10,000원/900젬
- `GachaBoxManager` 박스 가격: bronze 500젬 / silver 800젬 / gold 1200젬
- `InsectLevelCurve.cs` 캔디 비용 공식 (BASE=4, GROWTH=1.14)

### grep 자동 점검
- `AddCoins` 호출부 (코인 발행 경로) — 0건이면 FAIL
- `SpendCoins` 호출부 (코인 지출 경로)
- `(Add|Spend)Gems`와 `(Add|Spend)Candy` 동시 라인 (교환 메서드)

### 사용자 입력
| 인자 | 기본값 | 설명 |
|---|---|---|
| `--days` | 14 | 시뮬 기간 |
| `--profile` | ftp | ftp(0젬/주) / mid(400젬/주) / whale(2700젬/주) |
| `--gacha-per-week` | 2 | 주당 가챠 횟수 (silver 기준) |
| `--daily-candy-income` | 100 | 일일 캔디 평균 수입 |
| `--team-size` | 6 | 동시 육성 팀 크기 |
| `--current-level` | 25 | 병목 점검 시 곤충 레벨 |

## Phase 2: 시뮬 실행

```bash
python .claude/scripts/economy_sim.py --days 14 --profile ftp
python .claude/scripts/economy_sim.py --days 30 --profile whale --gacha-per-week 5
```

권장 시나리오:
- **F2P 14일**: 무과금 유저 2주 진행 (캔디 병목 + 젬 부족)
- **whale 30일**: 고래 한 달 (젬 잔고 인플레/디플레)
- **mid 14일**: 중과금 균형점

## Phase 3: 출력 — 위험 신호 표 (FAIL/WARN/PASS)

5개 항목 자동 평가:

| 항목 | 임계값 | 의미 |
|---|---|---|
| 코인 발행 경로 | >= 1 | `AddCoins` 호출 0건 = 데드 화폐 (의상 구매 불가) |
| 중반 캔디 병목 | < 4.0일 | 6마리 동시 레벨업 4일+ 대기 = 이탈 위험 |
| N일 후 젬 잔고 | <= 골드박스 4회분 | 잔고 과대 = 지출 경로 부족 (카테고리 부재) |
| 캔디↔젬 교환 경로 | >= 1 | 0건이면 강제 결제 유도 |
| 프리미엄 vs 베이직 의상 가격비 | < 5x | 5배+ 차이는 P2W |

## Phase 4: 비관적 권장사항 (FAIL/WARN별)

| 위험 신호 | 권장 조정 |
|---|---|
| 코인 데드 화폐 FAIL | (a) `InsectRewardCalculator`에 코인 보상 추가 / (b) 코인 시스템 완전 제거 후 베이직 의상도 캔디 결제 |
| 중반 캔디 병목 FAIL | `CANDY_GROWTH` 1.14 → 1.10 또는 `daily_candy_income` 증가 (보상 배율 상향) |
| 젬 잔고 과대 WARN | 젬 카테고리 추가 (이벤트 박스/한정 의상) 또는 캔디↔젬 교환 도입 |
| 캔디↔젬 교환 부재 WARN | `PlayerCurrencyWallet`에 `ExchangeCandyToGems(rate)` 추가 (예: 100캔디 = 10젬) |
| P2W 격차 WARN | 베이직 의상 효과 상향 또는 프리미엄 의상 가격 인하 |

## Phase 5: 에이전트 위임 가이드

| 영역 | 주담당 | 부수 |
|---|---|---|
| `PlayerCurrencyWallet.cs` 통화 발행/교환 메서드 | data-architect | game-designer |
| `InsectRewardCalculator.cs` 코인 보상 추가 | game-designer | data-architect |
| `CharacterOutfitManager.cs` 의상 가격 조정 | game-designer | data-architect |
| `CashShopManager.cs` 가챠 박스 가격 | game-designer | data-architect |
| `ShopUIController.cs` 신규 카테고리 (젬 지출 경로) | ui-dev | game-designer |

코드 수정 후 `/verify` 호출 → 사용자 메모리 룰 적용.

## 가정 / 한계

- 일일 캔디 수입은 사용자 입력 의존 — 정확값은 텔레메트리 필요
- 사용자 행동 균등 가정 (실제는 초반 폭주 후반 정체)
- 이벤트/시즌 보상 미반영
- 의상 P2W는 평균가 휴리스틱 — 정확한 검증은 `CharacterOutfitManager.cs` 인스펙터 참조
- 동적 호출(Reflection)은 grep 미탐지

## 관련 스킬

- `/balance-sim battle` — 단일 전투 데미지
- `/progression-sim` — Lv1→50 캔디/EXP 곡선
- `/gacha-sim` — 가챠 박스 기댓값
- `/verify` — 코드 수정 후 8항목 검증
