#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.NPC;
using UnityEditor;
using UnityEngine;

namespace InsectGame.EditorTools
{
    /// <summary>
    /// 「장부」 압박이 <b>실제 보스전에서 도는지</b> 배치모드로 확인하는 도구.
    ///
    /// <code>
    /// Unity.exe -batchmode -projectPath &lt;proj&gt; -logFile &lt;log&gt; \
    ///   -executeMethod InsectGame.EditorTools.LedgerDuelProbe.Run \
    ///   -probeOut .claude/cache/ledger-probe.md [-probeBoss ledger_chief]
    /// </code>
    ///
    /// <b>왜 테스트로 안 되나.</b> <see cref="LedgerPressure"/>는 순수부라 EditMode 테스트가
    /// 이미 고정한다. 하지만 그 규칙이 <b>전투에 연결됐는지</b>는 다른 문제다 —
    /// <c>ArmLedger</c>를 안 부르면 임계가 0이라 게이지도 안 뜨고 정독도 안 터지는데,
    /// 예외도 경고도 없이 그냥 <b>평범한 전투가 된다.</b> 이 저장소가 이번 세션에만
    /// 같은 형태(배선 누락 = 무증상)를 세 번 겪었다.
    ///
    /// 그래서 실제 경로를 그대로 탄다: <c>NpcDuelController.TryStartBossDuel</c>로 대결을
    /// 열고, <c>UseBasicAttack</c>을 연타해(이 압박이 겨냥하는 바로 그 패턴) 매 턴
    /// 장부 값과 플레이어 피해를 기록한다. 확인하는 것 넷:
    /// <list type="number">
    ///   <item>임계가 표 값으로 걸렸는가(<c>ArmLedger</c> 배선)</item>
    ///   <item>연타에 장부가 차는가(<c>NoteLedgerAction</c> 배선)</item>
    ///   <item>임계에서 터지고 0으로 돌아가는가(톱니)</item>
    ///   <item>터진 턴의 피해가 <b>실제로 더 큰가</b>(<c>GetDamage</c> 배율)</item>
    /// </list>
    /// </summary>
    public static class LedgerDuelProbe
    {
        private const string StageKey = "InsectGame.LedgerDuelProbe.Stage";
        private const string DefaultScene = "Assets/Scenes/PlayScene.unity";
        private const string DefaultOut = ".claude/cache/ledger-probe.md";
        private const string DefaultBoss = "ledger_chief";

        /// <summary>부트스트랩 대기(초).</summary>
        private const float BootSeconds = 6f;
        /// <summary>탈출구(초).</summary>
        private const float HardTimeoutSeconds = 180f;
        /// <summary>연타할 턴 수 — 임계 4~9를 여러 주기 돌기에 넉넉하다.</summary>
        private const int Turns = 24;
        /// <summary>플레이어 곤충 레벨 — 관장(Lv.72)에게 몇 턴은 버텨야 표본이 모인다.</summary>
        private const int DefaultProbeLevel = 60;
        private static int probeLevel = DefaultProbeLevel;

        private static string outPath;
        private static string bossId;
        private static float startTime;
        private static float bootDeadline;
        private static readonly List<string> rows = new List<string>();
        private static readonly List<string> notes = new List<string>();

        [MenuItem("InsectGame/오염 거점/장부 압박 실전 확인 (배치모드 전용)", false, 401)]
        public static void Run()
        {
            outPath = ReadArg("-probeOut", DefaultOut);
            bossId = ReadArg("-probeBoss", DefaultBoss);
            probeLevel = ReadInt("-probeLevel", DefaultProbeLevel);

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                DefaultScene, UnityEditor.SceneManagement.OpenSceneMode.Single);
            SessionState.SetString(StageKey, outPath + "|" + bossId + "|" + probeLevel);
            Log("scene=" + DefaultScene + " boss=" + bossId + " out=" + outPath);
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
            bossId = parts.Length > 1 ? parts[1] : DefaultBoss;
            probeLevel = parts.Length > 2 && int.TryParse(parts[2], out int lv) ? lv : DefaultProbeLevel;

            startTime = Time.realtimeSinceStartup;
            bootDeadline = startTime + BootSeconds;
            rows.Clear();
            notes.Clear();
            readTaken.Clear();
            plainTaken.Clear();
            EditorApplication.update += Tick;
            Log("플레이모드 진입 — 부트스트랩 대기");
        }

        private static void Tick()
        {
            float now = Time.realtimeSinceStartup;
            if (now - startTime > HardTimeoutSeconds) { Finish("하드 타임아웃"); return; }
            if (now < bootDeadline) return;

            var duels = UnityEngine.Object.FindFirstObjectByType<NpcDuelController>();
            var battle = UnityEngine.Object.FindFirstObjectByType<InsectBattleController>();
            var collection = UnityEngine.Object.FindFirstObjectByType<PlayerInsectCollection>();
            if (duels == null || battle == null || collection == null)
            {
                bootDeadline = now + 1f;
                return;
            }

            EditorApplication.update -= Tick;   // 이 아래는 한 번에 끝난다(전투는 동기 호출이다)
            try { Probe(duels, battle, collection); }
            catch (Exception e) { notes.Add("예외: " + e.Message); }
            Finish(null);
        }

        private static void Probe(NpcDuelController duels, InsectBattleController battle,
                                  PlayerInsectCollection collection)
        {
            if (!NpcBossDuels.TryGet(bossId, out NpcBossDuels.BossDuel duel))
            {
                notes.Add("보스 '" + bossId + "'가 대결 표에 없다");
                return;
            }
            notes.Add("표의 임계: **" + duel.ledgerThreshold + "** (" + duel.displayName + " Lv." + duel.level + ")");

            // 대결에는 살아 있는 곤충이 하나 필요하다 — 실제 지급 경로로 넣는다.
            collection.AddCapturedInsect(duel.insectId, probeLevel);
            notes.Add("프로브 곤충: " + duel.insectId + " Lv." + probeLevel);

            latestPlayer = null;
            probedBattle = battle;
            battle.BattleUpdated += OnBattleUpdated;
            if (!duels.TryStartBossDuel(bossId, Time.time))
            {
                notes.Add("**대결이 안 열렸다** — CanBossDuel이 false(리더 없음/쿨다운/데이터)");
                return;
            }

            int armed = battle.LedgerThreshold;
            notes.Add("전투에 걸린 임계: **" + armed + "**"
                      + (armed == duel.ledgerThreshold ? " — 표와 일치" : " — **표와 다르다(ArmLedger 배선 이상)**"));
            if (!LedgerPressure.IsActive(armed))
            {
                notes.Add("**장부가 안 걸렸다** — ArmLedger 미호출이면 증상이 없다(그냥 평범한 전투가 된다)");
                return;
            }

            // 같은 행동(기본공격)만 되풀이한다 — 이 압박이 겨냥하는 바로 그 패턴이다.
            // **HP가 넉넉해야 표본이 모인다.** 관장은 Lv.72이고 프로브 곤충은 Lv.60이라
            // 서너 대면 쓰러진다 — 맞을 때마다 되살려 24턴을 채운다(피해 관측이 목적이다).
            for (int turn = 1; turn <= Turns; turn++)
            {
                if (!battle.IsBattleInProgress())
                {
                    notes.Add("턴 " + turn + "에서 전투가 끝났다(한쪽이 쓰러짐) — 표본은 여기까지");
                    break;
                }

                TopUp();   // **턴 시작에** 채운다 — 아래 주석 참조
                int before = battle.LedgerTally;
                int readsBefore = battle.LedgerReadCount;
                bool warned = LedgerPressure.IsWarning(before, armed);

                float hpBefore = PlayerHp();
                float enemyBefore = EnemyHp();
                battle.UseBasicAttack();
                bool fired = battle.LedgerReadCount > readsBefore;
                int taken = Mathf.RoundToInt(Mathf.Max(0f, hpBefore - PlayerHp()));
                int given = Mathf.RoundToInt(Mathf.Max(0f, enemyBefore - EnemyHp()));

                rows.Add(string.Format("| {0} | {1}/{2} | {3} | {4} ({5:F0}→{6:F0}/{7}) | {8} | {9} | {10} |",
                    turn, before, armed,
                    fired ? "**정독!**" : (warned ? "경고" : "-"),
                    taken, hpBefore, PlayerHp(),
                    latestPlayer != null ? latestPlayer.MaxHp : 0,
                    given, battle.LedgerTally, battle.LedgerReadCount));

                if (fired) readTaken.Add(taken); else if (taken > 0) plainTaken.Add(taken);
            }
        }

        // 스탯은 컨트롤러가 **이벤트로만** 넘겨 준다(공개 게터가 없다). 대결을 열기 전에
        // 구독해 두고 마지막 값을 들고 있는다 — StartDuel과 매 행동이 이걸 울린다.
        // 프로브 전용 게터를 컨트롤러에 뚫지 않으려는 것이다(검증 도구가 제품 표면을 늘리면 안 된다).
        private static InsectBattleStats latestPlayer;
        private static InsectBattleStats latestEnemy;
        // 정독 턴과 평턴의 피해 표본 — 배율이 정말 걸리는지 평균으로 대조한다.
        private static readonly List<int> readTaken = new List<int>();
        private static readonly List<int> plainTaken = new List<int>();
        // 해지는 Finish 한 곳에서 한다 — Probe에는 이른 return이 셋이라 거기서 하면 하나를 빠뜨린다.
        private static InsectBattleController probedBattle;

        private static void OnBattleUpdated(InsectBattleStats player, InsectBattleStats enemy)
        {
            latestPlayer = player;
            latestEnemy = enemy;
        }

        private static float PlayerHp()
        {
            return latestPlayer != null ? latestPlayer.CurrentHp : 0f;
        }

        private static float EnemyHp()
        {
            return latestEnemy != null ? latestEnemy.CurrentHp : 0f;
        }

        /// <summary>
        /// 양쪽을 가득 채운다. 이 프로브는 <b>승부가 아니라 피해 배율</b>을 보는 도구라,
        /// 한쪽이 쓰러지면 표본이 서너 개에서 끊긴다.
        ///
        /// <b>반드시 턴 시작에 부른다.</b> <c>Heal</c>은 0 HP를 되살리지 않으므로
        /// (기절은 병원 전용이다) 턴 끝에서 채우면 한 번 쓰러진 뒤 표본이 전부 0이 된다 —
        /// 처음에 그렇게 짜서 "정독 평균 피해 0"이라는 엉뚱한 표를 얻었다.
        /// 그래도 한 방에 최대 HP를 넘게 맞으면 못 버티므로, 보스와 프로브 레벨을 맞춰야 한다.
        /// </summary>
        private static void TopUp()
        {
            if (latestPlayer != null) latestPlayer.Heal(latestPlayer.MaxHp);
            if (latestEnemy != null) latestEnemy.Heal(latestEnemy.MaxHp);
        }

        private static void Finish(string error)
        {
            EditorApplication.update -= Tick;
            if (probedBattle != null)
            {
                probedBattle.BattleUpdated -= OnBattleUpdated;
                probedBattle = null;
            }
            SessionState.SetString(StageKey, "");

            var sb = new StringBuilder();
            sb.AppendLine("# 장부 압박 — 실전 확인 (" + bossId + ")");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(error))
            {
                sb.AppendLine("> **중단**: " + error);
                sb.AppendLine();
            }
            foreach (string n in notes) sb.AppendLine("- " + n);
            sb.AppendLine();
            sb.AppendLine("기본공격만 되풀이했을 때(= 이 압박이 겨냥하는 패턴):");
            sb.AppendLine();
            sb.AppendLine("| 턴 | 행동 전 장부 | 상태 | 받은 피해 (HP전→후/최대) | 준 피해 | 행동 후 장부 | 누적 정독 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (string r in rows) sb.AppendLine(r);
            sb.AppendLine();
            sb.AppendLine("## 피해 대조");
            sb.AppendLine();
            sb.AppendLine("| 구간 | 표본 | 평균 피해 |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine("| 평턴 | " + plainTaken.Count + " | " + Avg(plainTaken).ToString("F1") + " |");
            sb.AppendLine("| **정독 턴** | " + readTaken.Count + " | **" + Avg(readTaken).ToString("F1") + "** |");
            if (readTaken.Count > 0 && plainTaken.Count > 0)
            {
                float ratio = Avg(readTaken) / Mathf.Max(0.001f, Avg(plainTaken));
                sb.AppendLine();
                sb.AppendLine("실측 배율 **" + ratio.ToString("F2") + "배** (설계값 "
                    + LedgerPressure.ReadDamageMultiplier.ToString("F2") + "배)");
            }

            bool ok = rows.Count > 0 && string.IsNullOrEmpty(error);
            sb.AppendLine();
            sb.AppendLine("결과: **" + (ok ? "표본 " + rows.Count + "턴 수집" : "실패") + "**");

            try
            {
                string root = Path.GetDirectoryName(Application.dataPath);
                string full = Path.GetFullPath(Path.Combine(root ?? ".", outPath));
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
                Log("보고서 → " + full);
            }
            catch (Exception e) { Log("보고서 저장 실패: " + e.Message); }

            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static float Avg(List<int> xs)
        {
            if (xs.Count == 0) return 0f;
            int sum = 0;
            for (int i = 0; i < xs.Count; i++) sum += xs[i];
            return (float)sum / xs.Count;
        }

        private static int ReadInt(string name, int fallback)
        {
            string raw = ReadArg(name, null);
            return int.TryParse(raw, out int v) ? v : fallback;
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
            Debug.Log("[LEDGER] " + msg);
        }
    }
}
#endif
