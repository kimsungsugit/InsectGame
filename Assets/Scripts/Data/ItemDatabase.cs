using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Data
{
    [CreateAssetMenu(menuName = "InsectGame/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        public List<ItemData> items = new List<ItemData>();

        public ItemData FindById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            return items.Find(item => item != null && item.itemId == itemId);
        }

        /// <summary>
        /// Resources/ItemDatabase 에셋이 없는 빌드에서도 상점/인벤토리 핵심 아이템이 동작하는 폴백.
        /// 런타임 ScriptableObject라 .meta/에셋 의존성이 없다.
        /// </summary>
        public static ItemDatabase CreateRuntimeDefault()
        {
            ItemDatabase database = ScriptableObject.CreateInstance<ItemDatabase>();
            database.items = new List<ItemData>
            {
                CreateItem("net_silver", "은빛 채집망", "포획 확률이 15% 증가합니다.",
                    ItemRarity.Rare, 0.15f, 1f, 1f, 1f, 600f),
                CreateItem("net_gold", "황금 채집망", "포획 확률이 30% 증가합니다.",
                    ItemRarity.Epic, 0.30f, 1f, 1f, 1f, 600f),
                CreateItem("exp_boost", "경험치 부스터", "10분 동안 경험치를 2배 획득합니다.",
                    ItemRarity.Rare, 0f, 2f, 1f, 1f, 600f),
            };
            return database;
        }

        private static ItemData CreateItem(string id, string name, string description,
            ItemRarity rarity, float captureBonus, float expMultiplier,
            float candyMultiplier, float rareSpawnMultiplier, float durationSeconds)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = id;
            item.displayName = name;
            item.description = description;
            item.rarity = rarity;
            item.captureChanceBonus = captureBonus;
            item.expMultiplier = expMultiplier;
            item.candyMultiplier = candyMultiplier;
            item.rareSpawnMultiplier = rareSpawnMultiplier;
            item.durationSeconds = durationSeconds;
            return item;
        }
    }
}
