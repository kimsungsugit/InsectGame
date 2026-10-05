#if UNITY_EDITOR
using System;
using System.IO;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;
using InsectGame.Spawning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace InsectGame.EditorTools
{
    /// <summary>Isolated, deterministic model gallery. Camera captures exclude IMGUI.
    /// Batch: -executeMethod InsectGame.EditorTools.ModelDesignCapture.Run -modelCaptureOut path
    /// Do not pass -quit or -nographics; requires a licensed editor and graphics device.</summary>
    public static class ModelDesignCapture
    {
        private const string Pending = "InsectGame.ModelDesignCapture.Pending";

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Gallery capture requires batch mode to protect the open scene.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
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
            try { CaptureGallery(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(3); }
        }

        private static void CaptureGallery()
        {
            string output = "Artifacts/model-design";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-modelCaptureOut") output = args[i + 1];
            Directory.CreateDirectory(output);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.46f, 0.53f);
            Light light = new GameObject("GalleryKey").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            Camera camera = new GameObject("GalleryCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.10f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 30f;
            string[] ids = { "rhinoceros_beetle", "stag_beetle", "mantis", "monarch_butterfly", "dragonfly", "ant" };
            foreach (string id in ids)
            {
                var data = ScriptableObject.CreateInstance<InsectData>();
                data.insectId = id;
                GameObject model = new GameObject(id);
                model.AddComponent<InsectEntity>().BuildForBattle(data, 1, false);
                CaptureViews(camera, model, output, id);
                model.SetActive(false);
                UnityEngine.Object.Destroy(model);
                UnityEngine.Object.Destroy(data);
            }
            GameObject player = new GameObject("GalleryPlayer");
            player.SetActive(false);
            player.AddComponent<PlayerVisualBuilder>().BuildForPreview(new AppearanceSpec());
            player.SetActive(true);
            CaptureViews(camera, player, output, "player");
            player.SetActive(false);
            UnityEngine.Object.Destroy(player);
            for (int i = 0; i < 2; i++)
            {
                GameObject npc = new GameObject("GalleryNpc");
                NpcVisualBuilder.Build(npc.transform, i == 0 ? NpcVisualBuilder.RandomVillager(12) : NpcVisualBuilder.RandomKid(12));
                CaptureViews(camera, npc, output, i == 0 ? "adult" : "child");
                npc.SetActive(false);
                NpcVisualBuilder.CleanupMaterials(npc.transform);
                UnityEngine.Object.Destroy(npc);
            }
            File.WriteAllText(Path.Combine(output, "README.txt"), "27 model-only PNGs: front, side and rear for six insects and player/adult/child. Identical lighting, per-model bounds framing. Excludes IMGUI, gameplay, Android performance, and animation validation.");
        }

        private static void CaptureViews(Camera camera, GameObject model, string output, string name)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            camera.orthographicSize = Mathf.Max(bounds.size.y, Mathf.Max(bounds.size.x, bounds.size.z)) * 0.7f;
            Vector3[] directions = { new Vector3(1f, 0.55f, 1.8f), new Vector3(2f, 0.4f, 0f), new Vector3(-1f, 0.55f, -1.8f) };
            string[] views = { "front", "side", "rear" };
            RenderTexture rt = RenderTexture.GetTemporary(768, 768, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = new Texture2D(768, 768, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                for (int i = 0; i < directions.Length; i++)
                {
                    camera.transform.position = bounds.center + directions[i].normalized * 7f;
                    camera.transform.LookAt(bounds.center);
                    camera.Render();
                    RenderTexture.active = rt;
                    pixels.ReadPixels(new Rect(0f, 0f, 768f, 768f), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + "_" + views[i] + ".png"), pixels.EncodeToPNG());
                }
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
