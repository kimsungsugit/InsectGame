using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class CharacterOutfitUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private CharacterOutfitManager outfitManager;
        [SerializeField] private OutfitBonusProvider bonusProvider;
        [SerializeField] private CharacterModelPreviewRenderer modelPreview;

        private bool isOpen;

        /// <summary>
        /// 아이템별 보너스 문구 캐시. <c>GetPrimaryBonusText()</c>는 <c>$"포획 +{x*100:0}%"</c>처럼
        /// 문자열을 만드는데 호출부가 <b>카드 루프 안</b>이라 카드마다·OnGUI 패스마다 새로 났다
        /// (바로 위 줄의 GUIStyle 회귀는 막아뒀으면서 문자열은 남아 있던 자리다).
        /// 카탈로그는 세션 내내 불변이라 한 번 구우면 무효화가 필요 없다.
        /// </summary>
        private readonly Dictionary<string, string> bonusTextCache = new Dictionary<string, string>();

        /// <summary>세트 진행도 별 문자열 캐시 — 키는 (채운 수, 전체 수). 필요한 조합이 몇 개뿐이다.</summary>
        private static readonly Dictionary<int, string> StarCache = new Dictionary<int, string>();

        private string BonusTextFor(OutfitItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) return "";
            if (!bonusTextCache.TryGetValue(item.itemId, out string text))
            {
                text = item.statBonus.GetPrimaryBonusText();
                bonusTextCache[item.itemId] = text;
            }

            return text;
        }

        /// <summary>
        /// "★★☆☆" — 예전엔 <c>stars += ...</c>를 루프로 돌려 세트마다·패스마다 total개의 문자열이 났다
        /// (덧붙이기 루프라 할당이 제곱으로 는다). 조합 수가 적으니 구워 둔다.
        /// </summary>
        private static string StarsFor(int equipped, int total)
        {
            int safeTotal = Mathf.Clamp(total, 0, 32);
            int safeEquipped = Mathf.Clamp(equipped, 0, safeTotal);
            int key = safeTotal * 64 + safeEquipped;
            if (StarCache.TryGetValue(key, out string cached)) return cached;

            System.Text.StringBuilder sb = new System.Text.StringBuilder(safeTotal);
            for (int i = 0; i < safeTotal; i++) sb.Append(i < safeEquipped ? '★' : '☆');
            string built = sb.ToString();
            StarCache[key] = built;
            return built;
        }
        private OutfitSlot selectedSlot = OutfitSlot.Hat;
        private Vector2 scrollPos;
        private readonly UIDirectScroll directScroll = new UIDirectScroll();

        // 장착 피드백
        private float equipFlashTimer;
        private string lastEquippedId;
        private float setCompleteFlashTimer;
        private readonly System.Collections.Generic.Dictionary<string, bool> prevSetStates =
            new System.Collections.Generic.Dictionary<string, bool>();

        // 패널 페이드
        private TweenHandle openFade;
        private bool wasOpen;

        // 캐릭터 미리보기 — 3D 마네킹이 있으면 그걸, 없으면 2D 도트 폴백.
        private float previewRotate;      // 2D 폴백의 좌우 흔들림 위상
        private float previewYaw = CharacterModelPreviewRenderer.FrontYaw;   // 3D 마네킹 Y 회전(도). 드래그로 바뀐다
        private bool previewDragging;
        private float previewDragLastX;
        // 지금 그릴 조합. 실장착을 복사해 두고 입어보기(try-on) 시 한 슬롯만 덮어쓴다.
        private readonly OutfitLoadout previewLoadout = new OutfitLoadout();
        private OutfitItem tryOnItem;        // 호버 중인 카드. 실장착은 건드리지 않는다
        private bool hoverFoundThisPass;

        // 호버 툴팁 (ScrollView 밖에서 렌더링)
        private OutfitItem hoveredItemForTooltip;
        private Rect hoveredCardScreenRect;

        public bool IsOpen => isOpen;

        private readonly string[] slotLabels = new string[]
        {
            "모자", "상의", "하의", "겉옷", "신발", "가방", "도구", "악세서리"
        };

        // 스타일 캐시
        private GUIStyle panelStyle;
        private GUIStyle tabNormalStyle;
        private GUIStyle tabSelectedStyle;
        private GUIStyle cardStyle;
        private GUIStyle cardEquippedStyle;
        private GUIStyle cardLockedStyle;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle coinStyle;
        private GUIStyle buttonStyle;
        private GUIStyle closeStyle;
        private GUIStyle bonusStyle;
        private GUIStyle setStyle;
        private GUIStyle setActiveStyle;
        private GUIStyle infoStyleCache;
        private GUIStyle infoNameStyleCache;
        private bool stylesInitialized;

        // 매 OnGUI 프레임 FindFirstObjectByType 회귀 차단 — 첫 조회 1회만.
        private PlayerCurrencyWallet walletCache;

        private static readonly Color InfoLabelCol = new Color(0.85f, 0.9f, 1f);

        public void Toggle()
        {
            isOpen = !isOpen;
            if (isOpen) scrollPos = Vector2.zero;
            // 외형(성별·머리·얼굴)은 캐릭터 생성 화면에서만 바뀌므로 이 모달 밖에서만 변한다.
            // 여기서 한 번 표시해 주면 렌더러가 매 프레임 PlayerPrefs를 두드리지 않아도 된다.
            if (modelPreview != null) modelPreview.InvalidatePreview();
            directScroll.Reset();
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
            // 옛은 isOpen=true 그대로 두고 Unregister만 호출 → 같은 GO SetActive 토글 시
            // isOpen=true이지만 Registry 미등록 상태로 stale. HandleEscape가 이 모달을 무시.
            isOpen = false;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }

        public void AutoWire(CharacterOutfitManager manager)
        {
            if (outfitManager == null) outfitManager = manager;
        }

        public void AutoWire(CharacterOutfitManager manager, OutfitBonusProvider bonus)
        {
            if (outfitManager == null) outfitManager = manager;
            if (bonusProvider == null) bonusProvider = bonus;
        }

        public void AutoWire(CharacterModelPreviewRenderer preview)
        {
            if (modelPreview == null) modelPreview = preview;
        }

        // ── 캐릭터 미리보기 ──

        /// <summary>
        /// 3D 마네킹이 준비돼 있으면 그것을, 아직이면 2D 도트 폴백을 그린다.
        /// 드래그로 돌릴 수 있고, 카드에 마우스를 올리면 사기 전에 입어볼 수 있다(실장착 불변).
        /// </summary>
        private void DrawCharacterPreview(Rect area, bool mobile)
        {
            UITheme theme = UITheme.Instance;
            UISurface.Card(area, theme.surfaceBase, theme.surfaceBorder);

            float infoH = mobile ? 42f : 84f;
            Rect stage = new Rect(area.x + 8f, area.y + 8f,
                area.width - 16f, Mathf.Max(1f, area.height - 16f - infoH));

            Texture preview = null;
            if (modelPreview != null)
            {
                SyncPreviewLoadout();
                HandlePreviewDrag(stage);
                preview = modelPreview.GetPreview(previewLoadout, previewYaw);
            }

            if (preview != null)
            {
                GUI.DrawTexture(stage, preview, ScaleMode.ScaleToFit, true);
            }
            else
            {
                // 콜드 캐시(첫 프레임)나 렌더러 미배선 — 기존 2D 도트 캐릭터로 버틴다.
                float charScale = mobile ? 1.7f : 2.9f;
                float swayX = Mathf.Sin(previewRotate * Mathf.Deg2Rad) * 12f * charScale;
                CharacterPortraitRenderer.DrawWithOutfit(
                    stage.center.x, stage.y + stage.height * 0.5f, charScale, swayX);
            }

            // 지금 보고 있는 슬롯이 무엇을 입고 있는지. 이름 길이는 데이터가 정하고 상자는 고정이라
            // LabelFit으로 줄여 맞춘다(GUI.Label을 쓰면 text_fit_lint가 막는다).
            OutfitItem shown = ResolvePreviewItem(selectedSlot);
            string curName = shown != null ? shown.displayName : "(없음)";
            float infoY = area.yMax - infoH - 4f;
            if (mobile)
            {
                UIHelper.LabelFit(new Rect(area.x + 16f, infoY, area.width - 32f, 38f),
                    $"{slotLabels[(int)selectedSlot]}: {curName}", infoNameStyleCache);
            }
            else
            {
                GUI.Label(new Rect(area.x + 16f, infoY, area.width - 32f, 32f),
                    $"현재 {slotLabels[(int)selectedSlot]}:", infoStyleCache);
                UIHelper.LabelFit(new Rect(area.x + 16f, infoY + 32f, area.width - 32f, 36f),
                    curName, infoNameStyleCache);
            }
        }

        /// <summary>실장착을 복사한 뒤 호버 중인 아이템만 덮어쓴다 — 이게 입어보기(try-on)다.</summary>
        private void SyncPreviewLoadout()
        {
            previewLoadout.CopyFrom(outfitManager);
            if (tryOnItem != null) previewLoadout.Set(tryOnItem.slot, tryOnItem.itemId);
        }

        private OutfitItem ResolvePreviewItem(OutfitSlot slot)
        {
            if (tryOnItem != null && tryOnItem.slot == slot) return tryOnItem;
            return outfitManager.GetEquipped(slot);
        }

        private void HandlePreviewDrag(Rect stage)
        {
            Event e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (stage.Contains(e.mousePosition))
                    {
                        previewDragging = true;
                        previewDragLastX = e.mousePosition.x;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (previewDragging)
                    {
                        // 가상좌표 기준이라 화면 해상도가 달라도 같은 손맛이 난다.
                        previewYaw -= (e.mousePosition.x - previewDragLastX) * 0.6f;
                        previewDragLastX = e.mousePosition.x;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (previewDragging)
                    {
                        previewDragging = false;
                        e.Use();
                    }
                    break;
            }
        }

        // P키는 QuickAccessBarUI에서 처리

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;
            bool mobile = UIScale.IsMobileLayout;

            Texture2D panelTex = UIHelper.GetCachedTex(new Color(0.08f, 0.1f, 0.2f, 0.92f));
            Texture2D tabNormalTex = UIHelper.GetCachedTex(new Color(0.25f, 0.28f, 0.35f, 1f));
            Texture2D tabSelTex = UIHelper.GetCachedTex(new Color(0.3f, 0.5f, 0.9f, 1f));
            Texture2D cardTex = UIHelper.GetCachedTex(new Color(0.15f, 0.17f, 0.25f, 1f));
            Texture2D cardEqTex = UIHelper.GetCachedTex(new Color(0.2f, 0.25f, 0.35f, 1f));
            Texture2D cardLockTex = UIHelper.GetCachedTex(new Color(0.1f, 0.1f, 0.15f, 0.9f));
            Texture2D btnTex = UIHelper.GetCachedTex(new Color(0.2f, 0.5f, 0.2f, 1f));
            Texture2D closeTex = UIHelper.GetCachedTex(new Color(0.7f, 0.15f, 0.15f, 1f));

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTex;

            tabNormalStyle = new GUIStyle(GUI.skin.button);
            tabNormalStyle.normal.background = tabNormalTex;
            tabNormalStyle.normal.textColor = Color.white;
            tabNormalStyle.fontSize = mobile ? 26 : 24;
            tabNormalStyle.fontStyle = FontStyle.Bold;
            tabNormalStyle.alignment = TextAnchor.MiddleCenter;

            tabSelectedStyle = new GUIStyle(tabNormalStyle);
            tabSelectedStyle.normal.background = tabSelTex;

            cardStyle = new GUIStyle(GUI.skin.box);
            cardStyle.normal.background = cardTex;
            cardStyle.normal.textColor = Color.white;
            cardStyle.alignment = TextAnchor.UpperCenter;
            cardStyle.padding = new RectOffset(4, 4, 4, 4);

            cardEquippedStyle = new GUIStyle(cardStyle);
            cardEquippedStyle.normal.background = cardEqTex;

            cardLockedStyle = new GUIStyle(cardStyle);
            cardLockedStyle.normal.background = cardLockTex;
            cardLockedStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 1f);

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 22;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
            titleStyle.alignment = TextAnchor.MiddleLeft;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 22;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.wordWrap = true;

            coinStyle = new GUIStyle(GUI.skin.label);
            coinStyle.fontSize = 26;
            coinStyle.fontStyle = FontStyle.Bold;
            coinStyle.normal.textColor = new Color(1f, 0.84f, 0f, 1f);
            coinStyle.alignment = TextAnchor.MiddleLeft;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.background = btnTex;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.fontSize = mobile ? 23 : 20;
            buttonStyle.fontStyle = FontStyle.Bold;

            closeStyle = new GUIStyle(GUI.skin.button);
            closeStyle.normal.background = closeTex;
            closeStyle.normal.textColor = Color.white;
            closeStyle.fontSize = mobile ? 24 : 20;
            closeStyle.fontStyle = FontStyle.Bold;

            bonusStyle = new GUIStyle(GUI.skin.label);
            bonusStyle.fontSize = 10;
            bonusStyle.normal.textColor = new Color(0.4f, 0.9f, 0.4f);
            bonusStyle.alignment = TextAnchor.MiddleCenter;

            setStyle = new GUIStyle(GUI.skin.label);
            setStyle.fontSize = 13;
            setStyle.normal.textColor = new Color(0.6f, 0.6f, 0.7f);
            setStyle.alignment = TextAnchor.MiddleLeft;
            setStyle.wordWrap = true;

            setActiveStyle = new GUIStyle(setStyle);
            setActiveStyle.fontStyle = FontStyle.Bold;

            // OnGUI 매 프레임 new GUIStyle 회귀 차단 — DrawPanel 캐릭터 아래 슬롯 정보 라벨용.
            infoStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 19, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            infoStyleCache.normal.textColor = InfoLabelCol;

            infoNameStyleCache = new GUIStyle(infoStyleCache)
            { fontSize = 24, fontStyle = FontStyle.Bold };
            infoNameStyleCache.normal.textColor = Color.white;
        }

        private void OnGUI()
        {
            // 패널 페이드
            float panelAlpha = UIHelper.AnimatePanelOpen(ref openFade, isOpen, ref wasOpen);
            if (!isOpen && panelAlpha <= 0.01f) return;
            if (outfitManager == null) return;

            // 고DPI 세로 모바일에서 UI가 물리 픽셀로 그려져 과도하게 작아지던 근본 문제 해결 —
            // 가상 캔버스(세로 1080x1920 / 가로 1920x1080) 좌표계로 통일. End()는 OnGUI 말미 1곳.
            UIScale.Begin();

            GUI.color = new Color(1f, 1f, 1f, panelAlpha);

            // 타이머 감소 (OnGUI는 프레임당 여러번 호출되므로 Repaint에서만)
            if (Event.current.type == EventType.Repaint)
            {
                if (equipFlashTimer > 0f) equipFlashTimer -= Time.deltaTime;
                if (setCompleteFlashTimer > 0f) setCompleteFlashTimer -= Time.deltaTime;
            }

            InitStyles();

            // 회전 애니메이션 갱신 (Repaint에서만 누적)
            if (Event.current.type == EventType.Repaint)
                previewRotate += Time.deltaTime * 30f;

            Rect panelRect = UISafeLayout.CenteredPanel(1200f, 820f);
            float panelW = panelRect.width;
            float panelH = panelRect.height;
            bool mobile = UIScale.IsMobileLayout;
            float x = panelRect.x;
            float y = panelRect.y;

            GUI.Box(panelRect, "", panelStyle);

            // 제목 — UIHelper.CachedStyle로 1회 캐싱 (옛 매 OnGUI new GUIStyle 회귀 차단)
            GUIStyle bigTitle = UIHelper.CachedStyle("outfit_big_title", () =>
            {
                GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                s.normal.textColor = Color.white;
                return s;
            });
            GUI.Label(new Rect(x + 24, y + 14, 540, 50), "캐릭터 꾸미기", bigTitle);

            // 닫기 버튼
            float closeSize = mobile ? 58f : 44f;
            if (GUI.Button(new Rect(x + panelW - closeSize - 12f, y + 10f, closeSize, mobile ? 56f : 40f), "X", closeStyle))
            {
                CloseModal();
            }

            // ── 슬롯 탭 치수 (미리보기가 이 값에 의존하므로 먼저 정한다) ──
            // 모바일 세로: 슬롯 8개를 4열 2행으로 배치. 옛 1행 8열은 tabW가 5열 기준이라
            // 7·8번 탭(도구·악세서리)이 화면 밖으로 잘려 접근 불가였음. 데스크톱은 우측 세로 1열 유지.
            float tabY = y + 70f;
            float tabGap = 6f;
            int tabsPerRow = 4;
            float tabW = mobile ? (panelW - 40f - tabGap * (tabsPerRow - 1)) / tabsPerRow : 140f;
            float tabH = mobile ? 64f : 50f;

            OutfitSlot[] slots = (OutfitSlot[])System.Enum.GetValues(typeof(OutfitSlot));
            int tabRows = mobile ? Mathf.CeilToInt(slots.Length / (float)tabsPerRow) : slots.Length;
            float tabBlockH = tabRows * (tabH + tabGap);

            // ── 캐릭터 미리보기 ──
            // 데스크톱은 좌측 세로 패널, 모바일 세로는 탭 아래 가로 스트립.
            // 예전엔 `if (!mobile)` 안에만 있어 모바일에서는 미리보기가 아예 없었다.
            // 모바일 y는 tabBlockH에서 파생한다 — 탭 높이를 바꿔도 겹치지 않게(값을 두 곳에 적지 않는다).
            float charAreaX = x + 20f;
            float charAreaY = mobile ? tabY + tabBlockH + 8f : y + 70f;
            float charAreaW = mobile ? panelW - 40f : 360f;
            float charAreaH = mobile ? 300f : panelH - 90f;

            Rect charArea = new Rect(charAreaX, charAreaY, charAreaW, charAreaH);
            DrawCharacterPreview(charArea, mobile);

            // ── 슬롯 탭 (데스크톱: 캐릭터 우측 / 모바일: 상단) ──
            float tabX = mobile ? x + 20f : charAreaX + charAreaW + 20f;
            for (int i = 0; i < slots.Length; i++)
            {
                Rect tabRect = mobile
                    ? new Rect(tabX + (i % tabsPerRow) * (tabW + tabGap), tabY + (i / tabsPerRow) * (tabH + tabGap), tabW, tabH)
                    : new Rect(tabX, tabY + i * (tabH + tabGap), tabW, tabH);
                GUIStyle style = (slots[i] == selectedSlot) ? tabSelectedStyle : tabNormalStyle;
                if (GUI.Button(tabRect, slotLabels[i], style))
                {
                    selectedSlot = slots[i];
                    scrollPos = Vector2.zero;
                    directScroll.Reset();
                }
            }

            // ── 세트 정보 패널 ──
            if (bonusProvider != null && !mobile)
            {
                float setY = tabY + slots.Length * (tabH + tabGap) + 8;
                ActiveSetInfo[] activeSets = bonusProvider.GetActiveSets();
                foreach (ActiveSetInfo setInfo in activeSets)
                {
                    bool active = setInfo.isPartialActive || setInfo.isFullActive;
                    GUIStyle sStyle = active ? setActiveStyle : setStyle;
                    Color prevColor = sStyle.normal.textColor;
                    if (active) sStyle.normal.textColor = setInfo.set.setColor;

                    int total = setInfo.set.requiredItemIds.Length;
                    string stars = StarsFor(setInfo.equippedCount, total);

                    string setLabel = $"{setInfo.set.displayName} ({setInfo.equippedCount}/{total})\n{stars}";
                    if (setInfo.isFullActive)
                    {
                        setLabel += "\n" + setInfo.set.fullBonus.GetPrimaryBonusText();
                    }
                    else if (setInfo.isPartialActive)
                    {
                        setLabel += "\n" + setInfo.set.partialBonus.GetPrimaryBonusText();
                    }

                    float setH = active ? 52 : 36;
                    Rect setRect = new Rect(tabX, setY, tabW, setH);

                    // 세트 완성 글로우
                    if (setCompleteFlashTimer > 0f && active)
                    {
                        float glowAlpha = Mathf.Clamp01(setCompleteFlashTimer / 1f) * 0.6f;
                        UIHelper.DrawRarityGlow(setRect, setInfo.set.setColor, glowAlpha, Time.time);
                    }

                    GUI.Label(setRect, setLabel, sStyle);
                    sStyle.normal.textColor = prevColor;
                    setY += setH + 4;
                }
            }

            // ── 오른쪽 아이템 그리드 ──
            // 모바일 그리드는 탭 2행 + 미리보기 스트립 아래에서 시작하고, 하단은 보너스 요약(-76)·
            // 재화(-44) 라벨과 겹치지 않게 84px 여백을 남긴다.
            float gridX = mobile ? x + 20f : tabX + tabW + 20f;
            float gridY = mobile ? charArea.yMax + 12f : y + 70f;
            float gridW = mobile ? panelW - 40f : panelW - (gridX - x) - 20f;
            float gridH = Mathf.Max(1f, mobile ? panelH - (gridY - y) - 84f : panelH - 150f);

            OutfitItem[] items = outfitManager.GetItemsForSlot(selectedSlot);

            float cardW = mobile ? Mathf.Max(190f, (gridW - 38f) * 0.5f) : 200f;
            float cardH = mobile ? 316f : 284f;
            float cardGap = 14f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((gridW - 10) / (cardW + cardGap)));
            int rows = Mathf.CeilToInt((float)items.Length / cols);
            float contentH = rows * (cardH + cardGap) + 10;

            hoverFoundThisPass = false;

            Rect viewRect = new Rect(gridX, gridY, gridW, gridH);
            Rect contentRect = new Rect(0, 0, gridW, contentH);
            directScroll.Handle(ref scrollPos, viewRect, contentH, cardH * 0.3f);
            scrollPos = GUI.BeginScrollView(
                viewRect,
                scrollPos,
                contentRect,
                GUIStyle.none,
                GUIStyle.none);

            for (int i = 0; i < items.Length; i++)
            {
                OutfitItem item = items[i];
                int col = i % cols;
                int row = i / cols;
                float cx = col * (cardW + cardGap) + 5;
                float cy = row * (cardH + cardGap) + 5;
                Rect cardRect = new Rect(cx, cy, cardW, cardH);
                // 스크롤 뷰포트 밖 카드는 3D 썸네일을 요청하지 않는다 — 목록 전체를 굽느라
                // 프레임당 1렌더 예산을 화면에 안 보이는 카드에 쓰지 않게. (곤충 도감엔 없는 최적화)
                bool cardVisible = cardRect.yMax >= scrollPos.y - 8f && cardRect.y <= scrollPos.y + gridH + 8f;

                bool owned = outfitManager.IsOwned(item.itemId);
                bool equipped = outfitManager.IsEquipped(item.itemId);

                // 카드 배경
                GUIStyle cStyle = owned ? (equipped ? cardEquippedStyle : cardStyle) : cardLockedStyle;
                GUI.Box(cardRect, "", cStyle);

                // 호버 하이라이트 (ScrollView 내부에서는 mousePosition이 이미 로컬 좌표)
                bool isHovered = Event.current.type == EventType.Repaint
                    && cardRect.Contains(Event.current.mousePosition);
                if (isHovered)
                {
                    GUI.DrawTexture(cardRect, UIHelper.GetCachedTex(new Color(1f, 1f, 1f, 0.08f)));
                    UIHelper.DrawBorder(cardRect, new Color(0.7f, 0.8f, 1f, 0.5f), 1);
                    hoveredItemForTooltip = item;
                    hoveredCardScreenRect = new Rect(gridX + cx - scrollPos.x, gridY + cy - scrollPos.y, cardW, cardH);
                    // 입어보기 — 미보유 아이템도 포함한다. "사기 전에 어떻게 보이나"가 핵심이다.
                    // 미리보기는 이 값을 다음 패스에서 읽으므로 한 프레임 늦는데, 체감되지 않는다.
                    tryOnItem = item;
                    hoverFoundThisPass = true;
                }

                // 장착중 금색 테두리
                if (equipped)
                {
                    UIHelper.DrawBorder(cardRect, new Color(1f, 0.84f, 0f, 1f), 2);
                }

                // 장착 플래시 오버레이
                if (equipFlashTimer > 0f && item.itemId == lastEquippedId)
                {
                    float flashAlpha = Mathf.Clamp01(equipFlashTimer / 0.4f) * 0.6f;
                    GUI.DrawTexture(cardRect, UIHelper.GetCachedTex(new Color(1f, 1f, 1f, flashAlpha)));
                }

                // 프로시저럴 의상 아이콘 — 슬롯별 실제 형태 (모자/도구/신발/...)
                // 옛 GetSlotSymbol("^", "T" 등 텍스트)은 어떤 아이템인지 시각적 구별 불가
                float previewSize = 100f;
                Rect previewRect = new Rect(cx + (cardW - previewSize) * 0.5f, cy + 10, previewSize, previewSize);
                if (item.primaryColor.a > 0.01f)
                {
                    // 배경 (어두운 톤)
                    Color bgCol = new Color(item.primaryColor.r * 0.3f + 0.05f, item.primaryColor.g * 0.3f + 0.05f, item.primaryColor.b * 0.3f + 0.05f, 0.8f);
                    GUI.DrawTexture(previewRect, UIHelper.GetCachedTex(bgCol));
                    // 테두리
                    GUI.DrawTexture(new Rect(previewRect.x, previewRect.y, previewRect.width, 2), UIHelper.GetCachedTex(item.primaryColor));
                    GUI.DrawTexture(new Rect(previewRect.x, previewRect.yMax - 2, previewRect.width, 2), UIHelper.GetCachedTex(item.primaryColor));
                    GUI.DrawTexture(new Rect(previewRect.x, previewRect.y, 2, previewRect.height), UIHelper.GetCachedTex(item.primaryColor));
                    GUI.DrawTexture(new Rect(previewRect.xMax - 2, previewRect.y, 2, previewRect.height), UIHelper.GetCachedTex(item.primaryColor));
                    // 3D 마네킹 썸네일이 준비됐으면 그것, 아직이면 레시피를 정사영한 2D.
                    // 둘 다 OutfitShapeLibrary 하나를 읽으므로 어느 쪽이 나와도 착용 모습과 일치한다.
                    Texture thumb = (modelPreview != null && cardVisible)
                        ? modelPreview.GetThumbnail(item.slot, item.itemId)
                        : null;
                    if (thumb != null)
                        GUI.DrawTexture(previewRect, thumb, ScaleMode.ScaleToFit, true);
                    else
                        CharacterPortraitRenderer.DrawItemPreview(previewRect, item.slot, item.itemId, item.primaryColor, item.secondaryColor);
                }
                else
                {
                    GUI.DrawTexture(previewRect, UIHelper.GetCachedTex(new Color(0.15f, 0.15f, 0.15f, 0.5f)));
                    GUIStyle emptyStyle = UIHelper.CachedStyle("outfit_empty", () =>
                    {
                        GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                        s.normal.textColor = new Color(0.4f, 0.4f, 0.4f);
                        return s;
                    });
                    GUI.Label(previewRect, "---", emptyStyle);
                }

                // 이름 — 길이는 데이터가 정하고 상자는 고정이라 LabelFit으로 줄여 맞춘다.
                Rect nameRect = new Rect(cx + 4, cy + 112, cardW - 8, 44);
                UIHelper.LabelFit(nameRect, item.displayName, labelStyle);

                // 보너스 표시 — CachedStyle로 1회 캐싱 (카드 12개 × 30FPS = 360회/초 new GUIStyle 회귀 차단)
                string bonusText = BonusTextFor(item);
                if (!string.IsNullOrEmpty(bonusText))
                {
                    Rect bonusRect = new Rect(cx + 4, cy + 158, cardW - 8, 24);
                    GUIStyle bigBonus = UIHelper.CachedStyle("outfit_big_bonus", () =>
                    {
                        GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter };
                        s.normal.textColor = new Color(0.4f, 0.9f, 0.4f);
                        return s;
                    });
                    GUI.Label(bonusRect, bonusText, bigBonus);
                }

                // 세트 도트 표시
                if (bonusProvider != null)
                {
                    ActiveSetInfo[] activeSets = bonusProvider.GetActiveSets();
                    foreach (ActiveSetInfo setInfo in activeSets)
                    {
                        foreach (string reqId in setInfo.set.requiredItemIds)
                        {
                            if (reqId == item.itemId)
                            {
                                Rect dotRect = new Rect(cx + 4, cy + 4, 10, 10);
                                GUI.DrawTexture(dotRect, UIHelper.GetCachedTex(setInfo.set.setColor));
                                break;
                            }
                        }
                    }
                }

                // 버튼 영역
                float itemButtonH = mobile ? 56f : 42f;
                Rect btnRect = new Rect(cx + 14, cy + cardH - itemButtonH - 12f, cardW - 28, itemButtonH);

                if (owned)
                {
                    if (equipped)
                    {
                        GUI.enabled = false;
                        GUI.Button(btnRect, "장착중", buttonStyle);
                        GUI.enabled = true;
                    }
                    else
                    {
                        if (GUI.Button(btnRect, "장착", buttonStyle))
                        {
                            outfitManager.Equip(item.itemId);
                            equipFlashTimer = 0.4f;
                            lastEquippedId = item.itemId;
                            if (InsectGame.Core.AudioManager.Instance != null)
                                InsectGame.Core.AudioManager.Instance.PlaySFX(InsectGame.Core.SfxType.Equip);
                            CheckSetCompletion();
                        }
                    }
                }
                else
                {
                    if (item.gemPrice > 0)
                    {
                        // 보석 구매 (프리미엄)
                        int currentGems = CashShopManager.Instance != null ? CashShopManager.Instance.Gems : 0;
                        bool canAfford = currentGems >= item.gemPrice;
                        GUI.backgroundColor = canAfford ? new Color(0.3f, 0.2f, 0.6f) : new Color(0.3f, 0.3f, 0.3f);
                        GUI.enabled = canAfford;
                        if (GUI.Button(btnRect, $"💎{item.gemPrice}", buttonStyle))
                        {
                            if (outfitManager.TryPurchaseWithGems(item.itemId))
                            {
                                outfitManager.Equip(item.itemId);
                            }
                        }
                        GUI.enabled = true;
                        GUI.backgroundColor = Color.white;

                        // 프리미엄 표시
                        Rect premRect = new Rect(cx + 4, cy + 186, cardW - 8, 26);
                        GUIStyle premStyle = UIHelper.CachedStyle("outfit_prem", () =>
                        {
                            GUIStyle s = new GUIStyle(GUI.skin.label);
                            s.fontSize = 17;
                            s.normal.textColor = new Color(0.9f, 0.7f, 1f);
                            s.alignment = TextAnchor.MiddleCenter;
                            return s;
                        });
                        GUI.Label(premRect, "★ 프리미엄", premStyle);
                    }
                    else if (item.price > 0)
                    {
                        // 캔디 구매
                        if (GUI.Button(btnRect, $"🍬{item.price}", buttonStyle))
                        {
                            if (outfitManager.TryPurchase(item.itemId))
                            {
                                outfitManager.Equip(item.itemId);
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(item.unlockCondition))
                    {
                        GUI.enabled = false;
                        GUI.Button(btnRect, "잠김", buttonStyle);
                        GUI.enabled = true;

                        Rect hintRect = new Rect(cx + 4, cy + 186, cardW - 8, 26);
                        GUIStyle hintStyle = UIHelper.CachedStyle("outfit_hint", () =>
                        {
                            GUIStyle s = new GUIStyle(GUI.skin.label);
                            s.fontSize = 16;
                            s.normal.textColor = new Color(0.7f, 0.6f, 0.3f);
                            s.alignment = TextAnchor.MiddleCenter;
                            s.wordWrap = true;
                            return s;
                        });
                        // 원문 토큰("region_garden"·"level_15")을 그대로 그리면 한국어 게임에
                        // 영문 식별자가 노출된다. 사람이 읽는 문장으로 바꾸고, 길이가 데이터에서
                        // 오므로 고정 26px 상자에 맞춰 폰트를 줄인다(rules/ui-layout.md의 LabelFit).
                        UIHelper.LabelFit(hintRect, DescribeUnlockCondition(item.unlockCondition), hintStyle);
                    }
                }
            }

            GUI.EndScrollView();

            // 카드에서 마우스가 벗어나면 입어보기를 풀고 실장착으로 돌아간다.
            if (Event.current.type == EventType.Repaint && !hoverFoundThisPass) tryOnItem = null;

            // ── 호버 툴팁 (ScrollView 밖에서 렌더) ──
            if (hoveredItemForTooltip != null && hoveredItemForTooltip.statBonus.HasAnyBonus()
                && outfitManager.IsOwned(hoveredItemForTooltip.itemId))
            {
                var b = hoveredItemForTooltip.statBonus;
                string tip = "";
                if (b.captureChanceBonus > 0f) tip += $"포획 +{b.captureChanceBonus * 100f:0}%\n";
                if (b.atkBonus > 0f) tip += $"ATK +{b.atkBonus * 100f:0}%\n";
                if (b.defBonus > 0f) tip += $"DEF +{b.defBonus * 100f:0}%\n";
                if (b.moveSpeedBonus > 0f) tip += $"이속 +{b.moveSpeedBonus * 100f:0}%\n";
                if (b.expMultiplier > 0f) tip += $"경험치 +{b.expMultiplier * 100f:0}%\n";
                if (b.candyMultiplier > 0f) tip += $"캔디 +{b.candyMultiplier * 100f:0}%\n";
                if (b.rareSpawnBonus > 0f) tip += $"레어 +{b.rareSpawnBonus * 100f:0}%\n";
                tip = tip.TrimEnd('\n');

                int lineCount = tip.Split('\n').Length;
                float tipH = lineCount * 17 + 8;
                Rect tipRect = new Rect(
                    hoveredCardScreenRect.x,
                    hoveredCardScreenRect.y - tipH - 4,
                    hoveredCardScreenRect.width,
                    tipH);
                GUI.DrawTexture(tipRect, UIHelper.GetCachedTex(new Color(0.05f, 0.07f, 0.15f, 0.92f)));
                GUIStyle tipStyle = UIHelper.CachedStyle("outfit_tip", () =>
                {
                    GUIStyle s = new GUIStyle(GUI.skin.label);
                    s.fontSize = 12;
                    s.normal.textColor = new Color(0.7f, 0.95f, 0.7f);
                    s.alignment = TextAnchor.MiddleCenter;
                    s.wordWrap = true;
                    s.padding = new RectOffset(4, 4, 4, 4);
                    return s;
                });
                GUI.Label(tipRect, tip, tipStyle);
            }
            hoveredItemForTooltip = null;

            // ── 하단 보너스 요약 + 코인 표시 ──
            if (bonusProvider != null)
            {
                OutfitStatBonus total = bonusProvider.GetTotalBonus();
                if (total.HasAnyBonus())
                {
                    string summary = "장비 보너스:";
                    if (total.captureChanceBonus > 0f) summary += $" 포획+{total.captureChanceBonus * 100f:0}%";
                    if (total.atkBonus > 0f) summary += $" ATK+{total.atkBonus * 100f:0}%";
                    if (total.defBonus > 0f) summary += $" DEF+{total.defBonus * 100f:0}%";
                    if (total.moveSpeedBonus > 0f) summary += $" 이속+{total.moveSpeedBonus * 100f:0}%";
                    if (total.expMultiplier > 0f) summary += $" 경험치+{total.expMultiplier * 100f:0}%";
                    if (total.candyMultiplier > 0f) summary += $" 캔디+{total.candyMultiplier * 100f:0}%";
                    if (total.rareSpawnBonus > 0f) summary += $" 레어+{total.rareSpawnBonus * 100f:0}%";

                    Rect summaryRect = new Rect(x + 24, y + panelH - 76, panelW - 48, 30);
                    GUIStyle summaryStyle = UIHelper.CachedStyle("outfit_summary", () =>
                    {
                        GUIStyle s = new GUIStyle(GUI.skin.label);
                        s.fontSize = 19;
                        s.fontStyle = FontStyle.Bold;
                        s.normal.textColor = new Color(0.4f, 0.9f, 0.4f);
                        s.alignment = TextAnchor.MiddleLeft;
                        return s;
                    });
                    GUI.Label(summaryRect, summary, summaryStyle);
                }
            }

            Rect coinRect = new Rect(x + 24, y + panelH - 44, 820, 36);
            if (walletCache == null)
                walletCache = outfitManager.GetComponent<PlayerCurrencyWallet>() ??
                    FindFirstObjectByType<PlayerCurrencyWallet>();
            int coinCount = (walletCache != null) ? walletCache.Coins : 0;
            int gemCount = CashShopManager.Instance != null ? CashShopManager.Instance.Gems : 0;
            GUI.Label(coinRect, $"보유 코인: 🪙{coinCount}    보석: 💎{gemCount}", coinStyle);

            // GUI.color 복원
            GUI.color = Color.white;

            UIScale.End();
        }

        private void CheckSetCompletion()
        {
            if (bonusProvider == null) return;

            ActiveSetInfo[] sets = bonusProvider.GetActiveSets();
            foreach (ActiveSetInfo setInfo in sets)
            {
                bool nowActive = setInfo.isPartialActive || setInfo.isFullActive;
                bool wasActive = prevSetStates.TryGetValue(setInfo.set.setId, out bool prev) && prev;

                if (nowActive && !wasActive)
                {
                    setCompleteFlashTimer = 1f;
                    if (InsectGame.Core.AudioManager.Instance != null)
                        InsectGame.Core.AudioManager.Instance.PlaySFX(InsectGame.Core.SfxType.SetComplete);
                }

                prevSetStates[setInfo.set.setId] = nowActive;
            }
        }

        private static string GetSlotSymbol(OutfitSlot slot)
        {
            switch (slot)
            {
                case OutfitSlot.Hat: return "^";
                case OutfitSlot.Top: return "T";
                case OutfitSlot.Bottom: return "II";
                case OutfitSlot.Outerwear: return "W";
                case OutfitSlot.Shoes: return "U";
                case OutfitSlot.Backpack: return "B";
                case OutfitSlot.Tool: return "+";
                case OutfitSlot.Accessory: return "*";
                default: return "?";
            }
        }

        // ── 해금 조건 문구 ──

        // regionId → 표시명. RegionDefinitions에서 1회만 파생한다(이름을 여기 박으면 낡는다).
        private static Dictionary<string, string> regionNameCache;

        /// <summary>
        /// <c>OutfitItem.unlockCondition</c>의 원문 토큰을 사람이 읽는 문장으로 바꾼다.
        ///
        /// 이 값은 UI에 그대로 그려지던 자리다 — 한국어 게임에서 "region_garden"·"level_15"가
        /// 카드에 노출됐다. 알 수 없는 형식은 토큰을 그대로 돌려주므로, 새 조건 형식을 추가해도
        /// 화면이 비지는 않는다(대신 여기 분기를 늘려 문장을 붙일 것).
        ///
        /// <b>해금 판정은 여기서 하지 않는다.</b> 현재 저장소에 <c>unlockCondition</c>을 평가해
        /// 소유를 부여하는 코드가 없어 조건부 의상 4벌은 획득 불가 상태다 — 그 배선은 해금 시점을
        /// 정하는 게임 디자인 결정이라 별건이다. 이 메서드는 표시만 고친다.
        /// </summary>
        internal static string DescribeUnlockCondition(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return "";

            if (condition.StartsWith("region_"))
            {
                string regionId = condition.Substring("region_".Length);
                return $"{RegionDisplayName(regionId)} 도달 시 해금";
            }
            if (condition.StartsWith("level_"))
            {
                string lv = condition.Substring("level_".Length);
                return int.TryParse(lv, out int n) ? $"Lv.{n} 달성 시 해금" : condition;
            }
            if (condition.StartsWith("quest_"))
            {
                return "특정 퀘스트 완료 시 해금";
            }
            return condition;   // 미지의 형식 — 토큰이라도 보여 준다
        }

        private static string RegionDisplayName(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return "특정 지역";
            if (regionNameCache == null)
            {
                regionNameCache = new Dictionary<string, string>();
                foreach (Data.RegionData r in RegionDefinitions.CreateAll())
                {
                    if (r != null && !string.IsNullOrEmpty(r.regionId))
                        regionNameCache[r.regionId] = r.displayName;
                }
            }
            return regionNameCache.TryGetValue(regionId, out string name) && !string.IsNullOrEmpty(name)
                ? name : regionId;
        }
    }
}
