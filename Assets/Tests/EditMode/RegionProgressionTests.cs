#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 리전 진행 사슬 무결성 — 1막 7지역 + 2막(ver2) 6지역.
    ///
    /// 리전 해금 경로는 <c>RegionManager.DefeatGuardian</c> 하나뿐이다. 그래서 사슬이 끊기거나
    /// 수문장이 잡을 수 없는 레벨이면 그 뒤 콘텐츠 전체가 도달 불가가 된다 — 런타임엔 아무 에러도
    /// 안 나고 그냥 조용히 막힌다. 여기서 데이터 차원으로 잡는다.
    /// </summary>
    [TestFixture]
    public class RegionProgressionTests
    {
        /// <summary>
        /// 메인 진행 사슬. <b>RegionManager.GetNextRegionId의 switch가 권위 있는 출처</b>이고
        /// (private이라 직접 호출 불가) 이 배열은 그 사본이다. switch를 고치면 여기도 고칠 것.
        /// garden은 meadow 수문장 격파 시 별도 해금되는 분기라 사슬에서 빠진다.
        /// </summary>
        private static readonly string[] MainChain =
        {
            "meadow", "pond", "forest", "swamp", "mountain", "ruins",
            "hollow", "dunes", "frostline", "emberfall", "canopy", "nameless"
        };

        private static Dictionary<string, RegionData> ByIdWithGarden()
        {
            Dictionary<string, RegionData> map = new Dictionary<string, RegionData>();
            foreach (RegionData r in RegionDefinitions.CreateAll()) map[r.regionId] = r;
            return map;
        }

        [Test]
        public void MainChain_CoversEveryRegionExceptGarden()
        {
            Dictionary<string, RegionData> map = ByIdWithGarden();
            foreach (string id in MainChain)
            {
                Assert.IsTrue(map.ContainsKey(id), $"사슬에 있는 {id}가 RegionDefinitions에 없음");
            }
            Assert.AreEqual(map.Count, MainChain.Length + 1,
                "사슬(+garden 분기)에 포함되지 않은 리전이 있다 — 해금 경로 없이 정의만 된 리전은 도달 불가");
        }

        [Test]
        public void MainChain_RequiredLevel_StrictlyIncreases()
        {
            Dictionary<string, RegionData> map = ByIdWithGarden();
            for (int i = 1; i < MainChain.Length; i++)
            {
                RegionData prev = map[MainChain[i - 1]];
                RegionData cur = map[MainChain[i]];
                Assert.Greater(cur.requiredLevel, prev.requiredLevel,
                    $"{cur.regionId}의 입장 레벨이 앞 리전 {prev.regionId}보다 높지 않다 — 진행 곡선 역전");
            }
        }

        [Test]
        public void EveryGuardian_LevelAboveOwnRegionRequirement()
        {
            // 수문장 레벨이 입장 레벨보다 낮으면 게이트가 무의미해진다
            // (꽃밭 수문장이 실제로 그랬다 — 입장 18에 수문장 13이었다).
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                if (string.IsNullOrEmpty(r.guardianInsectId)) continue;
                Assert.Greater(r.guardianLevel, r.requiredLevel,
                    $"{r.regionId} 수문장 Lv.{r.guardianLevel}이 입장 Lv.{r.requiredLevel} 이하 — 게이트 무의미");
            }
        }

        [Test]
        public void EveryGuardian_LevelWithinInsectLevelCap()
        {
            // 곤충 레벨 상한을 넘는 수문장은 플레이어가 대등하게 키울 수 없다.
            int cap = GameConstants.Leveling.FallbackMaxLevel;
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                if (string.IsNullOrEmpty(r.guardianInsectId)) continue;
                Assert.LessOrEqual(r.guardianLevel, cap,
                    $"{r.regionId} 수문장 Lv.{r.guardianLevel} > 레벨 상한 {cap}");
            }
        }

        [Test]
        public void EveryChainRegionExceptLast_HasGuardian()
        {
            // 수문장이 없으면 그 리전에서 사슬이 끊겨 다음 리전이 영영 안 열린다
            // (1막의 ruins가 정확히 그 상태였고, 그래서 2막을 붙일 수 없었다).
            Dictionary<string, RegionData> map = ByIdWithGarden();
            for (int i = 0; i < MainChain.Length - 1; i++)
            {
                RegionData r = map[MainChain[i]];
                Assert.IsFalse(string.IsNullOrEmpty(r.guardianInsectId),
                    $"{r.regionId}에 수문장이 없어 다음 리전({MainChain[i + 1]})이 해금 불가");
            }
        }

        [Test]
        public void EveryGuardian_ExistsInOwnRegionPool()
        {
            // 수문장 종이 그 리전 풀에 없으면 조우 맥락이 어긋난다(엉뚱한 리전 종이 문지기로 선다).
            //
            // meadow만 예외다 — 수문장 mantis_green은 숲 종이고 초원 풀에 없다. 의도된 것이라
            // 본다: GetGuardianPosition이 수문장을 리전 **경계**에 세우므로, 초원 밖에서 온 더 센
            // 개체가 길을 막는 그림이 맞다. 1막 출시 데이터라 여기서 바꾸지 않고 예외로 기록한다.
            HashSet<string> knownOutsiderGuardians = new HashSet<string> { "meadow" };

            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                if (string.IsNullOrEmpty(r.guardianInsectId)) continue;
                if (knownOutsiderGuardians.Contains(r.regionId)) continue;

                HashSet<string> pool = new HashSet<string>();
                if (r.insectIds != null)
                {
                    foreach (string id in r.insectIds) pool.Add(id);
                }
                if (r.subAreas != null)
                {
                    foreach (SubAreaData sub in r.subAreas)
                    {
                        if (sub.exclusiveInsectIds == null) continue;
                        foreach (string id in sub.exclusiveInsectIds) pool.Add(id);
                    }
                }

                Assert.IsTrue(pool.Contains(r.guardianInsectId),
                    $"{r.regionId} 수문장 {r.guardianInsectId}가 자기 리전 풀에 없음");
            }
        }

        [Test]
        public void EveryRegion_HasNonEmptyInsectPool()
        {
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                Assert.IsNotNull(r.insectIds, $"{r.regionId}: insectIds가 null — 스폰 0마리");
                Assert.Greater(r.insectIds.Length, 0, $"{r.regionId}: 곤충 풀이 비어 스폰 0마리");
            }
        }

        [Test]
        public void SecondActRegions_DoNotOverlapAnyRegion()
        {
            // 겹치면 RegionManager.ContainsPoint가 먼저 걸린 리전을 돌려줘 그 띠에서
            // BGM·스폰 풀이 튄다.
            //
            // **2막끼리만 보면 안 된다** — 사슬상 이웃이 아닌 hollow와 emberfall이 0.8m 겹쳐 있었고,
            // 2막 내부만 보던 옛 판이 그걸 잡긴 했으나 1막과의 겹침은 여전히 사각이었다.
            // 여기서는 2막 각 리전을 **전체 리전**과 대조한다.
            // (1막끼리의 meadow-swamp / swamp-mountain 겹침은 출시된 기존 배치라 대상이 아니다.)
            HashSet<string> act2 = new HashSet<string>
            {
                "hollow", "dunes", "frostline", "emberfall", "canopy", "nameless"
            };
            RegionData[] all = RegionDefinitions.CreateAll();
            for (int i = 0; i < all.Length; i++)
            {
                for (int j = i + 1; j < all.Length; j++)
                {
                    RegionData a = all[i];
                    RegionData b = all[j];
                    if (!act2.Contains(a.regionId) && !act2.Contains(b.regionId)) continue;

                    float dx = a.centerPosition.x - b.centerPosition.x;
                    float dz = a.centerPosition.z - b.centerPosition.z;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    Assert.Greater(dist, a.radius + b.radius,
                        $"{a.regionId}와 {b.regionId}가 겹친다 (거리 {dist:F1} ≤ 반경합 {a.radius + b.radius:F1})");
                }
            }
        }

        [Test]
        public void AllRegions_WithinWorldBoundary()
        {
            // 경계벽(WorldTerrainBuilder.mapSize 520, 두께 3 → 내면 ±518.5) 밖에 놓인 리전은
            // 걸어서 도달할 수 없다. 2막 6지역을 얹으며 옛 320에서 넓힌 값이라 함께 고정한다.
            const float wallInner = 518f;
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                Assert.LessOrEqual(Mathf.Abs(r.centerPosition.x) + r.radius, wallInner,
                    $"{r.regionId}가 경계벽 X 밖으로 벗어남");
                Assert.LessOrEqual(Mathf.Abs(r.centerPosition.z) + r.radius, wallInner,
                    $"{r.regionId}가 경계벽 Z 밖으로 벗어남");
            }
        }

        [Test]
        public void MasterPrivileges_DeriveRegionLists_NotHardcoded()
        {
            // AuthManager.ApplyMasterPrivileges는 마스터 계정에 "전 지역 해금 + 전 수문장 격파"를
            // 준다. 그 목록을 문자열로 박아 두면 리전을 추가할 때마다 조용히 낡는다 —
            // 2막 6지역을 얹었을 때 실제로 어긋나 마스터 지도에 수문장이 미격파로 남았다.
            // 목록이 RegionDefinitions에서 파생되는지를 소스에서 확인한다.
            string path = System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "Scripts/Core/AuthManager.cs");
            Assert.IsTrue(System.IO.File.Exists(path), "AuthManager.cs를 못 찾았다");
            string src = System.IO.File.ReadAllText(path);

            // **선언을 찾아야 한다** — 그냥 이름으로 찾으면 위쪽 호출부가 먼저 잡혀
            // 엉뚱한 구간을 본문으로 읽는다(이 테스트를 처음 쓸 때 실제로 그렇게 헛짚었다).
            int idx = src.IndexOf("private void ApplyMasterPrivileges");
            Assert.Greater(idx, -1,
                "ApplyMasterPrivileges 선언을 못 찾았다 — 시그니처가 바뀌었으면 이 테스트도 고칠 것");
            // 본문 구간 = 선언부터 다음 멤버 선언 직전까지.
            int end = src.IndexOf("\n        private ", idx + 30);
            int endPub = src.IndexOf("\n        public ", idx + 30);
            if (endPub > -1 && (end < 0 || endPub < end)) end = endPub;
            string body = end > idx ? src.Substring(idx, end - idx) : src.Substring(idx);

            Assert.IsTrue(body.Contains("RegionDefinitions.CreateAll()"),
                "마스터 특권의 리전 목록이 RegionDefinitions에서 파생되지 않는다 — 하드코딩은 반드시 낡는다");

            // 주석은 걷어내고 코드만 본다 — 왜 하드코딩을 걷어냈는지 설명하는 주석에는
            // 옛 문자열이 그대로 인용돼 있어서, 그걸 위반으로 읽으면 설명을 못 남긴다.
            string codeOnly = System.Text.RegularExpressions.Regex.Replace(body, @"//[^\n]*", "");
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                Assert.IsFalse(codeOnly.Contains($"\"{r.regionId},"),
                    $"마스터 특권에 리전 ID '{r.regionId}'가 문자열로 박혀 있다");
            }
        }

        [Test]
        public void SubAreaIds_AreGloballyUnique()
        {
            // SubAreaEnter 스토리 트리거가 subAreaId 하나로 매칭하므로 중복되면 엉뚱한 리전에서 발화한다.
            HashSet<string> seen = new HashSet<string>();
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                if (r.subAreas == null) continue;
                foreach (SubAreaData sub in r.subAreas)
                {
                    Assert.IsTrue(seen.Add(sub.subAreaId),
                        $"서브에리어 ID 중복: {sub.subAreaId} ({r.regionId})");
                }
            }
        }
    }
}
#endif
