using System;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    public class ItemEffectManager : MonoBehaviour
    {
        [SerializeField] private ItemDatabase itemDatabase;

        private ItemData activeItem;
        private float remainingSeconds;

        public event Action<ItemData> ActiveItemChanged;
        public ItemData ActiveItem => activeItem;
        public float RemainingSeconds => remainingSeconds;

        private void Update()
        {
            if (activeItem == null)
            {
                return;
            }

            remainingSeconds -= Time.deltaTime;
            if (remainingSeconds <= 0f)
            {
                activeItem = null;
                remainingSeconds = 0f;
                ActiveItemChanged?.Invoke(null);
            }
        }

        public bool ActivateItem(ItemData item)
        {
            if (item == null)
            {
                return false;
            }

            activeItem = item;
            remainingSeconds = Mathf.Max(1f, item.durationSeconds);
            ActiveItemChanged?.Invoke(item);
            return true;
        }

        public bool ActivateItemById(string itemId)
        {
            EnsureDatabase();

            ItemData item = itemDatabase.FindById(itemId);
            return ActivateItem(item);
        }

        public float GetCaptureChanceBonus()
        {
            return activeItem != null ? activeItem.captureChanceBonus : 0f;
        }

        public float GetExpMultiplier()
        {
            return activeItem != null ? activeItem.expMultiplier : 1f;
        }

        public float GetCandyMultiplier()
        {
            return activeItem != null ? activeItem.candyMultiplier : 1f;
        }

        public float GetRareSpawnMultiplier()
        {
            return activeItem != null ? activeItem.rareSpawnMultiplier : 1f;
        }

        public ItemData GetActiveItem()
        {
            return activeItem;
        }

        public void AutoWire(ItemDatabase database)
        {
            if (itemDatabase == null)
            {
                itemDatabase = database;
            }
        }

        private void EnsureDatabase()
        {
            if (itemDatabase == null)
            {
                itemDatabase = Resources.Load<ItemDatabase>("ItemDatabase");
            }
        }
    }
}
