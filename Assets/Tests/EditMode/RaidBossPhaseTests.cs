#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InsectGame.Battle;
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
            // 평상시 전체공격 간격과 격노 후 간격을 같은 방식으로 재서 비교한다.
            int calm = RoundsBetweenAreaAttacks(enrage: false);
            int enraged = RoundsBetweenAreaAttacks(enrage: true);

            Assert.Greater(calm, enraged, "격노하면 전체공격이 더 자주 와야 한다");
            Assert.AreEqual(GameConstantsAreaInterval + 1, calm);
            Assert.AreEqual(GameConstantsEnragedAreaInterval + 1, enraged);
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

        private const int GameConstantsAreaInterval = 2;
        private const int GameConstantsEnragedAreaInterval = 1;

        /// <summary>전체공격이 실행된 뒤 다음 전체공격 예고까지 걸리는 라운드 수.</summary>
        private int RoundsBetweenAreaAttacks(bool enrage)
        {
            RaidBattleController raid = Raid(SignatureCount(0));
            if (enrage)
            {
                raid.BossStats.ApplyDamage(raid.BossStats.MaxHp * 6 / 10);
                RunRound(raid);   // 1라운드 예고는 전체공격 — 여기서 격노가 켜진다
                Assert.IsTrue(raid.BossEnraged);
            }

            // 전체공격이 실제로 실행되는 라운드까지 진행한다.
            while (raid.NextBossIntent == null
                || raid.NextBossIntent.Kind != RaidBossIntentKind.AreaAttack)
            {
                RunRound(raid);
            }

            RunRound(raid);   // 그 전체공격을 실행 → 쿨다운이 걸린다

            int rounds = 1;
            while (raid.NextBossIntent == null
                || raid.NextBossIntent.Kind != RaidBossIntentKind.AreaAttack)
            {
                RunRound(raid);
                rounds++;
            }

            return rounds;
        }

        private void RunRound(RaidBattleController raid)
        {
            Assert.IsNotNull(raid.ResolveTeamCommand(0), "팀 커맨드가 받아들여져야 한다");
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
