#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 주간 크기 대결의 일정·대상 종·등급 판정.
    /// 서버가 없으므로 "모든 기기가 같은 주에 같은 종을 받는다"가 순수 계산으로만 보장된다 —
    /// 여기가 흔들리면 플레이어마다 다른 과제를 받는다.
    /// </summary>
    [TestFixture]
    public class WeeklyContestScheduleTests
    {
        private readonly List<InsectData> created = new List<InsectData>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        private InsectData Species(string id, InsectRarity rarity, float baseMm = 30f)
        {
            InsectData data = ScriptableObject.CreateInstance<InsectData>();
            data.insectId = id;
            data.displayName = id;
            data.rarity = rarity;
            data.baseSizeMm = baseMm;
            data.baseWeightG = 2f;
            created.Add(data);
            return data;
        }

        [Test]
        public void WeekIndex_AtWeekBoundary_Increments()
        {
            long week = WeeklyContestSchedule.SecondsPerWeek;
            Assert.AreEqual(0, WeeklyContestSchedule.WeekIndex(week - 1));
            Assert.AreEqual(1, WeeklyContestSchedule.WeekIndex(week));
            Assert.AreEqual(1, WeeklyContestSchedule.WeekIndex(week * 2 - 1));
            Assert.AreEqual(2, WeeklyContestSchedule.WeekIndex(week * 2));
        }

        [Test]
        public void WeekIndex_NonPositive_IsZero()
        {
            Assert.AreEqual(0, WeeklyContestSchedule.WeekIndex(0));
            Assert.AreEqual(0, WeeklyContestSchedule.WeekIndex(-12345));
        }

        [Test]
        public void IsWithinWeek_BoundariesAreHalfOpen()
        {
            long start = WeeklyContestSchedule.WeekStartUnix(3);
            Assert.IsTrue(WeeklyContestSchedule.IsWithinWeek(start, 3));
            Assert.IsTrue(WeeklyContestSchedule.IsWithinWeek(start + WeeklyContestSchedule.SecondsPerWeek - 1, 3));
            Assert.IsFalse(WeeklyContestSchedule.IsWithinWeek(start + WeeklyContestSchedule.SecondsPerWeek, 3));
            Assert.IsFalse(WeeklyContestSchedule.IsWithinWeek(start - 1, 3));
        }

        [Test]
        public void IsWithinWeek_LegacyZeroTimestamp_IsNeverCounted()
        {
            // ★ 구세이브의 capturedUnix = 0이 1970년 첫 주로 잡히면 안 된다.
            Assert.IsFalse(WeeklyContestSchedule.IsWithinWeek(0, 0));
        }

        [Test]
        public void BuildPool_ExcludesRareAndAbove()
        {
            List<InsectData> all = new List<InsectData>
            {
                Species("c_ant", InsectRarity.Common),
                Species("u_bee", InsectRarity.Uncommon),
                Species("r_stag", InsectRarity.Rare),
                Species("e_moth", InsectRarity.Epic),
                Species("l_dragon", InsectRarity.Legendary),
            };

            List<InsectData> pool = WeeklyContestSchedule.BuildPool(all);

            Assert.AreEqual(2, pool.Count);
            CollectionAssert.AreEqual(new[] { "c_ant", "u_bee" }, pool.ConvertAll(p => p.insectId));
        }

        [Test]
        public void BuildPool_SortsByIdSoOrderIsDeviceIndependent()
        {
            List<InsectData> shuffled = new List<InsectData>
            {
                Species("zeta", InsectRarity.Common),
                Species("alpha", InsectRarity.Uncommon),
                Species("mid", InsectRarity.Common),
            };

            List<InsectData> pool = WeeklyContestSchedule.BuildPool(shuffled);

            CollectionAssert.AreEqual(new[] { "alpha", "mid", "zeta" }, pool.ConvertAll(p => p.insectId));
        }

        [Test]
        public void BuildPool_NullOrEmptyInput_ReturnsEmpty()
        {
            Assert.AreEqual(0, WeeklyContestSchedule.BuildPool(null).Count);
            Assert.AreEqual(0, WeeklyContestSchedule.BuildPool(new List<InsectData>()).Count);
            // 항목 자체가 null이거나 id가 비면 걸러진다.
            Assert.AreEqual(0, WeeklyContestSchedule.BuildPool(
                new List<InsectData> { null, Species(string.Empty, InsectRarity.Common) }).Count);
        }

        [Test]
        public void TargetFor_SameWeek_AlwaysSameSpecies()
        {
            List<InsectData> pool = WeeklyContestSchedule.BuildPool(new List<InsectData>
            {
                Species("a", InsectRarity.Common),
                Species("b", InsectRarity.Common),
                Species("c", InsectRarity.Uncommon),
            });

            Assert.AreSame(
                WeeklyContestSchedule.TargetFor(7, pool),
                WeeklyContestSchedule.TargetFor(7, pool));
        }

        [Test]
        public void TargetFor_ConsecutiveWeeks_RotateThroughPool()
        {
            List<InsectData> pool = WeeklyContestSchedule.BuildPool(new List<InsectData>
            {
                Species("a", InsectRarity.Common),
                Species("b", InsectRarity.Common),
                Species("c", InsectRarity.Uncommon),
            });

            Assert.AreEqual("a", WeeklyContestSchedule.TargetFor(0, pool).insectId);
            Assert.AreEqual("b", WeeklyContestSchedule.TargetFor(1, pool).insectId);
            Assert.AreEqual("c", WeeklyContestSchedule.TargetFor(2, pool).insectId);
            Assert.AreEqual("a", WeeklyContestSchedule.TargetFor(3, pool).insectId, "한 바퀴 뒤 처음으로 돌아와야 한다");
        }

        [Test]
        public void TargetFor_EmptyPool_ReturnsNullWithoutThrowing()
        {
            Assert.IsNull(WeeklyContestSchedule.TargetFor(5, new List<InsectData>()));
            Assert.IsNull(WeeklyContestSchedule.TargetFor(5, null));
        }

        [Test]
        public void TierForRatio_Thresholds()
        {
            Assert.AreEqual(ContestTier.None, WeeklyContestSchedule.TierForRatio(1.04f));
            Assert.AreEqual(ContestTier.Bronze, WeeklyContestSchedule.TierForRatio(WeeklyContestSchedule.BronzeRatio));
            Assert.AreEqual(ContestTier.Bronze, WeeklyContestSchedule.TierForRatio(1.14f));
            Assert.AreEqual(ContestTier.Silver, WeeklyContestSchedule.TierForRatio(WeeklyContestSchedule.SilverRatio));
            Assert.AreEqual(ContestTier.Silver, WeeklyContestSchedule.TierForRatio(1.21f));
            Assert.AreEqual(ContestTier.Gold, WeeklyContestSchedule.TierForRatio(WeeklyContestSchedule.GoldRatio));
        }

        [Test]
        public void TierThresholds_AreReachableWithinRollRange()
        {
            // 금 임계가 최대 배율(1.25)을 넘으면 아무도 달성할 수 없다.
            Assert.Less(WeeklyContestSchedule.GoldRatio, InsectSizeCalculator.MaxScale,
                "금 등급이 도달 불가능하다");
            Assert.Greater(WeeklyContestSchedule.BronzeRatio, 1f,
                "동 등급이 평균 개체로 자동 달성되면 의미가 없다");
        }

        [Test]
        public void RequiredMm_ScalesWithSpeciesBase()
        {
            InsectData species = Species("stag", InsectRarity.Common, baseMm: 40f);

            Assert.AreEqual(40f * WeeklyContestSchedule.BronzeRatio,
                WeeklyContestSchedule.RequiredMm(species, ContestTier.Bronze), 0.0001f);
            Assert.AreEqual(40f * WeeklyContestSchedule.GoldRatio,
                WeeklyContestSchedule.RequiredMm(species, ContestTier.Gold), 0.0001f);
            Assert.AreEqual(0f, WeeklyContestSchedule.RequiredMm(species, ContestTier.None));
            Assert.AreEqual(0f, WeeklyContestSchedule.RequiredMm(null, ContestTier.Gold));
        }

        [Test]
        public void TierLabel_CoversEveryTier()
        {
            foreach (ContestTier tier in System.Enum.GetValues(typeof(ContestTier)))
                Assert.IsNotEmpty(WeeklyContestSchedule.TierLabel(tier), $"{tier} 라벨이 비었다");
        }
    }
}
#endif
