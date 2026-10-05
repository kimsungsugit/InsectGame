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
        /// <summary>
        /// 컷인 길이(인트로 시계 초 = 1배속 실제 초). 진입 구간(<see cref="BattleReadPacing.EntryIntroSeconds"/>) <b>뒤에</b> 시작하고,
        /// 2배속이어도 실제 <see cref="BattleReadPacing.CutInMinSeconds"/> 밑으로는 줄지 않는다(BattleScreenUI.Flow).
        /// </summary>
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

        // 컷인의 "나" — 3D 마네킹(의상 창과 같은 렌더러). 없으면 2D 도트.
        private CharacterModelPreviewRenderer characterPreview;

        /// <summary>
        /// 결투 컷인의 "나"를 지금 장착한 3D로 그린다. 2D 도트는 모자·겉옷·도구 대부분을 못 그려
        /// 공들여 맞춘 옷이 대결 장면에서 사라졌다. 모놀리스 본체를 키우지 않게 이 partial에 둔다.
        /// </summary>
        public void AutoWire(CharacterModelPreviewRenderer preview)
        {
            if (characterPreview == null) characterPreview = preview;
        }

        /// <summary>컷인 속 내 모습 — 오른쪽(상대)을 3/4으로 바라본다. 발끝을 feet에 맞춘다.</summary>
        private void DrawMyCutInFigure(float centerX, float feet, float scale)
        {
            // 2D 기준: 몸 가운데 = feet − 74.5 × scale, 키 ≈ 2 × 74.5 × scale.
            float h = 2f * 74.5f * scale;
            Texture me = characterPreview != null ? characterPreview.GetEquippedPreview(140f) : null;
            if (me != null)
            {
                float w = h / CharacterModelPreviewRenderer.PreviewHeightPerWidth;
                GUI.DrawTexture(new Rect(centerX - w * 0.5f, feet - h, w, h), me, ScaleMode.ScaleToFit, true);
                return;
            }
            CharacterPortraitRenderer.DrawWithOutfit(centerX, feet - 74.5f * scale, scale);
        }

        private bool duelStylesReady;
        private GUIStyle cutInNameStyle;
        private GUIStyle cutInTitleStyle;
        private GUIStyle cutInSubStyle;
        private GUIStyle cutInVsStyle;
        private GUIStyle cutInLineStyle;
        private GUIStyle bubbleNameStyle;
        private GUIStyle bubbleLineStyle;

        private bool HasCutIn => hasDuelLines || guardianRegion != null;
        // 진입 구간(진입 샷·화면 쓸기·나타났다 문구) + [컷인 | 평범한 인트로의 나머지] — BattleReadPacing.IntroSeconds.
        private float IntroSeconds => BattleReadPacing.IntroSeconds(IntroCutInSeconds, PlainIntroSeconds);

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
            ResetEnemySendOut();
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

        /// <summary>
        /// HP가 선을 넘는 순간 상대가 한마디 한다. 보이는 막대(display)를 기준으로 — 맞는 순간과 맞춘다.
        /// 팀 대결이면 상대 HP 순간(흔들림·위기)은 <b>에이스</b>(마지막 곤충)에서만 — 첫 곤충이 절반 아래로 떨어졌다고
        /// 무너지는 소리를 하지 않게(<see cref="DuelBanter.EnemyMomentsAllowed"/>). 순번은 <b>화면에 서 있는</b> 곤충의 것이다
        /// (<see cref="ShownEnemyTeamIndex"/> — 쓰러지는 곤충의 막대가 줄어드는 동안 컨트롤러는 이미 다음 순번이다).
        /// 교체 문구가 떠 있는 동안은 말하지 않는다 — 가운데 한마디와 HP 카드 밑 말풍선이 겹친다.
        /// </summary>
        private void TickDuelBanter()
        {
            SyncDuelContext();
            TickEnemySendOut();
            if (sendOutShowing) return;
            if (!hasDuelLines || resultShown || playerStats == null || enemyStats == null) return;
            if (phase == Phase.None || phase == Phase.Intro || phase == Phase.EnemySwitch) return;
            // 앞 말풍선이 떠 있는 동안은 다음 순간을 미룬다 — 겹치면 둘 다 못 읽는다.
            if (bubbleText != null && Time.unscaledTime - bubbleShownAt < BubbleSeconds) return;
            float enemyRatio = enemyStats.MaxHp > 0 ? displayEnemyHp / enemyStats.MaxHp : 1f;
            float playerRatio = playerStats.MaxHp > 0 ? displayPlayerHp / playerStats.MaxHp : 1f;
            int teamSize = battleController != null ? battleController.EnemyTeamSize : 1;
            string line = DuelBanter.LineFor(duelLines,
                DuelBanter.Next(ref duelTracker, enemyRatio, playerRatio, teamSize, ShownEnemyTeamIndex));
            if (string.IsNullOrEmpty(line)) return;
            bubbleText = line;
            bubbleShownAt = Time.unscaledTime;
        }

        // ── 팀 대결 — 남은 곤충 공과 교체 문구 ──
        // 상대가 다음 곤충을 내보내는 교체 단계(Phase.EnemySwitch, 입력 없음) 동안 아래쪽 띠에 「집게가 지네를 내보냈다!」와
        // 그 밑에 상대의 교체 한마디(DuelBanter.SendOutLine)를 띄운다. 단계는 배속 시간 1.2초라 짧다 — 이어지는 「당신의 턴」
        // 배너(아래쪽 다른 자리)가 떠 있는 동안까지 남겨 읽을 시간을 번다. 자리는 DuelTeamHud.SendOut(순수 계산).

        private InsectBattleStats sendOutFor;
        private bool sendOutShowing;
        private float sendOutShownAt;
        private string sendOutTitle;
        private string sendOutLine;
        private GUIStyle sendOutTitleStyle;

        /// <summary>
        /// 화면에 서 있는 상대가 팀의 몇 번째인가. 컨트롤러는 쓰러지는 그 행동 안에서 이미 다음 순번으로 넘어갔지만(<c>EnemySwitched</c>)
        /// 화면은 교체 단계까지 쓰러지는 곤충을 그린다 — 그 사이는 한 칸 앞이다.
        /// </summary>
        private int ShownEnemyTeamIndex => battleController != null
            ? DuelTeamHud.ShownIndex(battleController.EnemyTeamIndex, pendingEnemySwitch)
            : 0;

        /// <summary>교체 단계에 들어서면 문구를 한 번 짓는다. 단계와 이어지는 턴 배너가 끝나면 내린다.</summary>
        private void TickEnemySendOut()
        {
            if (phase == Phase.EnemySwitch)
            {
                ComposeEnemySendOut();
                return;
            }
            if (sendOutShowing && (phase != Phase.TurnAnnounce || resultShown)) sendOutShowing = false;
        }

        // 들어온 곤충마다 한 번 — 문자열은 여기서만 만든다(OnGUI 매 패스 할당 방지).
        private void ComposeEnemySendOut()
        {
            if (enemyStats == null || ReferenceEquals(sendOutFor, enemyStats)) return;
            sendOutFor = enemyStats;
            string who = hasDuelLines && !string.IsNullOrEmpty(duelLines.name) ? duelLines.name : "상대";
            string insect = enemyStats.Data != null ? enemyStats.Data.displayName : "곤충";
            sendOutTitle = DuelTeamHud.SendOutText(who, insect);
            int incoming = battleController != null ? battleController.EnemyTeamIndex : 0;
            sendOutLine = hasDuelLines ? DuelBanter.SendOutLine(duelLines, incoming) : null;
            sendOutShowing = true;
            sendOutShownAt = Time.unscaledTime;
        }

        private void ResetEnemySendOut()
        {
            sendOutFor = null;
            sendOutShowing = false;
            sendOutTitle = null;
            sendOutLine = null;
        }

        /// <summary>
        /// 교체 문구 — 아래쪽 띠(행동 문구 자리 바로 위)에 큰 글자 한 줄, 그 밑에 상대의 한마디 카드. 3D 아레나 가운데(곤충이 들어서는
        /// 자리)는 비워 둔다. 0.15초에 걸쳐 떠오르고, 이어지는 「당신의 턴」 배너가 끝나 갈 때 함께 사라진다.
        /// </summary>
        private void DrawEnemySendOut()
        {
            if (phase == Phase.EnemySwitch) ComposeEnemySendOut();
            if (!sendOutShowing || resultShown || string.IsNullOrEmpty(sendOutTitle)) return;
            if (phase != Phase.EnemySwitch && phase != Phase.TurnAnnounce) return;
            EnsureDuelStyles();

            UITheme theme = UITheme.Instance;
            float age = Time.unscaledTime - sendOutShownAt;
            // 첫 프레임부터 0.35로 보인다(곧바로 진해진다) — 단계가 짧아(배속 1.2초) 첫 장면부터 읽혀야 한다.
            float alpha = Mathf.Lerp(DuelTeamHud.AppearAlpha, 1f, Mathf.Clamp01(age / 0.15f));
            if (phase == Phase.TurnAnnounce) alpha *= Mathf.Clamp01(announceTimer / 0.25f);
            if (alpha <= 0.001f) return;
            float rise = BattlePresentation.ReducedMotion ? 0f
                : (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.3f))) * DuelTeamHud.SendOutRise;

            bool hasLine = hasDuelLines && !string.IsNullOrEmpty(sendOutLine);
            DuelTeamHud.SendOutPlan plan = DuelTeamHud.SendOut(HudFrame.Current, hasLine);

            Color prev = GUI.color;
            GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha);
            Rect title = plan.Title;
            title.y += rise;
            UISurface.Card(title, theme.surfaceBase, theme.accentCoral);
            UISurface.Flat(new Rect(title.x + UITheme.Radius.Card, title.y + 3f, title.width - UITheme.Radius.Card * 2f, 5f),
                theme.accentCoral);
            sendOutTitleStyle.normal.textColor = theme.textPrimary;
            // 이름 둘을 데이터가 정한다("관장 하월이 이름 잃은 나방을 내보냈다!") — 넘치면 글자를 줄여 맞춘다.
            UIHelper.LabelFit(new Rect(title.x + 28f, title.y + 10f, title.width - 56f, title.height - 20f),
                sendOutTitle, sendOutTitleStyle);
            GUI.color = prev;

            if (hasLine)
            {
                Rect line = plan.Line;
                line.y += rise;
                // 한마디는 제목이 자리 잡은 뒤 타자로 시작한다.
                DrawSpeechCard(line, duelLines, sendOutLine, Mathf.Max(0f, age - 0.2f), alpha);
            }
        }

        /// <summary>
        /// 상대 HP 카드의 「상대 곤충」 글자 폭 — 팀 대결이면 오른쪽에 공 줄이 서므로 그 앞에서 멈춘다.
        /// </summary>
        private float EnemyCardLabelWidth(Rect card, float plainWidth)
        {
            if (battleController == null || !battleController.IsEnemyTeamBattle) return plainWidth;
            Rect row = DuelTeamHud.BallRow(card, battleController.EnemyTeamSize);
            return Mathf.Min(plainWidth, Mathf.Max(1f, row.x - (card.x + DuelTeamHud.CardTextInset) - UITheme.Space.S));
        }

        /// <summary>
        /// 상대 팀의 남은 곤충 — HP 카드 윗줄 오른쪽(레벨 앞)에 공 하나씩(●●○). 쓰러진 곤충은 흐린 회색, 지금 나와 있는 곤충은
        /// 호박색 고리를 두른다. 한 마리 대결이면 아무것도 그리지 않는다(카드가 예전과 같다).
        /// </summary>
        private void DrawEnemyTeamBalls(Rect card)
        {
            if (battleController == null || !battleController.IsEnemyTeamBattle) return;
            int count = battleController.EnemyTeamSize;
            Rect row = DuelTeamHud.BallRow(card, count);
            int shown = ShownEnemyTeamIndex;
            bool shownFainted = enemyStats != null && displayEnemyHp <= 0.01f;
            UITheme theme = UITheme.Instance;
            Color fainted = new Color(theme.textSecondary.r, theme.textSecondary.g, theme.textSecondary.b, 0.35f);
            for (int i = 0; i < count; i++)
            {
                Rect ball = DuelTeamHud.Ball(row, i, count);
                switch (DuelTeamHud.BallState(i, shown, shownFainted))
                {
                    case DuelTeamHud.TeamBall.Fainted:
                        UIShapes.Ellipse(ball, fainted);
                        break;
                    case DuelTeamHud.TeamBall.Current:
                        float ring = DuelTeamHud.CurrentRing;
                        UIShapes.Ellipse(new Rect(ball.x - ring, ball.y - ring, ball.width + ring * 2f, ball.height + ring * 2f),
                            theme.accentAmber);
                        UIShapes.Ellipse(ball, theme.accentCoral);
                        break;
                    default:
                        UIShapes.Ellipse(ball, theme.accentCoral);
                        break;
                }
            }
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
            sendOutTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = DuelTeamHud.SendOutFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
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

            // 컷인은 진입 구간(진입 샷·화면 쓸기·「○○이(가) 승부를 걸어왔다!」 — BattleScreenUI.Feel의 DrawEntry) 뒤에 시작한다.
            // 그동안은 아무것도 그리지 않는다(진입 문구는 진입 구간 끝에 사라져 이 컷인과 겹치지 않는다).
            float t = CutInClock;
            if (t < 0f) return true;
            UITheme theme = UITheme.Instance;
            bool portrait = UIScale.IsPortrait;
            bool reduced = BattlePresentation.ReducedMotion;
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
            if (!guardian) DrawMyCutInFigure(px, feet, 2.5f);
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

        /// <summary>전투 중 한마디 — 상대 HP 카드 바로 아래(장부 게이지·낮밤날씨 칩이 있으면 그 아래)에 잠깐 뜬다.</summary>
        private void DrawDuelBubble()
        {
            if (!hasDuelLines || string.IsNullOrEmpty(bubbleText) || resultShown) return;
            if (phase == Phase.Intro || phase == Phase.None || phase == Phase.EnemySwitch || sendOutShowing) return;
            float age = Time.unscaledTime - bubbleShownAt;
            if (age >= BubbleSeconds) return;
            EnsureDuelStyles();

            Rect card = DuelHudLayout.HpCard(UISafeLayout.Content, false);
            bool ledger = battleController != null && LedgerPressure.IsActive(battleController.LedgerThreshold);
            // 대결은 보정이 없어 칩이 서지 않는다 — 그래도 쌓는 순서는 한 곳(BattleHudStack)에서 정한다.
            bool environmentChip = battleController != null && battleController.EnemyEnvironment.HasEffect;
            DrawSpeechCard(BattleHudStack.Bubble(card, ledger, environmentChip), duelLines, bubbleText, age,
                Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((BubbleSeconds - age) / 0.35f));
        }

        /// <summary>결과 창(패배) 위 한마디 — 상대의 승리 대사. 도주는 말이 없다.</summary>
        private void DrawDuelResultQuote(Rect panel)
        {
            float y = Mathf.Max(UISafeLayout.ContentTop, panel.y - 138f);
            DrawDuelResultQuoteAt(new Rect(panel.x, y, panel.width, BattleHudStack.BubbleHeight));
        }

        /// <summary>
        /// 결과 한마디를 <paramref name="r"/>에 — 이기면 상대의 패배 대사, 지면 승리 대사. 도주는 말이 없다.
        /// 승리 화면은 「승리!」 바로 아래(<see cref="BattleVictoryLayout.QuoteRect"/>), 패배 창은 창 위에 세운다.
        /// </summary>
        private void DrawDuelResultQuoteAt(Rect r)
        {
            if (!hasDuelLines) return;
            if (battleController != null && battleController.DidEscape) return;
            string line = lastWon ? duelLines.defeat : duelLines.victory;
            if (string.IsNullOrEmpty(line)) return;
            EnsureDuelStyles();
            DrawSpeechCard(r, duelLines, line, resultTimer, Mathf.Clamp01(resultTimer / 0.2f));
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

    /// <summary>
    /// 1대1 <b>팀 대결</b>의 화면 자리와 문구 — <b>순수 계산</b>(테스트가 씬 없이 본다). 그리기는 <c>BattleScreenUI.Duel</c>.
    ///
    /// <b>남은 곤충 공</b>: 상대 HP 카드 윗줄 오른쪽, 레벨 글자 바로 앞에 오른쪽 맞춤으로 선다(지름 20 · 간격 8 — 다섯 마리 132px).
    /// 카드가 좁아 「상대 곤충」 글자 자리(100px)를 침범하면 공을 같은 비율로 줄인다. 카드 높이는 그대로라 아래 쌓기
    /// (장부 게이지·보정 칩·말풍선 — <c>BattleHudStack</c>)는 바뀌지 않는다.
    ///
    /// <b>교체 문구</b>: 아래쪽 행동 문구 띠(<c>DuelHudLayout.ActionBar</c>) 바로 위에 아래부터 한마디 카드(말풍선 높이 122) →
    /// 큰 글자 판(높이 96). 가로(1920×1080)면 판이 y≈672~768·폭 1100, 세로(1080×1920)면 y≈1486~1582·폭 = 안전 폭(1032).
    /// 3D 아레나의 가운데(새 곤충이 들어서는 자리)와 위쪽 HP 카드 쌓기·전투 문구 줄을 비켜 서고, 「당신의 턴」 배너
    /// (<c>DuelHudLayout.TurnBanner</c> — 바닥)와도 겹치지 않는다 — 그 배너가 떠 있는 동안까지 남아 있기 때문이다.
    /// </summary>
    internal static class DuelTeamHud
    {
        /// <summary>HP 카드 안 글자의 왼쪽 여백(<c>DrawHpBox</c>의 x + 18).</summary>
        internal const float CardTextInset = 18f;
        /// <summary>카드 오른쪽 위 「Lv. N」 자리(x + w − 102부터) + 간격 — 공 줄은 그 앞에서 끝난다.</summary>
        internal const float LevelReserve = 110f;
        /// <summary>윗줄(「상대 곤충」, y + 10 ~ 34)의 세로 가운데.</summary>
        internal const float RowCenterOffset = 22f;
        internal const float BallSize = 20f;
        internal const float BallGap = 8f;
        /// <summary>지금 나와 있는 곤충의 호박색 고리 두께.</summary>
        internal const float CurrentRing = 3f;
        /// <summary>「상대 곤충」 글자가 남겨 둘 최소 폭.</summary>
        internal const float MinLabelWidth = 100f;

        internal enum TeamBall { Waiting, Current, Fainted }

        /// <summary>
        /// 화면에 서 있는 상대의 순번. 컨트롤러는 쓰러진 그 행동 안에서 이미 다음 곤충으로 넘어갔고(<paramref name="controllerIndex"/>),
        /// 화면이 교체 단계에 들어가기 전(<paramref name="switchPending"/>)에는 아직 쓰러지는 곤충을 그린다 — 그때는 한 칸 앞이다.
        /// </summary>
        internal static int ShownIndex(int controllerIndex, bool switchPending) =>
            Mathf.Max(0, switchPending ? controllerIndex - 1 : controllerIndex);

        /// <summary>
        /// 공 하나의 상태 — 지금 서 있는 곤충보다 앞은 쓰러짐, 지금 곤충은 막대가 0이 되면 쓰러짐(막대와 같이 꺼진다), 뒤는 대기.
        /// </summary>
        internal static TeamBall BallState(int slot, int shownIndex, bool shownFainted)
        {
            if (slot < shownIndex) return TeamBall.Fainted;
            if (slot == shownIndex) return shownFainted ? TeamBall.Fainted : TeamBall.Current;
            return TeamBall.Waiting;
        }

        /// <summary>공 줄 전체 — 카드 윗줄 오른쪽(레벨 앞)에 오른쪽 맞춤. 높이 = 공 지름.</summary>
        internal static Rect BallRow(Rect card, int count)
        {
            int n = Mathf.Max(0, count);
            float right = card.xMax - LevelReserve;
            float minLeft = card.x + CardTextInset + MinLabelWidth;
            float size = BallSize, gap = BallGap;
            float need = n > 0 ? n * size + (n - 1) * gap : 0f;
            float room = Mathf.Max(0f, right - minLeft);
            if (need > room && need > 0f)
            {
                float k = room / need;
                size *= k;
                gap *= k;
                need = room;
            }
            return new Rect(right - need, card.y + RowCenterOffset - size * 0.5f, need, size);
        }

        /// <summary>줄 안의 <paramref name="index"/>번째 공(0부터, 왼쪽이 첫 곤충).</summary>
        internal static Rect Ball(Rect row, int index, int count)
        {
            float size = row.height;
            float gap = count > 1 ? (row.width - count * size) / (count - 1) : 0f;
            return new Rect(row.x + index * (size + gap), row.y, size, size);
        }

        // ── 교체 문구 ──

        internal const int SendOutFont = 46;
        internal const float SendOutTitleHeight = 96f;
        internal const float SendOutTitleMaxWidth = 1100f;
        internal const float SendOutLineMaxWidth = 900f;
        /// <summary>떠오를 때 아래에서 올라오는 거리 — 아래 행동 문구 띠와의 간격(16)보다 작게.</summary>
        internal const float SendOutRise = 12f;
        /// <summary>뜨는 첫 프레임의 불투명도 — 0에서 시작하면 단계의 첫 장면(QA 상태 캡처)에 글자가 없다.</summary>
        internal const float AppearAlpha = 0.35f;

        internal struct SendOutPlan
        {
            public Rect Title;
            /// <summary>한마디 카드(<see cref="HasLine"/>일 때만).</summary>
            public Rect Line;
            public bool HasLine;
        }

        /// <summary>교체 문구 자리 — 행동 문구 띠 바로 위에서 아래부터 한마디 카드 → 큰 글자 판. 가로 가운데.</summary>
        internal static SendOutPlan SendOut(HudFrame f, bool withLine)
        {
            Rect bar = DuelHudLayout.ActionBar(f);
            float bottom = bar.y - UITheme.Space.M;
            float cx = f.ContentLeft + f.ContentWidth * 0.5f;
            var plan = new SendOutPlan { HasLine = withLine };
            if (withLine)
            {
                float lw = Mathf.Min(SendOutLineMaxWidth, f.ContentWidth);
                plan.Line = new Rect(cx - lw * 0.5f, bottom - BattleHudStack.BubbleHeight, lw, BattleHudStack.BubbleHeight);
                bottom = plan.Line.y - UITheme.Space.S;
            }
            float tw = Mathf.Min(SendOutTitleMaxWidth, f.ContentWidth);
            plan.Title = new Rect(cx - tw * 0.5f, bottom - SendOutTitleHeight, tw, SendOutTitleHeight);
            return plan;
        }

        /// <summary>「집게가 지네를 내보냈다!」 — 조사는 받침으로 고른다(<see cref="KoreanJosa"/>).</summary>
        internal static string SendOutText(string trainer, string insect)
        {
            string who = string.IsNullOrEmpty(trainer) ? "상대" : trainer;
            string bug = string.IsNullOrEmpty(insect) ? "곤충" : insect;
            return $"{who}{KoreanJosa.IGa(who)} {bug}{KoreanJosa.EulReul(bug)} 내보냈다!";
        }
    }
}
