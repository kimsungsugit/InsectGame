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

        // 대사 앞 영상·등장 연출이 씬 재로드·UI 루트 토글로 끊기면 콜백이 ShowStory를 부른다 — 그때 창이 꺼져 있으면
        // static 모달 레지스트리에 열린 채 남으면 안 된다(새 씬의 조작이 막힌다). 쥐고 있다가 다시 켜지면 연다.
        [Test]
        public void ShowStory_WhileDisabled_DoesNotRegister_AndOpensWhenEnabledAgain()
        {
            var root = new GameObject("Dialogue disabled test");
            try
            {
                var ui = root.AddComponent<NpcDialogueUI>();
                var beat = new StoryBeat { beatId = "held", speakerNpcId = "ruins_scholar",
                    lines = new List<StoryLine> { new StoryLine { speaker = "세라", text = "기다렸어" } } };
                root.SetActive(false);

                ui.ShowStory(beat);
                Assert.IsFalse(ui.IsOpen, "꺼진 창은 열리지 않는다");
                Assert.IsFalse(ReferenceEquals(ModalUIRegistry.TopModal, ui), "꺼진 창이 모달 레지스트리에 올라가면 새 씬 조작이 막힌다");

                root.SetActive(true);
                Assert.IsTrue(ui.IsOpen, "다시 켜지면 쥐고 있던 비트를 연다");
                Assert.IsTrue(ReferenceEquals(ModalUIRegistry.TopModal, ui));
                ui.CloseModal();
                Assert.IsFalse(ui.IsOpen);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ShowStoryReplay_WhileDisabled_IsDropped_AndDestroyedWindowNeverRegisters()
        {
            var root = new GameObject("Dialogue replay disabled test");
            var beat = new StoryBeat { beatId = "replay", speakerNpcId = "ruins_scholar",
                lines = new List<StoryLine> { new StoryLine { speaker = "세라", text = "다시 읽기" } } };
            var ui = root.AddComponent<NpcDialogueUI>();
            try
            {
                root.SetActive(false);
                ui.ShowStoryReplay(beat);
                root.SetActive(true);
                Assert.IsFalse(ui.IsOpen, "다시보기는 쥐지 않는다 — 저널에서 다시 누르면 된다");
            }
            finally { Object.DestroyImmediate(root); }

            // 파괴된 창(씬 재로드 뒤 늦게 온 콜백) — 관리 껍데기로 불려도 레지스트리에 오르지 않는다.
            ui.ShowStory(beat);
            Assert.IsFalse(ReferenceEquals(ModalUIRegistry.TopModal, ui));
        }
    }
}
#endif
