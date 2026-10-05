#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 스토리 영상 화면의 자막 띠·「건너뛰기」 자리(<see cref="StoryVideoScreenLayout"/>) — 가로·세로·노치·배율별.
    /// 그림은 QA 빌드 <c>-battleScenario story-video</c>(가로 1280×720·세로 720×1280).
    /// </summary>
    [TestFixture]
    public class StoryVideoScreenLayoutTests
    {
        private static IEnumerable<TestCaseData> Screens()
        {
            yield return new TestCaseData("desktop 1280x720", 1280f, 720f, 0f, 0f, 0f, 0f, false);
            yield return new TestCaseData("desktop 1920x1080", 1920f, 1080f, 0f, 0f, 0f, 0f, false);
            yield return new TestCaseData("desktop 2560x1080", 2560f, 1080f, 0f, 0f, 0f, 0f, false);
            yield return new TestCaseData("tablet 4x3", 1600f, 1200f, 0f, 0f, 0f, 0f, true);
            foreach (float s in new[] { 2f / 3f, 1f, 4f / 3f })
            {
                string k = s.ToString("0.###");
                yield return new TestCaseData($"portrait 720x1280 s{k}", 720f * s, 1280f * s, 0f, 0f, 0f, 0f, true);
                yield return new TestCaseData($"portrait 20x9 notch s{k}", 1080f * s, 2400f * s, 0f, 0f, 100f * s, 40f * s, true);
                yield return new TestCaseData($"landscape 20x9 notch s{k}", 2400f * s, 1080f * s, 100f * s, 100f * s, 0f, 30f * s, true);
                // 노치 인셋이 커서 가로 띠가 「건너뛰기」 열까지 닿는 화면 — 띠가 버튼 위로 올라가야 한다.
                yield return new TestCaseData($"landscape 16x9 wide inset s{k}", 1920f * s, 1080f * s, 160f * s, 160f * s, 0f, 30f * s, true);
            }
        }

        [TestCaseSource(nameof(Screens))]
        public void SubtitleBand_And_Skip_StayInside_AndNeverOverlap(string name, float pw, float ph,
            float l, float r, float t, float b, bool mobile)
        {
            HudFrame f = HudFrame.ForScreen(pw, ph, l, r, t, b, mobile);
            Rect band = StoryVideoScreenLayout.SubtitleBand(f);
            Rect skip = StoryVideoScreenLayout.Skip(f);

            foreach (Rect rect in new[] { band, skip })
            {
                Assert.GreaterOrEqual(rect.xMin, f.SafeLeft - 0.01f, $"{name}: 왼쪽 세이프 에어리어 밖");
                Assert.LessOrEqual(rect.xMax, f.Width - f.SafeRight + 0.01f, $"{name}: 오른쪽 세이프 에어리어 밖");
                Assert.GreaterOrEqual(rect.yMin, f.ContentTop - 0.01f, $"{name}: 위 콘텐츠 영역 밖");
                Assert.LessOrEqual(rect.yMax, f.ContentBottom + 0.01f, $"{name}: 아래 콘텐츠 영역 밖");
            }
            Assert.IsFalse(band.Overlaps(skip), $"{name}: 자막 띠 {band}와 「건너뛰기」 {skip}가 겹친다");
            Assert.GreaterOrEqual(skip.height, UIScale.MinTouchHeight, $"{name}: 터치 높이 하한");
        }

        [TestCaseSource(nameof(Screens))]
        public void Skip_IsTopRightInPortrait_BottomRightInLandscape(string name, float pw, float ph,
            float l, float r, float t, float b, bool mobile)
        {
            HudFrame f = HudFrame.ForScreen(pw, ph, l, r, t, b, mobile);
            Rect skip = StoryVideoScreenLayout.Skip(f);
            Assert.Greater(skip.center.x, f.Width * 0.5f, $"{name}: 오른쪽");
            if (f.Portrait) Assert.Less(skip.center.y, f.Height * 0.5f, $"{name}: 세로는 위 — 아래는 자막 띠 자리");
            else Assert.Greater(skip.center.y, f.Height * 0.5f, $"{name}: 가로는 아래");
            // 자막 띠는 늘 아래 절반.
            Assert.Greater(StoryVideoScreenLayout.SubtitleBand(f).center.y, f.Height * 0.5f, $"{name}: 자막은 아래");
        }

        [TestCaseSource(nameof(Screens))]
        public void SubtitleText_FitsTwoLines_OfTheLongestCue(string name, float pw, float ph,
            float l, float r, float t, float b, bool mobile)
        {
            HudFrame f = HudFrame.ForScreen(pw, ph, l, r, t, b, mobile);
            Rect text = StoryVideoScreenLayout.SubtitleText(StoryVideoScreenLayout.SubtitleBand(f));
            float line = StoryVideoScreenLayout.LineHeight(StoryVideoScreenLayout.SubtitleFontSize);
            Assert.GreaterOrEqual(text.height, line * StoryVideoScreenLayout.SubtitleLines, $"{name}: 두 줄이 안 들어간다");
            // 가장 긴 자막(24자)이 두 줄 안에 줄이지 않고 든다 — 한 줄에 12자(한글 한 자 ≈ 글자 크기).
            Assert.GreaterOrEqual(text.width, 12f * StoryVideoScreenLayout.SubtitleFontSize, $"{name}: 한 줄이 너무 좁다");
        }

        [Test]
        public void Readability_ForKids()
        {
            Assert.That(StoryVideoScreenLayout.SubtitleFontSize, Is.InRange(34, 36), "아이가 읽는 자막 크기");
            Assert.That(StoryVideoScreenLayout.SubtitleBandAlpha, Is.InRange(0.65f, 0.7f), "밝은 그림책 장면 위의 띠 농도");
            Assert.LessOrEqual(StoryVideoScreenLayout.LineHeight(StoryVideoScreenLayout.SkipFontSize),
                StoryVideoScreenLayout.SkipHeight, "「건너뛰기」 글자가 버튼 높이 안");
        }
    }
}
#endif
