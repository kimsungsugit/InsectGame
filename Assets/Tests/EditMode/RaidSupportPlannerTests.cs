#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Battle;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 비-리더 팀원의 스킬 선택 AI. <b>난수를 쓰지 않으므로</b> 주입 없이 전 케이스를 고정할 수 있다.
    ///
    /// 이 클래스가 생기기 전엔 리더 외 4마리가 <c>Attack × 0.25</c> 고정 지원 공격만 했다 —
    /// 스킬도 상성도 자속도 없어서 어떤 곤충을 넣든 결과가 같았다.
    /// </summary>
    [TestFixture]
    public class RaidSupportPlannerTests
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
        public void SelectSupportSkillIndex_PicksHighestEffectiveSkill()
        {
            // 같은 위력인데 속성만 다르다 — 상성이 좋은 쪽이 뽑혀야 한다.
            Assert.Greater(
                InsectTypeChart.GetEffectiveness(InsectElement.Leaf, InsectElement.Water, InsectElement.None),
                InsectTypeChart.GetEffectiveness(InsectElement.Light, InsectElement.Water, InsectElement.None),
                "전제: 물 보스에게 풀이 빛보다 효과적이어야 이 비교가 성립한다");

            InsectSkill weak = Skill("light", SkillEffectType.Damage, 40, element: InsectElement.Light);
            InsectSkill strong = Skill("leaf", SkillEffectType.Damage, 40, element: InsectElement.Leaf);
            InsectBattleStats attacker = Stats("ally", 300, 60, 60, InsectElement.Bug);
            InsectBattleStats boss = Stats("boss", 9000, 120, 100, InsectElement.Water);

            int picked = RaidSupportPlanner.SelectSupportSkillIndex(
                1, attacker, new[] { weak, strong }, new[] { 0, 0 },
                boss, new[] { attacker }, null, RaidTeamStance.Assault);

            Assert.AreEqual(1, picked);
        }

        [Test]
        public void SelectSupportSkillIndex_SkipsCooldownSkills()
        {
            InsectSkill strong = Skill("big", SkillEffectType.Damage, 90);
            InsectSkill weak = Skill("small", SkillEffectType.Damage, 10);
            InsectBattleStats attacker = Stats("ally", 300, 60, 60);
            InsectBattleStats boss = Stats("boss", 9000, 120, 100);

            int picked = RaidSupportPlanner.SelectSupportSkillIndex(
                1, attacker, new[] { strong, weak }, new[] { 2, 0 },
                boss, new[] { attacker }, null, RaidTeamStance.Assault);

            Assert.AreEqual(1, picked, "쿨다운 중인 최강 스킬 대신 쓸 수 있는 것을 골라야 한다");
        }

        [Test]
        public void SelectSupportSkillIndex_AllOnCooldown_ReturnsMinusOne()
        {
            // -1이면 호출부가 기존 기본 지원 공격으로 폴백한다 — 턴이 비지 않는다.
            InsectSkill a = Skill("a", SkillEffectType.Damage, 40);
            InsectSkill b = Skill("b", SkillEffectType.Damage, 40);
            InsectBattleStats attacker = Stats("ally", 300, 60, 60);
            InsectBattleStats boss = Stats("boss", 9000, 120, 100);

            int picked = RaidSupportPlanner.SelectSupportSkillIndex(
                1, attacker, new[] { a, b }, new[] { 1, 3 },
                boss, new[] { attacker }, null, RaidTeamStance.Assault);

            Assert.AreEqual(-1, picked);
        }

        [Test]
        public void SelectSupportSkillIndex_NoSkills_ReturnsMinusOne()
        {
            InsectBattleStats attacker = Stats("ally", 300, 60, 60);
            InsectBattleStats boss = Stats("boss", 9000, 120, 100);

            Assert.AreEqual(-1, RaidSupportPlanner.SelectSupportSkillIndex(
                1, attacker, null, null, boss, new[] { attacker }, null, RaidTeamStance.Assault));
            Assert.AreEqual(-1, RaidSupportPlanner.SelectSupportSkillIndex(
                1, attacker, new InsectSkill[0], new int[0], boss,
                new[] { attacker }, null, RaidTeamStance.Assault));
        }

        [Test]
        public void SelectSupportSkillIndex_PrefersStunWhenIntentIsArea()
        {
            // 전체공격 예고를 읽으면 기절이 최우선 — 팀 전원분 피해를 통째로 지운다.
            InsectSkill hit = Skill("hit", SkillEffectType.Damage, 60);
            InsectSkill stun = Skill("stun", SkillEffectType.Stun, 1);
            InsectBattleStats attacker = Stats("ally", 300, 60, 60);
            InsectBattleStats boss = Stats("boss", 9000, 120, 100);
            InsectBattleStats[] team = { attacker, attacker, attacker, attacker, attacker };

            int onArea = RaidSupportPlanner.SelectSupportSkillIndex(
                1, attacker, new[] { hit, stun }, new[] { 0, 0 },
                boss, team, Intent(true, -1), RaidTeamStance.Assault);
            int onSingle = RaidSupportPlanner.SelectSupportSkillIndex(
                1, attacker, new[] { hit, stun }, new[] { 0, 0 },
                boss, team, Intent(false, 3), RaidTeamStance.Assault);

            Assert.AreEqual(1, onArea, "전체공격 예고면 기절");
            Assert.AreEqual(0, onSingle, "단일 예고면 굳이 기절을 쓰지 않는다");
        }

        [Test]
        public void SelectSupportSkillIndex_TiesBreakByLowestIndex()
        {
            // 같은 점수면 항상 앞 인덱스 — 난수를 쓰지 않으므로 라운드가 결정론적으로 남는다.
            InsectSkill first = Skill("a", SkillEffectType.Damage, 40);
            InsectSkill second = Skill("b", SkillEffectType.Damage, 40);
            InsectBattleStats attacker = Stats("ally", 300, 60, 60);
            InsectBattleStats boss = Stats("boss", 9000, 120, 100);

            for (int repeat = 0; repeat < 3; repeat++)
            {
                Assert.AreEqual(0, RaidSupportPlanner.SelectSupportSkillIndex(
                    1, attacker, new[] { first, second }, new[] { 0, 0 },
                    boss, new[] { attacker }, null, RaidTeamStance.Assault));
            }
        }

        [Test]
        public void SelectSupportSkillIndex_HealOnlyWhenHurt()
        {
            InsectSkill hit = Skill("hit", SkillEffectType.Damage, 5);
            InsectSkill heal = Skill("heal", SkillEffectType.Heal, 1, effectValue: 0.5f);
            InsectBattleStats boss = Stats("boss", 9000, 120, 100);

            InsectBattleStats healthy = Stats("ally", 300, 60, 60);
            Assert.AreEqual(0, RaidSupportPlanner.SelectSupportSkillIndex(
                1, healthy, new[] { hit, heal }, new[] { 0, 0 },
                boss, new[] { healthy }, null, RaidTeamStance.Support),
                "멀쩡한데 회복을 쓰면 턴 낭비다");

            InsectBattleStats hurt = Stats("ally", 300, 60, 60);
            hurt.ApplyDamage(hurt.MaxHp - 1);
            Assert.AreEqual(1, RaidSupportPlanner.SelectSupportSkillIndex(
                1, hurt, new[] { hit, heal }, new[] { 0, 0 },
                boss, new[] { hurt }, null, RaidTeamStance.Support));
        }

        [Test]
        public void SelectSupportSkillIndex_BuffAtStackCap_IsNotChosen()
        {
            // 상한에 닿은 버프는 Capped로 턴만 소비한다 — 점수 0으로 걸러야 한다.
            // 팀이 시전자 하나뿐이라 "전원 상한"이 곧 시전자 상한이다.
            InsectSkill buff = Skill("buff", SkillEffectType.BuffAttack, 1, effectValue: 0.3f);
            InsectBattleStats attacker = Stats("ally", 300, 60, 60);
            InsectBattleStats boss = Stats("boss", 9000, 120, 100);

            Assert.AreEqual(0, RaidSupportPlanner.SelectSupportSkillIndex(
                1, attacker, new[] { buff }, new[] { 0 },
                boss, new[] { attacker }, null, RaidTeamStance.Assault));

            for (int i = 0; i < GameConstantsStackCap; i++)
                Assert.IsTrue(attacker.TryStackAttackBonus(0.3f));

            Assert.AreEqual(-1, RaidSupportPlanner.SelectSupportSkillIndex(
                1, attacker, new[] { buff }, new[] { 0 },
                boss, new[] { attacker }, null, RaidTeamStance.Assault));
        }

        [Test]
        public void SelectSupportSkillIndex_CasterCappedButAlliesHaveRoom_StillBuffs()
        {
            // ★ 버프가 팀 전체로 바뀐 뒤의 계약 — 시전자 스택만 보고 0을 내면 아군 여유를 버린다.
            InsectSkill buff = Skill("buff", SkillEffectType.BuffAttack, 1, effectValue: 0.3f);
            InsectBattleStats caster = Stats("caster", 300, 60, 60);
            InsectBattleStats ally = Stats("ally", 300, 60, 60);
            InsectBattleStats boss = Stats("boss", 9000, 120, 100);
            for (int i = 0; i < GameConstantsStackCap; i++)
                caster.TryStackAttackBonus(0.3f);

            Assert.AreEqual(0, RaidSupportPlanner.SelectSupportSkillIndex(
                1, caster, new[] { buff }, new[] { 0 },
                boss, new[] { caster, ally }, null, RaidTeamStance.Assault),
                "시전자가 상한이어도 아군에게 여유가 있으면 여전히 쓸 값이 있다");
        }

        [Test]
        public void SelectSupportSkillIndex_EveryTargetCapped_SkipsBuff()
        {
            InsectSkill buff = Skill("buff", SkillEffectType.BuffAttack, 1, effectValue: 0.3f);
            InsectBattleStats caster = Stats("caster", 300, 60, 60);
            InsectBattleStats ally = Stats("ally", 300, 60, 60);
            InsectBattleStats boss = Stats("boss", 9000, 120, 100);
            for (int i = 0; i < GameConstantsStackCap; i++)
            {
                caster.TryStackAttackBonus(0.3f);
                ally.TryStackAttackBonus(0.3f);
            }

            Assert.AreEqual(-1, RaidSupportPlanner.SelectSupportSkillIndex(
                1, caster, new[] { buff }, new[] { 0 },
                boss, new[] { caster, ally }, null, RaidTeamStance.Assault));
        }

        [Test]
        public void SelectSupportSkillIndex_BossStunImmune_DoesNotPickStun()
        {
            // 면역 라운드에 기절을 걸면 저항당해 턴만 소비한다 — 버프의 상한 가드와 같은 이유로 거른다.
            InsectSkill hit = Skill("hit", SkillEffectType.Damage, 60);
            InsectSkill stun = Skill("stun", SkillEffectType.Stun, 1);
            InsectBattleStats attacker = Stats("ally", 300, 60, 60);
            InsectBattleStats boss = Stats("boss", 9000, 120, 100);
            InsectBattleStats[] team = { attacker, attacker, attacker, attacker, attacker };
            // **단일 대상 예고로 검증한다** — RaidBossUsesAreaAttack가 false라 프로덕션은
            // AOE 예고를 절대 내지 않는다. AOE로만 검증하면 실제로 안 도는 분기를 고정하게 된다.
            RaidBossIntent single = Intent(false, 1);

            // Guard 스탠스 — 기절 가중치가 가장 높다(1.4). 단일 대상 예고에서 기절 가치는
            // 전체공격의 alive배가 아니라 0.4배라, Assault로는 데미지가 이기는 게 정상이다.
            Assert.AreEqual(1, RaidSupportPlanner.SelectSupportSkillIndex(
                1, attacker, new[] { hit, stun }, new[] { 0, 0 },
                boss, team, single, RaidTeamStance.Guard, bossStunImmune: false),
                "면역이 아니면 Guard 스탠스에서 기절을 고른다");

            Assert.AreEqual(0, RaidSupportPlanner.SelectSupportSkillIndex(
                1, attacker, new[] { hit, stun }, new[] { 0, 0 },
                boss, team, single, RaidTeamStance.Guard, bossStunImmune: true),
                "면역이면(또는 이번 라운드에 이미 명중했으면) 기절을 버리고 다른 걸 쓴다");
        }

        [Test]
        public void SelectSupportSkillIndex_GuardStance_ShiftsPickToDefensive()
        {
            // 같은 손패로 스탠스만 바꾸면 선택이 달라져야 한다 — 스탠스가 실제로 AI를 조종하는지.
            InsectSkill hit = Skill("hit", SkillEffectType.Damage, 55);
            InsectSkill guard = Skill("guard", SkillEffectType.DefenseBuff, 1, effectValue: 0.4f);
            InsectBattleStats attacker = Stats("ally", 300, 60, 60);
            InsectBattleStats boss = Stats("boss", 9000, 200, 100);
            InsectBattleStats[] team = { attacker, attacker };
            // 단일 대상 예고 + **자기가 지목되지 않은** 슬롯. 프로덕션이 실제로 타는 경로이고,
            // 옛 코드는 여기서 방어 버프를 0으로 버려 스탠스를 바꿔도 선택이 안 바뀌었다.
            RaidBossIntent single = Intent(false, 0);

            int assault = RaidSupportPlanner.SelectSupportSkillIndex(
                1, attacker, new[] { hit, guard }, new[] { 0, 0 },
                boss, team, single, RaidTeamStance.Assault);
            int guarded = RaidSupportPlanner.SelectSupportSkillIndex(
                1, attacker, new[] { hit, guard }, new[] { 0, 0 },
                boss, team, single, RaidTeamStance.Guard);

            Assert.AreEqual(0, assault);
            Assert.AreEqual(1, guarded);
        }

        private const int GameConstantsStackCap = 3;

        private static RaidBossIntent Intent(bool isArea, int targetSlot)
        {
            return new RaidBossIntent
            {
                Kind = isArea ? RaidBossIntentKind.AreaAttack : RaidBossIntentKind.SingleTarget,
                TargetSlot = targetSlot,
                Element = InsectElement.Bug,
                EffectType = SkillEffectType.Damage,
                DisplayName = isArea ? "전체 공격" : "공격"
            };
        }

        private InsectBattleStats Stats(string id, int hp, int attack, int defense,
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
            return new InsectBattleStats(data, 10);
        }

        private InsectSkill Skill(string id, SkillEffectType effectType, int power,
            InsectElement element = InsectElement.Bug, float effectValue = 0f)
        {
            InsectSkill skill = Track(ScriptableObject.CreateInstance<InsectSkill>());
            skill.skillId = id;
            skill.displayName = id;
            skill.element = element;
            skill.effectType = effectType;
            skill.power = power;
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
