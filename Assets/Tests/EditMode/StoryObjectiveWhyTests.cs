#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Story;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// HUD 목표 행 두 번째 줄의 이유(<see cref="StoryBeat.why"/>). 트래커(<c>StoryObjectiveTracker.Why</c>)는
    /// MonoBehaviour라 고르는 부분만 순수 함수(<see cref="StoryObjectiveResolver.WhyFor"/>)로 떼어 여기서 고정한다.
    /// 실제 데이터 검사는 story_lint 검사 33과 같은 기준이다 — 대상 집합은 C#의 목표 우선순위가 정한다.
    /// </summary>
    [TestFixture]
    public class StoryObjectiveWhyTests
    {
        // story_lint 검사 33과 같은 값. 목표 행 둘째 줄(22pt, 한 줄)에 줄이지 않고 드는 길이에 여유를 둔 값이다.
        private const int MaxWhyChars = 20;

        private static StoryBeat Beat(string id, string chapter, string why = null,
            string prereq = null, string triggerType = "NpcTalk")
        {
            return new StoryBeat
            {
                beatId = id,
                chapterId = chapter,
                prerequisiteBeatId = prereq,
                why = why,
                trigger = new StoryTrigger { type = triggerType, param = "npc" },
            };
        }

        // ── WhyFor ──

        [Test]
        public void WhyFor_BeatWithWhy_ReturnsIt()
        {
            Assert.AreEqual("상자에 갇힌 곤충이 있대",
                StoryObjectiveResolver.WhyFor(Beat("ch8_arrive", "ch8", "상자에 갇힌 곤충이 있대"), false));
        }

        [Test]
        public void WhyFor_RedirectedToGatekeeper_ReturnsEmpty()
        {
            // 잠긴 리전이라 "모래언덕으로 가려면 들판 수문장 격파"로 바꿔 쳤다 — 이유는 모래언덕 비트의 것이라 붙이면 엉뚱하다.
            Assert.AreEqual(string.Empty,
                StoryObjectiveResolver.WhyFor(Beat("ch8_arrive", "ch8", "상자에 갇힌 곤충이 있대"), true));
        }

        [Test]
        public void WhyFor_NullBeatOrMissingWhy_ReturnsEmptyNotNull()
        {
            Assert.AreEqual(string.Empty, StoryObjectiveResolver.WhyFor(null, false));
            Assert.AreEqual(string.Empty, StoryObjectiveResolver.WhyFor(Beat("a", "ch1"), false));
            Assert.AreEqual(string.Empty, StoryObjectiveResolver.WhyFor(Beat("a", "ch1", string.Empty), false));
        }

        [Test]
        public void WhyFor_WhitespaceOrPaddedWhy_IsTrimmed()
        {
            Assert.AreEqual(string.Empty, StoryObjectiveResolver.WhyFor(Beat("a", "ch1", "   "), false));
            Assert.AreEqual("숲이 조용해졌대", StoryObjectiveResolver.WhyFor(Beat("a", "ch1", " 숲이 조용해졌대 "), false));
        }

        // ── 이유가 꼭 있어야 하는 급 — 목표 우선순위의 0급 ──

        [Test]
        public void IsObjectiveThread_SpineOrFinale_IsTrueAndLeafIsFalse()
        {
            var spine = new HashSet<string> { "ch3_spine" };

            Assert.IsTrue(StoryObjectiveResolver.IsObjectiveThread(Beat("ch3_spine", "ch3"), spine));
            Assert.IsTrue(StoryObjectiveResolver.IsObjectiveThread(Beat("fin_epilogue", StoryObjectiveResolver.FinaleChapterId), spine),
                "종장 leaf는 목표에서 스파인과 같은 급이다(CompareObjectivePriority)");
            Assert.IsFalse(StoryObjectiveResolver.IsObjectiveThread(Beat("gd_forest", "ch3"), spine));
            Assert.IsFalse(StoryObjectiveResolver.IsObjectiveThread(null, spine));
        }

        [Test]
        public void IsObjectiveThread_AgreesWithObjectivePriority()
        {
            // 0급 판정이 우선순위와 갈리면 lint·테스트가 엉뚱한 비트에 이유를 요구한다.
            var spine = new HashSet<string> { "a" };
            StoryBeat thread = Beat("a", "ch5");
            StoryBeat leaf = Beat("b", "ch1");   // 앞 장이어도 leaf는 0급 뒤다

            Assert.IsTrue(StoryObjectiveResolver.IsObjectiveThread(thread, spine));
            Assert.IsFalse(StoryObjectiveResolver.IsObjectiveThread(leaf, spine));
            Assert.Less(StoryObjectiveResolver.CompareObjectivePriority(thread, leaf, spine), 0);
        }

        // ── 실제 Story.json ──

        [Test]
        public void RealStory_EveryMainObjectiveThreadBeat_HasShortWhy()
        {
            var beats = new List<StoryBeat>(StoryService.AllBeats());
            Assume.That(beats.Count, Is.GreaterThan(0), "Story.json 로드 실패");
            HashSet<string> spine = StoryObjectiveResolver.CollectSpineBeatIds(beats);
            HashSet<string> choiceTargets = StoryObjectiveResolver.CollectChoiceTargetIds(beats);

            var missing = new List<string>();
            int required = 0;
            foreach (StoryBeat b in beats)
            {
                if (b == null || !IsMainChapter(b.chapterId)) continue;
                if (!StoryObjectiveResolver.IsObjectiveThread(b, spine)) continue;
                if (choiceTargets.Contains(b.beatId)) continue;                       // 고르는 순간 뜬다 — 목표가 아니다
                if (b.trigger != null && b.trigger.type == StoryDirector.TriggerImmediate) continue;   // 목표 행에 머무르지 않는다
                required++;
                if (string.IsNullOrWhiteSpace(b.why)) missing.Add(b.beatId);
            }

            Assert.Greater(required, 0, "이유가 필요한 비트를 하나도 못 찾았다 — 기준이 무너졌다");
            Assert.IsEmpty(missing, $"HUD 목표로 먼저 뽑히는 본편 비트 {missing.Count}/{required}개에 이유(why)가 없다: "
                + string.Join(", ", missing));
        }

        [Test]
        public void RealStory_EveryWrittenWhy_FitsOneHudLine()
        {
            var tooLong = new List<string>();
            foreach (StoryBeat b in StoryService.AllBeats())
            {
                if (b == null || string.IsNullOrEmpty(b.why)) continue;
                if (b.why.Length > MaxWhyChars) tooLong.Add($"{b.beatId}={b.why.Length}자");
            }
            Assert.IsEmpty(tooLong, $"이유는 {MaxWhyChars}자 이하(HUD 한 줄): " + string.Join(", ", tooLong));
        }

        // 본편 — ch1..ch12와 종장. ChapterRank가 본편에만 1~12·1000을 준다.
        private static bool IsMainChapter(string chapterId)
        {
            if (chapterId == StoryObjectiveResolver.FinaleChapterId) return true;
            return !string.IsNullOrEmpty(chapterId) && chapterId.StartsWith("ch")
                && int.TryParse(chapterId.Substring(2), out _);
        }
    }
}
#endif
