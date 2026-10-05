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

        /// <summary>
        /// 맞닿은 큐(앞 큐의 끝 = 뒤 큐의 시작)는 저작 의도다(예: 9.8 + 2.4 = 12.2). float로 더하면 12.2000008이 되어
        /// 12.2f보다 커지므로 그만큼은 겹침으로 치지 않는다 — 진짜 겹침(0.2초 등)은 그대로 잡는다.
        /// </summary>
        private const float TouchEpsilon = 1e-3f;

        [TestCaseSource(nameof(AllVideos))]
        public void Library_Cues_AreOrderedAndInsideVideo(string videoId)
        {
            Assert.IsTrue(StoryVideoLibrary.TryGet(videoId, out StoryVideoDefinition def));
            Assert.Greater(def.cues.Length, 0, $"{videoId}에 자막이 하나도 없다");
            float lastEnd = 0f;
            for (int i = 0; i < def.cues.Length; i++)
            {
                StoryVideoCue cue = def.cues[i];
                Assert.IsFalse(string.IsNullOrWhiteSpace(cue.text), $"{videoId} 큐 {i}의 문구가 비었다");
                Assert.Greater(cue.duration, 1f, $"{videoId} 큐 {i}가 읽기엔 너무 짧다");
                Assert.GreaterOrEqual(cue.at, 0f, $"{videoId} 큐 {i}가 영상 앞에서 시작한다");
                Assert.GreaterOrEqual(cue.at, lastEnd - TouchEpsilon, $"{videoId} 큐 {i}가 앞 큐와 겹친다({cue.at:F2} < {lastEnd:F2})");
                Assert.LessOrEqual(cue.End, def.expectedDuration + TouchEpsilon, $"{videoId} 큐 {i}가 영상 밖으로 나간다");
                lastEnd = cue.End;
            }
        }

        // ── 그림책 15편(2026-10-05) — 초등 고학년: 영상이 설명하고 대사는 짧게 ──

        /// <summary>
        /// 자막 한 줄 상한(공백·문장부호 포함 글자 수). 아이가 2~3초 안에 한 번에 읽는 길이 — 사양의 최장이
        /// 「이름을 받으면, 울타리 안에 자리를 얻는다.」(24자)다. 이보다 길면 문장을 둘로 나눌 것.
        /// </summary>
        private const int MaxCueChars = 24;

        /// <summary>대사 앞(introVideoId)에 붙는 네 편 — 새로 그렸다. 빠지면 그 비트가 영상 없이 짧은 대사만 남는다.</summary>
        private static readonly string[] IntroVideos =
        {
            StoryVideoLibrary.Ch6Wall, StoryVideoLibrary.Ch7Fence, StoryVideoLibrary.FinShadow, StoryVideoLibrary.FinReturn,
        };

        [Test]
        public void Library_HasFifteenStoryVideos_IncludingTheFourIntros()
        {
            Assert.AreEqual(15, AllVideos.Length, "그림책 영상은 15편이다(대사 뒤 11 · 대사 앞 4) — 늘리거나 줄였다면 이 수와 사양을 함께 고칠 것");
            foreach (string id in IntroVideos)
                CollectionAssert.Contains(AllVideos, id, $"대사 앞 영상 {id}가 상수로 없다");

            Assert.IsTrue(StoryVideoLibrary.TryGet(StoryVideoLibrary.Ch6Wall, out StoryVideoDefinition wall));
            Assert.AreEqual("ch6_wall.mp4", wall.fileName);
            Assert.AreEqual(15f, wall.expectedDuration, 1e-4f);
            Assert.IsTrue(StoryVideoLibrary.TryGet(StoryVideoLibrary.Ch7Fence, out StoryVideoDefinition fence));
            Assert.AreEqual("ch7_fence.mp4", fence.fileName);
            Assert.AreEqual(14f, fence.expectedDuration, 1e-4f);
            Assert.IsTrue(StoryVideoLibrary.TryGet(StoryVideoLibrary.FinShadow, out StoryVideoDefinition shadow));
            Assert.AreEqual("fin_shadow.mp4", shadow.fileName);
            Assert.AreEqual(14.4f, shadow.expectedDuration, 1e-4f);
            Assert.IsTrue(StoryVideoLibrary.TryGet(StoryVideoLibrary.FinReturn, out StoryVideoDefinition back));
            Assert.AreEqual("fin_return.mp4", back.fileName);
            Assert.AreEqual(14.6f, back.expectedDuration, 1e-4f);
        }

        [TestCaseSource(nameof(AllVideos))]
        public void Library_Definitions_AreAtMostFifteenSeconds(string videoId)
        {
            Assert.IsTrue(StoryVideoLibrary.TryGet(videoId, out StoryVideoDefinition def));
            Assert.LessOrEqual(def.expectedDuration, 15f + 1e-4f, $"{videoId}가 15초를 넘는다 — 아이가 기다리기엔 길다");
        }

        [TestCaseSource(nameof(AllVideos))]
        public void Library_Cues_AreShortEnoughToReadAtOnce(string videoId)
        {
            Assert.IsTrue(StoryVideoLibrary.TryGet(videoId, out StoryVideoDefinition def));
            foreach (StoryVideoCue cue in def.cues)
            {
                Assert.LessOrEqual(cue.text.Length, MaxCueChars, $"{videoId} 자막이 {cue.text.Length}자다: 「{cue.text}」");
                StringAssert.DoesNotContain("\n", cue.text, $"{videoId} 자막은 한 줄이다 — 줄바꿈은 자막 띠 두 줄을 먹는다");
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

        /// <summary>
        /// 쉬운 말로 바꾼 옛 말(story_lint 검사 32의 <c>RETIRED_WORDS</c>와 같은 목록) — 대사는 그 검사가 보지만 자막은
        /// C#에 있어 거기 안 걸린다. 한 화면에서 대사는 「이름 벽」, 자막은 「봉인」이면 같은 것을 두 이름으로 부르게 된다.
        /// </summary>
        [TestCaseSource(nameof(AllVideos))]
        public void Library_Cues_UseTheEasyWords(string videoId)
        {
            string[] retired = { "봉인", "무명", "지워진 개체", "예비 울타리" };
            Assert.IsTrue(StoryVideoLibrary.TryGet(videoId, out StoryVideoDefinition def));
            foreach (StoryVideoCue cue in def.cues)
                foreach (string word in retired)
                    StringAssert.DoesNotContain(word, cue.text, $"{videoId} 자막에 쓰지 않는 말 「{word}」");
        }

        /// <summary>
        /// 실제 Story.json(JsonUtility)이 <c>introVideoId</c>를 읽는가 + 그 비트의 저작 규칙. story_lint 검사 25·13·36과 같은 규칙을
        /// 런타임 파서 쪽에서 한 번 더 본다 — 필드 이름이 어긋나면 JsonUtility는 조용히 버리고 영상이 그냥 안 나온다.
        /// </summary>
        [Test]
        public void RealStory_IntroVideoBeats_PointToLibraryAndStayShort()
        {
            var expected = new Dictionary<string, string>
            {
                ["ch6_secret"] = StoryVideoLibrary.Ch6Wall,
                ["ch7_opening"] = StoryVideoLibrary.Ch7Fence,
                ["fin_unnamed"] = StoryVideoLibrary.FinShadow,
                ["fin_seal"] = StoryVideoLibrary.FinReturn,
            };

            int intros = 0;
            foreach (StoryBeat beat in StoryService.AllBeats())
            {
                if (beat == null || string.IsNullOrEmpty(beat.introVideoId)) continue;
                intros++;
                Assert.IsTrue(StoryVideoLibrary.TryGet(beat.introVideoId, out _), $"{beat.beatId}: 모르는 introVideoId {beat.introVideoId}");
                Assert.IsTrue(string.IsNullOrEmpty(beat.videoId), $"{beat.beatId}: 대사 앞·뒤 영상을 한 비트에 두지 않는다");
                Assert.IsNotNull(beat.lines, beat.beatId);
                Assert.That(beat.lines.Count, Is.InRange(1, 6), $"{beat.beatId}: 영상이 설명하므로 대사는 1~6줄");
            }

            foreach (KeyValuePair<string, string> pair in expected)
            {
                Assert.IsTrue(StoryService.TryGetBeat(pair.Key, out StoryBeat beat), $"{pair.Key}가 Story.json에 없다");
                Assert.AreEqual(pair.Value, beat.introVideoId, $"{pair.Key}의 대사 앞 영상");
                Assert.IsTrue(string.IsNullOrEmpty(beat.cutsceneId), $"{pair.Key}: 영상으로 옮긴 비트에 옛 컷신이 남았다");
            }
            Assert.GreaterOrEqual(intros, expected.Count);
        }
    }
}
#endif
