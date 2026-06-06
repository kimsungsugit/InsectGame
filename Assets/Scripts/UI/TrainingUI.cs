using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class TrainingUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private TrainingManager trainingManager;
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private PlayerCandyInventory candyInventory;

        private bool isOpen;

        private enum Page { InsectSelect, MethodSelect, SkillLearn, SkillEquip, SkillReplace }
        private Page page;
        private string selectedInstanceId;
        private int selectedMethodIndex = -1;
        private string pendingNewSkillId;
        private Vector2 scrollPos;
        private string feedbackMsg;
        private float feedbackTimer;

        // DrawInsectSelect 핫스팟 캐시 — owned.Count × 4 GUIStyle/프레임 회피.
        private bool insectSelectStylesReady;
        private GUIStyle insectSelectSubStyle;
        private GUIStyle insectSelectNameStyle; // textColor 동적
        private GUIStyle insectSelectInfoStyle;
        private GUIStyle insectSelectBtnStyle;

        private static readonly Color TrainSubGrayCol = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color TrainItemBgCol = new Color(0.1f, 0.12f, 0.18f, 0.85f);
        private static readonly Color TrainInfoGrayCol = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color TrainBtnGreenCol = new Color(0.3f, 0.45f, 0.25f);

        // 잔여 영역(Panel/Back/Method/SkillLearn/SkillEquip/SkillReplace/SkillCard/Feedback) 캐시.
        private bool trainDetailStylesReady;
        private GUIStyle panelTitleStyle;
        private GUIStyle panelCloseStyle;
        private GUIStyle backBtnStyle;
        private GUIStyle methodNameStyle;          // textColor 동적 (rc)
        private GUIStyle methodEquipBtnStyle;
        private GUIStyle methodCardNameStyle;      // textColor 동적
        private GUIStyle methodCardDescStyle;
        private GUIStyle methodCostStyle;          // textColor 동적 (canTrain)
        private GUIStyle methodLockStyle;
        private GUIStyle methodCountStyle;
        private GUIStyle methodTrainBtnStyle;
        private GUIStyle learnHeaderStyle;         // textColor 동적 (theme)
        private GUIStyle learnBtnStyle;
        private GUIStyle learnedTagStyle;
        private GUIStyle equipNameStyle;           // textColor 동적
        private GUIStyle equipSlotNumStyle;
        private GUIStyle equipSlotNameStyle;       // textColor 동적
        private GUIStyle equipSlotInfoStyle;
        private GUIStyle equipRemBtnStyle;
        private GUIStyle equipEmptyStyle;
        private GUIStyle equipLearnedHeaderStyle;
        private GUIStyle equipLearnedNameStyle;    // textColor 동적
        private GUIStyle equipLearnedInfoStyle;
        private GUIStyle equipEqBtnStyle;
        private GUIStyle equipEqTagStyle;
        private GUIStyle replaceHeaderStyle;
        private GUIStyle replaceNewStyle;          // textColor 동적
        private GUIStyle replaceOldNameStyle;      // textColor 동적
        private GUIStyle replaceOldInfoStyle;
        private GUIStyle replaceForgetBtnStyle;
        private GUIStyle replaceCancelBtnStyle;
        private GUIStyle cardSkillNameStyle;       // textColor 동적
        private GUIStyle cardDescStyle;
        private GUIStyle cardCdStyle;
        private GUIStyle feedbackStyle;            // textColor 동적 (alpha)

        private static readonly Color PanelBgCol = new Color(0.04f, 0.06f, 0.1f, 0.96f);
        private static readonly Color PanelHeaderCol = new Color(0.15f, 0.2f, 0.3f);
        private static readonly Color PanelAccentOrangeCol = new Color(0.9f, 0.6f, 0.2f);
        private static readonly Color PanelTitleYellowCol = new Color(1f, 0.85f, 0.4f);
        private static readonly Color MethodEquipBtnCol = new Color(0.2f, 0.35f, 0.55f);
        private static readonly Color MethodCardDarkCol = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color MethodNameDimCol = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color MethodDescGrayCol = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color MethodCostOkCol = new Color(1f, 0.5f, 0.8f);
        private static readonly Color MethodCostBadCol = new Color(0.5f, 0.3f, 0.3f);
        private static readonly Color MethodLockRedCol = new Color(1f, 0.4f, 0.3f);
        private static readonly Color MethodCountGrayCol = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color MethodTrainOkBgCol = new Color(0.3f, 0.5f, 0.25f);
        private static readonly Color MethodTrainOffBgCol = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color LearnBtnFullCol = new Color(0.5f, 0.35f, 0.2f);
        private static readonly Color LearnBtnOkCol = new Color(0.25f, 0.5f, 0.3f);
        private static readonly Color LearnBtnOffCol = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color LearnedTagGreenCol = new Color(0.3f, 0.9f, 0.5f);
        private static readonly Color EquipSlotBgCol = new Color(0.1f, 0.12f, 0.18f, 0.85f);
        private static readonly Color EquipSlotNumGrayCol = new Color(0.3f, 0.3f, 0.4f);
        private static readonly Color EquipInfoGrayCol = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color EquipRemBgCol = new Color(0.4f, 0.2f, 0.2f);
        private static readonly Color EquipEmptyGrayCol = new Color(0.35f, 0.35f, 0.4f);
        private static readonly Color EquipLearnedHeaderBlueCol = new Color(0.7f, 0.75f, 1f);
        private static readonly Color EquipLearnedBgCol = new Color(0.08f, 0.1f, 0.15f, 0.8f);
        private static readonly Color EquipNameDimCol = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color EquipLearnedInfoCol = new Color(0.45f, 0.45f, 0.45f);
        private static readonly Color EquipEqBgCol = new Color(0.2f, 0.4f, 0.3f);
        private static readonly Color EquipEqTagGreenCol = new Color(0.4f, 0.7f, 0.4f);
        private static readonly Color ReplaceHeaderGrayCol = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color ReplaceOldBgCol = new Color(0.1f, 0.12f, 0.18f, 0.85f);
        private static readonly Color ReplaceOldInfoCol = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color ReplaceForgetBgCol = new Color(0.55f, 0.2f, 0.2f);
        private static readonly Color ReplaceCancelBgCol = new Color(0.3f, 0.3f, 0.35f);
        private static readonly Color CardLearnedBgCol = new Color(0.08f, 0.1f, 0.14f, 0.7f);
        private static readonly Color CardActiveBgCol = new Color(0.1f, 0.12f, 0.18f, 0.85f);
        private static readonly Color CardLearnedNameCol = new Color(0.45f, 0.45f, 0.45f);
        private static readonly Color CardDescGrayCol = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color CardCdGrayCol = new Color(0.45f, 0.45f, 0.5f);

        private void InitTrainDetailStyles()
        {
            if (trainDetailStylesReady) return;
            panelTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            panelTitleStyle.normal.textColor = PanelTitleYellowCol;
            panelCloseStyle = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
            backBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
            methodNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            methodEquipBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
            methodCardNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            methodCardDescStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
            methodCardDescStyle.normal.textColor = MethodDescGrayCol;
            methodCostStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleRight };
            methodLockStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleRight };
            methodLockStyle.normal.textColor = MethodLockRedCol;
            methodCountStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleRight };
            methodCountStyle.normal.textColor = MethodCountGrayCol;
            methodTrainBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
            learnHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            learnBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
            learnedTagStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            learnedTagStyle.normal.textColor = LearnedTagGreenCol;
            equipNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            equipSlotNumStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            equipSlotNumStyle.normal.textColor = EquipSlotNumGrayCol;
            equipSlotNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            equipSlotInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = 24 };
            equipSlotInfoStyle.normal.textColor = EquipInfoGrayCol;
            equipRemBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 24 };
            equipEmptyStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Italic };
            equipEmptyStyle.normal.textColor = EquipEmptyGrayCol;
            equipLearnedHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            equipLearnedHeaderStyle.normal.textColor = EquipLearnedHeaderBlueCol;
            equipLearnedNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            equipLearnedInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = 22 };
            equipLearnedInfoStyle.normal.textColor = EquipLearnedInfoCol;
            equipEqBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 22 };
            equipEqTagStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleRight };
            equipEqTagStyle.normal.textColor = EquipEqTagGreenCol;
            replaceHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            replaceHeaderStyle.normal.textColor = ReplaceHeaderGrayCol;
            replaceNewStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            replaceOldNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            replaceOldInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = 24 };
            replaceOldInfoStyle.normal.textColor = ReplaceOldInfoCol;
            replaceForgetBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
            replaceCancelBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 24 };
            cardSkillNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            cardDescStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
            cardDescStyle.normal.textColor = CardDescGrayCol;
            cardCdStyle = new GUIStyle(GUI.skin.label) { fontSize = 22 };
            cardCdStyle.normal.textColor = CardCdGrayCol;
            feedbackStyle = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            trainDetailStylesReady = true;
        }

        // GetAllOwned 매 프레임 호출 회피 — InsectUpdated 이벤트로 invalidate (CollectionUI 패턴).
        private List<PlayerInsectData> cachedOwned;
        private bool ownedCacheDirty = true;

        private void InitInsectSelectStyles()
        {
            if (insectSelectStylesReady) return;
            insectSelectSubStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            insectSelectSubStyle.normal.textColor = TrainSubGrayCol;
            insectSelectNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            insectSelectInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = 24 };
            insectSelectInfoStyle.normal.textColor = TrainInfoGrayCol;
            insectSelectBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 26 };
            insectSelectStylesReady = true;
        }

        private List<PlayerInsectData> GetCachedOwned()
        {
            if (collection == null) return null;
            if (ownedCacheDirty || cachedOwned == null)
            {
                cachedOwned = collection.GetAllOwned();
                ownedCacheDirty = false;
            }
            return cachedOwned;
        }

        private void HandleInsectUpdated(PlayerInsectData _) { ownedCacheDirty = true; }

        private void OnEnable()
        {
            if (collection != null)
            {
                collection.InsectUpdated -= HandleInsectUpdated;
                collection.InsectUpdated += HandleInsectUpdated;
            }
            ownedCacheDirty = true;
        }

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            if (isOpen) { page = Page.InsectSelect; selectedInstanceId = null; selectedMethodIndex = -1; }
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }
        public void CloseModal()
        {
            isOpen = false;
            ModalUIRegistry.Unregister(this);
        }
        private void OnDisable()
        {
            ModalUIRegistry.Unregister(this);
            if (collection != null)
                collection.InsectUpdated -= HandleInsectUpdated;
        }

        private void Update()
        {
            if (feedbackTimer > 0) feedbackTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            UIScale.Begin();
            switch (page)
            {
                case Page.InsectSelect: DrawInsectSelect(); break;
                case Page.MethodSelect: DrawMethodSelect(); break;
                case Page.SkillLearn: DrawSkillLearn(); break;
                case Page.SkillEquip: DrawSkillEquip(); break;
                case Page.SkillReplace: DrawSkillReplace(); break;
            }

            if (feedbackTimer > 0)
                DrawFeedback();
            UIScale.End();
        }

        private void DrawPanel(string title, out float px, out float py, out float pw, out float ph)
        {
            InitTrainDetailStyles();
            pw = 1000f; ph = 900f;
            px = (UIScale.VirtualScreenWidth - pw) / 2f;
            py = (UIScale.VirtualScreenHeight - ph) / 2f;

            GUI.color = PanelBgCol;
            GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);
            GUI.color = PanelHeaderCol;
            GUI.DrawTexture(new Rect(px, py, pw, 70), Texture2D.whiteTexture);
            GUI.color = PanelAccentOrangeCol;
            GUI.DrawTexture(new Rect(px, py + 70, pw, 5), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(px + 120, py + 10, pw - 240, 50), title, panelTitleStyle);

            if (GUI.Button(new Rect(px + pw - 60, py + 12, 50, 46), "X", panelCloseStyle))
                CloseModal();
        }

        private bool DrawBackButton(float px, float py)
        {
            InitTrainDetailStyles();
            return GUI.Button(new Rect(px + 12, py + 12, 110, 46), "< Back", backBtnStyle);
        }

        private void DrawInsectSelect()
        {
            DrawPanel("TRAINING CENTER", out float px, out float py, out float pw, out float ph);

            if (collection == null) return;
            List<PlayerInsectData> owned = GetCachedOwned();
            if (owned == null) return;

            InitInsectSelectStyles();

            int candy = candyInventory != null ? candyInventory.Candies : 0;
            GUI.Label(new Rect(px, py + 76, pw, 36), $"훈련할 곤충을 선택하세요  |  캔디: {candy}", insectSelectSubStyle);

            float listY = py + 120;
            float listH = ph - 130;
            float itemH = 108f;
            Rect area = new Rect(px + 10, listY, pw - 20, listH);
            Rect view = new Rect(0, 0, area.width - 20, owned.Count * itemH);
            scrollPos = GUI.BeginScrollView(area, scrollPos, view);

            for (int i = 0; i < owned.Count; i++)
            {
                PlayerInsectData pid = owned[i];
                InsectData data = collection.GetInsectData(pid.insectId);
                Rect r = new Rect(0, i * itemH, view.width, itemH - 3);

                Color rc = data != null ? UITheme.Instance.GetInsectRarityColor(data.rarity) : Color.gray;
                GUI.color = TrainItemBgCol;
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = rc;
                GUI.DrawTexture(new Rect(r.x, r.y, 6, r.height), Texture2D.whiteTexture);

                if (data != null)
                    CapturePopupUI.DrawTypedInsectPortrait(r.x + 60, r.y + r.height / 2f, data.insectId, data.rarity, 1f);

                // 캐시 스타일 + textColor만 동적 갱신 (BattleScreenUI 패턴).
                insectSelectNameStyle.normal.textColor = rc;
                GUI.color = Color.white;
                string name = data != null
                    ? $"{data.displayName} #{GetShortInstanceId(pid)}"
                    : $"{pid.insectId} #{GetShortInstanceId(pid)}";
                GUI.Label(new Rect(r.x + 100, r.y + 10, r.width - 250, 36), name, insectSelectNameStyle);

                int learned = pid.learnedSkillIds != null ? pid.learnedSkillIds.Count : 0;
                GUI.Label(new Rect(r.x + 100, r.y + 46, r.width - 250, 30),
                    $"Lv.{pid.level}  |  스킬: {learned}/{PlayerInsectData.MaxLearnedSkills}  |  장착: {pid.EquippedCount()}/{PlayerInsectData.MaxEquipSlots}", insectSelectInfoStyle);

                GUI.backgroundColor = TrainBtnGreenCol;
                if (GUI.Button(new Rect(r.x + r.width - 140, r.y + r.height / 2f - 24, 120, 48), "훈련", insectSelectBtnStyle))
                {
                    selectedInstanceId = pid.instanceId;
                    page = Page.MethodSelect;
                    scrollPos = Vector2.zero;
                }
                GUI.backgroundColor = Color.white;
            }
            GUI.EndScrollView();
        }

        private void DrawMethodSelect()
        {
            DrawPanel("CHOOSE TRAINING", out float px, out float py, out float pw, out float ph);
            if (DrawBackButton(px, py)) { page = Page.InsectSelect; return; }

            PlayerInsectData pid = GetPid();
            InsectData data = pid != null ? collection.GetInsectData(pid.insectId) : null;
            if (pid == null) { page = Page.InsectSelect; return; }

            Color rc = data != null ? UITheme.Instance.GetInsectRarityColor(data.rarity) : Color.gray;
            // 캐시 + textColor 동적 갱신.
            methodNameStyle.normal.textColor = rc;
            GUI.Label(new Rect(px, py + 76, pw, 36), data != null ? $"{data.displayName} Lv.{pid.level}" : pid.insectId, methodNameStyle);

            GUI.backgroundColor = MethodEquipBtnCol;
            if (GUI.Button(new Rect(px + pw - 210, py + 76, 190, 44), "스킬 장착", methodEquipBtnStyle))
            {
                page = Page.SkillEquip;
                scrollPos = Vector2.zero;
            }
            GUI.backgroundColor = Color.white;

            if (trainingManager == null || trainingManager.Methods == null) return;

            float startY = py + 130;
            float cardH = 150f;
            TrainingMethod[] methods = trainingManager.Methods;

            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                float cy = startY + i * (cardH + 6);
                bool canTrain = trainingManager.CanTrain(m, pid);
                bool levelOk = pid.level >= m.requiredLevel;

                // themeColor scaled new Color는 struct stack 거짓양성 (BattleArenaController 판단 일관).
                GUI.color = new Color(m.themeColor.r * 0.15f, m.themeColor.g * 0.15f, m.themeColor.b * 0.15f, 0.8f);
                GUI.DrawTexture(new Rect(px + 15, cy, pw - 30, cardH), Texture2D.whiteTexture);
                GUI.color = levelOk ? m.themeColor : MethodCardDarkCol;
                GUI.DrawTexture(new Rect(px + 15, cy, 6, cardH), Texture2D.whiteTexture);

                methodCardNameStyle.normal.textColor = levelOk ? m.themeColor : MethodNameDimCol;
                GUI.color = Color.white;
                GUI.Label(new Rect(px + 38, cy + 14, pw - 250, 36), m.displayName, methodCardNameStyle);

                GUI.Label(new Rect(px + 38, cy + 50, pw - 260, 60), m.description, methodCardDescStyle);

                methodCostStyle.normal.textColor = canTrain ? MethodCostOkCol : MethodCostBadCol;
                GUI.Label(new Rect(px + pw - 250, cy + 14, 170, 30), $"비용: {m.candyCost}", methodCostStyle);

                if (!levelOk)
                {
                    GUI.Label(new Rect(px + pw - 250, cy + 50, 170, 30), $"Lv.{m.requiredLevel} 필요", methodLockStyle);
                }

                int skillCount = m.skillPool != null ? m.skillPool.Length : 0;
                GUI.Label(new Rect(px + pw - 250, cy + cardH - 40, 170, 28), $"스킬 {skillCount}개", methodCountStyle);

                GUI.backgroundColor = canTrain ? MethodTrainOkBgCol : MethodTrainOffBgCol;
                GUI.enabled = canTrain;
                if (GUI.Button(new Rect(px + pw - 170, cy + cardH / 2f - 22, 130, 46), "시작", methodTrainBtnStyle))
                {
                    selectedMethodIndex = i;
                    page = Page.SkillLearn;
                    scrollPos = Vector2.zero;
                }
                GUI.enabled = true;
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawSkillLearn()
        {
            DrawPanel("LEARN SKILLS", out float px, out float py, out float pw, out float ph);
            if (DrawBackButton(px, py)) { page = Page.MethodSelect; return; }

            PlayerInsectData pid = GetPid();
            if (pid == null || trainingManager == null || selectedMethodIndex < 0) { page = Page.MethodSelect; return; }

            TrainingMethod method = trainingManager.Methods[selectedMethodIndex];
            InsectSkill[] skills = trainingManager.GetAvailableSkills(method, pid);

            learnHeaderStyle.normal.textColor = method.themeColor;
            GUI.Label(new Rect(px, py + 76, pw, 34), $"{method.displayName}  |  Cost: {method.candyCost} Candy", learnHeaderStyle);

            float listY = py + 120;
            float listH = ph - 130;
            float itemH = 130f;
            Rect area = new Rect(px + 10, listY, pw - 20, listH);
            Rect view = new Rect(0, 0, area.width - 20, skills.Length * itemH);
            scrollPos = GUI.BeginScrollView(area, scrollPos, view);

            for (int i = 0; i < skills.Length; i++)
            {
                InsectSkill skill = skills[i];
                if (skill == null) continue;
                Rect r = new Rect(0, i * itemH, view.width, itemH - 4);
                bool learned = pid.HasLearnedSkill(skill.skillId);

                DrawSkillCard(r, skill, learned, method.themeColor);

                if (!learned)
                {
                    bool canAfford = trainingManager.CanTrain(method, pid);
                    bool isFull = pid.IsSkillsFull();
                    string btnLabel = isFull ? "Replace" : "Learn";
                    GUI.backgroundColor = canAfford ? (isFull ? LearnBtnFullCol : LearnBtnOkCol) : LearnBtnOffCol;
                    GUI.enabled = canAfford;
                    if (GUI.Button(new Rect(r.x + r.width - 130, r.y + r.height / 2f - 23, 110, 46), btnLabel, learnBtnStyle))
                    {
                        if (isFull)
                        {
                            pendingNewSkillId = skill.skillId;
                            page = Page.SkillReplace;
                            scrollPos = Vector2.zero;
                        }
                        else if (trainingManager.TrainSkill(method, pid, skill.skillId))
                        {
                            feedbackMsg = $"{skill.displayName} learned!";
                            feedbackTimer = 2f;
                        }
                    }
                    GUI.enabled = true;
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.Label(new Rect(r.x + r.width - 150, r.y + r.height / 2f - 17, 140, 34), "습득완료", learnedTagStyle);
                }
            }
            GUI.EndScrollView();
        }

        private void DrawSkillEquip()
        {
            DrawPanel("EQUIP SKILLS", out float px, out float py, out float pw, out float ph);
            if (DrawBackButton(px, py)) { page = Page.MethodSelect; return; }

            PlayerInsectData pid = GetPid();
            if (pid == null) { page = Page.InsectSelect; return; }

            InsectData data = collection.GetInsectData(pid.insectId);
            Color rc = data != null ? UITheme.Instance.GetInsectRarityColor(data.rarity) : Color.gray;

            equipNameStyle.normal.textColor = rc;
            GUI.Label(new Rect(px, py + 76, pw, 34),
                $"{(data != null ? data.displayName : pid.insectId)} - Skill Slots", equipNameStyle);

            float slotY = py + 120;
            float slotH = 90f;

            for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
            {
                float sy = slotY + i * (slotH + 4);
                string eqId = pid.GetEquippedSkill(i);
                InsectSkill eqSkill = eqId != null ? trainingManager.GetSkill(eqId) : null;

                GUI.color = EquipSlotBgCol;
                GUI.DrawTexture(new Rect(px + 15, sy, pw - 30, slotH), Texture2D.whiteTexture);

                GUI.color = Color.white;
                GUI.Label(new Rect(px + 15, sy, 52, slotH), $"{i + 1}", equipSlotNumStyle);

                if (eqSkill != null)
                {
                    Color sc = GetSkillColor(eqSkill.effectType);
                    GUI.color = sc;
                    GUI.DrawTexture(new Rect(px + 15, sy, 5, slotH), Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    equipSlotNameStyle.normal.textColor = sc;
                    GUI.Label(new Rect(px + 80, sy + 10, pw - 290, 32), eqSkill.displayName, equipSlotNameStyle);

                    string typeStr = eqSkill.effectType == SkillEffectType.Damage ? $"DMG {eqSkill.power}" :
                                     eqSkill.effectType == SkillEffectType.BuffAttack ? $"ATK UP +{eqSkill.effectValue * 100:0}%" :
                                     $"ATK DOWN -{eqSkill.effectValue * 100:0}%";
                    GUI.Label(new Rect(px + 80, sy + 44, pw - 290, 30), $"{typeStr}  |  CD: {eqSkill.cooldownTurns}t", equipSlotInfoStyle);

                    GUI.backgroundColor = EquipRemBgCol;
                    if (GUI.Button(new Rect(px + pw - 150, sy + 10, 110, 38), "해제", equipRemBtnStyle))
                    {
                        pid.EquipSkill(null, i);
                        collection.ForceSave();
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.Label(new Rect(px + 80, sy + slotH / 2f - 17, pw - 170, 34), "빈 슬롯", equipEmptyStyle);
                }
            }

            float learnedY = slotY + PlayerInsectData.MaxEquipSlots * (slotH + 4) + 10;
            GUI.Label(new Rect(px + 15, learnedY, pw - 30, 34), "Learned Skills:", equipLearnedHeaderStyle);

            float listY2 = learnedY + 40;
            float listH2 = ph - (listY2 - py) - 10;
            float itemH2 = 68f;

            List<string> learned = pid.learnedSkillIds ?? new List<string>();
            Rect area = new Rect(px + 15, listY2, pw - 30, listH2);
            Rect viewR = new Rect(0, 0, area.width - 20, learned.Count * itemH2);
            scrollPos = GUI.BeginScrollView(area, scrollPos, viewR);

            for (int i = 0; i < learned.Count; i++)
            {
                InsectSkill sk = trainingManager.GetSkill(learned[i]);
                if (sk == null) continue;

                Rect r = new Rect(0, i * itemH2, viewR.width, itemH2 - 3);
                bool isEquipped = IsEquipped(pid, sk.skillId);

                GUI.color = EquipLearnedBgCol;
                GUI.DrawTexture(r, Texture2D.whiteTexture);

                Color sc2 = GetSkillColor(sk.effectType);
                GUI.color = sc2;
                GUI.DrawTexture(new Rect(r.x, r.y, 5, r.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                equipLearnedNameStyle.normal.textColor = isEquipped ? EquipNameDimCol : sc2;
                GUI.Label(new Rect(r.x + 14, r.y + 6, r.width - 180, 32), sk.displayName, equipLearnedNameStyle);

                GUI.Label(new Rect(r.x + 14, r.y + 38, r.width - 180, 26),
                    sk.effectType == SkillEffectType.Damage ? $"DMG {sk.power}" : sk.effectType.ToString(), equipLearnedInfoStyle);

                if (!isEquipped && pid.EquippedCount() < PlayerInsectData.MaxEquipSlots)
                {
                    GUI.backgroundColor = EquipEqBgCol;
                    if (GUI.Button(new Rect(r.x + r.width - 100, r.y + r.height / 2f - 18, 88, 36), "Equip", equipEqBtnStyle))
                    {
                        for (int s = 0; s < PlayerInsectData.MaxEquipSlots; s++)
                        {
                            if (pid.GetEquippedSkill(s) == null)
                            {
                                pid.EquipSkill(sk.skillId, s);
                                collection.ForceSave();
                                // q_equip 진행도 — 사용자 직접 장착만 카운트
                                // (TrainingManager 자동 장착/PlayerInsectCollection 마이그레이션은 제외)
                                InsectGame.Core.TutorialQuestManager.Instance?.NotifySkillEquipped();
                                break;
                            }
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
                else if (isEquipped)
                {
                    GUI.Label(new Rect(r.x + r.width - 130, r.y + r.height / 2f - 15, 120, 30), "장착중", equipEqTagStyle);
                }
            }
            GUI.EndScrollView();
        }

        private void DrawSkillReplace()
        {
            DrawPanel("REPLACE SKILL", out float px, out float py, out float pw, out float ph);
            if (DrawBackButton(px, py)) { page = Page.SkillLearn; pendingNewSkillId = null; return; }

            PlayerInsectData pid = GetPid();
            if (pid == null || trainingManager == null || string.IsNullOrEmpty(pendingNewSkillId))
            { page = Page.SkillLearn; return; }

            InsectSkill newSkill = trainingManager.GetSkill(pendingNewSkillId);
            if (newSkill == null) { page = Page.SkillLearn; return; }

            GUI.Label(new Rect(px, py + 76, pw, 36), $"스킬이 가득 찼습니다 ({PlayerInsectData.MaxLearnedSkills}/{PlayerInsectData.MaxLearnedSkills})! 잊을 스킬을 선택하세요:", replaceHeaderStyle);

            Color nc = GetSkillColor(newSkill.effectType);
            replaceNewStyle.normal.textColor = nc;
            string newInfo = newSkill.effectType == SkillEffectType.Damage ? $"DMG {newSkill.power}" :
                             newSkill.effectType == SkillEffectType.BuffAttack ? $"ATK UP +{newSkill.effectValue * 100:0}%" :
                             $"ATK DOWN -{newSkill.effectValue * 100:0}%";
            GUI.Label(new Rect(px, py + 116, pw, 34), $"New: {newSkill.displayName}  ({newInfo})", replaceNewStyle);

            GUI.color = nc;
            GUI.DrawTexture(new Rect(px + 100, py + 154, pw - 200, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float startY = py + 170;
            float cardH = 120f;
            System.Collections.Generic.List<string> learned = pid.learnedSkillIds ?? new System.Collections.Generic.List<string>();

            TrainingMethod method = selectedMethodIndex >= 0 ? trainingManager.Methods[selectedMethodIndex] : null;

            for (int i = 0; i < learned.Count; i++)
            {
                InsectSkill old = trainingManager.GetSkill(learned[i]);
                if (old == null) continue;

                float cy = startY + i * (cardH + 4);
                Color oc = GetSkillColor(old.effectType);

                GUI.color = ReplaceOldBgCol;
                GUI.DrawTexture(new Rect(px + 15, cy, pw - 30, cardH), Texture2D.whiteTexture);
                GUI.color = oc;
                GUI.DrawTexture(new Rect(px + 15, cy, 6, cardH), Texture2D.whiteTexture);
                GUI.color = Color.white;

                replaceOldNameStyle.normal.textColor = oc;
                GUI.Label(new Rect(px + 38, cy + 10, pw - 260, 36), old.displayName, replaceOldNameStyle);

                string oldInfo = old.effectType == SkillEffectType.Damage ? $"DMG {old.power}" :
                                 old.effectType == SkillEffectType.BuffAttack ? $"ATK UP +{old.effectValue * 100:0}%" :
                                 $"ATK DOWN -{old.effectValue * 100:0}%";
                GUI.Label(new Rect(px + 38, cy + 46, pw - 260, 30), $"{oldInfo}  |  CD: {old.cooldownTurns}t", replaceOldInfoStyle);

                GUI.backgroundColor = ReplaceForgetBgCol;
                if (GUI.Button(new Rect(px + pw - 190, cy + cardH / 2f - 24, 140, 48), "잊기", replaceForgetBtnStyle))
                {
                    if (method != null && trainingManager.TrainSkill(method, pid, pendingNewSkillId, old.skillId))
                    {
                        feedbackMsg = $"Forgot {old.displayName}, learned {newSkill.displayName}!";
                        feedbackTimer = 2.5f;
                        pendingNewSkillId = null;
                        page = Page.SkillLearn;
                        scrollPos = Vector2.zero;
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            GUI.backgroundColor = ReplaceCancelBgCol;
            if (GUI.Button(new Rect(px + pw / 2f - 90, py + ph - 60, 180, 48), "Cancel", replaceCancelBtnStyle))
            {
                pendingNewSkillId = null;
                page = Page.SkillLearn;
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawSkillCard(Rect r, InsectSkill skill, bool learned, Color accent)
        {
            InitTrainDetailStyles();
            GUI.color = learned ? CardLearnedBgCol : CardActiveBgCol;
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            Color sc = GetSkillColor(skill.effectType);
            GUI.color = sc;
            GUI.DrawTexture(new Rect(r.x, r.y, 6, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // textColor 동적 갱신 (learned/sc).
            cardSkillNameStyle.normal.textColor = learned ? CardLearnedNameCol : sc;
            GUI.Label(new Rect(r.x + 16, r.y + 10, r.width - 160, 36), skill.displayName, cardSkillNameStyle);

            string typeStr = skill.effectType == SkillEffectType.Damage ? $"데미지: {skill.power}" :
                             skill.effectType == SkillEffectType.BuffAttack ? $"ATK UP +{skill.effectValue * 100:0}% ({skill.effectDurationTurns}t)" :
                             $"ATK DOWN -{skill.effectValue * 100:0}% ({skill.effectDurationTurns}t)";
            GUI.Label(new Rect(r.x + 16, r.y + 46, r.width - 160, 30), typeStr, cardDescStyle);

            GUI.Label(new Rect(r.x + 16, r.y + 76, r.width - 160, 28), $"쿨다운: {skill.cooldownTurns}턴", cardCdStyle);
        }

        private void DrawFeedback()
        {
            InitTrainDetailStyles();
            float alpha = Mathf.Clamp01(feedbackTimer / 0.5f);
            // alpha 동적이라 매 호출 textColor 갱신 (new Color struct stack 거짓양성).
            feedbackStyle.normal.textColor = new Color(0.3f, 1f, 0.5f, alpha);
            GUI.Label(new Rect(0, UIScale.VirtualScreenHeight * 0.15f, UIScale.VirtualScreenWidth, 30), feedbackMsg, feedbackStyle);
        }

        private PlayerInsectData GetPid()
        {
            if (collection == null || string.IsNullOrEmpty(selectedInstanceId)) return null;
            return collection.GetByInstanceId(selectedInstanceId);
        }

        private bool IsEquipped(PlayerInsectData pid, string skillId)
        {
            for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
                if (pid.GetEquippedSkill(i) == skillId) return true;
            return false;
        }

        private Color GetSkillColor(SkillEffectType type)
        {
            switch (type)
            {
                case SkillEffectType.Damage: return new Color(0.9f, 0.35f, 0.3f);
                case SkillEffectType.BuffAttack: return new Color(0.3f, 0.8f, 0.4f);
                case SkillEffectType.DebuffAttack: return new Color(0.7f, 0.4f, 0.9f);
                default: return Color.gray;
            }
        }

        public void AutoWire(TrainingManager tm, PlayerInsectCollection col, PlayerCandyInventory candy)
        {
            if (trainingManager == null) trainingManager = tm;
            // collection 변경 시 InsectUpdated 구독 동기화 (OnEnable 이후 호출 케이스 대응).
            if (collection != col)
            {
                if (collection != null)
                    collection.InsectUpdated -= HandleInsectUpdated;
                collection = col;
                if (collection != null && isActiveAndEnabled)
                {
                    collection.InsectUpdated -= HandleInsectUpdated;
                    collection.InsectUpdated += HandleInsectUpdated;
                }
                ownedCacheDirty = true;
            }
            if (candyInventory == null) candyInventory = candy;
        }

        private static string GetShortInstanceId(PlayerInsectData data)
        {
            if (data == null || string.IsNullOrEmpty(data.instanceId))
            {
                return "----";
            }

            return data.instanceId.Substring(0, Mathf.Min(6, data.instanceId.Length)).ToUpperInvariant();
        }
    }
}
