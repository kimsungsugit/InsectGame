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
            private readonly int i;
            private readonly int j;

            public MeshKey(Shape shape, float a, float b, float c, float d, int i, int j)
            {
                this.shape = shape;
                this.a = a;
                this.b = b;
                this.c = c;
                this.d = d;
                this.i = i;
                this.j = j;
            }

            public bool Equals(MeshKey o)
            {
                return shape == o.shape && a == o.a && b == o.b && c == o.c && d == o.d && i == o.i && j == o.j;
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
                Vector3 inner = new Vector3(half.x - radius, half.y - radius, half.z - radius);

                List<Vector3> verts = new List<Vector3>();
                List<Vector3> norms = new List<Vector3>();
                List<int> tris = new List<int>();

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

                Mesh mesh = new Mesh { name = "ProcRoundedBox" };
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            });
        }

        // ── 테이퍼드 캡슐 ─────────────────────────────────────

        /// <summary>
        /// 위아래 굵기가 다른 캡슐. 팔(어깨→손목)·다리(허벅지→발목)용 —
        /// 지금은 굵기가 일정한 내장 Capsule(552정점)이라 사지가 파이프처럼 보인다.
        ///
        /// 중심은 원점, 축은 Y. 전체 높이는 <paramref name="height"/> + 양 끝 반구다
        /// (내장 Capsule과 같은 규약이라 기존 좌표를 그대로 쓸 수 있다).
        /// </summary>
        public static Mesh TaperedCapsule(float radiusTop, float radiusBottom, float height, int rings, int segments)
        {
            rings = Mathf.Max(3, rings);
            segments = Mathf.Max(3, segments);
            return Cached(new MeshKey(Shape.TaperedCapsule, radiusTop, radiusBottom, height, 0f, rings, segments), () =>
            {
                float halfH = height * 0.5f;
                int vCount = (rings + 1) * (segments + 1);
                Vector3[] verts = new Vector3[vCount];
                Vector3[] norms = new Vector3[vCount];
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
                        float u = s / (float)segments * Mathf.PI * 2f;
                        float cx = Mathf.Cos(u);
                        float cz = Mathf.Sin(u);

                        int idx = r * (segments + 1) + s;
                        verts[idx] = new Vector3(cx * ringR, y, cz * ringR);
                        norms[idx] = new Vector3(cx * slopeXZ, slopeY, cz * slopeXZ).normalized;
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
                mesh.triangles = tris.ToArray();
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
