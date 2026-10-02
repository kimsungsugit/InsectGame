#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    [TestFixture]
    public class BattlePacingTests
    {
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
        private InsectBattleStats player;
        private InsectBattleStats enemy;

        [TearDown]
        public void Cleanup()
        {
            foreach (var obj in objects) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            objects.Clear();
        }

        private InsectBattleController CreateBattle(int rarity, int level, int power, bool wild = true)
        {
            var data = ScriptableObject.CreateInstance<InsectData>();
            objects.Add(data);
            data.insectId = "pacing_fixture";
            data.baseHp = 49 + rarity * 18;
            data.baseAtk = 19 + rarity * 9;
            data.baseDef = 14 + rarity * 7;
            data.primaryType = InsectElement.None;
            var skill = ScriptableObject.CreateInstance<InsectSkill>();
            objects.Add(skill);
            skill.effectType = SkillEffectType.Damage;
            skill.element = InsectElement.None;
            skill.power = power;
            skill.accuracy = 1f;
            skill.cooldownTurns = 2;
            data.skills = new[] { skill };
            var go = new GameObject("BattlePacingFixture");
            objects.Add(go);
            var controller = go.AddComponent<InsectBattleController>();
            controller.StartDuel(data, level, data, level, (p, e) => { player = p; enemy = e; });
            if (wild) typeof(InsectBattleController).GetField("duelMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, false);
            return controller;
        }

        [Test]
        public void NeutralMatrix_EqualStats_MedianBetweenFourAndSixRounds()
        {
            var rounds = new List<int>();
            foreach (int rarity in new[] { 0, 1, 2, 3, 4 })
            foreach (int level in new[] { 5, 15, 30, 50 })
            foreach (int power in new[] { 18, 30, 45 })
            {
                var battle = CreateBattle(rarity, level, power);
                int count = 0;
                while (player.CurrentHp > 0 && enemy.CurrentHp > 0 && count < 30)
                {
                    if (battle.CanUseSkill(0)) battle.UseSkill(0); else battle.UseBasicAttack();
                    count++;
                }
                rounds.Add(count);
            }
            rounds.Sort();
            float median = (rounds[29] + rounds[30]) / 2f;
            TestContext.WriteLine($"60 neutral fixtures: min={rounds[0]}, median={median}, max={rounds[59]}");
            Assert.That(median, Is.InRange(4f, 6f));
            Assert.Less(rounds[59], 12, "Neutral fights must not become attrition stalls.");
        }

        [Test]
        public void WildPacing_LeavesDuelDamageAndMaximumHpUnchanged()
        {
            var wild = CreateBattle(2, 15, 30);
            int maximum = enemy.MaxHp;
            wild.UseSkill(0);
            int wildDamage = maximum - wild.EnemyHpAfterPlayerAction;
            var duel = CreateBattle(2, 15, 30, false);
            Assert.AreEqual(maximum, enemy.MaxHp);
            duel.UseSkill(0);
            int duelDamage = maximum - duel.EnemyHpAfterPlayerAction;
            Assert.Greater(duelDamage, wildDamage);
            Assert.AreEqual(Mathf.RoundToInt(45f * 1.5f), duelDamage);
        }

        [Test]
        public void Resolution_SnapshotsPlayerActionBeforeEnemyReply()
        {
            var battle = CreateBattle(2, 15, 30);
            int hp = player.CurrentHp;
            battle.UseSkill(0);
            Assert.IsTrue(battle.EnemyActedThisRound);
            Assert.AreEqual(hp, battle.PlayerHpAfterPlayerAction);
            Assert.Less(player.CurrentHp, battle.PlayerHpAfterPlayerAction);
            Assert.AreEqual(enemy.CurrentHp, battle.EnemyHpAfterPlayerAction);
        }

        [Test]
        public void LethalPlayerAction_DoesNotInventEnemyReply()
        {
            var battle = CreateBattle(0, 5, 1000);
            battle.UseSkill(0);
            Assert.AreEqual(0, enemy.CurrentHp);
            Assert.IsFalse(battle.EnemyActedThisRound);
        }

        [Test]
        public void StunnedEnemy_DoesNotCreateAnAttackPresentation()
        {
            var battle = CreateBattle(2, 15, 30);
            typeof(InsectBattleController).GetField("enemyStunTurns", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(battle, 1);
            int hp = player.CurrentHp;
            battle.UseSkill(0);
            Assert.IsFalse(battle.EnemyActedThisRound);
            Assert.AreEqual(hp, player.CurrentHp);
        }

        [Test]
        public void GuardianDamage_IsExcludedFromWildPacing()
        {
            var battle = CreateBattle(2, 15, 30);
            typeof(InsectBattleController).GetProperty("EnemyGuardianRegionId").SetValue(battle, "forest");
            int hp = enemy.CurrentHp;
            battle.UseSkill(0);
            Assert.AreEqual(Mathf.RoundToInt(45f * 1.5f), hp - battle.EnemyHpAfterPlayerAction);
        }

        [Test]
        public void FatalEnemyReply_IsPresentedBeforeResult()
        {
            var battle = CreateBattle(2, 15, 1000, false);
            var uiObject = new GameObject("BattlePhaseFixture");
            objects.Add(uiObject);
            var ui = uiObject.AddComponent<InsectGame.UI.BattleScreenUI>();
            ui.AutoWire(battle, null, null);
            battle.StartDuel(player.Data, 15, enemy.Data, 15);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var phase = typeof(InsectGame.UI.BattleScreenUI).GetField("phase", flags);
            phase.SetValue(ui, Enum.Parse(phase.FieldType, "PlayerTurn"));
            typeof(InsectGame.UI.BattleScreenUI).GetMethod("TryBasicAttack", flags).Invoke(ui, null);
            Assert.AreEqual("PlayerAttack", phase.GetValue(ui).ToString());
            typeof(InsectGame.UI.BattleScreenUI).GetField("phaseTimer", flags).SetValue(ui, 3f);
            typeof(InsectGame.UI.BattleScreenUI).GetMethod("Update", flags).Invoke(ui, null);
            Assert.AreEqual("TurnAnnounce", phase.GetValue(ui).ToString(), "Fatal reply must precede result.");
            typeof(InsectGame.UI.BattleScreenUI).GetMethod("FinishTurnAnnounce", flags).Invoke(ui, null);
            Assert.AreEqual("EnemyAttack", phase.GetValue(ui).ToString());
        }

        private static void SetPrivate(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        }

        [Test]
        public void PoisonAfterEnemyAction_IsNotCountedAsEnemyHit()
        {
            var battle = CreateBattle(2, 15, 30);
            typeof(InsectBattleController).GetMethod("AddEffect", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(battle, new object[] { true, 1000f, 2, InsectBattleController.EffectKind.Dot });
            battle.UseSkill(0);
            Assert.Greater(battle.PlayerHpAfterEnemyAction, 0, "Enemy hit itself is survivable.");
            Assert.AreEqual(0, player.CurrentHp, "The KO belongs to end-of-round poison.");
            Assert.Greater(battle.PlayerHpAfterPlayerAction, battle.PlayerHpAfterEnemyAction);
        }

        // 상대가 자기에게 건 버프는 상대 것이다 — 상태 줄(DrawHpBox)이 targetIsPlayer로 편을 가르고,
        // 가운데 문구는 자리로 편을 알 수 없으니 "상대"를 붙인다.
        [Test]
        public void EnemySelfBuff_BelongsToTheEnemy_AndSaysSo()
        {
            var battle = CreateBattle(2, 15, 30);
            var buff = ScriptableObject.CreateInstance<InsectSkill>();
            objects.Add(buff);
            buff.effectType = SkillEffectType.BuffAttack;
            buff.effectValue = 0.3f;
            buff.effectDurationTurns = 3;
            typeof(InsectBattleController).GetMethod("ApplySkill", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(battle, new object[] { enemy, player, buff, false });

            InsectBattleController.EffectSnapshot[] effects = battle.GetActiveEffects();
            Assert.AreEqual(1, effects.Length);
            Assert.IsFalse(effects[0].targetIsPlayer, "상대의 자기 버프가 내 곤충에 걸렸다");
            Assert.AreEqual(InsectBattleController.EffectKind.AtkBuff, effects[0].kind);
            Assert.Greater(effects[0].value, 0f);

            Assert.AreEqual("공격력 상승!", InsectBattleController.SidedText(true, "공격력 상승!"));
            Assert.AreEqual("상대 공격력 상승!", InsectBattleController.SidedText(false, "공격력 상승!"));
        }

        [Test]
        public void PlayerStun_SkipsAnimationAndDoesNotSpendSkillCooldown()
        {
            var battle = CreateBattle(2, 15, 30);
            SetPrivate(battle, "playerStunTurns", 1);
            int enemyHp = enemy.CurrentHp;
            battle.UseSkill(0);
            Assert.IsFalse(battle.PlayerActedThisRound);
            Assert.IsTrue(battle.EnemyActedThisRound);
            Assert.AreEqual(enemyHp, battle.EnemyHpAfterPlayerAction);
            Assert.IsTrue(battle.CanUseSkill(0));
        }

        [Test]
        public void InvalidSkill_DoesNotResolveRoundOrChangeSnapshots()
        {
            var battle = CreateBattle(2, 15, 30);
            int hp = player.CurrentHp;
            int enemyHp = enemy.CurrentHp;
            int updates = 0;
            battle.BattleUpdated += (p, e) => updates++;
            battle.UseSkill(-1);
            battle.UseSkill(99);
            Assert.AreEqual(0, updates);
            Assert.AreEqual(hp, player.CurrentHp);
            Assert.AreEqual(enemyHp, enemy.CurrentHp);
            Assert.IsFalse(battle.PlayerActedThisRound);
            Assert.IsFalse(battle.EnemyActedThisRound);
        }

        [Test]
        public void FailedEscape_OnlyResolvesEnemyReply()
        {
            int seed = 0;
            for (; seed < 1000; seed++)
                if (new BattleRandomSource(seed).Next01() >= 0.5f) break;
            var battle = CreateBattle(2, 15, 30);
            int before = enemy.CurrentHp;
            battle.SetRandomSeed(seed);
            Assert.IsFalse(battle.TryEscape());
            Assert.IsFalse(battle.DidEscape);
            Assert.IsFalse(battle.PlayerActedThisRound);
            Assert.IsTrue(battle.EnemyActedThisRound);
            Assert.AreEqual(before, battle.EnemyHpAfterPlayerAction);
            Assert.AreEqual(before, enemy.CurrentHp);
        }

        [Test]
        public void SuccessfulEscape_FlagAvailableToEventAndResetByNextBattle()
        {
            var battle = CreateBattle(2, 15, 30);
            int seed = 0;
            for (; seed < 1000; seed++)
                if (new BattleRandomSource(seed).Next01() < 0.5f) break;
            battle.SetRandomSeed(seed);
            bool observedEscape = false;
            int events = 0;
            battle.BattleEnded += won =>
            {
                observedEscape = battle.DidEscape;
                events++;
                Assert.IsFalse(won);
            };
            Assert.IsTrue(battle.TryEscape());
            Assert.IsTrue(observedEscape, "Subscribers must distinguish escape from defeat in the callback.");
            Assert.IsTrue(battle.DidEscape);
            Assert.AreEqual(1, events);
            Assert.IsFalse(battle.TryEscape(), "Ended battle cannot raise escape twice.");
            Assert.AreEqual(1, events);
            Assert.IsTrue(battle.StartDuel(player.Data, 15, enemy.Data, 15));
            Assert.IsFalse(battle.DidEscape, "A new battle cannot inherit the escape outcome.");
        }

        [Test]
        public void OrdinaryDefeat_DoesNotReportEscape()
        {
            var battle = CreateBattle(2, 15, 1000);
            bool ended = false;
            battle.BattleEnded += won =>
            {
                ended = true;
                Assert.IsFalse(won);
                Assert.IsFalse(battle.DidEscape);
            };
            battle.UseBasicAttack();
            Assert.AreEqual(0, player.CurrentHp);
            Assert.IsTrue(ended);
            Assert.IsFalse(battle.DidEscape);
        }

        [TestCase(SkillEffectType.Heal)]
        [TestCase(SkillEffectType.DefenseBuff)]
        public void RepeatedRecoveryOrDefense_WithDamageFallback_DoesNotStall(SkillEffectType type)
        {
            var battle = CreateBattle(2, 15, 30);
            var support = ScriptableObject.CreateInstance<InsectSkill>();
            objects.Add(support);
            support.effectType = type;
            support.effectValue = type == SkillEffectType.Heal ? 0.3f : 0.35f;
            support.effectDurationTurns = 3;
            support.cooldownTurns = 3;
            support.accuracy = 1f;
            SetPrivate(battle, "playerOverrideSkills", new[] { player.Data.skills[0], support });
            SetPrivate(battle, "playerCooldowns", new int[2]);
            int rounds = 0;
            int supportUses = 0;
            while (player.CurrentHp > 0 && enemy.CurrentHp > 0 && rounds < 30)
            {
                bool useSupport = battle.CanUseSkill(1) &&
                    (type == SkillEffectType.DefenseBuff || player.CurrentHp < player.MaxHp * 0.7f);
                if (useSupport) { battle.UseSkill(1); supportUses++; }
                else if (battle.CanUseSkill(0)) battle.UseSkill(0);
                else battle.UseBasicAttack();
                rounds++;
            }
            TestContext.WriteLine($"{type}: rounds={rounds}, supportUses={supportUses}");
            Assert.Greater(supportUses, 0);
            Assert.Less(rounds, 20, "A support rotation must not turn this neutral matchup into a stall.");
            Assert.IsTrue(player.CurrentHp <= 0 || enemy.CurrentHp <= 0);
        }

        [Test]
        public void HealingSnapshot_PrecedesEnemyDamageAndHasNoDamageToEnemy()
        {
            var battle = CreateBattle(2, 15, 30);
            var heal = ScriptableObject.CreateInstance<InsectSkill>();
            objects.Add(heal);
            heal.effectType = SkillEffectType.Heal;
            heal.effectValue = 0.3f;
            SetPrivate(battle, "playerOverrideSkills", new[] { heal });
            player.ApplyDamage(60);
            int before = player.CurrentHp;
            int enemyBefore = enemy.CurrentHp;
            battle.UseSkill(0);
            Assert.Greater(battle.PlayerHpAfterPlayerAction, before);
            Assert.Less(battle.PlayerHpAfterEnemyAction, battle.PlayerHpAfterPlayerAction);
            Assert.AreEqual(enemyBefore, battle.EnemyHpAfterPlayerAction);
        }

        [Test]
        public void DisableEnableDuringAction_RestoresOwnershipAndCanFinishReply()
        {
            var battle = CreateBattle(2, 15, 30, false);
            var go = new GameObject("BattleResumeFixture");
            objects.Add(go);
            var ui = go.AddComponent<InsectGame.UI.BattleScreenUI>();
            ui.AutoWire(battle, null, null);
            battle.StartDuel(player.Data, 15, enemy.Data, 15);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var phase = ui.GetType().GetField("phase", flags);
            phase.SetValue(ui, Enum.Parse(phase.FieldType, "PlayerTurn"));
            ui.GetType().GetMethod("TryBasicAttack", flags).Invoke(ui, null);
            go.SetActive(false);
            Assert.IsFalse(battle.DeferPresentation);
            go.SetActive(true);
            Assert.IsTrue(battle.DeferPresentation);
            SetPrivate(ui, "phaseTimer", 3f);
            ui.GetType().GetMethod("Update", flags).Invoke(ui, null);
            Assert.AreEqual("TurnAnnounce", phase.GetValue(ui).ToString());
            Assert.IsTrue((bool)ui.GetType().GetField("impactRevealed", flags).GetValue(ui));
        }

        [Test]
        public void MissesAndEscape_AreUnaffectedByPresentationRandomDraws()
        {
            var original = UnityEngine.Random.state;
            try
            {
                for (int seed = 0; seed < 20; seed++)
                {
                    var first = CreateBattle(2, 15, 30);
                    first.SetRandomSeed(seed);
                    player.Data.skills[0].accuracy = 0.55f;
                    first.UseSkill(0);
                    int expectedPlayer = player.CurrentHp;
                    int expectedEnemy = enemy.CurrentHp;
                    bool expectedEscape = first.TryEscape();
                    int expectedAfterEscape = player.CurrentHp;
                    var second = CreateBattle(2, 15, 30);
                    second.SetRandomSeed(seed);
                    player.Data.skills[0].accuracy = 0.55f;
                    for (int draw = 0; draw < 127; draw++) _ = UnityEngine.Random.value;
                    second.UseSkill(0);
                    Assert.AreEqual(expectedPlayer, player.CurrentHp);
                    Assert.AreEqual(expectedEnemy, enemy.CurrentHp);
                    for (int draw = 0; draw < 251; draw++) _ = UnityEngine.Random.value;
                    Assert.AreEqual(expectedEscape, second.TryEscape());
                    Assert.AreEqual(expectedAfterEscape, player.CurrentHp);
                }
            }
            finally { UnityEngine.Random.state = original; }
        }

        [Test]
        public void PresentationSpeed_DoesNotChangeResolvedDamageOrCooldowns()
        {
            // Exercise the presentation rate without creating/changing persisted user settings.
            var speedField = typeof(BattlePresentation).GetField("speed", BindingFlags.Static | BindingFlags.NonPublic);
            float previous = (float)speedField.GetValue(null);
            try
            {
                speedField.SetValue(null, 1f);
                var first = CreateBattle(2, 15, 30);
                first.UseSkill(0);
                int expectedPlayer = player.CurrentHp;
                int expectedEnemy = enemy.CurrentHp;
                bool expectedSkill = first.CanUseSkill(0);
                speedField.SetValue(null, 2f);
                var second = CreateBattle(2, 15, 30);
                second.UseSkill(0);
                Assert.AreEqual(expectedPlayer, player.CurrentHp);
                Assert.AreEqual(expectedEnemy, enemy.CurrentHp);
                Assert.AreEqual(expectedSkill, second.CanUseSkill(0));
            }
            finally { speedField.SetValue(null, previous); }
        }
    }
}
#endif
