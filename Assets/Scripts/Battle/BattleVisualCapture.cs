#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>Standalone-only visual QA fixture. No bootstrap, authentication or save services.</summary>
    public sealed class BattleVisualCapture : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        /// <summary>프레임 사이 간격(실제 초). 히트스톱(0.06~0.2초)을 보려면 0.1보다 촘촘해야 한다.</summary>
        internal static float CaptureInterval = 0.1f;
        private float watchdogStart;
        private float watchdogSeconds = 120f;
        private void Awake()
        {
            watchdogStart = Time.realtimeSinceStartup;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-insectUiInteractive") >= 0)
                watchdogSeconds = 210f;
        }
        private void Update()
        {
            if (Time.realtimeSinceStartup - watchdogStart > watchdogSeconds) Application.Quit(4);
        }
        private IEnumerator Start()
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            { Application.Quit(2); yield break; }
            string[] args = Environment.GetCommandLineArgs();
            int arg = Array.IndexOf(args, "-battleCaptureOut");
            if (arg < 0 || arg + 1 >= args.Length) { Application.Quit(2); yield break; }
            string output = Path.GetFullPath(args[arg + 1]);
            if (Directory.Exists(output) && Directory.GetFileSystemEntries(output).Length > 0)
            { Debug.LogError("Capture requires an empty output directory."); Application.Quit(2); yield break; }
            Directory.CreateDirectory(output);
            int scenarioArg = Array.IndexOf(args, "-battleScenario");
            string scenario = scenarioArg >= 0 && scenarioArg + 1 < args.Length ? args[scenarioArg + 1] : "duel";
            // 연출 비교용 — 같은 장면을 카메라 스타일만 바꿔 찍는다(off|punch|cinematic).
            int styleArg = Array.IndexOf(args, "-battleCamStyle");
            if (styleArg >= 0 && styleArg + 1 < args.Length
                && Enum.TryParse(args[styleArg + 1], true, out BattleCameraDirector.Style camStyle))
                BattleCameraDirector.Current = camStyle;
            int intervalArg = Array.IndexOf(args, "-captureInterval");
            float interval = 0.1f;
            if (intervalArg >= 0 && intervalArg + 1 < args.Length)
                float.TryParse(args[intervalArg + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out interval);
            CaptureInterval = Mathf.Clamp(interval, 0.02f, 1f);
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            UnityEngine.Random.InitState(8173);
            // Override in memory only; setters persist user preferences.
            typeof(BattlePresentation).GetField("speed", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 1f);
            typeof(BattlePresentation).GetField("reducedMotion", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
            typeof(BattlePresentation).GetField("reducedFlashes", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.40f, 0.44f);
            var cameraObject = new GameObject("QACamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.backgroundColor = new Color(0.065f, 0.105f, 0.11f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.fieldOfView = 42f;
            camera.farClipPlane = 200f;
            CameraFollower follower = cameraObject.AddComponent<CameraFollower>();
            follower.SetTarget(new GameObject("QACameraTarget").transform);
            if (scenario == "insect-ui")
            {
                follower.enabled = false;
                yield return InsectDetailVisualCapture.Run(output, camera);
                yield break;
            }
            if (scenario == "map")
            {
                follower.enabled = false;
                yield return WorldMapVisualCapture.Run(output, camera);
                yield break;
            }
            if (scenario == "story")
            {
                follower.enabled = false;
                yield return InsectGame.Story.StoryDialogueCapture.Run(output, camera);
                yield break;
            }
            if (scenario == "materials")
            {
                // 월드 반투명·발광이 빌드에서 살아 있는가 — 수치 판정(SceneryMaterialVisualCapture 주석).
                follower.enabled = false;
                yield return SceneryMaterialVisualCapture.Run(output, camera);
                yield break;
            }
            if (scenario == "badge")
            {
                follower.enabled = false;
                yield return BadgeVisualCapture.Run(output, camera);
                yield break;
            }
            if (scenario == "outfit")
            {
                // 의상 창·캐릭터 생성 화면 — IMGUI라 배치 캡처로는 창이 안 보인다(OutfitVisualCapture 주석).
                follower.enabled = false;
                yield return InsectGame.UI.OutfitVisualCapture.Run(output, camera);
                yield break;
            }
            if (scenario == "field-ui")
            {
                // 필드 HUD·포획 선택·배틀팀 — IMGUI라 배치 캡처로는 안 보인다(FieldHudVisualCapture 주석).
                follower.enabled = false;
                yield return InsectGame.UI.FieldHudVisualCapture.Run(output, camera);
                yield break;
            }
            if (scenario == "dream-island")
            {
                // 「챔피언의 꿈」 — 꾸며진 섬·도입 카드·안내·깨어남 자막. 전투는 dream-battle이 찍는다.
                yield return InsectGame.UI.DreamVisualCapture.Run(output, camera);
                yield break;
            }
            if (scenario == "island-ui")
            {
                // 나의 섬 — HUD·꾸미기·상점·곤충·방문·가이드. 꾸미기 화면이 카메라를 직접 옮기므로 팔로워를 끄지 않는다.
                yield return InsectGame.UI.IslandVisualCapture.Run(output, camera);
                yield break;
            }
            BattleArenaController arena = new GameObject("QAArena").AddComponent<BattleArenaController>();
            if (scenario == "raid" || scenario == "raid-unite")
            {
                yield return RaidVisualCapture.Run(output, follower, arena, scenario == "raid-unite");
                yield break;
            }
            InsectBattleController controller = new GameObject("QAController").AddComponent<InsectBattleController>();
            BattleScreenUI ui = new GameObject("QABattleUI").AddComponent<BattleScreenUI>();
            controller.AutoWire(arena);
            ui.AutoWire(controller, follower, null);
            ui.AutoWire(arena);
            bool dreamBattle = scenario == "dream-battle";
            InsectData player = dreamBattle
                ? Fixture(InsectGame.Core.DreamPrologueData.AceInsectId, "헤라클레스 천공각")
                : Fixture("rhinoceros_beetle", "장수풍뎅이");
            InsectData enemy = dreamBattle
                ? Fixture(InsectGame.Core.DreamPrologueData.ChallengerInsectId, "태고의 비천룡")
                : Fixture("mantis", "사마귀");
            // 속성 임팩트 검수 — 같은 스킬의 속성만 매 턴 바꿔 10종을 차례로 쓴다. 한쪽이 먼저 쓰러지지
            // 않게 양쪽 HP를 크게 잡고 재사용 대기를 없앤다(연출만 보는 픽스처, 밸런스와 무관).
            InsectElement[] elementCycle = (InsectElement[])Enum.GetValues(typeof(InsectElement));
            if (scenario == "elements")
            {
                player.baseHp = enemy.baseHp = 4000;
                player.skills[0].cooldownTurns = 0;
            }
            InsectBattleStats playerStats = null, enemyStats = null;
            PlayerInsectData first = scenario == "swap" ? ConfigureSwap(ui, player) : null;
            controller.SetRandomSeed(8173);
            if (scenario == "escape") controller.SetRandomSource(new EscapeSequence());
            controller.BattleUpdated += (p, e) => { playerStats = p; enemyStats = e; };
            if (dreamBattle)
            {
                // 진짜 지휘자가 샌드박스 전투를 연다 — 안내 문구·결과 화면·도망 버튼 숨김을 실제 코드로 본다.
                var dreamDb = ScriptableObject.CreateInstance<InsectDatabase>();
                dreamDb.insects.Add(player);
                dreamDb.insects.Add(enemy);
                var dreamDirector = new GameObject("QADreamDirector").AddComponent<InsectGame.Story.DreamPrologueDirector>();
                dreamDirector.AutoWire(null, null, null, null, null, controller, ui, dreamDb, null);
                if (!dreamDirector.StartBattleForCapture()) { Debug.LogError("[QA] dream battle did not start"); Application.Quit(3); yield break; }
            }
            else controller.StartDuel(player, 15, enemy, 15, null, null, first);
            // 연출 검수 — 간부전(컷인·장부·전투 중 한마디·결과 한마디)과 수문장전(곤충끼리 서는 컷인).
            // 수문장 표식은 StartBattle(월드 개체)에서만 서므로 리플렉션으로 세운다 — 적 픽스처가 초원 수호자와 같은 사마귀다.
            if (scenario == "boss") { controller.SetDuelOpponent("ledger_grip"); controller.ArmLedger(6); }
            if (scenario == "rival") controller.SetDuelOpponent("rival_final");
            if (scenario == "guardian")
                typeof(InsectBattleController).GetProperty("EnemyGuardianRegionId").SetValue(controller, "meadow");
            FieldInfo phaseField = typeof(BattleScreenUI).GetField("phase", PrivateInstance);
            FieldInfo shownPlayer = typeof(BattleScreenUI).GetField("displayPlayerHp", PrivateInstance);
            FieldInfo shownEnemy = typeof(BattleScreenUI).GetField("displayEnemyHp", PrivateInstance);
            FieldInfo impact = typeof(BattleScreenUI).GetField("impactRevealed", PrivateInstance);
            FieldInfo phaseTimer = typeof(BattleScreenUI).GetField("phaseTimer", PrivateInstance);
            MethodInfo attack = typeof(BattleScreenUI).GetMethod("TryBasicAttack", PrivateInstance);
            MethodInfo skill = typeof(BattleScreenUI).GetMethod("TryUseSkill", PrivateInstance);
            MethodInfo escape = typeof(BattleScreenUI).GetMethod("TryEscape", PrivateInstance);
            MethodInfo swap = typeof(BattleScreenUI).GetMethod("TrySwapToSlot", PrivateInstance);
            float started = Time.realtimeSinceStartup;
            float nextFrame = 0f, turnAt = -1f, resultAt = -1f;
            int frame = 0, turns = 0;
            bool swapped = false, speedChanged = false;
            string previous = "";
            using (var elementLog = new StreamWriter(Path.Combine(output, "elements.csv")))
            using (var timeline = new StreamWriter(Path.Combine(output, "timeline.csv")))
            {
                elementLog.WriteLine("frame,elapsed,element");
                timeline.WriteLine("frame,elapsed,phase,actualPlayerHp,actualEnemyHp,displayPlayerHp,displayEnemyHp,impactRevealed,phaseTimer,width,height,speed,playerX,enemyX");
                while (Time.realtimeSinceStartup - started < 90f)
                {
                    yield return new WaitForEndOfFrame();
                    float elapsed = Time.realtimeSinceStartup - started;
                    string phase = phaseField.GetValue(ui).ToString();
                    bool changed = phase != previous;
                    if (changed && (phase == "PlayerTurn" || phase == "SwapSelect")) turnAt = elapsed;
                    if (scenario == "speed" && !speedChanged && phase == "PlayerAttack" && (float)phaseTimer.GetValue(ui) > 0.5f)
                    {
                        typeof(BattlePresentation).GetField("speed", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 2f);
                        speedChanged = true;
                    }
                    if (phase == "Result" && resultAt < 0f) resultAt = elapsed;
                    if (changed || elapsed >= nextFrame)
                    {
                        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(Path.Combine(output, $"frame-{frame:D5}.jpg"), shot.EncodeToJPG(85));
                        if (changed) File.WriteAllBytes(Path.Combine(output, $"state-{frame:D5}-{phase}.png"), shot.EncodeToPNG());
                        Destroy(shot);
                        float px = arena.PlayerModel != null ? camera.WorldToViewportPoint(arena.PlayerModel.transform.position).x : -1f;
                        float ex = arena.EnemyModel != null ? camera.WorldToViewportPoint(arena.EnemyModel.transform.position).x : -1f;
                        timeline.WriteLine(FormattableString.Invariant($"{frame},{elapsed:F3},{phase},{playerStats.CurrentHp},{enemyStats.CurrentHp},{shownPlayer.GetValue(ui)},{shownEnemy.GetValue(ui)},{impact.GetValue(ui)},{phaseTimer.GetValue(ui)},{Screen.width},{Screen.height},{BattlePresentation.Speed},{px},{ex}"));
                        timeline.Flush();
                        frame++;
                        nextFrame = elapsed + CaptureInterval;
                    }
                    previous = phase;
                    if (phase == "PlayerTurn" && elapsed - turnAt >= 0.8f)
                    {
                        if (scenario == "escape") escape.Invoke(ui, null);
                        else if (scenario == "elements")
                        {
                            if (turns >= elementCycle.Length) break;
                            InsectElement element = elementCycle[turns++];
                            player.skills[0].element = element;
                            elementLog.WriteLine(FormattableString.Invariant($"{frame},{elapsed:F3},{element}"));
                            elementLog.Flush();
                            skill.Invoke(ui, new object[] { 0 });
                        }
                        else if (turns++ == 0) skill.Invoke(ui, new object[] { 0 });
                        else attack.Invoke(ui, null);
                        turnAt = elapsed;
                    }
                    if (scenario == "swap" && phase == "SwapSelect" && !swapped && elapsed - turnAt >= 0.8f)
                    { swap.Invoke(ui, new object[] { 1 }); swapped = true; }
                    if (resultAt >= 0f && elapsed - resultAt >= 1.8f) break;
                }
            }
            File.WriteAllText(Path.Combine(output, "manifest.txt"),
                $"Actual standalone ScreenCapture including IMGUI\nScenario={scenario}\nCameraStyle={BattleCameraDirector.Current}\nSeed=8173\nSize={Screen.width}x{Screen.height}\nFrames={frame}\nResultReached={resultAt >= 0f}\nSwapped={swapped}\nSpeedChanged={speedChanged}\nFixture=neutral level15 duel; not a balance certification\n");
            bool completed = scenario == "elements" ? turns >= elementCycle.Length : resultAt >= 0f;
            Application.Quit(completed ? 0 : 3);
        }

        private static PlayerInsectData ConfigureSwap(BattleScreenUI ui, InsectData starter)
        {
            // Inactive components never run Awake/Load. Only UI lookup is wired;
            // collection.saveData stays null and controller has no persistence services.
            var holder = new GameObject("QAInactiveTeam");
            holder.SetActive(false);
            var collection = holder.AddComponent<PlayerInsectCollection>();
            var team = holder.AddComponent<BattleTeamManager>();
            var bench = Fixture("stag_beetle", "사슴벌레");
            var database = ScriptableObject.CreateInstance<InsectDatabase>();
            database.insects.Add(starter); database.insects.Add(bench);
            typeof(PlayerInsectCollection).GetField("database", PrivateInstance).SetValue(collection, database);
            var lookup = (Dictionary<string, PlayerInsectData>)typeof(PlayerInsectCollection).GetField("lookup", PrivateInstance).GetValue(collection);
            var first = new PlayerInsectData { instanceId = "qa-first", insectId = starter.insectId, level = 15, currentHp = 1 };
            var second = new PlayerInsectData { instanceId = "qa-bench", insectId = bench.insectId, level = 15, currentHp = -1 };
            foreach (var pid in new[] { first, second })
            { pid.learnedSkillIds.Add("qa_strike"); pid.equippedSkillIds.Add("qa_strike"); lookup[pid.instanceId] = pid; }
            typeof(BattleTeamManager).GetField("saveData", PrivateInstance).SetValue(team,
                new BattleTeamSave { slotIds = new List<string> { first.instanceId, second.instanceId, "", "", "" } });
            ui.AutoWire(team, collection, null);
            return first;
        }

        private sealed class EscapeSequence : IRaidRandomSource
        {
            private int rolls;
            public float Next01() => rolls++ == 0 ? 0.99f : 0f;
            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        }

        internal static InsectData Fixture(string id, string label)
        {
            InsectSkill skill = ScriptableObject.CreateInstance<InsectSkill>();
            skill.skillId = "qa_strike";
            skill.displayName = "전력 돌진";
            skill.description = "힘을 모아 상대에게 돌진합니다.";
            skill.element = InsectElement.Bug;
            skill.power = 20;
            skill.cooldownTurns = 2;
            InsectData data = ScriptableObject.CreateInstance<InsectData>();
            data.insectId = id;
            data.displayName = label;
            data.baseHp = 140;
            data.baseAtk = 30;
            data.baseDef = 25;
            InsectSkill guard = ScriptableObject.CreateInstance<InsectSkill>();
            guard.skillId = "qa_guard";
            guard.displayName = "단단한 갑각";
            guard.effectType = SkillEffectType.DefenseBuff;
            guard.element = InsectElement.Bug;
            InsectSkill heal = ScriptableObject.CreateInstance<InsectSkill>();
            heal.skillId = "qa_heal";
            heal.displayName = "자연의 회복";
            heal.effectType = SkillEffectType.Heal;
            heal.element = InsectElement.Bug;
            InsectSkill strike = ScriptableObject.CreateInstance<InsectSkill>();
            strike.skillId = "qa_strike_light";
            strike.displayName = "날카로운 일격";
            strike.element = InsectElement.Bug;
            strike.power = 12;
            data.skills = id == "rhinoceros_beetle" ? new[] { skill, guard, heal, strike } : new[] { skill };
            return data;
        }
    }
}
#endif
