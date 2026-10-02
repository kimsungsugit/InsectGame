#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Dex;
using InsectGame.Spawning;
using InsectGame.Story;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 「챔피언의 꿈」의 섬·도입·깨어남 화면 촬영 fixture(<c>-battleScenario dream-island</c>).
    ///
    /// 진짜 <see cref="IslandWorldBuilder"/>가 꾸며진 섬을 짓고 진짜 <see cref="DreamPrologueDirector"/>가 안내를 그린다
    /// (IMGUI라 배치 캡처에는 안 잡힌다 — <c>rules/testing.md</c> 「한계 셋」). 전투 화면은 <c>dream-battle</c>이 따로 찍는다.
    /// 저장을 부르지 않는 메모리 fixture다.
    /// </summary>
    public static class DreamVisualCapture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static IEnumerator Run(string output, Camera camera)
        {
            int shots = 0;
            var holder = new GameObject("DreamQAData");
            holder.SetActive(false);

            // 섬에 나올 모든 종 — ID는 저작 데이터 그대로다(오타가 나면 그 곤충이 안 찍힌다).
            var database = ScriptableObject.CreateInstance<InsectDatabase>();
            var added = new HashSet<string>();
            foreach (var s in DreamPrologueData.IslandInsects) Species(database, added, s.insectId, s.insectId);
            Species(database, added, DreamPrologueData.AceInsectId, "헤라클레스 천공각");
            Species(database, added, DreamPrologueData.ChallengerInsectId, "태고의 비천룡");

            var collection = holder.AddComponent<PlayerInsectCollection>();
            Set(collection, "database", database);
            Set(collection, "saveData", new PlayerInsectCollectionSave());
            var candy = holder.AddComponent<PlayerCandyInventory>();
            Set(candy, "data", new PlayerCandyData());
            var wallet = holder.AddComponent<PlayerCurrencyWallet>();
            Set(wallet, "data", new PlayerCurrencyData());
            var regionManager = holder.AddComponent<RegionManager>();
            var island = holder.AddComponent<IslandManager>();
            island.PersistenceEnabled = false;
            island.AutoWire(collection, wallet, candy, database);
            island.LoadForCapture(new IslandSave { starterGranted = true, guideDone = true });

            // 섬 환경 프로필(SubAreaEnvironment의 섬 case)과 같은 값
            camera.backgroundColor = new Color(0.56f, 0.80f, 0.96f);
            camera.farClipPlane = 400f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.40f, 0.44f, 0.50f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.66f, 0.84f, 0.95f);
            RenderSettings.fogDensity = 0.006f;
            var sun = new GameObject("DreamQASun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.97f, 0.88f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, 35f, 0f);

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.GetComponent<Renderer>().material.color = new Color(0.3f, 0.45f, 0.85f);
            PlayerMovement movement = player.AddComponent<PlayerMovement>();
            movement.AutoWire(regionManager);
            CameraFollower follower = camera.GetComponent<CameraFollower>();
            follower.SetTarget(player.transform);

            InsectVisual.Renderer = new GameObject("DreamQAPreview").AddComponent<InsectModelPreviewRenderer>();

            var world = new GameObject("DreamQAWorld").AddComponent<IslandWorldBuilder>();
            world.AutoWire(regionManager, island, follower, movement, database);
            // 섬 HUD는 DreamMode에서 숨는다 — 일부러 만들어 두어 "정말 숨는가"도 같이 본다.
            var hud = new GameObject("DreamQAIslandHud").AddComponent<IslandHudUI>();
            hud.AutoWire(island, world, movement, null);

            var director = new GameObject("DreamQADirector").AddComponent<DreamPrologueDirector>();
            director.AutoWire(null, movement, player.transform, regionManager, world, null, null, database, null);

            // 도입 카드
            director.ShowForCapture("opening", 1.4f);
            yield return Wait(0.3f);
            shots++; yield return Capture(output, "01-opening-card");

            // 섬 도착 — 챔피언의 섬 카드
            director.ShowForCapture("island-title", 0.8f);
            yield return Wait(1.6f);
            shots++; yield return Capture(output, "02-island-title");

            // 걷기 안내 — 섬 HUD가 없어야 한다(꿈 모드)
            director.ShowForCapture("island-move", 2f);
            yield return Wait(0.4f);
            shots++; yield return Capture(output, "03-island-move");

            // 곤충에게 다가가기 — 가장 가까운 곤충을 가리키는 표식
            director.ShowForCapture("island-approach", 2f);
            yield return Wait(0.5f);
            shots++; yield return Capture(output, "04-island-approach");

            // 곤충 소개 카드
            director.ShowForCapture("island-card", 2f);
            yield return Wait(0.4f);
            shots++; yield return Capture(output, "05-island-card");

            // 분수 앞으로 — 목표 표식
            director.ShowForCapture("island-fountain", 2f);
            yield return Wait(0.5f);
            shots++; yield return Capture(output, "06-island-fountain");

            // 분수 가까이에서 본 광장
            Vector3 fountain = DreamPrologueData.FountainWorldPosition();
            player.transform.position = fountain + new Vector3(0f, 0.5f, -4.5f);
            follower.SnapToTarget();
            yield return Wait(0.9f);
            shots++; yield return Capture(output, "07-fountain-plaza");

            // 깨어남 자막(흰 화면이 걷히는 중)
            director.ShowForCapture("wake", 0.9f);
            yield return Wait(0.3f);
            shots++; yield return Capture(output, "08-wake-caption");

            director.EndCapture();
            File.WriteAllText(Path.Combine(output, "README.txt"),
                $"Actual standalone IMGUI over the real IslandWorldBuilder + DreamPrologueDirector. {shots} shots at " +
                $"{Screen.width}x{Screen.height} (mobile layout={UIScale.IsMobileLayout}).\n" +
                "Island: DreamPrologueData.BuildIslandSnapshot via EnterVisit (DreamMode on). In-memory fixture; nothing is saved.\n" +
                "Species are stand-in InsectData with the authored IDs (stats/skills are not the real database).\n");
            Application.Quit(shots > 0 ? 0 : 3);
        }

        private static void Species(InsectDatabase db, HashSet<string> added, string id, string name)
        {
            if (!added.Add(id)) return;
            var d = ScriptableObject.CreateInstance<InsectData>();
            d.insectId = id;
            d.displayName = name;
            d.rarity = InsectRarity.Legendary;
            d.description = name + " — 검수용 설명.";
            d.baseHp = 150;
            d.baseAtk = 40;
            d.baseDef = 30;
            d.baseSizeMm = 70f;
            d.baseWeightG = 14f;
            db.insects.Add(d);
        }

        private static void Set(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name, Private);
            if (f == null) throw new MissingFieldException(target.GetType().Name, name);
            f.SetValue(target, value);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private static IEnumerator Capture(string output, string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), shot.EncodeToPNG());
            UnityEngine.Object.Destroy(shot);
        }
    }
}
#endif
