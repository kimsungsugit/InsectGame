using InsectGame.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InsectGame.Core
{
    public class ShopUIController : MonoBehaviour
    {
        [SerializeField] private PlayerItemInventory inventory;
        [SerializeField] private PlayerCurrencyWallet wallet;
        [SerializeField] private ItemDatabase itemDatabase;
        [SerializeField] private Button[] buyButtons;
        [SerializeField] private TMP_Text[] buyLabels;
        [SerializeField] private string[] itemIds;
        [SerializeField] private int[] prices;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private string successMessage = "구매 완료";
        [SerializeField] private string failMessage = "보석 부족";
        [SerializeField] private bool allowCoinPayment = true;
        [SerializeField] private bool payWithCoins = false;
        [SerializeField] private TMP_Text[] priceLabels;
        [SerializeField] private Toggle coinsToggle;
        [SerializeField] private Toggle gemsToggle;
        [SerializeField] private TMP_Text paymentLabel;

        private void Start()
        {
            EnsureDatabase();
            if (buyButtons == null)
            {
                return;
            }

            if (coinsToggle != null)
            {
                coinsToggle.onValueChanged.RemoveAllListeners();
                coinsToggle.onValueChanged.AddListener(SetCoinPayment);
                coinsToggle.isOn = payWithCoins;
                coinsToggle.interactable = allowCoinPayment;
            }

            if (gemsToggle != null)
            {
                gemsToggle.onValueChanged.RemoveAllListeners();
                gemsToggle.onValueChanged.AddListener(SetGemPayment);
                gemsToggle.isOn = !payWithCoins;
            }

            for (int i = 0; i < buyButtons.Length; i++)
            {
                int index = i;
                Button button = buyButtons[i];
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Buy(index));

                if (buyLabels != null && index < buyLabels.Length && buyLabels[index] != null)
                {
                    string itemId = index < itemIds.Length ? itemIds[index] : string.Empty;
                    ItemData data = !string.IsNullOrEmpty(itemId) ? itemDatabase.FindById(itemId) : null;
                    int price = index < prices.Length ? prices[index] : 0;
                    buyLabels[index].text = data != null ? $"{data.displayName} ({price})" : "구매";
                }

                if (priceLabels != null && index < priceLabels.Length && priceLabels[index] != null)
                {
                    int price = index < prices.Length ? prices[index] : 0;
                    priceLabels[index].text = allowCoinPayment ? $"{price} 보석/코인" : $"{price} 보석";
                }
            }

            UpdatePaymentLabel();
        }

        private void Buy(int index)
        {
            if (inventory == null || itemDatabase == null || wallet == null || itemIds == null || index < 0 || index >= itemIds.Length)
            {
                return;
            }

            string itemId = itemIds[index];
            if (string.IsNullOrEmpty(itemId))
            {
                return;
            }

            int price = index < prices.Length ? prices[index] : 0;
            if (price > 0)
            {
                bool paid = payWithCoins && allowCoinPayment
                    ? wallet.SpendCoins(price)
                    : wallet.SpendGems(price);

                if (!paid)
                {
                    if (resultText != null)
                    {
                        resultText.text = failMessage;
                    }
                    return;
                }
            }

            inventory.AddItem(itemId, 1);
            if (resultText != null)
            {
                resultText.text = successMessage;
            }
        }

        public void AutoWire(PlayerItemInventory inv, ItemDatabase db, PlayerCurrencyWallet walletRef)
        {
            if (inventory == null)
            {
                inventory = inv;
            }

            if (itemDatabase == null)
            {
                itemDatabase = db;
            }

            if (wallet == null)
            {
                wallet = walletRef;
            }
        }

        public void SetCoinPayment(bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            payWithCoins = true;
            if (gemsToggle != null)
            {
                gemsToggle.isOn = false;
            }

            UpdatePaymentLabel();
        }

        public void SetGemPayment(bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            payWithCoins = false;
            if (coinsToggle != null)
            {
                coinsToggle.isOn = false;
            }

            UpdatePaymentLabel();
        }

        private void UpdatePriceLabels()
        {
            if (priceLabels == null)
            {
                return;
            }

            for (int i = 0; i < priceLabels.Length; i++)
            {
                if (priceLabels[i] == null)
                {
                    continue;
                }

                int price = i < prices.Length ? prices[i] : 0;
                priceLabels[i].text = allowCoinPayment ? $"{price} 보석/코인" : $"{price} 보석";
            }
        }

        private void UpdatePaymentLabel()
        {
            if (paymentLabel == null)
            {
                return;
            }

            if (!allowCoinPayment)
            {
                paymentLabel.text = "결제: 보석만";
                return;
            }

            paymentLabel.text = payWithCoins ? "결제: 코인" : "결제: 보석";
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
