#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 레이드 진입 가드 — 팀 전원이 기절(HP 0)한 상태로 시작하면 살아 있는 슬롯이 없어
    /// <c>ActiveSlot</c>이 -1로 남고, 팀이 행동해야 오는 보스 턴도 그 안에만 있는 패배 판정도
    /// 영영 오지 않는다(= 조작도 종료도 불가). 그래서 <b>시작 자체를 막는다.</b>
    ///
    /// 레이드 패배 후 <c>PersistTeamHp</c>가 팀 5마리를 전부 0 HP로 저장하므로 실제로 재현되는 경로다.
    /// </summary>
    [TestFixture]
    public class RaidStartGuardTests
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
        public void StartRaid_AllTeamMembersFainted_DoesNotStart()
        {
            RaidBattleController raid = CreateController();
            InsectEntity boss = CreateBossEntity();

            bool started = raid.StartRaid(
                boss, CreateTeam(5), Levels(5), CreatePids(5, aliveSlot: -1), Skills(5));

            Assert.IsFalse(started, "전원 기절이면 시작하지 않아야 한다");
            Assert.IsFalse(raid.IsActive);
            Assert.IsNull(raid.TeamStats, "실패한 시작이 팀 상태를 남기면 다음 진입이 그 잔재를 본다");
            Assert.IsNull(raid.BossStats);
        }

        [Test]
        public void StartRaid_AllTeamMembersFainted_ReleasesBossEngagement()
        {
            RaidBattleController raid = CreateController();
            InsectEntity boss = CreateBossEntity();

            raid.StartRaid(boss, CreateTeam(5), Levels(5), CreatePids(5, aliveSlot: -1), Skills(5));

            // StartRaid는 판정 전에 이미 SetEngaged(true)를 건다 — 되돌리지 않으면 보스가
            // 필드에 붙박여 도주도 다른 상호작용도 못 하는 유령이 된다.
            Assert.IsFalse(
                GetPrivateBool(boss, "engaged"),
                "시작에 실패했으면 보스를 필드에 묶어두면 안 된다");
        }

        [Test]
        public void StartRaid_OnlyOneMemberAlive_StartsWithThatSlotActive()
        {
            RaidBattleController raid = CreateController();

            bool started = raid.StartRaid(
                CreateBossEntity(), CreateTeam(5), Levels(5), CreatePids(5, aliveSlot: 3), Skills(5));

            Assert.IsTrue(started, "한 마리라도 살아 있으면 레이드는 가능하다");
            Assert.IsTrue(raid.IsActive);
            Assert.AreEqual(1, raid.AliveCount());
            Assert.AreEqual(3, raid.ActiveSlot, "리더는 생존 슬롯이어야 한다");
        }

        [Test]
        public void StartRaid_NoPersistedHp_StartsAtFullHp()
        {
            RaidBattleController raid = CreateController();

            // currentHp = -1(미초기화 센티넬)은 구세이브·신규 곤충이다 — 기절이 아니라 풀피다.
            PlayerInsectData[] pids = CreatePids(5, aliveSlot: -1);
            foreach (PlayerInsectData pid in pids) pid.currentHp = -1;

            Assert.IsTrue(raid.StartRaid(CreateBossEntity(), CreateTeam(5), Levels(5), pids, Skills(5)));
            Assert.AreEqual(5, raid.AliveCount());
        }

        // ── 헬퍼 ──

        private RaidBattleController CreateController()
        {
            GameObject controllerObject = Track(new GameObject("RaidGuardTestController"));
            RaidBattleController controller = controllerObject.AddComponent<RaidBattleController>();
            controller.SetRandomSource(new FixedRaidRandomSource());
            return controller;
        }

        private InsectEntity CreateBossEntity()
        {
            GameObject bossObject = Track(new GameObject("RaidGuardTestBoss"));
            InsectEntity boss = bossObject.AddComponent<InsectEntity>();
            SetField(boss, "data", CreateData("boss", 4000, 40, 90));
            SetField(boss, "level", 10);
            return boss;
        }

        private InsectData[] CreateTeam(int count)
        {
            InsectData[] team = new InsectData[count];
            for (int i = 0; i < count; i++)
                team[i] = CreateData($"team_{i}", 300, 50, 60);
            return team;
        }

        /// <summary>
        /// <paramref name="aliveSlot"/>만 온전하고 나머지는 기절(<c>currentHp = 0</c>).
        /// -1을 주면 전원 기절.
        /// </summary>
        private static PlayerInsectData[] CreatePids(int count, int aliveSlot)
        {
            PlayerInsectData[] pids = new PlayerInsectData[count];
            for (int i = 0; i < count; i++)
            {
                pids[i] = new PlayerInsectData
                {
                    instanceId = $"inst_{i}",
                    insectId = $"team_{i}",
                    level = 10,
                    currentHp = i == aliveSlot ? 120 : 0
                };
            }
            return pids;
        }

        private static int[] Levels(int count)
        {
            return Enumerable.Repeat(10, count).ToArray();
        }

        private InsectSkill[][] Skills(int count)
        {
            InsectSkill[] equipped = { CreateSkill("guard_test_hit", SkillEffectType.Damage, 8, 0) };
            InsectSkill[][] skills = new InsectSkill[count][];
            for (int i = 0; i < count; i++) skills[i] = equipped;
            return skills;
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

        private InsectSkill CreateSkill(string id, SkillEffectType effectType, int power, int cooldown)
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

        private static bool GetPrivateBool(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return (bool)field.GetValue(target);
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
