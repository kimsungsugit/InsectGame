using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>훈련소 「성장 훈련」이 올리는 능력치. 개체값(0~15) 하나에 대응한다.</summary>
    public enum GrowthStat { Hp, Attack, Defense }

    /// <summary>
    /// 훈련 비용의 <b>단일 출처</b> — 기술 습득(회당 캔디·필요 횟수)과 능력치 훈련(개체값 +1).
    /// 계수는 <see cref="GameConstants.Training"/>, 실측 근거는 <c>rules/balance.md</c> 「훈련 기준점」.
    ///
    /// 예전엔 기술 가격이 <c>power</c>에서만 나왔다. 버프·약화·회복·기절·방어 기술은 위력 칸이 1이라
    /// 공식이 "가장 약한 기술"로 읽었고, 광폭화(공격 +60%, Lv26)가 <b>26캔디 1회</b>로 끝날 때 파멸의 독침은
    /// 216캔디였다. 종족기·전용기는 아예 손으로 적은 상수(4·8·12·14·22·35·50)였다. 지금은 효과를 위력
    /// 눈금으로 환산한 <see cref="SkillValue"/> 하나에서 모든 가격이 나온다 — 피해기는 옛 값과 같다.
    /// </summary>
    public static class TrainingPricing
    {
        /// <summary>
        /// 기술의 가치를 <b>위력 눈금</b>으로. 피해기는 기대 피해(위력 × 명중), 상태기는 효과 크기 × 지속을
        /// 기준 위력으로 환산한다(<see cref="GameConstants.Training"/>).
        /// </summary>
        public static float SkillValue(InsectSkill skill)
        {
            if (skill == null) return 0f;
            float accuracy = Mathf.Clamp01(skill.accuracy);
            int turns = Mathf.Max(1, skill.effectDurationTurns);

            switch (skill.effectType)
            {
                case SkillEffectType.BuffAttack:
                case SkillEffectType.DebuffAttack:
                case SkillEffectType.DefenseBuff:
                    return Mathf.Abs(skill.effectValue) * turns * GameConstants.Training.StatusRefPower;
                case SkillEffectType.Heal:
                    return Mathf.Clamp01(skill.effectValue) * GameConstants.Training.HealRefPower;
                case SkillEffectType.PoisonDot:
                    // 턴당 피해(power) × 지속 — 방어를 무시하지만 한 번에 들어가지 않는다. 둘이 상쇄한다고 본다.
                    return skill.power * turns * accuracy;
                case SkillEffectType.Stun:
                    return GameConstants.Training.StunRefPower * accuracy;
                default:
                    return skill.power * accuracy;
            }
        }

        /// <summary>
        /// 누적 훈련 필요 횟수 — <c>1 + 가치 / 12</c>, 1~5. 피해기(명중 1)는 옛 <c>1 + power / 12</c>와 같다.
        /// 기술 디스크의 "1회"는 방식의 규칙이라 호출부(<c>TrainingManager.GetRequiredSessions</c>)가 가른다.
        /// </summary>
        public static int RequiredSessions(InsectSkill skill)
        {
            if (skill == null) return 1;
            return Mathf.Clamp(1 + ValuePoints(skill) / GameConstants.Training.SessionValueStep, 1, GameConstants.Training.MaxSessions);
        }

        /// <summary>
        /// 훈련 1회분 캔디 — <c>가치 / 2 + 해금 레벨</c>, 최소 5. 해금 레벨은 범용기면 <c>requiredLevel</c>,
        /// 종족기면 learnset의 <c>learnLevel</c>이다(같은 가치라도 늦게 열리는 기술이 비싸다).
        /// 부트스트랩이 기술을 만들 때 이 값을 <c>InsectSkill.trainingCost</c>에 굽고, 결제는 그 값을 읽는다.
        /// </summary>
        public static int SessionCost(InsectSkill skill, int unlockLevel)
        {
            return Mathf.Max(GameConstants.Training.MinSessionCost, ValuePoints(skill) / 2 + Mathf.Max(1, unlockLevel));
        }

        /// <summary>
        /// 가치의 정수 눈금. 작은 여유(0.001)를 더해 내린다 — <c>0.35f × 2 × 30</c>이 부동소수로 20.9999…가 되어
        /// 21이 아니라 20으로 떨어지는 식의 한 칸 오차를 막는다. 피해기(정수 위력 × 명중 1)는 그대로다.
        /// </summary>
        private static int ValuePoints(InsectSkill skill)
        {
            return Mathf.FloorToInt(SkillValue(skill) + 0.001f);
        }

        /// <summary>습득까지 드는 총 캔디(회당 × 횟수). 표시·밸런스 검산용.</summary>
        public static int TotalSkillCost(InsectSkill skill, int unlockLevel)
        {
            return SessionCost(skill, unlockLevel) * RequiredSessions(skill);
        }

        /// <summary>
        /// 능력치(개체값) <paramref name="currentIv"/> → +1 비용. 이미 최대(15)면 0.
        /// <c>20 × 1.2^현재값 × 등급 배율</c> — 올릴수록 가파르다(0→1은 20, 14→15는 257, 전설은 ×2.8).
        /// </summary>
        public static int StatCost(int currentIv, InsectRarity rarity)
        {
            if (currentIv >= PlayerInsectData.MaxIV) return 0;
            float cost = GameConstants.Training.StatBaseCost
                * Mathf.Pow(GameConstants.Training.StatCostGrowth, Mathf.Max(0, currentIv))
                * InsectRewardCalculator.GetRarityMultiplier(rarity);
            return Mathf.Max(1, Mathf.RoundToInt(cost));
        }

        /// <summary>한 능력치를 <paramref name="fromIv"/>에서 <paramref name="toIv"/>까지 올리는 총 캔디.</summary>
        public static int StatCostRange(int fromIv, int toIv, InsectRarity rarity)
        {
            int total = 0;
            int end = Mathf.Min(toIv, PlayerInsectData.MaxIV);
            for (int v = Mathf.Max(0, fromIv); v < end; v++)
                total += StatCost(v, rarity);
            return total;
        }
    }
}
