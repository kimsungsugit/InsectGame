using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Data
{
    [CreateAssetMenu(menuName = "InsectGame/Insect Data", fileName = "InsectData")]
    public class InsectData : ScriptableObject
    {
        public string insectId;
        public string displayName;
        [TextArea(2, 4)] public string description;
        [TextArea(1, 3)] public string habitatHint;
        public InsectRarity rarity;
        public InsectElement primaryType = InsectElement.Bug;
        public InsectElement secondaryType = InsectElement.None;
        [Range(1, 999)] public int basePower = 10;
        [Header("Base Stats")]
        [Range(10, 300)] public int baseHp = 50;
        [Range(5, 200)] public int baseAtk = 20;
        [Range(5, 200)] public int baseDef = 15;
        [Range(0.1f, 100f)] public float spawnWeight = 1f;
        [Range(0f, 1f)] public float captureDifficulty = 0.3f;
        [Header("Leveling")]
        [Range(1, 100)] public int minLevel = 1;
        [Range(1, 100)] public int maxLevel = 3;
        [Range(1, 500)] public int expReward = 5;
        [Range(0, 500)] public int candyReward = 2;
        [Header("Item Reward")]
        public string itemRewardId = "mat_leaf";
        [Range(0, 50)] public int itemRewardCount = 1;
        public InsectLevelCurve levelCurve;
        [Header("Skills")]
        public InsectSkill[] skills;
        public InsectLearnableSkill[] learnset;
        public GameObject prefabOverride;
        public InsectSpawnCondition spawnCondition;

        public bool Matches(WorldState state)
        {
            return spawnCondition.Matches(state);
        }
    }
}
