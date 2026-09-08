#if UNITY_EDITOR
using InsectGame.Opening;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    [TestFixture]
    public class OpeningSequenceTests
    {
        [Test]
        public void TryBegin_FirstColdStart_AllowsPlayback()
        {
            OpeningAutoPlayPolicy policy = new OpeningAutoPlayPolicy();

            bool shouldPlay = policy.TryBegin(OpeningPlaybackRequest.ColdStart);

            Assert.IsTrue(shouldPlay);
        }

        [Test]
        public void TryBegin_SecondColdStart_StillAllowsPlayback()
        {
            OpeningAutoPlayPolicy policy = new OpeningAutoPlayPolicy();
            Assert.IsTrue(policy.TryBegin(OpeningPlaybackRequest.ColdStart));

            bool shouldPlayAgain = policy.TryBegin(OpeningPlaybackRequest.ColdStart);

            Assert.IsTrue(shouldPlayAgain);
        }

        [Test]
        public void TryBegin_ManualReplay_AlwaysAllowsPlayback()
        {
            OpeningAutoPlayPolicy policy = new OpeningAutoPlayPolicy();

            bool shouldPlay = policy.TryBegin(OpeningPlaybackRequest.ManualReplay);

            Assert.IsTrue(shouldPlay);
        }

        [Test]
        public void Advance_AtTimelineBoundaries_ChangesExpectedVisualPhase()
        {
            Assert.AreEqual(OpeningVisualPhase.Glow, StateAt(0f).Phase);

            OpeningSequenceState firstBlend = StateAt(OpeningSequenceState.GlowCrossFadeStart);
            Assert.AreEqual(OpeningVisualPhase.GlowCrossFade, firstBlend.Phase);
            Assert.AreEqual(0, firstBlend.CurrentImageIndex);
            Assert.AreEqual(1, firstBlend.NextImageIndex);
            Assert.AreEqual(0f, firstBlend.ImageBlend, 0.0001f);

            OpeningSequenceState horizon = StateAt(OpeningSequenceState.HorizonStart);
            Assert.AreEqual(OpeningVisualPhase.Horizon, horizon.Phase);
            Assert.AreEqual(1, horizon.CurrentImageIndex);
            Assert.AreEqual(-1, horizon.NextImageIndex);

            Assert.AreEqual(
                OpeningVisualPhase.HorizonCrossFade,
                StateAt(OpeningSequenceState.HorizonCrossFadeStart).Phase);

            OpeningSequenceState gathering = StateAt(OpeningSequenceState.GatheringStart);
            Assert.AreEqual(OpeningVisualPhase.Gathering, gathering.Phase);
            Assert.AreEqual(2, gathering.CurrentImageIndex);

            Assert.AreEqual(OpeningVisualPhase.TitleReveal, StateAt(OpeningSequenceState.TitleStart).Phase);
            Assert.AreEqual(OpeningVisualPhase.TitleHold, StateAt(OpeningSequenceState.TitleHoldStart).Phase);

            OpeningSequenceState finalFade = StateAt(OpeningSequenceState.FinalFadeStart);
            Assert.AreEqual(OpeningVisualPhase.FinalFade, finalFade.Phase);
            Assert.AreEqual(0f, finalFade.FadeAlpha, 0.0001f);

            Assert.AreEqual(OpeningVisualPhase.Completed, StateAt(OpeningSequenceState.Duration).Phase);
        }

        [Test]
        public void TrySkip_BeforeOneSecond_IsLocked()
        {
            OpeningSequenceState state = new OpeningSequenceState();
            state.Advance(OpeningSequenceState.SkipUnlockTime - 0.01f);
            Assert.IsFalse(state.TrySkip());

            state.Advance(0.01f);
            Assert.IsTrue(state.TrySkip());
        }

        [Test]
        public void Advance_SkipFade_CompletesAfterQuarterSecondOnce()
        {
            OpeningSequenceState state = StateAt(OpeningSequenceState.SkipUnlockTime);
            int completionCount = 0;
            state.Completed += () => completionCount++;

            Assert.IsTrue(state.TrySkip());
            state.Advance(OpeningSequenceState.SkipFadeDuration - 0.01f);
            Assert.AreEqual(0, completionCount);
            Assert.IsFalse(state.IsCompleted);

            state.Advance(0.02f);
            state.Advance(1f);

            Assert.IsTrue(state.IsCompleted);
            Assert.IsTrue(state.WasSkipped);
            Assert.AreEqual(1, completionCount);
        }

        [Test]
        public void TrySkip_DuringNaturalFade_ContinuesWithoutBrightnessJump()
        {
            OpeningSequenceState state = StateAt(OpeningSequenceState.FinalFadeStart + 0.4f);
            float naturalFade = state.FadeAlpha;
            int completionCount = 0;
            state.Completed += () => completionCount++;

            Assert.IsTrue(state.TrySkip());
            Assert.AreEqual(naturalFade, state.FadeAlpha, 0.0001f);

            state.Advance(OpeningSequenceState.SkipFadeDuration * 0.5f);
            Assert.Greater(state.FadeAlpha, naturalFade);

            state.Advance(OpeningSequenceState.SkipFadeDuration);
            state.Advance(1f);
            Assert.IsTrue(state.IsCompleted);
            Assert.AreEqual(1f, state.FadeAlpha, 0.0001f);
            Assert.AreEqual(1, completionCount);
        }

        [Test]
        public void Advance_NaturalEnd_RaisesCompletionExactlyOnce()
        {
            OpeningSequenceState state = new OpeningSequenceState();
            int completionCount = 0;
            state.Completed += () => completionCount++;

            state.Advance(OpeningSequenceState.Duration);
            state.Advance(OpeningSequenceState.Duration);
            state.TrySkip();

            Assert.IsTrue(state.IsCompleted);
            Assert.IsFalse(state.WasSkipped);
            Assert.AreEqual(1, completionCount);
        }

        [Test]
        public void PlaybackClock_ConsumeBeforeReset_ReturnsZero()
        {
            OpeningPlaybackClock clock = new OpeningPlaybackClock();

            Assert.AreEqual(0f, clock.Consume(12d));
        }

        [Test]
        public void PlaybackClock_ConsumeNormalFrame_ReturnsRealtimeDelta()
        {
            OpeningPlaybackClock clock = new OpeningPlaybackClock();
            clock.Reset(10d);

            Assert.AreEqual(0.016f, clock.Consume(10.016d), 0.0001f);
        }

        [Test]
        public void PlaybackClock_LongFrame_ClampsAndAdvancesBaseline()
        {
            OpeningPlaybackClock clock = new OpeningPlaybackClock();
            clock.Reset(2d);

            Assert.AreEqual(OpeningPlaybackClock.MaxFrameDelta, clock.Consume(7d), 0.0001f);
            Assert.AreEqual(0.05f, clock.Consume(7.05d), 0.0001f);
        }

        [Test]
        public void PlaybackClock_InvalidOrRegressedTime_ReturnsZeroAndRecoversSafely()
        {
            OpeningPlaybackClock clock = new OpeningPlaybackClock();
            clock.Reset(double.NaN);
            Assert.AreEqual(0f, clock.Consume(1d));

            clock.Reset(5d);
            Assert.AreEqual(0f, clock.Consume(double.NaN));
            Assert.AreEqual(0f, clock.Consume(-1d));
            Assert.AreEqual(0f, clock.Consume(4d));
            Assert.AreEqual(0.05f, clock.Consume(4.05d), 0.0001f);
        }

        [Test]
        public void SkipInputGate_NeutralBeforeUnlock_DoesNotArm()
        {
            OpeningSkipInputGate gate = new OpeningSkipInputGate();

            Assert.IsFalse(gate.ShouldSkip(false, false, false));
            Assert.IsFalse(gate.IsArmed);
        }

        [Test]
        public void SkipInputGate_HeldLaunchTouchAtUnlock_RequiresReleaseThenNewEdge()
        {
            OpeningSkipInputGate gate = new OpeningSkipInputGate();

            Assert.IsFalse(gate.ShouldSkip(true, true, true));
            Assert.IsFalse(gate.ShouldSkip(true, true, false));
            Assert.IsFalse(gate.IsArmed);

            Assert.IsFalse(gate.ShouldSkip(true, false, false));
            Assert.IsTrue(gate.IsArmed);
            Assert.IsTrue(gate.ShouldSkip(true, true, true));
        }

        [Test]
        public void SkipInputGate_InputEdgeOnUnlockFrame_IsNotAcceptedAsNeutral()
        {
            OpeningSkipInputGate gate = new OpeningSkipInputGate();

            Assert.IsFalse(gate.ShouldSkip(true, false, true));
            Assert.IsFalse(gate.IsArmed);
            Assert.IsFalse(gate.ShouldSkip(true, false, false));
            Assert.IsTrue(gate.ShouldSkip(true, false, true));
        }

        [Test]
        public void SkipInputGate_ResetOrRelock_RequiresAnotherNeutralFrame()
        {
            OpeningSkipInputGate gate = new OpeningSkipInputGate();
            Assert.IsFalse(gate.ShouldSkip(true, false, false));
            Assert.IsTrue(gate.IsArmed);

            gate.Reset();
            Assert.IsFalse(gate.ShouldSkip(true, true, true));
            Assert.IsFalse(gate.ShouldSkip(true, false, false));
            Assert.IsTrue(gate.IsArmed);

            Assert.IsFalse(gate.ShouldSkip(false, false, false));
            Assert.IsFalse(gate.IsArmed);
        }

        [Test]
        public void CalculateSkipButtonRect_LandscapePortraitAndInsetSafeAreas_StayInsideWithTouchHeight()
        {
            Rect[] safeAreas =
            {
                new Rect(0f, 0f, 1920f, 1080f),
                new Rect(0f, 0f, 1080f, 1920f),
                new Rect(48f, 72f, 984f, 1776f)
            };

            for (int i = 0; i < safeAreas.Length; i++)
            {
                Rect safeArea = safeAreas[i];
                Rect button = OpeningSceneController.CalculateSkipButtonRect(safeArea);

                Assert.GreaterOrEqual(button.height, 56f);
                Assert.GreaterOrEqual(button.xMin, safeArea.xMin);
                Assert.GreaterOrEqual(button.yMin, safeArea.yMin);
                Assert.LessOrEqual(button.xMax, safeArea.xMax);
                Assert.LessOrEqual(button.yMax, safeArea.yMax);
            }
        }

        [Test]
        public void CalculateSkipButtonRect_AllSafeAreas_IsBottomCenteredPill()
        {
            // 우상단 각진 버튼에서 하단 중앙 알약으로 옮겼다. 되돌아가지 않게 의도를 고정한다.
            Rect[] safeAreas =
            {
                new Rect(0f, 0f, 1920f, 1080f),
                new Rect(0f, 0f, 1080f, 1920f),
                new Rect(48f, 72f, 984f, 1776f)
            };

            for (int i = 0; i < safeAreas.Length; i++)
            {
                Rect safeArea = safeAreas[i];
                Rect button = OpeningSceneController.CalculateSkipButtonRect(safeArea);

                Assert.AreEqual(safeArea.center.x, button.center.x, 1f, "가로 중앙 정렬이 아니다");
                Assert.Greater(button.yMin, safeArea.y + safeArea.height * 0.5f,
                    "하단 절반을 벗어났다 — 타이틀·아트워크를 가린다");
                Assert.Greater(button.width, button.height * 2f, "알약 비례가 아니다");
            }
        }

        [Test]
        public void Narration_DoesNotOverlapTitle()
        {
            // 타이틀은 6.2s에 뜬다. 앞의 두 줄이 거기까지 남아 있으면 글자가 겹쳐 둘 다 못 읽는다.
            Assert.LessOrEqual(OpeningSequenceState.Narration2End, OpeningSequenceState.TitleStart + 0.01f,
                "두 번째 내레이션이 타이틀과 겹친다");
            // 세 번째 줄은 타이틀이 자리잡은 뒤(TitleHold 8s) 얹는 것이 의도다.
            Assert.GreaterOrEqual(OpeningSequenceState.Narration3Start, OpeningSequenceState.TitleHoldStart,
                "세 번째 내레이션이 타이틀이 자리잡기 전에 뜬다");
        }

        [Test]
        public void Narration_EndsBeforeSequenceEnds()
        {
            // 마지막 줄이 페이드아웃(9.2s~10s) 뒤까지 남으면 검은 화면에 글자만 뜬다.
            Assert.LessOrEqual(OpeningSequenceState.Narration3End, OpeningSequenceState.Duration,
                "내레이션이 오프닝보다 늦게 끝난다");
        }

        [Test]
        public void Narration_LinesDoNotOverlapEachOther()
        {
            Assert.Less(OpeningSequenceState.Narration1End, OpeningSequenceState.Narration2Start,
                "1번과 2번 내레이션이 겹친다");
            Assert.Less(OpeningSequenceState.Narration2End, OpeningSequenceState.Narration3Start,
                "2번과 3번 내레이션이 겹친다");
        }

        [Test]
        public void GetNarration_ReturnsCorrectLineAtEachWindow()
        {
            AssertNarrationAt((OpeningSequenceState.Narration1Start + OpeningSequenceState.Narration1End) * 0.5f, 0);
            AssertNarrationAt((OpeningSequenceState.Narration2Start + OpeningSequenceState.Narration2End) * 0.5f, 1);
            AssertNarrationAt((OpeningSequenceState.Narration3Start + OpeningSequenceState.Narration3End) * 0.5f, 2);

            // 창 사이 공백과 시작 전에는 아무것도 안 뜬다.
            AssertNarrationAt(0.2f, -1);
            AssertNarrationAt((OpeningSequenceState.Narration1End + OpeningSequenceState.Narration2Start) * 0.5f, -1);
        }

        [Test]
        public void GetNarration_FadesInAndOutWithinEachWindow()
        {
            // 창 경계에서 알파가 0에 가깝고 가운데서 1이어야 툭 튀지 않는다.
            OpeningSequenceState atStart = StateAt(OpeningSequenceState.Narration1Start + 0.02f);
            atStart.GetNarration(out int i1, out float aStart);
            OpeningSequenceState atMid = StateAt(
                (OpeningSequenceState.Narration1Start + OpeningSequenceState.Narration1End) * 0.5f);
            atMid.GetNarration(out _, out float aMid);

            Assert.AreEqual(0, i1);
            Assert.Less(aStart, 0.2f, "시작에서 알파가 이미 높다 — 페이드 인이 없다");
            Assert.AreEqual(1f, aMid, 0.01f, "가운데서 완전히 보이지 않는다");
        }

        private static void AssertNarrationAt(float elapsed, int expectedIndex)
        {
            OpeningSequenceState state = StateAt(elapsed);
            state.GetNarration(out int index, out _);
            Assert.AreEqual(expectedIndex, index, $"경과 {elapsed:F2}s의 내레이션 줄이 다르다");
        }

        private static OpeningSequenceState StateAt(float elapsed)
        {
            OpeningSequenceState state = new OpeningSequenceState();
            state.Advance(elapsed);
            return state;
        }
    }
}
#endif
