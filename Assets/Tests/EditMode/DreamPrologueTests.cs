#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 「챔피언의 꿈」 — 규칙(샌드박스 피해·시작 조건)과 <b>꿈 밖에 아무것도 남기지 않는다는 약속</b>.
    ///
    /// 이 연출의 실패는 전부 조용하다: 챔피언전 승리가 퀘스트·스토리·재화를 슬쩍 올려도, 섬 배치에서 물건 하나가
    /// 빠져도, 곤충 ID 하나가 오타여도 예외도 경고도 없다. 그래서 화면이 아니라 상태를 고정한다.
    /// </summary>
    [TestFixture]
    public class DreamPrologueTests
    {
        private readonly List<Object> objects = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            DreamPrologueState.End();
            foreach (Object o in objects) if (o != null) Object.DestroyImmediate(o);
            objects.Clear();
        }

        // ── 샌드박스 피해 규칙 ──

        [Test]
        public void PlayerDamage_IsScaledUp()
        {
            Assert.AreEqual(Mathf.RoundToInt(100 * SandboxBattleRules.PlayerDamageScale),
                SandboxBattleRules.PlayerDamage(100, 100000, SandboxBattleRules.MinPlayerActions));
        }

        [Test]
        public void PlayerDamage_BeforeMinActions_NeverKills()
        {
            // 첫 일격이 적을 쓰러뜨리면 스킬 하나 눌러 보고 끝난다 — 적 HP가 1은 남아야 한다.
            for (int action = 1; action < SandboxBattleRules.MinPlayerActions; action++)
                Assert.AreEqual(49, SandboxBattleRules.PlayerDamage(1000, 50, action), $"{action}번째 행동");
        }

        [Test]
        public void PlayerDamage_OnTheLastAction_AlwaysKills()
        {
            // 공식이 낸 피해가 아무리 작아도 마지막 행동에서는 끝난다 — 끝나지 않는 꿈은 없다.
            Assert.AreEqual(5000, SandboxBattleRules.PlayerDamage(1, 5000, SandboxBattleRules.MaxPlayerActions));
            Assert.AreEqual(5000, SandboxBattleRules.PlayerDamage(1, 5000, SandboxBattleRules.MaxPlayerActions + 3));
        }

        [Test]
        public void PlayerDamage_AgainstDeadEnemy_IsZero()
        {
            Assert.AreEqual(0, SandboxBattleRules.PlayerDamage(100, 0, 2));
        }

        [Test]
        public void EnemyDamage_IsScaledDown()
        {
            Assert.AreEqual(Mathf.RoundToInt(100 * SandboxBattleRules.EnemyDamageScale),
                SandboxBattleRules.EnemyDamage(100, 10000, 10000));
        }

        [Test]
        public void EnemyDamage_NeverTakesTheChampionBelowTheFloor()
        {
            int max = 1000;
            int floor = SandboxBattleRules.HpFloor(max);
            Assert.AreEqual(300, floor);
            Assert.AreEqual(10, SandboxBattleRules.EnemyDamage(500, floor + 10, max), "하한까지만 깎인다");
            Assert.AreEqual(0, SandboxBattleRules.EnemyDamage(500, floor, max), "이미 하한이면 피해가 없다");
            Assert.AreEqual(0, SandboxBattleRules.EnemyDamage(500, floor - 50, max));
        }

        [Test]
        public void MinActions_IsBelowMaxActions()
        {
            // 거꾸로 되면 "적이 못 죽는 구간"과 "반드시 죽는 구간"이 겹쳐 규칙이 서로를 지운다.
            Assert.Less(SandboxBattleRules.MinPlayerActions, SandboxBattleRules.MaxPlayerActions);
        }

        // ── 시작 조건 ──

        [Test]
        public void ShouldAutoStart_NewAccountAtFirstQuest_Starts()
        {
            Assert.IsTrue(DreamPrologueRules.ShouldAutoStart("q_move", 0, false, false, false, false));
        }

        [TestCase("q_talk_elder", 0, false, false, false, false, TestName = "AfterFirstQuest")]
        [TestCase("q_move", 1, false, false, false, false, TestName = "FirstQuestStarted")]
        [TestCase("q_move", 0, true, false, false, false, TestName = "AlreadyPlayed")]
        [TestCase("q_move", 0, false, true, false, false, TestName = "ModalOpen_LoginOrDialogue")]
        [TestCase("q_move", 0, false, false, true, false, TestName = "PlayerFrozen")]
        [TestCase("q_move", 0, false, false, false, true, TestName = "InSubArea")]
        [TestCase(null, 0, false, false, false, false, TestName = "NoActiveQuest")]
        public void ShouldAutoStart_OtherSituations_DoesNot(string quest, int progress, bool played,
            bool modal, bool frozen, bool subArea)
        {
            Assert.IsFalse(DreamPrologueRules.ShouldAutoStart(quest, progress, played, modal, frozen, subArea));
        }

        [Test]
        public void CanReplay_IgnoresAlreadyPlayed_ButNotRunningOrBusy()
        {
            Assert.IsTrue(DreamPrologueRules.CanReplay(false, false, false, false));
            Assert.IsFalse(DreamPrologueRules.CanReplay(true, false, false, false));
            Assert.IsFalse(DreamPrologueRules.CanReplay(false, true, false, false));
            Assert.IsFalse(DreamPrologueRules.CanReplay(false, false, true, false));
            Assert.IsFalse(DreamPrologueRules.CanReplay(false, false, false, true));
        }

        [Test]
        public void StartQuestId_IsTheFirstStoryQuestOfTheRealChain()
        {
            // 시작 조건이 "첫 퀘스트가 진행 0으로 활성"이라는 전제다 — 첫 퀘스트가 바뀌면 프롤로그가 영영 안 뜬다.
            var host = new GameObject("DreamPrologueTests_Quests");
            objects.Add(host);
            var mgr = host.AddComponent<TutorialQuestManager>();
            typeof(TutorialQuestManager).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mgr, null);
            TutorialQuest first = null;
            foreach (TutorialQuest q in mgr.GetAllQuests())
                if (q.category == QuestCategory.Story) { first = q; break; }
            Assert.AreEqual(DreamPrologueRules.StartQuestId, first.questId);
        }

        // ── 저작 데이터 ──

        [Test]
        public void IslandSnapshot_KeepsEveryAuthoredObjectAfterSanitize()
        {
            // 남이 만든 섬을 받을 때와 같은 정리를 거친다 — 겹침·경계 밖·모르는 ID는 조용히 버려지는데,
            // 그러면 분수 광장에 분수가 없는 꿈이 된다.
            IslandSnapshot snapshot = DreamPrologueData.BuildIslandSnapshot("하늘");
            int authored = snapshot.placed.Count;
            IslandSaveRules.SanitizeSnapshot(snapshot);
            Assert.AreEqual(authored, snapshot.placed.Count, "배치가 정리에서 빠졌다 — 겹침·경계·카탈로그 ID를 확인");
            Assert.AreEqual(DreamPrologueData.IslandObjects.Length, snapshot.placed.Count);
        }

        [Test]
        public void IslandSnapshot_KeepsEveryInsect_AndStaysWithinTheSlotLimit()
        {
            IslandSnapshot snapshot = DreamPrologueData.BuildIslandSnapshot("하늘");
            IslandSaveRules.SanitizeSnapshot(snapshot);
            Assert.AreEqual(DreamPrologueData.IslandInsects.Length, snapshot.insects.Count);
            Assert.LessOrEqual(DreamPrologueData.IslandInsects.Length, GameConstants.Island.MaxInsectSlots);
        }

        [Test]
        public void IslandSnapshot_LeavesTheArrivalCorridorOpen()
        {
            // 입구에서 분수까지 가운데 두 칸이 막히면 꿈의 마지막 목표에 닿을 수 없다.
            IslandSnapshot snapshot = DreamPrologueData.BuildIslandSnapshot("하늘");
            int half = IslandGrid.GridSize(snapshot.sizeLevel) / 2;
            for (int z = -half + 2; z < DreamPrologueData.FountainZ; z++)
                for (int x = -1; x <= 0; x++)
                    foreach (IslandPlacedRecord p in snapshot.placed)
                    {
                        IslandObjectDef def = IslandCatalog.Get(p.id);
                        if (!def.blocksMovement) continue;
                        IslandGrid.Footprint(def, p.rot, out int w, out int d);
                        Assert.IsFalse(IslandGrid.RectsOverlap(x, z, 1, 1, p.x, p.z, w, d),
                            $"{p.id}가 입구→분수 통로 ({x},{z})를 막는다");
                    }
        }

        [Test]
        public void IslandSnapshot_FountainIsWhereTheGoalMarkerPoints()
        {
            IslandSnapshot snapshot = DreamPrologueData.BuildIslandSnapshot("하늘");
            IslandPlacedRecord fountain = snapshot.placed.Find(p => p.id == "f_fountain");
            Assert.IsNotNull(fountain);
            Assert.AreEqual(DreamPrologueData.FountainX, fountain.x);
            Assert.AreEqual(DreamPrologueData.FountainZ, fountain.z);
        }

        [Test]
        public void ChampionInsect_IsMaxedOutAndStartsAtFullHealth()
        {
            InsectData data = NewSpecies(DreamPrologueData.AceInsectId, 150, 40, 30);
            PlayerInsectData pid = DreamPrologueData.BuildChampionInsect(data);
            Assert.AreEqual(15, pid.ivHp);
            Assert.AreEqual(15, pid.ivAtk);
            Assert.AreEqual(15, pid.ivDef);
            Assert.AreEqual(DreamPrologueData.AceLevel, pid.level);
            Assert.AreEqual(pid.GetTotalHp(data.baseHp), pid.currentHp, "-1(미초기화)로 두면 기절한 채 시작한다");
        }

        [Test]
        public void ChampionSkills_PickStrongestDamageFirst_AndKeepOneBuff()
        {
            InsectData data = NewSpecies("skill_probe", 100, 30, 20);
            var all = new List<InsectLearnableSkill>
            {
                Learn(NewSkill("jab", 20, SkillEffectType.Damage), 1),
                Learn(NewSkill("boost", 0, SkillEffectType.BuffAttack), 5),
                Learn(NewSkill("trait", 35, SkillEffectType.Damage), 9),
                Learn(NewSkill("burst", 55, SkillEffectType.Damage), 13),
                Learn(NewSkill("storm", 70, SkillEffectType.Damage), 17),
                Learn(NewSkill("signature", 90, SkillEffectType.Damage), 20),
            };
            data.learnset = all.ToArray();

            InsectSkill[] picked = DreamPrologueData.PickChampionSkills(data, DreamPrologueData.AceLevel);

            Assert.AreEqual(DreamPrologueData.SkillCount, picked.Length);
            Assert.AreEqual("signature", picked[0].skillId, "필살기가 맨 앞");
            Assert.AreEqual("storm", picked[1].skillId);
            Assert.AreEqual("burst", picked[2].skillId);
            Assert.AreEqual(SkillEffectType.BuffAttack, picked[3].effectType, "마지막 칸은 능력 상승기");
        }

        [Test]
        public void ChampionSkills_WithoutBuff_FillsWithDamage_AndUnlearnedAreSkipped()
        {
            InsectData data = NewSpecies("skill_probe2", 100, 30, 20);
            data.learnset = new[]
            {
                Learn(NewSkill("a", 10, SkillEffectType.Damage), 1),
                Learn(NewSkill("b", 20, SkillEffectType.Damage), 5),
                Learn(NewSkill("c", 30, SkillEffectType.Damage), 9),
                Learn(NewSkill("d", 40, SkillEffectType.Damage), 13),
                Learn(NewSkill("late", 99, SkillEffectType.Damage), 90),   // 아직 못 배운 기술
            };
            InsectSkill[] picked = DreamPrologueData.PickChampionSkills(data, 80);
            Assert.AreEqual(4, picked.Length);
            foreach (InsectSkill s in picked) Assert.AreNotEqual("late", s.skillId);
        }

        [Test]
        public void ChampionSkills_NoLearnset_IsEmpty_SoTheSpeciesOwnSkillsAreUsed()
        {
            InsectData data = NewSpecies("skill_probe3", 100, 30, 20);
            Assert.AreEqual(0, DreamPrologueData.PickChampionSkills(data, 80).Length);
            Assert.AreEqual(0, DreamPrologueData.PickChampionSkills(null, 80).Length);
        }

        // ── 샌드박스 전투는 아무것도 남기지 않는다 ──

        private sealed class Counts
        {
            public int battleEnded;
            public int duelEnded;
            public bool won;
        }

        private InsectBattleController NewController(out PlayerCandyInventory candy, out PlayerProgressController progress,
            out PlayerCurrencyWallet wallet, out PlayerInsectCollection collection, out Counts counts)
        {
            // 보상 지급처를 전부 붙여 둔다 — 샌드박스가 건드리면 그 값이 바뀌어 잡힌다.
            var go = new GameObject("DreamSandboxFixture");
            go.SetActive(false);   // Awake의 디스크 로드를 막는다
            objects.Add(go);
            candy = go.AddComponent<PlayerCandyInventory>();
            progress = go.AddComponent<PlayerProgressController>();
            wallet = go.AddComponent<PlayerCurrencyWallet>();
            collection = go.AddComponent<PlayerInsectCollection>();
            typeof(PlayerCandyInventory).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(candy, new PlayerCandyData());
            typeof(PlayerProgressController).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(progress, new PlayerProgressData { level = 3, currentXp = 7 });
            typeof(PlayerCurrencyWallet).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(wallet, new PlayerCurrencyData { coins = 11, gems = 2 });

            var controller = go.AddComponent<InsectBattleController>();
            controller.AutoWire(collection, candy, progress, null);
            controller.AutoWire(wallet);
            var c = new Counts();
            controller.BattleEnded += won => { c.battleEnded++; c.won = won; };
            controller.DuelEnded += won => c.duelEnded++;
            counts = c;
            return controller;
        }

        private InsectBattleController StartSandbox(out InsectBattleStats player, out InsectBattleStats enemy,
            out PlayerCandyInventory candy, out PlayerProgressController progress, out PlayerCurrencyWallet wallet,
            out PlayerInsectCollection collection, out Counts counts, out PlayerInsectData champion)
        {
            InsectBattleController controller = NewController(out candy, out progress, out wallet, out collection, out counts);
            InsectData ace = NewSpecies("dream_ace_probe", 150, 40, 30);
            InsectData foe = NewSpecies("dream_foe_probe", 400, 40, 30);
            InsectSkill hit = NewSkill("hit", 40, SkillEffectType.Damage);
            ace.skills = new[] { hit };
            foe.skills = new[] { hit };
            champion = DreamPrologueData.BuildChampionInsect(ace);

            InsectBattleStats p = null, e = null;
            bool started = controller.StartSandbox(ace, 80, foe, 72, new[] { hit }, champion, (a, b) => { p = a; e = b; });
            Assert.IsTrue(started);
            player = p;
            enemy = e;
            return controller;
        }

        [Test]
        public void Sandbox_PlayedToTheEnd_PaysNothingAndTouchesNoSaveState()
        {
            var controller = StartSandbox(out var player, out var enemy, out var candy, out var progress,
                out var wallet, out var collection, out var counts, out var champion);
            int candyBefore = candy.Candies;
            int levelBefore = progress.Level;
            int xpBefore = progress.CurrentXp;
            int coinsBefore = wallet.Coins;
            int ownedBefore = collection.GetAllOwned().Count;
            int hpBefore = champion.currentHp;
            Assert.IsTrue(controller.IsSandbox);

            for (int i = 0; i < 12 && counts.battleEnded == 0; i++)
            {
                if (controller.CanUseSkill(0)) controller.UseSkill(0); else controller.UseBasicAttack();
            }

            Assert.AreEqual(1, counts.battleEnded, "꿈의 전투가 끝나지 않았다");
            Assert.IsTrue(counts.won, "챔피언이 졌다");
            Assert.AreEqual(0, counts.duelEnded, "DuelEnded를 쏘면 NPC 대결 보상·쿨다운·간부 격파 기록이 움직인다");
            Assert.AreEqual(candyBefore, candy.Candies, "캔디가 올랐다");
            Assert.AreEqual(levelBefore, progress.Level, "레벨이 올랐다");
            Assert.AreEqual(xpBefore, progress.CurrentXp, "경험치가 올랐다");
            Assert.AreEqual(coinsBefore, wallet.Coins, "코인이 올랐다");
            Assert.AreEqual(ownedBefore, collection.GetAllOwned().Count, "곤충이 컬렉션에 들어갔다");
            Assert.AreEqual(0, controller.GetLastCandyReward());
            Assert.AreEqual(0, controller.GetLastExpReward());
            Assert.IsFalse(controller.GetLastCaptureAttempted(), "승리 뒤 포획 판정이 돌았다");
            Assert.AreEqual(hpBefore, champion.currentHp, "꿈속의 피해가 개체의 저장 HP에 새었다");
        }

        [Test]
        public void Sandbox_EndsWithinTheActionCap_AndTheChampionNeverFalls()
        {
            var controller = StartSandbox(out var player, out var enemy, out _, out _, out _, out _, out var counts, out _);
            int floor = SandboxBattleRules.HpFloor(player.MaxHp);
            int actions = 0;
            while (counts.battleEnded == 0 && actions < 20)
            {
                if (controller.CanUseSkill(0)) controller.UseSkill(0); else controller.UseBasicAttack();
                actions++;
                Assert.GreaterOrEqual(player.CurrentHp, floor, $"{actions}번째 행동 뒤 챔피언이 하한 밑으로 내려갔다");
            }
            Assert.IsTrue(counts.won);
            Assert.GreaterOrEqual(actions, SandboxBattleRules.MinPlayerActions, "너무 일찍 끝났다 — 스킬을 눌러 볼 틈이 없다");
            Assert.LessOrEqual(actions, SandboxBattleRules.MaxPlayerActions + 2, "끝나지 않는 꿈 — 쿨다운으로 기본 공격이 낀 턴을 감안해도 이 안에는 끝나야 한다");
        }

        [Test]
        public void Sandbox_CannotEscape()
        {
            var controller = StartSandbox(out _, out _, out _, out _, out _, out _, out var counts, out _);
            Assert.IsFalse(controller.TryEscape());
            Assert.AreEqual(0, counts.battleEnded, "도주로 꿈이 끝났다");
        }

        [Test]
        public void Sandbox_ForceVictory_EndsThroughTheNormalPath()
        {
            var controller = StartSandbox(out _, out _, out var candy, out _, out _, out _, out var counts, out _);
            int candyBefore = candy.Candies;
            controller.ForceSandboxVictory();
            Assert.AreEqual(1, counts.battleEnded);
            Assert.IsTrue(counts.won);
            Assert.AreEqual(candyBefore, candy.Candies);
            controller.ForceSandboxVictory();   // 두 번째는 무시된다
            Assert.AreEqual(1, counts.battleEnded);
        }

        [Test]
        public void NextOrdinaryBattle_DoesNotInheritTheSandboxFlag()
        {
            // 표지가 남으면 이후 모든 전투가 보상 0·BattleWin 없음이 된다 — 가장 조용하고 가장 큰 사고다.
            var controller = StartSandbox(out _, out _, out _, out _, out _, out _, out var counts, out _);
            controller.ForceSandboxVictory();
            Assert.IsTrue(controller.IsSandbox);

            InsectData a = NewSpecies("after_a", 100, 30, 20);
            InsectData b = NewSpecies("after_b", 100, 30, 20);
            Assert.IsTrue(controller.StartDuel(a, 5, b, 5));
            Assert.IsFalse(controller.IsSandbox);
        }

        // ── 꿈 중에는 꿈 밖이 움직이지 않는다 ──

        [Test]
        public void QuestProgress_IsSuspendedWhileTheDreamRuns()
        {
            var host = new GameObject("DreamPrologueTests_QuestSuspend");
            objects.Add(host);
            var mgr = host.AddComponent<TutorialQuestManager>();
            typeof(TutorialQuestManager).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mgr, null);
            typeof(TutorialQuestManager).GetField("tutorialSessionStarted", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mgr, true);
            var progress = (Dictionary<string, int>)typeof(TutorialQuestManager).GetField("questProgress", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mgr);
            progress.Clear();

            DreamPrologueState.Begin();
            mgr.NotifyAction(QuestType.Battle);
            mgr.NotifyCapture(InsectRarity.Common);
            Assert.AreEqual(0, progress.Count, "꿈속의 행동이 퀘스트에 들어갔다");

            DreamPrologueState.End();
            // 꿈이 끝나면 다시 센다 — 영영 멈춰 있으면 안 된다.
            typeof(TutorialQuestManager).GetField("activeQuestId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mgr, "q_capture3");
            mgr.NotifyCapture(InsectRarity.Common);
            Assert.Greater(progress.Count, 0);
        }

        [Test]
        public void State_BeginAndEnd_Toggle()
        {
            Assert.IsFalse(DreamPrologueState.Active);
            DreamPrologueState.Begin();
            Assert.IsTrue(DreamPrologueState.Active);
            DreamPrologueState.End();
            Assert.IsFalse(DreamPrologueState.Active);
        }

        // ── 실제 곤충 DB로 ──
        //
        // 위 테스트는 가짜 종으로 규칙을 본다. 아래는 부트스트랩이 만드는 **진짜 DB**로 저작 데이터를 확인한다 —
        // 종 ID 오타는 "그 곤충만 조용히 안 나오는" 결함이고, 실제 능력치·기술에서 꿈의 전투가 정말
        // 3~4턴에 이기고 끝나는지는 가짜 종으로는 알 수 없다.

        private InsectDatabase RealDatabase()
        {
            var host = new GameObject("DreamPrologueTests_RealDb");
            host.SetActive(false);   // Awake(월드 생성)를 막고 private 생성 함수만 부른다
            objects.Add(host);
            var bootstrap = host.AddComponent<PlaySceneBootstrap>();
            var db = (InsectDatabase)typeof(PlaySceneBootstrap)
                .GetMethod("EnsureExpandedDatabase", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bootstrap, null);
            Assert.IsNotNull(db);
            objects.Add(db);
            return db;
        }

        [Test]
        public void EverySpeciesId_ExistsInTheRealDatabase()
        {
            InsectDatabase db = RealDatabase();
            var ids = new List<string> { DreamPrologueData.AceInsectId, DreamPrologueData.ChallengerInsectId };
            foreach (var s in DreamPrologueData.IslandInsects) ids.Add(s.insectId);
            foreach (string id in ids)
                Assert.IsNotNull(db.GetById(id), $"'{id}'가 곤충 DB에 없다 — 그 곤충이 꿈에서 조용히 빠진다");
        }

        [Test]
        public void IslandInsects_AreAllHighRarity()
        {
            // "제일 좋은 곤충들"이 섬의 약속이다 — 흔한 곤충이 섞이면 꿈의 값어치가 떨어진다.
            InsectDatabase db = RealDatabase();
            foreach (var s in DreamPrologueData.IslandInsects)
                Assert.GreaterOrEqual((int)db.GetById(s.insectId).rarity, (int)InsectRarity.Epic, s.insectId);
            Assert.GreaterOrEqual((int)db.GetById(DreamPrologueData.AceInsectId).rarity, (int)InsectRarity.Legendary);
        }

        [Test]
        public void RealChampion_HasFourUsableSkills_SignatureFirst()
        {
            InsectDatabase db = RealDatabase();
            InsectData ace = db.GetById(DreamPrologueData.AceInsectId);
            InsectSkill[] skills = DreamPrologueData.PickChampionSkills(ace, DreamPrologueData.AceLevel);

            Assert.AreEqual(DreamPrologueData.SkillCount, skills.Length, "전투 화면의 스킬 카드가 비는 칸이 생긴다");
            Assert.IsTrue(skills[0].isSignatureSkill || skills[0].power >= skills[1].power, "가장 강한 기술이 맨 앞이어야 한다");
        }

        [Test]
        public void RealSandboxBattle_WinsInThreeToFourActions_WithoutFalling()
        {
            InsectDatabase db = RealDatabase();
            InsectData ace = db.GetById(DreamPrologueData.AceInsectId);
            InsectData foe = db.GetById(DreamPrologueData.ChallengerInsectId);
            InsectBattleController controller = NewController(out _, out _, out _, out _, out var counts);
            PlayerInsectData champion = DreamPrologueData.BuildChampionInsect(ace);
            InsectSkill[] skills = DreamPrologueData.PickChampionSkills(ace, DreamPrologueData.AceLevel);

            InsectBattleStats player = null;
            Assert.IsTrue(controller.StartSandbox(ace, DreamPrologueData.AceLevel, foe, DreamPrologueData.ChallengerLevel,
                skills, champion, (p, e) => player = p));

            int floor = SandboxBattleRules.HpFloor(player.MaxHp);
            int actions = 0;
            while (counts.battleEnded == 0 && actions < 12)
            {
                bool acted = false;
                for (int i = 0; i < skills.Length && !acted; i++)
                    if (controller.CanUseSkill(i)) { controller.UseSkill(i); acted = true; }
                if (!acted) controller.UseBasicAttack();
                actions++;
                Assert.GreaterOrEqual(player.CurrentHp, floor, $"{actions}번째 행동 뒤 챔피언이 하한 밑으로 내려갔다");
            }

            Assert.IsTrue(counts.won, "실제 곤충으로는 챔피언이 이기지 못했다");
            Assert.GreaterOrEqual(actions, SandboxBattleRules.MinPlayerActions);
            Assert.LessOrEqual(actions, SandboxBattleRules.MaxPlayerActions,
                "스킬을 매 턴 쓸 수 있는 실제 구성에서는 마지막 일격 상한(" + SandboxBattleRules.MaxPlayerActions + ")에 끝나야 한다");
        }

        // ── 도우미 ──

        private InsectData NewSpecies(string id, int hp, int atk, int def)
        {
            var d = ScriptableObject.CreateInstance<InsectData>();
            objects.Add(d);
            d.insectId = id;
            d.displayName = id;
            d.rarity = InsectRarity.Legendary;
            d.baseHp = hp;
            d.baseAtk = atk;
            d.baseDef = def;
            d.primaryType = InsectElement.None;
            return d;
        }

        private InsectSkill NewSkill(string id, int power, SkillEffectType type)
        {
            var s = ScriptableObject.CreateInstance<InsectSkill>();
            objects.Add(s);
            s.skillId = id;
            s.displayName = id;
            s.power = Mathf.Max(1, power);
            s.effectType = type;
            s.element = InsectElement.None;
            s.accuracy = 1f;
            s.cooldownTurns = 0;
            s.effectValue = 0.3f;
            s.effectDurationTurns = 2;
            return s;
        }

        private static InsectLearnableSkill Learn(InsectSkill skill, int level)
        {
            return new InsectLearnableSkill { skillId = skill.skillId, learnLevel = level, skill = skill };
        }
    }
}
#endif
