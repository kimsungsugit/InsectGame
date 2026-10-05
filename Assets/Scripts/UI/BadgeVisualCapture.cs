#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 배지 획득 연출·배지 케이스의 실제 IMGUI 촬영 fixture(<c>-battleScenario badge</c>).
    ///
    /// <b>저장을 건드리지 않는다</b> — 격파 기록과 수령 상태를 리플렉션으로 메모리에만 넣는다
    /// (<c>RegionManager.DefeatGuardian</c>·<c>GuardianBadgeService.TryClaim</c>은 PlayerPrefs에 쓴다).
    /// 연출은 서비스 이벤트 대신 핸들러를 직접 불러 연다 — 순서(대사보다 먼저)는 걸음 검증이 본다.
    /// </summary>
    public static class BadgeVisualCapture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static IEnumerator Run(string output, Camera camera)
        {
            RegionData[] regions = RegionDefinitions.CreateAll();
            new GameObject("VillageQA").AddComponent<VillageBuilder>().Build(regions);
            GameObject village = GameObject.Find("MainVillage");
            Vector3 center = village != null ? village.transform.position : Vector3.zero;
            camera.transform.position = center + new Vector3(0, 14, 23);
            camera.transform.LookAt(center + Vector3.up);
            Light light = new GameObject("BadgeQAKey").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(48, -30, 0);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = center;
            ground.transform.localScale = Vector3.one * 20;
            ground.GetComponent<Renderer>().material.color = new Color(.30f, .40f, .29f);

            var host = new GameObject("BadgeQA");
            RegionManager regionManager = host.AddComponent<RegionManager>();
            regionManager.Initialize(regions);
            SetDefeated(regionManager, "meadow", "pond", "forest", "garden");

            GuardianBadgeService service = host.AddComponent<GuardianBadgeService>();
            service.AutoWire(regionManager, null);
            typeof(GuardianBadgeService).GetField("claimed", Private).SetValue(service, new HashSet<int>());
            typeof(GuardianBadgeService).GetField("loaded", Private).SetValue(service, true);

            ItemDatabase items = ItemDatabase.CreateRuntimeDefault();
            BadgeCeremonyUI ceremony = new GameObject("BadgeCeremonyQA").AddComponent<BadgeCeremonyUI>();
            ceremony.AutoWire(service, regionManager, items);
            MethodInfo onAwarded = typeof(BadgeCeremonyUI).GetMethod("OnAwarded", Private);

            int shots = 0;
            // 1) 네 번째 배지 + 이정표 보상 카드 — 연출 타임라인을 따라 찍는다.
            onAwarded.Invoke(ceremony, new object[] { new GuardianBadgeService.Award
                { regionId = "garden", earned = 4, milestones = new[] { 4 } } });
            float opened = Time.realtimeSinceStartup;
            foreach (float at in new[] { 0.3f, 0.6f, 0.95f, 1.2f, 1.55f, 2.0f, 2.7f })
            {
                while (Time.realtimeSinceStartup - opened < at) yield return null;
                shots++;
                yield return Capture(output, $"ceremony-garden-{at:0.00}");
            }
            ceremony.CloseModal();

            // 2) 2막 은빛 배지(보상 없음) — 끝 장면만.
            SetDefeated(regionManager, "meadow", "pond", "forest", "garden", "swamp", "mountain", "ruins",
                "hollow", "dunes", "frostline");
            onAwarded.Invoke(ceremony, new object[] { new GuardianBadgeService.Award
                { regionId = "frostline", earned = 10, milestones = new int[0] } });
            opened = Time.realtimeSinceStartup;
            foreach (float at in new[] { 0.9f, 2.7f })
            {
                while (Time.realtimeSinceStartup - opened < at) yield return null;
                shots++;
                yield return Capture(output, $"ceremony-frostline-{at:0.00}");
            }
            ceremony.CloseModal();

            // 3) 케이스 — 배지 4개, 이정표 4 받을 수 있음. 얻은 배지·안 가 본 땅 두 가지 상세.
            SetDefeated(regionManager, "meadow", "pond", "forest", "garden");
            BadgeCaseUI badgeCase = new GameObject("BadgeCaseQA").AddComponent<BadgeCaseUI>();
            badgeCase.AutoWire(service, regionManager, items);
            badgeCase.Toggle();
            yield return new WaitForSecondsRealtime(0.6f);
            shots++;
            yield return Capture(output, "case-earned");
            typeof(BadgeCaseUI).GetField("selected", Private).SetValue(badgeCase, 11);
            typeof(BadgeCaseUI).GetMethod("BuildDetail", Private).Invoke(badgeCase, null);
            yield return new WaitForSecondsRealtime(0.4f);
            shots++;
            yield return Capture(output, "case-locked");
            badgeCase.CloseModal();

            File.WriteAllText(Path.Combine(output, "README.txt"),
                $"Actual standalone IMGUI. {shots} shots. Guardian records and milestone claims injected in memory " +
                "(no PlayerPrefs writes). Ceremony opened by calling its award handler directly; ordering against " +
                "story dialogue is validated by StoryBeatWalkthrough, not here. Village backdrop is a staging fixture.");
            Object.Destroy(items);
            Application.Quit(shots > 0 ? 0 : 3);
        }

        private static void SetDefeated(RegionManager manager, params string[] ids)
        {
            typeof(RegionManager).GetField("defeatedGuardians", Private).SetValue(manager, new HashSet<string>(ids));
            typeof(RegionManager).GetField("unlockedRegions", Private).SetValue(manager,
                new HashSet<string>(new[] { "meadow", "pond", "forest", "garden", "swamp" }));
        }

        private static IEnumerator Capture(string output, string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), shot.EncodeToPNG());
            Object.Destroy(shot);
        }
    }
}
#endif
