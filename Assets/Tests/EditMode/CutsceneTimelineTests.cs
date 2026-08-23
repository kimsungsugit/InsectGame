#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
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
        /// <summary>
        /// 저작된 컷신 전부를 <b>리플렉션으로 센다.</b> 손으로 나열하면 새 컷신이 검사에서
        /// 조용히 빠진다 — 배열 하나로 모으는 것만으로는 부족했다. 그 배열 자체가 손 목록이라
        /// 6번째 컷신을 <c>CutsceneLibrary</c>에만 추가하면 길이·복귀 검사가 그걸 <b>안 본다</b>
        /// (게다가 아래 케이스 목록과 이중으로 어긋난다). 이 저장소가 <c>verify_coverage</c>·
        /// <c>literal_fit_lint</c>에서 겪은 "검사기가 새 항목을 못 보는" 계열이다.
        ///
        /// 상수 이름이 아니라 <b>값</b>(cs_*)을 모은다 — <c>TryGet</c>이 받는 것이 그쪽이다.
        /// </summary>
        private static readonly string[] AllCutscenes = CollectCutsceneIds();

        private static string[] CollectCutsceneIds()
        {
            var ids = new List<string>();
            FieldInfo[] fields = typeof(CutsceneLibrary)
                .GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (FieldInfo f in fields)
            {
                // const 문자열만 — static readonly가 섞여도 GetRawConstantValue가 터지지 않게.
                if (!f.IsLiteral || f.IsInitOnly) continue;
                if (f.FieldType != typeof(string)) continue;
                ids.Add((string)f.GetRawConstantValue());
            }

            ids.Sort(System.StringComparer.Ordinal);   // 케이스 이름이 실행마다 흔들리지 않게
            return ids.ToArray();
        }

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

        [TestCaseSource(nameof(AllCutscenes))]
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
        public void Library_IdCollection_IsNotEmpty()
        {
            // **리플렉션이 빈 배열을 내면 아래 검사들이 전부 "통과"한다** — 0건 통과는 통과가
            // 아니라 검사기 고장이다(rules/testing.md의 "0건 보고는 실패다"와 같은 이야기).
            Assert.GreaterOrEqual(AllCutscenes.Length, 5,
                "CutsceneLibrary의 const 문자열을 못 읽었다 — 추출이 낡았다");
            CollectionAssert.AllItemsAreNotNull(AllCutscenes);
            CollectionAssert.AllItemsAreUnique(AllCutscenes);
            foreach (string id in AllCutscenes)
                Assert.IsTrue(CutsceneLibrary.TryGet(id, out _), $"{id}가 switch에 없다");
        }

        [TestCaseSource(nameof(AllCutscenes))]
        public void Library_Cutscenes_ChainWithoutJumpCut(string cutsceneId)
        {
            // 컷 N+1의 camFrom이 컷 N의 camTo와 다르면 그 경계에서 카메라가 **순간이동한다.**
            // 지금 5종은 전부 이어져 있지만 아무도 그걸 지키게 하고 있지 않았다 — 좌표 한 줄만
            // 고쳐도 조용히 점프 컷이 된다(컴파일도 되고 예외도 없다).
            Assert.IsTrue(CutsceneLibrary.TryGet(cutsceneId, out CutsceneShot[] shots));

            for (int i = 1; i < shots.Length; i++)
            {
                Assert.AreEqual(0f, Vector3.Distance(shots[i].camFrom, shots[i - 1].camTo), 0.001f,
                    $"{cutsceneId} 컷 {i}의 시작이 앞 컷의 끝과 다르다 "
                    + $"({shots[i - 1].camTo} → {shots[i].camFrom})");
            }
        }

        [TestCaseSource(nameof(AllCutscenes))]
        public void Library_Cutscenes_KeepCameraOffThePlayerAndFacingSomewhere(string cutsceneId)
        {
            Assert.IsTrue(CutsceneLibrary.TryGet(cutsceneId, out CutsceneShot[] shots));

            foreach (CutsceneShot shot in shots)
            {
                // 카메라가 플레이어에 너무 붙으면 모델 안으로 들어가 화면이 살덩이로 찬다.
                Assert.Greater(new Vector2(shot.camFrom.x, shot.camFrom.z).magnitude, 0.9f, cutsceneId);
                Assert.Greater(new Vector2(shot.camTo.x, shot.camTo.z).magnitude, 0.9f, cutsceneId);

                // 시선 지점이 카메라와 겹치면 LookRotation이 0벡터를 받는다. CameraFollower가
                // 그때 **직전 회전을 그대로 유지**하므로 예외도 로그도 없이 카메라만 굳는다.
                Assert.Greater(Vector3.Distance(shot.lookAt, shot.camFrom), 0.5f, cutsceneId);
                Assert.Greater(Vector3.Distance(shot.lookAt, shot.camTo), 0.5f, cutsceneId);
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
            foreach (string id in AllCutscenes)
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

            foreach (string id in AllCutscenes)
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
