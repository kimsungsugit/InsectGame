using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Spawning
{
    /// <summary>
    /// 필드 곤충 개체군 — <b>기록(슬롯)이 곧 개체이고, 월드의 곤충은 가까울 때만 빌려 쓰는 몸이다.</b>
    ///
    /// 리전마다 면적에 비례한 슬롯(<see cref="FieldSlot"/>)이 있고 각 슬롯이 종·레벨·색다름·자리를 기록한다.
    /// 플레이어 45m 안의 산 슬롯만 풀에서 몸을 꺼내 세우고, 55m 밖이면 몸만 풀로 돌린다. 그래서
    /// <b>리전을 오가도 곤충이 새로 굴려지지 않는다</b> — 돌아오면 같은 곤충이 같은 자리에 있다.
    /// 새 개체가 들어서는 건 시간뿐이다: 잡거나 이기거나 놓친 자리는 1~2분 뒤에, 수명(4~7분)이 다한 자리는
    /// 플레이어 눈 밖에서 그 시점의 시간대·날씨로 바뀐다.
    ///
    /// 예전엔 5초마다 1마리씩 상한(리전 10·전체 32)까지 채우고 60m 밖은 지웠으며, 8초마다 현재 리전 스폰
    /// 포인트를 플레이어 둘레 10~43m로 끌어왔다 — 리전을 옮기면 옛 곤충은 사라지고 새로 굴린 곤충이 채워졌다
    /// (지역 이동 리롤). 서브에리어도 들어갈 때마다 새로 굴렸다.
    ///
    /// 수치·규칙은 <see cref="FieldSpawnRules"/>, 슬롯 전이는 <see cref="FieldPopulation"/>이 든다. 세션 간에는
    /// 저장하지 않는다 — 로드하면 새로 채운다.
    /// </summary>
    public class InsectSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InsectDatabase database;
        [SerializeField] private WorldStateProvider worldStateProvider;
        [SerializeField] private SpawnPoint[] spawnPoints;
        [SerializeField] private ItemEffectManager itemEffects;

        [Header("Spawn Settings")]
        [SerializeField] private GameObject defaultPrefab;
        // 동시 실체 상한 — 기록(슬롯) 수가 아니라 월드에 세운 몸의 수다. 플레이어 45m 안에는 보통 10~14마리가 선다.
        [SerializeField] private int maxActiveTotal = 32;
        [SerializeField] private int prewarmPoolSize = 32;
        [SerializeField] private int subAreaActiveCount = 2;
        [SerializeField] private float subAreaRespawnSeconds = 45f;

        /// <summary>정화 직후 곤충을 되돌리기까지의 사이(초) — 연출이 끝나고 채운다.</summary>
        private const float RepopulateDelaySeconds = 1.2f;

        /// <summary>
        /// 정화 직후 플레이어 둘레 고리(<see cref="NearPlayerRingMin"/>~<see cref="NearPlayerRingMax"/>m)에 세우는 수(귀환종 포함).
        /// 나머지 늘어난 자리는 리전 전체에 흩는다 — 전부 발밑에 몰면 오염 전보다 붐빈다.
        /// </summary>
        private const int RepopulateNearCount = 5;
        private const float NearPlayerRingMin = 10f;
        private const float NearPlayerRingMax = 30f;

        private Core.RegionBlightManager blight;
        private OutfitBonusProvider outfitBonus;
        private RegionManager regionManager;
        private SubAreaData currentSubArea;
        private string currentSubAreaKey;

        private SimpleObjectPool pool;
        private bool poolInitialized;

        private readonly List<InsectEntity> activeInsects = new List<InsectEntity>();

        /// <summary>지금 월드에 서 있는 곤충(읽기 전용) — NPC 등 외부 시스템이 FindObjectsByType 없이 소비.</summary>
        public IReadOnlyList<InsectEntity> ActiveInsects => activeInsects;

        /// <summary>
        /// 영웅·전설 곤충이 월드에 섰다(몸을 세울 때마다) — 지도(<c>RegionMapUI</c>)가 레이드 표식을 단다.
        /// 멀어졌다 돌아와 같은 개체가 다시 서면 다시 울린다. 옛 표식은 몸이 풀로 돌아가면 지도가 스스로 지우고,
        /// 풀에서 같은 몸이 다른 개체로 되살아난 경우는 <see cref="InsectEntity.SpawnSerial"/>로 가린다.
        /// </summary>
        public event System.Action<InsectEntity> RaidBossSpawned;

        /// <summary>
        /// 스토리가 지금 기다리는 포획 종 — (리전 ID, 채울 목록). 부트스트랩이 <c>StoryDirector</c>에 배선한다.
        /// 스포닝이 스토리를 직접 참조하지 않게 정적 훅으로 둔다(<see cref="InsectEntity.FleePreventChanceProvider"/>와
        /// 같은 형태). null이면 보조 없이 등급표대로만 굴린다.
        /// </summary>
        public static System.Action<string, List<string>> StoryCaptureTargetProvider;

        private readonly FieldPopulation population = new FieldPopulation();
        private readonly Dictionary<InsectEntity, FieldSlot> slotByEntity = new Dictionary<InsectEntity, FieldSlot>();
        private readonly List<RegionSpawnInfo> regionInfos = new List<RegionSpawnInfo>();
        private readonly Dictionary<string, RegionSpawnInfo> regionInfoById = new Dictionary<string, RegionSpawnInfo>();
        private readonly List<string> pendingCleansed = new List<string>();

        /// <summary>
        /// 정화 복구(<see cref="RepopulateCleansed"/>)를 기다리는 리전 — 그동안 평소 틱이 슬롯을 늘리지 않는다.
        ///
        /// 정화는 목록에 먼저 들고 이벤트가 나중이라, 늘어난 상한을 다음 틱(0.5초 안)이 먼저 보고 새 슬롯을 평소 규칙
        /// (리전 전역·플레이어 20m 밖)으로 채워 버렸다. 1.2초 뒤의 복구는 빈 자리가 없어 "플레이어 둘레 다섯 마리"를
        /// 한 번도 못 했고, 귀환종이 먼 곳에 먼저 뽑히면 그것마저 가까이 오지 않았다(2026-09-29 리뷰).
        /// </summary>
        private readonly HashSet<string> cleanseHold = new HashSet<string>();

        /// <summary>한 틱에 새로 굴리는 최대 수 — 서브에리어에 오래 있다 나오면 만료된 자리가 한꺼번에 몰린다.</summary>
        private const int MaxRollsPerTick = 24;

        private readonly List<string> rotationWanted = new List<string>();

        private float tickTimer;
        private bool fieldFillAllowed;   // 물 기록을 기다렸다(Start)
        private bool fieldReady;         // 메인 필드 첫 채우기가 끝났다
        private Vector3 subAreaAnchor;   // 서브에리어 진입 직후 플레이어 자리 — 방 안 판정의 눈
        private bool subAreaAnchored;
        private bool storyProviderWarned;

        // 틱마다 재사용하는 버퍼 — Update 경로에서 할당하지 않는다.
        private readonly List<FieldSlot> materializeQueue = new List<FieldSlot>();
        private readonly List<InsectData> regionCandidates = new List<InsectData>();
        private readonly List<InsectData> rarityCandidates = new List<InsectData>();
        private readonly bool[] rarityAvailable = new bool[FieldSpawnRules.RarityCount];
        private readonly List<string> storyWanted = new List<string>();
        private readonly HashSet<string> storyCandidateIds = new HashSet<string>();
        private readonly HashSet<string> storyAliveIds = new HashSet<string>();
        private readonly HashSet<string> stateCandidateIds = new HashSet<string>();
        private readonly Collider[] bodyProbeHits = new Collider[16];
        private bool stateCandidatesValid;
        private WorldState stateCandidatesFor;
        private static readonly System.Comparison<FieldSlot> ByDistance = (a, b) => a.SortKey.CompareTo(b.SortKey);

        // 자리 굴리기 고리 — PickSpawnPosition에 넘기는 대리자를 매번 새로 만들지 않게 필드로 든다.
        private Vector3 rollCenter;
        private float rollInnerRadius;
        private float rollOuterRadius;
        private System.Func<Vector3> rollInRing;

        /// <summary>리전 하나의 스폰 표 — 풀·레벨 대역은 스폰 포인트가, 원판·서브에리어 게이트·귀환종은 리전 정의가 든다.</summary>
        private sealed class RegionSpawnInfo
        {
            public string RegionId;
            public Vector3 Center;
            public float Radius;
            public int MinLevel;
            public int MaxLevel;
            public SubAreaData[] SubAreas;
            public RegionData Data;
            /// <summary>오염 전 슬롯 수(설 수 있는 땅 × 밀도). 오염 감소는 <see cref="RegionCap"/>만 얹는다.</summary>
            public int BaseSlots;
            public readonly List<InsectData> Pool = new List<InsectData>();
        }

        /// <summary>
        /// 초기 채우기가 물 기록(<see cref="RegionDressingBuilder.WaterRecorded"/>)을 기다리는 최대 프레임 수. 장식 빌더도
        /// 부트스트랩 Awake에서 붙어 같은 프레임의 Start에서 물을 적는데, 같은 프레임 Start끼리의 순서는 정해져 있지 않다 —
        /// 스포너가 먼저 돌면 나루터 물가·습지 웅덩이·강을 모른 채 첫 개체군을 놓는다. 한 프레임이면 모든 Start가 끝난다.
        /// 장식 빌더가 없는 씬(월드 미생성·테스트)에서도 영원히 기다리지 않게 상한을 둔다.
        /// </summary>
        private const int WaterWaitFrames = 2;

        private void Start()
        {
            EnsureSelfSufficient();
            TryInitPool();
            if (RegionDressingBuilder.WaterRecorded) AllowFieldFill();
            else StartCoroutine(AllowFieldFillAfterWater());
        }

        private System.Collections.IEnumerator AllowFieldFillAfterWater()
        {
            for (int i = 0; i < WaterWaitFrames && !RegionDressingBuilder.WaterRecorded; i++)
                yield return null;
            AllowFieldFill();
        }

        private void AllowFieldFill()
        {
            fieldFillAllowed = true;
            // 그 사이 서브에리어로 들어갔을 수 있다(마지막 서브에리어 복원) — 그땐 메인 월드 콜라이더가 숨겨져
            // 자리를 못 잰다. 나오는 첫 틱에 채운다(Update).
            if (currentSubArea == null) FillField();
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
            if (database == null || defaultPrefab == null)
            {
                return;
            }

            tickTimer += Time.deltaTime;
            if (tickTimer < FieldSpawnRules.TickSeconds)
            {
                return;
            }
            tickTimer = 0f;

            CleanupDeadEntities();

            // 서브에리어 안: 메인 필드 기록은 그대로 두고(시간은 계속 흐른다) 방의 슬롯만 돌본다.
            if (currentSubArea != null)
            {
                TickSubArea();
                return;
            }

            if (!fieldReady)
            {
                if (fieldFillAllowed) FillField();
                if (!fieldReady) return;
            }

            TickField();
        }

        // ======= 메인 필드 — 채우기 · 재생 · 순환 · 실체화 =======

        /// <summary>
        /// 시작 채우기 — 모든 리전의 슬롯을 한 번에 굴린다. 기록은 싸다(몸은 플레이어 둘레에만 선다).
        /// 리전별로 따로 채우던 옛 흐름(현재 리전만 채우고, 옮기면 다시)은 없다.
        /// </summary>
        private void FillField()
        {
            if (fieldReady || currentSubArea != null) return;
            if (!BuildRegionInfos()) return;
            TryInitPool();
            // 부트스트랩이 막 세운 콜라이더(나무 줄기·건물·바위)를 잰다 — 자동 동기화가 꺼져 있어
            // (DynamicsManager m_AutoSyncTransforms 0) 첫 물리 스텝 전엔 옮긴 자리를 모른다.
            Physics.SyncTransforms();

            float now = Time.time;
            for (int k = 0; k < regionInfos.Count; k++)
            {
                RegionSpawnInfo info = regionInfos[k];
                if (!cleanseHold.Contains(info.RegionId)) ReconcileRegionSlots(info, now);
                List<FieldSlot> slots = population.SlotsOf(info.RegionId);
                for (int i = 0; i < slots.Count; i++)
                {
                    if (FieldPopulation.IsDueForRoll(slots[i], now))
                        RollFieldSlot(slots[i], info, now, FieldSpawnRules.InitialSpawnMinPlayerDistance,
                            FieldSpawnRules.RollInitialLifetime(Random.value));
                }
            }
            fieldReady = true;

            if (TryGetPlayerPosition(out Vector3 playerPos)) SyncFieldBodies(playerPos);

            // 한 번만 남기는 요약 — 배치 캡처·기기 로그로 개체군 크기와 첫 화면의 몸 수를 확인한다.
            int slotTotal = 0, alive = 0;
            var perRegion = new System.Text.StringBuilder();
            for (int k = 0; k < regionInfos.Count; k++)
            {
                List<FieldSlot> slots = population.SlotsOf(regionInfos[k].RegionId);
                slotTotal += slots.Count;
                for (int i = 0; i < slots.Count; i++) if (slots[i].IsAlive) alive++;
                perRegion.Append(k > 0 ? ", " : string.Empty).Append(regionInfos[k].RegionId).Append(' ')
                    .Append(slots.Count).Append('/').Append(regionInfos[k].BaseSlots);
            }
            Debug.Log($"[InsectSpawner] 필드 개체군 채움 — 리전 {regionInfos.Count}곳, 슬롯 {slotTotal}(산 개체 {alive}), "
                      + $"지금 선 몸 {activeInsects.Count} | 리전별 슬롯/기본: {perRegion}");
        }

        private void TickField()
        {
            float now = Time.time;

            // 서브에리어 안에서 정화된 리전 — 메인 월드가 보이는 지금 채운다.
            if (pendingCleansed.Count > 0)
            {
                for (int i = 0; i < pendingCleansed.Count; i++) RepopulateCleansed(pendingCleansed[i]);
                pendingCleansed.Clear();
            }

            bool hasPlayer = TryGetPlayerPosition(out Vector3 playerPos);
            int rolls = 0;
            for (int k = 0; k < regionInfos.Count; k++)
            {
                RegionSpawnInfo info = regionInfos[k];
                // 오염 상한은 로그인·클라우드 재적재로도 바뀐다 — 매 틱 맞춘다(바뀌지 않았으면 비교만 한다).
                // 정화 복구를 기다리는 리전은 복구가 늘린다(cleanseHold 주석).
                if (!cleanseHold.Contains(info.RegionId)) ReconcileRegionSlots(info, now);

                List<FieldSlot> slots = population.SlotsOf(info.RegionId);
                for (int i = 0; i < slots.Count && rolls < MaxRollsPerTick; i++)
                {
                    FieldSlot slot = slots[i];
                    if (FieldPopulation.IsDueForRoll(slot, now))
                    {
                        // 빈 자리 재생 — 그 시점의 시간대·날씨로, 플레이어 코앞은 피해서.
                        RollFieldSlot(slot, info, now, FieldSpawnRules.SpawnMinPlayerDistance,
                            FieldSpawnRules.RollLifetime(Random.value));
                        rolls++;
                        continue;
                    }

                    if (!FieldPopulation.IsExpired(slot, now)) continue;
                    float d = hasPlayer ? PlanarDistance(slot.Position, playerPos) : float.PositiveInfinity;
                    bool busy = slot.IsMaterialized && IsBusy(slot.Entity);
                    if (!FieldPopulation.CanRotate(slot, now, d, busy)) continue;
                    // 본편이 기다리는 포획 목표종은 수명으로 바꾸지 않는다 — 찾으러 오는 사이 사라지면 보조(20%)로
                    // 다시 나오기까지 또 기다려야 한다. 비트가 발화하면 목표에서 빠져 다음 수명에 평소대로 돈다.
                    if (IsStoryCaptureTarget(info.RegionId, slot.InsectId)) continue;
                    rolls++;

                    // 수명 순환 — 몸이 있으면(25m 밖) 먼저 거두고 그 자리를 새 개체로 바꾼다.
                    RecallSlot(slot);
                    FieldPopulation.Vacate(slot, now);
                    RollFieldSlot(slot, info, now, FieldSpawnRules.SpawnMinPlayerDistance,
                        FieldSpawnRules.RollLifetime(Random.value));
                }
            }

            if (hasPlayer) SyncFieldBodies(playerPos);
        }

        /// <summary>
        /// 몸을 세우고 거둔다. <b>세우기가 먼저다</b> — 먼저 거두면 방금 풀로 돌린 몸이 같은 틱에 다른 개체로 다시 나와,
        /// 그 참조를 쥔 쪽(지도 레이드 표식·아이 NPC 예약)이 한 프레임 안의 교체를 못 본다.
        /// </summary>
        private void SyncFieldBodies(Vector3 playerPos)
        {
            materializeQueue.Clear();
            for (int k = 0; k < regionInfos.Count; k++)
            {
                List<FieldSlot> slots = population.SlotsOf(regionInfos[k].RegionId);
                for (int i = 0; i < slots.Count; i++)
                {
                    FieldSlot slot = slots[i];
                    if (!slot.IsAlive || slot.IsMaterialized) continue;
                    float d = PlanarDistance(slot.Position, playerPos);
                    if (!FieldPopulation.ShouldMaterialize(slot, d)) continue;
                    slot.SortKey = d;
                    materializeQueue.Add(slot);
                }
            }
            // 동시 상한에 걸리면 가까운 것부터 선다.
            if (materializeQueue.Count > 1) materializeQueue.Sort(ByDistance);
            for (int i = 0; i < materializeQueue.Count && activeInsects.Count < maxActiveTotal; i++)
                Materialize(materializeQueue[i], materializeQueue[i].Key);

            for (int i = activeInsects.Count - 1; i >= 0; i--)
            {
                if (i >= activeInsects.Count) continue;
                InsectEntity e = activeInsects[i];
                if (ReferenceEquals(e, null) || !slotByEntity.TryGetValue(e, out FieldSlot slot) || slot.IsSubArea) continue;
                if (FieldPopulation.ShouldRecall(slot, PlanarDistance(slot.Position, playerPos), IsBusy(e)))
                    RecallSlot(slot);
            }
        }

        /// <summary>
        /// 빈 슬롯에 새 개체를 들인다 — 종(등급표 → 리전 풀)·자리(리전 원판 전체)·레벨(리전 대역)을 한 번 굴려 기록한다.
        /// 종이나 자리를 못 구하면 <see cref="FieldSpawnRules.RetrySeconds"/> 뒤에 다시 굴린다(스폰을 영영 건너뛰지 않는다).
        /// </summary>
        private bool RollFieldSlot(FieldSlot slot, RegionSpawnInfo info, float now, float minPlayerDistance,
            float lifetime, InsectData forced = null, bool nearPlayer = false)
        {
            InsectData data = forced != null ? forced : PickFieldSpecies(info);
            if (data == null || !TryPickFieldPosition(info, minPlayerDistance, nearPlayer, out Vector3 position))
            {
                FieldPopulation.Vacate(slot, now + FieldSpawnRules.RetrySeconds);
                return false;
            }

            int level = FieldSpawnRules.RollFieldLevel(info.MinLevel, info.MaxLevel, Random.value);
            // 색다름·지워짐도 여기서 한 번만 굴린다 — 몸을 다시 세울 때마다 굴리면 같은 곤충의 색이 바뀐다.
            bool shiny = Random.value < InsectEntity.FieldShinyChance;
            float erasedChance = GetErasedChance(info.RegionId);
            bool erased = erasedChance > 0f && Random.value < erasedChance;
            FieldPopulation.Fill(slot, data.insectId, data, level, shiny, erased, position, now + lifetime);
            return true;
        }

        /// <summary>
        /// 종 고르기 — <b>등급을 먼저 전역 표로 굴리고</b>(<see cref="FieldSpawnRules.PickRarity"/>, 레어 부스트 포함),
        /// 그 등급의 후보(리전 풀 ∩ 지금 시간대·날씨) 안에서 <c>spawnWeight</c>로 종을 고른다. 그래서 등급 분포는
        /// 리전과 무관하다. 스토리가 기다리는 종이 그 리전에 없으면 일정 확률로 그 종을 먼저 준다.
        /// </summary>
        private InsectData PickFieldSpecies(RegionSpawnInfo info)
        {
            if (info.Pool.Count == 0) return null;

            regionCandidates.Clear();
            if (RefreshStateCandidates())
            {
                for (int i = 0; i < info.Pool.Count; i++)
                    if (stateCandidateIds.Contains(info.Pool[i].insectId)) regionCandidates.Add(info.Pool[i]);
            }
            // 시간대·날씨에 리전 풀이 통째로 걸러지면 풀 전체로 굴린다. 옛 코드는 이때 전역 DB 후보로 새서
            // 그 리전과 무관한 종(초원 한복판에 유적 전설)이 뜰 수 있었다.
            if (regionCandidates.Count == 0) regionCandidates.AddRange(info.Pool);

            InsectData story = PickStoryAssist(info);
            if (story != null) return story;

            for (int r = 0; r < rarityAvailable.Length; r++) rarityAvailable[r] = false;
            for (int i = 0; i < regionCandidates.Count; i++)
            {
                int r = (int)regionCandidates[i].rarity;
                if (r >= 0 && r < rarityAvailable.Length) rarityAvailable[r] = true;
            }
            int rarity = FieldSpawnRules.PickRarity(Random.value, RareSpawnBoost(), rarityAvailable);
            if (rarity < 0) return null;

            rarityCandidates.Clear();
            for (int i = 0; i < regionCandidates.Count; i++)
                if ((int)regionCandidates[i].rarity == rarity) rarityCandidates.Add(regionCandidates[i]);
            return InsectDatabase.PickWeighted(rarityCandidates, Random.value);
        }

        /// <summary>아이템·의상의 희귀 출현 배수 — 등급표의 희귀 이상 몫에 곱한다(<see cref="FieldSpawnRules.BoostedShare"/>).</summary>
        private float RareSpawnBoost()
        {
            return (itemEffects != null ? itemEffects.GetRareSpawnMultiplier() : 1f)
                   * (outfitBonus != null ? outfitBonus.GetRareSpawnMultiplier() : 1f);
        }

        /// <summary>
        /// 지금 시간대·날씨에 나올 수 있는 종 ID. 상태가 바뀔 때만 다시 만든다(게임 하루 12분에 몇 번).
        /// 공급자가 없으면 false — 걸러 내지 않는다.
        /// </summary>
        private bool RefreshStateCandidates()
        {
            if (worldStateProvider == null || database == null) return false;
            WorldState state = worldStateProvider.GetWorldState();
            if (stateCandidatesValid && state.DayPhase == stateCandidatesFor.DayPhase
                && state.Weather == stateCandidatesFor.Weather && state.Hour24 == stateCandidatesFor.Hour24)
                return true;

            stateCandidatesValid = true;
            stateCandidatesFor = state;
            stateCandidateIds.Clear();
            List<InsectData> list = database.GetCandidates(state);
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) stateCandidateIds.Add(list[i].insectId);
            return true;
        }

        /// <summary>이 종이 지금 이 리전에서 본편 포획 목표인가(수명 순환 면제용). 조회 실패는 "아니다"로 본다.</summary>
        private bool IsStoryCaptureTarget(string regionId, string insectId)
        {
            System.Action<string, List<string>> provider = StoryCaptureTargetProvider;
            if (provider == null || string.IsNullOrEmpty(insectId)) return false;
            rotationWanted.Clear();
            try { provider(regionId, rotationWanted); }
            catch (System.Exception) { return false; }   // 경고는 PickStoryAssist가 한 번 남긴다
            return rotationWanted.Contains(insectId);
        }

        /// <summary>
        /// 스토리 포획 보조 — 발화를 기다리는 <c>CaptureInsect</c> 비트의 종이 이 리전에 살아 있지 않으면
        /// <see cref="FieldSpawnRules.StoryAssistChance"/> 확률로 그 종을 준다. 확률을 먼저 굴려 대부분의 굴림은
        /// 스토리 조회 없이 지나간다.
        /// </summary>
        private InsectData PickStoryAssist(RegionSpawnInfo info)
        {
            System.Action<string, List<string>> provider = StoryCaptureTargetProvider;
            if (provider == null || string.IsNullOrEmpty(info.RegionId)) return null;

            float chance = Random.value;
            if (chance >= FieldSpawnRules.StoryAssistChance) return null;

            storyWanted.Clear();
            try
            {
                provider(info.RegionId, storyWanted);
            }
            catch (System.Exception e)
            {
                // 스토리 쪽 예외가 스폰을 멈추게 두지 않는다 — 보조만 빼고 평소대로 굴린다.
                if (!storyProviderWarned)
                {
                    storyProviderWarned = true;
                    Debug.LogWarning($"[InsectSpawner] 스토리 포획 보조 조회 실패 — 보조 없이 굴린다: {e.Message}");
                }
                return null;
            }
            if (storyWanted.Count == 0) return null;

            storyCandidateIds.Clear();
            for (int i = 0; i < regionCandidates.Count; i++) storyCandidateIds.Add(regionCandidates[i].insectId);
            storyAliveIds.Clear();
            population.CollectAliveIds(info.RegionId, storyAliveIds);

            string id = FieldSpawnRules.PickStoryTarget(storyWanted, storyCandidateIds, storyAliveIds, chance, Random.value);
            if (id == null) return null;
            for (int i = 0; i < regionCandidates.Count; i++)
                if (regionCandidates[i].insectId == id) return regionCandidates[i];
            return null;
        }

        // ======= 메인 필드 자리 — 리전 원판 · 높이(둔덕) · 물 · 콜라이더 =======
        //
        // 메인 필드 자리는 전부 TryPickFieldPosition 한 곳을 지난다(시작 채우기 · 재생 · 수명 순환 · 정화 복구).
        // 물 윤곽(호수·나루터 물가·습지 웅덩이·강 …)의 단일 출처는 RegionDressingBuilder의 물 판정 절이다 — 필드 아이템
        // (CaptureItemSpawner)도 같은 PickSpawnPosition을 지난다. 서브에리어는 PickSubAreaSlotPosition 주석.

        /// <summary>메인 필드 자리를 굴리는 최대 횟수 — 물·게이트·콜라이더·플레이어 근처·겹침을 거르고 남는 자리를 찾는다.</summary>
        internal const int FieldPositionAttempts = 12;

        /// <summary>
        /// 몸 자리 검사(발 위 높이·반경). 나무 줄기·건물 벽·바위·울타리 기둥 속이면 거른다 — 그 안에 선 곤충은
        /// 보이지도 잡히지도 않는다. 낮은 풀·돌턱(0.35m 아래)은 걸리지 않는다.
        /// </summary>
        private const float BodyProbeHeight = 0.8f;
        private const float BodyProbeRadius = 0.45f;

        private bool TryPickFieldPosition(RegionSpawnInfo info, float minPlayerDistance, bool nearPlayer,
            out Vector3 position)
        {
            position = default;
            bool hasPlayer = TryGetPlayerPosition(out Vector3 playerPos);
            float usableRadius = Mathf.Max(1f, info.Radius - FieldSpawnRules.RegionEdgeMargin);
            if (rollInRing == null) rollInRing = RollInRing;

            // 정화 직후처럼 "보여야 하는" 개체는 플레이어 둘레 고리에서 먼저 찾는다(플레이어가 그 리전 안일 때만).
            bool ring = nearPlayer && hasPlayer && PlanarDistance(playerPos, info.Center) < usableRadius;
            int attempts = ring ? FieldPositionAttempts * 2 : FieldPositionAttempts;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                if (ring && attempt < FieldPositionAttempts)
                {
                    rollCenter = playerPos;
                    rollInnerRadius = Mathf.Max(NearPlayerRingMin, minPlayerDistance);
                    rollOuterRadius = Mathf.Max(rollInnerRadius + 1f, NearPlayerRingMax);
                }
                else
                {
                    rollCenter = info.Center;
                    rollInnerRadius = 0f;
                    rollOuterRadius = usableRadius;
                }

                Vector3 p = PickSpawnPosition(rollInRing);
                if (PlanarDistance(p, info.Center) > usableRadius) continue;   // 고리가 리전 밖으로 나갔거나 물가로 밀려 나갔다
                if (IsInsideSubAreaGate(p, info.SubAreas)) continue;
                if (hasPlayer && minPlayerDistance > 0f && PlanarDistance(p, playerPos) < minPlayerDistance) continue;
                if (IsCrowded(info.RegionId, p)) continue;
                p.y = PlayerMovement.GroundHeight(ProbeColliderTop(p.x, p.y, p.z, 0.01f), p.x, p.z, false);
                if (IsBodyBlocked(p)) continue;
                position = p;
                return true;
            }
            return false;
        }

        /// <summary>고리(안 반경 0이면 원판)에서 넓이에 고르게 한 점 — 반지름을 제곱근으로 뽑아야 가운데로 몰리지 않는다.</summary>
        private Vector3 RollInRing()
        {
            float inner2 = rollInnerRadius * rollInnerRadius;
            float outer2 = rollOuterRadius * rollOuterRadius;
            float r = Mathf.Sqrt(Mathf.Lerp(inner2, outer2, Random.value));
            float a = Random.value * Mathf.PI * 2f;
            return rollCenter + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        /// <summary>
        /// 서브에리어 게이트 원 + 여유 안인가. 원 안의 곤충을 잡으러 들어가면 잡기 E와 진입 E가 한 프레임에 겹친다
        /// (부트스트랩 <c>PushOutOfSubAreas</c>가 스폰 포인트에 두던 것과 같은 마진).
        /// </summary>
        private static bool IsInsideSubAreaGate(Vector3 p, SubAreaData[] subAreas)
        {
            if (subAreas == null) return false;
            for (int i = 0; i < subAreas.Length; i++)
            {
                SubAreaData sub = subAreas[i];
                if (sub == null) continue;
                float safe = sub.radius + FieldSpawnRules.SubAreaGateMargin;
                if (PlanarSqr(p, sub.centerPosition) < safe * safe) return true;
            }
            return false;
        }

        /// <summary>같은 리전의 산 개체와 너무 붙는가 — 풀 더미가 겹쳐 한 덩어리로 보이지 않게.</summary>
        private bool IsCrowded(string key, Vector3 p)
        {
            List<FieldSlot> slots = population.SlotsOf(key);
            float min2 = FieldSpawnRules.MinSlotSpacing * FieldSpawnRules.MinSlotSpacing;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].IsAlive && PlanarSqr(slots[i].Position, p) < min2) return true;
            return false;
        }

        private bool IsBodyBlocked(Vector3 foot)
        {
            int n = Physics.OverlapSphereNonAlloc(foot + Vector3.up * BodyProbeHeight, BodyProbeRadius, bodyProbeHits,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = bodyProbeHits[i];
                if (c == null) continue;
                if (c.GetComponentInParent<InsectEntity>() != null) continue;
                if (c.GetComponentInParent<PlayerMovement>() != null) continue;
                // 얇은 판(바닥 평면·데크)은 설 자리를 막지 않는다 — 플레이어 이동 차단(IsBlockedPosition)과 같은 기준.
                if (c.bounds.size.y < 0.25f) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 물 위 후보를 버리고 다시 뽑는 최대 횟수. 원이 거의 다 물이면 끝까지 실패할 수 있어 상한을 두고,
        /// 그때는 <see cref="PushOutOfWater"/>로 물가에 올린다. 물가로 밀린 자리가 리전·게이트 밖이면
        /// <see cref="TryPickFieldPosition"/>이 그 후보를 버리고 원판에서 다시 굴린다.
        /// </summary>
        internal const int SpawnPositionRolls = 8;

        /// <summary>
        /// 물가에서 이 거리(m)까지는 물로 친다 — 곤충 발밑 풀 더미(반경 0.34~0.54m × 등급 배율 1.0~1.9)가
        /// 물가에 걸쳐 수면 위로 솟지 않게.
        /// </summary>
        internal const float WaterShoreMargin = 1.0f;

        /// <summary>물가로 밀어낼 때 한 걸음(m). 호수 끝(물가 + 여유)까지 수십 걸음이다.</summary>
        private const float ShoreStep = 0.5f;
        private const int MaxShoreSteps = 400;

        /// <summary>
        /// 발(<paramref name="footY"/>) 위 한 걸음(<see cref="PlayerMovement.StepHeight"/>)에서 아래로 쏴 맞은 콜라이더 윗면.
        /// 없으면 음의 무한대. 플레이어 접지 레이와 같은 출발 높이라 플레이어가 못 올라서는 것(나무 줄기·벽·지붕)은
        /// 레이가 그 안에서 출발해 안 맞는다. 트리거(NPC 몸통·아이템 줍기 구·포획 근접 구)는 땅이 아니다.
        /// </summary>
        private static float ProbeColliderTop(float x, float footY, float z, float depthBelowFoot)
        {
            float step = PlayerMovement.StepHeight;
            return Physics.Raycast(new Vector3(x, footY + step, z), Vector3.down, out RaycastHit hit,
                    step + depthBelowFoot, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : float.NegativeInfinity;
        }

        /// <summary>
        /// 순수 판정 — <paramref name="roll"/>(무작위 자리)을 물이 아닐 때까지 최대
        /// <see cref="SpawnPositionRolls"/>번 굴리고, 끝까지 물이면 마지막 후보를 물가로 밀어낸 뒤 그 자리 지면에 세운다.
        ///
        /// <b>y는 대입이다(<c>+=</c>가 아니다).</b> 둔덕은 콜라이더가 없어 평면 y에 선 곤충이 사구 속에 묻혔다(바닥 위
        /// 모래언덕 사구 최대 0.62m, 재 더미 1.05m). 옛 스폰 포인트는 플레이어 둘레로 끌려오며 플레이어 y를 복사해 두었는데
        /// 플레이어도 둔덕 높이만큼 올라서 있으므로, 거기에 둔덕 높이를 더하면 두 번 올랐다. 굴린 후보의 y는 쓰지 않는다.
        /// 평지에서는 <see cref="FieldGround.FloorY"/>(0.1)다.
        ///
        /// 물 곤충(소금쟁이 등)도 예외 없이 뭍에 둔다 — 데이터에 수면 서식 표시가 없고(<c>habitatHint</c>는 "연못" 같은
        /// 리전 문구, 물 속성은 모기·물방개까지 묶는다), 필드 곤충 연출이 기어다니는 종에 풀 더미를 둘러 세워
        /// 물 위에 두면 수면에서 풀이 솟는다. 물가 1m 밖에 서므로 여전히 "물가 곤충"으로 읽힌다.
        /// </summary>
        internal static Vector3 PickSpawnPosition(System.Func<Vector3> roll)
        {
            Vector3 p = roll();
            for (int i = 1; i < SpawnPositionRolls && IsOnWater(p); i++)
                p = roll();
            if (IsOnWater(p))
                p = PushOutOfWater(p);
            p.y = FieldGround.SurfaceY(p.x, p.z);
            return p;
        }

        /// <summary>
        /// 이 자리가 물 위인가(물가에서 <see cref="WaterShoreMargin"/>만큼 바깥까지 물로 친다). 윤곽은
        /// <see cref="RegionDressingBuilder.IsOnWater"/> — 호수·나루터 물가·습지 웅덩이·강·옛 원기둥 물 전부다.
        /// </summary>
        internal static bool IsOnWater(Vector3 p) => RegionDressingBuilder.IsOnWater(p, WaterShoreMargin);

        /// <summary>
        /// 물 밖(여유 포함)까지 한 방향으로 민다 — 처음 덮은 물의 중심에서 이 자리를 지나는 반직선(강이면 가까운 둑 쪽).
        /// 가장 가까운 물가는 아니어도 방향이 보존돼 굴린 자리 쪽 물가에 선다(<see cref="RegionDressingBuilder.PushOutOfWater"/>).
        /// </summary>
        internal static Vector3 PushOutOfWater(Vector3 p)
            => RegionDressingBuilder.PushOutOfWater(p, WaterShoreMargin, ShoreStep, MaxShoreSteps);

        // ======= 리전 표 =======

        /// <summary>
        /// 스폰 포인트(풀·레벨 대역)와 리전 정의(원판·게이트·귀환종)를 리전 단위로 묶는다. 한 번만 만든다.
        /// 서브에리어 포인트는 거른다 — 부모 리전 ID를 달고 있어 그대로 두면 전용종이 필드 풀에 섞인다.
        /// </summary>
        private bool BuildRegionInfos()
        {
            if (regionInfos.Count > 0) return true;
            if (database == null || spawnPoints == null || spawnPoints.Length == 0) return false;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnPoint point = spawnPoints[i];
                if (point == null || point.isSubAreaPoint) continue;
                string id = point.regionId ?? string.Empty;
                if (regionInfoById.ContainsKey(id)) continue;

                var info = new RegionSpawnInfo
                {
                    RegionId = id,
                    MinLevel = Mathf.Max(1, point.regionMinLevel),
                };
                info.MaxLevel = Mathf.Max(info.MinLevel, point.regionMaxLevel);

                if (point.regionInsectIds != null && point.regionInsectIds.Length > 0)
                {
                    for (int k = 0; k < point.regionInsectIds.Length; k++)
                    {
                        InsectData d = database.GetById(point.regionInsectIds[k]);
                        if (d != null && !info.Pool.Contains(d)) info.Pool.Add(d);
                    }
                }
                else if (database.insects != null)
                {
                    // 풀을 모르는 포인트(스폰 포인트 없이 뜬 씬의 자체 생성분) — 옛 규칙처럼 리전 필터 없이 DB 전체.
                    for (int k = 0; k < database.insects.Count; k++)
                        if (database.insects[k] != null) info.Pool.Add(database.insects[k]);
                }

                RegionData region = regionManager != null && id.Length > 0 ? regionManager.GetRegionById(id) : null;
                if (region != null)
                {
                    info.Data = region;
                    info.Center = region.centerPosition;
                    info.Radius = region.radius;
                    info.SubAreas = region.subAreas;
                }
                else
                {
                    EstimateDiscFromPoints(info);
                }
                info.BaseSlots = FieldSpawnRules.SlotCountFor(UsableArea(info));

                regionInfos.Add(info);
                regionInfoById[id] = info;
            }
            return regionInfos.Count > 0;
        }

        /// <summary>리전 정의가 없는 씬 — 그 리전 포인트들을 감싸는 원으로 어림한다.</summary>
        private void EstimateDiscFromPoints(RegionSpawnInfo info)
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnPoint p = spawnPoints[i];
                if (p == null || p.isSubAreaPoint || (p.regionId ?? string.Empty) != info.RegionId) continue;
                sum += p.transform.position;
                n++;
            }
            info.Center = n > 0 ? sum / n : Vector3.zero;
            float r = 0f;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnPoint p = spawnPoints[i];
                if (p == null || p.isSubAreaPoint || (p.regionId ?? string.Empty) != info.RegionId) continue;
                r = Mathf.Max(r, PlanarDistance(p.transform.position, info.Center));
            }
            info.Radius = Mathf.Max(15f, r + 5f);
        }

        /// <summary>
        /// 곤충이 설 수 있는 땅 넓이(㎡) — 리전 원판(가장자리 여유 제외)에서 물과 서브에리어 게이트를 뺀 몫.
        /// 해바라기 씨 배열(Vogel 나선)로 고르게 찍어 잰다 — 무작위로 재면 실행마다 슬롯 수가 달라진다.
        /// 물 위 자리까지 셈에 넣으면 호수가 큰 리전(연못)은 뭍의 밀도만 두 배가 된다.
        /// </summary>
        private const int AreaSamples = 256;

        private static float UsableArea(RegionSpawnInfo info)
        {
            float r = Mathf.Max(1f, info.Radius - FieldSpawnRules.RegionEdgeMargin);
            const float goldenAngle = 2.39996323f;
            int land = 0;
            for (int i = 0; i < AreaSamples; i++)
            {
                float rr = r * Mathf.Sqrt((i + 0.5f) / AreaSamples);
                float a = i * goldenAngle;
                Vector3 p = info.Center + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                if (IsOnWater(p) || IsInsideSubAreaGate(p, info.SubAreas)) continue;
                land++;
            }
            return Mathf.PI * r * r * land / AreaSamples;
        }

        /// <summary>메인 필드 슬롯 수를 오염 상한에 맞춘다 — 슬롯 수를 바꾸는 유일한 길(서브에리어 제외).</summary>
        private void ReconcileRegionSlots(RegionSpawnInfo info, float now)
        {
            population.EnsureSlotCount(info.RegionId, RegionCap(info), now, isSubArea: false);
        }

        /// <summary>
        /// 이 리전의 슬롯 수. 기본은 설 수 있는 땅 × 밀도이고, 오염 거점이 살아 있으면 줄어든다.
        ///
        /// <b>0으로 내려가지 않는 것이 중요하다</b> — 그 리전에서의 포획·전투를 조건으로 건
        /// 스토리 비트가 여럿이라(오염 아크 둘 + 1막의 특정 종 포획) 곤충이 아예 안 뜨면
        /// 발화 지점에 영영 도달하지 못한다. 하한은 <c>BlightPolicy.MinActive</c>가 든다.
        /// </summary>
        private int RegionCap(RegionSpawnInfo info)
        {
            bool blighted = blight != null && blight.IsBlighted(info.RegionId);
            return Core.BlightPolicy.MaxActiveFor(blighted, info.BaseSlots);
        }

        // ======= 몸 — 세우기 · 거두기 · 게임플레이 퇴장 =======

        /// <summary>기록된 개체 그대로 몸을 세운다(종·레벨·색다름·지워짐·자리). 굴리는 것은 없다.</summary>
        private bool Materialize(FieldSlot slot, string homeRegionId)
        {
            InsectData data = slot.Data != null ? slot.Data : (database != null ? database.GetById(slot.InsectId) : null);
            if (data == null)
            {
                FieldPopulation.Vacate(slot, Time.time + FieldSpawnRules.RetrySeconds);
                return false;
            }
            slot.Data = data;

            TryInitPool();
            GameObject prefab = data.prefabOverride != null ? data.prefabOverride : defaultPrefab;
            if (prefab == null) return false;

            GameObject instance = pool != null && prefab == defaultPrefab ? pool.Get() : Instantiate(prefab, transform);
            instance.SetActive(true);
            // Initialize 전이어야 한다 — Initialize가 이 좌표를 basePosition(배회·풀 더미의 기준)으로 삼는다.
            instance.transform.position = slot.Position;

            InsectEntity entity = instance.GetComponent<InsectEntity>();
            if (entity == null) entity = instance.AddComponent<InsectEntity>();
            entity.Initialize(data, slot.Level, homeRegionId, DespawnEntity, slot.Shiny, slot.Erased);

            slot.Entity = entity;
            slotByEntity[entity] = slot;
            activeInsects.Add(entity);

            if (data.rarity == InsectRarity.Epic || data.rarity == InsectRarity.Legendary)
            {
                RaidBossSpawned?.Invoke(entity);
            }
            return true;
        }

        /// <summary>
        /// 몸만 풀로 돌린다 — <b>개체는 그대로 남는다</b>(<see cref="FieldPopulation.MarkRecalled"/>).
        /// 게임플레이 퇴장 콜백(<see cref="DespawnEntity"/>)을 거치지 않으므로 자리가 비지 않는다.
        /// </summary>
        private void RecallSlot(FieldSlot slot)
        {
            InsectEntity e = slot.Entity;
            FieldPopulation.MarkRecalled(slot);
            if (ReferenceEquals(e, null)) return;
            slotByEntity.Remove(e);
            activeInsects.Remove(e);
            if (e != null && e.Recall()) ReturnToPool(e);
        }

        /// <summary>
        /// 서 있는 몸을 전부 거둔다(서브에리어 진입·이탈) — 기록은 남는다. 메인 필드 곤충은 돌아오면 같은 자리에,
        /// 서브에리어 곤충은 다시 들어오면 같은 방에 선다.
        /// </summary>
        private void RecallAllBodies()
        {
            for (int i = activeInsects.Count - 1; i >= 0; i--)
            {
                if (i >= activeInsects.Count) continue;
                InsectEntity e = activeInsects[i];
                if (!ReferenceEquals(e, null) && slotByEntity.TryGetValue(e, out FieldSlot slot))
                {
                    RecallSlot(slot);
                    continue;
                }
                activeInsects.RemoveAt(i);
                if (e != null && e.Recall()) ReturnToPool(e);
            }
        }

        /// <summary>
        /// 게임플레이 퇴장(포획 성공·실패, 전투 승패·도주 종료, 레이드, 아이 NPC 가로채기) — 단 필드의 놓침 도주는
        /// 퇴장이 아니라 이동이다(같은 개체를 눈 밖 다른 자리로 — <see cref="FieldPopulation.RelocateAfterFlee"/>).
        /// <see cref="InsectEntity.Despawn"/>의 콜백이다. 그 자리는 비고 재생 지연 뒤에 새로 찬다
        /// (필드 1~2분, 서브에리어 <c>subAreaRespawnSeconds</c>).
        /// </summary>
        private void DespawnEntity(InsectEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            activeInsects.Remove(entity);
            if (slotByEntity.TryGetValue(entity, out FieldSlot slot))
            {
                slotByEntity.Remove(entity);
                if (ReferenceEquals(slot.Entity, entity) && entity.Fled && !slot.IsSubArea
                    && regionInfoById.TryGetValue(slot.Key, out RegionSpawnInfo fledInfo)
                    && TryPickFieldPosition(fledInfo, FieldSpawnRules.SpawnMinPlayerDistance, false, out Vector3 away))
                {
                    // 달아났다 — 같은 개체가 플레이어 눈 밖 다른 자리로 간다(FieldPopulation.RelocateAfterFlee 주석).
                    FieldPopulation.RelocateAfterFlee(slot, away);
                }
                else if (ReferenceEquals(slot.Entity, entity))
                {
                    float delay = slot.IsSubArea
                        ? subAreaRespawnSeconds
                        : FieldSpawnRules.RollRespawnDelay(Random.value);
                    FieldPopulation.MarkRemovedByGameplay(slot, Time.time, delay);
                }
            }

            ReturnToPool(entity);
        }

        private void ReturnToPool(InsectEntity entity)
        {
            if (entity == null) return;
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
                InsectEntity e = activeInsects[i];
                if (e != null && e.gameObject.activeInHierarchy) continue;
                activeInsects.RemoveAt(i);
                // 파기·비활성으로 몸을 잃었다 — 개체는 그대로 두고 몸만 뗀다(다음 틱에 다시 선다).
                if (!ReferenceEquals(e, null) && slotByEntity.TryGetValue(e, out FieldSlot slot))
                {
                    slotByEntity.Remove(e);
                    if (ReferenceEquals(slot.Entity, e)) FieldPopulation.MarkRecalled(slot);
                }
            }
        }

        /// <summary>포획·전투에 붙잡혔거나 플레이어를 알아챈(경계·도주) 몸 — 거두지도, 수명으로 바꾸지도 않는다.</summary>
        private static bool IsBusy(InsectEntity e) => e != null && (e.IsEngaged || e.IsAlerted);

        // 옛은 GetPlayerTransform이 매 호출 GameObject.FindWithTag + GameObject.Find — lazy 캐싱으로 첫 1회 후 재사용.
        private Transform cachedPlayerTransformForSpawner;

        private Transform GetPlayerTransform()
        {
            if (cachedPlayerTransformForSpawner != null) return cachedPlayerTransformForSpawner;
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null) playerObj = GameObject.Find("Player");
            if (playerObj != null) cachedPlayerTransformForSpawner = playerObj.transform;
            return cachedPlayerTransformForSpawner;
        }

        private bool TryGetPlayerPosition(out Vector3 position)
        {
            Transform player = GetPlayerTransform();
            position = player != null ? player.position : Vector3.zero;
            return player != null;
        }

        private static float PlanarSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b) => Mathf.Sqrt(PlanarSqr(a, b));

        // ======= 배선 =======

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
        /// 오염 거점 상태 — 거점이 살아 있는 리전은 슬롯 수를 줄이고, 무너지면 되돌린다.
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
            // 꺼지면 복구 코루틴도 멈춘다 — 보류만 남으면 그 리전 슬롯이 영영 안 는다. 다시 켜진 첫 틱이 채운다.
            foreach (string r in cleanseHold)
                if (!pendingCleansed.Contains(r)) pendingCleansed.Add(r);
        }

        // ======= 정화 복구 — 이동이 아니라 사건이라 즉시 채운다 =======

        /// <summary>
        /// 거점이 무너졌다 — 그 리전의 곤충을 즉시 되돌린다.
        ///
        /// 다음 틱의 재생을 기다리면 빈 들판이 남아 "돌아왔다"가 안 읽힌다. 귀환종을 먼저 한 자리에 확정하는 것도
        /// 같은 이유다 — 무작위에 맡기면 정화 직후 화면에 흔한 종만 뜰 수 있다.
        /// </summary>
        private void OnRegionCleansed(string regionId)
        {
            if (!isActiveAndEnabled || string.IsNullOrEmpty(regionId)) return;
            cleanseHold.Add(regionId);   // 이 프레임부터 평소 틱이 늘어난 상한을 먼저 채우지 않게
            StartCoroutine(RepopulateCleansedRegion(regionId));
        }

        private System.Collections.IEnumerator RepopulateCleansedRegion(string regionId)
        {
            // 정화 연출·컷신이 카메라를 잡고 있는 동안 곤충이 튀어나오면 화면이 어수선하다.
            // 한 박자 뒤에 채운다.
            yield return new WaitForSeconds(RepopulateDelaySeconds);
            if (blight == null || blight.IsBlighted(regionId))   // 그새 상태가 바뀌었다
            {
                cleanseHold.Remove(regionId);
                yield break;
            }

            // 그 사이 서브에리어로 들어갔거나 첫 채우기 전이면 메인 월드 콜라이더로 자리를 못 잰다(HideMainWorld가
            // 숨겼다) — 메인 필드로 나오는 첫 틱에 채운다(TickField). 버리면 늘어난 자리가 평소 재생으로만 차서
            // 귀환종이 확정되지 않는다.
            if (currentSubArea != null || !fieldReady)
            {
                if (!pendingCleansed.Contains(regionId)) pendingCleansed.Add(regionId);
                yield break;
            }
            RepopulateCleansed(regionId);
        }

        private void RepopulateCleansed(string regionId)
        {
            cleanseHold.Remove(regionId ?? string.Empty);
            if (!regionInfoById.TryGetValue(regionId ?? string.Empty, out RegionSpawnInfo info)) return;
            float now = Time.time;
            ReconcileRegionSlots(info, now);   // 상한이 늘어난 만큼 빈 자리가 생긴다
            List<FieldSlot> slots = population.SlotsOf(info.RegionId);
            int near = 0;

            // 귀환종 먼저 — 한 자리에 확정. 월드 상태(시간·날씨) 필터를 거치지 않는다: 정화는 그 자리에서 눈으로
            // 확인해야 하는 사건이라, 밤이라서 혹은 비가 와서 안 나오면 연출이 통째로 죽는다. 리전 풀 밖의 종은
            // 띄우지 않는다(blight_lint 4가 귀환종 ∈ 리전 풀을 고정한다).
            InsectData returning = ReturningSpecies(info);
            if (returning != null && !population.HasAlive(info.RegionId, returning.insectId))
            {
                FieldSlot target = FindSlotForReturning(slots);
                if (target != null)
                {
                    RecallSlot(target);
                    FieldPopulation.Vacate(target, now);
                    if (RollFieldSlot(target, info, now, FieldSpawnRules.CleanseSpawnMinPlayerDistance,
                            FieldSpawnRules.RollLifetime(Random.value), returning, nearPlayer: true))
                        near++;
                }
            }

            // 나머지 빈 자리도 지금 채운다. 앞 몇 자리만 플레이어 둘레에 — 전부 몰면 오염 전보다 붐빈다.
            for (int i = 0; i < slots.Count; i++)
            {
                FieldSlot slot = slots[i];
                if (!FieldPopulation.IsDueForRoll(slot, now)) continue;
                bool nearPlayer = near < RepopulateNearCount;
                if (RollFieldSlot(slot, info, now, FieldSpawnRules.CleanseSpawnMinPlayerDistance,
                        FieldSpawnRules.RollLifetime(Random.value), null, nearPlayer) && nearPlayer)
                    near++;
            }

            // 가까우면 바로 선다.
            if (TryGetPlayerPosition(out Vector3 playerPos)) SyncFieldBodies(playerPos);
        }

        private static InsectData ReturningSpecies(RegionSpawnInfo info)
        {
            string id = info.Data != null ? info.Data.blightReturningInsectId : null;
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < info.Pool.Count; i++)
                if (info.Pool[i].insectId == id) return info.Pool[i];
            return null;
        }

        /// <summary>귀환종을 들일 자리 — 빈 자리가 먼저, 없으면 몸 없이 멀리 있는 산 자리(눈앞의 곤충은 바꾸지 않는다).</summary>
        private static FieldSlot FindSlotForReturning(List<FieldSlot> slots)
        {
            FieldSlot fallback = null;
            for (int i = 0; i < slots.Count; i++)
            {
                FieldSlot s = slots[i];
                if (!s.IsAlive) return s;
                if (fallback == null && !s.IsMaterialized) fallback = s;
            }
            return fallback;
        }

        // ======= 서브에리어 — 같은 원칙(기록이 개체), 슬롯마다 재생 =======

        private void OnSubAreaChanged(SubAreaData subArea)
        {
            currentSubArea = subArea;
            currentSubAreaKey = subArea != null ? SubAreaKey(subArea) : null;
            subAreaAnchored = false;

            // 진입이든 이탈이든 서 있는 몸은 전부 거둔다 — 기록은 남는다. 진입 땐 메인 월드가 SetActive(false)로
            // 숨겨지고, 이탈 땐 방이 파기되므로 어느 쪽이든 남겨 두면 허공에 뜬 몸이 된다.
            RecallAllBodies();

            if (subArea != null && HasExclusives(subArea))
            {
                // SubAreaWorldBuilder.EnterSubArea가 같은 이벤트에서 player를 방 입구로 텔레포트하지만 구독 순서상
                // 이쪽이 먼저 불린다. 한 프레임 뒤에 앵커를 잡아야 곤충과 player가 같은 공간에 선다.
                StartCoroutine(EnterSubAreaDelayed(subArea));
            }
        }

        private System.Collections.IEnumerator EnterSubAreaDelayed(SubAreaData subArea)
        {
            yield return null;
            // currentSubArea가 그새 바뀌었다면(빠른 Exit) 스킵
            if (currentSubArea != subArea) yield break;
            Transform pTrans = GetPlayerTransform();
            subAreaAnchor = pTrans != null ? pTrans.position : subArea.centerPosition;
            subAreaAnchored = true;
            RefreshSubArea(subArea, Time.time, true);
        }

        private void TickSubArea()
        {
            if (!subAreaAnchored || currentSubArea == null || !HasExclusives(currentSubArea)) return;
            RefreshSubArea(currentSubArea, Time.time, false);
        }

        /// <summary>
        /// 방의 슬롯을 돌본다 — 빈 자리가 시간이 되면 채우고, 산 개체에 몸을 세운다. 나갔다 다시 들어오면
        /// <b>같은 개체가 같은 자리에</b> 선다(방은 늘 (2000,·,2000)에 지어지므로 월드 좌표 그대로 기록한다).
        /// 옛 규칙은 들어올 때마다 새로 굴렸고, 방 안이 "0마리가 된 뒤 45초"를 기다렸다 — 이제 자리마다 따로 45초다.
        /// </summary>
        private void RefreshSubArea(SubAreaData sub, float now, bool entering)
        {
            string key = currentSubAreaKey ?? SubAreaKey(sub);
            int count = FieldSpawnRules.SubAreaSlotCount(sub.exclusiveInsectIds.Length, subAreaActiveCount);
            population.EnsureSlotCount(key, count, now, isSubArea: true);
            List<FieldSlot> slots = population.SlotsOf(key);
            for (int i = 0; i < slots.Count; i++)
            {
                FieldSlot slot = slots[i];
                if (FieldPopulation.IsDueForRoll(slot, now))
                {
                    RollSubAreaSlot(slot, sub, slots.Count, now);
                }
                else if (entering && slot.IsAlive && !slot.IsMaterialized
                         && !IsInsideSubAreaRoom(subAreaAnchor, slot.Position))
                {
                    // 방이 달라졌다(동굴 미로는 들어올 때마다 새로 짓는다) — 개체는 그대로, 자리만 다시 잡는다.
                    slot.Position = PickSubAreaSlotPosition(sub);
                }

                if (slot.IsAlive && !slot.IsMaterialized && activeInsects.Count < maxActiveTotal)
                    Materialize(slot, string.Empty);
            }
        }

        private void RollSubAreaSlot(FieldSlot slot, SubAreaData sub, int slotCount, float now)
        {
            string id = FieldSpawnRules.SubAreaSpecies(sub.exclusiveInsectIds, slot.Index, slotCount, slot.Generation + 1);
            InsectData data = database != null ? database.GetById(id) : null;
            if (data == null)
            {
                // 전용종 ID가 DB에 없다 — 다음 차례 종으로 넘어가게 세대를 올려 두고 잠시 뒤 다시.
                slot.Generation++;
                FieldPopulation.Vacate(slot, now + FieldSpawnRules.RetrySeconds);
                return;
            }

            int level = Random.Range(sub.minLevel, sub.maxLevel + 1);
            bool shiny = Random.value < InsectEntity.FieldShinyChance;
            // 서브에리어 전용종은 시간대와 무관하다 — 수명 없이 잡히거나 놓칠 때까지 산다.
            FieldPopulation.Fill(slot, data.insectId, data, level, shiny, false, PickSubAreaSlotPosition(sub),
                float.PositiveInfinity);
        }

        private static bool HasExclusives(SubAreaData sub)
            => sub != null && sub.exclusiveInsectIds != null && sub.exclusiveInsectIds.Length > 0;

        // 리전 ID와 겹치지 않게 접두사를 붙인다 — 슬롯 기록부는 메인 필드와 서브에리어를 한 사전에 든다.
        private static string SubAreaKey(SubAreaData sub) => "sub:" + (sub != null ? sub.subAreaId : string.Empty);

        /// <summary>새로 차는 서브에리어 곤충이 플레이어 바로 옆에 생기지 않게 두는 거리(m) — 방이 작아 필드보다 짧다.</summary>
        private const float SubAreaMinPlayerDistance = 4f;
        private const int SubAreaPlayerGapAttempts = 4;

        /// <summary>
        /// 서브에리어 슬롯 자리 — 진입 앵커 둘레(0.6R)에서 방 안만 받는다(<see cref="PickSubAreaSpawnPosition"/>).
        /// 높이는 플레이어 접지와 같은 규칙(서브에리어판 — 콜라이더만)으로 그 자리 바닥에서 잰다. 메인 필드 규칙의
        /// 둔덕·호수(PickSpawnPosition)는 걸지 않는다: (2000,·,2000) 너머라 둘 다 없고 바닥도 FloorY(0.1)가 아니라 y=0이다.
        /// 플레이어 y를 쓰지 않는 이유 — 진입 1프레임 뒤라 플레이어가 입구 텔레포트의 +0.5에 떠 있을 수 있다(얼어 있으면
        /// 접지를 안 돈다). 레이 깊이 2m는 플레이어 접지 레이(groundCheckDistance)와 같다. 방 안 판정이 같은 레이로 바닥을
        /// 이미 확인했다 — 안 잡힐 수 있는 건 당기기까지 실패한 마지막 안전망(앵커 자리)뿐이고, 그때는 앵커 y 그대로 둔다.
        /// </summary>
        private Vector3 PickSubAreaSlotPosition(SubAreaData sub)
        {
            Vector3 anchor = subAreaAnchor;
            bool hasPlayer = TryGetPlayerPosition(out Vector3 playerPos);
            Vector3 spawnPos = anchor;
            for (int attempt = 0; attempt < SubAreaPlayerGapAttempts; attempt++)
            {
                spawnPos = PickSubAreaSpawnPosition(anchor, sub.radius * 0.6f);
                if (!hasPlayer || PlanarDistance(spawnPos, playerPos) >= SubAreaMinPlayerDistance) break;
            }

            float floorTop = ProbeColliderTop(spawnPos.x, anchor.y, spawnPos.z, 2f);
            if (!float.IsNegativeInfinity(floorTop))
                spawnPos.y = PlayerMovement.GroundHeight(floorTop, spawnPos.x, spawnPos.z, true);
            return spawnPos;
        }

        // ======= 서브에리어 스폰 자리 — 방 안쪽 =======
        //
        // 서브에리어는 벽으로 봉한 방이다(SubAreaWorldBuilder.CreateBoundaryWalls, 반쪽 크기 9~16m — 테마마다 다르다). 플레이어는
        // 남쪽 입구(방 로컬 z −8)에 서는데 옛 스폰은 그 둘레 반경 0.6R(월드 배율 뒤 9~10.8m)에 뿌려서, 남쪽 벽(−9~−16) 너머와
        // 바닥 판 밖까지 나갔다 — 벽 속·허공에 뜬 곤충은 보이지도 잡히지도 않는다.
        //
        // 방 크기를 여기 다시 적지 않는다. 그 값은 빌더에 private으로만 있고(읽기 전용 조회 없음) story_lint 21이 소스에서
        // 같은 값을 읽는다 — 사본을 두면 벽을 옮길 때 조용히 어긋난다. 대신 지어진 방에 직접 묻는다: ①후보 발밑에 바닥이
        // 있고 ②플레이어 눈높이에서 후보까지 벽이 없으면 방 안이다. 벽 두께(1m) 속이나 벽 너머면 선이 벽 안쪽 면에 막히고,
        // 바닥 판 밖이면 바닥 레이가 허공을 친다. 미로·기둥 뒤도 같은 이유로 버려진다(플레이어가 볼 수 있는 자리에만 선다).

        /// <summary>서브에리어 자리를 굴리는 최대 횟수 — 방 밖 후보는 버리고 다시 뽑는다.</summary>
        internal const int SubAreaSpawnRolls = 12;

        /// <summary>끝까지 방 밖이면 마지막 후보를 플레이어 쪽으로 반씩 당기는 횟수(1/8까지).</summary>
        internal const int SubAreaPullSteps = 3;

        /// <summary>
        /// 시야 검사 높이(발 위 m). 바위·그루터기 같은 낮은 소품은 넘겨 보고, 벽(3~7m)·기둥·미로 벽에는 막힌다.
        /// 진입 직후 플레이어는 입구 텔레포트의 +0.5에 떠 있을 수 있어 선은 바닥 위 0.9~1.4m를 지난다.
        /// </summary>
        private const float SubAreaSightHeight = 0.9f;

        // NonAlloc 결과는 거리순이 아니다 — 버퍼가 차면 벽이 빠졌을 수 있어 방 밖으로 친다.
        private readonly RaycastHit[] subAreaSightHits = new RaycastHit[32];

        private Vector3 PickSubAreaSpawnPosition(Vector3 anchor, float radius)
        {
            return PickContainedPosition(
                () =>
                {
                    Vector2 o = Random.insideUnitCircle * radius;
                    return anchor + new Vector3(o.x, 0f, o.y);
                },
                anchor, p => IsInsideSubAreaRoom(anchor, p), SubAreaSpawnRolls);
        }

        /// <summary>
        /// 순수 판정 — <paramref name="roll"/>을 <paramref name="inside"/>가 참일 때까지 최대 <paramref name="rolls"/>번 굴린다.
        /// 끝까지 거짓이면 마지막 후보를 <paramref name="anchor"/> 쪽으로 반씩 당겨(<see cref="SubAreaPullSteps"/>번) 들어오는
        /// 첫 자리를 쓴다. 플레이어는 방 안에 서 있으므로 당길수록 들어온다. 그래도 밖이면 플레이어 자리다 —
        /// <b>스폰을 건너뛰지 않는다</b>(방이 작아 자리 하나 못 구해 슬롯이 비면 그 방 곤충이 눈에 띄게 준다). y는 후보 것 그대로다.
        /// </summary>
        internal static Vector3 PickContainedPosition(System.Func<Vector3> roll, Vector3 anchor,
            System.Func<Vector3, bool> inside, int rolls)
        {
            Vector3 last = anchor;
            for (int i = 0; i < rolls; i++)
            {
                last = roll();
                if (inside(last)) return last;
            }
            for (int k = 0; k < SubAreaPullSteps; k++)
            {
                last = new Vector3((anchor.x + last.x) * 0.5f, last.y, (anchor.z + last.z) * 0.5f);
                if (inside(last)) return last;
            }
            return new Vector3(anchor.x, last.y, anchor.z);
        }

        private bool IsInsideSubAreaRoom(Vector3 anchor, Vector3 p)
        {
            // ① 발밑 바닥 — 바닥 판 밖(방 너머의 허공)이면 아무것도 안 맞는다. 높이 규칙과 같은 레이다(아래 floorTop).
            if (float.IsNegativeInfinity(ProbeColliderTop(p.x, anchor.y, p.z, 2f))) return false;

            // ② 플레이어 눈높이 → 후보. 트리거(입구·출구 구)는 벽이 아니고, 먼저 놓은 곤충·플레이어 몸도 벽이 아니다.
            Vector3 from = anchor + Vector3.up * SubAreaSightHeight;
            Vector3 to = new Vector3(p.x, from.y, p.z);
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1e-3f) return true;
            int n = Physics.RaycastNonAlloc(from, d / len, subAreaSightHits, len,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (n >= subAreaSightHits.Length) return false;
            for (int i = 0; i < n; i++)
            {
                Collider c = subAreaSightHits[i].collider;
                if (c == null) continue;
                if (c.GetComponentInParent<InsectEntity>() != null) continue;
                if (c.GetComponentInParent<PlayerMovement>() != null) continue;
                return false;
            }
            return true;
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

            // 리전 곤충 수는 프로파일이 아니라 리전의 설 수 있는 땅이 정한다(FieldSpawnRules.SlotCountFor).
            maxActiveTotal = Mathf.Max(1, profile.maxActiveTotal);
            subAreaActiveCount = Mathf.Max(1, profile.subAreaActiveCount);
            subAreaRespawnSeconds = Mathf.Max(5f, profile.subAreaRespawnSeconds);
        }
    }
}
