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
            string prereq = null, string triggerType = "NpcTalk", string param = "npc",
            string gate = null)
        {
            return new StoryBeat
            {
                beatId = id,
                chapterId = chapter,
                order = order,
                prerequisiteBeatId = prereq,
                requiredBeatId = gate,
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

        // ── 순위 비교 (목표 도출과 실제 발화가 공유하는 단일 출처) ──

        [Test]
        public void CompareBeatPriority_SpineWinsOverLeaf()
        {
            var spine = new HashSet<string> { "s" };
            StoryBeat s = Beat("s", "ch5", 9);
            StoryBeat leaf = Beat("l", "ch1", 0);
            // 스파인이 챕터·order를 모두 이긴다 — 놓치면 체인이 끊기는 쪽이 먼저다.
            Assert.Less(StoryObjectiveResolver.CompareBeatPriority(s, leaf, spine), 0);
            Assert.Greater(StoryObjectiveResolver.CompareBeatPriority(leaf, s, spine), 0);
        }

        [Test]
        public void CompareBeatPriority_ChapterBeatsOrder_AndNumericNotLexical()
        {
            var spine = new HashSet<string>();
            // "ch10"은 문자열 정렬로는 "ch2"보다 앞이지만 이야기상 뒤다.
            Assert.Less(
                StoryObjectiveResolver.CompareBeatPriority(Beat("a", "ch2", 99), Beat("b", "ch10", 0), spine), 0);
        }

        [Test]
        public void CompareBeatPriority_MainStoryBeatsSideChapters()
        {
            var spine = new HashSet<string>();
            // 곁이야기(npc/side)는 본편 뒤로 민다 — 이게 이번 회귀의 핵심이다.
            Assert.Less(
                StoryObjectiveResolver.CompareBeatPriority(Beat("a", "ch1", 0), Beat("b", "npc", 0), spine), 0);
            Assert.Less(
                StoryObjectiveResolver.CompareBeatPriority(Beat("a", "ch12", 0), Beat("b", "side", 0), spine), 0);
        }

        [Test]
        public void CompareBeatPriority_SameRank_FallsBackToBeatId_SoOrderIsDeterministic()
        {
            var spine = new HashSet<string>();
            Assert.Less(
                StoryObjectiveResolver.CompareBeatPriority(Beat("aaa", "ch1", 3), Beat("bbb", "ch1", 3), spine), 0);
            Assert.AreEqual(0,
                StoryObjectiveResolver.CompareBeatPriority(Beat("same", "ch1", 3), Beat("same", "ch1", 3), spine));
        }

        [Test]
        public void CompareBeatPriority_NullsSortLast()
        {
            var spine = new HashSet<string>();
            Assert.Less(StoryObjectiveResolver.CompareBeatPriority(Beat("a", "ch1", 0), null, spine), 0);
            Assert.Greater(StoryObjectiveResolver.CompareBeatPriority(null, Beat("a", "ch1", 0), spine), 0);
            Assert.AreEqual(0, StoryObjectiveResolver.CompareBeatPriority(null, null, spine));
        }

        [Test]
        public void CompareBeatPriority_CampaignOpenerBeatsAmbientChatter()
        {
            // 실제로 겹치던 쌍이다: 마을 어르신에게 말을 걸면 1막 개막(ch1_intro)과
            // 앰비언트 잡담(talk_elder)이 **함께** 자격을 갖는다. 둘 다 스파인이라
            // 챕터 순위가 가른다 — 이게 뒤집히면 처음 하는 사람이 프롤로그 컷신 대신
            // 잡담을 보고, 어느 쪽이 뜰지는 실행마다 달라진다.
            var spine = new HashSet<string> { "ch1_intro", "talk_elder" };
            StoryBeat opener = Beat("ch1_intro", "ch1", 0, param: "village_elder");
            StoryBeat chatter = Beat("talk_elder", "npc", 0, param: "village_elder");

            Assert.Less(StoryObjectiveResolver.CompareBeatPriority(opener, chatter, spine), 0);

            // 선택기를 통해서도 같은 답이 나와야 한다(발화 쪽이 같은 함수를 쓴다).
            StoryBeat chosen = StoryObjectiveResolver.SelectObjectiveBeat(
                new[] { chatter, opener }, Seen(), spine);
            Assert.AreEqual("ch1_intro", chosen.beatId);

            // 입력 순서가 바뀌어도 같아야 한다 — AllBeats()는 Dictionary.Values라 순서가 없다.
            chosen = StoryObjectiveResolver.SelectObjectiveBeat(
                new[] { opener, chatter }, Seen(), spine);
            Assert.AreEqual("ch1_intro", chosen.beatId);
        }

        // ── 진행 게이트 (requiredBeatId) ──

        [Test]
        public void SelectObjectiveBeat_UnsatisfiedBeatGate_IsSkipped()
        {
            // 실제로 났던 모양이다: 여운 비트가 "같은 NPC의 직전 여운"만 prereq로 물고 있어서
            // 시작 지역에서 말만 반복해도 뒷 챕터 대사가 나왔다. 게이트를 걸면 목표에서도
            // 빠져야 한다 — 발화 쪽(StoryDirector.BeatGateSatisfied)과 답이 갈리면
            // 잠긴 비트를 안내해 놓고 가 보면 아무 일도 안 일어난다.
            var gated = Beat("ch11_echo", "ch11", 45, prereq: "talk_elder", gate: "ch11_arrive");
            var beats = new[] { gated };

            StoryBeat chosen = StoryObjectiveResolver.SelectObjectiveBeat(
                beats, Seen("talk_elder"), StoryObjectiveResolver.CollectSpineBeatIds(beats));

            Assert.IsNull(chosen, "prereq만 충족하고 진행 게이트가 안 열렸으면 목표가 아니다");
        }

        [Test]
        public void SelectObjectiveBeat_SatisfiedBeatGate_IsChosen()
        {
            var gated = Beat("ch11_echo", "ch11", 45, prereq: "talk_elder", gate: "ch11_arrive");
            var beats = new[] { gated };

            StoryBeat chosen = StoryObjectiveResolver.SelectObjectiveBeat(
                beats, Seen("talk_elder", "ch11_arrive"),
                StoryObjectiveResolver.CollectSpineBeatIds(beats));

            Assert.AreEqual("ch11_echo", chosen.beatId);
        }

        [Test]
        public void SelectObjectiveBeat_BeatGateWithoutPrerequisite_StillGates()
        {
            // ch10_echo가 이 모양이다 — prereq가 아예 없어서 처음부터 자격을 갖고 있었다.
            var beats = new[] { Beat("ch10_echo", "ch10", 40, gate: "ch10_arrive") };

            Assert.IsNull(StoryObjectiveResolver.SelectObjectiveBeat(
                beats, Seen(), StoryObjectiveResolver.CollectSpineBeatIds(beats)));
            Assert.IsNotNull(StoryObjectiveResolver.SelectObjectiveBeat(
                beats, Seen("ch10_arrive"), StoryObjectiveResolver.CollectSpineBeatIds(beats)));
        }

        // ── 목표 종류: 리전이 실린 포획/전투 ──

        [Test]
        public void KindOf_CaptureOrBattleWithRegion_IsActInRegion()
        {
            // 무param 포획/전투는 트리거가 위치를 안 싣지만 리전 게이트가 사실상 위치다.
            // 예전엔 전부 Freeform이라 HUD가 "모험을 이어가세요"만 띄웠다.
            Assert.AreEqual(StoryObjectiveKind.ActInRegion,
                StoryObjectiveResolver.KindOf("CaptureInsect", "meadow"));
            Assert.AreEqual(StoryObjectiveKind.ActInRegion,
                StoryObjectiveResolver.KindOf("BattleWin", "swamp"));

            // 리전이 없으면 갈 곳이 없다 — 화살표를 띄우면 없는 곳을 가리킨다.
            Assert.AreEqual(StoryObjectiveKind.Freeform,
                StoryObjectiveResolver.KindOf("CaptureInsect", null));
            Assert.AreEqual(StoryObjectiveKind.Freeform,
                StoryObjectiveResolver.KindOf("BattleWin", ""));

            // 레벨·도감은 리전이 있어도 위치 목표가 아니다.
            Assert.AreEqual(StoryObjectiveKind.Freeform,
                StoryObjectiveResolver.KindOf("LevelReach", "meadow"));
            Assert.AreEqual(StoryObjectiveKind.Freeform,
                StoryObjectiveResolver.KindOf("DexProgress", "meadow"));
        }

        [TestCase("LevelReach", "3", 3)]
        [TestCase("DexProgress", "60", 60)]
        [TestCase("LevelReach", "숫자아님", -1)]
        [TestCase("CaptureInsect", "5", -1)]
        [TestCase("BattleWin", "", -1)]
        public void ThresholdOf_OnlyProgressTriggers(string triggerType, string param, int expected)
        {
            Assert.AreEqual(expected, StoryObjectiveResolver.ThresholdOf(triggerType, param));
        }

        // ── 목표 문구 ──

        [Test]
        public void DescribeActionObjective_Capture_NamesRegionOnlyWhenElsewhere()
        {
            Assert.AreEqual("연못에서 왕잠자리 포획", StoryObjectiveResolver.DescribeActionObjective(
                "CaptureInsect", "연못", false, "왕잠자리", null, -1, -1));
            Assert.AreEqual("왕잠자리 포획하기", StoryObjectiveResolver.DescribeActionObjective(
                "CaptureInsect", "연못", true, "왕잠자리", null, -1, -1));
            Assert.AreEqual("초원에서 곤충 포획", StoryObjectiveResolver.DescribeActionObjective(
                "CaptureInsect", "초원", false, null, null, -1, -1));
            Assert.AreEqual("야생 곤충 1마리 포획", StoryObjectiveResolver.DescribeActionObjective(
                "CaptureInsect", "초원", true, null, null, -1, -1));
        }

        [Test]
        public void DescribeActionObjective_BattleWin_NamesRegionOnlyWhenElsewhere()
        {
            Assert.AreEqual("습지에서 전투 승리", StoryObjectiveResolver.DescribeActionObjective(
                "BattleWin", "습지", false, null, null, -1, -1));
            Assert.AreEqual("야생 곤충과 전투 승리", StoryObjectiveResolver.DescribeActionObjective(
                "BattleWin", "습지", true, null, null, -1, -1));
        }

        [Test]
        public void DescribeActionObjective_BattleWin_NamesSpeciesWhenTargeted()
        {
            // 종을 지정한 전투 비트(fin_seal)는 **무엇을 이겨야 하는지** 말해야 한다.
            // 안 그러면 "이름 없는 자리에서 전투 승리"로 떨어져 아무거나 이기면 되는 줄 안다 —
            // 그게 정확히 종 지정을 도입하기 전 엔딩이 터지던 조건이다.
            Assert.AreEqual("이름 없는 자리에서 이름 없는 사마귀 쓰러뜨리기",
                StoryObjectiveResolver.DescribeActionObjective(
                    "BattleWin", "이름 없는 자리", false, "이름 없는 사마귀", null, -1, -1));
            Assert.AreEqual("이름 없는 사마귀 쓰러뜨리기",
                StoryObjectiveResolver.DescribeActionObjective(
                    "BattleWin", "이름 없는 자리", true, "이름 없는 사마귀", null, -1, -1));
        }

        [Test]
        public void DescribeActionObjective_ProgressTriggers_ShowCurrentValue()
        {
            // 현재값이 있으면 함께 보여준다 — "얼마나 남았는가"가 곧 안내다.
            Assert.AreEqual("트레이너 Lv.3 달성 · 현재 Lv.2", StoryObjectiveResolver.DescribeActionObjective(
                "LevelReach", "", false, null, null, 3, 2));
            Assert.AreEqual("도감 60종 기록 · 현재 42종", StoryObjectiveResolver.DescribeActionObjective(
                "DexProgress", "", false, null, null, 60, 42));

            // 현재값을 모르면(-1) 임계값만. 참조가 미주입이어도 안내가 사라지지 않는다.
            Assert.AreEqual("트레이너 Lv.3 달성", StoryObjectiveResolver.DescribeActionObjective(
                "LevelReach", "", false, null, null, 3, -1));
        }

        [Test]
        public void DescribeActionObjective_QuestComplete_UsesTitle()
        {
            Assert.AreEqual("'첫 수문장' 완료하기", StoryObjectiveResolver.DescribeActionObjective(
                "QuestComplete", "", false, null, "첫 수문장", -1, -1));
            Assert.AreEqual("퀘스트 완료하기", StoryObjectiveResolver.DescribeActionObjective(
                "QuestComplete", "", false, null, null, -1, -1));
        }

        // ── 스토리 인물과 만난 적 있는가 (보스전 게이트) ──

        [Test]
        public void HasMetNpc_SeenSpeakerBeat_CountsAsMet()
        {
            // 대치 비트(SubAreaEnter)는 화자로만 인물을 싣는다 — 집게·저울·관장의 소개가 그 형태다.
            var beat = Beat("ch8_confront", "ch8", 28, triggerType: "SubAreaEnter", param: "dunes_vault");
            beat.speakerNpcId = "ledger_grip";
            var beats = new[] { beat };

            Assert.IsFalse(StoryObjectiveResolver.HasMetNpc(beats, Seen(), "ledger_grip"),
                "아직 못 봤으면 만난 적 없다 — 여기서 true면 소개 전에 보스전이 열린다");
            Assert.IsTrue(StoryObjectiveResolver.HasMetNpc(beats, Seen("ch8_confront"), "ledger_grip"));
        }

        [Test]
        public void HasMetNpc_SeenNpcTalkBeat_CountsAsMet()
        {
            var beats = new[] { Beat("talk_grip", "ch8", 80, param: "ledger_grip") };

            Assert.IsFalse(StoryObjectiveResolver.HasMetNpc(beats, Seen(), "ledger_grip"));
            Assert.IsTrue(StoryObjectiveResolver.HasMetNpc(beats, Seen("talk_grip"), "ledger_grip"));
        }

        [Test]
        public void HasMetNpc_OtherNpcOrEmptyInput_IsFalse()
        {
            var beats = new[] { Beat("talk_grip", "ch8", 80, param: "ledger_grip") };

            Assert.IsFalse(StoryObjectiveResolver.HasMetNpc(beats, Seen("talk_grip"), "ledger_chief"));
            Assert.IsFalse(StoryObjectiveResolver.HasMetNpc(beats, Seen("talk_grip"), null));
            Assert.IsFalse(StoryObjectiveResolver.HasMetNpc(null, Seen("talk_grip"), "ledger_grip"));
        }

        [Test]
        public void DescribeActionObjective_UnknownTrigger_FallsBackWithoutCrashing()
        {
            Assert.AreEqual("초원(으)로", StoryObjectiveResolver.DescribeActionObjective(
                "오타난타입", "초원", false, null, null, -1, -1));
            Assert.AreEqual("모험을 이어가세요", StoryObjectiveResolver.DescribeActionObjective(
                "오타난타입", "", false, null, null, -1, -1));
        }
    }
}
#endif
