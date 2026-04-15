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

            CurrentHp = MaxHp;
            AttackBonus = 0f;
        }

        public void ResetHp()
        {
            CurrentHp = MaxHp;
        }

        public void ApplyDamage(int amount, int attackerAtk = 0, int defenderDef = 0)
        {
            int finalDamage = amount;
            if (attackerAtk > 0 && defenderDef > 0)
            {
                float ratio = attackerAtk / (float)Mathf.Max(1, defenderDef);
                finalDamage = Mathf.RoundToInt(amount * Mathf.Clamp(ratio, 0.5f, 2.5f));
            }
            CurrentHp = Mathf.Max(0, CurrentHp - Mathf.Max(1, finalDamage));
        }
    }
}
