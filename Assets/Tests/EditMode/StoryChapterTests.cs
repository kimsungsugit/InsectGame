#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Story;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 장 「지난 이야기」(<see cref="StoryChapter"/>) — 장을 여는 비트의 대사 앞에 카드로 뜨고 저널 장 탭 머리에 보인다.
    /// 실패는 전부 조용하다: 여는 비트 ID가 오타면 카드가 안 뜨고, 다른 장의 비트면 엉뚱한 장면 앞에 뜬다.
    /// story_lint 검사 34가 같은 기준을 정적으로 본다 — 기준값(4줄·40자)은 그쪽과 같아야 한다.
    /// </summary>
    [TestFixture]
    public class StoryChapterTests
    {
        private static readonly string[] CampaignChapters =
        {
            "ch1", "ch2", "ch3", "ch4", "ch5", "ch6", "ch7", "ch8", "ch9", "ch10", "ch11", "ch12", "fin",
        };

        private const int MaxRecapLines = 4;
        private const int MaxCardLineChars = 40;
        // 지난 이야기가 없는 장 — 1장만 recap이 비어도 된다.
        private const string FirstChapterId = "ch1";

        private static Dictionary<string, StoryBeat> BeatsById()
        {
            var byId = new Dictionary<string, StoryBeat>();
            foreach (StoryBeat b in StoryService.AllBeats())
                if (b != null && !string.IsNullOrEmpty(b.beatId)) byId[b.beatId] = b;
            return byId;
        }

        // ── 실제 Story.json ──

        [Test]
        public void AllChapters_RealStory_AreThirteenCampaignChaptersInOrder()
        {
            var ids = new List<string>();
            foreach (StoryChapter c in StoryService.AllChapters())
                ids.Add(c != null ? c.chapterId : null);

            CollectionAssert.AreEqual(CampaignChapters, ids,
                "장은 ch1~ch12·fin 13개가 이 순서여야 한다(배열 순서가 곧 장 순서) — 실제: " + string.Join(",", ids));
        }

        [Test]
        public void AllChapters_RealStory_OpeningBeatExistsInSameChapter()
        {
            Dictionary<string, StoryBeat> byId = BeatsById();
            Assume.That(byId.Count, Is.GreaterThan(0), "Story.json 로드 실패");

            var openings = new HashSet<string>();
            foreach (StoryChapter c in StoryService.AllChapters())
            {
                Assert.IsNotNull(c);
                Assert.IsFalse(string.IsNullOrEmpty(c.openingBeatId), $"{c.chapterId}: openingBeatId가 비어 있다");
                Assert.IsTrue(byId.TryGetValue(c.openingBeatId, out StoryBeat opening),
                    $"{c.chapterId}: 여는 비트 {c.openingBeatId}가 없다 — 카드가 영영 안 뜬다");
                Assert.AreEqual(c.chapterId, opening.chapterId,
                    $"{c.chapterId}: 여는 비트 {c.openingBeatId}가 다른 장({opening.chapterId})의 비트다 — 엉뚱한 장면 앞에 카드가 뜬다");
                Assert.IsTrue(openings.Add(c.openingBeatId),
                    $"{c.openingBeatId}를 두 장이 연다 — 뒤 장의 카드가 영영 안 뜬다");
            }
        }

        [Test]
        public void AllChapters_RealStory_RecapAndGoalFitTheCard()
        {
            int count = 0;
            foreach (StoryChapter c in StoryService.AllChapters())
            {
                count++;
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.title), $"{c.chapterId}: title이 비어 있다");
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.goal), $"{c.chapterId}: goal이 비어 있다");
                Assert.LessOrEqual(c.goal.Length, MaxCardLineChars, $"{c.chapterId}: goal {c.goal.Length}자");

                Assert.IsNotNull(c.recap, $"{c.chapterId}: recap이 null");
                Assert.LessOrEqual(c.recap.Count, MaxRecapLines, $"{c.chapterId}: recap {c.recap.Count}줄");
                if (c.chapterId != FirstChapterId)
                    Assert.GreaterOrEqual(c.recap.Count, 1, $"{c.chapterId}: 지난 이야기가 없다(1장 외에는 1줄 이상)");
                for (int i = 0; i < c.recap.Count; i++)
                {
                    string line = c.recap[i];
                    Assert.IsFalse(string.IsNullOrWhiteSpace(line), $"{c.chapterId}.recap[{i}]: 빈 줄");
                    Assert.LessOrEqual(line.Length, MaxCardLineChars, $"{c.chapterId}.recap[{i}]: {line.Length}자");
                }
            }
            Assert.AreEqual(CampaignChapters.Length, count, "장 수가 맞지 않는다");
        }

        [Test]
        public void TryGetChapterOpenedBy_RealStory_TrueOnlyForOpeningBeats()
        {
            Dictionary<string, StoryBeat> byId = BeatsById();
            Assume.That(byId.Count, Is.GreaterThan(0), "Story.json 로드 실패");

            var openingToChapter = new Dictionary<string, string>();
            foreach (StoryChapter c in StoryService.AllChapters())
                if (c != null && !string.IsNullOrEmpty(c.openingBeatId)) openingToChapter[c.openingBeatId] = c.chapterId;
            Assume.That(openingToChapter.Count, Is.GreaterThan(0), "chapters가 비어 있다");

            foreach (string beatId in byId.Keys)
            {
                bool opens = StoryService.TryGetChapterOpenedBy(beatId, out StoryChapter chapter);
                bool expected = openingToChapter.TryGetValue(beatId, out string chapterId);
                Assert.AreEqual(expected, opens, $"{beatId}: 장을 여는 비트 판정이 어긋난다");
                if (expected)
                {
                    Assert.IsNotNull(chapter);
                    Assert.AreEqual(chapterId, chapter.chapterId);
                    Assert.AreEqual(beatId, chapter.openingBeatId);
                }
                else
                {
                    Assert.IsNull(chapter, $"{beatId}: 여는 비트가 아닌데 장을 돌려준다");
                }
            }
        }

        [Test]
        public void TryGetChapterOpenedBy_NullOrEmptyBeatId_ReturnsFalse()
        {
            Assert.IsFalse(StoryService.TryGetChapterOpenedBy(null, out StoryChapter a));
            Assert.IsNull(a);
            Assert.IsFalse(StoryService.TryGetChapterOpenedBy(string.Empty, out StoryChapter b));
            Assert.IsNull(b);
        }

        [Test]
        public void TryGetChapter_RealStory_FindsEveryCampaignChapterAndRejectsUnknown()
        {
            foreach (string id in CampaignChapters)
            {
                Assert.IsTrue(StoryService.TryGetChapter(id, out StoryChapter c), $"{id} 장이 없다");
                Assert.AreEqual(id, c.chapterId);
            }
            Assert.IsFalse(StoryService.TryGetChapter("town", out StoryChapter town), "마을 이야기는 장 카드가 없다");
            Assert.IsNull(town);
            Assert.IsFalse(StoryService.TryGetChapter(null, out _));
        }

        // ── 순수 파싱 — 옛 JSON 호환 ──

        [Test]
        public void StoryListJson_WithoutChaptersKey_ParsesAsEmptyChapterList()
        {
            // 장 데이터가 들어오기 전의 Story.json 형태. JsonUtility는 없는 키를 건드리지 않으므로 필드 초기값(빈 목록)이 남아야 한다 —
            // null이 되면 AllChapters·카드 경로가 NRE로 죽는다.
            const string oldJson = "{\"beats\":[{\"beatId\":\"ch1_intro\",\"chapterId\":\"ch1\",\"order\":1}]}";

            StoryList list = JsonUtility.FromJson<StoryList>(oldJson);

            Assert.IsNotNull(list);
            Assert.AreEqual(1, list.beats.Count);
            Assert.IsNotNull(list.chapters, "chapters 키가 없는 옛 JSON이 null 목록으로 읽혔다");
            Assert.AreEqual(0, list.chapters.Count);
            Assert.IsTrue(string.IsNullOrEmpty(list.beats[0].why), "why가 없는 옛 비트는 이유가 비어 있다");
            Assert.AreEqual(string.Empty, StoryObjectiveResolver.WhyFor(list.beats[0], false),
                "이유 없는 옛 비트도 HUD에는 빈 문자열이 간다(null을 흘리지 않는다)");
        }

        [Test]
        public void StoryListJson_WithChapters_ParsesTitleRecapGoalInOrder()
        {
            const string json = "{\"beats\":[],\"chapters\":["
                + "{\"chapterId\":\"ch1\",\"title\":\"1장 · 초원\",\"openingBeatId\":\"ch1_intro\",\"recap\":[],\"goal\":\"어르신 만나기\"},"
                + "{\"chapterId\":\"ch2\",\"title\":\"2장 · 연못\",\"openingBeatId\":\"ch2_arrive\","
                + "\"recap\":[\"첫 줄\",\"둘째 줄\",\"셋째 줄\"],\"goal\":\"연못으로\"}]}";

            StoryList list = JsonUtility.FromJson<StoryList>(json);

            Assert.AreEqual(2, list.chapters.Count);
            Assert.AreEqual("ch1", list.chapters[0].chapterId);
            Assert.AreEqual(0, list.chapters[0].recap.Count, "빈 recap은 빈 목록이다(카드 없음)");
            StoryChapter ch2 = list.chapters[1];
            Assert.AreEqual("2장 · 연못", ch2.title);
            Assert.AreEqual("ch2_arrive", ch2.openingBeatId);
            CollectionAssert.AreEqual(new[] { "첫 줄", "둘째 줄", "셋째 줄" }, ch2.recap);
            Assert.AreEqual("연못으로", ch2.goal);
        }

        [Test]
        public void StoryListJson_ChapterWithoutRecapKey_KeepsEmptyRecapList()
        {
            const string json = "{\"chapters\":[{\"chapterId\":\"ch1\",\"title\":\"1장\",\"openingBeatId\":\"ch1_intro\",\"goal\":\"g\"}]}";

            StoryList list = JsonUtility.FromJson<StoryList>(json);

            Assert.AreEqual(1, list.chapters.Count);
            Assert.IsNotNull(list.chapters[0].recap, "recap 키가 없으면 빈 목록이어야 한다 — 카드 경로가 Count를 읽는다");
            Assert.AreEqual(0, list.chapters[0].recap.Count);
            Assert.IsNotNull(list.beats, "beats 키가 없어도 빈 목록");
        }
    }
}
#endif
