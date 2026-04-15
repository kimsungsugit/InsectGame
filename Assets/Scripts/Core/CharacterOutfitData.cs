using UnityEngine;

namespace InsectGame.Core
{
    public enum OutfitSlot
    {
        Hat,
        Top,
        Bottom,
        Outerwear,
        Shoes,
        Backpack,
        Tool,
        Accessory
    }

    [System.Serializable]
    public struct OutfitStatBonus
    {
        public float captureChanceBonus;   // +0.02 = 포획률 +2%
        public float expMultiplier;        // +0.05 = 경험치 +5%
        public float candyMultiplier;      // +0.03 = 캔디 +3%
        public float rareSpawnBonus;       // +0.05 = 레어 스폰 +5%
        public float moveSpeedBonus;       // +0.10 = 이속 +10%
        public float atkBonus;             // +0.03 = ATK +3%
        public float defBonus;             // +0.02 = DEF +2%

        public static OutfitStatBonus operator +(OutfitStatBonus a, OutfitStatBonus b)
        {
            return new OutfitStatBonus
            {
                captureChanceBonus = a.captureChanceBonus + b.captureChanceBonus,
                expMultiplier = a.expMultiplier + b.expMultiplier,
                candyMultiplier = a.candyMultiplier + b.candyMultiplier,
                rareSpawnBonus = a.rareSpawnBonus + b.rareSpawnBonus,
                moveSpeedBonus = a.moveSpeedBonus + b.moveSpeedBonus,
                atkBonus = a.atkBonus + b.atkBonus,
                defBonus = a.defBonus + b.defBonus
            };
        }

        public bool HasAnyBonus()
        {
            return captureChanceBonus != 0f || expMultiplier != 0f || candyMultiplier != 0f
                || rareSpawnBonus != 0f || moveSpeedBonus != 0f || atkBonus != 0f || defBonus != 0f;
        }

        public string GetPrimaryBonusText()
        {
            if (captureChanceBonus > 0f) return $"포획 +{captureChanceBonus * 100f:0}%";
            if (atkBonus > 0f) return $"ATK +{atkBonus * 100f:0}%";
            if (defBonus > 0f) return $"DEF +{defBonus * 100f:0}%";
            if (moveSpeedBonus > 0f) return $"이속 +{moveSpeedBonus * 100f:0}%";
            if (expMultiplier > 0f) return $"경험치 +{expMultiplier * 100f:0}%";
            if (candyMultiplier > 0f) return $"캔디 +{candyMultiplier * 100f:0}%";
            if (rareSpawnBonus > 0f) return $"레어 +{rareSpawnBonus * 100f:0}%";
            return "";
        }
    }

    [System.Serializable]
    public class OutfitItem
    {
        public string itemId;
        public string displayName;
        public string description;
        public OutfitSlot slot;
        public Color primaryColor;
        public Color secondaryColor;
        public int price;              // 캔디 가격 (0이면 캔디로 구매 불가)
        public int gemPrice;            // 보석 가격 (0이면 보석으로 구매 불가)
        public bool isPremium;          // 프리미엄 의상 여부
        public bool unlockedByDefault;
        public string unlockCondition;
        public OutfitStatBonus statBonus;
    }
}
