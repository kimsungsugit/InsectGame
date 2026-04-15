using UnityEngine;

namespace InsectGame.Data
{
    [CreateAssetMenu(menuName = "InsectGame/Item Rarity Palette", fileName = "ItemRarityPalette")]
    public class ItemRarityPalette : ScriptableObject
    {
        public Color commonColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        public Color uncommonColor = new Color(0.5f, 0.9f, 0.5f, 1f);
        public Color rareColor = new Color(0.4f, 0.7f, 1f, 1f);
        public Color epicColor = new Color(0.8f, 0.4f, 1f, 1f);
        public Color legendaryColor = new Color(1f, 0.75f, 0.2f, 1f);
        [Header("Particle Gradients")]
        public Gradient commonGradient;
        public Gradient uncommonGradient;
        public Gradient rareGradient;
        public Gradient epicGradient;
        public Gradient legendaryGradient;
        [Header("Pulse Strength")]
        [Range(0f, 0.5f)] public float commonPulse = 0.05f;
        [Range(0f, 0.5f)] public float uncommonPulse = 0.08f;
        [Range(0f, 0.5f)] public float rarePulse = 0.12f;
        [Range(0f, 0.5f)] public float epicPulse = 0.18f;
        [Range(0f, 0.5f)] public float legendaryPulse = 0.25f;

        [Header("Border Thickness")]
        public float commonBorderThickness = 1f;
        public float uncommonBorderThickness = 1f;
        public float rareBorderThickness = 2f;
        public float epicBorderThickness = 2f;
        public float legendaryBorderThickness = 3f;

        [Header("Glow Intensity")]
        [Range(0f, 1f)] public float commonGlowIntensity = 0f;
        [Range(0f, 1f)] public float uncommonGlowIntensity = 0f;
        [Range(0f, 1f)] public float rareGlowIntensity = 0.3f;
        [Range(0f, 1f)] public float epicGlowIntensity = 0.5f;
        [Range(0f, 1f)] public float legendaryGlowIntensity = 0.8f;

        [Header("Animation Speed")]
        public float commonAnimSpeed = 0f;
        public float uncommonAnimSpeed = 0f;
        public float rareAnimSpeed = 1f;
        public float epicAnimSpeed = 2f;
        public float legendaryAnimSpeed = 3f;

        public Color GetColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon:
                    return uncommonColor;
                case ItemRarity.Rare:
                    return rareColor;
                case ItemRarity.Epic:
                    return epicColor;
                case ItemRarity.Legendary:
                    return legendaryColor;
                default:
                    return commonColor;
            }
        }

        public float GetPulseStrength(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon:
                    return uncommonPulse;
                case ItemRarity.Rare:
                    return rarePulse;
                case ItemRarity.Epic:
                    return epicPulse;
                case ItemRarity.Legendary:
                    return legendaryPulse;
                default:
                    return commonPulse;
            }
        }

        public Gradient GetGradient(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon:
                    return uncommonGradient;
                case ItemRarity.Rare:
                    return rareGradient;
                case ItemRarity.Epic:
                    return epicGradient;
                case ItemRarity.Legendary:
                    return legendaryGradient;
                default:
                    return commonGradient;
            }
        }

        public float GetBorderThickness(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return uncommonBorderThickness;
                case ItemRarity.Rare: return rareBorderThickness;
                case ItemRarity.Epic: return epicBorderThickness;
                case ItemRarity.Legendary: return legendaryBorderThickness;
                default: return commonBorderThickness;
            }
        }

        public float GetGlowIntensity(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return uncommonGlowIntensity;
                case ItemRarity.Rare: return rareGlowIntensity;
                case ItemRarity.Epic: return epicGlowIntensity;
                case ItemRarity.Legendary: return legendaryGlowIntensity;
                default: return commonGlowIntensity;
            }
        }

        public float GetAnimSpeed(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return uncommonAnimSpeed;
                case ItemRarity.Rare: return rareAnimSpeed;
                case ItemRarity.Epic: return epicAnimSpeed;
                case ItemRarity.Legendary: return legendaryAnimSpeed;
                default: return commonAnimSpeed;
            }
        }
    }
}
