#if UNITY_EDITOR
using InsectGame.Battle;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 이야기 전투의 등장·변신 연출 — 시각표·곡선·카메라 샷·그림자 색(<see cref="BattleStaging"/>·<see cref="BattleCameraDirector"/>).
    /// 화면 단계(교체 1.2초·변신 1.5초·수문장 인트로)와 어긋나면 연출이 단계 밖으로 넘치거나 다음 차례가 연출 구도에서 열린다.
    /// </summary>
    [TestFixture]
    public class BattleStagingTests
    {
        private BattleCameraDirector.Style savedStyle;

        [SetUp]
        public void SetUp()
        {
            savedStyle = BattleCameraDirector.Current;
            BattleCameraDirector.Current = BattleCameraDirector.Style.Cinematic;
        }

        [TearDown]
        public void TearDown()
        {
            BattleCameraDirector.Current = savedStyle;
        }

        private static void AssertNear(Vector3 expected, Vector3 actual, float tolerance, string message = null)
        {
            Assert.LessOrEqual(Vector3.Distance(expected, actual), tolerance, message ?? $"{expected} vs {actual}");
        }

        // ───────────── ① 상대 교체 등장 ─────────────

        [Test]
        public void EntranceTimeline_LeapsLandsAndSettles_InsideTheSwitchPhase()
        {
            Assert.Greater(BattleStaging.EntranceLeapStart, 0f);
            Assert.Less(BattleStaging.EntranceLeapStart, BattleStaging.EntranceLand);
            Assert.Less(BattleStaging.EntranceLand, BattleStaging.EntranceSettleEnd);
            Assert.Less(BattleStaging.EntranceSettleEnd, 1f, "착지 반동이 교체 단계 안에서 끝나야 한다");
            Assert.Less(BattleStaging.EntranceBeamEnd, BattleStaging.EntranceLand, "빛살은 착지 전에 착지점을 알린다");
            Assert.Less(BattleStaging.EntranceCamIn, BattleStaging.EntranceLand, "착지는 클로즈업이 자리 잡은 뒤");
            Assert.Less(BattleStaging.EntranceLand, BattleStaging.EntranceCamHoldEnd);
            Assert.Less(BattleStaging.EntranceCamReleaseEnd, 1f, "「당신의 턴」은 원래 구도에서 열린다");
        }

        [Test]
        public void LeapPosition_StartsAtLaunch_EndsAtRest_AndArcsAboveBoth()
        {
            var launch = new Vector3(5.1f, 0.5f, 1.95f);
            var rest = new Vector3(2.1f, 0.5f, 0.35f);
            float apex = BattleStaging.EntranceApex;
            AssertNear(launch, BattleStaging.LeapPosition(launch, rest, apex, 0f), 1e-4f);
            AssertNear(rest, BattleStaging.LeapPosition(launch, rest, apex, 1f), 1e-4f);
            Assert.Greater(BattleStaging.LeapPosition(launch, rest, apex, 0.5f).y, rest.y + apex * 0.9f);

            // 수평으로는 착지점 쪽으로만 다가간다(되돌아가지 않는다).
            float previous = float.MaxValue;
            for (int i = 0; i <= 20; i++)
            {
                Vector3 p = BattleStaging.LeapPosition(launch, rest, apex, i / 20f);
                float horizontal = new Vector2(p.x - rest.x, p.z - rest.z).magnitude;
                Assert.LessOrEqual(horizontal, previous + 1e-4f);
                previous = horizontal;
            }
        }

        [Test]
        public void LandingSquash_StartsSquashed_Overshoots_AndSettlesAtOne()
        {
            Assert.AreEqual(0.78f, BattleStaging.LandingSquash(0f), 1e-4f);
            Assert.AreEqual(1f, BattleStaging.LandingSquash(1f), 1e-4f);
            float max = 0f;
            for (int i = 0; i <= 50; i++)
            {
                float s = BattleStaging.LandingSquash(i / 50f);
                Assert.GreaterOrEqual(s, 0.77f);
                Assert.LessOrEqual(s, 1.12f);
                max = Mathf.Max(max, s);
            }
            Assert.Greater(max, 1.03f, "눌렸다가 튀어 올라야 착지 반동으로 읽힌다");
            Assert.Greater(BattleStaging.SquashWidth(0.8f), 1f, "눌리면 퍼진다");
            Assert.AreEqual(1f, BattleStaging.SquashWidth(1f), 1e-6f);
        }

        [Test]
        public void EvaluateEntrance_StartsFromThePreviousFraming_AndReleasesToTheNewOne()
        {
            var fromPos = new Vector3(0.2f, 2.1f, -6.4f);
            Quaternion fromRot = Quaternion.Euler(12f, -12f, 0f);
            var basePos = new Vector3(0.5f, 2.3f, -7.1f);
            Quaternion baseRot = Quaternion.Euler(12f, -12f, 0f);
            var landing = new Vector3(2.3f, 0.8f, 0.35f);
            var opponent = new Vector3(-2.3f, 0.8f, -0.35f);

            BattleCameraDirector.Shot first = BattleCameraDirector.EvaluateEntrance(0f, 1.2f, fromPos, fromRot, basePos, baseRot, landing, opponent);
            Assert.AreEqual(1f, first.Weight, 1e-5f, "첫 프레임은 샷이 화면을 쥔다 — 새 구도로 튀지 않게");
            AssertNear(fromPos, first.Position, 1e-4f, "첫 프레임은 교체 전 구도");
            Assert.Less(Quaternion.Angle(fromRot, first.Rotation), 0.01f);

            BattleCameraDirector.Shot landed = BattleCameraDirector.EvaluateEntrance(BattleStaging.EntranceLand, 1.2f,
                fromPos, fromRot, basePos, baseRot, landing, opponent);
            Assert.AreEqual(1f, landed.Weight, 1e-5f);
            Assert.Less(Vector3.Distance(landed.Position, landing), Vector3.Distance(basePos, landing), "착지는 다가가서 본다");

            float previous = 1f;
            for (int i = 0; i <= 20; i++)
            {
                float p = Mathf.Lerp(BattleStaging.EntranceCamHoldEnd, 1f, i / 20f);
                float w = BattleCameraDirector.EvaluateEntrance(p, 1.2f, fromPos, fromRot, basePos, baseRot, landing, opponent).Weight;
                Assert.LessOrEqual(w, previous + 1e-5f, "풀리는 동안 가중치는 줄기만 한다");
                previous = w;
            }
            Assert.AreEqual(0f, BattleCameraDirector.EvaluateEntrance(BattleStaging.EntranceCamReleaseEnd, 1.2f,
                fromPos, fromRot, basePos, baseRot, landing, opponent).Weight);
        }

        [Test]
        public void StagingShots_OffStyle_LeaveTheFramingAlone()
        {
            BattleCameraDirector.Current = BattleCameraDirector.Style.Off;
            Assert.AreEqual(0f, BattleCameraDirector.EvaluateEntrance(0.5f, 1.2f, Vector3.zero, Quaternion.identity,
                Vector3.zero, Quaternion.identity, Vector3.one, Vector3.zero).Weight);
            Assert.AreEqual(0f, BattleCameraDirector.EvaluateBossTransform(0.5f, 1.5f, Vector3.zero, Quaternion.identity, Vector3.one).Weight);
            Assert.AreEqual(0f, BattleCameraDirector.EvaluateGuardianIntro(0.5f, Vector3.zero, Vector3.one, Quaternion.identity).Weight);
        }

        // ───────────── ② 그림자 변신 ─────────────

        [Test]
        public void TransformTimeline_SwapsUnderFullCover_AndClearsBeforeTheNextTurn()
        {
            Assert.AreEqual(1f, BattleStaging.SmokeCover(BattleStaging.TransformSwap), 1e-5f, "모델은 연기가 가장 짙을 때 바뀐다");
            Assert.AreEqual(BattleStaging.TransformVeilPeak, BattleStaging.TransformVeil(BattleStaging.TransformSwap), 1e-5f);
            Assert.AreEqual(0f, BattleStaging.SmokeCover(0f), 1e-5f);
            Assert.AreEqual(0f, BattleStaging.SmokeCover(1f), 1e-5f);
            Assert.AreEqual(0f, BattleStaging.TransformVeil(0f), 1e-5f);
            Assert.AreEqual(0f, BattleStaging.TransformVeil(1f), 1e-5f, "단계가 끝나면 먹빛이 다 걷힌다");
            Assert.Less(BattleStaging.TransformEngulfEnd, BattleStaging.TransformSwap);
            Assert.Less(BattleStaging.TransformSwap, BattleStaging.TransformRoar);
            Assert.Less(BattleStaging.TransformRoar, BattleStaging.TransformRevealEnd);
            Assert.Less(BattleStaging.TransformRevealEnd, 1f);
            Assert.Less(BattleStaging.TransformCamReleaseEnd, 1f, "다음 차례는 원래 구도에서 열린다");
        }

        [Test]
        public void TransformScale_IsContinuousAcrossTheSwap_AndEndsAtFullSize()
        {
            Assert.AreEqual(1f, BattleStaging.TransformScale(0f), 1e-5f);
            Assert.AreEqual(1f, BattleStaging.TransformScale(1f), 1e-5f);
            Assert.AreEqual(BattleStaging.TransformShrink, BattleStaging.TransformScale(BattleStaging.TransformSwap), 1e-5f);
            Assert.AreEqual(BattleStaging.TransformScale(BattleStaging.TransformSwap - 1e-4f),
                BattleStaging.TransformScale(BattleStaging.TransformSwap), 0.005f, "갈아끼우는 순간 크기가 튀지 않는다");
            for (int i = 0; i <= 60; i++)
            {
                float s = BattleStaging.TransformScale(i / 60f);
                Assert.GreaterOrEqual(s, BattleStaging.TransformShrink - 1e-5f);
                Assert.LessOrEqual(s, BattleStaging.TransformOvershoot + 1e-5f);
            }
        }

        [Test]
        public void EvaluateBossTransform_NeverPassesTheTeamLine_AndReturnsToTheRaidFraming()
        {
            var team = new Vector3(0f, 0.5f, -2f);
            var bossPos = new Vector3(0f, 2.2f, 3f);
            BattleArenaController.ComputeRaidCameraFraming(team, bossPos, out Vector3 basePos, out Vector3 look);
            Quaternion baseRot = Quaternion.LookRotation(look - basePos);
            var bossCenter = new Vector3(0f, 2.4f, 3f);

            Assert.AreEqual(0f, BattleCameraDirector.EvaluateBossTransform(0f, 1.5f, basePos, baseRot, bossCenter).Weight, 1e-5f,
                "원래 구도에서 출발한다");
            Assert.AreEqual(1f, BattleCameraDirector.EvaluateBossTransform(BattleStaging.TransformSwap, 1.5f, basePos, baseRot, bossCenter).Weight, 1e-5f);
            Assert.AreEqual(0f, BattleCameraDirector.EvaluateBossTransform(BattleStaging.TransformCamReleaseEnd, 1.5f, basePos, baseRot, bossCenter).Weight);
            for (int i = 0; i <= 40; i++)
            {
                BattleCameraDirector.Shot shot = BattleCameraDirector.EvaluateBossTransform(i / 40f, 1.5f, basePos, baseRot, bossCenter);
                if (shot.Weight <= 0f) continue;
                Assert.Less(shot.Position.z, team.z - 1f, $"진행률 {i / 40f}: 카메라가 팀 줄을 넘었다");
            }
        }

        // ───────────── ③ 수문장 등장 ─────────────

        [Test]
        public void GuardianIntro_AddsUnderASecond_AndReleasesBeforeTheFirstTurn()
        {
            const float OrdinaryRaidIntro = 2f;   // RaidBattleUI.RaidIntroSeconds
            Assert.Greater(BattleStaging.GuardianIntroSeconds, OrdinaryRaidIntro);
            Assert.LessOrEqual(BattleStaging.GuardianIntroSeconds - OrdinaryRaidIntro, 1f, "레이드 진행 시간을 1초 넘게 늘리지 않는다");
            Assert.Less(BattleStaging.GuardianRoarAt, BattleStaging.GuardianApproachEnd);
            Assert.Less(BattleStaging.GuardianApproachEnd, BattleStaging.GuardianReleaseStart);
            Assert.Less(BattleStaging.GuardianReleaseEnd, BattleStaging.GuardianIntroSeconds, "첫 차례는 원래 구도에서 열린다");
            Assert.AreEqual(0f, BattleStaging.RoarPose(BattleStaging.GuardianReleaseStart), 1e-5f, "풀리기 전에 포효 자세가 끝난다");

            Quaternion rot = Quaternion.Euler(-5f, 0f, 0f);
            Assert.AreEqual(1f, BattleCameraDirector.EvaluateGuardianIntro(0f, Vector3.back * 10f, Vector3.back * 6f, rot).Weight, 1e-5f,
                "인트로 첫 장면부터 등장 컷이다");
            Assert.AreEqual(0f, BattleCameraDirector.EvaluateGuardianIntro(BattleStaging.GuardianReleaseEnd,
                Vector3.back * 10f, Vector3.back * 6f, rot).Weight);
        }

        [Test]
        public void GuardianApproachStart_StaysInsideTheWalls_AndBehindTheEnd()
        {
            var end = new Vector3(0f, 1.4f, -9f);
            Quaternion rot = Quaternion.Euler(-2f, 0f, 0f);
            Vector3 start = BattleStaging.GuardianApproachStart(end, rot, 12f, Vector3.zero, 13f, 0f);
            Assert.LessOrEqual(Mathf.Abs(start.z), 13f - BattleStaging.GuardianWallMargin + 1e-4f, "벽 밖에 서면 벽의 바깥 면이 화면을 막는다");
            Assert.LessOrEqual(start.z, end.z, "출발점은 끝 자리보다 뒤다");
            Assert.GreaterOrEqual(start.y, BattleStaging.GuardianMinCameraHeight - 1e-4f);
        }

        // 받침대(2.2m) 위 수문장 — 사마귀·큰 뿔(키 큼)·아틀라스나방(날개 넓음)·나비·반딧불이·작은 곤충. 레이드 배율 2.3배 기준의 대략값.
        private static readonly Bounds[] GuardianBodies =
        {
            new Bounds(new Vector3(0f, 2.25f, 3f), new Vector3(1.6f, 1.9f, 3.6f)),
            new Bounds(new Vector3(0f, 2.45f, 3f), new Vector3(2.0f, 2.5f, 3.4f)),
            new Bounds(new Vector3(0f, 2.3f, 3f), new Vector3(4.6f, 1.4f, 2.6f)),
            new Bounds(new Vector3(0f, 2.35f, 3f), new Vector3(3.8f, 2.0f, 2.0f)),
            new Bounds(new Vector3(0f, 2.2f, 3f), new Vector3(1.8f, 1.1f, 2.0f)),
            new Bounds(new Vector3(0f, 2.2f, 3f), new Vector3(1.0f, 0.8f, 1.2f)),
        };

        /// <summary>
        /// 등장 컷 내내 수문장이 화면 위쪽 2/3 안에 있다 — 아래 1/3은 ui-dev의 「○○의 수문장 · 별칭 / 이름」 배너 자리다.
        /// FOV는 CameraFollower.ApplyAspectFov와 같은 식(세로 60°, 가로 60/√비율, 32~60°).
        /// </summary>
        [TestCase(1280, 720)]
        [TestCase(720, 1280)]
        [TestCase(2400, 1080)]
        [TestCase(1080, 2400)]
        public void GuardianShot_KeepsTheGuardianInTheUpperTwoThirds(int width, int height)
        {
            float aspect = (float)width / height;
            float fov = height >= width ? 60f : Mathf.Clamp(60f / Mathf.Sqrt(aspect), 32f, 60f);
            float tanV = Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f);
            float tanH = tanV * aspect;
            var team = new Vector3(0f, 0.5f, -2f);
            var window = new Rect(0f, BattleStaging.GuardianWindowBottom, 1f,
                BattleStaging.GuardianWindowTop - BattleStaging.GuardianWindowBottom);
            float[] times = { 0f, 0.3f, BattleStaging.GuardianRoarAt + 0.04f, 0.9f, BattleStaging.GuardianApproachEnd,
                BattleStaging.GuardianReleaseStart - 0.01f };

            foreach (Bounds body in GuardianBodies)
            {
                Bounds fit = body;
                fit.Expand(0.3f);
                BattleStaging.GuardianEndShot(fit, aspect, fov, 0f, window, team, 0f,
                    out Vector3 endPos, out Vector3 endTarget, out Quaternion rotation);
                Assert.GreaterOrEqual(endPos.y, BattleStaging.GuardianMinCameraHeight - 1e-4f, "카메라가 팀 머리 위에 선다");
                Assert.LessOrEqual(endPos.z, team.z - BattleStaging.GuardianTeamClearance + 1e-3f, "카메라가 팀 줄 뒤에 선다");
                float span = Mathf.Max(BattleArenaController.ArenaWallSpan, Mathf.Abs(endPos.z) + 3f);
                Vector3 startPos = BattleStaging.GuardianApproachStart(endPos, rotation, Vector3.Distance(endPos, endTarget),
                    Vector3.zero, span, 0f);

                foreach (float t in times)
                {
                    BattleCameraDirector.Shot shot = BattleCameraDirector.EvaluateGuardianIntro(t, startPos, endPos, rotation);
                    Assert.AreEqual(1f, shot.Weight, 1e-5f);
                    Quaternion inverse = Quaternion.Inverse(shot.Rotation);
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = body.center + Vector3.Scale(body.extents, new Vector3(
                            (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                        Vector3 local = inverse * (corner - shot.Position);
                        Assert.Greater(local.z, 0.1f, "수문장이 카메라 앞에 있다");
                        float vx = 0.5f + 0.5f * local.x / (local.z * tanH);
                        float vy = 0.5f + 0.5f * local.y / (local.z * tanV);
                        string where = $"{width}x{height} t={t:F2} 몸 {body.size} 모서리 {i}: ({vx:F3}, {vy:F3})";
                        Assert.GreaterOrEqual(vy, 1f / 3f, "아래 1/3(배너 자리)로 내려왔다 — " + where);
                        Assert.LessOrEqual(vy, 1f, "화면 위로 잘렸다 — " + where);
                        Assert.GreaterOrEqual(vx, 0f, "화면 왼쪽으로 잘렸다 — " + where);
                        Assert.LessOrEqual(vx, 1f, "화면 오른쪽으로 잘렸다 — " + where);
                    }
                }
            }
        }

        // ───────────── 그림자 색 ─────────────

        [Test]
        public void ShadowTint_ZeroStrength_IsIdentity_AndAlphaIsKept()
        {
            var wing = new Color(0.96f, 0.83f, 0.18f, 0.3f);
            Color same = BattleStaging.ShadowTint(wing, 0f);
            Assert.AreEqual(wing.r, same.r, 1e-5f);
            Assert.AreEqual(wing.g, same.g, 1e-5f);
            Assert.AreEqual(wing.b, same.b, 1e-5f);
            Assert.AreEqual(0.3f, BattleStaging.ShadowTint(wing, BattleStaging.ShadowBorrowedStrength).a, 1e-5f, "날개 막의 투명도는 그대로");
            Assert.AreEqual(0.3f, BattleStaging.ShadowGlowTint(wing).a, 1e-5f);
        }

        [Test]
        public void ShadowTint_BorrowedForm_IsDarkPurple_ButKeepsThePattern()
        {
            var yellow = new Color(0.96f, 0.83f, 0.18f);   // 호랑나비 노랑
            var stripe = new Color(0.08f, 0.07f, 0.06f);   // 검은 줄
            Color y = BattleStaging.ShadowTint(yellow, BattleStaging.ShadowBorrowedStrength);
            Color s = BattleStaging.ShadowTint(stripe, BattleStaging.ShadowBorrowedStrength);
            Assert.Less(Luma(y), Luma(yellow) * 0.6f, "빌린 모습은 진짜보다 확실히 어둡다");
            Assert.Greater(y.b, y.g, "검보라 — 노랑이 남지 않는다");
            Assert.Greater(Luma(y), Luma(s), "밝고 어두운 무늬 차는 남는다");
            // 원래 모습(사마귀)은 같은 톤이되 덜 덮는다.
            var green = new Color(0.35f, 0.62f, 0.28f);
            Color original = BattleStaging.ShadowTint(green, BattleStaging.ShadowOriginalStrength);
            Color borrowed = BattleStaging.ShadowTint(green, BattleStaging.ShadowBorrowedStrength);
            Assert.Greater(original.g - original.b, borrowed.g - borrowed.b, "원래 모습엔 제 색이 조금 더 남는다");
            Assert.Less(BattleStaging.ShadowOriginalStrength, BattleStaging.ShadowBorrowedStrength);
        }

        [Test]
        public void ShadowBoss_IsExactlyTheFormChangingBoss()
        {
            Assert.IsTrue(BattleStaging.IsShadowBoss("mantis_unnamed"));
            Assert.IsFalse(BattleStaging.IsShadowBoss("mantis_green"));
            Assert.IsFalse(BattleStaging.IsShadowBoss(null));
        }

        private static float Luma(Color c)
        {
            return c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        }
    }
}
#endif
