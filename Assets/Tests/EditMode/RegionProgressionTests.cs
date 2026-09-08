#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
        /// (private이라 직접 호출 불가) 이 배열은 그 사본이다.
        /// <b>사본이 어긋나면 아래 세 테스트가 잡는다</b> — 예전엔 "switch를 고치면 여기도 고칠 것"이라고
        /// 사람에게 부탁만 했고, 그래서 ③(런타임 switch)이 빠져도 ①↔②만 맞으면 전부 통과했다.
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
        public void EveryRegion_HasExplicitSpawnLevelRange()
        {
            // PlaySceneBootstrap.GetRegionLevelRange는 리전별 필드 스폰 레벨 폭을 switch로 정한다.
            // case를 빠뜨리면 default 5로 떨어져 그 리전만 유독 좁은 대역이 되는데, 에러도 안 나고
            // 화면상 티도 잘 안 나서 오래 남는다 — 2막 6지역이 실제로 그렇게 묶여 있었다.
            string path = System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "Scripts/Core/PlaySceneBootstrap.cs");
            Assert.IsTrue(System.IO.File.Exists(path), "PlaySceneBootstrap.cs를 못 찾았다");
            string src = System.IO.File.ReadAllText(path);

            int idx = src.IndexOf("private int GetRegionLevelRange");
            Assert.Greater(idx, -1,
                "GetRegionLevelRange 선언을 못 찾았다 — 이름이 바뀌었으면 이 테스트도 고칠 것");
            int end = src.IndexOf("\n        private ", idx + 30);
            string body = end > idx ? src.Substring(idx, end - idx) : src.Substring(idx);

            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                StringAssert.Contains($"case \"{r.regionId}\"", body,
                    $"{r.regionId}: GetRegionLevelRange에 case가 없어 default 5로 떨어진다");
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

        // ── 사슬은 세 곳에 적혀 있다 ──
        //
        // ① RegionDefinitions(데이터) ② 위 MainChain(이 파일) ③ RegionManager의
        // Get{Next,Previous}RegionId switch(런타임 진행). 위 테스트들은 ①↔②만 대조한다.
        // ③이 빠지면 리전을 추가해도 전부 통과하는데,
        //   - Next 누락 → 수문장을 잡아도 다음 리전이 안 열린다(진행 영구 차단)
        //   - Prev 누락 → GetGuardianPosition의 fromCenter가 원점이 되어 수문장이 엉뚱한 자리에 선다
        // 둘 다 런타임엔 아무 에러도 없이 조용히 막힌다. switch가 private이라 실행할 수 없어
        // 소스를 읽는다(InsectPortraitRoutingTests가 같은 이유로 같은 방식을 쓴다).

        private const string RegionManagerSourcePath = "Scripts/Core/RegionManager.cs";

        /// <summary>
        /// 메서드 하나의 본문만 잘라 온다. <b>파일 전체에서 찾으면 안 된다</b> — 두 switch가
        /// 서로의 역방향이라 `case "nameless": return "canopy";`(Prev)가 Next 검사에 걸린다.
        /// 실제로 그렇게 썼다가 종착지 검사가 거짓 실패했다.
        /// </summary>
        private static string ReadMethodBody(string signature)
        {
            string path = Path.Combine(Application.dataPath, RegionManagerSourcePath);
            Assert.IsTrue(File.Exists(path),
                $"{RegionManagerSourcePath}: 파일을 못 찾았다 — 경로가 바뀌었으면 이 테스트도 고칠 것");
            string source = File.ReadAllText(path);

            int start = source.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.Greater(start, -1, $"{signature}를 못 찾았다 — 시그니처가 바뀌었는가?");

            int open = source.IndexOf('{', start);
            Assert.Greater(open, -1, $"{signature}의 본문 시작을 못 찾았다");

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail($"{signature}의 중괄호가 닫히지 않았다");
            return string.Empty;
        }

        [Test]
        public void MainChain_MatchesRegionManagerForwardSwitch()
        {
            string body = ReadMethodBody("private string GetNextRegionId");
            for (int i = 0; i + 1 < MainChain.Length; i++)
            {
                StringAssert.Contains(
                    $"case \"{MainChain[i]}\": return \"{MainChain[i + 1]}\";",
                    body,
                    $"GetNextRegionId에 {MainChain[i]} → {MainChain[i + 1]} 배선이 없다 — 그 지점에서 진행이 막힌다");
            }
        }

        [Test]
        public void MainChain_MatchesRegionManagerBackwardSwitch()
        {
            string body = ReadMethodBody("private string GetPreviousRegionId");
            for (int i = 0; i + 1 < MainChain.Length; i++)
            {
                StringAssert.Contains(
                    $"case \"{MainChain[i + 1]}\": return \"{MainChain[i]}\";",
                    body,
                    $"GetPreviousRegionId에 {MainChain[i + 1]} → {MainChain[i]} 배선이 없다 — 수문장 스폰 좌표가 원점으로 떨어진다");
            }
        }

        [Test]
        public void LastRegion_HasNoForwardEdge()
        {
            // 종착지에 Next가 생기면 정의되지 않은 리전으로 보낸다(해금은 되는데 갈 곳이 없다).
            // ver3를 붙일 때는 MainChain에 먼저 추가하고 나서 switch를 잇는 순서가 된다.
            string body = ReadMethodBody("private string GetNextRegionId");
            string last = MainChain[MainChain.Length - 1];
            StringAssert.DoesNotContain(
                $"case \"{last}\": return \"",
                body,
                $"{last}는 사슬의 끝인데 다음 리전 배선이 있다 — MainChain을 먼저 늘릴 것");
        }

        [Test]
        public void GuardianWorldPosition_HasNoDuplicateFormulaInBootstrap()
        {
            // PlaySceneBootstrap이 GetGuardianWorldPosition이라는 **낡은 사본**을 들고 있었다.
            // 같은 공식을 쓰면서 이전 리전 switch만 자체 보유해 ver1 5개에서 멈춰 있었고,
            // 그래서 ruins와 2막 6리전의 실물 수문장이 지도 마커와 다른 자리에 섰다
            // (최대 105m — dunes는 연못 안, canopy는 유적 안). ruins는 2막의 유일한 문이라
            // 지도가 가리키는 곳에 아무것도 없으면 에러 없이 조용히 진행이 막힌다.
            string bootstrap = ReadSource("Assets/Scripts/Core/PlaySceneBootstrap.cs");

            StringAssert.DoesNotContain("private Vector3 GetGuardianWorldPosition", bootstrap,
                "수문장 좌표 계산이 다시 복제됐다 — RegionManager.GetGuardianPosition 하나만 쓸 것");
            StringAssert.Contains("regionMgr.GetGuardianPosition(r)", bootstrap,
                "CreateGuardians가 RegionManager에서 좌표를 받지 않는다");
        }

        [Test]
        public void EverySubAreaEnvironmentType_HasLightingProfile()
        {
            // 지오메트리(SubAreaWorldBuilder)와 조명(SubAreaEnvironment)은 짝이다.
            // 한쪽만 늘리면 밀폐 공간을 지어 놓고 바깥 햇빛을 쬔다 — 2막 4종이 실제로 그랬다.
            string definitions = ReadSource("Assets/Scripts/Core/RegionDefinitions.cs");
            string environment = ReadSource("Assets/Scripts/Core/SubAreaEnvironment.cs");

            var used = new HashSet<string>();
            foreach (Match m in Regex.Matches(definitions, @"environmentType\s*=\s*""([a-z_]+)"""))
                used.Add(m.Groups[1].Value);

            Assert.Greater(used.Count, 0, "environmentType 추출 실패 — 이 테스트가 무의미해졌다");

            var missing = new List<string>();
            foreach (string type in used)
                if (!environment.Contains($"case \"{type}\":")) missing.Add(type);

            CollectionAssert.IsEmpty(missing,
                $"조명 프로필이 없어 야외 주광으로 떨어지는 서브에리어 환경: {string.Join(", ", missing)}");
        }

        [Test]
        public void EveryGuardian_StandsInsideItsOwnRegion()
        {
            // 예전 공식은 두 리전 중심의 **중점**이었다. 리전이 겹칠 때만 경계가 되는데
            // 이 월드의 리전은 떨어져 있어서 13개 중 9개가 어느 리전에도 없는 허공에 섰다
            // (hollow는 자기 중심에서 77m 밖). 전역 Ground 위라 떨어지진 않지만
            // **리전 안을 아무리 둘러봐도 수문장이 안 보였다** — 실제 기기에서 그렇게 보고됐다.
            GameObject host = new GameObject("RegionManagerGuardianTest");
            try
            {
                RegionManager mgr = host.AddComponent<RegionManager>();
                RegionData[] all = RegionDefinitions.CreateAll();
                mgr.Initialize(all);

                foreach (RegionData r in all)
                {
                    if (r == null || string.IsNullOrEmpty(r.guardianInsectId)) continue;

                    Vector3 pos = mgr.GetGuardianPosition(r);
                    Vector3 d = pos - r.centerPosition;
                    d.y = 0f;

                    Assert.Less(d.magnitude, r.radius,
                        $"{r.regionId} 수문장이 자기 리전 밖에 있다 " +
                        $"({d.magnitude:F1}m / 반경 {r.radius:F1}m) — 리전 안에서는 보이지 않는다");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static string ReadSource(string relativePath)
        {
            string full = Path.Combine(Application.dataPath, "..", relativePath);
            Assert.IsTrue(File.Exists(full), $"소스를 못 찾음: {relativePath}");
            return File.ReadAllText(full);
        }
    }
}
#endif
