using System.Collections.Generic;

namespace InsectGame.Story
{
    // 스토리 한 조각(비트) — JSON(Assets/Resources/Story.json)에 저작, JsonUtility로 파싱.
    // 퀘스트/대화/리전을 갈아엎지 않고 관찰(구독)만 하는 가산 레이어. 필드 컨벤션은 데이터 클래스
    // 관례를 따라 public(GameSaveData/TutorialQuest/InsectLoreEntry와 동형 — JsonUtility 직렬화).
    [System.Serializable]
    public class StoryBeat
    {
        public string beatId;
        public string chapterId;
        public int order;
        // 선행 비트(옵션). 비면 무조건 충족. 채워지면 그 비트를 이미 열람해야 발화.
        public string prerequisiteBeatId;
        // 리전 잠금(옵션). 채워지면 현재 리전이 이 ID일 때만 발화. 무param CaptureInsect/BattleWin의
        // 늦발화 얼룩(엉뚱한 리전에서 옛 비트가 뜨는 것) 차단. 비면 무제약 — JsonUtility 누락 필드는
        // 기본값(null)이라 기존 비트 전부 호환. story_lint 검사 7이 대상 존재·무가드 권고를 검증.
        public string requiredRegionId;
        public StoryTrigger trigger;
        // 이름/초상만 참조(대사는 lines[]에 저작). NpcDialogueDatabase 앰비언트와 분리.
        public string speakerNpcId;
        public List<StoryLine> lines = new List<StoryLine>();
        // 분기(옵션) — 데이터 모델만 보존(story_lint 검사 4). 현 렌더러는 lines[] 순차 표시.
        public List<StoryChoice> choices = new List<StoryChoice>();
        public StoryReward onComplete;
        public bool oneShot = true;
    }

    [System.Serializable]
    public class StoryLine
    {
        public string speaker;
        public string text;
    }

    [System.Serializable]
    public class StoryChoice
    {
        public string text;
        public string nextBeatId;
    }

    // 트리거 타입은 문자열(닫힌 enum) — StoryDirector 평가 switch가 전 케이스 처리.
    // RegionEnter / QuestComplete / LevelReach / CaptureInsect / BattleWin / SubAreaEnter / Immediate
    [System.Serializable]
    public class StoryTrigger
    {
        public string type;
        public string param;
    }

    // 퀘스트 보상 필드 재사용(TutorialQuest와 동형). unlockQuestId는 데이터만 보존
    // (스토리→퀘스트 역주입은 설계상 배제 — story_lint 검사 5의 무결성 대상으로만).
    [System.Serializable]
    public class StoryReward
    {
        public int rewardCandy;
        public int rewardExp;
        public string rewardItemId;
        public int rewardItemCount;
        public string rewardInsectId;
        public string rewardInsectDisplayName;
        public int rewardInsectLevel;
        public string unlockQuestId;
    }

    // JsonUtility 래퍼 — 루트 { "beats": [ ... ] } (InsectLoreList와 동형).
    [System.Serializable]
    public class StoryList
    {
        public List<StoryBeat> beats = new List<StoryBeat>();
    }
}
