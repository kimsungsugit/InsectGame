using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    public class SubAreaEnvironment : MonoBehaviour
    {
        [SerializeField] private RegionManager regionManager;

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

        // 전환 상태
        private EnvironmentProfile targetProfile;
        private EnvironmentProfile currentState;
        private float transitionProgress = 1f;
        private float transitionSpeed = 2f;

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
            }

            defaultAmbient = RenderSettings.ambientLight;
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
            targetProfile = currentState;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized) return;
            if (transitionProgress >= 1f) return;

            transitionProgress = Mathf.Clamp01(transitionProgress + Time.deltaTime * transitionSpeed);
            float t = Mathf.SmoothStep(0f, 1f, transitionProgress);
            ApplyLerp(currentState, targetProfile, t);
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

        private void ApplyLerp(EnvironmentProfile from, EnvironmentProfile to, float t)
        {
            if (directionalLight != null)
            {
                directionalLight.color = Color.Lerp(from.lightColor, to.lightColor, t);
                directionalLight.intensity = Mathf.Lerp(from.lightIntensity, to.lightIntensity, t);
                directionalLight.transform.rotation = Quaternion.Slerp(from.lightRotation, to.lightRotation, t);
            }

            RenderSettings.ambientLight = Color.Lerp(from.ambientColor, to.ambientColor, t);
            // 페이드 도중: from이 fog이고 t<1일 때만 유지. to가 fog면 항상 ON. 둘 다 off이면 즉시 false.
            // **안개는 런타임에만 켠다** — 빌드 씬은 둘 다 m_Fog: 0이라, GraphicsSettings의 Fog Modes가
            // Automatic이면 기기 빌드에서 FOG_EXP/EXP2 셰이더 변형이 빠져 에디터에서만 보인다.
            // 그래서 Custom(Exp·Exp2 유지)으로 둔다 — Automatic으로 되돌리지 말 것.
            RenderSettings.fog = (from.fogEnabled && t < 1f) || to.fogEnabled;
            RenderSettings.fogColor = Color.Lerp(from.fogColor, to.fogColor, t);
            // to가 fog 없으면 fogDensity를 0으로 보간 (잔여 안개 제거)
            float targetDensity = to.fogEnabled ? to.fogDensity : 0f;
            float sourceDensity = from.fogEnabled ? from.fogDensity : 0f;
            RenderSettings.fogDensity = Mathf.Lerp(sourceDensity, targetDensity, t);
            // 서브에리어는 좁은 방이라 지수(Exp), 메인 필드의 리전 연무는 원경만 흐리는 지수제곱(Exp2).
            // 모드는 RegionAtmosphere의 상수로만 적는다 — 가시거리 검사(FieldThemeTests)가 같은 상수로 투과율을 계산하므로
            // 여기서 모드를 바꾸면 검사 계산도 함께 바뀐다(리터럴을 쓰면 FieldFog_RuntimeUsesTheSharedModeConstant가 잡는다).
            bool inSubArea = regionManager != null && regionManager.CurrentSubArea != null;
            RenderSettings.fogMode = inSubArea ? RegionAtmosphere.SubAreaFogMode
                : (to.fogEnabled || from.fogEnabled) ? RegionAtmosphere.FieldFogMode : defaultFogMode;

            if (mainCamera != null)
                mainCamera.backgroundColor = Color.Lerp(from.cameraBg, to.cameraBg, t);

            // 안개 해제: 전환 완료 + 대상이 안개 없음이면 끔
            if (t >= 1f && !to.fogEnabled)
                RenderSettings.fog = false;
        }

        private EnvironmentProfile SnapshotCurrent()
        {
            var p = new EnvironmentProfile();
            if (directionalLight != null)
            {
                p.lightColor = directionalLight.color;
                p.lightIntensity = directionalLight.intensity;
                p.lightRotation = directionalLight.transform.rotation;
            }
            p.ambientColor = RenderSettings.ambientLight;
            p.fogEnabled = RenderSettings.fog;
            p.fogColor = RenderSettings.fogColor;
            p.fogDensity = RenderSettings.fogDensity;
            p.cameraBg = mainCamera != null ? mainCamera.backgroundColor : Color.black;
            return p;
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
