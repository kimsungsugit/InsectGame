using System;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Spawning
{
    public class InsectEntity : MonoBehaviour
    {
        [SerializeField] private InsectData data;
        [SerializeField] private int level = 1;

        private Action<InsectEntity> onDespawn;
        private SpawnPoint ownerPoint;
        private float bobPhase;
        private Vector3 basePosition;
        private float wingPhase;
        private bool shiny;
        private bool forBattle;
        private float shinySparkleTimer;
        private Transform cachedShinySparkle;
        private Transform cachedNameLabel;
        private bool despawnedThisCycle; // Despawn 다중 호출 가드 (Battle/Capture 동시 호출 시 풀 중복 반환 차단)

        // Camera.main은 매 호출마다 FindGameObjectWithTag — 최대 20마리×매 프레임 핫패스 회피.
        private static Camera cachedMainCam;

        public InsectData Data => data;
        public int Level => level;
        public bool IsShiny => shiny;
        public SpawnPoint OwnerPoint => ownerPoint;
        public string RegionId => ownerPoint != null ? ownerPoint.regionId : string.Empty;

        public void Initialize(InsectData insectData, int insectLevel, SpawnPoint point, Action<InsectEntity> despawnCallback)
        {
            data = insectData;
            level = insectLevel;
            ownerPoint = point;
            onDespawn = despawnCallback;
            shiny = UnityEngine.Random.value < 0.01f; // 1% 확률 색다른 곤충
            // 풀 재사용 회귀 방지: BuildForBattle에서 true로 설정된 forBattle이 남아있으면
            // 다음 Update에서 회전 안 하는 정적 곤충이 됨. 매 Initialize마다 명시적 false.
            forBattle = false;
            // 풀 재사용 시 stale Transform 참조 회피 (ClearChildren 직후 cache 무효).
            cachedNameLabel = null;
            cachedShinySparkle = null;
            despawnedThisCycle = false;

            ClearChildren();
            BuildModel();
            AddRarityEffects();
            CreateNameLabel();
            CreateGroundMarker();
            float scale = GetRarityScale();
            transform.localScale = Vector3.one * scale;
            basePosition = transform.position;
            bobPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            wingPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        public void BuildForBattle(InsectData insectData, int insectLevel, bool shinyOverride)
        {
            data = insectData;
            level = insectLevel;
            shiny = shinyOverride;
            forBattle = true;
            cachedNameLabel = null;
            cachedShinySparkle = null;
            despawnedThisCycle = false;

            ClearChildren();
            BuildModel();
            basePosition = transform.position;
            bobPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            wingPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
        }

        private void Update()
        {
            float bobSpeed = 1.6f + (bobPhase % 1.5f);
            float bob = Mathf.Sin(Time.time * bobSpeed + bobPhase) * 0.25f;
            transform.position = basePosition + new Vector3(0f, bob, 0f);
            if (!forBattle)
                transform.Rotate(Vector3.up, 12f * Time.deltaTime, Space.World);

            AnimateWings();
            if (shiny) AnimateShinySparkle();

            // Camera.main 매 프레임 FindGameObjectWithTag 회피 — static cache.
            if (cachedMainCam == null) cachedMainCam = Camera.main;
            if (cachedMainCam != null)
            {
                if (cachedNameLabel == null) cachedNameLabel = transform.Find("NameLabel");
                if (cachedNameLabel != null)
                    cachedNameLabel.rotation = cachedMainCam.transform.rotation;
            }
        }

        private void AnimateShinySparkle()
        {
            shinySparkleTimer += Time.deltaTime;
            if (cachedShinySparkle == null)
            {
                Transform existing = transform.Find("ShinySparkle");
                if (existing != null)
                {
                    cachedShinySparkle = existing;
                }
                else
                {
                    GameObject sparkleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sparkleObj.name = "ShinySparkle";
                    sparkleObj.transform.SetParent(transform, false);
                    sparkleObj.transform.localScale = Vector3.one * 0.15f;
                    Collider sc = sparkleObj.GetComponent<Collider>();
                    if (sc != null) Destroy(sc);
                    ApplyColor(sparkleObj, new Color(1f, 1f, 0.6f, 0.8f));
                    cachedShinySparkle = sparkleObj.transform;
                }
            }
            Transform sparkle = cachedShinySparkle;

            // 반짝임 원형 이동 + 크기 맥동
            float angle = shinySparkleTimer * 3f;
            float radius = 0.6f;
            float sparkY = 0.3f + Mathf.Sin(shinySparkleTimer * 2f) * 0.4f;
            sparkle.localPosition = new Vector3(Mathf.Cos(angle) * radius, sparkY, Mathf.Sin(angle) * radius);
            float pulse = 0.1f + Mathf.Abs(Mathf.Sin(shinySparkleTimer * 5f)) * 0.12f;
            sparkle.localScale = Vector3.one * pulse;
        }

        private void AnimateWings()
        {
            Transform wl = transform.Find("WingL");
            Transform wr = transform.Find("WingR");
            if (wl == null || wr == null) return;

            string id = data != null ? data.insectId ?? "" : "";
            float speed = 6f;
            float amplitude = 25f;
            if (id.Contains("butterfly") || id.Contains("moth") || id.Contains("luna") || id.Contains("atlas"))
            { speed = 3f; amplitude = 35f; }
            else if (id.Contains("damselfly"))
            { speed = 4f; amplitude = 30f; }
            else if (id.Contains("bee") || id.Contains("dragonfly"))
            { speed = 12f; amplitude = 20f; }
            else if (id.Contains("wasp") || id.Contains("hornet"))
            { speed = 14f; amplitude = 18f; }
            else if (id.Contains("mosquito") || id.Contains("fly"))
            { speed = 16f; amplitude = 15f; }

            float angle = Mathf.Sin(Time.time * speed + wingPhase) * amplitude;
            wl.localRotation = Quaternion.Euler(0f, 0f, angle);
            wr.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        private void BuildModel()
        {
            if (data == null) { BuildGenericBeetle(GetRarityColor(), Color.gray); return; }
            string id = data.insectId ?? "";
            Color col = GetInsectColor();
            // 음영색: 단순 절반(칙칙·무채색화)이 아니라 HSV로 채도 살짝 올리고 명도만 낮춰 색감 유지.
            Color.RGBToHSV(col, out float dh, out float ds, out float dv);
            Color dark = Color.HSVToRGB(dh, Mathf.Min(1f, ds * 1.1f), dv * 0.6f);

            // 순서 중요: 구체적인 ID를 먼저 체크 (antlion→ant 오매칭 방지 등)
            if (id.Contains("antlion"))
                BuildAntlion(col, dark);
            else if (id.Contains("aphid"))
                BuildAphid(col, dark);
            else if (id.Contains("butterfly") || id.Contains("luna") || id.Contains("atlas") || id.Contains("alexandras"))
                BuildButterfly(col, dark);
            else if (id.Contains("moth"))
                BuildMoth(col, dark);
            else if (id.Contains("orchid"))
                BuildOrchidMantis(col, dark);
            else if (id.Contains("ghost"))
                BuildGhostMantis(col, dark);
            else if (id.Contains("mantis"))
                BuildMantis(col, dark);
            else if (id.Contains("damselfly"))
                BuildDamselfly(col, dark);
            else if (id.Contains("dragonfly") || id.Contains("ancient"))
                BuildDragonfly(col, dark);
            else if (id.Contains("firefly"))
                BuildFirefly(col, dark);
            else if (id.Contains("bee"))
                BuildBee(col, dark);
            else if (id.Contains("hornet") || id.Contains("wasp"))
                BuildWasp(col, dark);
            else if (id.Contains("rhinoceros") || id.Contains("hercules"))
                BuildRhinocerosBeetle(col, dark);
            else if (id.Contains("stag") || id.Contains("golden_stag"))
                BuildHornBeetle(col, dark);
            else if (id.Contains("cicada"))
                BuildCicada(col, dark);
            else if (id.Contains("cricket") || id.Contains("katydid"))
                BuildCricket(col, dark);
            else if (id.Contains("ant"))
                BuildAnt(col, dark);
            else if (id.Contains("water_strider") || id.Contains("strider"))
                BuildWaterStrider(col, dark);
            else if (id.Contains("diving"))
                BuildDivingBeetle(col, dark);
            else if (id.Contains("scarab") || id.Contains("jewel"))
                BuildJewelBeetle(col, dark);
            else if (id.Contains("ladybug"))
                BuildLadybug(col, dark);
            else if (id.Contains("grasshopper"))
                BuildGrasshopper(col, dark);
            else if (id.Contains("spider"))
                BuildSpider(col, dark);
            else if (id.Contains("stick_insect") || id.Contains("leaf_insect"))
                BuildStickInsect(col, dark);
            else if (id.Contains("centipede"))
                BuildCentipede(col, dark);
            else if (id.Contains("pill_bug"))
                BuildPillBug(col, dark);
            else if (id.Contains("earwig"))
                BuildEarwig(col, dark);
            else if (id.Contains("longhorn"))
                BuildLonghornBeetle(col, dark);
            else if (id.Contains("caterpillar"))
                BuildCaterpillar(col, dark);
            else if (id.Contains("mosquito") || id.Contains("fly"))
                BuildFly(col, dark);
            else if (id.Contains("dung"))
                BuildDungBeetle(col, dark);
            else if (id.Contains("click"))
                BuildClickBeetle(col, dark);
            else
                BuildGenericBeetle(col, dark);
        }

        private void BuildGenericBeetle(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.72f, 0.46f, 0.86f), body);
            MakeTopGloss(Vector3.zero, new Vector3(0.72f, 0.46f, 0.86f));
            MakePart("ShellL", PrimitiveType.Sphere, new Vector3(-0.13f, 0.18f, -0.05f), new Vector3(0.32f, 0.18f, 0.75f), dark);
            MakePart("ShellR", PrimitiveType.Sphere, new Vector3(0.13f, 0.18f, -0.05f), new Vector3(0.32f, 0.18f, 0.75f), dark);
            MakePart("ShellLine", PrimitiveType.Cylinder, new Vector3(0f, 0.22f, -0.05f), new Vector3(0.02f, 0.01f, 0.7f), body);
            // 가슴마디(prothorax) — 머리·몸 연결 자연화(옛엔 머리가 몸에 바로 붙어 뭉툭)
            MakePart("Prothorax", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0.32f), new Vector3(0.5f, 0.34f, 0.32f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.56f), new Vector3(0.46f, 0.42f, 0.42f), dark);
            MakeEyes(0.68f, 0.13f);
            MakePart("FrontLegL", PrimitiveType.Capsule, new Vector3(-0.28f, -0.15f, 0.3f), new Vector3(0.06f, 0.22f, 0.06f),
                dark, Quaternion.Euler(0f, 0f, 25f));
            MakePart("FrontLegR", PrimitiveType.Capsule, new Vector3(0.28f, -0.15f, 0.3f), new Vector3(0.06f, 0.22f, 0.06f),
                dark, Quaternion.Euler(0f, 0f, -25f));
            MakeLegs(dark, 3, 0f);
            MakeAntennae(dark, 0.45f);
        }

        private void BuildHornBeetle(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.8f, 0.5f, 1.0f), body);
            MakePart("Shell", PrimitiveType.Sphere, new Vector3(0f, 0.15f, -0.1f), new Vector3(0.75f, 0.35f, 0.85f), dark);
            MakePart("ShellLineL", PrimitiveType.Cylinder, new Vector3(-0.15f, 0.25f, -0.1f), new Vector3(0.02f, 0.01f, 0.7f), body);
            MakePart("ShellLineR", PrimitiveType.Cylinder, new Vector3(0.15f, 0.25f, -0.1f), new Vector3(0.02f, 0.01f, 0.7f), body);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.1f, 0.55f), new Vector3(0.5f, 0.4f, 0.45f), dark);
            MakePart("HornBase", PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0.6f), new Vector3(0.1f, 0.2f, 0.1f), body,
                Quaternion.Euler(20f, 0f, 0f));
            MakePart("HornMid", PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0.75f), new Vector3(0.07f, 0.2f, 0.07f), body,
                Quaternion.Euler(35f, 0f, 0f));
            MakePart("HornTip", PrimitiveType.Sphere, new Vector3(0f, 0.65f, 0.9f), Vector3.one * 0.1f, body);
            MakePart("ClawL", PrimitiveType.Cube, new Vector3(-0.28f, -0.22f, 0.25f), new Vector3(0.05f, 0.08f, 0.1f), dark);
            MakePart("ClawR", PrimitiveType.Cube, new Vector3(0.28f, -0.22f, 0.25f), new Vector3(0.05f, 0.08f, 0.1f), dark);
            MakeEyes(0.7f, 0.14f, 0.2f);
            MakeLegs(dark, 3, 0f);
        }

        private void BuildButterfly(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.18f, 0.34f, 0.18f), dark,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.45f), new Vector3(0.3f, 0.3f, 0.28f), dark);
            Color wingCol = new Color(body.r, body.g, body.b, 0.85f);
            Color wingSpot = new Color(Mathf.Min(1, body.r + 0.3f), Mathf.Min(1, body.g + 0.3f), body.b * 0.5f);
            Color wingEdge = new Color(dark.r, dark.g, dark.b, 0.7f);
            MakeWing("WingL", new Vector3(-0.5f, 0.1f, 0f), new Vector3(0.7f, 0.02f, 0.6f), wingCol);
            MakeWing("WingR", new Vector3(0.5f, 0.1f, 0f), new Vector3(0.7f, 0.02f, 0.6f), wingCol);
            MakePart("SpotL1", PrimitiveType.Sphere, new Vector3(-0.45f, 0.12f, 0.1f), new Vector3(0.15f, 0.02f, 0.15f), wingSpot);
            MakePart("SpotR1", PrimitiveType.Sphere, new Vector3(0.45f, 0.12f, 0.1f), new Vector3(0.15f, 0.02f, 0.15f), wingSpot);
            MakePart("SpotL2", PrimitiveType.Sphere, new Vector3(-0.55f, 0.12f, 0f), new Vector3(0.12f, 0.02f, 0.12f), wingSpot);
            MakePart("SpotR2", PrimitiveType.Sphere, new Vector3(0.55f, 0.12f, 0f), new Vector3(0.12f, 0.02f, 0.12f), wingSpot);
            MakePart("SpotL3", PrimitiveType.Sphere, new Vector3(-0.4f, 0.12f, -0.1f), new Vector3(0.1f, 0.02f, 0.1f), wingSpot);
            MakePart("SpotR3", PrimitiveType.Sphere, new Vector3(0.4f, 0.12f, -0.1f), new Vector3(0.1f, 0.02f, 0.1f), wingSpot);
            MakePart("WingTipL", PrimitiveType.Sphere, new Vector3(-0.88f, 0.1f, 0.05f), new Vector3(0.18f, 0.025f, 0.24f), wingEdge);
            MakePart("WingTipR", PrimitiveType.Sphere, new Vector3(0.88f, 0.1f, 0.05f), new Vector3(0.18f, 0.025f, 0.24f), wingEdge);
            MakePart("WingLB", PrimitiveType.Sphere, new Vector3(-0.35f, 0.08f, -0.25f), new Vector3(0.45f, 0.02f, 0.4f), wingCol);
            MakePart("WingRB", PrimitiveType.Sphere, new Vector3(0.35f, 0.08f, -0.25f), new Vector3(0.45f, 0.02f, 0.4f), wingCol);
            MakeAntennae(dark, 0.35f);
            MakePart("AntBallL", PrimitiveType.Sphere, new Vector3(-0.15f, 0.42f, 0.57f), Vector3.one * 0.06f, dark);
            MakePart("AntBallR", PrimitiveType.Sphere, new Vector3(0.15f, 0.42f, 0.57f), Vector3.one * 0.06f, dark);
            MakeEyes(0.45f, 0.18f);
        }

        private void BuildMoth(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.2f, 0.35f, 0.2f), body,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Fur", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.15f), new Vector3(0.35f, 0.3f, 0.3f), body);
            MakePart("FurFluff", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0.2f), new Vector3(0.28f, 0.22f, 0.25f),
                new Color(body.r * 1.1f, body.g * 1.1f, body.b * 1.0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.4f), new Vector3(0.3f, 0.28f, 0.28f), dark);
            Color wingCol = new Color(body.r * 0.8f, body.g * 0.7f, body.b * 0.6f);
            Color eyeSpotCol = new Color(Mathf.Min(1, body.r + 0.1f), body.g * 0.4f, body.b * 0.3f);
            MakeWing("WingL", new Vector3(-0.55f, 0.05f, 0.05f), new Vector3(0.8f, 0.02f, 0.7f), wingCol);
            MakeWing("WingR", new Vector3(0.55f, 0.05f, 0.05f), new Vector3(0.8f, 0.02f, 0.7f), wingCol);
            MakePart("EyeSpotL", PrimitiveType.Sphere, new Vector3(-0.5f, 0.07f, 0.05f), new Vector3(0.18f, 0.02f, 0.18f), eyeSpotCol);
            MakePart("EyeSpotR", PrimitiveType.Sphere, new Vector3(0.5f, 0.07f, 0.05f), new Vector3(0.18f, 0.02f, 0.18f), eyeSpotCol);
            MakePart("EyeSpotCoreL", PrimitiveType.Sphere, new Vector3(-0.5f, 0.08f, 0.05f), new Vector3(0.08f, 0.02f, 0.08f), Color.black);
            MakePart("EyeSpotCoreR", PrimitiveType.Sphere, new Vector3(0.5f, 0.08f, 0.05f), new Vector3(0.08f, 0.02f, 0.08f), Color.black);
            MakeAntennae(dark, 0.4f, true);
            MakePart("FeatherL", PrimitiveType.Cube, new Vector3(-0.18f, 0.35f, 0.6f), new Vector3(0.1f, 0.02f, 0.06f), dark);
            MakePart("FeatherR", PrimitiveType.Cube, new Vector3(0.18f, 0.35f, 0.6f), new Vector3(0.1f, 0.02f, 0.06f), dark);
            MakeEyes(0.4f, 0.15f);
        }

        private void BuildMantis(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.15f), new Vector3(0.2f, 0.5f, 0.2f), body,
                Quaternion.Euler(80f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0.15f), new Vector3(0.25f, 0.2f, 0.25f), body);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0.25f), new Vector3(0.38f, 0.28f, 0.25f), dark);
            MakePart("HeadCrest", PrimitiveType.Cube, new Vector3(0f, 0.38f, 0.22f), new Vector3(0.12f, 0.06f, 0.12f), dark);
            MakePart("ArmUpperL", PrimitiveType.Capsule, new Vector3(-0.22f, 0.15f, 0.3f), new Vector3(0.08f, 0.2f, 0.08f),
                body, Quaternion.Euler(-10f, 0f, 20f));
            MakePart("ArmUpperR", PrimitiveType.Capsule, new Vector3(0.22f, 0.15f, 0.3f), new Vector3(0.08f, 0.2f, 0.08f),
                body, Quaternion.Euler(-10f, 0f, -20f));
            MakePart("ArmLowerL", PrimitiveType.Capsule, new Vector3(-0.28f, 0.28f, 0.42f), new Vector3(0.06f, 0.18f, 0.06f),
                body, Quaternion.Euler(-40f, 0f, 15f));
            MakePart("ArmLowerR", PrimitiveType.Capsule, new Vector3(0.28f, 0.28f, 0.42f), new Vector3(0.06f, 0.18f, 0.06f),
                body, Quaternion.Euler(-40f, 0f, -15f));
            MakePart("ClawL", PrimitiveType.Cube, new Vector3(-0.3f, 0.38f, 0.55f), new Vector3(0.05f, 0.18f, 0.04f), dark);
            MakePart("ClawR", PrimitiveType.Cube, new Vector3(0.3f, 0.38f, 0.55f), new Vector3(0.05f, 0.18f, 0.04f), dark);
            Color wingFold = new Color(body.r * 0.7f, body.g * 0.8f, body.b * 0.6f, 0.5f);
            MakePart("WingFoldL", PrimitiveType.Cube, new Vector3(-0.08f, 0.12f, -0.2f), new Vector3(0.15f, 0.01f, 0.4f), wingFold);
            MakePart("WingFoldR", PrimitiveType.Cube, new Vector3(0.08f, 0.12f, -0.2f), new Vector3(0.15f, 0.01f, 0.4f), wingFold);
            MakeLegs(dark, 2, -0.15f);
            MakeAntennae(dark, 0.3f);
            MakeEyes(0.3f, 0.22f);
        }

        private void BuildDragonfly(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.3f), new Vector3(0.12f, 0.6f, 0.12f), body,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("TailSeg1", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.65f), new Vector3(0.1f, 0.1f, 0.12f), body);
            MakePart("TailSeg2", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.78f), new Vector3(0.09f, 0.09f, 0.1f), dark);
            MakePart("TailSeg3", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.9f), new Vector3(0.08f, 0.08f, 0.09f), body);
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.2f), new Vector3(0.2f, 0.18f, 0.2f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.35f), new Vector3(0.35f, 0.25f, 0.3f), dark);
            Color wingCol = new Color(0.8f, 0.9f, 1f, 0.4f);
            Color veinCol = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            MakeWing("WingL", new Vector3(-0.45f, 0.1f, 0.15f), new Vector3(0.7f, 0.01f, 0.15f), wingCol);
            MakeWing("WingR", new Vector3(0.45f, 0.1f, 0.15f), new Vector3(0.7f, 0.01f, 0.15f), wingCol);
            MakePart("VeinFL", PrimitiveType.Cylinder, new Vector3(-0.45f, 0.11f, 0.15f), new Vector3(0.01f, 0.01f, 0.13f), veinCol,
                Quaternion.Euler(0f, 0f, 85f));
            MakePart("VeinFR", PrimitiveType.Cylinder, new Vector3(0.45f, 0.11f, 0.15f), new Vector3(0.01f, 0.01f, 0.13f), veinCol,
                Quaternion.Euler(0f, 0f, -85f));
            MakePart("WingLB", PrimitiveType.Cube, new Vector3(-0.4f, 0.08f, -0.05f), new Vector3(0.6f, 0.01f, 0.13f), wingCol);
            MakePart("WingRB", PrimitiveType.Cube, new Vector3(0.4f, 0.08f, -0.05f), new Vector3(0.6f, 0.01f, 0.13f), wingCol);
            MakePart("VeinBL", PrimitiveType.Cylinder, new Vector3(-0.4f, 0.09f, -0.05f), new Vector3(0.01f, 0.01f, 0.11f), veinCol,
                Quaternion.Euler(0f, 0f, 85f));
            MakePart("VeinBR", PrimitiveType.Cylinder, new Vector3(0.4f, 0.09f, -0.05f), new Vector3(0.01f, 0.01f, 0.11f), veinCol,
                Quaternion.Euler(0f, 0f, -85f));
            MakePart("EyeL", PrimitiveType.Sphere, new Vector3(-0.15f, 0.15f, 0.4f), Vector3.one * 0.16f, new Color(0.2f, 0.8f, 0.3f));
            MakePart("EyeR", PrimitiveType.Sphere, new Vector3(0.15f, 0.15f, 0.4f), Vector3.one * 0.16f, new Color(0.2f, 0.8f, 0.3f));
        }

        private void BuildBee(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.55f, 0.45f, 0.7f), body);
            MakeTopGloss(Vector3.zero, new Vector3(0.55f, 0.45f, 0.7f), 0.1f);
            MakePart("Stripe1", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.15f), new Vector3(0.54f, 0.02f, 0.54f),
                Color.black, Quaternion.Euler(90f, 0f, 0f));
            MakePart("Stripe2", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.56f, 0.02f, 0.56f),
                Color.black, Quaternion.Euler(90f, 0f, 0f));
            MakePart("Stripe3", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.15f), new Vector3(0.52f, 0.02f, 0.52f),
                Color.black, Quaternion.Euler(90f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.22f), new Vector3(0.38f, 0.32f, 0.3f),
                new Color(body.r * 0.9f, body.g * 0.8f, body.b * 0.3f));
            MakePart("ThoraxFuzz", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0.22f), new Vector3(0.32f, 0.25f, 0.25f),
                new Color(body.r, body.g * 0.9f, body.b * 0.4f, 0.7f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.4f), new Vector3(0.35f, 0.32f, 0.3f), dark);
            Color wingCol = new Color(1f, 1f, 1f, 0.35f);
            MakeWing("WingL", new Vector3(-0.3f, 0.25f, 0.05f), new Vector3(0.4f, 0.01f, 0.25f), wingCol);
            MakeWing("WingR", new Vector3(0.3f, 0.25f, 0.05f), new Vector3(0.4f, 0.01f, 0.25f), wingCol);
            MakePart("PollenL", PrimitiveType.Sphere, new Vector3(-0.22f, -0.18f, -0.05f), Vector3.one * 0.08f,
                new Color(1f, 0.85f, 0.2f));
            MakePart("PollenR", PrimitiveType.Sphere, new Vector3(0.22f, -0.18f, -0.05f), Vector3.one * 0.08f,
                new Color(1f, 0.85f, 0.2f));
            MakePart("Stinger", PrimitiveType.Capsule, new Vector3(0f, -0.05f, -0.45f), new Vector3(0.06f, 0.15f, 0.06f),
                dark, Quaternion.Euler(80f, 0f, 0f));
            MakeEyes(0.4f, 0.14f);
            MakeAntennae(dark, 0.35f);
            MakeLegs(dark, 3, 0f);
        }

        private void BuildFirefly(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.5f, 0.35f, 0.7f), dark);
            MakePart("LightOrgan", PrimitiveType.Sphere, new Vector3(0f, -0.05f, -0.3f), new Vector3(0.4f, 0.3f, 0.35f),
                new Color(0.9f, 1f, 0.3f, 0.9f));
            MakePart("GlowOuter", PrimitiveType.Sphere, new Vector3(0f, -0.05f, -0.3f), new Vector3(0.5f, 0.38f, 0.42f),
                new Color(0.95f, 1f, 0.5f, 0.3f));
            MakePart("GlowPulse", PrimitiveType.Sphere, new Vector3(0f, -0.02f, -0.32f), new Vector3(0.3f, 0.22f, 0.25f),
                new Color(1f, 1f, 0.8f, 0.5f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.4f), new Vector3(0.3f, 0.28f, 0.3f), dark);
            Color wingCol = new Color(body.r, body.g, body.b, 0.3f);
            MakeWing("WingL", new Vector3(-0.3f, 0.2f, 0f), new Vector3(0.35f, 0.01f, 0.3f), wingCol);
            MakeWing("WingR", new Vector3(0.3f, 0.2f, 0f), new Vector3(0.35f, 0.01f, 0.3f), wingCol);
            MakeEyes(0.4f, 0.13f);
            MakeAntennae(dark, 0.3f);
            MakeLegs(dark, 3, 0f);
        }

        private void BuildCicada(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.5f, 0.35f, 0.85f), body);
            MakePart("AbdSeg1", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.15f), new Vector3(0.48f, 0.02f, 0.48f),
                dark, Quaternion.Euler(90f, 0f, 0f));
            MakePart("AbdSeg2", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.28f), new Vector3(0.42f, 0.02f, 0.42f),
                dark, Quaternion.Euler(90f, 0f, 0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.5f), new Vector3(0.4f, 0.35f, 0.35f), dark);
            Color wingCol = new Color(0.7f, 0.8f, 0.7f, 0.3f);
            Color wingVein = new Color(0.4f, 0.5f, 0.4f, 0.5f);
            MakeWing("WingL", new Vector3(-0.35f, 0.15f, -0.1f), new Vector3(0.5f, 0.01f, 0.55f), wingCol);
            MakeWing("WingR", new Vector3(0.35f, 0.15f, -0.1f), new Vector3(0.5f, 0.01f, 0.55f), wingCol);
            MakePart("WingVeinL", PrimitiveType.Cylinder, new Vector3(-0.35f, 0.16f, -0.1f), new Vector3(0.01f, 0.01f, 0.45f),
                wingVein, Quaternion.Euler(0f, 0f, 80f));
            MakePart("WingVeinR", PrimitiveType.Cylinder, new Vector3(0.35f, 0.16f, -0.1f), new Vector3(0.01f, 0.01f, 0.45f),
                wingVein, Quaternion.Euler(0f, 0f, -80f));
            MakePart("CompEyeL", PrimitiveType.Sphere, new Vector3(-0.18f, 0.15f, 0.5f), Vector3.one * 0.17f,
                new Color(0.6f, 0.2f, 0.2f));
            MakePart("CompEyeR", PrimitiveType.Sphere, new Vector3(0.18f, 0.15f, 0.5f), Vector3.one * 0.17f,
                new Color(0.6f, 0.2f, 0.2f));
            MakeLegs(dark, 3, 0.1f);
        }

        private void BuildCricket(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.3f, 0.4f, 0.3f), body,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.4f), new Vector3(0.3f, 0.28f, 0.28f), dark);
            MakePart("ThighBL", PrimitiveType.Sphere, new Vector3(-0.18f, -0.05f, -0.18f), new Vector3(0.12f, 0.08f, 0.18f), dark);
            MakePart("ThighBR", PrimitiveType.Sphere, new Vector3(0.18f, -0.05f, -0.18f), new Vector3(0.12f, 0.08f, 0.18f), dark);
            MakePart("LegBL", PrimitiveType.Capsule, new Vector3(-0.22f, -0.12f, -0.22f), new Vector3(0.06f, 0.4f, 0.06f),
                dark, Quaternion.Euler(-30f, 0f, 30f));
            MakePart("LegBR", PrimitiveType.Capsule, new Vector3(0.22f, -0.12f, -0.22f), new Vector3(0.06f, 0.4f, 0.06f),
                dark, Quaternion.Euler(-30f, 0f, -30f));
            Color wingFold = new Color(body.r * 0.7f, body.g * 0.7f, body.b * 0.6f, 0.6f);
            MakePart("WingFoldL", PrimitiveType.Cube, new Vector3(-0.06f, 0.1f, -0.1f), new Vector3(0.12f, 0.01f, 0.35f), wingFold);
            MakePart("WingFoldR", PrimitiveType.Cube, new Vector3(0.06f, 0.1f, -0.1f), new Vector3(0.12f, 0.01f, 0.35f), wingFold);
            MakeLegs(dark, 2, 0.1f);
            MakePart("LongAntL", PrimitiveType.Capsule, new Vector3(-0.1f, 0.2f, 0.55f), new Vector3(0.02f, 0.3f, 0.02f),
                dark, Quaternion.Euler(-25f, 0f, 10f));
            MakePart("LongAntR", PrimitiveType.Capsule, new Vector3(0.1f, 0.2f, 0.55f), new Vector3(0.02f, 0.3f, 0.02f),
                dark, Quaternion.Euler(-25f, 0f, -10f));
            MakeEyes(0.4f, 0.13f);
        }

        private void BuildAnt(Color body, Color dark)
        {
            MakePart("Abdomen", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.3f), new Vector3(0.35f, 0.3f, 0.4f), body);
            MakePart("Petiole", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.1f), new Vector3(0.06f, 0.08f, 0.06f), dark,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.22f, 0.2f, 0.25f), dark);
            MakePart("Neck", PrimitiveType.Capsule, new Vector3(0f, 0.03f, 0.18f), new Vector3(0.06f, 0.06f, 0.06f), dark,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.3f), new Vector3(0.3f, 0.28f, 0.28f), dark);
            MakePart("MandibleL", PrimitiveType.Cube, new Vector3(-0.08f, 0f, 0.45f), new Vector3(0.06f, 0.04f, 0.1f), body);
            MakePart("MandibleR", PrimitiveType.Cube, new Vector3(0.08f, 0f, 0.45f), new Vector3(0.06f, 0.04f, 0.1f), body);
            MakeLegs(dark, 3, -0.05f);
            MakePart("ElbowAntL", PrimitiveType.Capsule, new Vector3(-0.1f, 0.2f, 0.45f), new Vector3(0.03f, 0.15f, 0.03f),
                dark, Quaternion.Euler(-50f, 0f, 15f));
            MakePart("ElbowAntR", PrimitiveType.Capsule, new Vector3(0.1f, 0.2f, 0.45f), new Vector3(0.03f, 0.15f, 0.03f),
                dark, Quaternion.Euler(-50f, 0f, -15f));
            MakePart("ElbowAntL2", PrimitiveType.Capsule, new Vector3(-0.14f, 0.35f, 0.52f), new Vector3(0.025f, 0.12f, 0.025f),
                dark, Quaternion.Euler(-10f, 0f, 5f));
            MakePart("ElbowAntR2", PrimitiveType.Capsule, new Vector3(0.14f, 0.35f, 0.52f), new Vector3(0.025f, 0.12f, 0.025f),
                dark, Quaternion.Euler(-10f, 0f, -5f));
            MakeEyes(0.3f, 0.1f);
        }

        private void BuildWaterStrider(Color body, Color dark)
        {
            Color sheen = new Color(Mathf.Min(1, body.r + 0.2f), Mathf.Min(1, body.g + 0.2f), Mathf.Min(1, body.b + 0.25f));
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.12f, 0.35f, 0.12f), sheen,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.03f, 0.35f), new Vector3(0.18f, 0.16f, 0.18f), dark);
            MakePart("LegFL", PrimitiveType.Capsule, new Vector3(-0.4f, -0.05f, 0.2f), new Vector3(0.03f, 0.4f, 0.03f),
                dark, Quaternion.Euler(0f, 0f, 70f));
            MakePart("LegFR", PrimitiveType.Capsule, new Vector3(0.4f, -0.05f, 0.2f), new Vector3(0.03f, 0.4f, 0.03f),
                dark, Quaternion.Euler(0f, 0f, -70f));
            MakePart("LegML", PrimitiveType.Capsule, new Vector3(-0.5f, -0.05f, 0f), new Vector3(0.03f, 0.5f, 0.03f),
                dark, Quaternion.Euler(0f, 0f, 80f));
            MakePart("LegMR", PrimitiveType.Capsule, new Vector3(0.5f, -0.05f, 0f), new Vector3(0.03f, 0.5f, 0.03f),
                dark, Quaternion.Euler(0f, 0f, -80f));
            MakePart("LegBL", PrimitiveType.Capsule, new Vector3(-0.4f, -0.05f, -0.2f), new Vector3(0.03f, 0.45f, 0.03f),
                dark, Quaternion.Euler(0f, 0f, 75f));
            MakePart("LegBR", PrimitiveType.Capsule, new Vector3(0.4f, -0.05f, -0.2f), new Vector3(0.03f, 0.45f, 0.03f),
                dark, Quaternion.Euler(0f, 0f, -75f));
            Color ripple = new Color(0.7f, 0.85f, 1f, 0.4f);
            MakePart("RippleFL", PrimitiveType.Sphere, new Vector3(-0.55f, -0.12f, 0.2f), Vector3.one * 0.06f, ripple);
            MakePart("RippleFR", PrimitiveType.Sphere, new Vector3(0.55f, -0.12f, 0.2f), Vector3.one * 0.06f, ripple);
            MakePart("RippleML", PrimitiveType.Sphere, new Vector3(-0.7f, -0.12f, 0f), Vector3.one * 0.06f, ripple);
            MakePart("RippleMR", PrimitiveType.Sphere, new Vector3(0.7f, -0.12f, 0f), Vector3.one * 0.06f, ripple);
            MakePart("RippleBL", PrimitiveType.Sphere, new Vector3(-0.55f, -0.12f, -0.2f), Vector3.one * 0.06f, ripple);
            MakePart("RippleBR", PrimitiveType.Sphere, new Vector3(0.55f, -0.12f, -0.2f), Vector3.one * 0.06f, ripple);
            MakeEyes(0.35f, 0.08f);
        }

        private void BuildDivingBeetle(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.6f, 0.3f, 0.95f), body);
            MakePart("Shell", PrimitiveType.Sphere, new Vector3(0f, 0.1f, 0f), new Vector3(0.55f, 0.15f, 0.85f), dark);
            MakePart("Keel", PrimitiveType.Cube, new Vector3(0f, -0.12f, 0f), new Vector3(0.08f, 0.04f, 0.8f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.5f), new Vector3(0.38f, 0.25f, 0.32f), dark);
            MakePart("PaddleL", PrimitiveType.Cube, new Vector3(-0.35f, -0.1f, -0.2f), new Vector3(0.22f, 0.03f, 0.18f), dark);
            MakePart("PaddleR", PrimitiveType.Cube, new Vector3(0.35f, -0.1f, -0.2f), new Vector3(0.22f, 0.03f, 0.18f), dark);
            MakePart("PaddleFringeL", PrimitiveType.Cube, new Vector3(-0.45f, -0.1f, -0.2f), new Vector3(0.04f, 0.01f, 0.16f),
                new Color(dark.r, dark.g, dark.b, 0.6f));
            MakePart("PaddleFringeR", PrimitiveType.Cube, new Vector3(0.45f, -0.1f, -0.2f), new Vector3(0.04f, 0.01f, 0.16f),
                new Color(dark.r, dark.g, dark.b, 0.6f));
            Color bubbleCol = new Color(0.8f, 0.9f, 1f, 0.35f);
            MakePart("Bubble", PrimitiveType.Sphere, new Vector3(0f, -0.15f, -0.35f), Vector3.one * 0.15f, bubbleCol);
            MakeLegs(dark, 2, 0.15f);
            MakeEyes(0.5f, 0.12f);
        }

        private void BuildJewelBeetle(Color body, Color dark)
        {
            Color shimmer1 = new Color(Mathf.Min(1, body.r + 0.2f), Mathf.Min(1, body.g + 0.3f), Mathf.Min(1, body.b + 0.2f));
            Color shimmer2 = new Color(body.b * 0.5f, Mathf.Min(1, body.r + 0.3f), Mathf.Min(1, body.g + 0.2f));
            Color shimmer3 = new Color(Mathf.Min(1, body.g + 0.2f), body.r * 0.6f, Mathf.Min(1, body.b + 0.3f));
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.65f, 0.4f, 0.9f), body);
            MakePart("ShellLayer1", PrimitiveType.Sphere, new Vector3(0f, 0.18f, 0.1f), new Vector3(0.58f, 0.12f, 0.35f), shimmer1);
            MakePart("ShellLayer2", PrimitiveType.Sphere, new Vector3(0f, 0.17f, -0.05f), new Vector3(0.56f, 0.1f, 0.3f), shimmer2);
            MakePart("ShellLayer3", PrimitiveType.Sphere, new Vector3(0f, 0.16f, -0.2f), new Vector3(0.52f, 0.1f, 0.3f), shimmer3);
            MakePart("ShellL", PrimitiveType.Sphere, new Vector3(-0.12f, 0.15f, 0f), new Vector3(0.3f, 0.2f, 0.8f), shimmer1);
            MakePart("ShellR", PrimitiveType.Sphere, new Vector3(0.12f, 0.15f, 0f), new Vector3(0.3f, 0.2f, 0.8f), shimmer1);
            MakePart("ShellGloss", PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.08f, 0.7f),
                new Color(1f, 1f, 1f, 0.15f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.5f), new Vector3(0.35f, 0.3f, 0.3f), dark);
            MakeLegs(dark, 3, 0f);
            MakeAntennae(dark, 0.35f);
            MakeEyes(0.5f, 0.14f);
        }

        private void BuildRhinocerosBeetle(Color body, Color dark)
        {
            Color gloss = new Color(Mathf.Min(1, body.r + 0.15f), Mathf.Min(1, body.g + 0.1f), body.b * 0.8f);
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.9f, 0.55f, 1.1f), body);
            MakePart("Shell", PrimitiveType.Sphere, new Vector3(0f, 0.2f, -0.05f), new Vector3(0.82f, 0.3f, 0.95f), dark);
            MakePart("ShellGloss", PrimitiveType.Sphere, new Vector3(0f, 0.25f, -0.05f), new Vector3(0.7f, 0.12f, 0.8f),
                new Color(1f, 1f, 1f, 0.12f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0.6f), new Vector3(0.55f, 0.45f, 0.5f), dark);
            MakePart("HornMain", PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0.7f), new Vector3(0.1f, 0.35f, 0.1f), body,
                Quaternion.Euler(25f, 0f, 0f));
            // 3분절 곡선으로 뿔이 부드럽게 휨(옛 직선 실린더 2개 = 뚝뚝 끊김)
            MakePart("HornCurve", PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0.82f), new Vector3(0.08f, 0.18f, 0.08f), body,
                Quaternion.Euler(40f, 0f, 0f));
            MakePart("HornMid", PrimitiveType.Sphere, new Vector3(0f, 0.62f, 0.88f), Vector3.one * 0.09f, body);
            MakePart("HornTip", PrimitiveType.Cylinder, new Vector3(0f, 0.72f, 0.96f), new Vector3(0.06f, 0.13f, 0.06f), gloss,
                Quaternion.Euler(52f, 0f, 0f));
            // 끝 분기(Y자 뿔) — 장수풍뎅이 시그니처 실루엣
            MakePart("HornForkL", PrimitiveType.Cylinder, new Vector3(-0.05f, 0.78f, 1.0f), new Vector3(0.04f, 0.1f, 0.04f), gloss,
                Quaternion.Euler(50f, 0f, 12f));
            MakePart("HornForkR", PrimitiveType.Cylinder, new Vector3(0.05f, 0.78f, 1.0f), new Vector3(0.04f, 0.1f, 0.04f), gloss,
                Quaternion.Euler(50f, 0f, -12f));
            MakePart("HornSmall", PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0.55f), new Vector3(0.07f, 0.15f, 0.07f), dark,
                Quaternion.Euler(15f, 0f, 0f));
            MakePart("ClawL", PrimitiveType.Cube, new Vector3(-0.3f, -0.25f, 0.3f), new Vector3(0.06f, 0.08f, 0.12f), dark);
            MakePart("ClawR", PrimitiveType.Cube, new Vector3(0.3f, -0.25f, 0.3f), new Vector3(0.06f, 0.08f, 0.12f), dark);
            MakeEyes(0.78f, 0.14f, 0.22f);
            MakeLegs(dark, 3, 0f);
        }

        private void BuildOrchidMantis(Color body, Color dark)
        {
            Color petal = new Color(1f, 0.75f, 0.8f);
            Color petalLight = new Color(1f, 0.9f, 0.92f);
            MakePart("Body", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.12f), new Vector3(0.18f, 0.45f, 0.18f), petalLight,
                Quaternion.Euler(80f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0.15f), new Vector3(0.25f, 0.2f, 0.22f), petal);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0.25f), new Vector3(0.32f, 0.26f, 0.24f), petalLight);
            MakePart("HeadCrest", PrimitiveType.Cube, new Vector3(0f, 0.38f, 0.23f), new Vector3(0.1f, 0.06f, 0.1f), petal);
            MakePart("ArmL", PrimitiveType.Capsule, new Vector3(-0.22f, 0.15f, 0.3f), new Vector3(0.07f, 0.25f, 0.07f),
                petal, Quaternion.Euler(-15f, 0f, 18f));
            MakePart("ArmR", PrimitiveType.Capsule, new Vector3(0.22f, 0.15f, 0.3f), new Vector3(0.07f, 0.25f, 0.07f),
                petal, Quaternion.Euler(-15f, 0f, -18f));
            MakePart("ClawL", PrimitiveType.Cube, new Vector3(-0.28f, 0.35f, 0.48f), new Vector3(0.05f, 0.16f, 0.04f), dark);
            MakePart("ClawR", PrimitiveType.Cube, new Vector3(0.28f, 0.35f, 0.48f), new Vector3(0.05f, 0.16f, 0.04f), dark);
            MakePart("PetalLegFL", PrimitiveType.Sphere, new Vector3(-0.2f, -0.1f, 0.1f), new Vector3(0.15f, 0.04f, 0.12f), petal);
            MakePart("PetalLegFR", PrimitiveType.Sphere, new Vector3(0.2f, -0.1f, 0.1f), new Vector3(0.15f, 0.04f, 0.12f), petal);
            MakePart("PetalLegML", PrimitiveType.Sphere, new Vector3(-0.22f, -0.12f, -0.05f), new Vector3(0.16f, 0.04f, 0.13f), petalLight);
            MakePart("PetalLegMR", PrimitiveType.Sphere, new Vector3(0.22f, -0.12f, -0.05f), new Vector3(0.16f, 0.04f, 0.13f), petalLight);
            MakeLegs(petal, 2, -0.1f);
            MakeAntennae(petal, 0.3f);
            MakeEyes(0.3f, 0.2f);
        }

        private void BuildLadybug(Color body, Color dark)
        {
            Color red = new Color(0.9f, 0.15f, 0.1f);
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.65f, 0.5f, 0.7f), red);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.4f), new Vector3(0.28f, 0.25f, 0.25f), dark);
            MakePart("ShellLine", PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(0.02f, 0.01f, 0.6f), Color.black);
            MakePart("Spot1", PrimitiveType.Sphere, new Vector3(-0.15f, 0.26f, 0.1f), Vector3.one * 0.09f, Color.black);
            MakePart("Spot2", PrimitiveType.Sphere, new Vector3(0.15f, 0.26f, 0.1f), Vector3.one * 0.09f, Color.black);
            MakePart("Spot3", PrimitiveType.Sphere, new Vector3(-0.1f, 0.27f, -0.1f), Vector3.one * 0.1f, Color.black);
            MakePart("Spot4", PrimitiveType.Sphere, new Vector3(0.1f, 0.27f, -0.1f), Vector3.one * 0.1f, Color.black);
            MakePart("Spot5", PrimitiveType.Sphere, new Vector3(-0.18f, 0.24f, -0.2f), Vector3.one * 0.08f, Color.black);
            MakePart("Spot6", PrimitiveType.Sphere, new Vector3(0.18f, 0.24f, -0.2f), Vector3.one * 0.08f, Color.black);
            MakePart("Spot7", PrimitiveType.Sphere, new Vector3(0f, 0.27f, 0f), Vector3.one * 0.08f, Color.black);
            MakeLegs(dark, 3, 0.05f);
            MakeEyes(0.4f, 0.1f);
            MakeAntennae(dark, 0.3f);
        }

        private void BuildGrasshopper(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.25f, 0.5f, 0.25f), body,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Abdomen", PrimitiveType.Sphere, new Vector3(0f, -0.02f, -0.3f), new Vector3(0.22f, 0.2f, 0.25f), body);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.45f), new Vector3(0.3f, 0.28f, 0.28f), dark);
            MakePart("Jaw", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0.58f), new Vector3(0.12f, 0.06f, 0.08f), dark);
            // 뒷다리: 허벅지를 위로 솟게 하고 무릎 관절 추가
            MakePart("ThighBL", PrimitiveType.Sphere, new Vector3(-0.18f, 0.12f, -0.18f), new Vector3(0.16f, 0.12f, 0.25f), dark);
            MakePart("ThighBR", PrimitiveType.Sphere, new Vector3(0.18f, 0.12f, -0.18f), new Vector3(0.16f, 0.12f, 0.25f), dark);
            MakePart("KneeBL", PrimitiveType.Sphere, new Vector3(-0.26f, 0.18f, -0.32f), Vector3.one * 0.07f, dark);
            MakePart("KneeBR", PrimitiveType.Sphere, new Vector3(0.26f, 0.18f, -0.32f), Vector3.one * 0.07f, dark);
            MakePart("ShinBL", PrimitiveType.Capsule, new Vector3(-0.3f, -0.08f, -0.42f), new Vector3(0.04f, 0.5f, 0.04f),
                dark, Quaternion.Euler(-50f, 0f, 15f));
            MakePart("ShinBR", PrimitiveType.Capsule, new Vector3(0.3f, -0.08f, -0.42f), new Vector3(0.04f, 0.5f, 0.04f),
                dark, Quaternion.Euler(-50f, 0f, -15f));
            MakePart("FootBL", PrimitiveType.Sphere, new Vector3(-0.32f, -0.35f, -0.55f), Vector3.one * 0.04f, dark);
            MakePart("FootBR", PrimitiveType.Sphere, new Vector3(0.32f, -0.35f, -0.55f), Vector3.one * 0.04f, dark);
            MakeLegs(dark, 2, 0.15f);
            Color wingFold = new Color(body.r * 0.7f, body.g * 0.8f, body.b * 0.6f, 0.5f);
            MakePart("WingFoldL", PrimitiveType.Cube, new Vector3(-0.06f, 0.1f, -0.1f), new Vector3(0.12f, 0.01f, 0.4f), wingFold);
            MakePart("WingFoldR", PrimitiveType.Cube, new Vector3(0.06f, 0.1f, -0.1f), new Vector3(0.12f, 0.01f, 0.4f), wingFold);
            MakeEyes(0.45f, 0.16f);
            MakeAntennae(dark, 0.35f);
        }

        private void BuildWasp(Color body, Color dark)
        {
            Color yellow = new Color(1f, 0.85f, 0.1f);
            MakePart("Abdomen", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.25f), new Vector3(0.4f, 0.35f, 0.55f), yellow);
            MakePart("AbdStripe1", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.15f), new Vector3(0.39f, 0.02f, 0.39f),
                Color.black, Quaternion.Euler(90f, 0f, 0f));
            MakePart("AbdStripe2", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.3f), new Vector3(0.36f, 0.02f, 0.36f),
                Color.black, Quaternion.Euler(90f, 0f, 0f));
            MakePart("AbdStripe3", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.42f), new Vector3(0.3f, 0.02f, 0.3f),
                Color.black, Quaternion.Euler(90f, 0f, 0f));
            MakePart("Waist", PrimitiveType.Capsule, new Vector3(0f, 0f, 0.02f), new Vector3(0.06f, 0.1f, 0.06f), Color.black,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.18f), new Vector3(0.3f, 0.28f, 0.28f), Color.black);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.4f), new Vector3(0.3f, 0.28f, 0.28f), dark);
            MakePart("Stinger", PrimitiveType.Capsule, new Vector3(0f, -0.05f, -0.55f), new Vector3(0.05f, 0.2f, 0.05f),
                dark, Quaternion.Euler(85f, 0f, 0f));
            Color wingCol = new Color(1f, 1f, 1f, 0.3f);
            MakeWing("WingL", new Vector3(-0.28f, 0.22f, 0.05f), new Vector3(0.38f, 0.01f, 0.22f), wingCol);
            MakeWing("WingR", new Vector3(0.28f, 0.22f, 0.05f), new Vector3(0.38f, 0.01f, 0.22f), wingCol);
            MakeLegs(dark, 3, 0.05f);
            MakeEyes(0.4f, 0.13f);
            MakeAntennae(dark, 0.3f);
        }

        private void BuildSpider(Color body, Color dark)
        {
            MakePart("Abdomen", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.25f), new Vector3(0.55f, 0.5f, 0.6f), body);
            MakePart("Cephalothorax", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.15f), new Vector3(0.35f, 0.3f, 0.35f), dark);
            MakePart("FangL", PrimitiveType.Capsule, new Vector3(-0.06f, -0.05f, 0.35f), new Vector3(0.04f, 0.1f, 0.04f),
                dark, Quaternion.Euler(20f, 0f, 5f));
            MakePart("FangR", PrimitiveType.Capsule, new Vector3(0.06f, -0.05f, 0.35f), new Vector3(0.04f, 0.1f, 0.04f),
                dark, Quaternion.Euler(20f, 0f, -5f));
            float[] angles = { 30f, 55f, 110f, 140f };
            for (int i = 0; i < 4; i++)
            {
                float rad = angles[i] * Mathf.Deg2Rad;
                float x = Mathf.Cos(rad) * 0.45f;
                float z = Mathf.Sin(rad) * 0.15f - 0.05f;
                MakePart($"SpiderLegL{i}", PrimitiveType.Capsule, new Vector3(-Mathf.Abs(x), -0.08f, z),
                    new Vector3(0.04f, 0.35f, 0.04f), dark, Quaternion.Euler(0f, 0f, 55f + i * 5f));
                MakePart($"SpiderLegR{i}", PrimitiveType.Capsule, new Vector3(Mathf.Abs(x), -0.08f, z),
                    new Vector3(0.04f, 0.35f, 0.04f), dark, Quaternion.Euler(0f, 0f, -(55f + i * 5f)));
            }
            for (int i = 0; i < 4; i++)
            {
                float xOff = -0.06f + i * 0.04f;
                float size = (i < 2) ? 0.06f : 0.04f;
                MakePart($"SpEyeL{i}", PrimitiveType.Sphere, new Vector3(xOff - 0.02f, 0.15f + i * 0.02f, 0.3f),
                    Vector3.one * size, Color.black);
                MakePart($"SpEyeR{i}", PrimitiveType.Sphere, new Vector3(-xOff + 0.02f, 0.15f + i * 0.02f, 0.3f),
                    Vector3.one * size, Color.black);
            }
        }

        private void BuildStickInsect(Color body, Color dark)
        {
            string id = data != null ? data.insectId ?? "" : "";
            bool isLeaf = id.Contains("leaf_insect");
            Color col = isLeaf ? new Color(0.3f, 0.6f, 0.2f) : body;
            Color colDark = isLeaf ? new Color(0.2f, 0.4f, 0.12f) : dark;

            MakePart("BodySeg1", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.25f), new Vector3(0.06f, 0.3f, 0.06f), col,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("BodySeg2", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.06f, 0.3f, 0.06f), colDark,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("BodySeg3", PrimitiveType.Capsule, new Vector3(0f, 0f, 0.25f), new Vector3(0.06f, 0.25f, 0.06f), col,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.45f), new Vector3(0.12f, 0.1f, 0.12f), colDark);
            for (int i = 0; i < 3; i++)
            {
                float z = -0.2f + i * 0.2f;
                MakePart($"StickLegL{i}", PrimitiveType.Capsule, new Vector3(-0.35f, -0.08f, z),
                    new Vector3(0.03f, 0.35f, 0.03f), colDark, Quaternion.Euler(0f, 0f, 65f));
                MakePart($"StickLegR{i}", PrimitiveType.Capsule, new Vector3(0.35f, -0.08f, z),
                    new Vector3(0.03f, 0.35f, 0.03f), colDark, Quaternion.Euler(0f, 0f, -65f));
            }
            MakeAntennae(colDark, 0.35f);
            MakeEyes(0.45f, 0.06f);
            if (isLeaf)
            {
                MakePart("LeafWingL", PrimitiveType.Cube, new Vector3(-0.15f, 0.05f, 0f), new Vector3(0.25f, 0.02f, 0.4f), col);
                MakePart("LeafWingR", PrimitiveType.Cube, new Vector3(0.15f, 0.05f, 0f), new Vector3(0.25f, 0.02f, 0.4f), col);
                MakePart("LeafVeinL", PrimitiveType.Cylinder, new Vector3(-0.15f, 0.06f, 0f), new Vector3(0.01f, 0.01f, 0.35f),
                    colDark, Quaternion.Euler(0f, 0f, 85f));
                MakePart("LeafVeinR", PrimitiveType.Cylinder, new Vector3(0.15f, 0.06f, 0f), new Vector3(0.01f, 0.01f, 0.35f),
                    colDark, Quaternion.Euler(0f, 0f, -85f));
            }
        }

        private void BuildCentipede(Color body, Color dark)
        {
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.4f), new Vector3(0.2f, 0.16f, 0.2f), dark);
            MakePart("FangL", PrimitiveType.Capsule, new Vector3(-0.08f, -0.02f, 0.52f), new Vector3(0.04f, 0.08f, 0.04f),
                new Color(0.6f, 0.1f, 0.1f), Quaternion.Euler(15f, 0f, 10f));
            MakePart("FangR", PrimitiveType.Capsule, new Vector3(0.08f, -0.02f, 0.52f), new Vector3(0.04f, 0.08f, 0.04f),
                new Color(0.6f, 0.1f, 0.1f), Quaternion.Euler(15f, 0f, -10f));
            for (int i = 0; i < 6; i++)
            {
                float z = 0.25f - i * 0.14f;
                float size = 0.16f - i * 0.005f;
                Color segCol = (i % 2 == 0) ? body : dark;
                MakePart($"Seg{i}", PrimitiveType.Sphere, new Vector3(0f, 0f, z), new Vector3(size, 0.1f, 0.13f), segCol);
                MakePart($"CentiLegL{i}", PrimitiveType.Capsule, new Vector3(-0.15f, -0.1f, z),
                    new Vector3(0.03f, 0.12f, 0.03f), dark, Quaternion.Euler(0f, 0f, 30f));
                MakePart($"CentiLegR{i}", PrimitiveType.Capsule, new Vector3(0.15f, -0.1f, z),
                    new Vector3(0.03f, 0.12f, 0.03f), dark, Quaternion.Euler(0f, 0f, -30f));
            }
            MakeAntennae(dark, 0.3f);
            MakeEyes(0.4f, 0.07f);
        }

        private void BuildPillBug(Color body, Color dark)
        {
            // 둥근 갑옷 느낌: 겹겹이 쌓인 마디 Sphere로 반구형 등 표현
            Color shell1 = body;
            Color shell2 = new Color(body.r * 0.85f, body.g * 0.85f, body.b * 0.85f);
            MakePart("Shell1", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.18f), new Vector3(0.48f, 0.28f, 0.22f), shell1);
            MakePart("Shell2", PrimitiveType.Sphere, new Vector3(0f, 0.1f, 0.08f), new Vector3(0.55f, 0.32f, 0.2f), shell2);
            MakePart("Shell3", PrimitiveType.Sphere, new Vector3(0f, 0.11f, -0.02f), new Vector3(0.58f, 0.34f, 0.2f), shell1);
            MakePart("Shell4", PrimitiveType.Sphere, new Vector3(0f, 0.1f, -0.12f), new Vector3(0.56f, 0.32f, 0.2f), shell2);
            MakePart("Shell5", PrimitiveType.Sphere, new Vector3(0f, 0.08f, -0.22f), new Vector3(0.5f, 0.28f, 0.2f), shell1);
            MakePart("Shell6", PrimitiveType.Sphere, new Vector3(0f, 0.05f, -0.3f), new Vector3(0.4f, 0.22f, 0.18f), shell2);
            MakePart("ShellTail", PrimitiveType.Sphere, new Vector3(0f, 0.02f, -0.36f), new Vector3(0.28f, 0.15f, 0.12f), dark);
            // 마디 사이 홈(어두운 라인)
            for (int i = 0; i < 5; i++)
            {
                float z = 0.13f - i * 0.1f;
                MakePart($"SegLine{i}", PrimitiveType.Cylinder, new Vector3(0f, 0.14f, z), new Vector3(0.5f - i * 0.02f, 0.005f, 0.5f - i * 0.02f),
                    dark, Quaternion.Euler(90f, 0f, 0f));
            }
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.3f), new Vector3(0.22f, 0.18f, 0.2f), dark);
            for (int i = 0; i < 7; i++)
            {
                float z = 0.15f - i * 0.07f;
                float legLen = 0.06f + (i < 3 ? 0f : 0.02f);
                MakePart($"PillLegL{i}", PrimitiveType.Capsule, new Vector3(-0.24f, -0.14f, z),
                    new Vector3(0.025f, legLen, 0.025f), dark, Quaternion.Euler(0f, 0f, 20f));
                MakePart($"PillLegR{i}", PrimitiveType.Capsule, new Vector3(0.24f, -0.14f, z),
                    new Vector3(0.025f, legLen, 0.025f), dark, Quaternion.Euler(0f, 0f, -20f));
            }
            MakeAntennae(dark, 0.2f);
            MakeEyes(0.3f, 0.05f);
        }

        private void BuildEarwig(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.22f, 0.45f, 0.22f), body,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.2f), new Vector3(0.2f, 0.16f, 0.18f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.42f), new Vector3(0.25f, 0.22f, 0.25f), dark);
            // 집게: 크고 뚜렷한 V자 (각도 강화 + 끝 부분 두껍게)
            Color pincerCol = new Color(dark.r * 0.7f, dark.g * 0.5f, dark.b * 0.4f);
            MakePart("PincerBaseL", PrimitiveType.Capsule, new Vector3(-0.06f, -0.02f, -0.42f), new Vector3(0.055f, 0.12f, 0.055f),
                pincerCol, Quaternion.Euler(-45f, 0f, 25f));
            MakePart("PincerBaseR", PrimitiveType.Capsule, new Vector3(0.06f, -0.02f, -0.42f), new Vector3(0.055f, 0.12f, 0.055f),
                pincerCol, Quaternion.Euler(-45f, 0f, -25f));
            MakePart("PincerTipL", PrimitiveType.Capsule, new Vector3(-0.14f, -0.04f, -0.58f), new Vector3(0.045f, 0.14f, 0.045f),
                pincerCol, Quaternion.Euler(-70f, 0f, 10f));
            MakePart("PincerTipR", PrimitiveType.Capsule, new Vector3(0.14f, -0.04f, -0.58f), new Vector3(0.045f, 0.14f, 0.045f),
                pincerCol, Quaternion.Euler(-70f, 0f, -10f));
            MakePart("PincerEndL", PrimitiveType.Sphere, new Vector3(-0.15f, -0.08f, -0.7f), Vector3.one * 0.04f, pincerCol);
            MakePart("PincerEndR", PrimitiveType.Sphere, new Vector3(0.15f, -0.08f, -0.7f), Vector3.one * 0.04f, pincerCol);
            Color wingFold = new Color(body.r * 0.7f, body.g * 0.7f, body.b * 0.6f, 0.6f);
            MakePart("WingFoldL", PrimitiveType.Cube, new Vector3(-0.06f, 0.1f, -0.1f), new Vector3(0.1f, 0.01f, 0.25f), wingFold);
            MakePart("WingFoldR", PrimitiveType.Cube, new Vector3(0.06f, 0.1f, -0.1f), new Vector3(0.1f, 0.01f, 0.25f), wingFold);
            MakeLegs(dark, 3, 0f);
            MakeAntennae(dark, 0.32f);
            MakeEyes(0.42f, 0.1f);
        }

        private void BuildLonghornBeetle(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.65f, 0.4f, 0.85f), body);
            MakePart("ShellL", PrimitiveType.Sphere, new Vector3(-0.12f, 0.16f, -0.05f), new Vector3(0.3f, 0.18f, 0.72f), dark);
            MakePart("ShellR", PrimitiveType.Sphere, new Vector3(0.12f, 0.16f, -0.05f), new Vector3(0.3f, 0.18f, 0.72f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.48f), new Vector3(0.35f, 0.3f, 0.32f), dark);
            MakePart("LongAntL1", PrimitiveType.Capsule, new Vector3(-0.12f, 0.22f, 0.55f), new Vector3(0.03f, 0.25f, 0.03f),
                dark, Quaternion.Euler(-35f, 0f, 20f));
            MakePart("LongAntL2", PrimitiveType.Capsule, new Vector3(-0.22f, 0.45f, 0.7f), new Vector3(0.025f, 0.22f, 0.025f),
                dark, Quaternion.Euler(-15f, 0f, 10f));
            MakePart("LongAntL3", PrimitiveType.Capsule, new Vector3(-0.28f, 0.68f, 0.82f), new Vector3(0.02f, 0.2f, 0.02f),
                dark, Quaternion.Euler(-5f, 0f, 5f));
            MakePart("LongAntR1", PrimitiveType.Capsule, new Vector3(0.12f, 0.22f, 0.55f), new Vector3(0.03f, 0.25f, 0.03f),
                dark, Quaternion.Euler(-35f, 0f, -20f));
            MakePart("LongAntR2", PrimitiveType.Capsule, new Vector3(0.22f, 0.45f, 0.7f), new Vector3(0.025f, 0.22f, 0.025f),
                dark, Quaternion.Euler(-15f, 0f, -10f));
            MakePart("LongAntR3", PrimitiveType.Capsule, new Vector3(0.28f, 0.68f, 0.82f), new Vector3(0.02f, 0.2f, 0.02f),
                dark, Quaternion.Euler(-5f, 0f, -5f));
            MakeLegs(dark, 3, 0f);
            MakeEyes(0.48f, 0.12f);
        }

        private void BuildDamselfly(Color body, Color dark)
        {
            Color light = new Color(Mathf.Min(1, body.r + 0.15f), Mathf.Min(1, body.g + 0.15f), Mathf.Min(1, body.b + 0.2f));
            MakePart("Body", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.3f), new Vector3(0.08f, 0.5f, 0.08f), light,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("TailSeg1", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.6f), new Vector3(0.07f, 0.07f, 0.08f), light);
            MakePart("TailSeg2", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.7f), new Vector3(0.06f, 0.06f, 0.07f), body);
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.04f, 0.15f), new Vector3(0.14f, 0.12f, 0.14f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.06f, 0.3f), new Vector3(0.25f, 0.2f, 0.22f), dark);
            Color wingCol = new Color(0.85f, 0.92f, 1f, 0.35f);
            MakeWing("WingL", new Vector3(-0.3f, 0.08f, 0.05f), new Vector3(0.5f, 0.01f, 0.1f), wingCol);
            MakeWing("WingR", new Vector3(0.3f, 0.08f, 0.05f), new Vector3(0.5f, 0.01f, 0.1f), wingCol);
            MakePart("WingLB", PrimitiveType.Cube, new Vector3(-0.28f, 0.06f, -0.08f), new Vector3(0.45f, 0.01f, 0.08f), wingCol);
            MakePart("WingRB", PrimitiveType.Cube, new Vector3(0.28f, 0.06f, -0.08f), new Vector3(0.45f, 0.01f, 0.08f), wingCol);
            MakePart("EyeL", PrimitiveType.Sphere, new Vector3(-0.1f, 0.12f, 0.35f), Vector3.one * 0.12f, new Color(0.3f, 0.7f, 0.9f));
            MakePart("EyeR", PrimitiveType.Sphere, new Vector3(0.1f, 0.12f, 0.35f), Vector3.one * 0.12f, new Color(0.3f, 0.7f, 0.9f));
            MakeLegs(dark, 3, 0.1f);
        }

        private void BuildCaterpillar(Color body, Color dark)
        {
            // 통통한 애벌레: 머리와 꼬리 마디가 크고 중간이 가장 뚱뚱
            float[] sizes = { 0.13f, 0.16f, 0.18f, 0.19f, 0.18f, 0.15f, 0.11f };
            float[] yOff  = { 0.02f, 0.01f, 0f,    0f,    0f,   0.01f, 0.02f };
            Color light = new Color(Mathf.Min(1, body.r + 0.15f), Mathf.Min(1, body.g + 0.15f), body.b * 0.8f);
            for (int i = 0; i < sizes.Length; i++)
            {
                float z = 0.36f - i * 0.12f;
                float s = sizes[i];
                Color segCol = (i % 2 == 0) ? body : light;
                MakePart($"CaterSeg{i}", PrimitiveType.Sphere, new Vector3(0f, yOff[i], z),
                    new Vector3(s, s * 0.85f, 0.11f), segCol);
                // 마디 위 등점 무늬 (작은 원형 장식)
                if (i > 0 && i < sizes.Length - 1)
                {
                    MakePart($"CaterDot{i}", PrimitiveType.Sphere, new Vector3(0f, s * 0.7f, z),
                        Vector3.one * 0.03f, dark);
                }
                // 다리: 진짜 다리(앞 3쌍) + 배다리(뒤 4쌍)
                if (i >= 1)
                {
                    float legSize = (i >= 4) ? 0.045f : 0.035f;
                    MakePart($"CaterLegL{i}", PrimitiveType.Sphere, new Vector3(-s * 0.55f, -s * 0.5f, z),
                        Vector3.one * legSize, dark);
                    MakePart($"CaterLegR{i}", PrimitiveType.Sphere, new Vector3(s * 0.55f, -s * 0.5f, z),
                        Vector3.one * legSize, dark);
                }
            }
            // 큰 둥근 머리
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.04f, 0.46f), new Vector3(0.16f, 0.15f, 0.14f), dark);
            // 큰 귀여운 눈
            MakePart("EyeL", PrimitiveType.Sphere, new Vector3(-0.06f, 0.08f, 0.5f), Vector3.one * 0.08f, Color.white);
            MakePart("EyeR", PrimitiveType.Sphere, new Vector3(0.06f, 0.08f, 0.5f), Vector3.one * 0.08f, Color.white);
            MakePart("PupilL", PrimitiveType.Sphere, new Vector3(-0.06f, 0.08f, 0.54f), Vector3.one * 0.05f, Color.black);
            MakePart("PupilR", PrimitiveType.Sphere, new Vector3(0.06f, 0.08f, 0.54f), Vector3.one * 0.05f, Color.black);
            // 짧고 귀여운 더듬이
            MakePart("AntStubL", PrimitiveType.Capsule, new Vector3(-0.05f, 0.12f, 0.5f), new Vector3(0.02f, 0.05f, 0.02f),
                dark, Quaternion.Euler(-25f, 0f, 20f));
            MakePart("AntStubR", PrimitiveType.Capsule, new Vector3(0.05f, 0.12f, 0.5f), new Vector3(0.02f, 0.05f, 0.02f),
                dark, Quaternion.Euler(-25f, 0f, -20f));
            // 꼬리 돌기
            MakePart("TailHorn", PrimitiveType.Capsule, new Vector3(0f, 0.05f, -0.48f), new Vector3(0.025f, 0.06f, 0.025f),
                body, Quaternion.Euler(-50f, 0f, 0f));
        }

        private void BuildGhostMantis(Color body, Color dark)
        {
            Color ghost = new Color(0.45f, 0.4f, 0.35f, 0.65f);
            Color ghostDark = new Color(0.3f, 0.28f, 0.25f, 0.6f);
            MakePart("Body", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.15f), new Vector3(0.2f, 0.5f, 0.2f), ghost,
                Quaternion.Euler(80f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0.15f), new Vector3(0.25f, 0.2f, 0.25f), ghost);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0.25f), new Vector3(0.35f, 0.28f, 0.25f), ghostDark);
            MakePart("HeadCrest", PrimitiveType.Cube, new Vector3(0f, 0.42f, 0.22f), new Vector3(0.18f, 0.1f, 0.15f), ghost);
            MakePart("CrestTip", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0.2f), new Vector3(0.12f, 0.06f, 0.1f), ghostDark);
            MakePart("ArmUpperL", PrimitiveType.Capsule, new Vector3(-0.22f, 0.15f, 0.3f), new Vector3(0.08f, 0.2f, 0.08f),
                ghost, Quaternion.Euler(-10f, 0f, 20f));
            MakePart("ArmUpperR", PrimitiveType.Capsule, new Vector3(0.22f, 0.15f, 0.3f), new Vector3(0.08f, 0.2f, 0.08f),
                ghost, Quaternion.Euler(-10f, 0f, -20f));
            MakePart("ArmLowerL", PrimitiveType.Capsule, new Vector3(-0.28f, 0.28f, 0.42f), new Vector3(0.06f, 0.18f, 0.06f),
                ghost, Quaternion.Euler(-40f, 0f, 15f));
            MakePart("ArmLowerR", PrimitiveType.Capsule, new Vector3(0.28f, 0.28f, 0.42f), new Vector3(0.06f, 0.18f, 0.06f),
                ghost, Quaternion.Euler(-40f, 0f, -15f));
            MakePart("LeafDecorL", PrimitiveType.Cube, new Vector3(-0.3f, 0.1f, 0.05f), new Vector3(0.1f, 0.02f, 0.15f), ghost);
            MakePart("LeafDecorR", PrimitiveType.Cube, new Vector3(0.3f, 0.1f, 0.05f), new Vector3(0.1f, 0.02f, 0.15f), ghost);
            MakePart("LeafDecorLB", PrimitiveType.Cube, new Vector3(-0.25f, 0.08f, -0.15f), new Vector3(0.08f, 0.02f, 0.12f), ghostDark);
            MakePart("LeafDecorRB", PrimitiveType.Cube, new Vector3(0.25f, 0.08f, -0.15f), new Vector3(0.08f, 0.02f, 0.12f), ghostDark);
            MakeLegs(ghostDark, 2, -0.15f);
            MakeAntennae(ghostDark, 0.3f);
            MakeEyes(0.3f, 0.2f);
        }

        private void BuildFly(Color body, Color dark)
        {
            string id = data != null ? data.insectId ?? "" : "";
            bool isMosquito = id.Contains("mosquito");

            if (isMosquito)
            {
                // 모기: 가늘고 긴 몸, 긴 주둥이, 긴 다리
                MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.1f, 0.2f, 0.1f), dark,
                    Quaternion.Euler(90f, 0f, 0f));
                MakePart("Abdomen", PrimitiveType.Capsule, new Vector3(0f, -0.02f, -0.2f), new Vector3(0.08f, 0.18f, 0.08f),
                    new Color(0.4f, 0.15f, 0.12f), Quaternion.Euler(100f, 0f, 0f));
                MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.04f, 0.2f), new Vector3(0.14f, 0.13f, 0.14f), dark);
                Color eyeCol = new Color(0.2f, 0.2f, 0.2f);
                MakePart("BigEyeL", PrimitiveType.Sphere, new Vector3(-0.06f, 0.08f, 0.24f), Vector3.one * 0.08f, eyeCol);
                MakePart("BigEyeR", PrimitiveType.Sphere, new Vector3(0.06f, 0.08f, 0.24f), Vector3.one * 0.08f, eyeCol);
                MakePart("Proboscis", PrimitiveType.Capsule, new Vector3(0f, -0.02f, 0.32f), new Vector3(0.015f, 0.35f, 0.015f),
                    dark, Quaternion.Euler(75f, 0f, 0f));
                Color wingCol = new Color(1f, 1f, 1f, 0.25f);
                MakeWing("WingL", new Vector3(-0.18f, 0.12f, 0f), new Vector3(0.28f, 0.01f, 0.1f), wingCol);
                MakeWing("WingR", new Vector3(0.18f, 0.12f, 0f), new Vector3(0.28f, 0.01f, 0.1f), wingCol);
                // 모기 특유의 긴 다리 6개
                for (int i = 0; i < 3; i++)
                {
                    float z = 0.05f - i * 0.1f;
                    float spread = 30f + i * 12f;
                    MakePart($"MosqLegL{i}", PrimitiveType.Capsule, new Vector3(-0.2f, -0.15f, z),
                        new Vector3(0.02f, 0.3f, 0.02f), dark, Quaternion.Euler(-10f, 0f, spread));
                    MakePart($"MosqLegR{i}", PrimitiveType.Capsule, new Vector3(0.2f, -0.15f, z),
                        new Vector3(0.02f, 0.3f, 0.02f), dark, Quaternion.Euler(-10f, 0f, -spread));
                }
                MakeAntennae(dark, 0.12f, true);
            }
            else
            {
                // 파리: 통통한 몸, 거대한 빨간 복안, 짧은 주둥이
                MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.3f, 0.25f, 0.35f), dark);
                MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.03f, 0.15f), new Vector3(0.24f, 0.22f, 0.2f),
                    new Color(dark.r * 0.8f, dark.g * 0.8f, dark.b * 1.2f));
                MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.06f, 0.28f), new Vector3(0.26f, 0.24f, 0.24f), dark);
                Color eyeCol = new Color(0.75f, 0.12f, 0.08f);
                // 파리 복안: 머리의 대부분을 차지
                MakePart("BigEyeL", PrimitiveType.Sphere, new Vector3(-0.1f, 0.12f, 0.3f), Vector3.one * 0.17f, eyeCol);
                MakePart("BigEyeR", PrimitiveType.Sphere, new Vector3(0.1f, 0.12f, 0.3f), Vector3.one * 0.17f, eyeCol);
                MakePart("EyeHighlightL", PrimitiveType.Sphere, new Vector3(-0.08f, 0.16f, 0.34f), Vector3.one * 0.05f, Color.white);
                MakePart("EyeHighlightR", PrimitiveType.Sphere, new Vector3(0.08f, 0.16f, 0.34f), Vector3.one * 0.05f, Color.white);
                MakePart("Proboscis", PrimitiveType.Capsule, new Vector3(0f, -0.04f, 0.38f), new Vector3(0.04f, 0.08f, 0.04f),
                    dark, Quaternion.Euler(60f, 0f, 0f));
                Color wingCol = new Color(1f, 1f, 1f, 0.3f);
                MakeWing("WingL", new Vector3(-0.22f, 0.16f, 0.05f), new Vector3(0.35f, 0.01f, 0.18f), wingCol);
                MakeWing("WingR", new Vector3(0.22f, 0.16f, 0.05f), new Vector3(0.35f, 0.01f, 0.18f), wingCol);
                MakeLegs(dark, 3, 0.08f);
            }
        }

        private GameObject MakePart(string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Color color,
            Quaternion? rotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            if (rotation.HasValue)
                part.transform.localRotation = rotation.Value;

            Collider col = part.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);

            ApplyColor(part, color);
            return part;
        }

        private void MakeWing(string name, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.name = name;
            wing.transform.SetParent(transform, false);
            wing.transform.localPosition = pos;
            wing.transform.localScale = scale;
            Collider col = wing.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            ApplyColor(wing, color);
        }

        private void MakeLegs(Color color, int pairs, float zOffset)
        {
            Color joint = new Color(color.r * 0.7f + 0.03f, color.g * 0.7f + 0.03f, color.b * 0.7f + 0.03f);
            for (int i = 0; i < pairs; i++)
            {
                float z = zOffset + (i - (pairs - 1) * 0.5f) * 0.2f;
                // 앞·중·뒷다리 각도 변주(기계적 동일각 해소) + z 부채꼴 펼침
                float zSpread = (i - (pairs - 1) * 0.5f) * 0.04f;
                float upAng = 26f + i * 4f;
                float loAng = 8f + i * 3f;
                // 대퇴 (상단)
                MakePart($"LegUL{i}", PrimitiveType.Capsule, new Vector3(-0.22f, -0.1f, z + zSpread),
                    new Vector3(0.055f, 0.12f, 0.055f), color, Quaternion.Euler(0f, 0f, upAng));
                MakePart($"LegUR{i}", PrimitiveType.Capsule, new Vector3(0.22f, -0.1f, z + zSpread),
                    new Vector3(0.055f, 0.12f, 0.055f), color, Quaternion.Euler(0f, 0f, -upAng));
                // 관절
                MakePart($"KneeL{i}", PrimitiveType.Sphere, new Vector3(-0.3f, -0.2f, z + zSpread),
                    Vector3.one * 0.05f, joint);
                MakePart($"KneeR{i}", PrimitiveType.Sphere, new Vector3(0.3f, -0.2f, z + zSpread),
                    Vector3.one * 0.05f, joint);
                // 경절 (하단)
                MakePart($"LegLL{i}", PrimitiveType.Capsule, new Vector3(-0.32f, -0.3f, z + zSpread),
                    new Vector3(0.038f, 0.12f, 0.038f), color, Quaternion.Euler(0f, 0f, loAng));
                MakePart($"LegLR{i}", PrimitiveType.Capsule, new Vector3(0.32f, -0.3f, z + zSpread),
                    new Vector3(0.038f, 0.12f, 0.038f), color, Quaternion.Euler(0f, 0f, -loAng));
                // 발끝(tarsus) — 접지감(옛엔 발끝 없어 공중에 뜬 느낌)
                MakePart($"FootL{i}", PrimitiveType.Sphere, new Vector3(-0.345f, -0.4f, z + zSpread),
                    Vector3.one * 0.03f, joint);
                MakePart($"FootR{i}", PrimitiveType.Sphere, new Vector3(0.345f, -0.4f, z + zSpread),
                    Vector3.one * 0.03f, joint);
            }
        }

        private void MakeAntennae(Color color, float zBase, bool feathered = false)
        {
            // 2분절 굴절로 부드러운 S곡선(옛 직선 캡슐 1개 = 막대기 느낌 해소).
            MakePart("AntBaseL", PrimitiveType.Capsule, new Vector3(-0.1f, 0.18f, zBase + 0.13f),
                new Vector3(0.03f, 0.14f, 0.03f), color, Quaternion.Euler(-38f, 0f, 16f));
            MakePart("AntBaseR", PrimitiveType.Capsule, new Vector3(0.1f, 0.18f, zBase + 0.13f),
                new Vector3(0.03f, 0.14f, 0.03f), color, Quaternion.Euler(-38f, 0f, -16f));
            MakePart("AntMidL", PrimitiveType.Capsule, new Vector3(-0.15f, 0.36f, zBase + 0.2f),
                new Vector3(0.025f, 0.12f, 0.025f), color, Quaternion.Euler(-10f, 0f, 8f));
            MakePart("AntMidR", PrimitiveType.Capsule, new Vector3(0.15f, 0.36f, zBase + 0.2f),
                new Vector3(0.025f, 0.12f, 0.025f), color, Quaternion.Euler(-10f, 0f, -8f));
            float tipScale = feathered ? 0.08f : 0.055f;
            MakePart("AntTipL", PrimitiveType.Sphere, new Vector3(-0.17f, 0.46f, zBase + 0.23f), Vector3.one * tipScale, color);
            MakePart("AntTipR", PrimitiveType.Sphere, new Vector3(0.17f, 0.46f, zBase + 0.23f), Vector3.one * tipScale, color);
            if (feathered)
            {
                // 나방/모기 깃털 더듬이 — 끝에 양옆 작은 깃
                MakePart("AntFeatherL", PrimitiveType.Cube, new Vector3(-0.16f, 0.40f, zBase + 0.22f),
                    new Vector3(0.07f, 0.012f, 0.03f), color, Quaternion.Euler(0f, 0f, 20f));
                MakePart("AntFeatherR", PrimitiveType.Cube, new Vector3(0.16f, 0.40f, zBase + 0.22f),
                    new Vector3(0.07f, 0.012f, 0.03f), color, Quaternion.Euler(0f, 0f, -20f));
            }
        }

        private void MakeEyes(float zPos, float size, float xSpread = 0.12f)
        {
            MakePart("EyeL", PrimitiveType.Sphere, new Vector3(-xSpread, 0.15f, zPos), Vector3.one * size, Color.white);
            MakePart("EyeR", PrimitiveType.Sphere, new Vector3(xSpread, 0.15f, zPos), Vector3.one * size, Color.white);
            // 큰 동공 (치비 톤: 64%)
            float pupilSize = size * 0.64f;
            MakePart("PupilL", PrimitiveType.Sphere, new Vector3(-xSpread, 0.15f, zPos + 0.04f), Vector3.one * pupilSize, new Color(0.05f, 0.05f, 0.08f));
            MakePart("PupilR", PrimitiveType.Sphere, new Vector3(xSpread, 0.15f, zPos + 0.04f), Vector3.one * pupilSize, new Color(0.05f, 0.05f, 0.08f));
            // 메인 하이라이트 (확대 — 촉촉한 큰 눈)
            float hlSize = size * 0.28f;
            MakePart("HighlightL", PrimitiveType.Sphere, new Vector3(-(xSpread - 0.02f), 0.19f, zPos + 0.05f), Vector3.one * hlSize, new Color(1f, 1f, 1f, 0.95f));
            MakePart("HighlightR", PrimitiveType.Sphere, new Vector3(xSpread - 0.02f, 0.19f, zPos + 0.05f), Vector3.one * hlSize, new Color(1f, 1f, 1f, 0.95f));
            // 서브 글린트 (동공 반대편 작은 반짝임 — 치비 캐릭터의 생기있는 눈 시그니처)
            float glintSize = size * 0.12f;
            MakePart("GlintL", PrimitiveType.Sphere, new Vector3(-(xSpread + 0.025f), 0.115f, zPos + 0.05f), Vector3.one * glintSize, new Color(1f, 1f, 1f, 0.8f));
            MakePart("GlintR", PrimitiveType.Sphere, new Vector3(xSpread - 0.025f, 0.115f, zPos + 0.05f), Vector3.one * glintSize, new Color(1f, 1f, 1f, 0.8f));
        }

        // 곤충 등껍질 상단 흰색 반투명 글로스 — 입체 광택(딱정벌레/풍뎅이류 1줄 호출).
        private void MakeTopGloss(Vector3 bodyCenter, Vector3 bodyScale, float intensity = 0.14f)
        {
            MakePart("TopGloss", PrimitiveType.Sphere,
                bodyCenter + new Vector3(0f, bodyScale.y * 0.35f, bodyScale.z * 0.05f),
                new Vector3(bodyScale.x * 0.7f, bodyScale.y * 0.18f, bodyScale.z * 0.75f),
                new Color(1f, 1f, 1f, intensity));
        }

        private void ApplyColor(GameObject go, Color color)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            Material mat = new Material(shader);
            mat.color = color;
            // PBR 광택: 옛 ApplyColor는 색만 칠해 전 곤충이 무광 점토처럼 보였음(품질 저하 핵심).
            // Standard/URP Lit에서만 _Glossiness/_Metallic 설정(Unlit/Sprites fallback은 프로퍼티 없어 가드).
            bool pbr = shader.name == "Standard" || shader.name.Contains("Lit");
            if (color.a < 1f)
            {
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                // 날개/반투명: 막·천 느낌(번들거림 억제)
                if (pbr) { mat.SetFloat("_Glossiness", 0.2f); mat.SetFloat("_Smoothness", 0.2f); mat.SetFloat("_Metallic", 0f); }
            }
            else if (pbr)
            {
                // 외골격 키틴 광택 + 미세 금속감 — 전 34종 동시 개선
                mat.SetFloat("_Glossiness", 0.55f);
                mat.SetFloat("_Smoothness", 0.55f);
                mat.SetFloat("_Metallic", 0.15f);
            }
            r.material = mat;
        }

        private Color GetInsectColor()
        {
            if (data == null) return Color.gray;
            uint hash = 0;
            string id = data.insectId ?? "";
            foreach (char c in id) hash = hash * 31 + c;
            float hue = (hash % 360) / 360f;
            float sat = 0.5f + (hash % 100) / 200f;
            float val = 0.6f + (hash % 80) / 200f;

            if (shiny)
            {
                // Shiny: 색조 반전 + 채도 높이기 + 밝기 올리기 + 금빛 틴트
                hue = (hue + 0.5f) % 1f;
                sat = Mathf.Min(1f, sat + 0.2f);
                val = Mathf.Min(1f, val + 0.15f);
            }

            Color baseCol = Color.HSVToRGB(hue, sat, val);
            Color rarityTint = GetRarityColor();
            Color result = Color.Lerp(baseCol, rarityTint, 0.3f);

            if (shiny)
            {
                // 은은한 광택 추가 (색조 반전과 함께 자연스러운 밝기)
                result = Color.Lerp(result, Color.white, 0.15f);
            }

            return result;
        }

        private void CreateNameLabel()
        {
            Transform existing = transform.Find("NameLabel");
            if (existing != null) DestroyImmediate(existing.gameObject);
            if (data == null) return;

            GameObject label = new GameObject("NameLabel");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = new Vector3(0f, 2.5f, 0f);

            TextMesh text = label.AddComponent<TextMesh>();
            string prefix = shiny ? "★ " : "";
            string suffix = shiny ? " ★" : "";
            text.text = $"{prefix}{data.displayName} Lv.{level}{suffix}";
            text.characterSize = 0.2f;
            text.fontSize = 48;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = GetRarityColor();
        }

        private void CreateGroundMarker()
        {
            Transform existing = transform.Find("GroundMarker");
            if (existing != null) DestroyImmediate(existing.gameObject);

            Color color = GetRarityColor();
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "GroundMarker";
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            marker.transform.localScale = new Vector3(2f, 0.02f, 2f);

            Collider mc = marker.GetComponent<Collider>();
            if (mc != null) UnityEngine.Object.Destroy(mc);
            ApplyColor(marker, new Color(color.r, color.g, color.b, 0.5f));
        }

        private Color GetRarityColor()
        {
            if (data == null) return Color.gray;
            switch (data.rarity)
            {
                case InsectRarity.Common:    return new Color(0.55f, 0.45f, 0.3f);
                case InsectRarity.Uncommon:  return new Color(0.3f, 0.7f, 0.3f);
                case InsectRarity.Rare:      return new Color(0.3f, 0.5f, 0.9f);
                case InsectRarity.Epic:      return new Color(0.7f, 0.3f, 0.9f);
                case InsectRarity.Legendary: return new Color(1f, 0.8f, 0.2f);
                default:                     return Color.gray;
            }
        }

        private void BuildAphid(Color body, Color dark)
        {
            // 진딧물: 아주 작고 둥글둥글, 긴 다리, 꿀관
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.35f, 0.3f, 0.4f), body);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.25f), new Vector3(0.2f, 0.18f, 0.2f), dark);
            // 꿀관 (뒤쪽 돌기 2개)
            MakePart("CornicleL", PrimitiveType.Capsule, new Vector3(-0.1f, 0.1f, -0.22f), new Vector3(0.03f, 0.1f, 0.03f),
                body, Quaternion.Euler(-20f, 0f, 10f));
            MakePart("CornicleR", PrimitiveType.Capsule, new Vector3(0.1f, 0.1f, -0.22f), new Vector3(0.03f, 0.1f, 0.03f),
                body, Quaternion.Euler(-20f, 0f, -10f));
            // 긴 가느다란 다리
            for (int i = 0; i < 3; i++)
            {
                float z = -0.05f + i * 0.12f;
                MakePart($"LegL{i}", PrimitiveType.Capsule, new Vector3(-0.18f, -0.15f, z),
                    new Vector3(0.02f, 0.18f, 0.02f), dark, Quaternion.Euler(0f, 0f, 20f));
                MakePart($"LegR{i}", PrimitiveType.Capsule, new Vector3(0.18f, -0.15f, z),
                    new Vector3(0.02f, 0.18f, 0.02f), dark, Quaternion.Euler(0f, 0f, -20f));
            }
            MakeAntennae(dark, 0.25f);
            MakeEyes(0.28f, 0.08f);
        }

        private void BuildAntlion(Color body, Color dark)
        {
            // 개미귀신: 큰 턱, 납작한 몸, 넓은 머리
            MakePart("Body", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.1f), new Vector3(0.4f, 0.2f, 0.6f), body);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.3f), new Vector3(0.4f, 0.22f, 0.35f), dark);
            // 거대한 턱 (집게)
            MakePart("JawL", PrimitiveType.Capsule, new Vector3(-0.12f, 0f, 0.5f), new Vector3(0.05f, 0.2f, 0.05f),
                dark, Quaternion.Euler(-50f, 20f, 0f));
            MakePart("JawR", PrimitiveType.Capsule, new Vector3(0.12f, 0f, 0.5f), new Vector3(0.05f, 0.2f, 0.05f),
                dark, Quaternion.Euler(-50f, -20f, 0f));
            MakePart("JawTipL", PrimitiveType.Sphere, new Vector3(-0.18f, 0.05f, 0.65f), Vector3.one * 0.04f, body);
            MakePart("JawTipR", PrimitiveType.Sphere, new Vector3(0.18f, 0.05f, 0.65f), Vector3.one * 0.04f, body);
            MakeLegs(dark, 3, -0.05f);
            MakeEyes(0.35f, 0.12f);
        }

        private void BuildDungBeetle(Color body, Color dark)
        {
            // 쇠똥구리: 넓적한 몸, 삽 모양 머리, 굵은 앞다리
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.7f, 0.4f, 0.8f), body);
            MakePart("Shell", PrimitiveType.Sphere, new Vector3(0f, 0.15f, -0.05f), new Vector3(0.65f, 0.25f, 0.7f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.45f), new Vector3(0.45f, 0.25f, 0.3f), dark);
            // 삽 모양 머리 돌기
            MakePart("Shovel", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.55f), new Vector3(0.35f, 0.06f, 0.1f), dark);
            // 굵은 앞다리 (삽질용)
            MakePart("DigLegL", PrimitiveType.Capsule, new Vector3(-0.3f, -0.1f, 0.3f), new Vector3(0.1f, 0.2f, 0.1f),
                dark, Quaternion.Euler(0f, 0f, 35f));
            MakePart("DigLegR", PrimitiveType.Capsule, new Vector3(0.3f, -0.1f, 0.3f), new Vector3(0.1f, 0.2f, 0.1f),
                dark, Quaternion.Euler(0f, 0f, -35f));
            // 소똥 (옆에)
            MakePart("DungBall", PrimitiveType.Sphere, new Vector3(0.4f, -0.1f, -0.3f), Vector3.one * 0.25f,
                new Color(0.35f, 0.28f, 0.15f));
            MakeLegs(dark, 2, -0.1f);
            MakeEyes(0.45f, 0.1f);
        }

        private void BuildClickBeetle(Color body, Color dark)
        {
            // 방아벌레: 길쭉한 몸, 뾰족한 모서리, 도약 장치(전흉)
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.25f, 0.5f, 0.25f), body,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Shell", PrimitiveType.Cube, new Vector3(0f, 0.1f, -0.1f), new Vector3(0.22f, 0.08f, 0.6f), dark);
            MakePart("ShellLine", PrimitiveType.Cylinder, new Vector3(0f, 0.14f, -0.1f), new Vector3(0.01f, 0.01f, 0.55f), body);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.35f), new Vector3(0.22f, 0.18f, 0.2f), dark);
            // 전흉 (클릭 장치)
            MakePart("Pronotum", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0.2f), new Vector3(0.24f, 0.1f, 0.15f), body);
            MakePart("ClickSpine", PrimitiveType.Capsule, new Vector3(0f, -0.02f, 0.15f), new Vector3(0.04f, 0.06f, 0.04f),
                body, Quaternion.Euler(90f, 0f, 0f));
            MakeLegs(dark, 3, 0f);
            MakeAntennae(dark, 0.3f);
            MakeEyes(0.38f, 0.08f);
        }

        private void AddRarityEffects()
        {
            if (data == null) return;

            if (data.rarity == InsectRarity.Epic)
            {
                // Epic: 은은한 보라 오라
                GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                aura.name = "EpicAura";
                aura.transform.SetParent(transform, false);
                aura.transform.localPosition = Vector3.zero;
                aura.transform.localScale = Vector3.one * 1.3f;
                Collider c = aura.GetComponent<Collider>();
                if (c != null) Destroy(c);
                ApplyColor(aura, new Color(0.6f, 0.2f, 0.8f, 0.08f));
            }
            else if (data.rarity == InsectRarity.Legendary)
            {
                // Legendary: 금색 오라 + 빛나는 링
                GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                aura.name = "LegendaryAura";
                aura.transform.SetParent(transform, false);
                aura.transform.localPosition = Vector3.zero;
                aura.transform.localScale = Vector3.one * 1.5f;
                Collider c = aura.GetComponent<Collider>();
                if (c != null) Destroy(c);
                ApplyColor(aura, new Color(1f, 0.85f, 0.2f, 0.1f));

                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "LegendaryRing";
                ring.transform.SetParent(transform, false);
                ring.transform.localPosition = new Vector3(0f, -0.05f, 0f);
                ring.transform.localScale = new Vector3(1.2f, 0.01f, 1.2f);
                Collider rc = ring.GetComponent<Collider>();
                if (rc != null) Destroy(rc);
                ApplyColor(ring, new Color(1f, 0.8f, 0.15f, 0.3f));
            }
        }

        private float GetRarityScale()
        {
            if (data == null) return 1.2f;
            switch (data.rarity)
            {
                case InsectRarity.Common:    return 1.0f;
                case InsectRarity.Uncommon:  return 1.2f;
                case InsectRarity.Rare:      return 1.4f;
                case InsectRarity.Epic:      return 1.6f;
                case InsectRarity.Legendary: return 1.9f;
                default:                     return 1.2f;
            }
        }

        public void Despawn()
        {
            // 다중 호출 가드 — Battle/Capture가 동시에 Despawn 호출 시 풀 중복 반환 차단.
            // 옛은 onDespawn 두 번 발화 → 풀이 같은 객체 두 번 Return → 다음 Get에서 같은 인스턴스 2번 회귀.
            if (despawnedThisCycle) return;
            despawnedThisCycle = true;

            // 풀 반환 전 진행 중 코루틴 정리 (다음 인스턴스 사용 시 잔존 영향 방지)
            StopAllCoroutines();
            if (ownerPoint != null)
                ownerPoint.NotifyDespawned();
            onDespawn?.Invoke(this);
        }
    }
}
