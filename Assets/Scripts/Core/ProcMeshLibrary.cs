using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 캐릭터용 프로시저럴 메시 생성기.
    ///
    /// 왜 필요한가: 이 저장소의 캐릭터는 Unity 내장 프리미티브 조합인데, 그게 두 가지를 동시에
    /// 망치고 있었다.
    /// - <b>모양</b>: 몸통·부츠가 90° 모서리 Cube, 손이 구체 하나, 팔다리가 굵기가 일정한 캡슐.
    /// - <b>비용</b>: 내장 Sphere가 <b>515정점</b>인데 눈·동공·하이라이트·홍조 8개가 전부
    ///   <c>scale (0.15, 0.17, 0.06)</c>으로 눌린 그 Sphere였다 — 원판 하나 그리는 데 4,120정점.
    ///
    /// 그래서 이 라이브러리는 품질을 올리면서 정점을 <b>줄인다</b>. 한 캐릭터 기준
    /// 약 10,400정점 → 3,500 이하가 목표다.
    ///
    /// 부수 효과로 배칭 가능성이 생긴다. Unity 동적 배칭 상한이 <b>300정점</b>이라 515정점 Sphere는
    /// 같은 머티리얼을 공유해도(피부 7노드가 그렇다) 절대 병합되지 않았다. 목표는 아니고 덤이다.
    ///
    /// <b>이 메시들은 파괴하지 않는다.</b> <c>PlayerVisualBuilder.runtimeMaterials</c>와 대칭이
    /// 아닌 이유는 소유자가 인스턴스가 아니라 <b>프로세스</b>이기 때문이다 — 캐시 크기가 파라미터
    /// 조합 수(성별 2 × 부위 ~10)로 상한되고, 플레이어·마네킹·NPC가 같은 메시를 공유한다.
    /// 인스턴스마다 만들었다 지우면 <c>OnDestroy</c>에서 "이 메시를 아직 누가 쓰는가"를 판정해야
    /// 하는데, 그건 <c>OutfitShapeLibrary.DestroySpawnedMaterials</c>가 <c>OP_</c> 접두로
    /// 소유자를 가르느라 겪은 문제와 같은 종류다. 정적 캐시가 그 문제를 아예 없앤다.
    /// (<see cref="OutfitShapeLibrary.GetPrimMesh"/>가 같은 이유로 같은 구조다.)
    ///
    /// <b>크기는 메시에 굽고 <c>localScale = Vector3.one</c>로 쓴다.</b> 둥근 모서리는 비균등
    /// 스케일에 왜곡되기 때문이다(0.4×0.1 상자에 스케일을 걸면 모서리 반경이 축마다 달라진다).
    /// 그래서 <b>bind 가능 노드에는 쓸 수 없다</b> — <c>OutfitShapeLibrary.ApplyBound</c>가
    /// <c>sharedMesh</c>와 <c>localScale</c>을 레시피 값으로 덮어쓰므로,
    /// Cap/CapBrim/Backpack/BackpackStrap/NetHandle/NetRing/Acc*는 내장 프리미티브로 남긴다.
    /// </summary>
    public static class ProcMeshLibrary
    {
        private enum Shape
        {
            Disc,
            LowSphere,
            RoundedBox,
            TaperedCapsule,
            Diamond,
            ProfiledRoundedBox,
            Arc,
            Sector,
            FringeShell,
            DrapeShell,
            Torus,
            NetHead,
        }

        /// <summary>
        /// 캐시 키. <b>문자열이 아니라 구조체다</b> — 조회가 캐릭터를 지을 때마다 수십 번 일어나는데
        /// 문자열 키면 그때마다 새 문자열이 난다(<c>CharacterModelPreviewRenderer.ThumbId</c>와 같은 규율).
        /// float를 그대로 비교하는 건 호출부가 리터럴 상수를 넘기기 때문이다 — 계산된 값이 아니라
        /// 같은 코드 경로면 비트가 정확히 같다.
        /// </summary>
        private readonly struct MeshKey : System.IEquatable<MeshKey>
        {
            private readonly Shape shape;
            private readonly float a;
            private readonly float b;
            private readonly float c;
            private readonly float d;
            private readonly float e;
            private readonly float f;
            private readonly float g;
            private readonly int i;
            private readonly int j;

            public MeshKey(Shape shape, float a, float b, float c, float d, int i, int j)
                : this(shape, a, b, c, d, 0f, 0f, 0f, i, j) { }

            /// <summary>인자가 넷을 넘는 생성기(프로필 상자)용. 기존 키는 e·f·g가 0이라 값이 바뀌지 않는다.</summary>
            public MeshKey(Shape shape, float a, float b, float c, float d, float e, float f, float g, int i, int j)
            {
                this.shape = shape;
                this.a = a;
                this.b = b;
                this.c = c;
                this.d = d;
                this.e = e;
                this.f = f;
                this.g = g;
                this.i = i;
                this.j = j;
            }

            public bool Equals(MeshKey o)
            {
                return shape == o.shape && a == o.a && b == o.b && c == o.c && d == o.d
                    && e == o.e && f == o.f && g == o.g && i == o.i && j == o.j;
            }

            public override bool Equals(object obj)
            {
                return obj is MeshKey o && Equals(o);
            }

            public override int GetHashCode()
            {
                int h = (int)shape;
                h = h * 397 ^ a.GetHashCode();
                h = h * 397 ^ b.GetHashCode();
                h = h * 397 ^ c.GetHashCode();
                h = h * 397 ^ d.GetHashCode();
                h = h * 397 ^ e.GetHashCode();
                h = h * 397 ^ f.GetHashCode();
                h = h * 397 ^ g.GetHashCode();
                h = h * 397 ^ i;
                h = h * 397 ^ j;
                return h;
            }
        }

        private static Dictionary<MeshKey, Mesh> cache;

        private static Mesh Cached(MeshKey key, System.Func<Mesh> build)
        {
            if (cache == null) cache = new Dictionary<MeshKey, Mesh>();
            if (cache.TryGetValue(key, out Mesh m) && m != null) return m;

            m = build();
            m.hideFlags = HideFlags.HideAndDontSave;   // 씬 저장·언로드 대상에서 뺀다(프로세스 수명)
            cache[key] = m;
            return m;
        }

        /// <summary>테스트용. 캐시가 실제로 재사용되는지 확인한다.</summary>
        internal static int CachedMeshCount => cache != null ? cache.Count : 0;

        // ── 원판 ─────────────────────────────────────────────

        /// <summary>
        /// XY 평면 원판(+Z를 향한다). 눈·동공·하이라이트·홍조용 — 이 넷이 정점 낭비의 대부분이었다.
        ///
        /// <paramref name="bulge"/>가 0보다 크면 중심이 그만큼 앞으로 나온 얕은 돔이 된다.
        /// 완전 평면이면 옆에서 볼 때 두께가 0이라 눈이 사라진다 — 눌린 Sphere가 (나쁜 방식으로나마)
        /// 주던 볼록함을 대신한다. 노멀도 그 곡률을 따라 줘서 하이라이트가 눈동자를 타고 돈다.
        /// </summary>
        public static Mesh Disc(float radiusX, float radiusY, float bulge, int segments)
        {
            segments = Mathf.Max(3, segments);
            return Cached(new MeshKey(Shape.Disc, radiusX, radiusY, bulge, 0f, segments, 0), () =>
            {
                Vector3[] verts = new Vector3[segments + 1];
                Vector3[] norms = new Vector3[segments + 1];
                int[] tris = new int[segments * 3];

                verts[0] = new Vector3(0f, 0f, bulge);
                norms[0] = Vector3.forward;

                for (int s = 0; s < segments; s++)
                {
                    float t = s / (float)segments * Mathf.PI * 2f;
                    verts[s + 1] = new Vector3(Mathf.Cos(t) * radiusX, Mathf.Sin(t) * radiusY, 0f);
                    // 테두리 노멀을 바깥으로 눕혀 돔처럼 셰이딩된다(bulge가 0이면 정면 그대로).
                    norms[s + 1] = new Vector3(Mathf.Cos(t) * bulge, Mathf.Sin(t) * bulge, Mathf.Max(0.05f, radiusX)).normalized;

                    // 와인딩은 정점 노멀(+Z)과 같은 쪽을 향해야 한다.
                    // (0, next, s+1) 순서는 면 노멀이 −Z가 나와 백페이스 컬링에 걸렸다 —
                    // 눈·동공·하이라이트·홍조가 정면에서 통째로 사라지는 상태였고,
                    // 예외도 경고도 없어 "각도 탓"으로 오해하기 쉬웠다.
                    int next = (s + 1) % segments + 1;
                    tris[s * 3] = 0;
                    tris[s * 3 + 1] = s + 1;
                    tris[s * 3 + 2] = next;
                }

                Mesh mesh = new Mesh { name = "ProcDisc" };
                mesh.vertices = verts;
                mesh.normals = norms;
                mesh.triangles = tris;
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        // ── 저폴리 구체 ───────────────────────────────────────

        /// <summary>
        /// UV 구체. 내장 Sphere(515정점)를 대체한다 — 치비 스케일에서 머리조차 화면의 일부라
        /// 그 밀도가 필요 없다.
        /// 반지름은 축마다 다르게 줄 수 있어(타원체) 머리·귀·코를 한 생성기로 덮는다.
        /// </summary>
        public static Mesh LowSphere(float radiusX, float radiusY, float radiusZ, int rings, int segments)
        {
            rings = Mathf.Max(2, rings);
            segments = Mathf.Max(3, segments);
            return Cached(new MeshKey(Shape.LowSphere, radiusX, radiusY, radiusZ, 0f, rings, segments), () =>
            {
                int vCount = (rings + 1) * (segments + 1);
                Vector3[] verts = new Vector3[vCount];
                Vector3[] norms = new Vector3[vCount];
                List<int> tris = new List<int>(rings * segments * 6);

                for (int r = 0; r <= rings; r++)
                {
                    float v = r / (float)rings;
                    float phi = v * Mathf.PI;              // 0(위) ~ π(아래)
                    float y = Mathf.Cos(phi);
                    float ring = Mathf.Sin(phi);

                    for (int s = 0; s <= segments; s++)
                    {
                        float u = s / (float)segments;
                        float theta = u * Mathf.PI * 2f;
                        Vector3 unit = new Vector3(Mathf.Cos(theta) * ring, y, Mathf.Sin(theta) * ring);

                        int idx = r * (segments + 1) + s;
                        verts[idx] = new Vector3(unit.x * radiusX, unit.y * radiusY, unit.z * radiusZ);
                        // 타원체의 노멀은 단위구 노멀이 아니라 반지름으로 나눈 방향이다.
                        norms[idx] = new Vector3(
                            unit.x / Mathf.Max(1e-4f, radiusX),
                            unit.y / Mathf.Max(1e-4f, radiusY),
                            unit.z / Mathf.Max(1e-4f, radiusZ)).normalized;
                    }
                }

                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        int a = r * (segments + 1) + s;
                        int b = a + segments + 1;

                        // 극 링에서는 한쪽 삼각형이 축퇴한다 — 넣지 않는다(빈 삼각형은 낭비다).
                        // 와인딩은 바깥을 향해야 한다. 뒤집히면 백페이스 컬링에 걸려 예외 없이
                        // 안 보인다 — ProcMeshLibraryTests가 이 방향을 고정한다.
                        if (r != 0)
                        {
                            tris.Add(a); tris.Add(a + 1); tris.Add(b);
                        }
                        if (r != rings - 1)
                        {
                            tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                        }
                    }
                }

                Mesh mesh = new Mesh { name = "ProcLowSphere" };
                mesh.vertices = verts;
                mesh.normals = norms;
                mesh.triangles = tris.ToArray();
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        // ── 둥근 상자 ─────────────────────────────────────────

        /// <summary>
        /// 모서리가 둥근 상자. 몸통·셔츠·부츠·손(미튼)용 — 지금 그 자리들이 전부 90° 모서리 Cube라
        /// 캐릭터가 "부품을 겹쳐놓은 것"처럼 보이는 가장 큰 원인이다.
        ///
        /// 만드는 법: 각 면을 <paramref name="subdiv"/>×<paramref name="subdiv"/>로 나눈 격자를
        /// 만들고, 각 정점을 <b>안쪽 상자(size − 2r)로 clamp한 점 c</b>에서 반경 r만큼 밀어낸다.
        /// 그러면 면은 평평하고 모서리·꼭짓점만 둥글어진다. 노멀은 <c>normalize(p − c)</c>로
        /// 해석적으로 준다 — <c>RecalculateNormals</c>에 맡기면 면 경계가 각져서 둥근 티가 안 난다.
        /// </summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int subdiv)
        {
            subdiv = Mathf.Max(1, subdiv);
            Vector3 half = size * 0.5f;
            // 반경이 가장 짧은 반쪽 변을 넘으면 형태가 뒤집힌다.
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.999f);

            return Cached(new MeshKey(Shape.RoundedBox, size.x, size.y, size.z, radius, subdiv, 0), () =>
            {
                List<Vector3> verts = new List<Vector3>();
                List<Vector3> norms = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> tris = new List<int>();
                BuildRoundedBox(half, radius, subdiv, verts, norms, uvs, tris);

                Mesh mesh = new Mesh { name = "ProcRoundedBox" };
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        /// <summary>
        /// 폭(X)이 높이를 따라 변하는 둥근 상자 — 몸통 실루엣용. <see cref="RoundedBox"/>를 짓고
        /// X만 높이별 배율로 누른다. 배율은 아래·가운데·위 세 값을 지나는 2차 곡선이다
        /// (남자는 어깨가 넓고 허리로 좁아지고, 여자는 허리가 들어가고 골반이 나온다).
        ///
        /// <b>Z(두께)는 건드리지 않는다</b> — 셔츠 패널·가슴 배지 같은 덧붙임이 몸통 앞면 z에 맞춰
        /// 놓여 있어, 앞면이 기울면 아래쪽이 떠 보인다. 배율은 1 이하로 쓴다(겉옷 레시피가 어깨폭에 맞춰 있다).
        ///
        /// 노멀은 변형의 역전치 야코비안으로 해석적으로 옮긴다 — <c>RecalculateNormals</c>는 면 경계에서
        /// 복제된 정점마다 다른 값을 내 이음매가 보인다(<see cref="RoundedBox"/>가 해석적 노멀을 쓰는 이유와 같다).
        /// </summary>
        public static Mesh ProfiledRoundedBox(Vector3 size, float radius, int subdiv,
            float bottomScale, float midScale, float topScale)
        {
            subdiv = Mathf.Max(1, subdiv);
            Vector3 half = size * 0.5f;
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.999f);

            return Cached(new MeshKey(Shape.ProfiledRoundedBox, size.x, size.y, size.z, radius,
                bottomScale, midScale, topScale, subdiv, 0), () =>
            {
                List<Vector3> verts = new List<Vector3>();
                List<Vector3> norms = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> tris = new List<int>();
                BuildRoundedBox(half, radius, subdiv, verts, norms, uvs, tris);

                float height = Mathf.Max(1e-4f, size.y);
                for (int k = 0; k < verts.Count; k++)
                {
                    Vector3 p = verts[k];
                    Vector3 n = norms[k];
                    float t = Mathf.Clamp01((p.y + half.y) / height);
                    float sc = ProfileScale(t, bottomScale, midScale, topScale);
                    float dsdy = ProfileSlope(t, bottomScale, midScale, topScale) / height;

                    // x' = x·s(y). 노멀은 J^{-T}n = (nx/s, ny − x·s'·nx/s, nz).
                    Vector3 m = new Vector3(n.x / sc, n.y - p.x * dsdy * n.x / sc, n.z);
                    verts[k] = new Vector3(p.x * sc, p.y, p.z);
                    norms[k] = m.sqrMagnitude > 1e-10f ? m.normalized : n;
                }

                Mesh mesh = new Mesh { name = "ProcProfiledRoundedBox" };
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        /// <summary>둥근 상자 한 면의 평면 UV — <see cref="BuildRoundedBox"/>의 remarks 참고.</summary>
        internal static Vector2 FaceUv(int face, Vector3 p, Vector3 half)
        {
            float x = p.x / Mathf.Max(1e-5f, half.x);
            float y = p.y / Mathf.Max(1e-5f, half.y);
            float z = p.z / Mathf.Max(1e-5f, half.z);
            // 아틀라스: u 0~0.5 = 앞면, 0.5~1 = 뒷면·옆면. 단추·주머니·거미줄 중심 같은 **앞면 전용 무늬**가
            // 등과 옆구리에 복사되지 않게 한다(OutfitPatternLibrary가 두 칸을 따로 그린다).
            float fu, fv = (y + 1f) * 0.5f;
            bool front = false;
            switch (face)
            {
                case 0: fu = (z + 1f) * 0.5f; break;                          // +X
                case 1: fu = (1f - z) * 0.5f; break;                          // −X
                case 2: fu = (1f - x) * 0.5f; fv = 1f; front = true; break;   // +Y(윗면) — 앞면 윗줄 색
                case 3: fu = (1f - x) * 0.5f; fv = 0f; front = true; break;   // −Y(아랫면) — 앞면 밑줄 색
                case 4: fu = (1f - x) * 0.5f; front = true; break;            // +Z(앞) — 정면에서 보면 +X가 왼쪽
                default: fu = (x + 1f) * 0.5f; break;                         // −Z(뒤)
            }
            // 칸 경계에서 이웃 칸이 번지지 않게(밉맵·쌍선형) 칸 안쪽으로 조금 당긴다.
            fu = Mathf.Lerp(0.01f, 0.99f, fu);
            return new Vector2(front ? fu * 0.5f : 0.5f + fu * 0.5f, fv);
        }

        /// <summary>(0,b)·(0.5,m)·(1,top)을 지나는 2차 곡선. 0 가까이로 떨어지지 않게 막는다.</summary>
        internal static float ProfileScale(float t, float b, float m, float top)
        {
            float l0 = 2f * (t - 0.5f) * (t - 1f);
            float l1 = -4f * t * (t - 1f);
            float l2 = 2f * t * (t - 0.5f);
            return Mathf.Max(0.05f, b * l0 + m * l1 + top * l2);
        }

        private static float ProfileSlope(float t, float b, float m, float top)
        {
            return b * (4f * t - 3f) + m * (4f - 8f * t) + top * (4f * t - 1f);
        }

        /// <remarks>
        /// UV는 면마다 0..1 평면 투영이다(무늬 텍스처용). 앞·뒤·옆면은 <b>그 면을 바라보는 사람 기준으로</b>
        /// 왼→오른쪽이 u라 비대칭 무늬가 뒤집혀 보이지 않고, v는 전부 높이라 가로 줄무늬가 옆면까지 이어진다.
        /// 윗면·아랫면은 v를 1·0에 고정해 맞닿은 줄의 색을 그대로 잇는다(어깨 위에 줄무늬가 가로로 눕지 않게).
        /// </remarks>
        private static void BuildRoundedBox(Vector3 half, float radius, int subdiv,
            List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris)
        {
            Vector3 inner = new Vector3(half.x - radius, half.y - radius, half.z - radius);

            // 6면: (축, 부호)
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = normals[f];
                // 면 평면의 두 접선축 — ±X면은 (up, forward), ±Y면은 (right, forward), ±Z면은 (right, up).
                // 인접 면이 모서리에서 같은 좌표를 내야 틈이 안 생기므로 축은 항상 양의 방향으로 잡는다.
                Vector3 tu = (f < 2) ? Vector3.up : Vector3.right;
                Vector3 tv = (f < 4) ? Vector3.forward : Vector3.up;

                int start = verts.Count;
                for (int iy = 0; iy <= subdiv; iy++)
                {
                    for (int ix = 0; ix <= subdiv; ix++)
                    {
                        float u = ix / (float)subdiv * 2f - 1f;   // -1..1
                        float v = iy / (float)subdiv * 2f - 1f;

                        // 면 위의 점(상자 표면 좌표)
                        Vector3 p = Vector3.Scale(n, half)
                                  + tu * (u * Vector3.Dot(half, tu))
                                  + tv * (v * Vector3.Dot(half, tv));

                        // 안쪽 상자로 clamp → 그 점이 곡률 중심
                        Vector3 c = new Vector3(
                            Mathf.Clamp(p.x, -inner.x, inner.x),
                            Mathf.Clamp(p.y, -inner.y, inner.y),
                            Mathf.Clamp(p.z, -inner.z, inner.z));

                        Vector3 dir = p - c;
                        Vector3 normal = dir.sqrMagnitude > 1e-8f ? dir.normalized : n;

                        verts.Add(c + normal * radius);
                        norms.Add(normal);
                        uvs.Add(FaceUv(f, p, half));
                    }
                }

                // 격자 삼각형 (a, c, b)의 면 노멀은 cross(tv, tu) 방향이다. 그게 이 면의 바깥
                // 노멀과 반대면 순서를 뒤집는다.
                //
                // 면마다 하드코딩하면 틀린다 — 축 순환 때문에 ±X와 ±Z가 ±Y와 반대로 나온다.
                // 처음에 `f == 1 || f == 3 || f == 5`로 적었다가 6면 중 4면이 뒤집혔고,
                // 그건 백페이스 컬링에 걸려 예외 없이 안 보인다.
                bool flip = Vector3.Dot(Vector3.Cross(tv, tu), n) < 0f;

                int stride = subdiv + 1;
                for (int iy = 0; iy < subdiv; iy++)
                {
                    for (int ix = 0; ix < subdiv; ix++)
                    {
                        int a = start + iy * stride + ix;
                        int b = a + 1;
                        int c2 = a + stride;
                        int d = c2 + 1;

                        if (flip)
                        {
                            tris.Add(a); tris.Add(b); tris.Add(c2);
                            tris.Add(b); tris.Add(d); tris.Add(c2);
                        }
                        else
                        {
                            tris.Add(a); tris.Add(c2); tris.Add(b);
                            tris.Add(b); tris.Add(c2); tris.Add(d);
                        }
                    }
                }
            }
        }

        // ── 테이퍼드 캡슐 ─────────────────────────────────────

        /// <summary>
        /// 위아래 굵기가 다른 캡슐. 팔(어깨→손목)·다리(허벅지→발목)용 —
        /// 지금은 굵기가 일정한 내장 Capsule(552정점)이라 사지가 파이프처럼 보인다.
        ///
        /// 중심은 원점, 축은 Y. 전체 높이는 <paramref name="height"/> + 양 끝 반구다
        /// (내장 Capsule과 같은 규약이라 기존 좌표를 그대로 쓸 수 있다).
        /// </summary>
        /// <param name="yOffset">
        /// 모든 정점을 Y로 민다. 0이면 기존과 같은 캐시 키·같은 메시다. 팔처럼 <b>한쪽 끝을 축으로 돌리는</b>
        /// 부위에 쓴다 — Transform은 원점을 축으로 도므로, 원점이 캡슐 가운데면 팔이 어깨가 아니라
        /// 팔 한가운데에서 돌아 윗부분이 몸통 뒤로 빠진다.
        /// </param>
        public static Mesh TaperedCapsule(float radiusTop, float radiusBottom, float height, int rings, int segments,
            float yOffset = 0f)
        {
            rings = Mathf.Max(3, rings);
            segments = Mathf.Max(3, segments);
            return Cached(new MeshKey(Shape.TaperedCapsule, radiusTop, radiusBottom, height, yOffset, rings, segments), () =>
            {
                float halfH = height * 0.5f;
                int vCount = (rings + 1) * (segments + 1);
                Vector3[] verts = new Vector3[vCount];
                Vector3[] norms = new Vector3[vCount];
                Vector2[] uvs = new Vector2[vCount];
                List<int> tris = new List<int>(rings * segments * 6);

                for (int r = 0; r <= rings; r++)
                {
                    float t = r / (float)rings;   // 0 = 위, 1 = 아래
                    float y, ringR, slopeY, slopeXZ;

                    if (t < 0.25f)
                    {
                        // 위 반구
                        float k = t / 0.25f;                       // 0..1
                        float ang = k * Mathf.PI * 0.5f;
                        y = halfH + Mathf.Cos(ang) * radiusTop;
                        ringR = Mathf.Sin(ang) * radiusTop;
                        // 구면 노멀은 (sin·cx, cos, sin·cz)다 — 수평 성분에 sin을 곱하지 않으면
                        // 극점 노멀이 (cx,1,cz)가 되어 45°로 눕고 캡 전체 셰이딩이 어긋난다.
                        slopeY = Mathf.Cos(ang);
                        slopeXZ = Mathf.Sin(ang);
                    }
                    else if (t > 0.75f)
                    {
                        // 아래 반구
                        float k = (t - 0.75f) / 0.25f;             // 0..1
                        float ang = k * Mathf.PI * 0.5f;
                        y = -halfH - Mathf.Sin(ang) * radiusBottom;
                        ringR = Mathf.Cos(ang) * radiusBottom;
                        slopeY = -Mathf.Sin(ang);
                        slopeXZ = Mathf.Cos(ang);
                    }
                    else
                    {
                        // 원뿔대 몸통
                        float k = (t - 0.25f) / 0.5f;              // 0..1
                        y = Mathf.Lerp(halfH, -halfH, k);
                        ringR = Mathf.Lerp(radiusTop, radiusBottom, k);
                        // 테이퍼면의 노멀은 수직이 아니다 — 기울기만큼 위로 눕는다.
                        slopeY = (radiusBottom - radiusTop) / Mathf.Max(1e-4f, height);
                        slopeXZ = 1f;
                    }

                    for (int s = 0; s <= segments; s++)
                    {
                        // 링은 **뒤(−Z)에서 시작**한다 — UV 이음매(u 0↔1)가 무늬 텍스처에서 뒤쪽에 숨는다.
                        // 형태는 예전(+X 시작)과 같고 정점 순서만 돈다(와인딩 방향 동일).
                        float u = (s / (float)segments + 0.75f) * Mathf.PI * 2f;
                        float cx = Mathf.Cos(u);
                        float cz = Mathf.Sin(u);

                        int idx = r * (segments + 1) + s;
                        verts[idx] = new Vector3(cx * ringR, y + yOffset, cz * ringR);
                        norms[idx] = new Vector3(cx * slopeXZ, slopeY, cz * slopeXZ).normalized;
                        uvs[idx] = new Vector2(s / (float)segments, 1f - t);   // 원통 투영: u 둘레, v 위→아래
                    }
                }

                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < segments; s++)
                    {
                        int a = r * (segments + 1) + s;
                        int b = a + segments + 1;   // 한 링 아래
                        // 바깥을 향하는 순서. LowSphere와 같은 규약이다.
                        //
                        // 극 링(r=0 위, r=rings-1 아래)은 ringR이 0이라 정점이 한 점에 겹친다 —
                        // 그쪽 삼각형 하나는 넓이가 0인 축퇴 삼각형이라 넣지 않는다.
                        // (정점을 줄이려고 만든 라이브러리가 빈 삼각형을 20개씩 싣던 자리다.)
                        if (r != 0)
                        {
                            tris.Add(a); tris.Add(a + 1); tris.Add(b);
                        }
                        if (r != rings - 1)
                        {
                            tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                        }
                    }
                }

                Mesh mesh = new Mesh { name = "ProcTaperedCapsule" };
                mesh.vertices = verts;
                mesh.normals = norms;
                mesh.uv = uvs;
                mesh.triangles = tris.ToArray();
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        // ── 평면 호·부채꼴 (+Z를 향한다) ─────────────────────

        /// <summary>
        /// 두께 있는 호(띠). 입꼬리가 올라간 미소, 눈 위 속눈썹 선처럼 <b>휜 선</b>을 그린다 —
        /// 예전 입은 폭 0.02~0.05의 납작한 상자라 표정 네 가지가 화면에서 구분되지 않았다.
        /// 각도는 도 단위, +X에서 반시계로 잰다(270°가 아래). 원점은 원의 중심이다.
        /// </summary>
        public static Mesh Arc(float radius, float thickness, float startDeg, float endDeg, int segments)
        {
            segments = Mathf.Max(1, segments);
            return Cached(new MeshKey(Shape.Arc, radius, thickness, startDeg, endDeg, segments, 0), () =>
            {
                Vector3[] verts = new Vector3[(segments + 1) * 2];
                Vector3[] norms = new Vector3[verts.Length];
                int[] tris = new int[segments * 6];
                float rIn = Mathf.Max(0f, radius - thickness * 0.5f);
                float rOut = radius + thickness * 0.5f;

                for (int k = 0; k <= segments; k++)
                {
                    float a = Mathf.Lerp(startDeg, endDeg, k / (float)segments) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    verts[k * 2] = dir * rIn;
                    verts[k * 2 + 1] = dir * rOut;
                    norms[k * 2] = Vector3.forward;
                    norms[k * 2 + 1] = Vector3.forward;
                }

                // 각도가 반시계로 늘면 (안k, 밖k, 밖k+1)의 면 노멀이 +Z다. 끝각이 더 작으면 뒤집는다.
                bool ccw = endDeg >= startDeg;
                for (int k = 0; k < segments; k++)
                {
                    int i0 = k * 2, o0 = k * 2 + 1, i1 = k * 2 + 2, o1 = k * 2 + 3;
                    int t = k * 6;
                    if (ccw)
                    {
                        tris[t] = i0; tris[t + 1] = o0; tris[t + 2] = o1;
                        tris[t + 3] = i0; tris[t + 4] = o1; tris[t + 5] = i1;
                    }
                    else
                    {
                        tris[t] = i0; tris[t + 1] = o1; tris[t + 2] = o0;
                        tris[t + 3] = i0; tris[t + 4] = i1; tris[t + 5] = o1;
                    }
                }

                Mesh mesh = new Mesh { name = "ProcArc" };
                mesh.vertices = verts;
                mesh.normals = norms;
                mesh.triangles = tris;
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        /// <summary>
        /// 부채꼴(중심에서 호까지 채운 면). 180°→360°면 윗변이 평평한 반원 — 활짝 웃는 입이다.
        /// 원점은 원의 중심, 각도 규약은 <see cref="Arc"/>와 같다.
        /// </summary>
        public static Mesh Sector(float radiusX, float radiusY, float startDeg, float endDeg, int segments)
        {
            segments = Mathf.Max(1, segments);
            return Cached(new MeshKey(Shape.Sector, radiusX, radiusY, startDeg, endDeg, segments, 0), () =>
            {
                Vector3[] verts = new Vector3[segments + 2];
                Vector3[] norms = new Vector3[verts.Length];
                int[] tris = new int[segments * 3];

                verts[0] = Vector3.zero;
                norms[0] = Vector3.forward;
                for (int k = 0; k <= segments; k++)
                {
                    float a = Mathf.Lerp(startDeg, endDeg, k / (float)segments) * Mathf.Deg2Rad;
                    verts[k + 1] = new Vector3(Mathf.Cos(a) * radiusX, Mathf.Sin(a) * radiusY, 0f);
                    norms[k + 1] = Vector3.forward;
                }

                bool ccw = endDeg >= startDeg;
                for (int k = 0; k < segments; k++)
                {
                    tris[k * 3] = 0;
                    tris[k * 3 + 1] = ccw ? k + 1 : k + 2;
                    tris[k * 3 + 2] = ccw ? k + 2 : k + 1;
                }

                Mesh mesh = new Mesh { name = "ProcSector" };
                mesh.vertices = verts;
                mesh.normals = norms;
                mesh.triangles = tris;
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        // ── 앞머리 껍질 ───────────────────────────────────────

        /// <summary>
        /// 이마를 덮는 앞머리 한 장 — 머리 타원체(반지름 <paramref name="radiusXZ"/>·<paramref name="radiusY"/>,
        /// 중심 원점) 표면의 한 조각이고, **아래 끝만** 톱니(남자)나 물결(여자)로 잘린다.
        ///
        /// 왜 전용 생성기인가: 캡슐·구 조각을 이마에 늘어놓으면 조각마다 음영이 따로 져서 "이빨"이나
        /// "꼰 끈"으로 읽혔다(2026-09-30 시안 두 번). 곡면 한 장이면 음영이 이어지고 끝모양만 남는다.
        ///
        /// 각도는 도 단위. 방위각 θ는 +Z(정면)에서 잰다(±<paramref name="halfAngleDeg"/>),
        /// 고도 φ는 적도에서 위로 잰다. 윗변은 <paramref name="topDeg"/>(정수리 덮개 밑으로 들어가게 높게),
        /// 톱니 끝은 <paramref name="tipDeg"/>, 톱니 뿌리는 거기서 <paramref name="toothDeg"/>만큼 위다.
        /// <paramref name="sideDropDeg"/>만큼 양 끝(관자놀이)이 더 내려온다.
        /// <paramref name="skew"/>(0~1)는 톱니 꼭짓점의 위치 — 0.5면 대칭, 크면 한쪽으로 쓸린다.
        /// <paramref name="rounded"/>면 톱니 대신 둥근 물결이다(여자 앞머리).
        /// 바깥(머리 반대쪽)만 그린다 — 안쪽은 머리에 붙어 보이지 않는다.
        /// </summary>
        public static Mesh FringeShell(float radiusXZ, float radiusY, float halfAngleDeg, float topDeg, float tipDeg,
            float toothDeg, float sideDropDeg, int teeth, float skew, bool rounded)
        {
            teeth = Mathf.Clamp(teeth, 1, 40);
            skew = Mathf.Clamp(skew, 0.05f, 0.95f);
            int skewKey = Mathf.RoundToInt(skew * 100f);
            return Cached(new MeshKey(Shape.FringeShell, radiusXZ, radiusY, halfAngleDeg, topDeg, tipDeg, toothDeg, sideDropDeg,
                teeth * 1000 + skewKey * 2 + (rounded ? 1 : 0), 0), () =>
            {
                int cols = teeth * 4;   // 톱니당 4열이면 끝이 뾰족하게 선다(6열은 정점만 늘었다)
                const int rows = 5;
                Vector3[] verts = new Vector3[(cols + 1) * (rows + 1)];
                Vector3[] norms = new Vector3[verts.Length];
                int[] tris = new int[cols * rows * 6];

                for (int c = 0; c <= cols; c++)
                {
                    float u = c / (float)cols;                                  // 0..1 (−θ → +θ)
                    float theta = Mathf.Lerp(-halfAngleDeg, halfAngleDeg, u) * Mathf.Deg2Rad;
                    float bottom = FringeEdgeDeg(u, tipDeg, toothDeg, sideDropDeg, teeth, skew, rounded);

                    for (int r = 0; r <= rows; r++)
                    {
                        float phi = Mathf.Lerp(topDeg, bottom, r / (float)rows) * Mathf.Deg2Rad;
                        float cp = Mathf.Cos(phi);
                        Vector3 unit = new Vector3(Mathf.Sin(theta) * cp, Mathf.Sin(phi), Mathf.Cos(theta) * cp);
                        int idx = c * (rows + 1) + r;
                        verts[idx] = new Vector3(unit.x * radiusXZ, unit.y * radiusY, unit.z * radiusXZ);
                        norms[idx] = new Vector3(unit.x / radiusXZ, unit.y / radiusY, unit.z / radiusXZ).normalized;
                    }
                }

                // 열은 +X로, 행은 아래로 간다 — (a, 아래, 오른쪽)이 정면에서 +Z를 향한다.
                int t = 0;
                for (int c = 0; c < cols; c++)
                {
                    for (int r = 0; r < rows; r++)
                    {
                        int a = c * (rows + 1) + r;
                        int down = a + 1;
                        int right = a + rows + 1;
                        int diag = right + 1;
                        tris[t++] = a; tris[t++] = down; tris[t++] = right;
                        tris[t++] = right; tris[t++] = down; tris[t++] = diag;
                    }
                }

                Mesh mesh = new Mesh { name = "ProcFringeShell" };
                mesh.vertices = verts;
                mesh.normals = norms;
                mesh.triangles = tris;
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        /// <summary>앞머리 아래 끝의 고도(도). u = 0..1 가로 위치. 순수 함수라 모양을 테스트로 고정한다.</summary>
        internal static float FringeEdgeDeg(float u, float tipDeg, float toothDeg, float sideDropDeg,
            int teeth, float skew, bool rounded)
        {
            float q = u * teeth;
            q -= Mathf.Floor(q);
            if (u >= 1f) q = 1f;                          // 마지막 열은 톱니 끝이 아니라 뿌리

            float w;                                      // 1 = 끝(가장 아래), 0 = 뿌리
            // sin(π)은 부동소수에서 −8.7e-8이라 그대로 Sqrt하면 **NaN**이다 — 오른쪽 끝 열이 통째로 사라졌다.
            if (rounded) w = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(q * Mathf.PI)));
            else w = q < skew ? q / skew : (1f - q) / (1f - skew);

            float side = Mathf.Abs(u * 2f - 1f);
            return tipDeg + toothDeg * (1f - w) - sideDropDeg * side * side;
        }

        // ── 늘어진 천(망토·코트 자락·치맛단) ─────────────────

        /// <summary>
        /// 위에서 아래로 늘어진 천 한 장 — Y축을 감싸는 원통 조각이고 **뒤(−Z)를 중심으로** 펼쳐진다.
        /// 단위 크기: 윗변 반지름 0.5, 높이 1(원점 = 윗변 중앙, 아래로 −1). 파츠 스케일로 폭·길이·깊이를 준다.
        /// 옛 망토·코트 자락은 두께 0.03의 상자라 "등에 붙인 판자"였다(2026-09-30 전수 캡처).
        ///
        /// <paramref name="arcDeg"/>는 감싸는 각도(180이면 반원, 360이면 치마처럼 한 바퀴),
        /// <paramref name="flare"/>는 밑단 반지름 배율(1보다 크면 퍼진다),
        /// <paramref name="folds"/>·<paramref name="foldDepth"/>는 밑으로 갈수록 깊어지는 주름(반지름 방향 사인파).
        /// <b>양면</b>이다 — 망토 안쪽은 옆에서 보이므로 뒷면도 그린다(노멀을 뒤집은 두 번째 면).
        /// UV는 u = 가로, v = 위(1)→아래(0).
        /// </summary>
        /// <param name="rows">세로 분할. 짧은 천(옷깃)은 2면 충분하다 — 정점 = (열+1)·(행+1)·2.</param>
        public static Mesh DrapeShell(float arcDeg, float flare, int folds, float foldDepth, int rows = 8)
        {
            arcDeg = Mathf.Clamp(arcDeg, 10f, 360f);
            folds = Mathf.Clamp(folds, 0, 24);
            rows = Mathf.Clamp(rows, 1, 16);
            return Cached(new MeshKey(Shape.DrapeShell, arcDeg, flare, foldDepth, 0f, folds, rows), () =>
            {
                int cols = Mathf.Max(8, Mathf.CeilToInt(arcDeg / 12f));
                int per = (cols + 1) * (rows + 1);
                Vector3[] verts = new Vector3[per * 2];
                Vector3[] norms = new Vector3[per * 2];
                Vector2[] uvs = new Vector2[per * 2];
                int[] tris = new int[cols * rows * 12];

                float half = arcDeg * 0.5f * Mathf.Deg2Rad;
                float slope = 0.5f * (flare - 1f);
                for (int c = 0; c <= cols; c++)
                {
                    float u = c / (float)cols;
                    float th = Mathf.Lerp(-half, half, u);
                    for (int r = 0; r <= rows; r++)
                    {
                        float v = r / (float)rows;                       // 0 위 → 1 아래
                        float fold = folds > 0 ? Mathf.Sin(u * folds * Mathf.PI * 2f) * foldDepth * v : 0f;
                        float rad = 0.5f * Mathf.Lerp(1f, flare, v) + fold;
                        // 뒤(−Z) 중심: th = 0이 (0, ·, −r).
                        Vector3 p = new Vector3(Mathf.Sin(th) * rad, -v, -Mathf.Cos(th) * rad);
                        int i = c * (rows + 1) + r;
                        verts[i] = p;
                        verts[i + per] = p;
                        // 바깥 노멀: 반지름 방향 + 퍼짐 기울기(밑단이 퍼지면 면이 아래를 조금 본다)
                        Vector3 n = new Vector3(Mathf.Sin(th), -slope, -Mathf.Cos(th)).normalized;
                        norms[i] = n;
                        norms[i + per] = -n;
                        uvs[i] = new Vector2(u, 1f - v);
                        uvs[i + per] = new Vector2(u, 1f - v);
                    }
                }

                // 바깥면 와인딩은 바깥 노멀과 같은 쪽 — ProcMeshLibraryTests가 와인딩↔노멀 일치로 고정한다.
                // 안쪽면은 같은 정점을 복제해 노멀을 뒤집고 순서도 뒤집는다.
                int t = 0;
                for (int c = 0; c < cols; c++)
                {
                    for (int r = 0; r < rows; r++)
                    {
                        int a = c * (rows + 1) + r;
                        int down = a + 1;
                        int right = a + rows + 1;
                        int diag = right + 1;
                        tris[t++] = a; tris[t++] = right; tris[t++] = down;
                        tris[t++] = right; tris[t++] = diag; tris[t++] = down;
                        tris[t++] = a + per; tris[t++] = down + per; tris[t++] = right + per;
                        tris[t++] = right + per; tris[t++] = down + per; tris[t++] = diag + per;
                    }
                }

                Mesh mesh = new Mesh { name = "ProcDrapeShell" };
                mesh.vertices = verts;
                mesh.normals = norms;
                mesh.uv = uvs;
                mesh.triangles = tris;
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        // ── 고리(토러스) ──────────────────────────────────────

        /// <summary>
        /// Y축을 도는 고리. 단위 크기: 큰 반지름 0.5, 관 반지름 <paramref name="tube"/>(바깥지름 ≈ 1 + 2·tube).
        /// 잠자리채 테·모자 띠·팔찌처럼 **구멍이 있어야 하는** 자리에 쓴다 — 원기둥으로 흉내 내면 판이 막혀
        /// 잠자리채가 망치로 보였다.
        /// </summary>
        public static Mesh Torus(float tube, int segments, int sides)
        {
            tube = Mathf.Clamp(tube, 0.005f, 0.45f);
            segments = Mathf.Clamp(segments, 6, 64);
            sides = Mathf.Clamp(sides, 3, 16);
            return Cached(new MeshKey(Shape.Torus, tube, 0f, 0f, 0f, segments, sides), () =>
            {
                Vector3[] verts = new Vector3[(segments + 1) * (sides + 1)];
                Vector3[] norms = new Vector3[verts.Length];
                Vector2[] uvs = new Vector2[verts.Length];
                int[] tris = new int[segments * sides * 6];

                for (int i = 0; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    for (int j = 0; j <= sides; j++)
                    {
                        float b = j / (float)sides * Mathf.PI * 2f;
                        Vector3 n = dir * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                        int k = i * (sides + 1) + j;
                        verts[k] = dir * 0.5f + n * tube;
                        norms[k] = n;
                        uvs[k] = new Vector2(i / (float)segments, j / (float)sides);
                    }
                }

                int t = 0;
                for (int i = 0; i < segments; i++)
                {
                    for (int j = 0; j < sides; j++)
                    {
                        int a = i * (sides + 1) + j;
                        int b = a + sides + 1;
                        tris[t++] = a; tris[t++] = a + 1; tris[t++] = b;
                        tris[t++] = b; tris[t++] = a + 1; tris[t++] = b + 1;
                    }
                }

                Mesh mesh = new Mesh { name = "ProcTorus" };
                mesh.vertices = verts;
                mesh.normals = norms;
                mesh.uv = uvs;
                mesh.triangles = tris;
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        // ── 잠자리채 머리(테 + 그물 주머니) ─────────────────

        /// <summary>
        /// 잠자리채 머리 — **세운 고리(테) + 뒤(−Z)로 늘어진 그물 주머니**를 한 메시로 굽는다.
        /// 원점은 테의 **아래 끝**(자루가 꽂히는 점)이라, 노드를 자루 끝에 두면 테가 자루 위에 선다.
        /// 단위 크기: 테 지름 1(중심 (0, 0.5, 0), XY 평면), 관 반지름 <paramref name="tube"/>,
        /// 주머니 깊이 <paramref name="depth"/>(끝은 조금 처진다).
        ///
        /// 한 메시인 이유: 잠자리채는 NetRing **bind** 노드 하나라(PlayerMovement가 스윙에 캐시한다)
        /// 파츠를 따로 달 수 없다. 옛 모양은 납작한 원기둥(막힌 판)이라 자루와 합쳐 망치로 보였다.
        /// 주머니는 양면이다 — 테 안으로 안쪽이 보인다.
        /// </summary>
        public static Mesh NetHead(float tube, float depth)
        {
            tube = Mathf.Clamp(tube, 0.01f, 0.2f);
            depth = Mathf.Clamp(depth, 0.1f, 2f);
            return Cached(new MeshKey(Shape.NetHead, tube, depth, 0f, 0f, 0, 0), () =>
            {
                const int seg = 16, sides = 5, rows = 4;   // 한 손에 드는 소품 — 테 102 + 그물 170정점
                List<Vector3> verts = new List<Vector3>();
                List<Vector3> norms = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> tris = new List<int>();
                Vector3 center = new Vector3(0f, 0.5f, 0f);

                // 테: XY 평면의 토러스. 큰 원 방향 d = (sin a, −cos a, 0) — a = 0이 아래 끝(원점).
                int ringStart = verts.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    Vector3 d = new Vector3(Mathf.Sin(a), -Mathf.Cos(a), 0f);
                    for (int j = 0; j <= sides; j++)
                    {
                        float b = j / (float)sides * Mathf.PI * 2f;
                        Vector3 n = d * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                        verts.Add(center + d * 0.5f + n * tube);
                        norms.Add(n);
                        uvs.Add(new Vector2(i / (float)seg, j / (float)sides));
                    }
                }
                for (int i = 0; i < seg; i++)
                {
                    for (int j = 0; j < sides; j++)
                    {
                        int a0 = ringStart + i * (sides + 1) + j;
                        int b0 = a0 + sides + 1;
                        tris.Add(a0); tris.Add(b0); tris.Add(a0 + 1);
                        tris.Add(b0); tris.Add(b0 + 1); tris.Add(a0 + 1);
                    }
                }

                // 주머니: 테(반지름 0.5)에서 뒤로 모이는 원뿔. 끝이 아래로 조금 처진다.
                Vector3 tip = center + new Vector3(0f, -0.18f, -depth);
                int bagStart = verts.Count;
                for (int r = 0; r <= rows; r++)
                {
                    float t = r / (float)rows;                  // 0 테 → 1 끝
                    float rad = 0.5f * (1f - t) * (1f - 0.25f * t);
                    Vector3 c = Vector3.Lerp(center, tip, t);
                    c.y -= Mathf.Sin(t * Mathf.PI) * 0.06f;     // 가운데가 살짝 처진 곡선
                    for (int i = 0; i <= seg; i++)
                    {
                        float a = i / (float)seg * Mathf.PI * 2f;
                        Vector3 d = new Vector3(Mathf.Sin(a), -Mathf.Cos(a), 0f);
                        verts.Add(c + d * rad);
                        norms.Add((d + Vector3.back * 0.35f).normalized);
                        uvs.Add(new Vector2(i / (float)seg, 1f - t));
                    }
                }
                int per = (rows + 1) * (seg + 1);
                // 안쪽면 — 같은 정점을 복제해 노멀을 뒤집는다
                for (int k = 0; k < per; k++)
                {
                    verts.Add(verts[bagStart + k]);
                    norms.Add(-norms[bagStart + k]);
                    uvs.Add(uvs[bagStart + k]);
                }
                for (int r = 0; r < rows; r++)
                {
                    for (int i = 0; i < seg; i++)
                    {
                        int a0 = bagStart + r * (seg + 1) + i;
                        int b0 = a0 + seg + 1;
                        // 바깥(아래·뒤를 보는 면): (a, 다음 행, 옆) — 와인딩↔노멀 일치는 테스트가 고정한다.
                        tris.Add(a0); tris.Add(b0); tris.Add(a0 + 1);
                        tris.Add(a0 + 1); tris.Add(b0); tris.Add(b0 + 1);
                        tris.Add(a0 + per); tris.Add(a0 + 1 + per); tris.Add(b0 + per);
                        tris.Add(a0 + 1 + per); tris.Add(b0 + 1 + per); tris.Add(b0 + per);
                    }
                }

                Mesh mesh = new Mesh { name = "ProcNetHead" };
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        // ── 팔면체 ───────────────────────────────────────────

        /// <summary>
        /// 아이템 픽업용 다이아몬드(팔면체).
        ///
        /// <c>CaptureItemPickup.CreateDiamondMesh</c>에 있던 것을 옮겼다 — 그쪽은 픽업이 스폰될
        /// 때마다 <c>new Mesh()</c>를 만들고 파괴 경로에 회수가 없어, 120초 수명 × 반복 스폰만큼
        /// 실제로 샜다. 여기 캐시에 두면 프로세스당 1개로 상한된다.
        /// </summary>
        public static Mesh Diamond(float radius, float topHeight, float bottomDepth)
        {
            return Cached(new MeshKey(Shape.Diamond, radius, topHeight, bottomDepth, 0f, 0, 0), () =>
            {
                Vector3[] verts =
                {
                    new Vector3(0f, topHeight, 0f),
                    new Vector3(radius, 0f, radius),
                    new Vector3(radius, 0f, -radius),
                    new Vector3(-radius, 0f, -radius),
                    new Vector3(-radius, 0f, radius),
                    new Vector3(0f, -bottomDepth, 0f),
                };
                int[] tris =
                {
                    0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1,
                    5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4,
                };

                Mesh mesh = new Mesh { name = "ProcDiamond" };
                mesh.vertices = verts;
                mesh.triangles = tris;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        // ── 조립 헬퍼 ─────────────────────────────────────────

        /// <summary>
        /// 커스텀 메시 노드를 만든다. <c>CreatePrimitive</c>를 쓰지 않으므로 콜라이더가 생겼다
        /// 파괴되는 왕복이 없다 — 캐릭터 하나에 그 왕복이 54번 있었다.
        /// (<c>OutfitShapeLibrary.ApplySpawned</c>가 같은 이유로 같은 형태다.)
        ///
        /// <b>스케일은 걸지 않는다.</b> 크기는 메시에 구워져 있고, 둥근 모서리는 비균등 스케일에
        /// 왜곡되기 때문이다. 호출부가 위치·회전만 준다.
        /// </summary>
        public static GameObject CreateNode(string name, Transform parent, Mesh mesh, Material material,
            Vector3 localPosition, Quaternion localRotation)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().material = material;
            return go;
        }

        /// <summary>회전이 필요 없는 흔한 경우.</summary>
        public static GameObject CreateNode(string name, Transform parent, Mesh mesh, Material material,
            Vector3 localPosition)
        {
            return CreateNode(name, parent, mesh, material, localPosition, Quaternion.identity);
        }
    }
}
