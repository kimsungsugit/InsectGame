using InsectGame.Capture;
using InsectGame.Data;
using InsectGame.Dex;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Core
{
    public class SceneAutoWire : MonoBehaviour
    {
        private void Awake()
        {
            InsectDatabase database = FindFirstObjectByType<InsectDatabase>();
            WorldStateProvider worldState = FindFirstObjectByType<WorldStateProvider>();
            GameClock clock = FindFirstObjectByType<GameClock>();
            WeatherSystem weather = FindFirstObjectByType<WeatherSystem>();

            worldState?.AutoWire(clock, weather);

            InsectSpawner spawner = FindFirstObjectByType<InsectSpawner>();
            if (spawner != null)
            {
                SpawnPoint[] points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
                spawner.AutoWire(database, worldState, points);
            }

            GameplayTuningApplier tuning = FindFirstObjectByType<GameplayTuningApplier>();

            InsectLoreBootstrapper lore = FindFirstObjectByType<InsectLoreBootstrapper>();
            lore?.AutoWire(database);

            DexController dexController = FindFirstObjectByType<DexController>();
            PlayerProgressController progress = FindFirstObjectByType<PlayerProgressController>();
            PlayerCandyInventory candy = FindFirstObjectByType<PlayerCandyInventory>();
            PlayerItemInventory itemInventory = FindFirstObjectByType<PlayerItemInventory>();
            ItemDatabase itemDatabase = FindFirstObjectByType<ItemDatabase>();
            PlayerCurrencyWallet wallet = FindFirstObjectByType<PlayerCurrencyWallet>();
            PlayerInsectCollection insectCollection = FindFirstObjectByType<PlayerInsectCollection>();
            PlayerProgressUIController progressUi = FindFirstObjectByType<PlayerProgressUIController>();
            PlayerCurrencyUIController currencyUi = FindFirstObjectByType<PlayerCurrencyUIController>();
            Battle.InsectBattleController battleController = FindFirstObjectByType<Battle.InsectBattleController>();
            Battle.InsectBattleUIController battleUi = FindFirstObjectByType<Battle.InsectBattleUIController>();
            PlayerInsectLevelUpUIController levelUpUi = FindFirstObjectByType<PlayerInsectLevelUpUIController>();
            PlayerInsectSelectionUIController selectionUi = FindFirstObjectByType<PlayerInsectSelectionUIController>();
            PlayerItemInventoryGridUIController inventoryUi = FindFirstObjectByType<PlayerItemInventoryGridUIController>();
            ItemEffectManager itemEffects = FindFirstObjectByType<ItemEffectManager>();
            ShopUIController shopUi = FindFirstObjectByType<ShopUIController>();
            ItemRarityTuningUIController tuningUi = FindFirstObjectByType<ItemRarityTuningUIController>();
            DexDetailUIController dexDetail = FindFirstObjectByType<DexDetailUIController>();
            DexListUIController dexList = FindFirstObjectByType<DexListUIController>();
            DexUIController dexSummary = FindFirstObjectByType<DexUIController>();

            dexDetail?.AutoWire(database, dexController);
            dexList?.AutoWire(database, dexController, dexDetail, dexList.transform, null);
            dexSummary?.AutoWire(dexController);

            CaptureController captureController = FindFirstObjectByType<CaptureController>();
            CaptureMinigameController minigame = FindFirstObjectByType<CaptureMinigameController>();
            CaptureRaycastTrigger raycastTrigger = FindFirstObjectByType<CaptureRaycastTrigger>();
            CaptureProximityTrigger proximityTrigger = FindFirstObjectByType<CaptureProximityTrigger>();
            CaptureTriggerModeController modeController = FindFirstObjectByType<CaptureTriggerModeController>();
            CaptureTriggerOptionsUI optionsUI = FindFirstObjectByType<CaptureTriggerOptionsUI>();
            CaptureInputController inputController = FindFirstObjectByType<CaptureInputController>();
            CaptureFeedbackController feedbackController = FindFirstObjectByType<CaptureFeedbackController>();

            captureController?.AutoWire(dexController);
            captureController?.AutoWire(progress);
            captureController?.AutoWire(insectCollection);
            captureController?.AutoWire(candy);
            captureController?.AutoWire(itemEffects);
            minigame?.AutoWire(captureController);
            raycastTrigger?.AutoWire(Camera.main, minigame);
            proximityTrigger?.AutoWire(minigame);
            modeController?.AutoWire(raycastTrigger, proximityTrigger);
            optionsUI?.AutoWire(modeController);
            inputController?.AutoWire(modeController, raycastTrigger, proximityTrigger);
            feedbackController?.AutoWire(captureController);
            tuning?.AutoWire(spawner, captureController);
            progressUi?.AutoWire(progress);
            progressUi?.AutoWire(candy);
            currencyUi?.AutoWire(wallet);

            insectCollection?.AutoWire(database, candy);
            battleController?.AutoWire(insectCollection, candy, progress, itemInventory);
            battleUi?.AutoWire(battleController, insectCollection);
            levelUpUi?.AutoWire(insectCollection);
            selectionUi?.AutoWire(insectCollection, levelUpUi);
            inventoryUi?.AutoWire(itemInventory, itemDatabase, itemEffects);
            itemEffects?.AutoWire(itemDatabase);

            spawner?.AutoWire(itemEffects);
            shopUi?.AutoWire(itemInventory, itemDatabase, wallet);
            tuningUi?.AutoWire(Resources.Load<ItemRarityPalette>("ItemRarityPalette"));

            TrainingManager training = FindFirstObjectByType<TrainingManager>();
            BattleTeamManager teamManager = FindFirstObjectByType<BattleTeamManager>();
            RegionManager regionMgr = FindFirstObjectByType<RegionManager>();
            Battle.RaidBattleController raidController = FindFirstObjectByType<Battle.RaidBattleController>();

            TutorialQuestManager questManager = FindFirstObjectByType<TutorialQuestManager>();
            questManager?.AutoWire(insectCollection, candy, progress, itemInventory,
                battleController, raidController, dexController, training, teamManager, regionMgr);
        }
    }
}
