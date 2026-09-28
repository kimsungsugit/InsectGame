#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    public class WorldRouteLayoutTests
    {
        [Test]
        public void GatewaySigns_ReuseOneMarkerPerRegionAndKeepLaneClear()
        {
            var before = new System.Collections.Generic.HashSet<int>();
            foreach (GameObject existing in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                before.Add(existing.GetInstanceID());
            GameObject host = new GameObject("GatewaySignsTest");
            try
            {
                RegionData[] regions = RegionDefinitions.CreateAll();
                host.AddComponent<RegionTerrainBuilder>().BuildBoundaries(regions);
                int signs = 0;
                foreach (RegionData region in regions)
                {
                    GameObject marker = GameObject.Find("GatewayMarker_" + region.regionId + "_0");
                    Assert.IsNotNull(marker, region.regionId);
                    signs++;
                    Assert.AreEqual(4, marker.transform.childCount, "기존 표지마다 네 장식 자식만 추가한다");
                    foreach (Collider collider in marker.GetComponentsInChildren<Collider>())
                        Assert.IsFalse(collider.enabled, "표지판이 이동 통로를 막으면 안 된다");
                    Transform board = marker.transform.Find("WayfindingBoard");
                    Assert.Less(marker.GetComponent<MeshRenderer>().bounds.max.y,
                        board.GetComponent<MeshRenderer>().bounds.min.y, "기둥이 표지판의 글자를 가리면 안 된다");
                    Vector3 gateway = WorldRouteLayout.GetGateway(region, regions);
                    Vector3 outward = (gateway - region.centerPosition).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, outward).normalized;
                    float centerOffset = Mathf.Abs(Vector3.Dot(board.position - gateway, right));
                    Assert.Greater(centerOffset - board.lossyScale.x * 0.5f,
                        WorldRouteLayout.RoadWidth * 0.5f + 0.4f, region.regionId);
                    TextMesh inside = marker.transform.Find("DestinationsInside").GetComponent<TextMesh>();
                    Assert.That(inside.text, Does.StartWith(region.displayName));
                    Assert.IsNotNull(inside.font);
                    Assert.AreEqual(inside.font.material, inside.GetComponent<MeshRenderer>().sharedMaterial);
                    Assert.AreEqual("InsectGame/GatewayText", inside.font.material.shader.name);
                    Vector3 textBounds = inside.GetComponent<MeshRenderer>().localBounds.size;
                    Assert.LessOrEqual(textBounds.x, 2.86f);
                    Assert.LessOrEqual(textBounds.y, 1.43f);
                    Assert.Greater(textBounds.y, 0.3f, "글자가 판에 비해 지나치게 작아서는 안 된다");

                    foreach (WorldRouteEdge edge in WorldRouteLayout.FieldConnections)
                    {
                        string target = edge.FromRegionId == region.regionId ? edge.ToRegionId :
                            edge.ToRegionId == region.regionId ? edge.FromRegionId : null;
                        if (target != null) Assert.That(inside.text,
                            Does.Contain(WorldRouteLayout.Find(regions, target).displayName));
                    }
                }
                Assert.AreEqual(13, signs);
            }
            finally
            {
                foreach (GameObject generated in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                    if (generated != null && !before.Contains(generated.GetInstanceID())) Object.DestroyImmediate(generated);
            }
        }

        [Test]
        public void PondBridge_AlignsWithGatewayAndLeavesWaterPassageOpen()
        {
            var before = new System.Collections.Generic.HashSet<int>();
            foreach (GameObject existing in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                before.Add(existing.GetInstanceID());
            GameObject host = new GameObject("RouteRiverTest");
            try
            {
                RegionData[] regions = RegionDefinitions.CreateAll();
                WorldTerrainBuilder builder = host.AddComponent<WorldTerrainBuilder>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(WorldTerrainBuilder).GetMethod("BuildRiver", flags).Invoke(builder, new object[] { regions });
                typeof(WorldTerrainBuilder).GetMethod("BuildBridges", flags).Invoke(builder, new object[] { regions });
                Physics.SyncTransforms();
                Transform bridge = GameObject.Find("Bridge_PondRiver_Floor").transform;
                RegionData pond = WorldRouteLayout.Find(regions, "pond");
                Vector3 direction = (WorldRouteLayout.GetGateway(pond, regions) - pond.centerPosition).normalized;
                Assert.Greater(Vector3.Dot(bridge.forward, direction), 0.999f);
                Assert.IsNotNull(GameObject.Find("River_Water_0"));
                for (int i = -10; i <= 10; i++)
                {
                    Vector3 point = bridge.position + direction * i * 0.5f + Vector3.up * 1.4f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Collider waterBlocker = GameObject.Find("River_Blocker_" + side).GetComponent<Collider>();
                        Assert.Greater(Vector3.Distance(point, waterBlocker.ClosestPoint(point)), 0.4f);
                    }
                }
            }
            finally
            {
                foreach (GameObject generated in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                    if (!before.Contains(generated.GetInstanceID())) Object.DestroyImmediate(generated);
            }
        }

        [Test]
        public void EveryRegionGateway_HasClearApproachOutsideOtherRegions()
        {
            RegionData[] regions = RegionDefinitions.CreateAll();
            foreach (RegionData region in regions)
            {
                Vector3 gateway = WorldRouteLayout.GetGateway(region, regions);
                Assert.AreEqual(region.radius - 1f, Vector3.Distance(region.centerPosition, gateway), 0.01f);
                foreach (RegionData other in regions)
                    if (other != region)
                        Assert.GreaterOrEqual(WorldRouteLayout.DistanceToSegment(other.centerPosition,
                            region.centerPosition, gateway), other.radius + WorldRouteLayout.CorridorClearance - 0.01f,
                            region.regionId + " gateway enters locked " + other.regionId);
            }
        }

        [Test]
        public void EveryFieldRoute_ExistsUsesGatewayAndAvoidsThirdRegions()
        {
            RegionData[] regions = RegionDefinitions.CreateAll();
            foreach (WorldRouteEdge edge in WorldRouteLayout.FieldConnections)
            {
                Vector3[] route = WorldRouteLayout.BuildRoute(regions, edge.FromRegionId, edge.ToRegionId);
                string label = edge.FromRegionId + " -> " + edge.ToRegionId;
                Assert.GreaterOrEqual(route.Length, 4, label);
                RegionData from = WorldRouteLayout.Find(regions, edge.FromRegionId);
                RegionData to = WorldRouteLayout.Find(regions, edge.ToRegionId);
                Assert.AreEqual(WorldRouteLayout.GetGateway(from, regions), route[1], label);
                Assert.AreEqual(WorldRouteLayout.GetGateway(to, regions), route[route.Length - 2], label);
                for (int i = 1; i < route.Length; i++)
                    foreach (RegionData other in regions)
                    {
                        if (other == from && i <= 2 || other == to && i >= route.Length - 2) continue;
                        Assert.GreaterOrEqual(WorldRouteLayout.DistanceToSegment(other.centerPosition,
                            route[i - 1], route[i]), other.radius + WorldRouteLayout.CorridorClearance - 0.01f,
                            label + " segment " + i + " enters " + other.regionId);
                    }
                Assert.AreSame(route, WorldRouteLayout.BuildRoute(regions, edge.FromRegionId, edge.ToRegionId));
            }
        }
    }
}
#endif
