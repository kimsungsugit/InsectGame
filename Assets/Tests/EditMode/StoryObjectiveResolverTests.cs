#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Story;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 메인퀘스트 "다음 목표" 도출의 순수 부분. 씬 없이 도는 로직만 다룬다
    /// (월드 좌표 해석·자동 주행은 StoryObjectiveTracker/PlayerMovement 쪽이고 기기 확인 대상).
    /// </summary>
    [TestFixture]
    public class StoryObjectiveResolverTests
    {
        private static StoryBeat Beat(string id, string chapter, int order,
            string prereq = null, string triggerType = "NpcTalk", string param = "npc")
        {
            return new StoryBeat
            {
                beatId = id,
                chapterId = chapter,
                order = order,
                prerequisiteBeatId = prereq,
                trigger = new StoryTrigger { type = triggerType, param = param }
            };
        }

        private static System.Func<string, bool> Seen(params string[] ids)
        {
            var set = new HashSet<string>(ids);
            return id => set.Contains(id);
        }

        // ── 스파인 수집 ──

        [Test]
        public void CollectSpineBeatIds_ReferencedAsPrerequisite_IsSpine()
        {
            var beats = new[] { Beat("a", "ch1", 1), Beat("b", "ch1", 2, prereq: "a") };

            HashSet<string> spine = StoryObjectiveResolver.CollectSpineBeatIds(beats);

            Assert.IsTrue(spine.Contains("a"), "prerequisite로 지목된 a가 스파인이어야 한다");
            Assert.IsFalse(spine.Contains("b"), "아무도 지목하지 않는 b는 leaf다");
        }

        [Test]
        public void CollectSpineBeatIds_NullInput_ReturnsEmpty()
        {
            Assert.AreEqual(0, StoryObjectiveResolver.CollectSpineBeatIds(null).Count);
        }

        // ── 챕터 순위 ──

        [Test]
        public void ChapterRank_TwoDigitChapter_SortsAfterSingleDigit()
        {
            // 문자열 정렬이면 "ch10" < "ch2"라 10장이 2장보다 앞에 온다 — 그 회귀를 막는다.
            Assert.Less(StoryObjectiveResolver.ChapterRank("ch2"), StoryObjectiveResolver.ChapterRank("ch10"));
            Assert.Less(StoryObjectiveResolver.ChapterRank("ch9"), StoryObjectiveResolver.ChapterRank("ch12"));
        }

        [Test]
        public void ChapterRank_FinaleAndSideChapters_SortAfterMainChapters()
        {
            int ch12 = StoryObjectiveResolver.ChapterRank("ch12");
            Assert.Greater(StoryObjectiveResolver.ChapterRank("fin"), ch12);
            Assert.Greater(StoryObjectiveResolver.ChapterRank("side"), StoryObjectiveResolver.ChapterRank("fin"));
            Assert.Greater(StoryObjectiveResolver.ChapterRank("npc"), StoryObjectiveResolver.ChapterRank("fin"));
        }

        // ── 트리거 → 목표 종류 ──

        [TestCase("NpcTalk", StoryObjectiveKind.TalkToNpc)]
        [TestCase("RegionEnter", StoryObjectiveKind.EnterRegion)]
        [TestCase("SubAreaEnter", StoryObjectiveKind.EnterSubArea)]
        [TestCase("GuardianDefeat", StoryObjectiveKind.DefeatGuardian)]
        [TestCase("BattleWin", StoryObjectiveKind.Freeform)]
        [TestCase("CaptureInsect", StoryObjectiveKind.Freeform)]
        [TestCase("LevelReach", StoryObjectiveKind.Freeform)]
        [TestCase("DexProgress", StoryObjectiveKind.Freeform)]
        [TestCase("오타난타입", StoryObjectiveKind.Freeform)]
        public void KindOf_MapsTriggerTypes(string triggerType, StoryObjectiveKind expected)
        {
            Assert.AreEqual(expected, StoryObjectiveResolver.KindOf(triggerType));
        }

        // ── 목표 선택 ──

        [Test]
        public void SelectObjectiveBeat_UnsatisfiedPrerequisite_IsSkipped()
        {
            var beats = new[] { Beat("a", "ch1", 1), Beat("b", "ch1", 2, prereq: "a") };

            StoryBeat chosen = StoryObjectiveResolver.SelectObjectiveBeat(
                beats, Seen(), StoryObjectiveResolver.CollectSpineBeatIds(beats));

            Assert.AreEqual("a", chosen.beatId, "a를 아직 안 봤으므로 b는 차례가 아니다");
        }

        [Test]
        public void SelectObjectiveBeat_SeenBeat_IsSkipped()
        {
            var beats = new[] { Beat("a", "ch1", 1), Beat("b", "ch1", 2, prereq: "a") };

            StoryBeat chosen = StoryObjectiveResolver.SelectObjectiveBeat(
                beats, Seen("a"), StoryObjectiveResolver.CollectSpineBeatIds(beats));

            Assert.AreEqual("b", chosen.beatId);
        }

        [Test]
        public void SelectObjectiveBeat_SpineBeatWinsOverLeaf()
        {
            // leaf(flavor)는 같은 챕터의 더 앞 order라도 스파인에 밀린다.
            var spineHead = Beat("spine", "ch1", 9);
            var leaf = Beat("leaf", "ch1", 1);
            var next = Beat("next", "ch1", 10, prereq: "spine");
            var beats = new[] { leaf, spineHead, next };

            StoryBeat chosen = StoryObjectiveResolver.SelectObjectiveBeat(
                beats, Seen(), StoryObjectiveResolver.CollectSpineBeatIds(beats));

            Assert.AreEqual("spine", chosen.beatId);
        }

        [Test]
        public void SelectObjectiveBeat_OnlyLeavesRemain_StillReturnsOne()
        {
            // 캠페인 마지막 비트는 아무도 prerequisite로 지목하지 않아 스파인 집합에 없다.
            // 스파인만 고집하면 최종장에서 목표가 사라진다.
            var beats = new[] { Beat("finale", "fin", 1) };

            StoryBeat chosen = StoryObjectiveResolver.SelectObjectiveBeat(
                beats, Seen(), StoryObjectiveResolver.CollectSpineBeatIds(beats));

            Assert.IsNotNull(chosen);
            Assert.AreEqual("finale", chosen.beatId);
        }

        [Test]
        public void SelectObjectiveBeat_SameChapterAndOrder_IsDeterministicByBeatId()
        {
            // AllBeats()는 Dictionary.Values라 순서가 비결정적이다. 입력 순서를 뒤집어도
            // 같은 답이 나와야 "실행마다 목표가 바뀌는" 증상이 안 생긴다.
            var forward = new[] { Beat("zeta", "ch1", 1), Beat("alpha", "ch1", 1) };
            var reversed = new[] { Beat("alpha", "ch1", 1), Beat("zeta", "ch1", 1) };

            StoryBeat a = StoryObjectiveResolver.SelectObjectiveBeat(forward, Seen(), null);
            StoryBeat b = StoryObjectiveResolver.SelectObjectiveBeat(reversed, Seen(), null);

            Assert.AreEqual("alpha", a.beatId);
            Assert.AreEqual(a.beatId, b.beatId, "입력 순서가 결과를 바꾸면 안 된다");
        }

        [Test]
        public void SelectObjectiveBeat_EarlierChapterWins()
        {
            var beats = new[] { Beat("late", "ch10", 1), Beat("early", "ch2", 99) };

            StoryBeat chosen = StoryObjectiveResolver.SelectObjectiveBeat(beats, Seen(), null);

            Assert.AreEqual("early", chosen.beatId, "ch2가 ch10보다 앞이다(문자열 정렬이면 뒤집힌다)");
        }

        [Test]
        public void SelectObjectiveBeat_AllSeen_ReturnsNull()
        {
            var beats = new[] { Beat("a", "ch1", 1) };

            Assert.IsNull(StoryObjectiveResolver.SelectObjectiveBeat(beats, Seen("a"), null));
        }

        [Test]
        public void SelectObjectiveBeat_NullArguments_ReturnNull()
        {
            Assert.IsNull(StoryObjectiveResolver.SelectObjectiveBeat(null, Seen(), null));
            Assert.IsNull(StoryObjectiveResolver.SelectObjectiveBeat(new StoryBeat[0], null, null));
        }

        [Test]
        public void SelectObjectiveBeat_QuestGateNotMet_IsNotAdvertised()
        {
            // 잠긴 목표를 안내하면 "시킨 대로 갔는데 아무 일도 안 일어나는" 상태가 된다.
            var gated = Beat("gated", "ch1", 1);
            gated.requiredQuestId = "q_dex";
            var beats = new[] { gated };

            Assert.IsNull(
                StoryObjectiveResolver.SelectObjectiveBeat(beats, Seen(), null, _ => false),
                "퀘스트 미완료인데 목표로 안내했다");

            Assert.IsNotNull(
                StoryObjectiveResolver.SelectObjectiveBeat(beats, Seen(), null, _ => true),
                "퀘스트를 마쳤는데 목표가 안 뜬다");
        }

        [Test]
        public void SelectObjectiveBeat_NoQuestPredicate_IgnoresGate()
        {
            // 판정자를 안 넘기면(퀘스트 매니저 미주입) 게이트를 무시한다 —
            // 목표가 영영 안 뜨는 것보다 조금 이르게 뜨는 쪽이 낫다.
            var gated = Beat("gated", "ch1", 1);
            gated.requiredQuestId = "q_dex";

            Assert.IsNotNull(StoryObjectiveResolver.SelectObjectiveBeat(
                new[] { gated }, Seen(), null));
        }

        // ── 실제 Story.json으로 도는 회귀 ──

        // ── 튜토리얼 ↔ 스토리 분리 ──

        [Test]
        public void RealStoryData_Ch1Intro_StartsByTalkingToElder()
        {
            // 예전엔 Immediate라 게임을 켜자마자 마을 어르신의 "오, 드디어 왔구나!"가 떴다 —
            // 만나지도 않았는데 인사를 받는 셈이었고 조작을 배우기 전에 서사가 시작됐다.
            StoryBeat intro = null;
            foreach (StoryBeat b in StoryService.AllBeats())
                if (b != null && b.beatId == "ch1_intro") { intro = b; break; }

            Assert.IsNotNull(intro, "ch1_intro가 없다 — 캠페인 진입점이 사라졌다");
            Assert.AreEqual("NpcTalk", intro.trigger.type,
                "1막은 마을 어르신에게 말을 걸어 시작한다");
            Assert.AreEqual("village_elder", intro.trigger.param);
            Assert.AreEqual(intro.speakerNpcId, intro.trigger.param,
                "화자와 대화 상대가 다르면 엉뚱한 NPC가 그 대사를 한다");
        }

        [Test]
        public void RealStoryData_Ch1Intro_IsGatedBehindTutorial()
        {
            // 게이트가 빠지면 튜토리얼을 건너뛰고 서사가 열려 분리가 무의미해진다.
            StoryBeat intro = null;
            foreach (StoryBeat b in StoryService.AllBeats())
                if (b != null && b.beatId == "ch1_intro") { intro = b; break; }

            Assert.IsNotNull(intro);
            Assert.IsFalse(string.IsNullOrEmpty(intro.requiredQuestId),
                "튜토리얼 게이트가 없다 — 조작을 배우기 전에 스토리가 열린다");
        }

        [Test]
        public void RealStoryData_EveryFirstChapterBeat_ChainsFromIntro()
        {
            // 나머지 ch1 비트는 포획·전투로 자동 발화하는 트리거를 쓴다. intro를 prereq로
            // 삼는 사슬이 끊기면 **스토리를 시작하지 않았는데** 라온 소개 같은 비트가 먼저 뜬다.
            var byId = new Dictionary<string, StoryBeat>();
            foreach (StoryBeat b in StoryService.AllBeats())
                if (b != null && !string.IsNullOrEmpty(b.beatId)) byId[b.beatId] = b;

            foreach (StoryBeat b in byId.Values)
            {
                if (b.chapterId != "ch1" || b.beatId == "ch1_intro") continue;

                // prereq 사슬을 거슬러 올라가면 ch1_intro에 닿아야 한다.
                string cursor = b.prerequisiteBeatId;
                bool reachesIntro = false;
                for (int guard = 0; guard < byId.Count && !string.IsNullOrEmpty(cursor); guard++)
                {
                    if (cursor == "ch1_intro") { reachesIntro = true; break; }
                    cursor = byId.TryGetValue(cursor, out StoryBeat prev) ? prev.prerequisiteBeatId : null;
                }

                Assert.IsTrue(reachesIntro,
                    $"{b.beatId}가 ch1_intro 없이 발화할 수 있다 — 스토리 시작 전에 뜬다");
            }
        }

        [Test]
        public void SelectObjectiveBeat_RealStoryData_NewSaveStartsAtFirstChapter()
        {
            var beats = new List<StoryBeat>(StoryService.AllBeats());
            Assume.That(beats.Count, Is.GreaterThan(0), "Story.json 로드 실패");

            StoryBeat chosen = StoryObjectiveResolver.SelectObjectiveBeat(
                beats, Seen(), StoryObjectiveResolver.CollectSpineBeatIds(beats));

            Assert.IsNotNull(chosen, "신규 세이브인데 목표가 없다 — 캠페인 진입점이 막혔다");
            Assert.AreEqual(1, StoryObjectiveResolver.ChapterRank(chosen.chapterId),
                $"신규 세이브의 첫 목표가 1장이 아니다: {chosen.beatId}({chosen.chapterId})");
        }

        [Test]
        public void SelectObjectiveBeat_RealStoryData_AlwaysHasObjectiveUntilAllSeen()
        {
            // 비트를 하나씩 열람해 가며 매번 목표가 있는지 확인한다 — 중간에 null이 나오면
            // 그 지점에서 "다음에 뭘 해야 하는지" 안내가 끊긴다.
            var beats = new List<StoryBeat>(StoryService.AllBeats());
            Assume.That(beats.Count, Is.GreaterThan(0), "Story.json 로드 실패");
            HashSet<string> spine = StoryObjectiveResolver.CollectSpineBeatIds(beats);

            var seen = new HashSet<string>();
            for (int guard = 0; guard < beats.Count + 1; guard++)
            {
                StoryBeat next = StoryObjectiveResolver.SelectObjectiveBeat(beats, seen.Contains, spine);
                if (next == null)
                {
                    Assert.AreEqual(beats.Count, seen.Count,
                        $"전부 열람하기 전에 목표가 끊겼다 — {seen.Count}/{beats.Count}");
                    return;
                }
                seen.Add(next.beatId);
            }

            Assert.Fail("목표가 진행되지 않고 같은 비트를 반복한다");
        }
    }
}
#endif
