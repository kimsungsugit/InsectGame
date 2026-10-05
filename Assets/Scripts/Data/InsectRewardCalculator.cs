using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Data
{
    public static class InsectRewardCalculator
    {
        /// <summary>
        /// 등급만 본 <b>기준</b> EXP. 실제 지급은 레벨을 받는 오버로드를 쓴다 — 이것만 쓰면 Lv60 곤충도
        /// Lv1과 같은 EXP를 준다(2026-10-01 전까지 실제로 그랬다).
        /// </summary>
        public static int GetExpReward(InsectData data)
        {
            if (data == null)
            {
                return 0;
            }

            float multiplier = GetRarityMultiplier(data.rarity);
            int reward = (int)(data.expReward * multiplier);
            return reward < 0 ? 0 : reward;
        }

        /// <summary>
        /// 캐릭터가 실제로 받는 EXP — 등급 × 곤충 레벨 × 캐릭터와의 레벨 차(<see cref="TrainerLevelGap.ExpMultiplier"/>).
        /// 포획·전투·NPC 대결·레이드가 모두 이걸 부른다. <paramref name="trainerLevel"/>은 <b>지급 전</b> 레벨이다 —
        /// 지급 뒤에 읽으면 방금 오른 레벨로 차를 재서 표시값과 지급값이 갈린다.
        /// 아이템·의상 EXP 부스터는 호출부가 곱한다.
        /// </summary>
        public static int GetExpReward(InsectData data, int insectLevel, int trainerLevel)
        {
            if (data == null)
            {
                return 0;
            }

            float scaled = data.expReward * GetRarityMultiplier(data.rarity)
                           * TrainerLevelGap.ExpMultiplier(trainerLevel, insectLevel);
            return Mathf.Max(0, Mathf.RoundToInt(scaled));
        }

        public static int GetCandyReward(InsectData data)
        {
            if (data == null)
            {
                return 0;
            }

            float multiplier = GetRarityMultiplier(data.rarity);
            int reward = (int)(data.candyReward * multiplier);
            return reward < 0 ? 0 : reward;
        }

        public static int GetItemRewardCount(InsectData data)
        {
            if (data == null)
            {
                return 0;
            }

            float multiplier = GetRarityMultiplier(data.rarity);
            int reward = (int)(data.itemRewardCount * multiplier);
            return reward < 0 ? 0 : reward;
        }

        /// <summary>
        /// 등급 배율(보상·훈련 공용). 능력치 훈련 비용도 이 표를 곱한다 — 희귀할수록 잡을 때 캔디를
        /// 더 주는 만큼 완성하는 데도 더 든다. 표가 두 벌이면 한쪽만 고쳐져 어긋난다.
        /// </summary>
        public static float GetRarityMultiplier(InsectRarity rarity)
        {
            switch (rarity)
            {
                case InsectRarity.Common:
                    return 1f;
                case InsectRarity.Uncommon:
                    return 1.2f;
                case InsectRarity.Rare:
                    return 1.5f;
                case InsectRarity.Epic:
                    return 2.0f;
                case InsectRarity.Legendary:
                    return 2.8f;
                default:
                    return 1f;
            }
        }
    }
}
