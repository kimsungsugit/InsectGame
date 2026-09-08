using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Dex;   // DexBrowseLayout — 목록 뷰포트 컬링 계산 공유
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
        private readonly UIDirectScroll directScroll = new UIDirectScroll();
        private Vector2 detailScrollPos;
        private readonly UIDirectScroll detailDirectScroll = new UIDirectScroll();
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
        private GUIStyle learnsetHeaderStyle;
        private GUIStyle learnsetLevelStyle;
        private GUIStyle learnsetNameStyle;
        private GUIStyle learnsetMetaStyle;
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
            panelTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            panelTitleStyle.normal.textColor = Color.white;
            panelCloseStyle = new GUIStyle(GUI.skin.button) { fontSize = 40, fontStyle = FontStyle.Bold };
            panelTabActiveStyle = new GUIStyle(GUI.skin.button) { fontSize = 34, fontStyle = FontStyle.Bold };
            panelTabInactiveStyle = new GUIStyle(GUI.skin.button) { fontSize = 34, fontStyle = FontStyle.Normal };
            detailBackStyle = new GUIStyle(GUI.skin.button) { fontSize = 32, fontStyle = FontStyle.Bold };
            detailNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            detailRarityStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter };
            detailGradeDispStyle = new GUIStyle(GUI.skin.label) { fontSize = 62, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            detailGradePercStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            detailDescStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, wordWrap = true };
            detailDescStyle.normal.textColor = DescGrayCol;
            detailHintStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Italic };
            learnsetHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            learnsetHeaderStyle.normal.textColor = new Color(0.72f, 0.9f, 0.78f);
            learnsetLevelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            learnsetNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 27,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            learnsetMetaStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 23,
                alignment = TextAnchor.MiddleRight,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            detailHintStyle.normal.textColor = HintGreenCol;
            statsLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 38 };
            statsLabelStyle.normal.textColor = StatsLabelCol;
            statsValueStyle = new GUIStyle(GUI.skin.label) { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            statsValueStyle.normal.textColor = Color.white;
            statsCandyValStyle = new GUIStyle(statsValueStyle);
            statsCandyValStyle.normal.textColor = CandyValCol;
            luLvLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
            luLvLabelStyle.normal.textColor = LuLabelBlueCol;
            luLvNumStyle = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold };
            luLvNumStyle.normal.textColor = Color.white;
            luXpLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            luXpLabelStyle.normal.textColor = LuXpLabelCol;
            luXpValStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            luXpValStyle.normal.textColor = LuXpValCol;
            luMaxLvStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleRight };
            luMaxLvStyle.normal.textColor = LuMaxLvCol;
            luBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
            luCandyInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            luMsgStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            barLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold };
            barLabelStyle.normal.textColor = BarLabelGrayCol;
            barIvStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
            barTotalStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleRight };
            barTotalStyle.normal.textColor = BarTotalLightCol;
            barIvLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 27 };
            barIvLabelStyle.normal.textColor = BarIvLabelGrayCol;
            centeredLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            detailStylesReady = true;
        }

        // GetAllOwned 매 프레임 호출 회피 — InsectUpdated 이벤트 시 invalidate.
        private List<PlayerInsectData> cachedOwned;
        // 목록 행의 정보·스탯 문자열. cachedOwned와 같은 순간에 함께 굽는다(아래 BuildRowTextCache).
        private string[] cachedRowInfo;
        private string[] cachedRowStats;
        private bool ownedCacheDirty = true;

        // ── 정렬 ──
        // 순서 규칙의 단일 출처는 InsectBrowseSort다(배틀팀 피커와 공유). 여기서는 그 위에
        // "배틀팀 먼저"를 얹는다 — 편성한 곤충을 찾으려고 목록을 스크롤하지 않아도 되게.
        [SerializeField] private BattleTeamManager teamManager;
        private InsectSortMode sortMode = InsectSortMode.Rarity;
        private bool teamFirst = true;
        // 정렬 칩 Rect 배열은 죽은 필드였다(대입만·읽기 없음) — BattleTeamUI와 같은 이유로 제거.
        private Rect teamFirstChip;
        // 정렬 결과는 cachedOwned와 같은 순간에 굽는다 — 행 문자열 캐시(cachedRowInfo)가
        // **인덱스로** 목록을 참조하므로 둘의 순서가 어긋나면 다른 곤충의 스탯이 표시된다.
        private readonly List<PlayerInsectData> sortedOwned = new List<PlayerInsectData>();
        private System.Func<PlayerInsectData, bool> isInTeamCache;

        private void InitItemStyles()
        {
            if (itemStylesReady) return;
            itemNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 38, fontStyle = FontStyle.Bold };
            itemInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = 32 };
            itemInfoStyle.normal.textColor = ItemInfoGrayCol;
            itemGradeStyle = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            itemStatMiniStyle = new GUIStyle(GUI.skin.label) { fontSize = 27, alignment = TextAnchor.MiddleRight };
            itemStatMiniStyle.normal.textColor = ItemStatGrayCol;
            itemViewStyle = new GUIStyle(GUI.skin.button) { fontSize = 31 };
            itemStylesReady = true;
        }

        private List<PlayerInsectData> GetCachedOwned()
        {
            if (insectCollection == null) return null;
            if (ownedCacheDirty || cachedOwned == null)
            {
                // OwnedView는 컬렉션의 재사용 버퍼(보관 금지)지만 여기서 곧바로 sortedOwned로
                // 복사하므로 안전하다 — GetAllOwned()처럼 무효화마다 List를 새로 만들지 않는다.
                SortInto(insectCollection.OwnedView);
                cachedOwned = sortedOwned;
                BuildRowTextCache();
                ownedCacheDirty = false;
            }
            return cachedOwned;
        }

        /// <summary>
        /// 정렬 결과를 <see cref="sortedOwned"/>에 채운다. 무효화 시점에만 돌기 때문에
        /// OnGUI 패스마다 비교자가 도는 일이 없다. <b>반드시 BuildRowTextCache보다 먼저</b> 부른다 —
        /// 행 문자열이 인덱스로 대응하므로 순서가 나중에 바뀌면 다른 곤충의 정보가 붙는다.
        /// </summary>
        private void SortInto(IReadOnlyList<PlayerInsectData> source)
        {
            // 델리게이트를 필드에 캐시한다 — 무효화가 잦아(포획·레벨업·치료마다) 매번 새로 만들면 쌓인다.
            if (isInTeamCache == null)
                isInTeamCache = pid => teamManager != null && pid != null && teamManager.IsInTeam(pid.instanceId);

            InsectBrowseSort.Sort(source, insectCollection, sortMode, sortedOwned,
                isInTeamCache, teamFirst && teamManager != null);
        }

        private void HandleTeamChanged() { ownedCacheDirty = true; }

        /// <summary>
        /// 목록 행의 정보·스탯 문자열을 미리 굽는다. 값이 전부 (종·개체·레벨) 파생이라 불변인데
        /// 예전엔 <see cref="DrawInsectItem"/>이 <b>행마다 매 OnGUI 패스</b>에 다시 만들었다 —
        /// enum <c>ToString</c>(박싱) + <c>SizeLabel</c> + 보간 2개로 행당 4개다. 뷰포트 컬링을
        /// 넣은 뒤에도 "보이는 행 × 패스 수"만큼 계속 나온다(OnGUI는 Layout·Repaint·입력마다 돈다).
        ///
        /// 무효화는 이미 있는 <c>ownedCacheDirty</c>가 맡는다 — 포획·레벨업·치료·진화가 전부
        /// <c>InsectUpdated</c>를 쏘므로 레벨이 바뀌면 문자열도 함께 다시 굽힌다.
        /// (형제 화면인 RegionMapUI·TrainingUI가 같은 결함을 같은 방식으로 이미 고쳤다.)
        /// </summary>
        private void BuildRowTextCache()
        {
            int n = cachedOwned != null ? cachedOwned.Count : 0;
            if (cachedRowInfo == null || cachedRowInfo.Length < n)
            {
                cachedRowInfo = new string[n];
                cachedRowStats = new string[n];
            }

            for (int i = 0; i < n; i++)
            {
                PlayerInsectData pid = cachedOwned[i];
                if (pid == null)
                {
                    cachedRowInfo[i] = string.Empty;
                    cachedRowStats[i] = string.Empty;
                    continue;
                }

                InsectData data = insectCollection.GetInsectData(pid.insectId);
                string rarityStr = data != null ? data.rarity.ToString() : "?";
                // 크기는 #코드를 대신하는 개체 구분 축이라 목록 줄에 함께 보여준다.
                string sizeStr = data != null
                    ? "  |  " + InsectSizeCalculator.SizeLabel(InsectSizeCalculator.SizeMm(data, pid))
                    : string.Empty;
                cachedRowInfo[i] = $"Lv.{pid.level}  |  {rarityStr}  |  IV: {pid.IVPercent * 100:0}%{sizeStr}";
                cachedRowStats[i] = data != null
                    ? $"HP:{pid.ivHp} ATK:{pid.ivAtk} DEF:{pid.ivDef}"
                    : string.Empty;
            }
        }

        private void HandleInsectUpdated(PlayerInsectData _) { ownedCacheDirty = true; }

        private void OnEnable()
        {
            if (insectCollection != null)
            {
                insectCollection.InsectUpdated -= HandleInsectUpdated;
                insectCollection.InsectUpdated += HandleInsectUpdated;
            }
            // 오프닝 다시보기가 UI 루트를 껐다 켜므로 OnDisable에서 끊은 것을 여기서 되살린다
            // (rules/ui-layout.md의 구독 규칙 — AutoWire는 Bootstrap에서 한 번만 불린다).
            if (teamManager != null)
            {
                teamManager.TeamChanged -= HandleTeamChanged;
                teamManager.TeamChanged += HandleTeamChanged;
            }
            ownedCacheDirty = true;
        }

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            if (!isOpen)
            {
                selectedInstanceId = null;
                directScroll.Reset();
                detailDirectScroll.Reset();
            }
            else
            {
                scrollPos = Vector2.zero;
                directScroll.Reset();
                detailScrollPos = Vector2.zero;
                detailDirectScroll.Reset();
            }
            if (isOpen && TutorialQuestManager.Instance != null)
                TutorialQuestManager.Instance.NotifyCollectionOpened();
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }
        public void CloseModal()
        {
            isOpen = false;
            selectedInstanceId = null;
            directScroll.Reset();
            detailDirectScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }
        private void OnDisable()
        {
            isOpen = false;
            selectedInstanceId = null;
            directScroll.Reset();
            detailDirectScroll.Reset();
            ModalUIRegistry.Unregister(this);
            if (insectCollection != null)
                insectCollection.InsectUpdated -= HandleInsectUpdated;
            if (teamManager != null)
                teamManager.TeamChanged -= HandleTeamChanged;
        }

        // 빈 `Update()`와 빈 `DrawToggleButton()`이 여기 있었다. 전자는 본문이 없어도 Unity가
        // 매 프레임 managed→native 호출을 하고, 후자는 `OnGUI` 첫 줄에서 `isOpen` 검사보다
        // **앞에** 불려 닫혀 있을 때도 매 패스 돌았다. 둘 다 하는 일이 없어 제거했다
        // (퀵바 진입점은 `QuickAccessBarUI`가 맡는다).

        private void OnGUI()
        {
            if (!isOpen) return;

            UIScale.Begin();
            if (selectedInstanceId != null)
                DrawDetailPanel();
            else
                DrawPanel();
            UIScale.End();
        }

        private void DrawPanel()
        {
            InitDetailStyles();
            // 세이프에어리어 + 세로 마진 안으로 자동 clamp — 가로 캔버스(높이 1080)에서 잘리던 자리다.
            Rect panel = UISafeLayout.AnchoredPanel(1000f, 1000f, UISafeLayout.HAlign.Right);
            float panelW = panel.width;
            float panelH = panel.height;
            float panelX = panel.x;
            float panelY = panel.y;

            UISurface.Card(new Rect(panelX, panelY, panelW, panelH), PanelBgCol, UITheme.Instance.surfaceBorder);
            UISurface.Rounded(new Rect(panelX + 3f, panelY + 3f, panelW - 6f, 88f), PanelHeaderCol);

            GUI.color = Color.white;
            UIHelper.LabelFit(new Rect(panelX, panelY + 16, panelW - 84, 58), "컬렉션", panelTitleStyle);

            if (GUI.Button(new Rect(panelX + panelW - 72, panelY + 16, 56, 56), "X", panelCloseStyle))
            {
                CloseModal();
            }

            float tabY = panelY + 98;
            for (int i = 0; i < tabNames.Length; i++)
            {
                float tabX = panelX + i * 300 + 24;
                bool active = selectedTab == i;
                GUI.backgroundColor = active ? TabActiveBgCol : TabInactiveBgCol;
                if (GUI.Button(new Rect(tabX, tabY, 280, 64), tabNames[i], active ? panelTabActiveStyle : panelTabInactiveStyle))
                {
                    selectedTab = i;
                    scrollPos = Vector2.zero;
                    directScroll.Reset();
                }
            }
            GUI.backgroundColor = Color.white;
            GUI.color = Color.white;

            float contentY = tabY + 78;
            // 정렬 줄은 보유 곤충 탭에만 붙인다 — 통계 탭에는 목록이 없다.
            if (selectedTab == 0)
                contentY += DrawSortBar(panelX + 18, contentY, panelW - 36);
            float contentH = panelH - (contentY - panelY) - 16;
            Rect contentRect = new Rect(panelX + 18, contentY, panelW - 36, contentH);

            if (selectedTab == 0)
                DrawInsectList(contentRect);
            else
                DrawStats(contentRect);
        }

        /// <summary>정렬 칩 + "팀 먼저" 토글. 소비한 세로 높이를 돌려준다.</summary>
        private float DrawSortBar(float x, float y, float w)
        {
            InitItemStyles();
            float h = UIScale.IsMobileLayout ? 60f : 52f;
            float toggleW = 180f;
            float chipW = (w - toggleW - 6f - (InsectBrowseSort.Order.Length - 1) * 6f) / InsectBrowseSort.Order.Length;

            for (int i = 0; i < InsectBrowseSort.Order.Length; i++)
            {
                InsectSortMode mode = InsectBrowseSort.Order[i];
                Rect chip = new Rect(x + i * (chipW + 6f), y, chipW, h);
                GUI.backgroundColor = sortMode == mode ? TabActiveBgCol : TabInactiveBgCol;
                if (GUI.Button(chip, InsectBrowseSort.Label(mode), itemViewStyle) && sortMode != mode)
                {
                    sortMode = mode;
                    ownedCacheDirty = true;   // 순서와 행 문자열을 함께 다시 굽는다
                    scrollPos = Vector2.zero;
                    directScroll.Reset();
                }
            }

            teamFirstChip = new Rect(x + w - toggleW, y, toggleW, h);
            GUI.backgroundColor = teamFirst ? TabActiveBgCol : TabInactiveBgCol;
            if (GUI.Button(teamFirstChip, teamFirst ? "팀 먼저 ON" : "팀 먼저 OFF", itemViewStyle))
            {
                teamFirst = !teamFirst;
                ownedCacheDirty = true;
                scrollPos = Vector2.zero;
                directScroll.Reset();
            }
            GUI.backgroundColor = Color.white;

            return h + 12f;
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
                DrawCenteredLabel(area, UIScale.IsMobileLayout
                    ? "아직 포획한 곤충이 없습니다!\n곤충에 가까이 가서 포획 버튼을 누르세요"
                    : "아직 포획한 곤충이 없습니다!\n곤충에 가까이 가서 E키를 누르세요", NoInsectCol);
                return;
            }

            InitItemStyles();

            float itemH = 168f;
            float totalH = owned.Count * itemH;
            Rect viewRect = new Rect(0, 0, area.width, totalH);

            directScroll.Handle(ref scrollPos, area, totalH, itemH * 0.35f);
            scrollPos = GUI.BeginScrollView(
                area,
                scrollPos,
                viewRect,
                GUIStyle.none,
                GUIStyle.none);
            // 화면에 걸치는 줄만 그린다 — DrawInsectItem이 개체마다 3D 썸네일을 요청하므로,
            // 컬링하지 않으면 한 뷰포트 분량짜리 캐시가 영구 스래싱한다(2026-08-06 audit).
            DexBrowseLayout.GetVisibleRowRange(
                scrollPos.y, area.height, itemH - 4f, 4f, owned.Count,
                out int firstVisible, out int lastVisible);

            for (int i = firstVisible; i <= lastVisible; i++)
            {
                PlayerInsectData pid = owned[i];
                InsectData data = insectCollection.GetInsectData(pid.insectId);
                // 문자열은 BuildRowTextCache가 미리 구웠다. 길이 가드는 캐시와 목록이 어긋난
                // 한 프레임(다음 GetCachedOwned가 맞춘다)에 대한 안전망이다.
                string rowInfo = cachedRowInfo != null && i < cachedRowInfo.Length ? cachedRowInfo[i] : string.Empty;
                string rowStats = cachedRowStats != null && i < cachedRowStats.Length ? cachedRowStats[i] : string.Empty;
                bool inTeam = teamManager != null && teamManager.IsInTeam(pid.instanceId);
                if (DrawInsectItem(new Rect(0, i * itemH, viewRect.width, itemH - 4), pid, data, rowInfo, rowStats, inTeam))
                {
                    selectedInstanceId = pid.instanceId;
                    detailScrollPos = Vector2.zero;
                    detailDirectScroll.Reset();
                }
            }
            GUI.EndScrollView();
        }

        private bool DrawInsectItem(Rect rect, PlayerInsectData pid, InsectData data,
            string infoText, string statsText, bool inTeam)
        {
            bool clicked = false;
            Color rarityColor = data != null ? GetRarityColor(data.rarity) : Color.gray;
            int rarityTier = data != null ? (int)data.rarity : 0;

            UISurface.Card(rect, ItemBgCol, UITheme.Instance.surfaceBorder);
            GUI.color = Color.white;

            UIHelper.DrawRarityBorder(rect, rarityTier, Time.time);

            if (data != null)
                InsectVisual.Draw(rect.x + 72, rect.y + rect.height / 2f + 2, 96f, data, pid != null && pid.isShiny, 1f);

            string displayName = GetOwnedDisplayName(pid, data);
            // 캐시 스타일 + textColor만 동적 갱신 (BattleScreenUI 패턴, owned.Count×5 GUIStyle/프레임 회피).
            itemNameStyle.normal.textColor = rarityColor;
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 138, rect.y + 12, rect.width - 320, 48), displayName, itemNameStyle);

            GUI.Label(new Rect(rect.x + 138, rect.y + 66, rect.width - 320, 40), infoText, itemInfoStyle);

            string gradeStr = CapturePopupUI.GetGradeLabel(pid.Grade);
            Color gradeCol = UITheme.Instance.GetGradeColor(pid.Grade);
            itemGradeStyle.normal.textColor = gradeCol;
            GUI.Label(new Rect(rect.x + rect.width - 170, rect.y + 12, 150, 54), gradeStr, itemGradeStyle);

            if (!string.IsNullOrEmpty(statsText))
            {
                GUI.Label(new Rect(rect.x + 138, rect.y + 112, rect.width - 320, 34),
                    statsText, itemStatMiniStyle);
            }

            // "팀 먼저"로 위에 모아둔 개체가 어디까지인지 목록만 보고 알 수 있게 표시한다.
            // statsText는 같은 띠의 오른쪽 끝에 붙으므로 왼쪽 96px는 비어 있다.
            if (inTeam)
            {
                UISurface.Chip(new Rect(rect.x + 138, rect.y + 110, 100f, 38f),
                    "배틀팀", UITheme.Instance.accentMint, Color.white);
            }

            GUI.backgroundColor = ItemViewBlueCol;
            float detailButtonH = UIScale.IsMobileLayout ? 64f : 52f;
            if (GUI.Button(new Rect(rect.x + rect.width - 172f, rect.y + rect.height - detailButtonH - 8f, 156f, detailButtonH), "상세", itemViewStyle))
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

            // 1040은 "하단에 습득 기술(learnset) 섹션까지 담고 싶은" 희망 높이 — 안전 영역이 좁으면 줄어든다.
            Rect panel = UISafeLayout.AnchoredPanel(1000f, 1040f, UISafeLayout.HAlign.Right);
            float panelW = panel.width;
            float panelH = panel.height;
            float panelX = panel.x;
            float panelY = panel.y;

            UISurface.Card(new Rect(panelX, panelY, panelW, panelH), PanelBgCol, UITheme.Instance.surfaceBorder);

            Color rarityCol = data != null ? GetRarityColor(data.rarity) : Color.gray;
            int detailRarityTier = data != null ? (int)data.rarity : 0;
            GUI.color = Color.white;

            Rect detailRect = new Rect(panelX, panelY, panelW, panelH);
            UIHelper.DrawRarityBorder(detailRect, detailRarityTier, Time.time);
            if (detailRarityTier >= 3)
                UIHelper.DrawRarityGlow(detailRect, rarityCol, detailRarityTier >= 4 ? 0.6f : 0.3f, Time.time);

            if (GUI.Button(new Rect(panelX + 16, panelY + 16, 150, 60), "< 뒤로", detailBackStyle))
            {
                selectedInstanceId = null;
                detailScrollPos = Vector2.zero;
                detailDirectScroll.Reset();
            }

            if (GUI.Button(new Rect(panelX + panelW - 72, panelY + 16, 56, 56), "X", panelCloseStyle))
            {
                CloseModal();
            }

            float portraitCx = panelX + panelW / 2f;
            float portraitCy = panelY + 168;

            // 동적 색상(rarityCol scaled)은 struct stack 할당, GC 영향 없음 (BattleArenaController 판단 일관).
            GUI.color = new Color(rarityCol.r * 0.15f, rarityCol.g * 0.15f, rarityCol.b * 0.15f, 0.6f);
            GUI.DrawTexture(new Rect(portraitCx - 90, portraitCy - 90, 180, 180), Texture2D.whiteTexture);

            GUI.color = new Color(rarityCol.r, rarityCol.g, rarityCol.b, 0.2f);
            GUI.DrawTexture(new Rect(portraitCx - 84, portraitCy - 84, 168, 168), Texture2D.whiteTexture);

            // 등급·ID를 따로 뽑아 넘기던 자리 — 파사드가 InsectData 하나만 받는다.
            InsectVisual.Draw(portraitCx, portraitCy, 168f, data, pid != null && pid.isShiny, 1f);

            string displayName = GetOwnedDisplayName(pid, data);
            // 캐시 스타일 + textColor만 동적 갱신.
            detailNameStyle.normal.textColor = rarityCol;
            GUI.color = Color.white;
            GUI.Label(new Rect(panelX, panelY + 278, panelW, 60), displayName, detailNameStyle);

            detailRarityStyle.normal.textColor = new Color(rarityCol.r, rarityCol.g, rarityCol.b, 0.8f);
            string elementLabel = data != null
                ? InsectTypeChart.GetDisplayName(data.primaryType)
                    + (data.secondaryType != InsectElement.None ? "/" + InsectTypeChart.GetDisplayName(data.secondaryType) : "")
                : "타입 미상";
            GUI.Label(new Rect(panelX, panelY + 340, panelW, 42),
                data != null
                    ? $"{data.rarity} · {elementLabel} 타입 · {InsectSizeCalculator.Summary(data, pid)}"
                    : "Unknown",
                detailRarityStyle);

            Color gradeCol = UITheme.Instance.GetGradeColor(pid.Grade);
            string gradeLabel = CapturePopupUI.GetGradeLabel(pid.Grade);

            detailGradeDispStyle.normal.textColor = gradeCol;
            GUI.Label(new Rect(panelX + panelW - 172, panelY + 268, 150, 82), gradeLabel, detailGradeDispStyle);

            detailGradePercStyle.normal.textColor = new Color(gradeCol.r, gradeCol.g, gradeCol.b, 0.7f);
            UIHelper.LabelFit(new Rect(panelX + panelW - 172, panelY + 348, 150, 36),
                $"{pid.IVPercent * 100:0}%", detailGradePercStyle);

            float lowerTop = panelY + 398f;
            // 패널이 안전 영역에 맞춰 줄면 뷰포트도 함께 줄어 스크롤로 넘어간다(음수 방지).
            Rect lowerViewport = new Rect(
                panelX + 24f,
                lowerTop,
                panelW - 48f,
                Mathf.Max(1f, panelY + panelH - lowerTop - 16f));
            int learnsetRows = CountLearnsetRows(data);
            float learnsetRowH = UIScale.IsMobileLayout ? 58f : 50f;
            float lowerContentH = GetDetailLowerContentHeight(
                learnsetRows,
                UIScale.IsMobileLayout);
            detailDirectScroll.Handle(
                ref detailScrollPos,
                lowerViewport,
                lowerContentH,
                learnsetRowH);

            detailScrollPos = GUI.BeginScrollView(
                lowerViewport,
                detailScrollPos,
                new Rect(0f, 0f, lowerViewport.width, lowerContentH),
                GUIStyle.none,
                GUIStyle.none);

            DrawLevelUpSection(10f, 0f, lowerViewport.width - 20f, pid, data);

            float statBlockY = 168f;
            float statBlockH = 264f;
            GUI.color = StatBlockBgCol;
            GUI.DrawTexture(new Rect(10f, statBlockY, lowerViewport.width - 20f, statBlockH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float sx = 28f;
            float sw = lowerViewport.width - 56f;

            float barY = statBlockY + 18;
            int bHp = data != null ? data.baseHp : 50;
            int bAtk = data != null ? data.baseAtk : 20;
            int bDef = data != null ? data.baseDef : 15;

            DrawStatBar(sx, barY, sw, "HP", pid.ivHp, pid.GetTotalHp(bHp), bHp);
            DrawStatBar(sx, barY + 78, sw, "ATK", pid.ivAtk, pid.GetTotalAtk(bAtk), bAtk);
            DrawStatBar(sx, barY + 156, sw, "DEF", pid.ivDef, pid.GetTotalDef(bDef), bDef);

            if (data != null && !string.IsNullOrEmpty(data.description))
            {
                float descY = statBlockY + statBlockH + 16;
                // 84px 고정 박스 — 긴 설명은 폰트를 줄여 맞춘다(잘림 방지).
                UIHelper.LabelFit(
                    new Rect(16f, descY, lowerViewport.width - 32f, 84f), data.description, detailDescStyle);
            }

            if (data != null && !string.IsNullOrEmpty(data.habitatHint))
            {
                float hintY = statBlockY + statBlockH + 108;
                GUI.Label(new Rect(16f, hintY, lowerViewport.width - 32f, 40),
                    $"서식지: {data.habitatHint}", detailHintStyle);
            }

            // 습득 기술(레벨별) — 성장 로드맵. 도감이 스킬·습득레벨을 노출하지 않던 문제 해소.
            DrawLearnset(16f, statBlockY + statBlockH + 152f, lowerViewport.width - 32f, pid, data);
            GUI.EndScrollView();
        }

        private static int CountLearnsetRows(InsectData data)
        {
            if (data == null || data.learnset == null)
                return 0;

            int count = 0;
            foreach (InsectLearnableSkill learnable in data.learnset)
            {
                if (learnable != null && learnable.skill != null)
                    count++;
            }
            return count;
        }

        internal static float GetDetailLowerContentHeight(int learnsetRows, bool mobileLayout)
        {
            int safeRows = Mathf.Max(0, learnsetRows);
            if (safeRows == 0)
                return 594f;

            float rowHeight = mobileLayout ? 58f : 50f;
            return 640f + safeRows * rowHeight;
        }

        // 레벨별 습득 기술을 compact 목록으로. 현재 레벨 습득분은 강조, 미습득은 딤.
        private void DrawLearnset(float x, float y, float w, PlayerInsectData pid, InsectData data)
        {
            if (data == null || data.learnset == null || data.learnset.Length == 0) return;

            bool mobile = UIScale.IsMobileLayout;
            learnsetNameStyle.fontSize = mobile ? 29 : 27;
            GUI.Label(new Rect(x, y, w, 42), "습득 기술", learnsetHeaderStyle);
            float ry = y + 44f;
            float rowH = mobile ? 58f : 50f;
            float levelW = 84f;
            float metaW = Mathf.Clamp(w * 0.38f, 280f, 360f);
            float nameW = Mathf.Max(140f, w - levelW - metaW - 16f);
            int level = pid != null ? pid.level : 1;
            foreach (InsectLearnableSkill ls in data.learnset)
            {
                if (ls == null || ls.skill == null) continue;
                bool learned = ls.learnLevel <= level;
                string typeLabel = ls.skill.effectType == SkillEffectType.Damage ? "공격"
                    : ls.skill.effectType == SkillEffectType.BuffAttack ? "버프" : "디버프";
                learnsetLevelStyle.normal.textColor = learned
                    ? new Color(0.78f, 0.86f, 1f)
                    : SkillUILayout.DisabledSecondaryTextColor;
                learnsetNameStyle.normal.textColor = learned
                    ? Color.white
                    : SkillUILayout.DisabledTextColor;
                learnsetMetaStyle.normal.textColor = learned
                    ? new Color(0.76f, 0.82f, 0.92f)
                    : SkillUILayout.DisabledSecondaryTextColor;

                GUI.Label(new Rect(x, ry, levelW, rowH), $"Lv{ls.learnLevel}", learnsetLevelStyle);
                GUI.Label(new Rect(x + levelW, ry, nameW, rowH), ls.skill.displayName, learnsetNameStyle);
                GUI.Label(new Rect(x + w - metaW, ry, metaW, rowH),
                    $"{InsectTypeChart.GetDisplayName(ls.skill.element)} · {typeLabel}"
                    + (learned ? "" : " · 미습득"),
                    learnsetMetaStyle);
                ry += rowH;
            }
        }

        private void DrawStats(Rect area)
        {
            InitDetailStyles();

            float y = area.y + 20;
            float rowH = 74f;
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
            GUI.DrawTexture(new Rect(x, y, w, 150), Texture2D.whiteTexture);
            GUI.color = LuAccentBlueCol;
            GUI.DrawTexture(new Rect(x, y, w, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 16, y + 10, 130, 26), "LEVEL", luLvLabelStyle);
            GUI.Label(new Rect(x + 16, y + 38, 104, 54), pid.level.ToString(), luLvNumStyle);

            int maxLv = insectCollection != null ? insectCollection.GetMaxLevel(pid.insectId) : 50;
            int candyCost = insectCollection != null ? insectCollection.GetCandyCostForLevel(pid.insectId, pid.level) : (4 + (pid.level - 1) * 2);
            bool isMaxLevel = pid.level >= maxLv;
            int xpNeeded = insectCollection != null ? insectCollection.GetXpToNextLevel(pid.insectId, pid.level) : (20 + (pid.level - 1) * 8);
            float xpRatio = xpNeeded > 0 ? Mathf.Clamp01((float)pid.currentXp / xpNeeded) : 1f;

            float barX = x + 128;
            float barW = w - 350;
            float barH = 24f;
            float barY2 = y + 46;

            GUI.Label(new Rect(barX, y + 16, barW, 24), isMaxLevel ? "MAX LEVEL" : "경험치 (EXP)", luXpLabelStyle);

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

            GUI.Label(new Rect(barX, barY2 + barH + 4, barW, 24), $"최대 Lv.{maxLv}", luMaxLvStyle);

            float btnX = x + w - 200;
            float btnY2 = y + 16;
            float btnW2 = 184f;
            float btnH2 = 64f;

            int currentCandy = candyInventory != null ? candyInventory.Candies : 0;
            bool canAfford = currentCandy >= candyCost && !isMaxLevel;

            GUI.backgroundColor = canAfford ? LuBtnGreenCol : LuBtnDisabledCol;
            GUI.enabled = canAfford;
            if (GUI.Button(new Rect(btnX, btnY2, btnW2, btnH2),
                isMaxLevel ? "MAX" : $"레벨업\n<size=21>캔디 {candyCost}</size>", luBtnStyle))
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
            GUI.Label(new Rect(btnX, btnY2 + btnH2 + 4, btnW2, 24),
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
                GUI.Label(new Rect(x, y + 114, w, 34), levelUpMsg, luMsgStyle);
            }
        }

        private void DrawStatBar(float x, float y, float w, string label, int iv, int total, int baseStat)
        {
            InitDetailStyles();
            GUI.Label(new Rect(x, y, 90, 40), label, barLabelStyle);

            float barX = x + 100;
            float barW = w - 270;
            float barH = 28f;
            float barY2 = y + 6;

            GUI.color = BarBgCol;
            GUI.DrawTexture(new Rect(barX, barY2, barW, barH), Texture2D.whiteTexture);

            float ivRatio = iv / (float)PlayerInsectData.MaxIV;
            Color barCol = CapturePopupUI.GetIVBarColor(iv);
            GUI.color = barCol;
            GUI.DrawTexture(new Rect(barX, barY2, barW * ivRatio, barH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // barIvStyle textColor 동적 갱신 (barCol 따라).
            barIvStyle.normal.textColor = barCol;
            GUI.Label(new Rect(barX + barW + 10, y, 66, 40), $"{iv}", barIvStyle);

            GUI.Label(new Rect(x + w - 90, y, 90, 40), $"{total}", barTotalStyle);

            GUI.Label(new Rect(x, y + 44, w, 34),
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

        // #코드 미표시 — 이로치 표식(★)만 남긴다. 개체 구분은 레벨·IV 등급·크기가 맡는다.
        private string GetOwnedDisplayName(PlayerInsectData pid, InsectData data)
        {
            string baseName = data != null ? data.displayName : (pid != null ? pid.insectId : "Unknown");
            string shinyMark = (pid != null && pid.isShiny) ? "★ " : "";
            return shinyMark + baseName;
        }

        /// <summary>
        /// 배틀팀 소속을 목록 맨 위로 올리기 위해서만 쓴다. 팀이 바뀌면 목록 순서도 바뀌므로
        /// <see cref="BattleTeamManager.TeamChanged"/>를 구독해 캐시를 무효화한다 —
        /// 안 하면 팀에서 뺀 곤충이 화면을 다시 열 때까지 맨 위에 남는다.
        /// </summary>
        public void AutoWire(BattleTeamManager team)
        {
            if (teamManager == team) return;
            if (teamManager != null) teamManager.TeamChanged -= HandleTeamChanged;
            teamManager = team;
            if (teamManager != null && isActiveAndEnabled)
            {
                teamManager.TeamChanged -= HandleTeamChanged;
                teamManager.TeamChanged += HandleTeamChanged;
            }
            ownedCacheDirty = true;
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
