using System;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace InsectGame.Battle
{
    public class InsectBattleController : MonoBehaviour
    {
        [SerializeField] private PlayerInsectCollection playerCollection;
        [SerializeField] private PlayerCandyInventory candyInventory;
        [SerializeField] private PlayerProgressController playerProgress;
        [SerializeField] private PlayerItemInventory itemInventory;
        [SerializeField] private Dex.DexController dexController;
        [SerializeField] private BattleArenaController arena;
        // 배틀 승리 시 소량 코인 지급용 — 상점 코인결제·베이직 의상의 지속 수급원. Dex 첫 발견 보상은
        // 곤충당 1회뿐이라 반복 소모품 구매를 지탱하지 못하므로 별개의 반복 faucet로 보강한다.
        [SerializeField] private PlayerCurrencyWallet wallet;

        // 배틀 승리 코인 보상(고정 소량). 근거: sim 기준 전투 10/일 × 승률 → 일 ~24코인 수준의 트리클로,
        // 상점 소모품 재구매를 지탱하되 Dex faucet(생애 3958)·젬 경제를 압도하지 않는다.
        private const int BattleVictoryCoins = 3;

        public event Action<bool> BattleEnded;
        /// <summary>NPC 대결이 끝났을 때만 발화(승리 여부). 야생 전투에서는 울리지 않는다.</summary>
        public event Action<bool> DuelEnded;
        public event Action<InsectBattleStats, InsectBattleStats> BattleUpdated;
        public event Action PlayerFainted;

        // 적이 직전 턴에 사용한 스킬(null=기본공격/쿨다운) — UI가 EnemyAttack 페이즈 연출에 사용.
        public InsectSkill LastEnemySkill { get; private set; }

        /// <summary>
        /// 이번 전투 상대의 <b>종 ID</b>. 아직 전투를 시작하지 않았으면 null.
        ///
        /// <c>BattleEnded</c>가 <c>Action&lt;bool&gt;</c>이라 "무엇을 이겼는가"를 못 싣는다.
        /// 시그니처를 바꾸면 구독자 셋(UI·튜토리얼 퀘스트·스토리)과 배치 검증 도구의 리플렉션이
        /// 함께 따라오므로, 대신 <b>발화 시점에 읽을 수 있는 상태</b>로 노출한다.
        /// <c>enemyStats</c>는 <c>BeginBattleCommon</c>에서 한 번만 잡히고 전투 중 교체되지 않으므로
        /// <c>BattleEnded</c> 시점에도 유효하다.
        ///
        /// <c>StoryDirector</c>가 <c>BattleWin</c> 트리거의 종 지정에 쓴다 — 그쪽은 결과 화면
        /// 뒤로 발화를 <b>미루므로</b> 이 값을 그때 다시 읽으면 늦다. 미룰 때 param에 실어야 한다.
        /// </summary>
        public string EnemyInsectId =>
            enemyStats != null && enemyStats.Data != null ? enemyStats.Data.insectId : null;

        private InsectBattleStats playerStats;
        private InsectBattleStats enemyStats;
        private InsectEntity enemyEntity;
        private bool enemyShinyAtStart; // 시작 시점 스냅샷 — 도주/풀 재사용된 라이브 참조로 보상 오등록 방지
        private bool duelMode;          // NPC 대결 — 포획 롤·야생 아이템 드랍 없음(StartDuel 참조)

        // ── 「장부」 압박(명부회 보스전 전용) ─────────────────────────────
        // 규칙과 상수는 LedgerPressure(순수부)가 들고, 임계는 NpcBossDuels 표가 든다.
        // 여기는 그 둘을 잇는 **상태**만 갖는다.
        private int ledgerThreshold;                              // 0 = 장부 없음(야생·아이 대결)
        private int ledgerTally;
        private int lastActionKey = LedgerPressure.NoActionKey;
        // 이번 적 턴에 「장부에 올랐다」가 터졌는가 — GetDamage가 읽어 피해를 키운다.
        // 이번 적 턴에 정독을 **겨눴는가**(장부가 찼다)와 **실제로 썼는가**(배율이 걸렸다)를
        // 가른다. 둘을 하나로 두면 곱할 피해가 없던 턴·빗나간 턴에도 장부가 비워진다.
        private bool ledgerArmedThisTurn;
        private bool ledgerSpentThisTurn;
        private int ledgerReadCount;
        private InsectSkill[] playerOverrideSkills;
        private int[] playerCooldowns;
        private int enemyCooldown;
        private int playerStunTurns;   // >0이면 플레이어 다음 행동 스킵(P4 Stun)
        private int enemyStunTurns;    // >0이면 적 다음 행동 스킵
        // 지속 상태(전투 간 유지) — 현재 활성 플레이어 곤충의 감염. 전투 시작/교체 시 seed, 종료/교체 시 write-back.
        private bool playerPoisoned;
        private bool playerParalyzed;
        private const int PersistentPoisonDamage = 8;   // 감염 곤충의 전투 시작 시 재적용 독 피해(턴당)
        private const int PersistentPoisonTurns = 3;
        private readonly List<ActiveEffect> effects = new List<ActiveEffect>();
        private int lastCandyReward;
        private int lastExpReward;
        // 종료 가드 — 승리/패배 처리(보상 지급·BattleEnded)를 1회로 제한.
        // 없으면 종료 후 액션이 한 번 더 들어올 때(rapid tap/입력 큐) 보상 이중 지급.
        private bool battleEnded;
        private string lastItemId;
        private int lastItemCount;
        private bool lastPlayerWon;
        private bool lastCaptureAttempted;
        private bool lastCaptureSucceeded;
        private float lastCaptureChance;

        private BattleArenaController Arena => arena;

        public void StartBattle(InsectData playerInsect, int playerLevel, InsectEntity enemy, Action<InsectBattleStats, InsectBattleStats> onStarted = null, InsectSkill[] equippedSkills = null, Core.PlayerInsectData playerPid = null)
        {
            if (playerInsect == null || enemy == null || enemy.Data == null)
            {
                return;
            }

            // 기절(0 HP) 곤충 출전 백스톱 — UI 가드를 우회한 진입점(leader=insects[0] 등)도 즉사 반복 방지.
            if (playerPid != null && playerPid.IsFainted)
            {
                return;
            }

            // 배틀 동안 야생 엔티티가 도주(patience 소진)→Despawn→풀 재사용되어 종료 시 엉뚱한
            // 곤충이 포획/디스폰되는 보상 무결성 손상을 차단. (미니게임/선택 UI는 이미 SetEngaged)
            enemy.SetEngaged(true);
            BeginBattleCommon(playerInsect, playerLevel, enemy.Data, enemy.Level, equippedSkills, playerPid);
            enemyEntity = enemy;
            enemyShinyAtStart = enemy.IsShiny;
            duelMode = false;
            onStarted?.Invoke(playerStats, enemyStats);
            BattleUpdated?.Invoke(playerStats, enemyStats);
        }

        /// <summary>
        /// NPC 대결 — 월드 엔티티 없이 데이터만으로 붙는다. 상대 곤충은 NPC의 것이므로
        /// 포획 롤과 야생 아이템 드랍을 건너뛴다(캔디·EXP·코인은 정상 지급).
        /// 아이템 보상은 승부를 건 쪽(NpcDuelController)이 준다.
        /// </summary>
        public bool StartDuel(InsectData playerInsect, int playerLevel, InsectData enemyInsect, int enemyLevel,
            Action<InsectBattleStats, InsectBattleStats> onStarted = null, InsectSkill[] equippedSkills = null,
            Core.PlayerInsectData playerPid = null)
        {
            if (playerInsect == null || enemyInsect == null) return false;
            if (playerPid != null && playerPid.IsFainted) return false;

            BeginBattleCommon(playerInsect, playerLevel, enemyInsect, enemyLevel, equippedSkills, playerPid);
            enemyEntity = null;          // 디스폰할 월드 개체가 없다
            enemyShinyAtStart = false;
            duelMode = true;
            onStarted?.Invoke(playerStats, enemyStats);
            BattleUpdated?.Invoke(playerStats, enemyStats);
            return true;
        }

        // StartBattle/StartDuel이 공유하는 초기화 — 야생/듀얼 차이는 호출부가 뒤에 덮는다.
        private void BeginBattleCommon(InsectData playerInsect, int playerLevel,
            InsectData enemyInsect, int enemyLevel, InsectSkill[] equippedSkills, Core.PlayerInsectData playerPid)
        {
            playerStats = new InsectBattleStats(playerInsect, playerLevel, playerPid);
            enemyStats = new InsectBattleStats(enemyInsect, enemyLevel);
            playerOverrideSkills = ResolvePlayerSkills(playerInsect, equippedSkills, playerPid);
            int skillCount = playerOverrideSkills != null ? playerOverrideSkills.Length : (playerInsect.skills != null ? playerInsect.skills.Length : 0);
            playerCooldowns = new int[skillCount];
            enemyCooldown = 0;
            playerStunTurns = 0;
            enemyStunTurns = 0;
            effects.Clear();
            SeedPersistentState(playerPid);   // 지속 독/마비 재적용
            // 의상·아이템 보너스를 **첫 턴부터** 태운다. 없으면 AttackBonus/DefenseBonus가 0으로
            // 시작해, 첫 공격은 의상 ATK 배율을 못 받고 적의 첫 반격은 의상 DEF를 못 받는다
            // (RecalculateBonuses는 TickEffects·AddEffect·SwapPlayerInsect에서만 불렸다).
            // 하필 `SeedPersistentState`가 **감염된 곤충일 때만** AddEffect→Recalculate를 태워서,
            // "독에 걸려 있어야 의상 보너스가 켜지는" 상태였다. 교체(SwapPlayerInsect)와 레이드
            // (RaidBattleController가 시작 시 직접 대입)는 이미 첫 턴부터 적용된다 — 여기만 빠졌다.
            RecalculateBonuses();
            // 장부는 보스 듀얼에서만 켠다. **여기서 반드시 끈다** — 안 그러면 보스전 뒤
            // 이어지는 야생 전투가 임계를 물려받아 잡곤충이 정독을 쓴다.
            ledgerThreshold = 0;
            ledgerTally = 0;
            lastActionKey = LedgerPressure.NoActionKey;
            ledgerArmedThisTurn = false;
            ledgerSpentThisTurn = false;
            ledgerReadCount = 0;
            lastCandyReward = 0;
            lastExpReward = 0;
            lastItemId = string.Empty;
            lastItemCount = 0;
            lastPlayerWon = false;
            lastCaptureAttempted = false;
            lastCaptureSucceeded = false;
            lastCaptureChance = 0f;
            battleEnded = false;
            // onStarted/BattleUpdated는 호출부가 야생/듀얼 고유 필드(enemyEntity 등)를 채운 뒤에 울린다 —
            // BattleScreenUI.OnBattleUpdated가 GetEnemyEntity()를 읽어 아레나 위치를 잡기 때문이다.
        }

        /// <summary>
        /// 이 전투에 「장부」를 건다 — <see cref="NPC.NpcDuelController.TryStartBossDuel"/>이
        /// <c>StartDuel</c> 직후에 부른다. <c>StartDuel</c>의 인자로 받지 않는 이유는
        /// 아이 대결이 같은 함수를 쓰기 때문이다(그쪽엔 장부가 없다).
        /// </summary>
        public void ArmLedger(int threshold)
        {
            ledgerThreshold = LedgerPressure.IsActive(threshold) ? threshold : 0;
            ledgerTally = 0;
            lastActionKey = LedgerPressure.NoActionKey;
            ledgerArmedThisTurn = false;
            ledgerSpentThisTurn = false;
            ledgerReadCount = 0;
        }

        /// <summary>현재 장부 값 — UI 게이지가 읽는다.</summary>
        public int LedgerTally => ledgerTally;

        /// <summary>이 전투의 장부 임계. 0이면 장부 없음(게이지를 안 그린다).</summary>
        public int LedgerThreshold => ledgerThreshold;

        /// <summary>
        /// 이번 전투에서 「장부에 올랐다」가 터진 횟수. 게이지는 터지는 순간 0으로 돌아가
        /// <b>밖에서는 발동을 볼 수 없다</b> — 값이 줄어든 게 완화(-1) 때문인지 발동 때문인지
        /// 구분이 안 된다. 그래서 누적 횟수를 따로 센다(배치모드 검증이 이걸 읽는다).
        /// </summary>
        public int LedgerReadCount => ledgerReadCount;

        /// <summary>
        /// 플레이어의 이번 행동을 장부에 적는다. 직전과 같은 행동이면 차고, 바꾸면 지워진다.
        /// <b>적 턴보다 먼저</b> 불려야 한다 — 그래야 이번 턴 반복이 이번 턴 반격에 반영된다.
        /// </summary>
        private void NoteLedgerAction(int actionKey)
        {
            if (!LedgerPressure.IsActive(ledgerThreshold)) return;
            bool repeated = lastActionKey != LedgerPressure.NoActionKey && lastActionKey == actionKey;
            lastActionKey = actionKey;
            ledgerTally = LedgerPressure.NextTally(ledgerTally, ledgerThreshold, repeated);
        }

        public void UseSkill(int skillIndex)
        {
            if (playerStats == null || enemyStats == null)
            {
                return;
            }
            if (battleEnded) return; // 종료 후 액션 차단

            if (!CanUseSkill(skillIndex))
            {
                return;
            }

            // 기절 상태면 이번 행동 스킵(스킬 소모 없음) — 적은 그대로 반격.
            if (playerStunTurns > 0)
            {
                playerStunTurns--;
                TryPlayEffectText("행동 불가!", new Color(1f, 0.85f, 0.3f));
            }
            else
            {
                InsectSkill[] skills = GetPlayerSkills();
                InsectSkill skill = skills != null && skillIndex < skills.Length ? skills[skillIndex] : GetSkill(playerStats.Data, skillIndex);
                ApplySkill(playerStats, enemyStats, skill, true);

                if (skill != null)
                {
                    playerCooldowns[skillIndex] = skill.cooldownTurns;
                }

                // **행동한 턴만 적힌다.** 기절로 건너뛴 턴은 보스가 볼 것이 없었다 —
                // 밖에 두면 손 못 댄 턴에 장부가 차고, 반대로 아무 키나 눌러 완화를
                // 공짜로 얻는 길도 함께 열린다.
                NoteLedgerAction(skillIndex);
            }

            if (enemyStats.CurrentHp > 0)
            {
                UseEnemyTurn();
            }

            TickEffects();
            TickCooldowns();
            BattleUpdated?.Invoke(playerStats, enemyStats);
            CheckEnd();
        }

        public void UseBasicAttack()
        {
            if (playerStats == null || enemyStats == null)
            {
                return;
            }
            if (battleEnded) return; // 종료 후 액션 차단

            if (playerStunTurns > 0)
            {
                playerStunTurns--;
                TryPlayEffectText("행동 불가!", new Color(1f, 0.85f, 0.3f));
            }
            else
            {
                int damage = Mathf.Max(1, Mathf.RoundToInt(playerStats.Attack * 0.7f));
                enemyStats.ApplyDamage(damage, playerStats.Attack, enemyStats.Defense);
                TryPlayHitFlash(false);

                // 쿨다운 없는 기본공격 연타가 이 압박이 겨냥하는 바로 그 패턴이다.
                // 기절로 건너뛴 턴은 적지 않는다(UseSkill과 같은 이유).
                NoteLedgerAction(LedgerPressure.BasicAttackKey);
            }

            if (enemyStats.CurrentHp > 0)
            {
                UseEnemyTurn();
            }

            TickEffects();
            TickCooldowns();
            BattleUpdated?.Invoke(playerStats, enemyStats);
            CheckEnd();
        }

        public bool TryEscape()
        {
            if (playerStats == null || enemyStats == null)
            {
                return false;
            }
            if (battleEnded) return false; // 종료 후 액션 차단

            int levelDiff = playerStats.Level - enemyStats.Level;
            float escapeChance = Mathf.Clamp(0.5f + levelDiff * 0.05f, 0.1f, 0.9f);
            bool escaped = UnityEngine.Random.value < escapeChance;

            if (escaped)
            {
                // 도주 성공 시에도 enemyEntity Despawn — 사용자 의도("전투 끝나면 사라져야").
                // 옛은 도주 후 곤충이 필드에 잔존했고 같은 적이 그대로 다시 만남 가능.
                if (enemyEntity != null) enemyEntity.Despawn();
                PersistActivePlayer();   // 도주 시에도 남은 HP·감염 저장
                battleEnded = true;
                BattleEnded?.Invoke(false);
                // 도주도 대결의 한 결과다 — 여기서 안 알리면 NpcDuelController가
                // MarkDuelFinished를 못 걸어 90초 쿨다운이 통째로 우회된다("결과와 무관하게"가
                // 그 쿨다운의 설계 의도다). 승리/전멸패배 두 경로만 발화하던 누락.
                if (duelMode) DuelEnded?.Invoke(false);
                return true;
            }

            NoteLedgerAction(LedgerPressure.EscapeKey);
            UseEnemyTurn();
            TickEffects();
            TickCooldowns();
            BattleUpdated?.Invoke(playerStats, enemyStats);
            CheckEnd();
            return false;
        }

        public bool CanUseSkill(int skillIndex)
        {
            if (playerStats == null || playerStats.Data == null) return false;

            InsectSkill[] skills = GetPlayerSkills();
            if (skills == null || skills.Length == 0) return false;
            if (skillIndex < 0 || skillIndex >= skills.Length) return false;
            if (skills[skillIndex] == null) return false;
            if (playerCooldowns != null && skillIndex < playerCooldowns.Length && playerCooldowns[skillIndex] > 0) return false;

            return true;
        }

        public InsectSkill[] GetPlayerSkills()
        {
            if (playerOverrideSkills != null) return playerOverrideSkills;
            if (playerStats != null && playerStats.PlayerData != null && playerCollection != null)
            {
                InsectSkill[] resolved = playerCollection.GetEquippedSkills(playerStats.PlayerData);
                if (resolved != null && resolved.Length > 0)
                {
                    return resolved;
                }
            }
            return playerStats != null && playerStats.Data != null ? playerStats.Data.skills : null;
        }

        private InsectSkill GetPrimarySkill(InsectData data)
        {
            if (data == null)
            {
                return null;
            }

            if (data.learnset != null && data.learnset.Length > 0)
            {
                int currentLevel = enemyStats != null ? enemyStats.Level : data.minLevel;
                InsectSkill best = null;
                foreach (InsectLearnableSkill learnable in data.learnset)
                {
                    if (learnable == null || learnable.skill == null || learnable.learnLevel > currentLevel)
                    {
                        continue;
                    }

                    best = learnable.skill;
                }

                if (best != null)
                {
                    return best;
                }
            }

            if (data.skills == null || data.skills.Length == 0)
            {
                return null;
            }

            return data.skills[0];
        }

        private InsectSkill GetSkill(InsectData data, int index)
        {
            if (data == null || data.skills == null || data.skills.Length == 0)
            {
                return null;
            }

            if (index < 0 || index >= data.skills.Length)
            {
                return data.skills[0];
            }

            return data.skills[index];
        }

        private void ApplySkill(InsectBattleStats attacker, InsectBattleStats defender, InsectSkill skill, bool isPlayer)
        {
            // 방어자(맞는 쪽)가 플래시되어야 함. isPlayer는 공격자 기준이므로 반전.
            bool defenderIsPlayer = !isPlayer;

            if (skill == null)
            {
                int damage = GetDamage(attacker, 10);
                defender.ApplyDamage(damage, attacker.Attack, defender.Defense);
                TryPlayHitFlash(defenderIsPlayer);
                return;
            }

            // 스킬 이름 효과 텍스트 (속성 색상)
            if (!string.IsNullOrEmpty(skill.displayName))
            {
                TryPlayEffectText($"{skill.displayName}!", BattleArenaController.GetUIElementColor(skill.element));
            }

            switch (skill.effectType)
            {
                case SkillEffectType.BuffAttack:
                    if (AddEffect(isPlayer, skill.effectValue, skill.effectDurationTurns, EffectKind.AtkBuff))
                        TryPlayEffectText("공격력 상승!", new Color(1f, 0.8f, 0.3f));
                    else
                        TryPlayEffectText("이미 최대치!", new Color(0.7f, 0.7f, 0.75f));
                    break;
                case SkillEffectType.DebuffAttack:
                    if (AddEffect(!isPlayer, -skill.effectValue, skill.effectDurationTurns, EffectKind.AtkBuff))
                        TryPlayEffectText("공격력 하락!", new Color(0.6f, 0.4f, 0.9f));
                    else
                        TryPlayEffectText("이미 최대치!", new Color(0.7f, 0.7f, 0.75f));
                    break;
                case SkillEffectType.Heal:
                {
                    int healAmt = Mathf.Max(1, Mathf.RoundToInt(attacker.MaxHp * Mathf.Clamp01(skill.effectValue)));
                    attacker.Heal(healAmt);
                    TryPlayEffectText($"HP +{healAmt}!", new Color(0.4f, 1f, 0.5f));
                    break;
                }
                case SkillEffectType.PoisonDot:
                    if (!LandsHit(skill)) { TryPlayEffectText("빗나갔다!", new Color(0.7f, 0.7f, 0.75f)); break; }
                    // 대상에 턴당 피해(power) 부여. TickEffects에서 매턴 적용. 플레이어 피격 시 지속 감염(전투 후 유지).
                    AddEffect(!isPlayer, skill.power, skill.effectDurationTurns, EffectKind.Dot);
                    if (defenderIsPlayer) playerPoisoned = true;
                    TryPlayHitFlash(defenderIsPlayer);
                    TryPlayEffectText("중독!", new Color(0.6f, 0.9f, 0.3f));
                    break;
                case SkillEffectType.Stun:
                    if (!LandsHit(skill)) { TryPlayEffectText("빗나갔다!", new Color(0.7f, 0.7f, 0.75f)); break; }
                    // 대상 다음 행동 1회 스킵(별도 카운터). 플레이어 피격 시 지속 마비(전투 후 유지).
                    if (defenderIsPlayer) { playerStunTurns = 1; playerParalyzed = true; } else enemyStunTurns = 1;
                    TryPlayHitFlash(defenderIsPlayer);
                    TryPlayEffectText("기절!", new Color(1f, 0.9f, 0.3f));
                    break;
                case SkillEffectType.DefenseBuff:
                    if (AddEffect(isPlayer, skill.effectValue, skill.effectDurationTurns, EffectKind.DefBuff))
                        TryPlayEffectText("방어력 상승!", new Color(0.4f, 0.7f, 1f));
                    else
                        TryPlayEffectText("이미 최대치!", new Color(0.7f, 0.7f, 0.75f));
                    break;
                default:
                    if (!LandsHit(skill)) { TryPlayEffectText("빗나갔다!", new Color(0.7f, 0.7f, 0.75f)); break; }
                    int baseDamage = skill.power;
                    float effectiveness = InsectTypeChart.GetEffectiveness(
                        skill.element,
                        defender.Data != null ? defender.Data.primaryType : InsectElement.None,
                        defender.Data != null ? defender.Data.secondaryType : InsectElement.None);
                    float sameTypeBonus = attacker.Data != null
                        ? InsectTypeChart.GetSameTypeBonus(skill.element, attacker.Data.primaryType, attacker.Data.secondaryType)
                        : 1f;
                    int damage = Mathf.Max(1, Mathf.RoundToInt(GetDamage(attacker, baseDamage) * effectiveness * sameTypeBonus));
                    defender.ApplyDamage(damage, attacker.Attack, defender.Defense);
                    TryPlayHitFlash(defenderIsPlayer);
                    if (effectiveness > 1.05f)
                        TryPlayEffectText("효과가 굉장했다!", new Color(1f, 0.55f, 0.2f));
                    else if (effectiveness < 0.95f)
                        TryPlayEffectText("효과가 별로인 듯하다...", new Color(0.55f, 0.65f, 0.8f));
                    break;
            }
        }

        private int GetDamage(InsectBattleStats attacker, int baseDamage)
        {
            float multiplier = Mathf.Clamp(1f + attacker.AttackBonus, 0.3f, 3f);
            // 장부 정독은 **적이 때릴 때만** 걸린다. attacker 참조로 가른다 —
            // duelMode 같은 상태로 가르면 플레이어 공격까지 배율을 먹는다.
            //
            // <b>여기가 정독을 실제로 쓰는 유일한 지점이다.</b> 배율을 건 사실을
            // 그대로 남겨, 장부를 비울지 말지를 `UseEnemyTurn`이 **결과를 보고** 정한다 —
            // 이 자리에 도달하지 못한 턴(버프·회복·독·기절, 그리고 <b>빗나감</b>)은
            // 곱할 것이 없었던 턴이므로 장부를 그대로 들고 다음 턴을 기다린다.
            if (ledgerArmedThisTurn && ReferenceEquals(attacker, enemyStats))
            {
                multiplier *= LedgerPressure.ReadDamageMultiplier;
                ledgerSpentThisTurn = true;
            }
            int damage = Mathf.RoundToInt((baseDamage + attacker.Level * 2) * multiplier);
            return Mathf.Max(1, damage);
        }

        // 명중 판정(순수 로직, 주입 롤=테스트 가능). roll<순명중이면 명중. 순명중은 최소 0.3 보장(완전 회피 방지).
        public static bool RollHit(float accuracy, float evasion, float roll)
        {
            float hitChance = Mathf.Clamp(accuracy - evasion, 0.3f, 1f);
            return roll < hitChance;
        }

        // 이번 공격이 명중하는가 — accuracy 1.0(대부분)이면 항상 명중. Random 소비는 저명중 스킬에서만.
        private bool LandsHit(InsectSkill skill)
        {
            float acc = skill != null ? skill.accuracy : 1f;
            if (acc >= 0.999f) return true;   // 완전명중 스킬은 롤 없이 통과
            return RollHit(acc, 0f, UnityEngine.Random.value);
        }

        private void UseEnemyTurn()
        {
            // 기절 상태면 적 행동 스킵.
            if (enemyStunTurns > 0)
            {
                enemyStunTurns--;
                LastEnemySkill = null;
                TryPlayEffectText("적 행동 불가!", new Color(1f, 0.85f, 0.3f));
                return;
            }

            InsectSkill enemySkill = GetPrimarySkill(enemyStats.Data);
            if (enemyCooldown > 0)
            {
                enemySkill = null;
            }

            // 「장부에 올랐다」 — 되풀이한 자리를 친다. 게이지는 도로 비운다(톱니형).
            // **피해 배율은 GetDamage 한 곳에서만** 걸린다 — ApplySkill의 분기가 여럿이라
            // 각 분기에 곱하면 하나를 빠뜨리고 그게 조용히 어긋난다.
            //
            // **쓴 턴에만 비운다.** 장부가 찼다고 무조건 소모하면, 곱할 피해가 없던 턴
            // (버프·회복·독·기절)과 **빗나간 턴**에 게이지만 비워지고 아무 일도 안 일어난다 —
            // 경고를 보고 각오한 쪽에서는 압박이 실력이 아니라 운으로 읽힌다.
            // 그래서 여기서는 **겨누기만** 하고, 실제로 배율이 걸렸는지는 `GetDamage`가
            // `ledgerSpentThisTurn`으로 알려 준다. 못 썼으면 장부를 든 채 다음 턴을 기다린다.
            //
            // 이 구조는 스킬 종류를 열거하지 않는다는 점이 중요하다 — 옛 판은
            // `SkillEffectType`을 나열해 "때리는 턴인가"를 미리 판정했고, 그래서
            // **빗나감을 못 봤다**(산·유적 거점 보스의 주력기 `storm`은 명중 0.9다).
            ledgerArmedThisTurn = LedgerPressure.IsFull(ledgerTally, ledgerThreshold);
            ledgerSpentThisTurn = false;

            LastEnemySkill = enemySkill;   // UI가 EnemyAttack 연출(속성·근접여부)에 사용
            ApplySkill(enemyStats, playerStats, enemySkill, false);

            if (ledgerSpentThisTurn)
            {
                ledgerTally = 0;
                ledgerReadCount++;
                TryPlayEffectText("장부에 올랐다!", new Color(0.95f, 0.35f, 0.3f));
            }
            ledgerArmedThisTurn = false;
            ledgerSpentThisTurn = false;
            if (enemySkill != null)
            {
                enemyCooldown = enemySkill.cooldownTurns;
            }
        }

        private void TickCooldowns()
        {
            if (playerCooldowns != null)
            {
                for (int i = 0; i < playerCooldowns.Length; i++)
                {
                    if (playerCooldowns[i] > 0)
                    {
                        playerCooldowns[i]--;
                    }
                }
            }

            if (enemyCooldown > 0)
            {
                enemyCooldown--;
            }
        }

        // 지속 감염을 전투 시작/교체 시 재적용 — 감염 곤충은 독 DoT·마비 스킵을 안고 시작.
        private void SeedPersistentState(Core.PlayerInsectData pid)
        {
            playerPoisoned = pid != null && pid.isPoisoned;
            playerParalyzed = pid != null && pid.isParalyzed;
            if (playerPoisoned) AddEffect(true, PersistentPoisonDamage, PersistentPoisonTurns, EffectKind.Dot);
            if (playerParalyzed) playerStunTurns = 1;
        }

        // 현재 활성 플레이어 곤충의 남은 HP·감염을 영구 저장(전투 종료/교체 시).
        private void PersistActivePlayer()
        {
            if (playerCollection == null || playerStats == null || playerStats.PlayerData == null) return;
            playerCollection.SetAfterBattle(playerStats.PlayerData, playerStats.CurrentHp, playerPoisoned, playerParalyzed);
        }

        /// <summary>
        /// 지속 효과 추가. 상한(GameConstants.Battle.MaxBuffStacks)에 걸려 추가하지 못하면 false.
        /// Dot는 상한 대상이 아니다 — 버프/디버프 배율만 무한 누적이 문제였다.
        /// </summary>
        private bool AddEffect(bool targetIsPlayer, float value, int duration, EffectKind kind = EffectKind.AtkBuff)
        {
            if (duration <= 0)
            {
                return false;
            }

            if (kind != EffectKind.Dot && CountStacks(targetIsPlayer, value, kind) >= GameConstants.Battle.MaxBuffStacks)
            {
                return false;
            }

            effects.Add(new ActiveEffect
            {
                targetIsPlayer = targetIsPlayer,
                value = value,
                remainingTurns = duration,
                kind = kind
            });
            RecalculateBonuses();
            return true;
        }

        // 같은 대상·같은 종류·같은 방향(부호)으로 살아 있는 효과 수. 만료된 것은 TickEffects가 이미 지웠다.
        // 부호를 따지므로 공격력 상승 3스택이 찼어도 공격력 하락은 그대로 걸린다(되돌릴 길을 막지 않는다).
        private int CountStacks(bool targetIsPlayer, float value, EffectKind kind)
        {
            bool positive = value > 0f;
            int count = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                ActiveEffect e = effects[i];
                if (e.targetIsPlayer == targetIsPlayer && e.kind == kind && (e.value > 0f) == positive)
                    count++;
            }
            return count;
        }

        private void TickEffects()
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                ActiveEffect effect = effects[i];

                // 지속 피해(Dot) — 매턴 대상에 value만큼 피해. 상성/방어 배율 없는 순수 피해.
                if (effect.kind == EffectKind.Dot)
                {
                    int dot = Mathf.Max(1, Mathf.RoundToInt(effect.value));
                    InsectBattleStats target = effect.targetIsPlayer ? playerStats : enemyStats;
                    if (target != null && target.CurrentHp > 0)
                    {
                        target.ApplyDamage(dot);
                        TryPlayHitFlash(effect.targetIsPlayer);
                    }
                }

                effect.remainingTurns--;
                if (effect.remainingTurns <= 0)
                {
                    effects.RemoveAt(i);
                }
                else
                {
                    effects[i] = effect;
                }
            }

            RecalculateBonuses();
        }

        private void RecalculateBonuses()
        {
            // 효과를 kind별로 합산 — AtkBuff(음수=디버프)→AttackBonus, DefBuff→DefenseBonus. Dot는 보너스 무관.
            float playerAtk = 0f, enemyAtk = 0f, playerDef = 0f, enemyDef = 0f;
            foreach (ActiveEffect effect in effects)
            {
                if (effect.kind == EffectKind.AtkBuff)
                {
                    if (effect.targetIsPlayer) playerAtk += effect.value; else enemyAtk += effect.value;
                }
                else if (effect.kind == EffectKind.DefBuff)
                {
                    if (effect.targetIsPlayer) playerDef += effect.value; else enemyDef += effect.value;
                }
            }

            if (playerStats != null)
            {
                float outfitAtk = outfitBonus != null ? outfitBonus.GetAtkBonus() : 0f;
                float itemAtk = itemEffects != null ? itemEffects.GetAtkBonus() : 0f;
                playerStats.AttackBonus = playerAtk + outfitAtk + itemAtk;

                // 방어 보너스(전투 효과 + 의상 + 아이템) — ApplyDamage에서 유효 방어로 소비.
                float outfitDef = outfitBonus != null ? outfitBonus.GetDefBonus() : 0f;
                float itemDef = itemEffects != null ? itemEffects.GetDefBonus() : 0f;
                playerStats.DefenseBonus = playerDef + outfitDef + itemDef;
            }

            if (enemyStats != null)
            {
                enemyStats.AttackBonus = enemyAtk;
                enemyStats.DefenseBonus = enemyDef;
            }
        }

        private void CheckEnd()
        {
            if (playerStats == null || enemyStats == null)
            {
                return;
            }
            if (battleEnded) return; // 이미 종료 — 보상/이벤트 중복 차단

            bool playerWon = enemyStats.CurrentHp <= 0 && playerStats.CurrentHp > 0;
            if (playerWon && duelMode)
            {
                // NPC 대결 승리 — 상대는 NPC 소유라 포획도, 야생 드랍도 없다.
                // 성장 재화(캔디·EXP)와 코인만 야생과 같은 계산으로 지급한다.
                InsectData duelData = enemyStats.Data;
                float duelCandyMul = (itemEffects != null ? itemEffects.GetCandyMultiplier() : 1f)
                                    * (outfitBonus != null ? outfitBonus.GetCandyMultiplier() : 1f);
                float duelExpMul = (itemEffects != null ? itemEffects.GetExpMultiplier() : 1f)
                                  * (outfitBonus != null ? outfitBonus.GetExpMultiplier() : 1f);
                lastCandyReward = Mathf.RoundToInt(InsectRewardCalculator.GetCandyReward(duelData) * duelCandyMul);
                lastExpReward = Mathf.RoundToInt(InsectRewardCalculator.GetExpReward(duelData) * duelExpMul);
                candyInventory?.AddCandy(lastCandyReward);
                playerProgress?.GainXp(lastExpReward);
                wallet?.AddCoins(BattleVictoryCoins);

                lastItemId = string.Empty;
                lastItemCount = 0;
                lastCaptureAttempted = false;
                lastCaptureSucceeded = false;
                lastCaptureChance = 0f;
                TryPlayFaint(false);
            }
            else if (playerWon && enemyEntity != null)
            {
                // 보상은 시작 시점 스냅샷(enemyStats)에서 — SetEngaged로 도주를 막았지만,
                // 라이브 enemyEntity 대신 스냅샷을 써 이중 안전(엉뚱한 종/레벨/이로치 등록 방지).
                InsectData enemyData = enemyStats.Data;
                int enemyLevel = enemyStats.Level;
                int itemCount = InsectRewardCalculator.GetItemRewardCount(enemyData);
                string itemId = enemyData.itemRewardId;
                // 같은 승리에서 지급하는 XP가 이번 포획 확률을 바꾸지 않도록 보상 지급 전에 고정한다.
                int capturePlayerLevel = playerProgress != null
                    ? playerProgress.Level
                    : playerStats.Level;

                // EXP/캔디 부스터(아이템·아웃핏) 배율 — 포획 경로(CaptureController)와 동일 항목만 적용.
                float candyMultiplier = (itemEffects != null ? itemEffects.GetCandyMultiplier() : 1f)
                                       * (outfitBonus != null ? outfitBonus.GetCandyMultiplier() : 1f);
                float expMultiplier = (itemEffects != null ? itemEffects.GetExpMultiplier() : 1f)
                                     * (outfitBonus != null ? outfitBonus.GetExpMultiplier() : 1f);
                int candy = Mathf.RoundToInt(InsectRewardCalculator.GetCandyReward(enemyData) * candyMultiplier);
                int exp = Mathf.RoundToInt(InsectRewardCalculator.GetExpReward(enemyData) * expMultiplier);

                candyInventory?.AddCandy(candy);
                playerProgress?.GainXp(exp);
                // 승리 소량 코인 — 상점 코인결제/베이직 의상 지속 수급(반복 faucet). AddCoins가 세이브 트리거.
                wallet?.AddCoins(BattleVictoryCoins);
                if (!string.IsNullOrEmpty(itemId) && itemCount > 0)
                {
                    itemInventory?.AddItem(itemId, itemCount);
                }

                float activeItemBonus = itemEffects != null
                    ? itemEffects.GetCaptureChanceBonus()
                    : 0f;
                float equippedOutfitBonus = outfitBonus != null
                    ? outfitBonus.GetCaptureChanceBonus()
                    : 0f;
                lastCaptureAttempted = true;
                lastCaptureChance = BattleCaptureChanceCalculator.Calculate(
                    enemyData.rarity,
                    enemyData.captureDifficulty,
                    capturePlayerLevel,
                    enemyLevel,
                    activeItemBonus,
                    equippedOutfitBonus);
                bool captureRollSucceeded = BattleCaptureChanceCalculator.IsSuccessful(
                    lastCaptureChance,
                    UnityEngine.Random.value);

                if (dexController != null)
                {
                    dexController.RegisterEncounter(enemyData.insectId);
                }

                if (captureRollSucceeded)
                {
                    if (playerCollection == null)
                    {
                        Debug.LogError("[Battle] playerCollection null — 전투 포획 저장 실패: " + enemyData.insectId);
                    }
                    else
                    {
                        Core.PlayerInsectData captured = playerCollection.AddCapturedInsect(
                            enemyData.insectId,
                            enemyLevel,
                            enemyShinyAtStart);
                        if (captured == null)
                        {
                            Debug.LogError("[Battle] AddCapturedInsect가 null을 반환해 전투 포획에 실패했습니다: "
                                           + enemyData.insectId);
                        }
                        else
                        {
                            lastCaptureSucceeded = true;
                            dexController?.RegisterCapture(enemyData.insectId);
                            // 전투는 CaptureController/CaptureResolved를 우회하므로 성공한 실제 포획만 직접 알린다.
                            TutorialQuestManager.Instance?.NotifyCapture(enemyData.rarity);
                        }
                    }
                }

                lastCandyReward = candy;
                lastExpReward = exp;
                lastItemId = itemId;
                lastItemCount = itemCount;

                TryPlayFaint(false);
                enemyEntity.Despawn();
            }

            if (enemyStats.CurrentHp <= 0)
            {
                PersistActivePlayer();   // 승리 — 활성 곤충의 남은 HP·감염 영구 저장(전체치료 없음)
                battleEnded = true;
                lastPlayerWon = playerWon;
                BattleEnded?.Invoke(playerWon);
                if (duelMode) DuelEnded?.Invoke(playerWon);
            }
            else if (playerStats.CurrentHp <= 0)
            {
                TryPlayFaint(true);
                TryPlayEffectText("쓰러졌다!", new Color(0.9f, 0.2f, 0.2f));
                // 죽은 곤충의 0 HP를 즉시 영구 저장(멱등) — 전멸 패배(교체 팀원 없음)에서 스왑·!fainted 경로가 모두
                // 스킵돼 마지막 곤충이 무료 부활하던 누락을 차단. 교체 시 SwapPlayerInsect가 다시 0으로 persist(무해).
                PersistActivePlayer();
                // 핸들러 예외 격리 — 한 구독자 예외가 BattleEnded fallback을 차단하지 않게
                bool fainted = false;
                try
                {
                    if (PlayerFainted != null)
                    {
                        PlayerFainted.Invoke();
                        fainted = true;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[InsectBattle] PlayerFainted 핸들러 예외: {e.Message}");
                }
                // fainted=true: UI(SwapSelect)가 처리 — 팀원 교체로 배틀을 이어갈 수 있으므로
                //   battleEnded/Despawn/BattleEnded를 보류해야 한다. (옛: battleEnded를 무조건 set해
                //   교체 후 새 곤충이 액션 가드에 막혀 배틀이 멈추는 무한정지 버그.)
                // !fainted: 교체 핸들러 없음 → 컨트롤러가 패배로 종료 + 적 Despawn(필드 잔존 방지).
                if (!fainted)
                {
                    // (기절 0 HP는 위에서 이미 persist — 여기선 종료 처리만)
                    battleEnded = true;
                    if (enemyEntity != null) enemyEntity.Despawn();
                    BattleEnded?.Invoke(false);
                    if (duelMode) DuelEnded?.Invoke(false);
                }
            }
        }

        /// <summary>
        /// 교체할 팀원이 없어 기절이 곧 패배인 경우, UI가 이걸 불러 <b>패배를 확정</b>한다.
        ///
        /// 위 <c>PlayerFainted</c> 분기는 <b>구독자가 있기만 하면</b> <c>fainted = true</c>로 보고
        /// 종료 처리를 UI에 넘긴다("교체로 이어갈 수 있으니 보류"). 그런데 유일한 구독자인
        /// <c>BattleScreenUI</c>는 벤치가 비면 교체창 대신 결과 화면으로 가면서 <b>컨트롤러에
        /// 되돌려 알리지 않았다</b> — 그래서 전멸 패배에서는 <c>BattleEnded</c>도 <c>DuelEnded</c>도
        /// 영영 발화하지 않았다.
        ///
        /// NPC 대결이 그 이벤트로 보상·쿨다운을 걸기 때문에 결과가 컸다: 지고 나면 90초/120초
        /// 재도전 쿨다운이 통째로 우회되고, 무엇보다 <c>NpcDuelController.activeBossId</c>가 남아
        /// <b>다음 아이 대결의 승리가 간부 격파로 오기록</b>됐다(PlayerPrefs 영구 저장 + 클라우드 동기라
        /// 되돌릴 수 없다 — 관장을 싸우지도 않고 격파 처리하고 보상까지 받는다).
        ///
        /// 도주 경로는 같은 누락을 이미 한 번 고쳤는데(<c>DuelEnded</c> 추가) 정작 전멸 패배 경로가
        /// <c>fainted</c> 가드에 가려 남아 있었다.
        ///
        /// <b>멱등하다</b> — 이미 종료됐으면 아무것도 하지 않으므로 승리·도주로 온 호출은 무시된다.
        /// </summary>
        public void ConcludeDefeatWithoutSwap()
        {
            if (battleEnded) return;

            battleEnded = true;
            lastPlayerWon = false;
            if (enemyEntity != null) enemyEntity.Despawn();
            BattleEnded?.Invoke(false);
            if (duelMode) DuelEnded?.Invoke(false);
        }

        // ── 액션 헬퍼 (BattleArenaController 시각 코루틴 호출 wrapper) ──

        private void TryPlayHitFlash(bool isPlayerSide)
        {
            if (Arena == null) return;
            GameObject model = isPlayerSide ? Arena.PlayerModel : Arena.EnemyModel;
            if (model != null && model.activeInHierarchy)
            {
                StartCoroutine(Arena.PlayHitFlashCoroutine(model));
            }
        }

        private void TryPlayFaint(bool isPlayerSide)
        {
            if (Arena == null) return;
            GameObject model = isPlayerSide ? Arena.PlayerModel : Arena.EnemyModel;
            if (model != null && model.activeInHierarchy)
            {
                StartCoroutine(Arena.PlayFaintCoroutine(model));
            }
        }

        private void TryPlayEffectText(string text, Color color)
        {
            if (Arena == null || string.IsNullOrEmpty(text)) return;
            Arena.PlayEffectText(text, color);
        }

        public void AutoWire(PlayerInsectCollection collection, PlayerCandyInventory candies, PlayerProgressController progress, PlayerItemInventory items)
        {
            if (playerCollection == null)
            {
                playerCollection = collection;
            }

            if (candyInventory == null)
            {
                candyInventory = candies;
            }

            if (playerProgress == null)
            {
                playerProgress = progress;
            }

            if (itemInventory == null)
            {
                itemInventory = items;
            }
        }

        public void AutoWire(Dex.DexController dex)
        {
            if (dexController == null) dexController = dex;
        }

        public void AutoWire(PlayerCurrencyWallet walletRef)
        {
            if (wallet == null) wallet = walletRef;
        }

        public void AutoWire(BattleArenaController a)
        {
            if (arena == null) arena = a;
        }

        private OutfitBonusProvider outfitBonus;

        public void AutoWire(OutfitBonusProvider bonus)
        {
            if (outfitBonus == null) outfitBonus = bonus;
        }

        private ItemEffectManager itemEffects;

        public void AutoWire(ItemEffectManager effects)
        {
            if (itemEffects == null) itemEffects = effects;
        }

        public void SwapPlayerInsect(InsectData newInsect, int newLevel, InsectSkill[] equippedSkills = null, Core.PlayerInsectData playerPid = null)
        {
            if (newInsect == null || enemyStats == null) return;

            PersistActivePlayer();   // 교체 전 이전 곤충의 남은 HP·감염 저장(기절이면 0 그대로)

            playerStats = new InsectBattleStats(newInsect, newLevel, playerPid);
            playerOverrideSkills = ResolvePlayerSkills(newInsect, equippedSkills, playerPid);
            int skillCount = playerOverrideSkills != null ? playerOverrideSkills.Length : (newInsect.skills != null ? newInsect.skills.Length : 0);
            playerCooldowns = new int[skillCount];
            effects.Clear();
            playerStunTurns = 0;
            SeedPersistentState(playerPid);   // 새 곤충의 지속 독/마비 재적용
            RecalculateBonuses();

            // Faint 후 비활성/페이드된 PlayerModel을 새 곤충으로 재생성
            if (Arena != null) Arena.RebuildPlayerInsect(newInsect, newLevel);

            BattleUpdated?.Invoke(playerStats, enemyStats);
        }

        private InsectSkill[] ResolvePlayerSkills(InsectData insect, InsectSkill[] equippedSkills, Core.PlayerInsectData playerPid)
        {
            if (equippedSkills != null && equippedSkills.Length > 0)
            {
                return equippedSkills;
            }

            if (playerPid != null && playerCollection != null)
            {
                InsectSkill[] resolved = playerCollection.GetEquippedSkills(playerPid);
                if (resolved != null && resolved.Length > 0)
                {
                    return resolved;
                }
            }

            return insect != null ? insect.skills : null;
        }

        public bool IsBattleInProgress()
        {
            return playerStats != null && enemyStats != null;
        }

        public InsectEntity GetEnemyEntity() => enemyEntity;

        public int[] GetPlayerCooldowns()
        {
            if (playerCooldowns == null)
            {
                return Array.Empty<int>();
            }

            int[] copy = new int[playerCooldowns.Length];
            Array.Copy(playerCooldowns, copy, playerCooldowns.Length);
            return copy;
        }

        public EffectSnapshot[] GetActiveEffects()
        {
            EffectSnapshot[] snapshots = new EffectSnapshot[effects.Count];
            for (int i = 0; i < effects.Count; i++)
            {
                ActiveEffect effect = effects[i];
                snapshots[i] = new EffectSnapshot
                {
                    targetIsPlayer = effect.targetIsPlayer,
                    value = effect.value,
                    remainingTurns = effect.remainingTurns,
                    kind = effect.kind
                };
            }

            return snapshots;
        }

        public int GetLastCandyReward()
        {
            return lastCandyReward;
        }

        public bool GetLastPlayerWon()
        {
            return lastPlayerWon;
        }

        public int GetLastExpReward()
        {
            return lastExpReward;
        }

        public bool GetLastCaptureAttempted()
        {
            return lastCaptureAttempted;
        }

        public bool GetLastCaptureSucceeded()
        {
            return lastCaptureSucceeded;
        }

        public float GetLastCaptureChance()
        {
            return lastCaptureChance;
        }

        public string GetLastItemId()
        {
            return lastItemId;
        }

        public int GetLastItemCount()
        {
            return lastItemCount;
        }

        // 효과 종류 — AtkBuff(공격 배율, 음수=디버프), DefBuff(방어 배율), Dot(턴당 피해).
        // 스턴은 틱 간섭 방지 위해 효과 리스트가 아니라 별도 카운터(playerStunTurns/enemyStunTurns)로 관리.
        public enum EffectKind { AtkBuff, DefBuff, Dot }

        public struct EffectSnapshot
        {
            public bool targetIsPlayer;
            public float value;
            public int remainingTurns;
            public EffectKind kind;
        }

        private struct ActiveEffect
        {
            public bool targetIsPlayer;
            public float value;
            public int remainingTurns;
            public EffectKind kind;
        }
    }
}
