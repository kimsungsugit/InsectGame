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

        // 헤더 부제("4/5 · 팀 전투력 1272")는 값이 바뀔 때만 다시 만든다 — OnGUI는 프레임당 여러 패스다.
        private string headerSubtitle = string.Empty;
        private int headerFilled = -1;
        private int headerCp = -1;

        // OnGUI 매 프레임 new GUIStyle 회귀 차단 — 캐시 필드 + InitTeamStyles 1회 초기화.
        // 동적 textColor는 매 호출 갱신 (BattleScreenUI ComboCol 패턴).
        // 색은 전부 UITheme 토큰에서 받는다(rules/ui-layout.md) — 한때 이 파일에만 자기 색이 23개 있었다.
        private GUIStyle teamSubCache, slotNumCache, slotNameCache, slotInfoCache, slotHpCache;
        private GUIStyle buttonCache, chipButtonCache, emptyPlusCache;
        private bool teamStylesInit;

        private void InitTeamStyles()
        {
            if (teamStylesInit) return;
            teamStylesInit = true;
            UITheme t = UITheme.Instance;

            teamSubCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            slotNumCache = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            slotNumCache.normal.textColor = t.textSecondary;
            slotNameCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            slotInfoCache = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleLeft };
            slotInfoCache.normal.textColor = t.textSecondary;
            slotHpCache = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            buttonCache = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonCache.normal.textColor = t.textPrimary;
            chipButtonCache = new GUIStyle(buttonCache) { fontSize = 26 };
            emptyPlusCache = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            emptyPlusCache.normal.textColor = t.textMuted;
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

        /// <summary>
        /// 창 자리. 세로 화면은 높이를 크게 잡는다 — 960×940 고정일 때 세로 화면 가운데 절반만 쓰고
        /// 위아래가 비었다(의상 창·캐시샵이 1560으로 고친 것과 같은 결함). 안전 영역을 넘으면 하네스가 줄인다.
        /// </summary>
        private static Rect PanelRect()
        {
            bool mobile = UIScale.IsMobileLayout;
            return UISafeLayout.CenteredPanel(mobile ? 1000f : 1040f, mobile ? 1560f : 960f);
        }

        private void DrawTeamPanel()
        {
            UITheme t = UITheme.Instance;
            Rect panel = PanelRect();
            float panelW = panel.width;
            float panelH = panel.height;
            float px = panel.x;
            float py = panel.y;

            UISurface.Card(panel);
            // 도감과 같은 액센트 헤더 — 제목·부제·닫기를 한 줄에 둔다.
            // 화면 전체가 한국어라 퀵바·HUD가 이 화면을 부르는 "배틀팀"을 그대로 쓴다.
            if (UISurface.Header(new Rect(px + 3f, py + 3f, panelW - 6f, 88f), "배틀팀", HeaderSubtitle()))
            {
                CloseModal();
                return;
            }

            // 부상 안내 + 회복 진입 — 팀을 짜는 자리에서 바로 상태를 알고 고칠 수 있어야 한다.
            // 치료 자체는 병원이 한다(재화·결제·환불 로직을 여기서 복제하면 곧 어긋난다).
            int injured = InjuredTeamCount();
            bool canHeal = injured > 0 && hospitalUi != null;
            teamSubCache.normal.textColor = injured > 0 ? t.accentCoral : t.textSecondary;
            UIHelper.LabelFit(new Rect(px + 28f, py + 104f, panelW - (canHeal ? 330f : 56f), 52f),
                injured > 0
                    ? $"부상 {injured}마리 — 회복하고 나가세요"
                    : $"배틀용 곤충을 최대 {BattleTeamManager.MaxSlots}마리 선택하세요",
                teamSubCache);

            if (canHeal)
            {
                float healH = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 52f, 60f);
                // 폭 250 — "회복하러 가기"(30px 굵은 글자 6자)가 안쪽 여백을 빼고도 들어가는 값.
                if (UISurface.Button(new Rect(px + panelW - 278f, py + 104f + (52f - healH) / 2f, 250f, healH),
                        "회복하러 가기", t.btnPrimary, chipButtonCache))
                {
                    // 배틀팀을 닫고 병원을 연다 — 모달 둘이 겹치면 뒤쪽 버튼이 클릭을 가로챈다.
                    CloseModal();
                    // Toggle은 말 그대로 토글이라, 병원이 이미 열려 있으면 이 버튼이 그걸 닫아
                    // 두 창이 다 사라진다. 지금은 퀵바의 IsAnyOpen 게이트 덕에 도달하지 않지만
                    // 게이트 하나에 기대는 대신 여기서 "열기"로 못박는다.
                    if (!hospitalUi.IsOpen) hospitalUi.Toggle();
                    return;
                }
            }

            float slotY = py + 170f;
            const float slotGap = 10f;
            // 패널이 안전 영역에 맞춰 줄면 슬롯 높이도 함께 줄여 마지막 슬롯이 잘리지 않게 한다.
            float slotAvail = panelH - (slotY - py) - 20f;
            float slotH = Mathf.Clamp(
                (slotAvail - (BattleTeamManager.MaxSlots - 1) * slotGap) / BattleTeamManager.MaxSlots,
                UIScale.MinTouchHeight,
                UIScale.IsMobileLayout ? 300f : 142f);

            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                DrawSlot(px + 20f, slotY + i * (slotH + slotGap), panelW - 40f, slotH, i);
            }
        }

        /// <summary>"4/5 · 팀 전투력 1272" — 합이 바뀔 때만 문자열을 다시 만든다.</summary>
        private string HeaderSubtitle()
        {
            int filled = 0;
            int cp = 0;
            if (teamManager != null && collection != null)
            {
                for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
                {
                    PlayerInsectData pid = collection.GetByInstanceId(teamManager.GetSlot(i));
                    if (pid == null) continue;
                    InsectData data = collection.GetInsectData(pid.insectId);
                    if (data == null) continue;
                    filled++;
                    cp += PlayerInsectCombatPower.Calculate(data, pid);
                }
            }
            if (filled != headerFilled || cp != headerCp)
            {
                headerFilled = filled;
                headerCp = cp;
                headerSubtitle = $"{filled}/{BattleTeamManager.MaxSlots} · 팀 전투력 {cp}";
            }
            return headerSubtitle;
        }

        private void DrawSlot(float x, float y, float w, float h, int index)
        {
            UITheme t = UITheme.Instance;
            string instanceId = teamManager != null ? teamManager.GetSlot(index) : null;
            PlayerInsectData pid = !string.IsNullOrEmpty(instanceId) && collection != null
                ? collection.GetByInstanceId(instanceId) : null;
            InsectData data = pid != null ? collection.GetInsectData(pid.insectId) : null;
            bool hasInsect = !string.IsNullOrEmpty(instanceId);

            UISurface.Card(new Rect(x, y, w, h), hasInsect ? t.surfaceRaised : t.surfaceBase, t.surfaceBorder);
            DrawSlotNumber(x, y, h, index);

            float thumb = Mathf.Min(h - 20f, UIScale.IsMobileLayout ? 200f : 120f);
            Rect frame = new Rect(x + 70f, y + (h - thumb) / 2f, thumb, thumb);
            float textX = frame.xMax + 20f;
            float textW = x + w - 190f - textX;
            float actionH = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 52f, 60f);

            if (hasInsect)
            {
                if (data != null)
                {
                    Color rarityCol = t.GetInsectRarityColor(data.rarity);
                    // 6px 레일 — 각진 채로 두고(ui-layout.md) 세로를 카드 반경만큼 물려
                    // 둥근 모서리를 뚫지 않게 한다.
                    UISurface.Flat(
                        new Rect(x + 3f, y + 3f + UITheme.Radius.Card, 6f,
                            Mathf.Max(4f, h - 6f - UITheme.Radius.Card * 2f)),
                        rarityCol);

                    UISurface.Rounded(frame, Color.Lerp(t.surfaceBase, rarityCol, 0.14f));
                    InsectVisual.Draw(frame.center.x, frame.center.y, thumb * 0.94f, data, pid.isShiny,
                        pid.IsFainted ? 0.45f : 1f);

                    DrawInsectLines(textX, y + h * 0.5f, textW, pid, data, slotNameCache);
                }

                // 두 행동 버튼 — 세로로 쌓되 슬롯이 낮으면 가운데로 모은다.
                float stackH = actionH * 2f + 10f;
                float by = y + Mathf.Max(8f, (h - stackH) / 2f);
                // "변경"은 테두리색 — btnSecondary는 surfaceRaised로 동기화돼 슬롯 카드와 같은 색이라 버튼이 사라져 보였다.
                if (UISurface.Button(new Rect(x + w - 170f, by, 150f, actionH), "변경", t.surfaceBorder, buttonCache))
                {
                    OpenPicker(index);
                    return;
                }
                if (UISurface.Button(new Rect(x + w - 170f, by + actionH + 10f, 150f, actionH), "제거",
                        Color.Lerp(t.btnDanger, t.surfaceRaised, 0.35f), buttonCache))
                    teamManager?.RemoveSlot(index);
            }
            else
            {
                UISurface.Rounded(frame, t.surfaceCard);
                GUI.Label(frame, "+", emptyPlusCache);

                slotInfoCache.normal.textColor = t.textMuted;
                UIHelper.LabelFit(new Rect(textX, y + h / 2f - 22f, textW, 44f), "빈 슬롯 — 곤충을 넣어 주세요", slotInfoCache);
                slotInfoCache.normal.textColor = t.textSecondary;

                float selectH = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 56f, 64f);
                if (UISurface.Button(new Rect(x + w - 170f, y + h / 2f - selectH * 0.5f, 150f, selectH), "선택",
                        t.btnPrimary, buttonCache))
                    OpenPicker(index);
            }
        }

        /// <summary>좌측 번호 원 — 슬롯 순서가 곧 레이드 출전 순서다.</summary>
        private void DrawSlotNumber(float x, float y, float h, int index)
        {
            Rect badge = new Rect(x + 16f, y + h / 2f - 22f, 44f, 44f);
            UISurface.Rounded(badge, UITheme.Instance.surfaceBase, UITheme.Radius.Chip);
            GUI.Label(badge, (index + 1).ToString(), slotNumCache);
        }

        /// <summary>
        /// 이름 · (Lv·등급·CP) · HP 막대 세 줄을 <paramref name="midY"/> 기준 세로 가운데로 그린다.
        /// 배틀팀 슬롯과 피커 행이 같은 모양을 쓴다(두 곳이 따로 놀던 때는 한쪽만 HP를 보였다).
        /// </summary>
        private void DrawInsectLines(float x, float midY, float w, PlayerInsectData pid, InsectData data, GUIStyle nameStyle)
        {
            UITheme t = UITheme.Instance;
            Color rarityCol = t.GetInsectRarityColor(data.rarity);
            nameStyle.normal.textColor = rarityCol;
            // 46px는 이 폰트로 한 줄이라, 긴 이름이 줄바꿈되면 둘째 줄이 잘렸다.
            UIHelper.LabelFit(new Rect(x, midY - 56f, w, 46f), GetOwnedDisplayName(pid, data), nameStyle);

            int cp = PlayerInsectCombatPower.Calculate(data, pid);
            GUI.Label(new Rect(x, midY - 10f, w, 34f),
                $"Lv.{pid.level}  ·  {data.rarity.Korean()}  ·  CP {cp}", slotInfoCache);

            // 부상이면 그 자리에서 알린다 — 헤더의 "회복하러 가기"만으로는 **누가** 다쳤는지 모른다.
            int maxHp = pid.GetTotalHp(data.baseHp);
            int curHp = pid.currentHp < 0 ? maxHp : pid.currentHp;   // -1은 구세이브 미초기화 = 풀피
            float ratio = maxHp > 0 ? (float)curHp / maxHp : 0f;
            float barW = Mathf.Min(w * 0.55f, 300f);
            UISurface.Meter(new Rect(x, midY + 34f, barW, 12f), ratio, t.GetHpColor(ratio));
            bool hurt = NeedsHeal(pid);
            slotHpCache.normal.textColor = hurt ? t.accentCoral : t.textSecondary;
            UIHelper.LabelFit(new Rect(x + barW + 12f, midY + 24f, Mathf.Max(1f, w - barW - 12f), 32f),
                hurt ? HurtLabel(pid, data) : $"HP {curHp}/{maxHp}", slotHpCache);
        }

        private void OpenPicker(int index)
        {
            selectingSlot = index;
            listScroll = Vector2.zero;
            directScroll.Reset();
            pickerSortDirty = true;   // 열 때마다 최신 보유 목록으로 다시 정렬
        }

        private void DrawInsectPicker()
        {
            UITheme t = UITheme.Instance;
            Rect panel = PanelRect();
            float panelW = panel.width;
            float panelH = panel.height;
            float px = panel.x;
            float py = panel.y;

            UISurface.Card(panel);
            // 헤더의 오른쪽 버튼이 곧 "뒤로"다 — 창을 닫지 않고 슬롯 화면으로 돌아간다.
            if (UISurface.Header(new Rect(px + 3f, py + 3f, panelW - 6f, 88f),
                    $"{selectingSlot + 1}번 슬롯", "넣을 곤충 선택", "‹ 뒤로"))
            {
                selectingSlot = -1;
                directScroll.Reset();
                return;
            }

            if (collection == null) return;

            // ── 정렬 칩 ──
            // 팀에 넣을 곤충을 고를 때 필요한 건 "무엇이 강한가"다. 기본은 등급 → CP 순이고,
            // 레벨·전투력·최근 획득으로 바꿀 수 있다. 순서 규칙은 InsectBrowseSort가 단일 출처이며
            // 보유 곤충 화면도 같은 것을 쓴다(두 화면이 다르게 정렬하면 방금 본 개체를 다시 찾아야 한다).
            float chipY = py + 106f;
            float chipH = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 48f, 56f);
            float chipGap = 8f;
            float chipW = (panelW - 48f - chipGap * (InsectBrowseSort.Order.Length - 1)) / InsectBrowseSort.Order.Length;
            for (int i = 0; i < InsectBrowseSort.Order.Length; i++)
            {
                InsectSortMode mode = InsectBrowseSort.Order[i];
                Rect chip = new Rect(px + 24f + i * (chipW + chipGap), chipY, chipW, chipH);
                if (UISurface.Button(chip, InsectBrowseSort.Label(mode), t.surfaceRaised, chipButtonCache, pickerSort == mode)
                    && pickerSort != mode)
                {
                    pickerSort = mode;
                    pickerSortDirty = true;
                    listScroll = Vector2.zero;
                    directScroll.Reset();
                }
            }

            EnsurePickerSorted();
            List<PlayerInsectData> owned = pickerSorted;
            float listY = chipY + chipH + 14f;
            float listH = Mathf.Max(1f, panelH - (listY - py) - 16f);
            float itemH = UIScale.IsMobileLayout ? 168f : 130f;
            float totalH = owned.Count * itemH;
            Rect listArea = new Rect(px + 16f, listY, panelW - 32f, listH);
            Rect viewRect = new Rect(0, 0, listArea.width - 12f, totalH);

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
                listScroll.y, listArea.height, itemH - 8f, 8f, owned.Count,
                out int firstVisible, out int lastVisible);

            for (int i = firstVisible; i <= lastVisible; i++)
            {
                PlayerInsectData pid = owned[i];
                InsectData data = collection.GetInsectData(pid.insectId);
                bool alreadyInTeam = teamManager != null && teamManager.IsInTeam(pid.instanceId);
                DrawPickerItem(new Rect(4f, i * itemH, viewRect.width - 4f, itemH - 8f), pid, data, alreadyInTeam);
            }
            GUI.EndScrollView();
            UISurface.ScrollAffordance(listArea, listScroll, totalH, t.accentCoral);
        }

        private void DrawPickerItem(Rect rect, PlayerInsectData pid, InsectData data, bool alreadyInTeam)
        {
            UITheme t = UITheme.Instance;
            UISurface.Card(rect, alreadyInTeam ? t.surfaceBase : t.surfaceRaised, t.surfaceBorder);

            float thumb = Mathf.Min(rect.height - 20f, UIScale.IsMobileLayout ? 140f : 104f);
            Rect frame = new Rect(rect.x + 20f, rect.y + (rect.height - thumb) / 2f, thumb, thumb);
            float textX = frame.xMax + 20f;
            float textW = rect.xMax - 200f - textX;

            if (data != null)
            {
                Color rarityCol = t.GetInsectRarityColor(data.rarity);
                // 이미 팀에 있으면 레일을 어둡게 — 고를 수 없는 줄이 한눈에 가라앉는다.
                UISurface.Flat(
                    new Rect(rect.x + 3f, rect.y + 3f + UITheme.Radius.Card, 5f,
                        Mathf.Max(4f, rect.height - 6f - UITheme.Radius.Card * 2f)),
                    alreadyInTeam ? Color.Lerp(rarityCol, t.surfaceBase, 0.6f) : rarityCol);

                UISurface.Rounded(frame, Color.Lerp(t.surfaceBase, rarityCol, alreadyInTeam ? 0.05f : 0.14f));
                InsectVisual.Draw(frame.center.x, frame.center.y, thumb * 0.94f, data, pid != null && pid.isShiny,
                    alreadyInTeam ? 0.4f : 1f);

                DrawInsectLines(textX, rect.y + rect.height * 0.5f, textW, pid, data, slotNameCache);
            }

            if (alreadyInTeam)
            {
                UISurface.Chip(new Rect(rect.xMax - 180f, rect.y + rect.height / 2f - 20f, 160f, 40f), "팀에 있음",
                    t.surfaceRaised, t.textSecondary);
            }
            else
            {
                float selectH = SkillUILayout.GetTouchHeight(UIScale.IsMobileLayout, 56f, 64f);
                if (UISurface.Button(new Rect(rect.xMax - 180f, rect.y + rect.height / 2f - selectH * 0.5f, 160f, selectH),
                        "선택", t.btnPrimary, buttonCache)
                    && !directScroll.IsDragging)
                {
                    if (teamManager != null)
                    {
                        teamManager.SetSlot(selectingSlot, pid.instanceId);
                        selectingSlot = -1;
                        directScroll.Reset();
                    }
                }
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
