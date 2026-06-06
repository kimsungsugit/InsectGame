using InsectGame.Core;
using InsectGame.Dex;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class PlayerStatusHUD : MonoBehaviour
    {
        [SerializeField] private PlayerProgressController progress;
        [SerializeField] private PlayerCandyInventory candyInventory;
        [SerializeField] private PlayerCurrencyWallet currencyWallet;
        [SerializeField] private PlayerInsectCollection insectCollection;
        [SerializeField] private PlayerItemInventory itemInventory;
        [SerializeField] private DexController dexController;
        [SerializeField] private BattleTeamManager teamManager;
        [SerializeField] private RegionManager regionManager;

        // GUIStyle 캐싱
        private GUIStyle sectionTitleStyle;
        private GUIStyle levelBadgeLabelStyle;
        private GUIStyle levelBadgeNumStyle;
        private GUIStyle xpTitleStyle;
        private GUIStyle xpTextStyle;
        private GUIStyle xpPctStyle;
        private GUIStyle regionNameStyle;
        private GUIStyle regionSubStyle;
        private GUIStyle statBoxLblStyle;
        private GUIStyle statBoxValStyle;
        private GUIStyle toggleStyle;
        private GUIStyle alertNameStyle;
        private GUIStyle alertDescStyle;
        private bool stylesInitialized;

        private bool expanded = true;
        private float xpBarAnim;
        private float toggleAnim = 1f;

        private string subAreaAlertName;
        private string subAreaAlertDesc;
        private float subAreaAlertTimer;
        private bool subscribedSubArea;

        // OnGUI 매 프레임 new Color 회피용 (alpha/scaled 동적 값 제외).
        private static readonly Color PanelBgCol = new Color(0.03f, 0.04f, 0.08f, 0.92f);
        private static readonly Color PanelAccentBlueCol = new Color(0.3f, 0.6f, 1f);
        private static readonly Color PanelDividerCol = new Color(0.15f, 0.18f, 0.25f);
        private static readonly Color LvBadgeBgDarkCol = new Color(0.15f, 0.25f, 0.5f);
        private static readonly Color LvBadgeAccentCol = new Color(0.3f, 0.6f, 1f);
        private static readonly Color XpBarBgCol = new Color(0.08f, 0.08f, 0.12f);
        private static readonly Color XpBarFillDarkCol = new Color(0.2f, 0.5f, 0.9f);
        private static readonly Color XpBarFillLightCol = new Color(0.3f, 0.65f, 1f);
        private static readonly Color StatCandyPinkCol = new Color(1f, 0.7f, 0.85f);
        private static readonly Color StatCoinGoldCol = new Color(1f, 0.85f, 0.3f);
        private static readonly Color StatGemBlueCol = new Color(0.4f, 0.7f, 1f);
        private static readonly Color StatTeamOrangeCol = new Color(1f, 0.6f, 0.3f);
        private static readonly Color StatOwnedGreenCol = new Color(0.4f, 0.85f, 0.5f);
        private static readonly Color StatDiscoveredBlueCol = new Color(0.6f, 0.8f, 1f);
        private static readonly Color StatCapturedGoldCol = new Color(1f, 0.85f, 0.3f);
        private static readonly Color RegionDefaultCol = new Color(0.6f, 0.7f, 0.8f);

        // GetAllOwned 캐싱 — DrawCollectionSection 매 프레임 호출 회피 (CollectionUI 패턴).
        private int cachedOwnedCount;
        private bool ownedCountCacheDirty = true;

        private void HandleInsectUpdated(PlayerInsectData _) { ownedCountCacheDirty = true; }

        private bool subscribedInsects;

        private void OnEnable()
        {
            if (regionManager != null && !subscribedSubArea)
            {
                regionManager.SubAreaChanged += OnSubAreaEntered;
                subscribedSubArea = true;
            }
            if (insectCollection != null && !subscribedInsects)
            {
                insectCollection.InsectUpdated += HandleInsectUpdated;
                subscribedInsects = true;
            }
            ownedCountCacheDirty = true;
        }

        private void OnDisable()
        {
            if (regionManager != null && subscribedSubArea)
                regionManager.SubAreaChanged -= OnSubAreaEntered;
            subscribedSubArea = false;
            if (insectCollection != null && subscribedInsects)
                insectCollection.InsectUpdated -= HandleInsectUpdated;
            subscribedInsects = false;
        }

        private void OnSubAreaEntered(SubAreaData subArea)
        {
            if (subArea != null)
            {
                subAreaAlertName = subArea.displayName;
                subAreaAlertDesc = subArea.description ?? "";
                subAreaAlertTimer = 3.5f;
            }
        }

        private void Update()
        {
            float target = expanded ? 1f : 0f;
            toggleAnim = Mathf.MoveTowards(toggleAnim, target, Time.deltaTime * 6f);

            if (progress != null)
            {
                float xpRatio = progress.XpToNextLevel > 0
                    ? (float)progress.CurrentXp / progress.XpToNextLevel : 0f;
                xpBarAnim = Mathf.MoveTowards(xpBarAnim, xpRatio, Time.deltaTime * 2f);
            }

            if (subAreaAlertTimer > 0f)
                subAreaAlertTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            UIScale.Begin();
            DrawSubAreaAlert();

            if (progress == null) { UIScale.End(); return; }

            InitStyles();

            float panelW = 480f;
            float panelH = 540f;
            float margin = 20f;
            float slideX = Mathf.Lerp(-panelW + 50, 0, toggleAnim);
            float px = margin + slideX;
            float py = margin;

            GUI.color = PanelBgCol;
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = PanelAccentBlueCol;
            GUI.DrawTexture(new Rect(px, py, panelW, 4), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(px, py + panelH - 3, panelW, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(px, py, 3, panelH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(px + panelW - 3, py, 3, panelH), Texture2D.whiteTexture);

            GUI.color = Color.white;

            float cy = py + 16;

            DrawLevelSection(px, cy, panelW);
            cy += 135;

            DrawResourceSection(px, cy, panelW);
            cy += 160;

            DrawCollectionSection(px, cy, panelW);
            cy += 85;

            DrawRegionSection(px, cy, panelW);

            Rect toggleRect = new Rect(px + panelW - 46, py + 8, 38, 38);
            GUI.color = PanelDividerCol;
            GUI.DrawTexture(toggleRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(toggleRect, expanded ? "◀" : "▶", toggleStyle);

            Event evt = Event.current;
            if (evt != null && evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (toggleRect.Contains(evt.mousePosition))
                {
                    expanded = !expanded;
                    evt.Use();
                }
            }
            UIScale.End();
        }

        private void DrawLevelSection(float px, float cy, float pw)
        {
            int level = progress.Level;
            int xp = progress.CurrentXp;
            int xpNeeded = progress.XpToNextLevel;

            GUI.Label(new Rect(px + 20, cy, 150, 28), "PLAYER", sectionTitleStyle);

            float lvBadgeX = px + 20;
            float lvBadgeY = cy + 32;

            GUI.color = LvBadgeBgDarkCol;
            GUI.DrawTexture(new Rect(lvBadgeX, lvBadgeY, 84, 60), Texture2D.whiteTexture);
            GUI.color = LvBadgeAccentCol;
            GUI.DrawTexture(new Rect(lvBadgeX, lvBadgeY, 84, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(lvBadgeX, lvBadgeY + 57, 84, 3), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(lvBadgeX, lvBadgeY + 2, 84, 22), "LEVEL", levelBadgeLabelStyle);
            GUI.Label(new Rect(lvBadgeX, lvBadgeY + 20, 84, 40), level.ToString(), levelBadgeNumStyle);

            float barX = lvBadgeX + 100;
            float barW = pw - 140;
            float barH = 26f;
            float barY = lvBadgeY + 6;

            GUI.Label(new Rect(barX, lvBadgeY - 4, barW, 24), "경험치 (EXP)", xpTitleStyle);

            GUI.color = XpBarBgCol;
            GUI.DrawTexture(new Rect(barX, barY + 24, barW, barH), Texture2D.whiteTexture);

            if (xpBarAnim > 0)
            {
                GUI.color = XpBarFillDarkCol;
                GUI.DrawTexture(new Rect(barX, barY + 24 + barH / 2, barW * xpBarAnim, barH / 2), Texture2D.whiteTexture);
                GUI.color = XpBarFillLightCol;
                GUI.DrawTexture(new Rect(barX, barY + 24, barW * xpBarAnim, barH / 2), Texture2D.whiteTexture);

                float shine = Mathf.Sin(Time.time * 2f) * 0.15f;
                if (shine > 0)
                {
                    GUI.color = new Color(1f, 1f, 1f, shine);
                    GUI.DrawTexture(new Rect(barX, barY + 24, barW * xpBarAnim, barH), Texture2D.whiteTexture);
                }
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(barX, barY + 24, barW, barH), $"{xp} / {xpNeeded}", xpTextStyle);

            int percent = xpNeeded > 0 ? Mathf.RoundToInt((float)xp / xpNeeded * 100f) : 100;
            GUI.Label(new Rect(barX, barY + 52, barW, 24), $"{percent}%", xpPctStyle);
        }

        private void DrawResourceSection(float px, float cy, float pw)
        {
            GUI.Label(new Rect(px + 20, cy, 150, 26), "RESOURCES", sectionTitleStyle);

            float halfW = (pw - 56) / 2f;

            // Row 1: 캔디 + 코인
            float row1Y = cy + 32;
            int candies = candyInventory != null ? candyInventory.Candies : 0;
            DrawStatBox(px + 20, row1Y, halfW, 56, "캔디", candies.ToString(), StatCandyPinkCol);
            int coins = currencyWallet != null ? currencyWallet.Coins : 0;
            DrawStatBox(px + 20 + halfW + 14, row1Y, halfW, 56, "코인", coins.ToString(), StatCoinGoldCol);

            // Row 2: 보석 + 배틀팀
            float row2Y = row1Y + 62;
            int gems = currencyWallet != null ? currencyWallet.Gems : 0;
            DrawStatBox(px + 20, row2Y, halfW, 56, "보석", gems.ToString(), StatGemBlueCol);
            int teamCount = teamManager != null ? teamManager.FilledSlots : 0;
            DrawStatBox(px + 20 + halfW + 14, row2Y, halfW, 56, "배틀팀", $"{teamCount}/5", StatTeamOrangeCol);
        }

        private void DrawCollectionSection(float px, float cy, float pw)
        {
            GUI.Label(new Rect(px + 20, cy, 180, 26), "COLLECTION", sectionTitleStyle);

            float rowY = cy + 32;
            float thirdW = (pw - 68) / 3f;

            // GetAllOwned 캐싱 — InsectUpdated 이벤트로 invalidate (매 프레임 List 할당 회피).
            if (ownedCountCacheDirty && insectCollection != null)
            {
                cachedOwnedCount = insectCollection.GetAllOwned().Count;
                ownedCountCacheDirty = false;
            }
            DrawStatBox(px + 20, rowY, thirdW, 56, "보유", cachedOwnedCount.ToString(), StatOwnedGreenCol);

            int discovered = 0;
            int captured = 0;
            if (dexController != null)
            {
                var data = dexController.GetSaveData();
                if (data != null && data.records != null)
                {
                    discovered = data.records.Count;
                    foreach (var r in data.records)
                        if (r.capturedCount > 0) captured++;
                }
            }
            DrawStatBox(px + 20 + thirdW + 12, rowY, thirdW, 56, "발견", discovered.ToString(), StatDiscoveredBlueCol);
            DrawStatBox(px + 20 + (thirdW + 12) * 2, rowY, thirdW, 56, "포획", captured.ToString(), StatCapturedGoldCol);
        }

        private void DrawRegionSection(float px, float cy, float pw)
        {
            GUI.Label(new Rect(px + 20, cy, 150, 26), "LOCATION", sectionTitleStyle);

            string regionName = "탐험 중...";
            Color regionCol = RegionDefaultCol;
            string regionInsects = "";
            if (regionManager != null && regionManager.CurrentRegion != null)
            {
                var r = regionManager.CurrentRegion;
                regionName = r.displayName;
                regionCol = r.themeColor;
                if (r.insectIds != null && r.insectIds.Length > 0)
                    regionInsects = $"출현 곤충: {r.insectIds.Length}종";
            }

            regionNameStyle.normal.textColor = regionCol;
            GUI.Label(new Rect(px + 20, cy + 30, pw - 40, 36), regionName, regionNameStyle);

            // SubArea 안에서는 이름을 상시 표시 (▾ 표시 + 빛바랜 색)
            float subY = cy + 62;
            if (regionManager != null && regionManager.CurrentSubArea != null)
            {
                Color subCol = new Color(regionCol.r * 0.85f + 0.15f, regionCol.g * 0.85f + 0.15f, regionCol.b * 0.85f + 0.15f);
                regionSubStyle.normal.textColor = subCol;
                GUI.Label(new Rect(px + 20, subY, pw - 40, 24), $"▾ {regionManager.CurrentSubArea.displayName}", regionSubStyle);
                subY += 22;
            }

            if (!string.IsNullOrEmpty(regionInsects))
                GUI.Label(new Rect(px + 20, subY, pw - 40, 24), regionInsects, regionSubStyle);
        }

        private void DrawStatBox(float x, float y, float w, float h, string label, string value, Color accent)
        {
            GUI.color = new Color(accent.r * 0.08f, accent.g * 0.08f, accent.b * 0.08f, 0.8f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(x, y, w, 3), Texture2D.whiteTexture);

            statBoxLblStyle.normal.textColor = new Color(accent.r * 0.7f, accent.g * 0.7f, accent.b * 0.7f);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8, y + 4, w - 16, 22), label, statBoxLblStyle);

            statBoxValStyle.normal.textColor = accent;
            GUI.Label(new Rect(x + 8, y + 20, w - 16, 34), value, statBoxValStyle);
        }

        public void AutoWire(PlayerProgressController prog, PlayerCandyInventory candy,
            PlayerInsectCollection collection, PlayerItemInventory items,
            DexController dex, BattleTeamManager team, RegionManager region)
        {
            if (progress == null) progress = prog;
            if (candyInventory == null) candyInventory = candy;
            if (insectCollection == null) insectCollection = collection;
            if (itemInventory == null) itemInventory = items;
            if (dexController == null) dexController = dex;
            if (teamManager == null) teamManager = team;
            if (regionManager == null)
            {
                regionManager = region;
                if (regionManager != null && !subscribedSubArea)
                {
                    regionManager.SubAreaChanged += OnSubAreaEntered;
                    subscribedSubArea = true;
                }
            }
        }

        public void AutoWire(PlayerCurrencyWallet wallet)
        {
            if (currencyWallet == null) currencyWallet = wallet;
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            sectionTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
            sectionTitleStyle.normal.textColor = new Color(0.5f, 0.6f, 0.8f);

            levelBadgeLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            levelBadgeLabelStyle.normal.textColor = new Color(0.5f, 0.7f, 1f);

            levelBadgeNumStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            levelBadgeNumStyle.normal.textColor = Color.white;

            xpTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            xpTitleStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f);

            xpTextStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            xpTextStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);

            xpPctStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleRight };
            xpPctStyle.normal.textColor = new Color(0.5f, 0.65f, 0.9f);

            regionNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };

            regionSubStyle = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            regionSubStyle.normal.textColor = new Color(0.55f, 0.6f, 0.7f);

            statBoxLblStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            statBoxValStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };

            toggleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            toggleStyle.normal.textColor = new Color(0.6f, 0.7f, 0.9f);

            alertNameStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            alertDescStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        }

        private void DrawSubAreaAlert()
        {
            if (subAreaAlertTimer <= 0f) return;

            InitStyles();

            float alpha = Mathf.Clamp01(subAreaAlertTimer / 0.5f);
            float sw = UIScale.VirtualScreenWidth;

            // 배경
            GUI.color = new Color(0f, 0f, 0f, 0.75f * alpha);
            GUI.DrawTexture(new Rect(sw * 0.2f, 70, sw * 0.6f, 72), Texture2D.whiteTexture);
            // 상단 라인
            GUI.color = new Color(1f, 0.85f, 0.3f, 0.8f * alpha);
            GUI.DrawTexture(new Rect(sw * 0.2f, 70, sw * 0.6f, 3), Texture2D.whiteTexture);

            // 서브에리어 이름 (캐시된 스타일 + 알파만 변경)
            alertNameStyle.normal.textColor = new Color(1f, 0.9f, 0.4f, alpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(sw * 0.2f, 74, sw * 0.6f, 38), subAreaAlertName, alertNameStyle);

            // 설명
            alertDescStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, alpha * 0.9f);
            GUI.Label(new Rect(sw * 0.2f, 110, sw * 0.6f, 26), subAreaAlertDesc, alertDescStyle);

            GUI.color = Color.white;
        }
    }
}
