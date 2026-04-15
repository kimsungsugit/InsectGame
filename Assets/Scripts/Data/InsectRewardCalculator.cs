namespace InsectGame.Data
{
    public static class InsectRewardCalculator
    {
        public static int GetExpReward(InsectData data)
        {
            if (data == null)
            {
                return 0;
            }

            float multiplier = GetRarityMultiplier(data.rarity);
            int reward = (int)(data.expReward * multiplier);
            return reward < 0 ? 0 : reward;
        }

        public static int GetCandyReward(InsectData data)
        {
            if (data == null)
            {
                return 0;
            }

            float multiplier = GetRarityMultiplier(data.rarity);
            int reward = (int)(data.candyReward * multiplier);
            return reward < 0 ? 0 : reward;
        }

        public static int GetItemRewardCount(InsectData data)
        {
            if (data == null)
            {
                return 0;
            }

            float multiplier = GetRarityMultiplier(data.rarity);
            int reward = (int)(data.itemRewardCount * multiplier);
            return reward < 0 ? 0 : reward;
        }

        private static float GetRarityMultiplier(InsectRarity rarity)
        {
            switch (rarity)
            {
                case InsectRarity.Common:
                    return 1f;
                case InsectRarity.Uncommon:
                    return 1.2f;
                case InsectRarity.Rare:
                    return 1.5f;
                case InsectRarity.Epic:
                    return 2.0f;
                case InsectRarity.Legendary:
                    return 2.8f;
                default:
                    return 1f;
            }
        }
    }
}
