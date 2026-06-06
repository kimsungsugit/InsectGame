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
        private Color defaultCameraBg;

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
            defaultFogEnabled = RenderSettings.fog;
            defaultFogColor = RenderSettings.fogColor;
            defaultFogDensity = RenderSettings.fogDensity;

            if (mainCamera != null)
                defaultCameraBg = mainCamera.backgroundColor;

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
                case "cave":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.4f, 0.35f, 0.3f),
                        lightIntensity = 0.35f,
                        lightRotation = Quaternion.Euler(80f, 30f, 0f),
                        ambientColor = new Color(0.08f, 0.06f, 0.05f),
                        fogEnabled = true,
                        fogColor = new Color(0.05f, 0.04f, 0.03f),
                        fogDensity = 0.06f,
                        cameraBg = new Color(0.02f, 0.02f, 0.02f)
                    };

                case "deep_forest":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.5f, 0.7f, 0.35f),
                        lightIntensity = 0.5f,
                        lightRotation = Quaternion.Euler(70f, 45f, 0f),
                        ambientColor = new Color(0.1f, 0.18f, 0.06f),
                        fogEnabled = true,
                        fogColor = new Color(0.12f, 0.2f, 0.08f),
                        fogDensity = 0.04f,
                        cameraBg = new Color(0.05f, 0.08f, 0.03f)
                    };

                case "underwater":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.3f, 0.55f, 0.8f),
                        lightIntensity = 0.45f,
                        lightRotation = Quaternion.Euler(85f, 0f, 0f),
                        ambientColor = new Color(0.08f, 0.15f, 0.25f),
                        fogEnabled = true,
                        fogColor = new Color(0.1f, 0.25f, 0.45f),
                        fogDensity = 0.08f,
                        cameraBg = new Color(0.04f, 0.1f, 0.2f)
                    };

                case "pond":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.7f, 0.85f, 0.95f),
                        lightIntensity = 0.8f,
                        lightRotation = Quaternion.Euler(55f, 20f, 0f),
                        ambientColor = new Color(0.2f, 0.3f, 0.35f),
                        fogEnabled = true,
                        fogColor = new Color(0.5f, 0.6f, 0.7f),
                        fogDensity = 0.015f,
                        cameraBg = new Color(0.15f, 0.25f, 0.35f)
                    };

                case "fog":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.6f, 0.58f, 0.55f),
                        lightIntensity = 0.4f,
                        lightRotation = Quaternion.Euler(60f, 30f, 0f),
                        ambientColor = new Color(0.2f, 0.2f, 0.18f),
                        fogEnabled = true,
                        fogColor = new Color(0.55f, 0.52f, 0.48f),
                        fogDensity = 0.1f,
                        cameraBg = new Color(0.35f, 0.33f, 0.3f)
                    };

                case "reeds":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.85f, 0.8f, 0.55f),
                        lightIntensity = 0.9f,
                        lightRotation = Quaternion.Euler(45f, 60f, 0f),
                        ambientColor = new Color(0.25f, 0.28f, 0.15f),
                        fogEnabled = true,
                        fogColor = new Color(0.55f, 0.58f, 0.4f),
                        fogDensity = 0.02f,
                        cameraBg = new Color(0.2f, 0.22f, 0.12f)
                    };

                case "peak":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.95f, 0.95f, 1f),
                        lightIntensity = 1.5f,
                        lightRotation = Quaternion.Euler(35f, 30f, 0f),
                        ambientColor = new Color(0.4f, 0.42f, 0.5f),
                        fogEnabled = true,
                        fogColor = new Color(0.8f, 0.85f, 0.95f),
                        fogDensity = 0.02f,
                        cameraBg = new Color(0.5f, 0.55f, 0.7f)
                    };

                case "flower_maze":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(1f, 0.88f, 0.75f),
                        lightIntensity = 1.1f,
                        lightRotation = Quaternion.Euler(45f, 50f, 0f),
                        ambientColor = new Color(0.35f, 0.25f, 0.3f),
                        fogEnabled = true,
                        fogColor = new Color(0.85f, 0.7f, 0.75f),
                        fogDensity = 0.025f,
                        cameraBg = new Color(0.3f, 0.2f, 0.25f)
                    };

                case "greenhouse":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.85f, 1f, 0.8f),
                        lightIntensity = 1.0f,
                        lightRotation = Quaternion.Euler(50f, 30f, 0f),
                        ambientColor = new Color(0.25f, 0.35f, 0.2f),
                        fogEnabled = false,
                        fogColor = defaultFogColor,
                        fogDensity = 0f,
                        cameraBg = new Color(0.15f, 0.22f, 0.12f)
                    };

                case "temple":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.7f, 0.6f, 0.9f),
                        lightIntensity = 0.5f,
                        lightRotation = Quaternion.Euler(75f, 10f, 0f),
                        ambientColor = new Color(0.12f, 0.08f, 0.2f),
                        fogEnabled = true,
                        fogColor = new Color(0.2f, 0.15f, 0.3f),
                        fogDensity = 0.045f,
                        cameraBg = new Color(0.08f, 0.05f, 0.15f)
                    };

                case "underground":
                    return new EnvironmentProfile
                    {
                        lightColor = new Color(0.3f, 0.25f, 0.2f),
                        lightIntensity = 0.25f,
                        lightRotation = Quaternion.Euler(85f, 0f, 0f),
                        ambientColor = new Color(0.05f, 0.04f, 0.03f),
                        fogEnabled = true,
                        fogColor = new Color(0.04f, 0.03f, 0.02f),
                        fogDensity = 0.08f,
                        cameraBg = new Color(0.01f, 0.01f, 0.01f)
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
