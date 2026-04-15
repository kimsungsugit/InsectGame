using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Dex;
using UnityEngine;

namespace InsectGame.UI
{
    public class RegionMapUI : MonoBehaviour
    {
        [SerializeField] private RegionManager regionManager;
        [SerializeField] private PlayerProgressController progress;
        [SerializeField] private DexController dex;
        [SerializeField] private InsectDatabase database;

        private bool isOpen;
        private string selectedRegionId;
        private Vector2 dexScroll;

        public bool IsOpen => isOpen;
        public void Toggle() { isOpen = !isOpen; if (!isOpen) selectedRegionId = null; }

        private void Update() { }

        private void OnGUI()
        {
            if (!isOpen) return;

            if (selectedRegionId != null)
                DrawRegionDetail();
            else
                DrawMap();
        }

        private void DrawMap()
        {
            float panelW = 1060f;
            float panelH = 900f;
            float px = (Screen.width - panelW) / 2f;
            float py = (Screen.height - panelH) / 2f;

            GUI.color = new Color(0.04f, 0.06f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(0.15f, 0.18f, 0.25f);
            GUI.DrawTexture(new Rect(px, py, panelW, 70), Texture2D.whiteTexture);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Color.white;
            GUI.color = Color.white;
            GUI.Label(new Rect(px, py + 10, panelW - 60, 50), "WORLD MAP", titleStyle);

            GUIStyle closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(px + panelW - 60, py + 10, 50, 50), "X", closeStyle))
            {
                isOpen = false;
                selectedRegionId = null;
            }

            if (regionManager == null || regionManager.Regions == null)
            {
                GUIStyle noData = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
                noData.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
                GUI.Label(new Rect(px, py + 40, panelW, panelH - 40), "No regions available", noData);
                return;
            }

            int playerLv = progress != null ? progress.Level : 1;
            GUIStyle lvStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            lvStyle.normal.textColor = new Color(0.7f, 0.85f, 1f);
            GUI.Label(new Rect(px, py + 72, panelW, 36), $"Player Level: {playerLv}", lvStyle);

            float mapX = px + 30;
            float mapY = py + 116;
            float mapW = panelW - 60;
            float mapH = panelH - 126;

            GUI.color = new Color(0.08f, 0.1f, 0.15f, 0.8f);
            GUI.DrawTexture(new Rect(mapX, mapY, mapW, mapH), Texture2D.whiteTexture);

            DrawMiniMap(mapX, mapY, mapW, mapH);
            DrawRegionCards(mapX, mapY, mapW, mapH);
        }

        private void DrawMiniMap(float mx, float my, float mw, float mh)
        {
            if (regionManager.Regions == null) return;

            float worldMinX = float.MaxValue, worldMaxX = float.MinValue;
            float worldMinZ = float.MaxValue, worldMaxZ = float.MinValue;
            foreach (var r in regionManager.Regions)
            {
                float left = r.centerPosition.x - r.radius;
                float right = r.centerPosition.x + r.radius;
                float bottom = r.centerPosition.z - r.radius;
                float top = r.centerPosition.z + r.radius;
                if (left < worldMinX) worldMinX = left;
                if (right > worldMaxX) worldMaxX = right;
                if (bottom < worldMinZ) worldMinZ = bottom;
                if (top > worldMaxZ) worldMaxZ = top;
            }

            float padding = 30f;
            worldMinX -= padding; worldMaxX += padding;
            worldMinZ -= padding; worldMaxZ += padding;

            float worldW = worldMaxX - worldMinX;
            float worldH = worldMaxZ - worldMinZ;
            if (worldW < 1) worldW = 1;
            if (worldH < 1) worldH = 1;

            float miniH = mh * 0.45f;
            float miniW = mw;
            float miniX = mx;
            float miniY = my;

            foreach (var r in regionManager.Regions)
            {
                bool accessible = regionManager.IsRegionAccessible(r);
                bool isCurrent = regionManager.CurrentRegion == r;

                float nx = (r.centerPosition.x - worldMinX) / worldW;
                float nz = (r.centerPosition.z - worldMinZ) / worldH;
                float nr = r.radius / Mathf.Max(worldW, worldH);

                float cx = miniX + nx * miniW;
                float cy = miniY + (1f - nz) * miniH;
                float cr = nr * Mathf.Min(miniW, miniH);
                cr = Mathf.Max(cr, 20f);

                Color col = r.themeColor;
                GUI.color = new Color(col.r, col.g, col.b, isCurrent ? 0.5f : 0.25f);
                GUI.DrawTexture(new Rect(cx - cr, cy - cr, cr * 2, cr * 2), Texture2D.whiteTexture);

                if (isCurrent)
                {
                    GUI.color = new Color(col.r, col.g, col.b, 0.8f);
                    GUI.DrawTexture(new Rect(cx - cr, cy - cr, cr * 2, 2), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - cr, cy + cr - 2, cr * 2, 2), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - cr, cy - cr, 2, cr * 2), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + cr - 2, cy - cr, 2, cr * 2), Texture2D.whiteTexture);
                }

                GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 26, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                nameStyle.normal.textColor = Color.white;
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 90, cy - 16, 180, 34), r.displayName, nameStyle);

                string diffLabel;
                Color diffColor;
                if (r.requiredLevel <= 2) { diffLabel = "쉬움"; diffColor = new Color(0.4f, 0.9f, 0.5f); }
                else if (r.requiredLevel <= 5) { diffLabel = "보통"; diffColor = new Color(0.9f, 0.8f, 0.3f); }
                else { diffLabel = "어려움"; diffColor = new Color(1f, 0.4f, 0.3f); }

                GUIStyle diffStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                diffStyle.normal.textColor = diffColor;
                GUI.Label(new Rect(cx - 60, cy + 14, 120, 28), $"난이도: {diffLabel}", diffStyle);
            }

            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                float pnx = (player.transform.position.x - worldMinX) / worldW;
                float pnz = (player.transform.position.z - worldMinZ) / worldH;
                float ppx = miniX + pnx * miniW;
                float ppy = miniY + (1f - pnz) * miniH;
                GUI.color = Color.yellow;
                GUI.DrawTexture(new Rect(ppx - 8, ppy - 8, 16, 16), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            float cardY = miniY + miniH + 10;
            float cardH = mh - miniH - 15;
            DrawRegionList(mx, cardY, mw, cardH);
        }

        private void DrawRegionList(float x, float y, float w, float h)
        {
            RegionData[] accessible = regionManager.GetAccessibleRegions();
            if (accessible.Length == 0) return;

            float cardW = (w - 10 * (accessible.Length - 1)) / Mathf.Max(accessible.Length, 1);
            cardW = Mathf.Min(cardW, 290);

            for (int i = 0; i < accessible.Length; i++)
            {
                var r = accessible[i];
                float cx = x + i * (cardW + 10);
                bool isCurrent = regionManager.CurrentRegion == r;

                GUI.color = new Color(r.themeColor.r * 0.2f, r.themeColor.g * 0.2f, r.themeColor.b * 0.2f, 0.7f);
                GUI.DrawTexture(new Rect(cx, y, cardW, h), Texture2D.whiteTexture);

                GUI.color = r.themeColor;
                GUI.DrawTexture(new Rect(cx, y, cardW, 3), Texture2D.whiteTexture);

                if (isCurrent)
                {
                    GUI.color = new Color(r.themeColor.r, r.themeColor.g, r.themeColor.b, 0.15f);
                    GUI.DrawTexture(new Rect(cx, y, cardW, h), Texture2D.whiteTexture);
                }

                GUIStyle ns = new GUIStyle(GUI.skin.label)
                { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                ns.normal.textColor = r.themeColor;
                GUI.color = Color.white;
                GUI.Label(new Rect(cx, y + 12, cardW, 32), r.displayName, ns);

                if (isCurrent)
                {
                    GUIStyle cur = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                    cur.normal.textColor = new Color(0.5f, 1f, 0.5f);
                    GUI.Label(new Rect(cx, y + 44, cardW, 32), "현재 위치", cur);
                }

                string diffLabel;
                Color diffColor;
                if (r.requiredLevel <= 2) { diffLabel = "쉬움"; diffColor = new Color(0.4f, 0.9f, 0.5f); }
                else if (r.requiredLevel <= 5) { diffLabel = "보통"; diffColor = new Color(0.9f, 0.8f, 0.3f); }
                else { diffLabel = "어려움"; diffColor = new Color(1f, 0.4f, 0.3f); }
                GUIStyle diffS = new GUIStyle(GUI.skin.label)
                { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                diffS.normal.textColor = diffColor;
                float diffY = isCurrent ? y + 72 : y + 44;
                GUI.Label(new Rect(cx, diffY, cardW, 28), $"난이도: {diffLabel}", diffS);

                int total = r.insectIds != null ? r.insectIds.Length : 0;
                int caught = CountCaught(r);
                GUIStyle cs = new GUIStyle(GUI.skin.label)
                { fontSize = 26, alignment = TextAnchor.MiddleCenter };
                cs.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(cx, y + h - 72, cardW, 32), $"{caught}/{total}", cs);

                float bar = total > 0 ? (float)caught / total : 0;
                float barW = cardW - 30;
                GUI.color = new Color(0.15f, 0.15f, 0.2f);
                GUI.DrawTexture(new Rect(cx + 10, y + h - 36, barW, 12), Texture2D.whiteTexture);
                GUI.color = new Color(0.3f, 0.8f, 0.3f);
                GUI.DrawTexture(new Rect(cx + 10, y + h - 36, barW * bar, 12), Texture2D.whiteTexture);

                GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 24 };
                GUI.backgroundColor = new Color(0.2f, 0.3f, 0.5f);
                GUI.color = Color.white;
                if (GUI.Button(new Rect(cx + 5, y + h - 38 + 20, cardW - 10, 38), "도감", btnStyle))
                    selectedRegionId = r.regionId;
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawRegionCards(float mx, float my, float mw, float mh) { }

        private void DrawRegionDetail()
        {
            RegionData region = regionManager.GetRegionById(selectedRegionId);
            if (region == null) { selectedRegionId = null; return; }

            float panelW = 1060f;
            float panelH = 900f;
            float px = (Screen.width - panelW) / 2f;
            float py = (Screen.height - panelH) / 2f;

            GUI.color = new Color(0.04f, 0.06f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(region.themeColor.r * 0.3f, region.themeColor.g * 0.3f, region.themeColor.b * 0.3f, 1f);
            GUI.DrawTexture(new Rect(px, py, panelW, 70), Texture2D.whiteTexture);
            GUI.color = region.themeColor;
            GUI.DrawTexture(new Rect(px, py + 70, panelW, 5), Texture2D.whiteTexture);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = region.themeColor;
            GUI.color = Color.white;
            GUI.Label(new Rect(px + 140, py + 10, panelW - 280, 50), $"{region.displayName} Dex", titleStyle);

            GUIStyle backStyle = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(px + 12, py + 12, 120, 46), "< Back", backStyle))
                selectedRegionId = null;

            GUIStyle closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(px + panelW - 60, py + 12, 50, 46), "X", closeStyle))
            {
                isOpen = false;
                selectedRegionId = null;
            }

            if (!string.IsNullOrEmpty(region.description))
            {
                GUIStyle descStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 24, wordWrap = true, alignment = TextAnchor.MiddleCenter };
                descStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                GUI.Label(new Rect(px + 30, py + 78, panelW - 60, 34), region.description, descStyle);
            }

            int total = region.insectIds != null ? region.insectIds.Length : 0;
            int caught = CountCaught(region);
            GUIStyle summaryStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            summaryStyle.normal.textColor = new Color(0.7f, 0.85f, 1f);
            GUI.Label(new Rect(px, py + 118, panelW, 36), $"Captured: {caught} / {total}", summaryStyle);

            float barX = px + panelW * 0.2f;
            float barW = panelW * 0.6f;
            float barY = py + 160;
            GUI.color = new Color(0.15f, 0.15f, 0.2f);
            GUI.DrawTexture(new Rect(barX, barY, barW, 16), Texture2D.whiteTexture);
            float fill = total > 0 ? (float)caught / total : 0;
            GUI.color = region.themeColor;
            GUI.DrawTexture(new Rect(barX, barY, barW * fill, 16), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float listY = py + 186;
            float listH = panelH - 196;
            Rect listArea = new Rect(px + 15, listY, panelW - 30, listH);

            if (region.insectIds == null || region.insectIds.Length == 0)
            {
                GUIStyle noInsect = new GUIStyle(GUI.skin.label)
                { fontSize = 15, alignment = TextAnchor.MiddleCenter };
                noInsect.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
                GUI.Label(listArea, "No insects registered", noInsect);
                return;
            }

            float itemH = 120f;
            float totalListH = region.insectIds.Length * itemH;
            Rect viewRect = new Rect(0, 0, listArea.width - 20, totalListH);

            dexScroll = GUI.BeginScrollView(listArea, dexScroll, viewRect);
            for (int i = 0; i < region.insectIds.Length; i++)
            {
                DrawDexItem(new Rect(0, i * itemH, viewRect.width, itemH - 4), region.insectIds[i], region.themeColor);
            }
            GUI.EndScrollView();
        }

        private void DrawDexItem(Rect rect, string insectId, Color themeCol)
        {
            InsectData data = FindInsectData(insectId);
            bool isCaught = dex != null && dex.HasRecord(insectId);

            GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.85f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            if (isCaught && data != null)
            {
                Color rarityCol = UITheme.Instance.GetInsectRarityColor(data.rarity);
                GUI.color = rarityCol;
                GUI.DrawTexture(new Rect(rect.x, rect.y, 6, rect.height), Texture2D.whiteTexture);

                CapturePopupUI.DrawTypedInsectPortrait(rect.x + 62, rect.y + rect.height / 2f, data.insectId, data.rarity, 1f);

                GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 28, fontStyle = FontStyle.Bold };
                nameStyle.normal.textColor = rarityCol;
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x + 110, rect.y + 14, rect.width - 140, 36), data.displayName, nameStyle);

                GUIStyle infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 26 };
                infoStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                GUI.Label(new Rect(rect.x + 110, rect.y + 50, rect.width - 140, 34),
                    $"{data.rarity}  |  CP {PlayerInsectCombatPower.CalculateBasePreview(data, data.minLevel)}", infoStyle);

                GUIStyle checkStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
                checkStyle.normal.textColor = new Color(0.3f, 1f, 0.5f);
                GUI.Label(new Rect(rect.x + rect.width - 60, rect.y + 18, 50, 50), "V", checkStyle);
            }
            else
            {
                GUI.color = new Color(0.3f, 0.3f, 0.3f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 6, rect.height), Texture2D.whiteTexture);

                GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                GUI.DrawTexture(new Rect(rect.x + 24, rect.y + rect.height / 2f - 30, 60, 60), Texture2D.whiteTexture);

                GUIStyle unknownStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                unknownStyle.normal.textColor = new Color(0.35f, 0.35f, 0.35f);
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x + 24, rect.y + rect.height / 2f - 30, 60, 60), "?", unknownStyle);

                GUIStyle hiddenName = new GUIStyle(GUI.skin.label)
                { fontSize = 28, fontStyle = FontStyle.Italic };
                hiddenName.normal.textColor = new Color(0.4f, 0.4f, 0.4f);
                string displayHint = data != null ? new string('?', data.displayName.Length) : "???";
                GUI.Label(new Rect(rect.x + 110, rect.y + 22, rect.width - 140, 36), displayHint, hiddenName);

                GUIStyle notCaught = new GUIStyle(GUI.skin.label) { fontSize = 26 };
                notCaught.normal.textColor = new Color(0.35f, 0.35f, 0.35f);
                GUI.Label(new Rect(rect.x + 110, rect.y + 58, rect.width - 140, 34), "아직 포획하지 않음", notCaught);
            }
        }

        private int CountCaught(RegionData region)
        {
            if (region == null || region.insectIds == null || dex == null) return 0;
            int count = 0;
            foreach (string id in region.insectIds)
            {
                if (dex.HasRecord(id)) count++;
            }
            return count;
        }

        private InsectData FindInsectData(string insectId)
        {
            if (database == null || database.insects == null) return null;
            foreach (var d in database.insects)
            {
                if (d != null && d.insectId == insectId) return d;
            }
            return null;
        }

        public void AutoWire(RegionManager rm, PlayerProgressController prog, DexController dexCtrl, InsectDatabase db)
        {
            if (regionManager == null) regionManager = rm;
            if (progress == null) progress = prog;
            if (dex == null) dex = dexCtrl;
            if (database == null) database = db;
        }
    }
}
