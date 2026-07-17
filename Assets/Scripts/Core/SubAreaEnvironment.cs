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
        // 하늘색 폴백 — Camera.main이 없어 캡처를 못 했을 때만 쓰인다(형제 필드와 동일 패턴).
        private Color defaultCameraBg = new Color(0.5f, 0.8f, 1f);
        // 메인 필드의 원래 카메라 클리어 플래그(보통 Skybox). 서브지역에선 SolidColor로 바꿔야
        // backgroundColor가 실제로 렌더된다 — Skybox 모드에서 Unity는 backgroundColor를 무시한다.
        private CameraClearFlags defaultClearFlags = CameraClearFlags.Skybox;
        // 메인 필드의 원래 환경광 모드(보통 Skybox). 서브지역에선 Flat로 바꿔 ambientColor가 실제 적용되게 하고,
        // 빠져나올 때 이 값으로 복원한다. (Skybox 모드에선 ambientColor가 무시돼 서브지역을 밝힐 수 없었음)
        private UnityEngine.Rendering.AmbientMode defaultAmbientMode = UnityEngine.Rendering.AmbientMode.Skybox;

        // 전환 상태
        private EnvironmentProfile targetProfile;
        private EnvironmentProfile currentState;
        private float transitionProgress = 1f;
        private float transitionSpeed = 2f;

        private bool initialized;

        public void AutoWire(RegionManager rm)
        {
            if (regionManager != null)
                regionManager.SubAreaChanged -= OnSubAreaChanged;
            regionManager = rm;
            if (regionManager != null)
                regionManager.SubAreaChanged += OnSubAreaChanged;
        }

        private void OnDisable()
        {
            if (regionManager != null)
                regionManager.SubAreaChanged -= OnSubAreaChanged;
        }

        private void Start()
        {
            CaptureDefaults();
        }

        private void CaptureDefaults()
        {
            if (initialized) return;

            directionalLight = FindFirstObjectByType<Light>();
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
                profile = BuildDefaultProfile();
            else
                profile = GetProfileForType(subArea.environmentType);

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
            RenderSettings.fog = (from.fogEnabled && t < 1f) || to.fogEnabled;
            RenderSettings.fogColor = Color.Lerp(from.fogColor, to.fogColor, t);
            // to가 fog 없으면 fogDensity를 0으로 보간 (잔여 안개 제거)
            float targetDensity = to.fogEnabled ? to.fogDensity : 0f;
            float sourceDensity = from.fogEnabled ? from.fogDensity : 0f;
            RenderSettings.fogDensity = Mathf.Lerp(sourceDensity, targetDensity, t);
            RenderSettings.fogMode = FogMode.Exponential;

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
