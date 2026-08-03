using System;
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Battle
{
    public class RaidBattleController : MonoBehaviour
    {
        [SerializeField] private PlayerInsectCollection playerCollection;
        [SerializeField] private PlayerCandyInventory candyInventory;
        [SerializeField] private PlayerProgressController playerProgress;
        [SerializeField] private Dex.DexController dexController;
        [SerializeField] private TrainingManager trainingManager;
        [SerializeField] private BattleArenaController arena;

        public event Action RaidUpdated;
        public event Action<bool> RaidEnded;
        public event Action<RaidRoundResult> RaidTeamRushResolved;
        public event Action<RaidRoundResult> RaidBossResponseResolved;
        public event Action<RaidRoundResult> RaidRoundCompleted;

        // 보스가 직전 턴에 쓴 시그니처 스킬(null=기본/AOE) — UI가 BossAttack 페이즈 연출 속성에 사용.
        public InsectSkill LastBossSkill { get; private set; }

        public InsectBattleStats BossStats { get; private set; }
        public InsectBattleStats[] TeamStats { get; private set; }
        public InsectData[] TeamData { get; private set; }
        public PlayerInsectData[] TeamPids { get; private set; }
        public InsectSkill[][] TeamSkills { get; private set; }
        public int[][] TeamCooldowns { get; private set; }
        public InsectEntity BossEntity { get; private set; }

        public int TurnNumber { get; private set; }
        public int ActiveSlot { get; private set; }
        public bool IsActive { get; private set; }
        public string LastActionText { get; private set; }
        public int LastDamageToBoss { get; private set; }
        public int LastDamageToTeam { get; private set; }
        public int LastHitSlot { get; private set; }
        public bool BossUsedAoe { get; private set; }
        public bool PlayerWon { get; private set; }
        public int RewardCandy { get; private set; }
        public int RewardExp { get; private set; }

        public float UniteGauge { get; private set; }
        public const float UniteGaugeMax = GameConstants.Battle.UniteGaugeMax;
        public bool CanUniteAttack => CanSubmitTeamCommand
            && UniteGauge >= UniteGaugeMax && AliveCount() >= 2;
        public bool LastWasUnite { get; private set; }
        public int[] UniteSlotDamages { get; private set; }
        public RaidBossIntent NextBossIntent { get; private set; }
        public RaidRoundResult CurrentRoundResult { get; private set; }
        public RaidRoundStage RoundStage => roundStage;
        public bool CanSubmitTeamCommand => IsActive && roundStage == RaidRoundStage.Ready;
        public bool IsAwaitingBossResponse => IsActive
            && roundStage == RaidRoundStage.TeamResolved
            && CurrentRoundResult != null
            && CurrentRoundResult.EndState == RaidRoundEndState.Ongoing;
        public bool IsAwaitingPresentationCompletion => IsActive
            && CurrentRoundResult != null
            && ((roundStage == RaidRoundStage.TeamResolved
                    && CurrentRoundResult.EndState == RaidRoundEndState.Victory)
                || roundStage == RaidRoundStage.BossResolved);

        // 0이면 다음 의도가 AOE. AOE 후 2로 설정되어 단일 공격 2회를 거친 뒤 다시 AOE.
        private int bossCooldown;
        private bool bossStunned;   // 팀 Stun 스킬로 보스 다음 턴 스킵(P4)
        private bool bossShinyAtStart; // 시작 시점 스냅샷 — 도주/풀 재사용된 라이브 보스 참조로 이로치 오등록 방지
        private RaidRoundStage roundStage = RaidRoundStage.Completed;
        private IRaidRandomSource randomSource = new UnityRaidRandomSource();
        private bool raidEndedRaised;

        public void StartRaid(InsectEntity bossEntity,
            InsectData[] teamInsects, int[] teamLevels,
            PlayerInsectData[] teamPids, InsectSkill[][] teamSkills)
        {
            if (bossEntity == null || bossEntity.Data == null
                || teamInsects == null || teamLevels == null
                || teamInsects.Length == 0 || teamLevels.Length < teamInsects.Length)
            {
                return;
            }

            BossEntity = bossEntity;
            bossShinyAtStart = bossEntity.IsShiny;
            // 레이드 동안 보스(야생 엔티티)가 도주→Despawn→풀 재사용되어 종료 시 무관 곤충이
            // 등록/Despawn되는 보상 무결성 손상 차단. (1v1 StartBattle과 동일)
            bossEntity.SetEngaged(true);
            InsectData bd = bossEntity.Data;

            InsectBattleStats rawBoss = new InsectBattleStats(bd, bossEntity.Level);
            BossStats = new RaidBossStats(bd, bossEntity.Level, rawBoss.MaxHp * 5, rawBoss.Attack * 3 / 2, rawBoss.Defense * 13 / 10);

            int count = teamInsects.Length;
            TeamStats = new InsectBattleStats[count];
            TeamData = teamInsects;
            TeamPids = teamPids;
            TeamSkills = teamSkills;
            TeamCooldowns = new int[count][];

            for (int i = 0; i < count; i++)
            {
                PlayerInsectData pid = teamPids != null && i < teamPids.Length
                    ? teamPids[i]
                    : null;
                TeamStats[i] = new InsectBattleStats(teamInsects[i], teamLevels[i], pid);
                // 의상/아이템 강화 — 레이드는 팀 보너스를 초기화하는 코드가 없어 공격/방어가 미반영이었음. 여기서 세팅.
                TeamStats[i].AttackBonus = (outfitBonus != null ? outfitBonus.GetAtkBonus() : 0f)
                                         + (itemEffects != null ? itemEffects.GetAtkBonus() : 0f);
                TeamStats[i].DefenseBonus = (outfitBonus != null ? outfitBonus.GetDefBonus() : 0f)
                                          + (itemEffects != null ? itemEffects.GetDefBonus() : 0f);
                int sc = teamSkills != null && i < teamSkills.Length && teamSkills[i] != null
                    ? teamSkills[i].Length
                    : 0;
                TeamCooldowns[i] = new int[sc];
            }

            TurnNumber = 0;
            ActiveSlot = FindFirstAlive();
            IsActive = true;
            PlayerWon = false;
            LastActionText = "";
            LastDamageToBoss = 0;
            LastDamageToTeam = 0;
            LastHitSlot = -1;
            BossUsedAoe = false;
            LastWasUnite = false;
            UniteGauge = 0f;
            UniteSlotDamages = null;
            bossCooldown = 0;
            bossStunned = false;
            LastBossSkill = null;
            RewardCandy = 0;
            RewardExp = 0;
            CurrentRoundResult = null;
            roundStage = RaidRoundStage.Ready;
            raidEndedRaised = false;

            PrepareNextBossIntent();
            RaidUpdated?.Invoke();
        }

        public void SelectSlot(int slot)
        {
            if (!CanSubmitTeamCommand) return;
            if (slot < 0 || slot >= TeamStats.Length) return;
            if (TeamStats[slot].CurrentHp <= 0) return;
            ActiveSlot = slot;
        }

        public bool CanUseSkill(int skillIndex)
        {
            if (!CanSubmitTeamCommand || ActiveSlot < 0) return false;
            // 방어: 활성 슬롯이 기절/무효면 행동 불가(죽은 멤버 공격 방지). 정상 흐름은 보스턴 후 자동 전환됨.
            if (ActiveSlot >= TeamStats.Length || TeamStats[ActiveSlot] == null
                || TeamStats[ActiveSlot].CurrentHp <= 0) return false;
            var skills = TeamSkills != null && ActiveSlot < TeamSkills.Length ? TeamSkills[ActiveSlot] : null;
            if (skills == null || skillIndex < 0 || skillIndex >= skills.Length) return false;
            if (skills[skillIndex] == null) return false;
            var cd = TeamCooldowns[ActiveSlot];
            if (skillIndex < cd.Length && cd[skillIndex] > 0) return false;
            return true;
        }

        public void UseSkill(int skillIndex)
        {
            ResolveTeamCommand(skillIndex);
        }

        public RaidRoundResult ResolveTeamCommand(int skillIndex)
        {
            if (!CanUseSkill(skillIndex)) return null;

            int leaderSlot = ActiveSlot;
            InsectBattleStats leader = TeamStats[leaderSlot];
            InsectSkill skill = TeamSkills[leaderSlot][skillIndex];
            RaidRoundResult result = new RaidRoundResult(
                TurnNumber + 1,
                leaderSlot,
                skillIndex,
                false,
                NextBossIntent,
                TeamStats.Length);

            RaidActionResult leaderAction = RaidRoundResolver.ResolveLeaderSkill(
                leaderSlot, skillIndex, leader, BossStats, skill, randomSource);
            result.AddTeamAction(leaderAction);
            if (skill.effectType == SkillEffectType.Stun)
                bossStunned = true;

            // 선택한 리더 외 생존 팀원은 같은 러시에 기본 지원 공격으로 참여한다.
            // 슬롯 순서로 계산하여 RNG와 오버킬 배분도 항상 결정론적으로 유지한다.
            for (int i = 0; i < TeamStats.Length; i++)
            {
                if (i == leaderSlot || TeamStats[i] == null
                    || TeamStats[i].CurrentHp <= 0)
                {
                    continue;
                }

                result.AddTeamAction(
                    RaidRoundResolver.ResolveSupportAssist(i, TeamStats[i], BossStats));
            }

            TeamCooldowns[leaderSlot][skillIndex] = skill.cooldownTurns;
            if (result.TotalDamageToBoss > 0)
            {
                UniteGauge = Mathf.Min(
                    UniteGauge + 12f + result.TotalDamageToBoss * 0.15f,
                    UniteGaugeMax);
            }

            LastDamageToBoss = result.TotalDamageToBoss;
            LastDamageToTeam = 0;
            LastHitSlot = -1;
            BossUsedAoe = false;
            LastBossSkill = null;
            LastWasUnite = false;
            UniteSlotDamages = null;
            LastActionText = BuildTeamRushText(result);

            if (BossStats.CurrentHp <= 0)
                result.EndState = RaidRoundEndState.Victory;

            result.Stage = RaidRoundStage.TeamResolved;
            CurrentRoundResult = result;
            roundStage = RaidRoundStage.TeamResolved;
            RaidTeamRushResolved?.Invoke(result);
            RaidUpdated?.Invoke();
            return result;
        }

        public RaidRoundResult ResolveBossResponse()
        {
            if (!IsAwaitingBossResponse) return null;

            RaidRoundResult result = CurrentRoundResult;
            RaidBossIntent intent = result.BossIntent;

            if (bossStunned)
            {
                bossStunned = false;
                result.BossResponseResolved = true;
                result.BossResponseSkipped = true;
                result.BossAction = new RaidActionResult
                {
                    Kind = RaidActionKind.BossSkipped,
                    SourceSlot = -1,
                    TargetSlot = intent != null ? intent.TargetSlot : -1,
                    Skill = intent != null ? intent.Skill : null,
                    Element = intent != null ? intent.Element : InsectElement.None,
                    EffectType = intent != null ? intent.EffectType : SkillEffectType.Damage,
                    DisplayName = "기절"
                };
                LastBossSkill = null;
                LastDamageToTeam = 0;
                LastHitSlot = -1;
                BossUsedAoe = false;
                LastActionText += "\n보스가 기절해 움직이지 못한다!";
            }
            else
            {
                RaidActionResult bossAction = RaidRoundResolver.ResolveBossIntent(
                    intent, BossStats, TeamStats, result.BossDamageBySlot);
                result.BossAction = bossAction;
                result.BossResponseResolved = true;
                for (int i = 0; i < result.BossDamageBySlot.Length; i++)
                    result.TotalDamageToTeam += result.BossDamageBySlot[i];

                bool area = intent != null && intent.IsArea;
                if (area)
                    bossCooldown = 2;
                else if (bossCooldown > 0)
                    bossCooldown--;

                LastBossSkill = intent != null ? intent.Skill : null;
                LastDamageToTeam = result.TotalDamageToTeam;
                LastHitSlot = area ? -1 : (intent != null ? intent.TargetSlot : -1);
                BossUsedAoe = area;
                LastActionText += BuildBossResponseText(intent);
                UniteGauge = Mathf.Min(
                    UniteGauge + (area ? 18f : 10f),
                    UniteGaugeMax);
            }

            if (FindFirstAlive() < 0)
                result.EndState = RaidRoundEndState.Defeat;

            if (ActiveSlot >= 0 && ActiveSlot < TeamStats.Length
                && TeamStats[ActiveSlot] != null && TeamStats[ActiveSlot].CurrentHp <= 0)
            {
                int next = FindFirstAlive();
                if (next >= 0) ActiveSlot = next;
            }

            result.Stage = RaidRoundStage.BossResolved;
            roundStage = RaidRoundStage.BossResolved;
            RaidBossResponseResolved?.Invoke(result);
            RaidUpdated?.Invoke();
            return result;
        }

        public bool CompleteRoundPresentation()
        {
            if (!IsAwaitingPresentationCompletion) return false;

            RaidRoundResult completed = CurrentRoundResult;
            TickCooldowns();
            TurnNumber++;
            completed.RoundNumber = TurnNumber;
            completed.Stage = RaidRoundStage.Completed;
            roundStage = RaidRoundStage.Completed;
            RaidRoundCompleted?.Invoke(completed);

            if (completed.EndState != RaidRoundEndState.Ongoing)
            {
                CompleteRaid(completed.EndState == RaidRoundEndState.Victory);
                RaidUpdated?.Invoke();
                return true;
            }

            roundStage = RaidRoundStage.Ready;
            PrepareNextBossIntent();
            RaidUpdated?.Invoke();
            return true;
        }

        public void SetRandomSource(IRaidRandomSource source)
        {
            randomSource = source ?? new UnityRaidRandomSource();
        }

        private void PrepareNextBossIntent()
        {
            if (!IsActive || BossStats == null || TeamStats == null)
            {
                NextBossIntent = null;
                return;
            }

            InsectSkill signature = GetUnlockedBossSignature(
                BossStats.Data, BossStats.Level);
            NextBossIntent = RaidRoundResolver.CreateBossIntent(
                TurnNumber + 1,
                BossStats,
                TeamStats,
                bossCooldown,
                signature,
                randomSource);
        }

        private static string BuildTeamRushText(RaidRoundResult result)
        {
            if (result == null || result.TeamActions.Count == 0)
                return string.Empty;

            RaidActionResult leader = result.TeamActions[0];
            string leaderText;
            if (leader.Missed)
                leaderText = $"{leader.DisplayName}! 빗나갔다!";
            else if (leader.Capped)
                leaderText = $"{leader.DisplayName}! 이미 최대치다!";   // 스택 상한 — 턴은 소비됐다
            else
                leaderText = $"{leader.DisplayName}!";
            int supports = Mathf.Max(0, result.TeamActions.Count - 1);
            return supports > 0
                ? $"{leaderText}\n팀 러시! 지원 {supports}마리"
                : leaderText;
        }

        private string BuildBossResponseText(RaidBossIntent intent)
        {
            if (intent == null || BossStats == null || BossStats.Data == null)
                return string.Empty;
            return intent.IsArea
                ? $"\n{BossStats.Data.displayName}의 전체 공격!"
                : $"\n{BossStats.Data.displayName}의 {intent.DisplayName}!";
        }

        private static InsectSkill GetUnlockedBossSignature(InsectData data, int level)
        {
            if (data == null || data.learnset == null) return null;
            foreach (InsectLearnableSkill learnable in data.learnset)
            {
                if (learnable != null
                    && learnable.skill != null
                    && learnable.skill.isSignatureSkill
                    && learnable.learnLevel <= level)
                    return learnable.skill;
            }
            return null;
        }

        private void TickCooldowns()
        {
            for (int s = 0; s < TeamCooldowns.Length; s++)
            {
                for (int i = 0; i < TeamCooldowns[s].Length; i++)
                    if (TeamCooldowns[s][i] > 0) TeamCooldowns[s][i]--;
            }
        }

        // 레이드 종료 시 팀 각 곤충의 남은 HP를 영구 저장(전투 후 전체치료 없음). 감염 플래그는 기존값 보존
        // (보스는 팀에 독/마비를 걸지 않으므로 레이드는 HP만 변동).
        private void PersistTeamHp()
        {
            if (playerCollection == null || TeamStats == null || TeamPids == null) return;
            for (int i = 0; i < TeamStats.Length && i < TeamPids.Length; i++)
            {
                if (TeamStats[i] == null || TeamPids[i] == null) continue;
                playerCollection.SetAfterBattle(TeamPids[i], TeamStats[i].CurrentHp, TeamPids[i].isPoisoned, TeamPids[i].isParalyzed);
            }
        }

        private void CompleteRaid(bool playerWon)
        {
            if (!IsActive || raidEndedRaised) return;

            raidEndedRaised = true;
            PlayerWon = playerWon;
            IsActive = false;
            roundStage = RaidRoundStage.Completed;
            NextBossIntent = null;
            PersistTeamHp();

            if (playerWon)
            {
                OnRaidVictory();
            }
            else
            {
                // 레이드 패배 시에도 BossEntity Despawn — 옛은 패배 시 보스가 필드에 잔존,
                // 다음 진입 시 같은 보스 중첩 발동 가능 (사용자 보고: "전투 끝나면 사라져야").
                if (BossEntity != null) BossEntity.Despawn();
            }

            RaidEnded?.Invoke(playerWon);
        }

        private void OnRaidVictory()
        {
            InsectData bd = BossStats.Data;
            int candyBase = InsectRewardCalculator.GetCandyReward(bd);
            int expBase = InsectRewardCalculator.GetExpReward(bd);
            // EXP/캔디 부스터(아이템·아웃핏) 배율 — 포획 경로(CaptureController)와 동일 항목만 적용.
            // 레이드 ×3 보너스 위에 부스터를 곱하고, 표기/지급이 같도록 곱한 최종값을 저장.
            float candyMultiplier = (itemEffects != null ? itemEffects.GetCandyMultiplier() : 1f)
                                   * (outfitBonus != null ? outfitBonus.GetCandyMultiplier() : 1f);
            float expMultiplier = (itemEffects != null ? itemEffects.GetExpMultiplier() : 1f)
                                 * (outfitBonus != null ? outfitBonus.GetExpMultiplier() : 1f);
            RewardCandy = Mathf.RoundToInt(candyBase * 3 * candyMultiplier);
            RewardExp = Mathf.RoundToInt(expBase * 3 * expMultiplier);

            if (candyInventory != null) candyInventory.AddCandy(RewardCandy);
            if (playerProgress != null) playerProgress.GainXp(RewardExp);
            if (dexController != null)
            {
                dexController.RegisterEncounter(bd.insectId);
                dexController.RegisterCapture(bd.insectId);
            }
            if (BossEntity != null)
            {
                // playerCollection 가드 분리 — null이어도 보스 Despawn은 항상 보장
                if (playerCollection != null)
                {
                    // 레벨/이로치는 시작 스냅샷(BossStats.Level/bossShinyAtStart) — 라이브 참조 회피
                    playerCollection.AddCapturedInsect(bd.insectId, BossStats.Level, bossShinyAtStart);
                }
                BossEntity.Despawn();
            }
        }

        public void UseUniteAttack()
        {
            ResolveUniteCommand();
        }

        public RaidRoundResult ResolveUniteCommand()
        {
            if (!CanUniteAttack) return null;

            RaidRoundResult result = new RaidRoundResult(
                TurnNumber + 1,
                ActiveSlot,
                -1,
                true,
                NextBossIntent,
                TeamStats.Length);
            List<string> names = new List<string>();
            for (int i = 0; i < TeamStats.Length; i++)
            {
                InsectBattleStats attacker = TeamStats[i];
                if (attacker == null || attacker.CurrentHp <= 0) continue;
                result.AddTeamAction(
                    RaidRoundResolver.ResolveUniteContribution(i, attacker, BossStats));
                names.Add(attacker.Data != null ? attacker.Data.displayName : $"팀원 {i + 1}");
            }

            UniteGauge = 0f;
            LastWasUnite = true;
            LastDamageToBoss = result.TotalDamageToBoss;
            UniteSlotDamages = result.UniteSlotDamages;
            LastActionText = $"★ 합체공격! {string.Join(" + ", names)} ★";
            BossUsedAoe = false;
            LastBossSkill = null;
            LastDamageToTeam = 0;
            LastHitSlot = -1;

            if (BossStats.CurrentHp <= 0)
                result.EndState = RaidRoundEndState.Victory;

            result.Stage = RaidRoundStage.TeamResolved;
            CurrentRoundResult = result;
            roundStage = RaidRoundStage.TeamResolved;
            RaidTeamRushResolved?.Invoke(result);
            RaidUpdated?.Invoke();
            return result;
        }

        public int AliveCount()
        {
            int c = 0;
            if (TeamStats != null)
                foreach (var s in TeamStats)
                    if (s != null && s.CurrentHp > 0) c++;
            return c;
        }

        private int FindFirstAlive()
        {
            if (TeamStats == null) return -1;
            for (int i = 0; i < TeamStats.Length; i++)
                if (TeamStats[i] != null && TeamStats[i].CurrentHp > 0) return i;
            return -1;
        }

        public void AutoWire(PlayerInsectCollection col, PlayerCandyInventory candy,
            PlayerProgressController prog, Dex.DexController dex, TrainingManager tm)
        {
            if (playerCollection == null) playerCollection = col;
            if (candyInventory == null) candyInventory = candy;
            if (playerProgress == null) playerProgress = prog;
            if (dexController == null) dexController = dex;
            if (trainingManager == null) trainingManager = tm;
        }

        public void AutoWire(BattleArenaController a)
        {
            if (arena == null) arena = a;
        }

        private ItemEffectManager itemEffects;

        public void AutoWire(ItemEffectManager effects)
        {
            if (itemEffects == null) itemEffects = effects;
        }

        private OutfitBonusProvider outfitBonus;

        public void AutoWire(OutfitBonusProvider bonus)
        {
            if (outfitBonus == null) outfitBonus = bonus;
        }

        // ── 액션 헬퍼 (BattleArenaController 시각 코루틴 wrapper) ──

        private BattleArenaController Arena => arena;

        private void TryPlayBossHitFlash()
        {
            if (Arena == null) return;
            GameObject m = Arena.BossModel;
            if (m != null && m.activeInHierarchy)
                StartCoroutine(Arena.PlayHitFlashCoroutine(m));
        }

        private void TryPlayTeamHitFlash(int index)
        {
            if (Arena == null) return;
            GameObject m = Arena.GetTeamModel(index);
            if (m != null && m.activeInHierarchy)
                StartCoroutine(Arena.PlayHitFlashCoroutine(m));
        }

        private void TryPlayBossFaint()
        {
            if (Arena == null) return;
            GameObject m = Arena.BossModel;
            if (m != null && m.activeInHierarchy)
                StartCoroutine(Arena.PlayFaintCoroutine(m));
        }

        private void TryPlayTeamFaint(int index)
        {
            if (Arena == null) return;
            GameObject m = Arena.GetTeamModel(index);
            if (m != null && m.activeInHierarchy)
                StartCoroutine(Arena.PlayFaintCoroutine(m));
        }

        private void TryPlayEffectText(string text, Color color)
        {
            if (Arena == null || string.IsNullOrEmpty(text)) return;
            Arena.PlayEffectText(text, color);
        }

    }

    public class RaidBossStats : InsectBattleStats
    {
        public RaidBossStats(InsectData data, int level, int hp, int atk, int def)
            : base(data, level)
        {
            MaxHp = hp;
            Attack = atk;
            Defense = def;
            ResetHp();
        }
    }
}
