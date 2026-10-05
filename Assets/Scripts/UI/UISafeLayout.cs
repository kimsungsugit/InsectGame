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

    /// <summary>
    /// 화면 한 장의 배치 기준 — <b>실제 화면이든 테스트가 세운 화면이든 같은 식으로</b> HUD 자리를 계산하게 한다.
    /// 필드 HUD의 순수 배치 함수가 이걸 받는다. 그리기는 <see cref="Current"/>(Screen·SafeArea·<see cref="UIScale"/>)를 넘기고,
    /// 전수 겹침 검사(<c>HudOverlapSweepTests</c>)는 해상도·스케일·노치별로 <see cref="ForScreen"/>을 세워 넘긴다 —
    /// 식을 테스트에 베껴 두면 원본이 바뀔 때 테스트가 거짓으로 통과하므로, 계산은 언제나 HUD 쪽 함수를 부른다.
    /// 가상 좌표·스케일·모바일 판정은 <see cref="UIScale"/>과 같은 식이다(<c>UISafeLayoutTests</c>가 맞춰 본다).
    /// 화면 모양 말고 하나 더 — 섬 HUD가 서 있는지(<see cref="IslandHud"/>)를 든다. 가운데 무대(<c>HudStage</c>)의 자리가 그걸로 갈린다.
    /// </summary>
    public readonly struct HudFrame
    {
        /// <summary>실제 화면 픽셀.</summary>
        public readonly float PixelWidth;
        public readonly float PixelHeight;
        /// <summary>세이프 에어리어 인셋(픽셀, GUI 기준 — 위는 노치, 아래는 제스처 바).</summary>
        public readonly float PixelSafeLeft;
        public readonly float PixelSafeRight;
        public readonly float PixelSafeTop;
        public readonly float PixelSafeBottom;
        /// <summary><see cref="UIScale.Scale"/>과 같은 값.</summary>
        public readonly float Scale;
        /// <summary>가상 화면(<see cref="UIScale.VirtualScreenWidth"/>·Height).</summary>
        public readonly float Width;
        public readonly float Height;
        /// <summary><see cref="UIScale.IsMobileLayout"/>과 같은 값.</summary>
        public readonly bool Mobile;
        /// <summary><see cref="UIScale.IsPortrait"/>와 같은 값.</summary>
        public readonly bool Portrait;
        /// <summary>
        /// 섬 HUD 판(<c>IslandHudUI</c> — 내 섬·남의 섬)이 이 화면에 서 있는가. 가운데 무대(<c>HudStage.Area</c>)가 이때만 섬 HUD를 피한다 —
        /// 필드에서도 피하면 세로 화면의 카드가 화면 왼쪽 절반으로 밀린다(2026-10-03 QA 실측).
        /// <see cref="Current"/>는 <c>HudPresence</c>(섬 HUD가 매 프레임 표시한다)에서 읽고, <see cref="ForScreen"/>은 false(필드)다 —
        /// 섬 화면은 <see cref="WithIslandHud"/>로 세운다.
        /// </summary>
        public readonly bool IslandHud;

        private HudFrame(float pixelWidth, float pixelHeight, float safeLeft, float safeRight, float safeTop, float safeBottom,
            bool mobile, bool islandHud)
        {
            IslandHud = islandHud;
            PixelWidth = Mathf.Max(1f, pixelWidth);
            PixelHeight = Mathf.Max(1f, pixelHeight);
            PixelSafeLeft = Mathf.Max(0f, safeLeft);
            PixelSafeRight = Mathf.Max(0f, safeRight);
            PixelSafeTop = Mathf.Max(0f, safeTop);
            PixelSafeBottom = Mathf.Max(0f, safeBottom);
            Portrait = PixelHeight > PixelWidth;
            float refW = Portrait ? UIScale.PortraitReferenceWidth : UIScale.ReferenceWidth;
            float refH = Portrait ? UIScale.PortraitReferenceHeight : UIScale.ReferenceHeight;
            Scale = Mathf.Max(0.3f, Mathf.Min(PixelWidth / refW, PixelHeight / refH));
            Width = PixelWidth / Scale;
            Height = PixelHeight / Scale;
            Mobile = mobile;
        }

        /// <summary>
        /// 화면을 세운다. <paramref name="mobilePlatform"/>은 <c>Application.isMobilePlatform</c> — 세로로 긴 창은 그것과 무관하게
        /// 모바일 배치다(<see cref="UIScale.IsMobileLayout"/>과 같은 판정).
        /// </summary>
        public static HudFrame ForScreen(float pixelWidth, float pixelHeight, float safeLeft, float safeRight, float safeTop,
            float safeBottom, bool mobilePlatform)
        {
            bool mobile = mobilePlatform || pixelHeight > pixelWidth * 1.08f;
            return new HudFrame(pixelWidth, pixelHeight, safeLeft, safeRight, safeTop, safeBottom, mobile, false);
        }

        /// <summary>지금 화면(섬 HUD가 서 있는지는 <c>HudPresence</c>에서).</summary>
        public static HudFrame Current => new HudFrame(Screen.width, Screen.height, SafeArea.Left, SafeArea.Right,
            SafeArea.Top, SafeArea.Bottom, UIScale.IsMobileLayout, HudPresence.IsShowing(HudPresenceItem.IslandHud));

        /// <summary>같은 화면에서 섬 HUD가 서 있거나(<paramref name="on"/>) 없는 판.</summary>
        public HudFrame WithIslandHud(bool on) => new HudFrame(PixelWidth, PixelHeight, PixelSafeLeft, PixelSafeRight,
            PixelSafeTop, PixelSafeBottom, Mobile, on);

        // ── 가상 좌표 ──

        public float SafeLeft => PixelSafeLeft / Scale;
        public float SafeRight => PixelSafeRight / Scale;
        public float SafeTop => PixelSafeTop / Scale;
        public float SafeBottom => PixelSafeBottom / Scale;

        public UISafeLayout.SafeBox Horizontal =>
            UISafeLayout.ComputeWithMargin(Width, SafeLeft, SafeRight, UISafeLayout.MarginX);
        public UISafeLayout.SafeBox Vertical => UISafeLayout.Compute(Height, SafeTop, SafeBottom);

        public float ContentLeft => Horizontal.Start;
        public float ContentRight => Horizontal.End;
        public float ContentWidth => Horizontal.Extent;
        public float ContentTop => Vertical.Start;
        public float ContentBottom => Vertical.End;
        public float ContentHeight => Vertical.Extent;

        public float ClampWidth(float desired) => UISafeLayout.ClampSize(desired, Horizontal);
        public float ClampHeight(float desired) => UISafeLayout.ClampSize(desired, Vertical);

        /// <summary><c>UISafeLayout.TopPanel</c>과 같은 값.</summary>
        public Rect TopPanel(float width, float height, UISafeLayout.HAlign align = UISafeLayout.HAlign.Center)
        {
            UISafeLayout.SafeBox h = Horizontal;
            UISafeLayout.SafeBox v = Vertical;
            float w = UISafeLayout.ClampSize(width, h);
            return new Rect(UISafeLayout.AlignStart(w, align, h), v.Start, w, UISafeLayout.ClampSize(height, v));
        }

        /// <summary><c>UISafeLayout.BottomPanel</c>과 같은 값.</summary>
        public Rect BottomPanel(float width, float height, UISafeLayout.HAlign align = UISafeLayout.HAlign.Center)
        {
            UISafeLayout.SafeBox h = Horizontal;
            UISafeLayout.SafeBox v = Vertical;
            float w = UISafeLayout.ClampSize(width, h);
            float ph = UISafeLayout.ClampSize(height, v);
            return new Rect(UISafeLayout.AlignStart(w, align, h), UISafeLayout.EndStart(ph, v), w, ph);
        }

        /// <summary><c>UISafeLayout.CenteredY</c>와 같은 값.</summary>
        public float CenteredY(float height) => UISafeLayout.CenterStart(height, Vertical);

        // ── 픽셀 좌표(UIScale.Begin을 쓰지 않는 화면) ──

        public UISafeLayout.SafeBox PixelHorizontal =>
            UISafeLayout.ComputeWithMargin(PixelWidth, PixelSafeLeft, PixelSafeRight, UISafeLayout.MarginX);
        public UISafeLayout.SafeBox PixelVertical => UISafeLayout.Compute(PixelHeight, PixelSafeTop, PixelSafeBottom);

        /// <summary>가상 → 픽셀.</summary>
        public Rect ToPixels(Rect virtualRect) =>
            new Rect(virtualRect.x * Scale, virtualRect.y * Scale, virtualRect.width * Scale, virtualRect.height * Scale);

        /// <summary>픽셀 → 가상(<c>FieldHudInput.RegisterBlockingRect</c>가 받는 좌표).</summary>
        public Rect ToVirtual(Rect pixelRect) =>
            new Rect(pixelRect.x / Scale, pixelRect.y / Scale, pixelRect.width / Scale, pixelRect.height / Scale);
    }
}
