#if UNITY_EDITOR
using System;
using System.IO;
using InsectGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace InsectGame.EditorTools
{
    /// <summary>Isolated village geometry capture; no bootstrap, saves, or account services.</summary>
    public static class VillageDesignCapture
    {
        private const string Pending = "InsectGame.VillageDesignCapture.Pending";

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a separate batch editor.");
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
            EditorApplication.update -= Tick;
            SessionState.SetBool(Pending, false);
            try { Capture(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(3); }
        }

        private static void Capture()
        {
            string output = "Artifacts/village-design";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-villageCaptureOut") output = args[i + 1];
            if (Directory.Exists(output) && Directory.GetFileSystemEntries(output).Length > 0)
                throw new IOException("Capture directory must be empty.");
            Directory.CreateDirectory(output);
            UnityEngine.Random.InitState(8173);
            var regions = RegionDefinitions.CreateAll();
            new GameObject("VillageCaptureHost").AddComponent<VillageBuilder>().Build(regions);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.48f, .50f, .53f);
            Light key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 250f;
            ground.GetComponent<Renderer>().material.color = new Color(.30f, .40f, .29f);
            Camera camera = new GameObject("CaptureCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.13f, .19f, .23f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 160f;
            camera.fieldOfView = 50f;
            Transform main = GameObject.Find("MainVillage").transform;
            Shot(camera, main.position, new Vector3(0, 29, 32), output, "village_overview");
            foreach (string name in new[] { "House_1", "Shop", "Hospital", "TrainingHall", "GachaHut" })
            {
                Transform building = main.Find(name);
                if (building == null) throw new InvalidOperationException("Missing " + name);
                Vector3 target = building.position + Vector3.up * 1.8f;
                Vector3 offset = building.TransformDirection(new Vector3(7, 4, 11));
                Shot(camera, target, offset, output, name);
            }
            foreach (string id in new[] { "forest", "swamp", "mountain" })
            {
                Transform outpost = GameObject.Find("Outpost_" + id).transform;
                Shot(camera, outpost.position + Vector3.up * 1.4f,
                    outpost.TransformDirection(new Vector3(7, 5, 13)), output, "outpost_" + id);
            }
            File.WriteAllText(Path.Combine(output, "README.txt"),
                "Seed 8173. Actual VillageBuilder geometry, fixed lighting/cameras. Isolated from saves. Excludes NPCs, IMGUI, terrain clutter, input and Android performance.");
        }

        private static void Shot(Camera camera, Vector3 target, Vector3 offset, string output, string name)
        {
            camera.transform.position = target + offset;
            camera.transform.LookAt(target);
            RenderTexture rt = RenderTexture.GetTemporary(1280, 800, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
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
    }
}
#endif
