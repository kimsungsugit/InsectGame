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
            if (method == null || insect == null)
                return new InsectSkill[0];

            List<InsectSkill> result = new List<InsectSkill>();
            HashSet<string> seen = new HashSet<string>();

            if (method.methodId == "species")
            {
                InsectData insectData = collection != null ? collection.GetInsectData(insect.insectId) : null;
                if (insectData == null || insectData.learnset == null) return result.ToArray();

                foreach (InsectLearnableSkill learnable in insectData.learnset)
                {
                    if (learnable == null || learnable.learnLevel > insect.level) continue;
                    InsectSkill skill = learnable.skill ?? GetSkill(learnable.skillId);
                    if (skill != null && !string.IsNullOrEmpty(skill.skillId) && seen.Add(skill.skillId))
                        result.Add(skill);
                }
                return result.ToArray();
            }

            if (method.skillPool == null) return result.ToArray();
            InsectData ownedData = collection != null ? collection.GetInsectData(insect.insectId) : null;
            foreach (string id in method.skillPool)
            {
                if (skillLookup.TryGetValue(id, out InsectSkill skill)
                    && IsCompatibleWithInsect(skill, ownedData)
                    && seen.Add(skill.skillId))
                    result.Add(skill);
            }
            return result.ToArray();
        }

        // 무할당 스킬 개수 카운트 — DrawMethodSelect가 매 프레임 6개 방식을 호출하므로 GetAvailableSkills의
        // List/ToArray 할당을 피한다. 재사용 HashSet(countSeen)만 Clear해 GC 압박 0. 로직은 GetAvailableSkills와 동일.
        private readonly HashSet<string> countSeen = new HashSet<string>();

        public int GetAvailableSkillCount(TrainingMethod method, PlayerInsectData insect)
        {
            if (method == null || insect == null) return 0;
            countSeen.Clear();
            int count = 0;

            if (method.methodId == "species")
            {
                InsectData insectData = collection != null ? collection.GetInsectData(insect.insectId) : null;
                if (insectData == null || insectData.learnset == null) return 0;
                foreach (InsectLearnableSkill learnable in insectData.learnset)
                {
                    if (learnable == null || learnable.learnLevel > insect.level) continue;
                    InsectSkill skill = learnable.skill ?? GetSkill(learnable.skillId);
                    if (skill != null && !string.IsNullOrEmpty(skill.skillId) && countSeen.Add(skill.skillId))
                        count++;
                }
                return count;
            }

            if (method.skillPool == null) return 0;
            InsectData ownedData = collection != null ? collection.GetInsectData(insect.insectId) : null;
            foreach (string id in method.skillPool)
            {
                if (skillLookup.TryGetValue(id, out InsectSkill skill)
                    && IsCompatibleWithInsect(skill, ownedData)
                    && countSeen.Add(skill.skillId))
                    count++;
            }
            return count;
        }

        public bool CanTrain(TrainingMethod method, PlayerInsectData insect)
        {
            if (method == null || insect == null || candyInventory == null) return false;
            if (insect.level < method.requiredLevel) return false;
            if (candyInventory.Candies < method.candyCost) return false;
            return true;
        }

        public bool CanTrain(TrainingMethod method, PlayerInsectData insect, string skillId)
        {
            if (method == null || insect == null || candyInventory == null) return false;
            if (insect.level < method.requiredLevel) return false;
            if (!IsSkillAllowed(method, insect, skillId)) return false;
            return candyInventory.Candies >= GetTrainingCost(method, insect, skillId);
        }

        public int GetTrainingCost(TrainingMethod method, PlayerInsectData insect, string skillId)
        {
            if (method == null) return 0;
            InsectSkill skill = GetSkill(skillId);
            int skillCost = skill != null ? skill.trainingCost : 0;
            return Mathf.Max(1, Mathf.Max(method.candyCost, skillCost));
        }

        public bool TrainSkill(TrainingMethod method, PlayerInsectData insect, string skillId, string replaceSkillId = null)
        {
            if (!CanTrain(method, insect, skillId)) return false;
            if (insect.HasLearnedSkill(skillId)) return false;
            if (!skillLookup.ContainsKey(skillId)) return false;

            int trainingCost = GetTrainingCost(method, insect, skillId);

            // 액션 성공 후 SpendCandy — 옛은 SpendCandy 먼저라 ReplaceSkill 실패(replaceSkillId가
            // learnedSkillIds에 없는 경우 등) 시 candy 손실 회귀.
            if (insect.IsSkillsFull())
            {
                if (string.IsNullOrEmpty(replaceSkillId)) return false;
                if (!insect.ReplaceSkill(replaceSkillId, skillId)) return false;
                if (!candyInventory.SpendCandy(trainingCost)) return false;
            }
            else
            {
                if (!insect.LearnSkill(skillId)) return false;
                if (!candyInventory.SpendCandy(trainingCost)) return false;

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
            {
                // 스킬을 갈아 끼웠다는 걸 컬렉션 구독자에게 알린다. `TrainingCompleted`는 구독자가
                // 0이라 아무에게도 안 갔고, 그래서 훈련 직후 캐시를 쓰는 화면이 옛 스킬셋으로 남았다.
                collection.NotifyInsectChanged(insect);
                collection.ForceSave();
            }

            TrainingCompleted?.Invoke();
            // q_training 진행도 — TrainSkill 성공 분기 끝
            TutorialQuestManager.Instance?.NotifyTraining();
            return true;
        }

        private bool IsSkillAllowed(TrainingMethod method, PlayerInsectData insect, string skillId)
        {
            if (method == null || insect == null || string.IsNullOrEmpty(skillId)) return false;
            if (!skillLookup.TryGetValue(skillId, out InsectSkill skill)) return false;

            InsectData insectData = collection != null ? collection.GetInsectData(insect.insectId) : null;
            if (method.methodId == "species")
            {
                if (insectData == null || insectData.learnset == null) return false;
                foreach (InsectLearnableSkill learnable in insectData.learnset)
                {
                    if (learnable != null
                        && learnable.skillId == skillId
                        && learnable.learnLevel <= insect.level)
                        return true;
                }
                return false;
            }

            if (skill.isSignatureSkill) return false;

            bool inPool = false;
            if (method.skillPool != null)
            {
                foreach (string id in method.skillPool)
                {
                    if (id == skillId) { inPool = true; break; }
                }
            }
            return inPool && IsCompatibleWithInsect(skill, insectData);
        }

        private static bool IsCompatibleWithInsect(InsectSkill skill, InsectData insect)
        {
            if (skill == null) return false;
            if (skill.element == InsectElement.None) return true;
            if (insect == null) return false;
            return skill.element == insect.primaryType || skill.element == insect.secondaryType;
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
