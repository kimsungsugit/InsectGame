using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    public static class PlayerInsectCombatPower
    {
        public static int Calculate(InsectData data, PlayerInsectData pid)
        {
            if (data == null)
            {
                return 0;
            }

            int level = pid != null ? Mathf.Max(1, pid.level) : Mathf.Max(1, data.minLevel);
            int hp = pid != null ? pid.GetTotalHp(data.baseHp) : data.baseHp + level * 3;
            int atk = pid != null ? pid.GetTotalAtk(data.baseAtk) : data.baseAtk + level * 2;
            int def = pid != null ? pid.GetTotalDef(data.baseDef) : data.baseDef + level;

            float statScore = hp * 0.25f + atk * 0.45f + def * 0.30f;
            float levelMultiplier = 0.85f + level * 0.08f;
            float rarityBonus = 1f + (int)data.rarity * 0.12f;
            float basePowerBonus = data.basePower * 0.6f;

            return Mathf.Max(10, Mathf.RoundToInt((statScore + basePowerBonus) * levelMultiplier * rarityBonus));
        }

        public static int CalculateBasePreview(InsectData data, int level = 1)
        {
            if (data == null)
            {
                return 0;
            }

            int clampedLevel = Mathf.Max(1, level);
            int hp = data.baseHp + clampedLevel * 3;
            int atk = data.baseAtk + clampedLevel * 2;
            int def = data.baseDef + clampedLevel;

            float statScore = hp * 0.25f + atk * 0.45f + def * 0.30f;
            float levelMultiplier = 0.85f + clampedLevel * 0.08f;
            float rarityBonus = 1f + (int)data.rarity * 0.12f;
            float basePowerBonus = data.basePower * 0.6f;

            return Mathf.Max(10, Mathf.RoundToInt((statScore + basePowerBonus) * levelMultiplier * rarityBonus));
        }
    }
}
