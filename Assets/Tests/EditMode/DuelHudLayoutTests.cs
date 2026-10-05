#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Battle;
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

        // ── 연속 공격 배지 — 화면 한 장(HudFrame)에서 전투 HUD의 다른 자리와 겹치지 않는다 ──
        //
        // 예전 배지는 오른쪽 위 고정 좌표(y 100, 폭 180)라 가로·세로 모두 상대 HP 카드를, 세로에선 배속 버튼까지 덮었다.
        // 자리는 전부 화면 파일의 순수 함수에서 받는다(식을 여기 다시 세우지 않는다).

        [TestCase(1280f, 720f, 0f, 0f, false)]
        [TestCase(720f, 1280f, 0f, 0f, true)]
        [TestCase(1920f, 1080f, 0f, 0f, false)]
        [TestCase(1080f, 1920f, 0f, 0f, true)]
        [TestCase(1080f, 2400f, 110f, 60f, true)]
        [TestCase(2400f, 1080f, 0f, 0f, true)]
        [TestCase(1440f, 1080f, 0f, 0f, false)]
        public void ComboBadge_UnderMyHpCard_ClearsEveryBattleHudPiece(float width, float height,
            float notchTop, float gestureBottom, bool mobilePlatform)
        {
            HudFrame f = HudFrame.ForScreen(width, height, 0f, 0f, notchTop, gestureBottom, mobilePlatform);
            Rect safe = DuelHudLayout.Safe(f);
            Rect player = DuelHudLayout.HpCard(safe, true);
            Rect enemy = DuelHudLayout.HpCard(safe, false);
            var fixedPieces = new List<KeyValuePair<string, Rect>>
            {
                Piece("내 HP 카드", player),
                Piece("상대 HP 카드", enemy),
                Piece("머리 띠", DuelHudLayout.Heading(f)),
                Piece("배속 버튼", DuelHudLayout.SpeedControl(f)),
                Piece("행동 패널", DuelHudLayout.SkillDeck(f)),
                Piece("턴 배너", DuelHudLayout.TurnBanner(f)),
                Piece("공격 배너", DuelHudLayout.PhaseBanner(f)),
                Piece("행동 문구", DuelHudLayout.ActionBar(f)),
                Piece("전투 문구 1줄", BattleEffectTextOverlay.RowRect(f, 0)),
                Piece("전투 문구 2줄", BattleEffectTextOverlay.RowRect(f, 1)),
                Piece("전투 문구 3줄", BattleEffectTextOverlay.RowRect(f, 2)),
            };

            foreach (bool playerChip in new[] { false, true })
                foreach (bool enemyChip in new[] { false, true })
                    foreach (bool ledger in new[] { false, true })
                    {
                        Rect badge = BattleHudStack.ComboBadge(player, playerChip);
                        // 뜰 때 커진 순간까지 잰다 — 가운데 축으로 ComboPulsePeak배.
                        Rect pulsed = Grow(badge, BattleHudStack.ComboPulsePeak);
                        AssertContained(safe, badge);
                        Assert.GreaterOrEqual(pulsed.xMin, 0f);
                        Assert.LessOrEqual(pulsed.xMax, f.Width);

                        var pieces = new List<KeyValuePair<string, Rect>>(fixedPieces);
                        // 칩 폭은 문구가 정한다 — 가장 넓은 경우(카드 폭 전체)로 잰다.
                        if (playerChip) pieces.Add(Piece("내 보정 칩", BattleHudStack.EnvironmentChip(player, true, false, player.width)));
                        if (ledger) pieces.Add(Piece("장부 게이지", BattleHudStack.LedgerGauge(enemy)));
                        if (enemyChip) pieces.Add(Piece("상대 보정 칩", BattleHudStack.EnvironmentChip(enemy, false, ledger, enemy.width)));
                        pieces.Add(Piece("대결 말풍선", BattleHudStack.Bubble(enemy, ledger, enemyChip)));

                        foreach (KeyValuePair<string, Rect> piece in pieces)
                            Assert.IsFalse(pulsed.Overlaps(piece.Value),
                                $"{width}x{height} 칩{playerChip}/{enemyChip} 장부{ledger}: 연속 배지 {pulsed} ↔ {piece.Key} {piece.Value}");
                    }
        }

        [Test]
        public void ComboBadge_WithMyEnvironmentChip_SitsBelowTheChip()
        {
            Rect card = new Rect(24f, 100f, 460f, DuelHudLayout.HpCardHeight);
            Rect chip = BattleHudStack.EnvironmentChip(card, true, false, 200f);
            Rect plain = BattleHudStack.ComboBadge(card, false);
            Rect stacked = BattleHudStack.ComboBadge(card, true);
            Assert.AreEqual(card.x, plain.x);
            Assert.Greater(plain.y, card.yMax);
            Assert.Greater(stacked.y, chip.yMax);
            Assert.AreEqual(BattleHudStack.ComboBadgeHeight, stacked.height);
        }

        [Test]
        public void ComboPulse_Appearing_ShrinksFromPeakToOne()
        {
            Assert.AreEqual(BattleHudStack.ComboPulsePeak, BattleHudStack.ComboPulse(0f, false), 1e-4f);
            float mid = BattleHudStack.ComboPulse(BattleHudStack.ComboPulseSeconds * 0.5f, false);
            Assert.Less(mid, BattleHudStack.ComboPulsePeak);
            Assert.Greater(mid, 1f);
            Assert.AreEqual(1f, BattleHudStack.ComboPulse(BattleHudStack.ComboPulseSeconds, false));
            Assert.AreEqual(1f, BattleHudStack.ComboPulse(2f, false));
            Assert.AreEqual(1f, BattleHudStack.ComboPulse(0f, true), "줄인 움직임이면 커지지 않는다");
        }

        // ── HP 카드 상태 줄(BattleStatusLine) ──

        [Test]
        public void StatusTally_SameKindTwice_KeepsLongestAndIgnoresOtherSide()
        {
            var effects = new[]
            {
                Effect(true, InsectBattleController.EffectKind.Dot, 5f, 2),
                Effect(true, InsectBattleController.EffectKind.Dot, 5f, 3),
                Effect(true, InsectBattleController.EffectKind.AtkBuff, 0.2f, 1),
                Effect(true, InsectBattleController.EffectKind.AtkBuff, -0.2f, 2),
                Effect(true, InsectBattleController.EffectKind.DefBuff, 0.3f, 0),
                Effect(false, InsectBattleController.EffectKind.AtkBuff, 0.5f, 4),
            };
            BattleStatusLine.Turns mine = BattleStatusLine.Tally(1, effects, true);
            Assert.AreEqual(1, mine.Stun);
            Assert.AreEqual(3, mine.Poison);
            Assert.AreEqual(1, mine.AttackUp);
            Assert.AreEqual(2, mine.AttackDown);
            Assert.AreEqual(0, mine.DefenseUp, "남은 턴 0은 이미 끝난 효과");

            BattleStatusLine.Turns theirs = BattleStatusLine.Tally(0, effects, false);
            Assert.AreEqual(4, theirs.AttackUp);
            Assert.AreEqual(0, theirs.Poison);
            Assert.AreEqual(0, theirs.Stun);
        }

        [Test]
        public void StatusCompose_NoEffects_ShowsHpOrLowHpOnly()
        {
            var none = new BattleStatusLine.Turns();
            Assert.AreEqual("HP", BattleStatusLine.Compose(false, none, Chars10, 300f));
            Assert.AreEqual("체력 위험", BattleStatusLine.Compose(true, none, Chars10, 300f));
        }

        [Test]
        public void StatusCompose_StunFirst_ThenPoisonThenDebuffThenBuff()
        {
            var t = new BattleStatusLine.Turns { Stun = 1, Poison = 3, AttackUp = 2, AttackDown = 1 };
            Assert.AreEqual("기절 1턴  중독 3턴  공격↓ 1턴  공격↑ 2턴", BattleStatusLine.Compose(false, t, Chars10, 1000f));
            Assert.AreEqual("체력 위험  기절 1턴  중독 3턴  공격↓ 1턴  공격↑ 2턴", BattleStatusLine.Compose(true, t, Chars10, 1000f));
        }

        [Test]
        public void StatusCompose_TooNarrow_FoldsTailIntoCount()
        {
            var t = new BattleStatusLine.Turns { Stun = 1, Poison = 3, AttackUp = 2 };
            // 글자당 10px — "기절 1턴  중독 3턴  공격↑ 2턴"은 20글자 = 200px.
            Assert.AreEqual("기절 1턴  중독 3턴  공격↑ 2턴", BattleStatusLine.Compose(false, t, Chars10, 200f));
            Assert.AreEqual("기절 1턴  중독 3턴 외 1", BattleStatusLine.Compose(false, t, Chars10, 160f));
            Assert.AreEqual("기절 1턴 외 2", BattleStatusLine.Compose(false, t, Chars10, 150f));
        }

        [Test]
        public void StatusCompose_LowHpAndNarrow_DropsHeadOnlyWhenNoTagFitsBesideIt()
        {
            var t = new BattleStatusLine.Turns { Stun = 1, Poison = 3, AttackUp = 2 };
            Assert.AreEqual("체력 위험  기절 1턴 외 2", BattleStatusLine.Compose(true, t, Chars10, 160f));
            Assert.AreEqual("기절 1턴 외 2", BattleStatusLine.Compose(true, t, Chars10, 100f));
        }

        [Test]
        public void StatusKey_DifferentContent_DifferentKey()
        {
            var stun = new BattleStatusLine.Turns { Stun = 1 };
            var poison = new BattleStatusLine.Turns { Poison = 1 };
            Assert.AreNotEqual(stun.Key(false), poison.Key(false));
            Assert.AreNotEqual(stun.Key(false), stun.Key(true));
            Assert.AreEqual(stun.Key(false), new BattleStatusLine.Turns { Stun = 1 }.Key(false));
        }

        private static float Chars10(string s) => s.Length * 10f;

        private static KeyValuePair<string, Rect> Piece(string name, Rect rect) => new KeyValuePair<string, Rect>(name, rect);

        private static Rect Grow(Rect r, float scale)
        {
            Vector2 c = r.center;
            return new Rect(c.x - r.width * scale * 0.5f, c.y - r.height * scale * 0.5f, r.width * scale, r.height * scale);
        }

        private static InsectBattleController.EffectSnapshot Effect(bool player, InsectBattleController.EffectKind kind,
            float value, int turns)
        {
            return new InsectBattleController.EffectSnapshot
            {
                targetIsPlayer = player,
                kind = kind,
                value = value,
                remainingTurns = turns
            };
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
