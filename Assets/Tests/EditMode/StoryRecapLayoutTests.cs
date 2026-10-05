#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using InsectGame.Story;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 이야기를 따라가기 쉽게 하는 화면 장치 셋의 배치 — 「지난 이야기」 카드(<see cref="StoryRecapLayout"/>),
    /// 저널 장 머리(<see cref="StoryJournalHeadLayout"/>), HUD 목표 행의 「왜」 둘째 줄(<see cref="QuestChipLayout"/>).
    /// 전부 순수 계산이라 화면을 띄우지 않고 가로·세로·배율별로 잰다. 그림은 QA 빌드 <c>-battleScenario story</c>의 recap-* 컷.
    /// </summary>
    [TestFixture]
    public class StoryRecapLayoutTests
    {
        private static IEnumerable<TestCaseData> Screens()
        {
            yield return new TestCaseData("desktop 1280x720", 1280f, 720f, 0f, 0f, 0f, 0f, false);
            yield return new TestCaseData("desktop 1920x1080", 1920f, 1080f, 0f, 0f, 0f, 0f, false);
            yield return new TestCaseData("desktop 2560x1080", 2560f, 1080f, 0f, 0f, 0f, 0f, false);
            foreach (float s in new[] { 2f / 3f, 1f, 4f / 3f })
            {
                string k = s.ToString("0.###");
                yield return new TestCaseData($"portrait 720x1280 s{k}", 720f * s, 1280f * s, 0f, 0f, 0f, 0f, true);
                yield return new TestCaseData($"portrait 20x9 notch s{k}", 1080f * s, 2400f * s, 0f, 0f, 100f * s, 40f * s, true);
                yield return new TestCaseData($"landscape 20x9 notch s{k}", 2400f * s, 1080f * s, 100f * s, 100f * s, 0f, 30f * s, true);
            }
        }

        private static HudFrame Frame(float pw, float ph, float l, float r, float t, float b, bool mobile)
            => HudFrame.ForScreen(pw, ph, l, r, t, b, mobile);

        // ── 카드를 띄우는가 ──

        [Test]
        public void ShouldShow_OnlyForFirstShowing_WithARecapLine()
        {
            var ch = new StoryChapter { chapterId = "ch8", openingBeatId = "ch8_arrive", recap = new List<string> { "지난 줄" }, goal = "목표" };
            Assert.IsTrue(StoryRecapLayout.ShouldShow(ch, false));
            Assert.IsFalse(StoryRecapLayout.ShouldShow(ch, true), "저널 다시보기에는 카드가 없다");
            Assert.IsFalse(StoryRecapLayout.ShouldShow(null, false));
            Assert.IsFalse(StoryRecapLayout.ShouldShow(new StoryChapter { recap = new List<string>() }, false), "1장처럼 지난 이야기가 없으면 카드 없음");
            Assert.IsFalse(StoryRecapLayout.ShouldShow(new StoryChapter { recap = new List<string> { "", "  " } }, false), "빈 줄만이면 카드 없음");
            Assert.IsFalse(StoryRecapLayout.ShouldShow(new StoryChapter { recap = null }, false));
        }

        [Test]
        public void BodyFont_IsAtLeastTheDialogueBody()
        {
            // 대사 본문은 가로 34·세로 36(NpcDialogueUI.DrawStoryStage) — 아이가 읽는 카드는 그보다 작지 않다.
            Assert.GreaterOrEqual(StoryRecapLayout.BodyFont, 36);
            Assert.Greater(StoryRecapLayout.TitleFont, StoryRecapLayout.BodyFont);
        }

        // ── 대사 상자와 카드 판 ──

        [Test]
        public void DialogueBox_KeepsTheStageSize()
        {
            // 무대(DrawStoryStage)가 쓰던 값 그대로 — 가로 min(1400, 안전 폭)×350, 세로 안전 폭×500, 안전 영역 바닥.
            HudFrame land = Frame(1920f, 1080f, 0f, 0f, 0f, 0f, false);
            Rect box = StoryRecapLayout.DialogueBox(land);
            Assert.AreEqual(1400f, box.width, 0.01f);
            Assert.AreEqual(350f, box.height, 0.01f);
            Assert.AreEqual(land.ContentBottom, box.yMax, 0.01f);
            Assert.AreEqual(land.Width * 0.5f, box.center.x, 0.01f);

            HudFrame port = Frame(1080f, 1920f, 0f, 0f, 0f, 0f, true);
            Rect pbox = StoryRecapLayout.DialogueBox(port);
            Assert.AreEqual(port.ContentWidth, pbox.width, 0.01f);
            Assert.AreEqual(500f, pbox.height, 0.01f);
            Assert.AreEqual(port.ContentBottom, pbox.yMax, 0.01f);
        }

        [TestCaseSource(nameof(Screens))]
        public void Area_StandsAboveTheDialogueBox_InsideTheSafeArea(string name, float pw, float ph, float l, float r, float t, float b, bool mobile)
        {
            HudFrame f = Frame(pw, ph, l, r, t, b, mobile);
            Rect area = StoryRecapLayout.Area(f);
            Rect box = StoryRecapLayout.DialogueBox(f);
            Assert.LessOrEqual(area.yMax, box.y - UITheme.Space.L + 0.01f, name + ": 판이 대사 상자 위에서 끝난다");
            Assert.GreaterOrEqual(area.y, f.ContentTop - 0.01f, name);
            Assert.GreaterOrEqual(area.x, f.ContentLeft - 0.01f, name);
            Assert.LessOrEqual(area.xMax, f.ContentRight + 0.01f, name);
            // 지난 이야기 3줄(세로에선 두 줄씩 접힌다) + 두 줄 목표가 줄이지 않고 들어갈 만큼 높다.
            float lines = f.Portrait ? 2f : 1f;
            float need = StoryRecapLayout.FixedHeight(3, true)
                         + (3f + 1f) * lines * StoryRecapLayout.LineHeight(StoryRecapLayout.BodyFont);
            Assert.GreaterOrEqual(area.height, need, $"{name}: 판 {area} 높이가 보통 카드({need})보다 낮다");
        }

        // ── 카드 배치 ──

        [TestCaseSource(nameof(Screens))]
        public void Layout_TypicalChapter_FitsUnsqueezed_InOrder(string name, float pw, float ph, float l, float r, float t, float b, bool mobile)
        {
            HudFrame f = Frame(pw, ph, l, r, t, b, mobile);
            Rect area = StoryRecapLayout.Area(f);
            float body = StoryRecapLayout.LineHeight(StoryRecapLayout.BodyFont);
            float lines = f.Portrait ? 2f : 1f;   // 세로 화면은 30자 한 줄이 두 줄로 접힌다
            var recap = new[] { body * lines, body * lines, body * lines };
            StoryRecapLayout.Plan p = StoryRecapLayout.Layout(area, StoryRecapLayout.CardWidth(f), recap, body * lines);

            Assert.IsFalse(p.Squeezed, name + ": 보통 장은 줄이지 않는다");
            AssertInside(area, p.Card, name + " 카드");
            Assert.AreEqual(Mathf.Min(StoryRecapLayout.CardWidth(f), area.width), p.Card.width, 0.01f);
            Assert.AreEqual(area.center.x, p.Card.center.x, 0.01f, name + ": 가로 가운데");
            AssertStacked(name, p);
            for (int i = 0; i < recap.Length; i++) Assert.AreEqual(recap[i], p.Recap[i].height, 0.01f, "잰 높이 그대로");
        }

        [TestCaseSource(nameof(Screens))]
        public void Layout_ManyLongLines_SqueezesBodyAndStaysInsideTheArea(string name, float pw, float ph, float l, float r, float t, float b, bool mobile)
        {
            HudFrame f = Frame(pw, ph, l, r, t, b, mobile);
            Rect area = StoryRecapLayout.Area(f);
            float body = StoryRecapLayout.LineHeight(StoryRecapLayout.BodyFont);
            float[] recap = Enumerable.Repeat(body * 3f, 10).ToArray();   // 10줄 × 세 줄씩 접힘
            StoryRecapLayout.Plan p = StoryRecapLayout.Layout(area, StoryRecapLayout.CardWidth(f), recap, body * 3f);

            Assert.IsTrue(p.Squeezed, name);
            AssertInside(area, p.Card, name + " 카드");
            AssertStacked(name, p);
            Assert.LessOrEqual(p.Goal.yMax, p.Card.yMax - StoryRecapLayout.PadY + 0.5f, name + ": 목표가 카드 안에서 끝난다");
        }

        [Test]
        public void Layout_NoGoal_HasNoDividerOrGoalSlot()
        {
            var area = new Rect(0f, 0f, 1200f, 640f);
            StoryRecapLayout.Plan p = StoryRecapLayout.Layout(area, 1200f, new[] { 49f }, 0f);
            Assert.IsFalse(p.HasGoal);
            Assert.AreEqual(0f, p.Goal.height);
            Assert.AreEqual(StoryRecapLayout.FixedHeight(1, false) + 49f, p.Card.height, 0.01f);
        }

        // ── 저널 장 머리 ──

        [Test]
        public void JournalHead_StaysInTheTopHalf_AndTheListStartsBelowIt()
        {
            var area = new Rect(300f, 200f, 640f, 800f);
            float body = StoryJournalHeadLayout.LineHeight(StoryJournalHeadLayout.BodyFont);
            StoryJournalHeadLayout.Plan p = StoryJournalHeadLayout.Layout(area, new[] { body, body * 2f, body }, body);
            Assert.IsFalse(p.Squeezed);
            Assert.AreEqual(area.y, p.Block.y, 0.01f);
            Assert.LessOrEqual(p.Block.height, area.height * StoryJournalHeadLayout.MaxShare + 0.01f);
            Assert.GreaterOrEqual(p.List.y, p.Block.yMax, "목록은 블록 아래에서 시작한다");
            Assert.AreEqual(area.yMax, p.List.yMax, 0.01f);
            AssertInside(p.Block, p.Recap[0], "첫 줄");
            AssertInside(p.Block, p.Goal, "목표");
            Assert.LessOrEqual(p.Recap[2].yMax, p.Divider.y);
            Assert.LessOrEqual(p.Divider.yMax, p.Goal.y);

            // 줄이 아주 많아도 블록은 영역 절반을 넘지 않는다(목록이 늘 보인다).
            StoryJournalHeadLayout.Plan big = StoryJournalHeadLayout.Layout(area, Enumerable.Repeat(body * 3f, 12).ToArray(), body * 3f);
            Assert.IsTrue(big.Squeezed);
            Assert.LessOrEqual(big.Block.height, area.height * StoryJournalHeadLayout.MaxShare + 0.5f);
            AssertInside(big.Block, big.Goal, "넘친 목표");

            // 내용이 없으면 블록도 없고 목록이 영역을 다 쓴다.
            StoryJournalHeadLayout.Plan none = StoryJournalHeadLayout.Layout(area, new float[0], 0f);
            Assert.AreEqual(area, none.List);
            Assert.AreEqual(0f, none.Block.height);
        }

        [Test]
        public void JournalHead_HasContent_RecapOrGoal()
        {
            Assert.IsFalse(StoryJournalHeadLayout.HasContent(null));
            Assert.IsFalse(StoryJournalHeadLayout.HasContent(new StoryChapter()));
            Assert.IsTrue(StoryJournalHeadLayout.HasContent(new StoryChapter { goal = "목표만" }), "1장은 지난 이야기 없이 목표만 있다");
            Assert.IsTrue(StoryJournalHeadLayout.HasContent(new StoryChapter { recap = new List<string> { "줄" } }));
            Assert.IsFalse(StoryJournalHeadLayout.HasContent(new StoryChapter { recap = new List<string> { " " }, goal = "" }));
        }

        // ── HUD 목표 행의 「왜」 ──

        [Test]
        public void ObjectiveRow_WhyAddsASecondLine_OnlyWhenPresent()
        {
            Assert.AreEqual(QuestChipLayout.RowHeight, QuestChipLayout.RowHeightFor(false), "이유가 없으면 높이 그대로");
            Assert.AreEqual(QuestChipLayout.RowHeightWithWhy, QuestChipLayout.RowHeightFor(true));
            Assert.Less(QuestChipLayout.WhyFontSize, QuestChipLayout.ObjectiveFontSize, "이유는 할 일보다 작다");

            var row = new Rect(40f, 500f, 500f, QuestChipLayout.RowHeightWithWhy);
            Rect main = QuestChipLayout.RowMainLine(row, true);
            Rect why = QuestChipLayout.RowWhyLine(row);
            AssertInside(row, main, "할 일 줄");
            AssertInside(row, why, "이유 줄");
            Assert.LessOrEqual(main.yMax, why.y + 0.01f, "이유는 할 일 아래");
            Assert.GreaterOrEqual(main.height, Mathf.Ceil(QuestChipLayout.ObjectiveFontSize * 1.35f));
            Assert.GreaterOrEqual(why.height, Mathf.Ceil(QuestChipLayout.WhyFontSize * 1.35f));
            Assert.GreaterOrEqual(why.x, main.x, "이유는 글리프 폭만큼 들여 쓴다");

            // 이유가 없으면 할 일이 행 전체(예전과 같다).
            var plain = new Rect(40f, 500f, 500f, QuestChipLayout.RowHeight);
            Rect only = QuestChipLayout.RowMainLine(plain, false);
            Assert.AreEqual(plain.y, only.y);
            Assert.AreEqual(plain.height, only.height);
        }

        [TestCaseSource(nameof(Screens))]
        public void ObjectiveRowWithWhy_StaysInsideTheSafeArea_AndTheStageClearsIt(string name, float pw, float ph, float l, float r, float t, float b, bool mobile)
        {
            HudFrame f = Frame(pw, ph, l, r, t, b, mobile);
            float h = QuestChipLayout.RowHeightWithWhy;
            Rect chip = QuestChipLayout.ChipRect(f, QuestChipLayout.ExpandedHeight, h);
            Rect row = QuestChipLayout.Row(chip, QuestChipLayout.StackWidth(f), h);
            Assert.GreaterOrEqual(row.y, f.ContentTop - 0.5f, name);
            Assert.LessOrEqual(row.yMax, f.ContentBottom + 0.5f, $"{name}: 두 줄 목표 행 {row}이(가) 안전 영역 아래로 나간다");
            Assert.LessOrEqual(chip.yMax, row.y, name);
            // 가운데 무대(잠깐 뜨는 카드가 서는 자리)는 두 줄 행과 겹치지 않는다 — 필드와 섬 둘 다.
            foreach (bool island in new[] { false, true })
                Assert.IsFalse(HudStage.Area(f, island).Overlaps(row), $"{name}{(island ? " 섬" : "")}: 무대 {HudStage.Area(f, island)} ↔ 목표 행 {row}");
        }

        // ── 잡담 띠 ──

        [Test]
        public void NextStoryLine_AppendsWhy_OnlyWhenPresent()
        {
            Assert.AreEqual("다음 이야기 · 모래언덕으로", NpcDialogueUI.ComposeNextStory("모래언덕으로", null));
            Assert.AreEqual("다음 이야기 · 모래언덕으로", NpcDialogueUI.ComposeNextStory("모래언덕으로", ""));
            Assert.AreEqual("다음 이야기 · 모래언덕으로 — 상자에 갇힌 곤충이 있대",
                NpcDialogueUI.ComposeNextStory("모래언덕으로", "상자에 갇힌 곤충이 있대"));
        }

        // ── 실제 대사창 — 카드는 처음 뜰 때만 ──

        [Test]
        public void RealOpeningBeat_ShowsTheCardWhenFired_ButNotOnJournalReplay()
        {
            StoryChapter chapter = StoryService.AllChapters().FirstOrDefault(c =>
                StoryRecapLayout.ShouldShow(c, false)
                && StoryService.TryGetBeat(c.openingBeatId, out StoryBeat b) && b.lines != null && b.lines.Count > 0);
            if (chapter == null) Assert.Ignore("Story.json에 지난 이야기가 있는 장이 아직 없다");
            StoryService.TryGetBeat(chapter.openingBeatId, out StoryBeat beat);

            var root = new GameObject("Recap card test");
            try
            {
                // StoryDirector를 주입하지 않는다 — 닫아도 완료 처리(보상·열람 기록)가 없다.
                var ui = root.AddComponent<NpcDialogueUI>();
                ui.ShowStoryReplay(beat);
                Assert.IsTrue(ui.IsOpen);
                Assert.IsFalse(ui.IsShowingRecap, "다시보기에는 카드가 없다");
                ui.CloseModal();

                ui.ShowStory(beat);
                Assert.IsTrue(ui.IsShowingRecap, $"{chapter.chapterId}를 여는 {beat.beatId}가 처음 뜨면 카드가 먼저다");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ── 도우미 ──

        private static void AssertStacked(string name, StoryRecapLayout.Plan p)
        {
            AssertInside(p.Card, p.Title, name + " 제목");
            float y = p.Title.yMax;
            Assert.LessOrEqual(y, p.RecapHeader.y + 0.01f, name + ": 소제목은 제목 아래");
            y = p.RecapHeader.yMax;
            foreach (Rect line in p.Recap)
            {
                Assert.LessOrEqual(y, line.y + 0.01f, name + ": 줄이 겹치지 않고 아래로");
                y = line.yMax;
            }
            if (!p.HasGoal) return;
            Assert.LessOrEqual(y, p.Divider.y + 0.01f, name + ": 구분선은 마지막 줄 아래");
            Assert.LessOrEqual(p.Divider.yMax, p.GoalHeader.y + 0.01f);
            Assert.LessOrEqual(p.GoalHeader.yMax, p.Goal.y + 0.01f);
            AssertInside(p.Card, p.Goal, name + " 목표");
        }

        private static void AssertInside(Rect outer, Rect inner, string what)
        {
            Assert.IsTrue(inner.xMin >= outer.xMin - 0.5f && inner.yMin >= outer.yMin - 0.5f
                          && inner.xMax <= outer.xMax + 0.5f && inner.yMax <= outer.yMax + 0.5f,
                $"{what} {inner}이(가) {outer} 밖으로 나간다");
        }
    }
}
#endif
