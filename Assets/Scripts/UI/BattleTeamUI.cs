using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Dex;   // DexBrowseLayout — 목록 뷰포트 컬링 계산 공유
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
        private readonly UIDirectScroll directScroll = new UIDirectScroll();

        [SerializeField] private HospitalUI hospitalUi;

        // ── 피커 정렬 ──
        // 정렬은 패스마다 하지 않는다 — 비교자 델리게이트와 리스트 순회가 매 OnGUI 패스에 든다.
        // 피커를 열 때와 기준을 바꿀 때만 다시 굽고, 그 사이에는 이 버퍼를 그대로 읽는다.
        private InsectSortMode pickerSort = InsectSortMode.Rarity;
        private readonly List<PlayerInsectData> pickerSorted = new List<PlayerInsectData>();
        private bool pickerSortDirty = true;
        // 정렬 칩 Rect를 보관하던 `Rect[4]` 필드가 있었다 — 대입만 하고 아무도 읽지 않는 죽은 필드였고,
        // 길이 4가 `InsectBrowseSort.Order`와 코드로 묶여 있지 않아 정렬 모드를 하나 더 늘리면
        // OnGUI에서 IndexOutOfRange가 날 자리였다. 히트 테스트는 `GUI.Button`이 직접 하므로 필요 없다.
        private int pickerSortedCount = -1;

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
        private static readonly Color SubCol = new Color(0.78f, 0.78f, 0.82f);
        private static readonly Color SlotBg = new Color(0.1f, 0.12f, 0.18f, 0.85f);
        private static readonly Color NumCol = new Color(0.58f, 0.62f, 0.76f);
        private static readonly Color InfoCol = new Color(0.76f, 0.76f, 0.8f);
        private static readonly Color RemoveBg = new Color(0.5f, 0.2f, 0.2f);
        private static readonly Color ChangeBg = new Color(0.25f, 0.35f, 0.55f);
        private static readonly Color HealBg = new Color(0.22f, 0.55f, 0.38f);
        private static readonly Color HurtCol = new Color(1f, 0.5f, 0.42f);
        private static readonly Color EmptyBoxCol = new Color(0.25f, 0.25f, 0.3f, 0.5f);
        private static readonly Color EmptyCol = new Color(0.68f, 0.68f, 0.74f);
        private static readonly Color HintCol = new Color(0.66f, 0.66f, 0.72f);
        private static readonly Color AddBg = new Color(0.2f, 0.4f, 0.3f);
        private static readonly Color PickerHeaderBg = new Color(0.15f, 0.2f, 0.3f);
        private static readonly Color PickerItemActiveBg = new Color(0.1f, 0.12f, 0.18f, 0.85f);
        private static readonly Color PickerItemDisabledBg = new Color(0.08f, 0.08f, 0.12f, 0.6f);
        private static readonly Color PickerInfoCol = new Color(0.72f, 0.72f, 0.76f);
        private static readonly Color TagCol = new Color(0.86f, 0.84f, 0.48f);
        private static readonly Color BtnBg = new Color(0.2f, 0.45f, 0.3f);
        private static readonly Color DimNameCol = new Color(0.64f, 0.64f, 0.68f);

        private void InitTeamStyles()
        {
            if (teamStylesInit) return;
            teamStylesInit = true;

            teamTitleCache = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            teamTitleCache.normal.textColor = TitleCol;
            teamCloseCache = new GUIStyle(GUI.skin.button) { fontSize = 36, fontStyle = FontStyle.Bold };
            teamSubCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            teamSubCache.normal.textColor = SubCol;

            slotNumCache = new GUIStyle(GUI.skin.label) { fontSize = 47, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            slotNumCache.normal.textColor = NumCol;
            slotNameCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
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
            pickerNameCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 31,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
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
            listScroll = Vector2.zero;
            directScroll.Reset();
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }
        public void CloseModal()
        {
            isOpen = false;
            selectingSlot = -1;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }
        private void OnDisable()
        {
            isOpen = false;
            selectingSlot = -1;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }

        // 빈 Update()가 있었다 — 본문이 없어도 Unity는 매 프레임 managed→native 호출을 한다.
        // CollectionUI 재감사(2026-08-07)가 같은 것을 지웠다. 되살리지 말 것.

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
            Rect panel = UISafeLayout.CenteredPanel(960f, 940f);
            float panelW = panel.width;
            float panelH = panel.height;
            float px = panel.x;
            float py = panel.y;

            UISurface.Card(new Rect(px, py, panelW, panelH), PanelBg, UITheme.Instance.surfaceBorder);
            UISurface.Rounded(new Rect(px + 3f, py + 3f, panelW - 6f, 84f), HeaderBg);
            GUI.color = HeaderLine;
            GUI.DrawTexture(new Rect(px + 3f, py + 84, panelW - 6f, 5), Texture2D.whiteTexture);

            GUI.color = Color.white;
            // 화면 전체가 한국어인데 이 세 곳(제목·피커 제목·뒤로)만 영어로 남아 있었다.
            // 퀵바·HUD가 이 화면을 "배틀팀"으로 부르므로 같은 말을 쓴다.
            GUI.Label(new Rect(px, py + 14, panelW - 72, 58), "배틀팀", teamTitleCache);

            float closeH = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 52f, 64f);
            if (GUI.Button(new Rect(px + panelW - 80f, py + 10f, 70f, closeH), "X", teamCloseCache))
            {
                CloseModal();
            }

            // 부상 안내 + 회복 진입 — 팀을 짜는 자리에서 바로 상태를 알고 고칠 수 있어야 한다.
            // 치료 자체는 병원이 한다(재화·결제·환불 로직을 여기서 복제하면 곧 어긋난다).
            int injured = InjuredTeamCount();
            GUI.Label(new Rect(px + 24f, py + 92, panelW - (injured > 0 ? 272f : 48f), 52),
                injured > 0
                    ? $"부상 {injured}마리 — 회복하고 나가세요"
                    : $"배틀용 곤충을 최대 {BattleTeamManager.MaxSlots}마리 선택하세요",
                teamSubCache);

            if (injured > 0 && hospitalUi != null)
            {
                float healH = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 48f, 56f);
                GUI.backgroundColor = HealBg;
                // 폭 224 — "회복하러 가기"는 31px 글자 6자라 안쪽 여백을 빼면 190px쯤 든다.
                // 172로 잡았더니 마지막 글자가 잘렸다(wordWrap이 없어 가로로 잘린다).
                if (GUI.Button(new Rect(px + panelW - 248f, py + 94f, 224f, healH), "회복하러 가기", slotRemoveCache))
                {
                    // 배틀팀을 닫고 병원을 연다 — 모달 둘이 겹치면 뒤쪽 버튼이 클릭을 가로챈다.
                    CloseModal();
                    // Toggle은 말 그대로 토글이라, 병원이 이미 열려 있으면 이 버튼이 그걸 닫아
                    // 두 창이 다 사라진다. 지금은 퀵바의 IsAnyOpen 게이트 덕에 도달하지 않지만
                    // 게이트 하나에 기대는 대신 여기서 "열기"로 못박는다.
                    if (!hospitalUi.IsOpen) hospitalUi.Toggle();
                    GUI.backgroundColor = Color.white;
                    return;
                }
                GUI.backgroundColor = Color.white;
            }

            float slotY = py + 150;
            const float slotGap = 6f;
            // 패널이 안전 영역에 맞춰 줄면 슬롯 높이도 함께 줄여 마지막 슬롯이 잘리지 않게 한다.
            float slotAvail = panelH - (slotY - py) - 12f;
            float slotH = Mathf.Clamp(
                (slotAvail - (BattleTeamManager.MaxSlots - 1) * slotGap) / BattleTeamManager.MaxSlots,
                UIScale.MinTouchHeight,
                UIScale.IsMobileLayout ? 148f : 128f);

            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                DrawSlot(px + 18, slotY + i * (slotH + slotGap), panelW - 36, slotH, i);
            }
        }

        private void DrawSlot(float x, float y, float w, float h, int index)
        {
            string instanceId = teamManager != null ? teamManager.GetSlot(index) : null;
            bool hasInsect = !string.IsNullOrEmpty(instanceId);

            UISurface.Card(new Rect(x, y, w, h), SlotBg, UITheme.Instance.surfaceBorder);

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, 64, h), $"{index + 1}", slotNumCache);

            if (hasInsect)
            {
                PlayerInsectData pid = collection != null ? collection.GetByInstanceId(instanceId) : null;
                InsectData data = pid != null && collection != null ? collection.GetInsectData(pid.insectId) : null;

                if (data != null)
                {
                    Color rarityCol = UITheme.Instance.GetInsectRarityColor(data.rarity);
                    // 6px 레일 — 각진 채로 두고(ui-layout.md) 세로를 카드 반경만큼 물려
                    // 둥근 모서리를 뚫지 않게 한다.
                    UISurface.Flat(
                        new Rect(x + 3f, y + 3f + UITheme.Radius.Card, 6f,
                            Mathf.Max(4f, h - 6f - UITheme.Radius.Card * 2f)),
                        rarityCol);

                    InsectVisual.Draw(x + 90, y + h / 2f, 96f, data, pid != null && pid.isShiny, 1f);

                    slotNameCache.normal.textColor = rarityCol;
                    GUI.color = Color.white;
                    // 46px는 이 폰트로 한 줄이라, 긴 이름이 줄바꿈되면 둘째 줄이 잘렸다.
                    UIHelper.LabelFit(
                        new Rect(x + 150, y + 16, w - 330, 46), GetOwnedDisplayName(pid, data), slotNameCache);

                    int lv = pid != null ? pid.level : 1;
                    int cp = PlayerInsectCombatPower.Calculate(data, pid);
                    // 부상이면 그 자리에서 알린다 — 헤더의 "회복하러 가기"만으로는 **누가** 다쳤는지 모른다.
                    bool hurt = NeedsHeal(pid);
                    slotInfoCache.normal.textColor = hurt ? HurtCol : InfoCol;
                    GUI.Label(new Rect(x + 150, y + 64, w - 330, 42),
                        hurt
                            ? $"Lv.{lv}  |  CP {cp}  |  {HurtLabel(pid, data)}"
                            : $"Lv.{lv}  |  {data.rarity}  |  CP {cp}",
                        slotInfoCache);
                    slotInfoCache.normal.textColor = InfoCol;
                }

                GUI.backgroundColor = RemoveBg;
                float actionH = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 50f, 60f);
                if (GUI.Button(new Rect(x + w - 168, y + 10f, 150, actionH), "제거", slotRemoveCache))
                    teamManager?.RemoveSlot(index);

                GUI.backgroundColor = ChangeBg;
                if (GUI.Button(new Rect(x + w - 168, y + h - actionH - 10f, 150, actionH), "변경", slotRemoveCache))
                {
                    selectingSlot = index;
                    listScroll = Vector2.zero;
                    directScroll.Reset();
                    pickerSortDirty = true;   // 열 때마다 최신 보유 목록으로 다시 정렬
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.color = EmptyBoxCol;
                GUI.DrawTexture(new Rect(x + 64, y + h / 2f - 32, 64, 64), Texture2D.whiteTexture);

                GUI.color = Color.white;
                UIHelper.LabelFit(new Rect(x + 64, y + h / 2f - 32, 64, 64), "+", slotEmptyCache);

                GUI.backgroundColor = AddBg;
                float selectH = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 52f, 64f);
                if (GUI.Button(new Rect(x + w - 170, y + h / 2f - selectH * 0.5f, 150, selectH), "선택", slotAddCache))
                {
                    selectingSlot = index;
                    listScroll = Vector2.zero;
                    directScroll.Reset();
                    pickerSortDirty = true;   // 열 때마다 최신 보유 목록으로 다시 정렬
                }
                GUI.backgroundColor = Color.white;

                GUI.Label(new Rect(x + 150, y + h / 2f - 21, w - 330, 42), "빈 슬롯", slotHintCache);
            }
        }

        private void DrawInsectPicker()
        {
            Rect panel = UISafeLayout.CenteredPanel(960f, 940f);
            float panelW = panel.width;
            float panelH = panel.height;
            float px = panel.x;
            float py = panel.y;

            UISurface.Card(new Rect(px, py, panelW, panelH), PanelBg, UITheme.Instance.surfaceBorder);
            UISurface.Rounded(new Rect(px + 3f, py + 3f, panelW - 6f, 84f), PickerHeaderBg);

            GUI.color = Color.white;
            GUI.Label(new Rect(px + 130, py + 14, panelW - 260, 58),
                $"{selectingSlot + 1}번 슬롯에 넣을 곤충 선택", pickerTitleCache);

            float backH = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 52f, 64f);
            // 화살표는 ASCII '<' 그대로 둔다 — 이 라벨에서 이미 렌더되던 글자라 폰트 아틀라스가 확실하다.
            if (GUI.Button(new Rect(px + 14, py + 10f, 150f, backH), "< 뒤로", pickerBackCache))
            {
                selectingSlot = -1;
                directScroll.Reset();
            }

            if (collection == null) return;

            // ── 정렬 칩 ──
            // 팀에 넣을 곤충을 고를 때 필요한 건 "무엇이 강한가"다. 기본은 등급 → CP 순이고,
            // 레벨·전투력·최근 획득으로 바꿀 수 있다. 순서 규칙은 InsectBrowseSort가 단일 출처이며
            // 보유 곤충 화면도 같은 것을 쓴다(두 화면이 다르게 정렬하면 방금 본 개체를 다시 찾아야 한다).
            float chipY = py + 92f;
            float chipH = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 44f, 52f);
            float chipW = (panelW - 40f - 18f) / InsectBrowseSort.Order.Length;
            for (int i = 0; i < InsectBrowseSort.Order.Length; i++)
            {
                InsectSortMode mode = InsectBrowseSort.Order[i];
                Rect chip = new Rect(px + 20f + i * (chipW + 6f), chipY, chipW, chipH);
                GUI.backgroundColor = pickerSort == mode ? ChangeBg : SlotBg;
                if (GUI.Button(chip, InsectBrowseSort.Label(mode), pickerBtnCache) && pickerSort != mode)
                {
                    pickerSort = mode;
                    pickerSortDirty = true;
                    listScroll = Vector2.zero;
                    directScroll.Reset();
                }
            }
            GUI.backgroundColor = Color.white;

            EnsurePickerSorted();
            List<PlayerInsectData> owned = pickerSorted;
            float listY = chipY + chipH + 8f;
            float listH = panelH - (listY - py) - 10f;
            float itemH = UIScale.IsMobileLayout ? 132f : 116f;
            float totalH = owned.Count * itemH;
            Rect listArea = new Rect(px + 10, listY, panelW - 20, listH);
            Rect viewRect = new Rect(0, 0, listArea.width, totalH);

            directScroll.Handle(ref listScroll, listArea, totalH, itemH * 0.35f);
            listScroll = GUI.BeginScrollView(
                listArea,
                listScroll,
                viewRect,
                GUIStyle.none,
                GUIStyle.none);
            // 화면에 걸치는 줄만 그린다 — 아래 DrawPickerItem이 개체마다 3D 썸네일을 요청하는데
            // 캐시가 한 뷰포트 분량이라, 전 개체를 매 패스 훑으면 LRU가 안정되지 않아 렌더러가
            // 프레임마다 곤충 모델을 만들었다 부순다(2026-08-06 audit, 도감·훈련과 같은 결함).
            DexBrowseLayout.GetVisibleRowRange(
                listScroll.y, listArea.height, itemH - 3f, 3f, owned.Count,
                out int firstVisible, out int lastVisible);

            for (int i = firstVisible; i <= lastVisible; i++)
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
            UISurface.Card(
                rect,
                alreadyInTeam ? PickerItemDisabledBg : PickerItemActiveBg,
                UITheme.Instance.surfaceBorder);

            if (data != null)
            {
                Color rarityCol = UITheme.Instance.GetInsectRarityColor(data.rarity);
                // alreadyInTeam이면 alpha/채도 낮춤 (struct copy + 필드 갱신)
                Color railCol = rarityCol;
                if (alreadyInTeam) { railCol.r *= 0.4f; railCol.g *= 0.4f; railCol.b *= 0.4f; }
                UISurface.Flat(
                    new Rect(rect.x + 3f, rect.y + 3f + UITheme.Radius.Card, 5f,
                        Mathf.Max(4f, rect.height - 6f - UITheme.Radius.Card * 2f)),
                    railCol);

                InsectVisual.Draw(rect.x + 48, rect.y + rect.height / 2f, 96f, data, pid != null && pid.isShiny, alreadyInTeam ? 0.4f : 1f);

                pickerNameCache.normal.textColor = alreadyInTeam ? DimNameCol : rarityCol;
                GUI.color = Color.white;
                UIHelper.LabelFit(
                    new Rect(rect.x + 100, rect.y + 14, rect.width - 280, 42),
                    GetOwnedDisplayName(pid, data), pickerNameCache);

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
                float selectH = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 52f, 64f);
                if (GUI.Button(new Rect(rect.x + rect.width - 170, rect.y + rect.height / 2f - selectH * 0.5f, 150, selectH), "선택", pickerBtnCache))
                {
                    if (teamManager != null)
                    {
                        teamManager.SetSlot(selectingSlot, pid.instanceId);
                        selectingSlot = -1;
                        directScroll.Reset();
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

        /// <summary>병원 진입점 — 팀에 부상이 있을 때 헤더 버튼으로 넘긴다(치료 로직은 그쪽 소유).</summary>
        public void AutoWire(HospitalUI hospital)
        {
            if (hospitalUi == null) hospitalUi = hospital;
        }

        /// <summary>
        /// 팀 슬롯 중 치료가 필요한 수. 판정은 병원과 같다 — HP가 깎였거나 독/마비.
        /// <b>여기서 치료비를 계산하지 않는다</b>: 병원이 결제 수단·할인·환불을 들고 있고,
        /// 두 곳에서 값을 만들면 표시와 실제가 갈린다.
        /// </summary>
        private int InjuredTeamCount()
        {
            if (teamManager == null || collection == null) return 0;

            int count = 0;
            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                PlayerInsectData pid = collection.GetByInstanceId(teamManager.GetSlot(i));
                if (pid != null && NeedsHeal(pid)) count++;
            }
            return count;
        }

        /// <summary>
        /// 피커 목록을 정렬해 버퍼에 담는다. <b>OnGUI 패스마다 정렬하지 않는다</b> —
        /// 비교자 델리게이트 할당과 전체 순회가 프레임당 두 번 이상 들기 때문이다.
        /// 기준을 바꿀 때(dirty)와 보유 수가 달라졌을 때만 다시 굽는다.
        /// 레벨업으로 CP만 바뀐 경우는 다음에 피커를 열 때 반영된다(순간 갱신이 필요한 화면이 아니다).
        /// </summary>
        private void EnsurePickerSorted()
        {
            IReadOnlyList<PlayerInsectData> owned = collection.OwnedView;
            if (!pickerSortDirty && pickerSortedCount == owned.Count) return;

            InsectBrowseSort.Sort(owned, collection, pickerSort, pickerSorted);
            pickerSortedCount = owned.Count;
            pickerSortDirty = false;
        }

        /// <summary>부상 요약 — 상태이상이 HP보다 급하므로 먼저 보여준다.</summary>
        private string HurtLabel(PlayerInsectData pid, InsectData data)
        {
            if (pid.isPoisoned && pid.isParalyzed) return "독·마비";
            if (pid.isPoisoned) return "독";
            if (pid.isParalyzed) return "마비";

            int maxHp = pid.GetTotalHp(data.baseHp);
            int curHp = pid.currentHp < 0 ? maxHp : pid.currentHp;
            return curHp <= 0 ? "기절" : $"HP {curHp}/{maxHp}";
        }

        private bool NeedsHeal(PlayerInsectData pid)
        {
            if (pid == null) return false;
            if (pid.isPoisoned || pid.isParalyzed) return true;

            InsectData data = collection.GetInsectData(pid.insectId);
            if (data == null) return false;
            int maxHp = pid.GetTotalHp(data.baseHp);
            // currentHp -1은 구세이브 미초기화 센티넬 = 풀피다(0 기절과 구분해야 한다).
            int curHp = pid.currentHp < 0 ? maxHp : pid.currentHp;
            return curHp < maxHp;
        }

        // instanceId 앞 6자리(#A3F2B1)는 붙이지 않는다 — 같은 종을 구분하려던 GUID 조각인데
        // 플레이어에겐 의미 없는 문자열이었다. 구분은 레벨·IV 등급·크기가 맡는다.
        private static string GetOwnedDisplayName(PlayerInsectData pid, InsectData data)
        {
            return data != null ? data.displayName : (pid != null ? pid.insectId : "Unknown");
        }
    }
}
