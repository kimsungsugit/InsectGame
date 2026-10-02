using InsectGame.Core;
using InsectGame.Story;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 필드 소식 — 레벨업, 이야기 보상, 라온과의 내기, 색다른 조우를 화면에 잠깐 띄운다.
    ///
    /// 소식은 <see cref="FieldMomentFeed"/>에서 <b>꺼내 온다</b>(구독하지 않는다). UI 루트가 꺼졌다 켜질 때
    /// 구독이 사라지는 계열(rules/ui-layout.md)에 걸리지 않고, 대사·전투 화면이 덮은 동안 들어온 소식은
    /// 그 뒤에 차례로 뜬다. 내기 점수판도 <see cref="RivalRaceController"/>의 상태를 읽기만 한다.
    ///
    /// <b>비모달</b>이라 자리를 <see cref="FieldHudInput"/>에 등록한다 — 안 하면 카드를 누른 탭이 월드
    /// 클릭-이동으로 새어 캐릭터가 그 밑으로 걸어간다.
    /// </summary>
    public class FieldMomentsUI : MonoBehaviour
    {
        private const float ShowSeconds = 3.6f;
        private const float FadeInSeconds = 0.25f;
        private const float FadeOutSeconds = 0.35f;
        private const float ToastHeight = 108f;
        private const float RaceChipHeight = 58f;
        // 리전 배너(ContentTop부터 80) 아래, 코치 배너(ContentTop+150) 위.
        private const float RaceChipTop = 86f;
        // 데스크톱에서 토스트가 놓이는 자리 — 코치 배너(ContentTop+150, 높이 100) 아래.
        private const float DesktopToastTop = 262f;

        private FieldMomentFeed feed;
        private PlayerProgressController progress;
        private RivalRaceController rivalRace;
        private PlayerMovement playerMovement;

        private bool hasCurrent;
        private FieldMoment current;
        private float shownFor;
        // 마지막으로 본 레벨업 일련번호. 음수면 아직 못 봤다 — 처음 본 값은 기준일 뿐 연출하지 않는다.
        private int seenLevelUpSerial = -1;

        // 목표 수는 규칙(RivalRaceRules.Target)이 정한다 — 문구에 숫자를 따로 박아 두면 규칙을 바꿀 때 어긋난다.
        private static readonly string RaceGoalText = $"먼저 {RivalRaceRules.Target}마리!";

        private GUIStyle titleStyle, bodyStyle, glyphStyle, raceNameStyle, raceMidStyle;
        private bool stylesReady;

        public void AutoWire(FieldMomentFeed momentFeed, PlayerProgressController progressController,
            RivalRaceController race, PlayerMovement movement)
        {
            if (feed == null) feed = momentFeed;
            if (progress == null) progress = progressController;
            if (rivalRace == null) rivalRace = race;
            if (playerMovement == null) playerMovement = movement;
        }

        // 대사·전투·메뉴가 화면을 덮었거나 포획 창처럼 조작이 묶인 동안은 그리지 않고 시간도 흐르지 않는다 —
        // 보지 못한 소식이 그사이에 지나가 버리지 않게.
        private bool Hidden => ModalUIRegistry.IsAnyOpen() || (playerMovement != null && playerMovement.IsFrozen);

        private void Update()
        {
            WatchLevelUp();
            if (Hidden) return;

            if (hasCurrent)
            {
                shownFor += Time.deltaTime;
                if (shownFor >= ShowSeconds) hasCurrent = false;
            }
            if (!hasCurrent && feed != null && feed.TryDequeue(out current))
            {
                hasCurrent = true;
                shownFor = 0f;
                if (current.Kind == FieldMomentKind.LevelUp && AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFX(SfxType.LevelUp);
            }
        }

        private void WatchLevelUp()
        {
            if (progress == null || feed == null) return;
            int serial = progress.LevelUpSerial;
            if (seenLevelUpSerial < 0) { seenLevelUpSerial = serial; return; }
            if (serial == seenLevelUpSerial) return;
            seenLevelUpSerial = serial;

            // 캐릭터 레벨이 실제로 바꾸는 것을 한 줄로 — 곤충이 캐릭터보다 이만큼 넘게 높으면 포획이 깎인다.
            int level = progress.Level;
            feed.Push(FieldMomentKind.LevelUp, $"레벨 업!  Lv.{level}",
                $"이제 Lv.{level + GameConstants.TrainerLevel.CaptureGraceLevels} 곤충까지 레벨 차 페널티 없이 잡을 수 있습니다");
        }

        private void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            UITheme t = UITheme.Instance;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, alignment = TextAnchor.MiddleLeft };
            glyphStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            glyphStyle.normal.textColor = t.surfaceBase;
            raceNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            raceMidStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            raceMidStyle.normal.textColor = t.textSecondary;
        }

        private void OnGUI()
        {
            bool race = rivalRace != null && rivalRace.IsActive;
            if (!race && !hasCurrent) return;
            if (Hidden) return;

            UIScale.Begin();
            InitStyles();
            if (race) DrawRaceChip();
            if (hasCurrent) DrawToast();
            GUI.color = Color.white;
            UIScale.End();
        }

        // ── 라온과의 내기 점수판 ──

        private void DrawRaceChip()
        {
            UITheme t = UITheme.Instance;
            float left = UISafeLayout.ContentLeft;
            float right = left + UISafeLayout.ContentWidth;
            if (UIScale.IsMobileLayout)
            {
                // 리전 배너와 같은 구간에 둔다 — 좌상단 상태 탭과 우상단 단축 바 사이(KeyGuideHUD와 같은 계산).
                // 화면 중앙에 두면 세로 화면에서 오른쪽 끝("라온")이 단축 바 밑에 깔린다(2026-10-02 검수 캡처).
                left = PlayerStatusHUD.CollapsedTabRight + UITheme.Space.S;
                right = QuickAccessBarUI.ShortcutBarRect.x - UITheme.Space.S;
            }
            float w = Mathf.Min(460f, Mathf.Max(1f, right - left));
            Rect chip = new Rect(left + (right - left - w) * 0.5f,
                UISafeLayout.ContentTop + RaceChipTop, w, RaceChipHeight);
            FieldHudInput.RegisterBlockingRect(chip);
            UISurface.HudCard(chip);

            raceNameStyle.normal.textColor = t.accentMint;
            GUI.Label(new Rect(chip.x + 10f, chip.y, 48f, chip.height), "나", raceNameStyle);
            DrawPips(chip.x + 62f, chip.center.y, 1f, rivalRace.PlayerCount, t.accentMint);

            // 가운데 문구는 양쪽 점수(이름 + 점 셋, 한쪽 148px) 사이에 남는 폭만 쓴다.
            float midW = Mathf.Max(60f, chip.width - 296f);
            UIHelper.LabelFit(new Rect(chip.center.x - midW * 0.5f, chip.y, midW, chip.height),
                RaceGoalText, raceMidStyle);

            DrawPips(chip.xMax - 62f, chip.center.y, -1f, rivalRace.RivalCount, t.accentCoral);
            raceNameStyle.normal.textColor = t.accentCoral;
            UIHelper.LabelFit(new Rect(chip.xMax - 62f, chip.y, 56f, chip.height), "라온", raceNameStyle);
        }

        // 점 세 개 — 잡은 만큼 채운다. direction이 음수면 오른쪽에서 왼쪽으로 채운다(라온 쪽).
        private static void DrawPips(float startX, float centerY, float direction, int filled, Color color)
        {
            UITheme t = UITheme.Instance;
            const float size = 18f;
            const float pitch = 26f;
            for (int i = 0; i < RivalRaceRules.Target; i++)
            {
                float cx = startX + direction * (i * pitch + size * 0.5f);
                UIShapes.Ellipse(new Rect(cx - size * 0.5f, centerY - size * 0.5f, size, size),
                    i < filled ? color : t.surfaceBorder);
            }
        }

        // ── 소식 한 장 ──

        private Rect ToastRect()
        {
            float w = Mathf.Min(760f, UISafeLayout.ContentWidth);
            float x = UISafeLayout.ContentLeft + (UISafeLayout.ContentWidth - w) * 0.5f;
            // 모바일은 화면 가운데 줄 아래(캐릭터 발밑)에 놓는다 — 위쪽은 미니맵·퀘스트 칩·메뉴 격자 자리라
            // 그리기 순서가 정해지지 않은 IMGUI에서 그 밑에 깔린다(섬 안내 배너와 같은 자리, rules/island.md).
            float y = UIScale.IsMobileLayout
                ? Mathf.Clamp(UIScale.VirtualScreenHeight * (UIScale.IsPortrait ? 0.59f : 0.64f),
                    UISafeLayout.ContentTop, UISafeLayout.ContentBottom - ToastHeight)
                : UISafeLayout.ContentTop + DesktopToastTop;
            return new Rect(x, y, w, ToastHeight);
        }

        private void DrawToast()
        {
            UITheme t = UITheme.Instance;
            float alpha = Mathf.Clamp01(shownFor / FadeInSeconds)
                * Mathf.Clamp01((ShowSeconds - shownFor) / FadeOutSeconds);
            Rect card = ToastRect();
            card.y -= (1f - Mathf.Clamp01(shownFor / FadeInSeconds)) * 14f;   // 살짝 내려앉으며 나타난다
            FieldHudInput.RegisterBlockingRect(card);

            Color accent = AccentOf(current.Kind);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            UISurface.Card(card, t.surfaceCard, Color.Lerp(t.surfaceBorder, accent, 0.6f));
            UISurface.Flat(new Rect(card.x + UITheme.Radius.Card, card.y + 3f, card.width - UITheme.Radius.Card * 2f, 4f), accent);
            GUI.color = Color.white;

            // 왼쪽 기호 원 — 처음 0.9초 동안 둘레로 점이 퍼진다(레벨업·색다른 조우처럼 좋은 소식일 때만).
            Vector2 icon = new Vector2(card.x + 58f, card.center.y + 2f);
            if (current.Kind != FieldMomentKind.Rival) DrawBurst(icon, accent, alpha);
            UIShapes.Ellipse(new Rect(icon.x - 30f, icon.y - 30f, 60f, 60f), new Color(accent.r, accent.g, accent.b, alpha));
            glyphStyle.normal.textColor = new Color(t.surfaceBase.r, t.surfaceBase.g, t.surfaceBase.b, alpha);
            GUI.Label(new Rect(icon.x - 30f, icon.y - 30f, 60f, 60f), GlyphOf(current.Kind), glyphStyle);

            float textX = card.x + 106f;
            float textW = card.xMax - 20f - textX;
            bool hasBody = !string.IsNullOrEmpty(current.Body);
            titleStyle.normal.textColor = new Color(accent.r, accent.g, accent.b, alpha);
            UIHelper.LabelFit(new Rect(textX, card.y + (hasBody ? 14f : 32f), textW, 44f), current.Title, titleStyle);
            if (hasBody)
            {
                bodyStyle.normal.textColor = new Color(t.textSecondary.r, t.textSecondary.g, t.textSecondary.b, alpha);
                UIHelper.LabelFit(new Rect(textX, card.y + 60f, textW, 36f), current.Body, bodyStyle);
            }
        }

        private void DrawBurst(Vector2 center, Color accent, float alpha)
        {
            const float burstSeconds = 0.9f;
            if (shownFor >= burstSeconds) return;
            float k = shownFor / burstSeconds;
            float radius = 34f + k * 30f;
            float size = 10f * (1f - k);
            Color dot = new Color(accent.r, accent.g, accent.b, alpha * (1f - k));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f + k * 0.8f;
                UIShapes.Ellipse(new Rect(center.x + Mathf.Cos(angle) * radius - size * 0.5f,
                    center.y + Mathf.Sin(angle) * radius - size * 0.5f, size, size), dot);
            }
        }

        private static Color AccentOf(FieldMomentKind kind)
        {
            UITheme t = UITheme.Instance;
            switch (kind)
            {
                case FieldMomentKind.Reward: return t.accentMint;
                case FieldMomentKind.Rival: return t.accentCoral;
                default: return t.accentAmber;   // LevelUp · Discovery
            }
        }

        private static string GlyphOf(FieldMomentKind kind)
        {
            switch (kind)
            {
                case FieldMomentKind.LevelUp: return "▲";
                case FieldMomentKind.Reward: return "★";
                case FieldMomentKind.Rival: return "VS";
                default: return "!";
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>검수 빌드 전용 — 소식 한 장을 지정한 시점(초)에 세워 둔다.</summary>
        public void ShowForCapture(FieldMoment moment, float at)
        {
            current = moment;
            hasCurrent = true;
            shownFor = at;
        }
#endif
    }
}
