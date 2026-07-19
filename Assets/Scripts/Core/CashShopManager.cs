using System;
using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    public enum CashItemCategory { MinigameItem, GachaBox }

    [Serializable]
    public class CashShopItem
    {
        public string itemId;
        public string displayName;
        public string description;
        public CashItemCategory category;
        public int priceKRW;
        public int gemPrice;
        public string rewardItemId;
        public int rewardCount;
    }

    public class CashShopManager : MonoBehaviour
    {
        public static CashShopManager Instance { get; private set; }

        private CashShopItem[] shopItems;
        private int gems;
        private PlayerCurrencyWallet wallet; // 보석 이중 관리 동기화용

        // wallet이 single source of truth. wallet 있으면 wallet.Gems 우선, AutoWire 전에는 캐시 사용.
        public int Gems => wallet != null ? wallet.Gems : gems;

        public void AutoWire(PlayerCurrencyWallet w)
        {
            if (wallet == null) wallet = w;
            if (wallet == null) return;

            // 1회 마이그레이션: PlayerPrefs "InsectGame.Gems" 값이 wallet보다 크면 wallet에 반영 후 키 삭제.
            // 이후 사이클부터 wallet 단일 소스로 동작 — 이중 관리 종료.
            if (wallet.Gems < gems)
            {
                wallet.AddGems(gems - wallet.Gems);
            }
            // PlayerPrefs 키는 더 이상 진실의 원천이 아님 — 다음 세션 혼동 방지
            if (PlayerPrefs.HasKey(GemsKey))
            {
                PlayerPrefs.DeleteKey(GemsKey);
                PlayerPrefs.Save();
            }
            gems = wallet.Gems; // 캐시 동기화
            GemsChanged?.Invoke();
        }

        public event Action<CashShopItem> ItemPurchased;
        public event Action GemsChanged;

        private const string GemsKey = "InsectGame.Gems";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            gems = PlayerPrefs.GetInt(GemsKey, 0);
            InitializeShopItems();
        }

        private void InitializeShopItems()
        {
            shopItems = new CashShopItem[]
            {
                // -- 보석 충전 (실제 결제 -> 보석 지급) --
                new CashShopItem { itemId = "gem_200",  displayName = "보석 150개",  description = "탐험에 필요한 보석",     category = CashItemCategory.MinigameItem, priceKRW = 2000,  gemPrice = 0, rewardCount = 150 },
                new CashShopItem { itemId = "gem_550",  displayName = "보석 400개",  description = "10% 보너스!",           category = CashItemCategory.MinigameItem, priceKRW = 5000,  gemPrice = 0, rewardCount = 400 },
                new CashShopItem { itemId = "gem_1200", displayName = "보석 900개",  description = "20% 보너스!",           category = CashItemCategory.MinigameItem, priceKRW = 10000, gemPrice = 0, rewardCount = 900 },

                // -- 미니게임 아이템 (보석으로 구매) --
                new CashShopItem { itemId = "shop_net_silver",   displayName = "은빛 채집망",   description = "포획 확률 +15% 증가",      category = CashItemCategory.MinigameItem, priceKRW = 0, gemPrice = 200, rewardItemId = "net_silver",   rewardCount = 5 },
                new CashShopItem { itemId = "shop_net_gold",     displayName = "황금 채집망",   description = "포획 확률 +30% 증가",      category = CashItemCategory.MinigameItem, priceKRW = 0, gemPrice = 400, rewardItemId = "net_gold",     rewardCount = 3 },
                // shop_rare_incense (rewardItemId="incense_rare") — ItemData SO 미정의로 비활성화. /add-item 스킬로 추가 후 복원
                new CashShopItem { itemId = "shop_candy_pack",   displayName = "캔디 대량팩",   description = "캔디 50개",                category = CashItemCategory.MinigameItem, priceKRW = 0, gemPrice = 300, rewardItemId = "candy",        rewardCount = 50 },
                new CashShopItem { itemId = "shop_exp_boost",    displayName = "경험치 부스터", description = "10분간 경험치 2배",        category = CashItemCategory.MinigameItem, priceKRW = 0, gemPrice = 300, rewardItemId = "exp_boost",    rewardCount = 1 },

                // -- 슈퍼 아이템 (보석으로 구매) — Epic/Legendary 강화 묶음. 무료는 후반 퀘스트 보상으로도 획득 --
                new CashShopItem { itemId = "shop_beast_mark",     displayName = "맹수의 표식",   description = "10분간 전투 공격력 +35%",           category = CashItemCategory.MinigameItem, priceKRW = 0, gemPrice = 450,  rewardItemId = "beast_mark",     rewardCount = 2 },
                new CashShopItem { itemId = "shop_binding_net",    displayName = "포박의 그물",   description = "10분간 포획 +35% & 도주 방지",      category = CashItemCategory.MinigameItem, priceKRW = 0, gemPrice = 500,  rewardItemId = "binding_net",    rewardCount = 2 },
                new CashShopItem { itemId = "shop_golden_censer",  displayName = "황금 향로",     description = "15분간 희귀 출현 ×2.3 & 포획 +25%", category = CashItemCategory.MinigameItem, priceKRW = 0, gemPrice = 900,  rewardItemId = "golden_censer",  rewardCount = 1 },
                new CashShopItem { itemId = "shop_spirit_blessing", displayName = "정령의 가호",  description = "10분간 공격력·방어력 대폭 상승",     category = CashItemCategory.MinigameItem, priceKRW = 0, gemPrice = 1000, rewardItemId = "spirit_blessing", rewardCount = 1 },

                // -- 치료 아이템 (보석으로 편의 구매 — 코인 상점에서도 저렴하게 판매) --
                new CashShopItem { itemId = "shop_wound_salve",  displayName = "상처약 묶음",   description = "곤충 HP 40 회복 ×5",          category = CashItemCategory.MinigameItem, priceKRW = 0, gemPrice = 30, rewardItemId = "wound_salve",  rewardCount = 5 },
                new CashShopItem { itemId = "shop_full_restore", displayName = "종합 치료제",   description = "HP 전액 + 모든 상태 치료 ×3", category = CashItemCategory.MinigameItem, priceKRW = 0, gemPrice = 60, rewardItemId = "full_restore", rewardCount = 3 },

                // -- 랜덤 상자 (보석으로 구매) --
                new CashShopItem { itemId = "box_bronze", displayName = "브론즈 상자", description = "기본 곤충 + 소량 희귀 확률", category = CashItemCategory.GachaBox, priceKRW = 0, gemPrice = 500, rewardCount = 1 },
                new CashShopItem { itemId = "box_silver", displayName = "실버 상자",   description = "희귀 곤충 확률 UP!",        category = CashItemCategory.GachaBox, priceKRW = 0, gemPrice = 600, rewardCount = 1 },
                new CashShopItem { itemId = "box_gold",   displayName = "골드 상자",   description = "전설 곤충 확률 대폭 UP!",   category = CashItemCategory.GachaBox, priceKRW = 0, gemPrice = 750, rewardCount = 1 },
            };
        }

        // 실결제 공급자(IAPManager) — 없거나 미준비면 프로덕션에선 구매 비활성(무료 지급 금지).
        private IPurchaseProvider purchaseProvider;
        public bool IsRealMoneyReady => purchaseProvider != null && purchaseProvider.IsReady;
        public void SetPurchaseProvider(IPurchaseProvider provider) { purchaseProvider = provider; }

        // UI가 보석 패키지 구매 버튼을 활성화할지 — 프로덕션은 결제 모듈 준비 시에만, 에디터/개발은 항상(테스트).
        public bool CanBuyRealMoney
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return IsRealMoneyReady;
#endif
            }
        }

        // -- 보석 충전 (실제 결제) --
        // 반환값: 구매 "요청 시작" 성공 여부(실제 지급은 비동기 콜백/ItemPurchased 이벤트로 반영).
        public bool PurchaseWithRealMoney(string itemId)
        {
            CashShopItem item = GetItem(itemId);
            if (item == null || item.priceKRW <= 0 || !item.itemId.StartsWith("gem_")) return false;

            // 실결제 모듈이 준비됐으면 스토어 결제 → 완료 콜백에서만 지급(영수증 검증은 공급자).
            if (purchaseProvider != null && purchaseProvider.IsReady)
            {
                // 공급자가 Google Play 영수증 서버 검증과 권위 잔액 반영까지 처리한다.
                // 콜백은 실패 로깅만 — 클라이언트에서 rewardCount를 더하면 중복 지급 위험.
                purchaseProvider.Purchase(item.itemId, success =>
                {
                    if (!success) Debug.LogWarning("[CashShop] 결제 실패/취소: " + item.itemId);
                });
                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 결제 모듈 미연동(에디터/개발 빌드) — 테스트 편의로 즉시 지급.
            GrantGemPackage(item);
            return true;
#else
            // 프로덕션에서 결제 모듈이 없으면 구매 불가 — 무료 지급 금지(Play 정책).
            Debug.LogWarning("[CashShop] 결제 모듈 미초기화 — 보석 구매 불가");
            return false;
#endif
        }

        // UI 표시용 가격 — 실결제 모듈이 주는 현지화 가격 우선(Google 청구액과 일치), 없으면 priceKRW 폴백.
        public string GetRealMoneyPriceText(string itemId)
        {
            CashShopItem item = GetItem(itemId);
            if (item == null) return "";
            if (purchaseProvider != null && purchaseProvider.IsReady)
            {
                string p = purchaseProvider.GetLocalizedPrice(itemId);
                if (!string.IsNullOrEmpty(p)) return p;
            }
            return item.priceKRW.ToString("N0") + "원"; // 예: 2,000원
        }

        /// <summary>
        /// 서버가 Google Play 구매 토큰을 검증하고 Firestore에서 원자적으로 계산한 잔액을 반영한다.
        /// 클라이언트가 rewardCount를 직접 더하지 않아 재전달/재시작 시 중복 지급되지 않는다.
        /// </summary>
        public bool ApplyVerifiedGemBalance(string productId, int verifiedBalance)
        {
            CashShopItem item = GetItem(productId);
            if (item == null || item.priceKRW <= 0 || !item.itemId.StartsWith("gem_"))
                return false;

            int safeBalance = Mathf.Max(0, verifiedBalance);
            if (wallet != null)
            {
                wallet.SetGems(safeBalance);
                gems = wallet.Gems;
            }
            else
            {
                gems = safeBalance;
                SaveGems();
            }

            GemsChanged?.Invoke();
            ItemPurchased?.Invoke(item);
            return true;
        }

        // 에디터/개발 빌드 전용 가상 지급. 릴리스 실결제는 ApplyVerifiedGemBalance만 사용한다.
        private void GrantGemPackage(CashShopItem item)
        {
            AddGems(item.rewardCount);
            if (CloudSaveManager.Instance != null)
                CloudSaveManager.Instance.SaveToCloud();
            ItemPurchased?.Invoke(item);
        }

        // -- 보석으로 아이템 구매 --
        public bool PurchaseWithGems(string itemId)
        {
            CashShopItem item = GetItem(itemId);
            if (item == null || item.gemPrice <= 0) return false;

            bool isMaster = AuthManager.Instance != null && AuthManager.Instance.IsMasterAccount;
            // Gems 프로퍼티 사용 — wallet 우선 (single source of truth).
            // 옛은 stale gems 캐시 사용으로 외부 wallet 변경 후 잘못된 검사 가능.
            if (!isMaster && Gems < item.gemPrice) return false;

            // 1단계: 지급 가능 여부 사전 검증 (수령 시스템 존재 확인). 차감 전이라 환불 불필요.
            bool deliverable = true;
            if (item.category == CashItemCategory.GachaBox)
            {
                deliverable = GachaBoxManager.Instance != null;
            }
            else if (!string.IsNullOrEmpty(item.rewardItemId))
            {
                if (item.rewardItemId == "candy")
                    deliverable = FindFirstObjectByType<PlayerCandyInventory>() != null;
                else
                    deliverable = FindFirstObjectByType<PlayerItemInventory>() != null;
            }
            if (!deliverable) return false;

            // 2단계: 결제 (보석 차감) — AddGems(-...) 경로로 wallet 동기화 보장.
            // Gems 프로퍼티로 환불 기준값 캡처 (wallet 변경 시 stale 방지).
            int gemsBefore = Gems;
            if (!isMaster) AddGems(-item.gemPrice);

            // 3단계: 지급. 실패 시 환불.
            bool delivered = false;
            try
            {
                if (item.category == CashItemCategory.GachaBox)
                {
                    GachaBoxManager.Instance.OpenBox(item.itemId);
                    delivered = true;
                }
                else if (!string.IsNullOrEmpty(item.rewardItemId))
                {
                    if (item.rewardItemId == "candy")
                    {
                        PlayerCandyInventory candy = FindFirstObjectByType<PlayerCandyInventory>();
                        if (candy != null) { candy.AddCandy(item.rewardCount); delivered = true; }
                    }
                    else
                    {
                        PlayerItemInventory inv = FindFirstObjectByType<PlayerItemInventory>();
                        if (inv != null) { inv.AddItem(item.rewardItemId, item.rewardCount); delivered = true; }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CashShop] 지급 실패 — 환불 진행: {e.Message}");
                delivered = false;
            }

            if (!delivered)
            {
                // 환불: AddGems 양수 경로로 wallet 동기화 보장. gemsBefore - 현재 Gems 차이만큼 복원.
                int refund = gemsBefore - Gems;
                if (refund > 0) AddGems(refund);
                return false;
            }

            ItemPurchased?.Invoke(item);
            return true;
        }

        public void AddGems(int amount)
        {
            // amount 0 early return — 무의미한 GemsChanged 발화 + 구독자 갱신 차단.
            if (amount == 0) return;

            // wallet 단일 소스 경로. wallet 있으면 wallet으로만 변경, 캐시는 read-back.
            if (wallet != null)
            {
                if (amount > 0) wallet.AddGems(amount);
                else wallet.SpendGems(-amount);
                gems = wallet.Gems;
            }
            else
            {
                // AutoWire 전(매우 짧은 부트스트랩 구간) fallback — PlayerPrefs 캐시
                gems += amount;
                SaveGems();
            }
            GemsChanged?.Invoke();
        }

        private void SaveGems()
        {
            PlayerPrefs.SetInt(GemsKey, gems);
            PlayerPrefs.Save();
        }

        public CashShopItem GetItem(string itemId)
        {
            if (shopItems == null) return null;
            foreach (var item in shopItems)
                if (item.itemId == itemId) return item;
            return null;
        }

        public CashShopItem[] GetItemsByCategory(CashItemCategory category)
        {
            List<CashShopItem> result = new List<CashShopItem>();
            if (shopItems == null) return result.ToArray();
            foreach (var item in shopItems)
                if (item.category == category) result.Add(item);
            return result.ToArray();
        }

        public CashShopItem[] GetGemPackages()
        {
            List<CashShopItem> result = new List<CashShopItem>();
            if (shopItems == null) return result.ToArray();
            foreach (var item in shopItems)
                if (item.priceKRW > 0) result.Add(item);
            return result.ToArray();
        }
    }
}
