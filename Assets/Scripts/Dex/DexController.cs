using System;
using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Dex
{
    public class DexController : MonoBehaviour
    {
        [SerializeField] private InsectDatabase database;

        private DexSaveData saveData;
        private readonly Dictionary<string, DexRecord> lookup = new Dictionary<string, DexRecord>();

        public event Action<DexSaveData> DexUpdated;

        private void Awake()
        {
            saveData = DexSaveService.Load();
            foreach (DexRecord record in saveData.records)
            {
                if (!string.IsNullOrEmpty(record.insectId))
                {
                    lookup[record.insectId] = record;
                }
            }
        }

        public void RegisterEncounter(string insectId)
        {
            if (string.IsNullOrEmpty(insectId))
            {
                return;
            }

            DexRecord record = GetOrCreateRecord(insectId);
            record.discoveredCount++;
            SaveAndNotify();
        }

        public void RegisterCapture(string insectId)
        {
            if (string.IsNullOrEmpty(insectId))
            {
                return;
            }

            DexRecord record = GetOrCreateRecord(insectId);
            record.capturedCount++;
            SaveAndNotify();
        }

        public bool IsDiscovered(string insectId)
        {
            return lookup.ContainsKey(insectId);
        }

        public bool HasRecord(string insectId)
        {
            return lookup.TryGetValue(insectId, out DexRecord r) && r.capturedCount > 0;
        }

        public bool TryGetRecord(string insectId, out DexRecord record)
        {
            return lookup.TryGetValue(insectId, out record);
        }

        public DexSaveData GetSaveData()
        {
            return saveData;
        }

        private DexRecord GetOrCreateRecord(string insectId)
        {
            if (lookup.TryGetValue(insectId, out DexRecord record))
            {
                return record;
            }

            record = new DexRecord(insectId);
            lookup[insectId] = record;
            saveData.records.Add(record);
            return record;
        }

        private void SaveAndNotify()
        {
            DexSaveService.Save(saveData);
            DexUpdated?.Invoke(saveData);
        }
    }
}
