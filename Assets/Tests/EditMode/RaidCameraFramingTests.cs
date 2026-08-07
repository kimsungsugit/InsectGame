#if UNITY_EDITOR
using InsectGame.Battle;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 레이드 카메라가 <b>팀 뒤에서 보스를 정면으로</b> 보는지 고정한다.
    ///
    /// 회귀 이력: 아레나가 정면 구도를 잡아도 <c>CameraFollower.EnterBattleMode</c>가 대결 축의
    /// 측면(<c>Cross(dir, up)</c>)에 카메라를 놓고 그 값을 매 프레임 적용해, 레이드가 옆에서 보였다.
    /// 여기서 검증하는 건 그 "측면으로 밀린 성분이 0인가"다.
    /// </summary>
    [TestFixture]
    public class RaidCameraFramingTests
    {
        // 실제 아레나 배치(BattleArenaController.SetupRaidBattle)와 같은 상대 좌표.
        private static readonly Vector3 TeamPos = new Vector3(0f, 0.5f, -3.5f);
        private static readonly Vector3 BossPos = new Vector3(0f, 1.2f, 4f);

        [Test]
        public void ComputeRaidCameraFraming_PlacesCameraBehindTeam()
        {
            BattleArenaController.ComputeRaidCameraFraming(
                TeamPos, BossPos, out Vector3 camPos, out _);

            Vector3 axis = HorizontalAxis(TeamPos, BossPos);
            Assert.Less(
                Vector3.Dot(camPos - TeamPos, axis), 0f,
                "카메라는 팀→보스 축의 뒤쪽에 있어야 한다(앞이면 팀이 보스를 가린다)");
        }

        [Test]
        public void ComputeRaidCameraFraming_HasNoSidewaysOffset()
        {
            BattleArenaController.ComputeRaidCameraFraming(
                TeamPos, BossPos, out Vector3 camPos, out Vector3 lookTarget);

            Vector3 axis = HorizontalAxis(TeamPos, BossPos);
            Vector3 side = Vector3.Cross(axis, Vector3.up).normalized;

            Assert.AreEqual(0f, Vector3.Dot(camPos - TeamPos, side), 0.001f,
                "카메라가 측면으로 밀리면 예전의 옆에서 보는 각도로 돌아간다");
            Assert.AreEqual(0f, Vector3.Dot(lookTarget - TeamPos, side), 0.001f,
                "시선 지점도 축 위에 있어야 보스가 화면 중앙에 온다");
        }

        [Test]
        public void ComputeRaidCameraFraming_LooksTowardBoss()
        {
            BattleArenaController.ComputeRaidCameraFraming(
                TeamPos, BossPos, out Vector3 camPos, out Vector3 lookTarget);

            Assert.Greater(
                Vector3.Dot(lookTarget - camPos, BossPos - camPos), 0f,
                "보스는 시선 앞쪽(정면)에 있어야 한다");
            Assert.Greater(camPos.y, TeamPos.y, "팀보다 높아야 팀 너머로 보스가 보인다");
            Assert.Greater(camPos.y, lookTarget.y, "내려다보는 각도여야 한다");
        }

        [Test]
        public void ComputeRaidCameraFraming_DerivesFromActualPositions()
        {
            // 축이 뒤집혀도(보스가 -Z쪽) 카메라는 여전히 팀 뒤에 붙는다 — 상수 박기였다면 깨진다.
            Vector3 flippedBoss = new Vector3(0f, 1.2f, -11f);
            BattleArenaController.ComputeRaidCameraFraming(
                TeamPos, flippedBoss, out Vector3 camPos, out _);

            Assert.Greater(camPos.z, TeamPos.z, "보스가 -Z에 있으면 카메라는 +Z쪽(팀 뒤)이어야 한다");
        }

        [Test]
        public void ComputeRaidCameraFraming_SamePosition_DoesNotProduceNaN()
        {
            // 축 길이 0 — 폴백이 없으면 normalized가 0벡터가 되어 카메라가 팀 안에 박힌다.
            BattleArenaController.ComputeRaidCameraFraming(
                TeamPos, TeamPos, out Vector3 camPos, out Vector3 lookTarget);

            Assert.IsFalse(float.IsNaN(camPos.x) || float.IsNaN(camPos.y) || float.IsNaN(camPos.z));
            Assert.Greater((camPos - lookTarget).magnitude, 0.5f, "카메라와 시선 지점이 겹치면 안 된다");
        }

        private static Vector3 HorizontalAxis(Vector3 from, Vector3 to)
        {
            Vector3 axis = to - from;
            axis.y = 0f;
            return axis.normalized;
        }
    }
}
#endif
