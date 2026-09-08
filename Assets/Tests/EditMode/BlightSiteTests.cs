#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 명부회 오염 거점 정의의 참조 무결성.
    ///
    /// 거점의 세 필드는 <b>따로 틀려도 예외가 안 난다</b> — 보스 ID가 대결 표에 없으면 정화가
    /// 영영 불가능하고, 귀환종이 그 리전 풀에 없으면 정화해도 아무것도 안 돌아온다.
    /// 둘 다 런타임엔 조용하므로 여기서 잡는다.
    /// </summary>
    [TestFixture]
    public class BlightSiteTests
    {
        private static List<RegionData> Sites()
        {
            List<RegionData> sites = new List<RegionData>();
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                if (r != null && r.HasBlightSite) sites.Add(r);
            }
            return sites;
        }

        [Test]
        public void BlightSites_AtLeastOneIsDefined()
        {
            // 0이면 시스템 전체가 죽은 코드다 — 배선이 다 돼 있어도 아무 일도 안 일어난다.
            Assert.Greater(Sites().Count, 0, "오염 거점이 하나도 정의돼 있지 않다");
        }

        /// <summary>
        /// 거점 보스가 <c>NpcBossDuels</c> 표에 없으면 <c>TryStartBossDuel</c>이 false를 돌려
        /// 도전 자체가 열리지 않는다 — 그 리전은 영구 오염이 된다.
        /// </summary>
        [Test]
        public void BlightSites_BossExistsInDuelTable()
        {
            foreach (RegionData r in Sites())
            {
                Assert.IsTrue(NpcBossDuels.TryGet(r.blightBossNpcId, out _),
                    $"{r.regionId}의 거점 보스 '{r.blightBossNpcId}'가 NpcBossDuels 표에 없다 — 정화 불가");
            }
        }

        /// <summary>
        /// 한 사람이 거점 둘을 맡으면 "그자를 꺾으면 그 거점이 닫힌다"가 성립하지 않는다.
        /// 어느 쪽이 정화되는지 코드가 답할 수 없게 된다.
        /// </summary>
        [Test]
        public void BlightSites_NoBossOwnsTwoSites()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (RegionData r in Sites())
            {
                Assert.IsTrue(seen.Add(r.blightBossNpcId),
                    $"'{r.blightBossNpcId}'가 거점을 둘 이상 맡고 있다 ({r.regionId})");
            }
        }

        /// <summary>
        /// 귀환종이 그 리전 풀에 없으면 정화 뒤 명시 스폰이 리전 필터에 걸러진다 —
        /// 연출은 도는데 곤충은 안 나온다.
        /// </summary>
        [Test]
        public void BlightSites_ReturningInsectIsInRegionPool()
        {
            foreach (RegionData r in Sites())
            {
                Assert.IsFalse(string.IsNullOrEmpty(r.blightReturningInsectId),
                    $"{r.regionId}에 귀환종이 없다 — 정화해도 돌아올 것이 없다");
                Assert.IsNotNull(r.insectIds, $"{r.regionId}의 곤충 풀이 null이다");
                Assert.Contains(r.blightReturningInsectId, r.insectIds,
                    $"{r.regionId}의 귀환종 '{r.blightReturningInsectId}'가 그 리전 풀에 없다");
            }
        }

        [Test]
        public void BlightSites_HaveDisplayName()
        {
            foreach (RegionData r in Sites())
            {
                Assert.IsFalse(string.IsNullOrEmpty(r.blightSiteName),
                    $"{r.regionId}의 거점 이름이 비었다 — 지도·대사에 빈칸이 뜬다");
            }
        }

        /// <summary>
        /// 세 필드를 반만 채운 리전이 없어야 한다. <c>HasBlightSite</c>가 보스 ID만 보므로,
        /// 이름·귀환종만 채우면 그 리전은 거점이 없는 것으로 조용히 취급된다.
        /// </summary>
        [Test]
        public void BlightSites_FieldsAreAllOrNothing()
        {
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                if (r == null || r.HasBlightSite) continue;
                Assert.IsTrue(string.IsNullOrEmpty(r.blightSiteName),
                    $"{r.regionId}: 거점 보스 없이 이름만 있다 — 거점으로 인식되지 않는다");
                Assert.IsTrue(string.IsNullOrEmpty(r.blightReturningInsectId),
                    $"{r.regionId}: 거점 보스 없이 귀환종만 있다");
            }
        }

        /// <summary>
        /// 거점 리전은 <b>수문장이 있는 리전</b>이어야 한다 — 진행 경로 밖에 두면 아무도 안 간다.
        /// (초원만 수문장 규약에서 예외인데, 거긴 거점을 두지 않는다.)
        /// </summary>
        [Test]
        public void BlightSites_AreOnGuardedRegions()
        {
            foreach (RegionData r in Sites())
            {
                Assert.IsFalse(string.IsNullOrEmpty(r.guardianInsectId),
                    $"{r.regionId}에 수문장이 없다 — 진행 경로에서 벗어난 리전에 거점을 두었다");
            }
        }
    }
}
#endif
