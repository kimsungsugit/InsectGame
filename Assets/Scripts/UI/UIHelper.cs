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
        }

        // ── 패널 페이드 ──

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
