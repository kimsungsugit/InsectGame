#if UNITY_EDITOR
using InsectGame.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 도주 방향·거리(<see cref="FleePath"/>) — 곤충이 벽·건물·바위·나무 줄기와 서브에리어 방 벽을 뚫고 달아나던 결함.
    ///
    /// 곤충은 몸 콜라이더가 없어 물리가 막아 주지 않는다. 그래서 도주를 시작할 때 방향마다 막힘 거리를 재고
    /// (<c>InsectEntity.MeasureFleeClearance</c> — 씬이 필요해 여기선 가짜 측정기를 쓴다) 이 순수 판정이 방향과
    /// 갈 거리를 정한다.
    /// </summary>
    [TestFixture]
    public class FleePathTests
    {
        private static readonly Vector3 Away = Vector3.forward;   // 플레이어 반대쪽

        private static float AngleFromAway(Vector3 dir) => Vector3.SignedAngle(Away, dir, Vector3.up);

        [Test]
        public void Choose_OpenField_RunsStraightAwayFullDistance()
        {
            Vector3 dir = FleePath.Choose(Away, 1f, _ => FleePath.ProbeLength, out float allowed);
            Assert.AreEqual(0f, AngleFromAway(dir), 0.01f);
            Assert.AreEqual(FleePath.MaxDistance, allowed, 1e-5f);
        }

        [Test]
        public void Choose_WallStraightBehind_VeersToFirstOpenSideCandidate()
        {
            // 정면(반대쪽) ±20° 안은 2m 앞이 벽이다.
            System.Func<Vector3, float> probe = d => Mathf.Abs(AngleFromAway(d)) < 20f ? 2f : FleePath.ProbeLength;

            Vector3 right = FleePath.Choose(Away, 1f, probe, out float allowedR);
            Assert.AreEqual(35f, AngleFromAway(right), 0.5f, "첫 뚫린 후보(+35°)를 안 골랐다");
            Assert.AreEqual(FleePath.MaxDistance, allowedR, 1e-5f);

            Vector3 left = FleePath.Choose(Away, -1f, probe, out _);
            Assert.AreEqual(-35f, AngleFromAway(left), 0.5f, "좌우 뒤집기(sideSign)가 안 먹는다");
        }

        [Test]
        public void Choose_Cornered_TakesFarthestAndStopsBeforeTheWall()
        {
            // 사방이 막혔다 — +70° 쪽이 가장 멀다(5m).
            System.Func<Vector3, float> probe = d => Mathf.Abs(AngleFromAway(d) - 70f) < 1f ? 5f : 1.5f;
            Vector3 dir = FleePath.Choose(Away, 1f, probe, out float allowed);
            Assert.AreEqual(70f, AngleFromAway(dir), 0.5f);
            Assert.AreEqual(5f - FleePath.WallMargin, allowed, 1e-5f, "장애물 앞 여유만큼 덜 가야 벽면에 안 파묻힌다");
        }

        [Test]
        public void Choose_PressedAgainstWalls_DoesNotMoveAtAll()
        {
            // 방 구석에 붙어 모든 방향이 여유보다 가깝다 — 제자리에서 타이머가 끝나 사라진다.
            FleePath.Choose(Away, 1f, _ => FleePath.WallMargin * 0.5f, out float allowed);
            Assert.AreEqual(0f, allowed, 1e-6f);
        }

        [Test]
        public void CandidateAngles_NeverTurnBackTowardThePlayer()
        {
            for (int i = 0; i < FleePath.CandidateAngles.Length; i++)
            {
                Vector3 d = FleePath.DirectionFor(Away, i, 1f);
                Assert.AreEqual(0f, d.y, 1e-6f, "수평이 아니다");
                Assert.AreEqual(1f, d.magnitude, 1e-5f);
                Assert.Less(Mathf.Abs(AngleFromAway(d)), 120f, "플레이어 쪽으로 뛰어드는 후보가 있다");
            }
        }

        [Test]
        public void StepDistance_SumNeverExceedsAllowed()
        {
            const float allowed = 3.2f;
            float travelled = 0f;
            for (int frame = 0; frame < 120; frame++)
                travelled += FleePath.StepDistance(7.5f / 60f, allowed, travelled);
            Assert.AreEqual(allowed, travelled, 1e-4f, "벽 앞에서 멈추지 않거나 덜 갔다");
            Assert.AreEqual(0f, FleePath.StepDistance(1f, allowed, allowed), 1e-6f);
        }

        [Test]
        public void MaxDistance_CoversFastestFleeMotion()
        {
            // 비행 도주 7.5m/s × 1.1초 — 이보다 짧으면 뚫린 방향인데도 중간에 멈춘다.
            Assert.GreaterOrEqual(FleePath.MaxDistance, 7.5f * 1.1f);
        }
    }
}
#endif
