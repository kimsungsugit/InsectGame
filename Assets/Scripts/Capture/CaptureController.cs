using System;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;
using InsectGame.Core;

namespace InsectGame.Capture
{
    public class CaptureController : MonoBehaviour
    {
        [Range(0f, 1f)] [SerializeField] private float baseSuccessChance = 0.6f;
        [Range(0f, 0.5f)] [SerializeField] private float rarityPenaltyStep = 0.08f;
        [Range(0f, 1f)] [SerializeField] private float difficultyPenaltyScale = 0.4f;
        [Range(0f, 0.3f)] [SerializeField] private float perfectTimingBonus = 0.15f;
        [Range(0f, 0.5f)] [SerializeField] private float timingWindow = 0.15f;
        [Header("Level Modifier")]
        [SerializeField] private float playerLevelBonusStep = 0.02f;
        [SerializeField] private float enemyLevelPenaltyStep = 0.03f;

        [SerializeField] private Dex.DexController dexController;
        [SerializeField] private PlayerProgressController playerProgress;
        [SerializeField] private PlayerInsectCollection insectCollection;
        [SerializeField] private PlayerCandyInventory candyInventory;
        [SerializeField] private ItemEffectManager itemEffects;
        [SerializeField] private OutfitBonusProvider outfitBonus;

        public event Action<InsectEntity, bool> CaptureResolved;

        public void AttemptCapture(InsectEntity target, float timing01, float extraBonus = 0f)
        {
            if (target == null || target.Data == null)
            {
                return;
            }

            float chance = Mathf.Clamp01(CalculateSuccessChance(target.Data, target.Level, timing01) + extraBonus);
            bool success = UnityEngine.Random.value <= chance;

            // 이벤트 핸들러 예외가 DEX/보상 처리를 차단하지 않도록 격리
            try { CaptureResolved?.Invoke(target, success); }
            catch (System.Exception e) { Debug.LogWarning($"[CaptureController] CaptureResolved 핸들러 예외: {e.Message}"); }

            if (dexController != null)
            {
                dexController.RegisterEncounter(target.Data.insectId);
                if (success)
                {
                    dexController.RegisterCapture(target.Data.insectId);
                }
            }

            if (success)
            {
                if (playerProgress != null)
                {
                    int exp = InsectRewardCalculator.GetExpReward(target.Data);
                    float expMultiplier = (itemEffects != null ? itemEffects.GetExpMultiplier() : 1f)
                                        * (outfitBonus != null ? outfitBonus.GetExpMultiplier() : 1f);
                    playerProgress.GainXp(Mathf.RoundToInt(exp * expMultiplier));
                }

                if (candyInventory != null)
                {
                    int candy = InsectRewardCalculator.GetCandyReward(target.Data);
                    float candyMultiplier = (itemEffects != null ? itemEffects.GetCandyMultiplier() : 1f)
                                           * (outfitBonus != null ? outfitBonus.GetCandyMultiplier() : 1f);
                    candyInventory.AddCandy(Mathf.RoundToInt(candy * candyMultiplier));
                }
                insectCollection?.AddCapturedInsect(target.Data.insectId, target.Level);
                target.Despawn();
            }
            else
            {
                // 포획 실패 시에도 항상 Despawn — 사용자 의도("미니게임 끝나면 사라져야").
                // 옛은 50% 확률 잔존이라 같은 곤충에 중첩 미니게임 발동 + 필드 중복 인스턴스 가능.
                target.Despawn();
            }
        }

        private float CalculateSuccessChance(InsectData data, int insectLevel, float timing01)
        {
            float chance = baseSuccessChance;
            chance -= (int)data.rarity * rarityPenaltyStep;
            chance -= data.captureDifficulty * difficultyPenaltyScale;
            chance += GetLevelModifier(insectLevel);
            chance += itemEffects != null ? itemEffects.GetCaptureChanceBonus() : 0f;
            chance += outfitBonus != null ? outfitBonus.GetCaptureChanceBonus() : 0f;

            float timingBonus = Mathf.Abs(timing01 - 0.5f) <= timingWindow ? perfectTimingBonus : 0f;
            chance += timingBonus;

            return Mathf.Clamp01(chance);
        }

        private float GetLevelModifier(int insectLevel)
        {
            int playerLevel = playerProgress != null ? playerProgress.Level : 1;
            int diff = playerLevel - Mathf.Max(1, insectLevel);
            if (diff >= 0)
            {
                return diff * playerLevelBonusStep;
            }

            return diff * enemyLevelPenaltyStep;
        }

        public void AutoWire(Dex.DexController dex)
        {
            if (dexController == null)
            {
                dexController = dex;
            }
        }

        public void AutoWire(PlayerProgressController progress)
        {
            if (playerProgress == null)
            {
                playerProgress = progress;
            }
        }

        public void AutoWire(PlayerInsectCollection collection)
        {
            if (insectCollection == null)
            {
                insectCollection = collection;
            }
        }

        public void AutoWire(PlayerCandyInventory candy)
        {
            if (candyInventory == null)
            {
                candyInventory = candy;
            }
        }

        public void AutoWire(ItemEffectManager effects)
        {
            if (itemEffects == null)
            {
                itemEffects = effects;
            }
        }

        public void AutoWire(OutfitBonusProvider bonus)
        {
            if (outfitBonus == null)
            {
                outfitBonus = bonus;
            }
        }

        public void ApplyTuning(GameplayTuningProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            baseSuccessChance = Mathf.Clamp01(profile.baseSuccessChance);
            rarityPenaltyStep = Mathf.Clamp(profile.rarityPenaltyStep, 0f, 0.5f);
            difficultyPenaltyScale = Mathf.Clamp01(profile.difficultyPenaltyScale);
            perfectTimingBonus = Mathf.Clamp(profile.perfectTimingBonus, 0f, 0.5f);
            timingWindow = Mathf.Clamp(profile.timingWindow, 0f, 0.5f);
        }
    }
}
