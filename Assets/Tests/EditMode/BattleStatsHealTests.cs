using NUnit.Framework;
using InsectGame.Battle;

namespace InsectGame.Tests
{
    // P4 신규 효과 타입 Heal의 회복 로직 검증. (null, 20): MaxHp = 10 + 20*5 = 110.
    [TestFixture]
    public class BattleStatsHealTests
    {
        [Test]
        public void Heal_RestoresHp_ClampedToMax()
        {
            var s = new InsectBattleStats(null, 20);   // MaxHp 110, CurrentHp 110
            s.ApplyDamage(60);                          // 순수 피해 60 → 50
            Assert.AreEqual(50, s.CurrentHp);
            s.Heal(30);
            Assert.AreEqual(80, s.CurrentHp);
            s.Heal(999);                                // MaxHp 상한 초과분은 잘림
            Assert.AreEqual(110, s.CurrentHp);
        }

        [Test]
        public void Heal_NonPositive_NoChange()
        {
            var s = new InsectBattleStats(null, 20);
            s.ApplyDamage(40);                          // 70
            s.Heal(0);
            s.Heal(-10);
            Assert.AreEqual(70, s.CurrentHp);
        }

        [Test]
        public void Heal_FaintedInsect_CannotRevive()
        {
            var s = new InsectBattleStats(null, 20);
            s.ApplyDamage(999);                         // CurrentHp 0 (기절)
            Assert.AreEqual(0, s.CurrentHp);
            s.Heal(50);                                 // 기절 곤충은 회복 불가
            Assert.AreEqual(0, s.CurrentHp);
        }

        // 순수 피해(방어/공격 인자 없이) — DoT가 사용하는 ApplyDamage(amount) 경로.
        [Test]
        public void ApplyDamage_PureAmount_NoMitigation()
        {
            var s = new InsectBattleStats(null, 20);
            s.ApplyDamage(12);                          // DoT 틱: 방어 무관 순수 12
            Assert.AreEqual(98, s.CurrentHp);
        }
    }
}
