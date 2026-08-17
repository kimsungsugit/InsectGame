using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Dex;   // DexBrowseLayout — 도감이 쓰는 순수 뷰포트 컬링 계산을 공유한다
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
        private readonly UIDirectScroll directScroll = new UIDirectScroll();
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
        private static readonly Color EquipSlotNumGrayCol = new Color(0.58f, 0.62f, 0.76f);
        private static readonly Color EquipInfoGrayCol = new Color(0.72f, 0.72f, 0.76f);
        private static readonly Color EquipRemBgCol = new Color(0.4f, 0.2f, 0.2f);
        private static readonly Color EquipEmptyGrayCol = new Color(0.66f, 0.66f, 0.72f);
        private static readonly Color EquipLearnedHeaderBlueCol = new Color(0.7f, 0.75f, 1f);
        private static readonly Color EquipLearnedBgCol = new Color(0.08f, 0.1f, 0.15f, 0.8f);
        private static readonly Color EquipNameDimCol = new Color(0.66f, 0.66f, 0.7f);
        private static readonly Color EquipLearnedInfoCol = new Color(0.68f, 0.68f, 0.72f);
        private static readonly Color EquipEqBgCol = new Color(0.2f, 0.4f, 0.3f);
        private static readonly Color EquipEqTagGreenCol = new Color(0.4f, 0.7f, 0.4f);
        private static readonly Color ReplaceHeaderGrayCol = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color ReplaceOldBgCol = new Color(0.1f, 0.12f, 0.18f, 0.85f);
        private static readonly Color ReplaceOldInfoCol = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color ReplaceForgetBgCol = new Color(0.55f, 0.2f, 0.2f);
        private static readonly Color ReplaceCancelBgCol = new Color(0.3f, 0.3f, 0.35f);
        private static readonly Color CardLearnedBgCol = new Color(0.08f, 0.1f, 0.14f, 0.7f);
        private static readonly Color CardActiveBgCol = new Color(0.1f, 0.12f, 0.18f, 0.85f);
        private static readonly Color CardLearnedNameCol = new Color(0.68f, 0.68f, 0.72f);
        private static readonly Color CardDescGrayCol = new Color(0.72f, 0.72f, 0.76f);
        private static readonly Color CardCdGrayCol = new Color(0.66f, 0.66f, 0.72f);

        private void InitTrainDetailStyles()
        {
            if (trainDetailStylesReady) return;
            panelTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            panelTitleStyle.normal.textColor = PanelTitleYellowCol;
            panelCloseStyle = new GUIStyle(GUI.skin.button) { fontSize = 34, fontStyle = FontStyle.Bold };
            backBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 31, fontStyle = FontStyle.Bold };
            methodNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            methodEquipBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 32, fontStyle = FontStyle.Bold };
            methodCardNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
            methodCardDescStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, wordWrap = true };
            methodCardDescStyle.normal.textColor = MethodDescGrayCol;
            methodCostStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleRight };
            methodLockStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleRight };
            methodLockStyle.normal.textColor = MethodLockRedCol;
            methodCountStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleRight };
            methodCountStyle.normal.textColor = MethodCountGrayCol;
            methodTrainBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 32, fontStyle = FontStyle.Bold };
            learnHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            learnBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 32, fontStyle = FontStyle.Bold };
            learnedTagStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            learnedTagStyle.normal.textColor = LearnedTagGreenCol;
            equipNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            equipSlotNumStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            equipSlotNumStyle.normal.textColor = EquipSlotNumGrayCol;
            equipSlotNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 29,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            equipSlotInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = 30 };
            equipSlotInfoStyle.normal.textColor = EquipInfoGrayCol;
            equipRemBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 30 };
            equipEmptyStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Italic };
            equipEmptyStyle.normal.textColor = EquipEmptyGrayCol;
            equipLearnedHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            equipLearnedHeaderStyle.normal.textColor = EquipLearnedHeaderBlueCol;
            equipLearnedNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 29,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            equipLearnedInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = 28 };
            equipLearnedInfoStyle.normal.textColor = EquipLearnedInfoCol;
            equipEqBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 28 };
            equipEqTagStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleRight };
            equipEqTagStyle.normal.textColor = EquipEqTagGreenCol;
            replaceHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter };
            replaceHeaderStyle.normal.textColor = ReplaceHeaderGrayCol;
            replaceNewStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            replaceOldNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 29,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            replaceOldInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = 30 };
            replaceOldInfoStyle.normal.textColor = ReplaceOldInfoCol;
            replaceForgetBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 32, fontStyle = FontStyle.Bold };
            replaceCancelBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 30 };
            cardSkillNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 29,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            cardDescStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, wordWrap = true };
            cardDescStyle.normal.textColor = CardDescGrayCol;
            cardCdStyle = new GUIStyle(GUI.skin.label) { fontSize = 28 };
            cardCdStyle.normal.textColor = CardCdGrayCol;
            feedbackStyle = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            trainDetailStylesReady = true;
        }

        // GetAllOwned 매 프레임 호출 회피 — InsectUpdated 이벤트로 invalidate (CollectionUI 패턴).
        private List<PlayerInsectData> cachedOwned;
        private bool ownedCacheDirty = true;

        private void InitInsectSelectStyles()
        {
            if (insectSelectStylesReady) return;
            insectSelectSubStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter };
            insectSelectSubStyle.normal.textColor = TrainSubGrayCol;
            insectSelectNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
            insectSelectInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = 30 };
            insectSelectInfoStyle.normal.textColor = TrainInfoGrayCol;
            insectSelectBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 32 };
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

        /// <summary>
        /// 목록 한 줄의 정보 문구("Lv.n | 스킬 a/b | 장착 c/d | 크기") 캐시.
        /// 개체마다 <b>문자열 2개</b>(크기 접미 + 본문 보간)와 크기 계산이 들던 자리인데, 호출부가
        /// 목록 루프 안이라 개체 수 × OnGUI 패스마다 반복됐다(60마리면 패스당 120개).
        /// 값은 레벨·습득/장착 수·크기에서만 파생되고 그 변화는 전부 <c>InsectUpdated</c>로 오므로,
        /// 보유 목록 캐시와 <b>같은 신호로 함께</b> 비운다.
        /// </summary>
        private readonly Dictionary<string, string> ownedInfoCache = new Dictionary<string, string>();

        private string OwnedInfoLine(PlayerInsectData pid, InsectData data)
        {
            string key = pid.instanceId;
            if (string.IsNullOrEmpty(key)) return BuildOwnedInfoLine(pid, data);
            if (ownedInfoCache.TryGetValue(key, out string cached)) return cached;

            string built = BuildOwnedInfoLine(pid, data);
            ownedInfoCache[key] = built;
            return built;
        }

        private static string BuildOwnedInfoLine(PlayerInsectData pid, InsectData data)
        {
            int learned = pid.learnedSkillIds != null ? pid.learnedSkillIds.Count : 0;
            string sizeStr = data != null
                ? "  |  " + InsectSizeCalculator.SizeLabel(InsectSizeCalculator.SizeMm(data, pid))
                : string.Empty;
            return $"Lv.{pid.level}  |  스킬: {learned}/{PlayerInsectData.MaxLearnedSkills}"
                + $"  |  장착: {pid.EquippedCount()}/{PlayerInsectData.MaxEquipSlots}{sizeStr}";
        }

        private void HandleInsectUpdated(PlayerInsectData _)
        {
            ownedCacheDirty = true;
            ownedInfoCache.Clear();   // 레벨업·스킬 습득·장착 변경이 전부 이 신호로 온다
        }

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
            if (isOpen)
            {
                ChangePage(Page.InsectSelect);
                selectedInstanceId = null;
                selectedMethodIndex = -1;
            }
            else
            {
                directScroll.Reset();
            }
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }
        public void CloseModal()
        {
            isOpen = false;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }
        private void OnDisable()
        {
            isOpen = false;
            pendingNewSkillId = null;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
            if (collection != null)
                collection.InsectUpdated -= HandleInsectUpdated;
        }

        private void Update()
        {
            if (feedbackTimer > 0) feedbackTimer -= Time.deltaTime;
        }

        private void ChangePage(Page nextPage)
        {
            page = nextPage;
            scrollPos = Vector2.zero;
            directScroll.Reset();
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
            Rect panel = UISafeLayout.CenteredPanel(1000f, 1000f);
            pw = panel.width;
            ph = panel.height;
            px = panel.x;
            py = panel.y;

            UISurface.Card(new Rect(px, py, pw, ph), PanelBgCol, UITheme.Instance.surfaceBorder);
            UISurface.Rounded(new Rect(px + 3f, py + 3f, pw - 6f, 70f), PanelHeaderCol);
            GUI.color = PanelAccentOrangeCol;
            GUI.DrawTexture(new Rect(px, py + 70, pw, 5), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(px + 120, py + 8, pw - 240, 58), title, panelTitleStyle);

            if (GUI.Button(new Rect(px + pw - 80f, py + 6f, 68f, UIScale.IsMobileLayout ? 62f : 50f), "X", panelCloseStyle))
                CloseModal();
        }

        private bool DrawBackButton(float px, float py)
        {
            InitTrainDetailStyles();
            return GUI.Button(new Rect(px + 12, py + 6f, 150f, UIScale.IsMobileLayout ? 62f : 50f), "< Back", backBtnStyle);
        }

        private void DrawInsectSelect()
        {
            DrawPanel("TRAINING CENTER", out float px, out float py, out float pw, out float ph);

            if (collection == null) return;
            List<PlayerInsectData> owned = GetCachedOwned();
            if (owned == null) return;

            InitInsectSelectStyles();

            int candy = candyInventory != null ? candyInventory.Candies : 0;
            GUI.Label(new Rect(px, py + 78, pw, 44), $"훈련할 곤충을 선택하세요  |  캔디: {candy}", insectSelectSubStyle);

            float listY = py + 128;
            float listH = ph - 138;
            float itemH = UIScale.IsMobileLayout ? 150f : 130f;
            Rect area = new Rect(px + 10, listY, pw - 20, listH);
            float contentHeight = owned.Count * itemH;
            Rect view = new Rect(0, 0, area.width, contentHeight);
            directScroll.Handle(ref scrollPos, area, contentHeight, itemH * 0.35f);
            scrollPos = GUI.BeginScrollView(
                area,
                scrollPos,
                view,
                GUIStyle.none,
                GUIStyle.none);

            // 화면에 걸치는 줄만 그린다. IMGUI 스크롤뷰엔 가상화가 없어서, 컬링하지 않으면 보유 곤충
            // 전부에 대해 아래 `InsectVisual.Draw`가 3D 썸네일을 요청한다 — 캐시는 한 뷰포트 분량(24칸)이라
            // 60마리를 매 패스 훑으면 LRU가 절대 안정되지 않고 렌더러가 프레임마다 곤충 모델을
            // 만들었다 부순다(도감에서 P0였던 것과 같은 구조, 2026-08-06 audit).
            DexBrowseLayout.GetVisibleRowRange(
                scrollPos.y, area.height, itemH - 3f, 3f, owned.Count,
                out int firstVisible, out int lastVisible);

            for (int i = firstVisible; i <= lastVisible; i++)
            {
                PlayerInsectData pid = owned[i];
                InsectData data = collection.GetInsectData(pid.insectId);
                Rect r = new Rect(0, i * itemH, view.width, itemH - 3);

                Color rc = data != null ? UITheme.Instance.GetInsectRarityColor(data.rarity) : Color.gray;
                UISurface.Card(r, TrainItemBgCol, UITheme.Instance.surfaceBorder);
                GUI.color = rc;
                GUI.DrawTexture(new Rect(r.x, r.y, 6, r.height), Texture2D.whiteTexture);

                if (data != null)
                    InsectVisual.Draw(r.x + 70, r.y + r.height / 2f, 96f, data, pid != null && pid.isShiny, 1f);

                // 캐시 스타일 + textColor만 동적 갱신 (BattleScreenUI 패턴).
                insectSelectNameStyle.normal.textColor = rc;
                GUI.color = Color.white;
                // #코드 미표시 — 개체 구분은 아래 줄의 레벨·스킬 수·크기가 맡는다.
                string name = data != null ? data.displayName : pid.insectId;
                GUI.Label(new Rect(r.x + 120, r.y + 14, r.width - 290, 44), name, insectSelectNameStyle);

                GUI.Label(new Rect(r.x + 120, r.y + 66, r.width - 290, 38),
                    OwnedInfoLine(pid, data), insectSelectInfoStyle);

                GUI.backgroundColor = TrainBtnGreenCol;
                float trainH = UIScale.IsMobileLayout ? 64f : 52f;
                if (GUI.Button(new Rect(r.x + r.width - 160, r.y + r.height / 2f - trainH * 0.5f, 140, trainH), "훈련", insectSelectBtnStyle))
                {
                    selectedInstanceId = pid.instanceId;
                    ChangePage(Page.MethodSelect);
                }
                GUI.backgroundColor = Color.white;
            }
            GUI.EndScrollView();
        }

        private void DrawMethodSelect()
        {
            DrawPanel("CHOOSE TRAINING", out float px, out float py, out float pw, out float ph);
            if (DrawBackButton(px, py)) { ChangePage(Page.InsectSelect); return; }

            PlayerInsectData pid = GetPid();
            InsectData data = pid != null ? collection.GetInsectData(pid.insectId) : null;
            if (pid == null) { ChangePage(Page.InsectSelect); return; }

            Color rc = data != null ? UITheme.Instance.GetInsectRarityColor(data.rarity) : Color.gray;
            // 캐시 + textColor 동적 갱신.
            methodNameStyle.normal.textColor = rc;
            string typeLabel = data != null
                ? InsectTypeChart.GetDisplayName(data.primaryType)
                    + (data.secondaryType != InsectElement.None ? "/" + InsectTypeChart.GetDisplayName(data.secondaryType) : "")
                : "타입 미상";
            UIHelper.LabelFit(new Rect(px, py + 80, pw, 44),
                data != null ? $"{data.displayName} Lv.{pid.level}  ·  {typeLabel} 타입" : pid.insectId,
                methodNameStyle);

            GUI.backgroundColor = MethodEquipBtnCol;
            if (GUI.Button(new Rect(px + pw - 240f, py + 76f, 220f, UIScale.IsMobileLayout ? 64f : 50f), "스킬 장착", methodEquipBtnStyle))
                ChangePage(Page.SkillEquip);
            GUI.backgroundColor = Color.white;

            if (trainingManager == null || trainingManager.Methods == null) return;

            TrainingMethod[] methods = trainingManager.Methods;
            float cardH = 172f;
            // 6개 방식이 세로/가로 캔버스를 넘지 않도록 스크롤 컨테이너로 감싼다.
            Rect area = new Rect(px + 10, py + 138, pw - 20, ph - 150);
            float contentHeight = methods.Length * (cardH + 8);
            Rect view = new Rect(0, 0, area.width, contentHeight);
            directScroll.Handle(ref scrollPos, area, contentHeight, cardH * 0.35f);
            scrollPos = GUI.BeginScrollView(
                area,
                scrollPos,
                view,
                GUIStyle.none,
                GUIStyle.none);
            float cardW = view.width;

            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                float cy = i * (cardH + 8);
                bool canTrain = trainingManager.CanTrain(m, pid);
                bool levelOk = pid.level >= m.requiredLevel;

                // themeColor scaled new Color는 struct stack 거짓양성 (BattleArenaController 판단 일관).
                GUI.color = new Color(m.themeColor.r * 0.15f, m.themeColor.g * 0.15f, m.themeColor.b * 0.15f, 0.8f);
                GUI.DrawTexture(new Rect(0, cy, cardW, cardH), Texture2D.whiteTexture);
                GUI.color = levelOk ? m.themeColor : MethodCardDarkCol;
                GUI.DrawTexture(new Rect(0, cy, 6, cardH), Texture2D.whiteTexture);

                methodCardNameStyle.normal.textColor = levelOk ? m.themeColor : MethodNameDimCol;
                GUI.color = Color.white;
                UIHelper.LabelFit(new Rect(24, cy + 16, cardW - 380, 44), m.displayName, methodCardNameStyle);

                GUI.Label(new Rect(24, cy + 70, cardW - 380, 90), m.description, methodCardDescStyle);

                methodCostStyle.normal.textColor = canTrain ? MethodCostOkCol : MethodCostBadCol;
                GUI.Label(new Rect(cardW - 340, cy + 16, 175, 38), $"비용: {m.candyCost}", methodCostStyle);

                if (!levelOk)
                {
                    GUI.Label(new Rect(cardW - 340, cy + 62, 175, 38), $"Lv.{m.requiredLevel} 필요", methodLockStyle);
                }

                int skillCount = trainingManager.GetAvailableSkillCount(m, pid);
                UIHelper.LabelFit(new Rect(cardW - 340, cy + cardH - 44, 175, 34), $"스킬 {skillCount}개", methodCountStyle);

                GUI.backgroundColor = canTrain ? MethodTrainOkBgCol : MethodTrainOffBgCol;
                GUI.enabled = canTrain;
                float startButtonH = UIScale.IsMobileLayout ? 64f : 52f;
                if (GUI.Button(new Rect(cardW - 150, cy + cardH / 2f - startButtonH * 0.5f, 130, startButtonH), "시작", methodTrainBtnStyle))
                {
                    selectedMethodIndex = i;
                    ChangePage(Page.SkillLearn);
                }
                GUI.enabled = true;
                GUI.backgroundColor = Color.white;
            }
            GUI.EndScrollView();
        }

        private void DrawSkillLearn()
        {
            DrawPanel("LEARN SKILLS", out float px, out float py, out float pw, out float ph);
            if (DrawBackButton(px, py)) { ChangePage(Page.MethodSelect); return; }

            PlayerInsectData pid = GetPid();
            if (pid == null || trainingManager == null || selectedMethodIndex < 0)
            {
                ChangePage(Page.MethodSelect);
                return;
            }

            TrainingMethod method = trainingManager.Methods[selectedMethodIndex];
            InsectSkill[] skills = trainingManager.GetAvailableSkills(method, pid);

            learnHeaderStyle.normal.textColor = method.themeColor;
            UIHelper.LabelFit(new Rect(px, py + 80, pw, 42), $"{method.displayName}  |  기술별 캔디 비용", learnHeaderStyle);

            float listY = py + 128;
            float listH = ph - 138;
            float itemH = UIScale.IsMobileLayout ? 176f : 164f;
            Rect area = new Rect(px + 10, listY, pw - 20, listH);
            float contentHeight = skills.Length * itemH;
            Rect view = new Rect(0, 0, area.width, contentHeight);
            directScroll.Handle(ref scrollPos, area, contentHeight, itemH * 0.35f);
            scrollPos = GUI.BeginScrollView(
                area,
                scrollPos,
                view,
                GUIStyle.none,
                GUIStyle.none);

            for (int i = 0; i < skills.Length; i++)
            {
                InsectSkill skill = skills[i];
                if (skill == null) continue;
                Rect r = new Rect(0, i * itemH, view.width, itemH - 4);
                bool learned = pid.HasLearnedSkill(skill.skillId);

                DrawSkillCard(r, skill, learned, method.themeColor);

                if (!learned)
                {
                    int trainingCost = trainingManager.GetTrainingCost(method, pid, skill.skillId);
                    bool canAfford = trainingManager.CanTrain(method, pid, skill.skillId);
                    bool isFull = pid.IsSkillsFull();
                    string btnLabel = isFull ? $"교체 {trainingCost}" : $"습득 {trainingCost}";
                    GUI.backgroundColor = canAfford ? (isFull ? LearnBtnFullCol : LearnBtnOkCol) : LearnBtnOffCol;
                    GUI.enabled = canAfford;
                    float learnButtonH = UIScale.IsMobileLayout ? 64f : 52f;
                    if (GUI.Button(new Rect(r.x + r.width - 160, r.y + r.height / 2f - learnButtonH * 0.5f, 140, learnButtonH), btnLabel, learnBtnStyle))
                    {
                        if (isFull)
                        {
                            pendingNewSkillId = skill.skillId;
                            ChangePage(Page.SkillReplace);
                        }
                        else if (trainingManager.TrainSkill(method, pid, skill.skillId))
                        {
                            feedbackMsg = $"{skill.displayName} 습득 완료!";
                            feedbackTimer = 2f;
                        }
                    }
                    GUI.enabled = true;
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.Label(new Rect(r.x + r.width - 180, r.y + r.height / 2f - 28, 160, 56),
                        "습득완료", learnedTagStyle);
                }
            }
            GUI.EndScrollView();
        }

        private void DrawSkillEquip()
        {
            DrawPanel("EQUIP SKILLS", out float px, out float py, out float pw, out float ph);
            if (DrawBackButton(px, py)) { ChangePage(Page.MethodSelect); return; }

            PlayerInsectData pid = GetPid();
            if (pid == null) { ChangePage(Page.InsectSelect); return; }

            InsectData data = collection.GetInsectData(pid.insectId);
            Color rc = data != null ? UITheme.Instance.GetInsectRarityColor(data.rarity) : Color.gray;

            equipNameStyle.normal.textColor = rc;
            UIHelper.LabelFit(new Rect(px, py + 80, pw, 42),
                $"{(data != null ? data.displayName : pid.insectId)} - Skill Slots", equipNameStyle);

            float slotY = py + 128;
            float slotH = UIScale.IsMobileLayout ? 126f : 112f;

            for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
            {
                float sy = slotY + i * (slotH + 4);
                string eqId = pid.GetEquippedSkill(i);
                InsectSkill eqSkill = eqId != null ? trainingManager.GetSkill(eqId) : null;

                UISurface.Card(new Rect(px + 15, sy, pw - 30, slotH), EquipSlotBgCol, UITheme.Instance.surfaceBorder);

                GUI.color = Color.white;
                GUI.Label(new Rect(px + 15, sy, 60, slotH), $"{i + 1}", equipSlotNumStyle);

                if (eqSkill != null)
                {
                    Color sc = GetSkillColor(eqSkill.effectType);
                    GUI.color = sc;
                    GUI.DrawTexture(new Rect(px + 15, sy, 5, slotH), Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    equipSlotNameStyle.normal.textColor = SkillUILayout.GetReadableAccent(sc);
                    GUI.Label(new Rect(px + 90, sy + 8, pw - 320, 52),
                        eqSkill.displayName, equipSlotNameStyle);

                    string typeStr = eqSkill.effectType == SkillEffectType.Damage ? $"DMG {eqSkill.power}" :
                                     eqSkill.effectType == SkillEffectType.BuffAttack ? $"ATK UP +{eqSkill.effectValue * 100:0}%" :
                                     $"ATK DOWN -{eqSkill.effectValue * 100:0}%";
                    GUI.Label(new Rect(px + 90, sy + 64, pw - 320, 40),
                        $"{typeStr}  |  CD: {eqSkill.cooldownTurns}t", equipSlotInfoStyle);

                    GUI.backgroundColor = EquipRemBgCol;
                    float removeButtonH = UIScale.IsMobileLayout ? 62f : 48f;
                    if (GUI.Button(new Rect(px + pw - 170, sy + (slotH - removeButtonH) * 0.5f, 130, removeButtonH), "해제", equipRemBtnStyle))
                    {
                        pid.EquipSkill(null, i);
                        collection.ForceSave();
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.Label(new Rect(px + 90, sy + slotH / 2f - 20, pw - 200, 40), "빈 슬롯", equipEmptyStyle);
                }
            }

            float learnedY = slotY + PlayerInsectData.MaxEquipSlots * (slotH + 4) + 12;
            GUI.Label(new Rect(px + 15, learnedY, pw - 30, 42), "Learned Skills:", equipLearnedHeaderStyle);

            float listY2 = learnedY + 50;
            float listH2 = ph - (listY2 - py) - 10;
            float itemH2 = UIScale.IsMobileLayout ? 108f : 96f;

            List<string> learned = pid.learnedSkillIds ?? new List<string>();
            Rect area = new Rect(px + 15, listY2, pw - 30, listH2);
            float contentHeight = learned.Count * itemH2;
            Rect viewR = new Rect(0, 0, area.width, contentHeight);
            directScroll.Handle(ref scrollPos, area, contentHeight, itemH2 * 0.35f);
            scrollPos = GUI.BeginScrollView(
                area,
                scrollPos,
                viewR,
                GUIStyle.none,
                GUIStyle.none);

            for (int i = 0; i < learned.Count; i++)
            {
                InsectSkill sk = trainingManager.GetSkill(learned[i]);
                if (sk == null) continue;

                Rect r = new Rect(0, i * itemH2, viewR.width, itemH2 - 3);
                bool isEquipped = IsEquipped(pid, sk.skillId);

                UISurface.Card(r, EquipLearnedBgCol, UITheme.Instance.surfaceBorder);

                Color sc2 = GetSkillColor(sk.effectType);
                GUI.color = sc2;
                GUI.DrawTexture(new Rect(r.x, r.y, 5, r.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                equipLearnedNameStyle.normal.textColor = isEquipped
                    ? EquipNameDimCol
                    : SkillUILayout.GetReadableAccent(sc2);
                GUI.Label(new Rect(r.x + 16, r.y + 6, r.width - 210, 48),
                    sk.displayName, equipLearnedNameStyle);

                GUI.Label(new Rect(r.x + 16, r.y + 56, r.width - 210, 36),
                    sk.effectType == SkillEffectType.Damage ? $"DMG {sk.power}" : sk.effectType.ToString(), equipLearnedInfoStyle);

                if (!isEquipped && pid.EquippedCount() < PlayerInsectData.MaxEquipSlots)
                {
                    GUI.backgroundColor = EquipEqBgCol;
                    float equipButtonH = UIScale.IsMobileLayout ? 62f : 48f;
                    if (GUI.Button(new Rect(r.x + r.width - 134f, r.y + r.height / 2f - equipButtonH * 0.5f, 118f, equipButtonH), "Equip", equipEqBtnStyle))
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
                    GUI.Label(new Rect(r.x + r.width - 160, r.y + r.height / 2f - 18, 150, 36), "장착중", equipEqTagStyle);
                }
            }
            GUI.EndScrollView();
        }

        private void DrawSkillReplace()
        {
            DrawPanel("REPLACE SKILL", out float px, out float py, out float pw, out float ph);
            if (DrawBackButton(px, py))
            {
                ChangePage(Page.SkillLearn);
                pendingNewSkillId = null;
                return;
            }

            PlayerInsectData pid = GetPid();
            if (pid == null || trainingManager == null || string.IsNullOrEmpty(pendingNewSkillId))
            {
                ChangePage(Page.SkillLearn);
                return;
            }

            InsectSkill newSkill = trainingManager.GetSkill(pendingNewSkillId);
            if (newSkill == null) { ChangePage(Page.SkillLearn); return; }

            TrainingMethod method = selectedMethodIndex >= 0 ? trainingManager.Methods[selectedMethodIndex] : null;
            int replaceCost = trainingManager.GetTrainingCost(method, pid, pendingNewSkillId);

            GUI.Label(new Rect(px, py + 80, pw, 44),
                $"기술이 가득 찼습니다 ({PlayerInsectData.MaxLearnedSkills}/{PlayerInsectData.MaxLearnedSkills}) · 교체 비용 {replaceCost} 캔디",
                replaceHeaderStyle);

            Color nc = GetSkillColor(newSkill.effectType);
            replaceNewStyle.normal.textColor = SkillUILayout.GetReadableAccent(nc);
            string newInfo = newSkill.effectType == SkillEffectType.Damage ? $"DMG {newSkill.power}" :
                             newSkill.effectType == SkillEffectType.BuffAttack ? $"ATK UP +{newSkill.effectValue * 100:0}%" :
                             $"ATK DOWN -{newSkill.effectValue * 100:0}%";
            GUI.Label(new Rect(px + 24f, py + 124, pw - 48f, 58),
                $"New: {newSkill.displayName}  ({newInfo})", replaceNewStyle);

            GUI.color = nc;
            GUI.DrawTexture(new Rect(px + 100, py + 188, pw - 200, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float cardH = 140f;
            System.Collections.Generic.List<string> learned = pid.learnedSkillIds ?? new System.Collections.Generic.List<string>();
            float cancelY = py + ph - 72f;
            Rect listArea = new Rect(
                px + 15f,
                py + 204f,
                pw - 30f,
                Mathf.Max(80f, cancelY - (py + 204f) - 12f));
            float contentHeight = GetSkillReplacementContentHeight(learned.Count);
            Rect viewRect = new Rect(0f, 0f, listArea.width, contentHeight);
            directScroll.Handle(ref scrollPos, listArea, contentHeight, cardH * 0.35f);
            scrollPos = GUI.BeginScrollView(
                listArea,
                scrollPos,
                viewRect,
                GUIStyle.none,
                GUIStyle.none);

            for (int i = 0; i < learned.Count; i++)
            {
                InsectSkill old = trainingManager.GetSkill(learned[i]);
                if (old == null) continue;

                float cy = i * (cardH + 4f);
                Color oc = GetSkillColor(old.effectType);

                UISurface.Card(new Rect(0f, cy, viewRect.width, cardH), ReplaceOldBgCol, UITheme.Instance.surfaceBorder);
                GUI.color = oc;
                GUI.DrawTexture(new Rect(0f, cy, 6f, cardH), Texture2D.whiteTexture);
                GUI.color = Color.white;

                replaceOldNameStyle.normal.textColor = SkillUILayout.GetReadableAccent(oc);
                GUI.Label(new Rect(25f, cy + 8f, viewRect.width - 270f, 52f),
                    old.displayName, replaceOldNameStyle);

                string oldInfo = old.effectType == SkillEffectType.Damage ? $"DMG {old.power}" :
                                 old.effectType == SkillEffectType.BuffAttack ? $"ATK UP +{old.effectValue * 100:0}%" :
                                 $"ATK DOWN -{old.effectValue * 100:0}%";
                GUI.Label(new Rect(25f, cy + 66f, viewRect.width - 270f, 40f),
                    $"{oldInfo}  |  CD: {old.cooldownTurns}t", replaceOldInfoStyle);

                GUI.backgroundColor = ReplaceForgetBgCol;
                if (GUI.Button(
                    new Rect(viewRect.width - 195f, cy + cardH / 2f - 28f, 160f, 56f),
                    "잊기",
                    replaceForgetBtnStyle))
                {
                    if (method != null && trainingManager.TrainSkill(method, pid, pendingNewSkillId, old.skillId))
                    {
                        feedbackMsg = $"{old.displayName}을 잊고 {newSkill.displayName}을 습득했습니다!";
                        feedbackTimer = 2.5f;
                        pendingNewSkillId = null;
                        ChangePage(Page.SkillLearn);
                    }
                }
                GUI.backgroundColor = Color.white;
            }
            GUI.EndScrollView();

            GUI.backgroundColor = ReplaceCancelBgCol;
            if (GUI.Button(new Rect(px + pw / 2f - 100, cancelY, 200, 56), "Cancel", replaceCancelBtnStyle))
            {
                pendingNewSkillId = null;
                ChangePage(Page.SkillLearn);
            }
            GUI.backgroundColor = Color.white;
        }

        internal static float GetSkillReplacementContentHeight(int learnedSkillCount)
        {
            return Mathf.Max(0, learnedSkillCount) * 144f;
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
            cardSkillNameStyle.normal.textColor = learned
                ? CardLearnedNameCol
                : SkillUILayout.GetReadableAccent(sc);
            GUI.Label(new Rect(r.x + 18, r.y + 6, r.width - 200, 58),
                skill.displayName, cardSkillNameStyle);

            string elementName = InsectTypeChart.GetDisplayName(skill.element);
            string typeStr = skill.effectType == SkillEffectType.Damage ? $"{elementName} 타입 · 데미지: {skill.power}" :
                             skill.effectType == SkillEffectType.BuffAttack ? $"{elementName} 타입 · ATK UP +{skill.effectValue * 100:0}% ({skill.effectDurationTurns}t)" :
                             $"{elementName} 타입 · ATK DOWN -{skill.effectValue * 100:0}% ({skill.effectDurationTurns}t)";
            GUI.Label(new Rect(r.x + 18, r.y + 66, r.width - 200, 50),
                typeStr, cardDescStyle);

            UIHelper.LabelFit(new Rect(r.x + 18, r.y + 120, r.width - 200, 34),
                $"쿨다운: {skill.cooldownTurns}턴", cardCdStyle);
        }

        private void DrawFeedback()
        {
            InitTrainDetailStyles();
            float alpha = Mathf.Clamp01(feedbackTimer / 0.5f);
            // alpha 동적이라 매 호출 textColor 갱신 (new Color struct stack 거짓양성).
            feedbackStyle.normal.textColor = new Color(0.3f, 1f, 0.5f, alpha);
            GUI.Label(new Rect(0, UIScale.VirtualScreenHeight * 0.15f, UIScale.VirtualScreenWidth, 60), feedbackMsg, feedbackStyle);
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

    }
}
