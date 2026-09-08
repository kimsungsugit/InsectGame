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
