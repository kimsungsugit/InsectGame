using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>놓인 물건이 섬에 주는 효과의 합.</summary>
    public struct IslandEffects
    {
        public int comfort;
        public float yieldBonus;
        public float capHours;
        public float bondSpeed;
    }

    /// <summary>
    /// 섬 생산의 <b>순수</b> 공식 — 계수는 <see cref="GameConstants.Island"/>. 근거는 rules/balance.md 「섬 기준점」.
    ///
    /// 캔디/h = 0.25 × 섬 등급 배율 × (1 + 쾌적도 보너스 + 설비 보너스) × (1 + 친밀도 보너스)
    /// </summary>
    public static class IslandYield
    {
        /// <summary>
        /// 섬 전용 등급 배율. <b>보상 배율(C1.0~L2.8)을 그대로 쓰지 않는다</b> — 그 표를 쓰면 전설 10마리를 푼
        /// 섬이 하루 300캔디를 넘겨 평소 활동 수입의 두 배가 된다. 섬은 곁들이는 수입이라 폭을 눌러 둔다.
        /// </summary>
        public static float RarityFactor(InsectRarity rarity)
        {
            switch (rarity)
            {
                case InsectRarity.Uncommon: return 1.1f;
                case InsectRarity.Rare: return 1.25f;
                case InsectRarity.Epic: return 1.4f;
                case InsectRarity.Legendary: return 1.6f;
                default: return 1f;
            }
        }

        /// <summary>
        /// 놓인 물건의 효과를 모은다.
        /// 쾌적도는 같은 물건 <b>첫 개는 온전히, 둘째는 절반, 셋째부터는 0</b> — 종류를 늘려야 오른다.
        /// 설비 효과는 같은 id가 몇 개든 <b>한 번만</b> 센다.
        /// </summary>
        public static IslandEffects CollectEffects(IReadOnlyList<IslandPlacedRecord> placed)
        {
            var effects = new IslandEffects();
            if (placed == null) return effects;

            for (int i = 0; i < placed.Count; i++)
            {
                IslandPlacedRecord p = placed[i];
                if (p == null) continue;
                IslandObjectDef def = IslandCatalog.Get(p.id);
                if (def == null) continue;

                // 목록이 수백 개를 넘지 않아 앞쪽 재탐색으로 센다 — 사전을 만들면 호출마다 할당이 생긴다.
                int earlier = 0;
                for (int j = 0; j < i; j++)
                    if (placed[j] != null && placed[j].id == p.id) earlier++;

                if (earlier == 0) effects.comfort += def.comfort;
                else if (earlier == 1) effects.comfort += def.comfort / 2;

                if (earlier > 0) continue;
                switch (def.effect)
                {
                    case IslandEffectKind.YieldBonus: effects.yieldBonus += def.effectValue; break;
                    case IslandEffectKind.CapHours: effects.capHours += def.effectValue; break;
                    case IslandEffectKind.BondSpeed: effects.bondSpeed += def.effectValue; break;
                }
            }
            return effects;
        }

        public static float ComfortBonus(int comfort)
        {
            float ratio = Mathf.Clamp01(comfort / (float)GameConstants.Island.ComfortForMaxBonus);
            return ratio * GameConstants.Island.MaxComfortBonus;
        }

        public static float CapHours(IslandEffects effects)
        {
            return Mathf.Min(GameConstants.Island.MaxCapHours,
                GameConstants.Island.BaseCapHours + Mathf.Max(0f, effects.capHours));
        }

        /// <summary>그 하트 수에 필요한 누적 방목 시간 — 6·18·36·60·90.</summary>
        public static float BondHoursForLevel(int level)
        {
            int l = Mathf.Clamp(level, 0, GameConstants.Island.MaxBondLevel);
            return GameConstants.Island.BondHoursUnit * l * (l + 1) * 0.5f;
        }

        public static int BondLevel(float hours)
        {
            int level = 0;
            for (int l = 1; l <= GameConstants.Island.MaxBondLevel; l++)
                if (hours >= BondHoursForLevel(l)) level = l;
            return level;
        }

        public static float BondBonus(int bondLevel)
        {
            return Mathf.Clamp(bondLevel, 0, GameConstants.Island.MaxBondLevel)
                   * GameConstants.Island.BondBonusPerLevel;
        }

        /// <summary>섬 전체에 걸리는 배율(쾌적도 + 설비). 곤충별 친밀도 배율은 따로 곱한다.</summary>
        public static float IslandMultiplier(IslandEffects effects)
        {
            return 1f + ComfortBonus(effects.comfort) + Mathf.Max(0f, effects.yieldBonus);
        }

        public static float CandyPerHour(InsectRarity rarity, int bondLevel, IslandEffects effects)
        {
            return GameConstants.Island.CandyPerInsectHour * RarityFactor(rarity)
                   * IslandMultiplier(effects) * (1f + BondBonus(bondLevel));
        }

        public static float CoinPerHour(int bondLevel, IslandEffects effects)
        {
            return GameConstants.Island.CoinPerInsectHour
                   * IslandMultiplier(effects) * (1f + BondBonus(bondLevel));
        }

        /// <summary>
        /// 이번 정산에서 쳐 줄 시간. 상한(<paramref name="capHours"/>)에서 이미 쌓인 만큼을 뺀 범위 안에서만 준다.
        ///
        /// <paramref name="nextSettleUnix"/>는 저장할 새 기준 시각이다.
        /// - 처음(<paramref name="lastSettleUnix"/> ≤ 0)이면 기준점만 잡고 0을 준다.
        /// - 시계가 뒤로 갔으면 0을 주고 <b>기준 시각을 당기지 않는다</b>(앞뒤로 흔들어 상한분을 반복해 받는 걸 막는다).
        ///   다만 <see cref="GameConstants.Island.ClockRollbackResetSeconds"/>를 넘게 되돌아갔다면 시계를 바로잡은
        ///   것으로 보고 지금으로 당긴다 — 안 그러면 한 번 미래로 간 섬이 그 시각까지 영영 멈춘다.
        /// </summary>
        public static float SettleHours(long lastSettleUnix, long nowUnix, float accruedHours, float capHours,
            out long nextSettleUnix)
        {
            if (lastSettleUnix <= 0)
            {
                nextSettleUnix = nowUnix;
                return 0f;
            }

            if (nowUnix < lastSettleUnix)
            {
                nextSettleUnix = lastSettleUnix - nowUnix > GameConstants.Island.ClockRollbackResetSeconds
                    ? nowUnix
                    : lastSettleUnix;
                return 0f;
            }

            nextSettleUnix = nowUnix;
            float elapsed = (nowUnix - lastSettleUnix) / 3600f;
            float room = Mathf.Max(0f, capHours - Mathf.Max(0f, accruedHours));
            return Mathf.Clamp(elapsed, 0f, room);
        }
    }
}
