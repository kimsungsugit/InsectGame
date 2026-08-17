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
    // 기존 이벤트(RegionChanged/BattleEnded/SubAreaChanged/QuestCompleted/ProgressChanged/InsectCaptured)를
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
        // DexProgress 트리거 소스. 다른 의존성과 생성 순서가 달라 별도 AutoWire로 받는다.
        private InsectGame.Dex.DexController dexController;
        // 보상 지급 의존성 — 트리거 소스와 분리해 별도 AutoWire(keyGuide/quickBar 다중 AutoWire 관례).
        private PlayerCandyInventory candyInventory;
        private PlayerItemInventory itemInventory;

        private StoryProgressData progress;
        // 발화됐으나 아직 완료(모달 닫힘)되지 않은 비트 — 같은 이벤트 반복 시 중복 발화 차단.
        private string pendingBeatId;
        private bool subscribed;
        // 렌더러(NpcDialogueUI) 미배선 시점에 발화된 비트 보류함 — 구독 시 accessor가 flush한다.
        // 빌더 예외로 구독이 건너뛰면 oneShot 인트로가 헤드리스로 소모돼 영구 소실되던 것 방지.
        private StoryBeat deferredBeat;

        private Action<StoryBeat> storyBeatTriggered;
        // 커스텀 accessor: 렌더러가 Start 이후(빌더 예외 등)에 늦게 구독하면 보류 비트를 즉시 전달.
        public event Action<StoryBeat> StoryBeatTriggered
        {
            add
            {
                storyBeatTriggered += value;
                if (deferredBeat != null)
                {
                    StoryBeat b = deferredBeat;
                    deferredBeat = null;
                    value(b);
                }
            }
            remove { storyBeatTriggered -= value; }
        }
        // 비트 완료(스토리 모달 닫힘) 신호 — WorldInteractionController가 조우 카메라 포커스를 릴리즈.
        public event Action<StoryBeat> StoryBeatCompleted;

        // 트리거 타입 상수(닫힌 enum). JSON trigger.type과 반드시 일치. EvaluateTriggers switch가 전 케이스 처리.
        internal const string TriggerRegionEnter = "RegionEnter";
        internal const string TriggerQuestComplete = "QuestComplete";
        internal const string TriggerLevelReach = "LevelReach";
        internal const string TriggerCaptureInsect = "CaptureInsect";
        internal const string TriggerBattleWin = "BattleWin";
        internal const string TriggerSubAreaEnter = "SubAreaEnter";
        internal const string TriggerImmediate = "Immediate";
        // 스토리 NPC(어르신/라온/세라)에게 다가가 대화 시 발화. param=storyNpcId. 이벤트 소스는
        // WorldInteractionController가 OnNpcTalked를 호출하는 것(구독 대신 직접 진입점).
        internal const string TriggerNpcTalk = "NpcTalk";
        // 수문장 격파. param=regionId. 소스는 RegionManager.GuardianDefeated.
        // **일생 리전당 1회만 발화한다**(DefeatGuardian의 idempotent 가드) — QuestComplete와 같은
        // 부류라 이 트리거를 쓰는 비트는 **leaf 전용**이다. 어떤 비트의 prerequisiteBeatId도
        // 되어선 안 된다. 스파인에 걸면 그 순간 prereq가 미충족인 세이브는 캠페인이 영구 정지한다.
        internal const string TriggerGuardianDefeat = "GuardianDefeat";
        // 도감에 이름을 새긴 종 수가 임계에 닿으면 발화. param=정수 임계값.
        // LevelReach와 같은 누적형이라 **재발화 트리거다** — 임계를 넘긴 뒤 도감이 갱신될 때마다
        // 다시 평가되므로 스파인에 걸어도 안전하다(GuardianDefeat와 다르다).
        internal const string TriggerDexProgress = "DexProgress";

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

        /// <summary>
        /// DexProgress 트리거 소스. <b>Start보다 먼저 불려야 한다</b> — 구독이 Start에서
        /// 한 번만 걸리므로 그 뒤에 주입하면 이 타입 비트가 영영 발화하지 않는다.
        /// Bootstrap은 다른 AutoWire와 같은 자리에서 부른다.
        /// </summary>
        public void AutoWire(InsectGame.Dex.DexController dex)
        {
            if (dexController == null) dexController = dex;
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
                regionManager.GuardianDefeated += OnGuardianDefeated;
            }
            if (battleController != null)
                battleController.BattleEnded += OnBattleEnded;
            if (questManager != null)
                questManager.QuestCompleted += OnQuestCompleted;
            // LevelReach / CaptureInsect 소스 — 닫힌 enum의 나머지 두 타입도 배선(누락 시 영구 미발화).
            if (progressController != null)
                progressController.ProgressChanged += OnProgressChanged;
            if (insectCollection != null)
                insectCollection.InsectCaptured += OnInsectCaptured;
            if (dexController != null)
                dexController.DexUpdated += OnDexUpdated;
        }

        private void UnsubscribeEvents()
        {
            if (!subscribed) return;
            subscribed = false;

            if (regionManager != null)
            {
                regionManager.RegionChanged -= OnRegionChanged;
                regionManager.SubAreaChanged -= OnSubAreaChanged;
                regionManager.GuardianDefeated -= OnGuardianDefeated;
            }
            if (battleController != null)
                battleController.BattleEnded -= OnBattleEnded;
            if (questManager != null)
                questManager.QuestCompleted -= OnQuestCompleted;
            if (progressController != null)
                progressController.ProgressChanged -= OnProgressChanged;
            if (insectCollection != null)
                insectCollection.InsectCaptured -= OnInsectCaptured;
            if (dexController != null)
                dexController.DexUpdated -= OnDexUpdated;
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

        private void OnGuardianDefeated(string regionId)
        {
            if (!string.IsNullOrEmpty(regionId)) EvaluateTriggers(TriggerGuardianDefeat, regionId);
        }

        private void OnDexUpdated(InsectGame.Dex.DexSaveData _)
        {
            // 발견이 아니라 **포획해 이름을 새긴 종 수**다 — 2막의 "빈칸이 메워진다"가 곧 이 값이다.
            int count = dexController != null ? dexController.CapturedSpeciesCount : 0;
            EvaluateTriggers(TriggerDexProgress, count.ToString());
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

        private void OnInsectCaptured(PlayerInsectData insect)
        {
            // InsectCaptured는 실제 포획/획득(AddInsectInternal)에서만 발화 — XP·치료·진화 오발화 없음.
            EvaluateTriggers(TriggerCaptureInsect, insect != null ? insect.insectId : null);
        }

        // WorldInteractionController가 스토리 NPC에게 대화(E) 시 호출 — 그 NPC의 NpcTalk 비트를 발화.
        // 반환: 실제로 비트가 발화했으면 true(호출부가 false면 앰비언트 대사로 폴백). 이벤트 구독이
        // 아니라 직접 진입점 — WorldInteractionController(UI)가 StoryDirector를 AutoWire해 호출한다.
        public bool OnNpcTalked(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return false;
            return EvaluateTriggers(TriggerNpcTalk, npcId);   // 발화하면 true(대사 없는 비트도 정확)
        }

        // --- 중앙 트리거 평가 ---

        // triggerType의 미열람·prereq충족·param일치 비트를 찾아 하나만 발화(모달 클로버링 방지).
        // switch(triggerType)는 닫힌 enum — JSON이 쓰는 모든 trigger.type을 여기서 처리해야 한다.
        // 누락 시 그 타입 비트가 영영 발화하지 않음. story_lint 검사 6이 이 switch를 이벤트 구독과 교차검사.
        //
        // **첫 일치가 아니라 최우선 일치를 고른다.** AllBeats()는 Dictionary.Values라 순서가
        // 비결정적인데, 동시에 자격을 갖는 비트가 실제로 있다(어르신에게 말 걸 때 1막 개막
        // ch1_intro와 앰비언트 talk_elder가 함께 걸린다 — 포획 트리거도 ch2/ch4/ch5에 각 한 쌍).
        // 첫 일치를 집으면 **실행마다 다른 비트가 뜨고**, 무엇보다 HUD 목표(StoryObjectiveResolver가
        // 결정적으로 고른 것)와 실제 발화가 갈린다 — 안내를 따라갔는데 다른 대사가 나온다.
        // 순위는 StoryObjectiveResolver.CompareBeatPriority 하나가 정한다(사본 없음).
        private bool EvaluateTriggers(string triggerType, string eventParam)
        {
            if (string.IsNullOrEmpty(triggerType)) return false;

            StoryBeat chosen = null;

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
                    case TriggerGuardianDefeat:
                        // param 완전 일치(리전/퀘스트/서브에리어 ID / 스토리 NPC ID / 수문장 리전 ID).
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
                    case TriggerDexProgress:
                        // 현재 값(eventParam) >= 임계값(beat.trigger.param).
                        // LevelReach는 트레이너 레벨, DexProgress는 이름을 새긴 종 수.
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
                // 퀘스트 게이트 — requiredQuestId가 채워진 비트는 그 튜토리얼을 마쳐야 발화.
                matches = matches && RegionGateSatisfied(beat) && QuestGateSatisfied(beat);

                if (matches
                    && (chosen == null
                        || StoryObjectiveResolver.CompareBeatPriority(beat, chosen, SpineBeatIds()) < 0))
                {
                    chosen = beat;
                }
            }

            if (chosen == null) return false;
            FireBeat(chosen);
            return true;
        }

        /// <summary>
        /// 스파인 집합(다른 비트가 prerequisite로 지목하는 비트). Story.json에서만 나오므로
        /// 진행과 무관하고 1회 계산이면 족하다 — 목표 도출과 발화가 같은 캐시를 공유한다.
        /// </summary>
        private HashSet<string> SpineBeatIds()
        {
            if (spineBeatIdsCache == null)
                spineBeatIdsCache = StoryObjectiveResolver.CollectSpineBeatIds(StoryService.AllBeats());
            return spineBeatIdsCache;
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

        /// <summary>
        /// requiredQuestId가 채워진 비트는 그 튜토리얼 퀘스트를 <b>완료해야</b> 발화한다(비면 무제약).
        ///
        /// 튜토리얼과 스토리를 갈라 놓는 장치다. 예전엔 <c>ch1_intro</c>가 <c>Immediate</c>라
        /// 게임을 켜자마자 마을 어르신의 "오, 드디어 왔구나!"가 떴다 — 만나지도 않았는데 인사를
        /// 받는 셈이었고, 조작을 배우기도 전에 서사가 시작됐다. 이제 기본 조작을 익힌 뒤
        /// 어르신을 찾아가야 이야기가 열린다.
        ///
        /// <b>questManager가 없으면 통과시킨다</b> — 게이트가 스토리를 영구히 막는 것보다
        /// 조금 이르게 열리는 쪽이 낫다(진행 정지는 복구 수단이 없다).
        /// </summary>
        private bool QuestGateSatisfied(StoryBeat beat)
        {
            if (beat == null || string.IsNullOrEmpty(beat.requiredQuestId)) return true;
            if (questManager == null) return true;
            return questManager.IsQuestCompleted(beat.requiredQuestId);
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

            if (storyBeatTriggered != null)
            {
                // NpcDialogueUI가 lines[]를 모달로 렌더 → 닫으면 CompleteBeat 콜백.
                storyBeatTriggered.Invoke(beat);
            }
            else
            {
                // 렌더러 미배선(빌더 예외로 구독 누락 등) — 헤드리스로 즉시 완료하면 oneShot 인트로가
                // 대사 없이 보상만 지급되고 seen 마킹돼 영구 소실된다. 보류했다가 구독 시 flush(accessor)하고,
                // 끝내 구독자가 없으면 seen 미마킹으로 남아 다음 정상 부팅에서 발화한다.
                deferredBeat = beat;
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

            StoryBeatCompleted?.Invoke(beat);   // 모달 닫힘 → 조우 카메라 포커스 조기 릴리즈
        }

        private void MarkSeen(string beatId)
        {
            if (progress == null) progress = new StoryProgressData();
            if (progress.seenBeatIds == null) progress.seenBeatIds = new List<string>();
            if (!progress.seenBeatIds.Contains(beatId))
                progress.seenBeatIds.Add(beatId);
            objectiveDirty = true;   // 진행이 바뀌면 "다음 목표"도 바뀐다
        }

        // ------------------------------------------------------------------
        // 다음 목표 도출 — HUD 목표 행 / 미니맵 쐐기 / 자동 주행이 소비.
        // ------------------------------------------------------------------

        // 스파인 집합은 Story.json에서만 나온다(진행과 무관) — StoryService 캐시가 정적이라 1회면 족하다.
        private HashSet<string> spineBeatIdsCache;
        // 목표는 HUD가 매 프레임 묻는다. 72비트를 프레임마다 훑지 않도록 캐시하고
        // 진행이 바뀔 때(MarkSeen/클라우드 재적재)만 무효화한다.
        private bool objectiveDirty = true;
        private StoryObjective cachedObjective;

        /// <summary>
        /// 지금 이야기를 잇는 목표 하나. 없으면(전부 열람했거나 prereq가 전부 미충족) false.
        /// 선택 규칙과 결정성은 <see cref="StoryObjectiveResolver.SelectObjectiveBeat"/>에 있다.
        /// </summary>
        public bool TryGetCurrentObjective(out StoryObjective objective)
        {
            if (objectiveDirty)
            {
                RecomputeObjective();
                objectiveDirty = false;
            }
            objective = cachedObjective;
            return objective.IsValid;
        }

        private void RecomputeObjective()
        {
            // 퀘스트 게이트를 함께 넘긴다 — 안 넘기면 튜토리얼 중에 "마을 어르신에게 말 걸기"를
            // 안내해 놓고 정작 가서 말을 걸면 아무 일도 안 일어난다.
            StoryBeat beat = StoryObjectiveResolver.SelectObjectiveBeat(
                StoryService.AllBeats(), IsSeen, SpineBeatIds(),
                questManager != null ? questManager.IsQuestCompleted : (System.Func<string, bool>)null);

            if (beat == null || beat.trigger == null)
            {
                cachedObjective = default;
                return;
            }

            cachedObjective = new StoryObjective(
                beat.beatId,
                StoryObjectiveResolver.KindOf(beat.trigger.type),
                beat.trigger.param,
                beat.requiredRegionId);
        }

        private bool IsSeen(string beatId)
        {
            return progress != null && progress.seenBeatIds != null
                && progress.seenBeatIds.Contains(beatId);
        }

        /// <summary>
        /// 이 비트를 이미 열람했는가 — 스토리 저널(StoryJournalUI)이 잠금/다시보기를 가른다.
        /// 진행 자체는 여전히 이 클래스만 쓴다(읽기 전용 노출).
        /// </summary>
        public bool HasSeen(string beatId)
        {
            return IsSeen(beatId);
        }

        /// <summary>열람한 비트 수 — 저널 헤더의 진행률 표시용.</summary>
        public int SeenCount => progress != null && progress.seenBeatIds != null
            ? progress.seenBeatIds.Count : 0;

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
                {
                    insectCollection.AddCapturedInsect(reward.rewardInsectId, Mathf.Max(1, reward.rewardInsectLevel));

                    // 도감 등록은 지급과 한 쌍이다(`TutorialQuestManager`와 같은 형태). 빠뜨리면
                    // 준 곤충이 도감에 없어 100% 완주가 막히는데, **여기선 자기 발등도 찍는다** —
                    // 아래 `CapturedSpeciesCount`가 DexProgress 비트의 판정값이라 자기가 준 보상이
                    // 자기 트리거를 못 밀어올린다. 지금은 Story.json 전 비트가 rewardInsectId=""라
                    // 휴면 상태지만, 첫 곤충 보상 비트를 저작하는 순간 발현된다.
                    if (dexController != null)
                    {
                        dexController.RegisterEncounter(reward.rewardInsectId);
                        dexController.RegisterCapture(reward.rewardInsectId);
                    }
                }
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
            // 클라우드에서 다른 기기의 진행이 내려오면 목표도 달라진다 — 캐시를 버린다.
            objectiveDirty = true;
        }
    }
}
