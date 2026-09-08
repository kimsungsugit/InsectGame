using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>주간 크기 대결 달성 등급.</summary>
    public enum ContestTier
    {
        None,
        Bronze,
        Silver,
        Gold,
    }

    /// <summary>
    /// 주간 크기 대결의 일정·대상 종·등급 판정. 전부 순수 함수라 씬 없이 테스트된다.
    ///
    /// 주차는 Unix 시각을 604800으로 나눈 값이다 — 타임존·서머타임에 흔들리지 않고,
    /// 모든 기기가 같은 주에 같은 종을 받는다(서버 없이 동기가 맞는 유일한 방법).
    /// <see cref="GameClock"/>은 게임 내 낮/밤 사이클이라 여기와 무관하다.
    /// </summary>
    public static class WeeklyContestSchedule
    {
        public const long SecondsPerWeek = 604800L;

        // 종 기준 몸길이 대비 배율 임계. 개체 롤 범위가 0.75~1.25이므로
        // 금(1.22)은 롤 94 이상이라 "노리고 여러 마리 잡아야" 나온다.
        public const float BronzeRatio = 1.05f;
        public const float SilverRatio = 1.15f;
        public const float GoldRatio = 1.22f;

        /// <summary>Unix 초 → 주차. 음수(1970 이전)는 0으로 눕힌다.</summary>
        public static int WeekIndex(long unixSeconds)
        {
            if (unixSeconds <= 0L) return 0;
            return (int)(unixSeconds / SecondsPerWeek);
        }

        /// <summary>해당 주차가 시작한 Unix 초.</summary>
        public static long WeekStartUnix(int weekIndex)
        {
            return Mathf.Max(0, weekIndex) * SecondsPerWeek;
        }

        /// <summary>이 시각이 해당 주차 안에 드는가. 0(구세이브 미상)은 항상 false.</summary>
        public static bool IsWithinWeek(long unixSeconds, int weekIndex)
        {
            if (unixSeconds <= 0L) return false;
            long start = WeekStartUnix(weekIndex);
            return unixSeconds >= start && unixSeconds < start + SecondsPerWeek;
        }

        /// <summary>
        /// 대결 대상이 될 수 있는 종 — <b>Uncommon 이하</b>만. 도감을 채우고 나면 쓸모가
        /// 사라지는 저레어 곤충에게 다시 잡을 이유를 주는 게 이 대결의 목적이다.
        /// insectId 사전순으로 정렬해 기기·재설치와 무관하게 같은 순서를 보장한다.
        /// </summary>
        public static List<InsectData> BuildPool(IReadOnlyList<InsectData> allInsects)
        {
            List<InsectData> pool = new List<InsectData>();
            if (allInsects == null) return pool;

            for (int i = 0; i < allInsects.Count; i++)
            {
                InsectData data = allInsects[i];
                if (data == null || string.IsNullOrEmpty(data.insectId)) continue;
                if (data.rarity > InsectRarity.Uncommon) continue;
                pool.Add(data);
            }

            pool.Sort((a, b) => string.CompareOrdinal(a.insectId, b.insectId));
            return pool;
        }

        /// <summary>주차에 해당하는 대상 종. 풀이 비면 null.</summary>
        public static InsectData TargetFor(int weekIndex, IReadOnlyList<InsectData> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            int index = Mathf.Abs(weekIndex) % pool.Count;
            return pool[index];
        }

        /// <summary>종 기준 대비 몸길이 비율 → 등급.</summary>
        public static ContestTier TierForRatio(float sizeRatio)
        {
            if (sizeRatio >= GoldRatio) return ContestTier.Gold;
            if (sizeRatio >= SilverRatio) return ContestTier.Silver;
            if (sizeRatio >= BronzeRatio) return ContestTier.Bronze;
            return ContestTier.None;
        }

        /// <summary>등급 도달에 필요한 몸길이(mm). UI가 "앞으로 몇 mm"를 보여줄 때 쓴다.</summary>
        public static float RequiredMm(InsectData species, ContestTier tier)
        {
            if (species == null) return 0f;
            switch (tier)
            {
                case ContestTier.Bronze: return species.baseSizeMm * BronzeRatio;
                case ContestTier.Silver: return species.baseSizeMm * SilverRatio;
                case ContestTier.Gold: return species.baseSizeMm * GoldRatio;
                default: return 0f;
            }
        }

        public static string TierLabel(ContestTier tier)
        {
            switch (tier)
            {
                case ContestTier.Bronze: return "동";
                case ContestTier.Silver: return "은";
                case ContestTier.Gold: return "금";
                default: return "미달성";
            }
        }
    }
}
