#if UNITY_EDITOR
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 지도·미니맵 마커 배치의 순수부. 화면(IMGUI)은 테스트로 못 보므로 좌표 계산만 고정한다.
    ///
    /// <c>KeepClear</c>가 지키는 것: 캠페인 첫 목표(어르신)는 시작점에서 9m 앞이라 월드 지도 축척에서는
    /// 내 위치 점과 한 자리다 — 떼어 놓지 않으면 <c>!</c> 배지가 내 위치 점 밑에 깔려 안 보인다.
    /// </summary>
    [TestFixture]
    public class MapMarkerProjectionTests
    {
        [Test]
        public void KeepClear_FarEnough_LeavesMarkerAlone()
        {
            Vector2 marker = new Vector2(100f, 40f);
            Assert.AreEqual(marker, MapMarkerProjection.KeepClear(marker, new Vector2(10f, 40f), 27f));
        }

        [Test]
        public void KeepClear_TooClose_PushesOutAlongSameDirection()
        {
            Vector2 obstacle = new Vector2(50f, 50f);
            Vector2 moved = MapMarkerProjection.KeepClear(new Vector2(53f, 46f), obstacle, 25f);

            Assert.AreEqual(25f, Vector2.Distance(moved, obstacle), 0.001f);
            // (3, -4) 방향 그대로 — 오른쪽 위로 가면 된다는 정보가 남아야 한다.
            Assert.AreEqual(50f + 15f, moved.x, 0.001f);
            Assert.AreEqual(50f - 20f, moved.y, 0.001f);
        }

        [Test]
        public void KeepClear_ExactlyOnTop_GoesUpInGuiSpace()
        {
            Vector2 obstacle = new Vector2(50f, 50f);
            Vector2 moved = MapMarkerProjection.KeepClear(obstacle, obstacle, 20f);

            Assert.AreEqual(50f, moved.x, 0.001f);
            Assert.AreEqual(30f, moved.y, 0.001f);   // GUI는 아래가 +y — 위는 y가 줄어든다
        }

        [Test]
        public void KeepClear_NonPositiveGap_IsNoOp()
        {
            Vector2 marker = new Vector2(5f, 5f);
            Assert.AreEqual(marker, MapMarkerProjection.KeepClear(marker, marker, 0f));
        }

        [Test]
        public void TryRadarOffset_InsideRadius_MapsNorthToUp()
        {
            bool ok = MapMarkerProjection.TryRadarOffset(Vector3.zero, new Vector3(0f, 0f, 22.5f), 45f, 100f, out Vector2 offset);

            Assert.IsTrue(ok);
            Assert.AreEqual(0f, offset.x, 0.001f);
            Assert.AreEqual(-50f, offset.y, 0.001f);   // 월드 +Z = 미니맵 위쪽
        }

        [Test]
        public void TryRadarOffset_OutsideRadius_IsRejected()
        {
            Assert.IsFalse(MapMarkerProjection.TryRadarOffset(Vector3.zero, new Vector3(46f, 0f, 0f), 45f, 100f, out _));
        }
    }
}
#endif
