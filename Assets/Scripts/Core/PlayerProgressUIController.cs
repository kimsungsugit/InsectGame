using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InsectGame.Core
{
    public class PlayerProgressUIController : MonoBehaviour
    {
        [SerializeField] private PlayerProgressController progressController;
        [SerializeField] private PlayerCandyInventory candyInventory;
        [SerializeField] private Text levelText;
        [SerializeField] private Text xpText;
        [SerializeField] private TMP_Text levelTextTmp;
        [SerializeField] private TMP_Text xpTextTmp;
        [SerializeField] private Text candyText;
        [SerializeField] private TMP_Text candyTextTmp;

        private void OnEnable()
        {
            if (progressController != null)
            {
                progressController.ProgressChanged += HandleProgressChanged;
                HandleProgressChanged(null);
            }

            if (candyInventory != null)
            {
                candyInventory.CandyChanged += HandleCandyChanged;
                HandleCandyChanged(candyInventory.Candies);
            }
        }

        private void OnDisable()
        {
            if (progressController != null)
            {
                progressController.ProgressChanged -= HandleProgressChanged;
            }

            if (candyInventory != null)
            {
                candyInventory.CandyChanged -= HandleCandyChanged;
            }
        }

        private void HandleProgressChanged(PlayerProgressData data)
        {
            int level = progressController != null ? progressController.Level : 1;
            int currentXp = progressController != null ? progressController.CurrentXp : 0;
            int nextXp = progressController != null ? progressController.XpToNextLevel : 0;

            string text = $"레벨 {level} ({currentXp}/{nextXp})";
            if (levelText != null)
            {
                levelText.text = text;
            }

            if (xpText != null)
            {
                xpText.text = $"{currentXp}/{nextXp}";
            }

            if (levelTextTmp != null)
            {
                levelTextTmp.text = text;
            }

            if (xpTextTmp != null)
            {
                xpTextTmp.text = $"{currentXp}/{nextXp}";
            }
        }

        private void HandleCandyChanged(int candies)
        {
            string text = $"사탕 {candies}";
            if (candyText != null)
            {
                candyText.text = text;
            }

            if (candyTextTmp != null)
            {
                candyTextTmp.text = text;
            }
        }

        public void AutoWire(PlayerProgressController progress)
        {
            if (progressController == null || progressController != progress)
            {
                if (progressController != null)
                    progressController.ProgressChanged -= HandleProgressChanged;
                progressController = progress;
                if (progressController != null)
                {
                    progressController.ProgressChanged += HandleProgressChanged;
                    HandleProgressChanged(null);
                }
            }
        }

        public void AutoWire(PlayerCandyInventory candy)
        {
            if (candyInventory == null || candyInventory != candy)
            {
                if (candyInventory != null)
                    candyInventory.CandyChanged -= HandleCandyChanged;
                candyInventory = candy;
                if (candyInventory != null)
                {
                    candyInventory.CandyChanged += HandleCandyChanged;
                    HandleCandyChanged(candyInventory.Candies);
                }
            }
        }
    }
}
