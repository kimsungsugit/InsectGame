using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    public class TutorialQuestManager : MonoBehaviour, ICloudReloadable
    {
        public static TutorialQuestManager Instance { get; private set; }

        private PlayerInsectCollection insectCollection;
        private PlayerCandyInventory candyInventory;
        private PlayerProgressController progressController;
        private PlayerItemInventory itemInventory;
        private Battle.InsectBattleController battleController;
        private Battle.RaidBattleController raidController;
        private Dex.DexController dexController;
        private TrainingManager trainingManager;
        private BattleTeamManager battleTeamManager;
        private RegionManager regionManager;
        private WeeklyContestManager weeklyContest;

        private TutorialQuest[] allQuests;
        private Dictionary<string, int> questProgress = new Dictionary<string, int>();
        private HashSet<string> completedQuests = new HashSet<string>();
        // 완료됐지만 아직 퀘스트 창(DrawDetailPanel)에서 확인 안 한 퀘스트 — 퀵바 배지 카운터 소스.
        private HashSet<string> unseenCompleted = new HashSet<string>();
        private string activeQuestId;
        private bool tutorialSessionStarted;

        // 서브 퀘스트 상태 — 클라우드 동기(CloudSaveManager DTO questSideProgress/questSideRepeat). 스토리 questProgress와 별개 키.
        private Dictionary<string, int> sideProgress = new Dictionary<string, int>();
        private Dictionary<string, int> sideRepeatCount = new Dictionary<string, int>();

        private Vector3 lastPlayerPos;
        private Transform cachedPlayerTransform; // 매 프레임 GameObject.Find("Player") 회피

        // 플레이어 Transform 지연 캐싱 — 최초 1회만 Find, 이후 재사용(디스폰 시 재탐색).
        private Transform PlayerTransform()
        {
            if (cachedPlayerTransform == null)
            {
                GameObject p = GameObject.Find("Player");
                if (p != null) cachedPlayerTransform = p.transform;
            }
            return cachedPlayerTransform;
        }

        // 계정별 키 — 같은 기기에서 계정 간 퀘스트 진행이 섞이지 않도록 UserId로 스코핑.
        // (비로그인 시 전역 키로 폴백. 클라우드 브리지(CloudSaveManager)도 동일 스코핑 사용.)
        private static string ProgressKey => AuthManager.ScopedKey(GameConstants.PrefsKeys.QuestProgress);
        private static string CompletedKey => AuthManager.ScopedKey(GameConstants.PrefsKeys.QuestCompleted);
        private static string ActiveKey => AuthManager.ScopedKey(GameConstants.PrefsKeys.ActiveQuest);
        private static string UnseenKey => AuthManager.ScopedKey(GameConstants.PrefsKeys.QuestUnseen);
        private static string SideProgressKey => AuthManager.ScopedKey(GameConstants.PrefsKeys.QuestSideProgress);
        private static string SideRepeatKey => AuthManager.ScopedKey(GameConstants.PrefsKeys.QuestSideRepeat);

        public event System.Action<TutorialQuest> QuestActivated;
        public event System.Action<TutorialQuest, int, int> QuestProgressUpdated;
        public event System.Action<TutorialQuest> QuestCompleted;

        public TutorialQuest ActiveQuest { get; private set; }

        // 퀵바 퀘스트 버튼 배지에 표시할 '완료했지만 아직 안 본' 퀘스트 수. QuickAccessBarUI가 매 프레임 폴링.
        public int UnseenCompletedCount => unseenCompleted.Count;

        public int ActiveProgress
        {
            get
            {
                if (activeQuestId != null && questProgress.ContainsKey(activeQuestId))
                    return questProgress[activeQuestId];
                return 0;
            }
        }

        public bool AllCompleted
        {
            // 스토리 퀘스트만 대상 — 서브(반복)는 영구 완료가 없어 카운트에서 제외.
            get
            {
                if (allQuests == null) return false;
                foreach (TutorialQuest q in allQuests)
                    if (q.category == QuestCategory.Story && !completedQuests.Contains(q.questId)) return false;
                return true;
            }
        }

        public void AutoWire(PlayerInsectCollection col, PlayerCandyInventory candy,
            PlayerProgressController prog, PlayerItemInventory items,
            Battle.InsectBattleController battle, Battle.RaidBattleController raid,
            Dex.DexController dex, TrainingManager training,
            BattleTeamManager team, RegionManager region)
        {
            if (insectCollection == null) insectCollection = col;
            if (candyInventory == null) candyInventory = candy;
            if (progressController == null) progressController = prog;
            if (itemInventory == null) itemInventory = items;
            if (battleController == null) battleController = battle;
            if (raidController == null) raidController = raid;
            if (dexController == null) dexController = dex;
            if (trainingManager == null) trainingManager = training;
            if (battleTeamManager == null) battleTeamManager = team;
            if (regionManager == null) regionManager = region;
        }

        /// <summary>
        /// 주간 크기 대결 연결. Start(SubscribeEvents) 뒤에 배선될 수 있어 여기서도 구독한다 —
        /// q_team이 구독 등록 누락으로 영구 정지했던 전례(rules/quest-system.md) 때문에
        /// 이벤트 기반 QuestType은 구독 지점을 반드시 이중으로 확인한다.
        /// </summary>
        public void AutoWire(WeeklyContestManager contest)
        {
            if (weeklyContest == contest) return;
            if (weeklyContest != null) weeklyContest.TierReached -= OnContestTierReached;
            weeklyContest = contest;
            if (weeklyContest != null) weeklyContest.TierReached += OnContestTierReached;
        }

        /// <summary>이번 주 대결 대상 종 — TutorialQuestUI가 퀘스트 문구를 덮어쓸 때 쓴다.</summary>
        public Data.InsectData WeeklyContestTarget =>
            weeklyContest != null ? weeklyContest.TargetInsect : null;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            Initialize();
            LoadProgress();
            SubscribeEvents();

            // 로그인/월드 로비 뒤에 시작해야 첫 퀘스트 배너가 가려지지 않는다.
            ActiveQuest = GetQuest(activeQuestId);

            Transform player = PlayerTransform();
            if (player != null)
            {
                lastPlayerPos = player.position;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            // **파기된 자신을 static에 남기지 않는다.** 남겨 두면 `Instance != null`(파괴 검사)은
            // false인데 `Instance?.`(진짜 null 검사)는 통과해 두 관용구가 서로 다른 답을 낸다 —
            // 저장소 안에 `Instance?.`가 19곳 있고 그중 절반이 이 매니저다.
            // 이 오브젝트는 `World/TutorialQuestManager`로 **부모가 있어** DontDestroyOnLoad
            // 대상도 아니다(씬 재로드마다 실제로 파기된다). WorldChannelManager와 같은 처리.
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        private void Initialize()
        {
            // **배열 순서가 곧 첫 퀘스트다.** ActivateNextQuest가 배열을 위에서부터 훑어
            // 첫 미완료·prereq충족 스토리 퀘스트를 고르는데, q_collection/q_dex는 prereq가
            // 아예 없어서 순서만이 그 둘보다 먼저 오게 하는 유일한 장치다.
            //
            // q_move가 맨 앞인 이유: 예전엔 q_approach(첫 포획)가 첫 퀘스트라, 처음 켠 사람이
            // **움직이는 법을 배우기 전에** 곤충을 잡으라는 지시를 받았다.
            allQuests = new TutorialQuest[]
            {
                new TutorialQuest
                {
                    questId = "q_move", title = "첫 걸음!",
                    description = "화면 왼쪽 조이스틱으로 움직여보세요",
                    hint = "화면 왼쪽 아래를 누른 채 원하는 방향으로 밀어보세요",
                    type = QuestType.Movement, targetCount = 1,
                    rewardCandy = 3
                },
                // **박사(마을 어르신)에게 먼저 간다.** 예전엔 조작을 배우자마자 "곤충을 잡아라"였고
                // 어르신 대화는 그걸 **끝낸 뒤에야** 열렸다 — 맨손으로 혼자 잡아낸 다음에 인사를
                // 받는 순서라 이야기가 뒤에서 따라왔다. 이제 첫 파트너를 받고 그걸로 배운다.
                // 곤충 지급은 이 퀘스트가 아니라 `ch1_intro` 비트의 보상이 한다 — 대사 안에서
                // 건네받아야 "받았다"는 감각이 생기고, 지급 지점이 둘로 갈리지 않는다.
                new TutorialQuest
                {
                    questId = "q_talk_elder", title = "마을 어르신을 만나다",
                    description = "마을 어르신이 당신을 기다리고 있습니다 — 찾아가 이야기를 들으세요",
                    hint = "마을 어르신에게 다가가면 먼저 다가와 인사합니다. [E]로 대화하세요",
                    type = QuestType.TalkToElder, targetCount = 1,
                    prerequisiteQuestId = "q_move",
                    rewardCandy = 3
                },
                new TutorialQuest
                {
                    questId = "q_approach", title = "첫 곤충 포획!",
                    description = "사라져가는 곤충을 만나 기록하세요 — 포획이 곧 그 생명을 붙드는 일입니다",
                    hint = "풀밭에서 움직이는 곤충에게 다가가 포획 미니게임을 완료하세요",
                    type = QuestType.Capture, targetCount = 1,
                    prerequisiteQuestId = "q_talk_elder",
                    rewardCandy = 5, rewardExp = 10
                },
                new TutorialQuest
                {
                    questId = "q_collection", title = "컬렉션 확인",
                    description = "C키로 보유 곤충을 확인해보세요",
                    hint = "C키를 눌러 컬렉션 화면을 열어보세요",
                    type = QuestType.ViewCollection, targetCount = 1,
                    rewardExp = 5
                },
                new TutorialQuest
                {
                    questId = "q_dex", title = "도감 열기",
                    description = "D키로 도감을 열어 발견한 곤충을 확인하세요",
                    hint = "D키를 눌러 도감 화면을 열어보세요",
                    type = QuestType.OpenDex, targetCount = 1,
                    rewardExp = 5
                },
                new TutorialQuest
                {
                    questId = "q_capture3", title = "곤충 수집가",
                    description = "곤충을 3마리 포획하세요 — 기록이 쌓일수록 사라짐을 늦출 수 있습니다",
                    hint = "풀밭을 돌아다니며 다양한 곤충을 잡아보세요",
                    type = QuestType.Capture, targetCount = 3,
                    prerequisiteQuestId = "q_approach",
                    rewardCandy = 10
                },
                new TutorialQuest
                {
                    questId = "q_levelup", title = "첫 레벨업!",
                    description = "컬렉션에서 캔디로 곤충을 레벨업하세요",
                    hint = "컬렉션에서 곤충을 선택하고 레벨업 버튼을 누르세요",
                    type = QuestType.LevelUp, targetCount = 1,
                    prerequisiteQuestId = "q_capture3",
                    rewardCandy = 8, rewardExp = 15
                },
                new TutorialQuest
                {
                    questId = "q_equip", title = "스킬 장착",
                    description = "훈련 메뉴에서 곤충에게 스킬을 장착하세요",
                    hint = "훈련 메뉴를 열고 스킬 장착 탭을 확인하세요",
                    type = QuestType.EquipSkill, targetCount = 1,
                    prerequisiteQuestId = "q_levelup",
                    rewardExp = 10
                },
                new TutorialQuest
                {
                    questId = "q_battle", title = "첫 전투!",
                    description = "야생 곤충과 전투해서 승리하세요",
                    hint = "야생 곤충에게 다가가 전투를 시작하세요",
                    type = QuestType.Battle, targetCount = 1,
                    prerequisiteQuestId = "q_equip",
                    rewardCandy = 10, rewardExp = 20,
                    rewardItemId = "exp_boost", rewardItemCount = 1
                },
                new TutorialQuest
                {
                    questId = "q_item", title = "아이템 활용",
                    description = "아이템을 사용해보세요 (채집망 등)",
                    hint = "인벤토리에서 아이템을 선택해 사용하세요",
                    type = QuestType.UseItem, targetCount = 1,
                    prerequisiteQuestId = "q_battle",
                    rewardCandy = 5,
                    rewardItemId = "net_silver", rewardItemCount = 2
                },
                new TutorialQuest
                {
                    questId = "q_training", title = "훈련 시작!",
                    description = "훈련 메뉴에서 곤충을 훈련시키세요",
                    hint = "훈련 메뉴를 열고 훈련 방법을 선택하세요",
                    type = QuestType.Training, targetCount = 1,
                    prerequisiteQuestId = "q_battle",
                    rewardExp = 15
                },
                new TutorialQuest
                {
                    questId = "q_team", title = "팀 편성",
                    description = "전투 팀에 곤충을 배치하세요",
                    hint = "팀 편성 화면에서 슬롯에 곤충을 배치하세요",
                    type = QuestType.SetTeam, targetCount = 1,
                    prerequisiteQuestId = "q_training",
                    rewardCandy = 10
                },
                new TutorialQuest
                {
                    questId = "q_battle3", title = "전투의 달인",
                    description = "전투에서 3번 승리하세요",
                    hint = "야생 곤충들과 전투를 반복하세요",
                    type = QuestType.Battle, targetCount = 3,
                    prerequisiteQuestId = "q_team",
                    rewardCandy = 15, rewardExp = 30
                },
                new TutorialQuest
                {
                    questId = "q_capture_rare", title = "희귀종 발견!",
                    description = "Uncommon 이상 등급 곤충을 포획하세요",
                    hint = "특별한 색상이나 효과를 가진 곤충을 찾아보세요",
                    type = QuestType.CaptureRare, targetCount = 1,
                    prerequisiteQuestId = "q_battle3",
                    rewardCandy = 20, rewardExp = 25,
                    rewardItemId = "net_gold", rewardItemCount = 1
                },
                new TutorialQuest
                {
                    questId = "q_guardian1", title = "수호자 도전!",
                    description = "사라짐에 동요한 초원의 수문장을 넘어 새 지역으로 나아가세요",
                    hint = "초원 경계 근처에서 수문장을 찾아 전투하세요",
                    type = QuestType.DefeatGuardian, targetCount = 1,
                    prerequisiteQuestId = "q_capture_rare",
                    rewardCandy = 30, rewardExp = 50
                },
                new TutorialQuest
                {
                    questId = "q_visit_pond", title = "새 세계: 연못",
                    description = "연못 지역에 들어가보세요",
                    hint = "수문장을 물리친 뒤 연못 방향으로 이동하세요",
                    type = QuestType.VisitRegion, targetCount = 1,
                    prerequisiteQuestId = "q_guardian1",
                    rewardCandy = 10, rewardExp = 20
                },
                new TutorialQuest
                {
                    questId = "q_subarea", title = "숨겨진 장소",
                    description = "서브구역(동굴, 갈대밭 등)을 탐험하세요",
                    hint = "연못 주변의 특별한 장소를 찾아보세요",
                    type = QuestType.VisitSubArea, targetCount = 1,
                    prerequisiteQuestId = "q_visit_pond",
                    rewardCandy = 15, rewardExp = 20
                },
                new TutorialQuest
                {
                    questId = "q_raid", title = "레이드 도전!",
                    description = "Epic 이상 곤충에게 레이드 전투를 시도하세요",
                    hint = "강력한 보스 곤충을 찾아 레이드를 시작하세요",
                    type = QuestType.RaidBattle, targetCount = 1,
                    prerequisiteQuestId = "q_subarea",
                    rewardCandy = 25, rewardExp = 40
                },
                new TutorialQuest
                {
                    questId = "q_capture10", title = "곤충 박사",
                    description = "총 10마리의 곤충을 포획하세요",
                    hint = "다양한 지역을 탐험하며 곤충을 모으세요",
                    type = QuestType.Capture, targetCount = 10,
                    prerequisiteQuestId = "q_raid",
                    rewardCandy = 30, rewardExp = 50,
                    rewardItemId = "binding_net", rewardItemCount = 1
                },
                new TutorialQuest
                {
                    questId = "q_battle10", title = "전투 마스터",
                    description = "전투에서 10번 승리하세요",
                    hint = "다양한 야생 곤충들과 전투하세요",
                    type = QuestType.Battle, targetCount = 10,
                    prerequisiteQuestId = "q_capture10",
                    rewardCandy = 40, rewardExp = 60,
                    rewardItemId = "beast_mark", rewardItemCount = 1
                },
                new TutorialQuest
                {
                    questId = "q_complete", title = "모험의 시작",
                    description = "축하합니다! 이제 자유롭게 모험하세요!",
                    hint = "월드를 자유롭게 탐험하세요",
                    type = QuestType.Movement, targetCount = 1,
                    prerequisiteQuestId = "q_battle10",
                    rewardCandy = 100, rewardExp = 100,
                    rewardItemId = "spirit_blessing", rewardItemCount = 1
                },

                // --- 서브 퀘스트(다중 활성, 반복 시 목표 상승) — category=Side ---
                new TutorialQuest
                {
                    questId = "s_capture_wild", title = "야생 곤충 수집",
                    description = "야생 곤충을 포획하세요. 달성할수록 다음 목표가 늘어납니다.",
                    hint = "필드에서 곤충을 계속 포획하세요",
                    type = QuestType.Capture, targetCount = 5, targetIncrement = 5,
                    category = QuestCategory.Side, repeatable = true,
                    prerequisiteQuestId = "q_capture3",
                    rewardCandy = 15, rewardExp = 10
                },
                new TutorialQuest
                {
                    questId = "s_battle_win", title = "전투 단련",
                    description = "배틀에서 승리하세요. 반복할수록 목표가 상승합니다.",
                    hint = "야생 곤충에게 배틀을 걸어 이기세요",
                    type = QuestType.Battle, targetCount = 3, targetIncrement = 3,
                    category = QuestCategory.Side, repeatable = true,
                    prerequisiteQuestId = "q_battle",
                    rewardCandy = 20, rewardExp = 15
                },
                new TutorialQuest
                {
                    questId = "s_raid_win", title = "레이드 도전자",
                    description = "레이드에서 승리하세요. 반복할수록 목표가 상승합니다.",
                    hint = "Epic/Legendary 곤충에게 레이드를 도전하세요",
                    type = QuestType.RaidBattle, targetCount = 1, targetIncrement = 1,
                    category = QuestCategory.Side, repeatable = true,
                    prerequisiteQuestId = "q_raid",
                    rewardCandy = 40, rewardExp = 30,
                    rewardItemId = "guardian_totem", rewardItemCount = 1
                },
                new TutorialQuest
                {
                    questId = "s_npc_duel", title = "동네 최강자",
                    description = "곤충잡이 아이와의 대결에서 승리하세요. 반복할수록 목표가 상승합니다.",
                    hint = "필드를 돌아다니는 아이에게 [E]로 대결을 신청하세요",
                    type = QuestType.NpcDuel, targetCount = 3, targetIncrement = 2,
                    category = QuestCategory.Side, repeatable = true,
                    prerequisiteQuestId = "q_battle",
                    rewardCandy = 25, rewardExp = 20,
                    rewardItemId = "wound_salve", rewardItemCount = 3
                },

                // --- 등급 패키지 포획(각 등급을 콕 집는다) — QuestType.CaptureRarity ---
                new TutorialQuest
                {
                    // 제목·설명은 틀이다 — TutorialQuestUI가 이번 주 대상 종 이름으로 덮어쓴다.
                    questId = "s_weekly_contest", title = "주간 크기 대결",
                    description = "이번 주 지정 곤충을 큰 개체로 포획하세요.",
                    hint = "같은 종이라도 개체마다 크기가 다릅니다 — 여러 마리 잡아 보세요",
                    type = QuestType.SizeContest, targetCount = 1, targetIncrement = 1,
                    category = QuestCategory.Side, repeatable = true,
                    prerequisiteQuestId = "q_capture3",
                    rewardCandy = 30, rewardExp = 25,
                    rewardItemId = "net_silver", rewardItemCount = 2
                },
                new TutorialQuest
                {
                    questId = "s_pack_common", title = "일반 곤충 채집단",
                    description = "일반(Common) 등급 곤충을 포획하세요. 반복할수록 목표가 상승합니다.",
                    hint = "어느 리전에서나 흔하게 만날 수 있습니다",
                    type = QuestType.CaptureRarity, requiredRarity = InsectRarity.Common,
                    targetCount = 8, targetIncrement = 6,
                    category = QuestCategory.Side, repeatable = true,
                    prerequisiteQuestId = "q_capture3",
                    rewardCandy = 12, rewardExp = 8,
                    rewardItemId = "net_basic", rewardItemCount = 3
                },
                new TutorialQuest
                {
                    questId = "s_pack_uncommon", title = "고급 곤충 채집단",
                    description = "고급(Uncommon) 등급 곤충을 포획하세요. 반복할수록 목표가 상승합니다.",
                    hint = "은빛 채집망을 쓰면 성공률이 오릅니다",
                    type = QuestType.CaptureRarity, requiredRarity = InsectRarity.Uncommon,
                    targetCount = 5, targetIncrement = 4,
                    category = QuestCategory.Side, repeatable = true,
                    prerequisiteQuestId = "q_capture3",
                    rewardCandy = 20, rewardExp = 15,
                    rewardItemId = "net_silver", rewardItemCount = 2
                },
                new TutorialQuest
                {
                    questId = "s_pack_rare", title = "희귀 곤충 채집단",
                    description = "희귀(Rare) 등급 곤충을 포획하세요. 반복할수록 목표가 상승합니다.",
                    hint = "황금 채집망과 포박의 그물을 함께 쓰세요",
                    type = QuestType.CaptureRarity, requiredRarity = InsectRarity.Rare,
                    targetCount = 3, targetIncrement = 2,
                    category = QuestCategory.Side, repeatable = true,
                    prerequisiteQuestId = "q_capture10",
                    rewardCandy = 35, rewardExp = 28,
                    rewardItemId = "net_gold", rewardItemCount = 2
                },
                new TutorialQuest
                {
                    questId = "s_pack_epic", title = "영웅 곤충 채집단",
                    description = "영웅(Epic) 등급 곤충을 포획하세요. 반복할수록 목표가 상승합니다.",
                    hint = "레이드로 약화시킨 뒤 포획하면 수월합니다",
                    type = QuestType.CaptureRarity, requiredRarity = InsectRarity.Epic,
                    targetCount = 2, targetIncrement = 1,
                    category = QuestCategory.Side, repeatable = true,
                    prerequisiteQuestId = "q_capture10",
                    rewardCandy = 60, rewardExp = 50,
                    rewardItemId = "golden_censer", rewardItemCount = 1
                },
                new TutorialQuest
                {
                    questId = "s_pack_legendary", title = "전설 곤충 채집단",
                    description = "전설(Legendary) 등급 곤충을 포획하세요. 반복할수록 목표가 상승합니다.",
                    hint = "최고 난도입니다 — 포획 보정 아이템을 모두 준비하세요",
                    type = QuestType.CaptureRarity, requiredRarity = InsectRarity.Legendary,
                    targetCount = 1, targetIncrement = 1,
                    category = QuestCategory.Side, repeatable = true,
                    prerequisiteQuestId = "q_capture10",
                    rewardCandy = 120, rewardExp = 100,
                    rewardItemId = "spirit_blessing", rewardItemCount = 1
                },
            };
        }

        // --- 이벤트 구독 ---

        private void SubscribeEvents()
        {
            if (battleController != null)
                battleController.BattleEnded += OnBattleEnded;

            if (raidController != null)
                raidController.RaidEnded += OnRaidEnded;

            if (regionManager != null)
            {
                regionManager.RegionChanged += OnRegionChanged;
                regionManager.SubAreaChanged += OnSubAreaChanged;
            }

            // 팀 편성 변경 (q_team) — 옛은 NotifyTeamSet 호출처 없어 영구 정지
            if (battleTeamManager != null)
                battleTeamManager.TeamChanged += OnTeamChanged;

            // 주간 크기 대결 등급 달성. AutoWire(WeeklyContestManager)에서도 구독하므로
            // 배선 순서와 무관하게 정확히 한 번만 걸리도록 해지 후 구독한다.
            if (weeklyContest != null)
            {
                weeklyContest.TierReached -= OnContestTierReached;
                weeklyContest.TierReached += OnContestTierReached;
            }
        }

        private void UnsubscribeEvents()
        {
            if (battleController != null)
                battleController.BattleEnded -= OnBattleEnded;

            if (raidController != null)
                raidController.RaidEnded -= OnRaidEnded;

            if (regionManager != null)
            {
                regionManager.RegionChanged -= OnRegionChanged;
                regionManager.SubAreaChanged -= OnSubAreaChanged;
            }

            if (battleTeamManager != null)
                battleTeamManager.TeamChanged -= OnTeamChanged;

            if (weeklyContest != null)
                weeklyContest.TierReached -= OnContestTierReached;
        }

        private void OnTeamChanged() => NotifyAction(QuestType.SetTeam);

        // 등급을 새로 달성했을 때만 울린다(WeeklyContestManager가 중복 발화를 막는다).
        // 등급별 추가 보상은 여기서 지급한다 — 퀘스트 보상(고정)과 별개로 동/은/금 차등을 준다.
        private void OnContestTierReached(ContestTier tier)
        {
            if (weeklyContest != null && weeklyContest.TryClaim(out ContestTier claimed))
                GrantContestTierReward(claimed);
            // NotifySizeContestTier()를 거치지 않고 직접 부른다 — quest_lint의 배선 검사가
            // "핸들러 본문에 NotifyAction(QuestType.X)"를 찾는다(OnTeamChanged와 같은 형태).
            // 한 겹 감싸면 배선이 안 보여 q_team류 영구 정지로 오판된다.
            NotifyAction(QuestType.SizeContest);
        }

        // 동/은/금 차등 보상. TryClaim이 주차·등급 중복을 막으므로 여기선 지급만 한다.
        private void GrantContestTierReward(ContestTier tier)
        {
            int candy;
            string itemId;
            int itemCount;
            switch (tier)
            {
                case ContestTier.Gold: candy = 80; itemId = "net_gold"; itemCount = 2; break;
                case ContestTier.Silver: candy = 40; itemId = "net_silver"; itemCount = 1; break;
                case ContestTier.Bronze: candy = 20; itemId = null; itemCount = 0; break;
                default: return;
            }

            if (candyInventory != null) candyInventory.AddCandy(candy);
            else Debug.LogWarning($"[Quest] candyInventory null — 주간 대결 캔디 손실 (+{candy})");

            if (!string.IsNullOrEmpty(itemId) && itemCount > 0)
            {
                if (itemInventory != null) itemInventory.AddItem(itemId, itemCount);
                else Debug.LogWarning($"[Quest] itemInventory null — 주간 대결 아이템 손실: {itemId}x{itemCount}");
            }
        }

        // --- 이벤트 핸들러 ---

        private void OnBattleEnded(bool playerWon)
        {
            if (playerWon)
            {
                NotifyAction(QuestType.Battle);
            }
        }

        private void OnRaidEnded(bool playerWon)
        {
            if (playerWon)
            {
                NotifyAction(QuestType.RaidBattle);
            }
        }

        private void OnRegionChanged(RegionData region)
        {
            if (region != null)
            {
                NotifyAction(QuestType.VisitRegion);
            }
        }

        private void OnSubAreaChanged(SubAreaData subArea)
        {
            if (subArea != null)
            {
                NotifyAction(QuestType.VisitSubArea);
            }
        }

        // --- Update: 이동 감지 ---

        private void Update()
        {
            if (!tutorialSessionStarted) return;
            if (ActiveQuest == null) return;

            // 이동 퀘스트가 아닐 땐 위치 추적 자체가 불필요 — Find/거리계산 스킵.
            if (ActiveQuest.type != QuestType.Movement) return;

            Transform player = PlayerTransform();
            if (player != null)
            {
                float dist = Vector3.Distance(player.position, lastPlayerPos);
                if (dist > 1f)
                {
                    IncrementProgress(activeQuestId);
                }
                lastPlayerPos = player.position;
            }
        }

        // --- 외부 알림 메서드 ---

        public void NotifyAction(QuestType type, int count = 1)
        {
            if (!tutorialSessionStarted) return;
            if (ActiveQuest != null && ActiveQuest.type == type)
                IncrementProgress(activeQuestId, count);
            ProgressSideQuests(type, count);   // 서브 퀘스트(다중 활성)도 함께 진행
        }

        /// <summary>
        /// 마을 어르신(박사)과 첫 대화 — <c>WorldInteractionController</c>가 스토리 NPC 대화 시 부른다.
        ///
        /// <b>이벤트 구독이 아니라 직접 호출이다.</b> 이 저장소는 이벤트 기반 QuestType이
        /// <c>SubscribeEvents</c> 등록 누락으로 영구 정지한 전례가 있어(q_team), 진행에 필수인
        /// 통지는 발생 지점에서 직접 부르는 쪽을 택했다.
        /// </summary>
        public void NotifyTalkToElder()
        {
            NotifyAction(QuestType.TalkToElder);
        }

        public void NotifyCapture(InsectRarity rarity)
        {
            if (!tutorialSessionStarted) return;

            if (ActiveQuest != null)
            {
                if (ActiveQuest.type == QuestType.Capture)
                    IncrementProgress(activeQuestId);
                else if (ActiveQuest.type == QuestType.CaptureRare && rarity >= InsectRarity.Uncommon)
                    IncrementProgress(activeQuestId);
                else if (ActiveQuest.type == QuestType.CaptureRarity && rarity == ActiveQuest.requiredRarity)
                    IncrementProgress(activeQuestId);
            }

            // 서브 포획 퀘스트: Capture는 모든 포획, CaptureRare는 Uncommon+, CaptureRarity는 지정 등급만.
            ProgressSideCapture(rarity);
        }

        public void NotifyBattleWon()
        {
            NotifyAction(QuestType.Battle);
        }

        public void NotifyRaidCompleted()
        {
            NotifyAction(QuestType.RaidBattle);
        }

        public void NotifyLevelUp()
        {
            NotifyAction(QuestType.LevelUp);
        }

        public void NotifyItemUsed()
        {
            NotifyAction(QuestType.UseItem);
        }

        public void NotifyTraining()
        {
            NotifyAction(QuestType.Training);
        }

        public void NotifyTeamSet()
        {
            NotifyAction(QuestType.SetTeam);
        }

        public void NotifyCollectionOpened()
        {
            NotifyAction(QuestType.ViewCollection);
        }

        public void NotifyDexOpened()
        {
            NotifyAction(QuestType.OpenDex);
        }

        public void NotifySkillEquipped()
        {
            NotifyAction(QuestType.EquipSkill);
        }

        public void NotifyGuardianDefeated()
        {
            NotifyAction(QuestType.DefeatGuardian);
        }

        /// <summary>곤충잡이 아이와의 대결에서 이겼을 때 — NpcDuelController가 호출.</summary>
        public void NotifyNpcDuelWon()
        {
            NotifyAction(QuestType.NpcDuel);
        }

        /// <summary>주간 크기 대결에서 등급을 새로 달성했을 때 — WeeklyContestManager 이벤트가 호출.</summary>
        public void NotifySizeContestTier()
        {
            NotifyAction(QuestType.SizeContest);
        }

        // --- 진행 추적 ---

        private void IncrementProgress(string questId, int amount = 1)
        {
            if (completedQuests.Contains(questId)) return;

            if (!questProgress.ContainsKey(questId))
                questProgress[questId] = 0;
            questProgress[questId] += amount;

            TutorialQuest quest = GetQuest(questId);
            if (quest == null) return;

            QuestProgressUpdated?.Invoke(quest, questProgress[questId], quest.targetCount);

            if (questProgress[questId] >= quest.targetCount)
            {
                CompleteQuest(questId);
            }
            else
            {
                SaveProgress();
            }
        }

        private void CompleteQuest(string questId)
        {
            completedQuests.Add(questId);
            TutorialQuest quest = GetQuest(questId);
            if (quest == null) return;

            GrantRewards(quest);

            // 완료했지만 아직 퀘스트 창에서 안 본 목록에 추가 → 퀵바 배지 +1. 창을 열면 MarkQuestsSeen로 비운다.
            unseenCompleted.Add(questId);

            QuestCompleted?.Invoke(quest);
            SaveProgress();
            // 퀘스트 완료 보상은 캔디/XP/아이템/곤충 → 클라우드 즉시 동기 (다른 기기 진입 시 재진행 방지).
            // IncrementProgress의 잦은 호출은 120초 자동저장에 맡겨 API 폭주 차단.
            if (CloudSaveManager.Instance != null) CloudSaveManager.Instance.SaveToCloud();
            ActivateNextQuest();
        }

        // 보상 지급(캔디/XP/아이템/곤충 + 팀 비었으면 스타터 1슬롯 배치). 스토리·서브 퀘스트 공용.
        private void GrantRewards(TutorialQuest quest)
        {
            if (quest == null) return;

            if (quest.rewardCandy > 0)
            {
                if (candyInventory != null) candyInventory.AddCandy(quest.rewardCandy);
                else Debug.LogWarning($"[Quest] candyInventory null — 캔디 보상 손실: {quest.questId} (+{quest.rewardCandy})");
            }

            if (quest.rewardExp > 0)
            {
                if (progressController != null) progressController.GainXp(quest.rewardExp);
                else Debug.LogWarning($"[Quest] progressController null — XP 보상 손실: {quest.questId} (+{quest.rewardExp})");
            }

            if (!string.IsNullOrEmpty(quest.rewardItemId) && quest.rewardItemCount > 0)
            {
                if (itemInventory != null) itemInventory.AddItem(quest.rewardItemId, quest.rewardItemCount);
                else Debug.LogWarning($"[Quest] itemInventory null — 아이템 보상 손실: {quest.questId} {quest.rewardItemId}x{quest.rewardItemCount}");
            }

            if (!string.IsNullOrEmpty(quest.rewardInsectId))
            {
                if (insectCollection != null)
                {
                    insectCollection.AddCapturedInsect(
                        quest.rewardInsectId,
                        Mathf.Max(1, quest.rewardInsectLevel));

                    // **도감 등록을 빠뜨리면 안 된다.** 보상 곤충은 소유·출전까지 하는데 도감에는
                    // 영원히 미발견으로 남아 100% 완주가 불가능해진다. 게다가 `CapturedSpeciesCount`가
                    // 스토리 DexProgress 트리거의 판정값이라(`StoryDirector`) 전 플레이어의 진행이
                    // 한 종만큼 늦게 열린다. `dexController`는 오래 배선만 돼 있고 한 번도 읽히지
                    // 않는 죽은 필드였다 — 포획(`CaptureController`)·가챠(`GachaBoxManager`)와 같은 형태로 맞춘다.
                    if (dexController != null)
                    {
                        dexController.RegisterEncounter(quest.rewardInsectId);
                        dexController.RegisterCapture(quest.rewardInsectId);
                    }

                    // 첫 곤충의 1번 슬롯 자동 배치는 여기 없다 — `BattleTeamManager`가
                    // `InsectCaptured`를 구독해 지급 경로 전체에 대해 한 번에 처리한다.
                    // 여기 두면 퀘스트 보상 경로에만 걸리는데, 실제로 그래서 첫 파트너가
                    // 스토리 비트 보상으로 옮겨간 순간 이 코드가 죽고 팀이 영원히 비었다.
                }
                else
                {
                    Debug.LogWarning($"[Quest] insectCollection null — 곤충 보상 손실: {quest.questId} {quest.rewardInsectId}");
                }
            }
        }

        // --- 서브 퀘스트(다중 활성 + 반복 상승) ---

        // 해금(prereq 완료)됐고, 반복이거나 아직 미완료면 활성.
        private bool IsSideActive(TutorialQuest q)
        {
            if (q == null || q.category != QuestCategory.Side) return false;
            if (!string.IsNullOrEmpty(q.prerequisiteQuestId) && !completedQuests.Contains(q.prerequisiteQuestId))
                return false;
            if (!q.repeatable && completedQuests.Contains(q.questId)) return false;
            return true;
        }

        // 유효 목표 = 기본 + (반복 완료 횟수 × 증가량). 반복 아니면 기본 그대로.
        public int EffectiveTarget(TutorialQuest q)
        {
            if (q == null) return 0;
            if (q.category == QuestCategory.Side && q.repeatable)
                return q.targetCount + GetSideRepeatCount(q.questId) * Mathf.Max(0, q.targetIncrement);
            return q.targetCount;
        }

        public int GetSideProgress(string questId)
            => sideProgress.TryGetValue(questId, out int v) ? v : 0;

        public int GetSideRepeatCount(string questId)
            => sideRepeatCount.TryGetValue(questId, out int v) ? v : 0;

        // 활성 서브 퀘스트(UI 나열용).
        public IEnumerable<TutorialQuest> ActiveSideQuests()
        {
            if (allQuests == null) yield break;
            foreach (TutorialQuest q in allQuests)
                if (IsSideActive(q)) yield return q;
        }

        private void ProgressSideQuests(QuestType type, int count)
        {
            if (allQuests == null) return;
            foreach (TutorialQuest q in allQuests)
            {
                if (q.type != type || !IsSideActive(q)) continue;
                IncrementSideProgress(q, count);
            }
        }

        private void ProgressSideCapture(InsectRarity rarity)
        {
            if (allQuests == null) return;
            foreach (TutorialQuest q in allQuests)
            {
                if (!IsSideActive(q)) continue;
                if (q.type == QuestType.Capture) IncrementSideProgress(q, 1);
                else if (q.type == QuestType.CaptureRare && rarity >= InsectRarity.Uncommon) IncrementSideProgress(q, 1);
                else if (q.type == QuestType.CaptureRarity && rarity == q.requiredRarity) IncrementSideProgress(q, 1);
            }
        }

        private void IncrementSideProgress(TutorialQuest q, int amount)
        {
            int cur = GetSideProgress(q.questId) + amount;
            sideProgress[q.questId] = cur;
            int target = EffectiveTarget(q);
            QuestProgressUpdated?.Invoke(q, cur, target);
            if (cur >= target) CompleteSideQuest(q);
            else SaveProgress();
        }

        private void CompleteSideQuest(TutorialQuest q)
        {
            GrantRewards(q);
            if (q.repeatable)
            {
                sideRepeatCount[q.questId] = GetSideRepeatCount(q.questId) + 1;
                sideProgress[q.questId] = 0;   // 다음 티어로 리셋 → 목표 상승
            }
            else
            {
                completedQuests.Add(q.questId);
                sideProgress.Remove(q.questId);
            }
            unseenCompleted.Add(q.questId);
            QuestCompleted?.Invoke(q);
            SaveProgress();
            // 반복 서브는 잦아 즉시 클라우드 PATCH를 생략(120s 오토세이브에 위임 — API 폭주 차단).
            // 비반복 서브(영구 완료)만 즉시 동기(다른 기기 재획득 방지).
            if (!q.repeatable && CloudSaveManager.Instance != null) CloudSaveManager.Instance.SaveToCloud();
            // ActivateNextQuest 호출 안 함 — 서브는 스토리 체인과 무관.
        }

        private void ActivateNextQuest()
        {
            if (allQuests == null) return;

            foreach (TutorialQuest quest in allQuests)
            {
                if (quest.category != QuestCategory.Story) continue;   // 서브는 선형 체인 제외
                if (completedQuests.Contains(quest.questId)) continue;

                if (!string.IsNullOrEmpty(quest.prerequisiteQuestId)
                    && !completedQuests.Contains(quest.prerequisiteQuestId))
                    continue;

                activeQuestId = quest.questId;
                ActiveQuest = quest;
                SaveProgress();
                // 이미 충족된 DefeatGuardian이면 자동완료(CompleteQuest가 다음 퀘스트를 활성화).
                if (ReconcileActiveGuardianQuest()) return;
                QuestActivated?.Invoke(quest);
                return;
            }

            activeQuestId = null;
            ActiveQuest = null;
        }

        // 선격파 정합: DefeatGuardian 퀘스트가 활성인데 이미 수문장이 격파돼 있으면(퀘스트 활성 전에 격파해
        // NotifyGuardianDefeated가 ActiveQuest.type 불일치로 no-op됐던 경우 — 재격파 불가로 영구정지) 즉시
        // 자동 완료한다. 반환: 자동 완료했으면 true(호출부가 QuestActivated 중복 발화를 피하게).
        private bool ReconcileActiveGuardianQuest()
        {
            if (ActiveQuest == null || ActiveQuest.type != QuestType.DefeatGuardian) return false;
            if (completedQuests.Contains(ActiveQuest.questId)) return false;
            if (!AnyGuardianDefeated()) return false;
            CompleteQuest(ActiveQuest.questId);
            return true;
        }

        private bool AnyGuardianDefeated()
        {
            if (regionManager == null || regionManager.Regions == null) return false;
            foreach (var region in regionManager.Regions)
            {
                if (string.IsNullOrEmpty(region.guardianInsectId)) continue;
                if (regionManager.IsGuardianDefeated(region.regionId)) return true;
            }
            return false;
        }

        // 클라우드 로드 후 PlayerPrefs(퀘스트 진행/완료/활성)를 다시 읽어 인메모리 갱신.
        // 이벤트 재구독은 안 함(Start에서 이미 구독). 활성 퀘스트가 비면 다음 퀘스트 재선정.
        public void ReloadFromDisk()
        {
            LoadProgress();
            ActiveQuest = GetQuest(activeQuestId);

            if (!tutorialSessionStarted) return;

            if (ActiveQuest == null || completedQuests.Contains(activeQuestId))
                ActivateNextQuest();
            else if (!ReconcileActiveGuardianQuest()) // 스톨된 가디언 퀘스트면 자동완료
                QuestActivated?.Invoke(ActiveQuest);
        }

        public void BeginTutorialForCurrentAccount()
        {
            if (tutorialSessionStarted) return;

            tutorialSessionStarted = true;
            LoadProgress();
            ActiveQuest = GetQuest(activeQuestId);

            if (ActiveQuest == null || completedQuests.Contains(activeQuestId))
            {
                ActivateNextQuest();
            }
            else if (!ReconcileActiveGuardianQuest()) // 스톨된 가디언 퀘스트면 자동완료
            {
                QuestActivated?.Invoke(ActiveQuest);
            }

            Transform player = PlayerTransform();
            if (player != null) lastPlayerPos = player.position;
        }

        public void ResetForNewAccount()
        {
            tutorialSessionStarted = false;
            questProgress.Clear();
            completedQuests.Clear();
            unseenCompleted.Clear();
            sideProgress.Clear();
            sideRepeatCount.Clear();
            activeQuestId = null;
            ActiveQuest = null;

            PlayerPrefs.DeleteKey(ProgressKey);
            PlayerPrefs.DeleteKey(CompletedKey);
            PlayerPrefs.DeleteKey(ActiveKey);
            PlayerPrefs.DeleteKey(UnseenKey);
            PlayerPrefs.DeleteKey(SideProgressKey);
            PlayerPrefs.DeleteKey(SideRepeatKey);
            PlayerPrefs.DeleteKey(GameConstants.PrefsKeys.TutorialHidden);

            if (AuthManager.Instance != null && !string.IsNullOrEmpty(AuthManager.Instance.UserId))
            {
                PlayerPrefs.DeleteKey(
                    GameConstants.PrefsKeys.TutorialHidden + "." + AuthManager.Instance.UserId);
            }

            PlayerPrefs.Save();
        }

        // 개발/테스트용: 튜토리얼을 처음 상태로 되돌린다(로컬 + 클라우드).
        // 마스터 계정 등에서 모든 튜토리얼이 이미 완료되어 재테스트가 불가능할 때 사용.
        public void RestartTutorialForTesting()
        {
            ResetForNewAccount();              // 로컬 PlayerPrefs + 인메모리 상태 초기화 (session=false)
            BeginTutorialForCurrentAccount();  // 첫 퀘스트(q_approach) 재활성화
            // 비운 상태를 즉시 클라우드에 푸시 — 재로그인/재동기화로 완료상태 복원 방지.
            if (CloudSaveManager.Instance != null) CloudSaveManager.Instance.SaveToCloud();
            Debug.Log("[TutorialQuestManager] 튜토리얼 진행을 초기화했습니다(테스트용).");
        }

        public TutorialQuest[] GetAllQuests()
        {
            return allQuests;
        }

        public bool IsQuestCompleted(string questId)
        {
            return completedQuests.Contains(questId);
        }

        /// <summary>
        /// 퀘스트 표시 제목(없으면 null). 스토리 목표 행이 <c>QuestComplete</c> 비트를
        /// "'첫 수문장' 완료하기"로 풀어 쓰는 데 쓴다 — 예전엔 questId를 못 읽어
        /// "모험을 이어가세요"로 떨어졌다. 읽기 전용이라 진행에 영향이 없다.
        /// </summary>
        public string GetQuestTitle(string questId)
        {
            TutorialQuest quest = GetQuest(questId);
            return quest != null ? quest.title : null;
        }

        // 사용자가 퀘스트 창을 열어 완료 목록을 확인했을 때 호출 — 미확인 완료 배지를 0으로 리셋.
        public void MarkQuestsSeen()
        {
            if (unseenCompleted.Count == 0) return;
            unseenCompleted.Clear();
            SaveProgress();
        }

        private TutorialQuest GetQuest(string questId)
        {
            if (allQuests == null || string.IsNullOrEmpty(questId)) return null;

            foreach (TutorialQuest quest in allQuests)
            {
                if (quest.questId == questId)
                    return quest;
            }
            return null;
        }

        // --- 저장/로드 ---

        private void SaveProgress()
        {
            // 진행도 저장
            List<string> progressEntries = new List<string>();
            foreach (var kvp in questProgress)
            {
                progressEntries.Add(kvp.Key + ":" + kvp.Value);
            }
            PlayerPrefs.SetString(ProgressKey, string.Join(",", progressEntries));

            // 완료 퀘스트 저장
            List<string> completedList = new List<string>(completedQuests);
            PlayerPrefs.SetString(CompletedKey, string.Join(",", completedList));

            // 현재 활성 퀘스트 저장
            PlayerPrefs.SetString(ActiveKey, activeQuestId ?? "");

            // 미확인 완료 목록 저장 — 앱 재시작 후에도 배지 유지.
            PlayerPrefs.SetString(UnseenKey, string.Join(",", new List<string>(unseenCompleted)));

            // 서브 퀘스트 진행/반복횟수 저장(로컬 전용).
            PlayerPrefs.SetString(SideProgressKey, SerializeIntDict(sideProgress));
            PlayerPrefs.SetString(SideRepeatKey, SerializeIntDict(sideRepeatCount));

            PlayerPrefs.Save();
        }

        private static string SerializeIntDict(Dictionary<string, int> dict)
        {
            List<string> entries = new List<string>();
            foreach (var kvp in dict) entries.Add(kvp.Key + ":" + kvp.Value);
            return string.Join(",", entries);
        }

        private static void ParseIntDict(string s, Dictionary<string, int> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (string entry in s.Split(','))
            {
                string[] parts = entry.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int v)) into[parts[0]] = v;
            }
        }

        private void LoadProgress()
        {
            questProgress.Clear();
            completedQuests.Clear();
            unseenCompleted.Clear();
            sideProgress.Clear();
            sideRepeatCount.Clear();

            // 진행도 로드
            string progressStr = PlayerPrefs.GetString(ProgressKey, "");
            if (!string.IsNullOrEmpty(progressStr))
            {
                string[] entries = progressStr.Split(',');
                foreach (string entry in entries)
                {
                    string[] parts = entry.Split(':');
                    if (parts.Length == 2)
                    {
                        int value;
                        if (int.TryParse(parts[1], out value))
                        {
                            questProgress[parts[0]] = value;
                        }
                    }
                }
            }

            // 완료 퀘스트 로드
            string completedStr = PlayerPrefs.GetString(CompletedKey, "");
            if (!string.IsNullOrEmpty(completedStr))
            {
                string[] ids = completedStr.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (string id in ids)
                {
                    completedQuests.Add(id);
                }
            }

            // 활성 퀘스트 로드
            activeQuestId = PlayerPrefs.GetString(ActiveKey, "");
            if (string.IsNullOrEmpty(activeQuestId))
            {
                activeQuestId = null;
                ActiveQuest = null;
            }

            // 미확인 완료 목록 로드 — 실제 완료된 것만 유효(완료 목록과 교차해 stale 방지).
            string unseenStr = PlayerPrefs.GetString(UnseenKey, "");
            if (!string.IsNullOrEmpty(unseenStr))
            {
                string[] unseenIds = unseenStr.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (string id in unseenIds)
                {
                    if (completedQuests.Contains(id))
                        unseenCompleted.Add(id);
                }
            }

            // 서브 퀘스트 진행/반복 로드(로컬 전용).
            ParseIntDict(PlayerPrefs.GetString(SideProgressKey, ""), sideProgress);
            ParseIntDict(PlayerPrefs.GetString(SideRepeatKey, ""), sideRepeatCount);

            // 배열 중간에 새로 끼운 퀘스트를 기존 세이브에 맞춰 정리한다. 세 로드 경로
            // (Start / BeginTutorialForCurrentAccount / ReloadFromDisk)가 전부 여기를 지난다.
            BackfillSkippedStoryQuests();
        }

        /// <summary>
        /// <b>배열 중간에 삽입된 퀘스트를 기존 세이브에서 소급 완료 처리한다.</b>
        /// 자기보다 <b>뒤</b>에 있는 스토리 퀘스트를 이미 깬 세이브라면, 그 사이에 끼워 넣은
        /// 퀘스트는 이미 지나간 단계다.
        ///
        /// 없으면 이미 진행한 유저가 <b>뒤로 되돌아간다</b> — <c>ActivateNextQuest</c>가 배열을
        /// 앞에서부터 훑어 첫 미완료를 고르기 때문이다. 실제로 <c>q_talk_elder</c>를 3번 자리에
        /// 끼우자 튜토리얼을 마친 세이브에서 "마을 어르신을 만나다"가 부활했다(완주 상태가 깨지고
        /// 튜토리얼 칩이 다시 뜬다). 진행이 막히진 않지만 진척이 되감긴 것으로 보인다.
        ///
        /// <b>보상은 주지 않는다.</b> 하지 않은 일에 대한 지급이고, 곤충 보상이 걸린 퀘스트라면
        /// 그대로 복제가 된다. 완료 표시만 남긴다.
        ///
        /// 판정이 성립하는 근거는 <b>완료 순서 = 배열 순서</b>다. <c>ActivateNextQuest</c>가
        /// 배열 앞에서부터 고르고, 모든 <c>prerequisiteQuestId</c>가 배열에서 자기보다 <b>앞</b>을
        /// 가리키므로(prereq 미충족으로 건너뛰는 항목이 없다) 뒤엣것이 완료됐다면 앞엣것도
        /// 완료됐어야 한다. <c>quest_lint</c> 검사 9가 그 전제를 고정한다 — 깨지면 이 소급이
        /// <b>아직 할 차례인 퀘스트를 건너뛴다</b>.
        ///
        /// 서브 퀘스트는 대상이 아니다(다중 활성이라 순서 개념이 없다).
        /// </summary>
        private void BackfillSkippedStoryQuests()
        {
            // 판정은 순수부(TutorialQuestOrder)가 한다 — 경계가 미묘해서 테스트로 고정했다.
            List<string> targets =
                TutorialQuestOrder.CollectBackfillTargets(allQuests, completedQuests.Contains);
            if (targets.Count == 0) return;

            for (int i = 0; i < targets.Count; i++)
            {
                completedQuests.Add(targets[i]);
                Debug.Log($"[Quest] 소급 완료: {targets[i]} — 뒤 퀘스트를 이미 깬 세이브라 지나간 단계다");
            }

            // 활성 퀘스트가 방금 완료 처리됐다면 비운다 — 호출부가 ActivateNextQuest로 재선정한다
            // (세 경로 모두 `ActiveQuest == null || completedQuests.Contains(activeQuestId)`를 본다).
            if (!string.IsNullOrEmpty(activeQuestId) && completedQuests.Contains(activeQuestId))
            {
                activeQuestId = null;
                ActiveQuest = null;
            }

            // **미확인 배지는 올리지 않는다.** 하지 않은 퀘스트의 완료 알림을 띄우면
            // 보상을 받은 것으로 오해한다(소급은 조용해야 한다).
            SaveProgress();
        }
    }
}
