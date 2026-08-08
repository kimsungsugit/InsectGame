using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 리더가 아닌 레이드 팀원이 이번 라운드에 쓸 스킬을 고른다.
    ///
    /// 예전엔 비-리더 4마리가 <c>Attack × 0.25</c> 고정 지원 공격만 했다 — 스킬도, 상성도, 자속도
    /// 없어서 어떤 곤충을 넣든 결과가 같았다. 팀 편성이 레이드 전 유일한 결정인데 피해의 4/5가
    /// 그 결정을 무시했다는 뜻이다.
    ///
    /// <b>난수를 쓰지 않는다.</b> 동점은 항상 최저 인덱스가 이긴다 — 컨트롤러가 슬롯 순서대로
    /// 계산해 라운드를 결정론적으로 유지하는 것과 같은 이유이고, 덕분에 이 클래스는
    /// <see cref="IRaidRandomSource"/> 주입 없이 전 케이스를 테스트로 고정할 수 있다.
    ///
    /// 점수는 <b>"이번 라운드의 HP 변동 기대치"</b>라는 하나의 단위로 매긴다(피해량 · 막을 피해량 ·
    /// 회복량). 서로 다른 효과를 같은 자로 재야 비교가 성립하기 때문이다.
    /// 0점은 "쓰지 않는다"는 뜻이라 멀쩡할 때의 회복, 상한에 닿은 버프는 자연히 걸러진다.
    /// </summary>
    public static class RaidSupportPlanner
    {
        /// <summary>이 비율 미만으로 깎였을 때만 회복을 후보에 올린다. 멀쩡한데 힐을 쓰면 턴 낭비다.</summary>
        public const float HealNeedRatio = 0.6f;

        /// <summary>
        /// 쓸 스킬의 인덱스. <b>-1이면 쓸 만한 스킬이 없다</b>(스킬 없음 · 전부 쿨다운 · 전부 0점) —
        /// 호출부는 기존 기본 지원 공격으로 폴백한다.
        /// </summary>
        public static int SelectSupportSkillIndex(int slot, InsectBattleStats attacker,
            InsectSkill[] skills, int[] cooldowns, InsectBattleStats boss,
            InsectBattleStats[] team, RaidBossIntent intent, RaidTeamStance stance,
            bool bossStunImmune = false)
        {
            if (attacker == null || boss == null || skills == null) return -1;
            if (attacker.CurrentHp <= 0) return -1;

            int best = -1;
            float bestScore = 0f;
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] == null) continue;
                if (cooldowns != null && i < cooldowns.Length && cooldowns[i] > 0) continue;

                float score = ScoreSkill(
                    slot, attacker, skills[i], boss, team, intent, stance, bossStunImmune);
                // `>`라서 동점이면 앞 인덱스가 남는다 — 결정론 유지(RNG 미사용).
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>이번 라운드에 이 스킬이 만들 HP 변동 기대치. 0이면 쓰지 않는다.</summary>
        internal static float ScoreSkill(int slot, InsectBattleStats attacker,
            InsectSkill skill, InsectBattleStats boss, InsectBattleStats[] team,
            RaidBossIntent intent, RaidTeamStance stance, bool bossStunImmune = false)
        {
            if (attacker == null || boss == null || skill == null) return 0f;

            bool incomingArea = intent != null && intent.IsArea;
            bool targetedAtMe = intent != null && !intent.IsArea && intent.TargetSlot == slot;
            float support = GameConstants.Battle.RaidSupportSkillPowerMultiplier;
            float accuracy = Mathf.Clamp01(skill.accuracy <= 0f ? 1f : skill.accuracy);
            int alive = AliveCount(team);

            switch (skill.effectType)
            {
                case SkillEffectType.Damage:
                {
                    float raw = (skill.power + attacker.Level * 2)
                        * Effectiveness(skill.element, boss)
                        * SameTypeBonus(skill.element, attacker);
                    return raw * support * accuracy * Weight(stance, SkillRole.Offense);
                }

                case SkillEffectType.PoisonDot:
                {
                    float raw = skill.power * Mathf.Max(1, skill.effectDurationTurns);
                    return raw * support * accuracy * Weight(stance, SkillRole.Offense);
                }

                case SkillEffectType.Heal:
                {
                    // 리졸버와 **같은 대상**을 본다 — 생존 아군 중 최저 HP(자신 포함).
                    // 둘이 어긋나면 "멀쩡한 자신을 보고 안 쓰거나, 다친 아군을 보고 썼는데
                    // 정작 자신이 회복되는" 헛턴이 난다.
                    InsectBattleStats target = RaidRoundResolver.LowestHpAlly(team, attacker);
                    if (target == null || target.MaxHp <= 0) return 0f;
                    float ratio = target.CurrentHp / (float)target.MaxHp;
                    if (ratio >= HealNeedRatio) return 0f;

                    float healed = target.MaxHp * Mathf.Clamp01(skill.effectValue) * support;
                    float deficit = target.MaxHp - target.CurrentHp;
                    return Mathf.Min(healed, deficit) * Weight(stance, SkillRole.Heal);
                }

                // 방어 버프는 팀 전체에 간다(시전자 전액·아군 절반).
                //
                // **지목되지 않았다고 0으로 버리지 않는다.** 옛 코드는 `!incomingArea && !targetedAtMe`면
                // 즉시 0이었는데, 보스 전체공격이 꺼지면서(RaidBossUsesAreaAttack = false)
                // incomingArea가 상시 false가 됐다 — 그래서 **5명 중 지목된 1명 말고는 방어 버프를
                // 영영 후보에 못 올렸다**. 레이드 버프는 만료가 없고(RaidRoundResolver의 의도된
                // divergence) 팀 전체에 걸리므로 지목 여부와 무관하게 남은 라운드 내내 값이 있다 —
                // 바로 아래 BuffAttack이 `×2f`로 다라운드 가치를 매기는 것과 같은 이유다.
                case SkillEffectType.DefenseBuff:
                {
                    float room = BuffRoom(team, attacker, defense: true);
                    if (room <= 0f) return 0f;

                    // 이번 라운드에 실제로 맞는 쪽이 더 급하다 — 기존 가치를 그대로 두고,
                    // 지목되지 않은 슬롯은 절반으로 본다(후보에는 오르되 뒤로 밀린다).
                    float urgency = (incomingArea || targetedAtMe) ? 1f : 0.5f;

                    return boss.Attack * Mathf.Max(0f, skill.effectValue) * room * urgency
                        * Weight(stance, SkillRole.Defense);
                }

                case SkillEffectType.BuffAttack:
                {
                    float room = BuffRoom(team, attacker, defense: false);
                    if (room <= 0f) return 0f;

                    // 이번 라운드가 아니라 남은 라운드 전체에 걸쳐 값이 붙는다 — 2라운드분으로 본다.
                    return attacker.Attack * Mathf.Max(0f, skill.effectValue) * 2f * room
                        * Weight(stance, SkillRole.Buff);
                }

                case SkillEffectType.DebuffAttack:
                {
                    if (boss.AttackStacks <= -GameConstants.Battle.MaxBuffStacks) return 0f;

                    return boss.Attack * Mathf.Max(0f, skill.effectValue) * 2f
                        * Weight(stance, SkillRole.Debuff);
                }

                case SkillEffectType.Stun:
                {
                    // 보스가 연속 기절 면역이면 지금 걸어도 저항당한다 — 턴만 소비하므로 후보에서 뺀다
                    // (버프를 스택 상한에서 거르는 것과 같은 이유. 이 가드가 없어 기절기를 든 팀원들이
                    //  면역 라운드에도 나란히 기절을 시도했다).
                    if (bossStunImmune) return 0f;

                    // 전체공격을 통째로 막으면 팀 전원분 피해를 지운다 — 예고를 읽었을 때 최고 가치.
                    float prevented = incomingArea
                        ? boss.Attack * Mathf.Max(1, alive) * 0.66f
                        : boss.Attack * 0.4f;
                    return prevented * accuracy * Weight(stance, SkillRole.Stun);
                }

                default:
                    return 0f;
            }
        }

        private enum SkillRole { Offense, Defense, Heal, Buff, Debuff, Stun }

        /// <summary>스탠스가 각 역할에 주는 가중치. 플레이어가 성향만 바꾸고 선택은 AI가 한다.</summary>
        private static float Weight(RaidTeamStance stance, SkillRole role)
        {
            switch (stance)
            {
                case RaidTeamStance.Guard:
                    switch (role)
                    {
                        case SkillRole.Offense: return 0.75f;
                        case SkillRole.Defense: return 1.5f;
                        case SkillRole.Heal: return 1.15f;
                        case SkillRole.Buff: return 0.8f;
                        case SkillRole.Debuff: return 1.25f;
                        case SkillRole.Stun: return 1.4f;
                        default: return 1f;
                    }

                case RaidTeamStance.Support:
                    switch (role)
                    {
                        case SkillRole.Offense: return 0.85f;
                        case SkillRole.Defense: return 1f;
                        case SkillRole.Heal: return 1.6f;
                        case SkillRole.Buff: return 1.1f;
                        case SkillRole.Debuff: return 1.1f;
                        case SkillRole.Stun: return 1f;
                        default: return 1f;
                    }

                default:   // Assault
                    switch (role)
                    {
                        case SkillRole.Offense: return 1.25f;
                        case SkillRole.Defense: return 0.7f;
                        case SkillRole.Heal: return 0.7f;
                        case SkillRole.Buff: return 1.2f;
                        case SkillRole.Debuff: return 0.9f;
                        case SkillRole.Stun: return 0.9f;
                        default: return 1f;
                    }
            }
        }

        /// <summary>
        /// 이 버프가 <b>실제로 붙을</b> 대상의 가중 합 — 시전자 1.0, 아군은
        /// <c>RaidRoundResolver.TeamBuffAllyShare</c>(전달 비율)만큼. 스택 상한에 닿은 대상은 안 센다.
        ///
        /// 0이면 전원이 상한이라 쓰면 <c>Capped</c>로 턴만 소비한다. 예전엔 <b>시전자 스택만</b> 봤는데,
        /// 버프가 팀 전체로 바뀐 뒤로는 시전자가 상한이어도 아군 4명에게 여유가 있을 수 있다 —
        /// 그때 0을 내면 멀쩡히 쓸모 있는 스킬을 버렸다(반대로 값도 시전자 몫만 세어 과소평가했다).
        /// </summary>
        private static float BuffRoom(InsectBattleStats[] team, InsectBattleStats caster, bool defense)
        {
            int cap = GameConstants.Battle.MaxBuffStacks;
            float room = 0f;
            if (caster != null && Stacks(caster, defense) < cap) room += 1f;
            if (team == null) return room;

            for (int i = 0; i < team.Length; i++)
            {
                InsectBattleStats ally = team[i];
                if (ally == null || ally == caster || ally.CurrentHp <= 0) continue;
                if (Stacks(ally, defense) < cap) room += RaidRoundResolver.TeamBuffAllyShare;
            }

            return room;
        }

        private static int Stacks(InsectBattleStats stats, bool defense)
        {
            return defense ? stats.DefenseStacks : stats.AttackStacks;
        }

        private static int AliveCount(InsectBattleStats[] team)
        {
            if (team == null) return 0;
            int count = 0;
            for (int i = 0; i < team.Length; i++)
                if (team[i] != null && team[i].CurrentHp > 0) count++;
            return count;
        }

        private static float Effectiveness(InsectElement element, InsectBattleStats boss)
        {
            return InsectTypeChart.GetEffectiveness(
                element,
                boss.Data != null ? boss.Data.primaryType : InsectElement.None,
                boss.Data != null ? boss.Data.secondaryType : InsectElement.None);
        }

        private static float SameTypeBonus(InsectElement element, InsectBattleStats attacker)
        {
            return attacker.Data != null
                ? InsectTypeChart.GetSameTypeBonus(
                    element, attacker.Data.primaryType, attacker.Data.secondaryType)
                : 1f;
        }
    }
}
