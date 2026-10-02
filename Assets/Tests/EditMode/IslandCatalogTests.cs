#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// <see cref="IslandCatalog"/> — 섬 물건 정의의 정합. 카탈로그는 코드 정의라 오타가 런타임까지 간다:
    /// 가격이 0이면 살 수 없는 물건이 상점에 뜨고, 모델 switch에서 빠지면 상자 모양으로 놓인다.
    /// </summary>
    [TestFixture]
    public class IslandCatalogTests
    {
        [Test]
        public void Ids_AreUniqueAndWellFormed()
        {
            var seen = new HashSet<string>();
            foreach (IslandObjectDef def in IslandCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(def.id));
                Assert.IsTrue(seen.Add(def.id), $"섬 물건 id 중복: {def.id}");
                // 서버(functions/island.js)가 받는 형식 — 여기 안 맞으면 공개할 때 그 물건이 조용히 빠진다.
                Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(def.id, "^[a-z][a-z0-9_]{1,31}$"),
                    $"섬 물건 id 형식: {def.id}");
                Assert.AreSame(def, IslandCatalog.Get(def.id));
            }
        }

        [Test]
        public void EveryObject_HasNameDescriptionAndFootprint()
        {
            foreach (IslandObjectDef def in IslandCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(def.displayName), def.id);
                Assert.IsFalse(string.IsNullOrEmpty(def.description), def.id);
                Assert.GreaterOrEqual(def.width, 1, def.id);
                Assert.GreaterOrEqual(def.depth, 1, def.id);
                Assert.GreaterOrEqual(def.comfort, 0, def.id);
            }
        }

        [Test]
        public void EveryObject_HasExactlyOnePrice()
        {
            foreach (IslandObjectDef def in IslandCatalog.All)
            {
                bool coin = def.coinPrice > 0;
                bool gem = def.gemPrice > 0;
                Assert.IsTrue(coin ^ gem, $"{def.id}: 코인가와 다이아가 중 정확히 하나만 양수여야 한다");
            }
        }

        [Test]
        public void EffectObjects_HavePositiveValue_AndPlainOnesHaveNone()
        {
            foreach (IslandObjectDef def in IslandCatalog.All)
            {
                if (def.effect == IslandEffectKind.None) Assert.AreEqual(0f, def.effectValue, def.id);
                else Assert.Greater(def.effectValue, 0f, def.id);
            }
        }

        [Test]
        public void EveryObject_FitsOnTheSizeLevelThatUnlocksIt()
        {
            foreach (IslandObjectDef def in IslandCatalog.All)
            {
                Assert.LessOrEqual(def.requiredSizeLevel, GameConstants.Island.MaxSizeLevel, def.id);
                // 그 물건이 풀리는 섬 크기에서 실제로 놓을 자리가 있어야 한다(가운데 근처).
                Assert.AreEqual(IslandPlaceResult.Ok,
                    IslandGrid.CanPlace(null, -1, def, -def.width, 0, 0, def.requiredSizeLevel), def.id);
            }
        }

        [Test]
        public void EachCategory_HasObjects()
        {
            var counts = new Dictionary<IslandObjectCategory, int>();
            foreach (IslandObjectDef def in IslandCatalog.All)
            {
                counts.TryGetValue(def.category, out int n);
                counts[def.category] = n + 1;
            }
            foreach (IslandObjectCategory category in System.Enum.GetValues(typeof(IslandObjectCategory)))
                Assert.IsTrue(counts.ContainsKey(category) && counts[category] > 0, $"상점 탭이 비었다: {category}");
        }

        [Test]
        public void EveryObject_HasItsOwnModel()
        {
            // 모델 switch에서 빠진 id는 상자 모양 폴백으로 놓인다 — 예외도 경고도 없다.
            foreach (IslandObjectDef def in IslandCatalog.All)
                Assert.IsTrue(IslandObjectBuilder.HasModel(def.id), $"전용 모델이 없다: {def.id}");
            Assert.IsFalse(IslandObjectBuilder.HasModel("x_from_future"));
        }

        [Test]
        public void StarterKit_PointsAtRealObjects_AndIncludesTheGuideBench()
        {
            bool hasBench = false;
            foreach ((string id, int count) in IslandCatalog.StarterKit)
            {
                Assert.IsNotNull(IslandCatalog.Get(id), $"스타터 키트의 물건이 카탈로그에 없다: {id}");
                Assert.Greater(count, 0);
                if (id == "f_bench") hasBench = true;
            }
            // 가이드의 「물건 놓기」가 벤치를 놓아 보라고 말한다.
            Assert.IsTrue(hasBench);
        }

        [Test]
        public void SizePrice_EveryLevelBelowMax_HasBothPrices()
        {
            for (int level = 0; level < GameConstants.Island.MaxSizeLevel; level++)
            {
                Assert.IsTrue(IslandCatalog.SizePrice(level, out int coins, out int gems));
                Assert.Greater(coins, 0);
                Assert.Greater(gems, 0);
            }
            Assert.IsFalse(IslandCatalog.SizePrice(GameConstants.Island.MaxSizeLevel, out _, out _));
            Assert.IsFalse(IslandCatalog.SizePrice(-1, out _, out _));
        }

        [Test]
        public void SlotPrice_CoversEverySlotUpToMax_AndRises()
        {
            int max = GameConstants.Island.MaxInsectSlots - GameConstants.Island.BaseInsectSlots;
            int previousCoins = 0;
            for (int extra = 0; extra < max; extra++)
            {
                Assert.IsTrue(IslandCatalog.SlotPrice(extra, out int coins, out int gems), $"슬롯 {extra}의 가격이 없다");
                Assert.Greater(coins, previousCoins, "슬롯 값은 살수록 올라야 한다");
                Assert.Greater(gems, 0);
                previousCoins = coins;
            }
            Assert.IsFalse(IslandCatalog.SlotPrice(max, out _, out _));
        }

        [Test]
        public void QuestIslandRewards_PointAtRealObjects()
        {
            // 퀘스트 보상의 섬 물건 id가 오타면 "보이지도 않고 주지도 않는" 보상이 된다(포맷터·지급이 같은 술어를 쓴다).
            string source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(UnityEngine.Application.dataPath, "Scripts/Core/TutorialQuestManager.cs"));
            var matches = System.Text.RegularExpressions.Regex.Matches(source, "rewardIslandObjectId\\s*=\\s*\"([^\"]+)\"");
            Assert.Greater(matches.Count, 0, "섬 물건을 주는 퀘스트가 하나도 없다 — 정규식이 소스와 어긋났는지 확인");
            foreach (System.Text.RegularExpressions.Match m in matches)
                Assert.IsNotNull(IslandCatalog.Get(m.Groups[1].Value), $"퀘스트 보상 섬 물건이 카탈로그에 없다: {m.Groups[1].Value}");
        }
    }
}
#endif
