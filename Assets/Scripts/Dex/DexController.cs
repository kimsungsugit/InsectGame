using System;
using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Dex
{
    public class DexController : MonoBehaviour, InsectGame.Core.ICloudReloadable
    {
        [SerializeField] private InsectDatabase database;
        // 도감 첫 발견 코인 보상 지급용 — InsectLoreEntry.rewardCoins를 실제 지급(DexDetailUI가 광고하는 값).
        [SerializeField] private InsectGame.Core.PlayerCurrencyWallet wallet;

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

        // 코인 지갑 주입 — 첫 발견 보상 지급용. Bootstrap이 호출.
        public void AutoWire(InsectGame.Core.PlayerCurrencyWallet walletRef)
        {
            if (wallet == null)
            {
                wallet = walletRef;
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
            // 첫 발견 = 레코드 최초 생성 시점. 여기서 딱 한 번 코인 보상을 지급한다.
            // 재발견(이미 lookup에 존재)은 위에서 early-return되어 재지급 없음. Awake/ReloadFromDisk는
            // saveData.records를 직접 순회해 lookup을 채우므로(GetOrCreateRecord 미경유) 로드 시 지급되지 않는다.
            GrantFirstDiscoveryReward(insectId);
            return record;
        }

        // 도감 첫 발견 코인 보상 — InsectLoreEntry.rewardCoins를 실제 지급. DexDetailUI가 "보상: N 코인"으로
        // 광고만 하던 값의 실현. 곤충당 1회(GetOrCreateRecord 생성 시점에서만 호출). 세이브는 AddCoins가 트리거.
        private void GrantFirstDiscoveryReward(string insectId)
        {
            if (wallet == null)
            {
                return;
            }

            if (InsectLoreService.TryGetEntry(insectId, out InsectLoreEntry lore)
                && lore != null && lore.rewardCoins > 0)
            {
                wallet.AddCoins(lore.rewardCoins);
            }
        }

        private void SaveAndNotify()
        {
            DexSaveService.Save(saveData);
            DexUpdated?.Invoke(saveData);
        }

        // 클라우드 로드 후 dex_save.json을 다시 읽어 lookup 재구성 + UI 갱신.
        // DexUpdated는 표시 갱신용이라 발화 안전(퀘스트는 OpenDex 키 입력에만 반응).
        public void ReloadFromDisk()
        {
            lookup.Clear();
            saveData = DexSaveService.Load();
            foreach (DexRecord record in saveData.records)
            {
                if (!string.IsNullOrEmpty(record.insectId))
                {
                    lookup[record.insectId] = record;
                }
            }
            DexUpdated?.Invoke(saveData);
        }
    }
}
