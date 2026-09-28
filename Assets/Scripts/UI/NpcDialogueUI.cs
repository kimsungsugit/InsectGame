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
        public void ShowStory(InsectGame.Story.StoryBeat beat)
        {
            if (beat == null) return;

            // 대사 없음 — 표시 없이 즉시 완료(보상/seen 처리).
            if (beat.lines == null || beat.lines.Count == 0)
            {
                if (storyDirector != null) storyDirector.CompleteBeat(beat.beatId);
                return;
            }

            // 다른 모달(주민 대화)이 열려 있으면 정리 후 스토리로 전환.
            if (isOpen) CloseModal();

            currentBeat = beat;
            storyMode = true;
            storyReplay = false;
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

            ShowStory(beat);
            storyReplay = true;   // ShowStory가 false로 돌려놓은 뒤에 켠다
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
            if (storyMode) DrawStoryStage(panelAlpha);
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
                UIHelper.LabelFit(new Rect(textX, py + panelH - 108f, textW, 32f),
                    "다음 이야기 · " + objectiveTracker.Label, lineStyle);
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
            bool portraitLayout = UIScale.IsPortrait;
            float boxW = portraitLayout ? UISafeLayout.ContentWidth : Mathf.Min(1400f, UISafeLayout.ContentWidth);
            float boxH = portraitLayout ? 500f : 350f;
            Rect box = UISafeLayout.BottomPanel(boxW, boxH);
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
                string advanceLabel = revealed && isLast ? "닫기" : "다음 ▶";
                if (GUI.Button(new Rect(box.xMax - btnW - 24f, btnY, btnW, btnH), advanceLabel, buttonStyle))
                {
                    AdvanceStory();
                    if (!isOpen) return;
                }
                // 건너뛰기 — 장면 전체를 닫는다(비트는 완료 처리된다). 마지막 줄에선 위 버튼이 그 일을 한다.
                // 선택지가 있는 비트는 건너뛸 수 없다 — CloseModal이 선택 전 닫기를 삼키므로 누르면 아무 일도 없다.
                if (!isLast && !HasChoices
                    && GUI.Button(new Rect(box.xMax - btnW * 2f - 40f, btnY, btnW, btnH), "건너뛰기", buttonStyle))
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
        /// 스토리 인물 전신 초상. 전투 컷인·말풍선도 이걸 쓴다 — 한 인물이 화면마다 다르게 생기면 안 된다.
        /// 기준은 대사창 무대와 같다: (cx, cy)가 몸 가운데, 발끝이 cy + 74.5 × scale. 표에 없으면 false.
        /// </summary>
        internal static bool TryDrawStoryPortrait(string id, float cx, float cy, float scale)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (!GetStoryPortrait(id, out int gender, out int skinIdx, out int hairIdx,
                    out int hairStyle, out int faceType, out Color top, out Color hat))
                return false;
            CharacterPortraitRenderer.Draw(cx, cy, scale, gender, skinIdx, hairIdx, hairStyle, faceType,
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

        private static bool GetStoryPortrait(string id, out int gender, out int skinIdx, out int hairIdx,
            out int hairStyle, out int faceType, out Color top, out Color hat)
        {
            switch (id)
            {
                case "catcher_rival": // 라온 — 밝은 주황 상의, 캡, 미소
                    gender = 0; skinIdx = 2; hairIdx = 1; hairStyle = 0; faceType = 1;
                    top = new Color(1f, 0.55f, 0.3f); hat = new Color(1f, 0.65f, 0.2f); return true;
                case "ruins_scholar": // 세라 — 보라 상의, 올림머리, 모자 없음
                    gender = 1; skinIdx = 0; hairIdx = 4; hairStyle = 3; faceType = 0;
                    top = new Color(0.6f, 0.45f, 0.7f); hat = new Color(0f, 0f, 0f, 0f); return true;
                case "village_elder": // 마을 어르신 — 따뜻한 상의, 모자, 밝은 머리
                    gender = 0; skinIdx = 1; hairIdx = 2; hairStyle = 0; faceType = 0;
                    top = new Color(0.85f, 0.7f, 0.4f); hat = new Color(0.55f, 0.35f, 0.25f); return true;
                // 1막 하수 2인 — 검은 상의로 통일한다. 이름 대신 그 색이 이들의 정체다.
                // 2막 간부와 같은 계열이라 나중에 "그때 그 옷"으로 회수된다.
                case "ledger_thug_cord": // 끈 — 챙 깊은 모자로 얼굴을 가린다
                    gender = 0; skinIdx = 2; hairIdx = 0; hairStyle = 0; faceType = 1;
                    top = new Color(0.16f, 0.16f, 0.20f); hat = new Color(0.10f, 0.10f, 0.13f); return true;
                case "ledger_thug_pin": // 핀 — 짧은 앞머리와 검은 모자
                    gender = 0; skinIdx = 0; hairIdx = 1; hairStyle = 1; faceType = 0;
                    top = new Color(0.16f, 0.16f, 0.20f); hat = new Color(0.10f, 0.10f, 0.13f); return true;
                case "ledger_thug_rule": // 자 — 모자 없이 묶은 머리
                    gender = 1; skinIdx = 1; hairIdx = 3; hairStyle = 3; faceType = 1;
                    top = new Color(0.16f, 0.16f, 0.20f); hat = new Color(0f, 0f, 0f, 0f); return true;

                // 2막 간부 4인. 표에 없으면 **얼굴 없이** 말한다 — 1막 하수에게는 초상이 있는데
                // 8~12장 대치의 상대들, 심지어 최종 보스인 관장이 그랬다. 하수의 검은 상의를
                // 그대로 이어받되(같은 계열임이 색으로 읽힌다) 채도를 조금씩 달리해 구분한다.
                case "ledger_grip": // 집게 — 힘으로 밀어붙인다. 짧은 머리, 모자 없음
                    gender = 0; skinIdx = 3; hairIdx = 0; hairStyle = 0; faceType = 1;
                    top = new Color(0.18f, 0.17f, 0.21f); hat = new Color(0f, 0f, 0f, 0f); return true;
                case "ledger_scale": // 저울 — 숫자로 말한다. 단정한 올림머리
                    gender = 1; skinIdx = 0; hairIdx = 1; hairStyle = 3; faceType = 0;
                    top = new Color(0.20f, 0.20f, 0.26f); hat = new Color(0f, 0f, 0f, 0f); return true;
                case "ledger_ink": // 먹 — 붓을 놓지 못한다. 챙 있는 모자로 눈을 가린다
                    gender = 0; skinIdx = 1; hairIdx = 4; hairStyle = 0; faceType = 0;
                    top = new Color(0.14f, 0.14f, 0.18f); hat = new Color(0.09f, 0.09f, 0.12f); return true;
                case "ledger_chief": // 관장 — 최종 보스. 유일하게 밝은 머리로 격을 가른다
                    gender = 0; skinIdx = 2; hairIdx = 2; hairStyle = 0; faceType = 0;
                    top = new Color(0.12f, 0.12f, 0.16f); hat = new Color(0.30f, 0.26f, 0.18f); return true;

                default:
                    gender = 0; skinIdx = 0; hairIdx = 0; hairStyle = 0; faceType = 0;
                    top = Color.white; hat = new Color(0f, 0f, 0f, 0f); return false;
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

            stylesInited = true;
        }
    }
}
