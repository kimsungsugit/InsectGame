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

        [Header("Ambush")]
        // 습격 판정에 쓰는 시간·날씨(플레이어가 있는 리전 기준)와 서브에리어 여부. AutoWire(WorldStateProvider, RegionManager)로 받는다.
        [SerializeField] private WorldStateProvider worldStateProvider;
        [SerializeField] private RegionManager regionManager;

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

        // ── 습격(AmbushRules) ──
        // 습격형 곤충이 "지금 덤벼들어도 되나"를 묻는 판정(InsectEntity.AmbushGate)을 여기서 세우고, 닿으면 「습격!」 창을 연다.
        // 판정의 전역 칸(시간·날씨·꿈·서브에리어·멈춤·창·교전·쿨다운·배틀팀)은 프레임마다 한 번만 채우고 곤충마다 개체 칸만 바꾼다.
        private System.Func<InsectEntity, AmbushRefusal> ambushGate;   // 한 번만 묶는다 — OnEnable마다 새 대리자를 만들지 않게
        private bool ambushRefsSearched;
        private int ambushFrame = -1;
        private AmbushRules.Context ambushBase;
        private bool ambushStateKnown;
        private int ambushTeamFilled;
        private int ambushTeamReady;
        // 교전(선택 창·미니게임·전투·레이드)이 끝난 시각 — 전역 쿨다운의 기준. 습격으로 시작한 교전이면 습격 쿨다운도 함께 건다.
        private int encounterTrackFrame = -1;
        private bool inEncounterLastFrame;
        private bool encounterIsAmbush;
        private float lastEncounterEndTime;
        private float lastAmbushEndTime = float.NegativeInfinity;
        private bool feedbackIsWarn;          // 현재 피드백이 습격 경고인지(스타일 분기)
        private GUIStyle warnStyle;

        private void OnEnable()
        {
            // 구독은 여기서 걸고 OnDisable에서 푼다 — 꺼졌다 켜져도 되살아난다(subscription_lint).
            if (ambushGate == null) ambushGate = EvaluateAmbushGate;
            InsectEntity.AmbushGate = ambushGate;
            InsectEntity.AmbushStarted -= OnAmbushStarted;
            InsectEntity.AmbushStarted += OnAmbushStarted;
            InsectEntity.AmbushReached -= OnAmbushReached;
            InsectEntity.AmbushReached += OnAmbushReached;
        }

        private void OnDisable()
        {
            // 판정은 static이라 씬을 다시 읽어도 남는다 — 파기된 이 컴포넌트로 들어가지 않게 내 것이면 비운다(null이면 습격이 없다).
            if (InsectEntity.AmbushGate == ambushGate) InsectEntity.AmbushGate = null;
            InsectEntity.AmbushStarted -= OnAmbushStarted;
            InsectEntity.AmbushReached -= OnAmbushReached;
        }

        private void Start()
        {
            // 씬이 막 열렸을 때도 숨 돌릴 틈을 둔다 — 로드 직후 곁의 사마귀가 바로 덮치지 않게(AfterEncounterGraceSeconds).
            lastEncounterEndTime = Time.time;
        }

        private void Update()
        {
            TrackEncounter();
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
                // Hide가 아니라 CloseModal — 습격 창에서 ESC는 「닫기」가 아니라 도망치기다(그냥 닫으면 도망 판정을 우회한다).
                if (choiceUi != null && choiceUi.IsChoiceOpen)
                    choiceUi.CloseModal();
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
                        choiceUi.CloseModal();   // Update와 같은 이유 — 습격 창의 ESC는 도망치기다
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

            // 자리는 CatchButtonLayout(순수 계산) — 전수 겹침 검사(HudOverlapSweepTests)와 같은 함수다.
            HudFrame frame = HudFrame.Current;
            float radius = CatchButtonLayout.Radius;
            Vector2 center = CatchButtonLayout.Center(frame);
            float cx = center.x;
            float cy = center.y;
            Rect rect = CatchButtonLayout.ButtonRect(frame);
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
                // 이웃(상호작용 버튼·설정 버튼·피드백 글자)에 닿지 않는 반지름까지만 퍼진다(CatchButtonLayout.SwingRadius).
                float er = Mathf.Lerp(radius, CatchButtonLayout.SwingRadius(frame), t);
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
                if (feedbackIsWarn)
                {
                    // 습격 경고 — 곤충 이름이 길이를 정하므로 고정 상자에 LabelFit(ui-layout.md). 화면 오른쪽 밖으로 밀리지 않게 가둔다.
                    // 글자만 그린다(패널 없음) — 쫓기는 동안 이 자리를 탭해 달아날 수 있어야 해서 클릭-이동을 막지 않는다.
                    // 폭 520 — 가로 모바일에서 화면 가운데 줄의 섬 안내 배너(폭 760, IslandGuideUI.CoachRect)와 36px 띄운다.
                    // 560일 땐 Scale 0.667에서 배너 오른쪽 모서리와 4px 겹쳤다.
                    UIHelper.LabelFit(CatchButtonLayout.WarnRect(frame), feedbackMessage, warnStyle);
                }
                else
                {
                    GUIStyle fs = feedbackIsMiss ? missStyle : feedbackStyle;
                    GUI.Label(CatchButtonLayout.FeedbackRect(frame, feedbackIsMiss), feedbackMessage, fs);
                }
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

            // **수문장은 접근 굴림을 건너뛴다.** 이 굴림은 야생이 눈치채기 전에 다가가는 판정인데
            // (성공률에 등급 페널티가 붙는다), 수문장은 길목에 서서 기다리는 상대라 숨어들 게 없다.
            // 그대로 두면 Legendary 수문장이 하한 8%까지 떨어져 말 거는 데만 수십 번 두드려야 한다.
            if (!target.IsGuardian)
            {
                float distance = Vector3.Distance(proximityTrigger.transform.position, target.transform.position);
                float chance = CalculateApproachChance(target, distance);
                if (Random.value > chance)
                {
                    // 연타로 다시 시도할 수 있게 도망가지 않고 근접 유지(막 누르기). 실제 난이도는 미니게임에서.
                    ShowFeedback("미스! 다시 시도하세요.", true);
                    return;
                }
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
            feedbackIsWarn = false;
        }

        /// <summary>습격 경고 — 다가오는 동안(멈칫 끝 → 닿기까지 최대 수 초) 보이도록 보통 안내보다 조금 오래 둔다.</summary>
        private void ShowWarning(string message)
        {
            feedbackMessage = message;
            feedbackTimer = 3f;
            feedbackIsMiss = false;
            feedbackIsWarn = true;
        }

        // ── 습격 판정·창 ─────────────────────────────────────────────────

        /// <summary>
        /// <see cref="InsectEntity.AmbushGate"/>의 본체 — 이 곤충이 지금 덤벼들어도 되는가(막혔다면 까닭). 규칙은 <see cref="AmbushRules.Check"/>.
        /// 시간·날씨를 모르면(월드 상태 미배선) 덤벼들지 않는다 — 기본값(아침·맑음)으로 판정하면 주행성 말벌이 엉뚱한 때 깬다.
        /// </summary>
        private AmbushRefusal EvaluateAmbushGate(InsectEntity e)
        {
            if (e == null || e.Data == null) return AmbushRefusal.NotAwake;
            RefreshAmbushBase();
            if (!ambushStateKnown) return AmbushRefusal.NotAwake;

            AmbushRules.Context c = ambushBase;
            c.Habit = InsectGame.Data.InsectHabits.For(e.Data);
            c.IsGuardian = e.IsGuardian;
            c.IsEngaged = e.IsEngaged;
            c.EntityCooldownLeft = e.AmbushCooldownLeft;
            c.CanFight = AmbushRules.CanFight(CaptureChoiceUI.IsRaidRarity(e.Data.rarity), ambushTeamFilled, ambushTeamReady);
            return AmbushRules.Check(c);
        }

        /// <summary>판정의 전역 칸을 프레임마다 한 번 채운다 — 다가오는 곤충이 여럿이어도 같은 값을 읽는다.</summary>
        private void RefreshAmbushBase()
        {
            if (ambushFrame == Time.frameCount) return;
            ambushFrame = Time.frameCount;
            EnsureAmbushRefs();
            TrackEncounter();

            ambushStateKnown = worldStateProvider != null;
            float now = Time.time;
            ambushBase = new AmbushRules.Context
            {
                // 플레이어가 있는 리전에서 보이는 날씨(설산의 눈·사막의 센바람) — 스폰과 같은 기준(rules/world-environment.md).
                State = ambushStateKnown ? worldStateProvider.GetWorldState(CurrentRegionId()) : default(WorldState),
                SinceLastAmbushEnded = now - lastAmbushEndTime,
                SinceLastEncounterEnded = now - lastEncounterEndTime,
                PlayerInEncounter = IsInEncounter(),
                DreamActive = DreamPrologueState.Active,
                InSubArea = regionManager != null && regionManager.CurrentSubArea != null,
                PlayerFrozen = IsPlayerFrozen(),
                ModalOpen = ModalUIRegistry.IsAnyOpen()
            };

            ambushTeamFilled = 0;
            ambushTeamReady = 0;
            if (choiceUi != null) choiceUi.GetFightReadiness(out ambushTeamFilled, out ambushTeamReady);
        }

        private string CurrentRegionId()
        {
            if (regionManager == null) return null;
            InsectGame.Data.RegionData region = regionManager.CurrentRegion;
            return region != null ? region.regionId : null;
        }

        /// <summary>배선이 빠졌을 때 한 번만 찾는다(AutoWire가 우선 — FindFirstObjectByType는 폴백).</summary>
        private void EnsureAmbushRefs()
        {
            if (ambushRefsSearched) return;
            ambushRefsSearched = true;
            if (worldStateProvider == null) worldStateProvider = FindFirstObjectByType<WorldStateProvider>();
            if (regionManager == null) regionManager = FindFirstObjectByType<RegionManager>();
        }

        /// <summary>플레이어가 교전(선택 창·미니게임·전투·레이드) 중인가.</summary>
        private bool IsInEncounter()
        {
            return (choiceUi != null && choiceUi.IsChoiceOpen)
                || (minigame != null && minigame.IsActive)
                || (battleScreen != null && battleScreen.IsBattleActive)
                || (raidScreen != null && raidScreen.IsRaidActive);
        }

        /// <summary>
        /// 교전이 끝난 순간을 적는다(프레임마다 한 번) — 전역 쿨다운의 기준. 습격 창 → 전투로 이어지는 동안은 한 교전이다
        /// (창을 닫고 전투를 여는 것이 한 호출 안에서 일어나 그 사이에 끊기지 않는다).
        /// </summary>
        private void TrackEncounter()
        {
            if (encounterTrackFrame == Time.frameCount) return;
            encounterTrackFrame = Time.frameCount;
            bool inEncounter = IsInEncounter();
            if (inEncounterLastFrame && !inEncounter)
            {
                lastEncounterEndTime = Time.time;
                if (encounterIsAmbush) lastAmbushEndTime = Time.time;
                encounterIsAmbush = false;
            }
            inEncounterLastFrame = inEncounter;
        }

        /// <summary>습격형이 멈칫을 끝내고 다가오기 시작했다 — 잡기 버튼 위에 경고를 띄운다(보고 달아날 틈).</summary>
        private void OnAmbushStarted(InsectEntity e)
        {
            if (e == null || e.Data == null) return;
            ShowWarning($"{e.DisplayNameForPlayer} Lv.{e.Level} — 덤벼든다! 달아나면 떼어 낼 수 있다");
        }

        /// <summary>
        /// 습격형이 닿았다 — 판정을 한 번 더 묻고(다가오는 사이 다른 창이 열렸거나 쿨다운이 시작됐을 수 있다) 「습격!」 창을 연다.
        /// 열지 않으면 곤충이 스스로 물러난다(<c>InsectEntity.ReachPlayer</c>).
        /// </summary>
        private void OnAmbushReached(InsectEntity e)
        {
            if (e == null || e.Data == null || choiceUi == null) return;
            if (EvaluateAmbushGate(e) != AmbushRefusal.None) return;

            string reason = AmbushRules.ReasonLine(InsectGame.Data.InsectHabits.For(e.Data), ambushBase.State);
            if (!choiceUi.ShowAmbush(e, reason)) return;

            // 이 창에서 시작하는 교전(창 → 전투·레이드)이 끝나면 습격 쿨다운이 걸린다.
            encounterIsAmbush = true;
            inEncounterLastFrame = true;
            feedbackTimer = 0f;   // "덤벼든다!" 경고는 창이 대신한다
            PlayerMovement pm = GetPlayerMovement();
            if (pm != null) pm.FaceTowards(e.transform.position);
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

            // 습격 경고 — 경고색 토큰(새 색을 만들지 않는다). 두 줄까지 들어가는 상자에 LabelFit으로 그린다.
            warnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            warnStyle.normal.textColor = UITheme.Instance.accentCoral;
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

        /// <summary>
        /// 습격 판정의 시간·날씨(플레이어가 있는 리전 기준)와 서브에리어 여부. 빠지면 처음 판정 때 한 번 찾아보고, 그래도 없으면
        /// 습격이 일어나지 않는다(곤충이 예전처럼 달아나기만 한다).
        /// </summary>
        public void AutoWire(WorldStateProvider worldState, RegionManager regions)
        {
            if (worldStateProvider == null) worldStateProvider = worldState;
            if (regionManager == null) regionManager = regions;
        }
    }

    /// <summary>
    /// 우하단 잡기 버튼과 그 위 글자(놓침·습격 경고), 탭 고리의 자리 — <b>순수 계산</b>. 그리기(<see cref="CaptureInputController"/>)와
    /// 전수 겹침 검사(<c>HudOverlapSweepTests</c>)가 같은 함수를 부른다. 상호작용 버튼(<c>WorldInteractionController</c>)·
    /// 가운데 무대(<c>HudStage</c>)·동굴 입구 버튼·꿈 안내 카드가 이 값을 기준으로 비켜 선다.
    /// </summary>
    public static class CatchButtonLayout
    {
        public const float Radius = 96f;
        /// <summary>오른쪽 안전 가장자리와 버튼 사이.</summary>
        public const float RightGap = 40f;
        /// <summary>
        /// 아래 '설정' 버튼(AccountSettingsUI)을 피하는 여백 — <b>픽셀</b> 92를 가상으로 환산한다(스케일이 작을수록 가상 여백이 크다).
        /// 예전 계정 버튼이 픽셀 좌표로 하단에 고정돼 있던 때의 보정이다.
        /// </summary>
        public const float AccountClearPixels = 92f;
        /// <summary>잡기 버튼과 그 왼쪽 상호작용 버튼 사이(<c>WorldInteractionController</c>가 같은 값을 쓴다).</summary>
        public const float NeighborGap = 36f;
        public const float LabelWidth = 520f;
        public const float LabelGap = 16f;
        public const float WarnHeight = 96f;
        public const float MissHeight = 72f;
        public const float NoteHeight = 52f;
        /// <summary>탭 고리가 이웃이 없을 때 퍼질 수 있는 배율.</summary>
        public const float SwingMaxScale = 1.85f;

        public static Vector2 Center(HudFrame f)
        {
            return new Vector2(f.Width - f.SafeRight - Radius - RightGap,
                f.ContentBottom - Radius - AccountClearPixels / f.Scale);
        }

        public static Rect ButtonRect(HudFrame f)
        {
            Vector2 c = Center(f);
            return new Rect(c.x - Radius, c.y - Radius, Radius * 2f, Radius * 2f);
        }

        // 글자 칸의 왼쪽 끝 — 버튼 가운데에 맞추되 화면 오른쪽 안전 가장자리(24) 밖으로 밀리지 않게 가둔다.
        // (놓침 글자는 예전에 가두지 않아 오른쪽 180px가 화면 밖이었다.)
        private static float LabelX(HudFrame f)
        {
            return Mathf.Min(Center(f).x - LabelWidth * 0.5f, f.Width - f.SafeRight - 24f - LabelWidth);
        }

        /// <summary>습격 경고 — 글자만(패널 없음). 곤충 이름이 길이를 정하므로 그리는 쪽이 LabelFit으로 맞춘다.</summary>
        public static Rect WarnRect(HudFrame f)
        {
            return new Rect(LabelX(f), Center(f).y - Radius - WarnHeight - LabelGap, LabelWidth, WarnHeight);
        }

        /// <summary>잡기 결과 글자 — 놓쳤으면 크게(72), 그 밖의 안내는 52.</summary>
        public static Rect FeedbackRect(HudFrame f, bool miss)
        {
            float h = miss ? MissHeight : NoteHeight;
            return new Rect(LabelX(f), Center(f).y - Radius - h - LabelGap, LabelWidth, h);
        }

        /// <summary>
        /// 탭 고리가 퍼지는 최대 반지름 — 이웃에 닿지 않게 가둔다. 왼쪽 상호작용 버튼까지 <c>Radius + NeighborGap</c>,
        /// 위 글자 칸까지 <c>Radius + LabelGap</c>, 아래 설정 버튼까지 <c>Radius + 92/Scale − 46</c>. 버튼보다 작아지면 버튼 크기다.
        /// 예전엔 늘 1.85배(178)까지 퍼져 상호작용 버튼·설정 버튼·글자를 덮었다.
        /// </summary>
        public static float SwingRadius(HudFrame f)
        {
            float toInteract = Radius + NeighborGap - 2f;
            float toLabel = Radius + LabelGap - 2f;
            float toSettings = Radius + AccountClearPixels / f.Scale - AccountSettingsUI.OpenButtonHeight - 2f;
            float r = Mathf.Min(Radius * SwingMaxScale, Mathf.Min(toInteract, Mathf.Min(toLabel, toSettings)));
            return Mathf.Max(Radius, r);
        }

        /// <summary>탭 고리가 가장 크게 퍼졌을 때의 자리.</summary>
        public static Rect SwingRect(HudFrame f)
        {
            Vector2 c = Center(f);
            float r = SwingRadius(f);
            return new Rect(c.x - r, c.y - r, r * 2f, r * 2f);
        }
    }
}
