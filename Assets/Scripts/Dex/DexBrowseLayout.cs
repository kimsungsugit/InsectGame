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

        /// <summary>
        /// 스크롤 뷰포트에 실제로 걸치는 행 구간 <c>[first, last]</c>. 항목이 없으면 first &gt; last.
        ///
        /// IMGUI 스크롤뷰에는 가상화가 없어서 <b>호출부가 컬링하지 않으면 화면 밖 항목까지 전부
        /// 그려진다.</b> 도감은 그 위에 3D 썸네일 캐시가 얹혀 있어 대가가 특히 크다 —
        /// 128종을 매 패스 요청하는데 캐시는 한 뷰포트 분량(24칸)이라, 적중이 LRU를 훑고 지나가
        /// 캐시가 절대 안정되지 않는다. 그러면 <c>InsectModelPreviewRenderer</c>가 프레임마다
        /// 곤충 모델을 통째로 만들었다 부수고 RenderTexture를 create/Release 한다.
        /// (그 렌더러가 "프레임당 1개만 렌더한다"고 못박은 전제가 호출부에서 깨져 있었다.)
        ///
        /// 위아래로 <paramref name="overscanRows"/>행씩 여유를 둬 스크롤 중 빈칸이 보이지 않게 한다.
        /// </summary>
        public static void GetVisibleRowRange(
            float scrollY,
            float viewportHeight,
            float cardHeight,
            float gap,
            int rowCount,
            out int first,
            out int last,
            int overscanRows = 1)
        {
            first = 0;
            last = -1;
            if (rowCount <= 0) return;

            float stride = Mathf.Max(0.0001f, cardHeight + Mathf.Max(0f, gap));
            int over = Mathf.Max(0, overscanRows);

            int top = Mathf.FloorToInt(Mathf.Max(0f, scrollY) / stride) - over;
            int bottom = Mathf.FloorToInt((Mathf.Max(0f, scrollY) + Mathf.Max(0f, viewportHeight)) / stride) + over;

            first = Mathf.Clamp(top, 0, rowCount - 1);
            last = Mathf.Clamp(bottom, 0, rowCount - 1);
        }

        /// <summary>행 구간을 항목 인덱스 구간으로 옮긴다. 마지막 행이 덜 찼을 때 상한을 넘지 않는다.</summary>
        public static void GetVisibleItemRange(
            float scrollY,
            float viewportHeight,
            float cardHeight,
            float gap,
            int itemCount,
            int columns,
            out int firstItem,
            out int lastItem,
            int overscanRows = 1)
        {
            firstItem = 0;
            lastItem = -1;
            if (itemCount <= 0) return;

            int safeColumns = Mathf.Max(1, columns);
            int rows = Mathf.CeilToInt(itemCount / (float)safeColumns);

            GetVisibleRowRange(scrollY, viewportHeight, cardHeight, gap, rows,
                out int firstRow, out int lastRow, overscanRows);
            if (lastRow < firstRow) return;

            firstItem = firstRow * safeColumns;
            lastItem = Mathf.Min(itemCount - 1, (lastRow + 1) * safeColumns - 1);
        }

        /// <summary>
        /// 도감 번호 라벨 "NO. 001" — 인덱스 하나에서만 파생되므로 세션 내내 불변이다.
        ///
        /// 그런데 호출부(<c>DexScreenUI.DrawDexTile</c>)는 타일마다, 그리고 <b>OnGUI 패스마다</b>
        /// 이 문자열을 새로 만들고 있었다(IMGUI는 한 프레임에 Layout·Repaint·입력마다 패스가 돈다).
        /// 미리 구워두고 인덱스로 꺼낸다 — 128종이면 배열 한 번, 그 뒤로는 할당 0.
        /// </summary>
        private static string[] numberLabels;

        public static string NumberLabel(int index)
        {
            if (index < 0) return "";
            if (numberLabels == null || numberLabels.Length <= index)
            {
                string[] grown = new string[Mathf.Max(index + 1, 128)];
                for (int i = 0; i < grown.Length; i++) grown[i] = $"NO. {i + 1:D3}";
                numberLabels = grown;
            }
            return numberLabels[index];
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
