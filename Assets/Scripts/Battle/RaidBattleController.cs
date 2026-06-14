using System;
using System.Collections;
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
        [SerializeField] private BattleArenaController arena;

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
            // 방어: 활성 슬롯이 기절/무효면 행동 불가(죽은 멤버 공격 방지). 정상 흐름은 보스턴 후 자동 전환됨.
            if (ActiveSlot >= TeamStats.Length || TeamStats[ActiveSlot] == null
                || TeamStats[ActiveSlot].CurrentHp <= 0) return false;
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

            // 스킬 이름 효과 텍스트 (속성 색상)
            if (skill != null && !string.IsNullOrEmpty(skill.displayName))
                TryPlayEffectText($"{skill.displayName}!", BattleArenaController.GetUIElementColor(skill.element));

            if (skill.effectType == SkillEffectType.Damage)
            {
                BossStats.ApplyDamage(damage, attacker.Attack, BossStats.Defense);
                LastDamageToBoss = Mathf.Max(1, Mathf.RoundToInt(damage * (attacker.Attack / (float)Mathf.Max(1, BossStats.Defense))));
                LastActionText = $"{attacker.Data.displayName}의 {skill.displayName}!";
                TryPlayBossHitFlash();
            }
            else if (skill.effectType == SkillEffectType.BuffAttack)
            {
                attacker.AttackBonus += skill.effectValue;
                LastDamageToBoss = 0;
                LastActionText = $"{attacker.Data.displayName}의 {skill.displayName}! ATK UP!";
                TryPlayEffectText("공격력 상승!", new Color(1f, 0.8f, 0.3f));
            }
            else
            {
                BossStats.AttackBonus -= skill.effectValue;
                LastDamageToBoss = 0;
                LastActionText = $"{attacker.Data.displayName}의 {skill.displayName}! Boss ATK DOWN!";
                TryPlayEffectText("보스 공격력 하락!", new Color(0.6f, 0.4f, 0.9f));
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
                TryPlayEffectText("전체 공격!", new Color(1f, 0.5f, 0.2f));
                for (int i = 0; i < TeamStats.Length; i++)
                {
                    if (TeamStats[i].CurrentHp > 0)
                    {
                        bool wasAlive = TeamStats[i].CurrentHp > 0;
                        TeamStats[i].ApplyDamage(aoeDmg, BossStats.Attack, TeamStats[i].Defense);
                        TryPlayTeamHitFlash(i);
                        if (wasAlive && TeamStats[i].CurrentHp <= 0)
                            TryPlayTeamFaint(i);
                    }
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
                    bool wasAlive = TeamStats[target].CurrentHp > 0;
                    TeamStats[target].ApplyDamage(bossDmg, BossStats.Attack, TeamStats[target].Defense);
                    LastDamageToTeam = Mathf.Max(1, Mathf.RoundToInt(bossDmg * (BossStats.Attack / (float)Mathf.Max(1, TeamStats[target].Defense))));
                    LastHitSlot = target;
                    LastActionText += $"\n{bd.displayName}이(가) {TeamStats[target].Data.displayName}을(를) 공격!";
                    UniteGauge = Mathf.Min(UniteGauge + 10f, UniteGaugeMax);

                    TryPlayTeamHitFlash(target);
                    if (wasAlive && TeamStats[target].CurrentHp <= 0)
                    {
                        TryPlayTeamFaint(target);
                        TryPlayEffectText($"{TeamStats[target].Data.displayName} 쓰러짐!", new Color(0.9f, 0.2f, 0.2f));
                    }
                }
            }

            if (ActiveSlot >= 0 && ActiveSlot < TeamStats.Length
                && TeamStats[ActiveSlot] != null && TeamStats[ActiveSlot].CurrentHp <= 0)
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
            if (!IsActive) return; // 이미 종료(IsActive=false) — ×3 보상/캡처 중복 차단

            if (BossStats.CurrentHp <= 0)
            {
                TryPlayBossFaint();
                TryPlayEffectText("보스 격파!", new Color(0.3f, 1f, 0.5f));
                PlayerWon = true;
                IsActive = false;
                OnRaidVictory();
                RaidEnded?.Invoke(true);
            }
            else if (FindFirstAlive() < 0)
            {
                TryPlayEffectText("팀 전멸!", new Color(0.9f, 0.2f, 0.2f));
                PlayerWon = false;
                IsActive = false;
                // 레이드 패배 시에도 BossEntity Despawn — 옛은 패배 시 보스가 필드에 잔존,
                // 다음 진입 시 같은 보스 중첩 발동 가능 (사용자 보고: "전투 끝나면 사라져야").
                if (BossEntity != null) BossEntity.Despawn();
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
            if (BossEntity != null)
            {
                // playerCollection 가드 분리 — null이어도 보스 Despawn은 항상 보장
                if (playerCollection != null)
                {
                    bool bossShiny = BossEntity.IsShiny;
                    playerCollection.AddCapturedInsect(bd.insectId, BossEntity.Level, bossShiny);
                }
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

            TryPlayEffectText("★ 합체공격! ★", new Color(1f, 0.9f, 0.3f));

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
                // 실제 보스 HP 감소량과 동일 공식 사용 — ApplyDamage 내부 clamp(0.5, 2.5) 정합.
                // 옛은 actual = dmg × (atk/def) (clamp 없음) → UI 표시가 실제보다 크게 보였고,
                // totalDmg += dmg (방어 미반영)는 LastDamageToBoss를 과대 표시.
                float ratio = Mathf.Clamp(atk.Attack / (float)Mathf.Max(1, BossStats.Defense), 0.5f, 2.5f);
                int actual = Mathf.Max(1, Mathf.RoundToInt(dmg * ratio));
                UniteSlotDamages[i] = actual;
                totalDmg += actual;
                names.Add(atk.Data.displayName);
                BossStats.ApplyDamage(dmg, atk.Attack, BossStats.Defense);
            }

            TryPlayBossHitFlash();
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

        public void AutoWire(BattleArenaController a)
        {
            if (arena == null) arena = a;
        }

        // ── 액션 헬퍼 (BattleArenaController 시각 코루틴 wrapper) ──

        private BattleArenaController Arena => arena;

        private void TryPlayBossHitFlash()
        {
            if (Arena == null) return;
            GameObject m = Arena.BossModel;
            if (m != null && m.activeInHierarchy)
                StartCoroutine(Arena.PlayHitFlashCoroutine(m));
        }

        private void TryPlayTeamHitFlash(int index)
        {
            if (Arena == null) return;
            GameObject m = Arena.GetTeamModel(index);
            if (m != null && m.activeInHierarchy)
                StartCoroutine(Arena.PlayHitFlashCoroutine(m));
        }

        private void TryPlayBossFaint()
        {
            if (Arena == null) return;
            GameObject m = Arena.BossModel;
            if (m != null && m.activeInHierarchy)
                StartCoroutine(Arena.PlayFaintCoroutine(m));
        }

        private void TryPlayTeamFaint(int index)
        {
            if (Arena == null) return;
            GameObject m = Arena.GetTeamModel(index);
            if (m != null && m.activeInHierarchy)
                StartCoroutine(Arena.PlayFaintCoroutine(m));
        }

        private void TryPlayEffectText(string text, Color color)
        {
            if (Arena == null || string.IsNullOrEmpty(text)) return;
            Arena.PlayEffectText(text, color);
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
