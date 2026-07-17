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
    }
}
