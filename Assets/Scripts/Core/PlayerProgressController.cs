using System;
using UnityEngine;

namespace InsectGame.Core
{
    public class PlayerProgressController : MonoBehaviour
    {
        [SerializeField] private int maxLevel = 50;
        [SerializeField] private int baseXpToLevel = 50;
        [SerializeField] private int xpGrowthPerLevel = 15;

        private PlayerProgressData data;

        public int Level => data != null ? data.level : 1;
        public int CurrentXp => data != null ? data.currentXp : 0;
        public int XpToNextLevel => GetXpToNextLevel(Level);

        public event Action<PlayerProgressData> ProgressChanged;

        private void Awake()
        {
            data = PlayerProgressSaveService.Load();
        }

        public void GainXp(int amount)
        {
            if (data == null || amount <= 0)
            {
                return;
            }

            data.currentXp += amount;
            while (data.level < maxLevel && data.currentXp >= GetXpToNextLevel(data.level))
            {
                data.currentXp -= GetXpToNextLevel(data.level);
                data.level++;
            }

            PlayerProgressSaveService.Save(data);
            ProgressChanged?.Invoke(data);
        }

        private int GetXpToNextLevel(int level)
        {
            return Mathf.Max(10, baseXpToLevel + (level - 1) * xpGrowthPerLevel);
        }
    }
}
