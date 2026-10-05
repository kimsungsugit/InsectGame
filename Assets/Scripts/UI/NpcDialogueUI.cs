using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.NPC;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 주민 대화 모달 — 하단 대화 패널(이름 + 대사, [다음]/[닫기]).
    /// Show: Register + SetFrozen(true) + npc.BeginTalk / CloseModal: 역순 해제 (CaptureChoiceUI 관례).
    /// GUI.Button은 터치 합성 클릭으로 동작 — 대화 중 SetFrozen이라 조이스틱 점유 문제 없음.
    /// </summary>
    public class NpcDialogueUI : MonoBehaviour, IModalUI
    {
        private PlayerMovement playerMovement;

        private VillagerNpc currentNpc;
        private string[] lines;
        private int lineIndex;
        private bool isOpen;
        private int openedFrame; // 여는 터치가 같은 프레임의 버튼을 누르는 것 방지용

        // GUIStyle 1회 캐시 (OnGUI 매 프레임 new 금지)
        private GUIStyle nameStyle;
        private GUIStyle lineStyle;
        private GUIStyle buttonStyle;
        private GUIStyle choiceStyle;
        private bool stylesInited;

        // 패널 페이드 상태(UIHelper.AnimatePanelOpen이 소유). CharacterOutfitUI와 같은 관례.
        private TweenHandle openFade;
        private bool wasOpen;

        // 스토리 비트 렌더 — StoryDirector.StoryBeatTriggered 구독. 기존 대화 모달 렌더 재사용.
        private InsectGame.Story.StoryDirector storyDirector;
        private InsectGame.Story.StoryObjectiveTracker objectiveTracker;
        public void AutoWire(InsectGame.Story.StoryObjectiveTracker tracker)
        {
            if (objectiveTracker == null) objectiveTracker = tracker;
        }

        private InsectGame.Story.StoryBeat currentBeat;
        private InsectGame.Story.StoryLine[] storyLines;
        private bool storyMode;
        // 다시보기 — 저널에서 이미 열람한 비트를 다시 읽는 중. storyMode는 켠 채로 두어
        // 화자명·초상 렌더를 그대로 쓰되, 닫을 때 CompleteBeat만 건너뛴다.
        // **이 플래그가 없으면 다시 읽을 때마다 onComplete 보상이 재지급된다.**
        private bool storyReplay;

        // 진행 표시("n/총") 캐시 — 줄이 바뀔 때만 다시 만든다.
        private string progressCache;
        private int progressCacheIndex = -1;
        private int progressCacheTotal = -1;

        // ── 스토리 무대(비트를 열 때 한 번 계산) ── 줄마다 누가 어디 서고 누가 말하는지, 어떤 연출인지.
        // 규칙은 StoryDialogueStaging(순수 계산)이 정하고 여기선 그리기만 한다.
        private StoryDialogueStaging.Stage[] storyStages;
        private StoryDialogueStaging.LineFx[] storyFx;
        private string[] storyActiveIds;
        private float lineShownAt;
        private float speakerChangedAt;
        private bool lineRevealAll;
        private GUIStyle namePlateStyle;
        private GUIStyle progressStyle;
        private readonly GUIContent namePlateContent = new GUIContent();

        // ── 「지난 이야기」 카드 ── 장을 여는 비트가 **처음** 뜰 때(다시보기 아님) 첫 대사 앞에 한 장.
        // 자리는 StoryRecapLayout(순수 계산)이 정하고, 줄 높이는 래핑으로 재서 (장, 화면 크기)마다 한 번 굽는다.
        private InsectGame.Story.StoryChapter recapChapter;
        private bool recapShowing;
        private float recapShownAt;
        private StoryRecapLayout.Plan recapPlan;
        private InsectGame.Story.StoryChapter recapPlanChapter;
        private float recapPlanW = -1f, recapPlanH = -1f;
        private string[] recapLineTexts;
        private GUIStyle recapTitleStyle;
        private GUIStyle recapSectionStyle;
        private GUIStyle recapBodyStyle;
        private GUIStyle recapGoalStyle;
        private GUIStyle recapHintStyle;

        // 잡담 띠의 "다음 이야기 · 할 일 — 이유" — 할 일·이유가 바뀔 때만 잇는다(OnGUI 매 패스 문자열 할당 방지).
        private string nextStoryCache;
        private string nextStoryLabelSource;
        private string nextStoryWhySource;

        /// <summary>지금 「지난 이야기」 카드가 떠 있는가(첫 대사 앞).</summary>
        public bool IsShowingRecap => isOpen && recapShowing;

        public bool IsOpen => isOpen;

        // 라이벌 대결 — 평소 대화에 [대결] 버튼을 붙인다(라온). 누르면 대화를 닫고 다음 Update에서 건다:
        // OnGUI 안에서 전투를 시작하면 이 모달이 닫히는 프레임과 전투 화면이 열리는 프레임이 엉킨다.
        private InsectGame.NPC.NpcDuelController duelController;
        private bool rivalDuelOffered;
        private string pendingRivalNpcId;

        public void AutoWire(InsectGame.NPC.NpcDuelController duel)
        {
            if (duelController == null) duelController = duel;
        }

        // 대사 직후 대결(StoryBeat.duelAfter) — 선택지 결과 대사처럼 자기 값이 빈 비트 뒤에도 대결이 기다리면
        // 마지막 버튼을 「승부!」로 바꾼다. 대결을 여는 건 런처다(이 창은 평소처럼 닫기만 한다).
        private InsectGame.Story.StoryDuelLauncher duelLauncher;
        private GUIStyle duelButtonStyle;

        public void AutoWire(InsectGame.Story.StoryDuelLauncher launcher)
        {
            if (duelLauncher == null) duelLauncher = launcher;
        }

        /// <summary>
        /// 지금 떠 있는 스토리 대사가 끝나면 대결이 열리는가 — 비트 자신의 <c>duelAfter</c>, 또는 런처가 이미 기다리는 대결
        /// (선택지를 단 비트가 끝나며 대기열에 넣었다). 다시보기·꿈은 대결을 열지 않는다.
        /// </summary>
        private bool StoryLeadsToDuel()
        {
            if (!storyMode || currentBeat == null) return false;
            return DialogueDuelPrompt.LeadsToDuel(currentBeat.duelAfter,
                duelLauncher != null && duelLauncher.HasPendingDuel, storyReplay, DreamPrologueState.Active);
        }

        public void AutoWire(PlayerMovement player)
        {
            if (playerMovement == null) playerMovement = player;
        }

        // 스토리 지휘자 주입 — 비트 발화 구독. Bootstrap이 호출.
        public void AutoWire(InsectGame.Story.StoryDirector director)
        {
            if (storyDirector == null && director != null)
            {
                storyDirector = director;
                storyDirector.StoryBeatTriggered += OnStoryBeatTriggered;
            }
        }

        private void OnDestroy()
        {
            if (storyDirector != null)
                storyDirector.StoryBeatTriggered -= OnStoryBeatTriggered;
        }

        // 대사 앞 연출 게이트(옵션). 배선되지 않으면 지금까지처럼 곧바로 대사를 띄운다.
        private InsectGame.Story.IStoryStagePrelude stagePrelude;

        /// <summary>
        /// NPC 등장 연출 주입 — 비트에 <c>stageEnterId</c>가 있으면 <b>대사보다 먼저</b> 돌린다.
        /// "라온이 뛰어 들어오고 나서 말한다"의 순서가 여기서 갈린다.
        /// </summary>
        public void AutoWire(InsectGame.Story.IStoryStagePrelude prelude)
        {
            if (stagePrelude == null) stagePrelude = prelude;
        }

        private void OnStoryBeatTriggered(InsectGame.Story.StoryBeat beat)
        {
            // 연출이 있으면 그것이 끝나며 ShowStory를 부른다. 연출 쪽은 어떤 경로로 끝나든
            // (도착·건너뛰기·타임아웃) 콜백을 반드시 부르기로 계약돼 있다 — 안 그러면 이 비트가
            // pendingBeatId에 갇혀 캠페인이 멈춘다.
            //
            // **예외까지 여기서 막는다.** `StoryDirector.FireBeat`는 이벤트를 쏘기 **전에**
            // `pendingBeatId`를 커밋하고 `EvaluateTriggers`는 그 비트를 영구 스킵하므로,
            // 연출 쪽에서 예외가 튀어 `ShowStory`에 도달하지 못하면 그 세션에서 재발화 경로가 없다.
            // 연출의 자체 타임아웃도 구제하지 못한다 — 재생 상태를 세우기 **전에** 던지면
            // 타임아웃 자체가 무장되지 않는다. 연출을 잃는 건 감수해도 진행은 잃지 않는다.
            try
            {
                if (stagePrelude != null && stagePrelude.TryPlayPrelude(beat, () => ShowStory(beat))) return;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Dialogue] 스토리 연출 실패 — 대사로 넘어간다: {e}");
            }
            ShowStory(beat);
        }

        // 스토리 비트를 대화 모달로 렌더 — lines[]를 순차 표시(speaker는 라인별). 닫으면 CompleteBeat 콜백.
        // 장을 여는 비트면 첫 대사 앞에 「지난 이야기」 카드가 한 장 먼저 뜬다(StoryChapter.openingBeatId).
        public void ShowStory(InsectGame.Story.StoryBeat beat)
        {
            OpenStory(beat, false);
        }

        /// <summary>
        /// 스토리 대사를 연다. <paramref name="replay"/>(저널 다시보기)면 닫을 때 완료 처리를 건너뛰고 「지난 이야기」 카드도 띄우지 않는다 —
        /// 카드는 그 장에 처음 들어설 때의 되짚기다. 대사 앞 연출이 붙은 비트는 연출이 끝나며 여기로 오므로 순서는
        /// <b>대사 앞 영상(<c>introVideoId</c>) → 등장 연출(<c>stageEnterId</c>) → 「지난 이야기」 카드 → 첫 대사</b>다
        /// (앞 둘은 <see cref="InsectGame.Story.StoryPreludeChain"/>이 차례로 돌리고, 카드는 여기서 선다 — 카드가 영상·연출과 겹치지 않는다).
        /// 대사 <b>뒤</b> 영상(<c>videoId</c>)·컷신은 대사를 닫은 뒤(<c>StoryBeatCompleted</c>)에 돈다.
        /// </summary>
        private void OpenStory(InsectGame.Story.StoryBeat beat, bool replay)
        {
            if (beat == null) return;

            // **꺼진·파괴된 창으로는 열지 않는다.** 대사 앞 영상·등장 연출은 어떤 길로 끝나든 콜백(ShowStory)을 부르는데,
            // 씬 재로드·로그아웃·UI 루트 토글(오프닝 다시보기)이 그 끝을 만들면 이 창이 먼저 꺼져 있을 수 있다. 그대로 열면
            // static ModalUIRegistry에 열린 채 등록된다 — 꺼진 창은 OnGUI·OnDisable이 다시 안 돌아 닫힐 길이 없고, 파괴된 뒤에도
            // IModalUI 참조 비교는 null이 아니라(IsAnyOpen의 정리가 못 거른다) 새 씬의 조작이 영영 막힌다.
            // 그래서 비트를 쥐고만 있다가 다시 켜지면 연다(OnEnable). 씬째 사라지면 비트는 열람 표시 없이 남아 다음 부팅에 다시 뜬다 —
            // 렌더러가 없을 때의 StoryDirector 정책(deferredBeat: 보류했다가 구독되면 띄우고, 끝내 없으면 seen 미마킹)과 같다.
            // 다시보기는 쥐지 않는다 — 저널에서 다시 누르면 된다.
            if (this == null) return;
            if (!isActiveAndEnabled)
            {
                if (!replay) heldStoryBeat = beat;
                return;
            }

            // 대사 없음 — 표시 없이 즉시 완료(보상/seen 처리).
            if (beat.lines == null || beat.lines.Count == 0)
            {
                if (!replay && storyDirector != null) storyDirector.CompleteBeat(beat.beatId);
                return;
            }

            // 다른 모달(주민 대화)이 열려 있으면 정리 후 스토리로 전환.
            if (isOpen) CloseModal();

            // 런처는 부트스트랩이 AutoWire로 잇는다. 배선이 빠졌을 때만 한 번 찾는다(비트를 열 때만 — 매 프레임 아님).
            if (duelLauncher == null) duelLauncher = FindFirstObjectByType<InsectGame.Story.StoryDuelLauncher>();

            currentBeat = beat;
            storyMode = true;
            storyReplay = replay;
            recapChapter = null;
            recapShowing = false;
            if (InsectGame.Story.StoryService.TryGetChapterOpenedBy(beat.beatId, out InsectGame.Story.StoryChapter chapter)
                && StoryRecapLayout.ShouldShow(chapter, replay))
            {
                recapChapter = chapter;
                recapShowing = true;
                recapShownAt = Time.unscaledTime;
            }
            storyLines = beat.lines.ToArray();
            lines = new string[storyLines.Length];
            for (int i = 0; i < storyLines.Length; i++)
                lines[i] = storyLines[i] != null ? storyLines[i].text : "";
            PrepareStage(beat);
            BeginStoryLine(0);
            isOpen = true;
            openedFrame = Time.frameCount;
            ModalUIRegistry.Register(this);
            if (playerMovement != null) playerMovement.SetFrozen(true);
        }

        /// <summary>
        /// 이미 열람한 비트를 저널에서 다시 읽는다 — <see cref="ShowStory"/>와 렌더는 같고
        /// 닫을 때 <c>CompleteBeat</c>만 부르지 않는다(보상 재지급·seen 재기록 방지).
        /// <b>미열람 비트에는 절대 쓰지 말 것</b> — 대사를 보여주면서 seen 마킹은 안 되므로
        /// 나중에 정상 트리거로 한 번 더 뜬다. 호출부(StoryJournalUI)가 열람 여부를 걸러야 한다.
        /// </summary>
        public void ShowStoryReplay(InsectGame.Story.StoryBeat beat)
        {
            if (beat == null || beat.lines == null || beat.lines.Count == 0) return;

            // **읽고 있는 진짜 비트를 밀어내지 않는다.** ShowStory는 열려 있는 모달을
            // CloseModal로 정리하는데, 그 경로가 CompleteBeat를 불러 **읽지 않은 대사가
            // 보상까지 받고 열람 처리된다.** StoryDirector 쪽은 발화 경로에 같은 가드를
            // 걸어 뒀지만(FireBeat), 저널 다시보기는 그 경로를 거치지 않는다.
            if (isOpen && storyMode && !storyReplay)
            {
                Debug.LogWarning("[Dialogue] 진행 중인 스토리 대사가 있어 다시보기를 건너뛴다");
                return;
            }

            // 다시보기 표지는 OpenStory가 열린 모달을 정리(CloseModal)한 **뒤에** 켠다 — 카드도 다시보기에선 뜨지 않는다.
            OpenStory(beat, true);
        }

        /// <summary>대화 시작 — WorldInteractionController가 호출.</summary>
        public void Show(VillagerNpc npc)
        {
            if (npc == null || isOpen) return;
            // 진행 중인 스토리/주민 대화를 교체하면 화자와 완료 콜백이 어긋난다.
            storyMode = false;
            storyReplay = false;
            currentBeat = null;
            storyLines = null;
            recapChapter = null;
            recapShowing = false;
            currentNpc = npc;
            // 스토리 인물은 전용 잡담이 있으면 그걸 쓴다 — 마을 주민 풀로 떨어지면
            // 명부회 간부가 날씨 이야기를 한다(비트를 아직 못 봤거나 전부 본 뒤의 경로).
            if (!npc.IsStoryNpc
                || !NpcDialogueDatabase.TryGetStoryNpcLines(npc.StoryNpcId,
                    storyDirector != null && storyDirector.HasDefeatedStoryNpc(npc.StoryNpcId),
                    storyDirector != null && storyDirector.IsRegionCleansed(npc.RegionId), out lines))
            {
                lines = NpcDialogueDatabase.GetLines(npc.NpcId, npc.RegionId);
            }
            lineIndex = 0;
            rivalDuelOffered = npc.IsStoryNpc && duelController != null
                && duelController.CanRivalDuel(npc.StoryNpcId, Time.time);
            isOpen = true;
            openedFrame = Time.frameCount;
            ModalUIRegistry.Register(this);
            if (playerMovement != null)
            {
                playerMovement.SetFrozen(true);
                npc.BeginTalk(playerMovement.transform);
            }
            else
            {
                npc.BeginTalk(null);
            }
        }

        public void CloseModal()
        {
            if (!isOpen) return;
            // 선택지가 떠 있는데 고르지 않고 닫히면(ESC) 선택 비트는 seen이 되고 결과 둘은 영영 미열람이다 —
            // 저널은 미열람 선택 결과를 숨기고 다시보기엔 버튼이 없어 **결과를 볼 길이 0**이 된다.
            // 그래서 선택 중엔 닫기를 삼킨다. OnDisable(UI 루트 토글)은 ForceClose로 지나간다.
            if (HasChoices && !choiceResolved) return;
            choiceResolved = false;
            isOpen = false;
            // 페이드 상태를 되돌린다 — OnGUI가 `!isOpen`에서 곧바로 return하므로 닫힘 전이가
            // AnimatePanelOpen에 전달되지 않는다. 그대로 두면 wasOpen이 true로 굳어
            // **두 번째 열림부터 페이드가 사라진다**(다음 열림에서 전이가 감지되지 않는다).
            wasOpen = false;
            ModalUIRegistry.Unregister(this);
            if (playerMovement != null) playerMovement.SetFrozen(false);
            if (currentNpc != null) currentNpc.EndTalk();
            currentNpc = null;
            lines = null;
            // 카드가 떠 있는 채 닫혀도(건너뛰기·ESC) 다음 열림에 남지 않게 — 카드는 OpenStory만 다시 세운다.
            recapChapter = null;
            recapShowing = false;

            // 스토리 비트였으면 완료 콜백(보상/seen). 상태를 먼저 비워 CompleteBeat 재진입에 안전.
            // 저널 다시보기(storyReplay)는 이미 열람·보상 완료된 비트라 콜백을 건너뛴다.
            if (storyMode)
            {
                InsectGame.Story.StoryBeat done = currentBeat;
                bool replay = storyReplay;
                storyMode = false;
                storyReplay = false;
                currentBeat = null;
                storyLines = null;
                if (!replay && storyDirector != null && done != null)
                    storyDirector.CompleteBeat(done.beatId);
            }
        }

        private bool choiceResolved;

        // 꺼진 채 열려던 스토리 비트(OpenStory의 가드) — 다시 켜지면 그때 연다. 씬째 사라지면 같이 사라진다(비트는 미열람으로 남는다).
        private InsectGame.Story.StoryBeat heldStoryBeat;

        private void OnEnable()
        {
            if (heldStoryBeat == null || isOpen) return;
            InsectGame.Story.StoryBeat beat = heldStoryBeat;
            heldStoryBeat = null;
            OpenStory(beat, false);
        }

        private void OnDisable()
        {
            choiceResolved = true;   // 루트가 꺼지는 건 플레이어의 닫기가 아니다 — 선택 가드를 지나간다
            // Unregister만 하면 isOpen이 true로 남아 다시 켰을 때 "열린 것으로 아는데
            // 레지스트리엔 없는" 상태가 된다. 그러면 (a) HandleEscape가 이 모달을 무시해
            // ESC가 frozen만 풀고 Update가 즉시 재프리즈 → ESC 영구 무력화, (b)
            // IsAnyOpen()이 false라 WorldInteractionController의 재진입 가드가 뚫려
            // 이전 currentNpc를 EndTalk 없이 덮어써 그 주민이 Talking에 갇힌다
            // (VillagerNpc.CanTalk가 영구 false → 다시는 대화 불가).
            //
            // 옛 주석은 CaptureChoiceUI 관례를 인용했으나 그건 이 프로젝트가 이미 P1으로
            // 두 번 폐기한 방식이다(CharacterOutfitUI/RegionMapUI 라운드). 현재 표준은
            // 상태까지 되돌리는 것 — CloseModal이 그 일을 전부 한다.
            CloseModal();
        }

        private void Update()
        {
            if (!string.IsNullOrEmpty(pendingRivalNpcId) && !isOpen)
            {
                string rivalId = pendingRivalNpcId;
                pendingRivalNpcId = null;
                if (duelController != null) duelController.TryStartRivalDuel(rivalId, Time.time);
            }
            if (!isOpen) return;
            // 대화 상대가 사라짐(ApplyTuning 비활성화 등) — 안전 종료. 스토리 모드는 NPC가 없으므로 스킵.
            if (!storyMode && (currentNpc == null || !currentNpc.gameObject.activeInHierarchy))
            {
                CloseModal();
                return;
            }

            // PlayerMovement의 AutoUnfreeze(20s)가 대화를 길게 읽는 동안 프리즈를 풀면
            // 모달이 열린 채 이동 가능해진다 — 열려 있는 동안 프리즈를 재적용(타이머 리셋).
            if (playerMovement != null && !playerMovement.IsFrozen)
                playerMovement.SetFrozen(true);
        }

        private void OnGUI()
        {
            if (!isOpen || lines == null || lines.Length == 0) return;

            // 대화를 연 바로 그 터치(합성 마우스)가 같은 자리의 [다음]/[닫기]를 즉시 누르는
            // 것 방지 — 세로 고해상 기기에서 상호작용 원버튼과 대화 버튼의 y밴드가 겹친다.
            Event evt = Event.current;
            if (evt != null && Time.frameCount <= openedFrame + 1
                && (evt.type == EventType.MouseDown || evt.type == EventType.MouseUp))
            {
                evt.Use();
                return;
            }

            EnsureStyles();
            UIScale.Begin();
            // 패널 페이드 — 대사창이 툭 튀어나오면 NPC가 다가와 인사하는 흐름이 거기서 끊긴다.
            // **열릴 때만** 페이드한다: 닫을 때는 CloseModal이 lines/storyLines/currentBeat을
            // 그 자리에서 비우고 보상까지 지급하므로(CompleteBeat), 사라지는 동안 그릴 내용이
            // 남아 있지 않다. 내용을 살려 두려면 비트 완료 시점을 미뤄야 하는데 그건 이 저장소가
            // 영구 정지를 겪은 자리라 건드리지 않는다.
            float panelAlpha = UIHelper.AnimatePanelOpen(ref openFade, isOpen, ref wasOpen);
            GUI.color = new Color(1f, 1f, 1f, panelAlpha);

            // **스토리는 무대, 잡담은 하단 띠.** 둘을 같은 모양으로 그리면 지금 보고 있는 것이
            // 이야기인지 잡담인지 구분되지 않는다 — 스토리는 딤 위에 인물이 서서 주고받고,
            // 잡담은 초상 없이 작은 검은 띠 하나다.
            if (storyMode && recapShowing) DrawRecapCard(panelAlpha);
            else if (storyMode) DrawStoryStage(panelAlpha);
            else DrawAmbient(panelAlpha);

            // 페이드 알파를 남기지 않는다 — GUI.color는 전역이라 다음 컴포넌트의 OnGUI까지 물든다.
            GUI.color = Color.white;
            UIScale.End();
        }

        // ───────────────────────── 주민 잡담 — 하단 띠 ─────────────────────────
        private void DrawAmbient(float panelAlpha)
        {
            nameStyle.fontSize = 26;
            lineStyle.fontSize = 24;
            Rect panel = UISafeLayout.BottomPanel(920f, 258f);
            float panelW = panel.width, panelH = panel.height, px = panel.x, py = panel.y;

            // 페이드 알파를 곱해 넣고, 복구도 흰색이 아니라 그 알파로 되돌린다 —
            // 흰색으로 되돌리면 이 뒤의 이름·대사·버튼이 페이드에서 빠진다.
            GUI.color = new Color(0f, 0f, 0f, 0.82f * panelAlpha);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, panelAlpha);

            string npcName = currentNpc != null ? currentNpc.DisplayName : "주민";
            float textX = px + 28f;
            float textW = panelW - 56f;
            float nameH = 34f;
            float nameY = py + 16f;
            float lineY = nameY + nameH + 6f;
            float btnBandH = 114f;
            float lineH = Mathf.Max(40f, py + panelH - btnBandH - lineY);

            UIHelper.LabelFit(new Rect(textX, nameY, Mathf.Max(40f, textW - 110f), nameH), npcName, nameStyle);
            // 대사 길이는 데이터가 정한다 — 상자에 안 들어가면 폰트를 줄여 맞춘다.
            UIHelper.LabelFit(new Rect(textX, lineY, textW, lineH),
                lines[Mathf.Clamp(lineIndex, 0, lines.Length - 1)], lineStyle);

            if (objectiveTracker != null && objectiveTracker.HasObjective)
            {
                Color previousTextColor = lineStyle.normal.textColor;
                lineStyle.normal.textColor = UITheme.Instance.textSecondary;
                // 이유(「왜」)가 있으면 같은 줄 끝에 " — 이유"로 붙인다. 이 띠는 대사 줄과 버튼 줄 사이 한 줄뿐이라
                // 둘째 줄을 들이면 대사 상자가 줄어든다 — 넘치면 LabelFit이 글자를 줄여 맞춘다.
                UIHelper.LabelFit(new Rect(textX, py + panelH - 108f, textW, 32f),
                    NextStoryLabel(objectiveTracker.Label, objectiveTracker.Why), lineStyle);
                lineStyle.normal.textColor = previousTextColor;
            }

            // 진행 표시 (n/총) — **상자 높이를 숫자로 박지 않는다**(한글 줄높이 ≈ fontSize × 1.35).
            UIHelper.LabelFit(new Rect(px + panelW - 120f, nameY, 92f, Mathf.Ceil(nameStyle.fontSize * 1.35f)),
                ProgressLabel(), nameStyle);

            float btnW = 170f, btnH = 56f;
            float btnY = py + panelH - btnH - 14f;
            if (lineIndex < lines.Length - 1)
            {
                if (GUI.Button(new Rect(px + panelW - btnW * 2f - 40f, btnY, btnW, btnH), "다음", buttonStyle))
                    lineIndex++;
            }
            if (GUI.Button(new Rect(px + panelW - btnW - 24f, btnY, btnW, btnH), "닫기", buttonStyle))
                CloseModal();
            // 라이벌 대결 — 대사를 다 넘기지 않아도 건다(잡담은 매번 같은 말이라 끝까지 읽힐 이유가 없다).
            if (rivalDuelOffered && currentNpc != null
                && GUI.Button(new Rect(px + 24f, btnY, 220f, btnH), "승부하기", buttonStyle))
            {
                pendingRivalNpcId = currentNpc.StoryNpcId;
                CloseModal();
            }
        }

        /// <summary>"다음 이야기 · 할 일" 또는 "다음 이야기 · 할 일 — 이유". 할 일·이유가 바뀔 때만 다시 잇는다.</summary>
        private string NextStoryLabel(string label, string why)
        {
            if (nextStoryCache == null || !ReferenceEquals(label, nextStoryLabelSource) || !ReferenceEquals(why, nextStoryWhySource))
            {
                nextStoryLabelSource = label;
                nextStoryWhySource = why;
                nextStoryCache = ComposeNextStory(label, why);
            }
            return nextStoryCache;
        }

        /// <summary>잡담 띠의 다음 이야기 한 줄 — 순수. 이유가 비면 예전과 같다.</summary>
        internal static string ComposeNextStory(string label, string why)
        {
            string head = "다음 이야기 · " + (label ?? string.Empty);
            return string.IsNullOrEmpty(why) ? head : head + " — " + why;
        }

        // 문자열은 줄이 바뀔 때만 만든다 — OnGUI는 프레임당 여러 번 도는데 보간은 매번 새 문자열이다.
        private string ProgressLabel()
        {
            if (progressCacheIndex != lineIndex || progressCacheTotal != lines.Length)
            {
                progressCacheIndex = lineIndex;
                progressCacheTotal = lines.Length;
                progressCache = (lineIndex + 1) + "/" + lines.Length;
            }
            return progressCache;
        }

        // ───────────────────────── 스토리 — 무대 ─────────────────────────

        /// <summary>비트를 열 때 한 번 — 줄마다 초상 ID·좌우 배치·연출 태그를 풀어 둔다.</summary>
        private void PrepareStage(InsectGame.Story.StoryBeat beat)
        {
            int n = storyLines.Length;
            string[] ids = new string[n];
            storyFx = new StoryDialogueStaging.LineFx[n];
            for (int i = 0; i < n; i++)
            {
                InsectGame.Story.StoryLine line = storyLines[i];
                string speaker = line != null ? line.speaker : null;
                // 지문은 누구의 얼굴도 빌리지 않는다 — 비트 화자로 떨어지면 해설을 그 사람이 말하는 것처럼 보인다.
                ids[i] = StoryDialogueStaging.IsNarration(speaker)
                    ? null
                    : NpcDialogueDatabase.StoryPortraitId(speaker, beat.speakerNpcId);
                // 초상이 없는 인물은 무대에 세우지 않는다(이름표만 뜬다).
                if (ids[i] != null && !GetStoryPortrait(ids[i], out _, out _, out _, out _, out _, out _, out _))
                    ids[i] = null;
                storyFx[i] = StoryDialogueStaging.ParseFx(line != null ? line.fx : null);
            }
            storyStages = StoryDialogueStaging.AssignSides(ids);
            storyActiveIds = new string[n];
            for (int i = 0; i < n; i++)
            {
                StoryDialogueStaging.Stage st = storyStages[i];
                storyActiveIds[i] = st.Active == StoryDialogueStaging.Side.Left ? st.LeftId
                    : st.Active == StoryDialogueStaging.Side.Right ? st.RightId : null;
            }
        }

        private void BeginStoryLine(int index)
        {
            float now = Time.unscaledTime;
            bool speakerChanged = index == 0 || storyActiveIds == null || index >= storyActiveIds.Length
                || storyActiveIds[index] != storyActiveIds[index - 1];
            lineIndex = index;
            lineShownAt = now;
            lineRevealAll = false;
            if (speakerChanged) speakerChangedAt = now;
        }

        private StoryDialogueStaging.LineFx CurrentFx =>
            storyFx != null && lineIndex >= 0 && lineIndex < storyFx.Length
                ? storyFx[lineIndex] : StoryDialogueStaging.LineFx.None;

        /// <summary>지금 줄이 다 나왔는가 — 다 나오기 전의 [다음]·탭은 넘기지 않고 줄을 마저 보여준다.</summary>
        private bool IsStoryLineRevealed()
        {
            if (lineRevealAll || lines == null || lineIndex < 0 || lineIndex >= lines.Length) return true;
            string text = lines[lineIndex] ?? "";
            return StoryDialogueStaging.VisibleChars(text.Length, Time.unscaledTime - lineShownAt, CurrentFx) >= text.Length;
        }

        /// <summary>[다음]·탭·Space — 덜 나왔으면 마저 보여주고, 다 나왔으면 다음 줄, 마지막이면 닫는다.</summary>
        private void AdvanceStory()
        {
            if (!IsStoryLineRevealed())
            {
                lineRevealAll = true;
                return;
            }
            if (lineIndex < lines.Length - 1)
            {
                BeginStoryLine(lineIndex + 1);
                return;
            }
            if (!HasChoices) CloseModal();   // 선택지는 반드시 골라야 한다(CloseModal도 삼킨다)
        }

        private void DrawStoryStage(float panelAlpha)
        {
            UITheme t = UITheme.Instance;
            float now = Time.unscaledTime;
            float since = now - lineShownAt;
            StoryDialogueStaging.LineFx fx = CurrentFx;
            InsectGame.Story.StoryLine sl = storyLines != null && lineIndex >= 0 && lineIndex < storyLines.Length
                ? storyLines[lineIndex] : null;
            bool narration = sl != null && StoryDialogueStaging.IsNarration(sl.speaker);
            StoryDialogueStaging.Stage stage = storyStages != null && lineIndex >= 0 && lineIndex < storyStages.Length
                ? storyStages[lineIndex] : default(StoryDialogueStaging.Stage);

            UISurface.Dim((fx & StoryDialogueStaging.LineFx.Dark) != 0 ? 0.86f : 0.66f);

            // 상자는 아래, 인물은 그 위에 선다. 세로 화면은 폭이 좁아 대사가 여러 줄로 접히므로 상자를 키운다.
            // 상자 자리는 「지난 이야기」 카드와 같은 순수 계산(StoryRecapLayout.DialogueBox) — 카드가 상자 위 판에 선다.
            bool portraitLayout = UIScale.IsPortrait;
            Rect box = StoryRecapLayout.DialogueBox(HudFrame.Current);
            box.x += StoryDialogueStaging.ShakeOffset(fx, since);

            // ── 인물 ── 발은 상자 뒤로 숨는다. 듣는 쪽을 먼저, 말하는 쪽을 나중에(앞에) 그린다.
            float figH = portraitLayout ? 440f : 360f;
            float scale = figH / 137f;                       // CharacterPortraitRenderer 치비 전신 ≈ 137 × scale
            float cy = box.y + 40f - 74.5f * scale;          // 발끝(cy + 74.5s)이 상자 윗변 40px 아래
            float inset = box.width * (portraitLayout ? 0.23f : 0.17f);
            float leftX = box.x + inset, rightX = box.xMax - inset;
            float hop = StoryDialogueStaging.HopOffset(now - speakerChangedAt);
            bool leftActive = stage.Active == StoryDialogueStaging.Side.Left;
            bool rightActive = stage.Active == StoryDialogueStaging.Side.Right;
            if (!leftActive) DrawStageFigure(stage.LeftId, leftX, cy, scale, false, 0f, now);
            if (!rightActive) DrawStageFigure(stage.RightId, rightX, cy, scale, false, 0f, now);
            if (leftActive) DrawStageFigure(stage.LeftId, leftX, cy, scale, true, hop, now);
            if (rightActive) DrawStageFigure(stage.RightId, rightX, cy, scale, true, hop, now);

            // ── 상자 ──
            UISurface.Card(box, t.surfaceBase, t.accentAmber);
            // 상단 액센트 — 둥근 모서리를 뚫지 않게 긴 축을 반경만큼 물린다(rules/ui-layout.md).
            UISurface.Flat(new Rect(box.x + UITheme.Radius.Card, box.y + 3f, box.width - UITheme.Radius.Card * 2f, 5f),
                t.accentAmber);

            // ── 이름표 ── 말하는 사람 쪽 가장자리. 지문엔 없다.
            if (!narration)
                DrawNamePlate(box, sl, rightActive);

            // ── 대사 ──
            float btnBandH = portraitLayout ? 112f : 100f;
            Rect textRect = new Rect(box.x + 40f, box.y + 50f, box.width - 80f,
                Mathf.Max(40f, box.height - 50f - btnBandH));
            string text = lines[Mathf.Clamp(lineIndex, 0, lines.Length - 1)] ?? "";
            int visible = lineRevealAll ? text.Length : StoryDialogueStaging.VisibleChars(text.Length, since, fx);

            int baseSize = lineStyle.fontSize;
            FontStyle baseFontStyle = lineStyle.fontStyle;
            TextAnchor baseAnchor = lineStyle.alignment;
            Color baseColor = lineStyle.normal.textColor;
            int size = portraitLayout ? 36 : 34;
            if ((fx & StoryDialogueStaging.LineFx.Shout) != 0)
            {
                size = Mathf.RoundToInt(size * 1.15f);
                lineStyle.fontStyle = FontStyle.Bold;
            }
            if ((fx & StoryDialogueStaging.LineFx.Whisper) != 0)
            {
                size = Mathf.RoundToInt(size * 0.9f);
                lineStyle.fontStyle = FontStyle.Italic;
                lineStyle.normal.textColor = t.textSecondary;
            }
            if (narration)
            {
                lineStyle.fontStyle = FontStyle.Italic;
                lineStyle.alignment = TextAnchor.MiddleCenter;
                lineStyle.normal.textColor = new Color(0.9f, 0.86f, 0.74f);
            }
            lineStyle.fontSize = size;
            UIHelper.LabelFitReveal(textRect, text, visible, lineStyle);
            lineStyle.fontSize = baseSize;
            lineStyle.fontStyle = baseFontStyle;
            lineStyle.alignment = baseAnchor;
            lineStyle.normal.textColor = baseColor;

            bool revealed = visible >= text.Length;
            bool isLast = lineIndex >= lines.Length - 1;

            // 계속 표시 — 다 나왔고 다음 줄이 있으면 오른쪽 아래에서 깜빡인다.
            if (revealed && !isLast)
            {
                float blink = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(now * 3.2f));
                Color c = GUI.color;
                GUI.color = new Color(t.accentAmber.r, t.accentAmber.g, t.accentAmber.b, c.a * blink);
                UIHelper.LabelFit(new Rect(textRect.xMax - 44f, textRect.yMax - 44f, 44f, 44f), "▼", progressStyle);
                GUI.color = c;
            }

            // ── 진행 · 버튼 ──
            float btnW = portraitLayout ? 230f : 220f;
            float btnH = 72f;
            float btnY = box.yMax - btnH - 22f;
            UIHelper.LabelFit(new Rect(box.x + 36f, btnY + (btnH - 40f) * 0.5f, 110f, 40f), ProgressLabel(), progressStyle);

            bool choosing = isLast && revealed && HasChoices;
            if (choosing)
            {
                // 마지막 줄이 다 나온 뒤에만 고른다 — 덜 읽은 채 고르면 선택의 무게가 사라진다.
                float choiceX = box.x + 160f;
                DrawChoices(choiceX, btnY, box.xMax - 28f - choiceX, btnH);
                if (!isOpen) return;   // 골랐다 — CloseModal이 상태를 비웠다
            }
            else
            {
                // 대사 뒤 곧바로 대결이면 마지막 버튼이 「승부!」다(강조색) — 누르면 평소처럼 닫고, 대결은 런처가 연다.
                bool duelNext = StoryLeadsToDuel();
                Rect advanceRect = new Rect(box.xMax - btnW - 24f, btnY, btnW, btnH);
                bool advance = DialogueDuelPrompt.IsDuelButton(revealed, isLast, duelNext)
                    ? DrawDuelButton(advanceRect, DialogueDuelPrompt.DuelLabel)
                    : GUI.Button(advanceRect, DialogueDuelPrompt.AdvanceLabel(revealed, isLast, duelNext), buttonStyle);
                if (advance)
                {
                    AdvanceStory();
                    if (!isOpen) return;
                }
                // 건너뛰기 — 장면 전체를 닫는다(비트는 완료 처리된다). 마지막 줄에선 위 버튼이 그 일을 한다.
                // 선택지가 있는 비트는 건너뛸 수 없다 — CloseModal이 선택 전 닫기를 삼키므로 누르면 아무 일도 없다.
                // 대결이 이어지면 「건너뛰고 승부」 — 건너뛰어도 싸움은 열린다는 걸 버튼이 말한다.
                if (!isLast && !HasChoices
                    && GUI.Button(new Rect(box.xMax - btnW * 2f - 40f, btnY, btnW, btnH),
                        DialogueDuelPrompt.SkipButtonLabel(duelNext), buttonStyle))
                {
                    CloseModal();
                    return;
                }

                // 대사 영역을 누르거나 Space/Enter — [다음]과 같다. 버튼 띠와 겹치지 않는 자리라 버튼을 먹지 않는다.
                if (GUI.Button(new Rect(box.x, box.y, box.width, textRect.yMax - box.y), GUIContent.none, GUIStyle.none))
                {
                    AdvanceStory();
                    if (!isOpen) return;
                }
                Event e = Event.current;
                if (e != null && e.type == EventType.KeyDown
                    && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
                {
                    e.Use();
                    AdvanceStory();
                    if (!isOpen) return;
                }
            }

            // ── 번쩍임 ── 맨 위에 덮는다.
            float flash = StoryDialogueStaging.FlashOverlay(fx, since);
            if (flash > 0f)
                UISurface.Flat(new Rect(0f, 0f, UIScale.VirtualScreenWidth, UIScale.VirtualScreenHeight),
                    new Color(1f, 1f, 1f, flash * panelAlpha));
        }

        // ───────────────────────── 「지난 이야기」 카드 — 장을 여는 비트의 첫 대사 앞 ─────────────────────────

        /// <summary>카드를 넘긴다 — 첫 대사를 지금부터(타자 효과·화자 등장 포함) 시작한다.</summary>
        private void EndRecap()
        {
            recapShowing = false;
            BeginStoryLine(0);
        }

        /// <summary>
        /// 「지난 이야기」 카드 — 대사 상자 위 넓은 판(<see cref="StoryRecapLayout.Area"/>) 가운데. 아래 대사 상자 자리에는 같은 [다음 ▶]·[건너뛰기]가
        /// 같은 자리에 서고, 화면 어디를 눌러도·Space/Enter도 넘긴다(대사와 같은 입력). 뜬 뒤 <see cref="StoryRecapLayout.InputDelay"/>초는
        /// 입력을 받지 않는다 — 걷던 손가락이 카드를 바로 넘기지 않게. [건너뛰기]는 대사처럼 장면 전체를 닫는다(카드도 함께).
        /// </summary>
        private void DrawRecapCard(float panelAlpha)
        {
            if (recapChapter == null) { recapShowing = false; return; }
            UITheme t = UITheme.Instance;
            HudFrame f = HudFrame.Current;
            UISurface.Dim(0.78f);

            EnsureRecapPlan(f);
            StoryRecapLayout.Plan p = recapPlan;

            // ── 카드 ── 상단 액센트는 둥근 모서리를 뚫지 않게 긴 축을 반경만큼 물린다(rules/ui-layout.md).
            UISurface.Card(p.Card, t.surfaceBase, t.accentAmber);
            UISurface.Flat(new Rect(p.Card.x + UITheme.Radius.Card, p.Card.y + 3f, p.Card.width - UITheme.Radius.Card * 2f, 6f),
                t.accentAmber);
            string title = string.IsNullOrEmpty(recapChapter.title) ? recapChapter.chapterId : recapChapter.title;
            UIHelper.LabelFit(p.Title, title, recapTitleStyle);

            if (p.Recap != null && p.Recap.Length > 0)
            {
                recapSectionStyle.normal.textColor = t.textSecondary;
                UIHelper.LabelFit(p.RecapHeader, "지난 이야기", recapSectionStyle);
                for (int i = 0; i < p.Recap.Length && recapLineTexts != null && i < recapLineTexts.Length; i++)
                    UIHelper.LabelFit(p.Recap[i], recapLineTexts[i], recapBodyStyle);
                if (p.HasGoal) UISurface.Flat(p.Divider, t.surfaceBorder);
            }
            if (p.HasGoal)
            {
                recapSectionStyle.normal.textColor = t.accentMint;
                UIHelper.LabelFit(p.GoalHeader, "이번 목표", recapSectionStyle);
                UIHelper.LabelFit(p.Goal, recapChapter.goal, recapGoalStyle);
            }

            // ── 대사 상자 자리 ── 첫 대사가 설 자리에 같은 버튼을 세운다(누르던 자리 그대로 넘긴다).
            bool portraitLayout = f.Portrait;
            Rect box = StoryRecapLayout.DialogueBox(f);
            UISurface.Card(box, t.surfaceBase, t.accentAmber);
            UISurface.Flat(new Rect(box.x + UITheme.Radius.Card, box.y + 3f, box.width - UITheme.Radius.Card * 2f, 5f),
                t.accentAmber);

            bool ready = Time.unscaledTime - recapShownAt >= StoryRecapLayout.InputDelay;
            float btnBandH = portraitLayout ? 112f : 100f;
            Rect hintRect = new Rect(box.x + 40f, box.y + 40f, box.width - 80f, Mathf.Max(40f, box.height - 40f - btnBandH));
            Color baseColor = GUI.color;
            GUI.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (ready ? 1f : 0.4f));
            UIHelper.LabelFit(hintRect, "화면을 누르면 이야기가 시작돼요", recapHintStyle);
            GUI.color = baseColor;

            float btnW = portraitLayout ? 230f : 220f;
            float btnH = 72f;
            float btnY = box.yMax - btnH - 22f;
            // 버튼을 먼저 — IMGUI는 먼저 처리된 컨트롤이 MouseDown을 가져간다. 화면 전체 넘기기는 그 뒤에 깐다.
            if (GUI.Button(new Rect(box.xMax - btnW - 24f, btnY, btnW, btnH), "다음 ▶", buttonStyle) && ready)
            {
                EndRecap();
                return;
            }
            // 선택지가 있는 비트는 건너뛸 수 없다(CloseModal이 선택 전 닫기를 삼킨다) — 대사 화면과 같은 규칙.
            if (!HasChoices
                && GUI.Button(new Rect(box.xMax - btnW * 2f - 40f, btnY, btnW, btnH),
                    DialogueDuelPrompt.SkipButtonLabel(StoryLeadsToDuel()), buttonStyle) && ready)
            {
                CloseModal();
                return;
            }
            if (GUI.Button(new Rect(0f, 0f, UIScale.VirtualScreenWidth, UIScale.VirtualScreenHeight), GUIContent.none, GUIStyle.none)
                && ready)
            {
                EndRecap();
                return;
            }
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown
                && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                e.Use();
                if (ready) EndRecap();
            }
        }

        /// <summary>
        /// 카드 배치를 (장, 화면 크기)마다 한 번 굽는다 — 줄 높이는 래핑으로 재야 해서(CalcHeight) OnGUI 안에서만 부른다.
        /// 빈 줄은 건너뛴다. 줄머리에 "• "를 붙인다(아이가 줄을 하나씩 짚어 읽게).
        /// </summary>
        private void EnsureRecapPlan(HudFrame f)
        {
            if (recapLineTexts != null && ReferenceEquals(recapPlanChapter, recapChapter)
                && recapPlanW == f.Width && recapPlanH == f.Height) return;
            recapPlanChapter = recapChapter;
            recapPlanW = f.Width;
            recapPlanH = f.Height;

            List<string> src = recapChapter.recap;
            int n = 0;
            if (src != null)
                for (int i = 0; i < src.Count; i++)
                    if (!string.IsNullOrWhiteSpace(src[i])) n++;
            recapLineTexts = new string[n];
            float[] heights = new float[n];
            float cardW = StoryRecapLayout.CardWidth(f);
            float textW = StoryRecapLayout.TextWidth(cardW);
            int k = 0;
            if (src != null)
                for (int i = 0; i < src.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(src[i])) continue;
                    recapLineTexts[k] = "• " + src[i].Trim();
                    heights[k] = StoryRecapLayout.BodyHeight(UIHelper.MeasureWrappedHeight(recapBodyStyle, recapLineTexts[k], textW));
                    k++;
                }
            string goal = recapChapter.goal;
            float goalH = string.IsNullOrWhiteSpace(goal) ? 0f
                : StoryRecapLayout.BodyHeight(UIHelper.MeasureWrappedHeight(recapGoalStyle, goal, textW));
            recapPlan = StoryRecapLayout.Layout(StoryRecapLayout.Area(f), cardW, heights, goalH);
        }

        /// <summary>
        /// 무대 위 인물 하나. 말하는 쪽은 밝고 크게(화자가 바뀌면 한 번 뛰어오른다), 듣는 쪽은 어둡게 한 발 물러선다.
        /// 숨 쉬듯 아주 조금 오르내린다 — 정지한 종이 인형처럼 보이지 않게.
        /// </summary>
        private static void DrawStageFigure(string id, float x, float cy, float scale, bool active, float hop, float now)
        {
            if (!HasStoryPortrait(id)) return;

            // 렌더러는 호출부의 GUI.color를 곱해 그린다(패널 페이드 알파) — 듣는 쪽은 그 위에 어둡게 곱한다.
            Color prev = GUI.color;
            GUI.color = active
                ? prev
                : new Color(prev.r * 0.42f, prev.g * 0.42f, prev.b * 0.5f, prev.a);
            float s = active ? scale : scale * 0.93f;
            float breath = Mathf.Sin(now * 1.6f + x * 0.013f) * 1.5f;
            float y = cy - hop + breath + (active ? 0f : 10f);
            TryDrawStoryPortrait(id, x, y, s);
            GUI.color = prev;
        }

        /// <summary>이 스토리 인물에게 초상이 있는가 — 없으면 대사창·전투 컷인 모두 얼굴 없이 말한다.</summary>
        internal static bool HasStoryPortrait(string id)
        {
            return !string.IsNullOrEmpty(id)
                && GetStoryPortrait(id, out _, out _, out _, out _, out _, out _, out _);
        }

        /// <summary>
        /// 초상의 상의·모자색(모자 없으면 알파 0). 월드 외형(<c>NpcVisualBuilder.StoryNpcAppearance</c>)과
        /// 같은지 테스트가 대조한다 — 두 표가 따로 있어 한쪽만 고치면 대사창과 필드의 옷이 갈린다.
        /// </summary>
        internal static bool TryGetStoryPortraitColors(string id, out Color top, out Color hat)
        {
            top = hat = default;
            return !string.IsNullOrEmpty(id)
                && GetStoryPortrait(id, out _, out _, out _, out _, out _, out top, out hat);
        }

        /// <summary>초상의 피부·머리색·머리 모양(초상 번호) — 월드 외형과 같은지 테스트가 대조한다.</summary>
        internal static bool TryGetStoryPortraitFace(string id, out Color skin, out Color hair, out int hairStyle)
        {
            skin = hair = default;
            hairStyle = 0;
            return !string.IsNullOrEmpty(id)
                && GetStoryPortrait(id, out _, out skin, out hair, out hairStyle, out _, out _, out _);
        }

        /// <summary>
        /// 스토리 인물 전신 초상. 전투 컷인·말풍선도 이걸 쓴다 — 한 인물이 화면마다 다르게 생기면 안 된다.
        /// 기준은 대사창 무대와 같다: (cx, cy)가 몸 가운데, 발끝이 cy + 74.5 × scale. 표에 없으면 false.
        /// </summary>
        internal static bool TryDrawStoryPortrait(string id, float cx, float cy, float scale)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (!GetStoryPortrait(id, out int gender, out Color skin, out Color hair,
                    out int hairStyle, out int faceType, out Color top, out Color hat))
                return false;
            CharacterPortraitRenderer.DrawWithColors(cx, cy, scale, gender, skin, hair, hairStyle, faceType,
                top, new Color(0.18f, 0.22f, 0.28f), new Color(0.2f, 0.12f, 0.06f), hat, 0f, false);
            return true;
        }

        /// <summary>이름표 — 상자 윗변에 걸친 작은 카드. 오른쪽 사람이 말하면 오른쪽 끝으로 옮긴다.</summary>
        private void DrawNamePlate(Rect box, InsectGame.Story.StoryLine line, bool right)
        {
            string speaker = line != null ? line.speaker : null;
            string fallback = currentBeat != null ? currentBeat.speakerNpcId : null;
            string name = NpcDialogueDatabase.StorySpeakerName(string.IsNullOrEmpty(speaker) ? fallback : speaker);
            // 직접 다가가 말을 건 조우(NpcTalk)면 첫 줄 화자명에 플로리시 — 만남을 강조.
            if (lineIndex == 0 && currentBeat != null && currentBeat.trigger != null
                && currentBeat.trigger.type == "NpcTalk")
                name = "✦ " + name;

            namePlateContent.text = name;
            float w = Mathf.Clamp(namePlateStyle.CalcSize(namePlateContent).x + 48f, 160f, box.width * 0.5f);
            const float h = 62f;
            Rect plate = new Rect(right ? box.xMax - 32f - w : box.x + 32f, box.y - h * 0.55f, w, h);
            UITheme t = UITheme.Instance;
            UISurface.Card(plate, t.surfaceRaised, t.accentAmber);
            UIHelper.LabelFit(new Rect(plate.x + 12f, plate.y, plate.width - 24f, plate.height), name, namePlateStyle);
        }

        /// <summary>지금 떠 있는 스토리 비트의 마지막 줄에 선택지가 붙어 있는가(다시보기는 제외).</summary>
        public bool HasChoices =>
            isOpen && storyMode && !storyReplay && currentBeat != null
            && currentBeat.choices != null && currentBeat.choices.Count > 0;

        /// <summary>
        /// 선택지를 고른다 — 결과 비트를 큐 맨 앞에 걸고 대사창을 닫는다. 닫힘이 <c>CompleteBeat</c>를
        /// 부르고 그것이 큐를 흘리므로 결과가 곧바로 이어 뜬다. 배치 걸음 도구도 이 경로를 쓴다.
        /// </summary>
        public void SelectChoice(int index)
        {
            if (!HasChoices) return;
            index = Mathf.Clamp(index, 0, currentBeat.choices.Count - 1);
            choiceResolved = true;
            InsectGame.Story.StoryChoice choice = currentBeat.choices[index];
            if (storyDirector != null && choice != null) storyDirector.QueueChoice(choice.nextBeatId);
            else if (storyDirector == null) Debug.LogWarning("[Dialogue] StoryDirector 미배선 — 선택 결과가 유실된다");
            CloseModal();
        }

        /// <summary>한 줄에 놓는 선택 버튼 상한 — 세로 화면(선택 영역 ≈660px)에서 4개부터 패널을 뚫는다.</summary>
        private const int MaxChoicesPerRow = 3;

        // 선택 버튼을 가로로 나란히 — 2~3개. 문구 길이는 저작이 정한다(짧게, 14자 안팎)만,
        // 세로 화면에서는 그것도 잘리므로 LabelFit으로 글자를 줄여 맞춘다.
        private void DrawChoices(float x, float y, float width, float height)
        {
            int count = Mathf.Min(currentBeat.choices.Count, MaxChoicesPerRow);
            const float gap = 16f;
            float w = Mathf.Max(120f, (width - gap * (count - 1)) / count);
            choiceStyle.fontSize = storyMode ? 28 : 24;   // 스토리 버튼(72px)에 맞춘 기준 — LabelFit이 넘칠 때만 줄인다
            for (int i = 0; i < count; i++)
            {
                InsectGame.Story.StoryChoice choice = currentBeat.choices[i];
                string text = choice != null && !string.IsNullOrEmpty(choice.text) ? choice.text : "…";
                Rect r = new Rect(x + (w + gap) * i, y, w, height);
                if (DrawChoiceButton(r, text))
                {
                    SelectChoice(i);
                    return;   // CloseModal이 상태를 비웠다 — 같은 프레임에 더 그리지 않는다
                }
            }
        }

        // UISurface.Button과 같은 표면(그림자·둥근 몸통·호버)이되 라벨만 LabelFit으로 — 그쪽은 GUI.Label 고정이라
        // 폰트 축소가 없어 세로 화면에서 14자 문구가 잘린다.
        private bool DrawChoiceButton(Rect r, string text)
        {
            UITheme t = UITheme.Instance;
            Color body = t.surfaceRaised;
            if (r.Contains(UIScale.VirtualMousePosition)) body = Color.Lerp(body, Color.white, 0.16f);
            UISurface.Rounded(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), t.surfaceShadow);
            UISurface.Rounded(r, body);
            UIHelper.LabelFit(new Rect(r.x + 10f, r.y, r.width - 20f, r.height), text, choiceStyle);
            return GUI.Button(r, string.Empty, GUIStyle.none);
        }

        /// <summary>
        /// 「승부!」 — 산호색 몸통에 호박색 테두리, 테두리가 숨 쉬듯 진해졌다 옅어진다(줄인 움직임 설정이면 멈춘다).
        /// 선택 버튼과 같은 구조(서피스 + LabelFit + 투명 클릭)라 [다음 ▶]과 같은 자리·크기에 선다.
        /// 맥동은 색이 아니라 알파로 준다 — 둥근 서피스는 색마다 텍스처를 구워 캐시하므로 색을 매 프레임 바꾸면 텍스처가 쌓인다.
        /// </summary>
        private bool DrawDuelButton(Rect r, string text)
        {
            UITheme t = UITheme.Instance;
            float pulse = BattlePresentation.ReducedMotion ? 1f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            Color body = r.Contains(UIScale.VirtualMousePosition) ? Color.Lerp(t.accentCoral, Color.white, 0.16f) : t.accentCoral;
            UISurface.Rounded(new Rect(r.x + 2f, r.y + 5f, r.width, r.height), t.surfaceShadow);
            Color prev = GUI.color;
            GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * (0.55f + 0.45f * pulse));
            UISurface.Rounded(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), t.accentAmber);
            GUI.color = prev;
            UISurface.Rounded(r, body);
            UIHelper.LabelFit(new Rect(r.x + 10f, r.y, r.width - 20f, r.height), text, duelButtonStyle);
            return GUI.Button(r, string.Empty, GUIStyle.none);
        }

        /// <summary>
        /// 초상 한 장의 재료. 표(<see cref="GetStoryPortraitEntry"/>)는 성별·표정만 들고,
        /// 피부·머리색·머리 모양·몸통·모자는 전부 <b>월드 외형에서 받는다</b>(<see cref="ApplyWorldAppearance"/>).
        /// </summary>
        private static bool GetStoryPortrait(string id, out int gender, out Color skin, out Color hair,
            out int hairStyle, out int faceType, out Color top, out Color hat)
        {
            skin = hair = top = hat = default;
            hairStyle = 0;
            if (!GetStoryPortraitEntry(id, out gender, out faceType)) return false;
            ApplyWorldAppearance(id, out skin, out hair, out hairStyle, out top, out hat);
            return true;
        }

        /// <summary>
        /// 월드 외형(<c>NpcVisualBuilder.StoryNpcAppearance</c>) → 초상. 색을 표에 따로 적지 않는다.
        ///
        /// 따로 적었던 시절 두 표가 같은 번호를 <b>서로 다른 팔레트</b>로 읽었다(초상은 플레이어 팔레트
        /// <c>CharacterPalette</c>, 월드는 NPC 팔레트). 그래서 명부회 일곱 전원, 동행자 셋, 마을 주민 대부분이 필드와
        /// 달랐다 — 세라는 필드 검은 머리가 대사창 보라, 어르신·물결 할머니·너울은 필드 백발이 대사창 금발, 라온은
        /// 밝은 피부가 대사창 구릿빛이었다. 플레이어 팔레트엔 회색이 없어 번호로는 백발을 옮길 수도 없었다.
        ///
        /// 몸통은 <b>겉옷을 입었으면 겉옷 색</b>이다(플레이어 초상과 같은 관례 — <c>CharacterPortraitRenderer</c>의
        /// Outerwear 처리). 명부회 간부는 아이보리 코트, 하수는 남색 조끼라 대사창의 "검은 옷의 사내" 호칭과 초상의
        /// 짙은 몸통이 어긋나지 않는다. 머리 모양 번호는 두 체계가 다르다 — 월드 0 짧은·1 중간·2 올림 ↔
        /// 초상 0 짧은·1 중간·2 긴·3 올림.
        /// </summary>
        private static void ApplyWorldAppearance(string id, out Color skin, out Color hair, out int hairStyle,
            out Color top, out Color hat)
        {
            NpcAppearance a = NpcVisualBuilder.StoryNpcAppearance(id);
            skin = a.skin;
            hair = a.hair;
            hairStyle = a.hairStyle == 2 ? 3 : a.hairStyle;
            top = VisibleTorsoColor(a);
            hat = a.hasHat ? new Color(a.hat.r, a.hat.g, a.hat.b, 1f) : new Color(0f, 0f, 0f, 0f);
        }

        /// <summary>월드 모델에서 몸통으로 보이는 색 — 코트·조끼·도포는 겉옷 색, 그 밖엔 셔츠 색(앞치마는 앞판뿐이다).</summary>
        internal static Color VisibleTorsoColor(NpcAppearance a)
        {
            bool outer = a.wear == NpcWear.Coat || a.wear == NpcWear.Vest || a.wear == NpcWear.Robe;
            return outer ? a.wearColor : a.top;
        }

        // 초상 표 — 초상이 있는 인물과 그 성별·표정(월드 외형에 성별·표정이 없다). 색·머리는 ApplyWorldAppearance.
        // 표에 없으면 **얼굴 없이** 말한다 — 8~12장 대치의 간부들, 최종 보스인 관장까지 그랬다.
        private static bool GetStoryPortraitEntry(string id, out int gender, out int faceType)
        {
            switch (id)
            {
                case "catcher_rival": gender = 0; faceType = 1; return true;   // 라온 — 미소
                case "ruins_scholar": gender = 1; faceType = 0; return true;   // 세라
                case "village_elder": gender = 0; faceType = 0; return true;   // 마을 어르신

                // 명부회 일곱
                case "ledger_thug_cord": gender = 0; faceType = 1; return true;   // 끈 — 챙 깊은 모자로 얼굴을 가린다
                case "ledger_thug_pin": gender = 0; faceType = 0; return true;    // 핀 — 가장 어린 말단
                case "ledger_thug_rule": gender = 1; faceType = 1; return true;   // 자 — 모자 없이 묶은 머리
                case "ledger_grip": gender = 0; faceType = 1; return true;        // 집게 — 힘으로 밀어붙인다
                case "ledger_scale": gender = 1; faceType = 0; return true;       // 저울 — 숫자로 말한다
                case "ledger_ink": gender = 0; faceType = 0; return true;         // 먹 — 붓을 놓지 못한다
                case "ledger_chief": gender = 0; faceType = 0; return true;       // 관장 — 최종 보스

                // 마을 이야기 주민 12인
                case "town_meadow": gender = 1; faceType = 1; return true;     // 달래
                case "town_pond": gender = 1; faceType = 0; return true;       // 물결 할머니
                case "town_forest": gender = 0; faceType = 1; return true;     // 솔
                case "town_swamp": gender = 1; faceType = 0; return true;      // 이끼
                case "town_mountain": gender = 0; faceType = 0; return true;   // 너울
                case "town_garden": gender = 1; faceType = 1; return true;     // 누리
                case "town_ruins": gender = 0; faceType = 0; return true;      // 결
                case "town_hollow": gender = 1; faceType = 0; return true;     // 메아리
                case "town_dunes": gender = 0; faceType = 1; return true;      // 모래
                case "town_frostline": gender = 1; faceType = 0; return true;  // 서리
                case "town_emberfall": gender = 0; faceType = 0; return true;  // 숯
                case "town_canopy": gender = 1; faceType = 1; return true;     // 잎새

                default: gender = 0; faceType = 0; return false;
            }
        }

        private void EnsureStyles()
        {
            if (stylesInited) return;

            nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            nameStyle.normal.textColor = UITheme.Instance.accentAmber;

            lineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            lineStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            // 선택 버튼은 UISurface.Button 위에 그린다 — 그쪽은 GUI.Label로 라벨만 찍으므로 **label 파생**이어야
            // 한다. button 파생을 넘기면 유니티 기본 회색 상자가 둥근 서피스 위에 겹친다(TutorialQuestUI가 겪은 그것).
            choiceStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            choiceStyle.normal.textColor = Color.white;

            // 「승부!」 — 대사 버튼(72px)에 꽉 차게 크게. label 파생(서피스 위에 글자만 찍는다).
            duelButtonStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            duelButtonStyle.normal.textColor = UITheme.Instance.textPrimary;

            // 무대 이름표 — 상자 윗변에 걸친 카드 안 가운데.
            namePlateStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            namePlateStyle.normal.textColor = UITheme.Instance.accentAmber;

            // 진행 표시·계속 표시(▼) — 작고 흐리게.
            progressStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                alignment = TextAnchor.MiddleLeft
            };
            progressStyle.normal.textColor = UITheme.Instance.textSecondary;

            // 「지난 이야기」 카드 — 아이가 읽는 화면이라 본문을 대사 본문(가로 34·세로 36) 이상으로 둔다.
            recapTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = StoryRecapLayout.TitleFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            recapTitleStyle.normal.textColor = UITheme.Instance.accentAmber;
            recapSectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = StoryRecapLayout.SectionFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            recapSectionStyle.normal.textColor = UITheme.Instance.textSecondary;
            recapBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = StoryRecapLayout.BodyFont,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            recapBodyStyle.normal.textColor = UITheme.Instance.textPrimary;
            recapGoalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = StoryRecapLayout.BodyFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            recapGoalStyle.normal.textColor = UITheme.Instance.textPrimary;
            recapHintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            recapHintStyle.normal.textColor = UITheme.Instance.textSecondary;

            stylesInited = true;
        }
    }

    /// <summary>
    /// 스토리 대사 버튼의 문구 — <b>순수 계산</b>. 대사가 끝나면 곧바로 대결이 열리는 장면(<c>StoryBeat.duelAfter</c>, 또는
    /// 선택지 결과 대사처럼 런처가 이미 대결을 기다리는 장면)이면 마지막 줄의 [닫기]가 「승부!」가 되고, [건너뛰기]는
    /// 「건너뛰고 승부」가 된다 — 아이가 "닫으면 끝"이라고 읽고 지나가지 않게. 누르는 일은 그대로 닫기다(대결은 런처가 연다).
    /// </summary>
    internal static class DialogueDuelPrompt
    {
        public const string DuelLabel = "승부!";
        public const string SkipToDuelLabel = "건너뛰고 승부";
        public const string SkipLabel = "건너뛰기";
        public const string CloseLabel = "닫기";
        public const string NextLabel = "다음 ▶";

        /// <summary>
        /// 이 대사가 끝나면 대결이 열리는가. 비트의 <paramref name="duelAfter"/>가 대결 표에 있는 상대이거나
        /// (<c>StoryDuelLauncher.KindOf</c> — 오타는 런처도 버린다), 런처가 이미 대결을 기다리면 참. 다시보기(저널)와 꿈은 대결을 열지 않는다.
        /// </summary>
        public static bool LeadsToDuel(string duelAfter, bool launcherPending, bool replay, bool dream)
        {
            if (replay || dream) return false;
            if (!string.IsNullOrWhiteSpace(duelAfter)
                && InsectGame.Story.StoryDuelLauncher.KindOf(duelAfter.Trim()) != InsectGame.Story.StoryDuelLauncher.DuelKind.None)
                return true;
            return launcherPending;
        }

        /// <summary>오른쪽 버튼이 「승부!」인가 — 마지막 줄이 다 나왔고 대결이 이어질 때만. 덜 나왔으면 [다음 ▶](줄을 마저 보인다).</summary>
        public static bool IsDuelButton(bool revealed, bool isLast, bool duel) => revealed && isLast && duel;

        /// <summary>오른쪽 버튼 문구.</summary>
        public static string AdvanceLabel(bool revealed, bool isLast, bool duel) =>
            revealed && isLast ? (duel ? DuelLabel : CloseLabel) : NextLabel;

        /// <summary>왼쪽 [건너뛰기] 문구.</summary>
        public static string SkipButtonLabel(bool duel) => duel ? SkipToDuelLabel : SkipLabel;
    }

    /// <summary>
    /// 「지난 이야기」 카드의 자리 — <b>순수 계산</b>. 장을 여는 비트(<c>StoryChapter.openingBeatId</c>)가 처음 뜰 때 첫 대사 앞에 한 장 선다
    /// (<see cref="NpcDialogueUI"/>). 대사 상자(<see cref="DialogueBox"/> — 대사 무대가 같은 함수를 쓴다) 위, 안전 영역 윗변까지의 판
    /// (<see cref="Area"/>) 가운데에 선다.
    ///
    /// <b>가로</b>(가상 1920×1080): 상자 1400×350이 바닥에, 판은 그 위 24px까지(높이 약 640), 카드 폭 min(1200, 안전 폭).
    /// <b>세로</b>(가상 1080×1920): 상자 안전 폭×500, 판 높이 약 1280, 카드 폭 = 안전 폭(1032).
    /// 높이는 줄마다 잰 높이(래핑 — 세로에선 30자 한 줄이 두 줄로 접힌다)로 쌓고, 판보다 크면 본문 줄만 같은 비율로 줄인다
    /// (그리기는 <c>UIHelper.LabelFit</c>이 글자를 줄여 맞춘다) — 줄 수가 늘어도 판 밖으로 나가지 않는다.
    /// </summary>
    public static class StoryRecapLayout
    {
        /// <summary>장 제목.</summary>
        public const int TitleFont = 48;
        /// <summary>「지난 이야기」·「이번 목표」 소제목.</summary>
        public const int SectionFont = 28;
        /// <summary>지난 이야기 줄·목표 — 대사 본문(가로 34·세로 36) 이상.</summary>
        public const int BodyFont = 36;
        public const float PadX = 40f;
        public const float PadY = 30f;
        /// <summary>가로 화면 카드 최대 폭 — 대사 상자(1400)보다 좁혀 한 줄을 눈으로 따라가기 쉽게.</summary>
        public const float LandscapeMaxWidth = 1200f;
        public const float DividerHeight = 2f;
        /// <summary>카드가 뜬 뒤 입력을 받지 않는 시간(초) — 걷던 손가락이 카드를 바로 넘기지 않게.</summary>
        public const float InputDelay = 0.4f;

        // 대사 상자 — 무대(NpcDialogueUI.DrawStoryStage)와 카드가 같은 값을 쓴다.
        public const float LandscapeBoxMaxWidth = 1400f;
        public const float LandscapeBoxHeight = 350f;
        public const float PortraitBoxHeight = 500f;

        /// <summary>한 줄이 안 잘리는 상자 높이 — 한글 줄높이 ≈ 글자 × 1.35(rules/ui-layout.md).</summary>
        public static float LineHeight(int fontSize) => Mathf.Ceil(fontSize * 1.35f);

        /// <summary>
        /// 카드를 띄우는가 — 다시보기(저널)가 아니고, 장 데이터가 있고, 지난 이야기에 비지 않은 줄이 하나라도 있을 때.
        /// 1장처럼 지난 이야기가 없는 장은 카드 없이 곧바로 대사다.
        /// </summary>
        public static bool ShouldShow(InsectGame.Story.StoryChapter chapter, bool replay)
        {
            if (replay || chapter == null || chapter.recap == null) return false;
            for (int i = 0; i < chapter.recap.Count; i++)
                if (!string.IsNullOrWhiteSpace(chapter.recap[i])) return true;
            return false;
        }

        /// <summary>스토리 대사 상자(가상 좌표) — 가로 min(1400, 안전 폭)×350, 세로 안전 폭×500, 안전 영역 바닥.</summary>
        public static Rect DialogueBox(HudFrame f)
        {
            float w = f.Portrait ? f.ContentWidth : Mathf.Min(LandscapeBoxMaxWidth, f.ContentWidth);
            return f.BottomPanel(w, f.Portrait ? PortraitBoxHeight : LandscapeBoxHeight);
        }

        /// <summary>카드가 설 판 — 안전 영역 윗변부터 대사 상자 위 24px까지, 안전 폭 전부.</summary>
        public static Rect Area(HudFrame f)
        {
            Rect box = DialogueBox(f);
            float top = f.ContentTop;
            float bottom = Mathf.Max(top + 1f, box.y - UITheme.Space.L);
            return new Rect(f.ContentLeft, top, f.ContentWidth, bottom - top);
        }

        public static float CardWidth(HudFrame f) => f.Portrait ? f.ContentWidth : Mathf.Min(LandscapeMaxWidth, f.ContentWidth);

        /// <summary>카드 안 글자 폭 — 줄 높이를 이 폭으로 잰다.</summary>
        public static float TextWidth(float cardWidth) => Mathf.Max(1f, cardWidth - PadX * 2f);

        /// <summary>잰 높이(CalcHeight) → 본문 줄 상자 높이. 한 줄 높이(1.35배)보다 낮게 잡지 않는다.</summary>
        public static float BodyHeight(float measured) => Mathf.Max(LineHeight(BodyFont), Mathf.Ceil(measured));

        /// <summary>카드 한 장의 배치.</summary>
        public struct Plan
        {
            public Rect Card;
            public Rect Title;
            public Rect RecapHeader;
            /// <summary>지난 이야기 줄마다 하나.</summary>
            public Rect[] Recap;
            public Rect Divider;
            public Rect GoalHeader;
            public Rect Goal;
            public bool HasGoal;
            /// <summary>판보다 커서 본문 줄을 줄였다(그리기의 LabelFit이 글자를 줄여 맞춘다).</summary>
            public bool Squeezed;
        }

        /// <summary>본문 줄이 아닌 것(여백·제목·소제목·간격·구분선)의 높이.</summary>
        public static float FixedHeight(int recapCount, bool hasGoal)
        {
            float h = PadY + LineHeight(TitleFont) + UITheme.Space.S;
            if (recapCount > 0)
                h += LineHeight(SectionFont) + UITheme.Space.XS + UITheme.Space.XS * (recapCount - 1);
            if (hasGoal)
            {
                if (recapCount > 0) h += UITheme.Space.M + DividerHeight + UITheme.Space.M;
                h += LineHeight(SectionFont) + UITheme.Space.XS;
            }
            return h + PadY;
        }

        /// <summary>
        /// 카드 배치 — <paramref name="area"/>(<see cref="Area"/>) 가운데에 <paramref name="cardWidth"/> 폭으로. 본문 줄 높이는 호출부가 잰 값
        /// (<see cref="BodyHeight"/>)이고, <paramref name="goalHeight"/>가 0이면 목표 칸이 없다. 다 쌓은 높이가 판보다 크면 본문 줄만
        /// 같은 비율로 줄여 카드가 판 안에 든다.
        /// </summary>
        public static Plan Layout(Rect area, float cardWidth, IReadOnlyList<float> recapHeights, float goalHeight)
        {
            int n = recapHeights != null ? recapHeights.Count : 0;
            bool hasGoal = goalHeight > 0f;
            float fixedH = FixedHeight(n, hasGoal);
            float body = hasGoal ? goalHeight : 0f;
            for (int i = 0; i < n; i++) body += Mathf.Max(0f, recapHeights[i]);

            float room = Mathf.Max(1f, area.height);
            float k = 1f;
            if (fixedH + body > room && body > 0f) k = Mathf.Clamp01((room - fixedH) / body);
            float cardH = Mathf.Min(room, fixedH + body * k);
            float w = Mathf.Clamp(cardWidth, 1f, Mathf.Max(1f, area.width));

            var p = new Plan { HasGoal = hasGoal, Squeezed = k < 1f, Recap = new Rect[n] };
            p.Card = new Rect(area.x + (area.width - w) * 0.5f, area.y + (room - cardH) * 0.5f, w, cardH);
            float x = p.Card.x + PadX;
            float tw = TextWidth(w);
            float y = p.Card.y + PadY;
            p.Title = new Rect(x, y, tw, LineHeight(TitleFont));
            y += p.Title.height + UITheme.Space.S;
            if (n > 0)
            {
                p.RecapHeader = new Rect(x, y, tw, LineHeight(SectionFont));
                y += p.RecapHeader.height + UITheme.Space.XS;
                for (int i = 0; i < n; i++)
                {
                    float h = Mathf.Max(1f, Mathf.Max(0f, recapHeights[i]) * k);
                    p.Recap[i] = new Rect(x, y, tw, h);
                    y += h + (i < n - 1 ? UITheme.Space.XS : 0f);
                }
            }
            if (hasGoal)
            {
                if (n > 0)
                {
                    y += UITheme.Space.M;
                    p.Divider = new Rect(x, y, tw, DividerHeight);
                    y += DividerHeight + UITheme.Space.M;
                }
                p.GoalHeader = new Rect(x, y, tw, LineHeight(SectionFont));
                y += p.GoalHeader.height + UITheme.Space.XS;
                p.Goal = new Rect(x, y, tw, Mathf.Max(1f, goalHeight * k));
            }
            return p;
        }
    }
}
