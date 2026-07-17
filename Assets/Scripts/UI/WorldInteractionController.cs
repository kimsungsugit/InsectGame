using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.NPC;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 월드 상호작용 컨트롤러 — 마을 건물(상점/훈련소/랜덤상자) + 주민 대화의 근접 스캔/발동.
    /// 0.15s 간격 스캔, E키 또는 모바일 원형 버튼으로 발동 (CaptureInputController 패턴의 자체 최소 구현).
    /// HasPriorityTarget: 대상이 최근접 곤충보다 가까우면 true — 잡기(E) 입력과의 우선순위 조정에
    /// CaptureInputController가 소비(메인 모델 통합). 매 스캔 시 갱신·캐시.
    /// 계획상 Core였으나 NPC/UI 참조가 필요해 UI 계층에 배치 (UI → {NPC, Core, Spawning} 허용).
    /// </summary>
    public class WorldInteractionController : MonoBehaviour
    {
        private const float ScanInterval = 0.15f;
        private const float VillagerTalkRadius = 3f;

        private CashShopUI shop;
        private TrainingUI training;
        private PlayerMovement playerMovement;
        private NpcDialogueUI dialogue;
        private InsectSpawner spawner;
        private NpcManager npcManager;

        private readonly List<InteractionPointDef> points = new List<InteractionPointDef>();

        private float scanTimer;
        private InteractionPointDef currentPoint;   // 현재 대상 건물 (주민이 더 가까우면 null)
        private VillagerNpc currentVillager;        // 현재 대상 주민 (건물이 더 가까우면 null)
        private float currentTargetDistance;
        private bool hasPriorityTarget;             // 스캔 시 갱신·캐시 (프로퍼티 계산 금지)
        private string promptText = string.Empty;   // OnGUI 매 프레임 문자열 보간 할당 회피 — 스캔 시 캐시
        private string buttonText = string.Empty;

        // OnGUI 캐시
        private GUIStyle promptStyle;
        private GUIStyle buttonLabelStyle;
        private Texture2D circleFillTex;
        private Texture2D circleRingTex;
        private Rect interactButtonRect;            // 모바일 raw 터치 히트테스트용 (직전 OnGUI 갱신)

        /// <summary>현재 대상(건물/주민)이 존재하고 최근접 곤충보다 가까우면 true.</summary>
        public bool HasPriorityTarget => hasPriorityTarget;

        public void AutoWire(CashShopUI cashShop, TrainingUI trainingUi, PlayerMovement player)
        {
            if (shop == null) shop = cashShop;
            if (training == null) training = trainingUi;
            if (playerMovement == null) playerMovement = player;
        }

        public void AutoWire(NpcDialogueUI dialogueUi)
        {
            if (dialogue == null) dialogue = dialogueUi;
        }

        public void AutoWire(InsectSpawner insectSpawner)
        {
            if (spawner == null) spawner = insectSpawner;
        }

        public void AutoWire(NpcManager manager)
        {
            if (npcManager == null) npcManager = manager;
        }

        /// <summary>VillageBuilder가 생성한 상호작용 지점 등록 — 부트스트랩이 호출.</summary>
        public void RegisterPoints(List<InteractionPointDef> defs)
        {
            if (defs == null) return;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i] != null) points.Add(defs[i]);
            }
        }

        private void Update()
        {
            // 발동 판정은 '프레임 시작 시점' 스냅샷으로 — 같은 프레임의 Scan()이 값을 뒤집으면
            // CaptureInputController(잡기)가 이미 소비한 동일 물리 E 입력을 여기서 또 읽어
            // 포획 시도 + 상호작용이 동시에 발화하는 이중 발화가 생긴다 (실행 순서 미지정).
            bool hadPriority = hasPriorityTarget;

            scanTimer -= Time.deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = ScanInterval;
                Scan();
            }

            if (!hadPriority) return;

            // E키 발동 — 곤충이 더 가까우면 hasPriorityTarget=false라 잡기(CaptureInputController)에 양보
            if (Input.GetKeyDown(KeyCode.E))
                Activate();

            // 모바일: 두 번째 손가락 raw 터치 (조이스틱이 첫 손가락 점유 시 합성 마우스 미동작 우회)
            if (Input.touchSupported && interactButtonRect.width > 0f
                && FieldHudInput.TryGetTapInVirtualRect(interactButtonRect))
                Activate();
        }

        // ── 0.15s 간격 스캔: 최근접 건물 + 대화 가능 최근접 주민 + 곤충 거리 비교 ──
        private void Scan()
        {
            currentPoint = null;
            currentVillager = null;
            hasPriorityTarget = false;

            if (playerMovement == null) return;
            // 차단 조건: 모달 열림 또는 플레이어 frozen (anyBlockingUI 패턴의 최소 구현)
            if (ModalUIRegistry.IsAnyOpen() || playerMovement.IsFrozen) return;

            Vector3 playerPos = playerMovement.transform.position;

            // 최근접 상호작용 포인트 (각자 radius 내)
            InteractionPointDef bestPoint = null;
            float bestPointDist = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                InteractionPointDef def = points[i];
                float d = Vector3.Distance(playerPos, def.worldPosition);
                if (d <= def.radius && d < bestPointDist)
                {
                    bestPointDist = d;
                    bestPoint = def;
                }
            }

            // 대화 가능 최근접 주민 (반경 3m)
            VillagerNpc bestVillager = null;
            float bestVillagerDist = float.MaxValue;
            if (npcManager != null)
            {
                IReadOnlyList<VillagerNpc> villagers = npcManager.Villagers;
                for (int i = 0; i < villagers.Count; i++)
                {
                    VillagerNpc v = villagers[i];
                    if (v == null || !v.CanTalk || !v.gameObject.activeInHierarchy) continue;
                    float d = Vector3.Distance(playerPos, v.transform.position);
                    if (d <= VillagerTalkRadius && d < bestVillagerDist)
                    {
                        bestVillagerDist = d;
                        bestVillager = v;
                    }
                }
            }

            // 더 가까운 쪽을 단일 대상으로
            if (bestPoint == null && bestVillager == null) return;
            if (bestVillager != null && (bestPoint == null || bestVillagerDist <= bestPointDist))
            {
                currentVillager = bestVillager;
                currentTargetDistance = bestVillagerDist;
                promptText = $"[E] 대화: {bestVillager.DisplayName}";
                buttonText = "대화";
            }
            else
            {
                currentPoint = bestPoint;
                currentTargetDistance = bestPointDist;
                promptText = $"[E] {bestPoint.label}";
                buttonText = ShortButtonLabel(bestPoint.kind);
            }

            // 최근접 곤충 거리 비교 — 곤충이 더 가까우면 잡기 우선(HasPriorityTarget=false)
            float nearestInsectDist = float.MaxValue;
            if (spawner != null)
            {
                IReadOnlyList<InsectEntity> insects = spawner.ActiveInsects;
                for (int i = 0; i < insects.Count; i++)
                {
                    InsectEntity e = insects[i];
                    if (e == null || !e.gameObject.activeInHierarchy || !e.CanBeEngaged) continue;
                    float d = Vector3.Distance(playerPos, e.transform.position);
                    if (d < nearestInsectDist) nearestInsectDist = d;
                }
            }
            hasPriorityTarget = currentTargetDistance < nearestInsectDist;
        }

        private void Activate()
        {
            // 스캔 간격(0.15s) 사이에 모달이 열렸을 수 있음 — 발동 직전 재확인
            if (ModalUIRegistry.IsAnyOpen()) return;
            if (playerMovement != null && playerMovement.IsFrozen) return;

            if (currentVillager != null)
            {
                if (dialogue != null) dialogue.Show(currentVillager);
            }
            else if (currentPoint != null)
            {
                switch (currentPoint.kind)
                {
                    case InteractionKind.ItemShop:
                        if (shop != null) shop.OpenAtTab(1);
                        break;
                    case InteractionKind.Gacha:
                        if (shop != null) shop.OpenAtTab(2);
                        break;
                    case InteractionKind.Training:
                        if (training != null) training.Toggle();
                        break;
                }
            }

            // 발동 후 즉시 재스캔 (모달이 열렸으면 대상 해제 → 프롬프트/버튼 숨김)
            currentPoint = null;
            currentVillager = null;
            hasPriorityTarget = false;
            scanTimer = 0f;
        }

        private void OnGUI()
        {
            if (!hasPriorityTarget) return;
            if (ModalUIRegistry.IsAnyOpen()) return;

            EnsureStyles();
            EnsureCircleTex();

            UIScale.Begin();

            float vw = UIScale.VirtualScreenWidth;
            float vh = UIScale.VirtualScreenHeight;
            float safeR = UIScale.VirtualSafeRight;
            float safeB = UIScale.VirtualSafeBottom;

            // 프롬프트 — 화면 하단 중앙에서 좌측 오프셋 (잡기 버튼/미스 피드백과 겹침 회피)
            GUI.Label(new Rect(vw / 2f - 560f, vh - safeB - 96f, 640f, 44f), promptText, promptStyle);

            // 원형 상호작용 버튼 — 잡기 버튼(우하단, 반경 96) 왼쪽에 배치
            float radius = 80f;
            float accountClear = 92f / UIScale.Scale; // CaptureInputController와 동일한 '계정' 버튼 회피 보정
            float catchCx = vw - safeR - 96f - 40f;   // 잡기 버튼 중심 X (DrawCatchButton과 동기)
            float cx = catchCx - 96f - radius - 36f;  // 잡기 버튼 왼쪽
            float cy = vh - safeB - 96f - accountClear; // 잡기 버튼과 같은 높이(중심 정렬)
            Rect rect = new Rect(cx - radius, cy - radius, radius * 2f, radius * 2f);
            interactButtonRect = rect;
            FieldHudInput.RegisterBlockingRect(rect); // 버튼 위 탭의 클릭-이동 오발 차단

            Color baseCol = new Color(0.45f, 0.85f, 1f);
            GUI.color = new Color(baseCol.r * 0.22f, baseCol.g * 0.22f, baseCol.b * 0.22f, 0.92f);
            GUI.DrawTexture(rect, circleFillTex);
            GUI.color = new Color(baseCol.r, baseCol.g, baseCol.b, 0.7f + 0.3f * Mathf.Sin(Time.time * 5f));
            GUI.DrawTexture(rect, circleRingTex);
            GUI.color = Color.white;
            GUI.Label(rect, buttonText, buttonLabelStyle);

            // 데스크탑(마우스): GUI.Button으로 처리. 터치 기기는 Update의 raw 히트테스트가 처리
            // (이중 발화 회피 — CaptureInputController.DrawCatchButton 패턴)
            if (!Input.touchSupported && GUI.Button(rect, GUIContent.none, GUIStyle.none))
                Activate();

            UIScale.End();
        }

        private static string ShortButtonLabel(InteractionKind kind)
        {
            switch (kind)
            {
                case InteractionKind.ItemShop: return "상점";
                case InteractionKind.Gacha: return "상자";
                case InteractionKind.Training: return "훈련";
                default: return "확인";
            }
        }

        private void EnsureStyles()
        {
            if (promptStyle != null) return;

            promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            promptStyle.normal.textColor = new Color(0.75f, 0.95f, 1f);

            buttonLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            buttonLabelStyle.normal.textColor = Color.white;
        }

        private void EnsureCircleTex()
        {
            if (circleFillTex == null) circleFillTex = MakeCircle(128, false);
            if (circleRingTex == null) circleRingTex = MakeCircle(128, true);
        }

        // 원형 텍스처 1회 생성 (CaptureInputController.MakeCircle 패턴)
        private static Texture2D MakeCircle(int size, bool ring)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a;
                    if (ring) a = (d > 0.86f && d <= 1f) ? 1f : 0f;
                    else a = d <= 1f ? 1f : 0f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            t.Apply();
            return t;
        }
    }
}
