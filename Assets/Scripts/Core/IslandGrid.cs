using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>배치가 거부된 이유 — UI가 문구로 바꾼다.</summary>
    public enum IslandPlaceResult
    {
        Ok,
        UnknownObject,
        OutOfBounds,
        Reserved,
        Overlap,
        NotOwned,
    }

    /// <summary>
    /// 섬 격자의 <b>순수</b> 규칙 — 차지 칸·경계·겹침·도착 칸 보호. MonoBehaviour와 떼어 놓아 테스트로 고정한다
    /// (<see cref="QuestRegionGate"/>와 같은 성격).
    ///
    /// 좌표는 섬 중심이 원점이고 한 변이 짝수 칸이다 — 칸 번호는 <c>-half</c>부터 <c>half - 1</c>까지.
    /// 중심 기준이라 섬을 넓혀도 놓아둔 물건의 좌표가 그대로 유효하다(마이그레이션이 없다).
    /// </summary>
    public static class IslandGrid
    {
        /// <summary>섬 한 변의 칸 수.</summary>
        public static int GridSize(int sizeLevel)
        {
            int level = Mathf.Clamp(sizeLevel, 0, GameConstants.Island.MaxSizeLevel);
            return GameConstants.Island.BaseGridSize + level * GameConstants.Island.GridSizeStep;
        }

        /// <summary>회전을 반영한 차지 칸. 90°·270°면 가로·세로가 바뀐다.</summary>
        public static void Footprint(IslandObjectDef def, int rot, out int width, out int depth)
        {
            bool swap = (((rot % 4) + 4) % 4) % 2 == 1;
            width = swap ? def.depth : def.width;
            depth = swap ? def.width : def.depth;
        }

        public static bool InBounds(int x, int z, int width, int depth, int gridSize)
        {
            int half = gridSize / 2;
            return x >= -half && z >= -half && x + width <= half && z + depth <= half;
        }

        /// <summary>
        /// 도착 칸(나루터 앞 2×2)과 겹치는가. <b>여길 막으면 섬에 들어오자마자 갇힌다</b> —
        /// 캐릭터가 서는 자리이고, 끼임 복구도 이 자리로 보낸다.
        /// </summary>
        public static bool TouchesArrival(int x, int z, int width, int depth, int gridSize)
        {
            int half = gridSize / 2;
            return RectsOverlap(x, z, width, depth, -1, -half, 2, 2);
        }

        public static bool RectsOverlap(int ax, int az, int aw, int ad, int bx, int bz, int bw, int bd)
        {
            return ax < bx + bw && bx < ax + aw && az < bz + bd && bz < az + ad;
        }

        /// <summary>
        /// 이 자리에 놓을 수 있는가. <paramref name="ignoreIndex"/>는 옮기는 중인 물건 자신(겹침 검사에서 뺀다, 없으면 -1).
        /// 카탈로그에 없는 id의 기존 배치는 겹침 검사에서도 무시한다 — 구버전이 모르는 물건이 자리를 영영 막지 않게.
        /// </summary>
        public static IslandPlaceResult CanPlace(IReadOnlyList<IslandPlacedRecord> placed, int ignoreIndex,
            IslandObjectDef def, int x, int z, int rot, int sizeLevel)
        {
            if (def == null) return IslandPlaceResult.UnknownObject;
            int size = GridSize(sizeLevel);
            Footprint(def, rot, out int w, out int d);
            if (!InBounds(x, z, w, d, size)) return IslandPlaceResult.OutOfBounds;
            if (TouchesArrival(x, z, w, d, size)) return IslandPlaceResult.Reserved;

            if (placed != null)
            {
                for (int i = 0; i < placed.Count; i++)
                {
                    if (i == ignoreIndex) continue;
                    IslandPlacedRecord other = placed[i];
                    if (other == null) continue;
                    IslandObjectDef otherDef = IslandCatalog.Get(other.id);
                    if (otherDef == null) continue;
                    Footprint(otherDef, other.rot, out int ow, out int od);
                    if (RectsOverlap(x, z, w, d, other.x, other.z, ow, od)) return IslandPlaceResult.Overlap;
                }
            }
            return IslandPlaceResult.Ok;
        }

        /// <summary>그 칸을 덮고 있는 배치의 인덱스. 없으면 -1.</summary>
        public static int FindAt(IReadOnlyList<IslandPlacedRecord> placed, int cellX, int cellZ)
        {
            if (placed == null) return -1;
            for (int i = 0; i < placed.Count; i++)
            {
                IslandPlacedRecord p = placed[i];
                if (p == null) continue;
                IslandObjectDef def = IslandCatalog.Get(p.id);
                if (def == null) continue;
                Footprint(def, p.rot, out int w, out int d);
                if (cellX >= p.x && cellX < p.x + w && cellZ >= p.z && cellZ < p.z + d) return i;
            }
            return -1;
        }

        /// <summary>차지 영역의 중심(섬 원점 기준, y = 0).</summary>
        public static Vector3 FootprintCenter(int x, int z, int width, int depth)
        {
            float cs = GameConstants.Island.CellSize;
            return new Vector3((x + width * 0.5f) * cs, 0f, (z + depth * 0.5f) * cs);
        }

        /// <summary>섬 원점 기준 좌표가 속한 칸.</summary>
        public static void CellAt(Vector3 local, out int cellX, out int cellZ)
        {
            float cs = GameConstants.Island.CellSize;
            cellX = Mathf.FloorToInt(local.x / cs);
            cellZ = Mathf.FloorToInt(local.z / cs);
        }

        /// <summary>캐릭터가 도착하는 자리(섬 원점 기준) — 남쪽 가장자리 가운데, 보호된 2×2의 중심.</summary>
        public static Vector3 ArrivalPoint(int sizeLevel)
        {
            int half = GridSize(sizeLevel) / 2;
            return new Vector3(0f, 0f, (-half + 1) * GameConstants.Island.CellSize);
        }

        /// <summary>섬 땅의 반 변 길이(m).</summary>
        public static float HalfExtent(int sizeLevel)
        {
            return GridSize(sizeLevel) * 0.5f * GameConstants.Island.CellSize;
        }

        /// <summary>
        /// 비어 있는 칸 목록(도착 칸 제외) — 방목 곤충이 돌아다닐 자리.
        /// 지나갈 수 있는 물건(꽃밭·덤불) 위는 빈 칸으로 친다.
        /// </summary>
        public static void CollectFreeCells(IReadOnlyList<IslandPlacedRecord> placed, int sizeLevel,
            List<Vector2Int> result)
        {
            result.Clear();
            int size = GridSize(sizeLevel);
            int half = size / 2;
            for (int z = -half; z < half; z++)
            {
                for (int x = -half; x < half; x++)
                {
                    if (TouchesArrival(x, z, 1, 1, size)) continue;
                    int at = FindAt(placed, x, z);
                    if (at >= 0)
                    {
                        IslandObjectDef def = IslandCatalog.Get(placed[at].id);
                        if (def != null && def.blocksMovement) continue;
                    }
                    result.Add(new Vector2Int(x, z));
                }
            }
        }
    }
}
