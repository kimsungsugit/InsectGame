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
                },
                new OutfitSetDefinition
                {
                    setId = "set_cowboy",
                    displayName = "서부의 총잡이",
                    description = "서부 개척시대 카우보이 풀 세트",
                    requiredItemIds = new[] { "hat_cowboy", "top_cowboy", "bot_cowboy", "shoe_cowboy", "tool_lasso", "acc_bandana" },
                    partialThreshold = 3,
                    partialBonus = new OutfitStatBonus { captureChanceBonus = 0.03f, moveSpeedBonus = 0.04f },
                    fullBonus = new OutfitStatBonus { captureChanceBonus = 0.06f, moveSpeedBonus = 0.08f, atkBonus = 0.02f },
                    setColor = new Color(0.5f, 0.35f, 0.15f)
                },
                new OutfitSetDefinition
                {
                    setId = "set_hero",
                    displayName = "슈퍼 히어로",
                    description = "정의의 거미줄로 곤충을 포획하는 히어로",
                    requiredItemIds = new[] { "hat_hero_mask", "top_hero_suit", "bot_hero_suit", "tool_web_shooter", "acc_spider_emblem" },
                    partialThreshold = 3,
                    partialBonus = new OutfitStatBonus { atkBonus = 0.04f, moveSpeedBonus = 0.04f },
                    fullBonus = new OutfitStatBonus { atkBonus = 0.06f, moveSpeedBonus = 0.08f, captureChanceBonus = 0.04f },
                    setColor = new Color(0.8f, 0.1f, 0.1f)
                },
                new OutfitSetDefinition
                {
                    setId = "set_ninja",
                    displayName = "그림자 닌자",
                    description = "어둠 속에서 곤충을 포획하는 닌자",
                    requiredItemIds = new[] { "hat_ninja", "top_ninja", "bot_ninja", "tool_shuriken", "acc_ninja_scarf" },
                    partialThreshold = 3,
                    partialBonus = new OutfitStatBonus { moveSpeedBonus = 0.06f, atkBonus = 0.02f },
                    fullBonus = new OutfitStatBonus { moveSpeedBonus = 0.10f, atkBonus = 0.04f, captureChanceBonus = 0.03f },
                    setColor = new Color(0.15f, 0.1f, 0.25f)
                },
                new OutfitSetDefinition
                {
                    setId = "set_pirate",
                    displayName = "곤충 해적단",
                    description = "보물 곤충을 찾아 항해하는 해적",
                    requiredItemIds = new[] { "hat_pirate", "top_pirate", "bot_pirate", "tool_cutlass", "acc_eyepatch" },
                    partialThreshold = 3,
                    partialBonus = new OutfitStatBonus { atkBonus = 0.03f, candyMultiplier = 0.04f },
                    fullBonus = new OutfitStatBonus { atkBonus = 0.05f, candyMultiplier = 0.08f, captureChanceBonus = 0.03f },
                    setColor = new Color(0.1f, 0.1f, 0.1f)
                },
                new OutfitSetDefinition
                {
                    setId = "set_cyber",
                    displayName = "사이버 헌터",
                    description = "미래 기술로 레어 곤충을 감지하는 사이버 전사",
                    requiredItemIds = new[] { "hat_cyber_visor", "top_cyber", "bot_cyber", "tool_blaster", "acc_neon_ring" },
                    partialThreshold = 3,
                    partialBonus = new OutfitStatBonus { rareSpawnBonus = 0.04f, captureChanceBonus = 0.03f },
                    fullBonus = new OutfitStatBonus { rareSpawnBonus = 0.08f, captureChanceBonus = 0.05f, atkBonus = 0.02f },
                    setColor = new Color(0f, 0.9f, 1f)
                },
                new OutfitSetDefinition
                {
                    setId = "set_wizard",
                    displayName = "곤충 마법사",
                    description = "마법의 힘으로 곤충을 매혹하는 마법사",
                    requiredItemIds = new[] { "hat_wizard", "outer_wizard", "tool_wand", "acc_crystal_orb" },
                    partialThreshold = 2,
                    partialBonus = new OutfitStatBonus { rareSpawnBonus = 0.04f, captureChanceBonus = 0.03f },
                    fullBonus = new OutfitStatBonus { rareSpawnBonus = 0.07f, captureChanceBonus = 0.06f, expMultiplier = 0.03f },
                    setColor = new Color(0.4f, 0.2f, 0.7f)
                },
                new OutfitSetDefinition
                {
                    setId = "set_military",
                    displayName = "곤충 특공대",
                    description = "군사 작전으로 곤충을 포획하는 특수부대",
                    requiredItemIds = new[] { "hat_military", "top_military", "bot_military", "tool_tranq_gun", "acc_dog_tag" },
                    partialThreshold = 3,
                    partialBonus = new OutfitStatBonus { defBonus = 0.04f, captureChanceBonus = 0.03f },
                    fullBonus = new OutfitStatBonus { defBonus = 0.06f, captureChanceBonus = 0.06f, atkBonus = 0.03f },
                    setColor = new Color(0.3f, 0.35f, 0.2f)
                }
            };
        }
    }
}
