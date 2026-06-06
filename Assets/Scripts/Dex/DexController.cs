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
        // 같은 insectId 중복 호출 안전망 (0.1초 디바운스). 정상 액션은 같은 곤충을 그 시간 안에
        // 두 번 등록하지 않음. Battle+Capture+Raid+Gacha 4개 호출처가 서로 독립 액션이지만 안전망.
        private readonly Dictionary<string, float> lastEncounterTime = new Dictionary<string, float>();
        private readonly Dictionary<string, float> lastCaptureTime = new Dictionary<string, float>();
        private const float DexDebounceSeconds = 0.1f;

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

            // 0.1초 안 같은 곤충 중복 등록 차단
            if (lastEncounterTime.TryGetValue(insectId, out float last)
                && Time.unscaledTime - last < DexDebounceSeconds)
            {
                return;
            }
            lastEncounterTime[insectId] = Time.unscaledTime;

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

            if (lastCaptureTime.TryGetValue(insectId, out float last)
                && Time.unscaledTime - last < DexDebounceSeconds)
            {
                return;
            }
            lastCaptureTime[insectId] = Time.unscaledTime;

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
