#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using InsectGame.Battle;
using InsectGame.NPC;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 4단계 「전투 체감」의 화면 — 진입 문구·화면 쓸기, 상성(타격 순간 표시·스킬 카드 칩), 전용기 컷인 띠, 승리 화면(보상 줄 시각·「눌러서 계속」),
    /// 레이드 팀 줄(내려갈 땐 곧바로·수문장 등장/변신/결과엔 숨김).
    ///
    /// 자리·문구·시각은 전부 화면 파일의 순수 함수(<see cref="BattleEntryText"/>·<see cref="BattleEntryStaging"/>·<see cref="BattleEntryLayout"/>·
    /// <see cref="MatchupHud"/>·<see cref="SignatureCutInLayout"/>·<see cref="BattleRewardLines"/>·<see cref="BattleVictoryLayout"/>·<see cref="RaidTeamStrip"/>)를
    /// 부른다 — 식을 여기 다시 세우지 않는다. IMGUI 그리기 자체는 QA 빌드(<c>-battleScenario duel·victory·signature·raid·guardian-intro·raid-forms</c>)로 본다.
    /// 화면은 가로 1280×720·세로 720×1280을 기준으로(가상 1920×1080·1080×1920) 노치·4:3·20:9까지 잰다.
    /// </summary>
    [TestFixture]
    public class BattleFeelHudTests
    {
        private static readonly object[] Screens =
        {
            new object[] { 1280f, 720f, 0f, 0f, false },
            new object[] { 720f, 1280f, 0f, 0f, true },
            new object[] { 1920f, 1080f, 0f, 0f, false },
            new object[] { 1080f, 1920f, 0f, 0f, true },
            new object[] { 1080f, 2400f, 110f, 60f, true },
            new object[] { 2400f, 1080f, 0f, 0f, true },
            new object[] { 1440f, 1080f, 0f, 0f, false },
        };

        // ── 진입 문구 ──

        [Test]
        public void EntryText_EveryKind_OneShortLine_WithTheRightParticle()
        {
            Assert.AreEqual("야생 사마귀가 나타났다!", BattleEntryText.For(BattleKind.Wild, "사마귀", null, null));
            Assert.AreEqual("야생 장수풍뎅이가 나타났다!", BattleEntryText.For(BattleKind.Wild, "장수풍뎅이", null, null));
            Assert.AreEqual("야생 왕사슴벌레가 나타났다!", BattleEntryText.For(BattleKind.Wild, "왕사슴벌레", null, null));
            Assert.AreEqual("야생 모래 지네가 나타났다!", BattleEntryText.For(BattleKind.Wild, "모래 지네", null, null));
            Assert.AreEqual("야생 쇠똥구리 왕이 나타났다!", BattleEntryText.For(BattleKind.Wild, "쇠똥구리 왕", null, null));
            Assert.AreEqual("야생 Atlas이(가) 나타났다!", BattleEntryText.For(BattleKind.Wild, "Atlas", null, null), "모르면 둘 다 적는다");
            Assert.AreEqual("초원의 수문장 사마귀!", BattleEntryText.For(BattleKind.Guardian, "사마귀", null, "초원"));
            Assert.AreEqual("수문장 사마귀!", BattleEntryText.For(BattleKind.Guardian, "사마귀", null, null), "리전을 모르면 리전 없이");
            Assert.AreEqual("집게가 승부를 걸어왔다!", BattleEntryText.For(BattleKind.BossDuel, "지네", "집게", null));
            Assert.AreEqual("관장 하월이 승부를 걸어왔다!", BattleEntryText.For(BattleKind.BossDuel, "나방", "관장 하월", null));
            Assert.AreEqual("상대가 승부를 걸어왔다!", BattleEntryText.For(BattleKind.BossDuel, "나방", null, null));
            Assert.AreEqual("라온이 승부를 걸어왔다!", BattleEntryText.For(BattleKind.RivalDuel, "사슴벌레", null, null));
            Assert.AreEqual("라온이 승부를 걸어왔다!", BattleEntryText.For(BattleKind.RivalDuel, "사슴벌레", "라온", null));
            Assert.AreEqual("곤충잡이 아이가 승부를 걸어왔다!", BattleEntryText.For(BattleKind.KidDuel, "무당벌레", null, null));
            Assert.IsNull(BattleEntryText.For(BattleKind.Sandbox, "비천룡", null, null), "꿈 챔피언전은 꿈이 자기 카드를 띄운다");
            Assert.AreEqual("야생 곤충이 나타났다!", BattleEntryText.For(BattleKind.Wild, null, null, null));
            Assert.AreEqual("레이드 보스 왕사슴벌레 출현!", BattleEntryText.Raid("왕사슴벌레"));
            Assert.AreEqual("레이드 보스 곤충 출현!", BattleEntryText.Raid(""));
        }

        [Test]
        public void EntryText_RealDuelOpponents_GetAKnownParticle()
        {
            int checkedCount = 0;
            foreach (string id in DuelBanter.Ids)
            {
                Assert.IsTrue(DuelBanter.TryGet(id, out DuelBanter.Lines lines), id);
                bool rival = NpcRivalDuels.TryGetStage(id, out _);
                BattleKind kind = rival ? BattleKind.RivalDuel : BattleKind.BossDuel;
                string text = BattleEntryText.For(kind, "곤충", lines.name, null);
                StringAssert.EndsWith("승부를 걸어왔다!", text, id);
                Assert.IsFalse(text.Contains("("), $"{id}: 「{lines.name}」의 조사를 모른다 — {text}");
                if (rival) Assert.AreEqual("라온이 승부를 걸어왔다!", text, id);
                checkedCount++;
            }
            Assert.Greater(checkedCount, 5, "한마디 표가 비었다");
        }

        // ── 진입 시각표 ──

        [Test]
        public void EntryWipe_StartsCovered_OpensByItsEnd_AndOnlyMovesOneWay()
        {
            Assert.AreEqual(0f, BattleEntryStaging.WipeOpen(0f), 1e-5f);
            Assert.AreEqual(1f, BattleEntryStaging.WipeOpen(BattleEntryStaging.WipeEnd), 1e-5f);
            Assert.AreEqual(1f, BattleEntryStaging.WipeOpen(1f), 1e-5f);
            float last = -1f;
            for (float p = 0f; p <= 1f; p += 0.02f)
            {
                float open = BattleEntryStaging.WipeOpen(p);
                Assert.GreaterOrEqual(open, last - 1e-6f, "막이 되돌아온다");
                last = open;
            }
            // 쓸기는 아레나 진입 샷(1초) 안에 끝난다 — 샷이 구도에 내려앉는 걸 막이 가리지 않게.
            Assert.Less(BattleEntryStaging.WipeEnd * BattleReadPacing.EntryIntroSeconds, BattleFlourish.OpeningShotSeconds);
        }

        [TestCaseSource(nameof(Screens))]
        public void EntryWipe_EdgeGeometry_CoversThenClearsTheWholeScreen(float width, float height, float notchTop, float gestureBottom, bool mobile)
        {
            HudFrame f = HudFrame.ForScreen(width, height, 0f, 0f, notchTop, gestureBottom, mobile);
            float closed = BattleEntryStaging.WipeEdgeX(0f, f.Width, f.Height);
            // 열림 0 — 모서리(와 앞세운 띠)가 화면 맨 위에서도 왼쪽 밖이라 화면 전체가 막이다.
            Assert.LessOrEqual(BattleEntryStaging.EdgeXAt(closed, 0f, f.Height) + BattleEntryStaging.StripeWidth, 0f + 0.01f);
            Assert.LessOrEqual(BattleEntryStaging.EdgeXAt(closed, f.Height, f.Height), BattleEntryStaging.EdgeXAt(closed, 0f, f.Height),
                "모서리는 위가 오른쪽으로 기운다");
            float open = BattleEntryStaging.WipeEdgeX(1f, f.Width, f.Height);
            // 열림 1 — 모서리의 가장 왼쪽(화면 맨 아래)도 화면 오른쪽 밖이다.
            Assert.GreaterOrEqual(BattleEntryStaging.EdgeXAt(open, f.Height, f.Height), f.Width - 0.01f);
        }

        [Test]
        public void EntryTitle_ShowsInsideTheEntry_LeavesBeforeTheCutIn_OrHoldsToTheIntroEnd()
        {
            float introPlain = BattleReadPacing.IntroSeconds(0f, 2f);
            Assert.AreEqual(0f, BattleEntryStaging.TitleAlpha(0.1f, 0.14f, introPlain, false), "쓸기가 한창일 때는 아직");
            float full = BattleEntryStaging.TitleIn + BattleEntryStaging.TitleFade;
            Assert.AreEqual(1f, BattleEntryStaging.TitleAlpha(full, full * 1.4f, introPlain, false), 1e-4f);
            // 컷인 전투 — 진입 구간 끝(=컷인 시작)에는 사라졌다. 그 전에 다 보이는 순간이 있다.
            Assert.AreEqual(0f, BattleEntryStaging.TitleAlpha(1f, 1.4f, 4.6f, true), 1e-4f, "진입 문구가 컷인과 겹친다");
            Assert.Less(full, 1f - BattleEntryStaging.TitleFade, "컷인 전투에서 문구가 다 보이는 순간이 없다");
            Assert.AreEqual(1f, BattleEntryStaging.TitleAlpha(0.6f, 0.84f, 4.6f, true), 1e-4f);
            // 평범한 인트로 — 진입 구간 뒤에도 남았다가 인트로 끝에 사라진다.
            Assert.AreEqual(1f, BattleEntryStaging.TitleAlpha(1f, 1.6f, introPlain, false), 1e-4f);
            Assert.AreEqual(0f, BattleEntryStaging.TitleAlpha(1f, introPlain, introPlain, false), 1e-4f);
        }

        [Test]
        public void EntryTitlePop_StartsSmall_OvershootsALittle_SettlesToOne()
        {
            Assert.AreEqual(BattleEntryStaging.PopStart, BattleEntryStaging.Pop(0f, false), 1e-4f);
            float max = 0f;
            for (float s = 0f; s < 1f; s += 0.01f) max = Mathf.Max(max, BattleEntryStaging.Pop(s, false));
            Assert.LessOrEqual(max, BattleEntryStaging.PopPeak + 1e-4f);
            Assert.Greater(max, 1f, "튀어나오지 않는다");
            Assert.AreEqual(1f, BattleEntryStaging.Pop(BattleEntryStaging.PopSettleAt, false), 1e-4f);
            Assert.AreEqual(1f, BattleEntryStaging.Pop(0f, true), 1e-4f, "줄인 움직임이면 크기를 바꾸지 않는다");
            // 다 자리 잡는 시각이 진입 구간 안이다(1.4초 장면에서 「나타났다!」가 멈춰 읽힌다).
            Assert.Less(BattleEntryStaging.TitleIn * BattleReadPacing.EntryIntroSeconds + BattleEntryStaging.PopSettleAt,
                BattleReadPacing.EntryIntroSeconds);
        }

        // ── 진입 자리 ──

        [TestCaseSource(nameof(Screens))]
        public void DuelEntryTitle_UpperCenter_ClearsTheHpCardsAndTheirStack(float width, float height, float notchTop, float gestureBottom, bool mobile)
        {
            HudFrame f = HudFrame.ForScreen(width, height, 0f, 0f, notchTop, gestureBottom, mobile);
            Rect safe = DuelHudLayout.Safe(f);
            Rect title = BattleEntryLayout.DuelTitle(f);
            AssertContained(safe, title, $"{width}x{height} 진입 문구");
            Assert.GreaterOrEqual(title.height - 12f, RaidStageLayout.LineHeight(BattleEntryLayout.TitleFont), "큰 글자 한 줄이 안 들어간다");
            Assert.Less(title.center.y, f.Height * 0.4f, "위쪽 가운데가 아니다 — 아레나 진입 샷(가운데)을 가린다");
            Assert.AreEqual(f.Width * 0.5f, title.center.x, 1f, "가운데가 아니다");
            foreach (KeyValuePair<string, Rect> p in IntroPieces(f))
                Assert.IsFalse(title.Overlaps(p.Value), $"{width}x{height}: 진입 문구 {title} ↔ {p.Key} {p.Value}");
            // 튀어나올 때(최대 배율) 커진 판도 HP 카드를 덮지 않는다 — 가로 화면의 카드 사이 자리는 카드 높이 안에 있다.
            Rect grown = Grow(title, BattleEntryStaging.PopPeak);
            Rect player = DuelHudLayout.HpCard(safe, true), enemy = DuelHudLayout.HpCard(safe, false);
            Assert.IsFalse(grown.Overlaps(player) || grown.Overlaps(enemy), $"{width}x{height}: 튀어나온 진입 문구가 HP 카드에 닿는다");
            Assert.GreaterOrEqual(grown.x, safe.x - 0.01f, $"{width}x{height}: 튀어나온 진입 문구가 안전 영역 왼쪽 밖");
            Assert.LessOrEqual(grown.xMax, safe.xMax + 0.01f, $"{width}x{height}: 튀어나온 진입 문구가 안전 영역 오른쪽 밖");
        }

        [TestCaseSource(nameof(Screens))]
        public void RaidEntryTitle_OnTheLowerStage_ClearsTheBossAndTheTeam(float width, float height, float notchTop, float gestureBottom, bool mobile)
        {
            HudFrame f = HudFrame.ForScreen(width, height, 0f, 0f, notchTop, gestureBottom, mobile);
            Rect title = BattleEntryLayout.RaidTitle(f);
            AssertContained(RaidStageLayout.LowerStage(f), title, $"{width}x{height} 레이드 진입 문구");
            Assert.Greater(title.y, f.Height * 0.5f, "위쪽(보스 자리)에 섰다 — 옛 RAID BOSS(30%)가 보스와 겹쳤다");
            Assert.GreaterOrEqual(title.height, RaidStageLayout.LineHeight(BattleEntryLayout.RaidTitleFont)
                + RaidStageLayout.LineHeight(BattleEntryLayout.RaidSubFont) + 20f, "두 줄이 안 들어간다");
            Rect grown = Grow(title, BattleEntryStaging.PopPeak);
            foreach (KeyValuePair<string, Rect> p in RaidPieces(f))
                Assert.IsFalse(grown.Overlaps(p.Value), $"{width}x{height}: 레이드 진입 문구 ↔ {p.Key} {p.Value}");
            Assert.GreaterOrEqual(grown.x, f.ContentLeft - 0.01f, $"{width}x{height}: 튀어나온 레이드 진입 문구가 안전 영역 밖");
            Assert.LessOrEqual(grown.xMax, f.ContentRight + 0.01f, $"{width}x{height}: 튀어나온 레이드 진입 문구가 안전 영역 밖");
        }

        // ── 상성 ──

        [Test]
        public void Matchup_Words_Arrows_AndColors_PerGrade()
        {
            Assert.AreEqual("아주 잘 통했다!", MatchupHud.ImpactText(Matchup.Super));
            Assert.AreEqual("잘 통했다!", MatchupHud.ImpactText(Matchup.Good));
            Assert.AreEqual("별로 안 통했다…", MatchupHud.ImpactText(Matchup.Weak));
            Assert.AreEqual("거의 안 통했다…", MatchupHud.ImpactText(Matchup.Resisted));
            Assert.IsNull(MatchupHud.ImpactText(Matchup.Neutral), "보통이면 아무것도 띄우지 않는다");
            Assert.AreEqual("아주 잘 통해요", MatchupHud.ChipText(Matchup.Super));
            Assert.AreEqual("잘 통해요", MatchupHud.ChipText(Matchup.Good));
            Assert.AreEqual("안 통해요", MatchupHud.ChipText(Matchup.Weak));
            Assert.AreEqual("거의 안 통해요", MatchupHud.ChipText(Matchup.Resisted));
            Assert.IsNull(MatchupHud.ChipText(Matchup.Neutral));

            Assert.AreEqual(2, MatchupHud.ArrowCount(Matchup.Super));
            Assert.AreEqual(1, MatchupHud.ArrowCount(Matchup.Good));
            Assert.AreEqual(1, MatchupHud.ArrowCount(Matchup.Weak));
            Assert.AreEqual(2, MatchupHud.ArrowCount(Matchup.Resisted));
            Assert.AreEqual(0, MatchupHud.ArrowCount(Matchup.Neutral));
            Assert.IsTrue(MatchupHud.PointsUp(Matchup.Super) && MatchupHud.PointsUp(Matchup.Good));
            Assert.IsFalse(MatchupHud.PointsUp(Matchup.Weak) || MatchupHud.PointsUp(Matchup.Resisted));

            Assert.AreEqual(MatchupTone.Good, MatchupHud.Tone(Matchup.Super), "아주 잘 — 민트");
            Assert.AreEqual(MatchupTone.Good, MatchupHud.Tone(Matchup.Good), "잘 — 민트");
            Assert.AreEqual(MatchupTone.Bad, MatchupHud.Tone(Matchup.Weak), "안 — 코랄");
            Assert.AreEqual(MatchupTone.Faint, MatchupHud.Tone(Matchup.Resisted), "거의 안 — 회색");
            Assert.AreEqual(MatchupTone.None, MatchupHud.Tone(Matchup.Neutral));
        }

        [Test]
        public void Matchup_EveryChartPair_ShowsExactlyWhenTheGradeIsNotNeutral()
        {
            foreach (InsectGame.Data.InsectElement attack in System.Enum.GetValues(typeof(InsectGame.Data.InsectElement)))
            foreach (InsectGame.Data.InsectElement primary in System.Enum.GetValues(typeof(InsectGame.Data.InsectElement)))
            foreach (InsectGame.Data.InsectElement secondary in System.Enum.GetValues(typeof(InsectGame.Data.InsectElement)))
            {
                float e = InsectGame.Data.InsectTypeChart.GetEffectiveness(attack, primary, secondary);
                Matchup m = ElementMatchup.Describe(e);
                bool shows = m != Matchup.Neutral;
                Assert.AreEqual(shows, MatchupHud.ImpactText(m) != null, $"{attack}→{primary}/{secondary} ×{e}");
                Assert.AreEqual(shows, MatchupHud.ChipText(m) != null, $"{attack}→{primary}/{secondary} ×{e}");
                Assert.AreEqual(shows, MatchupHud.ArrowCount(m) > 0);
                if (shows) Assert.AreEqual(e > 1f, MatchupHud.PointsUp(m), $"{attack}→{primary}/{secondary} ×{e} 화살표 방향");
            }
        }

        [TestCase(38, false)]
        [TestCase(64, true)]
        [TestCase(96, true)]
        [TestCase(48, false)]
        public void MatchupCaption_SitsAboveTheNumber_AndAboveTheCritLine(int numberFont, bool crit)
        {
            const float numberCenterY = 500f;
            float cy = MatchupHud.CaptionCenterY(numberCenterY, numberFont, crit);
            float captionBottom = cy + MatchupHud.CaptionHeight * 0.5f;
            float numberTop = numberCenterY - numberFont * 0.55f;
            Assert.LessOrEqual(captionBottom, numberTop + 0.01f, "숫자를 덮는다");
            if (crit)
            {
                // 「치명타!」 줄 — 1대1 BattleScreenUI.CritCaptionCenterY(가운데 = 윗변 − 22, 높이 40)와 같은 어림.
                float critTop = numberTop - 22f - 20f;
                Assert.LessOrEqual(captionBottom, critTop + 0.01f, "「치명타!」를 덮는다");
            }
            Assert.AreEqual(cy, MatchupHud.CaptionCenterAbove(numberTop - (crit ? MatchupHud.CritRowHeight : 0f)), 1e-3f);
        }

        [Test]
        public void MatchupCaption_PopsThenSettles_AndStaysInsideTheSafeArea()
        {
            Assert.AreEqual(MatchupHud.PopPeak, MatchupHud.CaptionScale(0f, false), 1e-4f);
            Assert.AreEqual(1f, MatchupHud.CaptionScale(MatchupHud.PopSeconds, false), 1e-4f);
            Assert.AreEqual(1f, MatchupHud.CaptionScale(0f, true), 1e-4f);
            Assert.Greater(MatchupHud.CaptionWidth(Matchup.Super, 200f), MatchupHud.CaptionWidth(Matchup.Good, 200f), "화살표 둘이 하나보다 넓다");

            HudFrame f = HudFrame.ForScreen(720f, 1280f, 0f, 0f, 0f, 0f, true);
            float w = MatchupHud.CaptionWidth(Matchup.Resisted, 260f);
            float left = MatchupHud.ClampCenterX(0f, w * 0.5f, f) - w * 0.5f;
            float right = MatchupHud.ClampCenterX(f.Width, w * 0.5f, f) + w * 0.5f;
            Assert.GreaterOrEqual(left, f.ContentLeft - 0.01f, "화면 왼쪽 끝 곤충 위의 표시가 밖으로 나간다");
            Assert.LessOrEqual(right, f.ContentRight + 0.01f, "화면 오른쪽 끝 곤충 위의 표시가 밖으로 나간다");
            Assert.AreEqual(500f, MatchupHud.ClampCenterX(500f, w * 0.5f, f), 1e-3f, "가운데 근처는 그대로");
        }

        [TestCase(1280f, 720f, false)]
        [TestCase(720f, 1280f, true)]
        [TestCase(2400f, 1080f, true)]
        public void SkillCardChip_FitsUnderTheLastRow_InsideTheCard(float width, float height, bool mobile)
        {
            HudFrame f = HudFrame.ForScreen(width, height, 0f, 0f, 0f, 0f, mobile);
            Rect deck = DuelHudLayout.SkillDeck(f);
            for (int count = 1; count <= 4; count++)
            for (int i = 0; i < count; i++)
            {
                Rect card = DuelHudLayout.SkillCard(deck, f.Portrait, i, count);
                // BattleScreenUI.DrawSkillPanel과 같은 줄: 재사용 줄(y+162, 28) 아래 y+192.
                Rect row = new Rect(card.x + 14f, card.y + 192f, card.width - 28f, MatchupHud.ChipHeight);
                Assert.GreaterOrEqual(row.y, card.y + 162f + 28f, "재사용 줄과 겹친다");
                Assert.LessOrEqual(row.yMax, card.yMax - 2f, "카드 아래 테두리를 밟는다");
                float chip = MatchupHud.ChipWidth(Matchup.Resisted, 150f, row.width);
                Assert.LessOrEqual(chip, row.width + 0.01f);
            }
            Assert.GreaterOrEqual(MatchupHud.ChipHeight, MatchupHud.ChipFont * 1.35f, "칩 글자가 잘린다");
        }

        // ── 전용기 컷인 띠 ──

        [TestCaseSource(nameof(Screens))]
        public void SignatureBand_AcrossTheScreen_ClearsEveryBattleHudPiece(float width, float height, float notchTop, float gestureBottom, bool mobile)
        {
            HudFrame f = HudFrame.ForScreen(width, height, 0f, 0f, notchTop, gestureBottom, mobile);
            Rect safe = DuelHudLayout.Safe(f);
            foreach (bool mine in new[] { true, false })
            {
                SignatureCutInLayout.Plan plan = SignatureCutInLayout.For(f, mine);
                string tag = $"{width}x{height} {(mine ? "내" : "상대")} 전용기";
                Assert.AreEqual(0f, plan.Band.x, 0.01f, tag + ": 띠가 화면 왼쪽 끝부터가 아니다");
                Assert.AreEqual(f.Width, plan.Band.width, 0.01f, tag + ": 띠가 화면을 가로지르지 않는다");
                Assert.GreaterOrEqual(plan.Band.y, f.ContentTop - 0.01f, tag);
                Assert.LessOrEqual(plan.Band.yMax, f.ContentBottom + 0.01f, tag);
                foreach (KeyValuePair<string, Rect> row in new[]
                         { Piece("배지", plan.Badge), Piece("시전자", plan.Caster), Piece("기술 이름", plan.Skill) })
                {
                    AssertContained(plan.Band, row.Value, tag + " " + row.Key);
                    Assert.GreaterOrEqual(row.Value.x, safe.x - 0.01f, tag + " " + row.Key + ": 안전 영역 왼쪽 밖");
                    Assert.LessOrEqual(row.Value.xMax, safe.xMax + 0.01f, tag + " " + row.Key + ": 안전 영역 오른쪽 밖");
                }
                Assert.IsFalse(plan.Badge.Overlaps(plan.Caster), tag + ": 배지와 시전자 이름");
                Assert.IsFalse(plan.Skill.Overlaps(plan.Caster) || plan.Skill.Overlaps(plan.Badge), tag + ": 기술 이름이 윗줄과 겹친다");
                Assert.GreaterOrEqual(plan.Skill.height, RaidStageLayout.LineHeight(SignatureCutInLayout.SkillFont), tag + ": 큰 글자 한 줄이 안 들어간다");
                Assert.Less(mine ? plan.Badge.x : -plan.Badge.xMax, mine ? plan.Caster.x : -plan.Caster.xMax, tag + ": 배지가 들어오는 쪽에 있지 않다");
                foreach (KeyValuePair<string, Rect> p in AttackPieces(f))
                    Assert.IsFalse(plan.Band.Overlaps(p.Value), $"{tag}: 띠 {plan.Band} ↔ {p.Key} {p.Value}");
            }
        }

        [Test]
        public void SignatureBand_SlidesInFromTheCastersSide_ThenFadesAfterTheCutIn()
        {
            Assert.AreEqual(1f, SignatureCutInLayout.Slide(0f, false), 1e-4f, "첫 프레임은 화면 밖에서 시작한다");
            Assert.AreEqual(0f, SignatureCutInLayout.Slide(SignatureCutInLayout.SlideShare, false), 1e-4f);
            Assert.AreEqual(0f, SignatureCutInLayout.Slide(1f, false), 1e-4f);
            Assert.AreEqual(0f, SignatureCutInLayout.Slide(0f, true), 1e-4f, "줄인 움직임이면 제자리");
            float last = 2f;
            for (float p = 0f; p <= 1f; p += 0.02f)
            {
                float s = SignatureCutInLayout.Slide(p, false);
                Assert.LessOrEqual(s, last + 1e-5f, "도로 밀려난다");
                last = s;
            }
            Assert.Less(SignatureCutInLayout.Offset(1f, true, 1920f), 0f, "내 전용기는 왼쪽에서");
            Assert.Greater(SignatureCutInLayout.Offset(1f, false, 1920f), 0f, "상대 전용기는 오른쪽에서");
            Assert.AreEqual(1f, SignatureCutInLayout.Alpha(-0.2f), 1e-4f);
            Assert.AreEqual(0f, SignatureCutInLayout.Alpha(SignatureCutInLayout.TailSeconds), 1e-4f);
            // 컷인(아레나 0.5초)의 절반 넘게 제자리에서 읽힌다.
            Assert.Less(SignatureCutInLayout.SlideShare, 0.5f);
            Assert.Greater(BattleFlourish.SignatureCutInEnd, 0f);
        }

        // ── 승리 화면 ──

        [Test]
        public void RewardLines_Duel_CandyExpCoinItemThenTheInsect()
        {
            List<RewardLine> lines = BattleRewardLines.Duel(12, 34, 3, "꿀", 2, true, true, "사마귀");
            CollectionAssert.AreEqual(new[] { "캔디 +12", "경험치 +34", "코인 +3", "꿀 ×2", "사마귀를 잡았다!" }, Texts(lines));
            Assert.IsTrue(lines[4].Wide, "곤충 포획은 한 줄 통째로");
            Assert.AreEqual(RewardTone.Caught, lines[4].Tone);
            AssertNarrowFirst(lines);

            CollectionAssert.AreEqual(new[] { "캔디 +12", "경험치 +34", "사마귀는 달아났다" },
                Texts(BattleRewardLines.Duel(12, 34, 0, null, 0, true, false, "사마귀")), "0인 보상은 줄이 없다");
            CollectionAssert.AreEqual(new[] { "캔디 +5", "경험치 +9" },
                Texts(BattleRewardLines.Duel(5, 9, 0, "꿀", 0, false, false, "사마귀")), "대결은 포획 줄이 없다");
            CollectionAssert.AreEqual(new[] { "장수풍뎅이를 잡았다!" },
                Texts(BattleRewardLines.Duel(0, 0, 0, null, 0, true, true, "장수풍뎅이")));
            List<RewardLine> empty = BattleRewardLines.Duel(0, 0, 0, null, 0, false, false, null);
            Assert.AreEqual(1, empty.Count, "보상이 하나도 없어도 빈 판을 띄우지 않는다");
            Assert.AreEqual(RewardTone.Cheer, empty[0].Tone);

            // 야생 드랍 재료는 아이템 데이터베이스에 이름이 없다 — 아이에게 ID(mat_leaf)를 보이지 않는다.
            Assert.AreEqual(BattleRewardLines.MaterialName, BattleRewardLines.FallbackItemName("mat_leaf"));
            Assert.AreEqual("net_gold", BattleRewardLines.FallbackItemName("net_gold"));
            Assert.IsNull(BattleRewardLines.FallbackItemName(""));
            var wild = ScriptableObject.CreateInstance<InsectGame.Data.InsectData>();
            try
            {
                Assert.IsFalse(BattleRewardLines.FallbackItemName(wild.itemRewardId).Contains("_"), "야생 곤충의 기본 드랍이 ID 그대로 보인다");
            }
            finally
            {
                Object.DestroyImmediate(wild);
            }
        }

        [Test]
        public void RewardLines_Raid_RewardsBonusThenTheBoss()
        {
            List<RewardLine> lines = BattleRewardLines.Raid(30, 60, "왕사슴벌레");
            CollectionAssert.AreEqual(new[] { "캔디 +30", "경험치 +60", "레이드 보너스 ×3", "왕사슴벌레를 잡았다!" }, Texts(lines));
            Assert.IsTrue(lines[3].Wide);
            AssertNarrowFirst(lines);
            Assert.AreEqual(BattleRewardLines.DreamCheer, BattleRewardLines.Cheer(null)[0].Text);
        }

        [TestCaseSource(nameof(Screens))]
        public void VictoryScreen_TitleOnTop_RewardsAtTheBottom_TheMiddleStaysOpen(float width, float height, float notchTop, float gestureBottom, bool mobile)
        {
            HudFrame f = HudFrame.ForScreen(width, height, 0f, 0f, notchTop, gestureBottom, mobile);
            Rect safe = DuelHudLayout.Safe(f);
            Rect title = BattleVictoryLayout.TitleRect(f);
            Rect quote = BattleVictoryLayout.QuoteRect(f);
            Rect hint = BattleVictoryLayout.HintRect(f);
            Assert.GreaterOrEqual(title.height - 12f, RaidStageLayout.LineHeight(BattleVictoryLayout.TitleFont), "「승리!」 한 줄이 안 들어간다");
            Assert.GreaterOrEqual(hint.height - 8f, RaidStageLayout.LineHeight(BattleVictoryLayout.HintFont));
            Assert.LessOrEqual(quote.yMax, f.Height * 0.35f + 0.01f, $"{width}x{height}: 위쪽 묶음이 가운데로 내려온다");

            foreach (List<RewardLine> lines in SampleLines())
            {
                BattleVictoryLayout.Grid grid = BattleVictoryLayout.Rewards(f, lines);
                string tag = $"{width}x{height} 보상 {lines.Count}줄";
                var pieces = new List<KeyValuePair<string, Rect>>
                {
                    Piece("승리", title), Piece("튀어나온 승리", Grow(title, BattleVictoryLayout.TitlePopPeak)),
                    Piece("한마디", quote), Piece("눌러서 계속", hint), Piece("보상 판", grid.Panel)
                };
                foreach (KeyValuePair<string, Rect> p in pieces)
                    if (p.Key != "튀어나온 승리") AssertContained(safe, p.Value, tag + " " + p.Key);
                for (int i = 0; i < pieces.Count; i++)
                for (int j = i + 1; j < pieces.Count; j++)
                {
                    if ((pieces[i].Key == "승리" && pieces[j].Key == "튀어나온 승리")) continue;
                    Assert.IsFalse(pieces[i].Value.Overlaps(pieces[j].Value), $"{tag}: {pieces[i].Key} {pieces[i].Value} ↔ {pieces[j].Key} {pieces[j].Value}");
                }
                // 가운데(아레나 승리 연출 — 1대1 포즈·레이드 팀 점프)는 비운다.
                Assert.GreaterOrEqual(grid.Panel.y, f.Height * 0.6f - 0.01f, $"{tag}: 보상 판이 가운데로 올라온다");

                var cells = new List<Rect>();
                for (int i = 0; i < lines.Count; i++)
                {
                    Rect cell = BattleVictoryLayout.Cell(grid, i, lines);
                    AssertContained(grid.Panel, cell, $"{tag} {i}번째 줄");
                    AssertContained(grid.Panel, Shift(cell, BattleVictoryLayout.RewardRise), $"{tag} {i}번째 줄(올라오는 중)");
                    Assert.GreaterOrEqual(cell.height, RaidStageLayout.LineHeight(BattleVictoryLayout.WideRowFont), tag);
                    foreach (Rect other in cells) Assert.IsFalse(cell.Overlaps(other), $"{tag}: {i}번째 줄이 앞 줄과 겹친다");
                    if (lines[i].Wide) Assert.AreEqual(grid.Panel.width - BattleVictoryLayout.PanelPad * 2f, cell.width, 0.01f, tag + " 넓은 줄");
                    if (i > 0) Assert.GreaterOrEqual(cell.y + 0.01f, cells[cells.Count - 1].y, $"{tag}: 줄 순서가 위로 거슬러 간다");
                    cells.Add(cell);
                }
            }
        }

        [Test]
        public void VictoryTiming_TitlePops_RewardsOneByOneFromPointEight_HintOnlyWhenItCanClose()
        {
            Assert.AreEqual(BattleVictoryLayout.TitlePopStart, BattleVictoryLayout.TitleScale(0f, false), 1e-4f);
            Assert.AreEqual(1f, BattleVictoryLayout.TitleScale(BattleVictoryLayout.TitlePopSettleAt, false), 1e-4f);
            Assert.AreEqual(1f, BattleVictoryLayout.TitleScale(0f, true), 1e-4f);
            float max = 0f;
            for (float s = 0f; s < 1f; s += 0.01f) max = Mathf.Max(max, BattleVictoryLayout.TitleScale(s, false));
            Assert.Greater(max, 1f, "「승리!」가 튀어나오지 않는다");
            Assert.LessOrEqual(max, BattleVictoryLayout.TitlePopPeak + 1e-4f);

            Assert.AreEqual(0.8f, BattleVictoryLayout.RewardAppearAt(0), 1e-4f);
            Assert.AreEqual(0f, BattleVictoryLayout.RewardAlpha(0.79f, 0), 1e-4f, "0.8초 전에 보상이 뜬다");
            Assert.AreEqual(1f, BattleVictoryLayout.RewardAlpha(0.8f + BattleVictoryLayout.RewardFade, 0), 1e-4f);
            for (int i = 1; i < 6; i++)
            {
                Assert.Greater(BattleVictoryLayout.RewardAppearAt(i), BattleVictoryLayout.RewardAppearAt(i - 1) + BattleVictoryLayout.RewardFade - 1e-4f,
                    "앞 줄이 다 떠오르기 전에 다음 줄이 뜬다 — 한 줄씩이 아니다");
            }
            // 첫 줄은 「승리!」가 자리 잡은 뒤, 아레나 승리 연출(2초) 도중에 뜬다.
            Assert.Greater(BattleVictoryLayout.RewardAppearAt(0), BattleVictoryLayout.TitlePopSettleAt);
            Assert.Less(BattleVictoryLayout.RewardAppearAt(0), BattleFlourish.VictorySeconds);

            // 「눌러서 계속」 — 결과 화면이 누름을 받을 때(0.6초)부터만.
            float lockEnd = BattleResultRules.InputLockSeconds;
            Assert.AreEqual(0f, BattleVictoryLayout.HintAlpha(BattleResultRules.CanClose(lockEnd - 0.01f), -0.01f, false), 1e-4f);
            Assert.IsTrue(BattleResultRules.CanClose(lockEnd));
            Assert.AreEqual(0f, BattleVictoryLayout.HintAlpha(true, 0f, false), 1e-4f, "닫을 수 있게 된 순간부터 떠오른다");
            for (float s = BattleVictoryLayout.HintFadeSeconds; s < 4f; s += 0.05f)
            {
                float a = BattleVictoryLayout.HintAlpha(true, s, false);
                Assert.GreaterOrEqual(a, 0.55f - 1e-4f, "깜빡이다 안 보일 만큼 옅어진다");
                Assert.LessOrEqual(a, 1f + 1e-4f);
            }
            Assert.AreEqual(1f, BattleVictoryLayout.HintAlpha(true, 2f, true), 1e-4f, "줄인 움직임이면 깜빡이지 않는다");
        }

        [Test]
        public void ResultScreens_NoLongerPromiseAnAutomaticReturn()
        {
            // 결과 화면은 이제 눌러야 닫힌다(BattleResultRules — 꿈 챔피언전만 예외). 예전 안내문이 남으면 거짓말이 된다.
            foreach (string file in new[] { "UI/BattleScreenUI.cs", "UI/RaidBattleUI.Draw.cs", "UI/BattleFeelHud.cs" })
            {
                string path = Path.Combine(Application.dataPath, "Scripts", file);
                Assert.IsTrue(File.Exists(path), path);
                string source = File.ReadAllText(path);
                StringAssert.DoesNotContain("잠시 후 탐험으로 돌아갑니다", source, file);
                StringAssert.DoesNotContain("잠시 후 자동으로 돌아갑니다", source, file);
            }
            Assert.AreEqual("눌러서 계속", BattleVictoryLayout.ContinueHint);
        }

        // ── 레이드 팀 줄 ──

        [Test]
        public void RaidTeamStrip_DropsAtOnce_RisesSmoothly()
        {
            Assert.AreEqual(1f, RaidTeamStrip.NextDrop(0f, true, 1f / 60f), 1e-4f, "기술을 고른 첫 프레임에 곧바로 아래 — 화면 가운데를 지나지 않는다");
            Assert.AreEqual(1f, RaidTeamStrip.NextDrop(0.3f, true, 0f), 1e-4f);
            float half = RaidTeamStrip.NextDrop(1f, false, RaidTeamStrip.SlideSeconds * 0.5f);
            Assert.AreEqual(0.5f, half, 1e-3f, "올라올 때는 미끄러진다");
            Assert.AreEqual(0f, RaidTeamStrip.NextDrop(1f, false, RaidTeamStrip.SlideSeconds), 1e-4f);
        }

        [Test]
        public void RaidTeamStrip_HidesForGuardianIntroTransformAndResult()
        {
            Assert.IsTrue(RaidTeamStrip.Hidden(true, false, false), "수문장 등장 — 보스 아랫부분을 가렸다");
            Assert.IsTrue(RaidTeamStrip.Hidden(false, true, false), "그림자 변신 — 연기를 가렸다");
            Assert.IsTrue(RaidTeamStrip.Hidden(false, false, true), "결과 — 팀 점프를 가린다");
            Assert.IsFalse(RaidTeamStrip.Hidden(false, false, false));
            Assert.AreEqual(1f, RaidTeamStrip.NextHide(0f, true, true, 0f), 1e-4f, "전투 첫 장면은 곧바로");
            Assert.AreEqual(0.5f, RaidTeamStrip.NextHide(0f, true, false, RaidTeamStrip.FadeSeconds * 0.5f), 1e-3f);
            Assert.AreEqual(0f, RaidTeamStrip.NextHide(1f, false, false, RaidTeamStrip.FadeSeconds), 1e-4f);
        }

        // ── 도우미 ──

        private static IEnumerable<List<RewardLine>> SampleLines()
        {
            yield return BattleRewardLines.Cheer(null);
            yield return BattleRewardLines.Duel(12, 34, 0, null, 0, false, false, null);
            yield return BattleRewardLines.Duel(12, 34, 0, null, 0, true, true, "사마귀");
            yield return BattleRewardLines.Raid(30, 60, "왕사슴벌레");
            yield return BattleRewardLines.Duel(12, 34, 3, "꿀", 2, true, false, "아주 긴 이름의 전설 장수풍뎅이");
            yield return BattleRewardLines.Duel(12, 34, 3, "꿀", 2, false, false, null);
        }

        /// <summary>진입 구간에 함께 서는 것 — 머리 띠, HP 카드와 그 밑 쌓기(장부 게이지·보정 칩이 다 선 가장 긴 경우).</summary>
        private static IEnumerable<KeyValuePair<string, Rect>> IntroPieces(HudFrame f)
        {
            Rect safe = DuelHudLayout.Safe(f);
            Rect player = DuelHudLayout.HpCard(safe, true);
            Rect enemy = DuelHudLayout.HpCard(safe, false);
            yield return Piece("머리 띠", DuelHudLayout.Heading(f));
            yield return Piece("내 HP 카드", player);
            yield return Piece("상대 HP 카드", enemy);
            yield return Piece("내 보정 칩", BattleHudStack.EnvironmentChip(player, true, false, player.width));
            yield return Piece("장부 게이지", BattleHudStack.LedgerGauge(enemy));
            yield return Piece("상대 보정 칩", BattleHudStack.EnvironmentChip(enemy, false, true, enemy.width));
        }

        /// <summary>공격 연출 동안 함께 서는 것 — HP 쌓기(말풍선·연속 배지까지), 위쪽 띠·배속, 아래쪽 공격 배너·행동 문구, 전투 문구 줄.</summary>
        private static IEnumerable<KeyValuePair<string, Rect>> AttackPieces(HudFrame f)
        {
            foreach (KeyValuePair<string, Rect> p in IntroPieces(f)) yield return p;
            Rect safe = DuelHudLayout.Safe(f);
            Rect player = DuelHudLayout.HpCard(safe, true);
            Rect enemy = DuelHudLayout.HpCard(safe, false);
            yield return Piece("대결 말풍선", BattleHudStack.Bubble(enemy, true, true));
            yield return Piece("연속 배지", Grow(BattleHudStack.ComboBadge(player, true), BattleHudStack.ComboPulsePeak));
            yield return Piece("배속 버튼", DuelHudLayout.SpeedControl(f));
            yield return Piece("공격 배너", DuelHudLayout.PhaseBanner(f));
            yield return Piece("행동 문구", DuelHudLayout.ActionBar(f));
            yield return Piece("턴 배너", DuelHudLayout.TurnBanner(f));
            for (int i = 0; i < 3; i++) yield return Piece($"전투 문구 {i + 1}줄", BattleEffectTextOverlay.RowRect(f, i));
        }

        private static IEnumerable<KeyValuePair<string, Rect>> RaidPieces(HudFrame f)
        {
            yield return Piece("보스 HP 카드", RaidStageLayout.BossCard(f));
            yield return Piece("보스 예고", RaidStageLayout.BossIntent(f));
            yield return Piece("팀 줄(쉬는 자리)", RaidStageLayout.TeamStripRest(f));
            yield return Piece("합체 게이지", RaidStageLayout.UniteGauge(f));
        }

        private static void AssertNarrowFirst(List<RewardLine> lines)
        {
            bool sawWide = false;
            foreach (RewardLine line in lines)
            {
                if (line.Wide) sawWide = true;
                else Assert.IsFalse(sawWide, "좁은 줄이 넓은 줄 뒤에 왔다 — 보상 판 칸 배치가 이 순서를 믿는다");
            }
        }

        private static List<string> Texts(List<RewardLine> lines)
        {
            var texts = new List<string>(lines.Count);
            foreach (RewardLine line in lines) texts.Add(line.Text);
            return texts;
        }

        private static KeyValuePair<string, Rect> Piece(string name, Rect rect) => new KeyValuePair<string, Rect>(name, rect);

        private static Rect Shift(Rect r, float dy) => new Rect(r.x, r.y + dy, r.width, r.height);

        /// <summary>가운데를 축으로 <paramref name="scale"/>배 — 행렬로 키우는 그리기(튀어나오기)의 겉모습.</summary>
        private static Rect Grow(Rect r, float scale)
        {
            float w = r.width * scale, h = r.height * scale;
            return new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h);
        }

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
