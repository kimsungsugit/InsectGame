using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.NPC
{
    /// <summary>
    /// NPC 중앙 관리자 — 스폰/틱/곤충 예약 조정.
    /// - 개별 NPC Update 금지: 여기서 라운드로빈 TickAI(프레임당 최대 3명) + 40m 이내 TickMovement.
    /// - 예약(HashSet)으로 두 아이가 같은 곤충을 쫓지 않게 조정. Despawn 잔존 예약은 주기 sweep.
    /// - PlaySceneBootstrap이 AutoWire → SpawnFromAnchors → ApplyTuning 순서로 연결(역순 호출도 안전).
    /// </summary>
    public class NpcManager : MonoBehaviour
    {
        private const int AiTicksPerFrame = 3;
        private const float MovementRadius = 40f;
        private const float MovementRadiusSq = MovementRadius * MovementRadius;
        private const float ReservationSweepInterval = 5f;

        [Header("Tuning (GameplayTuningProfile.ApplyTuning으로 갱신)")]
        // 저작된 앵커 수와 맞춘다. 앵커보다 작으면 SyncSpawns가 앞에서부터 잘라 뒤쪽이 버려지는데,
        // VillageBuilder는 본마을(주민 8)을 먼저 넣고 전초기지를 리전 순서로 뒤에 붙인다
        // — 캡이 10이면 swamp/mountain/garden/ruins 전초기지 4곳이 오두막·모닥불만 있고
        // 주민이 영구히 0명이 된다(잡기 아이도 mountain 몫이 누락).
        // 앵커: 주민 14(본마을 8 + 전초기지 6) / 잡기 아이 7(meadow 2 + KidSpots 5).
        [SerializeField] private int villagerCount = 14;
        [SerializeField] private int catcherKidCount = 7;
        [SerializeField] private float kidCatchCooldownSeconds = NpcCatchRules.DefaultCatchCooldownSeconds;

        private InsectSpawner spawner;
        private RegionManager regionManager;
        private Transform playerTransform;
        private Vector3 playerPos;       // 프레임당 1회 갱신 캐시
        private bool hasPlayer;

        private readonly List<NpcSpawnAnchor> villagerAnchors = new List<NpcSpawnAnchor>();
        private readonly List<NpcSpawnAnchor> kidAnchors = new List<NpcSpawnAnchor>();
        private readonly List<VillagerNpc> villagers = new List<VillagerNpc>();
        private readonly List<CatcherKidNpc> kids = new List<CatcherKidNpc>();
        private bool anchorsReceived;

        private readonly HashSet<InsectEntity> reservedInsects = new HashSet<InsectEntity>();
        // sweep 조건: 파괴/비활성(despawn 후 풀 반환) 엔티티 — 캡처 없는 정적 람다로 할당 회피
        private static readonly System.Predicate<InsectEntity> DeadReservation =
            e => e == null || !e.gameObject.activeInHierarchy;

        private static readonly IReadOnlyList<InsectEntity> EmptyInsects = new List<InsectEntity>();

        private int tickIndex;
        private float sweepTimer = ReservationSweepInterval;

        public IReadOnlyList<VillagerNpc> Villagers => villagers;

        /// <summary>리전 진행 상태 참조 (향후 리전별 NPC 행동 분기용 — CS0414 방지 겸 공개).</summary>
        public RegionManager Region => regionManager;

        /// <summary>프레임당 1회 캐시된 플레이어 참조 (AutoWire 주입 — Find 미사용).</summary>
        public Transform PlayerTransform => playerTransform;

        /// <summary>아이 포획 쿨다운(초) — CatcherKidNpc가 읽음.</summary>
        public float KidCatchCooldownSeconds => kidCatchCooldownSeconds;

        /// <summary>스포너의 활성 곤충 목록 — 아이 스캔용 (FindObjectsByType 대체).</summary>
        public IReadOnlyList<InsectEntity> ActiveInsects =>
            spawner != null ? spawner.ActiveInsects : EmptyInsects;

        public void AutoWire(InsectSpawner insectSpawner, RegionManager region, Transform player)
        {
            if (spawner == null) spawner = insectSpawner;
            if (regionManager == null) regionManager = region;
            if (playerTransform == null)
            {
                playerTransform = player;
                // 앵커 스폰이 먼저 일어났다면(호출 순서 방어) 컬링 타깃 사후 연결
                if (player != null && (villagers.Count > 0 || kids.Count > 0))
                    RewireCullingTargets();
            }
        }

        /// <summary>월드 좌표와 플레이어 사이 거리 — 플레이어 없으면 무한대(아이 규칙: 제약 없음 취급).</summary>
        public float DistanceFromPlayer(Vector3 worldPos)
        {
            if (!hasPlayer) return float.MaxValue;
            return Vector3.Distance(playerPos, worldPos);
        }

        // ── 곤충 예약 (아이 간 중복 추적 방지) ──

        public bool IsReserved(InsectEntity insect)
        {
            return insect != null && reservedInsects.Contains(insect);
        }

        public bool TryReserveInsect(InsectEntity insect)
        {
            if (insect == null) return false;
            return reservedInsects.Add(insect);
        }

        public void ReleaseInsect(InsectEntity insect)
        {
            if (insect == null) return;
            reservedInsects.Remove(insect);
        }

        // ── 스폰 ──

        /// <summary>VillageBuilder 결과 앵커로 NPC 스폰. 튜닝 수만큼 앵커 앞에서부터 사용.</summary>
        public void SpawnFromAnchors(List<NpcSpawnAnchor> anchors)
        {
            if (anchors == null) return;

            villagerAnchors.Clear();
            kidAnchors.Clear();
            for (int i = 0; i < anchors.Count; i++)
            {
                NpcSpawnAnchor a = anchors[i];
                if (a == null) continue;
                if (a.kind == NpcKind.Villager) villagerAnchors.Add(a);
                else if (a.kind == NpcKind.CatcherKid) kidAnchors.Add(a);
            }
            anchorsReceived = true;
            SyncSpawns();
        }

        /// <summary>튜닝 프로필 반영. SpawnFromAnchors 이후 호출되어도 수 증감을 동기화.</summary>
        public void ApplyTuning(GameplayTuningProfile profile)
        {
            if (profile == null) return;
            villagerCount = profile.villagerCount;
            catcherKidCount = profile.catcherKidCount;
            kidCatchCooldownSeconds = profile.kidCatchCooldownSeconds;
            if (anchorsReceived) SyncSpawns();
        }

        // 스폰 수 동기화: 이미 스폰된 NPC는 activeSelf 토글, 부족분은 여분 앵커에서 추가 스폰.
        private void SyncSpawns()
        {
            int villagerTarget = Mathf.Min(villagerCount, villagerAnchors.Count);
            while (villagers.Count < villagerTarget)
            {
                int i = villagers.Count;
                villagers.Add(SpawnVillager(villagerAnchors[i], i));
            }
            for (int i = 0; i < villagers.Count; i++)
            {
                if (villagers[i] != null && villagers[i].gameObject.activeSelf != (i < villagerTarget))
                    villagers[i].gameObject.SetActive(i < villagerTarget);
            }

            int kidTarget = Mathf.Min(catcherKidCount, kidAnchors.Count);
            while (kids.Count < kidTarget)
            {
                int i = kids.Count;
                kids.Add(SpawnKid(kidAnchors[i], i));
            }
            for (int i = 0; i < kids.Count; i++)
            {
                if (kids[i] != null && kids[i].gameObject.activeSelf != (i < kidTarget))
                    kids[i].gameObject.SetActive(i < kidTarget);
            }
        }

        private VillagerNpc SpawnVillager(NpcSpawnAnchor anchor, int index)
        {
            string npcId = $"villager_{anchor.regionId}_{index}";
            int seed = NpcDialogueDatabase.StableHash(npcId);

            GameObject go = CreateNpcObject($"Npc_{npcId}", anchor.position);
            NpcVisualBuilder.Build(go.transform, NpcVisualBuilder.RandomVillager(seed));

            VillagerNpc npc = go.AddComponent<VillagerNpc>();
            npc.Initialize(anchor, npcId, NpcDialogueDatabase.GetVillagerName(seed), seed);
            AttachCulling(go);
            return npc;
        }

        private CatcherKidNpc SpawnKid(NpcSpawnAnchor anchor, int index)
        {
            string npcId = $"kid_{anchor.regionId}_{index}";
            int seed = NpcDialogueDatabase.StableHash(npcId);

            GameObject go = CreateNpcObject($"Npc_{npcId}", anchor.position);
            NpcVisualBuilder.Build(go.transform, NpcVisualBuilder.RandomKid(seed));

            CatcherKidNpc npc = go.AddComponent<CatcherKidNpc>();
            npc.Initialize(this, anchor, npcId, seed);
            AttachCulling(go);
            return npc;
        }

        private GameObject CreateNpcObject(string name, Vector3 position)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            return go;
        }

        private void AttachCulling(GameObject go)
        {
            DistanceCulling culling = go.AddComponent<DistanceCulling>();
            if (playerTransform != null) culling.SetTarget(playerTransform);
        }

        private void RewireCullingTargets()
        {
            for (int i = 0; i < villagers.Count; i++)
                if (villagers[i] != null) SetCullingTarget(villagers[i].gameObject);
            for (int i = 0; i < kids.Count; i++)
                if (kids[i] != null) SetCullingTarget(kids[i].gameObject);
        }

        private void SetCullingTarget(GameObject go)
        {
            DistanceCulling culling = go.GetComponent<DistanceCulling>();
            if (culling != null) culling.SetTarget(playerTransform);
        }

        // ── 중앙 틱 ──

        private void Update()
        {
            // ① 플레이어 위치 프레임당 1회 캐시
            hasPlayer = playerTransform != null;
            if (hasPlayer) playerPos = playerTransform.position;

            float time = Time.time;
            float dt = Time.deltaTime;
            int total = villagers.Count + kids.Count;
            if (total == 0) return;

            // ② 라운드로빈 TickAI — 프레임당 최대 3명 (NPC 내부에서 주기 자체 스로틀)
            int steps = Mathf.Min(AiTicksPerFrame, total);
            for (int s = 0; s < steps; s++)
            {
                tickIndex = (tickIndex + 1) % total;
                if (tickIndex < villagers.Count)
                {
                    VillagerNpc v = villagers[tickIndex];
                    if (v != null && v.isActiveAndEnabled) v.TickAI(time);
                }
                else
                {
                    CatcherKidNpc k = kids[tickIndex - villagers.Count];
                    if (k != null && k.isActiveAndEnabled) k.TickAI(time);
                }
            }

            // ③ 플레이어 40m 이내 NPC만 TickMovement + 애니 (밖은 스킵)
            for (int i = 0; i < villagers.Count; i++)
            {
                VillagerNpc v = villagers[i];
                if (v == null || !v.isActiveAndEnabled) continue;
                if (hasPlayer && (v.transform.position - playerPos).sqrMagnitude > MovementRadiusSq) continue;
                v.TickMovement(dt, time);
            }
            for (int i = 0; i < kids.Count; i++)
            {
                CatcherKidNpc k = kids[i];
                if (k == null || !k.isActiveAndEnabled) continue;
                if (hasPlayer && (k.transform.position - playerPos).sqrMagnitude > MovementRadiusSq) continue;
                k.TickMovement(dt, time);
            }

            // ④ 예약 sweep — Despawn/풀 반환된 엔티티의 잔존 예약 정리
            sweepTimer -= dt;
            if (sweepTimer <= 0f)
            {
                sweepTimer = ReservationSweepInterval;
                if (reservedInsects.Count > 0) reservedInsects.RemoveWhere(DeadReservation);
            }
        }
    }
}
