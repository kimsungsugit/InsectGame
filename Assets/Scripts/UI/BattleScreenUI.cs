using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;
using System.Collections.Generic;

namespace InsectGame.UI
{
    public partial class BattleScreenUI : MonoBehaviour
    {
        [SerializeField] private InsectBattleController battleController;
        [SerializeField] private CameraFollower cameraFollower;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private BattleTeamManager teamManager;
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private TrainingManager trainingManager;
        [SerializeField] private BattleArenaController arena;

        private enum Phase { None, Intro, PlayerTurn, PlayerAttack, EnemyAttack, TurnAnnounce, SwapSelect, Result }

        private Phase phase = Phase.None;
        public bool IsBattleActive => phase != Phase.None;

        private InsectBattleStats playerStats;
        private InsectBattleStats enemyStats;
        private InsectBattleStats prevPlayerStats;
        private InsectBattleStats prevEnemyStats;

        private Vector3 battlePlayerOrigin;
        private Vector3 battleEnemyOrigin;
        private bool battleEnemyShiny;
        private bool hasArenaSnapshot;

        private void RestoreArenaAfterEnable()
        {
            if (!hasArenaSnapshot || phase == Phase.None || phase == Phase.Result || arena == null
                || !arena.isActiveAndEnabled || arena.IsActive || playerStats == null || enemyStats == null) return;
            arena.SetupNormalBattle(playerStats.Data, playerStats.Level, enemyStats.Data, enemyStats.Level,
                battleEnemyShiny, battlePlayerOrigin, battleEnemyOrigin);
        }

        private float phaseTimer;
        private float attackDuration = 1.8f;
        private bool impactRevealed;
        private float impactTimer;
        private bool faintStarted;
        private float faintTimer;

        private void RevealImpact(bool playerAction)
        {
            if (impactRevealed) return;
            impactRevealed = true;
            impactTimer = 0f;
            if (playerAction && battleController != null)
            {
                revealPlayerHp = battleController.PlayerHpAfterPlayerAction;
                revealEnemyHp = battleController.EnemyHpAfterPlayerAction;
            }
            else
            {
                if (battleController != null)
                {
                    revealPlayerHp = battleController.PlayerHpAfterEnemyAction;
                    revealEnemyHp = battleController.EnemyHpAfterEnemyAction;
                }
            }
            battleController?.PresentResolvedAction(playerAction);
            int hitDamage = playerAction ? lastDamageToEnemy : lastDamageToPlayer;
            if (hitDamage > 0 && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(playerAction && lastWasCritical ? SfxType.CriticalHit : SfxType.Hit);
            if (playerAction && lastWasCritical && !BattlePresentation.ReducedFlashes)
            {
                screenFlashTimer = 0.18f;
                screenFlashColor = new Color(1f, 0.95f, 0.3f);
            }
        }

        private bool FinishFaintPresentation()
        {
            // Residual damage/healing is a round-end step, distinct from either attack's impact.
            if (playerStats != null) revealPlayerHp = playerStats.CurrentHp;
            if (enemyStats != null) revealEnemyHp = enemyStats.CurrentHp;
            if (!pendingResult && !pendingSwap) return true;
            bool playerFainted = playerStats != null && playerStats.CurrentHp <= 0;
            bool enemyFainted = enemyStats != null && enemyStats.CurrentHp <= 0;
            if (!playerFainted && !enemyFainted) return true;
            if (!faintStarted)
            {
                faintStarted = true;
                faintTimer = 0f;
                if (arena != null && arena.IsActive)
                {
                    GameObject model = playerFainted ? arena.PlayerModel : arena.EnemyModel;
                    if (model != null) StartCoroutine(arena.PlayFaintCoroutine(model));
                }
            }
            return faintTimer >= 0.7f;
        }
        private float introTimer;
        private string actionText;
        private float actionTimer;
        // 인터-턴 배너(턴 가시성) — 다음 페이즈 전 "당신의 턴/상대의 턴"을 중앙에 표시하는 대기.
        private const float TurnAnnounceDuration = 0.9f;
        private float announceTimer;
        private string announceText;
        private bool announceIsPlayer;
        private Phase announceNextPhase;   // 배너 종료 후 전이할 페이즈
        private bool announceTriggerEnemy; // 종료 후 EnemyAttack 연출을 발동할지
        private int lastDamageToEnemy;
        private bool lastWasCritical;
        private int comboCount;
        private float comboDisplayTimer;
        private float screenFlashTimer;
        private Color screenFlashColor;

        private GUIStyle cachedComboNumStyle;
        private GUIStyle cachedComboLblStyle;

        // OnGUI 매 프레임 호출되는 GUIStyle 캐싱 (PlayerStatusHUD 패턴)
        private bool stylesInitialized;
        private GUIStyle turnStyle3dCache;
        private GUIStyle turnStyleCache;
        private GUIStyle playerLabelCache;
        private GUIStyle playerTagCache;
        private GUIStyle enemyLabelCache;
        private GUIStyle enemyTagCache;
        private GUIStyle nameTagCache;
        private GUIStyle hpNameStyleCache;
        private GUIStyle hpLvStyleCache;
        private GUIStyle hpMiniStatCache;
        private GUIStyle hpTextCache;
        private GUIStyle hpEffStyleCache;
        private GUIStyle introVsStyleCache;
        private GUIStyle introPNameStyleCache;
        private GUIStyle introENameStyleCache;
        private GUIStyle introFightStyleCache;
        private GUIStyle introEncounterStyleCache;
        private GUIStyle skillHeaderCache;
        private GUIStyle skillKeyNumCache;
        private GUIStyle skillNameStyleCache;
        private GUIStyle skillTypeLabelCache;
        private GUIStyle skillEffLabelCache;   // 상성 배지(효과적/비효과)
        private GUIStyle skillInfoStyleCache;
        private GUIStyle skillCdStyleCache;
        private GUIStyle skillCdInfoCache;
        private GUIStyle skillFKeyCache;
        private GUIStyle skillFInfoCache;
        private GUIStyle skillEscStyleCache;
        private GUIStyle skillEscInfoCache;
        private GUIStyle dmgStyle3dCache;
        private GUIStyle critLblCache;
        private GUIStyle skillStyle3dCache;
        private GUIStyle skillNameAtkStyleCache;
        private GUIStyle dmgStyleAtkCache;
        private GUIStyle effStyleAtkCache;
        private GUIStyle buffDebuffSkillStyleCache;
        private GUIStyle upStyleCache;
        private GUIStyle downStyleCache;
        private GUIStyle actionTextStyleCache;
        private GUIStyle victoryStyleCache;
        private GUIStyle rewardStyleCache;
        private GUIStyle rewardValStyleCache;
        private GUIStyle defeatStyleCache;
        private GUIStyle defeatGuideStyleCache;
        private GUIStyle defeatHintStyleCache;
        private GUIStyle phaseIndicatorStyleCache;
        private GUIStyle swapHeaderCache;
        private GUIStyle swapKeyStyleCache;
        private GUIStyle swapEmptyStyleCache;
        private GUIStyle swapNameStyleCache;
        private GUIStyle swapInfoStyleCache;
        private GUIStyle swapStatStyleCache;
        private GUIStyle swapFaintStyleCache;
        private GUIStyle swapCurStyleCache;

        private int lastDamageToPlayer;
        private string lastSkillName;
        private InsectElement lastSkillElement = InsectElement.Bug;

        private int savedEnemyHp;
        private int savedPlayerHp;
        private bool hpSnapshotTaken;

        private bool resultShown;
        private bool lastWon;
        private float resultTimer;

        private float playerShake;
        private float enemyShake;

        private float displayPlayerHp;
        private float displayEnemyHp;
        // 칩바(ghost) — displayHp보다 느리게 따라와 최근 피해량을 잔상으로 표시(Phase 1 저즈).
        private float chipPlayerHp;
        private float chipEnemyHp;
        // HP 리빌(P2) — displayHp는 CurrentHp가 아니라 이 값을 향해 tween. 아레나 연출 임팩트 순간 갱신해
        // HP 감소를 타격과 정렬. 아레나 없으면(2D 폴백) Update에서 즉시 CurrentHp로.
        private float revealPlayerHp;
        private float revealEnemyHp;
        // 종료 지연(P2) — 마지막 공격 연출이 결과화면보다 먼저 보이도록. OnBattleEnded가 세팅, 페이즈 머신이 소비.
        private bool pendingResult;
        // 기절 교체 지연(P2) — 적의 치명타 연출이 끝난 뒤 교체창으로. OnPlayerFainted가 세팅.
        private bool pendingSwap;

        private int turnNumber;

        private Rect[] skillBtnRects = new Rect[4];
        private bool[] skillBtnUsable = new bool[4];
        private int skillBtnCount;
        private Rect basicAtkRect;
        private Rect escapeRect;

        private readonly HashSet<string> faintedInsectIds = new HashSet<string>();
        private string currentInsectId;
        private float swapMessageTimer;
        private Rect[] swapBtnRects = new Rect[5];
        private bool[] swapBtnAvail = new bool[5];

        private bool wantSkill0, wantSkill1, wantSkill2, wantSkill3;
        private bool wantBasicAtk, wantEscape;
        private bool wantSwap0, wantSwap1, wantSwap2, wantSwap3, wantSwap4;
        private bool wantMouseClick;
        private Vector2 guiMousePos;

        private void OnEnable()
        {
            // OnDisable이 해지한 구독을 되살린다 — 오프닝 다시보기가 UI 루트를 토글하므로
            // AutoWire 1회 구독만으로는 배틀 화면이 영구히 죽는다. 해지 뒤 구독이라 중복 없음.
            SubscribeBattleController();
        }

        private void OnDisable()
        {
            faintStarted = false;
            wantMouseClick = false;
            wantSkill0 = wantSkill1 = wantSkill2 = wantSkill3 = false;
            wantBasicAtk = wantEscape = false;
            if (battleController != null)
            {
                battleController.DeferPresentation = false;
                battleController.BattleUpdated -= OnBattleUpdated;
                battleController.BattleEnded -= OnBattleEnded;
                battleController.PlayerFainted -= OnPlayerFainted;
            }
        }

        private void OnPlayerFainted()
        {
            if (playerStats != null && playerStats.PlayerData != null)
                faintedInsectIds.Add(playerStats.PlayerData.instanceId);

            // 적의 치명타로 기절한 경우, 공격 연출(PlayerAttack/EnemyAttack) 중이면 연출이 끝난 뒤
            // 교체창/결과화면으로(적 킬블로가 먼저 보이게). 그 외엔 즉시.
            bool midAttack = phase == Phase.PlayerAttack || phase == Phase.EnemyAttack;
            if (HasAvailableTeamMember())
            {
                if (midAttack) pendingSwap = true;
                else EnterSwapSelect();
            }
            else
            {
                lastWon = false;
                if (midAttack) pendingResult = true;   // EnterResult가 패배 SFX/BGM까지 처리
                else EnterResult();
            }
        }

        private bool HasAvailableTeamMember()
        {
            if (teamManager == null || collection == null) return false;
            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                string slotId = teamManager.GetSlot(i);
                if (string.IsNullOrEmpty(slotId)) continue;
                if (faintedInsectIds.Contains(slotId)) continue;
                if (slotId == currentInsectId) continue;
                PlayerInsectData pid = collection.GetByInstanceId(slotId);
                if (pid != null && pid.IsFainted) continue;   // 지속 기절(0 HP)은 치료 전까지 출전 불가
                InsectData data = pid != null ? collection.GetInsectData(pid.insectId) : null;
                if (data != null) return true;
            }
            return false;
        }

        private void OnBattleUpdated(InsectBattleStats player, InsectBattleStats enemy)
        {
            if (phase == Phase.None)
            {
                playerStats = player;
                enemyStats = enemy;
                displayPlayerHp = player.CurrentHp;
                displayEnemyHp = enemy.CurrentHp;
                chipPlayerHp = player.CurrentHp;
                chipEnemyHp = enemy.CurrentHp;
                revealPlayerHp = player.CurrentHp;
                revealEnemyHp = enemy.CurrentHp;
                pendingResult = false;
                pendingSwap = false;
                announceTimer = 0f;
                turnNumber = 0;
                phase = Phase.Intro;
                introTimer = 0f;
                resultShown = false;
                faintedInsectIds.Clear();
                ResetDuelPresentation();
                if (player.PlayerData != null) currentInsectId = player.PlayerData.instanceId;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayBGM(BgmType.Battle);

                DisableCanvasBattleUI();

                InsectEntity enemyEntity = battleController.GetEnemyEntity();
                Vector3 pPos = playerMovement != null ? playerMovement.transform.position : Vector3.zero;
                Vector3 ePos = enemyEntity != null ? enemyEntity.transform.position : pPos + Vector3.forward * 5f;

                // 3D 아레나 생성 (월드 위치 전달)
                if (arena != null)
                {
                    bool enemyShiny3d = enemyEntity != null && enemyEntity.IsShiny;
                    battlePlayerOrigin = pPos;
                    battleEnemyOrigin = ePos;
                    battleEnemyShiny = enemyShiny3d;
                    hasArenaSnapshot = true;
                    arena.SetupNormalBattle(
                        player.Data, player.Level, enemy.Data, enemy.Level, enemyShiny3d,
                        pPos, ePos);
                }
                else if (cameraFollower != null && enemyEntity != null)
                {
                    // 3D 아레나 없으면 기존 카메라 모드
                    cameraFollower.EnterBattleMode(pPos, ePos);
                }
                if (playerMovement != null) playerMovement.SetFrozen(true);
                return;
            }

            if (phase == Phase.SwapSelect)
            {
                playerStats = player;
                enemyStats = enemy;
                displayPlayerHp = player.CurrentHp;
                chipPlayerHp = player.CurrentHp;
                revealPlayerHp = player.CurrentHp;
                if (player.PlayerData != null) currentInsectId = player.PlayerData.instanceId;
                phase = Phase.PlayerTurn;
                phaseTimer = 0f;
                actionText = $"{player.Data.displayName} 출격!";
                actionTimer = 1.5f;
                return;
            }

            prevPlayerStats = playerStats;
            prevEnemyStats = enemyStats;

            int oldEnemyHp = hpSnapshotTaken ? savedEnemyHp : (enemyStats != null ? enemyStats.CurrentHp : 0);
            hpSnapshotTaken = false;

            playerStats = player;
            enemyStats = enemy;

            lastDamageToEnemy = Mathf.Max(0, oldEnemyHp - battleController.EnemyHpAfterPlayerAction);
            lastDamageToPlayer = Mathf.Max(0, battleController.PlayerHpAfterPlayerAction - battleController.PlayerHpAfterEnemyAction);

            // 크리티컬 판정: 적 MaxHp의 25% 이상 데미지
            lastWasCritical = lastDamageToEnemy > 0 && enemy.MaxHp > 0 && lastDamageToEnemy >= enemy.MaxHp * 0.25f;

            if (lastDamageToEnemy > 0) comboCount++;
            if (lastDamageToPlayer > 0) comboCount = 0;

            phase = Phase.PlayerAttack;
            if (!battleController.PlayerActedThisRound)
            {
                attackDuration = 0.8f;
                actionText = "행동을 마쳤다";
                lastSkillName = string.Empty;
            }
            phaseTimer = 0f;
            impactRevealed = false;
            faintStarted = false;
        }

        private void OnBattleEnded(bool playerWon)
        {
            lastWon = playerWon;
            // 튜토리얼/서브 배틀 진행은 TutorialQuestManager가 battleController.BattleEnded를 직접
            // 구독(OnBattleEnded)해 처리한다 — 여기서 또 NotifyBattleWon을 부르면 1승이 +2로 이중 카운트.

            // 수문장 격파 체크
            if (playerWon)
                CheckGuardianDefeat();

            // 킬블로 연출 스포일 방지 — 공격 페이즈 중이면 연출이 끝난 뒤 결과화면으로(페이즈 머신이 EnterResult).
            // 그 외(도주 등 즉시 종료)엔 곧바로 결과화면. 승패 SFX/BGM은 EnterResult에서(연출 도중 스포일 방지).
            if (phase == Phase.PlayerAttack || phase == Phase.EnemyAttack)
                pendingResult = true;
            else
                EnterResult();
        }

        /// <summary>
        /// 이 승리가 수문장 격파인가 — <b>싸운 개체가 그 수문장이었는지</b>로만 답한다.
        ///
        /// <b>종·레벨로는 판정할 수 없다.</b> 리전 13곳 중 9곳은 수문장 종이 <b>자기 리전
        /// 야생 풀에도</b> 들어 있고(pond·garden·ruins·hollow·dunes·frostline·emberfall·
        /// canopy·nameless), 그중 8곳은 야생 스폰 상한(<c>requiredLevel + GetRegionLevelRange</c>)이
        /// 옛 격파 임계(<c>guardianLevel - 2</c>)를 넘는다. 그래서 <b>필드에서 마주친 야생을 이기는
        /// 것만으로 리전이 열렸다</b> — 유적에서 야생 파라오풍뎅이 Lv40+를 이기면 2막 6리전이 도미노로.
        ///
        /// <b>좌표로도 판정할 수 없다.</b> 그 다음 시도가 "수문장 자리 15m 안에서 싸웠나"였는데,
        /// 그 반경엔 야생이 그대로 들어왔다: 당시 스포너는 현재 리전 스폰포인트를 <b>플레이어로부터 10~43m</b>
        /// 나선 위로 끌어오고 거기서 다시 5m만큼 흩어서 <b>최근접 스폰이 플레이어에서 5m</b>였다.
        /// 지금은 야생이 리전 원판 전체에 기록돼 흩어지지만 수문장 앞을 비켜 두지 않는다 —
        /// 수문장과 싸우려면 그 앞에 서야 하니 야생이 반경에 들어오는 건 우연이 아니라 구조이고,
        /// 위 9개 리전에서는 종·레벨 조건까지 동시에 맞는다. 반경을 좁혀도 같은 결함이 남는다.
        ///
        /// 그래서 <b>"바로 그 개체였나"</b>만 묻는다. 수문장은
        /// <c>PlaySceneBootstrap.SpawnGuardianInsect</c>가 <c>new GameObject</c>로 세우는 단
        /// 하나의 표식된 개체다(풀에서 오지 않는다). 값은
        /// <c>InsectBattleController.EnemyGuardianRegionId</c> — 시작 시점 스냅샷이라
        /// 승리 후 <c>Despawn</c>이 먼저 돌아도 안전하다.
        /// </summary>
        private void CheckGuardianDefeat()
        {
            if (battleController == null) return;

            // 스냅샷으로 판정한다 — 라이브 엔티티는 이미 디스폰됐을 수 있다.
            if (cachedRegionMgr == null) cachedRegionMgr = FindFirstObjectByType<RegionManager>();
            if (cachedRegionMgr == null) return;
            if (!cachedRegionMgr.TryDefeatGuardian(battleController.EnemyGuardianRegionId, "1v1")) return;

            if (TutorialQuestManager.Instance != null)
                TutorialQuestManager.Instance.NotifyGuardianDefeated();
        }

        // Both 2D and 3D honor the same action duration; a bounded grace period avoids a stuck coroutine.
        private bool PhaseAnimDone()
        {
            if (phaseTimer < attackDuration) return false;
            return arena == null || !arena.IsActive || !arena.IsPlayingSkill || phaseTimer > attackDuration + 1f;
        }

        // EnemyAttack 페이즈 진입 시 적 공격 연출 발동 — 적이 쓴 스킬 속성/근접여부로 러시·투사체·쉐이크.
        private void TriggerEnemyAttackEffect()
        {
            if (arena == null || !arena.IsActive || battleController == null) return;
            InsectSkill es = battleController.LastEnemySkill;
            InsectElement elem = es != null ? es.element
                : (enemyStats != null && enemyStats.Data != null ? enemyStats.Data.primaryType : InsectElement.Bug);
            SkillEffectType eff = es != null ? es.effectType : SkillEffectType.Damage;
            arena.PlaySkillEffect(false, elem, eff,
                () => { if (isActiveAndEnabled && phase == Phase.EnemyAttack) RevealImpact(false); },
                BattleArenaController.IsMeleeElement(elem), attackDuration,
                BuildHitCue(false, es != null ? es.displayName : null, eff));
        }

        // 적 치명타 연출이 끝난 뒤 교체창 진입.
        private void EnterSwapSelect()
        {
            phase = Phase.SwapSelect;
            phaseTimer = 0f;
            swapMessageTimer = 0f;
            pendingSwap = false;
        }

        // 마지막 공격 연출이 끝난 뒤 결과화면 진입 — 승패 SFX/BGM도 여기서(연출 도중 스포일 방지).
        private void EnterResult()
        {
            // **재진입 차단.** 아래 ConcludeDefeatWithoutSwap이 BattleEnded를 발화하고,
            // 그 핸들러(OnBattleEnded)가 이 메서드를 다시 부른다 — 같은 프레임 안에서 동기로.
            if (phase == Phase.Result) return;

            // **상태를 먼저 세운다.** 순서가 이 가드의 전부다: 상태가 Conclude 뒤에 있으면
            // 재진입이 위 가드를 통과해(그때 phase는 아직 Result가 아니다) 결과 진입이 통째로
            // 두 번 돌고, 패배 SFX·BGM이 겹쳐 재생된다.
            phase = Phase.Result;
            resultShown = true;
            resultTimer = 0f;
            pendingResult = false;

            // 교체할 팀원이 없어 여기로 온 패배는 **컨트롤러가 아직 종료 처리를 못 했다** —
            // `PlayerFainted`가 구독자 유무만 보고 "UI가 교체로 처리한다"고 판단해 손을 뗐기 때문이다.
            // 여기서 확정시키지 않으면 `BattleEnded`/`DuelEnded`가 영영 발화하지 않아 NPC 대결의
            // 보상·쿨다운이 통째로 건너뛰어진다(상세는 `ConcludeDefeatWithoutSwap` 주석).
            // 승리·도주로 온 경우엔 이미 종료돼 있어 멱등하게 무시된다.
            if (!lastWon && battleController != null) battleController.ConcludeDefeatWithoutSwap();

            if (AudioManager.Instance != null && (battleController == null || !battleController.DidEscape))
            {
                AudioManager.Instance.PlaySFX(lastWon ? SfxType.Victory : SfxType.Defeat);
                AudioManager.Instance.PlayBGM(lastWon ? BgmType.Victory : BgmType.Defeat);
            }
        }

        private void Update()
        {
            if (screenFlashTimer > 0f) screenFlashTimer -= Time.unscaledDeltaTime;
            if (comboDisplayTimer > 0f) comboDisplayTimer -= Time.unscaledDeltaTime;

            if (phase == Phase.None) return;
            RestoreArenaAfterEnable();

            phaseTimer += BattlePresentation.DeltaTime;
            introTimer += BattlePresentation.DeltaTime;

            if (actionTimer > 0) actionTimer -= BattlePresentation.DeltaTime;
            if (playerShake > 0) playerShake -= BattlePresentation.DeltaTime;
            if (enemyShake > 0) enemyShake -= BattlePresentation.DeltaTime;
            // Results can still close when another system pauses the simulation.
            if (resultShown) resultTimer += Time.unscaledDeltaTime;

            if (faintStarted) faintTimer += BattlePresentation.DeltaTime;
            if (impactRevealed) impactTimer += BattlePresentation.DeltaTime;
            if ((phase == Phase.PlayerAttack || phase == Phase.EnemyAttack) && !impactRevealed
                && (arena == null || !arena.IsActive || !arena.IsPlayingSkill) && phaseTimer >= attackDuration * 0.4f)
                RevealImpact(phase == Phase.PlayerAttack);

            if (playerStats != null)
                displayPlayerHp = Mathf.MoveTowards(displayPlayerHp, revealPlayerHp,
                    Mathf.Max(60f, playerStats.MaxHp / 0.5f) * BattlePresentation.DeltaTime);
            if (enemyStats != null)
                displayEnemyHp = Mathf.MoveTowards(displayEnemyHp, revealEnemyHp,
                    Mathf.Max(60f, enemyStats.MaxHp / 0.5f) * BattlePresentation.DeltaTime);

            // 칩바 — 실제 fill(displayHp)보다 느리게 감소해 최근 피해 잔상. 회복 시엔 스냅.
            float playerChipSpeed = Mathf.Max(22f, playerStats != null ? playerStats.MaxHp / 1.2f : 22f) * BattlePresentation.DeltaTime;
            float enemyChipSpeed = Mathf.Max(22f, enemyStats != null ? enemyStats.MaxHp / 1.2f : 22f) * BattlePresentation.DeltaTime;
            chipPlayerHp = chipPlayerHp > displayPlayerHp
                ? Mathf.MoveTowards(chipPlayerHp, displayPlayerHp, playerChipSpeed) : displayPlayerHp;
            chipEnemyHp = chipEnemyHp > displayEnemyHp
                ? Mathf.MoveTowards(chipEnemyHp, displayEnemyHp, enemyChipSpeed) : displayEnemyHp;

            // BGM 인텐시티: HP 30% 이하부터 가파르게 상승
            if (AudioManager.Instance != null && playerStats != null && playerStats.MaxHp > 0)
            {
                float hpRatio = displayPlayerHp / playerStats.MaxHp;
                float intensity = Mathf.Clamp01((0.5f - hpRatio) * 2f);
                AudioManager.Instance.SetBattleIntensity(intensity);
            }

            TickDuelBanter();

            // 인트로 길이는 상대에 따라 다르다 — 간부·수문장이면 컷인 길이(BattleScreenUI.Duel).
            if (phase == Phase.Intro && introTimer > IntroSeconds)
            {
                phase = Phase.PlayerTurn;
                phaseTimer = 0f;
            }

            if (phase == Phase.SwapSelect)
            {
                swapMessageTimer += BattlePresentation.DeltaTime;
                int swapIndex = -1;
                if (wantSwap0 || Input.GetKeyDown(KeyCode.Alpha1)) swapIndex = 0;
                else if (wantSwap1 || Input.GetKeyDown(KeyCode.Alpha2)) swapIndex = 1;
                else if (wantSwap2 || Input.GetKeyDown(KeyCode.Alpha3)) swapIndex = 2;
                else if (wantSwap3 || Input.GetKeyDown(KeyCode.Alpha4)) swapIndex = 3;
                else if (wantSwap4 || Input.GetKeyDown(KeyCode.Alpha5)) swapIndex = 4;
                wantSwap0 = wantSwap1 = wantSwap2 = wantSwap3 = wantSwap4 = false;
                if (swapIndex >= 0) TrySwapToSlot(swapIndex);

                if ((wantMouseClick || Input.GetMouseButtonDown(0)) && !IsSpeedControlPointerHit)
                {
                    Vector2 mousePos = wantMouseClick ? guiMousePos :
                        UIScale.VirtualMousePosition;
                    for (int i = 0; i < swapBtnRects.Length; i++)
                    {
                        if (swapBtnRects[i].width > 0 && swapBtnAvail[i] && swapBtnRects[i].Contains(mousePos))
                        {
                            TrySwapToSlot(i);
                            break;
                        }
                    }
                    wantMouseClick = false;
                }
            }

            if (phase == Phase.PlayerTurn && battleController != null)
            {
                if (wantSkill0 || Input.GetKeyDown(KeyCode.Alpha1)) TryUseSkill(0);
                else if (wantSkill1 || Input.GetKeyDown(KeyCode.Alpha2)) TryUseSkill(1);
                else if (wantSkill2 || Input.GetKeyDown(KeyCode.Alpha3)) TryUseSkill(2);
                else if (wantSkill3 || Input.GetKeyDown(KeyCode.Alpha4)) TryUseSkill(3);
                else if (wantBasicAtk || Input.GetKeyDown(KeyCode.F)) TryBasicAttack();
                else if (wantEscape || Input.GetKeyDown(KeyCode.Escape)) TryEscape();
                wantSkill0 = wantSkill1 = wantSkill2 = wantSkill3 = false;
                wantBasicAtk = wantEscape = false;

                if ((wantMouseClick || Input.GetMouseButtonDown(0)) && !IsSpeedControlPointerHit)
                {
                    Vector2 mousePos = wantMouseClick ? guiMousePos :
                        UIScale.VirtualMousePosition;
                    for (int i = 0; i < skillBtnCount; i++)
                    {
                        if (skillBtnUsable[i] && skillBtnRects[i].Contains(mousePos))
                        {
                            TryUseSkill(i);
                            break;
                        }
                    }
                    if (basicAtkRect.width > 0 && basicAtkRect.Contains(mousePos))
                        TryBasicAttack();
                    if (escapeRect.width > 0 && escapeRect.Contains(mousePos))
                        TryEscape();
                    wantMouseClick = false;
                }
            }

            if (phase == Phase.PlayerAttack && PhaseAnimDone())
            {
                RevealImpact(true);
                // The round resolves synchronously, but a fatal enemy reply must still be shown.
                if (battleController != null && battleController.EnemyActedThisRound)
                    BeginTurnAnnounce("상대의 턴", false, Phase.EnemyAttack, triggerEnemy: true);
                else if (FinishFaintPresentation())
                {
                    if (enemyStats != null) revealEnemyHp = enemyStats.CurrentHp;
                    if (playerStats != null) revealPlayerHp = playerStats.CurrentHp;
                    if (pendingSwap) EnterSwapSelect();
                    else if (pendingResult) EnterResult();
                    else BeginTurnAnnounce("당신의 턴", true, Phase.PlayerTurn, triggerEnemy: false);
                }
            }

            if (phase == Phase.EnemyAttack && PhaseAnimDone())
            {
                RevealImpact(false);
                if (FinishFaintPresentation())
                {
                    if (pendingSwap) EnterSwapSelect();
                    else if (pendingResult) EnterResult();
                    else BeginTurnAnnounce("당신의 턴", true, Phase.PlayerTurn, triggerEnemy: false);
                }
            }

            if (phase == Phase.TurnAnnounce)
            {
                announceTimer -= BattlePresentation.DeltaTime;
                // 탭/키 입력 시 즉시 스킵(반복 전투 배려) — 추가 상태 없이 기존 입력 플래그 재사용.
                if (wantMouseClick) { wantMouseClick = false; announceTimer = 0f; }
                if (announceTimer <= 0f) FinishTurnAnnounce();
            }

            if (phase == Phase.Result && resultTimer > 4f)
            {
                EndBattle();
            }

            // 탭 래치 차단 — `Input.GetMouseButtonDown`과 같은 한 프레임 수명으로 맞춘다.
            // `wantMouseClick`은 OnGUI(:MouseDown)가 세우는데 소거는 SwapSelect/PlayerTurn/TurnAnnounce
            // 분기 **안**에서만 했다. Intro·PlayerAttack·EnemyAttack·Result에서 탭하면 true로 남는다.
            // 하필 Intro 종료와 PlayerTurn 처리가 **같은 Update 패스**에 있어(위 Intro 전이 → PlayerTurn 블록),
            // 인트로 중 탭이 곧바로 다음 줄에서 소비된다 — 그것도 **지난 전투의** `escapeRect`/`basicAtkRect`로.
            // 두 Rect는 EndBattle이 지우지 않아 화면상 같은 자리에 남아 있으므로, 2번째 전투부터
            // **인트로 중 도주 버튼 근처를 탭하면 턴을 보기도 전에 도주가 실행됐다.**
            // (같은 결함을 RaidBattleUI에서 먼저 고쳤다 — 2026-08-06 audit.)
            wantMouseClick = false;
        }

        // 다음 턴을 알리는 중앙 배너 시작. 종료 후 nextPhase로 전이(triggerEnemy면 적 연출 발동).
        private void BeginTurnAnnounce(string text, bool isPlayer, Phase nextPhase, bool triggerEnemy)
        {
            phase = Phase.TurnAnnounce;
            phaseTimer = 0f;
            announceTimer = TurnAnnounceDuration;
            announceText = text;
            announceIsPlayer = isPlayer;
            announceNextPhase = nextPhase;
            announceTriggerEnemy = triggerEnemy;
        }

        private void FinishTurnAnnounce()
        {
            phase = announceNextPhase;
            phaseTimer = 0f;
            if (announceTriggerEnemy)
            {
                impactRevealed = false;
                attackDuration = battleController != null && battleController.LastEnemySkill != null ? 2.5f : 1.8f;
                SetEnemyActionText();          // 적 행동 텍스트(플레이어와 대칭)
                TriggerEnemyAttackEffect();    // 적 공격 연출 발동
            }
        }

        // 적 EnemyAttack 진입 시 "{적 이름}의 {스킬}!" 텍스트 — 3D 모드에서도 적 행동이 보이게(대칭).
        private void SetEnemyActionText()
        {
            if (enemyStats == null || enemyStats.Data == null) return;
            InsectSkill es = battleController != null ? battleController.LastEnemySkill : null;
            string skillName = es != null && !string.IsNullOrEmpty(es.displayName) ? es.displayName : "공격";
            actionText = $"{enemyStats.Data.displayName}의 {skillName}!";
            actionTimer = 1.5f;
        }

        private void SnapshotHp()
        {
            savedEnemyHp = enemyStats != null ? enemyStats.CurrentHp : 0;
            savedPlayerHp = playerStats != null ? playerStats.CurrentHp : 0;
            hpSnapshotTaken = true;
        }

        private void TryUseSkill(int index)
        {
            if (phase != Phase.PlayerTurn || battleController == null || !battleController.CanUseSkill(index)) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.SkillUse);

            turnNumber++;
            InsectSkill[] curSkills = battleController.GetPlayerSkills();
            InsectSkill skill = curSkills != null && index < curSkills.Length ? curSkills[index] : null;
            lastSkillName = skill != null ? skill.displayName : "공격";
            lastSkillElement = skill != null ? skill.element : playerStats.Data.primaryType;
            actionText = $"{playerStats.Data.displayName}의 {lastSkillName}!";
            actionTimer = 1.5f;
            attackDuration = 2.5f;
            SnapshotHp();

            battleController.UseSkill(index);
            if (battleController.PlayerActedThisRound && arena != null && arena.IsActive)
            {
                InsectElement elem = lastSkillElement;
                SkillEffectType effectType = (skill != null) ? skill.effectType : SkillEffectType.Damage;
                arena.PlaySkillEffect(true, elem, effectType,
                    () => { if (isActiveAndEnabled && phase == Phase.PlayerAttack) RevealImpact(true); },
                    BattleArenaController.IsMeleeElement(elem), attackDuration,
                    BuildHitCue(true, skill != null ? skill.displayName : null, effectType));
            }

        }

        private void TryBasicAttack()
        {
            if (phase != Phase.PlayerTurn || battleController == null) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.Attack);
            turnNumber++;
            lastSkillName = "기본 공격";
            lastSkillElement = (playerStats != null && playerStats.Data != null) ? playerStats.Data.primaryType : InsectElement.Bug;
            actionText = $"{playerStats.Data.displayName}의 기본 공격!";
            actionTimer = 1.5f;
            attackDuration = 1.8f;
            SnapshotHp();

            battleController.UseBasicAttack();
            if (battleController.PlayerActedThisRound && arena != null && arena.IsActive)
            {
                InsectElement elem = (playerStats != null && playerStats.Data != null) ? playerStats.Data.primaryType : InsectElement.Bug;
                // 기본 공격은 외치지 않는다(이름이 없다) — 울음과 타격감만.
                arena.PlaySkillEffect(true, elem, SkillEffectType.Damage,
                    () => { if (isActiveAndEnabled && phase == Phase.PlayerAttack) RevealImpact(true); },
                    BattleArenaController.IsMeleeElement(elem), attackDuration,
                    BuildHitCue(true, null, SkillEffectType.Damage));
            }

        }

        private void TrySwapToSlot(int slotIndex)
        {
            if (teamManager == null || collection == null || battleController == null) return;
            if (slotIndex < 0 || slotIndex >= BattleTeamManager.MaxSlots) return;

            string slotId = teamManager.GetSlot(slotIndex);
            if (string.IsNullOrEmpty(slotId)) return;
            if (faintedInsectIds.Contains(slotId)) return;
            if (slotId == currentInsectId) return;

            PlayerInsectData pid = collection.GetByInstanceId(slotId);
            if (pid != null && pid.IsFainted) return;   // 지속 기절(0 HP)은 교체 불가(치료 필요)
            InsectData data = pid != null ? collection.GetInsectData(pid.insectId) : null;
            if (data == null) return;

            InsectSkill[] equippedSkills = null;
            if (pid != null && collection != null)
                equippedSkills = collection.GetEquippedSkills(pid);

            int level = pid != null ? pid.level : 1;
            battleController.SwapPlayerInsect(data, level, equippedSkills, pid);
        }

        private void TryEscape()
        {
            if (phase != Phase.PlayerTurn || battleController == null) return;
            if (battleController.IsSandbox) return;   // 챔피언전은 도망칠 수 없다 — 턴도 문구도 소모하지 않는다
            attackDuration = 1.8f;
            SnapshotHp();
            bool escaped = battleController.TryEscape();
            if (escaped)
            {
                actionText = "도망쳤다!";
                actionTimer = 1.5f;
            }
            else
            {
                turnNumber++;
                actionText = "도망치지 못했다!";
                actionTimer = 1.5f;
            }
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            // DrawBattleOverlay
            turnStyle3dCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            turnStyle3dCache.normal.textColor = new Color(0.9f, 0.85f, 0.5f);

            turnStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            turnStyleCache.normal.textColor = new Color(0.9f, 0.85f, 0.5f);

            // DrawBattleField
            playerLabelCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            playerLabelCache.normal.textColor = new Color(0.5f, 0.85f, 1f);

            playerTagCache = new GUIStyle(GUI.skin.label)
            { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            playerTagCache.normal.textColor = new Color(0.4f, 0.6f, 0.8f);

            enemyLabelCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            enemyLabelCache.normal.textColor = new Color(1f, 0.5f, 0.4f);

            enemyTagCache = new GUIStyle(GUI.skin.label)
            { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            enemyTagCache.normal.textColor = new Color(0.8f, 0.4f, 0.35f);

            // DrawInsectSprite — fontSize depends on s, set per call
            nameTagCache = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            // DrawHpBox
            hpNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold };
            hpLvStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            hpLvStyleCache.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
            hpMiniStatCache = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            hpMiniStatCache.normal.textColor = new Color(0.55f, 0.55f, 0.6f);
            hpTextCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            hpTextCache.normal.textColor = Color.white;
            hpEffStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            hpEffStyleCache.normal.textColor = new Color(0.6f, 0.8f, 1f);

            // DrawIntro — fontSize is dynamic for vs/fight
            introVsStyleCache = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            introPNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            introENameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            introFightStyleCache = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            introEncounterStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            // DrawSkillPanel
            // 아래 라벨들은 전부 세로 가운데 정렬이다. IMGUI 기본값(Upper*)은 글자를 상자 맨 위에
            // 붙여 그려서, 상자가 글자보다 조금만 작아도 아랫부분(한글 받침·디센더)이 먼저 잘린다.
            // Middle*이면 남는 세로가 위아래로 반씩 나뉘어 같은 상자에서도 여백이 생긴다.
            skillHeaderCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            skillHeaderCache.normal.textColor = new Color(0.9f, 0.85f, 0.5f);
            skillKeyNumCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            skillNameStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 27,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            skillTypeLabelCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.MiddleLeft };
            // 상성 배지는 위력과 같은 줄의 오른쪽 칸이라 우측 정렬이다.
            skillEffLabelCache = new GUIStyle(GUI.skin.label)
            { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            skillInfoStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            skillCdStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            skillCdStyleCache.normal.textColor = new Color(1f, 0.4f, 0.3f);
            skillCdInfoCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleRight };
            skillCdInfoCache.normal.textColor = new Color(0.68f, 0.68f, 0.74f);
            skillFKeyCache = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            skillFKeyCache.normal.textColor = new Color(1f, 0.85f, 0.3f);
            skillFInfoCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            // 부제 두 줄은 원래 바탕색을 살짝 밝힌 정도(0.6/0.55/0.4 · 0.55/0.4/0.4)라 버튼 배경과
            // 거의 구분되지 않았다 — 호버로 배경이 밝아지면 각각 4.01 / 3.17까지 떨어져 AA 미만이었다.
            // 색조(따뜻한 금색 · 붉은색)는 유지한 채 밝기만 올린다: 평시 10.06 / 9.92, 호버 8.16 / 8.75.
            skillFInfoCache.normal.textColor = new Color(0.85f, 0.79f, 0.62f);
            skillEscStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            skillEscStyleCache.normal.textColor = new Color(0.9f, 0.5f, 0.4f);
            skillEscInfoCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            skillEscInfoCache.normal.textColor = new Color(0.88f, 0.72f, 0.68f);

            // DrawAttackAnimation — fontSize dynamic for dmg
            dmgStyle3dCache = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            critLblCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            critLblCache.normal.textColor = new Color(1f, 0.85f, 0.2f);
            skillStyle3dCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            skillStyle3dCache.normal.textColor = Color.white;
            skillNameAtkStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            dmgStyleAtkCache = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            effStyleAtkCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            // DrawBuffDebuffEffect
            buffDebuffSkillStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            upStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            downStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            // DrawActionText
            actionTextStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            // DrawResult
            victoryStyleCache = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            rewardStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            rewardValStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            defeatStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 50, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            defeatGuideStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            defeatHintStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter };

            // DrawPhaseIndicator
            phaseIndicatorStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            // DrawSwapSelect
            swapHeaderCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            swapKeyStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            swapEmptyStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            swapEmptyStyleCache.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
            swapNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            swapInfoStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            swapStatStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 17, alignment = TextAnchor.MiddleCenter };
            swapFaintStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            swapFaintStyleCache.normal.textColor = new Color(1f, 0.3f, 0.3f, 0.9f);
            swapCurStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            swapCurStyleCache.normal.textColor = new Color(1f, 0.3f, 0.3f, 0.9f);
        }

        private GUIStyle speedControlStyle;
        private bool ShowSpeedControl => phase != Phase.None && phase != Phase.Intro && !resultShown;
        private Rect SpeedControlRect => UISafeLayout.TopPanel(132f, 56f, UISafeLayout.HAlign.Right);
        private bool IsSpeedControlPointerHit => ShowSpeedControl && SpeedControlRect.Contains(UIScale.VirtualMousePosition);

        private void DrawSpeedControl()
        {
            if (!ShowSpeedControl) return;
            if (speedControlStyle == null)
                speedControlStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            speedControlStyle.normal.textColor = UITheme.Instance.textPrimary;
            if (UISurface.Button(SpeedControlRect, $"속도 {BattlePresentation.Speed:0}×", UITheme.Instance.surfaceRaised, speedControlStyle))
                BattlePresentation.Speed = BattlePresentation.Speed < 1.5f ? 2f : 1f;
        }

        private void OnGUI()
        {
            if (phase == Phase.None) return;

            InitStyles();
            UIScale.Begin();
            DrawScreenFlash();
            if (arena == null || !arena.IsActive) DrawComboCounter();

            Event evt = Event.current;
            if (evt != null && evt.type == EventType.KeyDown)
            {
                switch (evt.keyCode)
                {
                    case KeyCode.Alpha1: case KeyCode.Keypad1:
                        if (phase == Phase.PlayerTurn) wantSkill0 = true;
                        else if (phase == Phase.SwapSelect) wantSwap0 = true;
                        evt.Use(); break;
                    case KeyCode.Alpha2: case KeyCode.Keypad2:
                        if (phase == Phase.PlayerTurn) wantSkill1 = true;
                        else if (phase == Phase.SwapSelect) wantSwap1 = true;
                        evt.Use(); break;
                    case KeyCode.Alpha3: case KeyCode.Keypad3:
                        if (phase == Phase.PlayerTurn) wantSkill2 = true;
                        else if (phase == Phase.SwapSelect) wantSwap2 = true;
                        evt.Use(); break;
                    case KeyCode.Alpha4: case KeyCode.Keypad4:
                        if (phase == Phase.PlayerTurn) wantSkill3 = true;
                        else if (phase == Phase.SwapSelect) wantSwap3 = true;
                        evt.Use(); break;
                    case KeyCode.Alpha5: case KeyCode.Keypad5:
                        if (phase == Phase.SwapSelect) wantSwap4 = true;
                        evt.Use(); break;
                    case KeyCode.F:
                        if (phase == Phase.PlayerTurn) wantBasicAtk = true;
                        evt.Use(); break;
                    case KeyCode.Escape:
                        if (phase == Phase.PlayerTurn) wantEscape = true;
                        evt.Use(); break;
                }
            }

            if (evt != null && evt.type == EventType.MouseDown && evt.button == 0 && !IsSpeedControlPointerHit)
            {
                wantMouseClick = true;
                guiMousePos = new Vector2(evt.mousePosition.x, evt.mousePosition.y);
            }

            DrawBattleOverlay();
            DrawBattleField();
            DrawHpBars();

            if (phase == Phase.Intro)
            {
                if (!DrawDuelCutIn()) DrawIntro();
            }
            else if (phase == Phase.PlayerTurn)
                DrawSkillPanel();
            else if (phase == Phase.SwapSelect)
                DrawSwapSelect();
            else if (phase == Phase.PlayerAttack)
            {
                DrawAttackAnimation(true);
                DrawPhaseIndicator(battleController != null && !battleController.PlayerActedThisRound ? "행동 확인" : "내 곤충의 공격!");
            }
            else if (phase == Phase.EnemyAttack)
            {
                DrawAttackAnimation(false);
                DrawPhaseIndicator("적 곤충의 반격!");
            }
            else if (phase == Phase.TurnAnnounce)
                DrawTurnAnnounce();

            if (actionTimer > 0)
                DrawActionText();

            // 기술 이름 외치기·비명·타격 의성어 — 결과 패널보다 먼저(아래에). 마지막 "털썩…"이
            // 결과 패널 위에 찍히던 자리다.
            BattleShoutOverlay.Draw(arena);

            DrawDuelBubble();

            if (resultShown)
                DrawResult();

            // "효과가 굉장했다!" 같은 전투 문구. 아레나가 자기 OnGUI에서 픽셀 좌표로 그리던 것을
            // 가상 캔버스 안으로 들여왔다(BattleEffectTextOverlay 주석 참고).
            BattleEffectTextOverlay.Draw(arena);

            DrawSpeedControl();

            UIScale.End();
        }

        private void DrawBattleOverlay()
        {
            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;

            // 3D 아레나 활성 시: 상단 턴 표시 바만 그리고 2D 배경 스킵
            if (arena != null && arena.IsActive)
            {
                Rect heading = UISafeLayout.TopPanel(340f, 50f);
                UISurface.Card(heading, UITheme.Instance.surfaceBase, UITheme.Instance.surfaceBorder);
                turnStyle3dCache.normal.textColor = UITheme.Instance.textSecondary;
                string turnLabel = $"탐험 전투  ·  {turnNumber + 1}번째 행동";
                if (comboCount >= 2 && comboDisplayTimer > 0f) turnLabel += $"  ·  연속 {comboCount}";
                UIHelper.LabelFit(heading, turnLabel, turnStyle3dCache);
                return;
            }

            float arenaY = sh * 0.08f;
            float arenaH = sh * 0.52f;
            float horizon = arenaY + arenaH * 0.45f;
            float groundBot = arenaY + arenaH;

            GUI.color = new Color(0.02f, 0.03f, 0.06f, 0.95f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);

            int skyBands = 6;
            float skyBandH = (horizon - arenaY) / skyBands;
            for (int i = 0; i < skyBands; i++)
            {
                float t = (float)i / skyBands;
                float r = Mathf.Lerp(0.02f, 0.08f, t);
                float g = Mathf.Lerp(0.03f, 0.12f, t);
                float b = Mathf.Lerp(0.10f, 0.22f, t);
                GUI.color = new Color(r, g, b, 1f);
                GUI.DrawTexture(new Rect(0, arenaY + i * skyBandH, sw, skyBandH + 1), Texture2D.whiteTexture);
            }

            int groundBands = 10;
            float groundH = groundBot - horizon;
            for (int i = 0; i < groundBands; i++)
            {
                float t = (float)i / groundBands;
                float bandY = horizon + t * groundH;
                float bandH = groundH / groundBands + 1;
                float depth = 1f - t * 0.6f;
                float r = Mathf.Lerp(0.06f, 0.14f, t);
                float g = Mathf.Lerp(0.10f, 0.22f, t);
                float b2 = Mathf.Lerp(0.06f, 0.10f, t);
                GUI.color = new Color(r, g, b2, 1f);
                GUI.DrawTexture(new Rect(0, bandY, sw, bandH), Texture2D.whiteTexture);

                if (i > 2 && i % 2 == 0)
                {
                    GUI.color = new Color(r + 0.03f, g + 0.04f, b2 + 0.02f, 0.3f);
                    GUI.DrawTexture(new Rect(0, bandY, sw, 1), Texture2D.whiteTexture);
                }
            }

            GUI.color = new Color(0.18f, 0.28f, 0.18f, 0.5f);
            GUI.DrawTexture(new Rect(0, horizon - 1, sw, 3), Texture2D.whiteTexture);

            float playerPlatCX = sw * 0.22f;
            float enemyPlatCX = sw * 0.72f;
            float playerPlatY = arenaY + arenaH * 0.78f;
            float enemyPlatY = arenaY + arenaH * 0.52f;
            DrawPlatformEllipse(playerPlatCX, playerPlatY, sw * 0.16f, 22f, new Color(0.18f, 0.28f, 0.18f, 0.7f), new Color(0.28f, 0.40f, 0.28f, 0.4f));
            DrawPlatformEllipse(enemyPlatCX, enemyPlatY, sw * 0.13f, 16f, new Color(0.18f, 0.22f, 0.28f, 0.6f), new Color(0.28f, 0.35f, 0.45f, 0.35f));

            GUI.color = new Color(0.04f, 0.05f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(0, 0, sw, arenaY), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.5f, 0.9f, 0.4f);
            GUI.DrawTexture(new Rect(0, arenaY - 2, sw, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(0, 4, sw, 34), $"BATTLE  -  Turn {turnNumber + 1}", turnStyleCache);
        }

        private void DrawPlatformEllipse(float cx, float cy, float rx, float ry, Color fill, Color rim)
        {
            int segments = 16;
            for (int i = -segments; i <= segments; i++)
            {
                float t = (float)i / segments;
                float w = rx * 2f * Mathf.Sqrt(1f - t * t);
                float h = ry / segments * 2f;
                float sy = cy + t * ry;
                GUI.color = fill;
                GUI.DrawTexture(new Rect(cx - w / 2f, sy, w, Mathf.Max(h, 1f)), Texture2D.whiteTexture);
            }
            for (int i = -segments; i <= segments; i++)
            {
                float t = (float)i / segments;
                float w = rx * 2f * Mathf.Sqrt(Mathf.Max(0, 1f - t * t));
                float sy = cy + t * ry;
                GUI.color = rim;
                GUI.DrawTexture(new Rect(cx - w / 2f, sy, w, 1), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        private void DrawBattleField()
        {
            // 3D 아레나가 활성화되어 있으면 2D 곤충 그리기 스킵
            if (arena != null && arena.IsActive)
                return;

            if (playerStats == null || enemyStats == null) return;

            float arenaTop = UIScale.VirtualScreenHeight * 0.08f;
            float arenaH = UIScale.VirtualScreenHeight * 0.52f;

            float playerX = UIScale.VirtualScreenWidth * 0.22f;
            float playerY = arenaTop + arenaH * 0.72f;
            float enemyX = UIScale.VirtualScreenWidth * 0.72f;
            float enemyY = arenaTop + arenaH * 0.38f;

            float breathP = Mathf.Sin(Time.time * 2.2f) * 2f;
            float breathE = Mathf.Sin(Time.time * 1.8f + 1f) * 2f;
            playerY += breathP;
            enemyY += breathE;

            if (playerShake > 0)
            {
                playerX += Mathf.Sin(Time.time * 55f) * 10f;
                playerY += Mathf.Cos(Time.time * 55f) * 6f;
            }
            if (enemyShake > 0)
            {
                enemyX += Mathf.Sin(Time.time * 55f) * 10f;
                enemyY += Mathf.Cos(Time.time * 55f) * 6f;
            }

            float playerScale = 5.0f;
            float enemyScale = 4.2f;

            Color playerGlow = UITheme.Instance.GetInsectColor(playerStats.Data.insectId, playerStats.Data.rarity);
            float glowPulse = 0.08f + Mathf.Sin(Time.time * 2f) * 0.03f;
            GUI.color = new Color(playerGlow.r, playerGlow.g, playerGlow.b, glowPulse);
            float glowR = 90f;
            GUI.DrawTexture(new Rect(playerX - glowR, playerY - glowR, glowR * 2, glowR * 2), Texture2D.whiteTexture);

            Color enemyGlow = UITheme.Instance.GetInsectColor(enemyStats.Data.insectId, enemyStats.Data.rarity);
            GUI.color = new Color(enemyGlow.r, enemyGlow.g, enemyGlow.b, glowPulse);
            float glowR2 = 75f;
            GUI.DrawTexture(new Rect(enemyX - glowR2, enemyY - glowR2, glowR2 * 2, glowR2 * 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            DrawInsectSprite(playerX, playerY, playerStats.Data, playerScale, true);
            DrawInsectSprite(enemyX, enemyY, enemyStats.Data, enemyScale, false);

            GUI.color = Color.white;
            UIHelper.LabelFit(new Rect(playerX - 80, playerY + 42 * playerScale / 3f, 160, 26), playerStats.Data.displayName, playerLabelCache);
            GUI.Label(new Rect(playerX - 60, playerY + 42 * playerScale / 3f + 24, 120, 18), "내 곤충", playerTagCache);
            UIHelper.LabelFit(new Rect(enemyX - 80, enemyY + 38 * enemyScale / 3f, 160, 26), enemyStats.Data.displayName, enemyLabelCache);
            GUI.Label(new Rect(enemyX - 60, enemyY + 38 * enemyScale / 3f + 24, 120, 18), "야생 곤충", enemyTagCache);
        }

        private void DrawInsectSprite(float cx, float cy, InsectData data, float scale, bool flip)
        {
            Color col = UITheme.Instance.GetInsectColor(data.insectId, data.rarity);
            Color bodyCol = col;
            Color darkCol = new Color(col.r * 0.45f, col.g * 0.45f, col.b * 0.45f);
            Color lightCol = new Color(
                Mathf.Min(1, col.r + 0.4f), Mathf.Min(1, col.g + 0.4f), Mathf.Min(1, col.b + 0.4f));
            Color accentCol = UITheme.Instance.GetInsectRarityColor(data.rarity);

            float s = scale;
            float dir = flip ? 1f : -1f;
            string id = data.insectId ?? "";

            GUI.color = new Color(accentCol.r, accentCol.g, accentCol.b, 0.08f + (int)data.rarity * 0.03f);
            GUI.DrawTexture(new Rect(cx - 50 * s, cy - 50 * s, 100 * s, 100 * s), Texture2D.whiteTexture);

            if (id.Contains("butterfly") || id.Contains("moth") || id.Contains("luna") || id.Contains("atlas"))
                DrawButterfly(cx, cy, s, dir, bodyCol, darkCol, lightCol, accentCol);
            else if (id.Contains("orchid") || id.Contains("ghost"))
                DrawMantis(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else if (id.Contains("mantis"))
                DrawMantis(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else if (id.Contains("damselfly"))
                DrawDragonfly(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else if (id.Contains("dragonfly"))
                DrawDragonfly(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            // "beetle"이 "bee"를 품는다 — 가드가 없으면 아래 stag/rhinoceros/hercules 분기까지
            // 못 가고 딱정벌레가 전부 벌로 그려진다(InsectEntity.BuildModel의 같은 가드와 짝).
            else if ((id.Contains("bee") && !id.Contains("beetle")) || id.Contains("wasp") || id.Contains("hornet"))
                DrawBee(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else if (id.Contains("firefly"))
                DrawFirefly(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else if (id.Contains("stag") || id.Contains("rhinoceros") || id.Contains("hercules") || id.Contains("golden_stag"))
                DrawHornBeetle(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else if (id.Contains("spider"))
                DrawSpider(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else if (id.Contains("grasshopper") || id.Contains("cricket") || id.Contains("katydid"))
                DrawGrasshopper(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else if (id.Contains("centipede"))
                DrawCentipede(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else if (id.Contains("ladybug"))
                DrawLadybug(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else if (id.Contains("caterpillar"))
                DrawCaterpillar(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else if (id.Contains("ant"))
                DrawAnt(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else if (id.Contains("stick_insect") || id.Contains("leaf_insect"))
                DrawStickInsect(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else if (id.Contains("mosquito") || id.Contains("fly"))
                DrawFly(cx, cy, s, dir, bodyCol, darkCol, lightCol);
            else
                DrawBeetle(cx, cy, s, dir, bodyCol, darkCol, lightCol);

            nameTagCache.fontSize = (int)(14 * s / 3f);
            nameTagCache.normal.textColor = new Color(col.r, col.g, col.b, 0.9f);
            GUI.color = Color.white;
            GUI.Label(new Rect(cx - 40 * s, cy + 30 * s, 80 * s, 16 * s), data.displayName, nameTagCache);

            GUI.color = Color.white;
        }

        private void DrawBeetle(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 24 * s, cy - 18 * s, 48 * s, 30 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 20 * s, cy - 15 * s, 40 * s, 24 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 1 * s, cy - 15 * s, 2 * s, 24 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 30 * s, 20 * s, 16 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 27 * s, 14 * s, 10 * s), Texture2D.whiteTexture);
            DrawEyes(cx, cy - 24 * s, s, dir);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 42 * s, 2 * s, 14 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy - 42 * s, 2 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 44 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy - 44 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            DrawLegs(cx, cy, s, dark, 3);
            GUI.color = Color.white;
        }

        private void DrawHornBeetle(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 26 * s, cy - 16 * s, 52 * s, 32 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 22 * s, cy - 13 * s, 44 * s, 26 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 1 * s, cy - 13 * s, 2 * s, 26 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 12 * s, cy - 32 * s, 24 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 9 * s, cy - 28 * s, 18 * s, 12 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 2 * s, cy - 52 * s, 4 * s, 24 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 54 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 14 * s + dir * 2 * s, cy - 36 * s, 5 * s, 12 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 10 * s + dir * 2 * s, cy - 36 * s, 5 * s, 12 * s), Texture2D.whiteTexture);
            DrawEyes(cx, cy - 24 * s, s, dir);
            DrawLegs(cx, cy, s, dark, 3);
            GUI.color = Color.white;
        }

        private void DrawButterfly(float cx, float cy, float s, float dir, Color body, Color dark, Color light, Color accent)
        {
            float wingFlap = Mathf.Sin(Time.time * 4f) * 3 * s;
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.7f);
            GUI.DrawTexture(new Rect(cx - 40 * s, cy - 24 * s + wingFlap, 30 * s, 36 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 10 * s, cy - 24 * s - wingFlap, 30 * s, 36 * s), Texture2D.whiteTexture);
            GUI.color = new Color(light.r, light.g, light.b, 0.6f);
            GUI.DrawTexture(new Rect(cx - 34 * s, cy - 16 * s + wingFlap, 18 * s, 20 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 16 * s, cy - 16 * s - wingFlap, 18 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = new Color(body.r, body.g, body.b, 0.6f);
            GUI.DrawTexture(new Rect(cx - 34 * s, cy + 6 * s + wingFlap, 22 * s, 22 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 12 * s, cy + 6 * s - wingFlap, 22 * s, 22 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 20 * s, 8 * s, 38 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 3 * s, cy - 18 * s, 6 * s, 34 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 28 * s, 12 * s, 12 * s), Texture2D.whiteTexture);
            DrawEyes(cx, cy - 24 * s, s, dir);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 44 * s, 2 * s, 18 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 44 * s, 2 * s, 18 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 46 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 7 * s, cy - 46 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawMantis(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 14 * s, 16 * s, 36 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 12 * s, 12 * s, 32 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 32 * s, 20 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 28 * s, 14 * s, 12 * s), Texture2D.whiteTexture);
            DrawEyes(cx, cy - 26 * s, s, dir);
            float swingAngle = Mathf.Sin(Time.time * 3f) * 4 * s;
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 26 * s, cy - 22 * s + swingAngle, 18 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 22 * s - swingAngle, 18 * s, 5 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 30 * s, cy - 28 * s + swingAngle, 6 * s, 14 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 24 * s, cy - 28 * s - swingAngle, 6 * s, 14 * s), Texture2D.whiteTexture);
            DrawLegs(cx, cy + 6 * s, s, dark, 2);
            GUI.color = new Color(body.r, body.g, body.b, 0.25f);
            GUI.DrawTexture(new Rect(cx - 18 * s, cy - 8 * s, 12 * s, 24 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 8 * s, 12 * s, 24 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawDragonfly(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            float wingFlap = Mathf.Sin(Time.time * 6f) * 2 * s;
            GUI.color = new Color(light.r, light.g, light.b, 0.3f);
            GUI.DrawTexture(new Rect(cx - 38 * s, cy - 18 * s + wingFlap, 32 * s, 10 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 18 * s - wingFlap, 32 * s, 10 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 34 * s, cy - 6 * s - wingFlap, 28 * s, 8 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 6 * s + wingFlap, 28 * s, 8 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 12 * s, 8 * s, 44 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 3 * s, cy - 10 * s, 6 * s, 40 * s), Texture2D.whiteTexture);
            for (int i = 0; i < 4; i++)
            {
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 3 * s, cy + 8 * s + i * 7 * s, 6 * s, 2 * s), Texture2D.whiteTexture);
            }
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 26 * s, 20 * s, 16 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 24 * s, 8 * s, 8 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy - 24 * s, 8 * s, 8 * s), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 22 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4 * s, cy - 22 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawBee(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            GUI.DrawTexture(new Rect(cx - 28 * s, cy - 24 * s, 20 * s, 14 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 24 * s, 20 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 18 * s, cy - 14 * s, 36 * s, 26 * s), Texture2D.whiteTexture);
            GUI.color = new Color(0.1f, 0.1f, 0.05f);
            for (int i = 0; i < 3; i++)
            {
                GUI.DrawTexture(new Rect(cx - 16 * s, cy - 10 * s + i * 8 * s, 32 * s, 3 * s), Texture2D.whiteTexture);
            }
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 26 * s, 16 * s, 14 * s), Texture2D.whiteTexture);
            DrawEyes(cx, cy - 22 * s, s, dir);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 36 * s, 2 * s, 12 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3 * s, cy - 36 * s, 2 * s, 12 * s), Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.15f, 0.1f);
            GUI.DrawTexture(new Rect(cx - 2 * s, cy + 12 * s, 4 * s, 10 * s), Texture2D.whiteTexture);
            DrawLegs(cx, cy + 2 * s, s, dark, 3);
            GUI.color = Color.white;
        }

        private void DrawFirefly(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            float glowPulse = 0.3f + Mathf.Sin(Time.time * 4f) * 0.2f;
            GUI.color = new Color(0.8f, 1f, 0.4f, glowPulse);
            GUI.DrawTexture(new Rect(cx - 30 * s, cy - 30 * s, 60 * s, 60 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 14 * s, cy - 14 * s, 28 * s, 26 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 11 * s, cy - 11 * s, 22 * s, 20 * s), Texture2D.whiteTexture);
            float glowIntensity = 0.6f + Mathf.Sin(Time.time * 4f) * 0.4f;
            GUI.color = new Color(0.9f, 1f, 0.3f, glowIntensity);
            GUI.DrawTexture(new Rect(cx - 10 * s, cy + 6 * s, 20 * s, 12 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 24 * s, 16 * s, 12 * s), Texture2D.whiteTexture);
            DrawEyes(cx, cy - 20 * s, s, dir);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 36 * s, 2 * s, 14 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3 * s, cy - 36 * s, 2 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = new Color(body.r, body.g, body.b, 0.25f);
            GUI.DrawTexture(new Rect(cx - 22 * s, cy - 10 * s, 14 * s, 18 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 10 * s, 14 * s, 18 * s), Texture2D.whiteTexture);
            DrawLegs(cx, cy + 4 * s, s, dark, 3);
            GUI.color = Color.white;
        }

        private void DrawEyes(float cx, float cy, float s, float dir)
        {
            float eyeOff = 3 * s * dir;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - 6 * s + eyeOff, cy, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s + eyeOff, cy, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(cx - 4 * s + eyeOff, cy + 1.5f * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4 * s + eyeOff, cy + 1.5f * s, 2 * s, 2 * s), Texture2D.whiteTexture);
        }

        private void DrawLegs(float cx, float cy, float s, Color dark, int pairs)
        {
            GUI.color = dark;
            for (int i = 0; i < pairs; i++)
            {
                float lx = (i - (pairs - 1) * 0.5f) * 10 * s;
                GUI.DrawTexture(new Rect(cx + lx - 3 * s, cy + 12 * s, 6 * s, 14 * s), Texture2D.whiteTexture);
            }
        }

        private void DrawSpider(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            // Large round abdomen
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 22 * s, cy - 8 * s, 44 * s, 36 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 18 * s, cy - 5 * s, 36 * s, 30 * s), Texture2D.whiteTexture);
            // Cephalothorax (smaller front part)
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 12 * s, cy - 24 * s, 24 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 9 * s, cy - 21 * s, 18 * s, 14 * s), Texture2D.whiteTexture);
            // 8 eyes (small red dots in 2 rows)
            Color eyeCol = new Color(0.9f, 0.15f, 0.1f);
            GUI.color = eyeCol;
            GUI.DrawTexture(new Rect(cx - 6 * s + dir * 2 * s, cy - 20 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1 * s + dir * 2 * s, cy - 20 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3 * s + dir * 2 * s, cy - 20 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 8 * s + dir * 2 * s, cy - 20 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 5 * s + dir * 2 * s, cy - 16 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 0 * s + dir * 2 * s, cy - 16 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4 * s + dir * 2 * s, cy - 16 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 7 * s + dir * 2 * s, cy - 20 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            // Fangs
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 6 * s + dir * 4 * s, cy - 12 * s, 3 * s, 8 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3 * s + dir * 4 * s, cy - 12 * s, 3 * s, 8 * s), Texture2D.whiteTexture);
            // 8 legs (4 per side, radiating outward)
            GUI.color = dark;
            for (int i = 0; i < 4; i++)
            {
                float angle = (i - 1.5f) * 0.5f;
                float lx = Mathf.Cos(angle) * 28 * s;
                float ly = Mathf.Sin(angle) * 18 * s;
                // Upper segment
                GUI.DrawTexture(new Rect(cx - 28 * s + i * 2 * s, cy - 14 * s + i * 6 * s, lx * 0.6f, 3 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 10 * s + i * 2 * s, cy - 14 * s + i * 6 * s, lx * 0.6f, 3 * s), Texture2D.whiteTexture);
                // Lower segment (knee bend)
                GUI.DrawTexture(new Rect(cx - 38 * s + i * 3 * s, cy - 10 * s + i * 7 * s, 12 * s, 2 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 26 * s + i * 3 * s, cy - 10 * s + i * 7 * s, 12 * s, 2 * s), Texture2D.whiteTexture);
            }
            // Abdomen pattern
            GUI.color = new Color(dark.r, dark.g, dark.b, 0.4f);
            GUI.DrawTexture(new Rect(cx - 8 * s, cy + 2 * s, 16 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 6 * s, cy + 10 * s, 12 * s, 3 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawGrasshopper(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            // Long body
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 26 * s, cy - 10 * s, 52 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 22 * s, cy - 7 * s, 44 * s, 14 * s), Texture2D.whiteTexture);
            // Head
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx + dir * 18 * s - 8 * s, cy - 22 * s, 16 * s, 16 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx + dir * 18 * s - 6 * s, cy - 20 * s, 12 * s, 12 * s), Texture2D.whiteTexture);
            DrawEyes(cx + dir * 18 * s, cy - 18 * s, s * 0.8f, dir);
            // Antennae
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx + dir * 22 * s, cy - 38 * s, 2 * s, 18 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + dir * 26 * s, cy - 36 * s, 2 * s, 16 * s), Texture2D.whiteTexture);
            // Big hind legs (thick thigh, thin tibia, V-shape)
            GUI.color = dark;
            // Left hind leg
            GUI.DrawTexture(new Rect(cx - 20 * s, cy + 4 * s, 10 * s, 18 * s), Texture2D.whiteTexture); // thick thigh
            GUI.DrawTexture(new Rect(cx - 24 * s, cy + 18 * s, 3 * s, 22 * s), Texture2D.whiteTexture); // thin tibia
            // Right hind leg
            GUI.DrawTexture(new Rect(cx + 10 * s, cy + 4 * s, 10 * s, 18 * s), Texture2D.whiteTexture); // thick thigh
            GUI.DrawTexture(new Rect(cx + 22 * s, cy + 18 * s, 3 * s, 22 * s), Texture2D.whiteTexture); // thin tibia
            // Small front legs
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx + dir * 8 * s - 2 * s, cy + 10 * s, 3 * s, 12 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + dir * 14 * s - 2 * s, cy + 10 * s, 3 * s, 10 * s), Texture2D.whiteTexture);
            // Folded wings (semi-transparent)
            GUI.color = new Color(body.r, body.g, body.b, 0.25f);
            GUI.DrawTexture(new Rect(cx - 16 * s, cy - 12 * s, 28 * s, 8 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawCentipede(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            // 6 segments (elongated body)
            float segW = 14 * s;
            float segH = 12 * s;
            float startX = cx - 3f * segW * 0.5f;
            for (int i = 0; i < 6; i++)
            {
                float sx = startX + i * segW * 0.85f;
                float wave = Mathf.Sin(Time.time * 3f + i * 0.8f) * 2 * s;
                // Segment body
                GUI.color = (i % 2 == 0) ? body : dark;
                GUI.DrawTexture(new Rect(sx, cy - segH * 0.5f + wave, segW, segH), Texture2D.whiteTexture);
                // Legs on each segment (short)
                GUI.color = dark;
                GUI.DrawTexture(new Rect(sx + 2 * s, cy + segH * 0.5f + wave, 3 * s, 8 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(sx + segW - 5 * s, cy + segH * 0.5f + wave, 3 * s, 8 * s), Texture2D.whiteTexture);
            }
            // Head (first segment)
            float headX = (dir > 0) ? startX - 10 * s : startX + 5.5f * segW * 0.85f;
            float headWave = Mathf.Sin(Time.time * 3f) * 2 * s;
            GUI.color = dark;
            GUI.DrawTexture(new Rect(headX, cy - 10 * s + headWave, 14 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(headX + 2 * s, cy - 8 * s + headWave, 10 * s, 10 * s), Texture2D.whiteTexture);
            DrawEyes(headX + 7 * s, cy - 6 * s + headWave, s * 0.7f, dir);
            // Poison pincers
            GUI.color = new Color(0.8f, 0.2f, 0.1f);
            GUI.DrawTexture(new Rect(headX + dir * 8 * s, cy - 4 * s + headWave, 6 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(headX + dir * 8 * s, cy + 2 * s + headWave, 6 * s, 3 * s), Texture2D.whiteTexture);
            // Antennae
            GUI.color = body;
            GUI.DrawTexture(new Rect(headX + dir * 6 * s, cy - 20 * s + headWave, 2 * s, 12 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(headX + dir * 10 * s, cy - 18 * s + headWave, 2 * s, 10 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawLadybug(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            // Round hemispherical red body
            Color redBody = new Color(0.9f, 0.15f, 0.1f);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 24 * s, cy - 18 * s, 48 * s, 34 * s), Texture2D.whiteTexture);
            GUI.color = redBody;
            GUI.DrawTexture(new Rect(cx - 20 * s, cy - 15 * s, 40 * s, 28 * s), Texture2D.whiteTexture);
            // Wing split line
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 1 * s, cy - 15 * s, 2 * s, 28 * s), Texture2D.whiteTexture);
            // Black spots (5-7 dots)
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(cx - 14 * s, cy - 10 * s, 6 * s, 6 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 10 * s, 6 * s, 6 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 12 * s, cy + 2 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 7 * s, cy + 2 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 8 * s, cy + 10 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 5 * s, cy + 10 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 3 * s, cy - 6 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            // Small black head
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 28 * s, 16 * s, 14 * s), Texture2D.whiteTexture);
            // Eyes (white dots on black head)
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - 5 * s + dir * 2 * s, cy - 24 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s + dir * 2 * s, cy - 24 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(cx - 4 * s + dir * 2 * s, cy - 23 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3 * s + dir * 2 * s, cy - 23 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            // Short legs
            GUI.color = Color.black;
            for (int i = 0; i < 3; i++)
            {
                float lx = (i - 1) * 10 * s;
                GUI.DrawTexture(new Rect(cx + lx - 2 * s, cy + 13 * s, 4 * s, 8 * s), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        private void DrawCaterpillar(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            // 6 plump round segments in a row
            float segR = 10 * s;
            float startX = cx - 2.5f * segR * 1.4f;
            for (int i = 0; i < 6; i++)
            {
                float sx = startX + i * segR * 1.4f;
                float bounce = Mathf.Sin(Time.time * 2.5f + i * 0.6f) * 2 * s;
                // Segment (round-ish)
                Color segCol = (i % 2 == 0) ? body : light;
                GUI.color = dark;
                GUI.DrawTexture(new Rect(sx - segR - 1 * s, cy - segR - 1 * s + bounce, segR * 2 + 2 * s, segR * 2 + 2 * s), Texture2D.whiteTexture);
                GUI.color = segCol;
                GUI.DrawTexture(new Rect(sx - segR, cy - segR + bounce, segR * 2, segR * 2), Texture2D.whiteTexture);
                // Tiny legs under each segment
                GUI.color = dark;
                GUI.DrawTexture(new Rect(sx - 3 * s, cy + segR + bounce, 3 * s, 5 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(sx + 1 * s, cy + segR + bounce, 3 * s, 5 * s), Texture2D.whiteTexture);
            }
            // Head is the first or last segment (based on direction)
            float headX = dir > 0 ? startX - segR * 1.0f : startX + 5 * segR * 1.4f + segR * 1.0f;
            float headBounce = Mathf.Sin(Time.time * 2.5f) * 2 * s;
            GUI.color = dark;
            GUI.DrawTexture(new Rect(headX - segR * 1.2f, cy - segR * 1.3f + headBounce, segR * 2.4f, segR * 2.4f), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(headX - segR * 1.0f, cy - segR * 1.1f + headBounce, segR * 2.0f, segR * 2.0f), Texture2D.whiteTexture);
            // Big cute eyes
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(headX - 6 * s + dir * 3 * s, cy - 8 * s + headBounce, 7 * s, 7 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(headX + 1 * s + dir * 3 * s, cy - 8 * s + headBounce, 7 * s, 7 * s), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(headX - 4 * s + dir * 4 * s, cy - 5 * s + headBounce, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(headX + 3 * s + dir * 4 * s, cy - 5 * s + headBounce, 4 * s, 4 * s), Texture2D.whiteTexture);
            // Shiny eye highlights
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(headX - 3 * s + dir * 4 * s, cy - 6 * s + headBounce, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(headX + 4 * s + dir * 4 * s, cy - 6 * s + headBounce, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawAnt(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            // 3-part body: abdomen (rear), thorax (middle), head (front)
            // Abdomen (large oval)
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - dir * 20 * s - 12 * s, cy - 10 * s, 24 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - dir * 20 * s - 10 * s, cy - 8 * s, 20 * s, 16 * s), Texture2D.whiteTexture);
            // Thin waist connector
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - dir * 6 * s - 2 * s, cy - 3 * s, 4 * s, 6 * s), Texture2D.whiteTexture);
            // Thorax (medium)
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 12 * s, 16 * s, 16 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 10 * s, 12 * s, 12 * s), Texture2D.whiteTexture);
            // Head
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx + dir * 14 * s - 8 * s, cy - 18 * s, 16 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx + dir * 14 * s - 6 * s, cy - 16 * s, 12 * s, 10 * s), Texture2D.whiteTexture);
            DrawEyes(cx + dir * 14 * s, cy - 14 * s, s * 0.7f, dir);
            // Mandibles (big jaws)
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx + dir * 22 * s, cy - 12 * s, 8 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + dir * 22 * s, cy - 6 * s, 8 * s, 3 * s), Texture2D.whiteTexture);
            // Bent antennae (elbow shape)
            GUI.color = body;
            // First segment (vertical)
            GUI.DrawTexture(new Rect(cx + dir * 16 * s - 1 * s, cy - 30 * s, 2 * s, 14 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + dir * 20 * s - 1 * s, cy - 28 * s, 2 * s, 12 * s), Texture2D.whiteTexture);
            // Second segment (angled outward)
            GUI.DrawTexture(new Rect(cx + dir * 14 * s, cy - 38 * s, 6 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + dir * 18 * s, cy - 36 * s, 8 * s, 2 * s), Texture2D.whiteTexture);
            // 3 pairs of legs
            DrawLegs(cx, cy, s, dark, 3);
            GUI.color = Color.white;
        }

        private void DrawStickInsect(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            // Very long and thin straight body
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 3 * s, cy - 32 * s, 6 * s, 64 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 2 * s, cy - 30 * s, 4 * s, 60 * s), Texture2D.whiteTexture);
            // Body segments (subtle lines)
            GUI.color = dark;
            for (int i = 0; i < 5; i++)
            {
                GUI.DrawTexture(new Rect(cx - 2 * s, cy - 20 * s + i * 10 * s, 4 * s, 1 * s), Texture2D.whiteTexture);
            }
            // Small head
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 38 * s, 10 * s, 8 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 37 * s, 8 * s, 6 * s), Texture2D.whiteTexture);
            DrawEyes(cx, cy - 36 * s, s * 0.5f, dir);
            // 3 pairs of very thin long legs
            GUI.color = dark;
            for (int i = 0; i < 3; i++)
            {
                float legY = cy - 16 * s + i * 14 * s;
                // Left leg segments
                GUI.DrawTexture(new Rect(cx - 28 * s, legY, 26 * s, 2 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 36 * s, legY + 2 * s, 10 * s, 2 * s), Texture2D.whiteTexture);
                // Right leg segments
                GUI.DrawTexture(new Rect(cx + 3 * s, legY, 26 * s, 2 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 27 * s, legY + 2 * s, 10 * s, 2 * s), Texture2D.whiteTexture);
            }
            // Short antennae
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 3 * s, cy - 46 * s, 2 * s, 10 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy - 44 * s, 2 * s, 8 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawFly(float cx, float cy, float s, float dir, Color body, Color dark, Color light)
        {
            // Small body
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 8 * s, 20 * s, 18 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 6 * s, 16 * s, 14 * s), Texture2D.whiteTexture);
            // Huge red compound eyes (dominate the head)
            Color eyeRed = new Color(0.8f, 0.15f, 0.1f);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 14 * s, cy - 26 * s, 28 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = eyeRed;
            GUI.DrawTexture(new Rect(cx - 12 * s, cy - 24 * s, 11 * s, 16 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 1 * s, cy - 24 * s, 11 * s, 16 * s), Texture2D.whiteTexture);
            // Eye highlight
            GUI.color = new Color(1f, 0.4f, 0.3f, 0.5f);
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 22 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 5 * s, cy - 22 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            // Pupil
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(cx - 8 * s + dir * 2 * s, cy - 18 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 5 * s + dir * 2 * s, cy - 18 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            // Transparent wings (2)
            float wingFlap = Mathf.Sin(Time.time * 8f) * 3 * s;
            GUI.color = new Color(0.8f, 0.9f, 1f, 0.25f);
            GUI.DrawTexture(new Rect(cx - 30 * s, cy - 16 * s + wingFlap, 22 * s, 10 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 16 * s - wingFlap, 22 * s, 10 * s), Texture2D.whiteTexture);
            // Wing veins
            GUI.color = new Color(0.6f, 0.7f, 0.8f, 0.3f);
            GUI.DrawTexture(new Rect(cx - 24 * s, cy - 12 * s + wingFlap, 14 * s, 1 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 12 * s, cy - 12 * s - wingFlap, 14 * s, 1 * s), Texture2D.whiteTexture);
            // Mosquito proboscis (long snout) - check if mosquito
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx + dir * 8 * s - 1 * s, cy - 14 * s, 2 * s, 16 * s), Texture2D.whiteTexture);
            // Short legs
            DrawLegs(cx, cy + 4 * s, s * 0.8f, dark, 3);
            GUI.color = Color.white;
        }

        private void DrawHpBars()
        {
            if (playerStats == null || enemyStats == null) return;
            Rect safe = UISafeLayout.Content;
            Rect player = DuelHudLayout.HpCard(safe, true);
            Rect enemy = DuelHudLayout.HpCard(safe, false);
            DrawHpBox(player.x, player.y, player.width, playerStats, displayPlayerHp, true);
            DrawHpBox(enemy.x, enemy.y, enemy.width, enemyStats, displayEnemyHp, false);
            DrawLedgerGauge(enemy.x, enemy.yMax + UITheme.Space.S, enemy.width);
        }

        private void DrawLedgerGauge(float x, float y, float w)
        {
            if (battleController == null) return;
            int threshold = battleController.LedgerThreshold;
            if (!LedgerPressure.IsActive(threshold)) return;

            int tally = battleController.LedgerTally;
            float fill = LedgerPressure.Fill01(tally, threshold);
            // 단계 판정은 순수부가 든다 — 경고 여부만 보고 색을 고르면 **가득 찬 순간**
            // 평상색으로 되돌아간다(IsWarning은 정의상 IsFull일 때 false다).
            LedgerAlert alert = LedgerPressure.AlertOf(tally, threshold);
            const float h = 26f;

            UISurface.Flat(new Rect(x, y, w, h), new Color(0.09f, 0.09f, 0.14f, 0.94f));
            if (fill > 0.001f)
            {
                Color col;
                if (alert == LedgerAlert.Marked)
                {
                    // 이미 적힌 상태는 여러 턴 이어질 수 있다(때릴 자리가 날 때까지 기다린다).
                    // 정지된 붉은 막대는 그냥 배경이 되므로 맥동으로 "아직 살아 있음"을 남긴다.
                    float pulse = 0.78f + 0.22f * Mathf.PingPong(Time.time * 1.6f, 1f);
                    col = new Color(1f * pulse, 0.22f * pulse, 0.2f * pulse);
                }
                else if (alert == LedgerAlert.Warning) col = new Color(0.95f, 0.35f, 0.3f);
                else col = new Color(0.85f, 0.72f, 0.35f);
                UISurface.Flat(new Rect(x, y, w * fill, h), col);
            }

            Color prev = hpTextCache.normal.textColor;
            hpTextCache.normal.textColor = alert == LedgerAlert.Calm
                ? new Color(0.93f, 0.9f, 0.78f)
                : new Color(1f, 0.86f, 0.82f);
            string label = alert == LedgerAlert.Marked ? "장부 — 이미 적혔다"
                : alert == LedgerAlert.Warning ? "장부 — 곧 적힌다"
                : "장부";
            // 문구 길이가 상태로 갈리므로 LabelFit으로 상자에 맞춘다(상자를 키우면 아레나를 침범한다).
            UIHelper.LabelFit(new Rect(x, y, w, h), label, hpTextCache);
            // **되돌려 놓는다.** 이 스타일은 아래 HP 숫자가 이어서 쓴다(흰색 전제).
            hpTextCache.normal.textColor = prev;
        }

        private void DrawHpBox(float x, float y, float w, InsectBattleStats stats, float dispHp, bool isPlayer)
        {
            UITheme theme = UITheme.Instance;
            Color affiliation = isPlayer ? theme.accentMint : theme.accentCoral;
            Rect card = new Rect(x, y, w, DuelHudLayout.HpCardHeight);
            UISurface.Card(card, theme.surfaceBase, theme.surfaceBorder);
            UISurface.Flat(new Rect(x + UITheme.Radius.Card, y + 3f, w - UITheme.Radius.Card * 2f, 3f), affiliation);
            hpMiniStatCache.normal.textColor = affiliation;
            UIHelper.LabelFit(new Rect(x + 18f, y + 10f, w - 120f, 24f), isPlayer ? "내 파트너" : "상대 곤충", hpMiniStatCache);
            hpLvStyleCache.normal.textColor = theme.textSecondary;
            GUI.Label(new Rect(x + w - 102f, y + 12f, 84f, 30f), $"Lv. {stats.Level}", hpLvStyleCache);
            hpNameStyleCache.normal.textColor = theme.textPrimary;
            hpNameStyleCache.fontSize = 28;
            UIHelper.LabelFit(new Rect(x + 18f, y + 36f, w - 36f, 38f), stats.Data.displayName, hpNameStyleCache);

            float ratio = stats.MaxHp > 0 ? Mathf.Clamp01(dispHp / stats.MaxHp) : 0f;
            float chip = isPlayer ? chipPlayerHp : chipEnemyHp;
            float chipRatio = stats.MaxHp > 0 ? Mathf.Clamp01(chip / stats.MaxHp) : 0f;
            Rect track = new Rect(x + 18f, y + 80f, w - 36f, 10f);
            UISurface.Flat(track, theme.surfaceRaised);
            if (chipRatio > ratio)
                UISurface.Flat(new Rect(track.x, track.y, track.width * chipRatio, track.height), theme.accentAmber);
            Color hpColor = ratio > 0.2f ? affiliation : theme.accentAmber;
            UISurface.Flat(new Rect(track.x, track.y, track.width * ratio, track.height), hpColor);
            hpTextCache.normal.textColor = theme.textPrimary;
            hpTextCache.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(x + w - 186f, y + 96f, 168f, 28f), $"{Mathf.CeilToInt(dispHp)} / {stats.MaxHp}", hpTextCache);

            string status = ratio <= 0.2f ? "체력 위험" : "HP";
            InsectBattleController.EffectSnapshot[] effects = battleController != null ? battleController.GetActiveEffects() : null;
            if (effects != null)
                foreach (var effect in effects)
                {
                    if (effect.targetIsPlayer != isPlayer) continue;
                    string tag = effect.kind == InsectBattleController.EffectKind.DefBuff ? "방어↑"
                        : effect.kind == InsectBattleController.EffectKind.Dot ? "중독"
                        : effect.value >= 0 ? "공격↑" : "공격↓";
                    status += $"  {tag} {effect.remainingTurns}턴";
                }
            hpEffStyleCache.normal.textColor = ratio <= 0.2f ? theme.accentAmber : theme.textSecondary;
            UIHelper.LabelFit(new Rect(x + 18f, y + 96f, w - 216f, 30f), status, hpEffStyleCache);
        }

        private void DrawIntro()
        {
            if (playerStats == null || enemyStats == null) return;
            Rect panel = UISafeLayout.BottomPanel(720f, 144f);
            UISurface.Card(panel, UITheme.Instance.surfaceBase, UITheme.Instance.surfaceBorder);
            introEncounterStyleCache.normal.textColor = UITheme.Instance.textPrimary;
            UIHelper.LabelFit(new Rect(panel.x + 24f, panel.y + 18f, panel.width - 48f, 50f),
                $"{enemyStats.Data.displayName}와 마주쳤다", introEncounterStyleCache);
            rewardStyleCache.normal.textColor = UITheme.Instance.textSecondary;
            UIHelper.LabelFit(new Rect(panel.x + 24f, panel.y + 78f, panel.width - 48f, 40f),
                "파트너의 기술과 상성을 살펴보세요", rewardStyleCache);
        }

        private void DrawSkillPanel()
        {
            if (playerStats == null || playerStats.Data == null) return;
            UITheme theme = UITheme.Instance;
            bool portrait = UIScale.IsPortrait;
            bool mobile = UIScale.IsMobileLayout;
            Rect panel = UISafeLayout.BottomPanel(UISafeLayout.ContentWidth, portrait ? 610f : 300f);
            UISurface.Card(panel, theme.surfaceBase, theme.surfaceBorder);
            skillHeaderCache.normal.textColor = theme.textPrimary;
            skillHeaderCache.fontSize = 26;
            UIHelper.LabelFit(new Rect(panel.x + 20f, panel.y + 12f, panel.width * 0.58f, 38f),
                "파트너의 행동을 선택하세요", skillHeaderCache);
            skillCdInfoCache.normal.textColor = theme.textSecondary;
            UIHelper.LabelFit(new Rect(panel.x + panel.width * 0.6f, panel.y + 16f, panel.width * 0.4f - 20f, 30f),
                mobile ? "기술을 눌러 사용" : "1–4 기술  ·  F 기본 공격", skillCdInfoCache);

            InsectSkill[] skills = battleController != null ? battleController.GetPlayerSkills() : playerStats.Data.skills;
            int[] cooldowns = battleController != null ? battleController.GetPlayerCooldowns() : null;
            int count = skills != null ? Mathf.Min(skills.Length, 4) : 0;
            skillBtnCount = count;
            for (int i = 0; i < skillBtnRects.Length; i++)
            { skillBtnRects[i] = Rect.zero; skillBtnUsable[i] = false; }

            for (int i = 0; i < count; i++)
            {
                InsectSkill skill = skills[i];
                if (skill == null) continue;
                Rect card = DuelHudLayout.SkillCard(panel, portrait, i, count);
                int cd = cooldowns != null && i < cooldowns.Length ? cooldowns[i] : 0;
                bool canUse = battleController != null ? battleController.CanUseSkill(i) : cd <= 0;
                skillBtnRects[i] = card;
                skillBtnUsable[i] = canUse;
                bool hover = canUse && card.Contains(UIScale.VirtualMousePosition);
                UISurface.Card(card, hover ? theme.surfaceRaised : theme.surfaceCard,
                    hover ? theme.accentMint : theme.surfaceBorder);
                Color element = SkillUILayout.GetReadableAccent(GetElementColor(skill.element));
                UISurface.Chip(new Rect(card.x + 14f, card.y + 12f, Mathf.Min(132f, card.width - 70f), 30f),
                    InsectTypeChart.GetDisplayName(skill.element), theme.surfaceBase, canUse ? element : theme.textSecondary);
                if (!mobile)
                    UISurface.Chip(new Rect(card.xMax - 44f, card.y + 12f, 30f, 30f),
                        (i + 1).ToString(), theme.surfaceBase, theme.textSecondary);
                skillNameStyleCache.fontSize = mobile ? 28 : 26;
                skillNameStyleCache.normal.textColor = canUse ? theme.textPrimary : SkillUILayout.DisabledTextColor;
                UIHelper.LabelFit(new Rect(card.x + 14f, card.y + 48f, card.width - 28f, 44f), skill.displayName, skillNameStyleCache);
                skillTypeLabelCache.normal.textColor = theme.textSecondary;
                bool self = DuelHudLayout.TargetsSelf(skill.effectType);
                string target = self ? "자신" : enemyStats != null && enemyStats.Data != null ? enemyStats.Data.displayName : "상대";
                UIHelper.LabelFit(new Rect(card.x + 14f, card.y + 96f, card.width - 28f, 28f),
                    $"{SkillActionLabel(skill.effectType)} · 대상 {target}", skillTypeLabelCache);
                skillInfoStyleCache.normal.textColor = theme.textPrimary;
                UIHelper.LabelFit(new Rect(card.x + 14f, card.y + 128f, card.width - 28f, 28f), SkillPowerLabel(skill), skillInfoStyleCache);
                string availability = cd > 0 ? $"재사용까지 {cd}턴" : !canUse ? "지금 사용할 수 없음"
                    : skill.cooldownTurns > 0 ? $"사용 후 {skill.cooldownTurns}턴 대기" : "매 턴 사용 가능";
                skillCdStyleCache.alignment = TextAnchor.MiddleLeft;
                skillCdStyleCache.normal.textColor = cd > 0 ? theme.accentAmber : theme.textSecondary;
                UIHelper.LabelFit(new Rect(card.x + 14f, card.y + 162f, card.width - 28f, 28f), availability, skillCdStyleCache);
                if (skill.effectType == SkillEffectType.Damage && enemyStats != null && enemyStats.Data != null)
                {
                    float effectiveness = InsectTypeChart.GetEffectiveness(skill.element, enemyStats.Data.primaryType, enemyStats.Data.secondaryType);
                    if (effectiveness > 1.05f || effectiveness < 0.95f)
                    {
                        skillEffLabelCache.alignment = TextAnchor.MiddleLeft;
                        skillEffLabelCache.normal.textColor = effectiveness > 1f ? theme.accentMint : theme.accentCoral;
                        UIHelper.LabelFit(new Rect(card.x + 14f, card.y + 194f, card.width - 28f, 28f),
                            effectiveness > 1f ? "상성 유리 ↑" : "상성 불리 ↓", skillEffLabelCache);
                    }
                }
            }
            basicAtkRect = DuelHudLayout.UtilityCard(panel, portrait, false);
            DrawUtilityAction(basicAtkRect, mobile ? "기본 공격" : "[F] 기본 공격", "매 턴 사용 가능", false);
            // 챔피언전(샌드박스)에서는 도망가지 않는다 — 그리지 않으면 누를 수도 없다(escapeRect 폭 0이 입력 판정을 끈다).
            if (battleController != null && battleController.IsSandbox)
            {
                escapeRect = new Rect(0, 0, 0, 0);
                return;
            }
            escapeRect = DuelHudLayout.UtilityCard(panel, portrait, true);
            DrawUtilityAction(escapeRect, mobile ? "도망가기" : "[ESC] 도망가기", "확률에 따라 성공", true);
        }

        private void DrawUtilityAction(Rect card, string title, string subtitle, bool escape)
        {
            UITheme theme = UITheme.Instance;
            UISurface.Card(card, card.Contains(UIScale.VirtualMousePosition) ? theme.surfaceRaised : theme.surfaceCard, theme.surfaceBorder);
            GUIStyle titleStyle = escape ? skillEscStyleCache : skillFKeyCache;
            titleStyle.fontSize = 23;
            titleStyle.normal.textColor = escape ? theme.textSecondary : theme.accentMint;
            GUIStyle subtitleStyle = escape ? skillEscInfoCache : skillFInfoCache;
            subtitleStyle.wordWrap = false;
            subtitleStyle.normal.textColor = theme.textSecondary;
            UIHelper.LabelFit(new Rect(card.x + 12f, card.y + 14f, card.width - 24f, 34f), title, titleStyle);
            UIHelper.LabelFit(new Rect(card.x + 12f, card.y + 52f, card.width - 24f, 28f), subtitle, subtitleStyle);
        }

        private static string SkillActionLabel(SkillEffectType t)
        {
            switch (t)
            {
                case SkillEffectType.BuffAttack: return "공격 버프";
                case SkillEffectType.DebuffAttack: return "공격 디버프";
                case SkillEffectType.Heal: return "회복";
                case SkillEffectType.PoisonDot: return "중독";
                case SkillEffectType.Stun: return "기절";
                case SkillEffectType.DefenseBuff: return "방어 버프";
                default: return "공격";
            }
        }

        private static string SkillPowerLabel(InsectSkill skill)
        {
            switch (skill.effectType)
            {
                case SkillEffectType.BuffAttack: return "공격력 UP";
                case SkillEffectType.DebuffAttack: return "공격력 DOWN";
                case SkillEffectType.Heal: return "HP 회복";
                case SkillEffectType.PoisonDot: return $"지속 피해 {skill.power}";
                case SkillEffectType.Stun: return "행동 봉인";
                case SkillEffectType.DefenseBuff: return "방어력 UP";
                default: return $"위력: {skill.power}";
            }
        }

        private Color GetElementColor(InsectElement element)
        {
            switch (element)
            {
                case InsectElement.None: return new Color(0.65f, 0.65f, 0.68f);
                case InsectElement.Bug: return new Color(0.62f, 0.8f, 0.25f);
                case InsectElement.Poison: return new Color(0.6f, 0.2f, 0.8f);
                case InsectElement.Water: return new Color(0.2f, 0.5f, 1f);
                case InsectElement.Leaf: return new Color(0.2f, 0.85f, 0.3f);
                case InsectElement.Wind: return new Color(0.6f, 0.9f, 0.7f);
                case InsectElement.Electric: return new Color(1f, 0.95f, 0.2f);
                case InsectElement.Earth: return new Color(0.7f, 0.5f, 0.2f);
                case InsectElement.Light: return new Color(1f, 0.95f, 0.7f);
                case InsectElement.Dark: return new Color(0.4f, 0.15f, 0.5f);
                case InsectElement.Metal: return new Color(0.7f, 0.75f, 0.8f);
                default: return Color.white;
            }
        }

        private void DrawRotatedLine(float x1, float y1, float x2, float y2, float thickness, Color color)
        {
            Vector2 start = new Vector2(x1, y1);
            Vector2 end = new Vector2(x2, y2);
            float length = Vector2.Distance(start, end);
            if (length < 0.1f) return;
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            Vector2 center = (start + end) / 2f;

            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, center);
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - length / 2f, center.y - thickness / 2f, length, thickness), Texture2D.whiteTexture);
            GUI.matrix = saved;
        }

        private void DrawAttackAnimation(bool isPlayerAttack)
        {
            if (isPlayerAttack && battleController != null && !battleController.PlayerActedThisRound) return;
            // 3D 모드: 2D 이펙트 대신 3D 공격 (BattleArenaController의 코루틴이 처리)
            // 여기서는 데미지 숫자 + 스킬 이름만 OnGUI로 표시
            if (arena != null && arena.IsActive)
            {
                float t3d = 0.3f + Mathf.Clamp01(impactTimer / Mathf.Max(0.1f, attackDuration * 0.6f)) * 0.7f;
                int dmg3d = isPlayerAttack ? lastDamageToEnemy : lastDamageToPlayer;
                if (dmg3d > 0 && impactRevealed)
                {
                    float sw3d = UIScale.VirtualScreenWidth;
                    float sh3d = UIScale.VirtualScreenHeight;
                    Vector3 target = arena.GetCombatantScreenPosition(!isPlayerAttack);
                    float dmgX = target.x / Mathf.Max(1, Screen.width) * sw3d;
                    float dmgY = (1f - target.y / Mathf.Max(1, Screen.height)) * sh3d - 70f - (t3d - 0.3f) * 60f;

                    // 등장 팝(초반 확대 후 정착) + 후반 페이드아웃 — 데미지 숫자 저즈(2D 폴백 패턴 이식).
                    float dmgP = Mathf.Clamp01((t3d - 0.3f) / 0.7f);
                    float dmgAlpha = Mathf.Clamp01(1.15f - dmgP);
                    float popScale = 1f + Mathf.Sin(Mathf.Clamp01(dmgP * 2f) * Mathf.PI) * 0.3f;

                    // 크리티컬이면 더 큰 폰트 + 펄스 + CRITICAL! 라벨
                    bool isCrit = isPlayerAttack && lastWasCritical;
                    float critPulse = isCrit ? 1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 20f)) * 0.15f : 1f;
                    int dmgFontSize = Mathf.RoundToInt((isCrit ? 64f * critPulse : 38f) * popScale);

                    dmgStyle3dCache.fontSize = dmgFontSize;
                    Color dmgTextCol = isCrit
                        ? new Color(1f, 0.5f, 0.1f)
                        : (isPlayerAttack ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 0.3f, 0.3f));
                    dmgTextCol.a = dmgAlpha;
                    dmgStyle3dCache.normal.textColor = dmgTextCol;

                    // 크리티컬 그림자 효과
                    if (isCrit)
                    {
                        GUI.color = new Color(0f, 0f, 0f, 0.8f);
                        GUI.Label(new Rect(dmgX - 147f, dmgY + 3, 300f, 80), $"-{dmg3d}", dmgStyle3dCache);
                        GUI.color = Color.white;
                    }
                    GUI.Label(new Rect(dmgX - 150f, dmgY, 300f, 80), $"-{dmg3d}", dmgStyle3dCache);

                    if (isCrit)
                    {
                        GUI.Label(new Rect(dmgX - 150f, dmgY - 36, 300f, 36), "★ CRITICAL! ★", critLblCache);
                    }

                    if (isPlayerAttack && !string.IsNullOrEmpty(lastSkillName))
                    {
                        float skillY = isCrit ? dmgY + 72 : dmgY + 45;
                        GUI.Label(new Rect(dmgX - 150f, skillY, 300f, 30), lastSkillName, skillStyle3dCache);
                    }
                }
                return;
            }

            float progress = Mathf.Clamp01(phaseTimer / attackDuration);
            // Legacy 2D effects strike at t=.3; the shared presentation strikes at 40%.
            float t = progress < 0.4f ? progress * 0.75f : 0.3f + (progress - 0.4f) * (0.7f / 0.6f);
            float arenaTop = UIScale.VirtualScreenHeight * 0.08f;
            float arenaH = UIScale.VirtualScreenHeight * 0.52f;

            float atkX = isPlayerAttack ? UIScale.VirtualScreenWidth * 0.22f : UIScale.VirtualScreenWidth * 0.72f;
            float atkY = isPlayerAttack ? arenaTop + arenaH * 0.72f : arenaTop + arenaH * 0.38f;
            float tgtX = isPlayerAttack ? UIScale.VirtualScreenWidth * 0.72f : UIScale.VirtualScreenWidth * 0.22f;
            float tgtY = isPlayerAttack ? arenaTop + arenaH * 0.38f : arenaTop + arenaH * 0.72f;

            int dmg = isPlayerAttack ? lastDamageToEnemy : lastDamageToPlayer;

            // Element-based color
            InsectBattleStats atkStats = isPlayerAttack ? playerStats : enemyStats;
            InsectElement element = isPlayerAttack
                ? lastSkillElement
                : (atkStats != null && atkStats.Data != null ? atkStats.Data.primaryType : InsectElement.Bug);
            Color elemCol = GetElementColor(element);

            // Buff/Debuff effect: dmg == 0 with a skill name means buff or debuff
            if (dmg == 0 && !string.IsNullOrEmpty(lastSkillName))
            {
                DrawBuffDebuffEffect(isPlayerAttack, t, atkX, atkY, tgtX, tgtY, elemCol);
                GUI.color = Color.white;
                return;
            }

            // Phase 1: Attacker rushes toward target (position interpolation)
            if (t < 0.35f)
            {
                float rushT = t / 0.35f;
                float easeT = rushT * rushT * (3f - 2f * rushT);

                // Draw the attacker sprite rushing forward
                float rushX = Mathf.Lerp(atkX, tgtX, easeT * 0.6f);
                float rushY = Mathf.Lerp(atkY, tgtY, easeT * 0.6f) - Mathf.Sin(easeT * Mathf.PI) * 40f;

                // Rush trail effect
                for (int i = 1; i <= 5; i++)
                {
                    float trailT = Mathf.Max(0, easeT - i * 0.08f);
                    float tx = Mathf.Lerp(atkX, tgtX, trailT * 0.6f);
                    float ty = Mathf.Lerp(atkY, tgtY, trailT * 0.6f) - Mathf.Sin(trailT * Mathf.PI) * 40f;
                    float trailAlpha = (0.3f - i * 0.05f) * rushT;
                    float trailSize = 20f - i * 3f;
                    GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, trailAlpha);
                    GUI.DrawTexture(new Rect(tx - trailSize, ty - trailSize, trailSize * 2, trailSize * 2), Texture2D.whiteTexture);
                }

                // Projectile glow at rush point
                float projSize = 16f + Mathf.Sin(rushT * Mathf.PI * 3f) * 5f;
                GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, 0.2f);
                GUI.DrawTexture(new Rect(rushX - projSize * 2.5f, rushY - projSize * 2.5f, projSize * 5, projSize * 5), Texture2D.whiteTexture);
                GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, 0.85f);
                GUI.DrawTexture(new Rect(rushX - projSize / 2, rushY - projSize / 2, projSize, projSize), Texture2D.whiteTexture);
                GUI.color = new Color(1, 1, 1, 0.95f);
                GUI.DrawTexture(new Rect(rushX - 4, rushY - 4, 8, 8), Texture2D.whiteTexture);

                // Speed lines behind the rusher
                GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, 0.3f * rushT);
                float lineDir = isPlayerAttack ? 1f : -1f;
                for (int i = 0; i < 4; i++)
                {
                    float ly = rushY - 20 + i * 12;
                    float lx = rushX - lineDir * 30;
                    GUI.DrawTexture(new Rect(lx - lineDir * 40, ly, 40, 2), Texture2D.whiteTexture);
                }
            }

            // Phase 2: Element-specific impact effect
            if (impactRevealed && t < 0.8f && dmg > 0)
            {
                float impactT = (t - 0.3f) / 0.4f;
                DrawElementImpact(tgtX, tgtY, impactT, element, elemCol);
            }

            // Phase 3: Damage numbers and skill name
            if (dmg > 0 && impactRevealed)
            {
                float dmgT = (t - 0.3f) / 0.7f;

                if (!string.IsNullOrEmpty(lastSkillName) && isPlayerAttack)
                {
                    float skillAlpha = Mathf.Clamp01(1f - dmgT * 1.5f);
                    skillNameAtkStyleCache.normal.textColor = new Color(elemCol.r, elemCol.g, elemCol.b, skillAlpha);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(tgtX - 160, tgtY - 130 - dmgT * 25f, 320, 40), lastSkillName, skillNameAtkStyleCache);
                }

                float dmgAlpha = Mathf.Clamp01(1f - dmgT * 0.8f);
                float dmgScale = 1f + Mathf.Sin(dmgT * Mathf.PI * 0.5f) * 0.35f;
                int dmgFontSize = (int)(44 * dmgScale);
                dmgStyleAtkCache.fontSize = dmgFontSize;
                Color dmgCol = isPlayerAttack ? new Color(1, 1, 0.3f, dmgAlpha) : new Color(1, 0.3f, 0.3f, dmgAlpha);
                dmgStyleAtkCache.normal.textColor = dmgCol;
                GUI.color = Color.white;
                GUI.Label(new Rect(tgtX - 70, tgtY - 90 - dmgT * 55f, 140, 55), $"-{dmg}", dmgStyleAtkCache);

                // Effectiveness text based on element
                if (isPlayerAttack && dmgT < 0.5f)
                {
                    float effAlpha = Mathf.Clamp01(1f - dmgT * 2.5f);
                    effStyleAtkCache.normal.textColor = new Color(elemCol.r, elemCol.g, elemCol.b, effAlpha);
                    GUI.Label(new Rect(tgtX - 80, tgtY - 50 - dmgT * 30f, 160, 26), GetElementName(element), effStyleAtkCache);
                }
            }

            if (!isPlayerAttack && dmg > 0 && enemyStats != null && enemyStats.Data != null)
            {
                actionText = $"{enemyStats.Data.displayName}의 반격!";
                actionTimer = 0.8f;
            }

            GUI.color = Color.white;
        }

        private void DrawElementImpact(float tgtX, float tgtY, float impactT, InsectElement element, Color elemCol)
        {
            // Screen flash (common to all elements)
            if (impactT < 0.15f)
            {
                float flashAlpha = (1f - impactT / 0.15f) * 0.15f;
                GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, flashAlpha);
                GUI.DrawTexture(new Rect(0, 0, UIScale.VirtualScreenWidth, UIScale.VirtualScreenHeight), Texture2D.whiteTexture);
            }

            switch (element)
            {
                case InsectElement.Poison:
                    DrawImpactPoison(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Water:
                    DrawImpactWater(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Leaf:
                    DrawImpactLeaf(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Wind:
                    DrawImpactWind(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Electric:
                    DrawImpactElectric(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Earth:
                    DrawImpactEarth(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Light:
                    DrawImpactLight(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Dark:
                    DrawImpactDark(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Metal:
                    DrawImpactMetal(tgtX, tgtY, impactT, elemCol);
                    break;
                default:
                    DrawImpactBug(tgtX, tgtY, impactT, elemCol);
                    break;
            }
        }

        // Bug (default): radial lines + shockwave
        private void DrawImpactBug(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // Central flash
            float centerAlpha = (1f - impactT) * 0.8f;
            float flashSize = 90f + impactT * 70f;
            GUI.color = new Color(1f, 1f, 0.7f, centerAlpha);
            GUI.DrawTexture(new Rect(tgtX - flashSize / 2, tgtY - flashSize / 2, flashSize, flashSize), Texture2D.whiteTexture);

            // Shockwave ring
            float ringRadius = 40f + impactT * 120f;
            float ringThick = 8f * (1f - impactT);
            float ringAlpha = (1f - impactT) * 0.6f;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16;
                float rx = tgtX + Mathf.Cos(a) * ringRadius;
                float ry = tgtY + Mathf.Sin(a) * ringRadius;
                GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, ringAlpha);
                GUI.DrawTexture(new Rect(rx - ringThick / 2, ry - ringThick / 2, ringThick, ringThick), Texture2D.whiteTexture);
            }

            // Radial burst lines
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad + impactT * 1.5f;
                float ls = 20f + impactT * 30f;
                float le = 50f + impactT * 100f;
                float la = (1f - impactT) * 0.7f;
                float x1 = tgtX + Mathf.Cos(a) * ls;
                float y1 = tgtY + Mathf.Sin(a) * ls;
                float x2 = tgtX + Mathf.Cos(a) * le;
                float y2 = tgtY + Mathf.Sin(a) * le;
                DrawRotatedLine(x1, y1, x2, y2, 3f * (1f - impactT * 0.6f), new Color(1f, 1f, 0.7f, la));
            }

            // Sparks
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad + impactT * 2.5f;
                float dist = 35f + impactT * 90f;
                float sx = tgtX + Mathf.Cos(a) * dist;
                float sy = tgtY + Mathf.Sin(a) * dist;
                float sa = (1f - impactT) * 0.85f;
                float ss = 7f * (1f - impactT * 0.7f);
                GUI.color = new Color(1f, 1f, 0.5f, sa);
                GUI.DrawTexture(new Rect(sx - ss / 2, sy - ss / 2, ss, ss), Texture2D.whiteTexture);
            }
        }

        // Poison: purple fog + rising bubbles + toxic wave
        private void DrawImpactPoison(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // Large translucent purple circle expanding
            float fogRadius = 60f + impactT * 100f;
            float fogAlpha = (1f - impactT) * 0.35f;
            GUI.color = new Color(0.5f, 0.1f, 0.7f, fogAlpha);
            GUI.DrawTexture(new Rect(tgtX - fogRadius, tgtY - fogRadius, fogRadius * 2, fogRadius * 2), Texture2D.whiteTexture);

            // Inner darker core
            float coreR = 30f + impactT * 40f;
            GUI.color = new Color(0.3f, 0.0f, 0.5f, fogAlpha * 1.2f);
            GUI.DrawTexture(new Rect(tgtX - coreR, tgtY - coreR, coreR * 2, coreR * 2), Texture2D.whiteTexture);

            // Rising bubbles (6-8)
            for (int i = 0; i < 8; i++)
            {
                float bx = tgtX + Mathf.Sin(i * 1.3f + impactT * 4f) * (25f + i * 8f);
                float by = tgtY - impactT * (60f + i * 20f);
                float bSize = (6f + i * 1.5f) * (1f - impactT * 0.5f);
                float bAlpha = (1f - impactT) * 0.7f;
                GUI.color = new Color(0.6f, 0.15f, 0.85f, bAlpha);
                GUI.DrawTexture(new Rect(bx - bSize, by - bSize, bSize * 2, bSize * 2), Texture2D.whiteTexture);
                // Bubble highlight
                GUI.color = new Color(0.8f, 0.5f, 1f, bAlpha * 0.5f);
                GUI.DrawTexture(new Rect(bx - bSize * 0.3f, by - bSize * 0.6f, bSize * 0.6f, bSize * 0.4f), Texture2D.whiteTexture);
            }

            // Toxic wave ring
            float waveR = 50f + impactT * 80f;
            float waveAlpha = (1f - impactT) * 0.5f;
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12 + impactT * 2f;
                float wx = tgtX + Mathf.Cos(a) * waveR;
                float wy = tgtY + Mathf.Sin(a) * waveR;
                GUI.color = new Color(0.5f, 0.2f, 0.8f, waveAlpha);
                float ws = 6f * (1f - impactT * 0.4f);
                GUI.DrawTexture(new Rect(wx - ws / 2, wy - ws / 2, ws, ws), Texture2D.whiteTexture);
            }
        }

        // Water: concentric ripples + splashing droplets + vertical splash lines
        private void DrawImpactWater(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // 3 concentric ripple rings expanding with stagger
            for (int ring = 0; ring < 3; ring++)
            {
                float delay = ring * 0.12f;
                float rt = Mathf.Clamp01((impactT - delay) / (1f - delay));
                if (rt <= 0f) continue;
                float radius = 30f + rt * (80f + ring * 40f);
                float thick = (6f - ring) * (1f - rt);
                float alpha = (1f - rt) * 0.6f;
                for (int i = 0; i < 20; i++)
                {
                    float a = i * Mathf.PI * 2f / 20;
                    float rx = tgtX + Mathf.Cos(a) * radius;
                    float ry = tgtY + Mathf.Sin(a) * radius;
                    GUI.color = new Color(0.3f, 0.6f, 1f, alpha);
                    GUI.DrawTexture(new Rect(rx - thick / 2, ry - thick / 2, thick, thick), Texture2D.whiteTexture);
                }
            }

            // 8 droplets flying radially outward
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad + 0.3f;
                float dist = 20f + impactT * 110f;
                float dx = tgtX + Mathf.Cos(a) * dist;
                float dy = tgtY + Mathf.Sin(a) * dist + impactT * impactT * 40f; // gravity
                float dAlpha = (1f - impactT) * 0.8f;
                float dSize = 5f + (1f - impactT) * 4f;
                GUI.color = new Color(0.2f, 0.5f, 1f, dAlpha);
                GUI.DrawTexture(new Rect(dx - dSize / 2, dy - dSize / 2, dSize, dSize), Texture2D.whiteTexture);
            }

            // 3 vertical splash lines
            for (int i = -1; i <= 1; i++)
            {
                float lx = tgtX + i * 25f;
                float lyTop = tgtY - 30f - impactT * 70f;
                float lyBot = tgtY + 10f;
                float la = (1f - impactT) * 0.6f;
                DrawRotatedLine(lx, lyBot, lx, lyTop, 3f, new Color(0.3f, 0.6f, 1f, la));
            }

            // Central splash
            float splashSize = 40f + impactT * 30f;
            GUI.color = new Color(0.4f, 0.7f, 1f, (1f - impactT) * 0.5f);
            GUI.DrawTexture(new Rect(tgtX - splashSize / 2, tgtY - splashSize / 2, splashSize, splashSize), Texture2D.whiteTexture);
        }

        // Leaf: crossing slashes + rotating leaf fragments
        private void DrawImpactLeaf(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            float slashAlpha = (1f - impactT) * 0.8f;
            float slashLen = 60f + impactT * 50f;
            Color slashCol = new Color(0.1f, 0.8f, 0.2f, slashAlpha);

            // X-cross slashes using DrawRotatedLine
            DrawRotatedLine(tgtX - slashLen, tgtY - slashLen, tgtX + slashLen, tgtY + slashLen, 4f, slashCol);
            DrawRotatedLine(tgtX + slashLen, tgtY - slashLen, tgtX - slashLen, tgtY + slashLen, 4f, slashCol);

            // Secondary shorter slashes
            float s2 = slashLen * 0.6f;
            Color slashCol2 = new Color(0.2f, 0.9f, 0.3f, slashAlpha * 0.6f);
            DrawRotatedLine(tgtX - s2, tgtY, tgtX + s2, tgtY, 3f, slashCol2);
            DrawRotatedLine(tgtX, tgtY - s2, tgtX, tgtY + s2, 3f, slashCol2);

            // 6 leaf fragments (small rotated rectangles flying outward)
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad + impactT * 3f;
                float dist = 25f + impactT * 80f;
                float fx = tgtX + Mathf.Cos(a) * dist;
                float fy = tgtY + Mathf.Sin(a) * dist;
                float fragAngle = a * Mathf.Rad2Deg + impactT * 360f;
                float fAlpha = (1f - impactT) * 0.7f;
                float fSize = 10f * (1f - impactT * 0.4f);

                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(fragAngle, new Vector2(fx, fy));
                GUI.color = new Color(0.2f, 0.75f + i * 0.03f, 0.15f, fAlpha);
                GUI.DrawTexture(new Rect(fx - fSize, fy - fSize / 3, fSize * 2, fSize * 0.7f), Texture2D.whiteTexture);
                GUI.matrix = saved;
            }

            // Green flash at center
            float gFlash = (1f - impactT) * 0.5f;
            GUI.color = new Color(0.2f, 0.9f, 0.3f, gFlash);
            float gs = 50f + impactT * 30f;
            GUI.DrawTexture(new Rect(tgtX - gs / 2, tgtY - gs / 2, gs, gs), Texture2D.whiteTexture);
        }

        // Wind: swirling arcs + speed lines
        private void DrawImpactWind(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // 3 arc lines rotating and expanding around target
            for (int arc = 0; arc < 3; arc++)
            {
                float baseAngle = arc * 120f * Mathf.Deg2Rad + impactT * 8f;
                float arcRadius = 30f + impactT * (60f + arc * 20f);
                float arcAlpha = (1f - impactT) * 0.7f;
                Color arcCol = new Color(0.5f + arc * 0.1f, 0.85f, 0.65f + arc * 0.05f, arcAlpha);

                // Draw arc as series of short lines
                int segments = 8;
                float arcSpan = Mathf.PI * 0.6f;
                for (int s = 0; s < segments; s++)
                {
                    float a1 = baseAngle + (s / (float)segments) * arcSpan;
                    float a2 = baseAngle + ((s + 1) / (float)segments) * arcSpan;
                    float x1 = tgtX + Mathf.Cos(a1) * arcRadius;
                    float y1 = tgtY + Mathf.Sin(a1) * arcRadius;
                    float x2 = tgtX + Mathf.Cos(a2) * arcRadius;
                    float y2 = tgtY + Mathf.Sin(a2) * arcRadius;
                    DrawRotatedLine(x1, y1, x2, y2, 3f - impactT * 1.5f, arcCol);
                }
            }

            // Speed lines (8 lines radiating with slight curve)
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad + impactT * 1.5f;
                float ls = 40f + impactT * 20f;
                float le = 70f + impactT * 70f;
                float lAlpha = (1f - impactT) * 0.5f;
                float x1 = tgtX + Mathf.Cos(a) * ls;
                float y1 = tgtY + Mathf.Sin(a) * ls;
                float x2 = tgtX + Mathf.Cos(a + 0.1f) * le;
                float y2 = tgtY + Mathf.Sin(a + 0.1f) * le;
                DrawRotatedLine(x1, y1, x2, y2, 2f, new Color(0.6f, 0.95f, 0.7f, lAlpha));
            }

            // Swirl center
            float cAlpha = (1f - impactT) * 0.3f;
            float cSize = 35f + impactT * 20f;
            GUI.color = new Color(0.7f, 1f, 0.8f, cAlpha);
            GUI.DrawTexture(new Rect(tgtX - cSize / 2, tgtY - cSize / 2, cSize, cSize), Texture2D.whiteTexture);
        }

        // Electric: zigzag lightning + yellow flash + sparks
        private void DrawImpactElectric(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // Bright yellow flash
            float flashAlpha = (1f - impactT) * 0.6f;
            float flashSize = 70f + impactT * 50f;
            GUI.color = new Color(1f, 1f, 0.3f, flashAlpha);
            GUI.DrawTexture(new Rect(tgtX - flashSize / 2, tgtY - flashSize / 2, flashSize, flashSize), Texture2D.whiteTexture);

            // Blinking effect (flickers)
            bool flicker = Mathf.Sin(impactT * 40f) > 0f;
            if (flicker)
            {
                GUI.color = new Color(1f, 1f, 0.8f, 0.15f);
                GUI.DrawTexture(new Rect(0, 0, UIScale.VirtualScreenWidth, UIScale.VirtualScreenHeight), Texture2D.whiteTexture);
            }

            // 3-4 zigzag lightning bolts
            for (int bolt = 0; bolt < 4; bolt++)
            {
                float boltAngle = bolt * 90f * Mathf.Deg2Rad + impactT * 1.5f;
                float boltLen = 80f + impactT * 40f;
                float bAlpha = (1f - impactT) * 0.9f;
                Color boltCol = new Color(1f, 1f, 0.2f, bAlpha);

                // Draw zigzag as series of short angled segments
                int segs = 5;
                float segLen = boltLen / segs;
                float px = tgtX;
                float py = tgtY;
                for (int s = 0; s < segs; s++)
                {
                    float zigzag = ((s % 2 == 0) ? 1f : -1f) * (12f + Mathf.Sin(bolt * 2f + s) * 8f);
                    float nx = px + Mathf.Cos(boltAngle) * segLen + Mathf.Cos(boltAngle + Mathf.PI / 2) * zigzag;
                    float ny = py + Mathf.Sin(boltAngle) * segLen + Mathf.Sin(boltAngle + Mathf.PI / 2) * zigzag;
                    DrawRotatedLine(px, py, nx, ny, 3f, boltCol);
                    // Glow around bolt
                    DrawRotatedLine(px, py, nx, ny, 8f, new Color(1f, 1f, 0.5f, bAlpha * 0.2f));
                    px = nx;
                    py = ny;
                }
            }

            // Small sparks
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad + impactT * 5f;
                float dist = 30f + impactT * 60f;
                float sx = tgtX + Mathf.Cos(a) * dist;
                float sy = tgtY + Mathf.Sin(a) * dist;
                float sAlpha = (1f - impactT) * 0.8f;
                float sSize = 4f + Mathf.Sin(i + impactT * 20f) * 3f;
                GUI.color = new Color(1f, 1f, 0.3f, sAlpha);
                GUI.DrawTexture(new Rect(sx - sSize / 2, sy - sSize / 2, sSize, sSize), Texture2D.whiteTexture);
            }
        }

        // Earth: rising pillars + dust + shake lines
        private void DrawImpactEarth(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // 4-5 pillars rising from below
            for (int i = 0; i < 5; i++)
            {
                float px = tgtX - 50f + i * 25f;
                float pillarH = (40f + i * 15f) * Mathf.Clamp01(impactT * 3f);
                float pillarW = 12f + i * 2f;
                float pAlpha = (1f - impactT) * 0.8f;
                Color pillarCol = new Color(0.6f + i * 0.03f, 0.4f + i * 0.02f, 0.15f, pAlpha);
                GUI.color = pillarCol;
                GUI.DrawTexture(new Rect(px - pillarW / 2, tgtY + 10f - pillarH, pillarW, pillarH), Texture2D.whiteTexture);
                // Top highlight
                GUI.color = new Color(0.8f, 0.6f, 0.3f, pAlpha * 0.5f);
                GUI.DrawTexture(new Rect(px - pillarW / 2, tgtY + 10f - pillarH, pillarW, 4f), Texture2D.whiteTexture);
            }

            // Shake lines at bottom
            float shakeAlpha = (1f - impactT) * 0.5f;
            for (int i = 0; i < 6; i++)
            {
                float ly = tgtY + 20f + i * 5f;
                float lx1 = tgtX - 70f + Mathf.Sin(impactT * 30f + i) * 8f;
                float lx2 = tgtX + 70f + Mathf.Sin(impactT * 30f + i + 1f) * 8f;
                DrawRotatedLine(lx1, ly, lx2, ly, 2f, new Color(0.7f, 0.5f, 0.2f, shakeAlpha));
            }

            // Dust particles rising
            for (int i = 0; i < 8; i++)
            {
                float dx = tgtX + Mathf.Sin(i * 2.1f) * (40f + i * 10f);
                float dy = tgtY + 15f - impactT * (30f + i * 12f);
                float dAlpha = (1f - impactT) * 0.6f;
                float dSize = 4f + i * 0.8f;
                GUI.color = new Color(0.65f, 0.5f, 0.3f, dAlpha);
                GUI.DrawTexture(new Rect(dx - dSize / 2, dy - dSize / 2, dSize, dSize), Texture2D.whiteTexture);
            }

            // Ground crack effect
            float crackAlpha = (1f - impactT) * 0.7f;
            DrawRotatedLine(tgtX, tgtY + 10f, tgtX - 40f, tgtY + 25f, 2f, new Color(0.4f, 0.25f, 0.1f, crackAlpha));
            DrawRotatedLine(tgtX, tgtY + 10f, tgtX + 35f, tgtY + 20f, 2f, new Color(0.4f, 0.25f, 0.1f, crackAlpha));
            DrawRotatedLine(tgtX, tgtY + 10f, tgtX + 10f, tgtY + 30f, 2f, new Color(0.4f, 0.25f, 0.1f, crackAlpha));
        }

        // Light: golden beam from above + cross light + star sparkles
        private void DrawImpactLight(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // Beam from top
            float beamW = 40f + Mathf.Sin(impactT * 6f) * 10f;
            float beamAlpha = (1f - impactT) * 0.5f;
            GUI.color = new Color(1f, 0.95f, 0.6f, beamAlpha);
            GUI.DrawTexture(new Rect(tgtX - beamW / 2, 0, beamW, tgtY + 20f), Texture2D.whiteTexture);
            // Beam glow (wider, more transparent)
            GUI.color = new Color(1f, 0.9f, 0.5f, beamAlpha * 0.3f);
            GUI.DrawTexture(new Rect(tgtX - beamW, 0, beamW * 2, tgtY + 20f), Texture2D.whiteTexture);

            // Cross light at target
            float crossLen = 50f + impactT * 40f;
            float crossAlpha = (1f - impactT) * 0.7f;
            Color crossCol = new Color(1f, 1f, 0.8f, crossAlpha);
            DrawRotatedLine(tgtX - crossLen, tgtY, tgtX + crossLen, tgtY, 4f, crossCol);
            DrawRotatedLine(tgtX, tgtY - crossLen, tgtX, tgtY + crossLen, 4f, crossCol);

            // Central glow
            float glowSize = 60f + impactT * 40f;
            GUI.color = new Color(1f, 0.95f, 0.7f, (1f - impactT) * 0.6f);
            GUI.DrawTexture(new Rect(tgtX - glowSize / 2, tgtY - glowSize / 2, glowSize, glowSize), Texture2D.whiteTexture);

            // Star sparkles (6 small diamonds)
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad + impactT * 2f;
                float dist = 40f + impactT * 50f;
                float sx = tgtX + Mathf.Cos(a) * dist;
                float sy = tgtY + Mathf.Sin(a) * dist;
                float sAlpha = (1f - impactT) * 0.8f * (0.5f + 0.5f * Mathf.Sin(impactT * 15f + i * 2f));
                float sSize = 5f;
                // Draw diamond (two rotated lines crossing)
                DrawRotatedLine(sx - sSize, sy, sx + sSize, sy, 2f, new Color(1f, 1f, 0.7f, sAlpha));
                DrawRotatedLine(sx, sy - sSize, sx, sy + sSize, 2f, new Color(1f, 1f, 0.7f, sAlpha));
            }
        }

        // Dark: screen darken + shrinking purple orb + crack lines
        private void DrawImpactDark(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // Screen darkening
            float darkAlpha = (1f - impactT) * 0.3f;
            GUI.color = new Color(0.05f, 0f, 0.1f, darkAlpha);
            GUI.DrawTexture(new Rect(0, 0, UIScale.VirtualScreenWidth, UIScale.VirtualScreenHeight), Texture2D.whiteTexture);

            // Contracting dark-purple orb (starts big, shrinks to center)
            float orbRadius = 80f * (1f - impactT * 0.7f);
            float orbAlpha = (1f - impactT) * 0.7f;
            GUI.color = new Color(0.3f, 0.05f, 0.4f, orbAlpha);
            GUI.DrawTexture(new Rect(tgtX - orbRadius, tgtY - orbRadius, orbRadius * 2, orbRadius * 2), Texture2D.whiteTexture);

            // Inner void
            float innerR = orbRadius * 0.5f;
            GUI.color = new Color(0.1f, 0f, 0.15f, orbAlpha * 1.2f);
            GUI.DrawTexture(new Rect(tgtX - innerR, tgtY - innerR, innerR * 2, innerR * 2), Texture2D.whiteTexture);

            // Crack lines radiating from center
            float crackAlpha = (1f - impactT) * 0.8f;
            Color crackCol = new Color(0.6f, 0.1f, 0.8f, crackAlpha);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad + impactT * 0.5f;
                float len = 40f + impactT * 60f;
                float x2 = tgtX + Mathf.Cos(a) * len;
                float y2 = tgtY + Mathf.Sin(a) * len;
                // Jagged crack: 2-segment line
                float mx = tgtX + Mathf.Cos(a) * len * 0.5f + Mathf.Cos(a + 0.8f) * 10f;
                float my = tgtY + Mathf.Sin(a) * len * 0.5f + Mathf.Sin(a + 0.8f) * 10f;
                DrawRotatedLine(tgtX, tgtY, mx, my, 2.5f, crackCol);
                DrawRotatedLine(mx, my, x2, y2, 2f, crackCol);
            }

            // Pulsing ring
            float pulseR = 50f + Mathf.Sin(impactT * 12f) * 15f;
            float pulseAlpha = (1f - impactT) * 0.4f;
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12;
                float px = tgtX + Mathf.Cos(a) * pulseR;
                float py = tgtY + Mathf.Sin(a) * pulseR;
                GUI.color = new Color(0.5f, 0.1f, 0.6f, pulseAlpha);
                GUI.DrawTexture(new Rect(px - 3, py - 3, 6, 6), Texture2D.whiteTexture);
            }
        }

        // Metal: X-slash + shrapnel + highlight flash
        private void DrawImpactMetal(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // Sharp X-slash
            float slashLen = 65f + impactT * 40f;
            float slashAlpha = (1f - impactT) * 0.85f;
            Color slashCol = new Color(0.8f, 0.85f, 0.9f, slashAlpha);
            DrawRotatedLine(tgtX - slashLen, tgtY - slashLen * 0.7f, tgtX + slashLen, tgtY + slashLen * 0.7f, 4f, slashCol);
            DrawRotatedLine(tgtX + slashLen, tgtY - slashLen * 0.7f, tgtX - slashLen, tgtY + slashLen * 0.7f, 4f, slashCol);

            // Metallic flash at center
            float flashSize = 50f + impactT * 30f;
            GUI.color = new Color(0.9f, 0.95f, 1f, (1f - impactT) * 0.7f);
            GUI.DrawTexture(new Rect(tgtX - flashSize / 2, tgtY - flashSize / 2, flashSize, flashSize), Texture2D.whiteTexture);

            // Metal shrapnel (gray rectangles flying outward with rotation)
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad + 0.2f;
                float dist = 20f + impactT * 90f;
                float fx = tgtX + Mathf.Cos(a) * dist;
                float fy = tgtY + Mathf.Sin(a) * dist;
                float fragAngle = impactT * 400f + i * 40f;
                float fAlpha = (1f - impactT) * 0.75f;
                float fW = 8f * (1f - impactT * 0.3f);
                float fH = 4f * (1f - impactT * 0.3f);

                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(fragAngle, new Vector2(fx, fy));
                GUI.color = new Color(0.6f + i * 0.02f, 0.65f + i * 0.02f, 0.7f, fAlpha);
                GUI.DrawTexture(new Rect(fx - fW / 2, fy - fH / 2, fW, fH), Texture2D.whiteTexture);
                GUI.matrix = saved;
            }

            // Highlight sparkle
            float sparkAlpha = (1f - impactT) * 0.9f * (Mathf.Sin(impactT * 20f) > 0.3f ? 1f : 0.3f);
            float sparkSize = 6f;
            GUI.color = new Color(1f, 1f, 1f, sparkAlpha);
            GUI.DrawTexture(new Rect(tgtX - sparkSize, tgtY - 1, sparkSize * 2, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(tgtX - 1, tgtY - sparkSize, 2, sparkSize * 2), Texture2D.whiteTexture);
        }

        // Buff/Debuff visual effect
        private void DrawBuffDebuffEffect(bool isPlayerAttack, float t, float atkX, float atkY, float tgtX, float tgtY, Color elemCol)
        {
            // Determine positions: buff targets self (player side), debuff targets enemy
            float effectX, effectY;
            bool isBuff;

            if (isPlayerAttack)
            {
                // Player used buff/debuff skill — buff goes on player, debuff on enemy
                // Heuristic: if skill name contains common buff terms, it is a buff
                bool looksLikeBuff = lastSkillName != null &&
                    (lastSkillName.Contains("UP") || lastSkillName.Contains("강화") ||
                     lastSkillName.Contains("버프") || lastSkillName.Contains("올") ||
                     lastSkillName.Contains("증가") || lastSkillName.Contains("방어"));
                isBuff = looksLikeBuff;
                effectX = looksLikeBuff ? atkX : tgtX;
                effectY = looksLikeBuff ? atkY : tgtY;
            }
            else
            {
                // Enemy used buff/debuff — show on player (as target)
                isBuff = false;
                effectX = tgtX;
                effectY = tgtY;
            }

            if (isBuff)
            {
                DrawBuffVisual(effectX, effectY, t, elemCol);
            }
            else
            {
                DrawDebuffVisual(effectX, effectY, t, elemCol);
            }

            // Show skill name
            if (!string.IsNullOrEmpty(lastSkillName) && t < 0.8f)
            {
                float alpha = Mathf.Clamp01(1f - t * 1.3f);
                buffDebuffSkillStyleCache.normal.textColor = new Color(1f, 1f, 1f, alpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(effectX - 160, effectY - 120 - t * 20f, 320, 40), lastSkillName, buffDebuffSkillStyleCache);
            }
        }

        private void DrawBuffVisual(float cx, float cy, float t, Color elemCol)
        {
            // Pulsing aura circle
            float auraR = 50f + Mathf.Sin(t * Mathf.PI * 3f) * 15f;
            float auraAlpha = (1f - t) * 0.35f;
            GUI.color = new Color(0.3f, 0.7f, 1f, auraAlpha);
            GUI.DrawTexture(new Rect(cx - auraR, cy - auraR, auraR * 2, auraR * 2), Texture2D.whiteTexture);

            // Inner glow
            float innerR = auraR * 0.6f;
            GUI.color = new Color(0.4f, 0.9f, 0.5f, auraAlpha * 0.8f);
            GUI.DrawTexture(new Rect(cx - innerR, cy - innerR, innerR * 2, innerR * 2), Texture2D.whiteTexture);

            // Rising arrows (triangles approximated as narrow tall rects)
            for (int i = 0; i < 4; i++)
            {
                float ax = cx - 30f + i * 20f;
                float ay = cy + 20f - t * (80f + i * 15f);
                float arrowAlpha = (1f - t) * 0.8f;
                float arrowH = 16f;
                float arrowW = 8f;

                // Arrow body (vertical line)
                GUI.color = new Color(0.3f, 0.8f, 1f, arrowAlpha);
                GUI.DrawTexture(new Rect(ax - 1.5f, ay, 3f, arrowH), Texture2D.whiteTexture);

                // Arrow head (wider rect at top, approximating triangle)
                GUI.color = new Color(0.3f, 0.9f, 0.5f, arrowAlpha);
                GUI.DrawTexture(new Rect(ax - arrowW / 2, ay - 4f, arrowW, 4f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(ax - arrowW / 4, ay - 7f, arrowW / 2, 3f), Texture2D.whiteTexture);
            }

            // "ATK UP!" text
            float txtAlpha = (1f - t) * 0.9f;
            upStyleCache.normal.textColor = new Color(0.3f, 1f, 0.5f, txtAlpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(cx - 100, cy - 70 - t * 30f, 200, 45), "ATK UP!", upStyleCache);
        }

        private void DrawDebuffVisual(float cx, float cy, float t, Color elemCol)
        {
            // Dark aura
            float auraR = 50f + Mathf.Sin(t * Mathf.PI * 2f) * 10f;
            float auraAlpha = (1f - t) * 0.4f;
            GUI.color = new Color(0.3f, 0.05f, 0.05f, auraAlpha);
            GUI.DrawTexture(new Rect(cx - auraR, cy - auraR, auraR * 2, auraR * 2), Texture2D.whiteTexture);

            // Descending arrows
            for (int i = 0; i < 4; i++)
            {
                float ax = cx - 30f + i * 20f;
                float ay = cy - 20f + t * (60f + i * 12f);
                float arrowAlpha = (1f - t) * 0.8f;
                float arrowH = 16f;
                float arrowW = 8f;

                // Arrow body (vertical line going down)
                GUI.color = new Color(1f, 0.2f, 0.2f, arrowAlpha);
                GUI.DrawTexture(new Rect(ax - 1.5f, ay - arrowH, 3f, arrowH), Texture2D.whiteTexture);

                // Arrow head at bottom
                GUI.color = new Color(1f, 0.15f, 0.15f, arrowAlpha);
                GUI.DrawTexture(new Rect(ax - arrowW / 2, ay, arrowW, 4f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(ax - arrowW / 4, ay + 4f, arrowW / 2, 3f), Texture2D.whiteTexture);
            }

            // "ATK DOWN!" text
            float txtAlpha = (1f - t) * 0.9f;
            downStyleCache.normal.textColor = new Color(1f, 0.2f, 0.2f, txtAlpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(cx - 100, cy - 70 - t * 30f, 200, 45), "ATK DOWN!", downStyleCache);

            // Red flicker
            if (Mathf.Sin(t * 25f) > 0.5f)
            {
                GUI.color = new Color(1f, 0f, 0f, 0.08f);
                GUI.DrawTexture(new Rect(cx - 60, cy - 60, 120, 120), Texture2D.whiteTexture);
            }
        }

        private string GetElementName(InsectElement element)
        {
            switch (element)
            {
                case InsectElement.Poison: return "독 속성";
                case InsectElement.Water: return "물 속성";
                case InsectElement.Leaf: return "풀 속성";
                case InsectElement.Wind: return "바람 속성";
                case InsectElement.Electric: return "전기 속성";
                case InsectElement.Earth: return "땅 속성";
                case InsectElement.Light: return "빛 속성";
                case InsectElement.Dark: return "어둠 속성";
                case InsectElement.Metal: return "강철 속성";
                default: return "벌레 속성";
            }
        }

        private void DrawActionText()
        {
            if (string.IsNullOrEmpty(actionText) || phase == Phase.PlayerTurn || resultShown) return;
            Rect footer = UISafeLayout.BottomPanel(720f, 132f);
            Rect panel = new Rect(footer.x, footer.y, footer.width, 54f);
            UISurface.Card(panel, UITheme.Instance.surfaceBase, UITheme.Instance.surfaceBorder);
            actionTextStyleCache.fontSize = 24;
            actionTextStyleCache.normal.textColor = UITheme.Instance.textPrimary;
            UIHelper.LabelFit(new Rect(panel.x + 16f, panel.y + 8f, panel.width - 32f, 38f), actionText, actionTextStyleCache);
        }

        internal static string GetCaptureResultMessage(bool attempted, bool succeeded)
        {
            return !attempted ? string.Empty : succeeded ? "곤충을 포획했습니다!" : "곤충을 잡지 못했습니다.";
        }

        private void DrawResult()
        {
            UITheme theme = UITheme.Instance;
            UISurface.Dim(0.3f);
            Rect panel = UISafeLayout.CenteredPanel(680f, 320f);
            UISurface.Card(panel, theme.surfaceBase, theme.surfaceBorder);
            bool escaped = battleController != null && battleController.DidEscape;
            bool sandbox = battleController != null && battleController.IsSandbox;
            Color accent = lastWon || escaped ? theme.accentMint : theme.accentCoral;
            UISurface.Flat(new Rect(panel.x + 16f, panel.y + 3f, panel.width - 32f, 4f), accent);
            victoryStyleCache.fontSize = 42;
            victoryStyleCache.normal.textColor = accent;
            UIHelper.LabelFit(new Rect(panel.x + 24f, panel.y + 26f, panel.width - 48f, 62f),
                sandbox ? "챔피언 승리!" : escaped ? "무사히 이탈" : lastWon ? "전투 승리" : "다음 탐험을 준비해요", victoryStyleCache);
            rewardStyleCache.normal.textColor = theme.textSecondary;
            if (sandbox)
            {
                // 꿈속의 승리라 받는 것이 없다 — 0을 늘어놓는 보상 칸 대신 한 줄.
                UIHelper.LabelFit(new Rect(panel.x + 32f, panel.y + 124f, panel.width - 64f, 90f),
                    "관중석에서 함성이 쏟아진다", rewardStyleCache);
                return;
            }
            if (lastWon && battleController != null)
            {
                string capture = GetCaptureResultMessage(battleController.GetLastCaptureAttempted(), battleController.GetLastCaptureSucceeded());
                UIHelper.LabelFit(new Rect(panel.x + 24f, panel.y + 100f, panel.width - 48f, 40f),
                    string.IsNullOrEmpty(capture) ? "파트너와 함께 성장했습니다" : capture, rewardStyleCache);
                rewardValStyleCache.normal.textColor = theme.textPrimary;
                UISurface.Rounded(new Rect(panel.x + 24f, panel.y + 158f, panel.width - 48f, 62f), theme.surfaceCard);
                UIHelper.LabelFit(new Rect(panel.x + 32f, panel.y + 170f, (panel.width - 64f) * 0.5f, 38f),
                    $"캔디  +{battleController.GetLastCandyReward()}", rewardValStyleCache);
                UIHelper.LabelFit(new Rect(panel.center.x, panel.y + 170f, (panel.width - 64f) * 0.5f, 38f),
                    $"경험치  +{battleController.GetLastExpReward()}", rewardValStyleCache);
            }
            else if (escaped)
                UIHelper.LabelFit(new Rect(panel.x + 32f, panel.y + 116f, panel.width - 64f, 64f),
                    "전투를 벗어났습니다.\n탐험을 계속할 수 있습니다.", rewardStyleCache);
            else
                UIHelper.LabelFit(new Rect(panel.x + 32f, panel.y + 116f, panel.width - 64f, 64f),
                    "보유 곤충에서 팀과 기술을 확인하고\n훈련으로 파트너를 성장시켜 보세요", rewardStyleCache);
            defeatHintStyleCache.normal.textColor = theme.textSecondary;
            UIHelper.LabelFit(new Rect(panel.x + 24f, panel.y + 250f, panel.width - 48f, 32f),
                "잠시 후 탐험으로 돌아갑니다", defeatHintStyleCache);
            DrawDuelResultQuote(panel);
        }

        private void DrawPhaseIndicator(string text)
        {
            Rect panel = UISafeLayout.BottomPanel(720f, 62f);
            UISurface.Card(panel, UITheme.Instance.surfaceBase, UITheme.Instance.surfaceBorder);
            phaseIndicatorStyleCache.normal.textColor = phase == Phase.EnemyAttack ? UITheme.Instance.accentCoral : UITheme.Instance.accentMint;
            UIHelper.LabelFit(new Rect(panel.x + 18f, panel.y + 10f, panel.width - 36f, 42f), text, phaseIndicatorStyleCache);
        }

        private void DrawTurnAnnounce()
        {
            Rect panel = UISafeLayout.BottomPanel(620f, 98f);
            UISurface.Card(panel, UITheme.Instance.surfaceBase, UITheme.Instance.surfaceBorder);
            introVsStyleCache.fontSize = 34;
            introVsStyleCache.normal.textColor = announceIsPlayer ? UITheme.Instance.accentMint : UITheme.Instance.accentCoral;
            UIHelper.LabelFit(new Rect(panel.x + 20f, panel.y + 20f, panel.width - 40f, 56f), announceText, introVsStyleCache);
        }

        private InsectBattleUIController cachedCanvasBattleUI;
        private RegionManager cachedRegionMgr;

        private void DisableCanvasBattleUI()
        {
            if (cachedCanvasBattleUI == null)
                cachedCanvasBattleUI = FindFirstObjectByType<InsectBattleUIController>();
            if (cachedCanvasBattleUI != null)
            {
                cachedCanvasBattleUI.ForceHidePanel();
            }
        }

        private void DrawSwapSelect()
        {
            UITheme theme = UITheme.Instance;
            bool portrait = UIScale.IsPortrait;
            bool mobile = UIScale.IsMobileLayout;
            Rect panel = UISafeLayout.BottomPanel(Mathf.Min(UISafeLayout.ContentWidth, 1480f), portrait ? 610f : 340f);
            UISurface.Card(panel, theme.surfaceBase, theme.surfaceBorder);
            swapHeaderCache.normal.textColor = theme.textPrimary;
            swapHeaderCache.alignment = TextAnchor.MiddleLeft;
            string faintedName = playerStats != null && playerStats.Data != null ? playerStats.Data.displayName : "파트너";
            UIHelper.LabelFit(new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, 40f),
                $"{faintedName} 전투 불능 · 다음 파트너를 선택하세요", swapHeaderCache);

            int columns = portrait ? 3 : BattleTeamManager.MaxSlots;
            const float gap = 12f;
            float cardW = (panel.width - 40f - (columns - 1) * gap) / columns;
            const float cardH = 240f;
            for (int i = 0; i < swapBtnRects.Length; i++)
            {
                swapBtnRects[i] = Rect.zero;
                swapBtnAvail[i] = false;
            }
            if (teamManager == null || collection == null) return;

            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                Rect card = new Rect(panel.x + 20f + (i % columns) * (cardW + gap),
                    panel.y + 66f + (i / columns) * (cardH + gap), cardW, cardH);
                string slotId = teamManager.GetSlot(i);
                bool isEmpty = string.IsNullOrEmpty(slotId);
                PlayerInsectData pid = isEmpty ? null : collection.GetByInstanceId(slotId);
                bool isFainted = !isEmpty && (faintedInsectIds.Contains(slotId) || (pid != null && pid.IsFainted));
                bool isCurrent = !isEmpty && slotId == currentInsectId;
                InsectData data = pid != null ? collection.GetInsectData(pid.insectId) : null;
                bool available = !isEmpty && !isFainted && !isCurrent && data != null;
                swapBtnRects[i] = card;
                swapBtnAvail[i] = available;
                bool hovered = available && card.Contains(UIScale.VirtualMousePosition);
                UISurface.Card(card, hovered ? theme.surfaceRaised : theme.surfaceCard,
                    available ? theme.accentMint : theme.surfaceBorder);
                UISurface.Chip(new Rect(card.x + 12f, card.y + 10f, 40f, 28f),
                    mobile ? (i + 1).ToString() : $"[{i + 1}]", theme.surfaceBase, theme.textSecondary);

                string status = available ? "교체 가능" : isFainted ? "쓰러짐"
                    : isCurrent ? "출전 중" : isEmpty ? "비어 있음" : "정보 없음";
                Color statusColor = available ? theme.accentMint : isFainted ? theme.accentCoral : theme.textSecondary;
                UISurface.Chip(new Rect(card.x + 12f, card.yMax - 36f, card.width - 24f, 28f),
                    status, theme.surfaceBase, statusColor);
                if (isEmpty || data == null)
                {
                    swapEmptyStyleCache.normal.textColor = theme.textSecondary;
                    UIHelper.LabelFit(new Rect(card.x + 12f, card.y + 88f, card.width - 24f, 40f),
                        isEmpty ? "빈 슬롯" : "곤충 정보 없음", swapEmptyStyleCache);
                    continue;
                }

                // Use the same model thumbnail / species silhouette as collection and team screens.
                InsectVisual.Draw(new Rect(card.center.x - 42f, card.y + 38f, 84f, 76f), data, pid.isShiny, 1f);
                GUI.color = Color.white;
                swapNameStyleCache.normal.textColor = theme.textPrimary;
                UIHelper.LabelFit(new Rect(card.x + 12f, card.y + 116f, card.width - 24f, 32f), data.displayName, swapNameStyleCache);
                swapInfoStyleCache.normal.textColor = theme.textSecondary;
                UIHelper.LabelFit(new Rect(card.x + 12f, card.y + 150f, card.width - 24f, 26f),
                    $"Lv. {pid.level} · CP {PlayerInsectCombatPower.Calculate(data, pid)}", swapInfoStyleCache);
                int maxHp = pid.GetTotalHp(data.baseHp);
                int hp = isFainted ? 0 : pid.GetEffectiveHp(maxHp);
                swapStatStyleCache.normal.textColor = theme.textSecondary;
                UIHelper.LabelFit(new Rect(card.x + 12f, card.y + 178f, card.width - 24f, 26f),
                    $"HP {hp} / {maxHp}", swapStatStyleCache);
            }
        }

        private void EndBattle()
        {
            // try-finally: arena/AudioManager 등에서 예외가 나도 카메라/이동 복구는 반드시 실행
            // (영구 동결 회귀 방지).
            try
            {
                if (arena != null)
                    arena.CleanupArena();

                comboCount = 0;
                comboDisplayTimer = 0f;

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.RestoreExploreBGM();   // 있던 리전의 곡으로 (범용 Explore 아님)
                    AudioManager.Instance.ClearBattleIntensity();
                }
                phase = Phase.None;
                hasArenaSnapshot = false;
                playerStats = null;
                enemyStats = null;
                skillBtnCount = 0;
                // 입력 표면도 함께 비운다. `skillBtnCount`만 0으로 두던 자리인데, 히트 테스트는
                // `basicAtkRect.width > 0` / `escapeRect.width > 0`로 하므로 이 둘이 남으면 다음 전투
                // 첫 프레임의 오탭을 지난 전투 좌표로 받는다(위 Update 말미 주석 참조).
                basicAtkRect = new Rect(0, 0, 0, 0);
                escapeRect = new Rect(0, 0, 0, 0);
                for (int i = 0; i < swapBtnRects.Length; i++)
                {
                    swapBtnRects[i] = new Rect(0, 0, 0, 0);
                    swapBtnAvail[i] = false;
                }
                wantMouseClick = false;
                faintedInsectIds.Clear();
                currentInsectId = null;
            }
            finally
            {
                if (cameraFollower != null) cameraFollower.ExitBattleMode();
                // **모달이 떠 있으면 풀지 않는다.** 프리즈는 bool 하나라 주인이 여럿이면
                // 마지막에 쓴 쪽이 이긴다. 전투 승리로 스토리 비트가 뜨면 대화 모달이 먼저
                // 프리즈를 걸어 두는데, 결과 화면은 그와 무관하게 4초 뒤 스스로 닫히며 여기서
                // 프리즈를 푼다 — **대사를 읽는 동안 캐릭터가 걸어다녔다**(BattleWin 비트 12개
                // 전부 해당). 모달 쪽이 닫힐 때 자기가 푼다(NpcDialogueUI.CloseModal).
                // 그쪽이 끝내 안 풀어도 PlayerMovement의 AutoUnfreezeTime(20s)이 받아낸다.
                if (playerMovement != null && !ModalUIRegistry.IsAnyOpen())
                    playerMovement.SetFrozen(false);

                // **맨 마지막이다.** 화면·카메라·프리즈를 다 걷은 뒤에 알려야, 이어서 열리는
                // 대화 모달이 방금 푼 프리즈를 다시 걸 수 있다(순서가 뒤집히면 대사를 읽는
                // 동안 캐릭터가 걸어다닌다 — 바로 위 주석의 그 결함이다).
                // finally에 두는 이유: try에서 예외가 나도 이야기는 이어져야 한다.
                if (storyDirector != null) storyDirector.NotifyBattlePresentationClosed();
            }
        }

        private void DrawScreenFlash()
        {
            if (BattlePresentation.ReducedFlashes || screenFlashTimer <= 0f || (arena != null && arena.IsActive)) return;
            float alpha = Mathf.Clamp01(screenFlashTimer / 0.3f) * 0.4f;
            GUI.color = new Color(screenFlashColor.r, screenFlashColor.g, screenFlashColor.b, alpha);
            GUI.DrawTexture(new Rect(0, 0, UIScale.VirtualScreenWidth, UIScale.VirtualScreenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawComboCounter()
        {
            if (comboCount < 2 || comboDisplayTimer <= 0f) return;

            float alpha = Mathf.Clamp01(comboDisplayTimer / 0.5f);
            float scale = 1f + Mathf.Max(0f, (2.5f - comboDisplayTimer) * 2f) * 0.0f;
            // 콤보 시작 직후 큰 펄스
            float justAppeared = Mathf.Clamp01(2.5f - comboDisplayTimer) / 0.2f;
            float pulse = justAppeared < 1f ? Mathf.Lerp(1.4f, 1f, justAppeared) : 1f;

            float sw = UIScale.VirtualScreenWidth;
            float boxW = 180f;
            float boxH = 70f;
            float bx = sw - boxW - 30f;
            float by = 100f;

            // 배경
            GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
            GUI.DrawTexture(new Rect(bx, by, boxW, boxH), Texture2D.whiteTexture);

            // 강조 라인 — 3개 정적 색 (combo 등급별). 매 프레임 new Color 회귀 차단을 위해 static.
            Color comboCol = comboCount >= 5 ? ComboColHot :
                             comboCount >= 3 ? ComboColWarm :
                                                ComboColCool;
            // Color는 struct(value)지만 alpha만 다르면 매번 new 대신 .a 갱신
            Color tinted = comboCol;
            tinted.a = alpha;
            GUI.color = tinted;
            GUI.DrawTexture(new Rect(bx, by, boxW, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bx, by + boxH - 3, boxW, 3), Texture2D.whiteTexture);

            // 숫자 (펄스) — GUIStyle 캐싱
            if (cachedComboNumStyle == null)
                cachedComboNumStyle = new GUIStyle(GUI.skin.label)
                { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            cachedComboNumStyle.fontSize = Mathf.RoundToInt(40f * pulse);
            cachedComboNumStyle.normal.textColor = tinted;
            GUI.color = Color.white;
            GUI.Label(new Rect(bx, by - 4, boxW, boxH * 0.7f), $"{comboCount}", cachedComboNumStyle);

            // "COMBO" 라벨 — GUIStyle 캐싱
            if (cachedComboLblStyle == null)
                cachedComboLblStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Color lblCol = ComboLblBase;
            lblCol.a = alpha * 0.9f;
            cachedComboLblStyle.normal.textColor = lblCol;
            UIHelper.LabelFit(new Rect(bx, by + boxH - 22, boxW, 18), "COMBO", cachedComboLblStyle);
        }

        // 콤보 표시 정적 색 — 매 OnGUI new Color 회귀 차단
        private static readonly Color ComboColHot = new Color(1f, 0.4f, 0.2f);
        private static readonly Color ComboColWarm = new Color(1f, 0.85f, 0.3f);
        private static readonly Color ComboColCool = new Color(0.9f, 0.95f, 1f);
        private static readonly Color ComboLblBase = new Color(1f, 1f, 1f);
        // DrawSwapSelect 헤더 펄스 색 — alpha만 동적, RGB는 static
        private static readonly Color SwapHeaderBase = new Color(1f, 0.4f, 0.3f);

        // 팔레트는 UITheme이 단일 출처다. 여기 사본이 있을 땐 d3d90cf가 추가한 4종(회복·기절·독·방어)이
        // 이 화면에서만 빠져 회색으로 떨어졌다 — 레이드·훈련소와 같은 표를 읽는다.
        private Color GetSkillColor(SkillEffectType type) => UITheme.Instance.GetSkillColor(type);

        public void AutoWire(InsectBattleController bc, CameraFollower cam, PlayerMovement pm = null)
        {
            if (battleController != null && battleController != bc)
            {
                battleController.DeferPresentation = false;
                battleController.BattleUpdated -= OnBattleUpdated;
                battleController.BattleEnded -= OnBattleEnded;
                battleController.PlayerFainted -= OnPlayerFainted;
            }

            if (battleController == null || battleController != bc)
            {
                battleController = bc;
                SubscribeBattleController();
            }

            if (cameraFollower == null) cameraFollower = cam;
            if (playerMovement == null) playerMovement = pm;
        }

        /// <summary>
        /// 배틀 컨트롤러 구독. <b>AutoWire와 OnEnable이 공유한다</b> — 해지 뒤 구독이라 중복되지 않는다.
        ///
        /// 옛날엔 AutoWire에서만 구독했다. 그런데 오프닝 다시보기(OpeningReplayCoordinator)가
        /// UI 루트를 통째로 SetActive(false)했다가 되돌리므로, 한 번 다시보면 OnDisable이 해지한
        /// 구독을 아무도 되살리지 않아 **배틀 화면이 영구히 열리지 않았다**(OnBattleUpdated가
        /// Phase.Intro를 세우는 유일한 지점이다).
        /// </summary>
        private void SubscribeBattleController()
        {
            if (battleController == null) return;
            battleController.BattleUpdated -= OnBattleUpdated;
            battleController.BattleEnded -= OnBattleEnded;
            battleController.PlayerFainted -= OnPlayerFainted;
            battleController.DeferPresentation = true;
            battleController.BattleUpdated += OnBattleUpdated;
            battleController.BattleEnded += OnBattleEnded;
            battleController.PlayerFainted += OnPlayerFainted;
        }

        public void AutoWire(BattleTeamManager team, PlayerInsectCollection col, TrainingManager training)
        {
            if (teamManager == null) teamManager = team;
            if (collection == null) collection = col;
            if (trainingManager == null) trainingManager = training;
        }

        // 전투가 끝났다고 알려 줄 곳. 없어도 전투는 그대로 돌아간다 — 스토리 쪽이 12초 뒤
        // 스스로 쏜다(StoryDirector.BattleWinGiveUpSeconds).
        private InsectGame.Story.StoryDirector storyDirector;

        public void AutoWire(InsectGame.Story.StoryDirector director)
        {
            if (storyDirector == null) storyDirector = director;
        }

        public void AutoWire(BattleArenaController a)
        {
            if (arena == null) arena = a;
        }
    }

    /// <summary>
    /// IMGUI 스킬 카드에서 공유하는 순수 레이아웃/대비 계산.
    /// GUI 상태를 읽지 않아 EditMode 회귀 테스트에서 직접 검증할 수 있다.
    /// </summary>
    internal readonly struct SkillCardDetailRows
    {
        internal readonly Rect Power;
        internal readonly Rect Effectiveness;
        internal readonly Rect Cooldown;

        internal SkillCardDetailRows(Rect power, Rect effectiveness, Rect cooldown)
        {
            Power = power;
            Effectiveness = effectiveness;
            Cooldown = cooldown;
        }
    }

    internal static class DuelHudLayout
    {
        internal const float HpCardHeight = 140f;
        internal static Rect HpCard(Rect safe, bool player)
        {
            float width = Mathf.Min(460f, (safe.width - 32f) * 0.5f);
            return new Rect(player ? safe.x : safe.xMax - width, safe.y + 68f, width, HpCardHeight);
        }
        internal static Rect SkillCard(Rect panel, bool portrait, int index, int count)
        {
            float contentW = panel.width - 40f;
            float width = portrait ? (contentW - 12f) * 0.5f
                : (contentW - 200f - 16f - 12f * Mathf.Max(0, count - 1)) / Mathf.Max(1, count);
            return new Rect(panel.x + 20f + (portrait ? index % 2 : index) * (width + 12f),
                panel.y + 62f + (portrait ? index / 2 : 0) * 228f, width, 224f);
        }
        internal static Rect UtilityCard(Rect panel, bool portrait, bool escape)
        {
            float width = portrait ? (panel.width - 52f) * 0.5f : 200f;
            return new Rect(portrait ? panel.x + 20f + (escape ? width + 12f : 0f) : panel.xMax - width - 20f,
                portrait ? panel.yMax - 94f : panel.y + 62f + (escape ? 116f : 0f), width, portrait ? 80f : 108f);
        }
        internal static bool TargetsSelf(SkillEffectType type) => type == SkillEffectType.Heal
            || type == SkillEffectType.BuffAttack || type == SkillEffectType.DefenseBuff;
    }

    internal static class SkillUILayout
    {
        internal const float MinimumSkillNameHeight = 44f;

        /// <summary>
        /// 본문 글자가 위아래로 잘리지 않는 최소 행 높이.
        ///
        /// 한글 글리프는 폰트 크기의 <b>1.35배</b>쯤을 세로로 쓴다(라틴 문자보다 어센더·디센더가 크다).
        /// 카드의 정보 행은 18~20px 글자를 담으므로 20 × 1.35 = 27이 하한이고, 위아래 여백까지
        /// 생각해 28을 쓴다. 이 상수보다 낮은 행을 요청하면 <see cref="GetDetailRows"/>가 끌어올린다.
        /// </summary>
        internal const float MinimumDetailRowHeight = 28f;

        internal static readonly Color DisabledTextColor = new Color(0.68f, 0.68f, 0.72f);
        internal static readonly Color DisabledSecondaryTextColor = new Color(0.56f, 0.56f, 0.62f);

        internal static Rect GetNameRect(Rect cardRect, float top, float horizontalPadding, float requestedHeight)
        {
            float safePadding = Mathf.Max(0f, horizontalPadding);
            float safeTop = Mathf.Clamp(top, 0f, cardRect.height);
            float availableHeight = Mathf.Max(0f, cardRect.height - safeTop);
            float height = Mathf.Min(
                Mathf.Max(MinimumSkillNameHeight, requestedHeight),
                availableHeight);

            return new Rect(
                cardRect.x + safePadding,
                cardRect.y + safeTop,
                Mathf.Max(1f, cardRect.width - safePadding * 2f),
                height);
        }

        internal static float GetTouchHeight(bool mobileLayout, float desktopHeight)
        {
            return GetTouchHeight(mobileLayout, desktopHeight, UIScale.MinTouchHeight);
        }

        internal static float GetTouchHeight(bool mobileLayout, float desktopHeight, float preferredMobileHeight)
        {
            return mobileLayout
                ? Mathf.Max(UIScale.MinTouchHeight, preferredMobileHeight)
                : desktopHeight;
        }

        /// <summary>
        /// 스킬 카드 아래쪽 정보 영역. <b>2행</b>이다 — 첫 줄에 위력(좌)과 상성 배지(우)를 나란히,
        /// 둘째 줄에 쿨다운.
        ///
        /// 예전엔 3행이었는데 216px 카드 안에서 행 하나가 <b>22px</b>밖에 안 됐다. 20px 글자에
        /// 필요한 27px(<see cref="MinimumDetailRowHeight"/> 참고)에 못 미쳐 한글 위아래가 그대로
        /// 잘렸고, 마지막 행(y 192~214)은 카드 아래 테두리(213)를 파고들어 여백이 아예 없었다.
        /// 카드를 키우는 해법은 못 쓴다 — 스킬 패널이 커지면 전투 장면의 곤충을 더 가리고,
        /// 그건 이미 한 번 줄여 놓은 자리다. 그래서 <b>행 수를 줄여</b> 남는 세로를 여백으로 돌렸다.
        /// 위력과 상성은 애초에 짧아(각각 "위력: 45" / "효과적 ▲") 한 줄에 좌우로 들어간다.
        ///
        /// <paramref name="bottomPadding"/>은 선택 인자가 아니다 — 마지막 행이 카드 테두리에 닿지
        /// 않도록 호출부가 반드시 남겨야 하는 값이라 기본값을 주지 않는다.
        /// </summary>
        internal static SkillCardDetailRows GetDetailRows(
            Rect cardRect,
            float top,
            float horizontalPadding,
            float requestedRowHeight,
            float requestedGap,
            float bottomPadding)
        {
            float safeTop = Mathf.Clamp(top, 0f, cardRect.height);
            float safePadding = Mathf.Max(0f, horizontalPadding);
            float safeBottom = Mathf.Clamp(bottomPadding, 0f, cardRect.height - safeTop);
            float gap = Mathf.Max(0f, requestedGap);
            float availableHeight = Mathf.Max(0f, cardRect.height - safeTop - safeBottom);
            float maximumRowHeight = Mathf.Max(0f, (availableHeight - gap) * 0.5f);
            float rowHeight = Mathf.Min(
                Mathf.Max(MinimumDetailRowHeight, requestedRowHeight),
                maximumRowHeight);
            float x = cardRect.x + safePadding;
            float width = Mathf.Max(1f, cardRect.width - safePadding * 2f);
            float firstY = cardRect.y + safeTop;
            float secondY = firstY + rowHeight + gap;

            // 같은 줄이라 폭부터 갈라 둔다 — 위력이 길어도 상성 배지를 밟지 않는다.
            float powerWidth = Mathf.Max(1f, width * 0.56f);
            float effectivenessWidth = Mathf.Max(1f, width - powerWidth - gap);

            return new SkillCardDetailRows(
                new Rect(x, firstY, powerWidth, rowHeight),
                new Rect(x + width - effectivenessWidth, firstY, effectivenessWidth, rowHeight),
                new Rect(x, secondY, width, rowHeight));
        }

        /// <summary>
        /// 속성색을 어두운 버튼 배경 위에서 읽히게 만든다.
        ///
        /// 예전엔 흰색을 <b>일률적으로 28%</b> 섞었다. 밝은 속성(Electric·Light)은 그걸로 충분했지만
        /// 어두운 속성은 그대로 어두워서, <c>Dark</c>(0.4/0.15/0.5) 스킬의 "타입 · 동작" 줄이
        /// 버튼 배경(0.08/0.10/0.20)과 명암비 <b>3.69</b>, 호버 시엔 <b>2.43</b>까지 떨어졌다
        /// — 기기에서 "글씨가 배경색과 비슷해 안 보인다"고 보고된 자리다(WCAG AA 본문 기준 4.5).
        ///
        /// 이제 원래 밝기에 따라 섞는 양을 달리한다: 어두울수록 많이 섞고, 이미 밝으면 조금만 섞어
        /// 속성색의 정체성을 지킨다.
        ///
        /// <b>기준 배경은 평시가 아니라 호버다.</b> 첫 수정은 평시 배경(0.08/0.10/0.20)에만 맞춰
        /// 62%/28% 곡선을 잡았고 그래서 평시는 5.5+였지만 <b>호버 배경(0.18/0.22/0.38)에서 Dark 3.65 ·
        /// Poison 3.82</b>로 도로 떨어졌다 — 마우스를 올린 카드, 즉 <b>지금 고르려는 그 카드</b>의
        /// 글씨가 가장 안 보였다. 호버는 배경을 밝히는 연출이라 어두운 글씨와 정면으로 부딪힌다.
        /// 82%/36%로 올려 호버에서도 전 속성 4.6+(최저 Poison 4.67), 평시는 6.9+가 된다.
        /// </summary>
        internal static Color GetReadableAccent(Color accent)
        {
            // 지각 밝기(ITU-R BT.601) — 사람 눈이 초록에 민감한 것을 반영한다.
            float luminance = accent.r * 0.299f + accent.g * 0.587f + accent.b * 0.114f;
            float mix = Mathf.Lerp(0.82f, 0.36f, Mathf.Clamp01(luminance / 0.5f));

            Color readable = Color.Lerp(accent, Color.white, mix);
            readable.a = accent.a;
            return readable;
        }
    }
}
