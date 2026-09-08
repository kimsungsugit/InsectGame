using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Story
{
    // static 로더 — InsectLoreService 복제(ResourceName="Story", StoryList 파싱).
    // Assets/Resources/Story.json → Dictionary<beatId, StoryBeat>. 재컴파일 없이 데이터 편집.
    public static class StoryService
    {
        private static Dictionary<string, StoryBeat> cache;
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
