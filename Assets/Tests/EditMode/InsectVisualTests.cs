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
