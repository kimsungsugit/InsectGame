using System;
using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    public class TrainingManager : MonoBehaviour
    {
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private PlayerCandyInventory candyInventory;

        private TrainingMethod[] methods;
        private Dictionary<string, InsectSkill> skillLookup = new Dictionary<string, InsectSkill>();

        public TrainingMethod[] Methods => methods;

        public event Action TrainingCompleted;

        public void Initialize(TrainingMethod[] trainingMethods, InsectSkill[] allSkills)
        {
            methods = trainingMethods;
            skillLookup.Clear();
            if (allSkills != null)
            {
                foreach (var s in allSkills)
                    if (s != null && !string.IsNullOrEmpty(s.skillId))
                        skillLookup[s.skillId] = s;
            }
        }

        public InsectSkill GetSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return null;
            skillLookup.TryGetValue(skillId, out InsectSkill skill);
            return skill;
        }

        public InsectSkill[] GetAvailableSkills(TrainingMethod method, PlayerInsectData insect)
        {
            if (method == null || method.skillPool == null || insect == null)
                return new InsectSkill[0];

            List<InsectSkill> result = new List<InsectSkill>();
            foreach (string id in method.skillPool)
            {
                if (skillLookup.TryGetValue(id, out InsectSkill skill))
                    result.Add(skill);
            }
            return result.ToArray();
        }

        public bool CanTrain(TrainingMethod method, PlayerInsectData insect)
        {
            if (method == null || insect == null || candyInventory == null) return false;
            if (insect.level < method.requiredLevel) return false;
            if (candyInventory.Candies < method.candyCost) return false;
            return true;
        }

        public bool TrainSkill(TrainingMethod method, PlayerInsectData insect, string skillId, string replaceSkillId = null)
        {
            if (!CanTrain(method, insect)) return false;
            if (insect.HasLearnedSkill(skillId)) return false;
            if (!skillLookup.ContainsKey(skillId)) return false;

            bool inPool = false;
            if (method.skillPool != null)
            {
                foreach (string id in method.skillPool)
                    if (id == skillId) { inPool = true; break; }
            }
            if (!inPool) return false;

            // 액션 성공 후 SpendCandy — 옛은 SpendCandy 먼저라 ReplaceSkill 실패(replaceSkillId가
            // learnedSkillIds에 없는 경우 등) 시 candy 손실 회귀.
            if (insect.IsSkillsFull())
            {
                if (string.IsNullOrEmpty(replaceSkillId)) return false;
                if (!insect.ReplaceSkill(replaceSkillId, skillId)) return false;
                candyInventory.SpendCandy(method.candyCost);
            }
            else
            {
                if (!insect.LearnSkill(skillId)) return false;
                candyInventory.SpendCandy(method.candyCost);

                if (insect.EquippedCount() < PlayerInsectData.MaxEquipSlots)
                {
                    for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
                    {
                        if (insect.GetEquippedSkill(i) == null)
                        {
                            insect.EquipSkill(skillId, i);
                            break;
                        }
                    }
                }
            }

            if (collection != null)
                collection.ForceSave();

            TrainingCompleted?.Invoke();
            // q_training 진행도 — TrainSkill 성공 분기 끝
            TutorialQuestManager.Instance?.NotifyTraining();
            return true;
        }

        public InsectSkill[] GetEquippedSkills(PlayerInsectData insect)
        {
            if (insect == null) return new InsectSkill[0];
            List<InsectSkill> result = new List<InsectSkill>();
            for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
            {
                string id = insect.GetEquippedSkill(i);
                if (id != null && skillLookup.TryGetValue(id, out InsectSkill skill))
                    result.Add(skill);
                else
                    result.Add(null);
            }
            return result.ToArray();
        }

        public void AutoWire(PlayerInsectCollection col, PlayerCandyInventory candy)
        {
            if (collection == null) collection = col;
            if (candyInventory == null) candyInventory = candy;
        }
    }
}
