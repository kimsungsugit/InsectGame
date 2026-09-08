---
name: data-lint
description: 코드 내 하드코딩된 ID(리전/아이템/가챠) 정의·참조 정합성을 자동 검증하고 dead code, 누락 ID, 중복 정의를 검출합니다
argument-hint: "[--detail]"
---

# 데이터 정합성 자동 검증

리전·아이템·가챠 시스템의 코드 내 하드코딩 데이터를 정규식으로 추출 후 cross-reference 검증. dead method, 미참조 정의, 누락 참조, 중복 정의를 자동 검출합니다.
**원칙: FAIL 1건이라도 있으면 PASS 보고 금지.**

## Phase 1: 검증 항목

정본은 `data_lint.py`의 `evaluate_signals()`다. 아래는 지도일 뿐이니 개수가 어긋나면
출력을 믿을 것.

| # | 항목 | 검출 대상 |
|---|------|----------|
| 1 | 리전 참조 정합성 | RegionManager/RegionTerrainBuilder/RegionMapUI/AudioManager의 case "..." switch가 RegionDefinitions.CreateAll() 정의에 없는 ID 참조 시 FAIL |
| 2 | 리전 고아 | RegionDefinitions.CreateAll()에 정의했지만 4개 참조 위치 모두에서 미참조 시 WARN |
| 3 | dead method 일반 검출 | PlaySceneBootstrap의 private 메서드 중 참조 0건 시 FAIL (Unity 콜백 제외) |
| 4 | CashShop rewardItemId ↔ 아이템 풀 | CashShopManager.shopItems의 rewardItemId가 CreateCaptureItems / 알려진 풀에 없으면 WARN |
| 5 | itemId 중복 | CreateCaptureItems ∩ CashShop shopItems 교집합 시 FAIL |
| 6 | Gacha 풀 ID ↔ displayName | gachaExclusives 풀에 있고 exclusiveDisplayNames에 없는 ID는 FAIL |
| 7 | Gacha displayName 고아 | exclusiveDisplayNames에 있고 풀에 없는 ID는 WARN |
| 8-10 | Gacha Bronze/Silver/Gold 확률 분포 | `Bronze/Silver/GoldThresholds` 누적 임계값 단조증가 + Legendary > 0 검증. 위반 시 FAIL |
| 11 | CashShop UI 박스 가격 ↔ Manager gemPrice | UI `DrawBoxCard` 가격 인자가 `CashShopManager.shopItems[].gemPrice`와 다르면 FAIL |
| 12 | CashShop UI 박스 확률 텍스트 ↔ Gacha 임계값 | UI rateText의 등급별 % vs `GachaBoxManager`의 `*Thresholds` 환산값 차이 > 0.01 시 FAIL |

## Phase 2: 시뮬 실행

```bash
python .claude/scripts/data_lint.py
python .claude/scripts/data_lint.py --detail   # 추출된 데이터셋 상세 출력
```

`--detail`은 grep으로 추출된 모든 ID 집합을 노출 — false positive 디버그용.

### 종료 코드 — 1과 2를 구별할 것

| 코드 | 뜻 | 대응 |
|---|---|---|
| 0 | 이상 없음 | — |
| 1 | **데이터 FAIL** — 진짜 결함 | Phase 4 권장 조치 |
| 2 | **추출기 고장** — 검증기 자신의 문제 | 결과 전체를 신뢰하지 말 것. `data_lint.py`의 추출기부터 고친다 |

exit 2는 스크립트가 코드에서 기대한 심볼(`RegionDefinitions.CreateAll()`,
`*Thresholds` 등)을 못 찾았다는 뜻이다. 리팩터링이 정의를 옮겼을 때 나온다.
**이때 나오는 FAIL 목록은 전부 무의미하다** — 추출이 빈 집합을 반환해 전부 "정의 없음"으로
보이기 때문. 실제로 리전 정의가 `PlaySceneBootstrap`에서 `RegionDefinitions.cs`로
옮겨간 뒤 그 상태가 방치돼 리전 7개가 계속 거짓 FAIL이었다. 그래서 exit 2로 분리했다.

## Phase 3: 출력 — 위험 신호 표

각 항목별 PASS/WARN/FAIL 표로 출력. FAIL 1건 이상 시 종료 코드 1.

## Phase 4: 비관적 권장사항 (FAIL/WARN별)

| 위험 신호 | 권장 조치 |
|---|---|
| 리전 참조 정합성 FAIL | RegionManager/Terrain/MapUI/Audio switch에 등장하는 미정의 regionId를 RegionDefinitions.CreateAll()에 추가하거나 switch에서 제거 |
| 리전 고아 WARN | 정의했는데 미참조 — 게임에 등장 안 하는 리전. 4개 참조 위치(switch/case) 추가 또는 정의 제거 |
| **dead method FAIL** | 검출된 메서드 본체 통째 제거. PlaySceneBootstrap 모놀리스라 hook 경고 받지만 **단순 dead 코드 제거는 안전 영역**. 단, 이벤트 구독자/델리게이트 할당이 grep에서 정상 인식되므로 실제 사용처 재확인 권장 |
| CashShop rewardItemId 미매칭 WARN | (a) ItemData SO를 ItemDatabase에 추가, (b) `add-item` 스킬로 신규 등록, (c) `rewardItemId`를 정의된 풀로 변경 |
| itemId 중복 FAIL | CreateCaptureItems와 CashShop이 같은 itemId 정의 — 한쪽 제거 |
| Gacha displayName 누락 FAIL | `gachaExclusives` 풀에 있는 모든 ID는 `exclusiveDisplayNames` 사전에 한국어 이름 추가 필수 |
| Gacha 확률 분포 FAIL | 임계값 단조증가 위반 또는 Legendary 0% — `GachaBoxManager`의 `Bronze/Silver/GoldThresholds` 배열 정정 (`Get*Rarity`는 이 배열을 넘기는 한 줄일 뿐) |
| **CashShop UI 가격 ↔ Manager FAIL** | UI 하드코딩 가격(`CashShopUI.DrawBoxCard`)이 정본(`CashShopManager.gemPrice`)과 어긋남. UI 텍스트를 매니저 값으로 정정 (ui-dev 위임). 장기적으로 SO 도입 검토 (data-architect) |
| **CashShop UI 확률 텍스트 ↔ Gacha 임계값 FAIL** | UI rateText가 코드 확률과 어긋남. 매니저 임계값에서 단계별 확률 재계산해 UI 갱신 (ui-dev 위임) |

## Phase 5: 에이전트 위임 가이드

| 영역 | 주담당 | 부수 |
|---|---|---|
| `RegionDefinitions.CreateAll` 리전 정의 | game-designer | data-architect |
| `RegionManager` switch 분기 정정 | game-designer | — |
| `RegionTerrainBuilder` BuildXxxTerrain 추가 | visual-dev | — |
| `RegionMapUI` connections / GetRegionSymbol | ui-dev | — |
| `CashShopManager.shopItems` 가격/풀 | game-designer | data-architect |
| `GachaBoxManager.gachaExclusives` 풀 | game-designer | data-architect |
| ItemData SO 신규 추가 | data-architect | game-designer |

코드 수정 후 `/verify` 호출 → 사용자 메모리 룰 적용. **수정 후 data-lint 재실행으로 회귀 검증 필수**.

## 가정 / 한계

- 코드 내 하드코딩된 ID만 검증. **ScriptableObject(.asset) 직렬화 미지원**
- 정규식 기반 추출 — 코드 포맷 변경 시 false negative 가능
- `InsectDatabase.insects[]`의 insectId는 .asset 파일에 있어 미검증 (후속 작업)
- Inspector 직렬화 필드(예: `ShopUIController.itemIds[]`)는 grep 미지원
- `ItemDatabase.asset`이 비어있거나 미존재 시 rewardItemId 검증이 휴리스틱 풀에 의존

## 관련 스킬

- `/add-skill` `/add-item` `/add-region` — 신규 콘텐츠 추가 (Phase #1)
- `/balance-sim` `/progression-sim` `/economy-sim` `/gacha-sim` — 시뮬레이션 (Phase #2)
- `/verify` — 코드 수정 후 8항목 검증
