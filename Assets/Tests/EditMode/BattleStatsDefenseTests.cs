using NUnit.Framework;
using InsectGame.Battle;

namespace InsectGame.Tests
{
    // 방어 보너스(의상/아이템)가 InsectBattleStats.ApplyDamage 피해 공식에 반영되는지 검증.
    // 공식: effDef = defenderDef * (1 + DefenseBonus); ratio = atk / effDef; dmg = amount * clamp(ratio, 0.5, 2.5).
    [TestFixture]
    public class BattleStatsDefenseTests
    {
        // (null, 20): MaxHp = 10 + 20*5 = 110.
        [Test]
        public void ApplyDamage_NoDefenseBonus_BaselineUnchanged()
        {
            var s = new InsectBattleStats(null, 20);
            Assert.AreEqual(0f, s.DefenseBonus);     // 기본 0 (회귀 방어)
            s.ApplyDamage(20, 25, 25);               // ratio 25/25=1.0 → 20 피해
            Assert.AreEqual(90, s.CurrentHp);
        }

        [Test]
        public void ApplyDamage_DefenseBonus_ReducesDamage()
        {
            var s = new InsectBattleStats(null, 20);
            s.DefenseBonus = 1.0f;                    // 유효 방어 ×2
            s.ApplyDamage(20, 25, 25);               // effDef 50, ratio 0.5 → 10 피해
            Assert.AreEqual(100, s.CurrentHp);
        }

        [Test]
        public void ApplyDamage_HigherDefenseBonus_LowerDamage()
        {
            var low = new InsectBattleStats(null, 20);
            var high = new InsectBattleStats(null, 20);
            low.DefenseBonus = 0.2f;
            high.DefenseBonus = 0.8f;
            low.ApplyDamage(30, 40, 20);
            high.ApplyDamage(30, 40, 20);
            int dmgLow = low.MaxHp - low.CurrentHp;
            int dmgHigh = high.MaxHp - high.CurrentHp;
            Assert.Less(dmgHigh, dmgLow, "방어 보너스가 높을수록 피해가 적어야 한다");
        }
    }
}
