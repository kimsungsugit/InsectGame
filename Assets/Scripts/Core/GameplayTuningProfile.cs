using UnityEngine;

namespace InsectGame.Core
{
    [CreateAssetMenu(menuName = "InsectGame/Gameplay Tuning", fileName = "GameplayTuningProfile")]
    public class GameplayTuningProfile : ScriptableObject
    {
        [Header("Spawning")]
        // 기본값은 InsectSpawner 코드 기본값과 동기화 — 에셋을 새로 만들어도 하향 함정이 없도록 유지.
        [Range(1f, 180f)] public float spawnIntervalSeconds = 5f;
        [Range(1, 60)] public int maxActiveTotal = 32;
        [Range(1, 40)] public int initialSpawnCount = 20;
        [Range(1, 15)] public int maxActivePerRegion = 10;
        [Range(1, 5)] public int subAreaActiveCount = 2;
        [Range(5f, 180f)] public float subAreaRespawnSeconds = 45f;

        [Header("NPC")]
        [Range(0, 16)] public int villagerCount = 10;
        [Range(0, 10)] public int catcherKidCount = 6;
        [Range(10f, 120f)] public float kidCatchCooldownSeconds = 45f;

        [Header("Capture")]
        [Range(0f, 1f)] public float baseSuccessChance = 0.35f;
        [Range(0f, 0.5f)] public float rarityPenaltyStep = 0.08f;
        [Range(0f, 1f)] public float difficultyPenaltyScale = 0.45f;
        [Range(0f, 0.5f)] public float perfectTimingBonus = 0.1f;
        [Range(0f, 0.5f)] public float timingWindow = 0.15f;
    }
}
