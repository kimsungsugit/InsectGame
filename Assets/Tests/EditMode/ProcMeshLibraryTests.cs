#if UNITY_EDITOR
using NUnit.Framework;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 프로시저럴 메시 생성기.
    ///
    /// 이 코드의 결함은 <b>런타임에 조용하다</b> — 와인딩이 뒤집히면 예외 없이 면이 안 보이고,
    /// 노멀이 틀리면 셰이딩만 이상해지며, NaN이 하나 섞이면 그 메시 전체가 사라진다.
    /// 배치모드 캡처로는 "뭔가 이상하다"까지만 알 수 있어서 여기서 수치로 고정한다.
    ///
    /// 크기 인자는 실제 캐릭터가 쓰는 값과 겹치지 않게 잡았다 — 정적 캐시를 공유하므로
    /// 테스트가 게임 메시를 밀어내지 않도록.
    /// </summary>
    [TestFixture]
    public class ProcMeshLibraryTests
    {
        // ── 원판 ──

        [Test]
        public void Disc_VertexCount_IsCenterPlusRing()
        {
            Mesh m = ProcMeshLibrary.Disc(0.101f, 0.101f, 0f, 16);

            Assert.AreEqual(17, m.vertexCount, "중심 1 + 링 16이어야 한다");
            Assert.AreEqual(16 * 3, m.triangles.Length, "부채꼴 삼각형이 세그먼트 수만큼");
        }

        /// <summary>
        /// 내장 Sphere를 눌러 쓰던 자리를 대체하는 게 목적이다. 그 Sphere가 515정점이었다 —
        /// 원판 하나에 그 밀도를 쓰던 게 이 라이브러리를 만든 직접적인 이유다.
        /// </summary>
        [Test]
        public void Disc_IsFarCheaperThanBuiltinSphere()
        {
            Mesh m = ProcMeshLibrary.Disc(0.102f, 0.102f, 0f, 16);

            Assert.Less(m.vertexCount, 50, "원판이 50정점을 넘으면 대체의 의미가 없다");
        }

        /// <summary>bulge가 0이면 완전 평면, 크면 중심이 앞으로 나온다(옆에서 봐도 눈이 보인다).</summary>
        [Test]
        public void Disc_Bulge_LiftsOnlyTheCenter()
        {
            Mesh flat = ProcMeshLibrary.Disc(0.103f, 0.103f, 0f, 12);
            Mesh domed = ProcMeshLibrary.Disc(0.103f, 0.103f, 0.02f, 12);

            Assert.AreEqual(0f, flat.vertices[0].z, 1e-5f, "bulge 0이면 중심도 평면");
            Assert.AreEqual(0.02f, domed.vertices[0].z, 1e-5f, "bulge만큼 중심이 앞으로");
            Assert.AreEqual(0f, domed.vertices[1].z, 1e-5f, "테두리는 그대로 평면에 남는다");
        }

        // ── 저폴리 구체 ──

        [Test]
        public void LowSphere_Bounds_MatchRequestedRadii()
        {
            Mesh m = ProcMeshLibrary.LowSphere(0.31f, 0.22f, 0.19f, 8, 12);
            Bounds b = m.bounds;

            Assert.AreEqual(0.62f, b.size.x, 0.02f);
            Assert.AreEqual(0.44f, b.size.y, 0.02f);
            Assert.AreEqual(0.38f, b.size.z, 0.02f);
        }

        [Test]
        public void LowSphere_IsCheaperThanBuiltinSphere()
        {
            Mesh m = ProcMeshLibrary.LowSphere(0.32f, 0.32f, 0.32f, 8, 12);

            Assert.Less(m.vertexCount, 515, "내장 Sphere(515정점)보다 싸야 대체할 이유가 있다");
        }

        // ── 둥근 상자 ──

        [Test]
        public void RoundedBox_Bounds_MatchesRequestedSize()
        {
            Vector3 size = new Vector3(0.481f, 0.461f, 0.381f);
            Mesh m = ProcMeshLibrary.RoundedBox(size, 0.08f, 3);
            Bounds b = m.bounds;

            Assert.AreEqual(size.x, b.size.x, 0.01f, "요청한 폭과 달라지면 기존 좌표 배치가 어긋난다");
            Assert.AreEqual(size.y, b.size.y, 0.01f);
            Assert.AreEqual(size.z, b.size.z, 0.01f);
            Assert.AreEqual(Vector3.zero, b.center, "중심이 원점이어야 기존 localPosition을 그대로 쓴다");
        }

        [Test]
        public void RoundedBox_VertexCount_StaysUnderBudget()
        {
            Mesh m = ProcMeshLibrary.RoundedBox(new Vector3(0.482f, 0.462f, 0.382f), 0.08f, 3);

            Assert.LessOrEqual(m.vertexCount, 120, "몸통 하나에 120정점을 넘으면 예산을 다시 봐야 한다");
        }

        /// <summary>
        /// 반경이 가장 짧은 반쪽 변보다 크면 형태가 뒤집힌다(모서리가 서로를 지나친다).
        /// 얇은 파츠에 큰 반경을 넘겨도 죽지 않아야 한다.
        /// </summary>
        [Test]
        public void RoundedBox_OversizedRadius_ClampsInsteadOfInverting()
        {
            Vector3 size = new Vector3(0.4f, 0.041f, 0.1f);   // 아주 납작한 판
            Mesh m = ProcMeshLibrary.RoundedBox(size, 5f, 2);
            Bounds b = m.bounds;

            Assert.AreEqual(size.x, b.size.x, 0.01f, "반경을 clamp해도 요청 크기는 지켜야 한다");
            Assert.AreEqual(size.y, b.size.y, 0.01f);
            Assert.Greater(m.vertexCount, 0);
        }

        /// <summary>
        /// 인접 면이 모서리에서 정확히 같은 좌표를 내야 틈이 안 생긴다. 경계 정점의 좌표가
        /// 요청 크기의 반쪽을 넘지 않는 것으로 확인한다(넘으면 밀어내기가 과했다는 뜻).
        /// </summary>
        [Test]
        public void RoundedBox_NoVertexEscapesTheRequestedBox()
        {
            Vector3 size = new Vector3(0.483f, 0.463f, 0.383f);
            Mesh m = ProcMeshLibrary.RoundedBox(size, 0.09f, 3);
            Vector3 half = size * 0.5f;

            foreach (Vector3 v in m.vertices)
            {
                Assert.LessOrEqual(Mathf.Abs(v.x), half.x + 1e-3f, "정점이 상자 밖으로 나갔다");
                Assert.LessOrEqual(Mathf.Abs(v.y), half.y + 1e-3f);
                Assert.LessOrEqual(Mathf.Abs(v.z), half.z + 1e-3f);
            }
        }

        // ── 테이퍼드 캡슐 ──

        [Test]
        public void TaperedCapsule_TopIsNarrowerWhenTapered()
        {
            Mesh m = ProcMeshLibrary.TaperedCapsule(0.051f, 0.091f, 0.3f, 8, 10);

            float topWidth = 0f;
            float bottomWidth = 0f;
            foreach (Vector3 v in m.vertices)
            {
                float r = new Vector2(v.x, v.z).magnitude;
                if (v.y > 0.10f) topWidth = Mathf.Max(topWidth, r);
                if (v.y < -0.10f) bottomWidth = Mathf.Max(bottomWidth, r);
            }

            Assert.Less(topWidth, bottomWidth, "어깨→손목처럼 위가 가늘어야 테이퍼의 의미가 있다");
        }

        /// <summary>
        /// 전체 높이는 몸통 + 양 끝 반구다 — 내장 Capsule과 같은 규약이라야 기존 좌표를 그대로 쓴다.
        /// </summary>
        [Test]
        public void TaperedCapsule_TotalHeight_IncludesBothCaps()
        {
            Mesh m = ProcMeshLibrary.TaperedCapsule(0.06f, 0.06f, 0.32f, 8, 10);

            Assert.AreEqual(0.32f + 0.06f * 2f, m.bounds.size.y, 0.01f);
        }

        [Test]
        public void TaperedCapsule_IsCheaperThanBuiltinCapsule()
        {
            Mesh m = ProcMeshLibrary.TaperedCapsule(0.052f, 0.092f, 0.31f, 8, 10);

            Assert.Less(m.vertexCount, 552, "내장 Capsule(552정점)보다 싸야 한다");
        }

        // ── 공통 건전성 ──

        /// <summary>
        /// 볼록 형상은 모든 삼각형이 바깥을 향해야 한다. 와인딩이 뒤집히면 백페이스 컬링에
        /// 걸려 <b>예외 없이</b> 안 보인다 — 이 프로젝트에서 가장 잡기 어려운 종류의 결함이다.
        /// </summary>
        [Test]
        public void LowSphere_EveryTriangleFacesOutward()
        {
            AssertOutwardWinding(ProcMeshLibrary.LowSphere(0.33f, 0.33f, 0.33f, 8, 12), "LowSphere");
        }

        [Test]
        public void RoundedBox_EveryTriangleFacesOutward()
        {
            AssertOutwardWinding(ProcMeshLibrary.RoundedBox(new Vector3(0.484f, 0.464f, 0.384f), 0.08f, 3), "RoundedBox");
        }

        [Test]
        public void TaperedCapsule_EveryTriangleFacesOutward()
        {
            AssertOutwardWinding(ProcMeshLibrary.TaperedCapsule(0.053f, 0.093f, 0.32f, 8, 10), "TaperedCapsule");
        }

        [Test]
        public void Diamond_EveryTriangleFacesOutward()
        {
            AssertOutwardWinding(ProcMeshLibrary.Diamond(0.51f, 1.01f, 0.61f), "Diamond");
        }

        /// <summary>
        /// <b>원판은 위 검사에 걸 수 없다</b> — 평면이라 "바깥"의 기준인 <c>bounds.center</c>가
        /// 면 위에 놓여 방향 판정이 성립하지 않는다. 그래서 처음엔 Disc만 와인딩 검사에서
        /// 빠져 있었고, 그 틈으로 <b>실제 P0가 통과했다</b>: 삼각형 순서가 정점 노멀과 반대라
        /// 눈·동공·하이라이트·홍조가 정면에서 백페이스 컬링됐다(예외도 경고도 없다).
        ///
        /// 평면에는 다른 기준이 필요하다 — <b>면 노멀이 자기 정점 노멀과 같은 쪽</b>인가.
        /// 이 규약은 곡면에도 그대로 성립하므로 다른 생성기에도 함께 건다.
        /// </summary>
        [Test]
        public void EveryGenerator_FaceWindingAgreesWithVertexNormals()
        {
            AssertWindingMatchesNormals(ProcMeshLibrary.Disc(0.106f, 0.106f, 0.02f, 16), "Disc");
            AssertWindingMatchesNormals(ProcMeshLibrary.Disc(0.107f, 0.107f, 0f, 12), "Disc(평면)");
            AssertWindingMatchesNormals(ProcMeshLibrary.LowSphere(0.38f, 0.38f, 0.38f, 8, 12), "LowSphere");
            AssertWindingMatchesNormals(ProcMeshLibrary.RoundedBox(new Vector3(0.488f, 0.468f, 0.388f), 0.08f, 3), "RoundedBox");
            AssertWindingMatchesNormals(ProcMeshLibrary.TaperedCapsule(0.056f, 0.096f, 0.35f, 8, 10), "TaperedCapsule");
        }

        private static void AssertWindingMatchesNormals(Mesh mesh, string label)
        {
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            int[] t = mesh.triangles;

            int disagreeing = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (face.sqrMagnitude < 1e-12f) continue;   // 축퇴 삼각형은 방향이 없다

                // 세 정점 노멀의 평균 — 한 정점만 보면 곡률이 큰 자리에서 오판한다.
                Vector3 vertexNormal = n[t[i]] + n[t[i + 1]] + n[t[i + 2]];
                if (Vector3.Dot(face, vertexNormal) < 0f) disagreeing++;
            }

            Assert.AreEqual(0, disagreeing,
                label + ": 면 와인딩이 정점 노멀과 반대인 삼각형 " + disagreeing +
                "개 — 그만큼 백페이스 컬링돼 예외 없이 사라진다");
        }

        /// <summary>
        /// 극 링에 넓이 0인 삼각형을 싣지 않는다 — 정점을 줄이려고 만든 라이브러리가
        /// 빈 삼각형을 20개씩 그리고 있었다.
        /// </summary>
        [Test]
        public void ClosedGenerators_ContainNoDegenerateTriangles()
        {
            AssertNoDegenerates(ProcMeshLibrary.LowSphere(0.39f, 0.39f, 0.39f, 8, 12), "LowSphere");
            AssertNoDegenerates(ProcMeshLibrary.TaperedCapsule(0.057f, 0.097f, 0.36f, 8, 10), "TaperedCapsule");
        }

        private static void AssertNoDegenerates(Mesh mesh, string label)
        {
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;

            int degenerate = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (face.sqrMagnitude < 1e-12f) degenerate++;
            }

            Assert.AreEqual(0, degenerate, label + ": 넓이 0인 삼각형 " + degenerate + "개");
        }

        /// <summary>
        /// 반구 끝(극점)의 노멀은 축 방향이어야 한다. 수평 성분에 sin을 곱하지 않으면
        /// 극점이 45°로 눕고 캡 전체 셰이딩이 어긋난다 — 실제로 그 상태였다.
        /// </summary>
        [Test]
        public void TaperedCapsule_PoleNormals_PointAlongTheAxis()
        {
            Mesh m = ProcMeshLibrary.TaperedCapsule(0.058f, 0.098f, 0.37f, 8, 10);
            Vector3[] v = m.vertices;
            Vector3[] n = m.normals;

            float topY = m.bounds.max.y;
            float bottomY = m.bounds.min.y;

            for (int i = 0; i < v.Length; i++)
            {
                if (Mathf.Abs(v[i].y - topY) < 1e-4f)
                    Assert.AreEqual(1f, n[i].y, 0.05f, "위 극점 노멀이 +Y가 아니다");
                if (Mathf.Abs(v[i].y - bottomY) < 1e-4f)
                    Assert.AreEqual(-1f, n[i].y, 0.05f, "아래 극점 노멀이 −Y가 아니다");
            }
        }

        private static void AssertOutwardWinding(Mesh mesh, string label)
        {
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            Vector3 center = mesh.bounds.center;

            int inverted = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]];
                Vector3 b = v[t[i + 1]];
                Vector3 c = v[t[i + 2]];

                Vector3 faceNormal = Vector3.Cross(b - a, c - a);
                if (faceNormal.sqrMagnitude < 1e-12f) continue;   // 축퇴 삼각형은 넘어간다

                Vector3 outward = (a + b + c) / 3f - center;
                if (Vector3.Dot(faceNormal, outward) < 0f) inverted++;
            }

            Assert.AreEqual(0, inverted, label + ": 안쪽을 향한 삼각형이 " + inverted + "개 — 그만큼 면이 사라진다");
        }

        [Test]
        public void EveryGenerator_ProducesFiniteVertices()
        {
            AssertFinite(ProcMeshLibrary.Disc(0.104f, 0.104f, 0.01f, 16), "Disc");
            AssertFinite(ProcMeshLibrary.LowSphere(0.34f, 0.24f, 0.2f, 8, 12), "LowSphere");
            AssertFinite(ProcMeshLibrary.RoundedBox(new Vector3(0.485f, 0.465f, 0.385f), 0.08f, 3), "RoundedBox");
            AssertFinite(ProcMeshLibrary.TaperedCapsule(0.054f, 0.094f, 0.33f, 8, 10), "TaperedCapsule");
            AssertFinite(ProcMeshLibrary.Diamond(0.52f, 1.02f, 0.62f), "Diamond");
        }

        private static void AssertFinite(Mesh mesh, string label)
        {
            Assert.Greater(mesh.vertexCount, 0, label + ": 정점이 없다");
            Assert.AreEqual(0, mesh.triangles.Length % 3, label + ": 삼각형 인덱스가 3의 배수가 아니다");

            foreach (Vector3 v in mesh.vertices)
            {
                Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z),
                    label + ": NaN 정점이 있다 — 메시 전체가 사라진다");
                Assert.IsFalse(float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z),
                    label + ": 무한대 정점이 있다");
            }

            foreach (int idx in mesh.triangles)
            {
                Assert.That(idx, Is.InRange(0, mesh.vertexCount - 1), label + ": 삼각형 인덱스가 범위 밖이다");
            }
        }

        [Test]
        public void EveryGenerator_NormalsAreUnitLength()
        {
            AssertUnitNormals(ProcMeshLibrary.Disc(0.105f, 0.105f, 0.01f, 16), "Disc");
            AssertUnitNormals(ProcMeshLibrary.LowSphere(0.35f, 0.25f, 0.21f, 8, 12), "LowSphere");
            AssertUnitNormals(ProcMeshLibrary.RoundedBox(new Vector3(0.486f, 0.466f, 0.386f), 0.08f, 3), "RoundedBox");
            AssertUnitNormals(ProcMeshLibrary.TaperedCapsule(0.055f, 0.095f, 0.34f, 8, 10), "TaperedCapsule");
        }

        private static void AssertUnitNormals(Mesh mesh, string label)
        {
            Vector3[] normals = mesh.normals;
            Assert.AreEqual(mesh.vertexCount, normals.Length, label + ": 노멀 개수가 정점 수와 다르다");

            foreach (Vector3 n in normals)
            {
                Assert.AreEqual(1f, n.magnitude, 0.02f, label + ": 노멀이 단위벡터가 아니면 셰이딩이 어긋난다");
            }
        }

        // ── 캐시 ──

        /// <summary>
        /// 같은 인자면 같은 인스턴스를 돌려줘야 한다. 아니면 플레이어·마네킹·NPC가 각자 메시를
        /// 만들어 프로세스 수명 캐시라는 전제가 무너진다(그리고 아무도 그걸 회수하지 않는다).
        /// </summary>
        [Test]
        public void Cache_SameArguments_ReturnSameInstance()
        {
            Mesh a = ProcMeshLibrary.RoundedBox(new Vector3(0.487f, 0.467f, 0.387f), 0.08f, 3);
            Mesh b = ProcMeshLibrary.RoundedBox(new Vector3(0.487f, 0.467f, 0.387f), 0.08f, 3);

            Assert.AreSame(a, b);
        }

        [Test]
        public void Cache_DifferentArguments_ReturnDifferentInstances()
        {
            Mesh a = ProcMeshLibrary.LowSphere(0.36f, 0.36f, 0.36f, 8, 12);
            Mesh b = ProcMeshLibrary.LowSphere(0.37f, 0.36f, 0.36f, 8, 12);

            Assert.AreNotSame(a, b, "크기가 다르면 다른 메시여야 한다 — 같으면 한쪽이 잘못된 크기로 그려진다");
        }

        /// <summary>모양이 달라도 수치가 겹치면 안 된다 — 캐시 키에 Shape이 들어가는 이유.</summary>
        [Test]
        public void Cache_DifferentShapesWithSameNumbers_DoNotCollide()
        {
            Mesh disc = ProcMeshLibrary.Disc(0.5f, 0.5f, 0f, 8);
            Mesh sphere = ProcMeshLibrary.LowSphere(0.5f, 0.5f, 0f, 8, 8);

            Assert.AreNotSame(disc, sphere);
        }
    }
}
#endif
