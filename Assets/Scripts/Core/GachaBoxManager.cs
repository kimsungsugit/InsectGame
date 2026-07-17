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
        // 각 티어의 ID는 InsectDatabase 실제 rarity와 일치해야 함(저가 박스가 상위 곤충을 누출하거나
        // 팝업 등급≠수집 등급으로 갈리지 않도록). 추가 안전장치로 OpenBox가 결과 등급을 DB rarity로 보정한다.
        private static readonly Dictionary<InsectRarity, string[]> normalPool = new Dictionary<InsectRarity, string[]>
        {
            // 확장 64종 중 대표 26종 추가(등급 분산) — 각 티어 rarity는 InsectExpansionDefinitions와 일치
            { InsectRarity.Common,    new[] { "beetle_basic", "bee_worker", "cricket_field", "ant_soldier", "grasshopper_green", "ladybug_seven", "caterpillar_green", "moth_brown", "aphid_colony", "wasp_paper", "stick_insect_long", "water_strider_pond",
                                              "bee_bumble", "grasshopper_brown", "mosquito_tiger", "water_strider_stream", "ladybug_alpine", "centipede_red" } },
            { InsectRarity.Uncommon,  new[] { "mantis_green", "dragonfly_lake", "katydid_leaf", "damselfly_blue", "longhorn_beetle", "beetle_dung",
                                              "ladybug_harlequin", "antlion_pit", "dragonfly_scarlet", "bee_carpenter", "butterfly_peacock", "firefly_swamp" } },
            { InsectRarity.Rare,      new[] { "stag_beetle", "rhinoceros_beetle", "butterfly_swallowtail", "hornet_asian", "beetle_longhorn_rosalia", "butterfly_cabbage", "beetle_click", "butterfly_morpho", "spider_golden_orb",
                                              "dragonfly_jade", "stag_beetle_saw", "mantis_bark", "butterfly_glasswing", "moth_hummingbird", "butterfly_apollo", "scarab_relic" } },
            { InsectRarity.Epic,      new[] { "mantis_ghost", "leaf_insect_phantom", "luna_moth_silver", "jewel_beetle_gold", "firefly_blue", "mantis_orchid",
                                              "rhinoceros_beetle_titan", "bee_queen", "jewel_beetle_azure", "stag_beetle_iron" } },
            { InsectRarity.Legendary, new[] { "butterfly_alexandras", "beetle_golden_stag", "dragonfly_ancient", "atlas_moth_giant", "beetle_hercules",
                                              "scarab_pharaoh", "moth_comet", "hornet_emperor" } }
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

        // 곤충 ID의 DB 실제 rarity 조회 — 가챠 결과 등급 단일 출처. DB 미존재/미로드면 fallback(굴린 티어).
        private InsectRarity GetDbRarity(string insectId, InsectRarity fallback)
        {
            InsectDatabase db = database;
            if (db == null) db = FindFirstObjectByType<InsectDatabase>();
            if (db != null && db.insects != null)
            {
                for (int i = 0; i < db.insects.Count; i++)
                    if (db.insects[i] != null && db.insects[i].insectId == insectId)
                        return db.insects[i].rarity;
            }
            return fallback;
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
                // 결과/레벨 등급은 DB 실제 rarity 단일 출처 — 풀 배치와 DB가 어긋나도 팝업·도감·컬렉션이
                // 갈리지 않게(확률 표기 의무 정합). 풀에 없는/검증 fallback 곤충도 DB 기준으로 표시.
                InsectRarity resultRarity = GetDbRarity(insectId, rarity);

                // 각 보상을 독립 try-catch로 감싸 한 단계 실패가 나머지를 막지 않게 함
                // (곤충 지급 예외로 캔디/도감 미실행되는 회귀 방지).
                try
                {
                    if (insectCollection != null)
                        insectCollection.AddCapturedInsect(insectId, GetGachaLevel(resultRarity));
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
                    rarity = resultRarity,
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
        // 누적 임계값(roll 0~100). 분기 로직과 UI 확률 표기가 같은 출처를 쓰도록 상수로 분리.
        // 임계 배열: [C상한, U상한, R상한, E상한] — L은 나머지(임계4~100). 변경 = 밸런스 변경이므로 신중히.
        private static readonly float[] BronzeThresholds = { 55f, 85f, 97f, 99.5f };
        private static readonly float[] SilverThresholds = { 12f, 37f, 70f, 92f };
        private static readonly float[] GoldThresholds   = { 4f, 12f, 30f, 75f };

        // 표기 순서와 동일한 등급 배열 (C, U, R, E, L).
        private static readonly InsectRarity[] RarityOrder =
        {
            InsectRarity.Common, InsectRarity.Uncommon, InsectRarity.Rare,
            InsectRarity.Epic, InsectRarity.Legendary
        };

        private static InsectRarity GetRarityByThresholds(float roll, float[] t)
        {
            if (roll < t[0]) return InsectRarity.Common;
            if (roll < t[1]) return InsectRarity.Uncommon;
            if (roll < t[2]) return InsectRarity.Rare;
            if (roll < t[3]) return InsectRarity.Epic;
            return InsectRarity.Legendary;
        }

        private static float[] GetThresholds(string boxId)
        {
            switch (boxId)
            {
                case "box_bronze": return BronzeThresholds;
                case "box_silver": return SilverThresholds;
                case "box_gold":   return GoldThresholds;
                default:           return null;
            }
        }

        // boxId별 실제 등급 확률(%). 분기 임계값에서 파생 — UI 표기 단일 출처.
        // 반환: (등급, 퍼센트)[] 순서는 C,U,R,E,L. 임계 [a,b,c,d] → C=a, U=b-a, R=c-b, E=d-c, L=100-d.
        public (InsectRarity rarity, float percent)[] GetRates(string boxId)
        {
            float[] t = GetThresholds(boxId);
            if (t == null) return System.Array.Empty<(InsectRarity, float)>();

            var rates = new (InsectRarity rarity, float percent)[RarityOrder.Length];
            float prev = 0f;
            for (int i = 0; i < t.Length; i++)
            {
                rates[i] = (RarityOrder[i], t[i] - prev);
                prev = t[i];
            }
            rates[t.Length] = (RarityOrder[t.Length], 100f - prev); // Legendary = 나머지
            return rates;
        }

        // UI 표기용 문자열. DrawGachaTab의 하드코딩 rateText를 대체.
        // 형식: "C:12%  U:25%  R:33%\nE:22%  L:8%" (정수면 정수, 소수면 소수 1자리).
        public string GetRateText(string boxId)
        {
            var rates = GetRates(boxId);
            if (rates.Length == 0) return string.Empty;

            string[] labels = { "C", "U", "R", "E", "L" };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < rates.Length; i++)
            {
                // 첫 3개(C,U,R) 한 줄, 나머지(E,L) 다음 줄.
                if (i == 3) sb.Append('\n');
                else if (i > 0) sb.Append("  ");
                sb.Append(labels[i]).Append(':').Append(FormatPercent(rates[i].percent)).Append('%');
            }
            return sb.ToString();
        }

        private static string FormatPercent(float p)
        {
            // 정수면 소수점 없이, 아니면 소수 1자리 (예: 0.5, 2.5).
            return (Mathf.Abs(p - Mathf.Round(p)) < 0.001f)
                ? Mathf.RoundToInt(p).ToString()
                : p.ToString("0.#");
        }

        private InsectRarity GetBronzeRarity(float roll) => GetRarityByThresholds(roll, BronzeThresholds);

        private InsectRarity GetSilverRarity(float roll) => GetRarityByThresholds(roll, SilverThresholds);

        private InsectRarity GetGoldRarity(float roll) => GetRarityByThresholds(roll, GoldThresholds);

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
