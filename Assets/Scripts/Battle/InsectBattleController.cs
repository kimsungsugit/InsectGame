using System;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;
using System.Collections.Generic;

namespace InsectGame.Battle
{
    public class InsectBattleController : MonoBehaviour
    {
        [SerializeField] private PlayerInsectCollection playerCollection;
        [SerializeField] private PlayerCandyInventory candyInventory;
        [SerializeField] private PlayerProgressController playerProgress;
        [SerializeField] private PlayerItemInventory itemInventory;
        [SerializeField] private Dex.DexController dexController;

        public event Action<bool> BattleEnded;
        public event Action<InsectBattleStats, InsectBattleStats> BattleUpdated;
        public event Action PlayerFainted;

        private InsectBattleStats playerStats;
        private InsectBattleStats enemyStats;
        private InsectEntity enemyEntity;
        private InsectSkill[] playerOverrideSkills;
        private int[] playerCooldowns;
        private int enemyCooldown;
        private readonly List<ActiveEffect> effects = new List<ActiveEffect>();
        private int lastCandyReward;
        private int lastExpReward;
        private string lastItemId;
        private int lastItemCount;
        private bool lastPlayerWon;

        public void StartBattle(InsectData playerInsect, int playerLevel, InsectEntity enemy, Action<InsectBattleStats, InsectBattleStats> onStarted = null, InsectSkill[] equippedSkills = null, Core.PlayerInsectData playerPid = null)
        {
            if (playerInsect == null || enemy == null || enemy.Data == null)
            {
                return;
            }

            playerStats = new InsectBattleStats(playerInsect, playerLevel, playerPid);
            enemyStats = new InsectBattleStats(enemy.Data, enemy.Level);
            enemyEntity = enemy;
            playerOverrideSkills = ResolvePlayerSkills(playerInsect, equippedSkills, playerPid);
            int skillCount = playerOverrideSkills != null ? playerOverrideSkills.Length : (playerInsect.skills != null ? playerInsect.skills.Length : 0);
            playerCooldowns = new int[skillCount];
            enemyCooldown = 0;
            lastCandyReward = 0;
            lastExpReward = 0;
            lastItemId = string.Empty;
            lastItemCount = 0;
            lastPlayerWon = false;
            onStarted?.Invoke(playerStats, enemyStats);
            BattleUpdated?.Invoke(playerStats, enemyStats);
        }

        public void UseSkill(int skillIndex)
        {
            if (playerStats == null || enemyStats == null)
            {
                return;
            }

            if (!CanUseSkill(skillIndex))
            {
                return;
            }

            InsectSkill[] skills = GetPlayerSkills();
            InsectSkill skill = skills != null && skillIndex < skills.Length ? skills[skillIndex] : GetSkill(playerStats.Data, skillIndex);
            ApplySkill(playerStats, enemyStats, skill, true);

            if (skill != null)
            {
                playerCooldowns[skillIndex] = skill.cooldownTurns;
            }

            if (enemyStats.CurrentHp > 0)
            {
                UseEnemyTurn();
            }

            TickEffects();
            TickCooldowns();
            BattleUpdated?.Invoke(playerStats, enemyStats);
            CheckEnd();
        }

        public void UseBasicAttack()
        {
            if (playerStats == null || enemyStats == null)
            {
                return;
            }

            int damage = Mathf.Max(1, Mathf.RoundToInt(playerStats.Attack * 0.7f));
            enemyStats.ApplyDamage(damage, playerStats.Attack, enemyStats.Defense);

            if (enemyStats.CurrentHp > 0)
            {
                UseEnemyTurn();
            }

            TickEffects();
            TickCooldowns();
            BattleUpdated?.Invoke(playerStats, enemyStats);
            CheckEnd();
        }

        public bool TryEscape()
        {
            if (playerStats == null || enemyStats == null)
            {
                return false;
            }

            int levelDiff = playerStats.Level - enemyStats.Level;
            float escapeChance = Mathf.Clamp(0.5f + levelDiff * 0.05f, 0.1f, 0.9f);
            bool escaped = UnityEngine.Random.value < escapeChance;

            if (escaped)
            {
                BattleEnded?.Invoke(false);
                return true;
            }

            UseEnemyTurn();
            TickEffects();
            TickCooldowns();
            BattleUpdated?.Invoke(playerStats, enemyStats);
            CheckEnd();
            return false;
        }

        public bool CanUseSkill(int skillIndex)
        {
            if (playerStats == null || playerStats.Data == null) return false;

            InsectSkill[] skills = GetPlayerSkills();
            if (skills == null || skills.Length == 0) return false;
            if (skillIndex < 0 || skillIndex >= skills.Length) return false;
            if (skills[skillIndex] == null) return false;
            if (playerCooldowns != null && skillIndex < playerCooldowns.Length && playerCooldowns[skillIndex] > 0) return false;

            return true;
        }

        public InsectSkill[] GetPlayerSkills()
        {
            if (playerOverrideSkills != null) return playerOverrideSkills;
            if (playerStats != null && playerStats.PlayerData != null && playerCollection != null)
            {
                InsectSkill[] resolved = playerCollection.GetEquippedSkills(playerStats.PlayerData);
                if (resolved != null && resolved.Length > 0)
                {
                    return resolved;
                }
            }
            return playerStats != null && playerStats.Data != null ? playerStats.Data.skills : null;
        }

        private InsectSkill GetPrimarySkill(InsectData data)
        {
            if (data == null)
            {
                return null;
            }

            if (data.learnset != null && data.learnset.Length > 0)
            {
                int currentLevel = enemyStats != null ? enemyStats.Level : data.minLevel;
                InsectSkill best = null;
                foreach (InsectLearnableSkill learnable in data.learnset)
                {
                    if (learnable == null || learnable.skill == null || learnable.learnLevel > currentLevel)
                    {
                        continue;
                    }

                    best = learnable.skill;
                }

                if (best != null)
                {
                    return best;
                }
            }

            if (data.skills == null || data.skills.Length == 0)
            {
                return null;
            }

            return data.skills[0];
        }

        private InsectSkill GetSkill(InsectData data, int index)
        {
            if (data == null || data.skills == null || data.skills.Length == 0)
            {
                return null;
            }

            if (index < 0 || index >= data.skills.Length)
            {
                return data.skills[0];
            }

            return data.skills[index];
        }

        private void ApplySkill(InsectBattleStats attacker, InsectBattleStats defender, InsectSkill skill, bool isPlayer)
        {
            if (skill == null)
            {
                int damage = GetDamage(attacker, 10);
                defender.ApplyDamage(damage, attacker.Attack, defender.Defense);
                return;
            }

            switch (skill.effectType)
            {
                case SkillEffectType.BuffAttack:
                    AddEffect(isPlayer, skill.effectValue, skill.effectDurationTurns);
                    break;
                case SkillEffectType.DebuffAttack:
                    AddEffect(!isPlayer, -skill.effectValue, skill.effectDurationTurns);
                    break;
                default:
                    int baseDamage = skill.power;
                    int damage = GetDamage(attacker, baseDamage);
                    defender.ApplyDamage(damage, attacker.Attack, defender.Defense);
                    break;
            }
        }

        private int GetDamage(InsectBattleStats attacker, int baseDamage)
        {
            float multiplier = Mathf.Clamp(1f + attacker.AttackBonus, 0.3f, 3f);
            int damage = Mathf.RoundToInt((baseDamage + attacker.Level * 2) * multiplier);
            return Mathf.Max(1, damage);
        }

        private void UseEnemyTurn()
        {
            InsectSkill enemySkill = GetPrimarySkill(enemyStats.Data);
            if (enemyCooldown > 0)
            {
                enemySkill = null;
            }

            ApplySkill(enemyStats, playerStats, enemySkill, false);
            if (enemySkill != null)
            {
                enemyCooldown = enemySkill.cooldownTurns;
            }
        }

        private void TickCooldowns()
        {
            if (playerCooldowns != null)
            {
                for (int i = 0; i < playerCooldowns.Length; i++)
                {
                    if (playerCooldowns[i] > 0)
                    {
                        playerCooldowns[i]--;
                    }
                }
            }

            if (enemyCooldown > 0)
            {
                enemyCooldown--;
            }
        }

        private void AddEffect(bool targetIsPlayer, float value, int duration)
        {
            if (duration <= 0)
            {
                return;
            }

            effects.Add(new ActiveEffect
            {
                targetIsPlayer = targetIsPlayer,
                value = value,
                remainingTurns = duration
            });
            RecalculateBonuses();
        }

        private void TickEffects()
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                ActiveEffect effect = effects[i];
                effect.remainingTurns--;
                if (effect.remainingTurns <= 0)
                {
                    effects.RemoveAt(i);
                }
                else
                {
                    effects[i] = effect;
                }
            }

            RecalculateBonuses();
        }

        private void RecalculateBonuses()
        {
            float playerBonus = 0f;
            float enemyBonus = 0f;
            foreach (ActiveEffect effect in effects)
            {
                if (effect.targetIsPlayer)
                {
                    playerBonus += effect.value;
                }
                else
                {
                    enemyBonus += effect.value;
                }
            }

            if (playerStats != null)
            {
                float outfitAtk = outfitBonus != null ? outfitBonus.GetAtkBonus() : 0f;
                playerStats.AttackBonus = playerBonus + outfitAtk;
            }

            if (enemyStats != null)
            {
                enemyStats.AttackBonus = enemyBonus;
            }
        }

        private void CheckEnd()
        {
            if (playerStats == null || enemyStats == null)
            {
                return;
            }

            bool playerWon = enemyStats.CurrentHp <= 0 && playerStats.CurrentHp > 0;
            if (playerWon && enemyEntity != null)
            {
                int candy = InsectRewardCalculator.GetCandyReward(enemyEntity.Data);
                int exp = InsectRewardCalculator.GetExpReward(enemyEntity.Data);
                int itemCount = InsectRewardCalculator.GetItemRewardCount(enemyEntity.Data);
                string itemId = enemyEntity.Data.itemRewardId;

                candyInventory?.AddCandy(candy);
                playerProgress?.GainXp(exp);
                if (!string.IsNullOrEmpty(itemId) && itemCount > 0)
                {
                    itemInventory?.AddItem(itemId, itemCount);
                }

                bool entityShiny = enemyEntity != null && enemyEntity.IsShiny;
                playerCollection?.AddCapturedInsect(enemyEntity.Data.insectId, enemyEntity.Level, entityShiny);

                if (dexController != null)
                {
                    dexController.RegisterEncounter(enemyEntity.Data.insectId);
                    dexController.RegisterCapture(enemyEntity.Data.insectId);
                }

                lastCandyReward = candy;
                lastExpReward = exp;
                lastItemId = itemId;
                lastItemCount = itemCount;

                enemyEntity.Despawn();
            }

            if (enemyStats.CurrentHp <= 0)
            {
                lastPlayerWon = playerWon;
                BattleEnded?.Invoke(playerWon);
            }
            else if (playerStats.CurrentHp <= 0)
            {
                if (PlayerFainted != null)
                    PlayerFainted.Invoke();
                else
                    BattleEnded?.Invoke(false);
            }
        }

        public void AutoWire(PlayerInsectCollection collection, PlayerCandyInventory candies, PlayerProgressController progress, PlayerItemInventory items)
        {
            if (playerCollection == null)
            {
                playerCollection = collection;
            }

            if (candyInventory == null)
            {
                candyInventory = candies;
            }

            if (playerProgress == null)
            {
                playerProgress = progress;
            }

            if (itemInventory == null)
            {
                itemInventory = items;
            }
        }

        public void AutoWire(Dex.DexController dex)
        {
            if (dexController == null) dexController = dex;
        }

        private OutfitBonusProvider outfitBonus;

        public void AutoWire(OutfitBonusProvider bonus)
        {
            if (outfitBonus == null) outfitBonus = bonus;
        }

        public void SwapPlayerInsect(InsectData newInsect, int newLevel, InsectSkill[] equippedSkills = null, Core.PlayerInsectData playerPid = null)
        {
            if (newInsect == null || enemyStats == null) return;

            playerStats = new InsectBattleStats(newInsect, newLevel, playerPid);
            playerOverrideSkills = ResolvePlayerSkills(newInsect, equippedSkills, playerPid);
            int skillCount = playerOverrideSkills != null ? playerOverrideSkills.Length : (newInsect.skills != null ? newInsect.skills.Length : 0);
            playerCooldowns = new int[skillCount];
            effects.Clear();
            RecalculateBonuses();
            BattleUpdated?.Invoke(playerStats, enemyStats);
        }

        private InsectSkill[] ResolvePlayerSkills(InsectData insect, InsectSkill[] equippedSkills, Core.PlayerInsectData playerPid)
        {
            if (equippedSkills != null && equippedSkills.Length > 0)
            {
                return equippedSkills;
            }

            if (playerPid != null && playerCollection != null)
            {
                InsectSkill[] resolved = playerCollection.GetEquippedSkills(playerPid);
                if (resolved != null && resolved.Length > 0)
                {
                    return resolved;
                }
            }

            return insect != null ? insect.skills : null;
        }

        public bool IsBattleInProgress()
        {
            return playerStats != null && enemyStats != null;
        }

        public InsectEntity GetEnemyEntity() => enemyEntity;

        public int[] GetPlayerCooldowns()
        {
            if (playerCooldowns == null)
            {
                return Array.Empty<int>();
            }

            int[] copy = new int[playerCooldowns.Length];
            Array.Copy(playerCooldowns, copy, playerCooldowns.Length);
            return copy;
        }

        public EffectSnapshot[] GetActiveEffects()
        {
            EffectSnapshot[] snapshots = new EffectSnapshot[effects.Count];
            for (int i = 0; i < effects.Count; i++)
            {
                ActiveEffect effect = effects[i];
                snapshots[i] = new EffectSnapshot
                {
                    targetIsPlayer = effect.targetIsPlayer,
                    value = effect.value,
                    remainingTurns = effect.remainingTurns
                };
            }

            return snapshots;
        }

        public int GetLastCandyReward()
        {
            return lastCandyReward;
        }

        public bool GetLastPlayerWon()
        {
            return lastPlayerWon;
        }

        public int GetLastExpReward()
        {
            return lastExpReward;
        }

        public string GetLastItemId()
        {
            return lastItemId;
        }

        public int GetLastItemCount()
        {
            return lastItemCount;
        }

        public struct EffectSnapshot
        {
            public bool targetIsPlayer;
            public float value;
            public int remainingTurns;
        }

        private struct ActiveEffect
        {
            public bool targetIsPlayer;
            public float value;
            public int remainingTurns;
        }
    }
}
