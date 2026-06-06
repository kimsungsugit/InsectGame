using System;
using System.Collections.Generic;
using UnityEngine;
using InsectGame.Data;

namespace InsectGame.Core
{
    [Serializable]
    public class GachaResult
    {
        public string insectId;
        public string displayName;
        public InsectRarity rarity;
        public bool isExclusive;
        public int bonusCandy;
    }

    public class GachaBoxManager : MonoBehaviour
    {
        public static GachaBoxManager Instance { get; private set; }

        private PlayerInsectCollection insectCollection;
        private PlayerCandyInventory candyInventory;

        public GachaResult LastResult { get; private set; }
        public event Action<GachaResult> BoxOpened;

        // -- 가챠 전용 곤충 풀 --
        private static readonly Dictionary<InsectRarity, string[]> gachaExclusives = new Dictionary<InsectRarity, string[]>
        {
            { InsectRarity.Rare,      new[] { "gacha_golden_ladybug", "gacha_neon_firefly", "gacha_phantom_moth" } },
            { InsectRarity.Epic,      new[] { "gacha_crystal_dragonfly", "gacha_shadow_mantis", "gacha_ice_spider", "gacha_storm_hornet" } },
            { InsectRarity.Legendary, new[] { "gacha_rainbow_butterfly", "gacha_diamond_beetle", "gacha_celestial_beetle" } }
        };

        // -- 일반 곤충 풀 (등급별) --
        private static readonly Dictionary<InsectRarity, string[]> normalPool = new Dictionary<InsectRarity, string[]>
        {
            { InsectRarity.Common,    new[] { "beetle_basic", "bee_worker", "cricket_field", "ant_soldier", "grasshopper_green", "ladybug_seven", "caterpillar_green", "moth_brown", "beetle_dung", "aphid_colony" } },
            { InsectRarity.Uncommon,  new[] { "mantis_green", "dragonfly_lake", "water_strider_pond", "katydid_leaf", "damselfly_blue", "butterfly_cabbage", "beetle_click", "wasp_paper", "longhorn_beetle" } },
            { InsectRarity.Rare,      new[] { "stag_beetle", "rhinoceros_beetle", "butterfly_swallowtail", "hornet_asian", "jewel_beetle_gold", "firefly_blue", "stick_insect_long", "beetle_longhorn_rosalia" } },
            { InsectRarity.Epic,      new[] { "mantis_ghost", "atlas_moth_giant", "beetle_hercules", "butterfly_morpho", "leaf_insect_phantom", "spider_golden_orb", "luna_moth_silver" } },
            { InsectRarity.Legendary, new[] { "mantis_orchid", "butterfly_alexandras", "beetle_golden_stag", "dragonfly_ancient" } }
        };

        // -- 가챠 전용 곤충 한글 이름 매핑 --
        private static readonly Dictionary<string, string> gachaDisplayNames = new Dictionary<string, string>
        {
            { "gacha_golden_ladybug",    "황금무당벌레" },
            { "gacha_crystal_dragonfly", "수정잠자리" },
            { "gacha_shadow_mantis",     "그림자사마귀" },
            { "gacha_rainbow_butterfly", "무지개나비" },
            { "gacha_diamond_beetle",    "다이아몬드풍뎅이" },
            { "gacha_neon_firefly",      "네온반딧불이" },
            { "gacha_ice_spider",        "얼음거미" },
            { "gacha_phantom_moth",      "환영나방" },
            { "gacha_storm_hornet",      "폭풍말벌" },
            { "gacha_celestial_beetle",  "천상의풍뎅이" }
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void AutoWire(PlayerInsectCollection col, PlayerCandyInventory candy)
        {
            if (insectCollection == null) insectCollection = col;
            if (candyInventory == null) candyInventory = candy;
        }

        public void AutoWire(InsectDatabase db)
        {
            if (database == null) database = db;
        }

        // 캐시 — GetInsectDisplayName이 매 가챠 결과마다 FindFirstObjectByType 호출하던 회귀 차단
        private InsectDatabase database;

        // PickRandomInsect 결과 검증: data drift로 코드 상수의 ID가 DB에 없을 수 있음.
        // 옛은 검증 없이 AddCapturedInsect 호출 → DB에서 못 찾으면 에러 + 보상 일부만 지급.
        // 무효 ID면 fallback "beetle_basic"(Meadow Common, 항상 존재) 사용.
        private string ValidateInsectId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "beetle_basic";
            InsectDatabase db = database;
            if (db == null) db = FindFirstObjectByType<InsectDatabase>();
            if (db == null || db.insects == null) return id; // DB 미로드 — 검증 스킵
            for (int i = 0; i < db.insects.Count; i++)
            {
                if (db.insects[i] != null && db.insects[i].insectId == id) return id;
            }
            Debug.LogWarning($"[Gacha] insectId '{id}' DB 미존재 — fallback beetle_basic 사용");
            return "beetle_basic";
        }

        // 동시/중복 OpenBox 호출 차단 (버튼 rapid-click, 네트워크 지연 시 보상 이중 지급 방지)
        private bool isOpening;

        public void OpenBox(string boxId)
        {
            if (isOpening) return;
            isOpening = true;
            try
            {
                float roll = UnityEngine.Random.value * 100f;
                InsectRarity rarity;
                int bonusCandy;

                switch (boxId)
                {
                    case "box_bronze":
                        rarity = GetBronzeRarity(roll);
                        bonusCandy = UnityEngine.Random.Range(5, 16);
                        break;
                    case "box_silver":
                        rarity = GetSilverRarity(roll);
                        bonusCandy = UnityEngine.Random.Range(10, 31);
                        break;
                    case "box_gold":
                        rarity = GetGoldRarity(roll);
                        bonusCandy = UnityEngine.Random.Range(20, 51);
                        break;
                    default:
                        return;
                }

                string insectId = ValidateInsectId(PickRandomInsect(rarity, boxId));
                bool isExclusive = insectId.StartsWith("gacha_");

                // 각 보상을 독립 try-catch로 감싸 한 단계 실패가 나머지를 막지 않게 함
                // (곤충 지급 예외로 캔디/도감 미실행되는 회귀 방지).
                try
                {
                    if (insectCollection != null)
                        insectCollection.AddCapturedInsect(insectId, GetGachaLevel(rarity));
                }
                catch (System.Exception e) { Debug.LogError($"[Gacha] 곤충 지급 실패: {e.Message}"); }

                try
                {
                    if (candyInventory != null)
                        candyInventory.AddCandy(bonusCandy);
                }
                catch (System.Exception e) { Debug.LogError($"[Gacha] 캔디 지급 실패: {e.Message}"); }

                try
                {
                    Dex.DexController dex = FindFirstObjectByType<Dex.DexController>();
                    if (dex != null)
                    {
                        dex.RegisterEncounter(insectId);
                        dex.RegisterCapture(insectId);
                    }
                }
                catch (System.Exception e) { Debug.LogError($"[Gacha] 도감 등록 실패: {e.Message}"); }

                LastResult = new GachaResult
                {
                    insectId = insectId,
                    displayName = GetInsectDisplayName(insectId),
                    rarity = rarity,
                    isExclusive = isExclusive,
                    bonusCandy = bonusCandy
                };

                BoxOpened?.Invoke(LastResult);
            }
            finally
            {
                isOpening = false;
            }
        }

        // -- 등급 결정 --

        private InsectRarity GetBronzeRarity(float roll)
        {
            if (roll < 55f)   return InsectRarity.Common;
            if (roll < 85f)   return InsectRarity.Uncommon;
            if (roll < 97f)   return InsectRarity.Rare;
            if (roll < 99.5f) return InsectRarity.Epic;
            return InsectRarity.Legendary;
        }

        private InsectRarity GetSilverRarity(float roll)
        {
            // C:12% U:25% R:33% E:22% L:8% — 가격 600젬, EV/gem×1000 = 2.632 (bronze 2.308 대비 1.140x)
            if (roll < 12f) return InsectRarity.Common;
            if (roll < 37f) return InsectRarity.Uncommon;
            if (roll < 70f) return InsectRarity.Rare;
            if (roll < 92f) return InsectRarity.Epic;
            return InsectRarity.Legendary;
        }

        private InsectRarity GetGoldRarity(float roll)
        {
            // C:5% U:5% R:13% E:32% L:45% — 가격 750젬, EV/gem×1000 = 2.940 (silver 2.632 대비 1.117x)
            if (roll < 5f)  return InsectRarity.Common;
            if (roll < 10f) return InsectRarity.Uncommon;
            if (roll < 23f) return InsectRarity.Rare;
            if (roll < 55f) return InsectRarity.Epic;
            return InsectRarity.Legendary;
        }

        // -- 곤충 선택 --

        private string PickRandomInsect(InsectRarity rarity, string boxId)
        {
            float exclusiveChance;
            if (boxId == "box_gold") exclusiveChance = 0.5f;
            else if (boxId == "box_silver") exclusiveChance = 0.3f;
            else exclusiveChance = 0.2f;

            if (gachaExclusives.ContainsKey(rarity) && UnityEngine.Random.value < exclusiveChance)
            {
                string[] pool = gachaExclusives[rarity];
                return pool[UnityEngine.Random.Range(0, pool.Length)];
            }

            if (normalPool.ContainsKey(rarity))
            {
                string[] pool = normalPool[rarity];
                return pool[UnityEngine.Random.Range(0, pool.Length)];
            }

            return "beetle_basic";
        }

        private int GetGachaLevel(InsectRarity rarity)
        {
            switch (rarity)
            {
                case InsectRarity.Common:    return UnityEngine.Random.Range(1, 6);
                case InsectRarity.Uncommon:  return UnityEngine.Random.Range(3, 10);
                case InsectRarity.Rare:      return UnityEngine.Random.Range(5, 15);
                case InsectRarity.Epic:      return UnityEngine.Random.Range(10, 20);
                case InsectRarity.Legendary: return UnityEngine.Random.Range(15, 25);
                default: return 1;
            }
        }

        private string GetInsectDisplayName(string insectId)
        {
            if (gachaDisplayNames.ContainsKey(insectId))
                return gachaDisplayNames[insectId];

            // InsectLore.json 기반 이름은 InsectLoreService에서 이미 적용됨
            // database 캐시 우선, 없으면 FindFirstObjectByType fallback (옛은 매번 Find 호출)
            InsectDatabase db = database;
            if (db == null) db = FindFirstObjectByType<InsectDatabase>();
            if (db != null && db.insects != null)
            {
                foreach (var insect in db.insects)
                {
                    if (insect.insectId == insectId)
                        return insect.displayName;
                }
            }

            return insectId;
        }
    }
}
