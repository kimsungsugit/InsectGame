#if UNITY_EDITOR
using InsectGame.UI;
using NUnit.Framework;

namespace InsectGame.Tests
{
    [TestFixture]
    public class UIDirectScrollTests
    {
        [Test]
        public void ClampScrollY_ContentShorterThanViewport_ReturnsZero()
        {
            Assert.AreEqual(0f, UIDirectScroll.ClampScrollY(120f, 600f, 400f));
        }

        [Test]
        public void ApplyDragDelta_FingerMovesUp_ScrollsListDown()
        {
            float result = UIDirectScroll.ApplyDragDelta(100f, -45f, 300f, 900f);

            Assert.AreEqual(145f, result);
        }

        [Test]
        public void ApplyDragDelta_AtEitherEnd_ClampsToContentBounds()
        {
            Assert.AreEqual(0f, UIDirectScroll.ApplyDragDelta(10f, 80f, 300f, 900f));
            Assert.AreEqual(600f, UIDirectScroll.ApplyDragDelta(590f, -80f, 300f, 900f));
        }

        [Test]
        public void IsVerticalDrag_HorizontalOrTinyMovement_DoesNotCaptureList()
        {
            Assert.IsFalse(UIDirectScroll.IsVerticalDrag(new UnityEngine.Vector2(20f, 10f)));
            Assert.IsFalse(UIDirectScroll.IsVerticalDrag(new UnityEngine.Vector2(2f, 7f)));
            Assert.IsTrue(UIDirectScroll.IsVerticalDrag(new UnityEngine.Vector2(6f, 12f)));
        }

        [Test]
        public void IsGestureBeyondThreshold_HorizontalSwipe_CancelsTapWithoutScrolling()
        {
            UnityEngine.Vector2 horizontalSwipe = new UnityEngine.Vector2(20f, 3f);

            Assert.IsTrue(UIDirectScroll.IsGestureBeyondThreshold(horizontalSwipe));
            Assert.IsFalse(UIDirectScroll.IsVerticalDrag(horizontalSwipe));
        }

        /// <summary>
        /// 모달이 겹친 배경 목록은 입력을 받지 않지만 위치 clamp는 계속해야 한다.
        /// clamp보다 앞에 비활성 가드를 두면 모달이 열린 사이 배경 내용이 짧아졌을 때
        /// 범위 밖 스크롤이 그대로 남아, 모달을 닫는 순간 빈 공간이 보인다.
        /// </summary>
        [Test]
        public void Handle_NotInteractive_StillClampsScrollAndClearsGesture()
        {
            UIDirectScroll scroll = new UIDirectScroll();
            UnityEngine.Vector2 position = new UnityEngine.Vector2(37f, 9999f);

            bool consumed = scroll.Handle(
                ref position,
                new UnityEngine.Rect(0f, 0f, 400f, 300f),
                900f,
                36f,
                false);

            Assert.IsFalse(consumed);
            Assert.IsFalse(scroll.IsDragging);
            Assert.AreEqual(0f, position.x);
            Assert.AreEqual(600f, position.y);
        }

        [Test]
        public void CollectionDetail_MaxLearnset_IsTallerThanLowerViewportAndScrollable()
        {
            float contentHeight = CollectionUI.GetDetailLowerContentHeight(6, false);

            Assert.Greater(contentHeight, 626f);
            Assert.Greater(UIDirectScroll.ClampScrollY(10000f, 626f, contentHeight), 0f);
        }

        [Test]
        public void TrainingSkillReplacement_SixCards_RequiresDirectScroll()
        {
            float contentHeight = TrainingUI.GetSkillReplacementContentHeight(6);

            Assert.AreEqual(864f, contentHeight);
            Assert.Greater(contentHeight, 712f);
        }
    }
}
#endif
