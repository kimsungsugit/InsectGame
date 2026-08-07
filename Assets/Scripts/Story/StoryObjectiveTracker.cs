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

        private float refreshTimer;
        private bool hasObjective;
        private StoryObjective objective;
        private string label = string.Empty;
        private bool hasWorldTarget;
        private Vector3 targetPosition;
        private string targetRegionId = string.Empty;

        private string statusMessage = string.Empty;
        private float statusTimer;

        // ── 읽기 전용 표면 (HUD가 소비) ──

        public bool HasObjective => hasObjective;
        /// <summary>"세라에게 말 걸기" 같은 한 줄. 목표가 없으면 빈 문자열.</summary>
        public string Label => label;
        /// <summary>갈 곳이 정해진 목표인가 — false면 자동 주행 버튼을 띄우지 않는다.</summary>
        public bool HasWorldTarget => hasWorldTarget;
        public Vector3 TargetPosition => targetPosition;
        public bool IsRunning => playerMovement != null && playerMovement.IsAutoRunning;
        /// <summary>목표가 지금 리전에 있는가. 아니면 이동 전에 텔레포트가 필요하다.</summary>
        public bool TargetInCurrentRegion =>
            regionManager != null && regionManager.CurrentRegion != null
            && targetRegionId == regionManager.CurrentRegion.regionId;

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
                return;
            }

            switch (objective.Kind)
            {
                case StoryObjectiveKind.TalkToNpc: ResolveNpcTarget(); break;
                case StoryObjectiveKind.EnterRegion:
                case StoryObjectiveKind.DefeatGuardian: ResolveRegionTarget(); break;
                case StoryObjectiveKind.EnterSubArea: ResolveSubAreaTarget(); break;
                default: ResolveFreeform(); break;
            }
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

            label = $"{best.DisplayName}에게 말 걸기";
            targetPosition = best.transform.position;
            targetRegionId = best.RegionId ?? string.Empty;
            hasWorldTarget = true;
        }

        private void ResolveRegionTarget()
        {
            hasWorldTarget = false;
            RegionData region = regionManager != null ? regionManager.GetRegionById(objective.TargetId) : null;
            if (region == null) { ResolveFreeform(); return; }

            bool guardian = objective.Kind == StoryObjectiveKind.DefeatGuardian;
            label = guardian ? $"{region.displayName} 수문장 격파" : $"{region.displayName}(으)로";
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
                    label = $"{sub.displayName}(으)로";
                    targetPosition = sub.centerPosition;
                    targetRegionId = region.regionId;
                    hasWorldTarget = true;
                    return;
                }
            }
            ResolveFreeform();
        }

        // 위치가 없는 목표(전투 승리·포획·레벨·도감). 문구만 띄운다.
        private void ResolveFreeform()
        {
            hasWorldTarget = false;
            targetRegionId = string.Empty;
            switch (objective.Kind)
            {
                case StoryObjectiveKind.TalkToNpc: label = "동행자를 찾아 대화"; break;
                case StoryObjectiveKind.EnterRegion:
                case StoryObjectiveKind.EnterSubArea:
                case StoryObjectiveKind.DefeatGuardian: label = "새로운 장소를 찾아서"; break;
                default: label = "모험을 이어가세요"; break;
            }
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
                ShowStatus($"지도에서 {RegionDisplayName(targetRegionId)}(으)로 먼저 이동하세요");
                return;
            }

            playerMovement.BeginAutoRun(
                targetPosition,
                objective.Kind == StoryObjectiveKind.TalkToNpc
                    ? Mathf.Max(0.5f, WorldInteractionController.VillagerTalkRadius - TalkArriveMargin)
                    : 2f);
        }

        private string RegionDisplayName(string regionId)
        {
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
