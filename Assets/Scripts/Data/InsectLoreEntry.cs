using System;

namespace InsectGame.Data
{
    [Serializable]
    public class InsectLoreEntry
    {
        public string insectId;
        public string displayName;
        public string description;
        public string habitatHint;
        public string spritePath;
        public int rewardCoins;
    }
}
