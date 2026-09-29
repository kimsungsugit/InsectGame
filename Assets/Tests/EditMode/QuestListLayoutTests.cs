#if UNITY_EDITOR
using InsectGame.UI;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 퀘스트 목록이 아코디언으로 펼쳐지면서 행 높이가 가변이 됐다.
    /// 스크롤 콘텐츠 높이를 옛 방식(rowH × count)으로 잡으면 펼친 행 아래가 잘린다.
    /// </summary>
    [TestFixture]
    public class QuestListLayoutTests
    {
        private const float Delta = 0.01f;

        [Test]
        public void GetRowHeight_Collapsed_IsRowHeight()
        {
            Assert.AreEqual(QuestListLayout.RowHeight, QuestListLayout.GetRowHeight(false), Delta);
        }

        [Test]
        public void GetRowHeight_Expanded_AddsExtra()
        {
            Assert.AreEqual(
                QuestListLayout.RowHeight + QuestListLayout.ExpandedExtra,
                QuestListLayout.GetRowHeight(true),
                Delta);
        }

        [Test]
        public void GetContentHeight_StoryOnly_OmitsSideHeader()
        {
            // 서브가 없으면 서브 섹션 헤더도 그리지 않는다(기존 렌더 동작).
            float expected = QuestListLayout.SectionHeaderHeight + 5 * QuestListLayout.RowHeight;
            Assert.AreEqual(expected, QuestListLayout.GetContentHeight(5, 0, 0), Delta);
        }

        [Test]
        public void GetContentHeight_WithSide_IncludesBothHeaders()
        {
            float expected = QuestListLayout.SectionHeaderHeight * 2f
                + 5 * QuestListLayout.RowHeight
                + 3 * QuestListLayout.RowHeight;
            Assert.AreEqual(expected, QuestListLayout.GetContentHeight(5, 3, 0), Delta);
        }

        [Test]
        public void GetContentHeight_OneExpanded_AddsExactlyOneExtra()
        {
            float collapsed = QuestListLayout.GetContentHeight(5, 3, 0);
            Assert.AreEqual(
                collapsed + QuestListLayout.ExpandedExtra,
                QuestListLayout.GetContentHeight(5, 3, 1),
                Delta);
        }

        [Test]
        public void GetRowHeight_TrackableExpandedRow_AddsFollowButtonSpace_OnlyWhenExpanded()
        {
            Assert.AreEqual(
                QuestListLayout.RowHeight + QuestListLayout.ExpandedExtra + QuestListLayout.TrackButtonExtra,
                QuestListLayout.GetRowHeight(true, true), Delta);
            // 접힌 행엔 버튼이 없다 — 따라갈 수 있어도 높이는 그대로다.
            Assert.AreEqual(QuestListLayout.RowHeight, QuestListLayout.GetRowHeight(false, true), Delta);
        }

        [Test]
        public void GetContentHeight_TrackableExpanded_MatchesRowHeight_AndIsClampedToExpanded()
        {
            // 행 높이와 콘텐츠 높이가 같은 답을 내야 스크롤 끝의 [따라가기]가 잘리지 않는다.
            float collapsed = QuestListLayout.GetContentHeight(5, 3, 0);
            Assert.AreEqual(
                collapsed + QuestListLayout.GetRowHeight(true, true) - QuestListLayout.RowHeight,
                QuestListLayout.GetContentHeight(5, 3, 1, 1), Delta);
            // 펼친 행이 없는데 버튼 수만 넘어오면 무시한다.
            Assert.AreEqual(collapsed, QuestListLayout.GetContentHeight(5, 3, 0, 1), Delta);
        }

        [Test]
        public void GetContentHeight_ExpandedCountAboveRowCount_IsClamped()
        {
            // 펼침 수가 행 수를 넘으면 콘텐츠가 실제보다 길어져 빈 스크롤이 생긴다.
            float expected = QuestListLayout.GetContentHeight(2, 0, 2);
            Assert.AreEqual(expected, QuestListLayout.GetContentHeight(2, 0, 99), Delta);
        }

        [Test]
        public void GetContentHeight_NegativeCounts_TreatedAsZero()
        {
            Assert.AreEqual(
                QuestListLayout.SectionHeaderHeight,
                QuestListLayout.GetContentHeight(-3, -1, -5),
                Delta);
        }

        [Test]
        public void GetContentHeight_EmptyList_StillHasStoryHeader()
        {
            // 스토리 헤더는 항상 그린다 — 목록이 비어도 섹션 제목은 남는다.
            Assert.AreEqual(
                QuestListLayout.SectionHeaderHeight,
                QuestListLayout.GetContentHeight(0, 0, 0),
                Delta);
        }

        [Test]
        public void TrackButton_FitsInsideReservedSpace_AndMeetsTouchMinimum()
        {
            // 버튼 기하(TutorialQuestUI)와 행 높이 예약(TrackButtonExtra)이 따로 놀면 버튼이 다음 행을 덮는다.
            Assert.LessOrEqual(QuestListLayout.TrackButtonTopGap + QuestListLayout.TrackButtonHeight,
                QuestListLayout.TrackButtonExtra);
            Assert.GreaterOrEqual(QuestListLayout.TrackButtonHeight, UIScale.MinTouchHeight);
        }
    }
}
#endif
