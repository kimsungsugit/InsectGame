#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Data;
using InsectGame.UI;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 포획 선택 창의 「바로 쓸 채집망」. 포획 버튼이 채집망 선택 창을 건너뛰게 되면서, 무엇을 쓸지를
    /// 사람이 고르지 않는 경우가 생겼다 — 틀리면 귀한 채집망이 말없이 소모된다.
    /// </summary>
    [TestFixture]
    public class CaptureNetChoiceTests
    {
        private static readonly CaptureItemData[] Nets =
        {
            new CaptureItemData { itemId = "net_basic", displayName = "기본 채집망" },
            new CaptureItemData { itemId = "net_silver", displayName = "은빛 채집망" },
            new CaptureItemData { itemId = "net_gold", displayName = "황금 채집망" },
        };

        private static System.Func<string, int> Bag(params (string id, int count)[] owned)
        {
            var map = new Dictionary<string, int>();
            foreach ((string id, int count) in owned) map[id] = count;
            return id => map.TryGetValue(id, out int c) ? c : 0;
        }

        [Test]
        public void Resolve_NothingRemembered_PicksTheCommonestOwnedNet()
        {
            // 처음 포획하는 사람 — 황금 채집망이 있어도 기본부터 쓴다.
            CaptureItemData net = CaptureNetChoice.Resolve(Nets, string.Empty,
                Bag(("net_basic", 5), ("net_silver", 2), ("net_gold", 1)));
            Assert.AreEqual("net_basic", net.itemId);
        }

        [Test]
        public void Resolve_RememberedNetStillOwned_IsReused()
        {
            CaptureItemData net = CaptureNetChoice.Resolve(Nets, "net_silver",
                Bag(("net_basic", 5), ("net_silver", 2)));
            Assert.AreEqual("net_silver", net.itemId);
        }

        [Test]
        public void Resolve_RememberedNetRanOut_FallsBackToTheCommonestOwned_NotTheNextRarer()
        {
            // 은빛이 떨어졌다고 황금으로 올라가면 안 된다 — 가진 것 중 맨 앞(기본)으로 물러난다.
            CaptureItemData net = CaptureNetChoice.Resolve(Nets, "net_silver",
                Bag(("net_basic", 3), ("net_silver", 0), ("net_gold", 4)));
            Assert.AreEqual("net_basic", net.itemId);
        }

        [Test]
        public void Resolve_OnlyRareNetOwned_UsesIt()
        {
            CaptureItemData net = CaptureNetChoice.Resolve(Nets, "net_basic", Bag(("net_gold", 1)));
            Assert.AreEqual("net_gold", net.itemId);
        }

        [Test]
        public void Resolve_NothingOwned_IsNull()
        {
            Assert.IsNull(CaptureNetChoice.Resolve(Nets, "net_basic", Bag()));
        }

        [Test]
        public void Resolve_NullInput_IsSafe()
        {
            Assert.IsNull(CaptureNetChoice.Resolve(null, "net_basic", Bag(("net_basic", 1))));
            Assert.IsNull(CaptureNetChoice.Resolve(Nets, "net_basic", null));
        }
    }
}
#endif
