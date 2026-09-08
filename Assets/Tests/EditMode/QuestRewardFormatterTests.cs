#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 퀘스트 보상 표시 회귀 방지.
    ///
    /// 실제로 있었던 버그: 완료 배너가 캔디·경험치·곤충만 조립하고 rewardItemId/rewardItemCount는
    /// 읽지도 않아, 아이템을 주는 퀘스트 7개가 아이템을 지급하면서 화면엔 표시하지 않았다.
    /// 아래 Format_AllFourRewards_IncludesItem 이 그 회귀를 고정한다.
    /// </summary>
    [TestFixture]
    public class QuestRewardFormatterTests
    {
        private static TutorialQuest Quest(
            int candy = 0,
            int exp = 0,
            string itemId = null,
            int itemCount = 0,
            string insectId = null,
            string insectName = null,
            int insectLevel = 1)
        {
            return new TutorialQuest
            {
                questId = "q_test",
                title = "테스트 퀘스트",
                rewardCandy = candy,
                rewardExp = exp,
                rewardItemId = itemId,
                rewardItemCount = itemCount,
                rewardInsectId = insectId,
                rewardInsectDisplayName = insectName,
                rewardInsectLevel = insectLevel,
            };
        }

        private static string Resolver(string id)
        {
            return id == "net_gold" ? "황금 채집망" : null;
        }

        [Test]
        public void Format_NoRewards_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, QuestRewardFormatter.Format(Quest(), Resolver));
        }

        [Test]
        public void Format_NullQuest_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, QuestRewardFormatter.Format(null, Resolver));
        }

        [Test]
        public void Format_CandyOnly_ShowsCandy()
        {
            Assert.AreEqual("캔디 5", QuestRewardFormatter.Format(Quest(candy: 5), Resolver));
        }

        [Test]
        public void Format_ExpOnly_ShowsExp()
        {
            Assert.AreEqual("경험치 10", QuestRewardFormatter.Format(Quest(exp: 10), Resolver));
        }

        [Test]
        public void Format_ItemSingle_OmitsMultiplier()
        {
            // 1개짜리에 "×1"을 붙이면 군더더기다.
            Assert.AreEqual("황금 채집망",
                QuestRewardFormatter.Format(Quest(itemId: "net_gold", itemCount: 1), Resolver));
        }

        [Test]
        public void Format_ItemMultiple_ShowsMultiplier()
        {
            Assert.AreEqual("황금 채집망 ×2",
                QuestRewardFormatter.Format(Quest(itemId: "net_gold", itemCount: 2), Resolver));
        }

        [Test]
        public void Format_ItemUnresolvedName_FallsBackToId()
        {
            // ItemDatabase가 아직 주입되지 않았거나 ID가 DB에 없어도 보상 자체는 숨기지 않는다.
            Assert.AreEqual("binding_net",
                QuestRewardFormatter.Format(Quest(itemId: "binding_net", itemCount: 1), Resolver));
        }

        [Test]
        public void Format_ItemNullResolver_FallsBackToId()
        {
            Assert.AreEqual("net_gold",
                QuestRewardFormatter.Format(Quest(itemId: "net_gold", itemCount: 1), null));
        }

        [Test]
        public void Format_ItemIdWithoutCount_IsExcluded()
        {
            // GrantRewards는 count > 0 일 때만 지급한다. 표시도 같은 조건이어야 한다.
            Assert.AreEqual(string.Empty,
                QuestRewardFormatter.Format(Quest(itemId: "net_gold", itemCount: 0), Resolver));
        }

        [Test]
        public void Format_InsectWithDisplayName_UsesDisplayName()
        {
            Assert.AreEqual("레어 장수풍뎅이",
                QuestRewardFormatter.Format(
                    Quest(insectId: "rhinoceros_beetle", insectName: "레어 장수풍뎅이"), Resolver));
        }

        [Test]
        public void Format_InsectWithoutDisplayName_FallsBackToId()
        {
            // 지급은 ID로 이뤄지므로 표시명이 비어도 보상이 사라지면 안 된다.
            Assert.AreEqual("rhinoceros_beetle",
                QuestRewardFormatter.Format(Quest(insectId: "rhinoceros_beetle"), Resolver));
        }

        [Test]
        public void Format_InsectAboveLevelOne_ShowsLevel()
        {
            // ★ 회귀 고정 — GrantRewards가 Mathf.Max(1, rewardInsectLevel)로 실제 레벨을 적용하는데
            // 화면엔 종만 떠서 q_approach의 Lv.6 장수풍뎅이가 그냥 "장수풍뎅이"로 보였다.
            Assert.AreEqual("장수풍뎅이 Lv.6",
                QuestRewardFormatter.Format(
                    Quest(insectId: "rhinoceros_beetle", insectName: "장수풍뎅이", insectLevel: 6),
                    Resolver));
        }

        [Test]
        public void Format_InsectLevelOneOrBelow_OmitsLevel()
        {
            // 1은 기본값이라 군더더기고, 0·음수도 지급은 1이므로 같은 취급이어야 한다.
            foreach (int level in new[] { -3, 0, 1 })
            {
                Assert.AreEqual("장수풍뎅이",
                    QuestRewardFormatter.Format(
                        Quest(insectId: "rhinoceros_beetle", insectName: "장수풍뎅이", insectLevel: level),
                        Resolver),
                    $"레벨 {level}에서 접미사가 붙었다");
            }
        }

        [Test]
        public void HasAny_MatchesCollectedEntryCount()
        {
            // HasAny와 Collect가 같은 술어를 공유하는지 — 조건이 갈라지면 여기서 깨진다.
            TutorialQuest[] cases =
            {
                Quest(),
                Quest(candy: 5),
                Quest(exp: 1),
                Quest(itemId: "net_gold", itemCount: 1),
                Quest(itemId: "net_gold", itemCount: 0),
                Quest(insectId: "rhinoceros_beetle"),
                Quest(candy: 1, exp: 1, itemId: "net_gold", itemCount: 2, insectId: "x"),
            };

            System.Collections.Generic.List<QuestRewardEntry> buffer =
                new System.Collections.Generic.List<QuestRewardEntry>();
            foreach (TutorialQuest quest in cases)
            {
                QuestRewardFormatter.Collect(quest, Resolver, buffer);
                Assert.AreEqual(buffer.Count > 0, QuestRewardFormatter.HasAny(quest),
                    $"{quest.questId}: Collect {buffer.Count}건 vs HasAny 불일치");
            }
        }

        [Test]
        public void Format_AllFourRewards_IncludesItem()
        {
            // ★ 회귀 고정 — 아이템이 빠지면 실패한다.
            string text = QuestRewardFormatter.Format(
                Quest(candy: 20, exp: 25, itemId: "net_gold", itemCount: 1,
                    insectId: "rhinoceros_beetle", insectName: "장수풍뎅이"),
                Resolver);

            Assert.AreEqual("캔디 20 + 경험치 25 + 황금 채집망 + 장수풍뎅이", text);
            StringAssert.Contains("황금 채집망", text);
        }

        [Test]
        public void Collect_AllFourRewards_YieldsFourEntriesInOrder()
        {
            List<QuestRewardEntry> entries = new List<QuestRewardEntry>();
            QuestRewardFormatter.Collect(
                Quest(candy: 1, exp: 2, itemId: "net_gold", itemCount: 3,
                    insectId: "bee", insectName: "꿀벌"),
                Resolver,
                entries);

            Assert.AreEqual(4, entries.Count);
            Assert.AreEqual(QuestRewardKind.Candy, entries[0].Kind);
            Assert.AreEqual(QuestRewardKind.Exp, entries[1].Kind);
            Assert.AreEqual(QuestRewardKind.Item, entries[2].Kind);
            Assert.AreEqual(QuestRewardKind.Insect, entries[3].Kind);
            Assert.AreEqual(3, entries[2].Amount);
        }

        [Test]
        public void Collect_ReusedBuffer_IsClearedBetweenCalls()
        {
            List<QuestRewardEntry> entries = new List<QuestRewardEntry>();
            QuestRewardFormatter.Collect(Quest(candy: 5, exp: 5), Resolver, entries);
            Assert.AreEqual(2, entries.Count);

            QuestRewardFormatter.Collect(Quest(candy: 1), Resolver, entries);
            Assert.AreEqual(1, entries.Count, "재사용 버퍼가 이전 항목을 남기면 보상이 중복 표시된다");
        }

        [Test]
        public void HasAny_MatchesGrantConditions()
        {
            Assert.IsFalse(QuestRewardFormatter.HasAny(Quest()));
            Assert.IsFalse(QuestRewardFormatter.HasAny(null));
            Assert.IsTrue(QuestRewardFormatter.HasAny(Quest(candy: 1)));
            Assert.IsTrue(QuestRewardFormatter.HasAny(Quest(exp: 1)));
            Assert.IsTrue(QuestRewardFormatter.HasAny(Quest(itemId: "x", itemCount: 1)));
            Assert.IsFalse(QuestRewardFormatter.HasAny(Quest(itemId: "x", itemCount: 0)));
            Assert.IsTrue(QuestRewardFormatter.HasAny(Quest(insectId: "x")));
        }
    }
}
#endif
