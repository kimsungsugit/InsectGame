using UnityEngine;

namespace InsectGame.Data
{
    [System.Serializable]
    public class CaptureItemData
    {
        public string itemId;
        public string displayName;
        public string description;
        public Color themeColor;

        [Range(0f, 1f)] public float spawnWeight;
        [Range(0.5f, 2f)] public float speedMultiplier;
        [Range(0.5f, 2f)] public float zoneSizeMultiplier;
        [Range(0.5f, 2f)] public float timeLimitMultiplier;
        [Range(0f, 0.3f)] public float captureBonus;
    }
}
