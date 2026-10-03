using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 낮·밤과 날씨가 하늘·햇빛·환경광·안개에 주는 <b>순수 규칙</b>(색 수학뿐 — 씬·시계·렌더 설정에 손대지 않는다).
    /// <see cref="SubAreaEnvironment"/>가 <see cref="Evaluate"/>로 그 순간의 <see cref="SkyFrame"/>을 만들고 <see cref="Apply"/>로
    /// 리전·섬의 "기본 상태"(보정 전) 위에 얹는다. 시계(<c>GameClock</c>)와 날씨(<c>WeatherSystem</c>)를 읽는 것도 그쪽이다 —
    /// 여기는 숫자만 받으므로 테스트가 시각·날씨 조합을 전부 훑는다.
    ///
    /// <b>정오·맑음은 기본 상태를 그대로 돌려준다</b> — 이 규칙이 생기기 전의 한낮 모습(리전별 빛·연무)이 곧 기준이다.
    /// 나머지 시각·날씨는 그 기준에서 곱하고 섞어 파생한다. 그래서 기본 상태가 바뀌어도(리전 대기 표) 낮·밤이 따라간다.
    ///
    /// <b>시간 곡선은 키프레임 표다</b>(<see cref="Keys"/>) — 인접 키 사이를 smoothstep으로 이어서 시간대 경계에서 뚝 끊기지 않는다.
    /// 일출 5~7시 · 정오 12시(최대) · 저녁노을 17~20시 · 해 질 녘 20~22시 · 밤 22~5시. 해는 6시에 뜨고 19시에 진다
    /// (<see cref="SunHeight01"/>) — 게임 시간대(아침 6~11·낮 11~17·저녁 17~21·밤 21~6)와 겹쳐 읽히게 저녁을 길게 잡았다.
    ///
    /// <b>밤은 어둡되 보여야 한다.</b> 밤에는 디렉셔널 라이트가 달빛(차갑고 푸른 약한 빛, <see cref="MoonIntensity"/>)이 되고,
    /// 환경광에 <b>밝기 바닥</b>(<see cref="AmbientFloorSky"/> 등)을 건다 — 리전·날씨·시각이 어떤 조합이어도 바닥 아래로
    /// 내려가지 않는다(<see cref="EnsureLuma"/>). 기준 수치(기본 필드, 평면 위쪽을 향한 면의 조도 = 환경광 + 빛×sin(고도)):
    /// 정오 맑음 ≈ 1.43, 한밤 맑음 ≈ 0.62(44%), 한낮 비 ≈ 0.75, 한밤 비 ≈ 0.50. 이 게임은 감마 색공간이라 알베도 0.5짜리 풀밭이
    /// 한밤에 약 0.3(표시값 80/255)으로 그려진다 — 어두운 파랑이지만 곤충·캐릭터 윤곽이 읽히는 선이다.
    /// 실기기 밝기가 어긋나면 <see cref="MoonIntensity"/>와 세 바닥 상수만 만지면 된다.
    ///
    /// <b>새 게임은 6시(일출)에 시작한다</b>(<c>GameClock.startTime01</c> 0.25) — 첫 장면이 이 곡선의 6시다. 그래서 일출 쪽은 해가 낮아도
    /// 바닥이 정오의 약 55%로 밝게 잡았다(<see cref="SunMinPitch"/>). 맑은 첫 3분(<c>WeatherForecast.FirstClearSeconds</c>) 동안
    /// 일출 → 아침 → 정오가 지나간다.
    /// </summary>
    public static class WorldSkyRules
    {
        // ── 해·달 ──

        /// <summary>달빛 세기(디렉셔널 라이트). 낮 기본 1.0~1.3의 약 1/3 — 그림자는 지되 화면을 지배하지 않는다.</summary>
        public const float MoonIntensity = 0.45f;

        /// <summary>달빛 방향의 고도(도). 높게 떠서 그림자가 짧고 캐릭터 발밑에 모인다.</summary>
        public const float MoonPitch = 52f;

        /// <summary>달빛의 방향 각(yaw)이 기본 햇빛과 벌어지는 정도(도) — 낮과 그림자 방향이 달라 밤으로 읽힌다.</summary>
        public const float MoonYawOffset = 150f;

        /// <summary>
        /// 해가 지평선 근처에서도 이보다 낮아지지 않는다(도). 더 낮으면 그림자가 한없이 늘어나고(그림자 지도 해상도가 낮은 모바일에서 뭉개진다)
        /// 고각 카메라가 내려다보는 바닥이 어두워진다 — 14°에서는 첫 시작 시각인 6시의 위쪽 조도가 정오의 44%로 한밤 수준이었다.
        /// 22°면 그림자 길이가 높이의 2.5배이고 6시 조도가 약 55%다.
        /// </summary>
        public const float SunMinPitch = 22f;

        /// <summary>일출(−)에서 일몰(+)까지 햇빛 방향 각(yaw)이 기본에서 흔들리는 폭(도) — 그림자가 하루 동안 돈다.</summary>
        public const float SunYawSwing = 80f;

        public const float SunriseHour = 6f;
        public const float NoonHour = 12f;
        public const float SunsetHour = 19f;

        public static readonly Color MoonColor = new Color(0.62f, 0.74f, 1f, 1f);

        // ── 환경광 밝기 바닥(색의 휘도) ──
        // 평면 환경광(섬)은 사방이 같은 색이라 세 바닥이 모두 걸린다. 기본 필드의 낮 환경광은 위 0.55 · 옆 0.42 · 아래 0.24 정도.

        /// <summary>위쪽(하늘 쪽) 환경광 휘도의 바닥 — 바닥·지붕처럼 위를 향한 면이 이 밑으로 어두워지지 않는다.</summary>
        public const float AmbientFloorSky = 0.36f;

        /// <summary>옆쪽(적도) 환경광 휘도의 바닥 — 캐릭터·곤충의 옆면이 이 밑으로 어두워지지 않는다.</summary>
        public const float AmbientFloorEquator = 0.29f;

        /// <summary>아래쪽 환경광 휘도의 바닥 — 처마 밑·그늘.</summary>
        public const float AmbientFloorGround = 0.16f;

        /// <summary>옆면이 받는 빛의 평균 비율(수평 성분 × 이 값) — 조도 비교용 근사.</summary>
        public const float SideLightFactor = 0.64f;

        // ── 흐린 하늘의 연무 막 ──
        // 절차적 스카이박스로는 회색 하늘을 못 낸다. 대기 두께를 올리면 뿌예지는 게 아니라 긴 경로에서 파랑이 빠져 지평선이 주황이 된다
        // (레일리 계수가 두께의 2.5제곱 — ×2.6이면 약 11배. 흐린 정오가 해 질 녘처럼 찍혔다). 그래서 스카이박스는 맑은 하늘 그대로 두고
        // 그 앞에 <b>안개색 막</b>을 씌운다. 막의 색이 곧 원경을 덮는 안개색이라 산맥이 지평선에서 끊김 없이 하늘로 녹는다.

        /// <summary>이 흐림에서 연무 막이 지평선을 완전히 덮는다 — 안개 날씨의 흐림(0.70). 비·눈·안개는 전부 1, 센바람은 약 0.17.</summary>
        public const float VeilFullOvercast = 0.70f;

        /// <summary>지평선에서 이 고도(도)까지는 연무 막이 가장 짙다 — 원경 산맥·구름이 걸치는 띠. 그 아래(땅 쪽)도 가장 짙다.</summary>
        public const float VeilBandDegrees = 8f;

        /// <summary>이 고도(도)부터 위는 연무 막이 <see cref="VeilZenithFactor"/>만큼만 덮는다.</summary>
        public const float VeilTopDegrees = 60f;

        /// <summary>
        /// 천정 쪽 연무 막 짙기(지평선 대비). 0.8이면 흐린 날 천정에 원래 하늘이 20% 비쳐 회청색으로 남는다 —
        /// 낮은 구도에서도 화면 위끝이 고도 약 40°라 그 근처는 0.85 이상이다.
        /// </summary>
        public const float VeilZenithFactor = 0.8f;

        /// <summary>
        /// 연무 막의 세로 분포(지평선 대비 0~1) — 고도(도) <see cref="VeilBandDegrees"/> 아래는 1, <see cref="VeilTopDegrees"/> 위는
        /// <see cref="VeilZenithFactor"/>, 그 사이는 smoothstep. 지평선 쪽이 짙어야 경로가 긴 쪽이 더 뿌연 실제 하늘처럼 읽힌다.
        /// </summary>
        public static float VeilProfile(float elevationDegrees)
        {
            if (elevationDegrees <= VeilBandDegrees) return 1f;
            float t = Smooth((elevationDegrees - VeilBandDegrees) / (VeilTopDegrees - VeilBandDegrees));
            return Mathf.Lerp(1f, VeilZenithFactor, t);
        }

        /// <summary>
        /// 섬 같은 분리 서브에리어(안개가 지수 Exp)에서 날씨 안개 가산에 곱하는 환산 계수. 같은 11m에서 Exp2와 비슷한 투과율이 되게
        /// 밀도를 줄인다(Exp2 0.026 → Exp 0.0104).
        /// </summary>
        public const float SubAreaFogScale = 0.4f;

        // ── 시각 키프레임 ──

        private struct Key
        {
            public float hour;
            public float sun;        // 햇빛 세기 배수(정오 = 1)
            public Color sunTint;    // 햇빛색에 곱
            public float moon;       // 달빛 세기 배수(한밤 = 1)
            public Color ambient;    // 환경광에 곱
            public Color fog;        // 안개·하늘 배경에 곱
            public float exposure;   // 스카이박스 노출 배수
            public float stars;      // 별 투명도
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        private static Key K(float hour, float sun, Color sunTint, float moon, Color ambient, Color fog, float exposure, float stars)
        {
            return new Key { hour = hour, sun = sun, sunTint = sunTint, moon = moon, ambient = ambient, fog = fog, exposure = exposure, stars = stars };
        }

        // 한밤 — 푸르고 낮은 휘도. 안개·하늘은 짙은 남색, 환경광은 바닥(위 0.36)에 걸릴 만큼 푸르다.
        private static readonly Color NightAmbient = C(0.50f, 0.60f, 0.95f);
        private static readonly Color NightFog = C(0.12f, 0.17f, 0.32f);
        private static readonly Color White = C(1f, 1f, 1f);

        /// <summary>
        /// 시각(0~24) 키프레임. 처음과 끝(0·24)은 같은 값이라 자정을 넘겨도 이어진다.
        /// 일출 5.5~7 · 노을 17~20 · 해 질 녘 20~22 — 열(列) 순서: 햇빛 배수 · 햇빛 색 · 달빛 배수 · 환경광 색 · 안개/하늘 색 · 노출 · 별.
        /// 달빛은 해가 지기 전(18~19시)부터 차오르고 새벽에도 6시까지 남는다 — 그러지 않으면 해가 약해진 해 질 녘이
        /// 한밤보다 어두운 "홈"이 생긴다(실측: 달빛이 20시에 시작하면 19시 조도가 한밤의 74%).
        /// </summary>
        private static readonly Key[] Keys =
        {
            K(0.0f,  0.00f, White,                1.00f, NightAmbient,            NightFog,                0.10f, 1.00f),
            K(4.5f,  0.00f, C(1f, 0.55f, 0.35f),  1.00f, NightAmbient,            NightFog,                0.10f, 1.00f),
            K(5.5f,  0.28f, C(1f, 0.60f, 0.42f),  0.70f, C(0.72f, 0.70f, 0.90f), C(0.36f, 0.31f, 0.46f),  0.40f, 0.35f),
            K(6.0f,  0.72f, C(1f, 0.80f, 0.62f),  0.12f, C(0.92f, 0.88f, 0.94f), C(0.96f, 0.68f, 0.56f),  0.80f, 0.10f),
            K(7.0f,  0.90f, C(1f, 0.92f, 0.80f),  0.00f, C(0.96f, 0.95f, 0.97f), C(0.96f, 0.88f, 0.82f),  0.97f, 0.00f),
            K(9.0f,  0.97f, C(1f, 0.98f, 0.93f),  0.00f, C(0.99f, 0.98f, 0.99f), C(0.99f, 0.97f, 0.95f),  1.00f, 0.00f),
            K(12.0f, 1.00f, White,                0.00f, White,                   White,                   1.00f, 0.00f),
            K(15.0f, 0.97f, C(1f, 0.98f, 0.94f),  0.00f, C(0.99f, 0.98f, 0.97f), C(1f, 0.98f, 0.95f),     1.00f, 0.00f),
            K(17.0f, 0.86f, C(1f, 0.90f, 0.72f),  0.00f, C(0.95f, 0.91f, 0.90f), C(1f, 0.86f, 0.72f),     0.95f, 0.00f),
            K(18.0f, 0.68f, C(1f, 0.76f, 0.54f),  0.12f, C(0.88f, 0.80f, 0.84f), C(1f, 0.72f, 0.54f),     0.85f, 0.00f),
            K(19.0f, 0.42f, C(1f, 0.60f, 0.42f),  0.55f, C(0.78f, 0.68f, 0.84f), C(0.92f, 0.54f, 0.52f),  0.55f, 0.05f),
            K(20.0f, 0.15f, C(0.90f, 0.46f, 0.46f), 0.90f, C(0.62f, 0.60f, 0.88f), C(0.58f, 0.38f, 0.52f), 0.30f, 0.30f),
            K(21.0f, 0.00f, C(0.90f, 0.46f, 0.46f), 1.00f, C(0.52f, 0.60f, 0.94f), C(0.26f, 0.24f, 0.44f), 0.15f, 0.75f),
            K(22.0f, 0.00f, White,                1.00f, NightAmbient,            NightFog,                0.10f, 1.00f),
            K(24.0f, 0.00f, White,                1.00f, NightAmbient,            NightFog,                0.10f, 1.00f),
        };

        // ── 날씨 등급 ──

        private struct Grade
        {
            public float sunDim;      // 햇빛 배수
            public float moonDim;     // 달빛 배수
            public float ambientDim;  // 환경광 배수
            public float overcast;    // 0~1 흐림 — 색을 회색으로 풀고, 그림자·별·하늘에 번진다
            public float fogAdd;      // 안개 밀도 가산(FieldFogMode 단위)
            public Color cool;        // 햇빛·환경광에 곱하는 색조
        }

        private static Grade GradeOf(WeatherType weather)
        {
            switch (weather)
            {
                // 센바람 — 맑되 먼지가 낀다. 약간 따뜻하고 누런 빛.
                case WeatherType.Wind:
                    return new Grade { sunDim = 0.92f, moonDim = 0.95f, ambientDim = 0.98f, overcast = 0.12f, fogAdd = 0.001f, cool = C(1.00f, 0.98f, 0.93f) };
                // 비 — 두꺼운 구름. 햇빛이 40%로 줄고 그림자가 거의 사라진다. 푸른 회색.
                case WeatherType.Rain:
                    return new Grade { sunDim = 0.40f, moonDim = 0.55f, ambientDim = 0.82f, overcast = 0.85f, fogAdd = 0.006f, cool = C(0.88f, 0.94f, 1.00f) };
                // 안개 — 빛이 안개에 퍼져 환경광은 거의 그대로, 직사만 줄어든다. 밀도 가산이 이 날씨의 본체다.
                case WeatherType.Fog:
                    return new Grade { sunDim = 0.55f, moonDim = 0.65f, ambientDim = 0.95f, overcast = 0.70f, fogAdd = 0.026f, cool = C(0.96f, 0.97f, 0.98f) };
                // 눈 — 흐리지만 눈 반사로 환경광은 안 줄고, 밤에도 달빛이 덜 죽는다. 푸르스름한 흰빛.
                case WeatherType.Snow:
                    return new Grade { sunDim = 0.50f, moonDim = 0.80f, ambientDim = 0.97f, overcast = 0.75f, fogAdd = 0.012f, cool = C(0.92f, 0.97f, 1.05f) };
                default:
                    return new Grade { sunDim = 1f, moonDim = 1f, ambientDim = 1f, overcast = 0f, fogAdd = 0f, cool = White };
            }
        }

        // ── 값 ──

        /// <summary>
        /// 날씨 가중치 — 전환(<c>WeatherSystem.Blend01</c>) 중에는 두 날씨가 섞인다. 합이 1이 아니어도 <see cref="Evaluate"/>가 정규화한다.
        /// 지역마다 보이는 날씨가 다르므로(<see cref="WeatherForecast.EffectiveIn"/>) 호출부가 <b>지역 기준 날씨</b>를 넣는다.
        /// </summary>
        public struct WeatherMix
        {
            public float clear, rain, fog, wind, snow;

            public static WeatherMix Of(WeatherType weather)
            {
                var m = new WeatherMix();
                m.Add(weather, 1f);
                return m;
            }

            /// <summary>
            /// <paramref name="from"/>→<paramref name="to"/> 전환의 가중치. <paramref name="blend01"/> 0이면 from 100%, 1이면 to 100%.
            /// 진행은 smoothstep으로 꺾어 전환 양끝이 부드럽다.
            /// </summary>
            public static WeatherMix Blend(WeatherType from, WeatherType to, float blend01)
            {
                float t = Smooth(blend01);
                var m = new WeatherMix();
                m.Add(from, 1f - t);
                m.Add(to, t);
                return m;
            }

            /// <summary><see cref="WeatherSystem"/>의 전환 상태를 <paramref name="regionId"/> 기준(섬·길 위는 null)으로 읽는다.</summary>
            public static WeatherMix From(WeatherSystem weather, string regionId)
            {
                if (weather == null) return Of(WeatherType.Clear);
                WeatherType from = WeatherForecast.EffectiveIn(weather.PreviousWeather, regionId);
                WeatherType to = WeatherForecast.EffectiveIn(weather.CurrentWeather, regionId);
                return Blend(from, to, weather.Blend01);
            }

            public float Get(WeatherType weather)
            {
                switch (weather)
                {
                    case WeatherType.Rain: return rain;
                    case WeatherType.Fog: return fog;
                    case WeatherType.Wind: return wind;
                    case WeatherType.Snow: return snow;
                    default: return clear;
                }
            }

            private void Add(WeatherType weather, float weight)
            {
                switch (weather)
                {
                    case WeatherType.Rain: rain += weight; break;
                    case WeatherType.Fog: fog += weight; break;
                    case WeatherType.Wind: wind += weight; break;
                    case WeatherType.Snow: snow += weight; break;
                    default: clear += weight; break;
                }
            }

            public float Total => clear + rain + fog + wind + snow;
        }

        /// <summary>
        /// 조명 한 벌 — 기본(보정 전) 상태이자 보정 결과. 평면 환경광(섬·서브에리어)은 위·옆·아래를 같은 색으로 둔다.
        /// </summary>
        public struct LightingState
        {
            public Color lightColor;
            public float lightIntensity;
            public Quaternion lightRotation;
            public float shadowStrength;
            public Color ambientSky;
            public Color ambientEquator;
            public Color ambientGround;
            public bool fogEnabled;
            public Color fogColor;
            public float fogDensity;
            /// <summary>카메라 배경(섬처럼 SolidColor인 곳에선 곧 하늘색).</summary>
            public Color background;

            /// <summary>두 상태를 섞는다. 한쪽만 안개가 켜져 있으면 꺼진 쪽은 밀도 0으로 본다(꺼진 안개의 잔여 밀도가 번지지 않게).</summary>
            public static LightingState Lerp(LightingState a, LightingState b, float t)
            {
                t = Mathf.Clamp01(t);
                float da = a.fogEnabled ? a.fogDensity : 0f;
                float db = b.fogEnabled ? b.fogDensity : 0f;
                return new LightingState
                {
                    lightColor = Color.Lerp(a.lightColor, b.lightColor, t),
                    lightIntensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, t),
                    lightRotation = Quaternion.Slerp(a.lightRotation, b.lightRotation, t),
                    shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, t),
                    ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t),
                    ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t),
                    ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t),
                    fogEnabled = a.fogEnabled || b.fogEnabled,
                    fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
                    fogDensity = Mathf.Lerp(da, db, t),
                    background = Color.Lerp(a.background, b.background, t),
                };
            }
        }

        /// <summary>한 순간의 하늘 — <see cref="Evaluate"/>의 결과. 기본 상태와 무관한 배수·색조·세기만 든다.</summary>
        public struct SkyFrame
        {
            public float hour;
            /// <summary>햇빛 세기 배수(날씨 포함). 정오 맑음 = 1, 밤 = 0.</summary>
            public float sunFactor;
            /// <summary>햇빛색에 곱하는 색조(노을 주황·날씨 색조 포함).</summary>
            public Color sunTint;
            /// <summary>해의 높이 0(지평선)~1(정오) — 고도를 <see cref="SunMinPitch"/>~기본 고도로 보간한다.</summary>
            public float sunHeight;
            /// <summary>해의 방위 −1(일출)~+1(일몰).</summary>
            public float sunAzimuth;
            /// <summary>달빛 세기 배수(날씨 포함). 한밤 맑음 = 1.</summary>
            public float moonFactor;
            /// <summary>환경광에 곱하는 색조(날씨 색조 포함).</summary>
            public Color ambientTint;
            public float ambientDim;
            /// <summary>0~1 흐림.</summary>
            public float overcast;
            /// <summary>안개·하늘 배경에 곱하는 색조.</summary>
            public Color fogTint;
            /// <summary>안개 밀도 가산(<see cref="RegionAtmosphere.FieldFogMode"/> 단위).</summary>
            public float fogAdd;
            /// <summary>스카이박스 노출 배수(흐리면 더 낮다).</summary>
            public float skyExposure;
            /// <summary>
            /// 하늘을 덮는 연무 막의 짙기 0~1(<see cref="VeilFullOvercast"/>에서 1) — 연무 돔(<c>WorldSkyVisuals</c>)의 알파다.
            /// 맑음은 0이라 돔이 꺼진다. 비·눈·안개는 1이라 지평선이 안개색으로 완전히 덮인다.
            /// </summary>
            public float skyHaze;
            /// <summary>별 투명도 0~1(구름이 가리면 줄어든다).</summary>
            public float starAlpha;
            /// <summary>그림자 세기 배수 — 흐리면 그림자가 거의 사라지고 해가 낮으면 옅다.</summary>
            public float shadowScale;
            /// <summary>0.4(한밤)~1(맑은 정오) — 조명 없는 입자(비·눈)의 밝기를 장면에 맞추는 값.</summary>
            public float lightLevel;
        }

        // ── 판정 ──

        /// <summary>
        /// 하늘 보정이 걸리는 곳인가 — 메인 필드와 나의 섬뿐이다. 동굴·방 같은 다른 서브에리어는 실내라 보정이 없고,
        /// 「챔피언의 꿈」은 늘 맑은 한낮이다. 하늘 조명과 날씨 입자가 같은 판정을 쓴다.
        /// </summary>
        public static bool Applies(bool inSubArea, string subAreaId, bool dreamActive)
        {
            if (dreamActive) return false;
            return !inSubArea || subAreaId == GameConstants.Island.SubAreaId;
        }

        /// <summary>해의 높이 0(지평선)~1(정오). 6시에 뜨고 12시에 가장 높고 19시에 진다 — 정오에서 정확히 최대(미분 0)다.</summary>
        public static float SunHeight01(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            if (hour <= SunriseHour || hour >= SunsetHour) return 0f;
            if (hour <= NoonHour) return Mathf.Sin(Mathf.PI * 0.5f * (hour - SunriseHour) / (NoonHour - SunriseHour));
            return Mathf.Cos(Mathf.PI * 0.5f * (hour - NoonHour) / (SunsetHour - NoonHour));
        }

        /// <summary>해의 방위 −1(일출)~+1(일몰) — 해가 뜨기 전·진 뒤에는 양끝에 머문다.</summary>
        public static float SunAzimuth(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            float a = hour < NoonHour ? (hour - NoonHour) / (NoonHour - SunriseHour) : (hour - NoonHour) / (SunsetHour - NoonHour);
            return Mathf.Clamp(a, -1f, 1f);
        }

        // ── 평가 ──

        /// <summary>이 시각·날씨 가중치의 하늘. 시각은 0~24(소수) — <c>GameClock.GetHourFloat()</c>.</summary>
        public static SkyFrame Evaluate(float hour, WeatherMix mix)
        {
            hour = Mathf.Repeat(hour, 24f);
            Key k = SampleKey(hour);

            float total = mix.Total;
            if (total <= 1e-5f)
            {
                mix = WeatherMix.Of(WeatherType.Clear);
                total = 1f;
            }
            float inv = 1f / total;

            float sunDim = 0f, moonDim = 0f, ambientDim = 0f, overcast = 0f, fogAdd = 0f;
            float coolR = 0f, coolG = 0f, coolB = 0f;
            for (int i = 0; i < 5; i++)
            {
                WeatherType w = (WeatherType)i;
                float weight = mix.Get(w) * inv;
                if (weight <= 0f) continue;
                Grade g = GradeOf(w);
                sunDim += g.sunDim * weight;
                moonDim += g.moonDim * weight;
                ambientDim += g.ambientDim * weight;
                overcast += g.overcast * weight;
                fogAdd += g.fogAdd * weight;
                coolR += g.cool.r * weight;
                coolG += g.cool.g * weight;
                coolB += g.cool.b * weight;
            }
            Color cool = new Color(coolR, coolG, coolB, 1f);

            var f = new SkyFrame
            {
                hour = hour,
                sunFactor = k.sun * sunDim,
                sunTint = Mul(k.sunTint, cool),
                sunHeight = SunHeight01(hour),
                sunAzimuth = SunAzimuth(hour),
                moonFactor = k.moon * moonDim,
                ambientTint = Mul(k.ambient, cool),
                ambientDim = ambientDim,
                overcast = overcast,
                fogTint = k.fog,
                fogAdd = fogAdd,
                skyExposure = k.exposure * Mathf.Lerp(1f, 0.75f, overcast),
                skyHaze = Mathf.Clamp01(overcast / VeilFullOvercast),
                starAlpha = k.stars * (1f - overcast),
                shadowScale = (1f - 0.85f * overcast) * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(k.sun)),
            };
            f.lightLevel = Mathf.Lerp(0.40f, 1f, Mathf.Clamp01(f.sunFactor));
            return f;
        }

        /// <summary>한 날씨로 고정한 하늘.</summary>
        public static SkyFrame Evaluate(float hour, WeatherType weather) => Evaluate(hour, WeatherMix.Of(weather));

        /// <summary>
        /// 기본 상태(<paramref name="b"/>, 리전·섬의 보정 전 값) 위에 하늘을 얹는다.
        /// <paramref name="fieldFog"/>가 참이면 메인 필드(<see cref="RegionAtmosphere.FieldFogMode"/>, 지수제곱)의 안개 가산 규칙을,
        /// 거짓이면 섬 같은 서브에리어(<see cref="RegionAtmosphere.SubAreaFogMode"/>, 지수)의 환산·상한을 쓴다.
        /// </summary>
        public static LightingState Apply(LightingState b, SkyFrame f, bool fieldFog)
        {
            LightingState o = b;
            float oc = f.overcast;

            // ── 해와 달: 라이트 하나를 둘이 나눠 쓴다. 세기 비율로 색·방향을 섞어 해 질 녘에 부드럽게 넘어간다 ──
            Vector3 e = b.lightRotation.eulerAngles;
            float basePitch = e.x > 180f ? e.x - 360f : e.x;
            float baseYaw = e.y;
            float sunPitch = Mathf.Lerp(SunMinPitch, basePitch, f.sunHeight);
            Quaternion sunRot = Quaternion.Euler(sunPitch, baseYaw + f.sunAzimuth * SunYawSwing, 0f);
            Quaternion moonRot = Quaternion.Euler(MoonPitch, baseYaw + MoonYawOffset, 0f);

            float sunI = b.lightIntensity * f.sunFactor;
            float moonI = MoonIntensity * f.moonFactor;
            float lightTotal = sunI + moonI;
            Color sunColor = Desaturate(Mul(b.lightColor, f.sunTint), oc * 0.5f);
            if (lightTotal > 1e-4f)
            {
                float moonShare = moonI / lightTotal;
                o.lightIntensity = lightTotal;
                o.lightColor = Color.Lerp(sunColor, MoonColor, moonShare);
                o.lightRotation = Quaternion.Slerp(sunRot, moonRot, moonShare);
            }
            else
            {
                o.lightIntensity = 0f;
                o.lightColor = sunColor;
                o.lightRotation = sunRot;
            }
            o.shadowStrength = b.shadowStrength * f.shadowScale;

            // ── 환경광: 색조·밝기·흐림에 따른 탈색 → 바닥 ──
            o.ambientSky = Ambient(b.ambientSky, f, AmbientFloorSky);
            o.ambientEquator = Ambient(b.ambientEquator, f, AmbientFloorEquator);
            o.ambientGround = Ambient(b.ambientGround, f, AmbientFloorGround);

            // ── 안개·하늘: 시각 색조 → 흐리면 회색으로 풀고 어둡게 ──
            o.fogColor = SkyColor(b.fogColor, f);
            o.background = SkyColor(b.background, f);

            float add = fieldFog ? f.fogAdd : f.fogAdd * SubAreaFogScale;
            if (add > 0.0005f)
            {
                float cap = fieldFog ? RegionAtmosphere.MaxWeatherFogDensity : RegionAtmosphere.MaxWeatherFogDensitySubArea;
                float baseDensity = b.fogEnabled ? b.fogDensity : 0f;
                o.fogEnabled = true;
                // 기본이 이미 상한 위인 경우는 없지만(리전 연무 ≤ 0.009) 있어도 기본을 깎지는 않는다
                o.fogDensity = Mathf.Max(baseDensity, Mathf.Min(baseDensity + add, cap));
            }
            return o;
        }

        // ── 조도(테스트·진단) ──

        /// <summary>색의 휘도(감마 색공간 근사).</summary>
        public static float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        /// <summary>하늘을 향한 평면이 받는 조도 근사 = 위쪽 환경광 + 빛 × sin(고도).</summary>
        public static float UpLuminance(LightingState s)
        {
            Vector3 forward = s.lightRotation * Vector3.forward;
            float up = Mathf.Max(0f, -forward.y);
            return Luminance(s.ambientSky) + s.lightIntensity * Luminance(s.lightColor) * up;
        }

        /// <summary>서 있는 캐릭터의 옆면이 받는 조도 근사 = 옆쪽 환경광 + 빛 × 수평 성분 × <see cref="SideLightFactor"/>.</summary>
        public static float SideLuminance(LightingState s)
        {
            Vector3 forward = s.lightRotation * Vector3.forward;
            float horizontal = new Vector2(forward.x, forward.z).magnitude;
            return Luminance(s.ambientEquator) + s.lightIntensity * Luminance(s.lightColor) * horizontal * SideLightFactor;
        }

        // ── 내부 ──

        private static Key SampleKey(float hour)
        {
            int last = Keys.Length - 1;
            for (int i = 0; i < last; i++)
            {
                Key a = Keys[i];
                Key b = Keys[i + 1];
                if (hour > b.hour) continue;
                float span = Mathf.Max(0.0001f, b.hour - a.hour);
                float t = Smooth((hour - a.hour) / span);
                return new Key
                {
                    hour = hour,
                    sun = Mathf.Lerp(a.sun, b.sun, t),
                    sunTint = Color.Lerp(a.sunTint, b.sunTint, t),
                    moon = Mathf.Lerp(a.moon, b.moon, t),
                    ambient = Color.Lerp(a.ambient, b.ambient, t),
                    fog = Color.Lerp(a.fog, b.fog, t),
                    exposure = Mathf.Lerp(a.exposure, b.exposure, t),
                    stars = Mathf.Lerp(a.stars, b.stars, t),
                };
            }
            return Keys[last];
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static Color Mul(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, 1f);

        /// <summary>색을 자기 휘도의 회색 쪽으로 <paramref name="amount"/>만큼 푼다.</summary>
        private static Color Desaturate(Color c, float amount)
        {
            float l = Luminance(c);
            return new Color(Mathf.Lerp(c.r, l, amount), Mathf.Lerp(c.g, l, amount), Mathf.Lerp(c.b, l, amount), 1f);
        }

        private static Color Ambient(Color c, SkyFrame f, float floor)
        {
            Color a = Mul(c, f.ambientTint);
            a = new Color(a.r * f.ambientDim, a.g * f.ambientDim, a.b * f.ambientDim, 1f);
            return EnsureLuma(Desaturate(a, f.overcast * 0.35f), floor);
        }

        private static Color SkyColor(Color c, SkyFrame f)
        {
            Color s = Desaturate(Mul(c, f.fogTint), f.overcast * 0.8f);
            float dim = Mathf.Lerp(1f, 0.85f, f.overcast);
            return new Color(s.r * dim, s.g * dim, s.b * dim, 1f);
        }

        /// <summary>휘도가 바닥보다 낮으면 색조를 지킨 채 바닥까지 끌어올린다 — 연속이다(바닥 위에선 손대지 않는다).</summary>
        private static Color EnsureLuma(Color c, float floor)
        {
            float l = Luminance(c);
            if (l >= floor) return c;
            if (l < 1e-4f) return new Color(floor, floor, floor, 1f);
            float k = floor / l;
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }
    }
}
