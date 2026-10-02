#if UNITY_EDITOR
using System;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InsectGame.Tests
{
    /// <summary>
    /// <see cref="TrainingPricing"/> — 기술 가격(가치 기준)과 능력치(개체값) 훈련 비용.
    ///
    /// 예전엔 기술 가격이 <c>power</c>에서만 나와 상태기(위력 칸 1)가 "가장 약한 기술"로 팔렸다
    /// (광폭화 공격 +60% = 26캔디 1회, 파멸의 독침 = 216캔디). 피해기 가격은 그대로 두고 상태기만
    /// 효과로 값을 매기는 것이 설계라, 두 방향을 함께 고정한다.
    /// </summary>
    [TestFixture]
    public class TrainingPricingTests
    {
        private static InsectSkill Skill(SkillEffectType type, int power, float effect = 0.2f, int turns = 2, float accuracy = 1f)
        {
            InsectSkill s = ScriptableObject.CreateInstance<InsectSkill>();
            s.skillId = "t_" + type + power;
            s.effectType = type;
            s.power = power;
            s.effectValue = effect;
            s.effectDurationTurns = turns;
            s.accuracy = accuracy;
            return s;
        }

        [Test]
        public void DamageSkills_KeepOldPowerPricing()
        {
            // 옛 범용기 공식: 회당 max(5, power/2 + 요구Lv), 횟수 clamp(1 + power/12, 1, 5).
            foreach (int power in new[] { 10, 13, 20, 28, 36, 40, 60, 78 })
            foreach (int level in new[] { 1, 7, 16, 34 })
            {
                InsectSkill s = Skill(SkillEffectType.Damage, power);
                try
                {
                    Assert.AreEqual(Mathf.Max(5, power / 2 + level), TrainingPricing.SessionCost(s, level), $"위력 {power} Lv{level}");
                    Assert.AreEqual(Mathf.Clamp(1 + power / 12, 1, 5), TrainingPricing.RequiredSessions(s), $"위력 {power}");
                }
                finally { Object.DestroyImmediate(s); }
            }
        }

        [Test]
        public void StatusSkill_IsPricedByEffect_NotByPowerField()
        {
            // 광폭화(공격 +60% · 2턴, Lv26)와 돌격(위력 20, Lv7) — 옛 가격은 26 대 34로 광폭화가 더 쌌다.
            InsectSkill berserk = Skill(SkillEffectType.BuffAttack, 1, 0.6f, 2);
            InsectSkill charge = Skill(SkillEffectType.Damage, 20);
            try
            {
                Assert.Greater(TrainingPricing.RequiredSessions(berserk), 1, "상태기도 한 번에 배우면 안 된다");
                Assert.Greater(TrainingPricing.TotalSkillCost(berserk, 26), TrainingPricing.TotalSkillCost(charge, 7) * 3,
                    "공격 +60% 버프가 위력 20 돌격의 몇 배는 되어야 한다");
                Assert.Greater(TrainingPricing.SkillValue(berserk), 30f);
            }
            finally
            {
                Object.DestroyImmediate(berserk);
                Object.DestroyImmediate(charge);
            }
        }

        [Test]
        public void EveryEffectType_HasRealValue()
        {
            // 위력 칸이 1인 효과가 하나라도 1로 읽히면 그 종류만 다시 헐값이 된다.
            foreach (SkillEffectType type in Enum.GetValues(typeof(SkillEffectType)))
            {
                int power = type == SkillEffectType.Damage || type == SkillEffectType.PoisonDot ? 12 : 1;
                InsectSkill s = Skill(type, power, 0.3f, 3);
                try
                {
                    Assert.GreaterOrEqual(TrainingPricing.SkillValue(s), 12f, $"{type}의 가치가 위력 칸(1)으로 떨어졌다");
                }
                finally { Object.DestroyImmediate(s); }
            }
        }

        [Test]
        public void LowerAccuracy_LowersValue()
        {
            InsectSkill sure = Skill(SkillEffectType.Damage, 42);
            InsectSkill risky = Skill(SkillEffectType.Damage, 42, accuracy: 0.9f);
            try
            {
                Assert.Less(TrainingPricing.SkillValue(risky), TrainingPricing.SkillValue(sure));
            }
            finally
            {
                Object.DestroyImmediate(sure);
                Object.DestroyImmediate(risky);
            }
        }

        [Test]
        public void LaterUnlock_CostsMore()
        {
            InsectSkill s = Skill(SkillEffectType.Damage, 26);
            try
            {
                Assert.Greater(TrainingPricing.SessionCost(s, 20), TrainingPricing.SessionCost(s, 5));
            }
            finally { Object.DestroyImmediate(s); }
        }

        // ── 능력치(개체값) ──

        [Test]
        public void StatCost_EscalatesPerIv_AndZeroAtMax()
        {
            Assert.AreEqual(20, TrainingPricing.StatCost(0, InsectRarity.Common));
            Assert.AreEqual(257, TrainingPricing.StatCost(14, InsectRarity.Common));
            Assert.AreEqual(0, TrainingPricing.StatCost(PlayerInsectData.MaxIV, InsectRarity.Common), "최대면 더 올릴 수 없다");
            for (int iv = 1; iv < PlayerInsectData.MaxIV; iv++)
                Assert.Greater(TrainingPricing.StatCost(iv, InsectRarity.Common), TrainingPricing.StatCost(iv - 1, InsectRarity.Common));
        }

        [Test]
        public void StatCost_RarerCostsMore_UsingRewardMultiplier()
        {
            int common = TrainingPricing.StatCost(8, InsectRarity.Common);
            int legendary = TrainingPricing.StatCost(8, InsectRarity.Legendary);
            Assert.AreEqual(Mathf.RoundToInt(common * InsectRewardCalculator.GetRarityMultiplier(InsectRarity.Legendary)), legendary, 1);
            Assert.Greater(TrainingPricing.StatCost(8, InsectRarity.Rare), common);
        }

        [Test]
        public void AverageCommon_ToGradeS_CostsMoreThanLevelingToFifty()
        {
            // 설계: 평균 일반 개체(5/5/5)를 S급(14/14/13)으로 ≈ 2,900캔디 — Lv1→50 레벨업 전부(2,548)보다 크다.
            // "댓가가 커야 한다"가 요구였다. 계수를 낮추면 이 테스트가 먼저 알린다.
            int toS = TrainingPricing.StatCostRange(5, 14, InsectRarity.Common) * 2
                + TrainingPricing.StatCostRange(5, 13, InsectRarity.Common);
            int levelTo50 = 0;
            for (int lv = 1; lv < 50; lv++)
                levelTo50 += GameConstants.Leveling.FallbackBaseCandyCost + (lv - 1) * GameConstants.Leveling.FallbackCandyCostGrowth;

            Assert.Greater(toS, levelTo50);
            Assert.That(toS, Is.InRange(2500, 3300), "표준안(20×1.2^IV) 대역을 벗어났다 — balance.md 「훈련 기준점」도 함께 고칠 것");
        }

        [Test]
        public void GradeForSum_BoundariesMatchPercentThresholds()
        {
            Assert.AreEqual(41, PlayerInsectData.MinIvSumFor(IVGrade.S));
            Assert.AreEqual(32, PlayerInsectData.MinIvSumFor(IVGrade.A));
            Assert.AreEqual(23, PlayerInsectData.MinIvSumFor(IVGrade.B));
            Assert.AreEqual(14, PlayerInsectData.MinIvSumFor(IVGrade.C));
            Assert.AreEqual(0, PlayerInsectData.MinIvSumFor(IVGrade.D));

            var pid = new PlayerInsectData { ivHp = 14, ivAtk = 14, ivDef = 12 };
            Assert.AreEqual(IVGrade.A, pid.Grade);
            pid.ivDef = 13;
            Assert.AreEqual(IVGrade.S, pid.Grade, "합 41이 S");
        }

        [Test]
        public void RaiseIv_CapsAtMax_AndKeepsMissingHp()
        {
            var pid = new PlayerInsectData { ivHp = 3, ivAtk = PlayerInsectData.MaxIV, currentHp = 50 };

            Assert.IsTrue(pid.RaiseIv(GrowthStat.Hp));
            Assert.AreEqual(4, pid.ivHp);
            Assert.AreEqual(50 + PlayerInsectData.HpPerIv, pid.currentHp, "최대 HP가 는 만큼 현재 HP도 올라야 부상으로 안 뜬다");

            Assert.IsFalse(pid.RaiseIv(GrowthStat.Attack), "15에서는 더 못 올린다");
            Assert.AreEqual(PlayerInsectData.MaxIV, pid.ivAtk);

            var fainted = new PlayerInsectData { ivHp = 3, currentHp = 0 };
            fainted.RaiseIv(GrowthStat.Hp);
            Assert.AreEqual(0, fainted.currentHp, "기절은 훈련으로 풀리지 않는다");

            var legacy = new PlayerInsectData { ivHp = 3, currentHp = -1 };
            legacy.RaiseIv(GrowthStat.Hp);
            Assert.AreEqual(-1, legacy.currentHp, "미초기화 센티넬은 그대로 둔다(로드가 풀피로 채운다)");
        }
    }
}
#endif
