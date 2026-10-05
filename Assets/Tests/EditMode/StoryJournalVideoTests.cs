#if UNITY_EDITOR
using InsectGame.Story;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 저널 영상 다시보기 — 행 버튼 자리(<see cref="StoryJournalRowLayout"/>)와 다시 볼 영상 고르기(<see cref="StoryJournalVideo"/>).
    /// 화면은 QA 빌드 <c>-battleScenario story-video</c>의 <c>00-journal-*</c> 컷(ESC가 영상만 닫는지도 거기서 잰다).
    /// </summary>
    [TestFixture]
    public class StoryJournalVideoTests
    {
        // 목록 영역 폭 — 데스크톱(패널 960 − 탭 260 − 여백), 모바일(탭 220), 좁게 줄어든 패널.
        [TestCase(644f, 92f)]
        [TestCase(684f, 104f)]
        [TestCase(480f, 92f)]
        public void RowButtons_InsideRow_NoOverlap_TouchHeight(float width, float height)
        {
            var row = new Rect(0f, 300f, width, height);
            Rect replay = StoryJournalRowLayout.ReplayButton(row);
            Rect video = StoryJournalRowLayout.VideoButton(row);

            foreach (Rect b in new[] { replay, video })
            {
                Assert.GreaterOrEqual(b.height, UIScale.MinTouchHeight, "터치 높이 하한");
                Assert.GreaterOrEqual(b.xMin, row.xMin);
                Assert.LessOrEqual(b.xMax, row.xMax);
                Assert.GreaterOrEqual(b.yMin, row.yMin, "버튼이 행 위로 넘친다");
                Assert.LessOrEqual(b.yMax, row.yMax, "버튼이 행 아래로 넘친다");
            }
            Assert.IsFalse(replay.Overlaps(video), "「▶ 영상」과 「다시 읽기」가 겹친다");
            Assert.Less(video.xMax, replay.xMin, "「▶ 영상」은 「다시 읽기」 왼쪽");
            Assert.AreEqual(replay.y, video.y, 0.01f, "두 버튼이 같은 줄");
        }

        [TestCase(644f, 92f)]
        [TestCase(684f, 104f)]
        [TestCase(480f, 92f)]
        public void RowText_StopsBeforeTheLeftmostButton(float width, float height)
        {
            var row = new Rect(20f, 0f, width, height);
            float textX = row.x + StoryJournalRowLayout.TextLeft;

            float withVideo = StoryJournalRowLayout.TextWidth(row, true);
            Assert.LessOrEqual(textX + withVideo, StoryJournalRowLayout.VideoButton(row).xMin, "글자가 「▶ 영상」 밑으로 들어간다");

            float withoutVideo = StoryJournalRowLayout.TextWidth(row, false);
            Assert.LessOrEqual(textX + withoutVideo, StoryJournalRowLayout.ReplayButton(row).xMin, "글자가 「다시 읽기」 밑으로 들어간다");
            Assert.Greater(withoutVideo, withVideo, "영상이 없는 행은 글자 폭이 더 넓다");
        }

        [Test]
        public void ReplayButton_StaysInTheSameColumn_WithOrWithoutVideo()
        {
            // 「다시 읽기」 자리는 영상 유무를 묻지 않는다 — 행마다 버튼 열이 흔들리지 않는다.
            var a = new Rect(0f, 0f, 644f, 92f);
            var b = new Rect(0f, 100f, 644f, 92f);
            Assert.AreEqual(StoryJournalRowLayout.ReplayButton(a).x, StoryJournalRowLayout.ReplayButton(b).x, 0.01f);
            Assert.AreEqual(StoryJournalRowLayout.ReplayButton(a).width, StoryJournalRowLayout.ReplayWidth, 0.01f);
        }

        // ── 다시 볼 영상 ──

        private static bool Known(string id) => id == "vid_a" || id == "vid_b";

        [Test]
        public void Videos_IntroThenOutro_WhenTheBeatHasBoth()
        {
            var beat = new StoryBeat { beatId = "x", introVideoId = "vid_a", videoId = "vid_b" };
            StoryJournalVideo.Pair p = StoryJournalVideo.For(beat, Known);
            Assert.IsTrue(p.HasAny);
            Assert.AreEqual("vid_a", p.First, "대사 앞 영상이 먼저 — 이야기 순서");
            Assert.AreEqual("vid_b", p.Then);
        }

        [Test]
        public void Videos_OneVideo_PlaysAlone()
        {
            StoryJournalVideo.Pair intro = StoryJournalVideo.For(new StoryBeat { beatId = "i", introVideoId = "vid_a" }, Known);
            Assert.AreEqual("vid_a", intro.First);
            Assert.IsNull(intro.Then);

            StoryJournalVideo.Pair outro = StoryJournalVideo.For(new StoryBeat { beatId = "o", videoId = "vid_b" }, Known);
            Assert.AreEqual("vid_b", outro.First);
            Assert.IsNull(outro.Then);

            StoryJournalVideo.Pair same = StoryJournalVideo.For(new StoryBeat { beatId = "s", introVideoId = "vid_a", videoId = "vid_a" }, Known);
            Assert.AreEqual("vid_a", same.First);
            Assert.IsNull(same.Then, "같은 영상을 두 번 틀지 않는다");
        }

        [Test]
        public void Videos_NoneOrUnknown_MeansNoButton()
        {
            Assert.IsFalse(StoryJournalVideo.For(null, Known).HasAny);
            Assert.IsFalse(StoryJournalVideo.For(new StoryBeat { beatId = "n" }, Known).HasAny);
            Assert.IsFalse(StoryJournalVideo.For(new StoryBeat { beatId = "w", videoId = "  " }, Known).HasAny);
            Assert.IsFalse(StoryJournalVideo.For(new StoryBeat { beatId = "u", videoId = "vid_typo" }, Known).HasAny,
                "라이브러리가 모르는 ID에는 눌러도 안 나오는 버튼을 세우지 않는다");

            // 모르는 앞 영상은 빼고 아는 뒤 영상만.
            StoryJournalVideo.Pair p = StoryJournalVideo.For(new StoryBeat { beatId = "m", introVideoId = "vid_typo", videoId = "vid_b" }, Known);
            Assert.AreEqual("vid_b", p.First);
            Assert.IsNull(p.Then);
        }
    }
}
#endif
