#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InsectGame.NPC;
using InsectGame.Story;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    public class NpcDialogueContinuityTests
    {
        [TestCase("하월", "catcher_rival", "ledger_chief")]
        [TestCase("관장 하월", "catcher_rival", "ledger_chief")]
        [TestCase("세라", "catcher_rival", "ruins_scholar")]
        [TestCase("라온", "ruins_scholar", "catcher_rival")]
        [TestCase("village_elder", "catcher_rival", "village_elder")]
        [TestCase("", "catcher_rival", "catcher_rival")]
        [TestCase("나", "catcher_rival", null)]
        [TestCase("내레이션", "ruins_scholar", null)]
        public void Portrait_FollowsCurrentSpeaker(string speaker, string fallback, string expected)
        {
            Assert.AreEqual(expected, NpcDialogueDatabase.StoryPortraitId(speaker, fallback));
        }

        [Test]
        public void SpeakerId_UsesWorldName_WhileAuthoredNameIsPreserved()
        {
            Assert.AreEqual("세라", NpcDialogueDatabase.StorySpeakerName("ruins_scholar"));
            Assert.AreEqual("알 수 없는 목소리", NpcDialogueDatabase.StorySpeakerName("알 수 없는 목소리"));
        }

        [Test]
        public void Ambient_UsesActualDefeatAndCleansing_WithoutChangingOriginalLines()
        {
            Assert.IsTrue(NpcDialogueDatabase.TryGetStoryNpcLines("ledger_thug_cord", false, false, out var before));
            Assert.IsTrue(NpcDialogueDatabase.TryGetStoryNpcLines("ledger_thug_cord", true, false, out var defeated));
            Assert.IsTrue(NpcDialogueDatabase.TryGetStoryNpcLines("ledger_thug_cord", true, true, out var cleansed));
            CollectionAssert.AreNotEqual(before, defeated);
            CollectionAssert.AreNotEqual(defeated, cleansed);
            NpcDialogueDatabase.TryGetStoryNpcLines("ledger_thug_cord", false, false, out var untouched);
            CollectionAssert.AreEqual(before, untouched);
        }

        [Test]
        public void AmbientRequest_CannotReplaceActiveStoryOrItsCompletionState()
        {
            var root = new GameObject("Dialogue continuity test");
            try
            {
                var ui = root.AddComponent<NpcDialogueUI>();
                var npc = root.AddComponent<VillagerNpc>();
                var beat = new StoryBeat { beatId = "test", speakerNpcId = "ruins_scholar",
                    lines = new List<StoryLine> { new StoryLine { speaker = "세라", text = "진행 중" } } };
                ui.ShowStoryReplay(beat);
                ui.Show(npc);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                Assert.AreSame(beat, typeof(NpcDialogueUI).GetField("currentBeat", flags).GetValue(ui));
                Assert.IsTrue((bool)typeof(NpcDialogueUI).GetField("storyReplay", flags).GetValue(ui));
                Assert.IsNull(typeof(NpcDialogueUI).GetField("currentNpc", flags).GetValue(ui));
                Assert.IsTrue(ui.IsOpen);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
