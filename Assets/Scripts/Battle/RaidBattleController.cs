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
        /// <summary>
        /// 팀원 <b>한 마리</b>가 행동을 마쳤다. 라운드마다 생존 수만큼 발화한다 —
        /// UI가 이 곤충 하나의 공격 연출을 돌리는 신호다.
        /// </summary>
        public event Action<RaidActionResult> RaidMemberActionResolved;
        /// <summary>팀 <b>전원</b>이 행동을 마쳐 보스 차례로 넘어간다. 라운드당 한 번.</summary>
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
        /// <summary>
        /// <b>지금 행동할 차례인 팀 슬롯.</b> 라운드가 시작되면 앞 슬롯부터 하나씩 옮겨가고,
        /// 생존 팀원이 모두 행동하면 -1이 된다(그때 보스 차례로 넘어간다).
        /// 예전엔 "플레이어가 고른 리더"였고 나머지 4마리는 같은 순간에 자동으로 딸려 나갔다.
        /// </summary>
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

        /// <summary>
        /// 비-리더 팀원 AI의 성향. 플레이어가 1탭으로 바꾸고 바꿀 때까지 유지된다 —
        /// 5슬롯을 매 라운드 직접 지정하면 세로 모바일에서 라운드당 10탭이 되기 때문이다.
        /// </summary>
        public RaidTeamStance TeamStance { get; private set; } = RaidTeamStance.Assault;

        public void SetStance(RaidTeamStance stance)
        {
            if (TeamStance == stance) return;
            TeamStance = stance;
            RaidUpdated?.Invoke();
        }

        public float UniteGauge { get; private set; }
        public const float UniteGaugeMax = GameConstants.Battle.UniteGaugeMax;
        /// <summary>
        /// 합체공격을 쓸 수 있는가. 조건이 <see cref="AliveCount"/>가 아니라
        /// <see cref="RemainingActors"/>인 것이 핵심이다 — <see cref="ResolveUniteCommand"/>는
        /// <b>이번 라운드에 이미 행동한 슬롯을 참가에서 뺀다</b>(빼지 않으면 한 라운드에 두 번 때린다).
        /// 게이지는 행동마다 차므로 보통 슬롯 3~4의 차례에 100에 닿는데, 그때 살아있는 마릿수로
        /// 판정하면 실제 참가자가 1명뿐인 상황에서도 버튼이 열려 게이지 100을 1마리분에 태운다
        /// (같은 게이지를 슬롯 0에서 쓰면 5마리분이라 가치가 5배 차이 난다 — 그 1마리의 평범한
        /// 스킬보다도 약했다). 참가 규칙과 발동 조건은 같은 수를 봐야 한다.
        /// </summary>
        public bool CanUniteAttack => CanSubmitTeamCommand
            && UniteGauge >= UniteGaugeMax && RemainingActors >= 2;
        public bool LastWasUnite { get; private set; }
        public int[] UniteSlotDamages { get; private set; }
        public RaidBossIntent NextBossIntent { get; private set; }
        public RaidRoundResult CurrentRoundResult { get; private set; }
        public RaidRoundStage RoundStage => roundStage;
        /// <summary>
        /// 지금 팀 커맨드를 받을 수 있는가. <b><see cref="ActiveSlot"/>이 유효할 것</b>도 조건이다 —
        /// 순차 턴에서는 마지막 팀원이 행동을 마치면 슬롯이 -1이 되고 그 순간부터 보스 차례다.
        /// </summary>
        public bool CanSubmitTeamCommand => IsActive
            && roundStage == RaidRoundStage.Ready
            && ActiveSlot >= 0;
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
        /// <summary>
        /// 0보다 크면 이번 라운드엔 기절이 걸리지 않는다 — <b>연속 기절 잠금 방지</b>.
        /// 보스가 기절로 한 턴을 건너뛴 직후에만 켜지고 라운드마다 1씩 준다.
        /// 리더 1명만 스킬을 쓰던 시절엔 필요 없었지만, 팀 5마리가 각자 스킬을 쓰게 되면
        /// 기절기를 여럿 들고 매 라운드 재시도해 보스를 영구히 묶을 수 있다.
        /// </summary>
        private int bossStunImmuneRounds;

        /// <summary>
        /// 보스 HP가 절반 이하로 떨어지면 켜지는 <b>1회 래치</b>. 회복해도 풀리지 않는다 —
        /// 켜졌다 꺼졌다 하면 플레이어가 "지금 어느 국면인지"를 읽을 수 없다.
        /// 격노하면 전체공격 간격이 2→1로 줄고 단일 피해에 배율이 붙는다.
        /// </summary>
        public bool BossEnraged { get; private set; }

        private void UpdateBossPhase()
        {
            if (BossEnraged || BossStats == null || BossStats.MaxHp <= 0) return;
            float ratio = BossStats.CurrentHp / (float)BossStats.MaxHp;
            if (ratio <= GameConstants.Battle.RaidBossEnrageHpRatio)
                BossEnraged = true;
        }
        private bool bossShinyAtStart; // 시작 시점 스냅샷 — 도주/풀 재사용된 라이브 보스 참조로 이로치 오등록 방지
        private RaidRoundStage roundStage = RaidRoundStage.Completed;
        private IRaidRandomSource randomSource = new UnityRaidRandomSource();
        private bool raidEndedRaised;

        // ── 순차 팀 턴 ──
        // 팀 5마리가 한 라운드 안에서 **하나씩 차례로** 행동한다. 예전엔 리더 한 마리의 스킬만
        // 고르면 나머지가 같은 순간에 자동으로 딸려 나가 "팀 레이드"로 읽히지 않았다.
        /// <summary>이번 라운드에 이미 행동을 마친 슬롯. 라운드가 시작될 때마다 비운다.</summary>
        private bool[] actedThisRound;
        /// <summary>행동이 하나씩 쌓이는 이번 라운드의 결과. Ready 단계 내내 살아 있다.</summary>
        private RaidRoundResult roundInProgress;
        /// <summary>
        /// 이번 라운드에 기절이 <b>명중</b>했는가. 팀 전체에서 한 번만 판정하므로
        /// (여럿이 기절기를 들어도 보스가 여러 번 묶이지는 않는다) 팀 턴이 끝날 때 소비한다.
        /// </summary>
        private bool roundStunLanded;

        /// <summary>이번 라운드에 행동을 마친 팀원 수 — UI의 "2/5" 표기용.</summary>
        public int ActedThisRound
        {
            get
            {
                if (actedThisRound == null || TeamStats == null) return 0;
                int c = 0;
                for (int i = 0; i < actedThisRound.Length && i < TeamStats.Length; i++)
                    if (actedThisRound[i] && TeamStats[i] != null) c++;
                return c;
            }
        }

        /// <summary>이번 라운드에 행동할 팀원 총수(이미 행동한 쪽 + 아직 살아서 남은 쪽).</summary>
        public int RoundActorCount => ActedThisRound + RemainingActors;

        /// <summary>이 슬롯이 이번 라운드에 이미 행동했는가 — UI가 팀 목록에 "✓"를 찍는 데 쓴다.</summary>
        public bool HasActedThisRound(int slot)
        {
            return actedThisRound != null
                && slot >= 0 && slot < actedThisRound.Length
                && actedThisRound[slot];
        }

        /// <summary>아직 행동하지 않은 생존 팀원 수. 0이면 보스 차례다.</summary>
        public int RemainingActors
        {
            get
            {
                if (TeamStats == null || actedThisRound == null) return 0;
                int c = 0;
                for (int i = 0; i < TeamStats.Length; i++)
                {
                    if (i < actedThisRound.Length && actedThisRound[i]) continue;
                    if (TeamStats[i] != null && TeamStats[i].CurrentHp > 0) c++;
                }
                return c;
            }
        }

        /// <summary>
        /// 레이드를 시작한다. 시작하지 못했으면 <c>false</c>(인자 부적합 또는 <b>팀 전원 기절</b>).
        /// 실패 시 상태를 건드리지 않고 <see cref="RaidUpdated"/>도 발화하지 않는다 —
        /// 발화하면 <c>RaidBattleUI</c>가 Intro로 들어가 조작 불가 화면이 열린다.
        /// </summary>
        public bool StartRaid(InsectEntity bossEntity,
            InsectData[] teamInsects, int[] teamLevels,
            PlayerInsectData[] teamPids, InsectSkill[][] teamSkills)
        {
            if (bossEntity == null || bossEntity.Data == null
                || teamInsects == null || teamLevels == null
                || teamInsects.Length == 0 || teamLevels.Length < teamInsects.Length)
            {
                return false;
            }

            BossEntity = bossEntity;
            bossShinyAtStart = bossEntity.IsShiny;
            // 레이드 동안 보스(야생 엔티티)가 도주→Despawn→풀 재사용되어 종료 시 무관 곤충이
            // 등록/Despawn되는 보상 무결성 손상 차단. (1v1 StartBattle과 동일)
            bossEntity.SetEngaged(true);
            InsectData bd = bossEntity.Data;

            InsectBattleStats rawBoss = new InsectBattleStats(bd, bossEntity.Level);
            BossStats = new RaidBossStats(
                bd,
                bossEntity.Level,
                Mathf.RoundToInt(rawBoss.MaxHp * GameConstants.Battle.RaidBossHpMultiplier),
                rawBoss.Attack * 3 / 2,
                rawBoss.Defense * 13 / 10);

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

            // 전원 기절(HP 0)이면 시작하지 않는다. 진짜 HP는 지속 HP 시드(InsectBattleStats) 때문에
            // TeamStats를 만들어봐야 알 수 있어 판정이 여기에 있다.
            // 시작해버리면 ActiveSlot이 -1로 남아 CanUseSkill이 늘 false → 팀이 행동 못 함 →
            // 보스 턴(ResolveBossResponse)도 오지 않고, 패배 판정이 그 안에만 있어 **영구 정지**한다.
            // 위에서 이미 건 SetEngaged(true)를 되돌리고 만들던 상태를 전부 비운다.
            if (FindFirstAlive() < 0)
            {
                bossEntity.SetEngaged(false);
                BossEntity = null;
                BossStats = null;
                TeamStats = null;
                TeamData = null;
                TeamPids = null;
                TeamSkills = null;
                TeamCooldowns = null;
                return false;
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
            bossStunImmuneRounds = 0;
            BossEnraged = false;
            TeamStance = RaidTeamStance.Assault;
            LastBossSkill = null;
            RewardCandy = 0;
            RewardExp = 0;
            CurrentRoundResult = null;
            roundStage = RaidRoundStage.Ready;
            raidEndedRaised = false;
            actedThisRound = null;
            roundInProgress = null;
            roundStunLanded = false;

            PrepareNextBossIntent();
            BeginRound();   // 첫 라운드를 연다 — ActiveSlot이 첫 생존 슬롯에 선다
            RaidUpdated?.Invoke();
            return true;
        }

        /// <summary>
        /// 행동 순서를 앞당겨 <paramref name="slot"/>부터 시키고 싶을 때. <b>이미 이번 라운드에
        /// 행동한 슬롯으로는 돌아갈 수 없다</b> — 허용하면 한 곤충이 한 라운드에 여러 번 때린다.
        /// </summary>
        public void SelectSlot(int slot)
        {
            if (!CanSubmitTeamCommand) return;
            if (slot < 0 || slot >= TeamStats.Length) return;
            if (TeamStats[slot] == null || TeamStats[slot].CurrentHp <= 0) return;
            if (actedThisRound != null && slot < actedThisRound.Length && actedThisRound[slot]) return;
            ActiveSlot = slot;
        }

        /// <summary>
        /// 라운드를 연다 — 행동 기록을 비우고 첫 행동자를 세운 뒤 빈 결과를 만든다.
        /// <see cref="PrepareNextBossIntent"/> <b>뒤에</b> 불러야 한다(결과가 보스 예고를 안고 태어난다).
        /// </summary>
        private void BeginRound()
        {
            int count = TeamStats != null ? TeamStats.Length : 0;
            if (actedThisRound == null || actedThisRound.Length != count)
                actedThisRound = new bool[count];
            else
                for (int i = 0; i < count; i++) actedThisRound[i] = false;

            roundStunLanded = false;
            ActiveSlot = FindNextActor();
            roundInProgress = new RaidRoundResult(
                TurnNumber + 1, ActiveSlot, -1, false, NextBossIntent, count);
            CurrentRoundResult = roundInProgress;
            roundStage = RaidRoundStage.Ready;
        }

        /// <summary>이번 라운드에 아직 행동하지 않은 첫 생존 슬롯. 없으면 -1(=보스 차례).</summary>
        private int FindNextActor()
        {
            if (TeamStats == null || actedThisRound == null) return -1;
            for (int i = 0; i < TeamStats.Length; i++)
            {
                if (i < actedThisRound.Length && actedThisRound[i]) continue;
                if (TeamStats[i] != null && TeamStats[i].CurrentHp > 0) return i;
            }
            return -1;
        }

        public bool CanUseSkill(int skillIndex)
        {
            if (!CanSubmitTeamCommand || ActiveSlot < 0) return false;
            // 이미 행동한 슬롯은 다시 못 쓴다. 정상 흐름에서는 ActiveSlot이 미행동 슬롯만 가리키지만,
            // RaidMemberActionResolved가 발화하는 순간에는 아직 ActiveSlot이 **방금 행동한 슬롯**이고
            // roundStage도 Ready라 CanSubmitTeamCommand가 true다 — 그 핸들러에서 커맨드를 넣는
            // 구독자가 생기면 한 곤충이 라운드에 두 번 때린다. ResolveUniteCommand는 같은 위험을
            // HasActedThisRound로 막고 있었는데 이쪽 입구에만 그 검사가 없었다.
            if (HasActedThisRound(ActiveSlot)) return false;
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

        /// <summary>
        /// <b>지금 차례인 팀원 하나</b>가 고른 스킬을 쓴다. 예전엔 이 호출 한 번에 리더 + 나머지
        /// 생존 팀원 전원이 같은 순간에 행동했고(팀 러시), 그래서 플레이어가 조종하는 건
        /// 사실상 한 마리뿐이었다. 지금은 슬롯 하나만 소비하고 차례가 다음으로 넘어간다.
        /// </summary>
        public RaidRoundResult ResolveTeamCommand(int skillIndex)
        {
            if (!CanUseSkill(skillIndex)) return null;

            int slot = ActiveSlot;
            InsectSkill skill = TeamSkills[slot][skillIndex];
            RaidActionResult action = RaidRoundResolver.ResolveLeaderSkill(
                slot, skillIndex, TeamStats[slot], BossStats, TeamStats, skill, randomSource);
            TeamCooldowns[slot][skillIndex] = skill.cooldownTurns;
            return CommitMemberAction(slot, action);
        }

        /// <summary>
        /// 지금 차례인 팀원을 <b>AI에게 맡긴다</b>. 어느 스킬을 쓸지는 <see cref="RaidSupportPlanner"/>가
        /// 현재 <see cref="TeamStance"/>로 고르고, 위력은 직접 고를 때보다 낮다
        /// (<c>GameConstants.Battle.RaidSupportSkillPowerMultiplier</c>) — 직접 조작이 유리해야
        /// 위임이 "조작을 줄이는 선택지"로 남고 늘 옳은 답이 되지 않는다.
        /// </summary>
        public RaidRoundResult ResolveAutoCommand()
        {
            if (!CanSubmitTeamCommand) return null;

            int slot = ActiveSlot;
            if (TeamStats[slot] == null || TeamStats[slot].CurrentHp <= 0) return null;

            // TeamSkills는 호출부가 준 배열을 그대로 들고 있다 — 길이 보장은 계약이지 코드가 아니다.
            InsectSkill[] slotSkills = TeamSkills != null && slot < TeamSkills.Length
                ? TeamSkills[slot]
                : null;
            int pick = RaidSupportPlanner.SelectSupportSkillIndex(
                slot, TeamStats[slot], slotSkills, TeamCooldowns[slot],
                BossStats, TeamStats,
                roundInProgress != null ? roundInProgress.BossIntent : NextBossIntent,
                TeamStance,
                // 이번 라운드엔 기절이 안 걸린다는 걸 플래너도 알아야 한다 —
                // 모르면 기절기를 든 팀원이 저항당할 시도를 고른다.
                //
                // roundStunLanded도 함께 넘긴다: 기절은 팀 전체에서 **한 번만** 판정되는데
                // (FinishTeamPhase가 bool 하나를 소비한다), 팀 턴이 순차가 되면서 슬롯 0이
                // 맞히면 슬롯 1~4의 기절은 전부 확정 무의미해졌다. Guard 스탠스에서 기절
                // 가중치가 가장 높아 그대로 골라지므로, "전원 자동"이면 라운드당 최대 4턴이 날아간다.
                bossStunImmuneRounds > 0 || roundStunLanded);

            RaidActionResult action;
            if (pick < 0)
            {
                // 스킬이 없거나 전부 쿨다운·0점 — 기본 지원 공격으로 폴백.
                action = RaidRoundResolver.ResolveSupportAssist(slot, TeamStats[slot], BossStats);
            }
            else
            {
                InsectSkill picked = slotSkills[pick];
                action = RaidRoundResolver.ResolveSupportSkill(
                    slot, pick, TeamStats[slot], BossStats, TeamStats, picked, randomSource);
                TeamCooldowns[slot][pick] = picked.cooldownTurns;
            }

            return CommitMemberAction(slot, action);
        }

        /// <summary>
        /// 남은 팀원 <b>전부</b>를 AI에게 맡겨 이번 라운드의 팀 턴을 닫는다. 1탭짜리 조작 절약이고,
        /// 결과는 개편 전의 "리더만 고르면 나머지 자동"과 같은 자리다.
        /// 반환은 마지막으로 갱신된 라운드 결과(한 명도 처리하지 못했으면 null).
        /// </summary>
        public RaidRoundResult ResolveAutoRemaining()
        {
            RaidRoundResult last = null;
            // 슬롯 수만큼만 돈다 — 호출 하나가 반드시 슬롯 하나를 소비하므로 무한루프가 없다.
            int guard = TeamStats != null ? TeamStats.Length : 0;
            while (guard-- > 0 && CanSubmitTeamCommand)
            {
                RaidRoundResult resolved = ResolveAutoCommand();
                if (resolved == null) break;
                last = resolved;
            }
            return last;
        }

        /// <summary>
        /// 팀원 하나의 행동을 이번 라운드에 적는다 — 게이지·표시 문구를 갱신하고,
        /// 다음 행동자가 있으면 차례를 넘기고 없으면 팀 턴을 닫는다.
        /// 세 진입점(직접 선택·자동 위임·스킬 없음 폴백)이 전부 여기로 모인다.
        /// </summary>
        private RaidRoundResult CommitMemberAction(int slot, RaidActionResult action)
        {
            RaidRoundResult round = roundInProgress;
            if (round == null || action == null) return null;

            round.AddTeamAction(action);
            if (slot >= 0 && actedThisRound != null && slot < actedThisRound.Length)
                actedThisRound[slot] = true;
            if (action.StunApplied) roundStunLanded = true;

            if (action.Damage > 0)
            {
                // 라운드가 5행동으로 쪼개졌으므로 고정분(예전 12)도 나눠 준다.
                // 그대로 두면 라운드마다 게이지가 5배로 차 합체공격이 매 라운드 열린다.
                UniteGauge = Mathf.Min(
                    UniteGauge + 2.5f + action.Damage * 0.15f,
                    UniteGaugeMax);
            }

            LastDamageToBoss = action.Damage;
            LastDamageToTeam = 0;
            LastHitSlot = -1;
            BossUsedAoe = false;
            LastBossSkill = null;
            LastWasUnite = false;
            UniteSlotDamages = null;
            LastActionText = BuildMemberActionText(action);

            RaidMemberActionResolved?.Invoke(action);

            if (BossStats.CurrentHp <= 0)
            {
                round.EndState = RaidRoundEndState.Victory;
                FinishTeamPhase(round);
                return round;
            }

            int next = FindNextActor();
            if (next >= 0)
            {
                ActiveSlot = next;
                RaidUpdated?.Invoke();
                return round;
            }

            FinishTeamPhase(round);
            return round;
        }

        /// <summary>
        /// 팀 전원이 행동을 마쳤다 — 기절을 한 번만 판정하고 보스 차례로 넘긴다.
        /// 합체공격도 팀 턴을 통째로 소비하므로 같은 자리를 지난다.
        /// </summary>
        /// <param name="summaryText">
        /// 라운드 요약 문구. null이면 기본 요약을 쓴다 — 합체공격만 자기 문구를 넘긴다.
        /// </param>
        private void FinishTeamPhase(RaidRoundResult round, string summaryText = null)
        {
            // 기절은 팀 전체에서 한 번만 판정한다 — 명중(리졸버의 StunApplied)과 면역 둘 다 통과해야 한다.
            // 예전엔 리더의 effectType만 보고 무조건 걸어서 명중률을 무시했다.
            bool stunResisted = false;
            if (roundStunLanded)
            {
                if (bossStunImmuneRounds > 0) stunResisted = true;
                else bossStunned = true;
            }
            roundStunLanded = false;

            ActiveSlot = -1;
            LastDamageToBoss = round.TotalDamageToBoss;
            LastActionText = summaryText ?? BuildTeamRoundText(round);
            if (stunResisted)
                LastActionText += "\n보스가 기절에 저항했다!";

            round.Stage = RaidRoundStage.TeamResolved;
            CurrentRoundResult = round;
            roundStage = RaidRoundStage.TeamResolved;
            RaidTeamRushResolved?.Invoke(round);
            RaidUpdated?.Invoke();
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
                // 다음 라운드엔 기절이 안 걸린다 — 연속 기절로 보스를 영구히 묶는 것을 막는다.
                bossStunImmuneRounds = 2;   // 이번 라운드 말미에 1이 줄어 다음 라운드만 면역
                // **bossCooldown은 건드리지 않는다.** 스킵된 전체공격은 소비되지 않고 다음 라운드에
                // 다시 예고된다("막았다"가 아니라 "미뤘다"). 예전엔 갱신이 아래 else 안에만 있어서
                // 결과가 같아 보였지만 의미가 정반대였다 — AOE 예고 턴마다 기절을 맞추면
                // bossCooldown이 0에 머물러 보스가 전체공격을 **영원히 예고만** 했다.
                // 이제 위 면역이 연속 시도를 끊어 반드시 다음 라운드에 실행된다.
            }
            else
            {
                RaidActionResult bossAction = RaidRoundResolver.ResolveBossIntent(
                    intent, BossStats, TeamStats, result.BossDamageBySlot,
                    BossEnraged ? GameConstants.Battle.RaidBossEnragedDamageMultiplier : 1f);
                result.BossAction = bossAction;
                result.BossResponseResolved = true;
                for (int i = 0; i < result.BossDamageBySlot.Length; i++)
                    result.TotalDamageToTeam += result.BossDamageBySlot[i];

                bool area = intent != null && intent.IsArea;
                if (area)
                    bossCooldown = BossEnraged
                        ? GameConstants.Battle.RaidBossEnragedAreaInterval
                        : GameConstants.Battle.RaidBossAreaInterval;
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

            // 기절한 슬롯을 건너뛰는 처리는 여기 없다 — 다음 라운드를 여는 `BeginRound`가
            // 생존 슬롯만 골라 `ActiveSlot`을 다시 세운다(순차 턴에서는 그쪽이 단일 출처다).

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
            if (bossStunImmuneRounds > 0) bossStunImmuneRounds--;
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

            PrepareNextBossIntent();
            BeginRound();   // 다음 라운드 — 행동 기록을 비우고 첫 생존 슬롯에 차례를 준다
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

            UpdateBossPhase();
            InsectSkill signature = GetUnlockedBossSignature(
                BossStats.Data, BossStats.Level, TurnNumber);
            NextBossIntent = RaidRoundResolver.CreateBossIntent(
                TurnNumber + 1,
                BossStats,
                TeamStats,
                bossCooldown,
                signature,
                randomSource,
                GameConstants.Battle.RaidBossUsesAreaAttack);
        }

        /// <summary>팀원 하나가 방금 한 행동의 한 줄 설명. 순차 턴이라 라운드마다 여러 번 갱신된다.</summary>
        private string BuildMemberActionText(RaidActionResult action)
        {
            if (action == null) return string.Empty;

            string actor = SlotDisplayName(action.SourceSlot);
            if (action.Missed) return $"{actor}의 {action.DisplayName}! 빗나갔다!";
            if (action.Capped) return $"{actor}의 {action.DisplayName}! 이미 최대치다!";   // 턴은 소비됐다
            if (action.Healing > 0) return $"{actor}의 {action.DisplayName}! HP {action.Healing} 회복!";
            if (action.Damage > 0) return $"{actor}의 {action.DisplayName}! {action.Damage} 피해!";
            return $"{actor}의 {action.DisplayName}!";
        }

        /// <summary>팀 전원이 행동을 마친 뒤의 라운드 요약.</summary>
        private static string BuildTeamRoundText(RaidRoundResult result)
        {
            if (result == null || result.TeamActions.Count == 0)
                return string.Empty;
            return $"팀 {result.TeamActions.Count}마리 행동 완료!  TOTAL {result.TotalDamageToBoss}";
        }

        private string SlotDisplayName(int slot)
        {
            if (TeamStats == null || slot < 0 || slot >= TeamStats.Length) return "팀원";
            InsectBattleStats stats = TeamStats[slot];
            return stats != null && stats.Data != null
                ? stats.Data.displayName
                : $"팀원 {slot + 1}";
        }

        private string BuildBossResponseText(RaidBossIntent intent)
        {
            if (intent == null || BossStats == null || BossStats.Data == null)
                return string.Empty;
            return intent.IsArea
                ? $"\n{BossStats.Data.displayName}의 전체 공격!"
                : $"\n{BossStats.Data.displayName}의 {intent.DisplayName}!";
        }

        /// <summary>
        /// 해금된 시그니처 스킬을 <paramref name="rotation"/>으로 순환해 하나 고른다.
        ///
        /// 예전엔 learnset의 <b>첫 항목만 영구 반환</b>해서 보스가 매 턴 같은 기술만 썼다 —
        /// 한 번 싸우면 다 본 것이 됐다. 난수가 아니라 라운드 번호로 도는 이유는 예고(intent)와
        /// 실행이 같은 객체를 공유하는 결정론을 깨지 않기 위해서다.
        /// <b>시그니처가 하나뿐이면 결과가 예전과 완전히 같다</b> — 무위험 폴백.
        /// </summary>
        private static InsectSkill GetUnlockedBossSignature(InsectData data, int level, int rotation)
        {
            if (data == null || data.learnset == null) return null;

            int count = 0;
            foreach (InsectLearnableSkill learnable in data.learnset)
                if (IsUnlockedSignature(learnable, level)) count++;
            if (count == 0) return null;

            int pick = count > 1 ? ((rotation % count) + count) % count : 0;
            int seen = 0;
            foreach (InsectLearnableSkill learnable in data.learnset)
            {
                if (!IsUnlockedSignature(learnable, level)) continue;
                if (seen == pick) return learnable.skill;
                seen++;
            }

            return null;
        }

        private static bool IsUnlockedSignature(InsectLearnableSkill learnable, int level)
        {
            return learnable != null
                && learnable.skill != null
                && learnable.skill.isSignatureSkill
                && learnable.learnLevel <= level;
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
                roundInProgress != null ? roundInProgress.BossIntent : NextBossIntent,
                TeamStats.Length);
            List<string> names = new List<string>();
            for (int i = 0; i < TeamStats.Length; i++)
            {
                InsectBattleStats attacker = TeamStats[i];
                if (attacker == null || attacker.CurrentHp <= 0) continue;
                // **이번 라운드에 이미 행동한 슬롯은 참여하지 않는다.**
                // 게이지가 슬롯마다 차오르므로(CommitMemberAction) 팀 턴 **도중에** 100을 넘고,
                // CanUniteAttack은 ActiveSlot을 보지 않는다 — 슬롯 0~3으로 스킬을 다 쓴 뒤
                // 슬롯 4 차례에 [F]를 누르면 0~3이 한 라운드에 **두 번** 때렸다.
                // (아래 주석은 반대쪽 절반만 막고 있었다: 아직 차례가 안 온 팀원의 이중 행동.)
                if (HasActedThisRound(i)) continue;
                result.AddTeamAction(
                    RaidRoundResolver.ResolveUniteContribution(i, attacker, BossStats));
                names.Add(attacker.Data != null ? attacker.Data.displayName : $"팀원 {i + 1}");
            }

            // 이미 커밋된 이번 라운드 피해를 이월한다 — 아래에서 roundInProgress를 새 결과로
            // 갈아끼우므로, 이월하지 않으면 합체 이전 슬롯들의 피해가 통째로 사라져
            // FinishTeamPhase의 LastDamageToBoss와 UI 기여도 표시가 과소 집계된다.
            if (roundInProgress != null)
                result.TotalDamageToBoss += roundInProgress.TotalDamageToBoss;

            // 합체공격은 팀 턴을 통째로 소비한다 — 아직 차례가 오지 않았던 팀원도 여기에 참여했다.
            // 표시하지 않으면 합체 직후 남은 슬롯들이 한 번 더 행동해 한 라운드에 두 번 때린다.
            if (actedThisRound != null)
                for (int i = 0; i < actedThisRound.Length; i++) actedThisRound[i] = true;

            UniteGauge = 0f;
            LastWasUnite = true;
            UniteSlotDamages = result.UniteSlotDamages;
            BossUsedAoe = false;
            LastBossSkill = null;
            LastDamageToTeam = 0;
            LastHitSlot = -1;

            if (BossStats.CurrentHp <= 0)
                result.EndState = RaidRoundEndState.Victory;

            roundInProgress = result;
            FinishTeamPhase(result, $"★ 합체공격! {string.Join(" + ", names)} ★");
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
