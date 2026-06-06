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

                // -- 랜덤 상자 (보석으로 구매) --
                new CashShopItem { itemId = "box_bronze", displayName = "브론즈 상자", description = "기본 곤충 + 소량 희귀 확률", category = CashItemCategory.GachaBox, priceKRW = 0, gemPrice = 500, rewardCount = 1 },
                new CashShopItem { itemId = "box_silver", displayName = "실버 상자",   description = "희귀 곤충 확률 UP!",        category = CashItemCategory.GachaBox, priceKRW = 0, gemPrice = 600, rewardCount = 1 },
                new CashShopItem { itemId = "box_gold",   displayName = "골드 상자",   description = "전설 곤충 확률 대폭 UP!",   category = CashItemCategory.GachaBox, priceKRW = 0, gemPrice = 750, rewardCount = 1 },
            };
        }

        // -- 보석 충전 (실제 결제 시뮬레이션) --
        public bool PurchaseWithRealMoney(string itemId)
        {
            CashShopItem item = GetItem(itemId);
            if (item == null || item.priceKRW <= 0) return false;

            // TODO: 실제 결제 연동 (Google Play Billing / Apple IAP)
            // 현재는 바로 지급 (테스트용)

            if (item.itemId.StartsWith("gem_"))
            {
                AddGems(item.rewardCount);
                // 결제 손실 방지: 보석 충전 직후 클라우드 즉시 저장(자동저장 120초 대기 안 함).
                if (CloudSaveManager.Instance != null)
                    CloudSaveManager.Instance.SaveToCloud();
                ItemPurchased?.Invoke(item);
                return true;
            }

            return false;
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
