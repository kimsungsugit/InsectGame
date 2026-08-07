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
        private static readonly Vector3 TeamPos = new Vector3(0f, 0.5f, -2f);
        private static readonly Vector3 BossPos = new Vector3(0f, 2.2f, 3f);

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

        [Test]
        public void ComputeRaidCameraFraming_StaysInsideArenaWalls()
        {
            BattleArenaController.ComputeRaidCameraFraming(
                TeamPos, BossPos, out Vector3 camPos, out _);

            // 카메라가 경계벽 밖으로 나가면 벽의 **바깥 면**을 정면으로 마주 본다 —
            // 화면이 통째로 벽에 막혀 곤충이 하나도 보이지 않는다(실제로 그랬다).
            Assert.Less(Mathf.Abs(camPos.z), BattleArenaController.ArenaWallSpan, "z");
            Assert.Less(Mathf.Abs(camPos.x), BattleArenaController.ArenaWallSpan, "x");
            Assert.Less(camPos.y, BattleArenaController.ArenaWallHeight,
                "벽보다 높으면 벽 너머 필드가 비친다");
        }

        [Test]
        public void ComputeRaidCameraFraming_KeepsTeamAndBossVerticallyClose()
        {
            BattleArenaController.ComputeRaidCameraFraming(
                TeamPos, BossPos, out Vector3 camPos, out _);

            float teamAngle = VerticalAngle(camPos, TeamPos);
            float bossAngle = VerticalAngle(camPos, BossPos);

            // 세로 화면의 하단 1/3은 스킬 패널이 덮는다. 팀과 보스의 화면상 높이 차가 벌어지면
            // 하나를 화면에 맞추는 순간 다른 하나가 패널 뒤로 들어간다 — 팀 5마리가 그렇게 사라졌다
            // (옛 구도는 25도 차이였다). 수직 FOV 60도 기준 12도 = 화면 높이의 1/5.
            Assert.Less(Mathf.Abs(bossAngle - teamAngle), 12f,
                "팀과 보스가 화면에서 너무 멀리 떨어지면 하단 UI가 한쪽을 가린다");
            Assert.Greater(bossAngle, teamAngle, "보스가 팀보다 화면 위에 있어야 한다");
        }

        /// <summary>수평면 대비 올려다본 각도(도). 양수면 카메라보다 위.</summary>
        private static float VerticalAngle(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            return Mathf.Atan2(delta.y, horizontal) * Mathf.Rad2Deg;
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
