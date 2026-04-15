using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InsectGame.Battle
{
    public class InsectBattleUIController : MonoBehaviour
    {
        [SerializeField] private InsectBattleController battleController;
        [SerializeField] private PlayerInsectCollection playerCollection;
        [SerializeField] private Slider playerHpBar;
        [SerializeField] private Slider enemyHpBar;
        [SerializeField] private TMP_Text playerHpText;
        [SerializeField] private TMP_Text enemyHpText;
        [SerializeField] private Button[] skillButtons;
        [SerializeField] private TMP_Text[] skillLabels;
        [SerializeField] private TMP_Text[] skillCooldownLabels;
        [SerializeField] private Image[] skillIconImages;
        [SerializeField] private Image[] skillCooldownImages;
        [SerializeField] private Image[] skillBorderImages;
        [Header("Rarity Colors")]
        [SerializeField] private Color commonColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Color uncommonColor = new Color(0.5f, 0.9f, 0.5f, 1f);
        [SerializeField] private Color rareColor = new Color(0.4f, 0.7f, 1f, 1f);
        [SerializeField] private Color epicColor = new Color(0.8f, 0.4f, 1f, 1f);
        [SerializeField] private Color legendaryColor = new Color(1f, 0.75f, 0.2f, 1f);
        [SerializeField] private string skillIconResourceRoot = "SkillIcons";
        [SerializeField] private Text playerHpTextLegacy;
        [SerializeField] private Text enemyHpTextLegacy;
        [SerializeField] private Text[] skillCooldownLegacy;
        [SerializeField] private Button startBattleButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Text resultTextLegacy;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Text rewardTextLegacy;
        [Header("Effect UI")]
        [SerializeField] private TMP_Text playerEffectText;
        [SerializeField] private TMP_Text enemyEffectText;
        [SerializeField] private Text playerEffectTextLegacy;
        [SerializeField] private Text enemyEffectTextLegacy;
        [SerializeField] private string winMessage = "승리!";
        [SerializeField] private string loseMessage = "패배...";
        [SerializeField] private float enemySearchRadius = 12f;

        private InsectBattleStats playerStats;
        private InsectBattleStats enemyStats;
        private InsectEntity currentEnemy;
        private bool forcedHidden;

        public void ForceHidePanel()
        {
            forcedHidden = true;
            if (panelRoot != null) panelRoot.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);
        }

        private void Start()
        {
            if (startBattleButton != null)
            {
                startBattleButton.onClick.RemoveAllListeners();
                startBattleButton.onClick.AddListener(TryStartBattle);
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (battleController != null)
            {
                battleController.BattleUpdated += HandleBattleUpdated;
                battleController.BattleEnded += HandleBattleEnded;
            }
        }

        private void OnDisable()
        {
            if (battleController != null)
            {
                battleController.BattleUpdated -= HandleBattleUpdated;
                battleController.BattleEnded -= HandleBattleEnded;
            }
        }

        public void TryStartBattle()
        {
            if (playerCollection == null || battleController == null)
            {
                return;
            }

            if (!playerCollection.TryGetAnyOwned(out PlayerInsectData playerData))
            {
                return;
            }

            InsectData playerInsect = playerCollection.GetInsectData(playerData.insectId);
            if (playerInsect == null)
            {
                return;
            }

            currentEnemy = FindNearestEnemy();
            if (currentEnemy == null)
            {
                return;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            InsectSkill[] equippedSkills = playerCollection.GetEquippedSkills(playerData);
            battleController.StartBattle(playerInsect, playerData.level, currentEnemy, OnBattleStart, equippedSkills, playerData);
        }

        private void OnBattleStart(InsectBattleStats player, InsectBattleStats enemy)
        {
            playerStats = player;
            enemyStats = enemy;
            RefreshSkillButtons();
            UpdateHpUI();
            HideResult();
        }

        private void RefreshSkillButtons()
        {
            if (skillButtons == null)
            {
                return;
            }

            InsectData playerInsect = playerStats != null ? playerStats.Data : null;
            InsectSkill[] activeSkills = battleController != null ? battleController.GetPlayerSkills() : playerInsect != null ? playerInsect.skills : null;

            for (int i = 0; i < skillButtons.Length; i++)
            {
                Button button = skillButtons[i];
                if (button == null)
                {
                    continue;
                }

                int index = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => battleController.UseSkill(index));

                bool hasSkill = activeSkills != null && index < activeSkills.Length && activeSkills[index] != null;
                button.interactable = hasSkill;

                if (skillLabels != null && i < skillLabels.Length && skillLabels[i] != null)
                {
                    skillLabels[i].text = hasSkill ? activeSkills[index].displayName : "-";
                }

                if (skillIconImages != null && i < skillIconImages.Length && skillIconImages[i] != null)
                {
                    Sprite icon = hasSkill ? ResolveSkillIcon(activeSkills[index]) : null;
                    skillIconImages[i].sprite = icon;
                    skillIconImages[i].enabled = icon != null;
                }

                if (skillBorderImages != null && i < skillBorderImages.Length && skillBorderImages[i] != null)
                {
                    skillBorderImages[i].color = playerInsect != null ? GetRarityColor(playerInsect.rarity) : commonColor;
                    skillBorderImages[i].enabled = true;
                }
            }

            UpdateCooldownUI();
        }

        private void HandleBattleUpdated(InsectBattleStats player, InsectBattleStats enemy)
        {
            bool wasNull = playerStats == null;
            playerStats = player;
            enemyStats = enemy;

            if (forcedHidden)
            {
                if (panelRoot != null) panelRoot.SetActive(false);
                return;
            }

            if (wasNull && player != null)
            {
                if (panelRoot != null) panelRoot.SetActive(true);
                RefreshSkillButtons();
                HideResult();
            }

            UpdateHpUI();
            UpdateCooldownUI();
            UpdateEffectUI();
        }

        private void HandleBattleEnded(bool playerWon)
        {
            forcedHidden = false;

            if (panelRoot != null) panelRoot.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);

            playerStats = null;
            enemyStats = null;
        }

        private void UpdateHpUI()
        {
            if (playerStats == null || enemyStats == null)
            {
                return;
            }

            if (playerHpBar != null)
            {
                playerHpBar.maxValue = playerStats.MaxHp;
                playerHpBar.value = playerStats.CurrentHp;
            }

            if (enemyHpBar != null)
            {
                enemyHpBar.maxValue = enemyStats.MaxHp;
                enemyHpBar.value = enemyStats.CurrentHp;
            }

            if (playerHpText != null)
            {
                playerHpText.text = $"{playerStats.CurrentHp}/{playerStats.MaxHp}";
            }

            if (enemyHpText != null)
            {
                enemyHpText.text = $"{enemyStats.CurrentHp}/{enemyStats.MaxHp}";
            }

            if (playerHpTextLegacy != null)
            {
                playerHpTextLegacy.text = $"{playerStats.CurrentHp}/{playerStats.MaxHp}";
            }

            if (enemyHpTextLegacy != null)
            {
                enemyHpTextLegacy.text = $"{enemyStats.CurrentHp}/{enemyStats.MaxHp}";
            }
        }

        private void UpdateCooldownUI()
        {
            if (battleController == null || skillCooldownLabels == null)
            {
                return;
            }

            int[] cooldowns = battleController.GetPlayerCooldowns();
            for (int i = 0; i < skillCooldownLabels.Length; i++)
            {
                TMP_Text label = skillCooldownLabels[i];
                if (label == null)
                {
                    continue;
                }

                int value = i < cooldowns.Length ? cooldowns[i] : 0;
                label.text = value > 0 ? $"CD {value}" : string.Empty;
            }

            if (skillCooldownLegacy != null)
            {
                for (int i = 0; i < skillCooldownLegacy.Length; i++)
                {
                    Text label = skillCooldownLegacy[i];
                    if (label == null)
                    {
                        continue;
                    }

                    int value = i < cooldowns.Length ? cooldowns[i] : 0;
                    label.text = value > 0 ? $"CD {value}" : string.Empty;
                }
            }

            if (skillCooldownImages != null)
            {
                for (int i = 0; i < skillCooldownImages.Length; i++)
                {
                    Image img = skillCooldownImages[i];
                    if (img == null)
                    {
                        continue;
                    }

                    int value = i < cooldowns.Length ? cooldowns[i] : 0;
                    int max = GetSkillCooldownMax(i);
                    img.fillAmount = max > 0 ? Mathf.Clamp01((float)value / max) : 0f;
                    img.enabled = value > 0;
                }
            }
        }

        private int GetSkillCooldownMax(int index)
        {
            if (playerStats == null || playerStats.Data == null || playerStats.Data.skills == null || index >= playerStats.Data.skills.Length)
            {
                return 0;
            }

            return Mathf.Max(1, playerStats.Data.skills[index].cooldownTurns);
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
                    return commonColor;
            }
        }

        private Sprite ResolveSkillIcon(InsectSkill skill)
        {
            if (skill == null)
            {
                return null;
            }

            if (skill.icon != null)
            {
                return skill.icon;
            }

            string path = !string.IsNullOrEmpty(skill.iconResourcePath)
                ? skill.iconResourcePath
                : $"{skillIconResourceRoot}/{skill.skillId}";

            return Resources.Load<Sprite>(path);
        }

        private void UpdateEffectUI()
        {
            if (battleController == null)
            {
                return;
            }

            InsectBattleController.EffectSnapshot[] effects = battleController.GetActiveEffects();
            string playerText = "";
            string enemyText = "";
            foreach (InsectBattleController.EffectSnapshot effect in effects)
            {
                string label = effect.value >= 0f ? $"ATK+ {effect.remainingTurns}" : $"ATK- {effect.remainingTurns}";
                if (effect.targetIsPlayer)
                {
                    playerText = string.IsNullOrEmpty(playerText) ? label : $"{playerText} / {label}";
                }
                else
                {
                    enemyText = string.IsNullOrEmpty(enemyText) ? label : $"{enemyText} / {label}";
                }
            }

            if (playerEffectText != null)
            {
                playerEffectText.text = playerText;
            }
            if (enemyEffectText != null)
            {
                enemyEffectText.text = enemyText;
            }
            if (playerEffectTextLegacy != null)
            {
                playerEffectTextLegacy.text = playerText;
            }
            if (enemyEffectTextLegacy != null)
            {
                enemyEffectTextLegacy.text = enemyText;
            }
        }

        private void ShowResult(bool playerWon)
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }

            if (resultText != null)
            {
                resultText.text = playerWon ? winMessage : loseMessage;
            }

            if (resultTextLegacy != null)
            {
                resultTextLegacy.text = playerWon ? winMessage : loseMessage;
            }

            int candy = battleController != null ? battleController.GetLastCandyReward() : 0;
            int exp = battleController != null ? battleController.GetLastExpReward() : 0;
            string itemId = battleController != null ? battleController.GetLastItemId() : string.Empty;
            int itemCount = battleController != null ? battleController.GetLastItemCount() : 0;
            string itemText = string.IsNullOrEmpty(itemId) || itemCount <= 0 ? "아이템 없음" : $"아이템 {itemId} x{itemCount}";
            string reward = $"보상: 사탕 {candy} / 경험치 {exp} / {itemText}";
            if (rewardText != null)
            {
                rewardText.text = reward;
            }
            if (rewardTextLegacy != null)
            {
                rewardTextLegacy.text = reward;
            }
        }

        private void HideResult()
        {
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
        }

        private InsectEntity FindNearestEnemy()
        {
            InsectEntity[] entities = FindObjectsByType<InsectEntity>(FindObjectsSortMode.None);
            if (entities == null || entities.Length == 0)
            {
                return null;
            }

            GameObject player = GameObject.Find("Player");
            Vector3 origin = player != null ? player.transform.position : transform.position;
            float bestDistance = float.MaxValue;
            InsectEntity best = null;

            foreach (InsectEntity entity in entities)
            {
                if (entity == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(origin, entity.transform.position);
                if (distance < bestDistance && distance <= enemySearchRadius)
                {
                    bestDistance = distance;
                    best = entity;
                }
            }

            return best;
        }

        public void AutoWire(InsectBattleController controller, PlayerInsectCollection collection)
        {
            if (battleController != null && battleController != controller)
            {
                battleController.BattleUpdated -= HandleBattleUpdated;
                battleController.BattleEnded -= HandleBattleEnded;
            }

            if (battleController == null || battleController != controller)
            {
                battleController = controller;
                if (battleController != null)
                {
                    battleController.BattleUpdated -= HandleBattleUpdated;
                    battleController.BattleEnded -= HandleBattleEnded;
                    battleController.BattleUpdated += HandleBattleUpdated;
                    battleController.BattleEnded += HandleBattleEnded;
                }
            }

            if (playerCollection == null)
            {
                playerCollection = collection;
            }
        }
    }
}
