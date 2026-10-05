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
        // 명부회 오염 거점 정화 — RegionBlightManager.RegionCleansed가 알린다.
        // 이벤트 기반이라 구독 등록이 급소다(q_team 전례).
        CleanseBlight,
        // ── 나의 섬 ── 전부 IslandManager가 행동이 성립한 지점에서 Notify___로 직접 알린다(이벤트 구독이 아니다).
        // 내 섬에 들어감.
        VisitIsland,
        // 보유 곤충을 섬에 풀어놓음.
        ReleaseOnIsland,
        // 보관함의 물건을 섬에 놓음(옮기기·돌리기는 세지 않는다).
        PlaceIslandObject,
        // 쌓인 수확물을 받음.
        HarvestIsland,
        // 섬 상점에서 물건을 삼.
        IslandPurchase,
        // 다른 사람의 섬을 구경함.
        VisitFriendIsland,
        // 조건부 포획 — 몸길이·속성·이로치처럼 잡은 개체의 성질이 TutorialQuest의 조건 필드와 맞아야 센다.
        // 판정은 QuestTraitRules(순수). 포획 지점이 CaptureFacts를 실어 NotifyCapture(CaptureFacts)로 알린다.
        CaptureTrait,
        // 조건부 전투 승리 — 상대 등급·속성·레벨 차·내 행동 수·남은 HP가 조건과 맞아야 센다.
        // 전투 컨트롤러가 승리 지점에서 BattleFacts를 실어 NotifyBattleFeat로 알린다. resetOnLoss면 연승.
        BattleFeat,
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
        // 코인 보상 — 섬 퀘스트가 쓴다(가구 값이 코인이다). 다이아는 보상으로 줄 수 없다:
        // 서버 규칙이 클라이언트의 다이아 증가를 거부해 세이브 업로드 전체가 막힌다.
        public int rewardCoins = 0;
        // 섬 물건 보상(IslandCatalog의 id) — 섬 보관함으로 들어간다.
        public string rewardIslandObjectId;
        public int rewardIslandObjectCount = 0;
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
        // Side 전용 "지역 의뢰": 채우면 ①그 리전이 열리기 전에는 활성이 아니고 ②그 리전 안에서
        // 한 행동만 센다(서브에리어 포함 — 진입 중에도 RegionManager.CurrentRegion은 부모 리전에 머문다).
        // 비우면(기본 null) 기존 서브 퀘스트 그대로 어디서든 센다. 마을 이야기(Story.json의 town
        // 챕터)의 매듭 비트가 requiredQuestId로 이 퀘스트의 완료를 관찰한다 — 퀘스트는 스토리를 모른다.
        public string requiredRegionId;

        // ── 조건부 퀘스트(QuestType.CaptureTrait / BattleFeat) 전용 조건 — Side 전용 ──
        // 0·None·false·Common이 "조건 없음"이다(기본값이라 이 필드를 안 쓰는 기존 퀘스트는 영향이 없다).
        // 여러 개를 채우면 **전부** 맞아야 센다 — 판정은 QuestTraitRules가 한다.
        // 몸길이(mm): 개체의 몸길이가 이 범위 안일 때만. 소수 첫째 자리로 맞춰 본다(화면에 보이는 값과 같게).
        public float minSizeMm = 0f;
        public float maxSizeMm = 0f;
        // 종 기준 대비 크기 배율(0.75~1.25): 같은 종 평균보다 얼마나 큰가·작은가. 종마다 평균이 달라 몸길이와 따로 둔다.
        public float minSizeRatio = 0f;
        public float maxSizeRatio = 0f;
        // 잡은(CaptureTrait) 또는 맞선(BattleFeat) 곤충의 속성 — 주속성이나 부속성 어느 한쪽이면 된다.
        public InsectGame.Data.InsectElement requiredElement = InsectGame.Data.InsectElement.None;
        // CaptureTrait: 이로치(색다른 곤충)만.
        public bool requireShiny = false;
        // 잡은·맞선 곤충의 등급이 이 이상일 때만. 기본 Common은 모든 등급이라 "조건 없음"이다.
        public InsectGame.Data.InsectRarity minRarity = InsectGame.Data.InsectRarity.Common;
        // BattleFeat: (상대 레벨 − 내 곤충 레벨)이 이 이상. 0이면 조건 없음.
        public int minLevelEdge = 0;
        // BattleFeat: 이긴 전투에서 내가 행동한 횟수가 이 이하. 0이면 조건 없음.
        public int maxTurns = 0;
        // BattleFeat: 전투가 끝났을 때 내 곤충의 남은 HP가 이 퍼센트 이상. 0이면 조건 없음.
        public int minHpPercent = 0;
        // BattleFeat: 지거나 도망치면 진행을 0으로 되돌린다(연승). 조건이 안 맞는 승리는 건드리지 않는다.
        public bool resetOnLoss = false;
    }

    /// <summary>
    /// 지역 의뢰(<see cref="TutorialQuest.requiredRegionId"/>)의 <b>순수</b> 판정.
    /// MonoBehaviour와 떼어 놓아 테스트로 고정한다(<see cref="MovementProgress"/>와 같은 성격).
    /// </summary>
    public static class QuestRegionGate
    {
        /// <summary>
        /// 이번 행동이 이 퀘스트의 진행으로 세어지는가. 리전 한정이 없으면 늘 true.
        /// 리전 밖(리전 사이 빈 땅 — <c>CurrentRegion</c>이 null)에서 한 행동은 세지 않는다.
        /// </summary>
        public static bool Counts(string requiredRegionId, string currentRegionId)
        {
            if (string.IsNullOrEmpty(requiredRegionId)) return true;
            return requiredRegionId == currentRegionId;
        }

        /// <summary>
        /// 리전 게이트가 열렸는가. 판정 자체(<c>RegionManager.IsRegionAccessible</c>)는 호출부가 넘긴다 —
        /// 해금 규칙의 단일 출처를 여기 복제하지 않는다. 판정기가 없으면 닫힌 쪽으로 둔다
        /// (잠긴 리전의 의뢰가 먼저 뜨는 것보다 조금 늦게 뜨는 편이 낫다).
        /// </summary>
        public static bool IsOpen(string requiredRegionId, System.Func<string, bool> isRegionAccessible)
        {
            if (string.IsNullOrEmpty(requiredRegionId)) return true;
            return isRegionAccessible != null && isRegionAccessible(requiredRegionId);
        }
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

        /// <summary>
        /// <b>미리 세는 목표인가.</b> 포획·전투·레이드·레벨업처럼 "몇 번 했는가"를 세는 것만이다.
        ///
        /// 넣으면 안 되는 것이 있다:
        /// <list type="bullet">
        /// <item><c>CleanseBlight</c> — 「하나 무너뜨리기」와 「하나 더」가 이어져 있다. 미리 세면 첫 정화 하나가
        /// 둘을 한꺼번에 깬다.</item>
        /// <item><c>VisitRegion</c>·<c>VisitSubArea</c>·<c>Movement</c> — 그 퀘스트가 가리키는 순간의 행동이어야 한다
        /// (초원에 들어간 것이 "연못에 가 보세요"를 깨면 안 된다).</item>
        /// <item><c>TalkToElder</c>·<c>DefeatGuardian</c> — 스토리 비트와 맞물려 따로 정합한다.</item>
        /// </list>
        /// </summary>
        public static bool IsBankable(QuestType type)
        {
            switch (type)
            {
                case QuestType.Capture:
                case QuestType.CaptureRare:
                case QuestType.CaptureRarity:
                case QuestType.Battle:
                case QuestType.RaidBattle:
                case QuestType.LevelUp:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 이번 행동이 이 퀘스트의 진행으로 세어지는가. 포획은 <paramref name="action"/>을
        /// <c>Capture</c>로 넘기고 등급으로 가른다(<c>NotifyCapture</c>의 활성 퀘스트 판정과 같은 규칙).
        /// </summary>
        public static bool CountsToward(TutorialQuest quest, QuestType action, InsectGame.Data.InsectRarity rarity)
        {
            if (quest == null || !IsBankable(quest.type)) return false;
            if (action != QuestType.Capture) return quest.type == action;

            switch (quest.type)
            {
                case QuestType.Capture: return true;
                case QuestType.CaptureRare: return rarity >= InsectGame.Data.InsectRarity.Uncommon;
                case QuestType.CaptureRarity: return rarity == quest.requiredRarity;
                default: return false;
            }
        }

        /// <summary>
        /// 이번 행동을 <b>미리 세어 둘</b> 스토리 퀘스트 — 아직 안 끝났고 지금 활성도 아닌 것.
        /// 활성 퀘스트는 원래 경로(<c>IncrementProgress</c>)가 올리므로 여기서 빼야 두 번 세지 않는다.
        /// 서브 퀘스트는 대상이 아니다(다중 활성이라 자기 경로로 센다).
        /// </summary>
        public static void CollectBankTargets(TutorialQuest[] quests, System.Func<string, bool> isCompleted,
            string activeQuestId, QuestType action, InsectGame.Data.InsectRarity rarity, List<TutorialQuest> into)
        {
            if (into == null) return;
            into.Clear();
            if (quests == null || isCompleted == null) return;

            for (int i = 0; i < quests.Length; i++)
            {
                TutorialQuest q = quests[i];
                if (q == null || q.category != QuestCategory.Story) continue;
                if (q.questId == activeQuestId || isCompleted(q.questId)) continue;
                if (CountsToward(q, action, rarity)) into.Add(q);
            }
        }
    }
}
