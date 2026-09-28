using System;
using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Spawning
{
    /// <summary>Finite sculpture catalogue: shared meshes, never per insect allocations.
    /// Local coordinates match InsectEntity anatomy; consumers must not destroy these meshes.</summary>
    public static class InsectSculptureMeshes
    {
        public enum Shape
        {
            RhinoHorn, RhinoForkLeft, RhinoForkRight, ThoraxHorn, RhinoPronotum,
            ElytronLeft, ElytronRight, StagJawLeft, StagJawRight,
            MantisThorax, MantisFemurLeft, MantisFemurRight,
            MantisBladeLeft, MantisBladeRight, MantisHead, Wing,
            ButterflyForewing, ButterflyHindwing, DragonflyWing
        }

        private static readonly Dictionary<Shape, Mesh> Cache = new Dictionary<Shape, Mesh>();
        public static int CachedCount => Cache.Count;

        public static Mesh Get(Shape shape)
        {
            if (!Enum.IsDefined(typeof(Shape), shape)) throw new ArgumentOutOfRangeException(nameof(shape));
            if (Cache.TryGetValue(shape, out Mesh mesh) && mesh != null) return mesh;
            mesh = Build(shape);
            mesh.name = "InsectSculpture_" + shape;
            mesh.hideFlags = HideFlags.HideAndDontSave;
            Cache[shape] = mesh;
            return mesh;
        }

        // Domain-reload-disabled sessions also release their previous finite catalogue.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            foreach (Mesh mesh in Cache.Values)
            {
                if (mesh == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
            Cache.Clear();
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private static Mesh Build(Shape shape)
        {
            switch (shape)
            {
                case Shape.RhinoHorn:
                    return Sweep(V(0f, .15f, .57f), V(0f, .47f, .62f), V(0f, .78f, .85f), V(0f, .78f, 1.04f), .105f, .028f, 0f, .78f);
                case Shape.RhinoForkLeft:
                case Shape.RhinoForkRight:
                    return Mirrored(Sweep(V(0f, .78f, 1.01f), V(.05f, .80f, 1.05f), V(.13f, .82f, 1.08f), V(.16f, .83f, 1.20f), .033f, .002f, 0f, .72f), shape == Shape.RhinoForkLeft);
                case Shape.RhinoPronotum:
                    return Sweep(V(0f, .08f, .10f), V(0f, .16f, .22f), V(0f, .14f, .48f),
                        V(0f, .08f, .65f), .18f, .14f, .075f, .65f);
                case Shape.ThoraxHorn:
                    return Sweep(V(0f, .20f, .22f), V(0f, .35f, .32f), V(0f, .46f, .44f), V(0f, .48f, .65f), .10f, .004f, 0f, .8f);
                case Shape.ElytronLeft:
                case Shape.ElytronRight:
                    return Mirrored(Elytron(), shape == Shape.ElytronLeft);
                case Shape.StagJawLeft:
                case Shape.StagJawRight:
                    return Mirrored(Sweep(V(.14f, .12f, .80f), V(.41f, .13f, .95f), V(.35f, .14f, 1.23f), V(.06f, .14f, 1.39f), .085f, .003f, .006f, .60f), shape == Shape.StagJawLeft);
                case Shape.MantisThorax:
                    return Sweep(V(0f, -.015f, -.20f), V(0f, .08f, .02f), V(0f, .25f, .21f), V(0f, .32f, .31f), .075f, .046f, .008f, .82f);
                case Shape.MantisFemurLeft:
                case Shape.MantisFemurRight:
                    return Mirrored(Sweep(V(.075f, .18f, .17f), V(.24f, .16f, .25f), V(.27f, .25f, .43f), V(.23f, .38f, .53f), .028f, .022f, .009f, .58f), shape == Shape.MantisFemurLeft);
                case Shape.MantisBladeLeft:
                case Shape.MantisBladeRight:
                    return Mirrored(Sweep(V(.235f, .34f, .51f), V(.23f, .29f, .50f), V(.21f, .245f, .465f), V(.20f, .22f, .42f), .021f, .004f, .004f, .55f), shape == Shape.MantisBladeLeft);
                case Shape.MantisHead:
                    return Head();
                case Shape.Wing:
                    return Sweep(V(-.5f, 0f, 0f), V(-.17f, .08f, .07f), V(.22f, .04f, .09f), V(.5f, 0f, 0f), .002f, .002f, .49f, 1f);
                case Shape.ButterflyForewing:
                    return WingPlate(new[] {
                        new Vector2(-.50f, -.04f), new Vector2(-.44f, .20f),
                        new Vector2(-.30f, .43f), new Vector2(-.08f, .54f),
                        new Vector2(.14f, .50f), new Vector2(.34f, .34f),
                        new Vector2(.50f, .08f), new Vector2(.45f, -.12f),
                        new Vector2(.26f, -.18f), new Vector2(.02f, -.14f),
                        new Vector2(-.25f, -.17f)
                    }, .014f);
                case Shape.ButterflyHindwing:
                    return WingPlate(new[] {
                        new Vector2(-.47f, .13f), new Vector2(-.30f, .25f),
                        new Vector2(-.07f, .27f), new Vector2(.18f, .21f),
                        new Vector2(.42f, .08f), new Vector2(.50f, -.14f),
                        new Vector2(.36f, -.38f), new Vector2(.12f, -.51f),
                        new Vector2(-.14f, -.47f), new Vector2(-.39f, -.28f)
                    }, .012f);
                case Shape.DragonflyWing:
                    return WingPlate(new[] {
                        new Vector2(-.50f, 0f), new Vector2(-.40f, .075f),
                        new Vector2(-.16f, .14f), new Vector2(.18f, .13f),
                        new Vector2(.43f, .065f), new Vector2(.50f, 0f),
                        new Vector2(.39f, -.075f), new Vector2(.10f, -.12f),
                        new Vector2(-.20f, -.11f), new Vector2(-.42f, -.06f)
                    }, .008f);
                default: throw new ArgumentOutOfRangeException(nameof(shape));
            }
        }

        // A thin closed plate gives flying insects a readable wing outline from
        // above and from the battle camera's shallow angle. Shared by every
        // instance of a family, so the extra contour has no per-spawn mesh cost.
        private static Mesh WingPlate(Vector2[] outline, float halfThickness)
        {
            int count = outline.Length;
            var vertices = new Vector3[count * 2 + 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[count * 12];
            Vector2 center = Vector2.zero;
            for (int i = 0; i < count; i++) center += outline[i] / count;
            for (int i = 0; i < count; i++)
            {
                vertices[i] = V(outline[i].x, halfThickness, outline[i].y);
                vertices[i + count] = V(outline[i].x, -halfThickness, outline[i].y);
                uv[i] = uv[i + count] = outline[i] + Vector2.one * .5f;
            }
            int top = count * 2, bottom = top + 1;
            vertices[top] = V(center.x, halfThickness, center.y);
            vertices[bottom] = V(center.x, -halfThickness, center.y);
            uv[top] = uv[bottom] = center + Vector2.one * .5f;
            int k = 0;
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                triangles[k++] = top; triangles[k++] = i; triangles[k++] = next;
                triangles[k++] = bottom; triangles[k++] = next + count; triangles[k++] = i + count;
                triangles[k++] = i; triangles[k++] = i + count; triangles[k++] = next;
                triangles[k++] = i + count; triangles[k++] = next + count; triangles[k++] = next;
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles, uv = uv };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Elytron()
        {
            Mesh mesh = Sweep(V(.205f, -.005f, -.54f), V(.215f, .145f, -.30f),
                V(.215f, .145f, .09f), V(.205f, .045f, .34f), .015f, .045f, .18f, .64f);
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                // Curl the outer lip into the abdomen. A symmetric tube left its
                // farthest lateral edge suspended above the narrower body.
                float outer = Mathf.Clamp01((vertices[i].x - .215f) / .205f);
                vertices[i].y -= .09f * outer * outer;
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Mirrored(Mesh mesh, bool mirror)
        {
            if (!mirror) return mesh;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i].x = -vertices[i].x;
            int[] indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int index = indices[i]; indices[i] = indices[i + 1]; indices[i + 1] = index;
            }
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Closed cubic sweep, 17 interior rings × 10 sides + 2 rounded poles. Parallel-transported
        // frames avoid the twist discontinuity of crossing a fixed world-up vector.
        private static Mesh Sweep(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            float startRadius, float endRadius, float bulge, float flatten)
        {
            const int rings = 17, sides = 10;
            var vertices = new Vector3[rings * sides + 2];
            var uv = new Vector2[vertices.Length];
            var indices = new int[((rings - 1) * sides * 2 + sides * 2) * 3];
            Vector3 previousTangent = (b - a).normalized;
            Vector3 reference = Mathf.Abs(Vector3.Dot(previousTangent, Vector3.up)) < .9f ? Vector3.up : Vector3.forward;
            Vector3 normal = Vector3.Cross(previousTangent, reference).normalized;
            for (int i = 0; i < rings; i++)
            {
                float t = (i + 1f) / (rings + 1f), u = 1f - t;
                Vector3 center = u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
                Vector3 tangent = (3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c)).normalized;
                normal = Quaternion.FromToRotation(previousTangent, tangent) * normal;
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                float radius = Mathf.Lerp(startRadius, endRadius, t) + bulge * Mathf.Sin(Mathf.PI * t);
                // The terminal vertices are true poles, not centers of flat cut
                // disks. Ease the nearby rings into each pole like a rounded cap.
                float capProfile = Mathf.Sqrt(Mathf.Clamp01(Mathf.Min(t, 1f - t) / .12f));
                radius *= capProfile;
                for (int j = 0; j < sides; j++)
                {
                    float angle = 2f * Mathf.PI * j / sides;
                    vertices[i * sides + j] = center + radius * (Mathf.Cos(angle) * normal + Mathf.Sin(angle) * binormal * flatten);
                    uv[i * sides + j] = new Vector2(j / (float)sides, t);
                }
                previousTangent = tangent;
            }
            int k = 0;
            for (int i = 0; i < rings - 1; i++)
                for (int j = 0; j < sides; j++)
                {
                    int p = i * sides + j, q = i * sides + (j + 1) % sides;
                    indices[k++] = p; indices[k++] = q; indices[k++] = p + sides;
                    indices[k++] = q; indices[k++] = q + sides; indices[k++] = p + sides;
                }
            int start = rings * sides, end = start + 1;
            vertices[start] = a; vertices[end] = d;
            for (int j = 0; j < sides; j++)
            {
                int next = (j + 1) % sides;
                indices[k++] = start; indices[k++] = next; indices[k++] = j;
                indices[k++] = end; indices[k++] = (rings - 1) * sides + j; indices[k++] = (rings - 1) * sides + next;
            }
            Mesh mesh = new Mesh { vertices = vertices, triangles = indices, uv = uv };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Head()
        {
            // Rounded triangular head: narrow forward chin, broad eye-bearing crown.
            // The old six-vertex prism presented a flat rectangular side to the camera.
            return Sweep(V(0f, .22f, .39f), V(0f, .27f, .35f), V(0f, .36f, .30f),
                V(0f, .40f, .28f), .018f, .16f, .02f, .42f);
        }
    }
}
