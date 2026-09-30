#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.Data;

namespace InsectGame.Tests
{
    /// <summary>
    /// 조건부 의상 해금 판정(<see cref="OutfitUnlockRules"/>).
    ///
    /// 판정기가 생기기 전에는 카탈로그의 조건 4개가 문구로만 존재했다 — 꽃 왕관·연구원 가운·장화·
    /// 곤충박사 배지를 얻을 방법이 없었고, 연구원 세트는 완성이 불가능했다. 여기서는 판정식과
    /// **카탈로그의 조건이 실제로 도달 가능한가**(리전·퀘스트가 실재하는가)를 함께 고정한다.
    /// 오타 하나면 예외도 경고도 없이 그 의상이 다시 영영 잠긴다.
    /// </summary>
    [TestFixture]
    public class OutfitUnlockRulesTests
    {
        private static bool NoQuests(string _) => false;

        [Test]
        public void Region_MetOnlyWhenStandingThere()
        {
            Assert.IsTrue(OutfitUnlockRules.IsMet("region_garden", "garden", 1, NoQuests));
            Assert.IsFalse(OutfitUnlockRules.IsMet("region_garden", "meadow", 99, NoQuests));
            Assert.IsFalse(OutfitUnlockRules.IsMet("region_garden", null, 99, NoQuests));
        }

        [Test]
        public void Level_MetAtOrAboveThreshold()
        {
            Assert.IsFalse(OutfitUnlockRules.IsMet("level_15", null, 14, NoQuests));
            Assert.IsTrue(OutfitUnlockRules.IsMet("level_15", null, 15, NoQuests));
            Assert.IsTrue(OutfitUnlockRules.IsMet("level_15", null, 40, NoQuests));
        }

        [Test]
        public void Quest_AsksTheQuestIdWithoutPrefix()
        {
            string asked = null;
            bool met = OutfitUnlockRules.IsMet("quest_q_complete", null, 1, id => { asked = id; return true; });

            Assert.IsTrue(met);
            Assert.AreEqual("q_complete", asked, "접두사를 떼고 물어야 한다");
            Assert.IsFalse(OutfitUnlockRules.IsMet("quest_q_complete", null, 1, null), "퀘스트 출처가 없으면 거짓");
        }

        /// <summary>잘못 적은 토큰이 의상을 공짜로 풀어 주면 안 된다.</summary>
        [Test]
        public void UnknownOrMalformed_IsNeverMet()
        {
            Assert.IsFalse(OutfitUnlockRules.IsMet("", "garden", 99, _ => true));
            Assert.IsFalse(OutfitUnlockRules.IsMet(null, "garden", 99, _ => true));
            Assert.IsFalse(OutfitUnlockRules.IsMet("wat_garden", "garden", 99, _ => true));
            Assert.IsFalse(OutfitUnlockRules.IsMet("level_abc", null, 99, _ => true));
            Assert.IsFalse(OutfitUnlockRules.IsMet("level_0", null, 99, _ => true));
            Assert.IsFalse(OutfitUnlockRules.IsMet("region_", "", 99, _ => true));
            Assert.IsFalse(OutfitUnlockRules.IsMet("quest_", null, 99, _ => true));
        }

        // ── 카탈로그 정합 ──

        private static List<OutfitItem> ConditionalItems()
        {
            List<OutfitItem> list = new List<OutfitItem>();
            foreach (OutfitItem item in CharacterOutfitManager.BuildCatalog())
                if (!string.IsNullOrEmpty(item.unlockCondition)) list.Add(item);
            return list;
        }

        [Test]
        public void Catalog_EveryConditionIsAKnownFormat()
        {
            List<OutfitItem> items = ConditionalItems();
            Assert.Greater(items.Count, 0, "조건부 의상이 사라졌다 — 카탈로그가 바뀌었으면 이 테스트도 확인할 것");
            foreach (OutfitItem item in items)
                Assert.IsTrue(OutfitUnlockRules.IsKnownFormat(item.unlockCondition),
                    $"{item.itemId}: 판정기가 모르는 조건 '{item.unlockCondition}' — 영영 안 풀린다");
        }

        /// <summary>
        /// 조건부 의상은 가격이 없어야 한다 — 가격이 있으면 카드가 구매 버튼을 그리고 잠김 문구는 안 뜬다
        /// (<c>CharacterOutfitUI</c>의 분기 순서가 보석 → 코인 → 조건이다). 둘 다면 조건이 장식이 된다.
        /// </summary>
        [Test]
        public void Catalog_ConditionalItemsAreNotForSale()
        {
            foreach (OutfitItem item in ConditionalItems())
            {
                Assert.AreEqual(0, item.price, $"{item.itemId}: 조건부인데 코인 가격이 있다");
                Assert.AreEqual(0, item.gemPrice, $"{item.itemId}: 조건부인데 보석 가격이 있다");
                Assert.IsFalse(item.unlockedByDefault, $"{item.itemId}: 조건부인데 기본 보유다");
            }
        }

        [Test]
        public void Catalog_RegionConditionsNameRealRegions()
        {
            HashSet<string> regions = new HashSet<string>();
            foreach (RegionData r in RegionDefinitions.CreateAll())
                if (r != null && !string.IsNullOrEmpty(r.regionId)) regions.Add(r.regionId);

            foreach (OutfitItem item in ConditionalItems())
            {
                if (!item.unlockCondition.StartsWith(OutfitUnlockRules.RegionPrefix)) continue;
                string id = item.unlockCondition.Substring(OutfitUnlockRules.RegionPrefix.Length);
                Assert.IsTrue(regions.Contains(id), $"{item.itemId}: 리전 '{id}'이 없다 — 도달 불가");
            }
        }

        [Test]
        public void Catalog_LevelConditionsAreReachable()
        {
            foreach (OutfitItem item in ConditionalItems())
            {
                if (!OutfitUnlockRules.TryParseLevel(item.unlockCondition, out int lv)) continue;
                // PlayerProgressController.maxLevel(80)을 넘으면 영영 안 풀린다.
                Assert.LessOrEqual(lv, 80, $"{item.itemId}: 만렙보다 높은 레벨 조건");
            }
        }

        /// <summary>
        /// 퀘스트는 <c>TutorialQuestManager.Initialize</c>의 코드 배열에만 있다(씬 없이는 못 만든다) —
        /// quest_lint와 같은 방식으로 소스에서 questId를 읽는다.
        /// </summary>
        [Test]
        public void Catalog_QuestConditionsNameRealQuests()
        {
            string src = File.ReadAllText("Assets/Scripts/Core/TutorialQuestManager.cs");
            foreach (OutfitItem item in ConditionalItems())
            {
                if (!item.unlockCondition.StartsWith(OutfitUnlockRules.QuestPrefix)) continue;
                string id = item.unlockCondition.Substring(OutfitUnlockRules.QuestPrefix.Length);
                StringAssert.Contains($"questId = \"{id}\"", src, $"{item.itemId}: 퀘스트 '{id}'가 없다 — 도달 불가");
            }
        }
    }
}
#endif
