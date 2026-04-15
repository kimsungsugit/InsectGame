using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class CollectionUI : MonoBehaviour
    {
        [SerializeField] private PlayerInsectCollection insectCollection;
        [SerializeField] private PlayerCandyInventory candyInventory;
        [SerializeField] private PlayerProgressController progressController;

        private bool isOpen;
        private Vector2 scrollPos;
        private int selectedTab;

        private string selectedInstanceId;
        private readonly string[] tabNames = { "보유 곤충", "통계" };

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            if (!isOpen) selectedInstanceId = null;
            if (isOpen && TutorialQuestManager.Instance != null)
                TutorialQuestManager.Instance.NotifyCollectionOpened();
        }

        private void Update() { }

        private void OnGUI()
        {
            DrawToggleButton();

            if (!isOpen) return;

            if (selectedInstanceId != null)
                DrawDetailPanel();
            else
                DrawPanel();
        }

        private void DrawToggleButton()
        {
        }

        private void DrawPanel()
        {
            float panelW = 900f;
            float panelH = 820f;
            float panelX = Screen.width - panelW - 24f;
            float panelY = 24f;

            GUI.color = new Color(0.05f, 0.07f, 0.12f, 0.95f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(0.15f, 0.18f, 0.25f, 1f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 72), Texture2D.whiteTexture);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Color.white;
            GUI.color = Color.white;
            GUI.Label(new Rect(panelX, panelY + 12, panelW - 70, 50), "컬렉션", titleStyle);

            GUIStyle closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 34, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(panelX + panelW - 60, panelY + 12, 50, 50), "X", closeStyle))
            {
                isOpen = false;
                selectedInstanceId = null;
            }

            float tabY = panelY + 80;
            for (int i = 0; i < tabNames.Length; i++)
            {
                float tabX = panelX + i * 260 + 20;
                bool active = selectedTab == i;
                GUIStyle tabStyle = new GUIStyle(GUI.skin.button)
                { fontSize = 30, fontStyle = active ? FontStyle.Bold : FontStyle.Normal };
                GUI.backgroundColor = active ? new Color(0.3f, 0.5f, 0.9f) : new Color(0.2f, 0.2f, 0.3f);
                if (GUI.Button(new Rect(tabX, tabY, 240, 56), tabNames[i], tabStyle))
                    selectedTab = i;
            }
            GUI.backgroundColor = Color.white;
            GUI.color = Color.white;

            float contentY = tabY + 66;
            float contentH = panelH - (contentY - panelY) - 14;
            Rect contentRect = new Rect(panelX + 16, contentY, panelW - 32, contentH);

            if (selectedTab == 0)
                DrawInsectList(contentRect);
            else
                DrawStats(contentRect);
        }

        private void DrawInsectList(Rect area)
        {
            if (insectCollection == null)
            {
                DrawCenteredLabel(area, "데이터 없음", new Color(0.5f, 0.5f, 0.5f));
                return;
            }

            List<PlayerInsectData> owned = insectCollection.GetAllOwned();
            if (owned.Count == 0)
            {
                DrawCenteredLabel(area, "아직 포획한 곤충이 없습니다!\n곤충에 가까이 가서 E키를 누르세요", new Color(0.6f, 0.6f, 0.6f));
                return;
            }

            float itemH = 130f;
            float totalH = owned.Count * itemH;
            Rect viewRect = new Rect(0, 0, area.width - 24, totalH);

            scrollPos = GUI.BeginScrollView(area, scrollPos, viewRect);
            for (int i = 0; i < owned.Count; i++)
            {
                PlayerInsectData pid = owned[i];
                InsectData data = insectCollection.GetInsectData(pid.insectId);
                if (DrawInsectItem(new Rect(0, i * itemH, viewRect.width, itemH - 4), pid, data))
                    selectedInstanceId = pid.instanceId;
            }
            GUI.EndScrollView();
        }

        private bool DrawInsectItem(Rect rect, PlayerInsectData pid, InsectData data)
        {
            bool clicked = false;
            Color rarityColor = data != null ? GetRarityColor(data.rarity) : Color.gray;
            int rarityTier = data != null ? (int)data.rarity : 0;

            GUI.color = new Color(0.12f, 0.14f, 0.2f, 0.92f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            UIHelper.DrawRarityBorder(rect, rarityTier, Time.time);

            if (data != null)
                CapturePopupUI.DrawTypedInsectPortrait(rect.x + 60, rect.y + rect.height / 2f + 2, data.insectId, data.rarity, 1f);

            string displayName = GetOwnedDisplayName(pid, data);
            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 32, fontStyle = FontStyle.Bold };
            nameStyle.normal.textColor = rarityColor;
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 110, rect.y + 10, rect.width - 260, 40), displayName, nameStyle);

            GUIStyle infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 26 };
            infoStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);
            string rarityStr = data != null ? data.rarity.ToString() : "?";
            GUI.Label(new Rect(rect.x + 110, rect.y + 52, rect.width - 140, 32),
                $"Lv.{pid.level}  |  {rarityStr}  |  IV: {pid.IVPercent * 100:0}%", infoStyle);

            string gradeStr = CapturePopupUI.GetGradeLabel(pid.Grade);
            Color gradeCol = UITheme.Instance.GetGradeColor(pid.Grade);
            GUIStyle gradeStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            gradeStyle.normal.textColor = gradeCol;
            GUI.Label(new Rect(rect.x + rect.width - 140, rect.y + 8, 120, 42), gradeStr, gradeStyle);

            if (data != null)
            {
                GUIStyle statMini = new GUIStyle(GUI.skin.label)
                { fontSize = 22, alignment = TextAnchor.MiddleRight };
                statMini.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                GUI.Label(new Rect(rect.x + rect.width - 200, rect.y + 50, 180, 28),
                    $"HP:{pid.ivHp} ATK:{pid.ivAtk} DEF:{pid.ivDef}", statMini);
            }

            GUIStyle viewStyle = new GUIStyle(GUI.skin.button) { fontSize = 26 };
            GUI.backgroundColor = new Color(0.25f, 0.35f, 0.55f);
            if (GUI.Button(new Rect(rect.x + rect.width - 130, rect.y + rect.height - 50, 120, 44), "상세", viewStyle))
                clicked = true;
            GUI.backgroundColor = Color.white;

            return clicked;
        }

        private void DrawDetailPanel()
        {
            if (insectCollection == null) { selectedInstanceId = null; return; }

            PlayerInsectData pid = insectCollection.GetByInstanceId(selectedInstanceId);
            if (pid == null) { selectedInstanceId = null; return; }

            InsectData data = insectCollection.GetInsectData(pid.insectId);

            float panelW = 900f;
            float panelH = 820f;
            float panelX = Screen.width - panelW - 24f;
            float panelY = 24f;

            GUI.color = new Color(0.05f, 0.07f, 0.12f, 0.95f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            Color rarityCol = data != null ? GetRarityColor(data.rarity) : Color.gray;
            int detailRarityTier = data != null ? (int)data.rarity : 0;
            GUI.color = Color.white;

            Rect detailRect = new Rect(panelX, panelY, panelW, panelH);
            UIHelper.DrawRarityBorder(detailRect, detailRarityTier, Time.time);
            if (detailRarityTier >= 3)
                UIHelper.DrawRarityGlow(detailRect, rarityCol, detailRarityTier >= 4 ? 0.6f : 0.3f, Time.time);
            GUIStyle backStyle = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(panelX + 14, panelY + 14, 130, 50), "< 뒤로", backStyle))
                selectedInstanceId = null;

            GUIStyle closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 34, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(panelX + panelW - 60, panelY + 14, 50, 50), "X", closeStyle))
            {
                isOpen = false;
                selectedInstanceId = null;
            }

            float portraitCx = panelX + panelW / 2f;
            float portraitCy = panelY + 140;

            GUI.color = new Color(rarityCol.r * 0.15f, rarityCol.g * 0.15f, rarityCol.b * 0.15f, 0.6f);
            GUI.DrawTexture(new Rect(portraitCx - 80, portraitCy - 80, 160, 160), Texture2D.whiteTexture);

            GUI.color = new Color(rarityCol.r, rarityCol.g, rarityCol.b, 0.2f);
            GUI.DrawTexture(new Rect(portraitCx - 75, portraitCy - 75, 150, 150), Texture2D.whiteTexture);

            InsectRarity rarity = data != null ? data.rarity : InsectRarity.Common;
            string insId = data != null ? data.insectId : pid.insectId;
            CapturePopupUI.DrawTypedInsectPortrait(portraitCx, portraitCy, insId, rarity, 1f);

            string displayName = GetOwnedDisplayName(pid, data);
            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            nameStyle.normal.textColor = rarityCol;
            GUI.color = Color.white;
            GUI.Label(new Rect(panelX, panelY + 230, panelW, 50), displayName, nameStyle);

            GUIStyle rarityStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            rarityStyle.normal.textColor = new Color(rarityCol.r, rarityCol.g, rarityCol.b, 0.8f);
            GUI.Label(new Rect(panelX, panelY + 280, panelW, 34),
                data != null ? data.rarity.ToString() : "Unknown", rarityStyle);

            Color gradeCol = UITheme.Instance.GetGradeColor(pid.Grade);
            string gradeLabel = CapturePopupUI.GetGradeLabel(pid.Grade);

            GUIStyle gradeDisp = new GUIStyle(GUI.skin.label)
            { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            gradeDisp.normal.textColor = gradeCol;
            GUI.Label(new Rect(panelX + panelW - 130, panelY + 224, 100, 68), gradeLabel, gradeDisp);

            GUIStyle gradePerc = new GUIStyle(GUI.skin.label)
            { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            gradePerc.normal.textColor = new Color(gradeCol.r, gradeCol.g, gradeCol.b, 0.7f);
            GUI.Label(new Rect(panelX + panelW - 130, panelY + 290, 100, 30),
                $"{pid.IVPercent * 100:0}%", gradePerc);

            float statY = panelY + 330;

            DrawLevelUpSection(panelX + 30, statY, panelW - 60, pid, data);

            float statBlockY = statY + 130;
            float statBlockH = 210f;
            GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.8f);
            GUI.DrawTexture(new Rect(panelX + 30, statBlockY, panelW - 60, statBlockH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float sx = panelX + 46;
            float sw = panelW - 92;

            float barY = statBlockY + 16;
            int bHp = data != null ? data.baseHp : 50;
            int bAtk = data != null ? data.baseAtk : 20;
            int bDef = data != null ? data.baseDef : 15;

            DrawStatBar(sx, barY, sw, "HP", pid.ivHp, pid.GetTotalHp(bHp), bHp);
            DrawStatBar(sx, barY + 60, sw, "ATK", pid.ivAtk, pid.GetTotalAtk(bAtk), bAtk);
            DrawStatBar(sx, barY + 120, sw, "DEF", pid.ivDef, pid.GetTotalDef(bDef), bDef);

            if (data != null && !string.IsNullOrEmpty(data.description))
            {
                float descY = statBlockY + statBlockH + 14;
                GUIStyle descStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 26, wordWrap = true };
                descStyle.normal.textColor = new Color(0.72f, 0.72f, 0.72f);
                GUI.Label(new Rect(panelX + 36, descY, panelW - 72, 80), data.description, descStyle);
            }

            if (data != null && !string.IsNullOrEmpty(data.habitatHint))
            {
                float hintY = statBlockY + statBlockH + 86;
                GUIStyle hintLabel = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Italic };
                hintLabel.normal.textColor = new Color(0.5f, 0.65f, 0.5f);
                GUI.Label(new Rect(panelX + 36, hintY, panelW - 72, 32),
                    $"서식지: {data.habitatHint}", hintLabel);
            }
        }

        private void DrawStats(Rect area)
        {
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 32 };
            labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            GUIStyle valueStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            valueStyle.normal.textColor = Color.white;

            float y = area.y + 20;
            float rowH = 60f;
            float lw = area.width * 0.6f;
            float vw = area.width * 0.35f;

            int total = insectCollection != null ? insectCollection.GetAllOwned().Count : 0;
            int candy = candyInventory != null ? candyInventory.Candies : 0;
            int level = progressController != null ? progressController.Level : 1;
            int xp = progressController != null ? progressController.CurrentXp : 0;

            DrawStatRow(area.x, ref y, rowH, lw, vw, "플레이어 레벨", $"{level}", labelStyle, valueStyle);
            DrawStatRow(area.x, ref y, rowH, lw, vw, "경험치", $"{xp}", labelStyle, valueStyle);

            y += 12;
            GUI.color = new Color(0.3f, 0.3f, 0.4f);
            GUI.DrawTexture(new Rect(area.x, y, area.width, 1), Texture2D.whiteTexture);
            y += 12;
            GUI.color = Color.white;

            DrawStatRow(area.x, ref y, rowH, lw, vw, "포획한 곤충", $"{total}", labelStyle, valueStyle);
            GUIStyle candyVal = new GUIStyle(valueStyle);
            candyVal.normal.textColor = new Color(1f, 0.5f, 0.8f);
            DrawStatRow(area.x, ref y, rowH, lw, vw, "캔디", $"{candy}", labelStyle, candyVal);
        }

        private void DrawStatRow(float x, ref float y, float h, float lw, float vw,
            string label, string val, GUIStyle ls, GUIStyle vs)
        {
            GUI.Label(new Rect(x + 12, y, lw, h), label, ls);
            GUI.Label(new Rect(x + lw, y, vw, h), val, vs);
            y += h;
        }

        private string levelUpMsg;
        private float levelUpMsgTimer;

        private void DrawLevelUpSection(float x, float y, float w, PlayerInsectData pid, InsectData data)
        {
            GUI.color = new Color(0.08f, 0.10f, 0.16f, 0.9f);
            GUI.DrawTexture(new Rect(x, y, w, 120), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.7f, 1f);
            GUI.DrawTexture(new Rect(x, y, w, 3), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle lvLabel = new GUIStyle(GUI.skin.label)
            { fontSize = 16, fontStyle = FontStyle.Bold };
            lvLabel.normal.textColor = new Color(0.5f, 0.65f, 0.9f);
            GUI.Label(new Rect(x + 14, y + 8, 100, 20), "LEVEL", lvLabel);

            GUIStyle lvNum = new GUIStyle(GUI.skin.label)
            { fontSize = 36, fontStyle = FontStyle.Bold };
            lvNum.normal.textColor = Color.white;
            GUI.Label(new Rect(x + 14, y + 28, 80, 44), pid.level.ToString(), lvNum);

            int maxLv = insectCollection != null ? insectCollection.GetMaxLevel(pid.insectId) : 50;
            int candyCost = insectCollection != null ? insectCollection.GetCandyCostForLevel(pid.insectId, pid.level) : (4 + (pid.level - 1) * 2);
            bool isMaxLevel = pid.level >= maxLv;
            int xpNeeded = insectCollection != null ? insectCollection.GetXpToNextLevel(pid.insectId, pid.level) : (20 + (pid.level - 1) * 8);
            float xpRatio = xpNeeded > 0 ? Mathf.Clamp01((float)pid.currentXp / xpNeeded) : 1f;

            float barX = x + 100;
            float barW = w - 280;
            float barH = 18f;
            float barY2 = y + 34;

            GUIStyle xpLabel = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            xpLabel.normal.textColor = new Color(0.55f, 0.65f, 0.8f);
            GUI.Label(new Rect(barX, y + 14, barW, 18), isMaxLevel ? "MAX LEVEL" : "경험치 (EXP)", xpLabel);

            GUI.color = new Color(0.06f, 0.06f, 0.1f);
            GUI.DrawTexture(new Rect(barX, barY2, barW, barH), Texture2D.whiteTexture);

            if (!isMaxLevel && xpRatio > 0)
            {
                GUI.color = new Color(0.2f, 0.5f, 0.9f);
                GUI.DrawTexture(new Rect(barX, barY2 + barH / 2, barW * xpRatio, barH / 2), Texture2D.whiteTexture);
                GUI.color = new Color(0.35f, 0.65f, 1f);
                GUI.DrawTexture(new Rect(barX, barY2, barW * xpRatio, barH / 2), Texture2D.whiteTexture);
            }

            GUIStyle xpVal = new GUIStyle(GUI.skin.label)
            { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            xpVal.normal.textColor = new Color(0.85f, 0.9f, 1f);
            GUI.color = Color.white;
            GUI.Label(new Rect(barX, barY2, barW, barH),
                isMaxLevel ? "MAX" : $"{pid.currentXp} / {xpNeeded}", xpVal);

            GUIStyle maxLvS = new GUIStyle(GUI.skin.label)
            { fontSize = 13, alignment = TextAnchor.MiddleRight };
            maxLvS.normal.textColor = new Color(0.45f, 0.5f, 0.6f);
            GUI.Label(new Rect(barX, barY2 + barH + 2, barW, 16), $"최대 Lv.{maxLv}", maxLvS);

            float btnX = x + w - 168;
            float btnY2 = y + 14;
            float btnW2 = 154f;
            float btnH2 = 56f;

            int currentCandy = candyInventory != null ? candyInventory.Candies : 0;
            bool canAfford = currentCandy >= candyCost && !isMaxLevel;

            GUI.backgroundColor = canAfford ? new Color(0.2f, 0.5f, 0.3f) : new Color(0.15f, 0.15f, 0.18f);
            GUI.enabled = canAfford;
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            { fontSize = 22, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(btnX, btnY2, btnW2, btnH2),
                isMaxLevel ? "MAX" : $"레벨업\n<size=16>캔디 {candyCost}</size>", btnStyle))
            {
                if (insectCollection != null && insectCollection.TryLevelUpWithCandyByInstance(pid.instanceId))
                {
                    levelUpMsg = "레벨 업!";
                    levelUpMsgTimer = 1.5f;
                    if (TutorialQuestManager.Instance != null)
                        TutorialQuestManager.Instance.NotifyLevelUp();
                }
                else
                {
                    levelUpMsg = "캔디 부족!";
                    levelUpMsgTimer = 1.5f;
                }
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            GUIStyle candyInfo = new GUIStyle(GUI.skin.label)
            { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            candyInfo.normal.textColor = canAfford ? new Color(1f, 0.7f, 0.85f) : new Color(0.4f, 0.35f, 0.4f);
            GUI.Label(new Rect(btnX, btnY2 + btnH2 + 2, btnW2, 18),
                $"보유: {currentCandy} 캔디", candyInfo);

            if (levelUpMsgTimer > 0)
            {
                levelUpMsgTimer -= Time.deltaTime;
                float alpha = Mathf.Clamp01(levelUpMsgTimer / 0.5f);
                bool success = levelUpMsg == "레벨 업!";
                GUIStyle msgS = new GUIStyle(GUI.skin.label)
                { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                msgS.normal.textColor = success
                    ? new Color(0.3f, 1f, 0.5f, alpha)
                    : new Color(1f, 0.4f, 0.3f, alpha);
                GUI.Label(new Rect(x, y + 90, w, 30), levelUpMsg, msgS);
            }
        }

        private void DrawStatBar(float x, float y, float w, string label, int iv, int total, int baseStat)
        {
            GUIStyle labelS = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            labelS.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
            GUI.Label(new Rect(x, y, 80, 32), label, labelS);

            float barX = x + 90;
            float barW = w - 240;
            float barH = 24f;
            float barY2 = y + 4;

            GUI.color = new Color(0.15f, 0.15f, 0.2f);
            GUI.DrawTexture(new Rect(barX, barY2, barW, barH), Texture2D.whiteTexture);

            float ivRatio = iv / (float)PlayerInsectData.MaxIV;
            Color barCol = CapturePopupUI.GetIVBarColor(iv);
            GUI.color = barCol;
            GUI.DrawTexture(new Rect(barX, barY2, barW * ivRatio, barH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle ivS = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold };
            ivS.normal.textColor = barCol;
            GUI.Label(new Rect(barX + barW + 8, y, 56, 30), $"{iv}", ivS);

            GUIStyle totalS = new GUIStyle(GUI.skin.label)
            { fontSize = 26, alignment = TextAnchor.MiddleRight };
            totalS.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            GUI.Label(new Rect(x + w - 80, y, 80, 30), $"{total}", totalS);

            GUIStyle ivLabel = new GUIStyle(GUI.skin.label) { fontSize = 22 };
            ivLabel.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            GUI.Label(new Rect(x, y + 32, w, 24),
                $"기본 {baseStat} + IV {iv} + Lv 보너스", ivLabel);
        }

        private void DrawCenteredLabel(Rect area, string text, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            style.normal.textColor = color;
            GUI.Label(area, text, style);
        }

        private Color GetRarityColor(InsectRarity rarity)
        {
            return UITheme.Instance != null
                ? UITheme.Instance.GetInsectRarityColor(rarity)
                : CapturePopupUI.GetRarityColor(rarity);
        }

        private string GetOwnedDisplayName(PlayerInsectData pid, InsectData data)
        {
            string baseName = data != null ? data.displayName : (pid != null ? pid.insectId : "Unknown");
            string shortId = pid == null || string.IsNullOrEmpty(pid.instanceId)
                ? "----"
                : pid.instanceId.Substring(0, Mathf.Min(6, pid.instanceId.Length)).ToUpperInvariant();
            string shinyMark = (pid != null && pid.isShiny) ? "★ " : "";
            return $"{shinyMark}{baseName} #{shortId}";
        }

        public void AutoWire(PlayerInsectCollection collection, PlayerCandyInventory candy, PlayerProgressController progress)
        {
            if (insectCollection == null) insectCollection = collection;
            if (candyInventory == null) candyInventory = candy;
            if (progressController == null) progressController = progress;
        }
    }
}
