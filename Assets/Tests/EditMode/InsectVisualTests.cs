#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Dex;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 곤충 그림 경로의 순수 계산부 — 썸네일 캐시 정책과 도형 각도.
    /// 실제 렌더(RenderTexture·GUI.DrawTexture)는 `rules/testing.md`상 테스트 제외다.
    /// </summary>
    [TestFixture]
    public class InsectVisualTests
    {
        [Test]
        public void BattleModel_UpdateMovement_PreservesArenaOwnedPose()
        {
            GameObject model = new GameObject("ArenaOwnedPose");
            var data = ScriptableObject.CreateInstance<InsectGame.Data.InsectData>();
            data.insectId = "rhinoceros_beetle";
            try
            {
                var entity = model.AddComponent<InsectGame.Spawning.InsectEntity>();
                entity.BuildForBattle(data, 1, false);
                Vector3 position = new Vector3(7f, 2f, -3f);
                Quaternion rotation = Quaternion.Euler(0f, 61f, 0f);
                model.transform.SetPositionAndRotation(position, rotation);
                typeof(InsectGame.Spawning.InsectEntity).GetMethod("UpdateMovement",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(entity, null);
                Assert.AreEqual(position, model.transform.position);
                Assert.AreEqual(rotation, model.transform.rotation);
            }
            finally { Object.DestroyImmediate(model); Object.DestroyImmediate(data); }
        }

        [TestCase("monarch_butterfly", "SpotL1")]
        [TestCase("dragonfly", "VeinFL")]
        public void WingDecoration_FollowsHinge_WithoutChangingLocalAttachment(string id, string decoration)
        {
            GameObject model = new GameObject("WingContract");
            var data = ScriptableObject.CreateInstance<InsectGame.Data.InsectData>();
            data.insectId = id;
            try
            {
                model.AddComponent<InsectGame.Spawning.InsectEntity>().BuildForBattle(data, 1, false);
                Transform hinge = model.transform.Find("WingL");
                Transform detail = hinge.Find(decoration);
                Assert.IsNotNull(detail, "Wing decoration must be attached to animated hinge");
                float radius = Vector3.Distance(hinge.position, detail.position);
                Vector3 before = detail.position;
                hinge.localRotation = Quaternion.Euler(0f, 0f, 40f);
                Assert.AreEqual(radius, Vector3.Distance(hinge.position, detail.position), 0.0001f);
                Assert.Greater(Vector3.Distance(before, detail.position), 0.05f);
                Assert.IsNotNull(hinge.Find("WingLB"), "Hindwing must follow forewing hinge");
            }
            finally
            {
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(data);
            }
        }

        [TestCase("butterfly_monarch")]
        [TestCase("moth_night")]
        public void Lepidoptera_HasAttachedAntennaeHindwingsAndSixLegs(string id)
        {
            GameObject model = new GameObject("LepidopteraSilhouette");
            var species = ScriptableObject.CreateInstance<InsectGame.Data.InsectData>();
            species.insectId = id;
            try
            {
                model.AddComponent<InsectGame.Spawning.InsectEntity>().BuildForBattle(species, 1, false);
                Transform head = model.transform.Find("Head");
                Transform baseAntenna = model.transform.Find("AntBaseL");
                Transform midAntenna = model.transform.Find("AntMidL");
                Assert.IsNotNull(head);
                Assert.IsNotNull(baseAntenna);
                Assert.IsNotNull(midAntenna);
                Assert.IsTrue(head.GetComponent<Renderer>().bounds.Contains(SegmentEnd(baseAntenna, false)),
                    "Antenna must grow from the head instead of floating above it");
                Assert.Less(Vector3.Distance(SegmentEnd(baseAntenna, true), SegmentEnd(midAntenna, false)), .003f,
                    "Antenna joints must meet in the side portrait");
                Assert.IsNotNull(model.transform.Find("WingL/WingLB").GetComponent<MeshFilter>());
                for (int i = 0; i < 3; i++)
                    foreach (string side in new[] { "L", "R" })
                        Assert.IsNotNull(model.transform.Find("LegU" + side + i), "Missing flight leg " + side + i);
            }
            finally { Object.DestroyImmediate(model); Object.DestroyImmediate(species); }
        }

        [Test]
        public void Dragonfly_HindwingsAreMembranesAttachedToThorax()
        {
            GameObject model = new GameObject("DragonflySilhouette");
            var species = ScriptableObject.CreateInstance<InsectGame.Data.InsectData>();
            species.insectId = "dragonfly_lake";
            try
            {
                model.AddComponent<InsectGame.Spawning.InsectEntity>().BuildForBattle(species, 1, false);
                Bounds thorax = model.transform.Find("Thorax").GetComponent<Renderer>().bounds;
                foreach (string side in new[] { "L", "R" })
                {
                    Transform hinge = model.transform.Find("Wing" + side);
                    Transform front = hinge.Find("WingSurface" + side);
                    Transform hind = hinge.Find("Wing" + side + "B");
                    // The original node names remain available under the hinge.
                    Assert.IsNotNull(front);
                    Assert.IsNotNull(hind);
                    Assert.IsNotNull(hind.GetComponent<MeshFilter>(), "Hindwing was a sphere, not a membrane");
                    Assert.IsTrue(front.GetComponent<Renderer>().bounds.Intersects(thorax));
                    Assert.IsTrue(hind.GetComponent<Renderer>().bounds.Intersects(thorax));
                }
            }
            finally { Object.DestroyImmediate(model); Object.DestroyImmediate(species); }
        }

        [Test]
        public void Ant_MandiblesAreConnectedHooksGrowingFromHead()
        {
            GameObject model = new GameObject("AntSilhouette");
            var species = ScriptableObject.CreateInstance<InsectGame.Data.InsectData>();
            species.insectId = "ant_soldier";
            try
            {
                model.AddComponent<InsectGame.Spawning.InsectEntity>().BuildForBattle(species, 1, false);
                Bounds head = model.transform.Find("Head").GetComponent<Renderer>().bounds;
                foreach (string side in new[] { "L", "R" })
                {
                    Transform baseJaw = model.transform.Find("Mandible" + side);
                    Transform tipJaw = model.transform.Find("MandibleTip" + side);
                    Assert.IsNotNull(baseJaw);
                    Assert.IsNotNull(tipJaw);
                    Assert.IsTrue(head.Contains(SegmentEnd(baseJaw, false)));
                    Assert.Less(Vector3.Distance(SegmentEnd(baseJaw, true), SegmentEnd(tipJaw, false)), .003f);
                    Assert.Greater(Mathf.Abs(SegmentEnd(baseJaw, true).x - SegmentEnd(tipJaw, true).x), .06f,
                        "The mandible should curve inward rather than project as a box");
                }
            }
            finally { Object.DestroyImmediate(model); Object.DestroyImmediate(species); }
        }

        [Test]
        public void Stag_FrontClawsStartAtFeetRatherThanFloatUnderFace()
        {
            GameObject model = new GameObject("StagSilhouette");
            var species = ScriptableObject.CreateInstance<InsectGame.Data.InsectData>();
            species.insectId = "stag_beetle";
            try
            {
                model.AddComponent<InsectGame.Spawning.InsectEntity>().BuildForBattle(species, 1, false);
                foreach (string side in new[] { "L", "R" })
                {
                    Transform foot = model.transform.Find("Foot" + side + "2");
                    Transform claw = model.transform.Find("Claw" + side);
                    Assert.IsNotNull(foot);
                    Assert.IsNotNull(claw);
                    Assert.Less(Vector3.Distance(foot.position, SegmentEnd(claw, false)), .005f);
                }
            }
            finally { Object.DestroyImmediate(model); Object.DestroyImmediate(species); }
        }

        private static Vector3 SegmentEnd(Transform segment, bool upper) =>
            segment.position + segment.up * segment.localScale.y * (upper ? 1f : -1f);

        // ── 썸네일 캐시 키 ──

        [Test]
        public void ThumbKey_ShinyAndNormal_AreDistinct()
        {
            Assert.AreNotEqual(
                InsectModelPreviewRenderer.ThumbKey("rhinoceros_beetle", true),
                InsectModelPreviewRenderer.ThumbKey("rhinoceros_beetle", false),
                "이로치와 일반이 같은 키면 한쪽이 다른 쪽 그림을 쓴다");
        }

        // ── LRU ──

        [Test]
        public void TouchKey_ExistingKey_MovesToNewestWithoutDuplicating()
        {
            List<string> order = new List<string> { "a", "b", "c" };

            InsectModelPreviewRenderer.TouchKey(order, "a");

            Assert.AreEqual(3, order.Count, "이미 있는 키를 만지면 개수가 늘면 안 된다");
            Assert.AreEqual("a", order[order.Count - 1]);
            Assert.AreEqual("b", order[0], "가장 오래된 것이 앞으로 온다");
        }

        [Test]
        public void EvictKeys_UnderCap_EvictsNothing()
        {
            List<string> order = new List<string> { "a", "b" };

            Assert.AreEqual(0, InsectModelPreviewRenderer.EvictKeys(order, 4).Count);
            Assert.AreEqual(2, order.Count);
        }

        /// <summary>
        /// 상한 초과분은 **가장 오래된 것부터** 빠져야 한다. 반대로 하면 방금 그린 썸네일을
        /// 버리고 다음 프레임에 다시 렌더해 캐시가 무의미해진다.
        /// </summary>
        [Test]
        public void EvictKeys_OverCap_DropsOldestFirst()
        {
            List<string> order = new List<string> { "a", "b", "c", "d", "e" };

            List<string> evicted = InsectModelPreviewRenderer.EvictKeys(order, 3);

            CollectionAssert.AreEqual(new[] { "a", "b" }, evicted);
            CollectionAssert.AreEqual(new[] { "c", "d", "e" }, order);
        }

        [Test]
        public void EvictKeys_TouchedKeySurvivesEviction()
        {
            List<string> order = new List<string> { "a", "b", "c" };
            InsectModelPreviewRenderer.TouchKey(order, "a");   // a를 최신으로

            List<string> evicted = InsectModelPreviewRenderer.EvictKeys(order, 2);

            CollectionAssert.Contains(evicted, "b");
            CollectionAssert.Contains(order, "a");
        }

        [Test]
        public void EvictKeys_NullOrNegativeCap_DoesNotThrow()
        {
            Assert.AreEqual(0, InsectModelPreviewRenderer.EvictKeys(null, 3).Count);
            Assert.AreEqual(0, InsectModelPreviewRenderer.EvictKeys(new List<string> { "a" }, -1).Count);
        }

        // ── 도형 ──

        /// <summary>
        /// IMGUI는 y가 아래로 증가한다. 캡슐이 엉뚱한 방향으로 뻗으면 다리가 몸 위로 솟는다.
        /// </summary>
        [Test]
        public void AngleDegrees_ScreenSpaceDirections_AreCorrect()
        {
            Assert.AreEqual(0f, UIShapes.AngleDegrees(new Vector2(10f, 0f)), 0.01f, "오른쪽");
            Assert.AreEqual(90f, UIShapes.AngleDegrees(new Vector2(0f, 10f)), 0.01f, "화면 아래");
            Assert.AreEqual(-90f, UIShapes.AngleDegrees(new Vector2(0f, -10f)), 0.01f, "화면 위");
            Assert.AreEqual(45f, UIShapes.AngleDegrees(new Vector2(10f, 10f)), 0.01f, "우하향");
        }

        [Test]
        public void AngleDegrees_LengthDoesNotChangeAngle()
        {
            Assert.AreEqual(
                UIShapes.AngleDegrees(new Vector2(3f, 4f)),
                UIShapes.AngleDegrees(new Vector2(30f, 40f)),
                0.01f);
        }
    }
}
#endif
