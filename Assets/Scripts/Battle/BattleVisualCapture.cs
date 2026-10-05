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
                // 앞 컷: 「지난 이야기」 카드·HUD 목표 두 줄(할 일 + 왜) — 만든 것을 치우고 돌아온다. 뒤 컷: 대사 무대(끝에 종료).
                yield return InsectGame.UI.StoryRecapVisualCapture.Run(output, camera);
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
            if (scenario == "story-video") { yield return InsectGame.UI.StoryVideoVisualCapture.Run(output, camera); yield break; }
            if (scenario == "island-ui")
            {
                // 나의 섬 — HUD·꾸미기·상점·곤충·방문·가이드. 꾸미기 화면이 카메라를 직접 옮기므로 팔로워를 끄지 않는다.
                yield return InsectGame.UI.IslandVisualCapture.Run(output, camera);
                yield break;
            }
            BattleArenaController arena = new GameObject("QAArena").AddComponent<BattleArenaController>();
            if (scenario == "raid" || scenario == "raid-unite" || scenario == "raid-forms" || scenario == "guardian-intro")
            {
                yield return RaidVisualCapture.Run(output, follower, arena, scenario);
                yield break;
            }
            InsectBattleController controller = new GameObject("QAController").AddComponent<InsectBattleController>();
            BattleScreenUI ui = new GameObject("QABattleUI").AddComponent<BattleScreenUI>();
            controller.AutoWire(arena);
            arena.AutoWire(controller);   // 몸 상태 표시의 원천(Life partial) — 안 주면 아레나가 한 번 찾는다
            ui.AutoWire(controller, follower, null);
            ui.AutoWire(arena);
            bool dreamBattle = scenario == "dream-battle";
            InsectData player = dreamBattle
                ? Fixture(InsectGame.Core.DreamPrologueData.AceInsectId, "헤라클레스 천공각")
                : Fixture("rhinoceros_beetle", "장수풍뎅이");
            InsectData enemy = dreamBattle
                ? Fixture(InsectGame.Core.DreamPrologueData.ChallengerInsectId, "태고의 비천룡")
                : Fixture("mantis", "사마귀");
            // 상성 표시 검수(4단계) — duel·victory는 내 「전력 돌진」(벌레)이 상대(풀·어둠)에게 「아주 잘 통했다!」, 상대의 벌레 기술은
            // 내 곤충(강철)에게 「별로 안 통했다…」로 뜨고, 스킬 카드의 벌레 기술 둘에 「아주 잘 통해요」 칩이 선다. 다른 장면은 그대로(무속성).
            if (scenario == "duel" || scenario == "victory")
            {
                enemy.primaryType = InsectElement.Leaf;
                enemy.secondaryType = InsectElement.Dark;
                player.primaryType = InsectElement.Metal;
            }
            // 속성 임팩트 검수 — 같은 스킬의 속성만 매 턴 바꿔 10종을 차례로 쓴다. 한쪽이 먼저 쓰러지지
            // 않게 양쪽 HP를 크게 잡고 재사용 대기를 없앤다(연출만 보는 픽스처, 밸런스와 무관).
            InsectElement[] elementCycle = (InsectElement[])Enum.GetValues(typeof(InsectElement));
            if (scenario == "elements")
            {
                player.baseHp = enemy.baseHp = 4000;
                player.skills[0].cooldownTurns = 0;
            }
            // 4단계 전투 체감 검수 — 몸 상태 표시(status-fx)·전용기(signature)·승리(victory)·계열 몸짓(species-motions).
            bool statusFx = scenario == "status-fx";
            bool signatureScene = scenario == "signature";
            bool victoryScene = scenario == "victory";
            bool speciesMotions = scenario == "species-motions";
            if (statusFx || signatureScene || speciesMotions) player.baseHp = enemy.baseHp = 4000;
            if (signatureScene) MakeSignature(player, player.skills[0], "장수 대돌격");
            // 계열 몸짓 — 내 곤충 모델을 차례로 갈아 세우고 기본 공격(무속성 = 근접)을 한 번씩 쓴다. 상대(사마귀)의 반격이 베기를 보여 준다.
            string[] motionIds = { "mantis_green", "rhinoceros_beetle", "butterfly_swallowtail", "bee_worker", "spider_garden" };
            string[] motionNames = { "사마귀", "장수풍뎅이", "호랑나비", "일벌", "왕거미" };
            bool statusInjected = false;
            int statusInjectedFrame = -1;
            InsectBattleStats playerStats = null, enemyStats = null;
            PlayerInsectData first = scenario == "swap" ? ConfigureSwap(ui, player) : null;
            controller.SetRandomSeed(8173);
            if (scenario == "escape") controller.SetRandomSource(new EscapeSequence());
            controller.BattleUpdated += (p, e) => { playerStats = p; enemyStats = e; };
            bool teamDuel = scenario == "team-duel";
            int enemySwitches = 0;
            if (teamDuel)
            {
                // 상대 교체 등장 검수 — 집게(ledger_grip)의 진짜 팀 순서·레벨로 연다. 교체를 빨리 보려고 앞 둘은 HP 1로 세우고
                // (첫째는 onStarted에서 — 화면이 첫 HP를 읽기 전), 에이스는 들어올 때 HP 1/4로 깎는다. 밸런스와 무관한 연출 픽스처다.
                if (!TryStartTeamDuelFixture(controller, player, () => enemySwitches++))
                { Debug.LogError("[QA] team duel did not start"); Application.Quit(3); yield break; }
            }
            else if (dreamBattle)
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
            // 야생 전투로 보이게 — 진입 문구 「야생 사마귀가 나타났다!」와 승리 보상 줄(캔디·경험치·아이템·포획)을 보려고. 이 픽스처는 월드 개체 없이
            // 대결로 여므로 대결 표지만 끈다(야생 피해 배율 0.7이 걸린다 — 연출 픽스처라 밸런스와 무관). 보상은 컨트롤러가 야생 승리 때 채우는
            // 값을 미리 넣는다 — 월드 개체가 없으면 승리 경로가 이 값을 건드리지 않는다. 저장은 부르지 않는다(지갑·캔디·컬렉션 없음).
            bool wildLook = scenario == "duel" || victoryScene;
            // duel은 포획 실패 줄(「사마귀는 달아났다」), victory는 포획 성공 줄(「사마귀를 잡았다!」).
            if (wildLook) PresentAsWild(controller, victoryScene);
            // 승리 검수 — 상대를 HP 1로 세워 첫 기술이 마무리가 되게(쓰러짐 → 결과 → 승리 포즈·카메라 반 바퀴).
            if (victoryScene && enemyStats != null) typeof(InsectBattleStats).GetProperty("CurrentHp").SetValue(enemyStats, 1);
            // 연출 검수 — 간부전(컷인·장부·전투 중 한마디·결과 한마디)과 수문장전(곤충끼리 서는 컷인).
            // 수문장 표식은 StartBattle(월드 개체)에서만 서므로 리플렉션으로 세운다 — 적 픽스처가 초원 수호자와 같은 사마귀다.
            if (scenario == "boss") { controller.SetDuelOpponent("ledger_grip"); controller.ArmLedger(6); }
            if (scenario == "rival") controller.SetDuelOpponent("rival_final");
            if (scenario == "guardian")
                typeof(InsectBattleController).GetProperty("EnemyGuardianRegionId").SetValue(controller, "meadow");
            // 낮·밤·날씨 보정 칩 — 보정은 야생 전투(StartBattle, 월드 개체)에서만 걸리고 이 픽스처는 대결로 열므로 노트를 리플렉션으로 세운다.
            // 유리(민트)·불리(코랄)를 한 장면에 둘 다 띄우고, 가장 긴 문구(BattleEnvironment 테스트의 20자 상한)를 내 쪽에 둔다.
            if (scenario == "weather")
            {
                typeof(InsectBattleController).GetProperty("PlayerEnvironment").SetValue(controller,
                    new BattleEnvironmentNote { Multiplier = 1.1f, Reason = "아침 · 주행성 · 맑은 날을 좋아함" });
                typeof(InsectBattleController).GetProperty("EnemyEnvironment").SetValue(controller,
                    new BattleEnvironmentNote { Multiplier = 0.9f, Reason = "비 · 비를 싫어함" });
            }
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
                timeline.WriteLine("frame,elapsed,phase,actualPlayerHp,actualEnemyHp,displayPlayerHp,displayEnemyHp,impactRevealed,phaseTimer,width,height,speed,playerX,enemyX,enemyIndex,enemySwitchProgress,battleKind,entryProgress,signatureCutIn,resultShown,resultCanClose");
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
                        timeline.WriteLine(FormattableString.Invariant($"{frame},{elapsed:F3},{phase},{playerStats.CurrentHp},{enemyStats.CurrentHp},{shownPlayer.GetValue(ui)},{shownEnemy.GetValue(ui)},{impact.GetValue(ui)},{phaseTimer.GetValue(ui)},{Screen.width},{Screen.height},{BattlePresentation.Speed},{px},{ex},{controller.EnemyTeamIndex},{ui.EnemySwitchProgress:F3},{ui.CurrentBattleKind},{ui.EntryIntroProgress:F3},{arena.IsSignatureCutIn},{ui.ResultShownSeconds:F2},{ui.ResultCanClose}"));
                        timeline.Flush();
                        frame++;
                        nextFrame = elapsed + CaptureInterval;
                    }
                    previous = phase;
                    // 몸 상태 검수 — 두 번째 행동(연속 2!)이 해결된 직후에 상태를 한꺼번에 건다: 내 곤충 기절 2턴·공격 강화,
                    // 상대 독·방어 강화. 다음 차례는 기절로 건너뛰고(「행동 불가!」) 그 라운드 끝에 독이 「-8 독」으로 뜬다.
                    if (statusFx && !statusInjected && turns >= 2 && phase == "PlayerAttack")
                    {
                        InjectStatuses(controller);
                        statusInjected = true;
                        statusInjectedFrame = frame;
                    }
                    if ((statusFx && turns >= 4 || signatureScene && turns >= 2)
                        && phase == "PlayerTurn" && elapsed - turnAt >= 0.6f) break;
                    if (phase == "PlayerTurn" && elapsed - turnAt >= 0.8f)
                    {
                        if (speciesMotions)
                        {
                            if (turns >= motionIds.Length) break;
                            // 계열마다 내 곤충 모델만 갈아 세운다(HP 카드는 장수풍뎅이 그대로 — 몸짓 검수용).
                            arena.RebuildPlayerInsect(Fixture(motionIds[turns], motionNames[turns]), 15);
                            turns++;
                            attack.Invoke(ui, null);
                        }
                        else if (scenario == "escape") escape.Invoke(ui, null);
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
                    // 승리는 연출(BattleFlourish.VictorySeconds 2초) 뒤 머무는 구도까지 본다.
                    // 그 밖의 장면도 보상 줄(0.8초부터 0.3초 간격)과 「눌러서 계속」(0.6초부터)이 다 찍히게 2.6초.
                    if (resultAt >= 0f && elapsed - resultAt >= (victoryScene ? 3.2f : 2.6f)) break;
                }
            }
            File.WriteAllText(Path.Combine(output, "manifest.txt"),
                $"Actual standalone ScreenCapture including IMGUI\nScenario={scenario}\nCameraStyle={BattleCameraDirector.Current}\nSeed=8173\nSize={Screen.width}x{Screen.height}\nFrames={frame}\nResultReached={resultAt >= 0f}\nSwapped={swapped}\nSpeedChanged={speedChanged}\nEnemySwitches={enemySwitches}\nStatusInjectedAtFrame={statusInjectedFrame}\nVictoryFinished={arena.VictoryFinished}\nWildLook={wildLook}\nFixture={(teamDuel ? "ledger_grip team order/levels; first two at 1 HP, ace at 1/4 HP" : speciesMotions ? "player model rebuilt per turn: " + string.Join(",", motionIds) + " (HP card stays rhinoceros_beetle)" : "neutral level15 duel")}; not a balance certification\n");
            bool completed = scenario == "elements" ? turns >= elementCycle.Length
                : teamDuel ? resultAt >= 0f && enemySwitches >= 2
                : statusFx ? statusInjected && turns >= 4
                : signatureScene ? turns >= 2
                : speciesMotions ? turns >= motionIds.Length
                : resultAt >= 0f;
            Application.Quit(completed ? 0 : 3);
        }

        /// <summary>
        /// 몸 상태 검수용 — 컨트롤러 안쪽 상태를 리플렉션으로 건다(저장 없는 픽스처, 밸런스와 무관): 내 곤충 기절 2턴·공격 강화 4턴,
        /// 상대 독(턴당 8) 4턴·방어 강화 4턴. HP 카드 상태 줄(BattleStatusLine)과 아레나 몸 표시(BattleStatusLook)가 같은 원천을 읽는다.
        /// </summary>
        private static void InjectStatuses(InsectBattleController controller)
        {
            MethodInfo add = typeof(InsectBattleController).GetMethod("AddEffect", PrivateInstance);
            FieldInfo stun = typeof(InsectBattleController).GetField("playerStunTurns", PrivateInstance);
            if (add == null || stun == null) { Debug.LogError("[QA] status injection hooks missing"); return; }
            add.Invoke(controller, new object[] { true, 0.3f, 4, InsectBattleController.EffectKind.AtkBuff });
            add.Invoke(controller, new object[] { false, 8f, 4, InsectBattleController.EffectKind.Dot });
            add.Invoke(controller, new object[] { false, 0.3f, 4, InsectBattleController.EffectKind.DefBuff });
            stun.SetValue(controller, 2);
            Debug.Log("[QA] status-fx: player stun 2 + attack up, enemy poison + defense up");
        }

        /// <summary>
        /// 야생 전투 모양 — 대결 표지(<c>duelMode</c>)만 끄고 야생 승리 보상 값을 미리 넣는다(캔디 12·경험치 34·야생 드랍 재료 mat_leaf ×2 — 화면엔 「재료 ×2」·포획 시도 —
        /// <paramref name="captured"/>면 성공). 월드 개체가 없는 승리는 보상 경로를 건너뛰므로 이 값이 결과 화면까지 남는다.
        /// 리플렉션이 못 찾으면 대결 모양 그대로 찍힌다(로그만).
        /// </summary>
        private static void PresentAsWild(InsectBattleController controller, bool captured)
        {
            FieldInfo duel = typeof(InsectBattleController).GetField("duelMode", PrivateInstance);
            if (duel == null) { Debug.LogWarning("[QA] duelMode field missing — entry stays a kid duel"); return; }
            duel.SetValue(controller, false);
            void Set(string name, object value)
            {
                FieldInfo field = typeof(InsectBattleController).GetField(name, PrivateInstance);
                if (field != null) field.SetValue(controller, value);
                else Debug.LogWarning("[QA] reward field missing: " + name);
            }
            Set("lastCandyReward", 12);
            Set("lastExpReward", 34);
            Set("lastCoinReward", 3);   // 야생 승리 코인(BattleVictoryCoins) — 승리 화면 코인 줄
            Set("lastItemId", "mat_leaf");
            Set("lastItemCount", 2);
            Set("lastCaptureAttempted", true);
            Set("lastCaptureSucceeded", captured);
        }

        /// <summary>전용기 검수용 — 그 곤충의 learnset에 전용기 칸을 세운다(<see cref="SignatureSkills.IsSignature"/>가 보는 표시).</summary>
        private static void MakeSignature(InsectData owner, InsectSkill skill, string displayName)
        {
            skill.isSignatureSkill = true;
            skill.displayName = displayName;
            skill.cooldownTurns = 0;
            owner.learnset = new[] { new InsectLearnableSkill { skillId = skill.skillId, learnLevel = 1, skill = skill } };
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

        /// <summary>
        /// 집게(ledger_grip) 팀 대결 픽스처 — <c>NpcBossDuels</c>의 진짜 팀(내보내는 순서·레벨)을 픽스처 곤충으로 세우고 장부도 건다.
        /// 교체 등장을 빨리 보려고 앞 곤충들은 HP 1로 세우고(첫째는 <c>onStarted</c>에서 — 화면이 첫 HP를 읽기 전에), 에이스는 HP 1/4로 들어온다.
        /// </summary>
        private static bool TryStartTeamDuelFixture(InsectBattleController controller, InsectData player, Action onSwitch)
        {
            if (!InsectGame.NPC.NpcBossDuels.TryGet("ledger_grip", out InsectGame.NPC.NpcBossDuels.BossDuel grip) || !grip.IsTeam)
                return false;
            int size = grip.RosterSize;
            var team = new InsectData[size];
            var levels = new int[size];
            for (int i = 0; i < size; i++)
            {
                string id = grip.RosterInsectId(i);
                team[i] = Fixture(id, TeamDuelLabel(id));
                team[i].primaryType = InsectElement.Earth;   // 모래언덕 땅속 것들 — 착지 빛살 색이 이 속성을 따른다
                levels[i] = grip.RosterLevel(i);
            }
            PropertyInfo hp = typeof(InsectBattleStats).GetProperty("CurrentHp");
            if (!controller.StartTeamDuel(player, levels[size - 1], team, levels, (p, e) => hp.SetValue(e, 1)))
                return false;
            controller.SetDuelOpponent(grip.storyNpcId);
            controller.ArmLedger(grip.ledgerThreshold);
            controller.EnemySwitched += (outgoing, incoming) =>
            {
                onSwitch();
                bool ace = controller.EnemyTeamIndex >= size - 1;
                hp.SetValue(incoming, ace ? Mathf.Max(1, incoming.MaxHp / 4) : 1);
            };
            return true;
        }

        private static string TeamDuelLabel(string insectId)
        {
            switch (insectId)
            {
                case "scarab_sand": return "모래 풍뎅이";
                case "antlion_dune": return "모래언덕 개미귀신";
                case "centipede_sand": return "모래 지네";
                default: return insectId;
            }
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
