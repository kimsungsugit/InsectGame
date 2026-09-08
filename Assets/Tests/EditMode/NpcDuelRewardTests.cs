#if UNITY_EDITOR
using InsectGame.Data;
using InsectGame.NPC;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 곤충잡이 아이 대결의 보상 표와 상대 레벨 산정.
    /// 보상 아이템 ID는 ItemDatabase에 실재해야 한다 — 없는 ID는 런타임에 조용히 실패한다.
    /// </summary>
    [TestFixture]
    public class NpcDuelRewardTests
    {
        /// <summary>
        /// 상대 배정 인덱스는 어떤 NpcId가 와도 풀 범위 안이어야 한다.
        /// FNV-1a 해시가 음수일 때 `Mathf.Abs(int.MinValue)`가 음수를 돌려주면
        /// `pool[음수]`로 즉사하므로, 길이·문자열을 넓게 훑어 범위를 고정한다.
        /// </summary>
        [Test]
        public void PoolIndexFor_AnyNpcIdAndLength_StaysInsidePool()
        {
            int[] lengths = { 1, 2, 3, 7, 12 };
            foreach (int len in lengths)
            {
                for (int i = 0; i < 400; i++)
                {
                    int idx = NpcDuelController.PoolIndexFor("kid_" + i, len, i % len);
                    Assert.GreaterOrEqual(idx, 0, $"len={len} i={i}");
                    Assert.Less(idx, len, $"len={len} i={i}");
                }
            }
        }

        [Test]
        public void PoolIndexFor_EmptyPoolOrNullId_DoesNotThrow()
        {
            Assert.AreEqual(0, NpcDuelController.PoolIndexFor("kid_a", 0, 0));
            Assert.AreEqual(0, NpcDuelController.PoolIndexFor(null, 0, 0));
            Assert.GreaterOrEqual(NpcDuelController.PoolIndexFor(null, 5, 0), 0);
        }

        /// <summary>같은 아이는 세션이 바뀌어도 같은 곤충을 든다(결정적 배정).</summary>
        [Test]
        public void PoolIndexFor_SameNpcId_IsDeterministic()
        {
            Assert.AreEqual(
                NpcDuelController.PoolIndexFor("kid_meadow_3", 9, 0),
                NpcDuelController.PoolIndexFor("kid_meadow_3", 9, 0));
        }

        // ItemDatabase.CreateRuntimeDefault가 등록하는 ID 집합의 부분집합.
        private static readonly string[] KnownItemIds =
        {
            "wound_salve", "net_basic", "net_silver", "net_gold"
        };

        [Test]
        public void RewardItemFor_EveryRarity_ReturnsKnownItemId()
        {
            foreach (InsectRarity rarity in System.Enum.GetValues(typeof(InsectRarity)))
            {
                string id = NpcDuelController.RewardItemFor(rarity);
                Assert.IsNotNull(id, $"{rarity} 보상 ID가 null");
                Assert.IsNotEmpty(id, $"{rarity} 보상 ID가 빈 문자열");
                Assert.Contains(id, KnownItemIds, $"{rarity} 보상 '{id}'가 알려진 아이템이 아님");
            }
        }

        [Test]
        public void RewardCountFor_EveryRarity_IsPositive()
        {
            foreach (InsectRarity rarity in System.Enum.GetValues(typeof(InsectRarity)))
                Assert.Greater(NpcDuelController.RewardCountFor(rarity), 0, $"{rarity} 지급 수량이 0 이하");
        }

        [Test]
        public void RewardItemFor_HigherRarity_GivesBetterNet()
        {
            Assert.AreEqual("wound_salve", NpcDuelController.RewardItemFor(InsectRarity.Common));
            Assert.AreEqual("net_basic", NpcDuelController.RewardItemFor(InsectRarity.Uncommon));
            Assert.AreEqual("net_silver", NpcDuelController.RewardItemFor(InsectRarity.Rare));
            Assert.AreEqual("net_gold", NpcDuelController.RewardItemFor(InsectRarity.Epic));
            Assert.AreEqual("net_gold", NpcDuelController.RewardItemFor(InsectRarity.Legendary));
        }

        [Test]
        public void ResolveEnemyLevel_CaughtWithinSpread_KeepsCaughtLevel()
        {
            Assert.AreEqual(11, NpcDuelController.ResolveEnemyLevel(12, 11));
            Assert.AreEqual(14, NpcDuelController.ResolveEnemyLevel(12, 14));
        }

        [Test]
        public void ResolveEnemyLevel_CaughtFarBelow_ClampsUpToSpread()
        {
            // 아이가 초반 필드에서 잡은 1레벨 곤충이라도 일방적인 승부가 되지 않게 끌어올린다.
            Assert.AreEqual(28, NpcDuelController.ResolveEnemyLevel(30, 1));
        }

        [Test]
        public void ResolveEnemyLevel_CaughtFarAbove_ClampsDownToSpread()
        {
            Assert.AreEqual(7, NpcDuelController.ResolveEnemyLevel(5, 40));
        }

        [Test]
        public void ResolveEnemyLevel_NeverBelowOne()
        {
            Assert.GreaterOrEqual(NpcDuelController.ResolveEnemyLevel(1, 1), 1);
            Assert.GreaterOrEqual(NpcDuelController.ResolveEnemyLevel(1, 0), 1);
            Assert.GreaterOrEqual(NpcDuelController.ResolveEnemyLevel(0, -5), 1);
        }
    }
}
#endif
