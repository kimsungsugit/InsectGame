using System.Collections.Generic;
using InsectGame.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InsectGame.Core
{
    public class PlayerItemInventoryGridUIController : MonoBehaviour
    {
        [SerializeField] private PlayerItemInventory inventory;
        [SerializeField] private ItemDatabase itemDatabase;
        [SerializeField] private ItemEffectManager effectManager;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private ItemInventoryGridItem itemPrefab;
        [SerializeField] private TMP_Text activeItemText;
        [SerializeField] private TMP_Text remainingTimeText;
        [SerializeField] private Slider remainingTimeBar;
        [SerializeField] private Image remainingTimeRadial;
        [SerializeField] private Image remainingTimeIcon;

        private void OnEnable()
        {
            EnsureDatabase();
            if (inventory != null)
            {
                inventory.ItemsChanged += HandleItemsChanged;
            }

            if (effectManager != null)
            {
                effectManager.ActiveItemChanged += HandleActiveChanged;
            }

            BuildGrid();
            HandleActiveChanged(effectManager != null ? effectManager.GetActiveItem() : null);
            UpdateRemainingTime();
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.ItemsChanged -= HandleItemsChanged;
            }

            if (effectManager != null)
            {
                effectManager.ActiveItemChanged -= HandleActiveChanged;
            }
        }

        private void Update()
        {
            UpdateRemainingTime();
        }

        private void HandleItemsChanged(PlayerItemSave save)
        {
            BuildGrid();
        }

        private void HandleActiveChanged(ItemData item)
        {
            if (activeItemText == null)
            {
                return;
            }

            activeItemText.text = item != null ? $"사용중: {item.displayName}" : "사용중: 없음";
        }

        private void UpdateRemainingTime()
        {
            if (remainingTimeText == null || effectManager == null)
            {
                UpdateRemainingBar(0f, 0f);
                return;
            }

            if (effectManager.ActiveItem == null)
            {
                remainingTimeText.text = "남은 시간: 00:00";
                UpdateRemainingBar(0f, 0f);
                if (remainingTimeIcon != null)
                {
                    remainingTimeIcon.enabled = false;
                }
                return;
            }

            int seconds = Mathf.Max(0, Mathf.FloorToInt(effectManager.RemainingSeconds));
            int minutes = seconds / 60;
            int sec = seconds % 60;
            remainingTimeText.text = $"남은 시간: {minutes:00}:{sec:00}";
            UpdateRemainingBar(effectManager.RemainingSeconds, effectManager.ActiveItem.durationSeconds);
            if (remainingTimeIcon != null)
            {
                remainingTimeIcon.sprite = effectManager.ActiveItem.icon;
                remainingTimeIcon.enabled = remainingTimeIcon.sprite != null;
            }
        }

        private void UpdateRemainingBar(float remaining, float total)
        {
            if (remainingTimeBar == null)
            {
                if (remainingTimeRadial != null)
                {
                    remainingTimeRadial.fillAmount = 0f;
                }
                return;
            }

            remainingTimeBar.maxValue = Mathf.Max(1f, total);
            remainingTimeBar.value = Mathf.Clamp(remaining, 0f, remainingTimeBar.maxValue);

            if (remainingTimeRadial != null)
            {
                float max = Mathf.Max(1f, total);
                remainingTimeRadial.fillAmount = Mathf.Clamp01(remaining / max);
                remainingTimeRadial.enabled = remaining > 0f;
            }
        }

        public void BuildGrid()
        {
            EnsureDatabase();
            if (inventory == null || itemDatabase == null || contentRoot == null || itemPrefab == null)
            {
                return;
            }

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(contentRoot.GetChild(i).gameObject);
            }

            PlayerItemSave save = inventory.GetSnapshot();
            if (save == null || save.items == null)
            {
                return;
            }

            foreach (PlayerItemRecord record in save.items)
            {
                if (record == null || record.count <= 0)
                {
                    continue;
                }

                ItemData data = itemDatabase.FindById(record.itemId);
                ItemInventoryGridItem item = Instantiate(itemPrefab, contentRoot);
                item.Bind(data, record.count, TryUseItem);
            }
        }

        private void TryUseItem(string itemId)
        {
            if (inventory == null || effectManager == null || itemDatabase == null)
            {
                return;
            }

            if (!inventory.UseItem(itemId, 1))
            {
                return;
            }

            ItemData data = itemDatabase.FindById(itemId);
            if (data != null)
            {
                effectManager.ActivateItem(data);
            }
        }

        public void AutoWire(PlayerItemInventory inv, ItemDatabase db, ItemEffectManager effects)
        {
            if (inventory == null || inventory != inv)
            {
                if (inventory != null)
                    inventory.ItemsChanged -= HandleItemsChanged;
                inventory = inv;
                if (inventory != null)
                    inventory.ItemsChanged += HandleItemsChanged;
            }

            if (itemDatabase == null)
            {
                itemDatabase = db;
            }

            if (effectManager == null || effectManager != effects)
            {
                if (effectManager != null)
                    effectManager.ActiveItemChanged -= HandleActiveChanged;
                effectManager = effects;
                if (effectManager != null)
                    effectManager.ActiveItemChanged += HandleActiveChanged;
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
