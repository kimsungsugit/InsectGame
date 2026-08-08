#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Story;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 컷신 타임라인의 순수 계산. 카메라·조작 복귀는 MonoBehaviour 수명에 달려 있어
    /// 기기 확인 대상이고, 여기서는 <b>시간축이 컷을 건너뛰거나 겹치지 않는지</b>를 고정한다.
    /// </summary>
    [TestFixture]
    public class CutsceneTimelineTests
    {
        private static CutsceneShot[] ThreeShots()
        {
            return new[]
            {
                new CutsceneShot(1f, Vector3.zero, Vector3.one, Vector3.forward, "첫 컷"),
                new CutsceneShot(2f, Vector3.one, Vector3.up, Vector3.forward),
                new CutsceneShot(0.5f, Vector3.up, Vector3.zero, Vector3.forward, "끝 컷"),
            };
        }

        [Test]
        public void TotalDuration_SumsAllShots()
        {
            Assert.AreEqual(3.5f, CutsceneTimeline.TotalDuration(ThreeShots()), 0.001f);
        }

        [Test]
        public void TotalDuration_NullOrEmpty_IsZero()
        {
            Assert.AreEqual(0f, CutsceneTimeline.TotalDuration(null), 0.001f);
            Assert.AreEqual(0f, CutsceneTimeline.TotalDuration(new CutsceneShot[0]), 0.001f);
        }

        [TestCase(0f, 0)]
        [TestCase(0.99f, 0)]
        [TestCase(1f, 1)]
        [TestCase(2.99f, 1)]
        [TestCase(3f, 2)]
        [TestCase(3.49f, 2)]
        public void TryGetShot_MapsElapsedToShot(float elapsed, int expectedIndex)
        {
            Assert.IsTrue(CutsceneTimeline.TryGetShot(ThreeShots(), elapsed, out int index, out _));
            Assert.AreEqual(expectedIndex, index);
        }

        [Test]
        public void TryGetShot_PastEnd_ReturnsFalse()
        {
            // false가 재생 종료 신호다 — true로 새면 컷신이 안 끝나 조작이 영영 안 돌아온다.
            Assert.IsFalse(CutsceneTimeline.TryGetShot(ThreeShots(), 3.5f, out _, out _));
            Assert.IsFalse(CutsceneTimeline.TryGetShot(ThreeShots(), 999f, out _, out _));
        }

        [Test]
        public void TryGetShot_NullOrEmpty_ReturnsFalse()
        {
            Assert.IsFalse(CutsceneTimeline.TryGetShot(null, 0f, out _, out _));
            Assert.IsFalse(CutsceneTimeline.TryGetShot(new CutsceneShot[0], 0f, out _, out _));
        }

        [Test]
        public void TryGetShot_NegativeElapsed_ClampsToFirstShot()
        {
            Assert.IsTrue(CutsceneTimeline.TryGetShot(ThreeShots(), -5f, out int index, out float t));
            Assert.AreEqual(0, index);
            Assert.AreEqual(0f, t, 0.001f);
        }

        [Test]
        public void TryGetShot_ProgressSpansZeroToOneWithinShot()
        {
            CutsceneTimeline.TryGetShot(ThreeShots(), 1f, out _, out float start);
            CutsceneTimeline.TryGetShot(ThreeShots(), 2.999f, out _, out float end);

            Assert.AreEqual(0f, start, 0.01f);
            Assert.Greater(end, 0.99f);
        }

        [Test]
        public void TryGetShot_EveryInstantIsCoveredExactlyOnce()
        {
            // 컷 경계에서 하나가 건너뛰어지면 그 컷의 자막·흔들림이 통째로 사라진다.
            CutsceneShot[] shots = ThreeShots();
            var seen = new HashSet<int>();
            for (float e = 0f; e < 3.5f; e += 0.01f)
            {
                Assert.IsTrue(CutsceneTimeline.TryGetShot(shots, e, out int index, out _),
                    $"경과 {e:F2}s에서 컷이 없다");
                seen.Add(index);
            }
            Assert.AreEqual(shots.Length, seen.Count, "도달하지 못한 컷이 있다");
        }

        [Test]
        public void CameraOffsetAt_EndsMatchFromAndTo()
        {
            var shot = new CutsceneShot(2f, new Vector3(0f, 1f, -2f), new Vector3(0f, 5f, -8f), Vector3.zero);

            Assert.AreEqual(shot.camFrom, CutsceneTimeline.CameraOffsetAt(shot, 0f));
            Assert.AreEqual(shot.camTo, CutsceneTimeline.CameraOffsetAt(shot, 1f));
        }

        [Test]
        public void SubtitleAlpha_FadesInAndOut()
        {
            const float duration = 3f;

            Assert.AreEqual(0f, CutsceneTimeline.SubtitleAlpha(duration, 0f), 0.01f);
            Assert.AreEqual(1f, CutsceneTimeline.SubtitleAlpha(duration, 0.5f), 0.01f);
            Assert.AreEqual(0f, CutsceneTimeline.SubtitleAlpha(duration, 1f), 0.01f);
        }

        [Test]
        public void SubtitleAlpha_VeryShortShot_StaysOpaque()
        {
            // 페이드 2배보다 짧으면 페이드하지 않는다 — 안 그러면 자막이 깜빡이기만 한다.
            Assert.AreEqual(1f, CutsceneTimeline.SubtitleAlpha(0.4f, 0f), 0.01f);
            Assert.AreEqual(1f, CutsceneTimeline.SubtitleAlpha(0.4f, 0.5f), 0.01f);
        }

        // ── 저작된 컷신 ──

        [TestCase(CutsceneLibrary.SealOpening)]
        [TestCase(CutsceneLibrary.NamelessConfront)]
        public void Library_DefinedCutscenes_AreWellFormed(string cutsceneId)
        {
            Assert.IsTrue(CutsceneLibrary.TryGet(cutsceneId, out CutsceneShot[] shots), cutsceneId);
            Assert.Greater(shots.Length, 0);

            foreach (CutsceneShot shot in shots)
            {
                Assert.Greater(shot.duration, 0f, "지속이 0이면 그 컷은 절대 안 보인다");
                Assert.GreaterOrEqual(shot.dim, 0f);
                Assert.LessOrEqual(shot.dim, 1f);
                Assert.GreaterOrEqual(shot.shake, 0f);
            }
        }

        [Test]
        public void Library_UnknownId_ReturnsFalse()
        {
            Assert.IsFalse(CutsceneLibrary.TryGet("cs_없는거", out _));
            Assert.IsFalse(CutsceneLibrary.TryGet(null, out _));
        }

        [Test]
        public void Library_Cutscenes_EndNearPlayerSoCameraReturnsSmoothly()
        {
            // 마지막 컷이 플레이어에게서 멀면 컷신이 끝나는 순간 카메라가 튀어 돌아온다.
            foreach (string id in new[] { CutsceneLibrary.SealOpening, CutsceneLibrary.NamelessConfront })
            {
                Assert.IsTrue(CutsceneLibrary.TryGet(id, out CutsceneShot[] shots));
                CutsceneShot last = shots[shots.Length - 1];

                Assert.Less(last.camTo.magnitude, 8f, $"{id}의 마지막 카메라가 너무 멀다");
                Assert.Less(last.dim, 0.25f, $"{id}가 화면이 어두운 채로 끝난다");
            }
        }

        [Test]
        public void Library_Cutscenes_FinishBeforeAutoUnfreeze()
        {
            // **이게 이 파일에서 가장 중요한 검사다.** PlayerMovement는 frozen이 걸린 뒤
            // AutoUnfreezeTime이 지나면 스스로 푼다(먹통 방지 안전망). 컷신이 그보다 길면
            // 재생 도중 조작이 살아나 카메라는 컷신이 잡고 있는데 캐릭터가 움직이는 상태가 된다.
            // 여유를 두는 것은 컷신 시작 전에 프리즈가 이미 얼마간 진행돼 있을 수 있어서다.
            const float safetyMargin = 4f;

            foreach (string id in new[] { CutsceneLibrary.SealOpening, CutsceneLibrary.NamelessConfront })
            {
                Assert.IsTrue(CutsceneLibrary.TryGet(id, out CutsceneShot[] shots));
                float total = CutsceneTimeline.TotalDuration(shots);

                Assert.Greater(total, 3f, $"{id}가 너무 짧아 연출로 읽히지 않는다");
                Assert.Less(total, GameConstants.Player.AutoUnfreezeTime - safetyMargin,
                    $"{id}가 {total:F1}s로 자동 프리즈 해제({GameConstants.Player.AutoUnfreezeTime}s)에 너무 가깝다 "
                    + "— 재생 중 조작이 살아난다");
            }
        }
    }
}
#endif
