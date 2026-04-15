using UnityEngine;

namespace InsectGame.Data
{
    [System.Serializable]
    public class TrainingMethod
    {
        public string methodId;
        public string displayName;
        public string description;
        public Color themeColor;
        public int candyCost;
        public int requiredLevel;
        public string[] skillPool;
    }
}
