#if UNITY_EDITOR
using InsectGame.UI;
using NUnit.Framework;

namespace InsectGame.Tests
{
    [TestFixture]
    public class UISafeLayoutTests
    {
        private const float Delta = 0.01f;

        [Test]
        public void Margin_SmallScreen_ClampsToMin()
        {
            Assert.AreEqual(UISafeLayout.MinMargin, UISafeLayout.Compute(500f, 0f, 0f).Margin, Delta);
        }

        [Test]
        public void Margin_ReferenceLandscape_UsesRatio()
        {
            // 1080 × 0.03 = 32.4 — Min(24)과 Max(64) 사이라 비율이 그대로 쓰인다.
            Assert.AreEqual(32.4f, UISafeLayout.Compute(1080f, 0f, 0f).Margin, Delta);
        }

        [Test]
        public void Margin_TallScreen_ClampsToMax()
        {
            Assert.AreEqual(UISafeLayout.MaxMargin, UISafeLayout.Compute(3000f, 0f, 0f).Margin, Delta);
        }

        [Test]
        public void Compute_WithInsets_ShrinksExtentByInsetsAndBothMargins()
        {
            UISafeLayout.SafeBox box = UISafeLayout.Compute(1920f, 90f, 30f);

            float margin = 57.6f; // 1920 × 0.03
            Assert.AreEqual(margin, box.Margin, Delta);
            Assert.AreEqual(90f + margin, box.Start, Delta);
            Assert.AreEqual(1920f - 90f - 30f - margin * 2f, box.Extent, Delta);
            Assert.AreEqual(box.Start + box.Extent, box.End, Delta);
        }

        [Test]
        public void ClampSize_DesiredExceedsSafeArea_ReturnsAvailable()
        {
            UISafeLayout.SafeBox box = UISafeLayout.ComputeWithMargin(1080f, 0f, 0f, 24f);

            // 1080 - 48 = 1032 만 쓸 수 있다.
            Assert.AreEqual(1032f, UISafeLayout.ClampSize(2000f, box), Delta);
            Assert.AreEqual(940f, UISafeLayout.ClampSize(940f, box), Delta);
        }

        [Test]
        public void CenterStart_AsymmetricInsets_CentersInsideSafeArea()
        {
            // 상단 노치 90 / 하단 제스처바 30 — 화면 중앙이 아니라 안전 영역 중앙에 와야 한다.
            UISafeLayout.SafeBox box = UISafeLayout.Compute(1920f, 90f, 30f);
            float y = UISafeLayout.CenterStart(600f, box);

            Assert.AreEqual(box.Start + (box.Extent - 600f) * 0.5f, y, Delta);
            Assert.GreaterOrEqual(y, box.Start);
            Assert.LessOrEqual(y + 600f, box.End);
        }

        [Test]
        public void CenterStart_OversizedPanel_StaysInsideSafeArea()
        {
            UISafeLayout.SafeBox box = UISafeLayout.Compute(1080f, 60f, 60f);

            // 안전 영역보다 큰 패널을 요청해도 시작점이 위로 밀려나지 않는다(음수 오프셋 방지).
            Assert.AreEqual(box.Start, UISafeLayout.CenterStart(5000f, box), Delta);
        }

        [Test]
        public void EndStart_BottomAnchored_SitsAboveGestureBar()
        {
            UISafeLayout.SafeBox box = UISafeLayout.Compute(1920f, 0f, 48f);
            float y = UISafeLayout.EndStart(200f, box);

            Assert.AreEqual(box.End - 200f, y, Delta);
            Assert.LessOrEqual(y + 200f, 1920f - 48f);
        }

        [Test]
        public void AlignStart_LeftAndRight_HugOppositeEdges()
        {
            UISafeLayout.SafeBox box = UISafeLayout.ComputeWithMargin(1920f, 0f, 0f, UISafeLayout.MarginX);

            Assert.AreEqual(box.Start, UISafeLayout.AlignStart(400f, UISafeLayout.HAlign.Left, box), Delta);
            Assert.AreEqual(box.End - 400f, UISafeLayout.AlignStart(400f, UISafeLayout.HAlign.Right, box), Delta);
        }

        [Test]
        public void Compute_NoInsets_MatchesLegacyHospitalPattern()
        {
            // 회귀 방지: 인셋이 없고 마진이 24일 때 기존 HospitalUI 3줄 관용구와 같은 값이 나와야 한다.
            //   availH = 1080; ph = Min(940, availH - 24) = 940; py = 0 + (1080 - 940) * 0.5 = 70
            const float virtualH = 1080f;
            float legacyAvailH = virtualH;
            float legacyPh = System.Math.Min(940f, legacyAvailH - 24f);
            float legacyPy = (legacyAvailH - legacyPh) * 0.5f;

            UISafeLayout.SafeBox box = UISafeLayout.ComputeWithMargin(virtualH, 0f, 0f, 24f);
            float ph = UISafeLayout.ClampSize(940f, box);
            float py = UISafeLayout.CenterStart(940f, box);

            Assert.AreEqual(legacyPh, ph, Delta);
            Assert.AreEqual(legacyPy, py, Delta);
        }

        [Test]
        public void MinimapStackBelowY_SitsJustUnderMinimapPanel()
        {
            // 퀘스트 칩은 예전에 `ContentTop + 380f`로 미니맵 기하(150 + 220)를 손으로 베껴
            // 갖고 있었다. 미니맵 크기를 바꾸면 조용히 겹치거나 벌어지므로 관계를 고정한다.
            // 두 값 모두 같은 ContentTop을 쓰므로 화면 상태와 무관하게 상쇄된다.
            float minimapBottomOffset = MinimapUI.TopOffset + MinimapUI.PanelSize;
            float stackOffset = MinimapUI.StackBelowY - UISafeLayout.ContentTop;

            Assert.GreaterOrEqual(stackOffset, minimapBottomOffset,
                "미니맵 아래 HUD가 미니맵 위로 파고든다");
            Assert.LessOrEqual(stackOffset - minimapBottomOffset, 24f,
                "미니맵과 아래 HUD 사이가 너무 벌어졌다");
        }
    }
}
#endif
