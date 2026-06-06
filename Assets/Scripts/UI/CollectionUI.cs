using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class CollectionUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private PlayerInsectCollection insectCollection;
        [SerializeField] private PlayerCandyInventory candyInventory;
        [SerializeField] private PlayerProgressController progressController;

        private bool isOpen;
        private Vector2 scrollPos;
        private int selectedTab;

        private string selectedInstanceId;
        private readonly string[] tabNames = { "보유 곤충", "통계" };

        // DrawInsectItem 핫스팟 캐시 — owned.Count × 5 GUIStyle/프레임 회피.
        // nameStyle/gradeStyle은 textColor만 동적 갱신(BattleScreenUI 패턴).
        private bool itemStylesReady;
        private GUIStyle itemNameStyle;
        private GUIStyle itemInfoStyle;
        private GUIStyle itemGradeStyle;
        private GUIStyle itemStatMiniStyle;
        private GUIStyle itemViewStyle;

        private static readonly Color ItemBgCol = new Color(0.12f, 0.14f, 0.2f, 0.92f);
        private static readonly Color ItemInfoGrayCol = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color ItemStatGrayCol = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color ItemViewBlueCol = new Color(0.25f, 0.35f, 0.55f);
        private static readonly Color EmptyDataCol = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color NoInsectCol = new Color(0.6f, 0.6f, 0.6f);

        // 잔여 영역(Panel/Detail/Stats/LevelUp/StatBar) 캐시 — 매 프레임 ~30 new GUIStyle 회피.
        private bool detailStylesReady;
        private GUIStyle panelTitleStyle;
        private GUIStyle panelCloseStyle;
        private GUIStyle panelTabActiveStyle;
        private GUIStyle panelTabInactiveStyle;
        private GUIStyle detailBackStyle;
        private GUIStyle detailNameStyle;        // textColor 동적
        private GUIStyle detailRarityStyle;      // textColor 동적
        private GUIStyle detailGradeDispStyle;   // textColor 동적
        private GUIStyle detailGradePercStyle;   // textColor 동적
        private GUIStyle detailDescStyle;
        private GUIStyle detailHintStyle;
        private GUIStyle statsLabelStyle;
        private GUIStyle statsValueStyle;
        private GUIStyle statsCandyValStyle;
        private GUIStyle luLvLabelStyle;
        private GUIStyle luLvNumStyle;
        private GUIStyle luXpLabelStyle;
        private GUIStyle luXpValStyle;
        private GUIStyle luMaxLvStyle;
        private GUIStyle luBtnStyle;
        private GUIStyle luCandyInfoStyle;       // textColor 동적
        private GUIStyle luMsgStyle;             // textColor 동적
        private GUIStyle barLabelStyle;
        private GUIStyle barIvStyle;             // textColor 동적
        private GUIStyle barTotalStyle;
        private GUIStyle barIvLabelStyle;
        private GUIStyle centeredLabelStyle;     // textColor 동적

        private static readonly Color PanelBgCol = new Color(0.05f, 0.07f, 0.12f, 0.95f);
        private static readonly Color PanelHeaderCol = new Color(0.15f, 0.18f, 0.25f, 1f);
        private static readonly Color TabActiveBgCol = new Color(0.3f, 0.5f, 0.9f);
        private static readonly Color TabInactiveBgCol = new Color(0.2f, 0.2f, 0.3f);
        private static readonly Color StatBlockBgCol = new Color(0.1f, 0.12f, 0.18f, 0.8f);
        private static readonly Color DescGrayCol = new Color(0.72f, 0.72f, 0.72f);
        private static readonly Color HintGreenCol = new Color(0.5f, 0.65f, 0.5f);
        private static readonly Color StatsLabelCol = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color StatsDividerCol = new Color(0.3f, 0.3f, 0.4f);
        private static readonly Color CandyValCol = new Color(1f, 0.5f, 0.8f);
        private static readonly Color LuBgCol = new Color(0.08f, 0.10f, 0.16f, 0.9f);
        private static readonly Color LuAccentBlueCol = new Color(0.3f, 0.7f, 1f);
        private static readonly Color LuLabelBlueCol = new Color(0.5f, 0.65f, 0.9f);
        private static readonly Color LuXpLabelCol = new Color(0.55f, 0.65f, 0.8f);
        private static readonly Color LuBarBgCol = new Color(0.06f, 0.06f, 0.1f);
        private static readonly Color LuBarFillDarkCol = new Color(0.2f, 0.5f, 0.9f);
        private static readonly Color LuBarFillLightCol = new Color(0.35f, 0.65f, 1f);
        private static readonly Color LuXpValCol = new Color(0.85f, 0.9f, 1f);
        private static readonly Color LuMaxLvCol = new Color(0.45f, 0.5f, 0.6f);
        private static readonly Color LuBtnGreenCol = new Color(0.2f, 0.5f, 0.3f);
        private static readonly Color LuBtnDisabledCol = new Color(0.15f, 0.15f, 0.18f);
        private static readonly Color LuCandyOkCol = new Color(1f, 0.7f, 0.85f);
        private static readonly Color LuCandyLowCol = new Color(0.4f, 0.35f, 0.4f);
        private static readonly Color BarBgCol = new Color(0.15f, 0.15f, 0.2f);
        private static readonly Color BarLabelGrayCol = new Color(0.75f, 0.75f, 0.75f);
        private static readonly Color BarTotalLightCol = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color BarIvLabelGrayCol = new Color(0.5f, 0.5f, 0.5f);

        private void InitDetailStyles()
        {
            if (detailStylesReady) return;
            panelTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            panelTitleStyle.normal.textColor = Color.white;
            panelCloseStyle = new GUIStyle(GUI.skin.button) { fontSize = 34, fontStyle = FontStyle.Bold };
            panelTabActiveStyle = new GUIStyle(GUI.skin.button) { fontSize = 30, fontStyle = FontStyle.Bold };
            panelTabInactiveStyle = new GUIStyle(GUI.skin.button) { fontSize = 30, fontStyle = FontStyle.Normal };
            detailBackStyle = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
            detailNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            detailRarityStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            detailGradeDispStyle = new GUIStyle(GUI.skin.label) { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            detailGradePercStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            detailDescStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, wordWrap = true };
            detailDescStyle.normal.textColor = DescGrayCol;
            detailHintStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Italic };
            detailHintStyle.normal.textColor = HintGreenCol;
            statsLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 32 };
            statsLabelStyle.normal.textColor = StatsLabelCol;
            statsValueStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            statsValueStyle.normal.textColor = Color.white;
            statsCandyValStyle = new GUIStyle(statsValueStyle);
            statsCandyValStyle.normal.textColor = CandyValCol;
            luLvLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            luLvLabelStyle.normal.textColor = LuLabelBlueCol;
            luLvNumStyle = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold };
            luLvNumStyle.normal.textColor = Color.white;
            luXpLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            luXpLabelStyle.normal.textColor = LuXpLabelCol;
            luXpValStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            luXpValStyle.normal.textColor = LuXpValCol;
            luMaxLvStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleRight };
            luMaxLvStyle.normal.textColor = LuMaxLvCol;
            luBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
            luCandyInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            luMsgStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            barLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            barLabelStyle.normal.textColor = BarLabelGrayCol;
            barIvStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            barTotalStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleRight };
            barTotalStyle.normal.textColor = BarTotalLightCol;
            barIvLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22 };
            barIvLabelStyle.normal.textColor = BarIvLabelGrayCol;
            centeredLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            detailStylesReady = true;
        }

        // GetAllOwned 매 프레임 호출 회피 — InsectUpdated 이벤트 시 invalidate.
        private List<PlayerInsectData> cachedOwned;
        private bool ownedCacheDirty = true;

        private void InitItemStyles()
        {
            if (itemStylesReady) return;
            itemNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
            itemInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = 26 };
            itemInfoStyle.normal.textColor = ItemInfoGrayCol;
            itemGradeStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            itemStatMiniStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleRight };
            itemStatMiniStyle.normal.textColor = ItemStatGrayCol;
            itemViewStyle = new GUIStyle(GUI.skin.button) { fontSize = 26 };
            itemStylesReady = true;
        }

        private List<PlayerInsectData> GetCachedOwned()
        {
            if (insectCollection == null) return null;
            if (ownedCacheDirty || cachedOwned == null)
            {
                cachedOwned = insectCollection.GetAllOwned();
                ownedCacheDirty = false;
            }
            return cachedOwned;
        }

        private void HandleInsectUpdated(PlayerInsectData _) { ownedCacheDirty = true; }

        private void OnEnable()
        {
            if (insectCollection != null)
            {
                insectCollection.InsectUpdated -= HandleInsectUpdated;
                insectCollection.InsectUpdated += HandleInsectUpdated;
            }
            ownedCacheDirty = true;
        }

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            if (!isOpen) selectedInstanceId = null;
            if (isOpen && TutorialQuestManager.Instance != null)
                TutorialQuestManager.Instance.NotifyCollectionOpened();
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }
        public void CloseModal()
        {
            isOpen = false;
            selectedInstanceId = null;
            ModalUIRegistry.Unregister(this);
        }
        private void OnDisable()
        {
            ModalUIRegistry.Unregister(this);
            if (insectCollection != null)
                insectCollection.InsectUpdated -= HandleInsectUpdated;
        }

        private void Update() { }

        private void OnGUI()
        {
            DrawToggleButton();

            if (!isOpen) return;

            UIScale.Begin();
            if (selectedInstanceId != null)
                DrawDetailPanel();
            else
                DrawPanel();
            UIScale.End();
        }

        private void DrawToggleButton()
        {
        }

        private void DrawPanel()
        {
            InitDetailStyles();
            float panelW = 900f;
            float panelH = 820f;
            float panelX = UIScale.VirtualScreenWidth - panelW - 24f;
            float panelY = 24f;

            GUI.color = PanelBgCol;
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = PanelHeaderCol;
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 72), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(panelX, panelY + 12, panelW - 70, 50), "컬렉션", panelTitleStyle);

            if (GUI.Button(new Rect(panelX + panelW - 60, panelY + 12, 50, 50), "X", panelCloseStyle))
            {
                CloseModal();
            }

            float tabY = panelY + 80;
            for (int i = 0; i < tabNames.Length; i++)
            {
                float tabX = panelX + i * 260 + 20;
                bool active = selectedTab == i;
                GUI.backgroundColor = active ? TabActiveBgCol : TabInactiveBgCol;
                if (GUI.Button(new Rect(tabX, tabY, 240, 56), tabNames[i], active ? panelTabActiveStyle : panelTabInactiveStyle))
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
                DrawCenteredLabel(area, "데이터 없음", EmptyDataCol);
                return;
            }

            List<PlayerInsectData> owned = GetCachedOwned();
            if (owned == null || owned.Count == 0)
            {
                DrawCenteredLabel(area, "아직 포획한 곤충이 없습니다!\n곤충에 가까이 가서 E키를 누르세요", NoInsectCol);
                return;
            }

            InitItemStyles();

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

            GUI.color = ItemBgCol;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            UIHelper.DrawRarityBorder(rect, rarityTier, Time.time);

            if (data != null)
                CapturePopupUI.DrawTypedInsectPortrait(rect.x + 60, rect.y + rect.height / 2f + 2, data.insectId, data.rarity, 1f);

            string displayName = GetOwnedDisplayName(pid, data);
            // 캐시 스타일 + textColor만 동적 갱신 (BattleScreenUI 패턴, owned.Count×5 GUIStyle/프레임 회피).
            itemNameStyle.normal.textColor = rarityColor;
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 110, rect.y + 10, rect.width - 260, 40), displayName, itemNameStyle);

            string rarityStr = data != null ? data.rarity.ToString() : "?";
            GUI.Label(new Rect(rect.x + 110, rect.y + 52, rect.width - 140, 32),
                $"Lv.{pid.level}  |  {rarityStr}  |  IV: {pid.IVPercent * 100:0}%", itemInfoStyle);

            string gradeStr = CapturePopupUI.GetGradeLabel(pid.Grade);
            Color gradeCol = UITheme.Instance.GetGradeColor(pid.Grade);
            itemGradeStyle.normal.textColor = gradeCol;
            GUI.Label(new Rect(rect.x + rect.width - 140, rect.y + 8, 120, 42), gradeStr, itemGradeStyle);

            if (data != null)
            {
                GUI.Label(new Rect(rect.x + rect.width - 200, rect.y + 50, 180, 28),
                    $"HP:{pid.ivHp} ATK:{pid.ivAtk} DEF:{pid.ivDef}", itemStatMiniStyle);
            }

            GUI.backgroundColor = ItemViewBlueCol;
            if (GUI.Button(new Rect(rect.x + rect.width - 130, rect.y + rect.height - 50, 120, 44), "상세", itemViewStyle))
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

            InitDetailStyles();

            float panelW = 900f;
            float panelH = 820f;
            float panelX = UIScale.VirtualScreenWidth - panelW - 24f;
            float panelY = 24f;

            GUI.color = PanelBgCol;
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            Color rarityCol = data != null ? GetRarityColor(data.rarity) : Color.gray;
            int detailRarityTier = data != null ? (int)data.rarity : 0;
            GUI.color = Color.white;

            Rect detailRect = new Rect(panelX, panelY, panelW, panelH);
            UIHelper.DrawRarityBorder(detailRect, detailRarityTier, Time.time);
            if (detailRarityTier >= 3)
                UIHelper.DrawRarityGlow(detailRect, rarityCol, detailRarityTier >= 4 ? 0.6f : 0.3f, Time.time);

            if (GUI.Button(new Rect(panelX + 14, panelY + 14, 130, 50), "< 뒤로", detailBackStyle))
                selectedInstanceId = null;

            if (GUI.Button(new Rect(panelX + panelW - 60, panelY + 14, 50, 50), "X", panelCloseStyle))
            {
                CloseModal();
            }

            float portraitCx = panelX + panelW / 2f;
            float portraitCy = panelY + 140;

            // 동적 색상(rarityCol scaled)은 struct stack 할당, GC 영향 없음 (BattleArenaController 판단 일관).
            GUI.color = new Color(rarityCol.r * 0.15f, rarityCol.g * 0.15f, rarityCol.b * 0.15f, 0.6f);
            GUI.DrawTexture(new Rect(portraitCx - 80, portraitCy - 80, 160, 160), Texture2D.whiteTexture);

            GUI.color = new Color(rarityCol.r, rarityCol.g, rarityCol.b, 0.2f);
            GUI.DrawTexture(new Rect(portraitCx - 75, portraitCy - 75, 150, 150), Texture2D.whiteTexture);

            InsectRarity rarity = data != null ? data.rarity : InsectRarity.Common;
            string insId = data != null ? data.insectId : pid.insectId;
            CapturePopupUI.DrawTypedInsectPortrait(portraitCx, portraitCy, insId, rarity, 1f);

            string displayName = GetOwnedDisplayName(pid, data);
            // 캐시 스타일 + textColor만 동적 갱신.
            detailNameStyle.normal.textColor = rarityCol;
            GUI.color = Color.white;
            GUI.Label(new Rect(panelX, panelY + 230, panelW, 50), displayName, detailNameStyle);

            detailRarityStyle.normal.textColor = new Color(rarityCol.r, rarityCol.g, rarityCol.b, 0.8f);
            GUI.Label(new Rect(panelX, panelY + 280, panelW, 34),
                data != null ? data.rarity.ToString() : "Unknown", detailRarityStyle);

            Color gradeCol = UITheme.Instance.GetGradeColor(pid.Grade);
            string gradeLabel = CapturePopupUI.GetGradeLabel(pid.Grade);

            detailGradeDispStyle.normal.textColor = gradeCol;
            GUI.Label(new Rect(panelX + panelW - 130, panelY + 224, 100, 68), gradeLabel, detailGradeDispStyle);

            detailGradePercStyle.normal.textColor = new Color(gradeCol.r, gradeCol.g, gradeCol.b, 0.7f);
            GUI.Label(new Rect(panelX + panelW - 130, panelY + 290, 100, 30),
                $"{pid.IVPercent * 100:0}%", detailGradePercStyle);

            float statY = panelY + 330;

            DrawLevelUpSection(panelX + 30, statY, panelW - 60, pid, data);

            float statBlockY = statY + 130;
            float statBlockH = 210f;
            GUI.color = StatBlockBgCol;
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
                GUI.Label(new Rect(panelX + 36, descY, panelW - 72, 80), data.description, detailDescStyle);
            }

            if (data != null && !string.IsNullOrEmpty(data.habitatHint))
            {
                float hintY = statBlockY + statBlockH + 86;
                GUI.Label(new Rect(panelX + 36, hintY, panelW - 72, 32),
                    $"서식지: {data.habitatHint}", detailHintStyle);
            }
        }

        private void DrawStats(Rect area)
        {
            InitDetailStyles();

            float y = area.y + 20;
            float rowH = 60f;
            float lw = area.width * 0.6f;
            float vw = area.width * 0.35f;

            // GetCachedOwned 사용 — 매 프레임 List 할당 회피.
            List<PlayerInsectData> ownedList = GetCachedOwned();
            int total = ownedList != null ? ownedList.Count : 0;
            int candy = candyInventory != null ? candyInventory.Candies : 0;
            int level = progressController != null ? progressController.Level : 1;
            int xp = progressController != null ? progressController.CurrentXp : 0;

            DrawStatRow(area.x, ref y, rowH, lw, vw, "플레이어 레벨", $"{level}", statsLabelStyle, statsValueStyle);
            DrawStatRow(area.x, ref y, rowH, lw, vw, "경험치", $"{xp}", statsLabelStyle, statsValueStyle);

            y += 12;
            GUI.color = StatsDividerCol;
            GUI.DrawTexture(new Rect(area.x, y, area.width, 1), Texture2D.whiteTexture);
            y += 12;
            GUI.color = Color.white;

            DrawStatRow(area.x, ref y, rowH, lw, vw, "포획한 곤충", $"{total}", statsLabelStyle, statsValueStyle);
            DrawStatRow(area.x, ref y, rowH, lw, vw, "캔디", $"{candy}", statsLabelStyle, statsCandyValStyle);
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
            InitDetailStyles();
            GUI.color = LuBgCol;
            GUI.DrawTexture(new Rect(x, y, w, 120), Texture2D.whiteTexture);
            GUI.color = LuAccentBlueCol;
            GUI.DrawTexture(new Rect(x, y, w, 3), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 14, y + 8, 100, 20), "LEVEL", luLvLabelStyle);
            GUI.Label(new Rect(x + 14, y + 28, 80, 44), pid.level.ToString(), luLvNumStyle);

            int maxLv = insectCollection != null ? insectCollection.GetMaxLevel(pid.insectId) : 50;
            int candyCost = insectCollection != null ? insectCollection.GetCandyCostForLevel(pid.insectId, pid.level) : (4 + (pid.level - 1) * 2);
            bool isMaxLevel = pid.level >= maxLv;
            int xpNeeded = insectCollection != null ? insectCollection.GetXpToNextLevel(pid.insectId, pid.level) : (20 + (pid.level - 1) * 8);
            float xpRatio = xpNeeded > 0 ? Mathf.Clamp01((float)pid.currentXp / xpNeeded) : 1f;

            float barX = x + 100;
            float barW = w - 280;
            float barH = 18f;
            float barY2 = y + 34;

            GUI.Label(new Rect(barX, y + 14, barW, 18), isMaxLevel ? "MAX LEVEL" : "경험치 (EXP)", luXpLabelStyle);

            GUI.color = LuBarBgCol;
            GUI.DrawTexture(new Rect(barX, barY2, barW, barH), Texture2D.whiteTexture);

            if (!isMaxLevel && xpRatio > 0)
            {
                GUI.color = LuBarFillDarkCol;
                GUI.DrawTexture(new Rect(barX, barY2 + barH / 2, barW * xpRatio, barH / 2), Texture2D.whiteTexture);
                GUI.color = LuBarFillLightCol;
                GUI.DrawTexture(new Rect(barX, barY2, barW * xpRatio, barH / 2), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(barX, barY2, barW, barH),
                isMaxLevel ? "MAX" : $"{pid.currentXp} / {xpNeeded}", luXpValStyle);

            GUI.Label(new Rect(barX, barY2 + barH + 2, barW, 16), $"최대 Lv.{maxLv}", luMaxLvStyle);

            float btnX = x + w - 168;
            float btnY2 = y + 14;
            float btnW2 = 154f;
            float btnH2 = 56f;

            int currentCandy = candyInventory != null ? candyInventory.Candies : 0;
            bool canAfford = currentCandy >= candyCost && !isMaxLevel;

            GUI.backgroundColor = canAfford ? LuBtnGreenCol : LuBtnDisabledCol;
            GUI.enabled = canAfford;
            if (GUI.Button(new Rect(btnX, btnY2, btnW2, btnH2),
                isMaxLevel ? "MAX" : $"레벨업\n<size=16>캔디 {candyCost}</size>", luBtnStyle))
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

            // luCandyInfoStyle textColor 동적 갱신 (canAfford 따라).
            luCandyInfoStyle.normal.textColor = canAfford ? LuCandyOkCol : LuCandyLowCol;
            GUI.Label(new Rect(btnX, btnY2 + btnH2 + 2, btnW2, 18),
                $"보유: {currentCandy} 캔디", luCandyInfoStyle);

            if (levelUpMsgTimer > 0)
            {
                levelUpMsgTimer -= Time.deltaTime;
                float alpha = Mathf.Clamp01(levelUpMsgTimer / 0.5f);
                bool success = levelUpMsg == "레벨 업!";
                // luMsgStyle textColor 동적 갱신 (alpha 변동이라 매 호출 new Color, struct stack).
                luMsgStyle.normal.textColor = success
                    ? new Color(0.3f, 1f, 0.5f, alpha)
                    : new Color(1f, 0.4f, 0.3f, alpha);
                GUI.Label(new Rect(x, y + 90, w, 30), levelUpMsg, luMsgStyle);
            }
        }

        private void DrawStatBar(float x, float y, float w, string label, int iv, int total, int baseStat)
        {
            InitDetailStyles();
            GUI.Label(new Rect(x, y, 80, 32), label, barLabelStyle);

            float barX = x + 90;
            float barW = w - 240;
            float barH = 24f;
            float barY2 = y + 4;

            GUI.color = BarBgCol;
            GUI.DrawTexture(new Rect(barX, barY2, barW, barH), Texture2D.whiteTexture);

            float ivRatio = iv / (float)PlayerInsectData.MaxIV;
            Color barCol = CapturePopupUI.GetIVBarColor(iv);
            GUI.color = barCol;
            GUI.DrawTexture(new Rect(barX, barY2, barW * ivRatio, barH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // barIvStyle textColor 동적 갱신 (barCol 따라).
            barIvStyle.normal.textColor = barCol;
            GUI.Label(new Rect(barX + barW + 8, y, 56, 30), $"{iv}", barIvStyle);

            GUI.Label(new Rect(x + w - 80, y, 80, 30), $"{total}", barTotalStyle);

            GUI.Label(new Rect(x, y + 32, w, 24),
                $"기본 {baseStat} + IV {iv} + Lv 보너스", barIvLabelStyle);
        }

        private void DrawCenteredLabel(Rect area, string text, Color color)
        {
            InitDetailStyles();
            centeredLabelStyle.normal.textColor = color;
            GUI.Label(area, text, centeredLabelStyle);
        }

        private Color GetRarityColor(InsectRarity rarity)
        {
            return UITheme.Instance.GetInsectRarityColor(rarity);
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
            // AutoWire가 OnEnable 이후 호출되는 경우 구독 누락 차단 — isActiveAndEnabled 시 구독 시도.
            if (insectCollection != collection)
            {
                if (insectCollection != null)
                    insectCollection.InsectUpdated -= HandleInsectUpdated;
                insectCollection = collection;
                if (insectCollection != null && isActiveAndEnabled)
                {
                    insectCollection.InsectUpdated -= HandleInsectUpdated;
                    insectCollection.InsectUpdated += HandleInsectUpdated;
                }
                ownedCacheDirty = true;
            }
            if (candyInventory == null) candyInventory = candy;
            if (progressController == null) progressController = progress;
        }
    }
}
