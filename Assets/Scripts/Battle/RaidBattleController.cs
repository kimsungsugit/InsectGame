using System;
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Battle
{
    public class RaidBattleController : MonoBehaviour
    {
        [SerializeField] private PlayerInsectCollection playerCollection;
        [SerializeField] private PlayerCandyInventory candyInventory;
        [SerializeField] private PlayerProgressController playerProgress;
        [SerializeField] private Dex.DexController dexController;
        [SerializeField] private TrainingManager trainingManager;

        public event Action RaidUpdated;
        public event Action<bool> RaidEnded;

        public InsectBattleStats BossStats { get; private set; }
        public InsectBattleStats[] TeamStats { get; private set; }
        public InsectData[] TeamData { get; private set; }
        public PlayerInsectData[] TeamPids { get; private set; }
        public InsectSkill[][] TeamSkills { get; private set; }
        public int[][] TeamCooldowns { get; private set; }
        public InsectEntity BossEntity { get; private set; }

        public int TurnNumber { get; private set; }
        public int ActiveSlot { get; private set; }
        public bool IsActive { get; private set; }
        public string LastActionText { get; private set; }
        public int LastDamageToBoss { get; private set; }
        public int LastDamageToTeam { get; private set; }
        public int LastHitSlot { get; private set; }
        public bool BossUsedAoe { get; private set; }
        public bool PlayerWon { get; private set; }
        public int RewardCandy { get; private set; }
        public int RewardExp { get; private set; }

        public float UniteGauge { get; private set; }
        public const float UniteGaugeMax = GameConstants.Battle.UniteGaugeMax;
        public bool CanUniteAttack => IsActive && UniteGauge >= UniteGaugeMax && AliveCount() >= 2;
        public bool LastWasUnite { get; private set; }
        public int[] UniteSlotDamages { get; private set; }

        private int bossCooldown;

        public void StartRaid(InsectEntity bossEntity,
            InsectData[] teamInsects, int[] teamLevels,
            PlayerInsectData[] teamPids, InsectSkill[][] teamSkills)
        {
            if (bossEntity == null || bossEntity.Data == null) return;

            BossEntity = bossEntity;
            InsectData bd = bossEntity.Data;

            InsectBattleStats rawBoss = new InsectBattleStats(bd, bossEntity.Level);
            BossStats = new RaidBossStats(bd, bossEntity.Level, rawBoss.MaxHp * 5, rawBoss.Attack * 3 / 2, rawBoss.Defense * 13 / 10);

            int count = teamInsects.Length;
            TeamStats = new InsectBattleStats[count];
            TeamData = teamInsects;
            TeamPids = teamPids;
            TeamSkills = teamSkills;
            TeamCooldowns = new int[count][];

            for (int i = 0; i < count; i++)
            {
                TeamStats[i] = new InsectBattleStats(teamInsects[i], teamLevels[i], teamPids != null ? teamPids[i] : null);
                int sc = teamSkills != null && teamSkills[i] != null ? teamSkills[i].Length : 0;
                TeamCooldowns[i] = new int[sc];
            }

            TurnNumber = 0;
            ActiveSlot = FindFirstAlive();
            IsActive = true;
            PlayerWon = false;
            LastActionText = "";
            LastDamageToBoss = 0;
            LastDamageToTeam = 0;
            LastHitSlot = -1;
            BossUsedAoe = false;
            LastWasUnite = false;
            UniteGauge = 0f;
            UniteSlotDamages = null;
            bossCooldown = 0;
            RewardCandy = 0;
            RewardExp = 0;

            RaidUpdated?.Invoke();
        }

        public void SelectSlot(int slot)
        {
            if (!IsActive) return;
            if (slot < 0 || slot >= TeamStats.Length) return;
            if (TeamStats[slot].CurrentHp <= 0) return;
            ActiveSlot = slot;
        }

        public bool CanUseSkill(int skillIndex)
        {
            if (!IsActive || ActiveSlot < 0) return false;
            var skills = TeamSkills != null && ActiveSlot < TeamSkills.Length ? TeamSkills[ActiveSlot] : null;
            if (skills == null || skillIndex < 0 || skillIndex >= skills.Length) return false;
            if (skills[skillIndex] == null) return false;
            var cd = TeamCooldowns[ActiveSlot];
            if (skillIndex < cd.Length && cd[skillIndex] > 0) return false;
            return true;
        }

        public void UseSkill(int skillIndex)
        {
            if (!CanUseSkill(skillIndex)) return;

            TurnNumber++;
            var attacker = TeamStats[ActiveSlot];
            var skill = TeamSkills[ActiveSlot][skillIndex];

            int baseDmg = skill != null ? skill.power : 10;
            float mult = Mathf.Clamp(1f + attacker.AttackBonus, 0.3f, 3f);
            int damage = Mathf.RoundToInt((baseDmg + attacker.Level * 2) * mult);
            damage = Mathf.Max(1, damage);

            if (skill.effectType == SkillEffectType.Damage)
            {
                BossStats.ApplyDamage(damage, attacker.Attack, BossStats.Defense);
                LastDamageToBoss = Mathf.Max(1, Mathf.RoundToInt(damage * (attacker.Attack / (float)Mathf.Max(1, BossStats.Defense))));
                LastActionText = $"{attacker.Data.displayName}의 {skill.displayName}!";
            }
            else if (skill.effectType == SkillEffectType.BuffAttack)
            {
                attacker.AttackBonus += skill.effectValue;
                LastDamageToBoss = 0;
                LastActionText = $"{attacker.Data.displayName}의 {skill.displayName}! ATK UP!";
            }
            else
            {
                BossStats.AttackBonus -= skill.effectValue;
                LastDamageToBoss = 0;
                LastActionText = $"{attacker.Data.displayName}의 {skill.displayName}! Boss ATK DOWN!";
            }

            TeamCooldowns[ActiveSlot][skillIndex] = skill.cooldownTurns;

            if (LastDamageToBoss > 0)
                UniteGauge = Mathf.Min(UniteGauge + 12f + LastDamageToBoss * 0.15f, UniteGaugeMax);

            BossUsedAoe = false;
            LastWasUnite = false;
            UniteSlotDamages = null;
            LastDamageToTeam = 0;
            LastHitSlot = -1;

            if (BossStats.CurrentHp > 0)
                ExecuteBossTurn();

            TickCooldowns();
            RaidUpdated?.Invoke();
            CheckEnd();
        }

        private void ExecuteBossTurn()
        {
            bossCooldown--;
            bool aoe = bossCooldown <= 0;

            InsectData bd = BossStats.Data;
            int bossBaseDmg = 10 + BossStats.Level * 2;
            float bossMult = Mathf.Clamp(1f + BossStats.AttackBonus, 0.3f, 3f);
            int bossDmg = Mathf.Max(1, Mathf.RoundToInt(bossBaseDmg * bossMult));

            if (aoe)
            {
                bossCooldown = 3;
                BossUsedAoe = true;
                int aoeDmg = Mathf.Max(1, bossDmg * 2 / 3);
                for (int i = 0; i < TeamStats.Length; i++)
                {
                    if (TeamStats[i].CurrentHp > 0)
                        TeamStats[i].ApplyDamage(aoeDmg, BossStats.Attack, TeamStats[i].Defense);
                }
                LastDamageToTeam = aoeDmg;
                LastHitSlot = -1;
                LastActionText += $"\n{bd.displayName}의 전체 공격!";
                UniteGauge = Mathf.Min(UniteGauge + 18f, UniteGaugeMax);
            }
            else
            {
                List<int> alive = new List<int>();
                for (int i = 0; i < TeamStats.Length; i++)
                    if (TeamStats[i].CurrentHp > 0) alive.Add(i);

                if (alive.Count > 0)
                {
                    int target = alive[UnityEngine.Random.Range(0, alive.Count)];
                    TeamStats[target].ApplyDamage(bossDmg, BossStats.Attack, TeamStats[target].Defense);
                    LastDamageToTeam = Mathf.Max(1, Mathf.RoundToInt(bossDmg * (BossStats.Attack / (float)Mathf.Max(1, TeamStats[target].Defense))));
                    LastHitSlot = target;
                    LastActionText += $"\n{bd.displayName}이(가) {TeamStats[target].Data.displayName}을(를) 공격!";
                    UniteGauge = Mathf.Min(UniteGauge + 10f, UniteGaugeMax);
                }
            }

            if (TeamStats[ActiveSlot].CurrentHp <= 0)
            {
                int next = FindFirstAlive();
                if (next >= 0) ActiveSlot = next;
            }
        }

        private void TickCooldowns()
        {
            for (int s = 0; s < TeamCooldowns.Length; s++)
            {
                for (int i = 0; i < TeamCooldowns[s].Length; i++)
                    if (TeamCooldowns[s][i] > 0) TeamCooldowns[s][i]--;
            }
        }

        private void CheckEnd()
        {
            if (BossStats.CurrentHp <= 0)
            {
                PlayerWon = true;
                IsActive = false;
                OnRaidVictory();
                RaidEnded?.Invoke(true);
            }
            else if (FindFirstAlive() < 0)
            {
                PlayerWon = false;
                IsActive = false;
                RaidEnded?.Invoke(false);
            }
        }

        private void OnRaidVictory()
        {
            InsectData bd = BossStats.Data;
            int candyBase = InsectRewardCalculator.GetCandyReward(bd);
            int expBase = InsectRewardCalculator.GetExpReward(bd);
            RewardCandy = candyBase * 3;
            RewardExp = expBase * 3;

            if (candyInventory != null) candyInventory.AddCandy(RewardCandy);
            if (playerProgress != null) playerProgress.GainXp(RewardExp);
            if (dexController != null)
            {
                dexController.RegisterEncounter(bd.insectId);
                dexController.RegisterCapture(bd.insectId);
            }
            if (playerCollection != null && BossEntity != null)
            {
                bool bossShiny = BossEntity.IsShiny;
                playerCollection.AddCapturedInsect(bd.insectId, BossEntity.Level, bossShiny);
                BossEntity.Despawn();
            }
        }

        public void UseUniteAttack()
        {
            if (!CanUniteAttack) return;

            TurnNumber++;
            UniteGauge = 0f;
            LastWasUnite = true;
            LastDamageToBoss = 0;
            UniteSlotDamages = new int[TeamStats.Length];

            int totalDmg = 0;
            List<string> names = new List<string>();
            for (int i = 0; i < TeamStats.Length; i++)
            {
                if (TeamStats[i] == null || TeamStats[i].CurrentHp <= 0)
                {
                    UniteSlotDamages[i] = 0;
                    continue;
                }
                var atk = TeamStats[i];
                int baseDmg = 15 + atk.Level * 2;
                float mult = Mathf.Clamp(1f + atk.AttackBonus, 0.3f, 3f);
                int dmg = Mathf.Max(1, Mathf.RoundToInt(baseDmg * mult * 1.5f));
                int actual = Mathf.Max(1, Mathf.RoundToInt(dmg * (atk.Attack / (float)Mathf.Max(1, BossStats.Defense))));
                UniteSlotDamages[i] = actual;
                totalDmg += dmg;
                names.Add(atk.Data.displayName);
                BossStats.ApplyDamage(dmg, atk.Attack, BossStats.Defense);
            }

            LastDamageToBoss = totalDmg;
            LastActionText = $"★ 합체공격! {string.Join(" + ", names)} ★";

            BossUsedAoe = false;
            LastDamageToTeam = 0;
            LastHitSlot = -1;

            if (BossStats.CurrentHp > 0)
                ExecuteBossTurn();

            TickCooldowns();
            RaidUpdated?.Invoke();
            CheckEnd();
        }

        public int AliveCount()
        {
            int c = 0;
            if (TeamStats != null)
                foreach (var s in TeamStats)
                    if (s != null && s.CurrentHp > 0) c++;
            return c;
        }

        private int FindFirstAlive()
        {
            if (TeamStats == null) return -1;
            for (int i = 0; i < TeamStats.Length; i++)
                if (TeamStats[i] != null && TeamStats[i].CurrentHp > 0) return i;
            return -1;
        }

        public void AutoWire(PlayerInsectCollection col, PlayerCandyInventory candy,
            PlayerProgressController prog, Dex.DexController dex, TrainingManager tm)
        {
            if (playerCollection == null) playerCollection = col;
            if (candyInventory == null) candyInventory = candy;
            if (playerProgress == null) playerProgress = prog;
            if (dexController == null) dexController = dex;
            if (trainingManager == null) trainingManager = tm;
        }
    }

    public class RaidBossStats : InsectBattleStats
    {
        public RaidBossStats(InsectData data, int level, int hp, int atk, int def)
            : base(data, level)
        {
            MaxHp = hp;
            Attack = atk;
            Defense = def;
            ResetHp();
        }
    }
}
