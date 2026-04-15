using System;
using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    public struct ActiveSetInfo
    {
        public OutfitSetDefinition set;
        public int equippedCount;
        public bool isPartialActive;
        public bool isFullActive;
    }

    public class OutfitBonusProvider : MonoBehaviour
    {
        private CharacterOutfitManager outfitManager;
        private OutfitSetDefinition[] allSets;
        private OutfitStatBonus cachedTotal;
        private ActiveSetInfo[] cachedActiveSets;

        public event Action BonusChanged;

        public void AutoWire(CharacterOutfitManager manager)
        {
            if (outfitManager != null) return;
            outfitManager = manager;
            outfitManager.OutfitChanged += Recalculate;
            allSets = OutfitSetCatalog.GetAllSets();
            cachedActiveSets = new ActiveSetInfo[0];
            Recalculate();
        }

        private void OnEnable()
        {
            if (outfitManager != null)
            {
                outfitManager.OutfitChanged -= Recalculate;
                outfitManager.OutfitChanged += Recalculate;
            }
        }

        private void OnDisable()
        {
            if (outfitManager != null)
                outfitManager.OutfitChanged -= Recalculate;
        }

        // ── 조회 API ──

        public float GetCaptureChanceBonus() => cachedTotal.captureChanceBonus;
        public float GetExpMultiplier() => 1f + cachedTotal.expMultiplier;
        public float GetCandyMultiplier() => 1f + cachedTotal.candyMultiplier;
        public float GetRareSpawnMultiplier() => 1f + cachedTotal.rareSpawnBonus;
        public float GetMoveSpeedMultiplier() => 1f + cachedTotal.moveSpeedBonus;
        public float GetAtkBonus() => cachedTotal.atkBonus;
        public float GetDefBonus() => cachedTotal.defBonus;
        public OutfitStatBonus GetTotalBonus() => cachedTotal;
        public ActiveSetInfo[] GetActiveSets() => cachedActiveSets;

        // ── 재계산 ──

        private void Recalculate()
        {
            if (outfitManager == null) return;

            OutfitStatBonus total = default;

            // 1. 개별 아이템 보너스 합산
            OutfitSlot[] slots = (OutfitSlot[])Enum.GetValues(typeof(OutfitSlot));
            HashSet<string> equippedIds = new HashSet<string>();

            foreach (OutfitSlot slot in slots)
            {
                OutfitItem equipped = outfitManager.GetEquipped(slot);
                if (equipped == null) continue;
                equippedIds.Add(equipped.itemId);
                total = total + equipped.statBonus;
            }

            // 2. 세트 보너스 계산
            List<ActiveSetInfo> activeList = new List<ActiveSetInfo>();

            if (allSets != null)
            {
                foreach (OutfitSetDefinition set in allSets)
                {
                    int count = 0;
                    foreach (string reqId in set.requiredItemIds)
                    {
                        if (equippedIds.Contains(reqId)) count++;
                    }

                    if (count >= set.partialThreshold)
                    {
                        bool isFull = count >= set.requiredItemIds.Length;
                        OutfitStatBonus setBonus = isFull ? set.fullBonus : set.partialBonus;
                        total = total + setBonus;

                        activeList.Add(new ActiveSetInfo
                        {
                            set = set,
                            equippedCount = count,
                            isPartialActive = true,
                            isFullActive = isFull
                        });
                    }
                    else if (count > 0)
                    {
                        activeList.Add(new ActiveSetInfo
                        {
                            set = set,
                            equippedCount = count,
                            isPartialActive = false,
                            isFullActive = false
                        });
                    }
                }
            }

            cachedTotal = total;
            cachedActiveSets = activeList.ToArray();
            BonusChanged?.Invoke();
        }
    }
}
