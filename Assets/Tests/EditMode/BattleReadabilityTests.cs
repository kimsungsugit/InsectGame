#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InsectGame.Tests
{
    /// <summary>
    /// 전투 체감 강화(초등 고학년) — <b>읽을 시간</b>과 <b>판정 도우미</b>.
    ///
    /// ① 결과 화면은 눌러서 닫는다(처음 0.6초 잠금, 꿈 챔피언전만 저절로) ② 읽는 단계(진입 인트로·컷인·상대 교체·그림자 변신·수문장 등장)는
    /// 2배속이어도 최소 실제 시간을 지킨다 ③ 상성 등급(<see cref="ElementMatchup"/>)과 전용기 판별(<see cref="SignatureSkills"/>)이
    /// 1대1·레이드에서 같은 기준으로 마지막 행동에 남는다 ④ 전투 종류·전투 곡 고르기.
    ///
    /// 실패는 전부 조용하다 — 결과 화면이 다시 저절로 닫혀도, 2배속 문구가 0.6초에 사라져도 예외 하나 없다.
    /// </summary>
    [TestFixture]
    public class BattleReadabilityTests
    {
        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Frame = 1f / 60f;
        private readonly List<Object> objects = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (Object o in objects) if (o != null) Object.DestroyImmediate(o);
            objects.Clear();
        }

        // ── ① 결과 화면 닫기 ──

        [Test]
        public void ResultPress_IsIgnoredForTheFirstSixTenths()
        {
            Assert.AreEqual(0.6f, BattleResultRules.InputLockSeconds, 1e-6f);
            Assert.IsFalse(BattleResultRules.AcceptsPress(0f, true), "마지막 공격 연타가 결과를 바로 닫는다");
            Assert.IsFalse(BattleResultRules.AcceptsPress(0.59f, true));
            Assert.IsTrue(BattleResultRules.AcceptsPress(0.6f, true));
            Assert.IsTrue(BattleResultRules.AcceptsPress(120f, true));
            Assert.IsFalse(BattleResultRules.AcceptsPress(120f, false), "누르지 않으면 받을 것도 없다");
            Assert.IsFalse(BattleResultRules.CanClose(0.59f));
            Assert.IsTrue(BattleResultRules.CanClose(0.6f));
        }

        [Test]
        public void Result_NeverClosesByItself_OutsideTheDream()
        {
            // 예전 자동 닫힘 시각(1대1 4초·레이드 5초)과 그 훨씬 뒤까지 — 보상·한마디를 다 읽을 때까지 기다린다.
            foreach (float t in new[] { 0f, 4.01f, 5.01f, 60f, 3600f })
                Assert.IsFalse(BattleResultRules.ShouldAutoClose(t, sandbox: false), $"{t}초에 저절로 닫혔다");
        }

        [Test]
        public void DreamChampionResult_StillClosesByItself_AfterFourSeconds()
        {
            // 꿈 지휘자는 결과 화면이 닫히는 것(IsBattleActive)을 신호로 섬에 넘어가고, 그 상한은 14초다.
            Assert.IsFalse(BattleResultRules.ShouldAutoClose(BattleResultRules.SandboxAutoCloseSeconds, sandbox: true));
            Assert.IsTrue(BattleResultRules.ShouldAutoClose(BattleResultRules.SandboxAutoCloseSeconds + 0.01f, sandbox: true));
            Assert.Less(BattleResultRules.SandboxAutoCloseSeconds, 14f, "꿈 지휘자의 상한보다 먼저 닫혀야 결과 화면 위로 섬이 열리지 않는다");
        }

        [Test]
        public void AcceptedPress_ClosesOnRelease_OrAfterAShortHold()
        {
            Assert.IsTrue(BattleResultRules.ReleaseComplete(inputHeld: false, secondsSincePress: 0f));
            Assert.IsFalse(BattleResultRules.ReleaseComplete(inputHeld: true, secondsSincePress: 0.49f),
                "누른 프레임에 닫으면 그 탭이 필드 클릭-이동으로 샌다");
            Assert.IsTrue(BattleResultRules.ReleaseComplete(true, BattleResultRules.ReleaseGraceSeconds),
                "뗌을 못 받아도(포커스 잃음) 갇히지 않는다");
        }

        [Test]
        public void Screen_Result_WaitsForATap_IgnoresEarlyTaps_ThenClosesOnTheKey()
        {
            InsectBattleController battle = NewController();
            BattleScreenUI ui = NewScreen(battle);
            Assert.IsTrue(battle.StartDuel(Species("res_me", InsectElement.Bug), 10, Species("res_foe", InsectElement.Bug), 10));
            ShowResult(ui);
            Assert.AreEqual(0f, ui.ResultShownSeconds, 1e-6f);

            SetField(ui, "resultTimer", 30f);
            TickResult(ui);
            Assert.AreEqual("Result", PhaseOf(ui), "30초가 지나도 저절로 닫히지 않는다");
            Assert.AreEqual(30f, ui.ResultShownSeconds, 1e-6f);

            SetField(ui, "resultTimer", 0.3f);
            SetField(ui, "wantMouseClick", true);
            Assert.IsFalse(ui.ResultCanClose);
            TickResult(ui);
            Assert.AreEqual("Result", PhaseOf(ui), "잠금 중 탭은 버린다");
            Assert.IsFalse((bool)GetField(ui, "resultCloseArmed"), "잠금 중 탭을 쌓아 두면 잠금이 풀리는 순간 닫힌다");

            SetField(ui, "resultTimer", 0.7f);
            Assert.IsTrue(ui.ResultCanClose);
            SetField(ui, "wantResultClose", true);   // Space/Enter — OnGUI가 세운다
            TickResult(ui);
            Assert.AreEqual("None", PhaseOf(ui), "잠금 뒤 누름은 손을 뗀 프레임에 닫힌다(배치 러너엔 누른 손가락이 없다)");
            Assert.IsFalse(ui.ResultCanClose);
            Assert.AreEqual(0f, ui.ResultShownSeconds, 1e-6f);
        }

        [Test]
        public void Screen_DreamChampionResult_ClosesByItself()
        {
            InsectBattleController battle = NewController();
            BattleScreenUI ui = NewScreen(battle);
            InsectData ace = Species("dream_ace", InsectElement.Bug);
            ace.skills = new[] { Skill("dream_hit", InsectElement.Bug, false) };
            Assert.IsTrue(battle.StartSandbox(ace, 30, Species("dream_foe", InsectElement.Bug), 30, null, null));
            ShowResult(ui);

            SetField(ui, "resultTimer", 3.9f);
            TickResult(ui);
            Assert.AreEqual("Result", PhaseOf(ui));
            SetField(ui, "resultTimer", 4.1f);
            TickResult(ui);
            Assert.AreEqual("None", PhaseOf(ui), "꿈 흐름은 결과 화면이 닫히는 신호로 섬에 넘어간다");
        }

        // ── ② 읽는 단계의 배속 하한 ──

        [Test]
        public void ClockScale_AtNormalSpeed_LeavesEveryPhaseAsAuthored()
        {
            foreach ((float nominal, float min, string what) in ReadPhases())
            {
                Assert.AreEqual(1f, BattleReadPacing.ClockScale(nominal, min, 1f), 1e-6f, what);
                Assert.AreEqual(nominal, BattleReadPacing.RealSeconds(nominal, min, 1f), 1e-4f, what);
                Assert.AreEqual(nominal, BattleReadPacing.StagingSeconds(nominal, min, 1f), 1e-4f, what);
            }
        }

        [Test]
        public void ReadPhases_AtDoubleSpeed_LastAtLeastTheirFloor()
        {
            foreach ((float nominal, float min, string what) in ReadPhases())
            {
                Assert.GreaterOrEqual(nominal, min, $"{what}: 1배속 길이가 하한보다 짧으면 1배속도 늘어난다");
                float expected = Mathf.Max(min, nominal / 2f);
                Assert.AreEqual(expected, BattleReadPacing.RealSeconds(nominal, min, 2f), 1e-4f, what);

                // 실제 화면처럼 프레임마다 단계 시계를 올려 끝나는 실제 시간을 잰다.
                float scale = BattleReadPacing.ClockScale(nominal, min, 2f);
                float real = Simulate(nominal, _ => scale, 2f);
                Assert.GreaterOrEqual(real, min - Frame, $"{what}: 2배속에서 {real:0.00}초 — 문구를 못 읽는다");
                Assert.LessOrEqual(real, expected + Frame * 2f, what);
            }
        }

        [Test]
        public void ReadPhase_StillSpeedsUp_DownToItsFloor()
        {
            // 배속을 고른 사람에게 1배속을 강요하지 않는다 — 하한까지만 늦춘다.
            Assert.AreEqual(1.0f, BattleReadPacing.RealSeconds(BattleScreenUI.EnemySwitchSeconds, BattleReadPacing.EnemySwitchMinSeconds, 2f), 1e-4f);
            Assert.Less(BattleReadPacing.RealSeconds(BattleScreenUI.EnemySwitchSeconds, BattleReadPacing.EnemySwitchMinSeconds, 2f),
                BattleScreenUI.EnemySwitchSeconds);
            // 하한보다 넉넉히 긴 단계는 배속을 그대로 탄다.
            Assert.AreEqual(1f, BattleReadPacing.ClockScale(3f, 1f, 2f), 1e-6f);
            Assert.AreEqual(1.5f, BattleReadPacing.RealSeconds(3f, 1f, 2f), 1e-4f);
        }

        [Test]
        public void StagingSeconds_EndsTogetherWithThePhase()
        {
            // 아레나 연출은 배속 시계로 돈다 — 넘긴 길이 ÷ 배속 = 단계의 실제 길이여야 박자(교체 착지·변신 울부짖음)가 문구와 맞는다.
            foreach ((float nominal, float min, string what) in ReadPhases())
            {
                float staging = BattleReadPacing.StagingSeconds(nominal, min, 2f);
                Assert.AreEqual(BattleReadPacing.RealSeconds(nominal, min, 2f), staging / 2f, 1e-4f, what);
            }
            Assert.AreEqual(2.0f, BattleReadPacing.StagingSeconds(BattleScreenUI.EnemySwitchSeconds, BattleReadPacing.EnemySwitchMinSeconds, 2f), 1e-4f);
            Assert.AreEqual(2.0f, BattleReadPacing.StagingSeconds(RaidBattleUI.BossTransformDuration, BattleReadPacing.BossTransformMinSeconds, 2f), 1e-4f);
        }

        [Test]
        public void EntryIntro_RunsInRealTime_AtAnySpeed()
        {
            Assert.AreEqual(1.4f, BattleReadPacing.EntryIntroSeconds, 1e-6f);
            foreach (float speed in new[] { 1f, 2f })
            {
                float real = Simulate(BattleReadPacing.EntryIntroSeconds,
                    clock => BattleReadPacing.IntroClockScale(clock, 0f, speed), speed);
                Assert.AreEqual(BattleReadPacing.EntryIntroSeconds, real, Frame * 1.5f, $"{speed}배속 — 진입 샷·「나타났다!」 자리");
            }
            Assert.AreEqual(0f, BattleReadPacing.EntryProgress(0f));
            Assert.AreEqual(0.5f, BattleReadPacing.EntryProgress(0.7f), 1e-5f);
            Assert.AreEqual(1f, BattleReadPacing.EntryProgress(5f));
        }

        [Test]
        public void IntroWithCutIn_PlaysTheEntryFirst_ThenTheCutIn()
        {
            const float CutIn = 3.2f;   // BattleScreenUI.Duel.CutInSeconds
            Assert.AreEqual(CutIn, (float)typeof(BattleScreenUI).GetField("CutInSeconds", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), 1e-6f);
            float total = BattleReadPacing.IntroSeconds(CutIn, 2f);
            Assert.AreEqual(BattleReadPacing.EntryIntroSeconds + CutIn, total, 1e-5f);
            Assert.Less(BattleReadPacing.CutInElapsed(BattleReadPacing.EntryIntroSeconds - 0.01f), 0f, "진입 구간엔 컷인을 그리지 않는다");
            Assert.AreEqual(0f, BattleReadPacing.CutInElapsed(BattleReadPacing.EntryIntroSeconds), 1e-6f);

            float atOne = Simulate(total, clock => BattleReadPacing.IntroClockScale(clock, CutIn, 1f), 1f);
            float atTwo = Simulate(total, clock => BattleReadPacing.IntroClockScale(clock, CutIn, 2f), 2f);
            Assert.AreEqual(total, atOne, Frame * 2f, "1배속은 그대로");
            Assert.AreEqual(BattleReadPacing.EntryIntroSeconds + BattleReadPacing.CutInMinSeconds, atTwo, Frame * 3f,
                "2배속 — 진입 1.4초(실제) + 컷인 하한 2.0초");
        }

        [Test]
        public void PlainIntro_HoldsTheEntry_AndKeepsItsOneTimesLength()
        {
            Assert.AreEqual(2f, BattleReadPacing.IntroSeconds(0f, 2f), 1e-6f, "야생 인트로는 1배속 2초 그대로 — 앞 1.4초가 진입 구간");
            Assert.AreEqual(BattleReadPacing.EntryIntroSeconds, BattleReadPacing.IntroSeconds(0f, 1f), 1e-6f, "진입 구간보다 짧아지지 않는다");
            float atTwo = Simulate(2f, clock => BattleReadPacing.IntroClockScale(clock, 0f, 2f), 2f);
            Assert.AreEqual(BattleReadPacing.EntryIntroSeconds + (2f - BattleReadPacing.EntryIntroSeconds) / 2f, atTwo, Frame * 3f,
                "진입 구간 뒤의 나머지는 배속을 탄다");
        }

        [Test]
        public void Screen_IntroProgress_TracksTheEntryAndTheWholeIntro()
        {
            InsectBattleController battle = NewController();
            BattleScreenUI ui = NewScreen(battle);
            Assert.AreEqual(0f, ui.IntroProgress);
            Assert.IsTrue(battle.StartDuel(Species("intro_me", InsectElement.Bug), 10, Species("intro_foe", InsectElement.Bug), 10));
            Assert.IsTrue(ui.IsIntroPlaying);
            Assert.AreEqual(0f, ui.IntroProgress, 1e-6f);
            Assert.AreEqual(0f, ui.EntryIntroProgress, 1e-6f);
            SetField(ui, "introTimer", 0.7f);
            Assert.AreEqual(0.5f, ui.EntryIntroProgress, 1e-5f);
            Assert.AreEqual(0.35f, ui.IntroProgress, 1e-5f, "야생 인트로 2초 중 0.7초");
        }

        // ── ③ 상성 등급 ──

        [Test]
        public void Describe_RealChartValues_LandInTheirGrades()
        {
            Assert.AreEqual(Matchup.Super, ElementMatchup.Describe(InsectTypeChart.GetEffectiveness(InsectElement.Wind, InsectElement.Bug, InsectElement.Leaf)), "강×강 2.25");
            Assert.AreEqual(Matchup.Good, ElementMatchup.Describe(InsectTypeChart.StrongMultiplier), "강 1.5");
            Assert.AreEqual(Matchup.Neutral, ElementMatchup.Describe(1f));
            Assert.AreEqual(Matchup.Neutral, ElementMatchup.Describe(InsectTypeChart.GetEffectiveness(InsectElement.Bug, InsectElement.Leaf, InsectElement.Wind)), "강×약 1.005는 보통");
            Assert.AreEqual(Matchup.Weak, ElementMatchup.Describe(InsectTypeChart.ResistMultiplier), "약 0.67");
            Assert.AreEqual(Matchup.Resisted, ElementMatchup.Describe(InsectTypeChart.GetEffectiveness(InsectElement.Bug, InsectElement.Wind, InsectElement.Metal)), "약×약 0.45");
        }

        [Test]
        public void Describe_Boundaries()
        {
            Assert.AreEqual(Matchup.Super, ElementMatchup.Describe(2.0f));
            Assert.AreEqual(Matchup.Good, ElementMatchup.Describe(1.9999f));
            Assert.AreEqual(Matchup.Good, ElementMatchup.Describe(1.0501f));
            Assert.AreEqual(Matchup.Neutral, ElementMatchup.Describe(1.05f));
            Assert.AreEqual(Matchup.Neutral, ElementMatchup.Describe(0.95f));
            Assert.AreEqual(Matchup.Weak, ElementMatchup.Describe(0.9499f));
            Assert.AreEqual(Matchup.Weak, ElementMatchup.Describe(0.5001f));
            Assert.AreEqual(Matchup.Resisted, ElementMatchup.Describe(0.5f));
            Assert.AreEqual(Matchup.Neutral, ElementMatchup.Describe(float.NaN));
            Assert.AreEqual(Matchup.Neutral, default(Matchup), "기본값이 보통이어야 표지를 안 채운 행동이 보통으로 읽힌다");
        }

        [Test]
        public void Describe_AgreesWithTheBattleText_ForEveryPairInTheChart()
        {
            // 잘 통함(> 1.05)·안 통함(< 0.95) 경계가 피해 숫자 위 상성 표시·스킬 카드 칩(MatchupHud)과 갈리지 않는다. 등급마다 실제로 나오는 배수도 하나다.
            var seen = new Dictionary<Matchup, HashSet<float>>();
            foreach (InsectElement attack in Enum.GetValues(typeof(InsectElement)))
            foreach (InsectElement primary in Enum.GetValues(typeof(InsectElement)))
            foreach (InsectElement secondary in Enum.GetValues(typeof(InsectElement)))
            {
                float e = InsectTypeChart.GetEffectiveness(attack, primary, secondary);
                Matchup m = ElementMatchup.Describe(e);
                Assert.AreEqual(e > 1.05f, ElementMatchup.IsFavorable(m), $"{attack}→{primary}/{secondary} ×{e}");
                Assert.AreEqual(e < 0.95f, ElementMatchup.IsUnfavorable(m), $"{attack}→{primary}/{secondary} ×{e}");
                if (!seen.TryGetValue(m, out HashSet<float> values)) seen[m] = values = new HashSet<float>();
                values.Add(Mathf.Round(e * 1000f) / 1000f);
            }
            Assert.AreEqual(5, seen.Count, "실제 표에서 다섯 등급이 모두 나온다");
            Assert.AreEqual(1, seen[Matchup.Super].Count);
            Assert.AreEqual(1, seen[Matchup.Good].Count);
            Assert.AreEqual(1, seen[Matchup.Weak].Count);
            Assert.AreEqual(1, seen[Matchup.Resisted].Count);
        }

        [Test]
        public void Of_ReadsBothDefenderTypes()
        {
            Assert.AreEqual(Matchup.Super, ElementMatchup.Of(InsectElement.Wind, Species("of_bugleaf", InsectElement.Bug, InsectElement.Leaf)));
            Assert.AreEqual(Matchup.Neutral, ElementMatchup.Of(InsectElement.Wind, null));
            Assert.AreEqual(Matchup.Neutral, ElementMatchup.Of(InsectElement.None, Species("of_leaf", InsectElement.Leaf)), "무속성은 상성을 안 탄다");
        }

        // ── ③ 전용기 판별 — 실제 곤충 DB ──

        [Test]
        public void RealDatabase_Signatures_AreRecognisedOnlyOnTheirOwner()
        {
            InsectDatabase db = RealDatabase();
            var owners = new List<InsectData>();
            foreach (InsectData d in db.insects)
            {
                if (d == null || d.learnset == null) continue;
                InsectSkill signature = SignatureSkills.FindSignature(d);
                foreach (InsectLearnableSkill l in d.learnset)
                {
                    if (l == null || l.skill == null) continue;
                    if (SignatureSkills.IsSignatureEntry(l))
                    {
                        Assert.IsTrue(SignatureSkills.IsSignature(d, l.skill), $"{d.insectId}의 전용기 {l.skill.skillId}");
                        Assert.IsTrue(SignatureSkills.IsUnlocked(l, l.learnLevel));
                        Assert.IsFalse(SignatureSkills.IsUnlocked(l, l.learnLevel - 1), "해금 레벨 전에는 레이드 보스도 못 쓴다");
                    }
                    else
                    {
                        Assert.IsFalse(SignatureSkills.IsSignature(d, l.skill), $"{d.insectId}의 {l.skill.skillId}는 종족기다");
                    }
                }
                if (signature != null) owners.Add(d);
                if (d.rarity >= InsectRarity.Epic)
                    Assert.IsNotNull(signature, $"{d.insectId}({d.rarity})에 전용기가 없다 — 전용기 연출이 영영 안 뜬다");
            }

            Assert.GreaterOrEqual(owners.Count, 5, "실제 DB에 전용기를 가진 곤충이 너무 적다");
            // 남의 전용기는 내 전용기가 아니다(같은 기술을 남이 들고 있어도 그 곤충 learnset에 없으면 false).
            InsectSkill first = SignatureSkills.FindSignature(owners[0]);
            InsectSkill second = SignatureSkills.FindSignature(owners[1]);
            Assert.AreNotEqual(first.skillId, second.skillId);
            Assert.IsFalse(SignatureSkills.IsSignature(owners[1], first));
            Assert.IsFalse(SignatureSkills.IsSignature(owners[0], second));
        }

        [Test]
        public void RealDatabase_DreamChampion_SignatureIsTheirs_EvenAsAnotherInstance()
        {
            InsectDatabase db = RealDatabase();
            InsectData ace = db.GetById(DreamPrologueData.AceInsectId);
            Assert.IsNotNull(ace);
            InsectSkill signature = SignatureSkills.FindSignature(ace);
            Assert.IsNotNull(signature, "꿈의 챔피언(전설)이 전용기를 든다");
            // 장착 기술이 learnset과 다른 인스턴스로 와도 ID로 알아본다.
            InsectSkill copy = Track(ScriptableObject.CreateInstance<InsectSkill>());
            copy.skillId = signature.skillId;
            copy.isSignatureSkill = true;
            Assert.IsTrue(SignatureSkills.IsSignature(ace, copy));
            copy.isSignatureSkill = false;
            Assert.IsFalse(SignatureSkills.IsSignature(ace, copy), "전용기 표시가 없는 기술은 전용기가 아니다");
            Assert.IsFalse(SignatureSkills.IsSignature(null, signature));
            Assert.IsFalse(SignatureSkills.IsSignature(ace, null));
        }

        [Test]
        public void RealDatabase_RaidBossPicksASignature_TheSameRuleSaysIsTheirs()
        {
            InsectDatabase db = RealDatabase();
            MethodInfo pick = typeof(RaidBattleController).GetMethod("GetUnlockedBossSignature", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(pick, "레이드 보스의 전용기 고르기가 이름을 바꿨다 — 이 테스트를 따라 고칠 것");
            int checkedBosses = 0;
            foreach (InsectData d in db.insects)
            {
                if (d == null || d.rarity < InsectRarity.Epic) continue;
                var skill = (InsectSkill)pick.Invoke(null, new object[] { d, 80, 0 });
                Assert.IsNotNull(skill, d.insectId);
                Assert.IsTrue(SignatureSkills.IsSignature(d, skill), $"{d.insectId}: 보스가 고른 기술을 전용기 판별이 모른다");
                Assert.IsNull(pick.Invoke(null, new object[] { d, 1, 0 }), $"{d.insectId}: Lv1 보스가 전용기를 쓴다");
                checkedBosses++;
            }
            Assert.Greater(checkedBosses, 0);
        }

        // ── ③ 마지막 행동에 남는 표지 ──

        [Test]
        public void OneVsOne_LastAction_ReportsSignatureAndMatchup()
        {
            // 내 곤충: 땅 — 전용기 풀(물에 강하다 ×1.5 = Good). 상대: 물 — 전용기 벌레(땅에는 보통).
            InsectData me = Species("sig_me", InsectElement.Earth);
            InsectSkill mySig = Skill("sig_me_signature", InsectElement.Leaf, true);
            me.learnset = new[] { Learn(mySig, 1) };
            me.skills = new[] { mySig };
            InsectData foe = Species("sig_foe", InsectElement.Water);
            InsectSkill foeSig = Skill("sig_foe_signature", InsectElement.Bug, true);
            foe.learnset = new[] { Learn(foeSig, 1) };
            foe.skills = new[] { foeSig };

            InsectBattleController battle = NewController();
            Assert.IsTrue(battle.StartDuel(me, 10, foe, 10));
            battle.UseSkill(0);

            Assert.AreSame(mySig, battle.LastPlayerSkill);
            Assert.IsTrue(battle.LastPlayerSkillIsSignature);
            Assert.AreEqual(Matchup.Good, battle.LastPlayerHitMatchup);
            Assert.AreSame(foeSig, battle.LastEnemySkill);
            Assert.IsTrue(battle.LastEnemySkillIsSignature);
            Assert.AreEqual(Matchup.Neutral, battle.LastEnemyHitMatchup);

            // 다음 라운드(기본 공격)는 새로 시작한다 — 지난 라운드의 전용기·상성이 남지 않는다.
            battle.UseBasicAttack();
            Assert.IsNull(battle.LastPlayerSkill);
            Assert.IsFalse(battle.LastPlayerSkillIsSignature);
            Assert.AreEqual(Matchup.Neutral, battle.LastPlayerHitMatchup);
        }

        [Test]
        public void OneVsOne_SpeciesSkill_IsNotASignature_AndAMissLeavesNoMatchup()
        {
            InsectData me = Species("plain_me", InsectElement.Earth);
            InsectSkill jab = Skill("plain_jab", InsectElement.Leaf, false);
            jab.accuracy = 0.31f;
            me.learnset = new[] { Learn(jab, 1) };
            me.skills = new[] { jab };
            InsectBattleController battle = NewController();
            battle.SetRandomSource(new FixedRoll(0.99f));   // 명중 롤 0.99 ≥ 0.31 → 빗나감
            Assert.IsTrue(battle.StartDuel(me, 10, Species("plain_foe", InsectElement.Water), 10));
            battle.UseSkill(0);
            Assert.IsFalse(battle.LastPlayerSkillIsSignature);
            Assert.AreEqual(Matchup.Neutral, battle.LastPlayerHitMatchup, "빗나간 기술에 「잘 통해요」가 뜨면 안 된다");
        }

        [Test]
        public void Raid_ActionResults_ReportSignatureAndMatchup()
        {
            InsectData me = Species("raid_me", InsectElement.Earth);
            InsectSkill mySig = Skill("raid_me_signature", InsectElement.Leaf, true);
            me.learnset = new[] { Learn(mySig, 1) };
            InsectData bossData = Species("raid_boss", InsectElement.Water);
            InsectSkill bossSig = Skill("raid_boss_signature", InsectElement.Bug, true);
            bossData.learnset = new[] { Learn(bossSig, 1) };

            var attacker = new InsectBattleStats(me, 10);
            var boss = new InsectBattleStats(bossData, 10);
            var team = new[] { attacker };

            RaidActionResult leader = RaidRoundResolver.ResolveLeaderSkill(0, 0, attacker, boss, team, mySig, null);
            Assert.IsTrue(leader.IsSignature);
            Assert.AreEqual(Matchup.Good, leader.Matchup, "풀 → 물");

            RaidActionResult assist = RaidRoundResolver.ResolveSupportAssist(0, attacker, boss);
            Assert.IsFalse(assist.IsSignature, "지원 공격엔 기술이 없다");
            Assert.AreEqual(Matchup.Weak, assist.Matchup, "땅(주속성) → 물");

            var single = new RaidBossIntent
            {
                Kind = RaidBossIntentKind.SignatureSkill, TargetSlot = 0, Skill = bossSig,
                Element = bossSig.element, EffectType = SkillEffectType.Damage, DisplayName = bossSig.displayName
            };
            RaidActionResult bossHit = RaidRoundResolver.ResolveBossIntent(single, boss, team, new int[1]);
            Assert.IsTrue(bossHit.IsSignature, "보스 전용기는 보스의 지금 모습(CombatData)으로 잰다");
            Assert.AreEqual(Matchup.Neutral, bossHit.Matchup, "벌레 → 땅");

            var area = new RaidBossIntent { Kind = RaidBossIntentKind.AreaAttack, Element = InsectElement.Water, DisplayName = "전체" };
            RaidActionResult areaHit = RaidRoundResolver.ResolveBossIntent(area, boss, team, new int[1]);
            Assert.IsFalse(areaHit.IsSignature);
            Assert.AreEqual(Matchup.Neutral, areaHit.Matchup, "전체공격은 상성을 안 탄다");
        }

        // ── ④ 전투 종류·곡 ──

        [Test]
        public void BattleKinds_Classify_PicksTheRightKind()
        {
            Assert.AreEqual(BattleKind.Sandbox, BattleKinds.Classify(true, true, true, true, true), "꿈이 무엇보다 먼저");
            Assert.AreEqual(BattleKind.Guardian, BattleKinds.Classify(false, true, false, false, false));
            Assert.AreEqual(BattleKind.Wild, BattleKinds.Classify(false, false, false, false, false));
            Assert.AreEqual(BattleKind.KidDuel, BattleKinds.Classify(false, false, true, false, false));
            Assert.AreEqual(BattleKind.BossDuel, BattleKinds.Classify(false, false, true, true, false));
            Assert.AreEqual(BattleKind.RivalDuel, BattleKinds.Classify(false, false, true, true, true));
            Assert.IsTrue(BattleKinds.IsTrainerDuel(BattleKind.KidDuel));
            Assert.IsFalse(BattleKinds.IsTrainerDuel(BattleKind.Guardian));
        }

        [Test]
        public void Screen_CurrentBattleKind_FollowsTheControllerMarks()
        {
            Assert.AreEqual(BattleKind.KidDuel, KindAfter(b => b.StartDuel(Species("k1", InsectElement.Bug), 5, Species("k2", InsectElement.Bug), 5)));
            Assert.AreEqual(BattleKind.RivalDuel, KindAfter(b =>
            {
                b.StartDuel(Species("r1", InsectElement.Bug), 5, Species("r2", InsectElement.Bug), 5);
                b.SetDuelOpponent("rival_final");
            }));
            Assert.AreEqual(BattleKind.BossDuel, KindAfter(b =>
            {
                b.StartDuel(Species("b1", InsectElement.Bug), 5, Species("b2", InsectElement.Bug), 5);
                b.SetDuelOpponent("ledger_grip");
            }));
            Assert.AreEqual(BattleKind.Sandbox, KindAfter(b =>
                b.StartSandbox(Species("s1", InsectElement.Bug), 5, Species("s2", InsectElement.Bug), 5, null, null)));
        }

        [Test]
        public void BattleMusic_GuardianRaid_Rival_AndLedger()
        {
            Assert.AreEqual(BgmType.RaidBattle, BattleMusic.Raid(null));
            Assert.AreEqual(BgmType.RaidBattle, BattleMusic.Raid(string.Empty));
            Assert.AreEqual(BgmType.Guardian, BattleMusic.Raid("meadow"));
            Assert.AreEqual(BgmType.BossFinal, BattleMusic.BossDuel(true));
            Assert.AreEqual(BgmType.BossLedger, BattleMusic.BossDuel(false));
            Assert.AreEqual(BgmType.Rival, BattleMusic.RivalDuel);
        }

        [Test]
        public void BattleMusic_OneVsOneGuardian_UsesTheGuardianTheme()
        {
            // 초원 사마귀처럼 1대1로 맞서는 수문장도 레이드 수문장과 같은 곡이다.
            Assert.AreEqual(BgmType.Battle, BattleMusic.OneVsOne(null));
            Assert.AreEqual(BgmType.Battle, BattleMusic.OneVsOne(string.Empty));
            Assert.AreEqual(BgmType.Guardian, BattleMusic.OneVsOne("meadow"));
        }

        [Test]
        public void Wiring_RivalDuelAndRaidStart_SwitchTheMusic()
        {
            // 배선 누락은 무증상이다(일반 전투 곡이 그대로 흐를 뿐) — 소스로 고정한다.
            string duel = Source("Scripts/NPC/NpcDuelController.cs");
            int rival = duel.IndexOf("public bool TryStartRivalDuel", StringComparison.Ordinal);
            int nextMember = duel.IndexOf("private void OnRivalDuelEnded", StringComparison.Ordinal);
            Assert.Greater(rival, 0);
            Assert.Greater(nextMember, rival);
            StringAssert.Contains("BattleMusic.RivalDuel", duel.Substring(rival, nextMember - rival), "라온 대결이 라온 곡으로 안 바뀐다");
            StringAssert.Contains("BattleMusic.BossDuel(", duel, "간부 대결 곡 분기가 사라졌다");
            StringAssert.Contains("BattleMusic.Raid(raidController.BossGuardianRegionId)", Source("Scripts/UI/RaidBattleUI.cs"),
                "수문장 레이드가 수문장 곡으로 안 바뀐다");
        }

        // ── 도우미 ──

        private static IEnumerable<(float nominal, float min, string what)> ReadPhases()
        {
            float raidIntro = (float)typeof(RaidBattleUI).GetField("RaidIntroSeconds", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            yield return (BattleScreenUI.EnemySwitchSeconds, BattleReadPacing.EnemySwitchMinSeconds, "상대 교체");
            yield return (RaidBattleUI.BossTransformDuration, BattleReadPacing.BossTransformMinSeconds, "그림자 변신");
            yield return (BattleStaging.GuardianIntroSeconds, BattleReadPacing.GuardianIntroMinSeconds, "수문장 등장");
            yield return (raidIntro, BattleReadPacing.RaidIntroMinSeconds, "일반 레이드 인트로");
        }

        /// <summary>실제 화면처럼 60fps로 단계 시계를 올려 <paramref name="nominal"/>에 닿기까지의 실제 초.</summary>
        private static float Simulate(float nominal, Func<float, float> scaleAt, float speed)
        {
            float clock = 0f, real = 0f;
            while (clock < nominal && real < 60f)
            {
                clock += Frame * speed * scaleAt(clock);
                real += Frame;
            }
            return real;
        }

        private BattleKind KindAfter(Action<InsectBattleController> start)
        {
            InsectBattleController battle = NewController();
            BattleScreenUI ui = NewScreen(battle);
            start(battle);
            return ui.CurrentBattleKind;
        }

        private InsectBattleController NewController()
        {
            InsectBattleController battle = Track(new GameObject("ReadabilityBattle")).AddComponent<InsectBattleController>();
            battle.SetCritSource(null);   // 피해·HP를 보는 테스트는 치명타를 끈다(balance.md)
            return battle;
        }

        private BattleScreenUI NewScreen(InsectBattleController battle)
        {
            BattleScreenUI ui = Track(new GameObject("ReadabilityScreen")).AddComponent<BattleScreenUI>();
            ui.AutoWire(battle, null, null);
            return ui;
        }

        private static void ShowResult(BattleScreenUI ui)
        {
            SetField(ui, "lastWon", true);   // 패배면 EnterResult가 컨트롤러 종료(ConcludeDefeatWithoutSwap)까지 부른다 — 여기선 화면만 본다
            typeof(BattleScreenUI).GetMethod("EnterResult", Inst).Invoke(ui, null);
            Assert.AreEqual("Result", PhaseOf(ui));
        }

        // Update는 실제 프레임 시간(unscaledDeltaTime)을 결과 시계에 더한다 — 경계를 정확히 보려고 닫기 판정만 직접 부른다.
        private static void TickResult(BattleScreenUI ui) => typeof(BattleScreenUI).GetMethod("TickResultClose", Inst).Invoke(ui, null);

        private static string PhaseOf(BattleScreenUI ui) => GetField(ui, "phase").ToString();

        private static object GetField(object target, string name) => target.GetType().GetField(name, Inst).GetValue(target);

        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Inst).SetValue(target, value);

        private static string Source(string relative) => File.ReadAllText(Path.Combine(Application.dataPath, relative));

        private InsectDatabase RealDatabase()
        {
            var host = Track(new GameObject("BattleReadabilityTests_RealDb"));
            host.SetActive(false);   // Awake(월드 생성)를 막고 private 생성 함수만 부른다
            var bootstrap = host.AddComponent<PlaySceneBootstrap>();
            var db = (InsectDatabase)typeof(PlaySceneBootstrap)
                .GetMethod("EnsureExpandedDatabase", Inst).Invoke(bootstrap, null);
            Assert.IsNotNull(db);
            Track(db);
            return db;
        }

        private InsectData Species(string id, InsectElement primary, InsectElement secondary = InsectElement.None)
        {
            InsectData d = Track(ScriptableObject.CreateInstance<InsectData>());
            d.insectId = id;
            d.displayName = id;
            d.rarity = InsectRarity.Common;
            d.baseHp = 1000;
            d.baseAtk = 30;
            d.baseDef = 30;
            d.primaryType = primary;
            d.secondaryType = secondary;
            return d;
        }

        private InsectSkill Skill(string id, InsectElement element, bool signature)
        {
            InsectSkill s = Track(ScriptableObject.CreateInstance<InsectSkill>());
            s.skillId = id;
            s.displayName = id;
            s.element = element;
            s.effectType = SkillEffectType.Damage;
            s.power = 30;
            s.accuracy = 1f;
            s.cooldownTurns = 0;
            s.isSignatureSkill = signature;
            return s;
        }

        private static InsectLearnableSkill Learn(InsectSkill skill, int level)
            => new InsectLearnableSkill { skillId = skill.skillId, learnLevel = level, skill = skill };

        private T Track<T>(T obj) where T : Object
        {
            objects.Add(obj);
            return obj;
        }

        private sealed class FixedRoll : IRaidRandomSource
        {
            private readonly float value;
            public FixedRoll(float value) { this.value = value; }
            public float Next01() => value;
            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        }
    }
}
#endif
