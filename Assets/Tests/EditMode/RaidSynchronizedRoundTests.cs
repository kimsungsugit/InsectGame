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
            // 스킬을 들고 있으면 비-리더도 자기 스킬(SupportSkill)로 참여한다.
            // 예전엔 전원이 SupportAssist(ATK×0.25 고정)였다.
            Assert.AreEqual(
                4,
                result.TeamActions.Count(action => action.IsSupport));
            Assert.AreEqual(
                4,
                result.TeamActions.Count(action => action.Kind == RaidActionKind.SupportSkill));
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

        // ── 기절: 명중 판정 + 연속 잠금 방지 ──
        //
        // 예전엔 컨트롤러가 `effectType == Stun`만 보고 무조건 걸었다(명중률 무시). 게다가 기절 분기가
        // bossCooldown 갱신을 통째로 건너뛰어, 전체공격 예고 턴마다 기절을 맞추면 보스가
        // **전체공격을 영원히 예고만** 했다. 아래 셋이 그 셋을 각각 고정한다.

        [Test]
        public void StunnedAreaIntent_IsDelayedNotDenied()
        {
            InsectSkill stun = CreateSkill("stun", SkillEffectType.Stun, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 100000, 120, 100),
                CreateTeam(5, 3000, 40, 80),
                new[] { stun });
            Assert.AreEqual(RaidBossIntentKind.AreaAttack, raid.NextBossIntent.Kind,
                "전제: 1라운드 예고는 전체공격이다");

            raid.ResolveTeamCommand(0);
            Assert.IsTrue(raid.ResolveBossResponse().BossResponseSkipped);
            Assert.IsTrue(raid.CompleteRoundPresentation());
            Assert.AreEqual(RaidBossIntentKind.AreaAttack, raid.NextBossIntent.Kind,
                "스킵된 전체공격은 소비되지 않고 다시 예고돼야 한다");

            // 2라운드에도 같은 기절기를 쓴다 — 면역이 없으면 여기서 또 스킵돼 영구 봉인이 된다.
            int hpBefore = raid.TeamStats.Sum(stats => stats.CurrentHp);
            raid.ResolveTeamCommand(0);
            RaidRoundResult second = raid.ResolveBossResponse();

            Assert.IsFalse(second.BossResponseSkipped, "연속 기절은 막혀야 한다");
            Assert.IsTrue(raid.BossUsedAoe, "미뤄둔 전체공격이 실제로 나가야 한다");
            Assert.Less(raid.TeamStats.Sum(stats => stats.CurrentHp), hpBefore);
        }

        [Test]
        public void Stun_TwoRoundsInARow_SecondIsResisted()
        {
            InsectSkill stun = CreateSkill("stun", SkillEffectType.Stun, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 100000, 60, 100),
                CreateTeam(5, 3000, 40, 80),
                new[] { stun });

            raid.ResolveTeamCommand(0);
            raid.ResolveBossResponse();
            raid.CompleteRoundPresentation();
            raid.ResolveTeamCommand(0);

            StringAssert.Contains("저항", raid.LastActionText,
                "면역으로 막힌 기절은 턴을 소비하므로 플레이어에게 알려야 한다");
        }

        [Test]
        public void Stun_WhenRollMisses_BossActsNormally()
        {
            InsectSkill stun = CreateSkill("stun", SkillEffectType.Stun, 1, 0);
            stun.accuracy = 0.6f;   // 1미만이어야 명중을 굴린다
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 100000, 120, 100),
                CreateTeam(5, 3000, 40, 80),
                new[] { stun },
                new MissingRaidRandomSource());
            int hpBefore = raid.TeamStats.Sum(stats => stats.CurrentHp);

            RaidRoundResult teamRound = raid.ResolveTeamCommand(0);
            RaidRoundResult bossRound = raid.ResolveBossResponse();

            Assert.IsTrue(teamRound.TeamActions[0].Missed);
            Assert.IsFalse(teamRound.TeamActions[0].StunApplied);
            Assert.IsFalse(bossRound.BossResponseSkipped, "빗나간 기절로 보스 턴이 사라지면 안 된다");
            Assert.Less(raid.TeamStats.Sum(stats => stats.CurrentHp), hpBefore);
        }

        // ── 상성·자속이 팀 피해 전 경로에 적용되는가 ──
        //
        // 예전엔 리더 스킬만 상성을 탔다. 지원 공격은 `ATK × 0.25` 고정, 합체공격은 속성을
        // 화면에 **표시만** 하고 계산엔 쓰지 않았다 — 피해의 4/5가 팀 편성을 무시했다.

        [Test]
        public void SupportAssist_SuperEffective_ExceedsNeutral()
        {
            Assert.Greater(
                InsectTypeChart.GetEffectiveness(InsectElement.Leaf, InsectElement.Water, InsectElement.None),
                InsectTypeChart.GetEffectiveness(InsectElement.Leaf, InsectElement.Bug, InsectElement.None),
                "전제: 풀→물이 풀→벌레보다 효과적이어야 이 비교가 의미를 갖는다");

            int neutral = MeasureSupportAssistDamage(InsectElement.Leaf, InsectElement.Bug);
            int strong = MeasureSupportAssistDamage(InsectElement.Leaf, InsectElement.Water);

            Assert.Greater(strong, neutral);
        }

        [Test]
        public void UniteContribution_AppliesTypeChart()
        {
            int neutral = MeasureUniteDamage(InsectElement.Leaf, InsectElement.Bug);
            int strong = MeasureUniteDamage(InsectElement.Leaf, InsectElement.Water);

            Assert.Greater(strong, neutral);
        }

        private int MeasureSupportAssistDamage(InsectElement teamElement, InsectElement bossElement)
        {
            InsectSkill skill = CreateSkill("leader_hit", SkillEffectType.Damage, 1, 0);
            // 서포트에게 스킬을 주지 않아 **기본 지원 공격 폴백**을 타게 한다 — 그 경로의 상성을 잰다.
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 1000000, 30, 60, bossElement),
                CreateTeam(5, 3000, 200, 80, teamElement),
                new[] { skill },
                supportsHaveSkills: false);

            RaidRoundResult result = raid.ResolveTeamCommand(0);
            return result.TeamActions
                .First(action => action.Kind == RaidActionKind.SupportAssist)
                .Damage;
        }

        private int MeasureUniteDamage(InsectElement teamElement, InsectElement bossElement)
        {
            InsectSkill skill = CreateSkill("leader_hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 1000000, 30, 60, bossElement),
                CreateTeam(5, 3000, 200, 80, teamElement),
                new[] { skill });
            SetUniteGauge(raid, RaidBattleController.UniteGaugeMax);

            return raid.ResolveUniteCommand().UniteSlotDamages.Sum();
        }

        [Test]
        public void ResolveTeamCommand_EverySlotSetsItsOwnCooldown()
        {
            // 배열(TeamCooldowns)과 TickCooldowns는 원래 슬롯별로 있었는데 **세팅하는 곳이 리더뿐**이라
            // 비-리더 칸이 늘 0이었다 — 즉 서포트는 쿨다운 개념 자체가 없었다.
            InsectSkill skill = CreateSkill("cd_hit", SkillEffectType.Damage, 40, 3);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 1000000, 40, 100),
                CreateTeam(5, 3000, 60, 80),
                new[] { skill });

            raid.ResolveTeamCommand(0);

            for (int slot = 0; slot < raid.TeamCooldowns.Length; slot++)
                Assert.AreEqual(3, raid.TeamCooldowns[slot][0], $"slot {slot}");

            raid.ResolveBossResponse();
            Assert.IsTrue(raid.CompleteRoundPresentation());

            for (int slot = 0; slot < raid.TeamCooldowns.Length; slot++)
                Assert.AreEqual(2, raid.TeamCooldowns[slot][0], $"slot {slot} 틱");
        }

        [Test]
        public void ResolveTeamCommand_SupportsWithoutSkills_FallBackToAssist()
        {
            InsectSkill skill = CreateSkill("leader_hit", SkillEffectType.Damage, 40, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 1000000, 40, 100),
                CreateTeam(5, 3000, 60, 80),
                new[] { skill },
                supportsHaveSkills: false);

            RaidRoundResult result = raid.ResolveTeamCommand(0);

            Assert.AreEqual(
                4,
                result.TeamActions.Count(action => action.Kind == RaidActionKind.SupportAssist),
                "쓸 스킬이 없으면 턴을 비우지 않고 기본 지원 공격으로 폴백해야 한다");
            Assert.IsTrue(result.TeamActions.All(action => action.IsLeader || action.IsSupport));
        }

        private RaidBattleController CreateRaid(InsectData bossData,
            InsectData[] teamData, InsectSkill[] equippedSkills,
            IRaidRandomSource random = null, bool supportsHaveSkills = true)
        {
            GameObject bossObject = Track(new GameObject("RaidTestBoss"));
            InsectEntity bossEntity = bossObject.AddComponent<InsectEntity>();
            SetField(bossEntity, "data", bossData);
            SetField(bossEntity, "level", 10);

            GameObject controllerObject = Track(new GameObject("RaidTestController"));
            RaidBattleController controller =
                controllerObject.AddComponent<RaidBattleController>();
            controller.SetRandomSource(random ?? new FixedRaidRandomSource());

            int[] levels = Enumerable.Repeat(10, teamData.Length).ToArray();
            InsectSkill[][] skills = new InsectSkill[teamData.Length][];
            for (int i = 0; i < skills.Length; i++)
            {
                // 슬롯 0이 리더(ActiveSlot 기본값). supportsHaveSkills=false면 나머지는 빈 손패라
                // 플래너가 -1을 돌려주고 기본 지원 공격 폴백을 탄다.
                skills[i] = i == 0 || supportsHaveSkills
                    ? equippedSkills
                    : new InsectSkill[0];
            }

            controller.StartRaid(
                bossEntity,
                teamData,
                levels,
                null,
                skills);
            return controller;
        }

        private InsectData[] CreateTeam(int count, int hp, int attack, int defense,
            InsectElement primary = InsectElement.Bug)
        {
            InsectData[] result = new InsectData[count];
            for (int i = 0; i < count; i++)
                result[i] = CreateData($"team_{i}", hp, attack, defense, primary);
            return result;
        }

        private InsectData CreateData(string id, int hp, int attack, int defense,
            InsectElement primary = InsectElement.Bug)
        {
            InsectData data = Track(ScriptableObject.CreateInstance<InsectData>());
            data.insectId = id;
            data.displayName = id;
            data.primaryType = primary;
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

        /// <summary>모든 명중이 성공한다(<c>RollHit</c>는 <c>roll &lt; hitChance</c>).</summary>
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

        /// <summary>
        /// 모든 명중이 실패한다. <c>hitChance</c>는 0.3~1로 clamp되므로 0.99면 어떤 명중률에도 빗나간다.
        /// 이게 없으면 "빗나갔을 때"를 테스트할 수단 자체가 없었다(기존 소스는 0f 하나뿐).
        /// </summary>
        private sealed class MissingRaidRandomSource : IRaidRandomSource
        {
            public float Next01()
            {
                return 0.99f;
            }

            public int NextInt(int minInclusive, int maxExclusive)
            {
                return minInclusive;
            }
        }
    }
}
#endif
