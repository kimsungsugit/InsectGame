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

        // ── 뷰포트 컬링 ──
        //
        // IMGUI 스크롤뷰엔 가상화가 없다. 도감 그리드가 128종을 매 패스 전부 그리면서 타일마다
        // 3D 썸네일을 요청하면, 한 뷰포트 분량(24칸)짜리 LRU가 절대 안정되지 않아 렌더러가
        // 프레임마다 곤충 모델을 만들었다 부순다. 이 계산이 그 구간을 좁힌다.

        [Test]
        public void GetVisibleRowRange_TopOfList_StartsAtFirstRow()
        {
            DexBrowseLayout.GetVisibleRowRange(0f, 400f, 100f, 10f, 20, out int first, out int last);

            Assert.AreEqual(0, first);
            Assert.GreaterOrEqual(last, 3, "400px 뷰포트에 110px 행이 최소 3행은 걸친다");
            Assert.Less(last, 20);
        }

        [Test]
        public void GetVisibleRowRange_ScrolledPastTop_ExcludesRowsAbove()
        {
            // 스크롤 550 → 행 5(=550/110)부터. overscan 1행을 빼면 4.
            DexBrowseLayout.GetVisibleRowRange(550f, 400f, 100f, 10f, 40, out int first, out int last);

            Assert.AreEqual(4, first);
            Assert.Less(last, 40);
            Assert.Greater(first, 0, "위쪽 행이 잘려 나가야 컬링이 의미가 있다");
        }

        [Test]
        public void GetVisibleRowRange_BeyondEnd_ClampsToLastRow()
        {
            DexBrowseLayout.GetVisibleRowRange(99999f, 400f, 100f, 10f, 8, out int first, out int last);

            Assert.AreEqual(7, first);
            Assert.AreEqual(7, last);
        }

        [Test]
        public void GetVisibleRowRange_EmptyList_ReturnsEmptyRange()
        {
            DexBrowseLayout.GetVisibleRowRange(0f, 400f, 100f, 10f, 0, out int first, out int last);

            Assert.Greater(first, last, "빈 목록은 for(i=first; i<=last) 가 한 번도 안 돌아야 한다");
        }

        /// <summary>0으로 나누기 방지 — 행 높이·간격이 0이면 stride가 0이 된다.</summary>
        [Test]
        public void GetVisibleRowRange_ZeroStride_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                DexBrowseLayout.GetVisibleRowRange(0f, 400f, 0f, 0f, 5, out _, out _));
        }

        [Test]
        public void GetVisibleItemRange_LastPartialRow_DoesNotExceedItemCount()
        {
            // 10개 / 6열 = 2행. 두 번째 행은 4칸만 찬다.
            DexBrowseLayout.GetVisibleItemRange(0f, 9999f, 100f, 10f, 10, 6,
                out int firstItem, out int lastItem);

            Assert.AreEqual(0, firstItem);
            Assert.AreEqual(9, lastItem, "마지막 행이 덜 찼는데 인덱스가 배열 밖으로 나가면 안 된다");
        }

        [Test]
        public void GetVisibleItemRange_ScrolledDown_SkipsWholeRows()
        {
            DexBrowseLayout.GetVisibleItemRange(1100f, 300f, 100f, 10f, 128, 6,
                out int firstItem, out int lastItem);

            Assert.AreEqual(0, firstItem % 6, "구간은 행 경계에서 시작해야 열 정렬이 유지된다");
            Assert.Greater(firstItem, 0);
            Assert.LessOrEqual(lastItem, 127);
            Assert.GreaterOrEqual(lastItem, firstItem);
        }

        [Test]
        public void GetVisibleItemRange_EmptyOrZeroColumns_IsSafe()
        {
            DexBrowseLayout.GetVisibleItemRange(0f, 400f, 100f, 10f, 0, 6, out int f1, out int l1);
            Assert.Greater(f1, l1);

            Assert.DoesNotThrow(() =>
                DexBrowseLayout.GetVisibleItemRange(0f, 400f, 100f, 10f, 10, 0, out _, out _));
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

        // ── 도감 번호 라벨 ──
        //
        // 인덱스에서만 파생돼 불변인데 타일마다·패스마다 새로 만들어지던 것을 미리 굽는다.
        // 표시는 1-based("NO. 001"이 index 0)라 오프셋이 유일한 위험 지점이다.

        [Test]
        public void NumberLabel_IsOneBasedAndZeroPaddedToThree()
        {
            Assert.AreEqual("NO. 001", DexBrowseLayout.NumberLabel(0));
            Assert.AreEqual("NO. 010", DexBrowseLayout.NumberLabel(9));
            Assert.AreEqual("NO. 128", DexBrowseLayout.NumberLabel(127));
        }

        [Test]
        public void NumberLabel_SameIndex_ReturnsSameInstance()
        {
            // 캐시가 실제로 재사용되는지 — 매번 새 문자열이면 최적화가 사라진 것이다.
            Assert.AreSame(DexBrowseLayout.NumberLabel(5), DexBrowseLayout.NumberLabel(5));
        }

        [Test]
        public void NumberLabel_BeyondInitialCapacity_GrowsInsteadOfThrowing()
        {
            // 곤충이 128종을 넘어도(확장 정의가 이미 64종을 늘렸다) 범위 밖 접근이 나면 안 된다.
            Assert.AreEqual("NO. 301", DexBrowseLayout.NumberLabel(300));
            Assert.AreEqual("NO. 001", DexBrowseLayout.NumberLabel(0), "성장 후에도 앞쪽이 보존돼야 한다");
        }

        [Test]
        public void NumberLabel_NegativeIndex_ReturnsEmptyNotThrow()
        {
            Assert.AreEqual("", DexBrowseLayout.NumberLabel(-1));
        }

        [Test]
        public void GetGridContentHeight_TwoColumns_UsesCeilingRowCount()
        {
            float height = DexBrowseLayout.GetGridContentHeight(5, 2, 164f, 12f);

            Assert.AreEqual(516f, height);
        }

        /// <summary>
        /// 이 판정만 남기고 <c>FormatElementLabel</c>은 호출부 0이라 제거했다(2026-08-03).
        /// 그때 이 술어의 유일한 테스트 경로도 같이 사라지는데, 정작 생산 코드
        /// (<c>DexScreenUI.DrawElementBadges</c>)가 쓰는 건 이쪽이라 직접 고정한다.
        /// </summary>
        [Test]
        public void ShouldShowSecondary_NoneOrDuplicate_HidesSecondBadge()
        {
            Assert.IsFalse(DexBrowseLayout.ShouldShowSecondary(InsectElement.Water, InsectElement.None));
            Assert.IsFalse(DexBrowseLayout.ShouldShowSecondary(InsectElement.Water, InsectElement.Water));
            Assert.IsTrue(DexBrowseLayout.ShouldShowSecondary(InsectElement.Electric, InsectElement.Wind));
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
