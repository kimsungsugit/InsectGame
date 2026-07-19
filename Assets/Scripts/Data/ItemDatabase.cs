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

                // --- 슈퍼 아이템(Epic/Legendary 강화 묶음) — 유료(캐시샵) + 후반 퀘스트 보상으로만 획득 ---
                // 밸런스: 동시 1개만 활성(ItemEffectManager.activeItem 단일) → 효과 비중첩 전제.
                CreateItem("golden_censer", "황금 향로",
                    "15분 동안 희귀 곤충 출현율이 크게 오르고(×2.3) 포획 확률이 +25% 증가합니다.",
                    ItemRarity.Legendary, 0.25f, 1f, 1f, 2.3f, 900f),
                CreateItem("spirit_blessing", "정령의 가호",
                    "10분 동안 전투 공격력(+45%)과 방어력(피해 감소)이 함께 상승합니다.",
                    ItemRarity.Legendary, 0f, 1f, 1f, 1f, 600f, atkBonus: 0.45f, defBonus: 0.35f),
                CreateItem("binding_net", "포박의 그물",
                    "10분 동안 포획 확률이 +35% 증가하고 야생 곤충의 도주를 거의 막습니다.",
                    ItemRarity.Epic, 0.35f, 1f, 1f, 1f, 600f, fleePreventChance: 0.8f),
                CreateItem("beast_mark", "맹수의 표식",
                    "10분 동안 전투 공격력이 +35% 상승합니다.",
                    ItemRarity.Epic, 0f, 1f, 1f, 1f, 600f, atkBonus: 0.35f),
                CreateItem("guardian_totem", "수호의 토템",
                    "12분 동안 전투에서 받는 피해가 크게 줄어듭니다(방어 +40%).",
                    ItemRarity.Epic, 0f, 1f, 1f, 1f, 720f, defBonus: 0.4f),
            };
            return database;
        }

        private static ItemData CreateItem(string id, string name, string description,
            ItemRarity rarity, float captureBonus, float expMultiplier,
            float candyMultiplier, float rareSpawnMultiplier, float durationSeconds,
            float atkBonus = 0f, float defBonus = 0f, float fleePreventChance = 0f)
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
            item.atkBonus = atkBonus;
            item.defBonus = defBonus;
            item.fleePreventChance = fleePreventChance;
            return item;
        }
    }
}
