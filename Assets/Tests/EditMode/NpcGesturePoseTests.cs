#if UNITY_EDITOR
using System;
using InsectGame.NPC;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// NPC 몸짓의 순수 각도 계산. 실제 회전이 어떻게 보이는지는 기기 확인 대상이고,
    /// 여기서 고정하는 건 하나다 — <b>몸짓이 끝나면 관절이 정확히 0으로 돌아오는가</b>.
    /// 안 돌아오면 그 NPC가 팔을 든 채로 영영 서 있게 된다(애니메이터가 t=1을 마지막에
    /// 한 번 더 적용하는 이유이기도 하다).
    /// </summary>
    [TestFixture]
    public class NpcGesturePoseTests
    {
        private static readonly NpcGesture[] AllGestures =
            (NpcGesture[])Enum.GetValues(typeof(NpcGesture));

        [Test]
        public void DurationOf_None_IsZero()
        {
            Assert.AreEqual(0f, NpcGesturePose.DurationOf(NpcGesture.None), 0.0001f);
        }

        [Test]
        public void DurationOf_EveryRealGesture_IsPositive()
        {
            // 길이를 등록하지 않으면 애니메이터가 즉시 꺼서 몸짓이 조용히 사라진다.
            foreach (NpcGesture gesture in AllGestures)
            {
                if (gesture == NpcGesture.None) continue;
                Assert.Greater(NpcGesturePose.DurationOf(gesture), 0f,
                    $"{gesture}의 재생 시간이 등록되지 않았다");
            }
        }

        [Test]
        public void Evaluate_None_LeavesEveryJointFree()
        {
            NpcPoseDelta pose = NpcGesturePose.Evaluate(NpcGesture.None, 0.5f);
            Assert.IsFalse(pose.ownsRightArm);
            Assert.IsFalse(pose.ownsLeftArm);
            Assert.IsFalse(pose.ownsHead);
            Assert.IsFalse(pose.ownsBody);
        }

        [Test]
        public void Evaluate_EveryGesture_StartsAndEndsAtRest()
        {
            foreach (NpcGesture gesture in AllGestures)
            {
                if (gesture == NpcGesture.None) continue;
                AssertAtRest(gesture, 0f);
                AssertAtRest(gesture, 1f);
            }
        }

        [Test]
        public void Evaluate_EveryGesture_MovesSomethingMidway()
        {
            // 중간에 아무 채널도 안 움직이면 등록만 되고 화면엔 아무 일도 안 일어난다.
            foreach (NpcGesture gesture in AllGestures)
            {
                if (gesture == NpcGesture.None) continue;
                bool moved = false;
                for (int i = 1; i < 10 && !moved; i++)
                {
                    NpcPoseDelta pose = NpcGesturePose.Evaluate(gesture, i / 10f);
                    moved = Magnitude(pose) > 0.5f;
                }
                Assert.IsTrue(moved, $"{gesture}가 재생 내내 정지 상태다");
            }
        }

        [Test]
        public void Evaluate_EveryGesture_StaysWithinRigLimit()
        {
            // 팔은 어깨 피벗 없이 캡슐 중심에서 돌고 손은 팔의 자식이 아니다 —
            // 뜰채 스윙(72°)보다 크게 돌리면 손이 팔에서 떨어져 나온 게 눈에 띈다.
            foreach (NpcGesture gesture in AllGestures)
            {
                if (gesture == NpcGesture.None) continue;
                for (int i = 0; i <= 20; i++)
                {
                    NpcPoseDelta pose = NpcGesturePose.Evaluate(gesture, i / 20f);
                    Assert.LessOrEqual(Mathf.Abs(pose.rightArmDeg), NpcGesturePose.SwingMaxDeg + 0.01f,
                        $"{gesture} t={i / 20f} 오른팔 각도가 리그 상한을 넘었다");
                    Assert.LessOrEqual(Mathf.Abs(pose.leftArmDeg), NpcGesturePose.SwingMaxDeg + 0.01f,
                        $"{gesture} t={i / 20f} 왼팔 각도가 리그 상한을 넘었다");
                }
            }
        }

        [Test]
        public void Evaluate_ClampsOutOfRangeProgress()
        {
            // 음수·1 초과는 양 끝으로 잘린다 — 타이머 오차로 넘어가도 각도가 튀지 않는다.
            AssertSamePose(NpcGesturePose.Evaluate(NpcGesture.Wave, -0.5f),
                NpcGesturePose.Evaluate(NpcGesture.Wave, 0f));
            AssertSamePose(NpcGesturePose.Evaluate(NpcGesture.Wave, 1.7f),
                NpcGesturePose.Evaluate(NpcGesture.Wave, 1f));
        }

        [Test]
        public void Evaluate_NetSwing_PeaksAtMiddle_AndOnlyOwnsRightArm()
        {
            // 기존 동작 보존: 뜰채 스윙은 오른팔만 뺏고 왼팔은 걷기 스윙을 그대로 쓴다.
            NpcPoseDelta mid = NpcGesturePose.Evaluate(NpcGesture.NetSwing, 0.5f);
            Assert.AreEqual(NpcGesturePose.SwingMaxDeg, mid.rightArmDeg, 0.01f);
            Assert.IsTrue(mid.ownsRightArm);
            Assert.IsFalse(mid.ownsLeftArm);
            Assert.IsFalse(mid.ownsHead);
        }

        [Test]
        public void Evaluate_Nod_DrivesHeadOnly()
        {
            // t=0.125가 첫 끄덕임의 정점이다(t=0.25는 두 번 끄덕이는 사이의 영점이라 0이 나온다).
            NpcPoseDelta pose = NpcGesturePose.Evaluate(NpcGesture.Nod, 0.125f);
            Assert.IsTrue(pose.ownsHead);
            Assert.IsFalse(pose.ownsRightArm);
            Assert.IsFalse(pose.ownsLeftArm);
            Assert.Greater(Mathf.Abs(pose.headPitchDeg), 0.5f);
        }

        [Test]
        public void Evaluate_Recoil_OwnsBothArmsAndDipsBody()
        {
            NpcPoseDelta pose = NpcGesturePose.Evaluate(NpcGesture.Recoil, 0.5f);
            Assert.IsTrue(pose.ownsRightArm);
            Assert.IsTrue(pose.ownsLeftArm);
            Assert.IsTrue(pose.ownsBody);
            Assert.Less(pose.bodyOffsetY, 0f, "움찔은 몸을 낮춘다");
        }

        private static void AssertAtRest(NpcGesture gesture, float t)
        {
            NpcPoseDelta pose = NpcGesturePose.Evaluate(gesture, t);
            Assert.AreEqual(0f, pose.rightArmDeg, 0.01f, $"{gesture} t={t} 오른팔이 0이 아니다");
            Assert.AreEqual(0f, pose.leftArmDeg, 0.01f, $"{gesture} t={t} 왼팔이 0이 아니다");
            Assert.AreEqual(0f, pose.headPitchDeg, 0.01f, $"{gesture} t={t} 고개 pitch가 0이 아니다");
            Assert.AreEqual(0f, pose.headYawDeg, 0.01f, $"{gesture} t={t} 고개 yaw가 0이 아니다");
            Assert.AreEqual(0f, pose.bodyOffsetY, 0.001f, $"{gesture} t={t} 몸통 높이가 0이 아니다");
        }

        private static void AssertSamePose(NpcPoseDelta a, NpcPoseDelta b)
        {
            Assert.AreEqual(b.rightArmDeg, a.rightArmDeg, 0.001f);
            Assert.AreEqual(b.leftArmDeg, a.leftArmDeg, 0.001f);
            Assert.AreEqual(b.headPitchDeg, a.headPitchDeg, 0.001f);
            Assert.AreEqual(b.headYawDeg, a.headYawDeg, 0.001f);
            Assert.AreEqual(b.bodyOffsetY, a.bodyOffsetY, 0.001f);
        }

        private static float Magnitude(NpcPoseDelta pose)
        {
            return Mathf.Abs(pose.rightArmDeg) + Mathf.Abs(pose.leftArmDeg)
                + Mathf.Abs(pose.headPitchDeg) + Mathf.Abs(pose.headYawDeg)
                + Mathf.Abs(pose.bodyOffsetY) * 100f;
        }
    }
}
#endif
