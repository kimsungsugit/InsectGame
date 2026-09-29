#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Story;
using UnityEditor;
using UnityEngine;

namespace InsectGame.EditorTools
{
    /// <summary>
    /// 스토리 비트를 <b>실제로 발화시켜</b> 순서대로 확인하는 배치모드 도구.
    ///
    /// <code>
    /// Unity.exe -batchmode -projectPath &lt;proj&gt; -logFile &lt;log&gt; \
    ///   -executeMethod InsectGame.EditorTools.StoryBeatWalkthrough.Run \
    ///   -walkOut .claude/cache/story-walk.md [-walkRegion mountain] [-walkMode campaign]
    /// </code>
    ///
    /// <c>-walkMode</c>는 <c>blight</c>(기본 · 오염 거점 아크), <c>campaign</c>(1막 본편 + 꽃밭),
    /// <c>town</c>(마을 이야기 + 지역 의뢰 + 따라가기) 셋이다. 거점 모드는 <c>NpcTalk</c>·<c>CaptureInsect</c>·<c>BattleWin</c>·<c>RegionCleansed</c>만
    /// 두드리므로, <b><c>SubAreaEnter</c>와 <c>GuardianDefeat</c>에 걸린 비트는 오래 사각지대였다</b>
    /// — 본편에서 가장 많이 쓰는 두 트리거인데도 그랬다. campaign 모드가 그 둘을 실제 경로로 두드린다
    /// (근접 → [E] 진입 → 이탈 / <c>RegionManager.DefeatGuardian</c>).
    ///
    /// <b>왜 이게 필요한가.</b> <c>story_lint</c>는 Story.json을 <b>정적으로</b> 읽어 게이트·
    /// 참조 무결성만 본다. 비트가 <b>정말로 뜨는지</b>는 못 본다 — 발화는 트리거 이벤트,
    /// prereq 열람 여부, 리전 게이트, 우선순위 비교(<c>CompareBeatPriority</c>),
    /// <c>pendingBeatId</c> 잠금, 미뤄 둔 트리거 큐가 <b>런타임에</b> 맞물린 결과라서다.
    /// 그중 하나만 어긋나도 예외도 경고도 없이 <b>대사가 그냥 안 뜬다</b>.
    /// <c>LiveSceneCapture</c>도 여기엔 못 쓴다 — 대사창은 IMGUI라 카메라에 안 잡힌다.
    ///
    /// <b>무엇을 얼마나 진짜로 하나.</b> 게임이 쓰는 진입점을 그대로 부른다:
    /// <list type="bullet">
    ///   <item>대화 — <c>StoryDirector.OnNpcTalked</c> (<c>WorldInteractionController</c>가 부르는 그것)</item>
    ///   <item>포획 — <c>PlayerInsectCollection.AddCapturedInsect</c> → <c>InsectCaptured</c> 이벤트</item>
    ///   <item>전투 승리 — <c>InsectBattleController.BattleEnded</c> 대리자를 <b>구독 경로 그대로</b> 호출
    ///     (이벤트는 밖에서 올릴 수 없어 리플렉션으로 꺼낼 뿐이다. 구독이 비어 있으면 그 자체가
    ///     영구 미발화 결함이라 FAIL로 잡는다)</item>
    ///   <item>정화 — <c>RegionBlightManager.CleanseByBoss</c> (승리 경로와 같은 함수)</item>
    ///   <item>대사 닫기 — <c>NpcDialogueUI.CloseModal</c> (플레이어가 닫는 그 경로. 보상·열람 기록이 여기서 난다)</item>
    /// </list>
    /// <b>선행 비트만</b> <c>CompleteBeat</c>로 채운다 — 여기서 검증하려는 건 <c>bl_*</c>이지
    /// 1막 82비트가 아니다. 무엇을 채웠는지는 보고서에 그대로 적는다.
    ///
    /// <b>거점 목록을 박아두지 않는다.</b> <c>RegionData.HasBlightSite</c>를 런타임에 훑어
    /// 거점이 늘면 걸음도 저절로 늘어난다.
    ///
    /// 함정 셋(<c>LiveSceneCapture</c>와 같은 계열):
    /// <list type="number">
    ///   <item>플레이모드 진입은 도메인 리로드라 정적 상태가 날아간다 — <c>SessionState</c>로 넘긴다.</item>
    ///   <item>대사가 뜬 채로 두면 <c>DrainPendingTriggers</c>가 모달 가드에 막혀 <b>다음 비트가 영영 안 온다</b>.
    ///     그래서 청소부(<c>TickJanitor</c>)가 매 틱 하나씩 닫는다.</item>
    ///   <item><c>Time.timeScale</c>이 0으로 얼어 있는 구간이 있다 — 시간은 전부 <c>realtimeSinceStartup</c>으로 센다.</item>
    /// </list>
    /// </summary>
    public static class StoryBeatWalkthrough
    {
        private const string StageKey = "InsectGame.StoryBeatWalkthrough.Stage";
        private const string DefaultScene = "Assets/Scenes/PlayScene.unity";
        private const string DefaultOut = ".claude/cache/story-walk.md";

        /// <summary>플레이모드에 못 들어가거나 걸음이 끝나지 않을 때의 탈출구(초).</summary>
        private const float HardTimeoutSeconds = 900f;
        /// <summary>부트스트랩이 매니저를 다 지을 때까지 기다리는 시간(초).</summary>
        private const float BootSeconds = 6f;
        /// <summary>한 걸음이 결과를 낼 때까지 기다리는 시간(초).</summary>
        private const float StepTimeout = 14f;
        /// <summary>대사창이 열리기를 기다리는 시간(초) — 등장 연출이 앞에 붙으면 늦게 열린다.</summary>
        private const float ModalOpenWait = 8f;

        private static string outPath;
        private static string onlyRegion;
        /// <summary>
        /// 무엇을 걸을 것인가 — <c>blight</c>(기본, 오염 거점 아크) 또는 <c>campaign</c>(1막 본편 + 꽃밭).
        ///
        /// 거점 아크만 걷던 시절에는 <c>SubAreaEnter</c>·<c>GuardianDefeat</c> 구동부가 아예 없었다.
        /// 그 둘은 <b>정적 검사로는 발화를 증명할 수 없는</b> 트리거인데(story_lint는 Story.json을
        /// 읽을 뿐이고 IMGUI는 캡처가 안 된다) 본편 비트 상당수가 거기 걸려 있다.
        /// </summary>
        private static string walkMode;
        // `-walkChoice last` — 선택지가 뜨면 마지막 항목을 고른다(기본은 첫 항목).
        private static bool walkChoiceLast;

        private static float startTime;
        private static float bootDeadline;
        private static bool booted;

        // 발화 기록 — StoryBeatTriggered 구독으로 채운다(발화의 유일한 진실).
        private static readonly List<string> firedLog = new List<string>();
        // 아직 안 닫은 비트 큐. 청소부가 하나씩 닫는다.
        private static readonly List<string> toClose = new List<string>();
        private static string closingId;
        private static float closingSince;

        private static List<Step> steps;
        private static int cursor;
        private static readonly List<string> seeded = new List<string>();
        private static StoryDirector director;

        private class Step
        {
            public string site;          // 어느 거점의 걸음인가(빈 문자열이면 공통)
            public string label;
            public Action act;           // 이 걸음이 게임에 가하는 행위
            public Func<bool> until;     // 이게 참이 되면 통과
            public string expectBeat;    // 기대 비트(보고서용, until과 짝)
            public int maxTries = 1;

            public bool started;
            public int tries;
            public float since;
            public bool passed;
            public float elapsed;
        }

        [MenuItem("InsectGame/오염 거점/스토리 비트 걸어보기 (배치모드 전용)", false, 400)]
        public static void Run()
        {
            outPath = ReadArg("-walkOut", DefaultOut);
            onlyRegion = ReadArg("-walkRegion", "");
            walkMode = ReadArg("-walkMode", "blight");
            walkChoiceLast = ReadArg("-walkChoice", "first") == "last";

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                DefaultScene, UnityEditor.SceneManagement.OpenSceneMode.Single);

            SessionState.SetString(StageKey, outPath + "|" + onlyRegion + "|" + walkMode
                + "|" + (walkChoiceLast ? "last" : "first"));
            Log("scene=" + DefaultScene + " out=" + outPath
                + " region=" + (onlyRegion == "" ? "(전부)" : onlyRegion)
                + " mode=" + walkMode + " choice=" + (walkChoiceLast ? "last" : "first"));
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            string stage = SessionState.GetString(StageKey, "");
            if (string.IsNullOrEmpty(stage)) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;

            string[] parts = stage.Split('|');
            outPath = parts.Length > 0 ? parts[0] : DefaultOut;
            onlyRegion = parts.Length > 1 ? parts[1] : "";
            walkMode = parts.Length > 2 && parts[2] != "" ? parts[2] : "blight";
            walkChoiceLast = parts.Length > 3 && parts[3] == "last";

            startTime = Time.realtimeSinceStartup;
            bootDeadline = startTime + BootSeconds;
            booted = false;
            firedLog.Clear();
            toClose.Clear();
            seeded.Clear();
            closingId = null;
            steps = null;
            cursor = 0;

            EditorApplication.update += Tick;
            Log("플레이모드 진입 — 부트스트랩 대기");
        }

        // ──────────────────────────────────────────────────────────────
        private static void Tick()
        {
            float now = Time.realtimeSinceStartup;
            if (now - startTime > HardTimeoutSeconds)
            {
                Finish("하드 타임아웃 — 걸음이 끝나지 않았다");
                return;
            }

            if (!booted)
            {
                if (now < bootDeadline) return;
                if (!Bootstrapped()) { bootDeadline = now + 1f; return; }
                booted = true;
                if (IsCampaign) BuildCampaignSteps(); else if (IsTown) BuildTownSteps(); else BuildSteps();
                if (steps.Count == 0) { Finish("걸어볼 것이 없다"); return; }
                Log((IsCampaign ? "본편" : IsTown ? "마을 이야기" : "거점 " + SiteCount() + "개")
                    + " · 걸음 " + steps.Count + "개 시작");
            }

            TickJanitor(now);
            TickSteps(now);
        }

        /// <summary>
        /// 열린 대사를 하나씩 닫는다. <b>이걸 안 하면 다음 비트가 영영 안 온다</b> —
        /// <c>FireBeat</c>는 <c>pendingBeatId</c>가 차 있으면 그냥 돌아가고,
        /// <c>DrainPendingTriggers</c>는 모달이 열려 있으면 큐를 흘리지 않는다.
        /// 플레이어가 대사를 읽고 닫는 그 행위에 해당한다.
        /// </summary>
        private static void TickJanitor(float now)
        {
            // 수문장 배지 연출 — 수문장 걸음마다 gd_* 대사보다 먼저 뜬다. 모달이라 닫지 않으면 대사가 영영 안 온다.
            // 플레이어가 화면을 누르는 것처럼 닫고, 떴다는 사실을 보고서에 남긴다(순서 검증).
            var ceremony = UnityEngine.Object.FindFirstObjectByType<InsectGame.UI.BadgeCeremonyUI>();
            if (ceremony != null && ceremony.IsOpen)
            {
                Log("배지 연출 — " + ceremony.CurrentRegionId);
                ceremony.CloseModal();
                return;
            }

            if (closingId == null)
            {
                if (toClose.Count == 0) return;
                closingId = toClose[0];
                toClose.RemoveAt(0);
                closingSince = now;
                return;
            }

            var ui = UnityEngine.Object.FindFirstObjectByType<InsectGame.UI.NpcDialogueUI>();
            if (ui != null && ui.IsOpen)
            {
                if (ui.HasChoices)
                {
                    // 선택지가 붙은 비트는 고르는 것이 곧 닫는 것이다. 기본은 첫 항목,
                    // `-walkChoice last`면 마지막 항목 — 양쪽 결과 leaf가 실제로 뜨는지 두 번 돈다.
                    int idx = walkChoiceLast ? int.MaxValue : 0;
                    ui.SelectChoice(idx);
                    Log("선택 — " + closingId + " → " + (walkChoiceLast ? "마지막 항목" : "첫 항목"));
                }
                else
                {
                    ui.CloseModal();          // 플레이어가 닫는 경로 — 보상·열람 기록이 여기서 난다
                }
                closingId = null;
                return;
            }
            if (now - closingSince < ModalOpenWait) return;

            // 끝내 안 열렸다 — 렌더러 미배선이거나 등장 연출이 멈춰 섰다. 진행은 잇는다.
            if (director != null) director.CompleteBeat(closingId);
            Log("대사창이 안 열려 직접 완료 처리 — " + closingId);
            closingId = null;
        }

        private static void TickSteps(float now)
        {
            if (cursor >= steps.Count) { Finish(null); return; }
            Step s = steps[cursor];

            if (!s.started)
            {
                // 앞 걸음의 뒷정리(대사·컷신)가 끝나야 다음 행위를 가한다 — 안 그러면 겹친다.
                if (!Idle()) return;
                s.started = true;
                s.tries = 1;
                s.since = now;
                SafeAct(s);
                return;
            }

            if (s.until())
            {
                s.passed = true;
                s.elapsed = now - s.since;
                Advance();
                return;
            }

            if (now - s.since < StepTimeout) return;

            if (s.tries < s.maxTries)
            {
                // 다른 비트가 먼저 나갔을 수 있다(같은 트리거에 자격을 갖는 본편 비트).
                // 플레이어가 한 번 더 말을 거는 것과 같다 — 뒷정리가 끝난 뒤에만.
                if (!Idle()) return;
                s.tries++;
                s.since = now;
                SafeAct(s);
                return;
            }

            s.passed = false;
            s.elapsed = now - s.since;
            Advance();
        }

        private static void Advance()
        {
            Step s = steps[cursor];
            Log("[" + (s.passed ? "OK" : "FAIL") + "] " + s.label
                + " (시도 " + s.tries + "회, " + s.elapsed.ToString("F1") + "s)");
            cursor++;
        }

        private static void SafeAct(Step s)
        {
            try { if (s.act != null) s.act(); }
            catch (Exception e) { Log("걸음 예외 — " + s.label + ": " + e.Message); }
        }

        /// <summary>대사도 컷신도 없고 닫을 것도 없는 상태.</summary>
        private static bool Idle()
        {
            if (closingId != null || toClose.Count > 0) return false;
            if (InsectGame.UI.ModalUIRegistry.IsAnyOpen()) return false;
            var cut = UnityEngine.Object.FindFirstObjectByType<CutsceneDirector>();
            return cut == null || !cut.IsPlaying;
        }

        // ──────────────────────────────────────────────────────────────
        private static bool Bootstrapped()
        {
            director = UnityEngine.Object.FindFirstObjectByType<StoryDirector>();
            return director != null
                && UnityEngine.Object.FindFirstObjectByType<RegionManager>() != null
                && UnityEngine.Object.FindFirstObjectByType<RegionBlightManager>() != null
                && UnityEngine.Object.FindFirstObjectByType<PlayerInsectCollection>() != null
                && GameObject.Find("Player") != null;
        }

        private static int SiteCount()
        {
            int n = 0;
            foreach (RegionData r in Sites()) n++;
            return n;
        }

        /// <summary>
        /// 거점이 있는 리전 — <b>목록을 박아두지 않는다.</b> 거점을 늘리면 걸음도 저절로 는다.
        /// </summary>
        private static IEnumerable<RegionData> Sites()
        {
            var rm = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
            if (rm == null || rm.Regions == null) yield break;
            foreach (RegionData r in rm.Regions)
            {
                if (r == null || !r.HasBlightSite) continue;
                if (!string.IsNullOrEmpty(onlyRegion) && r.regionId != onlyRegion) continue;
                yield return r;
            }
        }

        private static void BuildSteps()
        {
            steps = new List<Step>();

            // 반복 가능해야 비교가 된다 — 열람 기록과 정화 기록을 함께 지운다.
            steps.Add(new Step
            {
                site = "-",
                label = "진행 기록 초기화(스토리 열람 + 정화)",
                act = ResetProgress,
                until = () => true,
            });

            foreach (RegionData r in Sites())
            {
                string rid = r.regionId;
                string boss = r.blightBossNpcId;
                string insect = r.blightReturningInsectId;
                string site = r.displayName + "(" + rid + ")";

                steps.Add(new Step
                {
                    site = site,
                    label = "선행 비트 채우기",
                    act = () => SeedPrerequisites("bl_" + rid + "_arrive"),
                    until = () => true,
                });
                steps.Add(new Step
                {
                    site = site,
                    label = "리전 이동",
                    act = () => MoveToRegion(rid),
                    until = () =>
                    {
                        var rm = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
                        return rm != null && rm.CurrentRegion != null && rm.CurrentRegion.regionId == rid;
                    },
                    maxTries = 3,
                });

                steps.Add(BeatStep(site, "말 걸기 ①", "bl_" + rid + "_arrive", () => Talk(boss), 6));
                steps.Add(BeatStep(site, "포획", "bl_" + rid + "_sign", () => Capture(insect), 4));
                steps.Add(BeatStep(site, "말 걸기 ②(대치 연출)", "bl_" + rid + "_confront", () => Talk(boss), 6));
                steps.Add(BeatStep(site, "전투 승리", "bl_" + rid + "_clash", WinBattle, 4));
                steps.Add(BeatStep(site, "정화(컷신)", "bl_" + rid + "_restore", () => Cleanse(boss, rid), 4));
            }
        }

        private static bool IsCampaign { get { return walkMode == "campaign"; } }

        /// <summary>
        /// 1막 본편 + 꽃밭 걸음. 거점 걸음(<see cref="BuildSteps"/>)이 <c>NpcTalk</c>·<c>CaptureInsect</c>·
        /// <c>BattleWin</c>·<c>RegionCleansed</c>만 두드리는 것과 달리, 여기서는
        /// <b><c>SubAreaEnter</c>와 <c>GuardianDefeat</c>도 실제 경로로</b> 두드린다.
        ///
        /// 리전 순서가 곧 여운 체인 순서다 — 연못(ch2)·숲(ch3)·습지(ch4)·산(ch5)·유적(ch6)을
        /// 이 차례로 걸어야 같은 NPC의 여운이 하나씩 열린다(체인은 직전 여운을 prereq로 문다).
        /// 순서를 바꾸면 뒤 여운이 안 열리는데, 그건 결함이 아니라 저작 그대로다.
        /// </summary>
        private static void BuildCampaignSteps()
        {
            steps = new List<Step>();
            steps.Add(new Step
            {
                site = "-",
                label = "진행 기록 초기화(스토리 열람 + 수문장)",
                act = ResetProgress,
                until = () => true,
            });

            // 초원 — 서브에리어에 이야기가 있다는 걸 처음 가르치는 자리
            Region("초원(meadow)", "meadow");
            SubAreaBeat("초원(meadow)", "meadow_cave", "ch1_cave", "동굴 진입");

            // 연못 — 대치 슬롯이 NpcTalk뿐이던 자리를 서브에리어로 채웠다
            Region("연못(pond)", "pond");
            SubAreaBeat("연못(pond)", "pond_reeds", "ch2_reeds", "갈대 진입");
            Beat("연못(pond)", "전투 승리", "ch2_clash", WinBattle, 4);
            Beat("연못(pond)", "수문장 격파", "gd_pond", () => Guardian("pond"), 3);
            Beat("연못(pond)", "말 걸기(여운)", "ch2_echo", () => Talk("catcher_rival"), 6);

            // 숲 — 세라는 원래 여기 서 있었다(앵커 추가 대상 아님)
            Region("숲(forest)", "forest");
            Beat("숲(forest)", "말 걸기(여운)", "ch3_echo", () => Talk("ruins_scholar"), 6);
            Beat("숲(forest)", "수문장 격파", "gd_forest", () => Guardian("forest"), 3);

            // 습지 — 라온 앵커 신규
            Region("습지(swamp)", "swamp");
            SubAreaBeat("습지(swamp)", "swamp_fog", "ch4_fog", "안개 진입");
            Beat("습지(swamp)", "말 걸기(여운)", "ch4_echo", () => Talk("catcher_rival"), 6);
            Beat("습지(swamp)", "수문장 격파", "gd_swamp", () => Guardian("swamp"), 3);

            // 산 — 세라 앵커 신규. ch5_clash와 bl_mountain_clash가 같은 트리거를 다툰다(정상)
            Region("산(mountain)", "mountain");
            Beat("산(mountain)", "전투 승리", "ch5_clash", WinBattle, 4);
            Beat("산(mountain)", "말 걸기(여운)", "ch5_echo", () => Talk("ruins_scholar"), 6);
            Beat("산(mountain)", "수문장 격파", "gd_mountain", () => Guardian("mountain"), 3);

            // 유적 — 세라 앵커 신규
            Region("유적(ruins)", "ruins");
            Beat("유적(ruins)", "말 걸기(여운)", "ch6_echo", () => Talk("ruins_scholar"), 6);

            // 꽃밭 — 이 라운드의 핵심. 비트 1개짜리였던 분기 지역에 5비트를 채웠다
            Region("꽃밭(garden)", "garden");
            SubAreaBeat("꽃밭(garden)", "garden_greenhouse", "garden_glass", "온실 진입(대치 연출)");
            Beat("꽃밭(garden)", "포획", "garden_sign", () => Capture("butterfly_azure"), 4);
            Beat("꽃밭(garden)", "전투 승리", "garden_clash", WinBattle, 4);
            SubAreaBeat("꽃밭(garden)", "garden_maze", "garden_fence", "미로 진입(떡밥 절반)");
            Beat("꽃밭(garden)", "수문장 격파", "gd_garden", () => Guardian("garden"), 3);

            // 이름 없는 자리 — 최종 수문장에 서사를 붙였다
            const string fin = "이름 없는 자리(nameless)";
            Region(fin, "nameless");
            Beat(fin, "수문장 격파", "gd_nameless", () => Guardian("nameless"), 3);

            // 최종장 — `fin_seal`이 **아무 승리로도 터지던** 자리다. 종 지정이 붙었으니
            // 여기서 마지막 두 걸음이 갈린다: 잡몹을 이기면 엔딩이 **안 나야** 하고,
            // 이름 없는 사마귀를 이겨야 난다. 그 순서 그대로 걷는다.
            SubAreaBeat(fin, "nameless_ledger", "ch12_confront", "장부의 방(관장 대면)");
            SubAreaBeat(fin, "nameless_core", "fin_unnamed", "빈칸(무명 대면·컷신)");
            // 무명 대면의 선택지 — 고르는 것이 대사창을 닫고, 결과 leaf가 큐 맨 앞에서 곧바로 뜬다.
            // 첫 항목(거절)이 기본, `-walkChoice last`면 수락 쪽(세라가 막는다). 둘 다 leaf라 진행은 같다.
            steps.Add(BeatStep(fin, walkChoiceLast ? "선택 결과(이름을 준다 → 세라가 막는다)" : "선택 결과(이름을 주지 않는다)",
                walkChoiceLast ? "fin_named" : "fin_refuse", () => { }, 2));
            steps.Add(new Step
            {
                site = fin,
                label = "잡몹 1v1 승리 — 엔딩이 뜨면 안 된다",
                // moth_pale은 Common이라 1v1이 실제로 열린다(레이드 대상이 아니다).
                act = () => WinBattleAgainst("moth_pale"),
                // 발화 자체를 기다리지 않는다. 큐가 비고 조용하면 통과 — 즉 아무 비트도 안 떴다는 뜻.
                until = () => Idle() && !firedLog.Contains("fin_seal"),
                maxTries = 1,
            });
            // **레이드로 건다.** mantis_unnamed는 Legendary라 CaptureChoiceUI가 1v1을 막는다 —
            // 1v1로 걸으면 게임에 없는 경로를 통과시키는 셈이 되고, 그게 이 결함을 놓친 이유다.
            Beat(fin, "이름 없는 사마귀 레이드 격파(엔딩)", "fin_seal",
                () => WinRaidAgainst("mantis_unnamed"), 3);
            // 관장의 마지막 말은 **추가 전투 없이 말을 걸어** 듣는다(StoryBible 0장 2026-09-19 연결성 보정 —
            // 트리거가 BattleWin → NpcTalk ledger_chief). 걸음이 옛 트리거로 두드리면 3회 시도 뒤 FAIL이 난다.
            Beat(fin, "말 걸기(관장 마지막 말)", "ch12_clash", () => Talk("ledger_chief"), 6);
        }

        private static bool IsTown { get { return walkMode == "town"; } }

        // 마을 이야기 한 편 — (리전, 주민, 의뢰, 의뢰가 전투인가, 목표 수, 징후 종).
        // 저작 데이터에서 파생하지 않고 적어 두는 이유: 이 표가 곧 **기대값**이다. 저작이 바뀌어
        // 여기와 어긋나면 걸음이 FAIL로 알려 준다(TownTaleTests가 정적 정합을, 이 걸음이 발화를 본다).
        private static readonly (string region, string npc, string quest, bool battle, int count, string sign)[] Tales =
        {
            ("meadow", "town_meadow", "s_town_meadow", false, 6, "ladybug_seven"),
            ("pond", "town_pond", "s_town_pond", false, 5, "water_strider_pond"),
            ("forest", "town_forest", "s_town_forest", true, 3, "cicada_summer"),
            ("swamp", "town_swamp", "s_town_swamp", false, 5, "firefly_swamp"),
            ("mountain", "town_mountain", "s_town_mountain", true, 4, "katydid_leaf"),
            ("garden", "town_garden", "s_town_garden", false, 5, "butterfly_peacock"),
            ("ruins", "town_ruins", "s_town_ruins", false, 3, "cricket_tomb"),
            // 2막 — 후일담 게이트가 종장(fin_seal)까지 가므로 선행 채우기가 본편을 거의 다 채운다.
            ("hollow", "town_hollow", "s_town_hollow", false, 5, "cricket_hush"),
            ("dunes", "town_dunes", "s_town_dunes", false, 5, "bee_digger"),
            ("frostline", "town_frostline", "s_town_frostline", true, 4, "beetle_hoarfrost"),
            ("emberfall", "town_emberfall", "s_town_emberfall", false, 5, "cricket_ember"),
            ("canopy", "town_canopy", "s_town_canopy", false, 3, "caterpillar_silk"),
        };

        /// <summary>
        /// 마을 이야기 걸음(<c>-walkMode town</c>). 스토리 발화만이 아니라 <b>퀘스트·따라가기와 맞물리는 곳</b>을
        /// 실제 진입점으로 본다 — 이 연작의 결함은 전부 그 이음매에서 조용히 난다:
        /// <list type="bullet">
        ///   <item>의뢰가 <b>다른 리전에서 세어지면</b> 안 된다(<c>QuestRegionGate</c>) — 리전 이동 전에 한 번 두드려 0인지 본다.</item>
        ///   <item>의뢰 완료 → 매듭 비트의 <c>requiredQuestId</c> 게이트가 열리는가.</item>
        ///   <item>따라가기가 만남 뒤 <c>[의뢰]</c> 목표를 잡고, 완료 뒤 <c>?</c> 표식과 "알리기"로 바뀌고,
        ///     매듭 뒤 본편을 기다리는 단계가 되면 스스로 풀리는가(<c>StoryObjectiveTracker</c>).</item>
        /// </list>
        /// 배치에는 로그인이 없어 퀘스트 세션이 꺼져 있고(<c>NotifyAction</c>이 무시된다) 해금 리전은 로컬 설정에
        /// 달려 있다 — 둘을 먼저 채우고 무엇을 채웠는지 보고서에 적는다(검증 대상 아님).
        /// </summary>
        private static void BuildTownSteps()
        {
            steps = new List<Step>();
            steps.Add(new Step
            {
                site = "-",
                label = "진행 기록 초기화(스토리 열람 + 수문장) + 의뢰 상태 채우기",
                act = () => { ResetProgress(); SeedTownQuestState(); },
                until = () => true,
            });

            for (int i = 0; i < Tales.Length; i++)
            {
                var t = Tales[i];
                if (!string.IsNullOrEmpty(onlyRegion) && t.region != onlyRegion) continue;
                string site = t.region + "(" + t.npc + ")";

                // 리전 밖 행동은 세지 않는다 — 앞 리전에 선 채로 한 번 두드린다(초원은 시작 지점이라 뺀다).
                if (i > 0)
                {
                    string quest = t.quest;
                    bool battle = t.battle;
                    steps.Add(new Step
                    {
                        site = site,
                        label = "리전 밖 의뢰 행동 — 세지 않아야 한다",
                        act = () => ErrandAction(battle, 1, quietBattle: true),
                        until = () => TownQuests() != null && TownQuests().GetSideProgress(quest) == 0
                            && !TownQuests().IsQuestCompleted(quest),
                        maxTries = 1,
                    });
                }

                Region(site, t.region);
                Beat(site, "만남(말 걸기)", "town_" + t.region + "_meet", () => Talk(t.npc), 6);

                string npc = t.npc;
                steps.Add(new Step
                {
                    site = site,
                    label = "따라가기 → [의뢰] 목표",
                    act = () => { var tr = Tracker(); if (tr != null) tr.TrackTale(npc); },
                    until = () => { var tr = Tracker(); return tr != null && tr.IsTrackingTale && tr.Label.StartsWith("[의뢰]"); },
                    maxTries = 2,
                });
                // 같은 리전의 본편 포획 비트(chN, 순위 앞)가 먼저 나가면 한 번에 하나씩 밀린다 — 넉넉히 준다.
                steps.Add(BeatStep(site, "징후(포획)", "town_" + t.region + "_sign", () => Capture(t.sign), 6));

                string q = t.quest;
                bool isBattle = t.battle;
                int count = t.count;
                steps.Add(new Step
                {
                    site = site,
                    label = "의뢰 " + count + "회(" + (isBattle ? "전투 승리" : "포획") + ")",
                    act = () => ErrandAction(isBattle, count, quietBattle: false),
                    until = () => TownQuests() != null && TownQuests().IsQuestCompleted(q),
                    maxTries = 2,
                });
                steps.Add(new Step
                {
                    site = site,
                    label = "보고 표식(?) + '알리기' 목표",
                    until = () =>
                    {
                        var tr = Tracker();
                        return director != null && director.GetTaleStep(npc, out _) == TaleStepKind.Report
                            && tr != null && HasMarker(tr, npc, InsectGame.NPC.QuestMark.Report)
                            && tr.Label.Contains("알리기");
                    },
                });
                steps.Add(BeatStep(site, "매듭(말 걸기)", "town_" + t.region + "_close", () => Talk(npc), 6));
                // 매듭 뒤 — 후일담 게이트(본편 뒷 장면)가 아직이면 따라가기가 **스스로 풀려야** 하고,
                // 이미 열려 있으면 **계속 따라가며** "말 걸기"를 가리켜야 한다. 둘 다 이 걸음에서 실제로 난다:
                // 앞 마을의 후일담을 채우며 본편 사슬이 함께 채워지므로(연못 → ch8_confront의 조상에
                // 습지 게이트 ch7_confront가, 초원 → ch7_arrive의 조상에 유적 게이트 ch7_opening이 있다).
                steps.Add(new Step
                {
                    site = site,
                    label = "매듭 뒤 — 대기면 따라가기 해제 / 후일담이 열려 있으면 계속",
                    until = () =>
                    {
                        var tr = Tracker();
                        if (tr == null || director == null) return false;
                        TaleStepKind k = director.GetTaleStep(npc, out _);
                        if (k == TaleStepKind.Waiting) return !tr.IsTrackingTale;
                        return k == TaleStepKind.Talk && tr.IsTrackingTale && tr.Label.Contains("말 걸기");
                    },
                });
                Beat(site, "후일담(말 걸기)", "town_" + t.region + "_after", () => Talk(npc), 6);
                steps.Add(new Step
                {
                    site = site,
                    label = "이야기 끝(Done) · 표식 없음",
                    until = () =>
                    {
                        var tr = Tracker();
                        return director != null && director.GetTaleStep(npc, out _) == TaleStepKind.Done
                            && tr != null && !HasMarker(tr, npc, InsectGame.NPC.QuestMark.New)
                            && !HasMarker(tr, npc, InsectGame.NPC.QuestMark.Report);
                    },
                });
            }
        }

        private static TutorialQuestManager TownQuests()
        {
            return UnityEngine.Object.FindFirstObjectByType<TutorialQuestManager>();
        }

        private static StoryObjectiveTracker Tracker()
        {
            return UnityEngine.Object.FindFirstObjectByType<StoryObjectiveTracker>();
        }

        private static bool HasMarker(StoryObjectiveTracker tr, string npcId, InsectGame.NPC.QuestMark mark)
        {
            var marks = tr.TaleMarkers;
            for (int i = 0; i < marks.Count; i++)
                if (marks[i].Npc != null && marks[i].Npc.StoryNpcId == npcId && marks[i].Mark == mark) return true;
            return false;
        }

        /// <summary>
        /// 의뢰 행동 — 포획은 <c>CaptureController</c>가 부르는 <c>NotifyCapture</c>, 전투는 <c>BattleEnded</c>
        /// 구독 경로(<see cref="WinBattle"/>) 그대로다. 리전 판정은 매니저 안에서 난다(그게 검증 대상이다).
        /// <paramref name="quietBattle"/>이면 전투도 퀘스트 통지만 한다 — 리전 밖 확인에서 앞 리전의
        /// 본편 전투 비트까지 흔들 이유가 없다(퀘스트가 BattleEnded에서 부르는 바로 그 함수다).
        /// </summary>
        private static void ErrandAction(bool battle, int count, bool quietBattle)
        {
            var qm = TownQuests();
            for (int i = 0; i < count; i++)
            {
                if (!battle) { if (qm != null) qm.NotifyCapture(InsectGame.Data.InsectRarity.Uncommon); }
                else if (quietBattle) { if (qm != null) qm.NotifyAction(QuestType.Battle); }
                else WinBattle();
            }
        }

        /// <summary>
        /// 의뢰가 세어질 수 있는 상태를 만든다 — 퀘스트 세션 켜기, 마을 의뢰 진행 비우기, 선행 퀘스트·리전 해금.
        /// 전부 인메모리이고, 퀘스트가 진행을 저장할 때 **배치 환경의 전역(비로그인) 키**에만 남는다 —
        /// 그 키는 걸음 전 값으로 떠 두었다가 <see cref="Finish"/>가 되돌린다(<see cref="SnapshotPlayerState"/>).
        /// </summary>
        private static void SeedTownQuestState()
        {
            SnapshotPlayerState();
            var qm = TownQuests();
            var rm = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
            if (qm == null || rm == null) { Log("퀘스트/리전 매니저 없음 — 의뢰를 채울 수 없다"); return; }

            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(TutorialQuestManager).GetField("tutorialSessionStarted", F).SetValue(qm, true);
            var completed = (HashSet<string>)typeof(TutorialQuestManager).GetField("completedQuests", F).GetValue(qm);
            var side = (Dictionary<string, int>)typeof(TutorialQuestManager).GetField("sideProgress", F).GetValue(qm);
            var unlocked = (HashSet<string>)typeof(RegionManager).GetField("unlockedRegions", F).GetValue(rm);

            foreach (var t in Tales)
            {
                completed.Remove(t.quest);
                side.Remove(t.quest);
                if (unlocked.Add(t.region)) seeded.Add("region:" + t.region);
            }
            foreach (string prereq in new[] { "q_capture3", "q_battle" })
                if (completed.Add(prereq)) seeded.Add("quest:" + prereq);

            // 따라가던 이야기가 남아 있으면 첫 걸음의 기대("따라가기 켜기")가 흐려진다.
            var tr = Tracker();
            if (tr != null) tr.StopTrackingTale();
            Log("마을 의뢰 상태 채움 — 세션 켜기, 의뢰 " + Tales.Length + "개 비움, 선행·해금 " + seeded.Count + "건");
        }

        // ── 걸음 전 상태 원복 ──
        // 걸음은 개발 PC의 **비로그인(전역) 세이브**를 그대로 쓴다. 진행 초기화(ResetProgress)가 스토리 세이브·
        // 수문장 격파·정화 기록을 지우고, 걸음 자체가 포획 보상·퀘스트·해금·따라가기를 저장한다. 되돌리지 않으면
        // 걸음 뒤 에디터에서 비로그인으로 플레이할 때 "해금됐는데 수문장은 안 쓰러진" 같은 섞인 상태가 남았다.
        // 첫 초기화 직전의 세이브 파일 전부(GameConstants.SaveFiles — 목록을 박지 않고 리플렉션으로 읽는다)와
        // 진행 키를 떠 두었다가 Finish에서 되돌린다. 설정 키(음량·그래픽)는 걸음이 건드리지 않아 빼 둔다.
        private static readonly string[] WalkTouchedPrefs =
        {
            GameConstants.PrefsKeys.QuestProgress, GameConstants.PrefsKeys.QuestCompleted,
            GameConstants.PrefsKeys.ActiveQuest, GameConstants.PrefsKeys.QuestSideProgress,
            GameConstants.PrefsKeys.QuestSideRepeat, GameConstants.PrefsKeys.QuestUnseen,
            GameConstants.PrefsKeys.TutorialHidden, GameConstants.PrefsKeys.WeeklyContestClaimed,
            GameConstants.PrefsKeys.BlightCleansed, GameConstants.PrefsKeys.BadgeMilestonesClaimed,
            GameConstants.PrefsKeys.TrackedTale, GameConstants.PrefsKeys.LastSubAreaId,
            // RegionManager·RegionBlightManager의 private 키와 같은 문자열 — 어긋나면 원복이 조용히 빠진다.
            "InsectGame.UnlockedRegions", "InsectGame.DefeatedGuardians", "InsectGame.DefeatedLedgerBosses",
        };

        private enum PrefKind { Absent, String, Int, Float }
        private struct PrefSnap { public PrefKind kind; public string s; public int i; public float f; }

        private static Dictionary<string, PrefSnap> prefsSnapshot;      // 스코프 키 → 걸음 전 값
        private static Dictionary<string, byte[]> saveFileSnapshot;    // 파일 경로 → 걸음 전 내용(null = 없던 파일)

        private static void SnapshotPlayerState()
        {
            if (prefsSnapshot != null) return;   // 첫 초기화 직전 값만 뜬다
            prefsSnapshot = new Dictionary<string, PrefSnap>();
            foreach (string baseKey in WalkTouchedPrefs)
            {
                string key = SaveScope.PrefsKey(baseKey);
                prefsSnapshot[key] = TakePref(key);
            }
            saveFileSnapshot = new Dictionary<string, byte[]>();
            foreach (FieldInfo f in typeof(GameConstants.SaveFiles).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!f.IsLiteral || f.FieldType != typeof(string)) continue;
                string path = SaveScope.FilePath((string)f.GetRawConstantValue());
                try { saveFileSnapshot[path] = File.Exists(path) ? File.ReadAllBytes(path) : null; }
                catch (Exception e) { Log("세이브 스냅숏 실패(" + path + "): " + e.Message); }
            }
            Log("걸음 전 상태를 떠 둠 — 키 " + prefsSnapshot.Count + "개, 세이브 파일 " + saveFileSnapshot.Count + "개");
        }

        // PlayerPrefs는 타입을 알려 주지 않는다 — 문자열 → 정수 → 실수 순으로 떠 본다(틀린 타입으로 읽으면 기본값이 온다).
        // 정수를 문자열로 되돌리면 GetInt 판독부가 0을 읽는다(SaveScope의 v3 결함과 같은 함정).
        private static PrefSnap TakePref(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return new PrefSnap { kind = PrefKind.Absent };
            const string Sentinel = "\u0001walk-sentinel";
            string sv = PlayerPrefs.GetString(key, Sentinel);
            if (sv != Sentinel) return new PrefSnap { kind = PrefKind.String, s = sv };
            int iv = PlayerPrefs.GetInt(key, int.MinValue);
            if (iv != int.MinValue) return new PrefSnap { kind = PrefKind.Int, i = iv };
            return new PrefSnap { kind = PrefKind.Float, f = PlayerPrefs.GetFloat(key, 0f) };
        }

        private static void RestorePlayerState()
        {
            if (prefsSnapshot == null) return;
            // 디바운스로 미뤄 둔 저장(PlayerInsectCollection 등)은 종료 중 OnDisable에서 flush된다 — 되돌린 뒤에
            // 그게 오면 원복을 덮는다. 씬을 먼저 꺼서 지금 다 쓰게 하고(dirty가 내려간다) 그 위에 되돌린다.
            if (Application.isPlaying)
                foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    if (root != null) root.SetActive(false);
            foreach (KeyValuePair<string, PrefSnap> kv in prefsSnapshot)
            {
                switch (kv.Value.kind)
                {
                    case PrefKind.Absent: PlayerPrefs.DeleteKey(kv.Key); break;
                    case PrefKind.String: PlayerPrefs.SetString(kv.Key, kv.Value.s); break;
                    case PrefKind.Int: PlayerPrefs.SetInt(kv.Key, kv.Value.i); break;
                    case PrefKind.Float: PlayerPrefs.SetFloat(kv.Key, kv.Value.f); break;
                }
            }
            PlayerPrefs.Save();
            int files = 0;
            foreach (KeyValuePair<string, byte[]> kv in saveFileSnapshot)
            {
                try
                {
                    if (kv.Value != null) File.WriteAllBytes(kv.Key, kv.Value);
                    else if (File.Exists(kv.Key)) File.Delete(kv.Key);
                    files++;
                }
                catch (Exception e) { Log("세이브 원복 실패(" + kv.Key + "): " + e.Message); }
            }
            Log("걸음 전 상태로 되돌림 — 키 " + prefsSnapshot.Count + "개, 세이브 파일 " + files + "개");
            prefsSnapshot = null;
            saveFileSnapshot = null;
        }

        private static void Region(string site, string regionId)
        {
            steps.Add(new Step
            {
                site = site,
                label = "리전 이동",
                act = () => MoveToRegion(regionId),
                until = () =>
                {
                    var rm = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
                    return rm != null && rm.CurrentRegion != null && rm.CurrentRegion.regionId == regionId;
                },
                maxTries = 3,
            });
        }

        /// <summary>선행 채우기 → 행위 → 발화 확인.</summary>
        private static void Beat(string site, string label, string beatId, Action act, int tries)
        {
            steps.Add(new Step
            {
                site = site,
                label = "선행 채우기(" + beatId + ")",
                act = () => SeedPrerequisites(beatId),
                until = () => true,
            });
            steps.Add(BeatStep(site, label, beatId, act, tries));
        }

        /// <summary>
        /// 서브에리어 비트 한 벌 — <b>근접 → 진입 → 이탈</b> 세 걸음이다.
        ///
        /// 진입은 <c>RequestEnterSubArea</c> 하나로 안 된다. 그 함수는 <c>nearbySubArea</c>가
        /// 차 있어야 하고, 그건 <c>RegionManager.Update</c>가 <b>위치로</b>만 채운다 —
        /// 플레이어가 걸어 들어가는 그 경로다. 그래서 먼저 좌표를 옮기고 한 프레임 기다린다.
        ///
        /// <b>이탈을 빠뜨리면 그 뒤 걸음이 전부 죽는다.</b> 진입이 sticky를 켜고 플레이어를
        /// (2000,0,2000)으로 옮기는데, sticky 동안 <c>Update</c>가 위치 판정을 통째로 건너뛰어
        /// 다음 <c>리전 이동</c>이 영영 성립하지 않는다.
        /// </summary>
        private static void SubAreaBeat(string site, string subAreaId, string beatId, string label)
        {
            steps.Add(new Step
            {
                site = site,
                label = "선행 채우기(" + beatId + ")",
                act = () => SeedPrerequisites(beatId),
                until = () => true,
            });
            steps.Add(new Step
            {
                site = site,
                label = "서브에리어 근접(" + subAreaId + ")",
                act = () => MoveToSubArea(subAreaId),
                until = () =>
                {
                    var rm = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
                    return rm != null && rm.NearbySubArea != null
                        && rm.NearbySubArea.subAreaId == subAreaId;
                },
                maxTries = 3,
            });
            steps.Add(BeatStep(site, label, beatId, EnterNearbySubArea, 3));
            steps.Add(new Step
            {
                site = site,
                label = "서브에리어 이탈",
                act = ExitSubArea,
                until = () =>
                {
                    var rm = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
                    return rm != null && rm.CurrentSubArea == null && !rm.SubAreaSticky;
                },
                maxTries = 3,
            });
        }

        private static Step BeatStep(string site, string label, string beatId, Action act, int tries)
        {
            return new Step
            {
                site = site,
                label = label,
                expectBeat = beatId,
                act = act,
                until = () => firedLog.Contains(beatId),
                maxTries = tries,
            };
        }

        // ── 게임에 가하는 행위 (전부 실제 진입점) ───────────────────────
        private static void Talk(string npcId)
        {
            if (director == null) return;
            if (!director.OnNpcTalked(npcId)) Log("말 걸기 — 발화 없음 (" + npcId + ")");
        }

        private static void Capture(string insectId)
        {
            var col = UnityEngine.Object.FindFirstObjectByType<PlayerInsectCollection>();
            if (col == null || string.IsNullOrEmpty(insectId)) return;
            col.AddCapturedInsect(insectId, 5);   // InsectCaptured 이벤트 → CaptureInsect 트리거
        }

        /// <summary>
        /// 전투 승리를 실제 구독 경로로 흘린다. 이벤트는 밖에서 올릴 수 없어 대리자를 꺼내 부른다 —
        /// 그래서 <b>구독이 비어 있으면 여기서 잡힌다</b>(그 자체가 영구 미발화 결함이다).
        /// </summary>
        private static void WinBattle() { WinBattleAgainst(null); }

        /// <summary>
        /// <paramref name="insectId"/>를 이긴 것으로 흘린다. null이면 상대를 지정하지 않는다
        /// (무param <c>BattleWin</c> 비트만 자격을 갖는다).
        ///
        /// <c>BattleWin</c>이 종 지정을 받게 되면서 필요해졌다 — <c>StoryDirector.OnBattleEnded</c>가
        /// <c>InsectBattleController.EnemyInsectId</c>를 읽어 큐에 싣기 때문에, 상대를 안 세우면
        /// <c>fin_seal</c>처럼 종을 문 비트는 **영영 안 뜬다**(그리고 그건 결함처럼 보인다).
        ///
        /// 전투 자체는 여기서도 시뮬레이션이다(이 도구가 늘 그랬듯). 다만 상대만은
        /// <b>실제 종 데이터</b>로 세워서, 스토리로 흘러가는 값이 진짜와 같아지게 한다.
        /// </summary>
        private static void WinBattleAgainst(string insectId)
        {
            var bc = UnityEngine.Object.FindFirstObjectByType<InsectGame.Battle.InsectBattleController>();
            if (bc == null) { Log("InsectBattleController 없음"); return; }

            if (!string.IsNullOrEmpty(insectId) && !SeatEnemy(bc, insectId)) return;

            FieldInfo f = typeof(InsectGame.Battle.InsectBattleController)
                .GetField("BattleEnded", BindingFlags.Instance | BindingFlags.NonPublic);
            var d = f != null ? f.GetValue(bc) as Action<bool> : null;
            if (d == null)
            {
                Log("**BattleEnded 구독자가 없다** — BattleWin 비트가 영영 발화하지 않는다");
                return;
            }
            d.Invoke(true);
            // 결과 화면이 닫혔다고 알린다 — 안 알리면 미뤄 둔 트리거가 12초를 기다린다.
            if (director != null) director.NotifyBattlePresentationClosed();
        }

        /// <summary>
        /// <b>레이드</b> 승리를 흘린다. Epic·Legendary는 이 길밖에 없다 —
        /// <c>CaptureChoiceUI.IsRaidTarget</c>이 그 두 등급에 포획과 1v1을 둘 다 막는다.
        ///
        /// 그래서 <c>fin_seal</c>(<c>BattleWin mantis_unnamed</c>, Legendary)은 1v1로 걸으면
        /// <b>실제 게임에 없는 경로를 통과시키는 셈</b>이 된다. 저작이 도달 가능한지 보려면
        /// 플레이어가 실제로 쓸 수 있는 길로 걸어야 한다.
        /// </summary>
        private static void WinRaidAgainst(string insectId)
        {
            var rc = UnityEngine.Object.FindFirstObjectByType<InsectGame.Battle.RaidBattleController>();
            if (rc == null) { Log("RaidBattleController 없음"); return; }

            var db = UnityEngine.Object.FindFirstObjectByType<InsectDatabase>();
            InsectData data = db != null ? db.GetById(insectId) : null;
            if (data == null) { Log("**곤충 '" + insectId + "'를 DB에서 못 찾았다**"); return; }

            PropertyInfo p = typeof(InsectGame.Battle.RaidBattleController)
                .GetProperty("BossStats", BindingFlags.Instance | BindingFlags.Public);
            if (p == null || p.SetMethod == null)
            {
                Log("**BossStats 세터를 못 찾았다** — 이름이 바뀌었다면 여기도 고쳐야 한다");
                return;
            }
            p.SetMethod.Invoke(rc, new object[] { new InsectGame.Battle.InsectBattleStats(data, 70) });

            // **실제 레이드 승리는 포획도 함께 준다**(`OnRaidVictory`가 `AddCapturedInsect`를
            // 부른 **뒤** `RaidEnded`를 쏜다). 그 순서를 그대로 재현해야 "전투 안에서 잡은 포획"
            // 경로가 걸린다 — 포획만 따로 두면 `StoryDirector.LateUpdate`의 미루기 분기가
            // 한 번도 안 돌고, 그 상태로 통과하면 이 도구가 또 없는 경로를 검증하는 셈이다.
            Capture(insectId);

            FieldInfo f = typeof(InsectGame.Battle.RaidBattleController)
                .GetField("RaidEnded", BindingFlags.Instance | BindingFlags.NonPublic);
            var d = f != null ? f.GetValue(rc) as Action<bool> : null;
            if (d == null)
            {
                Log("**RaidEnded 구독자가 없다** — 레이드 승리가 스토리에 안 흐른다");
                return;
            }
            d.Invoke(true);
            if (director != null) director.NotifyBattlePresentationClosed();
        }

        /// <summary>
        /// 컨트롤러의 <c>enemyStats</c>에 실제 종 데이터를 앉힌다.
        ///
        /// <c>StartDuel</c>을 부르지 않는 이유: 그건 프리즈·전투 UI·보상 경로까지 켜서
        /// 걸음의 <c>Idle()</c> 판정과 다툰다. 여기서 검증하려는 건 전투가 아니라
        /// <b>이긴 종이 스토리까지 흘러가는가</b>이므로, 그 한 값만 진짜로 만든다.
        /// </summary>
        private static bool SeatEnemy(InsectGame.Battle.InsectBattleController bc, string insectId)
        {
            var db = UnityEngine.Object.FindFirstObjectByType<InsectDatabase>();
            InsectData data = db != null ? db.GetById(insectId) : null;
            if (data == null)
            {
                Log("**곤충 '" + insectId + "'를 DB에서 못 찾았다** — 종 지정 승리를 못 만든다");
                return false;
            }

            FieldInfo f = typeof(InsectGame.Battle.InsectBattleController)
                .GetField("enemyStats", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null)
            {
                Log("**enemyStats 필드를 못 찾았다** — 이름이 바뀌었다면 여기도 고쳐야 한다");
                return false;
            }
            f.SetValue(bc, new InsectGame.Battle.InsectBattleStats(data, 60));
            return true;
        }

        private static void Cleanse(string boss, string regionId)
        {
            var blight = UnityEngine.Object.FindFirstObjectByType<RegionBlightManager>();
            if (blight == null) { Log("RegionBlightManager 없음"); return; }
            bool ok = blight.CleanseByBoss(boss, regionId);
            Log("정화 " + (ok ? "성공" : "실패(이미 정화됐거나 보스·리전 불일치)") + " — " + regionId);
            if (director != null) director.NotifyBattlePresentationClosed();
        }

        private static void MoveToRegion(string regionId)
        {
            var rm = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
            GameObject player = GameObject.Find("Player");
            if (rm == null || player == null) return;
            RegionData r = rm.GetRegionById(regionId);
            if (r == null) { Log("리전 '" + regionId + "'를 못 찾았다"); return; }

            Vector3 p = r.centerPosition;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out hit, 600f))
                p.y = hit.point.y + 1.2f;
            else
                p.y = player.transform.position.y + 5f;
            player.transform.position = p;
        }

        /// <summary>서브에리어 중심으로 옮긴다 — 이게 <c>RegionManager.Update</c>의 근접 판정을 채운다.</summary>
        private static void MoveToSubArea(string subAreaId)
        {
            var rm = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
            GameObject player = GameObject.Find("Player");
            if (rm == null || rm.Regions == null || player == null) return;

            foreach (RegionData r in rm.Regions)
            {
                if (r == null || r.subAreas == null) continue;
                foreach (SubAreaData sub in r.subAreas)
                {
                    if (sub == null || sub.subAreaId != subAreaId) continue;
                    Vector3 p = sub.centerPosition;
                    RaycastHit hit;
                    if (Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out hit, 600f))
                        p.y = hit.point.y + 1.2f;
                    else
                        p.y = player.transform.position.y + 5f;
                    player.transform.position = p;
                    return;
                }
            }
            Log("서브에리어 '" + subAreaId + "'를 못 찾았다");
        }

        /// <summary>[E] 키에 해당한다 — 근접 상태에서만 성립하는 명시 진입.</summary>
        private static void EnterNearbySubArea()
        {
            var rm = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
            if (rm == null) { Log("RegionManager 없음"); return; }
            if (rm.NearbySubArea == null) { Log("근접한 서브에리어가 없다 — 진입 불가"); return; }
            rm.RequestEnterSubArea();
        }

        /// <summary>F2·퇴장 버튼과 같은 경로. 빌더가 월드를 되돌리고 플레이어를 원위치시킨다.</summary>
        private static void ExitSubArea()
        {
            var builder = UnityEngine.Object.FindFirstObjectByType<SubAreaWorldBuilder>();
            if (builder != null) { builder.RequestExit(); return; }

            // 빌더가 없는 구성(최소 씬)에서는 매니저를 직접 푼다.
            var rm = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
            if (rm != null) rm.ForceExitSubArea();
        }

        /// <summary>
        /// 수문장 격파 — <c>BattleScreenUI.CheckGuardianDefeat</c>가 부르는 그 함수다.
        /// <b>리전당 일생 1회</b>라(idempotent 가드) 이미 격파한 상태면 조용히 아무 일도 안 난다.
        /// 그래서 <see cref="ResetProgress"/>가 격파 기록도 함께 지운다.
        /// </summary>
        private static void Guardian(string regionId)
        {
            var rm = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
            if (rm == null) { Log("RegionManager 없음"); return; }
            if (rm.IsGuardianDefeated(regionId))
                Log("이미 격파된 수문장 — " + regionId + " (GuardianDefeated가 안 울린다)");
            rm.DefeatGuardian(regionId);
            if (director != null) director.NotifyBattlePresentationClosed();
        }

        /// <summary>
        /// 이 비트가 서려면 먼저 열람돼 있어야 하는 것들을 <b>의존 순서대로</b> 채운다.
        /// 여기서 검증하려는 건 <c>bl_*</c>이지 1막 82비트가 아니다 — 무엇을 채웠는지는
        /// 보고서에 그대로 적어 둔다.
        /// </summary>
        private static void SeedPrerequisites(string beatId)
        {
            if (director == null) return;
            StoryBeat target;
            if (!StoryService.TryGetBeat(beatId, out target)) return;

            var order = new List<string>();
            var visiting = new HashSet<string>();

            Action<string> walk = null;
            walk = id =>
            {
                if (string.IsNullOrEmpty(id) || !visiting.Add(id)) return;
                StoryBeat b;
                if (!StoryService.TryGetBeat(id, out b)) return;
                walk(b.prerequisiteBeatId);
                walk(b.requiredBeatId);
                if (!order.Contains(id)) order.Add(id);
            };

            walk(target.prerequisiteBeatId);
            walk(target.requiredBeatId);

            foreach (string id in order)
            {
                if (director.HasSeen(id)) continue;
                director.CompleteBeat(id);
                if (!seeded.Contains(id)) seeded.Add(id);
            }
            Log("선행 " + order.Count + "개 채움 — " + string.Join(", ", order.ToArray()));
        }

        private static void ResetProgress()
        {
            SnapshotPlayerState();   // 지우기 전에 뜬다 — 두 번째 호출부터는 아무것도 안 한다
            try
            {
                string path = SaveScope.FilePath(GameConstants.SaveFiles.StoryProgress);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e) { Log("스토리 세이브 삭제 실패: " + e.Message); }

            string key = SaveScope.PrefsKey(GameConstants.PrefsKeys.BlightCleansed);
            PlayerPrefs.DeleteKey(key);

            // 수문장 격파 기록 — **본편 걸음에는 이게 꼭 필요하다.** DefeatGuardian은 idempotent라
            // 이미 격파된 리전에서는 GuardianDefeated가 아예 안 울리고, 그러면 gd_* 비트가
            // "발화 없음"으로 잡혀 결함처럼 보인다(실제로는 두 번째 실행이라서다).
            // 키 문자열은 RegionManager.GuardianKey가 private이라 여기 한 번 더 적는다 —
            // 어긋나면 초기화가 조용히 아무것도 안 지운다.
            PlayerPrefs.DeleteKey(SaveScope.PrefsKey("InsectGame.DefeatedGuardians"));
            // 배지 이정표 수령 — 격파 기록을 지웠으니 함께 지워야 이정표 지급(4·8)이 다시 걸린다.
            PlayerPrefs.DeleteKey(SaveScope.PrefsKey(GameConstants.PrefsKeys.BadgeMilestonesClaimed));
            PlayerPrefs.Save();

            if (director != null)
            {
                director.ReloadFromDisk();
                director.StoryBeatTriggered += OnBeatFired;   // 발화 기록 구독
            }
            var blight = UnityEngine.Object.FindFirstObjectByType<RegionBlightManager>();
            if (blight != null) blight.ReloadFromDisk();
            var regions = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
            if (regions != null) regions.ReloadFromDisk();
            var badgeService = UnityEngine.Object.FindFirstObjectByType<GuardianBadgeService>();
            if (badgeService != null) badgeService.ReloadFromDisk();
            Log("진행 기록 초기화 완료");
        }

        private static void OnBeatFired(StoryBeat beat)
        {
            if (beat == null) return;
            firedLog.Add(beat.beatId);
            toClose.Add(beat.beatId);
            Log("발화 — " + beat.beatId);
        }

        // ──────────────────────────────────────────────────────────────
        private static void Finish(string error)
        {
            EditorApplication.update -= Tick;
            if (director != null) director.StoryBeatTriggered -= OnBeatFired;
            SessionState.SetString(StageKey, "");
            RestorePlayerState();

            bool allPassed = true;
            var sb = new StringBuilder();
            sb.AppendLine(IsCampaign
                ? "# 1막 본편 + 꽃밭 스토리 비트 — 실제 발화 걸음"
                : IsTown ? "# 마을 이야기 + 지역 의뢰 + 따라가기 — 실제 발화 걸음"
                : "# 오염 거점 스토리 비트 — 실제 발화 걸음");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(error))
            {
                sb.AppendLine("> **중단**: " + error);
                sb.AppendLine();
            }
            sb.AppendLine((IsCampaign ? "본편" : IsTown ? "마을 이야기" : "거점 " + SiteCount() + "개")
                + " · 걸음 " + (steps != null ? steps.Count : 0) + "개");
            sb.AppendLine();
            sb.AppendLine("| 구역 | 걸음 | 기대 비트 | 시도 | 결과 |");
            sb.AppendLine("|---|---|---|---|---|");
            if (steps != null)
            {
                foreach (Step s in steps)
                {
                    if (!s.started || !s.passed) allPassed = false;
                    string res = !s.started ? "미실행" : (s.passed ? "OK" : "**FAIL**");
                    sb.AppendLine("| " + s.site + " | " + s.label + " | "
                        + (string.IsNullOrEmpty(s.expectBeat) ? "-" : "`" + s.expectBeat + "`") + " | "
                        + (s.started ? s.tries.ToString() : "-") + " | " + res + " |");
                }
            }
            sb.AppendLine();
            sb.AppendLine("## 발화 순서(실측)");
            sb.AppendLine();
            for (int i = 0; i < firedLog.Count; i++)
                sb.AppendLine((i + 1) + ". `" + firedLog[i] + "`");
            sb.AppendLine();
            sb.AppendLine("## 미리 채운 선행 비트 (검증 대상 아님)");
            sb.AppendLine();
            sb.AppendLine(seeded.Count == 0 ? "(없음)" : "`" + string.Join("`, `", seeded.ToArray()) + "`");

            bool success = allPassed && string.IsNullOrEmpty(error);
            sb.AppendLine();
            sb.AppendLine("결과: **" + (success ? "PASS" : "FAIL") + "**");

            try
            {
                string root = Path.GetDirectoryName(Application.dataPath);
                string full = Path.GetFullPath(Path.Combine(root ?? ".", outPath));
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
                Log("보고서 → " + full);
            }
            catch (Exception e) { Log("보고서 저장 실패: " + e.Message); }

            Log(success ? "전 걸음 통과" : "실패한 걸음이 있다");
            EditorApplication.Exit(success ? 0 : 1);
        }

        private static string ReadArg(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return fallback;
        }

        private static void Log(string msg)
        {
            Debug.Log("[WALK] " + msg);
        }
    }
}
#endif
