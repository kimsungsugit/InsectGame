using InsectGame.Dex;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InsectGame.UI
{
    public class PlayUIRefs : MonoBehaviour
    {
        [Header("Summary")]
        public Text discoveredText;
        public Text capturedText;
        public TMP_Text discoveredTextTmp;
        public TMP_Text capturedTextTmp;
        public Text playerLevelText;
        public Text playerXpText;
        public TMP_Text playerLevelTextTmp;
        public TMP_Text playerXpTextTmp;
        public TMP_Text playerCandyTextTmp;

        [Header("Capture")]
        public GameObject capturePanel;
        public Slider timingSlider;
        public Button confirmButton;
        public Button cancelButton;
        public Button startCaptureButton;
        public Text popupText;
        public TMP_Text popupTextTmp;

        [Header("Dex List")]
        public RectTransform listRoot;
        public DexListItemUI listItemPrefab;

        [Header("Dex Detail")]
        public GameObject detailPanel;
        public Text detailName;
        public Text detailRarity;
        public Text detailPower;
        public Text detailDesc;
        public Text detailHint;
        public Text detailCount;
        public Text detailReward;
        public TMP_Text detailNameTmp;
        public TMP_Text detailRarityTmp;
        public TMP_Text detailPowerTmp;
        public TMP_Text detailDescTmp;
        public TMP_Text detailHintTmp;
        public TMP_Text detailCountTmp;
        public TMP_Text detailRewardTmp;

        [Header("Battle UI")]
        public GameObject battlePanel;
        public Slider playerHpBar;
        public Slider enemyHpBar;
        public TMP_Text playerHpText;
        public TMP_Text enemyHpText;
        public Button[] skillButtons;
        public TMP_Text[] skillLabels;
        public TMP_Text[] skillCooldownLabels;
        public Button startBattleButton;
        public GameObject battleResultPanel;
        public TMP_Text battleResultText;
        public TMP_Text battleRewardText;
        public TMP_Text playerEffectText;
        public TMP_Text enemyEffectText;
        public Image[] skillIconImages;
        public Image[] skillCooldownImages;
        public Image[] skillBorderImages;

        [Header("Level Up UI")]
        public TMP_Text levelUpInsectNameText;
        public TMP_Text levelUpInsectLevelText;
        public TMP_Text levelUpCandyCostText;
        public Button levelUpButton;
        public TMP_Text levelUpResultText;
        public RectTransform levelUpListRoot;
        public Button levelUpListItemPrefab;
        public TMP_Text levelUpSelectedText;
        public TMP_Dropdown levelUpRarityDropdown;
        public Slider levelUpMinLevelSlider;
        public TMP_Text levelUpMinLevelLabel;
        public TMP_Text levelUpRarityLabel;

        [Header("Inventory UI")]
        public TMP_Text inventoryText;
        public RectTransform inventoryGridRoot;
        public InsectGame.Core.ItemInventoryGridItem inventoryGridItemPrefab;
        public TMP_Text activeItemText;
        public TMP_Text activeItemTimeText;
        public Slider activeItemTimeBar;
        public Image activeItemTimeRadial;
        public Image activeItemTimeIcon;
        public InsectGame.Data.ItemRarityPalette itemRarityPalette;

        [Header("Shop UI")]
        public Button[] shopBuyButtons;
        public TMP_Text[] shopBuyLabels;
        public TMP_Text shopResultText;
        public TMP_Text gemsText;
        public TMP_Text coinsText;
        public TMP_Text[] shopPriceLabels;
        public Toggle shopCoinsToggle;
        public Toggle shopGemsToggle;
        public TMP_Text shopPaymentLabel;

        [Header("Rarity Tuning UI")]
        public Slider commonPulseSlider;
        public Slider uncommonPulseSlider;
        public Slider rarePulseSlider;
        public Slider epicPulseSlider;
        public Slider legendaryPulseSlider;
        public TMP_Text commonPulseLabel;
        public TMP_Text uncommonPulseLabel;
        public TMP_Text rarePulseLabel;
        public TMP_Text epicPulseLabel;
        public TMP_Text legendaryPulseLabel;
    }
}
