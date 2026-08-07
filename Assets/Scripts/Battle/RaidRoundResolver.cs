using System.Collections.Generic;
using InsectGame.Core;
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

        /// <summary>합체공격 1인분 배율. 기여 자체가 스킬이 아니라 고정 위력이므로 여기서만 쓴다.</summary>
        public const float UniteContributionMultiplier = 1.5f;

        /// <summary>
        /// 난수원이 주입되지 않았을 때 쓰는 공유 인스턴스. <see cref="UnityRaidRandomSource"/>는 상태가
        /// 없어 공유해도 안전하다 — 호출마다 <c>new</c>하던 것을 없앤다(명중 판정이 늘어나면서 그 자리가 셋이 됐다).
        /// </summary>
        private static readonly IRaidRandomSource SharedRandom = new UnityRaidRandomSource();

        /// <summary>
        /// 명중 판정. 명중률이 사실상 1이면 굴리지 않는다(난수 소비 순서를 바꾸지 않기 위해서다 —
        /// 테스트가 <see cref="IRaidRandomSource"/> 주입으로 시나리오를 고정한다).
        /// 1v1과 같은 <see cref="InsectBattleController.RollHit"/>을 쓴다.
        /// </summary>
        private static bool Lands(InsectSkill skill, IRaidRandomSource random)
        {
            if (skill == null || skill.accuracy >= 0.999f) return true;
            return InsectBattleController.RollHit(
                skill.accuracy, 0f, (random ?? SharedRandom).Next01());
        }

        public static RaidBossIntent CreateBossIntent(int roundNumber,
            InsectBattleStats boss, InsectBattleStats[] team, int roundsUntilAreaAttack,
            InsectSkill signatureSkill, IRaidRandomSource random)
        {
            IRaidRandomSource source = random ?? SharedRandom;
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
            InsectBattleStats attacker, InsectBattleStats boss, InsectBattleStats[] team,
            InsectSkill skill, IRaidRandomSource random)
        {
            return ResolveTeamSkill(
                RaidActionKind.LeaderSkill, 1f, slot, skillIndex,
                attacker, boss, team, skill, random);
        }

        /// <summary>
        /// 리더가 아닌 팀원이 <b>자기 스킬</b>을 쓴다. 리더와 같은 효과 처리를 타고 위력만 낮다.
        /// 어느 스킬을 쓸지는 <see cref="RaidSupportPlanner"/>가 정하고 여기선 실행만 한다.
        /// </summary>
        public static RaidActionResult ResolveSupportSkill(int slot, int skillIndex,
            InsectBattleStats attacker, InsectBattleStats boss, InsectBattleStats[] team,
            InsectSkill skill, IRaidRandomSource random)
        {
            return ResolveTeamSkill(
                RaidActionKind.SupportSkill,
                GameConstants.Battle.RaidSupportSkillPowerMultiplier,
                slot, skillIndex, attacker, boss, team, skill, random);
        }

        /// <summary>
        /// 팀 스킬 실행의 <b>단일 처리부</b> — 리더와 서포트가 같은 7종 switch를 공유한다.
        /// 둘을 따로 두면 새 효과 타입이 한쪽에만 들어가 경로마다 다르게 도는 일이 반복된다
        /// (실제로 <c>BattleScreenUI.GetSkillColor</c>가 7종 중 3종에 멈춰 있었다).
        ///
        /// <paramref name="powerMultiplier"/>는 <b>피해와 회복량에만</b> 곱한다. 버프·디버프·기절은
        /// 스택이거나 불리언이라 배율에 의미가 없고, 총량은 이미
        /// <c>GameConstants.Battle.MaxBuffStacks</c>가 가둔다.
        /// </summary>
        private static RaidActionResult ResolveTeamSkill(RaidActionKind kind,
            float powerMultiplier, int slot, int skillIndex,
            InsectBattleStats attacker, InsectBattleStats boss, InsectBattleStats[] team,
            InsectSkill skill, IRaidRandomSource random)
        {
            RaidActionResult result = new RaidActionResult
            {
                Kind = kind,
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
                    if (!Lands(skill, random))
                    {
                        result.Missed = true;
                        return result;
                    }

                    int damage = CalculateSkillDamage(attacker, boss, skill, powerMultiplier);
                    result.Damage = ApplyDamageAndMeasure(
                        boss, damage, attacker.Attack, boss.Defense);
                    result.KnockedOut = result.Damage > 0 && boss.CurrentHp <= 0;
                    return result;

                // 레이드엔 1v1의 효과 만료(AddEffect + RecalculateBonuses)가 없어 보너스에 직접 누적한다.
                // 그래서 GameConstants.Battle.MaxBuffStacks(3회)로 방향별 상한을 건다 — 상한을 넘긴
                // 사용은 값을 바꾸지 않고 result.Capped로 알린다(턴은 소비되므로 UI가 알려야 한다).
                //
                // **상한 안에서는 여전히 만료되지 않는다** — 이건 의도된 divergence다(2026-08-03 결정).
                // break(DebuffAttack) 3회를 쌓으면 남은 라운드 내내 보스 공격이 Clamp 하한 0.3배로
                // 고정된다. 만료를 넣으려면 RaidBattleController:111-113이 같은 필드에 넣어둔
                // 의상·아이템 보너스와 전투 버프를 먼저 분리해야 하고, 그건 레이드 난이도를
                // 실측 없이 올린다. 상한이 최악을 유한하게 만든 지점에서 멈춘다.
                // 버프는 **팀 전체**에 간다 — 시전자 전액, 나머지 생존 아군은 절반.
                // 시전자만 받던 시절엔 탱커·버퍼 역할이 성립하지 않았다(5마리 중 1마리만 강해진다).
                // 스택 상한은 대상별로 그대로 걸리고, Capped는 **전원이 상한일 때만** true다.
                case SkillEffectType.BuffAttack:
                    result.Capped = !ApplyTeamBuff(team, attacker, skill.effectValue, attack: true);
                    return result;

                case SkillEffectType.Heal:
                {
                    // 회복 대상은 **생존 아군 중 최저 HP**(자신 포함). 시전자만 회복하면 힐러가
                    // 성립하지 않는다 — 다친 하나를 골라 살리는 트리아지가 레이드의 서사를 만든다.
                    // 전체 회복이 아닌 이유: 레이드엔 버프 만료가 없어 매 라운드 전원 회복이면
                    // 난이도가 통째로 무너진다.
                    InsectBattleStats healTarget = LowestHpAlly(team, attacker);
                    int hpBefore = healTarget.CurrentHp;
                    int healAmount = Mathf.Max(
                        1,
                        Mathf.RoundToInt(
                            healTarget.MaxHp * Mathf.Clamp01(skill.effectValue) * powerMultiplier));
                    healTarget.Heal(healAmount);
                    result.Healing = Mathf.Max(0, healTarget.CurrentHp - hpBefore);
                    result.TargetSlot = IndexOf(team, healTarget);
                    return result;
                }

                case SkillEffectType.DefenseBuff:
                    result.Capped = !ApplyTeamBuff(team, attacker, skill.effectValue, attack: false);
                    return result;

                // 기절도 명중을 굴린다. 예전엔 리졸버가 아무것도 안 하고 컨트롤러가 effectType만 보고
                // 무조건 걸어서, 1v1에선 LandsHit를 통과해야 하는 기절기가 레이드에선 100%였다.
                case SkillEffectType.Stun:
                    if (!Lands(skill, random))
                    {
                        result.Missed = true;
                        return result;
                    }

                    result.StunApplied = true;
                    return result;

                // 즉시 일괄딜(power × duration)은 그대로 둔다 — 턴당 틱으로 바꾸려면 보스 상태이상
                // 컨테이너와 틱 지점, UI가 새로 필요한데 전투가 duration보다 길면 기댓값이 같다.
                // 다만 명중 판정은 1v1과 맞춘다(그쪽은 LandsHit를 통과해야 Dot가 붙는다).
                case SkillEffectType.PoisonDot:
                    if (!Lands(skill, random))
                    {
                        result.Missed = true;
                        return result;
                    }

                    int dotDamage = Mathf.Max(
                        1,
                        Mathf.RoundToInt(
                            skill.power * Mathf.Max(1, skill.effectDurationTurns) * powerMultiplier));
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

            // 예전엔 `attacker.Attack * 0.25`를 그대로 넘겨 **상성도 자속도 타지 않았다.**
            // 팀 편성(속성 매칭)이 레이드 전 유일한 결정인데 피해의 4/5가 그 결정을 무시했다.
            int basePower = Mathf.Max(
                1, Mathf.RoundToInt(attacker.Attack * SupportAssistPowerMultiplier));
            int baseDamage = CalculateTeamDamage(attacker, boss, element, basePower);
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

            // 합체공격도 상성·자속을 탄다. `result.Element`를 **표시**만 하고 계산엔 안 쓰던 자리 —
            // 화면엔 속성이 뜨는데 수식은 무속성이라 표시와 결과가 어긋나 있었다.
            int basePower = Mathf.Max(
                1,
                Mathf.RoundToInt((15 + attacker.Level * 2) * UniteContributionMultiplier));
            int damage = CalculateTeamDamage(attacker, boss, element, basePower);
            result.Damage = ApplyDamageAndMeasure(
                boss, damage, attacker.Attack, boss.Defense);
            result.KnockedOut = result.Damage > 0 && boss.CurrentHp <= 0;
            return result;
        }

        /// <param name="rageMultiplier">
        /// 격노(HP 절반 이하) 시 <b>단일 대상 피해</b>에만 곱한다. 전체공격은 그대로 두고 대신
        /// 간격이 짧아진다 — 둘 다 세지면 격노 진입이 곧 전멸이 된다(레이드엔 부활·교체가 없다).
        /// </param>
        public static RaidActionResult ResolveBossIntent(RaidBossIntent intent,
            InsectBattleStats boss, InsectBattleStats[] team, int[] damageBySlot,
            float rageMultiplier = 1f)
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
            float rage = Mathf.Max(1f, rageMultiplier);
            int singleTargetDamage = Mathf.Max(1, Mathf.RoundToInt(bossDamage * rage));
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
                        * bossMultiplier * effectiveness * sameTypeBonus * rage));
            }

            result.Damage = ApplyDamageAndMeasure(
                singleTarget, singleTargetDamage, boss.Attack, singleTarget.Defense);
            if (damageBySlot != null && slot < damageBySlot.Length)
                damageBySlot[slot] = result.Damage;
            result.KnockedOut = result.Damage > 0 && singleTarget.CurrentHp <= 0;
            return result;
        }

        /// <summary>버프가 시전자 외 아군에게 전달되는 비율. 시전자는 전액.</summary>
        public const float TeamBuffAllyShare = 0.5f;

        /// <summary>
        /// 시전자에게 전액, 나머지 생존 아군에게 <see cref="TeamBuffAllyShare"/>만큼.
        /// <b>false를 돌려주면 모든 대상이 스택 상한</b>이라는 뜻 — 턴만 소비됐으므로 호출부가
        /// <c>Capped</c>로 알린다. 한 명이라도 실제로 붙었으면 true다.
        /// </summary>
        private static bool ApplyTeamBuff(InsectBattleStats[] team,
            InsectBattleStats caster, float value, bool attack)
        {
            bool applied = caster != null
                && (attack
                    ? caster.TryStackAttackBonus(value)
                    : caster.TryStackDefenseBonus(value));
            if (team == null) return applied;

            float share = value * TeamBuffAllyShare;
            for (int i = 0; i < team.Length; i++)
            {
                InsectBattleStats ally = team[i];
                if (ally == null || ally == caster || ally.CurrentHp <= 0) continue;
                bool ok = attack
                    ? ally.TryStackAttackBonus(share)
                    : ally.TryStackDefenseBonus(share);
                if (ok) applied = true;
            }

            return applied;
        }

        /// <summary>생존 아군 중 HP 비율이 가장 낮은 하나(자신 포함). 동률이면 앞 슬롯 — 결정론 유지.</summary>
        public static InsectBattleStats LowestHpAlly(
            InsectBattleStats[] team, InsectBattleStats fallback)
        {
            if (team == null) return fallback;

            InsectBattleStats best = null;
            float bestRatio = float.MaxValue;
            for (int i = 0; i < team.Length; i++)
            {
                InsectBattleStats ally = team[i];
                if (ally == null || ally.CurrentHp <= 0 || ally.MaxHp <= 0) continue;
                float ratio = ally.CurrentHp / (float)ally.MaxHp;
                if (ratio < bestRatio)
                {
                    bestRatio = ratio;
                    best = ally;
                }
            }

            return best ?? fallback;
        }

        private static int IndexOf(InsectBattleStats[] team, InsectBattleStats target)
        {
            if (team == null || target == null) return -1;
            for (int i = 0; i < team.Length; i++)
                if (team[i] == target) return i;
            return -1;
        }

        public static int ApplyDamageAndMeasure(InsectBattleStats target,
            int amount, int attackerAttack = 0, int defenderDefense = 0)
        {
            if (target == null || target.CurrentHp <= 0) return 0;
            int before = target.CurrentHp;
            target.ApplyDamage(amount, attackerAttack, defenderDefense);
            return Mathf.Max(0, before - target.CurrentHp);
        }

        /// <summary>
        /// 팀이 보스에게 넣는 피해의 <b>단일 계산부</b> — 공격 보너스 배율 → 상성 → 자속.
        ///
        /// 리더 스킬·지원 공격·합체공격 세 경로가 전부 여기를 탄다. 예전엔 리더만 이 계산을 받고
        /// 나머지 둘은 배율조차 없거나(지원) 상성·자속을 건너뛰어(합체), 같은 팀의 같은 곤충이
        /// 어느 경로로 때리느냐에 따라 속성이 있기도 없기도 했다.
        /// </summary>
        private static int CalculateTeamDamage(InsectBattleStats attacker,
            InsectBattleStats boss, InsectElement element, int basePower)
        {
            if (attacker == null || boss == null) return Mathf.Max(1, basePower);

            float multiplier = Mathf.Clamp(1f + attacker.AttackBonus, 0.3f, 3f);
            int damage = Mathf.Max(1, Mathf.RoundToInt(basePower * multiplier));
            float effectiveness = InsectTypeChart.GetEffectiveness(
                element,
                boss.Data != null ? boss.Data.primaryType : InsectElement.None,
                boss.Data != null ? boss.Data.secondaryType : InsectElement.None);
            float sameTypeBonus = attacker.Data != null
                ? InsectTypeChart.GetSameTypeBonus(
                    element,
                    attacker.Data.primaryType,
                    attacker.Data.secondaryType)
                : 1f;
            return Mathf.Max(
                1, Mathf.RoundToInt(damage * effectiveness * sameTypeBonus));
        }

        private static int CalculateSkillDamage(InsectBattleStats attacker,
            InsectBattleStats boss, InsectSkill skill, float powerMultiplier)
        {
            int basePower = Mathf.Max(
                1,
                Mathf.RoundToInt((skill.power + attacker.Level * 2) * powerMultiplier));
            return CalculateTeamDamage(attacker, boss, skill.element, basePower);
        }
    }
}
