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

        private TutorialQuest[] allQuests;
        private Dictionary<string, int> questProgress = new Dictionary<string, int>();
        private HashSet<string> completedQuests = new HashSet<string>();
        // 완료됐지만 아직 퀘스트 창(DrawDetailPanel)에서 확인 안 한 퀘스트 — 퀵바 배지 카운터 소스.
        private HashSet<string> unseenCompleted = new HashSet<string>();
        private string activeQuestId;
        private bool tutorialSessionStarted;

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
            get { return allQuests != null && completedQuests.Count >= allQuests.Length; }
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
        }

        private void Initialize()
        {
            allQuests = new TutorialQuest[]
            {
                new TutorialQuest
                {
                    questId = "q_approach", title = "첫 곤충 포획!",
                    description = "사라져가는 곤충을 만나 기록하세요 — 포획이 곧 그 생명을 붙드는 일입니다",
                    hint = "풀밭에서 움직이는 곤충에게 다가가 포획 미니게임을 완료하세요",
                    type = QuestType.Capture, targetCount = 1,
                    rewardCandy = 5, rewardExp = 10,
                    rewardInsectId = "rhinoceros_beetle",
                    rewardInsectDisplayName = "레어 장수풍뎅이",
                    rewardInsectLevel = 6
                },
                new TutorialQuest
                {
                    questId = "q_move", title = "첫 걸음!",
                    description = "화면 왼쪽 조이스틱으로 움직여보세요",
                    hint = "화면 왼쪽 아래를 누른 채 원하는 방향으로 밀어보세요",
                    type = QuestType.Movement, targetCount = 1,
                    prerequisiteQuestId = "q_approach",
                    rewardCandy = 3
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
                    rewardCandy = 30, rewardExp = 50
                },
                new TutorialQuest
                {
                    questId = "q_battle10", title = "전투 마스터",
                    description = "전투에서 10번 승리하세요",
                    hint = "다양한 야생 곤충들과 전투하세요",
                    type = QuestType.Battle, targetCount = 10,
                    prerequisiteQuestId = "q_capture10",
                    rewardCandy = 40, rewardExp = 60
                },
                new TutorialQuest
                {
                    questId = "q_complete", title = "모험의 시작",
                    description = "축하합니다! 이제 자유롭게 모험하세요!",
                    hint = "월드를 자유롭게 탐험하세요",
                    type = QuestType.Movement, targetCount = 1,
                    prerequisiteQuestId = "q_battle10",
                    rewardCandy = 100, rewardExp = 100
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
        }

        private void OnTeamChanged() => NotifyAction(QuestType.SetTeam);

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
            if (ActiveQuest == null || ActiveQuest.type != type) return;
            IncrementProgress(activeQuestId, count);
        }

        public void NotifyCapture(InsectRarity rarity)
        {
            if (!tutorialSessionStarted) return;
            if (ActiveQuest == null) return;

            if (ActiveQuest.type == QuestType.Capture)
            {
                IncrementProgress(activeQuestId);
            }
            else if (ActiveQuest.type == QuestType.CaptureRare && rarity >= InsectRarity.Uncommon)
            {
                IncrementProgress(activeQuestId);
            }
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

            if (quest.rewardCandy > 0)
            {
                if (candyInventory != null) candyInventory.AddCandy(quest.rewardCandy);
                else Debug.LogWarning($"[Quest] candyInventory null — 캔디 보상 손실: {questId} (+{quest.rewardCandy})");
            }

            if (quest.rewardExp > 0)
            {
                if (progressController != null) progressController.GainXp(quest.rewardExp);
                else Debug.LogWarning($"[Quest] progressController null — XP 보상 손실: {questId} (+{quest.rewardExp})");
            }

            if (!string.IsNullOrEmpty(quest.rewardItemId) && quest.rewardItemCount > 0)
            {
                if (itemInventory != null) itemInventory.AddItem(quest.rewardItemId, quest.rewardItemCount);
                else Debug.LogWarning($"[Quest] itemInventory null — 아이템 보상 손실: {questId} {quest.rewardItemId}x{quest.rewardItemCount}");
            }

            if (!string.IsNullOrEmpty(quest.rewardInsectId))
            {
                if (insectCollection != null)
                {
                    PlayerInsectData starter = insectCollection.AddCapturedInsect(
                        quest.rewardInsectId,
                        Mathf.Max(1, quest.rewardInsectLevel));

                    // 첫 포획 직후 바로 배틀할 수 있도록, 팀이 비어 있을 때만 1번 슬롯에 배치한다.
                    if (starter != null
                        && battleTeamManager != null
                        && !battleTeamManager.HasAnyInsect())
                    {
                        battleTeamManager.SetSlot(0, starter.instanceId);
                    }
                }
                else
                {
                    Debug.LogWarning($"[Quest] insectCollection null — 곤충 보상 손실: {questId} {quest.rewardInsectId}");
                }
            }

            // 완료했지만 아직 퀘스트 창에서 안 본 목록에 추가 → 퀵바 배지 +1. 창을 열면 MarkQuestsSeen로 비운다.
            unseenCompleted.Add(questId);

            QuestCompleted?.Invoke(quest);
            SaveProgress();
            // 퀘스트 완료 보상은 캔디/XP/아이템/곤충 → 클라우드 즉시 동기 (다른 기기 진입 시 재진행 방지).
            // IncrementProgress의 잦은 호출은 120초 자동저장에 맡겨 API 폭주 차단.
            if (CloudSaveManager.Instance != null) CloudSaveManager.Instance.SaveToCloud();
            ActivateNextQuest();
        }

        private void ActivateNextQuest()
        {
            if (allQuests == null) return;

            foreach (TutorialQuest quest in allQuests)
            {
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
            activeQuestId = null;
            ActiveQuest = null;

            PlayerPrefs.DeleteKey(ProgressKey);
            PlayerPrefs.DeleteKey(CompletedKey);
            PlayerPrefs.DeleteKey(ActiveKey);
            PlayerPrefs.DeleteKey(UnseenKey);
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

            PlayerPrefs.Save();
        }

        private void LoadProgress()
        {
            questProgress.Clear();
            completedQuests.Clear();
            unseenCompleted.Clear();

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
        }
    }
}
