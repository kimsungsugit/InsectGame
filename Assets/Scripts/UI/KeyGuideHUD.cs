using InsectGame.Battle;
using InsectGame.Capture;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class KeyGuideHUD : MonoBehaviour
    {
        [SerializeField] private CaptureMinigameController minigame;
        [SerializeField] private InsectBattleController battleController;
        [SerializeField] private InsectBattleUIController battleUi;
        [SerializeField] private RegionManager regionManager;
        [SerializeField] private PlayerItemInventory itemInventory;

        private bool battleActive;

        private void OnEnable()
        {
            if (battleController != null)
            {
                battleController.BattleUpdated += OnBattleUpdated;
                battleController.BattleEnded += OnBattleEnded;
            }
        }

        private void OnDisable()
        {
            if (battleController != null)
            {
                battleController.BattleUpdated -= OnBattleUpdated;
                battleController.BattleEnded -= OnBattleEnded;
            }
        }

        private void OnBattleUpdated(InsectBattleStats p, InsectBattleStats e) { battleActive = true; }
        private void OnBattleEnded(bool won) { battleActive = false; }

        private void OnGUI()
        {
            DrawKeyGuide();
            DrawCurrentRegion();
            DrawCaptureItems();
        }

        private void DrawKeyGuide()
        {
            float x = 20f;
            float lineH = 62f;
            int rowCount = 7;
            bool inMinigame = minigame != null && minigame.IsActive;
            if (inMinigame) rowCount++;
            if (battleActive) rowCount++;
            float bgH = (rowCount + 1) * lineH + 20;
            float y = Screen.height - bgH - 18f;

            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(x - 8, y - 8, 560, bgH), Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 0.5f, 0.8f, 0.6f);
            GUI.DrawTexture(new Rect(x - 8, y - 8, 560, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle header = new GUIStyle(GUI.skin.label)
            { fontSize = 36, fontStyle = FontStyle.Bold };
            header.normal.textColor = new Color(0.95f, 0.88f, 0.5f);

            GUIStyle keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
            keyStyle.normal.textColor = Color.white;

            GUIStyle descStyle = new GUIStyle(GUI.skin.label) { fontSize = 32 };
            descStyle.normal.textColor = new Color(0.78f, 0.78f, 0.78f);

            GUI.Label(new Rect(x + 6, y, 460, lineH), "조작법", header);
            y += lineH + 2;

            DrawKeyRow(x, ref y, lineH, "WASD", "이동", keyStyle, descStyle);
            DrawKeyRow(x, ref y, lineH, "E", inMinigame ? "타이밍 확인" : "포획", keyStyle, descStyle);

            if (inMinigame)
                DrawKeyRow(x, ref y, lineH, "ESC", "포획 취소", keyStyle, descStyle);

            if (battleActive)
                DrawKeyRow(x, ref y, lineH, "1~4", "스킬 사용", keyStyle, descStyle);

            DrawKeyRow(x, ref y, lineH, "T", "배틀 팀", keyStyle, descStyle);
            DrawKeyRow(x, ref y, lineH, "G", "훈련", keyStyle, descStyle);
            DrawKeyRow(x, ref y, lineH, "N", "도감", keyStyle, descStyle);
            DrawKeyRow(x, ref y, lineH, "TAB", "컬렉션", keyStyle, descStyle);
            DrawKeyRow(x, ref y, lineH, "M", "지도", keyStyle, descStyle);
        }

        private void DrawKeyRow(float x, ref float y, float h, string key, string desc, GUIStyle ks, GUIStyle ds)
        {
            float keyW = 120f;
            GUI.color = new Color(0.2f, 0.25f, 0.42f, 0.85f);
            GUI.DrawTexture(new Rect(x, y + 3, keyW, h - 6), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle centeredKey = new GUIStyle(ks) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(x, y, keyW, h), key, centeredKey);
            GUI.Label(new Rect(x + keyW + 16, y, 400, h), desc, ds);
            y += h;
        }

        private void DrawCurrentRegion()
        {
            if (regionManager == null) return;

            RegionData current = regionManager.CurrentRegion;
            string regionName = current != null ? current.displayName : "Wild";
            Color regionCol = current != null ? current.themeColor : new Color(0.5f, 0.5f, 0.5f);

            float w = 520f;
            float h = 80f;
            float x = (Screen.width - w) / 2f;
            float y = 14f;

            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = regionCol;
            GUI.DrawTexture(new Rect(x, y + h - 5, w, 5), Texture2D.whiteTexture);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = regionCol;
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w, h), regionName, style);
        }

        private void DrawCaptureItems()
        {
            if (itemInventory == null) return;

            float w = 440f;
            float h = 220f;
            float x = Screen.width - w - 20;
            float y = 60f;

            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.8f, 0.4f);
            GUI.DrawTexture(new Rect(x, y, w, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle titleS = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
            titleS.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
            GUI.Label(new Rect(x + 16, y + 10, w, 40), "포획 아이템", titleS);

            float iy = y + 56;
            DrawItemCount(x + 16, iy, "기본 채집망", itemInventory.GetCount("net_basic"), new Color(0.65f, 0.65f, 0.65f));
            DrawItemCount(x + 16, iy + 50, "실버 채집망", itemInventory.GetCount("net_silver"), new Color(0.75f, 0.82f, 0.95f));
            DrawItemCount(x + 16, iy + 100, "골드 채집망", itemInventory.GetCount("net_gold"), new Color(1f, 0.85f, 0.2f));
        }

        private void DrawItemCount(float x, float y, string label, int count, Color col)
        {
            GUI.color = col;
            GUI.DrawTexture(new Rect(x, y + 10, 22, 22), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle ns = new GUIStyle(GUI.skin.label) { fontSize = 30 };
            ns.normal.textColor = col;
            GUI.Label(new Rect(x + 32, y, 220, 42), label, ns);

            GUIStyle cs = new GUIStyle(GUI.skin.label)
            { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            cs.normal.textColor = count > 0 ? new Color(1f, 0.92f, 0.5f) : new Color(0.4f, 0.3f, 0.3f);
            GUI.Label(new Rect(x + 260, y, 120, 42), $"x{count}", cs);
        }

        public void AutoWire(CaptureMinigameController mg, InsectBattleController bc, InsectBattleUIController bui)
        {
            if (minigame == null) minigame = mg;
            if (battleController == null || battleController != bc)
            {
                if (battleController != null)
                {
                    battleController.BattleUpdated -= OnBattleUpdated;
                    battleController.BattleEnded -= OnBattleEnded;
                }
                battleController = bc;
                if (battleController != null)
                {
                    battleController.BattleUpdated += OnBattleUpdated;
                    battleController.BattleEnded += OnBattleEnded;
                }
            }
            if (battleUi == null) battleUi = bui;
        }

        public void AutoWire(RegionManager rm)
        {
            if (regionManager == null) regionManager = rm;
        }

        public void AutoWire(PlayerItemInventory inv)
        {
            if (itemInventory == null) itemInventory = inv;
        }
    }
}
