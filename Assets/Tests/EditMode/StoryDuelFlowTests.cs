#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using InsectGame.Core;
using InsectGame.NPC;
using InsectGame.Story;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 「이야기 전투」(2026-10-04) — 대사 직후 대결(<see cref="StoryBeat.duelAfter"/> → <see cref="StoryDuelLauncher"/>),
    /// 간부 필수(승리 비트가 다음 장의 선행), 그리고 이미 이긴 세이브의 자동 통과.
    ///
    /// 실패가 전부 무증상이라 여기서 고정한다: 대결이 그냥 안 열리거나(오타·퇴장 연출·꿈), 이겨도 다음 장이 안 열리거나
    /// (승리를 놓친 세이브), 이미 지나온 장으로 HUD가 되돌려 보낸다(나중에 끼워 넣은 스파인). 정적 저작은 story_lint 검사 35·8이 함께 본다.
    /// </summary>
    [TestFixture]
    public class StoryDuelFlowTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static System.Func<string, bool> Seen(params string[] ids)
        {
            var set = new HashSet<string>(ids);
            return set.Contains;
        }

        private static StoryBeat RealBeat(string id)
        {
            Assert.IsTrue(StoryService.TryGetBeat(id, out StoryBeat beat), $"Story.json에 {id}가 없다");
            return beat;
        }

        // ── 순수 판정 ──

        [Test]
        public void KindOf_BossesRivalAndOthers()
        {
            Assert.AreEqual(StoryDuelLauncher.DuelKind.Boss, StoryDuelLauncher.KindOf("ledger_grip"));
            Assert.AreEqual(StoryDuelLauncher.DuelKind.Boss, StoryDuelLauncher.KindOf("ledger_scale"));
            Assert.AreEqual(StoryDuelLauncher.DuelKind.Boss, StoryDuelLauncher.KindOf("ledger_chief"));
            Assert.AreEqual(StoryDuelLauncher.DuelKind.Rival, StoryDuelLauncher.KindOf("catcher_rival"));
            Assert.AreEqual(StoryDuelLauncher.DuelKind.None, StoryDuelLauncher.KindOf("ruins_scholar"));
            Assert.AreEqual(StoryDuelLauncher.DuelKind.None, StoryDuelLauncher.KindOf(""));
            Assert.AreEqual(StoryDuelLauncher.DuelKind.None, StoryDuelLauncher.KindOf(null));
        }

        [TestCase(false, false, false, false, false)]
        [TestCase(true, false, false, false, true)]    // 대사·영상·컷신·연출이 떠 있다
        [TestCase(false, true, false, false, true)]    // 다른 전투(결과 화면 포함)
        [TestCase(false, false, true, false, true)]    // 선택지 결과 대사가 큐에 남았다
        [TestCase(false, false, false, true, true)]    // 조작이 묶여 있다
        public void ShouldWait_AnyBlockerWaits(bool modal, bool battle, bool story, bool frozen, bool expected)
        {
            Assert.AreEqual(expected, StoryDuelLauncher.ShouldWait(modal, battle, story, frozen));
        }

        // ── 대기열 ──

        private static void Complete(StoryDuelLauncher launcher, StoryBeat beat)
        {
            typeof(StoryDuelLauncher).GetMethod("OnBeatCompleted", Private).Invoke(launcher, new object[] { beat });
        }

        [Test]
        public void BeatWithDuelAfter_IsQueued_OthersAreNot()
        {
            GameObject host = new GameObject("StoryDuelFlowTests");
            host.SetActive(false);   // Update가 돌아 진짜 전투를 열지 않게
            try
            {
                var launcher = host.AddComponent<StoryDuelLauncher>();
                Complete(launcher, new StoryBeat { beatId = "qa_talk" });
                Assert.IsFalse(launcher.HasPendingDuel, "duelAfter가 없는 비트는 대결을 걸지 않는다");

                Complete(launcher, new StoryBeat { beatId = "qa_confront", duelAfter = " ledger_grip " });
                Assert.IsTrue(launcher.HasPendingDuel);
                Assert.AreEqual("ledger_grip", launcher.PendingNpcId, "앞뒤 공백은 저작 실수로 보고 걷어 낸다");
                Assert.AreEqual("ledger_grip", launcher.LastQueuedNpcId);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void InDream_NothingIsQueued()
        {
            GameObject host = new GameObject("StoryDuelFlowTestsDream");
            host.SetActive(false);
            DreamPrologueState.Begin();
            try
            {
                var launcher = host.AddComponent<StoryDuelLauncher>();
                Complete(launcher, new StoryBeat { beatId = "qa_confront", duelAfter = "ledger_grip" });
                Assert.IsFalse(launcher.HasPendingDuel, "꿈은 이야기의 대결을 열지 않는다(rules/dream-prologue.md)");
            }
            finally
            {
                DreamPrologueState.End();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void UnknownOpponent_IsNotQueued_AndSaysSo()
        {
            GameObject host = new GameObject("StoryDuelFlowTestsTypo");
            host.SetActive(false);
            // LogAssert는 쓰지 않는다 — asmdef가 없어 테스트가 Assembly-CSharp로 컴파일되는 이 프로젝트에선 참조가 안 된다.
            int warned = 0;
            Application.LogCallback count = (message, _, type) =>
            {
                if (type == LogType.Warning && Regex.IsMatch(message, "대결 표에 없는 상대")) warned++;
            };
            Application.logMessageReceived += count;
            try
            {
                var launcher = host.AddComponent<StoryDuelLauncher>();
                Complete(launcher, new StoryBeat { beatId = "qa_confront", duelAfter = "ledger_gripp" });
                Assert.IsFalse(launcher.HasPendingDuel);
                Assert.AreEqual(1, warned, "오타는 조용히 넘기지 않는다 — 대결이 안 열리는 건 화면상 티가 안 난다");
            }
            finally
            {
                Application.logMessageReceived -= count;
                Object.DestroyImmediate(host);
            }
        }

        // ── 실제 Story.json ──

        [Test]
        public void RealStory_DuelAfterBeats_AreTheFourStoryBattles()
        {
            var actual = new Dictionary<string, string>();
            foreach (StoryBeat b in StoryService.AllBeats())
                if (b != null && !string.IsNullOrEmpty(b.duelAfter)) actual[b.beatId] = b.duelAfter;

            var expected = new Dictionary<string, string>
            {
                ["ch1_rival_intro"] = "catcher_rival",
                ["ch8_confront"] = "ledger_grip",
                ["ch9_confront"] = "ledger_scale",
                ["ch12_confront"] = "ledger_chief",
            };
            CollectionAssert.AreEquivalent(expected, actual);
            foreach (string id in expected.Keys)
            {
                StoryBeat b = RealBeat(id);
                Assert.IsTrue(string.IsNullOrEmpty(b.stageExitId), $"{id}: 퇴장 연출이 대결 상대를 걸어 나가게 한다");
                Assert.AreNotEqual(StoryDuelLauncher.DuelKind.None, StoryDuelLauncher.KindOf(b.duelAfter), id);
            }
        }

        [Test]
        public void RealStory_ScenesEndWithTheChallenge()
        {
            // 대사가 끝나면 곧바로 싸운다 — 마지막 줄이 도전이어야 "왜 갑자기 전투?"가 안 된다.
            StringAssert.EndsWith("승부다!", LastLine("ch1_rival_intro").text);
            Assert.AreEqual("집게", LastLine("ch8_confront").speaker);
            Assert.AreEqual("하월", LastLine("ch12_confront").speaker);
            // 저울은 선택지 장면이다 — 대결은 고른 결과 대사 뒤에 열리므로 결과 둘이 도전으로 끝난다.
            Assert.AreEqual("저울", LastLine("ch9_confront_answer").speaker);
            Assert.AreEqual("저울", LastLine("ch9_confront_silent").speaker);
        }

        private static StoryLine LastLine(string beatId)
        {
            StoryBeat b = RealBeat(beatId);
            Assert.Greater(b.lines.Count, 0, beatId);
            return b.lines[b.lines.Count - 1];
        }

        [Test]
        public void RealStory_RaonsFirstMeeting_OpensTheMeadowStageRightAway()
        {
            // 대사를 닫는 순간(CompleteBeat → MarkSeen 뒤 StoryBeatCompleted) 그 자리에서 단계가 골라져야 한다.
            Assert.AreEqual("meadow", RealBeat("ch1_rival_intro").requiredRegionId);
            Assert.IsTrue(NpcRivalDuels.TrySelect("catcher_rival", "meadow",
                Seen("ch1_intro", "ch1_first_capture", "ch1_rival_intro"), _ => false, out NpcRivalDuels.Stage s));
            Assert.AreEqual("rival_meadow", s.stageId);
        }

        [TestCase("duel_grip_win", "ch8_confront", "ch9_arrive")]
        [TestCase("duel_scale_win", "ch9_confront", "ch10_arrive")]
        [TestCase("duel_chief_win", "ch12_confront", "fin_unnamed")]
        public void RealStory_BossWin_GatesTheNextStep(string winId, string confrontId, string nextId)
        {
            StoryBeat win = RealBeat(winId);
            Assert.AreEqual(StoryDirector.TriggerDuelWin, win.trigger.type);
            Assert.AreEqual(confrontId, win.requiredBeatId, $"{winId}: 대치를 본 뒤에만 뜬다");
            // 리전 게이트를 두지 않는다 — 자동 통과(재확인)가 어디서든 이어지게. DuelWin 소스가 그 간부 대결뿐이라 엉뚱한 리전 발화도 없다.
            Assert.IsTrue(string.IsNullOrEmpty(win.requiredRegionId), $"{winId}: 리전 게이트가 자동 통과를 그 리전으로 묶는다");
            Assert.IsFalse(string.IsNullOrWhiteSpace(win.why), $"{winId}: 스파인 — HUD 이유가 필요하다");
            Assert.AreEqual(winId, RealBeat(nextId).prerequisiteBeatId, $"{nextId}: 간부를 이겨야 열린다");
        }

        [Test]
        public void RealStory_RetiredTauntBeats_AreGone()
        {
            // "한 번 더 말을 걸면 붙는다"(talk_*)는 대사 직후 대결로 바뀌었다. 남아 있으면 진 뒤 재도전하러
            // 말을 걸 때 그 대사가 대결을 가로막는다(WorldInteractionController는 NpcTalk 비트가 뜨면 대결을 안 연다).
            foreach (string id in new[] { "talk_grip", "talk_scale", "talk_chief" })
                Assert.IsFalse(StoryService.TryGetBeat(id, out _), id);
            foreach (StoryBeat b in StoryService.AllBeats())
            {
                if (b == null || b.trigger == null || b.trigger.type != StoryDirector.TriggerNpcTalk) continue;
                if (b.trigger.param != "ledger_grip" && b.trigger.param != "ledger_scale") continue;
                Assert.Fail($"{b.beatId}: 집게·저울에게 거는 말 걸기 비트가 재도전을 가로막는다");
            }
        }

        // ── 자동 통과 — 이미 이긴 세이브 ──

        [Test]
        public void AlreadyBeatenBoss_WinBeatFiresOnResweep_AnywhereAndOnce()
        {
            StoryBeat win = RealBeat("duel_grip_win");
            GameObject directorHost = new GameObject("StoryDuelFlowTestsDirector");
            GameObject stateHost = new GameObject("StoryDuelFlowTestsState");
            stateHost.SetActive(false);   // Awake/저장 로드를 돌리지 않는다
            try
            {
                var director = directorHost.AddComponent<StoryDirector>();
                var region = stateHost.AddComponent<RegionManager>();
                var duel = stateHost.AddComponent<NpcDuelController>();
                // 서릿길에 서 있다 — 이야기 잠금 전에 이미 열렸던 세이브. 승리 비트를 놓친 채다.
                SetField(region, "currentRegion", new InsectGame.Data.RegionData { regionId = "frostline" });
                SetField(duel, "bossStateLoaded", true);
                ((HashSet<string>)GetField(duel, "defeatedBosses")).Add("ledger_grip");
                director.AutoWire(region, null, null, null, null);
                director.AutoWire(duel);

                WithStoryCache(director, new[] { win }, () =>
                {
                    var fired = new List<string>();
                    director.StoryBeatTriggered += b => fired.Add(b.beatId);

                    Invoke(director, "ResweepPersistentConditions");
                    Invoke(director, "DrainPendingTriggers");
                    Assert.IsEmpty(fired, "대치를 보기 전에는 뜨지 않는다");

                    Invoke(director, "MarkSeen", "ch8_confront");
                    Invoke(director, "ResweepPersistentConditions");
                    Invoke(director, "DrainPendingTriggers");
                    CollectionAssert.AreEqual(new[] { "duel_grip_win" }, fired,
                        "다시 싸우지 않아도 재확인만으로 이어진다(다른 리전에서도)");

                    Invoke(director, "MarkSeen", "duel_grip_win");
                    SetField(director, "pendingBeatId", null);
                    Invoke(director, "ResweepPersistentConditions");
                    Invoke(director, "DrainPendingTriggers");
                    Assert.AreEqual(1, fired.Count, "본 승리 비트를 다시 띄우지 않는다");
                    Assert.IsFalse(director.IsBusy, "큐가 비었다 — 대사 직후 대결이 기다리지 않는다");
                });
            }
            finally
            {
                Object.DestroyImmediate(directorHost);
                Object.DestroyImmediate(stateHost);
            }
        }

        [Test]
        public void IsBusy_TracksTheStoryQueue()
        {
            GameObject host = new GameObject("StoryDuelFlowTestsBusy");
            try
            {
                var director = host.AddComponent<StoryDirector>();
                Assert.IsFalse(director.IsBusy);
                SetField(director, "pendingBeatId", "reading");
                Assert.IsTrue(director.IsBusy, "대사가 떠 있다");
                SetField(director, "pendingBeatId", null);
                director.QueueChoice("ch9_confront_answer");   // 선택지 결과가 큐에 들어간다
                Assert.IsTrue(director.IsBusy, "선택지 결과 대사가 아직 남았다 — 대결이 먼저 열리면 안 된다");
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ── 나중에 끼워 넣은 스파인 ──

        [Test]
        public void LiveSpine_OnlyBeatsWithUnseenFollowers()
        {
            var beats = new[]
            {
                new StoryBeat { beatId = "win", chapterId = "ch8" },
                new StoryBeat { beatId = "arrive", chapterId = "ch9", prerequisiteBeatId = "win" },
                new StoryBeat { beatId = "next", chapterId = "ch9", prerequisiteBeatId = "arrive" },
            };
            HashSet<string> fresh = StoryObjectiveResolver.CollectLiveSpineBeatIds(beats, Seen());
            CollectionAssert.AreEquivalent(new[] { "win", "arrive" }, fresh, "정상 진행에서는 정적 스파인과 같다");

            HashSet<string> legacy = StoryObjectiveResolver.CollectLiveSpineBeatIds(beats, Seen("arrive"));
            Assert.IsFalse(legacy.Contains("win"), "뒤를 이미 본 선행은 더 잇는 것이 없다");
            Assert.IsTrue(legacy.Contains("arrive"));
        }

        [Test]
        public void RealStory_SaveThatPassedFrostlineWithoutGrip_IsNotSentBack()
        {
            // 이야기 잠금 전에 서릿길을 지나온 세이브 — ch9까지 다 봤고 집게는 안 이겼다. 다음 목표는 잿불 쪽이어야 한다.
            var beats = new List<StoryBeat>(StoryService.AllBeats());
            Assume.That(beats.Count, Is.GreaterThan(0), "Story.json 로드 실패");
            var seen = new HashSet<string>();
            foreach (StoryBeat b in beats)
            {
                int rank = StoryObjectiveResolver.ChapterRank(b.chapterId);
                if (rank <= 9 && b.beatId != "duel_grip_win") seen.Add(b.beatId);
            }
            seen.Add("duel_scale_win");
            Assume.That(seen.Contains("ch9_arrive"));

            StoryBeat chosen = StoryObjectiveResolver.SelectObjectiveBeat(beats, seen.Contains,
                StoryObjectiveResolver.CollectLiveSpineBeatIds(beats, seen.Contains), null,
                StoryObjectiveResolver.CollectChoiceTargetIds(beats));
            Assert.IsNotNull(chosen);
            Assert.AreNotEqual("duel_grip_win", chosen.beatId, "지나온 장의 간부전으로 HUD가 되돌려 보낸다");
            Assert.AreEqual(10, StoryObjectiveResolver.ChapterRank(chosen.chapterId), $"다음 목표 {chosen.beatId}({chosen.chapterId})");
        }

        [Test]
        public void DescribeDuelLockObjective_NamesTheOpponent()
        {
            Assert.AreEqual("서릿길(으)로 가려면 집게에게 이기기",
                StoryObjectiveResolver.DescribeDuelLockObjective("서릿길", "집게"));
            Assert.AreEqual("서릿길(으)로", StoryObjectiveResolver.DescribeDuelLockObjective("서릿길", null));
        }

        // ── 리플렉션 도우미 (StoryDeferGateTests와 같은 방식 — 저장·보상 부작용 없이 상태만 세운다) ──

        private static void SetField(object target, string name, object value)
            => target.GetType().GetField(name, Private).SetValue(target, value);

        private static object GetField(object target, string name)
            => target.GetType().GetField(name, Private).GetValue(target);

        private static void Invoke(StoryDirector director, string name, params object[] args)
            => typeof(StoryDirector).GetMethod(name, Private).Invoke(director, args);

        private static void WithStoryCache(StoryDirector director, StoryBeat[] beats, System.Action action)
        {
            FieldInfo cache = typeof(StoryService).GetField("cache", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = cache.GetValue(null);
            var fixture = new Dictionary<string, StoryBeat>();
            foreach (StoryBeat beat in beats) fixture.Add(beat.beatId, beat);
            cache.SetValue(null, fixture);
            typeof(StoryDirector).GetField("progress", Private).SetValue(director, new StoryProgressData());
            try { action(); }
            finally { cache.SetValue(null, previous); }
        }
    }
}
#endif
