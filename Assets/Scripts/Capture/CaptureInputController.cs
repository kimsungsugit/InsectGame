using InsectGame.Dex;
using InsectGame.Core;
using InsectGame.Spawning;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Capture
{
    public class CaptureInputController : MonoBehaviour
    {
        [SerializeField] private CaptureTriggerModeController modeController;
        [SerializeField] private CaptureRaycastTrigger raycastTrigger;
        [SerializeField] private CaptureProximityTrigger proximityTrigger;
        [SerializeField] private CaptureMinigameController minigame;
        [SerializeField] private CaptureChoiceUI choiceUi;
        [SerializeField] private BattleScreenUI battleScreen;
        [SerializeField] private RaidBattleUI raidScreen;
        [SerializeField] private DexScreenUI dexScreen;
        // 건물/NPC 상호작용이 곤충보다 가까우면 E키를 양보 (이중 발화 차단)
        [SerializeField] private WorldInteractionController worldInteractions;

        [Header("Wild Encounter")]
        [Range(0.05f, 1f)] [SerializeField] private float baseApproachSuccessChance = 0.55f; // 채 휘두르기 연결 기본 확률(0.45→0.55 상향)
        [Range(0f, 0.25f)] [SerializeField] private float rarityApproachPenalty = 0.08f;
        [Range(0f, 0.25f)] [SerializeField] private float distancePenaltyPerMeter = 0.07f;
        [SerializeField] private float idealApproachDistance = 1.2f;

        private InsectEntity nearestInsect;
        private float nearCheckTimer;
        private float attemptCooldown;
        private float feedbackTimer;
        private string feedbackMessage;
        private PlayerMovement playerMovement;
        private GUIStyle captureButtonStyle;
        private GUIStyle feedbackStyle;
        private GUIStyle catchLabelStyle;
        private GUIStyle missStyle;          // 미스! 강조 표시(붉고 큼)
        private bool feedbackIsMiss;         // 현재 피드백이 미스인지(스타일 분기)
        private float swingTimer;            // 탭 직후 스윙 액션 연출(초)
        private Texture2D circleFillTex;     // 원형 버튼 채움
        private Texture2D circleRingTex;     // 원형 버튼 링
        private Rect catchButtonRect;        // 직전 OnGUI에서 갱신된 잡기 버튼 가상 rect(멀티터치 raw 히트테스트용)

        /// <summary>
        /// 지금 [E]가 포획으로 갈 대상이 있는가. <see cref="WorldInteractionController.HasPriorityTarget"/>과
        /// 같은 성격의 신호다 — 같은 키를 노리는 다른 시스템이 양보 여부를 판단하는 데 쓴다.
        /// 0.15초 간격 스캔 결과를 그대로 읽는다(프로퍼티에서 계산하지 않는다).
        /// </summary>
        public bool HasCatchTarget => nearestInsect != null && nearestInsect.Data != null;

        private void Update()
        {
            if (attemptCooldown > 0f) attemptCooldown -= Time.deltaTime;
            if (feedbackTimer > 0f) feedbackTimer -= Time.deltaTime;
            if (swingTimer > 0f) swingTimer -= Time.deltaTime;

            nearCheckTimer -= Time.deltaTime;
            if (nearCheckTimer <= 0f)
            {
                nearestInsect = FindNearestInsect();
                nearCheckTimer = 0.15f;
            }

            bool anyBlockingUI = (minigame != null && minigame.IsActive)
                || (choiceUi != null && choiceUi.IsChoiceOpen)
                || (battleScreen != null && battleScreen.IsBattleActive)
                || (raidScreen != null && raidScreen.IsRaidActive)
                || (dexScreen != null && dexScreen.IsOpen)
                || ModalUIRegistry.IsAnyOpen()
                || IsPlayerFrozen();

            if (Input.GetKeyDown(KeyCode.E) && !anyBlockingUI
                && (worldInteractions == null || !worldInteractions.HasPriorityTarget))
                TryStartCapture();

            // 멀티터치 잡기 — 가상 조이스틱이 첫 손가락을 점유 중이면 IMGUI 합성 마우스로는 잡기 버튼이
            // 안 눌린다. 두 번째 손가락의 raw 터치를 잡기 버튼 영역(직전 OnGUI 갱신)에서 직접 감지해 우회.
            // (데스크탑은 DrawCatchButton의 GUI.Button이 처리하므로 터치 지원 기기에서만)
            if (!anyBlockingUI && Input.touchSupported && catchButtonRect.width > 0f
                && FieldHudInput.TryGetTapInVirtualRect(catchButtonRect))
                TriggerCatchTap();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (choiceUi != null && choiceUi.IsChoiceOpen)
                    choiceUi.Hide();
                else if (minigame != null && minigame.IsActive)
                    minigame.CancelCapture();
            }
        }

        private void OnGUI()
        {
            bool anyUI = (minigame != null && minigame.IsActive)
                || (choiceUi != null && choiceUi.IsChoiceOpen)
                || (battleScreen != null && battleScreen.IsBattleActive)
                || (raidScreen != null && raidScreen.IsRaidActive)
                || (dexScreen != null && dexScreen.IsOpen)
                || ModalUIRegistry.IsAnyOpen()
                || IsPlayerFrozen();

            Event evt = Event.current;
            if (evt != null && evt.type == EventType.KeyDown)
            {
                if (evt.keyCode == KeyCode.E && !anyUI
                    && (worldInteractions == null || !worldInteractions.HasPriorityTarget))
                {
                    TryStartCapture();
                    evt.Use();
                }
                if (evt.keyCode == KeyCode.Escape)
                {
                    if (choiceUi != null && choiceUi.IsChoiceOpen)
                        choiceUi.Hide();
                    else if (minigame != null && minigame.IsActive)
                        minigame.CancelCapture();
                    evt.Use();
                }
            }

            if (anyUI) return;

            EnsureStyles();
            EnsureCircleTex();

            UIScale.Begin();
            DrawCatchButton();
            UIScale.End();
        }

        // 우측 하단 원형 '잡기' 버튼 — 곤충 가까이 가면 활성(레어도색·펄스), 멀면 흐릿. 연타 가능.
        private void DrawCatchButton()
        {
            bool near = nearestInsect != null && nearestInsect.Data != null;

            float vw = UIScale.VirtualScreenWidth;
            float safeR = UIScale.VirtualSafeRight;
            float radius = 96f;
            // '계정' 버튼(AccountSettingsUI)은 raw 픽셀로 하단 62px에 고정 — 스케일이 작을수록 가상좌표와
            // 어긋나 겹친다. 가상 여백을 1/Scale로 환산해 화면 스케일과 무관하게 항상 그 위로 띄운다.
            float accountClear = 92f / UIScale.Scale;
            float cx = vw - safeR - radius - 40f;
            float cy = UISafeLayout.ContentBottom - radius - accountClear; // 우하단 '계정' 버튼 위(스케일 보정)
            Rect rect = new Rect(cx - radius, cy - radius, radius * 2f, radius * 2f);
            catchButtonRect = rect; // 멀티터치 raw 히트테스트 + 클릭-이동 억제용으로 공유
            // PlayerMovement 클릭-이동이 이 버튼 위 탭을 월드 클릭으로 오인하지 않게 등록.
            FieldHudInput.RegisterBlockingRect(rect);

            Color baseCol = near
                ? UITheme.Instance.GetInsectRarityColor(nearestInsect.Data.rarity)
                : new Color(0.5f, 0.55f, 0.55f);
            float ringA = near ? (0.7f + 0.3f * Mathf.Sin(Time.time * 5f)) : 0.4f;

            // 스윙 액션 — 탭 직후 확장 링(휙!)
            if (swingTimer > 0f)
            {
                float t = 1f - Mathf.Clamp01(swingTimer / 0.35f); // 0→1
                float er = radius * (1f + t * 0.85f);
                GUI.color = new Color(baseCol.r, baseCol.g, baseCol.b, (1f - t) * 0.65f);
                GUI.DrawTexture(new Rect(cx - er, cy - er, er * 2f, er * 2f), circleRingTex);
            }

            // 채움 + 링
            GUI.color = new Color(baseCol.r * 0.22f, baseCol.g * 0.22f, baseCol.b * 0.22f, near ? 0.92f : 0.5f);
            GUI.DrawTexture(rect, circleFillTex);
            GUI.color = new Color(baseCol.r, baseCol.g, baseCol.b, ringA);
            GUI.DrawTexture(rect, circleRingTex);

            // 라벨
            catchLabelStyle.normal.textColor = near ? Color.white : new Color(0.75f, 0.78f, 0.78f);
            GUI.color = Color.white;
            GUI.Label(rect, near ? "잡기" : "잡기\n<size=20>가까이</size>", catchLabelStyle);

            // 피드백(버튼 위) — 미스는 크고 붉게, 그 외는 일반 안내.
            if (feedbackTimer > 0f && !string.IsNullOrEmpty(feedbackMessage))
            {
                GUIStyle fs = feedbackIsMiss ? missStyle : feedbackStyle;
                float fh = feedbackIsMiss ? 72f : 52f;
                GUI.Label(new Rect(cx - 260f, cy - radius - fh - 16f, 520f, fh), feedbackMessage, fs);
            }

            // 입력(투명 히트영역) — 데스크탑(마우스)은 GUI.Button으로 처리. 터치 기기는 Update의 raw
            // 히트테스트가 처리하므로 GUI.Button(합성 마우스)을 쓰지 않아 이중 발화/멀티터치 누락을 회피.
            if (!Input.touchSupported && GUI.Button(rect, GUIContent.none, GUIStyle.none))
                TriggerCatchTap();

            GUI.color = Color.white;
        }

        private void TriggerCatchTap()
        {
            swingTimer = 0.35f;
            // 캐릭터가 곤충 쪽을 향해 도구를 휙 휘두르는 액션.
            PlayerMovement pm = GetPlayerMovement();
            if (pm != null)
            {
                if (nearestInsect != null) pm.FaceTowards(nearestInsect.transform.position);
                pm.PlayCatchSwing();
            }
            TryStartCapture();
        }

        public void TryStartCapture()
        {
            if (attemptCooldown > 0f) return;
            attemptCooldown = 0.25f; // 연타(막 누르기) 허용

            InsectEntity target = nearestInsect ?? FindNearestInsect();
            if (target == null || !target.CanBeEngaged)
            {
                ShowFeedback("근처에 잡을 수 있는 곤충이 없습니다. 천천히 다가가세요.");
                nearestInsect = null;
                return;
            }

            float distance = Vector3.Distance(proximityTrigger.transform.position, target.transform.position);
            float chance = CalculateApproachChance(target, distance);
            if (Random.value > chance)
            {
                // 연타로 다시 시도할 수 있게 도망가지 않고 근접 유지(막 누르기). 실제 난이도는 미니게임에서.
                ShowFeedback("미스! 다시 시도하세요.", true);
                return;
            }

            if (choiceUi != null)
                choiceUi.ShowChoice(target);
            else if (minigame != null)
                minigame.StartMinigame(target);
        }

        private InsectEntity FindNearestInsect()
        {
            if (proximityTrigger == null) return null;

            InsectEntity[] allInsects = FindObjectsByType<InsectEntity>(FindObjectsSortMode.None);
            Vector3 origin = proximityTrigger.transform.position;
            float bestDist = float.MaxValue;
            InsectEntity best = null;
            float radius = 8f;

            SphereCollider col = proximityTrigger.GetComponent<SphereCollider>();
            if (col != null) radius = col.radius;

            foreach (var e in allInsects)
            {
                if (e == null || !e.gameObject.activeInHierarchy || !e.CanBeEngaged) continue;
                float d = Vector3.Distance(origin, e.transform.position);
                if (d <= radius && d < bestDist)
                {
                    bestDist = d;
                    best = e;
                }
            }
            return best;
        }

        private float CalculateApproachChance(InsectEntity target, float distance)
        {
            int rarity = target != null && target.Data != null ? (int)target.Data.rarity : 0;
            float chance = baseApproachSuccessChance - rarity * rarityApproachPenalty;
            chance -= Mathf.Max(0f, distance - idealApproachDistance) * distancePenaltyPerMeter;
            return Mathf.Clamp(chance, 0.08f, 0.65f);   // 상한 0.55→0.65 (base 상향이 잘리지 않게)
        }

        private PlayerMovement GetPlayerMovement()
        {
            if (playerMovement == null && proximityTrigger != null)
                playerMovement = proximityTrigger.GetComponentInParent<PlayerMovement>();
            return playerMovement;
        }

        private bool IsPlayerFrozen()
        {
            PlayerMovement pm = GetPlayerMovement();
            return pm != null && pm.IsFrozen;
        }

        private void ShowFeedback(string message, bool isMiss = false)
        {
            feedbackMessage = message;
            feedbackTimer = 2.2f;
            feedbackIsMiss = isMiss;
        }

        private void EnsureStyles()
        {
            if (captureButtonStyle != null) return;

            captureButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 27,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            captureButtonStyle.normal.textColor = new Color(0.9f, 1f, 0.9f);
            captureButtonStyle.hover.textColor = new Color(0.6f, 1f, 0.7f);
            captureButtonStyle.active.textColor = Color.white;

            feedbackStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            feedbackStyle.normal.textColor = new Color(1f, 0.85f, 0.45f);

            catchLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            catchLabelStyle.normal.textColor = Color.white;

            missStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            missStyle.normal.textColor = new Color(1f, 0.32f, 0.3f);
        }

        private void EnsureCircleTex()
        {
            if (circleFillTex == null) circleFillTex = MakeCircle(128, false);
            if (circleRingTex == null) circleRingTex = MakeCircle(128, true);
        }

        // 런타임 Texture2D는 씬 재로드로 사라지지 않는다 — 이 필드만 참조하는 언매니지드
        // 객체라 파기하지 않으면 재로드마다 쌓인다(WorldInteractionController와 같은 계열).
        private void OnDestroy()
        {
            if (circleFillTex != null) Destroy(circleFillTex);
            if (circleRingTex != null) Destroy(circleRingTex);
            circleFillTex = null;
            circleRingTex = null;
        }

        // 원형 텍스처 1회 생성. ring=true면 테두리 강조(링), false면 꽉 찬 원(채움).
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

        public void AutoWire(CaptureTriggerModeController controller, CaptureRaycastTrigger raycast, CaptureProximityTrigger proximity)
        {
            if (modeController == null) modeController = controller;
            if (raycastTrigger == null) raycastTrigger = raycast;
            if (proximityTrigger == null) proximityTrigger = proximity;
        }

        public void AutoWire(WorldInteractionController interactions)
        {
            if (worldInteractions == null) worldInteractions = interactions;
        }

        public void AutoWire(CaptureChoiceUI choice)
        {
            if (choiceUi == null) choiceUi = choice;
        }

        public void AutoWire(BattleScreenUI battle, RaidBattleUI raid, DexScreenUI dex)
        {
            if (battleScreen == null) battleScreen = battle;
            if (raidScreen == null) raidScreen = raid;
            if (dexScreen == null) dexScreen = dex;
        }
    }
}
