using System.Collections.Generic;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 레이드 화면의 <b>체감 화면</b>(4단계) — 일반 레이드 진입(화면 쓸기 + 아래 무대의 「레이드 보스 ○○ 출현!」), 타격 순간의 상성 표시,
    /// 전용기 행동 강조(사전 컷인 없이 행동 문구를 크게·속성색 테두리로), 승리 화면. 1대1(<c>BattleScreenUI.Feel</c>)과 같은 순수 계산·
    /// 그리기(<see cref="BattleFeelDraw"/>)를 쓴다. 수문장 레이드의 진입은 3단계 배너(<c>RaidBattleUI.Stage</c>)가 그대로 맡는다.
    ///
    /// 모놀리스(<c>RaidBattleUI.cs</c>·<c>.Draw.cs</c>)를 키우지 않으려고 partial로 뗐다. 본체는 <c>DrawIntro</c>(진입), <c>DrawAttackEffects</c>
    /// (상성·2D 전용기 이름), <c>DrawActionText</c>(전용기 강조), <c>DrawResult</c>(승리 화면)에서 부른다.
    /// </summary>
    public partial class RaidBattleUI
    {
        // ── 진입(일반 레이드) ──

        private string raidEntryTitle;
        private InsectData raidEntryFor;

        /// <summary>
        /// 일반 레이드 진입 — 처음 1.4초(인트로 시계)에 화면 쓸기, 그 위로 아래 무대 가운데에 「레이드 보스 ○○ 출현!」 + 「모두 힘을 합쳐 쓰러뜨리자!」.
        /// 위쪽(보스 자리)은 비운다 — 아레나 진입 샷(1초)이 보스 옆에서 팀 뒤로 돈다. 인트로 끝 0.25초에 사라지고 스킬 패널이 같은 자리를 이어받는다.
        /// </summary>
        private void DrawRaidEntry()
        {
            if (raidController == null || raidController.BossStats == null) return;
            HudFrame f = HudFrame.Current;
            bool reduced = BattlePresentation.ReducedMotion;
            float p = BattleReadPacing.EntryProgress(introTimer);
            BattleFeelDraw.EntryWipe(f, p, reduced);
            InsectData boss = raidController.BossStats.Data;
            if (raidEntryTitle == null || !ReferenceEquals(boss, raidEntryFor))
            {
                raidEntryFor = boss;
                raidEntryTitle = BattleEntryText.Raid(boss != null ? boss.displayName : null);
            }
            float alpha = BattleEntryStaging.TitleAlpha(p, introTimer, IntroSeconds, false);
            if (alpha <= 0.001f) return;
            float since = (p - BattleEntryStaging.TitleIn) * BattleReadPacing.EntryIntroSeconds;
            Color accent = boss != null ? UITheme.Instance.GetInsectRarityColor(boss.rarity) : UITheme.Instance.accentCoral;
            BattleFeelDraw.EntryTitle(BattleEntryLayout.RaidTitle(f), raidEntryTitle, BattleEntryText.RaidSubLine,
                BattleEntryLayout.RaidTitleFont, alpha, BattleEntryStaging.Pop(since, reduced), accent);
        }

        // ── 상성 ──

        /// <summary>타격 순간의 상성 표시 — 보통이면 없다. <paramref name="age"/>는 숫자가 뜬 뒤 초(튀어나오는 크기).</summary>
        private void DrawRaidImpactMatchup(Matchup m, float cx, float cy, float alpha, float age)
        {
            if (m == Matchup.Neutral) return;
            BattleFeelDraw.MatchupCaption(HudFrame.Current, cx, cy, m, alpha, MatchupHud.CaptionScale(age, BattlePresentation.ReducedMotion));
        }

        // ── 전용기 ──

        /// <summary>지금 화면이 보여 주는 행동 — 팀원 차례면 그 곤충의 행동, 보스 예고·공격이면 보스 행동. 합체공격은 null.</summary>
        private RaidActionResult ShownAction
        {
            get
            {
                if (phase == Phase.PlayerAttack) return lastMemberAction;
                if ((phase == Phase.BossAttack || phase == Phase.BossTelegraph) && activeRound != null) return activeRound.BossAction;
                return null;
            }
        }

        /// <summary>지금 보이는 팀원 행동이 전용기인가(2D 폴백의 기술 이름 강조).</summary>
        private bool ShownActionSignature
        {
            get
            {
                RaidActionResult a = ShownAction;
                return a != null && a.IsSignature;
            }
        }

        private string actionStyleFor;
        private bool actionStyleSignature;
        private Color actionStyleElement;

        /// <summary>
        /// 행동 문구 띠가 전용기 행동의 것인가 — 문구가 바뀌는 순간(그 행동의 연출 첫 프레임) 한 번 정해 둔다. 띠는 단계가 넘어간 뒤에도
        /// 잠깐 남는데(1.6~2초), 그때 단계로 다시 물으면 같은 문구가 도중에 보통 모양으로 바뀐다.
        /// </summary>
        private bool ActionTextIsSignature(out Color element)
        {
            if (!ReferenceEquals(actionStyleFor, actionText))
            {
                actionStyleFor = actionText;
                RaidActionResult a = ShownAction;
                actionStyleSignature = a != null && a.IsSignature;
                actionStyleElement = GetElementColor(a != null ? a.Element : InsectElement.Bug);
            }
            element = actionStyleElement;
            return actionStyleSignature;
        }

        /// <summary>
        /// 2D 폴백(아레나 없음)의 전용기 이름 — 보스 위에 크게, 속성색 테두리 판 + 「전용기!」 배지. 3D는 시전자가 말풍선으로 외치고
        /// 행동 문구 띠가 강조된다(<see cref="ActionTextIsSignature"/>).
        /// </summary>
        private void DrawRaidSignatureName2D(float cx, float top, float alpha)
        {
            if (alpha <= 0.001f || string.IsNullOrEmpty(lastSkillUsedName)) return;
            RaidActionResult a = ShownAction;
            Color element = GetElementColor(a != null ? a.Element : InsectElement.Bug);
            UITheme theme = UITheme.Instance;
            Rect plate = new Rect(cx - 230f, top, 460f, 64f);
            Color prev = GUI.color;
            GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha);
            UISurface.Card(plate, theme.surfaceBase, element);
            attackSkillNameStyleCache.fontSize = 40;
            attackSkillNameStyleCache.normal.textColor = SkillUILayout.GetReadableAccent(element);
            // 기술 이름을 데이터가 정한다 — 넘치면 글자를 줄여 맞춘다.
            UIHelper.LabelFit(new Rect(plate.x + 16f, plate.y + 6f, plate.width - 32f, plate.height - 12f), lastSkillUsedName,
                attackSkillNameStyleCache);
            attackSkillNameStyleCache.fontSize = 32;
            GUI.color = prev;
            BattleFeelDraw.SignatureBadge(new Rect(plate.x, plate.y - UITheme.Space.XS - 40f, 140f, 40f), alpha);
        }

        // ── 승리 ──

        private List<RewardLine> raidVictoryLines;
        private InsectBattleStats raidVictoryFor;

        /// <summary>
        /// 레이드 승리 화면 — 1대1과 같은 결: 위쪽 큰 「레이드 승리!」, 가운데는 팀 전원 점프(아레나, <see cref="BattleFlourish.VictorySeconds"/>),
        /// 아래 보상 판(0.8초부터 한 줄씩 — 캔디·경험치·보너스·보스 포획), 맨 아래 「눌러서 계속」. 시계는 실제 초(<see cref="ResultShownSeconds"/>).
        /// 위쪽 보스 카드·팀 줄·합체 게이지는 이 동안 걷힌다.
        /// </summary>
        private void DrawRaidVictory()
        {
            HudFrame f = HudFrame.Current;
            bool reduced = BattlePresentation.ReducedMotion;
            float shown = ResultShownSeconds;
            UISurface.Dim(0.12f);
            BattleFeelDraw.VictoryTitle(f, BattleVictoryLayout.RaidTitle, shown, reduced);
            BattleFeelDraw.RewardPanel(f, RaidVictoryLines(), shown);
            BattleFeelDraw.ContinueHint(f, ResultCanClose, shown - BattleResultRules.InputLockSeconds, reduced);
        }

        private List<RewardLine> RaidVictoryLines()
        {
            InsectBattleStats boss = raidController != null ? raidController.BossStats : null;
            if (raidVictoryLines != null && ReferenceEquals(raidVictoryFor, boss)) return raidVictoryLines;
            raidVictoryFor = boss;
            string bossName = boss != null && boss.Data != null ? boss.Data.displayName : null;
            raidVictoryLines = BattleRewardLines.Raid(raidController != null ? raidController.RewardCandy : 0,
                raidController != null ? raidController.RewardExp : 0, bossName);
            return raidVictoryLines;
        }
    }
}
