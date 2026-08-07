using System;
using System.Collections.Generic;
using InsectGame.Data;

namespace InsectGame.Battle
{
    public enum RaidActionKind
    {
        LeaderSkill,
        SupportAssist,
        /// <summary>리더가 아닌 팀원이 <b>자기 스킬</b>을 쓴 행동. 스킬이 없거나 전부 쿨다운이면 <see cref="SupportAssist"/>로 폴백한다.</summary>
        SupportSkill,
        UniteContribution,
        BossSingle,
        BossArea,
        BossSkipped
    }

    public enum RaidBossIntentKind
    {
        SingleTarget,
        SignatureSkill,
        AreaAttack
    }

    public enum RaidRoundEndState
    {
        Ongoing,
        Victory,
        Defeat
    }

    /// <summary>
    /// 팀 스탠스 — 리더가 아닌 팀원의 AI 성향. 플레이어가 1탭으로 바꾸고 바꿀 때까지 유지된다.
    /// 5슬롯을 매 라운드 직접 지정하면 세로 모바일에서 라운드당 10탭이 되므로, 조작은 리더 선택만
    /// 남기고 나머지는 이 성향으로 조종한다(평소 추가 탭 0, 보스 예고를 읽었을 때만 1탭).
    /// </summary>
    public enum RaidTeamStance
    {
        Assault,   // 총공격 — 피해 우선
        Guard,     // 수비 — 방어·기절·디버프 우선
        Support    // 지원 — 회복 우선
    }

    public enum RaidRoundStage
    {
        Ready,
        TeamResolved,
        BossResolved,
        Completed
    }

    public interface IRaidRandomSource
    {
        float Next01();
        int NextInt(int minInclusive, int maxExclusive);
    }

    public sealed class RaidBossIntent
    {
        public int RoundNumber { get; internal set; }
        public RaidBossIntentKind Kind { get; internal set; }
        public int TargetSlot { get; internal set; } = -1;
        public InsectSkill Skill { get; internal set; }
        public InsectElement Element { get; internal set; }
        public SkillEffectType EffectType { get; internal set; }
        public string DisplayName { get; internal set; }
        public bool IsArea => Kind == RaidBossIntentKind.AreaAttack;
    }

    public sealed class RaidActionResult
    {
        public RaidActionKind Kind { get; internal set; }
        public int SourceSlot { get; internal set; } = -1;
        public int TargetSlot { get; internal set; } = -1;
        public int SkillIndex { get; internal set; } = -1;
        public InsectSkill Skill { get; internal set; }
        public InsectElement Element { get; internal set; }
        public SkillEffectType EffectType { get; internal set; }
        public string DisplayName { get; internal set; }
        public int Damage { get; internal set; }
        public int Healing { get; internal set; }
        public bool Missed { get; internal set; }
        public bool KnockedOut { get; internal set; }
        // 버프·디버프가 스택 상한(GameConstants.Battle.MaxBuffStacks)에 걸려 값이 바뀌지 않았음.
        // 턴은 이미 소비됐으므로 UI가 "이미 최대치"를 알려야 한다.
        public bool Capped { get; internal set; }
        /// <summary>
        /// 기절 스킬이 <b>명중까지 성공</b>했음. 예전엔 컨트롤러가 <c>effectType == Stun</c>만 보고
        /// 무조건 <c>bossStunned = true</c>로 했다 — 1v1은 <c>LandsHit</c>를 통과해야 하는데 레이드만
        /// 무조건 걸려서, 명중 60%짜리 기절기가 레이드에선 100%였다.
        /// 실제 기절 여부(연속 기절 면역 등)는 컨트롤러가 이 값을 받아 최종 판정한다.
        /// </summary>
        public bool StunApplied { get; internal set; }
        public bool IsLeader => Kind == RaidActionKind.LeaderSkill;
        /// <summary>
        /// 리더가 아닌 팀원의 행동 — <b>기본 지원 공격과 자기 스킬을 모두 포함한다.</b>
        /// 호출부(<c>RaidBattleUI</c>의 볼리 연출 두 곳)가
        /// <c>if (action.Damage &lt;= 0 &amp;&amp; !action.IsSupport) continue;</c>로 거르므로,
        /// 여기에 <see cref="RaidActionKind.SupportSkill"/>을 빠뜨리면 <b>0딜 버프·힐·기절 서포트가
        /// 연출에서 통째로 사라진다</b>(피해가 없어서 앞 조건에 걸린다).
        /// </summary>
        public bool IsSupport => Kind == RaidActionKind.SupportAssist
            || Kind == RaidActionKind.SupportSkill;
        public bool IsUnite => Kind == RaidActionKind.UniteContribution;
    }

    public sealed class RaidRoundResult
    {
        private readonly List<RaidActionResult> teamActions = new List<RaidActionResult>();

        internal RaidRoundResult(int roundNumber, int leaderSlot, int leaderSkillIndex,
            bool isUnite, RaidBossIntent bossIntent, int teamSize)
        {
            RoundNumber = roundNumber;
            LeaderSlot = leaderSlot;
            LeaderSkillIndex = leaderSkillIndex;
            IsUnite = isUnite;
            BossIntent = bossIntent;
            BossDamageBySlot = new int[Math.Max(0, teamSize)];
            UniteSlotDamages = isUnite ? new int[Math.Max(0, teamSize)] : null;
            Stage = RaidRoundStage.Ready;
        }

        public int RoundNumber { get; internal set; }
        public int LeaderSlot { get; }
        public int LeaderSkillIndex { get; }
        public bool IsUnite { get; }
        public RaidBossIntent BossIntent { get; }
        public IReadOnlyList<RaidActionResult> TeamActions => teamActions;
        public RaidActionResult BossAction { get; internal set; }
        /// <summary>
        /// 보스 턴에 슬롯별로 받은 피해. <b>채우는 쪽은 RaidRoundResolver.ResolveBossIntent</b>가
        /// 이 배열을 그대로 받아 쓰고, 합산은 RaidBattleController가 따로 돈다
        /// (리졸버가 결과 모델을 몰라도 되게 유지하려는 의도적 분리 — 순수 배열로 테스트된다).
        /// 그러니 여기에 "슬롯 하나 기록 + TotalDamageToTeam 가산"을 하는 세터를 만들지 말 것.
        /// 실제로 그런 SetBossDamage가 있었고, 호출부는 0인데 쓰는 순간 컨트롤러의 합산과
        /// 겹쳐 TotalDamageToTeam이 두 배가 되는 함정이었다(2026-08-03 제거).
        /// </summary>
        public int[] BossDamageBySlot { get; }
        public int[] UniteSlotDamages { get; }
        public int TotalDamageToBoss { get; internal set; }
        public int TotalDamageToTeam { get; internal set; }
        public bool BossResponseResolved { get; internal set; }
        public bool BossResponseSkipped { get; internal set; }
        public RaidRoundEndState EndState { get; internal set; }
        public RaidRoundStage Stage { get; internal set; }
        public bool BattleEnded => EndState != RaidRoundEndState.Ongoing;
        public bool PlayerWon => EndState == RaidRoundEndState.Victory;

        internal void AddTeamAction(RaidActionResult action)
        {
            if (action == null) return;
            teamActions.Add(action);
            TotalDamageToBoss += Math.Max(0, action.Damage);
            if (UniteSlotDamages != null
                && action.SourceSlot >= 0 && action.SourceSlot < UniteSlotDamages.Length)
            {
                UniteSlotDamages[action.SourceSlot] = Math.Max(0, action.Damage);
            }
        }
    }
}
