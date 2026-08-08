using System.Collections.Generic;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.UI
{
    public partial class RaidBattleUI : MonoBehaviour
    {
        [SerializeField] private RaidBattleController raidController;
        [SerializeField] private CameraFollower cameraFollower;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private BattleArenaController arena;

        // `SelectInsect`는 없다 — 팀이 **슬롯 순서대로 하나씩** 행동하므로 누가 나설지 고를 여지가
        // 없고, 컨트롤러의 `ActiveSlot`이 차례를 들고 있다. 그래서 라운드가 열리면 곧바로
        // 그 곤충의 스킬 화면(`SelectSkill`)이 뜬다. 예전엔 [1-5] 곤충 선택을 한 번 거쳐야
        // 스킬이 보여서 "전투 중에 스킬이 안 보인다"는 인상을 줬다.
        private enum Phase
        {
            None,
            Intro,
            SelectSkill,
            PlayerAttack,
            BossTelegraph,
            BossAttack,
            UniteAttack,
            TeamTurnAnnounce,
            Result
        }

        private Phase phase = Phase.None;
        public bool IsRaidActive => phase != Phase.None;

        private float phaseTimer;
        private float introTimer;
        private float resultTimer;
        private bool resultShown;
        // 인터-턴 배너 — **팀 턴 쪽만** 있다.
        // 옛 `Phase.TurnAnnounce`는 "팀의 턴"과 "보스의 턴"을 함께 그렸는데, 5f0776f가 라운드 파이프라인을
        // BossTelegraph/TryCompleteRoundPresentation로 갈아끼우며 호출부 5곳을 전부 지워 배너가 한 번도
        // 뜨지 않게 됐다(기계 장치만 남아 살아 있는 코드처럼 보였다).
        // **보스 쪽은 되살리지 않는다** — `DrawBossTelegraph`가 같은 introFightStyle로 같은 중앙 위치에
        // "⚠ 보스 공격 준비!"를 그리고 `DrawBossIntent`가 `CASTING` + 진행바까지 얹어 더 많이 알려주므로,
        // 되살리면 0.72s 텔레그래프 위에 배너가 겹친다. 대체가 없는 팀 쪽만 여기서 복원했다.
        // 라운드가 끝나 조작이 돌아오는 순간에만 뜨고, 탭으로 즉시 넘길 수 있다.
        // 1v1(`BattleScreenUI`)의 동명 코드는 호출부가 살아 있어 그대로 둔다.
        private const float TeamTurnAnnounceDuration = 0.9f;
        private float announceTimer;

        private string actionText;
        private float actionTimer;
        private int lastDmgToBoss;
        private int lastDmgToTeam;
        private bool lastAoe;
        private int lastHitSlot;
        private string lastSkillUsedName;
        /// <summary>
        /// 방금 연출 중인 팀원 하나의 행동. 순차 턴에서 이펙트 속성·색을 정하는 기준이다 —
        /// 라운드의 "리더 행동"을 쓰면 3번째 곤충이 때리는데 1번째 곤충의 속성이 터진다.
        /// 합체공격은 특정 한 마리가 아니므로 null이고, 그때는 라운드 쪽으로 폴백한다.
        /// </summary>
        private RaidActionResult lastMemberAction;
        private RaidRoundResult activeRound;
        private bool teamAnimationComplete;
        private bool bossAnimationComplete;
        private bool bossResponseRequested;
        private bool presentationCompletionRequested;
        private const float TeamRushMinDuration = 0.85f;
        // ★ `DrawUniteAttackAnimation`의 타임라인과 반드시 함께 움직인다. 그 오버레이는 2.5s 기준으로
        // 작성돼 있다 — 마지막 멤버 돌진이 t=1.55에 착탄하고, 최종 폭발이 t>1.5, "TOTAL -N"이 t>1.8에
        // 뜬다. 5f0776f가 종료 조건을 `uniteAnimTimer > 2.5f`에서 이 상한으로 옮기며 1.15f를 넣는 바람에
        // 5인 팀이면 3명의 착탄·데미지 숫자와 폭발·TOTAL이 통째로 렌더되지 않았다(아레나 코루틴은
        // 0.42+0.28=0.70s에 끝나 `teamAnimationComplete`가 먼저 서고, 2D 폴백은 아예 즉시 true라
        // 두 경로 모두 정확히 상한에서 이탈한다). 오버레이는 3D 아레나 위에도 그려지므로 공통 문제다.
        private const float UniteRushMinDuration = 2.5f;
        private const float BossTelegraphDuration = 0.72f;
        private const float BossImpactMinDuration = 0.8f;

        private float displayBossHp;
        private float[] displayTeamHp;
        private float bossShake;
        private float[] teamShake;

        private int selectedSlot = -1;

        private Rect[] raidSkillRects = new Rect[4];
        private bool[] raidSkillUsable = new bool[4];
        private int raidSkillCount;
        /// <summary>스탠스 칩 3개의 히트 영역. 그리는 쪽(<c>DrawStanceChips</c>)이 매 패스 채운다.</summary>
        private Rect[] stanceRects = new Rect[3];
        /// <summary>이 곤충 하나를 AI에게 맡기는 버튼.</summary>
        private Rect autoBtnRect;
        /// <summary>남은 팀원 전부를 AI에게 맡기는 버튼(라운드를 1탭으로 닫는다).</summary>
        private Rect autoAllBtnRect;
        private bool autoBtnVisible;

        private bool wantSkillQ, wantSkillW, wantSkillE, wantSkillR;
        // `wantEscape`는 없다 — 스킬 화면에서 되돌아갈 곤충 선택 단계가 사라졌다.
        private bool wantUnite;
        private bool wantAuto;
        private bool wantAutoAll;
        /// <summary>
        /// "전원 자동"으로 남은 팀원을 <b>한 마리씩</b> 흘려보내는 중. 연출이 하나 끝날 때마다
        /// 다음 한 마리를 실행한다 — 라운드가 끝나거나 플레이어가 직접 고르면 꺼진다.
        /// </summary>
        private bool autoPilotRemaining;
        private bool wantMouseClick;
        private Vector2 guiMousePos;
        private float uniteAnimTimer;
        private Rect uniteBtnRect;
        private bool uniteBtnVisible;


        private void OnEnable()
        {
            // OnDisable이 해지한 구독을 되살린다 — 오프닝 다시보기가 UI 루트를 토글하기 때문.
            SubscribeRaidController();
        }

        private void OnDisable()
        {
            if (raidController != null)
            {
                raidController.RaidUpdated -= OnRaidUpdated;
                raidController.RaidEnded -= OnRaidEnded;
                raidController.RaidMemberActionResolved -= OnRaidMemberActionResolved;
                raidController.RaidTeamRushResolved -= OnRaidTeamRushResolved;
                raidController.RaidBossResponseResolved -= OnRaidBossResponseResolved;
                raidController.RaidRoundCompleted -= OnRaidRoundCompleted;
            }
            // timeScale 안전 복구 (다른 시스템이 변경한 채 종료된 경우 대비)
            if (Time.timeScale < 0.99f) Time.timeScale = 1f;
        }

        private void OnRaidUpdated()
        {
            if (!raidController.IsActive && phase != Phase.Result) return;

            if (phase == Phase.None)
            {
                int count = raidController.TeamStats.Length;
                displayTeamHp = new float[count];
                teamShake = new float[count];
                for (int i = 0; i < count; i++)
                    displayTeamHp[i] = raidController.TeamStats[i].CurrentHp;
                displayBossHp = raidController.BossStats.CurrentHp;
                bossShake = 0;
                selectedSlot = raidController.ActiveSlot;

                phase = Phase.Intro;
                introTimer = 0f;
                resultShown = false;
                activeRound = null;
                teamAnimationComplete = false;
                bossAnimationComplete = false;
                bossResponseRequested = false;
                presentationCompletionRequested = false;
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBGM(BgmType.RaidBattle);
                    AudioManager.Instance.PlaySFX(SfxType.BossAppear);
                }

                InsectEntity bossEnt = raidController.BossEntity;
                Vector3 bossWorldPos = bossEnt != null ? bossEnt.transform.position : Vector3.zero;

                // 3D 레이드 아레나 생성 (보스 월드 위치 전달)
                if (arena != null)
                {
                    bool bossShiny3d = bossEnt != null && bossEnt.IsShiny;
                    InsectData bossData3d = bossEnt != null ? bossEnt.Data : null;
                    int bossLevel3d = bossEnt != null ? bossEnt.Level : 1;
                    InsectData[] teamData3d = raidController.TeamData;
                    int[] teamLevels3d = new int[count];
                    for (int tl = 0; tl < count; tl++)
                        teamLevels3d[tl] = raidController.TeamStats[tl] != null ? raidController.TeamStats[tl].Level : 1;
                    if (bossData3d != null)
                        arena.SetupRaidBattle(bossData3d, bossLevel3d, bossShiny3d, teamData3d, teamLevels3d, bossWorldPos);
                }
                else if (cameraFollower != null && bossEnt != null)
                {
                    // playerMovement는 이미 AutoWire 주입됨. GameObject.Find 매 레이드 1회 호출 회귀 차단.
                    Vector3 pPos = playerMovement != null ? playerMovement.transform.position : Vector3.zero;
                    // 아레나가 있을 때(BattleArenaController.SetupBattleCamera)와 같은 정면 구도를 쓴다 —
                    // 좌표 쌍 오버로드는 측면에 카메라를 놓아 두 경로가 서로 다른 각도가 된다.
                    BattleArenaController.ComputeRaidCameraFraming(
                        pPos, bossWorldPos, out Vector3 raidCamPos, out Vector3 raidLookTarget);
                    cameraFollower.EnterBattleModeFramed(raidCamPos, raidLookTarget);
                }
                if (playerMovement != null) playerMovement.SetFrozen(true);
                return;
            }

            selectedSlot = raidController.ActiveSlot;
        }

        /// <summary>
        /// 팀원 <b>하나</b>가 행동을 마쳤다 — 그 곤충만의 공격을 연출한다.
        /// 라운드마다 생존 수만큼 들어오고, 연출이 끝나면 <c>Update</c>가 다음 차례(남은 팀원이
        /// 없으면 보스 턴)로 넘긴다. 예전엔 5마리분 볼리가 한 번에 나갔다.
        /// </summary>
        private void OnRaidMemberActionResolved(RaidActionResult action)
        {
            if (action == null) return;

            if (raidController != null) activeRound = raidController.CurrentRoundResult;
            lastMemberAction = action;
            AddSlotContribution(action);   // 라운드가 끝나기 전에 한 마리씩 쌓인다
            if (action.SourceSlot >= 0) selectedSlot = action.SourceSlot;
            lastDmgToBoss = action.Damage;
            lastDmgToTeam = 0;
            lastAoe = false;
            lastHitSlot = -1;
            lastSkillUsedName = !string.IsNullOrEmpty(action.DisplayName) ? action.DisplayName : "공격";
            actionText = raidController != null && !string.IsNullOrEmpty(raidController.LastActionText)
                ? raidController.LastActionText
                : lastSkillUsedName;
            actionTimer = 1.6f;
            if (action.Damage > 0) bossShake = 0.42f;

            teamAnimationComplete = arena == null || !arena.IsActive;
            bossAnimationComplete = false;
            bossResponseRequested = false;
            presentationCompletionRequested = false;
            phase = Phase.PlayerAttack;
            phaseTimer = 0f;
            uniteAnimTimer = 0f;

            if (arena == null || !arena.IsActive || action.SourceSlot < 0)
            {
                teamAnimationComplete = true;
                return;
            }

            arena.SetSelectedTeamIndex(action.SourceSlot);
            // 피해가 0인 행동(버프·회복·기절)도 그대로 연출한다 — 슬롯 하나짜리 볼리가
            // "이 곤충이 나섰다"를 보여준다. 예전 러시 경로는 0딜이면 통째로 걸러 버렸다.
            arena.PlayRaidVolley(
                new[] { action.SourceSlot },
                new[] { action.Element },
                () => { teamAnimationComplete = true; });
        }

        /// <summary>
        /// 팀 <b>전원</b>이 행동을 마쳐 보스 차례로 넘어간다. 일반 라운드의 연출은 이미
        /// <see cref="OnRaidMemberActionResolved"/>가 하나씩 돌렸으므로 여기서는 총합만 갱신한다 —
        /// <b>Phase를 건드리면 마지막 곤충의 연출이 중간에 잘린다.</b>
        /// 합체공격만 개별 이벤트 없이 여기로 바로 들어오므로 전용 연출을 여기서 시작한다.
        /// </summary>
        private void OnRaidTeamRushResolved(RaidRoundResult round)
        {
            if (round == null) return;

            activeRound = round;
            lastDmgToBoss = round.TotalDamageToBoss;

            // 일반 라운드의 기여 표시는 `AddSlotContribution`이 행동마다 이미 쌓아 두었다.
            // 합체공격만 개별 이벤트 없이 여기로 바로 들어오므로 여기서 한 번에 굽는다.
            if (!round.IsUnite) return;
            BuildSlotContributions(round);

            lastMemberAction = null;   // 합체는 특정 한 마리의 행동이 아니다
            if (round.LeaderSlot >= 0) selectedSlot = round.LeaderSlot;
            lastDmgToTeam = 0;
            lastAoe = false;
            lastHitSlot = -1;
            teamAnimationComplete = arena == null || !arena.IsActive;
            bossAnimationComplete = false;
            bossResponseRequested = false;
            presentationCompletionRequested = false;
            phaseTimer = 0f;
            uniteAnimTimer = 0f;
            phase = Phase.UniteAttack;
            lastSkillUsedName = "합체공격";
            actionText = $"★ 전원 합체공격! TOTAL {round.TotalDamageToBoss}";
            actionTimer = 2f;
            if (lastDmgToBoss > 0) bossShake = 0.42f;

            if (arena == null || !arena.IsActive) return;

            arena.PlayUniteAttackAnimation(() =>
            {
                teamAnimationComplete = true;
                if (cameraFollower != null) cameraFollower.Shake(0.5f, 0.6f);
            });
        }

        private void OnRaidBossResponseResolved(RaidRoundResult round)
        {
            if (round == null) return;

            activeRound = round;
            RaidActionResult bossAction = round.BossAction;
            lastDmgToTeam = round.TotalDamageToTeam;
            lastAoe = bossAction != null && bossAction.Kind == RaidActionKind.BossArea;
            lastHitSlot = bossAction != null ? bossAction.TargetSlot : -1;
            actionText = bossAction != null && !string.IsNullOrEmpty(bossAction.DisplayName)
                ? bossAction.DisplayName
                : round.BossResponseSkipped ? "보스가 기절해 움직이지 못한다!" : "";
            actionTimer = 2f;
            bossAnimationComplete = arena == null || !arena.IsActive
                || round.BossResponseSkipped || bossAction == null;
            presentationCompletionRequested = false;
            phase = Phase.BossAttack;
            phaseTimer = 0f;

            if (teamShake != null)
            {
                for (int i = 0; i < round.BossDamageBySlot.Length && i < teamShake.Length; i++)
                    if (round.BossDamageBySlot[i] > 0)
                        teamShake[i] = 0.42f;
            }

            if (bossAnimationComplete) return;

            arena.PlayRaidBossAttack(
                bossAction.Element,
                lastAoe,
                lastHitSlot,
                () => { bossAnimationComplete = true; });
        }

        private void OnRaidRoundCompleted(RaidRoundResult round)
        {
            activeRound = null;
            lastMemberAction = null;
            slotContribText = null;   // 다음 라운드에 지난 값이 새지 않게
            slotHealText = null;      // 회복 표시도 함께 — 한쪽만 비우면 +N만 남아 떠돈다
            autoPilotRemaining = false;   // 자동 위임은 라운드 단위 — 다음 라운드는 다시 직접 조작부터
            bossResponseRequested = false;
            presentationCompletionRequested = false;
            teamAnimationComplete = false;
            bossAnimationComplete = false;
            selectedSlot = raidController != null ? raidController.ActiveSlot : -1;
            if (!resultShown)
            {
                // 라운드가 끝나 조작이 돌아오는 순간에만 "팀의 턴" 배너를 끼운다. 라운드 **안**에서
                // 곤충이 하나씩 넘어가는 자리(연출 → 다음 SelectSkill)에는 넣지 않는다 —
                // 팀원 수만큼 배너가 뜨면 라운드가 배너로 도배된다.
                phase = Phase.TeamTurnAnnounce;
                phaseTimer = 0f;
                announceTimer = TeamTurnAnnounceDuration;
            }
        }

        private static RaidActionResult FindLeaderAction(RaidRoundResult round)
        {
            if (round == null) return null;
            foreach (RaidActionResult action in round.TeamActions)
                if (action != null && action.IsLeader)
                    return action;
            return null;
        }

        private void OnRaidEnded(bool playerWon)
        {
            resultShown = true;
            resultTimer = 0f;
            phase = Phase.Result;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(playerWon ? SfxType.Victory : SfxType.Defeat);
                AudioManager.Instance.PlayBGM(playerWon ? BgmType.Victory : BgmType.Defeat);
            }
            if (playerWon)
            {
                // 레이드 진행은 TutorialQuestManager가 raidController.RaidEnded를 직접 구독(OnRaidEnded)해
                // 처리 — 여기서 또 NotifyRaidCompleted를 부르면 1승이 +2로 이중 카운트(제거).
                CheckRaidGuardianDefeat();
            }
        }

        private RegionManager cachedRegionMgr;

        // 레이드 승리로 Epic/Legendary 수문장을 처치한 경우에도 다음 리전을 해금한다.
        // (1v1은 BattleScreenUI.CheckGuardianDefeat가 처리하지만, CaptureChoiceUI가 Epic/Legendary는
        //  [B] 1v1을 숨기고 [R] 레이드만 제공하므로 4개 수문장이 레이드 전용 → 격파 등록 누락 = 진행 차단.)
        private void CheckRaidGuardianDefeat()
        {
            if (raidController == null || raidController.BossStats == null
                || raidController.BossStats.Data == null) return;
            if (cachedRegionMgr == null) cachedRegionMgr = FindFirstObjectByType<RegionManager>();
            RegionManager regionMgr = cachedRegionMgr;
            if (regionMgr == null || regionMgr.Regions == null) return;

            // 보스 종/레벨은 시작 스냅샷(BossStats) — 디스폰/풀 재사용된 라이브 BossEntity 회피.
            string bossId = raidController.BossStats.Data.insectId;
            int bossLevel = raidController.BossStats.Level;
            foreach (var region in regionMgr.Regions)
            {
                if (string.IsNullOrEmpty(region.guardianInsectId)) continue;
                if (region.guardianInsectId != bossId) continue;
                if (regionMgr.IsGuardianDefeated(region.regionId)) continue;
                if (bossLevel >= region.guardianLevel - 2)
                {
                    regionMgr.DefeatGuardian(region.regionId);
                    Debug.Log($"[Guardian] {region.displayName} 수문장 격파(레이드)! 다음 지역 해금됨");
                    if (TutorialQuestManager.Instance != null)
                        TutorialQuestManager.Instance.NotifyGuardianDefeated();
                }
            }
        }

        private void Update()
        {
            if (phase == Phase.None) return;

            phaseTimer += Time.deltaTime;
            introTimer += Time.deltaTime;
            if (actionTimer > 0) actionTimer -= Time.deltaTime;
            if (bossShake > 0) bossShake -= Time.deltaTime;
            if (resultShown) resultTimer += Time.deltaTime;

            if (teamShake != null)
                for (int i = 0; i < teamShake.Length; i++)
                    if (teamShake[i] > 0) teamShake[i] -= Time.deltaTime;

            float hpSpeed = 80f * Time.deltaTime;
            if (raidController.BossStats != null)
                displayBossHp = Mathf.MoveTowards(displayBossHp, raidController.BossStats.CurrentHp, hpSpeed);
            if (raidController.TeamStats != null && displayTeamHp != null)
            {
                for (int i = 0; i < raidController.TeamStats.Length && i < displayTeamHp.Length; i++)
                    displayTeamHp[i] = Mathf.MoveTowards(displayTeamHp[i], raidController.TeamStats[i].CurrentHp, hpSpeed);
            }

            // 레이드 BGM 인텐시티: 보스 HP 낮을수록 상승
            if (AudioManager.Instance != null && raidController.BossStats != null && raidController.BossStats.MaxHp > 0)
            {
                float bossHpRatio = (float)raidController.BossStats.CurrentHp / raidController.BossStats.MaxHp;
                float intensity = Mathf.Clamp01((0.6f - bossHpRatio) * 1.7f);
                AudioManager.Instance.SetBattleIntensity(intensity);
            }

            if (phase == Phase.Intro && introTimer > 2f)
            {
                phase = Phase.SelectSkill;
                phaseTimer = 0f;
                if (raidController != null) selectedSlot = raidController.ActiveSlot;
            }

            if (phase == Phase.SelectSkill && raidController != null && raidController.IsActive)
            {
                // 차례는 컨트롤러가 들고 있다 — 화면은 그걸 따라간다(선택 UI가 따로 없다).
                if (raidController.ActiveSlot >= 0) selectedSlot = raidController.ActiveSlot;

                if (wantSkillQ || Input.GetKeyDown(KeyCode.Q)) TryUseRaidSkill(0);
                else if (wantSkillW || Input.GetKeyDown(KeyCode.W)) TryUseRaidSkill(1);
                else if (wantSkillE || Input.GetKeyDown(KeyCode.E)) TryUseRaidSkill(2);
                else if (wantSkillR || Input.GetKeyDown(KeyCode.R)) TryUseRaidSkill(3);
                wantSkillQ = wantSkillW = wantSkillE = wantSkillR = false;

                if (wantAuto || Input.GetKeyDown(KeyCode.A))
                {
                    TryAutoOne();
                    wantAuto = false;
                }
                else if (wantAutoAll || Input.GetKeyDown(KeyCode.S))
                {
                    TryAutoAll();
                    wantAutoAll = false;
                }

                if (wantUnite || Input.GetKeyDown(KeyCode.F))
                {
                    TryUnite();
                    wantUnite = false;
                }

                if (wantMouseClick || Input.GetMouseButtonDown(0))
                {
                    Vector2 mp = wantMouseClick ? guiMousePos :
                        UIScale.VirtualMousePosition;

                    // 스탠스 칩이 먼저다 — 차례를 소비하지 않는 입력이므로 소비 순서를 앞에 둔다.
                    if (!TryHandleStanceClick(mp))
                    {
                        if (uniteBtnVisible && uniteBtnRect.Contains(mp) && raidController.CanUniteAttack)
                        {
                            TryUnite();
                        }
                        else if (autoBtnVisible && autoBtnRect.Contains(mp))
                        {
                            TryAutoOne();
                        }
                        else if (autoBtnVisible && autoAllBtnRect.Contains(mp))
                        {
                            TryAutoAll();
                        }
                        else
                        {
                            for (int i = 0; i < raidSkillCount; i++)
                            {
                                if (raidSkillUsable[i] && raidSkillRects[i].Contains(mp))
                                {
                                    TryUseRaidSkill(i);
                                    break;
                                }
                            }
                        }
                    }
                    wantMouseClick = false;
                }
            }

            if (phase == Phase.UniteAttack || phase == Phase.PlayerAttack)
            {
                if (phase == Phase.UniteAttack)
                    uniteAnimTimer += Time.deltaTime;
                float minDuration = phase == Phase.UniteAttack
                    ? UniteRushMinDuration
                    : TeamRushMinDuration;
                bool animationReady = teamAnimationComplete || phaseTimer > 2.4f;
                if (animationReady && phaseTimer >= minDuration)
                {
                    // 순서가 중요하다: **남은 팀원이 먼저다.** 아직 행동하지 않은 곤충이 있으면
                    // 보스 턴이 아니라 다음 곤충의 스킬 화면으로 돌아간다.
                    if (raidController.CanSubmitTeamCommand)
                    {
                        if (autoPilotRemaining)
                        {
                            TryAutoOne();   // 전원 자동 — 다음 한 마리의 연출로 곧바로 이어진다
                        }
                        else
                        {
                            phase = Phase.SelectSkill;
                            phaseTimer = 0f;
                            selectedSlot = raidController.ActiveSlot;
                        }
                    }
                    else if (raidController.IsAwaitingBossResponse)
                    {
                        phase = Phase.BossTelegraph;
                        phaseTimer = 0f;
                        bossResponseRequested = false;
                    }
                    else
                    {
                        TryCompleteRoundPresentation();
                    }
                }
            }

            if (phase == Phase.BossTelegraph
                && phaseTimer >= BossTelegraphDuration
                && !bossResponseRequested)
            {
                bossResponseRequested = true;
                RaidRoundResult resolved = raidController.ResolveBossResponse();
                if (resolved == null && raidController.IsAwaitingPresentationCompletion)
                    TryCompleteRoundPresentation();
            }

            if (phase == Phase.BossAttack)
            {
                bool animationReady = bossAnimationComplete || phaseTimer > 2.4f;
                if (animationReady && phaseTimer >= BossImpactMinDuration)
                    TryCompleteRoundPresentation();
            }

            if (phase == Phase.TeamTurnAnnounce)
            {
                announceTimer -= Time.deltaTime;
                if (wantMouseClick) announceTimer = 0f;   // 탭으로 즉시 스킵(소거는 아래 말미가 담당)
                if (announceTimer <= 0f)
                {
                    phase = Phase.SelectSkill;
                    phaseTimer = 0f;
                    if (raidController != null) selectedSlot = raidController.ActiveSlot;
                }
            }

            if (phase == Phase.Result && resultTimer > 5f)
                EndRaid();

            // 탭 래치 차단 — `Input.GetMouseButtonDown`과 같은 한 프레임 수명으로 맞춘다.
            // `wantMouseClick`은 OnGUI(:MouseDown)가 세우는데 소거를 조작 분기 **안**에서만 하면,
            // 연출·인트로·결과 중에 화면을 탭한 값이 true로 남아 다음 조작 프레임의 첫 패스에서
            // 지난 라운드 Rect로 소비된다(스킬을 보기도 전에 하나가 눌린다). 순차 턴은 라운드마다
            // 조작 지점이 팀원 수만큼 생겨 이 창이 더 자주 열리므로 여기서 매 프레임 비운다.
            wantMouseClick = false;
        }

        private void TryCompleteRoundPresentation()
        {
            if (presentationCompletionRequested || raidController == null) return;
            if (!raidController.IsAwaitingPresentationCompletion) return;

            presentationCompletionRequested = true;
            raidController.CompleteRoundPresentation();
        }

        /// <summary>
        /// 스탠스 칩 클릭 처리. <b>라운드를 소비하지 않는다</b> — 성향만 바꾸고 곤충 선택은 그대로 남는다.
        /// </summary>
        private bool TryHandleStanceClick(Vector2 mouse)
        {
            if (raidController == null) return false;
            for (int i = 0; i < stanceRects.Length && i < StanceOrder.Length; i++)
            {
                if (stanceRects[i].width <= 0f || !stanceRects[i].Contains(mouse)) continue;
                raidController.SetStance(StanceOrder[i]);
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.ButtonClick);
                return true;
            }

            return false;
        }

        private void TryUseRaidSkill(int index)
        {
            if (raidController == null || !raidController.CanUseSkill(index)) return;

            autoPilotRemaining = false;   // 직접 골랐으면 자동 흘려보내기는 거기서 끝
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.SkillUse);
            // 차례의 단일 출처는 컨트롤러의 ActiveSlot이다 — 화면 캐시(selectedSlot)로 스킬을 찾으면
            // 두 값이 어긋난 프레임에 엉뚱한 곤충의 기술명이 뜬다.
            int slot = raidController.ActiveSlot;
            var skills = raidController.TeamSkills != null
                && slot >= 0 && slot < raidController.TeamSkills.Length
                ? raidController.TeamSkills[slot] : null;
            InsectSkill skill = skills != null && index < skills.Length ? skills[index] : null;
            lastSkillUsedName = skill != null ? skill.displayName : "공격";

            raidController.ResolveTeamCommand(index);
        }

        /// <summary>지금 차례인 곤충 하나를 AI에게 맡긴다.</summary>
        private void TryAutoOne()
        {
            if (raidController == null || !raidController.CanSubmitTeamCommand)
            {
                autoPilotRemaining = false;
                return;
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.SkillUse);
            raidController.ResolveAutoCommand();
        }

        /// <summary>
        /// 남은 팀원 전부를 AI에게 맡긴다 — 개편 전의 "리더만 고르면 나머지 자동"과 같은 자리다.
        /// <b>컨트롤러의 <c>ResolveAutoRemaining</c>을 부르지 않는다</b>: 그쪽은 한 프레임에 슬롯을
        /// 연달아 소비해서 팀원 수만큼의 볼리 코루틴이 겹쳐 돈다. 여기서는 플래그만 세우고
        /// 연출이 하나 끝날 때마다 다음 한 마리를 흘려보낸다.
        /// </summary>
        private void TryAutoAll()
        {
            if (raidController == null || !raidController.CanSubmitTeamCommand) return;
            autoPilotRemaining = true;
            TryAutoOne();
        }

        private void TryUnite()
        {
            if (raidController == null || !raidController.CanUniteAttack) return;
            autoPilotRemaining = false;
            lastSkillUsedName = "합체공격";
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.UniteAttack);
            raidController.ResolveUniteCommand();
        }

        // 보스 공격 연출 발동(`TriggerBossAttackEffect`)도 여기 있었다. `FinishTurnAnnounce`가 유일한
        // 호출부라 위 배너와 함께 죽었고, 그 안의 `arena.SetBossAttackTargetSlot(lastHitSlot)`도 같이 죽었다.
        // 기능 손실은 없다 — 새 경로 `OnRaidBossResponseResolved`가 `arena.PlayRaidBossAttack(..., lastHitSlot, ...)`로
        // 피격 슬롯을 인자로 직접 넘긴다. 즉 대상 지정이 필드에서 매개변수로 옮겨간 것뿐이다.

        private void OnGUI()
        {
            if (phase == Phase.None) return;

            UIScale.Begin();
            InitStyles();
            Event evt = Event.current;
            if (evt != null && evt.type == EventType.KeyDown)
            {
                // 곤충 선택([1-5])은 없어졌다 — 차례가 슬롯 순서로 정해지므로 키가 할 일이 없다.
                switch (evt.keyCode)
                {
                    case KeyCode.Q:
                        if (phase == Phase.SelectSkill) wantSkillQ = true;
                        evt.Use(); break;
                    case KeyCode.W:
                        if (phase == Phase.SelectSkill) wantSkillW = true;
                        evt.Use(); break;
                    case KeyCode.E:
                        if (phase == Phase.SelectSkill) wantSkillE = true;
                        evt.Use(); break;
                    case KeyCode.R:
                        if (phase == Phase.SelectSkill) wantSkillR = true;
                        evt.Use(); break;
                    case KeyCode.A:
                        if (phase == Phase.SelectSkill) wantAuto = true;
                        evt.Use(); break;
                    case KeyCode.S:
                        if (phase == Phase.SelectSkill) wantAutoAll = true;
                        evt.Use(); break;
                    case KeyCode.F:
                        if (phase == Phase.SelectSkill) wantUnite = true;
                        evt.Use(); break;
                }
            }

            if (evt != null && evt.type == EventType.MouseDown && evt.button == 0)
            {
                wantMouseClick = true;
                guiMousePos = evt.mousePosition;
            }

            DrawOverlay();
            DrawBossField();
            DrawTeamField();
            DrawBossHpBar();
            DrawTeamHpBars();
            DrawBossIntent();

            DrawUniteGaugeBar();

            if (phase == Phase.Intro)
                DrawIntro();
            else if (phase == Phase.SelectSkill)
                DrawSkillSelector();
            else if (phase == Phase.UniteAttack)
                DrawUniteAttackAnimation();
            else if (phase == Phase.PlayerAttack || phase == Phase.BossAttack)
                DrawAttackEffects();
            else if (phase == Phase.BossTelegraph)
                DrawBossTelegraph();
            else if (phase == Phase.TeamTurnAnnounce)
                DrawTeamTurnAnnounce();

            if (actionTimer > 0)
                DrawActionText();

            if (resultShown)
                DrawResult();

            UIScale.End();
        }


        private void EndRaid()
        {
            if (arena != null)
                arena.CleanupArena();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.RestoreExploreBGM();   // 있던 리전의 곡으로 (범용 Explore 아님)
                AudioManager.Instance.ClearBattleIntensity();
            }
            phase = Phase.None;
            // 재진입 누수 차단: 모든 phase 타이머 + 팀 캐시 초기화.
            // 옛은 phase=None만 세팅 → 다음 레이드 시작 첫 프레임에 resultTimer/uniteAnimTimer 잔존
            // → Intro 대기 시간 누락, Unite 애니메이션 스킵 등 타이밍 버그 + 옛 팀 캐시 참조 누수.
            phaseTimer = 0f;
            introTimer = 0f;
            resultTimer = 0f;
            uniteAnimTimer = 0f;
            announceTimer = 0f;
            displayTeamHp = null;
            teamShake = null;
            selectedSlot = -1;
            activeRound = null;
            lastMemberAction = null;
            slotContribText = null;
            slotHealText = null;
            teamAnimationComplete = false;
            bossAnimationComplete = false;
            bossResponseRequested = false;
            presentationCompletionRequested = false;
            // 입력 표면도 함께 비운다 — 남겨두면 지난 레이드의 버튼 Rect가 다음 레이드 첫 프레임의
            // 히트 테스트에 그대로 쓰인다(위 `wantMouseClick` 프레임 스코프화와 같은 결함 계열).
            raidSkillCount = 0;
            uniteBtnVisible = false;
            autoBtnVisible = false;
            autoPilotRemaining = false;
            wantMouseClick = false;
            for (int i = 0; i < stanceRects.Length; i++)
                stanceRects[i] = new Rect(0, 0, 0, 0);
            if (cameraFollower != null) cameraFollower.ExitBattleMode();
            if (playerMovement != null) playerMovement.SetFrozen(false);
        }


        public void AutoWire(RaidBattleController rc, CameraFollower cam, PlayerMovement pm = null)
        {
            if (raidController != null && raidController != rc)
            {
                raidController.RaidUpdated -= OnRaidUpdated;
                raidController.RaidEnded -= OnRaidEnded;
                raidController.RaidMemberActionResolved -= OnRaidMemberActionResolved;
                raidController.RaidTeamRushResolved -= OnRaidTeamRushResolved;
                raidController.RaidBossResponseResolved -= OnRaidBossResponseResolved;
                raidController.RaidRoundCompleted -= OnRaidRoundCompleted;
            }

            if (raidController == null || raidController != rc)
            {
                raidController = rc;
                SubscribeRaidController();
            }

            if (cameraFollower == null) cameraFollower = cam;
            if (playerMovement == null) playerMovement = pm;
        }

        /// <summary>
        /// 레이드 컨트롤러 구독. <b>AutoWire와 OnEnable이 공유한다</b>(해지 뒤 구독이라 중복 없음).
        /// BattleScreenUI와 같은 이유 — 오프닝 다시보기가 UI 루트를 토글하면 OnDisable이 해지한
        /// 구독을 되살릴 곳이 없어 레이드 화면이 영구히 열리지 않는다.
        /// </summary>
        private void SubscribeRaidController()
        {
            if (raidController == null) return;
            raidController.RaidUpdated -= OnRaidUpdated;
            raidController.RaidEnded -= OnRaidEnded;
            raidController.RaidMemberActionResolved -= OnRaidMemberActionResolved;
            raidController.RaidTeamRushResolved -= OnRaidTeamRushResolved;
            raidController.RaidBossResponseResolved -= OnRaidBossResponseResolved;
            raidController.RaidRoundCompleted -= OnRaidRoundCompleted;
            raidController.RaidUpdated += OnRaidUpdated;
            raidController.RaidEnded += OnRaidEnded;
            raidController.RaidMemberActionResolved += OnRaidMemberActionResolved;
            raidController.RaidTeamRushResolved += OnRaidTeamRushResolved;
            raidController.RaidBossResponseResolved += OnRaidBossResponseResolved;
            raidController.RaidRoundCompleted += OnRaidRoundCompleted;
        }

        public void AutoWire(BattleArenaController a)
        {
            if (arena == null) arena = a;
        }
    }
}
