#if UNITY_EDITOR
using InsectGame.UI;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 레이드 팀 슬롯의 화면 x 좌표. 팀 러시 투사체·AOE 피해 팝업·단일 피격 연출 세 곳이 같은 식을
    /// 따로 갖고 있다가 하나로 합쳐졌다 — 그중 팀 러시판만 1인 팀 중앙 정렬을 빠뜨리고 있었다.
    /// </summary>
    [TestFixture]
    public class RaidSlotLayoutTests
    {
        private const float Width = 1920f;

        [Test]
        public void AnchorX_SingleMember_IsCentered()
        {
            // ★ 회귀 고정 — 합치기 전 팀 러시판은 여기서 0.15*W(화면 왼쪽)를 냈다.
            Assert.AreEqual(Width * 0.5f, RaidSlotLayout.AnchorX(0, 1, Width));
        }

        [Test]
        public void AnchorX_FiveMembers_SpansEvenlyBetweenEnds()
        {
            float first = RaidSlotLayout.AnchorX(0, 5, Width);
            float last = RaidSlotLayout.AnchorX(4, 5, Width);

            Assert.AreEqual(Width * RaidSlotLayout.StartRatio, first, 0.01f);
            Assert.AreEqual(
                Width * (RaidSlotLayout.StartRatio + RaidSlotLayout.SpanRatio), last, 0.01f);

            float step = RaidSlotLayout.AnchorX(1, 5, Width) - first;
            for (int i = 1; i < 5; i++)
            {
                Assert.AreEqual(
                    step,
                    RaidSlotLayout.AnchorX(i, 5, Width) - RaidSlotLayout.AnchorX(i - 1, 5, Width),
                    0.01f,
                    $"slot {i} 간격");
            }
        }

        [Test]
        public void AnchorX_ThreeMembers_MiddleIsScreenCenter()
        {
            Assert.AreEqual(Width * 0.5f, RaidSlotLayout.AnchorX(1, 3, Width), 0.01f);
        }

        [Test]
        public void AnchorX_SlotOutOfRange_ClampsInsteadOfOverflowing()
        {
            float last = RaidSlotLayout.AnchorX(4, 5, Width);

            Assert.AreEqual(last, RaidSlotLayout.AnchorX(9, 5, Width), 0.01f);
            Assert.AreEqual(RaidSlotLayout.AnchorX(0, 5, Width),
                RaidSlotLayout.AnchorX(-3, 5, Width), 0.01f);
        }

        [Test]
        public void AnchorX_EmptyTeam_DoesNotDivideByZero()
        {
            Assert.AreEqual(Width * 0.5f, RaidSlotLayout.AnchorX(0, 0, Width));
        }
    }
}
#endif
