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

        public int Gems => gems;

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
                new CashShopItem { itemId = "shop_rare_incense", displayName = "희귀 향로",     description = "10분간 Rare+ 출현율 2배",  category = CashItemCategory.MinigameItem, priceKRW = 0, gemPrice = 350, rewardItemId = "incense_rare", rewardCount = 1 },
                new CashShopItem { itemId = "shop_candy_pack",   displayName = "캔디 대량팩",   description = "캔디 50개",                category = CashItemCategory.MinigameItem, priceKRW = 0, gemPrice = 300, rewardItemId = "candy",        rewardCount = 50 },
                new CashShopItem { itemId = "shop_exp_boost",    displayName = "경험치 부스터", description = "10분간 경험치 2배",        category = CashItemCategory.MinigameItem, priceKRW = 0, gemPrice = 300, rewardItemId = "exp_boost",    rewardCount = 1 },

                // -- 랜덤 상자 (보석으로 구매) --
                new CashShopItem { itemId = "box_bronze", displayName = "브론즈 상자", description = "기본 곤충 + 소량 희귀 확률", category = CashItemCategory.GachaBox, priceKRW = 0, gemPrice = 500,  rewardCount = 1 },
                new CashShopItem { itemId = "box_silver", displayName = "실버 상자",   description = "희귀 곤충 확률 UP!",        category = CashItemCategory.GachaBox, priceKRW = 0, gemPrice = 800,  rewardCount = 1 },
                new CashShopItem { itemId = "box_gold",   displayName = "골드 상자",   description = "전설 곤충 확률 대폭 UP!",   category = CashItemCategory.GachaBox, priceKRW = 0, gemPrice = 1200, rewardCount = 1 },
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
            if (!isMaster && gems < item.gemPrice) return false;
            if (!isMaster) gems -= item.gemPrice;
            SaveGems();
            GemsChanged?.Invoke();

            // 아이템 지급
            if (item.category == CashItemCategory.GachaBox)
            {
                if (GachaBoxManager.Instance != null)
                    GachaBoxManager.Instance.OpenBox(item.itemId);
            }
            else if (!string.IsNullOrEmpty(item.rewardItemId))
            {
                if (item.rewardItemId == "candy")
                {
                    PlayerCandyInventory candy = FindFirstObjectByType<PlayerCandyInventory>();
                    if (candy != null) candy.AddCandy(item.rewardCount);
                }
                else
                {
                    PlayerItemInventory inv = FindFirstObjectByType<PlayerItemInventory>();
                    if (inv != null) inv.AddItem(item.rewardItemId, item.rewardCount);
                }
            }

            ItemPurchased?.Invoke(item);
            return true;
        }

        public void AddGems(int amount)
        {
            gems += amount;
            SaveGems();
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
