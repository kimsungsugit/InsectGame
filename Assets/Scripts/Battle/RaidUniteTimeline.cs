namespace InsectGame.Battle
{
    /// <summary>
    /// 레이드 합체공격 타임라인(연출 초) — 3D 아레나의 돌진·타격과 UI 오버레이의 슬롯별 피해 숫자·TOTAL이
    /// 같은 시각을 쓰게 하는 단일 출처다. 둘 다 <c>BattlePresentation.DeltaTime</c>으로 세므로 히트스톱·
    /// 슬로모션 중에도 맞물린다.
    ///
    /// 예전엔 오버레이가 자기 숫자(0.3·0.2·0.45·1.5·1.8)를 박아 두고 아레나는 0.42초 만에 전원 동시
    /// 돌진을 끝내서, 3D에선 이미 돌아온 팀원 위로 2D 숫자가 차례로 떴다. 5인 팀이면 마지막 팀원의
    /// 타격(1.55초)이 "합동 일격"(1.5초)보다 늦기까지 했다.
    /// </summary>
    public static class RaidUniteTimeline
    {
        /// <summary>기 모으기 — 전원이 제자리에서 들썩이는 시간.</summary>
        public const float MemberStart = 0.3f;
        /// <summary>팀원끼리 출발 간격.</summary>
        public const float MemberStagger = 0.16f;
        /// <summary>한 팀원의 돌진 시간.</summary>
        public const float MemberTravel = 0.45f;
        /// <summary>마지막 합동 일격 — 5번째 팀원의 타격(1.39초) 뒤.</summary>
        public const float FinalStrike = 1.6f;
        /// <summary>TOTAL 표시 시작.</summary>
        public const float TotalReveal = 1.85f;
        /// <summary>팀원이 제자리로 돌아와 연출이 끝나는 시각. <c>RaidBattleUI.UniteRushMinDuration</c>과 같다.</summary>
        public const float Total = 2.5f;

        /// <summary>살아 있는 팀원 중 <paramref name="order"/>번째가 보스를 때리는 시각.</summary>
        public static float MemberHit(int order)
        {
            return MemberStart + order * MemberStagger + MemberTravel;
        }
    }
}
