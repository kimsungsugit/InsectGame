#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Text;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Story;
using UnityEditor;
using UnityEngine;

namespace InsectGame.EditorTools
{
    /// <summary>
    /// 첫 파트너 곤충 선택이 <b>실제로 지급까지 이어지는지</b> 배치모드로 확인한다.
    ///
    /// 왜 따로 필요한가 — 이 경로는 기존 검증 수단 어느 것도 닿지 않는다:
    /// - <c>StarterInsectTests</c>는 <c>ResolveChoice</c>의 <b>순수 계산부</b>만 본다.
    ///   PlayerPrefs를 읽어 화이트리스트로 거르는 것까지는 확인하지만, 그 결과가
    ///   <c>PlayerInsectCollection</c>과 도감에 들어가는지는 모른다.
    /// - <c>StoryBeatWalkthrough</c>는 <c>ch1_intro</c>를 <b>선행 비트로 미리 채운다</b>
    ///   (검증 대상이 그 뒤의 비트들이라서다). 그래서 이 비트가 실제로 발화하는 걸 아무도 안 걸었다.
    /// - <c>story_lint</c>는 Story.json을 정적으로 읽어 참조 무결성만 본다.
    ///
    /// 즉 선택이 통째로 무시돼도 <b>예외도 경고도 없이</b> 장수풍뎅이가 들어올 뿐이라
    /// 화면만 봐서는 "고른 대로 됐다"와 구분되지 않는다.
    ///
    /// <code>
    /// "$UNITY_EDITOR_PATH" -batchmode -projectPath "X:/" \
    ///   -executeMethod InsectGame.EditorTools.StarterInsectProbe.Run \
    ///   -probeOut .claude/cache/starter-probe.md -logFile .claude/cache/starter-probe.log
    /// </code>
    ///
    /// 종료 코드는 <b>모든 선택지가 정확히 그 종으로 지급됐을 때만</b> 0이다.
    ///
    /// <b>⚠ 연속 모드(인자 없이 전체 실행)의 결과는 첫 케이스만 신뢰할 수 있다.</b>
    /// <c>ResetAll</c>이 세이브를 지우고 <c>ReloadFromDisk</c>를 불러도 <c>StoryDirector</c>의
    /// 인메모리 상태(미뤄 둔 트리거·<c>pendingBeatId</c>)까지는 되돌리지 못해, 두 번째부터는
    /// 비트가 발화(<c>OnNpcTalked</c>=true)하고도 <b>완료 처리가 안 되는</b> 경우가 있다.
    /// 실측: 4케이스 연속 실행에서 1·3번째가 "비트완료=False"로 실패했는데,
    /// 같은 종을 <c>-probeOnly</c>로 단독 실행하면 <b>PASS</b>였다.
    ///
    /// 실제 플레이어는 <c>ch1_intro</c>를 일생 한 번만 겪으므로 이건 게임 결함이 아니라
    /// 프로브의 인공물이다. <b>정확한 검증은 종마다 프로세스를 나눠 단독 실행하는 것</b>이다:
    /// <code>
    /// for id in rhinoceros_beetle cicada_evening butterfly_swallowtail default; do
    ///   "$UNITY_EDITOR_PATH" -batchmode -projectPath "X:/"     ///     -executeMethod InsectGame.EditorTools.StarterInsectProbe.Run     ///     -probeOnly $id -probeOut ".claude/cache/probe-$id.md" -logFile ".claude/cache/probe-$id.log"
    /// done
    /// </code>
    /// </summary>
    public static class StarterInsectProbe
    {
        private const string ScenePath = "Assets/Scenes/PlayScene.unity";
        private static readonly StringBuilder Report = new StringBuilder();
        private static string outPath = ".claude/cache/starter-probe.md";
        private static int failures;

        /// <summary>
        /// 실제로 확인한 케이스 수. <b>0이면 FAIL이다</b> —
        /// 도구가 아무것도 안 하고 "PASS"를 내는 게 가장 나쁜 결과다
        /// (실제로 중첩 코루틴이 안 펼쳐져 그 상태였다).
        /// <c>testing.md</c>의 "0건 보고는 통과가 아니라 실패다"와 같은 규율.
        /// </summary>
        private static int probedCases;

        /// <summary>`-probeOnly &lt;insectId&gt;` — 지정하면 그 종 하나만 검증한다.</summary>
        private static string onlyId;

        /// <summary>
        /// <c>SessionState</c>로 넘기는 이유: <c>EnterPlaymode()</c>는 <b>도메인 리로드</b>를
        /// 일으켜 static 필드와 <c>EditorApplication.update</c> 구독이 전부 날아간다.
        /// 진입 전에 등록해 둔 코루틴은 플레이모드에서 존재하지 않는다 —
        /// 그래서 <c>LiveSceneCapture</c>도 같은 구조(<c>SessionState</c> + <c>InitializeOnLoadMethod</c>)를 쓴다.
        /// 이 구조 없이 만들었더니 프로브가 영원히 대기만 했다.
        /// </summary>
        private const string StageKey = "InsectGame.StarterInsectProbe.Stage";

        public static void Run()
        {
            ParseArgs();

            // 빈 씬으로 들어가면 부트스트랩이 아예 안 돈다 — 실제 게임 씬을 연 뒤 플레이모드로.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            SessionState.SetString(StageKey, outPath);
            SessionState.SetString(StageKey + ".only", onlyId ?? "");
            Log("scene=" + ScenePath + " out=" + outPath);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            string stage = SessionState.GetString(StageKey, "");
            if (string.IsNullOrEmpty(stage)) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;

            SessionState.EraseString(StageKey);   // 재진입 방지
            outPath = stage;
            onlyId = SessionState.GetString(StageKey + ".only", "");
            SessionState.EraseString(StageKey + ".only");
            failures = 0;
            Report.Length = 0;
            probedCases = 0;
            stack.Clear();
            stack.Push(Drive());
            EditorApplication.update += Pump;
        }

        /// <summary>
        /// 실행 중인 코루틴 스택.
        ///
        /// <b>수동 <c>MoveNext()</c>는 중첩 코루틴을 펼치지 않는다</b> — Unity의
        /// <c>StartCoroutine</c>만 <c>yield return IEnumerator</c>를 알아서 파고든다.
        /// 처음에 단일 <c>IEnumerator</c> 필드로 만들었더니 <c>yield return ProbeOne(...)</c>이
        /// 그냥 객체를 반환하고 넘어가, <b>검증을 한 건도 안 하고 PASS를 보고했다</b>.
        /// 그래서 스택으로 직접 펼친다.
        /// </summary>
        private static readonly System.Collections.Generic.Stack<IEnumerator> stack =
            new System.Collections.Generic.Stack<IEnumerator>();

        private static void Pump()
        {
            if (stack.Count == 0) return;
            try
            {
                IEnumerator top = stack.Peek();
                if (!top.MoveNext())
                {
                    stack.Pop();
                    if (stack.Count == 0)
                    {
                        EditorApplication.update -= Pump;
                        Finish();
                    }
                    return;
                }

                if (top.Current is IEnumerator nested) stack.Push(nested);
            }
            catch (Exception e)
            {
                Log("예외: " + e);
                failures++;
                stack.Clear();
                EditorApplication.update -= Pump;
                Finish();
            }
        }

        private static IEnumerator Drive()
        {
            // 부트스트랩이 전 시스템을 세울 때까지 기다린다(LiveSceneCapture와 같은 방식).
            float waited = 0f;
            while (!Bootstrapped() && waited < 25f)
            {
                waited += 0.1f;
                yield return null;
            }

            if (!Bootstrapped())
            {
                Log("부트스트랩 실패 — 25초 안에 StoryDirector/PlayerInsectCollection이 서지 않았다");
                failures++;
                yield break;
            }

            Report.AppendLine("# 첫 파트너 지급 경로 — 실제 발화 확인");
            Report.AppendLine();
            Report.AppendLine("| 고른 종 | 비트 발화 | 실제 지급 | 도감 등록 | 결과 |");
            Report.AppendLine("|---|---|---|---|---|");

            // `-probeOnly <insectId>`면 그 하나만 본다.
            // 반복 실행에서 StoryDirector 인메모리 상태가 남아 뒤쪽 케이스가 흔들릴 수 있는데,
            // 그게 프로브의 인공물인지 진짜 결함인지는 **단독 실행**으로만 가를 수 있다.
            if (!string.IsNullOrEmpty(onlyId))
            {
                Log("단독 모드 — " + onlyId);
                if (onlyId == "default") yield return ProbeDefault();
                else yield return ProbeOne(StarterInsectCatalog.Get(StarterInsectCatalog.IndexOf(onlyId)));
                yield break;
            }

            for (int i = 0; i < StarterInsectCatalog.Count; i++)
            {
                StarterInsectCatalog.Choice choice = StarterInsectCatalog.Get(i);
                yield return ProbeOne(choice);
            }

            // 선택이 없는 기존 세이브 — 오늘과 같은 동작(기본값)이어야 한다.
            yield return ProbeDefault();
        }

        private static IEnumerator ProbeOne(StarterInsectCatalog.Choice choice)
        {
            ResetAll();
            StarterInsectCatalog.SaveChoice(choice.InsectId);
            PlayerPrefs.Save();

            yield return null;

            bool fired = false;
            yield return FireIntroBeat(v => fired = v);
            yield return null;
            yield return null;   // 지급은 GrantReward 안에서 동기지만 이벤트 한 프레임 여유

            bool owned = OwnsSpecies(choice.InsectId);
            bool dexed = DexHas(choice.InsectId);
            bool wrongSpecies = !owned && OwnsSpecies(StarterInsectCatalog.DefaultId)
                                && choice.InsectId != StarterInsectCatalog.DefaultId;

            probedCases++;
            bool ok = fired && owned && dexed;
            if (!ok) failures++;

            Report.AppendLine(string.Format("| `{0}` | {1} | {2} | {3} | {4} |",
                choice.InsectId,
                fired ? "OK" : "**없음**",
                owned ? "OK" : (wrongSpecies ? "**기본값이 들어옴**" : "**없음**"),
                dexed ? "OK" : "**없음**",
                ok ? "OK" : "**FAIL**"));

            Log(string.Format("{0} — 발화={1} 보유={2} 도감={3} | 실제 보유목록=[{4}] | 비트완료={5} | 저장된선택='{6}'",
                choice.InsectId, fired, owned, dexed, OwnedList(), BeatSeen(), SavedChoice()));
        }

        /// <summary>선택 키가 없을 때. 기존 세이브가 오늘과 똑같이 동작해야 한다.</summary>
        private static IEnumerator ProbeDefault()
        {
            ResetAll();
            PlayerPrefs.DeleteKey(SaveScope.PrefsKey(StarterInsectCatalog.PrefsKeyBase));
            PlayerPrefs.Save();

            yield return null;
            bool fired = false;
            yield return FireIntroBeat(v => fired = v);
            yield return null;
            yield return null;

            probedCases++;
            bool owned = OwnsSpecies(StarterInsectCatalog.DefaultId);
            bool ok = fired && owned;
            if (!ok) failures++;

            Report.AppendLine(string.Format("| _(선택 없음)_ | {0} | {1} | — | {2} |",
                fired ? "OK" : "**없음**",
                owned ? "기본값 OK" : "**기본값 아님**",
                ok ? "OK" : "**FAIL**"));

            Log("선택 없음 — 발화=" + fired + " 기본값 보유=" + owned
                + " | 실제 보유목록=[" + OwnedList() + "] | 비트완료=" + BeatSeen()
                + " | 저장된선택='" + SavedChoice() + "'");
        }

        // ── 게임의 실제 진입점을 두드린다 ──

        /// <summary>
        /// <c>ch1_intro</c>는 <c>NpcTalk village_elder</c> 트리거다 —
        /// <c>WorldInteractionController</c>가 부르는 그 메서드를 그대로 쓴다.
        ///
        /// <b>보상은 발화가 아니라 대사창을 닫을 때 난다</b>(플레이어가 실제로 지나는 경로다).
        /// 게다가 창은 즉시 열리지 않으므로 기다렸다 닫는다 —
        /// <c>StoryBeatWalkthrough</c>가 같은 이유로 같은 대기를 둔다.
        /// 끝내 안 열리면 렌더러 미배선이므로 <c>CompleteBeat</c>로 진행만 잇고 그 사실을 남긴다.
        /// </summary>
        private static IEnumerator FireIntroBeat(Action<bool> onFired)
        {
            StoryDirector d = UnityEngine.Object.FindFirstObjectByType<StoryDirector>();
            if (d == null) { onFired(false); yield break; }

            if (!d.OnNpcTalked("village_elder")) { onFired(false); yield break; }

            float waited = 0f;
            while (waited < 3f)
            {
                var ui = UnityEngine.Object.FindFirstObjectByType<InsectGame.UI.NpcDialogueUI>();
                if (ui != null && ui.IsOpen)
                {
                    ui.CloseModal();      // 보상·열람 기록이 여기서 난다
                    onFired(true);
                    yield break;
                }
                waited += 0.05f;
                yield return null;
            }

            Log("대사창이 안 열려 CompleteBeat로 직접 완료 — 렌더러 미배선일 수 있다");
            d.CompleteBeat("ch1_intro");
            onFired(true);
        }

        // ── 상태 확인 ──

        private static bool OwnsSpecies(string insectId)
        {
            PlayerInsectCollection col = UnityEngine.Object.FindFirstObjectByType<PlayerInsectCollection>();
            if (col == null) return false;
            foreach (PlayerInsectData d in col.GetAllOwned())
            {
                if (d != null && d.insectId == insectId) return true;
            }
            return false;
        }

        private static bool DexHas(string insectId)
        {
            InsectGame.Dex.DexController dex =
                UnityEngine.Object.FindFirstObjectByType<InsectGame.Dex.DexController>();
            if (dex == null) return false;
            // HasRecord = capturedCount > 0. IsDiscovered는 '봤다'까지 포함이라 지급 확인엔 약하다.
            return dex.HasRecord(insectId);
        }

        /// <summary>보유 곤충 id 전체 — 무엇이 들어왔는지 봐야 원인이 좁혀진다.</summary>
        private static string OwnedList()
        {
            PlayerInsectCollection col = UnityEngine.Object.FindFirstObjectByType<PlayerInsectCollection>();
            if (col == null) return "(collection null)";
            var sb = new StringBuilder();
            foreach (PlayerInsectData d in col.GetAllOwned())
            {
                if (d == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(d.insectId);
            }
            return sb.Length == 0 ? "비어있음" : sb.ToString();
        }

        /// <summary>비트가 완료 처리됐는가 — 발화만 하고 완료가 안 되면 보상도 안 난다.</summary>
        private static string BeatSeen()
        {
            StoryDirector d = UnityEngine.Object.FindFirstObjectByType<StoryDirector>();
            if (d == null) return "(director null)";
            return d.HasSeen("ch1_intro").ToString();
        }

        private static string SavedChoice()
        {
            return PlayerPrefs.GetString(SaveScope.PrefsKey(StarterInsectCatalog.PrefsKeyBase), "(없음)");
        }

        // ── 초기화 ──

        /// <summary>
        /// 스토리 진행·보유 곤충·도감을 지워 매번 같은 출발선에서 시작한다.
        /// 안 지우면 두 번째 선택부터 <c>IsSeen</c>에 막혀 비트가 발화하지 않는다.
        /// </summary>
        private static void ResetAll()
        {
            TryDelete(GameConstants.SaveFiles.StoryProgress);
            TryDelete(GameConstants.SaveFiles.PlayerInsects);
            TryDelete(GameConstants.SaveFiles.DexSave);
            PlayerPrefs.Save();

            var d = UnityEngine.Object.FindFirstObjectByType<StoryDirector>();
            if (d != null) d.ReloadFromDisk();
            var col = UnityEngine.Object.FindFirstObjectByType<PlayerInsectCollection>();
            if (col != null) col.ReloadFromDisk();
            var dex = UnityEngine.Object.FindFirstObjectByType<InsectGame.Dex.DexController>();
            if (dex != null) dex.ReloadFromDisk();
        }

        private static void TryDelete(string saveFile)
        {
            try
            {
                string p = SaveScope.FilePath(saveFile);
                if (File.Exists(p)) File.Delete(p);
            }
            catch (Exception e) { Log("세이브 삭제 실패(" + saveFile + "): " + e.Message); }
        }

        private static bool Bootstrapped()
        {
            return UnityEngine.Object.FindFirstObjectByType<StoryDirector>() != null
                && UnityEngine.Object.FindFirstObjectByType<PlayerInsectCollection>() != null
                && UnityEngine.Object.FindFirstObjectByType<InsectGame.Dex.DexController>() != null;
        }

        // ── 출력 ──

        private static void ParseArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-probeOut") outPath = args[i + 1];
                if (args[i] == "-probeOnly") onlyId = args[i + 1];
            }
        }

        private static void Log(string msg)
        {
            Debug.Log("[STARTER-PROBE] " + msg);
        }

        private static void Finish()
        {
            if (string.IsNullOrEmpty(onlyId) && probedCases > 1)
            {
                Report.AppendLine();
                Report.AppendLine("> ⚠ **연속 모드는 첫 케이스만 신뢰할 수 있다.** `ResetAll`이 StoryDirector의");
                Report.AppendLine("> 인메모리 상태까지 되돌리지 못해 두 번째부터 비트 완료가 흔들린다.");
                Report.AppendLine("> 실패가 보이면 `-probeOnly <id>`로 단독 실행해 다시 확인할 것.");
            }

            if (probedCases == 0)
            {
                failures++;
                Report.AppendLine();
                Report.AppendLine("> **확인한 케이스가 0건이다 — 프로브 자체가 돌지 않았다.**");
                Report.AppendLine("> 표가 비었는데 PASS를 내면 검증이 있다고 착각하게 된다.");
            }

            Report.AppendLine();
            Report.AppendLine(failures == 0
                ? "결과: **PASS** (" + probedCases + "건 확인)"
                : "결과: **FAIL** (" + failures + "건 / 확인 " + probedCases + "건)");

            try
            {
                string dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(outPath, Report.ToString(), new UTF8Encoding(false));
                Log("보고서 → " + outPath);
            }
            catch (Exception e) { Log("보고서 쓰기 실패: " + e.Message); }

            EditorApplication.ExitPlaymode();
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
    }
}
#endif
