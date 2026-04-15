using UnityEngine;

namespace InsectGame.Data
{
    [CreateAssetMenu(menuName = "InsectGame/Insect Level Curve", fileName = "InsectLevelCurve")]
    public class InsectLevelCurve : ScriptableObject
    {
        [Range(1, 100)] public int maxLevel = 50;
        [Range(1, 999)] public int baseXpToLevel = 20;
        [Range(0, 999)] public int xpGrowthPerLevel = 8;
        [Range(1, 999)] public int baseCandyCost = 4;
        [Range(0, 999)] public int candyCostGrowth = 2;

        public int GetXpToNextLevel(int level)
        {
            // 지수 곡선: base × 1.12^(level-1) — 레벨당 12% 복리 증가
            // Lv1: 20 → Lv10: 55 → Lv20: 173 → Lv30: 539 → Lv40: 1679 → Lv50: 5228
            int xp = Mathf.RoundToInt(baseXpToLevel * Mathf.Pow(1.12f, level - 1));
            return Mathf.Max(1, xp);
        }

        public int GetCandyCost(int level)
        {
            // 지수 곡선: base × 1.14^(level-1) — 레벨당 14% 복리 증가
            // Lv1: 4 → Lv10: 13 → Lv20: 49 → Lv30: 181 → Lv40: 669 → Lv50: 2475
            int cost = Mathf.RoundToInt(baseCandyCost * Mathf.Pow(1.14f, level - 1));
            return Mathf.Max(1, cost);
        }
    }
}
