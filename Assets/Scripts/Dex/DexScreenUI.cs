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
        private Vector2 listScroll;
        private int selectedIndex = -1;
        private int currentTab;

        // === OnGUI GUIStyle 캐시 (옛은 매 OnGUI 31개 new GUIStyle 회귀) ===
        // 매 프레임 호출되는 영역 21개 + 조건부 호출 10개 = 총 31개 1회 초기화 후 재사용.
        // 동적 textColor/fontStyle은 매 호출 시 갱신.
        private GUIStyle titleStyleCache, countStyleCache, tabStyleCache, closeStyleCache;
        private GUIStyle numStyleCache, missingQStyleCache, listNameStyleCache, listSubStyleCache, checkStyleCache, eyeStyleCache;
        private GUIStyle detailQStyleCache, detailNameStyleCache, raritySCache, caughtSCache, seenSCache, unkSCache;
        private GUIStyle labelSCache, valSCache, hintSCache, habitatLabelCache, habitatValCache, descSCache;
        private GUIStyle ownedNameCache, ownedInfoCache, ownedStCache, ownedGrCache, ownedPctCache;
        private GUIStyle headerSCache, itemNameCache, itemDescCache, itemCountCache;
        private GUIStyle noSelectionCache, centeredCache;
        private bool dexStylesInitialized;

        // 매 OnGUI 호출되는 정적 Color — new Color 회귀 차단
        private static readonly Color DexBgColor = new Color(0.02f, 0.03f, 0.06f, 0.97f);
        private static readonly Color TopBarBg = new Color(0.06f, 0.08f, 0.14f, 1f);
        private static readonly Color TitleCol = new Color(1f, 0.88f, 0.3f);
        private static readonly Color CountCol = new Color(0.6f, 0.65f, 0.7f);
        private static readonly Color TabActiveBg = new Color(0.2f, 0.4f, 0.8f);
        private static readonly Color TabInactiveBg = new Color(0.12f, 0.14f, 0.2f);
        private static readonly Color TitleLineCol = new Color(1f, 0.88f, 0.3f, 0.6f);
        private static readonly Color ListBg = new Color(0.04f, 0.05f, 0.09f, 0.9f);
        private static readonly Color SelectedRowBg = new Color(0.15f, 0.2f, 0.35f, 0.95f);
        private static readonly Color RowBgEven = new Color(0.06f, 0.07f, 0.12f, 0.6f);
        private static readonly Color RowBgOdd = new Color(0.06f, 0.07f, 0.12f, 0.4f);
        private static readonly Color NumCol = new Color(0.35f, 0.38f, 0.42f);
        private static readonly Color UnknownIconCol = new Color(0.12f, 0.12f, 0.15f);
        private static readonly Color UnknownQCol = new Color(0.2f, 0.2f, 0.22f);
        private static readonly Color CaughtNameCol = new Color(0.55f, 0.55f, 0.6f);
        private static readonly Color SubCol = new Color(0.4f, 0.42f, 0.48f);
        private static readonly Color SubMissingCol = new Color(0.15f, 0.15f, 0.18f);
        private static readonly Color CheckBg = new Color(0.2f, 0.7f, 0.3f);
        private static readonly Color EyeBg = new Color(0.5f, 0.5f, 0.2f);
        private static readonly Color DetailBg = new Color(0.04f, 0.06f, 0.1f, 0.95f);
        private static readonly Color DetailUnknownBg = new Color(0.08f, 0.08f, 0.1f);
        private static readonly Color DetailUnknownQ = new Color(0.15f, 0.15f, 0.18f);
        private static readonly Color SeenLabelCol = new Color(0.8f, 0.8f, 0.3f);
        private static readonly Color CaughtLabelCol = new Color(0.3f, 0.85f, 0.4f);
        private static readonly Color UnkCol = new Color(0.2f, 0.2f, 0.22f);
        private static readonly Color InfoBoxBg = new Color(0.08f, 0.1f, 0.16f, 0.8f);
        private static readonly Color LabelCol = new Color(0.55f, 0.58f, 0.65f);
        private static readonly Color HintCol = new Color(0.4f, 0.42f, 0.48f);
        private static readonly Color HabitatLabelCol = new Color(0.35f, 0.55f, 0.35f);
        private static readonly Color HabitatValCol = new Color(0.5f, 0.75f, 0.5f);
        private static readonly Color DescBg = new Color(0.06f, 0.08f, 0.12f, 0.6f);
        private static readonly Color DescCol = new Color(0.6f, 0.62f, 0.68f);
        private static readonly Color OwnedBg = new Color(0.07f, 0.09f, 0.15f, 0.95f);
        private static readonly Color InfoCol = new Color(0.6f, 0.6f, 0.65f);
        private static readonly Color StCol = new Color(0.45f, 0.48f, 0.52f);
        private static readonly Color HeaderCol = new Color(0.7f, 0.75f, 0.8f);
        private static readonly Color ItemDescCol = new Color(0.5f, 0.52f, 0.58f);
        private static readonly Color ItemCountGood = new Color(1f, 0.92f, 0.5f);
        private static readonly Color ItemCountBad = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color CenteredCol = new Color(0.35f, 0.35f, 0.4f);
        private static readonly Color NoSelectionCol = new Color(0.3f, 0.3f, 0.35f);
        private static readonly Color CaughtBgAlpha = new Color(0.2f, 0.7f, 0.3f, 0.15f);
        private static readonly Color SeenBgAlpha = new Color(0.5f, 0.5f, 0.2f, 0.15f);

        private void InitDexStyles()
        {
            if (dexStylesInitialized) return;
            dexStylesInitialized = true;

            titleStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 52, fontStyle = FontStyle.Bold };
            titleStyleCache.normal.textColor = TitleCol;

            countStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 36 };
            countStyleCache.normal.textColor = CountCol;

            tabStyleCache = new GUIStyle(GUI.skin.button) { fontSize = 36 };

            closeStyleCache = new GUIStyle(GUI.skin.button) { fontSize = 38, fontStyle = FontStyle.Bold };

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
            { fontSize = 60, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

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
            valSCache.normal.textColor = Color.white;

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

            noSelectionCache = new GUIStyle(GUI.skin.label)
            { fontSize = 42, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            noSelectionCache.normal.textColor = NoSelectionCol;

            centeredCache = new GUIStyle(GUI.skin.label)
            { fontSize = 42, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            centeredCache.normal.textColor = CenteredCol;
        }

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            if (isOpen)
            {
                ModalUIRegistry.Register(this);
                if (TutorialQuestManager.Instance != null)
                    TutorialQuestManager.Instance.NotifyDexOpened();
            }
            else ModalUIRegistry.Unregister(this);
        }
        public void CloseModal()
        {
            isOpen = false;
            ModalUIRegistry.Unregister(this);
        }
        private void OnDisable() { ModalUIRegistry.Unregister(this); }

        private readonly string[] tabNames = { "곤충 도감", "보유 곤충", "아이템" };

        private void Update()
        {
            if (!isOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            { CloseModal(); return; }

            if (currentTab == 0 && database != null)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                { selectedIndex = Mathf.Max(0, selectedIndex - 1); ScrollToSelected(); }
                if (Input.GetKeyDown(KeyCode.DownArrow))
                { selectedIndex = Mathf.Min(database.insects.Count - 1, selectedIndex + 1); ScrollToSelected(); }
            }
        }

        private void ScrollToSelected()
        {
            float rowH = 80f;
            float targetY = selectedIndex * rowH;
            float viewH = UIScale.VirtualScreenHeight - (UIScale.IsMobileLayout ? 180f : 120f);
            if (targetY < listScroll.y) listScroll.y = targetY;
            else if (targetY + rowH > listScroll.y + viewH) listScroll.y = targetY + rowH - viewH;
        }

        private void OnGUI()
        {
            if (isOpen)
            {
                Event evt = Event.current;
                if (evt != null && evt.type == EventType.KeyDown)
                {
                    if (evt.keyCode == KeyCode.Escape)
                    { CloseModal(); evt.Use(); return; }
                    if (currentTab == 0 && database != null)
                    {
                        if (evt.keyCode == KeyCode.UpArrow)
                        { selectedIndex = Mathf.Max(0, selectedIndex - 1); ScrollToSelected(); evt.Use(); }
                        if (evt.keyCode == KeyCode.DownArrow)
                        { selectedIndex = Mathf.Min(database.insects.Count - 1, selectedIndex + 1); ScrollToSelected(); evt.Use(); }
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

            DrawTopBar();

            float contentY = UIScale.IsMobileLayout ? 180f : 120f;
            float contentH = UIScale.VirtualScreenHeight - contentY;

            if (currentTab == 0)
                DrawPokedex(contentY, contentH);
            else if (currentTab == 1)
                DrawOwnedInsects(contentY, contentH);
            else if (currentTab == 2)
                DrawItems(contentY, contentH);

            UIScale.End();
        }

        private void DrawTopBar()
        {
            bool mobile = UIScale.IsMobileLayout;
            float sw = UIScale.VirtualScreenWidth;
            float topBarH = mobile ? 174f : 114f;
            GUI.color = TopBarBg;
            GUI.DrawTexture(new Rect(0, 0, sw, topBarH), Texture2D.whiteTexture);

            int total = database.insects.Count;
            int discovered = 0, captured = 0;
            foreach (var ins in database.insects)
            {
                if (ins == null) continue;
                if (dexController.IsDiscovered(ins.insectId)) discovered++;
                if (dexController.HasRecord(ins.insectId)) captured++;
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(20, 10, mobile ? 350f : 500f, 60), "곤충 도감", titleStyleCache);
            GUI.Label(new Rect(mobile ? 350f : 320f, 16, mobile ? 490f : 500f, 50),
                $"발견 {discovered}/{total}   포획 {captured}/{total}", countStyleCache);

            float tabW = mobile ? (sw - 40f) / tabNames.Length : 230f;
            float tabGap = mobile ? 0f : 10f;
            float tabX = mobile ? 20f : sw / 2f - tabNames.Length * 120f;
            float tabY = mobile ? 94f : 66f;
            float tabH = mobile ? 68f : 42f;
            for (int i = 0; i < tabNames.Length; i++)
            {
                bool active = currentTab == i;
                tabStyleCache.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                GUI.backgroundColor = active ? TabActiveBg : TabInactiveBg;
                if (GUI.Button(new Rect(tabX + i * (tabW + tabGap), tabY, tabW, tabH), tabNames[i], tabStyleCache))
                { currentTab = i; listScroll = Vector2.zero; selectedIndex = -1; }
            }
            GUI.backgroundColor = Color.white;

            if (GUI.Button(new Rect(sw - 180, 10, 168, mobile ? 68f : 50f), mobile ? "닫기" : "닫기 [N]", closeStyleCache))
                CloseModal();

            GUI.color = TitleLineCol;
            GUI.DrawTexture(new Rect(0, topBarH - 2f, sw, 3), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawPokedex(float y, float h)
        {
            float listW = UIScale.IsMobileLayout ? 420f : 500f;
            float detailX = listW + 10;
            float detailW = UIScale.VirtualScreenWidth - detailX - 10;

            GUI.color = ListBg;
            GUI.DrawTexture(new Rect(0, y, listW, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float rowH = 80f;
            int count = database.insects.Count;
            float totalListH = count * rowH;

            listScroll = GUI.BeginScrollView(
                new Rect(4, y + 4, listW - 4, h - 8),
                listScroll,
                new Rect(0, 0, listW - 28, totalListH));

            for (int i = 0; i < count; i++)
            {
                InsectData ins = database.insects[i];
                if (ins == null) continue;

                float ry = i * rowH;
                bool found = dexController.IsDiscovered(ins.insectId);
                bool caught = dexController.HasRecord(ins.insectId);
                bool selected = i == selectedIndex;

                if (selected)
                {
                    GUI.color = SelectedRowBg;
                    GUI.DrawTexture(new Rect(0, ry, listW - 28, rowH - 2), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    Rect rowRect = new Rect(0, ry, listW - 28, rowH - 2);
                    UIHelper.DrawRarityBorder(rowRect, (int)ins.rarity, Time.time);
                }
                else
                {
                    GUI.color = (i % 2 == 0) ? RowBgEven : RowBgOdd;
                    GUI.DrawTexture(new Rect(0, ry, listW - 28, rowH - 2), Texture2D.whiteTexture);
                }

                GUI.color = Color.white;
                GUI.Label(new Rect(10, ry + 4, 72, 30), $"#{i + 1:D3}", numStyleCache);

                float iconCx = 62, iconCy = ry + rowH / 2f;
                if (found)
                {
                    Color ic = caught ? UITheme.Instance.GetInsectColor(ins.insectId, ins.rarity)
                        : new Color(0.3f, 0.3f, 0.35f);
                    DrawTinyInsect(iconCx, iconCy, 18f, ins.insectId, ic);
                }
                else
                {
                    GUI.color = UnknownIconCol;
                    GUI.DrawTexture(new Rect(iconCx - 14, iconCy - 14, 28, 28), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(iconCx - 14, iconCy - 14, 28, 28), "?", missingQStyleCache);
                }

                if (found)
                {
                    listNameStyleCache.normal.textColor = caught ? UITheme.Instance.GetInsectRarityColor(ins.rarity) : CaughtNameCol;
                    GUI.color = Color.white;
                    GUI.Label(new Rect(88, ry + 6, 260, 40), ins.displayName, listNameStyleCache);
                }
                else
                {
                    listNameStyleCache.normal.textColor = UnknownQCol;
                    GUI.color = Color.white;
                    GUI.Label(new Rect(88, ry + 6, 260, 40), "???", listNameStyleCache);
                }

                if (found)
                {
                    listSubStyleCache.normal.textColor = SubCol;
                    GUI.Label(new Rect(88, ry + 44, 250, 34), ins.rarity.ToString(), listSubStyleCache);
                }
                else
                {
                    listSubStyleCache.normal.textColor = SubMissingCol;
                    GUI.Label(new Rect(88, ry + 44, 250, 34), "미발견", listSubStyleCache);
                }

                if (caught)
                {
                    GUI.color = CheckBg;
                    GUI.DrawTexture(new Rect(listW - 66, ry + 22, 32, 32), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(listW - 66, ry + 22, 32, 32), "✓", checkStyleCache);
                }
                else if (found)
                {
                    GUI.color = EyeBg;
                    GUI.DrawTexture(new Rect(listW - 66, ry + 22, 32, 32), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(listW - 66, ry + 22, 32, 32), "◉", eyeStyleCache);
                }

                if (GUI.Button(new Rect(0, ry, listW - 28, rowH - 2), "", GUIStyle.none))
                    selectedIndex = i;
            }

            GUI.EndScrollView();

            if (selectedIndex >= 0 && selectedIndex < database.insects.Count)
                DrawDetail(detailX, y, detailW, h, database.insects[selectedIndex]);
            else
                DrawNoSelection(detailX, y, detailW, h);
        }

        private void DrawNoSelection(float x, float y, float w, float h)
        {
            GUI.Label(new Rect(x, y + h / 2f - 40, w, 80),
                "← 왼쪽 목록에서 곤충을 선택하세요\n(↑↓ 방향키 또는 클릭)", noSelectionCache);
        }

        private void DrawDetail(float x, float y, float w, float h, InsectData ins)
        {
            bool found = dexController.IsDiscovered(ins.insectId);
            bool caught = dexController.HasRecord(ins.insectId);
            DexRecord record = null;
            dexController.TryGetRecord(ins.insectId, out record);

            GUI.color = DetailBg;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

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
                GUI.color = portBg;
                GUI.DrawTexture(new Rect(boxX, boxY, previewSz, previewSz), Texture2D.whiteTexture);
                GUI.color = Color.white;

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
                float rotBtnY = boxY + previewSz - 56f;
                if (GUI.Button(new Rect(boxX + 8f, rotBtnY, 46f, 50f), "◀"))
                    previewAngle -= 30f;
                if (GUI.Button(new Rect(boxX + previewSz - 54f, rotBtnY, 46f, 50f), "▶"))
                    previewAngle += 30f;
                // 이로치(색다른 모습) 토글 — 박스 상단 중앙
                GUI.backgroundColor = previewShiny ? new Color(1f, 0.85f, 0.2f) : new Color(0.32f, 0.32f, 0.38f);
                float variantButtonH = UIScale.IsMobileLayout ? 60f : 40f;
                if (GUI.Button(new Rect(boxX + previewSz / 2f - 100f, boxY + 6f, 200f, variantButtonH), previewShiny ? "★ 색다른 모습" : "✦ 일반 / 색다른"))
                    previewShiny = !previewShiny;
                GUI.backgroundColor = Color.white;
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
                GUI.color = portBg;
                GUI.DrawTexture(new Rect(cx - portraitSize, py, portraitSize * 2f, portraitSize * 2f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                DrawTinyInsect(cx, py + portraitSize, portraitSize * 1.2f, ins.insectId, new Color(0.3f, 0.3f, 0.35f));
                py += portraitSize * 2 + 12;
            }
            else
            {
                GUI.color = DetailUnknownBg;
                GUI.DrawTexture(new Rect(cx - 70, py, 140, 140), Texture2D.whiteTexture);
                GUI.color = Color.white;
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
                GUI.Label(new Rect(x, py, w, 46), ins.rarity.ToString(), raritySCache);
                py += 50;
            }

            if (caught)
            {
                GUI.color = CaughtBgAlpha;
                GUI.DrawTexture(new Rect(cx - 120, py, 240, 50), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 120, py, 240, 50), "✓ 포획 완료", caughtSCache);
                py += 56;
            }
            else if (found)
            {
                GUI.color = SeenBgAlpha;
                GUI.DrawTexture(new Rect(cx - 120, py, 240, 50), Texture2D.whiteTexture);
                GUI.color = Color.white;
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

            GUI.color = InfoBoxBg;
            float infoBoxX = x + 20;
            float infoBoxW = w - 40;
            GUI.DrawTexture(new Rect(infoBoxX, py, infoBoxW, caught ? 396 : 120), Texture2D.whiteTexture);
            GUI.color = Color.white;

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

                if (record != null)
                {
                    DrawInfoRow(lx, py + 276, lw, "발견 횟수", $"{record.discoveredCount}회", labelSCache, valSCache);
                    DrawInfoRow(lx, py + 328, lw, "포획 횟수", $"{record.capturedCount}회", labelSCache, valSCache);
                }

                py += 408;
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
                GUI.color = DescBg;
                GUI.DrawTexture(new Rect(infoBoxX, py, infoBoxW, 160), Texture2D.whiteTexture);
                GUI.color = Color.white;
                // 라벨 높이를 박스에 맞춰 키움(옛 56 → 144) — 여러 줄 설명 짤림 해소.
                GUI.Label(new Rect(infoBoxX + 14, py + 10, infoBoxW - 28, 144), ins.description, descSCache);
            }
        }

        private void DrawInfoRow(float x, float y, float w, string label, string val,
            GUIStyle ls, GUIStyle vs)
        {
            GUI.Label(new Rect(x, y, w * 0.5f, 44), label, ls);
            GUI.Label(new Rect(x + w * 0.5f, y, w * 0.5f, 44), val, vs);
        }

        private void DrawOwnedInsects(float y, float h)
        {
            if (insectCollection == null)
            {
                DrawCentered(y, h, "곤충 컬렉션 데이터 없음");
                return;
            }

            List<PlayerInsectData> owned = insectCollection.GetAllOwned();
            if (owned.Count == 0)
            {
                DrawCentered(y, h, "아직 포획한 곤충이 없습니다\n필드에서 곤충에 다가가 E키를 눌러 포획하세요!");
                return;
            }

            float panelW = Mathf.Min(UIScale.VirtualScreenWidth - 40, 900);
            float px = (UIScale.VirtualScreenWidth - panelW) / 2f;
            // 폰트 확대에 맞춰 카드를 크게(열 수 감소) — 이름/스탯 표시 폭 확보. 스크롤뷰가 세로 흡수.
            int cols = Mathf.Max(1, Mathf.FloorToInt(panelW / 330f));
            float cardW = (panelW - (cols - 1) * 10) / cols;
            float cardH = 170f;
            int rows = Mathf.CeilToInt((float)owned.Count / cols);
            float totalH = rows * (cardH + 8);

            listScroll = GUI.BeginScrollView(
                new Rect(px, y + 4, panelW + 20, h - 8),
                listScroll,
                new Rect(0, 0, panelW, totalH));

            for (int i = 0; i < owned.Count; i++)
            {
                PlayerInsectData pid = owned[i];
                InsectData data = insectCollection.GetInsectData(pid.insectId);

                int col = i % cols;
                int row = i / cols;
                float cx = col * (cardW + 10);
                float cy = row * (cardH + 8);

                DrawOwnedCard(cx, cy, cardW, cardH, pid, data);
            }

            GUI.EndScrollView();
        }

        private void DrawOwnedCard(float x, float y, float w, float h, PlayerInsectData pid, InsectData data)
        {
            Color rc = data != null ? UITheme.Instance.GetInsectRarityColor(data.rarity) : Color.gray;
            int cardRarityTier = data != null ? (int)data.rarity : 0;

            GUI.color = OwnedBg;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            Rect cardRect = new Rect(x, y, w, h);
            UIHelper.DrawRarityBorder(cardRect, cardRarityTier, Time.time);
            if (cardRarityTier >= 3)
                UIHelper.DrawRarityGlow(cardRect, rc, cardRarityTier >= 4 ? 0.5f : 0.25f, Time.time);

            if (data != null)
            {
                Color ic = UITheme.Instance.GetInsectColor(data.insectId, data.rarity);
                DrawTinyInsect(x + 42, y + h / 2f + 4, 30f, data.insectId, ic);
            }

            string name = data != null ? data.displayName : pid.insectId;
            ownedNameCache.normal.textColor = rc;
            // 이름 폭은 우측 등급 컬럼(x+w-90) 앞까지로 제한 — 겹침 방지.
            GUI.Label(new Rect(x + 80, y + 12, w - 176, 52), name, ownedNameCache);

            string rStr = data != null ? data.rarity.ToString() : "?";
            // IV%는 우하단(x+w-90, y+82)과 아래 IV 상세줄에 이미 표시되므로 중간줄에선 생략 —
            // 폰트 확대(→34) + 좁은 폭(w-176≈269px)에 "Lv | 등급 | IV%"를 넣으면 뒤가 잘리던 회귀 차단.
            GUI.Label(new Rect(x + 80, y + 70, w - 176, 40),
                $"Lv.{pid.level}  |  {rStr}", ownedInfoCache);

            GUI.Label(new Rect(x + 80, y + 118, w - 40, 40),
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

        private void DrawItems(float y, float h)
        {
            if (itemInventory == null)
            {
                DrawCentered(y, h, "아이템 인벤토리 데이터 없음");
                return;
            }

            PlayerItemSave snapshot = itemInventory.GetSnapshot();
            if (snapshot == null || snapshot.items.Count == 0)
            {
                DrawCentered(y, h, "보유 아이템이 없습니다");
                return;
            }

            float panelW = Mathf.Min(UIScale.VirtualScreenWidth - 40, 700);
            float px = (UIScale.VirtualScreenWidth - panelW) / 2f;

            GUI.Label(new Rect(px, y + 10, panelW, 58), "보유 아이템", headerSCache);

            // 최대 6종(스크롤뷰 없음) — 폰트 확대해도 가로 폰(높이 1080, y=180) 예산 900px 내.
            float iy = y + 62;
            float itemH = 130f;

            foreach (PlayerItemRecord rec in snapshot.items)
            {
                if (rec.count <= 0) continue;

                DrawItemRow(px, iy, panelW, itemH, rec);
                iy += itemH + 6;
            }
        }

        private void DrawItemRow(float x, float y, float w, float h, PlayerItemRecord rec)
        {
            Color itemCol = GetItemColor(rec.itemId);

            GUI.color = OwnedBg;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = itemCol;
            GUI.DrawTexture(new Rect(x, y, 4, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + 20, y + h / 2f - 28, 56, 56), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string displayName = GetItemDisplayName(rec.itemId);
            string desc = GetItemDescription(rec.itemId);

            // 이름/설명 폭은 우측 수량 컬럼(x+w-180) 앞까지로 제한 — 겹침 방지.
            itemNameCache.normal.textColor = itemCol;
            GUI.Label(new Rect(x + 90, y + 14, w - 280, 52), displayName, itemNameCache);

            GUI.Label(new Rect(x + 90, y + 72, w - 280, 40), desc, itemDescCache);

            itemCountCache.normal.textColor = rec.count > 0 ? ItemCountGood : ItemCountBad;
            GUI.Label(new Rect(x + w - 180, y + 16, 160, 90), $"x{rec.count}", itemCountCache);
        }

        private void DrawCentered(float y, float h, string text)
        {
            GUI.Label(new Rect(0, y, UIScale.VirtualScreenWidth, h), text, centeredCache);
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
