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
///   [-probeLevel 40] [-probeTurns 90] [-probeVary 7]
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
    ///
    /// <b>한계 넷 — 전부 실측으로 배웠다.</b>
    /// <list type="number">
    ///   <item><b>같은 적 행동끼리만 나눈다.</b> 전체 평균비는 믿을 수 없다 —
    ///   <c>leaf_storm</c>의 쿨다운 4와 임계 7의 톱니 주기 4가 겹쳐 정독이 <b>매번
    ///   storm 턴에만</b> 떨어졌고, 기본타(74)와 storm(106)을 나눠 1.83배라는 거짓
    ///   숫자가 나왔다. 보고서의 행동별 표가 실측치다.</item>
    ///
    ///   <item><b>표본이 최대 HP에 잘리면 배율이 사라진다.</b> HP가 0에 닿으면 관측한
    ///   감소량은 피해가 아니라 상한이라, 평턴과 정독 턴이 똑같은 값으로 찍혀 항상
    ///   1.00배가 나온다. 잘린 표본은 세어서 빼고, 성한 표본이 없으면 <b>숫자를 내지
    ///   않는다</b>(그 오답을 한 번 냈다).</item>
    ///
    ///   <item><b>「보류」는 이 도구로 재현하기 어렵다.</b> 장부가 찼는데 못 때린 턴
    ///   (버프·회복·독·기절·<b>빗나감</b>)에 정독을 들고 넘어가는 동작인데, 기본공격만
    ///   되풀이하면 장부 주기와 적 쿨다운 주기가 <b>위상 고정</b>돼 정독이 늘 때리는
    ///   턴에만 떨어진다 — 90턴에서 피해 0인 턴이 30번 있었는데도 보류는 0이었다.
    ///   <c>-probeVary</c>로 행동을 섞으면 고정이 풀리지만 그만큼 전투가 빨리 끝난다.
    ///   이 경로의 보증은 <c>blight_lint</c> 검사 20(소모가 <c>ApplySkill</c> 뒤)과
    ///   순수부 테스트가 든다.</item>
    ///
    ///   <item><b>실행마다 결과가 조금 다르다.</b> 프로브 곤충을 실제 지급 경로로 넣어
    ///   IV가 무작위다 — 같은 <c>-probeLevel</c>로도 어떤 실행은 보스를 1턴에 눕힌다
    ///   (그러면 표본이 1턴에서 끊긴다). 배율만 보고 절대 피해량은 보지 말 것.</item>
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
        /// <summary>연타할 기본 턴 수 — 임계 4~9를 여러 주기 돌기에 넉넉하다.
        /// <b>보류</b>(장부가 찼는데 못 때린 턴)는 적 스킬 주기와 장부 주기가 맞물려야
        /// 나오므로, 그걸 보려면 -probeTurns로 길게 돌린다.</summary>
        private const int DefaultTurns = 24;
        private static int turns = DefaultTurns;
        /// <summary>
        /// N턴마다 한 번 <b>다른 행동</b>을 섞는다(0 = 안 섞음).
        ///
        /// 기본공격만 치면 장부 주기와 적 스킬 쿨다운 주기가 **위상 고정**돼, 정독이
        /// 늘 같은 종류의 적 턴에만 떨어진다 — 90턴을 돌려도 <b>보류</b>가 한 번도
        /// 안 나온 이유가 그것이다(피해 0인 턴이 30번 있었는데도). 실제 플레이어는
        /// 행동을 바꾸므로 그 고정이 풀린다. 이 인자가 그 조건을 재현한다.
        /// </summary>
        private static int varyEvery;
        /// <summary>플레이어 곤충 레벨 — 관장(Lv.72)에게 몇 턴은 버텨야 표본이 모인다.</summary>
        private const int DefaultProbeLevel = 60;
        private static int probeLevel = DefaultProbeLevel;

        private static string outPath;
        private static string bossId;
        private static float startTime;
        private static float bootDeadline;
        private static readonly List<string> rows = new List<string>();
        private static readonly List<string> notes = new List<string>();

        // **주의**: 이 도구는 실행할 때마다 로컬 세이브의 간부 격파 기록
        // (InsectGame.DefeatedLedgerBosses)을 지운다. 안 지우면 한 번 이긴 뒤로
        // 대결이 안 열려 1회용이 되기 때문이다. 진행 중인 플레이 세이브로 돌리지 말 것.
        [MenuItem("InsectGame/오염 거점/장부 압박 실전 확인 (배치모드 전용)", false, 401)]
        public static void Run()
        {
            outPath = ReadArg("-probeOut", DefaultOut);
            bossId = ReadArg("-probeBoss", DefaultBoss);
            probeLevel = ReadInt("-probeLevel", DefaultProbeLevel);
            turns = Mathf.Clamp(ReadInt("-probeTurns", DefaultTurns), 4, 400);
            varyEvery = Mathf.Max(0, ReadInt("-probeVary", 0));

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                DefaultScene, UnityEditor.SceneManagement.OpenSceneMode.Single);
            SessionState.SetString(StageKey, outPath + "|" + bossId + "|" + probeLevel + "|" + turns + "|" + varyEvery);
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
            // 도메인 리로드를 건너 살아남는 값은 SessionState뿐이다 — 인자는 여기서 되찾는다.
            turns = parts.Length > 3 && int.TryParse(parts[3], out int tn) ? tn : DefaultTurns;
            varyEvery = parts.Length > 4 && int.TryParse(parts[4], out int vr) ? vr : 0;

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
            InsectGame.Core.PlayerInsectData probeInsect =
                collection.AddCapturedInsect(duel.insectId, probeLevel);

            // **1번 슬롯에 앉힌다.** 넣기만 하면 안 나간다 — FindPlayerLeader는 팀 슬롯을
            // 먼저 보고, 거기엔 이미 스타터가 앉아 있다. 그래서 -probeLevel이 아무것도
            // 바꾸지 못한 채 엉뚱한 곤충으로 측정되고 있었다(Lv.95 요청에 Lv.5가 나갔다).
            // 측정 도구는 무엇으로 쟀는지가 재현 가능해야 한다.
            var team = UnityEngine.Object.FindFirstObjectByType<InsectGame.Core.BattleTeamManager>();
            bool seated = team != null && probeInsect != null
                          && team.SetSlot(0, probeInsect.instanceId);
            notes.Add("프로브 곤충: " + duel.insectId + " Lv." + probeLevel
                      + (seated ? " — 1번 슬롯에 앉힘" : " — **슬롯 배정 실패(팀 리더가 대신 나간다)**"));

            // **격파 기록을 지우고 시작한다.** 안 지우면 이 도구는 1회용이다 —
            // 프로브가 한 번 이기는 순간 그 보스가 defeatedBosses에 들어가고, 다음 실행부터
            // CanBossDuel이 false를 돌려 "대결이 안 열렸다"만 남는다(게임 규칙은 맞다,
            // 다만 측정 도구가 자기 전제를 스스로 무너뜨린다). 제품 표면은 안 늘린다 —
            // 클라우드 로드용으로 이미 있는 ReloadFromDisk를 그대로 쓴다.
            UnityEngine.PlayerPrefs.DeleteKey(
                InsectGame.Core.SaveScope.PrefsKey("InsectGame.DefeatedLedgerBosses"));
            duels.ReloadFromDisk();

            latestPlayer = null;
            probedBattle = battle;
            battle.BattleUpdated += OnBattleUpdated;
            if (!duels.TryStartBossDuel(bossId, Time.time))
            {
                notes.Add("**대결이 안 열렸다** — CanBossDuel이 false(리더 없음/쿨다운/데이터)");
                return;
            }

            // **실제 전투원을 적는다.** 넣은 곤충과 싸우는 곤충이 다를 수 있다 —
            // 리더 선택은 팀 편성이 정하지 프로브가 정하지 않는다. 이걸 안 적어서
            // "Lv.95인데 MaxHp가 Lv.55보다 낮다"를 한참 들여다봤다.
            if (latestPlayer != null && latestEnemy != null)
            {
                notes.Add(string.Format("실제 전투원 — 내 {0} Lv.{1} (HP {2} DEF {3}) vs 적 {4} Lv.{5} (ATK {6})",
                    latestPlayer.Data != null ? latestPlayer.Data.insectId : "?", latestPlayer.Level,
                    latestPlayer.MaxHp, latestPlayer.Defense,
                    latestEnemy.Data != null ? latestEnemy.Data.insectId : "?", latestEnemy.Level,
                    latestEnemy.Attack));
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
            for (int turn = 1; turn <= turns; turn++)
            {
                // IsBattleInProgress는 스탯 객체 유무만 본다 — **battleEnded를 안 본다.**
                // 그래서 한쪽이 쓰러진 뒤에도 true이고, 그 뒤 UseBasicAttack은 조용히
                // 아무것도 안 한다. 죽은 턴을 표본으로 세면 "24턴 수집"이라는 거짓
                // 자신감이 나온다(실제로 프로브 곤충이 보스를 1턴에 눕히고 그랬다).
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
                // 위상 고정을 푼다 — 자세한 이유는 varyEvery 주석.
                bool varied = varyEvery > 0 && turn % varyEvery == 0 && battle.CanUseSkill(0);
                if (varied) battle.UseSkill(0); else battle.UseBasicAttack();
                bool fired = battle.LedgerReadCount > readsBefore;
                int taken = Mathf.RoundToInt(Mathf.Max(0f, hpBefore - PlayerHp()));
                int given = Mathf.RoundToInt(Mathf.Max(0f, enemyBefore - EnemyHp()));

                // **표본이 잘렸는가.** HP가 0에 닿으면 관측한 감소량은 실제 피해가 아니라
                // MaxHp라는 상한이다 — 평턴과 정독 턴이 똑같이 MaxHp로 찍혀 배율이 사라진다.
                // 처음엔 이걸 안 보고 "실측 1.00배"라는 거짓 결론을 냈다.
                bool clipped = PlayerHp() <= 0.001f;
                // **보류** — 장부가 찼는데 안 터진 턴. 곱할 피해가 없었거나(버프·회복·독·기절)
                // 빗나간 턴이라 정독을 쓰지 않고 들고 넘어간 것이다. 이 열이 그 설계의 증거다.
                bool held = before >= armed && !fired;

                // **적이 무엇으로 때렸는지**를 남긴다. 이게 없으면 평턴과 정독 턴이
                // 서로 다른 스킬일 때 그 차이가 배율로 둔갑한다 — 실제로 겪었다:
                // storm의 쿨다운 4와 임계 7의 톱니 주기 4가 겹쳐 정독이 **매번 storm 턴에만**
                // 떨어졌고, 기본타(74)와 storm(106)을 나눠 1.83배라는 거짓 숫자가 나왔다.
                string act = battle.LastEnemySkill != null
                    ? battle.LastEnemySkill.skillId : "기본타";

                rows.Add(string.Format("| {0} | {1}/{2} | {3} | {4} | {5}{6} ({7:F0}→{8:F0}/{9}) | {10} | {11} | {12} |",
                    turn, before, armed,
                    fired ? "**정독!**" : (held ? "**보류**" : (warned ? "경고" : "-")),
                    act,
                    taken, clipped ? " ⚠잘림" : "", hpBefore, PlayerHp(),
                    latestPlayer != null ? latestPlayer.MaxHp : 0,
                    given, battle.LedgerTally, battle.LedgerReadCount));

                // 아무 일도 안 일어난 턴 = 전투가 이미 끝났다. 여기서 끊지 않으면 남은 턴이
                // 전부 0으로 찍혀 표에 섞인다.
                if (taken == 0 && given == 0 && battle.LedgerTally == before && !fired)
                {
                    rows.RemoveAt(rows.Count - 1);
                    notes.Add("턴 " + turn + "에서 **아무 변화가 없다** — 전투가 이미 끝난 것이다"
                              + "(IsBattleInProgress는 battleEnded를 안 본다). 표본은 여기까지.");
                    break;
                }

                if (held) heldTurns++;
                if (taken <= 0) missTurns++;
                // 잘린 표본은 배율 대조에서 뺀다 — 상한값끼리 나누면 항상 1.00이 나온다.
                if (clipped) clippedTurns++;
                else if (fired) { readTaken.Add(taken); Bucket(readByAct, act, taken); }
                else if (taken > 0) { plainTaken.Add(taken); Bucket(plainByAct, act, taken); }
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
        // 잘린 표본(HP가 0에 닿아 관측값이 상한인 턴) / 보류 턴 / 피해 0인 턴.
        // 적 행동별 피해 표본 — 같은 행동끼리만 나눠야 배율이 나온다.
        private static readonly Dictionary<string, List<int>> plainByAct = new Dictionary<string, List<int>>();
        private static readonly Dictionary<string, List<int>> readByAct = new Dictionary<string, List<int>>();
        private static int clippedTurns;
        private static int heldTurns;
        private static int missTurns;
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
        /// 두 곤충을 <b>턴 시작에</b> 풀피로 되돌린다 — 그래야 매 턴의 피해가 순수한
        /// 한 방 표본이 된다.
        ///
        /// <c>Heal</c>이 아니라 <c>ResetHp</c>다. <c>Heal</c>은 <b>0 HP를 못 살린다</b>
        /// (기절 회복은 병원 전용이라는 게임 규칙이다). 프로브 곤충이 한 방에 눕는
        /// 조합에서는 그 규칙 때문에 2턴째부터 표본이 전부 0으로 찍혔다 —
        /// 실제로 검은 옷의 여자(Lv.32)전에서 그렇게 측정이 통째로 죽었다.
        /// 측정 하네스는 게임 규칙에 지면 안 된다.
        /// </summary>
        private static void TopUp()
        {
            if (latestPlayer != null) latestPlayer.ResetHp();
            if (latestEnemy != null) latestEnemy.ResetHp();
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
            sb.AppendLine("| 턴 | 행동 전 장부 | 상태 | 적 행동 | 받은 피해 (HP전→후/최대) | 준 피해 | 행동 후 장부 | 누적 정독 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (string r in rows) sb.AppendLine(r);
            sb.AppendLine();
            sb.AppendLine("## 피해 대조");
            sb.AppendLine();
            sb.AppendLine("| 구간 | 표본 | 평균 피해 |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine("| 평턴 | " + plainTaken.Count + " | " + Avg(plainTaken).ToString("F1") + " |");
            sb.AppendLine("| **정독 턴** | " + readTaken.Count + " | **" + Avg(readTaken).ToString("F1") + "** |");
            sb.AppendLine("| 잘림(제외) | " + clippedTurns + " | — |");
            sb.AppendLine();
            if (readTaken.Count > 0 && plainTaken.Count > 0)
            {
                float ratio = Avg(readTaken) / Mathf.Max(0.001f, Avg(plainTaken));
                sb.AppendLine("전체 평균비 " + ratio.ToString("F2")
                    + "배 — **이 숫자는 믿지 말 것.** 평턴과 정독 턴의 적 행동이 다르면 "
                    + "그 차이까지 섞인다. 아래 행동별 대조가 실측치다.");
                sb.AppendLine();
                sb.AppendLine("| 적 행동 | 평턴 평균 | 정독 평균 | 실측 배율 |");
                sb.AppendLine("|---|---|---|---|");
                int comparable = 0;
                foreach (KeyValuePair<string, List<int>> kv in readByAct)
                {
                    if (!plainByAct.TryGetValue(kv.Key, out List<int> plain) || plain.Count == 0)
                    {
                        sb.AppendLine("| " + kv.Key + " | — | " + Avg(kv.Value).ToString("F1")
                            + " | 대조군 없음(이 행동은 정독 턴에만 나왔다) |");
                        continue;
                    }
                    comparable++;
                    sb.AppendLine("| " + kv.Key + " | " + Avg(plain).ToString("F1") + " | "
                        + Avg(kv.Value).ToString("F1") + " | **"
                        + (Avg(kv.Value) / Mathf.Max(0.001f, Avg(plain))).ToString("F2") + "배** |");
                }
                sb.AppendLine();
                sb.AppendLine("설계값 " + LedgerPressure.ReadDamageMultiplier.ToString("F2") + "배"
                    + (comparable == 0
                        ? " — **같은 행동으로 양쪽이 잡힌 표본이 없다.** 적 스킬의 쿨다운 주기와 "
                          + "장부 주기가 겹치면 이렇게 된다(임계나 프로브 레벨을 바꿔 어긋내 볼 것)."
                        : ""));
            }
            else
            {
                // **숫자를 지어내지 않는다.** 표본이 전부 잘렸는데 평균을 내면 항상 1.00배가
                // 나오고, 그건 "배율이 안 걸린다"로 읽힌다 — 실제로 그 오답을 한 번 냈다.
                sb.AppendLine("> **배율 측정 불가** — 성한 표본이 "
                    + (plainTaken.Count == 0 ? "평턴 0개" : "정독 턴 0개")
                    + (clippedTurns > 0
                        ? "다. " + clippedTurns + "턴이 최대 HP에 잘렸다(한 방에 눕는 조합이다). "
                          + "`-probeLevel`을 올려 프로브 곤충이 버티게 할 것."
                        : "다."));
            }
            sb.AppendLine();
            sb.AppendLine("| 관측 | 횟수 | 뜻 |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine("| **보류** | " + heldTurns + " | 장부가 찼는데 안 터진 턴 — 곱할 피해가 없거나 빗나가서 들고 넘어갔다 |");
            sb.AppendLine("| 피해 0 | " + missTurns + " | 빗나갔거나 적이 때리지 않은 턴 |");

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

        private static void Bucket(Dictionary<string, List<int>> map, string key, int value)
        {
            if (!map.TryGetValue(key, out List<int> xs)) { xs = new List<int>(); map[key] = xs; }
            xs.Add(value);
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
