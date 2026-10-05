#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// <see cref="IslandGrid"/> — 섬 격자의 차지 칸·경계·겹침·도착 칸 보호.
    /// 배치 규칙이 틀리면 증상이 조용하다: 건물이 겹쳐 놓이거나, 나루터 앞이 막혀 섬에 들어오자마자 갇힌다.
    /// </summary>
    [TestFixture]
    public class IslandGridTests
    {
        private static IslandObjectDef Def(string id) => IslandCatalog.Get(id);

        private static IslandPlacedRecord Placed(string id, int x, int z, int rot = 0)
            => new IslandPlacedRecord { id = id, x = x, z = z, rot = rot };

        [TestCase(0, 10)]
        [TestCase(1, 14)]
        [TestCase(2, 18)]
        [TestCase(3, 22)]
        public void GridSize_PerLevel_GrowsByFourAndStaysEven(int level, int expected)
        {
            Assert.AreEqual(expected, IslandGrid.GridSize(level));
            Assert.AreEqual(0, IslandGrid.GridSize(level) % 2, "한 변이 홀수면 섬 중심이 칸 한가운데로 가 좌표가 어긋난다");
        }

        [Test]
        public void GridSize_OutOfRangeLevel_IsClamped()
        {
            Assert.AreEqual(IslandGrid.GridSize(0), IslandGrid.GridSize(-5));
            Assert.AreEqual(IslandGrid.GridSize(GameConstants.Island.MaxSizeLevel), IslandGrid.GridSize(99));
        }

        [TestCase(0, 2, 1)]
        [TestCase(1, 1, 2)]
        [TestCase(2, 2, 1)]
        [TestCase(3, 1, 2)]
        [TestCase(-1, 1, 2)]
        public void Footprint_Rotation_SwapsWidthAndDepthOnOddTurns(int rot, int width, int depth)
        {
            IslandGrid.Footprint(Def("f_bench"), rot, out int w, out int d);
            Assert.AreEqual(width, w);
            Assert.AreEqual(depth, d);
        }

        [Test]
        public void CanPlace_EmptyIsland_AcceptsInteriorCell()
        {
            Assert.AreEqual(IslandPlaceResult.Ok,
                IslandGrid.CanPlace(new List<IslandPlacedRecord>(), -1, Def("f_bench"), 0, 0, 0, 0));
        }

        [Test]
        public void CanPlace_UnknownObject_IsRejected()
        {
            Assert.AreEqual(IslandPlaceResult.UnknownObject,
                IslandGrid.CanPlace(new List<IslandPlacedRecord>(), -1, null, 0, 0, 0, 0));
        }

        [Test]
        public void CanPlace_FootprintCrossingEdge_IsOutOfBounds()
        {
            // 10칸 섬의 칸 번호는 -5 ~ 4. 2칸짜리를 x=4에 두면 5번 칸까지 뻗는다.
            Assert.AreEqual(IslandPlaceResult.OutOfBounds,
                IslandGrid.CanPlace(null, -1, Def("f_bench"), 4, 0, 0, 0));
            Assert.AreEqual(IslandPlaceResult.Ok,
                IslandGrid.CanPlace(null, -1, Def("f_bench"), 3, 0, 0, 0));
            Assert.AreEqual(IslandPlaceResult.OutOfBounds,
                IslandGrid.CanPlace(null, -1, Def("f_bench"), -6, 0, 0, 0));
        }

        [Test]
        public void CanPlace_OnArrivalCells_IsReserved()
        {
            // 도착 칸은 남쪽 끝 가운데 2×2(x -1~0, z -5~-4). 여길 막으면 들어오자마자 갇힌다.
            Assert.AreEqual(IslandPlaceResult.Reserved,
                IslandGrid.CanPlace(null, -1, Def("t_rock"), 0, -5, 0, 0));
            Assert.AreEqual(IslandPlaceResult.Reserved,
                IslandGrid.CanPlace(null, -1, Def("t_rock"), -1, -4, 0, 0));
            Assert.AreEqual(IslandPlaceResult.Ok,
                IslandGrid.CanPlace(null, -1, Def("t_rock"), 1, -5, 0, 0));
        }

        [Test]
        public void ArrivalPoint_IsInsideTheReservedCells()
        {
            for (int level = 0; level <= GameConstants.Island.MaxSizeLevel; level++)
            {
                IslandGrid.CellAt(IslandGrid.ArrivalPoint(level) + new Vector3(0.01f, 0f, 0.01f), out int x, out int z);
                Assert.IsTrue(IslandGrid.TouchesArrival(x, z, 1, 1, IslandGrid.GridSize(level)),
                    $"섬 크기 {level}: 도착 자리가 보호된 칸 밖이다 — 그 자리에 물건을 놓을 수 있게 된다");
            }
        }

        [Test]
        public void CanPlace_OverlappingExisting_IsOverlap()
        {
            var placed = new List<IslandPlacedRecord> { Placed("b_cabin", 0, 0) };   // 3×3 → (0..2, 0..2)
            Assert.AreEqual(IslandPlaceResult.Overlap, IslandGrid.CanPlace(placed, -1, Def("t_rock"), 2, 2, 0, 0));
            Assert.AreEqual(IslandPlaceResult.Ok, IslandGrid.CanPlace(placed, -1, Def("t_rock"), 3, 2, 0, 0));
        }

        [Test]
        public void CanPlace_MovingObject_IgnoresItsOwnFootprint()
        {
            var placed = new List<IslandPlacedRecord> { Placed("b_cabin", 0, 0) };
            // 한 칸 옆으로 옮기면 자기 옛 자리와 겹친다 — 자기 자신은 빼야 옮길 수 있다.
            Assert.AreEqual(IslandPlaceResult.Ok, IslandGrid.CanPlace(placed, 0, Def("b_cabin"), 1, 0, 0, 0));
            Assert.AreEqual(IslandPlaceResult.Overlap, IslandGrid.CanPlace(placed, -1, Def("b_cabin"), 1, 0, 0, 0));
        }

        [Test]
        public void CanPlace_UnknownExistingRecord_DoesNotBlock()
        {
            // 더 새 버전에서 산 물건(카탈로그에 없는 id)이 자리를 영영 막으면 안 된다.
            var placed = new List<IslandPlacedRecord> { Placed("x_from_future", 0, 0) };
            Assert.AreEqual(IslandPlaceResult.Ok, IslandGrid.CanPlace(placed, -1, Def("t_rock"), 0, 0, 0, 0));
        }

        [Test]
        public void CanPlace_AfterExpansion_OldCoordinatesStayValid()
        {
            // 좌표가 섬 중심 기준이라 넓혀도 기존 배치가 그대로 유효하다(마이그레이션 없음).
            var def = Def("b_cabin");
            Assert.AreEqual(IslandPlaceResult.Ok, IslandGrid.CanPlace(null, -1, def, -5, 2, 0, 0));
            Assert.AreEqual(IslandPlaceResult.Ok, IslandGrid.CanPlace(null, -1, def, -5, 2, 0, 3));
            // 넓힌 섬에서만 되는 자리
            Assert.AreEqual(IslandPlaceResult.OutOfBounds, IslandGrid.CanPlace(null, -1, def, -7, 2, 0, 0));
            Assert.AreEqual(IslandPlaceResult.Ok, IslandGrid.CanPlace(null, -1, def, -7, 2, 0, 1));
        }

        [Test]
        public void FindAt_ReturnsIndexCoveringTheCell()
        {
            var placed = new List<IslandPlacedRecord> { Placed("t_rock", -3, -3), Placed("f_bench", 1, 1, 1) };
            Assert.AreEqual(0, IslandGrid.FindAt(placed, -3, -3));
            // 벤치(2×1)를 90° 돌리면 1×2 → (1,1)과 (1,2)
            Assert.AreEqual(1, IslandGrid.FindAt(placed, 1, 2));
            Assert.AreEqual(-1, IslandGrid.FindAt(placed, 2, 1));
        }

        [Test]
        public void CellAt_FootprintCenter_RoundTrips()
        {
            for (int x = -5; x < 5; x++)
            {
                IslandGrid.CellAt(IslandGrid.FootprintCenter(x, -2, 1, 1), out int cx, out int cz);
                Assert.AreEqual(x, cx);
                Assert.AreEqual(-2, cz);
            }
        }

        [Test]
        public void CollectFreeCells_SkipsBlockersAndArrival_ButKeepsWalkableObjects()
        {
            var placed = new List<IslandPlacedRecord>
            {
                Placed("t_boulder", 0, 0),     // 2×2, 막는다
                Placed("t_flowerbed", -4, 0),  // 2×2, 지나다닐 수 있다
            };
            var cells = new List<Vector2Int>();
            IslandGrid.CollectFreeCells(placed, 0, cells);

            int total = 10 * 10;
            Assert.AreEqual(total - 4 /*바위*/ - 4 /*도착 칸*/, cells.Count);
            CollectionAssert.DoesNotContain(cells, new Vector2Int(1, 1));
            CollectionAssert.DoesNotContain(cells, new Vector2Int(0, -5));
            CollectionAssert.Contains(cells, new Vector2Int(-4, 0));
        }
    }
}
#endif
