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

        private enum Phase
        {
            None,
            Intro,
            SelectInsect,
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

        private Rect[] insectBtnRects = new Rect[5];
        private bool[] insectBtnUsable = new bool[5];
        private int insectBtnCount;
        private Rect[] raidSkillRects = new Rect[4];
        private bool[] raidSkillUsable = new bool[4];
        private int raidSkillCount;
        /// <summary>스탠스 칩 3개의 히트 영역. 그리는 쪽(<c>DrawStanceChips</c>)이 매 패스 채운다.</summary>
        private Rect[] stanceRects = new Rect[3];

        private bool wantInsect0, wantInsect1, wantInsect2, wantInsect3, wantInsect4;
        private bool wantSkillQ, wantSkillW, wantSkillE, wantSkillR;
        private bool wantEscape;
        private bool wantUnite;
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

        private void OnRaidTeamRushResolved(RaidRoundResult round)
        {
            if (round == null) return;

            activeRound = round;
            BuildSlotContributions(round);   // 라운드당 1회만 문자열을 굽는다(패스마다 만들지 않는다)
            selectedSlot = round.LeaderSlot >= 0 ? round.LeaderSlot : raidController.ActiveSlot;
            lastDmgToBoss = round.TotalDamageToBoss;
            lastDmgToTeam = 0;
            lastAoe = false;
            lastHitSlot = -1;
            teamAnimationComplete = arena == null || !arena.IsActive;
            bossAnimationComplete = false;
            bossResponseRequested = false;
            presentationCompletionRequested = false;
            phaseTimer = 0f;
            uniteAnimTimer = 0f;
            phase = round.IsUnite ? Phase.UniteAttack : Phase.PlayerAttack;

            RaidActionResult leader = FindLeaderAction(round);
            lastSkillUsedName = round.IsUnite
                ? "합체공격"
                : leader != null && !string.IsNullOrEmpty(leader.DisplayName)
                    ? leader.DisplayName
                    : "팀 러시";
            actionText = round.IsUnite
                ? $"★ 전원 합체공격! TOTAL {round.TotalDamageToBoss}"
                : $"TEAM RUSH ×{round.TeamActions.Count}  TOTAL {round.TotalDamageToBoss}";
            actionTimer = 2f;
            if (lastDmgToBoss > 0) bossShake = 0.42f;

            if (arena == null || !arena.IsActive)
                return;

            if (round.IsUnite)
            {
                arena.PlayUniteAttackAnimation(() =>
                {
                    teamAnimationComplete = true;
                    if (cameraFollower != null) cameraFollower.Shake(0.5f, 0.6f);
                });
                return;
            }

            if (leader != null && leader.Damage <= 0 && leader.SourceSlot >= 0)
            {
                arena.SetSelectedTeamIndex(leader.SourceSlot);
                arena.PlaySkillEffect(
                    true,
                    leader.Element,
                    leader.EffectType,
                    null,
                    BattleArenaController.IsMeleeElement(leader.Element));
            }

            List<int> slots = new List<int>();
            List<InsectElement> elements = new List<InsectElement>();
            foreach (RaidActionResult action in round.TeamActions)
            {
                if (action == null || action.SourceSlot < 0) continue;
                if (action.Damage <= 0 && !action.IsSupport) continue;
                slots.Add(action.SourceSlot);
                elements.Add(action.Element);
            }

            if (slots.Count == 0)
            {
                teamAnimationComplete = true;
                return;
            }

            arena.PlayRaidVolley(slots.ToArray(), elements.ToArray(), () =>
            {
                teamAnimationComplete = true;
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
            slotContribText = null;   // 다음 라운드에 지난 값이 새지 않게
            bossResponseRequested = false;
            presentationCompletionRequested = false;
            teamAnimationComplete = false;
            bossAnimationComplete = false;
            selectedSlot = raidController != null ? raidController.ActiveSlot : -1;
            if (!resultShown)
            {
                // 조작이 돌아오는 순간에만 "팀의 턴" 배너를 끼운다(Intro→SelectInsect와 SelectSkill의
                // Escape 복귀는 배너 없이 즉시 — 각각 FIGHT! 연출이 있고, 취소는 즉각 반응이 맞다).
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
                phase = Phase.SelectInsect;
                phaseTimer = 0f;
            }

            if (phase == Phase.SelectInsect && raidController != null && raidController.IsActive)
            {
                int idx = -1;
                if (wantInsect0 || Input.GetKeyDown(KeyCode.Alpha1)) idx = 0;
                else if (wantInsect1 || Input.GetKeyDown(KeyCode.Alpha2)) idx = 1;
                else if (wantInsect2 || Input.GetKeyDown(KeyCode.Alpha3)) idx = 2;
                else if (wantInsect3 || Input.GetKeyDown(KeyCode.Alpha4)) idx = 3;
                else if (wantInsect4 || Input.GetKeyDown(KeyCode.Alpha5)) idx = 4;
                wantInsect0 = wantInsect1 = wantInsect2 = wantInsect3 = wantInsect4 = false;
                if (idx >= 0) TrySelectInsect(idx);

                if (wantUnite || Input.GetKeyDown(KeyCode.F))
                {
                    if (raidController.CanUniteAttack)
                    {
                        lastSkillUsedName = "합체공격";
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.UniteAttack);
                        raidController.ResolveUniteCommand();
                    }
                    wantUnite = false;
                }

                if (wantMouseClick || Input.GetMouseButtonDown(0))
                {
                    Vector2 mp = wantMouseClick ? guiMousePos :
                        UIScale.VirtualMousePosition;

                    // 스탠스 칩이 곤충 버튼보다 먼저다 — 헤더 줄에 있어 서로 겹치지 않지만,
                    // 라운드를 소비하지 않는 입력이므로 소비 순서를 앞에 둔다.
                    if (!TryHandleStanceClick(mp))
                    {
                        if (uniteBtnVisible && uniteBtnRect.Contains(mp) && raidController.CanUniteAttack)
                        {
                            lastSkillUsedName = "합체공격";
                            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.UniteAttack);
                            raidController.ResolveUniteCommand();
                        }
                        else
                        {
                            for (int i = 0; i < insectBtnCount; i++)
                            {
                                if (insectBtnUsable[i] && insectBtnRects[i].Contains(mp))
                                {
                                    TrySelectInsect(i);
                                    break;
                                }
                            }
                        }
                    }
                    wantMouseClick = false;
                }
            }
            else if (phase == Phase.SelectSkill && raidController != null && raidController.IsActive)
            {
                if (wantSkillQ || Input.GetKeyDown(KeyCode.Q)) TryUseRaidSkill(0);
                else if (wantSkillW || Input.GetKeyDown(KeyCode.W)) TryUseRaidSkill(1);
                else if (wantSkillE || Input.GetKeyDown(KeyCode.E)) TryUseRaidSkill(2);
                else if (wantSkillR || Input.GetKeyDown(KeyCode.R)) TryUseRaidSkill(3);
                wantSkillQ = wantSkillW = wantSkillE = wantSkillR = false;

                if (wantUnite || Input.GetKeyDown(KeyCode.F))
                {
                    if (raidController.CanUniteAttack)
                    {
                        lastSkillUsedName = "합체공격";
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.UniteAttack);
                        raidController.ResolveUniteCommand();
                    }
                    wantUnite = false;
                }

                if (wantEscape || Input.GetKeyDown(KeyCode.Escape))
                {
                    phase = Phase.SelectInsect;
                    phaseTimer = 0f;
                    wantEscape = false;
                }

                if (wantMouseClick || Input.GetMouseButtonDown(0))
                {
                    Vector2 mp = wantMouseClick ? guiMousePos :
                        UIScale.VirtualMousePosition;

                    if (uniteBtnVisible && uniteBtnRect.Contains(mp) && raidController.CanUniteAttack)
                    {
                        lastSkillUsedName = "합체공격";
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.UniteAttack);
                        raidController.ResolveUniteCommand();
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
                    if (raidController.IsAwaitingBossResponse)
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
                    phase = Phase.SelectInsect;
                    phaseTimer = 0f;
                }
            }

            if (phase == Phase.Result && resultTimer > 5f)
                EndRaid();

            // 탭 래치 차단 — `Input.GetMouseButtonDown`과 같은 한 프레임 수명으로 맞춘다.
            // `wantMouseClick`은 OnGUI(:MouseDown)가 세우는데 소거는 SelectInsect/SelectSkill 분기
            // **안**에서만 했다. 그래서 Intro·PlayerAttack·BossTelegraph·BossAttack·UniteAttack·Result에서
            // 화면을 탭하면 true로 남았다가, 다음 라운드가 SelectInsect로 들어오는 첫 프레임에
            // 지난 라운드의 `insectBtnRects`로 소비돼 **선택 패널을 보기도 전에 곤충이 골라지고**
            // SelectSkill로 건너뛰었다. 유일하게 중간에서 비워주던 자리가 위에서 지운 TurnAnnounce였다.
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

        private void TrySelectInsect(int index)
        {
            if (raidController.TeamStats != null && index < raidController.TeamStats.Length
                && raidController.TeamStats[index].CurrentHp > 0)
            {
                selectedSlot = index;
                raidController.SelectSlot(index);
                phase = Phase.SelectSkill;
                phaseTimer = 0f;
            }
        }

        private void TryUseRaidSkill(int index)
        {
            if (raidController.CanUseSkill(index))
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.SkillUse);
                var skills = raidController.TeamSkills != null && selectedSlot < raidController.TeamSkills.Length
                    ? raidController.TeamSkills[selectedSlot] : null;
                InsectSkill skill = skills != null && index < skills.Length ? skills[index] : null;
                lastSkillUsedName = skill != null ? skill.displayName : "공격";

                raidController.ResolveTeamCommand(index);
            }
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
                switch (evt.keyCode)
                {
                    case KeyCode.Alpha1: case KeyCode.Keypad1:
                        if (phase == Phase.SelectInsect) wantInsect0 = true;
                        evt.Use(); break;
                    case KeyCode.Alpha2: case KeyCode.Keypad2:
                        if (phase == Phase.SelectInsect) wantInsect1 = true;
                        evt.Use(); break;
                    case KeyCode.Alpha3: case KeyCode.Keypad3:
                        if (phase == Phase.SelectInsect) wantInsect2 = true;
                        evt.Use(); break;
                    case KeyCode.Alpha4: case KeyCode.Keypad4:
                        if (phase == Phase.SelectInsect) wantInsect3 = true;
                        evt.Use(); break;
                    case KeyCode.Alpha5: case KeyCode.Keypad5:
                        if (phase == Phase.SelectInsect) wantInsect4 = true;
                        evt.Use(); break;
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
                    case KeyCode.F:
                        if (phase == Phase.SelectSkill || phase == Phase.SelectInsect) wantUnite = true;
                        evt.Use(); break;
                    case KeyCode.Escape:
                        if (phase == Phase.SelectSkill) wantEscape = true;
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
            else if (phase == Phase.SelectInsect)
                DrawInsectSelector();
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
                AudioManager.Instance.PlayBGM(BgmType.Explore);
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
            teamAnimationComplete = false;
            bossAnimationComplete = false;
            bossResponseRequested = false;
            presentationCompletionRequested = false;
            // 입력 표면도 함께 비운다 — 남겨두면 지난 레이드의 버튼 Rect가 다음 레이드 첫 프레임의
            // 히트 테스트에 그대로 쓰인다(위 `wantMouseClick` 프레임 스코프화와 같은 결함 계열).
            insectBtnCount = 0;
            raidSkillCount = 0;
            uniteBtnVisible = false;
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
            raidController.RaidTeamRushResolved -= OnRaidTeamRushResolved;
            raidController.RaidBossResponseResolved -= OnRaidBossResponseResolved;
            raidController.RaidRoundCompleted -= OnRaidRoundCompleted;
            raidController.RaidUpdated += OnRaidUpdated;
            raidController.RaidEnded += OnRaidEnded;
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
