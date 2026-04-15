using System.Collections.Generic;
using InsectGame.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InsectGame.Core
{
    public class PlayerInsectSelectionUIController : MonoBehaviour
    {
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Button itemButtonPrefab;
        [SerializeField] private TMP_Text selectedText;
        [SerializeField] private PlayerInsectLevelUpUIController levelUpUi;
        [Header("Filters")]
        [SerializeField] private TMP_Dropdown rarityDropdown;
        [SerializeField] private Slider minLevelSlider;
        [SerializeField] private TMP_Text minLevelLabel;

        private string selectedInstanceId;
        private InsectRarity minRarity = InsectRarity.Common;
        private int minLevel = 1;

        private void Start()
        {
            if (rarityDropdown != null)
            {
                rarityDropdown.onValueChanged.RemoveAllListeners();
                rarityDropdown.onValueChanged.AddListener(SetRarityFilter);
            }

            if (minLevelSlider != null)
            {
                minLevelSlider.onValueChanged.RemoveAllListeners();
                minLevelSlider.onValueChanged.AddListener(SetMinLevelFilter);
            }

            BuildList();
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
            BuildList();
        }

        public void BuildList()
        {
            if (collection == null || contentRoot == null || itemButtonPrefab == null)
            {
                return;
            }

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(contentRoot.GetChild(i).gameObject);
            }

            List<PlayerInsectData> owned = collection.GetAllOwned();
            foreach (PlayerInsectData data in owned)
            {
                if (data == null)
                {
                    continue;
                }

                InsectData insect = collection.GetInsectData(data.insectId);
                if (!PassesFilter(data, insect))
                {
                    continue;
                }

                string label = insect != null
                    ? $"{insect.displayName} Lv {data.level} #{GetShortInstanceId(data)}"
                    : $"{data.insectId} #{GetShortInstanceId(data)}";
                Button button = Instantiate(itemButtonPrefab, contentRoot);
                TMP_Text text = button.GetComponentInChildren<TMP_Text>();
                if (text != null)
                {
                    text.text = label;
                }

                string id = data.instanceId;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Select(id));
            }

            if (string.IsNullOrEmpty(selectedInstanceId) && owned.Count > 0)
            {
                Select(owned[0].instanceId);
            }
        }

        public void Select(string instanceId)
        {
            selectedInstanceId = instanceId;
            PlayerInsectData selected = collection != null ? collection.GetByInstanceId(instanceId) : null;
            InsectData insect = selected != null ? collection.GetInsectData(selected.insectId) : null;
            if (selectedText != null)
            {
                selectedText.text = selected == null
                    ? "-"
                    : insect != null
                        ? $"{insect.displayName} #{GetShortInstanceId(selected)}"
                        : $"{selected.insectId} #{GetShortInstanceId(selected)}";
            }

            if (levelUpUi != null)
            {
                levelUpUi.SetSelectedInsect(instanceId);
            }
        }

        private bool PassesFilter(PlayerInsectData data, InsectData insect)
        {
            if (data.level < minLevel)
            {
                return false;
            }

            if (insect != null && insect.rarity < minRarity)
            {
                return false;
            }

            return true;
        }

        public void SetRarityFilter(int index)
        {
            minRarity = (InsectRarity)Mathf.Clamp(index, 0, System.Enum.GetValues(typeof(InsectRarity)).Length - 1);
            BuildList();
        }

        public void SetMinLevelFilter(float value)
        {
            minLevel = Mathf.Max(1, Mathf.RoundToInt(value));
            if (minLevelLabel != null)
            {
                minLevelLabel.text = $"최소 레벨 {minLevel}";
            }
            BuildList();
        }

        public void AutoWire(PlayerInsectCollection collectionRef, PlayerInsectLevelUpUIController levelUp)
        {
            if (collection == null || collection != collectionRef)
            {
                if (collection != null)
                    collection.InsectUpdated -= HandleInsectUpdated;
                collection = collectionRef;
                if (collection != null)
                    collection.InsectUpdated += HandleInsectUpdated;
            }

            if (levelUpUi == null)
            {
                levelUpUi = levelUp;
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
