using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Data
{
    [CreateAssetMenu(menuName = "InsectGame/Insect Database", fileName = "InsectDatabase")]
    public class InsectDatabase : ScriptableObject
    {
        public List<InsectData> insects = new List<InsectData>();

        public InsectData GetById(string insectId)
        {
            if (string.IsNullOrEmpty(insectId)) return null;
            foreach (InsectData data in insects)
            {
                if (data != null && data.insectId == insectId)
                    return data;
            }
            return null;
        }

        public List<InsectData> GetCandidates(WorldState state)
        {
            List<InsectData> results = new List<InsectData>();
            foreach (InsectData data in insects)
            {
                if (data != null && data.Matches(state))
                {
                    results.Add(data);
                }
            }
            return results;
        }

        public InsectData GetWeightedRandom(List<InsectData> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            float total = 0f;
            foreach (InsectData data in candidates)
            {
                // null 가드 — 외부에서 직접 candidates 전달 시 NRE 차단(GetCandidates 외 경로).
                if (data == null) continue;
                total += Mathf.Max(0.01f, data.spawnWeight);
            }

            float roll = Random.Range(0f, total);
            float cumulative = 0f;
            foreach (InsectData data in candidates)
            {
                if (data == null) continue;
                cumulative += Mathf.Max(0.01f, data.spawnWeight);
                if (roll <= cumulative)
                {
                    return data;
                }
            }

            return candidates[0];
        }

        public InsectData GetWeightedRandomWithRareBoost(List<InsectData> candidates, float rareMultiplier)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            float total = 0f;
            foreach (InsectData data in candidates)
            {
                if (data == null) continue;
                float weight = Mathf.Max(0.01f, data.spawnWeight) * GetRarityWeight(data, rareMultiplier);
                total += weight;
            }

            float roll = Random.Range(0f, total);
            float cumulative = 0f;
            foreach (InsectData data in candidates)
            {
                if (data == null) continue;
                float weight = Mathf.Max(0.01f, data.spawnWeight) * GetRarityWeight(data, rareMultiplier);
                cumulative += weight;
                if (roll <= cumulative)
                {
                    return data;
                }
            }

            return candidates[0];
        }

        private float GetRarityWeight(InsectData data, float rareMultiplier)
        {
            if (data == null)
            {
                return 1f;
            }

            float baseWeight;
            switch (data.rarity)
            {
                case InsectRarity.Common:
                    baseWeight = 1f;
                    break;
                case InsectRarity.Uncommon:
                    baseWeight = 0.45f;
                    break;
                case InsectRarity.Rare:
                    baseWeight = 0.12f;
                    break;
                case InsectRarity.Epic:
                    baseWeight = 0.03f;
                    break;
                case InsectRarity.Legendary:
                    baseWeight = 0.008f;
                    break;
                default:
                    baseWeight = 1f;
                    break;
            }

            if (data.rarity >= InsectRarity.Rare)
            {
                baseWeight *= Mathf.Max(1f, rareMultiplier);
            }

            return baseWeight;
        }
    }
}
