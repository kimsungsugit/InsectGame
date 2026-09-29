using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 리전 바닥 색의 단일 출처 — 바닥 평면(<c>PlaySceneBootstrap</c>)과 그 위의 얼룩·디테일
    /// (<see cref="RegionDressingBuilder"/>)이 같은 표를 읽는다.
    ///
    /// <b>옛 바닥색은 공식이었다</b>(<c>themeColor × 0.5 + 0.1</c>). 테마색은 지도·표지판용 강조색이라
    /// 그걸 어둡게 눌러 바닥에 깔면 서릿길은 회녹색(눈이 아니다), 모래언덕은 탁한 겨자색, 꽃밭은 적갈색,
    /// 연못은 리전 전체가 파란 "물"이 되어 플레이어가 물 위를 걸었다. 그래서 리전마다 손으로 고른다.
    ///
    /// 값은 감마 공간 반사율이다. 햇빛(1.2 × NdotL≈0.77) + Trilight 환경광을 합친 조명이 약 1.3배라
    /// 화면에 나오는 색은 여기 적은 값의 1.3배쯤이다 — 눈을 0.9 넘게 적으면 하얗게 날아간다(실제로 그랬다).
    ///
    /// 여기 없는 리전은 옛 공식으로 떨어진다(<see cref="Ground"/>) — 리전을 늘리면 이 표도 함께 늘린다
    /// (<c>FieldThemeTests.RegionPalette_CoversEveryRegion</c>이 전 리전 등록을 고정한다).
    /// </summary>
    public static class RegionPalette
    {
        public static bool Has(string regionId)
        {
            return TryGet(regionId, out _, out _, out _);
        }

        /// <summary>바닥 평면 색. 미등록 리전은 테마색 공식.</summary>
        public static Color Ground(string regionId, Color themeColor)
        {
            if (TryGet(regionId, out Color ground, out _, out _)) return ground;
            return new Color(themeColor.r * 0.5f + 0.1f, themeColor.g * 0.5f + 0.1f, themeColor.b * 0.4f + 0.08f);
        }

        /// <summary>바닥 위 얼룩 두 톤(밝은·짙은). 단색 판 느낌을 깨는 용도라 바닥색에서 크게 벗어나지 않는다.</summary>
        public static void PatchTones(string regionId, Color themeColor, out Color light, out Color dark)
        {
            if (TryGet(regionId, out Color ground, out light, out dark)) return;
            ground = Ground(regionId, themeColor);
            light = Color.Lerp(ground, Color.white, 0.12f);
            dark = Color.Lerp(ground, Color.black, 0.14f);
        }

        private static bool TryGet(string regionId, out Color ground, out Color light, out Color dark)
        {
            switch (regionId)
            {
                case "meadow":    ground = C(0.29f, 0.45f, 0.21f); light = C(0.35f, 0.51f, 0.24f); dark = C(0.25f, 0.39f, 0.18f); return true;
                // 연못 — 물가 풀밭. 물은 호수·여울 원반이 따로 칠한다
                case "pond":      ground = C(0.28f, 0.44f, 0.24f); light = C(0.33f, 0.49f, 0.26f); dark = C(0.33f, 0.34f, 0.24f); return true;
                case "forest":    ground = C(0.21f, 0.33f, 0.16f); light = C(0.27f, 0.39f, 0.18f); dark = C(0.24f, 0.24f, 0.15f); return true;
                case "swamp":     ground = C(0.24f, 0.28f, 0.17f); light = C(0.29f, 0.33f, 0.19f); dark = C(0.18f, 0.20f, 0.14f); return true;
                case "mountain":  ground = C(0.40f, 0.38f, 0.32f); light = C(0.46f, 0.44f, 0.38f); dark = C(0.33f, 0.37f, 0.25f); return true;
                // 꽃밭 — 분홍은 꽃이 낸다. 바닥은 잘 가꾼 밝은 풀
                case "garden":    ground = C(0.30f, 0.47f, 0.22f); light = C(0.36f, 0.53f, 0.25f); dark = C(0.26f, 0.40f, 0.19f); return true;
                case "ruins":     ground = C(0.44f, 0.40f, 0.31f); light = C(0.50f, 0.46f, 0.37f); dark = C(0.34f, 0.37f, 0.24f); return true;
                case "hollow":    ground = C(0.50f, 0.48f, 0.39f); light = C(0.56f, 0.54f, 0.45f); dark = C(0.43f, 0.41f, 0.33f); return true;
                case "dunes":     ground = C(0.66f, 0.57f, 0.39f); light = C(0.72f, 0.63f, 0.44f); dark = C(0.59f, 0.50f, 0.34f); return true;
                case "frostline": ground = C(0.66f, 0.70f, 0.76f); light = C(0.74f, 0.77f, 0.82f); dark = C(0.56f, 0.62f, 0.70f); return true;
                case "emberfall": ground = C(0.26f, 0.22f, 0.21f); light = C(0.36f, 0.33f, 0.31f); dark = C(0.16f, 0.14f, 0.14f); return true;
                case "canopy":    ground = C(0.24f, 0.41f, 0.21f); light = C(0.30f, 0.47f, 0.24f); dark = C(0.29f, 0.28f, 0.18f); return true;
                case "nameless":  ground = C(0.38f, 0.37f, 0.40f); light = C(0.45f, 0.44f, 0.47f); dark = C(0.31f, 0.30f, 0.34f); return true;
                default:
                    ground = light = dark = Color.clear;
                    return false;
            }
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);
    }
}
