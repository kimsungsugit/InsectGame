#if UNITY_EDITOR
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Tests
{
    [TestFixture]
    public class RegionDefinitionsScaleTests
    {
        // 베이스 Ground(±310)와 경계벽(±320) 안에 모든 리전이 들어와야 한다.
        private const float BoundaryLimit = 310f;

        private static RegionData Get(RegionData[] regions, string id)
        {
            foreach (var r in regions)
            {
                if (r.regionId == id) return r;
            }
            Assert.Fail($"리전 없음: {id}");
            return null;
        }

        private static bool Overlaps(RegionData a, RegionData b)
        {
            float dx = a.centerPosition.x - b.centerPosition.x;
            float dz = a.centerPosition.z - b.centerPosition.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            return dist < a.radius + b.radius;
        }

        [Test]
        public void WorldScale_Value_Is1_5()
        {
            Assert.AreEqual(1.5f, RegionDefinitions.WorldScale, 0.0001f);
        }

        [Test]
        public void CreateAll_RegionCount_Is7()
        {
            Assert.AreEqual(7, RegionDefinitions.CreateAll().Length);
        }

        [Test]
        public void CreateAll_ScaledRadius_MatchesExpected()
        {
            var regions = RegionDefinitions.CreateAll();
            Assert.AreEqual(75f, Get(regions, "meadow").radius, 0.001f);
            Assert.AreEqual(67.5f, Get(regions, "pond").radius, 0.001f);
            Assert.AreEqual(82.5f, Get(regions, "forest").radius, 0.001f);
            Assert.AreEqual(67.5f, Get(regions, "swamp").radius, 0.001f);
            Assert.AreEqual(75f, Get(regions, "mountain").radius, 0.001f);
            Assert.AreEqual(60f, Get(regions, "garden").radius, 0.001f);
            Assert.AreEqual(67.5f, Get(regions, "ruins").radius, 0.001f);
        }

        [Test]
        public void CreateAll_ScaledCenters_MatchExpected()
        {
            var regions = RegionDefinitions.CreateAll();
            Assert.AreEqual(Vector3.zero, Get(regions, "meadow").centerPosition);
            Assert.AreEqual(new Vector3(150f, 0f, 45f), Get(regions, "pond").centerPosition);
            Assert.AreEqual(new Vector3(-180f, 0f, -45f), Get(regions, "mountain").centerPosition);
            Assert.AreEqual(new Vector3(0f, 0f, 210f), Get(regions, "ruins").centerPosition);
        }

        [Test]
        public void CreateAll_AllRegions_WithinBoundary()
        {
            foreach (var r in RegionDefinitions.CreateAll())
            {
                Assert.LessOrEqual(Mathf.Abs(r.centerPosition.x) + r.radius, BoundaryLimit,
                    $"{r.regionId} X축 경계 초과");
                Assert.LessOrEqual(Mathf.Abs(r.centerPosition.z) + r.radius, BoundaryLimit,
                    $"{r.regionId} Z축 경계 초과");
            }
        }

        [Test]
        public void CreateAll_SubAreas_FullyInsideParentRegion()
        {
            foreach (var region in RegionDefinitions.CreateAll())
            {
                if (region.subAreas == null) continue;
                foreach (var sub in region.subAreas)
                {
                    float dx = sub.centerPosition.x - region.centerPosition.x;
                    float dz = sub.centerPosition.z - region.centerPosition.z;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    Assert.LessOrEqual(dist + sub.radius, region.radius + 0.01f,
                        $"{sub.subAreaId}가 부모 리전 {region.regionId} 밖으로 벗어남");
                }
            }
        }

        [Test]
        public void CreateAll_AdjacencyRelations_PreservedAfterScale()
        {
            // 균등 스케일은 겹침 관계를 보존해야 한다 — 스케일 전 겹치던 대표 쌍과
            // 분리돼 있던 대표 쌍의 관계가 그대로인지 고정한다.
            var regions = RegionDefinitions.CreateAll();
            Assert.IsTrue(Overlaps(Get(regions, "meadow"), Get(regions, "swamp")),
                "meadow-swamp는 스케일 전부터 겹치던 인접 쌍 — 관계가 깨짐");
            Assert.IsFalse(Overlaps(Get(regions, "meadow"), Get(regions, "pond")),
                "meadow-pond는 스케일 전부터 분리된 쌍 — 관계가 깨짐");
        }
    }
}
#endif
