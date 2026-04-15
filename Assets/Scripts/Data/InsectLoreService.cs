using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Data
{
    [System.Serializable]
    public class InsectLoreList
    {
        public List<InsectLoreEntry> entries = new List<InsectLoreEntry>();
    }

    public static class InsectLoreService
    {
        private static Dictionary<string, InsectLoreEntry> cache;
        private const string ResourceName = "InsectLore";

        public static void ApplyToDatabase(InsectDatabase database)
        {
            if (database == null)
            {
                return;
            }

            EnsureCache();
            if (cache == null)
            {
                return;
            }

            foreach (InsectData data in database.insects)
            {
                if (data == null || string.IsNullOrEmpty(data.insectId))
                {
                    continue;
                }

                if (cache.TryGetValue(data.insectId, out InsectLoreEntry entry))
                {
                    // displayName은 항상 Lore(한글)로 덮어쓰기
                    if (!string.IsNullOrWhiteSpace(entry.displayName))
                    {
                        data.displayName = entry.displayName;
                    }

                    if (!string.IsNullOrWhiteSpace(entry.description))
                    {
                        data.description = entry.description;
                    }

                    if (!string.IsNullOrWhiteSpace(entry.habitatHint))
                    {
                        data.habitatHint = entry.habitatHint;
                    }
                }
            }
        }

        public static bool TryGetEntry(string insectId, out InsectLoreEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(insectId))
            {
                return false;
            }

            EnsureCache();
            if (cache == null)
            {
                return false;
            }

            return cache.TryGetValue(insectId, out entry);
        }

        private static void EnsureCache()
        {
            if (cache != null)
            {
                return;
            }

            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null)
            {
                return;
            }

            InsectLoreList list = JsonUtility.FromJson<InsectLoreList>(asset.text);
            cache = new Dictionary<string, InsectLoreEntry>();
            if (list == null || list.entries == null)
            {
                return;
            }

            foreach (InsectLoreEntry entry in list.entries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.insectId))
                {
                    cache[entry.insectId] = entry;
                }
            }
        }
    }
}
