#if UNITY_EDITOR
using InsectGame.Battle;
using NUnit.Framework;

namespace InsectGame.Tests
{
    [TestFixture]
    public class BattleMotionTests
    {
        [TestCase("rhinoceros_beetle", true, false, BattleMotion.Kind.Charge)]
        [TestCase("stag_beetle", true, false, BattleMotion.Kind.Charge)]
        [TestCase("bee_queen", true, false, BattleMotion.Kind.Sting)]        // 4단계 — 벌은 찌르기 돌진(나는 종의 내리꽂기와 가른다)
        [TestCase("spider_garden", true, false, BattleMotion.Kind.Pounce)]
        [TestCase("ant_soldier", true, false, BattleMotion.Kind.Lunge)]
        [TestCase("RHINOCEROS_BEETLE", true, false, BattleMotion.Kind.Charge)]
        [TestCase(null, true, false, BattleMotion.Kind.Charge)]
        [TestCase("mantis", true, false, BattleMotion.Kind.Slash)]
        [TestCase("dragonfly", true, false, BattleMotion.Kind.Flight)]
        [TestCase("monarch_butterfly", true, false, BattleMotion.Kind.Flight)]
        [TestCase("ant", false, false, BattleMotion.Kind.Projectile)]
        [TestCase("mantis", false, false, BattleMotion.Kind.Projectile)]
        [TestCase("dragonfly", false, false, BattleMotion.Kind.Projectile)]
        [TestCase("mantis", true, true, BattleMotion.Kind.Support)]
        [TestCase("dragonfly", false, true, BattleMotion.Kind.Support)]
        public void Resolve_SpeciesAndAction_SelectsExpectedProfile(string species, bool melee, bool support, BattleMotion.Kind expected)
        {
            Assert.AreEqual(expected, BattleMotion.Resolve(species, melee, support));
        }

        [TestCase(BattleMotion.Kind.Charge)]
        [TestCase(BattleMotion.Kind.Slash)]
        [TestCase(BattleMotion.Kind.Flight)]
        [TestCase(BattleMotion.Kind.Projectile)]
        [TestCase(BattleMotion.Kind.Support)]
        [TestCase(BattleMotion.Kind.Sting)]
        [TestCase(BattleMotion.Kind.Pounce)]
        [TestCase(BattleMotion.Kind.Lunge)]
        public void Evaluate_NormalizedSequence_IsFiniteAndReturnsHome(BattleMotion.Kind kind)
        {
            for (int sample = -10; sample <= 110; sample++)
            {
                var pose = BattleMotion.Evaluate(kind, sample / 100f);
                AssertFinite(pose.Travel);
                AssertFinite(pose.Lift);
                AssertFinite(pose.Pitch);
                AssertFinite(pose.Roll);
            }
            AssertAtRest(kind, 0f);
            AssertAtRest(kind, 1f);
            AssertAtRest(kind, 1.1f);
        }

        [Test]
        public void MeleeProfiles_ImpactMatchesSharedTimingAndDistinctSilhouettes()
        {
            Assert.AreEqual(0.4f, BattleMotion.ImpactProgress, 0.00001f);
            var charge = BattleMotion.Evaluate(BattleMotion.Kind.Charge, BattleMotion.ImpactProgress);
            var slash = BattleMotion.Evaluate(BattleMotion.Kind.Slash, BattleMotion.ImpactProgress);
            Assert.Greater(charge.Travel, 0f);
            Assert.Greater(slash.Travel, 0f);
            Assert.Less(slash.Travel, charge.Travel, "Slash stays within a shorter contact range than charge.");
            float chargeLift = 0f;
            float flightLift = 0f;
            for (int i = 0; i <= 100; i++)
            {
                chargeLift = System.Math.Max(chargeLift, BattleMotion.Evaluate(BattleMotion.Kind.Charge, i / 100f).Lift);
                flightLift = System.Math.Max(flightLift, BattleMotion.Evaluate(BattleMotion.Kind.Flight, i / 100f).Lift);
            }
            Assert.Greater(flightLift, chargeLift, "Flight has a visibly higher arc than ground charge.");
        }

        [TestCase(BattleMotion.Kind.Charge)]
        [TestCase(BattleMotion.Kind.Slash)]
        [TestCase(BattleMotion.Kind.Flight)]
        [TestCase(BattleMotion.Kind.Sting)]
        [TestCase(BattleMotion.Kind.Pounce)]
        [TestCase(BattleMotion.Kind.Lunge)]
        public void MeleeMotion_PreservesAnticipationAndRecovery(BattleMotion.Kind kind)
        {
            Assert.Less(BattleMotion.Evaluate(kind, 0.12f).Travel, 0f,
                "Anticipation moves away before contact; the arena must use LerpUnclamped.");
            Assert.Greater(BattleMotion.Evaluate(kind, 0.55f).Travel, 0f);
            AssertAtRest(kind, 0.84f);
        }

        [Test]
        public void SupportProfile_NeverRushesOrTilts()
        {
            for (int i = 0; i <= 100; i++) AssertAtRest(BattleMotion.Kind.Support, i / 100f);
        }

        private static void AssertAtRest(BattleMotion.Kind kind, float progress)
        {
            var pose = BattleMotion.Evaluate(kind, progress);
            Assert.AreEqual(0f, pose.Travel, 0.0001f);
            Assert.AreEqual(0f, pose.Lift, 0.0001f);
            Assert.AreEqual(0f, pose.Pitch, 0.0001f);
            Assert.AreEqual(0f, pose.Roll, 0.0001f);
        }

        private static void AssertFinite(float value)
        {
            Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value));
        }
    }
}
#endif
