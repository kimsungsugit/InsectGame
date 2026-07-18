using System.Collections.Generic;
using InsectGame.Capture;
using InsectGame.Data;
using InsectGame.Dex;
using InsectGame.Spawning;
using InsectGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InsectGame.Core
{
    public class PlaySceneBootstrap : MonoBehaviour
    {
        [Header("Bootstrap Flags")]
        [SerializeField] private bool buildWorld = true;
        [SerializeField] private bool buildUI = true;
        [SerializeField] private bool buildSpawns = true;

        [Header("World Setup")]
        [SerializeField] private int spawnPointCount = 16;
#pragma warning disable 0414
        [SerializeField] private float spawnRadius = 18f;
#pragma warning restore 0414
        [SerializeField] private float playerProximityRadius = 4.5f;
        [SerializeField] private LayerMask insectLayer = -1;

        [Header("UI Layout")]
        [SerializeField] private Vector2 canvasSize = new Vector2(1080f, 1920f);
        [SerializeField] private bool preferUIPrefab = true;
        [SerializeField] private string uiConfigResourcePath = "PlayUIConfig";
        [SerializeField] private string uiPrefabResourcePath = "UI/PlayHUD";

        private readonly Dictionary<string, InsectSkill> generatedSkillCache = new Dictionary<string, InsectSkill>();

        private static Material CreateSafeMaterial(Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
            mat.color = color;
            return mat;
        }

        private void Awake()
        {
            Build();
        }

        public void Build()
        {
            Debug.Log("[PlaySceneBootstrap] Build 시작");
            GameObject player = EnsurePlayer();
            Debug.Log("[PlaySceneBootstrap] Player 생성 완료");
            Camera camera = EnsureCamera(player.transform);
            Debug.Log("[PlaySceneBootstrap] Camera 생성 완료");
            EnsureLight();

            if (buildWorld)
            {
                // EnsureGround는 과거 try/catch 밖이라 지형 생성 중 예외 1건이 BuildSystems(곤충/UI/스포너) 전체를
                // 죽여 "캐릭터 외 아무것도 없음" 장애로 이어졌다. 격리하여 지형 실패가 게임 본체를 멈추지 않게 한다.
                try
                {
                    EnsureGround();
                }
                catch (System.Exception e)
                {
                    groundError = $"{e.GetType().Name}: {e.Message}";
                    Debug.LogError($"[PlaySceneBootstrap] EnsureGround 실패 — 월드 지형 생성 중단(게임은 계속): {groundError}\n{e.StackTrace}");
                }
            }
            Debug.Log("[PlaySceneBootstrap] 월드 생성 완료, BuildSystems 시작");

            try
            {
                BuildSystems(player, camera);
                Debug.Log("[PlaySceneBootstrap] BuildSystems 완료");
            }
            catch (CriticalBootstrapException e)
            {
                // 핵심 시스템 (InsectDatabase, InsectSpawner 등) 실패 — 게임 진행 불가능.
                // catch-all로 묻으면 "곤충 안 보임" 같은 비명시적 장애로 이어짐 → 사용자 알림.
                Debug.LogError($"[PlaySceneBootstrap] 핵심 시스템 실패 — 게임 중단: {e.Message}\n{e.StackTrace}");
                criticalError = e.Message;
            }
            catch (System.Exception e)
            {
                // 부가 시스템 (UI/Battle/Capture 등) 부분 실패 — 옛 동작 유지(게임 계속).
                Debug.LogError($"[PlaySceneBootstrap] 시스템 초기화 중 에러 발생 (게임은 계속 실행됩니다): {e.Message}\n{e.StackTrace}");
            }
        }

        // 핵심 시스템 실패는 별도 예외로 분리하여 catch에서 게임 중단 처리.
        // 옛 catch-all은 database null 같은 치명 오류도 묻어 곤충 안 스폰 등의 비명시적 장애 발생.
        private class CriticalBootstrapException : System.Exception
        {
            public CriticalBootstrapException(string msg) : base(msg) { }
        }

        private string criticalError;
        private string groundError; // EnsureGround 실패 시 화면 표시용 (회색필드 진단)
        private GUIStyle criticalErrStyle; // OnGUI 매 프레임 new GUIStyle 차단 — lazy 1회 생성
        private GUIStyle groundErrStyle;

        private void OnGUI()
        {
            // 지형 생성 실패 배너 — 화면 상단에 항상 표시(콘솔보다 스크린샷 쉬움). 회색필드 원인 추적용.
            if (!string.IsNullOrEmpty(groundError))
            {
                if (groundErrStyle == null)
                {
                    groundErrStyle = new GUIStyle(GUI.skin.box)
                    { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = true };
                    groundErrStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
                }
                float gw = Screen.width - 20f;
                Rect gr = new Rect(10f, 10f, gw, 70f);
                GUI.color = new Color(0.2f, 0.05f, 0f, 0.9f);
                GUI.DrawTexture(gr, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(gr, $"[지형생성 실패] {groundError}", groundErrStyle);
            }

            if (string.IsNullOrEmpty(criticalError)) return;
            if (criticalErrStyle == null)
            {
                criticalErrStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                criticalErrStyle.normal.textColor = new Color(1f, 0.9f, 0.9f);
            }
            float w = Mathf.Min(640f, Screen.width - 40f);
            float h = 200f;
            Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(r, $"⚠ 핵심 시스템 초기화 실패\n\n{criticalError}\n\n게임을 재시작 하세요.", criticalErrStyle);
        }

        private void BuildSystems(GameObject player, Camera camera)
        {
            // 인증/클라우드/로그인 UI (다른 시스템보다 먼저 생성)
            AuthManager authManager = EnsureComponent<AuthManager>("World/AuthManager");
            CloudSaveManager cloudSave = EnsureComponent<CloudSaveManager>("World/CloudSaveManager");
            InsectGame.UI.LoginUI loginUI = EnsureComponent<InsectGame.UI.LoginUI>("UI/LoginUI");
            // 게스트 → 정식 계정 전환 패널(게스트일 때만 상단 배지 표시). 의존성 없음.
            EnsureComponent<InsectGame.UI.AccountLinkUI>("UI/AccountLinkUI");
            // 동기화 충돌(클라우드가 더 최신 + 로컬 진행) 시 선택 모달. CloudSaveManager(위) 이후 생성 — 구독 보장.
            EnsureComponent<InsectGame.UI.SaveConflictUI>("UI/SaveConflictUI");
            // 계정 설정/삭제 패널(Play 필수: 인앱 계정 삭제). AuthManager(위) 이후 생성.
            EnsureComponent<InsectGame.UI.AccountSettingsUI>("UI/AccountSettingsUI");

            // 월드/채널 시스템
            WorldChannelManager worldChannel = EnsureComponent<WorldChannelManager>("World/WorldChannel");
            InsectGame.UI.WorldLobbyUI worldLobbyUI = EnsureComponent<InsectGame.UI.WorldLobbyUI>("UI/WorldLobbyUI");
            worldLobbyUI.AutoWire(worldChannel);
            InsectGame.UI.WorldFieldMultiplayerUI worldFieldUi =
                EnsureComponent<InsectGame.UI.WorldFieldMultiplayerUI>("UI/WorldFieldMultiplayerUI");

            GameClock clock = EnsureComponent<GameClock>("World/GameClock");
            WeatherSystem weather = EnsureComponent<WeatherSystem>("World/WeatherSystem");
            WorldStateProvider worldState = EnsureComponent<WorldStateProvider>("World/WorldStateProvider");
            worldState.AutoWire(clock, weather);

            InsectDatabase database = EnsureExpandedDatabase();
            if (database == null || database.insects == null || database.insects.Count == 0)
            {
                throw new CriticalBootstrapException("InsectDatabase 생성 실패 — 곤충 데이터 로드 불가");
            }
            ValidateBattleDefinitions(database);

            InsectSpawner spawner = EnsureComponent<InsectSpawner>("World/InsectSpawner");
            if (spawner == null)
            {
                throw new CriticalBootstrapException("InsectSpawner 컴포넌트 생성 실패");
            }
            if (buildSpawns)
            {
                spawner.AutoWire(database, worldState, EnsureSpawnPoints());
            }
            spawner.gameObject.layer = 0;

            InsectLoreBootstrapper lore = EnsureComponent<InsectLoreBootstrapper>("World/InsectLoreBootstrapper");
            lore.AutoWire(database);

            Data.ItemDatabase itemDatabase = Resources.Load<Data.ItemDatabase>("ItemDatabase");
            if (itemDatabase == null) itemDatabase = Data.ItemDatabase.CreateRuntimeDefault();
            ItemEffectManager itemEffects = EnsureComponent<ItemEffectManager>("World/ItemEffects");
            itemEffects.AutoWire(itemDatabase);
            spawner.AutoWire(itemEffects);

            DexController dex = EnsureComponent<DexController>("UI/DexController");
            PlayerProgressController progress = EnsureComponent<PlayerProgressController>("World/PlayerProgress");
            PlayerInsectCollection insectCollection = EnsureComponent<PlayerInsectCollection>("World/PlayerInsects");
            PlayerCandyInventory candyInventory = EnsureComponent<PlayerCandyInventory>("World/PlayerCandies");
            PlayerItemInventory itemInventory = EnsureComponent<PlayerItemInventory>("World/PlayerItems");
            PlayerCurrencyWallet wallet = EnsureComponent<PlayerCurrencyWallet>("World/PlayerCurrency");
            dex.AutoWire(wallet); // 도감 첫 발견 시 InsectLoreEntry.rewardCoins 실지급(코인 발행 경로)
            // 클라우드 저장이 레벨/XP/캔디/코인을 실제 파일 시스템에서 읽고 쓰도록 연결
            // (옛 PlayerPrefs 미러와 어긋나 진행도가 클라우드에 동기화되지 않던 문제 수정).
            cloudSave.AutoWire(progress, candyInventory, wallet);
            // 클라우드 로드 후 인메모리 캐시 리로드 — 다른 기기 첫 로그인 시 즉시 반영(재시작 불필요).
            cloudSave.RegisterReloadable(insectCollection);
            cloudSave.RegisterReloadable(dex);
            cloudSave.RegisterReloadable(itemInventory); // 아이템도 클라우드 적용 후 인메모리 갱신
            ShopUIController shopUi = EnsureComponent<ShopUIController>("UI/ShopUI");
            shopUi.ConfigureCatalog(
                new[] { "net_silver", "net_gold", "exp_boost" },
                new[] { 200, 400, 300 });
            DexUIController dexSummary = EnsureComponent<DexUIController>("UI/DexSummary");
            dexSummary.AutoWire(dex);

            DexDetailUIController dexDetail = EnsureComponent<DexDetailUIController>("UI/DexDetail");
            dexDetail.AutoWire(database, dex);

            DexListUIController dexList = EnsureComponent<DexListUIController>("UI/DexList");

            DexScreenUI dexScreen = EnsureComponent<DexScreenUI>("UI/DexScreen");
            dexScreen.AutoWire(database, dex);
            dexScreen.AutoWire(insectCollection, itemInventory);
            // 곤충 3D 모델을 도감에 RenderTexture로 표시(옛 단색 박스/약식 2D 대체)
            InsectModelPreviewRenderer insectPreview = EnsureComponent<InsectModelPreviewRenderer>("UI/InsectModelPreview");
            dexScreen.AutoWire(insectPreview);

            CaptureController capture = EnsureComponent<CaptureController>("Capture/CaptureController");
            capture.AutoWire(dex);
            capture.AutoWire(progress);
            capture.AutoWire(insectCollection);
            capture.AutoWire(candyInventory);
            capture.AutoWire(itemEffects);

            CaptureMinigameController minigame = EnsureComponent<CaptureMinigameController>("Capture/CaptureMinigame");
            minigame.AutoWire(capture);

            CaptureFeedbackController feedback = EnsureComponent<CaptureFeedbackController>("Capture/CaptureFeedback");
            feedback.AutoWire(capture);

            PlayerProgressUIController progressUi = EnsureComponent<PlayerProgressUIController>("UI/PlayerProgressUI");
            progressUi.AutoWire(progress);
            progressUi.AutoWire(candyInventory);
            PlayerCurrencyUIController currencyUi = EnsureComponent<PlayerCurrencyUIController>("UI/CurrencyUI");
            currencyUi.AutoWire(wallet);

            insectCollection.AutoWire(database, candyInventory);

            BattleTeamManager battleTeam = EnsureComponent<BattleTeamManager>("Battle/BattleTeam");
            battleTeam.AutoWire(insectCollection);
            cloudSave.RegisterReloadable(battleTeam);

            Battle.InsectBattleController battleController = EnsureComponent<Battle.InsectBattleController>("Battle/BattleController");
            battleController.AutoWire(insectCollection, candyInventory, progress, itemInventory);
            battleController.AutoWire(dex);
            // EXP/캔디 부스터(아이템) 배율을 배틀 승리 보상에도 적용 — 포획 경로와 동일.
            battleController.AutoWire(itemEffects);
            battleController.AutoWire(wallet); // 승리 시 소량 코인 지급(상점 코인결제 지속 수급)
            Battle.InsectBattleUIController battleUi = EnsureComponent<Battle.InsectBattleUIController>("Battle/BattleUI");
            battleUi.AutoWire(battleController, insectCollection);

            CameraFollower camFollower = camera.GetComponent<CameraFollower>();
            PlayerMovement playerMov = player.GetComponent<PlayerMovement>();
            minigame.AutoWire(playerMov);
            // 모바일 터치 이동 — 가상 조이스틱(UI→Core 입력 푸시)
            InsectGame.UI.VirtualJoystickUI joystick = EnsureComponent<InsectGame.UI.VirtualJoystickUI>("UI/VirtualJoystick");
            joystick.AutoWire(playerMov);
            InsectGame.UI.BattleScreenUI battleScreen = EnsureComponent<InsectGame.UI.BattleScreenUI>("UI/BattleScreen");
            battleScreen.AutoWire(battleController, camFollower, playerMov);

            PlayerInsectLevelUpUIController levelUpUi = EnsureComponent<PlayerInsectLevelUpUIController>("UI/LevelUpUI");
            levelUpUi.AutoWire(insectCollection);

            PlayerInsectSelectionUIController selectionUi = EnsureComponent<PlayerInsectSelectionUIController>("UI/LevelUpSelection");
            selectionUi.AutoWire(insectCollection, levelUpUi);

            PlayerItemInventoryGridUIController inventoryUi = EnsureComponent<PlayerItemInventoryGridUIController>("UI/InventoryUI");
            inventoryUi.AutoWire(itemInventory, itemDatabase, itemEffects);
            itemEffects.AutoWire(itemDatabase);
            shopUi.AutoWire(itemInventory, itemDatabase, wallet);

            CaptureRaycastTrigger raycastTrigger = EnsureComponent<CaptureRaycastTrigger>("Capture/RaycastTrigger");
            raycastTrigger.AutoWire(camera, minigame);

            CaptureProximityTrigger proximityTrigger = EnsureComponent<CaptureProximityTrigger>("Capture/ProximityTrigger");
            proximityTrigger.transform.SetParent(player.transform, false);
            proximityTrigger.transform.localPosition = Vector3.zero;
            SphereCollider proximityCollider = proximityTrigger.GetComponent<SphereCollider>();
            if (proximityCollider == null)
            {
                proximityCollider = proximityTrigger.gameObject.AddComponent<SphereCollider>();
            }
            proximityCollider.isTrigger = true;
            proximityCollider.radius = playerProximityRadius;
            proximityTrigger.AutoWire(minigame);

            CaptureTriggerModeController modeController = EnsureComponent<CaptureTriggerModeController>("Capture/TriggerMode");
            modeController.AutoWire(raycastTrigger, proximityTrigger);

            TrainingManager trainingMgr = EnsureComponent<TrainingManager>("Training/TrainingManager");
            trainingMgr.AutoWire(insectCollection, candyInventory);
            InsectSkill[] allTrainingSkills = CreateTrainingSkills();
            trainingMgr.Initialize(CreateTrainingMethods(), CollectAllSkills(database, allTrainingSkills));

            battleScreen.AutoWire(battleTeam, insectCollection, trainingMgr);

            InsectGame.UI.TrainingUI trainingUi = EnsureComponent<InsectGame.UI.TrainingUI>("UI/TrainingUI");
            trainingUi.AutoWire(trainingMgr, insectCollection, candyInventory);

            Data.CaptureItemData[] captureItemDefs = CreateCaptureItems();

            Battle.RaidBattleController raidController = EnsureComponent<Battle.RaidBattleController>("Battle/RaidController");
            raidController.AutoWire(insectCollection, candyInventory, progress, dex, trainingMgr);
            // EXP/캔디 부스터(아이템) 배율을 레이드 승리 보상에도 적용 — 포획 경로와 동일.
            raidController.AutoWire(itemEffects);

            InsectGame.UI.RaidBattleUI raidBattleUi = EnsureComponent<InsectGame.UI.RaidBattleUI>("UI/RaidBattleUI");
            raidBattleUi.AutoWire(raidController, camFollower, playerMov);

            Battle.BattleArenaController arenaController = EnsureComponent<Battle.BattleArenaController>("World/BattleArena");
            battleController.AutoWire(arenaController);
            raidController.AutoWire(arenaController);
            battleScreen.AutoWire(arenaController);
            raidBattleUi.AutoWire(arenaController);

            InsectGame.UI.CaptureChoiceUI captureChoice = EnsureComponent<InsectGame.UI.CaptureChoiceUI>("UI/CaptureChoice");
            captureChoice.AutoWire(minigame, battleController, battleUi, battleTeam, insectCollection, proximityTrigger, capture, dex, trainingMgr, itemInventory, raidController);
            captureChoice.AutoWire(playerMov);
            captureChoice.SetCaptureItems(captureItemDefs);

            // 필드 아이템 스폰 비활성화 — 아이템은 샵/보상에서만 획득
            // Spawning.CaptureItemSpawner itemSpawner = EnsureComponent<Spawning.CaptureItemSpawner>("World/CaptureItemSpawner");
            // itemSpawner.AutoWire(itemInventory);
            // itemSpawner.Initialize(captureItemDefs, player.transform);

            if (itemInventory.GetCount("net_basic") < 1)
                itemInventory.AddItem("net_basic", 5);
            if (itemInventory.GetCount("net_silver") < 1)
                itemInventory.AddItem("net_silver", 3);
            if (itemInventory.GetCount("net_gold") < 1)
                itemInventory.AddItem("net_gold", 1);

            CaptureInputController inputController = EnsureComponent<CaptureInputController>("Capture/InputController");
            inputController.AutoWire(modeController, raycastTrigger, proximityTrigger);
            inputController.AutoWire(captureChoice);
            inputController.AutoWire(battleScreen, raidBattleUi, dexScreen);
            inputController.GetType().GetField("minigame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inputController, minigame);

            GameplayTuningApplier tuning = EnsureComponent<GameplayTuningApplier>("World/GameplayTuning");
            tuning.AutoWire(spawner, capture);

            InsectGame.UI.CollectionUI collectionUi = EnsureComponent<InsectGame.UI.CollectionUI>("UI/CollectionUI");
            collectionUi.AutoWire(insectCollection, candyInventory, progress);

            InsectGame.UI.CapturePopupUI capturePopup = EnsureComponent<InsectGame.UI.CapturePopupUI>("UI/CapturePopup");
            capturePopup.AutoWire(capture);
            capturePopup.AutoWire(insectCollection);

            InsectGame.UI.BattleTeamUI battleTeamUi = EnsureComponent<InsectGame.UI.BattleTeamUI>("UI/BattleTeamUI");
            battleTeamUi.AutoWire(battleTeam, insectCollection);

            SocialPvpManager socialPvp = EnsureComponent<SocialPvpManager>("World/SocialPvpManager");
            socialPvp.AutoWire(insectCollection, battleTeam, progress);
            InsectGame.UI.SocialPvpUI socialPvpUi = EnsureComponent<InsectGame.UI.SocialPvpUI>("UI/SocialPvpUI");
            socialPvpUi.AutoWire(socialPvp);

            Data.RegionData[] regionDefs = RegionDefinitions.CreateAll();
            RegionManager regionMgr = EnsureComponent<RegionManager>("World/RegionManager");
            regionMgr.Initialize(regionDefs);
            regionMgr.AutoWire(progress);
            cloudSave.RegisterReloadable(regionMgr);

            SubAreaEnvironment subAreaEnv = EnsureComponent<SubAreaEnvironment>("World/SubAreaEnvironment");
            subAreaEnv.AutoWire(regionMgr);

            SubAreaWorldBuilder subAreaWorld = EnsureComponent<SubAreaWorldBuilder>("World/SubAreaWorld");
            subAreaWorld.AutoWire(regionMgr, camFollower);

            spawner.AutoWire(regionMgr);

            // 리전 진입 시 BGM 자동 전환
            regionMgr.RegionChanged += region =>
            {
                if (region != null && AudioManager.Instance != null)
                    AudioManager.Instance.PlayBGMForRegion(region.regionId);
            };

            // 수문장 곤충 스폰 (3D 엔티티)
            CreateGuardians(regionDefs, database);

            PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();
            if (playerMovement != null) playerMovement.AutoWire(regionMgr);
            worldFieldUi.AutoWire(worldChannel, playerMovement);

            // 재진입 자동 복귀: 이전 종료 시 SubArea 안이었으면 그 위치로 텔레포트.
            // 다음 RegionManager.Update가 ContainsPoint 감지 → 자동 SubArea 진입 트리거.
            regionMgr.RestoreLastSubArea(player.transform);

            // 시작 시 frozen은 LoginUI/WorldLobbyUI가 OnGUI로 입력 흡수하므로 불필요.
            // 월드 선택 미완료 시 frozen이 풀리지 않는 버그 회피 (이전: SetFrozen(true) 호출).
            // 마우스 클릭은 PlayerMovement:84-85의 pointerOverUI 체크로 UI 위에서 캐릭터 이동 안 함.

            InsectGame.UI.RegionMapUI mapUi = EnsureComponent<InsectGame.UI.RegionMapUI>("UI/RegionMapUI");
            mapUi.AutoWire(regionMgr, progress, dex, database);
            mapUi.AutoWire(spawner);

            InsectGame.UI.KeyGuideHUD keyGuide = EnsureComponent<InsectGame.UI.KeyGuideHUD>("UI/KeyGuideHUD");
            keyGuide.AutoWire(minigame, battleController, battleUi);
            keyGuide.AutoWire(regionMgr);
            keyGuide.AutoWire(itemInventory);

            InsectGame.UI.QuickAccessBarUI quickBar = EnsureComponent<InsectGame.UI.QuickAccessBarUI>("UI/QuickAccessBar");
            quickBar.AutoWire(dexScreen, battleTeamUi, trainingUi, collectionUi, mapUi);
            quickBar.AutoWire(socialPvpUi);
            quickBar.AutoWire(battleScreen, raidBattleUi, playerMov);

            InsectGame.UI.PlayerStatusHUD statusHud = EnsureComponent<InsectGame.UI.PlayerStatusHUD>("UI/PlayerStatusHUD");
            statusHud.AutoWire(progress, candyInventory, insectCollection, itemInventory, dex, battleTeam, regionMgr);
            statusHud.AutoWire(wallet);

            // 좌상단 소형 미니맵(플레이어 중심 레이더, 곤충 위치) — 자기 충족형이라 AutoWire 불필요.
            EnsureComponent<InsectGame.UI.MinimapUI>("UI/Minimap");

            CharacterOutfitManager outfitManager = EnsureComponent<CharacterOutfitManager>("World/CharacterOutfit");
            outfitManager.AutoWire(wallet);
            cloudSave.RegisterReloadable(outfitManager);

            OutfitBonusProvider outfitBonus = EnsureComponent<OutfitBonusProvider>("World/CharacterOutfit");
            outfitBonus.AutoWire(outfitManager);

            playerMov.AutoWire(outfitBonus);
            capture.AutoWire(outfitBonus);
            battleController.AutoWire(outfitBonus);
            raidController.AutoWire(outfitBonus);
            spawner.AutoWire(outfitBonus);

            InsectGame.UI.CharacterOutfitUI outfitUi = EnsureComponent<InsectGame.UI.CharacterOutfitUI>("UI/CharacterOutfitUI");
            outfitUi.AutoWire(outfitManager, outfitBonus);

            if (buildWorld)
            {
                EnsureInsectPrefab(spawner);
            }

            if (buildUI)
            {
                BuildUI(dexSummary, dexDetail, dexList, minigame, inputController, feedback);
            }

            TutorialQuestManager questManager = EnsureComponent<TutorialQuestManager>("World/TutorialQuestManager");
            questManager.AutoWire(insectCollection, candyInventory, progress, itemInventory,
                battleController, raidController, dex, trainingMgr, battleTeam, regionMgr);
            cloudSave.RegisterReloadable(questManager);

            InsectGame.UI.TutorialQuestUI questUi = EnsureComponent<InsectGame.UI.TutorialQuestUI>("UI/TutorialQuestUI");
            questUi.AutoWire(questManager);

            // 스토리 시스템 — 기존 이벤트(리전/배틀/서브에리어/퀘스트/진행/컬렉션)를 관찰해 비트 발화.
            // 싱글턴 아님(AutoWire). 렌더는 NpcDialogueUI가 StoryBeatTriggered 구독(아래 마을/NPC 블록에서 배선).
            InsectGame.Story.StoryDirector storyDirector =
                EnsureComponent<InsectGame.Story.StoryDirector>("World/StoryDirector");
            storyDirector.AutoWire(regionMgr, battleController, progress, insectCollection, questManager);
            storyDirector.AutoWire(candyInventory, itemInventory);
            cloudSave.RegisterReloadable(storyDirector);

            // 캐시 상점 + 가챠 시스템
            CashShopManager cashShop = EnsureComponent<CashShopManager>("World/CashShop");
            cashShop.AutoWire(wallet); // 보석 동기화 (PlayerCurrencyWallet ↔ CashShopManager)
            // 실결제(Google Play Billing) 어댑터 — Start에서 CashShopManager에 공급자 등록.
            // Unity IAP 상품 조회 + 서버 검증 URL 준비 전에는 IsReady=false(프로덕션 구매 비활성).
            EnsureComponent<IAPManager>("World/IAPManager");
            GachaBoxManager gachaBox = EnsureComponent<GachaBoxManager>("World/GachaBox");
            gachaBox.AutoWire(insectCollection, candyInventory);
            gachaBox.AutoWire(database); // PickRandomInsect 결과 검증 + DisplayName 캐싱

            InsectGame.UI.CashShopUI cashShopUI = EnsureComponent<InsectGame.UI.CashShopUI>("UI/CashShopUI");
            InsectGame.UI.CharacterViewerUI viewerUI = EnsureComponent<InsectGame.UI.CharacterViewerUI>("UI/CharacterViewerUI");

            quickBar.AutoWire(viewerUI, outfitUi, cashShopUI);
            quickBar.AutoWire(questUi);

            // 마스터 계정이면 보석 99999 지급
            if (AuthManager.Instance != null && AuthManager.Instance.IsMasterAccount)
            {
                if (cashShop != null) cashShop.AddGems(99999 - cashShop.Gems);
            }

            // 마을 + NPC 시스템 — 건물(상점/훈련소/가챠)과 NPC(주민/곤충 잡는 아이)를 월드에 배치.
            // 상호작용은 cashShopUI/trainingUi 생성 이후여야 하므로 이 위치(오디오 앞)에 등록.
            // try 격리: 프로시저럴 빌더 예외가 이후의 튜닝/E키 양보 배선과 AudioManager 생성까지
            // 연쇄 스킵시키지 않도록 (EnsureGround의 GroundStep 단계 격리와 같은 취지).
            if (buildWorld)
            {
                try
                {
                VillageBuilder village = EnsureComponent<VillageBuilder>("World/VillageBuilder");
                VillageBuildResult villageResult = village.Build(regionDefs);

                InsectGame.UI.WorldInteractionController worldInteract =
                    EnsureComponent<InsectGame.UI.WorldInteractionController>("UI/WorldInteraction");
                worldInteract.AutoWire(cashShopUI, trainingUi, playerMov);
                worldInteract.AutoWire(spawner);
                if (villageResult != null)
                {
                    worldInteract.RegisterPoints(villageResult.interactions);
                }

                InsectGame.NPC.NpcManager npcManager =
                    EnsureComponent<InsectGame.NPC.NpcManager>("World/NpcManager");
                npcManager.AutoWire(spawner, regionMgr, player.transform);
                worldInteract.AutoWire(npcManager);

                InsectGame.UI.NpcDialogueUI npcDialogue =
                    EnsureComponent<InsectGame.UI.NpcDialogueUI>("UI/NpcDialogueUI");
                npcDialogue.AutoWire(playerMov);
                npcDialogue.AutoWire(storyDirector); // 스토리 비트 lines[] 모달 렌더 + 닫힘 시 완료 콜백
                worldInteract.AutoWire(npcDialogue);

                // 스폰은 배선 완료 후 (컬링 타깃/예약 시스템이 준비된 상태에서)
                if (villageResult != null)
                {
                    npcManager.SpawnFromAnchors(villageResult.npcAnchors);
                }

                tuning.AutoWire(npcManager);          // NPC 수/쿨다운 튜닝 프로필 반영
                inputController.AutoWire(worldInteract); // 건물/NPC 근접 시 잡기 E키 양보
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[PlaySceneBootstrap] 마을/NPC 초기화 실패 (게임은 정상 실행됩니다): {e.Message}");
                }
            }

            // 오디오는 모든 시스템 초기화 완료 후 별도 try-catch로
            try
            {
                AudioManager audioManager = EnsureComponent<AudioManager>("World/AudioManager");
                StartCoroutine(DelayedAudioStart(audioManager));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlaySceneBootstrap] 오디오 초기화 실패 (게임은 정상 실행됩니다): {e.Message}");
            }
        }

        private System.Collections.IEnumerator DelayedAudioStart(AudioManager audioManager)
        {
            yield return null; // 1프레임 대기 (씬 로딩 완료 후)
            yield return null; // 추가 1프레임 (렌더링 안정화)
            if (audioManager != null)
            {
                audioManager.PlayBGM(BgmType.Explore);
                audioManager.PlayAmbient("day");
                EnsureComponent<UIAudioBinder>("World/UIAudioBinder");
            }
        }

        private Camera EnsureCamera(Transform target)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camera = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
                camObj.tag = "MainCamera";
            }

            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.5f, 0.8f, 1f);
            // 기준 수직 FOV. 가로 화면(와이드)은 CameraFollower가 종횡비에 맞춰 줌인 보정.
            camera.fieldOfView = 60f;
            camera.transform.position = target.position + new Vector3(0f, 12f, -8f);
            camera.transform.LookAt(target.position + Vector3.up);

            CameraFollower follower = camera.GetComponent<CameraFollower>();
            if (follower == null)
            {
                follower = camera.gameObject.AddComponent<CameraFollower>();
            }
            follower.SetTarget(target);

            return camera;
        }

        private void EnsureLight()
        {
            // 그늘(그림자 지는 곳)이 새까매지지 않도록 그림자 강도를 낮추고 환경광을 약간 올린다.
            // 메인 필드는 Skybox 환경광이라 ambientIntensity가 그늘 밝기에 직접 기여.
            RenderSettings.ambientIntensity = Mathf.Max(RenderSettings.ambientIntensity, 1.25f);

            Light existing = FindFirstObjectByType<Light>();
            if (existing != null)
            {
                // 씬에 이미 있는 디렉셔널 광원: 그림자 강도만 완화(0.5)해 그늘을 밝힌다.
                if (existing.type == LightType.Directional)
                {
                    if (existing.shadows == LightShadows.None)
                        existing.shadows = LightShadows.Soft;
                    existing.shadowStrength = 0.5f;
                }
                return;
            }

            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.84f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.5f; // 1.0이면 그늘이 거의 검정 → 0.5로 완화
            light.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
        }

        private GameObject EnsurePlayer()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.transform.position = new Vector3(0f, 0.1f, 0f);
            }

            player.tag = "Player";

            if (player.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = player.AddComponent<Rigidbody>();
                rb.isKinematic = true;
            }

            if (player.GetComponent<CapsuleCollider>() == null)
            {
                // 충돌 캡슐은 옛 값 유지 (시각은 7등신, 충돌은 옛 캡슐 분리).
                // 새 캡슐(height 2.5, center 1.2)로 변경 시 OverlapSphere 자기 자신 가드 실패로 이동 차단 회귀 발생.
                CapsuleCollider cc = player.AddComponent<CapsuleCollider>();
                cc.height = 2f;
                cc.radius = 0.5f;
                cc.center = new Vector3(0, 1f, 0);
            }

            if (player.GetComponent<PlayerMovement>() == null)
            {
                player.AddComponent<PlayerMovement>();
            }

            if (player.GetComponentInChildren<MeshFilter>() == null)
            {
                player.AddComponent<PlayerVisualBuilder>();
            }

            return player;
        }

        // BuildPlayerVisual + BuildHair 계열은 PlayerVisualBuilder.cs로 이전됨.
        // (필드 캐릭터 6.4등신 슬림 비례 + CharacterOutfitManager 연동을 위해 분리)
        // 호출처: EnsurePlayer()에서 player.AddComponent<PlayerVisualBuilder>().

        private void EnsureGround()
        {
            if (GameObject.Find("Ground") != null)
            {
                return;
            }

            Data.RegionData[] regionDefs = RegionDefinitions.CreateAll();

            // --- Base ground ---
            Material baseMat = CreateSafeMaterial(new Color(0.25f, 0.45f, 0.18f));
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            // WorldScale 1.5 확장 후 최원점(mountain -255 / ruins 277.5)과 경계벽 내면(±318.5)을
            // 전부 덮도록 ±320 — 벽 앞까지 걸어가도 무바닥(무한 낙하) 구간이 없어야 한다.
            ground.transform.localScale = new Vector3(64f, 1f, 64f);
            ground.GetComponent<MeshRenderer>().material = baseMat;

            // 회색필드 진단: Plane 빌트인 메시가 null이면 지형이 안 보이고 "MeshCollider does not have a valid mesh"
            // 엔진 에러가 난다(캐릭터는 Cube/Sphere/Capsule이라 영향 없음). 디바이스에서 이 로그/배너로 즉시 판별.
            MeshFilter groundMf = ground.GetComponent<MeshFilter>();
            if (groundMf == null || groundMf.sharedMesh == null)
            {
                groundError = "Plane 빌트인 메시 NULL — 지형 안보임/회색 + MeshCollider 에러 원인";
                Debug.LogError("[PlaySceneBootstrap] Ground Plane 메시가 NULL입니다. 빌트인 Plane 메시 누락(Android 스트리핑 의심) — 지형 렌더 불가.");
            }

            // --- Rolling hills (flattened Spheres) ---
            Material hillMat = CreateSafeMaterial(new Color(0.28f, 0.48f, 0.2f));
            Vector3[] hillPositions = {
                new Vector3(45f, 0f, -75f), new Vector3(-60f, 0f, 90f),
                new Vector3(105f, 0f, 105f), new Vector3(-90f, 0f, -60f),
                new Vector3(0f, 0f, 120f)
            };
            float[] hillScales = { 18f, 14f, 20f, 16f, 12f };
            for (int i = 0; i < hillPositions.Length; i++)
            {
                GameObject hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hill.name = $"Ground_Hill_{i}";
                float hs = hillScales[i];
                // -0.15hs는 정점이 지면(+리전 평면 0.08) 위로 거의 안 나와 언덕이 안 보였음 → -0.1hs (정점 ~0.075hs).
                hill.transform.position = hillPositions[i] + new Vector3(0f, -hs * 0.1f, 0f);
                hill.transform.localScale = new Vector3(hs * 2f, hs * 0.35f, hs * 2f);
                hill.GetComponent<MeshRenderer>().material = hillMat;
                Object.Destroy(hill.GetComponent<Collider>());
            }

            // --- Region ground planes with distinct textures ---
            for (int ri = 0; ri < regionDefs.Length; ri++)
            {
                var region = regionDefs[ri];
                Color col = region.themeColor;
                Material mat = CreateSafeMaterial(new Color(col.r * 0.5f + 0.1f, col.g * 0.5f + 0.1f, col.b * 0.4f + 0.08f));
                GameObject regionGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
                regionGround.name = $"Region_{region.regionId}";
                regionGround.transform.position = region.centerPosition + new Vector3(0f, 0.08f, 0f);
                float s = region.radius / 5f;
                regionGround.transform.localScale = new Vector3(s, 1f, s);
                regionGround.GetComponent<MeshRenderer>().material = mat;
            }

            // --- Region boundary barriers ---
            Material barrierRockMat = CreateSafeMaterial(new Color(0.5f, 0.48f, 0.44f));
            Material barrierFenceMat = CreateSafeMaterial(new Color(0.5f, 0.35f, 0.15f));
            for (int ri = 0; ri < regionDefs.Length; ri++)
            {
                var region = regionDefs[ri];
                float bRad = region.radius * 0.85f;
                int barrierCount = 8;
                for (int bi = 0; bi < barrierCount; bi++)
                {
                    float ba = Mathf.PI * 2f * bi / barrierCount;
                    Vector3 bPos = region.centerPosition + new Vector3(Mathf.Cos(ba) * bRad, 0f, Mathf.Sin(ba) * bRad);

                    if (bi % 3 == 0)
                    {
                        // Large boulder barrier
                        GameObject boulder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        boulder.name = $"Barrier_{region.regionId}_{bi}_Rock";
                        float bs = Random.Range(1.2f, 2f);
                        boulder.transform.position = bPos + new Vector3(0f, bs * 0.2f, 0f);
                        boulder.transform.localScale = new Vector3(bs * 1.5f, bs * 0.6f, bs);
                        boulder.GetComponent<MeshRenderer>().material = barrierRockMat;
                    }
                    else
                    {
                        // Low fence post
                        GameObject fPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        fPost.name = $"Barrier_{region.regionId}_{bi}_Post";
                        fPost.transform.position = bPos + new Vector3(0f, 0.4f, 0f);
                        fPost.transform.localScale = new Vector3(0.15f, 0.8f, 0.15f);
                        fPost.GetComponent<MeshRenderer>().material = barrierFenceMat;
                        Object.Destroy(fPost.GetComponent<Collider>());
                    }
                }
            }

            // --- Paths (gravel style) ---
            Material pathMat = CreateSafeMaterial(new Color(0.6f, 0.5f, 0.35f));

            CreatePath(pathMat, Vector3.zero, regionDefs[1].centerPosition, 2.5f);
            CreatePath(pathMat, Vector3.zero, regionDefs[2].centerPosition, 2.5f);
            CreatePath(pathMat, Vector3.zero, regionDefs[3].centerPosition, 2.5f);

            // 단계별 격리: 한 빌더의 예외가 나머지 지형/수문장 생성을 막지 않도록 + 어느 단계가 실패했는지 로깅.
            GroundStep("AddSceneryObjects", () => AddSceneryObjects(baseMat));
            GroundStep("AddRegionScenery", () => AddRegionScenery(regionDefs));

            // 지형 구축 (고저차, 절벽, 강, 다리)
            GroundStep("WorldTerrainBuilder", () =>
            {
                WorldTerrainBuilder terrainBuilder = new GameObject("WorldTerrainBuilder").AddComponent<WorldTerrainBuilder>();
                terrainBuilder.BuildTerrain(regionDefs);
            });

            // 리전별 게임 필드 지형 (언덕, 길, 바위, 나무 등)
            GroundStep("RegionTerrainBuilder", () =>
            {
                RegionTerrainBuilder regionTerrain = new GameObject("RegionTerrainBuilder").AddComponent<RegionTerrainBuilder>();
                regionTerrain.BuildAllRegions(regionDefs);
            });

            // 수문장은 BuildSystems에서 database와 함께 생성
            GroundStep("CreateSubAreaEntries", () => CreateSubAreaEntries(regionDefs));
        }

        // 지형 생성 단계 격리 실행 — 실패 시 해당 단계명+예외를 로그/배너로 남기고 다음 단계 진행.
        private void GroundStep(string stepName, System.Action step)
        {
            try
            {
                step();
            }
            catch (System.Exception e)
            {
                string msg = $"{stepName}: {e.GetType().Name}: {e.Message}";
                if (string.IsNullOrEmpty(groundError)) groundError = msg;
                Debug.LogError($"[PlaySceneBootstrap] 지형 단계 실패 — {msg}\n{e.StackTrace}");
            }
        }

        private void CreatePath(Material mat, Vector3 from, Vector3 to, float width)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            // Slightly curved path via 3 segments with offset midpoint
            Vector3 perpendicular = new Vector3(-dir.z, 0f, dir.x).normalized;
            float curveOffset = length * 0.06f;
            Vector3 midA = Vector3.Lerp(from, to, 0.33f) + perpendicular * curveOffset;
            Vector3 midB = Vector3.Lerp(from, to, 0.66f) - perpendicular * curveOffset * 0.5f;
            Vector3[] segments = { from, midA, midB, to };

            Material edgeStoneMat = CreateSafeMaterial(new Color(0.55f, 0.5f, 0.42f));
            Material gravelMat = CreateSafeMaterial(new Color(0.58f, 0.52f, 0.4f));

            int pathIdx = 0;
            for (int seg = 0; seg < segments.Length - 1; seg++)
            {
                Vector3 segFrom = segments[seg];
                Vector3 segTo = segments[seg + 1];
                Vector3 segMid = (segFrom + segTo) / 2f;
                Vector3 segDir = segTo - segFrom;
                float segLen = segDir.magnitude;
                float segAngle = Mathf.Atan2(segDir.x, segDir.z) * Mathf.Rad2Deg;

                // Path plane segment
                GameObject path = GameObject.CreatePrimitive(PrimitiveType.Plane);
                path.name = $"Path_Seg_{seg}";
                path.transform.position = segMid + new Vector3(0f, 0.12f, 0f);
                path.transform.rotation = Quaternion.Euler(0f, segAngle, 0f);
                path.transform.localScale = new Vector3(width / 10f, 1f, segLen / 10f);
                path.GetComponent<MeshRenderer>().material = mat;

                // Gravel stones along path
                Vector3 segNorm = segDir.normalized;
                int gravelCount = Mathf.Max(3, (int)(segLen / 3f));
                for (int g = 0; g < gravelCount; g++)
                {
                    float t = (g + 0.5f) / gravelCount;
                    Vector3 gPos = Vector3.Lerp(segFrom, segTo, t);
                    float gOff = Random.Range(-width * 0.35f, width * 0.35f);
                    Vector3 segPerp = new Vector3(-segDir.z, 0f, segDir.x).normalized;
                    gPos += segPerp * gOff;

                    GameObject gravel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    gravel.name = $"Path_Gravel_{pathIdx}";
                    float gs = Random.Range(0.15f, 0.3f);
                    gravel.transform.position = gPos + new Vector3(0f, gs * 0.15f + 0.1f, 0f);
                    gravel.transform.localScale = new Vector3(gs * 1.3f, gs * 0.3f, gs);
                    gravel.GetComponent<MeshRenderer>().material = gravelMat;
                    Object.Destroy(gravel.GetComponent<Collider>());
                    pathIdx++;
                }

                // Edge stones on both sides
                int edgeCount = Mathf.Max(2, (int)(segLen / 5f));
                for (int e = 0; e < edgeCount; e++)
                {
                    float t = (e + 0.5f) / edgeCount;
                    Vector3 eBase = Vector3.Lerp(segFrom, segTo, t);
                    Vector3 segPerpE = new Vector3(-segDir.z, 0f, segDir.x).normalized;

                    for (int side = -1; side <= 1; side += 2)
                    {
                        GameObject edgeStone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        edgeStone.name = $"Path_Edge_{pathIdx}";
                        float es = Random.Range(0.2f, 0.35f);
                        edgeStone.transform.position = eBase + segPerpE * (side * width * 0.5f) + new Vector3(0f, es * 0.2f + 0.01f, 0f);
                        edgeStone.transform.localScale = new Vector3(es, es * 0.4f, es);
                        edgeStone.GetComponent<MeshRenderer>().material = edgeStoneMat;
                        Object.Destroy(edgeStone.GetComponent<Collider>());
                        pathIdx++;
                    }
                }
            }
        }

        private void AddRegionScenery(Data.RegionData[] regions)
        {
            foreach (var r in regions)
            {
                Vector3 c = r.centerPosition;
                float rad = r.radius * 0.7f;

                Material signMat = CreateSafeMaterial(new Color(0.55f, 0.35f, 0.15f));
                Material signBoardMat = CreateSafeMaterial(r.themeColor);

                GameObject signPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                signPost.name = $"Sign_{r.regionId}_Post";
                signPost.transform.position = c + new Vector3(-rad * 0.8f, 1f, -rad * 0.8f);
                signPost.transform.localScale = new Vector3(0.15f, 1f, 0.15f);
                signPost.GetComponent<MeshRenderer>().material = signMat;

                GameObject signBoard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                signBoard.name = $"Sign_{r.regionId}_Board";
                signBoard.transform.position = c + new Vector3(-rad * 0.8f, 2.2f, -rad * 0.8f);
                signBoard.transform.localScale = new Vector3(2f, 0.8f, 0.1f);
                signBoard.GetComponent<MeshRenderer>().material = signBoardMat;

                if (r.regionId == "meadow")
                {
                    AddMeadowScenery(c, rad);
                }
                else if (r.regionId == "pond")
                {
                    AddPondScenery(c, rad);
                }
                else if (r.regionId == "forest")
                {
                    AddForestScenery(c, rad);
                }
                else if (r.regionId == "swamp")
                {
                    AddSwampScenery(c, rad);
                }
                else if (r.regionId == "mountain")
                {
                    AddMountainScenery(c, rad);
                }
                else if (r.regionId == "garden")
                {
                    AddGardenScenery(c, rad);
                }
                else if (r.regionId == "ruins")
                {
                    AddRuinsScenery(c, rad);
                }
            }
        }

        private void AddMeadowScenery(Vector3 c, float rad)
        {
            Material fenceMat = CreateSafeMaterial(new Color(0.55f, 0.38f, 0.18f));
            Material wildflowerStemMat = CreateSafeMaterial(new Color(0.25f, 0.6f, 0.2f));
            Material benchWoodMat = CreateSafeMaterial(new Color(0.5f, 0.35f, 0.15f));
            Material windmillMat = CreateSafeMaterial(new Color(0.7f, 0.65f, 0.55f));
            Material windmillBladeMat = CreateSafeMaterial(new Color(0.85f, 0.82f, 0.75f));
            Material grassBladeMat = CreateSafeMaterial(new Color(0.3f, 0.62f, 0.22f));
            Material rockMat = CreateSafeMaterial(new Color(0.55f, 0.52f, 0.48f));
            Material scarecrowWoodMat = CreateSafeMaterial(new Color(0.5f, 0.35f, 0.12f));
            Material scarecrowHatMat = CreateSafeMaterial(new Color(0.35f, 0.22f, 0.08f));
            Material puddleMat = CreateSafeMaterial(new Color(0.3f, 0.5f, 0.7f, 0.5f));

            // --- Existing fence ---
            for (int i = 0; i < 6; i++)
            {
                float fenceX = -rad * 0.6f + i * (rad * 0.2f);
                Vector3 fencePos = c + new Vector3(fenceX, 0f, rad * 0.7f);

                GameObject post1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post1.name = $"Meadow_Fence_{i}_Post1";
                post1.transform.position = fencePos + new Vector3(0f, 0.5f, 0f);
                post1.transform.localScale = new Vector3(0.12f, 1f, 0.12f);
                post1.GetComponent<MeshRenderer>().material = fenceMat;
                Object.Destroy(post1.GetComponent<Collider>());

                GameObject post2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post2.name = $"Meadow_Fence_{i}_Post2";
                post2.transform.position = fencePos + new Vector3(rad * 0.2f, 0.5f, 0f);
                post2.transform.localScale = new Vector3(0.12f, 1f, 0.12f);
                post2.GetComponent<MeshRenderer>().material = fenceMat;
                Object.Destroy(post2.GetComponent<Collider>());

                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = $"Meadow_Fence_{i}_Rail";
                rail.transform.position = fencePos + new Vector3(rad * 0.1f, 0.6f, 0f);
                rail.transform.localScale = new Vector3(rad * 0.2f, 0.08f, 0.08f);
                rail.GetComponent<MeshRenderer>().material = fenceMat;
                Object.Destroy(rail.GetComponent<Collider>());

                GameObject railLow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                railLow.name = $"Meadow_Fence_{i}_RailLow";
                railLow.transform.position = fencePos + new Vector3(rad * 0.1f, 0.3f, 0f);
                railLow.transform.localScale = new Vector3(rad * 0.2f, 0.08f, 0.08f);
                railLow.GetComponent<MeshRenderer>().material = fenceMat;
                Object.Destroy(railLow.GetComponent<Collider>());
            }

            // --- Existing wildflowers ---
            Material[] wfColors = {
                CreateSafeMaterial(new Color(1f, 0.85f, 0.2f)),
                CreateSafeMaterial(new Color(0.95f, 0.5f, 0.6f)),
                CreateSafeMaterial(new Color(0.6f, 0.5f, 1f)),
                CreateSafeMaterial(new Color(1f, 1f, 0.4f))
            };
            for (int i = 0; i < 8; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(3f, rad * 0.6f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stem.name = $"Meadow_Wildflower_{i}_Stem";
                stem.transform.position = pos + new Vector3(0f, 0.3f, 0f);
                stem.transform.localScale = new Vector3(0.04f, 0.3f, 0.04f);
                stem.GetComponent<MeshRenderer>().material = wildflowerStemMat;
                Object.Destroy(stem.GetComponent<Collider>());

                GameObject petal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                petal.name = $"Meadow_Wildflower_{i}_Petal";
                float ps = Random.Range(0.2f, 0.35f);
                petal.transform.position = pos + new Vector3(0f, 0.65f, 0f);
                petal.transform.localScale = new Vector3(ps, ps * 0.5f, ps);
                petal.GetComponent<MeshRenderer>().material = wfColors[i % wfColors.Length];
                Object.Destroy(petal.GetComponent<Collider>());
            }

            // --- Existing benches ---
            for (int b = 0; b < 2; b++)
            {
                float bAngle = b == 0 ? 0.5f : 2.5f;
                Vector3 benchPos = c + new Vector3(Mathf.Cos(bAngle) * rad * 0.5f, 0f, Mathf.Sin(bAngle) * rad * 0.5f);

                GameObject seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seat.name = $"Meadow_Bench_{b}_Seat";
                seat.transform.position = benchPos + new Vector3(0f, 0.4f, 0f);
                seat.transform.localScale = new Vector3(1.5f, 0.1f, 0.5f);
                seat.GetComponent<MeshRenderer>().material = benchWoodMat;

                for (int leg = 0; leg < 4; leg++)
                {
                    GameObject l = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    l.name = $"Meadow_Bench_{b}_Leg_{leg}";
                    float lx = (leg % 2 == 0 ? -0.6f : 0.6f);
                    float lz = (leg < 2 ? -0.18f : 0.18f);
                    l.transform.position = benchPos + new Vector3(lx, 0.2f, lz);
                    l.transform.localScale = new Vector3(0.1f, 0.4f, 0.1f);
                    l.GetComponent<MeshRenderer>().material = benchWoodMat;
                    Object.Destroy(l.GetComponent<Collider>());
                }

                GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
                back.name = $"Meadow_Bench_{b}_Back";
                back.transform.position = benchPos + new Vector3(0f, 0.7f, -0.22f);
                back.transform.localScale = new Vector3(1.5f, 0.5f, 0.08f);
                back.GetComponent<MeshRenderer>().material = benchWoodMat;
                Object.Destroy(back.GetComponent<Collider>());
            }

            // --- Existing windmill ---
            Vector3 windmillPos = c + new Vector3(rad * 0.4f, 0f, -rad * 0.4f);
            GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tower.name = "Meadow_Windmill_Tower";
            tower.transform.position = windmillPos + new Vector3(0f, 3f, 0f);
            tower.transform.localScale = new Vector3(0.8f, 3f, 0.8f);
            tower.GetComponent<MeshRenderer>().material = windmillMat;

            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hub.name = "Meadow_Windmill_Hub";
            hub.transform.position = windmillPos + new Vector3(0f, 6.2f, 0f);
            hub.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            hub.GetComponent<MeshRenderer>().material = windmillMat;
            Object.Destroy(hub.GetComponent<Collider>());

            for (int bl = 0; bl < 4; bl++)
            {
                GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.name = $"Meadow_Windmill_Blade_{bl}";
                float bla = bl * 90f;
                blade.transform.position = windmillPos + new Vector3(0f, 6.2f, 0f);
                blade.transform.localScale = new Vector3(0.3f, 2.5f, 0.08f);
                blade.transform.rotation = Quaternion.Euler(0f, 0f, bla);
                blade.GetComponent<MeshRenderer>().material = windmillBladeMat;
                Object.Destroy(blade.GetComponent<Collider>());
            }

            // --- NEW: Tall grass blades (thin tall Cubes) ---
            for (int i = 0; i < 20; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(2f, rad * 0.7f);
                Vector3 gPos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float gh = Random.Range(0.5f, 1.2f);

                GameObject grass = GameObject.CreatePrimitive(PrimitiveType.Cube);
                grass.name = $"Meadow_Grass_{i}";
                grass.transform.position = gPos + new Vector3(0f, gh * 0.5f, 0f);
                grass.transform.localScale = new Vector3(0.05f, gh, 0.05f);
                grass.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-8f, 8f));
                grass.GetComponent<MeshRenderer>().material = grassBladeMat;
                Object.Destroy(grass.GetComponent<Collider>());
            }

            // --- NEW: Large boulders for butterflies to land on ---
            Vector3[] boulderOffsets = {
                new Vector3(rad * 0.3f, 0f, rad * 0.2f),
                new Vector3(-rad * 0.4f, 0f, -rad * 0.15f),
                new Vector3(rad * 0.1f, 0f, -rad * 0.5f)
            };
            for (int i = 0; i < boulderOffsets.Length; i++)
            {
                GameObject boulder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                boulder.name = $"Meadow_Boulder_{i}";
                float bs = Random.Range(0.8f, 1.4f);
                boulder.transform.position = c + boulderOffsets[i] + new Vector3(0f, bs * 0.25f, 0f);
                boulder.transform.localScale = new Vector3(bs * 1.4f, bs * 0.55f, bs * 1.1f);
                boulder.GetComponent<MeshRenderer>().material = rockMat;
            }

            // --- NEW: Scarecrow ---
            Vector3 scarecrowPos = c + new Vector3(-rad * 0.3f, 0f, rad * 0.35f);
            GameObject scBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            scBody.name = "Meadow_Scarecrow_Post";
            scBody.transform.position = scarecrowPos + new Vector3(0f, 1.2f, 0f);
            scBody.transform.localScale = new Vector3(0.12f, 2.4f, 0.12f);
            scBody.GetComponent<MeshRenderer>().material = scarecrowWoodMat;
            Object.Destroy(scBody.GetComponent<Collider>());

            GameObject scArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            scArm.name = "Meadow_Scarecrow_Arm";
            scArm.transform.position = scarecrowPos + new Vector3(0f, 1.8f, 0f);
            scArm.transform.localScale = new Vector3(1.6f, 0.1f, 0.1f);
            scArm.GetComponent<MeshRenderer>().material = scarecrowWoodMat;
            Object.Destroy(scArm.GetComponent<Collider>());

            GameObject scHat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            scHat.name = "Meadow_Scarecrow_Hat";
            scHat.transform.position = scarecrowPos + new Vector3(0f, 2.6f, 0f);
            scHat.transform.localScale = new Vector3(0.6f, 0.15f, 0.6f);
            scHat.GetComponent<MeshRenderer>().material = scarecrowHatMat;
            Object.Destroy(scHat.GetComponent<Collider>());

            GameObject scHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            scHead.name = "Meadow_Scarecrow_Head";
            scHead.transform.position = scarecrowPos + new Vector3(0f, 2.35f, 0f);
            scHead.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            scHead.GetComponent<MeshRenderer>().material = scarecrowHatMat;
            Object.Destroy(scHead.GetComponent<Collider>());

            // --- NEW: Flower patches (3 clusters of 5 flowers each) ---
            for (int p = 0; p < 3; p++)
            {
                float pa = Mathf.PI * 2f * p / 3f + 0.8f;
                float pd = rad * 0.45f;
                Vector3 patchCenter = c + new Vector3(Mathf.Cos(pa) * pd, 0f, Mathf.Sin(pa) * pd);

                for (int f = 0; f < 5; f++)
                {
                    float fa = Mathf.PI * 2f * f / 5f;
                    float fd = Random.Range(0.4f, 1.2f);
                    Vector3 fPos = patchCenter + new Vector3(Mathf.Cos(fa) * fd, 0f, Mathf.Sin(fa) * fd);

                    GameObject fStem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    fStem.name = $"Meadow_Patch_{p}_Flower_{f}_Stem";
                    fStem.transform.position = fPos + new Vector3(0f, 0.25f, 0f);
                    fStem.transform.localScale = new Vector3(0.03f, 0.25f, 0.03f);
                    fStem.GetComponent<MeshRenderer>().material = wildflowerStemMat;
                    Object.Destroy(fStem.GetComponent<Collider>());

                    GameObject fHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    fHead.name = $"Meadow_Patch_{p}_Flower_{f}_Head";
                    float fhs = Random.Range(0.15f, 0.25f);
                    fHead.transform.position = fPos + new Vector3(0f, 0.55f, 0f);
                    fHead.transform.localScale = new Vector3(fhs, fhs * 0.5f, fhs);
                    fHead.GetComponent<MeshRenderer>().material = wfColors[(p + f) % wfColors.Length];
                    Object.Destroy(fHead.GetComponent<Collider>());
                }
            }

            // --- NEW: Small puddle ---
            GameObject puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            puddle.name = "Meadow_Puddle";
            puddle.transform.position = c + new Vector3(rad * 0.15f, 0.02f, rad * 0.3f);
            puddle.transform.localScale = new Vector3(2f, 0.02f, 2f);
            puddle.GetComponent<MeshRenderer>().material = puddleMat;
            Object.Destroy(puddle.GetComponent<Collider>());
        }

        private void AddPondScenery(Vector3 c, float rad)
        {
            Material waterMat = CreateSafeMaterial(new Color(0.2f, 0.4f, 0.7f, 0.6f));
            Material reedMat = CreateSafeMaterial(new Color(0.35f, 0.55f, 0.2f));
            Material reedTopMat = CreateSafeMaterial(new Color(0.5f, 0.4f, 0.2f));
            Material lilyPadMat = CreateSafeMaterial(new Color(0.15f, 0.5f, 0.1f));
            Material lilyFlowerMat = CreateSafeMaterial(new Color(1f, 0.6f, 0.7f));
            Material stoneMat = CreateSafeMaterial(new Color(0.6f, 0.6f, 0.55f));
            Material shoreMat = CreateSafeMaterial(new Color(0.5f, 0.5f, 0.45f));
            Material bridgeWoodMat = CreateSafeMaterial(new Color(0.45f, 0.32f, 0.14f));
            Material willowTrunkMat = CreateSafeMaterial(new Color(0.3f, 0.22f, 0.1f));
            Material willowLeafMat = CreateSafeMaterial(new Color(0.2f, 0.5f, 0.15f));
            Material frogMat = CreateSafeMaterial(new Color(0.2f, 0.6f, 0.15f));
            Material rippleMat = CreateSafeMaterial(new Color(0.4f, 0.6f, 0.85f, 0.3f));

            // --- Existing water ---
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "Pond_Water";
            water.transform.position = c + new Vector3(0f, 0.05f, 0f);
            water.transform.localScale = new Vector3(15f, 0.05f, 15f);
            water.GetComponent<MeshRenderer>().material = waterMat;

            // --- Existing reeds (10) ---
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 2f * i / 10f + Random.Range(-0.2f, 0.2f);
                float d = rad * 0.45f + Random.Range(-1f, 1f);
                Vector3 reedPos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                float h = Random.Range(1.2f, 2f);
                GameObject reedStalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                reedStalk.name = $"Pond_Reed_{i}_Stalk";
                reedStalk.transform.position = reedPos + new Vector3(0f, h * 0.5f, 0f);
                reedStalk.transform.localScale = new Vector3(0.06f, h * 0.5f, 0.06f);
                reedStalk.GetComponent<MeshRenderer>().material = reedMat;
                Object.Destroy(reedStalk.GetComponent<Collider>());

                GameObject reedTop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                reedTop.name = $"Pond_Reed_{i}_Top";
                reedTop.transform.position = reedPos + new Vector3(0f, h + 0.15f, 0f);
                reedTop.transform.localScale = new Vector3(0.12f, 0.3f, 0.12f);
                reedTop.GetComponent<MeshRenderer>().material = reedTopMat;
                Object.Destroy(reedTop.GetComponent<Collider>());
            }

            // --- Existing lily pads ---
            for (int i = 0; i < 6; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(1.5f, rad * 0.35f);
                Vector3 lilyPos = c + new Vector3(Mathf.Cos(a) * d, 0.08f, Mathf.Sin(a) * d);

                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pad.name = $"Pond_LilyPad_{i}";
                pad.transform.position = lilyPos;
                float padSize = Random.Range(0.6f, 1f);
                pad.transform.localScale = new Vector3(padSize, 0.02f, padSize);
                pad.GetComponent<MeshRenderer>().material = lilyPadMat;
                Object.Destroy(pad.GetComponent<Collider>());

                if (i < 3)
                {
                    GameObject flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    flower.name = $"Pond_LilyFlower_{i}";
                    flower.transform.position = lilyPos + new Vector3(0f, 0.12f, 0f);
                    flower.transform.localScale = new Vector3(0.25f, 0.15f, 0.25f);
                    flower.GetComponent<MeshRenderer>().material = lilyFlowerMat;
                    Object.Destroy(flower.GetComponent<Collider>());
                }
            }

            // --- Existing stepping stones ---
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.PI * 0.3f + i * 0.6f;
                float d = rad * 0.2f;
                Vector3 stonePos = c + new Vector3(Mathf.Cos(a) * d, 0.1f, Mathf.Sin(a) * d);

                GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stone.name = $"Pond_SteppingStone_{i}";
                float ss = Random.Range(0.5f, 0.7f);
                stone.transform.position = stonePos;
                stone.transform.localScale = new Vector3(ss, 0.08f, ss);
                stone.GetComponent<MeshRenderer>().material = stoneMat;
                Object.Destroy(stone.GetComponent<Collider>());
            }

            // --- Existing shore rocks ---
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI * 2f * i / 6f + Random.Range(-0.3f, 0.3f);
                float d = rad * 0.5f + Random.Range(-0.5f, 0.5f);
                Vector3 rockPos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"Pond_ShoreRock_{i}";
                float rs = Random.Range(0.4f, 0.9f);
                rock.transform.position = rockPos + new Vector3(0f, rs * 0.2f, 0f);
                rock.transform.localScale = new Vector3(rs * 1.2f, rs * 0.5f, rs);
                rock.GetComponent<MeshRenderer>().material = shoreMat;
            }

            // --- NEW: Wooden bridge (arched planks over water) ---
            Vector3 bridgeStart = c + new Vector3(-rad * 0.3f, 0f, 0f);
            Vector3 bridgeEnd = c + new Vector3(rad * 0.3f, 0f, 0f);
            int plankCount = 8;
            for (int i = 0; i < plankCount; i++)
            {
                float t = (float)i / (plankCount - 1);
                Vector3 plankPos = Vector3.Lerp(bridgeStart, bridgeEnd, t);
                float archHeight = 0.8f * Mathf.Sin(t * Mathf.PI);
                plankPos.y = 0.15f + archHeight;

                GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.name = $"Pond_Bridge_Plank_{i}";
                plank.transform.position = plankPos;
                plank.transform.localScale = new Vector3(0.8f, 0.08f, 2f);
                plank.GetComponent<MeshRenderer>().material = bridgeWoodMat;
                Object.Destroy(plank.GetComponent<Collider>());
            }
            // Bridge railings
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject railing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                railing.name = $"Pond_Bridge_Rail_{(side > 0 ? "R" : "L")}";
                railing.transform.position = c + new Vector3(0f, 0.95f + 0.15f, side * 0.9f);
                railing.transform.localScale = new Vector3(rad * 0.6f, 0.08f, 0.06f);
                railing.GetComponent<MeshRenderer>().material = bridgeWoodMat;
                Object.Destroy(railing.GetComponent<Collider>());
            }

            // --- NEW: Weeping willows (2) ---
            Vector3[] willowPositions = {
                c + new Vector3(rad * 0.5f, 0f, rad * 0.35f),
                c + new Vector3(-rad * 0.45f, 0f, -rad * 0.3f)
            };
            for (int w = 0; w < willowPositions.Length; w++)
            {
                Vector3 wp = willowPositions[w];
                GameObject wTrunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wTrunk.name = $"Pond_Willow_{w}_Trunk";
                wTrunk.transform.position = wp + new Vector3(0f, 2f, 0f);
                wTrunk.transform.localScale = new Vector3(0.5f, 2f, 0.5f);
                wTrunk.GetComponent<MeshRenderer>().material = willowTrunkMat;

                // Drooping branches (cylinders angled downward)
                for (int br = 0; br < 6; br++)
                {
                    float ba = Mathf.PI * 2f * br / 6f;
                    GameObject branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    branch.name = $"Pond_Willow_{w}_Branch_{br}";
                    branch.transform.position = wp + new Vector3(Mathf.Cos(ba) * 1.2f, 2.8f, Mathf.Sin(ba) * 1.2f);
                    branch.transform.localScale = new Vector3(0.04f, 1.5f, 0.04f);
                    branch.transform.rotation = Quaternion.Euler(Random.Range(25f, 45f), ba * Mathf.Rad2Deg, 0f);
                    branch.GetComponent<MeshRenderer>().material = willowLeafMat;
                    Object.Destroy(branch.GetComponent<Collider>());
                }

                GameObject wCanopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                wCanopy.name = $"Pond_Willow_{w}_Canopy";
                wCanopy.transform.position = wp + new Vector3(0f, 4.2f, 0f);
                wCanopy.transform.localScale = new Vector3(3.5f, 2f, 3.5f);
                wCanopy.GetComponent<MeshRenderer>().material = willowLeafMat;
                Object.Destroy(wCanopy.GetComponent<Collider>());
            }

            // --- NEW: Frog on a rock ---
            Vector3 frogRockPos = c + new Vector3(-rad * 0.15f, 0f, rad * 0.25f);
            GameObject frogRock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            frogRock.name = "Pond_FrogRock_Base";
            frogRock.transform.position = frogRockPos + new Vector3(0f, 0.25f, 0f);
            frogRock.transform.localScale = new Vector3(1.2f, 0.5f, 1f);
            frogRock.GetComponent<MeshRenderer>().material = stoneMat;

            GameObject frog = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            frog.name = "Pond_Frog";
            frog.transform.position = frogRockPos + new Vector3(0f, 0.6f, 0f);
            frog.transform.localScale = new Vector3(0.3f, 0.2f, 0.35f);
            frog.GetComponent<MeshRenderer>().material = frogMat;
            Object.Destroy(frog.GetComponent<Collider>());

            // --- NEW: Dense reed cluster (one side, 10 extra) ---
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.8f + Random.Range(-0.4f, 0.4f);
                float d = rad * 0.4f + Random.Range(-2f, 2f);
                Vector3 rPos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float rh = Random.Range(1f, 1.8f);

                GameObject denseReed = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                denseReed.name = $"Pond_DenseReed_{i}";
                denseReed.transform.position = rPos + new Vector3(0f, rh * 0.5f, 0f);
                denseReed.transform.localScale = new Vector3(0.05f, rh * 0.5f, 0.05f);
                denseReed.GetComponent<MeshRenderer>().material = reedMat;
                Object.Destroy(denseReed.GetComponent<Collider>());
            }

            // --- NEW: Water ripple rings (concentric thin cylinders) ---
            for (int r = 0; r < 3; r++)
            {
                float rippleRad = 2f + r * 2.5f;
                GameObject ripple = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ripple.name = $"Pond_Ripple_{r}";
                ripple.transform.position = c + new Vector3(0f, 0.07f, 0f);
                ripple.transform.localScale = new Vector3(rippleRad, 0.01f, rippleRad);
                ripple.GetComponent<MeshRenderer>().material = rippleMat;
                Object.Destroy(ripple.GetComponent<Collider>());
            }
        }

        private void AddForestScenery(Vector3 c, float rad)
        {
            Material trunkMat = CreateSafeMaterial(new Color(0.35f, 0.22f, 0.1f));
            Material leafMat = CreateSafeMaterial(new Color(0.1f, 0.4f, 0.08f));
            Material darkLeafMat = CreateSafeMaterial(new Color(0.06f, 0.3f, 0.05f));
            Material bushMat = CreateSafeMaterial(new Color(0.14f, 0.42f, 0.1f));
            Material mossyMat = CreateSafeMaterial(new Color(0.3f, 0.5f, 0.25f));
            Material logMat = CreateSafeMaterial(new Color(0.35f, 0.25f, 0.1f));
            Material caveStoneMat = CreateSafeMaterial(new Color(0.4f, 0.38f, 0.35f));
            Material webMat = CreateSafeMaterial(new Color(0.95f, 0.95f, 0.95f, 0.35f));
            Material rootMat = CreateSafeMaterial(new Color(0.32f, 0.2f, 0.08f));

            // --- Existing trees (mix of big and small) ---
            for (int i = 0; i < 12; i++)
            {
                float a = Mathf.PI * 2f * i / 12;
                float d = Random.Range(rad * 0.3f, rad * 0.85f);
                Vector3 treePos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                bool isBigTree = (i % 3 == 0);
                float trunkHeight = isBigTree ? 3.5f : 2f;
                float trunkWidth = isBigTree ? 0.8f : 0.5f;

                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = $"Forest_Tree_{i}_Trunk";
                trunk.transform.position = treePos + new Vector3(0f, trunkHeight, 0f);
                trunk.transform.localScale = new Vector3(trunkWidth, trunkHeight, trunkWidth);
                trunk.GetComponent<MeshRenderer>().material = trunkMat;

                GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leaves.name = $"Forest_Tree_{i}_Leaves";
                float ls = isBigTree ? Random.Range(4f, 5.5f) : Random.Range(2.5f, 4f);
                float leafY = isBigTree ? 7.5f : 5f;
                leaves.transform.position = treePos + new Vector3(0f, leafY, 0f);
                leaves.transform.localScale = new Vector3(ls, ls * 0.7f, ls);
                leaves.GetComponent<MeshRenderer>().material = i % 3 == 0 ? darkLeafMat : leafMat;

                // Tree roots for big trees
                if (isBigTree)
                {
                    for (int rt = 0; rt < 4; rt++)
                    {
                        float ra = Mathf.PI * 2f * rt / 4f + Random.Range(-0.3f, 0.3f);
                        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        root.name = $"Forest_Tree_{i}_Root_{rt}";
                        root.transform.position = treePos + new Vector3(Mathf.Cos(ra) * 0.6f, 0.1f, Mathf.Sin(ra) * 0.6f);
                        root.transform.localScale = new Vector3(0.12f, 0.15f, 0.12f);
                        root.transform.rotation = Quaternion.Euler(Random.Range(50f, 70f), ra * Mathf.Rad2Deg, 0f);
                        root.GetComponent<MeshRenderer>().material = rootMat;
                        Object.Destroy(root.GetComponent<Collider>());
                    }
                }
            }

            // --- Existing fallen logs ---
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.PI * 2f * (i + 0.5f) / 3f;
                float d = Random.Range(rad * 0.25f, rad * 0.6f);
                Vector3 logPos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                CreateLog(logPos, Random.Range(2.5f, 4f), a * Mathf.Rad2Deg + Random.Range(-20f, 20f), logMat, $"Forest_FallenLog_{i}");
            }

            // --- Existing mushrooms ---
            for (int i = 0; i < 6; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(rad * 0.2f, rad * 0.7f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                CreateMushroom(pos, Random.Range(0.25f, 0.5f), $"Forest_Mushroom_{i}");
            }

            // --- Existing bushes ---
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.PI * 2f * i / 8f + Random.Range(-0.2f, 0.2f);
                float d = Random.Range(rad * 0.35f, rad * 0.75f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                CreateBush(pos, Random.Range(1f, 1.8f), bushMat, $"Forest_Bush_{i}");
            }

            // --- Existing mossy rocks (more of them now) ---
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI * 2f * i / 6f + 0.4f;
                float d = Random.Range(rad * 0.3f, rad * 0.65f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"Forest_MossyRock_{i}";
                float rs = Random.Range(0.6f, 1.2f);
                rock.transform.position = pos + new Vector3(0f, rs * 0.25f, 0f);
                rock.transform.localScale = new Vector3(rs * 1.1f, rs * 0.55f, rs);
                rock.GetComponent<MeshRenderer>().material = mossyMat;
            }

            // --- NEW: Fallen tree with moss ---
            Vector3 fallenPos = c + new Vector3(rad * 0.2f, 0f, -rad * 0.3f);
            GameObject fallenTrunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fallenTrunk.name = "Forest_FallenTree_Trunk";
            fallenTrunk.transform.position = fallenPos + new Vector3(0f, 0.4f, 0f);
            fallenTrunk.transform.localScale = new Vector3(0.6f, 3f, 0.6f);
            fallenTrunk.transform.rotation = Quaternion.Euler(0f, 35f, 88f);
            fallenTrunk.GetComponent<MeshRenderer>().material = logMat;

            GameObject fallenMoss = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallenMoss.name = "Forest_FallenTree_Moss";
            fallenMoss.transform.position = fallenPos + new Vector3(0.5f, 0.55f, 0f);
            fallenMoss.transform.localScale = new Vector3(2f, 0.08f, 0.7f);
            fallenMoss.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            fallenMoss.GetComponent<MeshRenderer>().material = mossyMat;
            Object.Destroy(fallenMoss.GetComponent<Collider>());

            // --- NEW: Cave entrance (arch of stones) ---
            Vector3 cavePos = c + new Vector3(-rad * 0.5f, 0f, rad * 0.15f);
            GameObject caveLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            caveLeft.name = "Forest_Cave_Left";
            caveLeft.transform.position = cavePos + new Vector3(0f, 1.2f, -1f);
            caveLeft.transform.localScale = new Vector3(1.2f, 2.4f, 0.8f);
            caveLeft.GetComponent<MeshRenderer>().material = caveStoneMat;

            GameObject caveRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            caveRight.name = "Forest_Cave_Right";
            caveRight.transform.position = cavePos + new Vector3(0f, 1.2f, 1f);
            caveRight.transform.localScale = new Vector3(1.2f, 2.4f, 0.8f);
            caveRight.GetComponent<MeshRenderer>().material = caveStoneMat;

            GameObject caveArch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            caveArch.name = "Forest_Cave_Arch";
            caveArch.transform.position = cavePos + new Vector3(0f, 2.5f, 0f);
            caveArch.transform.localScale = new Vector3(1.5f, 1f, 3f);
            caveArch.GetComponent<MeshRenderer>().material = caveStoneMat;

            // --- NEW: Spider webs between trees (thin white translucent Cubes) ---
            for (int i = 0; i < 3; i++)
            {
                float wa = Mathf.PI * 2f * i / 3f + 1.2f;
                float wd = rad * 0.5f;
                Vector3 wPos = c + new Vector3(Mathf.Cos(wa) * wd, 2.5f, Mathf.Sin(wa) * wd);

                GameObject web = GameObject.CreatePrimitive(PrimitiveType.Cube);
                web.name = $"Forest_Web_{i}";
                web.transform.position = wPos;
                web.transform.localScale = new Vector3(2f, 1.5f, 0.02f);
                web.transform.rotation = Quaternion.Euler(0f, wa * Mathf.Rad2Deg, 0f);
                web.GetComponent<MeshRenderer>().material = webMat;
                Object.Destroy(web.GetComponent<Collider>());
            }
        }

        private void AddGardenScenery(Vector3 c, float rad)
        {
            Material stemMat = CreateSafeMaterial(new Color(0.2f, 0.6f, 0.15f));
            Material borderMat = CreateSafeMaterial(new Color(0.45f, 0.3f, 0.12f));
            Material archMat = CreateSafeMaterial(new Color(0.85f, 0.82f, 0.75f));
            Material decoStoneMat1 = CreateSafeMaterial(new Color(0.7f, 0.55f, 0.9f));
            Material decoStoneMat2 = CreateSafeMaterial(new Color(0.4f, 0.8f, 0.7f));
            Material decoStoneMat3 = CreateSafeMaterial(new Color(0.9f, 0.7f, 0.4f));
            Material fountainStoneMat = CreateSafeMaterial(new Color(0.65f, 0.62f, 0.58f));
            Material fountainWaterMat = CreateSafeMaterial(new Color(0.4f, 0.6f, 0.85f, 0.5f));
            Material potMat = CreateSafeMaterial(new Color(0.6f, 0.35f, 0.15f));
            Material lanternStoneMat = CreateSafeMaterial(new Color(0.6f, 0.58f, 0.54f));
            Material lanternGlowMat = CreateSafeMaterial(new Color(1f, 0.95f, 0.6f));
            Material benchWoodMat = CreateSafeMaterial(new Color(0.85f, 0.78f, 0.68f));
            Material sunflowerYellowMat = CreateSafeMaterial(new Color(1f, 0.85f, 0.1f));
            Material sunflowerCenterMat = CreateSafeMaterial(new Color(0.4f, 0.25f, 0.1f));
            Material tulipMat = CreateSafeMaterial(new Color(1f, 0.2f, 0.3f));
            Material butterflySculptMat = CreateSafeMaterial(new Color(0.7f, 0.5f, 0.9f));

            Material[] flowerMats = {
                CreateSafeMaterial(new Color(1f, 0.3f, 0.4f)),
                CreateSafeMaterial(new Color(1f, 0.7f, 0.2f)),
                CreateSafeMaterial(new Color(0.7f, 0.3f, 1f)),
                CreateSafeMaterial(new Color(1f, 0.5f, 0.7f))
            };

            // --- Existing flowers (diverse types now) ---
            for (int i = 0; i < 12; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(3f, rad * 0.8f);
                Vector3 flowerPos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject fStem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fStem.name = $"Garden_Flower_{i}_Stem";
                fStem.transform.position = flowerPos + new Vector3(0f, 0.4f, 0f);
                fStem.transform.localScale = new Vector3(0.05f, 0.4f, 0.05f);
                fStem.GetComponent<MeshRenderer>().material = stemMat;
                Object.Destroy(fStem.GetComponent<Collider>());

                GameObject petal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                petal.name = $"Garden_Flower_{i}_Petal";
                float ps = Random.Range(0.3f, 0.6f);
                petal.transform.position = flowerPos + new Vector3(0f, 0.9f, 0f);
                petal.transform.localScale = new Vector3(ps, ps * 0.5f, ps);
                petal.GetComponent<MeshRenderer>().material = flowerMats[i % flowerMats.Length];
                Object.Destroy(petal.GetComponent<Collider>());
            }

            // --- Sunflowers (3) ---
            for (int i = 0; i < 3; i++)
            {
                float sa = Mathf.PI * 2f * i / 3f + 2f;
                float sd = rad * 0.35f;
                Vector3 sfPos = c + new Vector3(Mathf.Cos(sa) * sd, 0f, Mathf.Sin(sa) * sd);

                GameObject sfStem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                sfStem.name = $"Garden_Sunflower_{i}_Stem";
                sfStem.transform.position = sfPos + new Vector3(0f, 0.8f, 0f);
                sfStem.transform.localScale = new Vector3(0.08f, 0.8f, 0.08f);
                sfStem.GetComponent<MeshRenderer>().material = stemMat;
                Object.Destroy(sfStem.GetComponent<Collider>());

                GameObject sfFace = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                sfFace.name = $"Garden_Sunflower_{i}_Face";
                sfFace.transform.position = sfPos + new Vector3(0f, 1.7f, 0f);
                sfFace.transform.localScale = new Vector3(0.7f, 0.04f, 0.7f);
                sfFace.GetComponent<MeshRenderer>().material = sunflowerYellowMat;
                Object.Destroy(sfFace.GetComponent<Collider>());

                GameObject sfCenter = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                sfCenter.name = $"Garden_Sunflower_{i}_Center";
                sfCenter.transform.position = sfPos + new Vector3(0f, 1.74f, 0f);
                sfCenter.transform.localScale = new Vector3(0.35f, 0.03f, 0.35f);
                sfCenter.GetComponent<MeshRenderer>().material = sunflowerCenterMat;
                Object.Destroy(sfCenter.GetComponent<Collider>());
            }

            // --- Tulips (4) ---
            for (int i = 0; i < 4; i++)
            {
                float ta = Mathf.PI * 2f * i / 4f + 0.7f;
                float td = rad * 0.55f;
                Vector3 tPos = c + new Vector3(Mathf.Cos(ta) * td, 0f, Mathf.Sin(ta) * td);

                GameObject tStem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tStem.name = $"Garden_Tulip_{i}_Stem";
                tStem.transform.position = tPos + new Vector3(0f, 0.3f, 0f);
                tStem.transform.localScale = new Vector3(0.04f, 0.3f, 0.04f);
                tStem.GetComponent<MeshRenderer>().material = stemMat;
                Object.Destroy(tStem.GetComponent<Collider>());

                GameObject tBud = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                tBud.name = $"Garden_Tulip_{i}_Bud";
                tBud.transform.position = tPos + new Vector3(0f, 0.75f, 0f);
                tBud.transform.localScale = new Vector3(0.18f, 0.25f, 0.18f);
                tBud.GetComponent<MeshRenderer>().material = tulipMat;
                Object.Destroy(tBud.GetComponent<Collider>());
            }

            // --- Existing border ---
            int borderCount = 24;
            for (int i = 0; i < borderCount; i++)
            {
                float a = Mathf.PI * 2f * i / borderCount;
                float d = rad * 0.75f;
                Vector3 borderPos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Garden_Border_{i}";
                block.transform.position = borderPos + new Vector3(0f, 0.1f, 0f);
                block.transform.localScale = new Vector3(0.4f, 0.2f, 0.4f);
                block.transform.rotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
                block.GetComponent<MeshRenderer>().material = borderMat;
                Object.Destroy(block.GetComponent<Collider>());
            }

            // --- Existing deco stones ---
            Material[] decoMats = { decoStoneMat1, decoStoneMat2, decoStoneMat3 };
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.PI * 2f * i / 3f + 1f;
                float d = rad * 0.5f;
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stone.name = $"Garden_DecoStone_{i}";
                float ss = Random.Range(0.5f, 0.8f);
                stone.transform.position = pos + new Vector3(0f, ss * 0.3f, 0f);
                stone.transform.localScale = new Vector3(ss, ss * 0.6f, ss);
                stone.GetComponent<MeshRenderer>().material = decoMats[i];
                Object.Destroy(stone.GetComponent<Collider>());
            }

            // --- Existing arch ---
            Vector3 archPos = c + new Vector3(-rad * 0.7f, 0f, 0f);
            GameObject archLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            archLeft.name = "Garden_Arch_Left";
            archLeft.transform.position = archPos + new Vector3(0f, 1.5f, -0.8f);
            archLeft.transform.localScale = new Vector3(0.15f, 3f, 0.15f);
            archLeft.GetComponent<MeshRenderer>().material = archMat;
            Object.Destroy(archLeft.GetComponent<Collider>());

            GameObject archRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            archRight.name = "Garden_Arch_Right";
            archRight.transform.position = archPos + new Vector3(0f, 1.5f, 0.8f);
            archRight.transform.localScale = new Vector3(0.15f, 3f, 0.15f);
            archRight.GetComponent<MeshRenderer>().material = archMat;
            Object.Destroy(archRight.GetComponent<Collider>());

            GameObject archTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            archTop.name = "Garden_Arch_Top";
            archTop.transform.position = archPos + new Vector3(0f, 3.1f, 0f);
            archTop.transform.localScale = new Vector3(0.15f, 0.15f, 1.75f);
            archTop.GetComponent<MeshRenderer>().material = archMat;
            Object.Destroy(archTop.GetComponent<Collider>());

            Material archFlowerMat = CreateSafeMaterial(new Color(1f, 0.4f, 0.5f));
            for (int i = 0; i < 6; i++)
            {
                float offY = 1f + i * 0.4f;
                float offZ = (i % 2 == 0 ? -0.8f : 0.8f);
                GameObject archFlower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                archFlower.name = $"Garden_ArchFlower_{i}";
                archFlower.transform.position = archPos + new Vector3(0f, offY, offZ * 0.9f);
                archFlower.transform.localScale = new Vector3(0.2f, 0.15f, 0.2f);
                archFlower.GetComponent<MeshRenderer>().material = archFlowerMat;
                Object.Destroy(archFlower.GetComponent<Collider>());
            }

            // --- Existing big flowers ---
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 2f * i / 4f + 0.3f;
                float d = Random.Range(rad * 0.2f, rad * 0.55f);
                Vector3 bigFlowerPos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject bigStem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bigStem.name = $"Garden_BigFlower_{i}_Stem";
                bigStem.transform.position = bigFlowerPos + new Vector3(0f, 0.7f, 0f);
                bigStem.transform.localScale = new Vector3(0.1f, 0.7f, 0.1f);
                bigStem.GetComponent<MeshRenderer>().material = stemMat;
                Object.Destroy(bigStem.GetComponent<Collider>());

                GameObject bigPetal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bigPetal.name = $"Garden_BigFlower_{i}_Petal";
                float bps = Random.Range(0.7f, 1.1f);
                bigPetal.transform.position = bigFlowerPos + new Vector3(0f, 1.6f, 0f);
                bigPetal.transform.localScale = new Vector3(bps, bps * 0.5f, bps);
                bigPetal.GetComponent<MeshRenderer>().material = flowerMats[i % flowerMats.Length];
                Object.Destroy(bigPetal.GetComponent<Collider>());
            }

            // --- NEW: Fountain (center) ---
            GameObject fountainBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fountainBase.name = "Garden_Fountain_Base";
            fountainBase.transform.position = c + new Vector3(0f, 0.3f, 0f);
            fountainBase.transform.localScale = new Vector3(2.5f, 0.3f, 2.5f);
            fountainBase.GetComponent<MeshRenderer>().material = fountainStoneMat;

            GameObject fountainPillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fountainPillar.name = "Garden_Fountain_Pillar";
            fountainPillar.transform.position = c + new Vector3(0f, 1.2f, 0f);
            fountainPillar.transform.localScale = new Vector3(0.4f, 0.9f, 0.4f);
            fountainPillar.GetComponent<MeshRenderer>().material = fountainStoneMat;

            GameObject fountainBowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fountainBowl.name = "Garden_Fountain_Bowl";
            fountainBowl.transform.position = c + new Vector3(0f, 2.2f, 0f);
            fountainBowl.transform.localScale = new Vector3(1.5f, 0.15f, 1.5f);
            fountainBowl.GetComponent<MeshRenderer>().material = fountainStoneMat;

            GameObject fountainWater = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fountainWater.name = "Garden_Fountain_Water";
            fountainWater.transform.position = c + new Vector3(0f, 2.6f, 0f);
            fountainWater.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
            fountainWater.GetComponent<MeshRenderer>().material = fountainWaterMat;
            Object.Destroy(fountainWater.GetComponent<Collider>());

            GameObject fountainTop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fountainTop.name = "Garden_Fountain_Top";
            fountainTop.transform.position = c + new Vector3(0f, 3.1f, 0f);
            fountainTop.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            fountainTop.GetComponent<MeshRenderer>().material = fountainStoneMat;
            Object.Destroy(fountainTop.GetComponent<Collider>());

            // --- NEW: Butterfly sculpture ---
            Vector3 sculptPos = c + new Vector3(rad * 0.4f, 0f, rad * 0.3f);
            GameObject sculptBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sculptBase.name = "Garden_ButterflySculpt_Base";
            sculptBase.transform.position = sculptPos + new Vector3(0f, 0.5f, 0f);
            sculptBase.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
            sculptBase.GetComponent<MeshRenderer>().material = fountainStoneMat;

            GameObject sculptBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            sculptBody.name = "Garden_ButterflySculpt_Body";
            sculptBody.transform.position = sculptPos + new Vector3(0f, 1.3f, 0f);
            sculptBody.transform.localScale = new Vector3(0.15f, 0.3f, 0.15f);
            sculptBody.GetComponent<MeshRenderer>().material = butterflySculptMat;
            Object.Destroy(sculptBody.GetComponent<Collider>());

            for (int wing = -1; wing <= 1; wing += 2)
            {
                GameObject sculptWing = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sculptWing.name = $"Garden_ButterflySculpt_Wing_{(wing > 0 ? "R" : "L")}";
                sculptWing.transform.position = sculptPos + new Vector3(wing * 0.35f, 1.35f, 0f);
                sculptWing.transform.localScale = new Vector3(0.5f, 0.35f, 0.08f);
                sculptWing.GetComponent<MeshRenderer>().material = butterflySculptMat;
                Object.Destroy(sculptWing.GetComponent<Collider>());
            }

            // --- NEW: Second vine arch (different position) ---
            Vector3 arch2Pos = c + new Vector3(rad * 0.6f, 0f, rad * 0.1f);
            GameObject arch2Left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arch2Left.name = "Garden_Arch2_Left";
            arch2Left.transform.position = arch2Pos + new Vector3(0f, 1.5f, -0.8f);
            arch2Left.transform.localScale = new Vector3(0.15f, 3f, 0.15f);
            arch2Left.GetComponent<MeshRenderer>().material = archMat;
            Object.Destroy(arch2Left.GetComponent<Collider>());

            GameObject arch2Right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arch2Right.name = "Garden_Arch2_Right";
            arch2Right.transform.position = arch2Pos + new Vector3(0f, 1.5f, 0.8f);
            arch2Right.transform.localScale = new Vector3(0.15f, 3f, 0.15f);
            arch2Right.GetComponent<MeshRenderer>().material = archMat;
            Object.Destroy(arch2Right.GetComponent<Collider>());

            GameObject arch2Top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arch2Top.name = "Garden_Arch2_Top";
            arch2Top.transform.position = arch2Pos + new Vector3(0f, 3.1f, 0f);
            arch2Top.transform.localScale = new Vector3(0.15f, 0.15f, 1.75f);
            arch2Top.GetComponent<MeshRenderer>().material = archMat;
            Object.Destroy(arch2Top.GetComponent<Collider>());

            // --- NEW: Flower pots (5) ---
            for (int i = 0; i < 5; i++)
            {
                float pa = Mathf.PI * 2f * i / 5f + 1.5f;
                float pd = rad * 0.3f;
                Vector3 potPos = c + new Vector3(Mathf.Cos(pa) * pd, 0f, Mathf.Sin(pa) * pd);

                GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pot.name = $"Garden_Pot_{i}";
                pot.transform.position = potPos + new Vector3(0f, 0.2f, 0f);
                pot.transform.localScale = new Vector3(0.4f, 0.2f, 0.4f);
                pot.GetComponent<MeshRenderer>().material = potMat;
                Object.Destroy(pot.GetComponent<Collider>());

                GameObject potFlower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                potFlower.name = $"Garden_Pot_{i}_Flower";
                potFlower.transform.position = potPos + new Vector3(0f, 0.5f, 0f);
                potFlower.transform.localScale = new Vector3(0.25f, 0.2f, 0.25f);
                potFlower.GetComponent<MeshRenderer>().material = flowerMats[i % flowerMats.Length];
                Object.Destroy(potFlower.GetComponent<Collider>());
            }

            // --- NEW: Stone lanterns (2) ---
            Vector3[] lanternPositions = {
                c + new Vector3(-rad * 0.35f, 0f, rad * 0.4f),
                c + new Vector3(rad * 0.3f, 0f, -rad * 0.45f)
            };
            for (int i = 0; i < lanternPositions.Length; i++)
            {
                Vector3 lp = lanternPositions[i];
                GameObject lPillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                lPillar.name = $"Garden_Lantern_{i}_Pillar";
                lPillar.transform.position = lp + new Vector3(0f, 0.6f, 0f);
                lPillar.transform.localScale = new Vector3(0.2f, 0.6f, 0.2f);
                lPillar.GetComponent<MeshRenderer>().material = lanternStoneMat;
                Object.Destroy(lPillar.GetComponent<Collider>());

                GameObject lHood = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lHood.name = $"Garden_Lantern_{i}_Hood";
                lHood.transform.position = lp + new Vector3(0f, 1.35f, 0f);
                lHood.transform.localScale = new Vector3(0.5f, 0.15f, 0.5f);
                lHood.GetComponent<MeshRenderer>().material = lanternStoneMat;
                Object.Destroy(lHood.GetComponent<Collider>());

                GameObject lGlow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lGlow.name = $"Garden_Lantern_{i}_Glow";
                lGlow.transform.position = lp + new Vector3(0f, 1.15f, 0f);
                lGlow.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                lGlow.GetComponent<MeshRenderer>().material = lanternGlowMat;
                Object.Destroy(lGlow.GetComponent<Collider>());
            }

            // --- NEW: Garden benches (2) ---
            Vector3[] gardenBenchPositions = {
                c + new Vector3(rad * 0.5f, 0f, -rad * 0.2f),
                c + new Vector3(-rad * 0.5f, 0f, -rad * 0.35f)
            };
            for (int b = 0; b < gardenBenchPositions.Length; b++)
            {
                Vector3 bp = gardenBenchPositions[b];
                GameObject bSeat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bSeat.name = $"Garden_Bench_{b}_Seat";
                bSeat.transform.position = bp + new Vector3(0f, 0.4f, 0f);
                bSeat.transform.localScale = new Vector3(1.4f, 0.08f, 0.5f);
                bSeat.GetComponent<MeshRenderer>().material = benchWoodMat;

                for (int leg = 0; leg < 4; leg++)
                {
                    GameObject bLeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bLeg.name = $"Garden_Bench_{b}_Leg_{leg}";
                    float lx = (leg % 2 == 0 ? -0.55f : 0.55f);
                    float lz = (leg < 2 ? -0.18f : 0.18f);
                    bLeg.transform.position = bp + new Vector3(lx, 0.2f, lz);
                    bLeg.transform.localScale = new Vector3(0.08f, 0.4f, 0.08f);
                    bLeg.GetComponent<MeshRenderer>().material = benchWoodMat;
                    Object.Destroy(bLeg.GetComponent<Collider>());
                }

                GameObject bBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bBack.name = $"Garden_Bench_{b}_Back";
                bBack.transform.position = bp + new Vector3(0f, 0.65f, -0.22f);
                bBack.transform.localScale = new Vector3(1.4f, 0.4f, 0.06f);
                bBack.GetComponent<MeshRenderer>().material = benchWoodMat;
                Object.Destroy(bBack.GetComponent<Collider>());
            }
        }

        private void AddSwampScenery(Vector3 c, float rad)
        {
            Material swampGroundMat = CreateSafeMaterial(new Color(0.2f, 0.28f, 0.12f));
            Material deadTreeMat = CreateSafeMaterial(new Color(0.35f, 0.25f, 0.15f));
            Material puddleMat = CreateSafeMaterial(new Color(0.15f, 0.25f, 0.18f, 0.7f));
            Material mushroomStemMat = CreateSafeMaterial(new Color(0.6f, 0.55f, 0.5f));
            Material mushroomCapMat = CreateSafeMaterial(new Color(0.5f, 0.15f, 0.6f));
            Material fogMat = CreateSafeMaterial(new Color(0.85f, 0.88f, 0.82f, 0.15f));
            Material mossMat = CreateSafeMaterial(new Color(0.18f, 0.45f, 0.12f));
            Material vineMat = CreateSafeMaterial(new Color(0.15f, 0.38f, 0.1f));

            // --- 물웅덩이 4개 (어두운 물색) ---
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 2f * i / 4f + 0.5f;
                float d = Random.Range(rad * 0.2f, rad * 0.6f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0.02f, Mathf.Sin(a) * d);

                GameObject puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                puddle.name = $"Swamp_Puddle_{i}";
                puddle.transform.position = pos;
                float ps = Random.Range(2f, 4f);
                puddle.transform.localScale = new Vector3(ps, 0.02f, ps);
                puddle.GetComponent<MeshRenderer>().material = puddleMat;
                Object.Destroy(puddle.GetComponent<Collider>());
            }

            // --- 고목(죽은 나무) 6그루 (줄기만, 잎 없음) ---
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI * 2f * i / 6f + 0.3f;
                float d = Random.Range(rad * 0.15f, rad * 0.7f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float h = Random.Range(1.5f, 3.5f);
                float tilt = Random.Range(-12f, 12f);

                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = $"Swamp_DeadTree_{i}";
                trunk.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
                trunk.transform.localScale = new Vector3(0.25f, h * 0.5f, 0.25f);
                trunk.transform.rotation = Quaternion.Euler(0f, 0f, tilt);
                trunk.GetComponent<MeshRenderer>().material = deadTreeMat;

                // 이끼/덩굴 매달기
                if (i % 2 == 0)
                {
                    GameObject vine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    vine.name = $"Swamp_Vine_{i}";
                    vine.transform.position = pos + new Vector3(0.2f, h * 0.6f, 0f);
                    vine.transform.localScale = new Vector3(0.04f, h * 0.3f, 0.04f);
                    vine.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(5f, 20f));
                    vine.GetComponent<MeshRenderer>().material = vineMat;
                    Object.Destroy(vine.GetComponent<Collider>());
                }
            }

            // --- 독버섯 5개 (보라색) ---
            for (int i = 0; i < 5; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(rad * 0.1f, rad * 0.65f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stem.name = $"Swamp_Mushroom_{i}_Stem";
                stem.transform.position = pos + new Vector3(0f, 0.15f, 0f);
                stem.transform.localScale = new Vector3(0.08f, 0.15f, 0.08f);
                stem.GetComponent<MeshRenderer>().material = mushroomStemMat;
                Object.Destroy(stem.GetComponent<Collider>());

                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = $"Swamp_Mushroom_{i}_Cap";
                float cs = Random.Range(0.25f, 0.45f);
                cap.transform.position = pos + new Vector3(0f, 0.35f, 0f);
                cap.transform.localScale = new Vector3(cs, cs * 0.4f, cs);
                cap.GetComponent<MeshRenderer>().material = mushroomCapMat;
                Object.Destroy(cap.GetComponent<Collider>());
            }

            // --- 안개 표현 (큰 반투명 Sphere 여러 개) ---
            for (int i = 0; i < 8; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(rad * 0.1f, rad * 0.75f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, Random.Range(0.3f, 1.2f), Mathf.Sin(a) * d);

                GameObject fog = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fog.name = $"Swamp_Fog_{i}";
                float fs = Random.Range(3f, 6f);
                fog.transform.position = pos;
                fog.transform.localScale = new Vector3(fs, fs * 0.3f, fs);
                fog.GetComponent<MeshRenderer>().material = fogMat;
                Object.Destroy(fog.GetComponent<Collider>());
            }

            // --- 이끼 패치 ---
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 2f * i / 4f + 1.2f;
                float d = Random.Range(rad * 0.2f, rad * 0.5f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0.03f, Mathf.Sin(a) * d);

                GameObject moss = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                moss.name = $"Swamp_Moss_{i}";
                float ms = Random.Range(1.5f, 3f);
                moss.transform.position = pos;
                moss.transform.localScale = new Vector3(ms, 0.02f, ms);
                moss.GetComponent<MeshRenderer>().material = mossMat;
                Object.Destroy(moss.GetComponent<Collider>());
            }
        }

        private void AddMountainScenery(Vector3 c, float rad)
        {
            Material rockMat = CreateSafeMaterial(new Color(0.5f, 0.48f, 0.45f));
            Material darkRockMat = CreateSafeMaterial(new Color(0.35f, 0.33f, 0.3f));
            Material pineTrunkMat = CreateSafeMaterial(new Color(0.4f, 0.28f, 0.15f));
            Material pineLeafMat = CreateSafeMaterial(new Color(0.12f, 0.35f, 0.1f));
            Material wildflowerYellowMat = CreateSafeMaterial(new Color(0.95f, 0.85f, 0.2f));
            Material wildflowerPurpleMat = CreateSafeMaterial(new Color(0.6f, 0.3f, 0.8f));
            Material flagPoleMat = CreateSafeMaterial(new Color(0.5f, 0.5f, 0.5f));
            Material flagMat = CreateSafeMaterial(new Color(0.9f, 0.15f, 0.1f));
            Material snowMat = CreateSafeMaterial(new Color(0.95f, 0.96f, 0.98f));

            // --- 바위 지형 (큰 Sphere들, 회색) ---
            for (int i = 0; i < 8; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(rad * 0.1f, rad * 0.7f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"Mountain_Rock_{i}";
                float rs = Random.Range(1f, 3f);
                rock.transform.position = pos + new Vector3(0f, rs * 0.3f, 0f);
                rock.transform.localScale = new Vector3(rs, rs * 0.7f, rs * 0.9f);
                rock.GetComponent<MeshRenderer>().material = (i % 2 == 0) ? rockMat : darkRockMat;
            }

            // --- 절벽 표현 (큰 Cube 계단식) ---
            for (int i = 0; i < 4; i++)
            {
                float offsetX = -rad * 0.3f + i * 2.5f;
                float height = 2f + i * 1.2f;
                Vector3 pos = c + new Vector3(offsetX, height * 0.5f, rad * 0.4f);

                GameObject cliff = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cliff.name = $"Mountain_Cliff_{i}";
                cliff.transform.position = pos;
                cliff.transform.localScale = new Vector3(2.5f, height, 3f);
                cliff.GetComponent<MeshRenderer>().material = darkRockMat;
            }

            // --- 소나무 5그루 ---
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.PI * 2f * i / 5f + 0.8f;
                float d = Random.Range(rad * 0.2f, rad * 0.6f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float h = Random.Range(2f, 4f);

                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = $"Mountain_Pine_{i}_Trunk";
                trunk.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
                trunk.transform.localScale = new Vector3(0.2f, h * 0.5f, 0.2f);
                trunk.GetComponent<MeshRenderer>().material = pineTrunkMat;

                // 삼각형 잎 = 큰 Sphere + 작은 줄기
                for (int layer = 0; layer < 3; layer++)
                {
                    GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    leaves.name = $"Mountain_Pine_{i}_Leaves_{layer}";
                    float ls = 1.4f - layer * 0.35f;
                    float ly = h + layer * 0.7f;
                    leaves.transform.position = pos + new Vector3(0f, ly, 0f);
                    leaves.transform.localScale = new Vector3(ls, ls * 0.6f, ls);
                    leaves.GetComponent<MeshRenderer>().material = pineLeafMat;
                    Object.Destroy(leaves.GetComponent<Collider>());
                }
            }

            // --- 야생화 (작은 노란/보라 꽃) ---
            Material[] wildflowerMats = { wildflowerYellowMat, wildflowerPurpleMat };
            for (int i = 0; i < 8; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(rad * 0.15f, rad * 0.55f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flower.name = $"Mountain_Wildflower_{i}";
                float fs = Random.Range(0.12f, 0.22f);
                flower.transform.position = pos + new Vector3(0f, 0.15f, 0f);
                flower.transform.localScale = new Vector3(fs, fs * 0.6f, fs);
                flower.GetComponent<MeshRenderer>().material = wildflowerMats[i % 2];
                Object.Destroy(flower.GetComponent<Collider>());
            }

            // --- 정상 깃발 ---
            Vector3 flagPos = c + new Vector3(0f, 0f, rad * 0.3f);

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Mountain_FlagPole";
            pole.transform.position = flagPos + new Vector3(0f, 2.5f, 0f);
            pole.transform.localScale = new Vector3(0.08f, 2.5f, 0.08f);
            pole.GetComponent<MeshRenderer>().material = flagPoleMat;

            GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = "Mountain_Flag";
            flag.transform.position = flagPos + new Vector3(0.4f, 4.5f, 0f);
            flag.transform.localScale = new Vector3(0.8f, 0.5f, 0.05f);
            flag.GetComponent<MeshRenderer>().material = flagMat;
            Object.Destroy(flag.GetComponent<Collider>());

            // --- 정상 눈 패치 ---
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.PI * 2f * i / 3f;
                float d = rad * 0.15f;
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0.04f, Mathf.Sin(a) * d + rad * 0.3f);

                GameObject snow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                snow.name = $"Mountain_Snow_{i}";
                float ss = Random.Range(1.5f, 2.5f);
                snow.transform.position = pos;
                snow.transform.localScale = new Vector3(ss, 0.02f, ss);
                snow.GetComponent<MeshRenderer>().material = snowMat;
                Object.Destroy(snow.GetComponent<Collider>());
            }
        }

        private void AddRuinsScenery(Vector3 c, float rad)
        {
            Material pillarMat = CreateSafeMaterial(new Color(0.6f, 0.58f, 0.52f));
            Material wallMat = CreateSafeMaterial(new Color(0.5f, 0.48f, 0.42f));
            Material mossFloorMat = CreateSafeMaterial(new Color(0.25f, 0.4f, 0.2f));
            Material statueMat = CreateSafeMaterial(new Color(0.45f, 0.42f, 0.38f));
            Material glowMat = CreateSafeMaterial(new Color(0.95f, 0.9f, 0.4f, 0.3f));
            Material archStoneMat = CreateSafeMaterial(new Color(0.55f, 0.52f, 0.46f));
            Material mossMat = CreateSafeMaterial(new Color(0.2f, 0.42f, 0.15f));

            // --- 깨진 기둥 8개 (일부 기울어짐) ---
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.PI * 2f * i / 8f + 0.4f;
                float d = Random.Range(rad * 0.15f, rad * 0.65f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float h = Random.Range(1.5f, 4f);
                float tiltX = (i % 3 == 0) ? Random.Range(-15f, 15f) : 0f;
                float tiltZ = (i % 3 == 1) ? Random.Range(-10f, 10f) : 0f;

                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"Ruins_Pillar_{i}";
                pillar.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
                pillar.transform.localScale = new Vector3(0.4f, h * 0.5f, 0.4f);
                pillar.transform.rotation = Quaternion.Euler(tiltX, 0f, tiltZ);
                pillar.GetComponent<MeshRenderer>().material = pillarMat;

                // 기둥 상단 이끼
                if (i % 2 == 0)
                {
                    GameObject moss = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    moss.name = $"Ruins_PillarMoss_{i}";
                    moss.transform.position = pos + new Vector3(0f, h + 0.1f, 0f);
                    moss.transform.localScale = new Vector3(0.5f, 0.15f, 0.5f);
                    moss.GetComponent<MeshRenderer>().material = mossMat;
                    Object.Destroy(moss.GetComponent<Collider>());
                }
            }

            // --- 돌벽 조각 4개 ---
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 2f * i / 4f + 1.8f;
                float d = rad * 0.5f;
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float w = Random.Range(2f, 4f);
                float h = Random.Range(1f, 2.5f);

                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Ruins_Wall_{i}";
                wall.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
                wall.transform.localScale = new Vector3(w, h, 0.4f);
                wall.transform.rotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
                wall.GetComponent<MeshRenderer>().material = wallMat;
            }

            // --- 아치문 2개 ---
            Vector3[] archPositions = {
                c + new Vector3(-rad * 0.35f, 0f, rad * 0.2f),
                c + new Vector3(rad * 0.3f, 0f, -rad * 0.25f)
            };
            for (int i = 0; i < archPositions.Length; i++)
            {
                Vector3 ap = archPositions[i];
                float archAngle = (i == 0) ? 30f : -20f;

                GameObject leftCol = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leftCol.name = $"Ruins_Arch_{i}_Left";
                leftCol.transform.position = ap + new Vector3(-1f, 1.5f, 0f);
                leftCol.transform.localScale = new Vector3(0.35f, 1.5f, 0.35f);
                leftCol.transform.rotation = Quaternion.Euler(0f, archAngle, 0f);
                leftCol.GetComponent<MeshRenderer>().material = archStoneMat;

                GameObject rightCol = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rightCol.name = $"Ruins_Arch_{i}_Right";
                rightCol.transform.position = ap + new Vector3(1f, 1.5f, 0f);
                rightCol.transform.localScale = new Vector3(0.35f, 1.5f, 0.35f);
                rightCol.transform.rotation = Quaternion.Euler(0f, archAngle, 0f);
                rightCol.GetComponent<MeshRenderer>().material = archStoneMat;

                GameObject archTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                archTop.name = $"Ruins_Arch_{i}_Top";
                archTop.transform.position = ap + new Vector3(0f, 3.2f, 0f);
                archTop.transform.localScale = new Vector3(2.5f, 0.35f, 0.4f);
                archTop.transform.rotation = Quaternion.Euler(0f, archAngle, 0f);
                archTop.GetComponent<MeshRenderer>().material = archStoneMat;
            }

            // --- 고대 석상 1개 (곤충 모양: 몸체 + 날개) ---
            Vector3 statuePos = c + new Vector3(0f, 0f, -rad * 0.15f);

            GameObject statueBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            statueBase.name = "Ruins_Statue_Base";
            statueBase.transform.position = statuePos + new Vector3(0f, 0.4f, 0f);
            statueBase.transform.localScale = new Vector3(1.5f, 0.8f, 1.5f);
            statueBase.GetComponent<MeshRenderer>().material = wallMat;

            GameObject statueBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            statueBody.name = "Ruins_Statue_Body";
            statueBody.transform.position = statuePos + new Vector3(0f, 1.6f, 0f);
            statueBody.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
            statueBody.GetComponent<MeshRenderer>().material = statueMat;

            for (int wing = -1; wing <= 1; wing += 2)
            {
                GameObject statueWing = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                statueWing.name = $"Ruins_Statue_Wing_{(wing > 0 ? "R" : "L")}";
                statueWing.transform.position = statuePos + new Vector3(wing * 0.6f, 1.8f, 0f);
                statueWing.transform.localScale = new Vector3(0.7f, 0.5f, 0.1f);
                statueWing.GetComponent<MeshRenderer>().material = statueMat;
                Object.Destroy(statueWing.GetComponent<Collider>());
            }

            // --- 이끼 낀 바닥 (초록빛 Plane 대체: Cylinder flat) ---
            for (int i = 0; i < 5; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(rad * 0.1f, rad * 0.6f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0.03f, Mathf.Sin(a) * d);

                GameObject mossFloor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mossFloor.name = $"Ruins_MossFloor_{i}";
                float ms = Random.Range(2f, 4f);
                mossFloor.transform.position = pos;
                mossFloor.transform.localScale = new Vector3(ms, 0.02f, ms);
                mossFloor.GetComponent<MeshRenderer>().material = mossFloorMat;
                Object.Destroy(mossFloor.GetComponent<Collider>());
            }

            // --- 신비로운 빛 (노란 반투명 Sphere) ---
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI * 2f * i / 6f + 0.7f;
                float d = Random.Range(rad * 0.15f, rad * 0.5f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, Random.Range(1f, 3f), Mathf.Sin(a) * d);

                GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glow.name = $"Ruins_Glow_{i}";
                float gs = Random.Range(0.3f, 0.6f);
                glow.transform.position = pos;
                glow.transform.localScale = new Vector3(gs, gs, gs);
                glow.GetComponent<MeshRenderer>().material = glowMat;
                Object.Destroy(glow.GetComponent<Collider>());
            }
        }

        private void AddSceneryObjects(Material grassMat)
        {
            Material treeTrunkMat = CreateSafeMaterial(new Color(0.45f, 0.3f, 0.15f));
            Material treeLeafMat = CreateSafeMaterial(new Color(0.15f, 0.5f, 0.1f));
            Material rockMat = CreateSafeMaterial(new Color(0.55f, 0.55f, 0.5f));
            Material bushMat = CreateSafeMaterial(new Color(0.2f, 0.55f, 0.15f));
            Material bushDarkMat = CreateSafeMaterial(new Color(0.12f, 0.4f, 0.1f));
            Material logMat = CreateSafeMaterial(new Color(0.4f, 0.28f, 0.12f));
            Material grassTuftMat = CreateSafeMaterial(new Color(0.3f, 0.6f, 0.2f));
            Material cloudMat = CreateSafeMaterial(new Color(1f, 1f, 1f, 0.4f));
            Material mountainMat = CreateSafeMaterial(new Color(0.35f, 0.42f, 0.3f));
            Material lampPostMat = CreateSafeMaterial(new Color(0.3f, 0.3f, 0.3f));
            Material lampGlowMat = CreateSafeMaterial(new Color(1f, 0.92f, 0.6f));

            // --- Existing trees ---
            Vector3[] treePositions = {
                new Vector3(10f, 0f, 8f), new Vector3(-12f, 0f, 5f),
                new Vector3(8f, 0f, -10f), new Vector3(-9f, 0f, -12f),
                new Vector3(15f, 0f, 0f), new Vector3(-15f, 0f, -5f),
                new Vector3(5f, 0f, 15f), new Vector3(-6f, 0f, 14f)
            };

            for (int i = 0; i < treePositions.Length; i++)
            {
                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = $"Tree_{i}_Trunk";
                trunk.transform.position = treePositions[i] + new Vector3(0f, 1.5f, 0f);
                trunk.transform.localScale = new Vector3(0.4f, 1.5f, 0.4f);
                trunk.GetComponent<MeshRenderer>().material = treeTrunkMat;

                GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leaves.name = $"Tree_{i}_Leaves";
                leaves.transform.position = treePositions[i] + new Vector3(0f, 4f, 0f);
                float s = Random.Range(1.8f, 2.8f);
                leaves.transform.localScale = new Vector3(s, s * 0.8f, s);
                leaves.GetComponent<MeshRenderer>().material = treeLeafMat;
            }

            // --- Existing rocks ---
            Vector3[] rockPositions = {
                new Vector3(6f, 0f, 3f), new Vector3(-4f, 0f, -8f),
                new Vector3(12f, 0f, -4f), new Vector3(-10f, 0f, 10f),
                // (-16,-10)은 마을 부지(중심 (-31,-13), 반경~16m) 안이라 (24,6)으로 이동 — 콜라이더 유지 바위가 마을 통행 방해
                new Vector3(18f, 0f, 12f), new Vector3(24f, 0f, 6f),
                new Vector3(20f, 0f, -8f), new Vector3(-8f, 0f, 18f),
                new Vector3(3f, 0f, -18f), new Vector3(-20f, 0f, 3f),
                new Vector3(14f, 0f, 16f), new Vector3(-14f, 0f, -16f)
            };

            for (int i = 0; i < rockPositions.Length; i++)
            {
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"Rock_{i}";
                float rs = Random.Range(0.5f, 1.4f);
                rock.transform.position = rockPositions[i] + new Vector3(0f, rs * 0.3f, 0f);
                rock.transform.localScale = new Vector3(rs * 1.3f, rs * 0.6f, rs);
                rock.GetComponent<MeshRenderer>().material = rockMat;
            }

            // --- Existing bushes ---
            Vector3[] bushPositions = {
                new Vector3(22f, 0f, 5f), new Vector3(-18f, 0f, 12f),
                new Vector3(7f, 0f, 22f), new Vector3(-7f, 0f, -20f),
                // (-22,-8)은 마을 광장 바로 밖 침범이라 (26,-14)로 이동
                new Vector3(25f, 0f, -3f), new Vector3(26f, 0f, -14f),
                new Vector3(16f, 0f, -18f), new Vector3(-16f, 0f, 20f),
                new Vector3(30f, 0f, 15f), new Vector3(-25f, 0f, 15f),
                new Vector3(12f, 0f, -25f), new Vector3(-12f, 0f, 25f)
            };
            for (int i = 0; i < bushPositions.Length; i++)
                CreateBush(bushPositions[i], Random.Range(1.2f, 2.2f), i % 2 == 0 ? bushMat : bushDarkMat, $"Bush_{i}");

            // --- Existing grass tufts ---
            for (int i = 0; i < 20; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(8f, 30f);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                CreateGrassTuft(pos, Random.Range(0.3f, 0.7f), grassTuftMat, $"Grass_{i}");
            }

            // --- Existing logs ---
            Vector3[] logPositions = {
                new Vector3(13f, 0f, 5f), new Vector3(-11f, 0f, -6f),
                new Vector3(4f, 0f, -14f), new Vector3(-5f, 0f, 12f)
            };
            for (int i = 0; i < logPositions.Length; i++)
                CreateLog(logPositions[i], Random.Range(2f, 3.5f), Random.Range(0f, 180f), logMat, $"Log_{i}");

            // --- Existing mushrooms ---
            for (int i = 0; i < 8; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(10f, 28f);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                CreateMushroom(pos, Random.Range(0.3f, 0.6f), $"Mushroom_{i}");
            }

            // --- NEW: Distant mountains/hills (background, flattened Spheres far out) ---
            Vector3[] mountainPositions = {
                new Vector3(160f, 0f, 0f), new Vector3(-150f, 0f, 50f),
                new Vector3(0f, 0f, 170f), new Vector3(100f, 0f, -140f)
            };
            float[] mountainScales = { 40f, 35f, 45f, 30f };
            for (int i = 0; i < mountainPositions.Length; i++)
            {
                GameObject mountain = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mountain.name = $"Scenery_Mountain_{i}";
                float ms = mountainScales[i];
                mountain.transform.position = mountainPositions[i] + new Vector3(0f, ms * 0.15f, 0f);
                mountain.transform.localScale = new Vector3(ms * 2f, ms * 0.6f, ms * 2f);
                mountain.GetComponent<MeshRenderer>().material = mountainMat;
                Object.Destroy(mountain.GetComponent<Collider>());
            }

            // --- NEW: Clouds (high altitude, white translucent Spheres) ---
            for (int i = 0; i < 5; i++)
            {
                float cx = Random.Range(-80f, 80f);
                float cz = Random.Range(-80f, 80f);
                float cy = Random.Range(50f, 60f);

                GameObject cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cloud.name = $"Scenery_Cloud_{i}";
                float cs = Random.Range(8f, 14f);
                cloud.transform.position = new Vector3(cx, cy, cz);
                cloud.transform.localScale = new Vector3(cs * 2f, cs * 0.4f, cs);
                cloud.GetComponent<MeshRenderer>().material = cloudMat;
                Object.Destroy(cloud.GetComponent<Collider>());
            }

            // --- NEW: Street lamps along paths (4) ---
            Vector3[] lampPositions = {
                new Vector3(25f, 0f, 8f), new Vector3(-20f, 0f, 20f),
                new Vector3(15f, 0f, -22f), new Vector3(-30f, 0f, -15f)
            };
            for (int i = 0; i < lampPositions.Length; i++)
            {
                Vector3 lp = lampPositions[i];
                GameObject lampPole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                lampPole.name = $"Scenery_Lamp_{i}_Pole";
                lampPole.transform.position = lp + new Vector3(0f, 2f, 0f);
                lampPole.transform.localScale = new Vector3(0.12f, 2f, 0.12f);
                lampPole.GetComponent<MeshRenderer>().material = lampPostMat;
                Object.Destroy(lampPole.GetComponent<Collider>());

                GameObject lampLight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lampLight.name = $"Scenery_Lamp_{i}_Light";
                lampLight.transform.position = lp + new Vector3(0f, 4.2f, 0f);
                lampLight.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                lampLight.GetComponent<MeshRenderer>().material = lampGlowMat;
                Object.Destroy(lampLight.GetComponent<Collider>());
            }
        }

        private void CreateBush(Vector3 pos, float scale, Material mat, string objName)
        {
            GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = objName;
            bush.transform.position = pos + new Vector3(0f, scale * 0.3f, 0f);
            bush.transform.localScale = new Vector3(scale * 1.6f, scale * 0.8f, scale * 1.4f);
            bush.GetComponent<MeshRenderer>().material = mat;
            Object.Destroy(bush.GetComponent<Collider>());
        }

        private void CreateGrassTuft(Vector3 pos, float scale, Material mat, string objName)
        {
            for (int j = 0; j < 3; j++)
            {
                GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.name = $"{objName}_{j}";
                float offX = (j - 1) * scale * 0.3f;
                blade.transform.position = pos + new Vector3(offX, scale * 0.5f, 0f);
                blade.transform.localScale = new Vector3(0.06f, scale, 0.06f);
                blade.transform.rotation = Quaternion.Euler(0f, j * 40f, Random.Range(-10f, 10f));
                blade.GetComponent<MeshRenderer>().material = mat;
                Object.Destroy(blade.GetComponent<Collider>());
            }
        }

        private void CreateLog(Vector3 pos, float length, float angle, Material mat, string objName)
        {
            GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = objName;
            log.transform.position = pos + new Vector3(0f, 0.2f, 0f);
            log.transform.localScale = new Vector3(0.35f, length * 0.5f, 0.35f);
            log.transform.rotation = Quaternion.Euler(0f, angle, 90f);
            log.GetComponent<MeshRenderer>().material = mat;
        }

        private void CreateMushroom(Vector3 pos, float scale, string objName)
        {
            Material stemMat = CreateSafeMaterial(new Color(0.9f, 0.88f, 0.75f));
            Material capMat = CreateSafeMaterial(new Color(0.8f, 0.2f, 0.15f));

            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = $"{objName}_Stem";
            stem.transform.position = pos + new Vector3(0f, scale * 0.4f, 0f);
            stem.transform.localScale = new Vector3(scale * 0.3f, scale * 0.4f, scale * 0.3f);
            stem.GetComponent<MeshRenderer>().material = stemMat;
            Object.Destroy(stem.GetComponent<Collider>());

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = $"{objName}_Cap";
            cap.transform.position = pos + new Vector3(0f, scale * 0.9f, 0f);
            cap.transform.localScale = new Vector3(scale * 0.9f, scale * 0.45f, scale * 0.9f);
            cap.GetComponent<MeshRenderer>().material = capMat;
            Object.Destroy(cap.GetComponent<Collider>());
        }

        private InsectDatabase EnsureDatabase()
        {
            InsectDatabase database = ScriptableObject.CreateInstance<InsectDatabase>();
            database.insects = new List<InsectData>
            {
                CreateInsect("beetle_basic", "딱정벌레", InsectRarity.Common, 1.2f, 0.25f, "풀밭에서 흔히 볼 수 있는 딱정벌레", "초원"),
                CreateInsect("mantis_green", "사마귀", InsectRarity.Uncommon, 0.9f, 0.35f, "날카로운 앞다리를 가진 포식자", "초원"),
                CreateInsect("moth_night", "밤나방", InsectRarity.Uncommon, 0.8f, 0.32f, "밤에만 나타나는 신비한 나방", "초원"),
                CreateInsect("bee_worker", "일벌", InsectRarity.Common, 1.1f, 0.28f, "꽃가루를 열심히 모으는 일벌", "초원"),
                CreateInsect("dragonfly_lake", "잠자리", InsectRarity.Rare, 0.6f, 0.45f, "호수 주변을 빠르게 나는 잠자리", "연못"),
                CreateInsect("stag_beetle", "사슴벌레", InsectRarity.Rare, 0.5f, 0.48f, "커다란 집게로 유명한 사슴벌레", "숲"),
                CreateInsect("firefly_glow", "반딧불이", InsectRarity.Epic, 0.3f, 0.6f, "어둠 속에서 빛나는 신비한 곤충", "숲"),
                CreateInsect("butterfly_azure", "푸른나비", InsectRarity.Epic, 0.25f, 0.65f, "하늘빛 날개를 가진 아름다운 나비", "꽃밭"),
                CreateInsect("cricket_field", "귀뚜라미", InsectRarity.Common, 1.3f, 0.22f, "밤에 노래하는 귀뚜라미", "초원"),
                CreateInsect("ant_soldier", "병정개미", InsectRarity.Common, 1.4f, 0.2f, "군대처럼 행진하는 개미", "초원"),
                CreateInsect("water_strider", "소금쟁이", InsectRarity.Uncommon, 0.7f, 0.38f, "수면 위를 걷는 신기한 곤충", "연못"),
                CreateInsect("diving_beetle", "물방개", InsectRarity.Uncommon, 0.65f, 0.4f, "물속에서 사냥하는 포식자", "연못"),
                CreateInsect("cicada_summer", "매미", InsectRarity.Rare, 0.55f, 0.42f, "여름을 알리는 매미", "숲"),
                CreateInsect("rhinoceros_beetle", "장수풍뎅이", InsectRarity.Rare, 0.4f, 0.52f, "뿔이 인상적인 강력한 풍뎅이", "숲"),
                CreateInsect("luna_moth", "달나방", InsectRarity.Epic, 0.2f, 0.62f, "달빛 아래서만 나타나는 나방", "꽃밭"),
                CreateInsect("atlas_moth", "아틀라스나방", InsectRarity.Legendary, 0.1f, 0.75f, "세계에서 가장 큰 나방", "숲"),
                CreateInsect("golden_scarab", "황금풍뎅이", InsectRarity.Legendary, 0.08f, 0.8f, "전설의 황금빛 풍뎅이", "꽃밭"),
                CreateInsect("jewel_beetle", "비단벌레", InsectRarity.Legendary, 0.06f, 0.85f, "보석처럼 빛나는 희귀한 벌레", "연못")
            };
            return database;
        }

        private InsectData CreateInsect(string id, string name, InsectRarity rarity, float weight, float difficulty, string desc = "", string habitat = "")
        {
            InsectData data = ScriptableObject.CreateInstance<InsectData>();
            data.insectId = id;
            data.displayName = name;
            data.rarity = rarity;
            data.spawnWeight = weight;
            data.captureDifficulty = difficulty;
            data.description = desc;
            data.habitatHint = habitat;
            data.basePower = 8 + (int)rarity * 5;

            int r = (int)rarity;
            data.baseHp = 40 + r * 15 + Random.Range(0, 10);
            data.baseAtk = 15 + r * 8 + Random.Range(0, 6);
            data.baseDef = 10 + r * 6 + Random.Range(0, 5);

            data.skills = GenerateSkillsForInsect(id, name, rarity);
            return data;
        }

        private InsectSkill[] GenerateSkillsForInsect(string id, string name, InsectRarity rarity)
        {
            int count = 2 + Mathf.Min((int)rarity, 2);
            InsectSkill[] skills = new InsectSkill[count];

            skills[0] = CreateSkill($"{id}_atk", $"{name} 공격", SkillEffectType.Damage,
                8 + (int)rarity * 3, 0);

            skills[1] = CreateSkill($"{id}_power", $"{name} 강타", SkillEffectType.Damage,
                14 + (int)rarity * 5, 2);

            if (count >= 3)
                skills[2] = CreateSkill($"{id}_buff", "공격 강화", SkillEffectType.BuffAttack,
                    0, 3, 0.3f, 3);

            if (count >= 4)
                skills[3] = CreateSkill($"{id}_debuff", "약화 공격", SkillEffectType.DebuffAttack,
                    0, 3, 0.25f, 2);

            return skills;
        }

        private InsectSkill CreateSkill(string skillId, string displayName, SkillEffectType type,
            int power, int cooldown, float effectVal = 0.2f, int effectDur = 2)
        {
            InsectSkill skill = ScriptableObject.CreateInstance<InsectSkill>();
            skill.skillId = skillId;
            skill.displayName = displayName;
            skill.effectType = type;
            skill.power = Mathf.Max(1, power);
            skill.cooldownTurns = cooldown;
            skill.effectValue = effectVal;
            skill.effectDurationTurns = effectDur;
            return skill;
        }

        private TrainingMethod[] CreateTrainingMethods()
        {
            return new TrainingMethod[]
            {
                new TrainingMethod
                {
                    methodId = "species",
                    displayName = "종족 기술 연구",
                    description = "레벨로 해금한 타입 기술과 전용기를 캔디로 교체한다",
                    themeColor = new Color(0.3f, 0.85f, 0.75f),
                    candyCost = 8,
                    requiredLevel = 1,
                    skillPool = new string[0]
                },
                new TrainingMethod
                {
                    methodId = "stamina",
                    displayName = "체력 단련",
                    description = "기초 체력을 키워 강력한 물리 공격을 익힌다",
                    themeColor = new Color(0.9f, 0.4f, 0.2f),
                    candyCost = 5,
                    requiredLevel = 1,
                    skillPool = new[] { "tr_tackle", "tr_headbutt", "tr_bodyslam", "tr_charge" }
                },
                new TrainingMethod
                {
                    methodId = "combat",
                    displayName = "전투 훈련",
                    description = "실전 감각을 길러 다양한 공격 기술을 배운다",
                    themeColor = new Color(0.9f, 0.2f, 0.2f),
                    candyCost = 10,
                    requiredLevel = 3,
                    skillPool = new[] { "tr_slash", "tr_bite", "tr_sting", "tr_frenzy" }
                },
                new TrainingMethod
                {
                    methodId = "defense",
                    displayName = "방어 훈련",
                    description = "방어적 자세로 상대를 약화시키는 기술을 익힌다",
                    themeColor = new Color(0.3f, 0.5f, 0.9f),
                    candyCost = 8,
                    requiredLevel = 2,
                    skillPool = new[] { "tr_weaken", "tr_intimidate", "tr_shell_guard", "tr_acid_spray" }
                },
                new TrainingMethod
                {
                    methodId = "special",
                    displayName = "특수 훈련",
                    description = "자연의 힘을 빌려 버프와 특수 공격을 익힌다",
                    themeColor = new Color(0.4f, 0.85f, 0.4f),
                    candyCost = 12,
                    requiredLevel = 4,
                    skillPool = new[] { "tr_power_up", "tr_harden", "tr_nature_force", "tr_pheromone" }
                },
                new TrainingMethod
                {
                    methodId = "extreme",
                    displayName = "극한 훈련",
                    description = "극한의 수련으로 최강의 필살기를 터득한다",
                    themeColor = new Color(0.85f, 0.3f, 0.85f),
                    candyCost = 20,
                    requiredLevel = 6,
                    skillPool = new[] { "tr_mega_strike", "tr_berserk", "tr_eclipse", "tr_doom_sting" }
                }
            };
        }

        private InsectSkill[] CreateTrainingSkills()
        {
            List<InsectSkill> skills = new List<InsectSkill>();

            skills.Add(CreateSkill("tr_tackle", "돌진", SkillEffectType.Damage, 12, 0));
            skills.Add(CreateSkill("tr_headbutt", "박치기", SkillEffectType.Damage, 18, 1));
            skills.Add(CreateSkill("tr_bodyslam", "몸통 박치기", SkillEffectType.Damage, 25, 2));
            skills.Add(CreateSkill("tr_charge", "돌격", SkillEffectType.Damage, 30, 3));

            skills.Add(CreateSkill("tr_slash", "베기", SkillEffectType.Damage, 22, 1));
            skills.Add(CreateSkill("tr_bite", "물기", SkillEffectType.Damage, 28, 2));
            skills.Add(CreateSkill("tr_sting", "독침 찌르기", SkillEffectType.Damage, 35, 2));
            skills.Add(CreateSkill("tr_frenzy", "광란 공격", SkillEffectType.Damage, 45, 3));

            skills.Add(CreateSkill("tr_weaken", "약화시키기", SkillEffectType.DebuffAttack, 1, 2, 0.25f, 3));
            skills.Add(CreateSkill("tr_intimidate", "위협", SkillEffectType.DebuffAttack, 1, 3, 0.35f, 2));
            skills.Add(CreateSkill("tr_shell_guard", "껍질 방어", SkillEffectType.BuffAttack, 1, 3, 0.2f, 4));
            skills.Add(CreateSkill("tr_acid_spray", "산성 분무", SkillEffectType.DebuffAttack, 1, 2, 0.3f, 3));

            skills.Add(CreateSkill("tr_power_up", "파워 업", SkillEffectType.BuffAttack, 1, 3, 0.4f, 3));
            skills.Add(CreateSkill("tr_harden", "단단해지기", SkillEffectType.BuffAttack, 1, 2, 0.3f, 4));
            skills.Add(CreateSkill("tr_nature_force", "자연의 힘", SkillEffectType.Damage, 40, 3));
            skills.Add(CreateSkill("tr_pheromone", "페로몬", SkillEffectType.BuffAttack, 1, 4, 0.5f, 3));

            skills.Add(CreateSkill("tr_mega_strike", "메가 스트라이크", SkillEffectType.Damage, 55, 4));
            skills.Add(CreateSkill("tr_berserk", "광폭화", SkillEffectType.BuffAttack, 1, 4, 0.6f, 2));
            skills.Add(CreateSkill("tr_eclipse", "이클립스", SkillEffectType.Damage, 65, 5));
            skills.Add(CreateSkill("tr_doom_sting", "파멸의 독침", SkillEffectType.Damage, 75, 5));

            foreach (InsectSkill skill in skills)
            {
                if (skill == null) continue;
                skill.trainingCost = Mathf.Max(5, skill.power / 2);
                skill.description = "훈련을 통해 익힐 수 있는 범용 기술";

                switch (skill.skillId)
                {
                    case "tr_slash":
                    case "tr_pheromone":
                        skill.element = InsectElement.Bug;
                        break;
                    case "tr_bite":
                    case "tr_frenzy":
                    case "tr_intimidate":
                    case "tr_berserk":
                    case "tr_eclipse":
                        skill.element = InsectElement.Dark;
                        break;
                    case "tr_sting":
                    case "tr_acid_spray":
                    case "tr_doom_sting":
                        skill.element = InsectElement.Poison;
                        break;
                    case "tr_shell_guard":
                    case "tr_harden":
                        skill.element = InsectElement.Metal;
                        break;
                    case "tr_nature_force":
                        skill.element = InsectElement.Leaf;
                        break;
                    default:
                        skill.element = InsectElement.None;
                        break;
                }
            }

            return skills.ToArray();
        }

        private Data.CaptureItemData[] CreateCaptureItems()
        {
            return new Data.CaptureItemData[]
            {
                new Data.CaptureItemData
                {
                    itemId = "net_basic",
                    displayName = "기본 채집망",
                    description = "흔한 채집망 - 보통 난이도",
                    themeColor = new Color(0.6f, 0.6f, 0.6f),
                    spawnWeight = 0.6f,
                    speedMultiplier = 1f,
                    zoneSizeMultiplier = 1f,
                    timeLimitMultiplier = 1f,
                    captureBonus = 0f,
                },
                new Data.CaptureItemData
                {
                    itemId = "net_silver",
                    displayName = "은빛 채집망",
                    description = "좋은 품질 - 미니게임이 쉬워짐",
                    themeColor = new Color(0.75f, 0.82f, 0.95f),
                    spawnWeight = 0.3f,
                    speedMultiplier = 0.75f,
                    zoneSizeMultiplier = 1.3f,
                    timeLimitMultiplier = 1.3f,
                    captureBonus = 0.1f,
                },
                new Data.CaptureItemData
                {
                    itemId = "net_gold",
                    displayName = "황금 채집망",
                    description = "최고급 - 매우 쉬운 미니게임 + 포획률 보너스",
                    themeColor = new Color(1f, 0.85f, 0.2f),
                    spawnWeight = 0.1f,
                    speedMultiplier = 0.55f,
                    zoneSizeMultiplier = 1.6f,
                    timeLimitMultiplier = 1.6f,
                    captureBonus = 0.2f,
                },
            };
        }

        private InsectDatabase EnsureExpandedDatabase()
        {
            InsectDatabase database = ScriptableObject.CreateInstance<InsectDatabase>();
            database.insects = new List<InsectData>
            {
                // ── Meadow (14) ──
                CreateStableInsect("beetle_basic", "Field Beetle", InsectRarity.Common, 1.25f, 0.22f, "A reliable beetle found across the grassland.", "Meadow"),
                CreateStableInsect("bee_worker", "Worker Bee", InsectRarity.Common, 1.18f, 0.24f, "Collects pollen in steady loops around flowers.", "Meadow"),
                CreateStableInsect("cricket_field", "Field Cricket", InsectRarity.Common, 1.22f, 0.20f, "Sings loudly at dusk from low shrubs.", "Meadow"),
                CreateStableInsect("ant_soldier", "Soldier Ant", InsectRarity.Common, 1.28f, 0.18f, "Travels in organized lines and defends the nest.", "Meadow"),
                CreateStableInsect("grasshopper_green", "Green Grasshopper", InsectRarity.Common, 1.12f, 0.23f, "Leaps long distances between reeds.", "Meadow"),
                CreateStableInsect("ladybug_seven", "Seven-Spot Ladybug", InsectRarity.Common, 1.05f, 0.20f, "A small but lucky visitor to clover patches.", "Meadow"),
                CreateStableInsect("caterpillar_green", "Green Caterpillar", InsectRarity.Common, 0.95f, 0.19f, "Inches along leaves with quiet determination.", "Meadow"),
                CreateStableInsect("aphid_colony", "Aphid Colony", InsectRarity.Common, 1.30f, 0.18f, "Tiny insects clustered on stems in large numbers.", "Meadow"),
                CreateStableInsect("moth_brown", "Brown Moth", InsectRarity.Common, 0.92f, 0.21f, "A plain moth that blends into dry grass.", "Meadow"),
                CreateStableInsect("beetle_dung", "Dung Beetle", InsectRarity.Uncommon, 0.72f, 0.32f, "Rolls earth with surprising strength.", "Meadow"),
                CreateStableInsect("centipede_common", "Common Centipede", InsectRarity.Uncommon, 0.65f, 0.35f, "Moves fast through leaf litter.", "Meadow"),
                CreateStableInsect("katydid_leaf", "Leaf Katydid", InsectRarity.Uncommon, 0.58f, 0.38f, "Mimics a green leaf so well it vanishes.", "Meadow"),
                CreateStableInsect("butterfly_cabbage", "Cabbage Butterfly", InsectRarity.Rare, 0.28f, 0.45f, "A white butterfly common near vegetable patches.", "Meadow"),
                CreateStableInsect("beetle_click", "Click Beetle", InsectRarity.Rare, 0.22f, 0.48f, "Flips itself with an audible click when threatened.", "Meadow"),

                // ── Pond (13) ──
                CreateStableInsect("dragonfly_lake", "Lake Dragonfly", InsectRarity.Uncommon, 0.68f, 0.34f, "Cuts across the pond surface at high speed.", "Pond"),
                CreateStableInsect("water_strider_pond", "Pond Water Strider", InsectRarity.Common, 1.10f, 0.20f, "Skims the water without breaking the surface.", "Pond"),
                CreateStableInsect("diving_beetle_deep", "Deep Diving Beetle", InsectRarity.Uncommon, 0.58f, 0.39f, "A quick underwater ambusher in deep waters.", "Pond"),
                CreateStableInsect("diving_beetle_small", "Small Diving Beetle", InsectRarity.Common, 0.90f, 0.22f, "A nimble swimmer found in shallow pools.", "Pond"),
                CreateStableInsect("damselfly_blue", "Blue Damselfly", InsectRarity.Uncommon, 0.61f, 0.33f, "A narrow-winged flier that rests on reeds.", "Pond"),
                CreateStableInsect("mosquito_common", "Common Mosquito", InsectRarity.Common, 1.20f, 0.18f, "Common near still water in the evening.", "Pond"),
                CreateStableInsect("fly_house", "House Fly", InsectRarity.Common, 1.15f, 0.19f, "Buzzes around endlessly near the waterside.", "Pond"),
                CreateStableInsect("pill_bug_garden", "Garden Pill Bug", InsectRarity.Common, 0.98f, 0.21f, "Curls into a ball when disturbed.", "Pond"),
                CreateStableInsect("earwig_common", "Common Earwig", InsectRarity.Common, 1.00f, 0.23f, "Hides under damp stones during the day.", "Pond"),
                CreateStableInsect("cicada_evening", "Evening Cicada", InsectRarity.Rare, 0.30f, 0.44f, "Sings its song as the sun sets over the pond.", "Pond"),
                CreateStableInsect("firefly_blue", "Blue Firefly", InsectRarity.Epic, 0.12f, 0.60f, "Emits a cold blue glow above the water.", "Pond"),
                CreateStableInsect("dragonfly_emperor", "Emperor Dragonfly", InsectRarity.Epic, 0.09f, 0.63f, "A large dragonfly that rules the pond skies.", "Pond"),
                CreateStableInsect("dragonfly_ancient", "Ancient Dragonfly", InsectRarity.Legendary, 0.04f, 0.82f, "A living fossil from a bygone era.", "Pond"),

                // ── Forest (14) ──
                CreateStableInsect("stag_beetle", "Stag Beetle", InsectRarity.Rare, 0.30f, 0.46f, "Known for its large jaws and strong frame.", "Forest"),
                CreateStableInsect("rhinoceros_beetle", "Rhinoceros Beetle", InsectRarity.Rare, 0.22f, 0.50f, "A heavy horned beetle found on old trunks.", "Forest"),
                CreateStableInsect("cicada_summer", "Summer Cicada", InsectRarity.Uncommon, 0.55f, 0.34f, "Its call fills the forest at midday.", "Forest"),
                CreateStableInsect("moth_night", "Night Moth", InsectRarity.Common, 0.92f, 0.24f, "Drawn to dim lanterns after sunset.", "Forest"),
                CreateStableInsect("mantis_green", "Green Mantis", InsectRarity.Uncommon, 0.72f, 0.36f, "A patient hunter that strikes from tall grass.", "Forest"),
                CreateStableInsect("longhorn_beetle", "Longhorn Beetle", InsectRarity.Uncommon, 0.60f, 0.37f, "Its antennae are longer than its body.", "Forest"),
                CreateStableInsect("stick_insect_long", "Long Stick Insect", InsectRarity.Common, 0.90f, 0.24f, "Camouflages perfectly as a twig.", "Forest"),
                CreateStableInsect("beetle_longhorn_rosalia", "Rosalia Longhorn", InsectRarity.Rare, 0.25f, 0.52f, "A beautiful beetle with blue-grey markings.", "Forest"),
                CreateStableInsect("hornet_asian", "Asian Hornet", InsectRarity.Rare, 0.20f, 0.54f, "Fast, aggressive, and territorial.", "Forest"),
                CreateStableInsect("scarab_ancient", "Ancient Scarab", InsectRarity.Epic, 0.14f, 0.58f, "An ancient beetle revered in old legends.", "Forest"),
                CreateStableInsect("mantis_ghost", "Ghost Mantis", InsectRarity.Epic, 0.10f, 0.62f, "Almost invisible among dried leaves.", "Forest"),
                CreateStableInsect("atlas_moth_giant", "Giant Atlas Moth", InsectRarity.Legendary, 0.05f, 0.78f, "A giant moth with wing patterns like eyes.", "Forest"),
                CreateStableInsect("beetle_hercules", "Hercules Beetle", InsectRarity.Legendary, 0.03f, 0.85f, "The strongest beetle in the forest.", "Forest"),
                CreateStableInsect("leaf_insect_phantom", "Phantom Leaf Insect", InsectRarity.Epic, 0.08f, 0.65f, "A master of disguise among forest foliage.", "Forest"),

                // ── Garden (13) ──
                CreateStableInsect("butterfly_azure", "Azure Butterfly", InsectRarity.Uncommon, 0.55f, 0.32f, "A striking blue butterfly seen above flower beds.", "Garden"),
                CreateStableInsect("butterfly_monarch", "Monarch Butterfly", InsectRarity.Uncommon, 0.48f, 0.34f, "Drifts slowly across warm flower rows.", "Garden"),
                CreateStableInsect("butterfly_swallowtail", "Swallowtail Butterfly", InsectRarity.Rare, 0.28f, 0.45f, "A bright butterfly that glides over wildflowers.", "Garden"),
                CreateStableInsect("butterfly_morpho", "Morpho Butterfly", InsectRarity.Rare, 0.22f, 0.50f, "Its iridescent wings flash brilliant blue.", "Garden"),
                CreateStableInsect("butterfly_alexandras", "Alexandras Birdwing", InsectRarity.Legendary, 0.04f, 0.82f, "The largest butterfly in the world.", "Garden"),
                CreateStableInsect("luna_moth_silver", "Silver Luna Moth", InsectRarity.Epic, 0.12f, 0.60f, "Appears under moonlight around the greenhouse.", "Garden"),
                CreateStableInsect("jewel_beetle_gold", "Gold Jewel Beetle", InsectRarity.Epic, 0.10f, 0.63f, "A gemstone-bright beetle with golden sheen.", "Garden"),
                CreateStableInsect("firefly_glow", "Glow Firefly", InsectRarity.Uncommon, 0.45f, 0.38f, "Creates moving lights between flowers at night.", "Garden"),
                CreateStableInsect("spider_garden", "Garden Spider", InsectRarity.Common, 1.05f, 0.22f, "Weaves intricate webs between garden posts.", "Garden"),
                CreateStableInsect("spider_golden_orb", "Golden Orb Spider", InsectRarity.Rare, 0.18f, 0.56f, "Its golden web glints in the sunlight.", "Garden"),
                CreateStableInsect("mantis_orchid", "Orchid Mantis", InsectRarity.Epic, 0.08f, 0.65f, "Disguises itself as a delicate flower.", "Garden"),
                CreateStableInsect("beetle_golden_stag", "Golden Stag Beetle", InsectRarity.Legendary, 0.03f, 0.84f, "An exceptionally rare golden beetle.", "Garden"),
                CreateStableInsect("wasp_paper", "Paper Wasp", InsectRarity.Common, 0.95f, 0.25f, "Circles the garden edges looking for prey.", "Garden"),

                // -- Gacha Exclusive (10) -- spawnWeight=0 -> 필드 스폰 안 됨
                CreateStableInsect("gacha_golden_ladybug",    "Golden Ladybug",       InsectRarity.Rare,      0f, 0.5f,  "A golden ladybug from mystery box.", "Gacha"),
                CreateStableInsect("gacha_crystal_dragonfly", "Crystal Dragonfly",    InsectRarity.Epic,      0f, 0.6f,  "A dragonfly with crystal wings.", "Gacha"),
                CreateStableInsect("gacha_shadow_mantis",     "Shadow Mantis",        InsectRarity.Epic,      0f, 0.62f, "A mantis cloaked in dark mist.", "Gacha"),
                CreateStableInsect("gacha_rainbow_butterfly", "Rainbow Butterfly",    InsectRarity.Legendary, 0f, 0.8f,  "A legendary butterfly with seven-colored wings.", "Gacha"),
                CreateStableInsect("gacha_diamond_beetle",    "Diamond Beetle",       InsectRarity.Legendary, 0f, 0.85f, "A beetle that shines like a diamond.", "Gacha"),
                CreateStableInsect("gacha_neon_firefly",      "Neon Firefly",         InsectRarity.Rare,      0f, 0.48f, "A firefly that glows in neon colors.", "Gacha"),
                CreateStableInsect("gacha_ice_spider",        "Ice Spider",           InsectRarity.Epic,      0f, 0.58f, "A spider that weaves webs of ice.", "Gacha"),
                CreateStableInsect("gacha_phantom_moth",      "Phantom Moth",         InsectRarity.Rare,      0f, 0.45f, "A semi-transparent moth of mystery.", "Gacha"),
                CreateStableInsect("gacha_storm_hornet",      "Storm Hornet",         InsectRarity.Epic,      0f, 0.63f, "A hornet wreathed in lightning.", "Gacha"),
                CreateStableInsect("gacha_celestial_beetle",  "Celestial Beetle",     InsectRarity.Legendary, 0f, 0.88f, "A beetle inscribed with starlight.", "Gacha")
            };

            // 확장 64종(64→128) — 시드 목록은 InsectExpansionDefinitions가 단일 출처.
            // 스탯/타입/스킬은 기존 종과 동일하게 CreateStableInsect가 자동 파생한다.
            foreach (InsectSeed seed in InsectExpansionDefinitions.CreateAll())
            {
                database.insects.Add(CreateStableInsect(seed.id, seed.name, seed.rarity, seed.weight, seed.difficulty, seed.desc, seed.habitat));
            }

            return database;
        }

        private void ValidateBattleDefinitions(InsectDatabase database)
        {
            if (GameConstants.Player.MaxLearnedSkills != 4)
                throw new CriticalBootstrapException("배틀 데이터 검증 실패 — 최대 기술 수는 4개여야 함");
            if (InsectTypeChart.GetEffectiveness(InsectElement.Leaf, InsectElement.Water, InsectElement.None) <= 1f)
                throw new CriticalBootstrapException("배틀 데이터 검증 실패 — 타입 상성표 비활성");

            int typedCount = 0;
            int epicCount = 0;
            int signatureCount = 0;
            HashSet<string> signatureIds = new HashSet<string>();

            int dataWarnings = 0;
            foreach (InsectData insect in database.insects)
            {
                // 개별 곤충 데이터 결함은 게임 전체 부팅을 막지 않는다(throw 금지). 로그 + 건너뜀/런타임 보정으로
                // 해당 종만 영향받게 하고 나머지 시스템(곤충/UI/스폰)은 정상 기동시킨다. 데이터 회귀로 인한
                // "핵심 시스템 초기화 실패 → 게임 통째 먹통"을 차단. (전용기 타입/곤충 타입은 별도 출처라 손동기화 위험)
                if (insect == null || string.IsNullOrEmpty(insect.insectId))
                {
                    Debug.LogError("[BattleData] 곤충 ID 누락 — 해당 항목 건너뜀");
                    dataWarnings++;
                    continue;
                }
                if (insect.primaryType == InsectElement.None)
                {
                    Debug.LogError($"[BattleData] {insect.insectId} 타입 누락 — Bug로 보정");
                    insect.primaryType = InsectElement.Bug;
                    dataWarnings++;
                }
                if (insect.learnset == null || insect.learnset.Length < 5)
                {
                    Debug.LogError($"[BattleData] {insect.insectId} 레벨 기술표 부족({(insect.learnset == null ? 0 : insect.learnset.Length)}) — 전용기 검증 건너뜀");
                    typedCount++;
                    dataWarnings++;
                    continue;
                }

                typedCount++;
                if (insect.rarity < InsectRarity.Epic) continue;

                epicCount++;
                InsectSkill signature = null;
                foreach (InsectLearnableSkill learnable in insect.learnset)
                {
                    if (learnable != null && learnable.skill != null && learnable.skill.isSignatureSkill)
                    {
                        signature = learnable.skill;
                        break;
                    }
                }

                if (signature == null || string.IsNullOrEmpty(signature.skillId))
                {
                    Debug.LogError($"[BattleData] {insect.insectId} 전용기 누락 — 전용기 없이 진행");
                    dataWarnings++;
                    continue;
                }
                if (signature.element != insect.primaryType && signature.element != insect.secondaryType)
                {
                    Debug.LogWarning($"[BattleData] {insect.insectId} 전용기 타입 불일치({signature.element}) — primaryType({insect.primaryType})로 런타임 보정");
                    signature.element = insect.primaryType;
                    dataWarnings++;
                }
                if (signature.trainingCost <= 0)
                {
                    Debug.LogWarning($"[BattleData] {insect.insectId} 전용기 교체 비용 누락 — 1로 보정");
                    signature.trainingCost = 1;
                    dataWarnings++;
                }
                if (!signatureIds.Add(signature.skillId))
                {
                    Debug.LogError($"[BattleData] 전용기 ID 중복: {signature.skillId} ({insect.insectId}) — 카운트 스킵");
                    dataWarnings++;
                    continue;
                }
                signatureCount++;
            }

            if (dataWarnings > 0)
                Debug.LogWarning($"[BattleData] 데이터 경고 {dataWarnings}건 보정/건너뜀 — 부팅은 계속됨");
            Debug.Log($"[BattleData] 검증 완료 — 타입 {typedCount}종, 에픽+ {epicCount}종, 고유 전용기 {signatureCount}개");
        }

        private InsectData CreateStableInsect(string id, string name, InsectRarity rarity, float weight, float difficulty, string desc, string habitat)
        {
            InsectData data = ScriptableObject.CreateInstance<InsectData>();
            data.insectId = id;
            data.displayName = name;
            data.rarity = rarity;
            data.spawnWeight = weight;
            data.captureDifficulty = difficulty;
            data.description = desc;
            data.habitatHint = habitat;
            data.primaryType = InferPrimaryType(id, habitat);
            data.secondaryType = InferSecondaryType(id, data.primaryType);

            int rarityIndex = (int)rarity;
            int seed = GetStableValue(id);
            data.basePower = 10 + rarityIndex * 8 + seed % 4;
            data.baseHp = 44 + rarityIndex * 18 + seed % 12;
            data.baseAtk = 16 + rarityIndex * 9 + (seed / 10) % 7;
            data.baseDef = 12 + rarityIndex * 7 + (seed / 100) % 6;

            switch (rarity)
            {
                case InsectRarity.Common:
                    data.minLevel = 1;
                    data.maxLevel = 6;
                    data.expReward = 5;
                    data.candyReward = 2;
                    break;
                case InsectRarity.Uncommon:
                    data.minLevel = 3;
                    data.maxLevel = 9;
                    data.expReward = 8;
                    data.candyReward = 3;
                    break;
                case InsectRarity.Rare:
                    data.minLevel = 6;
                    data.maxLevel = 13;
                    data.expReward = 12;
                    data.candyReward = 4;
                    break;
                case InsectRarity.Epic:
                    data.minLevel = 10;
                    data.maxLevel = 18;
                    data.expReward = 17;
                    data.candyReward = 5;
                    break;
                default:
                    data.minLevel = 14;
                    data.maxLevel = 24;
                    data.expReward = 24;
                    data.candyReward = 6;
                    break;
            }

            data.itemRewardCount = 1 + Mathf.Min(3, rarityIndex);
            data.learnset = BuildLevelLearnset(data);
            data.skills = ExtractUniqueSkills(data.learnset);
            return data;
        }

        private InsectElement InferPrimaryType(string insectId, string habitat)
        {
            string id = insectId ?? string.Empty;
            string zone = habitat ?? string.Empty;

            if (id.Contains("storm_hornet"))
                return InsectElement.Electric;
            if (id.Contains("shadow_mantis") || id.Contains("phantom_moth"))
                return InsectElement.Dark;
            if (id.Contains("ice_spider"))
                return InsectElement.Water;
            if (id.Contains("crystal_dragonfly") || id.Contains("rainbow_butterfly"))
                return InsectElement.Light;
            if (id.Contains("celestial") || id.Contains("diamond") || id.Contains("gold") || id.Contains("jewel"))
                return InsectElement.Metal;
            if (id.Contains("firefly") || id.Contains("glow"))
                return InsectElement.Light;
            if (IsAntInsectId(id))
                return InsectElement.Earth;
            if (id.Contains("water") || id.Contains("pond") || id.Contains("lake") || id.Contains("diving") || id.Contains("mosquito"))
                return InsectElement.Water;
            if (IsBeeInsectId(id) || id.Contains("dragonfly") || id.Contains("damselfly") || id.Contains("butterfly") || id.Contains("moth"))
                return InsectElement.Wind;
            if (id.Contains("scarab") || id.Contains("beetle") || id.Contains("hornet"))
                return InsectElement.Metal;
            if (id.Contains("mantis") || id.Contains("leaf") || id.Contains("grasshopper"))
                return InsectElement.Leaf;
            if (id.Contains("lanternfly") || id.Contains("night") || id.Contains("atlas"))
                return InsectElement.Dark;
            if (id.Contains("wasp"))
                return InsectElement.Poison;
            if (id.Contains("mole") || id.Contains("cricket") || id.Contains("antlion"))
                return InsectElement.Earth;
            if (zone == "Pond")
                return InsectElement.Water;
            if (zone == "Forest")
                return InsectElement.Earth;
            if (zone == "Garden")
                return InsectElement.Wind;
            // 확장 리전 태그(Swamp/Mountain/Ruins) 폴백 — 확장 64종의 기본 속성.
            if (zone == "Swamp")
                return InsectElement.Poison;
            if (zone == "Mountain")
                return InsectElement.Earth;
            if (zone == "Ruins")
                return InsectElement.Dark;
            return InsectElement.Bug;
        }

        private InsectElement InferSecondaryType(string insectId, InsectElement primaryType)
        {
            string id = insectId ?? string.Empty;

            if (id.Contains("storm_hornet") || id.Contains("crystal_dragonfly"))
                return InsectElement.Wind;
            if (id.Contains("ice_spider"))
                return InsectElement.Poison;
            if (id.Contains("shadow_mantis") || id.Contains("phantom") || id.Contains("ghost"))
                return primaryType == InsectElement.Dark ? InsectElement.Poison : InsectElement.Dark;
            if (id.Contains("celestial") || id.Contains("diamond") || id.Contains("gold") || id.Contains("jewel") || id.Contains("rainbow"))
                return primaryType == InsectElement.Light ? InsectElement.Wind : InsectElement.Light;
            if (id.Contains("firefly") || id.Contains("glow"))
                return primaryType == InsectElement.Light ? InsectElement.Bug : InsectElement.Light;
            if (id.Contains("spider"))
                return primaryType == InsectElement.Poison ? InsectElement.Dark : InsectElement.Poison;
            if (IsAntInsectId(id))
                return primaryType == InsectElement.Poison ? InsectElement.Earth : InsectElement.Poison;
            if (id.Contains("ladybug"))
                return primaryType == InsectElement.Metal ? InsectElement.Bug : InsectElement.Metal;
            if (id.Contains("mosquito"))
                return primaryType == InsectElement.Poison ? InsectElement.Water : InsectElement.Poison;
            if (id.Contains("night") || id.Contains("shadow") || id.Contains("atlas"))
                return primaryType == InsectElement.Dark ? InsectElement.None : InsectElement.Dark;
            if (IsBeeInsectId(id) || id.Contains("wasp") || id.Contains("hornet"))
                return primaryType == InsectElement.Poison ? InsectElement.Wind : InsectElement.Poison;
            if (id.Contains("dragonfly") || id.Contains("damselfly"))
                return primaryType == InsectElement.Water ? InsectElement.Wind : InsectElement.Water;
            if (id.Contains("butterfly") || id.Contains("moth"))
                return primaryType == InsectElement.Wind ? InsectElement.Light : InsectElement.Wind;
            if (id.Contains("water") || id.Contains("pond") || id.Contains("lake") || id.Contains("diving"))
                return primaryType == InsectElement.Water ? InsectElement.None : InsectElement.Water;
            if (id.Contains("beetle") || id.Contains("scarab"))
                return primaryType == InsectElement.Metal ? InsectElement.Bug : InsectElement.Metal;
            if (id.Contains("leaf") || id.Contains("grasshopper") || id.Contains("mantis"))
                return primaryType == InsectElement.Leaf ? InsectElement.Bug : InsectElement.Leaf;

            return InsectElement.None;
        }

        private InsectLearnableSkill[] BuildLevelLearnset(InsectData data)
        {
            List<InsectLearnableSkill> learnset = new List<InsectLearnableSkill>
            {
                CreateLearnableSkill(GetTypedSkill(data.primaryType, "jab"), 1),
                CreateLearnableSkill(GetTypedSkill(data.primaryType, "boost"), 5),
                CreateLearnableSkill(GetTraitSkill(data), 9)
            };

            if (data.secondaryType != InsectElement.None && data.secondaryType != data.primaryType)
            {
                learnset.Add(CreateLearnableSkill(GetTypedSkill(data.secondaryType, "burst"), 13));
            }
            else
            {
                learnset.Add(CreateLearnableSkill(GetTypedSkill(data.primaryType, "break"), 13));
            }

            learnset.Add(CreateLearnableSkill(GetTypedSkill(data.primaryType, "storm"), 17));

            if (data.rarity >= InsectRarity.Epic)
            {
                int signatureLevel = data.rarity == InsectRarity.Legendary ? 20 : 15;
                learnset.Add(CreateLearnableSkill(CreateSignatureSkill(data), signatureLevel));
            }

            return learnset.ToArray();
        }

        private InsectLearnableSkill CreateLearnableSkill(InsectSkill skill, int level)
        {
            return new InsectLearnableSkill
            {
                skillId = skill != null ? skill.skillId : string.Empty,
                learnLevel = level,
                skill = skill
            };
        }

        private InsectSkill GetTraitSkill(InsectData data)
        {
            string id = data != null ? data.insectId ?? string.Empty : string.Empty;
            string traitKey;
            string displayName;
            InsectElement element;
            SkillEffectType effectType = SkillEffectType.Damage;
            int power = 30;
            float effectValue = 0.25f;

            if (id.Contains("spider"))
            {
                traitKey = "web"; displayName = "거미줄 구속"; element = InsectElement.Poison;
                effectType = SkillEffectType.DebuffAttack; power = 1; effectValue = 0.3f;
            }
            else if (id.Contains("mantis"))
            {
                traitKey = "mantis_blade"; displayName = "낫 앞다리 베기"; element = InsectElement.Leaf;
            }
            else if (id.Contains("beetle") || id.Contains("scarab") || id.Contains("ladybug"))
            {
                traitKey = "shell_charge"; displayName = "갑각 돌진"; element = InsectElement.Metal;
            }
            else if (id.Contains("dragonfly") || id.Contains("damselfly"))
            {
                traitKey = "aerial_dash"; displayName = "초고속 비행"; element = InsectElement.Wind; power = 32;
            }
            else if (id.Contains("butterfly") || id.Contains("moth"))
            {
                traitKey = "scale_wind"; displayName = "인분 회오리"; element = InsectElement.Wind;
            }
            else if (IsBeeInsectId(id) || id.Contains("wasp") || id.Contains("hornet") || id.Contains("mosquito"))
            {
                traitKey = "venom_sting"; displayName = "급소 독침"; element = InsectElement.Poison; power = 33;
            }
            else if (IsAntInsectId(id))
            {
                traitKey = "colony_rush"; displayName = "군체 돌격"; element = InsectElement.Earth;
            }
            else if (id.Contains("cricket") || id.Contains("grasshopper") || id.Contains("katydid"))
            {
                traitKey = "leap_crash"; displayName = "도약 강타"; element = InsectElement.Earth;
            }
            else if (id.Contains("firefly") || id.Contains("glow"))
            {
                traitKey = "flash"; displayName = "발광 교란"; element = InsectElement.Light;
                effectType = SkillEffectType.DebuffAttack; power = 1; effectValue = 0.28f;
            }
            else if (data != null && data.primaryType == InsectElement.Water)
            {
                traitKey = "water_skate"; displayName = "수면 질주"; element = InsectElement.Water;
            }
            else
            {
                traitKey = "insect_instinct"; displayName = "야생 본능"; element = InsectElement.Bug;
                effectType = SkillEffectType.BuffAttack; power = 1; effectValue = 0.3f;
            }

            string cacheKey = "trait_" + traitKey;
            if (generatedSkillCache.TryGetValue(cacheKey, out InsectSkill cached)) return cached;

            InsectSkill skill = CreateTypedSkillInternal(
                cacheKey, displayName, element, effectType, power, 2, effectValue, 3);
            skill.trainingCost = 12;
            skill.description = "곤충의 생태와 신체 특징을 살린 종족 기술";
            generatedSkillCache[cacheKey] = skill;
            return skill;
        }

        private InsectSkill CreateSignatureSkill(InsectData data)
        {
            string id = data != null ? data.insectId ?? string.Empty : string.Empty;
            string name;
            InsectElement element = data != null ? data.primaryType : InsectElement.Bug;

            switch (id)
            {
                case "firefly_blue": name = "청람 섬광"; element = InsectElement.Light; break;
                case "dragonfly_emperor": name = "황제의 초음속 강습"; element = InsectElement.Wind; break;
                case "dragonfly_ancient": name = "태고의 비천룡격"; element = InsectElement.Wind; break;
                case "scarab_ancient": name = "태양 성갑 충돌"; element = InsectElement.Metal; break;
                case "mantis_ghost": name = "유령 낫 연무"; element = InsectElement.Dark; break;
                case "atlas_moth_giant": name = "아틀라스 환월분"; element = InsectElement.Dark; break;
                case "beetle_hercules": name = "헤라클레스 천공각"; element = InsectElement.Metal; break;
                case "leaf_insect_phantom": name = "환엽 천변격"; element = InsectElement.Leaf; break;
                case "butterfly_alexandras": name = "여왕의 천공무"; element = InsectElement.Wind; break;
                case "luna_moth_silver": name = "은월 인분폭풍"; element = InsectElement.Light; break;
                case "jewel_beetle_gold": name = "황금 보석광선"; element = InsectElement.Light; break;
                case "mantis_orchid": name = "난초 단두참"; element = InsectElement.Leaf; break;
                case "beetle_golden_stag": name = "황금 대악력"; element = InsectElement.Metal; break;
                case "gacha_crystal_dragonfly": name = "수정 초음속 강습"; element = InsectElement.Light; break;
                case "gacha_shadow_mantis": name = "암영 단두참"; element = InsectElement.Dark; break;
                case "gacha_rainbow_butterfly": name = "칠색 오로라"; element = InsectElement.Light; break;
                case "gacha_diamond_beetle": name = "다이아몬드 대충각"; element = InsectElement.Metal; break;
                case "gacha_ice_spider": name = "빙결 거미줄 지옥"; element = InsectElement.Water; break;
                case "gacha_storm_hornet": name = "뇌제 독침"; element = InsectElement.Electric; break;
                case "gacha_celestial_beetle": name = "성천 갑각성"; element = InsectElement.Light; break;
                // ── 확장 64종 Epic+ 17종 전용기 (element는 primary/secondary 중 하나와 일치 검산됨) ──
                case "firefly_marsh": name = "늪 도깨비불"; element = InsectElement.Light; break;
                case "rhinoceros_beetle_titan": name = "타이탄 대뿔 붕격"; element = InsectElement.Metal; break;
                case "mantis_dead_leaf": name = "낙엽 은신참"; element = InsectElement.Leaf; break;
                case "bee_queen": name = "여왕의 칙령"; element = InsectElement.Wind; break;
                case "wasp_night": name = "어둠침 일섬"; element = InsectElement.Dark; break;
                case "spider_bog_widow": name = "검은늪 독아"; element = InsectElement.Poison; break;
                case "mantis_mist": name = "안개 낫질"; element = InsectElement.Leaf; break;
                case "stag_beetle_iron": name = "강철 대악력"; element = InsectElement.Metal; break;
                case "jewel_beetle_azure": name = "청람 보석광선"; element = InsectElement.Light; break;
                case "moth_shadow": name = "그림자 인분무"; element = InsectElement.Dark; break;
                case "wasp_gold": name = "황금 독침 강타"; element = InsectElement.Metal; break;
                case "cicada_ancient": name = "태고의 공명진동"; element = InsectElement.Earth; break;
                case "moth_comet": name = "혜성 꼬리 낙하"; element = InsectElement.Light; break;
                case "scarab_pharaoh": name = "태양신의 심판"; element = InsectElement.Metal; break;
                case "butterfly_midnight": name = "그믐밤 여왕무"; element = InsectElement.Dark; break;
                case "hornet_emperor": name = "황제의 처형침"; element = InsectElement.Poison; break;
                case "mantis_gold_temple": name = "황금 신전 단두참"; element = InsectElement.Metal; break;
                default: name = "궁극 생태 해방"; break;
            }

            string skillId = id + "_signature";
            if (generatedSkillCache.TryGetValue(skillId, out InsectSkill cached)) return cached;

            int power = data != null && data.rarity == InsectRarity.Legendary ? 78 : 60;
            int cooldown = data != null && data.rarity == InsectRarity.Legendary ? 5 : 4;
            InsectSkill signature = CreateTypedSkillInternal(
                skillId, name, element, SkillEffectType.Damage, power, cooldown, 0.2f, 2);
            signature.isSignatureSkill = true;
            signature.trainingCost = data != null && data.rarity == InsectRarity.Legendary ? 50 : 35;
            signature.description = "이 종만 사용할 수 있는 전용 필살기";
            generatedSkillCache[skillId] = signature;
            return signature;
        }

        private InsectSkill[] ExtractUniqueSkills(InsectLearnableSkill[] learnset)
        {
            List<InsectSkill> result = new List<InsectSkill>();
            HashSet<string> seen = new HashSet<string>();

            if (learnset == null)
            {
                return result.ToArray();
            }

            foreach (InsectLearnableSkill learnable in learnset)
            {
                if (learnable == null || learnable.skill == null || string.IsNullOrEmpty(learnable.skillId) || !seen.Add(learnable.skillId))
                {
                    continue;
                }

                result.Add(learnable.skill);
            }

            return result.ToArray();
        }

        private InsectSkill GetTypedSkill(InsectElement element, string tier)
        {
            string key = $"{element}_{tier}";
            if (generatedSkillCache.TryGetValue(key, out InsectSkill cached))
            {
                return cached;
            }

            InsectSkill skill = CreateTypedSkill(element, tier);
            generatedSkillCache[key] = skill;
            return skill;
        }

        private InsectSkill CreateTypedSkill(InsectElement element, string tier)
        {
            string label = GetElementLabel(element);
            InsectSkill skill;
            switch (tier)
            {
                case "jab":
                    skill = CreateTypedSkillInternal($"{element.ToString().ToLowerInvariant()}_jab", $"{label} 연타", element, SkillEffectType.Damage, 12, 0, 0.2f, 2);
                    skill.trainingCost = 4;
                    return skill;
                case "boost":
                    if (UsesBuffSkill(element))
                        skill = CreateTypedSkillInternal($"{element.ToString().ToLowerInvariant()}_boost", $"{label} 집중", element, SkillEffectType.BuffAttack, 1, 3, 0.3f, 3);
                    else
                        skill = CreateTypedSkillInternal($"{element.ToString().ToLowerInvariant()}_boost", $"{label} 압박", element, SkillEffectType.DebuffAttack, 1, 3, 0.25f, 3);
                    skill.trainingCost = 8;
                    return skill;
                case "burst":
                    skill = CreateTypedSkillInternal($"{element.ToString().ToLowerInvariant()}_burst", $"{label} 폭발", element, SkillEffectType.Damage, 26, 2, 0.2f, 2);
                    skill.trainingCost = 14;
                    return skill;
                case "break":
                    skill = CreateTypedSkillInternal($"{element.ToString().ToLowerInvariant()}_break", $"{label} 붕괴", element, SkillEffectType.DebuffAttack, 1, 3, 0.3f, 2);
                    skill.trainingCost = 14;
                    return skill;
                case "storm":
                    skill = CreateTypedSkillInternal($"{element.ToString().ToLowerInvariant()}_storm", $"{label} 폭풍", element, SkillEffectType.Damage, 42, 4, 0.2f, 2);
                    skill.trainingCost = 22;
                    return skill;
                default:
                    skill = CreateTypedSkillInternal($"{element.ToString().ToLowerInvariant()}_nova", $"{label} 노바", element, SkillEffectType.Damage, 52, 5, 0.2f, 2);
                    skill.trainingCost = 28;
                    return skill;
            }
        }

        private InsectSkill CreateTypedSkillInternal(string skillId, string displayName, InsectElement element, SkillEffectType type, int power, int cooldown, float effectValue, int effectDuration)
        {
            InsectSkill skill = CreateSkill(skillId, displayName, type, power, cooldown, effectValue, effectDuration);
            skill.element = element;
            skill.description = $"{GetElementLabel(element)} 타입의 힘을 사용하는 기술";
            return skill;
        }

        private static bool UsesBuffSkill(InsectElement element)
        {
            return element == InsectElement.Leaf || element == InsectElement.Light || element == InsectElement.Wind || element == InsectElement.Electric;
        }

        private static string GetElementLabel(InsectElement element)
        {
            switch (element)
            {
                case InsectElement.Leaf: return "풀";
                case InsectElement.Water: return "물";
                case InsectElement.Wind: return "바람";
                case InsectElement.Electric: return "전격";
                case InsectElement.Earth: return "대지";
                case InsectElement.Poison: return "맹독";
                case InsectElement.Light: return "빛";
                case InsectElement.Dark: return "그림자";
                case InsectElement.Metal: return "강철";
                default: return "벌레";
            }
        }

        private InsectSkill[] CollectAllSkills(InsectDatabase database, InsectSkill[] trainingSkills)
        {
            Dictionary<string, InsectSkill> allSkills = new Dictionary<string, InsectSkill>();

            if (database != null && database.insects != null)
            {
                foreach (InsectData insect in database.insects)
                {
                    if (insect == null)
                    {
                        continue;
                    }

                    if (insect.skills != null)
                    {
                        foreach (InsectSkill skill in insect.skills)
                        {
                            if (skill != null && !string.IsNullOrEmpty(skill.skillId))
                            {
                                allSkills[skill.skillId] = skill;
                            }
                        }
                    }

                    if (insect.learnset != null)
                    {
                        foreach (InsectLearnableSkill learnable in insect.learnset)
                        {
                            if (learnable != null && learnable.skill != null && !string.IsNullOrEmpty(learnable.skill.skillId))
                            {
                                allSkills[learnable.skill.skillId] = learnable.skill;
                            }
                        }
                    }
                }
            }

            if (trainingSkills != null)
            {
                foreach (InsectSkill skill in trainingSkills)
                {
                    if (skill != null && !string.IsNullOrEmpty(skill.skillId))
                    {
                        allSkills[skill.skillId] = skill;
                    }
                }
            }

            InsectSkill[] result = new InsectSkill[allSkills.Count];
            allSkills.Values.CopyTo(result, 0);
            return result;
        }


        private int GetStableValue(string text)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < text.Length; i++)
                {
                    hash = hash * 31 + text[i];
                }

                return hash == int.MinValue ? 0 : Mathf.Abs(hash);
            }
        }

        private static bool IsAntInsectId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return id.StartsWith("ant_") || id.Contains("_ant_") || id.Contains("antlion");
        }

        /// <summary>
        /// 스폰포인트가 서브에리어 원 안에 있으면 곤충이 그 안에 스폰되고, 플레이어가 잡으러
        /// 들어간 순간 잡기 E(CaptureInputController)와 서브에리어 진입 E(SubAreaWorldBuilder)가
        /// 같은 프레임에 동시 발화한다 — 원 밖으로 밀어낸다.
        /// 마진 9m = SpawnPoint 산포 반경 5m + 여유 4m (곤충까지 원 밖 보장).
        /// </summary>
        private static Vector3 PushOutOfSubAreas(Vector3 pos, Data.RegionData region)
        {
            if (region == null || region.subAreas == null) return pos;

            foreach (var sub in region.subAreas)
            {
                if (sub == null) continue;
                Vector3 d = pos - sub.centerPosition;
                d.y = 0f;
                float safe = sub.radius + 9f;
                if (d.sqrMagnitude < safe * safe)
                {
                    if (d.sqrMagnitude < 0.01f) d = Vector3.right;
                    pos = sub.centerPosition + d.normalized * safe;
                    pos.y = 0f;
                }
            }
            return pos;
        }

        private static bool IsBeeInsectId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return id.StartsWith("bee_") || id.EndsWith("_bee") || id.Contains("_bee_");
        }

        private SpawnPoint[] EnsureSpawnPoints()
        {
            SpawnPoint[] existing = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
            if (existing != null && existing.Length > 0)
            {
                return existing;
            }

            Data.RegionData[] regionDefs = RegionDefinitions.CreateAll();
            List<SpawnPoint> points = new List<SpawnPoint>();
            int basePerRegion = Mathf.Max(4, Mathf.CeilToInt(spawnPointCount / (float)Mathf.Max(1, regionDefs.Length)));

            foreach (var region in regionDefs)
            {
                // 맵 1.5배 확장에 맞춰 리전당 포인트 수를 반경 비례로 (r=60→5, r=82.5→8).
                int pointsPerRegion = Mathf.Max(basePerRegion, Mathf.RoundToInt(region.radius / 11f));
                for (int i = 0; i < pointsPerRegion; i++)
                {
                    float angle = Mathf.PI * 2f * i / pointsPerRegion;
                    // 2링 배치(내측 0.35R / 외측 0.65R) — 넓어진 리전 외곽까지 스폰 커버.
                    float dist = region.radius * ((i % 2 == 0) ? 0.35f : 0.65f);
                    Vector3 pos = region.centerPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
                    pos = PushOutOfSubAreas(pos, region);
                    GameObject pointObj = new GameObject($"SpawnPoint_{region.regionId}_{i + 1}");
                    pointObj.transform.position = pos;
                    SpawnPoint sp = pointObj.AddComponent<SpawnPoint>();
                    sp.regionId = region.regionId;
                    sp.regionInsectIds = region.insectIds;
                    sp.regionMinLevel = Mathf.Max(1, region.requiredLevel);
                    sp.regionMaxLevel = Mathf.Max(sp.regionMinLevel, region.requiredLevel + GetRegionLevelRange(region.regionId));
                    points.Add(sp);
                }
            }

            // 서브구역 스폰포인트
            foreach (var region in regionDefs)
            {
                if (region.subAreas == null) continue;
                foreach (var sub in region.subAreas)
                {
                    int subPoints = 4; // 서브구역당 4개 (맵 확장에 맞춰 3→4)
                    for (int i = 0; i < subPoints; i++)
                    {
                        float angle = Mathf.PI * 2f * i / subPoints;
                        float dist = sub.radius * 0.5f;
                        Vector3 pos = sub.centerPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
                        // 전용종은 게이트 원 바깥 가장자리에 출현 — 원 안은 진입 E와 잡기 E의 충돌 지대
                        pos = PushOutOfSubAreas(pos, region);
                        GameObject pointObj = new GameObject($"SpawnPoint_{sub.subAreaId}_{i + 1}");
                        pointObj.transform.position = pos;
                        SpawnPoint sp = pointObj.AddComponent<SpawnPoint>();
                        sp.regionId = region.regionId;
                        sp.regionInsectIds = sub.exclusiveInsectIds;
                        sp.regionMinLevel = sub.minLevel;
                        sp.regionMaxLevel = sub.maxLevel;
                        points.Add(sp);
                    }
                }
            }

            return points.ToArray();
        }

        private int GetRegionLevelRange(string regionId)
        {
            switch (regionId)
            {
                case "meadow": return 9;      // Lv.1~10
                case "pond": return 10;       // Lv.6~16
                case "forest": return 12;     // Lv.12~24
                case "swamp": return 12;      // Lv.20~32
                case "mountain": return 12;   // Lv.28~40
                case "garden": return 17;     // Lv.18~35
                case "ruins": return 14;      // Lv.36~50
                default: return 5;
            }
        }

        private void EnsureInsectPrefab(InsectSpawner spawner)
        {
            if (spawner == null)
            {
                return;
            }

            GameObject prefab = GameObject.Find("InsectPrefab");
            if (prefab == null)
            {
                prefab = new GameObject("InsectPrefab");

                GameObject bodyPart = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bodyPart.name = "Body";
                bodyPart.transform.SetParent(prefab.transform, false);
                bodyPart.transform.localPosition = Vector3.zero;
                bodyPart.transform.localScale = new Vector3(0.6f, 0.35f, 0.8f);

                GameObject headPart = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                headPart.name = "Head";
                headPart.transform.SetParent(prefab.transform, false);
                headPart.transform.localPosition = new Vector3(0f, 0.05f, 0.4f);
                headPart.transform.localScale = new Vector3(0.35f, 0.3f, 0.35f);

                GameObject antennaL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                antennaL.name = "AntennaL";
                antennaL.transform.SetParent(headPart.transform, false);
                antennaL.transform.localPosition = new Vector3(-0.3f, 0.6f, 0.3f);
                antennaL.transform.localScale = new Vector3(0.08f, 0.4f, 0.08f);
                antennaL.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
                Object.Destroy(antennaL.GetComponent<Collider>());

                GameObject antennaR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                antennaR.name = "AntennaR";
                antennaR.transform.SetParent(headPart.transform, false);
                antennaR.transform.localPosition = new Vector3(0.3f, 0.6f, 0.3f);
                antennaR.transform.localScale = new Vector3(0.08f, 0.4f, 0.08f);
                antennaR.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
                Object.Destroy(antennaR.GetComponent<Collider>());

                prefab.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                prefab.SetActive(false);
            }

            spawner.GetType().GetField("defaultPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(spawner, prefab);
        }

        private void BuildUI(DexUIController dexSummary, DexDetailUIController dexDetail, DexListUIController dexList,
            CaptureMinigameController minigame, CaptureInputController input, CaptureFeedbackController feedback)
        {
            if (preferUIPrefab && TryBuildUIFromPrefab(dexSummary, dexDetail, dexList, minigame, input, feedback))
            {
                return;
            }

            Canvas canvas = EnsureCanvas();
            RectTransform root = canvas.GetComponent<RectTransform>();
            root.sizeDelta = canvasSize;

            Font font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 28);
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Text discoveredText = CreateText(canvas.transform, "DiscoveredText", new Vector2(20f, -20f), "발견: 0", font);
            SetAnchorTopLeft(discoveredText.rectTransform);
            Text capturedText = CreateText(canvas.transform, "CapturedText", new Vector2(20f, -55f), "포획: 0", font);
            SetAnchorTopLeft(capturedText.rectTransform);
            dexSummary.GetType().GetField("discoveredText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexSummary, discoveredText);
            dexSummary.GetType().GetField("capturedText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexSummary, capturedText);

            Text levelText = CreateText(canvas.transform, "PlayerLevelText", new Vector2(20f, -90f), "레벨 1 (0/0)", font);
            SetAnchorTopLeft(levelText.rectTransform);
            Text xpText = CreateText(canvas.transform, "PlayerXpText", new Vector2(20f, -125f), "0/0", font);
            SetAnchorTopLeft(xpText.rectTransform);
            Text candyText = CreateText(canvas.transform, "PlayerCandyText", new Vector2(20f, -160f), "사탕 0", font);
            SetAnchorTopLeft(candyText.rectTransform);
            PlayerProgressUIController progressUi = EnsureComponent<PlayerProgressUIController>("UI/PlayerProgressUI");
            progressUi.GetType().GetField("levelText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(progressUi, levelText);
            progressUi.GetType().GetField("xpText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(progressUi, xpText);
            progressUi.GetType().GetField("candyText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(progressUi, candyText);

            TMP_Text gemsText = CreateTMPText(canvas.transform, "GemsText", new Vector2(20f, -195f), "Gems 0");
            SetAnchorTopLeft(gemsText.rectTransform);
            TMP_Text coinsText = CreateTMPText(canvas.transform, "CoinsText", new Vector2(20f, -230f), "Coins 0");
            SetAnchorTopLeft(coinsText.rectTransform);
            PlayerCurrencyUIController currencyUi = EnsureComponent<PlayerCurrencyUIController>("UI/CurrencyUI");
            currencyUi.GetType().GetField("gemsText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(currencyUi, gemsText);
            currencyUi.GetType().GetField("coinsText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(currencyUi, coinsText);

            Text popupText = CreateText(canvas.transform, "PopupText", new Vector2(0f, -300f), "", font, TextAnchor.MiddleCenter);
            popupText.enabled = false;
            feedback.GetType().GetField("popupText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(feedback, popupText);

            GameObject capturePanel = CreatePanel(canvas.transform, "CapturePanel", new Vector2(0f, -600f), new Vector2(600f, 200f));
            Slider slider = CreateSlider(capturePanel.transform, "TimingSlider", new Vector2(0f, 20f), new Vector2(400f, 20f));
            Button confirmButton = CreateButton(capturePanel.transform, "ConfirmButton", new Vector2(-120f, -40f), "포획", font);
            Button cancelButton = CreateButton(capturePanel.transform, "CancelButton", new Vector2(120f, -40f), "취소", font);

            minigame.GetType().GetField("timingSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(minigame, slider);
            minigame.GetType().GetField("panelRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(minigame, capturePanel);

            confirmButton.onClick.AddListener(minigame.ConfirmCapture);
            cancelButton.onClick.AddListener(minigame.CancelCapture);

            // Capture start is handled by E-key and OnGUI hint

            GameObject listPanel = CreatePanel(canvas.transform, "DexListPanel", new Vector2(-360f, -300f), new Vector2(360f, 500f));
            RectTransform listRoot = new GameObject("ListRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            listRoot.SetParent(listPanel.transform, false);
            listRoot.anchorMin = new Vector2(0f, 0f);
            listRoot.anchorMax = new Vector2(1f, 1f);
            listRoot.offsetMin = new Vector2(20f, 20f);
            listRoot.offsetMax = new Vector2(-20f, -20f);
            listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            listRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            DexListItemUI itemPrefab = CreateListItemPrefab(listRoot, font);
            itemPrefab.gameObject.SetActive(false);

            dexList.AutoWire(null, null, dexDetail, listRoot, itemPrefab);

            GameObject detailPanel = CreatePanel(canvas.transform, "DexDetailPanel", new Vector2(360f, -300f), new Vector2(360f, 500f));
            Text detailName = CreateText(detailPanel.transform, "NameText", new Vector2(0f, -30f), "???", font, TextAnchor.UpperLeft);
            Text detailRarity = CreateText(detailPanel.transform, "RarityText", new Vector2(0f, -70f), "등급: ???", font, TextAnchor.UpperLeft);
            Text detailPower = CreateText(detailPanel.transform, "PowerText", new Vector2(0f, -110f), "기본 힘: ???", font, TextAnchor.UpperLeft);
            Text detailDesc = CreateText(detailPanel.transform, "DescText", new Vector2(0f, -170f), "설명", font, TextAnchor.UpperLeft);
            Text detailHint = CreateText(detailPanel.transform, "HintText", new Vector2(0f, -260f), "힌트", font, TextAnchor.UpperLeft);
            Text detailCount = CreateText(detailPanel.transform, "CountText", new Vector2(0f, -330f), "발견 0 / 포획 0", font, TextAnchor.UpperLeft);
            Text detailReward = CreateText(detailPanel.transform, "RewardText", new Vector2(0f, -370f), "보상: ???", font, TextAnchor.UpperLeft);

            dexDetail.GetType().GetField("panelRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, detailPanel);
            dexDetail.GetType().GetField("nameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, detailName);
            dexDetail.GetType().GetField("rarityText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, detailRarity);
            dexDetail.GetType().GetField("powerText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, detailPower);
            dexDetail.GetType().GetField("descriptionText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, detailDesc);
            dexDetail.GetType().GetField("hintText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, detailHint);
            dexDetail.GetType().GetField("countText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, detailCount);
            dexDetail.GetType().GetField("rewardText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, detailReward);

            GameObject battlePanel = CreatePanel(canvas.transform, "BattlePanel", new Vector2(0f, -380f), new Vector2(640f, 320f));
            Slider playerHpBar = CreateSlider(battlePanel.transform, "PlayerHpBar", new Vector2(-120f, -40f), new Vector2(200f, 20f));
            Slider enemyHpBar = CreateSlider(battlePanel.transform, "EnemyHpBar", new Vector2(120f, -40f), new Vector2(200f, 20f));
            Text playerHpText = CreateText(battlePanel.transform, "PlayerHpText", new Vector2(-120f, -70f), "0/0", font, TextAnchor.MiddleCenter);
            Text enemyHpText = CreateText(battlePanel.transform, "EnemyHpText", new Vector2(120f, -70f), "0/0", font, TextAnchor.MiddleCenter);
            Button skill1 = CreateButton(battlePanel.transform, "Skill1Button", new Vector2(-160f, -130f), "스킬1", font);
            Button skill2 = CreateButton(battlePanel.transform, "Skill2Button", new Vector2(0f, -130f), "스킬2", font);
            Button skill3 = CreateButton(battlePanel.transform, "Skill3Button", new Vector2(160f, -130f), "스킬3", font);
            Text skillCd1 = CreateText(battlePanel.transform, "Skill1Cooldown", new Vector2(-160f, -160f), "", font, TextAnchor.MiddleCenter);
            Text skillCd2 = CreateText(battlePanel.transform, "Skill2Cooldown", new Vector2(0f, -160f), "", font, TextAnchor.MiddleCenter);
            Text skillCd3 = CreateText(battlePanel.transform, "Skill3Cooldown", new Vector2(160f, -160f), "", font, TextAnchor.MiddleCenter);
            // Battle start is handled through CaptureChoiceUI team select
            GameObject resultPanel = CreatePanel(canvas.transform, "BattleResultPanel", new Vector2(0f, -300f), new Vector2(300f, 120f));
            Text resultText = CreateText(resultPanel.transform, "ResultText", new Vector2(0f, -30f), "승리!", font, TextAnchor.MiddleCenter);
            resultPanel.SetActive(false);

            Battle.InsectBattleUIController battleUi = EnsureComponent<Battle.InsectBattleUIController>("Battle/BattleUI");
            battleUi.GetType().GetField("panelRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, battlePanel);
            battleUi.GetType().GetField("playerHpBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, playerHpBar);
            battleUi.GetType().GetField("enemyHpBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, enemyHpBar);
            battleUi.GetType().GetField("playerHpText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, null);
            battleUi.GetType().GetField("enemyHpText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, null);
            battleUi.GetType().GetField("playerHpTextLegacy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, playerHpText);
            battleUi.GetType().GetField("enemyHpTextLegacy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, enemyHpText);
            battleUi.GetType().GetField("skillButtons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, new[] { skill1, skill2, skill3 });
            battleUi.GetType().GetField("skillLabels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, null);
            battleUi.GetType().GetField("skillCooldownLegacy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, new[] { skillCd1, skillCd2, skillCd3 });
            battleUi.GetType().GetField("startBattleButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, null);
            battleUi.GetType().GetField("resultPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, resultPanel);
            battleUi.GetType().GetField("resultTextLegacy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, resultText);

            GameObject levelUpPanel = CreatePanel(canvas.transform, "LevelUpPanel", new Vector2(0f, -430f), new Vector2(300f, 220f));
            Text levelUpName = CreateText(levelUpPanel.transform, "LevelUpName", new Vector2(0f, -30f), "곤충", font, TextAnchor.MiddleCenter);
            Text levelUpLevel = CreateText(levelUpPanel.transform, "LevelUpLevel", new Vector2(0f, -70f), "Lv 1", font, TextAnchor.MiddleCenter);
            Text levelUpCost = CreateText(levelUpPanel.transform, "LevelUpCost", new Vector2(0f, -110f), "사탕 0", font, TextAnchor.MiddleCenter);
            Button levelUpButton = CreateButton(levelUpPanel.transform, "LevelUpButton", new Vector2(0f, -150f), "레벨업", font);
            Text levelUpResult = CreateText(levelUpPanel.transform, "LevelUpResult", new Vector2(0f, -190f), "", font, TextAnchor.MiddleCenter);

            PlayerInsectLevelUpUIController levelUpUi = EnsureComponent<PlayerInsectLevelUpUIController>("UI/LevelUpUI");
            levelUpUi.GetType().GetField("levelUpButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(levelUpUi, levelUpButton);
            levelUpUi.GetType().GetField("insectNameTextLegacy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(levelUpUi, levelUpName);
            levelUpUi.GetType().GetField("insectLevelTextLegacy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(levelUpUi, levelUpLevel);
            levelUpUi.GetType().GetField("candyCostTextLegacy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(levelUpUi, levelUpCost);
            levelUpUi.GetType().GetField("resultTextLegacy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(levelUpUi, levelUpResult);

            GameObject inventoryPanel = CreatePanel(canvas.transform, "InventoryPanel", new Vector2(300f, -400f), new Vector2(320f, 200f));
            RectTransform inventoryRoot = new GameObject("InventoryGridRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            inventoryRoot.SetParent(inventoryPanel.transform, false);
            inventoryRoot.anchorMin = new Vector2(0f, 0f);
            inventoryRoot.anchorMax = new Vector2(1f, 1f);
            inventoryRoot.offsetMin = new Vector2(10f, 10f);
            inventoryRoot.offsetMax = new Vector2(-10f, -40f);
            inventoryRoot.gameObject.AddComponent<GridLayoutGroup>().cellSize = new Vector2(140f, 60f);

            TMP_Text activeItemText = CreateTMPText(inventoryPanel.transform, "ActiveItemText", new Vector2(0f, -160f), "사용중: 없음");
            activeItemText.alignment = TextAlignmentOptions.Center;
            Slider activeTimeBar = CreateSlider(inventoryPanel.transform, "ActiveTimeBar", new Vector2(0f, -130f), new Vector2(240f, 16f));
            TMP_Text activeTimeText = CreateTMPText(inventoryPanel.transform, "ActiveTimeText", new Vector2(0f, -190f), "남은 시간: 00:00");
            activeTimeText.alignment = TextAlignmentOptions.Center;
            Image activeTimeRadial = CreateRadialImage(inventoryPanel.transform, "ActiveTimeRadial", new Vector2(120f, -40f), new Vector2(36f, 36f), true);
            Image activeTimeIcon = CreateRadialImage(inventoryPanel.transform, "ActiveTimeIcon", new Vector2(120f, -40f), new Vector2(28f, 28f), false);

            ItemInventoryGridItem inventoryItemPrefab = CreateInventoryItemPrefab(inventoryRoot);
            inventoryItemPrefab.gameObject.SetActive(false);

            PlayerItemInventoryGridUIController inventoryUi = EnsureComponent<PlayerItemInventoryGridUIController>("UI/InventoryUI");
            inventoryUi.GetType().GetField("contentRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, inventoryRoot);
            inventoryUi.GetType().GetField("itemPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, inventoryItemPrefab);
            inventoryUi.GetType().GetField("activeItemText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, activeItemText);
            inventoryUi.GetType().GetField("remainingTimeBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, activeTimeBar);
            inventoryUi.GetType().GetField("remainingTimeText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, activeTimeText);
            inventoryUi.GetType().GetField("remainingTimeRadial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, activeTimeRadial);
            inventoryUi.GetType().GetField("remainingTimeIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, activeTimeIcon);

            GameObject shopPanel = CreatePanel(canvas.transform, "ShopPanel", new Vector2(-300f, -400f), new Vector2(360f, 220f));
            Button buy1 = CreateButton(shopPanel.transform, "BuyButton1", new Vector2(0f, -40f), "구매1", font);
            Button buy2 = CreateButton(shopPanel.transform, "BuyButton2", new Vector2(0f, -100f), "구매2", font);
            Button buy3 = CreateButton(shopPanel.transform, "BuyButton3", new Vector2(0f, -160f), "구매3", font);
            TMP_Text shopResult = CreateTMPText(shopPanel.transform, "ShopResult", new Vector2(0f, -200f), "");
            shopResult.alignment = TextAlignmentOptions.Center;

            ShopUIController shopUi = EnsureComponent<ShopUIController>("UI/ShopUI");
            shopUi.GetType().GetField("buyButtons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(shopUi, new[] { buy1, buy2, buy3 });
            shopUi.GetType().GetField("buyLabels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(shopUi, new TMP_Text[] {
                    buy1.GetComponentInChildren<TextMeshProUGUI>(),
                    buy2.GetComponentInChildren<TextMeshProUGUI>(),
                    buy3.GetComponentInChildren<TextMeshProUGUI>()
                });
            shopUi.GetType().GetField("resultText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(shopUi, shopResult);
            Toggle coinsToggle = CreateToggle(shopPanel.transform, "CoinsToggle", new Vector2(-60f, -10f), new Vector2(120f, 24f), "코인");
            Toggle gemsToggle = CreateToggle(shopPanel.transform, "GemsToggle", new Vector2(60f, -10f), new Vector2(120f, 24f), "보석");
            shopUi.GetType().GetField("coinsToggle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(shopUi, coinsToggle);
            shopUi.GetType().GetField("gemsToggle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(shopUi, gemsToggle);
            TMP_Text paymentLabel = CreateTMPText(shopPanel.transform, "PaymentLabel", new Vector2(0f, -230f), "결제: 보석 또는 코인");
            paymentLabel.alignment = TextAlignmentOptions.Center;
            shopUi.GetType().GetField("paymentLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(shopUi, paymentLabel);

            GameObject tuningPanel = CreatePanel(canvas.transform, "RarityTuningPanel", new Vector2(0f, -400f), new Vector2(360f, 260f));
            Slider commonSlider = CreateSlider(tuningPanel.transform, "CommonSlider", new Vector2(0f, -30f), new Vector2(240f, 16f));
            Slider uncommonSlider = CreateSlider(tuningPanel.transform, "UncommonSlider", new Vector2(0f, -70f), new Vector2(240f, 16f));
            Slider rareSlider = CreateSlider(tuningPanel.transform, "RareSlider", new Vector2(0f, -110f), new Vector2(240f, 16f));
            Slider epicSlider = CreateSlider(tuningPanel.transform, "EpicSlider", new Vector2(0f, -150f), new Vector2(240f, 16f));
            Slider legendarySlider = CreateSlider(tuningPanel.transform, "LegendarySlider", new Vector2(0f, -190f), new Vector2(240f, 16f));
            TMP_Text commonLabel = CreateTMPText(tuningPanel.transform, "CommonLabel", new Vector2(0f, -10f), "Common 0.05");
            TMP_Text uncommonLabel = CreateTMPText(tuningPanel.transform, "UncommonLabel", new Vector2(0f, -50f), "Uncommon 0.08");
            TMP_Text rareLabel = CreateTMPText(tuningPanel.transform, "RareLabel", new Vector2(0f, -90f), "Rare 0.12");
            TMP_Text epicLabel = CreateTMPText(tuningPanel.transform, "EpicLabel", new Vector2(0f, -130f), "Epic 0.18");
            TMP_Text legendaryLabel = CreateTMPText(tuningPanel.transform, "LegendaryLabel", new Vector2(0f, -170f), "Legendary 0.25");

            ItemRarityTuningUIController tuningUi = EnsureComponent<ItemRarityTuningUIController>("UI/RarityTuningUI");
            tuningUi.GetType().GetField("palette", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, Resources.Load<Data.ItemRarityPalette>("ItemRarityPalette"));
            tuningUi.GetType().GetField("commonSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, commonSlider);
            tuningUi.GetType().GetField("uncommonSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, uncommonSlider);
            tuningUi.GetType().GetField("rareSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, rareSlider);
            tuningUi.GetType().GetField("epicSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, epicSlider);
            tuningUi.GetType().GetField("legendarySlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, legendarySlider);
            tuningUi.GetType().GetField("commonLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, commonLabel);
            tuningUi.GetType().GetField("uncommonLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, uncommonLabel);
            tuningUi.GetType().GetField("rareLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, rareLabel);
            tuningUi.GetType().GetField("epicLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, epicLabel);
            tuningUi.GetType().GetField("legendaryLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, legendaryLabel);

            capturePanel.SetActive(false);
            listPanel.SetActive(false);
            detailPanel.SetActive(false);
            battlePanel.SetActive(false);
            levelUpPanel.SetActive(false);
            inventoryPanel.SetActive(false);
            shopPanel.SetActive(false);
            tuningPanel.SetActive(false);
        }

        private bool TryBuildUIFromPrefab(DexUIController dexSummary, DexDetailUIController dexDetail, DexListUIController dexList,
            CaptureMinigameController minigame, CaptureInputController input, CaptureFeedbackController feedback)
        {
            GameObject prefab = null;
            PlayUIConfig config = Resources.Load<PlayUIConfig>(uiConfigResourcePath);
            if (config != null && config.playHudPrefab != null)
            {
                prefab = config.playHudPrefab;
            }
            else
            {
                prefab = Resources.Load<GameObject>(uiPrefabResourcePath);
            }

            if (prefab == null)
            {
                return false;
            }

            GameObject instance = Instantiate(prefab);
            PlayUIRefs refs = instance.GetComponentInChildren<PlayUIRefs>();
            if (refs == null)
            {
                Destroy(instance);
                return false;
            }

            EnsureCanvas();
            WireFromRefs(refs, dexSummary, dexDetail, dexList, minigame, input, feedback);
            return true;
        }

        private void WireFromRefs(PlayUIRefs refs, DexUIController dexSummary, DexDetailUIController dexDetail, DexListUIController dexList,
            CaptureMinigameController minigame, CaptureInputController input, CaptureFeedbackController feedback)
        {
            if (refs == null)
            {
                return;
            }

            dexSummary.GetType().GetField("discoveredText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexSummary, refs.discoveredText);
            dexSummary.GetType().GetField("capturedText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexSummary, refs.capturedText);
            dexSummary.GetType().GetField("discoveredTextTmp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexSummary, refs.discoveredTextTmp);
            dexSummary.GetType().GetField("capturedTextTmp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexSummary, refs.capturedTextTmp);

            minigame.GetType().GetField("timingSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(minigame, refs.timingSlider);
            minigame.GetType().GetField("panelRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(minigame, refs.capturePanel);

            if (refs.confirmButton != null)
            {
                refs.confirmButton.onClick.RemoveAllListeners();
                refs.confirmButton.onClick.AddListener(minigame.ConfirmCapture);
            }

            if (refs.cancelButton != null)
            {
                refs.cancelButton.onClick.RemoveAllListeners();
                refs.cancelButton.onClick.AddListener(minigame.CancelCapture);
            }

            if (refs.startCaptureButton != null)
            {
                refs.startCaptureButton.onClick.RemoveAllListeners();
                refs.startCaptureButton.onClick.AddListener(input.TryStartCapture);
            }

            feedback.GetType().GetField("popupText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(feedback, refs.popupText);
            feedback.GetType().GetField("popupTextTmp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(feedback, refs.popupTextTmp);

            PlayerProgressUIController progressUi = EnsureComponent<PlayerProgressUIController>("UI/PlayerProgressUI");
            progressUi.GetType().GetField("levelText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(progressUi, refs.playerLevelText);
            progressUi.GetType().GetField("xpText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(progressUi, refs.playerXpText);
            progressUi.GetType().GetField("levelTextTmp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(progressUi, refs.playerLevelTextTmp);
            progressUi.GetType().GetField("xpTextTmp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(progressUi, refs.playerXpTextTmp);
            progressUi.GetType().GetField("candyTextTmp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(progressUi, refs.playerCandyTextTmp);

            if (refs.listItemPrefab != null)
            {
                refs.listItemPrefab.gameObject.SetActive(false);
            }

            dexList.AutoWire(null, null, dexDetail, refs.listRoot, refs.listItemPrefab);

            dexDetail.GetType().GetField("panelRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailPanel);
            dexDetail.GetType().GetField("nameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailName);
            dexDetail.GetType().GetField("rarityText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailRarity);
            dexDetail.GetType().GetField("powerText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailPower);
            dexDetail.GetType().GetField("descriptionText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailDesc);
            dexDetail.GetType().GetField("hintText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailHint);
            dexDetail.GetType().GetField("countText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailCount);
            dexDetail.GetType().GetField("rewardText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailReward);

            dexDetail.GetType().GetField("nameTextTmp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailNameTmp);
            dexDetail.GetType().GetField("rarityTextTmp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailRarityTmp);
            dexDetail.GetType().GetField("powerTextTmp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailPowerTmp);
            dexDetail.GetType().GetField("descriptionTextTmp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailDescTmp);
            dexDetail.GetType().GetField("hintTextTmp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailHintTmp);
            dexDetail.GetType().GetField("countTextTmp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailCountTmp);
            dexDetail.GetType().GetField("rewardTextTmp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(dexDetail, refs.detailRewardTmp);

            Battle.InsectBattleUIController battleUi = EnsureComponent<Battle.InsectBattleUIController>("Battle/BattleUI");
            battleUi.GetType().GetField("panelRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.battlePanel);
            battleUi.GetType().GetField("playerHpBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.playerHpBar);
            battleUi.GetType().GetField("enemyHpBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.enemyHpBar);
            battleUi.GetType().GetField("playerHpText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.playerHpText);
            battleUi.GetType().GetField("enemyHpText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.enemyHpText);
            battleUi.GetType().GetField("skillButtons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.skillButtons);
            battleUi.GetType().GetField("skillLabels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.skillLabels);
            battleUi.GetType().GetField("skillCooldownLabels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.skillCooldownLabels);
            battleUi.GetType().GetField("startBattleButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.startBattleButton);
            battleUi.GetType().GetField("resultPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.battleResultPanel);
            battleUi.GetType().GetField("resultText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.battleResultText);
            battleUi.GetType().GetField("rewardText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.battleRewardText);
            battleUi.GetType().GetField("playerEffectText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.playerEffectText);
            battleUi.GetType().GetField("enemyEffectText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.enemyEffectText);
            battleUi.GetType().GetField("skillIconImages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.skillIconImages);
            battleUi.GetType().GetField("skillCooldownImages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.skillCooldownImages);
            battleUi.GetType().GetField("skillBorderImages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(battleUi, refs.skillBorderImages);

            PlayerInsectLevelUpUIController levelUpUi = EnsureComponent<PlayerInsectLevelUpUIController>("UI/LevelUpUI");
            levelUpUi.GetType().GetField("insectNameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(levelUpUi, refs.levelUpInsectNameText);
            levelUpUi.GetType().GetField("insectLevelText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(levelUpUi, refs.levelUpInsectLevelText);
            levelUpUi.GetType().GetField("candyCostText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(levelUpUi, refs.levelUpCandyCostText);
            levelUpUi.GetType().GetField("levelUpButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(levelUpUi, refs.levelUpButton);
            levelUpUi.GetType().GetField("resultText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(levelUpUi, refs.levelUpResultText);

            PlayerInsectSelectionUIController selectionUi = EnsureComponent<PlayerInsectSelectionUIController>("UI/LevelUpSelection");
            selectionUi.GetType().GetField("contentRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(selectionUi, refs.levelUpListRoot);
            selectionUi.GetType().GetField("itemButtonPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(selectionUi, refs.levelUpListItemPrefab);
            selectionUi.GetType().GetField("selectedText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(selectionUi, refs.levelUpSelectedText);
            selectionUi.GetType().GetField("rarityDropdown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(selectionUi, refs.levelUpRarityDropdown);
            selectionUi.GetType().GetField("minLevelSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(selectionUi, refs.levelUpMinLevelSlider);
            selectionUi.GetType().GetField("minLevelLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(selectionUi, refs.levelUpMinLevelLabel);

            PlayerItemInventoryGridUIController inventoryUi = EnsureComponent<PlayerItemInventoryGridUIController>("UI/InventoryUI");
            inventoryUi.GetType().GetField("contentRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, refs.inventoryGridRoot);
            inventoryUi.GetType().GetField("itemPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, refs.inventoryGridItemPrefab);
            inventoryUi.GetType().GetField("activeItemText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, refs.activeItemText);
            inventoryUi.GetType().GetField("remainingTimeText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, refs.activeItemTimeText);
            inventoryUi.GetType().GetField("remainingTimeBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, refs.activeItemTimeBar);
            if (refs.inventoryGridItemPrefab != null && refs.itemRarityPalette != null)
            {
                refs.inventoryGridItemPrefab.GetType().GetField("rarityPalette", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(refs.inventoryGridItemPrefab, refs.itemRarityPalette);
            }
            inventoryUi.GetType().GetField("remainingTimeRadial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, refs.activeItemTimeRadial);
            inventoryUi.GetType().GetField("remainingTimeIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(inventoryUi, refs.activeItemTimeIcon);

            ShopUIController shopUi = EnsureComponent<ShopUIController>("UI/ShopUI");
            shopUi.GetType().GetField("buyButtons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(shopUi, refs.shopBuyButtons);
            shopUi.GetType().GetField("buyLabels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(shopUi, refs.shopBuyLabels);
            shopUi.GetType().GetField("resultText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(shopUi, refs.shopResultText);
            shopUi.GetType().GetField("priceLabels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(shopUi, refs.shopPriceLabels);
            shopUi.GetType().GetField("coinsToggle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(shopUi, refs.shopCoinsToggle);
            shopUi.GetType().GetField("gemsToggle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(shopUi, refs.shopGemsToggle);
            shopUi.GetType().GetField("paymentLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(shopUi, refs.shopPaymentLabel);

            PlayerCurrencyUIController currencyUi = EnsureComponent<PlayerCurrencyUIController>("UI/CurrencyUI");
            currencyUi.GetType().GetField("gemsText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(currencyUi, refs.gemsText);
            currencyUi.GetType().GetField("coinsText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(currencyUi, refs.coinsText);

            ItemRarityTuningUIController tuningUi = EnsureComponent<ItemRarityTuningUIController>("UI/RarityTuningUI");
            tuningUi.GetType().GetField("palette", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, refs.itemRarityPalette);
            tuningUi.GetType().GetField("commonSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, refs.commonPulseSlider);
            tuningUi.GetType().GetField("uncommonSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, refs.uncommonPulseSlider);
            tuningUi.GetType().GetField("rareSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, refs.rarePulseSlider);
            tuningUi.GetType().GetField("epicSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, refs.epicPulseSlider);
            tuningUi.GetType().GetField("legendarySlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, refs.legendaryPulseSlider);
            tuningUi.GetType().GetField("commonLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, refs.commonPulseLabel);
            tuningUi.GetType().GetField("uncommonLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, refs.uncommonPulseLabel);
            tuningUi.GetType().GetField("rareLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, refs.rarePulseLabel);
            tuningUi.GetType().GetField("epicLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, refs.epicPulseLabel);
            tuningUi.GetType().GetField("legendaryLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(tuningUi, refs.legendaryPulseLabel);
        }

        private Canvas EnsureCanvas()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            return canvas;
        }

        private static void SetAnchorTopLeft(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
        }

        private static void SetAnchorBottom(RectTransform rect, Vector2 pos)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = pos;
        }

        private Text CreateText(Transform parent, string name, Vector2 anchoredPos, string content, Font font, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Text text = obj.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = 28;
            text.color = Color.white;
            text.alignment = anchor;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(360f, 60f);
            return text;
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.4f);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return panel;
        }

        private Button CreateButton(Transform parent, string name, Vector2 anchoredPos, string label, Font font)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Image image = obj.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            Button button = obj.AddComponent<Button>();
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(200f, 60f);

            Text text = CreateText(obj.transform, "Label", Vector2.zero, label, font, TextAnchor.MiddleCenter);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private Slider CreateSlider(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            Slider slider = obj.AddComponent<Slider>();
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(obj.transform, false);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.8f, 0.3f, 0.9f);

            slider.fillRect = fillImage.rectTransform;
            slider.targetGraphic = fillImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            return slider;
        }

        private Image CreateRadialImage(Transform parent, string name, Vector2 anchoredPos, Vector2 size, bool filled)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Image image = obj.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, filled ? 0.6f : 1f);
            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillAmount = 0f;
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return image;
        }

        private Toggle CreateToggle(Transform parent, string name, Vector2 anchoredPos, Vector2 size, string label)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            Toggle toggle = obj.AddComponent<Toggle>();
            Image bg = obj.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.4f);

            TMP_Text text = CreateTMPText(obj.transform, "Label", Vector2.zero, label);
            text.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return toggle;
        }

        private TMP_Text CreateTMPText(Transform parent, string name, Vector2 anchoredPos, string content)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            TMP_Text text = obj.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = 26;
            text.color = Color.white;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(280f, 50f);
            return text;
        }

        private ItemInventoryGridItem CreateInventoryItemPrefab(RectTransform parent)
        {
            GameObject item = new GameObject("InventoryItemPrefab");
            item.transform.SetParent(parent, false);
            Image bg = item.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(140f, 60f);

            Button button = item.AddComponent<Button>();
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(item.transform, false);
            Image icon = iconObj.AddComponent<Image>();
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(20f, 0f);
            iconRect.sizeDelta = new Vector2(32f, 32f);

            TMP_Text nameText = CreateTMPText(item.transform, "Name", new Vector2(20f, -10f), "아이템");
            nameText.alignment = TextAlignmentOptions.Left;
            TMP_Text countText = CreateTMPText(item.transform, "Count", new Vector2(60f, -10f), "x0");
            countText.alignment = TextAlignmentOptions.Right;

            ItemInventoryGridItem ui = item.AddComponent<ItemInventoryGridItem>();
            ui.GetType().GetField("iconImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(ui, icon);
            ui.GetType().GetField("nameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(ui, nameText);
            ui.GetType().GetField("countText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(ui, countText);
            ui.GetType().GetField("button", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(ui, button);

            return ui;
        }

        private DexListItemUI CreateListItemPrefab(RectTransform parent, Font font)
        {
            GameObject item = new GameObject("DexListItemPrefab");
            item.transform.SetParent(parent, false);
            Image bg = item.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(360f, 60f);

            Button button = item.AddComponent<Button>();
            Text nameText = CreateText(item.transform, "Name", new Vector2(-60f, -10f), "???", font, TextAnchor.MiddleLeft);
            Text statusText = CreateText(item.transform, "Status", new Vector2(120f, -10f), "미발견", font, TextAnchor.MiddleRight);

            DexListItemUI ui = item.AddComponent<DexListItemUI>();
            ui.GetType().GetField("nameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(ui, nameText);
            ui.GetType().GetField("statusText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(ui, statusText);
            ui.GetType().GetField("selectButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(ui, button);

            return ui;
        }

        private T EnsureComponent<T>(string path) where T : Component
        {
            GameObject obj = EnsureObject(path);
            T component = obj.GetComponent<T>();
            if (component == null)
            {
                component = obj.AddComponent<T>();
            }
            return component;
        }

        private GameObject EnsureObject(string path)
        {
            string[] parts = path.Split('/');
            GameObject current = null;
            string currentPath = string.Empty;

            foreach (string part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                GameObject found = GameObject.Find(currentPath);
                if (found == null)
                {
                    GameObject obj = new GameObject(part);
                    if (current != null)
                    {
                        obj.transform.SetParent(current.transform, false);
                    }
                    current = obj;
                }
                else
                {
                    current = found;
                }
            }

            return current;
        }

        private void CreateGuardians(Data.RegionData[] regions, InsectDatabase database)
        {
            foreach (var r in regions)
            {
                if (string.IsNullOrEmpty(r.guardianInsectId)) continue;

                Vector3 guardianPos = GetGuardianWorldPosition(r, regions);

                // 수문장 플랫폼
                Material platMat = CreateSafeMaterial(new Color(0.3f, 0.15f, 0.1f));
                GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                platform.name = $"Guardian_{r.regionId}_Platform";
                platform.transform.position = guardianPos + new Vector3(0, 0.15f, 0);
                platform.transform.localScale = new Vector3(4f, 0.15f, 4f);
                platform.GetComponent<MeshRenderer>().material = platMat;
                Object.Destroy(platform.GetComponent<Collider>());

                // 수문장 기둥 (양쪽)
                Material pillarMat = CreateSafeMaterial(new Color(0.4f, 0.3f, 0.25f));
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pillar.name = $"Guardian_{r.regionId}_Pillar_{(side > 0 ? "R" : "L")}";
                    pillar.transform.position = guardianPos + new Vector3(side * 3f, 2f, 0);
                    pillar.transform.localScale = new Vector3(0.5f, 2f, 0.5f);
                    pillar.GetComponent<MeshRenderer>().material = pillarMat;
                    Object.Destroy(pillar.GetComponent<Collider>());
                }

                // 오라 (빨간 반투명)
                Material auraMat = CreateSafeMaterial(new Color(0.9f, 0.2f, 0.1f, 0.15f));
                GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                aura.name = $"Guardian_{r.regionId}_Aura";
                aura.transform.position = guardianPos + new Vector3(0, 2f, 0);
                aura.transform.localScale = new Vector3(5f, 4f, 5f);
                aura.GetComponent<MeshRenderer>().material = auraMat;
                Object.Destroy(aura.GetComponent<Collider>());

                // 이름 표지판
                Material signMat = CreateSafeMaterial(new Color(0.15f, 0.08f, 0.05f));
                GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sign.name = $"Guardian_{r.regionId}_Sign";
                sign.transform.position = guardianPos + new Vector3(0, 4.5f, 0);
                sign.transform.localScale = new Vector3(4f, 1f, 0.15f);
                sign.GetComponent<MeshRenderer>().material = signMat;
                Object.Destroy(sign.GetComponent<Collider>());

                // 수문장 곤충 엔티티 스폰 (실제 3D 곤충)
                SpawnGuardianInsect(r, guardianPos, database);
            }
        }

        private void SpawnGuardianInsect(Data.RegionData region, Vector3 guardianPos, InsectDatabase database)
        {
            if (database == null || string.IsNullOrEmpty(region.guardianInsectId)) return;

            // 이미 격파된 수문장은 스폰하지 않음
            RegionManager regionMgr = FindFirstObjectByType<RegionManager>();
            if (regionMgr != null && regionMgr.IsGuardianDefeated(region.regionId)) return;

            // 수문장 InsectData 찾기
            InsectData guardianData = null;
            foreach (var insect in database.insects)
            {
                if (insect != null && insect.insectId == region.guardianInsectId)
                {
                    guardianData = insect;
                    break;
                }
            }
            if (guardianData == null) return;

            // 수문장 곤충 오브젝트 생성
            GameObject guardianObj = new GameObject($"Guardian_{region.regionId}_Insect");
            guardianObj.transform.position = guardianPos + new Vector3(0f, 1.5f, 0f);

            Spawning.InsectEntity entity = guardianObj.AddComponent<Spawning.InsectEntity>();
            entity.BuildForBattle(guardianData, region.guardianLevel, false);

            // 수문장 크기 크게 (1.8배)
            guardianObj.transform.localScale = Vector3.one * 1.8f;

            // 이름 라벨 추가 (수문장 표시)
            GameObject label = new GameObject("GuardianLabel");
            label.transform.SetParent(guardianObj.transform, false);
            label.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = $"⚔ {region.guardianDisplayName} Lv.{region.guardianLevel}";
            text.characterSize = 0.15f;
            text.fontSize = 48;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1f, 0.3f, 0.2f);
        }

        private Vector3 GetGuardianWorldPosition(Data.RegionData region, Data.RegionData[] allRegions)
        {
            Vector3 fromCenter = Vector3.zero;
            string prevId = null;
            switch (region.regionId)
            {
                case "pond": prevId = "meadow"; break;
                case "forest": prevId = "pond"; break;
                case "swamp": prevId = "forest"; break;
                case "mountain": prevId = "swamp"; break;
                case "garden": prevId = "meadow"; break;
            }
            if (prevId != null)
            {
                foreach (var r in allRegions)
                    if (r.regionId == prevId) { fromCenter = r.centerPosition; break; }
            }
            return (fromCenter + region.centerPosition) / 2f;
        }

        private void CreateSubAreaEntries(Data.RegionData[] regions)
        {
            foreach (var region in regions)
            {
                if (region.subAreas == null) continue;
                foreach (var sub in region.subAreas)
                {
                    Vector3 c = sub.centerPosition;
                    switch (sub.environmentType)
                    {
                        case "cave":
                            CreateCaveEntry(c, sub.subAreaId);
                            break;
                        case "deep_forest":
                            CreateDeepForestGate(c, sub.subAreaId);
                            break;
                        case "underwater":
                            CreateUnderwaterPool(c, sub.subAreaId);
                            break;
                        case "reeds":
                            CreateReedsArch(c, sub.subAreaId);
                            break;
                        case "flower_maze":
                            CreateFlowerMazeEntry(c, sub.subAreaId);
                            break;
                        case "greenhouse":
                            CreateGreenhouseFrame(c, sub.subAreaId);
                            break;
                        case "pond":
                            CreateUnderwaterPool(c, sub.subAreaId);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void CreateCaveEntry(Vector3 pos, string id)
        {
            Material stoneMat = CreateSafeMaterial(new Color(0.45f, 0.42f, 0.38f));
            Material darkMat = CreateSafeMaterial(new Color(0.1f, 0.08f, 0.06f));

            // 동굴 아치 (좌측 기둥)
            GameObject pillarL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillarL.name = $"SubArea_{id}_PillarL";
            pillarL.transform.position = pos + new Vector3(-1.5f, 1.5f, 0f);
            pillarL.transform.localScale = new Vector3(0.8f, 1.5f, 0.8f);
            pillarL.GetComponent<MeshRenderer>().material = stoneMat;

            // 동굴 아치 (우측 기둥)
            GameObject pillarR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillarR.name = $"SubArea_{id}_PillarR";
            pillarR.transform.position = pos + new Vector3(1.5f, 1.5f, 0f);
            pillarR.transform.localScale = new Vector3(0.8f, 1.5f, 0.8f);
            pillarR.GetComponent<MeshRenderer>().material = stoneMat;

            // 아치 상단
            GameObject arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arch.name = $"SubArea_{id}_Arch";
            arch.transform.position = pos + new Vector3(0f, 3.2f, 0f);
            arch.transform.localScale = new Vector3(4f, 0.6f, 1f);
            arch.GetComponent<MeshRenderer>().material = stoneMat;

            // 동굴 입구 어둠
            GameObject darkness = GameObject.CreatePrimitive(PrimitiveType.Cube);
            darkness.name = $"SubArea_{id}_Dark";
            darkness.transform.position = pos + new Vector3(0f, 1.5f, 0.3f);
            darkness.transform.localScale = new Vector3(2.5f, 2.8f, 0.3f);
            darkness.GetComponent<MeshRenderer>().material = darkMat;
            Object.Destroy(darkness.GetComponent<Collider>());
        }

        private void CreateDeepForestGate(Vector3 pos, string id)
        {
            Material trunkMat = CreateSafeMaterial(new Color(0.25f, 0.15f, 0.08f));
            Material leafMat = CreateSafeMaterial(new Color(0.08f, 0.3f, 0.05f));

            // 양쪽 큰 나무 기둥
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = $"SubArea_{id}_Trunk_{(side > 0 ? "R" : "L")}";
                trunk.transform.position = pos + new Vector3(side * 2f, 2.5f, 0f);
                trunk.transform.localScale = new Vector3(0.7f, 2.5f, 0.7f);
                trunk.GetComponent<MeshRenderer>().material = trunkMat;

                GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                canopy.name = $"SubArea_{id}_Canopy_{(side > 0 ? "R" : "L")}";
                canopy.transform.position = pos + new Vector3(side * 2f, 5.5f, 0f);
                canopy.transform.localScale = new Vector3(3f, 2f, 3f);
                canopy.GetComponent<MeshRenderer>().material = leafMat;
                Object.Destroy(canopy.GetComponent<Collider>());
            }

            // 나뭇잎 아치 (상단 연결)
            GameObject leafArch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leafArch.name = $"SubArea_{id}_LeafArch";
            leafArch.transform.position = pos + new Vector3(0f, 5f, 0f);
            leafArch.transform.localScale = new Vector3(5f, 1.5f, 2f);
            leafArch.GetComponent<MeshRenderer>().material = leafMat;
            Object.Destroy(leafArch.GetComponent<Collider>());
        }

        private void CreateUnderwaterPool(Vector3 pos, string id)
        {
            Material waterMat = CreateSafeMaterial(new Color(0.15f, 0.35f, 0.65f, 0.5f));
            Material edgeMat = CreateSafeMaterial(new Color(0.5f, 0.48f, 0.42f));

            // 물 표면
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = $"SubArea_{id}_Water";
            water.transform.position = pos + new Vector3(0f, 0.05f, 0f);
            water.transform.localScale = new Vector3(4f, 0.05f, 4f);
            water.GetComponent<MeshRenderer>().material = waterMat;
            Object.Destroy(water.GetComponent<Collider>());

            // 돌 테두리
            int stoneCount = 8;
            for (int i = 0; i < stoneCount; i++)
            {
                float angle = Mathf.PI * 2f * i / stoneCount;
                Vector3 stonePos = pos + new Vector3(Mathf.Cos(angle) * 2.2f, 0.2f, Mathf.Sin(angle) * 2.2f);
                GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stone.name = $"SubArea_{id}_Stone_{i}";
                stone.transform.position = stonePos;
                stone.transform.localScale = new Vector3(0.5f, 0.3f, 0.5f);
                stone.GetComponent<MeshRenderer>().material = edgeMat;
            }
        }

        private void CreateReedsArch(Vector3 pos, string id)
        {
            Material reedMat = CreateSafeMaterial(new Color(0.4f, 0.55f, 0.2f));
            Material reedTopMat = CreateSafeMaterial(new Color(0.55f, 0.45f, 0.25f));

            // 갈대 아치 양쪽
            for (int side = -1; side <= 1; side += 2)
            {
                for (int j = 0; j < 3; j++)
                {
                    float offset = j * 0.4f;
                    GameObject reed = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    reed.name = $"SubArea_{id}_Reed_{(side > 0 ? "R" : "L")}_{j}";
                    reed.transform.position = pos + new Vector3(side * (1.5f + offset), 2f, offset * 0.3f);
                    reed.transform.localScale = new Vector3(0.12f, 2f, 0.12f);
                    reed.transform.localRotation = Quaternion.Euler(0f, 0f, side * -15f);
                    reed.GetComponent<MeshRenderer>().material = reedMat;
                    Object.Destroy(reed.GetComponent<Collider>());

                    // 갈대 꼭대기
                    GameObject top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    top.name = $"SubArea_{id}_ReedTop_{(side > 0 ? "R" : "L")}_{j}";
                    top.transform.position = pos + new Vector3(side * (1.2f + offset), 4.1f, offset * 0.3f);
                    top.transform.localScale = new Vector3(0.2f, 0.4f, 0.2f);
                    top.GetComponent<MeshRenderer>().material = reedTopMat;
                    Object.Destroy(top.GetComponent<Collider>());
                }
            }
        }

        private void CreateFlowerMazeEntry(Vector3 pos, string id)
        {
            Material stemMat = CreateSafeMaterial(new Color(0.2f, 0.55f, 0.15f));
            Material[] flowerMats = {
                CreateSafeMaterial(new Color(1f, 0.3f, 0.4f)),
                CreateSafeMaterial(new Color(0.9f, 0.6f, 1f)),
                CreateSafeMaterial(new Color(1f, 0.85f, 0.2f))
            };

            // 꽃 아치
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stem.name = $"SubArea_{id}_Stem_{(side > 0 ? "R" : "L")}";
                stem.transform.position = pos + new Vector3(side * 2f, 2f, 0f);
                stem.transform.localScale = new Vector3(0.3f, 2f, 0.3f);
                stem.GetComponent<MeshRenderer>().material = stemMat;

                // 꽃들
                for (int f = 0; f < 3; f++)
                {
                    GameObject flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    flower.name = $"SubArea_{id}_Flower_{(side > 0 ? "R" : "L")}_{f}";
                    float yOff = 1f + f * 1.2f;
                    flower.transform.position = pos + new Vector3(side * 2f, yOff, 0.3f);
                    flower.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                    flower.GetComponent<MeshRenderer>().material = flowerMats[f % flowerMats.Length];
                    Object.Destroy(flower.GetComponent<Collider>());
                }
            }

            // 상단 아치 꽃
            GameObject archFlower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            archFlower.name = $"SubArea_{id}_ArchFlower";
            archFlower.transform.position = pos + new Vector3(0f, 4.5f, 0f);
            archFlower.transform.localScale = new Vector3(4.5f, 1f, 1f);
            archFlower.GetComponent<MeshRenderer>().material = stemMat;
            Object.Destroy(archFlower.GetComponent<Collider>());
        }

        private void CreateGreenhouseFrame(Vector3 pos, string id)
        {
            Material frameMat = CreateSafeMaterial(new Color(0.7f, 0.7f, 0.7f));
            Material glassMat = CreateSafeMaterial(new Color(0.8f, 0.9f, 1f, 0.2f));

            // 프레임 기둥 4개
            Vector3[] corners = {
                new Vector3(-2f, 0f, -2f), new Vector3(2f, 0f, -2f),
                new Vector3(-2f, 0f, 2f), new Vector3(2f, 0f, 2f)
            };
            for (int i = 0; i < corners.Length; i++)
            {
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"SubArea_{id}_Frame_{i}";
                pillar.transform.position = pos + corners[i] + new Vector3(0f, 2f, 0f);
                pillar.transform.localScale = new Vector3(0.15f, 2f, 0.15f);
                pillar.GetComponent<MeshRenderer>().material = frameMat;
            }

            // 유리 벽 (반투명)
            GameObject glassWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glassWall.name = $"SubArea_{id}_Glass";
            glassWall.transform.position = pos + new Vector3(0f, 2f, 0f);
            glassWall.transform.localScale = new Vector3(4.2f, 4f, 4.2f);
            glassWall.GetComponent<MeshRenderer>().material = glassMat;
            Object.Destroy(glassWall.GetComponent<Collider>());

            // 지붕 (삼각형 근사 — 납작한 큐브)
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = $"SubArea_{id}_Roof";
            roof.transform.position = pos + new Vector3(0f, 4.3f, 0f);
            roof.transform.localScale = new Vector3(4.5f, 0.3f, 4.5f);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            roof.GetComponent<MeshRenderer>().material = glassMat;
            Object.Destroy(roof.GetComponent<Collider>());
        }
    }
}
