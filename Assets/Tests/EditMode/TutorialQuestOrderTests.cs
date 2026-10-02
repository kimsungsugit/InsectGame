#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 배열 중간 삽입분의 소급 완료 판정. <b>경계가 미묘해서</b> 고정해 둔다 —
    /// 너무 넓으면 아직 할 차례인 퀘스트를 보상 없이 삼키고(플레이어는 그 단계를 영영 못 한다),
    /// 너무 좁으면 이미 진행한 세이브가 뒤로 되돌아간다.
    /// </summary>
    [TestFixture]
    public class TutorialQuestOrderTests
    {
        private static TutorialQuest Q(string id, QuestCategory category = QuestCategory.Story)
        {
            return new TutorialQuest { questId = id, category = category };
        }

        private static System.Func<string, bool> Done(params string[] ids)
        {
            var set = new HashSet<string>(ids);
            return id => set.Contains(id);
        }

        private static readonly TutorialQuest[] Chain =
        {
            Q("q_move"), Q("q_talk_elder"), Q("q_approach"), Q("q_collection"), Q("q_dex"),
        };

        [Test]
        public void CollectBackfillTargets_NewSave_BackfillsNothing()
        {
            // 아무것도 안 깬 세이브에서 소급이 돌면 튜토리얼 전체가 사라진다.
            Assert.AreEqual(0, TutorialQuestOrder.CollectBackfillTargets(Chain, Done()).Count);
        }

        [Test]
        public void CollectBackfillTargets_InsertedQuestBeforeProgress_IsBackfilled()
        {
            // q_dex까지 깬 세이브에 q_talk_elder가 끼어들었다 — 이미 지나간 단계다.
            List<string> targets = TutorialQuestOrder.CollectBackfillTargets(
                Chain, Done("q_move", "q_approach", "q_collection", "q_dex"));

            CollectionAssert.AreEqual(new[] { "q_talk_elder" }, targets);
        }

        [Test]
        public void CollectBackfillTargets_NextInLine_IsNotSwallowed()
        {
            // **가장 중요한 경계다.** q_move만 깬 세이브에서 q_talk_elder는 아직 할 차례다 —
            // 여기서 소급하면 그 퀘스트를 보상 없이 잃고 다시 할 방법이 없다.
            Assert.AreEqual(0,
                TutorialQuestOrder.CollectBackfillTargets(Chain, Done("q_move")).Count);
        }

        [Test]
        public void CollectBackfillTargets_CompletedRun_KeepsCompletion()
        {
            // 완주한 세이브 — 끼워 넣은 하나만 채우고 끝난다(완주 상태가 유지된다).
            List<string> targets = TutorialQuestOrder.CollectBackfillTargets(
                Chain, Done("q_move", "q_approach", "q_collection", "q_dex"));

            Assert.AreEqual(1, targets.Count);
        }

        [Test]
        public void CollectBackfillTargets_MultipleGaps_AreAllFilled()
        {
            // 한 번에 둘을 끼워도 뒤엣것 기준으로 함께 채운다.
            List<string> targets = TutorialQuestOrder.CollectBackfillTargets(
                Chain, Done("q_move", "q_dex"));

            CollectionAssert.AreEqual(new[] { "q_talk_elder", "q_approach", "q_collection" }, targets);
        }

        [Test]
        public void CollectBackfillTargets_SideQuests_AreIgnored()
        {
            // 서브 퀘스트는 다중 활성이라 순서 개념이 없다 — 미완료로 남아야 한다.
            var mixed = new[]
            {
                Q("q_move"), Q("s_capture_wild", QuestCategory.Side), Q("q_approach"), Q("q_dex"),
            };

            List<string> targets = TutorialQuestOrder.CollectBackfillTargets(
                mixed, Done("q_move", "q_dex"));

            CollectionAssert.AreEqual(new[] { "q_approach" }, targets);
        }

        [Test]
        public void CollectBackfillTargets_NullInput_IsSafe()
        {
            Assert.AreEqual(0, TutorialQuestOrder.CollectBackfillTargets(null, Done("q_move")).Count);
            Assert.AreEqual(0, TutorialQuestOrder.CollectBackfillTargets(Chain, null).Count);
        }

        // ── 미리 세기 ──
        //
        // 예전엔 활성 퀘스트만 진행을 셌다. 도감을 여는 퀘스트 중에 잡은 세 마리는 "3마리 포획"에
        // 들어가지 않아 차례가 오면 다시 잡아야 했다. 지금은 아직 차례가 안 온 스토리 퀘스트에도 센다.

        private static TutorialQuest Typed(string id, QuestType type, int target = 1,
            QuestCategory category = QuestCategory.Story)
        {
            return new TutorialQuest { questId = id, type = type, targetCount = target, category = category };
        }

        private static readonly TutorialQuest[] BankChain =
        {
            Typed("q_approach", QuestType.Capture),
            Typed("q_dex", QuestType.OpenDex, 1, QuestCategory.Side),
            Typed("q_capture3", QuestType.Capture, 3),
            Typed("q_levelup", QuestType.LevelUp),
            Typed("q_battle", QuestType.Battle),
            Typed("q_battle3", QuestType.Battle, 3),
            Typed("q_capture_rare", QuestType.CaptureRare),
            Typed("q_blight_first", QuestType.CleanseBlight),
            Typed("q_blight_both", QuestType.CleanseBlight),
            Typed("s_capture_wild", QuestType.Capture, 5, QuestCategory.Side),
        };

        private static List<string> BankIds(string active, QuestType action,
            InsectGame.Data.InsectRarity rarity, params string[] done)
        {
            var into = new List<TutorialQuest>();
            TutorialQuestOrder.CollectBankTargets(BankChain, Done(done), active, action, rarity, into);
            return into.ConvertAll(q => q.questId);
        }

        [Test]
        public void CollectBankTargets_Capture_CountsTowardEveryUpcomingCaptureQuest_ButNotTheActiveOne()
        {
            // 활성 퀘스트는 원래 경로가 올린다 — 여기서도 세면 한 번 잡고 두 번 올라간다.
            List<string> ids = BankIds("q_approach", QuestType.Capture, InsectGame.Data.InsectRarity.Common);
            CollectionAssert.AreEqual(new[] { "q_capture3" }, ids);
        }

        [Test]
        public void CollectBankTargets_UncommonCapture_AlsoCountsTowardRareCaptureQuest()
        {
            List<string> ids = BankIds("q_approach", QuestType.Capture, InsectGame.Data.InsectRarity.Uncommon);
            CollectionAssert.AreEqual(new[] { "q_capture3", "q_capture_rare" }, ids);
        }

        [Test]
        public void CollectBankTargets_CompletedAndSideQuests_AreSkipped()
        {
            // 서브(s_capture_wild)는 자기 경로로 세므로 대상이 아니고, 끝난 퀘스트는 더 올릴 것이 없다.
            List<string> ids = BankIds("q_levelup", QuestType.Capture, InsectGame.Data.InsectRarity.Common,
                "q_approach", "q_capture3");
            Assert.AreEqual(0, ids.Count);
        }

        [Test]
        public void CollectBankTargets_BattleWhileDoingSomethingElse_IsKeptForLater()
        {
            List<string> ids = BankIds("q_capture3", QuestType.Battle, InsectGame.Data.InsectRarity.Common, "q_approach");
            CollectionAssert.AreEqual(new[] { "q_battle", "q_battle3" }, ids);
        }

        [Test]
        public void CollectBankTargets_BlightCleanse_IsNeverBanked()
        {
            // 「하나 무너뜨리기」와 「하나 더」가 이어져 있다 — 미리 세면 첫 정화 하나가 둘을 한꺼번에 깬다.
            Assert.IsFalse(TutorialQuestOrder.IsBankable(QuestType.CleanseBlight));
            Assert.AreEqual(0, BankIds(null, QuestType.CleanseBlight, InsectGame.Data.InsectRarity.Common).Count);
        }

        [TestCase(QuestType.Movement)]
        [TestCase(QuestType.VisitRegion)]
        [TestCase(QuestType.VisitSubArea)]
        [TestCase(QuestType.TalkToElder)]
        [TestCase(QuestType.DefeatGuardian)]
        [TestCase(QuestType.SetTeam)]
        public void IsBankable_MomentBoundGoals_AreNot(QuestType type)
        {
            // 그 퀘스트가 가리키는 순간의 행동이어야 하는 것들 — 초원에 들어간 것이 "연못에 가 보세요"를 깨면 안 된다.
            Assert.IsFalse(TutorialQuestOrder.IsBankable(type));
        }

        [Test]
        public void CountsToward_CaptureRarity_MatchesOnlyThatRarity()
        {
            var pack = new TutorialQuest
            {
                questId = "pack", type = QuestType.CaptureRarity,
                requiredRarity = InsectGame.Data.InsectRarity.Rare
            };
            Assert.IsTrue(TutorialQuestOrder.CountsToward(pack, QuestType.Capture, InsectGame.Data.InsectRarity.Rare));
            Assert.IsFalse(TutorialQuestOrder.CountsToward(pack, QuestType.Capture, InsectGame.Data.InsectRarity.Epic));
        }

        [Test]
        public void CollectBankTargets_NullInput_IsSafe()
        {
            var into = new List<TutorialQuest> { Typed("stale", QuestType.Capture) };
            TutorialQuestOrder.CollectBankTargets(null, Done(), null, QuestType.Capture,
                InsectGame.Data.InsectRarity.Common, into);
            Assert.AreEqual(0, into.Count, "이전 호출의 대상이 남으면 엉뚱한 퀘스트가 올라간다");
        }

        // ── 실제 스토리 체인 ──
        //
        // 수문장 전까지는 "하는 일"만 남긴다. 창을 한 번 열면 끝나는 과제가 체인에 다시 끼면 첫 전투가
        // 뒤로 밀린다(예전엔 9번째였다). 순서 자체도 고정한다 — 남은 퀘스트의 **상대 순서**가 바뀌면
        // 소급 완료가 기존 세이브에서 아직 할 차례인 퀘스트를 보상 없이 삼킨다.

        private static TutorialQuest[] RealQuests()
        {
            var host = new UnityEngine.GameObject("QuestOrderProbe");
            try
            {
                var mgr = host.AddComponent<TutorialQuestManager>();
                typeof(TutorialQuestManager).GetMethod("Initialize",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(mgr, null);
                return mgr.GetAllQuests();
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void RealChain_UpToFirstGuardian_IsActionsOnly_InTheOldRelativeOrder()
        {
            var story = new List<string>();
            foreach (TutorialQuest q in RealQuests())
            {
                if (q.category != QuestCategory.Story) continue;
                story.Add(q.questId);
                if (q.questId == "q_guardian1") break;
            }

            CollectionAssert.AreEqual(new[]
            {
                "q_move", "q_talk_elder", "q_approach", "q_capture3", "q_levelup",
                "q_battle", "q_battle3", "q_capture_rare", "q_guardian1",
            }, story);
        }

        [TestCase("q_collection")]
        [TestCase("q_dex")]
        [TestCase("q_equip")]
        [TestCase("q_item")]
        [TestCase("q_training")]
        [TestCase("q_team")]
        public void RealChain_LookAroundChores_AreOptionalOneShotSideQuests(string questId)
        {
            TutorialQuest quest = System.Array.Find(RealQuests(), q => q.questId == questId);

            Assert.IsNotNull(quest, "ID를 지우면 그 퀘스트를 깬 세이브의 완료 기록이 떠돈다");
            Assert.AreEqual(QuestCategory.Side, quest.category);
            Assert.IsFalse(quest.repeatable);
            Assert.IsFalse(string.IsNullOrEmpty(quest.prerequisiteQuestId),
                "선행이 없으면 첫 포획도 하기 전에 서브 목록이 과제로 찬다");
        }

        [Test]
        public void RealChain_EveryStoryPrerequisite_IsAStoryQuest()
        {
            // 스토리가 서브(둘러보기) 과제를 선행으로 물면 그 과제를 안 한 사람의 본편이 멈춘다.
            TutorialQuest[] quests = RealQuests();
            foreach (TutorialQuest q in quests)
            {
                if (q.category != QuestCategory.Story || string.IsNullOrEmpty(q.prerequisiteQuestId)) continue;
                TutorialQuest prereq = System.Array.Find(quests, p => p.questId == q.prerequisiteQuestId);
                Assert.IsNotNull(prereq, $"{q.questId}: 선행 {q.prerequisiteQuestId} 없음");
                Assert.AreEqual(QuestCategory.Story, prereq.category, $"{q.questId}의 선행 {prereq.questId}가 서브다");
            }
        }

        // ── 이동 퀘스트 진행 규칙 ──
        //
        // 옛 판정은 `한 프레임 이동량 > 1m` 하나였다. 플레이어 속도는 8m/s(의상 보정 최대 ×2)라
        // 60fps에서 프레임당 0.13~0.27m — **게임의 첫 퀘스트가 시키는 대로 걸어서는 절대
        // 참이 되지 않았다.** 참이 되는 경우는 워프뿐인데 그건 "첫 걸음"이 아니다.

        [Test]
        public void Movement_WalkingOneFrame_DoesNotCompleteButAccumulates()
        {
            float acc = 0f;
            // 8m/s ÷ 60fps
            Assert.IsFalse(MovementProgress.Accumulate(8f / 60f, ref acc));
            Assert.Greater(acc, 0f);
        }

        [Test]
        public void Movement_WalkingEnoughFrames_Completes()
        {
            float acc = 0f;
            int frames = 0;
            bool done = false;
            // 3m를 8m/s로 걸으면 0.375초 — 60fps에서 23프레임이면 충분하다.
            while (frames < 60 && !done)
            {
                done = MovementProgress.Accumulate(8f / 60f, ref acc);
                frames++;
            }
            Assert.IsTrue(done, "걸어서 이동 퀘스트를 채우지 못한다");
            Assert.AreEqual(0f, acc, 0.001f, "채운 뒤에는 누적이 비어야 한다");
        }

        [Test]
        public void Movement_Teleport_IsNotCounted()
        {
            // 서브에리어 진입은 2000m 점프다. 그걸 "걸었다"로 세면 안 된다.
            float acc = 0f;
            Assert.IsFalse(MovementProgress.Accumulate(2000f, ref acc));
            Assert.AreEqual(0f, acc, 0.001f);
        }

        [Test]
        public void Movement_TeleportThreshold_LeavesNormalWalkingIntact()
        {
            // 상한이 정상 이동을 잘라내면 안 된다 — 8m/s가 한 프레임에 5m를 가려면
            // 0.6초짜리 프레임이어야 한다.
            Assert.Greater(MovementProgress.TeleportMeters, 8f / 30f);
            Assert.Greater(MovementProgress.TeleportMeters, MovementProgress.RequiredMeters * 0.5f);
        }

        [Test]
        public void Movement_NegativeDistance_IsIgnored()
        {
            float acc = 1f;
            Assert.IsFalse(MovementProgress.Accumulate(-5f, ref acc));
            Assert.AreEqual(1f, acc, 0.001f);
        }
    }
}
#endif
