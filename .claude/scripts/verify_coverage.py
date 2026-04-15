"""에이전트 커버리지 재검증"""
import glob

agents = {
    "architect": [
        "Assets/Scripts/Core/PlaySceneBootstrap.cs",
        "Assets/Scripts/Core/SceneAutoWire.cs",
        "Assets/Scripts/Core/PlaySceneAutoInit.cs",
        "Assets/Scripts/Core/AuthManager.cs",
        "Assets/Scripts/Core/FirebaseConfig.cs",
        "Assets/Scripts/Core/WorldChannelManager.cs",
    ],
    "battle-dev": [
        "Assets/Scripts/Battle/InsectBattleController.cs",
        "Assets/Scripts/Battle/InsectBattleStats.cs",
        "Assets/Scripts/Battle/InsectBattleUIController.cs",
        "Assets/Scripts/Battle/RaidBattleController.cs",
        "Assets/Scripts/Battle/BattleArenaController.cs",
        "Assets/Scripts/Core/BattleTeamManager.cs",
        "Assets/Scripts/Core/PlayerInsectCombatPower.cs",
        "Assets/Scripts/Data/InsectSkill.cs",
        "Assets/Scripts/Data/InsectLearnableSkill.cs",
        "Assets/Scripts/Data/InsectElement.cs",
        "Assets/Scripts/UI/BattleScreenUI.cs",
        "Assets/Scripts/UI/RaidBattleUI.cs",
        "Assets/Scripts/UI/BattleTeamUI.cs",
    ],
    "capture-dev": [
        "Assets/Scripts/Capture/CaptureController.cs",
        "Assets/Scripts/Capture/CaptureMinigameController.cs",
        "Assets/Scripts/Capture/CaptureInputController.cs",
        "Assets/Scripts/Capture/CaptureProximityTrigger.cs",
        "Assets/Scripts/Capture/CaptureRaycastTrigger.cs",
        "Assets/Scripts/Capture/CaptureTriggerModeController.cs",
        "Assets/Scripts/Capture/CaptureFeedbackController.cs",
        "Assets/Scripts/Capture/CaptureTriggerOptionsUI.cs",
        "Assets/Scripts/Spawning/InsectSpawner.cs",
        "Assets/Scripts/Spawning/SpawnPoint.cs",
        "Assets/Scripts/Spawning/InsectEntity.cs",
        "Assets/Scripts/Spawning/SimpleObjectPool.cs",
        "Assets/Scripts/Spawning/DistanceCulling.cs",
        "Assets/Scripts/Spawning/CaptureItemSpawner.cs",
        "Assets/Scripts/Spawning/CaptureItemPickup.cs",
        "Assets/Scripts/Data/InsectSpawnCondition.cs",
        "Assets/Scripts/Data/CaptureItemData.cs",
        "Assets/Scripts/Core/WeatherSystem.cs",
        "Assets/Scripts/Core/GameClock.cs",
        "Assets/Scripts/Core/WorldStateProvider.cs",
        "Assets/Scripts/Core/PlayerMovement.cs",
        "Assets/Scripts/UI/CaptureChoiceUI.cs",
    ],
    "data-architect": [
        "Assets/Scripts/Data/InsectData.cs",
        "Assets/Scripts/Data/InsectDatabase.cs",
        "Assets/Scripts/Data/InsectSkill.cs",
        "Assets/Scripts/Data/InsectLearnableSkill.cs",
        "Assets/Scripts/Data/InsectElement.cs",
        "Assets/Scripts/Data/InsectRarity.cs",
        "Assets/Scripts/Data/InsectSpawnCondition.cs",
        "Assets/Scripts/Data/InsectLevelCurve.cs",
        "Assets/Scripts/Data/InsectRewardCalculator.cs",
        "Assets/Scripts/Data/InsectLoreEntry.cs",
        "Assets/Scripts/Data/InsectLoreService.cs",
        "Assets/Scripts/Data/InsectLoreBootstrapper.cs",
        "Assets/Scripts/Data/ItemData.cs",
        "Assets/Scripts/Data/ItemDatabase.cs",
        "Assets/Scripts/Data/CaptureItemData.cs",
        "Assets/Scripts/Data/ItemRarityPalette.cs",
        "Assets/Scripts/Data/RegionData.cs",
        "Assets/Scripts/Data/SubAreaData.cs",
        "Assets/Scripts/Data/TrainingMethod.cs",
        "Assets/Scripts/Data/OutfitSetData.cs",
        "Assets/Scripts/Core/PlayerProgressSaveService.cs",
        "Assets/Scripts/Core/CloudSaveManager.cs",
        "Assets/Scripts/Core/PlayerProgressData.cs",
        "Assets/Scripts/Core/PlayerProgressController.cs",
        "Assets/Scripts/Core/PlayerCandyInventory.cs",
        "Assets/Scripts/Core/PlayerCurrencyWallet.cs",
        "Assets/Scripts/Core/PlayerItemInventory.cs",
        "Assets/Scripts/Core/PlayerInsectCollection.cs",
        "Assets/Scripts/Core/PlayerInsectData.cs",
        "Assets/Scripts/Core/GameConstants.cs",
        "Assets/Scripts/Core/CharacterOutfitData.cs",
        "Assets/Scripts/Dex/DexController.cs",
        "Assets/Scripts/Dex/DexRecord.cs",
        "Assets/Scripts/Dex/DexSaveData.cs",
        "Assets/Scripts/Dex/DexSaveService.cs",
        "Assets/Scripts/Dex/DexScreenUI.cs",
        "Assets/Scripts/Dex/DexUIController.cs",
        "Assets/Scripts/Dex/DexDetailUIController.cs",
        "Assets/Scripts/Dex/DexListUIController.cs",
        "Assets/Scripts/Dex/DexListUIPresetController.cs",
        "Assets/Scripts/Dex/DexListItemUI.cs",
        "Assets/Scripts/Dex/RarityIconProvider.cs",
    ],
    "game-designer": [
        "Assets/Scripts/Core/TrainingManager.cs",
        "Assets/Scripts/Core/TutorialQuestManager.cs",
        "Assets/Scripts/Core/TutorialQuestData.cs",
        "Assets/Scripts/Core/GachaBoxManager.cs",
        "Assets/Scripts/Core/CashShopManager.cs",
        "Assets/Scripts/Core/ItemEffectManager.cs",
        "Assets/Scripts/Core/RegionManager.cs",
    ],
    "ui-dev": [
        "Assets/Scripts/UI/MainMenuManager.cs",
        "Assets/Scripts/UI/BattleScreenUI.cs",
        "Assets/Scripts/UI/RaidBattleUI.cs",
        "Assets/Scripts/UI/CaptureChoiceUI.cs",
        "Assets/Scripts/UI/CapturePopupUI.cs",
        "Assets/Scripts/UI/PlayUIConfig.cs",
        "Assets/Scripts/UI/PlayUIRefs.cs",
        "Assets/Scripts/UI/PlayerStatusHUD.cs",
        "Assets/Scripts/UI/KeyGuideHUD.cs",
        "Assets/Scripts/UI/TrainingUI.cs",
        "Assets/Scripts/UI/CollectionUI.cs",
        "Assets/Scripts/UI/BattleTeamUI.cs",
        "Assets/Scripts/UI/RegionMapUI.cs",
        "Assets/Scripts/UI/SettingsPanel.cs",
        "Assets/Scripts/UI/LoginUI.cs",
        "Assets/Scripts/UI/CashShopUI.cs",
        "Assets/Scripts/UI/CharacterOutfitUI.cs",
        "Assets/Scripts/UI/CharacterViewerUI.cs",
        "Assets/Scripts/UI/QuickAccessBarUI.cs",
        "Assets/Scripts/UI/TutorialQuestUI.cs",
        "Assets/Scripts/UI/WorldLobbyUI.cs",
        "Assets/Scripts/UI/UIHelper.cs",
        "Assets/Scripts/UI/UITheme.cs",
        "Assets/Scripts/UI/UITween.cs",
        "Assets/Scripts/Core/PlayerCurrencyUIController.cs",
        "Assets/Scripts/Core/PlayerProgressUIController.cs",
        "Assets/Scripts/Core/PlayerInsectLevelUpUIController.cs",
        "Assets/Scripts/Core/PlayerInsectSelectionUIController.cs",
        "Assets/Scripts/Core/PlayerItemInventoryGridUIController.cs",
        "Assets/Scripts/Core/ItemRarityTuningUIController.cs",
        "Assets/Scripts/Core/ItemInventoryGridItem.cs",
        "Assets/Scripts/Core/ShopUIController.cs",
    ],
    "visual-dev": [
        "Assets/Scripts/Spawning/InsectEntity.cs",
        "Assets/Scripts/Battle/BattleArenaController.cs",
        "Assets/Scripts/UI/BattleScreenUI.cs",
        "Assets/Scripts/UI/RaidBattleUI.cs",
        "Assets/Scripts/Data/ItemRarityPalette.cs",
        "Assets/Scripts/Core/ProceduralAudioGenerator.cs",
        "Assets/Scripts/Core/AudioManager.cs",
        "Assets/Scripts/Core/CharacterOutfitManager.cs",
        "Assets/Scripts/Core/OutfitBonusProvider.cs",
        "Assets/Scripts/Core/CameraFollower.cs",
        "Assets/Scripts/Core/GameplayTuningApplier.cs",
        "Assets/Scripts/Core/GameplayTuningProfile.cs",
        "Assets/Scripts/Dex/RarityIconProvider.cs",
    ],
}

def n(p):
    return p.replace(chr(92), "/")

actual = set(n(f) for f in glob.glob("Assets/Scripts/**/*.cs", recursive=True))
covered = set()
for files in agents.values():
    covered.update(files)

print("=== 커버리지 요약 ===")
print(f"전체 .cs 파일: {len(actual)}개")
print(f"에이전트 커버: {len(actual & covered)}개")
uncovered = actual - covered
print(f"미할당: {len(uncovered)}개")
if uncovered:
    print()
    print("=== 여전히 미할당인 파일 ===")
    for f in sorted(uncovered):
        print(f"  {f}")

ghost = covered - actual
if ghost:
    print()
    print("=== 실제 없는 파일 (고스트) ===")
    for f in sorted(ghost):
        print(f"  {f}")

print()
print("=== 에이전트별 파일 수 ===")
for name, files in sorted(agents.items()):
    real = len(set(files) & actual)
    print(f"  {name:20s}: {real:3d}개")

print()
print("=== 의도적 공유 파일 ===")
file_owners = {}
for name, files in agents.items():
    for f in files:
        file_owners.setdefault(f, []).append(name)
for f, owners in sorted(file_owners.items()):
    if len(owners) > 1:
        fname = f.split("/")[-1]
        print(f"  {fname:40s} <- {', '.join(owners)}")
