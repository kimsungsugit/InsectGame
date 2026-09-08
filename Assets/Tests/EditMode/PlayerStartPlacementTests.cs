#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    [TestFixture]
    public class PlayerStartPlacementTests
    {
        private const float PositionTolerance = 0.01f;

        [Test]
        public void ResolveMainVillageEntrance_DefaultRegions_MatchesExpectedPosition()
        {
            PlayerStartPose pose = PlayerStartPlacement.ResolveMainVillageEntrance(
                RegionDefinitions.CreateAll());

            Assert.IsFalse(pose.IsFallback);
            Assert.AreEqual(-11.18f, pose.Position.x, PositionTolerance);
            Assert.AreEqual(0.1f, pose.Position.y, PositionTolerance);
            Assert.AreEqual(-12.92f, pose.Position.z, PositionTolerance);
        }

        [Test]
        public void ResolveMainVillageEntrance_DefaultRegions_FacesVillageCenter()
        {
            RegionData meadow = GetMeadow();
            PlayerStartPose pose = PlayerStartPlacement.ResolveMainVillageEntrance(
                RegionDefinitions.CreateAll());
            Vector3 villageCenter = VillageBuilder.GetMainVillageCenter(
                meadow.centerPosition, meadow.radius);
            Vector3 expectedForward = villageCenter - pose.Position;
            expectedForward.y = 0f;
            expectedForward.Normalize();

            Vector3 actualForward = pose.Rotation * Vector3.forward;
            Assert.Greater(Vector3.Dot(expectedForward, actualForward), 0.999f);
        }

        [Test]
        public void ResolveMainVillageEntrance_DefaultRegions_IsInsideMeadow()
        {
            RegionData meadow = GetMeadow();
            PlayerStartPose pose = PlayerStartPlacement.ResolveMainVillageEntrance(
                RegionDefinitions.CreateAll());

            Assert.IsTrue(meadow.ContainsPoint(pose.Position));
        }

        [Test]
        public void ResolveMainVillageEntrance_DefaultRegions_IsOutsideVillageFootprint()
        {
            RegionData meadow = GetMeadow();
            PlayerStartPose pose = PlayerStartPlacement.ResolveMainVillageEntrance(
                RegionDefinitions.CreateAll());
            Vector3 villageCenter = VillageBuilder.GetMainVillageCenter(
                meadow.centerPosition, meadow.radius);
            float distance = HorizontalDistance(pose.Position, villageCenter);

            Assert.Greater(distance, VillageBuilder.MainVillageFootprintRadius);
        }

        [Test]
        public void ResolveMainVillageEntrance_DefaultRegions_IsOutsideEveryMeadowSubArea()
        {
            RegionData meadow = GetMeadow();
            PlayerStartPose pose = PlayerStartPlacement.ResolveMainVillageEntrance(
                RegionDefinitions.CreateAll());

            Assert.IsNotNull(meadow.subAreas);
            foreach (SubAreaData subArea in meadow.subAreas)
            {
                Assert.IsFalse(subArea.ContainsPoint(pose.Position), subArea.subAreaId);
            }
        }

        [Test]
        public void ResolveMainVillageEntrance_NullRegions_ReturnsFallback()
        {
            AssertFallback(PlayerStartPlacement.ResolveMainVillageEntrance(null));
        }

        [Test]
        public void ResolveMainVillageEntrance_MissingMeadow_ReturnsFallback()
        {
            RegionData[] regions =
            {
                new RegionData { regionId = "forest", centerPosition = Vector3.one, radius = 10f }
            };

            AssertFallback(PlayerStartPlacement.ResolveMainVillageEntrance(regions));
        }

        private static RegionData GetMeadow()
        {
            foreach (RegionData region in RegionDefinitions.CreateAll())
            {
                if (region.regionId == "meadow") return region;
            }

            Assert.Fail("meadow 리전을 찾을 수 없습니다.");
            return null;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static void AssertFallback(PlayerStartPose pose)
        {
            Assert.IsTrue(pose.IsFallback);
            Assert.AreEqual(new Vector3(0f, 0.1f, 0f), pose.Position);
            Assert.AreEqual(Quaternion.identity, pose.Rotation);
        }
    }
}
#endif
