using System.Collections.Generic;
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
        // 마을 어르신(박사)에게 첫 대화 — WorldInteractionController가 스토리 NPC 대화 시
        // NotifyTalkToElder로 알린다. 첫 파트너 곤충을 받는 자리라 튜토리얼의 시작점이다.
        TalkToElder,
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

    /// <summary>
    /// 튜토리얼 배열 순서에 기대는 <b>순수</b> 판정. MonoBehaviour와 떼어 놓아 테스트로 고정한다
    /// (<c>StoryObjectiveResolver</c>와 같은 성격).
    /// </summary>
    /// <summary>
    /// 이동 퀘스트 진행 판정의 <b>순수</b> 부분. MonoBehaviour와 떼어 놓아 테스트로 고정한다
    /// (<see cref="InsectGame.Story.StoryStageTimeline"/>과 같은 성격).
    ///
    /// <b>왜 있나.</b> 예전 판정은 <c>Vector3.Distance(now, lastFrame) &gt; 1f</c> 하나였다.
    /// 그건 "한 프레임에 1m 이상"이라 60fps에서 <b>초속 60m</b>를 요구한다 —
    /// 플레이어 이동 속도는 8m/s(의상 보정 최대 ×2)라 프레임당 0.13~0.27m다.
    /// 즉 <b>게임의 첫 퀘스트가 시키는 대로 걸어서는 절대 참이 되지 않았다.</b>
    /// 참이 되는 경우는 워프뿐인데(서브에리어 진입 2000m 점프), 그건 "첫 걸음"이 아니다.
    /// </summary>
    public static class MovementProgress
    {
        /// <summary>1카운트에 필요한 누적 이동 거리(m). 몇 걸음이면 되도록 짧게 잡는다.</summary>
        public const float RequiredMeters = 3f;

        /// <summary>
        /// 한 프레임에 이 이상 움직였으면 걸은 게 아니라 <b>워프</b>다 — 세지 않는다.
        /// 서브에리어 진입·지도 이동·스폰 재배치가 여기 걸린다. 8m/s가 한 프레임에 5m를
        /// 가려면 0.6초짜리 프레임이어야 하므로 정상 이동을 잘라내지 않는다.
        /// </summary>
        public const float TeleportMeters = 5f;

        /// <summary>
        /// 이번 프레임 이동량을 누적하고, 한 카운트를 채웠으면 true(그리고 누적을 비운다).
        /// </summary>
        public static bool Accumulate(float frameDistance, ref float accumulated)
        {
            if (frameDistance < 0f || frameDistance >= TeleportMeters) return false;
            accumulated += frameDistance;
            if (accumulated < RequiredMeters) return false;
            accumulated = 0f;
            return true;
        }
    }

    public static class TutorialQuestOrder
    {
        /// <summary>
        /// <b>배열 중간에 삽입돼 기존 세이브가 건너뛴 스토리 퀘스트</b>를 찾는다.
        /// 자기보다 뒤에 있는 스토리 퀘스트를 이미 깬 세이브라면 그건 지나간 단계다.
        ///
        /// 없으면 이미 진행한 유저가 <b>뒤로 되돌아간다</b> — <c>ActivateNextQuest</c>가 배열을
        /// 앞에서부터 훑어 첫 미완료를 고르기 때문이다. <c>q_talk_elder</c>를 3번 자리에 끼우자
        /// 튜토리얼을 마친 세이브에서 "마을 어르신을 만나다"가 부활했다.
        ///
        /// <b>경계는 "가장 뒤에 완료된 것"이다.</b> 그보다 앞만 소급하므로, 아직 할 차례인
        /// 퀘스트는 건드리지 않는다 — q_move만 깬 세이브에서 q_talk_elder는 그대로 다음 차례다.
        ///
        /// 판정의 전제는 <b>완료 순서 = 배열 순서</b>이고, 그건 모든 prereq가 배열에서 자기보다
        /// 앞을 가리킬 때만 성립한다(<c>quest_lint</c> 검사 9가 고정한다).
        /// 서브 퀘스트는 다중 활성이라 순서 개념이 없어 대상이 아니다.
        /// </summary>
        public static List<string> CollectBackfillTargets(
            TutorialQuest[] quests, System.Func<string, bool> isCompleted)
        {
            var targets = new List<string>();
            if (quests == null || isCompleted == null) return targets;

            int lastCompleted = -1;
            for (int i = 0; i < quests.Length; i++)
            {
                TutorialQuest q = quests[i];
                if (q == null || q.category != QuestCategory.Story) continue;
                if (isCompleted(q.questId)) lastCompleted = i;
            }
            if (lastCompleted < 0) return targets;   // 스토리를 하나도 안 깬 세이브(신규 포함)

            for (int i = 0; i < lastCompleted; i++)
            {
                TutorialQuest q = quests[i];
                if (q == null || q.category != QuestCategory.Story) continue;
                if (isCompleted(q.questId)) continue;
                targets.Add(q.questId);
            }
            return targets;
        }
    }
}
