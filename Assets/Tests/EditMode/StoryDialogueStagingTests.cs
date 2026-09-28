#if UNITY_EDITOR
using InsectGame.UI;
using NUnit.Framework;
using Fx = InsectGame.UI.StoryDialogueStaging.LineFx;
using Side = InsectGame.UI.StoryDialogueStaging.Side;

namespace InsectGame.Tests
{
    [TestFixture]
    public class StoryDialogueStagingTests
    {
        [Test]
        public void ParseFx_CommaSeparated_IgnoresCaseAndSpaces()
        {
            Assert.AreEqual(Fx.Shake | Fx.Flash, StoryDialogueStaging.ParseFx("shake, Flash"));
            Assert.AreEqual(Fx.None, StoryDialogueStaging.ParseFx(null));
            Assert.AreEqual(Fx.None, StoryDialogueStaging.ParseFx(""));
        }

        [Test]
        public void ParseFx_UnknownToken_IsDropped()
        {
            // 모르는 토큰으로 연출 전체가 날아가면 안 된다 — 아는 것만 남긴다(오타는 story_lint가 잡는다).
            Assert.AreEqual(Fx.Pause, StoryDialogueStaging.ParseFx("pause,zoom"));
        }

        [Test]
        public void KnownFxTokens_EachMapsToItsOwnFlag()
        {
            // 목록(린트가 읽는 단일 출처)과 해석 switch가 어긋나면 저작한 연출이 조용히 사라진다.
            Fx seen = Fx.None;
            foreach (string token in StoryDialogueStaging.KnownFxTokens)
            {
                Fx fx = StoryDialogueStaging.ParseFx(token);
                Assert.AreNotEqual(Fx.None, fx, $"'{token}'이 아무 연출로도 풀리지 않는다");
                Assert.AreEqual(Fx.None, seen & fx, $"'{token}'이 다른 토큰과 같은 플래그다");
                seen |= fx;
            }
        }

        [Test]
        public void VisibleChars_GrowsOverTimeAndStopsAtLength()
        {
            const int len = 40;
            Assert.AreEqual(0, StoryDialogueStaging.VisibleChars(len, 0f, Fx.None));
            int early = StoryDialogueStaging.VisibleChars(len, 0.2f, Fx.None);
            int later = StoryDialogueStaging.VisibleChars(len, 0.6f, Fx.None);
            Assert.Greater(early, 0);
            Assert.Greater(later, early);
            float full = StoryDialogueStaging.RevealDuration(len, Fx.None);
            Assert.AreEqual(len, StoryDialogueStaging.VisibleChars(len, full + 0.01f, Fx.None));
            Assert.AreEqual(len, StoryDialogueStaging.VisibleChars(len, 999f, Fx.None));
        }

        [Test]
        public void VisibleChars_PauseDelaysTheFirstLetter()
        {
            Assert.AreEqual(0, StoryDialogueStaging.VisibleChars(20, StoryDialogueStaging.PauseSeconds - 0.01f, Fx.Pause));
            Assert.Greater(StoryDialogueStaging.VisibleChars(20, StoryDialogueStaging.PauseSeconds + 0.2f, Fx.Pause), 0);
        }

        [Test]
        public void TypingSpeed_ShoutFasterSlowSlower()
        {
            float normal = StoryDialogueStaging.TypingSpeed(Fx.None);
            Assert.Greater(StoryDialogueStaging.TypingSpeed(Fx.Shout), normal);
            Assert.Less(StoryDialogueStaging.TypingSpeed(Fx.Slow), normal);
            Assert.Less(StoryDialogueStaging.TypingSpeed(Fx.Whisper), normal);
        }

        [Test]
        public void RevealDuration_OrdinaryLineUnderTwoSeconds()
        {
            // 한 줄이 2초를 넘겨 나오면 읽는 사람이 기다리게 된다 — 50자 기준.
            Assert.Less(StoryDialogueStaging.RevealDuration(50, Fx.None), 2f);
        }

        [Test]
        public void ShakeAndFlash_OnlyWhenTaggedAndOnlyBriefly()
        {
            Assert.AreEqual(0f, StoryDialogueStaging.ShakeOffset(Fx.None, 0.05f));
            Assert.AreNotEqual(0f, StoryDialogueStaging.ShakeOffset(Fx.Shake, 0.05f));
            Assert.AreEqual(0f, StoryDialogueStaging.ShakeOffset(Fx.Shake, StoryDialogueStaging.ShakeSeconds));

            Assert.AreEqual(0f, StoryDialogueStaging.FlashOverlay(Fx.None, 0f));
            float start = StoryDialogueStaging.FlashOverlay(Fx.Flash, 0f);
            float mid = StoryDialogueStaging.FlashOverlay(Fx.Flash, StoryDialogueStaging.FlashSeconds * 0.5f);
            Assert.Greater(start, mid);
            Assert.AreEqual(0f, StoryDialogueStaging.FlashOverlay(Fx.Flash, StoryDialogueStaging.FlashSeconds));
        }

        [Test]
        public void HopOffset_RisesAndLandsWithinWindow()
        {
            Assert.AreEqual(0f, StoryDialogueStaging.HopOffset(0f), 0.0001f);
            Assert.Greater(StoryDialogueStaging.HopOffset(StoryDialogueStaging.HopSeconds * 0.5f), 0f);
            Assert.AreEqual(0f, StoryDialogueStaging.HopOffset(StoryDialogueStaging.HopSeconds));
        }

        [Test]
        public void AssignSides_TwoSpeakers_FaceEachOther()
        {
            var stages = StoryDialogueStaging.AssignSides(new[] { "a", "b", "a", "b" });
            Assert.AreEqual("a", stages[3].LeftId);
            Assert.AreEqual("b", stages[3].RightId);
            Assert.AreEqual(new[] { Side.Left, Side.Right, Side.Left, Side.Right },
                new[] { stages[0].Active, stages[1].Active, stages[2].Active, stages[3].Active });
        }

        [Test]
        public void AssignSides_FirstLineHasOnlyTheSpeakerOnStage()
        {
            var stages = StoryDialogueStaging.AssignSides(new[] { "a", "b" });
            Assert.AreEqual("a", stages[0].LeftId);
            Assert.IsNull(stages[0].RightId, "아직 말하지 않은 사람이 먼저 무대에 서 있다");
        }

        [Test]
        public void AssignSides_NarrationKeepsStageAndSpeaksForNoOne()
        {
            var stages = StoryDialogueStaging.AssignSides(new[] { "a", null, "b" });
            Assert.AreEqual(Side.None, stages[1].Active);
            Assert.AreEqual("a", stages[1].LeftId, "지문 줄에서 서 있던 사람이 사라졌다");
            Assert.AreEqual(Side.Right, stages[2].Active);
        }

        [Test]
        public void AssignSides_ThirdSpeaker_ReplacesWhoeverSpokeLongestAgo()
        {
            // a·b 다음 c — 더 오래 말하지 않은 a의 자리에 선다.
            var abc = StoryDialogueStaging.AssignSides(new[] { "a", "b", "c" });
            Assert.AreEqual("c", abc[2].LeftId);
            Assert.AreEqual("b", abc[2].RightId);

            // a·b·a 다음 c — 이번엔 b가 더 오래 말하지 않았다. 방금 말한 a는 남아 대꾸를 받는다.
            var abac = StoryDialogueStaging.AssignSides(new[] { "a", "b", "a", "c" });
            Assert.AreEqual("a", abac[3].LeftId);
            Assert.AreEqual("c", abac[3].RightId);
        }
    }
}
#endif
