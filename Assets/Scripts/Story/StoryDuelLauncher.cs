using InsectGame.Core;
using InsectGame.NPC;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>
    /// <b>대사 직후 대결</b> — 비트의 <see cref="StoryBeat.duelAfter"/>를 읽어, 그 장면이 다 끝난 첫 순간에 대결을 연다.
    ///
    /// 예전엔 대치 대사가 "한 번 더 말을 걸면 붙는다"(<c>talk_grip</c>)로 끝나서, 이야기 속 싸움이 플레이어가 다시
    /// 말을 걸 때까지 한 박자 밀렸다(아이들은 그 한 박자를 모른다 — 대사만 보고 지나갔다). 2026-10-04 사용자 결정:
    /// 명부회 간부전은 본편 필수이고 대치 대사가 끝나면 곧바로 붙는다. 라온의 초원 첫 만남도 "승부다!" 뒤 곧바로다.
    ///
    /// <b>기다리는 것</b>(<see cref="ShouldWait"/>): 같은 비트의 영상·컷신·NPC 연출, 선택지 결과 대사, 그 사이 미뤄 둔
    /// 다른 대사, 다른 전투(결과 화면 포함), 조작 잠금. 모달만 보면 <b>영상이 끝난 프레임과 큐가 선택 결과를 여는 프레임
    /// 사이</b>가 비어 보여 결과 대사보다 대결이 먼저 열린다(<c>ch9_confront</c>) — 그래서 스토리 큐(<see cref="StoryDirector.IsBusy"/>)도 본다.
    ///
    /// <b>못 열면 버린다</b> — 재도전 대기·출전 곤충 없음·자리를 떠남·너무 오래 막힘. 조용히 실패하되 로그를 남긴다.
    /// 그 상대에게 다시 말을 걸면 열리는 길이 따로 있다(간부: <c>WorldInteractionController</c> → <c>TryStartBossDuel</c>,
    /// 라온: 대화창 [승부하기]). 대기열은 한 칸이다 — 대사가 이어서 둘 끝나는 일은 없고, 있으면 나중 것이 맞다.
    ///
    /// <b>꿈 안에서는 아무것도 안 한다</b>(<c>DreamPrologueState.Active</c>, rules/dream-prologue.md). 샌드박스 전투는 꿈에서만 열리므로
    /// 같은 표지로 걸러진다 — <c>InsectBattleController.IsSandbox</c>는 다음 전투가 시작될 때까지 값이 남아 "지금 샌드박스인가"를 못 답한다.
    ///
    /// 싱글턴 아님 — 부트스트랩이 AutoWire로 잇는다. 이벤트 구독은 AutoWire·OnEnable이 함께 건다(UI 루트 토글로 꺼졌다 켜져도 산다).
    /// </summary>
    public class StoryDuelLauncher : MonoBehaviour
    {
        /// <summary>막힌 채 이만큼(실제 시간) 지나면 버린다 — 영상(최장 15초)·대사·결과 화면을 다 기다리고도 남는 값.</summary>
        public const float GiveUpSeconds = 120f;

        private StoryDirector storyDirector;
        private NpcDuelController duelController;
        private RegionManager regionManager;
        private CameraFollower cameraFollower;
        private PlayerMovement playerMovement;

        private string pendingNpcId = string.Empty;
        private string pendingBeatId = string.Empty;
        // 대기열에 넣을 때 서 있던 리전(서브에리어면 그 리전, 나의 섬이면 null) — 자리를 떠나면 그 장면은 끝난 것이다.
        private string pendingRegionId;
        private float pendingSeconds;

        /// <summary>지금 열리기를 기다리는 대결이 있는가.</summary>
        public bool HasPendingDuel => !string.IsNullOrEmpty(pendingNpcId);

        /// <summary>
        /// 기다리는 대결의 상대(storyNpcId). 없으면 빈 문자열. 대화창이 <b>선택지 결과 대사</b>의 마지막 버튼을 「승부!」로 바꿀 때
        /// 읽는다 — 결과 비트 자신은 <see cref="StoryBeat.duelAfter"/>가 비어 있고(선택지를 단 비트가 들고 있다), 그 비트가
        /// 끝난 순간 이미 여기 대기열에 들어와 있다.
        /// </summary>
        public string PendingNpcId => pendingNpcId;

        /// <summary>마지막으로 대기열에 넣은 상대 — 검수 도구(<c>StoryBeatWalkthrough</c>)가 "대사 직후 대결이 걸렸나"를 본다.</summary>
        public string LastQueuedNpcId { get; private set; } = string.Empty;

        /// <summary>
        /// 검수 도구(배치 걸음)가 켠다 — 켜진 동안 대기열에 넣기만 하고 실제 전투는 열지 않는다. 걸음은 전투를 시뮬레이션하므로
        /// 진짜 전투가 열리면 전투 화면이 걸음의 다음 행위를 막는다. 게임 코드는 이 값을 건드리지 않는다.
        /// </summary>
        public bool Suspended { get; set; }

        public void AutoWire(StoryDirector director, NpcDuelController duel, RegionManager region,
            CameraFollower follower, PlayerMovement movement)
        {
            if (storyDirector == null) storyDirector = director;
            if (duelController == null) duelController = duel;
            if (regionManager == null) regionManager = region;
            if (cameraFollower == null) cameraFollower = follower;
            if (playerMovement == null) playerMovement = movement;
            Subscribe();
        }

        // AutoWire와 OnEnable이 함께 부른다 — `-=` 뒤 `+=`라 중복 구독이 되지 않는다.
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
        }

        private void OnDestroy()
        {
            if (storyDirector != null) storyDirector.StoryBeatCompleted -= OnBeatCompleted;
        }

        private void OnBeatCompleted(StoryBeat beat)
        {
            if (beat == null || string.IsNullOrWhiteSpace(beat.duelAfter)) return;
            if (DreamPrologueState.Active) return;   // 꿈은 이야기의 대결을 열지 않는다

            string npcId = beat.duelAfter.Trim();
            if (KindOf(npcId) == DuelKind.None)
            {
                // story_lint 검사 35가 막는 오타 — 런타임에도 한 번 말한다(대결이 그냥 안 열리면 화면상 티가 안 난다).
                Debug.LogWarning($"[StoryDuel] {beat.beatId}: duelAfter '{npcId}'는 대결 표에 없는 상대다");
                return;
            }
            if (HasPendingDuel && pendingNpcId != npcId)
                Debug.Log($"[StoryDuel] {pendingBeatId} 뒤 {pendingNpcId} 대결을 {beat.beatId} 뒤 {npcId} 대결로 바꾼다");

            pendingNpcId = npcId;
            pendingBeatId = beat.beatId;
            pendingRegionId = regionManager != null ? regionManager.ActionRegionId : null;
            pendingSeconds = 0f;
            LastQueuedNpcId = npcId;
        }

        private void Update()
        {
            if (!HasPendingDuel) return;
            if (DreamPrologueState.Active) { Drop("꿈이 시작됐다"); return; }
            if (Suspended) return;

            pendingSeconds += Time.unscaledDeltaTime;

            string here = regionManager != null ? regionManager.ActionRegionId : null;
            if (regionManager != null && here != pendingRegionId) { Drop("그 자리를 떠났다"); return; }

            if (ShouldWait(ModalUIRegistry.IsAnyOpen(),
                    cameraFollower != null && cameraFollower.InBattleMode,
                    storyDirector != null && storyDirector.IsBusy,
                    playerMovement != null && playerMovement.IsFrozen))
            {
                if (pendingSeconds >= GiveUpSeconds) Drop($"{GiveUpSeconds:0}초 넘게 화면이 비지 않았다");
                return;
            }

            Launch();
        }

        private void Launch()
        {
            string npcId = pendingNpcId;
            string beatId = pendingBeatId;
            Clear();

            bool started = false;
            if (duelController != null)
            {
                switch (KindOf(npcId))
                {
                    case DuelKind.Boss: started = duelController.TryStartBossDuel(npcId, Time.time); break;
                    case DuelKind.Rival: started = duelController.TryStartRivalDuel(npcId, Time.time); break;
                }
            }
            if (!started)
                Debug.Log($"[StoryDuel] {beatId} 뒤 {npcId} 대결을 열지 못했다(재도전 대기·출전 곤충 없음 등) — 다시 말을 걸면 열린다");
        }

        private void Drop(string reason)
        {
            Debug.Log($"[StoryDuel] {pendingBeatId} 뒤 {pendingNpcId} 대결을 버린다 — {reason}");
            Clear();
        }

        private void Clear()
        {
            pendingNpcId = string.Empty;
            pendingBeatId = string.Empty;
            pendingRegionId = null;
            pendingSeconds = 0f;
        }

        // ── 순수 판정 (테스트가 씬 없이 본다) ──

        public enum DuelKind { None, Boss, Rival }

        /// <summary>
        /// 이 상대는 어느 대결인가 — 명부회 간부 표(<see cref="NpcBossDuels"/>)가 먼저다. 한 인물이 두 표에 함께 있을 일은 없다
        /// (<c>NpcRivalDuelTests</c>가 단계 ID와 간부 ID가 안 겹치는지 본다).
        /// </summary>
        public static DuelKind KindOf(string storyNpcId)
        {
            if (string.IsNullOrEmpty(storyNpcId)) return DuelKind.None;
            if (NpcBossDuels.IsBoss(storyNpcId)) return DuelKind.Boss;
            if (NpcRivalDuels.IsRival(storyNpcId)) return DuelKind.Rival;
            return DuelKind.None;
        }

        /// <summary>
        /// 아직 기다려야 하는가 — 하나라도 참이면 기다린다. 모달(대화·영상·컷신·NPC 연출·배지 연출·메뉴), 전투 화면(결과 포함 —
        /// 카메라의 배틀 모드가 유일한 신뢰 신호다, <c>StoryDirector.ShouldDeferNow</c>와 같은 이유), 스토리 큐, 조작 잠금.
        /// </summary>
        public static bool ShouldWait(bool modalOpen, bool battleOnScreen, bool storyBusy, bool playerFrozen)
        {
            return modalOpen || battleOnScreen || storyBusy || playerFrozen;
        }
    }
}
