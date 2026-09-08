#if UNITY_EDITOR
using System.Collections.Generic;
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
    /// 보스에게 "읽을 거리"를 준 두 가지 — HP 절반 격노 래치와 시그니처 로테이션.
    /// 예전 보스는 AOE/단일/단일 고정 사이클에 시그니처가 learnset <b>첫 항목 영구 고정</b>이라,
    /// 한 번 싸우면 다 본 것이 됐다.
    /// </summary>
    [TestFixture]
    public class RaidBossPhaseTests
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
        public void BossPhase_CrossesHalfHp_LatchesAndNeverUnlatches()
        {
            RaidBattleController raid = Raid(SignatureCount(0));
            Assert.IsFalse(raid.BossEnraged);

            // 정확히 60%를 깎아 40%만 남긴다(ApplyDamage는 atk/def를 안 주면 비율 보정이 없다).
            raid.BossStats.ApplyDamage(raid.BossStats.MaxHp * 6 / 10);
            RunRound(raid);

            Assert.IsTrue(raid.BossEnraged, "HP 절반 이하면 격노해야 한다");

            // 회복해도 풀리지 않는다 — 켜졌다 꺼졌다 하면 어느 국면인지 읽을 수 없다.
            raid.BossStats.Heal(raid.BossStats.MaxHp);
            RunRound(raid);
            Assert.IsTrue(raid.BossEnraged);
        }

        [Test]
        public void BossPhase_AboveHalfHp_StaysCalm()
        {
            RaidBattleController raid = Raid(SignatureCount(0));

            raid.BossStats.ApplyDamage(raid.BossStats.MaxHp / 4);   // 75% 남음
            RunRound(raid);

            Assert.IsFalse(raid.BossEnraged);
        }

        [Test]
        public void Enraged_ShortensAreaAttackInterval()
        {
            // 전체공격은 지금 꺼져 있지만(`GameConstants.Battle.RaidBossUsesAreaAttack`)
            // 간격 상수와 실행 경로는 그대로 살아 있다 — 되돌릴 때를 위해 관계를 고정한다.
            // 예전엔 실제 라운드를 돌려 예고까지의 간격을 셌는데, 스위치가 꺼진 지금 그 루프는
            // 영원히 전체공격 예고를 만나지 못한다(테스트가 멈춘다).
            Assert.Greater(
                GameConstants.Battle.RaidBossAreaInterval,
                GameConstants.Battle.RaidBossEnragedAreaInterval,
                "격노하면 전체공격이 더 자주 와야 한다");
            Assert.GreaterOrEqual(GameConstants.Battle.RaidBossEnragedAreaInterval, 1,
                "간격이 0이면 전체공격만 반복해 단일 공격이 사라진다");
        }

        [Test]
        public void BossIntent_AreaSwitchOff_KeepsSingleTargetEvenAtCountdownZero()
        {
            RaidBattleController raid = Raid(SignatureCount(0));

            RaidBossIntent blocked = RaidRoundResolver.CreateBossIntent(
                1, raid.BossStats, raid.TeamStats, 0, null,
                new LowestSlotRandomSource(), allowAreaAttack: false);
            RaidBossIntent allowed = RaidRoundResolver.CreateBossIntent(
                1, raid.BossStats, raid.TeamStats, 0, null,
                new LowestSlotRandomSource(), allowAreaAttack: true);

            Assert.IsFalse(blocked.IsArea, "스위치가 꺼지면 카운트다운이 0이어도 단일 대상이다");
            Assert.GreaterOrEqual(blocked.TargetSlot, 0, "단일 공격은 대상 슬롯을 정해야 한다");
            Assert.IsTrue(allowed.IsArea, "스위치를 켜면 예전 동작 그대로다");
        }

        [Test]
        public void SignatureRotation_TwoSignatures_AlternateAcrossRounds()
        {
            RaidBattleController raid = Raid(SignatureCount(2));

            HashSet<string> seen = new HashSet<string>();
            for (int round = 0; round < 6; round++)
            {
                if (raid.NextBossIntent != null
                    && raid.NextBossIntent.Kind == RaidBossIntentKind.SignatureSkill
                    && raid.NextBossIntent.Skill != null)
                {
                    seen.Add(raid.NextBossIntent.Skill.skillId);
                }

                RunRound(raid);
            }

            Assert.AreEqual(2, seen.Count,
                "시그니처가 둘이면 라운드마다 번갈아 나와야 한다(예전엔 첫 항목만 영구 고정)");
        }

        [Test]
        public void SignatureRotation_SingleSignature_MatchesLegacyBehaviour()
        {
            // ★ 무위험 폴백 — 시그니처가 하나면 로테이션 도입 전과 완전히 같아야 한다.
            RaidBattleController raid = Raid(SignatureCount(1));

            for (int round = 0; round < 5; round++)
            {
                if (raid.NextBossIntent != null
                    && raid.NextBossIntent.Kind == RaidBossIntentKind.SignatureSkill)
                {
                    Assert.AreEqual("sig_0", raid.NextBossIntent.Skill.skillId);
                }

                RunRound(raid);
            }
        }

        /// <summary>
        /// 라운드 하나를 끝까지 돌린다 — <b>팀 전원이 차례로</b> 행동해야 보스 차례가 온다.
        /// 예전엔 커맨드 한 번이 팀 전체를 움직여서 이 헬퍼도 한 줄이었다.
        /// </summary>
        private void RunRound(RaidBattleController raid)
        {
            int guard = raid.TeamStats.Length + 1;
            bool acted = false;
            while (guard-- > 0 && raid.CanSubmitTeamCommand)
            {
                Assert.IsNotNull(raid.ResolveTeamCommand(0), "팀 커맨드가 받아들여져야 한다");
                acted = true;
            }

            Assert.IsTrue(acted, "팀이 한 번은 행동해야 라운드가 진행된다");
            raid.ResolveBossResponse();
            Assert.IsTrue(raid.CompleteRoundPresentation());
        }

        /// <summary>시그니처 스킬 n개를 가진 보스 learnset.</summary>
        private InsectLearnableSkill[] SignatureCount(int count)
        {
            InsectLearnableSkill[] learnset = new InsectLearnableSkill[count];
            for (int i = 0; i < count; i++)
            {
                InsectSkill sig = Skill($"sig_{i}", SkillEffectType.Damage, 4);
                sig.isSignatureSkill = true;
                learnset[i] = new InsectLearnableSkill
                {
                    skillId = sig.skillId,
                    learnLevel = 1,
                    skill = sig
                };
            }

            return learnset;
        }

        private RaidBattleController Raid(InsectLearnableSkill[] bossLearnset)
        {
            InsectData bossData = Data("boss", 40000, 20, 60);
            bossData.learnset = bossLearnset;

            GameObject bossObject = Track(new GameObject("PhaseTestBoss"));
            InsectEntity bossEntity = bossObject.AddComponent<InsectEntity>();
            SetField(bossEntity, "data", bossData);
            SetField(bossEntity, "level", 10);

            GameObject controllerObject = Track(new GameObject("PhaseTestController"));
            RaidBattleController controller =
                controllerObject.AddComponent<RaidBattleController>();
            controller.SetRandomSource(new LowestSlotRandomSource());

            InsectData[] team = new InsectData[5];
            int[] levels = new int[5];
            InsectSkill[][] skills = new InsectSkill[5][];
            InsectSkill weak = Skill("poke", SkillEffectType.Damage, 1);
            for (int i = 0; i < 5; i++)
            {
                team[i] = Data($"ally_{i}", 4000, 20, 200);
                levels[i] = 10;
                skills[i] = new[] { weak };
            }

            controller.StartRaid(bossEntity, team, levels, null, skills);
            return controller;
        }

        private InsectData Data(string id, int hp, int attack, int defense)
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

        private InsectSkill Skill(string id, SkillEffectType effectType, int power)
        {
            InsectSkill skill = Track(ScriptableObject.CreateInstance<InsectSkill>());
            skill.skillId = id;
            skill.displayName = id;
            skill.element = InsectElement.Bug;
            skill.effectType = effectType;
            skill.power = power;
            skill.cooldownTurns = 0;
            skill.accuracy = 1f;
            return skill;
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private sealed class LowestSlotRandomSource : IRaidRandomSource
        {
            public float Next01() => 0f;
            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        }
    }
}
#endif
