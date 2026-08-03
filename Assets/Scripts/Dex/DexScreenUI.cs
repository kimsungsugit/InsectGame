using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Dex
{
    public class DexScreenUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private InsectDatabase database;
        [SerializeField] private DexController dexController;
        [SerializeField] private InsectModelPreviewRenderer previewRenderer; // 곤충 3D 모델 RenderTexture 프리뷰
        private float previewAngle = 150f; // 도감 곤충 프리뷰 Y회전(좌우 버튼으로 시점 변경)
        private bool previewShiny;         // 이로치(색다른 모습) 프리뷰 토글
        [SerializeField] private PlayerInsectCollection insectCollection;
        [SerializeField] private PlayerItemInventory itemInventory;

        private bool isOpen;
        // 탭별 스크롤을 분리한다. 탭을 오가거나 상세를 열어도 다른 목록 위치가 유지된다.
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private Vector2 ownedScroll;
        private Vector2 itemScroll;
        private readonly UIDirectScroll listDirectScroll = new UIDirectScroll();
        private readonly UIDirectScroll detailDirectScroll = new UIDirectScroll();
        private readonly UIDirectScroll ownedDirectScroll = new UIDirectScroll();
        private readonly UIDirectScroll itemDirectScroll = new UIDirectScroll();
        private int selectedIndex = -1;
        private int currentTab;
        private bool detailModalOpen;

        // 마지막으로 그린 도감 목록의 기하 정보. 키보드/이전·다음 선택 시 선택 카드가 화면에 남게 한다.
        private float lastListViewportHeight = 1f;
        private float lastListCardHeight = 128f;
        private float lastListGap = 10f;
        private int lastListColumns = 1;

        // === OnGUI GUIStyle 캐시 (옛은 매 OnGUI 31개 new GUIStyle 회귀) ===
        // 매 프레임 호출되는 영역 21개 + 조건부 호출 10개 = 총 31개 1회 초기화 후 재사용.
        // 동적 textColor/fontStyle은 매 호출 시 갱신.
        private GUIStyle titleStyleCache, countStyleCache, tabStyleCache, closeStyleCache;
        private GUIStyle numStyleCache, missingQStyleCache, listNameStyleCache, listSubStyleCache, checkStyleCache, eyeStyleCache;
        private GUIStyle detailQStyleCache, detailNameStyleCache, raritySCache, caughtSCache, seenSCache, unkSCache;
        private GUIStyle labelSCache, valSCache, hintSCache, habitatLabelCache, habitatValCache, descSCache;
        private GUIStyle ownedNameCache, ownedInfoCache, ownedStCache, ownedGrCache, ownedPctCache;
        private GUIStyle headerSCache, itemNameCache, itemDescCache, itemCountCache;
        private GUIStyle centeredCache, navButtonStyleCache, elementBadgeStyleCache;
        private GUIStyle cardNumberStyleCache, cardNameStyleCache, cardMetaStyleCache, cardStatusStyleCache;
        private GUIStyle listHeaderStyleCache, scrollHintStyleCache;
        private bool dexStylesInitialized;

        // 도감 팔레트 — 전부 UITheme 토큰에서 파생한다.
        //
        // 옛 버전은 밝은 크림/코랄 파스텔 47색을 여기에 직접 박아두었다. 그 결과 도감만
        // 다른 앱처럼 보였다(나머지 화면은 전부 다크 네이비). 색을 토큰에서 끌어오면
        // 테마를 한 곳에서 돌릴 수 있고, 도감의 따뜻한 액센트(코랄/앰버/민트)는
        // UITheme으로 승격돼 이제 다른 화면도 같은 액센트를 쓴다.
        private static UITheme T => UITheme.Instance;

        private static Color DexBgColor => T.surfaceBase;
        private static Color TopBarBg => T.accentCoral;
        private static Color TitleCol => Color.white;
        private static Color CountCol => new Color(1f, 0.94f, 0.88f);
        private static Color TabActiveBg => Color.Lerp(T.accentCoral, Color.white, 0.86f);
        private static Color TabInactiveBg => Color.Lerp(T.accentCoral, Color.black, 0.32f);
        private static Color TitleLineCol => new Color(T.accentAmber.r, T.accentAmber.g, T.accentAmber.b, 0.9f);
        private static Color ListBg => T.surfaceCard;
        private static Color SelectedRowBg => Color.Lerp(T.surfaceRaised, T.accentAmber, 0.24f);
        private static Color RowBgEven => T.surfaceCard;
        private static Color RowBgOdd => Color.Lerp(T.surfaceCard, T.surfaceRaised, 0.55f);
        private static Color NumCol => T.textMuted;
        private static Color UnknownQCol => T.textMuted;
        private static Color CaughtNameCol => T.textPrimary;
        private static Color SubCol => T.textSecondary;
        private static Color SubMissingCol => T.textMuted;
        private static Color CheckBg => T.accentMint;
        private static Color EyeBg => T.accentAmber;
        private static Color DetailBg => T.surfaceCard;
        private static Color DetailUnknownBg => T.surfaceRaised;
        private static Color DetailUnknownQ => T.textMuted;
        private static Color SeenLabelCol => T.accentAmber;
        private static Color CaughtLabelCol => T.accentMint;
        private static Color UnkCol => T.textMuted;
        private static Color InfoBoxBg => T.surfaceRaised;
        private static Color LabelCol => T.textSecondary;
        private static Color HintCol => T.textMuted;
        private static Color HabitatLabelCol => T.accentMint;
        private static Color HabitatValCol => Color.Lerp(T.accentMint, Color.white, 0.45f);
        private static Color DescBg => T.surfaceRaised;
        private static Color DescCol => T.textSecondary;
        private static Color OwnedBg => T.surfaceCard;
        private static Color InfoCol => T.textSecondary;
        private static Color StCol => T.textSecondary;
        private static Color HeaderCol => T.accentAmber;
        private static Color ItemDescCol => T.textSecondary;
        private static Color ItemCountGood => T.accentAmber;
        private static Color ItemCountBad => T.textMuted;
        private static Color CenteredCol => T.textSecondary;
        private static Color NoSelectionCol => T.textMuted;
        private static Color CaughtBgAlpha => new Color(T.accentMint.r, T.accentMint.g, T.accentMint.b, 0.22f);
        private static Color SeenBgAlpha => new Color(T.accentAmber.r, T.accentAmber.g, T.accentAmber.b, 0.20f);
        private static Color CoralDark => Color.Lerp(T.accentCoral, Color.black, 0.42f);
        private static Color LilacPanel => T.surfaceRaised;
        private static Color CardBorderCol => T.surfaceBorder;
        private static Color NavButtonBg => Color.Lerp(T.surfaceRaised, T.accentCoral, 0.18f);

        private void InitDexStyles()
        {
            if (dexStylesInitialized) return;
            dexStylesInitialized = true;

            titleStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold };
            titleStyleCache.normal.textColor = TitleCol;

            countStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            countStyleCache.normal.textColor = CountCol;

            tabStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter
            };

            closeStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            closeStyleCache.normal.textColor = Color.white;

            numStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 28 };
            numStyleCache.normal.textColor = NumCol;

            missingQStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            missingQStyleCache.normal.textColor = UnknownQCol;

            // 이름은 폭 제약(260px에 8글자 곤충명 존재)으로 36 유지 — 확대 시 잘림. 서브(등급 라벨)는 폭 여유로 확대.
            listNameStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold };
            listSubStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 32 };

            checkStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            checkStyleCache.normal.textColor = Color.white;

            eyeStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            eyeStyleCache.normal.textColor = Color.white;

            detailQStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            detailQStyleCache.normal.textColor = DetailUnknownQ;

            detailNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            raritySCache = new GUIStyle(GUI.skin.label) { fontSize = 38, alignment = TextAnchor.MiddleCenter };

            caughtSCache = new GUIStyle(GUI.skin.label)
            { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            caughtSCache.normal.textColor = CaughtLabelCol;

            seenSCache = new GUIStyle(GUI.skin.label)
            { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            seenSCache.normal.textColor = SeenLabelCol;

            unkSCache = new GUIStyle(GUI.skin.label) { fontSize = 38, alignment = TextAnchor.MiddleCenter };
            unkSCache.normal.textColor = UnkCol;

            labelSCache = new GUIStyle(GUI.skin.label) { fontSize = 34 };
            labelSCache.normal.textColor = LabelCol;

            valSCache = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            valSCache.normal.textColor = T.textPrimary;

            hintSCache = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleCenter };
            hintSCache.normal.textColor = HintCol;

            habitatLabelCache = new GUIStyle(GUI.skin.label) { fontSize = 32 };
            habitatLabelCache.normal.textColor = HabitatLabelCol;

            habitatValCache = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, wordWrap = true };
            habitatValCache.normal.textColor = HabitatValCol;

            descSCache = new GUIStyle(GUI.skin.label) { fontSize = 34, wordWrap = true };
            descSCache.normal.textColor = DescCol;

            ownedNameCache = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold };
            ownedInfoCache = new GUIStyle(GUI.skin.label) { fontSize = 34 };
            ownedInfoCache.normal.textColor = InfoCol;
            ownedStCache = new GUIStyle(GUI.skin.label) { fontSize = 34 };
            ownedStCache.normal.textColor = StCol;
            ownedGrCache = new GUIStyle(GUI.skin.label)
            { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            ownedPctCache = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };

            headerSCache = new GUIStyle(GUI.skin.label)
            { fontSize = 50, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            headerSCache.normal.textColor = HeaderCol;

            itemNameCache = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold };
            itemDescCache = new GUIStyle(GUI.skin.label) { fontSize = 36 };
            itemDescCache.normal.textColor = ItemDescCol;
            itemCountCache = new GUIStyle(GUI.skin.label)
            { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };

            centeredCache = new GUIStyle(GUI.skin.label)
            { fontSize = 42, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            centeredCache.normal.textColor = CenteredCol;

            navButtonStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            navButtonStyleCache.normal.textColor = T.textPrimary;

            elementBadgeStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };

            cardNumberStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            cardNumberStyleCache.normal.textColor = NumCol;

            // 타일이 세로형으로 바뀌면서 이름·등급은 가운데 정렬이 맞다(옛 가로형 행은 왼쪽).
            cardNameStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };

            cardMetaStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            cardMetaStyleCache.normal.textColor = SubCol;

            cardStatusStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            cardStatusStyleCache.normal.textColor = Color.white;

            listHeaderStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            listHeaderStyleCache.normal.textColor = T.accentMint;

            scrollHintStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            scrollHintStyleCache.normal.textColor = T.textMuted;
        }

        // 아래 4개는 이제 UISurface(공용 서피스)로 위임한다. 원래 이 파일이 원본이었고,
        // 다른 30개 화면은 각진 사각형만 쓰고 있었다 — 승격해서 전 화면이 같은 표면을 쓴다.
        // 호출부가 많아 래퍼는 남긴다.

        private void DrawRoundedRect(Rect rect, Color color) => UISurface.Rounded(rect, color);

        private void DrawRoundedCard(Rect rect, Color background, Color border)
            => UISurface.Card(rect, background, border);

        private bool DrawCuteButton(Rect rect, string label, Color background, GUIStyle style, bool selected = false)
            => UISurface.Button(rect, label, background, style, selected);

        private void DrawScrollAffordance(Rect viewport, Vector2 scroll, float contentHeight, Color accent)
            => UISurface.ScrollAffordance(viewport, scroll, contentHeight, accent);

        private void ResetDirectScrollGestures()
        {
            listDirectScroll.Reset();
            detailDirectScroll.Reset();
            ownedDirectScroll.Reset();
            itemDirectScroll.Reset();
        }

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            if (isOpen)
            {
                listScroll = Vector2.zero;
                detailScroll = Vector2.zero;
                ownedScroll = Vector2.zero;
                itemScroll = Vector2.zero;
                detailModalOpen = false;
                if (database != null && database.insects.Count > 0
                    && (selectedIndex < 0 || selectedIndex >= database.insects.Count))
                {
                    selectedIndex = 0;
                }
                ResetDirectScrollGestures();
                ModalUIRegistry.Register(this);
                if (TutorialQuestManager.Instance != null)
                    TutorialQuestManager.Instance.NotifyDexOpened();
            }
            else
            {
                ResetDirectScrollGestures();
                ModalUIRegistry.Unregister(this);
            }
        }
        public void CloseModal()
        {
            isOpen = false;
            detailModalOpen = false;
            ResetDirectScrollGestures();
            ModalUIRegistry.Unregister(this);
        }
        private void OnDisable()
        {
            isOpen = false;
            detailModalOpen = false;
            ResetDirectScrollGestures();
            ModalUIRegistry.Unregister(this);
        }

        private readonly string[] tabNames = { "곤충 도감", "보유 곤충", "아이템" };

        private void SelectIndex(int index, bool openDetail)
        {
            if (database == null || database.insects == null || database.insects.Count == 0)
            {
                selectedIndex = -1;
                return;
            }

            int clamped = Mathf.Clamp(index, 0, database.insects.Count - 1);
            if (selectedIndex != clamped)
            {
                selectedIndex = clamped;
                detailScroll = Vector2.zero;
                detailDirectScroll.Reset();
                previewShiny = false;
            }

            // 상세는 이제 가로·세로 모두 중앙 모달이다 — 방향 조건이 없다.
            if (openDetail)
            {
                detailModalOpen = true;
            }

            ScrollToSelected();
        }

        private void SelectRelative(int delta, bool keepDetailOpen = true)
        {
            if (database == null || database.insects == null)
            {
                return;
            }

            int next = DexBrowseLayout.WrapIndex(selectedIndex, delta, database.insects.Count);
            if (next < 0)
            {
                return;
            }

            SelectIndex(next, keepDetailOpen && detailModalOpen);
        }

        private void ScrollToSelected()
        {
            if (selectedIndex < 0)
            {
                return;
            }

            int row = selectedIndex / Mathf.Max(1, lastListColumns);
            float stride = lastListCardHeight + lastListGap;
            float targetY = row * stride;
            float viewH = Mathf.Max(1f, lastListViewportHeight);
            if (targetY < listScroll.y) listScroll.y = targetY;
            else if (targetY + lastListCardHeight > listScroll.y + viewH)
                listScroll.y = targetY + lastListCardHeight - viewH;
        }

        private void OnGUI()
        {
            if (isOpen)
            {
                Event evt = Event.current;
                if (evt != null && evt.type == EventType.KeyDown)
                {
                    if (evt.keyCode == KeyCode.Escape)
                    {
                        if (currentTab == 0 && detailModalOpen)
                        {
                            detailModalOpen = false;
                            ScrollToSelected();
                        }
                        else
                        {
                            CloseModal();
                        }
                        evt.Use();
                        return;
                    }
                    if (currentTab == 0 && database != null)
                    {
                        // ↑↓는 **한 행**만큼 움직인다. 좌측 1열 리스트였을 땐 ±1이 곧 한 행이었지만
                        // 전체 폭 그리드로 바뀐 뒤로는 ±1이 옆 칸이라, 6열에서 ↓를 여섯 번 눌러야
                        // 한 줄 내려가고 그동안 스크롤(행 기준)은 제자리였다.
                        int rowStep = Mathf.Max(1, lastListColumns);
                        if (evt.keyCode == KeyCode.UpArrow)
                        {
                            SelectRelative(-rowStep);
                            evt.Use();
                        }
                        if (evt.keyCode == KeyCode.DownArrow)
                        {
                            SelectRelative(rowStep);
                            evt.Use();
                        }
                        if (evt.keyCode == KeyCode.LeftArrow && detailModalOpen)
                        {
                            SelectRelative(-1);
                            evt.Use();
                        }
                        if (evt.keyCode == KeyCode.RightArrow && detailModalOpen)
                        {
                            SelectRelative(1);
                            evt.Use();
                        }
                        if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                            && !detailModalOpen && selectedIndex >= 0)
                        {
                            detailModalOpen = true;
                            detailScroll = Vector2.zero;
                            detailDirectScroll.Reset();
                            evt.Use();
                        }
                    }
                }
            }

            if (!isOpen || database == null || dexController == null) return;

            InitDexStyles();
            GUI.depth = -10;
            UIScale.Begin();

            GUI.color = DexBgColor;
            GUI.DrawTexture(new Rect(0, 0, UIScale.VirtualScreenWidth, UIScale.VirtualScreenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 세이프에어리어 + 세로 마진을 뺀 전체 콘텐츠 영역(도감은 전체화면이라 여기가 곧 패널).
            Rect safeRect = UISafeLayout.Content;
            float topBarH = DrawTopBar(safeRect);
            Rect contentRect = new Rect(
                safeRect.x + 10f,
                safeRect.y + topBarH + 10f,
                safeRect.width - 20f,
                Mathf.Max(1f, safeRect.height - topBarH - 20f));

            if (currentTab == 0)
                DrawPokedex(contentRect);
            else if (currentTab == 1)
                DrawOwnedInsects(contentRect);
            else if (currentTab == 2)
                DrawItems(contentRect);

            UIScale.End();
        }

        private float DrawTopBar(Rect safeRect)
        {
            bool mobile = UIScale.IsMobileLayout;
            float topBarH = mobile ? 174f : 114f;
            Rect topRect = new Rect(safeRect.x, safeRect.y, safeRect.width, topBarH);
            DrawRoundedRect(topRect, TopBarBg);

            int total = database.insects.Count;
            int discovered = 0, captured = 0;
            foreach (var ins in database.insects)
            {
                if (ins == null) continue;
                if (dexController.IsDiscovered(ins.insectId)) discovered++;
                if (dexController.HasRecord(ins.insectId)) captured++;
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(safeRect.x + 24f, safeRect.y + 8f, mobile ? 410f : 500f, 60f),
                "반짝 곤충 도감", titleStyleCache);
            GUI.Label(new Rect(safeRect.x + (mobile ? 420f : 390f), safeRect.y + 16f,
                    Mathf.Max(180f, safeRect.width - (mobile ? 610f : 600f)), 48f),
                $"발견 {discovered}/{total}  ·  포획 {captured}/{total}", countStyleCache);

            float tabW = mobile ? (safeRect.width - 40f) / tabNames.Length : 230f;
            float tabGap = mobile ? 0f : 10f;
            float tabX = mobile ? safeRect.x + 20f : safeRect.center.x - tabNames.Length * 120f;
            float tabY = safeRect.y + (mobile ? 94f : 66f);
            float tabH = mobile ? 68f : 44f;
            for (int i = 0; i < tabNames.Length; i++)
            {
                bool active = currentTab == i;
                tabStyleCache.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                tabStyleCache.normal.textColor = active ? CoralDark : Color.white;
                if (DrawCuteButton(
                    new Rect(tabX + i * (tabW + tabGap), tabY, tabW, tabH),
                    tabNames[i],
                    active ? TabActiveBg : TabInactiveBg,
                    tabStyleCache,
                    active))
                {
                    currentTab = i;
                    detailModalOpen = false;
                    ResetDirectScrollGestures();
                }
            }

            if (DrawCuteButton(
                new Rect(safeRect.xMax - 176f, safeRect.y + 8f, 164f, mobile ? 68f : 50f),
                mobile ? "× 닫기" : "× 닫기 [N]",
                CoralDark,
                closeStyleCache))
                CloseModal();

            GUI.color = TitleLineCol;
            GUI.DrawTexture(new Rect(safeRect.x + 16f, safeRect.y + topBarH - 4f, safeRect.width - 32f, 4f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            return topBarH;
        }

        // 타일 한 칸의 목표 폭. 실제 폭은 열 수로 나눈 값이라 이보다 커질 수 있다.
        private const float TargetTileWidth = 260f;
        private const float TileGap = 14f;

        // 보유 탭 타일 — 개체 정보를 담느라 도감 타일보다 넓다.
        // 397은 옛 `floor(panelW / 410f)`가 2열로 넘어가던 지점의 실제 카드 폭이다.
        // 그 값을 쓰면 공식을 GetGridColumns로 바꿔도 열 수가 그대로 나온다.
        private const float OwnedTileWidth = 397f;
        private const float OwnedTileGap = 12f;
        private const int OwnedMaxColumns = 3;

        private void DrawPokedex(Rect contentRect)
        {
            // 좌우 분할(좌 34% 목록 + 우 상세)을 버리고 전체 폭 그리드 하나만 그린다.
            // 상세는 그 위에 중앙 모달로 겹친다 — 가로/세로가 같은 경로를 탄다.
            DrawDexGrid(contentRect);

            if (detailModalOpen
                && selectedIndex >= 0
                && selectedIndex < database.insects.Count
                && database.insects[selectedIndex] != null)
            {
                DrawDetailModal(database.insects[selectedIndex]);
            }
        }

        private void DrawDexGrid(Rect panelRect)
        {
            DrawRoundedCard(panelRect, ListBg, CardBorderCol);

            int count = database.insects.Count;
            int discovered = 0;
            for (int i = 0; i < count; i++)
            {
                InsectData ins = database.insects[i];
                if (ins != null && dexController.IsDiscovered(ins.insectId)) discovered++;
            }

            GUI.Label(new Rect(panelRect.x + 22f, panelRect.y + 10f, panelRect.width - 44f, 42f),
                $"탐험 기록  ·  {discovered} / {count}종", listHeaderStyleCache);
            GUI.Label(new Rect(panelRect.x + 22f, panelRect.y + 46f, panelRect.width - 44f, 28f),
                "카드를 눌러 상세 보기  ·  ↑↓ 선택", scrollHintStyleCache);

            Rect viewport = new Rect(
                panelRect.x + 12f,
                panelRect.y + 76f,
                panelRect.width - 24f,
                Mathf.Max(1f, panelRect.height - 88f));
            float contentWidth = Mathf.Max(1f, viewport.width - 14f);
            float gap = TileGap;
            int columns = DexBrowseLayout.GetGridColumns(contentWidth, TargetTileWidth, gap);
            float cardWidth = (contentWidth - (columns - 1) * gap) / columns;
            // 정사각에 가까운 타일 — 아이콘을 크게 두고 그 아래 이름·등급·속성을 쌓는다.
            float cardHeight = cardWidth * 1.04f;
            float totalHeight = DexBrowseLayout.GetGridContentHeight(count, columns, cardHeight, gap);

            lastListViewportHeight = viewport.height;
            lastListCardHeight = cardHeight;
            lastListGap = gap;
            lastListColumns = columns;

            // 상세 모달이 이 그리드 **위에** 겹치는데 그리드가 먼저 그려진다 → Handle도 먼저 불린다.
            // 모달이 열린 동안 그리드가 입력을 받으면 모달 위에서 굴린 휠·드래그를 그리드가
            // 가로채(터치에서는 배경과 모달이 같은 손가락으로 함께 움직인다) 상세가 스크롤되지 않는다.
            listDirectScroll.Handle(ref listScroll, viewport, totalHeight, cardHeight * 0.34f, !detailModalOpen);
            listScroll = GUI.BeginScrollView(
                viewport,
                listScroll,
                new Rect(0f, 0f, contentWidth, Mathf.Max(viewport.height, totalHeight)),
                GUIStyle.none,
                GUIStyle.none);

            for (int i = 0; i < count; i++)
            {
                InsectData insect = database.insects[i];
                if (insect == null)
                {
                    continue;
                }

                int column = i % columns;
                int row = i / columns;
                Rect cardRect = new Rect(
                    column * (cardWidth + gap),
                    row * (cardHeight + gap),
                    cardWidth,
                    cardHeight);
                DrawDexTile(cardRect, insect, i);
            }

            GUI.EndScrollView();
            DrawScrollAffordance(viewport, listScroll, totalHeight, T.accentCoral);
        }

        /// <summary>
        /// 그리드 한 칸 — 아이콘(위, 크게) → 이름 → 등급 → 속성 순의 세로 타일.
        /// 좌우 분할을 없앤 뒤로 목록이 화면 전체를 쓰므로 가로형 행 대신 타일을 쓴다.
        /// 좌표는 전부 타일 크기 비율에서 파생 — 열 수가 2~6으로 변해도 무너지지 않는다.
        /// </summary>
        private void DrawDexTile(Rect rect, InsectData insect, int index)
        {
            bool found = dexController.IsDiscovered(insect.insectId);
            bool caught = dexController.HasRecord(insect.insectId);
            bool selected = index == selectedIndex;
            Color rarityColor = UITheme.Instance.GetInsectRarityColor(insect.rarity);

            Color cardBackground = selected ? SelectedRowBg : RowBgEven;
            Color border = selected
                ? TopBarBg
                : found ? Color.Lerp(rarityColor, CardBorderCol, 0.45f) : CardBorderCol;
            DrawRoundedCard(rect, cardBackground, border);

            float w = rect.width;
            float h = rect.height;

            // 도감 번호(좌상) · 포획 상태(우상)
            GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, w * 0.5f, h * 0.1f),
                $"NO. {index + 1:D3}", cardNumberStyleCache);

            float statusW = Mathf.Min(84f, w * 0.34f);
            float statusH = Mathf.Max(28f, h * 0.1f);
            Rect statusRect = new Rect(rect.xMax - statusW - 12f, rect.y + 8f, statusW, statusH);
            DrawRoundedRect(statusRect, caught ? CheckBg : found ? EyeBg : DetailUnknownBg);
            cardStatusStyleCache.normal.textColor = found ? Color.black : UnknownQCol;
            GUI.Label(statusRect, caught ? "포획" : found ? "발견" : "미발견", cardStatusStyleCache);

            // 아이콘 — 타일 중앙 상단의 정사각 패널
            float iconSize = w * 0.44f;
            Rect iconPanel = new Rect(rect.center.x - iconSize * 0.5f, rect.y + h * 0.13f, iconSize, iconSize);
            DrawRoundedRect(iconPanel, found ? Color.Lerp(DetailUnknownBg, rarityColor, 0.16f) : DetailUnknownBg);
            if (found)
            {
                Color insectColor = caught
                    ? UITheme.Instance.GetInsectColor(insect.insectId, insect.rarity)
                    : Color.Lerp(rarityColor, T.textMuted, 0.68f);
                DrawTinyInsect(iconPanel.center.x, iconPanel.center.y, iconSize * 0.44f, insect.insectId, insectColor);
            }
            else
            {
                GUI.Label(iconPanel, "?", detailQStyleCache);
            }

            float textX = rect.x + 8f;
            float textW = w - 16f;

            cardNameStyleCache.normal.textColor = found
                ? caught ? rarityColor : CaughtNameCol
                : UnknownQCol;
            GUI.Label(new Rect(textX, rect.y + h * 0.60f, textW, h * 0.14f),
                found ? insect.displayName : "???", cardNameStyleCache);

            cardMetaStyleCache.normal.textColor = found ? SubCol : SubMissingCol;
            GUI.Label(new Rect(textX, rect.y + h * 0.745f, textW, h * 0.1f),
                found ? GetRarityLabel(insect.rarity) : "미발견", cardMetaStyleCache);

            if (found)
            {
                DrawElementBadges(
                    new Rect(textX, rect.y + h * 0.85f, textW, h * 0.12f),
                    insect.primaryType,
                    insect.secondaryType,
                    true);
            }

            if (GUI.Button(rect, string.Empty, GUIStyle.none) && !listDirectScroll.IsDragging)
            {
                // 타일을 누르면 항상 상세 모달을 연다(가로·세로 동일).
                SelectIndex(index, true);
            }
        }

        /// <summary>
        /// 곤충 상세 — 그리드 위에 겹치는 중앙 모달. 배경은 딤 처리하고 딤을 누르면 닫힌다.
        /// 패널 Rect는 <see cref="UISafeLayout"/> 경유라 노치·세로 마진이 이미 빠져 있다.
        /// </summary>
        private void DrawDetailModal(InsectData insect)
        {
            UISurface.Dim(0.74f);

            float panelW = Mathf.Min(1180f, UIScale.VirtualScreenWidth - 64f);
            float panelH = UISafeLayout.ClampHeight(UIScale.IsPortrait ? 1500f : 920f);
            Rect panelRect = UISafeLayout.CenteredPanel(panelW, panelH);

            // 딤 클릭 → 닫기. 패널 자체에도 투명 버튼을 겹쳐 깔아(아래 흡수 버튼)
            // 패널 빈 곳 클릭이 여기까지 새지 않게 한다. IMGUI는 나중에 그린 쪽이 이긴다.
            if (GUI.Button(
                new Rect(0f, 0f, UIScale.VirtualScreenWidth, UIScale.VirtualScreenHeight),
                string.Empty, GUIStyle.none))
            {
                detailModalOpen = false;
                ScrollToSelected();
                return;
            }

            DrawRoundedCard(panelRect, DetailBg, CardBorderCol);
            GUI.Button(panelRect, string.Empty, GUIStyle.none);   // 클릭 흡수 (반환값 의도적 무시)

            float navHeight = 74f;
            float navBtnH = Mathf.Max(UIScale.MinTouchHeight, 58f);
            float navY = panelRect.y + 10f;

            Rect closeButton = new Rect(panelRect.x + 12f, navY, 176f, navBtnH);
            if (DrawCuteButton(closeButton, "× 닫기", CoralDark, navButtonStyleCache))
            {
                detailModalOpen = false;
                ScrollToSelected();
                return;
            }

            float navButtonWidth = Mathf.Min(154f, panelRect.width * 0.2f);
            Rect nextButton = new Rect(panelRect.xMax - navButtonWidth - 12f, navY, navButtonWidth, navBtnH);
            Rect previousButton = new Rect(nextButton.x - navButtonWidth - 10f, navY, navButtonWidth, navBtnH);
            if (DrawCuteButton(previousButton, "‹ 이전", NavButtonBg, navButtonStyleCache))
            {
                SelectRelative(-1);
            }
            if (DrawCuteButton(nextButton, "다음 ›", NavButtonBg, navButtonStyleCache))
            {
                SelectRelative(1);
            }

            GUI.Label(
                new Rect(closeButton.xMax + 8f, navY + 10f,
                    Mathf.Max(1f, previousButton.x - closeButton.xMax - 16f), 38f),
                $"{selectedIndex + 1:D3} / {database.insects.Count:D3}",
                scrollHintStyleCache);

            Rect viewport = new Rect(
                panelRect.x + 10f,
                panelRect.y + navHeight + 10f,
                panelRect.width - 20f,
                Mathf.Max(1f, panelRect.height - navHeight - 22f));
            bool found = dexController.IsDiscovered(insect.insectId);
            bool caught = dexController.HasRecord(insect.insectId);
            float contentHeight = GetDetailContentHeight(insect, found, caught);
            float contentWidth = Mathf.Max(1f, viewport.width - 14f);

            detailDirectScroll.Handle(ref detailScroll, viewport, contentHeight, 64f);
            detailScroll = GUI.BeginScrollView(
                viewport,
                detailScroll,
                new Rect(0f, 0f, contentWidth, Mathf.Max(viewport.height, contentHeight)),
                GUIStyle.none,
                GUIStyle.none);
            DrawDetail(0f, 0f, contentWidth, contentHeight, insect);
            GUI.EndScrollView();

            DrawScrollAffordance(viewport, detailScroll, contentHeight, T.accentCoral);
        }

        private float GetDetailContentHeight(InsectData insect, bool found, bool caught)
        {
            if (!found)
            {
                return 430f;
            }

            float height = caught ? 1110f : 830f;
            if (string.IsNullOrEmpty(insect.habitatHint))
            {
                height -= 100f;
            }
            if (caught && string.IsNullOrEmpty(insect.description))
            {
                height -= 175f;
            }
            return height;
        }

        private string GetRarityLabel(InsectRarity rarity)
        {
            switch (rarity)
            {
                case InsectRarity.Uncommon: return "고급";
                case InsectRarity.Rare: return "희귀";
                case InsectRarity.Epic: return "영웅";
                case InsectRarity.Legendary: return "전설";
                default: return "일반";
            }
        }

        private void DrawElementBadges(
            Rect area,
            InsectElement primary,
            InsectElement secondary,
            bool compact)
        {
            bool showSecondary = DexBrowseLayout.ShouldShowSecondary(primary, secondary);
            float primaryWidth = GetElementBadgeWidth(primary, compact);
            float secondaryWidth = showSecondary ? GetElementBadgeWidth(secondary, compact) : 0f;
            float gap = showSecondary ? 8f : 0f;
            float totalWidth = primaryWidth + secondaryWidth + gap;
            float startX = area.x + Mathf.Max(0f, (area.width - totalWidth) * 0.5f);
            float badgeHeight = compact ? 34f : 46f;
            float badgeY = area.y + Mathf.Max(0f, (area.height - badgeHeight) * 0.5f);

            DrawElementBadge(new Rect(startX, badgeY, primaryWidth, badgeHeight), primary, compact);
            if (showSecondary)
            {
                DrawElementBadge(
                    new Rect(startX + primaryWidth + gap, badgeY, secondaryWidth, badgeHeight),
                    secondary,
                    compact);
            }
        }

        private float GetElementBadgeWidth(InsectElement element, bool compact)
        {
            int length = InsectTypeChart.GetDisplayName(element).Length;
            return compact
                ? Mathf.Clamp(70f + length * 12f, 92f, 126f)
                : Mathf.Clamp(92f + length * 16f, 126f, 176f);
        }

        private void DrawElementBadge(Rect rect, InsectElement element, bool compact)
        {
            Color color = GetElementColor(element);
            DrawRoundedRect(rect, color);
            elementBadgeStyleCache.fontSize = compact ? 22 : 28;
            float luminance = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            elementBadgeStyleCache.normal.textColor = luminance > 0.72f
                ? new Color(0.2f, 0.25f, 0.28f)
                : Color.white;
            GUI.Label(rect, $"◆ {InsectTypeChart.GetDisplayName(element)}", elementBadgeStyleCache);
        }

        private Color GetElementColor(InsectElement element)
        {
            switch (element)
            {
                case InsectElement.Bug: return new Color(0.55f, 0.72f, 0.2f);
                case InsectElement.Leaf: return new Color(0.25f, 0.72f, 0.38f);
                case InsectElement.Water: return new Color(0.28f, 0.62f, 0.95f);
                case InsectElement.Wind: return new Color(0.35f, 0.78f, 0.72f);
                case InsectElement.Electric: return new Color(1f, 0.78f, 0.18f);
                case InsectElement.Earth: return new Color(0.68f, 0.48f, 0.28f);
                case InsectElement.Poison: return new Color(0.68f, 0.38f, 0.82f);
                case InsectElement.Light: return new Color(1f, 0.74f, 0.36f);
                case InsectElement.Dark: return new Color(0.34f, 0.28f, 0.55f);
                case InsectElement.Metal: return new Color(0.48f, 0.58f, 0.66f);
                default: return new Color(0.58f, 0.62f, 0.64f);
            }
        }

        private void DrawDetail(float x, float y, float w, float h, InsectData ins)
        {
            bool found = dexController.IsDiscovered(ins.insectId);
            bool caught = dexController.HasRecord(ins.insectId);
            DexRecord record = null;
            dexController.TryGetRecord(ins.insectId, out record);

            DrawRoundedRect(new Rect(x, y, w, h), T.surfaceCard);

            float cx = x + w / 2f;
            float py = y + 20;

            Color rarityCol = UITheme.Instance.GetInsectRarityColor(ins.rarity);

            if (found && caught && previewRenderer != null)
            {
                // 곤충 3D 프리뷰를 좌측에 크게(세로 가득), 텍스트는 우측 컬럼으로 재배치.
                // 가로 공간(detailW ~1400px)이 대부분 낭비되던 것을 활용 → 이미지 대형화, 폰트 크기는 불변.
                float rightEdge = x + w;
                // 절대 상한(380) 추가 — w는 상세 패널 전체 폭(~1400px)이라 비율만으론 패널 따라 계속 커짐.
                // 2-인자 Min 중첩(3-인자는 params float[] 할당 — OnGUI 핫패스 회피).
                float previewSz = Mathf.Min(Mathf.Min(h - 30f, w * 0.42f), 380f);
                float boxX = x + 10f;
                float boxY = y + 20f;

                Color portBg = rarityCol;
                portBg.r *= 0.1f; portBg.g *= 0.1f; portBg.b *= 0.1f; portBg.a = 0.5f;
                portBg = Color.Lerp(portBg, T.surfaceRaised, 0.74f);
                DrawRoundedCard(
                    new Rect(boxX, boxY, previewSz, previewSz),
                    portBg,
                    Color.Lerp(rarityCol, Color.white, 0.34f));

                bool ownsShiny = OwnsShiny(ins.insectId);
                Texture preview = previewRenderer.GetPreview(ins, previewAngle, previewShiny);
                if (preview != null)
                    GUI.DrawTexture(new Rect(boxX, boxY, previewSz, previewSz), preview, ScaleMode.ScaleToFit, true);
                else
                {
                    Color ic = UITheme.Instance.GetInsectColor(ins.insectId, ins.rarity);
                    DrawTinyInsect(boxX + previewSz / 2f, boxY + previewSz / 2f, previewSz * 0.6f, ins.insectId, ic);
                }
                // 좌우 회전 버튼 — 박스 하단 양끝
                float rotBtnY = boxY + previewSz - 68f;
                if (DrawCuteButton(
                    new Rect(boxX + 8f, rotBtnY, 60f, 60f),
                    "◀",
                    NavButtonBg,
                    navButtonStyleCache))
                    previewAngle -= 30f;
                if (DrawCuteButton(
                    new Rect(boxX + previewSz - 68f, rotBtnY, 60f, 60f),
                    "▶",
                    NavButtonBg,
                    navButtonStyleCache))
                    previewAngle += 30f;
                // 이로치(색다른 모습) 토글 — 박스 상단 중앙
                float variantButtonH = Mathf.Max(UIScale.MinTouchHeight, UIScale.IsMobileLayout ? 62f : 56f);
                if (DrawCuteButton(
                    new Rect(boxX + previewSz / 2f - 110f, boxY + 8f, 220f, variantButtonH),
                    previewShiny ? "★ 색다른 모습" : "✦ 모습 바꾸기",
                    previewShiny ? new Color(1f, 0.82f, 0.24f) : LilacPanel,
                    navButtonStyleCache,
                    previewShiny))
                    previewShiny = !previewShiny;
                if (ownsShiny)
                    GUI.Label(new Rect(boxX, boxY + previewSz - 112f, previewSz, 30f), "★ 색다른 개체 보유 중", caughtSCache);

                // 이후 모든 텍스트는 우측 컬럼에 그림 — x/w/cx/py만 재배치(폰트 스타일 불변).
                x = boxX + previewSz + 30f;
                w = rightEdge - x - 10f;
                cx = x + w / 2f;
                py = y + 20f;
            }
            else if (found)
            {
                // 발견만(미포획): 3D 모델 미공개 → 기존 중앙 약식 실루엣 유지.
                float portraitSize = 110f;
                Color portBg = rarityCol;
                portBg.r *= 0.1f; portBg.g *= 0.1f; portBg.b *= 0.1f; portBg.a = 0.5f;
                portBg = Color.Lerp(portBg, T.surfaceRaised, 0.74f);
                DrawRoundedCard(
                    new Rect(cx - portraitSize, py, portraitSize * 2f, portraitSize * 2f),
                    portBg,
                    Color.Lerp(rarityCol, Color.white, 0.45f));
                DrawTinyInsect(cx, py + portraitSize, portraitSize * 1.2f, ins.insectId, new Color(0.3f, 0.3f, 0.35f));
                py += portraitSize * 2 + 12;
            }
            else
            {
                DrawRoundedCard(
                    new Rect(cx - 70, py, 140, 140),
                    DetailUnknownBg,
                    CardBorderCol);
                GUI.Label(new Rect(cx - 70, py, 140, 140), "?", detailQStyleCache);
                py += 158;
            }

            if (found)
            {
                detailNameStyleCache.normal.textColor = caught ? rarityCol : CaughtNameCol;
                GUI.Label(new Rect(x, py, w, 66), ins.displayName, detailNameStyleCache);
            }
            else
            {
                detailNameStyleCache.normal.textColor = SubMissingCol;
                GUI.Label(new Rect(x, py, w, 66), "???", detailNameStyleCache);
            }
            py += 70;

            if (found)
            {
                raritySCache.normal.textColor = rarityCol;
                GUI.Label(new Rect(x, py, w, 46), GetRarityLabel(ins.rarity), raritySCache);
                py += 50;

                DrawElementBadges(
                    new Rect(x, py, w, 52f),
                    ins.primaryType,
                    ins.secondaryType,
                    false);
                py += 58f;
            }

            if (caught)
            {
                DrawRoundedRect(new Rect(cx - 120, py, 240, 50), CaughtBgAlpha);
                GUI.Label(new Rect(cx - 120, py, 240, 50), "✓ 포획 완료", caughtSCache);
                py += 56;
            }
            else if (found)
            {
                DrawRoundedRect(new Rect(cx - 120, py, 240, 50), SeenBgAlpha);
                GUI.Label(new Rect(cx - 120, py, 240, 50), "◉ 발견만 됨", seenSCache);
                py += 56;
            }
            else
            {
                GUI.Label(new Rect(x, py, w, 46), "아직 발견하지 못한 곤충입니다", unkSCache);
                py += 50;
                GUI.color = Color.white;
                return;
            }

            float infoBoxX = x + 20;
            float infoBoxW = w - 40;
            DrawRoundedCard(
                new Rect(infoBoxX, py, infoBoxW, caught ? 396 : 120),
                InfoBoxBg,
                CardBorderCol);

            float lx = infoBoxX + 16;
            float lw = infoBoxW - 32;

            if (caught)
            {
                // 행 간격 52px로 넓혀 글자 짤림/빡빡함 해소 (옛 46px).
                DrawInfoRow(lx, py + 12, lw, "HP", $"{ins.baseHp}", labelSCache, valSCache);
                DrawInfoRow(lx, py + 64, lw, "공격력", $"{ins.baseAtk}", labelSCache, valSCache);
                DrawInfoRow(lx, py + 116, lw, "방어력", $"{ins.baseDef}", labelSCache, valSCache);
                DrawInfoRow(lx, py + 168, lw, "레벨 범위", $"{ins.minLevel} ~ {ins.maxLevel}", labelSCache, valSCache);
                DrawInfoRow(lx, py + 220, lw, "포획 난이도",
                    ins.captureDifficulty < 0.3f ? "쉬움" : ins.captureDifficulty < 0.6f ? "보통" : "어려움", labelSCache, valSCache);
                // 종의 표준 크기 — 개체가 아니라 종 도감이므로 pid 없이 기준값을 보여준다.
                // 내 개체가 이보다 큰지 작은지는 보유 목록(CollectionUI)에서 비교한다.
                DrawInfoRow(lx, py + 272, lw, "표준 크기",
                    InsectSizeCalculator.Summary(ins, null), labelSCache, valSCache);

                if (record != null)
                {
                    DrawInfoRow(lx, py + 328, lw, "발견 횟수", $"{record.discoveredCount}회", labelSCache, valSCache);
                    DrawInfoRow(lx, py + 380, lw, "포획 횟수", $"{record.capturedCount}회", labelSCache, valSCache);
                }

                py += 460;
            }
            else
            {
                GUI.Label(new Rect(infoBoxX, py + 10, infoBoxW, 28), "포획하면 상세 스탯을 확인할 수 있습니다", hintSCache);

                if (record != null)
                    DrawInfoRow(lx, py + 56, lw, "발견 횟수", $"{record.discoveredCount}회", labelSCache, valSCache);

                py += 130;
            }

            if (!string.IsNullOrEmpty(ins.habitatHint))
            {
                py += 10;
                GUI.Label(new Rect(infoBoxX + 16, py, 90, 40), "서식지", habitatLabelCache);
                // 값 라벨 높이 확대(40→84) + wordWrap으로 긴 서식지 설명이 여러 줄로 온전히 표시.
                GUI.Label(new Rect(infoBoxX + 110, py, lw - 110, 84), ins.habitatHint, habitatValCache);
                py += 92;
            }

            if (caught && !string.IsNullOrEmpty(ins.description))
            {
                py += 8;
                DrawRoundedCard(
                    new Rect(infoBoxX, py, infoBoxW, 160),
                    DescBg,
                    CardBorderCol);
                // 박스는 고정이고 설명 길이는 종마다 다르다 — 넘치면 폰트를 줄여 맞춘다.
                // (박스를 키우면 아래 요소가 전부 밀린다. 옛날엔 56px에 그려 대놓고 잘렸다.)
                UIHelper.LabelFit(
                    new Rect(infoBoxX + 14, py + 10, infoBoxW - 28, 144), ins.description, descSCache);
            }
        }

        private void DrawInfoRow(float x, float y, float w, string label, string val,
            GUIStyle ls, GUIStyle vs)
        {
            GUI.Label(new Rect(x, y, w * 0.5f, 44), label, ls);
            GUI.Label(new Rect(x + w * 0.5f, y, w * 0.5f, 44), val, vs);
        }

        private void DrawOwnedInsects(Rect contentRect)
        {
            if (insectCollection == null)
            {
                DrawCentered(contentRect, "곤충 컬렉션 데이터 없음");
                return;
            }

            List<PlayerInsectData> owned = insectCollection.GetAllOwned();
            if (owned.Count == 0)
            {
                DrawCentered(contentRect, "아직 포획한 곤충이 없습니다\n필드에서 곤충에 다가가 포획해 보세요!");
                return;
            }

            DrawRoundedCard(contentRect, ListBg, CardBorderCol);
            GUI.Label(new Rect(contentRect.x + 24f, contentRect.y + 12f, contentRect.width - 48f, 52f),
                $"나의 곤충 친구  ·  {owned.Count}마리", headerSCache);

            float panelW = Mathf.Min(contentRect.width - 28f, 1400f);
            float px = contentRect.x + (contentRect.width - panelW) / 2f;
            float gap = OwnedTileGap;
            // 도감 탭과 같은 공식을 탄다. 예전엔 여기만 `floor(panelW / 410f)`로 자기 공식을 써서
            // "폭에서 열 수"라는 한 질문에 답이 둘이었다. 폭 기준은 아래 cardW·BeginScrollView와
            // 같은 `panelW - 14f`(스크롤바 여유)여야 세 계산이 어긋나지 않는다.
            int cols = DexBrowseLayout.GetGridColumns(panelW - 14f, OwnedTileWidth, gap, 1, OwnedMaxColumns);
            float cardW = (panelW - (cols - 1) * gap - 14f) / cols;
            float cardH = 182f;
            float totalH = DexBrowseLayout.GetGridContentHeight(owned.Count, cols, cardH, gap);
            Rect listViewport = new Rect(px, contentRect.y + 72f, panelW, contentRect.height - 86f);
            ownedDirectScroll.Handle(ref ownedScroll, listViewport, totalH, cardH * 0.34f);

            ownedScroll = GUI.BeginScrollView(
                listViewport,
                ownedScroll,
                new Rect(0, 0, panelW - 14f, Mathf.Max(listViewport.height, totalH)),
                GUIStyle.none,
                GUIStyle.none);

            for (int i = 0; i < owned.Count; i++)
            {
                PlayerInsectData pid = owned[i];
                InsectData data = insectCollection.GetInsectData(pid.insectId);

                int col = i % cols;
                int row = i / cols;
                float cx = col * (cardW + gap);
                float cy = row * (cardH + gap);

                DrawOwnedCard(cx, cy, cardW, cardH, pid, data);
            }

            GUI.EndScrollView();
            DrawScrollAffordance(listViewport, ownedScroll, totalH, new Color(0.58f, 0.4f, 0.82f, 0.75f));
        }

        private void DrawOwnedCard(float x, float y, float w, float h, PlayerInsectData pid, InsectData data)
        {
            Color rc = data != null ? UITheme.Instance.GetInsectRarityColor(data.rarity) : Color.gray;

            Rect cardRect = new Rect(x, y, w, h);
            DrawRoundedCard(cardRect, OwnedBg, Color.Lerp(rc, Color.white, 0.35f));
            // 6px 등급 레일 — 얇아서 각진 채로 둔다(ui-layout.md). y가 이미 카드 반경만큼 물려 있다.
            UISurface.Flat(new Rect(x + 8f, y + 12f, 6f, h - 24f), rc);

            if (data != null)
            {
                Color ic = UITheme.Instance.GetInsectColor(data.insectId, data.rarity);
                DrawRoundedRect(new Rect(x + 20f, y + 40f, 74f, 94f), DetailUnknownBg);
                DrawTinyInsect(x + 57f, y + h / 2f + 4, 34f, data.insectId, ic);
            }

            string name = data != null ? data.displayName : pid.insectId;
            ownedNameCache.normal.textColor = rc;
            // 이름 폭은 우측 등급 컬럼(x+w-90) 앞까지로 제한 — 겹침 방지.
            GUI.Label(new Rect(x + 108, y + 12, w - 204, 52), name, ownedNameCache);

            string rStr = data != null ? GetRarityLabel(data.rarity) : "?";
            // IV%는 우하단(x+w-90, y+82)과 아래 IV 상세줄에 이미 표시되므로 중간줄에선 생략 —
            // 폰트 확대(→34) + 좁은 폭(w-176≈269px)에 "Lv | 등급 | IV%"를 넣으면 뒤가 잘리던 회귀 차단.
            GUI.Label(new Rect(x + 108, y + 70, w - 204, 40),
                $"Lv.{pid.level}  |  {rStr}", ownedInfoCache);

            GUI.Label(new Rect(x + 108, y + 126, w - 132, 40),
                $"HP:{pid.ivHp}  ATK:{pid.ivAtk}  DEF:{pid.ivDef}", ownedStCache);

            Color gc = UITheme.Instance.GetGradeColor(pid.Grade);
            ownedGrCache.normal.textColor = gc;
            GUI.Label(new Rect(x + w - 90, y + 12, 80, 64), CapturePopupUI.GetGradeLabel(pid.Grade), ownedGrCache);

            // grade 색의 alpha만 변경 — 매 호출 new Color 회귀 차단
            Color pctCol = gc;
            pctCol.a = 0.7f;
            ownedPctCache.normal.textColor = pctCol;
            GUI.Label(new Rect(x + w - 90, y + 82, 80, 36), $"{pid.IVPercent * 100:0}%", ownedPctCache);
        }

        private void DrawItems(Rect contentRect)
        {
            if (itemInventory == null)
            {
                DrawCentered(contentRect, "아이템 인벤토리 데이터 없음");
                return;
            }

            PlayerItemSave snapshot = itemInventory.GetSnapshot();
            if (snapshot == null || snapshot.items.Count == 0)
            {
                DrawCentered(contentRect, "보유 아이템이 없습니다");
                return;
            }

            DrawRoundedCard(contentRect, ListBg, CardBorderCol);
            int positiveCount = 0;
            for (int i = 0; i < snapshot.items.Count; i++)
            {
                PlayerItemRecord record = snapshot.items[i];
                if (record != null && record.count > 0)
                {
                    positiveCount++;
                }
            }

            if (positiveCount == 0)
            {
                DrawCentered(contentRect, "보유 아이템이 없습니다");
                return;
            }

            float panelW = Mathf.Min(contentRect.width - 30f, 960f);
            float px = contentRect.x + (contentRect.width - panelW) / 2f;
            float itemH = 136f;
            float gap = 10f;
            float headerH = 74f;
            float totalH = DexBrowseLayout.GetItemContentHeight(positiveCount, headerH, itemH, gap, 20f);
            Rect viewport = new Rect(px, contentRect.y + 10f, panelW, contentRect.height - 20f);
            itemDirectScroll.Handle(ref itemScroll, viewport, totalH, 58f);

            itemScroll = GUI.BeginScrollView(
                viewport,
                itemScroll,
                new Rect(0f, 0f, panelW - 14f, Mathf.Max(viewport.height, totalH)),
                GUIStyle.none,
                GUIStyle.none);
            GUI.Label(new Rect(0f, 0f, panelW - 14f, 58f), $"보유 아이템  ·  {positiveCount}종", headerSCache);

            float itemY = headerH;
            for (int i = 0; i < snapshot.items.Count; i++)
            {
                PlayerItemRecord record = snapshot.items[i];
                if (record == null || record.count <= 0)
                {
                    continue;
                }

                DrawItemRow(0f, itemY, panelW - 18f, itemH, record);
                itemY += itemH + gap;
            }
            GUI.EndScrollView();
            DrawScrollAffordance(viewport, itemScroll, totalH, new Color(0.24f, 0.62f, 0.88f, 0.78f));
        }

        private void DrawItemRow(float x, float y, float w, float h, PlayerItemRecord rec)
        {
            Color itemCol = GetItemColor(rec.itemId);

            DrawRoundedCard(new Rect(x, y, w, h), OwnedBg, Color.Lerp(itemCol, Color.white, 0.4f));
            // 7px 등급 레일 — 위와 같은 이유로 각진 채로.
            UISurface.Flat(new Rect(x + 8f, y + 12f, 7f, h - 24f), itemCol);
            DrawRoundedRect(new Rect(x + 24f, y + h / 2f - 30f, 60f, 60f), itemCol);

            string displayName = GetItemDisplayName(rec.itemId);
            string desc = GetItemDescription(rec.itemId);

            // 이름/설명 폭은 우측 수량 컬럼(x+w-180) 앞까지로 제한 — 겹침 방지.
            itemNameCache.normal.textColor = itemCol;
            UIHelper.LabelFit(new Rect(x + 90, y + 14, w - 280, 52), displayName, itemNameCache);

            // 40px면 이 폰트로 한 줄이 겨우다 — 두 줄짜리 설명은 아랫줄이 통째로 잘렸다.
            UIHelper.LabelFit(new Rect(x + 90, y + 72, w - 280, 40), desc, itemDescCache);

            itemCountCache.normal.textColor = rec.count > 0 ? ItemCountGood : ItemCountBad;
            GUI.Label(new Rect(x + w - 180, y + 16, 160, 90), $"x{rec.count}", itemCountCache);
        }

        private void DrawCentered(Rect rect, string text)
        {
            DrawRoundedCard(rect, OwnedBg, CardBorderCol);
            GUI.Label(rect, text, centeredCache);
        }

        private void DrawTinyInsect(float cx, float cy, float size, string id, Color col)
        {
            Color dark = new Color(col.r * 0.5f, col.g * 0.5f, col.b * 0.5f);
            float s = size / 30f;

            if (string.IsNullOrEmpty(id)) id = "";

            if (id.Contains("butterfly") || id.Contains("luna") || id.Contains("atlas"))
            {
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 2 * s, cy - 10 * s, 4 * s, 20 * s), Texture2D.whiteTexture);
                GUI.color = new Color(col.r, col.g, col.b, 0.7f);
                GUI.DrawTexture(new Rect(cx - 20 * s, cy - 12 * s, 16 * s, 18 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 4 * s, cy - 12 * s, 16 * s, 18 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 16 * s, cy + 4 * s, 12 * s, 10 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 4 * s, cy + 4 * s, 12 * s, 10 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("moth"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 5 * s, cy - 6 * s, 10 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = new Color(col.r * 0.7f, col.g * 0.6f, col.b * 0.5f, 0.7f);
                GUI.DrawTexture(new Rect(cx - 20 * s, cy - 10 * s, 18 * s, 16 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 2 * s, cy - 10 * s, 18 * s, 16 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("mantis"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 4 * s, cy - 12 * s, 8 * s, 24 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 7 * s, cy - 16 * s, 14 * s, 8 * s), Texture2D.whiteTexture);
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 16 * s, cy - 8 * s, 10 * s, 3 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 6 * s, cy - 8 * s, 10 * s, 3 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("dragonfly"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 2 * s, cy - 6 * s, 4 * s, 24 * s), Texture2D.whiteTexture);
                GUI.color = new Color(col.r, col.g, col.b, 0.3f);
                GUI.DrawTexture(new Rect(cx - 18 * s, cy - 8 * s, 14 * s, 5 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 4 * s, cy - 8 * s, 14 * s, 5 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 6 * s, cy - 12 * s, 12 * s, 7 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("bee"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 10 * s, cy - 7 * s, 20 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = new Color(0, 0, 0, 0.8f);
                GUI.DrawTexture(new Rect(cx - 9 * s, cy - 2 * s, 18 * s, 2 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 9 * s, cy + 3 * s, 18 * s, 2 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("firefly"))
            {
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 8 * s, cy - 7 * s, 16 * s, 12 * s), Texture2D.whiteTexture);
                GUI.color = new Color(0.9f, 1f, 0.3f, 0.8f);
                GUI.DrawTexture(new Rect(cx - 6 * s, cy + 3 * s, 12 * s, 8 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("stag") || id.Contains("rhinoceros"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 12 * s, cy - 5 * s, 24 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 8 * s, cy - 14 * s, 16 * s, 10 * s), Texture2D.whiteTexture);
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 2 * s, cy - 22 * s, 4 * s, 10 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("cicada"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 10 * s, cy - 6 * s, 20 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = new Color(col.r, col.g, col.b, 0.3f);
                GUI.DrawTexture(new Rect(cx - 16 * s, cy - 2 * s, 12 * s, 12 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 4 * s, cy - 2 * s, 12 * s, 12 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("ant"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 4 * s, cy + 1 * s, 8 * s, 10 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 3 * s, cy - 4 * s, 6 * s, 6 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 5 * s, cy - 12 * s, 10 * s, 8 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("cricket"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 7 * s, cy - 4 * s, 14 * s, 12 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 6 * s, cy - 12 * s, 12 * s, 8 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 14 * s, cy + 4 * s, 8 * s, 3 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 6 * s, cy + 4 * s, 8 * s, 3 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("water") || id.Contains("strider"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 3 * s, cy - 8 * s, 6 * s, 18 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 18 * s, cy - 2 * s, 14 * s, 2 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 4 * s, cy - 2 * s, 14 * s, 2 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("diving"))
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 12 * s, cy - 6 * s, 24 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 8 * s, cy - 12 * s, 16 * s, 8 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("jewel") || id.Contains("scarab") || id.Contains("golden"))
            {
                Color shim = new Color(Mathf.Min(1, col.r + 0.2f), Mathf.Min(1, col.g + 0.2f), col.b);
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 12 * s, cy - 6 * s, 24 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = shim;
                GUI.DrawTexture(new Rect(cx - 9 * s, cy - 4 * s, 8 * s, 10 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 1 * s, cy - 4 * s, 8 * s, 10 * s), Texture2D.whiteTexture);
            }
            else
            {
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - 10 * s, cy - 5 * s, 20 * s, 12 * s), Texture2D.whiteTexture);
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 6 * s, cy - 12 * s, 12 * s, 8 * s), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        private string GetItemDisplayName(string itemId)
        {
            switch (itemId)
            {
                case "net_basic": return "기본 채집망";
                case "net_silver": return "은빛 채집망";
                case "net_gold": return "황금 채집망";
                case "exp_boost": return "경험치 부스터";
                case "golden_censer": return "황금 향로";
                case "spirit_blessing": return "정령의 가호";
                case "binding_net": return "포박의 그물";
                case "beast_mark": return "맹수의 표식";
                case "guardian_totem": return "수호의 토템";
                case "wound_salve": return "상처약";
                case "wound_salve_great": return "고급 상처약";
                case "antidote": return "해독제";
                case "paralysis_heal": return "마비 치료약";
                case "full_restore": return "종합 치료제";
                case "candy": return "곤충 사탕";
                case "mat_leaf": return "나뭇잎";
                case "mat_berry": return "열매";
                case "mat_honey": return "꿀";
                default: return itemId;
            }
        }

        private string GetItemDescription(string itemId)
        {
            switch (itemId)
            {
                case "net_basic": return "기본 포획 도구 - 보통 난이도";
                case "net_silver": return "좋은 품질 - 미니게임이 쉬워짐";
                case "net_gold": return "최고급 - 매우 쉬운 미니게임 + 포획률↑";
                case "exp_boost": return "10분 동안 경험치 획득량 2배";
                case "golden_censer": return "희귀 출현과 포획 확률을 크게 높여요";
                case "spirit_blessing": return "공격력과 방어력을 함께 높여요";
                case "binding_net": return "포획 확률을 높이고 도주를 막아요";
                case "beast_mark": return "전투 공격력을 크게 높여요";
                case "guardian_totem": return "전투에서 받는 피해를 줄여요";
                case "wound_salve": return "곤충 한 마리의 HP를 40 회복";
                case "wound_salve_great": return "곤충 한 마리의 HP를 120 회복";
                case "antidote": return "중독 상태를 치료";
                case "paralysis_heal": return "마비 상태를 치료";
                case "full_restore": return "HP 전부 회복 + 모든 상태 치료";
                case "candy": return "곤충의 성장과 훈련에 사용";
                case "mat_leaf": return "훈련에 사용되는 재료";
                case "mat_berry": return "곤충에게 줄 수 있는 열매";
                case "mat_honey": return "귀한 재료 - 높은 효과";
                default: return "아이템";
            }
        }

        private Color GetItemColor(string itemId)
        {
            switch (itemId)
            {
                case "net_basic": return new Color(0.6f, 0.6f, 0.6f);
                case "net_silver": return new Color(0.7f, 0.78f, 0.92f);
                case "net_gold": return new Color(1f, 0.85f, 0.2f);
                case "exp_boost": return new Color(0.35f, 0.65f, 1f);
                case "golden_censer": return new Color(1f, 0.66f, 0.12f);
                case "spirit_blessing": return new Color(0.72f, 0.48f, 0.94f);
                case "binding_net": return new Color(0.3f, 0.72f, 0.82f);
                case "beast_mark": return new Color(0.9f, 0.3f, 0.28f);
                case "guardian_totem": return new Color(0.45f, 0.6f, 0.82f);
                case "wound_salve": return new Color(0.95f, 0.46f, 0.5f);
                case "wound_salve_great": return new Color(0.88f, 0.32f, 0.58f);
                case "antidote": return new Color(0.42f, 0.75f, 0.4f);
                case "paralysis_heal": return new Color(1f, 0.72f, 0.22f);
                case "full_restore": return new Color(0.55f, 0.42f, 0.9f);
                case "candy": return new Color(1f, 0.45f, 0.68f);
                case "mat_leaf": return new Color(0.4f, 0.7f, 0.35f);
                case "mat_berry": return new Color(0.8f, 0.3f, 0.35f);
                case "mat_honey": return new Color(0.9f, 0.7f, 0.2f);
                default: return new Color(0.5f, 0.5f, 0.5f);
            }
        }

        // 선택된 종의 색다른(이로치) 개체를 플레이어가 보유 중인지 — 선택 곤충 1종에 대해서만 호출(저빈도).
        private bool OwnsShiny(string insectId)
        {
            if (insectCollection == null || string.IsNullOrEmpty(insectId)) return false;
            List<PlayerInsectData> owned = insectCollection.GetAllOwned();
            if (owned == null) return false;
            for (int i = 0; i < owned.Count; i++)
                if (owned[i] != null && owned[i].isShiny && owned[i].insectId == insectId) return true;
            return false;
        }

        public void AutoWire(InsectDatabase db, DexController dex)
        {
            if (database == null) database = db;
            if (dexController == null) dexController = dex;
        }

        public void AutoWire(PlayerInsectCollection col, PlayerItemInventory items)
        {
            if (insectCollection == null) insectCollection = col;
            if (itemInventory == null) itemInventory = items;
        }

        public void AutoWire(InsectModelPreviewRenderer pr)
        {
            if (previewRenderer == null) previewRenderer = pr;
        }
    }
}
