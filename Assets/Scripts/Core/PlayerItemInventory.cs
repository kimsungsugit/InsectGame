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

            // 손상 세이브 sanitize — null record + 빈 itemId + 중복 itemId 처리.
            // 옛은 (a) null record에서 NRE, (b) 중복 itemId 시 lookup은 마지막만 보존, save.items엔 둘 다 잔존 →
            //       AddItem 변경이 stale record로 동기화 깨짐.
            bool dirty = false;
            for (int i = save.items.Count - 1; i >= 0; i--)
            {
                PlayerItemRecord record = save.items[i];
                if (record == null || string.IsNullOrEmpty(record.itemId))
                {
                    save.items.RemoveAt(i);
                    dirty = true;
                    continue;
                }

                if (lookup.TryGetValue(record.itemId, out PlayerItemRecord existing))
                {
                    existing.count += record.count;
                    save.items.RemoveAt(i);
                    dirty = true;
                }
                else
                {
                    lookup[record.itemId] = record;
                }
            }

            if (dirty) Save(save);
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

            if (record.count < amount)
            {
                return false;
            }

            record.count -= amount;
            // 0 이하 잔량은 보유 목록에서 제거 (음수 잔존/강제 1 잔존 방지)
            if (record.count <= 0)
            {
                lookup.Remove(itemId);
                save.items.Remove(record);
            }
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
                return record.count > 0 ? record.count : 0;
            return 0;
        }

        public PlayerItemSave GetSnapshot()
        {
            // 내부 참조 노출 시 호출자(DexScreenUI 등)가 save.items 변경하면 lookup과 동기화 깨짐.
            // 얕은 복사 — items 리스트만 새로 (record 자체는 read-only 접근 의도).
            if (save == null) return new PlayerItemSave();
            return new PlayerItemSave { items = new List<PlayerItemRecord>(save.items) };
        }

        private PlayerItemSave Load()
        {
            string path = GetPath();
            if (!File.Exists(path))
            {
                return new PlayerItemSave();
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<PlayerItemSave>(json) ?? new PlayerItemSave();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlayerItemInventory] 손상된 세이브 — 기본값으로 시작: {e.Message}");
                return new PlayerItemSave();
            }
        }

        private void Save(PlayerItemSave data)
        {
            // PlayerProgressSaveService/PlayerCandyInventory/PlayerCurrencyWallet과 일관성 — null 진입 시 빈 파일 차단.
            if (data == null) return;
            string json = JsonUtility.ToJson(data, true);
            AtomicFileWriter.WriteAllText(GetPath(), json);
        }

        private string GetPath()
        {
            return Path.Combine(Application.persistentDataPath, GameConstants.SaveFiles.PlayerItems);
        }
    }
}
