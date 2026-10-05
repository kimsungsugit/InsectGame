#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 필드 테마(바닥 팔레트·리전 대기·서브에리어 입구·테마 방·NPC 옷차림·연못 호수·겹친 리전 울타리·둔덕 높이)의
    /// 데이터 차원 불변식.
    ///
    /// 전부 <b>증상이 조용한</b> 것들이다. 팔레트에 리전을 빠뜨리면 옛 공식색(서릿길 회녹색)으로 떨어지고,
    /// 연무가 짙으면 게임 카메라에서 캐릭터가 뿌옇게 묻히고, 입구 표식을 빠뜨리면 그 서브에리어는 필드에서
    /// 아무 표시 없이 근접 프롬프트로만 존재한다. 호수가 NPC 자리를 삼키거나 울타리 기둥이 이웃 마을 건물에 박히거나
    /// 둔덕이 높이 표에서 빠져도 마찬가지다 — 예외도 경고도 없다. 눈으로 보는 건 <c>FieldDesignTour</c>가 한다.
    /// </summary>
    [TestFixture]
    public class FieldThemeTests
    {
        // ── 바닥 팔레트 ──

        [Test]
        public void RegionPalette_CoversEveryRegion()
        {
            var missing = new List<string>();
            foreach (RegionData r in RegionDefinitions.CreateAll())
                if (!RegionPalette.Has(r.regionId)) missing.Add(r.regionId);
            CollectionAssert.IsEmpty(missing, "바닥색이 옛 테마색 공식으로 떨어지는 리전");
        }

        [Test]
        public void RegionPalette_GroundIsNotBlownOut()
        {
            // 조명이 약 1.3배라 반사율 0.8을 넘으면 화면에서 흰색으로 날아간다(서릿길이 실제로 그랬다)
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                Color g = RegionPalette.Ground(r.regionId, r.themeColor);
                Assert.LessOrEqual(g.maxColorComponent, 0.8f, $"{r.regionId}: 바닥이 너무 밝다 {g}");
                Assert.GreaterOrEqual(g.maxColorComponent, 0.12f, $"{r.regionId}: 바닥이 너무 어둡다 {g}");
            }
        }

        [Test]
        public void RegionPalette_PatchTonesStayNearGround()
        {
            // 얼룩은 단색 판을 깨는 용도다 — 바닥에서 크게 벗어나면 "진흙 웅덩이"처럼 읽힌다(꽃밭 갈색 얼룩)
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                Color g = RegionPalette.Ground(r.regionId, r.themeColor);
                RegionPalette.PatchTones(r.regionId, r.themeColor, out Color light, out Color dark);
                Assert.Less(Distance(g, light), 0.2f, $"{r.regionId}: 밝은 얼룩이 바닥에서 너무 멀다");
                Assert.Less(Distance(g, dark), 0.2f, $"{r.regionId}: 짙은 얼룩이 바닥에서 너무 멀다");
            }
        }

        // ── 리전 대기 ──

        [Test]
        public void RegionAtmosphere_CoversEveryRegion()
        {
            var missing = new List<string>();
            foreach (RegionData r in RegionDefinitions.CreateAll())
                if (!RegionAtmosphere.TryGet(r.regionId, out _)) missing.Add(r.regionId);
            CollectionAssert.IsEmpty(missing, "대기 프로필이 없는 리전");
        }

        [Test]
        public void RegionAtmosphere_FogNeverHidesThePlayer()
        {
            // 게임 카메라는 플레이어에서 약 11m — 투과율이 98% 아래로 내려가면 캐릭터가 뿌옇다.
            // 모드는 런타임(SubAreaEnvironment)이 쓰는 바로 그 상수다 — 모드가 Exp로 바뀌면 이 계산도 Exp로 바뀐다.
            float dist = RegionAtmosphere.PlayerCameraDistance;
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                Assert.IsTrue(RegionAtmosphere.TryGet(r.regionId, out RegionAtmosphere.Profile p));
                Assert.LessOrEqual(p.fogDensity, RegionAtmosphere.MaxFogDensity, r.regionId);
                float transmittance = RegionAtmosphere.FogTransmittance(RegionAtmosphere.FieldFogMode, p.fogDensity, dist);
                Assert.GreaterOrEqual(transmittance, RegionAtmosphere.MinPlayerTransmittance,
                    $"{r.regionId}: {RegionAtmosphere.FieldFogMode} {dist}m 투과율 {transmittance:F3}");
                Assert.Greater(p.fogDensity, 0f, $"{r.regionId}: 연무가 없으면 원경 산맥이 하늘에서 떠 보인다");
            }
            // 런타임은 밀도를 상한으로 자른다(SubAreaEnvironment.BuildRegionProfile) — 상한 자체도 같은 모드로 통과해야 한다
            float atCap = RegionAtmosphere.FogTransmittance(RegionAtmosphere.FieldFogMode, RegionAtmosphere.MaxFogDensity, dist);
            Assert.GreaterOrEqual(atCap, RegionAtmosphere.MinPlayerTransmittance, $"밀도 상한의 {dist}m 투과율 {atCap:F3}");
        }

        [Test]
        public void FogTransmittance_MatchesUnityFogFormulas()
        {
            // 식이 틀리면 위 검사가 무엇이든 통과시킨다 — 셰이더(UNITY_CALC_FOG_FACTOR)와 같은 값인지 알려진 점으로 고정
            Assert.AreEqual(Mathf.Exp(-1f), RegionAtmosphere.FogTransmittance(FogMode.Exponential, 0.1f, 10f), 1e-5f);
            Assert.AreEqual(Mathf.Exp(-2f), RegionAtmosphere.FogTransmittance(FogMode.Exponential, 0.1f, 20f), 1e-5f);
            Assert.AreEqual(Mathf.Exp(-1f), RegionAtmosphere.FogTransmittance(FogMode.ExponentialSquared, 0.1f, 10f), 1e-5f);
            Assert.AreEqual(Mathf.Exp(-4f), RegionAtmosphere.FogTransmittance(FogMode.ExponentialSquared, 0.1f, 20f), 1e-5f);
            Assert.AreEqual(0.5f, RegionAtmosphere.FogTransmittance(FogMode.Linear, 0f, 150f), 1e-5f);
            Assert.AreEqual(1f, RegionAtmosphere.FogTransmittance(FogMode.ExponentialSquared, 0.05f, 0f), 1e-6f);
        }

        [Test]
        public void FieldFog_RuntimeUsesTheSharedModeConstant()
        {
            // 런타임이 모드를 리터럴로 적으면 위 가시거리 검사가 다른 모드를 계산하게 된다 — 상수 경유를 소스에서 고정
            string src = ReadSource("Assets/Scripts/Core/SubAreaEnvironment.cs");
            StringAssert.Contains("RegionAtmosphere.FieldFogMode", src, "메인 필드 안개 모드를 상수로 쓰지 않는다");
            StringAssert.Contains("RegionAtmosphere.SubAreaFogMode", src, "서브에리어 안개 모드를 상수로 쓰지 않는다");
            Assert.IsFalse(Regex.IsMatch(src, @"FogMode\.(Linear|Exponential|ExponentialSquared)\b"),
                "SubAreaEnvironment에 안개 모드 리터럴이 있다 — RegionAtmosphere 상수를 거칠 것");
        }

        // ── 연못 호수 ──

        [Test]
        public void PondLake_DockEndsOverWaterAndLandsOnMud()
        {
            // 부두(나루터 널판)의 호수 쪽(북쪽) 끝 몇 m가 물 위, 나머지는 진흙 — 옛 호수는 부두 끝에서 1.2m 모자라 부두가 풀밭에 섰다
            RegionData pond = FindRegion("pond");
            RegionDressingBuilder.PondLakeLayout lake = RegionDressingBuilder.PondLake(pond.centerPosition, pond.radius);
            RegionTerrainBuilder.PondDockFootprint(pond.centerPosition, pond.radius, out Vector3 dock, out Vector2 size);
            Vector3 north = dock + Vector3.forward * (size.y * 0.5f);
            for (float t = 0f; t <= 2f; t += 0.25f)
                Assert.IsTrue(lake.water.Contains(north + Vector3.back * t, 1f, 0f), $"부두 북쪽 끝에서 {t}m가 물 밖이다");
            Assert.IsFalse(lake.water.Contains(north + Vector3.back * 4f, 1f, 0f), "부두 대부분이 물 한가운데로 들어갔다");
            for (float t = 0f; t <= size.y; t += 0.25f)
                Assert.IsTrue(lake.mud.Contains(north + Vector3.back * t, 1f, 0.3f), $"부두 북쪽 끝에서 {t}m가 풀밭이다 — 물가에서 끊겼다");
        }

        [Test]
        public void PondLake_KeepsSubAreaGatesWhereTheyBelong()
        {
            RegionData pond = FindRegion("pond");
            RegionDressingBuilder.PondLakeLayout lake = RegionDressingBuilder.PondLake(pond.centerPosition, pond.radius);
            foreach (SubAreaData sub in pond.subAreas)
            {
                if (sub.subAreaId == "pond_deep")
                {
                    // 연못 깊은 곳 — 물속 입구(돌 고리 반경 2.2m + 돌 0.25m)가 통째로 물 안, 물가에서 1m 넘게 안쪽
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI * 2f / 16f;
                        Vector3 p = sub.centerPosition + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.45f;
                        Assert.IsTrue(lake.water.Contains(p, 1f, -1f), $"연못 깊은 곳 입구가 물가에 걸린다 ({i})");
                    }
                }
                else
                {
                    // 갈대 밀림 등 뭍 입구 — 아치 기둥(중심에서 ±2.3m)이 진흙 가장자리에서 2m 넘게 밖
                    foreach (float side in new[] { -2.3f, 0f, 2.3f })
                        Assert.IsFalse(lake.mud.Contains(sub.centerPosition + Vector3.right * side, 1f, 2f),
                            $"{sub.subAreaId} 입구가 호수에 잠긴다");
                }
            }
        }

        [Test]
        public void PondLake_LeavesPondNpcsAndOutpostOnDryLand()
        {
            // 옛 호수 중심에서 20~24m 고리에 NPC·모닥불이 둘러서 있어 호수를 고르게 키울 수 없었다(PondLake 주석).
            // 자리는 VillageBuilder가 단일 출처라 사본을 두지 않고 실제로 지어 본다.
            RegionData[] regions = RegionDefinitions.CreateAll();
            RegionData pond = FindRegion(regions, "pond");
            RegionDressingBuilder.PondLakeLayout lake = RegionDressingBuilder.PondLake(pond.centerPosition, pond.radius);
            var host = new GameObject("PondLakeVillageTestHost");
            try
            {
                VillageBuildResult result = host.AddComponent<VillageBuilder>().Build(regions);
                int checkedAnchors = 0;
                foreach (NpcSpawnAnchor anchor in result.npcAnchors)
                {
                    if (anchor.regionId != "pond") continue;
                    checkedAnchors++;
                    Assert.IsFalse(lake.mud.Contains(anchor.position, 1f, 2.4f),
                        $"{anchor.kind} {anchor.storyNpcId}: 진흙 가장자리에서 2m 안쪽이다(발자리 0.4m 포함)");
                }
                Assert.GreaterOrEqual(checkedAnchors, 4, "연못 NPC 자리를 못 찾았다 — 이 검사가 무의미해졌다");

                Transform outpost = GameObject.Find("Village/Outpost_pond").transform;
                Vector3 fire = outpost.Find("Campfire").position;
                Assert.IsFalse(lake.mud.Contains(fire, 1f, 1.25f + 2f), "전초기지 모닥불(돌 고리 1.25m)이 물가 2m 안이다");

                // 전초기지 앞 작은 물가(RegionDressingBuilder.Pond의 cove, 진흙 원반 10.8×9.6)와 큰 호수가 붙어 한 덩어리가 되지 않는다
                var cove = new RegionDressingBuilder.DiscPlacement(outpost.TransformPoint(new Vector3(-3.2f, 0f, -2.5f)),
                    10.8f, 9.6f, outpost.eulerAngles.y, RegionDressingBuilder.PatchSegments);
                for (int i = 0; i < 36; i++)
                    Assert.IsFalse(lake.mud.Contains(cove.EdgePoint(i * Mathf.PI * 2f / 36f, 1f), 1f, 2f),
                        "전초기지 물가와 큰 호수의 진흙이 2m 안으로 붙는다");
            }
            finally
            {
                // Build가 만든 "Village" 루트는 host의 자식이 아니다 — 따로 치운다
                GameObject village = GameObject.Find("Village");
                if (village != null) Object.DestroyImmediate(village);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PondShoreRocks_NeverSitInTheLake()
        {
            // 부트스트랩 물가 바위(AddPondScenery의 Pond_ShoreRock_, 콜라이더 있음) — 옛 호수 중심(리전 중심)에서 0.35R ± 0.5m,
            // 60° 간격 ± 0.3rad, 크기 rs 0.4~0.9(몸 반경 0.6rs). 호수가 남동으로 옮겨 커진 뒤 0°·300° 두 개가 난수에 따라
            // 물 한가운데에 섰다. 고리 식은 부트스트랩에만 있어 소스에서 식과 호출을 고정하고, 난수 범위 전체를 훑는다.
            string src = ReadSource("Assets/Scripts/Core/PlaySceneBootstrap.cs");
            Match block = Regex.Match(src, @"// --- Existing shore rocks ---(.*?)// --- NEW: Wooden bridge", RegexOptions.Singleline);
            Assert.IsTrue(block.Success, "물가 바위 배치를 못 찾았다 — 이 테스트가 무의미해졌다");
            StringAssert.Contains("RegionDressingBuilder.PondShoreSpot(", block.Value, "물가 바위가 호수 윤곽을 안 본다");
            StringAssert.Contains("Mathf.PI * 2f * i / 6f + Random.Range(-0.3f, 0.3f)", block.Value, "고리 식이 바뀌었다 — 아래 사본도 고칠 것");
            StringAssert.Contains("rad * 0.5f + Random.Range(-0.5f, 0.5f)", block.Value, "고리 식이 바뀌었다 — 아래 사본도 고칠 것");
            StringAssert.Contains("Random.Range(0.4f, 0.9f)", block.Value, "크기 식이 바뀌었다 — 아래 사본도 고칠 것");

            RegionData pond = FindRegion("pond");
            RegionDressingBuilder.PondLakeLayout lake = RegionDressingBuilder.PondLake(pond.centerPosition, pond.radius);
            Vector3 c = pond.centerPosition;
            float rad = pond.radius * 0.7f;   // AddRegionScenery가 넘기는 배치 반경
            int moved = 0;
            for (int i = 0; i < 6; i++)
            for (int ai = 0; ai <= 24; ai++)
            for (int di = 0; di <= 8; di++)
            foreach (float rs in new[] { 0.4f, 0.9f })
            {
                float a = Mathf.PI * 2f * i / 6f - 0.3f + 0.6f * ai / 24f;
                float d = rad * 0.5f - 0.5f + di / 8f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float body = rs * 0.6f;
                Vector3 q = RegionDressingBuilder.PondShoreSpot(c, pond.radius, p, body);
                string at = $"바위 {i} ({p.x - c.x:F1}, {p.z - c.z:F1}) rs {rs}";
                Assert.IsFalse(lake.water.Contains(q, 1f, body), $"{at}: 옮긴 자리 ({q.x - c.x:F1}, {q.z - c.z:F1})가 물에 닿는다");
                Assert.AreEqual(p.y, q.y, 1e-6f, $"{at}: 높이를 바꿨다");
                if ((q - p).sqrMagnitude < 1e-8f) continue;

                moved++;
                Assert.IsTrue(lake.water.Contains(p, 1f, body), $"{at}: 물에 안 닿는 바위를 옮겼다");
                // 같은 방위(호수 중심에서 본 반직선)의 진흙 띠 바깥 가장자리 위
                Vector2 rockDir = new Vector2(p.x - lake.water.center.x, p.z - lake.water.center.z).normalized;
                Vector2 shoreDir = new Vector2(q.x - lake.water.center.x, q.z - lake.water.center.z).normalized;
                Assert.Less(Mathf.Abs(rockDir.x * shoreDir.y - rockDir.y * shoreDir.x), 1e-3f, $"{at}: 방위가 바뀌었다");
                Assert.Greater(Vector2.Dot(rockDir, shoreDir), 0f, $"{at}: 호수 반대편으로 옮겼다");
                Assert.IsTrue(lake.mud.Contains(q, 1f, 0.05f) && !lake.mud.Contains(q, 1f, -0.05f),
                    $"{at}: 진흙 가장자리가 아니다 ({q.x - c.x:F1}, {q.z - c.z:F1})");
            }
            Assert.Greater(moved, 0, "물에 잠기는 경우를 한 번도 안 만났다 — 호수나 바위 고리가 바뀌었다면 이 검사를 다시 볼 것");
        }

        [Test]
        public void PatchEdgeRadius_MatchesTheDiscMesh()
        {
            // 소품 배치·회피가 보는 윤곽과 실제로 그려지는 메시가 같은 테두리여야 갈대가 물가를 따라 선다
            foreach (int segments in new[] { RegionDressingBuilder.PatchSegments, RegionDressingBuilder.LakeSegments })
            {
                Mesh mesh = RegionDressingBuilder.IrregularDisc("EdgeTest", segments, RegionDressingBuilder.PatchRadius);
                try
                {
                    Vector3[] v = mesh.vertices;
                    for (int i = 1; i <= segments; i++)
                    {
                        Vector3 a = v[i], b = v[i % segments + 1];
                        Vector3 mid = (a + b) * 0.5f;
                        Assert.AreEqual(a.magnitude, RegionDressingBuilder.PatchEdgeRadius(Mathf.Atan2(a.z, a.x), segments), 1e-4f, $"{segments}분할 꼭짓점 {i}");
                        Assert.AreEqual(mid.magnitude, RegionDressingBuilder.PatchEdgeRadius(Mathf.Atan2(mid.z, mid.x), segments), 1e-4f, $"{segments}분할 변 {i}");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        // ── 울타리 ──

        [Test]
        public void RegionFences_DoNotStandInsideAnotherRegion()
        {
            // 초원·습지는 약 42m 겹친다 — 습지 둔덕 기둥이 본 마을 가챠 오두막 한가운데(1.0m)에 박혀 있었다
            var before = new HashSet<int>();
            foreach (GameObject existing in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                before.Add(existing.GetInstanceID());
            var host = new GameObject("FenceOverlapTestHost");
            try
            {
                RegionData[] regions = RegionDefinitions.CreateAll();
                host.AddComponent<RegionTerrainBuilder>().BuildBoundaries(regions);
                int posts = 0;
                foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                {
                    if (before.Contains(go.GetInstanceID()) || !go.name.StartsWith("Fence") || go.GetComponent<Collider>() == null) continue;
                    posts++;
                    Vector3 p = go.transform.position;
                    foreach (RegionData o in regions)
                    {
                        float d = new Vector2(p.x - o.centerPosition.x, p.z - o.centerPosition.z).magnitude;
                        // 제 리전의 기둥은 반경 − 1m에 선다 — 그보다 0.5m 넘게 안쪽이면 남의 리전 안에 선 기둥이다
                        Assert.GreaterOrEqual(d, o.radius - 1.5f, $"{go.name} ({p.x:F1}, {p.z:F1})이 {o.regionId} 안 {o.radius - d:F1}m에 섰다");
                    }
                }
                Assert.Greater(posts, 600, "울타리 기둥을 못 찾았다 — 이 검사가 무의미해졌다");
            }
            finally
            {
                foreach (GameObject generated in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                    if (generated != null && !before.Contains(generated.GetInstanceID())) Object.DestroyImmediate(generated);
            }
        }

        [Test]
        public void BootstrapBarriers_SkipOtherRegionsWithTheFenceRule()
        {
            // 부트스트랩 Barrier_ 고리(0.85R · 8개, 바위는 콜라이더 있음)도 울타리와 같은 판정으로 남의 리전 안을 건너뛴다.
            // 초원 바위 하나가 습지 안에, 습지 말뚝 둘이 초원 안에 섰다. 고리는 부트스트랩에만 있어 호출을 소스에서 고정한다.
            string src = ReadSource("Assets/Scripts/Core/PlaySceneBootstrap.cs");
            Match loop = Regex.Match(src, @"// --- Region boundary barriers ---(.*?)// WorldTerrainBuilder owns", RegexOptions.Singleline);
            Assert.IsTrue(loop.Success, "Barrier 배치를 못 찾았다 — 이 테스트가 무의미해졌다");
            StringAssert.Contains("RegionTerrainBuilder.IsInsideOtherRegionFence(regionDefs, region, bPos)", loop.Value,
                "Barrier가 울타리와 다른 판정을 쓴다(또는 안 쓴다)");
            StringAssert.Contains("float bRad = region.radius * 0.85f;", loop.Value, "고리 반경이 바뀌었다 — 아래 사본도 고칠 것");
            // 전역 난수 줄기 — 바위 크기를 건너뛰기 판정보다 먼저 뽑아야 뒤따르는 필드 소품이 안 밀린다
            int draw = loop.Value.IndexOf("Random.Range(1.2f, 2f)", System.StringComparison.Ordinal);
            int skip = loop.Value.IndexOf("if (inOtherRegion) continue;", System.StringComparison.Ordinal);
            Assert.IsTrue(draw >= 0 && skip >= 0, "바위 크기 난수 또는 건너뛰기를 못 찾았다");
            Assert.Less(draw, skip, "바위를 건너뛸 때 크기 난수를 안 뽑는다 — 전역 난수 호출 수가 바뀐다");

            RegionData[] regions = RegionDefinitions.CreateAll();
            RegionData meadow = FindRegion(regions, "meadow"), swamp = FindRegion(regions, "swamp");
            Vector3 Ring(RegionData r, int bi)
            {
                float ba = Mathf.PI * 2f * bi / 8f;
                return r.centerPosition + new Vector3(Mathf.Cos(ba), 0f, Mathf.Sin(ba)) * (r.radius * 0.85f);
            }
            // 초원·습지 겹침(약 42m) — 초원 5(말뚝, 길 위라 실제로는 IsOnRoute가 먼저 뺀다)·6(바위), 습지 1·2(말뚝)
            foreach ((RegionData r, int bi) in new[] { (meadow, 5), (meadow, 6), (swamp, 1), (swamp, 2) })
                Assert.IsTrue(RegionTerrainBuilder.IsInsideOtherRegionFence(regions, r, Ring(r, bi)), $"{r.regionId} {bi}: 남의 리전 안인데 짓는다");

            int flagged = 0;
            foreach (RegionData r in regions)
                for (int bi = 0; bi < 8; bi++)
                {
                    Vector3 p = Ring(r, bi);
                    if (RegionTerrainBuilder.IsInsideOtherRegionFence(regions, r, p)) { flagged++; continue; }
                    // 남긴 것은 어느 리전의 울타리 줄 안쪽에도 들지 않는다(제 리전 제외) — 제 리전 원만 보고 자기를 잡으면 전부 사라진다
                    foreach (RegionData o in regions)
                    {
                        if (o == r) continue;
                        float dist = new Vector2(p.x - o.centerPosition.x, p.z - o.centerPosition.z).magnitude;
                        Assert.GreaterOrEqual(dist, o.radius - 1f, $"{r.regionId} {bi}: {o.regionId} 안 {o.radius - dist:F1}m");
                    }
                }
            Assert.AreEqual(4, flagged, "겹친 리전의 Barrier 수가 바뀌었다 — 리전 배치가 바뀌었다면 위 목록도 다시 잴 것");
        }

        // ── 둔덕 높이 ──

        [Test]
        public void FieldGround_PlaceMound_SurfaceMatchesTheSphereMesh()
        {
            // 콜라이더 없는 둔덕의 높이 표가 실제로 그려지는 구와 같은 윗면을 내야 곤충·플레이어가 둔덕 위에 선다
            FieldGround.Clear();
            GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                var pos = new Vector3(40f, 0f, -25f);
                RegionTerrainBuilder.PlaceMound(mound, pos, 18f, 8f, 0.55f, 35f);
                Assert.AreEqual(1, FieldGround.Count);
                Assert.AreEqual(FieldGround.FloorY + 0.55f, FieldGround.SurfaceY(pos.x, pos.z), 1e-3f, "꼭대기가 rise만큼 솟지 않는다");

                Matrix4x4 m = mound.transform.localToWorldMatrix;
                int compared = 0;
                foreach (Vector3 local in mound.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    if (local.y < 0.02f) continue;   // 윗반구만 — 아랫면은 땅속이다
                    Vector3 w = m.MultiplyPoint3x4(local);
                    if (w.y < FieldGround.FloorY + 0.01f) continue;   // 바닥 아래로 묻힌 테두리
                    // 허용 5mm — 내장 구 메시 정점은 반지름 0.5에서 조금씩 벗어나 있고 그 오차가 둔덕 배율만큼 커진다
                    // (실측 최대 2.1mm). 발·곤충이 둔덕에 서는 데 의미 있는 차이는 cm 단위다.
                    Assert.AreEqual(w.y, FieldGround.SurfaceY(w.x, w.z), 5e-3f, $"구 표면 ({w.x:F2}, {w.z:F2})");
                    compared++;
                }
                Assert.Greater(compared, 20, "비교할 윗면 정점이 없다");

                // 지면 자국 밖(긴 축 반지름 9m 너머)은 바닥 그대로
                Vector3 longAxis = Quaternion.Euler(0f, 35f, 0f) * Vector3.right;
                Vector3 outside = pos + longAxis * 9.3f;
                Assert.AreEqual(FieldGround.FloorY, FieldGround.SurfaceY(outside.x, outside.z), 1e-5f);
                Assert.AreEqual(0f, FieldGround.LiftAt(pos.x + 30f, pos.z), 1e-6f);
            }
            finally
            {
                FieldGround.Clear();
                Object.DestroyImmediate(mound);
            }
        }

        [Test]
        public void FieldGround_OverlapTakesHigher_BuriedDomeIgnored()
        {
            FieldGround.Clear();
            try
            {
                FieldGround.AddDome(new Vector3(0f, -0.5f, 0f), new Vector3(5f, 0.8f, 5f), 0f);    // 꼭대기 0.3
                FieldGround.AddDome(new Vector3(1f, -1f, 0f), new Vector3(3f, 1.6f, 3f), 0f);     // 꼭대기 0.6
                FieldGround.AddDome(new Vector3(20f, -2f, 0f), new Vector3(6f, 1f, 6f), 0f);      // 완전히 묻힘 — 무시
                Assert.AreEqual(2, FieldGround.Count);
                Assert.AreEqual(0.6f, FieldGround.SurfaceY(1f, 0f), 1e-4f, "겹치면 높은 쪽");
                // 첫 둔덕만 걸치는 자리 — 반축 비율 0.6에서 윗면은 −0.5 + 0.8 × √(1 − 0.36) = 0.14
                Assert.AreEqual(0.14f, FieldGround.SurfaceY(-3f, 0f), 1e-4f, "타원체 윗면 식");
                Assert.AreEqual(FieldGround.FloorY, FieldGround.SurfaceY(20f, 0f), 1e-6f, "묻힌 둔덕이 바닥을 올렸다");
                Assert.AreEqual(0f, FieldGround.LiftAt(20f, 0f), 1e-6f);
            }
            finally
            {
                FieldGround.Clear();
            }
            Assert.AreEqual(0, FieldGround.Count);
            Assert.AreEqual(FieldGround.FloorY, FieldGround.SurfaceY(1f, 0f), 1e-6f, "Clear 뒤에도 옛 둔덕이 남았다");
        }

        [Test]
        public void RegionTerrain_RegistersEveryWalkThroughMound()
        {
            // 둔덕 계열(PlaceMound + 재 더미·이끼 둔덕)이 전부 높이 표에 올라야 한다 — 하나라도 빠지면 그 위의 곤충이 묻힌다
            string[] families = { "Scenery_MeadowHill_", "Scenery_Snow_", "Scenery_HollowRidge_", "Scenery_Dune_",
                "Scenery_SnowDrift_", "Scenery_LavaFlow_", "Scenery_AshMound_", "Scenery_MossMound_", "Scenery_VoidPlate_" };
            var before = new HashSet<int>();
            foreach (GameObject existing in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                before.Add(existing.GetInstanceID());
            Random.State randomBefore = Random.state;
            var host = new GameObject("MoundRegistryTestHost");
            try
            {
                host.AddComponent<RegionTerrainBuilder>().BuildAllRegions(RegionDefinitions.CreateAll());
                var seen = new Dictionary<string, int>();
                foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                {
                    if (before.Contains(go.GetInstanceID())) continue;
                    foreach (string family in families)
                    {
                        if (!go.name.StartsWith(family)) continue;
                        seen[family] = seen.TryGetValue(family, out int n) ? n + 1 : 1;
                        Vector3 p = go.transform.position;
                        float top = p.y + go.transform.lossyScale.y * 0.5f;
                        if (top <= FieldGround.FloorY) continue;
                        Assert.GreaterOrEqual(FieldGround.SurfaceY(p.x, p.z), top - 0.01f, $"{go.name}: 꼭대기 {top:F2}m가 높이 표에 없다");
                    }
                }
                foreach (string family in families)
                    Assert.IsTrue(seen.ContainsKey(family), $"{family} 둔덕이 하나도 안 지어졌다 — 이름이 바뀌었다면 이 표도 고칠 것");
                Assert.IsTrue(randomBefore.Equals(Random.state), "지형 빌드가 전역 난수 상태를 되돌리지 않았다");
            }
            finally
            {
                foreach (GameObject generated in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                    if (generated != null && !before.Contains(generated.GetInstanceID())) Object.DestroyImmediate(generated);
                FieldGround.Clear();
            }
        }

        // ── 서브에리어 입구 ──

        [Test]
        public void EverySubArea_HasFieldEntrance()
        {
            // 부트스트랩(CreateSubAreaEntries)이 environmentType으로 짓는 입구 + SubAreaGateBuilder 테마 입구.
            // 둘 다 아니면 그 서브에리어는 필드에 아무 표식이 없다(배치 캡처에서 15곳이 그랬다).
            string bootstrap = ReadSource("Assets/Scripts/Core/PlaySceneBootstrap.cs");
            Match body = Regex.Match(bootstrap, @"private void CreateSubAreaEntries\(.*?\n        \}", RegexOptions.Singleline);
            Assert.IsTrue(body.Success, "CreateSubAreaEntries를 못 찾았다 — 이 테스트가 무의미해졌다");
            var byEnv = new HashSet<string>();
            foreach (Match m in Regex.Matches(body.Value, @"case\s+""(\w+)""\s*:\s*\n\s*Create\w+\("))
                byEnv.Add(m.Groups[1].Value);
            Assert.Greater(byEnv.Count, 0, "입구 switch 추출 실패");

            var bare = new List<string>();
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                if (r.subAreas == null) continue;
                foreach (SubAreaData s in r.subAreas)
                    if (!SubAreaGateBuilder.Handles(s.subAreaId) && !byEnv.Contains(s.environmentType))
                        bare.Add($"{s.subAreaId}({s.environmentType})");
            }
            CollectionAssert.IsEmpty(bare, "필드 입구 표식이 없는 서브에리어");
        }

        [Test]
        public void SubAreaGateBuilder_OnlyNamesRealSubAreas()
        {
            // 오타면 부트스트랩도 테마 입구도 안 짓는 게 아니라, 테마 입구 쪽만 조용히 빠진다
            // 목록은 설계 표(Designs) 하나다 — 소스를 정규식으로 긁지 않고 표 자체를 읽는다
            var ids = new HashSet<string>();
            foreach (RegionData r in RegionDefinitions.CreateAll())
                if (r.subAreas != null) foreach (SubAreaData s in r.subAreas) ids.Add(s.subAreaId);
            int designed = 0;
            foreach (string id in SubAreaGateBuilder.DesignedIds)
            {
                designed++;
                Assert.IsTrue(ids.Contains(id), $"존재하지 않는 서브에리어: {id}");
            }
            Assert.Greater(designed, 0, "설계 표가 비었다 — 이 테스트가 무의미해졌다");
        }

        [Test]
        public void SubAreaGateHandles_DesignedOrUnknownId_TrueOnlyForDesigned()
        {
            // 부트스트랩은 Handles가 참이면 기본 입구를 건너뛴다 — 설계 없는 id에 참이면 그 입구는 조용히 사라진다
            foreach (string id in SubAreaGateBuilder.DesignedIds)
                Assert.IsTrue(SubAreaGateBuilder.Handles(id), $"{id}: 설계가 있는데 부트스트랩이 기본 입구까지 짓는다");
            Assert.IsFalse(SubAreaGateBuilder.Handles(null));
            Assert.IsFalse(SubAreaGateBuilder.Handles(""));
            Assert.IsFalse(SubAreaGateBuilder.Handles("no_such_sub_area"), "설계 없는 id를 맡겠다고 답하면 입구가 안 선다");
        }

        [Test]
        public void SubAreaGateBuild_AllRegions_EveryDesignPlacesParts()
        {
            // 표에 id가 있어도 설계가 비어 있으면(부품 0개) 결과는 같다 — 부트스트랩이 건너뛰고 아무것도 안 선다.
            // 실제로 지어 보고 id마다 SubArea_{id}_Gate_ 부품이 하나 이상인지 본다(BuildGate의 LogError도 여기서 드러난다).
            var host = new GameObject("SubAreaGateBuilderTestHost");
            try
            {
                host.AddComponent<SubAreaGateBuilder>().Build(RegionDefinitions.CreateAll());
                var counts = new Dictionary<string, int>();
                foreach (Transform child in host.transform)
                    foreach (string id in SubAreaGateBuilder.DesignedIds)
                        if (child.name.StartsWith($"SubArea_{id}_Gate_"))
                            counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;

                var empty = new List<string>();
                foreach (string id in SubAreaGateBuilder.DesignedIds)
                    if (!counts.ContainsKey(id)) empty.Add(id);
                CollectionAssert.IsEmpty(empty, "설계는 있는데 필드에 부품이 하나도 안 선 입구");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // ── 테마 방 ──

        [Test]
        public void ThemedRooms_KeepTheirParentBuilderSize()
        {
            // story_lint 21은 environmentType → 빌더(본 파일 switch)로 방 크기를 읽는다. 빌더 안에서 subAreaId로
            // 갈라진 방이 크기를 바꾸면 연출 좌표 검사가 거짓이 된다 — 배우가 벽 밖에 서도 PASS가 뜬다.
            string themes = ReadSource("Assets/Scripts/Core/SubAreaWorldBuilder.Themes.cs");
            AssertRoom(themes, "BuildAntlionPit", 14f);    // cave → BuildCave(14)
            AssertRoom(themes, "BuildBlankCore", 14f);     // underground → BuildCave(14)
            AssertRoom(themes, "BuildHiddenPuddle", 14f);  // pond → BuildUnderwater(14)
            AssertRoom(themes, "BuildCanopyCrown", 11f);   // peak → BuildMountainPeak(11)
        }

        private static void AssertRoom(string src, string builder, float half)
        {
            Match m = Regex.Match(src, $@"private void {builder}\(SubAreaData[^)]*\)\s*\{{(.*?)\n        \}}", RegexOptions.Singleline);
            Assert.IsTrue(m.Success, $"{builder}를 못 찾았다");
            Match walls = Regex.Match(m.Groups[1].Value, @"CreateBoundaryWalls\([^;]*?,\s*([\d.]+)f\s*,\s*[\d.]+f\s*\)");
            Assert.IsTrue(walls.Success, $"{builder}: CreateBoundaryWalls가 없다(방이 안 막힌다)");
            Assert.AreEqual(half, float.Parse(walls.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 0.001f,
                $"{builder}: 원래 빌더와 방 크기가 다르다");
        }

        // ── NPC 옷차림 ──

        [Test]
        public void RegionalDress_DoesNotChangeFaces()
        {
            // 옷차림은 별도 난수로 뽑는다 — 기존 색·머리 추첨을 건드리면 주민 얼굴이 통째로 바뀐다
            string[] regions = { null, "meadow", "frostline", "dunes", "emberfall", "canopy" };
            for (int seed = 1; seed < 40; seed++)
            {
                NpcAppearance plain = NpcVisualBuilder.RandomVillager(seed);
                foreach (string region in regions)
                {
                    NpcAppearance dressed = NpcVisualBuilder.RandomVillager(seed, region);
                    Assert.AreEqual(plain.hair, dressed.hair, $"seed {seed} {region}: 머리색");
                    Assert.AreEqual(plain.skin, dressed.skin, $"seed {seed} {region}: 피부색");
                    Assert.AreEqual(plain.hairStyle, dressed.hairStyle, $"seed {seed} {region}: 머리 모양");
                }
            }
        }

        [Test]
        public void RegionalDress_FitsTheClimate()
        {
            for (int seed = 1; seed < 30; seed++)
            {
                NpcAppearance frost = NpcVisualBuilder.RandomVillager(seed, "frostline");
                Assert.AreEqual(NpcWear.Coat, frost.wear, "서릿길 주민은 코트");
                Assert.IsTrue(frost.scarf && frost.hasHat && frost.hatStyle == NpcHatStyle.Beanie, "서릿길 주민은 털모자·목도리");

                NpcAppearance sand = NpcVisualBuilder.RandomVillager(seed, "dunes");
                Assert.AreEqual(NpcWear.Robe, sand.wear, "모래언덕 주민은 로브");
                Assert.AreEqual(NpcHatStyle.Headwrap, sand.hatStyle, "모래언덕 주민은 두건");

                NpcAppearance miner = NpcVisualBuilder.RandomVillager(seed, "emberfall");
                Assert.AreEqual(NpcHatStyle.Helmet, miner.hatStyle, "잿불 골짜기 주민은 안전모");

                // 코트·로브는 상체도 덮는다 — 몸통색이 겉옷색이어야 위아래가 한 벌로 읽힌다
                Assert.AreEqual(frost.wearColor, frost.top);
                Assert.AreEqual(sand.wearColor, sand.top);
            }
        }

        [Test]
        public void StoryNpcs_WearWhatTheirNotesPromise()
        {
            // 주석으로만 있던 약속들 — 전부 같은 야구모자 모양이라 색 말고는 구분이 안 됐다
            Assert.AreEqual(NpcHatStyle.Straw, NpcVisualBuilder.StoryNpcAppearance("town_pond").hatStyle, "물결 할머니 — 밀짚모자");
            Assert.AreEqual(NpcHatStyle.Wide, NpcVisualBuilder.StoryNpcAppearance("town_garden").hatStyle, "누리 — 챙 넓은 모자");
            Assert.AreEqual(NpcHatStyle.Beanie, NpcVisualBuilder.StoryNpcAppearance("town_frostline").hatStyle, "서리 — 털모자");
            NpcAppearance miner = NpcVisualBuilder.StoryNpcAppearance("town_emberfall");
            Assert.AreEqual(NpcHatStyle.Helmet, miner.hatStyle, "숯 — 안전모");
            Assert.AreEqual(NpcTool.Pickaxe, miner.tool, "숯 — 곡괭이");
            Assert.AreEqual(NpcTool.Axe, NpcVisualBuilder.StoryNpcAppearance("town_forest").tool, "솔 — 나무꾼의 도끼");
            Assert.AreEqual(NpcTool.Book, NpcVisualBuilder.StoryNpcAppearance("town_meadow").tool, "달래 — 그림책");
            Assert.IsTrue(NpcVisualBuilder.StoryNpcAppearance("ruins_scholar").glasses, "세라 — 안경");
        }

        [Test]
        public void LedgerSociety_SharesUniformMarkAndIvoryTop()
        {
            // 1막 하수가 간부와 같은 아이보리 상의를 입는 게 "저 옷을 어디서 봤더라"의 단서다 — 옷차림을 더해도 유지
            string[] members = { "ledger_chief", "ledger_grip", "ledger_scale", "ledger_ink",
                "ledger_thug_cord", "ledger_thug_rule", "ledger_thug_pin" };
            Color ivory = NpcVisualBuilder.StoryNpcAppearance("ledger_chief").top;
            foreach (string id in members)
            {
                NpcAppearance a = NpcVisualBuilder.StoryNpcAppearance(id);
                Assert.IsTrue(a.ledgerMark, $"{id}: 완장·배지가 없다");
                Assert.AreEqual(ivory, a.top, $"{id}: 상의색이 제복과 다르다");
            }
        }

        // ── 유틸 ──

        private static float Distance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        private static string ReadSource(string relativePath)
        {
            string full = Path.Combine(Application.dataPath, "..", relativePath);
            Assert.IsTrue(File.Exists(full), $"소스를 못 찾음: {relativePath}");
            return File.ReadAllText(full);
        }

        private static RegionData FindRegion(string regionId) => FindRegion(RegionDefinitions.CreateAll(), regionId);

        private static RegionData FindRegion(RegionData[] regions, string regionId)
        {
            RegionData r = WorldRouteLayout.Find(regions, regionId);
            Assert.IsNotNull(r, $"리전 없음: {regionId}");
            return r;
        }
    }
}
#endif
