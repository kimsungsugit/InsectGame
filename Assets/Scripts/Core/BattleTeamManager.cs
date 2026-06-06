using System;
using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    [Serializable]
    public class BattleTeamSave
    {
        public List<string> slotIds = new List<string>();
    }

    public class BattleTeamManager : MonoBehaviour
    {
        public const int MaxSlots = GameConstants.Battle.MaxTeamSlots;

        [SerializeField] private PlayerInsectCollection collection;

        private BattleTeamSave saveData;

        public event Action TeamChanged;

        public int FilledSlots
        {
            get
            {
                if (saveData == null) return 0;
                int count = 0;
                foreach (var id in saveData.slotIds)
                    if (!string.IsNullOrEmpty(id)) count++;
                return count;
            }
        }

        private void Awake()
        {
            saveData = Load();
            while (saveData.slotIds.Count < MaxSlots)
                saveData.slotIds.Add(string.Empty);

            MigrateLegacySlots();
        }

        public string GetSlot(int index)
        {
            if (saveData == null || index < 0 || index >= MaxSlots) return null;
            string id = saveData.slotIds[index];
            return string.IsNullOrEmpty(id) ? null : id;
        }

        public bool SetSlot(int index, string instanceId)
        {
            if (index < 0 || index >= MaxSlots) return false;

            string normalizedId = collection != null ? collection.ResolveLegacyOrInstanceId(instanceId) : instanceId;

            if (!string.IsNullOrEmpty(normalizedId))
            {
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (i != index && saveData.slotIds[i] == normalizedId)
                        return false;
                }
            }

            string newValue = normalizedId ?? string.Empty;
            // no-op 가드 — 같은 값 재설정 시 TeamChanged 발화 안 함.
            // 옛은 TutorialQuestManager가 TeamChanged 구독해 q_team 진행도 중복 가산 위험.
            if (saveData.slotIds[index] == newValue) return true;

            saveData.slotIds[index] = newValue;
            Save(saveData);
            TeamChanged?.Invoke();
            return true;
        }

        public bool RemoveSlot(int index)
        {
            return SetSlot(index, null);
        }

        public bool IsInTeam(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || saveData == null) return false;
            string normalizedId = collection != null ? collection.ResolveLegacyOrInstanceId(instanceId) : instanceId;
            return saveData.slotIds.Contains(normalizedId);
        }

        public List<string> GetAllSlots()
        {
            return new List<string>(saveData.slotIds);
        }

        public bool HasAnyInsect()
        {
            return FilledSlots > 0;
        }

        public void AutoWire(PlayerInsectCollection col)
        {
            if (collection == null) collection = col;
            MigrateLegacySlots();
        }

        private void MigrateLegacySlots()
        {
            if (saveData == null || collection == null)
            {
                return;
            }

            bool changed = false;
            for (int i = 0; i < saveData.slotIds.Count; i++)
            {
                string resolved = collection.ResolveLegacyOrInstanceId(saveData.slotIds[i]);
                if (resolved != saveData.slotIds[i])
                {
                    saveData.slotIds[i] = resolved ?? string.Empty;
                    changed = true;
                }
            }

            if (changed)
            {
                Save(saveData);
                // 마이그레이션은 시스템 내부 정리 — 사용자 액션 아님. TeamChanged 발화 시 TutorialQuestManager가
                // q_team 진행도 자동 가산하는 회귀 차단. UI는 다음 사용자 SetSlot 또는 첫 OnGUI 호출에서 갱신.
            }
        }

        private BattleTeamSave Load()
        {
            string path = GetPath();
            if (!System.IO.File.Exists(path))
                return new BattleTeamSave();
            try
            {
                string json = System.IO.File.ReadAllText(path);
                return JsonUtility.FromJson<BattleTeamSave>(json) ?? new BattleTeamSave();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BattleTeamManager] 손상된 세이브 — 기본값으로 시작: {e.Message}");
                return new BattleTeamSave();
            }
        }

        private void Save(BattleTeamSave data)
        {
            string json = JsonUtility.ToJson(data, true);
            AtomicFileWriter.WriteAllText(GetPath(), json);
        }

        private string GetPath()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, GameConstants.SaveFiles.BattleTeam);
        }
    }
}
