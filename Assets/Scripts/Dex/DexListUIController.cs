using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Dex
{
    public class DexListUIController : MonoBehaviour
    {
        public enum SortMode
        {
            ByName,
            ByRarity,
            ByCaptured
        }

        public enum FilterMode
        {
            All,
            DiscoveredOnly,
            UndiscoveredOnly
        }

        [SerializeField] private InsectDatabase database;
        [SerializeField] private DexController dexController;
        [SerializeField] private DexDetailUIController detailUI;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private DexListItemUI itemPrefab;
        [SerializeField] private SortMode sortMode = SortMode.ByName;
        [SerializeField] private FilterMode filterMode = FilterMode.All;

        private void Awake()
        {
            LoadPrefs();
        }

        private void Start()
        {
            BuildList();
        }

        private void OnEnable()
        {
            if (dexController != null)
            {
                dexController.DexUpdated += HandleDexUpdated;
            }
        }

        private void OnDisable()
        {
            if (dexController != null)
            {
                dexController.DexUpdated -= HandleDexUpdated;
            }
        }

        public void BuildList()
        {
            if (database == null || contentRoot == null || itemPrefab == null || dexController == null)
            {
                return;
            }

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(contentRoot.GetChild(i).gameObject);
            }

            List<InsectData> filtered = new List<InsectData>();
            foreach (InsectData data in database.insects)
            {
                if (data == null)
                {
                    continue;
                }

                bool discovered = dexController.IsDiscovered(data.insectId);
                if (!PassesFilter(discovered))
                {
                    continue;
                }

                filtered.Add(data);
            }

            SortList(filtered);

            foreach (InsectData data in filtered)
            {
                bool discovered = dexController.IsDiscovered(data.insectId);
                DexListItemUI item = Instantiate(itemPrefab, contentRoot);
                item.Initialize(data, discovered, HandleSelect);
            }
        }

        private void HandleSelect(string insectId)
        {
            if (detailUI != null)
            {
                detailUI.Show(insectId);
            }
        }

        private void HandleDexUpdated(DexSaveData data)
        {
            BuildList();
        }

        public void SetSortMode(int mode)
        {
            sortMode = (SortMode)Mathf.Clamp(mode, 0, System.Enum.GetValues(typeof(SortMode)).Length - 1);
            SavePrefs();
            BuildList();
        }

        public void SetFilterMode(int mode)
        {
            filterMode = (FilterMode)Mathf.Clamp(mode, 0, System.Enum.GetValues(typeof(FilterMode)).Length - 1);
            SavePrefs();
            BuildList();
        }

        public SortMode GetSortMode()
        {
            return sortMode;
        }

        public FilterMode GetFilterMode()
        {
            return filterMode;
        }

        public void AutoWire(InsectDatabase db, DexController dex, DexDetailUIController detail, Transform content, DexListItemUI prefab)
        {
            if (database == null)
            {
                database = db;
            }

            if (dexController == null || dexController != dex)
            {
                if (dexController != null)
                    dexController.DexUpdated -= HandleDexUpdated;
                dexController = dex;
                if (dexController != null)
                    dexController.DexUpdated += HandleDexUpdated;
            }

            if (detailUI == null)
            {
                detailUI = detail;
            }

            if (contentRoot == null)
            {
                contentRoot = content;
            }

            if (itemPrefab == null)
            {
                itemPrefab = prefab;
            }
        }

        private bool PassesFilter(bool discovered)
        {
            switch (filterMode)
            {
                case FilterMode.DiscoveredOnly:
                    return discovered;
                case FilterMode.UndiscoveredOnly:
                    return !discovered;
                default:
                    return true;
            }
        }

        private void SortList(List<InsectData> list)
        {
            switch (sortMode)
            {
                case SortMode.ByRarity:
                    list.Sort((a, b) =>
                    {
                        int rarityCompare = a.rarity.CompareTo(b.rarity);
                        if (rarityCompare != 0)
                        {
                            return rarityCompare;
                        }
                        return string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal);
                    });
                    break;
                case SortMode.ByCaptured:
                    list.Sort((a, b) =>
                    {
                        int aCount = dexController.TryGetRecord(a.insectId, out DexRecord aRecord) ? aRecord.capturedCount : 0;
                        int bCount = dexController.TryGetRecord(b.insectId, out DexRecord bRecord) ? bRecord.capturedCount : 0;
                        int countCompare = bCount.CompareTo(aCount);
                        if (countCompare != 0)
                        {
                            return countCompare;
                        }
                        return string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal);
                    });
                    break;
                default:
                    list.Sort((a, b) => string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal));
                    break;
            }
        }

        private void LoadPrefs()
        {
            int savedSort = PlayerPrefs.GetInt(GameConstants.PrefsKeys.DexSortMode, (int)sortMode);
            int savedFilter = PlayerPrefs.GetInt(GameConstants.PrefsKeys.DexFilterMode, (int)filterMode);

            sortMode = (SortMode)Mathf.Clamp(savedSort, 0, System.Enum.GetValues(typeof(SortMode)).Length - 1);
            filterMode = (FilterMode)Mathf.Clamp(savedFilter, 0, System.Enum.GetValues(typeof(FilterMode)).Length - 1);
        }

        private void SavePrefs()
        {
            PlayerPrefs.SetInt(GameConstants.PrefsKeys.DexSortMode, (int)sortMode);
            PlayerPrefs.SetInt(GameConstants.PrefsKeys.DexFilterMode, (int)filterMode);
            PlayerPrefs.Save();
        }
    }
}
