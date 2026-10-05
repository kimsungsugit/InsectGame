#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// <see cref="IslandYield"/> — 섬 생산·누적 상한·친밀도.
    /// 섬은 곁들이는 수입이어야 한다: 대역 검사가 "섬만 돌려도 되는 게임"으로 가는 수치 변경을 잡는다.
    /// </summary>
    [TestFixture]
    public class IslandYieldTests
    {
        /// <summary>economy_sim의 평소 활동 수입(포획 20 · 전투 10 · 레이드 주 3회) — 하루 약 156캔디.</summary>
        private const float DailyActiveCandy = 156f;

        private static IslandPlacedRecord Placed(string id, int x = 0, int z = 0)
            => new IslandPlacedRecord { id = id, x = x, z = z };

        [Test]
        public void RarityFactor_IsCompressed_ComparedToRewardMultiplier()
        {
            // 보상 배율(전설 2.8)을 그대로 쓰면 전설 10마리 섬이 활동 수입의 두 배가 된다.
            Assert.AreEqual(1f, IslandYield.RarityFactor(InsectRarity.Common), 1e-4f);
            Assert.Less(IslandYield.RarityFactor(InsectRarity.Legendary),
                InsectRewardCalculator.GetRarityMultiplier(InsectRarity.Legendary));
            Assert.Greater(IslandYield.RarityFactor(InsectRarity.Legendary), IslandYield.RarityFactor(InsectRarity.Epic));
            Assert.Greater(IslandYield.RarityFactor(InsectRarity.Epic), IslandYield.RarityFactor(InsectRarity.Rare));
            Assert.Greater(IslandYield.RarityFactor(InsectRarity.Rare), IslandYield.RarityFactor(InsectRarity.Uncommon));
            Assert.Greater(IslandYield.RarityFactor(InsectRarity.Uncommon), IslandYield.RarityFactor(InsectRarity.Common));
        }

        [Test]
        public void BasicIsland_DailyCandy_IsSmallShareOfActiveIncome()
        {
            // 기본 섬 = 일반 3마리, 물건 없음, 친밀도 0.
            float daily = 3f * IslandYield.CandyPerHour(InsectRarity.Common, 0, new IslandEffects()) * 24f;
            Assert.AreEqual(18f, daily, 0.01f);
            Assert.Less(daily / DailyActiveCandy, 0.2f, "기본 섬이 활동 수입의 20%를 넘는다");
        }

        [Test]
        public void FullyBuiltIsland_DailyCandy_DoesNotExceedActiveIncomeByMuch()
        {
            // 최대치: 전설 10마리 · 쾌적도 상한 · 생산 설비 전부 · 친밀도 5.
            var placed = new List<IslandPlacedRecord>();
            foreach (IslandObjectDef def in IslandCatalog.All)
                if (def.effect == IslandEffectKind.YieldBonus) placed.Add(Placed(def.id));
            IslandEffects effects = IslandYield.CollectEffects(placed);
            effects.comfort = GameConstants.Island.ComfortForMaxBonus * 2;

            float daily = GameConstants.Island.MaxInsectSlots
                          * IslandYield.CandyPerHour(InsectRarity.Legendary, GameConstants.Island.MaxBondLevel, effects)
                          * 24f;
            Assert.Less(daily, DailyActiveCandy * 1.25f,
                $"풀 확장 섬이 하루 {daily:0}캔디 — 활동 수입({DailyActiveCandy})의 1.25배를 넘는다");
            Assert.Greater(daily, DailyActiveCandy * 0.6f, "풀 확장 섬이 너무 짜다 — 확장할 이유가 없다");
        }

        [Test]
        public void CollectEffects_SameToolTwice_CountsOnce()
        {
            var one = IslandYield.CollectEffects(new List<IslandPlacedRecord> { Placed("o_feeder") });
            var two = IslandYield.CollectEffects(new List<IslandPlacedRecord> { Placed("o_feeder"), Placed("o_feeder", 2) });
            Assert.AreEqual(0.05f, one.yieldBonus, 1e-4f);
            Assert.AreEqual(one.yieldBonus, two.yieldBonus, 1e-4f, "먹이통을 여러 개 놓으면 효과가 쌓인다");
        }

        [Test]
        public void CollectEffects_DifferentTools_Stack()
        {
            var e = IslandYield.CollectEffects(new List<IslandPlacedRecord> { Placed("o_feeder"), Placed("o_water", 2) });
            Assert.AreEqual(0.10f, e.yieldBonus, 1e-4f);
        }

        [Test]
        public void CollectEffects_Comfort_FullThenHalfThenNothing()
        {
            int one = IslandCatalog.Get("f_bench").comfort;
            Assert.AreEqual(one, IslandYield.CollectEffects(new List<IslandPlacedRecord> { Placed("f_bench") }).comfort);
            Assert.AreEqual(one + one / 2, IslandYield.CollectEffects(
                new List<IslandPlacedRecord> { Placed("f_bench"), Placed("f_bench", 3) }).comfort);
            Assert.AreEqual(one + one / 2, IslandYield.CollectEffects(
                new List<IslandPlacedRecord> { Placed("f_bench"), Placed("f_bench", 3), Placed("f_bench", 0, 3) }).comfort,
                "같은 물건 셋째부터는 쾌적도를 주지 않는다");
        }

        [Test]
        public void CollectEffects_UnknownIds_AreIgnored()
        {
            var e = IslandYield.CollectEffects(new List<IslandPlacedRecord> { Placed("x_from_future"), null });
            Assert.AreEqual(0, e.comfort);
            Assert.AreEqual(0f, e.yieldBonus);
        }

        [Test]
        public void ComfortBonus_IsCappedAtMax()
        {
            Assert.AreEqual(0f, IslandYield.ComfortBonus(0), 1e-4f);
            Assert.AreEqual(GameConstants.Island.MaxComfortBonus * 0.5f,
                IslandYield.ComfortBonus(GameConstants.Island.ComfortForMaxBonus / 2), 1e-4f);
            Assert.AreEqual(GameConstants.Island.MaxComfortBonus,
                IslandYield.ComfortBonus(GameConstants.Island.ComfortForMaxBonus * 10), 1e-4f);
        }

        [Test]
        public void CapHours_BaseAndMax()
        {
            Assert.AreEqual(GameConstants.Island.BaseCapHours, IslandYield.CapHours(new IslandEffects()), 1e-4f);
            Assert.AreEqual(GameConstants.Island.MaxCapHours,
                IslandYield.CapHours(new IslandEffects { capHours = 999f }), 1e-4f);
        }

        [Test]
        public void CapHours_AllCatalogCapObjects_StayWithinMax()
        {
            var placed = new List<IslandPlacedRecord>();
            foreach (IslandObjectDef def in IslandCatalog.All)
                if (def.effect == IslandEffectKind.CapHours) placed.Add(Placed(def.id));
            float cap = IslandYield.CapHours(IslandYield.CollectEffects(placed));
            Assert.Greater(cap, GameConstants.Island.BaseCapHours);
            Assert.LessOrEqual(cap, GameConstants.Island.MaxCapHours);
        }

        [TestCase(0f, 0)]
        [TestCase(5.9f, 0)]
        [TestCase(6f, 1)]
        [TestCase(18f, 2)]
        [TestCase(36f, 3)]
        [TestCase(60f, 4)]
        [TestCase(89.9f, 4)]
        [TestCase(90f, 5)]
        [TestCase(9999f, 5)]
        public void BondLevel_Thresholds(float hours, int expected)
        {
            Assert.AreEqual(expected, IslandYield.BondLevel(hours));
        }

        [Test]
        public void BondBonus_MaxLevel_IsTwentyPercent()
        {
            Assert.AreEqual(0.20f, IslandYield.BondBonus(GameConstants.Island.MaxBondLevel), 1e-4f);
            Assert.AreEqual(0.20f, IslandYield.BondBonus(99), 1e-4f);
        }

        // ── 정산 ──

        private const long T0 = 1_760_000_000L;

        [Test]
        public void SettleHours_FirstEver_OnlySetsBaseline()
        {
            float dt = IslandYield.SettleHours(0, T0, 0f, 8f, out long next);
            Assert.AreEqual(0f, dt);
            Assert.AreEqual(T0, next);
        }

        [Test]
        public void SettleHours_NormalElapsed_GivesElapsedHours()
        {
            float dt = IslandYield.SettleHours(T0, T0 + 3 * 3600, 0f, 8f, out long next);
            Assert.AreEqual(3f, dt, 1e-3f);
            Assert.AreEqual(T0 + 3 * 3600, next);
        }

        [Test]
        public void SettleHours_BeyondCap_IsClampedToRemainingRoom()
        {
            Assert.AreEqual(8f, IslandYield.SettleHours(T0, T0 + 100 * 3600, 0f, 8f, out _), 1e-3f);
            Assert.AreEqual(3f, IslandYield.SettleHours(T0, T0 + 100 * 3600, 5f, 8f, out _), 1e-3f);
            Assert.AreEqual(0f, IslandYield.SettleHours(T0, T0 + 100 * 3600, 8f, 8f, out _), 1e-3f);
            // 설비를 넣어 상한이 줄었어도(쌓인 시간이 상한보다 큼) 음수가 나오지 않는다.
            Assert.AreEqual(0f, IslandYield.SettleHours(T0, T0 + 100 * 3600, 12f, 8f, out _), 1e-3f);
        }

        [Test]
        public void SettleHours_ClockMovedBackSlightly_GivesNothingAndKeepsBaseline()
        {
            // 시계를 앞뒤로 흔들어 상한분을 반복해 받는 걸 막는다 — 기준 시각을 당기지 않는다.
            float dt = IslandYield.SettleHours(T0, T0 - 3600, 0f, 8f, out long next);
            Assert.AreEqual(0f, dt);
            Assert.AreEqual(T0, next);
        }

        [Test]
        public void SettleHours_ClockMovedBackFar_ResetsBaseline()
        {
            // 한 번 먼 미래로 갔던 섬이 그 시각까지 영영 멈추지 않게.
            long now = T0 - GameConstants.Island.ClockRollbackResetSeconds - 10;
            float dt = IslandYield.SettleHours(T0, now, 0f, 8f, out long next);
            Assert.AreEqual(0f, dt);
            Assert.AreEqual(now, next);
        }
    }
}
#endif
