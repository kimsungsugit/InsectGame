using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Spawning
{
    public class InsectSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InsectDatabase database;
        [SerializeField] private WorldStateProvider worldStateProvider;
        [SerializeField] private SpawnPoint[] spawnPoints;
        [SerializeField] private ItemEffectManager itemEffects;

        [Header("Spawn Settings")]
        [SerializeField] private GameObject defaultPrefab;
        [SerializeField] private float spawnIntervalSeconds = 600f; // 10분
        [SerializeField] private int maxActiveTotal = 20;
        [SerializeField] private int prewarmPoolSize = 8;
        [SerializeField] private int initialSpawnCount = 5;
        [SerializeField] private int maxActivePerRegion = 5;

        private float spawnTimer;
        private float cleanupTimer;
        private float relocateTimer;
        private readonly List<InsectEntity> activeInsects = new List<InsectEntity>();
        private SimpleObjectPool pool;
        private bool poolInitialized;
        private string debugStatus = "초기화 대기";
        private int totalSpawned;

        private string raidAlertName;
        private InsectRarity raidAlertRarity;
        private float raidAlertTimer;
        private float raidAlertPulse;

        private void Start()
        {
            EnsureSelfSufficient();
            TryInitPool();
            SpawnInitialInsects();
        }

        private void EnsureSelfSufficient()
        {
            if (defaultPrefab == null)
            {
                defaultPrefab = CreateFallbackPrefab();
                Debug.Log("[InsectSpawner] 기본 프리팹이 없어서 자체 생성");
            }

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj == null) playerObj = GameObject.Find("Player");
                Vector3 center = playerObj != null ? playerObj.transform.position : Vector3.zero;

                List<SpawnPoint> points = new List<SpawnPoint>();
                for (int i = 0; i < 8; i++)
                {
                    float angle = Mathf.PI * 2f * i / 8f;
                    float dist = 12f + i * 2f;
                    Vector3 pos = center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                    GameObject pointObj = new GameObject($"FallbackSpawn_{i}");
                    pointObj.transform.position = pos;
                    points.Add(pointObj.AddComponent<SpawnPoint>());
                }
                spawnPoints = points.ToArray();
                Debug.Log($"[InsectSpawner] 스폰포인트가 없어서 {points.Count}개 자체 생성");
            }

            if (worldStateProvider == null)
            {
                worldStateProvider = FindFirstObjectByType<WorldStateProvider>();
                if (worldStateProvider != null)
                    Debug.Log("[InsectSpawner] WorldStateProvider 자동 탐색 완료");
            }

            if (database == null)
            {
                database = FindFirstObjectByType<InsectDatabase>();
                if (database == null)
                {
                    database = Resources.Load<InsectDatabase>("InsectDatabase");
                }
                if (database != null)
                    Debug.Log("[InsectSpawner] InsectDatabase 자동 탐색 완료");
            }
        }

        private GameObject CreateFallbackPrefab()
        {
            GameObject prefab = new GameObject("InsectPrefab_Fallback");

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(prefab.transform, false);
            body.transform.localScale = new Vector3(0.6f, 0.35f, 0.8f);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(prefab.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.05f, 0.4f);
            head.transform.localScale = new Vector3(0.35f, 0.3f, 0.35f);

            GameObject antennaL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            antennaL.name = "AntennaL";
            antennaL.transform.SetParent(head.transform, false);
            antennaL.transform.localPosition = new Vector3(-0.3f, 0.6f, 0.3f);
            antennaL.transform.localScale = new Vector3(0.08f, 0.4f, 0.08f);
            antennaL.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
            var colL = antennaL.GetComponent<Collider>();
            if (colL != null) UnityEngine.Object.Destroy(colL);

            GameObject antennaR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            antennaR.name = "AntennaR";
            antennaR.transform.SetParent(head.transform, false);
            antennaR.transform.localPosition = new Vector3(0.3f, 0.6f, 0.3f);
            antennaR.transform.localScale = new Vector3(0.08f, 0.4f, 0.08f);
            antennaR.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
            var colR = antennaR.GetComponent<Collider>();
            if (colR != null) UnityEngine.Object.Destroy(colR);

            prefab.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            prefab.SetActive(false);
            return prefab;
        }

        private void TryInitPool()
        {
            if (poolInitialized || defaultPrefab == null) return;
            pool = new SimpleObjectPool(defaultPrefab, prewarmPoolSize, transform);
            poolInitialized = true;
        }

        private void Update()
        {
            if (database == null || worldStateProvider == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                debugStatus = $"초기화 미완: DB={database != null} World={worldStateProvider != null} SP={spawnPoints?.Length ?? 0}";
                return;
            }

            if (defaultPrefab == null)
            {
                debugStatus = "프리팹 없음!";
                return;
            }

            if (raidAlertTimer > 0) raidAlertTimer -= Time.deltaTime;

            cleanupTimer += Time.deltaTime;
            if (cleanupTimer > 5f)
            {
                cleanupTimer = 0f;
                CleanupDeadEntities();
                DespawnFarInsects();
            }

            relocateTimer += Time.deltaTime;
            if (relocateTimer > 8f)
            {
                relocateTimer = 0f;
                RelocateSpawnPoints();
            }

            if (activeInsects.Count >= maxActiveTotal)
            {
                debugStatus = $"최대치 도달 ({activeInsects.Count}/{maxActiveTotal})";
                return;
            }

            string underpopulatedRegion = GetUnderpopulatedRegionId();
            if (!string.IsNullOrEmpty(underpopulatedRegion))
            {
                TrySpawn(underpopulatedRegion);
                return;
            }

            spawnTimer += Time.deltaTime;
            if (spawnTimer < spawnIntervalSeconds)
            {
                debugStatus = $"대기중... {spawnTimer:F1}/{spawnIntervalSeconds}s | 활성: {activeInsects.Count} | 총: {totalSpawned}";
                return;
            }

            spawnTimer = 0f;
            TrySpawn();
        }

        private void TrySpawn()
        {
            TrySpawn(null);
        }

        private void TrySpawn(string preferredRegionId)
        {
            TryInitPool();

            SpawnPoint point = GetAvailableSpawnPoint(preferredRegionId);
            if (point == null)
            {
                debugStatus = "스폰포인트 없음 (모두 사용중)";
                return;
            }

            if (!string.IsNullOrEmpty(point.regionId) && CountActiveInRegion(point.regionId) >= maxActivePerRegion)
            {
                debugStatus = $"리전 {point.regionId} 최대치 도달 ({maxActivePerRegion}마리)";
                return;
            }

            WorldState state = worldStateProvider.GetWorldState();
            List<InsectData> candidates = database.GetCandidates(state);
            if (candidates == null || candidates.Count == 0)
            {
                debugStatus = "후보 곤충 0마리 (조건 불일치)";
                return;
            }

            if (point.regionInsectIds != null && point.regionInsectIds.Length > 0)
            {
                List<InsectData> regionFiltered = new List<InsectData>();
                foreach (var c in candidates)
                {
                    foreach (string rid in point.regionInsectIds)
                    {
                        if (c.insectId == rid) { regionFiltered.Add(c); break; }
                    }
                }
                if (regionFiltered.Count > 0)
                    candidates = regionFiltered;
            }

            float rareBoost = (itemEffects != null ? itemEffects.GetRareSpawnMultiplier() : 1f)
                            * (outfitBonus != null ? outfitBonus.GetRareSpawnMultiplier() : 1f);
            InsectData selected = rareBoost > 1f
                ? database.GetWeightedRandomWithRareBoost(candidates, rareBoost)
                : database.GetWeightedRandom(candidates);
            if (selected == null)
            {
                debugStatus = "곤충 선택 실패";
                return;
            }

            GameObject prefab = selected.prefabOverride != null ? selected.prefabOverride : defaultPrefab;
            if (prefab == null)
            {
                debugStatus = "프리팹 null";
                return;
            }

            GameObject instance;
            if (pool != null && prefab == defaultPrefab)
            {
                instance = pool.Get();
            }
            else
            {
                instance = Instantiate(prefab, transform);
            }
            instance.SetActive(true);
            instance.transform.position = point.GetRandomPosition();

            InsectEntity entity = instance.GetComponent<InsectEntity>();
            if (entity == null)
            {
                entity = instance.AddComponent<InsectEntity>();
            }

            point.NotifySpawned();
            int level = GetSpawnLevel(selected, point);
            entity.Initialize(selected, level, point, DespawnEntity);
            activeInsects.Add(entity);
            totalSpawned++;
            debugStatus = $"스폰 성공! {selected.displayName} Lv.{level} | 활성: {activeInsects.Count} | 총: {totalSpawned}";

            if (selected.rarity == InsectRarity.Epic || selected.rarity == InsectRarity.Legendary)
            {
                raidAlertName = selected.displayName;
                raidAlertRarity = selected.rarity;
                raidAlertTimer = 6f;
                raidAlertPulse = 0f;
            }
        }

        private void SpawnInitialInsects()
        {
            if (database == null || worldStateProvider == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                return;
            }

            HashSet<string> seededRegions = new HashSet<string>();
            if (maxActivePerRegion > 0)
            {
                foreach (SpawnPoint point in spawnPoints)
                {
                    if (point == null || string.IsNullOrEmpty(point.regionId) || !seededRegions.Add(point.regionId))
                    {
                        continue;
                    }

                    if (activeInsects.Count >= maxActiveTotal)
                    {
                        break;
                    }

                    TrySpawn(point.regionId);
                }
            }

            int spawnCount = Mathf.Clamp(initialSpawnCount, 0, maxActiveTotal);
            int attempts = 0;
            int maxAttempts = Mathf.Max(spawnCount * 2, spawnPoints.Length * 6);
            while (activeInsects.Count < spawnCount && attempts < maxAttempts)
            {
                if (activeInsects.Count >= maxActiveTotal)
                {
                    break;
                }

                int beforeCount = activeInsects.Count;
                TrySpawn();
                attempts++;
                if (activeInsects.Count == beforeCount)
                {
                    break;
                }
            }

            spawnTimer = 0f;
        }

        private void OnGUI()
        {
            GUIStyle style = new GUIStyle(GUI.skin.box)
            { fontSize = 14, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            style.normal.textColor = Color.white;

            float y = Screen.height - 80;
            string info = $"[스포너] {debugStatus}\n" +
                          $"DB:{database != null} | 프리팹:{defaultPrefab != null} | 풀:{pool != null} | " +
                          $"SP:{spawnPoints?.Length ?? 0} | 활성:{activeInsects.Count}/{maxActiveTotal}";
            GUI.Box(new Rect(10, y, 600, 70), info, style);

            if (raidAlertTimer > 0)
                DrawRaidAlert();
        }

        private void DrawRaidAlert()
        {
            raidAlertPulse += Time.deltaTime;
            float alpha = raidAlertTimer > 1f ? 1f : raidAlertTimer;
            float fadeIn = Mathf.Clamp01((6f - raidAlertTimer) / 0.5f);
            alpha *= fadeIn;

            float sw = Screen.width;
            float cx = sw / 2f;
            float bannerH = 120f;
            float bannerY = 60f;

            bool isLegendary = raidAlertRarity == InsectRarity.Legendary;
            Color mainCol = isLegendary ? new Color(1f, 0.8f, 0.15f) : new Color(0.7f, 0.3f, 0.95f);
            Color bgCol = isLegendary ? new Color(0.15f, 0.1f, 0.02f) : new Color(0.08f, 0.03f, 0.15f);

            GUI.color = new Color(bgCol.r, bgCol.g, bgCol.b, 0.92f * alpha);
            GUI.DrawTexture(new Rect(0, bannerY, sw, bannerH), Texture2D.whiteTexture);

            float pulse = 0.6f + Mathf.Sin(raidAlertPulse * 6f) * 0.4f;
            GUI.color = new Color(mainCol.r, mainCol.g, mainCol.b, pulse * alpha);
            GUI.DrawTexture(new Rect(0, bannerY, sw, 4), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, bannerY + bannerH - 4, sw, 4), Texture2D.whiteTexture);

            float sideW = 120f + Mathf.Sin(raidAlertPulse * 4f) * 30f;
            GUI.color = new Color(mainCol.r, mainCol.g, mainCol.b, 0.08f * alpha);
            GUI.DrawTexture(new Rect(0, bannerY, sideW, bannerH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(sw - sideW, bannerY, sideW, bannerH), Texture2D.whiteTexture);

            GUI.color = Color.white;

            float shakeX = Mathf.Sin(raidAlertPulse * 12f) * 2f * Mathf.Clamp01(6f - raidAlertTimer);

            GUIStyle warningStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            warningStyle.normal.textColor = new Color(1f, 0.4f, 0.3f, alpha);
            GUI.Label(new Rect(shakeX, bannerY + 8, sw, 28),
                isLegendary ? "LEGENDARY RAID BOSS DETECTED!" : "EPIC RAID BOSS DETECTED!", warningStyle);

            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            nameStyle.normal.textColor = new Color(mainCol.r, mainCol.g, mainCol.b, alpha);
            GUI.Label(new Rect(shakeX, bannerY + 36, sw, 44), raidAlertName ?? "???", nameStyle);

            GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            hintStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, alpha * 0.8f);
            GUI.Label(new Rect(0, bannerY + 84, sw, 24), "가까이 가서 [E]키로 레이드에 도전하세요!", hintStyle);
        }

        private SpawnPoint GetAvailableSpawnPoint(string preferredRegionId = null)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(preferredRegionId))
            {
                int preferredStart = Random.Range(0, spawnPoints.Length);
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    SpawnPoint point = spawnPoints[(preferredStart + i) % spawnPoints.Length];
                    if (point != null && point.CanSpawn && point.regionId == preferredRegionId)
                    {
                        return point;
                    }
                }
            }

            int startIndex = Random.Range(0, spawnPoints.Length);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnPoint point = spawnPoints[(startIndex + i) % spawnPoints.Length];
                if (point != null && point.CanSpawn)
                {
                    return point;
                }
            }

            return null;
        }

        private string GetUnderpopulatedRegionId()
        {
            if (maxActivePerRegion <= 0 || spawnPoints == null || activeInsects.Count >= maxActiveTotal)
            {
                return null;
            }

            HashSet<string> regions = new HashSet<string>();
            foreach (SpawnPoint point in spawnPoints)
            {
                if (point != null && !string.IsNullOrEmpty(point.regionId))
                {
                    regions.Add(point.regionId);
                }
            }

            foreach (string regionId in regions)
            {
                if (CountActiveInRegion(regionId) < maxActivePerRegion && HasAvailableSpawnPoint(regionId))
                {
                    return regionId;
                }
            }

            return null;
        }

        private int CountActiveInRegion(string regionId)
        {
            int count = 0;
            for (int i = 0; i < activeInsects.Count; i++)
            {
                InsectEntity entity = activeInsects[i];
                if (entity != null && entity.RegionId == regionId)
                {
                    count++;
                }
            }

            return count;
        }

        private bool HasAvailableSpawnPoint(string regionId)
        {
            if (spawnPoints == null || string.IsNullOrEmpty(regionId))
            {
                return false;
            }

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnPoint point = spawnPoints[i];
                if (point != null && point.regionId == regionId && point.CanSpawn)
                {
                    return true;
                }
            }

            return false;
        }

        private void DespawnEntity(InsectEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            activeInsects.Remove(entity);

            if (pool != null && entity.gameObject.activeSelf && defaultPrefab != null)
            {
                pool.Return(entity.gameObject);
            }
            else
            {
                Destroy(entity.gameObject);
            }
        }

        private void CleanupDeadEntities()
        {
            for (int i = activeInsects.Count - 1; i >= 0; i--)
            {
                if (activeInsects[i] == null || !activeInsects[i].gameObject.activeInHierarchy)
                    activeInsects.RemoveAt(i);
            }
        }

        private void DespawnFarInsects()
        {
            Transform player = GetPlayerTransform();
            if (player == null) return;

            float maxDist = 60f;
            for (int i = activeInsects.Count - 1; i >= 0; i--)
            {
                InsectEntity entity = activeInsects[i];
                if (entity == null) { activeInsects.RemoveAt(i); continue; }
                float dist = Vector3.Distance(player.position, entity.transform.position);
                if (dist > maxDist)
                {
                    entity.Despawn();
                }
            }
        }

        private void RelocateSpawnPoints()
        {
            Transform player = GetPlayerTransform();
            if (player == null || spawnPoints == null) return;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null) continue;
                if (!spawnPoints[i].CanSpawn) spawnPoints[i].ResetCount();
                float angle = Mathf.PI * 2f * i / spawnPoints.Length;
                float dist = 10f + i * 3f;
                Vector3 pos = player.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                spawnPoints[i].transform.position = pos;
            }
        }

        private Transform GetPlayerTransform()
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null) playerObj = GameObject.Find("Player");
            return playerObj != null ? playerObj.transform : null;
        }

        private int GetSpawnLevel(InsectData data, SpawnPoint point)
        {
            if (data == null)
            {
                return 1;
            }

            int min, max;
            if (point != null && point.regionMaxLevel > 0)
            {
                min = Mathf.Max(1, point.regionMinLevel);
                max = Mathf.Max(min, point.regionMaxLevel);
            }
            else
            {
                min = Mathf.Max(1, data.minLevel);
                max = Mathf.Max(min, data.maxLevel);
            }

            // 메인 필드: Lv.1~max 범위에서 지수 분포 (항상 1부터)
            // 서브구역: min~max 범위에서 완화된 지수 분포 (고렙 보상)
            bool isSubArea = point != null && point.regionInsectIds != null && point.regionInsectIds.Length <= 5;

            int spawnMin, spawnMax;
            float power;
            if (isSubArea)
            {
                spawnMin = min;
                spawnMax = max;
                power = 2.0f;
            }
            else
            {
                spawnMin = 1;
                spawnMax = max;
                power = 3.5f;
            }

            // 등급이 높을수록 고레벨 나올 확률 추가 감소 (완화된 보정)
            if (data != null)
            {
                switch (data.rarity)
                {
                    case InsectRarity.Uncommon:  power += 0.3f; break;
                    case InsectRarity.Rare:      power += 0.6f; break;
                    case InsectRarity.Epic:      power += 1.0f; break;
                    case InsectRarity.Legendary: power += 1.5f; break;
                }
            }

            float roll = Random.value;
            float weighted = Mathf.Pow(roll, power);
            int level = spawnMin + Mathf.FloorToInt(weighted * (spawnMax - spawnMin + 1));
            return Mathf.Clamp(level, spawnMin, spawnMax);
        }

        public void AutoWire(InsectDatabase db, WorldStateProvider provider, SpawnPoint[] points)
        {
            if (database == null)
            {
                database = db;
            }

            if (worldStateProvider == null)
            {
                worldStateProvider = provider;
            }

            if ((spawnPoints == null || spawnPoints.Length == 0) && points != null && points.Length > 0)
            {
                spawnPoints = points;
            }
        }

        public void AutoWire(ItemEffectManager effects)
        {
            if (itemEffects == null)
            {
                itemEffects = effects;
            }
        }

        private OutfitBonusProvider outfitBonus;

        public void AutoWire(OutfitBonusProvider bonus)
        {
            if (outfitBonus == null)
            {
                outfitBonus = bonus;
            }
        }

        public void ApplyTuning(Core.GameplayTuningProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            spawnIntervalSeconds = Mathf.Max(1f, profile.spawnIntervalSeconds);
            maxActiveTotal = Mathf.Max(1, profile.maxActiveTotal);
        }
    }
}
