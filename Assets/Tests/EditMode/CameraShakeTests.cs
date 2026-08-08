#if UNITY_EDITOR
using InsectGame.Core;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 카메라 쉐이크 누적 규칙. 예전엔 역대 최댓값을 붙들고 매 호출마다 그 값을 통째로
    /// 되감아서, 강한 흔들림 뒤의 약한 호출이 <b>강한 것을 처음부터 다시 재생</b>했다.
    /// </summary>
    [TestFixture]
    public class CameraShakeTests
    {
        private const float Delta = 0.001f;

        [Test]
        public void FirstShake_TakesGivenValues()
        {
            CameraFollower.ResolveShake(0f, 0f, 0f, 0.4f, 0.5f,
                out float intensity, out float duration, out float timer);

            Assert.AreEqual(0.4f, intensity, Delta);
            Assert.AreEqual(0.5f, duration, Delta);
            Assert.AreEqual(0.5f, timer, Delta);
        }

        [Test]
        public void WeakShakeAfterStrongOne_DoesNotReplayTheStrongOne()
        {
            // 회귀의 핵심: 0.55 흔들림이 절반쯤 잦아든 시점에 0.1을 부르면
            // 예전엔 0.55/0.55가 처음부터 다시 재생됐다.
            CameraFollower.ResolveShake(
                curIntensity: 0.55f, curDuration: 0.55f, curTimer: 0.25f,
                newIntensity: 0.1f, newDuration: 0.3f,
                out float intensity, out _, out float timer);

            Assert.AreEqual(0.55f, intensity, Delta, "세기는 유지된다(여진이 본 흔들림을 끊지 않게)");
            Assert.AreEqual(0.3f, timer, Delta, "지속만 늘어난다 — 되감기가 아니다");
        }

        [Test]
        public void StrongerShake_TakesOver()
        {
            CameraFollower.ResolveShake(
                curIntensity: 0.2f, curDuration: 0.4f, curTimer: 0.4f,
                newIntensity: 0.6f, newDuration: 0.3f,
                out float intensity, out float duration, out float timer);

            Assert.AreEqual(0.6f, intensity, Delta);
            Assert.AreEqual(0.3f, duration, Delta);
            Assert.AreEqual(0.3f, timer, Delta);
        }

        [Test]
        public void ShakeThatDecayedBelowNewCall_LetsTheNewCallWin()
        {
            // 0.5짜리가 10%만 남았으면 실효 세기는 0.05 — 0.2 호출이 이겨야 한다.
            CameraFollower.ResolveShake(
                curIntensity: 0.5f, curDuration: 0.5f, curTimer: 0.05f,
                newIntensity: 0.2f, newDuration: 0.4f,
                out float intensity, out _, out float timer);

            Assert.AreEqual(0.2f, intensity, Delta);
            Assert.AreEqual(0.4f, timer, Delta);
        }

        [Test]
        public void ExpiredShake_IsTreatedAsZero()
        {
            CameraFollower.ResolveShake(
                curIntensity: 0.9f, curDuration: 0.9f, curTimer: 0f,
                newIntensity: 0.1f, newDuration: 0.2f,
                out float intensity, out _, out float timer);

            Assert.AreEqual(0.1f, intensity, Delta, "타이머가 끝났으면 역대 값이 새 호출을 이기면 안 된다");
            Assert.AreEqual(0.2f, timer, Delta);
        }

        [Test]
        public void RepeatedWeakShakes_DoNotEscalate()
        {
            // 레이드처럼 약한 타격이 연달아 와도 세기가 계단식으로 올라가면 안 된다.
            float intensity = 0f, duration = 0f, timer = 0f;
            for (int i = 0; i < 12; i++)
            {
                CameraFollower.ResolveShake(intensity, duration, timer, 0.15f, 0.25f,
                    out intensity, out duration, out timer);
                timer -= 0.05f;   // 프레임 경과를 흉내
            }

            Assert.LessOrEqual(intensity, 0.15f + Delta, "약한 흔들림 반복이 세기를 키웠다");
        }

        [Test]
        public void TimerNeverExceedsDuration()
        {
            // 남은 비율 계산(timer / duration)이 1을 넘으면 실효 세기가 원래보다 커진다.
            float intensity = 0f, duration = 0f, timer = 0f;
            float[,] calls = { { 0.4f, 0.5f }, { 0.1f, 0.9f }, { 0.2f, 0.3f }, { 0.05f, 1.2f } };

            for (int i = 0; i < calls.GetLength(0); i++)
            {
                CameraFollower.ResolveShake(intensity, duration, timer,
                    calls[i, 0], calls[i, 1], out intensity, out duration, out timer);
                Assert.LessOrEqual(timer, duration + Delta, $"{i}번째 호출에서 timer > duration");
            }
        }
    }
}
#endif
