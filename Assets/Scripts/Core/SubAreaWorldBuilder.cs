using InsectGame.Data;
using InsectGame.UI;
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

        // 포획 모달/배틀/미니게임 중 [E]·F2·진입/퇴장 버튼이 SubArea 진입·이탈을 발화하지 않도록
        // CaptureInputController.IsPlayerFrozen()과 동일한 신호로 가드. frozen은 미니게임/배틀/포획
        // 모달이 모두 SetFrozen(true)를 거므로 단일 신호로 셋 다 커버. lazy FindFirstObjectByType 캐싱.
        private PlayerMovement playerMovement;

        private GameObject subAreaRoot;
        private bool isInSubArea;
        private Vector3 savedPlayerPos;
        private SubAreaData currentSubArea;

        // UI 알림 — 진입/퇴장 시 화면 상단에 토스트 표시 (3초)
        private string notifyText;
        private float notifyTimer;
        private bool notifyIsEnter;
        private GUIStyle notifyStyleCache;
        private GUIStyle entryExitButtonStyleCache;
        private static readonly Color NotifyEnterCol = new Color(0.3f, 0.85f, 0.5f);
        private static readonly Color NotifyExitCol = new Color(0.85f, 0.75f, 0.3f);
        private static readonly Color NotifyBgCol = new Color(0f, 0f, 0f, 0.78f);

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

        /// <summary>
        /// 이번 서브에리어 빌드가 만든 런타임 머티리얼. <b>subAreaRoot를 Destroy해도 이건 안 지워진다</b> —
        /// <c>new Material(...)</c>은 GameObject가 아니라 별개 오브젝트라 명시적으로 파괴해야 한다.
        /// 한 번 지을 때마다 <c>Mat()</c>이 41번 불리고, 서브에리어는 25m 이탈·[E] 재진입으로
        /// 세션 내내 반복해서 들락거리므로 방치하면 계속 쌓인다.
        /// (같은 이유로 <c>NpcVisualBuilder.CleanupMaterials</c>·<c>PlayerVisualBuilder.SafeDestroyMat</c>가 있다.)
        /// </summary>
        private readonly List<Material> runtimeMaterials = new List<Material>();
        // Update의 Y 안전망용 player transform 캐싱 — 매 프레임 GameObject.Find 회귀 차단
        private Transform cachedPlayerTransform;

        public bool IsInSubArea => isInSubArea;

        // 포획 선택지(CaptureChoiceUI)·배틀·미니게임은 모두 PlayerMovement.SetFrozen(true)를 건다.
        // CaptureInputController.IsPlayerFrozen()과 동일하게 frozen만으로 셋 다 가드한다.
        private bool IsPlayerFrozen()
        {
            if (playerMovement == null)
                playerMovement = FindFirstObjectByType<PlayerMovement>();
            return playerMovement != null && playerMovement.IsFrozen;
        }

        // SubArea 진입/이탈을 막아야 하는 상태: 모달이 열려 있거나(포획 선택지·도감 등)
        // 플레이어가 frozen(배틀·레이드·미니게임·포획 모달)일 때. 같은 [E] 입력이 SubArea 진입으로
        // 새지 않게, 입력 처리·버튼 렌더 양쪽에서 이 신호로 차단한다.
        private bool IsSubAreaActionBlocked()
        {
            return ModalUIRegistry.IsAnyOpen() || IsPlayerFrozen();
        }

        /// <summary>
        /// [E]를 노리는 다른 시스템. **서브에리어 진입은 이 둘에 양보한다.**
        ///
        /// 같은 [E]를 세 시스템이 폴링한다 — 포획(`CaptureInputController`), 건물·NPC 상호작용
        /// (`WorldInteractionController`), 그리고 여기. 앞의 둘은 `HasPriorityTarget`으로 서로
        /// 양보하는데 서브에리어만 그 사슬 밖에 있었다. 진입 반경은 12m이고 포획 탐색 반경은 8m라
        /// **겹치는 자리가 흔하다**: 입구 근처에서 곤충을 잡으려고 [E]를 누르면 포획과 진입이
        /// 동시에 발화하고, 어느 쪽이 먼저 돌지는 Update 실행 순서에 달려 있어 정해져 있지 않다.
        /// (진입이 먼저면 플레이어가 2000m 밖으로 순간이동한 뒤 포획이 시작된다.)
        ///
        /// 셋 중 서브에리어가 양보하는 이유는 **여기만 큰 전용 버튼이 따로 있기 때문**이다
        /// (620×100, 하단 중앙). [E]는 편의지 유일한 진입로가 아니다.
        /// </summary>
        private WorldInteractionController worldInteractions;
        private InsectGame.Capture.CaptureInputController captureInput;

        public void AutoWire(WorldInteractionController interactions,
            InsectGame.Capture.CaptureInputController capture)
        {
            if (worldInteractions == null) worldInteractions = interactions;
            if (captureInput == null) captureInput = capture;
        }

        /// <summary>이번 [E]를 포획이나 상호작용이 가져가는가.</summary>
        private bool InteractKeyClaimed()
        {
            if (worldInteractions != null && worldInteractions.HasPriorityTarget) return true;
            if (captureInput != null && captureInput.HasCatchTarget) return true;
            return false;
        }

        public void AutoWire(RegionManager rm, CameraFollower cam)
        {
            if (regionManager != null)
                regionManager.SubAreaChanged -= OnSubAreaChanged;
            regionManager = rm;
            if (regionManager != null)
                regionManager.SubAreaChanged += OnSubAreaChanged;
            if (cameraFollower == null) cameraFollower = cam;
        }

        // `AutoWire`는 Bootstrap에서 한 번만 불린다 — `OnDisable`에서 해지한 구독을 여기서
        // 되살리지 않으면 이 컴포넌트가 한 번이라도 꺼졌다 켜지는 순간 서브에리어 진입·이탈이
        // 영구히 죽는다. `-=` 뒤 `+=`라 중복 구독이 되지 않는다.
        // (`subscription_lint`는 UI 루트 하위만 보므로 `World/` 아래인 이 파일은 검사 밖이다.)
        private void OnEnable()
        {
            if (regionManager == null) return;
            regionManager.SubAreaChanged -= OnSubAreaChanged;
            regionManager.SubAreaChanged += OnSubAreaChanged;
        }

        private void OnDisable()
        {
            if (regionManager != null)
                regionManager.SubAreaChanged -= OnSubAreaChanged;
        }

        // 씬 전환·종료로 이 컴포넌트가 죽을 때 마지막 빌드가 남아 있으면 그 머티리얼도 함께 정리한다.
        private void OnDestroy()
        {
            DestroySubAreaWorld();
        }

        /// <summary>
        /// 서브에리어 빌드 파기 — GameObject 트리와 <b>런타임 머티리얼을 함께</b> 지운다.
        /// 예전엔 <c>Destroy(subAreaRoot)</c>만 해서 오브젝트는 사라지고 머티리얼은 남았다.
        /// </summary>
        private void DestroySubAreaWorld()
        {
            if (subAreaRoot != null)
            {
                Destroy(subAreaRoot);
                subAreaRoot = null;
            }

            for (int i = 0; i < runtimeMaterials.Count; i++)
                if (runtimeMaterials[i] != null) Destroy(runtimeMaterials[i]);
            runtimeMaterials.Clear();
        }

        private void Update()
        {
            if (notifyTimer > 0f) notifyTimer -= Time.deltaTime;

            // 모달/배틀/미니게임/포획 모달(frozen) 중에는 수동 진입·퇴장 입력을 막는다.
            // CaptureInputController와 동일 신호 — 같은 [E]가 포획과 SubArea 진입에 동시 발화하던 충돌 차단.
            // (Y 추락/25m 자동 이탈 등 비자발적 안전망은 아래에서 계속 동작.)
            bool actionBlocked = IsSubAreaActionBlocked();

            // F2: SubArea 안일 때 수동 Exit 트리거 (자동 25m 이탈을 기다리지 않음)
            if (!actionBlocked && isInSubArea && Input.GetKeyDown(KeyCode.F2)) RequestExit();

            // [E]: 메인 월드에서 nearbySubArea 있으면 진입 트리거 (사용자 선택)
            if (!actionBlocked && !isInSubArea && !InteractKeyClaimed()
                && regionManager != null && regionManager.NearbySubArea != null
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
                // RequestExit로 통일 — 옛 ExitSubArea() 직접 호출은 RegionManager.currentSubArea를 정리하지
                // 않아 걸어서 나가면 [E] 재진입 불가 + 메인월드 region 가드 비활성(상태 누수). RequestExit는
                // ForceExitSubArea→SubAreaChanged(null)→OnSubAreaChanged→ExitSubArea(+오디오)까지 단일 경로.
                RequestExit();
            }
        }

        private void OnGUI()
        {
            // 모달/배틀/미니게임/포획 모달(frozen) 중에는 진입·퇴장 입력과 버튼을 모두 비활성.
            // Update와 동일 신호로 OnGUI 백업 입력·GUI.Button 렌더 양쪽을 일관 차단한다.
            bool actionBlocked = IsSubAreaActionBlocked();

            // F2 OnGUI Event 백업 (Input.GetKeyDown이 focus/IME 이슈로 놓칠 때)
            Event e = Event.current;
            if (!actionBlocked && e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.F2 && isInSubArea)
            {
                RequestExit();
                e.Use();
            }
            // [E] 진입 OnGUI Event 백업 — Update의 Input.GetKeyDown이 focus/IME로 놓칠 때 대비
            // (F2 퇴장과 동일 패턴). 사용자 보고 "E 눌러도 진입 안 됨"의 직접 원인.
            if (!actionBlocked && e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.E
                && !isInSubArea && !InteractKeyClaimed()
                && regionManager != null && regionManager.NearbySubArea != null)
            {
                regionManager.RequestEnterSubArea();
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
                // 픽셀 좌표계다(`UIScale.Begin()`을 쓰지 않는다). 고정 90px은 노치·상단 인셋이
                // 있는 기기에서 토스트를 그 아래로 밀어 넣는다 — 인셋이 0인 데스크톱에서는
                // 90이 그대로 이기고, 인셋이 있으면 그만큼 내려간다(rules/ui-layout.md의 Px 파사드).
                float toastY = Mathf.Max(90f, UISafeLayout.Px.ContentTop);
                Rect r = new Rect((Screen.width - w) * 0.5f, toastY, w, h);
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

            // 진입/퇴장 버튼은 모달/배틀/미니게임/포획 모달(frozen) 중에는 그리지도 입력받지도 않는다.
            // 모달이 빈 SubArea 위에 뜨거나 frozen 상태에서 '들어가기' 클릭으로 순간이동하던 충돌 차단.
            if (!actionBlocked)
            {
                // 진입과 퇴장은 같은 고정 위치의 큰 버튼으로 제공한다.
                if (isInSubArea)
                {
                    Rect r = GetEntryExitButtonRect();
                    BlockFieldClicks(r);
                    GUI.backgroundColor = NotifyExitCol;
                    if (GUI.Button(r, "메인 월드로 나가기", GetEntryExitButtonStyle()))
                        RequestExit();
                    GUI.backgroundColor = Color.white;
                }
                // 메인 월드 + 영역 안 → 동일 위치에 큰 진입 버튼 표시
                else if (regionManager != null && regionManager.NearbySubArea != null)
                {
                    SubAreaData sub = regionManager.NearbySubArea;
                    Rect r = GetEntryExitButtonRect();
                    BlockFieldClicks(r);
                    GUI.backgroundColor = NotifyEnterCol;
                    if (GUI.Button(r, $"{GetSubAreaDisplayName(sub)} 들어가기", GetEntryExitButtonStyle()))
                    {
                        regionManager.RequestEnterSubArea();
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            GUI.color = Color.white;
        }

        /// <summary>
        /// 이 버튼 위의 탭이 <b>월드 클릭-이동으로 새지 않게</b> 영역을 등록한다.
        /// 안 하면 "들어가기"를 누른 그 탭이 동시에 클릭-이동을 걸어, 서브에리어로 들어간
        /// 직후 캐릭터가 <b>새 월드의 엉뚱한 지점으로</b> 걸어간다(목적지는 옛 월드 좌표다).
        /// `PlayerMovement`가 `Input.GetMouseButtonDown(0)`을 Update에서 따로 폴링하는데,
        /// 그 시점엔 모달도 없고 IMGUI는 EventSystem을 안 거쳐 이 등록이 유일한 방어선이다
        /// (rules/ui-layout.md — 같은 결함을 QuickAccessBarUI·WorldFieldMultiplayerUI·
        /// TutorialQuestUI에서 이미 고쳤다. 이 화면의 버튼이 그중 가장 크다: 620×100).
        ///
        /// <b>이 화면은 픽셀 좌표로 그린다</b>(`UIScale.Begin()`을 쓰지 않는다) — 등록은
        /// 가상 좌표를 받으므로 `UIScale.Scale`로 나눠 넘긴다.
        /// </summary>
        private static void BlockFieldClicks(Rect pixelRect)
        {
            float s = UIScale.Scale;
            if (s <= 0f) return;
            FieldHudInput.RegisterBlockingRect(
                new Rect(pixelRect.x / s, pixelRect.y / s, pixelRect.width / s, pixelRect.height / s));
        }

        private Rect GetEntryExitButtonRect()
        {
            float availableWidth = Screen.width - SafeArea.Left - SafeArea.Right - 40f;
            float w = Mathf.Min(620f, availableWidth);
            float h = 100f;
            float x = (Screen.width - w) * 0.5f;
            float y = UISafeLayout.Px.BottomY(h);
            return new Rect(x, y, w, h);
        }

        private GUIStyle GetEntryExitButtonStyle()
        {
            if (entryExitButtonStyleCache != null) return entryExitButtonStyleCache;

            entryExitButtonStyleCache = new GUIStyle(GUI.skin.button)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(18, 18, 12, 12)
            };
            entryExitButtonStyleCache.normal.textColor = Color.white;
            entryExitButtonStyleCache.hover.textColor = Color.white;
            entryExitButtonStyleCache.active.textColor = Color.white;
            return entryExitButtonStyleCache;
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

            // 서브에리어 생성 (이전 빌드가 남아 있으면 머티리얼까지 함께 파기)
            DestroySubAreaWorld();
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
                // ── 2막(ver2) 전용 ── 서사 비중이 큰 4곳만 전용으로 짓는다. 나머지 8곳은
                // 1막 환경 재활용을 유지한다 — 전부 새로 만들 값어치가 없다.
                case "vault":
                    BuildLedgerVault(subArea);
                    break;
                case "archive":
                    BuildIceArchive(subArea);
                    break;
                case "kiln":
                    BuildEmberKiln(subArea);
                    break;
                case "ledger":
                    BuildLedgerHall(subArea);
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

        // ExitSubArea 복귀 좌표용 — 지면 raycast 스냅 후 막혀 있으면 주변 빈 자리를 spiral 탐색.
        // EnterSubArea의 FindSafeSpawnPosition과 대칭(이탈 측 충돌검사 부재로 산 바위에 박히던 비대칭 제거).
        private static Vector3 FindClearGroundPositionNear(Vector3 desired)
        {
            Vector3 snapped = SnapToGroundY(desired);
            if (IsSpawnPositionClear(snapped)) return snapped;

            float[] radii = { 3f, 5f, 7f };
            for (int r = 0; r < radii.Length; r++)
            {
                for (int dir = 0; dir < 8; dir++)
                {
                    float angle = dir * 45f * Mathf.Deg2Rad;
                    Vector3 cand = SnapToGroundY(new Vector3(
                        desired.x + Mathf.Sin(angle) * radii[r],
                        desired.y,
                        desired.z + Mathf.Cos(angle) * radii[r]));
                    if (IsSpawnPositionClear(cand)) return cand;
                }
            }
            return snapped; // 모두 막힘 — 최소한 지면엔 스냅(영구 박힘보다 나음)
        }

        // 위에서 아래로 raycast해 실제 지면 Y에 스냅(+0.5 여유). 히트 없으면 원 Y 유지.
        private static Vector3 SnapToGroundY(Vector3 pos)
        {
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 50f, pos.z), Vector3.down, out RaycastHit hit, 100f))
                pos.y = hit.point.y + 0.5f;
            return pos;
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
            DestroySubAreaWorld();

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
                // 진입(FindSafeSpawnPosition)과 대칭으로 지면 스냅 + 충돌 빈자리 보정. ShowMainWorld로
                // 메인 콜라이더(산 바위 Scenery_MountainRock/경사 등)가 복원된 뒤라, dest가 그 안에 박히면
                // PlayerMovement.IsBlockedPosition이 모든 이동을 막아 영구 갇힘 → 산에서 못 움직이던 원인.
                dest = FindClearGroundPositionNear(dest);
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
            // 어두운 머티리얼이 동굴이 새까매 보이던 핵심 원인 — 바닥/벽 밝기를 올려 환경광이 반사되게 한다.
            Material wallMat = Mat(new Color(0.4f, 0.35f, 0.29f));
            Material floorMat = Mat(new Color(0.32f, 0.28f, 0.22f));
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

            // 포인트 라이트 (횃불에서) — 범위/세기 상향 + 중앙 상단 따뜻한 채움광으로 전체를 밝힌다.
            CreatePointLight(new Vector3(0f, 3f, 0f), new Color(1f, 0.7f, 0.3f), 16f, 2.4f);
            CreatePointLight(new Vector3(10f, 3f, 5f), new Color(1f, 0.7f, 0.3f), 13f, 1.8f);
            CreatePointLight(new Vector3(-8f, 3f, -4f), new Color(1f, 0.7f, 0.3f), 13f, 1.8f);
            CreatePointLight(new Vector3(0f, 9f, 0f), new Color(0.95f, 0.88f, 0.72f), 26f, 1.3f);

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
            // 바닥을 조금 밝게 — 옛 (0.1,0.15,0.25)는 파란 fog와 겹쳐 캐릭터 발밑이 새까매 대비 상실.
            Material floorMat = Mat(new Color(0.16f, 0.22f, 0.33f));
            Material coralMat = Mat(new Color(0.8f, 0.3f, 0.4f));
            Material seaweedMat = Mat(new Color(0.1f, 0.4f, 0.15f));
            // 물 표면 alpha 0.3 → 0.16: 옛은 카메라-캐릭터 시선을 가로지르는 반투명 파란 막이 캐릭터를 덮어
            // "연못/수중에서 캐릭터가 안 보임"의 직접 원인이었음(사용자 보고). 위치도 함께 올려 이중 방어.
            Material waterMat = Mat(new Color(0.1f, 0.25f, 0.5f, 0.16f));
            SetTransparent(waterMat);

            CreateFloor(floorMat, 30f);

            // 물 표면 — 카메라(플레이어 Y0.5 + offset Y9 ≈ Y9.5) 위로 배치.
            // 옛 Y=6은 카메라(Y9.5)→캐릭터(Y1.35) 시선(Y 1.35~9.5)을 정면으로 관통해 파란 반투명 막이
            // 캐릭터 위를 덮었다. NoCollider라 카메라 회피(ResolveObstruction SphereCast)에도 안 걸려
            // 그냥 뚫고 렌더만 가림. Y=13으로 올려 부감 카메라 frustum 밖(카메라보다 위)으로 빼내 캐릭터를
            // 절대 가리지 않게 한다. 부감뷰 + Unity Plane 단면 특성상 이 면은 평상시 화면에 거의 안 보이며,
            // 수중 분위기는 파란 fog(SubAreaEnvironment)와 기포로 표현된다.
            GameObject waterTop = Prim(PrimitiveType.Plane, "WaterSurface");
            waterTop.transform.localPosition = new Vector3(0f, 13f, 0f);
            waterTop.transform.localScale = new Vector3(7f, 1f, 7f);
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

            // 조명 강화 — 옛 파란 포인트라이트 1개(0.8)는 짙은 파란 fog와 겹쳐 캐릭터가 어둡게 묻힘.
            // 상단 채움광(밝게) + 카메라측(캐릭터 정면) 채움광으로 캐릭터 가시성 확보.
            CreatePointLight(new Vector3(0f, 6f, 0f), new Color(0.4f, 0.62f, 0.9f), 22f, 1.2f);
            CreatePointLight(new Vector3(0f, 5f, -7f), new Color(0.5f, 0.7f, 0.95f), 16f, 1.0f);
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
        // ================= 2막(ver2) 전용 환경 =================

        /// <summary>
        /// 명부회 창고(dunes_vault) — 선반에 곤충 상자가 층층이 쌓인 방.
        /// 예전엔 그냥 동굴이었다. 남획의 현장인데 동굴로 보이면 서사가 전달되지 않는다.
        /// </summary>
        private void BuildLedgerVault(SubAreaData sub)
        {
            Material floorMat = Mat(new Color(0.30f, 0.26f, 0.20f));
            Material shelfMat = Mat(new Color(0.38f, 0.28f, 0.17f));
            Material crateMat = Mat(new Color(0.52f, 0.42f, 0.26f));
            Material glassMat = Mat(new Color(0.62f, 0.74f, 0.72f, 0.35f));
            SetTransparent(glassMat);   // 알파 재질은 Mat만으론 불투명하게 나온다
            Material lampMat = Mat(new Color(0.95f, 0.80f, 0.45f));

            CreateFloor(floorMat, 30f);

            // 선반 4열 — 통로를 가운데 남긴다.
            for (int col = 0; col < 4; col++)
            {
                float x = -9f + col * 6f;
                if (col >= 2) x += 2f;   // 가운데 통로
                for (int tier = 0; tier < 3; tier++)
                {
                    GameObject plank = Prim(PrimitiveType.Cube, $"Shelf_{col}_{tier}");
                    plank.transform.localPosition = new Vector3(x, 0.7f + tier * 1.05f, 0f);
                    plank.transform.localScale = new Vector3(2.6f, 0.12f, 16f);
                    Apply(plank, shelfMat);

                    // 상자 — 층마다 개수를 달리해 손댄 흔적을 남긴다.
                    int boxes = 5 - tier;
                    for (int b = 0; b < boxes; b++)
                    {
                        GameObject crate = Prim(PrimitiveType.Cube, $"Crate_{col}_{tier}_{b}");
                        crate.transform.localPosition =
                            new Vector3(x + (b % 2 == 0 ? -0.5f : 0.5f), 1.05f + tier * 1.05f, -6.5f + b * 3.1f);
                        crate.transform.localScale = new Vector3(1.1f, 0.75f, 1.1f);
                        Apply(crate, b % 3 == 0 ? glassMat : crateMat);
                    }
                }
                // 선반 기둥
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject post = Prim(PrimitiveType.Cylinder, $"ShelfPost_{col}_{side}");
                    post.transform.localPosition = new Vector3(x, 1.6f, side * 7.6f);
                    post.transform.localScale = new Vector3(0.16f, 1.6f, 0.16f);
                    Apply(post, shelfMat);
                }
            }

            // 매달린 등 — 창고 특유의 차가운 작업 조명.
            for (int i = 0; i < 3; i++)
            {
                GameObject lamp = Prim(PrimitiveType.Sphere, $"VaultLamp_{i}");
                lamp.transform.localPosition = new Vector3(0f, 3.4f, -6f + i * 6f);
                lamp.transform.localScale = Vector3.one * 0.5f;
                Apply(lamp, lampMat);
                CreatePointLight(new Vector3(0f, 3.4f, -6f + i * 6f), new Color(1f, 0.88f, 0.6f), 9f, 1.1f);
            }

            // 경계벽 — 없으면 바닥(Plane) 가장자리를 넘는 순간 그대로 떨어진다. Update의 Y<-3
            // 안전망이 "바닥 밖으로 떨어졌다" 경고와 함께 메인 월드로 강제 퇴장시키므로, 25m
            // 자동 이탈은 발동조차 못 한다. 나머지 9개 방은 예외 없이 전부 봉해 두었다.
            CreateBoundaryWalls(shelfMat, 13f, 5f);
        }

        /// <summary>빙하 서고(frostline_archive) — 얼음 기둥 속에 장부가 얼어붙어 있다.</summary>
        private void BuildIceArchive(SubAreaData sub)
        {
            Material floorMat = Mat(new Color(0.62f, 0.72f, 0.80f));
            Material iceMat = Mat(new Color(0.70f, 0.84f, 0.92f, 0.55f));
            SetTransparent(iceMat);
            Material deepMat = Mat(new Color(0.42f, 0.58f, 0.72f));
            Material paperMat = Mat(new Color(0.86f, 0.82f, 0.68f));
            Material glowMat = Mat(new Color(0.55f, 0.85f, 1f));

            CreateFloor(floorMat, 30f);

            // 얼음 기둥 8개 — 안에 장부가 한 권씩 갇혀 있다.
            for (int i = 0; i < 8; i++)
            {
                // 반칸(22.5도) 밀어 놓는다 — i * 45도면 i=6이 정확히 270도, 곧 (0, 0, -8)에 선다.
                // 그 좌표가 `FindSafeSpawnPosition`의 1차 입구 자리이고 기둥은 반지름 1.25의
                // 실콜라이더라, 이 방만 입구가 막혀 spiral 폴백으로 3m 밀려난 데서 등장했다.
                float deg = (i + 0.5f) * (360f / 8f);
                float a = deg * Mathf.Deg2Rad;
                Vector3 at = new Vector3(Mathf.Cos(a) * 8f, 0f, Mathf.Sin(a) * 8f);

                GameObject column = Prim(PrimitiveType.Cylinder, $"IceColumn_{i}");
                column.transform.localPosition = at + new Vector3(0f, 2.0f, 0f);
                column.transform.localScale = new Vector3(1.25f, 2.0f, 1.25f);
                Apply(column, iceMat);

                GameObject ledger = Prim(PrimitiveType.Cube, $"FrozenLedger_{i}");
                ledger.transform.localPosition = at + new Vector3(0f, 1.7f, 0f);
                // 극좌표 배치물의 접선 정렬 관례는 `-(각도 + 90)`이다(울타리·육각벽과 동일).
                // 장부는 두께 0.16의 판이라 이걸 어기면 방 한가운데서 보이는 게 옆날뿐이다 —
                // 옛 `i * 24f`는 배치각과 아무 관계가 없는 값이었다.
                ledger.transform.localRotation = Quaternion.Euler(0f, -(deg + 90f), 8f);
                ledger.transform.localScale = new Vector3(0.7f, 0.9f, 0.16f);
                Apply(ledger, paperMat);
            }

            // 가운데 균열 — 바닥이 갈라져 아래에서 빛이 샌다.
            for (int i = 0; i < 5; i++)
            {
                GameObject crack = Prim(PrimitiveType.Cube, $"IceCrack_{i}");
                crack.transform.localPosition = new Vector3(0f, 0.03f, -6f + i * 3f);
                crack.transform.localRotation = Quaternion.Euler(0f, (i % 2 == 0 ? 12f : -9f), 0f);
                crack.transform.localScale = new Vector3(0.35f, 0.02f, 3.2f);
                Apply(crack, glowMat);
            }

            GameObject core = Prim(PrimitiveType.Sphere, "ArchiveGlow");
            core.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            core.transform.localScale = Vector3.one * 1.1f;
            Apply(core, glowMat);
            CreatePointLight(new Vector3(0f, 1.2f, 0f), new Color(0.6f, 0.85f, 1f), 14f, 1.5f);

            // 매달린 고드름
            for (int i = 0; i < 10; i++)
            {
                GameObject spike = Prim(PrimitiveType.Cylinder, $"Icicle_{i}");
                spike.transform.localPosition = new Vector3(-10f + i * 2.2f, 4.2f, (i % 2 == 0 ? 4f : -4f));
                spike.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
                spike.transform.localScale = new Vector3(0.22f, 0.8f + (i % 3) * 0.3f, 0.22f);
                Apply(spike, deepMat);
            }

            // 경계벽 — 없으면 바닥(Plane) 가장자리를 넘는 순간 그대로 떨어진다. Update의 Y<-3
            // 안전망이 "바닥 밖으로 떨어졌다" 경고와 함께 메인 월드로 강제 퇴장시키므로, 25m
            // 자동 이탈은 발동조차 못 한다. 나머지 9개 방은 예외 없이 전부 봉해 두었다.
            CreateBoundaryWalls(deepMat, 12f, 6f);
        }

        /// <summary>잿불 가마(emberfall_kiln) — 용암 균열과 달군 가마. 열기가 보이는 방.</summary>
        private void BuildEmberKiln(SubAreaData sub)
        {
            Material floorMat = Mat(new Color(0.16f, 0.13f, 0.12f));
            Material basaltMat = Mat(new Color(0.22f, 0.19f, 0.20f));
            Material lavaMat = Mat(new Color(0.95f, 0.35f, 0.10f));
            Material emberMat = Mat(new Color(1f, 0.55f, 0.18f));

            CreateFloor(floorMat, 30f);

            // 가마 3기 — 원통 몸체 + 달아오른 아궁이.
            for (int i = 0; i < 3; i++)
            {
                float x = -8f + i * 8f;
                GameObject body = Prim(PrimitiveType.Cylinder, $"Kiln_{i}");
                body.transform.localPosition = new Vector3(x, 1.6f, 6f);
                body.transform.localScale = new Vector3(2.1f, 1.6f, 2.1f);
                Apply(body, basaltMat);

                GameObject mouth = Prim(PrimitiveType.Sphere, $"KilnMouth_{i}");
                mouth.transform.localPosition = new Vector3(x, 1.0f, 4.6f);
                mouth.transform.localScale = new Vector3(1.1f, 0.9f, 0.6f);
                Apply(mouth, lavaMat);
                CreatePointLight(new Vector3(x, 1.0f, 4.6f), new Color(1f, 0.45f, 0.15f), 11f, 2.0f);
            }

            // 용암 균열 — 바닥을 가로지른다.
            for (int i = 0; i < 6; i++)
            {
                GameObject vein = Prim(PrimitiveType.Cube, $"LavaVein_{i}");
                vein.transform.localPosition = new Vector3(-10f + i * 4f, 0.03f, -4f + (i % 3) * 2.5f);
                vein.transform.localRotation = Quaternion.Euler(0f, 20f + i * 25f, 0f);
                vein.transform.localScale = new Vector3(0.5f, 0.02f, 5.5f + (i % 2) * 2f);
                Apply(vein, lavaMat);
            }

            // 굳은 슬래그 더미
            for (int i = 0; i < 7; i++)
            {
                GameObject slag = Prim(PrimitiveType.Cube, $"Slag_{i}");
                slag.transform.localPosition = new Vector3(-9f + i * 3f, 0.35f, -9f);
                slag.transform.localRotation = Quaternion.Euler(0f, i * 33f, i % 2 == 0 ? 7f : -6f);
                slag.transform.localScale = new Vector3(1.4f, 0.7f, 1.2f);
                Apply(slag, i % 3 == 0 ? emberMat : basaltMat);
            }

            // 경계벽 — 없으면 바닥(Plane) 가장자리를 넘는 순간 그대로 떨어진다. Update의 Y<-3
            // 안전망이 "바닥 밖으로 떨어졌다" 경고와 함께 메인 월드로 강제 퇴장시키므로, 25m
            // 자동 이탈은 발동조차 못 한다. 나머지 9개 방은 예외 없이 전부 봉해 두었다.
            CreateBoundaryWalls(basaltMat, 14f, 5f);
        }

        /// <summary>
        /// 장부의 방(nameless_ledger) — 최종장의 무대. 텅 빈 서가와 바닥에 새겨진 이름들,
        /// 그리고 아직 채워지지 않은 빈칸 하나.
        /// </summary>
        private void BuildLedgerHall(SubAreaData sub)
        {
            Material floorMat = Mat(new Color(0.14f, 0.13f, 0.17f));
            Material stoneMat = Mat(new Color(0.28f, 0.27f, 0.32f));
            Material nameMat = Mat(new Color(0.72f, 0.70f, 0.62f));
            Material voidMat = Mat(new Color(0.04f, 0.03f, 0.06f));
            Material glowMat = Mat(new Color(0.85f, 0.82f, 0.60f));

            CreateFloor(floorMat, 32f);

            // 양옆 서가 — 칸은 있는데 대부분 비었다.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 5; i++)
                {
                    GameObject shelf = Prim(PrimitiveType.Cube, $"Rack_{side}_{i}");
                    shelf.transform.localPosition = new Vector3(side * 9f, 1.9f, -8f + i * 4f);
                    shelf.transform.localScale = new Vector3(0.6f, 3.8f, 3.0f);
                    Apply(shelf, stoneMat);

                    // 남은 장부 몇 권 — 드문드문.
                    if (i % 2 == 0)
                    {
                        GameObject book = Prim(PrimitiveType.Cube, $"Ledger_{side}_{i}");
                        book.transform.localPosition = new Vector3(side * 8.5f, 2.3f, -8f + i * 4f);
                        book.transform.localScale = new Vector3(0.22f, 0.8f, 1.6f);
                        Apply(book, nameMat);
                    }
                }
            }

            // 바닥에 새겨진 이름 줄 — 가운데로 갈수록 촘촘하다.
            for (int i = 0; i < 12; i++)
            {
                GameObject line = Prim(PrimitiveType.Cube, $"NameLine_{i}");
                line.transform.localPosition = new Vector3(0f, 0.02f, -9f + i * 1.6f);
                line.transform.localScale = new Vector3(5.5f - Mathf.Abs(i - 6) * 0.3f, 0.015f, 0.12f);
                Apply(line, nameMat);
            }

            // 빈칸 — 이름이 들어갈 자리 하나가 비어 있다. 이 방의 요점.
            GameObject blank = Prim(PrimitiveType.Cube, "TheBlank");
            blank.transform.localPosition = new Vector3(0f, 0.04f, 10f);
            blank.transform.localScale = new Vector3(3.2f, 0.03f, 2.0f);
            Apply(blank, voidMat);

            GameObject rim = Prim(PrimitiveType.Cube, "BlankRim");
            rim.transform.localPosition = new Vector3(0f, 0.03f, 10f);
            rim.transform.localScale = new Vector3(3.6f, 0.02f, 2.4f);
            Apply(rim, glowMat);

            GameObject lamp = Prim(PrimitiveType.Sphere, "HallLight");
            lamp.transform.localPosition = new Vector3(0f, 4.2f, 2f);
            lamp.transform.localScale = Vector3.one * 0.6f;
            Apply(lamp, glowMat);
            CreatePointLight(new Vector3(0f, 4.2f, 2f), new Color(0.95f, 0.90f, 0.70f), 16f, 1.2f);

            // 경계벽 — 없으면 바닥(Plane) 가장자리를 넘는 순간 그대로 떨어진다. Update의 Y<-3
            // 안전망이 "바닥 밖으로 떨어졌다" 경고와 함께 메인 월드로 강제 퇴장시키므로, 25m
            // 자동 이탈은 발동조차 못 한다. 나머지 9개 방은 예외 없이 전부 봉해 두었다.
            CreateBoundaryWalls(stoneMat, 13f, 6f);
        }

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
            runtimeMaterials.Add(mat);   // 빌드 파기 때 함께 정리 — 안 하면 진입할 때마다 41개씩 샌다
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
