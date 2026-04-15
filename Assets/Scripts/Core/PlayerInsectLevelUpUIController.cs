using InsectGame.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InsectGame.Core
{
    public class PlayerInsectLevelUpUIController : MonoBehaviour
    {
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private TMP_Text insectNameText;
        [SerializeField] private TMP_Text insectLevelText;
        [SerializeField] private TMP_Text candyCostText;
        [SerializeField] private Button levelUpButton;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Text insectNameTextLegacy;
        [SerializeField] private Text insectLevelTextLegacy;
        [SerializeField] private Text candyCostTextLegacy;
        [SerializeField] private Text resultTextLegacy;
        [SerializeField] private string successMessage = "레벨 업!";
        [SerializeField] private string failMessage = "사탕 부족";

        private PlayerInsectData current;
        private string selectedInstanceId;

        private void Start()
        {
            if (levelUpButton != null)
            {
                levelUpButton.onClick.RemoveAllListeners();
                levelUpButton.onClick.AddListener(LevelUpCurrent);
            }

            Refresh();
        }

        private void OnEnable()
        {
            if (collection != null)
            {
                collection.InsectUpdated += HandleInsectUpdated;
            }
        }

        private void OnDisable()
        {
            if (collection != null)
            {
                collection.InsectUpdated -= HandleInsectUpdated;
            }
        }

        private void HandleInsectUpdated(PlayerInsectData data)
        {
            Refresh();
        }

        public void Refresh()
        {
            if (collection == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(selectedInstanceId))
            {
                current = collection.GetByInstanceId(selectedInstanceId);
            }
            else if (!collection.TryGetAnyOwned(out current))
            {
                SetTexts("-", "-", "-");
                return;
            }

            InsectData insect = collection.GetInsectData(current.insectId);
            string name = insect != null
                ? $"{insect.displayName} #{GetShortInstanceId(current)}"
                : $"{current.insectId} #{GetShortInstanceId(current)}";
            int cost = 0;
            if (insect != null)
            {
                InsectLevelCurve curve = insect.levelCurve;
                if (curve != null)
                {
                    cost = curve.GetCandyCost(current.level);
                }
            }

            SetTexts(name, $"Lv {current.level}", $"사탕 {cost}");
        }

        public void SetSelectedInsect(string instanceId)
        {
            selectedInstanceId = instanceId;
            Refresh();
        }

        public void LevelUpCurrent()
        {
            if (collection == null || current == null)
            {
                return;
            }

            bool success = collection.TryLevelUpWithCandyByInstance(current.instanceId);
            if (resultText != null)
            {
                resultText.text = success ? successMessage : failMessage;
            }
            if (resultTextLegacy != null)
            {
                resultTextLegacy.text = success ? successMessage : failMessage;
            }

            Refresh();
        }

        private void SetTexts(string name, string level, string cost)
        {
            if (insectNameText != null)
            {
                insectNameText.text = name;
            }
            if (insectNameTextLegacy != null)
            {
                insectNameTextLegacy.text = name;
            }

            if (insectLevelText != null)
            {
                insectLevelText.text = level;
            }
            if (insectLevelTextLegacy != null)
            {
                insectLevelTextLegacy.text = level;
            }

            if (candyCostText != null)
            {
                candyCostText.text = cost;
            }
            if (candyCostTextLegacy != null)
            {
                candyCostTextLegacy.text = cost;
            }
        }

        public void AutoWire(PlayerInsectCollection playerCollection)
        {
            if (collection == null || collection != playerCollection)
            {
                if (collection != null)
                    collection.InsectUpdated -= HandleInsectUpdated;
                collection = playerCollection;
                if (collection != null)
                    collection.InsectUpdated += HandleInsectUpdated;
            }
        }

        private static string GetShortInstanceId(PlayerInsectData data)
        {
            if (data == null || string.IsNullOrEmpty(data.instanceId))
            {
                return "----";
            }

            return data.instanceId.Substring(0, Mathf.Min(6, data.instanceId.Length)).ToUpperInvariant();
        }
    }
}
