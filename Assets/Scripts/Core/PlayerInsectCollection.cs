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

    public class PlayerInsectCollection : MonoBehaviour, ICloudReloadable
    {
        [SerializeField] private InsectLevelCurve defaultCurve;
        [SerializeField] private InsectDatabase database;
        [SerializeField] private PlayerCandyInventory candyInventory;

        private PlayerInsectCollectionSave saveData;
        private readonly Dictionary<string, PlayerInsectData> lookup = new Dictionary<string, PlayerInsectData>();

        // 디스크 IO 디바운스: 가챠 10연 등 연속 변경 시 매번 File.WriteAllText 안 하고 0.5초 후 1회 저장.
        private bool saveDirty;
        private float saveDebounceTimer;
        private const float SaveDebounceSeconds = 0.5f;

        public event Action<PlayerInsectData> InsectUpdated;
        // 신규 포획/획득(AddInsectInternal) 전용 신호. InsectUpdated는 XP·치료·진화에서도 발화하므로
        // "포획" 트리거로 쓰면 오발화한다(StoryDirector CaptureInsect 비트). 이건 추가 경로에서만 발화.
        public event Action<PlayerInsectData> InsectCaptured;

        private void MarkDirty()
        {
            saveDirty = true;
            saveDebounceTimer = 0f;
        }

        // 즉시 저장(디바운스 우회) — 포획처럼 직후 앱 백그라운드 전환 시 CloudSave가 stale 파일을 읽어
        // 마지막 변경이 클라우드에서 누락되면 안 되는 경우에 사용.
        private void SaveNow()
        {
            saveDirty = false;
            saveDebounceTimer = 0f;
            if (saveData != null) Save(saveData);
        }

        private void Update()
        {
            if (!saveDirty) return;
            saveDebounceTimer += Time.unscaledDeltaTime;
            if (saveDebounceTimer >= SaveDebounceSeconds)
            {
                saveDebounceTimer = 0f;
                saveDirty = false;
                if (saveData != null) Save(saveData);
            }
        }

        private void OnDisable()
        {
            // 종료 직전 강제 flush — 디바운스 대기 중 변경 손실 방지
            if (saveDirty && saveData != null)
            {
                Save(saveData);
                saveDirty = false;
            }
        }

        private void OnApplicationQuit() { OnDisable(); }
        private void OnApplicationPause(bool pauseStatus) { if (pauseStatus) OnDisable(); }

        private void Awake()
        {
            LoadAndIndex();
        }

        // 디스크에서 보유 곤충을 읽어 lookup 재구성. Awake와 클라우드 로드 리로드가 공유.
        private void LoadAndIndex()
        {
            lookup.Clear();
            saveData = Load();
            bool needsSave = false;
            foreach (PlayerInsectData data in saveData.insects)
            {
                if (data == null)
                {
                    continue;
                }

                data.EnsureInstanceId();

                // 옛 세이브에서 instanceId 중복 발견 시 새 GUID 발급.
                // 옛은 dictionary에 1개만 보존되어 BattleTeam.GetByInstanceId가 나머지 인스턴스 못 찾는 회귀.
                if (lookup.ContainsKey(data.instanceId))
                {
                    data.instanceId = Guid.NewGuid().ToString("N");
                    needsSave = true;
                }

                // 크기 롤 초기화 — 구세이브(sizeRoll -1)는 instanceId 해시로 채운다.
                // 중복 GUID 재발급 뒤에 부른다(새 ID 기준으로 크기가 정해져야 결정적이다).
                if (data.sizeRoll < InsectSizeCalculator.MinRoll)
                {
                    data.EnsureSize();
                    needsSave = true;
                }

                InsectData insect = GetInsectData(data.insectId);
                // 지속 HP 초기화 — 구세이브(currentHp -1)는 풀피로 확정. insect null이면 다음 로드에 미룸.
                if (insect != null)
                {
                    int beforeHp = data.currentHp;
                    data.EnsureHp(data.GetTotalHp(insect.baseHp));
                    if (data.currentHp != beforeHp) needsSave = true;
                }
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

        // 클라우드 로드 후 player_insects.json을 다시 읽어 보유 목록 갱신.
        // 컬렉션 UI(IMGUI)는 매 프레임 읽어 자동 반영하므로 별도 이벤트 발화 불필요.
        public void ReloadFromDisk()
        {
            LoadAndIndex();
        }

        public PlayerInsectData AddCapturedInsect(string insectId, int level)
        {
            // shiny 미지정 — CreateWithIV의 자체 1% 롤을 그대로 사용(가챠/내부 폴백 등 필드 미경유 경로).
            return AddInsectInternal(insectId, level, null);
        }

        public PlayerInsectData AddCapturedInsect(string insectId, int level, bool isShiny)
        {
            // 필드/배틀/레이드에서 실제로 본 이로치 상태를 권위값으로 전달.
            return AddInsectInternal(insectId, level, isShiny);
        }

        private PlayerInsectData AddInsectInternal(string insectId, int level, bool? shinyOverride)
        {
            if (string.IsNullOrEmpty(insectId))
            {
                return null;
            }

            InsectData insect = GetInsectData(insectId);
            Data.InsectRarity rarity = insect != null ? insect.rarity : Data.InsectRarity.Common;
            PlayerInsectData data = PlayerInsectData.CreateWithIV(insectId, level, rarity);
            // 본 것과 보유가 일치하도록 권위값으로 덮어씀(true/false 모두). null이면 CreateWithIV 자체 롤 유지.
            // 옛 `if (isShiny) data.isShiny = true`는 true만 강제 → 일반 개체도 포획 시 1% 이로치化(이중 롤) 버그.
            if (shinyOverride.HasValue) data.isShiny = shinyOverride.Value;
            EnsureLevelSkills(data, insect);
            lookup[data.instanceId] = data;
            saveData.insects.Add(data);
            // 포획은 디바운스 없이 즉시 저장 — 직후 앱 백그라운드 전환 시 CloudSave가 stale player_insects를
            // 읽어 마지막 포획이 클라우드에서 누락되던 것 차단. (빈번한 XP 변경은 계속 디바운스)
            SaveNow();
            InsectUpdated?.Invoke(data);
            InsectCaptured?.Invoke(data); // 포획/획득 전용 — 스토리 CaptureInsect 등 포획 한정 트리거용
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

            // insect null 가드 — 형제 GainXp(InsectData, int) 오버로드와 대칭.
            // 외부에서 PlayerInsectData만 가진 채 insect=null로 호출하면 옛은 NRE.
            InsectLevelCurve curve = insect != null && insect.levelCurve != null ? insect.levelCurve : defaultCurve;
            data.currentXp += amount;
            int maxLevel = curve != null ? curve.maxLevel : 50;
            while (data.level < maxLevel && curve != null && data.currentXp >= curve.GetXpToNextLevel(data.level))
            {
                data.currentXp -= curve.GetXpToNextLevel(data.level);
                data.level++;
            }

            EnsureLevelSkills(data, insect);

            MarkDirty();
            InsectUpdated?.Invoke(data);
        }

        // ── 지속 HP·상태 치료 API (GainXp 패턴: mutate → MarkDirty → InsectUpdated) ──

        private int MaxHpOf(PlayerInsectData data)
        {
            InsectData insect = data != null ? GetInsectData(data.insectId) : null;
            int baseHp = insect != null ? insect.baseHp : 50;
            return data != null ? data.GetTotalHp(baseHp) : baseHp;
        }

        /// <summary>HP를 amount만큼 회복(상한 MaxHp). 기절(0)도 회복 가능(부활).</summary>
        public void HealInsect(PlayerInsectData data, int amount)
        {
            if (data == null || amount <= 0) return;
            int max = MaxHpOf(data);
            int cur = data.currentHp < 0 ? max : data.currentHp;
            data.currentHp = Mathf.Clamp(cur + amount, 0, max);
            MarkDirty();
            InsectUpdated?.Invoke(data);
        }

        /// <summary>HP 전액 + 모든 상태 해제(병원 젬 치료·종합치료제).</summary>
        public void FullHeal(PlayerInsectData data)
        {
            if (data == null) return;
            data.currentHp = MaxHpOf(data);
            data.isPoisoned = false;
            data.isParalyzed = false;
            MarkDirty();
            InsectUpdated?.Invoke(data);
        }

        public void CurePoison(PlayerInsectData data)
        {
            if (data == null || !data.isPoisoned) return;
            data.isPoisoned = false;
            MarkDirty();
            InsectUpdated?.Invoke(data);
        }

        public void CureParalysis(PlayerInsectData data)
        {
            if (data == null || !data.isParalyzed) return;
            data.isParalyzed = false;
            MarkDirty();
            InsectUpdated?.Invoke(data);
        }

        /// <summary>전투 종료 시 남은 HP·상태를 영구 기록(무료 전체치료 제거의 핵심).</summary>
        public void SetAfterBattle(PlayerInsectData data, int remainingHp, bool poisoned, bool paralyzed)
        {
            if (data == null) return;
            data.currentHp = Mathf.Clamp(remainingHp, 0, MaxHpOf(data));
            data.isPoisoned = poisoned;
            data.isParalyzed = paralyzed;
            MarkDirty();
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
            MarkDirty();
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

            // 손상된 세이브에서 null 항목이 섞일 경우 호출자(BattleTeamUI/CollectionUI/DexScreenUI 등) NRE 방지.
            var result = new List<PlayerInsectData>(saveData.insects.Count);
            foreach (PlayerInsectData d in saveData.insects)
            {
                if (d != null) result.Add(d);
            }
            return result;
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
                MarkDirty();
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

            if (data.equippedSkillIds.Count > PlayerInsectData.MaxEquipSlots)
            {
                data.equippedSkillIds.RemoveRange(
                    PlayerInsectData.MaxEquipSlots,
                    data.equippedSkillIds.Count - PlayerInsectData.MaxEquipSlots);
                changed = true;
            }

            // 옛 세이브(최대 12개)는 장착 중인 기술을 우선 보존해 새 4개 제한으로 이관한다.
            if (data.learnedSkillIds.Count > PlayerInsectData.MaxLearnedSkills)
            {
                List<string> limited = new List<string>(PlayerInsectData.MaxLearnedSkills);
                foreach (string equipped in data.equippedSkillIds)
                {
                    if (!string.IsNullOrEmpty(equipped)
                        && data.learnedSkillIds.Contains(equipped)
                        && !limited.Contains(equipped)
                        && limited.Count < PlayerInsectData.MaxLearnedSkills)
                        limited.Add(equipped);
                }
                foreach (string learned in data.learnedSkillIds)
                {
                    if (!string.IsNullOrEmpty(learned)
                        && !limited.Contains(learned)
                        && limited.Count < PlayerInsectData.MaxLearnedSkills)
                        limited.Add(learned);
                }
                data.learnedSkillIds = limited;
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

            try
            {
                string json = System.IO.File.ReadAllText(path);
                return JsonUtility.FromJson<PlayerInsectCollectionSave>(json) ?? new PlayerInsectCollectionSave();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerInsectCollection] 손상된 세이브 — 기본값으로 시작: {e.Message}");
                return new PlayerInsectCollectionSave();
            }
        }

        private void Save(PlayerInsectCollectionSave data)
        {
            string json = JsonUtility.ToJson(data, true);
            AtomicFileWriter.WriteAllText(GetPath(), json);
        }

        private string GetPath()
        {
            return SaveScope.FilePath(GameConstants.SaveFiles.PlayerInsects);
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
