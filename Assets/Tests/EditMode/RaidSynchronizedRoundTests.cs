#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InsectGame.Battle;
using InsectGame.Data;
using InsectGame.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    [TestFixture]
    public class RaidSynchronizedRoundTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    Object.DestroyImmediate(createdObjects[i]);
            }
            createdObjects.Clear();
        }

        [Test]
        public void ResolveTeamCommand_AllFiveAlive_ProducesLeaderAndFourSupports()
        {
            InsectSkill skill = CreateSkill("leader_hit", SkillEffectType.Damage, 8, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 5000, 30, 80),
                CreateTeam(5, 300, 50, 60),
                new[] { skill });

            RaidRoundResult result = raid.ResolveTeamCommand(0);

            Assert.IsNotNull(result);
            Assert.AreEqual(5, result.TeamActions.Count);
            Assert.AreEqual(RaidActionKind.LeaderSkill, result.TeamActions[0].Kind);
            CollectionAssert.AreEquivalent(
                new[] { 0, 1, 2, 3, 4 },
                result.TeamActions.Select(action => action.SourceSlot).ToArray());
            Assert.AreEqual(
                4,
                result.TeamActions.Count(action => action.Kind == RaidActionKind.SupportAssist));
            Assert.IsTrue(raid.IsAwaitingBossResponse);
        }

        [Test]
        public void ResolveTeamCommand_DeadMembers_AreExcludedFromRush()
        {
            InsectSkill skill = CreateSkill("leader_hit", SkillEffectType.Damage, 8, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 5000, 30, 80),
                CreateTeam(5, 300, 50, 60),
                new[] { skill });
            raid.TeamStats[2].ApplyDamage(999999);
            raid.TeamStats[4].ApplyDamage(999999);

            RaidRoundResult result = raid.ResolveTeamCommand(0);

            CollectionAssert.AreEquivalent(
                new[] { 0, 1, 3 },
                result.TeamActions.Select(action => action.SourceSlot).ToArray());
            Assert.AreEqual(3, result.TeamActions.Count);
        }

        [Test]
        public void ResolveBossResponse_CalledTwice_DealsDamageOnlyOnce()
        {
            InsectSkill skill = CreateSkill("leader_hit", SkillEffectType.Damage, 4, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 5000, 80, 80),
                CreateTeam(5, 500, 40, 70),
                new[] { skill });
            raid.ResolveTeamCommand(0);

            int hpBefore = raid.TeamStats.Sum(stats => stats.CurrentHp);
            RaidRoundResult first = raid.ResolveBossResponse();
            int hpAfterFirst = raid.TeamStats.Sum(stats => stats.CurrentHp);
            RaidRoundResult second = raid.ResolveBossResponse();

            Assert.IsNotNull(first);
            Assert.IsNull(second);
            Assert.Less(hpAfterFirst, hpBefore);
            Assert.AreEqual(
                hpAfterFirst,
                raid.TeamStats.Sum(stats => stats.CurrentHp));
            Assert.IsTrue(first.BossResponseResolved);
        }

        [Test]
        public void CompleteRoundPresentation_TicksCooldownExactlyOnce()
        {
            InsectSkill skill = CreateSkill("cooldown_hit", SkillEffectType.Damage, 4, 3);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 5000, 40, 100),
                CreateTeam(5, 500, 40, 80),
                new[] { skill });

            raid.ResolveTeamCommand(0);
            Assert.AreEqual(3, raid.TeamCooldowns[0][0]);
            raid.ResolveBossResponse();
            Assert.AreEqual(3, raid.TeamCooldowns[0][0]);

            Assert.IsTrue(raid.CompleteRoundPresentation());
            Assert.AreEqual(2, raid.TeamCooldowns[0][0]);
            Assert.IsFalse(raid.CompleteRoundPresentation());
            Assert.AreEqual(2, raid.TeamCooldowns[0][0]);
            Assert.AreEqual(1, raid.TurnNumber);
        }

        [Test]
        public void ResolveTeamCommand_KnocksOutBoss_NoBossResponseAndEndWaitsForPresentation()
        {
            InsectSkill skill = CreateSkill("finisher", SkillEffectType.Damage, 9999, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 10, 10, 5),
                CreateTeam(5, 500, 100, 80),
                new[] { skill });
            int raidEndedCount = 0;
            int roundCompletedCount = 0;
            raid.RaidEnded += _ => raidEndedCount++;
            raid.RaidRoundCompleted += _ => roundCompletedCount++;

            RaidRoundResult result = raid.ResolveTeamCommand(0);

            Assert.AreEqual(RaidRoundEndState.Victory, result.EndState);
            Assert.IsNull(raid.ResolveBossResponse());
            Assert.AreEqual(0, raidEndedCount);
            Assert.IsTrue(raid.IsAwaitingPresentationCompletion);

            Assert.IsTrue(raid.CompleteRoundPresentation());
            Assert.IsFalse(raid.CompleteRoundPresentation());
            Assert.AreEqual(1, roundCompletedCount);
            Assert.AreEqual(1, raidEndedCount);
            Assert.IsFalse(raid.IsActive);
        }

        [Test]
        public void ResolveBossResponse_AreaIntent_ReportsActualDamagePerSlot()
        {
            InsectSkill skill = CreateSkill("leader_hit", SkillEffectType.Damage, 1, 0);
            InsectData[] team =
            {
                CreateData("team_0", 1000, 20, 10),
                CreateData("team_1", 1000, 20, 30),
                CreateData("team_2", 1000, 20, 60),
                CreateData("team_3", 1000, 20, 120),
                CreateData("team_4", 1000, 20, 200)
            };
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 5000, 100, 100),
                team,
                new[] { skill });
            Assert.AreEqual(RaidBossIntentKind.AreaAttack, raid.NextBossIntent.Kind);
            raid.ResolveTeamCommand(0);
            int[] hpBefore = raid.TeamStats.Select(stats => stats.CurrentHp).ToArray();

            RaidRoundResult result = raid.ResolveBossResponse();

            int expectedTotal = 0;
            for (int i = 0; i < raid.TeamStats.Length; i++)
            {
                int actual = hpBefore[i] - raid.TeamStats[i].CurrentHp;
                Assert.AreEqual(actual, result.BossDamageBySlot[i], $"slot {i}");
                expectedTotal += actual;
            }
            Assert.AreEqual(expectedTotal, result.TotalDamageToTeam);
            Assert.AreEqual(expectedTotal, result.BossAction.Damage);
            Assert.Greater(result.BossDamageBySlot[0], result.BossDamageBySlot[4]);
        }

        [Test]
        public void ResolveUniteCommand_TotalMatchesContributionsAndSlots()
        {
            InsectSkill skill = CreateSkill("leader_hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 10000, 30, 100),
                CreateTeam(5, 500, 60, 80),
                new[] { skill });
            SetUniteGauge(raid, RaidBattleController.UniteGaugeMax);

            RaidRoundResult result = raid.ResolveUniteCommand();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsUnite);
            Assert.AreEqual(5, result.TeamActions.Count);
            Assert.AreEqual(
                result.TeamActions.Sum(action => action.Damage),
                result.TotalDamageToBoss);
            Assert.AreEqual(
                result.UniteSlotDamages.Sum(),
                result.TotalDamageToBoss);
            CollectionAssert.AreEqual(
                result.UniteSlotDamages,
                raid.UniteSlotDamages);
            Assert.AreEqual(0f, raid.UniteGauge);
            Assert.IsTrue(raid.IsAwaitingBossResponse);
        }

        [Test]
        public void Stun_SkipsExactlyOneTelegraphedBossResponse()
        {
            InsectSkill stun = CreateSkill("stun", SkillEffectType.Stun, 1, 0);
            InsectSkill hit = CreateSkill("hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 10000, 80, 100),
                CreateTeam(5, 1000, 40, 80),
                new[] { stun, hit });
            int hpBefore = raid.TeamStats.Sum(stats => stats.CurrentHp);

            raid.ResolveTeamCommand(0);
            RaidRoundResult stunnedRound = raid.ResolveBossResponse();

            Assert.IsTrue(stunnedRound.BossResponseSkipped);
            Assert.AreEqual(hpBefore, raid.TeamStats.Sum(stats => stats.CurrentHp));
            Assert.IsTrue(raid.CompleteRoundPresentation());
            Assert.AreEqual(RaidBossIntentKind.AreaAttack, raid.NextBossIntent.Kind);

            raid.ResolveTeamCommand(1);
            RaidRoundResult followingRound = raid.ResolveBossResponse();

            Assert.IsFalse(followingRound.BossResponseSkipped);
            Assert.Less(raid.TeamStats.Sum(stats => stats.CurrentHp), hpBefore);
        }

        private RaidBattleController CreateRaid(InsectData bossData,
            InsectData[] teamData, InsectSkill[] equippedSkills)
        {
            GameObject bossObject = Track(new GameObject("RaidTestBoss"));
            InsectEntity bossEntity = bossObject.AddComponent<InsectEntity>();
            SetField(bossEntity, "data", bossData);
            SetField(bossEntity, "level", 10);

            GameObject controllerObject = Track(new GameObject("RaidTestController"));
            RaidBattleController controller =
                controllerObject.AddComponent<RaidBattleController>();
            controller.SetRandomSource(new FixedRaidRandomSource());

            int[] levels = Enumerable.Repeat(10, teamData.Length).ToArray();
            InsectSkill[][] skills = new InsectSkill[teamData.Length][];
            for (int i = 0; i < skills.Length; i++)
                skills[i] = equippedSkills;

            controller.StartRaid(
                bossEntity,
                teamData,
                levels,
                null,
                skills);
            return controller;
        }

        private InsectData[] CreateTeam(int count, int hp, int attack, int defense)
        {
            InsectData[] result = new InsectData[count];
            for (int i = 0; i < count; i++)
                result[i] = CreateData($"team_{i}", hp, attack, defense);
            return result;
        }

        private InsectData CreateData(string id, int hp, int attack, int defense)
        {
            InsectData data = Track(ScriptableObject.CreateInstance<InsectData>());
            data.insectId = id;
            data.displayName = id;
            data.primaryType = InsectElement.Bug;
            data.secondaryType = InsectElement.None;
            data.baseHp = hp;
            data.baseAtk = attack;
            data.baseDef = defense;
            data.candyReward = 1;
            data.expReward = 1;
            return data;
        }

        private InsectSkill CreateSkill(string id, SkillEffectType effectType,
            int power, int cooldown)
        {
            InsectSkill skill = Track(ScriptableObject.CreateInstance<InsectSkill>());
            skill.skillId = id;
            skill.displayName = id;
            skill.element = InsectElement.Bug;
            skill.effectType = effectType;
            skill.power = power;
            skill.cooldownTurns = cooldown;
            skill.accuracy = 1f;
            return skill;
        }

        private T Track<T>(T obj) where T : Object
        {
            createdObjects.Add(obj);
            return obj;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void SetUniteGauge(RaidBattleController controller, float value)
        {
            PropertyInfo property = typeof(RaidBattleController).GetProperty(
                nameof(RaidBattleController.UniteGauge),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property);
            MethodInfo setter = property.GetSetMethod(true);
            Assert.IsNotNull(setter);
            setter.Invoke(controller, new object[] { value });
        }

        private sealed class FixedRaidRandomSource : IRaidRandomSource
        {
            public float Next01()
            {
                return 0f;
            }

            public int NextInt(int minInclusive, int maxExclusive)
            {
                return minInclusive;
            }
        }
    }
}
#endif
