#if UNITY_EDITOR
using InsectGame.Capture;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;

namespace InsectGame.Tests
{
    [TestFixture]
    public class CaptureChanceCalculatorTests
    {
        private static CaptureChanceTuning DefaultTuning => CaptureChanceTuning.Default;

        [TestCase(InsectRarity.Common, 0.22f, 0.512f)]
        [TestCase(InsectRarity.Rare, 0.48f, 0.248f)]
        [TestCase(InsectRarity.Epic, 0.62f, 0.112f)]
        [TestCase(InsectRarity.Legendary, 0.82f, 0.040f)]
        public void Calculate_RepresentativeMiss_UsesExpectedChance(
            InsectRarity rarity,
            float difficulty,
            float expected)
        {
            float actual = Calculate(rarity, difficulty, 1, 1, comboHits: 0);

            Assert.AreEqual(expected, actual, 0.0001f);
        }

        [TestCase(InsectRarity.Common, 0.22f, 0.812f)]
        [TestCase(InsectRarity.Rare, 0.48f, 0.548f)]
        [TestCase(InsectRarity.Epic, 0.62f, 0.412f)]
        [TestCase(InsectRarity.Legendary, 0.82f, 0.340f)]
        public void Calculate_RepresentativePerfectCombo_UsesExpectedChance(
            InsectRarity rarity,
            float difficulty,
            float expected)
        {
            float actual = Calculate(rarity, difficulty, 1, 1, comboHits: 3);

            Assert.AreEqual(expected, actual, 0.0001f);
        }

        [Test]
        public void Calculate_NormalCommonOneHit_UsesRaisedConditionalChance()
        {
            float chance = Calculate(
                InsectRarity.Common, captureDifficulty: 0.22f,
                playerLevel: 1, insectLevel: 3, comboHits: 1);

            Assert.GreaterOrEqual(chance, 0.50f);
            Assert.AreEqual(0.502f, chance, 0.0001f);
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
                float chance = Calculate(
                    rarity, captureDifficulty: 0.5f,
                    playerLevel: 10, insectLevel: 10, comboHits: 3);
                Assert.LessOrEqual(chance, previous);
                previous = chance;
            }
        }

        [Test]
        public void Calculate_DifficultyIncreases_ChanceNeverIncreases()
        {
            float easy = Calculate(InsectRarity.Common, 0f, 1, 1, comboHits: 0);
            float medium = Calculate(InsectRarity.Common, 0.5f, 1, 1, comboHits: 0);
            float hard = Calculate(InsectRarity.Common, 1f, 1, 1, comboHits: 0);

            Assert.GreaterOrEqual(easy, medium);
            Assert.GreaterOrEqual(medium, hard);
        }

        [Test]
        public void Calculate_PlayerLevelAdvantage_ChanceNeverDecreases()
        {
            float underLevel = Calculate(InsectRarity.Common, 0.22f, 1, 5, comboHits: 0);
            float equalLevel = Calculate(InsectRarity.Common, 0.22f, 5, 5, comboHits: 0);
            float overLevel = Calculate(InsectRarity.Common, 0.22f, 9, 5, comboHits: 0);

            Assert.GreaterOrEqual(equalLevel, underLevel);
            Assert.GreaterOrEqual(overLevel, equalLevel);
        }

        [Test]
        public void Calculate_CoreBelowFloor_PositiveBonusesAreNotSwallowed()
        {
            // 레벨 차는 유예(+5) 안에 둔다 — 이 테스트는 floor 위에 보너스가 얹히는지를 보고,
            // 그 너머의 레벨 제한 배율은 Calculate_InsectFarAboveTrainer_*가 본다.
            // 코어: .60 - .32 - .40 - .15 = -.27 → floor .04
            float noBonus = Calculate(
                InsectRarity.Legendary, 1f, 1, 6, comboHits: 0);
            float itemBonus = Calculate(
                InsectRarity.Legendary, 1f, 1, 6, comboHits: 0,
                activeItemBonus: 0.10f);
            float oneHit = Calculate(
                InsectRarity.Legendary, 1f, 1, 6, comboHits: 1);

            Assert.AreEqual(0.04f, noBonus, 0.0001f);
            Assert.AreEqual(0.14f, itemBonus, 0.0001f);
            Assert.AreEqual(0.09f, oneHit, 0.0001f);
        }

        [Test]
        public void Calculate_EpicAndLegendaryPerfectGoldNet_RemainBelowGuaranteed()
        {
            float epic = Calculate(
                InsectRarity.Epic, 0.62f, 1, 1, comboHits: 3,
                captureItemBonus: 0.20f);
            float legendary = Calculate(
                InsectRarity.Legendary, 0.82f, 1, 1, comboHits: 3,
                captureItemBonus: 0.20f);

            Assert.AreEqual(0.612f, epic, 0.0001f);
            Assert.AreEqual(0.540f, legendary, 0.0001f);
            Assert.Less(epic, 1f);
            Assert.Less(legendary, 1f);
        }

        [Test]
        public void Calculate_ExcessiveBonuses_ClampsAtOne()
        {
            float chance = Calculate(
                InsectRarity.Common, 0f, 100, 1, comboHits: 3,
                activeItemBonus: 1f, outfitBonus: 1f, captureItemBonus: 1f);

            Assert.AreEqual(1f, chance);
        }

        [Test]
        public void GetLevelModifier_ClampsAtMaximumLevelDelta_BothDirections()
        {
            float step = CaptureChanceTuning.DefaultPlayerLevelBonusStep;
            float penalty = CaptureChanceTuning.DefaultEnemyLevelPenaltyStep;
            int cap = CaptureChanceCalculator.MaximumLevelDelta;

            // 상한 안쪽은 그대로 비례한다.
            Assert.AreEqual(
                cap * step,
                CaptureChanceCalculator.GetLevelModifier(1 + cap, 1, step, penalty),
                0.0001f);

            // 상한을 넘겨도 더 오르지 않는다 — 여기가 없어서 고레벨이 전설을 무조건 잡았다.
            Assert.AreEqual(
                cap * step,
                CaptureChanceCalculator.GetLevelModifier(99, 1, step, penalty),
                0.0001f);

            Assert.AreEqual(
                -cap * penalty,
                CaptureChanceCalculator.GetLevelModifier(1, 99, step, penalty),
                0.0001f);
        }

        [Test]
        public void GetLevelModifier_MatchesBattlePathCap()
        {
            // 같은 규칙의 두 구현 — 상한이 갈라지면 같은 곤충이 경로에 따라 다르게 잡힌다.
            Assert.AreEqual(
                InsectGame.Battle.BattleCaptureChanceCalculator.MaximumLevelDelta,
                CaptureChanceCalculator.MaximumLevelDelta);
        }

        [Test]
        public void Calculate_HugeLevelAdvantage_DoesNotGuaranteeLegendaryCapture()
        {
            // 메인 필드는 리전과 무관하게 항상 Lv.1부터 스폰하므로 이 조합이 실제로 나온다.
            // 상한 이전에는 +0.76이 붙어 등급·난이도 페널티가 통째로 지워지고 1.0에 닿았다.
            float chance = Calculate(
                InsectRarity.Legendary, captureDifficulty: 0.82f,
                playerLevel: 40, insectLevel: 2, comboHits: 0);

            Assert.Less(chance, 1f, "레벨 우위만으로 전설 포획이 보장되면 안 된다");
            // base 0.60 - 등급 0.32 - 난이도 0.328 + 상한 0.10 = 0.052
            Assert.AreEqual(0.052f, chance, 0.0001f);
        }

        [Test]
        public void Calculate_InsectFarAboveTrainer_PerfectMinigameCannotCatch()
        {
            // 예전엔 덧셈 보정이 ±5에서 잘리고 최저 보장(.30) 위에 퍼펙트 보너스(+.30)가 얹혀
            // 캐릭터보다 15레벨 높은 일반 곤충이 66%로 잡혔다.
            float chance = Calculate(
                InsectRarity.Common, captureDifficulty: 0.22f,
                playerLevel: 10, insectLevel: 25, comboHits: 3,
                activeItemBonus: 0.10f, outfitBonus: 0.05f);

            Assert.LessOrEqual(chance, 0.05f, "레벨 차 +15는 보너스를 다 얹어도 사실상 포획 불가여야 한다");
            Assert.Greater(chance, 0f, "불가능이 아니라 극히 낮은 확률이다");
        }

        [Test]
        public void Calculate_PastGrace_ChanceStrictlyDecreasesUntilMinimum()
        {
            int grace = GameConstants.TrainerLevel.CaptureGraceLevels;
            float previous = Calculate(InsectRarity.Legendary, 0.82f, 20, 20 + grace, comboHits: 3);
            for (int gap = grace + 1; gap <= grace + 9; gap++)
            {
                float current = Calculate(InsectRarity.Legendary, 0.82f, 20, 20 + gap, comboHits: 3);
                Assert.Less(current, previous, $"레벨 차 +{gap}에서 확률이 줄지 않았다");
                previous = current;
            }
        }

        [Test]
        public void Calculate_WithinGrace_UnaffectedByLevelGapMultiplier()
        {
            // 유예 안(+5까지)은 옛 공식 그대로 — 정상 진행의 포획률을 바꾸지 않는다.
            int grace = GameConstants.TrainerLevel.CaptureGraceLevels;
            float chance = Calculate(InsectRarity.Rare, 0.50f, 20, 20 + grace, comboHits: 2);
            // base .60 - 등급 .16 - 난이도 .20 - 레벨 .15 = .09 → 하한 .14 + 타이밍 .15 + 콤보 .10 = .39
            Assert.AreEqual(0.39f, chance, 0.0001f);
        }

        [Test]
        public void CaptureGraceLevels_MatchesAdditiveLevelCap()
        {
            // 둘이 어긋나면 +5와 유예 사이에 확률이 평평한 구간이 생긴다(예전엔 +5 너머 전부가 그랬다).
            Assert.AreEqual(CaptureChanceCalculator.MaximumLevelDelta,
                GameConstants.TrainerLevel.CaptureGraceLevels);
        }

        /// <summary>
        /// 프로파일 에셋을 새로 만들었을 때 공식 기본값과 어긋나지 않는가.
        ///
        /// <b>이 테스트는 "일치"만 본다 — 두 값이 같다고 옳은 값인 건 아니다.</b>
        /// 실제로 2026-08-03 리팩터가 두 곳을 함께 원복하며 이 테스트를 심었고,
        /// 그 뒤로 이 테스트가 "틀린 한 쌍"을 서로 고정시켜 원복을 가렸다(2026-08-17 audit이 발견).
        /// 값 자체가 맞는지는 <c>rules/balance.md</c>의 floor 상호작용 절을 볼 것.
        /// </summary>
        [Test]
        public void GameplayTuningProfile_NewAssetDefaults_MatchCaptureFormulaDefaults()
        {
            GameplayTuningProfile profile =
                UnityEngine.ScriptableObject.CreateInstance<GameplayTuningProfile>();
            try
            {
                Assert.AreEqual(
                    CaptureChanceTuning.DefaultBaseSuccessChance,
                    profile.baseSuccessChance,
                    0.0001f);
                Assert.AreEqual(
                    CaptureChanceTuning.DefaultRarityPenaltyStep,
                    profile.rarityPenaltyStep,
                    0.0001f);
                Assert.AreEqual(
                    CaptureChanceTuning.DefaultDifficultyPenaltyScale,
                    profile.difficultyPenaltyScale,
                    0.0001f);
                Assert.AreEqual(
                    CaptureChanceTuning.DefaultPerfectTimingBonus,
                    profile.perfectTimingBonus,
                    0.0001f);
                Assert.AreEqual(
                    CaptureChanceTuning.DefaultTimingWindow,
                    profile.timingWindow,
                    0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [TestCase(-1, 0.1f, 0f)]
        [TestCase(0, 0.1f, 0f)]
        [TestCase(1, 0.3f, 0.05f)]
        [TestCase(2, 0.45f, 0.10f)]
        [TestCase(3, 0.5f, 0.15f)]
        [TestCase(4, 0.5f, 0.15f)]
        public void MinigameOutcome_ComboBoundary_ClampsAndMapsExpectedValues(
            int comboHits,
            float expectedTiming,
            float expectedBonus)
        {
            Assert.AreEqual(
                expectedTiming,
                CaptureMinigameProbability.GetTiming01(comboHits),
                0.0001f);
            Assert.AreEqual(
                expectedBonus,
                CaptureMinigameProbability.GetComboBonus(comboHits),
                0.0001f);
        }

        [Test]
        public void MinigameOutcome_MoreHits_BonusIsMonotonic()
        {
            float previous = -1f;
            for (int hits = 0; hits <= CaptureMinigameProbability.MaxComboHits; hits++)
            {
                float bonus = CaptureMinigameProbability.GetComboBonus(hits);
                Assert.GreaterOrEqual(bonus, previous);
                previous = bonus;
            }
        }

        private static float Calculate(
            InsectRarity rarity,
            float captureDifficulty,
            int playerLevel,
            int insectLevel,
            int comboHits,
            float activeItemBonus = 0f,
            float outfitBonus = 0f,
            float captureItemBonus = 0f)
        {
            return CaptureChanceCalculator.Calculate(
                rarity,
                captureDifficulty,
                playerLevel,
                insectLevel,
                CaptureMinigameProbability.GetTiming01(comboHits),
                activeItemBonus,
                outfitBonus,
                CaptureMinigameProbability.GetExtraBonus(comboHits, captureItemBonus),
                DefaultTuning);
        }
    }
}
#endif
