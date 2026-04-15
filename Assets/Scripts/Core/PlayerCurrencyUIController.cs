using TMPro;
using UnityEngine;

namespace InsectGame.Core
{
    public class PlayerCurrencyUIController : MonoBehaviour
    {
        [SerializeField] private PlayerCurrencyWallet wallet;
        [SerializeField] private TMP_Text gemsText;
        [SerializeField] private TMP_Text coinsText;

        private void OnEnable()
        {
            if (wallet != null)
            {
                wallet.CurrencyChanged += HandleChanged;
                HandleChanged(null);
            }
        }

        private void OnDisable()
        {
            if (wallet != null)
            {
                wallet.CurrencyChanged -= HandleChanged;
            }
        }

        private void HandleChanged(PlayerCurrencyData data)
        {
            if (gemsText != null)
            {
                gemsText.text = $"보석 {wallet.Gems}";
            }

            if (coinsText != null)
            {
                coinsText.text = $"코인 {wallet.Coins}";
            }
        }

        public void AutoWire(PlayerCurrencyWallet walletRef)
        {
            if (wallet == null || wallet != walletRef)
            {
                if (wallet != null)
                    wallet.CurrencyChanged -= HandleChanged;
                wallet = walletRef;
                if (wallet != null)
                {
                    wallet.CurrencyChanged += HandleChanged;
                    HandleChanged(null);
                }
            }
        }
    }
}
