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
            // q_item 진행 알림은 여기 있었으나 PlayerItemInventory.UseItem으로 옮겼다.
            // 이 메서드는 **시간제 부스터만** 지나간다 — 채집망·치료제는 도달하지 않는다.
            // 게다가 가방 경로는 UseItem 직후 여기를 부르므로 양쪽에 두면 한 번 써도 2가 오른다.
            return true;
        }

        public bool ActivateItemById(string itemId)
        {
            EnsureDatabase();
            if (itemDatabase == null) return false;
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

        public float GetAtkBonus()
        {
            return activeItem != null ? activeItem.atkBonus : 0f;
        }

        public float GetDefBonus()
        {
            return activeItem != null ? activeItem.defBonus : 0f;
        }

        public float GetFleePreventChance()
        {
            return activeItem != null ? activeItem.fleePreventChance : 0f;
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
                if (itemDatabase == null)
                    itemDatabase = ItemDatabase.CreateRuntimeDefault();
            }
        }
    }
}
