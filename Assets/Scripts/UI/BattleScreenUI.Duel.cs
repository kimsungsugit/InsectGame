using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 1v1 전투의 <b>상대가 누구인가</b>를 보여 주는 연출 — 대결 컷인, 전투 중 한마디, 결과 한마디,
    /// 그리고 수문장 컷인.
    ///
    /// 예전엔 관장과의 마지막 대결도, 초원의 수호자도, 필드의 잡곤충과 똑같이 "○○와 마주쳤다"
    /// 한 줄로 시작하고 끝까지 아무 말이 없었다. 대사는 <see cref="DuelBanter"/>가 들고, 누구와 싸우는지는
    /// 컨트롤러의 <c>DuelOpponentId</c>·<c>EnemyGuardianRegionId</c>(시작 시점 스냅샷)를 읽는다.
    ///
    /// 모놀리스 본체를 키우지 않으려고 partial로 떼었다. 본체는 네 군데서 부른다 —
    /// 시작(<see cref="ResetDuelPresentation"/>), 인트로 길이(<see cref="IntroSeconds"/>),
    /// Update(<see cref="TickDuelBanter"/>), OnGUI(컷인·말풍선·결과 한마디).
    /// </summary>
    public partial class BattleScreenUI
    {
        private const float PlainIntroSeconds = 2.0f;
        /// <summary>컷인 길이 — 전투 배속을 따른다(인트로 타이머가 배속 시간이다).</summary>
        private const float CutInSeconds = 3.2f;
        /// <summary>전투 중 말풍선 — 읽는 시간이라 배속과 무관하게 실제 초로 센다.</summary>
        private const float BubbleSeconds = 2.8f;
        private const float TauntDelay = 0.7f;

        private string duelKey = string.Empty;
        private bool hasDuelLines;
        private DuelBanter.Lines duelLines;
        private DuelBanter.Tracker duelTracker;
        private string guardianKey = string.Empty;
        private RegionData guardianRegion;
        private string bubbleText;
        private float bubbleShownAt = -99f;

        private bool duelStylesReady;
        private GUIStyle cutInNameStyle;
        private GUIStyle cutInTitleStyle;
        private GUIStyle cutInSubStyle;
        private GUIStyle cutInVsStyle;
        private GUIStyle cutInLineStyle;
        private GUIStyle bubbleNameStyle;
        private GUIStyle bubbleLineStyle;

        private bool HasCutIn => hasDuelLines || guardianRegion != null;
        private float IntroSeconds => HasCutIn ? CutInSeconds : PlainIntroSeconds;

        /// <summary>새 전투 — 앞 전투의 상대·말풍선·순간 기록을 버린다.</summary>
        private void ResetDuelPresentation()
        {
            duelKey = string.Empty;
            hasDuelLines = false;
            duelLines = default;
            duelTracker = default;
            guardianKey = string.Empty;
            guardianRegion = null;
            bubbleText = null;
            bubbleShownAt = -99f;
        }

        /// <summary>
        /// 컨트롤러의 상대 정보를 따라간다. <b>시작 콜백에서 한 번 읽으면 안 된다</b> — 간부전은
        /// <c>StartDuel</c>(여기서 인트로가 선다) <i>다음에</i> <c>SetDuelOpponent</c>가 불리므로
        /// 그 시점엔 아직 빈 값이다. 매 프레임 사본과 비교해 바뀌었을 때만 표를 다시 찾는다.
        /// </summary>
        private void SyncDuelContext()
        {
            if (battleController == null) return;
            string key = battleController.DuelOpponentId ?? string.Empty;
            if (key != duelKey)
            {
                duelKey = key;
                hasDuelLines = DuelBanter.TryGet(key, out duelLines);
                duelTracker = default;
            }
            string region = battleController.EnemyGuardianRegionId ?? string.Empty;
            if (region != guardianKey)
            {
                guardianKey = region;
                guardianRegion = null;
                if (region.Length > 0)
                {
                    if (cachedRegionMgr == null) cachedRegionMgr = FindFirstObjectByType<RegionManager>();
                    if (cachedRegionMgr != null) guardianRegion = cachedRegionMgr.GetRegionById(region);
                    // 리전 관리자가 없는 씬(QA 캡처 빌드)에서는 정의표에서 찾는다 — 수문장 이름은 정의에 박혀 있다.
                    if (guardianRegion == null) guardianRegion = FindRegionDefinition(region);
                }
            }
        }

        private static RegionData FindRegionDefinition(string regionId)
        {
            foreach (RegionData r in RegionDefinitions.CreateAll())
                if (r != null && r.regionId == regionId) return r;
            return null;
        }

        /// <summary>HP가 선을 넘는 순간 상대가 한마디 한다. 보이는 막대(display)를 기준으로 — 맞는 순간과 맞춘다.</summary>
        private void TickDuelBanter()
        {
            SyncDuelContext();
            if (!hasDuelLines || resultShown || playerStats == null || enemyStats == null) return;
            if (phase == Phase.None || phase == Phase.Intro) return;
            // 앞 말풍선이 떠 있는 동안은 다음 순간을 미룬다 — 겹치면 둘 다 못 읽는다.
            if (bubbleText != null && Time.unscaledTime - bubbleShownAt < BubbleSeconds) return;
            float enemyRatio = enemyStats.MaxHp > 0 ? displayEnemyHp / enemyStats.MaxHp : 1f;
            float playerRatio = playerStats.MaxHp > 0 ? displayPlayerHp / playerStats.MaxHp : 1f;
            string line = DuelBanter.LineFor(duelLines, DuelBanter.Next(ref duelTracker, enemyRatio, playerRatio));
            if (string.IsNullOrEmpty(line)) return;
            bubbleText = line;
            bubbleShownAt = Time.unscaledTime;
        }

        private void EnsureDuelStyles()
        {
            if (duelStylesReady) return;
            duelStylesReady = true;
            cutInNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 58, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            cutInTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            cutInSubStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleLeft };
            cutInVsStyle = new GUIStyle(GUI.skin.label) { fontSize = 110, fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter };
            cutInLineStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true };
            bubbleNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            bubbleLineStyle = new GUIStyle(GUI.skin.label) { fontSize = 27, alignment = TextAnchor.MiddleLeft, wordWrap = true, richText = true };
        }

        /// <summary>
        /// 대결·수문장 컷인. 그렸으면 true(평범한 인트로를 건너뛴다).
        /// 가운데 띠를 반으로 갈라 왼쪽은 나, 오른쪽은 상대 — 양쪽이 밀려 들어와 VS에서 부딪히고,
        /// 상대의 첫마디가 타자로 뜬다. 수문장은 사람이 아니라 곤충끼리 선다.
        /// </summary>
        private bool DrawDuelCutIn()
        {
            SyncDuelContext();
            if (!HasCutIn || playerStats == null || enemyStats == null) return false;
            EnsureDuelStyles();

            UITheme theme = UITheme.Instance;
            bool portrait = UIScale.IsPortrait;
            bool reduced = BattlePresentation.ReducedMotion;
            float t = introTimer;
            float sw = UIScale.VirtualScreenWidth;
            float cx = sw * 0.5f;
            float fade = Mathf.Clamp01(t / 0.18f) * Mathf.Clamp01((CutInSeconds - t) / 0.3f);
            float slide = reduced ? 1f : 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.38f), 3f);
            float offset = (1f - slide) * sw * 0.55f;

            Color prev = GUI.color;
            Color faded = new Color(prev.r, prev.g, prev.b, prev.a * fade);
            GUI.color = faded;
            UISurface.Dim(0.68f);

            // 수문장은 사람이 아니라 띠에 글자만 선다(곤충은 뒤의 아레나에 이미 서 있다) — 띠를 낮춘다.
            bool guardian = !hasDuelLines;
            float bandH = portrait ? (guardian ? 520f : 760f) : (guardian ? 380f : 470f);
            Rect band = new Rect(0f, UISafeLayout.CenteredY(bandH), sw, bandH);
            UISurface.Flat(band, theme.surfaceBase);
            Color mint = theme.accentMint, coral = theme.accentCoral;
            UISurface.Flat(new Rect(-offset, band.y, cx, band.height), new Color(mint.r, mint.g, mint.b, 0.16f));
            UISurface.Flat(new Rect(cx + offset, band.y, cx, band.height), new Color(coral.r, coral.g, coral.b, 0.18f));
            UISurface.Flat(new Rect(0f, band.y, sw, 6f), theme.accentAmber);
            UISurface.Flat(new Rect(0f, band.yMax - 6f, sw, 6f), theme.accentAmber);
            // 가운데 사선 — 두 진영이 부딪히는 선. DrawRotatedLine은 GUI.color를 덮어쓰고 안 되돌린다 —
            // 알파를 실어 넘기고 바로 복원한다(안 그러면 뒤에 그리는 초상이 전부 호박색으로 물든다).
            Color amber = theme.accentAmber;
            DrawRotatedLine(cx + 70f, band.y + 6f, cx - 70f, band.yMax - 6f, 8f,
                new Color(amber.r, amber.g, amber.b, amber.a * faded.a));
            GUI.color = faded;

            float feet = band.yMax - 14f;
            float figX = portrait ? sw * 0.25f : sw * 0.16f;
            float midY = band.y + band.height * 0.5f - 90f;

            // ── 나 ──
            float px = figX - offset;
            if (!guardian) CharacterPortraitRenderer.DrawWithOutfit(px, feet - 74.5f * 2.5f, 2.5f);
            // 그림이 없으면 글자 묶음을 바깥쪽으로 벌린다 — 가운데로 모으면 긴 수문장 이름이 VS에 붙는다.
            Rect myText = portrait
                ? new Rect(40f - offset, band.y + 36f, cx - 80f, 150f)
                : guardian
                    ? new Rect(160f - offset, midY, cx - 400f, 180f)
                    : new Rect(px + 150f, midY, cx - px - 250f, 180f);
            DrawCutInBlock(myText, "나의 파트너", playerStats.Data.displayName, $"Lv.{playerStats.Level}", theme.accentMint, false);

            // ── 상대 ──
            float ex = sw - figX + offset;
            string title, name, sub;
            if (guardian)
            {
                title = $"{guardianRegion.displayName}의 수문장";
                name = string.IsNullOrEmpty(guardianRegion.guardianDisplayName) ? enemyStats.Data.displayName : guardianRegion.guardianDisplayName;
                sub = $"Lv.{enemyStats.Level}";
            }
            else
            {
                // 초상은 DuelBanterTests가 전원 보장한다(없으면 얼굴 없이 글자만 선다).
                NpcDialogueUI.TryDrawStoryPortrait(duelLines.npcId, ex, feet - 74.5f * 2.5f, 2.5f);
                title = duelLines.title;
                name = duelLines.name;
                sub = $"{enemyStats.Data.displayName}  Lv.{enemyStats.Level}";
            }
            // 세로는 가운데 사선이 윗부분에서 오른쪽(cx+70)으로 기운다 — 글자 묶음을 그 오른쪽에서 시작해야
            // 긴 이름("초원의 수호자 사마귀")의 첫 글자를 사선이 관통하지 않는다.
            Rect foeText = portrait
                ? new Rect(cx + 90f + offset, band.y + 36f, cx - 130f, 150f)
                : guardian
                    ? new Rect(cx + 240f + offset, midY, cx - 400f, 180f)
                    : new Rect(cx + 100f + offset, midY, ex - cx - 250f, 180f);
            DrawCutInBlock(foeText, title, name, sub, theme.accentCoral, true);

            // ── VS — 양쪽이 닿는 순간 크게 찍혔다가 자리를 잡는다. ──
            float punch = reduced ? 0f : Mathf.Clamp01(1f - Mathf.Abs(t - 0.45f) / 0.18f);
            cutInVsStyle.fontSize = Mathf.RoundToInt(110f + 60f * punch);
            cutInVsStyle.normal.textColor = theme.accentAmber;
            // 세로 수문장은 띠 위쪽이 글자 묶음이라 VS를 그 아래에 둔다.
            float vsY = !portrait ? band.y + band.height * 0.5f
                : guardian ? band.y + 370f : band.y + band.height * 0.5f + 40f;
            GUI.Label(new Rect(cx - 200f, vsY - 110f, 400f, 220f), "VS", cutInVsStyle);

            // ── 첫마디 ──
            string line = guardian ? "이 땅의 길을 지키는 자가 앞을 막아선다." : duelLines.intro;
            float lineT = t - TauntDelay;
            if (!string.IsNullOrEmpty(line) && lineT > 0f)
            {
                Rect plate = new Rect(cx - Mathf.Min(560f, sw * 0.5f - 32f), band.yMax + 22f, Mathf.Min(1120f, sw - 64f), 104f);
                UISurface.Card(plate, theme.surfaceRaised, guardian ? theme.surfaceBorder : theme.accentCoral);
                cutInLineStyle.fontStyle = guardian ? FontStyle.Italic : FontStyle.Normal;
                cutInLineStyle.normal.textColor = guardian ? theme.textSecondary : theme.textPrimary;
                string shown = guardian ? line : $"“{line}”";
                int visible = reduced ? shown.Length
                    : StoryDialogueStaging.VisibleChars(shown.Length, lineT * 1.4f, StoryDialogueStaging.LineFx.None);
                UIHelper.LabelFitReveal(new Rect(plate.x + 24f, plate.y + 8f, plate.width - 48f, plate.height - 16f),
                    shown, visible, cutInLineStyle);
            }
            GUI.color = prev;
            return true;
        }

        /// <summary>컷인 한쪽의 글자 묶음 — 칭호(작게·강조색) / 이름(크게) / 곤충·레벨.</summary>
        private void DrawCutInBlock(Rect r, string title, string name, string sub, Color accent, bool right)
        {
            TextAnchor anchor = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            cutInTitleStyle.alignment = anchor;
            cutInNameStyle.alignment = anchor;
            cutInSubStyle.alignment = anchor;
            cutInTitleStyle.normal.textColor = accent;
            cutInNameStyle.normal.textColor = UITheme.Instance.textPrimary;
            cutInSubStyle.normal.textColor = UITheme.Instance.textSecondary;
            UIHelper.LabelFit(new Rect(r.x, r.y, r.width, 42f), title, cutInTitleStyle);
            UIHelper.LabelFit(new Rect(r.x, r.y + 42f, r.width, 80f), name, cutInNameStyle);
            UIHelper.LabelFit(new Rect(r.x, r.y + 122f, r.width, 40f), sub, cutInSubStyle);
        }

        /// <summary>전투 중 한마디 — 상대 HP 카드 바로 아래(장부 게이지가 있으면 그 아래)에 잠깐 뜬다.</summary>
        private void DrawDuelBubble()
        {
            if (!hasDuelLines || string.IsNullOrEmpty(bubbleText) || resultShown) return;
            if (phase == Phase.Intro || phase == Phase.None) return;
            float age = Time.unscaledTime - bubbleShownAt;
            if (age >= BubbleSeconds) return;
            EnsureDuelStyles();

            Rect card = DuelHudLayout.HpCard(UISafeLayout.Content, false);
            float top = card.yMax + UITheme.Space.S;
            if (battleController != null && LedgerPressure.IsActive(battleController.LedgerThreshold))
                top += 26f + UITheme.Space.S;
            DrawSpeechCard(new Rect(card.x, top, card.width, 122f), duelLines, bubbleText, age,
                Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((BubbleSeconds - age) / 0.35f));
        }

        /// <summary>결과 화면 위 한마디 — 이기면 상대의 패배 대사, 지면 승리 대사. 도주는 말이 없다.</summary>
        private void DrawDuelResultQuote(Rect panel)
        {
            if (!hasDuelLines) return;
            if (battleController != null && battleController.DidEscape) return;
            string line = lastWon ? duelLines.defeat : duelLines.victory;
            if (string.IsNullOrEmpty(line)) return;
            EnsureDuelStyles();
            float y = Mathf.Max(UISafeLayout.ContentTop, panel.y - 138f);
            DrawSpeechCard(new Rect(panel.x, y, panel.width, 122f), duelLines, line, resultTimer,
                Mathf.Clamp01(resultTimer / 0.2f));
        }

        /// <summary>말풍선 카드 — 왼쪽에 작은 전신 초상, 오른쪽에 이름과 대사(타자).</summary>
        private void DrawSpeechCard(Rect r, DuelBanter.Lines who, string text, float age, float alpha)
        {
            UITheme theme = UITheme.Instance;
            Color prev = GUI.color;
            GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha);
            UISurface.Card(r, theme.surfaceRaised, theme.accentCoral);
            const float figScale = 0.72f;
            bool drewFace = NpcDialogueUI.TryDrawStoryPortrait(who.npcId, r.x + 58f, r.yMax - 8f - 74.5f * figScale, figScale);
            float textX = r.x + (drewFace ? 112f : 20f);
            float textW = r.xMax - textX - 16f;
            bubbleNameStyle.normal.textColor = theme.accentAmber;
            UIHelper.LabelFit(new Rect(textX, r.y + 8f, textW, 30f), who.name, bubbleNameStyle);
            bubbleLineStyle.normal.textColor = theme.textPrimary;
            int visible = BattlePresentation.ReducedMotion ? text.Length
                : StoryDialogueStaging.VisibleChars(text.Length, age, StoryDialogueStaging.LineFx.None);
            UIHelper.LabelFitReveal(new Rect(textX, r.y + 38f, textW, r.height - 46f), text, visible, bubbleLineStyle);
            GUI.color = prev;
        }
    }
}
