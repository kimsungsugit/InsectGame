using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// UI 전역 스케일링 유틸. 기준 해상도(1920x1080)를 가상 좌표계로 제공하며,
    /// 실제 화면에 맞춰 GUI.matrix를 자동 조정합니다.
    ///
    /// 사용법 (주로 모달 UI에 적용):
    ///   private void OnGUI() {
    ///       UIScale.Begin();
    ///       // 기존 코드 — 단, Screen.width 대신 UIScale.VirtualScreenWidth 사용
    ///       UIScale.End();
    ///   }
    /// </summary>
    public static class UIScale
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        // GUI.matrix 스택 (nested Begin/End 지원)
        private static readonly System.Collections.Generic.Stack<Matrix4x4> matrixStack
            = new System.Collections.Generic.Stack<Matrix4x4>();

        // Scale 캐싱 (프레임마다 한 번씩 계산, 여러 번 참조 시 재사용)
        private static float cachedScale = 1f;
        private static float cachedVirtualW;
        private static float cachedVirtualH;
        private static int lastCacheFrame = -1;

        private static void RefreshCacheIfStale()
        {
            int f = Time.frameCount;
            if (f == lastCacheFrame) return;
            lastCacheFrame = f;
            float sx = Screen.width / ReferenceWidth;
            float sy = Screen.height / ReferenceHeight;
            cachedScale = Mathf.Max(0.3f, Mathf.Min(sx, sy));
            cachedVirtualW = Screen.width / cachedScale;
            cachedVirtualH = Screen.height / cachedScale;
        }

        /// <summary>현재 화면의 스케일 팩터. Min을 써서 모달이 화면을 벗어나지 않음.</summary>
        public static float Scale { get { RefreshCacheIfStale(); return cachedScale; } }

        /// <summary>가상 화면 너비 (스케일 적용 후 기준). Screen.width 대신 사용.</summary>
        public static float VirtualScreenWidth { get { RefreshCacheIfStale(); return cachedVirtualW; } }

        /// <summary>가상 화면 높이 (스케일 적용 후 기준). Screen.height 대신 사용.</summary>
        public static float VirtualScreenHeight { get { RefreshCacheIfStale(); return cachedVirtualH; } }

        /// <summary>OnGUI 시작 시 호출. GUI.matrix를 스케일 적용 상태로 바꿉니다.</summary>
        public static void Begin()
        {
            matrixStack.Push(GUI.matrix);
            RefreshCacheIfStale();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(cachedScale, cachedScale, 1f));
        }

        /// <summary>Begin과 짝으로 호출. matrix 원복.</summary>
        public static void End()
        {
            if (matrixStack.Count > 0)
                GUI.matrix = matrixStack.Pop();
        }

        /// <summary>HUD 등에서 부분 적용용 — 값만 스케일.</summary>
        public static float Value(float v) => v * Scale;

        /// <summary>폰트 크기 스케일 적용 (정수 반환).</summary>
        public static int FontSize(int baseSize) => Mathf.RoundToInt(baseSize * Scale);

        /// <summary>마우스 위치를 가상 좌표계로 변환 (matrix 적용 중이면 자동으로 쓰이지만 수동 계산 시 사용).</summary>
        public static Vector2 VirtualMousePosition
        {
            get
            {
                float s = Scale;
                return new Vector2(Input.mousePosition.x / s, (Screen.height - Input.mousePosition.y) / s);
            }
        }
    }
}
