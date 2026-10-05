#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Battle;
using InsectGame.Data;
using InsectGame.Story;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 「이야기 전투」의 화면(2026-10-04) — 대화창 「승부!」, 1대1 팀 대결의 남은 곤충 공·교체 문구, 레이드 변신 문구·수문장 등장 배너,
    /// 그리고 그 문구들의 조사(이/가·을/를).
    ///
    /// 자리는 전부 화면 파일의 순수 함수(<see cref="DuelTeamHud"/>·<see cref="RaidStageLayout"/>·<see cref="DialogueDuelPrompt"/>)를 부른다 —
    /// 식을 여기 다시 세우지 않는다. IMGUI 그리기 자체는 QA 빌드(<c>-battleScenario story</c>·<c>team-duel</c>·<c>raid-forms</c>·<c>guardian-intro</c>)로 본다.
    /// </summary>
    [TestFixture]
    public class StoryBattleHudTests
    {
        // ── 조사 ──

        [TestCase("집게", "가")]
        [TestCase("저울", "이")]
        [TestCase("관장 하월", "이")]
        [TestCase("라온", "이")]
        [TestCase("상대", "가")]
        [TestCase("사마귀(이로치)", "가")]   // 닫는 괄호는 건너뛰고 그 앞 글자(치)
        [TestCase("나방!", "이")]
        [TestCase("2", "가")]                // 이
        [TestCase("3", "이")]                // 삼
        [TestCase("10", "이")]               // 십(끝 0 — 받침)
        public void IGa_FollowsTheFinalConsonant(string word, string expected)
        {
            Assert.AreEqual(expected, KoreanJosa.IGa(word));
        }

        [TestCase("지네", "를")]
        [TestCase("사슴벌레", "를")]
        [TestCase("이름 잃은 나방", "을")]
        [TestCase("아틀라스나방", "을")]
        [TestCase("장수풍뎅이", "를")]
        [TestCase("왕잠자리", "를")]
        [TestCase("곰", "을")]
        public void EulReul_FollowsTheFinalConsonant(string word, string expected)
        {
            Assert.AreEqual(expected, KoreanJosa.EulReul(word));
        }

        [Test]
        public void OtherParticles_FollowTheFinalConsonant()
        {
            Assert.AreEqual("는", KoreanJosa.EunNeun("집게"));
            Assert.AreEqual("은", KoreanJosa.EunNeun("저울"));
            Assert.AreEqual("와", KoreanJosa.WaGwa("집게"));
            Assert.AreEqual("과", KoreanJosa.WaGwa("저울"));
            Assert.AreEqual("집게가", KoreanJosa.With("집게", KoreanJosa.IGa));
        }

        [TestCase("Atlas")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("!!")]
        public void UnknownEnding_WritesBothForms_InsteadOfGuessing(string word)
        {
            Assert.IsNull(KoreanJosa.HasFinalConsonant(word));
            Assert.AreEqual("이(가)", KoreanJosa.IGa(word));
            Assert.AreEqual("을(를)", KoreanJosa.EulReul(word));
        }

        [Test]
        public void SendOutText_PicksParticlesForBothNames()
        {
            Assert.AreEqual("집게가 지네를 내보냈다!", DuelTeamHud.SendOutText("집게", "지네"));
            Assert.AreEqual("관장 하월이 이름 잃은 나방을 내보냈다!", DuelTeamHud.SendOutText("관장 하월", "이름 잃은 나방"));
            Assert.AreEqual("상대가 곤충을 내보냈다!", DuelTeamHud.SendOutText(null, ""));
        }

        // ── 남은 곤충 공 ──

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        public void TeamBalls_SitOnTheTopRow_BetweenLabelAndLevel(int count)
        {
            Rect card = new Rect(1436f, 100f, 460f, DuelHudLayout.HpCardHeight);
            Rect row = DuelTeamHud.BallRow(card, count);
            Assert.LessOrEqual(row.xMax, card.xMax - DuelTeamHud.LevelReserve + 0.01f, "레벨 글자를 밟는다");
            Assert.GreaterOrEqual(row.x, card.x + DuelTeamHud.CardTextInset + DuelTeamHud.MinLabelWidth - 0.01f, "「상대 곤충」 자리를 밟는다");
            Assert.AreEqual(DuelTeamHud.BallSize, row.height, 0.01f, "넓은 카드에선 공을 줄이지 않는다");
            // 윗줄(y+10~34) 안 — HP 이름 줄(y+36~)을 건드리지 않는다.
            Assert.GreaterOrEqual(row.y - DuelTeamHud.CurrentRing, card.y + 8f);
            Assert.LessOrEqual(row.yMax + DuelTeamHud.CurrentRing, card.y + 36f);
            Rect previous = default;
            for (int i = 0; i < count; i++)
            {
                Rect ball = DuelTeamHud.Ball(row, i, count);
                Assert.GreaterOrEqual(ball.x, row.x - 0.01f);
                Assert.LessOrEqual(ball.xMax, row.xMax + 0.01f);
                if (i > 0) Assert.Greater(ball.x - previous.xMax, DuelTeamHud.CurrentRing * 2f - 0.01f, "고리 두른 공이 이웃 공에 닿는다");
                previous = ball;
            }
        }

        [Test]
        public void TeamBalls_NarrowCard_ShrinkButKeepTheLabelRoom()
        {
            Rect card = new Rect(0f, 0f, 300f, DuelHudLayout.HpCardHeight);
            Rect row = DuelTeamHud.BallRow(card, 5);
            Assert.GreaterOrEqual(row.x, card.x + DuelTeamHud.CardTextInset + DuelTeamHud.MinLabelWidth - 0.01f);
            Assert.LessOrEqual(row.xMax, card.xMax - DuelTeamHud.LevelReserve + 0.01f);
            Assert.Less(row.height, DuelTeamHud.BallSize);
            Assert.Greater(row.height, 0f);
        }

        [Test]
        public void ShownIndex_LagsOneBehindWhileTheFaintedInsectIsStillOnScreen()
        {
            Assert.AreEqual(0, DuelTeamHud.ShownIndex(1, true), "컨트롤러는 이미 둘째 — 화면은 아직 첫째가 쓰러지는 중");
            Assert.AreEqual(1, DuelTeamHud.ShownIndex(1, false));
            Assert.AreEqual(0, DuelTeamHud.ShownIndex(0, true));
            Assert.AreEqual(4, DuelTeamHud.ShownIndex(4, false));
        }

        [Test]
        public void BallState_BeforeIsFainted_ShownFollowsItsBar_AfterIsWaiting()
        {
            Assert.AreEqual(DuelTeamHud.TeamBall.Fainted, DuelTeamHud.BallState(0, 1, false));
            Assert.AreEqual(DuelTeamHud.TeamBall.Current, DuelTeamHud.BallState(1, 1, false));
            Assert.AreEqual(DuelTeamHud.TeamBall.Fainted, DuelTeamHud.BallState(1, 1, true));
            Assert.AreEqual(DuelTeamHud.TeamBall.Waiting, DuelTeamHud.BallState(2, 1, false));
        }

        // ── 교체 문구 — 화면 여러 장에서 전투 HUD의 다른 자리와 겹치지 않는다 ──

        [TestCase(1280f, 720f, 0f, 0f, false)]
        [TestCase(720f, 1280f, 0f, 0f, true)]
        [TestCase(1920f, 1080f, 0f, 0f, false)]
        [TestCase(1080f, 1920f, 0f, 0f, true)]
        [TestCase(1080f, 2400f, 110f, 60f, true)]
        [TestCase(2400f, 1080f, 0f, 0f, true)]
        [TestCase(1440f, 1080f, 0f, 0f, false)]
        public void SendOut_ClearsEveryBattleHudPiece(float width, float height, float notchTop, float gestureBottom, bool mobile)
        {
            HudFrame f = HudFrame.ForScreen(width, height, 0f, 0f, notchTop, gestureBottom, mobile);
            Rect safe = DuelHudLayout.Safe(f);
            Rect player = DuelHudLayout.HpCard(safe, true);
            Rect enemy = DuelHudLayout.HpCard(safe, false);
            // 상대 쪽 쌓기가 가장 긴 경우 — 장부 게이지 + 보정 칩 + 말풍선.
            var pieces = new List<KeyValuePair<string, Rect>>
            {
                Piece("내 HP 카드", player),
                Piece("상대 HP 카드", enemy),
                Piece("내 보정 칩", BattleHudStack.EnvironmentChip(player, true, false, player.width)),
                Piece("연속 배지", BattleHudStack.ComboBadge(player, true)),
                Piece("장부 게이지", BattleHudStack.LedgerGauge(enemy)),
                Piece("상대 보정 칩", BattleHudStack.EnvironmentChip(enemy, false, true, enemy.width)),
                Piece("대결 말풍선", BattleHudStack.Bubble(enemy, true, true)),
                Piece("머리 띠", DuelHudLayout.Heading(f)),
                Piece("배속 버튼", DuelHudLayout.SpeedControl(f)),
                Piece("턴 배너", DuelHudLayout.TurnBanner(f)),
                Piece("공격 배너", DuelHudLayout.PhaseBanner(f)),
                Piece("행동 문구", DuelHudLayout.ActionBar(f)),
                Piece("전투 문구 1줄", BattleEffectTextOverlay.RowRect(f, 0)),
                Piece("전투 문구 2줄", BattleEffectTextOverlay.RowRect(f, 1)),
                Piece("전투 문구 3줄", BattleEffectTextOverlay.RowRect(f, 2)),
            };

            foreach (bool withLine in new[] { false, true })
            {
                DuelTeamHud.SendOutPlan plan = DuelTeamHud.SendOut(f, withLine);
                Assert.AreEqual(withLine, plan.HasLine);
                var mine = new List<KeyValuePair<string, Rect>> { Piece("교체 제목", plan.Title) };
                if (withLine) mine.Add(Piece("교체 한마디", plan.Line));
                // 떠오르는 동안(아래로 SendOutRise만큼 내려간 자리)도 잰다.
                int count = mine.Count;
                for (int i = 0; i < count; i++)
                    mine.Add(Piece(mine[i].Key + "(떠오르는 중)", Shift(mine[i].Value, DuelTeamHud.SendOutRise)));

                foreach (KeyValuePair<string, Rect> m in mine)
                {
                    AssertContained(safe, m.Value, $"{width}x{height} {m.Key}");
                    foreach (KeyValuePair<string, Rect> p in pieces)
                        Assert.IsFalse(m.Value.Overlaps(p.Value), $"{width}x{height} 한마디{withLine}: {m.Key} {m.Value} ↔ {p.Key} {p.Value}");
                }
                if (withLine) Assert.IsFalse(plan.Title.Overlaps(plan.Line), "제목과 한마디가 겹친다");
                Assert.GreaterOrEqual(plan.Title.height - 20f, StoryRecapLayout.LineHeight(DuelTeamHud.SendOutFont), "큰 글자 한 줄이 안 들어간다");
                Assert.GreaterOrEqual(plan.Title.width, Mathf.Min(DuelTeamHud.SendOutTitleMaxWidth, f.ContentWidth) - 0.01f);
            }
        }

        // ── 레이드 — 변신 문구·수문장 배너는 아래쪽, 보스(위쪽)와 팀 줄을 비킨다 ──

        [TestCase(1280f, 720f, 0f, 0f, false)]
        [TestCase(720f, 1280f, 0f, 0f, true)]
        [TestCase(1920f, 1080f, 0f, 0f, false)]
        [TestCase(1080f, 1920f, 0f, 0f, true)]
        [TestCase(1080f, 2400f, 110f, 60f, true)]
        [TestCase(2400f, 1080f, 0f, 0f, true)]
        [TestCase(1440f, 1080f, 0f, 0f, false)]
        public void RaidTransformBanner_SitsOnTheLowerStage(float width, float height, float notchTop, float gestureBottom, bool mobile)
        {
            HudFrame f = HudFrame.ForScreen(width, height, 0f, 0f, notchTop, gestureBottom, mobile);
            Rect safe = DuelHudLayout.Safe(f);
            Rect banner = RaidStageLayout.TransformBanner(f);
            AssertContained(safe, banner, $"{width}x{height} 변신 문구");
            AssertContained(safe, Shift(banner, RaidStageLayout.BannerRise), $"{width}x{height} 변신 문구(떠오르는 중)");
            Assert.AreEqual(RaidStageLayout.TransformBannerHeight, banner.height, 0.01f, "아래 무대가 판보다 좁다");
            Assert.GreaterOrEqual(banner.height - 20f, RaidStageLayout.LineHeight(RaidStageLayout.TransformFont));
            foreach (KeyValuePair<string, Rect> p in RaidPieces(f))
            {
                Assert.IsFalse(banner.Overlaps(p.Value), $"{width}x{height}: 변신 문구 {banner} ↔ {p.Key} {p.Value}");
                Assert.IsFalse(Shift(banner, RaidStageLayout.BannerRise).Overlaps(p.Value), $"{width}x{height}: 떠오르는 변신 문구 ↔ {p.Key}");
            }
            Assert.Greater(banner.y, f.Height * 0.5f, "변신 문구가 위쪽(보스 자리)에 섰다");
        }

        [TestCase(1280f, 720f, 0f, 0f, false)]
        [TestCase(720f, 1280f, 0f, 0f, true)]
        [TestCase(1920f, 1080f, 0f, 0f, false)]
        [TestCase(1080f, 1920f, 0f, 0f, true)]
        [TestCase(1080f, 2400f, 110f, 60f, true)]
        [TestCase(2400f, 1080f, 0f, 0f, true)]
        [TestCase(1440f, 1080f, 0f, 0f, false)]
        public void GuardianBanner_LeavesTheUpperTwoThirdsToTheBoss(float width, float height, float notchTop, float gestureBottom, bool mobile)
        {
            HudFrame f = HudFrame.ForScreen(width, height, 0f, 0f, notchTop, gestureBottom, mobile);
            Rect safe = DuelHudLayout.Safe(f);
            foreach (bool hasIntro in new[] { true, false })
            {
                RaidStageLayout.GuardianPlan plan = RaidStageLayout.GuardianBanner(f, hasIntro);
                string tag = $"{width}x{height} 별칭{hasIntro}";
                AssertContained(safe, plan.Card, tag + " 배너");
                Assert.GreaterOrEqual(plan.Card.y, f.Height * 2f / 3f - 0.01f, tag + ": 보스 자리(위 2/3)를 덮는다");
                // 아레나의 수문장 샷은 안전 영역 아래에서 GuardianWindowBottom 위로 수문장을 담는다(visual-dev) — 배너는 그 밑에 선다.
                float safeBottom = f.Height - f.SafeBottom;
                float windowBottom = safeBottom - (safeBottom - f.SafeTop) * BattleStaging.GuardianWindowBottom;
                Assert.GreaterOrEqual(plan.Card.y, windowBottom - 0.01f, tag + ": 아레나의 수문장 창을 덮는다");
                // 인트로 동안 서 있는 것 — 보스 카드와 쉬는 자리의 팀 줄.
                Assert.IsFalse(plan.Card.Overlaps(RaidStageLayout.BossCard(f)), tag + ": 보스 HP 카드");
                Assert.IsFalse(plan.Card.Overlaps(RaidStageLayout.TeamStripRest(f)), tag + ": 팀 줄");

                var rows = new List<KeyValuePair<string, Rect>> { Piece("칭호", plan.Title), Piece("이름 줄", plan.NameRow) };
                if (hasIntro)
                {
                    rows.Add(Piece("별칭", plan.Epithet));
                    rows.Add(Piece("등장 줄", plan.Line));
                }
                for (int i = 0; i < rows.Count; i++)
                {
                    AssertContained(plan.Card, rows[i].Value, tag + " " + rows[i].Key);
                    for (int j = 0; j < i; j++)
                        Assert.IsFalse(rows[i].Value.Overlaps(rows[j].Value), $"{tag}: {rows[i].Key} ↔ {rows[j].Key}");
                }
                Assert.GreaterOrEqual(plan.Title.height, RaidStageLayout.LineHeight(RaidStageLayout.GuardianTitleFont));
                if (hasIntro)
                {
                    Assert.GreaterOrEqual(plan.Epithet.height, RaidStageLayout.LineHeight(RaidStageLayout.GuardianEpithetFont));
                    Assert.GreaterOrEqual(plan.Line.height, RaidStageLayout.LineHeight(RaidStageLayout.GuardianLineFont));
                }
                // 떠오르는 동안(아래로 밀린 자리)도 화면 밖으로 나가지 않는다.
                Assert.LessOrEqual(plan.Card.yMax + RaidStageLayout.GuardianSlide, f.Height + 0.01f, tag + ": 슬라이드가 화면 밖");
            }
            Assert.Greater(RaidStageLayout.GuardianHeight(true), RaidStageLayout.GuardianHeight(false));
        }

        [Test]
        public void EpithetPunch_StartsBigAndSettlesToOne()
        {
            Assert.AreEqual(RaidStageLayout.EpithetPunchPeak, RaidStageLayout.EpithetPunch(0f), 0.0001f);
            Assert.AreEqual(1f, RaidStageLayout.EpithetPunch(RaidStageLayout.EpithetPunchSeconds), 0.0001f);
            Assert.AreEqual(1f, RaidStageLayout.EpithetPunch(5f), 0.0001f);
            float last = RaidStageLayout.EpithetPunchPeak;
            for (float s = 0.02f; s <= RaidStageLayout.EpithetPunchSeconds; s += 0.02f)
            {
                float v = RaidStageLayout.EpithetPunch(s);
                Assert.LessOrEqual(v, last + 0.0001f, "도로 커진다");
                last = v;
            }
            // 찍히는 순간이 인트로 안이다 — 별칭이 보일 틈이 있어야 한다.
            Assert.Less(BattleStaging.GuardianRoarAt + RaidStageLayout.EpithetPunchSeconds, BattleStaging.GuardianIntroSeconds - 0.5f);
        }

        [Test]
        public void GuardianNameRow_CentersNameAndChips_AndShrinksOnlyTheName()
        {
            Rect row = new Rect(100f, 500f, 900f, 52f);
            RaidStageLayout.NameRow wide = RaidStageLayout.LayoutNameRow(row, 300f, 76f, 76f);
            Assert.AreEqual(300f, wide.Name.width, 0.01f);
            Assert.IsFalse(wide.Name.Overlaps(wide.Chip1));
            Assert.IsFalse(wide.Chip1.Overlaps(wide.Chip2));
            float left = wide.Name.x - row.x, right = row.xMax - wide.Chip2.xMax;
            Assert.AreEqual(left, right, 0.5f, "묶음이 가운데가 아니다");
            AssertContained(row, wide.Chip2, "둘째 칩");

            RaidStageLayout.NameRow narrow = RaidStageLayout.LayoutNameRow(row, 2000f, 76f, 0f);
            Assert.AreEqual(76f, narrow.Chip1.width, 0.01f, "칩은 줄이지 않는다");
            Assert.AreEqual(0f, narrow.Chip2.width, 0.01f);
            AssertContained(row, narrow.Name, "이름");
            AssertContained(row, narrow.Chip1, "칩");
            Assert.IsFalse(narrow.Name.Overlaps(narrow.Chip1));
            Assert.AreEqual(RaidStageLayout.GuardianChipHeight, narrow.Chip1.height, 0.01f);
            Assert.AreEqual(RaidStageLayout.ChipWidth("벌레"), 2 * RaidStageLayout.GuardianChipCharWidth + RaidStageLayout.GuardianChipPadding, 0.01f);
        }

        // ── 보스 이름 아래 「○○의 모습」 ──

        [Test]
        public void BossFormCaption_RowsStayInsideTheCardAboveTheHpBar()
        {
            HudFrame f = HudFrame.ForScreen(1920f, 1080f, 0f, 0f, 0f, 0f, false);
            Rect card = RaidStageLayout.BossCard(f);
            Rect caption = RaidStageLayout.BossFormCaptionRow(card);
            Rect stats = RaidStageLayout.BossStatRow(card);
            AssertContained(card, caption, "모습");
            AssertContained(card, stats, "ATK·DEF");
            Assert.IsFalse(caption.Overlaps(stats));
            Assert.LessOrEqual(caption.yMax, card.y + 62f, "HP 막대(y+62)를 밟는다");
            Assert.GreaterOrEqual(caption.y, card.y + 32f, "이름 줄(y+8~40)을 밟는다");
            Assert.AreEqual("호랑나비의 모습", RaidStageLayout.FormCaptionText("호랑나비"));
            Assert.IsNull(RaidStageLayout.FormCaptionText(""));
        }

        [Test]
        public void ShownForm_UntilTheArenaSwapsTheModel_KeepsTheOldForm()
        {
            InsectData mantis = ScriptableObject.CreateInstance<InsectData>();
            InsectData butterfly = ScriptableObject.CreateInstance<InsectData>();
            InsectData firefly = ScriptableObject.CreateInstance<InsectData>();
            try
            {
                var first = new RaidBossFormChange { FromIndex = 0, ToIndex = 1, FromData = mantis, ToData = butterfly };
                var second = new RaidBossFormChange { FromIndex = 1, ToIndex = 2, FromData = butterfly, ToData = firefly };

                // 원래 모습 — 글자 없음.
                Assert.IsNull(RaidStageLayout.ShownForm(0, mantis, RaidStageLayout.UnrevealedChange(false, null, null, 0f)));
                // 사마귀 → 나비가 걸렸지만 연출 전 — 아직 원래 모습(글자 없음).
                Assert.IsNull(RaidStageLayout.ShownForm(1, butterfly, RaidStageLayout.UnrevealedChange(true, first, null, 0f)));
                // 변신 단계 — 아레나가 모델을 바꾸기 전(TransformSwap 전)엔 그대로, 바꾼 뒤엔 새 모습.
                float before = BattleStaging.TransformSwap - 0.01f, after = BattleStaging.TransformSwap;
                Assert.IsNull(RaidStageLayout.ShownForm(1, butterfly, RaidStageLayout.UnrevealedChange(false, null, first, before)));
                Assert.AreSame(butterfly, RaidStageLayout.ShownForm(1, butterfly, RaidStageLayout.UnrevealedChange(false, null, first, after)));
                // 단계가 끝나면(활성 변신 없음) 새 모습.
                Assert.AreSame(butterfly, RaidStageLayout.ShownForm(1, butterfly, RaidStageLayout.UnrevealedChange(false, null, null, 0f)));
                // 나비 → 반딧불이가 걸렸지만 연출 전 — 나비.
                Assert.AreSame(butterfly, RaidStageLayout.ShownForm(2, firefly, RaidStageLayout.UnrevealedChange(true, second, null, 0f)));
                Assert.AreSame(butterfly, RaidStageLayout.ShownForm(2, firefly, RaidStageLayout.UnrevealedChange(false, null, second, before)));
                Assert.AreSame(firefly, RaidStageLayout.ShownForm(2, firefly, RaidStageLayout.UnrevealedChange(false, null, second, after)));
            }
            finally
            {
                Object.DestroyImmediate(mantis);
                Object.DestroyImmediate(butterfly);
                Object.DestroyImmediate(firefly);
            }
        }

        [Test]
        public void RoarPunch_OnlyAroundTheRoar()
        {
            Assert.AreEqual(1f, RaidStageLayout.RoarPunch(-0.1f), 0.0001f, "울부짖기 전엔 그대로");
            Assert.AreEqual(RaidStageLayout.RoarPunchPeak, RaidStageLayout.RoarPunch(0f), 0.0001f);
            Assert.AreEqual(1f, RaidStageLayout.RoarPunch(RaidStageLayout.EpithetPunchSeconds), 0.0001f);
            Assert.Less(RaidStageLayout.RoarPunchPeak, RaidStageLayout.EpithetPunchPeak + 0.0001f);
            // 울부짖는 박자가 변신 단계 안이다.
            Assert.Less(BattleStaging.TransformRoar * RaidBattleUI.BossTransformDuration + RaidStageLayout.EpithetPunchSeconds,
                RaidBattleUI.BossTransformDuration);
        }

        // ── 대화창 「승부!」 ──

        [Test]
        public void DialogueDuelPrompt_OwnDuelOrPendingDuel_TurnsTheLastButtonIntoDuel()
        {
            Assert.IsTrue(DialogueDuelPrompt.LeadsToDuel("ledger_grip", false, false, false), "간부");
            Assert.IsTrue(DialogueDuelPrompt.LeadsToDuel(" catcher_rival ", false, false, false), "라온(앞뒤 공백)");
            Assert.IsTrue(DialogueDuelPrompt.LeadsToDuel(null, true, false, false), "선택 결과 대사 — 런처가 기다린다");
            Assert.IsFalse(DialogueDuelPrompt.LeadsToDuel(null, false, false, false));
            Assert.IsFalse(DialogueDuelPrompt.LeadsToDuel("no_such_npc", false, false, false), "대결 표에 없는 상대 — 런처도 버린다");
            Assert.IsFalse(DialogueDuelPrompt.LeadsToDuel("ledger_grip", true, true, false), "저널 다시보기는 대결을 열지 않는다");
            Assert.IsFalse(DialogueDuelPrompt.LeadsToDuel("ledger_grip", true, false, true), "꿈은 대결을 열지 않는다");
        }

        [Test]
        public void DialogueDuelPrompt_Labels()
        {
            Assert.AreEqual(DialogueDuelPrompt.NextLabel, DialogueDuelPrompt.AdvanceLabel(false, true, true), "덜 나왔으면 줄을 마저 보인다");
            Assert.AreEqual(DialogueDuelPrompt.DuelLabel, DialogueDuelPrompt.AdvanceLabel(true, true, true));
            Assert.AreEqual(DialogueDuelPrompt.CloseLabel, DialogueDuelPrompt.AdvanceLabel(true, true, false));
            Assert.AreEqual(DialogueDuelPrompt.NextLabel, DialogueDuelPrompt.AdvanceLabel(true, false, true));
            Assert.IsTrue(DialogueDuelPrompt.IsDuelButton(true, true, true));
            Assert.IsFalse(DialogueDuelPrompt.IsDuelButton(false, true, true));
            Assert.IsFalse(DialogueDuelPrompt.IsDuelButton(true, false, true));
            Assert.AreEqual("건너뛰고 승부", DialogueDuelPrompt.SkipButtonLabel(true));
            Assert.AreEqual("건너뛰기", DialogueDuelPrompt.SkipButtonLabel(false));
        }

        [Test]
        public void RealStory_EveryDuelAfterBeat_ShowsTheDuelButton()
        {
            int found = 0;
            foreach (StoryBeat beat in StoryService.AllBeats())
            {
                if (beat == null || string.IsNullOrWhiteSpace(beat.duelAfter)) continue;
                found++;
                Assert.IsTrue(DialogueDuelPrompt.LeadsToDuel(beat.duelAfter, false, false, false),
                    $"{beat.beatId}: duelAfter '{beat.duelAfter}'인데 「승부!」가 안 뜬다");
            }
            Assert.Greater(found, 0, "Story.json에 대사 직후 대결이 하나도 없다");
        }

        // ── 도우미 ──

        private static IEnumerable<KeyValuePair<string, Rect>> RaidPieces(HudFrame f)
        {
            yield return Piece("보스 HP 카드", RaidStageLayout.BossCard(f));
            yield return Piece("보스 예고", RaidStageLayout.BossIntent(f));
            yield return Piece("팀 줄(쉬는 자리)", RaidStageLayout.TeamStripRest(f));
            yield return Piece("합체 게이지", RaidStageLayout.UniteGauge(f));
        }

        private static KeyValuePair<string, Rect> Piece(string name, Rect rect) => new KeyValuePair<string, Rect>(name, rect);

        private static Rect Shift(Rect r, float dy) => new Rect(r.x, r.y + dy, r.width, r.height);

        private static void AssertContained(Rect outer, Rect inner, string what)
        {
            const float eps = 0.01f;
            Assert.GreaterOrEqual(inner.xMin, outer.xMin - eps, $"{what}: 왼쪽 밖 {inner} / {outer}");
            Assert.LessOrEqual(inner.xMax, outer.xMax + eps, $"{what}: 오른쪽 밖 {inner} / {outer}");
            Assert.GreaterOrEqual(inner.yMin, outer.yMin - eps, $"{what}: 위 밖 {inner} / {outer}");
            Assert.LessOrEqual(inner.yMax, outer.yMax + eps, $"{what}: 아래 밖 {inner} / {outer}");
        }
    }
}
#endif
