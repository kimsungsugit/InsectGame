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
        private RegionBlightManager blight;
        // BattleWin 트리거의 **두 번째** 소스. 레이드도 전투이므로 이기면 같은 트리거를 쏜다.
        private RaidBattleController raidController;
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
        // 명부회 오염 거점을 무너뜨렸다. param=regionId. 소스는 RegionBlightManager.RegionCleansed.
        // **재발화 트리거다** — 정화 순간뿐 아니라 이미 정화된 리전에 다시 들어올 때도 쏜다
        // (OnRegionChanged 참조). 그 재발화가 없으면 정화 비트를 영영 못 보는 경로가 생긴다:
        // 거점 보스는 서브에리어 밖 본진에서도 도전 가능해서, 대치 비트를 보기 전에 이길 수
        // 있고 그 순간 정화 비트는 prereq 미충족이라 버려진다(FireBeat는 한 번에 하나만 쏜다).
        internal const string TriggerRegionCleansed = "RegionCleansed";

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

        /// <summary>
        /// RegionCleansed 트리거 소스. <c>DexController</c>와 같은 이유로 <b>Start보다 먼저</b>
        /// 불려야 한다 — 구독이 Start에서 한 번만 걸린다.
        /// </summary>
        public void AutoWire(RegionBlightManager blightManager)
        {
            if (blight == null) blight = blightManager;
        }

        /// <summary>
        /// <c>BattleWin</c>의 두 번째 소스. <c>DexController</c>와 같은 이유로 <b>Start보다 먼저</b>
        /// 불려야 한다.
        ///
        /// <b>없으면 Epic·Legendary를 이겨도 스토리가 모른다.</b> <c>CaptureChoiceUI</c>는
        /// 그 두 등급에 <b>포획과 1v1을 둘 다 막고 레이드만</b> 연다(<c>IsRaidTarget</c>). 그런데
        /// 레이드 승리는 포획(<c>AddCapturedInsect</c>)만 흘리고 <c>InsectBattleController.BattleEnded</c>는
        /// 안 울린다 — 그래서 <c>CaptureInsect</c> 비트는 도달하는데 <c>BattleWin</c> 비트만 못 했다.
        ///
        /// 최종장 <c>fin_seal</c>이 그 함정에 정확히 빠졌다: 저작이 <c>BattleWin mantis_unnamed</c>인데
        /// 그 종이 Legendary라 <b>1v1로 만날 방법이 아예 없었다.</b> 게다가 그 비트는 스파인이라
        /// (<c>ch12_clash</c>의 prereq · <c>fin_epilogue</c>의 게이트) 캠페인이 엔딩 직전에서
        /// 영구 정지한다. 예외도 경고도 안 난다.
        /// </summary>
        public void AutoWire(RaidBattleController raid)
        {
            if (raidController == null) raidController = raid;
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
            if (raidController != null)
                raidController.RaidEnded += OnRaidEnded;
            if (questManager != null)
                questManager.QuestCompleted += OnQuestCompleted;
            // LevelReach / CaptureInsect 소스 — 닫힌 enum의 나머지 두 타입도 배선(누락 시 영구 미발화).
            if (progressController != null)
                progressController.ProgressChanged += OnProgressChanged;
            if (insectCollection != null)
                insectCollection.InsectCaptured += OnInsectCaptured;
            if (dexController != null)
                dexController.DexUpdated += OnDexUpdated;
            if (blight != null)
                blight.RegionCleansed += OnRegionCleansed;
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
            if (raidController != null)
                raidController.RaidEnded -= OnRaidEnded;
            if (questManager != null)
                questManager.QuestCompleted -= OnQuestCompleted;
            if (progressController != null)
                progressController.ProgressChanged -= OnProgressChanged;
            if (insectCollection != null)
                insectCollection.InsectCaptured -= OnInsectCaptured;
            if (dexController != null)
                dexController.DexUpdated -= OnDexUpdated;
            if (blight != null)
                blight.RegionCleansed -= OnRegionCleansed;
        }

        // --- 이벤트 핸들러 → 중앙 평가 ---

        private void OnRegionChanged(RegionData region)
        {
            if (region == null) return;
            EvaluateTriggers(TriggerRegionEnter, region.regionId);

            // 이미 정화한 리전에 다시 들어왔다 — 정화 트리거를 재발화한다.
            // 정화 순간에 그 비트가 자격 미달이었거나(대치 비트를 아직 못 봄) 다른 비트가
            // 먼저 나가 버려졌을 수 있다. 재발화가 그 유일한 회복 경로다.
            // 이미 열람한 비트는 어차피 다시 안 뜨므로 중복 발화 걱정은 없다.
            if (blight != null && blight.IsCleansed(region.regionId))
                EvaluateTriggers(TriggerRegionCleansed, region.regionId);
        }

        /// <summary>
        /// 거점이 무너졌다 — 전투 결과 화면 뒤로 미룬다.
        ///
        /// <c>OnGuardianDefeated</c>와 같은 이유다. 정화는 보스전 승리 직후에 일어나는데 그건
        /// 결과 화면이 아직 떠 있는 시점이라, 미루지 않으면 정화 비트와 그 컷신이 보상 패널을
        /// 덮는다. 게다가 같은 전투가 <c>BattleWin</c>도 함께 자격을 주므로 둘을 순서대로 흘려야
        /// 앞 대사가 뒤 대사에 밀려나지 않는다.
        /// </summary>
        private void OnRegionCleansed(string regionId)
        {
            if (!string.IsNullOrEmpty(regionId)) DeferTrigger(TriggerRegionCleansed, regionId);
        }

        private void OnSubAreaChanged(SubAreaData subArea)
        {
            if (subArea != null) EvaluateTriggers(TriggerSubAreaEnter, subArea.subAreaId);
        }

        /// <summary>
        /// <b>이기자마자 대사를 띄우지 않는다.</b> <c>BattleEnded</c>는 KO 순간에 울리는데
        /// 전투 결과 화면은 그로부터 4초를 더 떠 있다(연출 페이즈가 끼면 6초 가까이). 그 위로
        /// 대화 모달이 열리면 <b>획득 EXP·캔디가 적힌 보상 패널을 통째로 덮는다</b> — 무엇을
        /// 얻었는지 못 본 채 대사를 읽게 된다. <c>BattleWin</c> 비트 12개 전부에 해당한다.
        ///
        /// 그래서 결과 화면이 스스로 닫힐 때(<c>BattleScreenUI.EndBattle</c>)까지 미룬다.
        /// 같은 판단을 <c>CutsceneDirector</c>가 컷신에 대해 이미 하고 있다 — 거기서는
        /// 카메라의 배틀 모드를 신호로 쓴다(BattleScreenUI는 IModalUI가 아니라 레지스트리로
        /// 알 수 없다). 이쪽은 화면 쪽이 끝났다고 <b>알려 주는</b> 형태다: UI가 스토리를 아는
        /// 방향은 허용되지만 그 반대는 의존 방향에 어긋난다.
        /// </summary>
        /// <summary>
        /// <b>이기자마자 대사를 띄우지 않는다.</b> <c>BattleEnded</c>는 KO 순간에 울리는데
        /// 전투 결과 화면은 그로부터 4초를 더 떠 있다(연출 페이즈가 끼면 6초 가까이). 그 위로
        /// 대화 모달이 열리면 <b>획득 EXP·캔디가 적힌 보상 패널을 통째로 덮는다</b>.
        /// </summary>
        private void OnBattleEnded(bool playerWon)
        {
            // **종 ID를 지금 읽어 큐에 싣는다.** 발화는 결과 화면이 닫힌 뒤로 미뤄지는데, 그때
            // 컨트롤러를 다시 물으면 이미 다음 전투가 시작됐을 수 있다(필드에서 연달아 붙는다).
            // 무param 비트는 이 값을 보지 않으므로 기존 저작은 그대로다.
            if (playerWon)
                DeferTrigger(TriggerBattleWin, battleController != null ? battleController.EnemyInsectId : null);
        }

        /// <summary>
        /// 레이드 승리도 <c>BattleWin</c>이다 — <b>Epic·Legendary는 그 길밖에 없다.</b>
        ///
        /// <c>CaptureChoiceUI.IsRaidTarget</c>이 그 두 등급에 포획과 1v1을 <b>둘 다 막고</b>
        /// 레이드만 연다. 그래서 종을 지정한 <c>BattleWin</c> 비트가 Epic·Legendary를 물면
        /// 1v1 경로가 존재하지 않아 <b>영영 발화하지 않는다</b>(AutoWire 요약 참조).
        ///
        /// 무param 비트에도 이롭다 — 예전에는 그 리전에서 레이드만 이기고 넘어간 플레이어가
        /// "그곳에서 전투 승리" 비트를 통째로 건너뛰었다.
        ///
        /// <c>OnBattleEnded</c>와 같은 이유로 <b>결과 화면 뒤로 미루고</b>, 종 ID는 지금 읽는다
        /// (<c>RaidBattleUI</c>의 정리 경로가 <c>NotifyBattlePresentationClosed</c>를 부른다).
        /// </summary>
        private void OnRaidEnded(bool playerWon)
        {
            if (!playerWon) return;
            string bossId = raidController != null && raidController.BossStats != null
                && raidController.BossStats.Data != null
                    ? raidController.BossStats.Data.insectId
                    : null;
            DeferTrigger(TriggerBattleWin, bossId);
        }

        /// <summary>
        /// 수문장 격파도 같은 자리에서 난다 — 오히려 더 이르다. <c>DefeatGuardian</c>은
        /// <c>CheckGuardianDefeat</c>에서 불리는데 그건 <b>결과 화면이 뜨기도 전</b>이라,
        /// 미루지 않으면 <c>gd_*</c> 비트가 전투 UI와 겹친 채로 열린다.
        ///
        /// 게다가 같은 전투에서 <c>BattleWin</c>도 함께 자격을 얻는다(유적 이후 리전은
        /// <c>gd_X</c>와 <c>chN_clash</c>가 같은 리전에 나란히 있다). 둘을 <b>순서대로</b>
        /// 흘려야 앞 대사가 뒤 대사에 밀려나지 않는다 — 그래서 큐다.
        /// </summary>
        private void OnGuardianDefeated(string regionId)
        {
            if (!string.IsNullOrEmpty(regionId)) DeferTrigger(TriggerGuardianDefeat, regionId);
        }

        // ── 전투 화면 뒤로 미뤄 둔 트리거 ──────────────────────────────────────
        private struct PendingTrigger
        {
            public string type;
            public string param;
        }

        private readonly List<PendingTrigger> pendingTriggers = new List<PendingTrigger>();
        private float pendingSeconds;
        // 전투 화면이 닫혔다는 통지를 받았다 — 그 뒤로는 모달만 기다리면 되고 시간은 안 센다.
        private bool presentationClosed;

        /// <summary>
        /// 미뤄 둔 발화를 포기하지 않고 <b>그냥 쏘는</b> 시각(초). 컷신의
        /// <c>PendingGiveUpSeconds</c>와 같은 값이지만 처리가 반대다 — 저쪽은 연출이라 버려도
        /// 되지만 <b>여기는 이야기의 진행이라 버리면 안 된다.</b> 전투 화면이 어떤 이유로든
        /// 닫혔다고 알려 주지 않으면(미배선·예외로 EndBattle 중단·씬 교체) 보상 패널을 덮는
        /// 쪽이 진행이 멈추는 것보다 훨씬 낫다.
        /// </summary>
        private const float PendingGiveUpSeconds = 12f;

        private void DeferTrigger(string type, string param)
        {
            if (!BeginDefer(type, param)) return;
            pendingTriggers.Add(new PendingTrigger { type = type, param = param });
        }

        /// <summary>
        /// 큐 <b>맨 앞</b>에 넣는다. 포획이 쓴다 — 게임에서는 포획이 전투 종료보다 먼저
        /// 일어나는데 미루기 판정은 프레임 끝에 하므로, 그냥 붙이면 발화 순서가 뒤집힌다.
        /// </summary>
        private void DeferTriggerFirst(string type, string param)
        {
            if (!BeginDefer(type, param)) return;
            pendingTriggers.Insert(0, new PendingTrigger { type = type, param = param });
        }

        /// <summary>중복이면 false. 큐가 비어 있었다면 대기 시계를 새로 건다.</summary>
        private bool BeginDefer(string type, string param)
        {
            for (int i = 0; i < pendingTriggers.Count; i++)
                if (pendingTriggers[i].type == type && pendingTriggers[i].param == param) return false;

            if (pendingTriggers.Count == 0)
            {
                pendingSeconds = 0f;
                presentationClosed = false;
            }
            return true;
        }

        /// <summary>
        /// 전투 화면(결과 포함)이 완전히 닫혔다 — 미뤄 둔 트리거를 지금 흘린다.
        /// <c>BattleScreenUI.EndBattle</c>과 <c>RaidBattleUI</c>의 정리 경로가 부른다.
        /// 미뤄 둔 게 없으면 아무 일도 안 한다.
        /// </summary>
        public void NotifyBattlePresentationClosed()
        {
            presentationClosed = true;
            DrainPendingTriggers();
        }

        /// <summary>
        /// 미뤄 둔 트리거를 <b>하나씩</b> 흘린다. 한 편이 화면에 뜨면 거기서 멈추고, 그 비트가
        /// 완료될 때(<see cref="CompleteBeat"/>) 다음이 이어진다.
        ///
        /// 밀어 넣으면 안 되는 이유가 조용하다: <c>NpcDialogueUI.ShowStory</c>는 열려 있는
        /// 모달을 <c>CloseModal</c>로 정리하는데, 그 경로가 <c>CompleteBeat</c>를 부른다 —
        /// <b>읽지도 않은 대사가 보상까지 지급된 채 열람 처리된다.</b> 화면엔 새 대사만 남아
        /// 무엇이 사라졌는지도 모른다.
        /// </summary>
        private void DrainPendingTriggers()
        {
            while (pendingTriggers.Count > 0)
            {
                // 한 편이 떠 있거나(pendingBeatId) 컷신이 도는 중(레지스트리)이면 기다린다.
                if (!string.IsNullOrEmpty(pendingBeatId)) return;
                if (InsectGame.UI.ModalUIRegistry.IsAnyOpen()) return;

                PendingTrigger t = pendingTriggers[0];
                pendingTriggers.RemoveAt(0);
                EvaluateTriggers(t.type, t.param);
            }

            // 다 흘렸다 — 다음 전투를 위해 되돌린다.
            presentationClosed = false;
            pendingSeconds = 0f;
        }

        private void Update()
        {
            if (pendingTriggers.Count == 0) return;

            // timeScale에 끌려다니면 안 된다 — 히트스톱·슬로모션이 결과 화면 직전까지 걸린다.
            pendingSeconds += Time.unscaledDeltaTime;

            // 대사·컷신이 떠 있는 동안은 시간이 지나도 밀어 넣지 않는다(위 주석의 그 손실).
            if (!string.IsNullOrEmpty(pendingBeatId)) return;
            if (InsectGame.UI.ModalUIRegistry.IsAnyOpen()) return;

            // 전투 화면은 IModalUI가 아니라 레지스트리로 알 수 없다 — 통지가 정상 경로다.
            // 통지를 이미 받았다면 모달만 걷히면 바로 흘린다(여기서 또 기다리면, 결과 화면
            // 위에 다른 창을 잠깐 열었다 닫은 것만으로 대사가 최대 12초 늦게 뜬다).
            if (!presentationClosed)
            {
                if (pendingSeconds < PendingGiveUpSeconds) return;
                // 그게 끝내 안 오면 겹치더라도 쏜다 — 진행을 잃는 것보다 낫다.
                Debug.LogWarning("[Story] 전투 화면 종료 통지가 없어 미뤄 둔 트리거를 그대로 발화한다");
            }

            DrainPendingTriggers();
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

        /// <summary>
        /// 이번 프레임에 들어온 포획 — <b>아직 어디서 잡은 것인지 모른다.</b>
        /// 판정은 <see cref="LateUpdate"/>가 한다(그 이유는 거기에 적었다).
        /// 가챠 다연차처럼 한 프레임에 여러 건이 들어올 수 있어 목록이다.
        /// </summary>
        private readonly List<string> frameCaptures = new List<string>();

        private void OnInsectCaptured(PlayerInsectData insect)
        {
            // InsectCaptured는 실제 포획/획득(AddInsectInternal)에서만 발화 — XP·치료·진화 오발화 없음.
            //
            // **여기서 바로 쏘지 않는다.** 전투·레이드 안에서 잡은 것이면 곧 결과 화면이 뜨는데,
            // 그걸 알려 주는 신호(BattleEnded/RaidEnded)는 **같은 프레임 뒤에** 온다.
            // 지금 쏘면 대사창이 획득 EXP·캔디 패널 위로 열린다 — `BattleWin`을 미루는 그 이유다.
            frameCaptures.Add(insect != null ? insect.insectId : null);
        }

        /// <summary>
        /// 포획을 <b>프레임 끝에</b> 판정한다. 여기까지 오면 같은 프레임의 전투 종료가
        /// 이미 큐에 들어와 있으므로, "전투 안에서 잡았는가"를 <b>상태를 묻지 않고</b> 알 수 있다.
        ///
        /// 컨트롤러에 물어보는 길은 막혀 있었다 — <c>InsectBattleController.IsBattleInProgress()</c>는
        /// <c>playerStats</c>/<c>enemyStats</c>가 한 번도 null이 되지 않아 첫 전투 이후 늘 true이고,
        /// <c>RaidBattleController.IsActive</c>는 포획이 일어나는 <c>OnRaidVictory</c>보다 <b>먼저</b>
        /// false가 된다. 둘 다 "지금 전투 중인가"를 못 답한다.
        ///
        /// <b>전투가 아니면 지연이 없다.</b> 필드·미니게임 포획은 큐가 비어 있어 그 자리에서 쏜다 —
        /// 무조건 <c>DeferTrigger</c>로 보내면 종료 통지가 오지 않아 <b>12초를 기다린다</b>.
        ///
        /// <b>덤으로 조용한 누락 하나가 함께 닫혔다.</b> <see cref="CompleteBeat"/>는
        /// <c>GrantReward</c>(곤충 지급)를 <c>pendingBeatId</c>를 <b>비우기 전에</b> 부른다.
        /// 그래서 스토리 보상으로 받은 곤충의 <c>CaptureInsect</c>는 즉시 평가되던 시절
        /// <see cref="FireBeat"/>의 <c>pendingBeatId</c> 가드에 걸려 **그냥 버려졌다**
        /// (예외도 경고도 없다). 프레임 끝으로 옮기니 그때는 이미 비워져 있어 정상 발화한다 —
        /// 배치 걸음에서 `ch12_sign`이 그 증거로 새로 떴다.
        /// </summary>
        private void LateUpdate()
        {
            if (frameCaptures.Count == 0) return;

            // 같은 프레임에 전투 종료가 들어왔는가. 들어왔으면 그 뒤로 함께 미룬다.
            bool battlePending = false;
            for (int i = 0; i < pendingTriggers.Count; i++)
                if (pendingTriggers[i].type == TriggerBattleWin) { battlePending = true; break; }

            for (int i = 0; i < frameCaptures.Count; i++)
            {
                // 포획이 전투 종료보다 **먼저** 일어났으므로 큐에서도 앞에 세운다 —
                // 미루기를 붙이면서 발화 순서가 뒤집히지 않게 한다.
                if (battlePending) DeferTriggerFirst(TriggerCaptureInsect, frameCaptures[i]);
                else EvaluateTriggers(TriggerCaptureInsect, frameCaptures[i]);
            }
            frameCaptures.Clear();
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
                    case TriggerRegionCleansed:
                        // param 완전 일치(리전/퀘스트/서브에리어 ID / 스토리 NPC ID / 수문장 리전 ID
                        // / 정화된 리전 ID).
                        matches = !string.IsNullOrEmpty(beat.trigger.param)
                            && beat.trigger.param == eventParam;
                        break;
                    case TriggerCaptureInsect:
                        // param 비면 아무 포획, 채우면 특정 곤충 ID.
                        matches = string.IsNullOrEmpty(beat.trigger.param)
                            || beat.trigger.param == eventParam;
                        break;
                    case TriggerBattleWin:
                        // param 비면 아무 승리, 채우면 **그 종을 이겼을 때만**(CaptureInsect와 같은 규약).
                        //
                        // 종 지정이 없던 시절 `fin_seal`(엔딩)이 `BattleWin ""` + req `nameless`였다.
                        // 무명과 대면한 직후 그 리전에서 **아무 야생 곤충이나 이기면** 엔딩 대사와
                        // 컷신·보상이 한꺼번에 터졌다 — 최종 수문장 격파와는 무관하게.
                        //
                        // 종을 지정해도 **재발화형은 유지된다**: 지정 종은 그 리전 스폰 풀에 있어야
                        // 하므로(story_lint 검사 11) 다시 만나 다시 이길 수 있다. 그래서 스파인에
                        // 걸어도 안전하다 — GuardianDefeat처럼 일생 1회가 아니다.
                        matches = string.IsNullOrEmpty(beat.trigger.param)
                            || beat.trigger.param == eventParam;
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
                // 진행 게이트 — requiredBeatId가 채워진 비트는 그 비트를 이미 열람해야 발화.
                matches = matches && RegionGateSatisfied(beat) && QuestGateSatisfied(beat)
                    && BeatGateSatisfied(beat);

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

        /// <summary>
        /// <c>requiredBeatId</c>가 채워진 비트는 그 비트를 <b>이미 열람해야</b> 발화한다(비면 무제약).
        ///
        /// <c>prerequisiteBeatId</c>와 <b>AND</b>다 — 저쪽은 체인의 순서를, 이쪽은 진행 단계를 맡는다.
        /// 여운 비트가 "같은 NPC의 직전 여운"만 물고 있어서 진행과 무관하게 말을 반복해 걸기만 하면
        /// 뒷 챕터 대사가 나왔다(초원에서 라온 3회 → 12장, 세라 4회 → 엔딩 에필로그).
        /// <see cref="StoryObjectiveResolver.SelectObjectiveBeat"/>도 <b>같은 게이트를 건다</b> —
        /// 한쪽만 걸면 잠긴 비트를 목표로 안내해 놓고 가서 말을 걸면 아무 일도 안 일어난다.
        /// </summary>
        private bool BeatGateSatisfied(StoryBeat beat)
        {
            if (beat == null || string.IsNullOrEmpty(beat.requiredBeatId)) return true;
            return IsSeen(beat.requiredBeatId);
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

            // **읽고 있는 비트를 밀어내지 않는다.** `EvaluateTriggers`는 같은 비트의 중복
            // 발화만 막았을 뿐(`beat.beatId == pendingBeatId`), **다른** 비트가 끼어드는 건
            // 막지 못했다. 그런데 `NpcDialogueUI.ShowStory`는 열린 모달을 `CloseModal`로
            // 정리하고, 그 경로가 `CompleteBeat`를 부른다 — 읽지 않은 대사가 보상까지 받고
            // 열람 처리된다(다시 볼 곳은 저널뿐이다).
            //
            // 실제 경로: 유적 이후 리전은 수문장을 잡으면 `gd_X`와 `chN_clash`가 **함께**
            // 자격을 얻는다. 놓친 트리거는 큐가 들고 있다가 이 비트가 끝나면 이어서 흘린다.
            if (!string.IsNullOrEmpty(pendingBeatId)) return;

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
            GrantReward(beat.onComplete, beatId);
            if (pendingBeatId == beatId) pendingBeatId = null;

            Save();
            // 스토리 보상은 캔디/XP/아이템/곤충 → 다른 기기 재관람 방지를 위해 즉시 클라우드 동기(퀘스트와 동일).
            if (CloudSaveManager.Instance != null) CloudSaveManager.Instance.SaveToCloud();

            StoryBeatCompleted?.Invoke(beat);   // 모달 닫힘 → 조우 카메라 포커스 조기 릴리즈

            // 한 편이 끝났으니 미뤄 둔 다음 편을 이어 붙인다. 컷신이 방금 시작됐다면
            // (StoryBeatCompleted 구독자) 레지스트리 가드에 걸려 여기서는 넘어가고,
            // 컷신이 끝난 뒤 Update가 집는다.
            DrainPendingTriggers();
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
                StoryObjectiveResolver.KindOf(beat.trigger.type, beat.requiredRegionId),
                beat.trigger.param,
                beat.requiredRegionId,
                beat.trigger.type,
                StoryObjectiveResolver.ThresholdOf(beat.trigger.type, beat.trigger.param));
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

        /// <summary>
        /// 이 스토리 NPC와 <b>이야기를 한 번이라도 나눴는가</b> — 그 인물이 화자인 비트나
        /// 그에게 말 거는 비트를 하나라도 열람했으면 true.
        ///
        /// 간부 보스전이 이걸 묻는다. 예전엔 호출부가 "이번 대화에서 비트가 안 떴다"만 보고
        /// 도전을 열었는데, 그건 <b>이미 소개를 봤다</b>와 <b>아직 차례가 아니다</b>를 구분하지
        /// 못한다. 집게·저울·관장의 소개는 각각 서브에리어 대치 비트(<c>chN_confront</c>)에
        /// 걸려 있어서, 리전에 도착하자마자 본진에 서 있는 그들에게 말을 걸면 <b>이름도 모르는
        /// 채로 보스전이 시작됐다</b> — 최종 보스인 관장까지 그랬다.
        ///
        /// 판정을 저작 데이터에서 낸다(인물 목록을 코드에 박지 않는다) — <c>speakerNpcId</c>는
        /// 대치·격전 비트가, <c>trigger.param</c>은 여운 비트가 채운다.
        /// </summary>
        public bool HasMetStoryNpc(string npcId)
        {
            return StoryObjectiveResolver.HasMetNpc(StoryService.AllBeats(), IsSeen, npcId);
        }

        // 보상 지급 — TutorialQuestManager.CompleteQuest 패턴 동일(null 시 경고 후 계속).
        /// <param name="beatId">
        /// 어느 비트의 보상인가. 첫 파트너 곤충만 플레이어 선택으로 바꾸므로 그 판정에 쓴다 —
        /// 호출부가 하나뿐이고 거기 beat이 있어서 값을 그냥 넘겨받는다.
        /// </param>
        private void GrantReward(StoryReward reward, string beatId)
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
                // 첫 파트너만 플레이어가 고른 종으로 바꾼다. **beatId로 게이트한다** —
                // 값 비교(rewardInsectId == "rhinoceros_beetle")로 하면 나중에 장수풍뎅이를 주는
                // 비트를 하나 더 저작했을 때 그것까지 조용히 하이재킹된다.
                // (지금 rewardInsectId가 채워진 비트는 ch1_intro·ch6_secret·fin_seal 셋이다.)
                string insectId = beatId == StarterInsectCatalog.StarterBeatId
                    ? StarterInsectCatalog.ResolveChoice(reward.rewardInsectId)
                    : reward.rewardInsectId;

                if (insectCollection != null)
                {
                    insectCollection.AddCapturedInsect(insectId, Mathf.Max(1, reward.rewardInsectLevel));

                    // 도감 등록은 지급과 한 쌍이다(`TutorialQuestManager`와 같은 형태). 빠뜨리면
                    // 준 곤충이 도감에 없어 100% 완주가 막히는데, **여기선 자기 발등도 찍는다** —
                    // 아래 `CapturedSpeciesCount`가 DexProgress 비트의 판정값이라 자기가 준 보상이
                    // 자기 트리거를 못 밀어올린다. 지금은 Story.json 전 비트가 rewardInsectId=""라
                    // 휴면 상태지만, 첫 곤충 보상 비트를 저작하는 순간 발현된다.
                    if (dexController != null)
                    {
                        dexController.RegisterEncounter(insectId);
                        dexController.RegisterCapture(insectId);
                    }
                }
                else Debug.LogWarning($"[Story] insectCollection null — 곤충 보상 손실 {insectId}");
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
