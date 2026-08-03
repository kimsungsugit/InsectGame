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
        // 곤충잡이 아이와의 1v1 대결 승리 — NpcDuelController가 NotifyNpcDuelWon으로 알린다.
        NpcDuel,
        // 특정 등급 곤충 포획. 어느 등급인지는 TutorialQuest.requiredRarity가 정한다
        // (Capture=전체, CaptureRare=Uncommon+ 와 달리 등급 하나를 콕 집는다).
        CaptureRarity,
        // 주간 크기 대결에서 등급 달성 — WeeklyContestManager.TierReached가 알린다.
        SizeContest,
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
        // QuestType.CaptureRarity 전용: 이 등급을 포획해야 진행된다. 다른 타입에서는 무시.
        // 기본값 Common은 enum의 0이라, 이 필드를 안 쓰는 기존 퀘스트에 영향이 없다.
        public InsectGame.Data.InsectRarity requiredRarity = InsectGame.Data.InsectRarity.Common;
    }
}
