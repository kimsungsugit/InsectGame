using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 세이프에어리어 + 세로 마진을 내장한 UI 배치 하네스.
    ///
    /// <see cref="UIScale"/>이 "가상 좌표계 변환"을 맡는다면 여기는 "그 좌표계 안 어디에 놓을지"를 맡는다.
    /// 패널의 y와 height를 손으로 계산하지 말고 여기서 <see cref="Rect"/>를 받아 쓴다:
    /// <code>
    ///   Rect panel = UISafeLayout.CenteredPanel(1000f, 940f);   // 높이는 안전 영역 안으로 자동 clamp
    /// </code>
    ///
    /// 세로 마진은 화면 높이의 3%(24~64px)다. 세이프에어리어(노치·제스처바) 위에 추가로 얹는다 —
    /// 인셋이 0인 데스크톱에서도 가장자리에 붙지 않게, 인셋이 있는 기기에서는 그만큼 더 안쪽으로.
    /// 가로 마진은 기존 <see cref="UIScale.ContentWidth"/> 기본값과 같은 24px 고정이다(세로만 늘린다).
    ///
    /// <see cref="UIScale.Begin"/>을 쓰지 않는 픽셀 좌표계 UI는 <see cref="Px"/> 파사드를 쓴다.
    /// </summary>
    public static class UISafeLayout
    {
        /// <summary>세로 마진 = 화면 높이 × 이 비율 (Min/Max로 clamp).</summary>
        public const float MarginRatio = 0.03f;
        public const float MinMargin = 24f;
        public const float MaxMargin = 64f;

        /// <summary>가로 마진. 세로와 달리 화면 크기에 비례시키지 않는다 — 기존 레이아웃 폭을 유지하기 위함.</summary>
        public const float MarginX = 24f;

        public enum HAlign { Left, Center, Right }

        /// <summary>한 축의 안전 배치 범위. <see cref="Compute"/>가 만든다.</summary>
        public struct SafeBox
        {
            /// <summary>콘텐츠 시작 좌표 (인셋 + 마진).</summary>
            public float Start;
            /// <summary>콘텐츠 끝 좌표 (= Start + Extent).</summary>
            public float End;
            /// <summary>콘텐츠 길이.</summary>
            public float Extent;
            /// <summary>이 축에 적용된 마진.</summary>
            public float Margin;
        }

        // ── 순수 계산부 (Screen 비의존 — PlayMode 테스트가 여기를 검증한다) ──

        /// <summary>
        /// 한 축의 안전 범위를 계산한다. 세로는 (화면높이, safeTop, safeBottom),
        /// 가로는 <see cref="ComputeX"/>가 고정 마진으로 호출한다.
        /// </summary>
        public static SafeBox Compute(float extent, float insetStart, float insetEnd)
        {
            float margin = Mathf.Clamp(extent * MarginRatio, MinMargin, MaxMargin);
            return Build(extent, insetStart, insetEnd, margin);
        }

        /// <summary>마진을 직접 지정하는 계산. 가로축(고정 24px)과 테스트에서 쓴다.</summary>
        public static SafeBox ComputeWithMargin(float extent, float insetStart, float insetEnd, float margin)
        {
            return Build(extent, insetStart, insetEnd, Mathf.Max(0f, margin));
        }

        private static SafeBox Build(float extent, float insetStart, float insetEnd, float margin)
        {
            float start = Mathf.Max(0f, insetStart) + margin;
            float available = Mathf.Max(1f, extent - Mathf.Max(0f, insetStart) - Mathf.Max(0f, insetEnd) - margin * 2f);
            return new SafeBox { Start = start, End = start + available, Extent = available, Margin = margin };
        }

        /// <summary>원하는 크기를 안전 범위 안으로 제한한다.</summary>
        public static float ClampSize(float desired, in SafeBox box)
        {
            return Mathf.Min(Mathf.Max(1f, desired), box.Extent);
        }

        /// <summary>안전 범위 중앙에 놓았을 때의 시작 좌표. 크기가 범위를 넘으면 Start에 붙인다.</summary>
        public static float CenterStart(float size, in SafeBox box)
        {
            return box.Start + (box.Extent - ClampSize(size, box)) * 0.5f;
        }

        /// <summary>안전 범위 끝에 붙였을 때의 시작 좌표(하단/우측 앵커).</summary>
        public static float EndStart(float size, in SafeBox box)
        {
            return box.End - ClampSize(size, box);
        }

        public static float AlignStart(float size, HAlign align, in SafeBox box)
        {
            switch (align)
            {
                case HAlign.Left: return box.Start;
                case HAlign.Right: return EndStart(size, box);
                default: return CenterStart(size, box);
            }
        }

        // ── 가상 좌표계 파사드 (UIScale.Begin 안에서 쓴다) ──

        /// <summary>현재 화면의 세로 안전 범위.</summary>
        public static SafeBox VerticalBox =>
            Compute(UIScale.VirtualScreenHeight, UIScale.VirtualSafeTop, UIScale.VirtualSafeBottom);

        /// <summary>현재 화면의 가로 안전 범위.</summary>
        public static SafeBox HorizontalBox =>
            ComputeWithMargin(UIScale.VirtualScreenWidth, UIScale.VirtualSafeLeft, UIScale.VirtualSafeRight, MarginX);

        public static float MarginY => VerticalBox.Margin;
        public static float ContentTop => VerticalBox.Start;
        public static float ContentBottom => VerticalBox.End;
        public static float ContentHeight => VerticalBox.Extent;
        public static float ContentLeft => HorizontalBox.Start;
        public static float ContentWidth => HorizontalBox.Extent;

        /// <summary>세이프에어리어와 마진을 뺀 전체 콘텐츠 영역.</summary>
        public static Rect Content
        {
            get
            {
                SafeBox h = HorizontalBox;
                SafeBox v = VerticalBox;
                return new Rect(h.Start, v.Start, h.Extent, v.Extent);
            }
        }

        /// <summary>패널 높이를 안전 영역 안으로 제한.</summary>
        public static float ClampHeight(float desired) => ClampSize(desired, VerticalBox);

        /// <summary>패널 폭을 안전 영역 안으로 제한.</summary>
        public static float ClampWidth(float desired) => ClampSize(desired, HorizontalBox);

        /// <summary>원하는 높이가 안전 영역을 넘는가 — 스크롤이 필요한지 판단할 때.</summary>
        public static bool Overflows(float desiredHeight) => desiredHeight > VerticalBox.Extent;

        /// <summary>화면 중앙 모달. 폭·높이 모두 안전 영역 안으로 clamp된다.</summary>
        public static Rect CenteredPanel(float width, float height)
        {
            return AnchoredPanel(width, height, HAlign.Center);
        }

        /// <summary>가로 정렬을 지정하는 패널. 세로는 항상 안전 영역 중앙.</summary>
        public static Rect AnchoredPanel(float width, float height, HAlign align)
        {
            SafeBox h = HorizontalBox;
            SafeBox v = VerticalBox;
            float w = ClampSize(width, h);
            float ph = ClampSize(height, v);
            return new Rect(AlignStart(w, align, h), CenterStart(ph, v), w, ph);
        }

        /// <summary>상단 앵커 패널 (세이프에어리어 + 마진 아래에 붙는다).</summary>
        public static Rect TopPanel(float width, float height, HAlign align = HAlign.Center)
        {
            SafeBox h = HorizontalBox;
            SafeBox v = VerticalBox;
            float w = ClampSize(width, h);
            float ph = ClampSize(height, v);
            return new Rect(AlignStart(w, align, h), v.Start, w, ph);
        }

        /// <summary>하단 앵커 패널 (제스처바 + 마진 위에 붙는다).</summary>
        public static Rect BottomPanel(float width, float height, HAlign align = HAlign.Center)
        {
            SafeBox h = HorizontalBox;
            SafeBox v = VerticalBox;
            float w = ClampSize(width, h);
            float ph = ClampSize(height, v);
            return new Rect(AlignStart(w, align, h), EndStart(ph, v), w, ph);
        }

        /// <summary>세로 위치만 필요한 경우(폭을 호출부가 직접 정할 때).</summary>
        public static float CenteredY(float height) => CenterStart(height, VerticalBox);

        /// <summary>하단 앵커 y좌표만 필요한 경우.</summary>
        public static float BottomY(float height) => EndStart(height, VerticalBox);

        // ── 픽셀 좌표계 파사드 (UIScale.Begin을 쓰지 않는 UI 전용) ──

        /// <summary>
        /// GUI.matrix 스케일 없이 실제 픽셀 좌표로 그리는 UI(LoginUI·SettingsPanel·오프닝 등)용.
        /// 계산 규칙은 가상 좌표계와 동일하고 기준만 Screen/SafeArea 픽셀이다.
        /// </summary>
        public static class Px
        {
            public static SafeBox VerticalBox =>
                Compute(Screen.height, SafeArea.Top, SafeArea.Bottom);

            public static SafeBox HorizontalBox =>
                ComputeWithMargin(Screen.width, SafeArea.Left, SafeArea.Right, MarginX);

            public static float MarginY => VerticalBox.Margin;
            public static float ContentTop => VerticalBox.Start;
            public static float ContentBottom => VerticalBox.End;
            public static float ContentHeight => VerticalBox.Extent;
            public static float ContentLeft => HorizontalBox.Start;
            public static float ContentWidth => HorizontalBox.Extent;

            public static Rect Content
            {
                get
                {
                    SafeBox h = HorizontalBox;
                    SafeBox v = VerticalBox;
                    return new Rect(h.Start, v.Start, h.Extent, v.Extent);
                }
            }

            public static float ClampHeight(float desired) => ClampSize(desired, VerticalBox);
            public static float ClampWidth(float desired) => ClampSize(desired, HorizontalBox);
            public static bool Overflows(float desiredHeight) => desiredHeight > VerticalBox.Extent;

            public static Rect CenteredPanel(float width, float height)
            {
                return AnchoredPanel(width, height, HAlign.Center);
            }

            public static Rect AnchoredPanel(float width, float height, HAlign align)
            {
                SafeBox h = HorizontalBox;
                SafeBox v = VerticalBox;
                float w = ClampSize(width, h);
                float ph = ClampSize(height, v);
                return new Rect(AlignStart(w, align, h), CenterStart(ph, v), w, ph);
            }

            public static Rect TopPanel(float width, float height, HAlign align = HAlign.Center)
            {
                SafeBox h = HorizontalBox;
                SafeBox v = VerticalBox;
                float w = ClampSize(width, h);
                float ph = ClampSize(height, v);
                return new Rect(AlignStart(w, align, h), v.Start, w, ph);
            }

            public static Rect BottomPanel(float width, float height, HAlign align = HAlign.Center)
            {
                SafeBox h = HorizontalBox;
                SafeBox v = VerticalBox;
                float w = ClampSize(width, h);
                float ph = ClampSize(height, v);
                return new Rect(AlignStart(w, align, h), EndStart(ph, v), w, ph);
            }

            public static float CenteredY(float height) => CenterStart(height, VerticalBox);
            public static float BottomY(float height) => EndStart(height, VerticalBox);
        }
    }
}
