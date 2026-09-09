#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Story;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 스토리 영상 저작의 순수 검사. 재생·복귀는 VideoPlayer와 MonoBehaviour 수명에 달려 있어
    /// 기기 확인 대상이고(배치모드엔 디코더가 없다), 여기서는 <b>저작이 프리즈 상한 안에 있고
    /// 자막 큐가 서로 겹치거나 영상 밖으로 나가지 않는지</b>를 고정한다.
    /// </summary>
    [TestFixture]
    public class StoryVideoLibraryTests
    {
        /// <summary>저작된 영상 전부를 리플렉션으로 센다 — 손 목록은 새 항목을 조용히 빠뜨린다(<c>CutsceneTimelineTests</c>와 같은 이유).</summary>
        private static readonly string[] AllVideos = CollectVideoIds();

        private static string[] CollectVideoIds()
        {
            var ids = new List<string>();
            foreach (FieldInfo f in typeof(StoryVideoLibrary).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!f.IsLiteral || f.IsInitOnly) continue;
                if (f.FieldType != typeof(string)) continue;
                ids.Add((string)f.GetRawConstantValue());
            }
            ids.Sort(System.StringComparer.Ordinal);
            return ids.ToArray();
        }

        // ── 순수 계산 ──

        private static StoryVideoCue[] ThreeCues()
        {
            return new[]
            {
                new StoryVideoCue(1f, 2f, "첫 줄"),
                new StoryVideoCue(4f, 1f, "둘째 줄"),
                new StoryVideoCue(6f, 2f, "셋째 줄"),
            };
        }

        [TestCase(0f, -1)]
        [TestCase(1f, 0)]
        [TestCase(2.99f, 0)]
        [TestCase(3f, -1)]
        [TestCase(4.5f, 1)]
        [TestCase(7.99f, 2)]
        [TestCase(8f, -1)]
        public void TryGetCue_MapsElapsedToCue(float elapsed, int expected)
        {
            bool found = StoryVideoTimeline.TryGetCue(ThreeCues(), elapsed, out int index);
            Assert.AreEqual(expected >= 0, found);
            Assert.AreEqual(expected, index);
        }

        [Test]
        public void TryGetCue_NullOrEmpty_IsFalse()
        {
            Assert.IsFalse(StoryVideoTimeline.TryGetCue(null, 1f, out _));
            Assert.IsFalse(StoryVideoTimeline.TryGetCue(new StoryVideoCue[0], 1f, out _));
        }

        [Test]
        public void CueProgress_IsClampedZeroToOne()
        {
            var cue = new StoryVideoCue(2f, 4f, "x");
            Assert.AreEqual(0f, StoryVideoTimeline.CueProgress(cue, 0f), 0.001f);
            Assert.AreEqual(0.5f, StoryVideoTimeline.CueProgress(cue, 4f), 0.001f);
            Assert.AreEqual(1f, StoryVideoTimeline.CueProgress(cue, 9f), 0.001f);
        }

        // ── 저작된 영상 ──

        [Test]
        public void Library_IdCollection_IsNotEmpty()
        {
            // 리플렉션이 빈 배열을 내면 아래 검사가 전부 "통과"한다 — 0건 통과는 검사기 고장이다.
            Assert.GreaterOrEqual(AllVideos.Length, 5, "StoryVideoLibrary의 const 문자열을 못 읽었다");
            CollectionAssert.AllItemsAreUnique(AllVideos);
            foreach (string id in AllVideos)
                Assert.IsTrue(StoryVideoLibrary.TryGet(id, out _), $"{id}가 switch에 없다");
        }

        [Test]
        public void Library_UnknownId_IsFalse()
        {
            Assert.IsFalse(StoryVideoLibrary.TryGet("vid_nope", out StoryVideoDefinition def));
            Assert.IsNull(def);
        }

        [TestCaseSource(nameof(AllVideos))]
        public void Library_Definitions_AreWellFormed(string videoId)
        {
            Assert.IsTrue(StoryVideoLibrary.TryGet(videoId, out StoryVideoDefinition def), videoId);
            Assert.AreEqual(videoId, def.videoId, "정의의 videoId가 상수와 다르다");
            StringAssert.StartsWith("vid_", videoId);
            StringAssert.EndsWith(".mp4", def.fileName);
            Assert.IsFalse(def.fileName.Contains("/") || def.fileName.Contains("\\"),
                "파일명에 경로를 넣지 않는다 — 폴더는 StoryVideoDirector가 붙인다");
            // 파일명 = videoId에서 vid_ 제거. 둘이 어긋나면 lint 보고와 폴더가 따로 논다.
            Assert.AreEqual(videoId.Substring(4) + ".mp4", def.fileName);
            Assert.IsNotNull(def.cues);
        }

        [TestCaseSource(nameof(AllVideos))]
        public void Library_Definitions_FinishBeforeAutoUnfreeze(string videoId)
        {
            // **가장 중요한 검사.** PlayerMovement는 frozen이 걸린 뒤 AutoUnfreezeTime이 지나면
            // 스스로 푼다. 프리즈 타이머는 실제 재생 시작(OnPrepared)에 다시 감기므로, 최악 프리즈는
            // 영상 길이 + 종료 이벤트가 안 올 때의 오버런 여유(StoryVideoDirector.OverrunGraceSeconds=3)다.
            // 그 합이 상한보다 작아야 검은 화면 뒤에서 캐릭터가 걷는 일이 없다.
            const float overrunGrace = 3f;
            const float safetyMargin = 1f;
            Assert.IsTrue(StoryVideoLibrary.TryGet(videoId, out StoryVideoDefinition def));
            Assert.Greater(def.expectedDuration, 3f, $"{videoId}가 너무 짧아 연출로 읽히지 않는다");
            Assert.LessOrEqual(def.expectedDuration + overrunGrace,
                GameConstants.Player.AutoUnfreezeTime - safetyMargin,
                $"{videoId}가 {def.expectedDuration:F1}s로 자동 프리즈 해제({GameConstants.Player.AutoUnfreezeTime}s)에 너무 가깝다");
        }

        [TestCaseSource(nameof(AllVideos))]
        public void Library_Cues_AreOrderedAndInsideVideo(string videoId)
        {
            Assert.IsTrue(StoryVideoLibrary.TryGet(videoId, out StoryVideoDefinition def));
            float lastEnd = 0f;
            for (int i = 0; i < def.cues.Length; i++)
            {
                StoryVideoCue cue = def.cues[i];
                Assert.IsFalse(string.IsNullOrWhiteSpace(cue.text), $"{videoId} 큐 {i}의 문구가 비었다");
                Assert.Greater(cue.duration, 1f, $"{videoId} 큐 {i}가 읽기엔 너무 짧다");
                Assert.GreaterOrEqual(cue.at, lastEnd, $"{videoId} 큐 {i}가 앞 큐와 겹친다");
                Assert.LessOrEqual(cue.End, def.expectedDuration, $"{videoId} 큐 {i}가 영상 밖으로 나간다");
                lastEnd = cue.End;
            }
        }

        [TestCaseSource(nameof(AllVideos))]
        public void Library_Cues_NeverNameTheNameless(string videoId)
        {
            // 이름을 부르지 않는 것이 이 이야기의 규칙이자 승리 조건이다(StoryBible 2장).
            // 대사가 지키는 금칙을 자막이 깨면 결말이 무너진다.
            Assert.IsTrue(StoryVideoLibrary.TryGet(videoId, out StoryVideoDefinition def));
            foreach (StoryVideoCue cue in def.cues)
                StringAssert.DoesNotContain("무명", cue.text, $"{videoId} 자막이 그것의 이름을 부른다");
        }
    }
}
#endif
