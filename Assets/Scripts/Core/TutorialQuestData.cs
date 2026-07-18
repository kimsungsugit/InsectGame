using UnityEngine;

namespace InsectGame.Core
{
    public enum QuestType
    {
        Movement,
        Capture,
        ViewCollection,
        LevelUp,
        UseItem,
        Battle,
        Training,
        SetTeam,
        RaidBattle,
        DefeatGuardian,
        VisitRegion,
        VisitSubArea,
        OpenDex,
        EquipSkill,
        CaptureRare,
    }

    // 퀘스트 분류 — Story(선형 메인 체인) vs Side(다중 활성, 일부 반복 상승).
    public enum QuestCategory
    {
        Story,
        Side,
    }

    [System.Serializable]
    public class TutorialQuest
    {
        public string questId;
        public string title;
        public string description;
        public string hint;
        public QuestType type;
        public int targetCount = 1;
        public int rewardCandy = 0;
        public int rewardExp = 0;
        public string rewardItemId;
        public int rewardItemCount = 0;
        public string rewardInsectId;
        public string rewardInsectDisplayName;
        public int rewardInsectLevel = 1;
        public string prerequisiteQuestId;
        // 분류: 기본 Story(기존 선형 체인 그대로). Side는 다중 활성 + 반복 상승 지원.
        public QuestCategory category = QuestCategory.Story;
        // Side 전용: true면 완료 시 영구완료 대신 목표를 올려 재시작(반복).
        public bool repeatable = false;
        // Side 반복 상승량: 유효 목표 = targetCount + (완료 횟수 × targetIncrement).
        public int targetIncrement = 0;
    }
}
