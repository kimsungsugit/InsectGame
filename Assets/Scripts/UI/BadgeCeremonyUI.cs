using System.Collections.Generic;
using System.Text;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Story;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 수문장 배지 획득 연출 — 수문장전 결과 화면이 닫히면 곧바로, <b>스토리 대사보다 먼저</b> 뜬다.
    ///
    /// 순서를 보장하는 장치: 전투 화면이 닫힐 때 <c>StoryDirector.NotifyBattlePresentationClosed</c>가
    /// 미뤄 둔 <c>gd_*</c> 대사를 흘리기 <b>직전에</b> <c>BattlePresentationClosed</c>를 울린다. 여기서 그걸 듣고
    /// 모달을 먼저 열면, 스토리 쪽 드레인이 모달 가드에 막혀 이 연출이 닫힐 때까지 기다린다
    /// (<c>StoryDirector.Update</c>가 이어서 흘린다). 폴링만 하면 대사가 먼저 열려 배지가 대사 뒤로 밀린다.
    ///
    /// 연출(실제 초): 딤 → 배지가 뒤집히며 내려앉음 → 충격(빛 번짐·반짝이 흩어짐·효과음) → 이름·수문장·새김글 →
    /// 13칸 진열줄에서 새 칸이 켜짐 → (이정표면) 보상 카드. 이르게 누르면 끝 장면으로 건너뛰고, 다시 누르면 닫힌다.
    ///
    /// 모달이다(<see cref="IModalUI"/>) — 등록하지 않으면 스토리가 "화면에 아무것도 없다"고 보고 위로 대사를 띄운다.
    /// 닫힐 때 프리즈를 푸는 쪽도 여기다(<c>BattleScreenUI.EndBattle</c>은 모달이 떠 있으면 풀지 않는다).
    /// </summary>
    public class BadgeCeremonyUI : MonoBehaviour, IModalUI
    {
        // ── 타임라인(실제 초) ──
        private const float DimIn = 0.3f;
        private const float DropStart = 0.1f;
        private const float DropEnd = 0.85f;
        private const float TextAt = 0.95f;
        private const float StripAt = 1.3f;
        private const float RewardAt = 1.75f;
        /// <summary>이보다 이르게 누르면 닫지 않고 끝 장면으로 건너뛴다 — 연타로 배지를 못 보고 지나가지 않게.</summary>
        private const float SettleAt = 2.1f;
        private const float SkipTo = 2.4f;
        private const int SparkleCount = 10;

        private GuardianBadgeService badges;
        private RegionManager regionManager;
        private ItemDatabase itemDatabase;
        private StoryDirector storyDirector;
        private CameraFollower cameraFollower;
        private PlayerMovement playerMovement;

        private readonly List<GuardianBadgeService.Award> queue = new List<GuardianBadgeService.Award>();
        private GuardianBadgeService.Award current;
        private bool open;
        private float t;
        private bool restoreFrozen;
        private bool impactPlayed;
        private bool rewardPlayed;

        // 열 때 한 번 만든다(OnGUI는 프레임당 여러 번 돈다).
        private string nameText;
        private string guardianText;
        private string flavorText;
        private string countText;
        private string rewardTitle;
        private string rewardText;
        private string hintText;
        private Color themeColor;
        private int newIndex;
        private readonly bool[] earnedMask = new bool[GuardianBadges.Total];
        private GuardianBadges.Badge[] table;

        private bool stylesReady;
        private GUIStyle titleStyle;
        private GUIStyle nameStyle;
        private GUIStyle subStyle;
        private GUIStyle flavorStyle;
        private GUIStyle countStyle;
        private GUIStyle rewardTitleStyle;
        private GUIStyle rewardStyle;
        private GUIStyle hintStyle;

        public bool IsOpen => open;
        /// <summary>지금 보여 주는 배지의 리전 — 검증 도구가 기록한다(<c>StoryBeatWalkthrough</c>).</summary>
        public string CurrentRegionId => open ? current.regionId : null;
        private bool HasReward => !string.IsNullOrEmpty(rewardText);

        /// <summary>ESC·뒤로 — 이 배지를 닫고 다음 배지(있으면)로.</summary>
        public void CloseModal() => Finish();

        public void AutoWire(GuardianBadgeService service, RegionManager region, ItemDatabase items)
        {
            if (badges == null) badges = service;
            if (regionManager == null) regionManager = region;
            if (itemDatabase == null) itemDatabase = items;
            Subscribe();
        }

        public void AutoWire(StoryDirector director, CameraFollower cam, PlayerMovement movement)
        {
            if (storyDirector == null) storyDirector = director;
            if (cameraFollower == null) cameraFollower = cam;
            if (playerMovement == null) playerMovement = movement;
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            if (badges != null) badges.BadgeAwarded -= OnAwarded;
            if (storyDirector != null) storyDirector.BattlePresentationClosed -= TryOpen;
            // 오프닝 다시보기가 UI 루트를 통째로 끈다 — 보던 배지는 줄 맨 앞으로 되돌려 다시 켜질 때 이어 보인다.
            if (open)
            {
                queue.Insert(0, current);
                Close();
            }
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled) return;
            if (badges != null)
            {
                badges.BadgeAwarded -= OnAwarded;
                badges.BadgeAwarded += OnAwarded;
            }
            if (storyDirector != null)
            {
                storyDirector.BattlePresentationClosed -= TryOpen;
                storyDirector.BattlePresentationClosed += TryOpen;
            }
        }

        private void OnAwarded(GuardianBadgeService.Award award)
        {
            queue.Add(award);
            TryOpen();   // 전투 중이면 여기서 막히고, 결과 화면이 닫힐 때 BattlePresentationClosed가 다시 부른다
        }

        private void TryOpen()
        {
            if (open || queue.Count == 0) return;
            if (cameraFollower != null && cameraFollower.InBattleMode) return;   // 전투 화면(결과 포함)이 아직 있다
            if (ModalUIRegistry.IsAnyOpen()) return;                              // 대사·컷신·영상이 먼저 떠 있다
            GuardianBadgeService.Award next = queue[0];
            queue.RemoveAt(0);
            Open(next);
        }

        private void Open(GuardianBadgeService.Award award)
        {
            current = award;
            open = true;
            t = 0f;
            impactPlayed = false;
            rewardPlayed = false;
            BuildTexts();

            restoreFrozen = playerMovement != null && playerMovement.IsFrozen;
            if (playerMovement != null)
            {
                playerMovement.CancelAutoRun();
                playerMovement.SetFrozen(true);
            }
            ModalUIRegistry.Register(this);
        }

        private void Finish()
        {
            if (!open)
            {
                ModalUIRegistry.Unregister(this);
                return;
            }
            Close();
            TryOpen();   // 줄 선 배지가 더 있으면 이어서(클라우드 병합 뒤 격파가 겹친 경우 등)
        }

        private void Close()
        {
            open = false;
            ModalUIRegistry.Unregister(this);
            if (playerMovement != null && !restoreFrozen) playerMovement.SetFrozen(false);
        }

        private void Advance()
        {
            if (!open) return;
            if (t < SettleAt)
            {
                t = Mathf.Max(t, SkipTo);
                return;
            }
            Finish();
        }

        private void Update()
        {
            if (!open)
            {
                if (queue.Count > 0) TryOpen();   // 통지 없이 전투가 끝난 경로의 안전망
                return;
            }

            // 한 프레임에 쌓는 시간을 제한한다 — 전투 화면을 걷는 프레임에 멈칫(GC·텍스처 로드)이 있으면
            // 그 몇백 ms가 한꺼번에 들어와 배지가 내려앉는 장면을 통째로 건너뛴다(QA 캡처에서 실제로 났다).
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (!impactPlayed && t >= DropEnd)
            {
                impactPlayed = true;
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.SetComplete);
            }
            if (!rewardPlayed && HasReward && t >= RewardAt)
            {
                rewardPlayed = true;
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.LevelUp);
            }
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                Advance();
        }

        private void BuildTexts()
        {
            if (table == null) table = GuardianBadges.All();
            GuardianBadges.TryGet(current.regionId, out GuardianBadges.Badge badge);
            RegionData region = regionManager != null ? regionManager.GetRegionById(current.regionId) : null;

            nameText = !string.IsNullOrEmpty(badge.name) ? badge.name : current.regionId;
            guardianText = region != null && !string.IsNullOrEmpty(region.guardianDisplayName)
                ? $"{region.guardianDisplayName}  ·  Lv.{region.guardianLevel}"
                : string.Empty;
            flavorText = badge.flavor ?? string.Empty;
            countText = $"모은 배지  {current.earned} / {GuardianBadges.Total}";
            themeColor = region != null ? region.themeColor : UITheme.Instance.accentAmber;
            newIndex = GuardianBadges.IndexOf(current.regionId);
            hintText = UIScale.IsMobileLayout ? "화면을 눌러 계속" : "클릭하거나 Space로 계속";

            for (int i = 0; i < table.Length && i < earnedMask.Length; i++)
                earnedMask[i] = badges != null && badges.IsEarned(table[i].regionId);
            if (newIndex >= 0 && newIndex < earnedMask.Length) earnedMask[newIndex] = true;

            rewardTitle = null;
            rewardText = null;
            if (current.milestones == null || current.milestones.Length == 0) return;

            StringBuilder title = new StringBuilder("배지 ");
            StringBuilder items = new StringBuilder();
            string lastTitle = null;
            for (int i = 0; i < current.milestones.Length; i++)
            {
                if (!GuardianBadges.TryGetMilestone(current.milestones[i], out GuardianBadges.Milestone m)) continue;
                if (i > 0) title.Append('·');
                title.Append(m.count);
                lastTitle = m.title;
                if (m.rewards == null) continue;
                foreach (GuardianBadges.Reward r in m.rewards)
                {
                    string name = ItemName(r.itemId);
                    if (string.IsNullOrEmpty(name)) continue;   // 이름을 모르면 원문 ID를 내보내지 않고 뺀다
                    if (items.Length > 0) items.Append("   ·   ");
                    items.Append(name).Append(" ×").Append(r.count);
                }
            }
            title.Append("개 달성");
            if (!string.IsNullOrEmpty(lastTitle)) title.Append("  —  「").Append(lastTitle).Append('」');
            rewardTitle = title.ToString();
            rewardText = items.Length > 0 ? items.ToString() : "보상을 받았다";
        }

        private string ItemName(string itemId)
        {
            ItemData item = itemDatabase != null ? itemDatabase.FindById(itemId) : null;
            return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : string.Empty;
        }

        // ── 그리기 ──

        private void OnGUI()
        {
            if (!open) return;
            int prevDepth = GUI.depth;
            GUI.depth = -30;   // HUD·도감(-10) 위, 계정 모달(-20)과는 동시에 뜨지 않는다
            UIScale.Begin();
            EnsureStyles();
            Draw();
            UIScale.End();
            GUI.depth = prevDepth;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            subStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            flavorStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };
            countStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            rewardTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            rewardStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
        }

        private void Draw()
        {
            UITheme theme = UITheme.Instance;
            float W = UIScale.VirtualScreenWidth;
            bool portrait = UIScale.IsPortrait;

            // 딤 — 화면 전체. 누르면 진행(투명 버튼이라 터치 합성 클릭도 받는다).
            float dim = Mathf.Clamp01(t / DimIn);
            UISurface.Dim(0.84f * dim);

            // ── 세로 배치: 제목 · 배지 · 이름 · 수문장 · 새김글 · 진열줄 · 보상 · 안내 ──
            float badgeSize = portrait ? 440f : 340f;
            const float titleH = 58f, nameH = 90f, subH = 44f, flavorH = 42f, countH = 38f, rewardH = 120f, hintH = 36f;
            float slot = Mathf.Min(64f, (W - 96f - 12f * 8f) / GuardianBadges.Total);
            float stripH = slot + 8f;
            float blockH = titleH + 16f + badgeSize + 18f + nameH + subH + flavorH + 26f + countH + stripH
                           + (HasReward ? 22f + rewardH : 0f) + 18f + hintH;
            float y = UISafeLayout.CenteredY(blockH);
            float cx = W * 0.5f;

            // 제목
            float textIn = Mathf.Clamp01((t - (TextAt - 0.25f)) / 0.35f);
            Color amber = theme.accentAmber;
            GUI.color = new Color(amber.r, amber.g, amber.b, textIn);
            GUI.Label(new Rect(cx - 400f, y + (1f - textIn) * 16f, 800f, titleH), "배지 획득!", titleStyle);
            GUI.color = Color.white;
            y += titleH + 16f;

            Vector2 center = new Vector2(cx, y + badgeSize * 0.5f);
            DrawBadgeMoment(center, badgeSize);
            y += badgeSize + 18f;

            // 이름 · 수문장 · 새김글
            float nameIn = Mathf.Clamp01((t - TextAt) / 0.3f);
            GUI.color = new Color(1f, 1f, 1f, nameIn);
            nameStyle.normal.textColor = theme.textPrimary;
            UIHelper.LabelFit(new Rect(cx - 520f, y + (1f - nameIn) * 20f, 1040f, nameH), nameText, nameStyle);
            y += nameH;
            float subIn = Mathf.Clamp01((t - TextAt - 0.12f) / 0.3f);
            GUI.color = new Color(1f, 1f, 1f, subIn);
            subStyle.normal.textColor = theme.textSecondary;
            if (!string.IsNullOrEmpty(guardianText))
                UIHelper.LabelFit(new Rect(cx - 520f, y, 1040f, subH), guardianText, subStyle);
            y += subH;
            flavorStyle.normal.textColor = Color.Lerp(theme.textSecondary, themeColor, 0.35f);
            UIHelper.LabelFit(new Rect(cx - 520f, y, 1040f, flavorH), flavorText, flavorStyle);
            y += flavorH + 26f;
            GUI.color = Color.white;

            // 진열줄 — 13칸. 새 칸은 늦게 켜지며 튄다.
            float stripIn = Mathf.Clamp01((t - StripAt) / 0.3f);
            GUI.color = new Color(1f, 1f, 1f, stripIn);
            countStyle.normal.textColor = theme.textSecondary;
            GUI.Label(new Rect(cx - 300f, y, 600f, countH), countText, countStyle);
            GUI.color = Color.white;   // 칸은 알파를 인자로 받는다 — 여기 남기면 두 번 곱해진다
            y += countH;
            DrawStrip(new Rect(cx - (slot * GuardianBadges.Total + 8f * (GuardianBadges.Total - 1)) * 0.5f, y + 4f, 0f, slot), slot, stripIn);
            GUI.color = Color.white;
            y += stripH;

            // 이정표 보상
            if (HasReward)
            {
                y += 22f;
                float rIn = Mathf.Clamp01((t - RewardAt) / 0.35f);
                if (rIn > 0f)
                {
                    float cardW = Mathf.Min(860f, W - 96f);
                    Rect card = new Rect(cx - cardW * 0.5f, y + (1f - rIn) * 30f, cardW, rewardH);
                    GUI.color = new Color(1f, 1f, 1f, rIn);
                    UISurface.Card(card, theme.surfaceRaised, theme.accentAmber);
                    rewardTitleStyle.normal.textColor = theme.accentAmber;
                    UIHelper.LabelFit(new Rect(card.x + 20f, card.y + 10f, card.width - 40f, 42f), rewardTitle, rewardTitleStyle);
                    rewardStyle.normal.textColor = theme.textPrimary;
                    UIHelper.LabelFit(new Rect(card.x + 20f, card.y + 54f, card.width - 40f, 56f), rewardText, rewardStyle);
                    GUI.color = Color.white;
                }
                y += rewardH;
            }

            // 안내 — 다 보인 뒤에 깜빡인다.
            y += 18f;
            if (t >= SettleAt)
            {
                float blink = 0.55f + 0.45f * Mathf.Sin((t - SettleAt) * 3.2f);
                Color muted = theme.textMuted;
                GUI.color = new Color(muted.r, muted.g, muted.b, blink);
                hintStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(cx - 300f, y, 600f, hintH), hintText, hintStyle);
                GUI.color = Color.white;
            }

            // 화면 전체가 버튼 — 맨 마지막에 둬야 위에서 그린 것에 가리지 않고 입력을 받는다.
            if (GUI.Button(new Rect(0f, 0f, W, UIScale.VirtualScreenHeight), GUIContent.none, GUIStyle.none))
                Advance();
        }

        /// <summary>배지가 떨어져 내려앉는 순간 — 빛살·번짐·반짝이·뒤집힘.</summary>
        private void DrawBadgeMoment(Vector2 center, float size)
        {
            float impact = t - DropEnd;   // 음수면 아직 내려오는 중

            // 뒤 빛: 리전 색 번짐(늘 은은하게) + 충격 뒤 도는 빛살
            float pulse = 0.85f + 0.15f * Mathf.Sin(t * 2.4f);
            float glowIn = Mathf.Clamp01(t / 0.6f);
            BadgeArt.DrawCentered(BadgeArt.Glow, center, size * 2.3f * pulse,
                new Color(themeColor.r, themeColor.g, themeColor.b, 0.42f * glowIn));
            if (impact > 0f)
            {
                float raysIn = Mathf.Clamp01(impact / 0.4f);
                float raySize = size * (2.2f + 0.25f * Mathf.Sin(t * 1.3f));
                Color warm = Color.Lerp(UITheme.Instance.accentAmber, Color.white, 0.45f);
                BadgeArt.DrawRotated(new Rect(center.x - raySize * 0.5f, center.y - raySize * 0.5f, raySize, raySize),
                    BadgeArt.Rays, t * 14f, new Color(warm.r, warm.g, warm.b, 0.5f * raysIn));
            }

            // 배지 — 위에서 내려오며 두 번 뒤집히고, 살짝 눌렸다 제자리로(EaseOutBack).
            float p = Mathf.Clamp01((t - DropStart) / (DropEnd - DropStart));
            float scale = Mathf.LerpUnclamped(1.7f, 1f, EaseOutBack(p));
            float flip = Mathf.Abs(Mathf.Cos((1f - EaseOutCubic(p)) * Mathf.PI * 2f));
            float drop = (1f - EaseOutCubic(p)) * -120f;
            float bob = impact > 0f ? Mathf.Sin(impact * 2.1f) * 5f : 0f;
            float w = size * scale * Mathf.Max(0.05f, flip);
            float h = size * scale;
            Rect badgeRect = new Rect(center.x - w * 0.5f, center.y - h * 0.5f + drop + bob, w, h);
            float shade = 0.55f + 0.45f * flip;   // 옆으로 설 때 어두워진다 — 두께가 있는 것처럼
            GUI.color = new Color(shade, shade, shade, Mathf.Clamp01(p * 4f));
            BadgeArt.Draw(badgeRect, current.regionId, true);
            GUI.color = Color.white;

            if (impact <= 0f) return;

            // 충격 번짐
            float flash = Mathf.Clamp01(1f - impact / 0.35f);
            if (flash > 0f)
                BadgeArt.DrawCentered(BadgeArt.Glow, center, size * (1.4f + impact * 4f), new Color(1f, 1f, 1f, 0.9f * flash));

            // 반짝이 — 고정 각도(난수 없이)로 흩어졌다 사라진다.
            float burst = Mathf.Clamp01(impact / 0.9f);
            if (burst < 1f)
            {
                for (int i = 0; i < SparkleCount; i++)
                {
                    float ang = (i / (float)SparkleCount) * Mathf.PI * 2f + (i % 2 == 0 ? 0.18f : -0.12f);
                    float reach = size * (0.52f + 0.5f * EaseOutCubic(burst) + (i % 3) * 0.06f);
                    Vector2 pos = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * reach;
                    float s = Mathf.Lerp(46f, 14f, burst) * (i % 2 == 0 ? 1f : 0.7f);
                    BadgeArt.DrawCentered(BadgeArt.Sparkle, pos, s, new Color(1f, 0.96f, 0.82f, 1f - burst));
                }
            }

            // 광택 — 배지 위 두 점이 번갈아 반짝인다.
            float glintA = Mathf.Clamp01(Mathf.Sin(impact * 2.6f) * 1.4f - 0.4f);
            float glintB = Mathf.Clamp01(Mathf.Sin(impact * 2.6f + 2.2f) * 1.4f - 0.4f);
            BadgeArt.DrawCentered(BadgeArt.Sparkle, center + new Vector2(-size * 0.2f, -size * 0.26f + bob), 38f,
                new Color(1f, 1f, 1f, glintA));
            BadgeArt.DrawCentered(BadgeArt.Sparkle, center + new Vector2(size * 0.24f, size * 0.12f + bob), 26f,
                new Color(1f, 1f, 1f, glintB));
        }

        private void DrawStrip(Rect start, float slot, float alpha)
        {
            if (table == null) return;
            float x = start.x;
            for (int i = 0; i < table.Length; i++)
            {
                Rect r = new Rect(x, start.y, slot, slot);
                bool earned = i < earnedMask.Length && earnedMask[i];
                if (i == newIndex)
                {
                    // 새 칸 — 진열줄이 켜진 뒤 한 박자 늦게 튀어 오른다.
                    float pop = Mathf.Clamp01((t - StripAt - 0.25f) / 0.35f);
                    float sc = pop <= 0f ? 1f : Mathf.LerpUnclamped(1.6f, 1f, EaseOutBack(pop));
                    Rect pr = new Rect(r.center.x - slot * sc * 0.5f, r.center.y - slot * sc * 0.5f, slot * sc, slot * sc);
                    if (pop > 0f)
                        BadgeArt.DrawCentered(BadgeArt.Glow, r.center, slot * 2.2f,
                            new Color(themeColor.r, themeColor.g, themeColor.b, 0.6f * alpha * (1f - 0.5f * pop)));
                    BadgeArt.Draw(pr, table[i].regionId, pop > 0f, alpha);
                }
                else
                {
                    BadgeArt.Draw(r, table[i].regionId, earned, alpha);
                }
                x += slot + 8f;
            }
        }

        private static float EaseOutCubic(float x)
        {
            float u = 1f - Mathf.Clamp01(x);
            return 1f - u * u * u;
        }

        private static float EaseOutBack(float x)
        {
            x = Mathf.Clamp01(x);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = x - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
