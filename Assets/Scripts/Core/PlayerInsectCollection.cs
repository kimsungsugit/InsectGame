using System;
using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    [Serializable]
    public class PlayerInsectCollectionSave
    {
        public List<PlayerInsectData> insects = new List<PlayerInsectData>();
    }

    public class PlayerInsectCollection : MonoBehaviour
    {
        [SerializeField] private InsectLevelCurve defaultCurve;
        [SerializeField] private InsectDatabase database;
        [SerializeField] private PlayerCandyInventory candyInventory;

        private PlayerInsectCollectionSave saveData;
        private readonly Dictionary<string, PlayerInsectData> lookup = new Dictionary<string, PlayerInsectData>();

        public event Action<PlayerInsectData> InsectUpdated;

        private void Awake()
        {
            saveData = Load();
            bool needsSave = false;
            foreach (PlayerInsectData data in saveData.insects)
            {
                if (data == null)
                {
                    continue;
                }

                data.EnsureInstanceId();
                InsectData insect = GetInsectData(data.insectId);
                if (EnsureLevelSkills(data, insect))
                {
                    needsSave = true;
                }
                lookup[data.instanceId] = data;
            }

            if (needsSave)
            {
                Save(saveData);
            }
        }

        public PlayerInsectData AddCapturedInsect(string insectId, int level)
        {
            return AddCapturedInsect(insectId, level, false);
        }

        public PlayerInsectData AddCapturedInsect(string insectId, int level, bool isShiny)
        {
            if (string.IsNullOrEmpty(insectId))
            {
                return null;
            }

            InsectData insect = GetInsectData(insectId);
            Data.InsectRarity rarity = insect != null ? insect.rarity : Data.InsectRarity.Common;
            PlayerInsectData data = PlayerInsectData.CreateWithIV(insectId, level, rarity);
            if (isShiny) data.isShiny = true;
            EnsureLevelSkills(data, insect);
            lookup[data.instanceId] = data;
            saveData.insects.Add(data);
            Save(saveData);
            InsectUpdated?.Invoke(data);
            return data;
        }

        public PlayerInsectData GetOrCreate(string insectId, int level)
        {
            PlayerInsectData existing = GetFirstOwnedBySpecies(insectId);
            return existing ?? AddCapturedInsect(insectId, level);
        }

        public PlayerInsectData GetByInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return null;
            }

            lookup.TryGetValue(instanceId, out PlayerInsectData data);
            return data;
        }

        public PlayerInsectData GetFirstOwnedBySpecies(string insectId)
        {
            if (string.IsNullOrEmpty(insectId) || saveData == null || saveData.insects == null)
            {
                return null;
            }

            foreach (PlayerInsectData data in saveData.insects)
            {
                if (data != null && data.insectId == insectId)
                {
                    data.EnsureInstanceId();
                    return data;
                }
            }

            return null;
        }

        public PlayerInsectData GetLatestOwnedBySpecies(string insectId)
        {
            if (string.IsNullOrEmpty(insectId) || saveData == null || saveData.insects == null)
            {
                return null;
            }

            for (int i = saveData.insects.Count - 1; i >= 0; i--)
            {
                PlayerInsectData data = saveData.insects[i];
                if (data != null && data.insectId == insectId)
                {
                    data.EnsureInstanceId();
                    return data;
                }
            }

            return null;
        }

        public void GainXp(InsectData insect, int amount)
        {
            if (insect == null || amount <= 0)
            {
                return;
            }

            PlayerInsectData data = GetFirstOwnedBySpecies(insect.insectId) ?? AddCapturedInsect(insect.insectId, insect.minLevel);
            GainXp(data, insect, amount);
        }

        public void GainXp(PlayerInsectData data, InsectData insect, int amount)
        {
            if (data == null)
            {
                return;
            }

            InsectLevelCurve curve = insect.levelCurve != null ? insect.levelCurve : defaultCurve;
            data.currentXp += amount;
            int maxLevel = curve != null ? curve.maxLevel : 50;
            while (data.level < maxLevel && curve != null && data.currentXp >= curve.GetXpToNextLevel(data.level))
            {
                data.currentXp -= curve.GetXpToNextLevel(data.level);
                data.level++;
            }

            EnsureLevelSkills(data, insect);

            Save(saveData);
            InsectUpdated?.Invoke(data);
        }

        public int GetCandyCostForLevel(string insectId, int level)
        {
            InsectData insect = GetInsectData(insectId);
            InsectLevelCurve curve = insect != null ? insect.levelCurve : null;
            if (curve == null) curve = defaultCurve;
            if (curve != null)
                return curve.GetCandyCost(level);
            return Mathf.Max(1, GameConstants.Leveling.FallbackBaseCandyCost + (level - 1) * GameConstants.Leveling.FallbackCandyCostGrowth);
        }

        public int GetMaxLevel(string insectId)
        {
            InsectData insect = GetInsectData(insectId);
            InsectLevelCurve curve = insect != null ? insect.levelCurve : null;
            if (curve == null) curve = defaultCurve;
            return curve != null ? curve.maxLevel : GameConstants.Leveling.FallbackMaxLevel;
        }

        public int GetXpToNextLevel(string insectId, int level)
        {
            InsectData insect = GetInsectData(insectId);
            InsectLevelCurve curve = insect != null ? insect.levelCurve : null;
            if (curve == null) curve = defaultCurve;
            if (curve != null)
                return curve.GetXpToNextLevel(level);
            return Mathf.Max(1, 20 + (level - 1) * 8);
        }

        public bool TryLevelUpWithCandy(string insectId)
        {
            return TryLevelUpWithCandy(GetFirstOwnedBySpecies(insectId));
        }

        public bool TryLevelUpWithCandyByInstance(string instanceId)
        {
            return TryLevelUpWithCandy(GetByInstanceId(instanceId));
        }

        public bool TryLevelUpWithCandy(PlayerInsectData data)
        {
            if (data == null || string.IsNullOrEmpty(data.insectId))
            {
                return false;
            }

            InsectData insect = GetInsectData(data.insectId);
            if (insect == null || candyInventory == null)
            {
                return false;
            }

            int maxLv = GetMaxLevel(data.insectId);
            if (data.level >= maxLv)
            {
                return false;
            }

            int cost = GetCandyCostForLevel(data.insectId, data.level);
            if (!candyInventory.SpendCandy(cost))
            {
                return false;
            }

            data.level++;
            data.currentXp = 0;
            EnsureLevelSkills(data, insect);
            Save(saveData);
            InsectUpdated?.Invoke(data);
            return true;
        }

        public bool TryGetAnyOwned(out PlayerInsectData insect)
        {
            insect = null;
            if (saveData == null || saveData.insects == null || saveData.insects.Count == 0)
            {
                return false;
            }

            insect = saveData.insects[0];
            return insect != null;
        }

        public List<PlayerInsectData> GetAllOwned()
        {
            if (saveData == null || saveData.insects == null)
            {
                return new List<PlayerInsectData>();
            }

            return new List<PlayerInsectData>(saveData.insects);
        }

        public string ResolveLegacyOrInstanceId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            PlayerInsectData byInstance = GetByInstanceId(id);
            if (byInstance != null)
            {
                return byInstance.instanceId;
            }

            PlayerInsectData bySpecies = GetFirstOwnedBySpecies(id);
            return bySpecies != null ? bySpecies.instanceId : null;
        }

        public InsectData GetInsectData(string insectId)
        {
            if (database == null || string.IsNullOrEmpty(insectId))
            {
                return null;
            }

            return database.insects.Find(item => item != null && item.insectId == insectId);
        }

        public InsectSkill[] GetEquippedSkills(PlayerInsectData data)
        {
            if (data == null)
            {
                return Array.Empty<InsectSkill>();
            }

            InsectData insect = GetInsectData(data.insectId);
            if (EnsureLevelSkills(data, insect))
            {
                Save(saveData);
            }

            InsectSkill[] result = new InsectSkill[PlayerInsectData.MaxEquipSlots];
            for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
            {
                string skillId = data.GetEquippedSkill(i);
                result[i] = ResolveSkill(insect, skillId);
            }

            return result;
        }

        public InsectSkill ResolveSkill(InsectData insect, string skillId)
        {
            if (insect == null || string.IsNullOrEmpty(skillId))
            {
                return null;
            }

            if (insect.learnset != null)
            {
                foreach (InsectLearnableSkill learnable in insect.learnset)
                {
                    if (learnable != null && learnable.skill != null && learnable.skillId == skillId)
                    {
                        return learnable.skill;
                    }
                }
            }

            if (insect.skills != null)
            {
                foreach (InsectSkill skill in insect.skills)
                {
                    if (skill != null && skill.skillId == skillId)
                    {
                        return skill;
                    }
                }
            }

            return null;
        }

        private bool EnsureLevelSkills(PlayerInsectData data, InsectData insect)
        {
            if (data == null)
            {
                return false;
            }

            bool changed = false;
            if (data.learnedSkillIds == null)
            {
                data.learnedSkillIds = new List<string>();
                changed = true;
            }

            if (data.equippedSkillIds == null)
            {
                data.equippedSkillIds = new List<string>();
                changed = true;
            }

            while (data.equippedSkillIds.Count < PlayerInsectData.MaxEquipSlots)
            {
                data.equippedSkillIds.Add(string.Empty);
                changed = true;
            }

            if (insect != null && insect.learnset != null)
            {
                foreach (InsectLearnableSkill learnable in insect.learnset)
                {
                    if (learnable == null || string.IsNullOrEmpty(learnable.skillId) || learnable.learnLevel > data.level)
                    {
                        continue;
                    }

                    if (!data.learnedSkillIds.Contains(learnable.skillId) && data.learnedSkillIds.Count < PlayerInsectData.MaxLearnedSkills)
                    {
                        data.learnedSkillIds.Add(learnable.skillId);
                        changed = true;
                    }
                }
            }
            else if (insect != null && insect.skills != null)
            {
                foreach (InsectSkill skill in insect.skills)
                {
                    if (skill == null || string.IsNullOrEmpty(skill.skillId))
                    {
                        continue;
                    }

                    if (!data.learnedSkillIds.Contains(skill.skillId) && data.learnedSkillIds.Count < PlayerInsectData.MaxLearnedSkills)
                    {
                        data.learnedSkillIds.Add(skill.skillId);
                        changed = true;
                    }
                }
            }

            for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
            {
                string equippedId = data.GetEquippedSkill(i);
                if (!string.IsNullOrEmpty(equippedId) && !data.learnedSkillIds.Contains(equippedId))
                {
                    data.EquipSkill(null, i);
                    changed = true;
                }
            }

            foreach (string skillId in data.learnedSkillIds)
            {
                if (string.IsNullOrEmpty(skillId))
                {
                    continue;
                }

                bool alreadyEquipped = false;
                for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
                {
                    if (data.GetEquippedSkill(i) == skillId)
                    {
                        alreadyEquipped = true;
                        break;
                    }
                }

                if (alreadyEquipped)
                {
                    continue;
                }

                for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
                {
                    if (string.IsNullOrEmpty(data.GetEquippedSkill(i)))
                    {
                        data.EquipSkill(skillId, i);
                        changed = true;
                        break;
                    }
                }
            }

            return changed;
        }

        private PlayerInsectCollectionSave Load()
        {
            string path = GetPath();
            if (!System.IO.File.Exists(path))
            {
                return new PlayerInsectCollectionSave();
            }

            string json = System.IO.File.ReadAllText(path);
            return JsonUtility.FromJson<PlayerInsectCollectionSave>(json) ?? new PlayerInsectCollectionSave();
        }

        private void Save(PlayerInsectCollectionSave data)
        {
            string json = JsonUtility.ToJson(data, true);
            System.IO.File.WriteAllText(GetPath(), json);
        }

        private string GetPath()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, GameConstants.SaveFiles.PlayerInsects);
        }

        public void ForceSave()
        {
            if (saveData != null) Save(saveData);
        }

        public void AutoWire(InsectDatabase db, PlayerCandyInventory candy)
        {
            if (database == null)
            {
                database = db;
            }

            if (candyInventory == null)
            {
                candyInventory = candy;
            }

            if (database != null && saveData != null && saveData.insects != null)
            {
                bool needsSave = false;
                foreach (PlayerInsectData data in saveData.insects)
                {
                    if (data == null)
                    {
                        continue;
                    }

                    if (EnsureLevelSkills(data, GetInsectData(data.insectId)))
                    {
                        needsSave = true;
                    }
                }

                if (needsSave)
                {
                    Save(saveData);
                }
            }
        }
    }
}
