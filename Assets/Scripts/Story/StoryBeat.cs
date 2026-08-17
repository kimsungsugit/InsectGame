using System.Collections.Generic;

namespace InsectGame.Story
{
    // 스토리 한 조각(비트) — JSON(Assets/Resources/Story.json)에 저작, JsonUtility로 파싱.
    // 퀘스트/대화/리전을 갈아엎지 않고 관찰(구독)만 하는 가산 레이어. 필드 컨벤션은 데이터 클래스
    // 관례를 따라 public(GameSaveData/TutorialQuest/InsectLoreEntry와 동형 — JsonUtility 직렬화).
    //
    // **모든 비트는 일생 1회다 — 비트별 토글은 없다.** 반복 여부는 StoryDirector의
    // seenBeatIds(story_progress.json)가 전역으로 정하고, 열람한 비트는 EvaluateTriggers가
    // 무조건 건너뛴다. 예전엔 `oneShot` 필드가 있었지만 **읽는 코드가 한 곳도 없어서**
    // `"oneShot": false`로 저작해도 아무 일이 일어나지 않았다(데이터가 동작을 거짓말했다).
    // 되살리려면 onComplete 재지급부터 막아야 한다 — 앰비언트 비트(talk_elder 등)도 캔디 5개를
    // 주므로 그대로 반복시키면 말 걸기 무한 파밍이 된다. 그건 정리가 아니라 별도 설계다.
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
        // 퀘스트 잠금(옵션). 채워지면 그 튜토리얼 퀘스트를 **완료해야** 발화한다.
        // 스토리를 튜토리얼과 갈라 놓는 장치다 — ch1_intro가 이걸 써서, 기본 조작(이동·포획·
        // 컬렉션·도감)을 익히기 전에는 마을 어르신이 이야기를 열어주지 않는다.
        // 비면 무제약. JsonUtility는 JSON에 없는 필드를 건드리지 않으므로 기존 비트 전부 호환.
        public string requiredQuestId;
        public StoryTrigger trigger;
        // 이름/초상만 참조(대사는 lines[]에 저작). NpcDialogueDatabase 앰비언트와 분리.
        public string speakerNpcId;
        public List<StoryLine> lines = new List<StoryLine>();
        // 분기(옵션) — 데이터 모델만 보존(story_lint 검사 4). 현 렌더러는 lines[] 순차 표시.
        public List<StoryChoice> choices = new List<StoryChoice>();
        public StoryReward onComplete;
        // 대사가 끝난 뒤 재생할 컷신(옵션). CutsceneLibrary의 ID여야 한다 — story_lint 검사 9가
        // 실재성을 고정한다(오타는 런타임에 LogWarning만 찍고 조용히 안 나온다).
        // JsonUtility는 JSON에 없는 필드를 건드리지 않으므로 기존 비트 전부 null로 남아 호환된다.
        public string cutsceneId;
        // 대사 **앞**에 재생할 NPC 연출(옵션) — 등장·다가옴. StoryStageLibrary의 ID여야 한다.
        // 대사가 없는 비트에는 무의미하다(모달 자체가 안 뜨므로 게이트가 걸리지 않는다).
        public string stageEnterId;
        // 대사 **뒤**에 재생할 NPC 연출(옵션) — 퇴장·안내.
        // **cutsceneId와 같은 비트에 함께 두지 않는다** — 둘 다 조작·카메라를 뺏어 다툰다.
        // story_lint 검사 13이 그 조합을 막는다.
        public string stageExitId;
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
