#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Dex;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>Standalone IMGUI 검수. 메모리 상태만 주입하며 게임 저장 서비스를 실행하지 않는다.</summary>
    public static class InsectDetailVisualCapture
    {
        private const BindingFlags PrivateFields = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void Set(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, PrivateFields);
            if (field == null) throw new System.MissingFieldException(target.GetType().Name, fieldName);
            field.SetValue(target, value);
        }

        private static T AddInactive<T>(GameObject holder) where T : Component => holder.AddComponent<T>();

        public static IEnumerator Run(string output, Camera camera)
        {
            // 플레이어 데이터와 도감 기록은 전부 메모리에 만든다. 비활성 호스트에서
            // AddComponent 하므로 Awake의 디스크 로드도 호출되지 않는다.
            var dataHolder = new GameObject("InsectDetailQAData");
            dataHolder.SetActive(false);
            var database = ScriptableObject.CreateInstance<InsectDatabase>();
            InsectData beetle = Species("rhinoceros_beetle", "장수풍뎅이", InsectRarity.Rare,
                "튼튼한 뿔을 가진 숲의 강자. 큰 나무 주변에서 발견됩니다.", "나무 그늘과 수액이 있는 숲");
            InsectData stag = Species("stag_beetle", "사슴벌레", InsectRarity.Rare,
                "넓은 턱으로 상대를 밀어냅니다. 밤이 되면 활발히 움직입니다.", "오래된 참나무 숲");
            InsectData butterfly = Species("butterfly", "나비", InsectRarity.Uncommon,
                "가벼운 날개로 꽃밭을 돌아다니며 꽃가루를 옮깁니다.", "햇빛이 드는 꽃밭");
            database.insects.Add(beetle);
            database.insects.Add(stag);
            database.insects.Add(butterfly);

            PlayerInsectData first = Owned("qa-rhino-first", beetle.insectId, 16, 11, 13, 9, false);
            PlayerInsectData second = Owned("qa-rhino-second", beetle.insectId, 22, 15, 14, 13, true);
            PlayerInsectData third = Owned("qa-stag", stag.insectId, 18, 12, 10, 15, false);
            var collection = AddInactive<PlayerInsectCollection>(dataHolder);
            Set(collection, "database", database);
            Set(collection, "saveData", new PlayerInsectCollectionSave
            {
                insects = new List<PlayerInsectData> { first, second, third }
            });
            var ownedLookup = (Dictionary<string, PlayerInsectData>)typeof(PlayerInsectCollection)
                .GetField("lookup", PrivateFields).GetValue(collection);
            ownedLookup.Add(first.instanceId, first);
            ownedLookup.Add(second.instanceId, second);
            ownedLookup.Add(third.instanceId, third);

            var dexController = AddInactive<DexController>(dataHolder);
            var dexSave = new DexSaveData();
            var dexLookup = (Dictionary<string, DexRecord>)typeof(DexController)
                .GetField("lookup", PrivateFields).GetValue(dexController);
            foreach (InsectData species in database.insects)
            {
                var record = new DexRecord(species.insectId) { discoveredCount = 2, capturedCount = 1 };
                dexSave.records.Add(record);
                dexLookup.Add(species.insectId, record);
            }
            Set(dexController, "saveData", dexSave);

            var visual = new GameObject("InsectDetailQAPreview").AddComponent<InsectModelPreviewRenderer>();
            InsectVisual.Renderer = visual;
            var collectionUI = new GameObject("InsectDetailQACollection").AddComponent<CollectionUI>();
            collectionUI.AutoWire(collection, null, null);
            var dexUI = new GameObject("InsectDetailQADex").AddComponent<DexScreenUI>();
            dexUI.AutoWire(database, dexController);
            dexUI.AutoWire(collection, null);
            dexUI.AutoWire(visual);
            dexUI.AutoWire(collectionUI);

            // 추가된 하단 7개 버튼도 실 UI 컴포넌트로 캡처한다. 목적지 화면은
            // 검수용 비활성 호스트에 두므로 클릭 시 계정/저장 기능이 시작되지 않는다.
            var quickTargets = new GameObject("InsectDetailQAQuickTargets");
            quickTargets.SetActive(false);
            var quick = new GameObject("InsectDetailQAQuickBar").AddComponent<QuickAccessBarUI>();
            Set(quick, "dexScreen", dexUI);
            Set(quick, "collectionUI", collectionUI);
            Set(quick, "battleTeamUI", AddInactive<BattleTeamUI>(quickTargets));
            Set(quick, "inventoryScreen", AddInactive<InventoryUI>(quickTargets));
            Set(quick, "questUI", AddInactive<TutorialQuestUI>(quickTargets));
            Set(quick, "regionMapUI", AddInactive<RegionMapUI>(quickTargets));

            camera.transform.position = new Vector3(0f, 7f, -12f);
            camera.transform.LookAt(new Vector3(0f, .6f, 0f));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 8f;
            ground.GetComponent<Renderer>().material.color = new Color(.16f, .27f, .23f);
            var light = new GameObject("InsectDetailQAKey").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -25f, 0f);

            yield return Capture(output, "01-quick-buttons");
            dexUI.Toggle();
            yield return Capture(output, "02-dex-grid");
            Set(dexUI, "detailModalOpen", true);
            Set(dexUI, "selectedIndex", 0);
            yield return Capture(output, "03-dex-detail");
            Set(dexUI, "previewAngle", 210f);
            Set(dexUI, "previewShiny", true);
            yield return Capture(output, "04-dex-detail-variant-angle");
            Set(dexUI, "detailModalOpen", false);
            Set(dexUI, "currentTab", 1);
            yield return Capture(output, "05-dex-owned-cards");
            dexUI.CloseModal();
            if (!collectionUI.OpenDetailForInstance(second.instanceId))
            {
                Debug.LogError("Collection detail fixture could not select second owned insect.");
                Application.Quit(3);
                yield break;
            }
            yield return Capture(output, "06-collection-instance-detail");
            collectionUI.CloseModal();
            collectionUI.Toggle();
            yield return Capture(output, "07-collection-list");
            collectionUI.CloseModal();

            File.WriteAllText(Path.Combine(output, "README.txt"),
                "Actual standalone ScreenCapture including DexScreenUI, CollectionUI and QuickAccessBarUI IMGUI.\n" +
                "Synthetic in-memory species, three owned insects (two of the same species), and captured dex records.\n" +
                "The collection detail selects qa-rhino-second by unique instanceId.\n" +
                "Dex tabs, detail open, angle and shiny states were set programmatically for screenshots.\n" +
                "These images do not prove pointer clicks or scroll gesture routing; click interaction needs live input.\n" +
                "No PlaySceneBootstrap, AuthManager, PlayerInsectCollection.Awake, DexController.Awake, or save calls.\n" +
                "Screen " + Screen.width + "x" + Screen.height + ". Background is a staging fixture.\n");

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-insectUiInteractive") >= 0)
            {
                File.AppendAllText(Path.Combine(output, "README.txt"),
                    "Interactive QA enabled: the quick bar remains visible for 180 seconds after automatic capture. " +
                    "Use it to click Dex and Collection controls in the standalone window; the window then closes automatically.\n");
                float interactiveUntil = Time.realtimeSinceStartup + 180f;
                while (Time.realtimeSinceStartup < interactiveUntil)
                    yield return null;
            }
            Application.Quit(0);
        }

        private static InsectData Species(string id, string name, InsectRarity rarity, string description, string habitat)
        {
            var data = ScriptableObject.CreateInstance<InsectData>();
            data.insectId = id;
            data.displayName = name;
            data.rarity = rarity;
            data.description = description;
            data.habitatHint = habitat;
            data.baseHp = 120;
            data.baseAtk = 35;
            data.baseDef = 28;
            data.baseSizeMm = 70f;
            data.baseWeightG = 20f;
            return data;
        }

        private static PlayerInsectData Owned(string instance, string species, int level,
            int hp, int attack, int defense, bool shiny)
        {
            return new PlayerInsectData
            {
                instanceId = instance,
                insectId = species,
                level = level,
                ivHp = hp,
                ivAtk = attack,
                ivDef = defense,
                isShiny = shiny,
                currentHp = 120,
                sizeRoll = 50
            };
        }

        private static IEnumerator Capture(string output, string name)
        {
            yield return new WaitForSecondsRealtime(.8f);
            yield return new WaitForEndOfFrame();
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
        }
    }
}
#endif
