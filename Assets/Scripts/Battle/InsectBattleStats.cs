using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Battle
{
    public class InsectBattleStats
    {
        public InsectData Data { get; }
        public PlayerInsectData PlayerData { get; }
        public int Level { get; }
        public int MaxHp { get; protected set; }
        public int CurrentHp { get; private set; }
        public int Attack { get; protected set; }
        public int Defense { get; protected set; }
        public float AttackBonus { get; set; }
        public float DefenseBonus { get; set; }   // 유효 방어 배율 가산(의상/아이템) — ApplyDamage에서 소비

        // 레이드 전용 스택 카운터. 1v1은 효과 목록(ActiveEffect)의 개수를 세어 상한을 잡지만,
        // 레이드엔 그 목록이 없어 보너스 값에 직접 누적하므로 몇 번 쌓였는지를 따로 센다.
        // 부호 있는 값: +면 버프 누적, -면 디버프 누적. 범위는 ±GameConstants.Battle.MaxBuffStacks.
        public int AttackStacks { get; private set; }
        public int DefenseStacks { get; private set; }

        /// <summary>
        /// 상한 안이면 보너스에 delta를 더하고 스택을 1 옮긴 뒤 true. 상한이면 아무것도 하지 않고 false.
        /// delta 부호가 곧 방향이다 — 반대 방향은 상한과 무관하게 항상 통과해 되돌릴 수 있다.
        /// </summary>
        public bool TryStackAttackBonus(float delta)
        {
            if (!CanStack(AttackStacks, delta)) return false;
            AttackBonus += delta;
            AttackStacks += delta > 0f ? 1 : -1;
            return true;
        }

        /// <summary>공격 버전과 같은 규칙의 방어 보너스 누적.</summary>
        public bool TryStackDefenseBonus(float delta)
        {
            if (!CanStack(DefenseStacks, delta)) return false;
            DefenseBonus += delta;
            DefenseStacks += delta > 0f ? 1 : -1;
            return true;
        }

        private static bool CanStack(int current, float delta)
        {
            if (Mathf.Approximately(delta, 0f)) return false;
            int max = GameConstants.Battle.MaxBuffStacks;
            return delta > 0f ? current < max : current > -max;
        }

        public InsectBattleStats(InsectData data, int level, PlayerInsectData pid = null)
        {
            Data = data;
            PlayerData = pid;
            Level = Mathf.Max(1, level);

            if (pid != null && data != null)
            {
                MaxHp = Mathf.Max(10, pid.GetTotalHp(data.baseHp));
                Attack = Mathf.Max(1, pid.GetTotalAtk(data.baseAtk));
                Defense = Mathf.Max(1, pid.GetTotalDef(data.baseDef));
            }
            else if (data != null)
            {
                MaxHp = Mathf.Max(10, data.baseHp + Level * GameConstants.Battle.HpPerLevel);
                Attack = Mathf.Max(1, data.baseAtk + Level * 2);
                Defense = Mathf.Max(1, data.baseDef + Level);
            }
            else
            {
                MaxHp = 10 + Level * 5;
                Attack = 10 + Level * 2;
                Defense = 5 + Level;
            }

            // 지속 HP 시드 — 보유 곤충(pid)이면 저장된 현재 HP로 시작(전투 간 유지). pid 없으면(야생/적) 풀피.
            CurrentHp = pid != null ? Mathf.Clamp(pid.GetEffectiveHp(MaxHp), 0, MaxHp) : MaxHp;
            AttackBonus = 0f;
            DefenseBonus = 0f;
        }

        public void ResetHp()
        {
            CurrentHp = MaxHp;
        }

        /// <summary>HP를 amount만큼 회복(MaxHp 상한). 0 이하 곤충은 회복 불가(기절 유지).</summary>
        public void Heal(int amount)
        {
            if (amount <= 0 || CurrentHp <= 0) return;
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
        }

        public void ApplyDamage(int amount, int attackerAtk = 0, int defenderDef = 0)
        {
            CurrentHp = Mathf.Max(0, CurrentHp - Mathf.Max(1, ResolveDamage(amount, attackerAtk, defenderDef)));
        }

        /// <summary>
        /// 공격력 대 방어력 비율까지 반영한 <b>실제로 깎일 피해</b> — <see cref="ApplyDamage"/>가 쓰는 계산 그대로다.
        /// 샌드박스 전투가 이 값에 배율·상한을 걸어야 해서 떼어 냈다(두 곳이 따로 계산하면 어긋난다).
        /// </summary>
        public int ResolveDamage(int amount, int attackerAtk = 0, int defenderDef = 0)
        {
            int finalDamage = amount;
            if (attackerAtk > 0 && defenderDef > 0)
            {
                // 방어 보너스(의상/아이템) 반영 — 유효 방어 상승 → 피해 감소.
                float effDef = defenderDef * (1f + DefenseBonus);
                float ratio = attackerAtk / Mathf.Max(1f, effDef);
                finalDamage = Mathf.RoundToInt(amount * Mathf.Clamp(ratio,
                    GameConstants.Battle.MinAtkDefRatio, GameConstants.Battle.MaxAtkDefRatio));
            }
            return finalDamage;
        }

        // ── 낮·밤·날씨 보정(BattleEnvironment) ──
        // 처음 걸 때의 공격·방어를 붙잡아 두고 언제나 그 값에 곱한다 — 두 번 불려도 누적되지 않고, 1을 걸면 원래 값으로 돌아간다.
        // 생성자가 아니라 처음 걸 때 붙잡는 이유: 파생 스탯(레이드 보스)은 base 생성자 뒤에 공격·방어를 다시 정한다.
        private bool environmentBaseCaptured;
        private int unscaledAttack;
        private int unscaledDefense;

        /// <summary>지금 걸려 있는 낮·밤·날씨 배수(1 = 보정 없음).</summary>
        public float EnvironmentMultiplier { get; private set; } = 1f;

        /// <summary>
        /// 낮·밤·날씨 보정 — 원래 <see cref="Attack"/>·<see cref="Defense"/>에 <paramref name="multiplier"/>를 곱해 반올림한다(최소 1).
        /// <b>HP는 건드리지 않는다</b>(HP바가 전투 시작에 튀지 않게). 의상·아이템·버프가 매 턴 다시 계산하는
        /// <see cref="AttackBonus"/>/<see cref="DefenseBonus"/>와는 따로 논다 — 그쪽에 섞으면 다음 재계산이 지운다.
        /// 0·음수·NaN·무한대는 1로 본다.
        /// </summary>
        public void ApplyEnvironment(float multiplier)
        {
            if (!environmentBaseCaptured)
            {
                unscaledAttack = Attack;
                unscaledDefense = Defense;
                environmentBaseCaptured = true;
            }

            float m = multiplier > 0f && !float.IsInfinity(multiplier) ? multiplier : 1f;
            EnvironmentMultiplier = m;
            Attack = Mathf.Max(1, Mathf.RoundToInt(unscaledAttack * m));
            Defense = Mathf.Max(1, Mathf.RoundToInt(unscaledDefense * m));
        }

        /// <summary>HP가 <paramref name="floor"/>보다 낮으면 그 값까지 올린다(샌드박스 전투의 하한 — 독 같은 지속 피해를 막는다).</summary>
        public void RaiseHpTo(int floor)
        {
            if (CurrentHp < floor) CurrentHp = Mathf.Min(MaxHp, Mathf.Max(1, floor));
        }
    }
}
