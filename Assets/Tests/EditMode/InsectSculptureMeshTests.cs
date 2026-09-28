#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using InsectGame.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    public class InsectSculptureMeshTests
    {
        private static IEnumerable<InsectSculptureMeshes.Shape> Shapes =>
            (InsectSculptureMeshes.Shape[])Enum.GetValues(typeof(InsectSculptureMeshes.Shape));

        [TestCaseSource(nameof(Shapes))]
        public void Sculpture_IsClosedOutwardFiniteAndWithinBudget(InsectSculptureMeshes.Shape shape)
        {
            Mesh mesh = InsectSculptureMeshes.Get(shape);
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Assert.LessOrEqual(vertices.Length, 180);
            Assert.Greater(mesh.bounds.size.sqrMagnitude, .001f);
            var edgeUses = new Dictionary<long, int>();
            var edgeDirections = new Dictionary<long, int>();
            double volume = 0;
            foreach (Vector3 v in vertices)
                Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
                    || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                Assert.That(a, Is.InRange(0, vertices.Length - 1));
                Assert.That(b, Is.InRange(0, vertices.Length - 1));
                Assert.That(c, Is.InRange(0, vertices.Length - 1));
                Assert.Greater(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).sqrMagnitude, 1e-14f);
                volume += Vector3.Dot(vertices[a], Vector3.Cross(vertices[b], vertices[c])) / 6.0;
                AddEdge(a, b, edgeUses, edgeDirections); AddEdge(b, c, edgeUses, edgeDirections); AddEdge(c, a, edgeUses, edgeDirections);
            }
            foreach (long edge in edgeUses.Keys)
            {
                Assert.AreEqual(2, edgeUses[edge], "Open or non-manifold edge: " + shape);
                Assert.AreEqual(0, edgeDirections[edge], "Inconsistent winding: " + shape);
            }
            Assert.Greater(volume, 0.0, "Inside-out mesh: " + shape);
            foreach (Vector3 normal in mesh.normals) Assert.AreEqual(1f, normal.magnitude, .001f);
        }

        [TestCase(InsectSculptureMeshes.Shape.RhinoHorn)]
        [TestCase(InsectSculptureMeshes.Shape.MantisFemurLeft)]
        [TestCase(InsectSculptureMeshes.Shape.MantisBladeRight)]
        [TestCase(InsectSculptureMeshes.Shape.ElytronRight)]
        public void RoundedPoles_HaveAxialDepthInsteadOfExposedFlatDisks(InsectSculptureMeshes.Shape shape)
        {
            Vector3[] vertices = InsectSculptureMeshes.Get(shape).vertices;
            const int sides = 10, rings = 17;
            for (int end = 0; end < 2; end++)
            {
                int offset = end == 0 ? 0 : (rings - 1) * sides;
                Vector3 pole = vertices[rings * sides + end];
                Vector3 center = Vector3.zero;
                for (int j = 0; j < sides; j++) center += vertices[offset + j] / sides;
                Vector3 axis = center - pole;
                Assert.Greater(axis.magnitude, .005f, "A ring centered on its cap produces a visible cut face");
                for (int j = 0; j < sides; j++)
                    Assert.Greater(Vector3.Dot(vertices[offset + j] - pole, axis.normalized), 0f,
                        "Cap must taper toward a terminal pole");
            }
        }

        [Test]
        public void MantisHead_RoundedSurface_HasWidthForEyesWithoutPrismFaces()
        {
            Mesh head = InsectSculptureMeshes.Get(InsectSculptureMeshes.Shape.MantisHead);
            Assert.Greater(head.vertexCount, 100, "The old six-point prism produced a block-like side silhouette");
            Assert.Greater(head.bounds.size.x, .25f, "Crown must support the eyes at x +/- .18");
            Assert.Less(head.bounds.size.z, .20f);
        }

        [Test]
        public void MantisBlades_StayInsideCompactFoldedForelegEnvelope()
        {
            Bounds blade = InsectSculptureMeshes.Get(InsectSculptureMeshes.Shape.MantisBladeRight).bounds;
            Assert.Greater(blade.min.y, .20f, "An overlong tip droops below the folded foreleg");
            Assert.Greater(blade.min.z, .40f);
            Assert.Less(blade.size.y, .16f);
        }

        private static void AddEdge(int from, int to, Dictionary<long, int> uses, Dictionary<long, int> directions)
        {
            long key = ((long)Math.Min(from, to) << 32) | (uint)Math.Max(from, to);
            uses.TryGetValue(key, out int count); uses[key] = count + 1;
            directions.TryGetValue(key, out int direction); directions[key] = direction + (from < to ? 1 : -1);
        }

        [Test]
        public void MirroredMandibles_HaveMatchingBoundsAndSeparateTips()
        {
            Mesh left = InsectSculptureMeshes.Get(InsectSculptureMeshes.Shape.StagJawLeft);
            Mesh right = InsectSculptureMeshes.Get(InsectSculptureMeshes.Shape.StagJawRight);
            Assert.AreEqual(-left.bounds.center.x, right.bounds.center.x, .0001f);
            Assert.AreEqual(left.bounds.size, right.bounds.size);
            Assert.Less(left.bounds.max.x, 0f);
            Assert.Greater(right.bounds.min.x, 0f);
        }

        [Test]
        public void Catalogue_ReusesMeshesAcrossInstances_AndHasFiniteSize()
        {
            foreach (InsectSculptureMeshes.Shape shape in Shapes)
                Assert.AreSame(InsectSculptureMeshes.Get(shape), InsectSculptureMeshes.Get(shape));
            Assert.AreEqual(Enum.GetValues(typeof(InsectSculptureMeshes.Shape)).Length, InsectSculptureMeshes.CachedCount);
        }
    }
}
#endif
