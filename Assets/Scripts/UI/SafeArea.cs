using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// Screen.safeArea 기반 인셋(픽셀). 노치/펀치홀/둥근모서리/제스처바 영역을 피해 가장자리 UI를 배치.
    ///
    /// Screen.safeArea는 좌하단 원점(Y-up)이고 IMGUI는 좌상단 원점(Y-down)이라 Top/Bottom을 변환해 제공.
    /// 사용: 상단 앵커 요소는 y에 <see cref="Top"/>를 더하고, 하단 앵커는 <see cref="Bottom"/>를 빼고,
    ///       좌/우 앵커는 <see cref="Left"/>/<see cref="Right"/>만큼 안쪽으로 민다.
    /// </summary>
    public static class SafeArea
    {
        private static int lastFrame = -1;
        private static float top, bottom, left, right;

        private static void Refresh()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;

            Rect sa = Screen.safeArea; // Y-up 픽셀
            left = Mathf.Max(0f, sa.x);
            right = Mathf.Max(0f, Screen.width - (sa.x + sa.width));
            bottom = Mathf.Max(0f, sa.y);
            top = Mathf.Max(0f, Screen.height - (sa.y + sa.height)); // GUI 상단(노치) 인셋
        }

        /// <summary>GUI 상단 인셋(노치/상태바) — 상단 앵커 요소의 y에 더한다.</summary>
        public static float Top { get { Refresh(); return top; } }

        /// <summary>GUI 하단 인셋(제스처바) — 하단 앵커 요소의 y에서 뺀다.</summary>
        public static float Bottom { get { Refresh(); return bottom; } }

        public static float Left { get { Refresh(); return left; } }
        public static float Right { get { Refresh(); return right; } }
    }
}
