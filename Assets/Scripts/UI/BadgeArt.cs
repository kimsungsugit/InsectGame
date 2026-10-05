using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 수문장 배지 그림 — <c>Resources/UI/Badges/</c>의 PNG를 한 번 읽어 캐시하고 그린다.
    /// 획득 연출(<see cref="BadgeCeremonyUI"/>)과 배지 케이스(<see cref="BadgeCaseUI"/>)가 함께 쓴다.
    ///
    /// 그림은 <c>Tools/Badges/guardian_badges.py</c>가 굽는다(금속 테두리·법랑·양각 문양을 4배 슈퍼샘플로).
    /// IMGUI 도형으로 그려 보는 것보다 훨씬 낫다 — 곡면 조명과 양각은 사각형·원판 조합으로 흉내가 안 난다.
    ///
    /// 그림이 없으면(빌드에서 빠짐 등) 원판으로 대신 그려 <b>빈칸이 되지 않게</b> 한다 — 예외도 경고도 없이
    /// 사라지면 "배지를 얻었는데 아무것도 안 보인다"가 된다.
    /// </summary>
    public static class BadgeArt
    {
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private static Texture2D rays;
        private static Texture2D glow;
        private static Texture2D sparkle;
        private static bool fxLoaded;

        public static Texture2D Get(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return null;
            if (cache.TryGetValue(regionId, out Texture2D tex)) return tex;
            tex = Resources.Load<Texture2D>(GuardianBadges.ArtPath(regionId));
            if (tex == null) Debug.LogWarning($"[Badge] 배지 그림 없음: {GuardianBadges.ArtPath(regionId)}");
            cache[regionId] = tex;   // null도 캐시한다 — 매 프레임 Resources.Load를 두드리지 않게
            return tex;
        }

        public static Texture2D Rays { get { EnsureFx(); return rays; } }
        public static Texture2D Glow { get { EnsureFx(); return glow; } }
        public static Texture2D Sparkle { get { EnsureFx(); return sparkle; } }

        private static void EnsureFx()
        {
            if (fxLoaded) return;
            fxLoaded = true;
            rays = Resources.Load<Texture2D>(GuardianBadges.ArtFolder + "badge_rays");
            glow = Resources.Load<Texture2D>(GuardianBadges.ArtFolder + "badge_glow");
            sparkle = Resources.Load<Texture2D>(GuardianBadges.ArtFolder + "badge_sparkle");
        }

        /// <summary>
        /// 아직 못 얻은 배지의 틴트 — 모양과 문양의 윤곽만 비친다("저 자리에 무언가 있다").
        /// 색을 새로 박지 않고 테마 테두리색에서 파생한다(<c>rules/ui-layout.md</c>).
        /// </summary>
        public static Color LockedTint
        {
            get
            {
                Color b = UITheme.Instance.surfaceBorder;
                return new Color(b.r * 0.42f, b.g * 0.42f, b.b * 0.44f, 0.92f);
            }
        }

        /// <summary>배지를 그린다. <paramref name="earned"/>가 아니면 어둡게.</summary>
        public static void Draw(Rect rect, string regionId, bool earned, float alpha = 1f)
        {
            Texture2D tex = Get(regionId);
            Color prev = GUI.color;
            Color tint = earned ? Color.white : LockedTint;
            GUI.color = new Color(tint.r * prev.r, tint.g * prev.g, tint.b * prev.b, tint.a * prev.a * alpha);
            if (tex != null)
            {
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
            }
            else
            {
                float inset = rect.width * 0.1f;
                UIShapes.Ellipse(new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f),
                    GUI.color * UITheme.Instance.accentAmber);
            }
            GUI.color = prev;
        }

        /// <summary>
        /// 가운데를 축으로 돌려 그린다. <c>GUIUtility.RotateAroundPivot</c>를 쓰지 않는 이유:
        /// 그건 피벗을 <b>행렬 적용 전</b> 좌표로 받아 회전을 <b>행렬 뒤</b>에 곱하므로, <c>UIScale</c>의
        /// 배율이 1이 아닌 화면에서 피벗이 어긋나 빛살이 배지 둘레를 떠돈다. 피벗을 화면 좌표로 옮겨 곱한다.
        /// </summary>
        public static void DrawRotated(Rect rect, Texture tex, float degrees, Color color)
        {
            if (tex == null) return;
            Matrix4x4 saved = GUI.matrix;
            Vector3 pivot = saved.MultiplyPoint3x4(new Vector3(rect.center.x, rect.center.y, 0f));
            GUI.matrix = Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, degrees), Vector3.one)
                         * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one)
                         * saved;
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, true);
            GUI.color = prev;
            GUI.matrix = saved;
        }

        /// <summary>텍스처를 가운데 정렬로 그린다(빛·반짝임 — 흰 그림에 색을 곱한다).</summary>
        public static void DrawCentered(Texture tex, Vector2 center, float size, Color color)
        {
            if (tex == null || size <= 0f) return;
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), tex,
                ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }
    }
}
