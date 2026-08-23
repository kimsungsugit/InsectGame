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
        // 메인 필드 곤충이 너무 적던 문제 — 동시/리전/초기 수를 늘리고 스폰 간격을 단축.
        // 맵 1.5배(면적 2.25배) 확장에 맞춰 상한 재상향 (18/12/7 → 32/20/10).
        [SerializeField] private float spawnIntervalSeconds = 5f;
        [SerializeField] private int maxActiveTotal = 32;
        [SerializeField] private int prewarmPoolSize = 32;
        [SerializeField] private int initialSpawnCount = 20;
        [SerializeField] private int maxActivePerRegion = 10;

        /// <summary>정화 직후 곤충을 되돌리기까지의 사이(초) — 연출이 끝나고 채운다.</summary>
        private const float RepopulateDelaySeconds = 1.2f;
        /// <summary>정화 직후 한 번에 채우는 최대 마리 수(상한에 걸리면 그 전에 멈춘다).</summary>
        private const int RepopulateBurst = 6;

        private Core.RegionBlightManager blight;
        [SerializeField] private int subAreaActiveCount = 2;
        [SerializeField] private float subAreaRespawnSeconds = 45f;

        private float spawnTimer;
        private float cleanupTimer;
        private float relocateTimer;
        private float subAreaRespawnTimer;
        private readonly List<InsectEntity> activeInsects = new List<InsectEntity>();

        /// <summary>현재 활성 곤충 목록(읽기 전용) — NPC 등 외부 시스템이 FindObjectsByType 없이 소비.</summary>
        public IReadOnlyList<InsectEntity> ActiveInsects => activeInsects;
        private SimpleObjectPool pool;
        private bool poolInitialized;
        private int totalSpawned;

        private RegionManager regionManager;
        private Data.SubAreaData currentSubArea;

        public event System.Action<InsectEntity> RaidBossSpawned;

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
                return;
            }

            if (defaultPrefab == null)
            {
                return;
            }

            // SubArea 진입 중: 메인 월드 스폰/정리는 스킵하되, SubArea 곤충이 캡처/배틀로 줄어들면
            // 재스폰. 빈 SubArea가 되는 것 방지.
            if (currentSubArea != null)
            {
                CleanupDeadEntities();
                if (currentSubArea.exclusiveInsectIds != null
                    && currentSubArea.exclusiveInsectIds.Length > 0
                    && activeInsects.Count < 1)
                {
                    subAreaRespawnTimer += Time.deltaTime;
                    if (subAreaRespawnTimer >= subAreaRespawnSeconds)
                    {
                        subAreaRespawnTimer = 0f;
                        SpawnSubAreaInsects(currentSubArea);
                    }
                }
                return;
            }
            subAreaRespawnTimer = 0f;

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
                return;
            }

            spawnTimer += Time.deltaTime;
            if (spawnTimer < spawnIntervalSeconds)
            {
                return;
            }

            spawnTimer = 0f;
            string underpopulatedRegion = GetUnderpopulatedRegionId();
            TrySpawn(underpopulatedRegion);
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
                return;
            }

            if (!string.IsNullOrEmpty(point.regionId) && CountActiveInRegion(point.regionId) >= RegionCap(point.regionId))
            {
                return;
            }

            WorldState state = worldStateProvider.GetWorldState();
            List<InsectData> candidates = database.GetCandidates(state);
            if (candidates == null || candidates.Count == 0)
            {
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

            // (서브에리어 전용 곤충 필터가 여기 있었으나 **도달할 수 없는 코드**였다.
            //  Update는 currentSubArea가 있으면 조기 반환하고, TrySpawn의 다른 두 호출부
            //  — SpawnInitialInsects(Start)와 RepopulateCleansedRegion(서브에리어 가드) — 도
            //  서브에리어 중에는 오지 않는다. 서브에리어 스폰은 SpawnSubAreaInsects가 전담한다.
            //  게다가 검사에 쓰던 GetRandomPosition()이 실제 스폰 좌표와 **다른 난수**라
            //  살아 있었다 해도 엉뚱한 지점으로 판정했다.)

            float rareBoost = (itemEffects != null ? itemEffects.GetRareSpawnMultiplier() : 1f)
                            * (outfitBonus != null ? outfitBonus.GetRareSpawnMultiplier() : 1f);
            InsectData selected = rareBoost > 1f
                ? database.GetWeightedRandomWithRareBoost(candidates, rareBoost)
                : database.GetWeightedRandom(candidates);
            if (selected == null)
            {
                return;
            }

            SpawnAt(selected, point);
        }

        /// <summary>
        /// 고른 종을 이 스폰 포인트에 실제로 띄운다 — 풀 취득·배치·초기화·집계.
        ///
        /// <see cref="TrySpawn(string)"/>(무작위 선택)과 <see cref="TrySpawnSpecific"/>(종 지정)이
        /// 공유한다. 사본을 두면 풀 반환·레이드 통지·erased 확률 중 하나가 한쪽에만 반영돼
        /// 조용히 어긋난다.
        /// </summary>
        private void SpawnAt(InsectData selected, SpawnPoint point)
        {
            if (selected == null || point == null) return;

            GameObject prefab = selected.prefabOverride != null ? selected.prefabOverride : defaultPrefab;
            if (prefab == null)
            {
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
            entity.Initialize(selected, level, point, DespawnEntity, GetErasedChance(point.regionId));
            activeInsects.Add(entity);
            totalSpawned++;

            if (selected.rarity == InsectRarity.Epic || selected.rarity == InsectRarity.Legendary)
            {
                RaidBossSpawned?.Invoke(entity);
            }
        }

        /// <summary>
        /// 특정 종을 특정 리전에 한 마리 띄운다 — 정화 직후 "돌아온 종"을 확정 노출하는 용도.
        ///
        /// 무작위 스폰과 달리 <b>월드 상태(시간·날씨) 필터를 거치지 않는다.</b> 정화는 그 자리에서
        /// 눈으로 확인해야 하는 사건이라, 밤이라서 혹은 비가 와서 안 나오면 연출이 통째로 죽는다.
        /// 대신 리전 풀 밖의 종은 띄우지 않는다(스폰 포인트가 그 리전 소속이면 자동으로 만족).
        /// </summary>
        private bool TrySpawnSpecific(string insectId, string regionId)
        {
            if (database == null || string.IsNullOrEmpty(insectId)) return false;
            if (activeInsects.Count >= maxActiveTotal) return false;
            // 지금 유일한 호출부(정화 직후)는 상한이 막 넓어진 뒤라 무해하지만, 검사를 빼 두면
            // 다음 호출부가 리전 상한을 조용히 넘긴다.
            if (CountActiveInRegion(regionId) >= RegionCap(regionId)) return false;

            InsectData data = database.GetById(insectId);
            if (data == null) return false;

            SpawnPoint point = GetAvailableSpawnPoint(regionId);
            if (point == null || point.regionId != regionId) return false;

            SpawnAt(data, point);
            return true;
        }

        private void SpawnInitialInsects()
        {
            if (database == null || worldStateProvider == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                return;
            }

            HashSet<string> seededRegions = new HashSet<string>();
            int spawnCount = Mathf.Clamp(initialSpawnCount, 0, maxActiveTotal);
            if (maxActivePerRegion > 0)
            {
                foreach (SpawnPoint point in spawnPoints)
                {
                    if (point == null || string.IsNullOrEmpty(point.regionId) || !seededRegions.Add(point.regionId))
                    {
                        continue;
                    }

                    if (activeInsects.Count >= spawnCount)
                    {
                        break;
                    }

                    TrySpawn(point.regionId);
                }
            }

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





        private SpawnPoint GetAvailableSpawnPoint(string preferredRegionId = null)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return null;
            }

            // 60m 디스폰 밖 포인트에서의 스폰은 5초 내 정리되는 낭비(churn) — 55m 안쪽만 사용.
            // (리전 링이 정적으로 유지되면서 원거리 포인트가 상시 존재하게 된 데 대한 방어)
            Transform player = GetPlayerTransform();
            bool useDistanceGate = player != null;
            Vector3 playerPos = useDistanceGate ? player.position : Vector3.zero;
            const float maxSpawnDistSq = 55f * 55f;

            if (!string.IsNullOrEmpty(preferredRegionId))
            {
                int preferredStart = Random.Range(0, spawnPoints.Length);
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    SpawnPoint point = spawnPoints[(preferredStart + i) % spawnPoints.Length];
                    if (point != null && point.CanSpawn && point.regionId == preferredRegionId
                        && (!useDistanceGate || (point.transform.position - playerPos).sqrMagnitude <= maxSpawnDistSq))
                    {
                        return point;
                    }
                }
            }

            int startIndex = Random.Range(0, spawnPoints.Length);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnPoint point = spawnPoints[(startIndex + i) % spawnPoints.Length];
                if (point != null && point.CanSpawn
                    && (!useDistanceGate || (point.transform.position - playerPos).sqrMagnitude <= maxSpawnDistSq))
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
                // 오염 리전은 상한이 낮아 금방 "부족하지 않음"이 된다 — 이 자리를 안 고치면
                // 오염 리전이 계속 우선 스폰 대상으로 뽑혀 다른 리전이 굶는다.
                if (CountActiveInRegion(regionId) < RegionCap(regionId) && HasAvailableSpawnPoint(regionId))
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

        // EnsureSpawnPoints가 배치한 리전 링 원위치 스냅샷 — 리전 이탈 시 복귀용
        private Vector3[] spawnPointHomes;

        private void RelocateSpawnPoints()
        {
            Transform player = GetPlayerTransform();
            if (player == null || spawnPoints == null) return;

            if (spawnPointHomes == null || spawnPointHomes.Length != spawnPoints.Length)
            {
                spawnPointHomes = new Vector3[spawnPoints.Length];
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    if (spawnPoints[i] != null) spawnPointHomes[i] = spawnPoints[i].transform.position;
                }
            }

            string playerRegionId = regionManager != null && regionManager.CurrentRegion != null
                ? regionManager.CurrentRegion.regionId
                : null;

            // 현재 리전 라벨의 포인트만 플레이어 주변으로 당기고, 나머지는 링 원위치 유지.
            // (옛: 전 포인트를 무조건 플레이어 나선(10+i*3m)으로 → 배열 앞쪽(초원/연못) 라벨만
            //  60m 디스폰 안에 남아, 어느 리전에 가도 초원/연못 곤충만 체감되는 문제)
            // 포인트 카운트를 실제 활성 개체로 재동기화한다. 옛 코드는 상한에 걸린 포인트를
            // 무조건 0으로 밀어(ResetCount) 상한 2가 "동시 2마리"가 아니라 "8초당 2마리"가 됐다.
            for (int i = 0; i < spawnPoints.Length; i++)
                if (spawnPoints[i] != null) spawnPoints[i].BeginRecount();
            for (int i = 0; i < activeInsects.Count; i++)
            {
                InsectEntity live = activeInsects[i];
                if (live != null && live.OwnerPoint != null) live.OwnerPoint.NotifyLive();
            }

            int localIndex = 0;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null) continue;

                // **서브에리어 포인트는 옮기지 않는다.** 부모 리전 ID를 그대로 달고 있어
                // 아래 리전 필터에 함께 걸리는데, 끌려오면 게이트 원 바깥 가장자리에만 있어야 할
                // 전용종이 필드 한복판에 뜬다(숲이면 필드 포인트 5개 대 서브 포인트 8개라
                // 플레이어 주변 스폰의 과반이 전용종이 된다). 한 번 끌려오면 리전을 뜨기 전까지
                // 원위치로도 안 돌아온다.
                if (spawnPoints[i].isSubAreaPoint) continue;

                if (playerRegionId != null && spawnPoints[i].regionId == playerRegionId)
                {
                    float angle = localIndex * 2.399963f;          // 황금각 — 포인트 수 무관 고른 방위
                    float dist = 10f + (localIndex % 12) * 3f;      // 10~43m (60m 디스폰 안쪽)
                    spawnPoints[i].transform.position = player.position
                        + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                    localIndex++;
                }
                else
                {
                    spawnPoints[i].transform.position = spawnPointHomes[i];
                }
            }
        }

        // 옛은 GetPlayerTransform이 매 호출 GameObject.FindWithTag + GameObject.Find — DespawnFarInsects(5초)
        // /RelocateSpawnPoints(8초)에서 호출. lazy 캐싱으로 첫 1회 후 재사용.
        private Transform cachedPlayerTransformForSpawner;

        private Transform GetPlayerTransform()
        {
            if (cachedPlayerTransformForSpawner != null) return cachedPlayerTransformForSpawner;
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null) playerObj = GameObject.Find("Player");
            if (playerObj != null) cachedPlayerTransformForSpawner = playerObj.transform;
            return cachedPlayerTransformForSpawner;
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
            // 옛 판정은 `regionInsectIds.Length <= 5` 휴리스틱이었다. 지금은 오판이 없지만
            // (리전 풀 14~20종 / 서브에리어 2~4종) 리전 풀이 줄거나 서브에리어에 6번째 종이
            // 붙는 순간 레벨 곡선이 조용히 뒤집힌다 — add-region/add-insect 어느 쪽도 안 잡는다.
            bool isSubArea = point != null && point.isSubAreaPoint;

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

        public void AutoWire(RegionManager rm)
        {
            if (regionManager != null)
                regionManager.SubAreaChanged -= OnSubAreaChanged;
            regionManager = rm;
            Subscribe();
        }

        /// <summary>
        /// 구독을 한자리에 모은다 — <c>AutoWire</c>와 <c>OnEnable</c>이 함께 부른다.
        /// <c>OnDisable</c>이 해지만 하고 되살리는 곳이 없으면, 컴포넌트가 한 번 꺼졌다 켜지는
        /// 순간 서브에리어 스폰과 정화 복구가 조용히 죽는다(rules/ui-layout.md가 UI에서 겪은
        /// 것과 같은 형태의 결함이다 — <c>-=</c> 뒤 <c>+=</c>라 중복 구독은 되지 않는다).
        /// </summary>
        private void Subscribe()
        {
            if (regionManager != null)
            {
                regionManager.SubAreaChanged -= OnSubAreaChanged;
                regionManager.SubAreaChanged += OnSubAreaChanged;
            }
            if (blight != null)
            {
                blight.RegionCleansed -= OnRegionCleansed;
                blight.RegionCleansed += OnRegionCleansed;
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        /// <summary>
        /// 오염 거점 상태 — 거점이 살아 있는 리전은 동시 출현 수를 줄이고, 무너지면 되돌린다.
        /// </summary>
        public void AutoWire(Core.RegionBlightManager blightManager)
        {
            if (blight != null)
                blight.RegionCleansed -= OnRegionCleansed;
            blight = blightManager;
            Subscribe();
        }

        private void OnDisable()
        {
            if (regionManager != null)
                regionManager.SubAreaChanged -= OnSubAreaChanged;
            if (blight != null)
                blight.RegionCleansed -= OnRegionCleansed;
        }

        /// <summary>
        /// 이 리전의 동시 출현 상한. 오염 거점이 살아 있으면 줄어든다.
        ///
        /// <b>0으로 내려가지 않는 것이 중요하다</b> — 그 리전에서의 포획·전투를 조건으로 건
        /// 스토리 비트가 여럿이라(오염 아크 둘 + 1막의 특정 종 포획) 곤충이 아예 안 뜨면
        /// 발화 지점에 영영 도달하지 못한다. 하한은 <c>BlightPolicy.MinActive</c>가 든다.
        /// </summary>
        private int RegionCap(string regionId)
        {
            bool blighted = blight != null && blight.IsBlighted(regionId);
            return Core.BlightPolicy.MaxActiveFor(blighted, maxActivePerRegion);
        }

        /// <summary>
        /// 거점이 무너졌다 — 그 리전의 곤충을 즉시 되돌린다.
        ///
        /// 다음 <c>Update</c>를 기다리면 스폰 간격만큼 빈 들판이 남아 "돌아왔다"가 안 읽힌다.
        /// 귀환종을 먼저 한 마리 확정 스폰하는 것도 같은 이유다 — 무작위에 맡기면 정화 직후
        /// 화면에 흔한 종만 뜰 수 있다.
        /// </summary>
        private void OnRegionCleansed(string regionId)
        {
            if (!isActiveAndEnabled || string.IsNullOrEmpty(regionId)) return;
            StartCoroutine(RepopulateCleansedRegion(regionId));
        }

        private System.Collections.IEnumerator RepopulateCleansedRegion(string regionId)
        {
            // 정화 연출·컷신이 카메라를 잡고 있는 동안 곤충이 튀어나오면 화면이 어수선하다.
            // 한 박자 뒤에 채운다.
            yield return new WaitForSeconds(RepopulateDelaySeconds);
            if (blight == null || blight.IsBlighted(regionId)) yield break;   // 그새 상태가 바뀌었다

            // 그 사이 서브에리어로 들어갔을 수 있다 — 형제 코루틴 SpawnSubAreaInsectsDelayed가
            // 같은 자리에 같은 가드를 둔다. 안 두면 HideMainWorld가 숨긴 메인 월드 좌표에
            // 곤충이 떠서 보이지도 않는 채 maxActiveTotal 예산만 먹는다.
            if (currentSubArea != null) yield break;

            Data.RegionData region = regionManager != null ? regionManager.GetRegionById(regionId) : null;
            if (region != null && !string.IsNullOrEmpty(region.blightReturningInsectId))
                TrySpawnSpecific(region.blightReturningInsectId, regionId);

            int cap = RegionCap(regionId);
            for (int i = 0; i < RepopulateBurst; i++)
            {
                if (CountActiveInRegion(regionId) >= cap) break;
                if (activeInsects.Count >= maxActiveTotal) break;
                // **GetAvailableSpawnPoint는 선호 리전에 쓸 포인트가 없으면 아무 리전으로 폴백한다.**
                // 그런데 위 탈출 조건은 이 리전의 마릿수만 센다 — 가드가 없으면 스폰될 때마다
                // NotifySpawned가 포인트를 쿨다운에 넣어 후반이 폴백에 걸리고, 정작 정화한
                // 리전은 안 차는데 이웃 리전에 여섯 마리를 밀어 넣는다.
                if (!HasAvailableSpawnPoint(regionId)) break;
                TrySpawn(regionId);
            }
        }

        private void OnSubAreaChanged(SubAreaData subArea)
        {
            currentSubArea = subArea;

            if (subArea != null)
            {
                // SubArea 진입: 메인 월드 활성 곤충 전부 풀로 반환 (메인 월드가 SetActive(false)되어 잔존 방지)
                DespawnAllActiveInsects();

                if (subArea.exclusiveInsectIds != null && subArea.exclusiveInsectIds.Length > 0)
                {
                    // SubAreaWorldBuilder.EnterSubArea가 같은 이벤트에서 player를 SubAreaOrigin으로
                    // 텔레포트하지만 구독 순서상 InsectSpawner가 먼저 호출됨. 1프레임 yield 후 스폰해야
                    // player가 새 좌표에 있는 상태에서 anchor 계산 → 곤충과 player가 같은 공간.
                    StartCoroutine(SpawnSubAreaInsectsDelayed(subArea));
                }
            }
            else
            {
                // SubArea 종료: SubArea 전용 곤충은 메인 좌표와 무관한 위치에 있으므로
                // 메인 복귀 후 즉시 정리해야 화면에 잔존하지 않음 (다음 Update에서 메인 스폰 재개).
                DespawnAllActiveInsects();
            }
        }

        private System.Collections.IEnumerator SpawnSubAreaInsectsDelayed(SubAreaData subArea)
        {
            yield return null;
            // currentSubArea가 그새 바뀌었다면(빠른 Exit) 스폰 스킵
            if (currentSubArea != subArea) yield break;
            SpawnSubAreaInsects(subArea);
        }

        private void DespawnAllActiveInsects()
        {
            // 스냅샷 후 List 비움 — entity.Despawn() 콜백이 activeInsects.Remove 호출 시 no-op (안전)
            var snapshot = activeInsects.ToArray();
            activeInsects.Clear();
            foreach (var entity in snapshot)
            {
                if (entity != null) entity.Despawn();
            }
        }

        private void SpawnSubAreaInsects(SubAreaData subArea)
        {
            if (database == null) return;

            // (직전 OnSubAreaChanged에서 DespawnAllActiveInsects로 메인 곤충 전부 정리 완료)

            // SubArea 진입 시 SubAreaWorldBuilder가 player를 별도 좌표(SubAreaOrigin)로 텔레포트했으므로
            // subArea.centerPosition(메인 좌표) 기준 스폰은 player와 다른 공간이라 절대 만날 수 없다.
            // player 현재 위치(=SubAreaOrigin 근처)를 anchor로 사용.
            Vector3 anchor = subArea.centerPosition;
            Transform pTrans = GetPlayerTransform();
            if (pTrans != null) anchor = pTrans.position;

            int spawnCount = Mathf.Min(subArea.exclusiveInsectIds.Length + 1, subAreaActiveCount);
            for (int i = 0; i < spawnCount; i++)
            {
                string insectId = subArea.exclusiveInsectIds[i % subArea.exclusiveInsectIds.Length];
                InsectData data = database.GetById(insectId);
                if (data == null) continue;

                Vector2 offset = Random.insideUnitCircle * (subArea.radius * 0.6f);
                Vector3 spawnPos = anchor + new Vector3(offset.x, 0f, offset.y);
                int level = Random.Range(subArea.minLevel, subArea.maxLevel + 1);

                GameObject instance;
                if (pool != null)
                    instance = pool.Get();
                else
                    instance = Instantiate(defaultPrefab, transform);

                instance.SetActive(true);
                instance.transform.position = spawnPos;

                InsectEntity entity = instance.GetComponent<InsectEntity>();
                if (entity == null) entity = instance.AddComponent<InsectEntity>();

                entity.Initialize(data, level, null, DespawnEntity);
                activeInsects.Add(entity);
                totalSpawned++;
            }
        }

        /// <summary>
        /// 이 리전에서 「지워진 개체」가 나올 확률.
        ///
        /// 2막 리전에서만 나온다 — 판정은 <see cref="RegionDefinitions.IsAct2Region"/>가
        /// requiredLevel에서 파생시키므로 여기에 리전 ID 목록이 없다(하드코딩 목록은 이 저장소에서
        /// 세 번 어긋났다).
        ///
        /// 텅 빈 들이 유독 높은 것은 설계다. 거긴 잦아듦이 가장 먼저 훑고 간 폐허 초원이라
        /// 서식종 절반이 초원·습지 종 재활용인데, 그게 <b>이름을 잃은 모습</b>으로 보여야
        /// "초원이 죽은 자리"로 읽힌다. 아니면 그냥 저레벨 곤충이 잘못 나온 것처럼 보인다.
        /// </summary>
        private float GetErasedChance(string regionId)
        {
            if (regionManager == null || string.IsNullOrEmpty(regionId)) return 0f;
            Data.RegionData region = regionManager.GetRegionById(regionId);
            if (!RegionDefinitions.IsAct2Region(region)) return 0f;

            switch (regionId)
            {
                case "hollow": return 0.55f;    // 이름을 잃은 땅 — 절반 넘게
                case "nameless": return 0.35f;  // 무명이 갇힌 자리
                default: return 0.12f;          // 나머지 2막 — 이따금 눈에 띄는 정도
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
            initialSpawnCount = Mathf.Clamp(profile.initialSpawnCount, 1, maxActiveTotal);
            maxActivePerRegion = Mathf.Max(1, profile.maxActivePerRegion);
            subAreaActiveCount = Mathf.Max(1, profile.subAreaActiveCount);
            subAreaRespawnSeconds = Mathf.Max(5f, profile.subAreaRespawnSeconds);
        }
    }
}
