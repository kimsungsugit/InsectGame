using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 날씨 입자 — 비(가는 줄기, 비스듬히) · 눈(느리게 흔들리며) · 센바람(옆으로 흐르는 먼지와 잎). 안개는 입자가 없고
    /// 조명 쪽 안개 밀도로 처리된다(<see cref="WorldSkyRules"/>). 카메라가 보는 곳 위에 큰 상자 방출기 셋을 얹어 따라다닌다.
    ///
    /// <b>무엇을 읽나.</b> 세계 날씨의 전환(<c>PreviousWeather</c>→<c>CurrentWeather</c>, <c>Blend01</c>)을 <b>지역 기준</b>으로 바꿔
    /// (<see cref="WeatherForecast.EffectiveIn"/> — 설산에선 비가 눈, 사막에선 눈이 센바람) 종류별 가중치로 만들고,
    /// 방출량을 그 가중치에 비례시킨다. 그래서 10초 전환 동안 비가 서서히 잦아들고 안개가 서서히 낀다. 섬에는 지역이 없으니
    /// 세계 날씨 그대로다.
    ///
    /// <b>켜지는 곳.</b> 메인 필드와 나의 섬뿐이다(<see cref="WorldSkyRules.Applies"/> — 하늘 조명과 같은 판정). 동굴 같은 실내와
    /// 「챔피언의 꿈」에서는 즉시 끄고 남은 입자를 치운다(서서히 줄이면 그 사이 실내로 옮겨 간 방출기에서 몇 알이 새어 나온다).
    ///
    /// <b>입자가 아무도 가리지 않게</b> 전부 가늘고 옅다 — 빗줄기 폭 4~6cm·알파 0.4, 눈송이 12~24cm, 렌즈에 붙은 입자는
    /// <c>maxParticleSize</c>(뷰포트 비율)로 잘라 화면을 덮지 못하게 했다. 깊이 테스트는 켜 둬서 건물·지형 뒤에서는 가려진다.
    ///
    /// <b>성능.</b> 시스템 셋, 머티리얼·텍스처 하나(<see cref="SkyFx"/>), 메시 없음. 살아 있는 입자는 데스크톱 기준 비 약 500 ·
    /// 눈 약 640 · 바람 약 120이고 모바일은 절반이다. 안 쓰는 시스템은 GameObject째 꺼서 비용이 0이다. <c>Update</c>에서 할당하지 않는다.
    ///
    /// 조명 없는 셰이더(<c>Sprites/Default</c>)라 밤에 입자만 밝게 뜨지 않도록 시각에 따라 색을 어둡게 푼다
    /// (<see cref="WorldSkyRules.SkyFrame.lightLevel"/>).
    /// </summary>
    public class WeatherEffects : MonoBehaviour
    {
        [SerializeField] private WorldStateProvider worldState;
        [SerializeField] private RegionManager regionManager;

        // 초당 방출량 — 살아 있는 입자 수 ≈ 방출량 × 수명
        private const float RainRate = 800f;     // × 0.625s ≈ 500 — 고리(약 970㎡)가 옛 상자(784㎡)보다 넓어 같은 밀도로 올렸다
        private const float SnowRate = 95f;      // × 6.75s ≈ 640 — 공중 전체에서 생겨 일부가 땅에 묻히는 만큼 올렸다(BuildSnow)
        private const float WindRate = 70f;      // × 1.7s ≈ 119
        private const int RainMax = 600;
        private const int SnowMax = 800;
        private const int WindMax = 200;
        private const float MobileScale = 0.5f;

        /// <summary>종류별 가중치가 목표로 가는 속도(초당). 10초 날씨 전환이 이미 부드러우므로 지역 경계·전환 시작의 계단만 다듬는다.</summary>
        private const float FadePerSecond = 1.5f;
        /// <summary>방출기 상자 중심을 카메라 앞 이 거리에 둔다 — 게임 카메라가 플레이어를 11m 앞에서 본다.</summary>
        private const float FollowDistance = 10f;
        private const float BoxSize = 28f;
        /// <summary>비 고리의 바깥 반지름(m) — 고각 카메라가 보는 땅(발밑 약 16m 앞까지)을 덮는다.</summary>
        private const float RainRingRadius = 18f;
        /// <summary>비를 뿌리지 않는 카메라 둘레 반지름(m) — 이보다 가까운 빗줄기는 화면을 가른다(<see cref="BuildRain"/>).</summary>
        private const float RainClearRadius = 4f;
        private const float ColorRefreshSeconds = 0.25f;
        private const float LookupSeconds = 2f;

        private sealed class Layer
        {
            public GameObject go;
            public ParticleSystem ps;
            public float baseRate;
            public float weight;
            public Color dayA, dayB, nightA, nightB;
        }

        private Layer rain;
        private Layer snow;
        private Layer wind;
        private Texture2D dot;
        private Material material;
        private float countScale = 1f;
        private bool built;
        private bool buildFailed;
        private float colorClock;
        private float lookupClock;
        private float lastLevel = -1f;
        private float tintT = 1f;   // 0(한밤 색)~1(한낮 색)

        /// <summary>시계·날씨 공급자와 현재 리전 정보를 주입한다. 안 주입하면 <c>FindFirstObjectByType</c>로 2초마다 찾는다.</summary>
        public void AutoWire(WorldStateProvider provider, RegionManager regions)
        {
            if (provider != null) worldState = provider;
            if (regions != null) regionManager = regions;
        }

        private void OnDisable()
        {
            // 꺼지면 입자도 같이 치운다 — 다시 켜질 때 옛 자리에 남은 입자가 보이지 않게
            DeactivateAll(true);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (dot != null) Destroy(dot);
            material = null;
            dot = null;
        }

        private void LateUpdate()
        {
            if (buildFailed) return;
            if (!built && !Build()) return;

            if ((worldState == null || regionManager == null) && Time.unscaledTime >= lookupClock)
            {
                if (worldState == null) worldState = FindFirstObjectByType<WorldStateProvider>();
                if (regionManager == null) regionManager = FindFirstObjectByType<RegionManager>();
                lookupClock = Time.unscaledTime + LookupSeconds;
            }

            Camera cam = Camera.main;
            if (cam == null) return;

            SubAreaData sub = regionManager != null ? regionManager.CurrentSubArea : null;
            bool outdoors = WorldSkyRules.Applies(sub != null, sub != null ? sub.subAreaId : null, DreamPrologueState.Active);
            if (!outdoors)
            {
                DeactivateAll(true);
                return;
            }

            // 지역 기준 날씨 — 섬·서브에리어는 지역이 없다(null). CurrentRegion은 서브에리어 안에서도 부모 리전이라 서브에리어를 먼저 거른다.
            string regionId = sub == null && regionManager != null && regionManager.CurrentRegion != null
                ? regionManager.CurrentRegion.regionId : null;
            WorldSkyRules.WeatherMix mix = worldState != null
                ? WorldSkyRules.WeatherMix.From(worldState.Weather, regionId)
                : WorldSkyRules.WeatherMix.Of(WeatherType.Clear);

            float dt = Time.deltaTime;
            UpdateLayer(rain, mix.rain, dt);
            UpdateLayer(snow, mix.snow, dt);
            UpdateLayer(wind, mix.wind, dt);

            colorClock -= Time.unscaledDeltaTime;
            if (colorClock <= 0f)
            {
                colorClock = ColorRefreshSeconds;
                RefreshColors(mix);
            }

            // 방출기가 카메라가 보는 곳을 따라간다. 입자는 세계 좌표라 이미 떨어지는 것들은 제자리에서 계속 떨어진다.
            Transform ct = cam.transform;
            transform.position = ct.position + ct.forward * FollowDistance;
            // 비 고리만 카메라 바로 위를 중심으로 — 높이는 다른 층과 같은 기준(시선 앞 지점)이다(BuildRain 주석).
            if (rain != null) rain.go.transform.position = new Vector3(ct.position.x, transform.position.y, ct.position.z);
        }

        // ── 층 갱신 ──

        private void UpdateLayer(Layer layer, float target, float dt)
        {
            if (layer == null) return;
            layer.weight = Mathf.MoveTowards(layer.weight, Mathf.Clamp01(target), FadePerSecond * dt);

            if (layer.weight > 0.003f)
            {
                if (!layer.go.activeSelf)
                {
                    layer.go.SetActive(true);
                    Tint(layer, tintT);   // 첫 입자부터 장면 밝기에 맞는 색으로 — 기본 흰색이 한순간 비치지 않게
                    layer.ps.Play();
                }
                ParticleSystem.EmissionModule emission = layer.ps.emission;
                emission.rateOverTime = layer.baseRate * layer.weight * countScale;
            }
            else if (layer.go.activeSelf)
            {
                ParticleSystem.EmissionModule emission = layer.ps.emission;
                emission.rateOverTime = 0f;
                // 방출을 멈춘 뒤 남은 입자가 다 떨어지면 끈다
                if (layer.ps.particleCount == 0) layer.go.SetActive(false);
            }
        }

        private void DeactivateAll(bool clear)
        {
            DeactivateLayer(rain, clear);
            DeactivateLayer(snow, clear);
            DeactivateLayer(wind, clear);
        }

        private static void DeactivateLayer(Layer layer, bool clear)
        {
            if (layer == null || layer.go == null) return;
            layer.weight = 0f;
            if (!layer.go.activeSelf) return;
            if (clear) layer.ps.Clear(true);
            layer.go.SetActive(false);
        }

        /// <summary>시각에 맞춰 입자 색을 어둡게 푼다. 새로 방출되는 입자부터 바뀐다(이미 떠 있는 것은 수명이 짧아 곧 교체된다).</summary>
        private void RefreshColors(WorldSkyRules.WeatherMix mix)
        {
            float hour = WorldSkyRules.NoonHour;
            if (worldState != null && worldState.Clock != null) hour = worldState.Clock.GetHourFloat();
            float level = WorldSkyRules.Evaluate(hour, mix).lightLevel;
            if (Mathf.Abs(level - lastLevel) < 0.02f) return;
            lastLevel = level;

            tintT = Mathf.Clamp01((level - 0.4f) / 0.6f);
            Tint(rain, tintT);
            Tint(snow, tintT);
            Tint(wind, tintT);
        }

        private static void Tint(Layer layer, float t)
        {
            if (layer == null) return;
            ParticleSystem.MainModule main = layer.ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(layer.nightA, layer.dayA, t), Color.Lerp(layer.nightB, layer.dayB, t));
        }

        // ── 만들기 ──

        private bool Build()
        {
            dot = SkyFx.CreateSoftDot(32);
            material = SkyFx.CreateMaterial(dot, "~WeatherParticle");
            if (material == null)
            {
                // 날씨 입자만 포기한다 — 하늘 조명(안개·흐림)은 입자 없이도 돈다
                Debug.LogWarning("[WeatherEffects] Sprites/Default 셰이더를 못 찾아 날씨 입자를 끈다.");
                if (dot != null) Destroy(dot);
                dot = null;
                buildFailed = true;
                return false;
            }

            countScale = Application.isMobilePlatform ? MobileScale : 1f;
            rain = BuildRain();
            snow = BuildSnow();
            wind = BuildWind();
            Tint(rain, tintT);
            Tint(snow, tintT);
            Tint(wind, tintT);
            built = true;
            return true;
        }

        private Layer NewLayer(string layerName, float rate, int maxParticles)
        {
            var layer = new Layer { baseRate = rate };
            layer.go = new GameObject("~" + layerName);
            layer.go.transform.SetParent(transform, false);
            layer.ps = layer.go.AddComponent<ParticleSystem>();
            // AddComponent가 곧바로 재생을 시작한다 — 멈춘 뒤에 설정한다
            layer.ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = layer.ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = Mathf.Max(8, Mathf.RoundToInt(maxParticles * (Application.isMobilePlatform ? MobileScale : 1f)));

            ParticleSystem.EmissionModule emission = layer.ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = layer.ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = Vector3.zero;

            ParticleSystemRenderer psr = layer.go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = material;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            psr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            layer.go.SetActive(false);
            return layer;
        }

        private static void SetVelocity(ParticleSystem ps, Vector2 x, Vector2 y, Vector2 z)
        {
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            // 세 축이 같은 모드(두 상수 사이 무작위)여야 한다
            vel.x = new ParticleSystem.MinMaxCurve(x.x, x.y);
            vel.y = new ParticleSystem.MinMaxCurve(y.x, y.y);
            vel.z = new ParticleSystem.MinMaxCurve(z.x, z.y);
        }

        private static void SetNoise(ParticleSystem ps, float strength, float frequency, float scroll)
        {
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = scroll;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        /// <summary>
        /// 비 — 12m 위 얇은 고리에서 초속 약 21m로 비스듬히 떨어지는 늘어진 빌보드(폭 4~6cm, 길이 약 0.7m).
        /// 수명 0.55~0.7초라 땅 높이에서 다 떨어지고 사라진다.
        ///
        /// <b>카메라 둘레 <see cref="RainClearRadius"/> 안에서는 뿌리지 않는다</b>(고리의 중심을 매 프레임 카메라 바로 위에 둔다 —
        /// <see cref="LateUpdate"/>). 상자에서 뿌리면 렌즈 코앞을 지나는 빗방울이 화면을 위아래로 가르는 굵은 빛줄기가 됐다
        /// (2026-10-03 캡처). 늘어난 길이는 속도에서 나와 <c>maxParticleSize</c>로 잘리지 않는다. 고각 카메라는 자기 발밑을
        /// 26° 아래부터 보므로 그 빈 기둥은 화면에 드러나지 않는다.
        /// </summary>
        private Layer BuildRain()
        {
            Layer l = NewLayer("Rain", RainRate, RainMax);
            ParticleSystem.MainModule main = l.ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.055f);

            ParticleSystem.ShapeModule shape = l.ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.rotation = new Vector3(90f, 0f, 0f);   // 원은 모양의 XY 평면에 놓인다 — 눕혀 수평 고리로
            shape.arc = 360f;
            shape.radius = RainRingRadius;
            shape.radiusThickness = 1f - RainClearRadius / RainRingRadius;   // 바깥 테두리에서 안쪽으로 이만큼만 채운다
            shape.position = new Vector3(0f, 12f, 0f);

            SetVelocity(l.ps, new Vector2(-3.4f, -2.6f), new Vector2(-23f, -19f), new Vector2(-1.2f, -0.6f));

            ParticleSystemRenderer r = l.go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2f;
            r.velocityScale = 0.03f;
            r.cameraVelocityScale = 0f;
            r.maxParticleSize = 0.03f;

            l.dayA = l.dayB = new Color(0.82f, 0.88f, 1f, 0.40f);
            l.nightA = l.nightB = new Color(0.50f, 0.60f, 0.85f, 0.34f);
            return l;
        }

        /// <summary>
        /// 눈 — 초속 1.3~2m로 느리게 내리며 잡음으로 좌우로 흔들리는 둥근 송이(12~24cm). 수명 6~7.5초.
        ///
        /// <b>판이 아니라 공중 전체(높이 0~12m)에서 생긴다.</b> 비처럼 12m 판에서만 뿌리면 송이가 카메라 시야(고각 카메라는 자기 높이
        /// 아래만 본다)까지 내려오는 데 5초가 넘게 걸려, 동굴에서 나오거나 설산 경계를 넘거나 눈이 막 내리기 시작할 때 화면이 그동안 비어
        /// 있었다(2026-10-03 캡처). 낮게 생긴 송이는 땅에 묻혀 깊이 테스트로 가려질 뿐이라 비용은 입자 몇 개다.
        /// </summary>
        private Layer BuildSnow()
        {
            Layer l = NewLayer("Snow", SnowRate, SnowMax);
            ParticleSystem.MainModule main = l.ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 7.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.ShapeModule shape = l.ps.shape;
            shape.scale = new Vector3(BoxSize, 12f, BoxSize);
            shape.position = new Vector3(0f, 6f, 0f);

            SetVelocity(l.ps, new Vector2(-0.5f, 0.5f), new Vector2(-2f, -1.3f), new Vector2(-0.5f, 0.5f));
            SetNoise(l.ps, 0.9f, 0.3f, 0.15f);

            ParticleSystemRenderer r = l.go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.maxParticleSize = 0.04f;

            l.dayA = l.dayB = new Color(1f, 1f, 1f, 0.90f);
            l.nightA = l.nightB = new Color(0.72f, 0.80f, 1f, 0.80f);
            return l;
        }

        /// <summary>
        /// 센바람 — 초속 9~13m로 옆으로 흐르는 가는 줄. 모래빛 먼지와 풀빛 잎이 섞인다(수명 1.4~2초).
        /// 비·눈처럼 위에서 내리는 게 아니라 땅 위 0~5m 높이의 상자 안에서 일어난다.
        /// </summary>
        private Layer BuildWind()
        {
            Layer l = NewLayer("Wind", WindRate, WindMax);
            ParticleSystem.MainModule main = l.ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);

            ParticleSystem.ShapeModule shape = l.ps.shape;
            shape.scale = new Vector3(BoxSize, 5f, BoxSize);
            shape.position = new Vector3(-6f, 1.5f, 0f);   // 지면(초점 아래 약 1m)에서 4m 위까지. 바람이 불어오는 쪽(−x)으로 치우쳐 시야에 오래 머문다

            SetVelocity(l.ps, new Vector2(9f, 13f), new Vector2(-0.4f, 0.8f), new Vector2(3f, 5f));
            SetNoise(l.ps, 1.2f, 0.5f, 0.3f);

            ParticleSystemRenderer r = l.go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2f;
            r.velocityScale = 0.12f;
            r.cameraVelocityScale = 0f;
            r.maxParticleSize = 0.02f;

            l.dayA = new Color(0.95f, 0.92f, 0.80f, 0.30f);   // 먼지
            l.dayB = new Color(0.62f, 0.72f, 0.34f, 0.65f);   // 잎
            l.nightA = new Color(0.50f, 0.55f, 0.70f, 0.25f);
            l.nightB = new Color(0.30f, 0.40f, 0.30f, 0.45f);
            return l;
        }
    }
}
