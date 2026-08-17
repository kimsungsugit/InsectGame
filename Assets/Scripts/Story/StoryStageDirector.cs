using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.NPC;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>
    /// 스토리 NPC를 <b>배우로 부리는</b> 지휘자. 두 가지를 한 컴포넌트에서 한다 —
    /// 둘 다 같은 <see cref="VillagerNpc"/>의 Scripted 상태를 쓰므로 나누면 서로 명령을 덮어쓴다.
    ///
    /// <b>① 조우 접근(저작 0).</b> 지금 목표가 "○○에게 말 걸기"인데 플레이어가 12m 안에 들어오면
    /// 그 NPC가 스스로 걸어와 대화 거리에서 멈추고 손을 흔든다. 규칙 기반이라 전 챕터에 적용된다.
    ///
    /// <b>② 저작 연출.</b> 비트의 <c>stageEnterId</c>(대사 앞)·<c>stageExitId</c>(대사 뒤)를
    /// <see cref="StoryStageLibrary"/>에서 찾아 스텝대로 돌린다.
    ///
    /// <b>복귀 보장이 급소다</b> — <see cref="CutsceneDirector"/>와 같은 이유다. 재생 중엔 조작을
    /// 막으므로 어떤 경로로 끝나든(정상 종료·ESC·비활성·타임아웃) 반드시 되돌려야 하고,
    /// 입장 연출은 그 위에 <b>대사를 여는 책임</b>까지 진다. <c>onDone</c>을 못 부르면
    /// 그 비트가 <c>pendingBeatId</c>에 갇혀 캠페인이 영구 정지한다.
    /// </summary>
    public class StoryStageDirector : MonoBehaviour, IModalUI, IStoryStagePrelude
    {
        private enum Mode { Idle, Prelude, Postlude }

        /// <summary>조우 접근 판단 주기(초) — 매 프레임 볼 이유가 없다.</summary>
        private const float ApproachInterval = 0.25f;
        /// <summary>앵커에 "돌아왔다"고 볼 거리(m).</summary>
        private const float HomeDistance = 1f;

        private StoryDirector storyDirector;
        private StoryObjectiveTracker objectiveTracker;
        private NpcManager npcManager;
        private PlayerMovement playerMovement;
        private Transform playerTransform;

        // ── 저작 연출 재생 상태 ──
        private Mode mode = Mode.Idle;
        private StoryStageStep[] steps;
        private int stepIndex = -1;
        private float stepTimer;
        private bool waitingForMove;
        private bool stepMoveDone;
        private float sequenceDeadline;
        private System.Action onPreludeDone;
        private bool restoreFrozen;
        // 이동 콜백이 자기 시퀀스의 것인지 가르는 표. Stop/Play마다 올린다 —
        // 없으면 건너뛴 시퀀스의 늦은 콜백이 새 시퀀스를 한 스텝 밀어버린다.
        private int sequenceToken;
        // 이번 시퀀스가 건드린 배우들 — 종료 시 걷다 만 상태를 정리한다.
        private readonly List<VillagerNpc> sequenceActors = new List<VillagerNpc>();

        // ── 조우 접근 상태 ──
        private float approachTimer;
        private VillagerNpc approachNpc;
        private bool approachGreeted;
        private bool approachReturning;
        // 이번 조우에서 이미 다가오기 시작했는가 — 히스테리시스를 '시작'에만 걸기 위한 표.
        private bool approachEngaged;

        // ── IModalUI ── 재생 중 ESC로 건너뛴다. 탈출구가 없으면 버그로 안 끝났을 때 갇힌다.
        public bool IsOpen => mode != Mode.Idle;
        public void CloseModal() => Stop();

        public bool IsPlaying => mode != Mode.Idle;

        public void AutoWire(StoryDirector director, StoryObjectiveTracker tracker,
            NpcManager npcs, PlayerMovement movement, Transform player)
        {
            if (storyDirector == null) storyDirector = director;
            if (objectiveTracker == null) objectiveTracker = tracker;
            if (npcManager == null) npcManager = npcs;
            if (playerMovement == null) playerMovement = movement;
            if (playerTransform == null) playerTransform = player;
            Subscribe();
        }

        // AutoWire와 OnEnable이 함께 부른다 — `-=` 뒤 `+=`라 중복 구독이 되지 않는다.
        // (오프닝 다시보기가 루트를 껐다 켜는 경로 — rules/ui-layout.md의 구독 회귀 계열)
        private void Subscribe()
        {
            if (storyDirector == null) return;
            storyDirector.StoryBeatCompleted -= OnBeatCompleted;
            storyDirector.StoryBeatCompleted += OnBeatCompleted;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (storyDirector != null) storyDirector.StoryBeatCompleted -= OnBeatCompleted;
            // 재생 중 비활성화되면 조작이 묶인 채 남는다 — 반드시 되돌린다.
            Stop();
        }

        // ==================== 저작 연출 ====================

        /// <summary>
        /// 대사 앞 연출. <see cref="NpcDialogueUI"/>가 비트를 렌더하기 전에 부른다.
        /// true를 돌려주면 <paramref name="onDone"/>이 불릴 때까지 대사가 미뤄진다.
        /// </summary>
        public bool TryPlayPrelude(StoryBeat beat, System.Action onDone)
        {
            if (beat == null || onDone == null) return false;

            // **죽었거나 꺼져 있으면 게이트를 걸지 않는다.** 호출부의 `stagePrelude != null`은
            // 필드가 인터페이스 타입이라 **C# 참조 비교로 컴파일된다** — `UnityEngine.Object`의
            // 오버로드된 `==`(파괴 검사)가 적용되지 않는다. 게다가 이 컴포넌트는 `World/` 아래고
            // 대화창은 `UI/` 아래라 서로 다른 루트다. 파괴·비활성 상태에서 true를 돌려주면
            // 모달 등록과 `SetFrozen(true)`만 남고 `Update`가 안 돌아 하드 타임아웃이 영영
            // 발화하지 않는다 → `onDone` 미호출 → 그 비트가 영구 정지한다.
            if (!isActiveAndEnabled) return false;

            if (string.IsNullOrEmpty(beat.stageEnterId)) return false;
            // 대사가 없는 비트는 모달 자체가 안 뜨므로(ShowStory가 즉시 완료) 게이트를 걸 이유가 없다.
            if (beat.lines == null || beat.lines.Count == 0) return false;
            return PlaySequence(beat.stageEnterId, Mode.Prelude, onDone);
        }

        private void OnBeatCompleted(StoryBeat beat)
        {
            if (beat == null || string.IsNullOrEmpty(beat.stageExitId)) return;
            // cutsceneId가 함께 있으면 CutsceneDirector와 조작·카메라를 다툰다. story_lint가
            // 그 조합을 금지하지만, 런타임에서도 컷신 쪽에 양보한다(카메라를 뺏는 쪽이 더 크다).
            if (!string.IsNullOrEmpty(beat.cutsceneId))
            {
                Debug.LogWarning($"[Stage] {beat.beatId}: cutsceneId와 stageExitId가 함께 있어 연출을 건너뛴다");
                return;
            }
            PlaySequence(beat.stageExitId, Mode.Postlude, null);
        }

        private bool PlaySequence(string stageId, Mode nextMode, System.Action onDone)
        {
            if (playerTransform == null || npcManager == null) return false;

            if (!StoryStageLibrary.TryGet(stageId, out StoryStageStep[] loaded))
            {
                // 오타를 조용히 넘기지 않는다 — 연출이 안 나오는 건 화면상 티가 안 난다.
                Debug.LogWarning($"[Stage] 알 수 없는 stageId: '{stageId}'");
                return false;
            }
            if (loaded == null || loaded.Length == 0) return false;

            // **재진입 가드** — 재생 중에 또 불리면 restoreFrozen이 자기가 만든 true를 읽어
            // 종료할 때 조작을 안 풀어준다(CutsceneDirector.Play와 같은 함정).
            if (mode != Mode.Idle) Stop();

            // 조우 접근과 겹치지 않게 추적을 끊는다. **집으로 돌려보내지는 않는다** —
            // 여기서 복귀를 걸면 방금 다가온 어르신이 대사 직전에 되돌아가 버린다.
            // 다만 걸어오던 중이면 멈춘다. 시퀀스가 배우를 온전히 지배해야 예측 가능하다.
            if (approachNpc != null && approachNpc.IsScripted) approachNpc.StopScripted();
            approachNpc = null;
            approachGreeted = false;
            approachReturning = false;
            approachEngaged = false;

            sequenceToken++;
            steps = loaded;
            stepIndex = -1;
            mode = nextMode;
            onPreludeDone = onDone;
            sequenceActors.Clear();

            restoreFrozen = playerMovement != null && playerMovement.IsFrozen;
            if (playerMovement != null)
            {
                playerMovement.CancelAutoRun();
                playerMovement.SetFrozen(true);
            }
            ModalUIRegistry.Register(this);

            sequenceDeadline = Time.unscaledTime + StoryStageTimeline.SequenceTimeoutSeconds(steps);
            AdvanceStep();
            return true;
        }

        /// <summary>
        /// 재생 종료 — <b>모든 종료 경로가 여기로 모인다.</b> 두 번 불려도 안전하다.
        /// </summary>
        public void Stop()
        {
            if (mode == Mode.Idle)
            {
                ModalUIRegistry.Unregister(this);
                return;
            }

            // 늦게 오는 이동 콜백을 무효화한다(아래 SnapToFinalPose가 StopScripted로 콜백을 터뜨린다).
            sequenceToken++;

            SnapToFinalPose();

            mode = Mode.Idle;
            steps = null;
            stepIndex = -1;
            stepTimer = 0f;
            waitingForMove = false;
            stepMoveDone = false;
            sequenceActors.Clear();

            if (playerMovement != null && !restoreFrozen) playerMovement.SetFrozen(false);
            ModalUIRegistry.Unregister(this);

            // **마지막에 부른다** — 콜백(대사 모달 열기)이 다시 프리즈를 걸 수 있으므로
            // 우리 복구가 먼저 끝나 있어야 한다. 그리고 어떤 경우에도 부른다.
            System.Action done = onPreludeDone;
            onPreludeDone = null;
            done?.Invoke();
        }

        /// <summary>
        /// 남은 스텝의 이동 목적지를 즉시 적용한다 — 건너뛰기·타임아웃으로 끊겨도 배우가
        /// 걷다 만 자리에 서 있지 않게. 앞에서부터 훑으므로 배우별로 <b>마지막</b> 목적지가 남는다.
        /// </summary>
        private void SnapToFinalPose()
        {
            for (int i = 0; i < sequenceActors.Count; i++)
                if (sequenceActors[i] != null) sequenceActors[i].StopScripted();

            if (steps == null) return;
            for (int i = 0; i < steps.Length; i++)
            {
                StoryStageStep step = steps[i];
                bool pending = i >= Mathf.Max(0, stepIndex);

                // **이미 지나간 스텝도 귀가만은 확인한다.** 이동 스텝은 NPC의 8초 타임아웃으로도
                // "완료"로 쳐서 시퀀스가 정상 종료되므로, 걷다 만 `ReturnToAnchor`는 예전엔
                // 아무도 보정하지 않았다(루프가 `stepIndex`부터라 정상 완주 시 한 번도 안 돌았다).
                // 그래서 라온이 초원 앵커까지 25m를 못 걷고 들판 한복판에 영구 정착했고, 그 개체가
                // `StoryObjectiveTracker`의 후보로 남아 HUD 쐐기와 자동 주행이 엉뚱한 곳을 가리켰다.
                // 역설적으로 ESC로 건너뛰면(이 루프가 도는 경로) 결과가 더 나았다.
                //
                // 지나간 스텝 중 **`ReturnToAnchor`만** 소급한다 — 앵커는 절대 좌표라 낡지 않지만
                // 플레이어 기준 오프셋은 그 사이 플레이어가 움직여 무의미해진다.
                if (!pending && step.action != StageAction.ReturnToAnchor) continue;
                if (pending
                    && step.action != StageAction.MoveToOffset
                    && step.action != StageAction.WarpToOffset
                    && step.action != StageAction.ReturnToAnchor) continue;

                VillagerNpc npc = FindActor(step.storyNpcId);
                if (npc == null) continue;
                Vector3 destination = step.action == StageAction.ReturnToAnchor
                    ? npc.AnchorPosition
                    : PlayerRelative(step.offset);

                // 소급 보정은 실제로 못 돌아간 배우에게만 — 이미 집에 있는데 워프시키면 낭비다.
                if (!pending
                    && HorizontalDistance(npc.transform.position, destination) <= HomeDistance) continue;

                npc.WarpTo(destination, playerTransform);
            }
        }

        private void AdvanceStep()
        {
            stepIndex++;
            if (steps == null || stepIndex >= steps.Length)
            {
                Stop();
                return;
            }

            waitingForMove = false;
            stepMoveDone = false;
            stepTimer = 0f;

            StoryStageStep step = steps[stepIndex];
            VillagerNpc npc = string.IsNullOrEmpty(step.storyNpcId) ? null : FindActor(step.storyNpcId);
            if (npc != null && !sequenceActors.Contains(npc)) sequenceActors.Add(npc);

            switch (step.action)
            {
                case StageAction.WarpToOffset:
                    if (npc != null) npc.WarpTo(PlayerRelative(step.offset), playerTransform);
                    break;

                case StageAction.MoveToOffset:
                    if (npc == null) break;   // 배우가 월드에 없으면 그 스텝은 건너뛴다
                    waitingForMove = true;
                    npc.BeginScriptedMove(PlayerRelative(step.offset), step.arriveRadius,
                        MakeArriveCallback());
                    break;

                case StageAction.ReturnToAnchor:
                    if (npc == null) break;
                    // 도착까지 기다린다 — 저작된 퇴장 연출이 실제로 보이도록.
                    //
                    // **알려진 UX 부채**: 마지막 스텝이 이것이면 대사를 닫은 뒤 배우가 앵커에 닿을
                    // 때까지(최악 8초, 시퀀스 상한 ~9초) 조작이 잠기는데 화면엔 안내가 없고 이
                    // 컴포넌트는 건너뛰기 버튼도 그리지 않는다(`CutsceneDirector`엔 있다).
                    // 여기서 기다리지 않게 바꾸면 `Stop()`의 `SnapToFinalPose`가 즉시 앵커로
                    // 워프시켜 **걷는 연출 자체가 사라진다** — 눈앞의 NPC가 순간이동하는 셈이라
                    // 더 나쁘다. 제대로 풀려면 "시퀀스 진행"과 "조작 잠금"을 분리하거나
                    // 건너뛰기 버튼을 붙여야 하고, 그건 연출 설계 판단이라 여기서 단독으로 하지 않는다.
                    waitingForMove = true;
                    npc.BeginScriptedReturn(MakeArriveCallback());
                    break;

                case StageAction.FacePlayer:
                    if (npc != null) npc.FacePlayer(playerTransform);
                    stepTimer = Mathf.Max(0f, step.duration);
                    break;

                case StageAction.Gesture:
                    if (npc != null) npc.PlayGesture(step.gesture);
                    stepTimer = Mathf.Max(NpcGesturePose.DurationOf(step.gesture), Mathf.Max(0f, step.duration));
                    break;

                case StageAction.Wait:
                    stepTimer = Mathf.Max(0f, step.duration);
                    break;
            }

            // 즉시 끝나는 스텝은 다음 Update가 넘긴다 — 여기서 재귀하면 깊이가 스텝 수만큼 된다.
        }

        // 토큰을 캡처한 도착 콜백. 시퀀스가 바뀐 뒤 도착한 늦은 콜백은 무시된다.
        private System.Action MakeArriveCallback()
        {
            int token = sequenceToken;
            return () =>
            {
                if (token != sequenceToken || mode == Mode.Idle) return;
                stepMoveDone = true;
            };
        }

        private void TickSequence()
        {
            // 하드 타임아웃 — 무슨 일이 있어도 여기서 끝난다. timeScale에 끌려다니면 안 되므로
            // unscaled를 쓴다(전투 슬로모션 직후에 발화할 수 있다).
            if (Time.unscaledTime >= sequenceDeadline)
            {
                Stop();
                return;
            }

            if (waitingForMove)
            {
                if (!stepMoveDone) return;
            }
            else if (stepTimer > 0f)
            {
                stepTimer -= Time.unscaledDeltaTime;
                if (stepTimer > 0f) return;
            }

            AdvanceStep();
        }

        // ==================== 조우 접근 ====================

        private void Update()
        {
            if (mode != Mode.Idle)
            {
                TickSequence();
                return;
            }

            approachTimer -= Time.deltaTime;
            if (approachTimer > 0f) return;
            approachTimer = ApproachInterval;
            TickApproach();
        }

        private void TickApproach()
        {
            VillagerNpc target = objectiveTracker != null ? objectiveTracker.TargetNpc : null;

            if (target != approachNpc)
            {
                ReleaseApproach();   // 이전 대상은 제자리로 돌려보낸다
                approachNpc = target;
                approachGreeted = false;
                approachReturning = false;
                approachEngaged = false;
            }

            if (approachNpc == null || playerTransform == null) return;
            // 대화·모달 중이거나 조작이 묶여 있으면 손대지 않는다.
            if (ModalUIRegistry.IsAnyOpen()) return;
            if (playerMovement != null && playerMovement.IsFrozen) return;
            if (approachNpc.IsTalking) return;

            float arriveRadius = StoryNpcApproach.ArriveRadiusFor(
                WorldInteractionController.VillagerTalkRadius);
            float distance = HorizontalDistance(approachNpc.transform.position, playerTransform.position);

            switch (StoryNpcApproach.Decide(distance, arriveRadius, approachNpc.IsScripted,
                approachGreeted, approachEngaged))
            {
                case ApproachAction.Walk:
                    approachReturning = false;
                    approachEngaged = true;
                    // 목표는 그 순간의 플레이어 위치다. 플레이어가 움직여 빗나가면 도착 후
                    // 다음 판단에서 다시 걸린다(재조준을 매 틱 하면 이동 타임아웃이 영영 안 온다).
                    //
                    // **플레이어의 자동 주행은 끊지 않는다.** 한 번 끊어 봤다가 더 나빠졌다 —
                    // 둘이 서로 옛 위치를 향해 가며 살짝 엇갈리는 건 몇 초 안에 수렴하지만,
                    // 플레이어를 세워 두면 NPC가 옛 지점에 도착해 멈춘 뒤 아무도 안 움직인다.
                    approachNpc.BeginScriptedMove(playerTransform.position, arriveRadius);
                    break;

                case ApproachAction.Greet:
                    if (approachNpc.IsScripted) approachNpc.StopScripted();
                    approachNpc.FacePlayer(playerTransform);
                    approachNpc.PlayGesture(NpcGesture.Wave);
                    approachGreeted = true;
                    approachReturning = false;
                    approachEngaged = false;   // 조우 종료 — 다음 조우는 12m부터 다시 시작
                    break;

                case ApproachAction.Return:
                    approachGreeted = false;
                    approachEngaged = false;
                    if (HorizontalDistance(approachNpc.transform.position, approachNpc.AnchorPosition)
                        <= HomeDistance)
                    {
                        approachReturning = false;
                    }
                    else if (!approachReturning || !approachNpc.IsScripted)
                    {
                        // 걷고 있는 동안에는 다시 걸지 않는다 — 매 틱 재발행하면 이동 타임아웃이
                        // 계속 미뤄져 영영 만료되지 않는다. 그래서 조건은 `IsScripted`다.
                        //
                        // **래치 하나로는 부족하다.** 예전엔 `!approachReturning` 뿐이라,
                        // 복귀 이동이 8초 타임아웃으로 중도 종료되면(예산 3.2m/s × 8s = 25.6m —
                        // 플레이어가 주행하며 NPC를 그보다 멀리 끌고 나가는 건 쉽다) 래치가 true로
                        // 남아 **다시는 명령이 안 나갔다**. 스토리 NPC는 `wanderRadius = 0`이라
                        // 배회 복귀도 없어서, 주석이 막겠다던 "어르신이 들판을 헤맨다"가 영구화됐다.
                        // 진입 데드존(12~16m)을 `engaged`로 고쳤던 것의 거울상이다.
                        approachReturning = true;
                        if (approachNpc.IsScripted) approachNpc.StopScripted();
                        approachNpc.BeginScriptedReturn();
                    }
                    break;
            }
        }

        /// <summary>목표가 바뀌었다 — 이전 NPC를 제자리로 돌려보낸다(대화 중이면 건드리지 않는다).</summary>
        private void ReleaseApproach()
        {
            if (approachNpc == null) return;
            if (!approachNpc.IsTalking)
            {
                if (approachNpc.IsScripted) approachNpc.StopScripted();
                if (HorizontalDistance(approachNpc.transform.position, approachNpc.AnchorPosition) > HomeDistance)
                    approachNpc.BeginScriptedReturn();
            }
            approachNpc = null;
        }

        // ==================== 공용 ====================

        // 같은 storyNpcId가 여러 리전에 서 있다 — 연출에 세울 개체는 플레이어에게 가장 가까운
        // 쪽을 고른다(어차피 Warp로 무대에 올리므로 어느 개체든 맞다).
        private VillagerNpc FindActor(string storyNpcId)
        {
            if (npcManager == null || string.IsNullOrEmpty(storyNpcId)) return null;

            VillagerNpc best = null;
            float bestDist = float.MaxValue;
            IReadOnlyList<VillagerNpc> list = npcManager.StoryNpcs;
            for (int i = 0; i < list.Count; i++)
            {
                VillagerNpc npc = list[i];
                if (npc == null || npc.StoryNpcId != storyNpcId) continue;
                float d = playerTransform != null
                    ? Vector3.SqrMagnitude(npc.transform.position - playerTransform.position)
                    : 0f;
                if (best == null || d < bestDist)
                {
                    best = npc;
                    bestDist = d;
                }
            }
            return best;
        }

        // 플레이어 기준 상대 좌표 → 월드. 회전은 적용하지 않는다(컷신 오프셋과 같은 규약).
        private Vector3 PlayerRelative(Vector3 offset)
        {
            return playerTransform != null ? playerTransform.position + offset : offset;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
