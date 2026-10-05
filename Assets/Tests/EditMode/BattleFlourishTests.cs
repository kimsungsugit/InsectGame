#if UNITY_EDITOR
using InsectGame.Battle;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 전투 체감 강화(4단계)의 순수 부분 — 계열 분류(진짜 곤충 ID), 계열 몸짓의 타격 시각, 전용기 컷인 시각표와 샷, 승리 포즈·카메라,
    /// 전투 진입 샷, 상태 표시 판정·곡선, 숨쉬기. 화면은 QA 캡처(<c>-battleScenario status-fx|signature|victory|species-motions</c>)로 본다.
    /// </summary>
    [TestFixture]
    public class BattleFlourishTests
    {
        private static readonly Vector3 BasePos = new Vector3(1000f, 2f, 993.5f);
        private static readonly Quaternion BaseRot = Quaternion.LookRotation(new Vector3(0f, -1.4f, 6.5f));
        private static readonly Vector3 Attacker = new Vector3(997.9f, 0.6f, 999.65f);
        private static readonly Vector3 Target = new Vector3(1002.1f, 0.6f, 1000.35f);

        private static readonly BattleMotion.Kind[] MeleeKinds =
        {
            BattleMotion.Kind.Charge, BattleMotion.Kind.Slash, BattleMotion.Kind.Flight,
            BattleMotion.Kind.Sting, BattleMotion.Kind.Pounce, BattleMotion.Kind.Lunge
        };

        private static readonly BattleMotion.Family[] Families =
        {
            BattleMotion.Family.Beetle, BattleMotion.Family.Mantis, BattleMotion.Family.Flier,
            BattleMotion.Family.Bee, BattleMotion.Family.Crawler, BattleMotion.Family.Other
        };

        // ── 계열 분류 — 모델 빌더(InsectEntity.BuildModel)와 같은 순서 ──

        [TestCase("mantis_green", BattleMotion.Family.Mantis)]
        [TestCase("mantis_orchid", BattleMotion.Family.Mantis)]
        [TestCase("mantis_ghost", BattleMotion.Family.Mantis)]
        [TestCase("gacha_shadow_mantis", BattleMotion.Family.Mantis)]
        [TestCase("rhinoceros_beetle", BattleMotion.Family.Beetle)]
        [TestCase("stag_beetle_saw", BattleMotion.Family.Beetle)]
        [TestCase("beetle_golden_stag", BattleMotion.Family.Beetle)]
        [TestCase("beetle_basic", BattleMotion.Family.Beetle)]
        [TestCase("scarab_ancient", BattleMotion.Family.Beetle)]
        [TestCase("ladybug_seven", BattleMotion.Family.Beetle)]
        [TestCase("diving_beetle_great", BattleMotion.Family.Beetle)]
        [TestCase("butterfly_swallowtail", BattleMotion.Family.Flier)]
        [TestCase("atlas_moth_giant", BattleMotion.Family.Flier)]
        [TestCase("dragonfly_ancient", BattleMotion.Family.Flier)]
        [TestCase("damselfly_blue", BattleMotion.Family.Flier)]
        [TestCase("firefly_blue", BattleMotion.Family.Flier)]
        [TestCase("cicada_summer", BattleMotion.Family.Flier)]
        [TestCase("fly_hover", BattleMotion.Family.Flier)]
        [TestCase("bee_worker", BattleMotion.Family.Bee)]
        [TestCase("wasp_paper", BattleMotion.Family.Bee)]
        [TestCase("hornet_asian", BattleMotion.Family.Bee)]
        [TestCase("gacha_storm_hornet", BattleMotion.Family.Bee)]
        [TestCase("spider_garden", BattleMotion.Family.Crawler)]
        [TestCase("centipede_red", BattleMotion.Family.Crawler)]
        [TestCase("antlion_pit", BattleMotion.Family.Crawler)]
        [TestCase("ant_soldier", BattleMotion.Family.Other)]
        [TestCase("cricket_field", BattleMotion.Family.Other)]
        [TestCase("grasshopper_locust", BattleMotion.Family.Other)]
        [TestCase("leaf_insect_phantom", BattleMotion.Family.Other)]
        [TestCase("water_strider_pond", BattleMotion.Family.Other)]
        [TestCase(null, BattleMotion.Family.Beetle)]
        public void FamilyOf_RealIds_FollowModelBuilder(string id, BattleMotion.Family expected)
        {
            Assert.AreEqual(expected, BattleMotion.FamilyOf(id));
        }

        [Test]
        public void FamilyOf_BeetleIdsContainingBee_AreNotBees()
        {
            // "beetle"에 "bee"가 들어 있다 — 가드가 빠지면 딱정벌레 전부가 찌르기를 한다(모델 빌더의 같은 함정).
            foreach (string id in new[] { "beetle_basic", "beetle_dung", "beetle_click", "beetle_longhorn_oak", "rhinoceros_beetle_titan" })
                Assert.AreEqual(BattleMotion.Family.Beetle, BattleMotion.FamilyOf(id), id);
        }

        [TestCase(BattleMotion.Family.Mantis, BattleMotion.Kind.Slash)]
        [TestCase(BattleMotion.Family.Beetle, BattleMotion.Kind.Charge)]
        [TestCase(BattleMotion.Family.Flier, BattleMotion.Kind.Flight)]
        [TestCase(BattleMotion.Family.Bee, BattleMotion.Kind.Sting)]
        [TestCase(BattleMotion.Family.Crawler, BattleMotion.Kind.Pounce)]
        [TestCase(BattleMotion.Family.Other, BattleMotion.Kind.Lunge)]
        public void MeleeKindOf_EachFamily_HasItsOwnMotion(BattleMotion.Family family, BattleMotion.Kind expected)
        {
            Assert.AreEqual(expected, BattleMotion.MeleeKindOf(family));
        }

        // ── 계열 몸짓 ──

        [Test]
        public void EveryMotion_HitsWithinOneSecondOfASkill_AndRestsByTheEnd()
        {
            // 스킬 연출은 2.5초(BattleScreenUI attackDuration) — 전용기 컷인 → 타격이 1초 안쪽이어야 한다.
            foreach (BattleMotion.Kind kind in MeleeKinds)
            {
                float impact = BattleMotion.ImpactOf(kind);
                Assert.LessOrEqual(impact * 2.5f, BattleFlourish.SignatureLeadMaxSeconds + 0.0001f, kind.ToString());
                Assert.Less(impact, BattleMotion.RestProgress, kind.ToString());
                foreach (BattleMotion.Family family in Families)
                {
                    for (int s = -5; s <= 110; s++)
                    {
                        BattleMotion.Pose pose = BattleMotion.Evaluate(kind, family, s / 100f);
                        AssertFinite(pose);
                        Assert.Less(Mathf.Abs(pose.Lift), 1.2f, $"{kind} 너무 높다");
                    }
                    AssertRest(BattleMotion.Evaluate(kind, family, BattleMotion.RestProgress));
                    AssertRest(BattleMotion.Evaluate(kind, family, 0f));
                }
            }
        }

        [Test]
        public void ProjectileCast_EachFamily_StaysHomeAndReturnsToRest()
        {
            foreach (BattleMotion.Family family in Families)
            {
                for (int s = 0; s <= 100; s++)
                {
                    BattleMotion.Pose pose = BattleMotion.Evaluate(BattleMotion.Kind.Projectile, family, s / 100f);
                    AssertFinite(pose);
                    Assert.Less(Mathf.Abs(pose.Travel), 0.15f, $"{family} 원거리 시전이 달려든다");
                }
                AssertRest(BattleMotion.Evaluate(BattleMotion.Kind.Projectile, family, BattleMotion.RestProgress));
            }
        }

        [Test]
        public void Motions_HaveDistinctSilhouettes()
        {
            float MaxLift(BattleMotion.Kind k) { float m = 0f; for (int i = 0; i <= 100; i++) m = Mathf.Max(m, BattleMotion.Evaluate(k, i / 100f).Lift); return m; }
            float MaxAbsRoll(BattleMotion.Kind k) { float m = 0f; for (int i = 0; i <= 100; i++) m = Mathf.Max(m, Mathf.Abs(BattleMotion.Evaluate(k, i / 100f).Roll)); return m; }
            // 나는 종은 높이 솟았다 내리꽂고, 딱정벌레는 낮게 들이받고, 사마귀는 휘두르며 몸이 크게 기운다.
            Assert.Greater(MaxLift(BattleMotion.Kind.Flight), 0.8f);
            Assert.AreEqual(0f, MaxLift(BattleMotion.Kind.Charge), 0.0001f);
            Assert.Greater(MaxAbsRoll(BattleMotion.Kind.Slash), 18f);
            // 딱정벌레는 들이받는 동안 머리를 숙이고 있다(양수 = 숙임), 나는 종은 내리꽂는 순간 머리가 아래.
            Assert.Greater(BattleMotion.Evaluate(BattleMotion.Kind.Charge, 0.3f).Pitch, 10f);
            Assert.Greater(BattleMotion.Evaluate(BattleMotion.Kind.Flight, BattleMotion.ImpactProgress - 0.01f).Pitch, 20f);
            // 찌르기는 곧은 선 — 들어가는 동안 낮아지기만 한다(포물선이 아니다).
            float before = BattleMotion.Evaluate(BattleMotion.Kind.Sting, 0.28f).Lift;
            float atHit = BattleMotion.Evaluate(BattleMotion.Kind.Sting, BattleMotion.StingImpactProgress - 0.001f).Lift;
            Assert.Less(atHit, before);
            // 덮치기는 정점에서 포물선으로 솟는다.
            Assert.Greater(BattleMotion.Evaluate(BattleMotion.Kind.Pounce, 0.29f).Lift, 0.5f);
        }

        [Test]
        public void Slash_SwingsTwice_FirstBeforeTheHit()
        {
            Assert.Less(BattleMotion.SlashFirstSwing, BattleMotion.ImpactProgress);
            BattleMotion.Pose first = BattleMotion.Evaluate(BattleMotion.Kind.Slash, BattleMotion.SlashFirstSwing);
            BattleMotion.Pose second = BattleMotion.Evaluate(BattleMotion.Kind.Slash, BattleMotion.ImpactProgress);
            // 두 번째는 반대 대각선이다.
            Assert.Greater(first.Roll, 10f);
            Assert.Less(second.Roll, -10f);
            Assert.IsTrue(BattleMotion.HasPreStrike(BattleMotion.Kind.Slash));
            Assert.IsFalse(BattleMotion.HasPreStrike(BattleMotion.Kind.Charge));
        }

        // ── 전용기 ──

        [Test]
        public void SignatureCutIn_EndsBeforeTheHit_ForEveryMotionAndLength()
        {
            foreach (BattleMotion.Kind kind in MeleeKinds)
            foreach (float duration in new[] { 0.6f, 1.8f, 2.5f })
            {
                float impact = BattleMotion.ImpactOf(kind) * duration;
                float cut = BattleFlourish.SignatureCutInEndFor(impact);
                Assert.Less(cut, impact, $"{kind} {duration}s — 컷인이 타격 뒤까지 간다");
                Assert.LessOrEqual(cut, BattleFlourish.SignatureCutInEnd);
            }
            // 레이드 볼리(0.46초 끝에 타격)도 컷인이 타격 전에 끝난다.
            Assert.Less(BattleFlourish.SignatureCutInEndFor(BattleArenaController.RaidVolleyImpactSeconds),
                BattleArenaController.RaidVolleyImpactSeconds);
        }

        [Test]
        public void SignatureShot_CutsToCasterFirst_ThenTargetAtHit_ThenReleases()
        {
            BattleCameraDirector.Style saved = BattleCameraDirector.Current;
            try
            {
                BattleCameraDirector.Current = BattleCameraDirector.Style.Cinematic;
                const float Duration = 2.5f;
                float impact = BattleMotion.ImpactProgress;
                BattleCameraDirector.Shot cut = BattleCameraDirector.EvaluateSignature(0.1f / Duration, Duration, BasePos, BaseRot,
                    Attacker, Target, 0.5f, false, impact);
                Assert.AreEqual(1f, cut.Weight, 0.001f, "컷인은 0.08초 안에 끊어 들어간다");
                Assert.Less(Vector3.Distance(cut.Position, Attacker), Vector3.Distance(BasePos, Attacker));
                BattleCameraDirector.Shot hit = BattleCameraDirector.EvaluateSignature(impact + 0.01f, Duration, BasePos, BaseRot,
                    Attacker, Target, 0.5f, false, impact);
                Assert.Less(Vector3.Distance(hit.Position, Target), Vector3.Distance(BasePos, Target) * 0.75f);
                Assert.AreEqual(0f, BattleCameraDirector.EvaluateSignature(0.9f, Duration, BasePos, BaseRot,
                    Attacker, Target, 0.5f, false, impact).Weight);

                BattleCameraDirector.Current = BattleCameraDirector.Style.Off;
                Assert.AreEqual(0f, BattleCameraDirector.EvaluateSignature(0.1f, Duration, BasePos, BaseRot,
                    Attacker, Target, 0.5f, false, impact).Weight);
            }
            finally
            {
                BattleCameraDirector.Current = saved;
            }
        }

        [Test]
        public void HitCue_WithSignature_KeepsTheHitAndAddsABoundedPause()
        {
            var plain = new BattleArenaController.HitCue("돌진", 0.4f, false, false, false);
            BattleArenaController.HitCue sig = plain.WithSignature(true);
            Assert.IsTrue(sig.Signature);
            Assert.AreEqual(plain.SkillName, sig.SkillName);
            Assert.AreEqual(plain.Weight, sig.Weight, 0.0001f);
            Assert.Greater(sig.HitStopSeconds, plain.HitStopSeconds);
            var heaviest = new BattleArenaController.HitCue("돌진", 1f, true, false, true, true);
            Assert.LessOrEqual(heaviest.HitStopSeconds, BattleArenaController.HitCue.MaxHitStop + 0.0001f);
            Assert.AreEqual(0f, new BattleArenaController.HitCue("돌진", 0f, false, true, false, true).HitStopSeconds);
        }

        // ── 승리 ──

        [Test]
        public void VictoryPose_EveryFamily_JumpsAndSettlesBeforeTheEnd()
        {
            Assert.LessOrEqual(BattleFlourish.VictoryPoseSeconds, BattleFlourish.VictorySeconds);
            foreach (BattleMotion.Family family in Families)
            {
                float maxLift = 0f;
                for (int i = 0; i <= 200; i++)
                {
                    BattleMotion.Pose pose = BattleFlourish.VictoryPose(family, i / 100f);
                    AssertFinite(pose);
                    Assert.GreaterOrEqual(pose.Lift, -0.0001f, $"{family} 땅속으로 들어간다");
                    maxLift = Mathf.Max(maxLift, pose.Lift);
                }
                Assert.Greater(maxLift, 0.2f, $"{family} 뛰어오르지 않는다");
                AssertRest(BattleFlourish.VictoryPose(family, 0f));
                AssertRest(BattleFlourish.VictoryPose(family, BattleFlourish.VictoryPoseSeconds));
            }
        }

        [Test]
        public void VictoryOrbit_TurnsTowardTheFace_AndStaysInsideTheWalls()
        {
            BattleCameraDirector.Style saved = BattleCameraDirector.Current;
            try
            {
                BattleCameraDirector.Current = BattleCameraDirector.Style.Cinematic;
                Vector3 face = Vector3.right;   // 내 곤충은 상대(+x)를 본다
                Vector3 pivot = Attacker;
                Assert.AreEqual(0f, BattleCameraDirector.EvaluateVictoryOrbit(0f, BasePos, BaseRot, pivot, face,
                    BattleFlourish.VictoryOrbitDegrees, 3f, 0.2f).Weight, 0.001f, "첫 프레임은 원래 구도");
                BattleCameraDirector.Shot end = BattleCameraDirector.EvaluateVictoryOrbit(BattleFlourish.VictorySeconds, BasePos, BaseRot,
                    pivot, face, BattleFlourish.VictoryOrbitDegrees, 3f, 0.2f);
                Assert.AreEqual(1f, end.Weight, 0.001f);
                Vector3 startDir = Flat(BasePos - pivot);
                Vector3 endDir = Flat(end.Position - pivot);
                Assert.AreEqual(BattleFlourish.VictoryOrbitDegrees, Vector3.Angle(startDir, endDir), 2f);
                Assert.Greater(Vector3.Dot(endDir, face), Vector3.Dot(startDir, face), "얼굴 반대쪽으로 돌았다");
                // 다 돈 뒤에도 마지막 구도에 머문다(결과 화면 뒤).
                BattleCameraDirector.Shot hold = BattleCameraDirector.EvaluateVictoryOrbit(BattleFlourish.VictorySeconds + 3f, BasePos, BaseRot,
                    pivot, face, BattleFlourish.VictoryOrbitDegrees, 3f, 0.2f);
                Assert.Less(Vector3.Distance(hold.Position, end.Position), 0.001f);
                for (float t = 0f; t <= BattleFlourish.VictorySeconds; t += 0.05f)
                {
                    Vector3 p = BattleCameraDirector.EvaluateVictoryOrbit(t, BasePos, BaseRot, pivot, face,
                        BattleFlourish.VictoryOrbitDegrees, 3f, 0.2f).Position;
                    Assert.Less(Mathf.Abs(p.x - 1000f), BattleArenaController.ArenaWallSpan - 1f);
                    Assert.Less(Mathf.Abs(p.z - 1000f), BattleArenaController.ArenaWallSpan - 1f);
                }
            }
            finally
            {
                BattleCameraDirector.Current = saved;
            }
        }

        [Test]
        public void FitDistance_PortraitNeedsMoreRoomThanLandscape()
        {
            float landscape = BattleFlourish.FitDistance(3f, 1f, 42f, 16f / 9f);
            float portrait = BattleFlourish.FitDistance(3f, 1f, 42f, 9f / 16f);
            Assert.Greater(portrait, landscape);
            Assert.Greater(landscape, 3f);
        }

        // ── 전투 진입 샷 ──

        [Test]
        public void OpeningShot_FitsTheEntryWindow_AndLandsExactlyOnTheBattleFraming()
        {
            Assert.LessOrEqual(BattleFlourish.OpeningShotSeconds, 1f + 0.0001f, "요구: 1초 안쪽");
            Assert.LessOrEqual(BattleFlourish.OpeningShotSeconds, BattleReadPacing.EntryIntroSeconds, "진입 구간 안에서 끝난다");
            BattleCameraDirector.Style saved = BattleCameraDirector.Current;
            try
            {
                BattleCameraDirector.Current = BattleCameraDirector.Style.Cinematic;
                Vector3 focus = Vector3.Lerp(Attacker, Target, 0.5f);
                BattleCameraDirector.Shot first = BattleCameraDirector.EvaluateOpening(0f, BasePos, BaseRot, focus, Target);
                Assert.AreEqual(1f, first.Weight, "전투가 이 샷으로 열린다");
                Assert.Less(Vector3.Distance(first.Position, Target), Vector3.Distance(BasePos, Target) * 0.6f, "상대 옆에서 시작");
                BattleCameraDirector.Shot last = BattleCameraDirector.EvaluateOpening(BattleFlourish.OpeningShotSeconds - 0.0001f,
                    BasePos, BaseRot, focus, Target);
                Assert.Less(Vector3.Distance(last.Position, BasePos), 0.01f);
                Assert.Less(Quaternion.Angle(last.Rotation, BaseRot), 1f);
                Assert.AreEqual(0f, BattleCameraDirector.EvaluateOpening(BattleFlourish.OpeningShotSeconds, BasePos, BaseRot, focus, Target).Weight);
                // 크게 휘돈다 — 중간 어디선가 상대 쪽(+x)으로 원래 자리보다 많이 나가 있다.
                float maxX = float.MinValue;
                for (float t = 0f; t < BattleFlourish.OpeningShotSeconds; t += 0.05f)
                    maxX = Mathf.Max(maxX, BattleCameraDirector.EvaluateOpening(t, BasePos, BaseRot, focus, Target).Position.x);
                Assert.Greater(maxX, BasePos.x + 2f);

                BattleCameraDirector.Current = BattleCameraDirector.Style.Off;
                Assert.AreEqual(0f, BattleCameraDirector.EvaluateOpening(0.2f, BasePos, BaseRot, focus, Target).Weight);
            }
            finally
            {
                BattleCameraDirector.Current = saved;
            }
        }

        // ── 상태 표시 ──

        [Test]
        public void StatusFromDuel_SplitsSidesAndKinds_LikeTheHpCard()
        {
            var effects = new[]
            {
                new InsectBattleController.EffectSnapshot { targetIsPlayer = true, value = 0.3f, remainingTurns = 2, kind = InsectBattleController.EffectKind.AtkBuff },
                new InsectBattleController.EffectSnapshot { targetIsPlayer = false, value = 6f, remainingTurns = 3, kind = InsectBattleController.EffectKind.Dot },
                new InsectBattleController.EffectSnapshot { targetIsPlayer = false, value = 0.3f, remainingTurns = 3, kind = InsectBattleController.EffectKind.DefBuff },
                new InsectBattleController.EffectSnapshot { targetIsPlayer = false, value = -0.2f, remainingTurns = 1, kind = InsectBattleController.EffectKind.AtkBuff },
                new InsectBattleController.EffectSnapshot { targetIsPlayer = true, value = -0.2f, remainingTurns = 0, kind = InsectBattleController.EffectKind.DefBuff },
            };
            Assert.AreEqual(BattleStatusFlags.Stun | BattleStatusFlags.AttackUp, BattleStatusLook.FromDuel(2, effects, true));
            Assert.AreEqual(BattleStatusFlags.Poison | BattleStatusFlags.DefenseUp | BattleStatusFlags.AttackDown,
                BattleStatusLook.FromDuel(0, effects, false));
            Assert.AreEqual(BattleStatusFlags.None, BattleStatusLook.FromDuel(0, null, true));
        }

        [Test]
        public void StatusFromStacks_SignIsDirection()
        {
            Assert.AreEqual(BattleStatusFlags.AttackUp | BattleStatusFlags.DefenseDown, BattleStatusLook.FromStacks(2, -1, false));
            Assert.AreEqual(BattleStatusFlags.Stun | BattleStatusFlags.AttackDown, BattleStatusLook.FromStacks(-3, 0, true));
            Assert.AreEqual(BattleStatusFlags.None, BattleStatusLook.FromStacks(0, 0, false));
        }

        [Test]
        public void RaidBossDazed_OnlyAfterTheSkippedResponse()
        {
            var round = new RaidRoundResult(1, 0, 0, false, null, 5);
            Assert.IsFalse(BattleStatusLook.RaidBossDazed(round));
            round.BossResponseSkipped = true;
            round.Stage = RaidRoundStage.TeamResolved;
            Assert.IsFalse(BattleStatusLook.RaidBossDazed(round), "건너뛰기가 정해지기 전(팀 차례)에는 별이 없다");
            round.Stage = RaidRoundStage.BossResolved;
            Assert.IsTrue(BattleStatusLook.RaidBossDazed(round));
            round.Stage = RaidRoundStage.Completed;
            Assert.IsTrue(BattleStatusLook.RaidBossDazed(round));
            Assert.IsFalse(BattleStatusLook.RaidBossDazed(null));
        }

        [Test]
        public void StatusCurves_FadeAtBothEnds_AndStayGentle()
        {
            BattleStatusLook.Bubble(0f, out _, out _, out float a0);
            BattleStatusLook.Bubble(1f, out _, out float s1, out float a1);
            BattleStatusLook.Bubble(0.5f, out _, out _, out float aMid);
            Assert.AreEqual(0f, a0, 0.001f);
            Assert.AreEqual(0f, a1, 0.001f);
            Assert.AreEqual(1f, aMid, 0.001f);
            Assert.Greater(s1, 1f, "끝에서 톡 부푼다");
            BattleStatusLook.Streak(0f, out float st0, out _);
            BattleStatusLook.Streak(1f, out float st1, out _);
            Assert.AreEqual(0f, st0, 0.001f);
            Assert.AreEqual(0f, st1, 0.001f);
            for (float t = 0f; t < 10f; t += 0.1f)
            {
                float shield = BattleStatusLook.ShieldAlpha(t, 1.3f, false);
                Assert.That(shield, Is.InRange(0.18f, 0.41f), "육각 막은 은은하다");
                Assert.AreEqual(0.26f, BattleStatusLook.ShieldAlpha(t, 1.3f, true), 0.0001f, "섬광 줄이기면 일렁이지 않는다");
            }
        }

        // ── 숨쉬기 ──

        [Test]
        public void Breathing_IsSmallAndBounded()
        {
            for (float t = 0f; t < 12f; t += 0.07f)
            {
                float b = BattleFlourish.Breath(t, BattleFlourish.BreathPeriod, 0.3f);
                Assert.That(b, Is.InRange(-1f, 1f));
                float nod = BattleFlourish.AntennaNod(t, 0.3f);
                Assert.That(nod, Is.InRange(-4.01f, 18.01f));
            }
            Assert.Less(BattleFlourish.BreathAmplitude, 0.06f, "숨쉬기가 크면 맞은 것처럼 보인다");
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.normalized;
        }

        private static void AssertFinite(BattleMotion.Pose pose)
        {
            foreach (float v in new[] { pose.Travel, pose.Lift, pose.Pitch, pose.Roll, pose.Yaw })
                Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
        }

        private static void AssertRest(BattleMotion.Pose pose)
        {
            Assert.AreEqual(0f, pose.Travel, 0.0001f);
            Assert.AreEqual(0f, pose.Lift, 0.0001f);
            Assert.AreEqual(0f, pose.Pitch, 0.0001f);
            Assert.AreEqual(0f, pose.Roll, 0.0001f);
            Assert.AreEqual(0f, Mathf.Repeat(pose.Yaw + 180f, 360f) - 180f, 0.01f);
        }
    }
}
#endif
