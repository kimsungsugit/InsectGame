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
        // VillageBuilder가 저작하는 앵커 수와 맞춘다(주민 14 = 본마을 8 + 전초기지 6,
        // 잡기 아이 7 = meadow 2 + KidSpots 5). 이보다 작으면 SyncSpawns가 앞에서부터
        // 잘라 뒤쪽 리전 전초기지에 주민이 0명이 된다 — 앵커를 늘렸다면 여기도 같이 올릴 것.
        [Range(0, 16)] public int villagerCount = 14;
        [Range(0, 10)] public int catcherKidCount = 7;
        [Range(10f, 120f)] public float kidCatchCooldownSeconds = 45f;

        [Header("Capture")]
        [Range(0f, 1f)] public float baseSuccessChance = 0.35f;
        [Range(0f, 0.5f)] public float rarityPenaltyStep = 0.08f;
        [Range(0f, 1f)] public float difficultyPenaltyScale = 0.45f;
        [Range(0f, 0.5f)] public float perfectTimingBonus = 0.1f;
        [Range(0f, 0.5f)] public float timingWindow = 0.15f;
    }
}
