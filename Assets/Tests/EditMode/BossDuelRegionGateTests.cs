#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 명부회 하수 대결의 리전 제한 — 오염 거점 보스(<see cref="RegionData.blightBossNpcId"/>)는 자기 거점 리전에서만 싸운다.
    ///
    /// 하수 둘은 거점이 없는 리전에도 서 있다. 연못(플레이어 Lv6~20)에서 <c>ch2_watchers</c>를 본 뒤 검은 옷의 사내
    /// (<c>ledger_thug_cord</c>, Lv34)에게 다시 말을 걸면 확인 없이 대결이 열렸고, 숲의 검은 옷의 여자(Lv32)도 같았다.
    /// 거점을 맡지 않은 간부(집게·저울·하월)는 지금처럼 리전과 무관하다.
    /// </summary>
    [TestFixture]
    public class BossDuelRegionGateTests
    {
        private static RegionData[] Fixture()
        {
            return new[]
            {
                new RegionData { regionId = "pond" },
                new RegionData { regionId = "forest", blightBossNpcId = "ledger_thug_pin" },
                new RegionData { regionId = "mountain", blightBossNpcId = "ledger_thug_rule" },
                new RegionData { regionId = "ruins", blightBossNpcId = "ledger_thug_cord" },
            };
        }

        [Test]
        public void SiteBoss_OnlyInItsOwnRegion()
        {
            RegionData[] regions = Fixture();
            Assert.IsFalse(NpcDuelController.BossDuelAllowedInRegion("ledger_thug_cord", "pond", regions), "연못에서 Lv34 하수와 붙었다");
            Assert.IsFalse(NpcDuelController.BossDuelAllowedInRegion("ledger_thug_cord", "mountain", regions), "다른 거점 리전도 아니다");
            Assert.IsTrue(NpcDuelController.BossDuelAllowedInRegion("ledger_thug_cord", "ruins", regions));

            Assert.IsFalse(NpcDuelController.BossDuelAllowedInRegion("ledger_thug_rule", "forest", regions), "숲의 검은 옷의 여자");
            Assert.IsTrue(NpcDuelController.BossDuelAllowedInRegion("ledger_thug_rule", "mountain", regions));
        }

        [Test]
        public void SiteBoss_OnTheIsland_IsNotAllowed()
        {
            // 나의 섬에서는 ActionRegionId가 null이다 — 섬은 어느 리전도 아니다(rules/island.md).
            Assert.IsFalse(NpcDuelController.BossDuelAllowedInRegion("ledger_thug_cord", null, Fixture()));
        }

        [Test]
        public void NonSiteBoss_IsAllowedAnywhere()
        {
            RegionData[] regions = Fixture();
            foreach (string exec in new[] { "ledger_grip", "ledger_scale", "ledger_chief" })
            {
                Assert.IsTrue(NpcDuelController.BossDuelAllowedInRegion(exec, "pond", regions), exec);
                Assert.IsTrue(NpcDuelController.BossDuelAllowedInRegion(exec, "ruins", regions), exec);
                Assert.IsTrue(NpcDuelController.BossDuelAllowedInRegion(exec, null, regions), exec);
            }
        }

        [Test]
        public void UnknownRegionTable_KeepsTheOldBehaviour()
        {
            Assert.IsTrue(NpcDuelController.BossDuelAllowedInRegion("ledger_thug_cord", "pond", null));
        }

        /// <summary>
        /// 실제 리전 표로 — 거점 보스가 정확히 거점 리전 하나에서만 열리고, 대결 표의 다른 상대는 어디서나 열린다.
        /// 거점 데이터(<c>RegionDefinitions</c>)를 옮기면 이 테스트가 그 인물의 대결 자리를 따라 옮겨 확인한다.
        /// </summary>
        [Test]
        public void RealRegions_EverySiteBoss_HasExactlyOneDuelRegion()
        {
            RegionData[] regions = RegionDefinitions.CreateAll();
            int siteBosses = 0;
            foreach (RegionData site in regions)
            {
                if (site == null || !site.HasBlightSite) continue;
                siteBosses++;
                int allowed = 0;
                foreach (RegionData here in regions)
                    if (here != null && NpcDuelController.BossDuelAllowedInRegion(site.blightBossNpcId, here.regionId, regions))
                        allowed++;
                Assert.AreEqual(1, allowed, $"{site.blightBossNpcId}의 대결이 {allowed}개 리전에서 열린다");
                Assert.IsTrue(NpcDuelController.BossDuelAllowedInRegion(site.blightBossNpcId, site.regionId, regions));
            }
            Assert.Greater(siteBosses, 0, "실제 리전 표에 오염 거점이 하나도 없다 — 파서·데이터를 확인할 것");

            Assert.IsTrue(NpcBossDuels.TryGet("ledger_grip", out _), "간부 표가 바뀌었다 — 아래 단언을 갱신할 것");
            foreach (RegionData here in regions)
                if (here != null)
                    Assert.IsTrue(NpcDuelController.BossDuelAllowedInRegion("ledger_grip", here.regionId, regions));
        }
    }
}
#endif
