using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Dex
{
    public class DexScreenUI : MonoBehaviour
    {
        [SerializeField] private InsectDatabase database;
        [SerializeField] private DexController dexController;
        [SerializeField] private PlayerInsectCollection insectCollection;
        [SerializeField] private PlayerItemInventory itemInventory;

        private bool isOpen;
        private Vector2 listScroll;
        private int selectedIndex = -1;
        private int currentTab;

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            if (isOpen && TutorialQuestManager.Instance != null)
                TutorialQuestManager.Instance.NotifyDexOpened();
        }

        private readonly string[] tabNames = { "곤충 도감", "보유 곤충", "아이템" };

        private void Update()
        {
            if (!isOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            { isOpen = false; return; }

            if (currentTab == 0 && database != null)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                { selectedIndex = Mathf.Max(0, selectedIndex - 1); ScrollToSelected(); }
                if (Input.GetKeyDown(KeyCode.DownArrow))
                { selectedIndex = Mathf.Min(database.insects.Count - 1, selectedIndex + 1); ScrollToSelected(); }
            }
        }

        private void ScrollToSelected()
        {
            float rowH = 80f;
            float targetY = selectedIndex * rowH;
            float viewH = Screen.height - 120;
            if (targetY < listScroll.y) listScroll.y = targetY;
            else if (targetY + rowH > listScroll.y + viewH) listScroll.y = targetY + rowH - viewH;
        }

        private void OnGUI()
        {
            if (isOpen)
            {
                Event evt = Event.current;
                if (evt != null && evt.type == EventType.KeyDown)
                {
                    if (evt.keyCode == KeyCode.Escape)
                    { isOpen = false; evt.Use(); return; }
                    if (currentTab == 0 && database != null)
                    {
                        if (evt.keyCode == KeyCode.UpArrow)
                        { selectedIndex = Mathf.Max(0, selectedIndex - 1); ScrollToSelected(); evt.Use(); }
                        if (evt.keyCode == KeyCode.DownArrow)
                        { selectedIndex = Mathf.Min(database.insects.Count - 1, selectedIndex + 1); ScrollToSelected(); evt.Use(); }
                    }
                }
            }

            if (!isOpen || database == null || dexController == null) return;

            GUI.depth = -10;

            GUI.color = new Color(0.02f, 0.03f, 0.06f, 0.97f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            DrawTopBar();

            float contentY = 120f;
            float contentH = Screen.height - contentY;

            if (currentTab == 0)
                DrawPokedex(contentY, contentH);
            else if (currentTab == 1)
                DrawOwnedInsects(contentY, contentH);
            else if (currentTab == 2)
                DrawItems(contentY, contentH);
        }

        private void DrawTopBar()
        {
            GUI.color = new Color(0.06f, 0.08f, 0.14f, 1f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, 114), Texture2D.whiteTexture);

            int total = database.insects.Count;
            int discovered = 0, captured = 0;
            foreach (var ins in database.insects)
            {
                if (ins == null) continue;
                if (dexController.IsDiscovered(ins.insectId)) discovered++;
                if (dexController.HasRecord(ins.insectId)) captured++;
            }

            GUIStyle titleS = new GUIStyle(GUI.skin.label)
            { fontSize = 52, fontStyle = FontStyle.Bold };
            titleS.normal.textColor = new Color(1f, 0.88f, 0.3f);
            GUI.color = Color.white;
            GUI.Label(new Rect(20, 10, 500, 60), "곤충 도감", titleS);

            GUIStyle countS = new GUIStyle(GUI.skin.label) { fontSize = 36 };
            countS.normal.textColor = new Color(0.6f, 0.65f, 0.7f);
            GUI.Label(new Rect(320, 16, 500, 50),
                $"발견 {discovered}/{total}   포획 {captured}/{total}", countS);

            float tabX = Screen.width / 2f - tabNames.Length * 120f;
            for (int i = 0; i < tabNames.Length; i++)
            {
                bool active = currentTab == i;
                GUIStyle tabS = new GUIStyle(GUI.skin.button)
                { fontSize = 36, fontStyle = active ? FontStyle.Bold : FontStyle.Normal };
                GUI.backgroundColor = active ? new Color(0.2f, 0.4f, 0.8f) : new Color(0.12f, 0.14f, 0.2f);
                if (GUI.Button(new Rect(tabX + i * 240, 66, 230, 42), tabNames[i], tabS))
                { currentTab = i; listScroll = Vector2.zero; selectedIndex = -1; }
            }
            GUI.backgroundColor = Color.white;

            GUIStyle closeS = new GUIStyle(GUI.skin.button)
            { fontSize = 38, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(Screen.width - 180, 10, 168, 50), "닫기 [N]", closeS))
                isOpen = false;

            GUI.color = new Color(1f, 0.88f, 0.3f, 0.6f);
            GUI.DrawTexture(new Rect(0, 112, Screen.width, 3), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawPokedex(float y, float h)
        {
            float listW = 500f;
            float detailX = listW + 10;
            float detailW = Screen.width - detailX - 10;

            GUI.color = new Color(0.04f, 0.05f, 0.09f, 0.9f);
            GUI.DrawTexture(new Rect(0, y, listW, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float rowH = 80f;
            int count = database.insects.Count;
            float totalListH = count * rowH;

            listScroll = GUI.BeginScrollView(
                new Rect(4, y + 4, listW - 4, h - 8),
                listScroll,
                new Rect(0, 0, listW - 28, totalListH));

            for (int i = 0; i < count; i++)
            {
                InsectData ins = database.insects[i];
                if (ins == null) continue;

                float ry = i * rowH;
                bool found = dexController.IsDiscovered(ins.insectId);
                bool caught = dexController.HasRecord(ins.insectId);
                bool selected = i == selectedIndex;

                if (selected)
                {
                    GUI.color = new Color(0.15f, 0.2f, 0.35f, 0.95f);
                    GUI.DrawTexture(new Rect(0, ry, listW - 28, rowH - 2), Texture2D.whiteTexture);
                    Color rc = UITheme.Instance.GetInsectRarityColor(ins.rarity);
                    GUI.color = Color.white;
                    Rect rowRect = new Rect(0, ry, listW - 28, rowH - 2);
                    UIHelper.DrawRarityBorder(rowRect, (int)ins.rarity, Time.time);
                }
                else
                {
                    GUI.color = new Color(0.06f, 0.07f, 0.12f, i % 2 == 0 ? 0.6f : 0.4f);
                    GUI.DrawTexture(new Rect(0, ry, listW - 28, rowH - 2), Texture2D.whiteTexture);
                }

                GUIStyle numS = new GUIStyle(GUI.skin.label) { fontSize = 28 };
                numS.normal.textColor = new Color(0.35f, 0.38f, 0.42f);
                GUI.color = Color.white;
                GUI.Label(new Rect(10, ry + 4, 72, 20), $"#{i + 1:D3}", numS);

                float iconCx = 62, iconCy = ry + rowH / 2f;
                if (found)
                {
                    Color ic = caught ? UITheme.Instance.GetInsectColor(ins.insectId, ins.rarity)
                        : new Color(0.3f, 0.3f, 0.35f);
                    DrawTinyInsect(iconCx, iconCy, 18f, ins.insectId, ic);
                }
                else
                {
                    GUI.color = new Color(0.12f, 0.12f, 0.15f);
                    GUI.DrawTexture(new Rect(iconCx - 14, iconCy - 14, 28, 28), Texture2D.whiteTexture);
                    GUIStyle qS = new GUIStyle(GUI.skin.label)
                    { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                    qS.normal.textColor = new Color(0.2f, 0.2f, 0.22f);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(iconCx - 14, iconCy - 14, 28, 28), "?", qS);
                }

                GUIStyle nameS = new GUIStyle(GUI.skin.label)
                { fontSize = 36, fontStyle = FontStyle.Bold };
                if (found)
                {
                    nameS.normal.textColor = caught ? UITheme.Instance.GetInsectRarityColor(ins.rarity)
                        : new Color(0.55f, 0.55f, 0.6f);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(88, ry + 6, 260, 40), ins.displayName, nameS);
                }
                else
                {
                    nameS.normal.textColor = new Color(0.2f, 0.2f, 0.22f);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(88, ry + 6, 260, 40), "???", nameS);
                }

                GUIStyle subS = new GUIStyle(GUI.skin.label) { fontSize = 28 };
                if (found)
                {
                    subS.normal.textColor = new Color(0.4f, 0.42f, 0.48f);
                    GUI.Label(new Rect(88, ry + 44, 240, 30), ins.rarity.ToString(), subS);
                }
                else
                {
                    subS.normal.textColor = new Color(0.15f, 0.15f, 0.18f);
                    GUI.Label(new Rect(88, ry + 44, 240, 30), "미발견", subS);
                }

                if (caught)
                {
                    GUI.color = new Color(0.2f, 0.7f, 0.3f);
                    GUI.DrawTexture(new Rect(listW - 66, ry + 22, 32, 32), Texture2D.whiteTexture);
                    GUIStyle checkS = new GUIStyle(GUI.skin.label)
                    { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                    checkS.normal.textColor = Color.white;
                    GUI.color = Color.white;
                    GUI.Label(new Rect(listW - 66, ry + 22, 32, 32), "✓", checkS);
                }
                else if (found)
                {
                    GUI.color = new Color(0.5f, 0.5f, 0.2f);
                    GUI.DrawTexture(new Rect(listW - 66, ry + 22, 32, 32), Texture2D.whiteTexture);
                    GUIStyle eyeS = new GUIStyle(GUI.skin.label)
                    { fontSize = 28, alignment = TextAnchor.MiddleCenter };
                    eyeS.normal.textColor = Color.white;
                    GUI.color = Color.white;
                    GUI.Label(new Rect(listW - 66, ry + 22, 32, 32), "◉", eyeS);
                }

                if (GUI.Button(new Rect(0, ry, listW - 28, rowH - 2), "", GUIStyle.none))
                    selectedIndex = i;
            }

            GUI.EndScrollView();

            if (selectedIndex >= 0 && selectedIndex < database.insects.Count)
                DrawDetail(detailX, y, detailW, h, database.insects[selectedIndex]);
            else
                DrawNoSelection(detailX, y, detailW, h);
        }

        private void DrawNoSelection(float x, float y, float w, float h)
        {
            GUIStyle s = new GUIStyle(GUI.skin.label)
            { fontSize = 42, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            s.normal.textColor = new Color(0.3f, 0.3f, 0.35f);
            GUI.Label(new Rect(x, y + h / 2f - 40, w, 80),
                "← 왼쪽 목록에서 곤충을 선택하세요\n(↑↓ 방향키 또는 클릭)", s);
        }

        private void DrawDetail(float x, float y, float w, float h, InsectData ins)
        {
            bool found = dexController.IsDiscovered(ins.insectId);
            bool caught = dexController.HasRecord(ins.insectId);
            DexRecord record = null;
            dexController.TryGetRecord(ins.insectId, out record);

            GUI.color = new Color(0.04f, 0.06f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float cx = x + w / 2f;
            float py = y + 20;

            Color rarityCol = UITheme.Instance.GetInsectRarityColor(ins.rarity);

            if (found)
            {
                float portraitSize = 90f;
                GUI.color = new Color(rarityCol.r * 0.1f, rarityCol.g * 0.1f, rarityCol.b * 0.1f, 0.5f);
                GUI.DrawTexture(new Rect(cx - portraitSize, py, portraitSize * 2, portraitSize * 2), Texture2D.whiteTexture);
                GUI.color = Color.white;

                Color ic = caught ? UITheme.Instance.GetInsectColor(ins.insectId, ins.rarity)
                    : new Color(0.3f, 0.3f, 0.35f);
                DrawTinyInsect(cx, py + portraitSize, portraitSize * 1.2f, ins.insectId, ic);

                py += portraitSize * 2 + 12;
            }
            else
            {
                GUI.color = new Color(0.08f, 0.08f, 0.1f);
                GUI.DrawTexture(new Rect(cx - 70, py, 140, 140), Texture2D.whiteTexture);
                GUIStyle qS = new GUIStyle(GUI.skin.label)
                { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                qS.normal.textColor = new Color(0.15f, 0.15f, 0.18f);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 70, py, 140, 140), "?", qS);
                py += 158;
            }

            GUIStyle nameS = new GUIStyle(GUI.skin.label)
            { fontSize = 60, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (found)
            {
                nameS.normal.textColor = caught ? rarityCol : new Color(0.5f, 0.5f, 0.55f);
                GUI.Label(new Rect(x, py, w, 66), ins.displayName, nameS);
            }
            else
            {
                nameS.normal.textColor = new Color(0.18f, 0.18f, 0.2f);
                GUI.Label(new Rect(x, py, w, 66), "???", nameS);
            }
            py += 70;

            if (found)
            {
                GUIStyle rarityS = new GUIStyle(GUI.skin.label)
                { fontSize = 38, alignment = TextAnchor.MiddleCenter };
                rarityS.normal.textColor = rarityCol;
                GUI.Label(new Rect(x, py, w, 46), ins.rarity.ToString(), rarityS);
                py += 50;
            }

            if (caught)
            {
                GUI.color = new Color(0.2f, 0.7f, 0.3f, 0.15f);
                GUI.DrawTexture(new Rect(cx - 120, py, 240, 50), Texture2D.whiteTexture);
                GUIStyle caughtS = new GUIStyle(GUI.skin.label)
                { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                caughtS.normal.textColor = new Color(0.3f, 0.85f, 0.4f);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 120, py, 240, 50), "✓ 포획 완료", caughtS);
                py += 56;
            }
            else if (found)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.2f, 0.15f);
                GUI.DrawTexture(new Rect(cx - 120, py, 240, 50), Texture2D.whiteTexture);
                GUIStyle seenS = new GUIStyle(GUI.skin.label)
                { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                seenS.normal.textColor = new Color(0.8f, 0.8f, 0.3f);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 120, py, 240, 50), "◉ 발견만 됨", seenS);
                py += 56;
            }
            else
            {
                GUIStyle unkS = new GUIStyle(GUI.skin.label)
                { fontSize = 38, alignment = TextAnchor.MiddleCenter };
                unkS.normal.textColor = new Color(0.2f, 0.2f, 0.22f);
                GUI.Label(new Rect(x, py, w, 46), "아직 발견하지 못한 곤충입니다", unkS);
                py += 50;
                GUI.color = Color.white;
                return;
            }

            GUI.color = new Color(0.08f, 0.1f, 0.16f, 0.8f);
            float infoBoxX = x + 20;
            float infoBoxW = w - 40;
            GUI.DrawTexture(new Rect(infoBoxX, py, infoBoxW, caught ? 350 : 120), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle labelS = new GUIStyle(GUI.skin.label) { fontSize = 34 };
            labelS.normal.textColor = new Color(0.55f, 0.58f, 0.65f);
            GUIStyle valS = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            valS.normal.textColor = Color.white;

            float lx = infoBoxX + 16;
            float lw = infoBoxW - 32;

            if (caught)
            {
                DrawInfoRow(lx, py + 10, lw, "HP", $"{ins.baseHp}", labelS, valS);
                DrawInfoRow(lx, py + 56, lw, "공격력", $"{ins.baseAtk}", labelS, valS);
                DrawInfoRow(lx, py + 102, lw, "방어력", $"{ins.baseDef}", labelS, valS);
                DrawInfoRow(lx, py + 148, lw, "레벨 범위", $"{ins.minLevel} ~ {ins.maxLevel}", labelS, valS);
                DrawInfoRow(lx, py + 194, lw, "포획 난이도",
                    ins.captureDifficulty < 0.3f ? "쉬움" : ins.captureDifficulty < 0.6f ? "보통" : "어려움", labelS, valS);

                if (record != null)
                {
                    DrawInfoRow(lx, py + 248, lw, "발견 횟수", $"{record.discoveredCount}회", labelS, valS);
                    DrawInfoRow(lx, py + 294, lw, "포획 횟수", $"{record.capturedCount}회", labelS, valS);
                }

                py += 360;
            }
            else
            {
                GUIStyle hintS = new GUIStyle(GUI.skin.label)
                { fontSize = 36, alignment = TextAnchor.MiddleCenter };
                hintS.normal.textColor = new Color(0.4f, 0.42f, 0.48f);
                GUI.Label(new Rect(infoBoxX, py + 10, infoBoxW, 28), "포획하면 상세 스탯을 확인할 수 있습니다", hintS);

                if (record != null)
                    DrawInfoRow(lx, py + 56, lw, "발견 횟수", $"{record.discoveredCount}회", labelS, valS);

                py += 130;
            }

            if (!string.IsNullOrEmpty(ins.habitatHint))
            {
                py += 8;
                GUIStyle habitatLabel = new GUIStyle(GUI.skin.label) { fontSize = 32 };
                habitatLabel.normal.textColor = new Color(0.35f, 0.55f, 0.35f);
                GUI.Label(new Rect(infoBoxX + 16, py, 80, 40), "서식지", habitatLabel);
                GUIStyle habitatVal = new GUIStyle(GUI.skin.label)
                { fontSize = 36, fontStyle = FontStyle.Bold };
                habitatVal.normal.textColor = new Color(0.5f, 0.75f, 0.5f);
                GUI.Label(new Rect(infoBoxX + 100, py, lw - 100, 40), ins.habitatHint, habitatVal);
                py += 46;
            }

            if (caught && !string.IsNullOrEmpty(ins.description))
            {
                py += 8;
                GUIStyle descS = new GUIStyle(GUI.skin.label)
                { fontSize = 34, wordWrap = true };
                descS.normal.textColor = new Color(0.6f, 0.62f, 0.68f);

                GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.6f);
                GUI.DrawTexture(new Rect(infoBoxX, py, infoBoxW, 110), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Label(new Rect(infoBoxX + 14, py + 8, infoBoxW - 28, 56), ins.description, descS);
            }
        }

        private void DrawInfoRow(float x, float y, float w, string label, string val,
            GUIStyle ls, GUIStyle vs)
        {
            GUI.Label(new Rect(x, y, w * 0.5f, 42), label, ls);
            GUI.Label(new Rect(x + w * 0.5f, y, w * 0.5f, 42), val, vs);
        }

        private void DrawOwnedInsects(float y, float h)
        {
            if (insectCollection == null)
            {
                DrawCentered(y, h, "곤충 컬렉션 데이터 없음");
                return;
            }

            List<PlayerInsectData> owned = insectCollection.GetAllOwned();
            if (owned.Count == 0)
            {
                DrawCentered(y, h, "아직 포획한 곤충이 없습니다\n필드에서 곤충에 다가가 E키를 눌러 포획하세요!");
                return;
            }

            float panelW = Mathf.Min(Screen.width - 40, 900);
            float px = (Screen.width - panelW) / 2f;
            int cols = Mathf.Max(1, Mathf.FloorToInt(panelW / 260f));
            float cardW = (panelW - (cols - 1) * 10) / cols;
            float cardH = 140f;
            int rows = Mathf.CeilToInt((float)owned.Count / cols);
            float totalH = rows * (cardH + 8);

            listScroll = GUI.BeginScrollView(
                new Rect(px, y + 4, panelW + 20, h - 8),
                listScroll,
                new Rect(0, 0, panelW, totalH));

            for (int i = 0; i < owned.Count; i++)
            {
                PlayerInsectData pid = owned[i];
                InsectData data = insectCollection.GetInsectData(pid.insectId);

                int col = i % cols;
                int row = i / cols;
                float cx = col * (cardW + 10);
                float cy = row * (cardH + 8);

                DrawOwnedCard(cx, cy, cardW, cardH, pid, data);
            }

            GUI.EndScrollView();
        }

        private void DrawOwnedCard(float x, float y, float w, float h, PlayerInsectData pid, InsectData data)
        {
            Color rc = data != null ? UITheme.Instance.GetInsectRarityColor(data.rarity) : Color.gray;
            int cardRarityTier = data != null ? (int)data.rarity : 0;

            GUI.color = new Color(0.07f, 0.09f, 0.15f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            Rect cardRect = new Rect(x, y, w, h);
            UIHelper.DrawRarityBorder(cardRect, cardRarityTier, Time.time);
            if (cardRarityTier >= 3)
                UIHelper.DrawRarityGlow(cardRect, rc, cardRarityTier >= 4 ? 0.5f : 0.25f, Time.time);

            if (data != null)
            {
                Color ic = UITheme.Instance.GetInsectColor(data.insectId, data.rarity);
                DrawTinyInsect(x + 40, y + h / 2f + 4, 26f, data.insectId, ic);
            }

            string name = data != null ? data.displayName : pid.insectId;
            GUIStyle nameS = new GUIStyle(GUI.skin.label)
            { fontSize = 38, fontStyle = FontStyle.Bold };
            nameS.normal.textColor = rc;
            GUI.Label(new Rect(x + 76, y + 8, w - 150, 44), name, nameS);

            GUIStyle infoS = new GUIStyle(GUI.skin.label) { fontSize = 32 };
            infoS.normal.textColor = new Color(0.6f, 0.6f, 0.65f);
            string rStr = data != null ? data.rarity.ToString() : "?";
            GUI.Label(new Rect(x + 76, y + 52, w - 90, 36),
                $"Lv.{pid.level}  |  {rStr}  |  IV {pid.IVPercent * 100:0}%", infoS);

            GUIStyle stS = new GUIStyle(GUI.skin.label) { fontSize = 28 };
            stS.normal.textColor = new Color(0.45f, 0.48f, 0.52f);
            GUI.Label(new Rect(x + 76, y + 88, w - 90, 30),
                $"HP:{pid.ivHp}  ATK:{pid.ivAtk}  DEF:{pid.ivDef}", stS);

            Color gc = UITheme.Instance.GetGradeColor(pid.Grade);
            GUIStyle grS = new GUIStyle(GUI.skin.label)
            { fontSize = 52, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            grS.normal.textColor = gc;
            GUI.Label(new Rect(x + w - 80, y + 8, 70, 60), CapturePopupUI.GetGradeLabel(pid.Grade), grS);

            GUIStyle pctS = new GUIStyle(GUI.skin.label)
            { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            pctS.normal.textColor = new Color(gc.r, gc.g, gc.b, 0.7f);
            GUI.Label(new Rect(x + w - 80, y + 68, 70, 30), $"{pid.IVPercent * 100:0}%", pctS);
        }

        private void DrawItems(float y, float h)
        {
            if (itemInventory == null)
            {
                DrawCentered(y, h, "아이템 인벤토리 데이터 없음");
                return;
            }

            PlayerItemSave snapshot = itemInventory.GetSnapshot();
            if (snapshot == null || snapshot.items.Count == 0)
            {
                DrawCentered(y, h, "보유 아이템이 없습니다");
                return;
            }

            float panelW = Mathf.Min(Screen.width - 40, 700);
            float px = (Screen.width - panelW) / 2f;

            GUIStyle headerS = new GUIStyle(GUI.skin.label)
            { fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            headerS.normal.textColor = new Color(0.7f, 0.75f, 0.8f);
            GUI.Label(new Rect(px, y + 10, panelW, 50), "보유 아이템", headerS);

            float iy = y + 52;
            float itemH = 120f;

            foreach (PlayerItemRecord rec in snapshot.items)
            {
                if (rec.count <= 0) continue;

                DrawItemRow(px, iy, panelW, itemH, rec);
                iy += itemH + 6;
            }
        }

        private void DrawItemRow(float x, float y, float w, float h, PlayerItemRecord rec)
        {
            Color itemCol = GetItemColor(rec.itemId);

            GUI.color = new Color(0.07f, 0.09f, 0.15f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = itemCol;
            GUI.DrawTexture(new Rect(x, y, 4, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.color = itemCol;
            GUI.DrawTexture(new Rect(x + 20, y + h / 2f - 24, 48, 48), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string displayName = GetItemDisplayName(rec.itemId);
            string desc = GetItemDescription(rec.itemId);

            GUIStyle nameS = new GUIStyle(GUI.skin.label)
            { fontSize = 38, fontStyle = FontStyle.Bold };
            nameS.normal.textColor = itemCol;
            GUI.Label(new Rect(x + 66, y + 12, w - 200, 46), displayName, nameS);

            GUIStyle descS = new GUIStyle(GUI.skin.label) { fontSize = 30 };
            descS.normal.textColor = new Color(0.5f, 0.52f, 0.58f);
            GUI.Label(new Rect(x + 66, y + 60, w - 200, 36), desc, descS);

            GUIStyle countS = new GUIStyle(GUI.skin.label)
            { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            countS.normal.textColor = rec.count > 0 ? new Color(1f, 0.92f, 0.5f) : new Color(0.3f, 0.3f, 0.3f);
            GUI.Label(new Rect(x + w - 160, y + 10, 140, 80), $"x{rec.count}", countS);
        }

        private void DrawCentered(float y, float h, string text)
        {
            GUIStyle s = new GUIStyle(GUI.skin.label)
            { fontSize = 42, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            s.normal.textColor = new Color(0.35f, 0.35f, 0.4f);
            GUI.Label(new Rect(0, y, Screen.width, h), text, s);
        }

        private void DrawTinyInsect(float cx, float cy, float size, string id, Color col)
        {
            Color dark = new Color(col.r * 0.5f, col.g * 0.5f, col.b * 0.5f);
            float s = size / 30f;

            if (string.IsNullOrEmpty(id)) id = "";

            if (id.Contains("butterfly") || id.Contains("luna") || id.Contains("atlas"))
            {
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 2 * s, cy - 10 * s, 4 * s, 20 * s), Texture2D.whiteTexture);
                GUI.color = new Color(col.r, col.g, col.b, 0.7f);
                GUI.DrawTexture(new Rect(cx - 20 * s, cy - 12 * s, 16 * s, 18 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 4 * s, cy - 12 * s, 16 * s, 18 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 16 * s, cy + 4 * s, 12 * s, 10 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 4 * s, cy + 4 * s, 12 * s, 10 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("moth"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 5 * s, cy - 6 * s, 10 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = new Color(col.r * 0.7f, col.g * 0.6f, col.b * 0.5f, 0.7f);
                GUI.DrawTexture(new Rect(cx - 20 * s, cy - 10 * s, 18 * s, 16 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 2 * s, cy - 10 * s, 18 * s, 16 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("mantis"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 4 * s, cy - 12 * s, 8 * s, 24 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 7 * s, cy - 16 * s, 14 * s, 8 * s), Texture2D.whiteTexture);
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 16 * s, cy - 8 * s, 10 * s, 3 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 6 * s, cy - 8 * s, 10 * s, 3 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("dragonfly"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 2 * s, cy - 6 * s, 4 * s, 24 * s), Texture2D.whiteTexture);
                GUI.color = new Color(col.r, col.g, col.b, 0.3f);
                GUI.DrawTexture(new Rect(cx - 18 * s, cy - 8 * s, 14 * s, 5 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 4 * s, cy - 8 * s, 14 * s, 5 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 6 * s, cy - 12 * s, 12 * s, 7 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("bee"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 10 * s, cy - 7 * s, 20 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = new Color(0, 0, 0, 0.8f);
                GUI.DrawTexture(new Rect(cx - 9 * s, cy - 2 * s, 18 * s, 2 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 9 * s, cy + 3 * s, 18 * s, 2 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("firefly"))
            {
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 8 * s, cy - 7 * s, 16 * s, 12 * s), Texture2D.whiteTexture);
                GUI.color = new Color(0.9f, 1f, 0.3f, 0.8f);
                GUI.DrawTexture(new Rect(cx - 6 * s, cy + 3 * s, 12 * s, 8 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("stag") || id.Contains("rhinoceros"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 12 * s, cy - 5 * s, 24 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 8 * s, cy - 14 * s, 16 * s, 10 * s), Texture2D.whiteTexture);
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 2 * s, cy - 22 * s, 4 * s, 10 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("cicada"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 10 * s, cy - 6 * s, 20 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = new Color(col.r, col.g, col.b, 0.3f);
                GUI.DrawTexture(new Rect(cx - 16 * s, cy - 2 * s, 12 * s, 12 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 4 * s, cy - 2 * s, 12 * s, 12 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("ant"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 4 * s, cy + 1 * s, 8 * s, 10 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 3 * s, cy - 4 * s, 6 * s, 6 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 5 * s, cy - 12 * s, 10 * s, 8 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("cricket"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 7 * s, cy - 4 * s, 14 * s, 12 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 6 * s, cy - 12 * s, 12 * s, 8 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 14 * s, cy + 4 * s, 8 * s, 3 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 6 * s, cy + 4 * s, 8 * s, 3 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("water") || id.Contains("strider"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 3 * s, cy - 8 * s, 6 * s, 18 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 18 * s, cy - 2 * s, 14 * s, 2 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 4 * s, cy - 2 * s, 14 * s, 2 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("diving"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 12 * s, cy - 6 * s, 24 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 8 * s, cy - 12 * s, 16 * s, 8 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("jewel") || id.Contains("scarab") || id.Contains("golden"))
            {
                Color shim = new Color(Mathf.Min(1, col.r + 0.2f), Mathf.Min(1, col.g + 0.2f), col.b);
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 12 * s, cy - 6 * s, 24 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = shim;
                GUI.DrawTexture(new Rect(cx - 9 * s, cy - 4 * s, 8 * s, 10 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 1 * s, cy - 4 * s, 8 * s, 10 * s), Texture2D.whiteTexture);
            }
            else
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 10 * s, cy - 5 * s, 20 * s, 12 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 6 * s, cy - 12 * s, 12 * s, 8 * s), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        private string GetItemDisplayName(string itemId)
        {
            switch (itemId)
            {
                case "net_basic": return "기본 채집망";
                case "net_silver": return "은빛 채집망";
                case "net_gold": return "황금 채집망";
                case "mat_leaf": return "나뭇잎";
                case "mat_berry": return "열매";
                case "mat_honey": return "꿀";
                default: return itemId;
            }
        }

        private string GetItemDescription(string itemId)
        {
            switch (itemId)
            {
                case "net_basic": return "기본 포획 도구 - 보통 난이도";
                case "net_silver": return "좋은 품질 - 미니게임이 쉬워짐";
                case "net_gold": return "최고급 - 매우 쉬운 미니게임 + 포획률↑";
                case "mat_leaf": return "훈련에 사용되는 재료";
                case "mat_berry": return "곤충에게 줄 수 있는 열매";
                case "mat_honey": return "귀한 재료 - 높은 효과";
                default: return "아이템";
            }
        }

        private Color GetItemColor(string itemId)
        {
            switch (itemId)
            {
                case "net_basic": return new Color(0.6f, 0.6f, 0.6f);
                case "net_silver": return new Color(0.7f, 0.78f, 0.92f);
                case "net_gold": return new Color(1f, 0.85f, 0.2f);
                case "mat_leaf": return new Color(0.4f, 0.7f, 0.35f);
                case "mat_berry": return new Color(0.8f, 0.3f, 0.35f);
                case "mat_honey": return new Color(0.9f, 0.7f, 0.2f);
                default: return new Color(0.5f, 0.5f, 0.5f);
            }
        }

        public void AutoWire(InsectDatabase db, DexController dex)
        {
            if (database == null) database = db;
            if (dexController == null) dexController = dex;
        }

        public void AutoWire(PlayerInsectCollection col, PlayerItemInventory items)
        {
            if (insectCollection == null) insectCollection = col;
            if (itemInventory == null) itemInventory = items;
        }
    }
}
