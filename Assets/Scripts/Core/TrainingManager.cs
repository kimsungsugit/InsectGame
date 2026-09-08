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
        // 기술 디스크 경로 — 보유 디스크에서 스킬 풀을 만들고 습득 시 1개 소비한다.
        [SerializeField] private PlayerItemInventory itemInventory;
        [SerializeField] private ItemDatabase itemDatabase;

        /// <summary>
        /// 기술 디스크 훈련 방식의 ID. 이 방식은 <b>스킬 풀이 고정 배열이 아니라
        /// 플레이어가 지금 들고 있는 디스크 아이템</b>에서 나온다 — 그래서 다른 방식들과
        /// 달리 <c>TrainingMethod.skillPool</c>이 비어 있다.
        /// </summary>
        public const string DiscMethodId = "disc";

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

            InsectData ownedData = collection != null ? collection.GetInsectData(insect.insectId) : null;

            if (method.methodId == DiscMethodId)
            {
                foreach (string id in OwnedDiscSkillIds())
                {
                    if (skillLookup.TryGetValue(id, out InsectSkill discSkill)
                        && MeetsLevel(discSkill, insect)
                        && IsCompatibleWithInsect(discSkill, ownedData)
                        && seen.Add(discSkill.skillId))
                        result.Add(discSkill);
                }
                return result.ToArray();
            }

            if (method.skillPool == null) return result.ToArray();
            foreach (string id in method.skillPool)
            {
                if (skillLookup.TryGetValue(id, out InsectSkill skill)
                    && MeetsLevel(skill, insect)
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

            InsectData ownedData = collection != null ? collection.GetInsectData(insect.insectId) : null;

            if (method.methodId == DiscMethodId)
            {
                foreach (string id in OwnedDiscSkillIds())
                {
                    if (skillLookup.TryGetValue(id, out InsectSkill discSkill)
                        && MeetsLevel(discSkill, insect)
                        && IsCompatibleWithInsect(discSkill, ownedData)
                        && countSeen.Add(discSkill.skillId))
                        count++;
                }
                return count;
            }

            if (method.skillPool == null) return 0;
            foreach (string id in method.skillPool)
            {
                if (skillLookup.TryGetValue(id, out InsectSkill skill)
                    && MeetsLevel(skill, insect)
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

        /// <summary>
        /// 이번 훈련 <b>1회분</b>의 캔디 비용. 누적 훈련은 회차마다 이 값을 받으므로
        /// 총비용은 여기에 <see cref="GetRequiredSessions"/>를 곱한 만큼이다.
        ///
        /// <b>기술 디스크는 방식의 고정 비용만 받는다.</b> 값은 이미 디스크를 사거나 얻는 데서
        /// 치렀는데, 여기서 <c>skill.trainingCost</c>까지 물리면 320젬짜리 디스크를 쓰면서
        /// 캔디 54가 또 나간다 — 그러면 "사서 바로 배운다"가 성립하지 않는다.
        /// </summary>
        public int GetTrainingCost(TrainingMethod method, PlayerInsectData insect, string skillId)
        {
            if (method == null) return 0;
            if (method.methodId == DiscMethodId) return Mathf.Max(0, method.candyCost);

            InsectSkill skill = GetSkill(skillId);
            int skillCost = skill != null ? skill.trainingCost : 0;
            return Mathf.Max(1, Mathf.Max(method.candyCost, skillCost));
        }

        /// <summary>
        /// 이 기술을 익히는 데 필요한 훈련 횟수. 위력이 셀수록 오래 걸린다.
        ///
        /// <b>기술 디스크는 예외로 1회다</b> — 디스크는 사서 얻는 물건이라 "즉시 습득"이
        /// 그 값어치이고, 누적 훈련과 대비돼야 둘 다 존재 이유가 생긴다.
        /// </summary>
        public int GetRequiredSessions(TrainingMethod method, InsectSkill skill)
        {
            if (skill == null) return 1;
            if (method != null && method.methodId == DiscMethodId) return 1;
            return Mathf.Clamp(1 + skill.power / 12, 1, 5);
        }

        public int GetRequiredSessions(TrainingMethod method, string skillId)
        {
            return GetRequiredSessions(method, GetSkill(skillId));
        }

        /// <summary>
        /// 훈련 1회. <b>바로 배우지 않는다</b> — 진척도를 1 올리고, 필요 횟수에 도달한
        /// 회차에만 실제로 습득시킨다. 반환값은 "이번 회차가 성립했는가"이지 "습득했는가"가 아니다
        /// (습득 여부는 <see cref="LastTrainingLearned"/>로 알린다 — UI가 문구를 가른다).
        ///
        /// 캔디는 <b>매 회차</b> 소비한다. 그게 누적 훈련의 비용이다.
        /// </summary>
        public bool TrainSkill(TrainingMethod method, PlayerInsectData insect, string skillId, string replaceSkillId = null)
        {
            LastTrainingLearned = false;
            LastTrainingProgress = 0;
            LastTrainingRequired = 0;

            if (!CanTrain(method, insect, skillId)) return false;
            if (insect.HasLearnedSkill(skillId)) return false;
            if (!skillLookup.TryGetValue(skillId, out InsectSkill skill)) return false;

            int required = GetRequiredSessions(method, skill);
            int trainingCost = GetTrainingCost(method, insect, skillId);

            // 마지막 회차라면 교체 대상이 확정돼 있어야 한다 — 캔디를 쓰기 **전에** 확인한다.
            bool finalSession = insect.GetTrainingProgress(skillId) + 1 >= required;
            bool needsReplace = finalSession && insect.IsSkillsFull();
            if (needsReplace && string.IsNullOrEmpty(replaceSkillId)) return false;

            // 액션 성공 후 SpendCandy — 옛은 SpendCandy 먼저라 ReplaceSkill 실패(replaceSkillId가
            // learnedSkillIds에 없는 경우 등) 시 candy 손실 회귀.
            // 비용 0(디스크 방식이 그렇게 설정될 수 있다)이면 SpendCandy가 amount<=0으로 거부해
            // 버튼은 켜져 있는데 눌러도 아무 일이 없다 — 0이면 차감 자체를 건너뛴다.
            if (trainingCost > 0 && !candyInventory.SpendCandy(trainingCost)) return false;

            int progress = insect.AddTrainingProgress(skillId);
            LastTrainingProgress = progress;
            LastTrainingRequired = required;

            if (progress >= required)
            {
                if (needsReplace)
                {
                    if (!insect.ReplaceSkill(replaceSkillId, skillId))
                    {
                        // 교체가 실패하면 이번 회차를 무르고 캔디를 돌려준다 — 진척도만 날리면
                        // 플레이어는 캔디를 쓴 채 아무것도 못 얻는다.
                        insect.RemoveTrainingProgress(skillId);
                        candyInventory.AddCandy(trainingCost);
                        return false;
                    }
                }
                else if (!insect.LearnSkill(skillId))
                {
                    insect.RemoveTrainingProgress(skillId);
                    candyInventory.AddCandy(trainingCost);
                    return false;
                }
                else if (insect.EquippedCount() < PlayerInsectData.MaxEquipSlots)
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

                insect.ClearTrainingProgress(skillId);   // 다 배웠다 — 기록을 남겨 둘 이유가 없다
                LastTrainingLearned = true;
                ConsumeDiscIfNeeded(method, skillId);
            }

            if (collection != null)
            {
                // 스킬을 갈아 끼웠다는 걸 컬렉션 구독자에게 알린다. `TrainingCompleted`는 구독자가
                // 0이라 아무에게도 안 갔고, 그래서 훈련 직후 캐시를 쓰는 화면이 옛 스킬셋으로 남았다.
                collection.NotifyInsectChanged(insect);
                collection.ForceSave();
            }

            TrainingCompleted?.Invoke();
            // q_training 진행도 — **매 회차** 올린다. 습득 회차에만 올리면 누적 훈련 도중
            // 퀘스트가 멈춘 것처럼 보인다.
            TutorialQuestManager.Instance?.NotifyTraining();
            return true;
        }

        /// <summary>직전 <see cref="TrainSkill"/>이 실제 습득까지 갔는가. UI 문구가 쓴다.</summary>
        public bool LastTrainingLearned { get; private set; }

        /// <summary>직전 훈련 뒤의 누적 횟수와 필요 횟수(UI "3/5" 표시용).</summary>
        public int LastTrainingProgress { get; private set; }

        public int LastTrainingRequired { get; private set; }

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
            if (!MeetsLevel(skill, insect)) return false;

            bool inPool = false;
            if (method.methodId == DiscMethodId)
            {
                foreach (string id in OwnedDiscSkillIds())
                {
                    if (id == skillId) { inPool = true; break; }
                }
            }
            else if (method.skillPool != null)
            {
                foreach (string id in method.skillPool)
                {
                    if (id == skillId) { inPool = true; break; }
                }
            }
            return inPool && IsCompatibleWithInsect(skill, insectData);
        }

        /// <summary>곤충 레벨이 이 기술의 요구 레벨에 닿았는가(종족 learnset 경로에는 안 쓴다).</summary>
        private static bool MeetsLevel(InsectSkill skill, PlayerInsectData insect)
        {
            if (skill == null || insect == null) return false;
            return insect.level >= skill.requiredLevel;
        }

        // ── 기술 디스크 ──────────────────────────────────────────────────────────

        private readonly List<string> discSkillBuffer = new List<string>();

        /// <summary>
        /// 지금 보유 중인 기술 디스크가 가르치는 기술 ID들. 매 프레임 도는 자리
        /// (<c>DrawMethodSelect</c>의 개수 세기)가 있어 재사용 버퍼를 쓴다.
        /// </summary>
        private List<string> OwnedDiscSkillIds()
        {
            discSkillBuffer.Clear();
            if (itemInventory == null || itemDatabase == null || itemDatabase.items == null)
                return discSkillBuffer;

            foreach (ItemData item in itemDatabase.items)
            {
                if (item == null || string.IsNullOrEmpty(item.teachSkillId)) continue;
                if (itemInventory.GetCount(item.itemId) <= 0) continue;
                if (!discSkillBuffer.Contains(item.teachSkillId))
                    discSkillBuffer.Add(item.teachSkillId);
            }
            return discSkillBuffer;
        }

        /// <summary>디스크로 배웠으면 그 디스크 1개를 소비한다. 다른 방식이면 아무 일도 안 한다.</summary>
        private void ConsumeDiscIfNeeded(TrainingMethod method, string skillId)
        {
            if (method == null || method.methodId != DiscMethodId) return;
            if (itemInventory == null || itemDatabase == null || itemDatabase.items == null) return;

            foreach (ItemData item in itemDatabase.items)
            {
                if (item == null || item.teachSkillId != skillId) continue;
                if (itemInventory.GetCount(item.itemId) <= 0) continue;
                itemInventory.UseItem(item.itemId, 1);
                return;
            }
        }

        /// <summary>
        /// 이 기술의 디스크 보유 장수. 디스크 방식은 습득 회차에 디스크 1장을 <b>소비</b>하므로
        /// UI가 버튼과 피드백에 이 값을 적는다 — 안 적으면 Legendary 디스크가 아무 표시 없이 사라진다.
        /// </summary>
        public int GetDiscCount(string skillId)
        {
            ItemData disc = FindDiscFor(skillId);
            if (disc == null || itemInventory == null) return 0;
            return itemInventory.GetCount(disc.itemId);
        }

        /// <summary>이 기술을 가르치는 디스크 아이템(없으면 null). <see cref="GetDiscCount"/>가 쓴다.</summary>
        public ItemData FindDiscFor(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || itemDatabase == null || itemDatabase.items == null) return null;
            // 훈련 화면이 스킬 카드마다 매 프레임 부른다 — 선형 탐색이면 프레임당 스킬 수 × 아이템 수.
            if (discByskill == null || discByskillSource != itemDatabase.items.Count)
            {
                discByskill = new Dictionary<string, ItemData>();
                foreach (ItemData item in itemDatabase.items)
                    if (item != null && !string.IsNullOrEmpty(item.teachSkillId) && !discByskill.ContainsKey(item.teachSkillId))
                        discByskill[item.teachSkillId] = item;
                discByskillSource = itemDatabase.items.Count;
            }
            discByskill.TryGetValue(skillId, out ItemData disc);
            return disc;
        }

        private Dictionary<string, ItemData> discByskill;
        private int discByskillSource = -1;   // 캐시를 만든 시점의 items.Count — DB가 바뀌면 다시 만든다

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

        /// <summary>기술 디스크 경로 배선 — 없으면 "기술 디스크" 방식의 목록이 항상 비어 보인다.</summary>
        public void AutoWire(PlayerItemInventory items, ItemDatabase itemDb)
        {
            if (itemInventory == null) itemInventory = items;
            if (itemDatabase == null) itemDatabase = itemDb;
        }
    }
}
