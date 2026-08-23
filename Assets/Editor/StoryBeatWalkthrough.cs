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
    /// 오염 거점 스토리 비트를 <b>실제로 발화시켜</b> 순서대로 확인하는 배치모드 도구.
    ///
    /// <code>
    /// Unity.exe -batchmode -projectPath &lt;proj&gt; -logFile &lt;log&gt; \
    ///   -executeMethod InsectGame.EditorTools.StoryBeatWalkthrough.Run \
    ///   -walkOut .claude/cache/story-walk.md [-walkRegion mountain]
    /// </code>
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

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                DefaultScene, UnityEditor.SceneManagement.OpenSceneMode.Single);

            SessionState.SetString(StageKey, outPath + "|" + onlyRegion);
            Log("scene=" + DefaultScene + " out=" + outPath
                + " region=" + (onlyRegion == "" ? "(전부)" : onlyRegion));
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
                BuildSteps();
                if (steps.Count == 0) { Finish("걸어볼 거점이 없다"); return; }
                Log("거점 " + SiteCount() + "개 · 걸음 " + steps.Count + "개 시작");
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
                ui.CloseModal();          // 플레이어가 닫는 경로 — 보상·열람 기록이 여기서 난다
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
        private static void WinBattle()
        {
            var bc = UnityEngine.Object.FindFirstObjectByType<InsectGame.Battle.InsectBattleController>();
            if (bc == null) { Log("InsectBattleController 없음"); return; }

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
            try
            {
                string path = SaveScope.FilePath(GameConstants.SaveFiles.StoryProgress);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e) { Log("스토리 세이브 삭제 실패: " + e.Message); }

            string key = SaveScope.PrefsKey(GameConstants.PrefsKeys.BlightCleansed);
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();

            if (director != null)
            {
                director.ReloadFromDisk();
                director.StoryBeatTriggered += OnBeatFired;   // 발화 기록 구독
            }
            var blight = UnityEngine.Object.FindFirstObjectByType<RegionBlightManager>();
            if (blight != null) blight.ReloadFromDisk();
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

            bool allPassed = true;
            var sb = new StringBuilder();
            sb.AppendLine("# 오염 거점 스토리 비트 — 실제 발화 걸음");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(error))
            {
                sb.AppendLine("> **중단**: " + error);
                sb.AppendLine();
            }
            sb.AppendLine("거점 " + SiteCount() + "개 · 걸음 " + (steps != null ? steps.Count : 0) + "개");
            sb.AppendLine();
            sb.AppendLine("| 거점 | 걸음 | 기대 비트 | 시도 | 결과 |");
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
