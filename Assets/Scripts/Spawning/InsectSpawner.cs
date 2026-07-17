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
        private string debugStatus = "초기화 대기";
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
                debugStatus = $"초기화 미완: DB={database != null} World={worldStateProvider != null} SP={spawnPoints?.Length ?? 0}";
                return;
            }

            if (defaultPrefab == null)
            {
                debugStatus = "프리팹 없음!";
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
                debugStatus = $"SubArea 활성: {currentSubArea.subAreaId} | {activeInsects.Count}마리";
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
                debugStatus = $"최대치 도달 ({activeInsects.Count}/{maxActiveTotal})";
                return;
            }

            spawnTimer += Time.deltaTime;
            if (spawnTimer < spawnIntervalSeconds)
            {
                debugStatus = $"대기중... {spawnTimer:F1}/{spawnIntervalSeconds}s | 활성: {activeInsects.Count} | 총: {totalSpawned}";
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

            // 서브에리어 전용 곤충 우선
            if (currentSubArea != null && currentSubArea.exclusiveInsectIds != null && currentSubArea.exclusiveInsectIds.Length > 0)
            {
                Vector3 spawnPos = point.GetRandomPosition();
                if (currentSubArea.ContainsPoint(spawnPos))
                {
                    List<InsectData> subFiltered = new List<InsectData>();
                    foreach (var c in candidates)
                    {
                        foreach (string sid in currentSubArea.exclusiveInsectIds)
                        {
                            if (c.insectId == sid) { subFiltered.Add(c); break; }
                        }
                    }
                    if (subFiltered.Count > 0)
                        candidates = subFiltered;
                }
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
                RaidBossSpawned?.Invoke(entity);
            }
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

        // OnGUI GUIStyle 캐싱 — 옛 매 프레임 new GUIStyle 회귀 차단
        private GUIStyle debugStyleCache;
        // 렌더 진단 캐시 — 매 프레임 Find 비용 회피 위해 1초마다 갱신.
        private float nextDiagRefresh;
        private string renderDiagCache = "(측정 중)";
        // 프리미티브 빌트인 메시 null 프로브 — 1회만 실행. Plane 메시가 null이면 회색필드+MeshCollider 에러 확정.
        private string primProbeCache;

        private void OnGUI()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (debugStyleCache == null)
            {
                debugStyleCache = new GUIStyle(GUI.skin.box)
                { fontSize = 14, alignment = TextAnchor.MiddleLeft, wordWrap = true };
                debugStyleCache.normal.textColor = Color.white;
            }

            // 회색 필드 원인 추적용 렌더 진단 — 셰이더/라이트강도/fog/카메라클립/지형머티리얼.
            // 프리미티브 빌트인 메시 1회 프로브 — Plane 메시가 NULL이면 지형 안보임+MeshCollider 에러 확정.
            if (primProbeCache == null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder("Prim ");
                PrimitiveType[] types = { PrimitiveType.Plane, PrimitiveType.Quad, PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Cylinder, PrimitiveType.Capsule };
                string[] tn = { "Pl", "Qd", "Cb", "Sp", "Cy", "Cp" };
                for (int i = 0; i < types.Length; i++)
                {
                    GameObject probe = null;
                    bool meshOk = false;
                    try
                    {
                        probe = GameObject.CreatePrimitive(types[i]);
                        MeshFilter pmf = probe.GetComponent<MeshFilter>();
                        meshOk = pmf != null && pmf.sharedMesh != null && pmf.sharedMesh.vertexCount > 0;
                    }
                    catch { meshOk = false; }
                    finally { if (probe != null) Destroy(probe); }
                    sb.Append(tn[i]).Append(meshOk ? "=OK " : "=NULL ");
                }
                primProbeCache = sb.ToString();
                Debug.Log("[InsectSpawner] 프리미티브 메시 프로브 → " + primProbeCache);
            }

            if (Time.unscaledTime >= nextDiagRefresh)
            {
                nextDiagRefresh = Time.unscaledTime + 1f;
                bool stdNull = Shader.Find("Standard") == null;
                Light[] lightsArr = FindObjectsByType<Light>(FindObjectsSortMode.None);
                MeshRenderer[] rendsArr = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
                int onRends = 0;
                for (int i = 0; i < rendsArr.Length; i++)
                    if (rendsArr[i].enabled && rendsArr[i].gameObject.activeInHierarchy) onRends++;
                string lightInfo = lightsArr.Length > 0
                    ? $"{lightsArr[0].type}I{lightsArr[0].intensity:0.0}dir{lightsArr[0].transform.forward.y:0.00}"
                    : "none";
                Camera mc = Camera.main;
                string camInfo = mc != null
                    ? $"fov{mc.fieldOfView:0} clip{mc.nearClipPlane:0.0}-{mc.farClipPlane:0} y{mc.transform.position.y:0.0} rx{mc.transform.eulerAngles.x:0}"
                    : "null";
                string groundInfo;
                GameObject g = GameObject.Find("Ground");
                if (g == null) groundInfo = "noObj";
                else
                {
                    MeshRenderer gr = g.GetComponent<MeshRenderer>();
                    MeshFilter gmf = g.GetComponent<MeshFilter>();
                    string meshState = (gmf == null || gmf.sharedMesh == null) ? "MESHNULL" : $"v{gmf.sharedMesh.vertexCount}";
                    if (gr == null || gr.sharedMaterial == null) groundInfo = $"noMat {meshState}";
                    else groundInfo = $"{meshState} {gr.sharedMaterial.shader.name} c{ColHex(gr.sharedMaterial.color)}";
                }
                // 스카이박스 셰이더가 회색 출처일 수 있어 셰이더명 노출. 앰비언트 색도(검정이면 전체 어두움).
                string skyInfo = RenderSettings.skybox != null
                    ? $"{(RenderSettings.skybox.shader != null ? RenderSettings.skybox.shader.name : "noShader")}"
                    : "null";
                renderDiagCache =
                    $"Std:{(stdNull ? "NULL" : "OK")} Light:{lightInfo} Amb:{RenderSettings.ambientMode}/{ColHex(RenderSettings.ambientLight)}\n" +
                    $"Fog:{(RenderSettings.fog ? RenderSettings.fogMode.ToString() : "off")} Cam:{camInfo}\n" +
                    $"Rend:{rendsArr.Length}(on{onRends}) Grd:{groundInfo}\n" +
                    $"Sky:{skyInfo} {primProbeCache}";
            }

            float y = Screen.height - 190;
            string info = $"[스포너] {debugStatus}\n" +
                          $"DB:{database != null} | 프리팹:{defaultPrefab != null} | 풀:{pool != null} | " +
                          $"SP:{spawnPoints?.Length ?? 0} | 활성:{activeInsects.Count}/{maxActiveTotal}\n" +
                          $"[렌더] {renderDiagCache}";
            GUI.Box(new Rect(10, y, 780, 174), info, debugStyleCache);

            // 테스트용 튜토리얼 리셋 버튼 (개발빌드 전용).
            if (GUI.Button(new Rect(660, y, 170, 50), "튜토리얼 리셋"))
            {
                TutorialQuestManager.Instance?.RestartTutorialForTesting();
            }
#endif
        }


#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static string ColHex(Color c) =>
            $"{Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255):X2}{Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255):X2}{Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255):X2}";
#endif

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
            int localIndex = 0;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null) continue;
                if (!spawnPoints[i].CanSpawn) spawnPoints[i].ResetCount();

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

        public void AutoWire(RegionManager rm)
        {
            if (regionManager != null)
                regionManager.SubAreaChanged -= OnSubAreaChanged;
            regionManager = rm;
            if (regionManager != null)
                regionManager.SubAreaChanged += OnSubAreaChanged;
        }

        private void OnDisable()
        {
            if (regionManager != null)
                regionManager.SubAreaChanged -= OnSubAreaChanged;
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
