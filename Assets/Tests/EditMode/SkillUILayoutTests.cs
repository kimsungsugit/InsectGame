#if UNITY_EDITOR
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    [TestFixture]
    public class SkillUILayoutTests
    {
        [Test]
        public void GetNameRect_NormalCard_StaysInsideCardAndKeepsReadableHeight()
        {
            Rect card = new Rect(30f, 40f, 240f, 212f);

            Rect label = SkillUILayout.GetNameRect(card, 50f, 12f, 56f);

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

        [Test]
        public void GetDetailRows_BattleCard_PreservesNameAndSeparatesAllRows()
        {
            Rect card = new Rect(30f, 40f, 240f, 216f);
            Rect name = SkillUILayout.GetNameRect(card, 54f, 14f, 58f);

            SkillCardDetailRows rows = SkillUILayout.GetDetailRows(
                card, 144f, 14f, 22f, 2f);

            Assert.GreaterOrEqual(name.height, SkillUILayout.MinimumSkillNameHeight);
            AssertRowsDoNotOverlap(card, rows, 2f);
        }

        [Test]
        public void GetDetailRows_RaidCard_PreservesNameAndSeparatesAllRows()
        {
            Rect card = new Rect(30f, 40f, 240f, 212f);
            Rect name = SkillUILayout.GetNameRect(card, 50f, 12f, 56f);

            SkillCardDetailRows rows = SkillUILayout.GetDetailRows(
                card, 136f, 12f, 22f, 2f);

            Assert.GreaterOrEqual(name.height, SkillUILayout.MinimumSkillNameHeight);
            AssertRowsDoNotOverlap(card, rows, 2f);
        }

        private static void AssertRowsDoNotOverlap(
            Rect card,
            SkillCardDetailRows rows,
            float expectedGap)
        {
            Assert.GreaterOrEqual(rows.Effectiveness.yMin - rows.Power.yMax, expectedGap);
            Assert.GreaterOrEqual(rows.Cooldown.yMin - rows.Effectiveness.yMax, expectedGap);
            Assert.GreaterOrEqual(rows.Power.yMin, card.yMin);
            Assert.LessOrEqual(rows.Cooldown.yMax, card.yMax);
        }
    }
}
#endif
