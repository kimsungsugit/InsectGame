using UnityEngine;
using InsectGame.Core;

namespace InsectGame.Data
{
    [System.Serializable]
    public class OutfitSetDefinition
    {
        public string setId;
        public string displayName;
        public string description;
        public string[] requiredItemIds;
        public int partialThreshold;
        public OutfitStatBonus partialBonus;
        public OutfitStatBonus fullBonus;
        public Color setColor;
    }

    public static class OutfitSetCatalog
    {
        public static OutfitSetDefinition[] GetAllSets()
        {
            return new[]
            {
                new OutfitSetDefinition
                {
                    setId = "set_jungle",
                    displayName = "정글 탐험가",
                    description = "정글 탐험에 최적화된 장비 세트",
                    requiredItemIds = new[] { "hat_safari", "top_vest", "bot_cargo", "outer_jacket", "shoe_boots" },
                    partialThreshold = 3,
                    partialBonus = new OutfitStatBonus { captureChanceBonus = 0.03f, moveSpeedBonus = 0.05f },
                    fullBonus = new OutfitStatBonus { captureChanceBonus = 0.05f, moveSpeedBonus = 0.10f },
                    setColor = new Color(0.4f, 0.7f, 0.3f)
                },
                new OutfitSetDefinition
                {
                    setId = "set_researcher",
                    displayName = "곤충 연구원",
                    description = "연구에 특화된 학자 세트",
                    requiredItemIds = new[] { "hat_straw", "top_lab", "bot_overalls", "outer_labcoat", "bag_science" },
                    partialThreshold = 3,
                    partialBonus = new OutfitStatBonus { expMultiplier = 0.08f },
                    fullBonus = new OutfitStatBonus { expMultiplier = 0.15f },
                    setColor = new Color(0.9f, 0.9f, 0.95f)
                },
                new OutfitSetDefinition
                {
                    setId = "set_galaxy",
                    displayName = "갤럭시",
                    description = "우주의 신비로운 기운이 깃든 세트",
                    requiredItemIds = new[] { "hat_butterfly_wing", "top_galaxy", "bot_galaxy", "outer_crystal", "shoe_crystal" },
                    partialThreshold = 3,
                    partialBonus = new OutfitStatBonus { rareSpawnBonus = 0.05f },
                    fullBonus = new OutfitStatBonus { rareSpawnBonus = 0.10f },
                    setColor = new Color(0.5f, 0.3f, 0.9f)
                },
                new OutfitSetDefinition
                {
                    setId = "set_legend",
                    displayName = "전설의 사냥꾼",
                    description = "전설의 곤충 사냥꾼만이 갖출 수 있는 세트",
                    requiredItemIds = new[] { "hat_crown", "top_flame", "bot_golden", "outer_legendary", "tool_diamond_net" },
                    partialThreshold = 3,
                    partialBonus = new OutfitStatBonus { captureChanceBonus = 0.04f, atkBonus = 0.03f },
                    fullBonus = new OutfitStatBonus { captureChanceBonus = 0.08f, atkBonus = 0.05f },
                    setColor = new Color(1f, 0.84f, 0f)
                }
            };
        }
    }
}
