using System;
using System.Collections.Generic;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Core
{
    public enum IslandMode
    {
        None,
        /// <summary>내 섬 — 꾸미기·수확이 된다.</summary>
        Own,
        /// <summary>남의 섬 구경 — 저장된 모습을 걸어다니며 볼 뿐이다.</summary>
        Visit,
    }

    /// <summary>
    /// 섬을 월드에 세우고 드나듦을 맡는다. 섬은 메인 월드와 떨어진 좌표에 짓고, 리전 상태는 합성 서브에리어
    /// (<see cref="GameConstants.Island.SubAreaId"/>)로 탄다 — 이유는 <see cref="RegionManager.EnterDetachedSubArea"/>.
    ///
    /// 모양은 <see cref="IslandTerrainBuilder"/>·<see cref="IslandObjectBuilder"/>가 짓고(콜라이더 없음),
    /// 여기는 <b>밟는 땅·경계 벽·물건 차단</b> 콜라이더와 방목 곤충, 꾸미기 화면이 쓰는 미리보기를 맡는다.
    /// </summary>
    public partial class IslandWorldBuilder : MonoBehaviour
    {
        /// <summary>
        /// 섬 중심(월드). 서브에리어 방(2000,·,2000)·배틀 아레나(1000,·,1000)·프리뷰 리그(·,-5000,·)와 겹치지 않고,
        /// 메인 월드(±540)에서 카메라 원거리 절단면 밖이라 서로 보이지 않는다.
        /// </summary>
        public static readonly Vector3 Origin = new Vector3(3000f, 0f, 3000f);

        /// <summary>꾸미기 화면의 카메라 오프셋 — 평소(0,9,-6)보다 높고 멀다. 섬을 넓게 봐야 놓을 자리가 보인다.</summary>
        private static readonly Vector3 EditCameraOffset = new Vector3(0f, 14f, -8.5f);

        // 필드 곤충은 등급에 따라 1.0~1.9배로 커지지만 섬은 가구 사이를 다니는 곳이라 전부 1배로 둔다 —
        // 키우면 전설 곤충이 벤치보다 커져 물건을 뚫고 다니는 것처럼 보인다.
        private const float InsectScale = 1f;
        private const float WallHeight = 4f;

        [SerializeField] private RegionManager regionManager;
        [SerializeField] private IslandManager island;
        [SerializeField] private CameraFollower cameraFollower;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private InsectDatabase database;

        private readonly SubAreaData islandArea = new SubAreaData
        {
            subAreaId = GameConstants.Island.SubAreaId,
            displayName = "나의 섬",
            description = "나만의 섬",
            detached = true,
            environmentType = "island",
            radius = 40f,
            exclusiveInsectIds = new string[0],   // 야생 스폰 없음 — 스포너가 몸을 거두고 아무것도 세우지 않는다
        };

        private IslandMode mode = IslandMode.None;
        private IslandSnapshot visitSnapshot;
        private Vector3 savedPlayerPos;
        private Quaternion savedPlayerRot;

        private GameObject root;
        private GameObject terrainRoot;
        private GameObject objectsRoot;
        private GameObject insectsRoot;
        private GameObject gridRoot;
        private readonly List<GameObject> objectInstances = new List<GameObject>();
        private IslandMaterialCache materials;
        private int builtSizeLevel = -1;

        private readonly List<Vector2Int> freeCells = new List<Vector2Int>();
        private readonly HashSet<Vector2Int> freeCellSet = new HashSet<Vector2Int>();

        // 꾸미기 미리보기
        private GameObject ghostRoot;
        private GameObject ghostModel;
        private GameObject ghostPad;
        private string ghostId;
        private int ghostRot = -1;
        private Transform editFocus;
        private bool editCameraActive;
        private int hiddenObjectIndex = -1;

        /// <summary>섬에 들어오거나 나갔다, 또는 내 섬 ↔ 남의 섬이 바뀌었다.</summary>
        public event Action ModeChanged;

        public IslandMode Mode => mode;

        /// <summary>
        /// 「챔피언의 꿈」이 섬을 쓰는 중인가. 켜면 섬 HUD가 숨고 방문 통지(퀘스트)가 가지 않는다 —
        /// 꿈속의 섬은 누구의 섬도 아니고 이 계정의 어떤 기록도 바꾸지 않는다.
        /// </summary>
        public bool DreamMode { get; set; }

        /// <summary>섬 격자 칸(차지 칸 기준)의 월드 중심. 프롤로그가 목표 지점을 잡는 데 쓴다.</summary>
        public static Vector3 CellWorldCenter(int x, int z, int width, int depth)
        {
            return Origin + IslandGrid.FootprintCenter(x, z, width, depth);
        }

        /// <summary>
        /// 지금 섬을 돌아다니는 곤충 중 <paramref name="from"/>에서 가장 가까운 것. 섬에 곤충이 없으면 false.
        /// 위치는 매 호출 읽는다 — 곤충은 걸어 다니므로 한 번 읽어 둔 좌표를 가리키면 엉뚱한 곳을 가리킨다.
        /// </summary>
        public bool TryGetNearestInsect(Vector3 from, out Vector3 position, out InsectData data, out int level)
        {
            position = Vector3.zero;
            data = null;
            level = 1;
            if (insectsRoot == null) return false;

            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < insectsRoot.transform.childCount; i++)
            {
                Transform child = insectsRoot.transform.GetChild(i);
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                InsectEntity entity = child.GetComponent<InsectEntity>();
                if (entity == null || entity.Data == null) continue;
                Vector3 delta = child.position - from;
                delta.y = 0f;
                float d = delta.sqrMagnitude;
                if (d >= best) continue;
                best = d;
                position = child.position;
                data = entity.Data;
                level = entity.Level;
                found = true;
            }
            return found;
        }
        public bool IsOnIsland => mode != IslandMode.None;
        public bool IsVisiting => mode == IslandMode.Visit;

        /// <summary>꾸미기 화면이 열려 있는가(<see cref="BeginEditCamera"/>~<see cref="EndEditCamera"/>). 손님 곤충이 그동안 들어서지 않는다.</summary>
        public bool IsEditing => editCameraActive;
        public IslandSnapshot VisitSnapshot => visitSnapshot;

        public int CurrentSizeLevel =>
            mode == IslandMode.Visit && visitSnapshot != null ? visitSnapshot.sizeLevel
            : island != null ? island.SizeLevel : 0;

        private IReadOnlyList<IslandPlacedRecord> CurrentPlaced =>
            mode == IslandMode.Visit && visitSnapshot != null ? visitSnapshot.placed
            : island != null ? island.Placed : null;

        public void AutoWire(RegionManager regions, IslandManager islandManager, CameraFollower camera,
            PlayerMovement movement, InsectDatabase insectDatabase)
        {
            Unsubscribe();
            if (regionManager == null) regionManager = regions;
            if (island == null) island = islandManager;
            if (cameraFollower == null) cameraFollower = camera;
            if (playerMovement == null) playerMovement = movement;
            if (database == null) database = insectDatabase;
            Subscribe();
        }

        // AutoWire는 Bootstrap에서 한 번만 불린다 — OnDisable에서 해지한 구독을 여기서 되살린다.
        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (regionManager != null)
            {
                regionManager.SubAreaChanged -= OnSubAreaChanged;
                regionManager.SubAreaChanged += OnSubAreaChanged;
            }
            if (island != null)
            {
                island.LayoutChanged -= OnLayoutChanged;
                island.LayoutChanged += OnLayoutChanged;
                island.InsectsChanged -= OnInsectsChanged;
                island.InsectsChanged += OnInsectsChanged;
            }
        }

        private void Unsubscribe()
        {
            if (regionManager != null) regionManager.SubAreaChanged -= OnSubAreaChanged;
            if (island != null)
            {
                island.LayoutChanged -= OnLayoutChanged;
                island.InsectsChanged -= OnInsectsChanged;
            }
        }

        private void OnDestroy()
        {
            DestroyWorld();
            if (dockMaterials != null)
            {
                dockMaterials.DestroyAll();
                dockMaterials = null;
            }
        }

        // ── 본 마을 나루터 ──

        /// <summary>본 마을 광장 기준 나루터 방향·거리. 242°는 집(215°)과 뽑기 오두막(270°) 사이의 빈 자리다.</summary>
        private const float DockAngleDeg = 242f;
        private const float DockDistance = 15.5f;
        public const string DockInteractionId = "village_island_dock";

        private IslandMaterialCache dockMaterials;

        /// <summary>
        /// 본 마을에 섬으로 가는 나루터를 세우고 상호작용 지점을 돌려준다. 탐험 메뉴의 [내 섬]과 같은 창을 연다 —
        /// 메뉴를 열지 않는 사람도 마을을 걷다가 섬을 만나게 하려는 두 번째 입구다.
        /// </summary>
        public InteractionPointDef BuildVillageDock(RegionData[] regions)
        {
            RegionData meadow = null;
            if (regions != null)
                for (int i = 0; i < regions.Length; i++)
                    if (regions[i] != null && regions[i].regionId == "meadow") meadow = regions[i];
            if (meadow == null) return null;

            Vector3 village = VillageBuilder.GetMainVillageCenter(meadow.centerPosition, meadow.radius);
            float rad = DockAngleDeg * Mathf.Deg2Rad;
            Vector3 pos = village + new Vector3(Mathf.Cos(rad) * DockDistance, 0f, Mathf.Sin(rad) * DockDistance);
            pos.y = FieldGround.SurfaceY(pos.x, pos.z);

            if (dockMaterials == null) dockMaterials = new IslandMaterialCache();
            // 이름이 `Scenery_`·`Region_` 등으로 시작하면 서브에리어 진입 때 꺼진다(SubAreaWorldBuilder.HideMainWorld).
            var dock = new GameObject("IslandDock");
            dock.transform.position = pos;
            Vector3 toVillage = village - pos;
            toVillage.y = 0f;
            if (toVillage.sqrMagnitude > 0.01f) dock.transform.rotation = Quaternion.LookRotation(toVillage.normalized);

            Color wood = new Color(0.55f, 0.38f, 0.22f);
            Color woodDark = new Color(0.36f, 0.24f, 0.14f);
            Color water = new Color(0.30f, 0.62f, 0.80f);
            Color sand = new Color(0.90f, 0.82f, 0.60f);
            Color sail = new Color(0.96f, 0.94f, 0.86f);

            // 모래 깔개 위의 작은 물웅덩이와 배 — 위에서 내려다봐도 "배 타는 곳"으로 읽히게.
            DockPart(dock.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, -1.6f), Vector3.zero,
                new Vector3(5.2f, 0.02f, 5.2f), sand);
            DockPart(dock.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, -2.0f), Vector3.zero,
                new Vector3(3.6f, 0.02f, 3.6f), water);
            DockPart(dock.transform, PrimitiveType.Cube, new Vector3(0f, 0.12f, -0.4f), Vector3.zero,
                new Vector3(1.4f, 0.1f, 2.6f), wood);
            DockPart(dock.transform, PrimitiveType.Cube, new Vector3(0.9f, 0.3f, -2.2f), new Vector3(0f, 25f, 0f),
                new Vector3(0.9f, 0.4f, 2.0f), woodDark);
            DockPart(dock.transform, PrimitiveType.Cube, new Vector3(0.9f, 1.0f, -2.2f), Vector3.zero,
                new Vector3(0.08f, 1.4f, 0.08f), woodDark);
            DockPart(dock.transform, PrimitiveType.Cube, new Vector3(1.15f, 1.15f, -2.2f), new Vector3(0f, 25f, 0f),
                new Vector3(0.04f, 0.9f, 0.8f), sail);

            // 팻말
            DockPart(dock.transform, PrimitiveType.Cube, new Vector3(-1.2f, 0.9f, 0.6f), Vector3.zero,
                new Vector3(0.14f, 1.8f, 0.14f), woodDark);
            DockPart(dock.transform, PrimitiveType.Cube, new Vector3(-1.2f, 1.9f, 0.6f), Vector3.zero,
                new Vector3(1.9f, 0.8f, 0.12f), wood);
            var textObj = new GameObject("DockSignText");
            textObj.transform.SetParent(dock.transform, false);
            textObj.transform.localPosition = new Vector3(-1.2f, 1.9f, 0.68f);
            textObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh text = textObj.AddComponent<TextMesh>();
            text.text = "내 섬";
            text.characterSize = 0.14f;
            text.fontSize = 48;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1f, 0.95f, 0.80f);

            return new InteractionPointDef
            {
                id = DockInteractionId,
                worldPosition = pos + dock.transform.forward * 1.2f,
                radius = 3f,
                label = "내 섬 나루터",
                kind = InteractionKind.IslandDock,
            };
        }

        private void DockPart(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localEuler,
            Vector3 localScale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = "DockPart";
            // 장식에 콜라이더를 남기지 않는다 — 걸어 지나갈 수 있어야 하고, 클릭-이동 레이가 걸리면 안 된다.
            Collider col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localEulerAngles = localEuler;
            part.transform.localScale = localScale;
            part.GetComponent<MeshRenderer>().sharedMaterial = dockMaterials.Get(color);
        }

        // ── 드나들기 ──

        /// <summary>지금 섬으로 떠날 수 있는가. 못 가면 이유 문구.</summary>
        public bool CanTravel(out string reason)
        {
            reason = null;
            if (regionManager == null || playerMovement == null || island == null)
            {
                reason = "섬을 불러오지 못했습니다.";
                return false;
            }
            if (mode == IslandMode.None && regionManager.CurrentSubArea != null)
            {
                reason = "서브지역 안에서는 떠날 수 없습니다. 먼저 밖으로 나가 주세요.";
                return false;
            }
            return true;
        }

        public bool EnterOwnIsland()
        {
            if (!CanTravel(out _)) return false;
            if (mode == IslandMode.Own) return true;
            visitSnapshot = null;
            return EnterInternal(IslandMode.Own);
        }

        /// <summary>남의 섬 구경. <paramref name="snapshot"/>은 받아서 이미 정리한 것(<see cref="IslandSaveRules.SanitizeSnapshot"/>).</summary>
        public bool EnterVisit(IslandSnapshot snapshot)
        {
            if (snapshot == null || !CanTravel(out _)) return false;
            visitSnapshot = snapshot;
            return EnterInternal(IslandMode.Visit);
        }

        private bool EnterInternal(IslandMode next)
        {
            EndEditCamera();
            bool wasOnIsland = mode != IslandMode.None;
            if (!wasOnIsland)
            {
                savedPlayerPos = playerMovement.transform.position;
                savedPlayerRot = playerMovement.transform.rotation;
            }

            mode = next;
            // 끼임 복구(PlayerMovement.RecoverToSafePosition)가 이 자리로 보낸다 — 물건을 놓을 수 없게 보호된 칸이다.
            islandArea.centerPosition = Origin + IslandGrid.ArrivalPoint(CurrentSizeLevel);
            islandArea.displayName = next == IslandMode.Visit && visitSnapshot != null
                ? visitSnapshot.ownerName + "의 섬"
                : "나의 섬";

            if (!wasOnIsland && !regionManager.EnterDetachedSubArea(islandArea))
            {
                mode = IslandMode.None;
                visitSnapshot = null;
                return false;
            }

            BuildWorld();
            TeleportPlayer(islandArea.centerPosition + Vector3.up * 0.5f, Quaternion.identity);
            if (cameraFollower != null)
            {
                cameraFollower.SetSubAreaMode(true);
                cameraFollower.SnapToTarget();
            }

            if (mode == IslandMode.Own) island.NotifyEnteredOwnIsland();
            else if (!DreamMode) island.NotifyVisitedFriendIsland();
            OnIslandEnteredForGuests();   // 손님 기록 정리(떠나 있는 동안 끝난 손님은 이미 떠났다) — IslandWorldBuilder.Guests.cs

            ModeChanged?.Invoke();
            return true;
        }

        /// <summary>남의 섬에서 내 섬으로 돌아온다(메인 월드로 나가지 않고).</summary>
        public bool ReturnToOwnIsland()
        {
            if (mode != IslandMode.Visit) return false;
            visitSnapshot = null;
            return EnterInternal(IslandMode.Own);
        }

        /// <summary>메인 월드로 나간다.</summary>
        public void ExitIsland()
        {
            if (mode == IslandMode.None || regionManager == null) return;
            // 정리는 SubAreaChanged(null)을 받는 쪽에서 한다 — 다른 경로로 강제 종료돼도 같은 정리가 돈다.
            regionManager.ForceExitSubArea();
        }

        private void OnSubAreaChanged(SubAreaData area)
        {
            if (area != null || mode == IslandMode.None) return;

            EndEditCamera();
            mode = IslandMode.None;
            visitSnapshot = null;
            DestroyWorld();
            TeleportPlayer(SnapToGround(savedPlayerPos), savedPlayerRot);
            if (cameraFollower != null)
            {
                cameraFollower.SetSubAreaMode(false);
                cameraFollower.SnapToTarget();
            }
            ModeChanged?.Invoke();
        }

        private void TeleportPlayer(Vector3 position, Quaternion rotation)
        {
            if (playerMovement == null) return;
            Physics.SyncTransforms();
            playerMovement.transform.SetPositionAndRotation(position, rotation);
            playerMovement.StopNavigation();
        }

        private static Vector3 SnapToGround(Vector3 pos)
        {
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 50f, pos.z), Vector3.down, out RaycastHit hit, 100f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y + 0.5f;
            return pos;
        }

        private void Update()
        {
            if (mode == IslandMode.None || playerMovement == null) return;

            // 경계 벽이 있어 여기 올 일은 없어야 한다. 그래도 바닥 밖으로 빠지면 도착 자리로 되돌린다
            // (서브에리어 방의 낙하 안전망은 SubAreaWorldBuilder 것이라 섬에서는 돌지 않는다).
            Vector3 p = playerMovement.transform.position;
            float limit = IslandGrid.HalfExtent(CurrentSizeLevel) + 3f;
            if (p.y < Origin.y - 3f || Mathf.Abs(p.x - Origin.x) > limit || Mathf.Abs(p.z - Origin.z) > limit)
                TeleportPlayer(islandArea.centerPosition + Vector3.up * 0.5f, Quaternion.identity);

            TickWorldMood();   // 방목 곤충의 시간·날씨 기분
            TickGuests();      // 손님 곤충 — IslandWorldBuilder.Guests.cs
        }

        // ── 월드 짓기 ──

        private void BuildWorld()
        {
            DestroyWorld();
            materials = new IslandMaterialCache();
            root = new GameObject("PlayerIsland");
            root.transform.position = Origin;

            builtSizeLevel = CurrentSizeLevel;
            terrainRoot = IslandTerrainBuilder.Build(builtSizeLevel, root.transform, materials);
            BuildColliders(builtSizeLevel);
            BuildGridOverlay(builtSizeLevel);

            objectsRoot = new GameObject("IslandObjects");
            objectsRoot.transform.SetParent(root.transform, false);
            insectsRoot = new GameObject("IslandInsects");
            insectsRoot.transform.SetParent(root.transform, false);

            RebuildObjects();
            RebuildInsects();

            SetLayerRecursively(root, SubAreaWorldBuilder.GetSubAreaEnvLayer());
            Physics.SyncTransforms();
        }

        private void DestroyWorld()
        {
            RecallAllGuestBodies();   // 손님 몸은 root 밖(풀)에 있다 — 섬을 허물 때 같이 거둔다. 기록은 남는다.
            HideGhost();
            objectInstances.Clear();
            hiddenObjectIndex = -1;
            if (root != null)
            {
                Destroy(root);
                root = null;
            }
            terrainRoot = objectsRoot = insectsRoot = gridRoot = null;
            if (materials != null)
            {
                materials.DestroyAll();
                materials = null;
            }
            builtSizeLevel = -1;
        }

        // 밟는 땅과 경계. 모양 쪽(IslandTerrainBuilder)은 콜라이더를 남기지 않는다 — 플레이어 접지와 클릭-이동이
        // 콜라이더를 보므로, 장식에 콜라이더가 있으면 모래톱이나 바다로 걸어 나간다.
        private void BuildColliders(int sizeLevel)
        {
            float half = IslandGrid.HalfExtent(sizeLevel);
            var colliders = new GameObject("IslandColliders");
            colliders.transform.SetParent(root.transform, false);

            AddBox(colliders.transform, "IslandGroundCollider",
                new Vector3(0f, -0.5f, 0f), new Vector3(half * 2f + 2f, 1f, half * 2f + 2f));

            const float t = 1f;
            AddBox(colliders.transform, "IslandWall_N", new Vector3(0f, WallHeight * 0.5f, half + t * 0.5f),
                new Vector3(half * 2f + t * 2f, WallHeight, t));
            AddBox(colliders.transform, "IslandWall_S", new Vector3(0f, WallHeight * 0.5f, -half - t * 0.5f),
                new Vector3(half * 2f + t * 2f, WallHeight, t));
            AddBox(colliders.transform, "IslandWall_E", new Vector3(half + t * 0.5f, WallHeight * 0.5f, 0f),
                new Vector3(t, WallHeight, half * 2f + t * 2f));
            AddBox(colliders.transform, "IslandWall_W", new Vector3(-half - t * 0.5f, WallHeight * 0.5f, 0f),
                new Vector3(t, WallHeight, half * 2f + t * 2f));
        }

        private static void AddBox(Transform parent, string name, Vector3 localCenter, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.AddComponent<BoxCollider>().size = size;
        }

        // 꾸미기 화면에서만 켜는 격자선. 선은 얇은 Quad이고 칸 수 + 1 줄씩이라 가장 큰 섬(22칸)도 46개다.
        private void BuildGridOverlay(int sizeLevel)
        {
            gridRoot = new GameObject("IslandGridOverlay");
            gridRoot.transform.SetParent(root.transform, false);
            int size = IslandGrid.GridSize(sizeLevel);
            int half = size / 2;
            float cs = GameConstants.Island.CellSize;
            float extent = size * cs;
            Material lineMat = materials.GetFade(new Color(1f, 1f, 1f, 0.28f));
            for (int i = -half; i <= half; i++)
            {
                AddFlatQuad(gridRoot.transform, "GridLineX", new Vector3(i * cs, 0.02f, 0f),
                    new Vector3(0.05f, extent, 1f), lineMat);
                AddFlatQuad(gridRoot.transform, "GridLineZ", new Vector3(0f, 0.02f, i * cs),
                    new Vector3(extent, 0.05f, 1f), lineMat);
            }
            gridRoot.SetActive(false);
        }

        private static GameObject AddFlatQuad(Transform parent, string name, Vector3 localPos, Vector3 scale,
            Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Collider col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = scale;
            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return quad;
        }

        private void OnLayoutChanged()
        {
            if (mode != IslandMode.Own || root == null) return;
            // 섬 크기가 바뀌면 땅·벽·격자까지 다시 지어야 한다.
            if (island.SizeLevel != builtSizeLevel)
            {
                bool gridWasOn = gridRoot != null && gridRoot.activeSelf;
                BuildWorld();
                islandArea.centerPosition = Origin + IslandGrid.ArrivalPoint(CurrentSizeLevel);
                TeleportPlayer(islandArea.centerPosition + Vector3.up * 0.5f, Quaternion.identity);
                if (cameraFollower != null) cameraFollower.SnapToTarget();
                SetGridVisible(gridWasOn);
                return;
            }
            RebuildObjects();
            RebuildInsects();   // 빈 칸이 달라졌다 — 곤충이 새 건물을 뚫고 다니지 않게
            RevalidateGuestCells();   // 손님이 선 칸에 물건이 놓였다 — 그 몸은 거두고 다른 빈 칸에 다시 선다
            SetLayerRecursively(root, SubAreaWorldBuilder.GetSubAreaEnvLayer());
            Physics.SyncTransforms();
        }

        private void OnInsectsChanged()
        {
            if (mode != IslandMode.Own || root == null) return;
            RebuildInsects();
            SetLayerRecursively(insectsRoot, SubAreaWorldBuilder.GetSubAreaEnvLayer());
        }

        private void RebuildObjects()
        {
            for (int i = objectsRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(objectsRoot.transform.GetChild(i).gameObject);
            objectInstances.Clear();
            hiddenObjectIndex = -1;

            IReadOnlyList<IslandPlacedRecord> placed = CurrentPlaced;
            if (placed == null) return;
            for (int i = 0; i < placed.Count; i++)
            {
                IslandPlacedRecord p = placed[i];
                IslandObjectDef def = p != null ? IslandCatalog.Get(p.id) : null;
                // 카탈로그가 모르는 물건(더 새 버전에서 산 것)은 그리지 않는다. 인덱스는 맞춰 둔다.
                if (def == null)
                {
                    objectInstances.Add(null);
                    continue;
                }
                objectInstances.Add(BuildPlaced(def, p));
            }
        }

        private GameObject BuildPlaced(IslandObjectDef def, IslandPlacedRecord p)
        {
            IslandGrid.Footprint(def, p.rot, out int w, out int d);
            GameObject holder = new GameObject("Placed_" + def.id);
            holder.transform.SetParent(objectsRoot.transform, false);
            holder.transform.localPosition = IslandGrid.FootprintCenter(p.x, p.z, w, d);

            GameObject model = IslandObjectBuilder.Build(def, holder.transform, materials);
            if (model != null) model.transform.localRotation = Quaternion.Euler(0f, p.rot * 90f, 0f);

            if (def.blocksMovement)
            {
                float cs = GameConstants.Island.CellSize;
                // 차지 칸보다 조금 작게 — 딱 맞추면 붙여 놓은 두 물건 사이의 한 칸 길이 막힌다.
                BoxCollider blocker = holder.AddComponent<BoxCollider>();
                blocker.size = new Vector3(w * cs - 0.3f, 2f, d * cs - 0.3f);
                blocker.center = new Vector3(0f, 1f, 0f);
            }
            return holder;
        }

        private void RebuildInsects()
        {
            for (int i = insectsRoot.transform.childCount - 1; i >= 0; i--)
                DestroyInsect(insectsRoot.transform.GetChild(i).gameObject);

            IslandGrid.CollectFreeCells(CurrentPlaced, CurrentSizeLevel, freeCells);
            freeCellSet.Clear();
            for (int i = 0; i < freeCells.Count; i++) freeCellSet.Add(freeCells[i]);
            if (freeCells.Count == 0 || database == null) return;

            if (mode == IslandMode.Visit && visitSnapshot != null)
            {
                for (int i = 0; i < visitSnapshot.insects.Count; i++)
                {
                    IslandSnapshotInsect s = visitSnapshot.insects[i];
                    SpawnInsect(database.GetById(s.insectId), s.level, s.shiny, i);
                }
            }
            else if (island != null)
            {
                IReadOnlyList<PlayerInsectData> list = island.ReleasedInsects;
                for (int i = 0; i < list.Count; i++)
                    SpawnInsect(database.GetById(list[i].insectId), list[i].level, list[i].isShiny, i);
            }
        }

        private void SpawnInsect(InsectData data, int level, bool shiny, int index)
        {
            if (data == null) return;
            // 시작 칸은 결정적으로 고른다 — 배치가 바뀌어 다시 세울 때마다 곤충들이 한꺼번에 딴 데로 튀지 않게.
            Vector2Int cell = freeCells[(index * 7919 + 13) % freeCells.Count];
            var go = new GameObject("IslandInsect_" + data.insectId);
            go.transform.SetParent(insectsRoot.transform, false);
            go.transform.localScale = Vector3.one * InsectScale;
            go.transform.position = Origin + IslandGrid.FootprintCenter(cell.x, cell.y, 1, 1)
                                    + Vector3.up * (IslandInsectWalker.GroundOffset * InsectScale);
            go.transform.rotation = Quaternion.Euler(0f, (index * 67) % 360, 0f);

            // 위치를 잡은 뒤에 짓는다 — BuildForBattle이 지금 자리를 기준점으로 삼는다.
            InsectEntity entity = go.AddComponent<InsectEntity>();
            entity.BuildForBattle(data, Mathf.Max(1, level), shiny);
            IslandInsectWalker walker = go.AddComponent<IslandInsectWalker>();
            walker.Initialize(Origin, freeCells, freeCellSet, index * 31 + 7);
            if (moodStateKnown) walker.ApplyWorld(moodState);   // 다시 세운 곤충도 지금 시간·날씨의 기분으로
        }

        // 곤충 모델은 조각마다 자기 머티리얼 인스턴스를 든다(InsectEntity.ApplyColorRaw). GameObject만 지우면
        // 그 머티리얼이 남는다 — 배치를 바꿀 때마다 곤충을 다시 세우므로 여기서 같이 지운다.
        private static void DestroyInsect(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].sharedMaterial != null) Destroy(renderers[i].material);
            Destroy(go);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        // ── 꾸미기 화면 지원 ──

        public void SetGridVisible(bool visible)
        {
            if (gridRoot != null) gridRoot.SetActive(visible);
        }

        /// <summary>화면 좌표가 가리키는 섬 칸. 카메라 시선이 지면과 만나지 않으면 false.</summary>
        public bool ScreenToCell(Vector2 screenPos, out int cellX, out int cellZ)
        {
            cellX = cellZ = 0;
            Camera cam = Camera.main;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(screenPos);
            // 콜라이더가 아니라 평면과 만나는 점을 쓴다 — 건물 지붕을 눌러도 그 아래 칸이 잡힌다.
            var plane = new Plane(Vector3.up, Origin);
            if (!plane.Raycast(ray, out float enter)) return false;
            IslandGrid.CellAt(ray.GetPoint(enter) - Origin, out cellX, out cellZ);
            return true;
        }

        /// <summary>
        /// 놓을 자리 미리보기. 모델은 물건·회전이 바뀔 때만 다시 짓고 평소엔 자리만 옮긴다.
        /// 바닥 판 색으로 놓을 수 있는지를 알린다.
        /// </summary>
        public void ShowGhost(string id, int x, int z, int rot, bool valid)
        {
            IslandObjectDef def = IslandCatalog.Get(id);
            if (def == null || root == null) { HideGhost(); return; }

            int r = ((rot % 4) + 4) % 4;
            IslandGrid.Footprint(def, r, out int w, out int d);
            float cs = GameConstants.Island.CellSize;

            if (ghostRoot == null)
            {
                ghostRoot = new GameObject("IslandGhost");
                ghostRoot.transform.SetParent(root.transform, false);
            }
            if (ghostModel == null || ghostId != id)
            {
                if (ghostModel != null) Destroy(ghostModel);
                ghostModel = IslandObjectBuilder.Build(def, ghostRoot.transform, materials);
                ghostId = id;
                ghostRot = -1;
                SetLayerRecursively(ghostRoot, SubAreaWorldBuilder.GetSubAreaEnvLayer());
            }
            if (ghostRot != r && ghostModel != null)
            {
                ghostModel.transform.localRotation = Quaternion.Euler(0f, r * 90f, 0f);
                ghostRot = r;
            }

            Color padColor = valid ? new Color(0.31f, 0.79f, 0.54f, 0.55f) : new Color(0.96f, 0.39f, 0.31f, 0.6f);
            if (ghostPad == null)
            {
                ghostPad = AddFlatQuad(ghostRoot.transform, "GhostPad", Vector3.zero, Vector3.one,
                    materials.GetFade(padColor));
                ghostPad.layer = SubAreaWorldBuilder.GetSubAreaEnvLayer();
            }
            ghostPad.GetComponent<MeshRenderer>().sharedMaterial = materials.GetFade(padColor);
            ghostPad.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            ghostPad.transform.localScale = new Vector3(w * cs, d * cs, 1f);

            ghostRoot.transform.localPosition = IslandGrid.FootprintCenter(x, z, w, d);
            ghostRoot.SetActive(true);
        }

        public void HideGhost()
        {
            if (ghostRoot != null) Destroy(ghostRoot);
            ghostRoot = ghostModel = ghostPad = null;
            ghostId = null;
            ghostRot = -1;
        }

        /// <summary>옮기는 중인 물건의 원래 모습을 잠깐 감춘다(-1이면 전부 다시 보인다).</summary>
        public void SetObjectHidden(int placedIndex)
        {
            if (hiddenObjectIndex >= 0 && hiddenObjectIndex < objectInstances.Count
                && objectInstances[hiddenObjectIndex] != null)
                objectInstances[hiddenObjectIndex].SetActive(true);
            hiddenObjectIndex = placedIndex;
            if (placedIndex >= 0 && placedIndex < objectInstances.Count && objectInstances[placedIndex] != null)
                objectInstances[placedIndex].SetActive(false);
        }

        /// <summary>
        /// 꾸미기 카메라 — 플레이어 대신 보이지 않는 초점을 따라가게 한다. 초점은 화면을 끌어 옮긴다.
        /// 꾸미기 화면은 모달이라 캐릭터가 서 있고, 카메라가 캐릭터에 묶여 있으면 섬의 한쪽만 보인다.
        /// </summary>
        public void BeginEditCamera()
        {
            if (editCameraActive || cameraFollower == null || playerMovement == null) return;
            if (editFocus == null)
            {
                var focus = new GameObject("IslandEditFocus");
                focus.transform.SetParent(transform, false);
                editFocus = focus.transform;
            }
            Vector3 start = playerMovement.transform.position;
            start.y = Origin.y;
            editFocus.SetPositionAndRotation(start, Quaternion.identity);
            cameraFollower.SetTarget(editFocus);
            cameraFollower.SetOffsetOverride(EditCameraOffset);
            editCameraActive = true;
        }

        public void EndEditCamera()
        {
            if (!editCameraActive) return;
            editCameraActive = false;
            if (cameraFollower == null) return;
            cameraFollower.ClearOffsetOverride();
            if (playerMovement != null) cameraFollower.SetTarget(playerMovement.transform);
        }

        /// <summary>꾸미기 카메라 초점을 옮긴다(월드 XZ 이동량). 섬 밖으로 나가지 않게 가둔다.</summary>
        public void PanEditCamera(Vector3 worldDelta)
        {
            if (!editCameraActive || editFocus == null) return;
            float half = IslandGrid.HalfExtent(CurrentSizeLevel);
            Vector3 p = editFocus.position + new Vector3(worldDelta.x, 0f, worldDelta.z);
            p.x = Mathf.Clamp(p.x, Origin.x - half, Origin.x + half);
            p.z = Mathf.Clamp(p.z, Origin.z - half, Origin.z + half);
            p.y = Origin.y;
            editFocus.position = p;
        }
    }
}
