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

        /// <summary>
        /// 본마을 건물이 서로 겹치지 않는다. 배치는 광장 중심 극좌표(각도, 거리)로 흩어져 있고
        /// <b>어느 검사도 그 각도가 실제로 비었는지 보지 않았다</b> — 병원 주석은 "빈 각도 315°"라
        /// 적어 놓고도 집5(320°)와 중심 거리가 1.48m라 집이 병원 안에 박혀 있었다(2026-08-08 발견).
        /// 벽이 둘 다 콜라이더를 유지해 통행까지 막혔다.
        ///
        /// 판정은 <b>실제 벽 크기</b>로 한다. 두 건물의 벽 반너비 합보다 중심 거리가 멀어야 한다 —
        /// 각도 목록을 눈으로 세는 방식은 이미 한 번 틀렸다.
        /// </summary>
        [Test]
        public void VillageBuildings_DoNotOverlapEachOther()
        {
            GameObject village = GameObject.Find("Village");
            Assert.IsNotNull(village, "Build가 Village 루트를 만들지 않았다");
            Transform mainVillage = village.transform.Find("MainVillage");
            Assert.IsNotNull(mainVillage, "본마을 루트(MainVillage)를 찾지 못했다");

            List<(string name, Vector3 pos, float halfSpan)> buildings =
                new List<(string, Vector3, float)>();
            foreach (Transform child in mainVillage)
            {
                // 벽 조각의 이름은 "Wall"(집·상점·병원·훈련소) 또는 "HexWall_n"(가챠 오두막)이다.
                float half = 0f;
                foreach (Transform part in child)
                {
                    if (!part.name.StartsWith("Wall") && !part.name.StartsWith("HexWall")) continue;

                    // 회전이 임의라 최악의 경우(대각)로 잡는다 — 반너비가 아니라 외접원 반지름.
                    Vector3 s = part.localScale;
                    float corner = 0.5f * Mathf.Sqrt(s.x * s.x + s.z * s.z);
                    // 육각벽처럼 중심에서 떨어져 놓인 조각은 그 거리도 더해야 실제 외곽이 된다.
                    Vector3 lp = part.localPosition;
                    half = Mathf.Max(half, Mathf.Sqrt(lp.x * lp.x + lp.z * lp.z) + corner);
                }
                if (half <= 0f) continue;   // 광장·우물·장식 등 벽 없는 노드는 대상이 아니다

                buildings.Add((child.name, child.position, half));
            }

            Assert.GreaterOrEqual(buildings.Count, 6,
                "벽을 가진 마을 건물이 너무 적다 — 이름 규칙이 바뀌었는지 확인할 것");

            for (int i = 0; i < buildings.Count; i++)
            {
                for (int j = i + 1; j < buildings.Count; j++)
                {
                    Vector3 d = buildings[i].pos - buildings[j].pos;
                    d.y = 0f;
                    float need = buildings[i].halfSpan + buildings[j].halfSpan;
                    Assert.Greater(d.magnitude, need,
                        $"{buildings[i].name}과 {buildings[j].name}이 겹친다 " +
                        $"(중심 거리 {d.magnitude:0.00}m < 필요 {need:0.00}m)");
                }
            }
        }
    }
}
#endif
