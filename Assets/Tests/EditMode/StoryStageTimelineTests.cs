#if UNITY_EDITOR
using InsectGame.NPC;
using InsectGame.Story;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// NPC 연출의 시간 계산. 여기서 고정하는 건 <b>하드 타임아웃이 항상 존재하고 유한한가</b>다 —
    /// 입장 연출은 끝나야 대사가 뜨므로, 안 끝나면 그 비트가 <c>pendingBeatId</c>에 갇혀
    /// 캠페인이 영구 정지한다. 실제 이동·건너뛰기 복귀는 기기 확인 대상.
    /// </summary>
    [TestFixture]
    public class StoryStageTimelineTests
    {
        [Test]
        public void SequenceTimeout_NullOrEmpty_IsMinimum()
        {
            Assert.AreEqual(StoryStageTimeline.MinSequenceSeconds,
                StoryStageTimeline.SequenceTimeoutSeconds(null), 0.001f);
            Assert.AreEqual(StoryStageTimeline.MinSequenceSeconds,
                StoryStageTimeline.SequenceTimeoutSeconds(new StoryStageStep[0]), 0.001f);
        }

        [Test]
        public void SequenceTimeout_IsAlwaysWithinBounds()
        {
            // 스텝이 아무리 많아도 상한을 넘지 않는다 — 조작을 그보다 오래 뺏지 않는다는 약속이다.
            var many = new StoryStageStep[12];
            for (int i = 0; i < many.Length; i++)
                many[i] = StoryStageStep.MoveTo("village_elder", Vector3.forward);

            float timeout = StoryStageTimeline.SequenceTimeoutSeconds(many);
            Assert.AreEqual(StoryStageTimeline.MaxSequenceSeconds, timeout, 0.001f);
            Assert.GreaterOrEqual(timeout, StoryStageTimeline.MinSequenceSeconds);
        }

        [Test]
        public void SequenceTimeout_ExceedsSumOfSteps()
        {
            // 스텝 합보다 커야 정상 재생이 타임아웃에 잘리지 않는다.
            var steps = new[]
            {
                StoryStageStep.Face("catcher_rival", 0.2f),
                StoryStageStep.Play("catcher_rival", NpcGesture.Wave),
                StoryStageStep.Pause(0.5f),
            };

            float sum = 0f;
            for (int i = 0; i < steps.Length; i++) sum += StoryStageTimeline.WorstCaseSeconds(steps[i]);

            Assert.Greater(StoryStageTimeline.SequenceTimeoutSeconds(steps), sum);
        }

        [Test]
        public void WorstCase_Warp_IsInstant()
        {
            // 즉시 스텝은 시간을 세지 않는다(다음 Update가 넘긴다).
            Assert.AreEqual(0f,
                StoryStageTimeline.WorstCaseSeconds(StoryStageStep.Warp("catcher_rival", Vector3.back)),
                0.001f);
        }

        [Test]
        public void WorstCase_Move_UsesNpcTimeout()
        {
            // 이동은 도착 판정이 안 올 수 있으므로 VillagerNpc의 이동 타임아웃과 같은 값을 쓴다.
            Assert.AreEqual(StoryStageTimeline.MoveTimeoutSeconds,
                StoryStageTimeline.WorstCaseSeconds(StoryStageStep.MoveTo("village_elder", Vector3.zero)),
                0.001f);
            Assert.AreEqual(StoryStageTimeline.MoveTimeoutSeconds,
                StoryStageTimeline.WorstCaseSeconds(StoryStageStep.GoHome("village_elder")),
                0.001f);
        }

        [Test]
        public void WorstCase_Gesture_CoversGestureDuration()
        {
            StoryStageStep step = StoryStageStep.Play("village_elder", NpcGesture.Wave);
            Assert.AreEqual(NpcGesturePose.DurationOf(NpcGesture.Wave),
                StoryStageTimeline.WorstCaseSeconds(step), 0.001f);
        }

        [Test]
        public void WorstCase_Wait_UsesDuration()
        {
            Assert.AreEqual(1.25f, StoryStageTimeline.WorstCaseSeconds(StoryStageStep.Pause(1.25f)), 0.001f);
            // 음수 저작 실수가 합을 깎아 타임아웃을 줄이지 못하게 한다.
            Assert.AreEqual(0f, StoryStageTimeline.WorstCaseSeconds(StoryStageStep.Pause(-3f)), 0.001f);
        }

        [Test]
        public void Library_EveryDeclaredStage_IsDispatched()
        {
            // 상수만 선언하고 switch에 case가 없으면 런타임에 LogWarning만 찍고 조용히 안 나온다.
            // (story_lint가 소스를 정규식으로 보는 것과 별개로, 여기선 실제 호출로 확인한다.)
            AssertStageExists(StoryStageLibrary.Ch1ElderGreet);
            AssertStageExists(StoryStageLibrary.Ch1RivalEnter);
            AssertStageExists(StoryStageLibrary.Ch1RivalExit);
        }

        [Test]
        public void Library_UnknownStage_ReturnsFalse()
        {
            Assert.IsFalse(StoryStageLibrary.TryGet("st_does_not_exist", out _));
            Assert.IsFalse(StoryStageLibrary.TryGet(null, out _));
        }

        [Test]
        public void Library_RivalEnter_WarpsBeforeWalking()
        {
            // 이 비트의 트리거(CaptureInsect)는 위치와 무관하게 터진다. 먼저 무대 밖으로 옮기지
            // 않으면 라온이 지도 반대편에 있는 채로 걸어오려다 타임아웃에 잘린다.
            Assert.IsTrue(StoryStageLibrary.TryGet(StoryStageLibrary.Ch1RivalEnter, out StoryStageStep[] steps));
            Assert.AreEqual(StageAction.WarpToOffset, steps[0].action);

            int walkIndex = -1;
            for (int i = 0; i < steps.Length; i++)
                if (steps[i].action == StageAction.MoveToOffset) { walkIndex = i; break; }
            Assert.Greater(walkIndex, 0, "Warp 뒤에 걸어 들어오는 스텝이 있어야 한다");
        }

        private static void AssertStageExists(string stageId)
        {
            Assert.IsTrue(StoryStageLibrary.TryGet(stageId, out StoryStageStep[] steps),
                $"{stageId}가 TryGet switch에 배선되지 않았다");
            Assert.IsNotNull(steps);
            Assert.Greater(steps.Length, 0, $"{stageId}에 스텝이 없다");
            Assert.LessOrEqual(StoryStageTimeline.SequenceTimeoutSeconds(steps),
                StoryStageTimeline.MaxSequenceSeconds + 0.001f);
        }
    }
}
#endif
