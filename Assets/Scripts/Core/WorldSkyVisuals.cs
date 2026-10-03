using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 하늘의 "그려지는 부분" — 스카이박스 머티리얼 인스턴스, 밤별, 흐린 날의 연무 막. <see cref="SubAreaEnvironment"/>가 소유하고 구동한다.
    ///
    /// <b>에셋을 고치지 않는다.</b> 씬의 스카이박스(<c>Default-Skybox</c>)를 <c>new Material(원본)</c>으로 복제해
    /// <c>RenderSettings.skybox</c>에 걸고, 끝나면 원본을 되돌려 놓고 복제본을 파괴한다. 셰이더 키워드는 건드리지 않는다
    /// (빌드에서 변형이 빠지는 함정 — 여기서 바꾸는 건 노출·땅 색 값뿐이다).
    ///
    /// <b>대기 두께는 건드리지 않는다.</b> 흐린 하늘을 뿌옇게 하려고 두께를 2.6배로 올렸더니 지평선이 진한 주황·금색이 됐다
    /// (2026-10-03 캡처, 정오 눈) — 절차적 스카이박스의 레일리 계수가 두께의 2.5제곱이라 긴 경로에서 파랑이 다 빠진다.
    /// 흐림은 대신 <b>연무 돔</b>이 맡는다: 카메라를 따라다니는 큰 구에 지금 프레임의 안개색을 칠하고, 지평선 쪽이 짙은 세로 알파
    /// (<see cref="WorldSkyRules.VeilProfile"/>) × 연무 짙기(<see cref="WorldSkyRules.SkyFrame.skyHaze"/>)로 하늘을 덮는다.
    /// 안개가 원경 산맥을 덮는 색과 같은 색이라 산이 지평선에서 끊김 없이 하늘로 녹는다. 맑은 날은 짙기 0이라 돔이 꺼진다(비용 0).
    ///
    /// <b>그리는 순서.</b> 스카이박스는 불투명 물체 뒤(큐 2500 다음)에 그려지고, 별(큐 2501) → 연무(2502) → 나머지 반투명(3000~) 순이다.
    /// 두 돔 다 깊이를 쓰지 않고 깊이 검사만 하므로 반경(먼 평면의 90%)보다 가까운 산·구름·지형이 앞에 남는다. 그보다 먼 것은
    /// 리전 연무에 이미 묻혀 안개색이다. 큐를 3000보다 앞에 둔 이유: 돔의 중심이 카메라라 같은 큐의 거리 정렬에서 늘 "가장 가까운 것"이 되어
    /// 맨 나중에 그려진다 — 물·유리 같은 반투명이 하늘을 배경으로 설 때 그 위를 덮어 버린다. 연무가 별보다 뒤라 구름이 별을 가린다.
    /// <c>Sprites/Default</c>는 안개를 안 받으므로(별이 900m에서도 보이는 이유) 돔 색이 곧 화면 색이다.
    ///
    /// <b>게임 카메라는 고각(약 56° 아래)이라 평소엔 하늘이 거의 안 보인다.</b> 지평선이 보이는 건 낮은 구도(배치 캡처·전투 연출)와
    /// 먼 곳이다. 그래서 둘 다 싼 것으로 족하다 — 별은 쿼드 170장, 연무는 고리 11개×32칸 구 하나로 각각 드로우콜 한 번이고,
    /// 필요 없을 때는 GameObject째 꺼진다. 달은 따로 안 그린다: 절차적 스카이박스의 해 원반이 디렉셔널 라이트(밤엔 달빛) 방향·색으로
    /// 그려지므로 그것이 달이다. 흐린 날엔 그 원반이 연무 너머로 흐릿하게 비친다.
    /// </summary>
    internal sealed class WorldSkyVisuals
    {
        private const int StarCount = 170;
        private const int StarSeed = 20261003;
        /// <summary>하늘 돔 반지름이 카메라 먼 평면의 이 비율 — 원경 산맥(월드 경계 ±520m)보다 멀어야 산이 별·연무 앞에 선다.</summary>
        private const float DomeFarFraction = 0.9f;
        private const float DomeMaxRadius = 900f;

        /// <summary>스카이박스(불투명 다음) 바로 뒤, 다른 반투명(3000)보다 앞.</summary>
        private const int StarQueue = 2501;
        private const int VeilQueue = 2502;

        /// <summary>연무 막을 켜는 최소 알파 — 그 아래는 꺼서 맑은 날 비용이 0이다.</summary>
        private const float VeilMinAlpha = 0.01f;
        private const int VeilSegments = 32;
        /// <summary>연무 돔의 고리 고도(도). 아래 반구는 한 고리로 덮고(늘 가장 짙다) 지평선 위를 촘촘히 나눠 세로 분포를 따라간다.</summary>
        private static readonly float[] VeilRings = { -90f, -12f, 0f, 8f, 16f, 26f, 38f, 50f, 60f, 75f, 90f };

        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int GroundId = Shader.PropertyToID("_GroundColor");

        // ── 스카이박스 ──
        private Material originalSky;
        private Material skyMat;
        private float baseExposure = 1.3f;
        private Color baseGround = new Color(0.369f, 0.349f, 0.341f);
        private bool hasExposure, hasGround;

        // ── 별 ──
        private GameObject starRoot;
        private MeshFilter starFilter;
        private Material starMat;
        private Texture2D dot;
        private bool starsFailed;
        private float lastStarAlpha = -1f;

        // ── 연무 막 ──
        private GameObject veilRoot;
        private MeshFilter veilFilter;
        private Material veilMat;
        private bool veilFailed;
        private Color lastVeilColor = new Color(-1f, -1f, -1f, -1f);

        /// <summary>씬의 스카이박스를 복제해 건다. 스카이박스가 없으면(없는 씬·테스트) 조용히 넘어간다.</summary>
        public void CreateSkybox()
        {
            if (skyMat != null) return;
            originalSky = RenderSettings.skybox;
            if (originalSky == null) return;

            skyMat = new Material(originalSky) { name = "~RuntimeSkybox" };
            hasExposure = skyMat.HasProperty(ExposureId);
            hasGround = skyMat.HasProperty(GroundId);
            if (hasExposure) baseExposure = skyMat.GetFloat(ExposureId);
            if (hasGround) baseGround = skyMat.GetColor(GroundId);
            RenderSettings.skybox = skyMat;
        }

        /// <summary>
        /// 하늘 한 프레임을 스카이박스·별·연무 막에 쓴다. <paramref name="strength"/> 0이면 원본 값(돔 꺼짐), 1이면 완전 보정.
        /// <paramref name="veilColor"/>는 이번 프레임에 실제로 쓴 안개색이다 — 연무 막이 원경 안개와 같은 색이어야 지평선이 녹는다.
        /// </summary>
        public void Apply(WorldSkyRules.SkyFrame f, float strength, Camera camera, Color veilColor)
        {
            strength = Mathf.Clamp01(strength);

            if (skyMat != null)
            {
                if (hasExposure)
                    skyMat.SetFloat(ExposureId, Mathf.Lerp(baseExposure, baseExposure * f.skyExposure, strength));
                if (hasGround)
                {
                    // 지평선 아래(보일 일은 드물다)가 한밤에 하얗게 남지 않게 노출을 따라 어둡힌다
                    float dim = Mathf.Lerp(1f, 0.25f + 0.75f * Mathf.Clamp01(f.skyExposure), strength);
                    skyMat.SetColor(GroundId, new Color(baseGround.r * dim, baseGround.g * dim, baseGround.b * dim, baseGround.a));
                }
            }

            ApplyStars(f.starAlpha * strength, camera);
            ApplyVeil(Mathf.Clamp01(f.skyHaze) * strength, veilColor, camera);
        }

        /// <summary>켜져 있는 돔(별·연무)을 카메라에 붙인다 — 꺼져 있으면 아무 일도 안 한다.</summary>
        public void Follow(Camera camera)
        {
            if (camera == null) return;
            bool stars = starRoot != null && starRoot.activeSelf;
            bool veil = veilRoot != null && veilRoot.activeSelf;
            if (!stars && !veil) return;

            float radius = Mathf.Min(DomeMaxRadius, camera.farClipPlane * DomeFarFraction);
            Vector3 position = camera.transform.position;
            var scale = new Vector3(radius, radius, radius);
            if (stars)
            {
                starRoot.transform.position = position;
                starRoot.transform.localScale = scale;
            }
            if (veil)
            {
                veilRoot.transform.position = position;
                veilRoot.transform.localScale = scale;
            }
        }

        public void Dispose()
        {
            if (skyMat != null)
            {
                if (RenderSettings.skybox == skyMat) RenderSettings.skybox = originalSky;
                Object.Destroy(skyMat);
                skyMat = null;
            }
            if (starRoot != null) Object.Destroy(starRoot);
            starRoot = null;
            if (starMat != null) Object.Destroy(starMat);
            starMat = null;
            if (starFilter != null && starFilter.sharedMesh != null) Object.Destroy(starFilter.sharedMesh);
            starFilter = null;
            if (dot != null) Object.Destroy(dot);
            dot = null;

            if (veilRoot != null) Object.Destroy(veilRoot);
            veilRoot = null;
            if (veilMat != null) Object.Destroy(veilMat);   // 텍스처는 내장 흰색이라 파괴하지 않는다
            veilMat = null;
            if (veilFilter != null && veilFilter.sharedMesh != null) Object.Destroy(veilFilter.sharedMesh);
            veilFilter = null;
        }

        // ── 별 ──

        private void ApplyStars(float alpha, Camera camera)
        {
            bool want = alpha > 0.01f;
            if (!want)
            {
                if (starRoot != null && starRoot.activeSelf) starRoot.SetActive(false);
                lastStarAlpha = -1f;
                return;
            }
            if (starsFailed) return;
            if (starRoot == null && !BuildStars()) return;

            if (!starRoot.activeSelf) starRoot.SetActive(true);
            // 알파를 매 틱 쓰지 않는다 — 0.02 이상 바뀔 때만
            if (Mathf.Abs(alpha - lastStarAlpha) > 0.02f)
            {
                starMat.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                lastStarAlpha = alpha;
            }
            Follow(camera);
        }

        private bool BuildStars()
        {
            dot = SkyFx.CreateSoftDot(32);
            starMat = SkyFx.CreateMaterial(dot, "~StarMat");
            if (starMat == null)
            {
                starsFailed = true;
                if (dot != null) Object.Destroy(dot);
                dot = null;
                return false;
            }
            starMat.renderQueue = StarQueue;

            starRoot = new GameObject("~SkyStars");
            starFilter = starRoot.AddComponent<MeshFilter>();
            starFilter.sharedMesh = BuildStarMesh();
            ConfigureRenderer(starRoot.AddComponent<MeshRenderer>(), starMat);
            starRoot.SetActive(false);
            return true;
        }

        private static void ConfigureRenderer(MeshRenderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// 단위 구 위쪽 반구에 별 쿼드 <see cref="StarCount"/>장. 쿼드는 구 중심을 향한다. 위치 시드가 고정이라 매번 같은 별자리다.
        /// 각크기 0.16~0.34°, 알파 0.45~1 — 밝은 별 몇 개와 흐린 별 다수.
        /// </summary>
        private static Mesh BuildStarMesh()
        {
            var rng = new System.Random(StarSeed);
            var verts = new Vector3[StarCount * 4];
            var colors = new Color[StarCount * 4];
            var uvs = new Vector2[StarCount * 4];
            var tris = new int[StarCount * 6];

            float minElevation = Mathf.Sin(6f * Mathf.Deg2Rad);
            for (int i = 0; i < StarCount; i++)
            {
                float sinEl = minElevation + (float)rng.NextDouble() * (1f - minElevation);
                float cosEl = Mathf.Sqrt(Mathf.Max(0f, 1f - sinEl * sinEl));
                float az = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(az) * cosEl, sinEl, Mathf.Sin(az) * cosEl);

                Vector3 right = Vector3.Cross(Vector3.up, dir);
                if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
                right.Normalize();
                Vector3 up = Vector3.Cross(dir, right);

                float half = Mathf.Tan(Mathf.Lerp(0.08f, 0.17f, (float)rng.NextDouble()) * Mathf.Deg2Rad);
                float bright = (float)rng.NextDouble();
                bright = 0.45f + 0.55f * bright * bright;
                // 대부분 푸른 흰색, 몇 개는 따뜻한 색
                Color tint = rng.NextDouble() < 0.15 ? new Color(1f, 0.9f, 0.75f, bright) : new Color(0.85f, 0.92f, 1f, bright);

                int v = i * 4;
                verts[v] = dir - right * half - up * half;
                verts[v + 1] = dir + right * half - up * half;
                verts[v + 2] = dir + right * half + up * half;
                verts[v + 3] = dir - right * half + up * half;
                uvs[v] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(0f, 1f);
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = tint;

                int t = i * 6;
                // 구 안쪽(중심의 카메라)에서 보이는 면이 앞면이 되게 감는다 — 셰이더는 Cull Off라 어느 쪽이든 그려지지만 일관되게
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "~SkyStarsMesh" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris;
            // 카메라를 따라다니는 단위 구 — 경계를 넉넉히 잡아 절두체 컬링에 잘리지 않게 한다
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2.2f);
            return mesh;
        }

        // ── 연무 막 ──

        /// <summary>
        /// 연무 막을 켜고 끄고 칠한다. 색 = 안개색, 알파 = 짙기 × 정점 알파(세로 분포). 색은 0.004 넘게 바뀔 때만 쓴다
        /// (하늘 틱이 0.1초라 그대로 써도 싸지만, 시간이 멈춘 검수 중에는 아예 안 쓰게).
        /// </summary>
        private void ApplyVeil(float alpha, Color color, Camera camera)
        {
            if (alpha <= VeilMinAlpha)
            {
                if (veilRoot != null && veilRoot.activeSelf) veilRoot.SetActive(false);
                lastVeilColor = new Color(-1f, -1f, -1f, -1f);
                return;
            }
            if (veilFailed) return;
            if (veilRoot == null && !BuildVeil()) return;

            if (!veilRoot.activeSelf) veilRoot.SetActive(true);
            var target = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
            if (Mathf.Abs(target.r - lastVeilColor.r) > 0.004f || Mathf.Abs(target.g - lastVeilColor.g) > 0.004f
                || Mathf.Abs(target.b - lastVeilColor.b) > 0.004f || Mathf.Abs(target.a - lastVeilColor.a) > 0.004f)
            {
                veilMat.color = target;
                lastVeilColor = target;
            }
            Follow(camera);
        }

        private bool BuildVeil()
        {
            // 텍스처는 내장 흰색 — 색은 전부 정점 알파와 머티리얼 색이 낸다
            veilMat = SkyFx.CreateMaterial(Texture2D.whiteTexture, "~SkyVeil");
            if (veilMat == null)
            {
                veilFailed = true;
                return false;
            }
            veilMat.renderQueue = VeilQueue;

            veilRoot = new GameObject("~SkyVeil");
            veilFilter = veilRoot.AddComponent<MeshFilter>();
            veilFilter.sharedMesh = BuildVeilMesh();
            ConfigureRenderer(veilRoot.AddComponent<MeshRenderer>(), veilMat);
            veilRoot.SetActive(false);
            return true;
        }

        /// <summary>
        /// 단위 구 — 고리(<see cref="VeilRings"/>) × <see cref="VeilSegments"/>칸. 정점 색은 흰색, 알파는 그 고리 고도의
        /// <see cref="WorldSkyRules.VeilProfile"/>. 땅 쪽 반구도 덮는다 — 월드 경계 밖 빈 곳에 스카이박스 땅색이 비치지 않게.
        /// </summary>
        private static Mesh BuildVeilMesh()
        {
            int rings = VeilRings.Length;
            int cols = VeilSegments + 1;
            var verts = new Vector3[rings * cols];
            var colors = new Color[rings * cols];
            var uvs = new Vector2[rings * cols];
            var tris = new int[(rings - 1) * VeilSegments * 6];

            for (int r = 0; r < rings; r++)
            {
                float el = VeilRings[r] * Mathf.Deg2Rad;
                float y = Mathf.Sin(el);
                float h = Mathf.Cos(el);
                float alpha = WorldSkyRules.VeilProfile(VeilRings[r]);
                for (int s = 0; s < cols; s++)
                {
                    float az = (float)s / VeilSegments * Mathf.PI * 2f;
                    int i = r * cols + s;
                    verts[i] = new Vector3(Mathf.Cos(az) * h, y, Mathf.Sin(az) * h);
                    colors[i] = new Color(1f, 1f, 1f, alpha);
                    uvs[i] = new Vector2((float)s / VeilSegments, (float)r / (rings - 1));
                }
            }

            int t = 0;
            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < VeilSegments; s++)
                {
                    int a = r * cols + s;
                    int b = a + cols;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            }

            var mesh = new Mesh { name = "~SkyVeilMesh" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2.2f);
            return mesh;
        }
    }

    /// <summary>
    /// 하늘·날씨 입자용 재료. <c>Sprites/Default</c>는 Always Included Shaders에 든 셰이더라 플레이어 빌드에 항상 있고
    /// (알파 블렌드·ZWrite Off·정점 색 지원) 키워드 변형이 없다 — Standard의 반투명 변형이 빌드에서 불투명으로 빠지는 함정이 없다.
    /// 전투 이펙트(<c>BattleArenaController.CreateFxMaterial</c>)가 폴백으로 쓰는 셰이더와 같다.
    /// </summary>
    internal static class SkyFx
    {
        /// <summary>가장자리가 부드럽게 사라지는 흰 원 — 별·눈송이·빗줄기가 같은 텍스처를 색과 크기만 바꿔 쓴다.</summary>
        public static Texture2D CreateSoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "~SoftDot",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>알파 블렌드 입자 머티리얼. 셰이더를 못 찾으면 null — 호출부가 그 효과를 끈다.</summary>
        public static Material CreateMaterial(Texture2D texture, string name)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            var mat = new Material(shader) { name = name };
            mat.mainTexture = texture;
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }
    }
}
