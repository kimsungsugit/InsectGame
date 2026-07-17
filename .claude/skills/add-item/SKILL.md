---
name: add-item
description: 새 아이템을 추가하고 8개 등록 지점 + 4경로 획득 가능성을 강제 검증합니다
argument-hint: "<itemId> <itemType: capture|consumable|cash> <rarity>"
---

# 새 아이템 추가

ItemData SO 생성 + 데이터베이스 등록 + 효과 분기 + 4가지 획득 경로 중 최소 1개 보장.
**원칙: grep 결과 0건이 하나라도 있으면 PASS 보고 금지. 4경로 합계 0이면 FAIL.**

## Phase 1: 필요 정보

| 필드 | 설명 | 예시 |
|------|------|------|
| itemId | 고유 ID (영문 소문자) | `incense_legendary` |
| itemType | capture(미니게임)/consumable(보너스)/cash(샵 판매) | `capture` |
| rarity | ItemRarity enum | Common/Uncommon/Rare/Epic/Legendary |
| displayName | 한글 표시명 | "전설의 향" |
| description | 설명 | "..." |
| themeColor | 필드 드롭 색상 (capture 한정) | new Color(1, 0.7, 0.2) |
| 효과 매개변수 | itemType별 다름 (Phase 2 참조) | — |

## Phase 2: itemType별 자동화 작업

### capture (포획 미니게임 아이템)
필드: `speedMultiplier`, `zoneSizeMultiplier`, `timeLimitMultiplier`, `captureBonus`, `spawnWeight`
등록 위치: `Assets/Scripts/Core/PlaySceneBootstrap.cs:2953-2994` `CreateCaptureItems()` 배열 끝에 `CaptureItemData` 추가.

### consumable (글로벌 효과 아이템)
필드: `captureChanceBonus`, `expMultiplier`, `candyMultiplier`, `rareSpawnMultiplier`, `durationSeconds`
등록 위치: ItemData SO + ItemDatabase.items 추가.
효과 처리: `Assets/Scripts/Core/ItemEffectManager.cs` getter 메서드는 분기 없음 — 새 효과 타입이면 ItemData에 필드 추가 + getter 추가 + 사용처에서 호출.

### cash (캐시샵 판매 아이템)
등록 위치: `Assets/Scripts/Core/CashShopManager.cs:44-65` `InitializeShopItems()` 배열에 `CashShopItem` 추가.
필드: `itemId = "shop_xxx"`, `rewardItemId = 실제 itemId`, `category`, `gemPrice`, `rewardCount`.

## Phase 3: 8개 등록 지점 (비관적 체크리스트)

| # | 등록 지점 | itemType 적용 | 누락 시 증상 | grep 검증 |
|---|---|---|---|---|
| 1 | ItemData SO + ItemDatabase 등록 | 모두 | `FindById()` null → 효과 발동 안 함 | `Grep "<itemId>" Assets/Scripts/Data/ItemDatabase.cs` (Inspector 직렬화는 안내만) |
| 2 | `PlaySceneBootstrap.cs:2953-2994` CreateCaptureItems() | capture | 미니게임 채집망 종류로 안 나옴 | `Grep "itemId = \"<itemId>\"" Assets/Scripts/Core/PlaySceneBootstrap.cs` |
| 3 | `CashShopManager.cs:44-65` InitializeShopItems() | cash | 캐시샵에서 구매 불가 | `Grep "<itemId>" Assets/Scripts/Core/CashShopManager.cs` |
| 4 | `PlaySceneBootstrap.cs:226-231` 초기 인벤토리 지급 | 선택 | 신규 유저 기본 지급 누락 | `Grep "AddItem.*\"<itemId>\"" Assets/Scripts/Core/PlaySceneBootstrap.cs` |
| 5 | `ShopUIController.cs:15-16` itemIds[] / prices[] | 일반 샵 | 샵 슬롯에 안 보임 | **Inspector 직렬화 — 텍스트 안내** (아래 참조) |
| 6 | ItemData.rarityIcon 지정 | 모두 | UI 아이콘 회색 박스 | **Inspector 직렬화 — 텍스트 안내** |
| 7 | ItemEffectManager 새 효과 분기 | consumable (신규 효과 시) | 사용해도 아무 일 없음 | `Grep "<itemId>\|new field name" Assets/Scripts/Core/ItemEffectManager.cs` |
| 8 | `CaptureMinigameController.cs:47-71` StartMinigame 매개변수 | capture (신규 효과 시) | 미니게임 효과 미적용 | `Grep "<itemId>\|item\." Assets/Scripts/Capture/CaptureMinigameController.cs` |

⚠️ **Inspector 직렬화 필드 (#5, #6)**: `.prefab`/`.unity` grep은 false negative 위험으로 미실행. 다음 텍스트 안내로 마무리:
- `Unity Editor → Project 창 → ShopUIController prefab 검색 → Inspector의 itemIds 배열에 "<itemId>" 추가, prices 배열에 가격 입력`
- `Unity Editor → ItemData SO 선택 → Inspector의 rarityIcon 슬롯에 스프라이트 드래그`

## Phase 4: 4경로 획득 가능성 (FAIL 조건)

아이템이 게임에 존재해도 유저가 획득할 수 없으면 죽은 콘텐츠. 4경로 중 최소 1개 매칭 필수:

| 경로 | grep 명령 | 통과 조건 |
|---|---|---|
| 캐시샵 | `Grep "<itemId>" Assets/Scripts/Core/CashShopManager.cs` | ≥ 1 |
| 가챠 | `Grep "<itemId>" Assets/Scripts/Core/GachaBoxManager.cs` | ≥ 1 |
| 전투/포획/퀘스트 보상 | `Grep "<itemId>" Assets/Scripts/Core` (Reward/Loot 키워드 동반) | ≥ 1 |
| 초기 지급 | `Grep "AddItem.*\"<itemId>\"" Assets/Scripts/Core/PlaySceneBootstrap.cs` | ≥ 1 |

**4경로 합계 0건이면 FAIL**:
> "이 아이템은 게임 내에서 획득할 수 없습니다. 최소 1개 경로를 추가하세요. (예: CashShopManager.InitializeShopItems()에 등록)"

## Phase 5: 에이전트 위임 가이드

| 영역 | 주담당 | 부수 |
|---|---|---|
| ItemData SO / ItemDatabase 데이터 모델 | data-architect | — |
| ItemEffectManager / CashShopManager 로직 | game-designer | — |
| CaptureItemData / CaptureMinigameController | capture-dev | — |
| ShopUIController / 인벤토리 UI 표시 | ui-dev | — |
| rarityIcon 스프라이트 / 시각 효과 | visual-dev | — |

`.claude/rules/agent-coordination.md` 갱신 — `ItemData/ItemDatabase` 행 신규 추가 (Phase D에서 일괄 처리).

## Phase 6: 세이브 호환성 경고

⚠️ **기존 itemId 변경 금지**. `PlayerItemRecord.itemId`가 JSON에 문자열로 저장. 변경 시 기존 유저 인벤토리에서 해당 아이템 데이터 매칭 실패 (`PlayerItemInventory.GetCount()`는 0 반환하지만 UI에 null 바인딩 발생 가능).

⚠️ **삭제된 itemId 잔존**: 세이브에는 기록이 남아있고 ItemDatabase에는 없으면 인벤토리 UI에서 null 참조. 삭제 대신 `obsoleteItem = true` 같은 마이그레이션 권장.

마이그레이션 필요 시 → `/save-migration` 호출.

## Phase 7: 완료 후 /verify 강제 호출

```
/verify
```
특히 다음 항목 직격:
- 항목 5 (세이브 호환성: PlayerItemSave 영향)
- 항목 8 (데이터 매칭 무결성: itemId↔Database, itemType↔효과 분기)

## 체크리스트 요약

- [ ] Phase 1 정보 수집 + itemType 분류
- [ ] Phase 2 itemType별 등록 위치 식별
- [ ] Phase 3 8개 grep 모두 통과 (Inspector 항목은 사용자가 Editor 작업 확인)
- [ ] Phase 4 **4경로 중 최소 1개 매칭** (FAIL 절대 회피)
- [ ] Phase 5 적절한 에이전트 위임
- [ ] Phase 6 기존 itemId 변경 없음
- [ ] Phase 7 `/verify` 호출 완료
