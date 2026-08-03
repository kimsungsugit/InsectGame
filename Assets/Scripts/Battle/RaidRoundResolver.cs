using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Battle
{
    internal sealed class UnityRaidRandomSource : IRaidRandomSource
    {
        public float Next01()
        {
            return Random.value;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return Random.Range(minInclusive, maxExclusive);
        }
    }

    public static class RaidRoundResolver
    {
        public const float SupportAssistPowerMultiplier = 0.25f;

        public static RaidBossIntent CreateBossIntent(int roundNumber,
            InsectBattleStats boss, InsectBattleStats[] team, int roundsUntilAreaAttack,
            InsectSkill signatureSkill, IRaidRandomSource random)
        {
            IRaidRandomSource source = random ?? new UnityRaidRandomSource();
            InsectData bossData = boss != null ? boss.Data : null;

            if (roundsUntilAreaAttack <= 0)
            {
                return new RaidBossIntent
                {
                    RoundNumber = roundNumber,
                    Kind = RaidBossIntentKind.AreaAttack,
                    TargetSlot = -1,
                    Skill = null,
                    Element = bossData != null ? bossData.primaryType : InsectElement.Bug,
                    EffectType = SkillEffectType.Damage,
                    DisplayName = "전체 공격"
                };
            }

            List<int> aliveSlots = new List<int>();
            if (team != null)
            {
                for (int i = 0; i < team.Length; i++)
                {
                    if (team[i] != null && team[i].CurrentHp > 0)
                        aliveSlots.Add(i);
                }
            }

            int targetSlot = -1;
            if (aliveSlots.Count > 0)
            {
                int pick = source.NextInt(0, aliveSlots.Count);
                pick = Mathf.Clamp(pick, 0, aliveSlots.Count - 1);
                targetSlot = aliveSlots[pick];
            }

            bool hasSignature = signatureSkill != null;
            return new RaidBossIntent
            {
                RoundNumber = roundNumber,
                Kind = hasSignature ? RaidBossIntentKind.SignatureSkill : RaidBossIntentKind.SingleTarget,
                TargetSlot = targetSlot,
                Skill = signatureSkill,
                Element = hasSignature
                    ? signatureSkill.element
                    : (bossData != null ? bossData.primaryType : InsectElement.Bug),
                EffectType = hasSignature ? signatureSkill.effectType : SkillEffectType.Damage,
                DisplayName = hasSignature && !string.IsNullOrEmpty(signatureSkill.displayName)
                    ? signatureSkill.displayName
                    : "공격"
            };
        }

        public static RaidActionResult ResolveLeaderSkill(int slot, int skillIndex,
            InsectBattleStats attacker, InsectBattleStats boss, InsectSkill skill,
            IRaidRandomSource random)
        {
            RaidActionResult result = new RaidActionResult
            {
                Kind = RaidActionKind.LeaderSkill,
                SourceSlot = slot,
                TargetSlot = -1,
                SkillIndex = skillIndex,
                Skill = skill,
                Element = skill != null ? skill.element : InsectElement.None,
                EffectType = skill != null ? skill.effectType : SkillEffectType.Damage,
                DisplayName = skill != null && !string.IsNullOrEmpty(skill.displayName)
                    ? skill.displayName
                    : "공격"
            };

            if (attacker == null || boss == null || skill == null)
                return result;

            switch (skill.effectType)
            {
                case SkillEffectType.Damage:
                    if (skill.accuracy < 0.999f
                        && !InsectBattleController.RollHit(
                            skill.accuracy, 0f, (random ?? new UnityRaidRandomSource()).Next01()))
                    {
                        result.Missed = true;
                        return result;
                    }

                    int damage = CalculateLeaderDamage(attacker, boss, skill);
                    result.Damage = ApplyDamageAndMeasure(
                        boss, damage, attacker.Attack, boss.Defense);
                    result.KnockedOut = result.Damage > 0 && boss.CurrentHp <= 0;
                    return result;

                // 레이드엔 1v1의 효과 만료(AddEffect + RecalculateBonuses)가 없어 보너스에 직접 누적한다.
                // 그래서 GameConstants.Battle.MaxBuffStacks(3회)로 방향별 상한을 건다 — 상한을 넘긴
                // 사용은 값을 바꾸지 않고 result.Capped로 알린다(턴은 소비되므로 UI가 알려야 한다).
                case SkillEffectType.BuffAttack:
                    result.Capped = !attacker.TryStackAttackBonus(skill.effectValue);
                    return result;

                case SkillEffectType.Heal:
                    int hpBefore = attacker.CurrentHp;
                    int healAmount = Mathf.Max(
                        1, Mathf.RoundToInt(attacker.MaxHp * Mathf.Clamp01(skill.effectValue)));
                    attacker.Heal(healAmount);
                    result.Healing = Mathf.Max(0, attacker.CurrentHp - hpBefore);
                    return result;

                case SkillEffectType.DefenseBuff:
                    result.Capped = !attacker.TryStackDefenseBonus(skill.effectValue);
                    return result;

                case SkillEffectType.Stun:
                    return result;

                case SkillEffectType.PoisonDot:
                    int dotDamage = Mathf.Max(
                        1, skill.power * Mathf.Max(1, skill.effectDurationTurns));
                    result.Damage = ApplyDamageAndMeasure(boss, dotDamage);
                    result.KnockedOut = result.Damage > 0 && boss.CurrentHp <= 0;
                    return result;

                // 상한이 없던 시절 `break`(effectValue 0.3, 쿨다운 3)를 세 번 쓰면 보스 공격 배율이
                // 하한 0.3에 닿고 남은 라운드 내내 회복되지 않았다. 이제 음수 방향도 3스택에서 멈춘다.
                case SkillEffectType.DebuffAttack:
                    result.Capped = !boss.TryStackAttackBonus(-skill.effectValue);
                    return result;

                default:
                    return result;
            }
        }

        public static RaidActionResult ResolveSupportAssist(int slot,
            InsectBattleStats attacker, InsectBattleStats boss)
        {
            InsectElement element = attacker != null && attacker.Data != null
                ? attacker.Data.primaryType
                : InsectElement.Bug;
            RaidActionResult result = new RaidActionResult
            {
                Kind = RaidActionKind.SupportAssist,
                SourceSlot = slot,
                TargetSlot = -1,
                SkillIndex = -1,
                Skill = null,
                Element = element,
                EffectType = SkillEffectType.Damage,
                DisplayName = "지원 공격"
            };

            if (attacker == null || boss == null || attacker.CurrentHp <= 0)
                return result;

            int baseDamage = Mathf.Max(
                1, Mathf.RoundToInt(attacker.Attack * SupportAssistPowerMultiplier));
            result.Damage = ApplyDamageAndMeasure(
                boss, baseDamage, attacker.Attack, boss.Defense);
            result.KnockedOut = result.Damage > 0 && boss.CurrentHp <= 0;
            return result;
        }

        public static RaidActionResult ResolveUniteContribution(int slot,
            InsectBattleStats attacker, InsectBattleStats boss)
        {
            InsectElement element = attacker != null && attacker.Data != null
                ? attacker.Data.primaryType
                : InsectElement.Bug;
            RaidActionResult result = new RaidActionResult
            {
                Kind = RaidActionKind.UniteContribution,
                SourceSlot = slot,
                TargetSlot = -1,
                SkillIndex = -1,
                Skill = null,
                Element = element,
                EffectType = SkillEffectType.Damage,
                DisplayName = "합체공격"
            };

            if (attacker == null || boss == null || attacker.CurrentHp <= 0)
                return result;

            int baseDamage = 15 + attacker.Level * 2;
            float multiplier = Mathf.Clamp(1f + attacker.AttackBonus, 0.3f, 3f);
            int damage = Mathf.Max(
                1, Mathf.RoundToInt(baseDamage * multiplier * 1.5f));
            result.Damage = ApplyDamageAndMeasure(
                boss, damage, attacker.Attack, boss.Defense);
            result.KnockedOut = result.Damage > 0 && boss.CurrentHp <= 0;
            return result;
        }

        public static RaidActionResult ResolveBossIntent(RaidBossIntent intent,
            InsectBattleStats boss, InsectBattleStats[] team, int[] damageBySlot)
        {
            RaidActionResult result = new RaidActionResult
            {
                Kind = intent != null && intent.IsArea
                    ? RaidActionKind.BossArea
                    : RaidActionKind.BossSingle,
                SourceSlot = -1,
                TargetSlot = intent != null ? intent.TargetSlot : -1,
                SkillIndex = -1,
                Skill = intent != null ? intent.Skill : null,
                Element = intent != null ? intent.Element : InsectElement.Bug,
                EffectType = intent != null ? intent.EffectType : SkillEffectType.Damage,
                DisplayName = intent != null ? intent.DisplayName : "공격"
            };

            if (intent == null || boss == null || team == null)
                return result;

            int bossBaseDamage = 10 + boss.Level * 2;
            float bossMultiplier = Mathf.Clamp(1f + boss.AttackBonus, 0.3f, 3f);
            int bossDamage = Mathf.Max(
                1, Mathf.RoundToInt(bossBaseDamage * bossMultiplier));

            if (intent.IsArea)
            {
                int areaDamage = Mathf.Max(1, bossDamage * 2 / 3);
                bool knockedOutAny = false;
                for (int i = 0; i < team.Length; i++)
                {
                    InsectBattleStats target = team[i];
                    if (target == null || target.CurrentHp <= 0) continue;
                    int actual = ApplyDamageAndMeasure(
                        target, areaDamage, boss.Attack, target.Defense);
                    if (damageBySlot != null && i < damageBySlot.Length)
                        damageBySlot[i] = actual;
                    result.Damage += actual;
                    if (actual > 0 && target.CurrentHp <= 0)
                        knockedOutAny = true;
                }
                result.KnockedOut = knockedOutAny;
                return result;
            }

            int slot = intent.TargetSlot;
            if (slot < 0 || slot >= team.Length
                || team[slot] == null || team[slot].CurrentHp <= 0)
            {
                return result;
            }

            InsectBattleStats singleTarget = team[slot];
            int singleTargetDamage = bossDamage;
            if (intent.Skill != null)
            {
                float effectiveness = InsectTypeChart.GetEffectiveness(
                    intent.Skill.element,
                    singleTarget.Data != null
                        ? singleTarget.Data.primaryType
                        : InsectElement.None,
                    singleTarget.Data != null
                        ? singleTarget.Data.secondaryType
                        : InsectElement.None);
                float sameTypeBonus = boss.Data != null
                    ? InsectTypeChart.GetSameTypeBonus(
                        intent.Skill.element,
                        boss.Data.primaryType,
                        boss.Data.secondaryType)
                    : 1f;
                singleTargetDamage = Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        (intent.Skill.power + boss.Level * 2)
                        * bossMultiplier * effectiveness * sameTypeBonus));
            }

            result.Damage = ApplyDamageAndMeasure(
                singleTarget, singleTargetDamage, boss.Attack, singleTarget.Defense);
            if (damageBySlot != null && slot < damageBySlot.Length)
                damageBySlot[slot] = result.Damage;
            result.KnockedOut = result.Damage > 0 && singleTarget.CurrentHp <= 0;
            return result;
        }

        public static int ApplyDamageAndMeasure(InsectBattleStats target,
            int amount, int attackerAttack = 0, int defenderDefense = 0)
        {
            if (target == null || target.CurrentHp <= 0) return 0;
            int before = target.CurrentHp;
            target.ApplyDamage(amount, attackerAttack, defenderDefense);
            return Mathf.Max(0, before - target.CurrentHp);
        }

        private static int CalculateLeaderDamage(InsectBattleStats attacker,
            InsectBattleStats boss, InsectSkill skill)
        {
            float multiplier = Mathf.Clamp(1f + attacker.AttackBonus, 0.3f, 3f);
            int damage = Mathf.Max(
                1, Mathf.RoundToInt((skill.power + attacker.Level * 2) * multiplier));
            float effectiveness = InsectTypeChart.GetEffectiveness(
                skill.element,
                boss.Data != null ? boss.Data.primaryType : InsectElement.None,
                boss.Data != null ? boss.Data.secondaryType : InsectElement.None);
            float sameTypeBonus = attacker.Data != null
                ? InsectTypeChart.GetSameTypeBonus(
                    skill.element,
                    attacker.Data.primaryType,
                    attacker.Data.secondaryType)
                : 1f;
            return Mathf.Max(
                1, Mathf.RoundToInt(damage * effectiveness * sameTypeBonus));
        }
    }
}
