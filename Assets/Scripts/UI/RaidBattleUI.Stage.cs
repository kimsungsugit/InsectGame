using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 레이드의 <b>이야기 문구</b> — 수문장 등장 배너(인트로), 보스 변신 문구(변신 단계), 보스 이름 아래 「○○의 모습」.
    /// 모놀리스(<c>RaidBattleUI.cs</c>·<c>.Draw.cs</c>)를 키우지 않으려고 partial로 뗐다(<c>BattleScreenUI.Duel</c>과 같은 까닭).
    ///
    /// 위쪽은 보스 자리다 — 수문장 등장 카메라(visual-dev)가 보스를 화면 위 2/3에 잡고, 변신 연기도 거기서 핀다. 그래서 글은 전부
    /// <b>아래쪽</b>에 선다(자리는 <see cref="RaidStageLayout"/>, 순수 계산). 데이터는 읽기만 한다 — 별칭·등장 줄은
    /// <see cref="GuardianIntros"/>(game-designer), 변신 줄·모습은 <see cref="RaidBattleController"/>(battle-dev).
    /// 본체는 세 군데서 부른다 — <c>DrawIntro</c> 첫 줄, OnGUI의 변신 단계 분기, <c>DrawBossHpBar</c>·<c>DrawBossField</c>의 이름 아래.
    /// </summary>
    public partial class RaidBattleUI
    {
        private bool stageStylesReady;
        private GUIStyle bossFormCaptionStyle;
        private GUIStyle transformLineStyle;
        private GUIStyle guardianTitleStyle;
        private GUIStyle guardianEpithetStyle;
        private GUIStyle guardianNameStyle;
        private GUIStyle guardianLineStyle;
        private GUIContent guardianMeasure;

        private void EnsureStageStyles()
        {
            if (stageStylesReady) return;
            stageStylesReady = true;
            bossFormCaptionStyle = new GUIStyle(GUI.skin.label)
            { fontSize = RaidStageLayout.FormCaptionFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            transformLineStyle = new GUIStyle(GUI.skin.label)
            { fontSize = RaidStageLayout.TransformFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            guardianTitleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = RaidStageLayout.GuardianTitleFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            guardianEpithetStyle = new GUIStyle(GUI.skin.label)
            { fontSize = RaidStageLayout.GuardianEpithetFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            guardianNameStyle = new GUIStyle(GUI.skin.label)
            { fontSize = RaidStageLayout.GuardianNameFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            guardianLineStyle = new GUIStyle(GUI.skin.label)
            { fontSize = RaidStageLayout.GuardianLineFont, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };
            guardianMeasure = new GUIContent();
        }

        // ── 보스 이름 아래 「○○의 모습」 ──

        private InsectData formCaptionFor;
        private string formCaption;

        /// <summary>
        /// 지금 보여 줄 「○○의 모습」(원래 모습이면 null). 변신이 걸려 있지만 아직 연출 전이거나, 변신 단계에서 아레나가 모델을 아직
        /// 안 바꿨으면(<see cref="BattleStaging.TransformSwap"/> 전) <b>바뀌기 전</b> 모습이다 — 컨트롤러는 임계를 넘긴 그 행동에서 이미 모습을
        /// 바꿨는데, 이름표가 먼저 바뀌면 연기 속 모델과 이름이 어긋난다. 문자열은 모습이 바뀔 때만 만든다.
        /// </summary>
        private string BossFormCaption()
        {
            if (raidController == null) return null;
            RaidBossFormChange unrevealed = RaidStageLayout.UnrevealedChange(bossFormPending, pendingBossTransform,
                ActiveBossTransform, BossTransformProgress);
            InsectData shown = RaidStageLayout.ShownForm(raidController.BossFormIndex, raidController.BossFormData, unrevealed);
            if (shown == null) return null;
            if (!ReferenceEquals(shown, formCaptionFor) || formCaption == null)
            {
                formCaptionFor = shown;
                formCaption = RaidStageLayout.FormCaptionText(shown.displayName);
            }
            return formCaption;
        }

        private void DrawBossFormCaption(Rect rect, string caption, bool centered = false)
        {
            EnsureStageStyles();
            bossFormCaptionStyle.alignment = centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            bossFormCaptionStyle.normal.textColor = UITheme.Instance.accentAmber;
            UIHelper.LabelFit(rect, caption, bossFormCaptionStyle);
        }

        // ── 변신 문구 ──

        /// <summary>
        /// 변신 단계(<see cref="BossTransformDuration"/>, 입력 없음) 동안 아래 무대 가운데에 「그림자가 ○○의 모습을 빌렸다!」를 크게.
        /// 위쪽의 보스(연기·모델 교체 — visual-dev)는 가리지 않는다. 처음 0.12에 떠오르고 마지막 0.1에 사라진다(진행률 기준).
        /// 연기가 덮기 전부터 뜬다 — 단계가 배속 1.5초라 바뀌는 순간(<see cref="BattleStaging.TransformSwap"/>)에 띄우면 읽을 틈이 없다.
        /// </summary>
        private void DrawBossTransform()
        {
            string line = BossTransformLine;
            if (string.IsNullOrEmpty(line)) return;
            EnsureStageStyles();

            UITheme theme = UITheme.Instance;
            float p = BossTransformProgress;
            float alpha = Mathf.Lerp(RaidStageLayout.AppearAlpha, 1f, Mathf.Clamp01(p / 0.12f)) * Mathf.Clamp01((1f - p) / 0.1f);
            if (alpha <= 0.001f) return;
            float rise = BattlePresentation.ReducedMotion ? 0f
                : (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p / 0.2f))) * RaidStageLayout.BannerRise;

            Rect banner = RaidStageLayout.TransformBanner(HudFrame.Current);
            banner.y += rise;
            Color prev = GUI.color;
            GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha);
            UISurface.Card(banner, theme.surfaceBase, theme.accentCoral);
            UISurface.Flat(new Rect(banner.x + UITheme.Radius.Card, banner.y + 3f, banner.width - UITheme.Radius.Card * 2f, 5f),
                theme.accentCoral);
            transformLineStyle.normal.textColor = theme.textPrimary;
            // 새 모습이 연기를 뚫고 울부짖는 박자(BattleStaging.TransformRoar)에 글자가 한 번 "쿵" — 상자가 아니라 GUI 행렬로 키운다.
            Rect text = new Rect(banner.x + 28f, banner.y + 10f, banner.width - 56f, banner.height - 20f);
            float punch = BattlePresentation.ReducedMotion ? 1f
                : RaidStageLayout.RoarPunch((p - BattleStaging.TransformRoar) * BossTransformDuration);
            Matrix4x4 saved = GUI.matrix;
            if (punch > 1.0001f)
                GUI.matrix = saved * Matrix4x4.TRS(new Vector3(text.center.x, text.center.y, 0f), Quaternion.identity,
                    new Vector3(punch, punch, 1f)) * Matrix4x4.TRS(new Vector3(-text.center.x, -text.center.y, 0f),
                    Quaternion.identity, Vector3.one);
            // 빌린 곤충 이름을 데이터가 정한다 — 넘치면 글자를 줄여 맞춘다.
            UIHelper.LabelFit(text, line, transformLineStyle);
            GUI.matrix = saved;
            GUI.color = prev;
        }

        // ── 수문장 등장 배너 ──

        private string guardianIntroKey = string.Empty;
        private bool guardianIntroReady;
        private bool guardianHasIntro;
        private string guardianTitle;
        private string guardianEpithet;
        private string guardianName;
        private string guardianLine;
        private InsectElement guardianPrimary;
        private InsectElement guardianSecondary;
        private string guardianPrimaryLabel;
        private string guardianSecondaryLabel;
        private float guardianNameWidth = -1f;

        /// <summary>
        /// 수문장 레이드면 인트로 동안 아래쪽 배너를 그리고 true — 「초원의 수문장」(작게) / 별칭(크게) / 곤충 이름 + 속성 칩 / 등장 한 줄.
        /// 수문장이 아니면 false(예전 RAID BOSS → 이름 → FIGHT! 인트로가 그대로 돈다). 별칭 표에 없는 리전이면 칭호와 이름만 선다.
        /// 박자는 아레나의 등장 컷(<see cref="BattleStaging"/>)에 맞춘다 — 판은 아래에서 0.35초에 걸쳐 떠오르고, 별칭은 수문장이 포효하는
        /// 순간(<see cref="BattleStaging.GuardianRoarAt"/>) "쾅" 하고 찍히고, 등장 한 줄은 그 뒤에 들어온다. 인트로 끝 0.2초에 옅어지고
        /// 스킬 패널이 같은 자리를 이어받는다.
        /// </summary>
        private bool DrawGuardianIntro()
        {
            if (!PrepareGuardianIntro()) return false;
            EnsureStageStyles();

            UITheme theme = UITheme.Instance;
            HudFrame f = HudFrame.Current;
            float t = introTimer;
            bool reduced = BattlePresentation.ReducedMotion;
            float ease = reduced ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.35f));
            // 인트로 끝(IntroSeconds — 수문장은 BattleStaging.GuardianIntroSeconds) 0.2초 전부터 옅어진다. 스킬 패널이 같은 자리를 이어받는다.
            float alpha = Mathf.Lerp(RaidStageLayout.AppearAlpha, 1f, Mathf.Clamp01(t / 0.25f)) * Mathf.Clamp01((IntroSeconds - t) / 0.2f);
            if (alpha <= 0.001f) return true;
            float drop = (1f - ease) * RaidStageLayout.GuardianSlide;

            RaidStageLayout.GuardianPlan plan = RaidStageLayout.GuardianBanner(f, guardianHasIntro);
            Rect card = Shift(plan.Card, drop);

            Color prev = GUI.color;
            GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha);
            UISurface.Card(card, theme.surfaceBase, theme.accentAmber);
            UISurface.Flat(new Rect(card.x + UITheme.Radius.Card, card.y + 3f, card.width - UITheme.Radius.Card * 2f, 6f),
                theme.accentCoral);

            guardianTitleStyle.normal.textColor = theme.accentAmber;
            UIHelper.LabelFit(Shift(plan.Title, drop), guardianTitle, guardianTitleStyle);
            if (guardianHasIntro)
            {
                // 별칭은 수문장이 포효하는 박자(BattleStaging.GuardianRoarAt)에 "쾅" 하고 찍힌다 — 잠깐 컸다가 제 크기로.
                // 크기는 글자 상자가 아니라 GUI 행렬로 키운다(상자를 매 프레임 바꾸면 LabelFit 캐시가 프레임마다 새 항목으로 찬다).
                float sinceRoar = t - BattleStaging.GuardianRoarAt;
                float epithetAlpha = reduced ? 1f : Mathf.Clamp01(sinceRoar / 0.08f);
                if (epithetAlpha > 0.001f)
                {
                    float punch = reduced ? 1f : RaidStageLayout.EpithetPunch(sinceRoar);
                    Rect epithet = Shift(plan.Epithet, drop);
                    Matrix4x4 saved = GUI.matrix;
                    if (punch > 1.0001f)
                        GUI.matrix = saved * Matrix4x4.TRS(new Vector3(epithet.center.x, epithet.center.y, 0f), Quaternion.identity,
                            new Vector3(punch, punch, 1f)) * Matrix4x4.TRS(new Vector3(-epithet.center.x, -epithet.center.y, 0f),
                            Quaternion.identity, Vector3.one);
                    GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha * epithetAlpha);
                    guardianEpithetStyle.normal.textColor = theme.textPrimary;
                    UIHelper.LabelFit(epithet, guardianEpithet, guardianEpithetStyle);
                    GUI.matrix = saved;
                    GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha);
                }
            }

            // 이름 + 속성 칩 — 묶음째 가운데. 이름 폭은 한 번만 잰다(레이드 한 번 동안 바뀌지 않는다).
            if (guardianNameWidth < 0f)
            {
                guardianMeasure.text = guardianName;
                guardianNameWidth = guardianNameStyle.CalcSize(guardianMeasure).x;
            }
            float chip1 = RaidStageLayout.ChipWidth(guardianPrimaryLabel);
            float chip2 = guardianSecondaryLabel != null ? RaidStageLayout.ChipWidth(guardianSecondaryLabel) : 0f;
            RaidStageLayout.NameRow row = RaidStageLayout.LayoutNameRow(Shift(plan.NameRow, drop), guardianNameWidth, chip1, chip2);
            guardianNameStyle.normal.textColor = theme.textPrimary;
            UIHelper.LabelFit(row.Name, guardianName, guardianNameStyle);
            DrawGuardianChip(row.Chip1, guardianPrimaryLabel, guardianPrimary);
            if (chip2 > 0f) DrawGuardianChip(row.Chip2, guardianSecondaryLabel, guardianSecondary);

            if (guardianHasIntro)
            {
                // 등장 한 줄은 포효 뒤에 들어온다.
                float lineAlpha = reduced ? 1f : Mathf.Clamp01((t - BattleStaging.GuardianRoarAt - 0.2f) / 0.25f);
                GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha * lineAlpha);
                guardianLineStyle.normal.textColor = theme.textSecondary;
                UIHelper.LabelFit(Shift(plan.Line, drop), guardianLine, guardianLineStyle);
            }
            GUI.color = prev;
            return true;
        }

        private static Rect Shift(Rect r, float dy) => new Rect(r.x, r.y + dy, r.width, r.height);

        private void DrawGuardianChip(Rect rect, string label, InsectElement element)
        {
            if (rect.width <= 0f || string.IsNullOrEmpty(label)) return;
            UITheme theme = UITheme.Instance;
            Color elem = GetElementColor(element);
            UISurface.Chip(rect, label, Color.Lerp(theme.surfaceBase, elem, 0.35f), SkillUILayout.GetReadableAccent(elem));
        }

        /// <summary>
        /// 이번 레이드가 수문장전이면 배너 글을 한 번 짓는다(리전이 바뀔 때만). 칭호의 리전 이름·곤충 이름은 리전 정의에서,
        /// 별칭·등장 줄은 <see cref="GuardianIntros"/>에서 — 같은 말을 두 곳에 두지 않는다. 리전 관리자가 없는 씬(QA 캡처)에서는 정의표를 본다.
        /// </summary>
        private bool PrepareGuardianIntro()
        {
            string region = raidController != null ? raidController.BossGuardianRegionId : null;
            if (string.IsNullOrEmpty(region) || raidController.BossStats == null || raidController.BossStats.Data == null)
            {
                guardianIntroKey = string.Empty;
                guardianIntroReady = false;
                return false;
            }
            if (region == guardianIntroKey) return guardianIntroReady;

            guardianIntroKey = region;
            guardianNameWidth = -1f;
            RegionData data = FindGuardianRegion(region);
            InsectData boss = raidController.BossStats.Data;
            guardianTitle = data != null && !string.IsNullOrEmpty(data.displayName) ? $"{data.displayName}의 수문장" : "수문장";
            guardianName = data != null && !string.IsNullOrEmpty(data.guardianDisplayName) ? data.guardianDisplayName : boss.displayName;
            guardianHasIntro = GuardianIntros.TryGet(region, out GuardianIntros.Intro intro)
                               && !string.IsNullOrEmpty(intro.epithet);
            guardianEpithet = guardianHasIntro ? intro.epithet : null;
            guardianLine = guardianHasIntro ? intro.line : null;
            guardianPrimary = boss.primaryType;
            guardianSecondary = boss.secondaryType;
            guardianPrimaryLabel = InsectTypeChart.GetDisplayName(guardianPrimary);
            guardianSecondaryLabel = guardianSecondary != InsectElement.None && guardianSecondary != guardianPrimary
                ? InsectTypeChart.GetDisplayName(guardianSecondary)
                : null;
            guardianIntroReady = true;
            return true;
        }

        private RegionData FindGuardianRegion(string regionId)
        {
            if (cachedRegionMgr == null) cachedRegionMgr = FindFirstObjectByType<RegionManager>();
            RegionData found = cachedRegionMgr != null ? cachedRegionMgr.GetRegionById(regionId) : null;
            if (found != null) return found;
            foreach (RegionData r in RegionDefinitions.CreateAll())
                if (r != null && r.regionId == regionId) return r;
            return null;
        }
    }

    /// <summary>
    /// 레이드 화면의 자리 — <b>순수 계산</b>(테스트가 화면 여러 장으로 겹침을 잰다). 위쪽은 보스, 가운데(화면 53%)는 팀 패널 줄과
    /// 합체 게이지, 그 아래가 <b>아래 무대</b>다 — 스킬 패널이 서는 자리인데 인트로·변신 단계엔 비어 있어 이야기 문구가 선다.
    ///
    /// <b>변신 문구</b>: 아래 무대 가운데, 높이 112 · 폭 min(1200, 안전 폭). 가로(1920×1080) y≈842, 세로(1080×1920) y≈1472.
    /// <b>수문장 배너</b>: 안전 영역 바닥에 붙는다. 별칭이 있으면 높이 277(가로 y≈771 — 화면 위 71%까지 비운다, 세로 y≈1585), 없으면 139.
    /// 폭 min(1100, 안전 폭). 팀 패널 줄(인트로 동안 쉬는 자리)과 겹치지 않는다.
    /// </summary>
    public static class RaidStageLayout
    {
        /// <summary>한 줄이 안 잘리는 높이 — 한글 줄높이 ≈ 글자 × 1.35(rules/ui-layout.md).</summary>
        public static float LineHeight(int fontSize) => Mathf.Ceil(fontSize * 1.35f);

        // ── 고정 자리(그리는 쪽과 같은 식 — 여기가 단일 출처) ──

        public const float BossCardMaxWidth = 700f;
        public const float BossCardHeight = 100f;

        /// <summary>보스 HP 카드(<c>DrawBossHpBar</c>) — 위 가운데, 폭 min(700, 화면 70%).</summary>
        public static Rect BossCard(HudFrame f)
        {
            float w = Mathf.Min(BossCardMaxWidth, f.Width * 0.7f);
            return new Rect((f.Width - w) * 0.5f, f.ContentTop, w, BossCardHeight);
        }

        public const int FormCaptionFont = 20;
        /// <summary>이름 아래 줄의 왼쪽 몫 — 「○○의 모습」. 오른쪽 나머지에 ATK·DEF가 오른쪽 맞춤으로 선다.</summary>
        public const float FormCaptionShare = 0.58f;

        /// <summary>보스 카드 이름 아래 줄 왼쪽 — 「호랑나비의 모습」.</summary>
        public static Rect BossFormCaptionRow(Rect card) =>
            new Rect(card.x + 14f, card.y + 36f, (card.width - 28f) * FormCaptionShare, 24f);

        /// <summary>보스 카드 이름 아래 줄 오른쪽 — 모습 글자가 있을 때의 ATK·DEF(오른쪽 맞춤).</summary>
        public static Rect BossStatRow(Rect card)
        {
            float left = (card.width - 28f) * FormCaptionShare + UITheme.Space.S;
            return new Rect(card.x + 14f + left, card.y + 38f, Mathf.Max(1f, card.width - 28f - left), 20f);
        }

        /// <summary>2D 보스 그림(아레나가 꺼졌을 때) 이름 밑 「○○의 모습」 — 가운데 맞춤.</summary>
        public static Rect SpriteFormCaption(float centerX, float top) => new Rect(centerX - 170f, top, 340f, 30f);

        /// <summary>「호랑나비의 모습」.</summary>
        public static string FormCaptionText(string formName) =>
            string.IsNullOrEmpty(formName) ? null : formName + "의 모습";

        /// <summary>
        /// 아직 화면에 드러나지 않은 변신 — 걸려 있지만 연출 전(<paramref name="transformPending"/>)이면 그 변신, 변신 단계에서 아레나가 모델을
        /// 바꾸기 전(진행률 &lt; <see cref="BattleStaging.TransformSwap"/>)이면 지금 단계의 변신. 다 드러났으면 null.
        /// </summary>
        public static RaidBossFormChange UnrevealedChange(bool transformPending, RaidBossFormChange pending,
            RaidBossFormChange active, float transformProgress)
        {
            if (transformPending && pending != null) return pending;
            if (active != null && transformProgress < BattleStaging.TransformSwap) return active;
            return null;
        }

        /// <summary>
        /// 이름 아래에 보여 줄 모습(원래 모습이면 null). 아직 드러나지 않은 변신(<paramref name="unrevealed"/>)이 있으면 바뀌기 <b>전</b>
        /// 모습 — 그것도 원래 모습이었으면 null.
        /// </summary>
        public static InsectData ShownForm(int formIndex, InsectData formData, RaidBossFormChange unrevealed)
        {
            if (unrevealed != null) return unrevealed.FromIndex > 0 ? unrevealed.FromData : null;
            return formIndex > 0 ? formData : null;
        }

        /// <summary>보스 예고(<c>DrawBossIntent</c>) — 보스 카드 아래.</summary>
        public static Rect BossIntent(HudFrame f)
        {
            float w = Mathf.Min(680f, f.Width - 48f);
            return new Rect((f.Width - w) * 0.5f, f.ContentTop + 110f, w, f.Mobile ? 64f : 54f);
        }

        public const float TeamStripHeight = 104f;

        /// <summary>
        /// 팀 패널 줄이 쉬는 자리(스킬을 고를 때·일반 레이드 인트로) — 화면 53%, 안전 영역 안. 폭은 안전 폭 전체로 잡는다(겹침 검사용 띠).
        /// <c>RaidBattleUI.Impact.TeamStripY</c>의 쉬는 자리와 같은 식이다. 수문장 등장·그림자 변신·결과 동안은 줄이 숨는다(<see cref="RaidTeamStrip"/>) —
        /// 그때 이 자리에 떠서 보스 아랫부분과 변신 연기를 가렸다.
        /// </summary>
        public static Rect TeamStripRest(HudFrame f)
        {
            float y = Mathf.Clamp(f.Height * 0.53f, f.ContentTop, f.ContentBottom - TeamStripHeight);
            return new Rect(f.ContentLeft, y, f.ContentWidth, TeamStripHeight);
        }

        public const float UniteGaugeMaxWidth = 400f;
        public const float UniteGaugeHeight = 46f;

        /// <summary>합체 게이지(<c>DrawUniteGaugeBar</c>) — 팀 패널 줄 바로 아래.</summary>
        public static Rect UniteGauge(HudFrame f)
        {
            float w = Mathf.Min(UniteGaugeMaxWidth, f.ContentWidth);
            float x = f.ContentLeft + f.ContentWidth * 0.5f - w * 0.5f;
            float y = Mathf.Clamp(f.Height * 0.53f + 114f, f.ContentTop, f.ContentBottom - UniteGaugeHeight);
            return new Rect(x, y, w, UniteGaugeHeight);
        }

        /// <summary>아래 무대 — 합체 게이지 밑(간격 16)부터 안전 영역 바닥까지, 안전 폭 전체.</summary>
        public static Rect LowerStage(HudFrame f)
        {
            float top = UniteGauge(f).yMax + UITheme.Space.M;
            return new Rect(f.ContentLeft, top, f.ContentWidth, Mathf.Max(1f, f.ContentBottom - top));
        }

        /// <summary>떠오를 때 아래에서 올라오는 거리.</summary>
        public const float BannerRise = 12f;
        /// <summary>뜨는 첫 프레임의 불투명도 — 0에서 시작하면 단계의 첫 장면(QA 상태 캡처)에 글자가 없다.</summary>
        public const float AppearAlpha = 0.35f;

        // ── 변신 문구 ──

        public const int TransformFont = 46;
        public const float TransformBannerHeight = 112f;
        public const float TransformBannerMaxWidth = 1200f;

        /// <summary>변신 문구 판 — 아래 무대 가운데.</summary>
        public static Rect TransformBanner(HudFrame f)
        {
            Rect stage = LowerStage(f);
            float w = Mathf.Min(TransformBannerMaxWidth, f.ContentWidth);
            float h = Mathf.Min(TransformBannerHeight, stage.height);
            return new Rect(stage.x + (stage.width - w) * 0.5f, stage.y + (stage.height - h) * 0.5f, w, h);
        }

        // ── 수문장 등장 배너 ──

        public const int GuardianTitleFont = 30;
        public const int GuardianEpithetFont = 60;
        public const int GuardianNameFont = 38;
        public const int GuardianLineFont = 30;
        public const float GuardianMaxWidth = 1100f;
        public const float GuardianPadX = 32f;
        public const float GuardianPadY = 20f;
        public const float GuardianChipHeight = 44f;
        public const float GuardianChipPadding = 28f;
        /// <summary>칩 글자 한 자 폭 어림(칩 글자는 높이 × 0.52 ≈ 23pt) — 속성 이름은 1~3자라 재지 않고 어림한다.</summary>
        public const float GuardianChipCharWidth = 24f;
        /// <summary>떠오를 때 아래에서 올라오는 거리 — 바닥 마진의 최솟값(24) 이하라 미끄러지는 동안에도 화면 밖으로 나가지 않는다.</summary>
        public const float GuardianSlide = UISafeLayout.MinMargin;

        /// <summary>별칭이 포효 박자에 찍힐 때의 크기 — 카드 폭을 크게 넘지 않게 작게 둔다.</summary>
        public const float EpithetPunchPeak = 1.12f;
        public const float EpithetPunchSeconds = 0.22f;

        /// <summary>포효 뒤 <paramref name="sinceRoar"/>초의 별칭 배율 — 1.12에서 0.22초에 걸쳐 1로. 포효 전(보이지 않는다)은 1.12.</summary>
        public static float EpithetPunch(float sinceRoar)
        {
            if (sinceRoar <= 0f) return EpithetPunchPeak;
            if (sinceRoar >= EpithetPunchSeconds) return 1f;
            return Mathf.Lerp(EpithetPunchPeak, 1f, Mathf.SmoothStep(0f, 1f, sinceRoar / EpithetPunchSeconds));
        }

        /// <summary>변신 문구가 울부짖는 박자에 한 번 커지는 크기(이미 떠 있는 글자라 별칭보다 작게).</summary>
        public const float RoarPunchPeak = 1.07f;

        /// <summary>
        /// 울부짖은 뒤 <paramref name="sinceRoar"/>초의 변신 문구 배율 — 그 전엔 1(이미 읽히는 중이다), 울부짖는 순간 1.07로 튀었다가
        /// 0.22초에 걸쳐 1로.
        /// </summary>
        public static float RoarPunch(float sinceRoar)
        {
            if (sinceRoar < 0f || sinceRoar >= EpithetPunchSeconds) return 1f;
            return Mathf.Lerp(RoarPunchPeak, 1f, Mathf.SmoothStep(0f, 1f, sinceRoar / EpithetPunchSeconds));
        }

        public struct GuardianPlan
        {
            public Rect Card;
            public Rect Title;
            /// <summary>별칭(있을 때만).</summary>
            public Rect Epithet;
            /// <summary>곤충 이름 + 속성 칩 줄(<see cref="LayoutNameRow"/>로 나눈다).</summary>
            public Rect NameRow;
            /// <summary>등장 한 줄(있을 때만).</summary>
            public Rect Line;
            public bool HasIntro;
        }

        /// <summary>배너 높이 — 별칭·등장 줄이 있으면 넷, 없으면 칭호·이름 둘.</summary>
        public static float GuardianHeight(bool hasIntro)
        {
            float h = GuardianPadY + LineHeight(GuardianTitleFont) + UITheme.Space.XS;
            if (hasIntro) h += LineHeight(GuardianEpithetFont) + UITheme.Space.XS;
            h += Mathf.Max(LineHeight(GuardianNameFont), GuardianChipHeight);
            if (hasIntro) h += UITheme.Space.S + LineHeight(GuardianLineFont);
            return h + GuardianPadY;
        }

        /// <summary>수문장 배너 — 안전 영역 바닥에 붙는 카드 한 장, 줄은 위에서부터 칭호 · 별칭 · 이름 줄 · 등장 줄.</summary>
        public static GuardianPlan GuardianBanner(HudFrame f, bool hasIntro)
        {
            float w = Mathf.Min(GuardianMaxWidth, f.ContentWidth);
            Rect card = f.BottomPanel(w, GuardianHeight(hasIntro));
            float x = card.x + GuardianPadX;
            float tw = Mathf.Max(1f, card.width - GuardianPadX * 2f);
            float y = card.y + GuardianPadY;
            var plan = new GuardianPlan { Card = card, HasIntro = hasIntro };
            plan.Title = new Rect(x, y, tw, LineHeight(GuardianTitleFont));
            y = plan.Title.yMax + UITheme.Space.XS;
            if (hasIntro)
            {
                plan.Epithet = new Rect(x, y, tw, LineHeight(GuardianEpithetFont));
                y = plan.Epithet.yMax + UITheme.Space.XS;
            }
            plan.NameRow = new Rect(x, y, tw, Mathf.Max(LineHeight(GuardianNameFont), GuardianChipHeight));
            y = plan.NameRow.yMax;
            if (hasIntro)
                plan.Line = new Rect(x, y + UITheme.Space.S, tw, LineHeight(GuardianLineFont));
            return plan;
        }

        /// <summary>속성 칩 폭 — 글자 수 × 어림 폭 + 좌우 여백.</summary>
        public static float ChipWidth(string label) =>
            string.IsNullOrEmpty(label) ? 0f : label.Length * GuardianChipCharWidth + GuardianChipPadding;

        public struct NameRow
        {
            public Rect Name;
            public Rect Chip1;
            /// <summary>두 번째 속성 칩(없으면 폭 0).</summary>
            public Rect Chip2;
        }

        /// <summary>
        /// 이름 + 칩(하나 또는 둘)을 묶음째 줄 가운데에. 줄이 모자라면 이름 상자만 줄인다(그리기의 LabelFit이 글자를 줄여 맞춘다) —
        /// 칩은 속성이라 잘리면 안 된다.
        /// </summary>
        public static NameRow LayoutNameRow(Rect row, float nameWidth, float chip1Width, float chip2Width)
        {
            float gap = UITheme.Space.S;
            float chips = Mathf.Max(0f, chip1Width) + (chip2Width > 0f ? gap + chip2Width : 0f);
            float chipsBlock = chips > 0f ? gap + chips : 0f;
            float name = Mathf.Clamp(nameWidth, 1f, Mathf.Max(1f, row.width - chipsBlock));
            float total = name + chipsBlock;
            float x = row.x + (row.width - total) * 0.5f;
            float chipY = row.y + (row.height - GuardianChipHeight) * 0.5f;
            var r = new NameRow { Name = new Rect(x, row.y, name, row.height) };
            float cx = x + name + gap;
            r.Chip1 = chip1Width > 0f ? new Rect(cx, chipY, chip1Width, GuardianChipHeight) : new Rect(cx, chipY, 0f, 0f);
            cx += chip1Width + gap;
            r.Chip2 = chip2Width > 0f ? new Rect(cx, chipY, chip2Width, GuardianChipHeight) : new Rect(cx, chipY, 0f, 0f);
            return r;
        }
    }
}
