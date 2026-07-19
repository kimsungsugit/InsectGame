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
                MaxHp = Mathf.Max(10, data.baseHp + Level * 3);
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
            int finalDamage = amount;
            if (attackerAtk > 0 && defenderDef > 0)
            {
                // 방어 보너스(의상/아이템) 반영 — 유효 방어 상승 → 피해 감소.
                float effDef = defenderDef * (1f + DefenseBonus);
                float ratio = attackerAtk / Mathf.Max(1f, effDef);
                finalDamage = Mathf.RoundToInt(amount * Mathf.Clamp(ratio, 0.5f, 2.5f));
            }
            CurrentHp = Mathf.Max(0, CurrentHp - Mathf.Max(1, finalDamage));
        }
    }
}
