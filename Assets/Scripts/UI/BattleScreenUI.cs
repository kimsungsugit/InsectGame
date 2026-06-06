using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;
using System.Collections.Generic;

namespace InsectGame.UI
{
    public class BattleScreenUI : MonoBehaviour
    {
        [SerializeField] private InsectBattleController battleController;
        [SerializeField] private CameraFollower cameraFollower;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private BattleTeamManager teamManager;
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private TrainingManager trainingManager;
        [SerializeField] private BattleArenaController arena;

        private enum Phase { None, Intro, PlayerTurn, PlayerAttack, EnemyAttack, SwapSelect, Result }

        private Phase phase = Phase.None;
        public bool IsBattleActive => phase != Phase.None;

        private InsectBattleStats playerStats;
        private InsectBattleStats enemyStats;
        private InsectBattleStats prevPlayerStats;
        private InsectBattleStats prevEnemyStats;

        private float phaseTimer;
        private float introTimer;
        private string actionText;
        private float actionTimer;
        private int lastDamageToEnemy;
        private bool lastWasCritical;
        private int comboCount;
        private float comboDisplayTimer;
        private float slowMoTimer;
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
            // 구독은 AutoWire에서만 수행 (중복 구독 방지).
        }

        private void OnDisable()
        {
            if (battleController != null)
            {
                battleController.BattleUpdated -= OnBattleUpdated;
                battleController.BattleEnded -= OnBattleEnded;
                battleController.PlayerFainted -= OnPlayerFainted;
            }
            // 슬로우모션 안전 복구 (예외/씬 전환 중 timeScale 잔존 방지)
            if (Time.timeScale < 0.99f) Time.timeScale = 1f;
            slowMoTimer = 0f;
        }

        private void OnPlayerFainted()
        {
            if (playerStats != null && playerStats.PlayerData != null)
                faintedInsectIds.Add(playerStats.PlayerData.instanceId);

            if (HasAvailableTeamMember())
            {
                phase = Phase.SwapSelect;
                phaseTimer = 0f;
                swapMessageTimer = 0f;
            }
            else
            {
                lastWon = false;
                resultShown = true;
                resultTimer = 0f;
                phase = Phase.Result;
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
                turnNumber = 0;
                phase = Phase.Intro;
                introTimer = 0f;
                resultShown = false;
                faintedInsectIds.Clear();
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
            int oldPlayerHp = hpSnapshotTaken ? savedPlayerHp : (playerStats != null ? playerStats.CurrentHp : 0);
            hpSnapshotTaken = false;

            playerStats = player;
            enemyStats = enemy;

            lastDamageToEnemy = Mathf.Max(0, oldEnemyHp - enemy.CurrentHp);
            lastDamageToPlayer = Mathf.Max(0, oldPlayerHp - player.CurrentHp);

            // 크리티컬 판정: 적 MaxHp의 25% 이상 데미지
            lastWasCritical = lastDamageToEnemy > 0 && enemy.MaxHp > 0 && lastDamageToEnemy >= enemy.MaxHp * 0.25f;

            if (lastDamageToEnemy > 0)
            {
                enemyShake = lastWasCritical ? 0.7f : 0.4f;
                comboCount++;
                comboDisplayTimer = 2.5f;

                // 슬로우모션 + 화면 플래시 (크리티컬만)
                if (lastWasCritical)
                {
                    slowMoTimer = 0.25f;
                    Time.timeScale = 0.4f;
                    screenFlashTimer = 0.3f;
                    screenFlashColor = new Color(1f, 0.95f, 0.3f);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.CriticalHit);
                }
            }
            if (lastDamageToPlayer > 0)
            {
                comboCount = 0; // 피격 시 콤보 리셋
                comboDisplayTimer = 0f;
            }

            phase = Phase.PlayerAttack;
            phaseTimer = 0f;
        }

        private void OnBattleEnded(bool playerWon)
        {
            lastWon = playerWon;
            resultShown = true;
            resultTimer = 0f;
            phase = Phase.Result;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(playerWon ? SfxType.Victory : SfxType.Defeat);
                AudioManager.Instance.PlayBGM(playerWon ? BgmType.Victory : BgmType.Defeat);
            }
            if (playerWon && TutorialQuestManager.Instance != null)
                TutorialQuestManager.Instance.NotifyBattleWon();

            // 수문장 격파 체크
            if (playerWon)
                CheckGuardianDefeat();
        }

        private void CheckGuardianDefeat()
        {
            if (battleController == null) return;
            InsectEntity enemy = battleController.GetEnemyEntity();
            if (enemy == null || enemy.Data == null) return;

            if (cachedRegionMgr == null) cachedRegionMgr = FindFirstObjectByType<RegionManager>();
            RegionManager regionMgr = cachedRegionMgr;
            if (regionMgr == null || regionMgr.Regions == null) return;

            string enemyId = enemy.Data.insectId;
            foreach (var region in regionMgr.Regions)
            {
                if (string.IsNullOrEmpty(region.guardianInsectId)) continue;
                if (region.guardianInsectId != enemyId) continue;
                if (regionMgr.IsGuardianDefeated(region.regionId)) continue;

                // 수문장과 레벨도 확인 (수문장 레벨 이상의 적이어야)
                if (enemy.Level >= region.guardianLevel - 2)
                {
                    regionMgr.DefeatGuardian(region.regionId);
                    Debug.Log($"[Guardian] {region.displayName} 수문장 격파! 다음 지역 해금됨");

                    if (TutorialQuestManager.Instance != null)
                        TutorialQuestManager.Instance.NotifyGuardianDefeated();
                }
            }
        }

        private void Update()
        {
            // 슬로우모션 처리 (unscaled로 타이머 감소)
            if (slowMoTimer > 0f)
            {
                slowMoTimer -= Time.unscaledDeltaTime;
                if (slowMoTimer <= 0f) Time.timeScale = 1f;
            }
            if (screenFlashTimer > 0f) screenFlashTimer -= Time.unscaledDeltaTime;
            if (comboDisplayTimer > 0f) comboDisplayTimer -= Time.unscaledDeltaTime;

            if (phase == Phase.None) return;

            phaseTimer += Time.deltaTime;
            introTimer += Time.deltaTime;

            if (actionTimer > 0) actionTimer -= Time.deltaTime;
            if (playerShake > 0) playerShake -= Time.deltaTime;
            if (enemyShake > 0) enemyShake -= Time.deltaTime;
            if (resultShown) resultTimer += Time.deltaTime;

            float hpSpeed = 60f * Time.deltaTime;
            if (playerStats != null)
                displayPlayerHp = Mathf.MoveTowards(displayPlayerHp, playerStats.CurrentHp, hpSpeed);
            if (enemyStats != null)
                displayEnemyHp = Mathf.MoveTowards(displayEnemyHp, enemyStats.CurrentHp, hpSpeed);

            // BGM 인텐시티: HP 30% 이하부터 가파르게 상승
            if (AudioManager.Instance != null && playerStats != null && playerStats.MaxHp > 0)
            {
                float hpRatio = (float)playerStats.CurrentHp / playerStats.MaxHp;
                float intensity = Mathf.Clamp01((0.5f - hpRatio) * 2f);
                AudioManager.Instance.SetBattleIntensity(intensity);
            }

            if (phase == Phase.Intro && introTimer > 2.0f)
            {
                phase = Phase.PlayerTurn;
                phaseTimer = 0f;
            }

            if (phase == Phase.SwapSelect)
            {
                swapMessageTimer += Time.deltaTime;
                int swapIndex = -1;
                if (wantSwap0 || Input.GetKeyDown(KeyCode.Alpha1)) swapIndex = 0;
                else if (wantSwap1 || Input.GetKeyDown(KeyCode.Alpha2)) swapIndex = 1;
                else if (wantSwap2 || Input.GetKeyDown(KeyCode.Alpha3)) swapIndex = 2;
                else if (wantSwap3 || Input.GetKeyDown(KeyCode.Alpha4)) swapIndex = 3;
                else if (wantSwap4 || Input.GetKeyDown(KeyCode.Alpha5)) swapIndex = 4;
                wantSwap0 = wantSwap1 = wantSwap2 = wantSwap3 = wantSwap4 = false;
                if (swapIndex >= 0) TrySwapToSlot(swapIndex);

                if (wantMouseClick || Input.GetMouseButtonDown(0))
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

                if (wantMouseClick || Input.GetMouseButtonDown(0))
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

            if (phase == Phase.PlayerAttack && phaseTimer > 0.8f)
            {
                if (lastDamageToPlayer > 0)
                {
                    playerShake = 0.4f;
                    phase = Phase.EnemyAttack;
                    phaseTimer = 0f;
                }
                else if (!resultShown)
                {
                    phase = Phase.PlayerTurn;
                    phaseTimer = 0f;
                }
            }

            if (phase == Phase.EnemyAttack && phaseTimer > 0.8f)
            {
                if (!resultShown)
                {
                    phase = Phase.PlayerTurn;
                    phaseTimer = 0f;
                }
            }

            if (phase == Phase.Result && resultTimer > 4f)
            {
                EndBattle();
            }
        }

        private void SnapshotHp()
        {
            savedEnemyHp = enemyStats != null ? enemyStats.CurrentHp : 0;
            savedPlayerHp = playerStats != null ? playerStats.CurrentHp : 0;
            hpSnapshotTaken = true;
        }

        private void TryUseSkill(int index)
        {
            if (battleController == null || !battleController.CanUseSkill(index)) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.SkillUse);

            turnNumber++;
            InsectSkill[] curSkills = battleController.GetPlayerSkills();
            InsectSkill skill = curSkills != null && index < curSkills.Length ? curSkills[index] : null;
            lastSkillName = skill != null ? skill.displayName : "공격";
            actionText = $"{playerStats.Data.displayName}의 {lastSkillName}!";
            actionTimer = 1.5f;
            SnapshotHp();

            if (arena != null && arena.IsActive)
            {
                InsectElement elem = (playerStats != null && playerStats.Data != null) ? playerStats.Data.primaryType : InsectElement.Bug;
                SkillEffectType effectType = (skill != null) ? skill.effectType : SkillEffectType.Damage;
                arena.PlaySkillEffect(true, elem, effectType);
            }

            battleController.UseSkill(index);
        }

        private void TryBasicAttack()
        {
            if (battleController == null) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.Attack);
            turnNumber++;
            lastSkillName = "기본 공격";
            actionText = $"{playerStats.Data.displayName}의 기본 공격!";
            actionTimer = 1.5f;
            SnapshotHp();

            if (arena != null && arena.IsActive)
            {
                InsectElement elem = (playerStats != null && playerStats.Data != null) ? playerStats.Data.primaryType : InsectElement.Bug;
                arena.PlaySkillEffect(true, elem, SkillEffectType.Damage);
            }

            battleController.UseBasicAttack();
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
            if (battleController == null) return;
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
            skillHeaderCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold };
            skillHeaderCache.normal.textColor = new Color(0.9f, 0.85f, 0.5f);
            skillKeyNumCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            skillNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold };
            skillTypeLabelCache = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            skillInfoStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            skillCdStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            skillCdStyleCache.normal.textColor = new Color(1f, 0.4f, 0.3f);
            skillCdInfoCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleRight };
            skillCdInfoCache.normal.textColor = new Color(0.45f, 0.45f, 0.5f);
            skillFKeyCache = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            skillFKeyCache.normal.textColor = new Color(1f, 0.85f, 0.3f);
            skillFInfoCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            skillFInfoCache.normal.textColor = new Color(0.6f, 0.55f, 0.4f);
            skillEscStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            skillEscStyleCache.normal.textColor = new Color(0.9f, 0.5f, 0.4f);
            skillEscInfoCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            skillEscInfoCache.normal.textColor = new Color(0.55f, 0.4f, 0.4f);

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

        private void OnGUI()
        {
            if (phase == Phase.None) return;

            InitStyles();
            UIScale.Begin();
            DrawScreenFlash();
            DrawComboCounter();

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

            if (evt != null && evt.type == EventType.MouseDown && evt.button == 0)
            {
                wantMouseClick = true;
                guiMousePos = new Vector2(evt.mousePosition.x, evt.mousePosition.y);
            }

            DrawBattleOverlay();
            DrawBattleField();
            DrawHpBars();

            if (phase == Phase.Intro)
                DrawIntro();
            else if (phase == Phase.PlayerTurn)
                DrawSkillPanel();
            else if (phase == Phase.SwapSelect)
                DrawSwapSelect();
            else if (phase == Phase.PlayerAttack)
            {
                DrawAttackAnimation(true);
                DrawPhaseIndicator("내 곤충의 공격!");
            }
            else if (phase == Phase.EnemyAttack)
            {
                DrawAttackAnimation(false);
                DrawPhaseIndicator("적 곤충의 반격!");
            }

            if (actionTimer > 0)
                DrawActionText();

            if (resultShown)
                DrawResult();

            UIScale.End();
        }

        private void DrawBattleOverlay()
        {
            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;

            // 3D 아레나 활성 시: 상단 턴 표시 바만 그리고 2D 배경 스킵
            if (arena != null && arena.IsActive)
            {
                GUI.color = new Color(0.02f, 0.03f, 0.06f, 0.7f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh * 0.08f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, 4, sw, 34), $"BATTLE  -  Turn {turnNumber + 1}", turnStyle3dCache);
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
            GUI.Label(new Rect(playerX - 80, playerY + 42 * playerScale / 3f, 160, 26), playerStats.Data.displayName, playerLabelCache);
            GUI.Label(new Rect(playerX - 60, playerY + 42 * playerScale / 3f + 24, 120, 18), "내 곤충", playerTagCache);
            GUI.Label(new Rect(enemyX - 80, enemyY + 38 * enemyScale / 3f, 160, 26), enemyStats.Data.displayName, enemyLabelCache);
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
            else if (id.Contains("bee") || id.Contains("wasp") || id.Contains("hornet"))
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

            float arenaBot = UIScale.VirtualScreenHeight * 0.60f;
            DrawHpBox(20, arenaBot + 8, 420, playerStats, displayPlayerHp, true);
            DrawHpBox(UIScale.VirtualScreenWidth - 440, arenaBot + 8, 420, enemyStats, displayEnemyHp, false);
        }

        private void DrawHpBox(float x, float y, float w, InsectBattleStats stats, float dispHp, bool isPlayer)
        {
            float h = 130f;
            Color rarityCol = UITheme.Instance.GetInsectRarityColor(stats.Data.rarity);

            GUI.color = new Color(0.05f, 0.06f, 0.12f, 0.94f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = rarityCol;
            GUI.DrawTexture(new Rect(x, y, w, 4), Texture2D.whiteTexture);
            GUI.color = new Color(rarityCol.r, rarityCol.g, rarityCol.b, 0.3f);
            GUI.DrawTexture(new Rect(x, y + h - 2, w, 2), Texture2D.whiteTexture);

            hpNameStyleCache.normal.textColor = rarityCol;
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 14, y + 10, w - 110, 32), stats.Data.displayName, hpNameStyleCache);

            GUI.Label(new Rect(x + w - 100, y + 10, 86, 28), $"Lv.{stats.Level}", hpLvStyleCache);

            GUI.Label(new Rect(x + 14, y + 42, w - 28, 22),
                $"ATK {stats.Attack}  DEF {stats.Defense}", hpMiniStatCache);

            float barX = x + 14;
            float barY = y + 70;
            float barW = w - 28;
            float barH = 26f;

            GUI.color = new Color(0.12f, 0.12f, 0.18f);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);

            float hpRatio = stats.MaxHp > 0 ? dispHp / stats.MaxHp : 0;
            Color hpColor = hpRatio > 0.5f ? new Color(0.3f, 0.85f, 0.35f) :
                           hpRatio > 0.2f ? new Color(0.95f, 0.8f, 0.2f) :
                           new Color(0.95f, 0.25f, 0.2f);
            GUI.color = hpColor;
            GUI.DrawTexture(new Rect(barX, barY, barW * hpRatio, barH), Texture2D.whiteTexture);

            GUI.color = new Color(hpColor.r + 0.15f, hpColor.g + 0.15f, hpColor.b + 0.15f, 0.4f);
            GUI.DrawTexture(new Rect(barX, barY, barW * hpRatio, barH / 3f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(barX, barY, barW, barH), $"{Mathf.CeilToInt(dispHp)} / {stats.MaxHp}", hpTextCache);

            InsectBattleController.EffectSnapshot[] effects = battleController != null ? battleController.GetActiveEffects() : null;
            if (effects != null)
            {
                string effectStr = "";
                foreach (var eff in effects)
                {
                    if (eff.targetIsPlayer == isPlayer)
                    {
                        string tag = eff.value >= 0 ? $"ATK+({eff.remainingTurns})" : $"ATK-({eff.remainingTurns})";
                        effectStr += (effectStr.Length > 0 ? " " : "") + tag;
                    }
                }
                if (effectStr.Length > 0)
                {
                    GUI.Label(new Rect(barX, barY + barH + 4, barW, 22), effectStr, hpEffStyleCache);
                }
            }
        }

        private void DrawIntro()
        {
            if (playerStats == null || enemyStats == null) return;

            float cx = UIScale.VirtualScreenWidth / 2f;
            float cy = UIScale.VirtualScreenHeight * 0.32f;
            float sw = UIScale.VirtualScreenWidth;

            Color pc = UITheme.Instance.GetInsectRarityColor(playerStats.Data.rarity);
            Color ec = UITheme.Instance.GetInsectRarityColor(enemyStats.Data.rarity);
            bool isEpicOrHigher = (int)enemyStats.Data.rarity >= 3 || (int)playerStats.Data.rarity >= 3;

            // Epic/Legendary background effect
            if (isEpicOrHigher)
            {
                float bgPulse = 0.03f + Mathf.Sin(introTimer * 4f) * 0.02f;
                Color bgCol = (int)enemyStats.Data.rarity >= 4
                    ? new Color(1f, 0.85f, 0.2f, bgPulse)
                    : new Color(0.6f, 0.3f, 0.9f, bgPulse);
                GUI.color = bgCol;
                GUI.DrawTexture(new Rect(0, 0, sw, UIScale.VirtualScreenHeight), Texture2D.whiteTexture);

                // Radial rays for legendary
                if ((int)enemyStats.Data.rarity >= 4)
                {
                    float rayAlpha = 0.04f + Mathf.Sin(introTimer * 3f) * 0.02f;
                    GUI.color = new Color(1f, 0.9f, 0.4f, rayAlpha);
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * 45f * Mathf.Deg2Rad + introTimer * 0.5f;
                        float rx = cx + Mathf.Cos(angle) * 300f;
                        float ry = cy + Mathf.Sin(angle) * 200f;
                        GUI.DrawTexture(new Rect(Mathf.Min(cx, rx), Mathf.Min(cy, ry),
                            Mathf.Abs(rx - cx) + 4, Mathf.Abs(ry - cy) + 4), Texture2D.whiteTexture);
                    }
                }
            }

            // Phase 1 (0 ~ 0.6s): "VS" text grows from center
            if (introTimer < 0.6f)
            {
                float vsT = Mathf.Clamp01(introTimer / 0.5f);
                float vsScale = 0.3f + vsT * 0.7f;
                float vsAlpha = vsT;
                int vsFontSize = (int)(72 * vsScale);

                // Dark backdrop
                GUI.color = new Color(0, 0, 0, 0.6f * vsAlpha);
                GUI.DrawTexture(new Rect(cx - 200, cy - 30, 400, 100), Texture2D.whiteTexture);

                introVsStyleCache.fontSize = vsFontSize;
                introVsStyleCache.normal.textColor = new Color(1f, 0.9f, 0.3f, vsAlpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 200, cy - 10, 400, 80), "VS", introVsStyleCache);

                // Player name slides in from left
                float slideP = Mathf.Clamp01((introTimer - 0.1f) / 0.4f);
                float pNameX = Mathf.Lerp(-400, cx - 350, slideP * slideP * (3f - 2f * slideP));
                introPNameStyleCache.normal.textColor = new Color(pc.r, pc.g, pc.b, slideP);
                GUI.Label(new Rect(pNameX, cy - 50, 300, 36),
                    $"{playerStats.Data.displayName} Lv.{playerStats.Level}", introPNameStyleCache);

                // Enemy name slides in from right
                float slideE = Mathf.Clamp01((introTimer - 0.15f) / 0.4f);
                float eNameX = Mathf.Lerp(sw + 100, cx + 50, slideE * slideE * (3f - 2f * slideE));
                introENameStyleCache.normal.textColor = new Color(ec.r, ec.g, ec.b, slideE);
                GUI.Label(new Rect(eNameX, cy + 56, 300, 36),
                    $"{enemyStats.Data.displayName} Lv.{enemyStats.Level}", introENameStyleCache);

                // Rarity color bars under names
                if (slideP > 0.5f)
                {
                    float barAlpha = (slideP - 0.5f) * 2f;
                    GUI.color = new Color(pc.r, pc.g, pc.b, 0.5f * barAlpha);
                    GUI.DrawTexture(new Rect(pNameX + 50, cy - 14, 250 * barAlpha, 3), Texture2D.whiteTexture);
                }
                if (slideE > 0.5f)
                {
                    float barAlpha = (slideE - 0.5f) * 2f;
                    GUI.color = new Color(ec.r, ec.g, ec.b, 0.5f * barAlpha);
                    GUI.DrawTexture(new Rect(eNameX, cy + 92, 250 * barAlpha, 3), Texture2D.whiteTexture);
                }
            }
            // Phase 2 (0.6 ~ 1.1s): "FIGHT!" flashes briefly
            else if (introTimer < 1.1f)
            {
                float fightT = (introTimer - 0.6f) / 0.5f;
                float fightScale = 0.6f + Mathf.Sin(fightT * Mathf.PI * 0.5f) * 0.4f;
                float fightAlpha = fightT < 0.3f ? fightT / 0.3f : Mathf.Clamp01(1f - (fightT - 0.6f) / 0.4f);
                int fightFontSize = (int)(64 * fightScale);

                // Flash backdrop
                GUI.color = new Color(1f, 0.9f, 0.3f, 0.08f * fightAlpha);
                GUI.DrawTexture(new Rect(0, cy - 40, sw, 120), Texture2D.whiteTexture);

                GUI.color = new Color(0, 0, 0, 0.7f * fightAlpha);
                GUI.DrawTexture(new Rect(cx - 220, cy - 10, 440, 80), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.3f, 0.2f, 0.6f * fightAlpha);
                GUI.DrawTexture(new Rect(cx - 220, cy - 10, 440, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 220, cy + 66, 440, 4), Texture2D.whiteTexture);

                introFightStyleCache.fontSize = fightFontSize;
                introFightStyleCache.normal.textColor = new Color(1f, 0.3f, 0.15f, fightAlpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 200, cy, 400, 60), "FIGHT!", introFightStyleCache);
            }
            // Phase 3 (1.1s+): Enemy encounter text
            else
            {
                float showAlpha = Mathf.Clamp01((introTimer - 1.1f) / 0.3f);

                GUI.color = new Color(0, 0, 0, 0.6f * showAlpha);
                GUI.DrawTexture(new Rect(cx - 320, cy - 10, 640, 60), Texture2D.whiteTexture);
                GUI.color = new Color(ec.r, ec.g, ec.b, 0.5f * showAlpha);
                GUI.DrawTexture(new Rect(cx - 320, cy - 10, 640, 3), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 320, cy + 47, 640, 3), Texture2D.whiteTexture);
                GUI.color = Color.white;

                introEncounterStyleCache.normal.textColor = new Color(ec.r, ec.g, ec.b, showAlpha);
                GUI.Label(new Rect(cx - 320, cy, 640, 44),
                    $"야생 {enemyStats.Data.displayName} Lv.{enemyStats.Level} 등장!", introEncounterStyleCache);
            }

            GUI.color = Color.white;
        }

        private void DrawSkillPanel()
        {
            if (playerStats == null || playerStats.Data == null) return;

            float sw = UIScale.VirtualScreenWidth;
            float panelH = 320f;
            float panelY = UIScale.VirtualScreenHeight - panelH;

            GUI.color = new Color(0.03f, 0.04f, 0.09f, 0.97f);
            GUI.DrawTexture(new Rect(0, panelY, sw, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(0.3f, 0.5f, 0.9f);
            GUI.DrawTexture(new Rect(0, panelY, sw, 4), Texture2D.whiteTexture);
            GUI.color = new Color(0.15f, 0.25f, 0.45f, 0.3f);
            GUI.DrawTexture(new Rect(0, panelY + 4, sw, 2), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(30, panelY + 12, 600, 36),
                "스킬을 선택하세요  (숫자키 1~4 또는 클릭)", skillHeaderCache);

            InsectSkill[] skills = battleController != null ? battleController.GetPlayerSkills() : playerStats.Data.skills;
            int[] cooldowns = battleController != null ? battleController.GetPlayerCooldowns() : new int[0];
            int count = skills != null ? Mathf.Min(skills.Length, 4) : 0;
            skillBtnCount = count;

            float extraW = 200f;
            float gap = 16f;
            float availW = sw - 40f - extraW - 30f;
            float btnW = Mathf.Max(220, Mathf.Min(320, (availW - gap * Mathf.Max(count - 1, 0)) / Mathf.Max(count, 1)));
            float btnH = 200f;
            float btnY = panelY + 58;
            float startX = 30;

            float pulse = 0.5f + Mathf.Sin(Time.time * 3f) * 0.15f;

            for (int i = 0; i < count; i++)
            {
                InsectSkill skill = skills[i];
                if (skill == null) continue;

                float bx = startX + i * (btnW + gap);
                int cd = i < cooldowns.Length ? cooldowns[i] : 0;
                bool canUse = cd <= 0;

                skillBtnRects[i] = new Rect(bx, btnY, btnW, btnH);
                skillBtnUsable[i] = canUse;

                bool isHovered = false;
                if (canUse)
                {
                    Vector2 mouseGui = UIScale.VirtualMousePosition;
                    isHovered = skillBtnRects[i].Contains(mouseGui);
                }

                Color bgCol;
                if (isHovered)
                    bgCol = new Color(0.18f, 0.22f, 0.38f);
                else if (canUse)
                    bgCol = new Color(0.08f, 0.10f, 0.20f);
                else
                    bgCol = new Color(0.05f, 0.05f, 0.07f);

                GUI.color = bgCol;
                GUI.DrawTexture(new Rect(bx, btnY, btnW, btnH), Texture2D.whiteTexture);

                Color borderCol = GetSkillColor(skill.effectType);
                if (canUse)
                {
                    GUI.color = isHovered ? Color.white : borderCol;
                    GUI.DrawTexture(new Rect(bx, btnY, btnW, 5), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(bx, btnY + btnH - 3, btnW, 3), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(bx, btnY, 2, btnH), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(bx + btnW - 2, btnY, 2, btnH), Texture2D.whiteTexture);
                }
                else
                {
                    GUI.color = new Color(0.25f, 0.25f, 0.25f);
                    GUI.DrawTexture(new Rect(bx, btnY, btnW, 2), Texture2D.whiteTexture);
                }

                Color skillIconCol = GetSkillColor(skill.effectType);
                float iconSize = 36f;
                float iconX = bx + 14;
                float iconY = btnY + 14;
                GUI.color = new Color(skillIconCol.r, skillIconCol.g, skillIconCol.b, 0.15f);
                GUI.DrawTexture(new Rect(iconX - 4, iconY - 4, iconSize + 8, iconSize + 8), Texture2D.whiteTexture);
                GUI.color = new Color(skillIconCol.r, skillIconCol.g, skillIconCol.b, canUse ? 0.9f : 0.3f);
                GUI.DrawTexture(new Rect(iconX, iconY, iconSize, iconSize), Texture2D.whiteTexture);

                if (skill.effectType == SkillEffectType.Damage)
                {
                    GUI.color = new Color(0, 0, 0, 0.6f);
                    GUI.DrawTexture(new Rect(iconX + 8, iconY + 4, 4, iconSize - 8), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(iconX + 16, iconY + 8, 4, iconSize - 16), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(iconX + 24, iconY + 2, 4, iconSize - 4), Texture2D.whiteTexture);
                }
                else
                {
                    GUI.color = new Color(1, 1, 1, 0.3f);
                    GUI.DrawTexture(new Rect(iconX + 10, iconY + 10, iconSize - 20, iconSize - 20), Texture2D.whiteTexture);
                }

                skillKeyNumCache.normal.textColor = canUse
                    ? new Color(1f, 0.85f, 0.3f, pulse + 0.5f)
                    : new Color(0.35f, 0.35f, 0.35f);
                GUI.color = canUse ? new Color(0.15f, 0.12f, 0.05f) : new Color(0.08f, 0.08f, 0.08f);
                GUI.DrawTexture(new Rect(bx + btnW - 48, btnY + 10, 38, 38), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(bx + btnW - 48, btnY + 10, 38, 38), $"{i + 1}", skillKeyNumCache);

                skillNameStyleCache.normal.textColor = canUse ? Color.white : new Color(0.4f, 0.4f, 0.4f);
                GUI.Label(new Rect(bx + 14, btnY + 58, btnW - 28, 34), skill.displayName, skillNameStyleCache);

                skillTypeLabelCache.normal.textColor = canUse ? skillIconCol : new Color(0.3f, 0.3f, 0.3f);
                string typeStr = skill.effectType == SkillEffectType.Damage ? "공격 스킬" :
                                 skill.effectType == SkillEffectType.BuffAttack ? "버프 스킬" : "디버프 스킬";
                GUI.Label(new Rect(bx + 14, btnY + 96, btnW - 28, 26), typeStr, skillTypeLabelCache);

                skillInfoStyleCache.normal.textColor = canUse ? new Color(0.9f, 0.85f, 0.65f) : new Color(0.3f, 0.3f, 0.3f);
                string powerStr = skill.effectType == SkillEffectType.Damage ? $"위력: {skill.power}" :
                                  skill.effectType == SkillEffectType.BuffAttack ? "공격력 UP" : "공격력 DOWN";
                GUI.Label(new Rect(bx + 14, btnY + 126, btnW - 28, 28), powerStr, skillInfoStyleCache);

                if (cd > 0)
                {
                    GUI.Label(new Rect(bx, btnY + 162, btnW - 14, 28), $"쿨다운 {cd}턴", skillCdStyleCache);

                    GUI.color = new Color(1f, 0.3f, 0.2f, 0.15f);
                    GUI.DrawTexture(new Rect(bx, btnY, btnW, btnH), Texture2D.whiteTexture);
                }
                else if (skill.cooldownTurns > 0)
                {
                    GUI.Label(new Rect(bx, btnY + 166, btnW - 14, 24), $"쿨다운: {skill.cooldownTurns}턴", skillCdInfoCache);
                }

                if (isHovered)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.06f);
                    GUI.DrawTexture(new Rect(bx, btnY, btnW, btnH), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }

            float extraX = startX + count * (btnW + gap) + 24;
            float extraBtnH = 80f;

            {
                basicAtkRect = new Rect(extraX, btnY, extraW, extraBtnH);
                Vector2 mouseGui = UIScale.VirtualMousePosition;
                bool hov = basicAtkRect.Contains(mouseGui);

                GUI.color = hov ? new Color(0.22f, 0.18f, 0.10f) : new Color(0.14f, 0.12f, 0.08f);
                GUI.DrawTexture(basicAtkRect, Texture2D.whiteTexture);
                GUI.color = new Color(0.9f, 0.6f, 0.2f);
                GUI.DrawTexture(new Rect(extraX, btnY, extraW, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(extraX, btnY, 2, extraBtnH), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(extraX + extraW - 2, btnY, 2, extraBtnH), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Label(new Rect(extraX, btnY + 8, extraW, 32), "[F] 기본 공격", skillFKeyCache);
                GUI.Label(new Rect(extraX, btnY + 44, extraW, 26), "쿨다운 없음", skillFInfoCache);
            }

            {
                float escY = btnY + extraBtnH + 12;
                escapeRect = new Rect(extraX, escY, extraW, extraBtnH);
                Vector2 mouseGui2 = UIScale.VirtualMousePosition;
                bool hov2 = escapeRect.Contains(mouseGui2);

                GUI.color = hov2 ? new Color(0.22f, 0.10f, 0.10f) : new Color(0.12f, 0.08f, 0.08f);
                GUI.DrawTexture(escapeRect, Texture2D.whiteTexture);
                GUI.color = new Color(0.7f, 0.3f, 0.3f);
                GUI.DrawTexture(new Rect(extraX, escY, extraW, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(extraX, escY, 2, extraBtnH), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(extraX + extraW - 2, escY, 2, extraBtnH), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Label(new Rect(extraX, escY + 8, extraW, 32), "[ESC] 도망가기", skillEscStyleCache);
                GUI.Label(new Rect(extraX, escY + 44, extraW, 26), "확률적 성공", skillEscInfoCache);
            }
        }

        private Color GetElementColor(InsectElement element)
        {
            switch (element)
            {
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
            // 3D 모드: 2D 이펙트 대신 3D 공격 (BattleArenaController의 코루틴이 처리)
            // 여기서는 데미지 숫자 + 스킬 이름만 OnGUI로 표시
            if (arena != null && arena.IsActive)
            {
                float t3d = Mathf.Clamp01(phaseTimer / 0.8f);
                int dmg3d = isPlayerAttack ? lastDamageToEnemy : lastDamageToPlayer;
                if (dmg3d > 0 && t3d >= 0.3f)
                {
                    float sw3d = UIScale.VirtualScreenWidth;
                    float sh3d = UIScale.VirtualScreenHeight;
                    float dmgY = sh3d * 0.25f - (t3d - 0.3f) * 60f;

                    // 크리티컬이면 더 큰 폰트 + 펄스 + CRITICAL! 라벨
                    bool isCrit = isPlayerAttack && lastWasCritical;
                    float critPulse = isCrit ? 1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 20f)) * 0.15f : 1f;
                    int dmgFontSize = isCrit ? Mathf.RoundToInt(64f * critPulse) : 38;

                    dmgStyle3dCache.fontSize = dmgFontSize;
                    dmgStyle3dCache.normal.textColor = isCrit
                        ? new Color(1f, 0.5f, 0.1f)
                        : (isPlayerAttack ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 0.3f, 0.3f));

                    // 크리티컬 그림자 효과
                    if (isCrit)
                    {
                        GUI.color = new Color(0f, 0f, 0f, 0.8f);
                        GUI.Label(new Rect(3, dmgY + 3, sw3d, 80), $"-{dmg3d}", dmgStyle3dCache);
                        GUI.color = Color.white;
                    }
                    GUI.Label(new Rect(0, dmgY, sw3d, 80), $"-{dmg3d}", dmgStyle3dCache);

                    if (isCrit)
                    {
                        GUI.Label(new Rect(0, dmgY - 36, sw3d, 36), "★ CRITICAL! ★", critLblCache);
                    }

                    if (!string.IsNullOrEmpty(lastSkillName))
                    {
                        float skillY = isCrit ? dmgY + 72 : dmgY + 45;
                        GUI.Label(new Rect(0, skillY, sw3d, 30), lastSkillName, skillStyle3dCache);
                    }
                }
                return;
            }

            float t = Mathf.Clamp01(phaseTimer / 0.8f);
            float arenaTop = UIScale.VirtualScreenHeight * 0.08f;
            float arenaH = UIScale.VirtualScreenHeight * 0.52f;

            float atkX = isPlayerAttack ? UIScale.VirtualScreenWidth * 0.22f : UIScale.VirtualScreenWidth * 0.72f;
            float atkY = isPlayerAttack ? arenaTop + arenaH * 0.72f : arenaTop + arenaH * 0.38f;
            float tgtX = isPlayerAttack ? UIScale.VirtualScreenWidth * 0.72f : UIScale.VirtualScreenWidth * 0.22f;
            float tgtY = isPlayerAttack ? arenaTop + arenaH * 0.38f : arenaTop + arenaH * 0.72f;

            int dmg = isPlayerAttack ? lastDamageToEnemy : lastDamageToPlayer;

            // Element-based color
            InsectBattleStats atkStats = isPlayerAttack ? playerStats : enemyStats;
            InsectElement element = (atkStats != null && atkStats.Data != null) ? atkStats.Data.primaryType : InsectElement.Bug;
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
            if (t >= 0.3f && t < 0.7f && dmg > 0)
            {
                if (AudioManager.Instance != null && phaseTimer >= 0.24f && phaseTimer < 0.26f)
                    AudioManager.Instance.PlaySFX(SfxType.Hit);
                float impactT = (t - 0.3f) / 0.4f;
                DrawElementImpact(tgtX, tgtY, impactT, element, elemCol);
            }

            // Phase 3: Damage numbers and skill name
            if (dmg > 0 && t >= 0.3f)
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
            if (string.IsNullOrEmpty(actionText)) return;

            float alpha = Mathf.Clamp01(actionTimer / 0.3f);
            float cx = UIScale.VirtualScreenWidth / 2f;
            float cy = UIScale.VirtualScreenHeight * 0.62f;

            GUI.color = new Color(0, 0, 0, 0.8f * alpha);
            GUI.DrawTexture(new Rect(cx - 320, cy - 8, 640, 56), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.5f, 0.9f, 0.5f * alpha);
            GUI.DrawTexture(new Rect(cx - 320, cy - 8, 640, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 320, cy + 45, 640, 3), Texture2D.whiteTexture);

            actionTextStyleCache.normal.textColor = new Color(1, 1, 1, alpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(cx - 320, cy - 2, 640, 48), actionText, actionTextStyleCache);
        }

        private void DrawResult()
        {
            float alpha = Mathf.Clamp01(resultTimer / 0.5f);
            float cx = UIScale.VirtualScreenWidth / 2f;
            float cy = UIScale.VirtualScreenHeight * 0.3f;
            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;

            if (lastWon)
            {
                // Victory: golden background glow
                float bgGlow = 0.06f * alpha + Mathf.Sin(resultTimer * 2f) * 0.02f;
                GUI.color = new Color(1f, 0.85f, 0.2f, bgGlow);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);

                // Star/radial line burst effect
                if (resultTimer < 3f)
                {
                    float starT = Mathf.Clamp01(resultTimer / 2f);
                    int starCount = 12;
                    for (int i = 0; i < starCount; i++)
                    {
                        float angle = i * (360f / starCount) * Mathf.Deg2Rad + resultTimer * 0.8f;
                        float lineStart = 50f + starT * 30f;
                        float lineEnd = 100f + starT * 180f;
                        float lineAlpha = (1f - starT * 0.5f) * 0.4f * alpha;
                        float lineW = 2.5f * (1f - starT * 0.4f);

                        float x1 = cx + Mathf.Cos(angle) * lineStart;
                        float y1 = cy + 30 + Mathf.Sin(angle) * lineStart;
                        float x2 = cx + Mathf.Cos(angle) * lineEnd;
                        float y2 = cy + 30 + Mathf.Sin(angle) * lineEnd;

                        GUI.color = new Color(1f, 0.9f, 0.3f, lineAlpha);
                        GUI.DrawTexture(new Rect(
                            Mathf.Min(x1, x2), Mathf.Min(y1, y2),
                            Mathf.Max(Mathf.Abs(x2 - x1), lineW), Mathf.Max(Mathf.Abs(y2 - y1), lineW)),
                            Texture2D.whiteTexture);
                    }

                    // Sparkle particles
                    for (int i = 0; i < 6; i++)
                    {
                        float sparkAngle = i * 60f * Mathf.Deg2Rad + resultTimer * 1.5f;
                        float sparkDist = 80f + Mathf.Sin(resultTimer * 3f + i) * 40f;
                        float sparkX = cx + Mathf.Cos(sparkAngle) * sparkDist;
                        float sparkY = cy + 30 + Mathf.Sin(sparkAngle) * sparkDist;
                        float sparkAlpha = 0.5f + Mathf.Sin(resultTimer * 5f + i * 1.2f) * 0.3f;
                        float sparkSize = 4f + Mathf.Sin(resultTimer * 4f + i) * 2f;
                        GUI.color = new Color(1f, 1f, 0.6f, sparkAlpha * alpha);
                        GUI.DrawTexture(new Rect(sparkX - sparkSize, sparkY - sparkSize, sparkSize * 2, sparkSize * 2), Texture2D.whiteTexture);
                    }
                }

                // Main panel
                GUI.color = new Color(0.05f, 0.04f, 0.02f, 0.8f * alpha);
                GUI.DrawTexture(new Rect(cx - 320, cy - 20, 640, 200), Texture2D.whiteTexture);
                // Gold borders
                Color gold = new Color(1f, 0.85f, 0.2f);
                GUI.color = new Color(gold.r, gold.g, gold.b, 0.8f * alpha);
                GUI.DrawTexture(new Rect(cx - 320, cy - 20, 640, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 320, cy + 176, 640, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 320, cy - 20, 3, 200), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 317, cy - 20, 3, 200), Texture2D.whiteTexture);

                // "VICTORY!" text with scale animation
                float victoryScale = 1f + Mathf.Sin(resultTimer * 2.5f) * 0.05f;
                int victoryFontSize = (int)(52 * victoryScale);
                victoryStyleCache.fontSize = victoryFontSize;
                victoryStyleCache.normal.textColor = new Color(gold.r, gold.g, gold.b, alpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 280, cy, 560, 60), "VICTORY!", victoryStyleCache);

                // Reward info with slide-in animation
                if (battleController != null)
                {
                    int candy = battleController.GetLastCandyReward();
                    int exp = battleController.GetLastExpReward();

                    float rewardAlpha = Mathf.Clamp01((resultTimer - 0.5f) / 0.5f);
                    float rewardSlide = Mathf.Lerp(30f, 0f, Mathf.Clamp01((resultTimer - 0.5f) / 0.4f));

                    rewardStyleCache.normal.textColor = new Color(0.9f, 0.9f, 0.9f, rewardAlpha);
                    GUI.Label(new Rect(cx - 260, cy + 68 + rewardSlide, 520, 30), "곤충을 포획했습니다!", rewardStyleCache);

                    float valAlpha = Mathf.Clamp01((resultTimer - 0.8f) / 0.4f);
                    float valSlide = Mathf.Lerp(20f, 0f, Mathf.Clamp01((resultTimer - 0.8f) / 0.3f));

                    rewardValStyleCache.normal.textColor = new Color(1f, 0.5f, 0.8f, valAlpha);
                    GUI.Label(new Rect(cx - 200, cy + 110 + valSlide, 190, 30), $"+{candy} Candy", rewardValStyleCache);
                    rewardValStyleCache.normal.textColor = new Color(0.4f, 0.85f, 1f, valAlpha);
                    GUI.Label(new Rect(cx + 10, cy + 110 + valSlide, 190, 30), $"+{exp} XP", rewardValStyleCache);

                    // Subtle candy/exp glow
                    if (valAlpha > 0.5f)
                    {
                        GUI.color = new Color(1f, 0.5f, 0.8f, 0.05f * valAlpha);
                        GUI.DrawTexture(new Rect(cx - 200, cy + 108 + valSlide, 190, 34), Texture2D.whiteTexture);
                        GUI.color = new Color(0.4f, 0.85f, 1f, 0.05f * valAlpha);
                        GUI.DrawTexture(new Rect(cx + 10, cy + 108 + valSlide, 190, 34), Texture2D.whiteTexture);
                    }
                }
            }
            else
            {
                // Defeat: darkening background
                float darkOverlay = Mathf.Clamp01(resultTimer / 1.5f) * 0.5f;
                GUI.color = new Color(0, 0, 0, darkOverlay);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);

                // Red vignette on edges
                float vignetteAlpha = 0.15f * alpha;
                GUI.color = new Color(0.5f, 0, 0, vignetteAlpha);
                GUI.DrawTexture(new Rect(0, 0, sw * 0.1f, sh), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(sw * 0.9f, 0, sw * 0.1f, sh), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, 0, sw, sh * 0.08f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, sh * 0.92f, sw, sh * 0.08f), Texture2D.whiteTexture);

                // Main panel
                GUI.color = new Color(0.08f, 0.02f, 0.02f, 0.85f * alpha);
                GUI.DrawTexture(new Rect(cx - 320, cy - 20, 640, 180), Texture2D.whiteTexture);
                // Red borders
                Color red = new Color(0.9f, 0.25f, 0.2f);
                GUI.color = new Color(red.r, red.g, red.b, 0.7f * alpha);
                GUI.DrawTexture(new Rect(cx - 320, cy - 20, 640, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 320, cy + 156, 640, 4), Texture2D.whiteTexture);

                // "DEFEAT..." text
                defeatStyleCache.normal.textColor = new Color(red.r, red.g, red.b, alpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 280, cy + 4, 560, 60), "DEFEAT...", defeatStyleCache);

                // Retry guidance with fade-in
                float guideAlpha = Mathf.Clamp01((resultTimer - 1f) / 0.5f);
                defeatGuideStyleCache.normal.textColor = new Color(0.7f, 0.5f, 0.5f, guideAlpha);
                GUI.Label(new Rect(cx - 260, cy + 80, 520, 28), "곤충을 강화하고 다시 도전하세요!", defeatGuideStyleCache);

                // Subtle pulsing hint
                float hintPulse = 0.4f + Mathf.Sin(resultTimer * 3f) * 0.2f;
                defeatHintStyleCache.normal.textColor = new Color(0.5f, 0.4f, 0.4f, hintPulse * guideAlpha);
                GUI.Label(new Rect(cx - 200, cy + 116, 400, 24), "훈련소에서 레벨업 가능", defeatHintStyleCache);
            }

            GUI.color = Color.white;
        }

        private void DrawPhaseIndicator(string text)
        {
            float panelH = 70f;
            float panelY = UIScale.VirtualScreenHeight - panelH;
            GUI.color = new Color(0.04f, 0.05f, 0.10f, 0.92f);
            GUI.DrawTexture(new Rect(0, panelY, UIScale.VirtualScreenWidth, panelH), Texture2D.whiteTexture);
            GUI.color = new Color(0.5f, 0.7f, 1f, 0.4f);
            GUI.DrawTexture(new Rect(0, panelY, UIScale.VirtualScreenWidth, 3), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float pulse = 0.6f + Mathf.Sin(Time.time * 5f) * 0.2f;
            phaseIndicatorStyleCache.normal.textColor = new Color(1f, 0.9f, 0.5f, pulse);
            GUI.Label(new Rect(0, panelY + 14, UIScale.VirtualScreenWidth, 40), text, phaseIndicatorStyleCache);
        }

        // FindFirstObjectByType 캐싱 — 배틀 시작/종료마다 재조회 회귀 차단.
        // ForceHidePanel은 InsectBattleUIController가 영구 객체라 1회 조회 후 재사용 안전.
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
            float panelW = UIScale.VirtualScreenWidth;
            float panelH = 340f;
            float panelY = UIScale.VirtualScreenHeight - panelH;

            GUI.color = new Color(0.04f, 0.03f, 0.08f, 0.97f);
            GUI.DrawTexture(new Rect(0, panelY, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.35f, 0.3f);
            GUI.DrawTexture(new Rect(0, panelY, panelW, 4), Texture2D.whiteTexture);

            float pulse = 0.7f + Mathf.Sin(Time.time * 3f) * 0.3f;
            // SwapHeader 색 static readonly + alpha만 갱신 (DrawCombo의 ComboCol 패턴과 동일)
            Color hdr = SwapHeaderBase;
            hdr.a = pulse;
            swapHeaderCache.normal.textColor = hdr;
            GUI.color = Color.white;
            string faintedName = playerStats != null && playerStats.Data != null ? playerStats.Data.displayName : "곤충";
            GUI.Label(new Rect(0, panelY + 10, panelW, 38),
                $"{faintedName}이(가) 쓰러졌다! 다음 곤충을 선택하세요 (숫자키 1~5 또는 클릭)", swapHeaderCache);

            float btnW = 240f;
            float btnH = 240f;
            float btnY = panelY + 56;
            float totalW = BattleTeamManager.MaxSlots * (btnW + 14) - 14;
            float startX = (panelW - totalW) / 2f;

            for (int i = 0; i < swapBtnRects.Length; i++)
            {
                swapBtnRects[i] = new Rect(0, 0, 0, 0);
                swapBtnAvail[i] = false;
            }

            if (teamManager == null || collection == null) return;

            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                float bx = startX + i * (btnW + 14);
                string slotId = teamManager.GetSlot(i);
                bool isEmpty = string.IsNullOrEmpty(slotId);
                bool isFainted = !isEmpty && faintedInsectIds.Contains(slotId);
                bool isCurrent = !isEmpty && slotId == currentInsectId;
                PlayerInsectData pid = isEmpty ? null : collection.GetByInstanceId(slotId);
                InsectData data = pid != null ? collection.GetInsectData(pid.insectId) : null;
                bool available = !isEmpty && !isFainted && !isCurrent && data != null;

                swapBtnRects[i] = new Rect(bx, btnY, btnW, btnH);
                swapBtnAvail[i] = available;

                Vector2 mouseGui = UIScale.VirtualMousePosition;
                bool hovered = available && swapBtnRects[i].Contains(mouseGui);

                Color bgCol;
                if (isFainted || isCurrent)
                    bgCol = new Color(0.08f, 0.05f, 0.05f, 0.9f);
                else if (hovered)
                    bgCol = new Color(0.15f, 0.20f, 0.35f);
                else if (available)
                    bgCol = new Color(0.10f, 0.12f, 0.20f);
                else
                    bgCol = new Color(0.06f, 0.06f, 0.08f);

                GUI.color = bgCol;
                GUI.DrawTexture(new Rect(bx, btnY, btnW, btnH), Texture2D.whiteTexture);

                if (available)
                {
                    Color borderCol2 = hovered ? Color.white : new Color(0.3f, 0.6f, 0.9f);
                    GUI.color = borderCol2;
                    GUI.DrawTexture(new Rect(bx, btnY, btnW, 4), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(bx, btnY + btnH - 3, btnW, 3), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(bx, btnY, 2, btnH), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(bx + btnW - 2, btnY, 2, btnH), Texture2D.whiteTexture);
                }

                swapKeyStyleCache.normal.textColor = available ? new Color(1f, 0.85f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);
                GUI.color = available ? new Color(0.15f, 0.12f, 0.05f) : new Color(0.06f, 0.06f, 0.06f);
                GUI.DrawTexture(new Rect(bx + 8, btnY + 8, 36, 36), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(bx + 8, btnY + 8, 36, 36), $"{i + 1}", swapKeyStyleCache);

                if (isEmpty || data == null)
                {
                    GUI.Label(new Rect(bx, btnY + 90, btnW, 30), "빈 슬롯", swapEmptyStyleCache);
                    continue;
                }

                int level = pid != null ? pid.level : 1;
                int cp = PlayerInsectCombatPower.Calculate(data, pid);

                Color rarityCol = UITheme.Instance.GetInsectRarityColor(data.rarity);
                Color insectCol = UITheme.Instance.GetInsectColor(data.insectId, data.rarity);

                GUI.color = new Color(insectCol.r, insectCol.g, insectCol.b, 0.15f);
                GUI.DrawTexture(new Rect(bx + btnW / 2f - 35, btnY + 48, 70, 70), Texture2D.whiteTexture);
                GUI.color = insectCol;
                GUI.DrawTexture(new Rect(bx + btnW / 2f - 26, btnY + 57, 52, 52), Texture2D.whiteTexture);

                swapNameStyleCache.normal.textColor = available ? rarityCol : new Color(rarityCol.r * 0.4f, rarityCol.g * 0.4f, rarityCol.b * 0.4f);
                GUI.color = Color.white;
                GUI.Label(new Rect(bx, btnY + 126, btnW, 28), data.displayName, swapNameStyleCache);

                swapInfoStyleCache.normal.textColor = available ? new Color(0.6f, 0.6f, 0.65f) : new Color(0.3f, 0.3f, 0.3f);
                GUI.Label(new Rect(bx, btnY + 156, btnW, 24), $"Lv.{level}  |  CP {cp}", swapInfoStyleCache);

                if (pid != null)
                {
                    int hp = pid.GetTotalHp(data.baseHp);
                    int atk = pid.GetTotalAtk(data.baseAtk);
                    swapStatStyleCache.normal.textColor = available ? new Color(0.5f, 0.5f, 0.55f) : new Color(0.25f, 0.25f, 0.25f);
                    GUI.Label(new Rect(bx, btnY + 182, btnW, 22), $"HP {hp}  ATK {atk}", swapStatStyleCache);
                }

                if (isFainted)
                {
                    GUI.Label(new Rect(bx, btnY + 208, btnW, 28), "쓰러짐", swapFaintStyleCache);
                }
                else if (isCurrent)
                {
                    GUI.Label(new Rect(bx, btnY + 208, btnW, 28), "현재 (쓰러짐)", swapCurStyleCache);
                }

                if (hovered)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.06f);
                    GUI.DrawTexture(new Rect(bx, btnY, btnW, btnH), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
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

                if (slowMoTimer > 0f) { Time.timeScale = 1f; slowMoTimer = 0f; }
                comboCount = 0;
                comboDisplayTimer = 0f;

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBGM(BgmType.Explore);
                    AudioManager.Instance.ClearBattleIntensity();
                }
                phase = Phase.None;
                playerStats = null;
                enemyStats = null;
                skillBtnCount = 0;
                faintedInsectIds.Clear();
                currentInsectId = null;
            }
            finally
            {
                if (Time.timeScale != 1f) Time.timeScale = 1f;
                if (cameraFollower != null) cameraFollower.ExitBattleMode();
                if (playerMovement != null) playerMovement.SetFrozen(false);
            }
        }

        private void DrawScreenFlash()
        {
            if (screenFlashTimer <= 0f) return;
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
            GUI.Label(new Rect(bx, by + boxH - 22, boxW, 18), "COMBO", cachedComboLblStyle);
        }

        // 콤보 표시 정적 색 — 매 OnGUI new Color 회귀 차단
        private static readonly Color ComboColHot = new Color(1f, 0.4f, 0.2f);
        private static readonly Color ComboColWarm = new Color(1f, 0.85f, 0.3f);
        private static readonly Color ComboColCool = new Color(0.9f, 0.95f, 1f);
        private static readonly Color ComboLblBase = new Color(1f, 1f, 1f);
        // DrawSwapSelect 헤더 펄스 색 — alpha만 동적, RGB는 static
        private static readonly Color SwapHeaderBase = new Color(1f, 0.4f, 0.3f);

        private Color GetSkillColor(SkillEffectType type)
        {
            switch (type)
            {
                case SkillEffectType.Damage: return new Color(0.9f, 0.35f, 0.3f);
                case SkillEffectType.BuffAttack: return new Color(0.3f, 0.8f, 0.4f);
                case SkillEffectType.DebuffAttack: return new Color(0.7f, 0.4f, 0.9f);
                default: return Color.gray;
            }
        }

        public void AutoWire(InsectBattleController bc, CameraFollower cam, PlayerMovement pm = null)
        {
            if (battleController != null && battleController != bc)
            {
                battleController.BattleUpdated -= OnBattleUpdated;
                battleController.BattleEnded -= OnBattleEnded;
                battleController.PlayerFainted -= OnPlayerFainted;
            }

            if (battleController == null || battleController != bc)
            {
                battleController = bc;
                if (battleController != null)
                {
                    battleController.BattleUpdated -= OnBattleUpdated;
                    battleController.BattleEnded -= OnBattleEnded;
                    battleController.PlayerFainted -= OnPlayerFainted;
                    battleController.BattleUpdated += OnBattleUpdated;
                    battleController.BattleEnded += OnBattleEnded;
                    battleController.PlayerFainted += OnPlayerFainted;
                }
            }

            if (cameraFollower == null) cameraFollower = cam;
            if (playerMovement == null) playerMovement = pm;
        }

        public void AutoWire(BattleTeamManager team, PlayerInsectCollection col, TrainingManager training)
        {
            if (teamManager == null) teamManager = team;
            if (collection == null) collection = col;
            if (trainingManager == null) trainingManager = training;
        }

        public void AutoWire(BattleArenaController a)
        {
            if (arena == null) arena = a;
        }
    }
}
