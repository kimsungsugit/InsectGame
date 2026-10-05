#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 행동이 일어난 리전(<see cref="RegionManager.ActionRegionIdOf"/>) — 지역 의뢰(<see cref="QuestRegionGate"/>)와 스토리 리전 게이트가 읽는다.
    ///
    /// 나의 섬은 분리 서브에리어라 들어가 있는 동안 <c>CurrentRegion</c>이 떠나기 전 리전으로 남는다(sticky). 그 값을 그대로 읽으면
    /// 섬에 찾아온 손님 곤충을 잡을 때 그 리전의 지역 의뢰·스토리 포획으로 셌다(2026-10-03).
    /// </summary>
    [TestFixture]
    public class RegionActionRegionTests
    {
        private static readonly RegionData Pond = new RegionData { regionId = "pond" };

        [Test]
        public void ActionRegion_OnTheField_IsTheCurrentRegion()
        {
            Assert.AreEqual("pond", RegionManager.ActionRegionIdOf(Pond, null));
            Assert.IsNull(RegionManager.ActionRegionIdOf(null, null), "길 위");
        }

        [Test]
        public void ActionRegion_InACave_StaysInsideItsRegion()
        {
            // 지역 의뢰는 서브에리어 안의 행동도 센다(rules/quest-system.md) — 동굴은 그 리전 안이다.
            var cave = new SubAreaData { subAreaId = "pond_deep", detached = false };
            Assert.AreEqual("pond", RegionManager.ActionRegionIdOf(Pond, cave));
        }

        [Test]
        public void ActionRegion_OnTheIsland_IsNoRegion()
        {
            var island = new SubAreaData { subAreaId = GameConstants.Island.SubAreaId, detached = true };
            Assert.IsNull(RegionManager.ActionRegionIdOf(Pond, island), "섬에서 잡은 손님이 떠나기 전 리전으로 세어진다");
        }

        [Test]
        public void RegionQuest_DoesNotCountACaptureOnTheIsland()
        {
            var island = new SubAreaData { subAreaId = GameConstants.Island.SubAreaId, detached = true };
            Assert.IsFalse(QuestRegionGate.Counts("pond", RegionManager.ActionRegionIdOf(Pond, island)));
            Assert.IsTrue(QuestRegionGate.Counts("pond", RegionManager.ActionRegionIdOf(Pond, null)));
            Assert.IsTrue(QuestRegionGate.Counts(null, RegionManager.ActionRegionIdOf(Pond, island)), "리전 제한 없는 의뢰는 섬에서도 센다");
        }
    }
}
#endif
