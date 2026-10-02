#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 캐릭터 레벨 ↔ 곤충 레벨 차이 규칙(<see cref="TrainerLevelGap"/>)과 그걸 쓰는 EXP 지급식.
    /// 포획 공식 쪽 적용은 <c>CaptureChanceCalculatorTests</c>·<c>BattleCaptureChanceCalculatorTests</c>가 본다.
    /// </summary>
    [TestFixture]
    public class TrainerLevelGapTests
    {
        private InsectData species;

        [SetUp]
        public void SetUp()
        {
            species = ScriptableObject.CreateInstance<InsectData>();
            species.insectId = "test_beetle";
            species.rarity = InsectRarity.Common;
            species.expReward = 5;
        }

        [TearDown]
        public void TearDown()
        {
            if (species != null) Object.DestroyImmediate(species);
        }

        // ── 포획 배율 ──

        [TestCase(-20)]
        [TestCase(0)]
        [TestCase(5)]
        public void CaptureMultiplier_WithinGrace_IsOne(int gap)
        {
            Assert.AreEqual(1f, TrainerLevelGap.CaptureMultiplier(30, 30 + gap), 0.0001f);
            Assert.IsFalse(TrainerLevelGap.IsCaptureRestricted(30, 30 + gap));
        }

        [TestCase(6, 0.9f)]
        [TestCase(10, 0.5f)]
        [TestCase(14, 0.1f)]
        public void CaptureMultiplier_BeyondGrace_DropsTenPercentPerLevel(int gap, float expected)
        {
            Assert.AreEqual(expected, TrainerLevelGap.CaptureMultiplier(20, 20 + gap), 0.0001f);
            Assert.IsTrue(TrainerLevelGap.IsCaptureRestricted(20, 20 + gap));
        }

        [TestCase(15)]
        [TestCase(60)]
        public void CaptureMultiplier_FarAbove_ClampsToMinimum(int gap)
        {
            Assert.AreEqual(GameConstants.TrainerLevel.MinCaptureMultiplier,
                TrainerLevelGap.CaptureMultiplier(10, 10 + gap), 0.0001f);
        }

        [Test]
        public void Gap_NonPositiveLevels_TreatedAsLevelOne()
        {
            Assert.AreEqual(0, TrainerLevelGap.Gap(0, 1));
            Assert.AreEqual(0, TrainerLevelGap.Gap(-3, 0));
            Assert.AreEqual(9, TrainerLevelGap.Gap(0, 10));
        }

        // ── EXP 배율 ──

        [Test]
        public void ExpLevelFactor_GrowsLinearlyWithInsectLevel()
        {
            Assert.AreEqual(1f, TrainerLevelGap.ExpLevelFactor(1), 0.0001f);
            Assert.AreEqual(2f, TrainerLevelGap.ExpLevelFactor(21), 0.0001f);
            Assert.AreEqual(4f, TrainerLevelGap.ExpLevelFactor(61), 0.0001f);
        }

        [TestCase(0, 1.0f)]
        [TestCase(5, 1.5f)]
        [TestCase(10, 2.0f)]
        [TestCase(30, 2.0f)]
        [TestCase(-5, 0.5f)]
        [TestCase(-8, 0.2f)]
        [TestCase(-30, 0.2f)]
        public void ExpGapFactor_HigherBonusCapsAtDouble_LowerPenaltyFloorsAtFifth(int gap, float expected)
        {
            Assert.AreEqual(expected, TrainerLevelGap.ExpGapFactor(40, 40 + gap), 0.0001f);
        }

        [Test]
        public void ExpMultiplier_HigherInsect_NeverGivesLess()
        {
            // 요구사항 그대로 — 더 높은 곤충을 이기거나 잡으면 EXP가 더 오른다(같은 캐릭터 레벨 기준).
            float previous = 0f;
            for (int insect = 1; insect <= 80; insect++)
            {
                float current = TrainerLevelGap.ExpMultiplier(30, insect);
                Assert.GreaterOrEqual(current, previous, $"곤충 Lv{insect}");
                previous = current;
            }
        }

        // ── 실제 지급식 (InsectRewardCalculator) ──

        [Test]
        public void GetExpReward_LevelOneEqualLevel_MatchesBaseReward()
        {
            Assert.AreEqual(InsectRewardCalculator.GetExpReward(species),
                InsectRewardCalculator.GetExpReward(species, insectLevel: 1, trainerLevel: 1));
        }

        [Test]
        public void GetExpReward_HigherLevelInsect_GivesMoreExp()
        {
            int lower = InsectRewardCalculator.GetExpReward(species, insectLevel: 20, trainerLevel: 30);
            int equal = InsectRewardCalculator.GetExpReward(species, insectLevel: 30, trainerLevel: 30);
            int higher = InsectRewardCalculator.GetExpReward(species, insectLevel: 40, trainerLevel: 30);

            Assert.Less(lower, equal);
            Assert.Less(equal, higher);
            // 같은 레벨: 5 × (1 + 29×0.05) = 12.25 → 12
            Assert.AreEqual(12, equal);
        }

        [Test]
        public void GetExpReward_RarityAndLevelCompound()
        {
            species.rarity = InsectRarity.Legendary;
            species.expReward = 24;
            // 24 × 2.8 × (1 + 60×0.05) × (1 + 10×0.10) = 67.2 × 4 × 2 = 537.6
            Assert.AreEqual(538, InsectRewardCalculator.GetExpReward(species, insectLevel: 61, trainerLevel: 51));
        }

        [Test]
        public void GetExpReward_FarBelowTrainer_StillGivesAtLeastOne()
        {
            // 5 × 1 × 0.2 = 1 — 바닥 배율에서도 0으로 떨어지지 않는다.
            Assert.AreEqual(1, InsectRewardCalculator.GetExpReward(species, insectLevel: 1, trainerLevel: 80));
        }

        [Test]
        public void GetExpReward_NullData_IsZero()
        {
            Assert.AreEqual(0, InsectRewardCalculator.GetExpReward(null, 10, 10));
        }
    }
}
#endif
