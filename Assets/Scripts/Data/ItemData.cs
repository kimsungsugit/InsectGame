using UnityEngine;

namespace InsectGame.Data
{
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    [CreateAssetMenu(menuName = "InsectGame/Item Data", fileName = "ItemData")]
    public class ItemData : ScriptableObject
    {
        public string itemId;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;
        public ItemRarity rarity = ItemRarity.Common;
        public Sprite rarityIcon;

        [Header("Capture Effects")]
        [Range(0f, 0.5f)] public float captureChanceBonus = 0.1f;
        [Range(1f, 3f)] public float expMultiplier = 1.0f;
        [Range(1f, 3f)] public float candyMultiplier = 1.0f;
        [Range(1f, 3f)] public float rareSpawnMultiplier = 1.0f;

        [Header("Duration")]
        [Range(30f, 3600f)] public float durationSeconds = 600f;
    }
}
