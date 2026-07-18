using System;
using System.Collections.Generic;
using System.IO;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Story
{
    // 스토리 트리거 평가·진행·보상 지휘자. MonoBehaviour + ICloudReloadable, 싱글턴 아님(AutoWire).
    // 기존 이벤트(RegionChanged/BattleEnded/SubAreaChanged/QuestCompleted/ProgressChanged/InsectUpdated)를
    // 재구독해 미열람·prereq충족·트리거일치 비트를 찾아 StoryBeatTriggered로 발화. 모달이 닫히면
    // CompleteBeat → seen 마킹 + onComplete 보상 + 저장 + 즉시 클라우드 동기(퀘스트 보상 패턴 동일).
    public class StoryDirector : MonoBehaviour, ICloudReloadable
    {
        // 트리거 평가 의존성(닫힌 enum의 각 소스). 각 null 가드.
        private RegionManager regionManager;
        private InsectBattleController battleController;
        private PlayerProgressController progressController;
        private PlayerInsectCollection insectCollection;
        private TutorialQuestManager questManager;
        // 보상 지급 의존성 — 트리거 소스와 분리해 별도 AutoWire(keyGuide/quickBar 다중 AutoWire 관례).
        private PlayerCandyInventory candyInventory;
        private PlayerItemInventory itemInventory;

        private StoryProgressData progress;
        // 발화됐으나 아직 완료(모달 닫힘)되지 않은 비트 — 같은 이벤트 반복 시 중복 발화 차단.
        private string pendingBeatId;
        private bool subscribed;

        public event Action<StoryBeat> StoryBeatTriggered;

        // 트리거 타입 상수(닫힌 enum). JSON trigger.type과 반드시 일치. EvaluateTriggers switch가 전 케이스 처리.
        private const string TriggerRegionEnter = "RegionEnter";
        private const string TriggerQuestComplete = "QuestComplete";
        private const string TriggerLevelReach = "LevelReach";
        private const string TriggerCaptureInsect = "CaptureInsect";
        private const string TriggerBattleWin = "BattleWin";
        private const string TriggerSubAreaEnter = "SubAreaEnter";
        private const string TriggerImmediate = "Immediate";
        // 스토리 NPC(어르신/라온/세라)에게 다가가 대화 시 발화. param=storyNpcId. 이벤트 소스는
        // WorldInteractionController가 OnNpcTalked를 호출하는 것(구독 대신 직접 진입점).
        private const string TriggerNpcTalk = "NpcTalk";

        // 트리거 평가 + 진행/보상 지급에 필요한 참조 주입. Bootstrap이 호출.
        public void AutoWire(RegionManager region, InsectBattleController battle,
            PlayerProgressController prog, PlayerInsectCollection collection,
            TutorialQuestManager quest)
        {
            if (regionManager == null) regionManager = region;
            if (battleController == null) battleController = battle;
            if (progressController == null) progressController = prog;
            if (insectCollection == null) insectCollection = collection;
            if (questManager == null) questManager = quest;
        }

        // 보상 인벤토리 주입(캔디/아이템). 곤충/EXP는 위 AutoWire의 collection/progress 재사용.
        public void AutoWire(PlayerCandyInventory candy, PlayerItemInventory items)
        {
            if (candyInventory == null) candyInventory = candy;
            if (itemInventory == null) itemInventory = items;
        }

        private void Awake()
        {
            progress = Load();
        }

        // 구독은 Start에서 — AutoWire가 Awake 뒤·Start 전에 호출되므로(EnsureComponent 즉시 add),
        // OnEnable에 두면 참조가 아직 null이라 구독 누락 → 영구 미발화(TutorialQuestManager와 동일한 이유).
        private void Start()
        {
            SubscribeEvents();
            // Immediate 트리거는 이벤트가 없으므로 시작 시 1회 평가.
            EvaluateTriggers(TriggerImmediate, null);
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        // --- 이벤트 구독 (SubscribeEvents/UnsubscribeEvents 짝) ---

        private void SubscribeEvents()
        {
            if (subscribed) return;
            subscribed = true;

            if (regionManager != null)
            {
                regionManager.RegionChanged += OnRegionChanged;
                regionManager.SubAreaChanged += OnSubAreaChanged;
            }
            if (battleController != null)
                battleController.BattleEnded += OnBattleEnded;
            if (questManager != null)
                questManager.QuestCompleted += OnQuestCompleted;
            // LevelReach / CaptureInsect 소스 — 닫힌 enum의 나머지 두 타입도 배선(누락 시 영구 미발화).
            if (progressController != null)
                progressController.ProgressChanged += OnProgressChanged;
            if (insectCollection != null)
                insectCollection.InsectUpdated += OnInsectUpdated;
        }

        private void UnsubscribeEvents()
        {
            if (!subscribed) return;
            subscribed = false;

            if (regionManager != null)
            {
                regionManager.RegionChanged -= OnRegionChanged;
                regionManager.SubAreaChanged -= OnSubAreaChanged;
            }
            if (battleController != null)
                battleController.BattleEnded -= OnBattleEnded;
            if (questManager != null)
                questManager.QuestCompleted -= OnQuestCompleted;
            if (progressController != null)
                progressController.ProgressChanged -= OnProgressChanged;
            if (insectCollection != null)
                insectCollection.InsectUpdated -= OnInsectUpdated;
        }

        // --- 이벤트 핸들러 → 중앙 평가 ---

        private void OnRegionChanged(RegionData region)
        {
            if (region != null) EvaluateTriggers(TriggerRegionEnter, region.regionId);
        }

        private void OnSubAreaChanged(SubAreaData subArea)
        {
            if (subArea != null) EvaluateTriggers(TriggerSubAreaEnter, subArea.subAreaId);
        }

        private void OnBattleEnded(bool playerWon)
        {
            if (playerWon) EvaluateTriggers(TriggerBattleWin, null);
        }

        private void OnQuestCompleted(TutorialQuest quest)
        {
            if (quest != null) EvaluateTriggers(TriggerQuestComplete, quest.questId);
        }

        private void OnProgressChanged(PlayerProgressData data)
        {
            int level = progressController != null ? progressController.Level : 0;
            EvaluateTriggers(TriggerLevelReach, level.ToString());
        }

        private void OnInsectUpdated(PlayerInsectData insect)
        {
            EvaluateTriggers(TriggerCaptureInsect, insect != null ? insect.insectId : null);
        }

        // WorldInteractionController가 스토리 NPC에게 대화(E) 시 호출 — 그 NPC의 NpcTalk 비트를 발화.
        // 반환: 실제로 비트가 발화했으면 true(호출부가 false면 앰비언트 대사로 폴백). 이벤트 구독이
        // 아니라 직접 진입점 — WorldInteractionController(UI)가 StoryDirector를 AutoWire해 호출한다.
        public bool OnNpcTalked(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return false;
            string before = pendingBeatId;
            EvaluateTriggers(TriggerNpcTalk, npcId);
            return pendingBeatId != before && !string.IsNullOrEmpty(pendingBeatId);
        }

        // --- 중앙 트리거 평가 ---

        // triggerType의 미열람·prereq충족·param일치 비트를 찾아 하나만 발화(모달 클로버링 방지).
        // switch(triggerType)는 닫힌 enum — JSON이 쓰는 모든 trigger.type을 여기서 처리해야 한다.
        // 누락 시 그 타입 비트가 영영 발화하지 않음. story_lint 검사 6이 이 switch를 이벤트 구독과 교차검사.
        private void EvaluateTriggers(string triggerType, string eventParam)
        {
            if (string.IsNullOrEmpty(triggerType)) return;

            foreach (StoryBeat beat in StoryService.AllBeats())
            {
                if (beat == null || beat.trigger == null) continue;
                if (beat.trigger.type != triggerType) continue;
                if (IsSeen(beat.beatId) || beat.beatId == pendingBeatId) continue;
                if (!PrerequisiteSatisfied(beat)) continue;

                bool matches;
                switch (triggerType)
                {
                    case TriggerRegionEnter:
                    case TriggerQuestComplete:
                    case TriggerSubAreaEnter:
                    case TriggerNpcTalk:
                        // param 완전 일치(리전/퀘스트/서브에리어 ID / 스토리 NPC ID).
                        matches = !string.IsNullOrEmpty(beat.trigger.param)
                            && beat.trigger.param == eventParam;
                        break;
                    case TriggerCaptureInsect:
                        // param 비면 아무 포획, 채우면 특정 곤충 ID.
                        matches = string.IsNullOrEmpty(beat.trigger.param)
                            || beat.trigger.param == eventParam;
                        break;
                    case TriggerBattleWin:
                        // param 비면 아무 승리(곤충 지정은 미지원).
                        matches = string.IsNullOrEmpty(beat.trigger.param);
                        break;
                    case TriggerLevelReach:
                        // 현재 레벨(eventParam) >= 임계 레벨(beat.trigger.param).
                        matches = int.TryParse(beat.trigger.param, out int need)
                            && int.TryParse(eventParam, out int cur)
                            && cur >= need;
                        break;
                    case TriggerImmediate:
                        matches = true;
                        break;
                    default:
                        // 미지원 타입 — JSON 오타 방어(무시).
                        matches = false;
                        break;
                }

                // 리전 게이트 — requiredRegionId가 채워진 비트는 현재 리전 일치 시에만 발화.
                matches = matches && RegionGateSatisfied(beat);

                if (matches)
                {
                    FireBeat(beat);
                    return;
                }
            }
        }

        // requiredRegionId가 채워진 비트는 현재 리전이 일치할 때만 발화(비면 무제약).
        // 무param CaptureInsect/BattleWin의 잘못된 리전 늦발화(발화 얼룩)를 차단.
        // regionManager는 AutoWire된 참조 — CurrentRegion(RegionManager.cs) 재사용.
        private bool RegionGateSatisfied(StoryBeat beat)
        {
            if (beat == null || string.IsNullOrEmpty(beat.requiredRegionId)) return true;
            string current = regionManager != null && regionManager.CurrentRegion != null
                ? regionManager.CurrentRegion.regionId : null;
            return beat.requiredRegionId == current;
        }

        private bool PrerequisiteSatisfied(StoryBeat beat)
        {
            if (beat == null) return false;
            if (string.IsNullOrEmpty(beat.prerequisiteBeatId)) return true;
            return IsSeen(beat.prerequisiteBeatId);
        }

        private void FireBeat(StoryBeat beat)
        {
            if (beat == null) return;
            pendingBeatId = beat.beatId;

            if (StoryBeatTriggered != null)
            {
                // NpcDialogueUI가 lines[]를 모달로 렌더 → 닫으면 CompleteBeat 콜백.
                StoryBeatTriggered.Invoke(beat);
            }
            else
            {
                // 렌더러 미배선(헤드리스/테스트) — 대사 없이 즉시 완료(보상/seen 처리).
                CompleteBeat(beat.beatId);
            }
        }

        // --- 비트 완료(모달 닫힘) → 진행/보상/저장 ---

        // NpcDialogueUI가 스토리 모달을 닫을 때 호출. seen 마킹 + onComplete 지급 + 저장 + 즉시 클라우드 동기.
        public void CompleteBeat(string beatId)
        {
            if (string.IsNullOrEmpty(beatId)) return;

            if (IsSeen(beatId))
            {
                if (pendingBeatId == beatId) pendingBeatId = null;
                return;
            }

            if (!StoryService.TryGetBeat(beatId, out StoryBeat beat))
            {
                if (pendingBeatId == beatId) pendingBeatId = null;
                return;
            }

            MarkSeen(beatId);
            GrantReward(beat.onComplete);
            if (pendingBeatId == beatId) pendingBeatId = null;

            Save();
            // 스토리 보상은 캔디/XP/아이템/곤충 → 다른 기기 재관람 방지를 위해 즉시 클라우드 동기(퀘스트와 동일).
            if (CloudSaveManager.Instance != null) CloudSaveManager.Instance.SaveToCloud();
        }

        private void MarkSeen(string beatId)
        {
            if (progress == null) progress = new StoryProgressData();
            if (progress.seenBeatIds == null) progress.seenBeatIds = new List<string>();
            if (!progress.seenBeatIds.Contains(beatId))
                progress.seenBeatIds.Add(beatId);
        }

        private bool IsSeen(string beatId)
        {
            return progress != null && progress.seenBeatIds != null
                && progress.seenBeatIds.Contains(beatId);
        }

        // 보상 지급 — TutorialQuestManager.CompleteQuest 패턴 동일(null 시 경고 후 계속).
        private void GrantReward(StoryReward reward)
        {
            if (reward == null) return;

            if (reward.rewardCandy > 0)
            {
                if (candyInventory != null) candyInventory.AddCandy(reward.rewardCandy);
                else Debug.LogWarning($"[Story] candyInventory null — 캔디 보상 손실 (+{reward.rewardCandy})");
            }

            if (reward.rewardExp > 0)
            {
                if (progressController != null) progressController.GainXp(reward.rewardExp);
                else Debug.LogWarning($"[Story] progressController null — XP 보상 손실 (+{reward.rewardExp})");
            }

            if (!string.IsNullOrEmpty(reward.rewardItemId) && reward.rewardItemCount > 0)
            {
                if (itemInventory != null) itemInventory.AddItem(reward.rewardItemId, reward.rewardItemCount);
                else Debug.LogWarning($"[Story] itemInventory null — 아이템 보상 손실 {reward.rewardItemId}x{reward.rewardItemCount}");
            }

            if (!string.IsNullOrEmpty(reward.rewardInsectId))
            {
                if (insectCollection != null)
                    insectCollection.AddCapturedInsect(reward.rewardInsectId, Mathf.Max(1, reward.rewardInsectLevel));
                else Debug.LogWarning($"[Story] insectCollection null — 곤충 보상 손실 {reward.rewardInsectId}");
            }

            // unlockQuestId: 스토리→퀘스트 역주입은 설계상 배제(단방향 관찰). 여기서 처리하지 않는다.
        }

        // --- 저장/로드 (SaveScope 계정별 격리, DexSaveService 패턴) ---

        private static string SavePath()
        {
            return SaveScope.FilePath(GameConstants.SaveFiles.StoryProgress);
        }

        private StoryProgressData Load()
        {
            string path = SavePath();
            if (!File.Exists(path))
            {
                return new StoryProgressData();
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<StoryProgressData>(json) ?? new StoryProgressData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[StoryDirector] 손상된 세이브 — 기본값으로 시작: {e.Message}");
                return new StoryProgressData();
            }
        }

        private void Save()
        {
            if (progress == null) return;
            string json = JsonUtility.ToJson(progress, true);
            AtomicFileWriter.WriteAllText(SavePath(), json);
        }

        // 클라우드 로드로 story_progress.json이 갱신된 뒤 인메모리 진행을 다시 읽는다(RegionManager 패턴).
        // 이벤트 재구독 없음(Start에서 이미 구독). 이미 본 비트는 재발화 안 함.
        public void ReloadFromDisk()
        {
            progress = Load();
        }
    }
}
