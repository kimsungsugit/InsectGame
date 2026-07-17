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

            teamTitleCache = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            teamTitleCache.normal.textColor = TitleCol;
            teamCloseCache = new GUIStyle(GUI.skin.button) { fontSize = 36, fontStyle = FontStyle.Bold };
            teamSubCache = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleCenter };
            teamSubCache.normal.textColor = SubCol;

            slotNumCache = new GUIStyle(GUI.skin.label) { fontSize = 47, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            slotNumCache.normal.textColor = NumCol;
            slotNameCache = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold };
            slotInfoCache = new GUIStyle(GUI.skin.label) { fontSize = 31 };
            slotInfoCache.normal.textColor = InfoCol;
            slotRemoveCache = new GUIStyle(GUI.skin.button) { fontSize = 31 };
            slotEmptyCache = new GUIStyle(GUI.skin.label) { fontSize = 52, alignment = TextAnchor.MiddleCenter };
            slotEmptyCache.normal.textColor = EmptyCol;
            slotAddCache = new GUIStyle(GUI.skin.button) { fontSize = 34 };
            slotHintCache = new GUIStyle(GUI.skin.label) { fontSize = 34 };
            slotHintCache.normal.textColor = HintCol;

            pickerTitleCache = new GUIStyle(GUI.skin.label) { fontSize = 39, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            pickerTitleCache.normal.textColor = Color.white;
            pickerBackCache = new GUIStyle(GUI.skin.button) { fontSize = 31, fontStyle = FontStyle.Bold };
            pickerNameCache = new GUIStyle(GUI.skin.label) { fontSize = 31, fontStyle = FontStyle.Bold };
            pickerInfoCache = new GUIStyle(GUI.skin.label) { fontSize = 31 };
            pickerInfoCache.normal.textColor = PickerInfoCol;
            pickerTagCache = new GUIStyle(GUI.skin.label) { fontSize = 31, alignment = TextAnchor.MiddleRight };
            pickerTagCache.normal.textColor = TagCol;
            pickerBtnCache = new GUIStyle(GUI.skin.button) { fontSize = 31 };
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
            float panelW = 960f;
            float panelH = 940f;
            float px = (UIScale.VirtualScreenWidth - panelW) / 2f;
            float py = (UIScale.VirtualScreenHeight - panelH) / 2f;

            GUI.color = PanelBg;
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = HeaderBg;
            GUI.DrawTexture(new Rect(px, py, panelW, 84), Texture2D.whiteTexture);
            GUI.color = HeaderLine;
            GUI.DrawTexture(new Rect(px, py + 84, panelW, 5), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(px, py + 14, panelW - 72, 58), "BATTLE TEAM", teamTitleCache);

            float closeH = UIScale.IsMobileLayout ? 64f : 52f;
            if (GUI.Button(new Rect(px + panelW - 80f, py + 10f, 70f, closeH), "X", teamCloseCache))
            {
                CloseModal();
            }

            GUI.Label(new Rect(px, py + 94, panelW, 42),
                $"배틀용 곤충을 최대 {BattleTeamManager.MaxSlots}마리 선택하세요", teamSubCache);

            float slotY = py + 146;
            float slotH = UIScale.IsMobileLayout ? 148f : 128f;

            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                DrawSlot(px + 18, slotY + i * (slotH + 6), panelW - 36, slotH, i);
            }
        }

        private void DrawSlot(float x, float y, float w, float h, int index)
        {
            string instanceId = teamManager != null ? teamManager.GetSlot(index) : null;
            bool hasInsect = !string.IsNullOrEmpty(instanceId);

            GUI.color = SlotBg;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, 64, h), $"{index + 1}", slotNumCache);

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
                    GUI.Label(new Rect(x + 150, y + 16, w - 330, 46), GetOwnedDisplayName(pid, data), slotNameCache);

                    int lv = pid != null ? pid.level : 1;
                    int cp = PlayerInsectCombatPower.Calculate(data, pid);
                    GUI.Label(new Rect(x + 150, y + 64, w - 330, 42),
                        $"Lv.{lv}  |  {data.rarity}  |  CP {cp}", slotInfoCache);
                }

                GUI.backgroundColor = RemoveBg;
                float actionH = UIScale.IsMobileLayout ? 60f : 50f;
                if (GUI.Button(new Rect(x + w - 168, y + 10f, 150, actionH), "제거", slotRemoveCache))
                    teamManager?.RemoveSlot(index);

                GUI.backgroundColor = ChangeBg;
                if (GUI.Button(new Rect(x + w - 168, y + h - actionH - 10f, 150, actionH), "변경", slotRemoveCache))
                    selectingSlot = index;
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.color = EmptyBoxCol;
                GUI.DrawTexture(new Rect(x + 64, y + h / 2f - 32, 64, 64), Texture2D.whiteTexture);

                GUI.color = Color.white;
                GUI.Label(new Rect(x + 64, y + h / 2f - 32, 64, 64), "+", slotEmptyCache);

                GUI.backgroundColor = AddBg;
                float selectH = UIScale.IsMobileLayout ? 64f : 52f;
                if (GUI.Button(new Rect(x + w - 170, y + h / 2f - selectH * 0.5f, 150, selectH), "선택", slotAddCache))
                    selectingSlot = index;
                GUI.backgroundColor = Color.white;

                GUI.Label(new Rect(x + 150, y + h / 2f - 21, w - 330, 42), "빈 슬롯", slotHintCache);
            }
        }

        private void DrawInsectPicker()
        {
            float panelW = 960f;
            float panelH = 940f;
            float px = (UIScale.VirtualScreenWidth - panelW) / 2f;
            float py = (UIScale.VirtualScreenHeight - panelH) / 2f;

            GUI.color = PanelBg;
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = PickerHeaderBg;
            GUI.DrawTexture(new Rect(px, py, panelW, 84), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(px + 130, py + 14, panelW - 260, 58),
                $"Select Insect for Slot {selectingSlot + 1}", pickerTitleCache);

            if (GUI.Button(new Rect(px + 14, py + 10f, 150f, UIScale.IsMobileLayout ? 64f : 52f), "< Back", pickerBackCache))
                selectingSlot = -1;

            if (collection == null) return;

            List<PlayerInsectData> owned = collection.GetAllOwned();
            float listY = py + 96;
            float listH = panelH - 106;
            float itemH = UIScale.IsMobileLayout ? 132f : 116f;
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
                GUI.Label(new Rect(rect.x + 100, rect.y + 14, rect.width - 280, 42), GetOwnedDisplayName(pid, data), pickerNameCache);

                int cp = PlayerInsectCombatPower.Calculate(data, pid);
                GUI.Label(new Rect(rect.x + 100, rect.y + 62, rect.width - 280, 40),
                    $"Lv.{pid.level}  |  CP {cp}", pickerInfoCache);
            }

            if (alreadyInTeam)
            {
                GUI.Label(new Rect(rect.x + rect.width - 200, rect.y + rect.height / 2f - 21, 190, 42), "팀에 있음", pickerTagCache);
            }
            else
            {
                GUI.backgroundColor = BtnBg;
                float selectH = UIScale.IsMobileLayout ? 64f : 52f;
                if (GUI.Button(new Rect(rect.x + rect.width - 170, rect.y + rect.height / 2f - selectH * 0.5f, 150, selectH), "선택", pickerBtnCache))
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
