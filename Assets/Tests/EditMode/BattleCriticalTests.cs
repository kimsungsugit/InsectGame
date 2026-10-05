#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InsectGame.Tests
{
    /// <summary>
    /// 진짜 치명타(1/16 · ×1.5) — 1v1과 레이드 리졸버, 그리고 전투 화면이 그 판정을 그대로 옮기는지.
    ///
    /// 예전엔 판정이 없었고 화면이 "적 최대 HP의 25% 이상"을 CRITICAL로 <b>표시만</b> 했다. 전투가 3~4라운드라
    /// 거의 매 타격이 그 선을 넘어 상시 CRITICAL이었고, 그 플래그가 아레나의 무거운 타격 연출로도 흘렀다.
    ///
    /// 치명타는 명중 롤과 <b>다른 줄기</b>다 — 여기 테스트는 줄기를 주입해 경계·배율·적용 범위를 고정한다.
    /// </summary>
    [TestFixture]
    public class BattleCriticalTests
    {
        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int Level = 10;
        private readonly List<Object> objects = new List<Object>();
        private InsectBattleStats player;
        private InsectBattleStats enemy;

        [TearDown]
        public void Cleanup()
        {
            foreach (Object obj in objects) if (obj != null) Object.DestroyImmediate(obj);
            objects.Clear();
        }

        // ── 순수 판정 ──

        [Test]
        public void RollCritical_BoundaryIsOneSixteenth()
        {
            Assert.AreEqual(1f / 16f, GameConstants.Battle.CritChance, 1e-7f);
            Assert.IsTrue(InsectBattleController.RollCritical(0.0624f), "1/16 바로 아래는 치명타");
            Assert.IsFalse(InsectBattleController.RollCritical(0.0626f), "1/16 바로 위는 아니다");
            Assert.IsTrue(InsectBattleController.RollCritical(0f));
            Assert.IsFalse(InsectBattleController.RollCritical(0.99999994f));
        }

        [Test]
        public void ApplyCritical_MultipliesByOneAndAHalf_AndRollsOnce()
        {
            var hit = new ScriptedRoll(0.0624f);
            Assert.AreEqual(60, InsectBattleController.ApplyCritical(40, hit, out bool critical));
            Assert.IsTrue(critical);
            Assert.AreEqual(1, hit.Draws, "피해기 한 번에 한 번만 굴린다");
            Assert.AreEqual(1.5f, GameConstants.Battle.CritMultiplier);

            var miss = new ScriptedRoll(0.0626f);
            Assert.AreEqual(40, InsectBattleController.ApplyCritical(40, miss, out critical));
            Assert.IsFalse(critical);

            Assert.AreEqual(2, InsectBattleController.ApplyCritical(1, new ScriptedRoll(0f), out _), "1 × 1.5 → 2(반올림)");
        }

        [Test]
        public void ApplyCritical_NullSource_DoesNotRoll_AndIsDeterministic()
        {
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(37, InsectBattleController.ApplyCritical(37, null, out bool critical));
                Assert.IsFalse(critical);
            }
        }

        [Test]
        public void ExpectedDamageGain_IsAboutThreePercent()
        {
            float gain = GameConstants.Battle.CritChance * (GameConstants.Battle.CritMultiplier - 1f);
            Assert.AreEqual(0.03125f, gain, 1e-6f, "체감용 변동이지 전투 길이 손잡이가 아니다 — 늘리면 balance.md 전투 길이부터");
        }

        // ── 1v1 ──

        [Test]
        public void Skill_CriticalRoll_DealsOneAndAHalfTimes_AndFlagsBothSides()
        {
            InsectBattleController battle = Duel(new ScriptedRoll(0.0624f));
            int enemyBefore = enemy.CurrentHp;
            int playerBefore = player.CurrentHp;

            battle.UseSkill(0);

            // 공격 40 = 방어 40 → 공방 비율 1, 대결이라 야생 페이싱 없음. (30 + Lv10) = 40 → ×1.5 = 60.
            Assert.AreEqual(60, enemyBefore - battle.EnemyHpAfterPlayerAction);
            Assert.AreEqual(60, playerBefore - battle.PlayerHpAfterEnemyAction);
            Assert.IsTrue(battle.LastPlayerHitCritical);
            Assert.IsTrue(battle.LastEnemyHitCritical);
        }

        [Test]
        public void Skill_RollJustAboveChance_IsNotCritical()
        {
            InsectBattleController battle = Duel(new ScriptedRoll(0.0626f));
            int enemyBefore = enemy.CurrentHp;

            battle.UseSkill(0);

            Assert.AreEqual(40, enemyBefore - battle.EnemyHpAfterPlayerAction);
            Assert.IsFalse(battle.LastPlayerHitCritical);
            Assert.IsFalse(battle.LastEnemyHitCritical);
        }

        [Test]
        public void BasicAttack_CanBeCritical()
        {
            InsectBattleController battle = Duel(new ScriptedRoll(0f));
            StunEnemy(battle);
            int enemyBefore = enemy.CurrentHp;

            battle.UseBasicAttack();

            int plain = Mathf.Max(1, Mathf.RoundToInt(player.Attack * 0.7f));
            Assert.AreEqual(Mathf.RoundToInt(plain * 1.5f), enemyBefore - battle.EnemyHpAfterPlayerAction);
            Assert.IsTrue(battle.LastPlayerHitCritical);
            Assert.IsFalse(battle.LastEnemyHitCritical, "기절한 상대는 때리지 않았다");
        }

        [Test]
        public void NullCritSource_IsDeterministic_AndNeverFlags()
        {
            InsectBattleController battle = Duel(null);
            for (int round = 0; round < 5; round++)
            {
                int enemyBefore = enemy.CurrentHp;
                if (battle.CanUseSkill(0)) battle.UseSkill(0); else battle.UseBasicAttack();
                Assert.IsFalse(battle.LastPlayerHitCritical);
                Assert.IsFalse(battle.LastEnemyHitCritical);
                Assert.Greater(enemyBefore, enemy.CurrentHp);
            }
        }

        [TestCase(SkillEffectType.BuffAttack)]
        [TestCase(SkillEffectType.DebuffAttack)]
        [TestCase(SkillEffectType.DefenseBuff)]
        [TestCase(SkillEffectType.Heal)]
        [TestCase(SkillEffectType.PoisonDot)]
        [TestCase(SkillEffectType.Stun)]
        public void NonDamageSkill_DoesNotRollCritical(SkillEffectType type)
        {
            var crit = new ScriptedRoll(0f);
            InsectBattleController battle = Duel(crit);
            InsectSkill support = Skill("support_" + type, type, 6);
            support.effectValue = 0.3f;
            support.effectDurationTurns = 2;
            SetPrivate(battle, "playerOverrideSkills", new[] { support });
            SetPrivate(battle, "playerCooldowns", new int[1]);
            StunEnemy(battle);

            battle.UseSkill(0);

            Assert.AreEqual(0, crit.Draws, $"{type}는 피해기가 아니다 — 치명타를 굴리면 안 된다");
            Assert.IsFalse(battle.LastPlayerHitCritical);
        }

        [Test]
        public void MissedSkill_DoesNotRollCritical()
        {
            var crit = new ScriptedRoll(0f);
            InsectBattleController battle = Duel(crit);
            player.Data.skills[0].accuracy = 0.5f;
            battle.SetRandomSource(new ScriptedRoll(0.99f));   // 명중 줄기: 빗나감
            StunEnemy(battle);
            int enemyBefore = enemy.CurrentHp;

            battle.UseSkill(0);

            Assert.AreEqual(enemyBefore, battle.EnemyHpAfterPlayerAction, "빗나갔다");
            Assert.AreEqual(0, crit.Draws, "빗나간 공격은 치명타를 굴리지 않는다");
            Assert.IsFalse(battle.LastPlayerHitCritical);
        }

        [Test]
        public void CritStream_DoesNotShiftHitRolls()
        {
            // 같은 명중 줄기에서 치명타를 켜든 끄든 명중 롤 소비가 같아야 한다 — 같은 줄기면 피해기마다 하나씩 밀린다.
            int Draws(IRaidRandomSource crit)
            {
                var hits = new ScriptedRoll(0.1f);
                InsectBattleController battle = Duel(crit);
                player.Data.skills[0].accuracy = 0.5f;
                battle.SetRandomSource(hits);
                for (int i = 0; i < 4; i++)
                {
                    if (battle.CanUseSkill(0)) battle.UseSkill(0); else battle.UseBasicAttack();
                }
                return hits.Draws;
            }

            Assert.AreEqual(Draws(null), Draws(new ScriptedRoll(0f)));
        }

        [Test]
        public void NextAction_ClearsTheCriticalFlags()
        {
            var crit = new ScriptedRoll(0f);
            InsectBattleController battle = Duel(crit);
            battle.UseSkill(0);
            Assert.IsTrue(battle.LastPlayerHitCritical);

            crit.Value = 0.5f;
            battle.UseBasicAttack();
            Assert.IsFalse(battle.LastPlayerHitCritical, "지난 라운드 치명타가 남으면 다음 타격도 CRITICAL로 뜬다");
            Assert.IsFalse(battle.LastEnemyHitCritical);
        }

        [Test]
        public void CriticalHit_WithEffectiveness_AddsNoTopLine_TheMatchupGoesToTheNumber()
        {
            InsectElement attack = InsectElement.None, defend = InsectElement.None;
            foreach (InsectElement a in Enum.GetValues(typeof(InsectElement)))
            foreach (InsectElement d in Enum.GetValues(typeof(InsectElement)))
            {
                if (attack == InsectElement.None && InsectTypeChart.GetEffectiveness(a, d, InsectElement.None) > 1.05f)
                {
                    attack = a;
                    defend = d;
                }
            }
            Assert.AreNotEqual(InsectElement.None, attack, "효과가 굉장한 상성 쌍이 표에 없다");

            InsectBattleController battle = Duel(new ScriptedRoll(0f));
            battle.DeferPresentation = true;
            player.Data.skills[0].element = attack;
            enemy.Data.primaryType = defend;
            StunEnemy(battle);

            battle.UseSkill(0);

            List<string> texts = PresentationTexts(battle);
            Assert.AreEqual(0, texts.Count(t => t.Contains("치명타")),
                "「치명타!」는 피해 숫자 위에 뜬다 — 위쪽 효과 문구에도 띄우면 한 타격에 두 번 보인다");
            // 상성도 같다 — 피해 숫자 위 화살표 + 「잘 통했다!」(BattleScreenUI.Feel)가 컨트롤러의 등급 하나를 읽는다.
            // 위쪽 효과 문구(예전 「효과가 굉장했다!」·「효과가 별로인 듯하다...」)로도 띄우면 한 타격에 상성 문구가 두 번 보인다.
            Assert.AreEqual(0, texts.Count(t => t.Contains("효과가")), "상성 문구가 위쪽 효과 문구에도 떴다");
            Assert.AreEqual(0, texts.Count(t => t.Contains("통했다")), "상성 문구는 화면이 피해 숫자 위에 띄운다");
            Assert.IsTrue(ElementMatchup.IsFavorable(battle.LastPlayerHitMatchup), "화면이 읽을 등급이 남아야 한다");
            Assert.IsNotNull(InsectGame.UI.MatchupHud.ImpactText(battle.LastPlayerHitMatchup));
        }

        [Test]
        public void CriticalHit_Neutral_AddsNoTopLine()
        {
            InsectBattleController battle = Duel(new ScriptedRoll(0f));
            battle.DeferPresentation = true;
            StunEnemy(battle);

            battle.UseBasicAttack();

            Assert.IsTrue(battle.LastPlayerHitCritical, "치명타 롤 0 — 치명타여야 한다");
            Assert.AreEqual(0, PresentationTexts(battle).Count(t => t.Contains("치명타")),
                "「치명타!」는 피해 숫자 위에만 뜬다(BattleScreenUI)");
        }

        [Test]
        public void Sandbox_NeverRollsCritical()
        {
            var crit = new ScriptedRoll(0f);
            InsectBattleController battle = NewController(crit);
            InsectData ace = Species("crit_dream_ace", 150, 40, 30);
            InsectData foe = Species("crit_dream_foe", 400, 40, 30);
            InsectSkill hit = Skill("crit_dream_hit", SkillEffectType.Damage, 40);
            ace.skills = new[] { hit };
            foe.skills = new[] { hit };
            PlayerInsectData champion = DreamPrologueData.BuildChampionInsect(ace);
            Assert.IsTrue(battle.StartSandbox(ace, 80, foe, 72, new[] { hit }, champion));

            bool ended = false;
            battle.BattleEnded += _ => ended = true;
            for (int i = 0; i < 8 && !ended; i++)
            {
                if (battle.CanUseSkill(0)) battle.UseSkill(0); else battle.UseBasicAttack();
                Assert.IsFalse(battle.LastPlayerHitCritical);
                Assert.IsFalse(battle.LastEnemyHitCritical);
            }
            Assert.IsTrue(ended);
            Assert.AreEqual(0, crit.Draws, "꿈 챔피언전은 SandboxBattleRules가 길이를 지킨다 — 치명타를 굴리지 않는다");
        }

        [Test]
        public void SetRandomSeed_SeedsTheCritStreamToo()
        {
            List<int> Run()
            {
                // 시드 없는 진짜 줄기로 시작한다 — SetRandomSeed가 치명타 줄기를 안 갈아끼우면 두 실행이 갈린다.
                InsectBattleController battle = Duel(new BattleRandomSource());
                battle.SetRandomSeed(4242);
                var hp = new List<int>();
                for (int i = 0; i < 12; i++)
                {
                    if (battle.CanUseSkill(0)) battle.UseSkill(0); else battle.UseBasicAttack();
                    hp.Add(enemy.CurrentHp);
                    hp.Add(player.CurrentHp);
                }
                return hp;
            }

            CollectionAssert.AreEqual(Run(), Run(), "시드 고정 전투가 치명타 때문에 실행마다 달라졌다");
        }

        // ── 상태 노출 ──

        [Test]
        public void PlayerStunTurns_AreReadableBetweenRounds()
        {
            // 상대의 기절기가 내 곤충에 걸리면 **다음 라운드의 내 행동**이 날아간다 — HP 카드가 그 사이에 읽는 값이다.
            InsectBattleController battle = Duel(null);
            InsectSkill stun = Skill("crit_enemy_stun", SkillEffectType.Stun, 1);
            stun.cooldownTurns = 5;
            enemy.Data.skills = new[] { stun };
            Assert.AreEqual(0, battle.PlayerStunTurns);

            battle.UseBasicAttack();
            Assert.AreEqual(1, battle.PlayerStunTurns, "상대 기절기가 걸렸는데 남은 턴을 읽을 수 없다");

            battle.UseBasicAttack();   // 기절로 건너뛴 차례 — 상대는 쿨다운이라 기본 공격
            Assert.IsFalse(battle.PlayerActedThisRound);
            Assert.AreEqual(0, battle.PlayerStunTurns);
        }

        [Test]
        public void EnemyStunTurns_AreReadable()
        {
            InsectBattleController battle = Duel(null);
            Assert.AreEqual(0, battle.EnemyStunTurns);
            StunEnemy(battle);
            Assert.AreEqual(1, battle.EnemyStunTurns);

            battle.UseBasicAttack();   // 기절은 이번 반격에서 소모된다

            Assert.IsFalse(battle.EnemyActedThisRound);
            Assert.AreEqual(0, battle.EnemyStunTurns);
        }

        [Test]
        public void PoisonTick_IsReportedPerRound_AndClearedByTheNextAction()
        {
            InsectBattleController battle = Duel(null);
            AddEffect(battle, false, 7f, 3, InsectBattleController.EffectKind.Dot);
            AddEffect(battle, true, 5f, 1, InsectBattleController.EffectKind.Dot);

            battle.UseSkill(0);
            Assert.AreEqual(7, battle.EnemyPoisonDamageThisRound);
            Assert.AreEqual(5, battle.PlayerPoisonDamageThisRound);
            Assert.AreEqual(battle.EnemyHpAfterEnemyAction - 7, enemy.CurrentHp, "독은 두 공격 스냅샷 뒤에 들어간다");

            battle.UseBasicAttack();
            Assert.AreEqual(7, battle.EnemyPoisonDamageThisRound, "다음 라운드도 같은 독이 다시 센다(누적 아님)");
            Assert.AreEqual(0, battle.PlayerPoisonDamageThisRound, "지속 1턴짜리 독은 끝났다");
        }

        // ── 전투 화면이 판정을 그대로 옮기는가 ──

        [Test]
        public void Screen_BigNonCriticalHit_IsNotShownAsCritical()
        {
            InsectBattleController battle = Duel(new ScriptedRoll(0.9f));
            player.Data.skills[0].power = 400;   // 적 최대 HP의 25%를 훌쩍 넘는 한 방 — 옛 휴리스틱이면 CRITICAL
            InsectGame.UI.BattleScreenUI ui = Screen(battle);
            StunEnemy(battle);

            Invoke(ui, "TryUseSkill", 0);

            Assert.Greater(enemy.MaxHp - enemy.CurrentHp, enemy.MaxHp / 4);
            Assert.IsFalse((bool)Get(ui, "lastWasCritical"), "치명타가 아닌데 CRITICAL로 표시됐다");
        }

        [Test]
        public void Screen_CriticalFlags_ComeFromTheController()
        {
            InsectBattleController battle = Duel(new ScriptedRoll(0f));
            InsectGame.UI.BattleScreenUI ui = Screen(battle);

            Invoke(ui, "TryBasicAttack");

            Assert.IsTrue((bool)Get(ui, "lastWasCritical"));
            Assert.IsTrue((bool)Get(ui, "lastEnemyWasCritical"), "상대의 치명타도 표시 계산에 실려야 한다");
        }

        [Test]
        public void Screen_ComboShows_OnSecondUnansweredHit_AndBreaksWhenTheReplyLands()
        {
            InsectBattleController battle = Duel(null);
            InsectGame.UI.BattleScreenUI ui = Screen(battle);

            // 1타 — 상대 기절(반격 없음). 콤보 1이라 아직 안 뜬다.
            StunEnemy(battle);
            PlayerTurn(ui);
            Invoke(ui, "TryBasicAttack");
            Invoke(ui, "RevealImpact", true);
            Assert.AreEqual(1, (int)Get(ui, "comboCount"));
            Assert.AreEqual(0f, (float)Get(ui, "comboDisplayTimer"));

            // 2타 — 또 반격 없음. 타격이 보이는 순간 콤보가 뜬다.
            StunEnemy(battle);
            PlayerTurn(ui);
            Invoke(ui, "TryBasicAttack");
            Invoke(ui, "RevealImpact", true);
            Assert.AreEqual(2, (int)Get(ui, "comboCount"));
            Assert.AreEqual(2.5f, (float)Get(ui, "comboDisplayTimer"), 1e-4f);

            // 3타 — 이번엔 반격이 나를 때린다. 내 타격 순간엔 3으로 뜨고, 반격이 보이는 순간 끊긴다.
            PlayerTurn(ui);
            Invoke(ui, "TryBasicAttack");
            Invoke(ui, "RevealImpact", true);
            Assert.AreEqual(3, (int)Get(ui, "comboCount"), "반격 판정만으로 콤보를 미리 끊으면 한 번도 안 뜬다");
            SetPrivate(ui, "impactRevealed", false);
            Invoke(ui, "RevealImpact", false);
            Assert.AreEqual(0, (int)Get(ui, "comboCount"));
            Assert.AreEqual(0f, (float)Get(ui, "comboDisplayTimer"));
        }

        // ── 헬퍼 ──

        private InsectBattleController Duel(IRaidRandomSource crit)
        {
            InsectBattleController battle = NewController(crit);
            InsectData p = Species("crit_player", 1000, 20, 30);
            InsectData e = Species("crit_enemy", 1000, 20, 30);
            p.skills = new[] { Skill("crit_hit_p", SkillEffectType.Damage, 30) };
            e.skills = new[] { Skill("crit_hit_e", SkillEffectType.Damage, 30) };
            Assert.IsTrue(battle.StartDuel(p, Level, e, Level, (a, b) => { player = a; enemy = b; }));
            Assert.AreEqual(player.Attack, enemy.Defense, "공방 비율 1이어야 배율이 그대로 보인다");
            return battle;
        }

        private InsectBattleController NewController(IRaidRandomSource crit)
        {
            InsectBattleController battle = Track(new GameObject("CritBattleFixture")).AddComponent<InsectBattleController>();
            battle.SetCritSource(crit);
            return battle;
        }

        private InsectGame.UI.BattleScreenUI Screen(InsectBattleController battle)
        {
            var ui = Track(new GameObject("CritScreenFixture")).AddComponent<InsectGame.UI.BattleScreenUI>();
            ui.AutoWire(battle, null, null);
            Assert.IsTrue(battle.StartDuel(player.Data, Level, enemy.Data, Level, (a, b) => { player = a; enemy = b; }));
            PlayerTurn(ui);
            return ui;
        }

        private static void PlayerTurn(InsectGame.UI.BattleScreenUI ui)
        {
            FieldInfo phase = typeof(InsectGame.UI.BattleScreenUI).GetField("phase", Inst);
            phase.SetValue(ui, Enum.Parse(phase.FieldType, "PlayerTurn"));
        }

        private static void StunEnemy(InsectBattleController battle) => SetPrivate(battle, "enemyStunTurns", 1);

        private static void AddEffect(InsectBattleController battle, bool targetIsPlayer, float value, int turns,
            InsectBattleController.EffectKind kind)
        {
            typeof(InsectBattleController).GetMethod("AddEffect", Inst)
                .Invoke(battle, new object[] { targetIsPlayer, value, turns, kind });
        }

        private static List<string> PresentationTexts(InsectBattleController battle)
        {
            var list = (System.Collections.IEnumerable)typeof(InsectBattleController).GetField("presentationTexts", Inst).GetValue(battle);
            var texts = new List<string>();
            foreach (object entry in list)
                texts.Add((string)entry.GetType().GetField("Item2").GetValue(entry));
            return texts;
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            MethodInfo m = target.GetType().GetMethod(method, Inst);
            Assert.IsNotNull(m, method);
            m.Invoke(target, args);
        }

        private static object Get(object target, string field)
        {
            FieldInfo f = target.GetType().GetField(field, Inst);
            Assert.IsNotNull(f, field);
            return f.GetValue(target);
        }

        private static void SetPrivate(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field, Inst);
            Assert.IsNotNull(f, field);
            f.SetValue(target, value);
        }

        private InsectData Species(string id, int hp, int atk, int def)
        {
            InsectData d = Track(ScriptableObject.CreateInstance<InsectData>());
            d.insectId = id;
            d.displayName = id;
            d.baseHp = hp;
            d.baseAtk = atk;
            d.baseDef = def;
            d.primaryType = InsectElement.None;
            d.secondaryType = InsectElement.None;
            return d;
        }

        private InsectSkill Skill(string id, SkillEffectType type, int power)
        {
            InsectSkill s = Track(ScriptableObject.CreateInstance<InsectSkill>());
            s.skillId = id;
            s.displayName = id;
            s.effectType = type;
            s.element = InsectElement.None;
            s.power = power;
            s.accuracy = 1f;
            s.cooldownTurns = 0;
            return s;
        }

        private T Track<T>(T obj) where T : Object
        {
            objects.Add(obj);
            return obj;
        }
    }

    /// <summary>
    /// 레이드 쪽 치명타(리졸버의 치명타 줄기)와 쓰러짐 연출 배선(<see cref="RaidBattleController.PresentPendingFaints"/>).
    /// 쓰러짐은 예전에 정의만 있고 호출이 0건이라 보스·팀원이 HP 0으로도 서 있었다.
    /// </summary>
    [TestFixture]
    public class RaidCriticalAndFaintTests
    {
        private readonly List<Object> objects = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (Object obj in objects) if (obj != null) Object.DestroyImmediate(obj);
            objects.Clear();
        }

        [Test]
        public void LeaderSkill_WithoutCritSource_NeverCrits()
        {
            for (int i = 0; i < 30; i++)
            {
                InsectBattleStats boss = Stats("boss", 100000, 20, 30);
                RaidActionResult r = RaidRoundResolver.ResolveLeaderSkill(0, 0, Stats("a", 500, 20, 30), boss,
                    new InsectBattleStats[0], Skill(SkillEffectType.Damage, 30), null);
                Assert.IsFalse(r.Critical);
                Assert.AreEqual(40, r.Damage);
            }
        }

        [Test]
        public void LeaderSkill_CritRoll_FlagsAndMultiplies()
        {
            InsectBattleStats boss = Stats("boss", 100000, 20, 30);
            var crit = new ScriptedRoll(0.0624f);
            RaidActionResult r = RaidRoundResolver.ResolveLeaderSkill(0, 0, Stats("a", 500, 20, 30), boss,
                new InsectBattleStats[0], Skill(SkillEffectType.Damage, 30), null, crit);
            Assert.IsTrue(r.Critical);
            Assert.AreEqual(60, r.Damage, "(30 + Lv10) × 1.5 — 공방 비율 1");
            Assert.AreEqual(1, crit.Draws);

            RaidActionResult plain = RaidRoundResolver.ResolveLeaderSkill(0, 0, Stats("a", 500, 20, 30), boss,
                new InsectBattleStats[0], Skill(SkillEffectType.Damage, 30), null, new ScriptedRoll(0.0626f));
            Assert.IsFalse(plain.Critical);
            Assert.AreEqual(40, plain.Damage);
        }

        [Test]
        public void SupportAssist_And_BossSingle_CanCrit()
        {
            InsectBattleStats boss = Stats("boss", 100000, 20, 30);
            RaidActionResult assist = RaidRoundResolver.ResolveSupportAssist(1, Stats("a", 500, 20, 30), boss, new ScriptedRoll(0f));
            Assert.IsTrue(assist.Critical);

            InsectBattleStats[] team = { Stats("t0", 100000, 20, 30) };
            var intent = RaidRoundResolver.CreateBossIntent(1, boss, team, 2, null, new ScriptedRoll(0f), false);
            int[] bySlot = new int[1];
            RaidActionResult plain = RaidRoundResolver.ResolveBossIntent(intent, boss, team, bySlot);
            int plainDamage = bySlot[0];
            RaidActionResult hit = RaidRoundResolver.ResolveBossIntent(intent, boss, team, bySlot, 1f, new ScriptedRoll(0f));
            Assert.IsFalse(plain.Critical);
            Assert.IsTrue(hit.Critical);
            Assert.AreEqual(Mathf.RoundToInt(plainDamage * 1.5f), bySlot[0], "보스 단일 공격 ×1.5(공방 비율 1)");
        }

        [TestCase(SkillEffectType.BuffAttack)]
        [TestCase(SkillEffectType.DefenseBuff)]
        [TestCase(SkillEffectType.DebuffAttack)]
        [TestCase(SkillEffectType.Heal)]
        [TestCase(SkillEffectType.Stun)]
        [TestCase(SkillEffectType.PoisonDot)]
        public void NonDamageSkill_DoesNotRollCritical(SkillEffectType type)
        {
            var crit = new ScriptedRoll(0f);
            InsectBattleStats caster = Stats("a", 500, 20, 30);
            InsectSkill skill = Skill(type, 6);
            skill.effectValue = 0.2f;
            skill.effectDurationTurns = 2;
            RaidActionResult r = RaidRoundResolver.ResolveLeaderSkill(0, 0, caster, Stats("boss", 100000, 20, 30),
                new[] { caster }, skill, null, crit);
            Assert.AreEqual(0, crit.Draws, $"{type}는 치명타를 굴리지 않는다");
            Assert.IsFalse(r.Critical);
        }

        [Test]
        public void Controller_ActionText_SaysCritical()
        {
            RaidBattleController raid = Raid(new ScriptedRoll(0f), null);
            raid.ResolveTeamCommand(0);
            RaidActionResult action = raid.CurrentRoundResult.TeamActions[0];
            Assert.IsTrue(action.Critical);
            StringAssert.Contains("치명타!", raid.LastActionText);
        }

        [Test]
        public void Controller_SetRandomSourceAlone_KeepsTheCritStreamSeparate()
        {
            // 테스트 스크립트 난수(0)만 주입했을 때 치명타 줄기가 그걸 따라가면 매 타격이 치명타가 된다.
            RaidBattleController raid = Raid(null, null, setCrit: false);
            raid.SetCritSource(new ScriptedRoll(0.9f));
            raid.ResolveTeamCommand(0);
            Assert.IsFalse(raid.CurrentRoundResult.TeamActions[0].Critical);
        }

        [Test]
        public void PresentPendingFaints_AnnouncesEachFaintOnce()
        {
            RaidBattleController raid = Raid(null, null);
            var seen = new List<int>();
            raid.FaintPresented += seen.Add;

            Assert.IsFalse(raid.PresentPendingFaints(), "아무도 안 쓰러졌다");
            CollectionAssert.IsEmpty(seen);

            raid.TeamStats[2].ApplyDamage(999999);
            raid.BossStats.ApplyDamage(99999999);
            Assert.IsFalse(raid.PresentPendingFaints(), "아레나가 없으면 기다릴 쓰러짐도 없다");
            CollectionAssert.AreEquivalent(new[] { -1, 2 }, seen);

            raid.PresentPendingFaints();
            Assert.AreEqual(2, seen.Count, "같은 개체를 두 번 쓰러뜨렸다");
        }

        [Test]
        public void PresentPendingFaints_SkipsMembersAlreadyDownAtStart()
        {
            var pids = new PlayerInsectData[5];
            for (int i = 0; i < 5; i++)
                pids[i] = new PlayerInsectData { instanceId = $"inst_{i}", insectId = $"team_{i}", level = 10, currentHp = i == 1 ? 0 : -1 };
            RaidBattleController raid = Raid(null, pids);
            var seen = new List<int>();
            raid.FaintPresented += seen.Add;

            raid.PresentPendingFaints();

            CollectionAssert.IsEmpty(seen, "시작 전부터 기절해 있던 팀원은 지금 쓰러진 게 아니다");
        }

        // ── 헬퍼 ──

        private RaidBattleController Raid(IRaidRandomSource crit, PlayerInsectData[] pids, bool setCrit = true)
        {
            InsectData bossData = Data("boss", 4000, 20, 30);
            InsectEntity boss = Track(new GameObject("CritRaidBoss")).AddComponent<InsectEntity>();
            SetField(boss, "data", bossData);
            SetField(boss, "level", 10);

            RaidBattleController raid = Track(new GameObject("CritRaidController")).AddComponent<RaidBattleController>();
            raid.SetRandomSource(new ScriptedRoll(0f));
            if (setCrit) raid.SetCritSource(crit);

            var team = new InsectData[5];
            var skills = new InsectSkill[5][];
            InsectSkill hit = Skill(SkillEffectType.Damage, 30);
            for (int i = 0; i < 5; i++)
            {
                team[i] = Data($"team_{i}", 300, 20, 30);
                skills[i] = new[] { hit };
            }
            Assert.IsTrue(raid.StartRaid(boss, team, Enumerable.Repeat(10, 5).ToArray(), pids, skills));
            return raid;
        }

        private InsectBattleStats Stats(string id, int hp, int atk, int def) => new InsectBattleStats(Data(id, hp, atk, def), 10);

        private InsectData Data(string id, int hp, int atk, int def)
        {
            InsectData d = Track(ScriptableObject.CreateInstance<InsectData>());
            d.insectId = id;
            d.displayName = id;
            d.baseHp = hp;
            d.baseAtk = atk;
            d.baseDef = def;
            d.primaryType = InsectElement.None;
            d.secondaryType = InsectElement.None;
            return d;
        }

        private InsectSkill Skill(SkillEffectType type, int power)
        {
            InsectSkill s = Track(ScriptableObject.CreateInstance<InsectSkill>());
            s.skillId = "raid_crit_" + type;
            s.displayName = s.skillId;
            s.effectType = type;
            s.element = InsectElement.None;
            s.power = power;
            s.accuracy = 1f;
            return s;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, name);
            f.SetValue(target, value);
        }

        private T Track<T>(T obj) where T : Object
        {
            objects.Add(obj);
            return obj;
        }
    }

    /// <summary>고정 값을 돌려주며 몇 번 뽑혔는지 센다 — 명중·치명타 줄기 주입용.</summary>
    internal sealed class ScriptedRoll : IRaidRandomSource
    {
        public float Value;
        public int Draws;

        public ScriptedRoll(float value) { Value = value; }

        public float Next01()
        {
            Draws++;
            return Value;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            Draws++;
            return minInclusive;
        }
    }
}
#endif
