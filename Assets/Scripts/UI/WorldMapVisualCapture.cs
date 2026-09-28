#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;
using InsectGame.Story;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>Standalone IMGUI map fixture. State is injected in memory; no bootstrap or save services.</summary>
    public static class WorldMapVisualCapture
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void Set(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(name, Fields);
            if (field == null) throw new System.MissingFieldException(instance.GetType().Name, name);
            field.SetValue(instance, value);
        }

        public static IEnumerator Run(string output, Camera camera)
        {
            RegionData[] regions = RegionDefinitions.CreateAll();
            RegionData meadow = WorldRouteLayout.Find(regions, "meadow");
            if (meadow == null || meadow.subAreas == null || meadow.subAreas.Length == 0)
            { Debug.LogError("Map fixture requires meadow and its entrance."); Application.Quit(3); yield break; }
            SubAreaData entrance = meadow.subAreas[0];
            var manager = new GameObject("MapQARegionState").AddComponent<RegionManager>();
            manager.enabled = false;
            // Do not call Initialize: it loads account progression. All fixture flags are transient.
            Set(manager, "regions", regions);
            Set(manager, "currentRegion", meadow);
            Set(manager, "unlockedRegions", new HashSet<string> { "meadow", "pond", "forest" });
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = entrance.centerPosition + new Vector3(-12f, .5f, -8f);
            var npcs = new GameObject("MapQANpcs").AddComponent<NpcManager>();
            npcs.enabled = false;
            var npc = new GameObject("MapQAStoryNpc").AddComponent<VillagerNpc>();
            npc.Initialize(new NpcSpawnAnchor { regionId = "meadow", position = player.transform.position + Vector3.right * 10f },
                "map_qa_elder", "마을 어르신", 8173, "village_elder");
            npc.transform.position = player.transform.position + Vector3.right * 10f;
            ((List<VillagerNpc>)typeof(NpcManager).GetField("storyNpcs", Fields).GetValue(npcs)).Add(npc);
            var tracker = new GameObject("MapQAObjective").AddComponent<StoryObjectiveTracker>();
            tracker.enabled = false;
            Set(tracker, "hasObjective", true);
            Set(tracker, "hasWorldTarget", true);
            Set(tracker, "targetPosition", npc.transform.position);
            Set(tracker, "playerTransform", player.transform);
            Set(tracker, "targetNpc", npc);
            Set(tracker, "label", "마을 어르신에게 말 걸기");
            var map = new GameObject("MapQAWorldMap").AddComponent<RegionMapUI>();
            Set(map, "regionManager", manager);
            Set(map, "playerTransform", player.transform);
            map.AutoWire(npcs);
            map.AutoWire(tracker);
            var minimap = new GameObject("MapQAMinimap").AddComponent<MinimapUI>();
            minimap.AutoWire(manager);
            minimap.AutoWire(npcs);
            minimap.AutoWire(tracker);
            camera.transform.position = player.transform.position + new Vector3(0f, 25f, -22f);
            camera.transform.LookAt(player.transform.position);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "MapQAGround";
            ground.transform.position = player.transform.position - Vector3.up * .5f;
            ground.transform.localScale = Vector3.one * 12f;
            ground.GetComponent<Renderer>().material.color = new Color(.22f, .31f, .26f);
            var light = new GameObject("MapQAKey").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            map.Toggle();
            yield return Capture(output, "map-overworld");
            Set(manager, "currentSubArea", entrance);
            manager.SetSubAreaSticky(true);
            player.transform.position = new Vector3(2000f, .5f, 2000f);
            yield return Capture(output, "map-subarea");
            map.CloseModal();
            yield return Capture(output, "minimap-subarea");
            Set(manager, "currentSubArea", null);
            manager.SetSubAreaSticky(false);
            player.transform.position = entrance.centerPosition + new Vector3(-12f, .5f, -8f);
            yield return Capture(output, "minimap-overworld");
            File.WriteAllText(Path.Combine(output, "README.txt"),
                "Artificial layout fixture; actual standalone RegionMapUI/MinimapUI IMGUI captured after end-of-frame. " +
                "Real RegionDefinitions and WorldRouteLayout; injected meadow/pond/forest unlocks, elder position and objective. " +
                "Subarea state and player coordinates are injected, not an actual entry transition. " +
                "No PlaySceneBootstrap, AuthManager, StoryDirector, PlayerProgressController or save-service creation; " +
                "RegionManager.Initialize is deliberately not called. Does not validate traversal, persistence, clicks or quest progression. " +
                "Screen " + Screen.width + "x" + Screen.height + ". Camera background is a staging fixture.");
            Application.Quit(0);
        }

        private static IEnumerator Capture(string output, string name)
        {
            yield return new WaitForSecondsRealtime(.8f);
            yield return new WaitForEndOfFrame();
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), screenshot.EncodeToPNG());
            Object.Destroy(screenshot);
        }
    }
}
#endif
