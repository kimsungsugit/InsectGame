#if UNITY_EDITOR
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Story;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 스토리 발화 관문(<c>StoryDirector.ShouldDeferNow</c>).
    ///
    /// 이 게이트가 막는 것 둘:
    /// <list type="number">
    /// <item><b>대화 중 끼어들기.</b> <c>NpcDialogueUI.ShowStory</c>는 열린 모달을
    /// <c>CloseModal</c>로 밀어낸다. 마을 사람과 이야기하던 중 레벨업·퀘스트 완료가 일어나면
    /// <b>읽던 대화가 통째로 사라지고</b> 스토리가 시작됐다.</item>
    /// <item><b>전투 결과 화면 덮기.</b> 전투 승리는 같은 프레임에 XP·도감·퀘스트를 함께
    /// 갱신하므로 <c>LevelReach</c>/<c>DexProgress</c>/<c>QuestComplete</c>가 즉시 발화해
    /// 보상 패널 위로 대사창이 열렸다 — <c>BattleWin</c>만 미루고 있었던 탓이다.</item>
    /// </list>
    ///
    /// private이라 리플렉션으로 부른다. 공개 API로 올리지 않는 이유: 이건 내부 판단이고,
    /// 밖에서 부를 일이 생기면 그때 <c>RouteTrigger</c>를 쓰는 게 맞다.
    /// </summary>
    [TestFixture]
    public class StoryDeferGateTests
    {
        /// <summary>테스트용 모달 — 스택에 올라가면 <c>IsAnyOpen</c>이 참이 된다.</summary>
        private sealed class FakeModal : IModalUI
        {
            public bool IsOpen { get; set; } = true;
            public void CloseModal() { IsOpen = false; }
        }

        private GameObject host;
        private StoryDirector director;
        private GameObject cameraObject;
        private CameraFollower follower;
        private MethodInfo shouldDefer;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("StoryDeferGateTests");
            director = host.AddComponent<StoryDirector>();

            cameraObject = new GameObject("StoryDeferGateTestsCamera");
            follower = cameraObject.AddComponent<CameraFollower>();

            shouldDefer = typeof(StoryDirector).GetMethod(
                "ShouldDeferNow", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(shouldDefer,
                "ShouldDeferNow가 사라졌다 — 관문을 걷어내면 대사가 대화·전투 화면을 다시 덮는다");
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
        }

        private bool Defers()
        {
            return (bool)shouldDefer.Invoke(director, null);
        }

        [Test]
        public void NothingOpen_DoesNotDefer()
        {
            Assert.IsFalse(Defers(), "아무것도 안 떠 있으면 그 자리에서 발화해야 한다");
        }

        [Test]
        public void ModalOpen_Defers()
        {
            FakeModal modal = new FakeModal();
            ModalUIRegistry.Register(modal);
            try
            {
                Assert.IsTrue(Defers(),
                    "대화·컷신이 떠 있는데 발화하면 읽던 대사가 밀려나고 보상까지 지급된다");
            }
            finally
            {
                ModalUIRegistry.Unregister(modal);
            }
        }

        [Test]
        public void ModalClosed_StopsDeferring()
        {
            FakeModal modal = new FakeModal();
            ModalUIRegistry.Register(modal);
            modal.CloseModal();     // IsOpen=false → IsAnyOpen이 죽은 참조로 정리한다

            Assert.IsFalse(Defers(), "대화가 끝났으면 미뤄 둔 편이 곧바로 이어져야 한다");
            ModalUIRegistry.Unregister(modal);
        }

        [Test]
        public void BattleCameraActive_Defers()
        {
            director.AutoWire(follower);
            follower.EnterBattleMode(Vector3.zero, Vector3.forward * 5f);
            try
            {
                Assert.IsTrue(Defers(),
                    "전투 화면이 떠 있는데 발화하면 대사창이 보상 패널을 덮는다");
            }
            finally
            {
                follower.ExitBattleMode();
            }
        }

        [Test]
        public void BattleCameraReleased_StopsDeferring()
        {
            director.AutoWire(follower);
            follower.EnterBattleMode(Vector3.zero, Vector3.forward * 5f);
            follower.ExitBattleMode();

            Assert.IsFalse(Defers(), "전투가 끝났으면 미뤄 둔 트리거가 이어져야 한다");
        }

        [Test]
        public void NoCameraWired_FallsBackToModalCheckOnly()
        {
            // 카메라 미배선은 옛 경로(전투 화면 종료 통지 + 12초 타이머)로 떨어진다.
            // 그때도 모달 판정은 살아 있어야 한다.
            Assert.IsFalse(Defers());

            FakeModal modal = new FakeModal();
            ModalUIRegistry.Register(modal);
            try { Assert.IsTrue(Defers()); }
            finally { ModalUIRegistry.Unregister(modal); }
        }

        [Test]
        public void PendingStory_EventIsQueuedEvenBeforeModalRegisters()
        {
            FieldInfo pending = typeof(StoryDirector).GetField("pendingBeatId", BindingFlags.Instance | BindingFlags.NonPublic);
            pending.SetValue(director, "reading");
            MethodInfo route = typeof(StoryDirector).GetMethod("RouteTrigger", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsFalse((bool)route.Invoke(director, new object[] { "CaptureInsect", "qa_insect" }));
            IList queue = (IList)typeof(StoryDirector).GetField("pendingTriggers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director);
            Assert.AreEqual(1, queue.Count, "모달 등록 전/보상 지급 중 이벤트도 읽던 대사 뒤에 남겨야 한다");
        }

        [Test]
        public void PendingStory_NpcTalkDoesNotClaimItOpenedAnotherStory()
        {
            WithStoryCache(new StoryBeat { beatId = "qa_npc", trigger = new StoryTrigger { type = "NpcTalk", param = "qa_npc" } }, () =>
            {
                typeof(StoryDirector).GetField("pendingBeatId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, "reading");
                Assert.IsFalse(director.OnNpcTalked("qa_npc"));
            });
        }

        [Test]
        public void LevelReachedBeforePrerequisite_ResweepContinuesWithoutAnotherLevelUp()
        {
            GameObject progressHost = new GameObject("InactiveStoryProgressFixture");
            progressHost.SetActive(false); // Awake/저장 로드를 실행하지 않는다.
            try
            {
                PlayerProgressController player = progressHost.AddComponent<PlayerProgressController>();
                typeof(PlayerProgressController).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(player, new PlayerProgressData { level = 5 });
                director.AutoWire(null, null, player, null, null);
                WithStoryCache(new StoryBeat
                {
                    beatId = "qa_growth", prerequisiteBeatId = "qa_intro",
                    trigger = new StoryTrigger { type = "LevelReach", param = "3" }
                }, () =>
                {
                    string fired = null;
                    director.StoryBeatTriggered += beat => fired = beat.beatId;
                    MethodInfo sweep = typeof(StoryDirector).GetMethod("ResweepPersistentConditions", BindingFlags.Instance | BindingFlags.NonPublic);
                    MethodInfo drain = typeof(StoryDirector).GetMethod("DrainPendingTriggers", BindingFlags.Instance | BindingFlags.NonPublic);
                    sweep.Invoke(director, null);
                    drain.Invoke(director, null);
                    Assert.IsNull(fired, "선행 대화 전에는 누적 조건만으로 발화하면 안 된다");
                    typeof(StoryDirector).GetMethod("MarkSeen", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(director, new object[] { "qa_intro" });
                    sweep.Invoke(director, null);
                    drain.Invoke(director, null);
                    Assert.AreEqual("qa_growth", fired);
                    Assert.AreEqual(5, player.Level);
                });
            }
            finally { Object.DestroyImmediate(progressHost); }
        }

        private void WithStoryCache(StoryBeat beat, System.Action action)
        {
            WithStoryCache(new[] { beat }, action);
        }

        [TestCase("DexProgress", "2")]
        [TestCase("RegionCleansed", "forest")]
        [TestCase("DuelWin", "ledger_grip")]
        public void PersistedCondition_RespectsPrerequisiteAndRegion_AndDoesNotReplay(string type, string param)
        {
            GameObject stateHost = new GameObject("InactivePersistentStoryFixture");
            stateHost.SetActive(false);
            try
            {
                var region = stateHost.AddComponent<RegionManager>();
                var dex = stateHost.AddComponent<InsectGame.Dex.DexController>();
                var blight = stateHost.AddComponent<RegionBlightManager>();
                var duel = stateHost.AddComponent<InsectGame.NPC.NpcDuelController>();
                SetField(region, "currentRegion", new InsectGame.Data.RegionData { regionId = "pond" });
                var savedDex = new InsectGame.Dex.DexSaveData();
                savedDex.records.Add(new InsectGame.Dex.DexRecord("qa_a") { capturedCount = 1 });
                savedDex.records.Add(new InsectGame.Dex.DexRecord("qa_b") { capturedCount = 1 });
                SetField(dex, "saveData", savedDex);
                SetField(blight, "loaded", true);
                ((HashSet<string>)GetField(blight, "cleansedRegions")).Add("forest");
                SetField(duel, "bossStateLoaded", true);
                ((HashSet<string>)GetField(duel, "defeatedBosses")).Add("ledger_grip");
                director.AutoWire(region, null, null, null, null);
                director.AutoWire(dex);
                director.AutoWire(blight);
                director.AutoWire(duel);
                WithStoryCache(new StoryBeat
                {
                    beatId = "qa_persisted", prerequisiteBeatId = "qa_intro", requiredRegionId = "forest",
                    trigger = new StoryTrigger { type = type, param = param }
                }, () =>
                {
                    int fired = 0;
                    director.StoryBeatTriggered += _ => fired++;
                    SweepAndDrain();
                    Assert.AreEqual(0, fired);
                    InvokeDirector("MarkSeen", "qa_intro");
                    SweepAndDrain();
                    Assert.AreEqual(0, fired, "조건과 선행 대화를 완료해도 다른 리전에서는 발화하지 않는다");
                    var forest = new InsectGame.Data.RegionData { regionId = "forest" };
                    SetField(region, "currentRegion", forest);
                    InvokeDirector("OnRegionChanged", forest);
                    InvokeDirector("DrainPendingTriggers");
                    Assert.AreEqual(1, fired, "재포획/재격파 없이 해당 리전 진입으로 이어진다");
                    SweepAndDrain();
                    Assert.AreEqual(1, fired, "열린 대사를 중복 발화하지 않는다");
                    // 저장/보상 부작용 없이 완료 상태를 재현한다.
                    InvokeDirector("MarkSeen", "qa_persisted");
                    SetField(director, "pendingBeatId", null);
                    SweepAndDrain();
                    Assert.AreEqual(1, fired, "이미 읽은 비트는 보상 가능한 대사로 다시 열리지 않는다");
                    Assert.AreEqual(2, dex.CapturedSpeciesCount);
                });
            }
            finally { Object.DestroyImmediate(stateHost); }
        }

        [Test]
        public void CompletedQuestResweep_DoesNotOvertakeQueuedChoiceResult()
        {
            GameObject questHost = new GameObject("InactiveStoryQuestFixture");
            questHost.SetActive(false);
            try
            {
                var quest = questHost.AddComponent<TutorialQuestManager>();
                ((HashSet<string>)GetField(quest, "completedQuests")).Add("qa_quest");
                director.AutoWire(null, null, null, null, quest);
                WithStoryCache(new[]
                {
                    new StoryBeat { beatId = "qa_choice", trigger = new StoryTrigger { type = "Immediate" } },
                    new StoryBeat { beatId = "qa_quest_story", trigger = new StoryTrigger { type = "QuestComplete", param = "qa_quest" } }
                }, () =>
                {
                    var fired = new List<string>();
                    director.StoryBeatTriggered += beat => fired.Add(beat.beatId);
                    director.QueueChoice("qa_choice");
                    InvokeDirector("ResweepCompletedQuests");
                    InvokeDirector("DrainPendingTriggers");
                    CollectionAssert.AreEqual(new[] { "qa_choice" }, fired);
                    InvokeDirector("MarkSeen", "qa_choice");
                    SetField(director, "pendingBeatId", null);
                    InvokeDirector("DrainPendingTriggers");
                    CollectionAssert.AreEqual(new[] { "qa_choice", "qa_quest_story" }, fired);
                });
            }
            finally { Object.DestroyImmediate(questHost); }
        }

        private static object GetField(object target, string name) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        [Test]
        public void FinalVictory_AfterSeal_ChiefConversationOpensAftermathWithoutAnotherBattle()
        {
            Assert.IsTrue(StoryService.TryGetBeat("ch12_clash", out StoryBeat aftermath));
            GameObject regionHost = new GameObject("InactiveFinalStoryRegionFixture");
            regionHost.SetActive(false);
            try
            {
                var region = regionHost.AddComponent<RegionManager>();
                SetField(region, "currentRegion", new InsectGame.Data.RegionData { regionId = "nameless" });
                director.AutoWire(region, null, null, null, null);
                WithStoryCache(aftermath, () =>
                {
                    string fired = null;
                    director.StoryBeatTriggered += beat => fired = beat.beatId;
                    Assert.IsFalse(director.OnNpcTalked("ledger_chief"), "최종 승리 전에 여운을 미리 말하지 않는다");
                    InvokeDirector("MarkSeen", "fin_seal");
                    Assert.IsTrue(director.OnNpcTalked("ledger_chief"));
                    Assert.AreEqual("ch12_clash", fired);
                    Assert.AreEqual(55, aftermath.onComplete.rewardCandy);
                    Assert.AreEqual(130, aftermath.onComplete.rewardExp);
                });
            }
            finally { Object.DestroyImmediate(regionHost); }
        }

        private static void SetField(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private void InvokeDirector(string name, params object[] args) => typeof(StoryDirector)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, args);

        private void SweepAndDrain()
        {
            InvokeDirector("ResweepPersistentConditions");
            InvokeDirector("DrainPendingTriggers");
        }

        private void WithStoryCache(StoryBeat[] beats, System.Action action)
        {
            FieldInfo cache = typeof(StoryService).GetField("cache", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = cache.GetValue(null);
            var fixture = new Dictionary<string, StoryBeat>();
            foreach (StoryBeat beat in beats) fixture.Add(beat.beatId, beat);
            cache.SetValue(null, fixture);
            // 실제 계정 진행과 무관한 테스트. CompleteBeat/Save는 호출하지 않는다.
            typeof(StoryDirector).GetField("progress", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(director, new StoryProgressData());
            try { action(); }
            finally { cache.SetValue(null, previous); }
        }
    }
}
#endif
