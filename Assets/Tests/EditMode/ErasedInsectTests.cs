#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 「지워진 개체」 출현 조건. 색 변환과 이름표는 렌더 경로라 기기 확인 대상이고,
    /// 여기서는 <b>어디에 나오는가</b>만 고정한다 — 그게 하드코딩 리전 목록으로 어긋나기 쉬운 부분이다.
    /// </summary>
    [TestFixture]
    public class ErasedInsectTests
    {
        [Test]
        public void Act2Threshold_SplitsActsExactly()
        {
            // 1막 마지막(유적)은 임계 미만, 2막 첫 리전(hollow)은 임계 이상이어야 한다.
            RegionData ruins = null, hollow = null;
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                if (r == null) continue;
                if (r.regionId == "ruins") ruins = r;
                if (r.regionId == "hollow") hollow = r;
            }

            Assert.IsNotNull(ruins);
            Assert.IsNotNull(hollow);
            Assert.Less(ruins.requiredLevel, RegionDefinitions.Act2MinRequiredLevel,
                "유적이 2막으로 분류된다 — 1막에 지워진 개체가 샌다");
            Assert.GreaterOrEqual(hollow.requiredLevel, RegionDefinitions.Act2MinRequiredLevel,
                "텅 빈 들이 1막으로 분류된다 — 2막 핵심 연출이 안 나온다");
        }

        [Test]
        public void IsAct2Region_ClassifiesEveryRegionCorrectly()
        {
            // 리전 ID 목록을 박아 두지 않고 requiredLevel에서 파생시키는 것이 설계다.
            // 기대 분류를 여기 한 번만 적어 두고, 파생이 그와 일치하는지 본다.
            string[] act2 = { "hollow", "dunes", "frostline", "emberfall", "canopy", "nameless" };

            foreach (RegionData region in RegionDefinitions.CreateAll())
            {
                if (region == null) continue;
                bool expected = System.Array.IndexOf(act2, region.regionId) >= 0;
                Assert.AreEqual(expected, RegionDefinitions.IsAct2Region(region),
                    $"{region.regionId}(requiredLevel {region.requiredLevel})의 2막 판정이 어긋난다");
            }
        }

        [Test]
        public void IsAct2Region_NullRegion_IsFalse()
        {
            Assert.IsFalse(RegionDefinitions.IsAct2Region(null));
        }

        [Test]
        public void Act2Regions_RequiredLevelsAreMonotonic()
        {
            // 2막 진행 순서대로 요구 레벨이 올라가야 한다 — 임계 판정이 이 순서에 기대고 있다.
            string[] order = { "hollow", "dunes", "frostline", "emberfall", "canopy", "nameless" };
            int previous = RegionDefinitions.Act2MinRequiredLevel - 1;

            foreach (string id in order)
            {
                RegionData region = null;
                foreach (RegionData r in RegionDefinitions.CreateAll())
                    if (r != null && r.regionId == id) { region = r; break; }

                Assert.IsNotNull(region, id);
                Assert.Greater(region.requiredLevel, previous, $"{id}의 요구 레벨이 앞 리전보다 낮거나 같다");
                previous = region.requiredLevel;
            }
        }
    }
}
#endif
