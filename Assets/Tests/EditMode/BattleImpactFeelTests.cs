#if UNITY_EDITOR
using System.Reflection;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 전투 타격감의 순수 부분 — 연출 카메라 샷, 히트스톱·슬로모션 시계, 외침 분류, 타격 세기.
    /// 화면은 QA 캡처(<c>BattleVisualCapture -battleCamStyle</c>)로 보고, 여기선 "원위치로 돌아오는가"
    /// "멈춘 시계가 풀리는가"처럼 눈으로 놓치기 쉬운 계약만 고정한다.
    /// </summary>
    [TestFixture]
    public class BattleImpactFeelTests
    {
        private static readonly Vector3 BasePos = new Vector3(0f, 2f, -8f);
        private static readonly Quaternion BaseRot = Quaternion.LookRotation(new Vector3(0f, -2f, 8f));
        private static readonly Vector3 Attacker = new Vector3(-2f, 0.6f, 0f);
        private static readonly Vector3 Target = new Vector3(2f, 0.6f, 0f);

        private static BattleCameraDirector.Shot Eval(BattleCameraDirector.Style style, float p,
            float weight = 0f, bool support = false)
        {
            return BattleCameraDirector.Evaluate(style, p, 2.5f, BasePos, BaseRot, Attacker, Target, weight, support);
        }

        // ── 연출 카메라 ──

        [Test]
        public void CameraShot_Off_NeverMovesCamera()
        {
            for (float p = 0f; p <= 1f; p += 0.05f)
                Assert.AreEqual(0f, Eval(BattleCameraDirector.Style.Off, p).Weight);
        }

        [TestCase(BattleCameraDirector.Style.Punch)]
        [TestCase(BattleCameraDirector.Style.Cinematic)]
        public void CameraShot_TimelineEnd_ReturnsToBaseFraming(BattleCameraDirector.Style style)
        {
            // 연출이 끝났는데 샷이 남으면 다음 턴 버튼을 누르는 동안에도 카메라가 붙어 있다.
            Assert.AreEqual(0f, Eval(style, 0.85f, 1f).Weight);
            Assert.AreEqual(0f, Eval(style, 1f, 1f).Weight);
        }

        [Test]
        public void CameraShot_Cinematic_WindupFramesAttacker_ImpactFramesTarget()
        {
            BattleCameraDirector.Shot windup = Eval(BattleCameraDirector.Style.Cinematic, 0.16f);
            BattleCameraDirector.Shot impact = Eval(BattleCameraDirector.Style.Cinematic, BattleMotion.ImpactProgress + 0.02f);
            Assert.AreEqual(1f, windup.Weight, 0.001f);
            Assert.Less(Vector3.Distance(windup.Position, Attacker), Vector3.Distance(BasePos, Attacker));
            Assert.AreEqual(1f, impact.Weight, 0.001f);
            Assert.Less(Vector3.Distance(impact.Position, Target), Vector3.Distance(BasePos, Target));
        }

        [Test]
        public void CameraShot_Cinematic_HeavyHitMovesCloser()
        {
            float p = 0.5f;   // 반동 진동이 0을 지나는 순간을 피해 유지 구간 중간
            float light = Vector3.Distance(Eval(BattleCameraDirector.Style.Cinematic, p, 0f).Position, Target);
            float heavy = Vector3.Distance(Eval(BattleCameraDirector.Style.Cinematic, p, 1f).Position, Target);
            Assert.Less(heavy, light);
        }

        [Test]
        public void CameraShot_Punch_OnlyAfterImpact_AndDecays()
        {
            Assert.AreEqual(0f, Eval(BattleCameraDirector.Style.Punch, 0.3f, 1f).Weight);
            float early = Eval(BattleCameraDirector.Style.Punch, BattleMotion.ImpactProgress + 0.01f, 1f).Weight;
            float late = Eval(BattleCameraDirector.Style.Punch, BattleMotion.ImpactProgress + 0.1f, 1f).Weight;
            Assert.Greater(early, 0.5f);
            Assert.Less(late, early);
        }

        [Test]
        public void CameraShot_Support_NeverCutsToTarget()
        {
            for (float p = 0f; p < 1f; p += 0.05f)
            {
                BattleCameraDirector.Shot shot = Eval(BattleCameraDirector.Style.Cinematic, p, 1f, support: true);
                if (shot.Weight <= 0f) continue;
                Assert.Less(Vector3.Distance(shot.Position, Attacker), Vector3.Distance(shot.Position, Target),
                    $"p={p}: 강화 연출이 상대 쪽으로 넘어갔다");
            }
        }

        // ── 레이드 합체공격 ──

        [Test]
        public void UniteTimeline_FullTeamHitsBeforeFinalStrike()
        {
            // 5인 팀의 마지막 타격이 "합동 일격"보다 늦으면 일격 뒤에 혼자 때리는 팀원이 생긴다
            // (옛 오버레이 숫자로는 1.55초 > 1.5초였다).
            Assert.Less(RaidUniteTimeline.MemberHit(4), RaidUniteTimeline.FinalStrike);
            Assert.Greater(RaidUniteTimeline.TotalReveal, RaidUniteTimeline.FinalStrike);
            Assert.GreaterOrEqual(RaidUniteTimeline.Total, RaidUniteTimeline.TotalReveal + 0.4f,
                "TOTAL이 뜨고 읽힐 시간 없이 연출이 끝난다");
        }

        [Test]
        public void UniteShot_StartsAndEndsOnBaseFraming_AndRushesInAtFinalStrike()
        {
            Vector3 team = new Vector3(0f, 0.5f, -2f);
            Vector3 boss = new Vector3(0f, 2.2f, 3f);
            Vector3 camPos = new Vector3(0f, 1.6f, -9.5f);
            Quaternion camRot = Quaternion.LookRotation(boss - camPos);
            Assert.AreEqual(0f, BattleCameraDirector.EvaluateUnite(0f, camPos, camRot, team, boss).Weight);
            Assert.AreEqual(0f, BattleCameraDirector.EvaluateUnite(RaidUniteTimeline.Total, camPos, camRot, team, boss).Weight);
            BattleCameraDirector.Shot strike = BattleCameraDirector.EvaluateUnite(
                RaidUniteTimeline.FinalStrike - 0.001f, camPos, camRot, team, boss);
            Assert.AreEqual(1f, strike.Weight, 0.001f);
            Assert.Less(Vector3.Distance(strike.Position, boss), Vector3.Distance(camPos, boss));
            // 붙더라도 팀 줄을 넘지 않는다 — 넘으면 팀원이 카메라 뒤로 사라진다.
            Assert.Less(strike.Position.z, team.z);
        }

        [Test]
        public void Kick_ZeroBeforeImpact_AndSettles()
        {
            Assert.AreEqual(Vector3.zero, BattleCameraDirector.Kick(Vector3.right, -0.1f, 1f));
            Assert.Less(BattleCameraDirector.Kick(Vector3.right, 0.8f, 1f).magnitude, 0.01f);
        }

        // ── 히트스톱·슬로모션 ──

        private float fakeNow;
        private System.Func<float> savedClock;
        private float savedSpeed;
        private bool savedReducedMotion;
        private static readonly FieldInfo SpeedField = typeof(BattlePresentation).GetField("speed", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo MotionField = typeof(BattlePresentation).GetField("reducedMotion", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void FakeClock()
        {
            savedClock = BattlePresentation.Clock;
            savedSpeed = (float)SpeedField.GetValue(null);
            savedReducedMotion = (bool)MotionField.GetValue(null);
            // 세터는 PlayerPrefs에 쓰므로 필드를 직접 바꾼다(사용자 설정을 건드리지 않게).
            SpeedField.SetValue(null, 1f);
            MotionField.SetValue(null, false);
            fakeNow = 100f;
            BattlePresentation.Clock = () => fakeNow;
            BattlePresentation.ClearTimeEffects();
        }

        [TearDown]
        public void RestoreClock()
        {
            BattlePresentation.ClearTimeEffects();
            BattlePresentation.Clock = savedClock;
            SpeedField.SetValue(null, savedSpeed);
            MotionField.SetValue(null, savedReducedMotion);
        }

        [Test]
        public void HitStop_FreezesPresentation_ThenReleasesOnRealTime()
        {
            BattlePresentation.HitStop(0.1f);
            Assert.AreEqual(0f, BattlePresentation.TimeScale);
            fakeNow += 0.11f;
            Assert.AreEqual(1f, BattlePresentation.TimeScale, "멈춘 시계로 만료를 세면 영영 안 풀린다");
        }

        [Test]
        public void HitStop_Overlapping_ExtendsNotAccumulates()
        {
            BattlePresentation.HitStop(0.1f);
            fakeNow += 0.05f;
            BattlePresentation.HitStop(0.1f);   // 남은 0.05 + 0.1이 아니라 지금부터 0.1
            fakeNow += 0.09f;
            Assert.AreEqual(0f, BattlePresentation.TimeScale);
            fakeNow += 0.02f;
            Assert.AreEqual(1f, BattlePresentation.TimeScale);
        }

        [Test]
        public void HitStop_DoubleSpeed_IsHalved()
        {
            SpeedField.SetValue(null, 2f);
            BattlePresentation.HitStop(0.1f);
            fakeNow += 0.06f;
            Assert.AreEqual(1f, BattlePresentation.TimeScale);
        }

        [Test]
        public void SlowMotion_ScalesThenReleases_AndHitStopWinsWhileBothActive()
        {
            BattlePresentation.SlowMotion(0.3f, 0.5f);
            Assert.AreEqual(0.3f, BattlePresentation.TimeScale, 0.0001f);
            BattlePresentation.HitStop(0.05f);
            Assert.AreEqual(0f, BattlePresentation.TimeScale);
            fakeNow += 0.1f;
            Assert.AreEqual(0.3f, BattlePresentation.TimeScale, 0.0001f);
            fakeNow += 0.5f;
            Assert.AreEqual(1f, BattlePresentation.TimeScale);
        }

        [Test]
        public void SlowMotion_ReducedMotion_IsSkipped()
        {
            MotionField.SetValue(null, true);
            BattlePresentation.SlowMotion(0.3f, 0.5f);
            Assert.AreEqual(1f, BattlePresentation.TimeScale);
        }

        [Test]
        public void ClearTimeEffects_DropsPendingStop()
        {
            BattlePresentation.HitStop(1f);
            BattlePresentation.SlowMotion(0.3f, 1f);
            BattlePresentation.ClearTimeEffects();
            Assert.AreEqual(1f, BattlePresentation.TimeScale);
        }

        // ── 외침 ──

        [TestCase("mantis", BattleShout.Cry.Mantis)]
        [TestCase("mantis_ember", BattleShout.Cry.Mantis)]
        [TestCase("dragonfly_jade", BattleShout.Cry.Flyer)]
        [TestCase("firefly_marsh", BattleShout.Cry.Flyer)]
        [TestCase("moth_pale", BattleShout.Cry.Flyer)]
        [TestCase("fly_hover", BattleShout.Cry.Buzzer)]
        [TestCase("bee_queen", BattleShout.Cry.Buzzer)]
        [TestCase("hornet_emperor", BattleShout.Cry.Buzzer)]
        [TestCase("cricket_tomb", BattleShout.Cry.Chirper)]
        [TestCase("cicada_ancient", BattleShout.Cry.Chirper)]
        [TestCase("ant", BattleShout.Cry.Crawler)]
        [TestCase("antlion_pit", BattleShout.Cry.Crawler)]
        [TestCase("spider_tomb", BattleShout.Cry.Crawler)]
        [TestCase("rhinoceros_beetle", BattleShout.Cry.Beetle)]
        [TestCase("stag_beetle_iron", BattleShout.Cry.Beetle)]
        [TestCase("pill_bug_rock", BattleShout.Cry.Beetle)]
        [TestCase(null, BattleShout.Cry.Beetle)]
        public void CryFor_MatchesWholeTokensOnly(string id, BattleShout.Cry expected)
        {
            // 부분 문자열로 보면 mantis에 ant가, dragonfly에 fly가 걸린다.
            Assert.AreEqual(expected, BattleShout.CryFor(id));
        }

        [Test]
        public void Sound_EveryElementHasLightAndHeavyWord()
        {
            foreach (InsectElement element in System.Enum.GetValues(typeof(InsectElement)))
            {
                string light = BattleShout.Sound(element, 0f);
                string heavy = BattleShout.Sound(element, 1f);
                Assert.IsNotEmpty(light, element.ToString());
                Assert.IsTrue(heavy.EndsWith("!!"), $"{element}: 센 타격은 '!!'로 끝나야 오버레이가 크게 그린다");
                Assert.AreNotEqual(light, heavy, element.ToString());
            }
        }

        [Test]
        public void Hurt_EveryCryHasLines()
        {
            foreach (BattleShout.Cry cry in System.Enum.GetValues(typeof(BattleShout.Cry)))
            {
                Assert.IsNotEmpty(BattleShout.Hurt(cry, 0f, 1), cry.ToString());
                Assert.IsNotEmpty(BattleShout.Hurt(cry, 1f, 1), cry.ToString());
            }
        }

        [TestCase("전력 돌진", "전력 돌진!!")]
        [TestCase("전력 돌진!", "전력 돌진!!")]
        [TestCase("  날카로운 일격  ", "날카로운 일격!!")]
        [TestCase("", "간다!!")]
        [TestCase(null, "간다!!")]
        public void Callout_NormalizesExclamation(string skill, string expected)
        {
            Assert.AreEqual(expected, BattleShout.Callout(skill));
        }

        // ── 타격 세기 ──

        [TestCase(0, 200, false, 0f)]
        [TestCase(50, 200, false, 0.625f)]    // 치명타 판정선(25%)
        [TestCase(80, 200, false, 1f)]        // 40%면 만점
        [TestCase(200, 200, false, 1f)]
        [TestCase(10, 200, true, 0.85f)]      // 치명타는 적게 들어가도 무겁게
        [TestCase(10, 0, false, 0f)]
        public void HitCueWeight_ScalesWithDamageShare(int damage, int maxHp, bool critical, float expected)
        {
            Assert.AreEqual(expected, BattleArenaController.HitCue.WeightFor(damage, maxHp, critical), 0.001f);
        }
    }
}
#endif
