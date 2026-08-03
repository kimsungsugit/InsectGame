using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 일반 1v1 전투 승리 후 포획 확률을 계산한다.
    /// 전투 보상과 승패는 이 결과와 무관하며, 성공한 경우에만 보유 목록과 도감 포획을 등록한다.
    /// </summary>
    internal static class BattleCaptureChanceCalculator
    {
        internal const float BaseSuccessChance = 0.90f;
        internal const float RarityPenaltyStep = 0.07f;
        internal const float DifficultyPenaltyScale = 0.50f;
        internal const float PlayerLevelBonusStep = 0.02f;
        internal const float EnemyLevelPenaltyStep = 0.03f;
        internal const int MaximumLevelDelta = 5;
        internal const float MinimumSuccessChance = 0.10f;
        internal const float MaximumSuccessChance = 0.95f;

        internal static float Calculate(
            InsectRarity rarity,
            float captureDifficulty,
            int playerLevel,
            int insectLevel,
            float activeItemBonus,
            float outfitBonus)
        {
            int rarityIndex = Mathf.Clamp(
                (int)rarity,
                (int)InsectRarity.Common,
                (int)InsectRarity.Legendary);

            float chance = BaseSuccessChance;
            chance -= rarityIndex * RarityPenaltyStep;
            chance -= Mathf.Clamp01(captureDifficulty) * DifficultyPenaltyScale;
            chance += GetLevelModifier(playerLevel, insectLevel);
            chance += Mathf.Max(0f, activeItemBonus);
            chance += Mathf.Max(0f, outfitBonus);

            return Mathf.Clamp(chance, MinimumSuccessChance, MaximumSuccessChance);
        }

        internal static float GetLevelModifier(int playerLevel, int insectLevel)
        {
            int levelDelta = Mathf.Clamp(
                Mathf.Max(1, playerLevel) - Mathf.Max(1, insectLevel),
                -MaximumLevelDelta,
                MaximumLevelDelta);

            return levelDelta >= 0
                ? levelDelta * PlayerLevelBonusStep
                : levelDelta * EnemyLevelPenaltyStep;
        }

        internal static bool IsSuccessful(float chance, float roll)
        {
            return roll < Mathf.Clamp01(chance);
        }
    }
}
