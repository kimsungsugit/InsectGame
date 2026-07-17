using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// UI 전역 스케일링 유틸. 가로 화면은 1920x1080, 세로 화면은 1080x1920을
    /// 기준 좌표계로 사용해 휴대폰에서 글자와 터치 버튼이 과도하게 축소되지 않게 합니다.
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
        public const float PortraitReferenceWidth = 1080f;
        public const float PortraitReferenceHeight = 1920f;
        public const float MinTouchHeight = 56f;

        // GUI.matrix 스택 (nested Begin/End 지원)
        private static readonly System.Collections.Generic.Stack<Matrix4x4> matrixStack
            = new System.Collections.Generic.Stack<Matrix4x4>();

        // Scale 캐싱 (프레임마다 한 번씩 계산, 여러 번 참조 시 재사용)
        private static float cachedScale = 1f;
        private static float cachedVirtualW;
        private static float cachedVirtualH;
        private static int lastCacheFrame = -1;

        /// <summary>
        /// 실기기(터치) 또는 세로형 Game View — 터치 친화 UI(큰 버튼, 키 안내 숨김)에 사용.
        /// 화면 방향과 무관하므로 가로로 든 기기에서도 true를 유지합니다.
        /// 기준 해상도(가상 캔버스) 선택에는 쓰지 말 것 — 그건 <see cref="IsPortrait"/> 사용.
        /// </summary>
        public static bool IsMobileLayout => Application.isMobilePlatform || Screen.height > Screen.width * 1.08f;

        /// <summary>현재 화면이 세로 방향인지 여부. 기준 해상도(가상 캔버스) 선택 전용.</summary>
        public static bool IsPortrait => Screen.height > Screen.width;

        private static void RefreshCacheIfStale()
        {
            int f = Time.frameCount;
            if (f == lastCacheFrame) return;
            lastCacheFrame = f;
            // 기준 해상도는 '터치 여부'가 아니라 '실제 화면 방향'으로 선택해야
            // 가로로 든 기기에서도 가로 캔버스(1920x1080)가 적용된다.
            // (IsMobileLayout은 isMobilePlatform 때문에 가로에서도 true라 사용 불가)
            bool portrait = Screen.height > Screen.width;
            float referenceWidth = portrait ? PortraitReferenceWidth : ReferenceWidth;
            float referenceHeight = portrait ? PortraitReferenceHeight : ReferenceHeight;
            float sx = Screen.width / referenceWidth;
            float sy = Screen.height / referenceHeight;
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

        public static float VirtualSafeTop => SafeArea.Top / Scale;
        public static float VirtualSafeBottom => SafeArea.Bottom / Scale;
        public static float VirtualSafeLeft => SafeArea.Left / Scale;
        public static float VirtualSafeRight => SafeArea.Right / Scale;

        public static float ContentWidth(float margin = 24f)
        {
            return Mathf.Max(1f, VirtualScreenWidth - VirtualSafeLeft - VirtualSafeRight - margin * 2f);
        }

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
