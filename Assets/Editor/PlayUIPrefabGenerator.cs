using System.IO;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Dex;
using InsectGame.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InsectGame.Editor
{
    public static class PlayUIPrefabGenerator
    {
        private const string PrefabPath = "Assets/Resources/UI/PlayHUD.prefab";
        private const string ConfigPath = "Assets/Resources/PlayUIConfig.asset";

        [MenuItem("InsectGame/UI/Create TMP Play HUD Prefab")]
        public static void CreateTmpPlayHudPrefab()
        {
            EnsureFolder("Assets/Resources/UI");

            GameObject root = new GameObject("PlayHUD");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.AddComponent<GraphicRaycaster>();

            PlayUIRefs refs = root.AddComponent<PlayUIRefs>();

            TMP_Text discovered = CreateTMPText(root.transform, "DiscoveredText", new Vector2(30f, -40f), "발견: 0");
            TMP_Text captured = CreateTMPText(root.transform, "CapturedText", new Vector2(30f, -80f), "포획: 0");
            refs.discoveredTextTmp = discovered;
            refs.capturedTextTmp = captured;

            TMP_Text levelText = CreateTMPText(root.transform, "PlayerLevelText", new Vector2(30f, -120f), "레벨 1 (0/0)");
            TMP_Text xpText = CreateTMPText(root.transform, "PlayerXpText", new Vector2(30f, -160f), "0/0");
            refs.playerLevelTextTmp = levelText;
            refs.playerXpTextTmp = xpText;

            TMP_Text candyText = CreateTMPText(root.transform, "PlayerCandyText", new Vector2(30f, -200f), "사탕 0");
            refs.playerCandyTextTmp = candyText;

            TMP_Text popup = CreateTMPText(root.transform, "PopupText", new Vector2(0f, -300f), "");
            popup.alignment = TextAlignmentOptions.Center;
            popup.enabled = false;
            refs.popupTextTmp = popup;

            GameObject capturePanel = CreatePanel(root.transform, "CapturePanel", new Vector2(0f, -600f), new Vector2(600f, 200f));
            Slider slider = CreateSlider(capturePanel.transform, "TimingSlider", new Vector2(0f, 20f), new Vector2(400f, 20f));
            Button confirmButton = CreateButton(capturePanel.transform, "ConfirmButton", new Vector2(-120f, -40f), "포획");
            Button cancelButton = CreateButton(capturePanel.transform, "CancelButton", new Vector2(120f, -40f), "취소");
            refs.capturePanel = capturePanel;
            refs.timingSlider = slider;
            refs.confirmButton = confirmButton;
            refs.cancelButton = cancelButton;

            Button startButton = CreateButton(root.transform, "StartCaptureButton", new Vector2(0f, -1000f), "포획 시작");
            refs.startCaptureButton = startButton;

            GameObject listPanel = CreatePanel(root.transform, "DexListPanel", new Vector2(-320f, -520f), new Vector2(420f, 520f));
            RectTransform listRoot = new GameObject("ListRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            listRoot.SetParent(listPanel.transform, false);
            listRoot.anchorMin = new Vector2(0f, 0f);
            listRoot.anchorMax = new Vector2(1f, 1f);
            listRoot.offsetMin = new Vector2(20f, 20f);
            listRoot.offsetMax = new Vector2(-20f, -20f);
            listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            listRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            refs.listRoot = listRoot;

            DexListItemUI listItemPrefab = CreateListItemPrefab(listRoot);
            listItemPrefab.gameObject.SetActive(false);
            refs.listItemPrefab = listItemPrefab;

            GameObject detailPanel = CreatePanel(root.transform, "DexDetailPanel", new Vector2(320f, -520f), new Vector2(420f, 520f));
            refs.detailPanel = detailPanel;
            refs.detailNameTmp = CreateTMPText(detailPanel.transform, "NameText", new Vector2(0f, -30f), "???");
            refs.detailRarityTmp = CreateTMPText(detailPanel.transform, "RarityText", new Vector2(0f, -70f), "등급: ???");
            refs.detailPowerTmp = CreateTMPText(detailPanel.transform, "PowerText", new Vector2(0f, -110f), "기본 힘: ???");
            refs.detailDescTmp = CreateTMPText(detailPanel.transform, "DescText", new Vector2(0f, -170f), "설명");
            refs.detailHintTmp = CreateTMPText(detailPanel.transform, "HintText", new Vector2(0f, -260f), "힌트");
            refs.detailCountTmp = CreateTMPText(detailPanel.transform, "CountText", new Vector2(0f, -330f), "발견 0 / 포획 0");
            refs.detailRewardTmp = CreateTMPText(detailPanel.transform, "RewardText", new Vector2(0f, -370f), "보상: ???");

            GameObject battlePanel = CreatePanel(root.transform, "BattlePanel", new Vector2(0f, -1200f), new Vector2(640f, 320f));
            refs.battlePanel = battlePanel;
            Slider playerHp = CreateSlider(battlePanel.transform, "PlayerHpBar", new Vector2(-120f, -40f), new Vector2(200f, 20f));
            Slider enemyHp = CreateSlider(battlePanel.transform, "EnemyHpBar", new Vector2(120f, -40f), new Vector2(200f, 20f));
            TMP_Text playerHpText = CreateTMPText(battlePanel.transform, "PlayerHpText", new Vector2(-120f, -70f), "0/0");
            TMP_Text enemyHpText = CreateTMPText(battlePanel.transform, "EnemyHpText", new Vector2(120f, -70f), "0/0");
            refs.playerHpBar = playerHp;
            refs.enemyHpBar = enemyHp;
            refs.playerHpText = playerHpText;
            refs.enemyHpText = enemyHpText;

            Button skill1 = CreateButton(battlePanel.transform, "Skill1Button", new Vector2(-160f, -130f), "스킬1");
            Button skill2 = CreateButton(battlePanel.transform, "Skill2Button", new Vector2(0f, -130f), "스킬2");
            Button skill3 = CreateButton(battlePanel.transform, "Skill3Button", new Vector2(160f, -130f), "스킬3");
            refs.skillButtons = new[] { skill1, skill2, skill3 };
            refs.skillLabels = new[]
            {
                skill1.GetComponentInChildren<TextMeshProUGUI>(),
                skill2.GetComponentInChildren<TextMeshProUGUI>(),
                skill3.GetComponentInChildren<TextMeshProUGUI>()
            };

            Button startBattle = CreateButton(root.transform, "StartBattleButton", new Vector2(0f, -1120f), "배틀 시작");
            refs.startBattleButton = startBattle;

            GameObject resultPanel = CreatePanel(root.transform, "BattleResultPanel", new Vector2(0f, -1020f), new Vector2(300f, 120f));
            TMP_Text resultText = CreateTMPText(resultPanel.transform, "ResultText", new Vector2(0f, -30f), "승리!");
            resultText.alignment = TextAlignmentOptions.Center;
            refs.battleResultPanel = resultPanel;
            refs.battleResultText = resultText;
            TMP_Text rewardText = CreateTMPText(resultPanel.transform, "RewardText", new Vector2(0f, -70f), "보상: 사탕 0");
            rewardText.alignment = TextAlignmentOptions.Center;
            refs.battleRewardText = rewardText;

            TMP_Text[] cooldownLabels = new TMP_Text[]
            {
                CreateTMPText(battlePanel.transform, "Skill1Cooldown", new Vector2(-160f, -160f), ""),
                CreateTMPText(battlePanel.transform, "Skill2Cooldown", new Vector2(0f, -160f), ""),
                CreateTMPText(battlePanel.transform, "Skill3Cooldown", new Vector2(160f, -160f), "")
            };
            refs.skillCooldownLabels = cooldownLabels;

            Image[] skillIcons = new Image[]
            {
                CreateRadialImage(skill1.transform, "Skill1Icon", new Vector2(0f, 0f), new Vector2(48f, 48f), false),
                CreateRadialImage(skill2.transform, "Skill2Icon", new Vector2(0f, 0f), new Vector2(48f, 48f), false),
                CreateRadialImage(skill3.transform, "Skill3Icon", new Vector2(0f, 0f), new Vector2(48f, 48f), false)
            };
            refs.skillIconImages = skillIcons;

            Image[] cooldownRings = new Image[]
            {
                CreateRadialImage(skill1.transform, "Skill1CooldownRing", new Vector2(0f, 0f), new Vector2(60f, 60f), true),
                CreateRadialImage(skill2.transform, "Skill2CooldownRing", new Vector2(0f, 0f), new Vector2(60f, 60f), true),
                CreateRadialImage(skill3.transform, "Skill3CooldownRing", new Vector2(0f, 0f), new Vector2(60f, 60f), true)
            };
            refs.skillCooldownImages = cooldownRings;

            Image[] borders = new Image[]
            {
                CreateRadialImage(skill1.transform, "Skill1Border", new Vector2(0f, 0f), new Vector2(64f, 64f), false),
                CreateRadialImage(skill2.transform, "Skill2Border", new Vector2(0f, 0f), new Vector2(64f, 64f), false),
                CreateRadialImage(skill3.transform, "Skill3Border", new Vector2(0f, 0f), new Vector2(64f, 64f), false)
            };
            foreach (Image border in borders)
            {
                border.color = new Color(1f, 1f, 1f, 0.6f);
            }
            refs.skillBorderImages = borders;

            TMP_Text playerEffect = CreateTMPText(battlePanel.transform, "PlayerEffectText", new Vector2(-120f, -10f), "");
            TMP_Text enemyEffect = CreateTMPText(battlePanel.transform, "EnemyEffectText", new Vector2(120f, -10f), "");
            playerEffect.alignment = TextAlignmentOptions.Center;
            enemyEffect.alignment = TextAlignmentOptions.Center;
            refs.playerEffectText = playerEffect;
            refs.enemyEffectText = enemyEffect;

            refs.levelUpInsectNameText = CreateTMPText(root.transform, "LevelUpName", new Vector2(0f, -1360f), "곤충");
            refs.levelUpInsectLevelText = CreateTMPText(root.transform, "LevelUpLevel", new Vector2(0f, -1400f), "Lv 1");
            refs.levelUpCandyCostText = CreateTMPText(root.transform, "LevelUpCost", new Vector2(0f, -1440f), "사탕 0");
            refs.levelUpButton = CreateButton(root.transform, "LevelUpButton", new Vector2(0f, -1480f), "레벨업");
            refs.levelUpResultText = CreateTMPText(root.transform, "LevelUpResult", new Vector2(0f, -1520f), "");

            GameObject levelUpListPanel = CreatePanel(root.transform, "LevelUpListPanel", new Vector2(0f, -1600f), new Vector2(320f, 240f));
            RectTransform levelUpListRoot = new GameObject("LevelUpListRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            levelUpListRoot.SetParent(levelUpListPanel.transform, false);
            levelUpListRoot.anchorMin = new Vector2(0f, 0f);
            levelUpListRoot.anchorMax = new Vector2(1f, 1f);
            levelUpListRoot.offsetMin = new Vector2(10f, 10f);
            levelUpListRoot.offsetMax = new Vector2(-10f, -10f);
            levelUpListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            levelUpListRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            refs.levelUpListRoot = levelUpListRoot;

            Button listItem = CreateButton(levelUpListRoot, "LevelUpListItem", Vector2.zero, "곤충");
            listItem.gameObject.SetActive(false);
            refs.levelUpListItemPrefab = listItem;

            TMP_Text selectedText = CreateTMPText(root.transform, "LevelUpSelectedText", new Vector2(0f, -1330f), "선택: -");
            selectedText.alignment = TextAlignmentOptions.Center;
            refs.levelUpSelectedText = selectedText;

            TMP_Dropdown rarityDropdown = CreateDropdown(root.transform, "RarityDropdown", new Vector2(0f, -1680f), new Vector2(240f, 40f),
                new[] { "Common", "Uncommon", "Rare", "Epic", "Legendary" });
            refs.levelUpRarityDropdown = rarityDropdown;

            Slider minLevelSlider = CreateSlider(root.transform, "MinLevelSlider", new Vector2(0f, -1730f), new Vector2(240f, 20f));
            minLevelSlider.minValue = 1;
            minLevelSlider.maxValue = 50;
            refs.levelUpMinLevelSlider = minLevelSlider;

            TMP_Text minLevelLabel = CreateTMPText(root.transform, "MinLevelLabel", new Vector2(0f, -1760f), "최소 레벨 1");
            minLevelLabel.alignment = TextAlignmentOptions.Center;
            refs.levelUpMinLevelLabel = minLevelLabel;

            TMP_Text rarityLabel = CreateTMPText(root.transform, "RarityLabel", new Vector2(0f, -1645f), "희귀도 필터");
            rarityLabel.alignment = TextAlignmentOptions.Center;
            refs.levelUpRarityLabel = rarityLabel;

            GameObject inventoryPanel = CreatePanel(root.transform, "InventoryPanel", new Vector2(0f, -1860f), new Vector2(320f, 160f));
            TMP_Text inventoryText = CreateTMPText(inventoryPanel.transform, "InventoryText", new Vector2(0f, -30f), "아이템 없음");
            inventoryText.alignment = TextAlignmentOptions.Center;
            refs.inventoryText = inventoryText;

            TMP_Text activeItemText = CreateTMPText(inventoryPanel.transform, "ActiveItemText", new Vector2(0f, -70f), "사용중: 없음");
            activeItemText.alignment = TextAlignmentOptions.Center;
            refs.activeItemText = activeItemText;

            TMP_Text activeTimeText = CreateTMPText(inventoryPanel.transform, "ActiveTimeText", new Vector2(0f, -100f), "남은 시간: 00:00");
            activeTimeText.alignment = TextAlignmentOptions.Center;
            refs.activeItemTimeText = activeTimeText;
            Slider activeTimeBar = CreateSlider(inventoryPanel.transform, "ActiveTimeBar", new Vector2(0f, -130f), new Vector2(240f, 16f));
            refs.activeItemTimeBar = activeTimeBar;
            Image radial = CreateRadialImage(inventoryPanel.transform, "ActiveTimeRadial", new Vector2(120f, -40f), new Vector2(36f, 36f), true);
            Image radialIcon = CreateRadialImage(inventoryPanel.transform, "ActiveTimeIcon", new Vector2(120f, -40f), new Vector2(28f, 28f), false);
            refs.activeItemTimeRadial = radial;
            refs.activeItemTimeIcon = radialIcon;

            GameObject gridPanel = CreatePanel(root.transform, "InventoryGridPanel", new Vector2(0f, -2060f), new Vector2(360f, 220f));
            RectTransform gridRoot = new GameObject("InventoryGridRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            gridRoot.SetParent(gridPanel.transform, false);
            gridRoot.anchorMin = new Vector2(0f, 0f);
            gridRoot.anchorMax = new Vector2(1f, 1f);
            gridRoot.offsetMin = new Vector2(10f, 10f);
            gridRoot.offsetMax = new Vector2(-10f, -10f);
            GridLayoutGroup grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(100f, 100f);
            grid.spacing = new Vector2(8f, 8f);
            refs.inventoryGridRoot = gridRoot;

            GameObject gridItem = new GameObject("InventoryItem");
            gridItem.transform.SetParent(gridRoot, false);
            Image itemBg = gridItem.AddComponent<Image>();
            itemBg.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
            RectTransform itemRect = gridItem.GetComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(100f, 100f);
            Button itemButton = gridItem.AddComponent<Button>();
            Image itemIcon = CreateRadialImage(gridItem.transform, "ItemIcon", Vector2.zero, new Vector2(48f, 48f), false);
            TMP_Text itemName = CreateTMPText(gridItem.transform, "ItemName", new Vector2(0f, -10f), "아이템");
            itemName.alignment = TextAlignmentOptions.Center;
            TMP_Text itemCount = CreateTMPText(gridItem.transform, "ItemCount", new Vector2(0f, -40f), "x0");
            itemCount.alignment = TextAlignmentOptions.Center;

            ItemInventoryGridItem gridItemUi = gridItem.AddComponent<ItemInventoryGridItem>();
            SetPrivateField(gridItemUi, "iconImage", itemIcon);
            SetPrivateField(gridItemUi, "nameText", itemName);
            SetPrivateField(gridItemUi, "countText", itemCount);
            SetPrivateField(gridItemUi, "button", itemButton);
            Image itemBorder = CreateRadialImage(gridItem.transform, "ItemBorder", Vector2.zero, new Vector2(96f, 96f), false);
            itemBorder.color = new Color(1f, 1f, 1f, 0.6f);
            Image itemRarityIcon = CreateRadialImage(gridItem.transform, "ItemRarityIcon", new Vector2(36f, -36f), new Vector2(20f, 20f), false);
            SetPrivateField(gridItemUi, "borderImage", itemBorder);
            SetPrivateField(gridItemUi, "rarityIconImage", itemRarityIcon);
            GameObject particleObj = new GameObject("RarityParticles");
            particleObj.transform.SetParent(gridItem.transform, false);
            ParticleSystem particles = particleObj.AddComponent<ParticleSystem>();
            SetPrivateField(gridItemUi, "rarityParticles", particles);
            gridItem.gameObject.SetActive(false);
            refs.inventoryGridItemPrefab = gridItemUi;
            // WireFromRefs가 읽는 필드 중 유일하게 TMP 짝이 없는 참조다. 여기서 안 채우면
            // 프리팹 경로로 뜰 때 팔레트가 null이 되어 Resources/ItemRarityPalette.asset이
            // 무시되고 하드코딩 폴백 색으로 돌아간다(절차 생성 경로는 Bootstrap이 주입한다).
            refs.itemRarityPalette = Resources.Load<InsectGame.Data.ItemRarityPalette>("ItemRarityPalette");

            GameObject shopPanel = CreatePanel(root.transform, "ShopPanel", new Vector2(0f, -2300f), new Vector2(360f, 200f));
            Button buy1 = CreateButton(shopPanel.transform, "BuyButton1", new Vector2(0f, -40f), "구매1");
            Button buy2 = CreateButton(shopPanel.transform, "BuyButton2", new Vector2(0f, -100f), "구매2");
            Button buy3 = CreateButton(shopPanel.transform, "BuyButton3", new Vector2(0f, -160f), "구매3");
            refs.shopBuyButtons = new[] { buy1, buy2, buy3 };
            refs.shopBuyLabels = new[]
            {
                buy1.GetComponentInChildren<TextMeshProUGUI>(),
                buy2.GetComponentInChildren<TextMeshProUGUI>(),
                buy3.GetComponentInChildren<TextMeshProUGUI>()
            };
            TMP_Text shopResult = CreateTMPText(shopPanel.transform, "ShopResult", new Vector2(0f, -190f), "");
            shopResult.alignment = TextAlignmentOptions.Center;
            refs.shopResultText = shopResult;
            refs.shopPriceLabels = refs.shopBuyLabels;
            Toggle coinsToggle = CreateToggle(shopPanel.transform, "CoinsToggle", new Vector2(-60f, -10f), new Vector2(120f, 24f), "코인");
            Toggle gemsToggle = CreateToggle(shopPanel.transform, "GemsToggle", new Vector2(60f, -10f), new Vector2(120f, 24f), "보석");
            refs.shopCoinsToggle = coinsToggle;
            refs.shopGemsToggle = gemsToggle;
            TMP_Text paymentLabel = CreateTMPText(shopPanel.transform, "PaymentLabel", new Vector2(0f, -230f), "결제: 보석 또는 코인");
            paymentLabel.alignment = TextAlignmentOptions.Center;
            refs.shopPaymentLabel = paymentLabel;

            GameObject tuningPanel = CreatePanel(root.transform, "RarityTuningPanel", new Vector2(0f, -2540f), new Vector2(360f, 260f));
            refs.commonPulseSlider = CreateSlider(tuningPanel.transform, "CommonSlider", new Vector2(0f, -30f), new Vector2(240f, 16f));
            refs.uncommonPulseSlider = CreateSlider(tuningPanel.transform, "UncommonSlider", new Vector2(0f, -70f), new Vector2(240f, 16f));
            refs.rarePulseSlider = CreateSlider(tuningPanel.transform, "RareSlider", new Vector2(0f, -110f), new Vector2(240f, 16f));
            refs.epicPulseSlider = CreateSlider(tuningPanel.transform, "EpicSlider", new Vector2(0f, -150f), new Vector2(240f, 16f));
            refs.legendaryPulseSlider = CreateSlider(tuningPanel.transform, "LegendarySlider", new Vector2(0f, -190f), new Vector2(240f, 16f));
            refs.commonPulseLabel = CreateTMPText(tuningPanel.transform, "CommonLabel", new Vector2(0f, -10f), "Common 0.05");
            refs.uncommonPulseLabel = CreateTMPText(tuningPanel.transform, "UncommonLabel", new Vector2(0f, -50f), "Uncommon 0.08");
            refs.rarePulseLabel = CreateTMPText(tuningPanel.transform, "RareLabel", new Vector2(0f, -90f), "Rare 0.12");
            refs.epicPulseLabel = CreateTMPText(tuningPanel.transform, "EpicLabel", new Vector2(0f, -130f), "Epic 0.18");
            refs.legendaryPulseLabel = CreateTMPText(tuningPanel.transform, "LegendaryLabel", new Vector2(0f, -170f), "Legendary 0.25");

            TMP_Text gemsText = CreateTMPText(root.transform, "GemsText", new Vector2(30f, -240f), "보석 0");
            TMP_Text coinsText = CreateTMPText(root.transform, "CoinsText", new Vector2(30f, -280f), "코인 0");
            refs.gemsText = gemsText;
            refs.coinsText = coinsText;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            if (prefab != null)
            {
                PlayUIConfig config = AssetDatabase.LoadAssetAtPath<PlayUIConfig>(ConfigPath);
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<PlayUIConfig>();
                    AssetDatabase.CreateAsset(config, ConfigPath);
                }

                config.playHudPrefab = prefab;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = prefab;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        private static TMP_Text CreateTMPText(Transform parent, string name, Vector2 anchoredPos, string content)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            TMP_Text text = obj.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = 28;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(360f, 60f);
            return text;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.4f);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return panel;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 anchoredPos, string label)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Image image = obj.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            Button button = obj.AddComponent<Button>();
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(200f, 60f);

            TMP_Text text = CreateTMPText(obj.transform, "Label", Vector2.zero, label);
            text.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            Slider slider = obj.AddComponent<Slider>();
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(obj.transform, false);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.8f, 0.3f, 0.9f);
            slider.fillRect = fillImage.rectTransform;
            slider.targetGraphic = fillImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            return slider;
        }

        private static DexListItemUI CreateListItemPrefab(RectTransform parent)
        {
            GameObject item = new GameObject("DexListItemPrefab");
            item.transform.SetParent(parent, false);
            Image bg = item.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(360f, 60f);

            Button button = item.AddComponent<Button>();
            TMP_Text nameText = CreateTMPText(item.transform, "Name", new Vector2(-60f, -10f), "???");
            TMP_Text statusText = CreateTMPText(item.transform, "Status", new Vector2(120f, -10f), "미발견");
            statusText.alignment = TextAlignmentOptions.Right;

            DexListItemUI ui = item.AddComponent<DexListItemUI>();
            SetPrivateField(ui, "nameTextTmp", nameText);
            SetPrivateField(ui, "statusTextTmp", statusText);
            SetPrivateField(ui, "selectButton", button);
            return ui;
        }

        private static void SetPrivateField(object target, string fieldName, Object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private static Image CreateRadialImage(Transform parent, string name, Vector2 anchoredPos, Vector2 size, bool filled)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Image image = obj.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, filled ? 0.6f : 1f);
            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillAmount = 0f;
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return image;
        }

        private static Toggle CreateToggle(Transform parent, string name, Vector2 anchoredPos, Vector2 size, string label)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            Toggle toggle = obj.AddComponent<Toggle>();
            Image bg = obj.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.4f);

            TMP_Text text = CreateTMPText(obj.transform, "Label", Vector2.zero, label);
            text.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return toggle;
        }
        private static TMP_Dropdown CreateDropdown(Transform parent, string name, Vector2 anchoredPos, Vector2 size, string[] options)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            TMP_Dropdown dropdown = obj.AddComponent<TMP_Dropdown>();
            TMP_Text label = CreateTMPText(obj.transform, "Label", Vector2.zero, options != null && options.Length > 0 ? options[0] : "Option");
            label.alignment = TextAlignmentOptions.Center;
            dropdown.captionText = label;
            dropdown.options.Clear();
            if (options != null)
            {
                foreach (string option in options)
                {
                    dropdown.options.Add(new TMP_Dropdown.OptionData(option));
                }
            }

            return dropdown;
        }
    }
}
