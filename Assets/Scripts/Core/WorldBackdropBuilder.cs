using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 원경 — 월드 둘레의 산맥과 하늘의 뭉게구름.
    ///
    /// <b>옛 원경은 월드를 1.5배로 넓히기 전 좌표에 박혀 있었다.</b> 「먼 산」 네 개(지름 60~90m,
    /// 높이 13~20m 납작 구)가 넓힌 뒤엔 꽃밭·유적·연못 바로 옆 필드 안쪽에 떨어져, 게임 카메라에
    /// 올리브색 돔으로 화면 절반을 덮었다(배치 캡처로 확인). 구름도 납작한 구 하나씩이라 회색 원반으로 보였다.
    ///
    /// 그래서 좌표를 적지 않는다 — **리전 원과 길(<see cref="WorldRouteLayout"/>)을 피해 바깥으로 훑어 나가며
    /// 처음 비는 자리**에 산을 세운다. 리전을 늘리거나 옮겨도 산맥이 필드를 덮지 않는다.
    /// 산의 모양·색은 가장 가까운 리전을 따른다(서릿길 옆은 눈 덮인 봉우리, 모래언덕 옆은 사암 대지…).
    ///
    /// 콜라이더는 없다. 전부 테두리 울타리 바깥이라 닿을 일이 없고, 있으면 카메라 차폐 검사만 무거워진다.
    /// 머티리얼별로 메시를 합쳐 산맥 전체가 드로우콜 몇 번으로 끝난다(모바일).
    /// 이름을 <c>Scenery_</c>로 시작하는 건 서브에리어 진입 때 <c>SubAreaWorldBuilder.HideMainWorld</c>가
    /// 접두어로 메인 월드를 끄기 때문이다.
    /// </summary>
    public class WorldBackdropBuilder : MonoBehaviour
    {
        /// <summary>배치 난수 시드 — 원경이 실행마다 같게. 전역 난수 상태는 끝나면 되돌린다.</summary>
        private const int LayoutSeed = 20260928;
        /// <summary>경계 벽(±520) 안쪽. 산 중심이 이 안에 있어야 벽 앞을 가린다.</summary>
        private const float BoundaryHalf = 500f;
        /// <summary>리전 원 가장자리와 산 기슭 사이 최소 여백(m).</summary>
        private const float RegionClearance = 26f;
        private const int Directions = 44;

        private enum Style { Dome, Peak, Mesa }

        private struct Theme
        {
            public Style style;
            public Color body;
            public Color cap;
            public float capFrom;     // 0~1 — 이 높이 비율부터 위를 cap 색으로 덮는다(1이면 cap 없음)
            public Vector2 height;    // m
            public Vector2 radius;    // m
            public bool capGlow;      // 잿불 분화구처럼 은은히 빛나는 cap
        }

        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();

        public void Build(RegionData[] regions)
        {
            if (regions == null || regions.Length == 0) return;
            Random.State prev = Random.state;
            Random.InitState(LayoutSeed);
            try
            {
                BuildRanges(regions);
                BuildClouds(regions);
            }
            finally
            {
                Random.state = prev;
            }
        }

        // ======= 산맥 =======

        private void BuildRanges(RegionData[] regions)
        {
            Vector3 origin = Vector3.zero;
            foreach (RegionData r in regions) origin += r.centerPosition;
            origin /= regions.Length;
            origin.y = 0f;

            // 색 + 표면(발광)으로 묶는다 — 색만 키로 쓰면 어느 리전의 빛나는 cap과 같은 색인 다른 리전의 몸체가
            // 한 메시로 합쳐져 함께 빛난다(SceneryMaterialKey 주석).
            var batches = new Dictionary<SceneryMaterialKey, List<CombineInstance>>();
            var placed = new List<Vector4>();   // xyz = 위치, w = 반경 — 산끼리 너무 겹치지 않게

            for (int i = 0; i < Directions; i++)
            {
                float angle = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / Directions;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                // 앞줄(낮고 가까움) + 뒷줄(높고 멂) — 겹쳐야 산'맥'으로 읽힌다
                for (int row = 0; row < 2; row++)
                {
                    float start = row == 0 ? 180f : 0f;
                    if (row == 1)
                    {
                        Vector4 last = placed.Count > 0 ? placed[placed.Count - 1] : Vector4.zero;
                        if (placed.Count == 0 || Vector3.Dot(((Vector3)last - origin).normalized, dir) < 0.95f) break;
                        start = ((Vector3)last - origin).magnitude + last.w * 0.9f;
                    }

                    if (!TryPlace(regions, origin, dir, start, placed, out Vector3 pos, out Theme theme, row))
                        break;

                    float radius = Random.Range(theme.radius.x, theme.radius.y);
                    float height = Random.Range(theme.height.x, theme.height.y) * (row == 1 ? 1.35f : 1f);
                    placed.Add(new Vector4(pos.x, 0f, pos.z, radius));

                    int seed = Random.Range(int.MinValue, int.MaxValue);
                    Mesh body = MountainMesh(theme.style, radius, height, seed, 0f, theme.capFrom);
                    Add(batches, SceneryMaterialKey.Matte(theme.body), body, pos);
                    if (theme.capFrom < 0.999f)
                    {
                        Mesh cap = MountainMesh(theme.style, radius, height, seed, theme.capFrom, 1f);
                        Add(batches, theme.capGlow ? SceneryMaterialKey.Glowing(theme.cap, theme.cap * 0.55f)
                            : SceneryMaterialKey.Matte(theme.cap), cap, pos);
                    }
                }
            }

            Transform root = new GameObject("Scenery_BackdropRanges").transform;
            root.SetParent(transform, false);
            int n = 0;
            foreach (var kv in batches)
            {
                var combined = new Mesh { name = "BackdropRange", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                combined.CombineMeshes(kv.Value.ToArray(), true, true);
                combined.RecalculateBounds();
                combined.UploadMeshData(true);   // 정적 원경 — CPU 사본 불필요
                meshes.Add(combined);
                foreach (CombineInstance ci in kv.Value) Destroy(ci.mesh);

                Material mat = SceneryMaterials.Create(kv.Key.Albedo);
                if (kv.Key.Glow) SceneryMaterials.SetEmission(mat, kv.Key.Emission);
                materials.Add(mat);

                // 칸에는 Scenery_ 접두어를 달지 않는다 — HideMainWorld가 루트만 끄면 되고, 칸까지 걸리면 진입마다 전수로 끈다
                var go = new GameObject($"BackdropRange_{n++}");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = combined;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 원경 그림자는 비용만 든다
                mr.receiveShadows = false;
            }
        }

        /// <summary>
        /// dir 방향으로 start부터 바깥으로 훑어 리전·길·다른 산과 겹치지 않는 첫 자리를 찾는다.
        /// </summary>
        private static bool TryPlace(RegionData[] regions, Vector3 origin, Vector3 dir, float start,
            List<Vector4> placed, out Vector3 pos, out Theme theme, int row)
        {
            pos = Vector3.zero;
            theme = default;
            for (float d = start; d < 760f; d += 10f)
            {
                Vector3 p = origin + dir * d;
                if (Mathf.Abs(p.x) > BoundaryHalf || Mathf.Abs(p.z) > BoundaryHalf) return false;

                RegionData near = NearestRegion(regions, p, out float edgeDist);
                if (near == null) return false;
                theme = ThemeFor(near.regionId);
                float foot = theme.radius.y * (row == 1 ? 1.1f : 0.9f);
                if (edgeDist < foot + RegionClearance) continue;
                if (WorldRouteLayout.IsOnRoute(regions, p, foot)) continue;

                bool crowded = false;
                foreach (Vector4 q in placed)
                {
                    float gap = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z));
                    if (gap < (q.w + foot) * 0.55f) { crowded = true; break; }
                }
                if (crowded) continue;

                pos = new Vector3(p.x, -0.6f, p.z);   // 기슭을 살짝 묻어 떠 보이지 않게
                return true;
            }
            return false;
        }

        private static RegionData NearestRegion(RegionData[] regions, Vector3 p, out float edgeDistance)
        {
            RegionData best = null;
            edgeDistance = float.MaxValue;
            foreach (RegionData r in regions)
            {
                if (r == null) continue;
                Vector3 c = r.centerPosition;
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z)) - r.radius;
                if (d < edgeDistance) { edgeDistance = d; best = r; }
            }
            return best;
        }

        private static Theme ThemeFor(string regionId)
        {
            switch (regionId)
            {
                case "frostline":
                    return new Theme { style = Style.Peak, body = new Color(0.52f, 0.58f, 0.66f), cap = new Color(0.93f, 0.95f, 0.98f),
                        capFrom = 0.38f, height = new Vector2(48f, 72f), radius = new Vector2(30f, 44f) };
                case "mountain":
                    return new Theme { style = Style.Peak, body = new Color(0.44f, 0.42f, 0.40f), cap = new Color(0.92f, 0.93f, 0.95f),
                        capFrom = 0.66f, height = new Vector2(44f, 66f), radius = new Vector2(30f, 44f) };
                case "emberfall":
                    return new Theme { style = Style.Peak, body = new Color(0.22f, 0.19f, 0.19f), cap = new Color(0.62f, 0.24f, 0.12f),
                        capFrom = 0.84f, height = new Vector2(40f, 58f), radius = new Vector2(30f, 42f), capGlow = true };
                case "dunes":
                    return new Theme { style = Style.Mesa, body = new Color(0.80f, 0.62f, 0.42f), cap = new Color(0.70f, 0.50f, 0.33f),
                        capFrom = 0.72f, height = new Vector2(20f, 32f), radius = new Vector2(30f, 44f) };
                case "ruins":
                    return new Theme { style = Style.Mesa, body = new Color(0.55f, 0.50f, 0.43f), cap = new Color(0.40f, 0.48f, 0.30f),
                        capFrom = 0.9f, height = new Vector2(24f, 36f), radius = new Vector2(30f, 42f) };
                case "forest":
                case "canopy":
                    return new Theme { style = Style.Dome, body = new Color(0.24f, 0.40f, 0.20f), cap = new Color(0.16f, 0.32f, 0.15f),
                        capFrom = 0.45f, height = new Vector2(26f, 40f), radius = new Vector2(32f, 46f) };
                case "swamp":
                    return new Theme { style = Style.Dome, body = new Color(0.30f, 0.34f, 0.22f), cap = new Color(0.23f, 0.29f, 0.18f),
                        capFrom = 0.55f, height = new Vector2(16f, 24f), radius = new Vector2(30f, 42f) };
                case "hollow":
                    return new Theme { style = Style.Dome, body = new Color(0.56f, 0.55f, 0.48f), cap = new Color(0.50f, 0.49f, 0.43f),
                        capFrom = 0.6f, height = new Vector2(16f, 26f), radius = new Vector2(30f, 42f) };
                case "nameless":
                    return new Theme { style = Style.Peak, body = new Color(0.37f, 0.35f, 0.41f), cap = new Color(0.30f, 0.29f, 0.34f),
                        capFrom = 0.7f, height = new Vector2(34f, 52f), radius = new Vector2(28f, 40f) };
                default: // meadow · pond · garden — 완만한 초록 구릉
                    return new Theme { style = Style.Dome, body = new Color(0.38f, 0.54f, 0.29f), cap = new Color(0.30f, 0.46f, 0.23f),
                        capFrom = 0.55f, height = new Vector2(18f, 28f), radius = new Vector2(32f, 46f) };
            }
        }

        private static void Add(Dictionary<SceneryMaterialKey, List<CombineInstance>> batches, SceneryMaterialKey key, Mesh mesh, Vector3 pos)
        {
            if (!batches.TryGetValue(key, out List<CombineInstance> list))
            {
                list = new List<CombineInstance>();
                batches[key] = list;
            }
            list.Add(new CombineInstance
            {
                mesh = mesh,
                transform = Matrix4x4.TRS(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one),
            });
        }

        /// <summary>
        /// 각진(flat-shaded) 저폴리 봉우리. 같은 seed로 <paramref name="from"/>~<paramref name="to"/> 높이
        /// 구간만 뽑으면 몸체와 cap(눈·숲·분화구)이 같은 윤곽을 공유한다 — cap은 바깥으로 3% 부풀려 몸체를 덮는다.
        /// </summary>
        private static Mesh MountainMesh(Style style, float radius, float height, int seed, float from, float to)
        {
            var rng = new System.Random(seed);
            float[] levels;
            float[] radii;
            switch (style)
            {
                case Style.Mesa:
                    levels = new[] { 0f, 0.28f, 0.62f, 0.72f, 0.96f };
                    radii = new[] { 1f, 0.86f, 0.74f, 0.70f, 0.64f };
                    break;
                case Style.Dome:
                    levels = new[] { 0f, 0.28f, 0.55f, 0.8f, 0.95f };
                    radii = new[] { 1f, 0.9f, 0.7f, 0.44f, 0.2f };
                    break;
                default:
                    levels = new[] { 0f, 0.3f, 0.58f, 0.8f, 0.93f };
                    radii = new[] { 1f, 0.68f, 0.4f, 0.2f, 0.08f };
                    break;
            }

            const int segments = 11;
            int ringCount = levels.Length;
            var rings = new Vector3[ringCount][];
            float jag = style == Style.Peak ? 0.16f : 0.08f;
            for (int k = 0; k < ringCount; k++)
            {
                rings[k] = new Vector3[segments];
                for (int s = 0; s < segments; s++)
                {
                    float a = (s + (float)(rng.NextDouble() - 0.5) * 0.35f) * Mathf.PI * 2f / segments;
                    float rr = radius * radii[k] * (1f + (float)(rng.NextDouble() - 0.5) * 2f * jag);
                    float y = height * levels[k] * (1f + (k == 0 ? 0f : (float)(rng.NextDouble() - 0.5) * 0.08f));
                    rings[k][s] = new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                }
            }
            Vector3 apex = style == Style.Mesa
                ? new Vector3(0f, height * 0.97f, 0f)
                : new Vector3((float)(rng.NextDouble() - 0.5) * radius * 0.15f, height, (float)(rng.NextDouble() - 0.5) * radius * 0.15f);

            bool isCap = from > 0.001f;
            float inflate = isCap ? 1.03f : 1f;
            var verts = new List<Vector3>();
            var tris = new List<int>();

            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i0 = verts.Count;
                verts.Add(Inflate(a, inflate, isCap));
                verts.Add(Inflate(b, inflate, isCap));
                verts.Add(Inflate(c, inflate, isCap));
                tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            }

            for (int k = 0; k < ringCount - 1; k++)
            {
                float mid = (levels[k] + levels[k + 1]) * 0.5f;
                if (mid < from || mid > to) continue;
                for (int s = 0; s < segments; s++)
                {
                    int n = (s + 1) % segments;
                    Vector3 a = rings[k][s], b = rings[k][n], c = rings[k + 1][s], d = rings[k + 1][n];
                    Tri(a, c, b);
                    Tri(b, c, d);
                }
            }
            float topMid = (levels[ringCount - 1] + 1f) * 0.5f;
            if (topMid >= from && topMid <= to)
            {
                for (int s = 0; s < segments; s++)
                {
                    int n = (s + 1) % segments;
                    Tri(rings[ringCount - 1][s], apex, rings[ringCount - 1][n]);
                }
            }

            var mesh = new Mesh { name = isCap ? "BackdropCap" : "BackdropBody" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Inflate(Vector3 v, float k, bool lift)
        {
            return new Vector3(v.x * k, v.y + (lift ? 0.25f : 0f), v.z * k);
        }

        // ======= 구름 =======

        /// <summary>
        /// 뭉게구름 — 크기가 다른 구 5~8개를 겹친 무리. 아랫면이 환경광만 받아 회색으로 칙칙해지지 않게
        /// 은은한 자체 발광을 준다. 전부 한 메시로 합쳐 드로우콜 1번.
        /// </summary>
        private void BuildClouds(RegionData[] regions)
        {
            Vector3 min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, 0f, float.MinValue);
            foreach (RegionData r in regions)
            {
                min = Vector3.Min(min, r.centerPosition - new Vector3(r.radius, 0f, r.radius));
                max = Vector3.Max(max, r.centerPosition + new Vector3(r.radius, 0f, r.radius));
            }

            Mesh puff = ProcMeshLibrary.LowSphere(0.5f, 0.5f, 0.5f, 7, 12);
            var parts = new List<CombineInstance>();
            const int clusters = 16;
            for (int i = 0; i < clusters; i++)
            {
                Vector3 c = new Vector3(Random.Range(min.x - 60f, max.x + 60f), Random.Range(62f, 84f),
                    Random.Range(min.z - 60f, max.z + 60f));
                float size = Random.Range(7f, 12f);
                int puffs = Random.Range(5, 9);
                float yaw = Random.Range(0f, 360f);
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                for (int j = 0; j < puffs; j++)
                {
                    float t = puffs <= 1 ? 0f : (float)j / (puffs - 1) - 0.5f;
                    float s = size * Random.Range(0.6f, 1.15f) * (1f - Mathf.Abs(t) * 0.7f);
                    Vector3 off = rot * new Vector3(t * size * 3.2f, Random.Range(0f, 0.25f) * s, Random.Range(-0.35f, 0.35f) * size);
                    parts.Add(new CombineInstance
                    {
                        mesh = puff,
                        transform = Matrix4x4.TRS(c + off, rot, new Vector3(s * 1.5f, s * 0.8f, s * 1.2f)),
                    });
                }
            }

            var combined = new Mesh { name = "BackdropClouds", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            combined.CombineMeshes(parts.ToArray(), true, true);
            combined.RecalculateBounds();
            combined.UploadMeshData(true);
            meshes.Add(combined);   // puff는 ProcMeshLibrary의 프로세스 캐시 — 파괴 금지

            Material mat = SceneryMaterials.Create(new Color(0.97f, 0.98f, 1f));
            SceneryMaterials.SetEmission(mat, new Color(0.42f, 0.44f, 0.48f));
            materials.Add(mat);

            var go = new GameObject("Scenery_BackdropClouds");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = combined;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private void OnDestroy()
        {
            foreach (Material m in materials) if (m != null) Destroy(m);
            foreach (Mesh m in meshes) if (m != null) Destroy(m);
            materials.Clear();
            meshes.Clear();
        }
    }
}
