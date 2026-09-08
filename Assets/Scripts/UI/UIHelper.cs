using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public static class UIHelper
    {
        private static readonly Dictionary<Color32, Texture2D> texCache = new Dictionary<Color32, Texture2D>();
        private static readonly Dictionary<string, GUIStyle> styleCache = new Dictionary<string, GUIStyle>();
        private const int MaxCacheSize = 256;

        // ── 텍스처 ──

        /// <summary>
        /// GUIStyle.normal.background 용 1x1 텍스처. **정적인 색에만 쓸 것.**
        /// 펄스/글로우/페이드처럼 연속 변하는 색은 <see cref="DrawTinted"/> 계열을 쓴다 —
        /// 그런 색을 여기 넣으면 프레임마다 새 Color32 키가 쌓여 캐시가 넘친다.
        /// </summary>
        public static Texture2D GetCachedTex(Color col)
        {
            Color32 key = col;
            if (texCache.TryGetValue(key, out Texture2D tex) && tex != null)
                return tex;

            if (texCache.Count >= MaxCacheSize)
            {
                // 여기서 Destroy하지 않는다. UIHelper의 styleCache는 비울 수 있지만,
                // 각 UI가 자기 필드로 들고 있는 GUIStyle(stylesReady 가드로 재생성되지
                // 않음)의 normal.background까지는 무효화할 수 없다. Destroy하면 그 UI는
                // 영구히 파괴된 텍스처를 참조해 배경이 사라진다.
                // 참조만 버리고 Unity 수명주기에 맡긴다. 1x1 RGBA32는 개당 4바이트라
                // 정적 색만 담기는 지금 구조에서 누적량은 무시할 수준이다.
                texCache.Clear();
                styleCache.Clear();
            }

            tex = MakeTex(1, 1, col);
            texCache[key] = tex;
            return tex;
        }

        /// <summary>
        /// 빌트인 흰 텍스처에 GUI.color로 색을 입혀 그린다. 색마다 텍스처를 만들지 않으므로
        /// 동적 색(펄스·글로우·알파 페이드)에 안전하다.
        /// </summary>
        private static void DrawTinted(Rect rect, Color color)
        {
            Color prev = GUI.color;
            // 대입이 아니라 곱셈이다. 호출부가 페이드 등으로 GUI.color를 이미 설정했을 수
            // 있고, 색 텍스처를 그리던 옛 동작도 그 값과 곱해졌다. 대입하면 그 페이드가 사라진다.
            GUI.color = prev * color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        // ── 드로잉 ──

        public static void DrawBorder(Rect rect, Color color, int thickness)
        {
            Color prev = GUI.color;
            GUI.color = prev * color;   // 대입이 아니라 곱셈 — DrawTinted 주석 참조
            Texture2D tex = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), tex);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), tex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), tex);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), tex);
            GUI.color = prev;
        }

        public static void DrawProgressBar(Rect rect, float ratio, Color bgColor, Color fillColor)
        {
            DrawTinted(rect, bgColor);
            if (ratio > 0f)
            {
                Rect fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height);
                DrawTinted(fillRect, fillColor);
            }
        }

        public static void DrawDimOverlay(float alpha = 0.6f)
        {
            // alpha가 호출부마다 달라 캐시 키가 무한히 늘 수 있는 자리다.
            DrawTinted(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, alpha));
        }

        // ── 레어도 시각 ──

        public static void DrawRarityBorder(Rect rect, int rarityTier, float time)
        {
            UITheme theme = UITheme.Instance;
            if (theme == null)
            {
                DrawBorder(rect, Color.gray, 1);
                return;
            }

            Color color = theme.GetRarityColor(rarityTier);
            int thickness;
            float animSpeed;
            float glowIntensity;

            switch (rarityTier)
            {
                case 0: // Common
                    thickness = 1; animSpeed = 0f; glowIntensity = 0f;
                    break;
                case 1: // Uncommon
                    thickness = 1; animSpeed = 0f; glowIntensity = 0f;
                    break;
                case 2: // Rare
                    thickness = 2; animSpeed = 1f; glowIntensity = 0.3f;
                    break;
                case 3: // Epic
                    thickness = 2; animSpeed = 2f; glowIntensity = 0.5f;
                    break;
                default: // Legendary
                    thickness = 3; animSpeed = 3f; glowIntensity = 0.8f;
                    break;
            }

            if (animSpeed > 0f)
            {
                float pulse = 0.7f + 0.3f * Mathf.Sin(time * animSpeed * Mathf.PI * 2f);
                color = new Color(color.r * pulse, color.g * pulse, color.b * pulse, color.a);
            }

            DrawBorder(rect, color, thickness);

            if (glowIntensity > 0f)
            {
                DrawRarityGlow(rect, color, glowIntensity, time);
            }
        }

        public static void DrawRarityGlow(Rect rect, Color color, float intensity, float time)
        {
            float breathe = 0.8f + 0.2f * Mathf.Sin(time * 1.5f);
            float effectiveIntensity = intensity * breathe;

            for (int i = 1; i <= 3; i++)
            {
                float expand = i * 2f;
                float alpha = effectiveIntensity * (0.15f / i);
                Color glowColor = new Color(color.r, color.g, color.b, alpha);
                Rect glowRect = new Rect(
                    rect.x - expand, rect.y - expand,
                    rect.width + expand * 2, rect.height + expand * 2);
                // breathe로 alpha가 매 프레임 변하므로 텍스처 캐시에 넣으면 안 된다.
                DrawTinted(glowRect, glowColor);
            }
        }

        // ── GUIStyle 캐시 ──

        public static GUIStyle CachedStyle(string key, System.Func<GUIStyle> factory)
        {
            if (styleCache.TryGetValue(key, out GUIStyle cached) && cached != null)
                return cached;

            GUIStyle style = factory();
            styleCache[key] = style;
            return style;
        }

        public static void ClearStyleCache()
        {
            styleCache.Clear();
            fitCache.Clear();
        }

        // ── 상자에 맞춰 그리는 텍스트 ──

        /// <summary>측정 전용 스크래치. GUIContent는 class라 매 프레임 new 하면 GC가 돈다.</summary>
        private static readonly GUIContent fitContent = new GUIContent();

        /// <summary>(텍스트, 폭, 높이, 기준 폰트) → 들어가는 폰트 크기. CalcHeight가 공짜가 아니라 캐시한다.</summary>
        private static readonly Dictionary<long, int> fitCache = new Dictionary<long, int>();
        private const int FitCacheMax = 512;

        /// <summary>더 줄이면 읽을 수 없는 하한. 여기서도 넘치면 잘리는 걸 받아들인다.</summary>
        public const int MinReadableFontSize = 18;

        /// <summary>
        /// <paramref name="rect"/> 안에 다 들어가도록 폰트를 줄여서 그린다.
        ///
        /// IMGUI에서 `wordWrap` 스타일을 **고정 높이** Rect에 그리면, 줄바꿈이 일어나는 순간
        /// 넘치는 줄이 통째로 잘린다. 이 저장소엔 그런 자리가 여럿 있었고(도감 설명 144px,
        /// 아이템 설명 40px, 보유 곤충 설명 84px, NPC 대사 88px) 한국어는 같은 뜻을 더 긴
        /// 글자수로 쓰는 데다 모바일에선 기준 폰트가 커져 훨씬 쉽게 넘쳤다.
        ///
        /// <b>`wordWrap`이 꺼져 있으면 가로도 함께 본다.</b> 줄바꿈이 없으면 높이는 폰트 크기와
        /// 무관하게 늘 한 줄이라 세로 검사만으로는 축소가 절대 발동하지 않고, 대신 넘치는 글자가
        /// 가로로 잘린다(가운데 정렬이면 앞뒤가 같이 잘려 더 나쁘다).
        ///
        /// 레이아웃(Rect)은 그대로 두고 **글자만 줄여 맞춘다** — 상자를 키우면 그 아래 요소가
        /// 전부 밀리므로 회귀 범위가 훨씬 커진다. 하한(<see cref="MinReadableFontSize"/>)까지
        /// 줄여도 안 들어가면 거기서 멈춘다.
        ///
        /// 호출부의 <paramref name="style"/>은 대개 공유 캐시라, 폰트 크기를 잠시 바꿔 그린 뒤
        /// 반드시 원복한다.
        /// </summary>
        public static void LabelFit(Rect rect, string text, GUIStyle style, int minFontSize = 0)
        {
            if (style == null || string.IsNullOrEmpty(text))
                return;

            int baseSize = style.fontSize > 0 ? style.fontSize : 12;
            int floor = Mathf.Clamp(
                minFontSize > 0 ? minFontSize : Mathf.Min(baseSize, MinReadableFontSize),
                1,
                baseSize);

            int fitted = FitFontSize(text, rect.width, rect.height, style, baseSize, floor);
            if (fitted == baseSize)
            {
                GUI.Label(rect, text, style);
                return;
            }

            style.fontSize = fitted;
            GUI.Label(rect, text, style);
            style.fontSize = baseSize;
        }

        /// <summary>
        /// 래핑된 <paramref name="text"/>가 <paramref name="width"/>에서 차지하는 높이.
        /// 상자를 키울 수 있는 레이아웃(스크롤 목록 등)에서 쓴다.
        /// </summary>
        public static float MeasureWrappedHeight(GUIStyle style, string text, float width)
        {
            if (style == null || string.IsNullOrEmpty(text) || width <= 0f)
                return 0f;

            fitContent.text = text;
            return style.CalcHeight(fitContent, width);
        }

        private static int FitFontSize(
            string text, float width, float height, GUIStyle style, int baseSize, int floor)
        {
            if (width <= 1f || height <= 1f)
                return baseSize;

            long key = FitKey(text, width, height, baseSize, style);
            if (fitCache.TryGetValue(key, out int cached))
                return cached;

            fitContent.text = text;
            // wordWrap이면 폭은 CalcHeight가 이미 반영한다(줄바꿈으로 흡수). 끄면 줄바꿈이 없어
            // **높이는 폰트 크기와 무관하게 항상 한 줄**이라 세로 검사만으로는 절대 발동하지 않고,
            // 넘치는 글자가 가로로 잘린다. 그 경우 폭도 함께 본다.
            // (RegionMapUI의 Label 헬퍼가 지도 핀을 1줄로 고정하려고 wordWrap을 전역으로 끈 탓에
            //  지역 설명까지 이 경로를 탄다 — 여유가 7%뿐이라 한 줄만 길어지면 잘린다.)
            bool checkWidth = !style.wordWrap;
            int size = baseSize;
            // 한 단계씩 줄인다 — 이분 탐색은 측정값이 폰트 크기에 대해 계단식이라 이득이 적고,
            // 실제로 필요한 감소폭이 몇 포인트라 선형이 오히려 측정 횟수가 적다.
            while (size > floor)
            {
                style.fontSize = size;
                bool fitsHeight = style.CalcHeight(fitContent, width) <= height;
                bool fitsWidth = !checkWidth || style.CalcSize(fitContent).x <= width;
                if (fitsHeight && fitsWidth)
                    break;
                size--;
            }
            style.fontSize = baseSize;

            if (fitCache.Count >= FitCacheMax)
                fitCache.Clear();
            fitCache[key] = size;
            return size;
        }

        /// <summary>
        /// 캐시 키. <b>스타일 자체가 키에 들어가야 한다</b> — 측정은 <c>style.CalcHeight</c>/
        /// <c>CalcSize</c>로 하고 그 결과는 폰트·볼드·<c>wordWrap</c>·패딩에 좌우되는데,
        /// 예전엔 (텍스트, 폭, 높이, 기준폰트)만 해싱했다. 그래서 이 넷이 같은 서로 다른 스타일이
        /// <b>하나의 답을 나눠 썼다</b>. 특히 <c>checkWidth = !style.wordWrap</c>이라 wordWrap이
        /// 다르면 <b>측정 규칙 자체가 다른데도</b> 캐시가 공유된다 — 가로 검사를 아예 하지 않은
        /// 값이 가로로 넘치는 라벨에 그대로 쓰인다. 증상은 조용한 글자 잘림이라,
        /// <c>LabelFit</c>이 막으려던 바로 그 결함이 캐시를 통해 되살아난다.
        ///
        /// 참조 동일성(<c>RuntimeHelpers.GetHashCode</c>)을 쓰는 이유는 측정에 영향을 주는
        /// 속성을 <b>빠짐없이</b> 나열할 자신이 없어서다. 스타일은 화면마다 1회 캐시되는
        /// 싱글턴이라 적중률 손해도 사실상 없고, 재생성되면 옛 항목은 <see cref="FitCacheMax"/>
        /// 초과 시 통째로 비워진다.
        /// </summary>
        // internal — 그리기는 IMGUI 컨텍스트가 필요해 테스트할 수 없지만 이 키 계산은 순수하다.
        // UIHelperFitKeyTests가 "스타일이 다르면 키도 달라야 한다"를 고정한다.
        internal static long FitKey(string text, float width, float height, int baseSize, GUIStyle style)
        {
            // 문자열 해시는 런타임마다 달라도 무방하다 — 이 캐시는 세션 안에서만 산다.
            long h = text.GetHashCode();
            h = h * 31 + Mathf.RoundToInt(width);
            h = h * 31 + Mathf.RoundToInt(height);
            h = h * 31 + baseSize;
            h = h * 31 + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(style);
            return h;
        }

        // ── 패널 페이드 ──

        /// <summary>
        /// 패널 열림/닫힘 알파(0~1). <paramref name="isOpen"/>이 바뀌는 순간을 감지해 트윈을 건다.
        ///
        /// <b>호출부는 두 갈래 중 하나를 반드시 지켜야 한다 — 안 지키면 조용히 페이드가 사라진다.</b>
        /// 전이 감지가 <paramref name="wasOpen"/> 하나에 걸려 있어서다.
        /// <list type="number">
        ///   <item>닫힌 프레임에도 <b>매 프레임 이걸 먼저 부르고</b> 알파가 0에 닿은 뒤에 그리기를
        ///     멈춘다(<c>CharacterOutfitUI</c> 방식 — 페이드아웃까지 보인다).</item>
        ///   <item>닫히면 곧바로 그리기를 멈추는 화면이라면(그래서 이 함수가 안 불린다),
        ///     닫는 자리에서 <c>wasOpen = false</c>로 되돌린다. 안 그러면 <c>wasOpen</c>이 true로
        ///     굳어 <b>두 번째 열림부터 전이가 감지되지 않는다</b> — 첫 열림에만 페이드가 걸리고
        ///     그 뒤로는 툭 튀어나온다(<c>NpcDialogueUI</c>가 이 갈래다).</item>
        /// </list>
        /// </summary>
        public static float AnimatePanelOpen(ref TweenHandle handle, bool isOpen, ref bool wasOpen)
        {
            if (isOpen != wasOpen)
            {
                handle = UITween.Create(isOpen ? 0f : 1f, isOpen ? 1f : 0f, isOpen ? 0.2f : 0.15f, EaseType.SmoothStep);
                wasOpen = isOpen;
            }

            if (handle.active)
                return UITween.Evaluate(ref handle);

            return isOpen ? 1f : 0f;
        }

        // ── 버튼 스타일 ──

        public enum ButtonType { Primary, Secondary, Danger, Disabled }

        public static GUIStyle GetButtonStyle(ButtonType type)
        {
            string key = "btn_" + type;
            return CachedStyle(key, () =>
            {
                UITheme theme = UITheme.Instance;
                Color bgColor;
                switch (type)
                {
                    case ButtonType.Secondary: bgColor = theme != null ? theme.btnSecondary : new Color(0.25f, 0.3f, 0.45f); break;
                    case ButtonType.Danger: bgColor = theme != null ? theme.btnDanger : new Color(0.7f, 0.15f, 0.15f); break;
                    case ButtonType.Disabled: bgColor = theme != null ? theme.btnDisabled : new Color(0.25f, 0.25f, 0.25f); break;
                    default: bgColor = theme != null ? theme.btnPrimary : new Color(0.2f, 0.5f, 0.2f); break;
                }

                GUIStyle style = new GUIStyle(GUI.skin.button);
                style.normal.background = GetCachedTex(bgColor);
                style.normal.textColor = Color.white;
                style.fontSize = 12;
                style.fontStyle = FontStyle.Bold;
                style.alignment = TextAnchor.MiddleCenter;
                return style;
            });
        }
    }
}
