#if UNITY_EDITOR
using NUnit.Framework;
using InsectGame.Battle;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Tests
{
    // 방어 보너스(의상/아이템)가 InsectBattleStats.ApplyDamage 피해 공식에 반영되는지 검증.
    // 공식: effDef = defenderDef * (1 + DefenseBonus); ratio = atk / effDef;
    //       dmg = amount * clamp(ratio, GameConstants.Battle.MinAtkDefRatio, MaxAtkDefRatio).
    //
    // **기대값을 숫자로 박지 않는다.** 예전엔 하한 0.5를 전제로 "HP 100"을 박아 뒀는데,
    // 전투 길이 조정으로 하한이 0.7이 되자 이 테스트만 깨졌다 — 검증하려던 성질
    // (방어 보너스가 피해를 줄인다)은 그대로였는데도. 상수에서 파생시켜 그 재발을 막는다.
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
            s.DefenseBonus = 1.0f;                    // 유효 방어 ×2 → ratio 25/50 = 0.5
            int before = s.CurrentHp;
            s.ApplyDamage(20, 25, 25);

            // ratio 0.5는 하한 아래라 하한으로 잘린다 — 기대값을 그 상수에서 계산한다.
            int expected = Mathf.RoundToInt(20 * Mathf.Clamp(0.5f,
                GameConstants.Battle.MinAtkDefRatio, GameConstants.Battle.MaxAtkDefRatio));
            Assert.AreEqual(before - expected, s.CurrentHp);
            Assert.Less(before - s.CurrentHp, 20, "방어 보너스를 걸었는데 피해가 안 줄었다");
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
#endif
