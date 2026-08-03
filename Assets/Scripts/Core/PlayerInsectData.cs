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

        // 지속 HP·상태(전투 간 유지). currentHp = -1은 '미초기화'(구세이브 마이그레이션) → 풀피 취급.
        // EnsureHp가 로드 시 실제 MaxHp로 채운다. isPoisoned/isParalyzed 기본 false(무상태) = 마이그레이션 무해.
        public int currentHp = -1;
        public bool isPoisoned;
        public bool isParalyzed;

        // 개체 크기 롤 0~100. **-1은 '미초기화'**(구세이브) — currentHp와 같은 센티넬 방식이다.
        // 0으로 두면 기존 곤충이 전부 최소 크기가 되므로, 로드 시 EnsureSize가 instanceId
        // 해시로 채운다(결정적이라 볼 때마다 값이 바뀌지 않는다).
        public int sizeRoll = -1;

        // 포획 시각(Unix 초). 0 = 미상(구세이브) — 주간 대결 집계에서 '이번 주 아님'으로 걸러진다.
        // 주간 기록을 따로 저장하지 않고 이 필드로 파생하므로, player_insects.json 블롭이
        // 클라우드로 올라가면서 기록도 함께 따라온다.
        public long capturedUnix;

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

        /// <summary>전투 시작 시 시드할 현재 HP. currentHp 미초기화(-1)면 풀피(maxHp).</summary>
        public int GetEffectiveHp(int maxHp)
        {
            if (currentHp < 0) return maxHp;
            return UnityEngine.Mathf.Clamp(currentHp, 0, maxHp);
        }

        /// <summary>기절(치료 전까지 출전 불가) — 초기화된 currentHp가 0.</summary>
        public bool IsFainted => currentHp == 0;

        /// <summary>로드 직후 currentHp 확정(미초기화면 풀피). EnsureInstanceId와 함께 호출.</summary>
        public void EnsureHp(int maxHp)
        {
            if (currentHp < 0) currentHp = maxHp;
            else currentHp = UnityEngine.Mathf.Clamp(currentHp, 0, maxHp);
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
                // 크기는 등급과 무관하게 균등 — IV처럼 등급이 높을수록 어렵게 하면
                // 저레어 종으로 도는 주간 크기 대결의 의미가 사라진다.
                sizeRoll = UnityEngine.Random.Range(
                    InsectSizeCalculator.MinRoll, InsectSizeCalculator.MaxRoll + 1),
                capturedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            return data;
        }

        /// <summary>
        /// 구세이브 마이그레이션 — sizeRoll이 -1이면 instanceId 해시로 채운다.
        /// EnsureHp와 같은 자리(로드 루프)에서 호출한다. instanceId가 먼저 보장돼야 하므로
        /// EnsureInstanceId 뒤에 부른다.
        /// </summary>
        public void EnsureSize()
        {
            if (sizeRoll < InsectSizeCalculator.MinRoll)
                sizeRoll = InsectSizeCalculator.RollFromInstanceId(instanceId);
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
