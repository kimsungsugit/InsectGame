using InsectGame.Battle;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 야생 전투의 <b>낮·밤·날씨 보정 칩</b> — 양쪽 HP 카드 바로 아래에 작게, 유리하면 민트 "밤 · 야행성 +10%",
    /// 불리하면 코랄 "비 · 비를 싫어함 −10%".
    ///
    /// 배수와 문구는 컨트롤러가 든다(<see cref="InsectBattleController.PlayerEnvironment"/>·<see cref="InsectBattleController.EnemyEnvironment"/>,
    /// 규칙은 <see cref="BattleEnvironment"/>). 여기는 그리기만 한다. 보정이 없으면(<see cref="BattleEnvironmentNote.HasEffect"/> 거짓 —
    /// 대결·수문장·샌드박스·레이드·실내) <b>아무것도 그리지 않고 자리도 비우지 않는다</b> — 아래에 붙는 대결 말풍선은
    /// 칩이 있을 때만 한 칸 내려간다(<see cref="BattleHudStack.BubbleTop"/>).
    ///
    /// 처음 뜰 때 한 번 살짝 커졌다 돌아온다(전투 시작·교체 — 처음 보는 표식이라 눈길을 한 번 끈다). 줄인 움직임 설정이면 하지 않는다.
    /// 모놀리스 본체를 키우지 않으려고 partial로 뗐다. 본체는 <c>DrawHpBars</c> 끝에서 한 번 부른다.
    /// </summary>
    public partial class BattleScreenUI
    {
        private const int EnvironmentChipFont = 20;

        /// <summary>한쪽 칩의 사본 — 문구가 바뀔 때(새 전투·교체)만 문자열을 잇고 폭을 잰다.</summary>
        private sealed class EnvironmentChipCache
        {
            public InsectBattleStats Owner;
            public string Reason;
            public int Percent;
            public string Label;
            public float TextWidth;
            public float ShownAt;
        }

        private readonly EnvironmentChipCache playerEnvChip = new EnvironmentChipCache();
        private readonly EnvironmentChipCache enemyEnvChip = new EnvironmentChipCache();
        private GUIStyle envChipStyle;
        private GUIContent envChipMeasure;

        /// <summary>HP 카드 아래의 보정 칩 둘. 상대 쪽은 장부 게이지가 있으면 그 아래.</summary>
        private void DrawEnvironmentChips(Rect playerCard, Rect enemyCard)
        {
            if (battleController == null) return;
            BattleEnvironmentNote player = battleController.PlayerEnvironment;
            BattleEnvironmentNote enemy = battleController.EnemyEnvironment;
            if (!player.HasEffect && !enemy.HasEffect) return;

            EnsureEnvironmentChipStyle();
            bool ledger = LedgerPressure.IsActive(battleController.LedgerThreshold);
            if (player.HasEffect) DrawEnvironmentChip(playerCard, true, false, player, playerStats, playerEnvChip);
            if (enemy.HasEffect) DrawEnvironmentChip(enemyCard, false, ledger, enemy, enemyStats, enemyEnvChip);
        }

        private void EnsureEnvironmentChipStyle()
        {
            if (envChipStyle != null) return;
            envChipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = EnvironmentChipFont,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            envChipMeasure = new GUIContent();
        }

        private void DrawEnvironmentChip(Rect card, bool isPlayer, bool ledgerBelow, BattleEnvironmentNote note,
            InsectBattleStats owner, EnvironmentChipCache cache)
        {
            bool favorable = note.Multiplier > 1f;
            int percent = note.Percent;
            // 값으로 비교한다 — 컨트롤러가 문구를 매번 새로 이어 돌려줘도 칩이 매 프레임 다시 뜨지 않게.
            if (!ReferenceEquals(cache.Owner, owner) || cache.Percent != percent || !string.Equals(cache.Reason, note.Reason))
            {
                cache.Owner = owner;
                cache.Reason = note.Reason;
                cache.Percent = percent;
                cache.Label = BattleHudStack.EnvironmentLabel(note.Reason, percent, favorable);
                envChipStyle.fontSize = EnvironmentChipFont;
                envChipMeasure.text = cache.Label;
                cache.TextWidth = envChipStyle.CalcSize(envChipMeasure).x;
                cache.ShownAt = Time.unscaledTime;
            }

            Rect chip = BattleHudStack.EnvironmentChip(card, isPlayer, ledgerBelow, cache.TextWidth);
            float pad = BattleHudStack.EnvironmentChipPadding;
            // 글자 상자는 크기를 고정한다 — 펄스 동안 상자 크기가 매 프레임 달라지면 LabelFit 캐시가 프레임마다 새 항목으로 찬다.
            Rect text = new Rect(chip.x + pad, chip.y, chip.width - pad * 2f, chip.height);
            Rect surface = chip;
            float scale = BattleHudStack.EnvironmentPulse(Time.unscaledTime - cache.ShownAt, BattlePresentation.ReducedMotion);
            if (scale > 1.0001f)
            {
                // 바깥 윗모서리를 축으로 표면만 키운다 — 위의 카드·장부 게이지를 덮지 않고, 카드 폭 밖으로 나가지 않는다.
                float w = Mathf.Min(chip.width * scale, card.width);
                surface = new Rect(isPlayer ? chip.x : chip.xMax - w, chip.y, w, chip.height * scale);
                text.center = surface.center;
            }

            UITheme theme = UITheme.Instance;
            Color accent = favorable ? theme.accentMint : theme.accentCoral;
            UISurface.Chip(surface, string.Empty, Color.Lerp(theme.surfaceBase, accent, 0.2f), accent);
            envChipStyle.fontSize = EnvironmentChipFont;
            envChipStyle.normal.textColor = Color.Lerp(accent, theme.textPrimary, 0.2f);
            // 문구 길이는 데이터(성향·시간대·날씨)가 정한다 — 칩이 카드 폭에 걸리면 글자를 줄여 맞춘다.
            UIHelper.LabelFit(text, cache.Label, envChipStyle);
        }
    }

    /// <summary>
    /// 전투 HP 카드 아래에 붙는 것들의 세로 쌓기 — <b>순수 계산</b>. 위에서부터 장부 게이지(상대 쪽, 걸렸을 때) →
    /// 낮·밤·날씨 칩(보정이 있을 때) → 대결 말풍선(상대 쪽, 대결일 때). 없는 것은 자리를 차지하지 않는다.
    /// </summary>
    internal static class BattleHudStack
    {
        internal const float LedgerGaugeHeight = 26f;
        internal const float EnvironmentChipHeight = 40f;
        internal const float EnvironmentChipPadding = 14f;
        internal const float EnvironmentChipMinWidth = 120f;

        internal const float PulseDelay = 0.25f;
        internal const float PulseSeconds = 0.8f;
        internal const float PulseAmount = 0.1f;

        /// <summary>환경 칩의 윗변 — 카드 바로 아래, 장부 게이지가 있으면 그 아래.</summary>
        internal static float EnvironmentChipTop(Rect card, bool ledgerBelow)
        {
            float top = card.yMax + UITheme.Space.S;
            return ledgerBelow ? top + LedgerGaugeHeight + UITheme.Space.S : top;
        }

        /// <summary>
        /// 환경 칩 — 글자 폭에 맞춘 작은 칩을 카드의 바깥 모서리 쪽에 붙인다(내 곤충은 왼쪽, 상대는 오른쪽 끝 맞춤).
        /// 폭은 카드 폭을 넘지 않는다 — 넘칠 문구는 그리는 쪽이 <c>UIHelper.LabelFit</c>으로 줄인다.
        /// </summary>
        internal static Rect EnvironmentChip(Rect card, bool player, bool ledgerBelow, float textWidth)
        {
            float w = Mathf.Clamp(textWidth + EnvironmentChipPadding * 2f + 4f, EnvironmentChipMinWidth, Mathf.Max(1f, card.width));
            return new Rect(player ? card.x : card.xMax - w, EnvironmentChipTop(card, ledgerBelow), w, EnvironmentChipHeight);
        }

        /// <summary>대결 말풍선의 윗변 — 장부 게이지·환경 칩이 있으면 그 아래.</summary>
        internal static float BubbleTop(Rect card, bool ledgerBelow, bool environmentChip)
        {
            float top = EnvironmentChipTop(card, ledgerBelow);
            return environmentChip ? top + EnvironmentChipHeight + UITheme.Space.S : top;
        }

        /// <summary>
        /// 처음 뜬 뒤 <paramref name="sinceShown"/>초의 크기 배율. 잠깐 기다렸다가 한 번 살짝(최대 +10%) 커졌다 돌아오고, 그 뒤로는 1이다.
        /// </summary>
        internal static float EnvironmentPulse(float sinceShown, bool reducedMotion)
        {
            if (reducedMotion) return 1f;
            float u = (sinceShown - PulseDelay) / PulseSeconds;
            if (u <= 0f || u >= 1f) return 1f;
            return 1f + PulseAmount * Mathf.Sin(u * Mathf.PI);
        }

        /// <summary>
        /// "밤 · 야행성 +10%" / "비 · 비를 싫어함 −10%". 부호는 배수의 방향에서 정한다(퍼센트가 반올림으로 0이 돼도 방향은 남는다).
        /// 빼기는 하이픈이 아니라 마이너스 기호(U+2212)다 — 더하기와 폭·높이가 맞는다.
        /// </summary>
        internal static string EnvironmentLabel(string reason, int percent, bool favorable)
        {
            string amount = (favorable ? "+" : "−") + Mathf.Abs(percent) + "%";
            return string.IsNullOrEmpty(reason) ? amount : reason + " " + amount;
        }
    }
}
