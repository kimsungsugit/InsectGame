#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Data;
using InsectGame.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 필드 스폰 규칙(<see cref="FieldSpawnRules"/>) — <b>희귀도는 리전과 무관하고 레벨만 리전을 따른다.</b>
    ///
    /// 예전엔 리전 풀의 종 가중치가 곧 등급 분포라 초원은 희귀 이상 0%, 유적은 일반 0%·희귀 50%였다.
    /// 등급을 먼저 전역 표로 굴리고 종은 그 등급 안에서 고르는 순서가 무너지면 풀 구성이 다시 등급을 정하게 된다 —
    /// 그걸 여기서 고정한다. 난수는 0~1을 고르게 훑어 넣는다(결정적).
    /// </summary>
    [TestFixture]
    public class FieldSpawnRulesTests
    {
        private const int Sweep = 20000;
        private readonly List<InsectData> created = new List<InsectData>();

        [TearDown]
        public void TearDown()
        {
            foreach (InsectData d in created)
                if (d != null) Object.DestroyImmediate(d);
            created.Clear();
        }

        private InsectData Insect(string id, InsectRarity rarity, float weight)
        {
            InsectData d = ScriptableObject.CreateInstance<InsectData>();
            d.insectId = id;
            d.rarity = rarity;
            d.spawnWeight = weight;
            created.Add(d);
            return d;
        }

        private static bool[] Available(params InsectRarity[] rarities)
        {
            var a = new bool[FieldSpawnRules.RarityCount];
            foreach (InsectRarity r in rarities) a[(int)r] = true;
            return a;
        }

        private static readonly bool[] AllFive = Available(InsectRarity.Common, InsectRarity.Uncommon,
            InsectRarity.Rare, InsectRarity.Epic, InsectRarity.Legendary);

        private static float TableTotal()
        {
            float t = 0f;
            for (int r = 0; r < FieldSpawnRules.RarityCount; r++) t += FieldSpawnRules.BaseShare(r);
            return t;
        }

        /// <summary>풀에서 스포너와 같은 순서로 고른다 — 등급 먼저(전역 표), 그다음 그 등급 안에서 spawnWeight.</summary>
        private static float[] RarityHistogram(List<InsectData> pool, float rareBoost)
        {
            var available = new bool[FieldSpawnRules.RarityCount];
            foreach (InsectData d in pool) available[(int)d.rarity] = true;
            var counts = new float[FieldSpawnRules.RarityCount];
            var sub = new List<InsectData>();
            for (int i = 0; i < Sweep; i++)
            {
                float roll = (i + 0.5f) / Sweep;
                int rarity = FieldSpawnRules.PickRarity(roll, rareBoost, available);
                sub.Clear();
                foreach (InsectData d in pool) if ((int)d.rarity == rarity) sub.Add(d);
                // 종 고르기 난수는 등급 난수와 따로 — 같은 roll을 쓰면 서로 묶인다.
                InsectData chosen = InsectDatabase.PickWeighted(sub, Mathf.Repeat(roll * 7.31f, 1f));
                counts[(int)chosen.rarity] += 1f / Sweep;
            }
            return counts;
        }

        // ── 등급표 ──

        [Test]
        public void EffectiveShares_FullPool_EqualsNormalizedTable()
        {
            float[] shares = FieldSpawnRules.EffectiveShares(1f, AllFive);
            float total = TableTotal();
            float sum = 0f;
            for (int r = 0; r < FieldSpawnRules.RarityCount; r++)
            {
                Assert.AreEqual(FieldSpawnRules.BaseShare(r) / total, shares[r], 1e-6f, $"등급 {(InsectRarity)r}");
                sum += shares[r];
            }
            Assert.AreEqual(1f, sum, 1e-5f);
        }

        /// <summary>
        /// 급소 — 풀 구성이 정반대인 두 리전(일반 투성이 / 전설 투성이)이 <b>같은 등급 분포</b>를 낸다.
        /// 옛 규칙(풀 전체를 spawnWeight로 한 번에 굴림)이라면 두 번째 풀은 전설이 대부분이었다.
        /// </summary>
        [Test]
        public void RarityDistribution_IsIndependentOfPoolComposition()
        {
            var commonHeavy = new List<InsectData>();
            for (int i = 0; i < 10; i++) commonHeavy.Add(Insect("c" + i, InsectRarity.Common, 5f));
            commonHeavy.Add(Insect("u", InsectRarity.Uncommon, 0.5f));
            commonHeavy.Add(Insect("r", InsectRarity.Rare, 0.3f));
            commonHeavy.Add(Insect("e", InsectRarity.Epic, 0.1f));
            commonHeavy.Add(Insect("l", InsectRarity.Legendary, 0.05f));

            var legendHeavy = new List<InsectData>();
            legendHeavy.Add(Insect("c'", InsectRarity.Common, 0.1f));
            legendHeavy.Add(Insect("u'", InsectRarity.Uncommon, 0.1f));
            legendHeavy.Add(Insect("r'", InsectRarity.Rare, 0.1f));
            legendHeavy.Add(Insect("e'", InsectRarity.Epic, 0.1f));
            for (int i = 0; i < 10; i++) legendHeavy.Add(Insect("l" + i, InsectRarity.Legendary, 5f));

            float[] a = RarityHistogram(commonHeavy, 1f);
            float[] b = RarityHistogram(legendHeavy, 1f);
            float total = TableTotal();
            for (int r = 0; r < FieldSpawnRules.RarityCount; r++)
            {
                float expect = FieldSpawnRules.BaseShare(r) / total;
                Assert.AreEqual(expect, a[r], 0.002f, $"일반 투성이 풀의 {(InsectRarity)r}");
                Assert.AreEqual(expect, b[r], 0.002f, $"전설 투성이 풀의 {(InsectRarity)r}");
            }
        }

        [Test]
        public void SpeciesWithinRarity_FollowsSpawnWeight()
        {
            // 같은 등급 안에서는 여전히 spawnWeight가 종을 정한다(3:1).
            var pool = new List<InsectData> { Insect("heavy", InsectRarity.Common, 3f), Insect("light", InsectRarity.Common, 1f) };
            int heavy = 0;
            for (int i = 0; i < Sweep; i++)
                if (InsectDatabase.PickWeighted(pool, (i + 0.5f) / Sweep).insectId == "heavy") heavy++;
            Assert.AreEqual(0.75f, heavy / (float)Sweep, 0.001f);
        }

        // ── 대체 ──

        [Test]
        public void Fallback_MissingRarity_PrefersNearestLowerThenUpper()
        {
            bool[] commonUncommon = Available(InsectRarity.Common, InsectRarity.Uncommon);
            Assert.AreEqual((int)InsectRarity.Uncommon, FieldSpawnRules.Fallback((int)InsectRarity.Legendary, commonUncommon),
                "전설은 가장 가까운 아래(고급)로 내려와야 한다 — 위로 올려 채우면 희귀가 흔해진다");
            Assert.AreEqual((int)InsectRarity.Uncommon, FieldSpawnRules.Fallback((int)InsectRarity.Rare, commonUncommon));

            bool[] noCommon = Available(InsectRarity.Uncommon, InsectRarity.Rare);
            Assert.AreEqual((int)InsectRarity.Uncommon, FieldSpawnRules.Fallback((int)InsectRarity.Common, noCommon),
                "아래가 통째로 없을 때만 위로 간다");

            bool[] gap = Available(InsectRarity.Common, InsectRarity.Legendary);
            Assert.AreEqual((int)InsectRarity.Common, FieldSpawnRules.Fallback((int)InsectRarity.Epic, gap),
                "위에 있어도 아래가 있으면 아래가 먼저다");
            Assert.AreEqual((int)InsectRarity.Rare, FieldSpawnRules.Fallback((int)InsectRarity.Rare, AllFive));
        }

        [Test]
        public void Fallback_NothingAvailable_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, FieldSpawnRules.Fallback(2, new bool[FieldSpawnRules.RarityCount]));
            Assert.AreEqual(-1, FieldSpawnRules.PickRarity(0.5f, 1f, new bool[FieldSpawnRules.RarityCount]));
        }

        [Test]
        public void EffectiveShares_MissingRarities_MoveShareDownAndStillSumToOne()
        {
            float[] s = FieldSpawnRules.EffectiveShares(1f, Available(InsectRarity.Common, InsectRarity.Uncommon));
            float total = TableTotal();
            Assert.AreEqual(FieldSpawnRules.CommonShare / total, s[(int)InsectRarity.Common], 1e-6f, "일반 몫은 그대로");
            Assert.AreEqual(1f - FieldSpawnRules.CommonShare / total, s[(int)InsectRarity.Uncommon], 1e-5f,
                "희귀·영웅·전설 몫이 전부 고급으로 내려온다");
            Assert.AreEqual(0f, s[(int)InsectRarity.Rare] + s[(int)InsectRarity.Epic] + s[(int)InsectRarity.Legendary], 1e-6f);
        }

        // ── 레어 부스트 ──

        [Test]
        public void RareBoost_MultipliesRarePlusThenRenormalizes()
        {
            float[] plain = FieldSpawnRules.EffectiveShares(1f, AllFive);
            float[] boosted = FieldSpawnRules.EffectiveShares(2f, AllFive);

            float sum = 0f;
            foreach (float v in boosted) sum += v;
            Assert.AreEqual(1f, sum, 1e-5f, "재정규화 안 됨");

            // 희귀 이상은 늘고 일반·고급은 준다. 같은 무리 안의 비율은 그대로다.
            for (int r = (int)InsectRarity.Rare; r < FieldSpawnRules.RarityCount; r++)
                Assert.Greater(boosted[r], plain[r], $"{(InsectRarity)r}이 안 늘었다");
            Assert.Less(boosted[(int)InsectRarity.Common], plain[(int)InsectRarity.Common]);
            Assert.AreEqual(plain[0] / plain[1], boosted[0] / boosted[1], 1e-4f, "일반:고급 비가 바뀌었다");
            Assert.AreEqual(plain[2] / plain[4], boosted[2] / boosted[4], 1e-4f, "희귀:전설 비가 바뀌었다");
        }

        [Test]
        public void RareBoost_AtOrBelowOne_ChangesNothing()
        {
            float[] plain = FieldSpawnRules.EffectiveShares(1f, AllFive);
            float[] low = FieldSpawnRules.EffectiveShares(0.5f, AllFive);
            for (int r = 0; r < FieldSpawnRules.RarityCount; r++) Assert.AreEqual(plain[r], low[r], 1e-6f);
        }

        [Test]
        public void PickRarity_Sweep_MatchesEffectiveShares_WithBoost()
        {
            float[] expect = FieldSpawnRules.EffectiveShares(1.5f, AllFive);
            var counts = new float[FieldSpawnRules.RarityCount];
            for (int i = 0; i < Sweep; i++)
                counts[FieldSpawnRules.PickRarity((i + 0.5f) / Sweep, 1.5f, AllFive)] += 1f / Sweep;
            for (int r = 0; r < FieldSpawnRules.RarityCount; r++)
                Assert.AreEqual(expect[r], counts[r], 0.001f, $"{(InsectRarity)r}");
        }

        // ── 레벨 ──

        [TestCase(1, 10)]
        [TestCase(36, 50)]
        [TestCase(62, 70)]
        [TestCase(5, 5)]
        public void RollFieldLevel_AlwaysInsideRegionBand(int min, int max)
        {
            for (int i = 0; i <= 1000; i++)
            {
                int level = FieldSpawnRules.RollFieldLevel(min, max, i / 1000f);
                Assert.GreaterOrEqual(level, min);
                Assert.LessOrEqual(level, max);
            }
            Assert.AreEqual(min, FieldSpawnRules.RollFieldLevel(min, max, 0f), "대역 바닥에 닿지 못한다");
            Assert.AreEqual(max, FieldSpawnRules.RollFieldLevel(min, max, 1f), "대역 천장에 닿지 못한다");
        }

        /// <summary>
        /// 옛 규칙은 대역을 버리고 Lv.1부터 굴렸다 — 유적(Lv.36~50)에서도 절반 가까이가 Lv.10 아래였다.
        /// 지금은 대역 안에서 낮은 쪽이 조금 더 흔할 뿐이다. 등급은 인자로 받지도 않는다.
        /// </summary>
        [Test]
        public void RollFieldLevel_LowEndSomewhatMoreCommon()
        {
            int low = 0, high = 0;
            const int min = 36, max = 50;   // 15단계 — 아래 5단계 / 위 5단계
            for (int i = 0; i < Sweep; i++)
            {
                int level = FieldSpawnRules.RollFieldLevel(min, max, (i + 0.5f) / Sweep);
                if (level <= min + 4) low++;
                else if (level >= max - 4) high++;
            }
            Assert.Greater(low, high, "낮은 쪽이 더 흔해야 한다");
            Assert.Greater(high, Sweep / 10, "높은 쪽이 거의 안 나오면 대역을 쓰는 의미가 없다");
        }

        // ── 슬롯 수 ──

        [Test]
        public void SlotCountFor_ScalesWithLandAndClamps()
        {
            Assert.AreEqual(FieldSpawnRules.MinRegionSlots, FieldSpawnRules.SlotCountFor(0f));
            Assert.AreEqual(FieldSpawnRules.MaxRegionSlots, FieldSpawnRules.SlotCountFor(1e7f));
            float r60 = Mathf.PI * 60f * 60f;
            Assert.AreEqual(Mathf.RoundToInt(r60 / FieldSpawnRules.SquareMetersPerInsect), FieldSpawnRules.SlotCountFor(r60));
            Assert.Greater(FieldSpawnRules.SlotCountFor(Mathf.PI * 80f * 80f), FieldSpawnRules.SlotCountFor(r60));
        }

        [Test]
        public void Radii_KeepHysteresisAndPopInOutsideSpawnGap()
        {
            // 세우기/거두기 사이가 벌어져 있어야 경계에서 매 틱 깜빡이지 않는다.
            Assert.Greater(FieldSpawnRules.RecallRadius, FieldSpawnRules.MaterializeRadius);
            // 수명 교체는 새 개체가 생겨도 되는 거리보다 멀어야 "눈앞에서 바뀜"이 없다.
            Assert.GreaterOrEqual(FieldSpawnRules.RotateMinPlayerDistance, FieldSpawnRules.SpawnMinPlayerDistance);
            Assert.GreaterOrEqual(FieldSpawnRules.RespawnDelayMin, 30f, "재생이 너무 빠르면 리전 이동 리롤과 다를 게 없다");
        }

        // ── 서브에리어 종 순환 ──

        [Test]
        public void SubAreaSpecies_FirstFillMatchesLegacy_ThenRotatesThroughEveryExclusive()
        {
            string[] ids = { "a", "b", "c" };
            int slots = FieldSpawnRules.SubAreaSlotCount(ids.Length, 2);
            Assert.AreEqual(2, slots);
            Assert.AreEqual("a", FieldSpawnRules.SubAreaSpecies(ids, 0, slots, 0), "첫 판은 옛 ids[i % n]");
            Assert.AreEqual("b", FieldSpawnRules.SubAreaSpecies(ids, 1, slots, 0));

            // 옛 규칙은 슬롯 번호만 봐서 "c"가 영영 안 나왔다.
            var seen = new HashSet<string>();
            for (int gen = 0; gen < 3; gen++)
                for (int s = 0; s < slots; s++)
                    seen.Add(FieldSpawnRules.SubAreaSpecies(ids, s, slots, gen));
            CollectionAssert.AreEquivalent(ids, seen);
        }

        [Test]
        public void SubAreaSlotCount_FollowsLegacyRule()
        {
            Assert.AreEqual(0, FieldSpawnRules.SubAreaSlotCount(0, 2));
            Assert.AreEqual(2, FieldSpawnRules.SubAreaSlotCount(1, 2));
            Assert.AreEqual(2, FieldSpawnRules.SubAreaSlotCount(4, 2));
            Assert.AreEqual(3, FieldSpawnRules.SubAreaSlotCount(2, 5));
        }

        // ── 스토리 포획 보조 ──

        [Test]
        public void PickStoryTarget_WantedSpeciesAbsent_PicksItWithinChance()
        {
            var wanted = new List<string> { "dragonfly_emperor" };
            var candidates = new HashSet<string> { "dragonfly_emperor", "frog" };
            var alive = new HashSet<string> { "frog" };
            Assert.AreEqual("dragonfly_emperor",
                FieldSpawnRules.PickStoryTarget(wanted, candidates, alive, 0f, 0.5f));
            Assert.IsNull(FieldSpawnRules.PickStoryTarget(wanted, candidates, alive, FieldSpawnRules.StoryAssistChance, 0.5f),
                "확률 밖이면 평소대로 굴린다");
        }

        [Test]
        public void PickStoryTarget_AlreadyAliveOrNotCandidate_ReturnsNull()
        {
            var wanted = new List<string> { "stag_beetle" };
            Assert.IsNull(FieldSpawnRules.PickStoryTarget(wanted, new HashSet<string> { "stag_beetle" },
                new HashSet<string> { "stag_beetle" }, 0f, 0f), "이미 그 리전에 살아 있으면 돕지 않는다");
            Assert.IsNull(FieldSpawnRules.PickStoryTarget(wanted, new HashSet<string> { "other" },
                new HashSet<string>(), 0f, 0f), "지금 나올 수 없는(풀·시간대 밖) 종은 주지 않는다");
            Assert.IsNull(FieldSpawnRules.PickStoryTarget(new List<string>(), null, null, 0f, 0f));
        }

        [Test]
        public void PickStoryTarget_SeveralEligible_SpreadsByPickRoll()
        {
            var wanted = new List<string> { "x", "y" };
            var cand = new HashSet<string> { "x", "y" };
            Assert.AreEqual("x", FieldSpawnRules.PickStoryTarget(wanted, cand, new HashSet<string>(), 0f, 0.1f));
            Assert.AreEqual("y", FieldSpawnRules.PickStoryTarget(wanted, cand, new HashSet<string>(), 0f, 0.9f));
            Assert.AreEqual("y", FieldSpawnRules.PickStoryTarget(wanted, cand, new HashSet<string> { "x" }, 0f, 0.1f),
                "살아 있는 쪽을 빼고 고른다");
        }
    }
}
#endif
