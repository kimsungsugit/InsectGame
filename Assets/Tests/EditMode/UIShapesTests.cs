#if UNITY_EDITOR
using InsectGame.UI;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// <see cref="UIShapes.Part"/>의 중간 roundness 혼합 수식.
    ///
    /// 그리기 자체는 자동화 대상이 아니지만(<c>rules/testing.md</c>), 이 수식은 한동안
    /// <b>방향이 뒤집혀</b> 있었다 — r이 클수록 각지고 작을수록 둥글었다. 2D 의상 카드 폴백이
    /// <c>RecipeRoundness</c>로 0.12(Cube)와 0.8(세운 원통)을 실제로 넘기고 있어서,
    /// 큐브가 타원으로 원통이 사각형으로 그려졌다. 방향을 여기서 고정한다.
    /// </summary>
    [TestFixture]
    public class UIShapesTests
    {
        [Test]
        public void BlendInsetRatio_ZeroRoundness_CoversDiscCompletely()
        {
            // 인셋 0 → 덮는 사각형이 rect 전체 → disc가 안 보이고 각진다.
            Assert.AreEqual(0f, UIShapes.BlendInsetRatio(0f), 0.0001f);
        }

        [Test]
        public void BlendInsetRatio_FullRoundness_LeavesDiscUncovered()
        {
            // 인셋 0.5 → 변마다 절반씩 물려 덮을 사각형이 남지 않는다 → disc 그대로 타원.
            Assert.AreEqual(0.5f, UIShapes.BlendInsetRatio(1f), 0.0001f);
        }

        [Test]
        public void BlendInsetRatio_IncreasesWithRoundness()
        {
            // ★ 회귀 고정 — 예전 `(1f - r)` 수식은 여기서 **감소**했다.
            float low = UIShapes.BlendInsetRatio(0.12f);    // Cube가 넘기는 값
            float high = UIShapes.BlendInsetRatio(0.8f);    // 세운 원통이 넘기는 값

            Assert.Less(low, high, "둥글수록 더 많이 깎아야 한다");
            Assert.Less(low, 0.25f, "거의 각져야 할 값이 절반 넘게 깎이면 안 된다");
            Assert.Greater(high, 0.25f, "거의 둥글어야 할 값이 조금만 깎이면 안 된다");
        }

        [Test]
        public void BlendInsetRatio_OutOfRange_Clamps()
        {
            Assert.AreEqual(0f, UIShapes.BlendInsetRatio(-3f), 0.0001f);
            Assert.AreEqual(0.5f, UIShapes.BlendInsetRatio(7f), 0.0001f);
        }

        /// <summary>
        /// 순수 분기(각짐)와 순수 분기(타원)의 임계값에서 혼합 수식이 <b>연속</b>인지.
        /// 끊기면 roundness를 조금 움직였을 때 모양이 튄다.
        /// </summary>
        [Test]
        public void BlendInsetRatio_IsContinuousAtBranchThresholds()
        {
            Assert.AreEqual(0f, UIShapes.BlendInsetRatio(0.01f), 0.01f,
                "각짐 분기(r<=0.01)와 이어져야 한다");
            Assert.AreEqual(0.5f, UIShapes.BlendInsetRatio(0.99f), 0.01f,
                "타원 분기(r>=0.99)와 이어져야 한다");
        }
    }
}
#endif
