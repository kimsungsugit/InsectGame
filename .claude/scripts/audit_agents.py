"""에이전트 커버리지 감사 스크립트"""
import subprocess, os

agents = {
    "battle-dev": [
        "Assets/Scripts/Battle/InsectBattleController.cs",
        "Assets/Scripts/Battle/InsectBattleStats.cs",
        "Assets/Scripts/Battle/InsectBattleUIController.cs",
        "Assets/Scripts/Battle/RaidBattleController.cs",
        "Assets/Scripts/UI/BattleScreenUI.cs",
        "Assets/Scripts/UI/RaidBattleUI.cs",
        "Assets/Scripts/UI/BattleTeamUI.cs",
        "Assets/Scripts/Core/BattleTeamManager.cs",
        "Assets/Scripts/Core/PlayerInsectCombatPower.cs",
        "Assets/Scripts/Data/InsectSkill.cs",
        "Assets/Scripts/Data/InsectLearnableSkill.cs",
        "Assets/Scripts/Data/InsectElement.cs",
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
        "Assets/Scripts/UI/CaptureChoiceUI.cs",
    ],
    "ui-dev": [
        "Assets/Scripts/UI/MainMenuManager.cs",
        "Assets/Scripts/UI/BattleScreenUI.cs",
        "Assets/Scripts/UI/RaidBattleUI.cs",
        "Assets/Scripts/UI/CaptureChoiceUI.cs",
        "Assets/Scripts/UI/PlayUIConfig.cs",
        "Assets/Scripts/UI/PlayUIRefs.cs",
        "Assets/Scripts/UI/PlayerStatusHUD.cs",
        "Assets/Scripts/UI/KeyGuideHUD.cs",
        "Assets/Scripts/UI/TrainingUI.cs",
        "Assets/Scripts/UI/CollectionUI.cs",
        "Assets/Scripts/UI/BattleTeamUI.cs",
        "Assets/Scripts/UI/RegionMapUI.cs",
        "Assets/Scripts/UI/SettingsPanel.cs",
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
        "Assets/Scripts/Core/PlayerProgressSaveService.cs",
        "Assets/Scripts/Core/CloudSaveManager.cs",
        "Assets/Scripts/Core/PlayerProgressData.cs",
        "Assets/Scripts/Core/PlayerCandyInventory.cs",
        "Assets/Scripts/Core/PlayerCurrencyWallet.cs",
        "Assets/Scripts/Core/PlayerItemInventory.cs",
        "Assets/Scripts/Core/GameConstants.cs",
        "Assets/Scripts/Dex/DexSaveData.cs",
        "Assets/Scripts/Dex/DexSaveService.cs",
        "Assets/Scripts/Dex/DexRecord.cs",
    ],
    "visual-dev": [
        "Assets/Scripts/Spawning/InsectEntity.cs",
        "Assets/Scripts/Battle/BattleArenaController.cs",
        "Assets/Scripts/UI/BattleScreenUI.cs",
        "Assets/Scripts/UI/RaidBattleUI.cs",
        "Assets/Scripts/Data/ItemRarityPalette.cs",
        "Assets/Scripts/Core/ProceduralAudioGenerator.cs",
        "Assets/Scripts/Dex/RarityIconProvider.cs",
    ],
}

def norm(p):
    return p.replace(chr(92), "/")

result = subprocess.run(["find", "Assets/Scripts", "-name", "*.cs"], capture_output=True, text=True)
actual_files = set(norm(l.strip()) for l in result.stdout.strip().split("\n") if l.strip())

covered = set()
for name, files in agents.items():
    covered.update(norm(f) for f in files)

print("=== 1. 에이전트에 명시됐지만 실제 없는 파일 ===")
missing = covered - actual_files
if missing:
    for f in sorted(missing):
        print(f"  MISSING: {f}")
else:
    print("  없음 (모두 존재)")

print()
print("=== 2. 어떤 에이전트도 담당하지 않는 파일 ===")
uncovered = actual_files - covered
for f in sorted(uncovered):
    print(f"  {f}")
print(f"  총 {len(uncovered)}개 / {len(actual_files)}개")

print()
print("=== 3. 여러 에이전트가 동시 담당하는 파일 ===")
file_owners = {}
for name, files in agents.items():
    for f in files:
        nf = norm(f)
        file_owners.setdefault(nf, []).append(name)
for f, owners in sorted(file_owners.items()):
    if len(owners) > 1:
        fname = f.split("/")[-1]
        print(f"  {fname:40s} <- {', '.join(owners)}")
