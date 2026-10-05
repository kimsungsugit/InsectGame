using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>
    /// <see cref="StoryDirector"/>가 뽑은 "다음 목표"를 <b>월드 좌표와 이름</b>으로 풀어 주고,
    /// 자동 주행을 켜고 끈다. UI는 여기만 읽으면 되므로 HUD가 NpcManager·RegionManager를
    /// 직접 들고 있지 않아도 된다(UI → Core 방향 유지).
    ///
    /// 갱신은 주기적이다 — NPC는 스폰·컬링으로 오갈 수 있어 매번 다시 찾아야 하지만,
    /// HUD가 묻는 매 프레임마다 전체 목록을 훑을 필요는 없다.
    /// </summary>
    public class StoryObjectiveTracker : MonoBehaviour
    {
        private const float RefreshInterval = 0.5f;
        /// <summary>도착 여유 — 대화 사거리보다 살짝 안쪽에서 멈춰 확실히 말이 걸리게 한다.</summary>
        private const float TalkArriveMargin = 0.6f;
        private const float StatusMessageSeconds = 4f;

        private StoryDirector storyDirector;
        private NpcManager npcManager;
        private RegionManager regionManager;
        private PlayerMovement playerMovement;
        private Transform playerTransform;

        // ── 라벨을 구체화하는 참조. 전부 옵션이다 ──
        // 없으면 문구에서 그 조각만 빠질 뿐 목표 자체는 그대로 나온다.
        // 안내가 통째로 사라지는 것보다 덜 구체적인 편이 낫다.
        private PlayerProgressController progressController;
        private InsectGame.Dex.DexController dexController;
        private TutorialQuestManager questManager;
        private InsectDatabase insectDatabase;

        private float refreshTimer;
        private bool hasObjective;
        private StoryObjective objective;
        private string label = string.Empty;
        // 목표 행 두 번째 줄의 이유 — 목표 비트의 why. 수문장으로 바꿔 쳤거나 의뢰를 따라가면 빈 문자열.
        private string why = string.Empty;
        // 이번 Refresh에서 TryRedirectLockedRegion이 목표를 앞 리전 수문장으로 바꿔 쳤는가.
        private bool redirectedToGatekeeper;
        private bool hasWorldTarget;
        private Vector3 targetPosition;
        private string targetRegionId = string.Empty;
        // TalkToNpc 목표가 고른 개체. Refresh가 매번 다시 고르므로 스폰/컬링으로 오가도 최신이다.
        private VillagerNpc targetNpc;

        private string statusMessage = string.Empty;
        private float statusTimer;

        // ── 마을 이야기 따라가기 ──
        // 비어 있으면 본편 목표를 안내한다. 채워져 있으면 그 주민의 이야기가 목표 행·미니맵 쐐기·
        // 지도 마커·원터치 이동을 전부 가져간다(사용자 결정: 의뢰가 본편을 대신한다, 2026-09-28).
        private string trackedTaleNpcId = string.Empty;
        // 불러온 계정 스코프 키 — 로그인·계정 전환으로 키가 바뀌면 다시 읽는다.
        private string trackedTalePrefsKey;
        // 이번 Refresh의 목표가 따라가기에서 왔는가, 그리고 그게 "말 걸기"인가(자동 주행 도착 반경).
        private bool trackedObjective;
        private bool trackedTalkTarget;
        private readonly List<TaleMarker> taleMarkers = new List<TaleMarker>();

        /// <summary>지도·미니맵이 그릴 의뢰 주민 표식 한 건.</summary>
        public readonly struct TaleMarker
        {
            public readonly VillagerNpc Npc;
            public readonly QuestMark Mark;
            public TaleMarker(VillagerNpc npc, QuestMark mark) { Npc = npc; Mark = mark; }
        }

        // ── 읽기 전용 표면 (HUD가 소비) ──

        /// <summary>
        /// 지금 말을 걸면 이야기가 나오는 마을 주민(<c>!</c> 새 이야기 / <c>?</c> 의뢰 보고).
        /// 0.5초마다 다시 만든다 — 지도·미니맵은 그리기만 한다.
        /// </summary>
        public IReadOnlyList<TaleMarker> TaleMarkers => taleMarkers;

        /// <summary>
        /// 이 주민의 표식(없으면 None). 지도·미니맵이 같은 탐색을 각자 들고 있던 것을 여기로 모았다 —
        /// 목록이 주민 수(12명) 안팎이라 선형 탐색이다. 판정은 <see cref="RefreshTaleMarkers"/>가 0.5초마다 만든다.
        /// </summary>
        public QuestMark TaleMarkOf(VillagerNpc npc)
        {
            if (npc == null) return QuestMark.None;
            for (int i = 0; i < taleMarkers.Count; i++)
                if (taleMarkers[i].Npc == npc) return taleMarkers[i].Mark;
            return QuestMark.None;
        }

        // ── 본편 표식 ──
        // 본편이 "이 사람에게 말 걸기"를 가리킬 때 그 개체 <b>하나</b>에만 단다. 동행자는 리전마다 서 있어서
        // storyNpcId로 달면 같은 사람의 표식이 열 개씩 뜬다(2026-09-28 결정) — 그래서 목표 행이 고르는 것과
        // 같은 한 개체(현재 리전 우선 → 최근접)에만 붙인다. 의뢰를 따라가는 중에도 남는다 — 목표 행이
        // 의뢰로 넘어가도 "본편은 저기서 이어진다"가 머리 위·지도에 보여야 돌아올 수 있다.
        private VillagerNpc mainMarkNpc;

        /// <summary>본편이 지금 말을 걸라고 가리키는 개체. 본편 목표가 대화가 아니면 null.</summary>
        public VillagerNpc MainMarkNpc => mainMarkNpc;

        /// <summary>
        /// 이 주민에게 그릴 표식 — 마을 의뢰(<c>!</c>·<c>?</c>)가 먼저고, 없으면 본편 대상인지 본다.
        /// 미니맵처럼 "무슨 표식이든 그리기만" 하는 쪽이 쓴다. 눌러서 따라가는 쪽은 <see cref="TaleMarkOf"/>.
        /// </summary>
        public QuestMark QuestMarkOf(VillagerNpc npc)
        {
            QuestMark tale = TaleMarkOf(npc);
            if (tale != QuestMark.None) return tale;
            return npc != null && npc == mainMarkNpc ? QuestMark.Main : QuestMark.None;
        }

        /// <summary>지금 목표 행이 의뢰 따라가기인가(HUD가 ✕ 해제 버튼을 붙인다).</summary>
        public bool IsTrackingTale => trackedObjective;

        public bool HasObjective => hasObjective;
        /// <summary>"세라에게 말 걸기" 같은 한 줄. 목표가 없으면 빈 문자열.</summary>
        public string Label => label;
        /// <summary>
        /// 목표 아래에 붙는 짧은 이유 한 줄("상자에 갇힌 곤충이 있대") — 목표 비트의 <see cref="StoryBeat.why"/>.
        /// <b>null이 아니라 빈 문자열</b>이 기본이다. 비는 경우: 목표가 없음 · 비트에 이유가 없음 ·
        /// 잠긴 리전이라 앞 리전 수문장으로 바꿔 쳤음(이유가 그 비트 것이라 수문장 안내에 붙으면 엉뚱하다) ·
        /// 마을 의뢰를 따라가는 중(목표 행이 본편 비트가 아니다). <see cref="Label"/>과 같은 Refresh에서 갱신되고,
        /// 값이 같으면 참조를 유지한다(HUD의 ReferenceEquals 캐시).
        /// </summary>
        public string Why => why;
        /// <summary>
        /// 지금 목표가 가리키는 스토리 NPC 개체. 목표가 <c>TalkToNpc</c>가 아니거나 그 NPC가
        /// 월드에 없으면 null. <see cref="StoryStageDirector"/>의 조우 접근이 읽는다 —
        /// 개체 선택 규칙(현재 리전 우선 → 최근접)을 저쪽에 복제하지 않기 위해서다.
        /// </summary>
        public VillagerNpc TargetNpc => targetNpc;
        /// <summary>갈 곳이 정해진 목표인가 — false면 자동 주행 버튼을 띄우지 않는다.</summary>
        public bool HasWorldTarget => hasWorldTarget;
        public Vector3 TargetPosition => targetPosition;
        /// <summary>목표가 있는 리전 ID(없으면 빈 문자열). 지도가 마커를 누르면 그 리전을 고른다.</summary>
        public string TargetRegionId => targetRegionId;
        public bool IsRunning => playerMovement != null && playerMovement.IsAutoRunning;
        /// <summary>
        /// 걸어서 갈 수 있는 목표인가 — 아니면 지도(텔레포트) 경로로 보낸다.
        ///
        /// <b>리전 밖에 서 있을 수 있다.</b> <c>RegionManager</c>는 플레이어 위치가 어느 리전
        /// 원 안에도 없으면 <c>CurrentRegion</c>을 null로 둔다 — 리전 사이 빈 땅을 지나는
        /// 동안이 그렇다. 그 상태를 "다른 리전"으로 읽으면 <b>바로 앞 목표를 눌러도 지도가
        /// 열린다</b>(걸어가면 되는데). 모르는 것이지 다른 것이 아니다.
        ///
        /// 그때는 목표 리전의 <b>해금 여부</b>로 가른다 — 잠긴 곳은 어차피 걸어 들어갈 수
        /// 없으므로 지도로 보내고, 열린 곳이면 걸어가게 둔다.
        /// </summary>
        public bool TargetInCurrentRegion
        {
            get
            {
                if (regionManager == null || string.IsNullOrEmpty(targetRegionId)) return false;

                RegionData current = regionManager.CurrentRegion;
                if (current != null) return targetRegionId == current.regionId;

                RegionData target = regionManager.GetRegionById(targetRegionId);
                return target != null && regionManager.IsRegionAccessible(target);
            }
        }

        /// <summary>수평 거리(m). 목표가 없으면 0.</summary>
        public float DistanceToTarget
        {
            get
            {
                if (!hasWorldTarget || playerTransform == null) return 0f;
                Vector3 d = targetPosition - playerTransform.position;
                d.y = 0f;
                return d.magnitude;
            }
        }

        /// <summary>목표 방향(수평 단위벡터). 미니맵 쐐기가 쓴다.</summary>
        public Vector3 DirectionToTarget
        {
            get
            {
                if (!hasWorldTarget || playerTransform == null) return Vector3.zero;
                Vector3 d = targetPosition - playerTransform.position;
                d.y = 0f;
                return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.zero;
            }
        }

        /// <summary>일시 안내 문구(주행 실패·지역 잠김). 없으면 빈 문자열.</summary>
        public string StatusMessage => statusTimer > 0f ? statusMessage : string.Empty;

        /// <summary>
        /// 목표가 <b>다른 리전</b>이라 지금 걸어갈 수 없을 때 <see cref="Toggle"/>이 쏜다.
        /// 인자는 목표 리전 ID — UI가 지도를 그 리전 선택 상태로 연다.
        ///
        /// 구독자가 없으면 예전처럼 안내 문구로 폴백한다. 이동 자체를 여기서 하지 않는 것은
        /// 해금·수문장 판정이 지도 클릭 경로에 있기 때문이다(우회 위험).
        /// </summary>
        public event System.Action<string> MapRequested;

        public void AutoWire(StoryDirector director, NpcManager npcs, RegionManager region,
            PlayerMovement movement, Transform player)
        {
            if (storyDirector == null) storyDirector = director;
            if (npcManager == null) npcManager = npcs;
            if (regionManager == null) regionManager = region;
            if (playerMovement == null) playerMovement = movement;
            if (playerTransform == null) playerTransform = player;
            SubscribeEvents();
        }

        /// <summary>
        /// 라벨 구체화용 참조. <b>목표 도출과는 무관하다</b> — 이게 없어도 목표는 나온다.
        /// 곤충 표시명·리전명·퀘스트 제목·현재 레벨/도감 종수를 여기서 얻는다.
        /// </summary>
        public void AutoWire(PlayerProgressController prog, InsectGame.Dex.DexController dex,
            TutorialQuestManager quests, InsectDatabase database)
        {
            if (progressController == null) progressController = prog;
            if (dexController == null) dexController = dex;
            if (questManager == null) questManager = quests;
            if (insectDatabase == null) insectDatabase = database;
        }

        // 구독을 메서드로 뺀 것은 OnEnable에서 되살리기 위해서다 — OpeningReplayCoordinator가
        // UI 루트를 껐다 켜는 경로에서 AutoWire는 다시 불리지 않는다(rules/ui-layout.md).
        private void SubscribeEvents()
        {
            if (playerMovement == null) return;
            playerMovement.AutoRunFailed -= OnAutoRunFailed;
            playerMovement.AutoRunFailed += OnAutoRunFailed;
        }

        private void OnEnable() => SubscribeEvents();

        private void OnDisable()
        {
            if (playerMovement != null) playerMovement.AutoRunFailed -= OnAutoRunFailed;
        }

        private void OnAutoRunFailed() => ShowStatus("길이 막혀 자동 이동을 멈췄습니다");

        private void Update()
        {
            if (statusTimer > 0f) statusTimer -= Time.deltaTime;

            refreshTimer -= Time.deltaTime;
            if (refreshTimer > 0f) return;
            refreshTimer = RefreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            EnsureTrackedTaleLoaded();
            RefreshQuestMarks();
            // 의뢰 따라가기는 본편 비트가 아니라 이유가 없다 — 본편 목표의 이유가 [의뢰] 줄 밑에 남지 않게 비운다.
            if (TryResolveTrackedTale()) { SetWhy(string.Empty); return; }   // false면 trackedObjective도 이미 내려가 있다

            hasObjective = storyDirector != null && storyDirector.TryGetCurrentObjective(out objective);
            if (!hasObjective)
            {
                label = string.Empty;
                SetWhy(string.Empty);
                hasWorldTarget = false;
                targetRegionId = string.Empty;
                targetNpc = null;
                return;
            }

            // 아래 switch에서 ResolveNpcTarget만 다시 채운다 — 목표 종류가 바뀌면 자동으로 비워진다.
            targetNpc = null;
            // 아래 해석 중 TryRedirectLockedRegion이 세운다 — 이유를 붙일지가 여기서 갈린다.
            redirectedToGatekeeper = false;

            switch (objective.Kind)
            {
                case StoryObjectiveKind.TalkToNpc: ResolveNpcTarget(); break;
                case StoryObjectiveKind.EnterRegion:
                case StoryObjectiveKind.DefeatGuardian: ResolveRegionTarget(); break;
                case StoryObjectiveKind.EnterSubArea: ResolveSubAreaTarget(); break;
                case StoryObjectiveKind.ActInRegion: ResolveActInRegion(); break;
                default: ResolveFreeform(); break;
            }

            StoryService.TryGetBeat(objective.BeatId, out StoryBeat objectiveBeat);
            SetWhy(StoryObjectiveResolver.WhyFor(objectiveBeat, redirectedToGatekeeper));

            TryAutoStartFirstObjective();
        }

        // ------------------------------------------------------------------
        // 마을 이야기 — 따라가기 + 표식
        // ------------------------------------------------------------------

        /// <summary>
        /// 지금 따라갈 거리가 있는가 — 그 의뢰 주민의 이야기가 말 걸기·보고·의뢰 진행 중 하나다.
        /// 리전에 아직 못 갔거나(만남 전 단계) 본편을 기다리는 중이면 버튼을 띄우지 않는다 —
        /// 눌러 봐야 "본편이 더 진행되면…"만 뜬다. 의뢰 단계인데 의뢰가 아직 잠겼으면(선행 퀘스트
        /// 미완료) 목록 행이 🔒인데 버튼만 뜨는 셈이라 띄우지 않는다.
        /// </summary>
        public bool CanTrackQuestNow(string questId)
        {
            if (storyDirector == null) return false;
            string npcId = storyDirector.FindTaleNpcForQuest(questId);
            if (string.IsNullOrEmpty(npcId)) return false;
            TaleStepKind step = storyDirector.GetTaleStep(npcId, out string errandQuestId);
            if (step == TaleStepKind.Errand) return !IsErrandLocked(errandQuestId);
            return step == TaleStepKind.Talk || step == TaleStepKind.Report;
        }

        // 의뢰가 아직 안 세어지는가 — 완료 전인데 선행 퀘스트·리전 해금이 안 됐다(목록의 🔒와 같은 판정).
        private bool IsErrandLocked(string questId)
        {
            if (questManager == null) return false;
            TutorialQuest quest = questManager.FindQuest(questId);
            return quest != null && !questManager.IsQuestCompleted(quest.questId) && !questManager.IsSideUnlocked(quest);
        }

        public bool IsQuestTracked(string questId)
        {
            if (string.IsNullOrEmpty(trackedTaleNpcId) || storyDirector == null) return false;
            return storyDirector.FindTaleNpcForQuest(questId) == trackedTaleNpcId;
        }

        public void TrackQuest(string questId)
        {
            if (storyDirector == null) return;
            TrackTale(storyDirector.FindTaleNpcForQuest(questId));
        }

        public bool IsTaleTracked(string npcId)
            => !string.IsNullOrEmpty(npcId) && npcId == trackedTaleNpcId;

        /// <summary>
        /// 이 주민의 이야기를 따라간다. 지금 할 일이 없으면(본편을 기다리거나 다 들었으면)
        /// 따라가지 않고 이유만 알린다 — 목표 행이 빈 안내를 붙들고 있지 않게.
        /// </summary>
        public void TrackTale(string npcId)
        {
            if (string.IsNullOrEmpty(npcId) || storyDirector == null) return;
            EnsureTrackedTaleLoaded();
            // 본편 목표로 달리던 중이면 멈춘다 — 목표가 바뀌었는데 옛 목적지로 계속 가면 안 된다.
            if (playerMovement != null && playerMovement.IsAutoRunning) playerMovement.CancelAutoRun();
            SetTrackedTale(npcId);
            trackedTaleUserAction = true;   // 이번 판정은 사용자가 막 누른 것 — 대기면 이유를 말하고 푼다
            Refresh();
            trackedTaleUserAction = false;
            if (trackedObjective) ShowStatus($"{NpcDialogueDatabase.StorySpeakerName(npcId)}의 의뢰를 따라갑니다");
        }

        /// <summary>따라가기 해제 — 목표 행이 본편으로 돌아간다.</summary>
        public void StopTrackingTale()
        {
            EnsureTrackedTaleLoaded();
            if (string.IsNullOrEmpty(trackedTaleNpcId)) return;
            if (playerMovement != null && playerMovement.IsAutoRunning) playerMovement.CancelAutoRun();
            SetTrackedTale(string.Empty);
            Refresh();
        }

        // 이번 세션에서 따라가는 이야기가 **실제로 할 일을 가진 적이 있는가**. 아래 대기 판정의 근거다.
        private bool trackedTaleActiveThisSession;
        // TrackTale 안에서 부른 Refresh인가(사용자가 방금 누른 것).
        private bool trackedTaleUserAction;

        private void SetTrackedTale(string npcId)
        {
            trackedTaleNpcId = npcId ?? string.Empty;
            trackedTaleActiveThisSession = false;
            if (string.IsNullOrEmpty(trackedTalePrefsKey)) return;
            PlayerPrefs.SetString(trackedTalePrefsKey, trackedTaleNpcId);
            PlayerPrefs.Save();
        }

        // 계정마다 따로 기억한다. 로그인이 트래커보다 늦게 끝나므로 키가 바뀔 때마다 다시 읽는다.
        private void EnsureTrackedTaleLoaded()
        {
            string key = AuthManager.ScopedKey(GameConstants.PrefsKeys.TrackedTale);
            if (key == trackedTalePrefsKey) return;
            trackedTalePrefsKey = key;
            trackedTaleNpcId = PlayerPrefs.GetString(key, string.Empty);
            trackedTaleActiveThisSession = false;
        }

        /// <summary>
        /// 따라가는 이야기를 목표로 푼다. 풀었으면 true(본편 목표를 건너뛴다).
        /// 할 일이 없는 단계면 따라가기를 스스로 풀고 false — 목표 행이 본편으로 돌아간다.
        /// </summary>
        private bool TryResolveTrackedTale()
        {
            trackedObjective = false;
            if (string.IsNullOrEmpty(trackedTaleNpcId) || storyDirector == null) return false;

            string name = NpcDialogueDatabase.StorySpeakerName(trackedTaleNpcId);
            TaleStepKind step = storyDirector.GetTaleStep(trackedTaleNpcId, out string questId);
            switch (step)
            {
                case TaleStepKind.Talk:
                case TaleStepKind.Report:
                    trackedTaleActiveThisSession = true;
                    return ResolveTaleTalk(step == TaleStepKind.Report);
                case TaleStepKind.Errand:
                    if (ResolveTaleErrand(questId)) { trackedTaleActiveThisSession = true; return true; }
                    break;
                case TaleStepKind.Waiting:
                    // **이번 세션에 할 일이 있던 이야기거나 방금 누른 것일 때만 푼다.** 세션 시작 직후엔
                    // 스토리 진행이 아직 안 읽혔을 수 있다(로그인·클라우드 적재가 트래커보다 늦다) — 그때는
                    // 열람 기록이 비어 모든 이야기가 "대기"로 보이고, 여기서 풀면 저장해 둔 따라가기가
                    // 재시작할 때마다 지워진다. 그 경우엔 조용히 본편을 안내하고 다음 Refresh를 기다린다.
                    if (!trackedTaleActiveThisSession && !trackedTaleUserAction) return false;
                    ShowStatus($"{name}의 이야기는 본편이 더 진행되면 이어집니다");
                    break;
                case TaleStepKind.Done:
                    ShowStatus($"{name}의 이야기를 모두 들었습니다");
                    break;
            }
            SetTrackedTale(string.Empty);
            return false;
        }

        private bool ResolveTaleTalk(bool report)
        {
            VillagerNpc npc = FindStoryNpc(trackedTaleNpcId);
            string name = npc != null ? npc.DisplayName : NpcDialogueDatabase.StorySpeakerName(trackedTaleNpcId);

            hasObjective = true;
            trackedObjective = true;
            trackedTalkTarget = true;
            SetLabel(report ? $"[의뢰] {name}에게 알리기" : $"[의뢰] {name}에게 말 걸기");

            targetNpc = npc;
            if (npc == null)
            {
                // 스폰 전이거나 컬링 중 — 안내 문구만 두고 다음 Refresh에서 다시 찾는다.
                hasWorldTarget = false;
                targetRegionId = string.Empty;
                return true;
            }
            targetPosition = npc.transform.position;
            targetRegionId = npc.RegionId ?? string.Empty;
            hasWorldTarget = true;
            return true;
        }

        private bool ResolveTaleErrand(string questId)
        {
            TutorialQuest quest = questManager != null ? questManager.FindQuest(questId) : null;
            if (quest == null) return false;

            // 만남 뒤에도 선행 퀘스트가 안 끝났으면 의뢰가 세어지지 않는다(초원은 q_capture3). 진행 0/6을
            // 띄우면 잡아도 숫자가 안 움직이니, 무엇을 먼저 끝내야 하는지를 말한다.
            if (IsErrandLocked(quest.questId))
            {
                hasObjective = true;
                trackedObjective = true;
                trackedTalkTarget = false;
                targetNpc = null;
                // 선행이 끝났는데도 잠겼다면 리전 쪽이다(마스터 특권 해제 등) — 끝난 퀘스트를 가리키지 않는다.
                string prereq = !string.IsNullOrEmpty(quest.prerequisiteQuestId) && !questManager.IsQuestCompleted(quest.prerequisiteQuestId)
                    ? questManager.GetQuestTitle(quest.prerequisiteQuestId) : null;
                SetLabel(string.IsNullOrEmpty(prereq) ? "[의뢰] 아직 열리지 않은 의뢰" : $"[의뢰] '{prereq}' 완료 후 시작");
                hasWorldTarget = false;
                targetRegionId = string.Empty;
                return true;
            }

            RegionData region = regionManager != null && !string.IsNullOrEmpty(quest.requiredRegionId)
                ? regionManager.GetRegionById(quest.requiredRegionId) : null;
            bool inside = region != null && InTargetRegion(region.regionId);
            int target = questManager.EffectiveTarget(quest);
            int current = questManager.IsQuestCompleted(quest.questId) ? target : questManager.GetSideProgress(quest.questId);

            hasObjective = true;
            trackedObjective = true;
            trackedTalkTarget = false;
            targetNpc = null;
            SetLabel("[의뢰] " + StoryTaleResolver.DescribeErrand(
                quest.type, region != null ? region.displayName : null, inside, current, target));

            // 리전 안이면 갈 곳이 없다(ActInRegion과 같은 규칙). 밖이면 리전 중심으로 — 다른 리전이면
            // Toggle이 지도를 연다. 의뢰 리전은 만남 비트가 이미 그 리전에서 열렸으므로 잠겨 있지 않다.
            if (region == null || inside)
            {
                hasWorldTarget = false;
                targetRegionId = string.Empty;
                return true;
            }
            targetPosition = region.centerPosition;
            targetRegionId = region.regionId;
            hasWorldTarget = true;
            return true;
        }

        // 같은 storyNpcId가 여러 리전에 설 수 있다 — 현재 리전 개체 우선, 없으면 첫 개체.
        // 마을 주민은 한 명씩이라 사실상 첫 일치다.
        private VillagerNpc FindStoryNpc(string storyNpcId)
        {
            if (npcManager == null || string.IsNullOrEmpty(storyNpcId)) return null;
            string current = regionManager != null && regionManager.CurrentRegion != null
                ? regionManager.CurrentRegion.regionId : null;
            VillagerNpc first = null;
            var list = npcManager.StoryNpcs;
            for (int i = 0; i < list.Count; i++)
            {
                VillagerNpc npc = list[i];
                if (npc == null || npc.StoryNpcId != storyNpcId) continue;
                if (current != null && npc.RegionId == current) return npc;
                if (first == null) first = npc;
            }
            return first;
        }

        /// <summary>
        /// 마을 주민마다 지금 할 일을 물어 머리 위 표식과 지도·미니맵 표식을 정한다.
        /// 동행자에게는 붙이지 않는다 — 세라·라온은 리전마다 서 있어 지도에 같은 사람의 표식이
        /// 열 개씩 뜬다(사용자 결정 2026-09-28). 대상은 저작 데이터의 town 챕터 주민뿐이다.
        /// </summary>
        private void RefreshTaleMarkers(VillagerNpc mainTarget)
        {
            taleMarkers.Clear();
            if (storyDirector == null || npcManager == null) return;

            IReadOnlyList<string> taleIds = storyDirector.TaleNpcIds;
            if (taleIds == null || taleIds.Count == 0) return;

            var list = npcManager.StoryNpcs;
            for (int i = 0; i < list.Count; i++)
            {
                VillagerNpc npc = list[i];
                if (npc == null || !ContainsId(taleIds, npc.StoryNpcId)) continue;

                TaleStepKind step = storyDirector.GetTaleStep(npc.StoryNpcId, out _);
                QuestMark mark = StoryTaleResolver.MarkFor(step, npc == mainTarget);
                npc.SetQuestMark(mark);
                if (mark == QuestMark.New || mark == QuestMark.Report) taleMarkers.Add(new TaleMarker(npc, mark));
            }
        }

        /// <summary>
        /// 머리 위·지도·미니맵 표식을 한 번에 정한다. 본편 대상을 <b>먼저</b> 고르고 마을 주민 루프에 넘긴다 —
        /// 따로 달면 본편 대상이 마을 주민일 때 두 판정이 0.5초마다 표식을 껐다 켠다.
        /// </summary>
        private void RefreshQuestMarks()
        {
            VillagerNpc main = ResolveMainTalkNpc();
            RefreshTaleMarkers(main);

            // 마을 주민이 아닌 본편 대상(어르신·동행자)은 위 루프가 건드리지 않는다 — 여기서 달고 뗀다.
            if (mainMarkNpc != null && mainMarkNpc != main && !IsTaleNpc(mainMarkNpc))
                mainMarkNpc.SetQuestMark(QuestMark.None);
            if (main != null && !IsTaleNpc(main)) main.SetQuestMark(QuestMark.Main);
            mainMarkNpc = main;
        }

        // 본편이 지금 말을 걸라고 하는 개체. 따라가기와 무관하게 본편 목표를 직접 묻는다 —
        // objective 필드는 따라가는 동안 갱신되지 않는다(Refresh가 먼저 돌아 나간다).
        private VillagerNpc ResolveMainTalkNpc()
        {
            if (storyDirector == null || npcManager == null) return null;
            if (!storyDirector.TryGetCurrentObjective(out StoryObjective main)) return null;
            if (main.Kind != StoryObjectiveKind.TalkToNpc) return null;
            return FindNearestStoryNpc(main.TargetId);
        }

        private bool IsTaleNpc(VillagerNpc npc)
        {
            if (npc == null || storyDirector == null) return false;
            IReadOnlyList<string> taleIds = storyDirector.TaleNpcIds;
            return taleIds != null && ContainsId(taleIds, npc.StoryNpcId);
        }

        private static bool ContainsId(IReadOnlyList<string> ids, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] == id) return true;
            return false;
        }

        // 캠페인 첫 목표를 한 번 자동으로 태워 보냈는가.
        private bool firstObjectiveAutoStarted;

        /// <summary>
        /// <b>캠페인 첫 목표에서만</b> 자동 주행을 스스로 시작한다.
        ///
        /// 처음 하는 사람은 곤충을 잡고 나서 "이제 뭘 하지"로 멈춘다 — 목표 행에 어르신이
        /// 떠도 그걸 눌러야 한다는 걸 모른다. 첫 한 번만 태워 보내면 "저기로 가면 되는구나"를
        /// 몸으로 배운다. 그 뒤부터는 직접 누른다.
        ///
        /// 판정을 <c>SeenCount == 0</c>으로 하는 것은 <b>beatId를 박지 않기 위해서다</b> —
        /// 스토리를 하나도 안 봤다는 건 곧 캠페인 첫 목표라는 뜻이고, 이야기를 고쳐도 안 낡는다.
        /// 한 번 시작하면 다시 걸지 않는다(길이 막혀 멈췄는데 또 끌고 가면 갇힌 기분이 든다).
        /// </summary>
        private void TryAutoStartFirstObjective()
        {
            if (firstObjectiveAutoStarted) return;
            if (DreamPrologueState.Active) return;   // 꿈속에서 본 마을 어르신을 향해 달려가면 안 된다
            if (!hasObjective || !hasWorldTarget) return;
            if (storyDirector == null || storyDirector.SeenCount > 0) return;
            if (playerMovement == null || playerMovement.IsFrozen || playerMovement.IsAutoRunning) return;
            // 대화·모달 중이면 조작을 뺏지 않는다.
            if (ModalUIRegistry.IsAnyOpen()) return;
            if (!TargetInCurrentRegion) return;

            // **플래그를 먼저 세운다** — Toggle이 내부에서 Refresh를 다시 부르고, 그 Refresh가
            // 이 메서드를 또 부른다. 플래그가 뒤에 있으면 그 자리에서 무한 재귀가 된다.
            firstObjectiveAutoStarted = true;
            Toggle();
        }

        // 같은 storyNpcId가 여러 리전에 서 있다(라온은 초원·모래언덕·잿불·이름없는자리 4곳).
        // NpcTalk 비트는 **리전을 가리지 않고** 그 NPC와 말하면 발화하므로 어느 개체를 가리켜도
        // 맞다 — 그래서 현재 리전의 개체를 우선하고, 없으면 가장 가까운 개체를 고른다.
        private void ResolveNpcTarget()
        {
            hasWorldTarget = false;
            VillagerNpc best = FindNearestStoryNpc(objective.TargetId);
            if (best == null) { ResolveFreeform(); return; }

            SetLabel(objective.TriggerType == StoryDirector.TriggerDuelWin
                ? $"{best.DisplayName}에게 대결 신청"
                : $"{best.DisplayName}에게 말 걸기");
            targetPosition = best.transform.position;
            targetRegionId = best.RegionId ?? string.Empty;
            targetNpc = best;
            hasWorldTarget = true;
        }

        // 목표 행과 본편 표식이 **같은 개체**를 골라야 한다 — 따로 고르면 "저기로 가라"는 줄과
        // 머리 위 !가 서로 다른 리전의 같은 사람을 가리킨다.
        private VillagerNpc FindNearestStoryNpc(string storyNpcId)
        {
            if (npcManager == null || string.IsNullOrEmpty(storyNpcId)) return null;

            string currentRegion = regionManager != null && regionManager.CurrentRegion != null
                ? regionManager.CurrentRegion.regionId : null;

            VillagerNpc best = null;
            bool bestInRegion = false;
            float bestDist = float.MaxValue;

            var list = npcManager.StoryNpcs;
            for (int i = 0; i < list.Count; i++)
            {
                VillagerNpc npc = list[i];
                if (npc == null || npc.StoryNpcId != storyNpcId) continue;

                bool inRegion = currentRegion != null && npc.RegionId == currentRegion;
                float dist = playerTransform != null
                    ? Vector3.SqrMagnitude(npc.transform.position - playerTransform.position)
                    : 0f;

                // 현재 리전 개체가 무조건 우선, 그 안에서 최근접.
                if (best == null || (inRegion && !bestInRegion) || (inRegion == bestInRegion && dist < bestDist))
                {
                    best = npc;
                    bestInRegion = inRegion;
                    bestDist = dist;
                }
            }
            return best;
        }

        private void ResolveRegionTarget()
        {
            hasWorldTarget = false;
            RegionData region = regionManager != null ? regionManager.GetRegionById(objective.TargetId) : null;
            if (region == null) { ResolveFreeform(); return; }

            if (TryRedirectLockedRegion(region)) return;

            bool guardian = objective.Kind == StoryObjectiveKind.DefeatGuardian;
            SetLabel(StoryObjectiveResolver.DescribeRegionObjective(
                region.displayName, guardian, region.guardianLevel));
            targetPosition = guardian ? regionManager.GetGuardianPosition(region) : region.centerPosition;
            targetRegionId = region.regionId;
            hasWorldTarget = true;
        }

        /// <summary>
        /// 목적지 리전이 <b>잠겨 있으면</b> 그 열쇠(앞 리전 수문장)로 안내를 바꿔 친다.
        /// 라벨·좌표·리전을 모두 앞 리전 쪽으로 두므로 HUD 문구·미니맵 쐐기·원터치 이동이
        /// 함께 옮겨 간다. 바꿔 쳤으면 true.
        ///
        /// 스토리는 해금을 <b>읽기만</b> 한다 — 판정(<c>IsRegionAccessible</c>)과 열쇠
        /// (<c>GetGatekeeperRegion</c>)는 <c>RegionManager</c>가 단일 출처다. 앞 리전도 잠겨
        /// 있으면(두 챕터 뒤를 가리키는 세이브) 열려 있는 곳이 나올 때까지 거슬러 간다.
        /// 사슬 밖(열쇠가 없는 곳)이면 손대지 않는다.
        /// </summary>
        private bool TryRedirectLockedRegion(RegionData region)
        {
            if (region == null || regionManager == null) return false;
            if (regionManager.IsRegionAccessible(region)) return false;

            // 잠긴 리전부터 거슬러 올라가며 **지금 손이 닿는 열쇠**를 찾는다. 열쇠는 둘이다 — 앞 리전 수문장, 그리고
            // 수문장을 이긴 뒤에도 남은 이야기 대결(서릿길 ← 집게, 잿불 골짜기 ← 저울). 수문장만 보던 시절의 로직으로는
            // 잿불 골짜기를 가리키는 세이브가 "모래언덕 수문장 격파"(이미 이긴 것)로 안내됐다.
            // 판정은 RegionManager.GetLockKind 하나다 — 필드 차단 문구·지도와 같은 답을 낸다.
            // 최대 리전 수만큼만 거슬러 간다 — 체인은 유한하지만 데이터 오류로 순환하면 여기서 멈춘다.
            RegionData locked = region;
            int hops = regionManager.Regions != null ? regionManager.Regions.Length : 16;
            while (locked != null && hops-- > 0)
            {
                RegionManager.LockKind kind = regionManager.GetLockKind(
                    locked, out RegionData gate, out RegionManager.StoryLock storyLock);

                if (kind == RegionManager.LockKind.StoryDuel)
                    return RedirectToDuelOpponent(region, storyLock);

                if (kind != RegionManager.LockKind.Guardian || gate == null) return false;   // 열쇠를 모른다 — 지어내지 않는다
                if (!regionManager.IsRegionAccessible(gate)) { locked = gate; continue; }   // 앞 리전도 잠겼다 — 한 칸 더

                SetLabel(StoryObjectiveResolver.DescribeRegionObjective(
                    region.displayName, false, 0, gate.displayName, gate.guardianLevel));
                targetPosition = regionManager.GetGuardianPosition(gate);
                targetRegionId = gate.regionId;
                hasWorldTarget = true;
                redirectedToGatekeeper = true;   // 이유는 원래 비트의 것 — 수문장 안내 밑에 붙이지 않는다(Refresh가 Why를 비운다)
                return true;
            }
            return false;   // 순환·상한 소진 — 잠긴 곳을 가리키지 않는다
        }

        /// <summary>
        /// 이야기 대결이 남은 잠금 — 그 간부에게 안내한다("서릿길(으)로 가려면 집게에게 이기기").
        /// 대개는 본편 목표가 이미 그 간부의 승리 비트(<c>duel_grip_win</c>, 스파인)라 같은 사람을 가리킨다 —
        /// 여기는 다른 비트(마을 이야기·곁가지)가 잠긴 리전을 가리킬 때의 안내다. 간부가 월드에 없으면(스폰 전·컬링)
        /// 문구만 남기고 화살표는 띄우지 않는다 — 다음 Refresh에서 다시 찾는다.
        /// </summary>
        private bool RedirectToDuelOpponent(RegionData region, RegionManager.StoryLock storyLock)
        {
            SetLabel(StoryObjectiveResolver.DescribeDuelLockObjective(region.displayName, storyLock.displayName));
            redirectedToGatekeeper = true;   // 원래 비트의 이유는 이 안내와 무관하다
            VillagerNpc opponent = FindNearestStoryNpc(storyLock.storyNpcId);
            if (opponent == null)
            {
                hasWorldTarget = false;
                targetRegionId = string.Empty;
                return true;
            }
            targetPosition = opponent.transform.position;
            targetRegionId = opponent.RegionId ?? string.Empty;
            hasWorldTarget = true;
            return true;
        }

        private void ResolveSubAreaTarget()
        {
            hasWorldTarget = false;
            if (regionManager == null || regionManager.Regions == null) { ResolveFreeform(); return; }

            foreach (RegionData region in regionManager.Regions)
            {
                if (region == null || region.subAreas == null) continue;
                foreach (SubAreaData sub in region.subAreas)
                {
                    if (sub == null || sub.subAreaId != objective.TargetId) continue;
                    // 잠긴 리전의 서브에리어도 열쇠부터 — 2막 대치 비트(SubAreaEnter)가 그 자리다.
                    if (TryRedirectLockedRegion(region)) return;
                    SetLabel($"{sub.displayName}(으)로");
                    targetPosition = sub.centerPosition;
                    targetRegionId = region.regionId;
                    hasWorldTarget = true;
                    return;
                }
            }
            ResolveFreeform();
        }

        /// <summary>
        /// "그 리전에서 무언가 하기"(무param 포획·전투 + <c>requiredRegionId</c>).
        ///
        /// 이미 그 리전 안이면 갈 곳이 없으므로 문구만 띄우고, 밖이면 <b>리전 중심을 타깃으로
        /// 잡아</b> 미니맵 쐐기·지도 마커·원터치 이동이 전부 살아난다. 저작된 비트 28개가
        /// 이 경로를 타며, 예전엔 전부 "모험을 이어가세요"로 떨어졌다.
        /// </summary>
        private void ResolveActInRegion()
        {
            hasWorldTarget = false;
            RegionData region = regionManager != null
                ? regionManager.GetRegionById(objective.RequiredRegionId) : null;
            if (region == null) { ResolveFreeform(); return; }
            if (TryRedirectLockedRegion(region)) return;

            bool inside = InTargetRegion(region.regionId);
            SetLabel(StoryObjectiveResolver.DescribeActionObjective(
                objective.TriggerType, region.displayName, inside,
                InsectDisplayName(objective.TargetId), null, objective.Threshold, -1));

            if (inside) { targetRegionId = string.Empty; return; }

            targetPosition = region.centerPosition;
            targetRegionId = region.regionId;
            hasWorldTarget = true;
        }

        // 위치가 정말로 없는 목표(레벨·도감·퀘스트 완료) + 위 해석들이 실패했을 때의 폴백.
        private void ResolveFreeform()
        {
            hasWorldTarget = false;
            targetRegionId = string.Empty;

            // 갈 곳이 있어야 할 종류인데 대상을 못 찾은 경우 — 종류에 맞는 폴백 문구.
            switch (objective.Kind)
            {
                case StoryObjectiveKind.TalkToNpc: SetLabel("동행자를 찾아 대화"); return;
                case StoryObjectiveKind.EnterRegion:
                case StoryObjectiveKind.EnterSubArea:
                case StoryObjectiveKind.DefeatGuardian: SetLabel("새로운 장소를 찾아서"); return;
            }

            SetLabel(StoryObjectiveResolver.DescribeActionObjective(
                objective.TriggerType,
                RegionDisplayName(objective.RequiredRegionId),
                InTargetRegion(objective.RequiredRegionId),
                InsectDisplayName(objective.TargetId),
                QuestTitle(objective.TargetId),
                objective.Threshold,
                CurrentProgressValue()));
        }

        // 라벨은 0.5초마다 다시 만들어지지만 대부분 같은 문자열이다. 값이 같으면 기존 참조를
        // 유지한다 — HUD(TutorialQuestUI.DrawObjectiveRow)가 ReferenceEquals로 캐시 적중을
        // 판정하므로, 매번 새 인스턴스를 물리면 저쪽 문자열 조립이 헛돈다.
        private void SetLabel(string value)
        {
            if (value == null) value = string.Empty;
            if (!string.Equals(label, value, System.StringComparison.Ordinal)) label = value;
        }

        // SetLabel과 같은 이유로 값이 같으면 참조를 유지한다.
        private void SetWhy(string value)
        {
            if (value == null) value = string.Empty;
            if (!string.Equals(why, value, System.StringComparison.Ordinal)) why = value;
        }

        private bool InTargetRegion(string regionId)
        {
            if (string.IsNullOrEmpty(regionId) || regionManager == null
                || regionManager.CurrentRegion == null) return false;
            return regionManager.CurrentRegion.regionId == regionId;
        }

        private string InsectDisplayName(string insectId)
        {
            if (string.IsNullOrEmpty(insectId) || insectDatabase == null) return null;
            InsectData data = insectDatabase.GetById(insectId);
            return data != null ? data.displayName : null;
        }

        private string QuestTitle(string questId)
        {
            if (string.IsNullOrEmpty(questId) || questManager == null) return null;
            return questManager.GetQuestTitle(questId);
        }

        /// <summary>
        /// 진행형 목표(레벨·도감)의 현재값. 모르면 -1(그때는 임계값만 띄운다).
        ///
        /// <b>매 Refresh마다 새로 읽는다.</b> <see cref="StoryObjective"/>는 StoryDirector가
        /// 캐시하고 진행이 바뀔 때만 무효화하므로, 거기 담으면 화면에 낡은 수치가 굳는다.
        /// </summary>
        private int CurrentProgressValue()
        {
            if (objective.TriggerType == StoryDirector.TriggerLevelReach)
                return progressController != null ? progressController.Level : -1;
            if (objective.TriggerType == StoryDirector.TriggerDexProgress)
                return dexController != null ? dexController.CapturedSpeciesCount : -1;
            return -1;
        }

        /// <summary>목표 행 버튼 — 주행 중이면 취소, 아니면 시작.</summary>
        public void Toggle()
        {
            if (playerMovement == null) return;

            if (playerMovement.IsAutoRunning)
            {
                playerMovement.CancelAutoRun();
                return;
            }

            Refresh();   // 버튼을 누른 순간의 최신 위치로
            if (!hasObjective || !hasWorldTarget) return;

            if (!TargetInCurrentRegion)
            {
                // 리전 이동은 지도의 기존 경로를 쓴다 — 접근 가능 여부·수문장 판정이 거기 있다.
                // 예전엔 문구만 띄우고 끝이라, 안내를 읽고도 지도를 직접 열어 그 리전을 찾아야 했다.
                if (MapRequested != null) MapRequested.Invoke(targetRegionId);
                else ShowStatus($"지도에서 {RegionDisplayName(targetRegionId)}(으)로 먼저 이동하세요");
                return;
            }

            // 따라가기 목표는 objective를 거치지 않는다 — 종류를 따로 들고 있다.
            bool talkTarget = trackedObjective ? trackedTalkTarget : objective.Kind == StoryObjectiveKind.TalkToNpc;
            playerMovement.BeginAutoRun(
                targetPosition,
                talkTarget
                    ? Mathf.Max(0.5f, WorldInteractionController.VillagerTalkRadius - TalkArriveMargin)
                    : 2f);
        }

        // 빈 ID면 빈 문자열을 준다 — DescribeActionObjective가 그걸 "리전 모름"으로 읽어
        // 지명 없는 문구로 떨어진다. null을 흘리면 그쪽 분기가 지명을 붙이려다 빈칸을 만든다.
        private string RegionDisplayName(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return string.Empty;
            RegionData r = regionManager != null ? regionManager.GetRegionById(regionId) : null;
            return r != null && !string.IsNullOrEmpty(r.displayName) ? r.displayName : regionId;
        }

        private void ShowStatus(string message)
        {
            statusMessage = message;
            statusTimer = StatusMessageSeconds;
        }
    }
}
