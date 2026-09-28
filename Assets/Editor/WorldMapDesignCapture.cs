#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace InsectGame.EditorTools
{
    /// <summary>Actual combined ground/scenery/buildings without invoking account or save systems.</summary>
    public static class WorldMapDesignCapture
    {
        private const string Pending = "InsectGame.WorldMapDesignCapture.Pending";
        private static Camera camera;
        private static RegionData[] regions;
        private static double builtAt;
        private static string output;

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Requires a separate batch editor.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (SessionState.GetBool(Pending, false)) EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (camera == null) { Build(); return; }
                if (EditorApplication.timeSinceStartup - builtAt < 1) return;
                EditorApplication.update -= Tick;
                SessionState.SetBool(Pending, false);
                AuditRoutes();
                foreach (RegionData region in regions)
                    Shot(region.centerPosition, new Vector3(.15f, 1.15f, .72f) * region.radius, region.regionId);
                Vector3 village = VillageBuilder.GetMainVillageCenter(regions[0].centerPosition, regions[0].radius);
                Shot(village, new Vector3(0, 29, 32), "village_full_field");
                Shot(Vector3.Lerp(regions[0].centerPosition, regions[1].centerPosition, .58f), new Vector3(0, 33, 22), "meadow_pond_crossing");
                GameObject bridge = GameObject.Find("Bridge_PondRiver_Floor");
                if (bridge != null) Shot(bridge.transform.position, new Vector3(12, 16, 12), "pond_bridge_detail");
                foreach (string regionId in new[] { "meadow", "pond", "ruins" })
                {
                    RegionData region = WorldRouteLayout.Find(regions, regionId);
                    Vector3 gateway = WorldRouteLayout.GetGateway(region, regions);
                    Vector3 outward = (gateway - region.centerPosition).normalized;
                    Vector3 target = gateway + Vector3.Cross(Vector3.up, outward) * 3.4f + Vector3.up * 2.05f;
                    Shot(target, -outward * 8f + Vector3.up * 2.2f, regionId + "_sign_inside");
                    Shot(target, outward * 8f + Vector3.up * 2.2f, regionId + "_sign_outside");
                }
                File.WriteAllText(Path.Combine(output, "README.txt"),
                    "Actual EnsureGround (terrain/scenery/subarea entries) plus VillageBuilder, seed8173. Bootstrap remains inactive: no BuildSystems, accounts, saves, NPCs or gameplay. Fixed overview cameras; excludes IMGUI, physical traversal and Android performance.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                SessionState.SetBool(Pending, false);
                EditorApplication.update -= Tick;
                Debug.LogException(error);
                EditorApplication.Exit(3);
            }
        }

        private static void Build()
        {
            output = "Artifacts/world-map";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-mapCaptureOut") output = args[i + 1];
            if (Directory.Exists(output) && Directory.GetFileSystemEntries(output).Length > 0)
                throw new IOException("Capture directory must be empty.");
            Directory.CreateDirectory(output);
            UnityEngine.Random.InitState(8173);
            GameObject host = new GameObject("InactiveGroundOnlyFixture");
            host.SetActive(false); // Never run Bootstrap.Awake -> BuildSystems.
            var bootstrap = host.AddComponent<PlaySceneBootstrap>();
            typeof(PlaySceneBootstrap).GetMethod("EnsureGround", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bootstrap, null);
            string error = (string)typeof(PlaySceneBootstrap).GetField("groundError", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bootstrap);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            regions = RegionDefinitions.CreateAll();
            new GameObject("VillageHost").AddComponent<VillageBuilder>().Build(regions);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.40f, .44f, .48f);
            Light light = new GameObject("CaptureKey").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.transform.rotation = Quaternion.Euler(48, -30, 0);
            camera = new GameObject("MapCaptureCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .19f, .23f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 1300f;
            camera.fieldOfView = 65f;
            builtAt = EditorApplication.timeSinceStartup;
        }

        private static void Shot(Vector3 target, Vector3 offset, string name)
        {
            camera.transform.position = target + offset;
            camera.transform.LookAt(target);
            RenderTexture rt = RenderTexture.GetTemporary(1440, 960, 24);
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(1440, 960, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, 1440, 960), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(pixels);
            }
        }

        private static void AuditRoutes()
        {
            Physics.SyncTransforms();
            using (var report = new StreamWriter(Path.Combine(output, "route-collisions.tsv")))
            {
                report.WriteLine("route\tsamples\tblockedSamples\tobstacles");
                foreach (WorldRouteEdge edge in WorldRouteLayout.FieldConnections)
                {
                    Vector3[] route = WorldRouteLayout.BuildRoute(regions, edge.FromRegionId, edge.ToRegionId);
                    int samples = 0, blocked = 0;
                    var obstacles = new HashSet<string>();
                    for (int segment = 1; segment < route.Length; segment++)
                    {
                        int steps = Mathf.CeilToInt(Vector3.Distance(route[segment - 1], route[segment]) / .75f);
                        for (int step = 0; step <= steps; step++)
                        {
                            Vector3 point = Vector3.Lerp(route[segment - 1], route[segment], (float)step / Mathf.Max(1, steps));
                            point.y = 1.48f;
                            bool hit = false;
                            foreach (Collider collider in Physics.OverlapSphere(point, .4f))
                            {
                                if (collider.isTrigger || collider.bounds.size.y < .25f) continue;
                                hit = true; obstacles.Add(collider.name);
                            }
                            samples++; if (hit) blocked++;
                        }
                    }
                    report.WriteLine($"{edge.FromRegionId}-{edge.ToRegionId}\t{samples}\t{blocked}\t{string.Join(",", obstacles)}");
                }
            }
        }
    }
}
#endif
