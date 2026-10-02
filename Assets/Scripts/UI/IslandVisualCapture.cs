#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Dex;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InsectGame.UI
{
    /// <summary>
    /// 나의 섬 화면의 <b>실제 IMGUI</b> 촬영 fixture(<c>-battleScenario island-ui</c>).
    ///
    /// 섬 화면은 전부 OnGUI라 배치 캡처에 안 찍힌다(<c>rules/testing.md</c> 「한계 셋」). 스탠드얼론 검수 빌드에서
    /// <c>ScreenCapture</c>로 찍는다. 섬 자체는 진짜 <see cref="IslandWorldBuilder"/>가 짓는다 — 지형·물건·곤충·
    /// 꾸미기 미리보기가 실제 경로다.
    ///
    /// <b>디스크에 쓰지 않는다.</b> 검수 빌드는 실제 게임과 같은 저장 폴더를 쓴다. 섬 매니저는
    /// <see cref="IslandManager.PersistenceEnabled"/>를 끄고, 재화를 건드리는 동작(구매·수확)은 부르지 않는다 —
    /// 지갑·캔디 컴포넌트는 차감 즉시 파일에 쓰기 때문이다.
    /// </summary>
    public static class IslandVisualCapture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static IEnumerator Run(string output, Camera camera)
        {
            int shots = 0;
            var holder = new GameObject("IslandQAData");
            holder.SetActive(false);

            // ── 종·보유 ──
            var database = ScriptableObject.CreateInstance<InsectDatabase>();
            InsectData rhino = Species(database, "rhinoceros_beetle", "장수풍뎅이", InsectRarity.Rare);
            InsectData stag = Species(database, "stag_beetle", "사슴벌레", InsectRarity.Rare);
            InsectData mantis = Species(database, "mantis_green", "사마귀", InsectRarity.Uncommon);
            InsectData azure = Species(database, "butterfly_azure", "푸른나비", InsectRarity.Epic);
            InsectData dragonfly = Species(database, "dragonfly_lake", "호수 잠자리", InsectRarity.Uncommon);
            InsectData beetle = Species(database, "beetle_basic", "들판 딱정벌레", InsectRarity.Common);
            InsectData hercules = Species(database, "beetle_hercules", "헤라클레스장수풍뎅이", InsectRarity.Legendary);

            var owned = new List<PlayerInsectData>
            {
                Owned("qa-rhino", rhino, 18, false),
                Owned("qa-stag", stag, 16, false),
                Owned("qa-mantis", mantis, 14, true),
                Owned("qa-azure", azure, 21, false),
                Owned("qa-dragonfly", dragonfly, 11, false),
                Owned("qa-beetle", beetle, 6, false),
                Owned("qa-hercules", hercules, 25, false),
            };
            var collection = holder.AddComponent<PlayerInsectCollection>();
            Set(collection, "database", database);
            Set(collection, "saveData", new PlayerInsectCollectionSave { insects = owned });
            var lookup = (Dictionary<string, PlayerInsectData>)Get(collection, "lookup");
            foreach (PlayerInsectData p in owned) lookup.Add(p.instanceId, p);

            var candy = holder.AddComponent<PlayerCandyInventory>();
            Set(candy, "data", new PlayerCandyData { candies = 340 });
            var wallet = holder.AddComponent<PlayerCurrencyWallet>();
            Set(wallet, "data", new PlayerCurrencyData { coins = 860, gems = 120 });
            var regionManager = holder.AddComponent<RegionManager>();

            // ── 섬 세이브(메모리) — 14칸 섬, 물건 14개, 곤충 4마리, 다섯 시간치 수확물 ──
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var save = new IslandSave
            {
                sizeLevel = 1, extraSlots = 1, starterGranted = true, guideDone = true,
                lastSettleUnix = now - 5 * 3600, harvestCount = 3,
            };
            foreach (var (id, x, z, rot) in new[]
                     {
                         ("b_cabin", -6, -2, 0), ("t_oak", 3, -1, 0), ("f_bench", -2, -3, 0), ("f_lantern", 1, -3, 0),
                         ("t_pond", 2, 2, 0), ("f_flowerpot", -3, -5, 0), ("f_flowerpot", 2, -5, 0),
                         ("t_flowerbed", -3, 0, 0), ("o_feeder", 0, -1, 0), ("f_fence", -6, -4, 0),
                         ("f_fence", -4, -4, 0), ("t_rock", 5, -4, 0), ("f_campfire", -1, 3, 0), ("b_storage", -6, 3, 0),
                     })
                save.placed.Add(new IslandPlacedRecord { id = id, x = x, z = z, rot = rot });
            foreach (var (id, count) in new[]
                     {
                         ("f_campfire", 2), ("f_table", 1), ("t_bush", 3), ("b_windmill", 1), ("f_sign", 1),
                         ("f_mailbox", 1), ("o_water", 1), ("t_sapling", 2),
                     })
                save.owned.Add(new IslandOwnedRecord { id = id, count = count });
            save.released.AddRange(new[] { "qa-rhino", "qa-azure", "qa-mantis", "qa-beetle" });
            save.bonds.Add(new IslandBondRecord { instanceId = "qa-rhino", hours = 40f });
            save.bonds.Add(new IslandBondRecord { instanceId = "qa-azure", hours = 95f });
            save.bonds.Add(new IslandBondRecord { instanceId = "qa-mantis", hours = 7f });

            var island = holder.AddComponent<IslandManager>();
            island.PersistenceEnabled = false;
            island.AutoWire(collection, wallet, candy, database);
            island.LoadForCapture(save);

            // ── 무대 — 섬 환경 프로필(SubAreaEnvironment의 섬 case)과 같은 값 ──
            camera.backgroundColor = new Color(0.56f, 0.80f, 0.96f);
            camera.farClipPlane = 400f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.40f, 0.44f, 0.50f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.66f, 0.84f, 0.95f);
            RenderSettings.fogDensity = 0.006f;
            var sun = new GameObject("IslandQASun").AddComponent<Light>();
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

            var visual = new GameObject("IslandQAPreview").AddComponent<InsectModelPreviewRenderer>();
            InsectVisual.Renderer = visual;

            var world = new GameObject("IslandQAWorld").AddComponent<IslandWorldBuilder>();
            world.AutoWire(regionManager, island, follower, movement, database);
            var share = new GameObject("IslandQAShare").AddComponent<IslandShareClient>();
            share.AutoWire(island);

            var editUi = new GameObject("IslandQAEdit").AddComponent<IslandEditUI>();
            editUi.AutoWire(island, world, null);   // 공유 클라이언트를 안 준다 — 닫을 때 서버로 올리지 않게
            var shopUi = new GameObject("IslandQAShop").AddComponent<IslandShopUI>();
            shopUi.AutoWire(island, wallet);
            var insectUi = new GameObject("IslandQAInsects").AddComponent<IslandInsectUI>();
            insectUi.AutoWire(island, collection);
            var guideUi = new GameObject("IslandQAGuide").AddComponent<IslandGuideUI>();
            guideUi.AutoWire(island, world, editUi);
            var hud = new GameObject("IslandQAHud").AddComponent<IslandHudUI>();
            hud.AutoWire(island, world, movement, share);
            var visitUi = new GameObject("IslandQAVisit").AddComponent<IslandVisitUI>();
            visitUi.AutoWire(island, world, share, null, hud);
            hud.AutoWire(editUi, shopUi, insectUi, visitUi, guideUi);
            var quick = new GameObject("IslandQAQuickBar").AddComponent<QuickAccessBarUI>();
            quick.AutoWire(visitUi);

            if (!world.EnterOwnIsland())
            {
                Debug.LogError("[IslandQA] 섬에 들어가지 못했다");
                Application.Quit(3);
                yield break;
            }
            // 안내는 뒤에서 따로 찍는다 — 들어올 때 스타터 키트·단계 진행이 돌았으므로 끝난 상태로 되돌린다.
            save.guideDone = true;

            yield return Wait(2.0f);
            shots++; yield return Capture(output, "01-island-hud");

            // ── 꾸미기 ──
            editUi.Open();
            yield return Wait(1.2f);
            shots++; yield return Capture(output, "02-edit");
            editUi.CarryForCapture("f_table", 0, 0, 0);
            yield return Wait(0.6f);
            shots++; yield return Capture(output, "03-edit-carry-ok");
            editUi.CarryForCapture("b_windmill", 2, -2, 0);   // 참나무와 겹친다
            yield return Wait(0.6f);
            shots++; yield return Capture(output, "04-edit-carry-blocked");
            editUi.PickPlacedForCapture(0);                    // 오두막을 집었다 — 옮기기·넣기
            yield return Wait(0.6f);
            shots++; yield return Capture(output, "05-edit-move-placed");
            editUi.CloseModal();
            yield return Wait(0.8f);

            // ── 상점 ──
            shopUi.Toggle();
            yield return Wait(0.8f);
            shots++; yield return Capture(output, "06-shop-furniture");
            Set(shopUi, "tab", 0);
            yield return Wait(0.5f);
            shots++; yield return Capture(output, "07-shop-building");
            Set(shopUi, "tab", 3);
            yield return Wait(0.5f);
            shots++; yield return Capture(output, "08-shop-tool");
            Set(shopUi, "tab", 4);
            yield return Wait(0.5f);
            shots++; yield return Capture(output, "09-shop-expand");
            shopUi.CloseModal();

            // ── 곤충 ──
            insectUi.Toggle();
            yield return Wait(1.6f);   // 썸네일이 프레임당 하나씩 렌더된다
            shots++; yield return Capture(output, "10-insects");
            insectUi.CloseModal();

            // ── 방문 창 ──
            visitUi.Toggle();
            yield return Wait(0.8f);
            shots++; yield return Capture(output, "11-visit-hub");
            visitUi.CloseModal();

            // ── 안내 — 첫 단계 배너, 꾸미기 화면 위의 배너, 도움말 ──
            save.guideDone = false;
            save.guideStep = (int)IslandGuideStep.OpenEdit;
            yield return Wait(0.6f);
            shots++; yield return Capture(output, "12-guide-first");
            save.guideStep = (int)IslandGuideStep.PlaceFirst;
            editUi.Open();
            yield return Wait(1.0f);
            shots++; yield return Capture(output, "13-guide-over-edit");
            editUi.CloseModal();
            save.guideStep = (int)IslandGuideStep.Finish;
            yield return Wait(0.8f);
            shots++; yield return Capture(output, "14-guide-finish");
            save.guideDone = true;
            guideUi.OpenHelp();
            yield return Wait(0.8f);
            shots++; yield return Capture(output, "15-help");
            guideUi.CloseModal();

            // ── 남의 섬 구경 ──
            var snapshot = new IslandSnapshot { ownerName = "하늘", sizeLevel = 0, comfort = 37 };
            foreach (var (id, x, z, rot) in new[]
                     {
                         ("b_greenhouse", -4, 0, 0), ("f_fountain", 1, 0, 0), ("t_blossom", -2, -3, 0),
                         ("f_hammock", 1, -3, 0), ("f_lantern", 4, -4, 0), ("o_honeypot", -5, -3, 0),
                     })
                snapshot.placed.Add(new IslandPlacedRecord { id = id, x = x, z = z, rot = rot });
            snapshot.insects.Add(new IslandSnapshotInsect { insectId = hercules.insectId, level = 30 });
            snapshot.insects.Add(new IslandSnapshotInsect { insectId = dragonfly.insectId, level = 12, shiny = true });
            snapshot.insects.Add(new IslandSnapshotInsect { insectId = stag.insectId, level = 19 });
            IslandSaveRules.SanitizeSnapshot(snapshot);
            hud.SetVisitInfo(new IslandInfo { ownerUid = "qa-owner", ownerName = "하늘", likes = 12, visits = 48 });
            world.EnterVisit(snapshot);
            yield return Wait(1.6f);
            shots++; yield return Capture(output, "16-visiting");

            // ── 가장 큰 섬 — 화면 없이 지형만(22칸) ──
            hud.SetVisitInfo(null);
            world.ReturnToOwnIsland();
            save.sizeLevel = GameConstants.Island.MaxSizeLevel;
            island.LoadForCapture(save);
            world.ExitIsland();
            yield return Wait(0.3f);
            world.EnterOwnIsland();
            save.guideDone = true;
            editUi.Open();   // 높은 부감으로 본다
            yield return Wait(1.4f);
            shots++; yield return Capture(output, "17-large-island-edit");
            editUi.CloseModal();

            File.WriteAllText(Path.Combine(output, "README.txt"),
                $"Actual standalone IMGUI over the real IslandWorldBuilder. {shots} shots at {Screen.width}x{Screen.height} " +
                $"(mobile layout={UIScale.IsMobileLayout}).\n" +
                "In-memory island save (IslandManager.PersistenceEnabled=false); wallet/candy on an inactive host and no " +
                "purchase/harvest calls, so nothing is written to disk.\n" +
                "Own island: size level 1 (14x14), 14 objects, 4 released insects, ~5h accrued. Visit: snapshot fixture.\n");
            Application.Quit(shots > 0 ? 0 : 3);
        }

        private static InsectData Species(InsectDatabase db, string id, string name, InsectRarity rarity)
        {
            var d = ScriptableObject.CreateInstance<InsectData>();
            d.insectId = id;
            d.displayName = name;
            d.rarity = rarity;
            d.description = name + " — 검수용 설명.";
            d.baseHp = 120;
            d.baseAtk = 34;
            d.baseDef = 28;
            d.baseSizeMm = 60f;
            d.baseWeightG = 12f;
            db.insects.Add(d);
            return d;
        }

        private static PlayerInsectData Owned(string instance, InsectData species, int level, bool shiny)
        {
            var p = new PlayerInsectData
            {
                instanceId = instance,
                insectId = species.insectId,
                level = level,
                ivHp = 10, ivAtk = 10, ivDef = 10,
                isShiny = shiny,
                sizeRoll = 50,
            };
            p.currentHp = p.GetTotalHp(species.baseHp);
            return p;
        }

        private static object Get(object target, string name)
        {
            FieldInfo f = target.GetType().GetField(name, Private);
            if (f == null) throw new MissingFieldException(target.GetType().Name, name);
            return f.GetValue(target);
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
            Object.Destroy(shot);
        }
    }
}
#endif
