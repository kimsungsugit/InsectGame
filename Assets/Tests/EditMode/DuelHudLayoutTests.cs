#if UNITY_EDITOR
using InsectGame.Data;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    [TestFixture]
    public class DuelHudLayoutTests
    {
        [TestCase(1920f, 1080f, false)]
        [TestCase(1440f, 1080f, false)]
        [TestCase(2560f, 1080f, false)]
        [TestCase(1080f, 1920f, true)]
        public void CombatCards_SafeAspectRatios_StayInsideWithoutOverlap(float width, float height, bool portrait)
        {
            var safe = new Rect(48f, 48f, width - 96f, height - 96f);
            Rect left = DuelHudLayout.HpCard(safe, true);
            Rect right = DuelHudLayout.HpCard(safe, false);
            AssertContained(safe, left);
            AssertContained(safe, right);
            Assert.Less(left.center.x, right.center.x);
            Assert.IsFalse(left.Overlaps(right));
            float deckHeight = portrait ? 610f : 300f;
            var deck = new Rect(safe.x, safe.yMax - deckHeight, safe.width, deckHeight);
            Assert.IsFalse(left.Overlaps(deck));
            Assert.IsFalse(right.Overlaps(deck));
            Rect basic = DuelHudLayout.UtilityCard(deck, portrait, false);
            Rect escape = DuelHudLayout.UtilityCard(deck, portrait, true);
            AssertContained(deck, basic);
            AssertContained(deck, escape);
            Assert.IsFalse(basic.Overlaps(escape));
            for (int count = 1; count <= 4; count++)
                for (int i = 0; i < count; i++)
                {
                    Rect card = DuelHudLayout.SkillCard(deck, portrait, i, count);
                    AssertContained(deck, card);
                    Assert.GreaterOrEqual(card.height, UIScale.MinTouchHeight);
                    Assert.GreaterOrEqual(card.width, 200f);
                    Assert.IsFalse(card.Overlaps(basic));
                    Assert.IsFalse(card.Overlaps(escape));
                    for (int j = 0; j < i; j++)
                        Assert.IsFalse(card.Overlaps(DuelHudLayout.SkillCard(deck, portrait, j, count)));
                }
        }

        [TestCase(SkillEffectType.Heal, true)]
        [TestCase(SkillEffectType.BuffAttack, true)]
        [TestCase(SkillEffectType.DefenseBuff, true)]
        [TestCase(SkillEffectType.Damage, false)]
        [TestCase(SkillEffectType.DebuffAttack, false)]
        [TestCase(SkillEffectType.Stun, false)]
        [TestCase(SkillEffectType.PoisonDot, false)]
        public void SkillTargetLabel_EffectType_IdentifiesCorrectSide(SkillEffectType type, bool self)
        {
            Assert.AreEqual(self, DuelHudLayout.TargetsSelf(type));
        }

        private static void AssertContained(Rect outer, Rect inner)
        {
            Assert.GreaterOrEqual(inner.xMin, outer.xMin);
            Assert.GreaterOrEqual(inner.yMin, outer.yMin);
            Assert.LessOrEqual(inner.xMax, outer.xMax + 0.01f);
            Assert.LessOrEqual(inner.yMax, outer.yMax + 0.01f);
        }
    }
}
#endif
