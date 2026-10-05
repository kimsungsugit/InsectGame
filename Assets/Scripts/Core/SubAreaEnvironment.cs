using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 리전·서브에리어의 조명 환경을 전환하고, 그 위에 <b>낮·밤·날씨 하늘</b>(<see cref="WorldSkyRules"/>)을 얹는다.
    ///
    /// <b>이중 적용을 막는 구조.</b> 전환 보간(<see cref="BlendBase"/>)은 "기본 상태"(<see cref="baseState"/>, 하늘 보정 전)만 만든다.
    /// 렌더 설정(RenderSettings·라이트·카메라 배경·스카이박스)에 쓰는 건 <see cref="WriteFinal"/>이 기본 상태에 하늘을 얹어 한 번에 한다.
    /// 스냅샷(<see cref="SnapshotCurrent"/>)은 렌더 설정이 아니라 기본 상태에서 뜬다 — 렌더 설정에서 읽으면 밤색이 기본으로 굳어
    /// 리전을 옮길 때마다 하늘이 한 겹씩 더 얹힌다.
    ///
    /// 보정이 걸리는 곳은 메인 필드와 나의 섬뿐이다(<see cref="WorldSkyRules.Applies"/>). 동굴·방은 실내라 없고
    /// 「챔피언의 꿈」은 늘 맑은 한낮이다. 걸리고 풀리는 경계는 <see cref="skyBlend"/>가 0.5초에 걸쳐 오가므로 동굴 입구에서
    /// 밤 하늘이 뚝 끊기지 않는다.
    /// </summary>
    public class SubAreaEnvironment : MonoBehaviour
    {
        [SerializeField] private RegionManager regionManager;
        [SerializeField] private WorldStateProvider worldState;

        private Light directionalLight;
        private Camera mainCamera;

        // 기본 환경 (메인 필드)
        private Color defaultLightColor = new Color(1f, 0.96f, 0.84f);
        private float defaultLightIntensity = 1.2f;
        private Quaternion defaultLightRotation = Quaternion.Euler(50f, 30f, 0f);
        private Color defaultAmbient = new Color(0.45f, 0.5f, 0.55f);
        private Color defaultFogColor = new Color(0.75f, 0.82f, 0.88f);
        private bool defaultFogEnabled;
        private float defaultFogDensity;
        private FogMode defaultFogMode;
        // 하늘색 폴백 — Camera.main이 없어 캡처를 못 했을 때만 쓰인다(형제 필드와 동일 패턴).
        private Color defaultCameraBg = new Color(0.5f, 0.8f, 1f);
        // 메인 필드의 원래 카메라 클리어 플래그(보통 Skybox). 서브지역에선 SolidColor로 바꿔야
        // backgroundColor가 실제로 렌더된다 — Skybox 모드에서 Unity는 backgroundColor를 무시한다.
        private CameraClearFlags defaultClearFlags = CameraClearFlags.Skybox;
        // 메인 필드의 원래 환경광 모드 — 지금은 Trilight다(PlaySceneBootstrap.EnsureLight: 이 씬엔 구운 환경광 프로브가
        // 없어 Skybox 모드면 그늘이 새까맸다). CaptureDefaults가 실제 값을 읽어 덮으므로 이 초기값은 캡처 전 폴백일 뿐이다.
        // 서브지역에선 Flat으로 바꿔 ambientColor 한 색이 사방을 비추게 하고(Trilight면 적도·바닥 색이 메인 필드 것으로 남는다.
        // 예전 Skybox 모드에선 ambientColor가 아예 무시돼 서브지역을 밝힐 수 없었다), 빠져나올 때 이 값으로 복원한다.
        private UnityEngine.Rendering.AmbientMode defaultAmbientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        // Trilight 환경광의 옆·아래 색과 그림자 세기 — 리전 대기는 위쪽(ambientLight = ambientSkyColor) 색만 바꾸므로 나머지는
        // 부트스트랩이 정한 값 그대로이고, 하늘 보정(밤·흐림)이 이 값 위에 얹힌다. CaptureDefaults가 실제 값을 읽어 덮는다.
        private Color defaultAmbientEquator = new Color(0.42f, 0.43f, 0.41f);
        private Color defaultAmbientGround = new Color(0.26f, 0.24f, 0.21f);
        private float defaultShadowStrength = 0.5f;

        // 전환 상태
        private EnvironmentProfile targetProfile;
        private EnvironmentProfile currentState;
        private float transitionProgress = 1f;
        private float transitionSpeed = 2f;

        // 기본 상태 — 전환 보간의 현재 값(하늘 보정 전). 스냅샷·전환의 출발점은 항상 이것이다.
        private EnvironmentProfile baseState;
        // 직전 보간에서 출발·도착 중 안개가 있었는가 — 필드 안개 모드(지수제곱)를 계속 쓸지 정한다.
        private bool baseFogUsed;

        // ── 낮·밤·날씨 하늘 ──
        /// <summary>하늘을 갱신하는 간격(초). 전환 중에는 매 프레임이다 — 게임 1시간이 30초라 0.1초 단위면 눈에 안 띈다.</summary>
        private const float SkyTickSeconds = 0.1f;
        /// <summary>하늘 보정이 걸리고 풀리는 데 드는 시간(초) — 전환 보간(<c>transitionSpeed</c> 2)과 같다.</summary>
        private const float SkyFadeSeconds = 0.5f;
        /// <summary><see cref="WorldStateProvider"/>를 못 찾았을 때 다시 찾기까지의 간격(초).</summary>
        private const float WorldLookupSeconds = 2f;

        private WorldSkyRules.SkyFrame skyFrame;
        private float skyBlend;                 // 0이면 기본 상태 그대로, 1이면 하늘 보정 전부
        private bool skyBlendReady;
        private float skyClock;
        private float skyLastTime;
        private float lastWrittenBlend = -1f;
        private float worldLookupClock;
        private WorldSkyVisuals skyVisuals;

        private bool initialized;

        // 메인 필드에서 따라갈 리전 대기(RegionAtmosphere). 서브에리어 안에선 기록만 하고 적용하지 않는다.
        private RegionData currentRegion;

        public void AutoWire(RegionManager rm)
        {
            if (regionManager != null)
            {
                regionManager.SubAreaChanged -= OnSubAreaChanged;
                regionManager.RegionChanged -= OnRegionChanged;
            }
            regionManager = rm;
            if (regionManager != null)
            {
                regionManager.SubAreaChanged += OnSubAreaChanged;
                regionManager.RegionChanged += OnRegionChanged;
            }
        }

        /// <summary>
        /// 시계·날씨 공급자를 주입한다. 안 주입해도 <c>FindFirstObjectByType</c>로 찾지만(2초 간격 재시도),
        /// 못 찾으면 하늘은 맑은 정오로 남는다 — 기존 한낮 모습 그대로다.
        /// </summary>
        public void AutoWire(WorldStateProvider provider)
        {
            if (provider != null) worldState = provider;
        }

        private void OnEnable()
        {
            if (regionManager == null) return;
            regionManager.SubAreaChanged -= OnSubAreaChanged;
            regionManager.SubAreaChanged += OnSubAreaChanged;
            regionManager.RegionChanged -= OnRegionChanged;
            regionManager.RegionChanged += OnRegionChanged;
            currentRegion = regionManager.CurrentRegion;
            OnSubAreaChanged(regionManager.CurrentSubArea);
        }

        private void OnDisable()
        {
            if (regionManager != null)
            {
                regionManager.SubAreaChanged -= OnSubAreaChanged;
                regionManager.RegionChanged -= OnRegionChanged;
            }
        }

        /// <summary>
        /// 리전이 바뀌면 그 리전의 대기로 옮겨 간다. 서브에리어 안(좌표가 2000대라 리전 판정이 멈춘다)에서는
        /// 기록만 해 두고, 나올 때 <see cref="OnSubAreaChanged"/>(null)가 그 리전 값으로 복귀한다.
        /// </summary>
        private void OnRegionChanged(RegionData region)
        {
            currentRegion = region;
            if (!initialized) CaptureDefaults();
            if (regionManager != null && regionManager.CurrentSubArea != null) return;
            currentState = SnapshotCurrent();
            targetProfile = BuildRegionProfile(currentRegion);
            transitionProgress = 0f;
        }

        private void Start()
        {
            CaptureDefaults();
        }

        private void CaptureDefaults()
        {
            if (initialized) return;

            directionalLight = RenderSettings.sun;
            if (directionalLight == null)
            {
                foreach (Light candidate in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (candidate.type != LightType.Directional) continue;
                    directionalLight = candidate;
                    break;
                }
            }
            mainCamera = Camera.main;

            if (directionalLight != null)
            {
                defaultLightColor = directionalLight.color;
                defaultLightIntensity = directionalLight.intensity;
                defaultLightRotation = directionalLight.transform.rotation;
                defaultShadowStrength = directionalLight.shadowStrength;
            }

            defaultAmbient = RenderSettings.ambientLight;
            defaultAmbientEquator = RenderSettings.ambientEquatorColor;
            defaultAmbientGround = RenderSettings.ambientGroundColor;
            defaultAmbientMode = RenderSettings.ambientMode;
            defaultFogEnabled = RenderSettings.fog;
            defaultFogColor = RenderSettings.fogColor;
            defaultFogDensity = RenderSettings.fogDensity;
            defaultFogMode = RenderSettings.fogMode;

            if (mainCamera != null)
            {
                defaultCameraBg = mainCamera.backgroundColor;
                defaultClearFlags = mainCamera.clearFlags;
            }

            currentState = BuildDefaultProfile();
            baseState = currentState;
            targetProfile = currentState;

            // 스카이박스 복제본 — 낮밤·날씨가 노출을 바꾼다. 원본 에셋은 건드리지 않고 OnDestroy가 되돌린다.
            skyVisuals = new WorldSkyVisuals();
            skyVisuals.CreateSkybox();
            initialized = true;
        }

        private void OnDestroy()
        {
            if (skyVisuals != null)
            {
                skyVisuals.Dispose();
                skyVisuals = null;
            }
        }

        private void Update()
        {
            if (!initialized) return;

            bool transitioning = transitionProgress < 1f;
            if (transitioning)
            {
                transitionProgress = Mathf.Clamp01(transitionProgress + Time.deltaTime * transitionSpeed);
                float t = Mathf.SmoothStep(0f, 1f, transitionProgress);
                BlendBase(currentState, targetProfile, t);
            }

            // 하늘 — 전환 중에는 매 프레임(기본 상태가 움직인다), 아니면 0.1초마다
            skyClock -= Time.unscaledDeltaTime;
            if (!transitioning && skyClock > 0f) return;
            skyClock = SkyTickSeconds;
            RefreshSky(transitioning);
        }

        private void LateUpdate()
        {
            // 별·연무 돔이 카메라를 따라다닌다(켜져 있을 때만 일한다)
            if (skyVisuals != null) skyVisuals.Follow(mainCamera);
        }

        // ── 낮·밤·날씨 하늘 ──

        /// <summary>하늘 보정이 걸리는 곳인가 — 메인 필드·나의 섬이고 꿈이 아닐 때.</summary>
        private bool SkyApplies()
        {
            SubAreaData sub = regionManager != null ? regionManager.CurrentSubArea : null;
            return WorldSkyRules.Applies(sub != null, sub != null ? sub.subAreaId : null, DreamPrologueState.Active);
        }

        /// <summary>
        /// 날씨를 읽을 리전 — 메인 필드의 현재 리전. 섬·서브에리어에는 지역이 없으니 null(세계 날씨 그대로)이다.
        /// <c>CurrentRegion</c>은 서브에리어 안에서도 부모 리전을 가리키므로 서브에리어를 먼저 거른다.
        /// </summary>
        private string WeatherRegionId()
        {
            if (regionManager == null || regionManager.CurrentSubArea != null) return null;
            return regionManager.CurrentRegion != null ? regionManager.CurrentRegion.regionId : null;
        }

        private WorldSkyRules.SkyFrame ComputeSkyFrame()
        {
            float hour = WorldSkyRules.NoonHour;
            WorldSkyRules.WeatherMix mix = WorldSkyRules.WeatherMix.Of(WeatherType.Clear);

            if (worldState == null && Time.unscaledTime >= worldLookupClock)
            {
                worldState = FindFirstObjectByType<WorldStateProvider>();
                worldLookupClock = Time.unscaledTime + WorldLookupSeconds;
            }
            if (worldState != null)
            {
                GameClock clock = worldState.Clock;
                if (clock != null) hour = clock.GetHourFloat();
                mix = WorldSkyRules.WeatherMix.From(worldState.Weather, WeatherRegionId());
            }
            return WorldSkyRules.Evaluate(hour, mix);
        }

        /// <summary>
        /// 하늘 한 틱 — 걸림 비율을 목표로 옮기고 하늘 프레임을 새로 평가해 렌더 설정에 쓴다.
        /// 걸릴 일이 없는 곳(동굴·방)에서는 비율이 0에 닿은 뒤 아무것도 쓰지 않는다.
        /// </summary>
        private void RefreshSky(bool force)
        {
            float now = Time.unscaledTime;
            float dt = skyLastTime > 0f ? Mathf.Clamp(now - skyLastTime, 0f, 0.5f) : 0f;
            skyLastTime = now;

            bool applies = SkyApplies();
            float target = applies ? 1f : 0f;
            if (!skyBlendReady)
            {
                // 첫 틱은 서서히 걸지 않는다 — 켜자마자 기본 한낮에서 밤으로 페이드되는 걸 막는다
                skyBlend = target;
                skyBlendReady = true;
            }
            else
            {
                skyBlend = Mathf.MoveTowards(skyBlend, target, dt / SkyFadeSeconds);
            }

            if (applies || skyBlend > 0f) skyFrame = ComputeSkyFrame();

            if (!force && !applies && skyBlend <= 0f && lastWrittenBlend <= 0f) return;
            lastWrittenBlend = skyBlend;
            WriteFinal();
        }

        private WorldSkyRules.LightingState ToState(EnvironmentProfile p, bool flatAmbient)
        {
            return new WorldSkyRules.LightingState
            {
                lightColor = p.lightColor,
                lightIntensity = p.lightIntensity,
                lightRotation = p.lightRotation,
                shadowStrength = defaultShadowStrength,
                ambientSky = p.ambientColor,
                // 평면 환경광(서브에리어)은 한 색이 사방을 비춘다. 필드(Trilight)는 옆·아래가 부트스트랩이 정한 색이다.
                ambientEquator = flatAmbient ? p.ambientColor : defaultAmbientEquator,
                ambientGround = flatAmbient ? p.ambientColor : defaultAmbientGround,
                fogEnabled = p.fogEnabled,
                fogColor = p.fogColor,
                fogDensity = p.fogDensity,
                background = p.cameraBg,
            };
        }

        /// <summary>
        /// 기본 상태에 하늘을 얹은 최종 값을 렌더 설정에 쓴다 — 이 파일에서 RenderSettings·라이트·카메라 배경에 쓰는 유일한 곳이다.
        /// 걸림 비율이 1 미만이면 기본 상태와 보정 결과를 섞는다(걸리고 풀리는 경계의 페이드).
        /// </summary>
        private void WriteFinal()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            bool inSubArea = regionManager != null && regionManager.CurrentSubArea != null;

            WorldSkyRules.LightingState b = ToState(baseState, inSubArea);
            WorldSkyRules.LightingState f = b;
            if (skyBlend > 0.001f)
            {
                WorldSkyRules.LightingState m = WorldSkyRules.Apply(b, skyFrame, !inSubArea);
                f = skyBlend >= 0.999f ? m : WorldSkyRules.LightingState.Lerp(b, m, skyBlend);
            }

            if (directionalLight != null)
            {
                directionalLight.color = f.lightColor;
                directionalLight.intensity = f.lightIntensity;
                directionalLight.transform.rotation = f.lightRotation;
                directionalLight.shadowStrength = f.shadowStrength;
            }

            // 평면(Flat) 환경광은 ambientLight, Trilight의 위쪽 색은 ambientSkyColor다 — 둘 다 같은 값을 적어 모드가 어느 쪽이든 맞는다.
            // 옆·아래 색은 Trilight(메인 필드)에서만 쓰이고 서브에리어(평면)에선 건드리지 않는다.
            RenderSettings.ambientLight = f.ambientSky;
            RenderSettings.ambientSkyColor = f.ambientSky;
            if (RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Flat)
            {
                RenderSettings.ambientEquatorColor = f.ambientEquator;
                RenderSettings.ambientGroundColor = f.ambientGround;
            }

            // **안개는 런타임에만 켠다** — 빌드 씬은 둘 다 m_Fog: 0이라, GraphicsSettings의 Fog Modes가
            // Automatic이면 기기 빌드에서 FOG_EXP/EXP2 셰이더 변형이 빠져 에디터에서만 보인다.
            // 그래서 Custom(Exp·Exp2 유지)으로 둔다 — Automatic으로 되돌리지 말 것.
            RenderSettings.fog = f.fogEnabled;
            RenderSettings.fogColor = f.fogColor;
            RenderSettings.fogDensity = f.fogEnabled ? f.fogDensity : 0f;
            // 서브에리어는 좁은 방이라 지수(Exp), 메인 필드의 리전 연무는 원경만 흐리는 지수제곱(Exp2).
            // 모드는 RegionAtmosphere의 상수로만 적는다 — 가시거리 검사(FieldThemeTests)가 같은 상수로 투과율을 계산하므로
            // 여기서 모드를 바꾸면 검사 계산도 함께 바뀐다(리터럴을 쓰면 FieldFog_RuntimeUsesTheSharedModeConstant가 잡는다).
            // 안개 날씨의 밀도 상한(RegionAtmosphere.MaxWeatherFogDensity*)도 같은 상수·같은 식의 검사가 지킨다.
            RenderSettings.fogMode = inSubArea ? RegionAtmosphere.SubAreaFogMode
                : (f.fogEnabled || baseFogUsed) ? RegionAtmosphere.FieldFogMode : defaultFogMode;

            if (mainCamera != null)
                mainCamera.backgroundColor = f.background;

            // 흐린 날의 연무 막은 이번에 쓴 안개색을 그대로 칠한다 — 원경 산을 덮는 안개와 같은 색이어야 지평선이 녹는다.
            // 섬(SolidColor 배경)도 같다. 동굴·꿈은 skyBlend가 0이라 막이 꺼진다.
            if (skyVisuals != null) skyVisuals.Apply(skyFrame, skyBlend, mainCamera, f.fogColor);
        }

        private void OnSubAreaChanged(SubAreaData subArea)
        {
            if (!initialized) CaptureDefaults();

            EnvironmentProfile profile;
            if (subArea == null)
                profile = BuildRegionProfile(currentRegion);
            else
                profile = GetProfileForSubArea(subArea);

            // 서브지역에선 Flat 환경광으로 전환(밝힌 ambientColor가 실제 적용됨), 메인 복귀 시 원래 모드로.
            RenderSettings.ambientMode = subArea != null
                ? UnityEngine.Rendering.AmbientMode.Flat
                : defaultAmbientMode;

            // 같은 이유로 클리어 플래그도 전환한다. 부트스트랩이 카메라를 Skybox로 고정하는데
            // 그 모드에선 Unity가 backgroundColor를 무시하므로, 프로필의 cameraBg가 아무리
            // 어두워도 하늘색이 그대로 보였다. 동굴 말고는 천장이 없어 하늘이 노출되고,
            // 내장 fog는 skybox에 적용되지 않아 fog로도 가릴 수 없었다.
            if (mainCamera != null)
            {
                mainCamera.clearFlags = subArea != null
                    ? CameraClearFlags.SolidColor
                    : defaultClearFlags;
            }

            // 현재 렌더 상태를 스냅샷으로 캡처
            currentState = SnapshotCurrent();
            targetProfile = profile;
            transitionProgress = 0f;
        }

        /// <summary>
        /// 출발→도착 보간으로 <b>기본 상태</b>(<see cref="baseState"/>)를 갱신한다 — 렌더 설정엔 쓰지 않는다(<see cref="WriteFinal"/>이 쓴다).
        /// </summary>
        private void BlendBase(EnvironmentProfile from, EnvironmentProfile to, float t)
        {
            EnvironmentProfile p = baseState;
            p.lightColor = Color.Lerp(from.lightColor, to.lightColor, t);
            p.lightIntensity = Mathf.Lerp(from.lightIntensity, to.lightIntensity, t);
            p.lightRotation = Quaternion.Slerp(from.lightRotation, to.lightRotation, t);
            p.ambientColor = Color.Lerp(from.ambientColor, to.ambientColor, t);
            // 페이드 도중: from이 fog이고 t<1일 때만 유지. to가 fog면 항상 ON. 둘 다 off이면 즉시 false.
            p.fogEnabled = (from.fogEnabled && t < 1f) || to.fogEnabled;
            p.fogColor = Color.Lerp(from.fogColor, to.fogColor, t);
            // to가 fog 없으면 fogDensity를 0으로 보간 (잔여 안개 제거)
            float targetDensity = to.fogEnabled ? to.fogDensity : 0f;
            float sourceDensity = from.fogEnabled ? from.fogDensity : 0f;
            p.fogDensity = Mathf.Lerp(sourceDensity, targetDensity, t);
            p.cameraBg = Color.Lerp(from.cameraBg, to.cameraBg, t);

            // 안개 해제: 전환 완료 + 대상이 안개 없음이면 끔
            if (t >= 1f && !to.fogEnabled)
                p.fogEnabled = false;

            baseFogUsed = to.fogEnabled || from.fogEnabled;
            baseState = p;
        }

        /// <summary>
        /// 전환의 출발점. <b>렌더 설정이 아니라 기본 상태에서 뜬다</b> — 렌더 설정에는 하늘 보정이 이미 얹혀 있어서
        /// 거기서 읽으면 밤색·날씨색이 기본으로 굳는다(<see cref="WriteFinal"/>이 매번 기본 상태 위에 다시 얹는다).
        /// </summary>
        private EnvironmentProfile SnapshotCurrent()
        {
            return baseState;
        }

        private EnvironmentProfile BuildDefaultProfile()
        {
            return new EnvironmentProfile
            {
                lightColor = defaultLightColor,
                lightIntensity = defaultLightIntensity,
                lightRotation = defaultLightRotation,
                ambientColor = defaultAmbient,
                fogEnabled = defaultFogEnabled,
                fogColor = defaultFogColor,
                fogDensity = defaultFogDensity,
                cameraBg = defaultCameraBg
            };
        }

        /// <summary>
        /// 메인 필드 기본값 위에 리전 대기(<see cref="RegionAtmosphere"/>)를 얹는다. 리전 밖(길 위)이거나
        /// 표에 없는 리전이면 기본값 그대로다. 햇빛 방향은 바꾸지 않는다 — 그림자 방향이 리전 경계에서
        /// 돌아가면 어색하다.
        /// </summary>
        private EnvironmentProfile BuildRegionProfile(RegionData region)
        {
            EnvironmentProfile p = BuildDefaultProfile();
            if (region == null || !RegionAtmosphere.TryGet(region.regionId, out RegionAtmosphere.Profile a)) return p;
            p.lightColor = a.light;
            p.lightIntensity = defaultLightIntensity * a.intensity;
            p.ambientColor = a.ambientSky;
            p.fogEnabled = true;
            p.fogColor = a.fog;
            p.fogDensity = Mathf.Min(a.fogDensity, RegionAtmosphere.MaxFogDensity);
            return p;
        }

        /// <summary>
        /// 같은 environmentType을 쓰는 방이라도 빛이 달라야 하는 곳 — 지오메트리를 subAreaId로 가르는
        /// <c>SubAreaWorldBuilder.Themes</c>와 짝이다. 개미귀신 구덩이는 하늘이 열린 사막(동굴 빛이면 안 된다),
        /// 가장 높은 가지는 나무 꼭대기의 햇빛, 빈칸은 창백한 여백. 없으면 타입 프로필로 떨어진다.
        /// </summary>
        private EnvironmentProfile GetProfileForSubArea(SubAreaData subArea)
        {
            switch (subArea.subAreaId)
            {
                // 나의 섬 — 탁 트인 바다 위의 한낮. 카메라 배경이 곧 하늘이고(서브에리어는 SolidColor로 그린다)
                // 안개색을 그 하늘과 맞춰 먼바다가 수평선으로 녹아들게 한다.
                case GameConstants.Island.SubAreaId:
                    // 환경광을 낮게 둔다 — 서브에리어는 평면(Flat) 환경광이라 그 값이 그대로 색에 더해진다.
                    // 0.6대로 두면 햇빛(1.1 × 0.79)과 합쳐 물건 색의 1.6배가 나와 잔디·모래가 파스텔로 날아간다.
                    return Profile(new Color(1f, 0.97f, 0.88f), 1.1f, Quaternion.Euler(52f, 35f, 0f), new Color(0.40f, 0.44f, 0.50f),
                        new Color(0.66f, 0.84f, 0.95f), 0.006f, new Color(0.56f, 0.80f, 0.96f));
                case "dunes_pit":
                    return Profile(new Color(1f, 0.93f, 0.78f), 1.25f, Quaternion.Euler(55f, 30f, 0f), new Color(0.55f, 0.50f, 0.42f),
                        new Color(0.86f, 0.78f, 0.62f), 0.012f, new Color(0.72f, 0.64f, 0.50f));
                case "canopy_crown":
                    return Profile(new Color(1f, 0.98f, 0.86f), 1.25f, Quaternion.Euler(50f, 40f, 0f), new Color(0.46f, 0.56f, 0.46f),
                        new Color(0.70f, 0.84f, 0.78f), 0.012f, new Color(0.58f, 0.76f, 0.86f));
                case "nameless_core":
                    return Profile(new Color(0.92f, 0.90f, 1f), 0.9f, Quaternion.Euler(85f, 0f, 0f), new Color(0.52f, 0.50f, 0.58f),
                        new Color(0.80f, 0.78f, 0.86f), 0.03f, new Color(0.86f, 0.84f, 0.90f));
                case "frostline_ridge":
                    return Profile(new Color(0.88f, 0.94f, 1f), 1.3f, Quaternion.Euler(35f, 30f, 0f), new Color(0.52f, 0.58f, 0.68f),
                        new Color(0.84f, 0.90f, 0.96f), 0.02f, new Color(0.72f, 0.80f, 0.90f));
                case "hollow_silence":
                    return Profile(new Color(0.9f, 0.9f, 0.88f), 0.9f, Quaternion.Euler(60f, 30f, 0f), new Color(0.50f, 0.50f, 0.48f),
                        new Color(0.74f, 0.74f, 0.72f), 0.035f, new Color(0.60f, 0.60f, 0.58f));
                case "emberfall_vent":
                    return Profile(new Color(1f, 0.55f, 0.3f), 0.8f, Quaternion.Euler(80f, 0f, 0f), new Color(0.40f, 0.24f, 0.18f),
                        new Color(0.22f, 0.11f, 0.07f), 0.035f, new Color(0.08f, 0.04f, 0.03f));
                case "mountain_cave":
                    return Profile(new Color(0.75f, 0.8f, 0.95f), 0.9f, Quaternion.Euler(80f, 30f, 0f), new Color(0.42f, 0.44f, 0.52f),
                        new Color(0.16f, 0.18f, 0.24f), 0.02f, new Color(0.06f, 0.07f, 0.10f));
                case "forest_cave":
                    return Profile(new Color(0.7f, 0.85f, 0.6f), 0.9f, Quaternion.Euler(80f, 30f, 0f), new Color(0.36f, 0.44f, 0.32f),
                        new Color(0.14f, 0.20f, 0.12f), 0.022f, new Color(0.05f, 0.08f, 0.04f));
                case "swamp_cave":
                    return Profile(new Color(0.62f, 0.75f, 0.6f), 0.85f, Quaternion.Euler(80f, 30f, 0f), new Color(0.34f, 0.40f, 0.32f),
                        new Color(0.12f, 0.16f, 0.12f), 0.025f, new Color(0.04f, 0.06f, 0.04f));
                case "hollow_burrow":
                    return Profile(new Color(0.8f, 0.76f, 0.66f), 0.9f, Quaternion.Euler(80f, 30f, 0f), new Color(0.46f, 0.43f, 0.38f),
                        new Color(0.28f, 0.25f, 0.21f), 0.022f, new Color(0.10f, 0.09f, 0.07f));
                case "canopy_bough":
                    return Profile(new Color(0.85f, 1f, 0.7f), 1.0f, Quaternion.Euler(70f, 45f, 0f), new Color(0.40f, 0.50f, 0.34f),
                        new Color(0.30f, 0.44f, 0.26f), 0.02f, new Color(0.16f, 0.26f, 0.12f));
                default:
                    return GetProfileForType(subArea.environmentType);
            }
        }

        private static EnvironmentProfile Profile(Color light, float intensity, Quaternion rotation, Color ambient,
            Color fog, float fogDensity, Color cameraBg)
        {
            return new EnvironmentProfile
            {
                lightColor = light,
                lightIntensity = intensity,
                lightRotation = rotation,
                ambientColor = ambient,
                fogEnabled = true,
                fogColor = fog,
                fogDensity = fogDensity,
                cameraBg = cameraBg,
            };
        }

        private EnvironmentProfile GetProfileForType(string envType)
        {
            switch (envType)
            {
                // 아래 프리셋은 무드(색조)는 유지하되 밝기 floor를 올리고 fog를 완화해
                // "너무 어두워 안 보임"을 개선. ambientMode=Flat 전환과 함께 ambientColor가 실제 적용됨.
                case "cave":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.7f, 0.62f, 0.5f),
                        lightIntensity = 0.95f,
                        lightRotation = Quaternion.Euler(80f, 30f, 0f),
                        ambientColor = new Color(0.5f, 0.44f, 0.37f),
                        fogEnabled = true,
                        fogColor = new Color(0.24f, 0.2f, 0.16f),
                        fogDensity = 0.018f,
                        cameraBg = new Color(0.1f, 0.08f, 0.06f)
                    };

                case "deep_forest":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.6f, 0.8f, 0.45f),
                        lightIntensity = 0.8f,
                        lightRotation = Quaternion.Euler(70f, 45f, 0f),
                        ambientColor = new Color(0.28f, 0.36f, 0.2f),
                        fogEnabled = true,
                        fogColor = new Color(0.2f, 0.3f, 0.14f),
                        fogDensity = 0.026f,
                        cameraBg = new Color(0.08f, 0.12f, 0.05f)
                    };

                case "underwater":
                    // fogDensity 0.05 → 0.032: 옛 0.05는 카메라~캐릭터 거리(~10.8m)에서 e^(-0.54)=58%만
                    // 투과해 캐릭터가 파랗게 묻힘. 0.032면 ~70% 투과로 캐릭터 선명 + 원경 벽은 여전히 안개.
                    // 빛/앰비언트도 상향해 수중 캐릭터 가시성 확보(무드는 파란 색조로 유지).
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.5f, 0.72f, 0.95f),
                        lightIntensity = 0.95f,
                        lightRotation = Quaternion.Euler(85f, 0f, 0f),
                        ambientColor = new Color(0.32f, 0.44f, 0.56f),
                        fogEnabled = true,
                        fogColor = new Color(0.18f, 0.36f, 0.54f),
                        fogDensity = 0.032f,
                        cameraBg = new Color(0.1f, 0.2f, 0.34f)
                    };

                case "pond":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.78f, 0.9f, 1f),
                        lightIntensity = 0.95f,
                        lightRotation = Quaternion.Euler(55f, 20f, 0f),
                        ambientColor = new Color(0.34f, 0.44f, 0.5f),
                        fogEnabled = true,
                        fogColor = new Color(0.55f, 0.65f, 0.74f),
                        fogDensity = 0.012f,
                        cameraBg = new Color(0.2f, 0.3f, 0.4f)
                    };

                case "fog":
                    // fogDensity 0.06 → 0.04: 옛 0.06은 캐릭터(~10.8m)에서 e^(-0.648)=52%만 투과해
                    // 캐릭터가 안개에 반쯤 사라짐. 0.04면 ~65% 투과로 캐릭터 식별 가능 + 안개 무드는 유지.
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.74f, 0.72f, 0.68f),
                        lightIntensity = 0.85f,
                        lightRotation = Quaternion.Euler(60f, 30f, 0f),
                        ambientColor = new Color(0.42f, 0.42f, 0.38f),
                        fogEnabled = true,
                        fogColor = new Color(0.64f, 0.62f, 0.57f),
                        fogDensity = 0.04f,
                        cameraBg = new Color(0.42f, 0.4f, 0.37f)
                    };

                case "reeds":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.9f, 0.85f, 0.6f),
                        lightIntensity = 1.0f,
                        lightRotation = Quaternion.Euler(45f, 60f, 0f),
                        ambientColor = new Color(0.38f, 0.42f, 0.26f),
                        fogEnabled = true,
                        fogColor = new Color(0.58f, 0.6f, 0.44f),
                        fogDensity = 0.016f,
                        cameraBg = new Color(0.24f, 0.26f, 0.15f)
                    };

                case "peak":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.95f, 0.95f, 1f),
                        lightIntensity = 1.5f,
                        lightRotation = Quaternion.Euler(35f, 30f, 0f),
                        ambientColor = new Color(0.5f, 0.52f, 0.6f),
                        fogEnabled = true,
                        fogColor = new Color(0.82f, 0.87f, 0.96f),
                        fogDensity = 0.016f,
                        cameraBg = new Color(0.55f, 0.6f, 0.72f)
                    };

                case "flower_maze":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(1f, 0.9f, 0.78f),
                        lightIntensity = 1.15f,
                        lightRotation = Quaternion.Euler(45f, 50f, 0f),
                        ambientColor = new Color(0.46f, 0.36f, 0.42f),
                        fogEnabled = true,
                        fogColor = new Color(0.86f, 0.72f, 0.77f),
                        fogDensity = 0.02f,
                        cameraBg = new Color(0.34f, 0.24f, 0.29f)
                    };

                case "greenhouse":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.88f, 1f, 0.82f),
                        lightIntensity = 1.05f,
                        lightRotation = Quaternion.Euler(50f, 30f, 0f),
                        ambientColor = new Color(0.4f, 0.5f, 0.34f),
                        fogEnabled = false,
                        fogColor = defaultFogColor,
                        fogDensity = 0f,
                        cameraBg = new Color(0.18f, 0.26f, 0.15f)
                    };

                case "temple":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.78f, 0.68f, 0.95f),
                        lightIntensity = 0.72f,
                        lightRotation = Quaternion.Euler(75f, 10f, 0f),
                        ambientColor = new Color(0.3f, 0.24f, 0.42f),
                        fogEnabled = true,
                        fogColor = new Color(0.3f, 0.24f, 0.42f),
                        fogDensity = 0.03f,
                        cameraBg = new Color(0.12f, 0.09f, 0.2f)
                    };

                case "underground":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.6f, 0.52f, 0.42f),
                        lightIntensity = 0.82f,
                        lightRotation = Quaternion.Euler(85f, 0f, 0f),
                        ambientColor = new Color(0.44f, 0.38f, 0.32f),
                        fogEnabled = true,
                        fogColor = new Color(0.2f, 0.17f, 0.14f),
                        fogDensity = 0.03f,
                        cameraBg = new Color(0.08f, 0.07f, 0.06f)
                    };

                // ── 2막(ver2) 전용 4종 ──
                // SubAreaWorldBuilder에는 이 넷의 지오메트리를 넣고 **여기 조명 프로필을 빠뜨려**
                // 전부 default(야외 주광)로 떨어졌다. 밀폐 공간을 지어 놓고 바깥 햇빛을 쬐는 꼴이라
                // 지오메트리만 보고는 티가 안 난다 — environmentType을 늘리면 이 switch도 함께 늘린다.
                case "vault":   // 명부회 창고 — 곤충 상자가 쌓인 실내. 차가운 작업 조명.
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.72f, 0.68f, 0.58f),
                        lightIntensity = 0.7f,
                        lightRotation = Quaternion.Euler(80f, 15f, 0f),
                        ambientColor = new Color(0.34f, 0.31f, 0.26f),
                        fogEnabled = true,
                        fogColor = new Color(0.16f, 0.14f, 0.11f),
                        fogDensity = 0.035f,
                        cameraBg = new Color(0.07f, 0.06f, 0.05f)
                    };

                case "archive": // 빙하 서고 — 얼음을 통과한 푸른 빛.
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.62f, 0.78f, 0.92f),
                        lightIntensity = 0.85f,
                        lightRotation = Quaternion.Euler(70f, 200f, 0f),
                        ambientColor = new Color(0.38f, 0.48f, 0.58f),
                        fogEnabled = true,
                        fogColor = new Color(0.30f, 0.40f, 0.50f),
                        fogDensity = 0.028f,
                        cameraBg = new Color(0.12f, 0.18f, 0.24f)
                    };

                case "kiln":    // 잿불 가마 — 아래에서 올라오는 용암 빛.
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(1f, 0.55f, 0.28f),
                        lightIntensity = 0.78f,
                        lightRotation = Quaternion.Euler(60f, 30f, 0f),
                        ambientColor = new Color(0.42f, 0.24f, 0.16f),
                        fogEnabled = true,
                        fogColor = new Color(0.24f, 0.12f, 0.08f),
                        fogDensity = 0.042f,
                        cameraBg = new Color(0.10f, 0.05f, 0.04f)
                    };

                case "ledger":  // 장부의 방 — 최종장. 빛이 거의 없고 바닥의 이름만 희미하다.
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.78f, 0.74f, 0.62f),
                        lightIntensity = 0.55f,
                        lightRotation = Quaternion.Euler(88f, 0f, 0f),
                        ambientColor = new Color(0.22f, 0.21f, 0.24f),
                        fogEnabled = true,
                        fogColor = new Color(0.10f, 0.09f, 0.12f),
                        fogDensity = 0.05f,
                        cameraBg = new Color(0.04f, 0.04f, 0.06f)
                    };

                default:
                    return BuildDefaultProfile();
            }
        }

        private struct EnvironmentProfile
        {
            public Color lightColor;
            public float lightIntensity;
            public Quaternion lightRotation;
            public Color ambientColor;
            public bool fogEnabled;
            public Color fogColor;
            public float fogDensity;
            public Color cameraBg;
        }
    }
}
