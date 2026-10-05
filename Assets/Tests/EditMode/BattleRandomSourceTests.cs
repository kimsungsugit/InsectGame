#if UNITY_EDITOR
using InsectGame.Battle;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    [TestFixture]
    public class BattleRandomSourceTests
    {
        [Test]
        public void CombatStream_VisualRandomConsumptionDoesNotChangeRolls()
        {
            var original = Random.state;
            try
            {
                var baseline = new BattleRandomSource(8173);
                var noisy = new BattleRandomSource(8173);
                for (int i = 0; i < 256; i++)
                {
                    for (int draw = 0; draw < i % 17; draw++) _ = Random.value;
                    Assert.AreEqual(baseline.Next01(), noisy.Next01());
                    Assert.AreEqual(baseline.NextInt(0, 5), noisy.NextInt(0, 5));
                }
            }
            finally { Random.state = original; }
        }

        [Test]
        public void CombatStream_DoesNotAdvanceVisualRandomState()
        {
            var original = Random.state;
            try
            {
                Random.InitState(25);
                float expected = Random.value;
                Random.InitState(25);
                var source = new BattleRandomSource(8173);
                for (int i = 0; i < 100; i++) { source.Next01(); source.NextInt(0, 5); }
                Assert.AreEqual(expected, Random.value);
            }
            finally { Random.state = original; }
        }

        [Test]
        public void RaidHitAndBossTarget_WithVisualNoise_KeepSameSequence()
        {
            var original = Random.state;
            InsectSkill skill = ScriptableObject.CreateInstance<InsectSkill>();
            try
            {
                skill.effectType = SkillEffectType.Damage;
                skill.element = InsectElement.None;
                skill.power = 10;
                skill.accuracy = 0.55f;
                var baseline = new BattleRandomSource(8173);
                var noisy = new BattleRandomSource(8173);
                var team = new InsectBattleStats[5];
                for (int i = 0; i < team.Length; i++) team[i] = new InsectBattleStats(null, 20);
                var bossA = new InsectBattleStats(null, 20);
                var bossB = new InsectBattleStats(null, 20);
                int hits = 0, misses = 0;
                for (int round = 1; round <= 50; round++)
                {
                    bossA.ResetHp(); bossB.ResetHp();
                    var expected = RaidRoundResolver.ResolveLeaderSkill(0, 0, team[0], bossA, team, skill, baseline);
                    var expectedIntent = RaidRoundResolver.CreateBossIntent(round, bossA, team, 3, null, baseline, false);
                    for (int draw = 0; draw < round * 3; draw++) _ = Random.value;
                    var actual = RaidRoundResolver.ResolveLeaderSkill(0, 0, team[0], bossB, team, skill, noisy);
                    var actualIntent = RaidRoundResolver.CreateBossIntent(round, bossB, team, 3, null, noisy, false);
                    Assert.AreEqual(expected.Damage, actual.Damage);
                    Assert.AreEqual(expectedIntent.TargetSlot, actualIntent.TargetSlot);
                    if (actual.Damage > 0) hits++; else misses++;
                }
                Assert.Greater(hits, 0);
                Assert.Greater(misses, 0);
            }
            finally { Object.DestroyImmediate(skill); Random.state = original; }
        }
    }
}
#endif
