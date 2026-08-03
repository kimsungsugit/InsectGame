using System;
using System.Collections.Generic;
using InsectGame.Data;

namespace InsectGame.Battle
{
    public enum RaidActionKind
    {
        LeaderSkill,
        SupportAssist,
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
        public bool IsLeader => Kind == RaidActionKind.LeaderSkill;
        public bool IsSupport => Kind == RaidActionKind.SupportAssist;
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
