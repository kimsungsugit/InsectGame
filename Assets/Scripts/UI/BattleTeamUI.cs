using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class BattleTeamUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private BattleTeamManager teamManager;
        [SerializeField] private PlayerInsectCollection collection;

        private bool isOpen;
        private int selectingSlot = -1;
        private Vector2 listScroll;

        // OnGUI 매 프레임 new GUIStyle 회귀 차단 — 11개 캐시 필드 + InitStyles 1회 초기화.
        // 동적 textColor는 매 호출 갱신 (BattleScreenUI ComboCol 패턴).
        private GUIStyle teamTitleCache, teamCloseCache, teamSubCache;
        private GUIStyle slotNumCache, slotNameCache, slotInfoCache, slotRemoveCache, slotEmptyCache, slotAddCache, slotHintCache;
        private GUIStyle pickerTitleCache, pickerBackCache, pickerNameCache, pickerInfoCache, pickerTagCache, pickerBtnCache;
        private bool teamStylesInit;

        // 정적 색 — new Color 매 프레임 회귀 차단
        private static readonly Color PanelBg = new Color(0.05f, 0.06f, 0.12f, 0.95f);
        private static readonly Color HeaderBg = new Color(0.15f, 0.18f, 0.28f);
        private static readonly Color HeaderLine = new Color(1f, 0.6f, 0.2f);
        private static readonly Color TitleCol = new Color(1f, 0.8f, 0.3f);
        private static readonly Color SubCol = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color SlotBg = new Color(0.1f, 0.12f, 0.18f, 0.85f);
        private static readonly Color NumCol = new Color(0.3f, 0.3f, 0.4f);
        private static readonly Color InfoCol = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color RemoveBg = new Color(0.5f, 0.2f, 0.2f);
        private static readonly Color ChangeBg = new Color(0.25f, 0.35f, 0.55f);
        private static readonly Color EmptyBoxCol = new Color(0.25f, 0.25f, 0.3f, 0.5f);
        private static readonly Color EmptyCol = new Color(0.3f, 0.3f, 0.35f);
        private static readonly Color HintCol = new Color(0.35f, 0.35f, 0.4f);
        private static readonly Color AddBg = new Color(0.2f, 0.4f, 0.3f);
        private static readonly Color PickerHeaderBg = new Color(0.15f, 0.2f, 0.3f);
        private static readonly Color PickerItemActiveBg = new Color(0.1f, 0.12f, 0.18f, 0.85f);
        private static readonly Color PickerItemDisabledBg = new Color(0.08f, 0.08f, 0.12f, 0.6f);
        private static readonly Color PickerInfoCol = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color TagCol = new Color(0.5f, 0.5f, 0.3f);
        private static readonly Color BtnBg = new Color(0.2f, 0.45f, 0.3f);
        private static readonly Color DimNameCol = new Color(0.4f, 0.4f, 0.4f);

        private void InitTeamStyles()
        {
            if (teamStylesInit) return;
            teamStylesInit = true;

            teamTitleCache = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            teamTitleCache.normal.textColor = TitleCol;
            teamCloseCache = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
            teamSubCache = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            teamSubCache.normal.textColor = SubCol;

            slotNumCache = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            slotNumCache.normal.textColor = NumCol;
            slotNameCache = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            slotInfoCache = new GUIStyle(GUI.skin.label) { fontSize = 24 };
            slotInfoCache.normal.textColor = InfoCol;
            slotRemoveCache = new GUIStyle(GUI.skin.button) { fontSize = 24 };
            slotEmptyCache = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
            slotEmptyCache.normal.textColor = EmptyCol;
            slotAddCache = new GUIStyle(GUI.skin.button) { fontSize = 26 };
            slotHintCache = new GUIStyle(GUI.skin.label) { fontSize = 26 };
            slotHintCache.normal.textColor = HintCol;

            pickerTitleCache = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            pickerTitleCache.normal.textColor = Color.white;
            pickerBackCache = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
            pickerNameCache = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            pickerInfoCache = new GUIStyle(GUI.skin.label) { fontSize = 24 };
            pickerInfoCache.normal.textColor = PickerInfoCol;
            pickerTagCache = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleRight };
            pickerTagCache.normal.textColor = TagCol;
            pickerBtnCache = new GUIStyle(GUI.skin.button) { fontSize = 24 };
        }

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            selectingSlot = -1;
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }
        public void CloseModal()
        {
            isOpen = false;
            selectingSlot = -1;
            ModalUIRegistry.Unregister(this);
        }
        private void OnDisable() { ModalUIRegistry.Unregister(this); }

        private void Update() { }

        private void OnGUI()
        {
            if (!isOpen) return;

            InitTeamStyles();
            UIScale.Begin();
            if (selectingSlot >= 0)
                DrawInsectPicker();
            else
                DrawTeamPanel();
            UIScale.End();
        }

        private void DrawTeamPanel()
        {
            float panelW = 840f;
            float panelH = 800f;
            float px = (UIScale.VirtualScreenWidth - panelW) / 2f;
            float py = (UIScale.VirtualScreenHeight - panelH) / 2f;

            GUI.color = PanelBg;
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = HeaderBg;
            GUI.DrawTexture(new Rect(px, py, panelW, 70), Texture2D.whiteTexture);
            GUI.color = HeaderLine;
            GUI.DrawTexture(new Rect(px, py + 70, panelW, 5), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(px, py + 10, panelW - 60, 50), "BATTLE TEAM", teamTitleCache);

            if (GUI.Button(new Rect(px + panelW - 55, py + 12, 50, 46), "X", teamCloseCache))
            {
                CloseModal();
            }

            GUI.Label(new Rect(px, py + 76, panelW, 36),
                $"배틀용 곤충을 최대 {BattleTeamManager.MaxSlots}마리 선택하세요", teamSubCache);

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

            GUI.color = SlotBg;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, 56, h), $"{index + 1}", slotNumCache);

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

                    slotNameCache.normal.textColor = rarityCol;
                    GUI.color = Color.white;
                    GUI.Label(new Rect(x + 130, y + 12, w - 260, 36), GetOwnedDisplayName(pid, data), slotNameCache);

                    int lv = pid != null ? pid.level : 1;
                    int cp = PlayerInsectCombatPower.Calculate(data, pid);
                    GUI.Label(new Rect(x + 130, y + 48, w - 260, 30),
                        $"Lv.{lv}  |  {data.rarity}  |  CP {cp}", slotInfoCache);
                }

                GUI.backgroundColor = RemoveBg;
                if (GUI.Button(new Rect(x + w - 140, y + 10, 120, 42), "제거", slotRemoveCache))
                    teamManager?.RemoveSlot(index);

                GUI.backgroundColor = ChangeBg;
                if (GUI.Button(new Rect(x + w - 140, y + 56, 120, 42), "변경", slotRemoveCache))
                    selectingSlot = index;
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.color = EmptyBoxCol;
                GUI.DrawTexture(new Rect(x + 60, y + h / 2f - 25, 50, 50), Texture2D.whiteTexture);

                GUI.color = Color.white;
                GUI.Label(new Rect(x + 60, y + h / 2f - 25, 50, 50), "+", slotEmptyCache);

                GUI.backgroundColor = AddBg;
                if (GUI.Button(new Rect(x + w - 150, y + h / 2f - 22, 130, 46), "선택", slotAddCache))
                    selectingSlot = index;
                GUI.backgroundColor = Color.white;

                GUI.Label(new Rect(x + 130, y + h / 2f - 16, w - 260, 34), "빈 슬롯", slotHintCache);
            }
        }

        private void DrawInsectPicker()
        {
            float panelW = 840f;
            float panelH = 800f;
            float px = (UIScale.VirtualScreenWidth - panelW) / 2f;
            float py = (UIScale.VirtualScreenHeight - panelH) / 2f;

            GUI.color = PanelBg;
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = PickerHeaderBg;
            GUI.DrawTexture(new Rect(px, py, panelW, 70), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(px + 120, py + 10, panelW - 240, 50),
                $"Select Insect for Slot {selectingSlot + 1}", pickerTitleCache);

            if (GUI.Button(new Rect(px + 12, py + 12, 110, 46), "< Back", pickerBackCache))
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
            GUI.color = alreadyInTeam ? PickerItemDisabledBg : PickerItemActiveBg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            if (data != null)
            {
                Color rarityCol = UITheme.Instance.GetInsectRarityColor(data.rarity);
                // alreadyInTeam이면 alpha/채도 낮춤 (struct copy + 필드 갱신)
                Color railCol = rarityCol;
                if (alreadyInTeam) { railCol.r *= 0.4f; railCol.g *= 0.4f; railCol.b *= 0.4f; }
                GUI.color = railCol;
                GUI.DrawTexture(new Rect(rect.x, rect.y, 5, rect.height), Texture2D.whiteTexture);

                CapturePopupUI.DrawTypedInsectPortrait(rect.x + 48, rect.y + rect.height / 2f, data.insectId, data.rarity, alreadyInTeam ? 0.4f : 1f);

                pickerNameCache.normal.textColor = alreadyInTeam ? DimNameCol : rarityCol;
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x + 90, rect.y + 10, rect.width - 220, 32), GetOwnedDisplayName(pid, data), pickerNameCache);

                int cp = PlayerInsectCombatPower.Calculate(data, pid);
                GUI.Label(new Rect(rect.x + 90, rect.y + 42, rect.width - 220, 30),
                    $"Lv.{pid.level}  |  CP {cp}", pickerInfoCache);
            }

            if (alreadyInTeam)
            {
                GUI.Label(new Rect(rect.x + rect.width - 170, rect.y + rect.height / 2f - 9, 160, 30), "팀에 있음", pickerTagCache);
            }
            else
            {
                GUI.backgroundColor = BtnBg;
                if (GUI.Button(new Rect(rect.x + rect.width - 140, rect.y + rect.height / 2f - 13, 120, 46), "선택", pickerBtnCache))
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
