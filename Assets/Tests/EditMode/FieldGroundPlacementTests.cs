#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 둔덕(<see cref="FieldGround"/>)·물(<c>RegionDressingBuilder</c>의 물 판정) 위에 서는 것들 — 곤충 스폰 자리
    /// (<c>InsectSpawner.PickSpawnPosition</c>)·곤충 도주/배회 높이(<c>InsectEntity.GroundRise</c>)·필드 아이템
    /// (<c>CaptureItemSpawner.PickItemPosition</c>)·NPC 발 높이(<c>VillagerNpc.StandHeight</c>)·서브에리어 방 안 스폰
    /// (<c>InsectSpawner.PickContainedPosition</c>)·플레이어 접지(<c>PlayerMovement.GroundHeight</c>·<c>FollowGround</c>).
    /// 둔덕은 콜라이더가 없어 전부 좌표로 높이를 묻는다.
    /// </summary>
    [TestFixture]
    public class FieldGroundPlacementTests
    {
        // 꼭대기 0.7(바닥 위 0.6) — 모래언덕 사구 높이. 구 중심이 바닥 아래라 가장자리가 완만하게 솟는다.
        private static readonly Vector3 DuneCenter = new Vector3(500f, -0.5f, 500f);
        private static readonly Vector3 DuneAxes = new Vector3(6f, 1.2f, 6f);

        // 재 더미(ms 2.4) — 구 중심이 바닥 위(0.432)라 가장자리에 0.33m 턱이 있다(RegionTerrainBuilder 잿불).
        private static readonly Vector3 AshCenter = new Vector3(-500f, 0.432f, -500f);
        private static readonly Vector3 AshAxes = new Vector3(2.16f, 0.72f, 2.16f);

        private const float FrameDt = 1f / 60f;

        [SetUp]
        public void SetUp()
        {
            FieldGround.Clear();
            RegionDressingBuilder.ClearWater();
        }

        [TearDown]
        public void TearDown()
        {
            FieldGround.Clear();
            RegionDressingBuilder.ClearWater();
        }

        private static RegionDressingBuilder.PondLakeLayout PondLake()
        {
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                if (r != null && r.regionId == "pond")
                    return RegionDressingBuilder.PondLake(r.centerPosition, r.radius);
            }
            Assert.Fail("pond 리전이 없다");
            return default;
        }

        // ── 곤충 스폰 높이 ──

        [Test]
        public void PickSpawnPosition_OnDune_StandsOnSurfaceY_NotAddedToCandidateY()
        {
            FieldGround.AddDome(DuneCenter, DuneAxes, 0f);
            // 재배치된 스폰 포인트는 플레이어 y를 복사해 두고, 플레이어는 이제 둔덕 높이만큼 올라서 있다 —
            // 후보 y에 둔덕 높이를 더하면 두 번 오른다.
            Vector3 candidate = new Vector3(DuneCenter.x + 1f, 0.7f, DuneCenter.z - 0.5f);

            Vector3 p = InsectSpawner.PickSpawnPosition(() => candidate);

            Assert.AreEqual(FieldGround.SurfaceY(candidate.x, candidate.z), p.y, 1e-5f, "사구 윗면에 서지 않았다");
            Assert.Greater(p.y, FieldGround.FloorY + 0.5f, "사구 위에서 바닥 높이에 섰다 — 몸이 묻힌다");
            Assert.AreEqual(candidate.x, p.x, 1e-6f);
            Assert.AreEqual(candidate.z, p.z, 1e-6f);
        }

        [Test]
        public void PickSpawnPosition_OffDune_StandsOnFloor()
        {
            FieldGround.AddDome(DuneCenter, DuneAxes, 0f);
            // 리전 링 원위치 포인트는 y 0, 재배치 포인트는 플레이어 y — 평지에선 둘 다 바닥 높이로 모인다
            foreach (float candidateY in new[] { 0f, 0.09f, 0.58f })
            {
                Vector3 candidate = new Vector3(DuneCenter.x + 30f, candidateY, DuneCenter.z);
                Vector3 p = InsectSpawner.PickSpawnPosition(() => candidate);
                Assert.AreEqual(FieldGround.FloorY, p.y, 1e-6f, $"후보 y {candidateY}");
            }
        }

        // ── 물 위 스폰 거르기 — 호수(기록 전에도 안다) ──

        [Test]
        public void PickSpawnPosition_WaterCandidate_IsRerolled()
        {
            RegionDressingBuilder.PondLakeLayout lake = PondLake();
            Vector3 wet = lake.water.center;
            Vector3 dry = lake.water.center + new Vector3(200f, 0f, 0f);
            Assert.IsTrue(InsectSpawner.IsOnWater(wet), "호수 중심이 물이 아니다");
            Assert.IsFalse(InsectSpawner.IsOnWater(dry));

            int calls = 0;
            Vector3 p = InsectSpawner.PickSpawnPosition(() => ++calls == 1 ? wet : dry);

            Assert.AreEqual(2, calls, "물 위 후보를 버리고 한 번 더 굴려야 한다");
            Assert.AreEqual(dry.x, p.x, 1e-4f);
            Assert.AreEqual(dry.z, p.z, 1e-4f);
            Assert.AreEqual(FieldGround.FloorY, p.y, 1e-6f);
        }

        [Test]
        public void PickSpawnPosition_AlwaysWater_PushedToShoreWithoutSkipping()
        {
            // 스폰 포인트 원이 통째로 물이어도 스폰을 건너뛰지 않는다 — 초기 채우기는 한 번 헛돌면 멈춘다
            RegionDressingBuilder.PondLakeLayout lake = PondLake();
            Vector3 center = lake.water.center;
            Vector3 wet = center + new Vector3(3f, 0.3f, -4f);
            int calls = 0;

            Vector3 p = InsectSpawner.PickSpawnPosition(() => { calls++; return wet; });

            Assert.AreEqual(InsectSpawner.SpawnPositionRolls, calls, "재추첨 상한을 지키지 않았다");
            Assert.IsFalse(InsectSpawner.IsOnWater(p), "물가로 밀어냈는데 아직 물 위다");
            Vector3 outward = new Vector3(wet.x - center.x, 0f, wet.z - center.z).normalized;
            Assert.IsTrue(InsectSpawner.IsOnWater(p - outward * 0.6f), "물가를 한참 지나쳐 풀밭 멀리 갔다");
            Vector3 moved = new Vector3(p.x - center.x, 0f, p.z - center.z).normalized;
            Assert.Greater(Vector3.Dot(moved, outward), 0.999f, "스폰 포인트 쪽이 아닌 물가로 밀었다");
            Assert.AreEqual(FieldGround.FloorY, p.y, 1e-6f);
        }

        [Test]
        public void PushOutOfWater_AtLakeCenter_StillLeavesWater()
        {
            // 중심에서는 방향이 없다 — 무한 루프 없이 한쪽 물가로 나가야 한다
            RegionDressingBuilder.PondLakeLayout lake = PondLake();
            Vector3 p = InsectSpawner.PushOutOfWater(lake.water.center);
            Assert.IsFalse(InsectSpawner.IsOnWater(p));
        }

        [Test]
        public void IsOnWater_CountsShoreMarginAsWater()
        {
            // 물가 바로 밖(풀 더미가 수면 위로 걸치는 거리)도 물로 친다
            RegionDressingBuilder.PondLakeLayout lake = PondLake();
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                Vector3 edge = lake.water.EdgePoint(a, 1f);
                Vector3 outward = new Vector3(edge.x - lake.water.center.x, 0f, edge.z - lake.water.center.z).normalized;
                Assert.IsTrue(InsectSpawner.IsOnWater(edge - outward * 0.5f), $"물 안쪽 ({i})");
                Assert.IsTrue(InsectSpawner.IsOnWater(edge + outward * (InsectSpawner.WaterShoreMargin * 0.5f)),
                    $"물가 여유 안쪽 ({i})");
                Assert.IsFalse(InsectSpawner.IsOnWater(edge + outward * (InsectSpawner.WaterShoreMargin + 0.5f)),
                    $"물가 여유 밖인데 물로 쳤다 ({i})");
            }
        }

        [Test]
        public void PickSpawnPosition_DryFirstRoll_UsesFirstRoll()
        {
            int calls = 0;
            Vector3 p = InsectSpawner.PickSpawnPosition(() => { calls++; return new Vector3(4f, 0f, 7f); });
            Assert.AreEqual(1, calls);
            Assert.AreEqual(4f, p.x, 1e-6f);
            Assert.AreEqual(7f, p.z, 1e-6f);
        }

        // ── 물 판정 — 빌드가 기록한 물(나루터 물가·습지 웅덩이·강·원기둥 물) ──

        private static readonly Vector3 PuddleCenter = new Vector3(-300f, FieldGround.FloorY, 260f);

        /// <summary>습지 웅덩이 한 장 — RegionDressingBuilder.Swamp가 b.Add(patchMesh …, new Vector3(s, 1f, s * 0.85f), pool)로 그리는 모양.</summary>
        private static RegionDressingBuilder.DiscPlacement Puddle(Vector3 center, float s, float yaw)
            => new RegionDressingBuilder.DiscPlacement(center, s, s * 0.85f, yaw, RegionDressingBuilder.PatchSegments);

        [Test]
        public void IsOnWater_BeforeRecord_KnowsOnlyTheLake()
        {
            // 빌드가 물을 다 적기 전엔 호수(리전 데이터의 순수 함수)만 안다 — 반쯤 적힌 웅덩이는 아직 없는 것으로 친다
            RegionDressingBuilder.AddWaterDisc(Puddle(PuddleCenter, 5f, 20f));
            Assert.IsFalse(RegionDressingBuilder.WaterRecorded);
            Assert.IsFalse(InsectSpawner.IsOnWater(PuddleCenter), "기록이 끝나기 전 웅덩이를 물로 쳤다");
            Assert.IsTrue(InsectSpawner.IsOnWater(PondLake().water.center), "기록 전인데 호수를 모른다");
        }

        [Test]
        public void IsOnWater_AfterRecord_SeesPuddlesRiverAndCylinders_AndOnlyWhatWasRecorded()
        {
            var puddle = Puddle(PuddleCenter, 5f, 20f);
            RegionDressingBuilder.AddWaterDisc(puddle);
            Vector3 river = new Vector3(40f, 0.14f, -600f);
            RegionDressingBuilder.AddWaterStrip(river, new Vector3(1f, 0f, 1f), 13f, 2.5f);   // 26×5m, 45° 기울어짐
            Vector3 barrel = new Vector3(-700f, 0.02f, 80f);
            RegionDressingBuilder.AddWaterCircle(barrel, 1.5f);
            RegionDressingBuilder.MarkWaterRecorded();
            Assert.AreEqual(3, RegionDressingBuilder.WaterShapeCount);

            float m = InsectSpawner.WaterShoreMargin;
            // 웅덩이 — 윤곽 + 물가 여유
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector3 edge = puddle.EdgePoint(a, 1f);
                Vector3 outward = new Vector3(edge.x - puddle.center.x, 0f, edge.z - puddle.center.z).normalized;
                Assert.IsTrue(InsectSpawner.IsOnWater(edge + outward * (m * 0.5f)), $"웅덩이 물가 여유 안 ({i})");
                Assert.IsFalse(InsectSpawner.IsOnWater(edge + outward * (m + 0.3f)), $"웅덩이 물가 여유 밖 ({i})");
            }
            // 강 — 길이 방향 끝과 옆 둑
            Vector3 along = new Vector3(1f, 0f, 1f).normalized, across = new Vector3(along.z, 0f, -along.x);
            Assert.IsTrue(InsectSpawner.IsOnWater(river + along * 12f));
            Assert.IsTrue(InsectSpawner.IsOnWater(river + across * (2.5f + m * 0.5f)));
            Assert.IsFalse(InsectSpawner.IsOnWater(river + across * (2.5f + m + 0.3f)));
            Assert.IsFalse(InsectSpawner.IsOnWater(river + along * (13f + m + 0.3f)));
            // 원기둥 물(부트스트랩 Pond_Water·Swamp_Puddle_* 등)
            Assert.IsTrue(InsectSpawner.IsOnWater(barrel + Vector3.right * (1.5f + m * 0.5f)));
            Assert.IsFalse(InsectSpawner.IsOnWater(barrel + Vector3.right * (1.5f + m + 0.3f)));
            // 기록 뒤에는 기록만 본다 — 호수는 빌드(Pond)가 스스로 적는다. 여기선 안 적었으니 물이 아니다.
            Assert.IsFalse(InsectSpawner.IsOnWater(PondLake().water.center));
        }

        [Test]
        public void PickSpawnPosition_RecordedPuddle_IsRerolled()
        {
            RegionDressingBuilder.AddWaterDisc(Puddle(PuddleCenter, 5f, 20f));
            RegionDressingBuilder.MarkWaterRecorded();
            Vector3 dry = PuddleCenter + new Vector3(0f, 0f, 12f);

            int calls = 0;
            Vector3 p = InsectSpawner.PickSpawnPosition(() => ++calls == 1 ? PuddleCenter : dry);

            Assert.AreEqual(2, calls, "습지 웅덩이 위 후보를 버리지 않았다");
            Assert.AreEqual(dry.z, p.z, 1e-5f);
        }

        [Test]
        public void PushOutOfWater_OverlappingPuddles_KeepsOneDirectionAndLeavesBoth()
        {
            // 겹친 두 웅덩이 사이에서 도형마다 방향을 새로 잡으면 번갈아 밀리며 제자리를 돈다 — 처음 방향을 끝까지 지킨다
            Vector3 a = new Vector3(600f, 0f, -300f), b = a + new Vector3(3f, 0f, 0f);
            RegionDressingBuilder.AddWaterCircle(a, 2f);
            RegionDressingBuilder.AddWaterCircle(b, 2f);
            RegionDressingBuilder.MarkWaterRecorded();
            Vector3 start = a + new Vector3(0.5f, 0f, 0f);   // a 안(겹친 쪽) — a 중심에서 +x 방향

            Vector3 p = InsectSpawner.PushOutOfWater(start);

            Assert.IsFalse(InsectSpawner.IsOnWater(p), "겹친 웅덩이 밖으로 못 나갔다");
            Assert.AreEqual(start.z, p.z, 1e-5f, "처음 방향(+x)을 벗어났다");
            Assert.Greater(p.x, b.x + 2f, "b를 건너 반대편 물가로 나가야 한다");
        }

        [Test]
        public void PushOutOfWater_River_ExitsTowardNearBank()
        {
            Vector3 river = new Vector3(-40f, 0f, 700f);
            RegionDressingBuilder.AddWaterStrip(river, Vector3.right, 13f, 2.5f);
            RegionDressingBuilder.MarkWaterRecorded();
            Vector3 start = river + new Vector3(4f, 0f, -1f);   // 남쪽 둑에 가까운 물 위

            Vector3 p = InsectSpawner.PushOutOfWater(start);

            Assert.IsFalse(InsectSpawner.IsOnWater(p));
            Assert.AreEqual(start.x, p.x, 1e-5f, "강을 따라 흘러갔다 — 둑 쪽 수직으로 나가야 한다");
            Assert.Less(p.z, river.z - 2.5f, "먼 둑으로 건너갔다");
        }

        [Test]
        public void RecordWaterPatch_MirrorsEveryDrawnWaterPatch()
        {
            // 나루터 물가·습지 웅덩이는 난수·전초기지 자리로 정해져 빌드가 그린 자리를 그 자리에서 적는다.
            // 적는 인자가 그린 인자와 어긋나면 물 판정이 조용히 옛 모양을 본다 — 소스에서 짝을 고정한다.
            string src = ReadSource("Assets/Scripts/Core/RegionDressingBuilder.cs");
            MatchCollection drawn = Regex.Matches(src,
                @"b\.Add\(patchMesh, (\w+) \+ Vector3\.up \* [\d.]+f, (.+?), (new Vector3\([^)]*\)), (shallow|deep|pool)\);");
            Assert.GreaterOrEqual(drawn.Count, 3, "물 원반(나루터 여울·깊은 물, 습지 웅덩이)을 못 찾았다 — 이 검사가 무의미해졌다");
            foreach (Match m in drawn)
            {
                string record = $"RecordWaterPatch({m.Groups[1].Value}, {m.Groups[2].Value}, {m.Groups[3].Value});";
                StringAssert.Contains(record, src, $"그린 물({m.Groups[4].Value})을 적지 않았거나 인자가 다르다: {m.Value}");
            }
            StringAssert.Contains("AddWaterDisc(lake.water);", src, "호수를 적지 않는다 — 기록 뒤 판정에서 호수가 빠진다");
            StringAssert.Contains("RecordSceneWater();", src, "다른 빌더의 물(강·원기둥 물)을 읽지 않는다");
        }

        // ── 곤충 도주·배회 높이 ──

        [Test]
        public void GroundRise_FollowsMoundsFromTheSpawnSpot()
        {
            FieldGround.AddDome(DuneCenter, DuneAxes, 0f);
            float top = FieldGround.SurfaceY(DuneCenter.x, DuneCenter.z);   // 0.7

            // 평지에서 스폰해 사구로 도망 — 사구 높이만큼 오른다(옛 도주는 basePosition.y 고정이라 사구 속을 지났다)
            float flatBase = FieldGround.SurfaceY(DuneCenter.x + 30f, DuneCenter.z);
            Assert.AreEqual(0f, InsectEntity.GroundRise(flatBase, DuneCenter.x + 30f, DuneCenter.z), 1e-6f);
            Assert.AreEqual(top - FieldGround.FloorY, InsectEntity.GroundRise(flatBase, DuneCenter.x, DuneCenter.z), 1e-5f);

            // 사구 꼭대기에서 스폰해 평지로 — 내려온다(옛 도주는 사구 높이에 뜬 채 갔다)
            Assert.AreEqual(FieldGround.FloorY - top, InsectEntity.GroundRise(top, DuneCenter.x + 30f, DuneCenter.z), 1e-5f);

            // 서브에리어 — (2000,·,2000) 너머엔 둔덕이 없다
            float subBase = FieldGround.SurfaceY(2000f, 1992f);
            Assert.AreEqual(0f, InsectEntity.GroundRise(subBase, 2006f, 1985f), 1e-6f);
        }

        [Test]
        public void GroundRise_FleeAcrossAshMound_NeverBuriesTheBody()
        {
            // 기어다니는 도주(발 위 0.05~0.12)를 재 더미(턱 0.33m) 가로질러 한 프레임씩 — 몸 바닥이 지면 아래로 안 들어간다
            FieldGround.AddDome(AshCenter, AshAxes, 0f);
            Vector3 start = AshCenter + new Vector3(-5f, 0f, 0f);
            // 스폰 자리 = 그 자리 지면(InsectSpawner.PickSpawnPosition) — basePosition.y와 baseSurfaceY가 같다
            Vector3 spawn = InsectSpawner.PickSpawnPosition(() => start);
            float baseSurface = FieldGround.SurfaceY(spawn.x, spawn.z);
            float deepestOld = 0f;
            for (float x = start.x; x <= AshCenter.x + 5f; x += 6f * FrameDt)
            {
                float ground = FieldGround.SurfaceY(x, start.z);
                float y = spawn.y + InsectEntity.GroundRise(baseSurface, x, start.z) + 0.05f;
                Assert.AreEqual(ground + 0.05f, y, 1e-5f, $"x {x - AshCenter.x:F2}: 지면 위 0.05에 있지 않다");
                deepestOld = Mathf.Max(deepestOld, ground - (spawn.y + 0.05f));   // 옛 식: basePosition.y 고정
            }
            Assert.Greater(deepestOld, 0.9f, "시나리오가 옛 결함(재 더미 속을 지나감)을 재현하지 못한다");
        }

        // ── 필드 아이템 ──

        [Test]
        public void PickItemPosition_HoversAboveSurface_AndSkipsWater()
        {
            FieldGround.AddDome(AshCenter, AshAxes, 0f);
            // 평지 — 옛 절대 높이 0.8 그대로
            Vector3 flat = CaptureItemSpawner.PickItemPosition(() => new Vector3(AshCenter.x + 20f, 0f, AshCenter.z));
            Assert.AreEqual(0.8f, flat.y, 1e-5f, "평지 높이가 옛 값(0.8)에서 바뀌었다");
            // 재 더미 꼭대기(바닥 위 1.05) — 옛 0.8은 그 속에 묻혔다
            Vector3 onAsh = CaptureItemSpawner.PickItemPosition(() => new Vector3(AshCenter.x, 0f, AshCenter.z));
            Assert.AreEqual(FieldGround.SurfaceY(AshCenter.x, AshCenter.z) + CaptureItemSpawner.HoverHeight, onAsh.y, 1e-5f);
            Assert.Greater(onAsh.y, FieldGround.SurfaceY(AshCenter.x, AshCenter.z) + 0.5f, "재 더미에 묻혔다");

            // 호수 위 후보는 곤충과 같은 규칙으로 버린다
            RegionDressingBuilder.PondLakeLayout lake = PondLake();
            Vector3 dry = lake.water.center + new Vector3(200f, 0f, 0f);
            int calls = 0;
            Vector3 p = CaptureItemSpawner.PickItemPosition(() => ++calls == 1 ? lake.water.center : dry);
            Assert.AreEqual(2, calls, "호수 위에 아이템을 놓았다");
            Assert.AreEqual(dry.x, p.x, 1e-4f);
        }

        // ── NPC 발 높이 ──

        [Test]
        public void StandHeight_WithoutMounds_KeepsTheRayHeight()
        {
            // 마을·서브에리어·평지 — 둔덕이 없는 곳에선 옛 높이(레이가 맞은 콜라이더 윗면)에서 한 치도 안 바뀐다
            FieldGround.AddDome(DuneCenter, DuneAxes, 0f);   // 멀리 있는 둔덕은 상관없다
            foreach (float c in new[] { 0f, 0.08f, 0.093f, 0.1f, 0.3f, 2.8f })
            {
                Assert.AreEqual(c, VillagerNpc.StandHeight(c, 0f, 0f), 1e-6f, $"평지 콜라이더 {c}");
                Assert.AreEqual(c, VillagerNpc.StandHeight(c, 2003f, 1994f), 1e-6f, $"서브에리어 콜라이더 {c}");
            }
        }

        [Test]
        public void StandHeight_OnDune_FollowsTheSurface_WithoutDoubleLift()
        {
            FieldGround.AddDome(DuneCenter, DuneAxes, 0f);
            float lift = FieldGround.LiftAt(DuneCenter.x, DuneCenter.z);   // 0.6
            float x = DuneCenter.x, z = DuneCenter.z;

            // 리전 평면(0.08) 위 — 평면 + 둔덕 높이
            Assert.AreEqual(RegionPlaneY + lift, VillagerNpc.StandHeight(RegionPlaneY, x, z), 1e-5f);
            // 사구 속에 묻힌 바위(0.45) — 둔덕 윗면이 이긴다
            Assert.AreEqual(FieldGround.SurfaceY(x, z), VillagerNpc.StandHeight(0.45f, x, z), 1e-5f);
            // 사구 위로 삐져나온 바위(0.9) — 바위 윗면. 둔덕 높이를 한 번 더 얹지 않는다(0.9 + 0.6으로 뜨지 않는다)
            Assert.AreEqual(0.9f, VillagerNpc.StandHeight(0.9f, x, z), 1e-6f);
        }

        [Test]
        public void StandHeight_KidWalkingOverDune_ClimbsAndComesBackDown()
        {
            // 잡기 아이가 사구 위 곤충을 쫓아 가로지른다 — 레이(groundY)는 늘 리전 평면만 맞힌다
            FieldGround.AddDome(DuneCenter, DuneAxes, 0f);
            float peak = float.MinValue, last = 0f;
            for (float x = DuneCenter.x - 10f; x <= DuneCenter.x + 10f; x += 3.5f * FrameDt)
            {
                last = VillagerNpc.StandHeight(RegionPlaneY, x, DuneCenter.z);
                peak = Mathf.Max(peak, last);
            }
            Assert.AreEqual(RegionPlaneY + (FieldGround.SurfaceY(DuneCenter.x, DuneCenter.z) - FieldGround.FloorY), peak, 0.01f,
                "사구 꼭대기에 오르지 못했다");
            Assert.AreEqual(RegionPlaneY, last, 1e-6f, "사구를 지난 뒤 옛 높이로 내려오지 않았다");
        }

        // ── 서브에리어 방 안 스폰 ──

        // 가장 작은 방(반쪽 9m, 벽 두께 1m → 안쪽 면 8.5m) — 입구는 방 로컬 z −8
        private static readonly Vector3 RoomCenter = new Vector3(2000f, 0f, 2000f);
        private const float RoomInner = 8.5f;

        private static bool InsideRoom(Vector3 p)
            => Mathf.Abs(p.x - RoomCenter.x) < RoomInner && Mathf.Abs(p.z - RoomCenter.z) < RoomInner;

        [Test]
        public void PickContainedPosition_RejectsCandidatesBeyondTheWall()
        {
            Vector3 anchor = RoomCenter + new Vector3(0f, 0.5f, -8f);
            var rolls = new Queue<Vector3>(new[]
            {
                anchor + new Vector3(0f, 0f, -6f),    // 남쪽 벽 너머(옛 스폰이 실제로 떨어지던 곳)
                anchor + new Vector3(-9f, 0f, 3f),    // 서쪽 벽 너머
                anchor + new Vector3(3f, 0f, 5f),     // 방 안
            });
            int calls = 0;

            Vector3 p = InsectSpawner.PickContainedPosition(() => { calls++; return rolls.Dequeue(); }, anchor, InsideRoom,
                InsectSpawner.SubAreaSpawnRolls);

            Assert.AreEqual(3, calls);
            Assert.IsTrue(InsideRoom(p));
            Assert.AreEqual(anchor.x + 3f, p.x, 1e-5f);
            Assert.AreEqual(anchor.z + 5f, p.z, 1e-5f);
        }

        [Test]
        public void PickContainedPosition_AllOutside_PullsTowardThePlayerWithoutSkipping()
        {
            // 굴린 자리가 전부 벽 너머여도 스폰을 건너뛰지 않는다 — 플레이어 쪽으로 반씩 당겨 들어오는 첫 자리
            Vector3 anchor = RoomCenter + new Vector3(0f, 0.5f, -5f);           // z 1995
            Vector3 outside = new Vector3(anchor.x, 0.5f, anchor.z - 10f);       // z 1985 — 남벽 안쪽 면(1991.5) 너머
            int calls = 0;

            Vector3 p = InsectSpawner.PickContainedPosition(() => { calls++; return outside; }, anchor, InsideRoom,
                InsectSpawner.SubAreaSpawnRolls);

            Assert.AreEqual(InsectSpawner.SubAreaSpawnRolls, calls, "재추첨 상한을 지키지 않았다");
            Assert.IsTrue(InsideRoom(p), "당기고도 방 밖이다");
            Assert.AreEqual(anchor.z - 2.5f, p.z, 1e-5f, "1/2(1990)은 밖, 1/4(1992.5)이 첫 방 안 자리다");
            Assert.AreEqual(anchor.x, p.x, 1e-5f);
        }

        [Test]
        public void PickContainedPosition_NothingInside_FallsBackToThePlayerSpot()
        {
            Vector3 anchor = RoomCenter + new Vector3(0f, 0.5f, -8f);
            Vector3 p = InsectSpawner.PickContainedPosition(() => anchor + new Vector3(0f, 0f, -30f), anchor, _ => false,
                InsectSpawner.SubAreaSpawnRolls);
            Assert.AreEqual(anchor.x, p.x, 1e-6f);
            Assert.AreEqual(anchor.z, p.z, 1e-6f);
        }

        // ── 플레이어 접지 ──

        // 리전 바닥 평면 콜라이더 윗면(PlaySceneBootstrap: 0.08 + 순번 × 0.001)
        private const float RegionPlaneY = 0.08f;

        /// <summary>PlayerMovement.Update의 접지를 프레임마다 흉내 낸다 — 레이가 맞는 콜라이더는 늘 바닥 평면.</summary>
        private static float WalkAcross(Vector3 domeCenter, float halfSpan, float speed, List<float> trace)
        {
            float y = FieldGround.FloorY;
            for (float x = domeCenter.x - halfSpan; x <= domeCenter.x + halfSpan; x += speed * FrameDt)
            {
                float ground = PlayerMovement.GroundHeight(RegionPlaneY, x, domeCenter.z, false);
                y = PlayerMovement.FollowGround(y, ground, FrameDt);
                trace.Add(y);
            }
            return y;
        }

        [Test]
        public void Grounding_WalkOverDune_ClimbsAndComesBackDown()
        {
            // 옛 접지 Max(pos.y, hit.y)는 올라가기만 했다 — 둔덕을 지나면 그 높이에 뜬 채 남았다
            FieldGround.AddDome(DuneCenter, DuneAxes, 0f);
            var trace = new List<float>();
            float end = WalkAcross(DuneCenter, 10f, 16f, trace);   // 의상 배율 2배 최고 속도

            float top = FieldGround.SurfaceY(DuneCenter.x, DuneCenter.z);
            float peak = Mathf.Max(trace.ToArray());
            Assert.AreEqual(top, peak, 0.03f, "사구 꼭대기에 오르지 못했다");
            Assert.AreEqual(FieldGround.FloorY, end, 1e-5f, "사구를 지난 뒤 바닥으로 내려오지 않았다");
        }

        [Test]
        public void Grounding_AshMoundRimStep_IsSpreadOverFrames()
        {
            // 재 더미 가장자리 0.33m 턱 — 한 프레임에 몸·카메라 시선이 튀지 않게 속도 상한으로 나눈다
            FieldGround.AddDome(AshCenter, AshAxes, 0f);
            var trace = new List<float>();
            float end = WalkAcross(AshCenter, 5f, 8f, trace);

            float maxStep = 0f;
            for (int i = 1; i < trace.Count; i++) maxStep = Mathf.Max(maxStep, Mathf.Abs(trace[i] - trace[i - 1]));
            Assert.LessOrEqual(maxStep, PlayerMovement.GroundFollowSpeed * FrameDt + 1e-4f, "턱에서 한 프레임에 튄다");
            Assert.Greater(Mathf.Max(trace.ToArray()), FieldGround.FloorY + 0.8f, "재 더미 위로 오르지 못했다");
            Assert.AreEqual(FieldGround.FloorY, end, 1e-5f, "재 더미를 지난 뒤 내려오지 않았다");
        }

        [Test]
        public void Grounding_OverLowRoundRock_ComesBackDown()
        {
            // 물가 바위(Pond_ShoreRock_, rs 0.9): 구 콜라이더 반경 0.6rs = 0.54, 중심 y 0.2rs = 0.18 → 꼭대기 0.72.
            // 몸통 검사(발 위 1.0~1.8m)엔 안 걸리고, 접지 레이(발 위 한 걸음)가 가장자리부터 차례로 맞혀 타고 오른다.
            // 옛 식 Max(pos.y, hit.y)는 거기서 내려오지 않아 바위를 지난 뒤에도 0.72에 떠 있었다.
            const float radius = 0.54f, centerY = 0.18f;
            float newY = FieldGround.FloorY, oldY = FieldGround.FloorY;
            float peak = 0f;
            for (float x = -3f; x <= 3f; x += 8f * FrameDt)
            {
                float rockTop = Mathf.Abs(x) < radius ? centerY + Mathf.Sqrt(radius * radius - x * x) : float.NegativeInfinity;
                // 레이는 발 위 한 걸음에서 출발한다 — 그보다 높은 윗면은 레이가 콜라이더 안에서 출발해 못 본다
                float hitNew = rockTop <= newY + PlayerMovement.StepHeight ? Mathf.Max(rockTop, RegionPlaneY) : RegionPlaneY;
                float hitOld = rockTop <= oldY + PlayerMovement.StepHeight ? Mathf.Max(rockTop, RegionPlaneY) : RegionPlaneY;

                newY = PlayerMovement.FollowGround(newY, PlayerMovement.GroundHeight(hitNew, x, 0f, false), FrameDt);
                oldY = Mathf.Max(oldY, hitOld);
                peak = Mathf.Max(peak, newY);
            }

            Assert.Greater(oldY, 0.6f, "시나리오가 옛 결함(바위 높이에 뜬 채 남음)을 재현하지 못한다");
            Assert.Greater(peak, FieldGround.FloorY + 0.3f, "바위를 타고 오르지 못했다 — 한 걸음 높이 안의 콜라이더는 밟는다");
            Assert.AreEqual(FieldGround.FloorY, newY, 1e-5f, "바위를 지난 뒤 바닥으로 내려오지 않았다");
        }

        [Test]
        public void GroundHeight_StandingOnDune_MatchesInsectSpawnHeight()
        {
            // 같은 자리에서 플레이어 발과 곤충이 같은 지면을 본다(둘 다 FieldGround)
            FieldGround.AddDome(DuneCenter, DuneAxes, 0f);
            Vector3 spot = new Vector3(DuneCenter.x - 2f, 0f, DuneCenter.z + 1f);
            float playerY = PlayerMovement.GroundHeight(RegionPlaneY, spot.x, spot.z, false);
            Vector3 insect = InsectSpawner.PickSpawnPosition(() => spot);
            Assert.AreEqual(insect.y, playerY, 1e-5f);
            Assert.Greater(playerY, FieldGround.FloorY + 0.4f);
        }

        [Test]
        public void GroundHeight_CollidersAndMoundsCompareInWorldFrame()
        {
            FieldGround.AddDome(DuneCenter, DuneAxes, 0f);
            float onDune = FieldGround.SurfaceY(DuneCenter.x, DuneCenter.z);   // 0.7

            // 평지 — 리전 평면(0.08)보다 FieldGround 바닥(0.1)이 높다: 옛 시작 높이 그대로
            Assert.AreEqual(FieldGround.FloorY, PlayerMovement.GroundHeight(RegionPlaneY, 0f, 0f, false), 1e-6f);
            // 콜라이더를 못 맞혔을 때(음의 무한대)도 둔덕·바닥은 남는다
            Assert.AreEqual(onDune, PlayerMovement.GroundHeight(float.NegativeInfinity, DuneCenter.x, DuneCenter.z, false), 1e-6f);
            // 평지 위 낮은 콜라이더(0.3)는 밟는다
            Assert.AreEqual(0.3f, PlayerMovement.GroundHeight(0.3f, 0f, 0f, false), 1e-6f);
            // 사구 속에 묻힌 바위 윗면(0.45)은 사구 윗면이 이긴다 — 둔덕 높이를 한 번 더 얹어 뜨지 않는다
            Assert.AreEqual(onDune, PlayerMovement.GroundHeight(0.45f, DuneCenter.x, DuneCenter.z, false), 1e-6f);
            // 사구 위로 삐져나온 바위(0.9)는 밟는다
            Assert.AreEqual(0.9f, PlayerMovement.GroundHeight(0.9f, DuneCenter.x, DuneCenter.z, false), 1e-6f);
            // 서브에리어 — 둔덕도 FieldGround 바닥(0.1)도 없다. 바닥 y=0에 선다(예전엔 입구 텔레포트 y 0.5에 뜬 채 남았다)
            Assert.AreEqual(0f, PlayerMovement.GroundHeight(0f, 2000f, 1992f, true), 1e-6f);
        }

        [Test]
        public void FollowGround_LimitsRateBothWaysAndConverges()
        {
            // 재 더미 가장자리 턱(0.33m)을 한 프레임에 넘지 않는다
            float y = PlayerMovement.FollowGround(0.1f, 0.43f, FrameDt);
            Assert.Less(y, 0.43f, "턱을 한 프레임에 넘었다");
            for (int i = 0; i < 10; i++) y = PlayerMovement.FollowGround(y, 0.43f, FrameDt);
            Assert.AreEqual(0.43f, y, 1e-6f, "오르막 목표에 수렴하지 않는다");

            // 내리막도 따라간다(옛 접지는 여기서 멈췄다)
            for (int i = 0; i < 10; i++) y = PlayerMovement.FollowGround(y, 0.1f, FrameDt);
            Assert.AreEqual(0.1f, y, 1e-6f, "내리막을 따라 내려오지 않는다");

            Assert.AreEqual(0.43f, PlayerMovement.FollowGround(0.43f, 0.1f, 0f), 1e-6f, "일시정지(dt 0)에 움직였다");
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
