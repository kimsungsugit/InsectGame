using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 캐릭터(트레이너) 레벨과 곤충 레벨의 차이가 포획·EXP에 주는 효과의 <b>단일 출처</b>.
    /// 계수는 <see cref="GameConstants.TrainerLevel"/>, 실측 근거는 <c>rules/balance.md</c>.
    ///
    /// 포획은 미니게임(<c>CaptureChanceCalculator</c>)과 전투 승리 후(<c>BattleCaptureChanceCalculator</c>)
    /// 두 경로가 같은 배율을 곱하고, EXP는 포획·전투·NPC 대결·레이드가 전부
    /// <c>InsectRewardCalculator.GetExpReward(data, insectLevel, trainerLevel)</c>로 이 배율을 받는다.
    /// 경로마다 따로 적으면 한쪽만 고쳐져 어긋난다.
    /// </summary>
    public static class TrainerLevelGap
    {
        /// <summary>곤충이 캐릭터보다 몇 레벨 높은가(음수면 낮다). 1 미만 레벨은 1로 본다.</summary>
        public static int Gap(int trainerLevel, int insectLevel)
        {
            return Mathf.Max(1, insectLevel) - Mathf.Max(1, trainerLevel);
        }

        /// <summary>
        /// 유예(<see cref="GameConstants.TrainerLevel.CaptureGraceLevels"/>)를 넘긴 레벨 차 —
        /// 포획 선택 화면의 경고도 이 값으로 판단한다.
        /// </summary>
        public static bool IsCaptureRestricted(int trainerLevel, int insectLevel)
        {
            return Gap(trainerLevel, insectLevel) > GameConstants.TrainerLevel.CaptureGraceLevels;
        }

        /// <summary>
        /// 포획 확률에 <b>마지막으로</b> 곱하는 배율(유예 안이면 1). 아이템·의상·미니게임 보너스와
        /// 등급별 최저 보장까지 다 더한 뒤에 곱한다 — 보너스로 레벨 격차를 메우지 못하게.
        /// </summary>
        public static float CaptureMultiplier(int trainerLevel, int insectLevel)
        {
            int over = Gap(trainerLevel, insectLevel) - GameConstants.TrainerLevel.CaptureGraceLevels;
            if (over <= 0) return 1f;
            return Mathf.Max(
                GameConstants.TrainerLevel.MinCaptureMultiplier,
                1f - over * GameConstants.TrainerLevel.CaptureDropPerLevel);
        }

        /// <summary>곤충 레벨 자체의 EXP 배율 — <c>1 + (Lv − 1) × ExpPerInsectLevel</c>.</summary>
        public static float ExpLevelFactor(int insectLevel)
        {
            return 1f + (Mathf.Max(1, insectLevel) - 1) * GameConstants.TrainerLevel.ExpPerInsectLevel;
        }

        /// <summary>
        /// 캐릭터와의 레벨 차 EXP 배율 — 높은 곤충은 +10%/Lv(최대 ×2), 낮은 곤충은 −10%/Lv(최저 ×0.2).
        /// </summary>
        public static float ExpGapFactor(int trainerLevel, int insectLevel)
        {
            int gap = Gap(trainerLevel, insectLevel);
            if (gap >= 0)
            {
                return 1f + Mathf.Min(gap, GameConstants.TrainerLevel.ExpHigherCapLevels)
                          * GameConstants.TrainerLevel.ExpHigherBonusPerLevel;
            }
            return Mathf.Max(
                GameConstants.TrainerLevel.ExpLowerMinFactor,
                1f + gap * GameConstants.TrainerLevel.ExpLowerPenaltyPerLevel);
        }

        /// <summary>EXP 최종 레벨 배율 = 곤충 레벨 배율 × 레벨 차 배율.</summary>
        public static float ExpMultiplier(int trainerLevel, int insectLevel)
        {
            return ExpLevelFactor(insectLevel) * ExpGapFactor(trainerLevel, insectLevel);
        }
    }
}
