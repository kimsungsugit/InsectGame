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
    }
}
#endif
