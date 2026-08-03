using InsectGame.Data;
using UnityEditor;
using UnityEngine;

namespace InsectGame.Editor
{
    /// <summary>
    /// `Assets/Resources/ItemRarityPalette.asset`을 재현 가능하게 생성/갱신합니다.
    ///
    /// 이 애셋은 `.cs` 정의만 있고 인스턴스가 없어서, `PlaySceneBootstrap`의
    /// `Resources.Load&lt;ItemRarityPalette&gt;("ItemRarityPalette")`가 항상 null을 돌려주고 있었다
    /// (소비자들이 전부 하드코딩 폴백으로 동작 — 2026-07-31 audit).
    ///
    /// 색·펄스·두께·글로우·속도 기본값은 <see cref="ItemRarityPalette"/> 필드 초기값을 그대로 쓴다
    /// (SO를 CreateInstance하면 그 값이 들어온다). 여기서 채우는 건 인스펙터 없이는 비어 있는
    /// **Gradient 5개**뿐이다 — 비워 두면 `ItemInventoryGridItem`의 파티클이 검게 나온다.
    /// </summary>
    public static class ItemRarityPaletteBuilder
    {
        private const string PaletteAssetPath = "Assets/Resources/ItemRarityPalette.asset";

        [MenuItem("Insect Game/Data/Build Item Rarity Palette")]
        public static void BuildPalette()
        {
            ItemRarityPalette palette = AssetDatabase.LoadAssetAtPath<ItemRarityPalette>(PaletteAssetPath);
            bool created = palette == null;
            if (created)
            {
                palette = ScriptableObject.CreateInstance<ItemRarityPalette>();
                AssetDatabase.CreateAsset(palette, PaletteAssetPath);
            }

            // 등급색을 기준으로 (어두운 시작 → 등급색 → 밝은 끝) 그라디언트를 만든다.
            // 알파는 파티클 수명에 맞춰 페이드 인/아웃.
            palette.commonGradient = BuildGradient(palette.commonColor);
            palette.uncommonGradient = BuildGradient(palette.uncommonColor);
            palette.rareGradient = BuildGradient(palette.rareColor);
            palette.epicGradient = BuildGradient(palette.epicColor);
            palette.legendaryGradient = BuildGradient(palette.legendaryColor);

            EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ItemRarityPaletteBuilder] {(created ? "생성" : "갱신")}: {PaletteAssetPath}");
        }

        private static Gradient BuildGradient(Color baseColor)
        {
            Color dark = new Color(baseColor.r * 0.45f, baseColor.g * 0.45f, baseColor.b * 0.45f, 1f);
            Color bright = new Color(
                Mathf.Min(1f, baseColor.r * 1.35f + 0.1f),
                Mathf.Min(1f, baseColor.g * 1.35f + 0.1f),
                Mathf.Min(1f, baseColor.b * 1.35f + 0.1f),
                1f);

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(dark, 0f),
                    new GradientColorKey(baseColor, 0.5f),
                    new GradientColorKey(bright, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }
    }
}
