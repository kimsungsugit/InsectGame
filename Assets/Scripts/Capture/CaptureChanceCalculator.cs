using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Capture
{
    /// <summary>CaptureController의 직렬화 튜닝값을 순수 계산기에 전달하는 불변 스냅샷.</summary>
    internal readonly struct CaptureChanceTuning
    {
        internal const float DefaultBaseSuccessChance = 0.60f;
        internal const float DefaultRarityPenaltyStep = 0.08f;
        internal const float DefaultDifficultyPenaltyScale = 0.40f;
        internal const float DefaultPerfectTimingBonus = 0.15f;
        internal const float DefaultTimingWindow = 0.15f;
        internal const float DefaultPlayerLevelBonusStep = 0.02f;
        internal const float DefaultEnemyLevelPenaltyStep = 0.03f;

        public float BaseSuccessChance { get; }
        public float RarityPenaltyStep { get; }
        public float DifficultyPenaltyScale { get; }
        public float PerfectTimingBonus { get; }
        public float TimingWindow { get; }
        public float PlayerLevelBonusStep { get; }
        public float EnemyLevelPenaltyStep { get; }

        public CaptureChanceTuning(
            float baseSuccessChance,
            float rarityPenaltyStep,
            float difficultyPenaltyScale,
            float perfectTimingBonus,
            float timingWindow,
            float playerLevelBonusStep,
            float enemyLevelPenaltyStep)
        {
            BaseSuccessChance = Mathf.Clamp01(baseSuccessChance);
            RarityPenaltyStep = Mathf.Max(0f, rarityPenaltyStep);
            DifficultyPenaltyScale = Mathf.Clamp01(difficultyPenaltyScale);
            PerfectTimingBonus = Mathf.Max(0f, perfectTimingBonus);
            TimingWindow = Mathf.Clamp(timingWindow, 0f, 0.5f);
            PlayerLevelBonusStep = Mathf.Max(0f, playerLevelBonusStep);
            EnemyLevelPenaltyStep = Mathf.Max(0f, enemyLevelPenaltyStep);
        }

        public static CaptureChanceTuning Default => new CaptureChanceTuning(
            DefaultBaseSuccessChance,
            DefaultRarityPenaltyStep,
            DefaultDifficultyPenaltyScale,
            DefaultPerfectTimingBonus,
            DefaultTimingWindow,
            DefaultPlayerLevelBonusStep,
            DefaultEnemyLevelPenaltyStep);
    }

    /// <summary>
    /// 일반 포획 공식. 레어도별 낮은 floor를 먼저 보장한 뒤 아이템·장비·타이밍·
    /// 미니게임 보너스를 더해, 음수 코어 확률이 보너스를 삼키지 않도록 한다.
    /// </summary>
    internal static class CaptureChanceCalculator
    {
        /// <summary>
        /// 레벨 차가 공식을 압도하지 못하게 하는 상한.
        /// <see cref="InsectGame.Battle.BattleCaptureChanceCalculator"/>의 MaximumLevelDelta와
        /// 같은 값이다 — 같은 규칙의 두 구현이고, 전투 쪽에만 있었다.
        ///
        /// 없을 때 무슨 일이 생겼나: 메인 필드는 리전이 아무리 높아도 **항상 Lv.1부터** 스폰한다
        /// (InsectSpawner의 지수 분포). 그래서 Lv.40 플레이어가 Lv.2 전설을 만나면
        /// +38×0.02 = +0.76이 붙어 등급 페널티(0.32)와 난이도 페널티(0.40)를 통째로 상쇄했다.
        /// 미니게임이 주 포획 경로인데 고레벨 구간에서 등급·난이도 설계가 통째로 무력화됐다.
        /// </summary>
        internal const int MaximumLevelDelta = 5;

        internal static float Calculate(
            InsectRarity rarity,
            float captureDifficulty,
            int playerLevel,
            int insectLevel,
            float timing01,
            float activeItemBonus,
            float outfitBonus,
            float minigameBonus,
            CaptureChanceTuning tuning)
        {
            int rarityIndex = Mathf.Clamp(
                (int)rarity, (int)InsectRarity.Common, (int)InsectRarity.Legendary);

            float coreChance = tuning.BaseSuccessChance;
            coreChance -= rarityIndex * tuning.RarityPenaltyStep;
            coreChance -= Mathf.Clamp01(captureDifficulty) * tuning.DifficultyPenaltyScale;
            coreChance += GetLevelModifier(
                playerLevel, insectLevel,
                tuning.PlayerLevelBonusStep, tuning.EnemyLevelPenaltyStep);

            float chance = Mathf.Max(coreChance, GetRarityFloor((InsectRarity)rarityIndex));
            chance += Mathf.Max(0f, activeItemBonus);
            chance += Mathf.Max(0f, outfitBonus);

            if (Mathf.Abs(timing01 - 0.5f) <= tuning.TimingWindow)
                chance += tuning.PerfectTimingBonus;

            chance += Mathf.Max(0f, minigameBonus);
            return Mathf.Clamp01(chance);
        }

        internal static float GetRarityFloor(InsectRarity rarity)
        {
            switch (rarity)
            {
                case InsectRarity.Common: return 0.30f;
                case InsectRarity.Uncommon: return 0.22f;
                case InsectRarity.Rare: return 0.14f;
                case InsectRarity.Epic: return 0.08f;
                case InsectRarity.Legendary: return 0.04f;
                default: return 0.04f;
            }
        }

        internal static float GetLevelModifier(
            int playerLevel,
            int insectLevel,
            float playerLevelBonusStep,
            float enemyLevelPenaltyStep)
        {
            int diff = Mathf.Clamp(
                Mathf.Max(1, playerLevel) - Mathf.Max(1, insectLevel),
                -MaximumLevelDelta,
                MaximumLevelDelta);
            return diff >= 0
                ? diff * Mathf.Max(0f, playerLevelBonusStep)
                : diff * Mathf.Max(0f, enemyLevelPenaltyStep);
        }
    }
}
