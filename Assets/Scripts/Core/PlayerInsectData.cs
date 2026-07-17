using System;
using System.Collections.Generic;

namespace InsectGame.Core
{
    public enum IVGrade { D, C, B, A, S }

    [Serializable]
    public class PlayerInsectData
    {
        public string instanceId;
        public string insectId;
        public int level = 1;
        public int currentXp = 0;
        public List<string> learnedSkillIds = new List<string>();
        public List<string> equippedSkillIds = new List<string>();

        public int ivHp;
        public int ivAtk;
        public int ivDef;
        public bool isShiny;

        public const int MaxEquipSlots = GameConstants.Player.MaxEquipSlots;
        public const int MaxLearnedSkills = GameConstants.Player.MaxLearnedSkills;
        public const int MaxIV = GameConstants.Player.MaxIV;

        public float IVPercent => (ivHp + ivAtk + ivDef) / (MaxIV * 3f);

        public IVGrade Grade
        {
            get
            {
                float pct = IVPercent;
                if (pct >= 0.9f) return IVGrade.S;
                if (pct >= 0.7f) return IVGrade.A;
                if (pct >= 0.5f) return IVGrade.B;
                if (pct >= 0.3f) return IVGrade.C;
                return IVGrade.D;
            }
        }

        public int GetTotalHp(int baseHp)
        {
            return baseHp + ivHp * 2 + level * 3;
        }

        public int GetTotalAtk(int baseAtk)
        {
            return baseAtk + ivAtk + level * 2;
        }

        public int GetTotalDef(int baseDef)
        {
            return baseDef + ivDef + level;
        }

        public static PlayerInsectData CreateWithIV(string insectId, int level, Data.InsectRarity rarity = Data.InsectRarity.Common)
        {
            // 등급이 높을수록 좋은 IV 나올 확률 감소
            float ivPower;
            switch (rarity)
            {
                case Data.InsectRarity.Common:    ivPower = 2.0f; break;  // 기본
                case Data.InsectRarity.Uncommon:  ivPower = 2.5f; break;  // 약간 어려움
                case Data.InsectRarity.Rare:      ivPower = 3.0f; break;  // 어려움
                case Data.InsectRarity.Epic:      ivPower = 4.0f; break;  // 매우 어려움
                case Data.InsectRarity.Legendary: ivPower = 5.0f; break;  // 극한
                default:                          ivPower = 2.0f; break;
            }

            var data = new PlayerInsectData
            {
                instanceId = Guid.NewGuid().ToString("N"),
                insectId = insectId,
                level = Math.Max(1, level),
                currentXp = 0,
                ivHp = RollIV(ivPower),
                ivAtk = RollIV(ivPower),
                ivDef = RollIV(ivPower),
                isShiny = UnityEngine.Random.value < 0.01f,
            };
            return data;
        }

        public void EnsureInstanceId()
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = Guid.NewGuid().ToString("N");
            }
        }

        private static int RollIV(float power = 2.0f)
        {
            float roll = UnityEngine.Random.value;
            roll = UnityEngine.Mathf.Pow(roll, power); // 높은 power → 낮은 IV에 편중
            // Random.value는 1.0 포함 가능 → roll*16=16으로 IV=16(0~15 불변식 위반). MaxIV로 클램프.
            return UnityEngine.Mathf.Min(MaxIV, (int)(roll * (MaxIV + 1)));
        }

        public bool HasLearnedSkill(string skillId)
        {
            return learnedSkillIds != null && learnedSkillIds.Contains(skillId);
        }

        public bool IsSkillsFull()
        {
            return learnedSkillIds != null && learnedSkillIds.Count >= MaxLearnedSkills;
        }

        public bool LearnSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return false;
            if (learnedSkillIds == null) learnedSkillIds = new List<string>();
            if (learnedSkillIds.Contains(skillId)) return false;
            if (learnedSkillIds.Count >= MaxLearnedSkills) return false;
            learnedSkillIds.Add(skillId);
            return true;
        }

        public bool ReplaceSkill(string oldSkillId, string newSkillId)
        {
            if (string.IsNullOrEmpty(oldSkillId) || string.IsNullOrEmpty(newSkillId)) return false;
            if (learnedSkillIds == null) return false;
            int idx = learnedSkillIds.IndexOf(oldSkillId);
            if (idx < 0) return false;
            if (learnedSkillIds.Contains(newSkillId)) return false;

            learnedSkillIds[idx] = newSkillId;

            if (equippedSkillIds != null)
            {
                for (int i = 0; i < equippedSkillIds.Count; i++)
                {
                    if (equippedSkillIds[i] == oldSkillId)
                        equippedSkillIds[i] = newSkillId;
                }
            }
            return true;
        }

        public bool EquipSkill(string skillId, int slot)
        {
            if (slot < 0 || slot >= MaxEquipSlots) return false;
            if (!string.IsNullOrEmpty(skillId) && !HasLearnedSkill(skillId)) return false;
            if (equippedSkillIds == null) equippedSkillIds = new List<string>();
            while (equippedSkillIds.Count < MaxEquipSlots) equippedSkillIds.Add("");
            if (!string.IsNullOrEmpty(skillId))
            {
                for (int i = 0; i < equippedSkillIds.Count; i++)
                    if (i != slot && equippedSkillIds[i] == skillId) return false;
            }
            equippedSkillIds[slot] = skillId ?? "";
            return true;
        }

        public string GetEquippedSkill(int slot)
        {
            if (equippedSkillIds == null || slot < 0 || slot >= equippedSkillIds.Count) return null;
            string id = equippedSkillIds[slot];
            return string.IsNullOrEmpty(id) ? null : id;
        }

        public int EquippedCount()
        {
            if (equippedSkillIds == null) return 0;
            int c = 0;
            foreach (var s in equippedSkillIds)
                if (!string.IsNullOrEmpty(s)) c++;
            return c;
        }
    }
}
