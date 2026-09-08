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

        // 훈련 진척 — "skillId:count" 형식의 문자열 목록.
        // **왜 Dictionary가 아닌가**: JsonUtility는 Dictionary를 직렬화하지 않는다(조용히 빈 값이 된다).
        // 구세이브엔 이 필드가 없고 JsonUtility는 없는 필드를 건드리지 않으므로 빈 리스트가 그대로
        // 남는다 — 마이그레이션이 무해하다(currentHp의 -1 센티넬과 달리 기본값이 곧 정답이다).
        public List<string> trainingProgress = new List<string>();

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
            return baseHp + ivHp * 2 + level * GameConstants.Battle.HpPerLevel;
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

        // ── 훈련 진척 ────────────────────────────────────────────────────────────
        //
        // 한 번의 훈련으로 기술을 얻지 않는다. 같은 기술을 필요 횟수만큼 훈련해야 습득된다
        // (필요 횟수는 TrainingManager.GetRequiredSessions가 위력에서 정한다).

        /// <summary>
        /// "skillId:count" 항목이 이 기술의 것인지 <b>문자열을 만들지 않고</b> 판정하고 값을 읽는다.
        ///
        /// 무할당인 이유: 훈련 화면(<c>TrainingUI.DrawSkillLearn</c>)이 <b>매 OnGUI 패스마다
        /// 스킬 하나당 두 번</b> 진척을 읽는다(버튼 라벨 + 진행 바). 여기서 <c>skillId + ":"</c>를
        /// 만들면 그 횟수만큼 문자열이 쌓인다 — <c>GetAvailableSkillCount</c>가 따로 있는 이유와 같다.
        /// </summary>
        private static bool TryReadEntry(string entry, string skillId, out int value)
        {
            value = 0;
            if (entry == null || skillId == null) return false;

            int n = skillId.Length;
            if (entry.Length <= n + 1) return false;      // 최소 "id:1"
            if (entry[n] != ':') return false;
            if (string.CompareOrdinal(entry, 0, skillId, 0, n) != 0) return false;

            // int.Parse(Substring)은 또 할당한다 — 자릿수를 직접 센다(진척은 한 자리~두 자리다).
            int v = 0;
            for (int i = n + 1; i < entry.Length; i++)
            {
                char c = entry[i];
                if (c < '0' || c > '9') return false;
                v = v * 10 + (c - '0');
            }
            value = v;
            return true;
        }

        /// <summary>이 기술을 지금까지 몇 번 훈련했는가. <b>무할당</b>(매 프레임 경로).</summary>
        public int GetTrainingProgress(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || trainingProgress == null) return 0;
            for (int i = 0; i < trainingProgress.Count; i++)
            {
                if (TryReadEntry(trainingProgress[i], skillId, out int v)) return v;
            }
            return 0;
        }

        /// <summary>훈련 1회분을 적립하고 누적 횟수를 돌려준다.</summary>
        public int AddTrainingProgress(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return 0;
            if (trainingProgress == null) trainingProgress = new List<string>();

            for (int i = 0; i < trainingProgress.Count; i++)
            {
                if (!TryReadEntry(trainingProgress[i], skillId, out int cur)) continue;
                int next = cur + 1;
                trainingProgress[i] = skillId + ":" + next;
                return next;
            }

            trainingProgress.Add(skillId + ":1");
            return 1;
        }

        /// <summary>
        /// 훈련 1회분을 <b>무른다</b>(교체 실패 등으로 회차를 되돌릴 때). 0 아래로는 안 내려간다.
        ///
        /// <see cref="AddTrainingProgress"/>의 역이 필요한 이유: 그쪽은 기존 항목이 있으면
        /// 제자리에서 갱신하고 없을 때만 뒤에 붙이므로, <b>"마지막 항목이 방금 올린 그것"이라는
        /// 가정이 성립하지 않는다.</b> 그 가정으로 되돌리면 엉뚱한 기술의 진척을 깎는다.
        /// </summary>
        public void RemoveTrainingProgress(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || trainingProgress == null) return;
            for (int i = 0; i < trainingProgress.Count; i++)
            {
                if (!TryReadEntry(trainingProgress[i], skillId, out int cur)) continue;

                int next = cur - 1;
                if (next <= 0) trainingProgress.RemoveAt(i);
                else trainingProgress[i] = skillId + ":" + next;
                return;
            }
        }

        /// <summary>습득이 끝난 기술의 진척 기록을 지운다(세이브가 무한히 자라지 않게).</summary>
        /// <summary>
        /// 죽은 진척 항목을 걷어낸다 — 이미 배운 기술의 진척, 그리고 <paramref name="isKnownSkill"/>이
        /// 주어졌을 때 DB에서 사라진 기술의 진척. 항목은 습득 회차에만 지워지므로 4/5에서 그만두거나
        /// 기술이 빠지면 세이브에 영구히 남았다. 로드 시 한 번 부른다. 바뀐 게 있으면 true.
        /// </summary>
        public bool PruneTrainingProgress(Func<string, bool> isKnownSkill)
        {
            if (trainingProgress == null || trainingProgress.Count == 0) return false;
            bool changed = false;
            for (int i = trainingProgress.Count - 1; i >= 0; i--)
            {
                string entry = trainingProgress[i];
                int sep = entry != null ? entry.LastIndexOf(':') : -1;
                string skillId = sep > 0 ? entry.Substring(0, sep) : null;
                bool dead = string.IsNullOrEmpty(skillId)
                    || HasLearnedSkill(skillId)
                    || (isKnownSkill != null && !isKnownSkill(skillId));
                if (!dead) continue;
                trainingProgress.RemoveAt(i);
                changed = true;
            }
            return changed;
        }

        public void ClearTrainingProgress(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || trainingProgress == null) return;
            for (int i = trainingProgress.Count - 1; i >= 0; i--)
            {
                if (TryReadEntry(trainingProgress[i], skillId, out _))
                    trainingProgress.RemoveAt(i);
            }
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
