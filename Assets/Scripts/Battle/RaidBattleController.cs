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

        // 보스가 직전 턴에 쓴 시그니처 스킬(null=기본/AOE) — UI가 BossAttack 페이즈 연출 속성에 사용.
        public InsectSkill LastBossSkill { get; private set; }

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
        private bool bossShinyAtStart; // 시작 시점 스냅샷 — 도주/풀 재사용된 라이브 보스 참조로 이로치 오등록 방지

        public void StartRaid(InsectEntity bossEntity,
            InsectData[] teamInsects, int[] teamLevels,
            PlayerInsectData[] teamPids, InsectSkill[][] teamSkills)
        {
            if (bossEntity == null || bossEntity.Data == null) return;

            BossEntity = bossEntity;
            bossShinyAtStart = bossEntity.IsShiny;
            // 레이드 동안 보스(야생 엔티티)가 도주→Despawn→풀 재사용되어 종료 시 무관 곤충이
            // 등록/Despawn되는 보상 무결성 손상 차단. (1v1 StartBattle과 동일)
            bossEntity.SetEngaged(true);
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
                // 의상/아이템 강화 — 레이드는 팀 보너스를 초기화하는 코드가 없어 공격/방어가 미반영이었음. 여기서 세팅.
                TeamStats[i].AttackBonus = (outfitBonus != null ? outfitBonus.GetAtkBonus() : 0f)
                                         + (itemEffects != null ? itemEffects.GetAtkBonus() : 0f);
                TeamStats[i].DefenseBonus = (outfitBonus != null ? outfitBonus.GetDefBonus() : 0f)
                                          + (itemEffects != null ? itemEffects.GetDefBonus() : 0f);
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
                float effectiveness = InsectTypeChart.GetEffectiveness(
                    skill.element,
                    BossStats.Data != null ? BossStats.Data.primaryType : InsectElement.None,
                    BossStats.Data != null ? BossStats.Data.secondaryType : InsectElement.None);
                float sameTypeBonus = attacker.Data != null
                    ? InsectTypeChart.GetSameTypeBonus(skill.element, attacker.Data.primaryType, attacker.Data.secondaryType)
                    : 1f;
                damage = Mathf.Max(1, Mathf.RoundToInt(damage * effectiveness * sameTypeBonus));
                int hpBefore = BossStats.CurrentHp;
                BossStats.ApplyDamage(damage, attacker.Attack, BossStats.Defense);
                LastDamageToBoss = Mathf.Max(1, hpBefore - BossStats.CurrentHp);
                LastActionText = $"{attacker.Data.displayName}의 {skill.displayName}!";
                TryPlayBossHitFlash();
                if (effectiveness > 1.05f)
                    TryPlayEffectText("효과가 굉장했다!", new Color(1f, 0.55f, 0.2f));
                else if (effectiveness < 0.95f)
                    TryPlayEffectText("효과가 별로인 듯하다...", new Color(0.55f, 0.65f, 0.8f));
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
            LastBossSkill = null;   // 기본값(AOE·기본공격) — 단일 대상 시그니처면 아래에서 갱신

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
                    InsectSkill signature = GetUnlockedBossSignature(bd, BossStats.Level);
                    LastBossSkill = signature;   // UI BossAttack 연출용(null이면 기본 속성)
                    int singleTargetDamage = bossDmg;
                    float effectiveness = 1f;
                    if (signature != null)
                    {
                        effectiveness = InsectTypeChart.GetEffectiveness(
                            signature.element,
                            TeamStats[target].Data != null ? TeamStats[target].Data.primaryType : InsectElement.None,
                            TeamStats[target].Data != null ? TeamStats[target].Data.secondaryType : InsectElement.None);
                        float sameTypeBonus = InsectTypeChart.GetSameTypeBonus(
                            signature.element, bd.primaryType, bd.secondaryType);
                        singleTargetDamage = Mathf.Max(1, Mathf.RoundToInt(
                            (signature.power + BossStats.Level * 2) * bossMult * effectiveness * sameTypeBonus));
                        TryPlayEffectText($"전용기 · {signature.displayName}!", BattleArenaController.GetUIElementColor(signature.element));
                    }

                    bool wasAlive = TeamStats[target].CurrentHp > 0;
                    int hpBefore = TeamStats[target].CurrentHp;
                    TeamStats[target].ApplyDamage(singleTargetDamage, BossStats.Attack, TeamStats[target].Defense);
                    LastDamageToTeam = Mathf.Max(1, hpBefore - TeamStats[target].CurrentHp);
                    LastHitSlot = target;
                    LastActionText += signature != null
                        ? $"\n{bd.displayName}의 {signature.displayName}!"
                        : $"\n{bd.displayName}이(가) {TeamStats[target].Data.displayName}을(를) 공격!";
                    UniteGauge = Mathf.Min(UniteGauge + 10f, UniteGaugeMax);

                    if (signature != null && effectiveness > 1.05f)
                        TryPlayEffectText("효과가 굉장했다!", new Color(1f, 0.55f, 0.2f));
                    else if (signature != null && effectiveness < 0.95f)
                        TryPlayEffectText("효과가 별로인 듯하다...", new Color(0.55f, 0.65f, 0.8f));

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

        private static InsectSkill GetUnlockedBossSignature(InsectData data, int level)
        {
            if (data == null || data.learnset == null) return null;
            foreach (InsectLearnableSkill learnable in data.learnset)
            {
                if (learnable != null
                    && learnable.skill != null
                    && learnable.skill.isSignatureSkill
                    && learnable.learnLevel <= level)
                    return learnable.skill;
            }
            return null;
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
            // EXP/캔디 부스터(아이템·아웃핏) 배율 — 포획 경로(CaptureController)와 동일 항목만 적용.
            // 레이드 ×3 보너스 위에 부스터를 곱하고, 표기/지급이 같도록 곱한 최종값을 저장.
            float candyMultiplier = (itemEffects != null ? itemEffects.GetCandyMultiplier() : 1f)
                                   * (outfitBonus != null ? outfitBonus.GetCandyMultiplier() : 1f);
            float expMultiplier = (itemEffects != null ? itemEffects.GetExpMultiplier() : 1f)
                                 * (outfitBonus != null ? outfitBonus.GetExpMultiplier() : 1f);
            RewardCandy = Mathf.RoundToInt(candyBase * 3 * candyMultiplier);
            RewardExp = Mathf.RoundToInt(expBase * 3 * expMultiplier);

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
                    // 레벨/이로치는 시작 스냅샷(BossStats.Level/bossShinyAtStart) — 라이브 참조 회피
                    playerCollection.AddCapturedInsect(bd.insectId, BossStats.Level, bossShinyAtStart);
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

        private ItemEffectManager itemEffects;

        public void AutoWire(ItemEffectManager effects)
        {
            if (itemEffects == null) itemEffects = effects;
        }

        private OutfitBonusProvider outfitBonus;

        public void AutoWire(OutfitBonusProvider bonus)
        {
            if (outfitBonus == null) outfitBonus = bonus;
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
