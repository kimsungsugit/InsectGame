#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 2막 거점 편의시설 배치. VillageBuilder.Build는 Awake/Start에 의존하지 않는 평범한
    /// 메서드라 컴포넌트를 하나 만들어 그대로 부를 수 있다(씬 구성 불필요).
    /// </summary>
    [TestFixture]
    public class OutpostFacilityTests
    {
        private GameObject host;
        private VillageBuildResult result;

        [OneTimeSetUp]
        public void BuildOnce()
        {
            host = new GameObject("VillageBuilderTestHost");
            VillageBuilder builder = host.AddComponent<VillageBuilder>();
            result = builder.Build(RegionDefinitions.CreateAll());
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            // Build가 만든 "Village" 루트는 host의 자식이 아니다 — 따로 치운다.
            GameObject village = GameObject.Find("Village");
            if (village != null) Object.DestroyImmediate(village);
            if (host != null) Object.DestroyImmediate(host);
        }

        private InteractionPointDef Find(string id)
        {
            foreach (InteractionPointDef p in result.interactions)
                if (p != null && p.id == id) return p;
            return null;
        }

        [Test]
        public void Build_ProducesInteractions()
        {
            Assert.IsNotNull(result);
            Assert.Greater(result.interactions.Count, 0, "상호작용이 하나도 안 만들어졌다");
        }

        [TestCase("outpost_dunes_shop", InteractionKind.ItemShop)]
        [TestCase("outpost_dunes_hospital", InteractionKind.Hospital)]
        [TestCase("outpost_canopy_shop", InteractionKind.ItemShop)]
        [TestCase("outpost_canopy_hospital", InteractionKind.Hospital)]
        [TestCase("outpost_canopy_training", InteractionKind.Training)]
        public void HubRegions_HaveExpectedFacility(string id, InteractionKind kind)
        {
            InteractionPointDef point = Find(id);
            Assert.IsNotNull(point, $"거점 시설 {id}가 없다 — 2막에서 회복하려면 초원까지 왕복해야 한다");
            Assert.AreEqual(kind, point.kind);
            Assert.Greater(point.radius, 0f);
        }

        [Test]
        public void NonHubRegions_HaveNoFacilities()
        {
            // 6리전 전부에 두면 마을의 특별함이 없어진다 — 거점 2곳만이 설계다.
            string[] nonHubs = { "hollow", "frostline", "emberfall", "nameless" };
            foreach (string regionId in nonHubs)
            {
                foreach (InteractionPointDef p in result.interactions)
                {
                    Assert.IsFalse(p != null && p.id != null && p.id.StartsWith($"outpost_{regionId}_"),
                        $"{regionId}는 거점이 아닌데 시설이 붙었다: {p.id}");
                }
            }
        }

        [Test]
        public void Gacha_StaysInMainVillageOnly()
        {
            int gachaCount = 0;
            foreach (InteractionPointDef p in result.interactions)
                if (p != null && p.kind == InteractionKind.Gacha) gachaCount++;

            Assert.AreEqual(1, gachaCount, "가챠는 초원 본마을 전용이다");
        }

        [Test]
        public void InteractionIds_AreUnique()
        {
            // 같은 id가 둘이면 뒤엣것이 앞엣것을 가려 한쪽이 영영 안 열린다.
            var seen = new HashSet<string>();
            foreach (InteractionPointDef p in result.interactions)
            {
                if (p == null || string.IsNullOrEmpty(p.id)) continue;
                Assert.IsTrue(seen.Add(p.id), $"상호작용 id 중복: {p.id}");
            }
        }

        [Test]
        public void HubFacilities_AreInsideTheirRegion()
        {
            // 시설이 리전 밖에 서면 걸어가서 쓸 수 없다.
            AssertInsideRegion("outpost_dunes_shop", "dunes");
            AssertInsideRegion("outpost_dunes_hospital", "dunes");
            AssertInsideRegion("outpost_canopy_shop", "canopy");
            AssertInsideRegion("outpost_canopy_hospital", "canopy");
            AssertInsideRegion("outpost_canopy_training", "canopy");
        }

        private void AssertInsideRegion(string interactionId, string regionId)
        {
            InteractionPointDef point = Find(interactionId);
            Assert.IsNotNull(point, interactionId);

            RegionData region = null;
            foreach (RegionData r in RegionDefinitions.CreateAll())
                if (r != null && r.regionId == regionId) { region = r; break; }
            Assert.IsNotNull(region, regionId);

            Vector3 d = point.worldPosition - region.centerPosition;
            d.y = 0f;
            Assert.Less(d.magnitude, region.radius,
                $"{interactionId}가 {regionId} 반경({region.radius}m) 밖에 있다");
        }

        [Test]
        public void EveryRegion_HasAnOutpostVillagerAnchor()
        {
            // 초원은 본마을이라 전초기지가 없다. 나머지 12리전은 전부 있어야 한다.
            foreach (RegionData region in RegionDefinitions.CreateAll())
            {
                if (region == null || region.regionId == "meadow") continue;

                bool found = false;
                foreach (NpcSpawnAnchor a in result.npcAnchors)
                {
                    if (a != null && a.kind == NpcKind.Villager && a.regionId == region.regionId)
                    {
                        found = true;
                        break;
                    }
                }
                Assert.IsTrue(found, $"{region.regionId}에 전초기지 주민 앵커가 없다");
            }
        }
    }
}
#endif
