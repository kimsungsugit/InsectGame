using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 리전별 대기 — 햇빛 색·세기, 환경광(하늘 쪽) 색, 원경 연무. <see cref="SubAreaEnvironment"/>가
    /// 서브에리어 밖에서 현재 리전의 값으로 부드럽게 옮겨 간다.
    ///
    /// 옛날엔 메인 필드 전체가 같은 한낮 빛이라 서릿길과 잿불 골짜기가 바닥색만 다른 같은 들판이었다.
    ///
    /// <b>연무는 지수제곱(Exp2)이고 옅다.</b> 목적은 원경 산맥을 하늘로 풀어 주는 공기원근이지 시야 차단이 아니다.
    /// 게임 카메라는 플레이어에서 약 11m 떨어져 있으므로 그 거리의 투과율이 98%를 넘어야 한다 —
    /// <see cref="MaxFogDensity"/>가 그 상한이고 <c>FieldThemeTests.RegionAtmosphere_FogNeverHidesThePlayer</c>가 고정한다.
    /// 내장 안개는 스카이박스에 안 걸리므로 연무색을 지평선 하늘색 근처로 잡는다(잿불·이름 없는 자리만 예외).
    ///
    /// 안개 모드는 <see cref="FieldFogMode"/> 상수 하나다. 런타임(<see cref="SubAreaEnvironment"/>)과 가시거리 검사가 둘 다
    /// 그 상수를 <see cref="FogTransmittance"/>에 넣는다 — 예전 검사는 Exp2 식을 테스트 안에 직접 적어서, 런타임이 Exp로
    /// 바뀌면(밀도 상한 0.009에서 11m 투과율이 99.0% → 90.6%로 떨어진다) 캐릭터가 뿌옇게 묻혀도 통과했다.
    /// </summary>
    public static class RegionAtmosphere
    {
        /// <summary>메인 필드 리전 연무의 안개 모드 — 원경만 흐리고 가까운 곳은 거의 안 건드리는 지수제곱.</summary>
        public const FogMode FieldFogMode = FogMode.ExponentialSquared;

        /// <summary>서브에리어 방의 안개 모드 — 벽이 가까운 좁은 방이라 거리에 고르게 먹는 지수.</summary>
        public const FogMode SubAreaFogMode = FogMode.Exponential;

        /// <summary>게임 카메라에서 플레이어까지의 거리(m, 고각 (0,9,−6) 구도).</summary>
        public const float PlayerCameraDistance = 11f;

        /// <summary>그 거리에서 지켜야 할 안개 투과율 하한 — 아래로 내려가면 캐릭터가 뿌옇다.</summary>
        public const float MinPlayerTransmittance = 0.98f;

        /// <summary><see cref="FieldFogMode"/>(Exp2)에서 11m 투과율이 98%가 되는 밀도(≈0.0129)에 여유를 둔 상한.</summary>
        public const float MaxFogDensity = 0.009f;

        // ── 안개 날씨(<see cref="WorldSkyRules"/>가 리전 연무 위에 얹는 가산) ──
        // 리전 연무는 "원경을 하늘로 푸는 공기원근"이라 98%였지만, 안개 낀 날은 안개가 **보여야** 한다. 대신 캐릭터가 묻히면 안 되므로
        // 같은 11m 거리에서 아래 하한을 지킨다. 한도는 투과율 식에서 거꾸로 푼 값에 여유를 둔 것이고
        // <c>WorldSkyRulesTests</c>가 모든 리전·시각·날씨 조합으로 같은 식(<see cref="FogTransmittance"/>)을 돌려 고정한다.

        /// <summary>메인 필드(<see cref="FieldFogMode"/>)의 안개 날씨에서 11m 투과율 하한 — 캐릭터가 읽히는 선.</summary>
        public const float MinWeatherTransmittance = 0.85f;

        /// <summary><see cref="FieldFogMode"/>(Exp2)에서 11m 투과율 85%가 되는 밀도(≈0.0366)에 여유를 둔 상한.</summary>
        public const float MaxWeatherFogDensity = 0.034f;

        /// <summary>섬 같은 분리 서브에리어(<see cref="SubAreaFogMode"/>, Exp)의 안개 날씨 11m 투과율 하한.</summary>
        public const float MinWeatherTransmittanceSubArea = 0.80f;

        /// <summary><see cref="SubAreaFogMode"/>(Exp)에서 11m 투과율 80%가 되는 밀도(≈0.0203)에 여유를 둔 상한.</summary>
        public const float MaxWeatherFogDensitySubArea = 0.019f;

        /// <summary>
        /// Unity 내장 안개의 투과율(1 = 안개 없음) — 셰이더의 <c>UNITY_CALC_FOG_FACTOR</c>와 같은 식이다.
        /// Linear e = (end − z)/(end − start), Exp e = exp(−density·z), Exp2 e = exp(−(density·z)²).
        /// Linear의 시작·끝 기본값은 <c>RenderSettings</c>의 기본값(0, 300)이다.
        /// </summary>
        public static float FogTransmittance(FogMode mode, float density, float distance, float linearStart = 0f, float linearEnd = 300f)
        {
            switch (mode)
            {
                case FogMode.Exponential:
                    return Mathf.Exp(-density * distance);
                case FogMode.ExponentialSquared:
                    float x = density * distance;
                    return Mathf.Exp(-x * x);
                default:
                    return linearEnd > linearStart ? Mathf.Clamp01((linearEnd - distance) / (linearEnd - linearStart)) : 1f;
            }
        }

        public struct Profile
        {
            public Color light;
            public float intensity;     // 기본 햇빛 세기에 곱한다
            public Color ambientSky;
            public Color fog;
            public float fogDensity;
        }

        public static bool TryGet(string regionId, out Profile p)
        {
            switch (regionId)
            {
                case "meadow":    p = P(1.00f, 0.96f, 0.86f, 1.00f, 0.50f, 0.56f, 0.64f, 0.76f, 0.83f, 0.90f, 0.0026f); return true;
                case "pond":      p = P(0.97f, 0.97f, 0.92f, 1.00f, 0.49f, 0.57f, 0.66f, 0.74f, 0.84f, 0.91f, 0.0030f); return true;
                case "forest":    p = P(0.95f, 0.98f, 0.84f, 0.95f, 0.44f, 0.52f, 0.47f, 0.66f, 0.76f, 0.68f, 0.0042f); return true;
                case "swamp":     p = P(0.90f, 0.94f, 0.80f, 0.90f, 0.42f, 0.47f, 0.42f, 0.58f, 0.64f, 0.54f, 0.0060f); return true;
                case "mountain":  p = P(0.98f, 0.98f, 1.00f, 1.05f, 0.50f, 0.55f, 0.64f, 0.78f, 0.83f, 0.90f, 0.0034f); return true;
                case "garden":    p = P(1.00f, 0.95f, 0.88f, 1.02f, 0.55f, 0.54f, 0.60f, 0.86f, 0.83f, 0.88f, 0.0028f); return true;
                case "ruins":     p = P(1.00f, 0.93f, 0.80f, 1.00f, 0.52f, 0.50f, 0.52f, 0.80f, 0.78f, 0.74f, 0.0036f); return true;
                case "hollow":    p = P(0.94f, 0.93f, 0.88f, 0.95f, 0.50f, 0.51f, 0.52f, 0.74f, 0.74f, 0.71f, 0.0052f); return true;
                case "dunes":     p = P(1.00f, 0.93f, 0.78f, 1.10f, 0.59f, 0.55f, 0.52f, 0.88f, 0.82f, 0.72f, 0.0034f); return true;
                case "frostline": p = P(0.88f, 0.94f, 1.00f, 1.00f, 0.54f, 0.60f, 0.71f, 0.82f, 0.88f, 0.95f, 0.0046f); return true;
                case "emberfall": p = P(1.00f, 0.80f, 0.62f, 0.92f, 0.55f, 0.44f, 0.40f, 0.60f, 0.46f, 0.42f, 0.0058f); return true;
                case "canopy":    p = P(0.94f, 1.00f, 0.84f, 0.96f, 0.45f, 0.55f, 0.47f, 0.64f, 0.78f, 0.66f, 0.0040f); return true;
                case "nameless":  p = P(0.84f, 0.84f, 0.92f, 0.86f, 0.45f, 0.44f, 0.50f, 0.52f, 0.50f, 0.58f, 0.0068f); return true;
                default:
                    p = default;
                    return false;
            }
        }

        private static Profile P(float lr, float lg, float lb, float intensity,
            float ar, float ag, float ab, float fr, float fg, float fb, float density)
        {
            return new Profile
            {
                light = new Color(lr, lg, lb),
                intensity = intensity,
                ambientSky = new Color(ar, ag, ab),
                fog = new Color(fr, fg, fb),
                fogDensity = density,
            };
        }
    }
}
