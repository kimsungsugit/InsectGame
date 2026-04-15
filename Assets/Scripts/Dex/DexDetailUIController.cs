using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InsectGame.Dex
{
    public class DexDetailUIController : MonoBehaviour
    {
        [SerializeField] private InsectDatabase database;
        [SerializeField] private DexController dexController;
        [SerializeField] private Text nameText;
        [SerializeField] private Text rarityText;
        [SerializeField] private Text powerText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text hintText;
        [SerializeField] private Text countText;
        [SerializeField] private TMP_Text nameTextTmp;
        [SerializeField] private TMP_Text rarityTextTmp;
        [SerializeField] private TMP_Text powerTextTmp;
        [SerializeField] private TMP_Text descriptionTextTmp;
        [SerializeField] private TMP_Text hintTextTmp;
        [SerializeField] private TMP_Text countTextTmp;
        [SerializeField] private Image insectImage;
        [SerializeField] private Image rarityIconImage;
        [SerializeField] private Text rewardText;
        [SerializeField] private TMP_Text rewardTextTmp;
        [SerializeField] private RarityIconProvider rarityIconProvider;
        [SerializeField] private GameObject panelRoot;
        [Header("Rarity Colors")]
        [SerializeField] private Color commonColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Color uncommonColor = new Color(0.5f, 0.9f, 0.5f, 1f);
        [SerializeField] private Color rareColor = new Color(0.4f, 0.7f, 1f, 1f);
        [SerializeField] private Color epicColor = new Color(0.8f, 0.4f, 1f, 1f);
        [SerializeField] private Color legendaryColor = new Color(1f, 0.75f, 0.2f, 1f);

        public void Show(string insectId)
        {
            if (database == null || dexController == null || string.IsNullOrEmpty(insectId))
            {
                return;
            }

            InsectData data = database.insects.Find(item => item != null && item.insectId == insectId);
            if (data == null)
            {
                return;
            }

            bool discovered = dexController.TryGetRecord(insectId, out DexRecord record);
            InsectLoreService.TryGetEntry(insectId, out InsectLoreEntry lore);
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            if (nameText != null)
            {
                nameText.text = discovered ? data.displayName : "???";
            }
            if (nameTextTmp != null)
            {
                nameTextTmp.text = discovered ? data.displayName : "???";
            }

            if (rarityText != null)
            {
                rarityText.text = discovered ? $"등급: {data.rarity}" : "등급: ???";
                rarityText.color = discovered ? GetRarityColor(data.rarity) : Color.white;
            }
            if (rarityTextTmp != null)
            {
                rarityTextTmp.text = discovered ? $"등급: {data.rarity}" : "등급: ???";
                rarityTextTmp.color = discovered ? GetRarityColor(data.rarity) : Color.white;
            }

            if (powerText != null)
            {
                powerText.text = discovered ? $"기본 CP: {PlayerInsectCombatPower.CalculateBasePreview(data, data.minLevel)}" : "기본 CP: ???";
            }
            if (powerTextTmp != null)
            {
                powerTextTmp.text = discovered ? $"기본 CP: {PlayerInsectCombatPower.CalculateBasePreview(data, data.minLevel)}" : "기본 CP: ???";
            }

            if (descriptionText != null)
            {
                if (discovered)
                {
                    string description = string.IsNullOrWhiteSpace(data.description) ? lore?.description : data.description;
                    descriptionText.text = string.IsNullOrWhiteSpace(description) ? "설명이 없다." : description;
                }
                else
                {
                    descriptionText.text = "아직 정보가 없다.";
                }
            }
            if (descriptionTextTmp != null)
            {
                if (discovered)
                {
                    string description = string.IsNullOrWhiteSpace(data.description) ? lore?.description : data.description;
                    descriptionTextTmp.text = string.IsNullOrWhiteSpace(description) ? "설명이 없다." : description;
                }
                else
                {
                    descriptionTextTmp.text = "아직 정보가 없다.";
                }
            }

            if (hintText != null)
            {
                if (discovered)
                {
                    string hint = string.IsNullOrWhiteSpace(data.habitatHint) ? lore?.habitatHint : data.habitatHint;
                    hintText.text = string.IsNullOrWhiteSpace(hint) ? "힌트: 없음" : hint;
                }
                else
                {
                    hintText.text = "힌트: ???";
                }
            }
            if (hintTextTmp != null)
            {
                if (discovered)
                {
                    string hint = string.IsNullOrWhiteSpace(data.habitatHint) ? lore?.habitatHint : data.habitatHint;
                    hintTextTmp.text = string.IsNullOrWhiteSpace(hint) ? "힌트: 없음" : hint;
                }
                else
                {
                    hintTextTmp.text = "힌트: ???";
                }
            }

            if (countText != null)
            {
                if (discovered)
                {
                    countText.text = $"발견 {record.discoveredCount} / 포획 {record.capturedCount}";
                }
                else
                {
                    countText.text = "발견 0 / 포획 0";
                }
            }
            if (countTextTmp != null)
            {
                if (discovered)
                {
                    countTextTmp.text = $"발견 {record.discoveredCount} / 포획 {record.capturedCount}";
                }
                else
                {
                    countTextTmp.text = "발견 0 / 포획 0";
                }
            }

            if (insectImage != null)
            {
                if (discovered && lore != null && !string.IsNullOrEmpty(lore.spritePath))
                {
                    Sprite sprite = Resources.Load<Sprite>(lore.spritePath);
                    insectImage.sprite = sprite;
                    insectImage.enabled = sprite != null;
                }
                else
                {
                    insectImage.enabled = false;
                }
            }

            if (rarityIconImage != null)
            {
                Sprite icon = rarityIconProvider != null ? rarityIconProvider.GetIcon(data.rarity) : null;
                rarityIconImage.sprite = icon;
                rarityIconImage.enabled = icon != null && discovered;
                rarityIconImage.color = GetRarityColor(data.rarity);
            }

            if (rewardText != null)
            {
                if (discovered && lore != null)
                {
                    rewardText.text = $"보상: {lore.rewardCoins} 코인";
                }
                else
                {
                    rewardText.text = "보상: ???";
                }
            }
            if (rewardTextTmp != null)
            {
                if (discovered && lore != null)
                {
                    rewardTextTmp.text = $"보상: {lore.rewardCoins} 코인";
                }
                else
                {
                    rewardTextTmp.text = "보상: ???";
                }
            }
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private Color GetRarityColor(InsectRarity rarity)
        {
            switch (rarity)
            {
                case InsectRarity.Common:
                    return commonColor;
                case InsectRarity.Uncommon:
                    return uncommonColor;
                case InsectRarity.Rare:
                    return rareColor;
                case InsectRarity.Epic:
                    return epicColor;
                case InsectRarity.Legendary:
                    return legendaryColor;
                default:
                    return Color.white;
            }
        }

        public void AutoWire(InsectDatabase db, DexController dex)
        {
            if (database == null)
            {
                database = db;
            }

            if (dexController == null)
            {
                dexController = dex;
            }
        }
    }
}
