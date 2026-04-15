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
        [SerializeField] private PlayerInsectCollection insectCollection;
        [SerializeField] private PlayerItemInventory itemInventory;
        [SerializeField] private DexController dexController;
        [SerializeField] private BattleTeamManager teamManager;
        [SerializeField] private RegionManager regionManager;

        private bool expanded = true;
        private float xpBarAnim;
        private float toggleAnim = 1f;

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
        }

        private void OnGUI()
        {
            if (progress == null) return;

            float panelW = 480f;
            float panelH = 470f;
            float margin = 20f;
            float slideX = Mathf.Lerp(-panelW + 50, 0, toggleAnim);
            float px = margin + slideX;
            float py = margin;

            GUI.color = new Color(0.03f, 0.04f, 0.08f, 0.92f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(0.3f, 0.6f, 1f);
            GUI.DrawTexture(new Rect(px, py, panelW, 4), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(px, py + panelH - 3, panelW, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(px, py, 3, panelH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(px + panelW - 3, py, 3, panelH), Texture2D.whiteTexture);

            GUI.color = Color.white;

            float cy = py + 16;

            DrawLevelSection(px, cy, panelW);
            cy += 135;

            DrawResourceSection(px, cy, panelW);
            cy += 100;

            DrawCollectionSection(px, cy, panelW);
            cy += 85;

            DrawRegionSection(px, cy, panelW);

            Rect toggleRect = new Rect(px + panelW - 46, py + 8, 38, 38);
            GUI.color = new Color(0.15f, 0.18f, 0.25f);
            GUI.DrawTexture(toggleRect, Texture2D.whiteTexture);
            GUIStyle toggleS = new GUIStyle(GUI.skin.label)
            { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            toggleS.normal.textColor = new Color(0.6f, 0.7f, 0.9f);
            GUI.color = Color.white;
            GUI.Label(toggleRect, expanded ? "◀" : "▶", toggleS);

            Event evt = Event.current;
            if (evt != null && evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (toggleRect.Contains(evt.mousePosition))
                {
                    expanded = !expanded;
                    evt.Use();
                }
            }
        }

        private void DrawLevelSection(float px, float cy, float pw)
        {
            int level = progress.Level;
            int xp = progress.CurrentXp;
            int xpNeeded = progress.XpToNextLevel;

            GUIStyle titleS = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold };
            titleS.normal.textColor = new Color(0.5f, 0.6f, 0.8f);
            GUI.Label(new Rect(px + 20, cy, 150, 28), "PLAYER", titleS);

            float lvBadgeX = px + 20;
            float lvBadgeY = cy + 32;

            GUI.color = new Color(0.15f, 0.25f, 0.5f);
            GUI.DrawTexture(new Rect(lvBadgeX, lvBadgeY, 84, 60), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.6f, 1f);
            GUI.DrawTexture(new Rect(lvBadgeX, lvBadgeY, 84, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(lvBadgeX, lvBadgeY + 57, 84, 3), Texture2D.whiteTexture);

            GUIStyle lvLabel = new GUIStyle(GUI.skin.label)
            { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            lvLabel.normal.textColor = new Color(0.5f, 0.7f, 1f);
            GUI.color = Color.white;
            GUI.Label(new Rect(lvBadgeX, lvBadgeY + 2, 84, 22), "LEVEL", lvLabel);

            GUIStyle lvNum = new GUIStyle(GUI.skin.label)
            { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            lvNum.normal.textColor = Color.white;
            GUI.Label(new Rect(lvBadgeX, lvBadgeY + 20, 84, 40), level.ToString(), lvNum);

            float barX = lvBadgeX + 100;
            float barW = pw - 140;
            float barH = 26f;
            float barY = lvBadgeY + 6;

            GUIStyle xpTitle = new GUIStyle(GUI.skin.label)
            { fontSize = 18, fontStyle = FontStyle.Bold };
            xpTitle.normal.textColor = new Color(0.7f, 0.8f, 0.9f);
            GUI.Label(new Rect(barX, lvBadgeY - 4, barW, 24), "경험치 (EXP)", xpTitle);

            GUI.color = new Color(0.08f, 0.08f, 0.12f);
            GUI.DrawTexture(new Rect(barX, barY + 24, barW, barH), Texture2D.whiteTexture);

            if (xpBarAnim > 0)
            {
                GUI.color = new Color(0.2f, 0.5f, 0.9f);
                GUI.DrawTexture(new Rect(barX, barY + 24 + barH / 2, barW * xpBarAnim, barH / 2), Texture2D.whiteTexture);
                GUI.color = new Color(0.3f, 0.65f, 1f);
                GUI.DrawTexture(new Rect(barX, barY + 24, barW * xpBarAnim, barH / 2), Texture2D.whiteTexture);

                float shine = Mathf.Sin(Time.time * 2f) * 0.15f;
                if (shine > 0)
                {
                    GUI.color = new Color(1f, 1f, 1f, shine);
                    GUI.DrawTexture(new Rect(barX, barY + 24, barW * xpBarAnim, barH), Texture2D.whiteTexture);
                }
            }

            GUIStyle xpText = new GUIStyle(GUI.skin.label)
            { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            xpText.normal.textColor = new Color(0.9f, 0.95f, 1f);
            GUI.color = Color.white;
            GUI.Label(new Rect(barX, barY + 24, barW, barH), $"{xp} / {xpNeeded}", xpText);

            int percent = xpNeeded > 0 ? Mathf.RoundToInt((float)xp / xpNeeded * 100f) : 100;
            GUIStyle pctS = new GUIStyle(GUI.skin.label)
            { fontSize = 17, alignment = TextAnchor.MiddleRight };
            pctS.normal.textColor = new Color(0.5f, 0.65f, 0.9f);
            GUI.Label(new Rect(barX, barY + 52, barW, 24), $"{percent}%", pctS);
        }

        private void DrawResourceSection(float px, float cy, float pw)
        {
            GUIStyle secTitle = new GUIStyle(GUI.skin.label)
            { fontSize = 19, fontStyle = FontStyle.Bold };
            secTitle.normal.textColor = new Color(0.5f, 0.6f, 0.8f);
            GUI.Label(new Rect(px + 20, cy, 150, 26), "RESOURCES", secTitle);

            float rowY = cy + 32;
            float halfW = (pw - 56) / 2f;

            int candies = candyInventory != null ? candyInventory.Candies : 0;
            DrawStatBox(px + 20, rowY, halfW, 56, "캔디", candies.ToString(), new Color(1f, 0.7f, 0.85f));

            int teamCount = teamManager != null ? teamManager.FilledSlots : 0;
            DrawStatBox(px + 20 + halfW + 14, rowY, halfW, 56, "배틀팀", $"{teamCount}/5", new Color(1f, 0.6f, 0.3f));
        }

        private void DrawCollectionSection(float px, float cy, float pw)
        {
            GUIStyle secTitle = new GUIStyle(GUI.skin.label)
            { fontSize = 19, fontStyle = FontStyle.Bold };
            secTitle.normal.textColor = new Color(0.5f, 0.6f, 0.8f);
            GUI.Label(new Rect(px + 20, cy, 180, 26), "COLLECTION", secTitle);

            float rowY = cy + 32;
            float thirdW = (pw - 68) / 3f;

            int owned = insectCollection != null ? insectCollection.GetAllOwned().Count : 0;
            DrawStatBox(px + 20, rowY, thirdW, 56, "보유", owned.ToString(), new Color(0.4f, 0.85f, 0.5f));

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
            DrawStatBox(px + 20 + thirdW + 12, rowY, thirdW, 56, "발견", discovered.ToString(), new Color(0.6f, 0.8f, 1f));
            DrawStatBox(px + 20 + (thirdW + 12) * 2, rowY, thirdW, 56, "포획", captured.ToString(), new Color(1f, 0.85f, 0.3f));
        }

        private void DrawRegionSection(float px, float cy, float pw)
        {
            GUIStyle secTitle = new GUIStyle(GUI.skin.label)
            { fontSize = 19, fontStyle = FontStyle.Bold };
            secTitle.normal.textColor = new Color(0.5f, 0.6f, 0.8f);
            GUI.Label(new Rect(px + 20, cy, 150, 26), "LOCATION", secTitle);

            string regionName = "탐험 중...";
            Color regionCol = new Color(0.6f, 0.7f, 0.8f);
            string regionInsects = "";
            if (regionManager != null && regionManager.CurrentRegion != null)
            {
                var r = regionManager.CurrentRegion;
                regionName = r.displayName;
                regionCol = r.themeColor;
                if (r.insectIds != null && r.insectIds.Length > 0)
                    regionInsects = $"출현 곤충: {r.insectIds.Length}종";
            }

            GUIStyle regS = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold };
            regS.normal.textColor = regionCol;
            GUI.Label(new Rect(px + 20, cy + 30, pw - 40, 36), regionName, regS);

            if (!string.IsNullOrEmpty(regionInsects))
            {
                GUIStyle subS = new GUIStyle(GUI.skin.label) { fontSize = 17 };
                subS.normal.textColor = new Color(0.55f, 0.6f, 0.7f);
                GUI.Label(new Rect(px + 20, cy + 62, pw - 40, 24), regionInsects, subS);
            }
        }

        private void DrawStatBox(float x, float y, float w, float h, string label, string value, Color accent)
        {
            GUI.color = new Color(accent.r * 0.08f, accent.g * 0.08f, accent.b * 0.08f, 0.8f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(x, y, w, 3), Texture2D.whiteTexture);

            GUIStyle lbl = new GUIStyle(GUI.skin.label)
            { fontSize = 16 };
            lbl.normal.textColor = new Color(accent.r * 0.7f, accent.g * 0.7f, accent.b * 0.7f);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8, y + 4, w - 16, 22), label, lbl);

            GUIStyle val = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            val.normal.textColor = accent;
            GUI.Label(new Rect(x + 8, y + 20, w - 16, 34), value, val);
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
            if (regionManager == null) regionManager = region;
        }
    }
}
