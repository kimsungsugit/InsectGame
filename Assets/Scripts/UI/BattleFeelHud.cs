using System.Collections.Generic;
using InsectGame.Battle;
using UnityEngine;

namespace InsectGame.UI
{
    // 4단계 「전투 체감」의 화면 — 진입 문구·화면 쓸기, 타격 순간의 상성 표시·스킬 카드 상성 칩, 전용기 컷인 띠, 승리 화면.
    // 이 파일은 둘로 나뉜다: 위쪽은 순수 계산(문구·시각표·자리 — BattleFeelHudTests가 씬 없이 본다), 맨 아래 BattleFeelDraw는
    // 1대1(BattleScreenUI.Feel)과 레이드(RaidBattleUI.Feel)가 함께 쓰는 IMGUI 그리기다. 대상은 초등 고학년 — 글자는 크게, 문장은 짧게.

    /// <summary>
    /// 전투 진입 큰 문구 — <b>순수</b>. 종류(<see cref="BattleKind"/>)마다 한 줄. 조사는 받침으로 고른다(<see cref="KoreanJosa"/>).
    /// 꿈 챔피언전(<see cref="BattleKind.Sandbox"/>)은 꿈이 자기 카드(「챔피언 결정전」)를 먼저 띄우므로 null(그리지 않는다).
    /// </summary>
    public static class BattleEntryText
    {
        /// <summary>라온 대결 — 한마디 표에 이름이 없을 때.</summary>
        public const string RivalName = "라온";

        /// <summary>곤충잡이 아이 대결 — 아이는 이름 표가 없다(<c>DuelBanter</c>는 간부·라온만 든다).</summary>
        public const string KidName = "곤충잡이 아이";

        /// <summary>일반 레이드 진입의 둘째 줄.</summary>
        public const string RaidSubLine = "모두 힘을 합쳐 쓰러뜨리자!";

        /// <param name="insect">상대 곤충 이름.</param>
        /// <param name="person">상대 사람 이름(간부·라온 — 한마디 표의 <c>name</c>). 없으면 빈 값.</param>
        /// <param name="regionName">수문장 리전 이름(「초원」). 없으면 빈 값.</param>
        public static string For(BattleKind kind, string insect, string person, string regionName)
        {
            string bug = string.IsNullOrEmpty(insect) ? "곤충" : insect;
            switch (kind)
            {
                case BattleKind.Wild:
                    return $"야생 {bug}{KoreanJosa.IGa(bug)} 나타났다!";
                case BattleKind.Guardian:
                    return string.IsNullOrEmpty(regionName) ? $"수문장 {bug}!" : $"{regionName}의 수문장 {bug}!";
                case BattleKind.KidDuel:
                    return Challenge(KidName);
                case BattleKind.RivalDuel:
                    return Challenge(string.IsNullOrEmpty(person) ? RivalName : person);
                case BattleKind.BossDuel:
                    return Challenge(string.IsNullOrEmpty(person) ? "상대" : person);
                default:
                    return null;
            }
        }

        /// <summary>「집게가 승부를 걸어왔다!」.</summary>
        public static string Challenge(string who)
        {
            string w = string.IsNullOrEmpty(who) ? "상대" : who;
            return $"{w}{KoreanJosa.IGa(w)} 승부를 걸어왔다!";
        }

        /// <summary>일반 레이드(수문장 아님) — 「레이드 보스 왕사슴벌레 출현!」.</summary>
        public static string Raid(string boss)
        {
            string b = string.IsNullOrEmpty(boss) ? "곤충" : boss;
            return $"레이드 보스 {b} 출현!";
        }
    }

    /// <summary>
    /// 전투 진입 구간의 시각표 — <b>순수</b>. 1대1은 <c>BattleScreenUI.EntryIntroProgress</c>(실제 1.4초, 0~1)를 넘긴다.
    /// ① 0 ~ <see cref="WipeEnd"/>: 화면을 덮은 어두운 막이 기울어진 모서리(호박색 띠)를 앞세워 오른쪽으로 쓸려 나가며 아레나가 열린다.
    /// ② <see cref="TitleIn"/>부터: 큰 문구가 살짝 튀어나왔다 자리 잡는다. 컷인이 이어지는 전투(간부·라온·수문장)는 진입 구간 끝에
    /// 사라지고(컷인은 그 뒤에 시작한다 — <c>BattleReadPacing.IntroSeconds</c>), 아니면 인트로 끝 0.25초에 사라진다.
    /// </summary>
    public static class BattleEntryStaging
    {
        /// <summary>화면 쓸기가 끝나는 진입 진행률.</summary>
        public const float WipeEnd = 0.35f;
        /// <summary>막의 모서리가 기운 각(도) — 위가 오른쪽으로 기운다(「/」).</summary>
        public const float WipeAngle = 16f;
        /// <summary>모서리를 앞세우는 호박색 띠의 폭.</summary>
        public const float StripeWidth = 26f;
        /// <summary>문구가 뜨기 시작하는 진입 진행률 — 막이 화면 가운데를 막 지날 즈음.</summary>
        public const float TitleIn = 0.2f;
        /// <summary>문구가 뜨고(진행률) 컷인 앞에서 사라지는(진행률) 길이.</summary>
        public const float TitleFade = 0.12f;
        /// <summary>컷인이 없는 전투에서 인트로 끝에 사라지는 초(인트로 시계).</summary>
        public const float TitleOutSeconds = 0.25f;

        public const float PopStart = 0.7f;
        public const float PopPeak = 1.12f;
        public const float PopPeakAt = 0.14f;
        public const float PopSettleAt = 0.3f;

        /// <summary>열린 정도 0~1(0 = 화면을 다 덮음, 1 = 다 열림). 끝으로 갈수록 느려진다.</summary>
        public static float WipeOpen(float entryProgress)
        {
            if (entryProgress >= WipeEnd) return 1f;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(entryProgress / WipeEnd));
        }

        /// <summary>모서리가 화면 위·아래 끝에서 가운데보다 옆으로 비켜 있는 거리.</summary>
        public static float HalfSlant(float screenHeight) => screenHeight * 0.5f * Mathf.Tan(WipeAngle * Mathf.Deg2Rad);

        /// <summary>
        /// 모서리가 화면 가운데 높이에서 지나는 x(가상 좌표). 막은 이 모서리의 오른쪽이다. 열림 0이면 띠까지 화면 왼쪽 밖이라
        /// 화면 전체가 막이고, 1이면 모서리의 가장 왼쪽(화면 아래)도 화면 오른쪽 밖이라 다 열렸다.
        /// </summary>
        public static float WipeEdgeX(float open, float screenWidth, float screenHeight)
        {
            float slant = HalfSlant(screenHeight);
            float from = -slant - StripeWidth * 2f;
            float to = screenWidth + slant;
            return Mathf.Lerp(from, to, Mathf.Clamp01(open));
        }

        /// <summary>모서리 선이 높이 <paramref name="y"/>(아래로 증가)에서 지나는 x.</summary>
        public static float EdgeXAt(float edgeX, float y, float screenHeight)
            => edgeX + (screenHeight * 0.5f - y) * Mathf.Tan(WipeAngle * Mathf.Deg2Rad);

        /// <summary>큰 문구의 불투명도.</summary>
        /// <param name="introElapsed">인트로 시계(초).</param>
        /// <param name="introSeconds">인트로 전체 길이(초).</param>
        /// <param name="cutInFollows">진입 구간 뒤에 대결·수문장 컷인이 이어지는가.</param>
        public static float TitleAlpha(float entryProgress, float introElapsed, float introSeconds, bool cutInFollows)
        {
            float fadeIn = Mathf.Clamp01((entryProgress - TitleIn) / TitleFade);
            float fadeOut = cutInFollows
                ? Mathf.Clamp01((1f - entryProgress) / TitleFade)
                : Mathf.Clamp01((introSeconds - introElapsed) / TitleOutSeconds);
            return fadeIn * fadeOut;
        }

        /// <summary>문구가 뜬 뒤 <paramref name="sinceShown"/>초의 크기 — 작게 시작해 살짝 넘쳤다가 1로. 줄인 움직임이면 늘 1.</summary>
        public static float Pop(float sinceShown, bool reducedMotion)
        {
            if (reducedMotion) return 1f;
            if (sinceShown <= 0f) return PopStart;
            if (sinceShown < PopPeakAt) return Mathf.Lerp(PopStart, PopPeak, Mathf.SmoothStep(0f, 1f, sinceShown / PopPeakAt));
            if (sinceShown < PopSettleAt)
                return Mathf.Lerp(PopPeak, 1f, Mathf.SmoothStep(0f, 1f, (sinceShown - PopPeakAt) / (PopSettleAt - PopPeakAt)));
            return 1f;
        }
    }

    /// <summary>
    /// 진입 문구의 자리 — <b>순수</b>. 1대1은 위쪽 가운데: 가로 화면이면 양쪽 HP 카드 <b>사이</b>(아레나 진입 샷은 그 아래에서 돈다),
    /// 세로처럼 카드 사이가 좁으면 카드 밑 쌓기(장부 게이지·보정 칩)가 다 서도 닿지 않는 그 아래. 레이드는 아래 무대
    /// (<see cref="RaidStageLayout.LowerStage"/>) 가운데 — 위쪽은 보스 자리라 옛 "RAID BOSS"(화면 30%)가 보스와 겹쳤다.
    /// </summary>
    public static class BattleEntryLayout
    {
        public const int TitleFont = 60;
        public const float TitleHeight = 132f;
        public const float TitleMaxWidth = 1100f;
        public const float TitlePadX = 28f;
        /// <summary>HP 카드 사이 틈이 이보다 좁으면(세로 화면) 카드 밑으로 내린다.</summary>
        public const float MinGapWidth = 720f;

        public const int RaidTitleFont = 54;
        public const int RaidSubFont = 28;
        public const float RaidTitleHeight = 170f;

        public static Rect DuelTitle(HudFrame f)
        {
            Rect safe = DuelHudLayout.Safe(f);
            Rect player = DuelHudLayout.HpCard(safe, true);
            Rect enemy = DuelHudLayout.HpCard(safe, false);
            float gap = enemy.x - player.xMax - UITheme.Space.M * 2f;
            // 폭은 튀어나올 때(최대 BattleEntryStaging.PopPeak배)에도 자리 밖으로 나가지 않게 줄여 잡는다 — 가로 화면이면 HP 카드에 닿지 않게.
            if (gap >= MinGapWidth)
            {
                float w = Mathf.Min(TitleMaxWidth, gap / BattleEntryStaging.PopPeak);
                float cx = (player.xMax + enemy.x) * 0.5f;
                float h = Mathf.Min(TitleHeight, DuelHudLayout.HpCardHeight);
                return new Rect(cx - w * 0.5f, player.y + (DuelHudLayout.HpCardHeight - h) * 0.5f, w, h);
            }
            float width = Mathf.Min(TitleMaxWidth, f.ContentWidth / BattleEntryStaging.PopPeak);
            float top = BattleHudStack.BubbleTop(enemy, true, true);
            return new Rect(safe.center.x - width * 0.5f, top, width, TitleHeight);
        }

        public static Rect RaidTitle(HudFrame f)
        {
            Rect stage = RaidStageLayout.LowerStage(f);
            float w = Mathf.Min(TitleMaxWidth, f.ContentWidth / BattleEntryStaging.PopPeak);
            float h = Mathf.Min(RaidTitleHeight, stage.height);
            return new Rect(stage.x + (stage.width - w) * 0.5f, stage.y + (stage.height - h) * 0.5f, w, h);
        }
    }

    /// <summary>상성 표시의 색 갈래 — 잘 통함(민트)·안 통함(코랄)·거의 안 통함(회색).</summary>
    public enum MatchupTone { None, Good, Bad, Faint }

    /// <summary>
    /// 상성을 아이 말로 — <b>순수</b>. 타격 순간 피해 숫자 위의 큰 표시(화살표 + 「아주 잘 통했다!」)와 스킬 카드 칩(「잘 통해요」 + 화살표).
    /// 등급은 <see cref="ElementMatchup"/>(배수 경계 2.0 / 1.05 / 0.95 / 0.5)가 정한다 — 1대1·레이드·칩이 같은 등급을 읽는다.
    /// 예전엔 같은 타격에 컨트롤러가 위쪽 효과 문구(「효과가 굉장했다!」)도 띄워 한 번 맞는데 상성 문구가 두 번 보일 뻔했다 — 지금은 여기 하나다.
    /// </summary>
    public static class MatchupHud
    {
        public const int CaptionFont = 34;
        public const float CaptionHeight = 52f;
        public const float CaptionArrowWidth = 22f;
        public const float CaptionArrowHeight = 30f;
        public const float ArrowGap = 4f;
        public const float IconTextGap = 10f;
        public const float CaptionPadX = 18f;
        /// <summary>숫자 위 「치명타!」 줄이 숫자 윗변 위로 차지하는 높이(가운데 = 윗변 − 22, 높이 40).</summary>
        public const float CritRowHeight = 42f;
        /// <summary>숫자(또는 「치명타!」)와 상성 표시 사이.</summary>
        public const float CaptionGap = 4f;
        public const float PopPeak = 1.3f;
        public const float PopSeconds = 0.2f;

        public const int ChipFont = 20;
        public const float ChipHeight = 28f;
        public const float ChipArrowWidth = 14f;
        public const float ChipPadX = 12f;

        /// <summary>타격 순간 문구. 보통이면 null(아무것도 띄우지 않는다).</summary>
        public static string ImpactText(Matchup m)
        {
            switch (m)
            {
                case Matchup.Super: return "아주 잘 통했다!";
                case Matchup.Good: return "잘 통했다!";
                case Matchup.Weak: return "별로 안 통했다…";
                case Matchup.Resisted: return "거의 안 통했다…";
                default: return null;
            }
        }

        /// <summary>스킬 카드 칩 문구(화살표는 도형으로 따로 그린다). 보통이면 null.</summary>
        public static string ChipText(Matchup m)
        {
            switch (m)
            {
                case Matchup.Super: return "아주 잘 통해요";
                case Matchup.Good: return "잘 통해요";
                case Matchup.Weak: return "안 통해요";
                case Matchup.Resisted: return "거의 안 통해요";
                default: return null;
            }
        }

        /// <summary>화살표 개수 — 아주(2)·잘(1)·안(1)·거의 안(2), 보통 0.</summary>
        public static int ArrowCount(Matchup m)
        {
            switch (m)
            {
                case Matchup.Super:
                case Matchup.Resisted:
                    return 2;
                case Matchup.Good:
                case Matchup.Weak:
                    return 1;
                default:
                    return 0;
            }
        }

        /// <summary>화살표가 위를 가리키는가(잘 통함).</summary>
        public static bool PointsUp(Matchup m) => ElementMatchup.IsFavorable(m);

        public static MatchupTone Tone(Matchup m)
        {
            switch (m)
            {
                case Matchup.Super:
                case Matchup.Good:
                    return MatchupTone.Good;
                case Matchup.Weak:
                    return MatchupTone.Bad;
                case Matchup.Resisted:
                    return MatchupTone.Faint;
                default:
                    return MatchupTone.None;
            }
        }

        /// <summary>화살표 묶음의 폭.</summary>
        public static float IconWidth(int arrows, float arrowWidth, float gap)
            => arrows <= 0 ? 0f : arrows * arrowWidth + (arrows - 1) * gap;

        /// <summary>타격 표시 알약의 폭 — 좌우 여백 + 화살표 + 간격 + 글자.</summary>
        public static float CaptionWidth(Matchup m, float textWidth)
        {
            float icon = IconWidth(ArrowCount(m), CaptionArrowWidth, ArrowGap);
            return CaptionPadX * 2f + icon + (icon > 0f ? IconTextGap : 0f) + Mathf.Max(0f, textWidth);
        }

        /// <summary>
        /// 타격 표시의 세로 가운데 — 피해 숫자 윗변(1대1·레이드의 「치명타!」와 같은 어림: 가운데 − 글자 × 0.55) 위, 치명타면 「치명타!」 줄 위.
        /// </summary>
        public static float CaptionCenterY(float numberCenterY, int numberFontSize, bool crit)
        {
            float top = numberCenterY - numberFontSize * 0.55f;
            if (crit) top -= CritRowHeight;
            return CaptionCenterAbove(top);
        }

        /// <summary>윗변 <paramref name="topY"/> 바로 위에 서는 타격 표시의 세로 가운데.</summary>
        public static float CaptionCenterAbove(float topY) => topY - CaptionGap - CaptionHeight * 0.5f;

        /// <summary>뜬 뒤 <paramref name="age"/>초의 크기 — 1.3에서 0.2초에 걸쳐 1로. 줄인 움직임이면 1.</summary>
        public static float CaptionScale(float age, bool reducedMotion)
        {
            if (reducedMotion || age >= PopSeconds) return 1f;
            if (age <= 0f) return PopPeak;
            return Mathf.Lerp(PopPeak, 1f, Mathf.SmoothStep(0f, 1f, age / PopSeconds));
        }

        /// <summary>폭 <paramref name="halfWidth"/>×2의 표시가 안전 영역 밖으로 나가지 않는 가운데 x.</summary>
        public static float ClampCenterX(float cx, float halfWidth, HudFrame f)
        {
            float lo = f.ContentLeft + halfWidth;
            float hi = f.ContentRight - halfWidth;
            return lo > hi ? (f.ContentLeft + f.ContentRight) * 0.5f : Mathf.Clamp(cx, lo, hi);
        }

        /// <summary>스킬 카드 칩의 폭 — 줄 폭을 넘지 않는다.</summary>
        public static float ChipWidth(Matchup m, float textWidth, float rowWidth)
        {
            float icon = IconWidth(ArrowCount(m), ChipArrowWidth, ArrowGap);
            return Mathf.Min(rowWidth, ChipPadX * 2f + Mathf.Max(0f, textWidth) + (icon > 0f ? UITheme.Space.XS + icon : 0f));
        }
    }

    /// <summary>
    /// 1대1 전용기 컷인 띠의 자리·움직임 — <b>순수</b>. 아레나가 시전자를 클로즈업하는 동안(<c>BattleArenaController.IsSignatureCutIn</c>,
    /// 0~0.5초) 화면 가로를 가로지르는 띠가 내 쪽이면 왼쪽에서, 상대 쪽이면 오른쪽에서 밀려 들어온다. 띠는 아래쪽 행동 문구 띠 바로 위 —
    /// HP 카드 쌓기·전투 문구 줄(화면 25%)·공격 배너와 겹치지 않는다. 컷인이 끝나면 <see cref="TailSeconds"/>에 걸쳐 사라진다.
    /// </summary>
    public static class SignatureCutInLayout
    {
        public const float BandHeight = 210f;
        public const int SkillFont = 72;
        public const int CasterFont = 28;
        public const int BadgeFont = 26;
        public const float BadgeWidth = 150f;
        public const float BadgeHeight = 46f;
        public const float PadTop = 18f;
        public const float PadBottom = 14f;
        public const float RowGap = 6f;
        /// <summary>컷인 진행률 중 밀려 들어오는 몫 — 나머지 동안은 제자리에서 읽힌다.</summary>
        public const float SlideShare = 0.45f;
        /// <summary>컷인이 끝난 뒤 사라지는 초(연출 시계).</summary>
        public const float TailSeconds = 0.18f;
        public const string Badge = "전용기!";

        public struct Plan
        {
            public Rect Band;
            public Rect Badge;
            public Rect Caster;
            public Rect Skill;
        }

        /// <summary>띠 한 장 — 화면 가로 전체(가장자리까지), 글자는 안전 영역 안. 내 쪽이면 왼쪽 맞춤, 상대 쪽이면 오른쪽 맞춤.</summary>
        public static Plan For(HudFrame f, bool fromPlayer)
        {
            Rect bar = DuelHudLayout.ActionBar(f);
            float bottom = bar.y - UITheme.Space.M;
            var plan = new Plan { Band = new Rect(0f, bottom - BandHeight, f.Width, BandHeight) };
            float left = f.ContentLeft + UITheme.Space.L;
            float right = f.ContentRight - UITheme.Space.L;
            float w = Mathf.Max(1f, right - left);
            float rowY = plan.Band.y + PadTop;
            float casterW = Mathf.Max(1f, w - BadgeWidth - UITheme.Space.S);
            if (fromPlayer)
            {
                plan.Badge = new Rect(left, rowY, BadgeWidth, BadgeHeight);
                plan.Caster = new Rect(plan.Badge.xMax + UITheme.Space.S, rowY, casterW, BadgeHeight);
            }
            else
            {
                plan.Badge = new Rect(right - BadgeWidth, rowY, BadgeWidth, BadgeHeight);
                plan.Caster = new Rect(left, rowY, casterW, BadgeHeight);
            }
            float skillTop = rowY + BadgeHeight + RowGap;
            plan.Skill = new Rect(left, skillTop, w, Mathf.Max(1f, plan.Band.yMax - PadBottom - skillTop));
            return plan;
        }

        /// <summary>밀려 들어오는 정도 — 1이면 화면 폭만큼 바깥, 0이면 제자리. 줄인 움직임이면 늘 0.</summary>
        public static float Slide(float cutInProgress, bool reducedMotion)
        {
            if (reducedMotion) return 0f;
            float u = Mathf.Clamp01(cutInProgress / SlideShare);
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            return 1f - eased;
        }

        /// <summary>가로 밀림(가상 px) — 내 쪽은 왼쪽(음수)에서, 상대 쪽은 오른쪽(양수)에서.</summary>
        public static float Offset(float slide, bool fromPlayer, float screenWidth)
            => slide * screenWidth * (fromPlayer ? -1f : 1f);

        /// <summary>
        /// 불투명도 — 컷인 동안 1(첫 프레임부터 — 짧은 구간이라 처음부터 읽혀야 한다), 끝난 뒤 <see cref="TailSeconds"/>에 걸쳐 0.
        /// <paramref name="sinceCutInEnd"/>는 컷인 끝 뒤 지난 초(컷인 중이면 음수).
        /// </summary>
        public static float Alpha(float sinceCutInEnd)
        {
            if (sinceCutInEnd <= 0f) return 1f;
            return Mathf.Clamp01(1f - sinceCutInEnd / TailSeconds);
        }
    }

    /// <summary>승리 화면 보상 줄의 색 갈래.</summary>
    public enum RewardTone { Candy, Exp, Coin, Item, Caught, Missed, Bonus, Cheer }

    /// <summary>승리 화면 보상 한 줄. <see cref="Wide"/>면 두 칸을 다 쓴다(곤충 포획·응원 한 줄 — 맨 끝에 온다).</summary>
    public struct RewardLine
    {
        public string Text;
        public RewardTone Tone;
        public bool Wide;

        public RewardLine(string text, RewardTone tone, bool wide = false)
        {
            Text = text;
            Tone = tone;
            Wide = wide;
        }
    }

    /// <summary>
    /// 승리 화면 보상 줄 짓기 — <b>순수</b>. 순서는 캔디 → 경험치 → 코인 → 아이템 → 곤충(가장 큰 소식이 마지막에 뜬다).
    /// 0인 보상은 줄을 만들지 않는다. 좁은 줄이 먼저, 넓은 줄이 뒤다(<see cref="BattleVictoryLayout.Cell"/>가 이 순서를 믿는다).
    /// </summary>
    public static class BattleRewardLines
    {
        public const string DuelEmptyCheer = "파트너와 함께 한 뼘 자랐다!";
        public const string DreamCheer = "관중석에서 함성이 쏟아진다!";
        /// <summary>이름표가 없는 재료(<c>mat_*</c>) — 아이에게 ID를 보이지 않는다.</summary>
        public const string MaterialName = "재료";

        /// <summary>아이템 데이터베이스에 이름이 없을 때의 표시 — 재료(<c>mat_</c>로 시작)는 「재료」, 그 밖은 ID 그대로(표시 누락 방지).</summary>
        public static string FallbackItemName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            return itemId.StartsWith("mat_", System.StringComparison.Ordinal) ? MaterialName : itemId;
        }

        /// <param name="coins">이번 승리로 받은 코인(모르면 0 — 줄을 만들지 않는다).</param>
        /// <param name="itemName">아이템 이름(없으면 빈 값).</param>
        /// <param name="captureTried">전투 포획 기회가 있었다(야생 승리).</param>
        public static List<RewardLine> Duel(int candy, int exp, int coins, string itemName, int itemCount,
            bool captureTried, bool captured, string insectName)
        {
            var lines = new List<RewardLine>(6);
            if (candy > 0) lines.Add(new RewardLine($"캔디 +{candy}", RewardTone.Candy));
            if (exp > 0) lines.Add(new RewardLine($"경험치 +{exp}", RewardTone.Exp));
            if (coins > 0) lines.Add(new RewardLine($"코인 +{coins}", RewardTone.Coin));
            if (!string.IsNullOrEmpty(itemName) && itemCount > 0)
                lines.Add(new RewardLine($"{itemName} ×{itemCount}", RewardTone.Item));
            if (captureTried)
            {
                string bug = string.IsNullOrEmpty(insectName) ? "곤충" : insectName;
                lines.Add(captured
                    ? new RewardLine($"{bug}{KoreanJosa.EulReul(bug)} 잡았다!", RewardTone.Caught, true)
                    : new RewardLine($"{bug}{KoreanJosa.EunNeun(bug)} 달아났다", RewardTone.Missed, true));
            }
            if (lines.Count == 0) lines.Add(new RewardLine(DuelEmptyCheer, RewardTone.Cheer, true));
            return lines;
        }

        /// <summary>레이드 승리 — 캔디·경험치(×3이 이미 곱해진 값)·보너스 표시·보스 포획(레이드는 이기면 반드시 잡는다).</summary>
        public static List<RewardLine> Raid(int candy, int exp, string bossName)
        {
            var lines = new List<RewardLine>(4);
            if (candy > 0) lines.Add(new RewardLine($"캔디 +{candy}", RewardTone.Candy));
            if (exp > 0) lines.Add(new RewardLine($"경험치 +{exp}", RewardTone.Exp));
            lines.Add(new RewardLine("레이드 보너스 ×3", RewardTone.Bonus));
            string boss = string.IsNullOrEmpty(bossName) ? "보스" : bossName;
            lines.Add(new RewardLine($"{boss}{KoreanJosa.EulReul(boss)} 잡았다!", RewardTone.Caught, true));
            return lines;
        }

        /// <summary>보상이 없는 승리(꿈 챔피언전) — 응원 한 줄.</summary>
        public static List<RewardLine> Cheer(string text)
            => new List<RewardLine> { new RewardLine(string.IsNullOrEmpty(text) ? DreamCheer : text, RewardTone.Cheer, true) };
    }

    /// <summary>
    /// 승리 화면 — <b>순수</b>. 위에서부터 큰 「승리!」(살짝 튀어나온다) → [대결 상대의 한마디] → (가운데는 비운다 — 아레나 승리 연출
    /// 2초가 거기서 돈다) → 보상 판(0.8초부터 한 줄씩) → 맨 아래 깜빡이는 「눌러서 계속」(닫을 수 있을 때부터).
    /// 보상 판은 좁은 줄을 두 칸씩, 넓은 줄(곤충 포획)은 한 줄 통째로 — 줄 수를 줄여 가운데를 넓게 남긴다.
    /// 예전 결과 창은 화면 가운데 판 하나라 승리 포즈를 덮었고, 저절로 돌아간다는 안내가 적혀 있었다(이제 눌러야 닫힌다 — BattleResultRules).
    /// </summary>
    public static class BattleVictoryLayout
    {
        public const string Title = "승리!";
        public const string ChampionTitle = "챔피언 승리!";
        public const string RaidTitle = "레이드 승리!";
        public const string ContinueHint = "눌러서 계속";

        public const int TitleFont = 96;
        public const float TitleHeight = 150f;
        public const float TitleMaxWidth = 760f;
        public const float QuoteMaxWidth = 900f;

        public const int RowFont = 30;
        public const int WideRowFont = 34;
        public const float RowHeight = 56f;
        public const float RowGap = 8f;
        public const float PanelPad = 14f;
        public const float PanelMaxWidth = 940f;

        public const int HintFont = 30;
        public const float HintHeight = 60f;
        public const float HintMaxWidth = 420f;

        /// <summary>보상 첫 줄이 뜨는 초 — 「승리!」가 자리 잡고 아레나 포즈가 한창일 때.</summary>
        public const float RewardStart = 0.8f;
        public const float RewardStep = 0.3f;
        public const float RewardFade = 0.2f;
        /// <summary>뜰 때 아래에서 올라오는 거리.</summary>
        public const float RewardRise = 10f;

        public const float TitlePopStart = 0.6f;
        public const float TitlePopPeak = 1.15f;
        public const float TitlePopPeakAt = 0.18f;
        public const float TitlePopSettleAt = 0.34f;

        public const float HintFadeSeconds = 0.25f;
        public const float HintBlinkPeriod = 1.1f;

        public static Rect TitleRect(HudFrame f) => f.TopPanel(Mathf.Min(TitleMaxWidth, f.ContentWidth), TitleHeight);

        /// <summary>대결 상대의 한마디 — 「승리!」 바로 아래(말풍선 높이). 「승리!」가 튀어나올 때 커지는 몫만큼 더 띄운다.</summary>
        public static Rect QuoteRect(HudFrame f)
        {
            Rect title = TitleRect(f);
            float w = Mathf.Min(QuoteMaxWidth, f.ContentWidth);
            float popOverflow = title.height * (TitlePopPeak - 1f) * 0.5f;
            return new Rect(f.ContentLeft + (f.ContentWidth - w) * 0.5f, title.yMax + popOverflow + UITheme.Space.S, w,
                BattleHudStack.BubbleHeight);
        }

        public static Rect HintRect(HudFrame f) => f.BottomPanel(Mathf.Min(HintMaxWidth, f.ContentWidth), HintHeight);

        public struct Grid
        {
            public Rect Panel;
            public int Columns;
            public int Narrow;
            public int Rows;
        }

        /// <summary>좁은 줄이 둘 이상이면 두 칸.</summary>
        public static int Columns(int narrow) => narrow >= 2 ? 2 : 1;

        /// <summary>보상 판 — 「눌러서 계속」 바로 위, 가로 가운데.</summary>
        public static Grid Rewards(HudFrame f, IReadOnlyList<RewardLine> lines)
        {
            int narrow = 0, wide = 0;
            if (lines != null)
                for (int i = 0; i < lines.Count; i++)
                    if (lines[i].Wide) wide++;
                    else narrow++;
            int cols = Columns(narrow);
            int rows = (narrow + cols - 1) / cols + wide;
            float h = PanelPad * 2f + rows * RowHeight + Mathf.Max(0, rows - 1) * RowGap;
            float w = Mathf.Min(PanelMaxWidth, f.ContentWidth);
            Rect hint = HintRect(f);
            float bottom = hint.y - UITheme.Space.S;
            return new Grid
            {
                Panel = new Rect(f.ContentLeft + (f.ContentWidth - w) * 0.5f, bottom - h, w, h),
                Columns = cols,
                Narrow = narrow,
                Rows = rows
            };
        }

        /// <summary><paramref name="index"/>번째 줄의 칸. 좁은 줄은 왼쪽→오른쪽·위→아래, 넓은 줄은 그 아래 한 줄씩.</summary>
        public static Rect Cell(Grid g, int index, IReadOnlyList<RewardLine> lines)
        {
            float innerW = g.Panel.width - PanelPad * 2f;
            float x0 = g.Panel.x + PanelPad;
            float y0 = g.Panel.y + PanelPad;
            int narrowBefore = 0, wideBefore = 0;
            for (int i = 0; i < index && lines != null && i < lines.Count; i++)
                if (lines[i].Wide) wideBefore++;
                else narrowBefore++;
            bool wideRow = lines != null && index < lines.Count && lines[index].Wide;
            int narrowRows = (g.Narrow + g.Columns - 1) / g.Columns;
            if (wideRow)
            {
                int row = narrowRows + wideBefore;
                return new Rect(x0, y0 + row * (RowHeight + RowGap), innerW, RowHeight);
            }
            int col = narrowBefore % g.Columns;
            int r = narrowBefore / g.Columns;
            float cellW = (innerW - (g.Columns - 1) * RowGap) / g.Columns;
            return new Rect(x0 + col * (cellW + RowGap), y0 + r * (RowHeight + RowGap), cellW, RowHeight);
        }

        public static float RewardAppearAt(int index) => RewardStart + Mathf.Max(0, index) * RewardStep;

        public static float RewardAlpha(float shownSeconds, int index)
            => Mathf.Clamp01((shownSeconds - RewardAppearAt(index)) / RewardFade);

        /// <summary>「승리!」 크기 — 작게 시작해 살짝 넘쳤다가(1.15) 1로. 줄인 움직임이면 1.</summary>
        public static float TitleScale(float shownSeconds, bool reducedMotion)
        {
            if (reducedMotion) return 1f;
            float s = shownSeconds;
            if (s <= 0f) return TitlePopStart;
            if (s < TitlePopPeakAt) return Mathf.Lerp(TitlePopStart, TitlePopPeak, Mathf.SmoothStep(0f, 1f, s / TitlePopPeakAt));
            if (s < TitlePopSettleAt)
                return Mathf.Lerp(TitlePopPeak, 1f, Mathf.SmoothStep(0f, 1f, (s - TitlePopPeakAt) / (TitlePopSettleAt - TitlePopPeakAt)));
            return 1f;
        }

        public static float TitleAlpha(float shownSeconds) => Mathf.Clamp01(shownSeconds / 0.12f);

        /// <summary>
        /// 「눌러서 계속」 불투명도 — 닫을 수 없으면 0, 닫을 수 있게 된 뒤 0.25초에 떠올라 천천히 깜빡인다(0.55~1). 줄인 움직임이면 깜빡이지 않는다.
        /// </summary>
        /// <param name="sinceCanClose">닫을 수 있게 된 뒤 지난 초.</param>
        public static float HintAlpha(bool canClose, float sinceCanClose, bool reducedMotion)
        {
            if (!canClose) return 0f;
            float fade = Mathf.Clamp01(sinceCanClose / HintFadeSeconds);
            if (reducedMotion) return fade;
            float blink = 0.775f + 0.225f * Mathf.Cos(Mathf.Max(0f, sinceCanClose) / HintBlinkPeriod * Mathf.PI * 2f);
            return fade * blink;
        }
    }

    /// <summary>
    /// 레이드 팀 패널 줄의 움직임 — <b>순수</b>. 3D 연출(팀원 공격·보스 예고·보스 공격·합체공격) 동안 화면 아래로 비켜서고,
    /// 수문장 등장·그림자 변신·결과 동안은 숨는다(그때 줄이 화면 53%에 떠서 보스 아랫부분과 변신 연기를 가렸다 — 3단계 QA).
    /// <b>내려갈 때는 곧바로</b> — 기술을 고른 순간 스킬 패널이 사라지는데 줄이 0.25초 동안 화면 가운데를 지나 내려가서,
    /// 행동 문구 띠(줄에 붙어 있다)와 함께 "아래 패널이 가운데 떴다 내려가는" 것처럼 보였다(1단계 QA). 올라올 때는 미끄러진다.
    /// </summary>
    public static class RaidTeamStrip
    {
        public const float SlideSeconds = 0.25f;
        public const float FadeSeconds = 0.2f;

        /// <summary>다음 내려감 값(0 = 쉬는 자리, 1 = 화면 아래).</summary>
        public static float NextDrop(float current, bool acting, float dt)
            => acting ? 1f : Mathf.MoveTowards(current, 0f, Mathf.Max(0f, dt) / SlideSeconds);

        /// <summary>줄을 숨기는 단계인가.</summary>
        public static bool Hidden(bool guardianIntro, bool transforming, bool result) => guardianIntro || transforming || result;

        /// <summary>다음 숨김 값(0 = 보임, 1 = 숨음). <paramref name="snap"/>이면 곧바로 숨는다(전투 첫 장면부터 숨어야 할 때).</summary>
        public static float NextHide(float current, bool hidden, bool snap, float dt)
        {
            if (hidden) return snap ? 1f : Mathf.MoveTowards(current, 1f, Mathf.Max(0f, dt) / FadeSeconds);
            return Mathf.MoveTowards(current, 0f, Mathf.Max(0f, dt) / FadeSeconds);
        }
    }

    /// <summary>
    /// 1대1·레이드가 함께 쓰는 그리기 — 진입 막·큰 문구, 상성 표시·칩, 전용기 띠, 승리 화면 조각. 자리·시각은 위의 순수 계산이 정하고
    /// 여기는 칠하기만 한다. 크기 변화(튀어나오기)는 글자 상자를 바꾸지 않고 GUI 행렬로 키운다 — 상자가 매 프레임 바뀌면 LabelFit 캐시가
    /// 프레임마다 새 항목으로 찬다. 회전·확대는 <c>GUIUtility.RotateAroundPivot</c> 대신 행렬을 곱한다(UIScale≠1이면 피벗이 어긋난다).
    /// </summary>
    internal static class BattleFeelDraw
    {
        private static GUIStyle titleStyle;
        private static GUIStyle subStyle;
        private static GUIStyle captionStyle;
        private static GUIStyle chipStyle;
        private static GUIStyle badgeStyle;
        private static GUIStyle casterStyle;
        private static GUIStyle skillStyle;
        private static GUIStyle victoryStyle;
        private static GUIStyle rowStyle;
        private static GUIStyle hintStyle;
        private static GUIContent measure;
        private static readonly float[] captionWidths = new float[5];
        private static readonly float[] chipWidths = new float[5];

        private static void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = BattleEntryLayout.TitleFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            subStyle = new GUIStyle(GUI.skin.label)
            { fontSize = BattleEntryLayout.RaidSubFont, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            captionStyle = new GUIStyle(GUI.skin.label)
            { fontSize = MatchupHud.CaptionFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            chipStyle = new GUIStyle(GUI.skin.label)
            { fontSize = MatchupHud.ChipFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            badgeStyle = new GUIStyle(GUI.skin.label)
            { fontSize = SignatureCutInLayout.BadgeFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            casterStyle = new GUIStyle(GUI.skin.label)
            { fontSize = SignatureCutInLayout.CasterFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            skillStyle = new GUIStyle(GUI.skin.label)
            { fontSize = SignatureCutInLayout.SkillFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            victoryStyle = new GUIStyle(GUI.skin.label)
            { fontSize = BattleVictoryLayout.TitleFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            rowStyle = new GUIStyle(GUI.skin.label)
            { fontSize = BattleVictoryLayout.RowFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            hintStyle = new GUIStyle(GUI.skin.label)
            { fontSize = BattleVictoryLayout.HintFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            measure = new GUIContent();
            for (int i = 0; i < captionWidths.Length; i++)
            {
                captionWidths[i] = -1f;
                chipWidths[i] = -1f;
            }
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        /// <summary>가운데 <paramref name="pivot"/>를 축으로 <paramref name="scale"/>배 — 돌려줄 원래 행렬을 준다.</summary>
        private static Matrix4x4 PushScale(Vector2 pivot, float scale)
        {
            Matrix4x4 saved = GUI.matrix;
            if (Mathf.Abs(scale - 1f) > 0.0001f)
                GUI.matrix = saved
                    * Matrix4x4.TRS(new Vector3(pivot.x, pivot.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f))
                    * Matrix4x4.TRS(new Vector3(-pivot.x, -pivot.y, 0f), Quaternion.identity, Vector3.one);
            return saved;
        }

        internal static Color ToneColor(MatchupTone tone)
        {
            UITheme t = UITheme.Instance;
            switch (tone)
            {
                case MatchupTone.Good: return t.accentMint;
                case MatchupTone.Bad: return t.accentCoral;
                case MatchupTone.Faint: return t.textSecondary;
                default: return t.textPrimary;
            }
        }

        internal static Color RewardColor(RewardTone tone)
        {
            UITheme t = UITheme.Instance;
            switch (tone)
            {
                case RewardTone.Candy: return t.accentCoral;
                case RewardTone.Exp: return t.skillDefense;
                case RewardTone.Coin: return t.coinColor;
                case RewardTone.Item: return t.accentAmber;
                case RewardTone.Caught: return t.accentMint;
                case RewardTone.Bonus: return t.accentAmber;
                case RewardTone.Missed: return t.textSecondary;
                default: return t.textPrimary;
            }
        }

        /// <summary>화살표 <paramref name="count"/>개를 나란히.</summary>
        internal static void Arrows(Rect area, int count, bool up, float arrowWidth, float gap, Color color)
        {
            for (int i = 0; i < count; i++)
                UIShapes.Arrow(new Rect(area.x + i * (arrowWidth + gap), area.y, arrowWidth, area.height), up, color);
        }

        // ── 진입 ──

        /// <summary>
        /// 화면 쓸기 — 어두운 막(호박색 띠를 앞세운 기울어진 모서리)이 오른쪽으로 쓸려 나간다. 줄인 움직임이면 막이 그 자리에서 옅어진다.
        /// 전투 화면의 다른 조각(HP 카드 등)보다 <b>나중에</b> 불러야 덮는다.
        /// </summary>
        internal static void EntryWipe(HudFrame f, float entryProgress, bool reducedMotion)
        {
            float open = BattleEntryStaging.WipeOpen(entryProgress);
            if (open >= 1f) return;
            UITheme theme = UITheme.Instance;
            Rect screen = new Rect(0f, 0f, f.Width, f.Height);
            if (reducedMotion)
            {
                UISurface.Flat(screen, WithAlpha(theme.surfaceBase, 1f - open));
                return;
            }
            float edge = BattleEntryStaging.WipeEdgeX(open, f.Width, f.Height);
            Vector2 pivot = new Vector2(edge, f.Height * 0.5f);
            float reach = (f.Width + f.Height) * 1.5f;
            Matrix4x4 saved = GUI.matrix;
            GUI.matrix = saved
                * Matrix4x4.TRS(new Vector3(pivot.x, pivot.y, 0f), Quaternion.Euler(0f, 0f, BattleEntryStaging.WipeAngle), Vector3.one)
                * Matrix4x4.TRS(new Vector3(-pivot.x, -pivot.y, 0f), Quaternion.identity, Vector3.one);
            float top = pivot.y - reach;
            UISurface.Flat(new Rect(edge, top, reach, reach * 2f), theme.surfaceBase);
            UISurface.Flat(new Rect(edge, top, BattleEntryStaging.StripeWidth, reach * 2f), theme.accentAmber);
            UISurface.Flat(new Rect(edge + BattleEntryStaging.StripeWidth + 10f, top, 8f, reach * 2f), theme.accentCoral);
            GUI.matrix = saved;
        }

        /// <summary>진입 큰 문구 — 어두운 카드 + 강조색 띠 + 큰 글자(둘째 줄은 있을 때만). 크기는 행렬로 키운다.</summary>
        internal static void EntryTitle(Rect rect, string title, string sub, int titleFont, float alpha, float scale, Color accent)
        {
            if (string.IsNullOrEmpty(title) || alpha <= 0.001f) return;
            EnsureStyles();
            UITheme theme = UITheme.Instance;
            Color prev = GUI.color;
            GUI.color = WithAlpha(prev, alpha);
            Matrix4x4 saved = PushScale(rect.center, scale);
            UISurface.Card(rect, theme.surfaceBase, accent);
            UISurface.Flat(new Rect(rect.x + UITheme.Radius.Card, rect.y + 3f, rect.width - UITheme.Radius.Card * 2f, 6f), accent);
            float pad = BattleEntryLayout.TitlePadX;
            bool hasSub = !string.IsNullOrEmpty(sub);
            float titleH = RaidStageLayout.LineHeight(titleFont) + 8f;
            float subH = RaidStageLayout.LineHeight(BattleEntryLayout.RaidSubFont);
            float blockH = hasSub ? titleH + subH : titleH;
            float y = rect.y + (rect.height - blockH) * 0.5f + 2f;
            Rect titleRect = new Rect(rect.x + pad, y, rect.width - pad * 2f, Mathf.Min(titleH, rect.height - 12f));
            titleStyle.fontSize = titleFont;
            titleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            UIHelper.LabelFit(new Rect(titleRect.x + 3f, titleRect.y + 3f, titleRect.width, titleRect.height), title, titleStyle);
            titleStyle.normal.textColor = theme.textPrimary;
            // 상대 이름을 데이터가 정한다 — 넘치면 글자를 줄여 맞춘다.
            UIHelper.LabelFit(titleRect, title, titleStyle);
            if (hasSub)
            {
                subStyle.normal.textColor = theme.textSecondary;
                UIHelper.LabelFit(new Rect(rect.x + pad, titleRect.yMax, rect.width - pad * 2f, subH), sub, subStyle);
            }
            GUI.matrix = saved;
            GUI.color = prev;
        }

        // ── 상성 ──

        private static float CaptionTextWidth(Matchup m, string text)
        {
            int i = Mathf.Clamp((int)m, 0, captionWidths.Length - 1);
            if (captionWidths[i] < 0f)
            {
                captionStyle.fontSize = MatchupHud.CaptionFont;
                measure.text = text;
                captionWidths[i] = captionStyle.CalcSize(measure).x;
            }
            return captionWidths[i];
        }

        private static float ChipTextWidth(Matchup m, string text)
        {
            int i = Mathf.Clamp((int)m, 0, chipWidths.Length - 1);
            if (chipWidths[i] < 0f)
            {
                chipStyle.fontSize = MatchupHud.ChipFont;
                measure.text = text;
                chipWidths[i] = chipStyle.CalcSize(measure).x;
            }
            return chipWidths[i];
        }

        /// <summary>
        /// 타격 순간의 상성 표시 — 가운데 (<paramref name="cx"/>, <paramref name="cy"/>)에 알약 하나(화살표 + 「아주 잘 통했다!」).
        /// 보통이면 아무것도 안 그린다. 안전 영역 밖으로 나가지 않게 가로를 당긴다.
        /// </summary>
        internal static void MatchupCaption(HudFrame f, float cx, float cy, Matchup m, float alpha, float scale)
        {
            string text = MatchupHud.ImpactText(m);
            if (text == null || alpha <= 0.001f) return;
            EnsureStyles();
            UITheme theme = UITheme.Instance;
            float textW = CaptionTextWidth(m, text);
            float w = MatchupHud.CaptionWidth(m, textW);
            float h = MatchupHud.CaptionHeight;
            cx = MatchupHud.ClampCenterX(cx, w * 0.5f, f);
            Rect pill = new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
            Color tone = ToneColor(MatchupHud.Tone(m));

            Color prev = GUI.color;
            GUI.color = WithAlpha(prev, alpha);
            Matrix4x4 saved = PushScale(pill.center, scale);
            UISurface.Card(pill, theme.surfaceBase, tone);
            int arrows = MatchupHud.ArrowCount(m);
            float iconW = MatchupHud.IconWidth(arrows, MatchupHud.CaptionArrowWidth, MatchupHud.ArrowGap);
            float x = pill.x + MatchupHud.CaptionPadX;
            Arrows(new Rect(x, pill.y + (h - MatchupHud.CaptionArrowHeight) * 0.5f, iconW, MatchupHud.CaptionArrowHeight),
                arrows, MatchupHud.PointsUp(m), MatchupHud.CaptionArrowWidth, MatchupHud.ArrowGap, tone);
            if (iconW > 0f) x += iconW + MatchupHud.IconTextGap;
            captionStyle.fontSize = MatchupHud.CaptionFont;
            captionStyle.normal.textColor = Color.Lerp(tone, theme.textPrimary, 0.25f);
            UIHelper.LabelFit(new Rect(x, pill.y, Mathf.Max(1f, pill.xMax - MatchupHud.CaptionPadX - x + 4f), h), text, captionStyle);
            GUI.matrix = saved;
            GUI.color = prev;
        }

        /// <summary>
        /// 스킬 카드의 상성 칩 — 「잘 통해요」 + 화살표. <paramref name="row"/> 안에서 글자 폭에 맞춰 왼쪽(또는 오른쪽) 맞춤. 그린 자리를 돌려준다
        /// (보통이면 아무것도 그리지 않고 폭 0). <paramref name="usable"/>이 거짓(쿨다운)이면 회색.
        /// </summary>
        internal static Rect MatchupChip(Rect row, Matchup m, bool alignRight, bool usable)
        {
            string text = MatchupHud.ChipText(m);
            if (text == null) return new Rect(alignRight ? row.xMax : row.x, row.y, 0f, row.height);
            EnsureStyles();
            UITheme theme = UITheme.Instance;
            float textW = ChipTextWidth(m, text);
            float w = MatchupHud.ChipWidth(m, textW, row.width);
            Rect chip = new Rect(alignRight ? row.xMax - w : row.x, row.y, w, row.height);
            Color tone = usable ? ToneColor(MatchupHud.Tone(m)) : theme.textSecondary;
            UISurface.Chip(chip, string.Empty, Color.Lerp(theme.surfaceBase, tone, 0.22f), tone);
            int arrows = MatchupHud.ArrowCount(m);
            float iconW = MatchupHud.IconWidth(arrows, MatchupHud.ChipArrowWidth, MatchupHud.ArrowGap);
            float arrowH = Mathf.Round(chip.height * 0.62f);
            Rect icon = new Rect(chip.xMax - MatchupHud.ChipPadX - iconW, chip.y + (chip.height - arrowH) * 0.5f, iconW, arrowH);
            Arrows(icon, arrows, MatchupHud.PointsUp(m), MatchupHud.ChipArrowWidth, MatchupHud.ArrowGap, tone);
            chipStyle.fontSize = MatchupHud.ChipFont;
            chipStyle.normal.textColor = Color.Lerp(tone, theme.textPrimary, 0.25f);
            float textRight = iconW > 0f ? icon.x - UITheme.Space.XS : chip.xMax - MatchupHud.ChipPadX;
            UIHelper.LabelFit(new Rect(chip.x + MatchupHud.ChipPadX, chip.y, Mathf.Max(1f, textRight - chip.x - MatchupHud.ChipPadX), chip.height),
                text, chipStyle);
            return chip;
        }

        // ── 전용기 ──

        /// <summary>
        /// 전용기 컷인 띠 — 어두운 띠에 속성색 위아래 테두리·글자 뒤 속성색 물결, 「전용기!」 배지, 시전자 이름(작게), 기술 이름(아주 크게).
        /// </summary>
        internal static void SignatureBand(HudFrame f, bool fromPlayer, string caster, string skill, Color element,
            float cutInProgress, float alpha, bool reducedMotion)
        {
            if (string.IsNullOrEmpty(skill) || alpha <= 0.001f) return;
            EnsureStyles();
            UITheme theme = UITheme.Instance;
            SignatureCutInLayout.Plan plan = SignatureCutInLayout.For(f, fromPlayer);
            float dx = SignatureCutInLayout.Offset(SignatureCutInLayout.Slide(cutInProgress, reducedMotion), fromPlayer, f.Width);
            Rect band = Shift(plan.Band, dx);
            Color readable = SkillUILayout.GetReadableAccent(element);

            Color prev = GUI.color;
            GUI.color = WithAlpha(prev, alpha);
            UISurface.Flat(band, WithAlpha(theme.surfaceBase, 0.9f));
            // 글자 쪽으로 짙어지는 속성색 물결 — 세 겹으로 계단을 지어 그라데이션처럼.
            for (int i = 0; i < 3; i++)
            {
                float share = 0.62f - i * 0.17f;
                float w = band.width * share;
                Rect wash = fromPlayer
                    ? new Rect(band.x, band.y + 6f, w, band.height - 12f)
                    : new Rect(band.xMax - w, band.y + 6f, w, band.height - 12f);
                UISurface.Flat(wash, WithAlpha(element, 0.12f));
            }
            UISurface.Flat(new Rect(band.x, band.y, band.width, 6f), element);
            UISurface.Flat(new Rect(band.x, band.yMax - 6f, band.width, 6f), element);
            // 속도선 — 밀려 들어오는 반대쪽으로 흐른다(줄인 움직임이면 멈춘다).
            float flow = reducedMotion ? 0f : Mathf.Repeat(Time.unscaledTime * 900f, band.width);
            for (int i = 0; i < 4; i++)
            {
                float ly = band.y + 30f + i * (band.height - 60f) / 3f;
                float lx = Mathf.Repeat(i * 0.37f * band.width + (fromPlayer ? -flow : flow), band.width);
                UISurface.Flat(new Rect(band.x + lx, ly, band.width * 0.18f, 2f), WithAlpha(readable, 0.45f));
            }

            Rect badge = Shift(plan.Badge, dx);
            UISurface.Chip(badge, string.Empty, theme.accentAmber, theme.surfaceBase);
            badgeStyle.normal.textColor = theme.surfaceBase;
            UIHelper.LabelFit(badge, SignatureCutInLayout.Badge, badgeStyle);

            TextAnchor anchor = fromPlayer ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            if (!string.IsNullOrEmpty(caster))
            {
                casterStyle.alignment = anchor;
                casterStyle.normal.textColor = theme.textPrimary;
                UIHelper.LabelFit(Shift(plan.Caster, dx), caster, casterStyle);
            }
            Rect skillRect = Shift(plan.Skill, dx);
            skillStyle.alignment = anchor;
            skillStyle.fontSize = SignatureCutInLayout.SkillFont;
            skillStyle.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            UIHelper.LabelFit(new Rect(skillRect.x + 4f, skillRect.y + 4f, skillRect.width, skillRect.height), skill, skillStyle);
            skillStyle.normal.textColor = readable;
            // 기술 이름을 데이터가 정한다 — 넘치면 글자를 줄여 맞춘다.
            UIHelper.LabelFit(skillRect, skill, skillStyle);
            GUI.color = prev;
        }

        private static Rect Shift(Rect r, float dx) => new Rect(r.x + dx, r.y, r.width, r.height);

        /// <summary>「전용기!」 배지 하나(호박색 알약) — 레이드 행동 문구 띠 위에 얹는다.</summary>
        internal static void SignatureBadge(Rect rect, float alpha)
        {
            if (alpha <= 0.001f) return;
            EnsureStyles();
            UITheme theme = UITheme.Instance;
            Color prev = GUI.color;
            GUI.color = WithAlpha(prev, alpha);
            UISurface.Chip(rect, string.Empty, theme.accentAmber, theme.surfaceBase);
            badgeStyle.normal.textColor = theme.surfaceBase;
            UIHelper.LabelFit(rect, SignatureCutInLayout.Badge, badgeStyle);
            GUI.color = prev;
        }

        // ── 승리 ──

        /// <summary>큰 「승리!」 — 반투명 판 위 호박색 글자, 처음에 살짝 튀어나온다.</summary>
        internal static void VictoryTitle(HudFrame f, string text, float shownSeconds, bool reducedMotion)
        {
            if (string.IsNullOrEmpty(text)) return;
            EnsureStyles();
            UITheme theme = UITheme.Instance;
            float alpha = BattleVictoryLayout.TitleAlpha(shownSeconds);
            if (alpha <= 0.001f) return;
            Rect rect = BattleVictoryLayout.TitleRect(f);
            Color prev = GUI.color;
            GUI.color = WithAlpha(prev, alpha);
            Matrix4x4 saved = PushScale(rect.center, BattleVictoryLayout.TitleScale(shownSeconds, reducedMotion));
            UISurface.Card(rect, WithAlpha(theme.surfaceBase, 0.72f), theme.accentAmber);
            victoryStyle.fontSize = BattleVictoryLayout.TitleFont;
            victoryStyle.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            Rect text0 = new Rect(rect.x + 16f, rect.y + 6f, rect.width - 32f, rect.height - 12f);
            UIHelper.LabelFit(new Rect(text0.x + 4f, text0.y + 5f, text0.width, text0.height), text, victoryStyle);
            victoryStyle.normal.textColor = theme.accentAmber;
            UIHelper.LabelFit(text0, text, victoryStyle);
            GUI.matrix = saved;
            GUI.color = prev;
        }

        /// <summary>보상 판 — 첫 줄이 뜰 때 판도 함께 떠오르고, 줄마다 정해진 시각(0.8초부터 0.3초 간격)에 아래에서 올라온다.</summary>
        internal static void RewardPanel(HudFrame f, IReadOnlyList<RewardLine> lines, float shownSeconds)
        {
            if (lines == null || lines.Count == 0) return;
            float panelAlpha = BattleVictoryLayout.RewardAlpha(shownSeconds, 0);
            if (panelAlpha <= 0.001f) return;
            EnsureStyles();
            UITheme theme = UITheme.Instance;
            BattleVictoryLayout.Grid grid = BattleVictoryLayout.Rewards(f, lines);
            Color prev = GUI.color;
            GUI.color = WithAlpha(prev, panelAlpha);
            UISurface.Card(grid.Panel, WithAlpha(theme.surfaceBase, 0.88f), theme.surfaceBorder);
            for (int i = 0; i < lines.Count; i++)
            {
                float a = BattleVictoryLayout.RewardAlpha(shownSeconds, i);
                if (a <= 0.001f) continue;
                RewardLine line = lines[i];
                Rect cell = BattleVictoryLayout.Cell(grid, i, lines);
                cell.y += (1f - a) * BattleVictoryLayout.RewardRise;
                Color tone = RewardColor(line.Tone);
                GUI.color = WithAlpha(prev, a);
                UISurface.Rounded(cell, line.Wide ? Color.Lerp(theme.surfaceCard, tone, 0.18f) : theme.surfaceCard);
                float dot = 18f;
                UIShapes.Ellipse(new Rect(cell.x + 16f, cell.center.y - dot * 0.5f, dot, dot), tone);
                rowStyle.fontSize = line.Wide ? BattleVictoryLayout.WideRowFont : BattleVictoryLayout.RowFont;
                rowStyle.alignment = line.Wide ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
                rowStyle.normal.textColor = line.Wide ? Color.Lerp(tone, theme.textPrimary, 0.2f) : theme.textPrimary;
                Rect textRect = line.Wide
                    ? new Rect(cell.x + 44f, cell.y, cell.width - 88f, cell.height)
                    : new Rect(cell.x + 44f, cell.y, cell.width - 56f, cell.height);
                // 곤충·아이템 이름을 데이터가 정한다 — 넘치면 글자를 줄여 맞춘다.
                UIHelper.LabelFit(textRect, line.Text, rowStyle);
            }
            GUI.color = prev;
        }

        /// <summary>맨 아래 「눌러서 계속」 알약 — 닫을 수 있을 때부터 천천히 깜빡인다.</summary>
        internal static void ContinueHint(HudFrame f, bool canClose, float sinceCanClose, bool reducedMotion)
        {
            float alpha = BattleVictoryLayout.HintAlpha(canClose, sinceCanClose, reducedMotion);
            if (alpha <= 0.001f) return;
            EnsureStyles();
            UITheme theme = UITheme.Instance;
            Rect rect = BattleVictoryLayout.HintRect(f);
            Color prev = GUI.color;
            GUI.color = WithAlpha(prev, alpha);
            UISurface.Card(rect, WithAlpha(theme.surfaceBase, 0.85f), theme.accentMint);
            hintStyle.fontSize = BattleVictoryLayout.HintFont;
            hintStyle.normal.textColor = theme.textPrimary;
            UIHelper.LabelFit(new Rect(rect.x + 12f, rect.y + 4f, rect.width - 24f, rect.height - 8f), BattleVictoryLayout.ContinueHint, hintStyle);
            GUI.color = prev;
        }

        /// <summary>패배·도주 창 안의 「눌러서 계속」 한 줄(판 없이 글자만) — 창의 예전 안내 자리에.</summary>
        internal static void ContinueHintLine(Rect rect, bool canClose, float sinceCanClose, bool reducedMotion)
        {
            float alpha = BattleVictoryLayout.HintAlpha(canClose, sinceCanClose, reducedMotion);
            if (alpha <= 0.001f) return;
            EnsureStyles();
            Color prev = GUI.color;
            GUI.color = WithAlpha(prev, alpha);
            hintStyle.fontSize = 24;
            hintStyle.normal.textColor = UITheme.Instance.textPrimary;
            UIHelper.LabelFit(rect, BattleVictoryLayout.ContinueHint, hintStyle);
            hintStyle.fontSize = BattleVictoryLayout.HintFont;
            GUI.color = prev;
        }
    }
}
