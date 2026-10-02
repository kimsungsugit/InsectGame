#if UNITY_EDITOR
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.UI;
using NUnit.Framework;

namespace InsectGame.Tests
{
    [TestFixture]
    public class BattleCaptureChanceCalculatorTests
    {
        [TestCase(InsectRarity.Common, 0.18f, 0.81f)]
        [TestCase(InsectRarity.Uncommon, 0.32f, 0.67f)]
        [TestCase(InsectRarity.Rare, 0.50f, 0.51f)]
        [TestCase(InsectRarity.Epic, 0.62f, 0.38f)]
        [TestCase(InsectRarity.Legendary, 0.82f, 0.21f)]
        public void Calculate_EqualLevelRepresentativeRarity_ReturnsExpectedChance(
            InsectRarity rarity,
            float captureDifficulty,
            float expected)
        {
            float actual = Calculate(
                rarity,
                captureDifficulty,
                playerLevel: 10,
                insectLevel: 10);

            Assert.AreEqual(expected, actual, 0.0001f);
        }

        [Test]
        public void Calculate_DifficultyOutsideRange_IsClamped()
        {
            float belowZero = Calculate(InsectRarity.Rare, -1f, 10, 10);
            float zero = Calculate(InsectRarity.Rare, 0f, 10, 10);
            float one = Calculate(InsectRarity.Rare, 1f, 10, 10);
            float aboveOne = Calculate(InsectRarity.Rare, 2f, 10, 10);

            Assert.AreEqual(zero, belowZero, 0.0001f);
            Assert.AreEqual(one, aboveOne, 0.0001f);
            Assert.Greater(zero, one);
        }

        [Test]
        public void Calculate_RarityIncreases_ChanceNeverIncreases()
        {
            float previous = float.MaxValue;
            foreach (InsectRarity rarity in new[]
                     {
                         InsectRarity.Common,
                         InsectRarity.Uncommon,
                         InsectRarity.Rare,
                         InsectRarity.Epic,
                         InsectRarity.Legendary
                     })
            {
                float current = Calculate(rarity, 0.5f, 10, 10);
                Assert.LessOrEqual(current, previous);
                previous = current;
            }
        }

        [Test]
        public void Calculate_InvalidHighRarity_ClampsToLegendary()
        {
            float invalid = Calculate((InsectRarity)999, 0.5f, 10, 10);
            float legendary = Calculate(InsectRarity.Legendary, 0.5f, 10, 10);

            Assert.AreEqual(legendary, invalid, 0.0001f);
        }

        [Test]
        public void GetLevelModifier_AdvantageAndDisadvantage_UseAsymmetricSteps()
        {
            Assert.AreEqual(
                0.06f,
                BattleCaptureChanceCalculator.GetLevelModifier(13, 10),
                0.0001f);
            Assert.AreEqual(
                -0.09f,
                BattleCaptureChanceCalculator.GetLevelModifier(7, 10),
                0.0001f);
        }

        [Test]
        public void GetLevelModifier_DeltaBeyondFive_IsClamped()
        {
            Assert.AreEqual(
                0.10f,
                BattleCaptureChanceCalculator.GetLevelModifier(99, 1),
                0.0001f);
            Assert.AreEqual(
                -0.15f,
                BattleCaptureChanceCalculator.GetLevelModifier(1, 99),
                0.0001f);
        }

        [Test]
        public void Calculate_CaptureBonuses_AddOnlyNonnegativeValues()
        {
            float baseline = Calculate(InsectRarity.Rare, 0.5f, 10, 10);
            float negative = Calculate(
                InsectRarity.Rare,
                0.5f,
                10,
                10,
                activeItemBonus: -0.20f,
                outfitBonus: -0.30f);
            float positive = Calculate(
                InsectRarity.Rare,
                0.5f,
                10,
                10,
                activeItemBonus: 0.10f,
                outfitBonus: 0.05f);

            Assert.AreEqual(baseline, negative, 0.0001f);
            Assert.AreEqual(baseline + 0.15f, positive, 0.0001f);
        }

        [Test]
        public void Calculate_ExtremeInputs_ClampToConfiguredBounds()
        {
            // 최저 보장은 레벨 유예(+5) 안에서의 보장이다 — 그 너머는 아래 테스트가 본다.
            float minimum = Calculate(
                InsectRarity.Legendary,
                1f,
                10,
                10 + GameConstants.TrainerLevel.CaptureGraceLevels);
            float maximum = Calculate(
                InsectRarity.Common,
                0f,
                99,
                1,
                activeItemBonus: 1f,
                outfitBonus: 1f);

            Assert.AreEqual(
                BattleCaptureChanceCalculator.MinimumSuccessChance,
                minimum,
                0.0001f);
            Assert.AreEqual(
                BattleCaptureChanceCalculator.MaximumSuccessChance,
                maximum,
                0.0001f);
        }

        [Test]
        public void Calculate_InsectFarAboveTrainer_GoesBelowMinimumGuarantee()
        {
            // 예전엔 Lv.1 캐릭터가 Lv.99 곤충을 이겨도 최저 10%가 보장됐다.
            // .90 - 레벨 상한 .15 = .75 → 레벨 차 +98은 배율 하한(×0.05) → .0375
            float chance = Calculate(InsectRarity.Common, 0f, 1, 99);

            Assert.Less(chance, BattleCaptureChanceCalculator.MinimumSuccessChance);
            Assert.AreEqual(0.75f * GameConstants.TrainerLevel.MinCaptureMultiplier, chance, 0.0001f);
        }

        [Test]
        public void Calculate_AnyGap_AppliesSharedTrainerLevelGapMultiplier()
        {
            // 미니게임 경로와 같은 TrainerLevelGap 배율을 쓴다 — 경로마다 따로 적으면 한쪽만 고쳐진다.
            // Common·난이도 0은 .90에서 덧셈 보정(최대 -.15)만 빠져 clamp에 닿지 않는다.
            for (int gap = 0; gap <= 20; gap++)
            {
                int trainer = 30 - gap;
                float unscaled = BattleCaptureChanceCalculator.BaseSuccessChance
                                 + BattleCaptureChanceCalculator.GetLevelModifier(trainer, 30);
                Assert.AreEqual(
                    unscaled * TrainerLevelGap.CaptureMultiplier(trainer, 30),
                    Calculate(InsectRarity.Common, 0f, trainer, 30),
                    0.0001f,
                    $"레벨 차 +{gap}");
            }
        }

        [Test]
        public void IsSuccessful_RollBoundary_SucceedsBelowAndFailsAtChance()
        {
            Assert.IsTrue(BattleCaptureChanceCalculator.IsSuccessful(0.5f, 0.4999f));
            Assert.IsFalse(BattleCaptureChanceCalculator.IsSuccessful(0.5f, 0.5f));
            Assert.IsFalse(BattleCaptureChanceCalculator.IsSuccessful(0.5f, 0.5001f));
        }

        [TestCase(false, false, "")]
        [TestCase(true, true, "곤충을 포획했습니다!")]
        [TestCase(true, false, "곤충을 잡지 못했습니다.")]
        public void BattleScreenCaptureResult_AttemptState_ReturnsExpectedMessage(
            bool attempted,
            bool succeeded,
            string expected)
        {
            Assert.AreEqual(
                expected,
                BattleScreenUI.GetCaptureResultMessage(attempted, succeeded));
        }

        private static float Calculate(
            InsectRarity rarity,
            float captureDifficulty,
            int playerLevel,
            int insectLevel,
            float activeItemBonus = 0f,
            float outfitBonus = 0f)
        {
            return BattleCaptureChanceCalculator.Calculate(
                rarity,
                captureDifficulty,
                playerLevel,
                insectLevel,
                activeItemBonus,
                outfitBonus);
        }
    }
}
#endif
