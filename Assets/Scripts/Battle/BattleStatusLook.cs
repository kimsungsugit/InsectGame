using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>몸에 보이는 상태 — 한 곤충에 여럿이 함께 걸릴 수 있다.</summary>
    [System.Flags]
    public enum BattleStatusFlags
    {
        None = 0,
        Poison = 1 << 0,
        Stun = 1 << 1,
        AttackUp = 1 << 2,
        AttackDown = 1 << 3,
        DefenseUp = 1 << 4,
        DefenseDown = 1 << 5
    }

    /// <summary>
    /// 상태이상·강화를 <b>몸에 보이는</b> 규칙 — 순수. 컨트롤러 상태를 플래그로 모으고, 색과 작은 반복 움직임의 곡선을 낸다.
    /// 그리기는 <c>BattleArenaController.Life</c>가 한다(모양·자리: 별은 머리 위, 거품은 몸 위, 오라·결은 발밑~몸, 막은 몸 둘레).
    ///
    /// 1대1은 HP 카드 상태 줄(<c>BattleStatusLine.Tally</c>)과 <b>같은 원천</b>(<c>PlayerStunTurns</c>·<c>GetActiveEffects</c>)을 같은 규칙으로 읽는다 —
    /// 카드에 「중독 3턴」이 떠 있으면 몸에도 거품이 오른다. 레이드는 버프 만료가 없어 누적 스택(<c>AttackStacks</c>·<c>DefenseStacks</c>)의 부호로 본다.
    /// </summary>
    public static class BattleStatusLook
    {
        public static readonly Color PoisonColor = new Color(0.74f, 0.4f, 0.98f);
        public static readonly Color StunColor = new Color(1f, 0.87f, 0.22f);
        public static readonly Color StunEdgeColor = new Color(0.55f, 0.3f, 0.04f);
        public static readonly Color AttackUpColor = new Color(1f, 0.44f, 0.12f);
        public static readonly Color AttackDownColor = new Color(0.6f, 0.15f, 0.08f);
        public static readonly Color DefenseUpColor = new Color(0.4f, 0.74f, 1f);
        public static readonly Color DefenseDownColor = new Color(0.13f, 0.23f, 0.6f);
        public static readonly Color HealColor = new Color(0.45f, 1f, 0.55f);

        /// <summary>1대1 한쪽의 몸 상태 — 기절은 컨트롤러의 따로 센 값, 나머지는 효과 목록(남은 턴이 있는 것만).</summary>
        public static BattleStatusFlags FromDuel(int stunTurns, InsectBattleController.EffectSnapshot[] effects, bool forPlayer)
        {
            BattleStatusFlags flags = stunTurns > 0 ? BattleStatusFlags.Stun : BattleStatusFlags.None;
            if (effects == null) return flags;
            for (int i = 0; i < effects.Length; i++)
            {
                InsectBattleController.EffectSnapshot e = effects[i];
                if (e.targetIsPlayer != forPlayer || e.remainingTurns <= 0) continue;
                switch (e.kind)
                {
                    case InsectBattleController.EffectKind.Dot:
                        flags |= BattleStatusFlags.Poison;
                        break;
                    case InsectBattleController.EffectKind.DefBuff:
                        flags |= e.value >= 0f ? BattleStatusFlags.DefenseUp : BattleStatusFlags.DefenseDown;
                        break;
                    default:
                        flags |= e.value >= 0f ? BattleStatusFlags.AttackUp : BattleStatusFlags.AttackDown;
                        break;
                }
            }
            return flags;
        }

        /// <summary>레이드 한 마리의 몸 상태 — 스택 부호(+ 강화, − 약화)와 보스 기절.</summary>
        public static BattleStatusFlags FromStacks(int attackStacks, int defenseStacks, bool stunned)
        {
            BattleStatusFlags flags = stunned ? BattleStatusFlags.Stun : BattleStatusFlags.None;
            if (attackStacks > 0) flags |= BattleStatusFlags.AttackUp;
            else if (attackStacks < 0) flags |= BattleStatusFlags.AttackDown;
            if (defenseStacks > 0) flags |= BattleStatusFlags.DefenseUp;
            else if (defenseStacks < 0) flags |= BattleStatusFlags.DefenseDown;
            return flags;
        }

        /// <summary>
        /// 레이드 보스가 지금 기절해 있는가 — 기절로 차례를 건너뛴 보스 응답(<c>BossResponseSkipped</c>)이 나온 뒤부터 그 라운드가 끝나도 다음
        /// 라운드가 시작될 때까지. 컨트롤러의 기절 표지(<c>bossStunned</c>)는 공개되지 않고, 기절 명중(<c>StunApplied</c>)은 면역 라운드에
        /// 저항될 수 있어 "건너뛰었다"만 확실하다.
        /// </summary>
        public static bool RaidBossDazed(RaidRoundResult round)
        {
            return round != null && round.BossResponseSkipped
                && (round.Stage == RaidRoundStage.BossResolved || round.Stage == RaidRoundStage.Completed);
        }

        // ── 움직임 곡선(나이 0..1) ──

        /// <summary>독 거품 한 알 — 몸 위로 오르며(0→1) 커지고, 끝에서 톡 부풀며 사라진다.</summary>
        public static void Bubble(float age01, out float rise, out float scale, out float alpha)
        {
            float a = Mathf.Clamp01(age01);
            rise = a;
            float pop = a > 0.85f ? (a - 0.85f) / 0.15f : 0f;
            scale = (0.45f + 0.75f * a) * (1f + 0.5f * pop);
            alpha = a < 0.12f ? a / 0.12f : a > 0.85f ? 1f - pop : 1f;
        }

        /// <summary>불꽃결·내려가는 결 한 가닥 — 나타났다(사인) 사라지고, 끝으로 갈수록 가늘어진다.</summary>
        public static void Streak(float age01, out float alpha, out float width)
        {
            float a = Mathf.Clamp01(age01);
            alpha = Mathf.Sin(a * Mathf.PI);
            width = 1f - 0.5f * a;
        }

        /// <summary>육각 막의 은은한 일렁임 0.18~0.4 — 위상이 다른 칸마다 띠처럼 지나간다. 섬광 줄이기면 고정 0.26.</summary>
        public static float ShieldAlpha(float seconds, float tilePhase, bool reducedFlashes)
        {
            if (reducedFlashes) return 0.26f;
            return 0.18f + 0.22f * Mathf.Max(0f, Mathf.Sin(seconds * 2.4f - tilePhase));
        }
    }
}
