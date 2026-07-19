using NUnit.Framework;
using InsectGame.Battle;

namespace InsectGame.Tests
{
    // 명중 판정(회피) 순수 로직 검증. RollHit(accuracy, evasion, roll): roll < clamp(acc-eva, 0.3, 1) 이면 명중.
    [TestFixture]
    public class HitRollTests
    {
        [Test]
        public void RollHit_FullAccuracy_AlwaysHits()
        {
            Assert.IsTrue(InsectBattleController.RollHit(1f, 0f, 0.0f));
            Assert.IsTrue(InsectBattleController.RollHit(1f, 0f, 0.99f));   // roll<1.0
        }

        [Test]
        public void RollHit_LowAccuracy_MissesAboveThreshold()
        {
            // acc 0.9 → hitChance 0.9. roll 0.85 명중, roll 0.95 빗나감.
            Assert.IsTrue(InsectBattleController.RollHit(0.9f, 0f, 0.85f));
            Assert.IsFalse(InsectBattleController.RollHit(0.9f, 0f, 0.95f));
        }

        [Test]
        public void RollHit_Evasion_LowersHitChance()
        {
            // acc 0.9, eva 0.2 → hitChance 0.7. roll 0.65 명중, 0.75 빗나감.
            Assert.IsTrue(InsectBattleController.RollHit(0.9f, 0.2f, 0.65f));
            Assert.IsFalse(InsectBattleController.RollHit(0.9f, 0.2f, 0.75f));
        }

        [Test]
        public void RollHit_FloorGuarantees30Percent()
        {
            // acc 0.1, eva 0.5 → clamp(-0.4, 0.3, 1) = 0.3. roll 0.25 명중(완전 회피 방지).
            Assert.IsTrue(InsectBattleController.RollHit(0.1f, 0.5f, 0.25f));
            Assert.IsFalse(InsectBattleController.RollHit(0.1f, 0.5f, 0.35f));
        }
    }
}
