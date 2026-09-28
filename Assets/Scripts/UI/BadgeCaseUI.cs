using System.Text;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 배지 케이스 — 수문장 배지 13개의 진열판, 고른 배지의 상세, 그리고 4·8·13개 이정표 보상.
    /// 퀵메뉴 [배지](K)로 연다.
    ///
    /// 못 얻은 배지도 <b>자리는 보인다</b>(어둡게) — 앞으로 무엇이 남았는지가 모으는 동기다. 다만 아직 못 가 본
    /// 땅의 이름은 가린다("???") — 2막 리전 이름("이름 없는 자리")이 1막에서 스포일러가 된다.
    ///
    /// 이정표는 대개 획득 연출이 바로 지급하지만, 이 기능 전에 배지를 모아 둔 세이브는 새 배지를 더 얻지 않으므로
    /// 여기 [받기]가 유일한 수령 경로다(<see cref="GuardianBadgeService.TryClaim"/>).
    /// </summary>
    public class BadgeCaseUI : MonoBehaviour, IModalUI
    {
        private GuardianBadgeService badges;
        private RegionManager regionManager;
        private ItemDatabase itemDatabase;

        private bool isOpen;
        private int selected = -1;

        private GuardianBadges.Badge[] table;
        private GuardianBadges.Milestone[] milestones;
        // 열 때 한 번 굽는 표시값(OnGUI 할당 방지). 수령·클라우드 반영으로 바뀌면 Refresh가 다시 굽는다.
        private readonly bool[] earned = new bool[GuardianBadges.Total];
        private readonly bool[] visited = new bool[GuardianBadges.Total];
        private readonly string[] cellLabel = new string[GuardianBadges.Total];
        private string[] milestoneHeader;
        private string[] milestoneReward;
        private string[] milestoneProgress;
        private int earnedCount;
        private string subtitle;
        private string detailName;
        private string detailGuardian;
        private string detailFlavor;
        private string toast;
        private float toastUntil;

        private bool stylesReady;
        private GUIStyle cellStyle;
        private GUIStyle detailNameStyle;
        private GUIStyle detailSubStyle;
        private GUIStyle detailFlavorStyle;
        private GUIStyle msHeadStyle;
        private GUIStyle msRewardStyle;
        private GUIStyle msProgressStyle;
        private GUIStyle buttonStyle;
        private GUIStyle toastStyle;

        public bool IsOpen => isOpen;

        /// <summary>받을 수 있는 이정표 수 — 퀵메뉴가 알림 점을 찍는다.</summary>
        public int ClaimableCount => badges != null ? badges.ClaimableCount : 0;

        public void AutoWire(GuardianBadgeService service, RegionManager region, ItemDatabase items)
        {
            if (badges == null) badges = service;
            if (regionManager == null) regionManager = region;
            if (itemDatabase == null) itemDatabase = items;
        }

        public void Toggle()
        {
            if (isOpen)
            {
                CloseModal();
                return;
            }
            isOpen = true;
            Refresh();
            // 처음엔 가장 최근에 얻은(표 순서로 가장 뒤) 배지를 고른다 — 방금 얻은 것을 보러 오는 게 보통이다.
            selected = 0;
            for (int i = 0; i < earned.Length; i++) if (earned[i]) selected = i;
            BuildDetail();
            toast = null;
            ModalUIRegistry.Register(this);
        }

        public void CloseModal()
        {
            isOpen = false;
            ModalUIRegistry.Unregister(this);
        }

        private void OnDisable()
        {
            // isOpen을 남겨 두면 레지스트리엔 없는데 열린 것으로 아는 상태가 된다(StoryJournalUI와 같은 이유).
            isOpen = false;
            ModalUIRegistry.Unregister(this);
        }

        // ── 표시값 ──

        private void Refresh()
        {
            if (table == null) table = GuardianBadges.All();
            if (milestones == null)
            {
                milestones = GuardianBadges.AllMilestones();
                milestoneHeader = new string[milestones.Length];
                milestoneReward = new string[milestones.Length];
                milestoneProgress = new string[milestones.Length];
                for (int i = 0; i < milestones.Length; i++)
                {
                    milestoneHeader[i] = $"배지 {milestones[i].count}개  ·  {milestones[i].title}";
                    milestoneReward[i] = RewardText(milestones[i]);
                }
            }

            earnedCount = 0;
            for (int i = 0; i < table.Length; i++)
            {
                earned[i] = badges != null && badges.IsEarned(table[i].regionId);
                RegionData region = regionManager != null ? regionManager.GetRegionById(table[i].regionId) : null;
                visited[i] = earned[i] || (regionManager != null && regionManager.IsRegionAccessible(region));
                if (earned[i]) earnedCount++;
                cellLabel[i] = earned[i] ? table[i].name : (visited[i] && region != null ? region.displayName : "???");
            }
            for (int i = 0; i < milestones.Length; i++)
                milestoneProgress[i] = $"{Mathf.Min(earnedCount, milestones[i].count)} / {milestones[i].count}";
            subtitle = $"수문장 배지 {earnedCount} / {GuardianBadges.Total}";
        }

        private void BuildDetail()
        {
            if (table == null || selected < 0 || selected >= table.Length) return;
            GuardianBadges.Badge b = table[selected];
            RegionData region = regionManager != null ? regionManager.GetRegionById(b.regionId) : null;
            if (earned[selected])
            {
                detailName = b.name;
                detailGuardian = region != null ? $"{region.displayName}  ·  {region.guardianDisplayName}  ·  Lv.{region.guardianLevel}" : string.Empty;
                detailFlavor = b.flavor;
            }
            else if (visited[selected] && region != null)
            {
                detailName = "아직 얻지 못한 배지";
                detailGuardian = $"{region.displayName}  ·  수문장 Lv.{region.guardianLevel}";
                detailFlavor = "이 땅의 수문장이 길을 내주면 얻는다.";
            }
            else
            {
                detailName = "아직 얻지 못한 배지";
                detailGuardian = "아직 가 보지 못한 땅";
                detailFlavor = "길이 열리면 이 자리에 무엇이 오는지 알게 된다.";
            }
        }

        private string RewardText(GuardianBadges.Milestone m)
        {
            if (m.rewards == null) return string.Empty;
            StringBuilder sb = new StringBuilder();
            foreach (GuardianBadges.Reward r in m.rewards)
            {
                ItemData item = itemDatabase != null ? itemDatabase.FindById(r.itemId) : null;
                if (item == null || string.IsNullOrEmpty(item.displayName)) continue;   // 원문 ID는 내보내지 않는다
                if (sb.Length > 0) sb.Append("  ·  ");
                sb.Append(item.displayName).Append(" ×").Append(r.count);
            }
            return sb.ToString();
        }

        private void Claim(int index)
        {
            if (badges == null || index < 0 || index >= milestones.Length) return;
            if (!badges.TryClaim(milestones[index].count)) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.LevelUp);
            toast = milestoneReward[index].Length > 0 ? "받았다!   " + milestoneReward[index] : "보상을 받았다!";
            toastUntil = Time.unscaledTime + 3f;
            Refresh();
        }

        // ── 그리기 ──

        private void OnGUI()
        {
            if (!isOpen) return;
            int prevDepth = GUI.depth;
            GUI.depth = -10;
            UIScale.Begin();
            EnsureStyles();
            DrawPanel();
            UIScale.End();
            GUI.depth = prevDepth;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            cellStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            detailNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            detailSubStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            detailFlavorStyle = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Italic, alignment = TextAnchor.UpperLeft, wordWrap = true };
            msHeadStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            msRewardStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            msProgressStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            toastStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        }

        private void DrawPanel()
        {
            UITheme theme = UITheme.Instance;
            bool portrait = UIScale.IsPortrait;
            UISurface.Dim(0.65f);
            Rect panel = portrait ? UISafeLayout.CenteredPanel(1020f, 1780f) : UISafeLayout.CenteredPanel(1600f, 960f);
            UISurface.Card(panel, theme.panelBg, theme.surfaceBorder);
            if (UISurface.Header(new Rect(panel.x + 3f, panel.y + 3f, panel.width - 6f, 84f), "배지 케이스", subtitle))
            {
                CloseModal();
                return;
            }

            const float pad = 24f, gap = 16f;
            Rect body = new Rect(panel.x + pad, panel.y + 104f, panel.width - pad * 2f, Mathf.Max(1f, panel.height - 104f - pad));

            if (portrait)
            {
                float detailH = 330f;
                float msH = Mathf.Min(340f, body.height * 0.22f);
                Rect detail = new Rect(body.x, body.y, body.width, detailH);
                Rect ms = new Rect(body.x, body.yMax - msH, body.width, msH);
                Rect grid = new Rect(body.x, detail.yMax + gap, body.width, Mathf.Max(1f, ms.y - gap - detail.yMax - gap));
                DrawDetail(detail, true);
                DrawGrid(grid, 4);
                DrawMilestones(ms, true);
            }
            else
            {
                float detailW = Mathf.Min(560f, body.width * 0.36f);
                float msH = 176f;
                Rect detail = new Rect(body.xMax - detailW, body.y, detailW, body.height);
                float leftW = body.width - detailW - gap;
                Rect ms = new Rect(body.x, body.yMax - msH, leftW, msH);
                Rect grid = new Rect(body.x, body.y, leftW, Mathf.Max(1f, ms.y - gap - body.y));
                DrawGrid(grid, 5);
                DrawMilestones(ms, false);
                DrawDetail(detail, false);
            }

            if (!string.IsNullOrEmpty(toast) && Time.unscaledTime < toastUntil)
            {
                float w = Mathf.Min(900f, panel.width - 80f);
                Rect tr = new Rect(panel.center.x - w * 0.5f, panel.yMax - 96f, w, 72f);
                UISurface.Card(tr, theme.surfaceRaised, theme.accentMint);
                toastStyle.normal.textColor = theme.textPrimary;
                UIHelper.LabelFit(new Rect(tr.x + 16f, tr.y + 6f, tr.width - 32f, tr.height - 12f), toast, toastStyle);
            }
        }

        private void DrawGrid(Rect area, int cols)
        {
            UITheme theme = UITheme.Instance;
            int rows = (GuardianBadges.Total + cols - 1) / cols;
            const float gap = 12f;
            float cellW = (area.width - gap * (cols - 1)) / cols;
            float cellH = Mathf.Max(UIScale.MinTouchHeight, (area.height - gap * (rows - 1)) / rows);
            for (int i = 0; i < table.Length; i++)
            {
                Rect cell = new Rect(area.x + (i % cols) * (cellW + gap), area.y + (i / cols) * (cellH + gap), cellW, cellH);
                bool sel = i == selected;
                UISurface.Card(cell, sel ? theme.surfaceRaised : theme.surfaceCard, sel ? theme.accentAmber : theme.surfaceBorder);

                float labelH = Mathf.Min(40f, cellH * 0.22f);
                float art = Mathf.Max(8f, Mathf.Min(cellW - 24f, cellH - labelH - 20f));
                Rect artRect = new Rect(cell.center.x - art * 0.5f, cell.y + 8f + (cellH - labelH - 16f - art) * 0.5f, art, art);
                if (earned[i])
                {
                    Color tc = RegionTheme(i);
                    BadgeArt.DrawCentered(BadgeArt.Glow, artRect.center, art * 1.5f, new Color(tc.r, tc.g, tc.b, sel ? 0.45f : 0.25f));
                }
                BadgeArt.Draw(artRect, table[i].regionId, earned[i]);

                cellStyle.normal.textColor = earned[i] ? theme.textPrimary : theme.textMuted;
                UIHelper.LabelFit(new Rect(cell.x + 6f, cell.yMax - labelH - 8f, cell.width - 12f, labelH), cellLabel[i], cellStyle);

                if (GUI.Button(cell, GUIContent.none, GUIStyle.none) && selected != i)
                {
                    selected = i;
                    BuildDetail();
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.ButtonClick);
                }
            }
        }

        private void DrawDetail(Rect area, bool horizontal)
        {
            if (selected < 0 || selected >= table.Length) return;
            UITheme theme = UITheme.Instance;
            UISurface.Card(area, theme.surfaceCard, theme.surfaceBorder);
            bool got = earned[selected];
            Color tc = RegionTheme(selected);

            Rect art;
            Rect text;
            if (horizontal)
            {
                float s = Mathf.Min(area.height - 40f, 280f);
                art = new Rect(area.x + 24f, area.center.y - s * 0.5f, s, s);
                text = new Rect(art.xMax + 28f, area.y + 24f, area.xMax - art.xMax - 52f, area.height - 48f);
            }
            else
            {
                float s = Mathf.Min(Mathf.Min(area.width - 140f, area.height * 0.46f), 380f);
                art = new Rect(area.center.x - s * 0.5f, area.y + 36f, s, s);
                text = new Rect(area.x + 28f, art.yMax + 28f, area.width - 56f, area.yMax - art.yMax - 52f);
            }

            if (got)
            {
                float pulse = 0.9f + 0.1f * Mathf.Sin(Time.unscaledTime * 2f);
                BadgeArt.DrawCentered(BadgeArt.Glow, art.center, art.width * 1.7f * pulse, new Color(tc.r, tc.g, tc.b, 0.4f));
                BadgeArt.DrawRotated(new Rect(art.center.x - art.width, art.center.y - art.height, art.width * 2f, art.height * 2f),
                    BadgeArt.Rays, Time.unscaledTime * 8f, new Color(1f, 0.95f, 0.8f, 0.16f));
            }
            BadgeArt.Draw(art, table[selected].regionId, got);

            float y = text.y;
            detailNameStyle.normal.textColor = got ? theme.textPrimary : theme.textSecondary;
            UIHelper.LabelFit(new Rect(text.x, y, text.width, 64f), detailName, detailNameStyle);
            y += 68f;
            detailSubStyle.normal.textColor = got ? Color.Lerp(theme.textSecondary, tc, 0.3f) : theme.textMuted;
            UIHelper.LabelFit(new Rect(text.x, y, text.width, 72f), detailGuardian, detailSubStyle);
            y += 80f;
            UISurface.Flat(new Rect(text.x, y, Mathf.Min(text.width, 320f), 3f), got ? theme.accentAmber : theme.surfaceBorder);
            y += 16f;
            detailFlavorStyle.normal.textColor = got ? theme.textPrimary : theme.textMuted;
            UIHelper.LabelFit(new Rect(text.x, y, text.width, Mathf.Max(40f, text.yMax - y)), detailFlavor, detailFlavorStyle);
        }

        private void DrawMilestones(Rect area, bool stacked)
        {
            UITheme theme = UITheme.Instance;
            int n = milestones.Length;
            const float gap = 12f;
            for (int i = 0; i < n; i++)
            {
                Rect card = stacked
                    ? new Rect(area.x, area.y + i * ((area.height - gap * (n - 1)) / n + gap), area.width, (area.height - gap * (n - 1)) / n)
                    : new Rect(area.x + i * ((area.width - gap * (n - 1)) / n + gap), area.y, (area.width - gap * (n - 1)) / n, area.height);
                int need = milestones[i].count;
                bool claimed = badges != null && badges.IsClaimed(need);
                bool ready = !claimed && earnedCount >= need;
                UISurface.Card(card, ready ? theme.surfaceRaised : theme.surfaceCard,
                    ready ? theme.accentAmber : (claimed ? theme.accentMint : theme.surfaceBorder));

                float actionW = stacked ? 180f : card.width - 32f;
                float actionH = Mathf.Max(UIScale.MinTouchHeight, 56f);
                Rect action = stacked
                    ? new Rect(card.xMax - actionW - 16f, card.center.y - actionH * 0.5f, actionW, actionH)
                    : new Rect(card.x + 16f, card.yMax - actionH - 12f, actionW, actionH);
                float textW = stacked ? action.x - card.x - 32f : card.width - 32f;

                msHeadStyle.normal.textColor = claimed ? theme.textSecondary : theme.accentAmber;
                UIHelper.LabelFit(new Rect(card.x + 16f, card.y + 8f, textW, 38f), milestoneHeader[i], msHeadStyle);
                msRewardStyle.normal.textColor = claimed ? theme.textMuted : theme.textPrimary;
                float rewardBottom = stacked ? card.yMax - 8f : action.y - 4f;
                UIHelper.LabelFit(new Rect(card.x + 16f, card.y + 46f, textW, Mathf.Max(30f, rewardBottom - card.y - 46f)),
                    milestoneReward[i], msRewardStyle);

                if (claimed)
                {
                    UISurface.Chip(new Rect(action.center.x - 60f, action.center.y - 20f, 120f, 40f), "받음",
                        Color.Lerp(theme.accentMint, theme.surfaceCard, 0.45f), theme.textPrimary);
                }
                else if (ready)
                {
                    buttonStyle.normal.textColor = theme.surfaceBase;
                    if (UISurface.Button(action, "받기", theme.accentAmber, buttonStyle)) Claim(i);
                }
                else
                {
                    float barW = action.width;
                    Rect bar = new Rect(action.x, action.center.y + 14f, barW, 8f);
                    UISurface.Flat(bar, theme.surfaceBorder);
                    UISurface.Flat(new Rect(bar.x, bar.y, barW * Mathf.Clamp01(earnedCount / (float)need), bar.height), theme.accentMint);
                    msProgressStyle.normal.textColor = theme.textSecondary;
                    GUI.Label(new Rect(action.x, action.center.y - 26f, action.width, 36f), milestoneProgress[i], msProgressStyle);
                }
            }
        }

        private Color RegionTheme(int index)
        {
            RegionData region = regionManager != null && table != null && index >= 0 && index < table.Length
                ? regionManager.GetRegionById(table[index].regionId)
                : null;
            return region != null ? region.themeColor : UITheme.Instance.accentAmber;
        }
    }
}
