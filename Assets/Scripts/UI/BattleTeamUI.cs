using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class BattleTeamUI : MonoBehaviour
    {
        [SerializeField] private BattleTeamManager teamManager;
        [SerializeField] private PlayerInsectCollection collection;

        private bool isOpen;
        private int selectingSlot = -1;
        private Vector2 listScroll;

        public bool IsOpen => isOpen;
        public void Toggle() { isOpen = !isOpen; selectingSlot = -1; }

        private void Update() { }

        private void OnGUI()
        {
            if (!isOpen) return;

            if (selectingSlot >= 0)
                DrawInsectPicker();
            else
                DrawTeamPanel();
        }

        private void DrawTeamPanel()
        {
            float panelW = 840f;
            float panelH = 800f;
            float px = (Screen.width - panelW) / 2f;
            float py = (Screen.height - panelH) / 2f;

            GUI.color = new Color(0.05f, 0.06f, 0.12f, 0.95f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(0.15f, 0.18f, 0.28f);
            GUI.DrawTexture(new Rect(px, py, panelW, 70), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.6f, 0.2f);
            GUI.DrawTexture(new Rect(px, py + 70, panelW, 5), Texture2D.whiteTexture);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(1f, 0.8f, 0.3f);
            GUI.color = Color.white;
            GUI.Label(new Rect(px, py + 10, panelW - 60, 50), "BATTLE TEAM", titleStyle);

            GUIStyle closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(px + panelW - 55, py + 12, 50, 46), "X", closeStyle))
            {
                isOpen = false;
                selectingSlot = -1;
            }

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            subStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(px, py + 76, panelW, 36), $"배틀용 곤충을 최대 {BattleTeamManager.MaxSlots}마리 선택하세요", subStyle);

            float slotY = py + 120;
            float slotH = 110f;

            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                DrawSlot(px + 15, slotY + i * (slotH + 4), panelW - 30, slotH, i);
            }
        }

        private void DrawSlot(float x, float y, float w, float h, int index)
        {
            string instanceId = teamManager != null ? teamManager.GetSlot(index) : null;
            bool hasInsect = !string.IsNullOrEmpty(instanceId);

            GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            GUIStyle numStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            numStyle.normal.textColor = new Color(0.3f, 0.3f, 0.4f);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, 56, h), $"{index + 1}", numStyle);

            if (hasInsect)
            {
                PlayerInsectData pid = collection != null ? collection.GetByInstanceId(instanceId) : null;
                InsectData data = pid != null && collection != null ? collection.GetInsectData(pid.insectId) : null;

                if (data != null)
                {
                    Color rarityCol = UITheme.Instance.GetInsectRarityColor(data.rarity);
                    GUI.color = rarityCol;
                    GUI.DrawTexture(new Rect(x, y, 6, h), Texture2D.whiteTexture);

                    CapturePopupUI.DrawTypedInsectPortrait(x + 90, y + h / 2f, data.insectId, data.rarity, 1f);

                    GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = 26, fontStyle = FontStyle.Bold };
                    nameStyle.normal.textColor = rarityCol;
                    GUI.color = Color.white;
                    GUI.Label(new Rect(x + 130, y + 12, w - 260, 36), GetOwnedDisplayName(pid, data), nameStyle);

                    GUIStyle infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 24 };
                    infoStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                    int lv = pid != null ? pid.level : 1;
                    int cp = PlayerInsectCombatPower.Calculate(data, pid);
                    GUI.Label(new Rect(x + 130, y + 48, w - 260, 30),
                        $"Lv.{lv}  |  {data.rarity}  |  CP {cp}", infoStyle);
                }

                GUIStyle removeStyle = new GUIStyle(GUI.skin.button) { fontSize = 24 };
                GUI.backgroundColor = new Color(0.5f, 0.2f, 0.2f);
                if (GUI.Button(new Rect(x + w - 140, y + 10, 120, 42), "제거", removeStyle))
                    teamManager?.RemoveSlot(index);

                GUI.backgroundColor = new Color(0.25f, 0.35f, 0.55f);
                if (GUI.Button(new Rect(x + w - 140, y + 56, 120, 42), "변경", removeStyle))
                    selectingSlot = index;
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.color = new Color(0.25f, 0.25f, 0.3f, 0.5f);
                GUI.DrawTexture(new Rect(x + 60, y + h / 2f - 25, 50, 50), Texture2D.whiteTexture);

                GUIStyle emptyStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 40, alignment = TextAnchor.MiddleCenter };
                emptyStyle.normal.textColor = new Color(0.3f, 0.3f, 0.35f);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 60, y + h / 2f - 25, 50, 50), "+", emptyStyle);

                GUIStyle addStyle = new GUIStyle(GUI.skin.button) { fontSize = 26 };
                GUI.backgroundColor = new Color(0.2f, 0.4f, 0.3f);
                if (GUI.Button(new Rect(x + w - 150, y + h / 2f - 22, 130, 46), "선택", addStyle))
                    selectingSlot = index;
                GUI.backgroundColor = Color.white;

                GUIStyle hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 26 };
                hintStyle.normal.textColor = new Color(0.35f, 0.35f, 0.4f);
                GUI.Label(new Rect(x + 130, y + h / 2f - 16, w - 260, 34), "빈 슬롯", hintStyle);
            }
        }

        private void DrawInsectPicker()
        {
            float panelW = 840f;
            float panelH = 800f;
            float px = (Screen.width - panelW) / 2f;
            float py = (Screen.height - panelH) / 2f;

            GUI.color = new Color(0.05f, 0.06f, 0.12f, 0.95f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(0.15f, 0.2f, 0.3f);
            GUI.DrawTexture(new Rect(px, py, panelW, 70), Texture2D.whiteTexture);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Color.white;
            GUI.color = Color.white;
            GUI.Label(new Rect(px + 120, py + 10, panelW - 240, 50),
                $"Select Insect for Slot {selectingSlot + 1}", titleStyle);

            GUIStyle backStyle = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(px + 12, py + 12, 110, 46), "< Back", backStyle))
                selectingSlot = -1;

            if (collection == null) return;

            List<PlayerInsectData> owned = collection.GetAllOwned();
            float listY = py + 80;
            float listH = panelH - 90;
            float itemH = 96f;
            float totalH = owned.Count * itemH;
            Rect viewRect = new Rect(0, 0, panelW - 40, totalH);
            Rect listArea = new Rect(px + 10, listY, panelW - 20, listH);

            listScroll = GUI.BeginScrollView(listArea, listScroll, viewRect);
            for (int i = 0; i < owned.Count; i++)
            {
                PlayerInsectData pid = owned[i];
                InsectData data = collection.GetInsectData(pid.insectId);
                bool alreadyInTeam = teamManager != null && teamManager.IsInTeam(pid.instanceId);
                DrawPickerItem(new Rect(0, i * itemH, viewRect.width, itemH - 3), pid, data, alreadyInTeam);
            }
            GUI.EndScrollView();
        }

        private void DrawPickerItem(Rect rect, PlayerInsectData pid, InsectData data, bool alreadyInTeam)
        {
            GUI.color = alreadyInTeam
                ? new Color(0.08f, 0.08f, 0.12f, 0.6f)
                : new Color(0.1f, 0.12f, 0.18f, 0.85f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            if (data != null)
            {
                Color rarityCol = UITheme.Instance.GetInsectRarityColor(data.rarity);
                GUI.color = alreadyInTeam ? new Color(rarityCol.r * 0.4f, rarityCol.g * 0.4f, rarityCol.b * 0.4f) : rarityCol;
                GUI.DrawTexture(new Rect(rect.x, rect.y, 5, rect.height), Texture2D.whiteTexture);

                CapturePopupUI.DrawTypedInsectPortrait(rect.x + 48, rect.y + rect.height / 2f, data.insectId, data.rarity, alreadyInTeam ? 0.4f : 1f);

                GUIStyle ns = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
                ns.normal.textColor = alreadyInTeam ? new Color(0.4f, 0.4f, 0.4f) : rarityCol;
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x + 90, rect.y + 10, rect.width - 220, 32), GetOwnedDisplayName(pid, data), ns);

                GUIStyle info = new GUIStyle(GUI.skin.label) { fontSize = 24 };
                info.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                int cp = PlayerInsectCombatPower.Calculate(data, pid);
                GUI.Label(new Rect(rect.x + 90, rect.y + 42, rect.width - 220, 30),
                    $"Lv.{pid.level}  |  CP {cp}", info);
            }

            if (alreadyInTeam)
            {
                GUIStyle tagStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 24, alignment = TextAnchor.MiddleRight };
                tagStyle.normal.textColor = new Color(0.5f, 0.5f, 0.3f);
                GUI.Label(new Rect(rect.x + rect.width - 170, rect.y + rect.height / 2f - 9, 160, 30), "팀에 있음", tagStyle);
            }
            else
            {
                GUIStyle btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 24 };
                GUI.backgroundColor = new Color(0.2f, 0.45f, 0.3f);
                if (GUI.Button(new Rect(rect.x + rect.width - 140, rect.y + rect.height / 2f - 13, 120, 46), "선택", btnStyle))
                {
                    if (teamManager != null)
                    {
                        teamManager.SetSlot(selectingSlot, pid.instanceId);
                        selectingSlot = -1;
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }

        public void AutoWire(BattleTeamManager tm, PlayerInsectCollection col)
        {
            if (teamManager == null) teamManager = tm;
            if (collection == null) collection = col;
        }

        private static string GetOwnedDisplayName(PlayerInsectData pid, InsectData data)
        {
            string baseName = data != null ? data.displayName : (pid != null ? pid.insectId : "Unknown");
            string shortId = pid == null || string.IsNullOrEmpty(pid.instanceId)
                ? "----"
                : pid.instanceId.Substring(0, Mathf.Min(6, pid.instanceId.Length)).ToUpperInvariant();
            return $"{baseName} #{shortId}";
        }
    }
}
