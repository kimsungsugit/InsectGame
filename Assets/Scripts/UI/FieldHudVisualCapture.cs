#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Dex;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 필드 HUD·포획 선택·배틀팀 화면의 **실제 IMGUI** 촬영 fixture(<c>-battleScenario field-ui</c>).
    ///
    /// 세 화면 다 OnGUI라 배치 캡처(LiveSceneCapture·FieldDesignTour)에는 안 찍힌다
    /// (<c>rules/testing.md</c> 「한계 셋」). 스탠드얼론 검수 빌드에서 <c>ScreenCapture</c>로 찍는다.
    ///
    /// <b>디스크 저장을 부르지 않는다</b> — 플레이어 상태 컴포넌트는 비활성 호스트에 붙여 Awake(로드)를 막고
    /// private 필드에 메모리 값을 넣는다(<see cref="InsectDetailVisualCapture"/>와 같은 방식).
    /// 필드 배경은 무대용 평지다 — 실제 리전 지형은 FieldDesignTour가 본다.
    /// </summary>
    public static class FieldHudVisualCapture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static IEnumerator Run(string output, Camera camera)
        {
            int shots = 0;
            var holder = new GameObject("FieldHudQAData");
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
                Owned("qa-rhino", rhino, 18, 14, 13, 11, false, -1),
                Owned("qa-stag", stag, 16, 9, 15, 12, false, 38),        // 부상
                Owned("qa-mantis", mantis, 14, 11, 12, 8, true, -1),
                Owned("qa-azure", azure, 21, 15, 14, 13, false, -1),
                Owned("qa-dragonfly", dragonfly, 11, 7, 9, 10, false, -1),
                Owned("qa-beetle", beetle, 6, 5, 6, 4, false, -1),
                Owned("qa-hercules", hercules, 25, 13, 15, 14, false, 0), // 기절
            };
            owned[1].isPoisoned = true;

            var collection = holder.AddComponent<PlayerInsectCollection>();
            Set(collection, "database", database);
            Set(collection, "saveData", new PlayerInsectCollectionSave { insects = owned });
            var lookup = (Dictionary<string, PlayerInsectData>)Get(collection, "lookup");
            foreach (PlayerInsectData p in owned) lookup.Add(p.instanceId, p);

            var team = holder.AddComponent<BattleTeamManager>();
            team.AutoWire(collection);
            Set(team, "saveData", new BattleTeamSave
            {
                slotIds = new List<string> { "qa-rhino", "qa-stag", "qa-azure", "qa-mantis", string.Empty }
            });

            var progress = holder.AddComponent<PlayerProgressController>();
            Set(progress, "data", new PlayerProgressData { level = 12, currentXp = 140 });
            var candy = holder.AddComponent<PlayerCandyInventory>();
            Set(candy, "data", new PlayerCandyData { candies = 340 });
            var wallet = holder.AddComponent<PlayerCurrencyWallet>();
            Set(wallet, "data", new PlayerCurrencyData { coins = 1250, gems = 30 });

            var items = holder.AddComponent<PlayerItemInventory>();
            var itemSave = new PlayerItemSave();
            var itemLookup = (Dictionary<string, PlayerItemRecord>)Get(items, "lookup");
            foreach (var (id, count) in new[] { ("net_basic", 12), ("net_silver", 3), ("net_gold", 0) })
            {
                var rec = new PlayerItemRecord { itemId = id, count = count };
                itemSave.items.Add(rec);
                itemLookup.Add(id, rec);
            }
            Set(items, "save", itemSave);

            var dex = holder.AddComponent<DexController>();
            var dexSave = new DexSaveData();
            var dexLookup = (Dictionary<string, DexRecord>)Get(dex, "lookup");
            foreach (InsectData s in database.insects)
            {
                var rec = new DexRecord(s.insectId) { discoveredCount = 3, capturedCount = s == hercules ? 0 : 1 };
                dexSave.records.Add(rec);
                dexLookup.Add(s.insectId, rec);
            }
            Set(dex, "saveData", dexSave);

            var region = new RegionData
            {
                regionId = "meadow",
                displayName = "햇살 초원",
                themeColor = new Color(0.55f, 0.85f, 0.4f),
                insectIds = new[] { "beetle_basic", "mantis_green", "butterfly_azure", "dragonfly_lake" }
            };
            var regionManager = holder.AddComponent<RegionManager>();
            Set(regionManager, "currentRegion", region);

            // ── 무대 — 실제 게임 카메라 구도((0,9,-6) 고각) ──
            camera.backgroundColor = new Color(0.52f, 0.72f, 0.86f);
            camera.transform.position = new Vector3(0f, 9f, -6f);
            camera.transform.LookAt(new Vector3(0f, 0f, 1.5f));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 10f;
            ground.GetComponent<Renderer>().material.color = new Color(0.36f, 0.56f, 0.28f);
            var sun = new GameObject("FieldHudQASun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.GetComponent<Renderer>().material.color = new Color(0.3f, 0.45f, 0.85f);

            InsectEntity target = Entity(mantis, 9, new Vector3(1.8f, 0.3f, 2.2f), false);
            Entity(beetle, 5, new Vector3(-3.5f, 0.3f, 4.5f), false);
            Entity(dragonfly, 7, new Vector3(5f, 0.3f, 7f), false);
            InsectEntity raidTarget = Entity(azure, 22, new Vector3(-2.2f, 0.3f, 1.6f), false);
            raidTarget.gameObject.SetActive(false);

            var visual = new GameObject("FieldHudQAPreview").AddComponent<InsectModelPreviewRenderer>();
            InsectVisual.Renderer = visual;

            // ── 필드 HUD ──
            var hud = new GameObject("FieldHudQAStatus").AddComponent<PlayerStatusHUD>();
            hud.AutoWire(progress, candy, collection, items, dex, team, regionManager);
            hud.AutoWire(wallet);
            var minimap = new GameObject("FieldHudQAMinimap").AddComponent<MinimapUI>();
            minimap.AutoWire(regionManager);
            minimap.AutoWire(hud);
            var keyGuide = new GameObject("FieldHudQAKeyGuide").AddComponent<KeyGuideHUD>();
            keyGuide.AutoWire(regionManager);
            keyGuide.AutoWire(items);

            var quickTargets = new GameObject("FieldHudQAQuickTargets");
            quickTargets.SetActive(false);
            var quick = new GameObject("FieldHudQAQuickBar").AddComponent<QuickAccessBarUI>();
            Set(quick, "dexScreen", quickTargets.AddComponent<DexScreenUI>());
            Set(quick, "collectionUI", quickTargets.AddComponent<CollectionUI>());
            Set(quick, "inventoryScreen", quickTargets.AddComponent<InventoryUI>());
            Set(quick, "questUI", quickTargets.AddComponent<TutorialQuestUI>());
            Set(quick, "regionMapUI", quickTargets.AddComponent<RegionMapUI>());

            var teamUi = new GameObject("FieldHudQATeam").AddComponent<BattleTeamUI>();
            teamUi.AutoWire(team, collection);
            teamUi.AutoWire(quickTargets.AddComponent<HospitalUI>());
            Set(quick, "battleTeamUI", teamUi);

            var choice = new GameObject("FieldHudQAChoice").AddComponent<CaptureChoiceUI>();
            choice.AutoWire(null, null, null, team, collection, null, null, dex, null, items, null);
            choice.SetCaptureItems(CaptureItems());

            yield return Wait(1.6f);
            shots++; yield return Capture(output, "01-field-hud");

            Set(hud, "expanded", false);
            yield return Wait(0.6f);
            shots++; yield return Capture(output, "02-field-hud-collapsed");

            choice.ShowChoice(target);
            yield return Wait(0.8f);
            shots++; yield return Capture(output, "03-capture-choice");
            Set(choice, "showItemSelect", true);
            yield return Wait(0.5f);
            shots++; yield return Capture(output, "04-capture-items");
            Set(choice, "showItemSelect", false);
            Set(choice, "showTeamSelect", true);
            yield return Wait(0.8f);
            shots++; yield return Capture(output, "05-capture-team");
            choice.Hide();

            raidTarget.gameObject.SetActive(true);
            choice.ShowChoice(raidTarget);
            yield return Wait(0.8f);
            shots++; yield return Capture(output, "06-capture-raid");
            choice.Hide();
            yield return Wait(0.3f);

            // 포획 성공 팝업 — 필드 흐름의 마지막 화면. 결과 이벤트 핸들러를 직접 불러 같은 상태를 만든다.
            var popup = new GameObject("FieldHudQAPopup").AddComponent<CapturePopupUI>();
            popup.AutoWire(collection);
            typeof(CapturePopupUI).GetMethod("OnCaptureResolved", Private).Invoke(popup, new object[] { target, true });
            Set(popup, "capturedNewSpecies", true);   // 그 종의 첫 포획 — 초상 모서리의 NEW
            yield return Wait(0.9f);
            shots++; yield return Capture(output, "09-capture-success");
            typeof(CapturePopupUI).GetMethod("OnCaptureResolved", Private).Invoke(popup, new object[] { target, false });
            yield return Wait(0.9f);
            shots++; yield return Capture(output, "10-capture-fail");
            Object.Destroy(popup.gameObject);
            yield return Wait(0.3f);

            // ── 포획 미니게임 3종 — 판을 원하는 순간에 세워 놓고 찍는다(조작은 가짜 입력으로 미리 돌린다) ──
            var minigame = new GameObject("FieldHudQAMinigame").AddComponent<InsectGame.Capture.CaptureMinigameController>();
            var board = new Vector2(InsectGame.Capture.CaptureMinigame.BoardWidth, InsectGame.Capture.CaptureMinigame.BoardHeight);
            minigame.StartForCapture(target, InsectGame.Capture.CaptureMinigameKind.Sneak, 1.1f, true, Vector2.zero);
            yield return Wait(0.6f);
            shots++; yield return Capture(output, "19-minigame-sneak");
            minigame.EndCapture();
            minigame.StartForCapture(target, InsectGame.Capture.CaptureMinigameKind.Sneak, 9f, false, Vector2.zero, true);
            yield return Wait(0.6f);
            shots++; yield return Capture(output, "20-minigame-sneak-look");
            minigame.EndCapture();
            minigame.StartForCapture(target, InsectGame.Capture.CaptureMinigameKind.Track, 1.4f, true, board * 0.5f);
            yield return Wait(0.6f);
            shots++; yield return Capture(output, "21-minigame-track");
            minigame.EndCapture();
            minigame.StartForCapture(target, InsectGame.Capture.CaptureMinigameKind.Toss, 0.3f, true, new Vector2(board.x * 0.62f, board.y * 0.36f));
            yield return Wait(0.6f);
            shots++; yield return Capture(output, "22-minigame-toss");
            minigame.EndCapture();
            Object.Destroy(minigame.gameObject);
            yield return Wait(0.3f);

            // ── 필드 소식 — 라온과의 내기 점수판 + 소식 카드(레벨업·보상·색다른 조우) ──
            // 내기 컨트롤러는 꺼 둔 채 상태만 넣는다(Update가 돌면 PlayerPrefs를 읽고 시계가 흐른다).
            var race = new GameObject("FieldHudQARace").AddComponent<InsectGame.Story.RivalRaceController>();
            race.enabled = false;
            Set(race, "active", true);
            Set(race, "<PlayerCount>k__BackingField", 2);
            Set(race, "<RivalCount>k__BackingField", 1);
            var moments = new GameObject("FieldHudQAMoments").AddComponent<FieldMomentsUI>();
            moments.AutoWire(null, null, race, null);
            moments.ShowForCapture(new FieldMoment(FieldMomentKind.LevelUp, "레벨 업!  Lv.13",
                "이제 Lv.18 곤충까지 레벨 차 페널티 없이 잡을 수 있습니다"), 0.45f);
            yield return Wait(0.2f);
            shots++; yield return Capture(output, "23-moment-levelup-race");
            moments.ShowForCapture(new FieldMoment(FieldMomentKind.Reward, "보상을 받았습니다",
                "캔디 +5 · 은빛 채집망 ×2 · 호수 잠자리 Lv.6"), 1.2f);
            yield return Wait(0.2f);
            shots++; yield return Capture(output, "24-moment-reward");
            moments.ShowForCapture(new FieldMoment(FieldMomentKind.Rival, "라온과의 내기에서 이겼습니다!",
                "라온: \"…졌다. 다음엔 안 봐줘!\"   캔디 +15 · 은빛 채집망 ×2"), 1.2f);
            yield return Wait(0.2f);
            shots++; yield return Capture(output, "25-moment-rival");
            Object.Destroy(moments.gameObject);
            Object.Destroy(race.gameObject);
            yield return Wait(0.3f);

            teamUi.Toggle();
            yield return Wait(1.2f);
            shots++; yield return Capture(output, "07-team");
            Set(teamUi, "selectingSlot", 4);
            Set(teamUi, "pickerSortDirty", true);
            yield return Wait(1.2f);
            shots++; yield return Capture(output, "08-team-picker");
            teamUi.CloseModal();

            // ── 훈련소 — 방식·범용기·종족 learnset을 실제 부트스트랩 코드로 만든다(가격이 곧 검수 대상이다) ──
            // 부트스트랩은 비활성 호스트에 붙여 Awake(월드 생성)를 막고 private 생성 함수만 부른다.
            var bootstrap = holder.AddComponent<PlaySceneBootstrap>();
            rhino.primaryType = InsectElement.Metal;
            rhino.secondaryType = InsectElement.Bug;
            azure.primaryType = InsectElement.Wind;
            azure.secondaryType = InsectElement.Light;
            var allSkills = new List<InsectSkill>(
                (InsectSkill[])typeof(PlaySceneBootstrap).GetMethod("CreateTrainingSkills", Private).Invoke(bootstrap, null));
            MethodInfo buildLearnset = typeof(PlaySceneBootstrap).GetMethod("BuildLevelLearnset", Private);
            foreach (InsectData s in database.insects)
            {
                s.learnset = (InsectLearnableSkill[])buildLearnset.Invoke(bootstrap, new object[] { s });
                foreach (InsectLearnableSkill l in s.learnset)
                    if (l != null && l.skill != null && !allSkills.Contains(l.skill)) allSkills.Add(l.skill);
            }
            var training = holder.AddComponent<TrainingManager>();
            training.AutoWire(collection, candy);
            training.Initialize(
                (TrainingMethod[])typeof(PlaySceneBootstrap).GetMethod("CreateTrainingMethods", Private).Invoke(bootstrap, null),
                allSkills.ToArray());

            // 장수풍뎅이: 종족기 셋을 배웠고 폭발·단단해지기를 훈련 중. 푸른나비: 6칸이 가득 찬 교체 상황.
            PlayerInsectData rhinoPid = owned[0];
            foreach (InsectLearnableSkill l in rhino.learnset)
            {
                if (l.learnLevel > 9) continue;
                rhinoPid.learnedSkillIds.Add(l.skillId);
                rhinoPid.equippedSkillIds.Add(l.skillId);
            }
            rhinoPid.trainingProgress.Add("bug_burst:1");
            rhinoPid.trainingProgress.Add("tr_harden:1");
            PlayerInsectData azurePid = owned[3];
            foreach (InsectLearnableSkill l in azure.learnset)
                azurePid.learnedSkillIds.Add(l.skillId);
            for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
                azurePid.equippedSkillIds.Add(azure.learnset[i].skillId);

            var trainingUi = new GameObject("FieldHudQATraining").AddComponent<TrainingUI>();
            trainingUi.AutoWire(training, collection, candy);
            trainingUi.OpenForCapture(null, "select");
            yield return Wait(1.2f);
            shots++; yield return Capture(output, "11-training-select");
            trainingUi.OpenForCapture("qa-rhino", "method");
            yield return Wait(1.0f);
            shots++; yield return Capture(output, "12-training-method");
            trainingUi.OpenForCapture("qa-rhino", "growth");
            yield return Wait(1.0f);
            shots++; yield return Capture(output, "13-training-growth");
            trainingUi.ShowFeedbackForCapture("등급 상승!  A → S", true);
            yield return Wait(0.4f);
            shots++; yield return Capture(output, "14-training-growth-toast");
            Set(trainingUi, "feedbackTimer", 0f);
            trainingUi.OpenForCapture("qa-rhino", "learn", 0);            // 종족 기술 연구
            yield return Wait(1.0f);
            shots++; yield return Capture(output, "15-training-learn-species");
            trainingUi.OpenForCapture("qa-rhino", "learn", 4);            // 특수 훈련 — 상태기 가격
            yield return Wait(1.0f);
            shots++; yield return Capture(output, "16-training-learn-special");
            trainingUi.OpenForCapture("qa-rhino", "equip");
            yield return Wait(1.0f);
            shots++; yield return Capture(output, "17-training-equip");
            trainingUi.OpenForCapture("qa-azure", "replace", 4, "tr_nature_force");
            yield return Wait(1.0f);
            shots++; yield return Capture(output, "18-training-replace");
            trainingUi.CloseModal();

            File.WriteAllText(Path.Combine(output, "README.txt"),
                $"Actual standalone IMGUI. {shots} shots at {Screen.width}x{Screen.height} (mobile layout={UIScale.IsMobileLayout}).\n" +
                "In-memory player state on an inactive host (no Awake/Load, no save calls). Field backdrop is a flat staging plane.\n" +
                "Team: 4/5 filled, slot 2 poisoned+hurt; one owned insect fainted. Capture choice/item/team states injected via private fields.\n" +
                "Training: methods/general skills/species learnsets built by the real PlaySceneBootstrap generators (prices are live). Candy 340.\n");
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

        private static PlayerInsectData Owned(string instance, InsectData species, int level,
            int hp, int atk, int def, bool shiny, int currentHp)
        {
            var p = new PlayerInsectData
            {
                instanceId = instance,
                insectId = species.insectId,
                level = level,
                ivHp = hp,
                ivAtk = atk,
                ivDef = def,
                isShiny = shiny,
                sizeRoll = 50
            };
            p.currentHp = currentHp < 0 ? p.GetTotalHp(species.baseHp) : currentHp;
            return p;
        }

        private static InsectEntity Entity(InsectData data, int level, Vector3 at, bool shiny)
        {
            var go = new GameObject("QAInsect_" + data.insectId);
            go.transform.position = at;
            var e = go.AddComponent<InsectEntity>();
            e.BuildForBattle(data, level, shiny);
            go.transform.localScale = Vector3.one * 1.4f;
            return e;
        }

        private static CaptureItemData[] CaptureItems()
        {
            return new[]
            {
                new CaptureItemData { itemId = "net_basic", displayName = "기본 채집망", description = "흔한 채집망 - 보통 난이도",
                    themeColor = new Color(0.6f, 0.6f, 0.6f), speedMultiplier = 1f, zoneSizeMultiplier = 1f, timeLimitMultiplier = 1f },
                new CaptureItemData { itemId = "net_silver", displayName = "은빛 채집망", description = "좋은 품질 - 미니게임이 쉬워짐",
                    themeColor = new Color(0.75f, 0.82f, 0.95f), speedMultiplier = 0.75f, zoneSizeMultiplier = 1.3f, timeLimitMultiplier = 1.3f, captureBonus = 0.1f },
                new CaptureItemData { itemId = "net_gold", displayName = "황금 채집망", description = "최고급 - 매우 쉬운 미니게임 + 포획률 보너스",
                    themeColor = new Color(1f, 0.85f, 0.2f), speedMultiplier = 0.55f, zoneSizeMultiplier = 1.6f, timeLimitMultiplier = 1.6f, captureBonus = 0.2f },
            };
        }

        private static object Get(object target, string name)
        {
            FieldInfo f = target.GetType().GetField(name, Private);
            if (f == null) throw new System.MissingFieldException(target.GetType().Name, name);
            return f.GetValue(target);
        }

        private static void Set(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name, Private);
            if (f == null) throw new System.MissingFieldException(target.GetType().Name, name);
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
