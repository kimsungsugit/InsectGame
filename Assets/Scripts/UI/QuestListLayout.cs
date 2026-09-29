using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 퀘스트 목록의 순수 높이 계산.
    ///
    /// 행이 아코디언으로 펼쳐지면서 높이가 가변이 됐다. 옛 코드처럼 <c>rowH * count</c>로
    /// 스크롤 콘텐츠 높이를 잡으면 펼친 행 아래가 잘리거나 스크롤이 모자란다.
    /// IMGUI 렌더와 분리해 PlayMode 테스트로 고정한다(<c>DexBrowseLayout</c>과 같은 성격).
    /// </summary>
    public static class QuestListLayout
    {
        public const float RowHeight = 66f;
        public const float SectionHeaderHeight = 34f;

        /// <summary>펼쳤을 때 행 아래에 덧붙는 높이 — 설명 + 진행 바 + 보상 전체.</summary>
        public const float ExpandedExtra = 158f;

        /// <summary>
        /// 펼친 행에 [따라가기] 버튼이 붙을 때 더 붙는 높이(마을 의뢰). 버튼 64px — 모바일 터치 하한
        /// (<c>UIScale.MinTouchHeight</c>)을 넘기고 위아래 여백을 둔다.
        /// </summary>
        public const float TrackButtonExtra = 76f;

        /// <summary>[따라가기] 버튼 높이와 펼친 내용 아래 여백 — 합이 <see cref="TrackButtonExtra"/> 안이어야 한다.</summary>
        public const float TrackButtonHeight = 64f;
        public const float TrackButtonTopGap = 4f;

        public static float GetRowHeight(bool expanded, bool trackable = false)
        {
            if (!expanded) return RowHeight;
            return RowHeight + ExpandedExtra + (trackable ? TrackButtonExtra : 0f);
        }

        /// <summary>
        /// 스크롤 콘텐츠 총 높이. 스토리 섹션 헤더는 항상 그리고, 서브 섹션 헤더는
        /// 서브 퀘스트가 하나라도 있을 때만 그린다(기존 렌더 동작과 동일).
        /// <paramref name="expandedTrackableCount"/>는 펼친 행 중 [따라가기]가 붙는 수 —
        /// 행 높이(<see cref="GetRowHeight"/>)와 같은 판정을 넘겨야 스크롤 끝이 잘리지 않는다.
        /// </summary>
        public static float GetContentHeight(int storyCount, int sideCount, int expandedCount,
            int expandedTrackableCount = 0)
        {
            int story = Mathf.Max(0, storyCount);
            int side = Mathf.Max(0, sideCount);
            int expanded = Mathf.Clamp(Mathf.Max(0, expandedCount), 0, story + side);
            int trackable = Mathf.Clamp(Mathf.Max(0, expandedTrackableCount), 0, expanded);

            float height = SectionHeaderHeight + story * RowHeight;
            if (side > 0)
            {
                height += SectionHeaderHeight + side * RowHeight;
            }
            return height + expanded * ExpandedExtra + trackable * TrackButtonExtra;
        }
    }
}
