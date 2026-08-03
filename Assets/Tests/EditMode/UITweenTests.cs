#if UNITY_EDITOR
using InsectGame.UI;
using NUnit.Framework;

namespace InsectGame.Tests
{
    [TestFixture]
    public class UITweenTests
    {
        private const float Delta = 0.0001f;

        /// <summary>
        /// 트윈이 시작·끝에서 튀지 않으려면 모든 곡선이 0→0, 1→1이어야 한다.
        /// 새 EaseType을 추가하면 여기 TestCase도 늘려 같은 계약을 강제할 것.
        /// </summary>
        [TestCase(EaseType.Linear)]
        [TestCase(EaseType.SmoothStep)]
        [TestCase(EaseType.EaseOutBack)]
        [TestCase(EaseType.EaseOutBounce)]
        [TestCase(EaseType.EaseInOutQuad)]
        public void Ease_EveryCurve_PinsBothEndpoints(EaseType ease)
        {
            Assert.AreEqual(0f, UITween.Ease(0f, ease), Delta, $"{ease}의 시작점이 0이 아니다");
            Assert.AreEqual(1f, UITween.Ease(1f, ease), Delta, $"{ease}의 끝점이 1이 아니다");
        }

        [Test]
        public void Ease_SmoothStepMidpoint_IsHalfway()
        {
            Assert.AreEqual(0.5f, UITween.Ease(0.5f, EaseType.SmoothStep), Delta);
        }

        /// <summary>
        /// EaseOutBack은 끝에서 목표를 넘겼다 돌아오는 곡선이라 1을 넘는 구간이 있어야 한다.
        /// 그래서 Evaluate가 Lerp가 아니라 LerpUnclamped를 쓴다 — 클램프하면 이 곡선이 죽는다.
        /// </summary>
        [Test]
        public void Ease_EaseOutBack_OvershootsBeforeSettling()
        {
            Assert.Greater(UITween.Ease(0.7f, EaseType.EaseOutBack), 1f);
        }

        [Test]
        public void Create_ZeroOrNegativeDuration_IsClampedAboveZero()
        {
            Assert.Greater(UITween.Create(0f, 1f, 0f).duration, 0f);
            Assert.Greater(UITween.Create(0f, 1f, -5f).duration, 0f);
        }

        [Test]
        public void Create_StartsActiveAtZeroElapsed()
        {
            TweenHandle h = UITween.Create(0f, 1f, 0.2f);

            Assert.IsTrue(h.active);
            Assert.IsFalse(UITween.IsComplete(ref h));
            Assert.AreEqual(0f, h.elapsed, Delta);
        }

        /// <summary>
        /// 완료된 트윈은 시간을 더 흘리지 않고 목표값을 돌려준다 —
        /// OnGUI가 프레임당 여러 번 불러도 값이 흔들리면 안 된다.
        /// </summary>
        [Test]
        public void Evaluate_InactiveHandle_ReturnsTargetWithoutAdvancing()
        {
            TweenHandle h = UITween.Create(0f, 1f, 0.2f);
            UITween.Stop(ref h);
            float before = h.elapsed;

            Assert.AreEqual(1f, UITween.Evaluate(ref h), Delta);
            Assert.AreEqual(1f, UITween.Evaluate(ref h), Delta);
            Assert.AreEqual(before, h.elapsed, Delta);
        }

        /// <summary>
        /// 같은 프레임에서 반복 호출해도 elapsed가 한 번만 는다.
        /// 이게 깨지면 0.2초 페이드가 OnGUI 패스 수만큼 빨라진다(2026-08-03 실제 결함).
        /// 테스트가 프레임을 넘길 수 없으므로 "여러 번 불러도 한 번만" 쪽을 고정한다.
        /// </summary>
        [Test]
        public void Evaluate_SameFrameRepeatedCalls_AdvancesTimeOnlyOnce()
        {
            TweenHandle h = UITween.Create(0f, 1f, 5f);

            UITween.Evaluate(ref h);
            float afterFirst = h.elapsed;
            for (int i = 0; i < 8; i++)
            {
                UITween.Evaluate(ref h);
            }

            Assert.AreEqual(afterFirst, h.elapsed, Delta);
        }
    }
}
#endif
