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
        // internal — StoryObjectiveTracker가 자동 주행 도착 반경을 여기서 파생시킨다.
        // 사본을 두면 이 값이 바뀔 때 "도착했는데 말이 안 걸린다"가 조용히 생긴다.
        internal const float VillagerTalkRadius = 3f;

        /// <summary>튜토리얼의 '박사' 역할을 맡은 스토리 NPC. `Story.json`의 `ch1_intro` 화자와 같다.</summary>
        internal const string ElderStoryNpcId = "village_elder";
        // 아이는 돌아다니므로 주민보다 조금 넉넉하게 — 쫓아가서 말 걸기가 덜 답답하다.
        private const float KidDuelRadius = 3.5f;
        private const float ResultToastSeconds = 3.5f;

        private CashShopUI shop;
        private TrainingUI training;
        private PlayerMovement playerMovement;
        private NpcDialogueUI dialogue;
        private InsectSpawner spawner;
        private NpcManager npcManager;
        private InsectGame.Story.StoryDirector storyDirector;   // 스토리 NPC 대화 → NpcTalk 트리거
        private CameraFollower cameraFollower;                   // 첫 조우 시네마틱 줌
        private HospitalUI hospital;                             // 병원 치료 UI
        private NpcDuelController duelController;                // 곤충잡이 아이 대결

        private readonly List<InteractionPointDef> points = new List<InteractionPointDef>();

        private float scanTimer;
        private InteractionPointDef currentPoint;   // 현재 대상 건물 (주민이 더 가까우면 null)
        private VillagerNpc currentVillager;        // 현재 대상 주민 (건물이 더 가까우면 null)
        private CatcherKidNpc currentKid;           // 현재 대결 대상 아이 (더 가까운 대상이 있으면 null)
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
        /// <summary>
        /// 화면 중앙 접근 배너의 히트 영역. 우하단 원형 버튼은 엄지가 닿는 자리라 편하지만
        /// <b>시선은 화면 가운데에 있다</b> — 건물 앞에 서고도 우하단을 못 찾는다는 보고가 있어
        /// 같은 동작을 중앙에서도 바로 누를 수 있게 뒀다. 둘은 대체가 아니라 병행이다.
        /// </summary>
        private Rect centerButtonRect;
        private GUIStyle centerLabelStyle;
        private GUIStyle centerHintStyle;

        /// <summary>현재 대상(건물/주민)이 존재하고 최근접 곤충보다 가까우면 true.</summary>
        public bool HasPriorityTarget => hasPriorityTarget;

        public void AutoWire(CashShopUI cashShop, TrainingUI trainingUi, PlayerMovement player)
        {
            if (shop == null) shop = cashShop;
            if (training == null) training = trainingUi;
            if (playerMovement == null) playerMovement = player;
        }

        public void AutoWire(HospitalUI hospitalUi)
        {
            if (hospital == null) hospital = hospitalUi;
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

        public void AutoWire(NpcDuelController duel)
        {
            if (duelController == null) duelController = duel;
        }

        public void AutoWire(InsectGame.Story.StoryDirector director)
        {
            if (storyDirector == null && director != null)
            {
                storyDirector = director;
                // 스토리 모달을 벨 종료 전에 닫으면 조우 줌을 부드럽게 조기 종료(카메라 잔류 제거).
                storyDirector.StoryBeatCompleted += OnStoryBeatCompleted;
            }
        }

        public void AutoWire(CameraFollower follower)
        {
            if (cameraFollower == null) cameraFollower = follower;
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

        private void Start()
        {
            // AutoWire되지 않았으면 카메라 팔로워를 직접 탐색(첫 조우 줌 폴백 — AutoWire 우선).
            if (cameraFollower == null) cameraFollower = FindFirstObjectByType<CameraFollower>();
        }

        private void OnDestroy()
        {
            if (storyDirector != null)
                storyDirector.StoryBeatCompleted -= OnStoryBeatCompleted;

            // **런타임에 만든 Texture2D는 씬이 내려가도 안 사라진다.** 애셋이 아니라 이 필드만
            // 참조하는 언매니지드 객체라, 파기하지 않으면 씬을 다시 로드할 때마다 128KB씩 쌓인다
            // (`AccountSettingsUI`의 로그아웃·계정삭제가 씬을 통째로 재로드한다).
            // `WorldLobbyUI`가 같은 계열로 audit에 걸린 적이 있다 — 그쪽은 매 프레임이라 더 빨랐을 뿐
            // 원인은 같다. (unity-csharp.md의 "Destroy 직접 호출 금지"는 풀링 대상 이야기다.)
            if (circleFillTex != null) Destroy(circleFillTex);
            if (circleRingTex != null) Destroy(circleRingTex);
            circleFillTex = null;
            circleRingTex = null;
        }

        // 스토리 비트 모달이 닫히면 조우 카메라 줌을 부드럽게 조기 종료(진행 중 포커스 없으면 no-op).
        private void OnStoryBeatCompleted(InsectGame.Story.StoryBeat beat)
        {
            if (cameraFollower != null) cameraFollower.ReleaseFocus();
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

            // 화면 중앙 배너도 같은 대접을 받는다 — 우하단 원형 버튼과 둘 다 살아 있다.
            if (Input.touchSupported && centerButtonRect.width > 0f
                && FieldHudInput.TryGetTapInVirtualRect(centerButtonRect))
                Activate();
        }

        // ── 0.15s 간격 스캔: 최근접 건물 + 대화 가능 최근접 주민 + 곤충 거리 비교 ──
        private void Scan()
        {
            currentPoint = null;
            currentVillager = null;
            currentKid = null;
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

            // 대화 가능 최근접 주민/스토리 NPC (반경 3m) — 두 목록 모두 스캔.
            VillagerNpc bestVillager = null;
            float bestVillagerDist = float.MaxValue;
            if (npcManager != null)
            {
                ScanTalkable(npcManager.Villagers, playerPos, ref bestVillager, ref bestVillagerDist);
                ScanTalkable(npcManager.StoryNpcs, playerPos, ref bestVillager, ref bestVillagerDist);
            }

            // 대결 가능한 최근접 아이 (반경 3.5m). 곤충을 쫓는 중이거나 쿨다운이면 후보에서 빠진다.
            CatcherKidNpc bestKid = null;
            float bestKidDist = float.MaxValue;
            if (npcManager != null && duelController != null)
            {
                IReadOnlyList<CatcherKidNpc> kids = npcManager.Kids;
                float now = Time.time;
                for (int i = 0; i < kids.Count; i++)
                {
                    CatcherKidNpc kid = kids[i];
                    if (kid == null || !kid.gameObject.activeInHierarchy) continue;
                    float d = Vector3.Distance(playerPos, kid.transform.position);
                    if (d > KidDuelRadius || d >= bestKidDist) continue;
                    if (!duelController.CanDuel(kid, now)) continue;
                    bestKidDist = d;
                    bestKid = kid;
                }
            }

            // 더 가까운 쪽을 단일 대상으로 (건물 / 주민 / 아이)
            if (bestPoint == null && bestVillager == null && bestKid == null) return;
            if (bestKid != null && bestKidDist <= bestVillagerDist && bestKidDist <= bestPointDist)
            {
                currentKid = bestKid;
                currentTargetDistance = bestKidDist;
                promptText = $"[E] 대결: {bestKid.DisplayName}";
                buttonText = "대결";
            }
            else if (bestVillager != null && (bestPoint == null || bestVillagerDist <= bestPointDist))
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

        // 대화 가능 최근접 NPC 갱신 — 일반 주민/스토리 NPC 공용.
        private static void ScanTalkable(IReadOnlyList<VillagerNpc> list, Vector3 playerPos,
            ref VillagerNpc best, ref float bestDist)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                VillagerNpc v = list[i];
                if (v == null || !v.CanTalk || !v.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(playerPos, v.transform.position);
                if (d <= VillagerTalkRadius && d < bestDist)
                {
                    bestDist = d;
                    best = v;
                }
            }
        }

        private void Activate()
        {
            // 스캔 간격(0.15s) 사이에 모달이 열렸을 수 있음 — 발동 직전 재확인
            if (ModalUIRegistry.IsAnyOpen()) return;
            if (playerMovement != null && playerMovement.IsFrozen) return;

            if (currentKid != null)
            {
                if (duelController != null) duelController.TryStartDuel(currentKid, Time.time);
            }
            else if (currentVillager != null)
            {
                // 조우 연출 — 스토리 NPC가 플레이어를 향해 돌아본다(시선 맞춤).
                if (currentVillager.IsStoryNpc && playerMovement != null)
                    currentVillager.FacePlayer(playerMovement.transform);

                // 스토리 NPC면 먼저 NpcTalk 스토리 발동 시도. 비트가 뜨면 NpcDialogueUI가 렌더,
                // 아니면(이미 봤거나 일반 주민) 앰비언트 대사로 폴백.
                // q_talk_elder 진행 — 첫 파트너 곤충을 받는 자리라 튜토리얼의 시작점이다.
                // **`OnNpcTalked`보다 먼저 부른다**: 그쪽이 비트를 발화시키면 대화 모달이 열리고,
                // 그 뒤에 통지하면 프레임 순서에 따라 퀘스트 진행이 한 박자 늦는다.
                // 이미 완료된 퀘스트면 `NotifyAction`이 활성 퀘스트 타입 불일치로 조용히 무시한다.
                if (currentVillager.IsStoryNpc
                    && currentVillager.StoryNpcId == ElderStoryNpcId)
                {
                    // **`?.`를 쓰지 않는다.** null 조건 연산자는 **진짜 참조**만 보므로,
                    // 파기된 MonoBehaviour가 static에 남아 있으면(싱글턴이 OnDestroy에서 안 비우면)
                    // 그대로 호출로 들어가 MissingReferenceException이 난다 —
                    // `UnityEngine.Object`의 오버로드된 `==`(파괴 검사)를 우회하기 때문이다.
                    // (`StoryStageDirector.TryPlayPrelude`가 같은 함정을 다룬다.)
                    TutorialQuestManager mgr = TutorialQuestManager.Instance;   // using InsectGame.Core
                    if (mgr != null) mgr.NotifyTalkToElder();
                }

                bool storyFired = currentVillager.IsStoryNpc && storyDirector != null
                    && storyDirector.OnNpcTalked(currentVillager.StoryNpcId);
                if (storyFired && cameraFollower != null)
                    cameraFollower.FocusOn(currentVillager.transform.position, 2.5f);   // 첫 조우 줌인

                // 명부회 간부는 대사가 끝난 다음 말을 걸면 대결로 이어진다.
                // 순서가 중요하다 — 첫 대화에서 곧바로 싸움을 걸면 그 인물이 누구인지 모르는 채로
                // 전투에 들어간다.
                //
                // **`!storyFired`만으로는 부족하다.** 그건 "이미 소개를 봤다"와 "아직 차례가
                // 아니다"를 구분하지 못한다 — 집게·저울·관장의 소개는 서브에리어 대치 비트에
                // 걸려 있어서, 리전에 도착해 본진의 그들에게 말을 걸면 소개 없이 보스전이
                // 시작됐다(최종 보스인 관장까지). `HasMetStoryNpc`가 그 둘을 가른다.
                //
                // **early return 금지** — 아래 재스캔 정리를 건너뛰면 전투 중에도 상호작용
                // 프롬프트가 화면에 남는다.
                bool metBefore = storyDirector != null
                    && storyDirector.HasMetStoryNpc(currentVillager.StoryNpcId);
                bool bossDuelStarted = !storyFired && metBefore && currentVillager.IsStoryNpc
                    && duelController != null
                    && duelController.TryStartBossDuel(currentVillager.StoryNpcId, Time.time);

                if (!storyFired && !bossDuelStarted && dialogue != null) dialogue.Show(currentVillager);
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
                    case InteractionKind.Hospital:
                        if (hospital != null) hospital.Toggle();
                        break;
                }
            }

            // 발동 후 즉시 재스캔 (모달이 열렸으면 대상 해제 → 프롬프트/버튼 숨김)
            currentPoint = null;
            currentVillager = null;
            currentKid = null;
            hasPriorityTarget = false;
            scanTimer = 0f;
        }

        private void OnGUI()
        {
            // 대결 결과 토스트는 대상이 없어도(대결 직후엔 아이가 쿨다운이라 대상에서 빠진다) 떠야 한다.
            bool showResult = duelController != null
                && !string.IsNullOrEmpty(duelController.LastResultText)
                && Time.time - duelController.LastResultTime < ResultToastSeconds;

            if (!hasPriorityTarget && !showResult) return;
            if (ModalUIRegistry.IsAnyOpen()) return;

            EnsureStyles();
            EnsureCircleTex();

            UIScale.Begin();

            if (showResult)
            {
                GUI.Label(
                    new Rect(UIScale.VirtualScreenWidth / 2f - 400f, UISafeLayout.ContentBottom - 150f, 800f, 44f),
                    duelController.LastResultText,
                    promptStyle);
            }

            if (!hasPriorityTarget)
            {
                UIScale.End();
                return;
            }

            float vw = UIScale.VirtualScreenWidth;
            float safeR = UIScale.VirtualSafeRight;

            // 프롬프트 — 화면 하단 중앙에서 좌측 오프셋 (잡기 버튼/미스 피드백과 겹침 회피).
            // **길이를 데이터가 정한다**(NPC 표시명·건물 라벨)는데 상자는 640 고정이고 wordWrap도
            // 없어서, 이름이 길면 **가로로 잘린다**(rules/ui-layout.md). `text_fit_lint`는 라벨 인자가
            // 캐시된 문자열(`promptText`)이라 데이터 출처를 못 봐서 이 자리를 놓친다.
            UIHelper.LabelFit(new Rect(vw / 2f - 560f, UISafeLayout.ContentBottom - 96f, 640f, 44f),
                promptText, promptStyle);

            DrawCenterButton(vw);

            // 원형 상호작용 버튼 — 잡기 버튼(우하단, 반경 96) 왼쪽에 배치
            float radius = 80f;
            float accountClear = 92f / UIScale.Scale; // CaptureInputController와 동일한 '계정' 버튼 회피 보정
            float catchCx = vw - safeR - 96f - 40f;   // 잡기 버튼 중심 X (DrawCatchButton과 동기)
            float cx = catchCx - 96f - radius - 36f;  // 잡기 버튼 왼쪽
            float cy = UISafeLayout.ContentBottom - 96f - accountClear; // 잡기 버튼과 같은 높이(중심 정렬)
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

        /// <summary>
        /// 화면 중앙 접근 배너. 플레이어 머리 위쯤(세로 58%)에 두어 캐릭터를 가리지 않으면서
        /// 시선이 머무는 자리에 놓는다. 고정 높이라 세로 마진 안으로 clamp한다
        /// (rules/ui-layout.md: 비율 배치는 허용, 다만 고정 높이는 가둔다).
        /// </summary>
        private void DrawCenterButton(float vw)
        {
            float w = Mathf.Min(560f, vw - 80f);
            float h = UIScale.IsMobileLayout ? 132f : 108f;
            float y = Mathf.Clamp(
                UIScale.VirtualScreenHeight * 0.58f,
                UISafeLayout.ContentTop,
                UISafeLayout.ContentBottom - h);
            Rect rect = new Rect((vw - w) * 0.5f, y, w, h);

            centerButtonRect = rect;
            FieldHudInput.RegisterBlockingRect(rect);   // 배너 위 탭이 클릭-이동으로 새지 않게

            bool hovered = !Input.touchSupported && rect.Contains(UIScale.VirtualMousePosition);
            float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * 4f);
            Color accent = new Color(0.45f, 0.85f, 1f);

            UISurface.Card(
                rect,
                hovered ? new Color(0.13f, 0.24f, 0.34f, 0.97f) : new Color(0.07f, 0.13f, 0.20f, 0.94f),
                new Color(accent.r, accent.g, accent.b, pulse));
            GUI.color = Color.white;

            GUI.Label(new Rect(rect.x, rect.y + 14f, rect.width, 46f), buttonText, centerLabelStyle);
            centerHintStyle.normal.textColor = new Color(0.72f, 0.88f, 1f, pulse);
            GUI.Label(
                new Rect(rect.x, rect.y + h - 46f, rect.width, 34f),
                Input.touchSupported ? "탭하여 들어가기" : "[E] 또는 클릭",
                centerHintStyle);

            // 데스크탑만 GUI.Button — 터치는 Update의 raw 히트테스트가 처리한다(이중 발화 회피,
            // 우하단 원형 버튼과 같은 규칙).
            if (!Input.touchSupported && GUI.Button(rect, GUIContent.none, GUIStyle.none))
                Activate();
        }

        private static string ShortButtonLabel(InteractionKind kind)
        {
            switch (kind)
            {
                case InteractionKind.ItemShop: return "상점";
                case InteractionKind.Gacha: return "상자";
                case InteractionKind.Training: return "훈련";
                case InteractionKind.Hospital: return "병원";
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

            // 중앙 배너 — 우하단 원형 버튼보다 크게 잡는다. 시선이 머무는 자리라 여기가 주 진입점이다.
            centerLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            centerLabelStyle.normal.textColor = Color.white;

            centerHintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter
            };
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
