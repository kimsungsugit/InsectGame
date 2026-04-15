using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace InsectGame.Core
{
    [Serializable]
    public class PlayerItemRecord
    {
        public string itemId;
        public int count;
    }

    [Serializable]
    public class PlayerItemSave
    {
        public List<PlayerItemRecord> items = new List<PlayerItemRecord>();
    }

    public class PlayerItemInventory : MonoBehaviour
    {
        private PlayerItemSave save;
        private readonly Dictionary<string, PlayerItemRecord> lookup = new Dictionary<string, PlayerItemRecord>();

        public event Action<PlayerItemSave> ItemsChanged;

        private void Awake()
        {
            save = Load();
            foreach (PlayerItemRecord record in save.items)
            {
                if (!string.IsNullOrEmpty(record.itemId))
                {
                    lookup[record.itemId] = record;
                }
            }
        }

        public void AddItem(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0)
            {
                return;
            }

            if (!lookup.TryGetValue(itemId, out PlayerItemRecord record))
            {
                record = new PlayerItemRecord { itemId = itemId, count = 0 };
                lookup[itemId] = record;
                save.items.Add(record);
            }

            record.count += amount;
            Save(save);
            ItemsChanged?.Invoke(save);
        }

        public bool UseItem(string itemId, int amount = 1)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0)
            {
                return false;
            }

            if (!lookup.TryGetValue(itemId, out PlayerItemRecord record))
            {
                return false;
            }

            int effective = Mathf.Max(record.count, 1);
            if (effective < amount)
            {
                return false;
            }

            record.count = effective - amount;
            if (record.count < 1) record.count = 1;
            Save(save);
            ItemsChanged?.Invoke(save);
            return true;
        }

        public int GetCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            if (lookup.TryGetValue(itemId, out PlayerItemRecord record))
                return Mathf.Max(record.count, 1);
            return 0;
        }

        public PlayerItemSave GetSnapshot()
        {
            return save;
        }

        private PlayerItemSave Load()
        {
            string path = GetPath();
            if (!File.Exists(path))
            {
                return new PlayerItemSave();
            }

            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<PlayerItemSave>(json) ?? new PlayerItemSave();
        }

        private void Save(PlayerItemSave data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetPath(), json);
        }

        private string GetPath()
        {
            return Path.Combine(Application.persistentDataPath, GameConstants.SaveFiles.PlayerItems);
        }
    }
}
