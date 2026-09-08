#if UNITY_EDITOR
using NUnit.Framework;
using InsectGame.Battle;
using InsectGame.NPC;

namespace InsectGame.Tests
{
    /// <summary>
    /// 명부회 보스전 「장부」 압박의 순수 계산부(<see cref="LedgerPressure"/>).
    ///
    /// 이 규칙이 어긋나면 <b>전투가 조용히 불공정해진다</b> — 예외도 경고도 없이 보스가
    /// 매 턴 1.6배로 때리거나(임계가 너무 낮음), 반대로 압박이 영영 안 걸린다(계산 오류).
    /// 씬 없이 도는 순수 로직이라 여기서 고정한다.
    /// </summary>
    [TestFixture]
    public class LedgerPressureTests
    {
        // ── 켜짐/꺼짐 ────────────────────────────────────────────────
        [Test]
        public void IsActive_ZeroThreshold_IsOff()
        {
            // 0은 "장부 없음"이다 — 야생 전투와 아이 대결이 이 값으로 돈다.
            Assert.IsFalse(LedgerPressure.IsActive(0));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void IsActive_BelowMinimum_IsOff(int threshold)
        {
            // 임계가 2 이하면 반복 한 번(+2)에 즉시 터져 피할 방법이 없다.
            Assert.Less(threshold, LedgerPressure.MinThreshold);
            Assert.IsFalse(LedgerPressure.IsActive(threshold));
        }

        [Test]
        public void NextTally_Inactive_StaysZero()
        {
            Assert.AreEqual(0, LedgerPressure.NextTally(5, 0, true));
        }

        // ── 차고 지워지는 규칙 ───────────────────────────────────────
        [Test]
        public void NextTally_RepeatedAction_Gains()
        {
            Assert.AreEqual(LedgerPressure.SameActionGain,
                LedgerPressure.NextTally(0, 7, true));
        }

        [Test]
        public void NextTally_VariedAction_Relieves()
        {
            Assert.AreEqual(4 - LedgerPressure.VariedActionRelief,
                LedgerPressure.NextTally(4, 7, false));
        }

        [Test]
        public void NextTally_VariedAtZero_DoesNotGoNegative()
        {
            // 음수로 내려가면 "미리 벌어 둔 여유"가 생겨 뒤이은 연타를 공짜로 흘린다.
            Assert.AreEqual(0, LedgerPressure.NextTally(0, 7, false));
        }

        [Test]
        public void NextTally_NeverExceedsThreshold()
        {
            // 임계 위로 쌓이면 터진 직후에도 곧바로 다시 터진다(연속 정독).
            int tally = 0;
            for (int i = 0; i < 20; i++) tally = LedgerPressure.NextTally(tally, 5, true);
            Assert.AreEqual(5, tally);
        }

        /// <summary>
        /// <b>완화가 이득보다 작아야 한다.</b> 같다면 두 행동을 번갈아 쓰는 것만으로
        /// 장부가 영원히 0 근처에 묶여 압박이 사라진다 — 되풀이는 벌하되
        /// 쿨다운에 몰려 어쩔 수 없이 겹치는 것은 따라잡히게 두는 것이 이 설계다.
        /// </summary>
        [Test]
        public void Relief_IsSmallerThanGain()
        {
            Assert.Less(LedgerPressure.VariedActionRelief, LedgerPressure.SameActionGain);
        }

        // ── 발동과 예고 ─────────────────────────────────────────────
        [Test]
        public void IsFull_AtThreshold_Triggers()
        {
            Assert.IsTrue(LedgerPressure.IsFull(6, 6));
            Assert.IsFalse(LedgerPressure.IsFull(5, 6));
        }

        [Test]
        public void IsFull_Inactive_NeverTriggers()
        {
            Assert.IsFalse(LedgerPressure.IsFull(99, 0));
        }

        [Test]
        public void IsWarning_PrecedesTrigger_AndStopsAtIt()
        {
            // 예고 없이 터지면 긴장이 아니라 사고다 — 임계 직전 구간은 반드시 경고여야 한다.
            Assert.IsTrue(LedgerPressure.IsWarning(6 - LedgerPressure.WarnMargin, 6));
            Assert.IsTrue(LedgerPressure.IsWarning(5, 6));
            Assert.IsFalse(LedgerPressure.IsWarning(6, 6), "발동 시점은 경고가 아니라 발동이다");
            Assert.IsFalse(LedgerPressure.IsWarning(0, 6));
        }

        [Test]
        public void EveryThreshold_HasAWarningTurnBeforeTriggering()
        {
            // 연타만 하는 최악의 경우에도 **터지기 전에 경고 상태를 한 번은 지난다**.
            foreach (NpcBossDuels.BossDuel duel in NpcBossDuels.All())
            {
                int threshold = duel.ledgerThreshold;
                if (!LedgerPressure.IsActive(threshold)) continue;

                bool sawWarning = false;
                int tally = 0;
                for (int turn = 0; turn < 40; turn++)
                {
                    if (LedgerPressure.IsFull(tally, threshold)) break;
                    if (LedgerPressure.IsWarning(tally, threshold)) sawWarning = true;
                    tally = LedgerPressure.NextTally(tally, threshold, true);
                }
                Assert.IsTrue(sawWarning,
                    $"{duel.storyNpcId}(임계 {threshold})는 경고 없이 터진다 — 게이지가 예고 구실을 못 한다");
            }
        }

        // ── 게이지 ──────────────────────────────────────────────────
        [Test]
        public void Fill01_SpansZeroToOne()
        {
            Assert.AreEqual(0f, LedgerPressure.Fill01(0, 8), 0.001f);
            Assert.AreEqual(0.5f, LedgerPressure.Fill01(4, 8), 0.001f);
            Assert.AreEqual(1f, LedgerPressure.Fill01(8, 8), 0.001f);
        }

        [Test]
        public void Fill01_Inactive_IsZero()
        {
            Assert.AreEqual(0f, LedgerPressure.Fill01(3, 0), 0.001f);
        }

        // ── 피해 배율 ───────────────────────────────────────────────
        [Test]
        public void DamageMultiplier_OnlyWhenTriggered()
        {
            Assert.AreEqual(1f, LedgerPressure.DamageMultiplier(false), 0.001f);
            Assert.AreEqual(LedgerPressure.ReadDamageMultiplier,
                LedgerPressure.DamageMultiplier(true), 0.001f);
        }

        [Test]
        public void ReadMultiplier_HurtsButDoesNotOneShot()
        {
            // 1.0 이하면 압박이 아니고, 2배를 넘으면 예고를 봤어도 한 방에 정리된다.
            Assert.Greater(LedgerPressure.ReadDamageMultiplier, 1f);
            Assert.LessOrEqual(LedgerPressure.ReadDamageMultiplier, 2f);
        }

        // ── 게이지 단계 ─────────────────────────────────────────────
        /// <summary>
        /// <b>장부가 찰수록 단계도 올라야 한다.</b> 중간에 내려가는 구간이 있으면
        /// 가장 위험한 순간이 안전색으로 보인다 — 실제로 그랬다. 화면이
        /// <c>IsWarning</c>만 보고 색을 고르던 동안, 게이지가 가득 찬 순간
        /// (<c>IsWarning</c>은 정의상 그때 false다) 붉은색에서 평상색으로 되돌아갔다.
        ///
        /// 발동이 같은 턴에 곧바로 일어나던 동안에는 그 상태가 한 프레임도 안 보여
        /// 드러나지 않았다. 정독을 <b>못 쓴 턴에는 들고 기다리도록</b> 고친 뒤로
        /// 여러 턴 이어지는 상태가 되면서 눈에 띄었다.
        /// </summary>
        [Test]
        public void AlertOf_NeverDropsAsTheLedgerFills()
        {
            foreach (NpcBossDuels.BossDuel duel in NpcBossDuels.All())
            {
                int threshold = duel.ledgerThreshold;
                LedgerAlert previous = LedgerAlert.Calm;
                for (int tally = 0; tally <= threshold; tally++)
                {
                    LedgerAlert now = LedgerPressure.AlertOf(tally, threshold);
                    Assert.GreaterOrEqual((int)now, (int)previous,
                        $"{duel.storyNpcId}(임계 {threshold}): 장부 {tally}에서 단계가 " +
                        $"{previous} → {now}로 **내려간다** — 그 구간이 실제보다 안전해 보인다");
                    previous = now;
                }
            }
        }

        [Test]
        public void AlertOf_FullLedger_IsMarked()
        {
            // 가득 찬 상태는 「이미 적혔다」다 — 때릴 자리가 날 때까지 여러 턴 이어질 수 있다.
            Assert.AreEqual(LedgerAlert.Marked, LedgerPressure.AlertOf(6, 6));
            Assert.AreEqual(LedgerAlert.Warning, LedgerPressure.AlertOf(5, 6));
            Assert.AreEqual(LedgerAlert.Calm, LedgerPressure.AlertOf(0, 6));
        }

        [Test]
        public void AlertOf_Inactive_IsAlwaysCalm()
        {
            // 장부 없는 전투(야생·아이 대결)에서 게이지가 뜨면 안 된다.
            Assert.AreEqual(LedgerAlert.Calm, LedgerPressure.AlertOf(99, 0));
        }

        // ── 행동 키 ─────────────────────────────────────────────────
        /// <summary>
        /// 기본공격·도주 키는 <b>스킬 인덱스와 겹치면 안 된다.</b> 겹치면 "기본공격 뒤
        /// 0번 스킬"이 반복으로 잘못 잡혀, 패턴을 바꿨는데도 장부가 찬다.
        /// </summary>
        [Test]
        public void ActionKeys_DoNotCollideWithSkillIndices()
        {
            Assert.Less(LedgerPressure.BasicAttackKey, 0);
            Assert.Less(LedgerPressure.EscapeKey, 0);
            Assert.AreNotEqual(LedgerPressure.BasicAttackKey, LedgerPressure.EscapeKey);
            Assert.AreNotEqual(LedgerPressure.NoActionKey, LedgerPressure.BasicAttackKey);
            Assert.AreNotEqual(LedgerPressure.NoActionKey, LedgerPressure.EscapeKey);
        }
    }
}
#endif
