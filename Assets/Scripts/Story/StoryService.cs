using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Story
{
    // static 로더 — InsectLoreService 복제(ResourceName="Story", StoryList 파싱).
    // Assets/Resources/Story.json → Dictionary<beatId, StoryBeat>. 재컴파일 없이 데이터 편집.
    public static class StoryService
    {
        private static Dictionary<string, StoryBeat> cache;
        // 장별 「지난 이야기」 — JSON 배열 순서 그대로(장 순서가 의미를 가진다).
        private static List<StoryChapter> chapters;
        private const string ResourceName = "Story";

        public static bool TryGetBeat(string beatId, out StoryBeat beat)
        {
            beat = null;
            if (string.IsNullOrEmpty(beatId))
            {
                return false;
            }

            EnsureCache();
            if (cache == null)
            {
                return false;
            }

            return cache.TryGetValue(beatId, out beat);
        }

        public static IEnumerable<StoryBeat> AllBeats()
        {
            EnsureCache();
            if (cache == null)
            {
                return System.Array.Empty<StoryBeat>();
            }

            return cache.Values;
        }

        /// <summary>장 「지난 이야기」 — JSON 순서(장 순서). 없으면 빈 목록.</summary>
        public static IReadOnlyList<StoryChapter> AllChapters()
        {
            EnsureCache();
            return chapters ?? (IReadOnlyList<StoryChapter>)System.Array.Empty<StoryChapter>();
        }

        public static bool TryGetChapter(string chapterId, out StoryChapter chapter)
        {
            chapter = null;
            if (string.IsNullOrEmpty(chapterId)) return false;
            foreach (StoryChapter c in AllChapters())
            {
                if (c != null && c.chapterId == chapterId) { chapter = c; return true; }
            }
            return false;
        }

        /// <summary>
        /// 이 비트가 여는 장 — 그 장의 <see cref="StoryChapter.openingBeatId"/>가 이 비트면 true.
        /// 카드는 지난 이야기(<see cref="StoryChapter.recap"/>)가 있을 때만 뜬다.
        /// </summary>
        public static bool TryGetChapterOpenedBy(string beatId, out StoryChapter chapter)
        {
            chapter = null;
            if (string.IsNullOrEmpty(beatId)) return false;
            foreach (StoryChapter c in AllChapters())
            {
                if (c != null && c.openingBeatId == beatId) { chapter = c; return true; }
            }
            return false;
        }

        private static void EnsureCache()
        {
            if (cache != null)
            {
                return;
            }

            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null)
            {
                return;
            }

            StoryList list = JsonUtility.FromJson<StoryList>(asset.text);
            cache = new Dictionary<string, StoryBeat>();
            chapters = list != null && list.chapters != null ? list.chapters : new List<StoryChapter>();
            if (list == null || list.beats == null)
            {
                return;
            }

            foreach (StoryBeat beat in list.beats)
            {
                if (beat != null && !string.IsNullOrEmpty(beat.beatId))
                {
                    cache[beat.beatId] = beat;
                }
            }
        }
    }
}
