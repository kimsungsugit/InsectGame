#if UNITY_EDITOR
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    [TestFixture]
    public class SkillUILayoutTests
    {
        // 실제 호출부 수치. 여기 값이 바뀌면 이 테스트도 함께 바꿔야 한다 — 그게 목적이다.
        private const float BattleCardHeight = 216f;
        private const float BattleRowsTop = 144f;
        private const float BattleHorizontalPadding = 14f;
        private const float RaidCardHeight = 212f;
        private const float RaidRowsTop = 140f;
        private const float RaidHorizontalPadding = 12f;
        private const float RowGap = 6f;
        private const float BottomPadding = 10f;

        // 스킬 카드 배경. 호버는 평시보다 밝아 어두운 글씨와 정면으로 부딪힌다.
        // 호버 위에 덧칠은 없다 — 있으면(예전 흰색 6%) 글자까지 함께 밝아져 이 수치가 무의미해진다.
        private static readonly Color CardBackground = new Color(0.08f, 0.10f, 0.20f);
        private static readonly Color CardHoverBackground = new Color(0.18f, 0.22f, 0.38f);

        [Test]
        public void GetNameRect_NormalCard_StaysInsideCardAndKeepsReadableHeight()
        {
            Rect card = new Rect(30f, 40f, 240f, RaidCardHeight);

            Rect label = SkillUILayout.GetNameRect(card, 50f, RaidHorizontalPadding, 50f);

            Assert.GreaterOrEqual(label.height, SkillUILayout.MinimumSkillNameHeight);
            Assert.GreaterOrEqual(label.xMin, card.xMin);
            Assert.LessOrEqual(label.xMax, card.xMax);
            Assert.GreaterOrEqual(label.yMin, card.yMin);
            Assert.LessOrEqual(label.yMax, card.yMax);
        }

        [Test]
        public void GetTouchHeight_MobileLayout_NeverDropsBelowProjectMinimum()
        {
            float height = SkillUILayout.GetTouchHeight(true, 44f, 48f);

            Assert.GreaterOrEqual(height, UIScale.MinTouchHeight);
            Assert.AreEqual(44f, SkillUILayout.GetTouchHeight(false, 44f, 80f));
        }

        [Test]
        public void GetReadableAccent_DarkAccent_BecomesBrighterAndPreservesAlpha()
        {
            Color dark = new Color(0.4f, 0.15f, 0.5f, 0.65f);

            Color readable = SkillUILayout.GetReadableAccent(dark);

            Assert.Greater(readable.grayscale, dark.grayscale);
            Assert.AreEqual(dark.a, readable.a, 0.0001f);
        }

        /// <summary>
        /// 타입 줄은 <b>호버 상태에서</b> 가장 안 보였다. 호버는 카드 배경을 밝히는 연출이라
        /// 어두운 속성색과 정면으로 부딪히는데, 처음 보정은 평시 배경만 보고 계수를 잡아
        /// Dark 3.65 · Poison 3.82로 AA(4.5) 아래에 머물렀다 — 하필 <b>지금 고르려는 카드</b>다.
        /// 평시만 재는 테스트는 이 회귀를 못 잡으므로 두 배경을 함께 잰다.
        /// </summary>
        [TestCase(0.4f, 0.15f, 0.5f, TestName = "Dark")]
        [TestCase(0.6f, 0.2f, 0.8f, TestName = "Poison")]
        [TestCase(0.2f, 0.5f, 1f, TestName = "Water")]
        [TestCase(0.9f, 0.35f, 0.3f, TestName = "Damage")]
        [TestCase(0.68f, 0.35f, 0.88f, TestName = "PoisonDot")]
        [TestCase(0.7f, 0.5f, 0.2f, TestName = "Earth")]
        [TestCase(1f, 0.95f, 0.7f, TestName = "Light")]
        public void GetReadableAccent_AnyAccent_ClearsAaOnBothCardBackgrounds(float r, float g, float b)
        {
            Color readable = SkillUILayout.GetReadableAccent(new Color(r, g, b));

            Assert.GreaterOrEqual(ContrastRatio(readable, CardBackground), 4.5f,
                "평시 카드 배경에서 타입 줄이 안 읽힌다");
            Assert.GreaterOrEqual(ContrastRatio(readable, CardHoverBackground), 4.5f,
                "호버 카드 배경에서 타입 줄이 안 읽힌다 — 고르려는 카드가 가장 안 보인다");
        }

        [Test]
        public void GetDetailRows_BattleCard_KeepsRowsReadableAndOffTheBottomBorder()
        {
            Rect card = new Rect(30f, 40f, 240f, BattleCardHeight);

            SkillCardDetailRows rows = SkillUILayout.GetDetailRows(
                card, BattleRowsTop, BattleHorizontalPadding,
                SkillUILayout.MinimumDetailRowHeight, RowGap, BottomPadding);

            AssertRowsAreUsable(card, rows, BottomPadding);
        }

        [Test]
        public void GetDetailRows_RaidCard_KeepsRowsReadableAndOffTheBottomBorder()
        {
            Rect card = new Rect(30f, 40f, 240f, RaidCardHeight);

            SkillCardDetailRows rows = SkillUILayout.GetDetailRows(
                card, RaidRowsTop, RaidHorizontalPadding,
                SkillUILayout.MinimumDetailRowHeight, RowGap, BottomPadding);

            AssertRowsAreUsable(card, rows, BottomPadding);
        }

        /// <summary>
        /// 카드가 실제로 쓰는 세로 배분이 서로 겹치지 않는지. 아이콘 · 이름 · 타입 · 정보 2행이
        /// 216(1v1) / 212(레이드) 안에 전부 들어가야 하고, 어느 두 구역도 포개지면 안 된다.
        /// </summary>
        [TestCase(BattleCardHeight, 54f, 50f, 110f, 28f, BattleRowsTop, BattleHorizontalPadding, 50f)]
        [TestCase(RaidCardHeight, 50f, 50f, 106f, 28f, RaidRowsTop, RaidHorizontalPadding, 44f)]
        public void CardBands_DoNotOverlapAndFitInsideTheCard(
            float cardHeight, float nameTop, float nameHeight,
            float typeTop, float typeHeight,
            float rowsTop, float horizontalPadding, float iconBottom)
        {
            Rect card = new Rect(30f, 40f, 240f, cardHeight);
            Rect name = SkillUILayout.GetNameRect(card, nameTop, horizontalPadding, nameHeight);
            Rect type = new Rect(card.x + horizontalPadding, card.y + typeTop,
                card.width - horizontalPadding * 2f, typeHeight);

            SkillCardDetailRows rows = SkillUILayout.GetDetailRows(
                card, rowsTop, horizontalPadding,
                SkillUILayout.MinimumDetailRowHeight, RowGap, BottomPadding);

            Assert.GreaterOrEqual(name.yMin, card.y + iconBottom, "이름이 아이콘 위로 올라탄다");
            Assert.LessOrEqual(name.yMax, type.yMin, "이름과 타입 줄이 겹친다");
            Assert.LessOrEqual(type.yMax, rows.Power.yMin, "타입 줄과 정보 행이 겹친다");
            AssertRowsAreUsable(card, rows, BottomPadding);
        }

        /// <summary>
        /// 호출부가 22 같은 옛 값을 넘겨도 행이 최소 높이 아래로 내려가지 않는다. 22px 상자에
        /// 20px 한글을 넣던 것이 원래 잘림의 원인이었다.
        /// </summary>
        [Test]
        public void GetDetailRows_TooSmallRequest_IsLiftedToMinimumRowHeight()
        {
            Rect card = new Rect(0f, 0f, 240f, BattleCardHeight);

            SkillCardDetailRows rows = SkillUILayout.GetDetailRows(
                card, BattleRowsTop, BattleHorizontalPadding, 22f, RowGap, BottomPadding);

            Assert.AreEqual(SkillUILayout.MinimumDetailRowHeight, rows.Power.height, 0.001f);
        }

        /// <summary>행이 아무리 커도 하단 여백을 먹지 않는다 — 카드 테두리를 침범하던 자리다.</summary>
        [Test]
        public void GetDetailRows_OversizedRequest_StillLeavesTheBottomPadding()
        {
            Rect card = new Rect(0f, 0f, 240f, BattleCardHeight);

            SkillCardDetailRows rows = SkillUILayout.GetDetailRows(
                card, BattleRowsTop, BattleHorizontalPadding, 400f, RowGap, BottomPadding);

            Assert.LessOrEqual(rows.Cooldown.yMax, card.yMax - BottomPadding);
        }

        private static void AssertRowsAreUsable(Rect card, SkillCardDetailRows rows, float bottomPadding)
        {
            Assert.GreaterOrEqual(rows.Power.height, SkillUILayout.MinimumDetailRowHeight,
                "행이 한글 글자 높이보다 낮다 — 위아래가 잘린다");
            Assert.AreEqual(rows.Power.height, rows.Cooldown.height, 0.001f);

            // 위력과 상성은 같은 줄의 좌/우 칸이다.
            Assert.AreEqual(rows.Power.yMin, rows.Effectiveness.yMin, 0.001f,
                "위력과 상성 배지가 다른 줄에 있다");
            Assert.LessOrEqual(rows.Power.xMax, rows.Effectiveness.xMin,
                "위력이 길면 상성 배지를 밟는다");

            Assert.GreaterOrEqual(rows.Cooldown.yMin - rows.Power.yMax, RowGap,
                "두 행이 붙어 있다");
            Assert.GreaterOrEqual(rows.Power.yMin, card.yMin);
            Assert.LessOrEqual(rows.Cooldown.yMax, card.yMax - bottomPadding,
                "마지막 행이 카드 아래 테두리까지 내려가 여백이 없다");
            Assert.GreaterOrEqual(rows.Power.xMin, card.xMin);
            Assert.LessOrEqual(rows.Effectiveness.xMax, card.xMax);
        }

        /// <summary>WCAG 2.1 명암비. 본문 기준은 4.5:1.</summary>
        private static float ContrastRatio(Color foreground, Color background)
        {
            float a = RelativeLuminance(foreground);
            float b = RelativeLuminance(background);
            float high = Mathf.Max(a, b);
            float low = Mathf.Min(a, b);
            return (high + 0.05f) / (low + 0.05f);
        }

        private static float RelativeLuminance(Color c)
        {
            return 0.2126f * ToLinear(c.r) + 0.7152f * ToLinear(c.g) + 0.0722f * ToLinear(c.b);
        }

        private static float ToLinear(float channel)
        {
            return channel <= 0.03928f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }
    }
}
#endif
