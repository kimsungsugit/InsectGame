#if UNITY_EDITOR
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 버프·너프 스택 상한(GameConstants.Battle.MaxBuffStacks).
    /// 레이드는 효과 만료가 없어 보너스에 직접 누적하므로 카운터가 유일한 방어선이다 —
    /// 상한이 없던 시절 break 3회로 보스 공격이 하한에 고정됐다.
    /// </summary>
    [TestFixture]
    public class BuffStackCapTests
    {
        private static InsectBattleStats MakeStats()
        {
            return new InsectBattleStats(null, 10);
        }

        [Test]
        public void MaxBuffStacks_IsThree()
        {
            Assert.AreEqual(3, GameConstants.Battle.MaxBuffStacks);
        }

        [Test]
        public void TryStackAttackBonus_UpToCap_AllSucceed()
        {
            InsectBattleStats stats = MakeStats();
            for (int i = 0; i < GameConstants.Battle.MaxBuffStacks; i++)
                Assert.IsTrue(stats.TryStackAttackBonus(0.2f), $"{i + 1}번째 스택이 거부됨");

            Assert.AreEqual(GameConstants.Battle.MaxBuffStacks, stats.AttackStacks);
            Assert.AreEqual(0.6f, stats.AttackBonus, 0.0001f);
        }

        [Test]
        public void TryStackAttackBonus_BeyondCap_RejectedAndValueUnchanged()
        {
            InsectBattleStats stats = MakeStats();
            for (int i = 0; i < GameConstants.Battle.MaxBuffStacks; i++)
                stats.TryStackAttackBonus(0.2f);

            float before = stats.AttackBonus;
            Assert.IsFalse(stats.TryStackAttackBonus(0.2f));
            Assert.AreEqual(before, stats.AttackBonus, 0.0001f);
            Assert.AreEqual(GameConstants.Battle.MaxBuffStacks, stats.AttackStacks);
        }

        [Test]
        public void TryStackAttackBonus_NegativeDirection_HasItsOwnCap()
        {
            InsectBattleStats stats = MakeStats();
            for (int i = 0; i < GameConstants.Battle.MaxBuffStacks; i++)
                Assert.IsTrue(stats.TryStackAttackBonus(-0.3f), $"{i + 1}번째 디버프가 거부됨");

            Assert.IsFalse(stats.TryStackAttackBonus(-0.3f));
            Assert.AreEqual(-GameConstants.Battle.MaxBuffStacks, stats.AttackStacks);
            Assert.AreEqual(-0.9f, stats.AttackBonus, 0.0001f);
        }

        [Test]
        public void TryStackAttackBonus_OppositeDirection_AlwaysAllowedAtCap()
        {
            // 상한이 되돌릴 길까지 막으면 안 된다 — 디버프가 꽉 차도 버프는 걸려야 회복이 가능하다.
            InsectBattleStats stats = MakeStats();
            for (int i = 0; i < GameConstants.Battle.MaxBuffStacks; i++)
                stats.TryStackAttackBonus(-0.3f);

            Assert.IsTrue(stats.TryStackAttackBonus(0.3f));
            Assert.AreEqual(-(GameConstants.Battle.MaxBuffStacks - 1), stats.AttackStacks);
            Assert.AreEqual(-0.6f, stats.AttackBonus, 0.0001f);
        }

        [Test]
        public void TryStackDefenseBonus_UsesSeparateCounter()
        {
            InsectBattleStats stats = MakeStats();
            for (int i = 0; i < GameConstants.Battle.MaxBuffStacks; i++)
                stats.TryStackAttackBonus(0.2f);

            // 공격 스택이 꽉 차도 방어는 별개 카운터라 그대로 쌓인다.
            Assert.IsTrue(stats.TryStackDefenseBonus(0.25f));
            Assert.AreEqual(1, stats.DefenseStacks);
            Assert.AreEqual(0.25f, stats.DefenseBonus, 0.0001f);
        }

        [Test]
        public void TryStackAttackBonus_ZeroDelta_IsRejected()
        {
            InsectBattleStats stats = MakeStats();
            Assert.IsFalse(stats.TryStackAttackBonus(0f));
            Assert.AreEqual(0, stats.AttackStacks);
        }

        [Test]
        public void RaidDebuffAtCap_LeavesBossMultiplierAboveFloor()
        {
            // 회귀 고정: 상한이 없던 시절 break(0.3) 3회로 배율이 하한 0.3에 닿아
            // 남은 라운드 내내 보스 공격이 30%로 고정됐다. 3스택이면 1-0.9=0.1... 이 아니라
            // 0.3 하한 위의 값이어야 한다는 뜻이 아니라, **4회째부터는 더 내려가지 않는다**는 뜻.
            InsectBattleStats boss = MakeStats();
            for (int i = 0; i < 10; i++)
                boss.TryStackAttackBonus(-0.3f);

            Assert.AreEqual(-GameConstants.Battle.MaxBuffStacks, boss.AttackStacks);
            Assert.AreEqual(-0.9f, boss.AttackBonus, 0.0001f);

            float multiplier = Mathf.Clamp(1f + boss.AttackBonus, 0.3f, 3f);
            Assert.AreEqual(0.3f, multiplier, 0.0001f, "3스택은 하한과 같은 값 — 그 이상 누적은 무의미");
        }

        [Test]
        public void RaidResolver_BuffAtCap_SetsCappedFlag()
        {
            InsectSkill buff = ScriptableObject.CreateInstance<InsectSkill>();
            buff.skillId = "test_boost";
            buff.displayName = "테스트 강화";
            buff.effectType = SkillEffectType.BuffAttack;
            buff.effectValue = 0.2f;
            buff.accuracy = 1f;

            InsectBattleStats attacker = MakeStats();
            InsectBattleStats boss = MakeStats();

            // team = null이면 시전자만 받는다 — 이 테스트가 재는 건 상한 판정이지 전파 범위가 아니다.
            // 팀 전파는 RaidTeamEffectTests가 따로 고정한다.
            for (int i = 0; i < GameConstants.Battle.MaxBuffStacks; i++)
            {
                RaidActionResult ok = RaidRoundResolver.ResolveLeaderSkill(0, 0, attacker, boss, null, buff, null);
                Assert.IsFalse(ok.Capped, $"{i + 1}번째는 상한이 아니어야 한다");
            }

            RaidActionResult capped = RaidRoundResolver.ResolveLeaderSkill(0, 0, attacker, boss, null, buff, null);
            Assert.IsTrue(capped.Capped);
            Assert.AreEqual(0.6f, attacker.AttackBonus, 0.0001f);

            Object.DestroyImmediate(buff);
        }
    }
}
#endif
