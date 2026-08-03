using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 둥근 모서리 · 그림자 · 호버가 있는 공통 IMGUI 서피스.
    ///
    /// 원래 <c>DexScreenUI</c>의 private 메서드였다(DrawRoundedRect/DrawRoundedCard/
    /// DrawCuteButton/DrawScrollAffordance). 제대로 만들어진 서피스인데 도감 한 화면에만
    /// 갇혀 있어서, 나머지 30개 화면은 전부 각진 <c>GUI.DrawTexture(rect, whiteTexture)</c>였다.
    /// 여기로 승격해 전 화면이 같은 표면을 쓴다.
    ///
    /// 색은 직접 정하지 않는다 — <see cref="UITheme"/> 토큰을 받아 그린다.
    /// 배치(y·height)도 정하지 않는다 — 그건 <see cref="UISafeLayout"/>의 몫이다.
    /// 여기가 정하는 건 "그 Rect를 어떤 질감으로 칠하는가"뿐이다.
    /// </summary>
    public static class UISurface
    {
        // 반경별·색별 둥근 텍스처 캐시. 키는 (RGBA 32비트 << 8 | 반경).
        private static readonly Dictionary<long, GUIStyle> roundedStyles = new Dictionary<long, GUIStyle>();
        private const int MaxCacheSize = 256;

        // ── 둥근 텍스처 ──

        private static long MakeKey(Color color, float radius)
        {
            Color32 c = color;
            long packed = ((long)c.r << 24) | ((long)c.g << 16) | ((long)c.b << 8) | c.a;
            return (packed << 8) | (byte)Mathf.Clamp(Mathf.RoundToInt(radius), 0, 255);
        }

        /// <summary>
        /// 지정 색·반경의 9-slice 둥근 배경 스타일. 색마다 텍스처를 굽지만 토큰화된 팔레트라
        /// 종류가 유한하다(실측 100~150개).
        ///
        /// <b>private인 것이 계약이다.</b> 반환된 GUIStyle을 아무도 보관하지 않아야
        /// <see cref="EvictAll"/>이 텍스처를 파괴할 수 있다. public으로 열면
        /// 어느 화면이 이걸 필드에 캐시하는 순간 파괴된 텍스처를 참조해 배경이 사라진다
        /// (<see cref="UIHelper.GetCachedTex"/>가 Destroy를 못 하는 이유가 바로 그것이다).
        /// </summary>
        private static GUIStyle GetRoundedStyle(Color color, float radius = UITheme.Radius.Card)
        {
            long key = MakeKey(color, radius);
            if (roundedStyles.TryGetValue(key, out GUIStyle cached) && cached != null)
            {
                return cached;
            }

            if (roundedStyles.Count >= MaxCacheSize)
            {
                EvictAll();
            }

            int size = Mathf.Max(16, Mathf.CeilToInt(radius * 4f));
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "UISurfaceRounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float dx = Mathf.Abs(px + 0.5f - half) - (half - radius);
                    float dy = Mathf.Abs(py + 0.5f - half) - (half - radius);
                    float outside = Mathf.Sqrt(
                        Mathf.Max(0f, dx) * Mathf.Max(0f, dx)
                        + Mathf.Max(0f, dy) * Mathf.Max(0f, dy)) - radius;
                    float alpha = Mathf.Clamp01(0.75f - outside);
                    pixels[py * size + px] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);

            int border = Mathf.CeilToInt(radius) + 4;
            GUIStyle style = new GUIStyle
            {
                border = new RectOffset(border, border, border, border)
            };
            style.normal.background = texture;
            roundedStyles[key] = style;
            return style;
        }

        /// <summary>
        /// 상한 초과 시 전량 폐기 — <b>텍스처까지 파괴한다.</b>
        ///
        /// HideAndDontSave는 씬 전환에도 살아남고 GC 대상도 아니라, 참조만 버리면
        /// 사이클마다 최대 ~2MB(48×48 RGBA32 × 256)가 영구히 샌다. 여기 스타일은
        /// <see cref="GetRoundedStyle"/>이 private이라 그리는 순간에만 쓰이고 아무도
        /// 보관하지 않으므로 파괴가 안전하다. 그리기는 OnGUI 안에서 동기로 끝나서
        /// 이번 프레임에 이미 그린 것에 소급 영향도 없다.
        /// </summary>
        private static void EvictAll()
        {
            foreach (KeyValuePair<long, GUIStyle> pair in roundedStyles)
            {
                GUIStyle style = pair.Value;
                Texture2D texture = style != null && style.normal != null
                    ? style.normal.background
                    : null;
                if (texture != null) Object.Destroy(texture);
            }
            roundedStyles.Clear();
        }

        // ── 기본 도형 ──

        /// <summary>
        /// 단색 둥근 사각형.
        ///
        /// <b>알파는 호출부 것을 살리고 RGB만 흰색으로 고정한다.</b> 텍스처가 이미 색을
        /// 굽고 있으므로 RGB까지 곱하면 이중 착색이 되고(그래서 <see cref="UIHelper.DrawTinted"/>의
        /// 전면 곱셈과 다르다 — 그쪽은 흰 텍스처를 그린다), 반대로 흰색을 통째로 대입하면
        /// 패널 페이드가 배경만 빼놓고 진행돼 배경이 툭 튀어나온다.
        /// </summary>
        public static void Rounded(Rect rect, Color color, float radius = UITheme.Radius.Card)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, previous.a);
            GUI.Box(rect, GUIContent.none, GetRoundedStyle(color, radius));
            GUI.color = previous;
        }

        /// <summary>
        /// 구분선·진행바·액센트 스트라이프처럼 <b>얇은</b>(대략 8px 이하) 요소용 각진 채움.
        /// 그런 크기에 둥근 9-slice를 쓰면 테두리 폭이 높이를 넘겨 뭉개진다
        /// (`rules/ui-layout.md`의 "얇은 것은 각진 채로 둔다").
        /// 둥근 패널 위에 얹을 때는 x를 반경만큼 물려 모서리를 뚫지 않게 할 것.
        /// </summary>
        public static void Flat(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = previous * color;   // 흰 텍스처를 그리므로 전면 곱셈이 맞다
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>그림자 + 테두리 + 본체 3겹으로 그리는 카드.</summary>
        public static void Card(Rect rect, Color background, Color border, float radius = UITheme.Radius.Card)
        {
            Rounded(new Rect(rect.x + 4f, rect.y + 6f, rect.width, rect.height), Shadow, radius);
            Rounded(rect, border, radius);
            Rounded(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f), background, radius);
        }

        /// <summary>테마 기본 카드 — 표면색 + 테두리색을 토큰에서 가져온다.</summary>
        public static void Card(Rect rect)
        {
            UITheme t = UITheme.Instance;
            Card(rect, t.surfaceCard, t.surfaceBorder);
        }

        /// <summary>
        /// 그림자 + 호버 밝기가 있는 버튼. 라벨은 <paramref name="style"/>로 그리고
        /// 클릭 판정은 투명 <c>GUI.Button</c>이 받는다.
        /// </summary>
        public static bool Button(Rect rect, string label, Color background, GUIStyle style, bool selected = false)
        {
            Color body = selected ? Color.Lerp(background, Color.white, 0.32f) : background;
            if (rect.Contains(UIScale.VirtualMousePosition))
            {
                body = Color.Lerp(body, Color.white, 0.16f);
            }

            Rounded(new Rect(rect.x + 2f, rect.y + 3f, rect.width, rect.height), Shadow);
            Rounded(rect, body);
            GUI.Label(rect, label, style);
            return GUI.Button(rect, string.Empty, GUIStyle.none);
        }

        /// <summary>등급·속성·보상 등을 담는 작은 알약 배지.</summary>
        public static void Chip(Rect rect, string text, Color background, Color textColor)
        {
            Rounded(rect, background, UITheme.Radius.Chip);
            GUIStyle style = TextStyle(SurfaceText.Chip, Mathf.RoundToInt(rect.height * 0.52f));
            style.normal.textColor = textColor;
            GUI.Label(rect, text, style);
        }

        /// <summary>라벨(좌) · 값(우) 한 줄. 도감 상세와 스탯 표에 공용.</summary>
        public static void StatRow(Rect rect, string label, string value, Color labelColor, Color valueColor)
        {
            int fontSize = Mathf.RoundToInt(rect.height * 0.62f);
            GUIStyle ls = TextStyle(SurfaceText.StatLabel, fontSize);
            GUIStyle vs = TextStyle(SurfaceText.StatValue, fontSize);
            ls.normal.textColor = labelColor;
            vs.normal.textColor = valueColor;
            GUI.Label(new Rect(rect.x, rect.y, rect.width * 0.5f, rect.height), label, ls);
            GUI.Label(new Rect(rect.center.x, rect.y, rect.width * 0.5f, rect.height), value, vs);
        }

        /// <summary>
        /// 화면 전체 딤. <b>가상 좌표계 전용</b> — <c>UIScale.Begin()</c> 안에서 부른다.
        /// 픽셀 좌표계라면 <see cref="UIHelper.DrawDimOverlay"/>를 쓸 것.
        /// </summary>
        public static void Dim(float alpha = 0.72f)
        {
            Color prev = GUI.color;
            GUI.color = prev * new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(
                new Rect(0f, 0f, UIScale.VirtualScreenWidth, UIScale.VirtualScreenHeight),
                Texture2D.whiteTexture);
            GUI.color = prev;
        }

        // ── 합성 컴포넌트 ──

        /// <summary>
        /// 액센트 헤더 바 — 타이틀(좌) · 부제(타이틀 옆) · 닫기(우).
        /// 반환값은 닫기 버튼이 눌렸는지 여부.
        /// </summary>
        public static bool Header(Rect rect, string title, string subtitle, string closeLabel = "× 닫기")
        {
            UITheme t = UITheme.Instance;
            Rounded(rect, t.accentCoral);

            int titleSize = Mathf.RoundToInt(rect.height * 0.34f);
            GUIStyle titleStyle = TextStyle(SurfaceText.HeaderTitle, titleSize);
            GUIStyle subStyle = TextStyle(SurfaceText.HeaderSubtitle, Mathf.RoundToInt(titleSize * 0.62f));

            float pad = UITheme.Space.L;
            float closeW = Mathf.Min(200f, rect.width * 0.24f);
            float closeH = Mathf.Max(UIScale.MinTouchHeight, rect.height * 0.5f);
            float titleW = Mathf.Max(120f, rect.width * 0.42f);

            GUI.Label(new Rect(rect.x + pad, rect.y + rect.height * 0.16f, titleW, rect.height * 0.5f),
                title, titleStyle);

            if (!string.IsNullOrEmpty(subtitle))
            {
                float subX = rect.x + pad + titleW + UITheme.Space.M;
                float subW = Mathf.Max(80f, rect.xMax - closeW - pad * 2f - subX);
                GUI.Label(new Rect(subX, rect.y + rect.height * 0.22f, subW, rect.height * 0.42f),
                    subtitle, subStyle);
            }

            bool closed = Button(
                new Rect(rect.xMax - closeW - pad * 0.5f, rect.y + (rect.height - closeH) * 0.5f, closeW, closeH),
                closeLabel,
                Color.Lerp(t.accentCoral, Color.black, 0.34f),
                TextStyle(SurfaceText.Close, Mathf.RoundToInt(closeH * 0.42f)));

            return closed;
        }

        /// <summary>
        /// 스크롤 위치를 알려주는 얇은 트랙+썸. 내용이 뷰포트를 넘지 않으면 아무것도 그리지 않는다.
        /// </summary>
        public static void ScrollAffordance(Rect viewport, Vector2 scroll, float contentHeight, Color accent)
        {
            float maxScroll = Mathf.Max(0f, contentHeight - viewport.height);
            if (maxScroll <= 1f)
            {
                return;
            }

            // 트랙 5px·썸 7px — 반경 3이어도 9-slice 테두리(3+4=7)가 폭을 넘겨 뭉개진다.
            // 이 파일이 규칙의 본보기여야 하므로 여기서도 Flat을 쓴다.
            Rect track = new Rect(viewport.xMax - 8f, viewport.y + 24f, 5f, viewport.height - 48f);
            Flat(track, new Color(1f, 1f, 1f, 0.10f));

            float visibleRatio = Mathf.Clamp01(viewport.height / contentHeight);
            float thumbHeight = Mathf.Max(42f, track.height * visibleRatio);
            float travel = Mathf.Max(0f, track.height - thumbHeight);
            float thumbY = track.y + travel * Mathf.Clamp01(scroll.y / maxScroll);
            Flat(new Rect(track.x - 1f, thumbY, 7f, thumbHeight), accent);
        }

        // ── 내부 스타일 캐시 ──
        // 폰트 크기가 Rect 높이에서 파생되므로 크기별로 캐시한다.
        //
        // UIHelper.CachedStyle을 쓰지 않는 이유: 그 API는 `CachedStyle("outfit_big_title", () => …)`
        // 처럼 **키가 상수이고 람다가 아무것도 캡처하지 않을 때만** 공짜다(컴파일러가 델리게이트를
        // 정적으로 재사용한다). 여기서는 키가 폰트 크기에 따라 달라져서 호출마다
        // 문자열 결합 1 + 클로저 1 + 델리게이트 1 = 3개를 할당하게 된다 — 캐시 적중이어도 그렇다.
        // 목록에 칩·스탯 행이 수십 개 깔리는 화면에서는 그대로 프레임 할당이 된다.
        // int 키 딕셔너리는 박싱이 없어 적중 경로가 완전히 무할당이다.

        private enum SurfaceText
        {
            Chip,
            StatLabel,
            StatValue,
            HeaderTitle,
            HeaderSubtitle,
            Close
        }

        private static readonly Dictionary<int, GUIStyle> textStyles = new Dictionary<int, GUIStyle>();

        private static Color Shadow => UITheme.Instance.surfaceShadow;

        private static GUIStyle TextStyle(SurfaceText kind, int fontSize)
        {
            // Rect 높이에서 파생되므로 안전 영역이 극단적으로 좁으면 0이나 음수가 들어온다
            // (ui-layout.md의 "패널 높이 파생값은 Max로 감싼다"와 같은 이유).
            int size = Mathf.Clamp(fontSize, 1, 400);
            int key = ((int)kind << 16) | size;
            if (textStyles.TryGetValue(key, out GUIStyle cached) && cached != null)
            {
                return cached;
            }

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = kind == SurfaceText.StatLabel ? FontStyle.Normal : FontStyle.Bold
            };

            switch (kind)
            {
                case SurfaceText.Chip:
                    style.alignment = TextAnchor.MiddleCenter;
                    style.clipping = TextClipping.Clip;
                    break;
                case SurfaceText.StatLabel:
                    style.alignment = TextAnchor.MiddleLeft;
                    break;
                case SurfaceText.StatValue:
                    style.alignment = TextAnchor.MiddleRight;
                    break;
                case SurfaceText.HeaderTitle:
                    style.alignment = TextAnchor.MiddleLeft;
                    style.normal.textColor = Color.white;
                    break;
                case SurfaceText.HeaderSubtitle:
                    style.alignment = TextAnchor.MiddleLeft;
                    style.normal.textColor = new Color(1f, 0.96f, 0.9f);
                    break;
                default:
                    style.alignment = TextAnchor.MiddleCenter;
                    style.normal.textColor = Color.white;
                    break;
            }

            textStyles[key] = style;
            return style;
        }
    }
}
