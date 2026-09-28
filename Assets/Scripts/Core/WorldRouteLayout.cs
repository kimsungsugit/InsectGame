using System.Collections.Generic;
using System.Runtime.CompilerServices;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    public readonly struct WorldRouteEdge
    {
        public string FromRegionId { get; }
        public string ToRegionId { get; }
        public WorldRouteEdge(string from, string to) { FromRegionId = from; ToRegionId = to; }
    }

    /// <summary>필드와 지도가 공유하는 길. 잠긴 지역을 관통하지 않고 각 지역의 한 입구를 공유한다.</summary>
    public static class WorldRouteLayout
    {
        public const float RoadWidth = 2.5f;
        public const float CorridorClearance = 2f;
        private const float OuterOffset = 4f;
        private const int RingSamples = 32;
        public static IReadOnlyList<WorldRouteEdge> FieldConnections { get; } = new[] {
            new WorldRouteEdge("meadow", "pond"), new WorldRouteEdge("meadow", "swamp"),
            new WorldRouteEdge("meadow", "garden"), new WorldRouteEdge("pond", "forest"),
            new WorldRouteEdge("forest", "swamp"), new WorldRouteEdge("swamp", "mountain"),
            new WorldRouteEdge("mountain", "ruins"), new WorldRouteEdge("ruins", "hollow"),
            new WorldRouteEdge("hollow", "dunes"), new WorldRouteEdge("dunes", "frostline"),
            new WorldRouteEdge("frostline", "emberfall"), new WorldRouteEdge("emberfall", "canopy"),
            new WorldRouteEdge("canopy", "nameless") };

        public static RegionData Find(RegionData[] regions, string id)
        {
            if (regions != null) foreach (RegionData region in regions)
                if (region != null && region.regionId == id) return region;
            return null;
        }

        public static Vector3 GetGateway(RegionData region, RegionData[] regions)
        {
            Vector3 direction = Vector3.right;
            float closest = float.MaxValue;
            foreach (RegionData other in regions)
            {
                if (other == null || other == region) continue;
                Vector3 delta = other.centerPosition - region.centerPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude < closest) { closest = delta.sqrMagnitude; direction = delta.normalized; }
            }
            if (region.connections != null && region.connections.Length > 0 && region.connections[0] != null)
            {
                float angle = region.connections[0].gatewayAngle * Mathf.Deg2Rad;
                direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            // 가장 가까운 방향부터 좌우로 조사한다. 겹치는 지역의 잠금 안으로 입구를 내지 않는다.
            Vector3 preferred = direction;
            for (int step = 0; step <= 72; step++)
            {
                float degrees = ((step + 1) / 2) * 5f * (step % 2 == 0 ? -1f : 1f);
                direction = Quaternion.Euler(0f, degrees, 0f) * preferred;
                Vector3 outer = region.centerPosition + direction * (region.radius + OuterOffset);
                if (Clear(region.centerPosition, outer, regions, region) &&
                    (region.regionId != "meadow" || DistanceToSegment(
                        VillageBuilder.GetMainVillageCenter(region.centerPosition, region.radius),
                        region.centerPosition, outer) > VillageBuilder.MainVillageFootprintRadius + CorridorClearance))
                    return region.centerPosition + direction * (region.radius - 1f);
            }
            return region.centerPosition + preferred * (region.radius - 1f);
        }

        private sealed class RouteCache
        {
            public int Stamp;
            public readonly Dictionary<string, Vector3[]> Routes = new Dictionary<string, Vector3[]>();
        }
        private static readonly ConditionalWeakTable<RegionData[], RouteCache> routeCaches =
            new ConditionalWeakTable<RegionData[], RouteCache>();

        public static Vector3[] BuildRoute(RegionData[] regions, string fromId, string toId)
        {
            if (regions == null) return new Vector3[0];
            int stamp = 17;
            unchecked
            {
                foreach (RegionData region in regions)
                {
                    if (region == null) continue;
                    stamp = stamp * 31 + region.centerPosition.GetHashCode();
                    stamp = stamp * 31 + region.radius.GetHashCode();
                    if (region.connections != null) foreach (RegionConnection connection in region.connections)
                        if (connection != null) stamp = stamp * 31 + connection.gatewayAngle.GetHashCode();
                }
            }
            RouteCache cache = routeCaches.GetValue(regions, _ => new RouteCache());
            if (cache.Stamp != stamp) { cache.Stamp = stamp; cache.Routes.Clear(); }
            string key = fromId + ":" + toId;
            if (!cache.Routes.TryGetValue(key, out Vector3[] route))
            {
                route = CalculateRoute(regions, fromId, toId);
                cache.Routes[key] = route;
            }
            return route;
        }

        private static Vector3[] CalculateRoute(RegionData[] regions, string fromId, string toId)
        {
            RegionData from = Find(regions, fromId), to = Find(regions, toId);
            if (from == null || to == null) return new Vector3[0];
            Vector3 gateA = GetGateway(from, regions), gateB = GetGateway(to, regions);
            Vector3 outerA = from.centerPosition + (gateA - from.centerPosition).normalized * (from.radius + OuterOffset);
            Vector3 outerB = to.centerPosition + (gateB - to.centerPosition).normalized * (to.radius + OuterOffset);
            var nodes = new List<Vector3> { outerA, outerB };
            foreach (RegionData region in regions)
            {
                if (region == null) continue;
                for (int i = 0; i < RingSamples; i++)
                {
                    float angle = i * Mathf.PI * 2f / RingSamples;
                    Vector3 point = region.centerPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (region.radius + OuterOffset);
                    if (Clear(point, point, regions, null)) nodes.Add(point);
                }
            }
            int count = nodes.Count;
            var distance = new float[count]; var previous = new int[count]; var visited = new bool[count];
            for (int i = 0; i < count; i++) { distance[i] = float.MaxValue; previous[i] = -1; }
            distance[0] = 0f;
            for (int step = 0; step < count; step++)
            {
                int best = -1;
                for (int i = 0; i < count; i++)
                    if (!visited[i] && (best < 0 || distance[i] < distance[best])) best = i;
                if (best < 0 || distance[best] == float.MaxValue) break;
                if (best == 1) break;
                visited[best] = true;
                for (int next = 0; next < count; next++)
                {
                    if (visited[next]) continue;
                    float candidate = distance[best] + Vector3.Distance(nodes[best], nodes[next]);
                    if (candidate >= distance[next] || !Clear(nodes[best], nodes[next], regions, null)) continue;
                    distance[next] = candidate; previous[next] = best;
                }
            }
            if (previous[1] < 0) return new Vector3[0]; // 막힌 직선을 그리는 대신 검증에서 누락을 드러낸다.
            var exterior = new List<Vector3>();
            for (int node = 1; node >= 0; node = previous[node]) exterior.Add(nodes[node]);
            exterior.Reverse();
            var route = new List<Vector3> { from.centerPosition, gateA };
            route.AddRange(exterior); route.Add(gateB); route.Add(to.centerPosition);
            return route.ToArray();
        }

        public static bool IsOnRoute(RegionData[] regions, Vector3 point, float objectRadius = 0f)
        {
            foreach (WorldRouteEdge edge in FieldConnections)
            {
                Vector3[] route = BuildRoute(regions, edge.FromRegionId, edge.ToRegionId);
                for (int i = 1; i < route.Length; i++)
                    if (DistanceToSegment(point, route[i - 1], route[i]) < RoadWidth * 0.5f + objectRadius + 0.5f)
                        return true;
            }
            RegionData meadow = Find(regions, "meadow");
            return meadow != null && DistanceToSegment(point, meadow.centerPosition,
                PlayerStartPlacement.ResolveMainVillageEntrance(regions).Position) < RoadWidth * 0.5f + objectRadius + 0.5f;
        }

        public static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            point.y = from.y = to.y = 0f;
            Vector3 delta = to - from;
            float t = delta.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(point - from, delta) / delta.sqrMagnitude) : 0f;
            return Vector3.Distance(point, from + delta * t);
        }

        private static bool Clear(Vector3 from, Vector3 to, RegionData[] regions, RegionData ignored)
        {
            foreach (RegionData region in regions)
                if (region != null && region != ignored &&
                    DistanceToSegment(region.centerPosition, from, to) < region.radius + CorridorClearance) return false;
            return true;
        }
    }
}
