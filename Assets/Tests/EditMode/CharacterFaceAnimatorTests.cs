#if UNITY_EDITOR
using NUnit.Framework;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 눈 깜빡임 곡선과 표정 테이블.
    ///
    /// 깜빡임의 위험은 <b>드리프트</b>다 — 매 프레임 스케일을 곱하면 눈이 조금씩 작아지다
    /// 영영 사라진다. 실제로 <c>PlayerMovement</c>의 도구 회전이 같은 종류의 문제를 겪고
    /// base 캐싱으로 막았다. 곡선이 양 끝에서 정확히 1로 돌아오는 것이 그 방어의 절반이고
    /// (나머지 절반은 구현이 base에 <b>대입</b>하는 것), 여기서 그 절반을 고정한다.
    /// </summary>
    [TestFixture]
    public class CharacterFaceAnimatorTests
    {
        // ── 깜빡임 곡선 ──

        /// <summary>
        /// 시작과 끝에서 눈이 완전히 떠 있어야 한다. 여기가 1이 아니면 깜빡일 때마다 눈이
        /// 조금씩 남거나 커져 누적된다.
        /// </summary>
        [Test]
        public void BlinkScale_AtBothEnds_IsFullyOpen()
        {
            Assert.AreEqual(1f, CharacterFaceAnimator.BlinkScale(0f), 1e-4f, "시작");
            Assert.AreEqual(1f, CharacterFaceAnimator.BlinkScale(1f), 1e-4f, "끝");
        }

        [Test]
        public void BlinkScale_AtMidpoint_IsFullyClosed()
        {
            Assert.AreEqual(0f, CharacterFaceAnimator.BlinkScale(0.5f), 1e-4f,
                "중간에 눈이 감기지 않으면 깜빡임이 보이지 않는다");
        }

        [Test]
        public void BlinkScale_StaysInUnitRange()
        {
            for (int i = 0; i <= 20; i++)
            {
                float phase = i / 20f;
                float v = CharacterFaceAnimator.BlinkScale(phase);
                Assert.That(v, Is.InRange(0f, 1f), $"phase {phase}: 눈 스케일이 0~1 밖이면 뒤집히거나 튄다");
            }
        }

        /// <summary>범위 밖 phase가 들어와도(프레임 스킵 등) 눈이 이상한 크기로 남지 않는다.</summary>
        [Test]
        public void BlinkScale_OutOfRangePhase_ClampsToOpen()
        {
            Assert.AreEqual(1f, CharacterFaceAnimator.BlinkScale(-1f), 1e-4f);
            Assert.AreEqual(1f, CharacterFaceAnimator.BlinkScale(5f), 1e-4f);
        }

        /// <summary>감기는 중간 지점은 완전히 감긴 것보다 크고 뜬 것보다 작아야 한다(단조 변화).</summary>
        [Test]
        public void BlinkScale_QuarterPhase_IsPartiallyClosed()
        {
            float quarter = CharacterFaceAnimator.BlinkScale(0.25f);

            Assert.Greater(quarter, 0f);
            Assert.Less(quarter, 1f);
        }

        // ── 깜빡임 간격 ──

        [Test]
        public void NextBlinkDelay_SpansTheAuthoredRange()
        {
            Assert.AreEqual(2.5f, CharacterFaceAnimator.NextBlinkDelay(0f), 1e-4f);
            Assert.AreEqual(6.0f, CharacterFaceAnimator.NextBlinkDelay(1f), 1e-4f);
        }

        [Test]
        public void NextBlinkDelay_AlwaysPositive_EvenForBadInput()
        {
            for (float r = -2f; r <= 3f; r += 0.5f)
            {
                Assert.Greater(CharacterFaceAnimator.NextBlinkDelay(r), 0f,
                    "간격이 0 이하면 매 프레임 깜빡여 눈이 떨린다");
            }
        }

        // ── 표정 테이블 ──

        /// <summary>
        /// enum에 값을 더하고 테이블을 안 고치면 그 표정이 조용히 Idle로 떨어진다 —
        /// 예외도 경고도 없이 "표정이 안 바뀐다"로만 나타난다.
        /// </summary>
        [Test]
        public void ExpressionValues_EveryEnumValue_ProducesFiniteNumbers()
        {
            foreach (FaceExpression e in System.Enum.GetValues(typeof(FaceExpression)))
            {
                CharacterFaceAnimator.ExpressionValues(e,
                    out float tilt, out float raise, out float widthScale, out float mouthRaise);

                Assert.IsFalse(float.IsNaN(tilt) || float.IsNaN(raise)
                            || float.IsNaN(widthScale) || float.IsNaN(mouthRaise), $"{e}: NaN");
                Assert.Greater(widthScale, 0f, $"{e}: 입 폭 배율이 0 이하면 입이 사라진다");
            }
        }

        /// <summary>Idle은 "생성 화면에서 고른 얼굴 그대로"다 — 아무것도 바꾸지 않아야 한다.</summary>
        [Test]
        public void ExpressionValues_Idle_IsIdentity()
        {
            CharacterFaceAnimator.ExpressionValues(FaceExpression.Idle,
                out float tilt, out float raise, out float widthScale, out float mouthRaise);

            Assert.AreEqual(0f, tilt, 1e-5f);
            Assert.AreEqual(0f, raise, 1e-5f);
            Assert.AreEqual(1f, widthScale, 1e-5f);
            Assert.AreEqual(0f, mouthRaise, 1e-5f);
        }

        /// <summary>표정이 서로 구별돼야 한다 — 값이 같으면 바꿔도 아무 변화가 없다.</summary>
        [Test]
        public void ExpressionValues_EachExpression_DiffersFromIdle()
        {
            foreach (FaceExpression e in System.Enum.GetValues(typeof(FaceExpression)))
            {
                if (e == FaceExpression.Idle) continue;

                CharacterFaceAnimator.ExpressionValues(e,
                    out float tilt, out float raise, out float widthScale, out float mouthRaise);

                bool differs = Mathf.Abs(tilt) > 1e-4f || Mathf.Abs(raise) > 1e-4f
                            || Mathf.Abs(widthScale - 1f) > 1e-4f || Mathf.Abs(mouthRaise) > 1e-4f;
                Assert.IsTrue(differs, $"{e}가 Idle과 값이 같다 — 표정을 바꿔도 화면이 그대로다");
            }
        }

        /// <summary>웃음과 슬픔은 눈썹이 반대로 기울어야 한다(안 그러면 둘이 비슷해 보인다).</summary>
        [Test]
        public void ExpressionValues_SmileAndSad_TiltBrowsOppositeWays()
        {
            CharacterFaceAnimator.ExpressionValues(FaceExpression.Smile, out float smileTilt, out _, out _, out _);
            CharacterFaceAnimator.ExpressionValues(FaceExpression.Sad, out float sadTilt, out _, out _, out _);

            Assert.Less(smileTilt * sadTilt, 0f, "부호가 같으면 두 표정이 같은 방향으로 기운다");
        }

        /// <summary>놀람은 눈썹이 가장 높이 올라가야 한다.</summary>
        [Test]
        public void ExpressionValues_Surprise_RaisesBrowsMost()
        {
            CharacterFaceAnimator.ExpressionValues(FaceExpression.Surprise, out _, out float surprise, out _, out _);
            CharacterFaceAnimator.ExpressionValues(FaceExpression.Smile, out _, out float smile, out _, out _);
            CharacterFaceAnimator.ExpressionValues(FaceExpression.Sad, out _, out float sad, out _, out _);

            Assert.Greater(surprise, smile);
            Assert.Greater(surprise, sad);
        }
    }
}
#endif
