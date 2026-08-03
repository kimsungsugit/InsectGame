using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Dex
{
    /// <summary>
    /// 도감 탐색의 순수 계산만 모은 헬퍼.
    /// IMGUI 렌더와 분리해 순환 선택, 그리드 높이, 속성 표기를 PlayMode 테스트로 검증한다.
    /// </summary>
    public static class DexBrowseLayout
    {
        public static int WrapIndex(int currentIndex, int delta, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            int origin = currentIndex;
            if (origin < 0 || origin >= count)
            {
                origin = delta < 0 ? 0 : -1;
            }

            int wrapped = (origin + delta) % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        /// <summary>
        /// 뷰포트 폭에서 그리드 열 수를 도출한다. 도감은 좌우 분할을 버리고 전체 폭 그리드
        /// 하나로 바뀌었으므로, 가로/세로 어느 쪽이든 이 계산 하나만 탄다.
        /// 한 칸이 <paramref name="targetCardWidth"/>보다 작아지지 않게 열 수를 정하고
        /// <paramref name="minColumns"/>~<paramref name="maxColumns"/>로 가둔다.
        /// </summary>
        public static int GetGridColumns(
            float viewportWidth,
            float targetCardWidth,
            float gap,
            int minColumns = 2,
            int maxColumns = 6)
        {
            int lo = Mathf.Max(1, minColumns);
            int hi = Mathf.Max(lo, maxColumns);
            if (viewportWidth <= 0f || targetCardWidth <= 0f)
            {
                return lo;
            }

            // n열이 들어가려면 n*card + (n-1)*gap <= width.
            int fit = Mathf.FloorToInt((viewportWidth + Mathf.Max(0f, gap)) / (targetCardWidth + Mathf.Max(0f, gap)));
            return Mathf.Clamp(fit, lo, hi);
        }

        public static float GetGridContentHeight(int itemCount, int columns, float cardHeight, float gap)
        {
            if (itemCount <= 0)
            {
                return 0f;
            }

            int safeColumns = Mathf.Max(1, columns);
            int rows = Mathf.CeilToInt(itemCount / (float)safeColumns);
            return rows * Mathf.Max(0f, cardHeight) + Mathf.Max(0, rows - 1) * Mathf.Max(0f, gap);
        }

        public static float GetItemContentHeight(
            int rowCount,
            float headerHeight,
            float rowHeight,
            float gap,
            float bottomPadding)
        {
            int safeRows = Mathf.Max(0, rowCount);
            float rowsHeight = safeRows * Mathf.Max(0f, rowHeight);
            float gapsHeight = Mathf.Max(0, safeRows - 1) * Mathf.Max(0f, gap);
            return Mathf.Max(0f, headerHeight) + rowsHeight + gapsHeight + Mathf.Max(0f, bottomPadding);
        }

        /// <summary>
        /// 보조 속성을 따로 표기할지. UI는 이 판정만 받아 배지를 하나 더 그린다
        /// (<c>DexScreenUI.DrawElementBadges</c>).
        ///
        /// 여기에 "속성 / 속성" 한 줄짜리 라벨을 만드는 <c>FormatElementLabel</c>이 같이 있었는데
        /// 호출부가 0이었다(2026-08-03 제거). 이름만 보면 속성 표기의 정석 API라 누가 쓰는 순간
        /// 배지 경로와 두 갈래가 됐을 자리다 — <c>RaidRoundModels.SetBossDamage</c>와 같은 형태.
        /// 한 줄 라벨이 다시 필요해지면 배지 쪽을 대체할지부터 정하고 되살릴 것.
        /// </summary>
        public static bool ShouldShowSecondary(InsectElement primary, InsectElement secondary)
        {
            return secondary != InsectElement.None && secondary != primary;
        }
    }
}
