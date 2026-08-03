#if UNITY_EDITOR
using InsectGame.Data;
using InsectGame.Dex;
using NUnit.Framework;

namespace InsectGame.Tests
{
    [TestFixture]
    public class DexBrowseLayoutTests
    {
        [Test]
        public void WrapIndex_NoSelectionForward_SelectsFirst()
        {
            Assert.AreEqual(0, DexBrowseLayout.WrapIndex(-1, 1, 128));
        }

        [Test]
        public void WrapIndex_AtEitherEnd_WrapsAround()
        {
            Assert.AreEqual(0, DexBrowseLayout.WrapIndex(127, 1, 128));
            Assert.AreEqual(127, DexBrowseLayout.WrapIndex(0, -1, 128));
        }

        [Test]
        public void WrapIndex_RowStepDelta_MovesExactlyOneGridRow()
        {
            // ★ 회귀 고정 — 도감이 좌측 1열 리스트에서 전체 폭 그리드로 바뀐 뒤,
            // ↑↓가 계속 ±1이라 6열에서 여섯 번 눌러야 한 줄 내려갔다.
            // 행 이동은 delta = 열 수여야 한다.
            const int columns = 6;
            Assert.AreEqual(6, DexBrowseLayout.WrapIndex(0, columns, 128));
            Assert.AreEqual(0, DexBrowseLayout.WrapIndex(6, -columns, 128));
            // 같은 열을 유지해야 한다(인덱스 % 열 수가 보존).
            Assert.AreEqual(2 % columns, DexBrowseLayout.WrapIndex(2, columns, 128) % columns);
        }

        [Test]
        public void WrapIndex_RowStepPastEnd_WrapsToTop()
        {
            const int columns = 6;
            // 마지막 행 근처에서 ↓를 누르면 맨 위로 감기되 인덱스가 범위를 벗어나면 안 된다.
            for (int i = 120; i < 128; i++)
            {
                int next = DexBrowseLayout.WrapIndex(i, columns, 128);
                Assert.GreaterOrEqual(next, 0);
                Assert.Less(next, 128);
            }
        }

        [Test]
        public void GetGridContentHeight_TwoColumns_UsesCeilingRowCount()
        {
            float height = DexBrowseLayout.GetGridContentHeight(5, 2, 164f, 12f);

            Assert.AreEqual(516f, height);
        }

        [Test]
        public void FormatElementLabel_SecondaryNoneOrDuplicate_ShowsPrimaryOnly()
        {
            Assert.AreEqual(
                "물",
                DexBrowseLayout.FormatElementLabel(InsectElement.Water, InsectElement.None));
            Assert.AreEqual(
                "물",
                DexBrowseLayout.FormatElementLabel(InsectElement.Water, InsectElement.Water));
        }

        [Test]
        public void FormatElementLabel_DualType_ShowsBothKoreanNames()
        {
            Assert.AreEqual(
                "전기 / 바람",
                DexBrowseLayout.FormatElementLabel(InsectElement.Electric, InsectElement.Wind));
        }

        [Test]
        public void GetItemContentHeight_MoreThanSixRows_ExceedsLandscapeViewport()
        {
            float height = DexBrowseLayout.GetItemContentHeight(8, 74f, 136f, 10f, 20f);

            Assert.Greater(height, 900f);
        }

        // ── 반응형 그리드 열 수 ──
        // 도감이 좌우 분할(좌 목록 + 우 상세)을 버리고 전체 폭 그리드가 되면서,
        // 가로·세로 구분 없이 이 계산 하나가 열 수를 정한다.

        [Test]
        public void GetGridColumns_LandscapeContentWidth_FillsSixColumns()
        {
            // 1920 기준 콘텐츠 폭 ≈ 1850 → 260px 타일이 6칸 이상 들어가지만 상한이 6이다.
            Assert.AreEqual(6, DexBrowseLayout.GetGridColumns(1850f, 260f, 14f));
        }

        [Test]
        public void GetGridColumns_PortraitContentWidth_UsesThreeColumns()
        {
            // 1080 세로 기준 콘텐츠 폭 ≈ 1010 → (1010+14)/(260+14) = 3.7 → 3열
            Assert.AreEqual(3, DexBrowseLayout.GetGridColumns(1010f, 260f, 14f));
        }

        [Test]
        public void GetGridColumns_NarrowWidth_ClampsToMinimum()
        {
            // 한 칸도 못 들어가는 폭이어도 최소 2열은 유지한다(1열이면 옛 목록으로 되돌아간다).
            Assert.AreEqual(2, DexBrowseLayout.GetGridColumns(200f, 260f, 14f));
        }

        [Test]
        public void GetGridColumns_ExactFit_DoesNotOverflow()
        {
            // 4칸 + 3간격이 정확히 들어맞는 폭에서 5열로 새지 않아야 한다.
            float exact = 4 * 260f + 3 * 14f;
            Assert.AreEqual(4, DexBrowseLayout.GetGridColumns(exact, 260f, 14f));
        }

        [Test]
        public void GetGridColumns_JustUnderNextColumn_StaysAtLower()
        {
            float justUnder = 4 * 260f + 3 * 14f - 1f;
            Assert.AreEqual(3, DexBrowseLayout.GetGridColumns(justUnder, 260f, 14f));
        }

        [Test]
        public void GetGridColumns_NonPositiveInput_ReturnsMinimum()
        {
            Assert.AreEqual(2, DexBrowseLayout.GetGridColumns(0f, 260f, 14f));
            Assert.AreEqual(2, DexBrowseLayout.GetGridColumns(1850f, 0f, 14f));
        }

        [Test]
        public void GetGridColumns_CustomBounds_AreRespected()
        {
            Assert.AreEqual(4, DexBrowseLayout.GetGridColumns(5000f, 260f, 14f, 1, 4));
            Assert.AreEqual(3, DexBrowseLayout.GetGridColumns(100f, 260f, 14f, 3, 6));
        }
    }
}
#endif
