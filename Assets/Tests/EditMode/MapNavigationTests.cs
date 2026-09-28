#if UNITY_EDITOR
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    public class MapNavigationTests
    {
        [TestCase(.6666667f, 0f)]
        [TestCase(.6666667f, 90f)]
        [TestCase(.8333333f, -45f)]
        [TestCase(1f, 180f)]
        public void MapRotation_KeepsVirtualPivotAndLineEndpointUnderScreenScale(float scale, float angle)
        {
            Matrix4x4 parent = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Vector2 pivot = new Vector2(450f, 320f);
            Matrix4x4 matrix = MapMarkerProjection.PivotMatrix(parent, pivot, angle);
            Vector3 actualPivot = matrix.MultiplyPoint3x4(Vector3.zero);
            Assert.That(Vector3.Distance(actualPivot, new Vector3(pivot.x * scale, pivot.y * scale, 0f)), Is.LessThan(.001f));
            Vector3 expectedEnd = parent.MultiplyPoint3x4(new Vector3(pivot.x, pivot.y, 0f)
                + Quaternion.Euler(0f, 0f, angle) * (Vector3.right * 80f));
            Assert.That(Vector3.Distance(matrix.MultiplyPoint3x4(Vector3.right * 80f), expectedEnd), Is.LessThan(.001f));
        }

        [Test]
        public void OverworldMarker_UsesEntranceWhileInsideSeparateSubWorld()
        {
            var sub = new SubAreaData { centerPosition = new Vector3(20f, 0f, 40f) };
            Assert.AreEqual(sub.centerPosition,
                MapMarkerProjection.OverworldPlayerPosition(new Vector3(2000f, 0f, 2000f), sub));
            Vector3 field = new Vector3(15f, 0f, 12f);
            Assert.AreEqual(field, MapMarkerProjection.OverworldPlayerPosition(field, null));
        }

        [Test]
        public void RadarProjection_UsesSameNorthAndScaleRegardlessOfHeight()
        {
            Assert.IsTrue(MapMarkerProjection.TryRadarOffset(Vector3.zero, new Vector3(10f, 400f, 20f),
                40f, 100f, out Vector2 offset));
            Assert.AreEqual(new Vector2(25f, -50f), offset);
            Assert.IsFalse(MapMarkerProjection.TryRadarOffset(Vector3.zero, Vector3.right * 41f,
                40f, 100f, out _));
            Assert.IsFalse(MapMarkerProjection.TryRadarOffset(Vector3.zero, Vector3.zero,
                0f, 100f, out _));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MapTravel_ExitsSubWorldBeforeAssigningDestination(bool travelToEntrance)
        {
            var root = new GameObject("Map navigation fixture");
            var player = new GameObject("Map navigation player");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                var manager = root.AddComponent<RegionManager>();
                var map = root.AddComponent<RegionMapUI>();
                var movement = player.AddComponent<PlayerMovement>();
                typeof(PlayerMovement).GetField("movingToClick", flags).SetValue(movement, true);
                typeof(PlayerMovement).GetField("clickTarget", flags).SetValue(movement, new Vector3(999f, 0f, 999f));
                typeof(PlayerMovement).GetField("joystickActive", flags).SetValue(movement, true);
                typeof(PlayerMovement).GetField("joystickInput", flags).SetValue(movement, Vector2.one);
                typeof(RegionManager).GetField("currentSubArea", flags).SetValue(manager,
                    new SubAreaData { subAreaId = "old" });
                manager.SetSubAreaSticky(true);
                typeof(RegionMapUI).GetField("regionManager", flags).SetValue(map, manager);
                typeof(RegionMapUI).GetField("playerTransform", flags).SetValue(map, player.transform);
                bool restored = false;
                manager.SubAreaChanged += value =>
                {
                    Assert.IsNull(value);
                    restored = true;
                    player.transform.position = new Vector3(7f, 2f, 7f);
                };
                Vector3 destination = new Vector3(80f, 0f, 40f);
                object target = travelToEntrance
                    ? (object)new SubAreaData { centerPosition = destination }
                    : new RegionData { centerPosition = destination };
                typeof(RegionMapUI).GetMethod(travelToEntrance ? "TeleportToSubArea" : "TeleportToRegion", flags)
                    .Invoke(map, new[] { target });
                Assert.IsFalse((bool)typeof(PlayerMovement).GetField("movingToClick", flags).GetValue(movement));
                Assert.IsFalse((bool)typeof(PlayerMovement).GetField("joystickActive", flags).GetValue(movement));
                Assert.AreEqual(Vector2.zero, typeof(PlayerMovement).GetField("joystickInput", flags).GetValue(movement));
                Assert.AreEqual(player.transform.position, typeof(PlayerMovement).GetField("clickTarget", flags).GetValue(movement));
                Assert.IsTrue(restored);
                Assert.IsFalse(manager.SubAreaSticky);
                Assert.IsNull(manager.CurrentSubArea);
                Assert.AreEqual(new Vector3(80f, 2f, 40f), player.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(root);
            }
        }
    }
}
#endif
