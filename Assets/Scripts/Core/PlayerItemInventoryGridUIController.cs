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
                inventory.ItemsChanged -= HandleItemsChanged;
                inventory.ItemsChanged += HandleItemsChanged;
            }

            if (effectManager != null)
            {
                effectManager.ActiveItemChanged -= HandleActiveChanged;
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

        // 잔여시간 표시는 1초 단위라 매 프레임 갱신 불필요. 디바운스로 60fps→1fps 갱신.
        // 만료 시점은 ItemEffectManager.ActiveItemChanged 이벤트가 즉시 처리하므로 무해.
        private float remainingTextTimer;

        private void Update()
        {
            remainingTextTimer += Time.unscaledDeltaTime;
            if (remainingTextTimer >= 1f)
            {
                remainingTextTimer = 0f;
                UpdateRemainingTime();
            }
        }

        private void HandleItemsChanged(PlayerItemSave save)
        {
            BuildGrid();
        }

        private void HandleActiveChanged(ItemData item)
        {
            if (activeItemText != null)
            {
                activeItemText.text = item != null ? $"사용중: {item.displayName}" : "사용중: 없음";
            }

            // 만료/시작 시점에 남은 시간 표시 즉시 갱신 — 옛은 Update 1초 디바운스 대기로 "남은 시간: 00:00" 표시 지연.
            // 주석(line 56)이 "만료 시점은 ActiveItemChanged 즉시 처리" 명시했으나 실제 호출 누락 회귀.
            UpdateRemainingTime();
            remainingTextTimer = 0f;
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

            // **템플릿은 건너뛴다.** `PlaySceneBootstrap`이 `itemPrefab`을 바로 이 `contentRoot`의
            // 자식으로 만들기 때문에, 그냥 전부 지우면 첫 갱신에서 템플릿까지 파괴 예약되고
            // 두 번째 갱신부터는 위 `itemPrefab == null` 가드에 걸려 조기 반환한다 —
            // 그리드가 **옛 수량으로 영구히 얼어붙는다**(아이템을 써도 개수가 그대로다).
            Transform template = itemPrefab.transform;
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = contentRoot.GetChild(i);
                if (child == template) continue;
                Destroy(child.gameObject);
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
                // 템플릿은 숨겨져 있고(`SetActive(false)`) `Instantiate`가 그 상태를 복사하므로,
                // 켜 주지 않으면 셀이 보이지 않고 `GridLayoutGroup` 배치에서도 빠진다.
                item.gameObject.SetActive(true);
                item.Bind(data, record.count, TryUseItem);
            }
        }

        private void TryUseItem(string itemId)
        {
            if (inventory == null || itemDatabase == null)
            {
                return;
            }

            ItemData data = itemDatabase.FindById(itemId);
            if (data == null) return;

            // 대상지정 치료 아이템 — 병원 선택기를 열어 곤충 지정(소비는 선택 시). 여기선 소비하지 않는다.
            if (data.isTargetedUse)
            {
                if (hospital != null) hospital.UseTreatmentItem(data, inventory);
                return;
            }

            // 시간제 부스터 — 즉시 소비 후 활성.
            if (effectManager == null) return;
            if (!inventory.UseItem(itemId, 1)) return;
            effectManager.ActivateItem(data);
        }

        private InsectGame.UI.HospitalUI hospital;
        public void AutoWire(InsectGame.UI.HospitalUI hospitalUi)
        {
            if (hospital == null) hospital = hospitalUi;
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
