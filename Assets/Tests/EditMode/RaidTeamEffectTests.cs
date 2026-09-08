#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 레이드에서 회복·버프가 <b>팀</b>에 닿는지. 예전엔 둘 다 시전자 본인만 받아서
    /// 5마리 중 1마리만 강해졌고, 힐러·탱커 역할이 성립할 수 없었다.
    /// </summary>
    [TestFixture]
    public class RaidTeamEffectTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void Heal_TargetsLowestHpAlly_NotAlwaysSelf()
        {
            InsectBattleStats[] team = Team(4);
            team[2].ApplyDamage(team[2].MaxHp - 5);   // 슬롯 2가 빈사
            InsectBattleStats boss = Stats("boss");
            InsectSkill heal = Skill("heal", SkillEffectType.Heal, effectValue: 0.5f);
            int casterHpBefore = team[0].CurrentHp;

            RaidActionResult result = RaidRoundResolver.ResolveLeaderSkill(
                0, 0, team[0], boss, team, heal, null);

            Assert.AreEqual(2, result.TargetSlot, "빈사 아군을 골라야 한다");
            Assert.Greater(result.Healing, 0);
            Assert.AreEqual(casterHpBefore, team[0].CurrentHp, "멀쩡한 시전자는 그대로여야 한다");
            Assert.Greater(team[2].CurrentHp, 5);
        }

        [Test]
        public void Heal_TargetsSelfWhenSelfIsLowest()
        {
            InsectBattleStats[] team = Team(3);
            team[0].ApplyDamage(team[0].MaxHp - 3);
            InsectBattleStats boss = Stats("boss");
            InsectSkill heal = Skill("heal", SkillEffectType.Heal, effectValue: 0.5f);

            RaidActionResult result = RaidRoundResolver.ResolveLeaderSkill(
                0, 0, team[0], boss, team, heal, null);

            Assert.AreEqual(0, result.TargetSlot);
            Assert.Greater(team[0].CurrentHp, 3);
        }

        [Test]
        public void Heal_SkipsFaintedAllies()
        {
            // 기절한 슬롯은 회복 대상이 아니다(레이드엔 부활이 없다).
            InsectBattleStats[] team = Team(3);
            team[1].ApplyDamage(999999);
            team[2].ApplyDamage(team[2].MaxHp / 2);
            InsectBattleStats boss = Stats("boss");
            InsectSkill heal = Skill("heal", SkillEffectType.Heal, effectValue: 0.5f);

            RaidActionResult result = RaidRoundResolver.ResolveLeaderSkill(
                0, 0, team[0], boss, team, heal, null);

            Assert.AreEqual(2, result.TargetSlot);
            Assert.AreEqual(0, team[1].CurrentHp, "기절한 슬롯은 살아나지 않는다");
        }

        [Test]
        public void TeamBuff_AppliesHalfToAllies()
        {
            InsectBattleStats[] team = Team(4);
            InsectBattleStats boss = Stats("boss");
            InsectSkill buff = Skill("buff", SkillEffectType.BuffAttack, effectValue: 0.4f);

            RaidActionResult result = RaidRoundResolver.ResolveLeaderSkill(
                1, 0, team[1], boss, team, buff, null);

            Assert.IsFalse(result.Capped);
            Assert.AreEqual(0.4f, team[1].AttackBonus, 0.0001f, "시전자는 전액");
            for (int i = 0; i < team.Length; i++)
            {
                if (i == 1) continue;
                Assert.AreEqual(
                    0.4f * RaidRoundResolver.TeamBuffAllyShare, team[i].AttackBonus, 0.0001f,
                    $"slot {i}는 절반");
            }
        }

        [Test]
        public void TeamBuff_SkipsFaintedAllies()
        {
            InsectBattleStats[] team = Team(3);
            team[2].ApplyDamage(999999);
            InsectBattleStats boss = Stats("boss");
            InsectSkill buff = Skill("guard", SkillEffectType.DefenseBuff, effectValue: 0.4f);

            RaidRoundResolver.ResolveLeaderSkill(0, 0, team[0], boss, team, buff, null);

            Assert.AreEqual(0.4f, team[0].DefenseBonus, 0.0001f);
            Assert.AreEqual(0.2f, team[1].DefenseBonus, 0.0001f);
            Assert.AreEqual(0f, team[2].DefenseBonus, 0.0001f, "기절한 슬롯엔 붙지 않는다");
        }

        [Test]
        public void TeamBuff_CappedOnlyWhenEveryTargetIsCapped()
        {
            InsectBattleStats[] team = Team(2);
            InsectBattleStats boss = Stats("boss");
            InsectSkill buff = Skill("buff", SkillEffectType.BuffAttack, effectValue: 0.3f);

            // 아군 하나만 먼저 상한까지 채운다 — 시전자에겐 아직 여유가 있으므로 Capped가 아니다.
            for (int i = 0; i < GameConstants.Battle.MaxBuffStacks; i++)
                Assert.IsTrue(team[1].TryStackAttackBonus(0.3f));

            RaidActionResult partial = RaidRoundResolver.ResolveLeaderSkill(
                0, 0, team[0], boss, team, buff, null);
            Assert.IsFalse(partial.Capped, "한 명이라도 붙었으면 Capped가 아니다");

            // 이제 시전자도 상한까지 채우면 전원 상한 → Capped.
            for (int i = 0; i < GameConstants.Battle.MaxBuffStacks; i++)
                team[0].TryStackAttackBonus(0.3f);

            RaidActionResult full = RaidRoundResolver.ResolveLeaderSkill(
                0, 0, team[0], boss, team, buff, null);
            Assert.IsTrue(full.Capped, "전원 상한이면 턴만 소비된다 — UI가 알려야 한다");
        }

        private InsectBattleStats[] Team(int count)
        {
            InsectBattleStats[] team = new InsectBattleStats[count];
            for (int i = 0; i < count; i++)
                team[i] = Stats($"ally_{i}");
            return team;
        }

        private InsectBattleStats Stats(string id)
        {
            InsectData data = Track(ScriptableObject.CreateInstance<InsectData>());
            data.insectId = id;
            data.displayName = id;
            data.primaryType = InsectElement.Bug;
            data.secondaryType = InsectElement.None;
            data.baseHp = 200;
            data.baseAtk = 40;
            data.baseDef = 40;
            return new InsectBattleStats(data, 10);
        }

        private InsectSkill Skill(string id, SkillEffectType effectType, float effectValue)
        {
            InsectSkill skill = Track(ScriptableObject.CreateInstance<InsectSkill>());
            skill.skillId = id;
            skill.displayName = id;
            skill.element = InsectElement.Bug;
            skill.effectType = effectType;
            skill.power = 1;
            skill.cooldownTurns = 0;
            skill.accuracy = 1f;
            skill.effectValue = effectValue;
            skill.effectDurationTurns = 2;
            return skill;
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }
    }
}
#endif
