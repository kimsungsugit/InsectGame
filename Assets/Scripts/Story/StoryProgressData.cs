using System.Collections.Generic;

namespace InsectGame.Story
{
    // story_progress.json 직렬화 모델. SaveScope.FilePath로 계정별 격리(DexSaveData 관례).
    // seenBeatIds는 무한 증가 집합이라 JSON 리스트가 자연스럽다(퀘스트 PlayerPrefs CSV는 레거시).
    [System.Serializable]
    public class StoryProgressData
    {
        public List<string> seenBeatIds = new List<string>();
        public string activeChapterId;
    }
}
