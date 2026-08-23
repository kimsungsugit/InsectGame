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
        private bool hasWorldTarget;
        private Vector3 targetPosition;
        private string targetRegionId = string.Empty;
        // TalkToNpc 목표가 고른 개체. Refresh가 매번 다시 고르므로 스폰/컬링으로 오가도 최신이다.
        private VillagerNpc targetNpc;

        private string statusMessage = string.Empty;
        private float statusTimer;

        // ── 읽기 전용 표면 (HUD가 소비) ──

        public bool HasObjective => hasObjective;
        /// <summary>"세라에게 말 걸기" 같은 한 줄. 목표가 없으면 빈 문자열.</summary>
        public string Label => label;
        /// <summary>
        /// 지금 목표가 가리키는 스토리 NPC 개체. 목표가 <c>TalkToNpc</c>가 아니거나 그 NPC가
        /// 월드에 없으면 null. <see cref="StoryStageDirector"/>의 조우 접근이 읽는다 —
        /// 개체 선택 규칙(현재 리전 우선 → 최근접)을 저쪽에 복제하지 않기 위해서다.
        /// </summary>
        public VillagerNpc TargetNpc => targetNpc;
        /// <summary>갈 곳이 정해진 목표인가 — false면 자동 주행 버튼을 띄우지 않는다.</summary>
        public bool HasWorldTarget => hasWorldTarget;
        public Vector3 TargetPosition => targetPosition;
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
            hasObjective = storyDirector != null && storyDirector.TryGetCurrentObjective(out objective);
            if (!hasObjective)
            {
                label = string.Empty;
                hasWorldTarget = false;
                targetRegionId = string.Empty;
                targetNpc = null;
                return;
            }

            // 아래 switch에서 ResolveNpcTarget만 다시 채운다 — 목표 종류가 바뀌면 자동으로 비워진다.
            targetNpc = null;

            switch (objective.Kind)
            {
                case StoryObjectiveKind.TalkToNpc: ResolveNpcTarget(); break;
                case StoryObjectiveKind.EnterRegion:
                case StoryObjectiveKind.DefeatGuardian: ResolveRegionTarget(); break;
                case StoryObjectiveKind.EnterSubArea: ResolveSubAreaTarget(); break;
                case StoryObjectiveKind.ActInRegion: ResolveActInRegion(); break;
                default: ResolveFreeform(); break;
            }

            TryAutoStartFirstObjective();
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
            if (npcManager == null || string.IsNullOrEmpty(objective.TargetId)) { ResolveFreeform(); return; }

            string currentRegion = regionManager != null && regionManager.CurrentRegion != null
                ? regionManager.CurrentRegion.regionId : null;

            VillagerNpc best = null;
            bool bestInRegion = false;
            float bestDist = float.MaxValue;

            var list = npcManager.StoryNpcs;
            for (int i = 0; i < list.Count; i++)
            {
                VillagerNpc npc = list[i];
                if (npc == null || npc.StoryNpcId != objective.TargetId) continue;

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

            if (best == null) { ResolveFreeform(); return; }

            SetLabel($"{best.DisplayName}에게 말 걸기");
            targetPosition = best.transform.position;
            targetRegionId = best.RegionId ?? string.Empty;
            targetNpc = best;
            hasWorldTarget = true;
        }

        private void ResolveRegionTarget()
        {
            hasWorldTarget = false;
            RegionData region = regionManager != null ? regionManager.GetRegionById(objective.TargetId) : null;
            if (region == null) { ResolveFreeform(); return; }

            bool guardian = objective.Kind == StoryObjectiveKind.DefeatGuardian;
            SetLabel(guardian ? $"{region.displayName} 수문장 격파" : $"{region.displayName}(으)로");
            targetPosition = guardian ? regionManager.GetGuardianPosition(region) : region.centerPosition;
            targetRegionId = region.regionId;
            hasWorldTarget = true;
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

            playerMovement.BeginAutoRun(
                targetPosition,
                objective.Kind == StoryObjectiveKind.TalkToNpc
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
