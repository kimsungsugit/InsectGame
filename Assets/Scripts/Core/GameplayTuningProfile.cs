using UnityEngine;

namespace InsectGame.Core
{
    [CreateAssetMenu(menuName = "InsectGame/Gameplay Tuning", fileName = "GameplayTuningProfile")]
    public class GameplayTuningProfile : ScriptableObject
    {
        [Header("Spawning")]
        [Range(1f, 180f)] public float spawnIntervalSeconds = 60f;
        [Range(1, 40)] public int maxActiveTotal = 20;

        [Header("Capture")]
        [Range(0f, 1f)] public float baseSuccessChance = 0.6f;
        [Range(0f, 0.5f)] public float rarityPenaltyStep = 0.08f;
        [Range(0f, 1f)] public float difficultyPenaltyScale = 0.4f;
        [Range(0f, 0.5f)] public float perfectTimingBonus = 0.15f;
        [Range(0f, 0.5f)] public float timingWindow = 0.15f;
    }
}
