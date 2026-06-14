using System;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;
using System.Collections;
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
        [SerializeField] private BattleArenaController arena;

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
        // 종료 가드 — 승리/패배 처리(보상 지급·BattleEnded)를 1회로 제한.
        // 없으면 종료 후 액션이 한 번 더 들어올 때(rapid tap/입력 큐) 보상 이중 지급.
        private bool battleEnded;
        private string lastItemId;
        private int lastItemCount;
        private bool lastPlayerWon;

        private BattleArenaController Arena => arena;

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
            battleEnded = false;
            onStarted?.Invoke(playerStats, enemyStats);
            BattleUpdated?.Invoke(playerStats, enemyStats);
        }

        public void UseSkill(int skillIndex)
        {
            if (playerStats == null || enemyStats == null)
            {
                return;
            }
            if (battleEnded) return; // 종료 후 액션 차단

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
            if (battleEnded) return; // 종료 후 액션 차단

            int damage = Mathf.Max(1, Mathf.RoundToInt(playerStats.Attack * 0.7f));
            enemyStats.ApplyDamage(damage, playerStats.Attack, enemyStats.Defense);
            TryPlayHitFlash(false);

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
            if (battleEnded) return false; // 종료 후 액션 차단

            int levelDiff = playerStats.Level - enemyStats.Level;
            float escapeChance = Mathf.Clamp(0.5f + levelDiff * 0.05f, 0.1f, 0.9f);
            bool escaped = UnityEngine.Random.value < escapeChance;

            if (escaped)
            {
                // 도주 성공 시에도 enemyEntity Despawn — 사용자 의도("전투 끝나면 사라져야").
                // 옛은 도주 후 곤충이 필드에 잔존했고 같은 적이 그대로 다시 만남 가능.
                if (enemyEntity != null) enemyEntity.Despawn();
                battleEnded = true;
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
            // 방어자(맞는 쪽)가 플래시되어야 함. isPlayer는 공격자 기준이므로 반전.
            bool defenderIsPlayer = !isPlayer;

            if (skill == null)
            {
                int damage = GetDamage(attacker, 10);
                defender.ApplyDamage(damage, attacker.Attack, defender.Defense);
                TryPlayHitFlash(defenderIsPlayer);
                return;
            }

            // 스킬 이름 효과 텍스트 (속성 색상)
            if (!string.IsNullOrEmpty(skill.displayName))
            {
                TryPlayEffectText($"{skill.displayName}!", BattleArenaController.GetUIElementColor(skill.element));
            }

            switch (skill.effectType)
            {
                case SkillEffectType.BuffAttack:
                    AddEffect(isPlayer, skill.effectValue, skill.effectDurationTurns);
                    TryPlayEffectText("공격력 상승!", new Color(1f, 0.8f, 0.3f));
                    break;
                case SkillEffectType.DebuffAttack:
                    AddEffect(!isPlayer, -skill.effectValue, skill.effectDurationTurns);
                    TryPlayEffectText("공격력 하락!", new Color(0.6f, 0.4f, 0.9f));
                    break;
                default:
                    int baseDamage = skill.power;
                    int damage = GetDamage(attacker, baseDamage);
                    defender.ApplyDamage(damage, attacker.Attack, defender.Defense);
                    TryPlayHitFlash(defenderIsPlayer);
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
            if (battleEnded) return; // 이미 종료 — 보상/이벤트 중복 차단

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
                if (playerCollection != null)
                    playerCollection.AddCapturedInsect(enemyEntity.Data.insectId, enemyEntity.Level, entityShiny);
                else
                    Debug.LogError("[Battle] playerCollection null — 캡처 보상 손실: " + enemyEntity.Data.insectId);

                if (dexController != null)
                {
                    dexController.RegisterEncounter(enemyEntity.Data.insectId);
                    dexController.RegisterCapture(enemyEntity.Data.insectId);
                }

                lastCandyReward = candy;
                lastExpReward = exp;
                lastItemId = itemId;
                lastItemCount = itemCount;

                TryPlayFaint(false);
                enemyEntity.Despawn();
            }

            if (enemyStats.CurrentHp <= 0)
            {
                battleEnded = true;
                lastPlayerWon = playerWon;
                BattleEnded?.Invoke(playerWon);
            }
            else if (playerStats.CurrentHp <= 0)
            {
                battleEnded = true;
                TryPlayFaint(true);
                TryPlayEffectText("쓰러졌다!", new Color(0.9f, 0.2f, 0.2f));
                // 패배 시에도 enemyEntity Despawn — 옛은 패배 시 곤충이 필드에 잔존했고, 다음
                // 진입 시 같은 곤충 중첩 발동 가능 (사용자 보고: "전투 끝나면 사라져야").
                if (enemyEntity != null) enemyEntity.Despawn();
                // 핸들러 예외 격리 — 한 구독자 예외가 BattleEnded fallback을 차단하지 않게
                bool fainted = false;
                try
                {
                    if (PlayerFainted != null)
                    {
                        PlayerFainted.Invoke();
                        fainted = true;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[InsectBattle] PlayerFainted 핸들러 예외: {e.Message}");
                }
                if (!fainted) BattleEnded?.Invoke(false);
            }
        }

        // ── 액션 헬퍼 (BattleArenaController 시각 코루틴 호출 wrapper) ──

        private void TryPlayHitFlash(bool isPlayerSide)
        {
            if (Arena == null) return;
            GameObject model = isPlayerSide ? Arena.PlayerModel : Arena.EnemyModel;
            if (model != null && model.activeInHierarchy)
            {
                StartCoroutine(Arena.PlayHitFlashCoroutine(model));
            }
        }

        private void TryPlayFaint(bool isPlayerSide)
        {
            if (Arena == null) return;
            GameObject model = isPlayerSide ? Arena.PlayerModel : Arena.EnemyModel;
            if (model != null && model.activeInHierarchy)
            {
                StartCoroutine(Arena.PlayFaintCoroutine(model));
            }
        }

        private void TryPlayEffectText(string text, Color color)
        {
            if (Arena == null || string.IsNullOrEmpty(text)) return;
            Arena.PlayEffectText(text, color);
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

        public void AutoWire(BattleArenaController a)
        {
            if (arena == null) arena = a;
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

            // Faint 후 비활성/페이드된 PlayerModel을 새 곤충으로 재생성
            if (Arena != null) Arena.RebuildPlayerInsect(newInsect, newLevel);

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
