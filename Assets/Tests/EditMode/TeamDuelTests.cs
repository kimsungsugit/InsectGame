#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 간부 팀 대결(<see cref="InsectBattleController.StartTeamDuel"/>) — 상대가 곤충을 차례로 내보내고 마지막이 쓰러져야 이긴다.
    ///
    /// 실패는 전부 조용하다: 첫 곤충이 쓰러질 때 전투가 끝나 버리면 간부전이 한 마리 대결로 줄고, 교체가 상대의 독·디버프를
    /// 안 지우면 새 곤충이 이전 곤충의 상처를 안고 나오고, 장부가 교체로 비워지면 압박이 곤충마다 처음부터 다시 찬다.
    /// 승리·보상이 두 번 나가면 간부 격파 기록과 보상이 겹친다. 그래서 이벤트 횟수와 상태를 고정한다.
    /// </summary>
    [TestFixture]
    public class TeamDuelTests
    {
        private readonly List<Object> objects = new List<Object>();
        private string currencyPath;

        [SetUp]
        public void SetUp()
        {
            // 승리 코인이 실제 세이브에 쓰인다 — 백업하고 비운 상태에서 잰다(DexCoinRewardTests와 같은 방식).
            currencyPath = SaveScope.FilePath(GameConstants.SaveFiles.PlayerCurrency);
            string backup = currencyPath + ".teamduelbak";
            // 앞선 실행이 도중에 죽어 백업이 남아 있으면 그게 진짜 세이브다 — 지우지 말고 먼저 되돌린다.
            if (File.Exists(backup))
            {
                if (File.Exists(currencyPath)) File.Delete(currencyPath);
                File.Move(backup, currencyPath);
            }
            if (File.Exists(currencyPath)) File.Move(currencyPath, backup);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            if (File.Exists(currencyPath)) File.Delete(currencyPath);
            if (File.Exists(currencyPath + ".teamduelbak")) File.Move(currencyPath + ".teamduelbak", currencyPath);
        }

        // ── 교체 ──

        [Test]
        public void FirstOpponentFaints_BattleContinues_AndSwitchesOnce()
        {
            Fixture f = NewFixture();
            InsectData[] team = { Enemy("e0"), Enemy("e1"), Enemy("e2") };
            Assert.IsTrue(f.battle.StartTeamDuel(f.player, 10, team, new[] { 5, 6, 7 }, equippedSkills: f.skills));
            Assert.IsTrue(f.battle.IsEnemyTeamBattle);
            Assert.AreEqual(3, f.battle.EnemyTeamSize);
            Assert.AreEqual(0, f.battle.EnemyTeamIndex);
            Assert.AreEqual(3, f.battle.EnemiesRemaining);

            f.battle.UseSkill(Nuke);

            Assert.AreEqual(0, f.counts.battleEnded, "첫 곤충이 쓰러졌다고 전투가 끝났다");
            Assert.AreEqual(0, f.counts.duelEnded);
            Assert.AreEqual(1, f.counts.switched, "교체 이벤트는 한 번");
            Assert.AreEqual("e0", f.counts.lastOutgoing.Data.insectId);
            Assert.AreEqual("e1", f.counts.lastIncoming.Data.insectId);
            Assert.AreEqual(6, f.counts.lastIncoming.Level);
            Assert.AreEqual(f.counts.lastIncoming.MaxHp, f.counts.lastIncoming.CurrentHp, "들어온 곤충은 풀피로 나온다");
            Assert.AreEqual(1, f.battle.EnemyTeamIndex);
            Assert.AreEqual(2, f.battle.EnemiesRemaining);
            Assert.IsTrue(f.battle.IsEnemyTeamMemberFainted(0));
            Assert.IsFalse(f.battle.IsEnemyTeamMemberFainted(1));
            Assert.AreEqual("e1", f.battle.EnemyInsectId, "지금 상대가 들어온 곤충이어야 한다");
            Assert.AreEqual("e2", f.battle.GetEnemyTeamInsect(2).insectId);
            Assert.AreEqual(7, f.battle.GetEnemyTeamLevel(2));
        }

        [Test]
        public void IncomingOpponent_DoesNotActInTheRoundItEnters()
        {
            Fixture f = NewFixture();
            InsectData[] team = { Enemy("e0"), Enemy("e1") };
            Assert.IsTrue(f.battle.StartTeamDuel(f.player, 10, team, new[] { 5, 5 }, equippedSkills: f.skills));
            f.battle.UseSkill(Nuke);
            Assert.IsFalse(f.battle.EnemyActedThisRound, "쓰러진 곤충 대신 들어온 곤충이 같은 라운드에 반격했다");
        }

        [Test]
        public void Switch_ResetsOpponentState_AndKeepsMine()
        {
            Fixture f = NewFixture();
            InsectData venomous = Enemy("e0");
            venomous.skills = new[] { Skill("enemy_venom", SkillEffectType.PoisonDot, 2, 0f, 5) };
            InsectData[] team = { venomous, Enemy("e1") };
            Assert.IsTrue(f.battle.StartTeamDuel(f.player, 10, team, new[] { 5, 5 }, equippedSkills: f.skills));

            f.battle.UseSkill(Boost);    // 내 공격 버프 — 상대는 내게 독
            f.battle.UseSkill(Weaken);   // 상대 공격 하락
            f.battle.UseSkill(Venom);    // 상대에게 독
            Assert.Greater(Count(f.battle, onPlayer: false), 0, "전제: 교체 전 상대에게 걸린 효과가 있어야 한다");
            int mineBefore = Count(f.battle, onPlayer: true);
            Assert.Greater(mineBefore, 0, "전제: 내게 걸린 효과(버프·독)가 있어야 한다");

            f.battle.UseSkill(Nuke);     // e0 쓰러짐 → e1

            Assert.AreEqual(1, f.counts.switched);
            Assert.AreEqual(0, Count(f.battle, onPlayer: false), "상대 쪽 버프·디버프·독은 새 곤충 기준으로 비워져야 한다");
            Assert.AreEqual(mineBefore, Count(f.battle, onPlayer: true), "내 곤충의 버프·독은 그대로여야 한다");
            Assert.AreEqual(0f, f.counts.lastIncoming.AttackBonus, 0.0001f, "들어온 곤충이 이전 곤충의 공격 하락을 물려받았다");
            Assert.AreEqual(0, f.battle.EnemyStunTurns);
            Assert.IsTrue(HasPlayerEffect(f.battle, InsectBattleController.EffectKind.AtkBuff), "내 공격 버프가 사라졌다");
            Assert.IsTrue(HasPlayerEffect(f.battle, InsectBattleController.EffectKind.Dot), "내게 걸린 독이 사라졌다(내 상태는 그대로다)");
        }

        [Test]
        public void Ledger_CarriesAcrossTheSwitch()
        {
            // 장부는 곤충이 아니라 인물의 것이다 — 교체로 비워지면 간부전마다 압박이 곤충 수만큼 쪼개진다.
            Fixture f = NewFixture();
            InsectData[] team = { Enemy("e0"), Enemy("e1") };
            Assert.IsTrue(f.battle.StartTeamDuel(f.player, 10, team, new[] { 5, 5 }, equippedSkills: f.skills));
            f.battle.ArmLedger(9);

            f.battle.UseBasicAttack();
            f.battle.UseBasicAttack();
            f.battle.UseBasicAttack();
            int before = f.battle.LedgerTally;
            Assert.Greater(before, 0, "전제: 같은 행동을 되풀이해 장부가 차 있어야 한다");
            Assert.AreEqual(0, f.counts.switched, "전제: 기본 공격 셋으로는 첫 곤충이 안 쓰러진다");

            f.battle.UseSkill(Nuke);

            Assert.AreEqual(1, f.counts.switched);
            Assert.AreEqual(9, f.battle.LedgerThreshold, "교체가 장부 임계를 지웠다");
            Assert.AreEqual(LedgerPressure.NextTally(before, 9, false), f.battle.LedgerTally,
                "교체가 장부를 비웠다 — 행동을 바꾼 만큼(-1)만 줄어야 한다");
        }

        // ── 끝 ──

        [Test]
        public void LastOpponentFaints_WinsExactlyOnce()
        {
            Fixture f = NewFixture();
            InsectData[] team = { Enemy("e0"), Enemy("e1"), Enemy("e2") };
            Assert.IsTrue(f.battle.StartTeamDuel(f.player, 10, team, new[] { 5, 6, 7 }, equippedSkills: f.skills));

            f.battle.UseSkill(Nuke);
            f.battle.UseSkill(Nuke);
            Assert.AreEqual(0, f.counts.battleEnded, "두 번째 곤충에서 끝났다");
            f.battle.UseSkill(Nuke);

            Assert.AreEqual(2, f.counts.switched, "세 마리 팀은 교체가 두 번");
            Assert.AreEqual(1, f.counts.battleEnded, "승리는 한 번");
            Assert.AreEqual(1, f.counts.duelEnded);
            Assert.IsTrue(f.counts.won);
            Assert.IsTrue(f.battle.GetLastPlayerWon());
            Assert.AreEqual(0, f.battle.EnemiesRemaining);
            Assert.AreEqual("e2", f.battle.EnemyInsectId, "BattleEnded 시점의 상대는 에이스다");

            f.battle.UseSkill(Nuke);   // 끝난 뒤 입력은 무시된다
            Assert.AreEqual(1, f.counts.battleEnded);
            Assert.AreEqual(2, f.counts.switched);
        }

        [Test]
        public void Rewards_AreTheSumOfOneDuelPerOpponent()
        {
            InsectData[] team = { Enemy("e0", InsectRarity.Common), Enemy("e1", InsectRarity.Rare), Enemy("e2", InsectRarity.Epic) };
            int[] levels = { 5, 6, 7 };

            int candySum = 0, expSum = 0;
            for (int i = 0; i < team.Length; i++)
            {
                Fixture single = NewFixture();
                Assert.IsTrue(single.battle.StartDuel(single.player, 10, team[i], levels[i], equippedSkills: single.skills));
                single.battle.UseSkill(Nuke);
                Assert.AreEqual(1, single.counts.battleEnded);
                candySum += single.battle.GetLastCandyReward();
                expSum += single.battle.GetLastExpReward();
            }
            Assert.Greater(candySum, 0, "전제: 보상이 0이면 합산을 잴 수 없다");

            Fixture f = NewFixture();
            int coinsBefore = f.wallet.Coins;
            Assert.IsTrue(f.battle.StartTeamDuel(f.player, 10, team, levels, equippedSkills: f.skills));
            for (int i = 0; i < team.Length; i++) f.battle.UseSkill(Nuke);

            Assert.AreEqual(1, f.counts.battleEnded);
            Assert.AreEqual(candySum, f.battle.GetLastCandyReward(), "캔디는 상대마다 한 마리 대결과 같은 식의 합");
            Assert.AreEqual(expSum, f.battle.GetLastExpReward(), "EXP는 상대마다 한 마리 대결과 같은 식의 합");
            Assert.AreEqual(coinsBefore + 3 * team.Length, f.wallet.Coins, "코인은 상대마다 승리 코인(3)");
            Assert.IsFalse(f.battle.GetLastCaptureAttempted(), "대결은 포획 판정이 없다");
        }

        [Test]
        public void Defeat_EndsOnceWithNoReward_AndTheNextChallengeStartsFromTheFirst()
        {
            Fixture f = NewFixture();
            InsectData brute = Enemy("brute");
            brute.baseHp = 50000;
            brute.baseAtk = 5000;
            brute.skills = new[] { Skill("crush", SkillEffectType.Damage, 5000) };
            InsectData[] team = { Enemy("e0"), brute };
            int coinsBefore = f.wallet.Coins;
            Assert.IsTrue(f.battle.StartTeamDuel(f.player, 10, team, new[] { 5, 9 }, equippedSkills: f.skills));

            f.battle.UseSkill(Nuke);   // e0 쓰러짐 → brute
            Assert.AreEqual(1, f.counts.switched);
            f.battle.UseSkill(Nuke);   // brute는 버티고 한 방에 눕힌다 — 교체 화면(PlayerFainted 구독자)이 없으니 패배

            Assert.AreEqual(1, f.counts.battleEnded, "패배는 한 번");
            Assert.AreEqual(1, f.counts.duelEnded);
            Assert.IsFalse(f.counts.won);
            Assert.AreEqual(0, f.battle.GetLastCandyReward(), "지면 쓰러뜨린 곤충의 보상도 없다");
            Assert.AreEqual(coinsBefore, f.wallet.Coins);

            Assert.IsTrue(f.battle.StartTeamDuel(f.player, 10, team, new[] { 5, 9 }, equippedSkills: f.skills));
            Assert.AreEqual(0, f.battle.EnemyTeamIndex, "재도전은 처음부터");
            Assert.AreEqual(2, f.battle.EnemiesRemaining);
            Assert.AreEqual("e0", f.battle.EnemyInsectId);
        }

        [Test]
        public void BattleFeat_UsesTheAce_AndTheWholeBattlesActionCount()
        {
            // 조건부 퀘스트 입력은 정적 매니저로 가므로 여기서는 그 재료를 본다 — 끝났을 때 상대는 에이스, 행동 수는 전체.
            Fixture f = NewFixture();
            InsectData[] team = { Enemy("e0"), Enemy("e1") };
            Assert.IsTrue(f.battle.StartTeamDuel(f.player, 10, team, new[] { 5, 8 }, equippedSkills: f.skills));
            f.battle.UseBasicAttack();
            f.battle.UseSkill(Nuke);
            f.battle.UseSkill(Nuke);
            Assert.AreEqual(1, f.counts.battleEnded);
            Assert.AreEqual("e1", f.battle.EnemyInsectId);
            Assert.AreEqual(3, (int)typeof(InsectBattleController)
                .GetField("playerActionCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.battle),
                "내 행동 수는 교체로 다시 세지 않는다");
        }

        [Test]
        public void StartTeamDuel_RejectsBadRosters()
        {
            Fixture f = NewFixture();
            Assert.IsFalse(f.battle.StartTeamDuel(f.player, 10, null, new[] { 5 }));
            Assert.IsFalse(f.battle.StartTeamDuel(f.player, 10, new InsectData[0], new int[0]));
            Assert.IsFalse(f.battle.StartTeamDuel(f.player, 10, new[] { Enemy("a"), null }, new[] { 5, 5 }), "빈 칸");
            Assert.IsFalse(f.battle.StartTeamDuel(f.player, 10, new[] { Enemy("a"), Enemy("b") }, new[] { 5 }), "레벨 누락");
            Assert.IsFalse(f.battle.IsBattleInProgress(), "거절은 상태를 건드리지 않는다");
        }

        // ── 한 마리 대결 회귀 ──

        [Test]
        public void SingleDuel_IsUnchanged_AndDoesNotInheritATeam()
        {
            Fixture f = NewFixture();
            Assert.IsTrue(f.battle.StartTeamDuel(f.player, 10, new[] { Enemy("t0"), Enemy("t1") }, new[] { 5, 5 },
                equippedSkills: f.skills));
            Assert.IsTrue(f.battle.IsEnemyTeamBattle);

            InsectData lone = Enemy("lone", InsectRarity.Rare);
            Assert.IsTrue(f.battle.StartDuel(f.player, 10, lone, 6, equippedSkills: f.skills));
            Assert.IsFalse(f.battle.IsEnemyTeamBattle, "앞 전투의 팀이 남았다");
            Assert.AreEqual(1, f.battle.EnemyTeamSize);
            Assert.AreEqual(0, f.battle.EnemyTeamIndex);
            Assert.AreEqual(1, f.battle.EnemiesRemaining);
            Assert.AreEqual("lone", f.battle.GetEnemyTeamInsect(0).insectId);

            int endedBefore = f.counts.battleEnded;
            f.battle.UseSkill(Nuke);
            Assert.AreEqual(endedBefore + 1, f.counts.battleEnded, "한 마리 대결은 그 곤충이 쓰러지면 끝난다");
            Assert.AreEqual(0, f.counts.switched, "한 마리 대결에 교체 이벤트가 울렸다");
            Assert.AreEqual(0, f.battle.EnemiesRemaining);
            Assert.AreEqual(Mathf.RoundToInt(InsectRewardCalculator.GetCandyReward(lone)), f.battle.GetLastCandyReward(),
                "한 마리 대결 보상이 예전 식과 다르다");
            Assert.AreEqual(Mathf.RoundToInt(InsectRewardCalculator.GetExpReward(lone, 6, 10)), f.battle.GetLastExpReward());
        }

        // ── 실제 DB ──

        [Test]
        public void RealDatabase_HasEveryRosterInsect()
        {
            InsectDatabase db = RealDatabase();
            foreach (NpcBossDuels.BossDuel d in NpcBossDuels.All())
                for (int i = 0; i < d.RosterSize; i++)
                    Assert.IsNotNull(db.GetById(d.RosterInsectId(i)),
                        $"{d.storyNpcId}의 {i + 1}번째 곤충 '{d.RosterInsectId(i)}'가 곤충 DB에 없다 — 그 곤충만 조용히 빠진다");
        }

        /// <summary>
        /// 전투 길이 감각 — 실제 곤충으로 간부 팀과 <b>같은 종·같은 레벨의 거울 팀</b>을 붙여 몇 번 행동하는지 로그(<c>[TeamDuel]</c>)에 남긴다.
        /// 한 마리 대결(에이스 대 에이스)과 나란히 적어 "몇 배 길어졌나"를 본다. 숫자를 단언하지 않는다 — 끝나기만 하면 된다
        /// (가장 센 피해기만 누르는 단순한 수라 실제 플레이보다 짧게 나온다).
        /// </summary>
        [Test]
        public void RealTeams_MirrorMatch_LogsHowLongTheyLast()
        {
            InsectDatabase db = RealDatabase();
            foreach (NpcBossDuels.BossDuel d in NpcBossDuels.All())
            {
                if (!d.IsTeam) continue;
                var team = new InsectData[d.RosterSize];
                var levels = new int[d.RosterSize];
                for (int i = 0; i < d.RosterSize; i++)
                {
                    team[i] = db.GetById(d.RosterInsectId(i));
                    levels[i] = d.RosterLevel(i);
                }

                MirrorResult teamRun = RunMirror(team, levels, team, levels, d.ledgerThreshold);
                MirrorResult aloneRun = RunMirror(new[] { team[team.Length - 1] }, new[] { levels[levels.Length - 1] },
                    new[] { team[team.Length - 1] }, new[] { levels[levels.Length - 1] }, d.ledgerThreshold);
                Debug.Log($"[TeamDuel] {d.storyNpcId} {d.RosterSize}마리 거울전 — 내 행동 {teamRun.actions}번 · 쓰러진 내 곤충 {teamRun.myFainted} · " +
                          $"{(teamRun.won ? "승" : "패")} | 에이스 한 마리 대결 {aloneRun.actions}번 → {(aloneRun.actions > 0 ? (float)teamRun.actions / aloneRun.actions : 0f):0.0}배");
                Assert.IsTrue(teamRun.ended, $"{d.storyNpcId}: 거울전이 {MirrorActionCap}번 안에 끝나지 않았다");
            }
        }

        private const int MirrorActionCap = 400;

        private struct MirrorResult
        {
            public bool ended;
            public bool won;
            public int actions;
            public int myFainted;
        }

        private MirrorResult RunMirror(InsectData[] mine, int[] myLevels, InsectData[] theirs, int[] theirLevels, int ledger)
        {
            Fixture f = NewFixture();
            f.battle.SetRandomSeed(7);
            f.battle.SetCritSource(null);
            var result = new MirrorResult();
            int myIndex = 0;
            bool fainted = false;
            f.battle.PlayerFainted += () => fainted = true;

            Assert.IsTrue(f.battle.StartTeamDuel(mine[0], myLevels[0], theirs, theirLevels,
                equippedSkills: StrongestSkills(mine[0], myLevels[0])));
            f.battle.ArmLedger(ledger);
            while (f.counts.battleEnded == 0 && result.actions < MirrorActionCap)
            {
                if (fainted)
                {
                    fainted = false;
                    result.myFainted++;
                    myIndex++;
                    if (myIndex >= mine.Length) { f.battle.ConcludeDefeatWithoutSwap(); break; }
                    f.battle.SwapPlayerInsect(mine[myIndex], myLevels[myIndex], StrongestSkills(mine[myIndex], myLevels[myIndex]));
                }

                int pick = -1;
                InsectSkill[] skills = f.battle.GetPlayerSkills();
                for (int i = 0; skills != null && i < skills.Length; i++)
                    if (f.battle.CanUseSkill(i) && (pick < 0 || skills[i].power > skills[pick].power)) pick = i;
                if (pick >= 0) f.battle.UseSkill(pick); else f.battle.UseBasicAttack();
                result.actions++;
            }
            result.ended = f.counts.battleEnded > 0;
            result.won = f.counts.won;
            return result;
        }

        // 해금된 피해기 중 위력 순으로 넷 — 거울전의 "같은 수준의 플레이어".
        private static InsectSkill[] StrongestSkills(InsectData data, int level)
        {
            var list = new List<InsectSkill>();
            if (data != null && data.learnset != null)
                foreach (InsectLearnableSkill l in data.learnset)
                    if (l != null && l.skill != null && l.learnLevel <= level && l.skill.effectType == SkillEffectType.Damage
                        && !list.Contains(l.skill))
                        list.Add(l.skill);
            list.Sort((a, b) => b.power.CompareTo(a.power));
            if (list.Count > 4) list.RemoveRange(4, list.Count - 4);
            return list.Count > 0 ? list.ToArray() : null;
        }

        // ── 도우미 ──

        private const int Nuke = 0;
        private const int Boost = 1;
        private const int Weaken = 2;
        private const int Venom = 3;

        private sealed class Counts
        {
            public int battleEnded;
            public int duelEnded;
            public int switched;
            public bool won;
            public InsectBattleStats lastOutgoing;
            public InsectBattleStats lastIncoming;
        }

        private sealed class Fixture
        {
            public InsectBattleController battle;
            public PlayerCurrencyWallet wallet;
            public InsectData player;
            public InsectSkill[] skills;
            public Counts counts;
        }

        private Fixture NewFixture()
        {
            // 캔디·경험치 지급처는 붙이지 않는다(디스크에 쓴다) — 값은 GetLast*Reward로 읽는다.
            // 코인은 지갑을 붙여 실제 지급을 본다(SetUp이 세이브를 비워 둔다).
            var go = new GameObject("TeamDuelFixture");
            go.SetActive(false);
            objects.Add(go);
            var wallet = go.AddComponent<PlayerCurrencyWallet>();
            typeof(PlayerCurrencyWallet).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(wallet, new PlayerCurrencyData { coins = 11, gems = 0 });
            var battle = go.AddComponent<InsectBattleController>();
            battle.AutoWire(wallet);
            battle.SetRandomSeed(1);
            battle.SetCritSource(null);   // 피해·보상을 정확히 보므로 치명타를 끈다

            var c = new Counts();
            battle.BattleEnded += won => { c.battleEnded++; c.won = won; };
            battle.DuelEnded += won => c.duelEnded++;
            battle.EnemySwitched += (o, i) => { c.switched++; c.lastOutgoing = o; c.lastIncoming = i; };

            InsectData player = Species("player", 5000, 20, 30, InsectRarity.Common);
            InsectSkill[] skills =
            {
                Skill("nuke", SkillEffectType.Damage, 2000),
                Skill("boost", SkillEffectType.BuffAttack, 1, 0.5f, 5),
                Skill("weaken", SkillEffectType.DebuffAttack, 1, 0.3f, 5),
                Skill("venom", SkillEffectType.PoisonDot, 3, 0f, 5),
            };
            player.skills = skills;
            return new Fixture { battle = battle, wallet = wallet, player = player, skills = skills, counts = c };
        }

        private InsectData Enemy(string id, InsectRarity rarity = InsectRarity.Common)
        {
            InsectData d = Species(id, 500, 10, 20, rarity);
            d.skills = new[] { Skill(id + "_poke", SkillEffectType.Damage, 1) };
            return d;
        }

        private InsectData Species(string id, int hp, int atk, int def, InsectRarity rarity)
        {
            var d = ScriptableObject.CreateInstance<InsectData>();
            objects.Add(d);
            d.insectId = id;
            d.displayName = id;
            d.rarity = rarity;
            d.baseHp = hp;
            d.baseAtk = atk;
            d.baseDef = def;
            d.primaryType = InsectElement.None;
            d.secondaryType = InsectElement.None;
            d.candyReward = 3;
            d.expReward = 5;
            return d;
        }

        private InsectSkill Skill(string id, SkillEffectType type, int power, float effectValue = 0f, int duration = 0)
        {
            var s = ScriptableObject.CreateInstance<InsectSkill>();
            objects.Add(s);
            s.skillId = id;
            s.displayName = id;
            s.element = InsectElement.None;
            s.effectType = type;
            s.power = power;
            s.effectValue = effectValue;
            s.effectDurationTurns = duration;
            s.cooldownTurns = 0;
            s.accuracy = 1f;
            return s;
        }

        private static int Count(InsectBattleController battle, bool onPlayer)
        {
            int n = 0;
            foreach (InsectBattleController.EffectSnapshot e in battle.GetActiveEffects())
                if (e.targetIsPlayer == onPlayer) n++;
            return n;
        }

        private static bool HasPlayerEffect(InsectBattleController battle, InsectBattleController.EffectKind kind)
        {
            foreach (InsectBattleController.EffectSnapshot e in battle.GetActiveEffects())
                if (e.targetIsPlayer && e.kind == kind) return true;
            return false;
        }

        private InsectDatabase RealDatabase()
        {
            var host = new GameObject("TeamDuelTests_RealDb");
            host.SetActive(false);   // Awake(월드 생성)를 막고 private 생성 함수만 부른다
            objects.Add(host);
            var bootstrap = host.AddComponent<PlaySceneBootstrap>();
            var db = (InsectDatabase)typeof(PlaySceneBootstrap)
                .GetMethod("EnsureExpandedDatabase", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bootstrap, null);
            Assert.IsNotNull(db);
            objects.Add(db);
            return db;
        }
    }
}
#endif
