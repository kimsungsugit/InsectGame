using InsectGame.Data;

namespace InsectGame.Battle
{
    /// <summary>
    /// 상성 등급 — 피해기 한 번이 상대에게 얼마나 통했나. 아이용 문구(「아주 잘 통해요」·「잘 통해요」·「잘 안 통해요」…)와
    /// 연출 세기는 화면 쪽(ui-dev·visual-dev)이 이 값으로 고른다. <see cref="Neutral"/>이 기본값(0)이다 — 피해기가 아니거나
    /// 빗나갔거나 상성을 안 타는 공격(레이드 보스 전체공격·독 일괄딜)은 이 값이다.
    /// </summary>
    public enum Matchup
    {
        /// <summary>보통(배수 0.95~1.05 — 실제 표에서는 1.0과 「강점×약점」 1.005).</summary>
        Neutral = 0,
        /// <summary>두 속성 모두에 강하다(실제 표에서는 2.25).</summary>
        Super,
        /// <summary>강하다(실제 표에서는 1.5).</summary>
        Good,
        /// <summary>약하다(실제 표에서는 0.67).</summary>
        Weak,
        /// <summary>두 속성 모두에 약하다(실제 표에서는 0.45 — 하한 clamp).</summary>
        Resisted
    }

    /// <summary>
    /// 상성 배수 → <see cref="Matchup"/>. 순수 — 1대1·레이드·스킬 카드가 같은 경계를 쓴다.
    ///
    /// 경계는 실제 상성표(<see cref="InsectTypeChart"/>: 강 ×1.5 · 약 ×0.67 · 복합 곱 · clamp 0.45~2.25)에서 나오는 여섯 값
    /// {2.25, 1.5, 1.005, 1.0, 0.67, 0.45}이 서로 다른 칸에 떨어지도록 잡았다. 「좋음/나쁨」 경계(1.05/0.95)는 전투 문구
    /// 타격 순간의 상성 표시(<c>BattleFeelHud</c> — 「잘 통했다!」 등)와 스킬 카드 칩이 <b>같은 값</b>을 읽는다 — 칩과 문구가 갈리지 않게.
    /// 강점×약점 복합(1.5×0.67 = 1.005)은 보통이다.
    /// </summary>
    public static class ElementMatchup
    {
        /// <summary>이 배수 이상이면 <see cref="Matchup.Super"/>(2.25만 해당 — 1.5와 2.25 사이).</summary>
        public const float SuperAtLeast = 2.0f;
        /// <summary>이 배수 초과면 <see cref="Matchup.Good"/> 이상.</summary>
        public const float GoodAbove = 1.05f;
        /// <summary>이 배수 미만이면 <see cref="Matchup.Weak"/> 이하.</summary>
        public const float WeakBelow = 0.95f;
        /// <summary>이 배수 이하면 <see cref="Matchup.Resisted"/>(0.45만 해당 — 0.45와 0.67 사이).</summary>
        public const float ResistedAtMost = 0.5f;

        /// <summary>상성 배수(<see cref="InsectTypeChart.GetEffectiveness"/>)의 등급.</summary>
        public static Matchup Describe(float effectiveness)
        {
            if (effectiveness >= SuperAtLeast) return Matchup.Super;
            if (effectiveness > GoodAbove) return Matchup.Good;
            if (effectiveness <= ResistedAtMost) return Matchup.Resisted;
            if (effectiveness < WeakBelow) return Matchup.Weak;
            return Matchup.Neutral;   // NaN도 여기로 온다(비교가 전부 거짓)
        }

        /// <summary>
        /// <paramref name="attack"/> 속성 기술이 <paramref name="defender"/>에게 통하는 등급. 상대를 모르면 보통.
        /// 레이드 보스는 <b>지금 모습</b>(<c>CombatData</c>)을 넘긴다 — 모습이 바뀌면 약점이 바뀐다.
        /// </summary>
        public static Matchup Of(InsectElement attack, InsectData defender)
        {
            if (defender == null) return Matchup.Neutral;
            return Describe(InsectTypeChart.GetEffectiveness(attack, defender.primaryType, defender.secondaryType));
        }

        /// <summary>잘 통했다(<see cref="Matchup.Super"/>·<see cref="Matchup.Good"/>).</summary>
        public static bool IsFavorable(Matchup matchup) => matchup == Matchup.Super || matchup == Matchup.Good;

        /// <summary>잘 안 통했다(<see cref="Matchup.Weak"/>·<see cref="Matchup.Resisted"/>).</summary>
        public static bool IsUnfavorable(Matchup matchup) => matchup == Matchup.Weak || matchup == Matchup.Resisted;
    }
}
