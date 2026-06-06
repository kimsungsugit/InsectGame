using InsectGame.Data;
using UnityEngine;
using System.Collections.Generic;

namespace InsectGame.Core
{
    /// <summary>
    /// 서브에리어 진입 시 완전히 다른 공간을 프로시저럴 생성합니다.
    /// 메인 월드를 숨기고, 별도 위치(2000,0,2000)에 미니 던전/환경을 구축합니다.
    /// </summary>
    public class SubAreaWorldBuilder : MonoBehaviour
    {
        [SerializeField] private RegionManager regionManager;
        [SerializeField] private CameraFollower cameraFollower;

        private GameObject subAreaRoot;
        private bool isInSubArea;
        private Vector3 savedPlayerPos;
        private SubAreaData currentSubArea;

        // UI 알림 — 진입/퇴장 시 화면 상단에 토스트 표시 (3초)
        private string notifyText;
        private float notifyTimer;
        private bool notifyIsEnter;
        private GUIStyle notifyStyleCache;
        private static readonly Color NotifyEnterCol = new Color(0.3f, 0.85f, 0.5f);
        private static readonly Color NotifyExitCol = new Color(0.85f, 0.75f, 0.3f);
        private static readonly Color NotifyBgCol = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color NotifyHintCol = new Color(0.7f, 0.75f, 0.85f);

        // Y=0 — 메인 월드 ground와 동일 평면. 옛 Y=0.5는 환경이 캐릭터로부터 위로 분리되어
        // "공중에 떠있는" 인상 회귀(사용자 명시 보고). 캐릭터 부유(Y=0.5 텔레포트 vs floor Y=0)는
        // 메인 월드와 동일 관례이라 시각적 위화감 없음.
        private static readonly Vector3 SubAreaOrigin = new Vector3(2000f, 0f, 2000f);

        // SubArea 환경 layer — CameraFollower.ResolveObstruction에서 차폐 제외 (캐릭터 가시성 우선).
        // Editor에서 User Layer 31에 "SubAreaEnv" 등록 권고. 미등록 시 fallback layer 31 사용.
        private static int subAreaEnvLayerCached = -1;
        public static int GetSubAreaEnvLayer()
        {
            if (subAreaEnvLayerCached < 0)
            {
                int idx = LayerMask.NameToLayer("SubAreaEnv");
                subAreaEnvLayerCached = idx >= 0 ? idx : 31;
            }
            return subAreaEnvLayerCached;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        // 메인 월드 오브젝트 숨김/복원용
        private readonly List<GameObject> hiddenMainObjects = new List<GameObject>();
        // Update의 Y 안전망용 player transform 캐싱 — 매 프레임 GameObject.Find 회귀 차단
        private Transform cachedPlayerTransform;

        public bool IsInSubArea => isInSubArea;

        public void AutoWire(RegionManager rm, CameraFollower cam)
        {
            if (regionManager != null)
                regionManager.SubAreaChanged -= OnSubAreaChanged;
            regionManager = rm;
            if (regionManager != null)
                regionManager.SubAreaChanged += OnSubAreaChanged;
            if (cameraFollower == null) cameraFollower = cam;
        }

        private void OnDisable()
        {
            if (regionManager != null)
                regionManager.SubAreaChanged -= OnSubAreaChanged;
        }

        private void Update()
        {
            if (notifyTimer > 0f) notifyTimer -= Time.deltaTime;
            // F2: SubArea 안일 때 수동 Exit 트리거 (자동 25m 이탈을 기다리지 않음)
            if (isInSubArea && Input.GetKeyDown(KeyCode.F2)) RequestExit();

            // [E]: 메인 월드에서 nearbySubArea 있으면 진입 트리거 (사용자 선택)
            if (!isInSubArea && regionManager != null && regionManager.NearbySubArea != null
                && Input.GetKeyDown(KeyCode.E))
            {
                regionManager.RequestEnterSubArea();
            }

            if (!isInSubArea) return;

            // player transform lazy 캐싱 — 매 프레임 GameObject.Find 회귀 차단
            if (cachedPlayerTransform == null)
            {
                GameObject p = GameObject.Find("Player");
                if (p == null) return;
                cachedPlayerTransform = p.transform;
            }

            // Y 안전망 — 바닥 밖 낙하 시 자동 메인 복귀 (외곽 벽/floor 사이 무한 낙하 방지)
            if (cachedPlayerTransform.position.y < -3f)
            {
                ShowNotify("⚠ 바닥 밖으로 떨어져 메인 월드로 복귀", false);
                RequestExit();
                return;
            }

            // 25m 이상 이탈 시 자동 Exit (입구 외곽으로 걸어가면 자연 복귀)
            float dx = cachedPlayerTransform.position.x - SubAreaOrigin.x;
            float dz = cachedPlayerTransform.position.z - SubAreaOrigin.z;
            if (dx * dx + dz * dz > 25f * 25f)
            {
                ExitSubArea();
                if (AudioManager.Instance != null)
                    AudioManager.Instance.SetSubAreaActive(false);
            }
        }

        private void OnGUI()
        {
            // F2 OnGUI Event 백업 (Input.GetKeyDown이 focus/IME 이슈로 놓칠 때)
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.F2 && isInSubArea)
            {
                RequestExit();
                e.Use();
            }

            // SubArea 안일 때 우측 상단에 출입 안내 + 토스트 알림
            if (notifyStyleCache == null)
            {
                notifyStyleCache = new GUIStyle(GUI.skin.label)
                { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }

            // 진입/퇴장 토스트 (3초 페이드)
            if (notifyTimer > 0f && !string.IsNullOrEmpty(notifyText))
            {
                float alpha = Mathf.Clamp01(notifyTimer / 3f);
                float w = 560f;
                float h = 56f;
                Rect r = new Rect((Screen.width - w) * 0.5f, 90f, w, h);
                Color bg = NotifyBgCol;
                bg.a = 0.78f * alpha;
                GUI.color = bg;
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                Color textCol = notifyIsEnter ? NotifyEnterCol : NotifyExitCol;
                textCol.a = alpha;
                notifyStyleCache.normal.textColor = textCol;
                GUI.color = Color.white;
                GUI.Label(r, notifyText, notifyStyleCache);
            }

            // SubArea 안 상시 안내 ("F2: 메인 월드로 나가기")
            if (isInSubArea)
            {
                float w = 280f;
                float h = 40f;
                Rect r = new Rect(Screen.width - w - 20f, Screen.height - h - 20f, w, h);
                GUI.color = NotifyBgCol;
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                notifyStyleCache.normal.textColor = NotifyHintCol;
                GUI.color = Color.white;
                GUI.Label(r, "F2: 메인 월드로 나가기", notifyStyleCache);
            }
            // 메인 월드 + 영역 안 → 진입 안내 ([E] 키)
            else if (regionManager != null && regionManager.NearbySubArea != null)
            {
                SubAreaData sub = regionManager.NearbySubArea;
                float w = 420f;
                float h = 80f;
                Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 60f, w, h);
                GUI.color = NotifyBgCol;
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                notifyStyleCache.normal.textColor = NotifyEnterCol;
                GUI.color = Color.white;
                GUI.Label(r, $"[E] {GetSubAreaDisplayName(sub)} 진입", notifyStyleCache);
            }
            GUI.color = Color.white;
        }

        private void OnSubAreaChanged(SubAreaData subArea)
        {
            if (subArea != null && !isInSubArea)
            {
                EnterSubArea(subArea);
                ShowNotify($"✨ {GetSubAreaDisplayName(subArea)} 진입", true);
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.SetSubAreaActive(true);
                    if (!string.IsNullOrEmpty(subArea.environmentType))
                        AudioManager.Instance.PlayAmbient(subArea.environmentType);
                }
            }
            else if (subArea == null && isInSubArea)
            {
                string exitedName = currentSubArea != null ? GetSubAreaDisplayName(currentSubArea) : "서브지역";
                ExitSubArea();
                ShowNotify($"← {exitedName}에서 나옴 (메인 월드 복귀)", false);
                if (AudioManager.Instance != null)
                    AudioManager.Instance.SetSubAreaActive(false);
            }
        }

        private static string GetSubAreaDisplayName(SubAreaData sub)
        {
            if (sub == null) return "서브지역";
            // SubAreaData에 displayName 또는 subAreaId 필드 — 둘 다 fallback으로
            string n = sub.displayName;
            if (string.IsNullOrEmpty(n)) n = sub.subAreaId;
            return string.IsNullOrEmpty(n) ? "서브지역" : n;
        }

        private void ShowNotify(string text, bool isEnter)
        {
            notifyText = text;
            notifyTimer = 3f;
            notifyIsEnter = isEnter;
        }

        /// <summary>F2 단축키 또는 외부 트리거로 수동 Exit 요청.</summary>
        public void RequestExit()
        {
            if (!isInSubArea || regionManager == null) return;
            // sticky 풀고 SubArea 강제 종료 — RegionManager가 SubAreaChanged(null) 발화 후 ExitSubArea 호출
            regionManager.ForceExitSubArea();
        }

        private void EnterSubArea(SubAreaData subArea)
        {
            currentSubArea = subArea;
            isInSubArea = true;

            // RegionManager가 텔레포트된 좌표(2000,0,2000)에서 SubAreaChanged(null) 무한 토글하지
            // 않도록 sticky 모드 ON. ExitSubArea에서 OFF + 쿨다운 설정.
            if (regionManager != null)
                regionManager.SetSubAreaSticky(true);

            // 플레이어 위치 저장
            GameObject player = GameObject.Find("Player");
            if (player != null) savedPlayerPos = player.transform.position;

            // 메인 월드 숨기기
            HideMainWorld();

            // 서브에리어 생성
            if (subAreaRoot != null) Destroy(subAreaRoot);
            subAreaRoot = new GameObject($"SubArea_{subArea.subAreaId}");
            subAreaRoot.transform.position = SubAreaOrigin;

            switch (subArea.environmentType)
            {
                case "cave":
                case "underground":
                    BuildCave(subArea);
                    break;
                case "deep_forest":
                    BuildDeepForest(subArea);
                    break;
                case "underwater":
                case "pond":
                    BuildUnderwater(subArea);
                    break;
                case "fog":
                    BuildFogSwamp(subArea);
                    break;
                case "peak":
                    BuildMountainPeak(subArea);
                    break;
                case "temple":
                    BuildTemple(subArea);
                    break;
                case "flower_maze":
                    BuildFlowerMaze(subArea);
                    break;
                case "greenhouse":
                    BuildGreenhouse(subArea);
                    break;
                case "reeds":
                    BuildReeds(subArea);
                    break;
                default:
                    BuildGenericArea(subArea);
                    break;
            }

            // 환경 전체에 SubArea layer 일괄 설정 — CameraFollower 차폐 제외용.
            // 8개 환경 빌드에 개별 추가하지 않고 subAreaRoot 자식 트리 전체 재귀 처리.
            SetLayerRecursively(subAreaRoot, GetSubAreaEnvLayer());

            // 플레이어를 서브에리어 입구로 텔레포트 — 벽 겹침 회피.
            // BuildCave의 무작위 미로(7×7), BuildTemple의 z=-8 pillar 등으로 옛 고정 좌표는
            // 환경별로 벽에 끼는 회귀 발생. OverlapSphere로 빈 공간 검사 후 보정.
            if (player != null)
            {
                player.transform.position = FindSafeSpawnPosition(SubAreaOrigin);
                // 좌표 점프 후 카메라 baseline 리셋 — 옛 메인 월드 좌표에서 SubArea(2000m)로
                // 슬슬 들어오는 시각적 끊김 차단. SetSubAreaMode가 내부적으로 ResetBaseline 호출.
                if (cameraFollower != null) cameraFollower.SetSubAreaMode(true);
            }
        }

        // SubArea 입구 안전 좌표: 옛 (0, 0.5, -8)을 1차 시도 후 벽 충돌 시 8방향 × 3반경(3/5/7m)
        // 으로 spiral 탐색. 모두 실패 시 SubArea 중심(0, 0.5, 0)을 마지막 fallback.
        private static Vector3 FindSafeSpawnPosition(Vector3 origin)
        {
            Vector3 preferred = origin + new Vector3(0f, 0.5f, -8f);
            if (IsSpawnPositionClear(preferred)) return preferred;

            float[] radii = { 3f, 5f, 7f };
            for (int r = 0; r < radii.Length; r++)
            {
                for (int dir = 0; dir < 8; dir++)
                {
                    float angle = dir * 45f * Mathf.Deg2Rad;
                    Vector3 candidate = origin + new Vector3(
                        Mathf.Sin(angle) * radii[r],
                        0.5f,
                        -8f + Mathf.Cos(angle) * radii[r]);
                    if (IsSpawnPositionClear(candidate)) return candidate;
                }
            }
            // 모든 시도 실패 — SubArea 중심으로 fallback (마지막 안전망)
            return origin + new Vector3(0f, 0.5f, 0f);
        }

        private static bool IsSpawnPositionClear(Vector3 pos)
        {
            // 플레이어 캡슐(반경 ~0.4, 높이 ~1.4) 점유 영역과 정합. PlayerMovement.IsBlockedPosition과 동일 패턴.
            Collider[] hits = Physics.OverlapSphere(pos + Vector3.up * 1.0f, 0.5f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider h = hits[i];
                if (h == null || h.isTrigger) continue;
                // 바닥은 통과(높이 낮음). y 두께 0.5m 이상이면 벽/장애물로 판정.
                if (h.bounds.size.y < 0.5f) continue;
                // Player 자신은 제외 (FindSafeSpawnPosition 호출 시점에 이미 옛 좌표에서 텔레포트 직전)
                if (h.gameObject.name == "Player") continue;
                if (h.attachedRigidbody != null && h.attachedRigidbody.gameObject.name == "Player") continue;
                return false;
            }
            return true;
        }

        private void ExitSubArea()
        {
            isInSubArea = false;
            string exitedId = currentSubArea != null ? currentSubArea.subAreaId : null;
            SubAreaData exited = currentSubArea;
            currentSubArea = null;

            // 서브에리어 파괴
            if (subAreaRoot != null)
            {
                Destroy(subAreaRoot);
                subAreaRoot = null;
            }

            // 메인 월드 복원
            ShowMainWorld();

            // 플레이어를 원래 위치로. 단 savedPlayerPos가 방금 나온 SubArea 안이면
            // 자동 재진입을 막기 위해 중심에서 약간 밖으로 밀어낸다.
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                Vector3 dest = savedPlayerPos;
                if (exited != null && exited.ContainsPoint(savedPlayerPos))
                {
                    Vector3 dir = savedPlayerPos - exited.centerPosition;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
                    dir.Normalize();
                    dest = exited.centerPosition + dir * (exited.radius + 2f);
                    dest.y = savedPlayerPos.y;
                }
                player.transform.position = dest;
                // 좌표 점프 후 카메라 baseline 리셋 + 일반 모드로 offset 복귀.
                // SetSubAreaMode(false)가 내부적으로 ResetBaseline 호출하여 한 번에 처리.
                if (cameraFollower != null) cameraFollower.SetSubAreaMode(false);
            }

            // RegionManager sticky 해제 + 같은 SubArea 재진입 쿨다운 시작
            if (regionManager != null)
                regionManager.SetSubAreaSticky(false, exitedId);
        }

        // SubArea Exit는 SubAreaOrigin에서 25m 이상 이탈하면 자동 트리거(출구 가장자리).
        // ESC 키는 ModalUIRegistry.HandleEscape와 충돌하므로 사용하지 않음 — 사용자가 입구 외곽으로
        // 걸어가면 자연스럽게 메인 월드로 복귀. 25m 이탈 로직은 통합된 Update() (라인 56+)로 이전됨.

        private void HideMainWorld()
        {
            hiddenMainObjects.Clear();
            string[] rootNames = { "Ground", "WorldTerrainBuilder" };
            foreach (string n in rootNames)
            {
                GameObject obj = GameObject.Find(n);
                if (obj != null && obj.activeSelf)
                {
                    hiddenMainObjects.Add(obj);
                    obj.SetActive(false);
                }
            }

            // Region_, Barrier_, Path_, Scenery_ 등 프리픽스 오브젝트 숨기기
            // 주의: 본인의 subAreaRoot은 SubArea_ prefix를 가지므로 명시적으로 제외해야 함
            Transform rootT = subAreaRoot != null ? subAreaRoot.transform : null;
            foreach (GameObject obj in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (obj == null || !obj.activeSelf) continue;
                if (obj == subAreaRoot) continue; // 본인 root 제외 (HideMainWorld가 본인을 끄는 회귀 방지)
                if (rootT != null && obj.transform.IsChildOf(rootT)) continue; // 본인 자식도 제외
                string name = obj.name;
                if (name.StartsWith("Region_") || name.StartsWith("Barrier_") ||
                    name.StartsWith("Path_") || name.StartsWith("Scenery_") ||
                    name.StartsWith("SubArea_") || name.StartsWith("Ground_Hill") ||
                    name.StartsWith("Slope_") || name.StartsWith("Cliff_") ||
                    name.StartsWith("River_") || name.StartsWith("Bridge_") ||
                    name.StartsWith("Boundary_") || name.StartsWith("Swamp_") ||
                    name.StartsWith("SpawnPoint_"))
                {
                    hiddenMainObjects.Add(obj);
                    obj.SetActive(false);
                }
            }
        }

        private void ShowMainWorld()
        {
            foreach (var obj in hiddenMainObjects)
            {
                if (obj != null) obj.SetActive(true);
            }
            hiddenMainObjects.Clear();
        }

        // ========== 동굴 ==========
        private void BuildCave(SubAreaData sub)
        {
            Material wallMat = Mat(new Color(0.25f, 0.22f, 0.18f));
            Material floorMat = Mat(new Color(0.15f, 0.13f, 0.1f));
            Material ceilingMat = Mat(new Color(0.1f, 0.08f, 0.06f));
            Material torchMat = Mat(new Color(1f, 0.7f, 0.2f));
            Material torchHandleMat = Mat(new Color(0.3f, 0.2f, 0.1f));

            // 바닥
            CreateFloor(floorMat, 30f);

            // 천장 — 카메라(약 y=12)보다 위로 배치하여 시야 가리지 않게.
            // ShadowsOnly로 렌더링하여 천장 그림자/어두운 분위기는 유지하되 카메라 시야는 가리지 않음.
            GameObject ceiling = Prim(PrimitiveType.Cube, "Ceiling");
            ceiling.transform.localPosition = new Vector3(0f, 14f, 0f);
            ceiling.transform.localScale = new Vector3(30f, 0.3f, 30f);
            Apply(ceiling, ceilingMat);
            NoCollider(ceiling);
            MeshRenderer ceilMr = ceiling.GetComponent<MeshRenderer>();
            if (ceilMr != null)
                ceilMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

            // 미로형 벽 생성
            int[,] maze = GenerateSimpleMaze(7, 7);
            // 입구 영역 (x=3~4, z=1~2) 4개 셀을 강제 빈 공간으로.
            // FindSafeSpawnPosition 선호 좌표 (0, 0.5, -8) 주변 셀이 벽이면 spiral 탐색해도 좁은 통로에 끼임.
            maze[3, 1] = 0;
            maze[3, 2] = 0;
            maze[4, 1] = 0;
            maze[4, 2] = 0;
            float cellSize = 4f;
            float offsetX = -cellSize * 3.5f;
            float offsetZ = -cellSize * 3.5f;

            for (int x = 0; x < 7; x++)
            {
                for (int z = 0; z < 7; z++)
                {
                    if (maze[x, z] == 1)
                    {
                        GameObject wall = Prim(PrimitiveType.Cube, $"CaveWall_{x}_{z}");
                        wall.transform.localPosition = new Vector3(offsetX + x * cellSize, 2.5f, offsetZ + z * cellSize);
                        wall.transform.localScale = new Vector3(cellSize - 0.2f, 5f, cellSize - 0.2f);
                        Apply(wall, wallMat);
                    }
                }
            }

            // 횃불 (통로에 배치)
            int torchCount = 0;
            for (int x = 0; x < 7 && torchCount < 12; x++)
            {
                for (int z = 0; z < 7 && torchCount < 12; z++)
                {
                    if (maze[x, z] == 0 && (x + z) % 3 == 0)
                    {
                        Vector3 pos = new Vector3(offsetX + x * cellSize + 1.5f, 0f, offsetZ + z * cellSize);
                        CreateTorch(pos, torchHandleMat, torchMat);
                        torchCount++;
                    }
                }
            }

            // 포인트 라이트 (횃불에서)
            CreatePointLight(new Vector3(0f, 3f, 0f), new Color(1f, 0.6f, 0.2f), 8f, 1.2f);
            CreatePointLight(new Vector3(10f, 3f, 5f), new Color(1f, 0.6f, 0.2f), 6f, 0.8f);
            CreatePointLight(new Vector3(-8f, 3f, -4f), new Color(1f, 0.6f, 0.2f), 6f, 0.8f);

            // 외곽 벽 — 바닥(30 = ±15) 안쪽에 배치하여 벽-바닥 사이 빠짐 방지
            CreateBoundaryWalls(wallMat, 14f, 5f);
        }

        // ========== 깊은 숲 ==========
        private void BuildDeepForest(SubAreaData sub)
        {
            Material groundMat = Mat(new Color(0.1f, 0.18f, 0.06f));
            Material trunkMat = Mat(new Color(0.2f, 0.12f, 0.06f));
            Material leafMat = Mat(new Color(0.05f, 0.25f, 0.03f));
            Material pathMat = Mat(new Color(0.3f, 0.25f, 0.15f));

            CreateFloor(groundMat, 35f);

            // 빽빽한 나무 (통로 제외)
            int[,] maze = GenerateSimpleMaze(9, 9);
            float cellSize = 3.5f;
            float off = -cellSize * 4.5f;

            for (int x = 0; x < 9; x++)
            {
                for (int z = 0; z < 9; z++)
                {
                    if (maze[x, z] == 1)
                    {
                        Vector3 pos = new Vector3(off + x * cellSize, 0f, off + z * cellSize);
                        // 나무 기둥 — 옛 Y=3 scale 3(범위 1.5~4.5)은 캐릭터 머리(2.2)보다 한참 솟아 부감 시
                        // 환경이 캐릭터 위에 떠 있는 인상. Y 1.5 scale 1.5(범위 0.75~2.25)로 머리 부근.
                        GameObject trunk = Prim(PrimitiveType.Cylinder, $"Tree_{x}_{z}");
                        trunk.transform.localPosition = pos + new Vector3(0f, 1.5f, 0f);
                        trunk.transform.localScale = new Vector3(0.8f, 1.5f, 0.8f);
                        Apply(trunk, trunkMat);
                        // 나뭇잎 — Y 3 scale 1.5(범위 1.5~4.5)로 캐릭터 머리 부근. 카메라 NormalOffset
                        // (0,12,-8) 시야선에서 잎사귀 위치는 시야선 아래이므로 캐릭터 가림 없음.
                        // ShadowsOnly 적용 시 잎사귀 자체가 안 보여 숲다움 사라짐(사용자 보고) — 정상 렌더링.
                        GameObject leaf = Prim(PrimitiveType.Sphere, $"Leaf_{x}_{z}");
                        leaf.transform.localPosition = pos + new Vector3(0f, 3f, 0f);
                        leaf.transform.localScale = new Vector3(2.5f, 1.5f, 2.5f);
                        Apply(leaf, leafMat);
                        NoCollider(leaf);
                    }
                    else
                    {
                        // 통로 바닥
                        GameObject path = Prim(PrimitiveType.Plane, $"Path_{x}_{z}");
                        path.transform.localPosition = new Vector3(off + x * cellSize, 0.05f, off + z * cellSize);
                        path.transform.localScale = new Vector3(cellSize / 10f, 1f, cellSize / 10f);
                        Apply(path, pathMat);
                        NoCollider(path);
                    }
                }
            }

            // 안개 구체
            Material fogMat = Mat(new Color(0.15f, 0.25f, 0.1f, 0.15f));
            SetTransparent(fogMat);
            for (int i = 0; i < 6; i++)
            {
                GameObject fog = Prim(PrimitiveType.Sphere, $"Fog_{i}");
                fog.transform.localPosition = new Vector3(Random.Range(-12f, 12f), 2f, Random.Range(-12f, 12f));
                fog.transform.localScale = Vector3.one * Random.Range(5f, 10f);
                Apply(fog, fogMat);
                NoCollider(fog);
            }

            CreatePointLight(Vector3.up * 8f, new Color(0.3f, 0.6f, 0.2f), 20f, 0.6f);
            CreateBoundaryWalls(trunkMat, 16f, 7f);
        }

        // ========== 수중 ==========
        private void BuildUnderwater(SubAreaData sub)
        {
            Material floorMat = Mat(new Color(0.1f, 0.15f, 0.25f));
            Material coralMat = Mat(new Color(0.8f, 0.3f, 0.4f));
            Material seaweedMat = Mat(new Color(0.1f, 0.4f, 0.15f));
            Material waterMat = Mat(new Color(0.1f, 0.25f, 0.5f, 0.3f));
            SetTransparent(waterMat);

            CreateFloor(floorMat, 30f);

            // 물 표면 (위에서 덮개)
            GameObject waterTop = Prim(PrimitiveType.Plane, "WaterSurface");
            waterTop.transform.localPosition = new Vector3(0f, 6f, 0f);
            waterTop.transform.localScale = new Vector3(4f, 1f, 4f);
            Apply(waterTop, waterMat);
            NoCollider(waterTop);

            // 산호
            for (int i = 0; i < 15; i++)
            {
                GameObject coral = Prim(PrimitiveType.Cylinder, $"Coral_{i}");
                Vector3 pos = new Vector3(Random.Range(-12f, 12f), 0f, Random.Range(-12f, 12f));
                float h = Random.Range(1f, 3f);
                coral.transform.localPosition = pos + new Vector3(0f, h * 0.5f, 0f);
                coral.transform.localScale = new Vector3(0.3f, h * 0.5f, 0.3f);
                Color cCol = new Color(Random.Range(0.5f, 1f), Random.Range(0.2f, 0.5f), Random.Range(0.3f, 0.7f));
                Apply(coral, Mat(cCol));
                NoCollider(coral);
            }

            // 해초
            for (int i = 0; i < 10; i++)
            {
                Vector3 pos = new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
                for (int j = 0; j < 3; j++)
                {
                    GameObject sw = Prim(PrimitiveType.Capsule, $"Seaweed_{i}_{j}");
                    sw.transform.localPosition = pos + new Vector3(j * 0.3f, 1f + j * 0.8f, 0f);
                    sw.transform.localScale = new Vector3(0.15f, 0.6f, 0.15f);
                    Apply(sw, seaweedMat);
                    NoCollider(sw);
                }
            }

            // 기포
            Material bubbleMat = Mat(new Color(0.6f, 0.8f, 1f, 0.3f));
            SetTransparent(bubbleMat);
            for (int i = 0; i < 20; i++)
            {
                GameObject bubble = Prim(PrimitiveType.Sphere, $"Bubble_{i}");
                bubble.transform.localPosition = new Vector3(Random.Range(-10f, 10f), Random.Range(1f, 5f), Random.Range(-10f, 10f));
                float bs = Random.Range(0.1f, 0.3f);
                bubble.transform.localScale = Vector3.one * bs;
                Apply(bubble, bubbleMat);
                NoCollider(bubble);
            }

            CreatePointLight(new Vector3(0f, 5f, 0f), new Color(0.2f, 0.5f, 0.8f), 18f, 0.8f);
            CreateBoundaryWalls(Mat(new Color(0.15f, 0.2f, 0.3f)), 14f, 6f);
        }

        // ========== 안개 늪 ==========
        private void BuildFogSwamp(SubAreaData sub)
        {
            Material mudMat = Mat(new Color(0.18f, 0.2f, 0.1f));
            Material waterMat = Mat(new Color(0.12f, 0.2f, 0.15f, 0.5f));
            SetTransparent(waterMat);

            CreateFloor(mudMat, 30f);

            // 물웅덩이
            for (int i = 0; i < 8; i++)
            {
                GameObject pool = Prim(PrimitiveType.Cylinder, $"Pool_{i}");
                pool.transform.localPosition = new Vector3(Random.Range(-10f, 10f), 0.02f, Random.Range(-10f, 10f));
                float ps = Random.Range(1.5f, 3f);
                pool.transform.localScale = new Vector3(ps, 0.02f, ps);
                Apply(pool, waterMat);
                NoCollider(pool);
            }

            // 안개 구체 (밀집)
            Material fogMat = Mat(new Color(0.5f, 0.5f, 0.45f, 0.12f));
            SetTransparent(fogMat);
            for (int i = 0; i < 15; i++)
            {
                GameObject fog = Prim(PrimitiveType.Sphere, $"Fog_{i}");
                fog.transform.localPosition = new Vector3(Random.Range(-14f, 14f), Random.Range(1f, 3f), Random.Range(-14f, 14f));
                fog.transform.localScale = Vector3.one * Random.Range(4f, 8f);
                Apply(fog, fogMat);
                NoCollider(fog);
            }

            // 고목
            Material deadWood = Mat(new Color(0.25f, 0.2f, 0.15f));
            for (int i = 0; i < 6; i++)
            {
                GameObject tree = Prim(PrimitiveType.Cylinder, $"DeadTree_{i}");
                Vector3 pos = new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
                tree.transform.localPosition = pos + new Vector3(0f, 1.5f, 0f);
                tree.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);
                tree.transform.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
                Apply(tree, deadWood);
            }

            // 윌오위스프 라이트
            CreatePointLight(new Vector3(3f, 2f, 5f), new Color(0.4f, 0.8f, 0.3f), 6f, 0.5f);
            CreatePointLight(new Vector3(-5f, 2f, -3f), new Color(0.3f, 0.6f, 0.8f), 5f, 0.4f);
            CreatePointLight(Vector3.up * 6f, new Color(0.5f, 0.5f, 0.4f), 15f, 0.4f);
            CreateBoundaryWalls(mudMat, 14f, 4f);
        }

        // ========== 산 정상 ==========
        private void BuildMountainPeak(SubAreaData sub)
        {
            Material rockMat = Mat(new Color(0.5f, 0.48f, 0.44f));
            Material snowMat = Mat(new Color(0.9f, 0.92f, 0.95f));
            Material pathMat = Mat(new Color(0.45f, 0.42f, 0.38f));

            CreateFloor(rockMat, 25f);

            // 눈 패치
            for (int i = 0; i < 8; i++)
            {
                GameObject snow = Prim(PrimitiveType.Plane, $"Snow_{i}");
                snow.transform.localPosition = new Vector3(Random.Range(-10f, 10f), 0.08f, Random.Range(-10f, 10f));
                float ss = Random.Range(0.3f, 0.6f);
                snow.transform.localScale = new Vector3(ss, 1f, ss);
                Apply(snow, snowMat);
                NoCollider(snow);
            }

            // 바위
            for (int i = 0; i < 12; i++)
            {
                GameObject rock = Prim(PrimitiveType.Sphere, $"Rock_{i}");
                Vector3 pos = new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
                float rs = Random.Range(0.5f, 2f);
                rock.transform.localPosition = pos + new Vector3(0f, rs * 0.3f, 0f);
                rock.transform.localScale = new Vector3(rs * 1.3f, rs * 0.5f, rs);
                Apply(rock, rockMat);
            }

            CreatePointLight(Vector3.up * 10f, new Color(0.9f, 0.95f, 1f), 25f, 1.5f);
            CreateBoundaryWalls(rockMat, 11f, 5f);
        }

        // ========== 사원 ==========
        private void BuildTemple(SubAreaData sub)
        {
            Material stoneMat = Mat(new Color(0.35f, 0.3f, 0.28f));
            Material floorMat = Mat(new Color(0.25f, 0.22f, 0.2f));
            Material glowMat = Mat(new Color(0.5f, 0.3f, 0.8f));
            Material torchMat = Mat(new Color(0.7f, 0.5f, 1f));

            CreateFloor(floorMat, 28f);

            // 기둥 (양옆 배치) — 옛 Y=3 scale 3(범위 1.5~4.5)은 캐릭터 머리(2.2)보다 솟음.
            // Y 1.8 scale 1.8(범위 0.9~2.7)로 캐릭터와 같은 평면감.
            for (int i = 0; i < 6; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject pillar = Prim(PrimitiveType.Cylinder, $"Pillar_{i}_{side}");
                    pillar.transform.localPosition = new Vector3(side * 5f, 1.8f, -8f + i * 3.5f);
                    pillar.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
                    Apply(pillar, stoneMat);
                }
            }

            // 제단
            GameObject altar = Prim(PrimitiveType.Cube, "Altar");
            altar.transform.localPosition = new Vector3(0f, 0.75f, 10f);
            altar.transform.localScale = new Vector3(3f, 1.5f, 2f);
            Apply(altar, stoneMat);

            // 신비로운 빛
            GameObject glow = Prim(PrimitiveType.Sphere, "AltarGlow");
            glow.transform.localPosition = new Vector3(0f, 2.5f, 10f);
            glow.transform.localScale = Vector3.one * 1.5f;
            Apply(glow, glowMat);
            NoCollider(glow);

            // 횃불
            for (int i = 0; i < 4; i++)
            {
                float z = -6f + i * 5f;
                CreateTorch(new Vector3(6f, 0f, z), stoneMat, torchMat);
                CreateTorch(new Vector3(-6f, 0f, z), stoneMat, torchMat);
            }

            CreatePointLight(new Vector3(0f, 4f, 10f), new Color(0.6f, 0.3f, 0.9f), 10f, 1.2f);
            CreatePointLight(new Vector3(0f, 5f, 0f), new Color(0.4f, 0.3f, 0.6f), 15f, 0.5f);
            CreateBoundaryWalls(stoneMat, 13f, 6f);
        }

        // ========== 꽃 미로 ==========
        private void BuildFlowerMaze(SubAreaData sub)
        {
            Material hedgeMat = Mat(new Color(0.15f, 0.4f, 0.1f));
            Material floorMat = Mat(new Color(0.3f, 0.4f, 0.2f));

            CreateFloor(floorMat, 30f);

            int[,] maze = GenerateSimpleMaze(8, 8);
            float cellSize = 3f;
            float off = -cellSize * 4f;

            for (int x = 0; x < 8; x++)
            {
                for (int z = 0; z < 8; z++)
                {
                    if (maze[x, z] == 1)
                    {
                        // 생울타리
                        GameObject hedge = Prim(PrimitiveType.Cube, $"Hedge_{x}_{z}");
                        hedge.transform.localPosition = new Vector3(off + x * cellSize, 1.2f, off + z * cellSize);
                        hedge.transform.localScale = new Vector3(cellSize - 0.1f, 2.4f, cellSize - 0.1f);
                        Apply(hedge, hedgeMat);

                        // 꽃 장식 (위에)
                        if ((x + z) % 2 == 0)
                        {
                            Color fc = new Color(Random.Range(0.7f, 1f), Random.Range(0.2f, 0.6f), Random.Range(0.3f, 0.8f));
                            GameObject flower = Prim(PrimitiveType.Sphere, $"Flower_{x}_{z}");
                            flower.transform.localPosition = new Vector3(off + x * cellSize, 2.6f, off + z * cellSize);
                            flower.transform.localScale = new Vector3(0.5f, 0.3f, 0.5f);
                            Apply(flower, Mat(fc));
                            NoCollider(flower);
                        }
                    }
                }
            }

            CreatePointLight(Vector3.up * 8f, new Color(1f, 0.9f, 0.7f), 20f, 1.2f);
            CreateBoundaryWalls(hedgeMat, 14f, 3f);
        }

        // ========== 온실 ==========
        private void BuildGreenhouse(SubAreaData sub)
        {
            Material frameMat = Mat(new Color(0.7f, 0.7f, 0.7f));
            Material glassMat = Mat(new Color(0.8f, 0.9f, 0.8f, 0.15f));
            SetTransparent(glassMat);
            Material soilMat = Mat(new Color(0.3f, 0.22f, 0.12f));

            CreateFloor(soilMat, 20f);

            // 유리 벽
            CreateGlassWall(glassMat, frameMat, 10f, 5f);

            // 화분
            Material potMat = Mat(new Color(0.6f, 0.35f, 0.15f));
            for (int i = 0; i < 8; i++)
            {
                float x = (i < 4) ? -6f : 6f;
                float z = -6f + (i % 4) * 4f;
                GameObject pot = Prim(PrimitiveType.Cylinder, $"Pot_{i}");
                pot.transform.localPosition = new Vector3(x, 0.4f, z);
                pot.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
                Apply(pot, potMat);
                // 식물
                Color plantCol = new Color(Random.Range(0.1f, 0.3f), Random.Range(0.4f, 0.8f), Random.Range(0.1f, 0.3f));
                GameObject plant = Prim(PrimitiveType.Sphere, $"Plant_{i}");
                plant.transform.localPosition = new Vector3(x, 1.2f, z);
                plant.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
                Apply(plant, Mat(plantCol));
                NoCollider(plant);
            }

            CreatePointLight(Vector3.up * 4f, new Color(0.85f, 1f, 0.8f), 18f, 1.0f);
        }

        // ========== 갈대밭 ==========
        private void BuildReeds(SubAreaData sub)
        {
            Material waterMat = Mat(new Color(0.15f, 0.25f, 0.35f, 0.5f));
            SetTransparent(waterMat);
            Material reedMat = Mat(new Color(0.4f, 0.5f, 0.2f));
            Material mudMat = Mat(new Color(0.25f, 0.22f, 0.15f));

            CreateFloor(mudMat, 25f);

            // 물 표면
            GameObject water = Prim(PrimitiveType.Plane, "Water");
            water.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            water.transform.localScale = new Vector3(2.5f, 1f, 2.5f);
            Apply(water, waterMat);
            NoCollider(water);

            // 갈대 (빽빽하게, 통로 제외)
            int[,] maze = GenerateSimpleMaze(10, 10);
            float cellSize = 2.5f;
            float off = -cellSize * 5f;

            for (int x = 0; x < 10; x++)
            {
                for (int z = 0; z < 10; z++)
                {
                    if (maze[x, z] == 1)
                    {
                        for (int r = 0; r < 3; r++)
                        {
                            GameObject reed = Prim(PrimitiveType.Cylinder, $"Reed_{x}_{z}_{r}");
                            float rx = off + x * cellSize + Random.Range(-0.5f, 0.5f);
                            float rz = off + z * cellSize + Random.Range(-0.5f, 0.5f);
                            reed.transform.localPosition = new Vector3(rx, 1.5f, rz);
                            reed.transform.localScale = new Vector3(0.08f, 1.5f, 0.08f);
                            reed.transform.localRotation = Quaternion.Euler(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
                            Apply(reed, reedMat);
                            NoCollider(reed);
                        }
                    }
                }
            }

            CreatePointLight(Vector3.up * 6f, new Color(0.8f, 0.75f, 0.5f), 18f, 0.9f);
            CreateBoundaryWalls(mudMat, 11f, 3f);
        }

        private void BuildGenericArea(SubAreaData sub)
        {
            CreateFloor(Mat(new Color(0.3f, 0.35f, 0.25f)), 20f);
            CreatePointLight(Vector3.up * 8f, Color.white, 20f, 1f);
            CreateBoundaryWalls(Mat(new Color(0.4f, 0.4f, 0.4f)), 9f, 4f);
        }

        // ========== 공통 유틸 ==========

        private int[,] GenerateSimpleMaze(int w, int h)
        {
            int[,] grid = new int[w, h];
            // 기본: 벽으로 채우기
            for (int x = 0; x < w; x++)
                for (int z = 0; z < h; z++)
                    grid[x, z] = 1;

            // DFS로 통로 파기
            System.Collections.Generic.Stack<Vector2Int> stack = new System.Collections.Generic.Stack<Vector2Int>();
            Vector2Int start = new Vector2Int(1, 1);
            grid[start.x, start.y] = 0;
            stack.Push(start);

            int[] dx = { 0, 0, 2, -2 };
            int[] dz = { 2, -2, 0, 0 };

            while (stack.Count > 0)
            {
                Vector2Int cur = stack.Peek();
                List<int> dirs = new List<int>();
                for (int d = 0; d < 4; d++)
                {
                    int nx = cur.x + dx[d];
                    int nz = cur.y + dz[d];
                    if (nx > 0 && nx < w - 1 && nz > 0 && nz < h - 1 && grid[nx, nz] == 1)
                        dirs.Add(d);
                }

                if (dirs.Count > 0)
                {
                    int d = dirs[Random.Range(0, dirs.Count)];
                    int mx = cur.x + dx[d] / 2;
                    int mz = cur.y + dz[d] / 2;
                    int nx = cur.x + dx[d];
                    int nz = cur.y + dz[d];
                    grid[mx, mz] = 0;
                    grid[nx, nz] = 0;
                    stack.Push(new Vector2Int(nx, nz));
                }
                else
                {
                    stack.Pop();
                }
            }

            // 입구/출구 보장
            grid[w / 2, 0] = 0;
            grid[w / 2, 1] = 0;
            grid[w / 2, h - 1] = 0;
            grid[w / 2, h - 2] = 0;

            return grid;
        }

        private void CreateFloor(Material mat, float size)
        {
            GameObject floor = Prim(PrimitiveType.Plane, "Floor");
            floor.transform.localPosition = Vector3.zero;
            floor.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
            Apply(floor, mat);
        }

        private void CreateBoundaryWalls(Material mat, float halfSize, float height)
        {
            string[] names = { "Wall_N", "Wall_S", "Wall_E", "Wall_W" };
            Vector3[] pos = {
                new Vector3(0f, height / 2f, halfSize),
                new Vector3(0f, height / 2f, -halfSize),
                new Vector3(halfSize, height / 2f, 0f),
                new Vector3(-halfSize, height / 2f, 0f)
            };
            Vector3[] scl = {
                new Vector3(halfSize * 2f, height, 1f),
                new Vector3(halfSize * 2f, height, 1f),
                new Vector3(1f, height, halfSize * 2f),
                new Vector3(1f, height, halfSize * 2f)
            };
            for (int i = 0; i < 4; i++)
            {
                GameObject wall = Prim(PrimitiveType.Cube, names[i]);
                wall.transform.localPosition = pos[i];
                wall.transform.localScale = scl[i];
                Apply(wall, mat);
            }
        }

        private void CreateTorch(Vector3 pos, Material handleMat, Material flameMat)
        {
            GameObject handle = Prim(PrimitiveType.Cylinder, "TorchHandle");
            handle.transform.localPosition = pos + new Vector3(0f, 1.2f, 0f);
            handle.transform.localScale = new Vector3(0.08f, 0.6f, 0.08f);
            Apply(handle, handleMat);
            NoCollider(handle);

            GameObject flame = Prim(PrimitiveType.Sphere, "TorchFlame");
            flame.transform.localPosition = pos + new Vector3(0f, 2f, 0f);
            flame.transform.localScale = new Vector3(0.25f, 0.35f, 0.25f);
            Apply(flame, flameMat);
            NoCollider(flame);
        }

        private void CreatePointLight(Vector3 localPos, Color color, float range, float intensity)
        {
            GameObject lightObj = new GameObject("SubAreaLight");
            lightObj.transform.SetParent(subAreaRoot.transform, false);
            lightObj.transform.localPosition = localPos;
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
        }

        private void CreateGlassWall(Material glassMat, Material frameMat, float halfSize, float height)
        {
            // 4면 유리
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                GameObject glass = Prim(PrimitiveType.Cube, $"Glass_{i}");
                glass.transform.localPosition = dir * halfSize + new Vector3(0f, height / 2f, 0f);
                glass.transform.localScale = new Vector3(
                    (i % 2 == 0) ? halfSize * 2f : 0.1f,
                    height,
                    (i % 2 == 0) ? 0.1f : halfSize * 2f);
                Apply(glass, glassMat);
                // 유리지만 collider 보존 — 옛은 NoCollider 호출로 통과 가능, 바닥 밖으로 빠짐.
            }
            // 프레임 기둥 4개
            Vector3[] corners = { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) };
            foreach (var c in corners)
            {
                GameObject post = Prim(PrimitiveType.Cylinder, "Frame");
                post.transform.localPosition = c * halfSize + new Vector3(0f, height / 2f, 0f);
                post.transform.localScale = new Vector3(0.15f, height / 2f, 0.15f);
                Apply(post, frameMat);
            }
        }

        // 프리미티브 생성 헬퍼
        private GameObject Prim(PrimitiveType type, string name)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(subAreaRoot.transform, false);
            return obj;
        }

        private void Apply(GameObject obj, Material mat)
        {
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = mat;
        }

        private void NoCollider(GameObject obj)
        {
            Collider c = obj.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        private Material Mat(Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
            mat.color = color;
            return mat;
        }

        private void SetTransparent(Material mat)
        {
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
        }
    }
}
