namespace InsectGame.Core
{
    /// <summary>
    /// <c>OutfitItem.unlockCondition</c> 토큰의 판정. 순수 함수라 씬 없이 테스트한다.
    ///
    /// 이 판정은 오래 <b>없었다</b>. 카탈로그에 조건 4개(<c>region_garden</c>·<c>level_15</c>·
    /// <c>region_pond</c>·<c>quest_q_complete</c>)가 적혀 있고 의상 화면은 그걸 "잠김 — ~시 해금"으로
    /// 보여 주는데, 조건을 평가해 소유를 주는 코드가 저장소 어디에도 없어 네 벌이 영영 잠겨 있었다
    /// (<c>top_lab</c>이 그중 하나라 연구원 세트는 완성 자체가 불가능했다).
    ///
    /// 형식은 <c>CharacterOutfitUI.DescribeUnlockCondition</c>의 문구와 1:1이다 —
    /// 새 형식을 늘리면 두 곳을 함께 고친다(<c>OutfitUnlockRulesTests</c>가 카탈로그의 모든 조건이
    /// 이 판정기로 해석되는지 고정한다).
    /// </summary>
    public static class OutfitUnlockRules
    {
        public const string RegionPrefix = "region_";
        public const string LevelPrefix = "level_";
        public const string QuestPrefix = "quest_";

        /// <summary>
        /// 조건이 지금 충족됐는가. 모르는 형식이나 빈 조건은 false다 — 잘못 적은 토큰이
        /// 의상을 공짜로 풀어 버리지 않게 한다.
        /// </summary>
        /// <param name="currentRegionId">
        /// 플레이어가 <b>지금 서 있는</b> 리전. 문구가 "~ 도달 시 해금"이라 개방(접근 가능)이 아니라
        /// 실제로 들어간 순간을 본다 — 개방으로 판정하면 가 보지도 않은 곳의 기념품을 먼저 받는다.
        /// </param>
        public static bool IsMet(string condition, string currentRegionId, int playerLevel,
            System.Func<string, bool> isQuestCompleted)
        {
            if (string.IsNullOrEmpty(condition)) return false;

            if (condition.StartsWith(RegionPrefix))
            {
                string regionId = condition.Substring(RegionPrefix.Length);
                return regionId.Length > 0 && regionId == currentRegionId;
            }
            if (condition.StartsWith(LevelPrefix))
            {
                return TryParseLevel(condition, out int need) && playerLevel >= need;
            }
            if (condition.StartsWith(QuestPrefix))
            {
                string questId = condition.Substring(QuestPrefix.Length);
                return questId.Length > 0 && isQuestCompleted != null && isQuestCompleted(questId);
            }
            return false;
        }

        /// <summary>"level_15" → 15. 형식이 틀리면 false.</summary>
        public static bool TryParseLevel(string condition, out int level)
        {
            level = 0;
            if (string.IsNullOrEmpty(condition) || !condition.StartsWith(LevelPrefix)) return false;
            return int.TryParse(condition.Substring(LevelPrefix.Length), out level) && level > 0;
        }

        /// <summary>이 판정기가 이해하는 형식인가(테스트·검사용).</summary>
        public static bool IsKnownFormat(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return false;
            if (condition.StartsWith(RegionPrefix)) return condition.Length > RegionPrefix.Length;
            if (condition.StartsWith(QuestPrefix)) return condition.Length > QuestPrefix.Length;
            return TryParseLevel(condition, out _);
        }
    }
}
