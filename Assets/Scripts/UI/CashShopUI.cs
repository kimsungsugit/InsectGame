using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class CashShopUI : MonoBehaviour, IModalUI
    {
        private bool isOpen;
        private int selectedTab; // 0=보석충전, 1=아이템, 2=랜덤상자
        private readonly Vector2[] tabScrollPositions = new Vector2[3];
        private readonly UIDirectScroll[] tabDirectScrolls =
        {
            new UIDirectScroll(),
            new UIDirectScroll(),
            new UIDirectScroll()
        };
        private readonly float[] tabContentHeights = new float[3];

        // 가챠 연출
        private bool showingGachaResult;
        private float gachaAnimTimer;
        private GachaResult gachaResult;

        // 구매 피드백
        private string feedbackMessage;
        private float feedbackTimer;

        // 캐릭터 미리보기 회전
        private float previewRotate;

        // OnGUI 매 호출마다 FindFirstObjectByType 비용 회피용 캐시
        private OutfitBonusProvider cachedBonusProvider;
        private PlayerCurrencyWallet cachedWallet;
        private InsectDatabase cachedInsectDb;

        // 가챠 영역 OnGUI 매 호출 new GUIStyle 11개 회귀 제거용 캐시 (DexScreenUI/BattleTeamUI 패턴).
        private bool gachaStylesReady;
        private GUIStyle gachaWarningStyle;
        private GUIStyle boxCenterBoldStyle;
        private GUIStyle boxRateStyle;
        private GUIStyle boxPriceStyle;
        private GUIStyle boxOpenStyle;
        private GUIStyle gachaShakeStyle;
        private GUIStyle gachaSubtitleStyle;
        private GUIStyle gachaTitleStyle;
        private GUIStyle gachaNameStyle;
        private GUIStyle gachaExclusiveStyle;
        private GUIStyle gachaCandyBonusStyle;
        private GUIStyle gachaOkStyle;

        // boxId별 확률 표기 캐시 — GachaBoxManager(실제 드랍 임계값 단일 출처)에서 1회 파생.
        // 하드코딩 문자열이 실제 분포와 어긋나 확률 공시 위반(구글플레이/한국 게임법)되던 회귀 차단.
        private readonly Dictionary<string, string> gachaRateTextCache = new Dictionary<string, string>();

        /// <summary>
        /// 카드 라벨 리치텍스트 캐시. 품목 데이터(이름·설명·수량·보석가)는 불변인데 예전엔
        /// OnGUI 패스마다 다시 만들었다 — 아이템 탭 기준 <b>패스당 36개</b>이고 OnGUI는
        /// 프레임당 여러 패스를 돈다(위 <see cref="gachaRateTextCache"/>와 같은 방식).
        ///
        /// 키는 <c>itemId</c> 하나로 충분하다 — 세 탭의 품목 집합이 겹치지 않는다:
        /// 보석팩은 <c>GetGemPackages()</c>, 아이템 탭은 <c>gemPrice &gt; 0</c>만(보석팩은 0이라 제외),
        /// 가챠는 <c>box_*</c>(카테고리가 GachaBox라 아이템 탭에 안 잡힌다).
        /// </summary>
        private struct CardText
        {
            public string title;    // 이름(굵게)
            public string sub;      // 수량 배지 — 없으면 null
            public string desc;     // 설명
            public string price;    // 💎 보석가 — 실결제 카드엔 없다(null, 아래 주석 참조)
        }

        private readonly Dictionary<string, CardText> cardTextCache = new Dictionary<string, CardText>();
        private bool cardTextMobile;

        /// <summary>
        /// <c>&lt;size=…&gt;</c> 태그가 모바일/데스크톱에 따라 다르므로 레이아웃이 바뀌면(회전 등)
        /// 캐시를 통째로 버린다. 안 버리면 회전 후에도 옛 글자 크기가 그대로 남는다.
        /// </summary>
        private void EnsureCardTextLayout(bool mobile)
        {
            if (cardTextMobile == mobile) return;
            cardTextMobile = mobile;
            cardTextCache.Clear();
        }

        private CardText GetGemCardText(CashShopItem item)
        {
            if (cardTextCache.TryGetValue(item.itemId, out CardText cached)) return cached;
            CardText t;
            t.title = $"<size=31><b>{item.displayName}</b></size>";
            t.sub = null;
            t.desc = $"<size={(cardTextMobile ? 21 : 16)}>{item.description}</size>";
            // **실결제 가격은 캐시하지 않는다.** GetRealMoneyPriceText는 IAP 모듈이 준비되면
            // 폴백가(priceKRW)에서 스토어 현지화가로 바뀐다 — 굳히면 결제 화면에 틀린 가격이 남는다.
            // 카드당 1개뿐이라 매 패스 만들어도 이 캐시가 없애려는 규모(카드당 3~4개)가 아니다.
            t.price = null;
            cardTextCache[item.itemId] = t;
            return t;
        }

        private CardText GetItemCardText(CashShopItem item)
        {
            if (cardTextCache.TryGetValue(item.itemId, out CardText cached)) return cached;
            CardText t;
            t.title = $"<size=28><b>{item.displayName}</b></size>";
            t.sub = item.rewardCount > 1 ? $"<size=23>x{item.rewardCount}</size>" : null;
            t.desc = $"<size={(cardTextMobile ? 20 : 15)}>{item.description}</size>";
            // 보석가는 품목 데이터라 불변 — 실결제 가격과 달리 캐시해도 안전하다.
            t.price = $"<size=26><b>💎 {item.gemPrice}</b></size>";
            cardTextCache[item.itemId] = t;
            return t;
        }

        private CardText GetBoxCardText(string boxId, string title, int price)
        {
            if (cardTextCache.TryGetValue(boxId, out CardText cached)) return cached;
            CardText t;
            t.title = $"<size=31><b>{title}</b></size>";
            t.sub = null;
            t.desc = null;   // 확률표는 gachaRateTextCache가 따로 든다(공시 정합성 때문에 출처가 다르다)
            t.price = $"<size=28><b>💎 {price}</b></size>";
            cardTextCache[boxId] = t;
            return t;
        }

        private static readonly Color GachaPriceAffordCol = new Color(0.4f, 0.7f, 1f);
        private static readonly Color GachaRateGrayCol = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color GachaCandyPinkCol = new Color(1f, 0.6f, 0.8f);

        // OnGUI 본문 + Tab 0/Tab 1 캐시 (Tab 2와 같은 처리 패턴).
        private bool mainStylesReady;
        private GUIStyle resTitleStyle;
        private GUIStyle resStyle; // gems/coins textColor 동적 갱신
        private GUIStyle bonusTitleStyle;
        private GUIStyle bonusLineStyle;
        private GUIStyle headerTitleStyle;
        private GUIStyle headerGemsStyle;
        private GUIStyle tabStyle;
        private GUIStyle feedbackStyle;
        private GUIStyle infoGrayStyle;
        private GUIStyle gemLabelStyle;
        private GUIStyle shopCenterBoldStyle;
        private GUIStyle yellowDescStyle;
        private GUIStyle buyButtonStyle;
        private GUIStyle itemRewardCountStyle;
        private GUIStyle itemDescGrayStyle;
        private GUIStyle itemPriceStyle; // textColor 동적

        // OnGUI 매 프레임 new Color 회귀 제거용 (alpha/구성 고정).
        private static readonly Color BackdropDimCol = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color PanelBgCol = new Color(0.12f, 0.12f, 0.18f, 0.95f);
        private static readonly Color CharAreaBgCol = new Color(0.04f, 0.06f, 0.12f, 0.7f);
        private static readonly Color ResTitleSoftCol = new Color(0.85f, 0.9f, 1f);
        private static readonly Color CoinGoldCol = new Color(1f, 0.85f, 0.3f);
        private static readonly Color BonusGreenBoldCol = new Color(0.4f, 0.9f, 0.4f);
        private static readonly Color BonusGreenLightCol = new Color(0.7f, 0.95f, 0.7f);
        private static readonly Color GemLabelLightCol = new Color(0.9f, 0.95f, 1f);
        private static readonly Color BuyButtonGreenCol = new Color(0.2f, 0.8f, 0.3f);
        private static readonly Color GemGlowCol = new Color(0.2f, 0.4f, 0.9f, 0.3f);
        private static readonly Color GemBorderCol = new Color(0.4f, 0.6f, 1f, 0.9f);
        private static readonly Color GemGradTopCol = new Color(0.5f, 0.7f, 1f, 0.9f);
        private static readonly Color GemGradBotCol = new Color(0.2f, 0.4f, 0.85f, 0.9f);
        private static readonly Color GemHighlightCol = new Color(1f, 1f, 1f, 0.4f);

        private readonly string[] tabNames = { "보석 충전", "아이템 상점", "랜덤 상자" };

        private void InitMainStyles()
        {
            if (mainStylesReady) return;
            bool mobile = UIScale.IsMobileLayout;
            resTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = mobile ? 27 : 23, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            resTitleStyle.normal.textColor = ResTitleSoftCol;
            resStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            bonusTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            bonusTitleStyle.normal.textColor = BonusGreenBoldCol;
            bonusLineStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            bonusLineStyle.normal.textColor = BonusGreenLightCol;
            headerTitleStyle = new GUIStyle(GUI.skin.label) { richText = true, normal = { textColor = Color.cyan } };
            headerGemsStyle = new GUIStyle(GUI.skin.label) { richText = true, normal = { textColor = GachaPriceAffordCol } };
            tabStyle = new GUIStyle(GUI.skin.button) { fontSize = mobile ? 28 : 26, fontStyle = FontStyle.Bold };
            feedbackStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = Color.green } };
            infoGrayStyle = new GUIStyle(GUI.skin.label) { fontSize = mobile ? 20 : 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } };
            gemLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 31, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            gemLabelStyle.normal.textColor = GemLabelLightCol;
            shopCenterBoldStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
            yellowDescStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = Color.yellow } };
            buyButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = mobile ? 27 : 23, fontStyle = FontStyle.Bold };
            itemRewardCountStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
            itemDescGrayStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = Color.gray } };
            itemPriceStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
            mainStylesReady = true;
        }

        private void InitGachaStyles()
        {
            if (gachaStylesReady) return;
            gachaWarningStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = Color.yellow } };
            boxCenterBoldStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
            boxRateStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = UIScale.IsMobileLayout ? 21 : 18, normal = { textColor = GachaRateGrayCol } };
            boxPriceStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
            boxOpenStyle = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
            gachaShakeStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
            gachaSubtitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
            gachaTitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
            gachaNameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = Color.white } };
            gachaExclusiveStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = Color.yellow } };
            gachaCandyBonusStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = GachaCandyPinkCol } };
            gachaOkStyle = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
            gachaStylesReady = true;
        }

        public bool IsOpen => isOpen;

        public void Toggle()
        {
            isOpen = !isOpen;
            showingGachaResult = false;
            ResetAllTabScrolls();
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }

        public void CloseModal()
        {
            isOpen = false;
            showingGachaResult = false;
            ResetAllTabScrolls();
            ModalUIRegistry.Unregister(this);
        }

        /// <summary>월드 상호작용(상점/가챠 건물 접근)에서 특정 탭으로 바로 연다. 0=보석충전, 1=아이템, 2=랜덤상자.</summary>
        public void OpenAtTab(int tab)
        {
            selectedTab = Mathf.Clamp(tab, 0, 2);
            showingGachaResult = false;
            ResetAllTabScrolls();
            if (!isOpen)
            {
                isOpen = true;
                ModalUIRegistry.Register(this);
            }
        }

        private void Update()
        {
            // F4키는 QuickAccessBarUI에서 처리
            if (showingGachaResult)
                gachaAnimTimer += Time.deltaTime;

            if (feedbackTimer > 0f)
                feedbackTimer -= Time.deltaTime;

            if (isOpen)
                previewRotate += Time.deltaTime * 30f;
        }

        private void OnEnable()
        {
            if (GachaBoxManager.Instance != null)
                GachaBoxManager.Instance.BoxOpened += OnBoxOpened;
        }

        private void OnDisable()
        {
            if (GachaBoxManager.Instance != null)
                GachaBoxManager.Instance.BoxOpened -= OnBoxOpened;
            // isOpen을 남겨 두면 **레지스트리엔 없는데 열린 것으로 아는** 상태가 된다 —
            // ESC가 이 모달을 건너뛰어 안 닫히고, 다음 [F4]는 여는 대신 닫는 쪽으로 간다.
            // 오프닝 다시보기가 UI 루트를 통째로 껐다 켜므로 실제로 도달하는 경로다
            // (StoryJournalUI·NpcDialogueUI가 같은 이유로 같은 처리를 한다).
            isOpen = false;
            showingGachaResult = false;
            ResetAllTabScrolls();
            ModalUIRegistry.Unregister(this);
        }

        private void OnBoxOpened(GachaResult result)
        {
            gachaResult = result;
            showingGachaResult = true;
            gachaAnimTimer = 0f;
            ResetAllTabScrolls();

            if (AudioManager.Instance != null)
            {
                if (result.rarity >= InsectRarity.Epic)
                    AudioManager.Instance.PlaySFX(SfxType.Victory);
                else
                    AudioManager.Instance.PlaySFX(SfxType.CaptureSuccess);
            }
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            InitMainStyles();

            // 다른 모달과 동일하게 가상 해상도(1920x1080 / 1080x1920) 좌표계로 통일.
            UIScale.Begin();

            float vw = UIScale.VirtualScreenWidth;
            float vh = UIScale.VirtualScreenHeight;

            // 전체화면 반투명 배경
            GUI.color = BackdropDimCol;
            GUI.DrawTexture(new Rect(0, 0, vw, vh), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 모바일 세로는 긴 화면(1920+)을 더 활용해 확대된 카드의 스크롤을 줄임. 데스크톱은 기존 유지.
            // 세이프에어리어 + 세로 마진은 하네스가 뺀다.
            Rect panelRect = UISafeLayout.CenteredPanel(1200f, UIScale.IsMobileLayout ? 1560f : 820f);
            float panelW = panelRect.width;
            float panelH = panelRect.height;
            float px = panelRect.x;
            float py = panelRect.y;

            // 패널 배경
            UISurface.Card(panelRect, PanelBgCol, UITheme.Instance.surfaceBorder);
            GUI.color = Color.white;

            // ── 좌측 캐릭터 미리보기 영역 (가챠 결과 화면에서는 숨김) ──
            bool mobile = UIScale.IsMobileLayout;
            if (!showingGachaResult && !mobile)
            {
                float charAreaW = 340f;
                float charAreaH = panelH - 60f;
                Rect charArea = new Rect(px + 16, py + 50, charAreaW, charAreaH);
                UISurface.Card(charArea, CharAreaBgCol, UITheme.Instance.surfaceBorder);
                GUI.color = Color.white;

                float charCx = charArea.x + charAreaW * 0.5f;
                float charCy = charArea.y + charAreaH * 0.40f;
                float charScale = 2.2f;
                float swayX = Mathf.Sin(previewRotate * Mathf.Deg2Rad) * 12f * charScale;
                CharacterPortraitRenderer.DrawWithOutfit(charCx, charCy, charScale, swayX);

                // 재화 정보 (캐릭터 아래) — 폰트 확대에 맞춰 시작 오프셋/행 높이 확대.
                float infoY = charArea.y + charAreaH - 220f;
                int gemsLeft = CashShopManager.Instance != null ? CashShopManager.Instance.Gems : 0;
                int coinsLeft = 0;
                if (cachedWallet == null) cachedWallet = FindFirstObjectByType<PlayerCurrencyWallet>();
                if (cachedWallet != null) coinsLeft = cachedWallet.Coins;

                GUI.Label(new Rect(charArea.x + 16, infoY, charAreaW - 32, 34), "보유 재화", resTitleStyle);

                // resStyle.normal.textColor는 gems/coins 라인마다 동적 갱신.
                resStyle.normal.textColor = GachaPriceAffordCol;
                GUI.Label(new Rect(charArea.x + 16, infoY + 38, charAreaW - 32, 36), $"💎 {gemsLeft}", resStyle);
                resStyle.normal.textColor = CoinGoldCol;
                GUI.Label(new Rect(charArea.x + 16, infoY + 78, charAreaW - 32, 36), $"🪙 {coinsLeft}", resStyle);

                // 장비 보너스 요약
                if (cachedBonusProvider == null) cachedBonusProvider = FindFirstObjectByType<OutfitBonusProvider>();
                OutfitBonusProvider bonusProv = cachedBonusProvider;
                if (bonusProv != null)
                {
                    OutfitStatBonus total = bonusProv.GetTotalBonus();
                    if (total.HasAnyBonus())
                    {
                        GUI.Label(new Rect(charArea.x + 16, infoY + 120, charAreaW - 32, 30), "장비 보너스", bonusTitleStyle);

                        string bonusText = "";
                        if (total.captureChanceBonus > 0f) bonusText += $"포획 +{total.captureChanceBonus * 100f:0}%   ";
                        if (total.atkBonus > 0f) bonusText += $"ATK +{total.atkBonus * 100f:0}%   ";
                        if (total.defBonus > 0f) bonusText += $"DEF +{total.defBonus * 100f:0}%   ";
                        if (total.moveSpeedBonus > 0f) bonusText += $"이속 +{total.moveSpeedBonus * 100f:0}%   ";
                        if (total.expMultiplier > 0f) bonusText += $"EXP +{total.expMultiplier * 100f:0}%   ";
                        if (total.candyMultiplier > 0f) bonusText += $"캔디 +{total.candyMultiplier * 100f:0}%   ";
                        if (total.rareSpawnBonus > 0f) bonusText += $"레어 +{total.rareSpawnBonus * 100f:0}%";
                        GUI.Label(new Rect(charArea.x + 16, infoY + 152, charAreaW - 32, 66), bonusText.Trim(), bonusLineStyle);
                    }
                }
            }

            // 우측 콘텐츠 영역
            float rightX = showingGachaResult || mobile ? px : px + 372f;
            float rightW = showingGachaResult || mobile ? panelW : panelW - 388f;
            Rect contentArea = new Rect(rightX, py, rightW, panelH);
            GUILayout.BeginArea(contentArea);

            // -- 헤더 --
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=39><b>  보석 상점</b></size>", headerTitleStyle);
            GUILayout.FlexibleSpace();
            int gems = CashShopManager.Instance != null ? CashShopManager.Instance.Gems : 0;
            GUILayout.Label($"<size=28><b>💎 {gems}</b></size>", headerGemsStyle);
            GUILayout.Space(14);
            if (GUILayout.Button("X", GUILayout.Width(mobile ? 62 : 52), GUILayout.Height(mobile ? 60 : 48)))
                Toggle();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // -- 가챠 연출 중이면 결과 화면만 표시 --
            if (showingGachaResult && gachaResult != null)
            {
                DrawGachaResultScreen(rightW, panelH);
                GUILayout.EndArea();
                UIScale.End();
                return;
            }

            // -- 탭 --
            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabNames.Length; i++)
            {
                GUI.color = (selectedTab == i) ? Color.cyan : Color.gray;
                if (GUILayout.Button(tabNames[i], tabStyle, GUILayout.Height(mobile ? 64 : 52)))
                    SelectTab(i);
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(14);

            // -- 피드백 메시지 --
            if (feedbackTimer > 0f && !string.IsNullOrEmpty(feedbackMessage))
            {
                GUILayout.Label($"<size=28><b>{feedbackMessage}</b></size>", feedbackStyle);
                GUILayout.Space(8);
            }

            // -- 탭 콘텐츠 --
            DrawScrollableTab(contentArea);

            GUILayout.EndArea();

            UIScale.End();
        }

        private void SelectTab(int tab)
        {
            int nextTab = Mathf.Clamp(tab, 0, tabNames.Length - 1);
            if (selectedTab == nextTab) return;
            selectedTab = nextTab;
            ResetTabScroll(selectedTab);
        }

        private void ResetAllTabScrolls()
        {
            for (int i = 0; i < tabScrollPositions.Length; i++)
                ResetTabScroll(i);
        }

        private void ResetTabScroll(int tab)
        {
            if (tab < 0 || tab >= tabScrollPositions.Length) return;
            tabScrollPositions[tab] = Vector2.zero;
            tabContentHeights[tab] = 0f;
            tabDirectScrolls[tab].Reset();
        }

        /// <summary>직전 Repaint에 잰 탭별 스크롤뷰 영역(패널 로컬 좌표). 터치 드래그 판정에만 쓴다.</summary>
        private readonly Rect[] tabViewports = new Rect[3];

        /// <summary>
        /// 탭 콘텐츠를 스크롤 영역에 그린다.
        ///
        /// <b>레이아웃 스크롤뷰(<c>GUILayout.BeginScrollView</c>)를 쓴다.</b> 한때
        /// <c>GUI.BeginScrollView</c> + 그 안의 <c>GUILayout.BeginArea</c>로 좌표계를 직접 리셋했는데,
        /// <c>OnGUI</c>가 이미 <c>GUILayout.BeginArea(contentArea)</c>를 열어 둔 상태라 <b>Area 중첩</b>이 됐다.
        /// Unity는 Area 중첩을 지원하지 않는다("Areas cannot be nested") — 레이아웃 그룹 스택이 어긋나
        /// <b>탭 버튼은 그려지는데 그 아래 카드가 하나도 나오지 않았다</b>(세 탭 전부).
        /// 스크롤 위치·콘텐츠 높이·뷰포트는 레이아웃 시스템이 스스로 관리하므로 여기서 잴 필요가 없다.
        /// </summary>
        private void DrawScrollableTab(Rect contentArea)
        {
            int tab = Mathf.Clamp(selectedTab, 0, tabNames.Length - 1);

            // 터치 드래그(UIDirectScroll)는 **화면 좌표** 뷰포트가 필요한데 레이아웃 스크롤뷰는
            // 자기 Rect를 돌려주지 않는다. 직전 Repaint에 재둔 값을 쓴다 — 한 프레임 늦지만
            // 패널 크기는 매 프레임 바뀌지 않는다(회전 시 한 프레임만 어긋난다).
            Vector2 position = tabScrollPositions[tab];
            Rect measured = tabViewports[tab];
            if (measured.height > 1f)
            {
                Rect directViewport = new Rect(
                    contentArea.x + measured.x,
                    contentArea.y + measured.y,
                    measured.width,
                    measured.height);
                float horizontalPosition = position.x;
                tabDirectScrolls[tab].Handle(
                    ref position,
                    directViewport,
                    Mathf.Max(measured.height, tabContentHeights[tab]),
                    UIScale.IsMobileLayout ? 72f : 52f);
                position.x = horizontalPosition;
            }

            position = GUILayout.BeginScrollView(
                position,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));

            // **콘텐츠 최소 폭을 명시한다.** 데스크톱 카드 가로배치는 뷰포트보다 넓다 —
            // 가챠 상자 3장이 300×3 + 간격 45 = 945px인데 콘텐츠 영역은 812px다.
            // 안 주면 레이아웃이 카드를 뷰포트에 욱여넣어 **찌그러뜨린다**(상자가 안 보인다는 증상).
            // 옛 `GUI.BeginScrollView` 경로는 contentWidth를 960/840으로 직접 줘서 가로 스크롤이
            // 생겼는데, 레이아웃 스크롤뷰로 바꾸며 그 지정이 빠진 것이 회귀였다.
            float minContentW = UIScale.IsMobileLayout ? 0f : (tab == 2 ? 960f : 840f);
            if (minContentW > 0f) GUILayout.BeginVertical(GUILayout.MinWidth(minContentW));
            else GUILayout.BeginVertical();

            switch (tab)
            {
                case 0: DrawGemTab(); break;
                case 1: DrawItemTab(); break;
                case 2: DrawGachaTab(); break;
            }
            GUILayout.EndVertical();
            // 콘텐츠 높이 — 위 수직 그룹의 Rect가 곧 그려진 높이다(터치 드래그의 스크롤 한계용).
            if (Event.current.type == EventType.Repaint)
                tabContentHeights[tab] = GUILayoutUtility.GetLastRect().height;

            GUILayout.EndScrollView();

            if (Event.current.type == EventType.Repaint)
                tabViewports[tab] = GUILayoutUtility.GetLastRect();

            tabScrollPositions[tab] = position;
        }

        // ===== Tab 0: 보석 충전 =====
        private void DrawGemTab()
        {
            if (CashShopManager.Instance == null) { GUILayout.Label("상점 초기화 중..."); return; }

            CashShopItem[] gemPacks = CashShopManager.Instance.GetGemPackages();

            GUILayout.Space(20);
            bool mobile = UIScale.IsMobileLayout;
            EnsureCardTextLayout(mobile);
            if (!mobile)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
            }

            foreach (var item in gemPacks)
            {
                if (mobile)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                }
                DrawGemCard(item);
                if (mobile)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(14);
                }
                else GUILayout.Space(15);
            }

            if (!mobile)
            {
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(30);
            GUILayout.Label("* 현재 테스트 모드: 결제 없이 즉시 지급됩니다.", infoGrayStyle);
        }

        private void DrawGemCard(CashShopItem item)
        {
            bool mobile = UIScale.IsMobileLayout;
            // 모바일(1열 세로)은 폭을 넉넉히, 데스크톱(가로행)은 폭 제약 유지 + 높이만 확대(스크롤 흡수).
            float cardW = mobile ? Mathf.Min(520f, UIScale.VirtualScreenWidth * 0.82f) : 260f;
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(cardW), GUILayout.Height(mobile ? 380f : 300f));

            // 보석 아이콘 (다이아몬드 형태)
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect gemRect = GUILayoutUtility.GetRect(70, 70);
            // 배경 글로우
            GUI.color = GemGlowCol;
            GUI.DrawTexture(new Rect(gemRect.x - 5, gemRect.y - 5, 80, 80), Texture2D.whiteTexture);
            // 외곽 테두리
            GUI.color = GemBorderCol;
            GUI.DrawTexture(new Rect(gemRect.x, gemRect.y, 70, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(gemRect.x, gemRect.y + 68, 70, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(gemRect.x, gemRect.y, 2, 70), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(gemRect.x + 68, gemRect.y, 2, 70), Texture2D.whiteTexture);
            // 내부 그라디언트 (위쪽 밝게)
            GUI.color = GemGradTopCol;
            GUI.DrawTexture(new Rect(gemRect.x + 4, gemRect.y + 4, 62, 30), Texture2D.whiteTexture);
            GUI.color = GemGradBotCol;
            GUI.DrawTexture(new Rect(gemRect.x + 4, gemRect.y + 34, 62, 32), Texture2D.whiteTexture);
            // 하이라이트
            GUI.color = GemHighlightCol;
            GUI.DrawTexture(new Rect(gemRect.x + 12, gemRect.y + 10, 20, 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(gemRect, "GEM", gemLabelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // 라벨은 GetGemCardText가 품목당 1회만 굽는다(실결제 가격은 예외 — 아래 참조).
            CardText text = GetGemCardText(item);
            GUILayout.Label(text.title, shopCenterBoldStyle);
            // 설명은 데스크톱 좁은 카드(260px)에서 잘림 방지 위해 모바일에서만 크게.
            GUILayout.Label(text.desc, yellowDescStyle);

            GUILayout.FlexibleSpace();
            // 실결제 모듈 준비 시 스토어 현지화 가격(실제 청구액과 일치), 아니면 priceKRW 폴백.
            GUILayout.Label($"<size=28><b>{CashShopManager.Instance.GetRealMoneyPriceText(item.itemId)}</b></size>", shopCenterBoldStyle);
            GUILayout.Space(8);

            // 결제 모듈 미준비(프로덕션)면 버튼 비활성 — 무료 지급/먹통 버튼 방지.
            bool canBuy = CashShopManager.Instance.CanBuyRealMoney;
            GUI.color = canBuy ? BuyButtonGreenCol : new Color(0.4f, 0.4f, 0.45f);
            GUI.enabled = canBuy;
            if (GUILayout.Button(canBuy ? "구매" : "준비 중", buyButtonStyle, GUILayout.Height(mobile ? 64 : 50)))
            {
                // 실결제는 비동기(완료 콜백에서 지급) → 중립 문구. 실제 충전은 보석 수 갱신으로 반영.
                if (CashShopManager.Instance.PurchaseWithRealMoney(item.itemId))
                    ShowFeedback("구매를 처리합니다...");
            }
            GUI.enabled = true;
            GUI.color = Color.white;

            GUILayout.EndVertical();
        }

        // ===== Tab 1: 아이템 상점 =====
        private void DrawItemTab()
        {
            if (CashShopManager.Instance == null) { GUILayout.Label("상점 초기화 중..."); return; }

            CashShopItem[] items = CashShopManager.Instance.GetItemsByCategory(CashItemCategory.MinigameItem);
            int gems = CashShopManager.Instance.Gems;
            bool mobile = UIScale.IsMobileLayout;
            EnsureCardTextLayout(mobile);
            int col = 0;

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            foreach (var item in items)
            {
                if (item.gemPrice <= 0) continue; // 보석팩 제외

                DrawItemCard(item, gems);
                col++;
                int columns = mobile ? 1 : 3;
                if (col % columns == 0)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    GUILayout.Space(10);
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawItemCard(CashShopItem item, int currentGems)
        {
            bool mobile = UIScale.IsMobileLayout;
            // 데스크톱은 3열 가로배치라 폭(260) 유지, 모바일(1열)만 폭 확대. 높이는 스크롤 흡수.
            float cardW = mobile ? Mathf.Min(520f, UIScale.VirtualScreenWidth * 0.82f) : 260f;
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(cardW), GUILayout.Height(mobile ? 330f : 260f));

            CardText text = GetItemCardText(item);
            GUILayout.Label(text.title, shopCenterBoldStyle);
            if (text.sub != null)
                GUILayout.Label(text.sub, itemRewardCountStyle);
            // 긴 설명 — 데스크톱 좁은 카드에서 잘림 방지 위해 모바일에서만 크게.
            GUILayout.Label(text.desc, itemDescGrayStyle);

            GUILayout.FlexibleSpace();

            bool canAfford = currentGems >= item.gemPrice;
            // itemPriceStyle 재사용 + textColor 동적 갱신.
            itemPriceStyle.normal.textColor = canAfford ? GachaPriceAffordCol : Color.red;
            GUILayout.Label(text.price, itemPriceStyle);

            GUILayout.Space(6);

            if (!canAfford)
            {
                GUI.enabled = false;
                GUILayout.Button("보석 부족", buyButtonStyle, GUILayout.Height(mobile ? 64 : 50));
                GUI.enabled = true;
            }
            else
            {
                GUI.color = ItemBuyBlueCol;
                if (GUILayout.Button("구매", buyButtonStyle, GUILayout.Height(mobile ? 64 : 50)))
                {
                    if (CashShopManager.Instance.PurchaseWithGems(item.itemId))
                        ShowFeedback($"{item.displayName} 획득!");
                    else
                        ShowFeedback("구매 실패!");
                }
                GUI.color = Color.white;
            }

            GUILayout.EndVertical();
        }

        private static readonly Color ItemBuyBlueCol = new Color(0.2f, 0.7f, 0.9f);

        // 박스 테마 색상 (매 프레임 new Color 제거).
        private static readonly Color BoxBronzeCol = new Color(0.6f, 0.4f, 0.2f);
        private static readonly Color BoxSilverCol = new Color(0.7f, 0.7f, 0.8f);
        private static readonly Color BoxGoldCol = new Color(1f, 0.85f, 0.3f);

        /// <summary>상자 그림 한 변(px). 카드 폭과 무관하게 고정 — 3장이 같은 크기로 보여야 한다.</summary>
        private const float ChestIconSize = 120f;
        /// <summary>가챠 결과의 곤충 초상 자리(px). 초상 자체는 고정 크기라 이 값은 <b>레이아웃 높이</b>다.</summary>
        private const float GachaPortraitSize = 140f;
        // 상자 그림자 — 테마색에서 파생되지 않는 유일한 색이라 상수로 둔다(팔레트 추가 아님).
        private static readonly Color ChestShadowCol = new Color(0f, 0f, 0f, 0.26f);

        /// <summary>
        /// 보물상자 하나를 <b>프로시저럴로</b> 그린다. 이 저장소엔 상자 아트가 없으므로
        /// <see cref="UIShapes.Part"/>를 조합한다 — <c>DexScreenUI</c>가 곤충 44종을 그리는 방식과 같다.
        ///
        /// 좌표는 전부 <paramref name="r"/>에 대한 <b>비율</b>이다. 카드 크기가 바뀌거나
        /// 연출 화면에서 더 크게 그려도 비례가 유지된다.
        ///
        /// 명암은 <paramref name="theme"/>에서 <c>Lerp</c>로 파생한다 — 브론즈/실버/골드 3종에
        /// 각각 팔레트를 만들면 색이 6개로 늘고 테마색을 바꿀 때 따로 논다(rules/ui-layout.md).
        ///
        /// <b>roundness는 0(각짐) 아니면 1(타원)만 쓴다.</b> 중간값은 disc 위에 인셋 사각형을
        /// 덮어 만드는 방식(<see cref="UIShapes.Part"/>)이라 옆구리가 오목하게 파여
        /// 상자가 <b>모래시계처럼</b> 보인다 — 실제로 0.16을 줬다가 그렇게 나왔다.
        /// 각진 몸통과 둥근 뚜껑은 섞지 말고 <b>겹쳐서</b> 만든다.
        /// </summary>
        private static void DrawChest(Rect r, Color theme)
        {
            float x = r.x, y = r.y, w = r.width, h = r.height;
            if (w <= 1f || h <= 1f) return;

            Color lidCol = Color.Lerp(theme, Color.white, 0.30f);   // 뚜껑은 빛을 받는다
            Color seamCol = Color.Lerp(theme, Color.black, 0.45f);
            Color bandCol = Color.Lerp(theme, Color.black, 0.62f);  // 세로 금속 밴드
            Color lockCol = Color.Lerp(theme, Color.white, 0.55f);
            Color rimCol = Color.Lerp(theme, Color.black, 0.28f);   // 바닥 굽

            // 바닥 그림자 — 없으면 상자가 공중에 뜬 스티커처럼 보인다.
            UIShapes.Part(new Rect(x + w * 0.14f, y + h * 0.87f, w * 0.72f, h * 0.085f), ChestShadowCol);

            // 뚜껑 돔(순수 타원). 아랫부분은 이음매와 몸통이 덮어 반원만 남는다.
            UIShapes.Part(new Rect(x + w * 0.10f, y + h * 0.20f, w * 0.80f, h * 0.36f), lidCol);

            // 몸통(각진 사각형) — 뚜껑보다 좁아 뚜껑이 턱처럼 내민다.
            UIShapes.Part(new Rect(x + w * 0.135f, y + h * 0.52f, w * 0.73f, h * 0.35f), theme, 0f);

            // 뚜껑과 몸통의 이음매 — 돔의 둥근 아랫변을 끊어 상자로 만든다.
            UIShapes.Part(new Rect(x + w * 0.10f, y + h * 0.475f, w * 0.80f, h * 0.06f), seamCol, 0f);

            // 바닥 굽
            UIShapes.Part(new Rect(x + w * 0.115f, y + h * 0.835f, w * 0.77f, h * 0.045f), rimCol, 0f);

            // 세로 금속 밴드 2줄 — 돔 안쪽에서 시작해 굽까지 내려온다.
            UIShapes.Part(new Rect(x + w * 0.27f, y + h * 0.30f, w * 0.065f, h * 0.575f), bandCol, 0f);
            UIShapes.Part(new Rect(x + w * 0.665f, y + h * 0.30f, w * 0.065f, h * 0.575f), bandCol, 0f);

            // 자물쇠판 + 열쇠구멍(원 + 아래로 뻗은 홈)
            UIShapes.Part(new Rect(x + w * 0.44f, y + h * 0.455f, w * 0.12f, h * 0.165f), lockCol, 0f);
            UIShapes.Part(new Rect(x + w * 0.478f, y + h * 0.505f, w * 0.044f, h * 0.05f), seamCol);
            UIShapes.Part(new Rect(x + w * 0.492f, y + h * 0.535f, w * 0.016f, h * 0.045f), seamCol, 0f);
        }

        // ===== Tab 2: 랜덤 상자 =====
        private void DrawGachaTab()
        {
            if (CashShopManager.Instance == null) { GUILayout.Label("상점 초기화 중..."); return; }

            InitGachaStyles();
            int gems = CashShopManager.Instance.Gems;
            bool mobile = UIScale.IsMobileLayout;
            EnsureCardTextLayout(mobile);

            GUILayout.Space(15);
            if (!mobile)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
            }

            DrawResponsiveBoxCard("box_bronze", "브론즈 상자", BoxBronzeCol,
                GetGachaRateText("box_bronze"), 500, gems, mobile);

            DrawResponsiveBoxCard("box_silver", "실버 상자", BoxSilverCol,
                GetGachaRateText("box_silver"), 600, gems, mobile);

            DrawResponsiveBoxCard("box_gold", "골드 상자", BoxGoldCol,
                GetGachaRateText("box_gold"), 750, gems, mobile);

            if (!mobile)
            {
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(20);
            GUILayout.Label("<size=17><b>* 상자 전용 곤충 10종 포함! (필드에서 만날 수 없음)</b></size>", gachaWarningStyle);
        }

        // 확률 표기는 GachaBoxManager(실제 드랍 임계값)에서 파생 — 하드코딩 금지(공시 위반 방지).
        // OnGUI 매 프레임 재계산 피하려 boxId별 1회 캐싱. Instance 미준비 시 빈 문자열(다음 프레임 재시도).
        private string GetGachaRateText(string boxId)
        {
            if (gachaRateTextCache.TryGetValue(boxId, out string cached))
                return cached;

            var mgr = GachaBoxManager.Instance;
            if (mgr == null) return string.Empty; // 아직 미초기화 — 캐시하지 않고 다음 프레임 재시도

            string text = mgr.GetRateText(boxId);
            gachaRateTextCache[boxId] = text;
            return text;
        }

        private void DrawResponsiveBoxCard(string boxId, string title, Color themeColor,
            string rateText, int price, int currentGems, bool mobile)
        {
            if (mobile)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
            }
            DrawBoxCard(boxId, title, themeColor, rateText, price, currentGems);
            if (mobile)
            {
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(15);
            }
            else GUILayout.Space(15);
        }

        private void DrawBoxCard(string boxId, string title, Color themeColor, string rateText, int price, int currentGems)
        {
            InitGachaStyles();
            // 가격은 CashShopManager(실제 차감 출처)에서 — 표시가/게이팅이 실제 gemPrice와 어긋나지 않도록.
            var boxItem = CashShopManager.Instance != null ? CashShopManager.Instance.GetItem(boxId) : null;
            if (boxItem != null && boxItem.gemPrice > 0) price = boxItem.gemPrice;
            bool mobile = UIScale.IsMobileLayout;
            // 데스크톱은 3개 가로배치라 폭(300) 유지, 모바일(1열)만 폭 확대. 높이는 스크롤 흡수.
            float cardW = mobile ? Mathf.Min(560f, UIScale.VirtualScreenWidth * 0.86f) : 300f;
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(cardW), GUILayout.Height(mobile ? 470f : 420f));

            // 상자 그림 — 예전엔 `GUILayout.Box("")`를 테마색으로 틴트한 **빈 정사각형**이었다.
            // 내장 스킨의 배경만 칠해지므로 화면엔 갈색/은색/금색 네모만 떴고, 그게
            // "상자 사진이 안 나온다"로 읽혔다. 이 저장소엔 상자 아트가 없다(이미지 에셋 9개가
            // 전부 앱 아이콘·오프닝·TMP 이모지다) — 그리려면 그려야 한다.
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect chestRect = GUILayoutUtility.GetRect(ChestIconSize, ChestIconSize,
                GUILayout.Width(ChestIconSize), GUILayout.Height(ChestIconSize));
            // 레이아웃 패스의 Rect는 아직 확정 전이라 그리면 엉뚱한 자리에 찍힌다.
            if (Event.current.type == EventType.Repaint) DrawChest(chestRect, themeColor);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            CardText text = GetBoxCardText(boxId, title, price);
            GUILayout.Label(text.title, boxCenterBoldStyle);

            GUILayout.Space(12);

            // 확률표
            GUILayout.Label(rateText, boxRateStyle);

            GUILayout.FlexibleSpace();

            bool canAfford = currentGems >= price;
            // 스타일 재사용 + textColor만 동적 갱신 (BattleScreenUI/DexScreenUI 패턴)
            boxPriceStyle.normal.textColor = canAfford ? GachaPriceAffordCol : Color.red;
            GUILayout.Label(text.price, boxPriceStyle);

            GUILayout.Space(8);

            if (!canAfford)
            {
                GUI.enabled = false;
                GUILayout.Button("보석 부족", boxOpenStyle, GUILayout.Height(56));
                GUI.enabled = true;
            }
            else
            {
                GUI.color = themeColor;
                if (GUILayout.Button("열기!", boxOpenStyle, GUILayout.Height(56)))
                {
                    CashShopManager.Instance.PurchaseWithGems(boxId);
                }
                GUI.color = Color.white;
            }

            GUILayout.EndVertical();
        }

        // 가챠 Phase 1 깜빡임 Lerp 양 끝 색상 (매 프레임 new Color 제거)
        private static readonly Color GachaFlashBlue = new Color(0.3f, 0.5f, 1f);
        private static readonly Color GachaFlashGold = new Color(1f, 0.85f, 0.3f);

        /// <summary>
        /// 결과 초상에 쓸 3D 썸네일을 미리 굽게 한다.
        ///
        /// 렌더러는 <b>프레임당 1장</b>만 굽고 첫 요청엔 null을 돌려준다(그동안 호출부는 2D 폴백을
        /// 그린다). Phase 3에서 처음 부르면 결과가 열리는 프레임에 2D가 한 번 보였다가 3D로 튀고,
        /// 하필 그 프레임에 <c>InsectEntity</c>를 통째로 만들었다 부수는 렌더 비용까지 겹친다 —
        /// 연출의 절정에서 딱 한 번 끊기는 셈이다. 상자가 흔들리는 1.5초 구간으로 옮겨 둔다.
        /// </summary>
        private void PrewarmGachaThumbnail()
        {
            if (InsectVisual.Renderer == null || gachaResult == null) return;
            if (cachedInsectDb == null) cachedInsectDb = FindFirstObjectByType<InsectDatabase>();
            InsectData data = cachedInsectDb != null ? cachedInsectDb.GetById(gachaResult.insectId) : null;
            // 캐시 적중 뒤에도 매 패스 불리지만 그때는 Dictionary 조회 + LRU 갱신뿐이다.
            if (data != null) InsectVisual.Renderer.GetThumbnail(data, false);
        }

        // ===== 가챠 연출 화면 =====
        /// <param name="contentW">감싸는 <c>BeginArea</c>의 폭. Phase 2 플래시가 화면을 꽉 채우는 데 쓴다.</param>
        /// <param name="contentH">같은 높이.</param>
        private void DrawGachaResultScreen(float contentW, float contentH)
        {
            InitGachaStyles();

            // Phase 1 (0~1.5초): 상자 흔들림 + 깜빡임
            if (gachaAnimTimer < 1.5f)
            {
                GUILayout.FlexibleSpace();
                float flash = Mathf.PingPong(gachaAnimTimer * 6f, 1f);
                Color flashCol = Color.Lerp(GachaFlashBlue, GachaFlashGold, flash);

                // 주석이 "상자 흔들림"이라 적혀 있었지만 실제로는 `???` 글자만 떨었다 —
                // 상자를 여는 연출인데 정작 상자가 없었다. 카드와 같은 그림을 흔든다.
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                float shakeSize = ChestIconSize * 1.35f;
                Rect shakeRect = GUILayoutUtility.GetRect(shakeSize, shakeSize,
                    GUILayout.Width(shakeSize), GUILayout.Height(shakeSize));
                if (Event.current.type == EventType.Repaint)
                {
                    // 좌우로 떨고 뚜껑이 들썩이듯 살짝 위아래로. 주기를 다르게 줘 기계적이지 않게.
                    shakeRect.x += Mathf.Sin(gachaAnimTimer * 34f) * shakeSize * 0.045f;
                    shakeRect.y += Mathf.Abs(Mathf.Sin(gachaAnimTimer * 11f)) * shakeSize * -0.03f;
                    DrawChest(shakeRect, flashCol);
                    PrewarmGachaThumbnail();
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUI.color = flashCol;
                GUILayout.Label("<size=52><b>???</b></size>", gachaShakeStyle);
                GUI.color = Color.white;

                GUILayout.Label("<size=28>상자를 여는 중...</size>", gachaSubtitleStyle);
                GUILayout.FlexibleSpace();
                return;
            }

            // Phase 2 (1.5~2초): 밝은 플래시 (struct new Color는 stack — GC 없음, 유지)
            //
            // 크기는 **감싸는 Area에서 받는다.** 예전엔 850x620 하드코딩이었는데 결과 화면의
            // Area는 1200x820(데스크톱) / 1200x1560(모바일)이라, 플래시가 좌상단만 덮고
            // 우·하단은 그대로 남는 **각진 흰 판**으로 보였다(모바일에선 높이의 40%).
            if (gachaAnimTimer < 2f)
            {
                float alpha = 1f - (gachaAnimTimer - 1.5f) * 2f;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(0, 0, contentW, contentH), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // Phase 3 (2초+): 결과 표시
            if (gachaAnimTimer >= 2f)
            {
                GUILayout.FlexibleSpace();

                // 등급별 배경색 플래시 (Legendary = 금색)
                Color rarityColor = GetRarityColor(gachaResult.rarity);

                // 등급 텍스트
                string rarityLabel = gachaResult.rarity.ToString().ToUpper();
                string stars = "";
                switch (gachaResult.rarity)
                {
                    case InsectRarity.Common:    stars = ""; break;
                    case InsectRarity.Uncommon:  stars = "* "; break;
                    case InsectRarity.Rare:      stars = "** "; break;
                    case InsectRarity.Epic:      stars = "*** "; break;
                    case InsectRarity.Legendary: stars = "**** "; break;
                }

                // 스타일 재사용 + textColor만 동적 갱신
                gachaTitleStyle.normal.textColor = rarityColor;
                GUILayout.Label($"<size=47><b>{stars}{rarityLabel}!{stars}</b></size>", gachaTitleStyle);
                GUILayout.Space(20);

                // 뽑은 곤충 초상. 예전엔 등급 색으로 칠한 **빈 정사각형**이라, 정작 무엇을 뽑았는지
                // 그림으로는 알 수 없었다(이름 라벨만 있었다).
                //
                // 그림 한 장의 단일 진입점은 `InsectVisual.Draw`다 — 3D 썸네일이 있으면 그것,
                // 없으면 안에서 `DrawTypedInsectPortrait`로 내려간다. 한때 여기서 2D 폴백을
                // **직접** 불렀는데, 그러면 다른 9개 화면(도감·보유곤충·팀편성·병원·훈련·포획선택·
                // 지역맵)이 전부 3D 모델을 보여주는 동안 **가챠 결과만 홀로 2D**가 된다.
                // 돈을 쓴 직후 화면이라 품질 격차가 가장 크게 보이는 자리다.
                //
                // 새 분기 사슬을 만들지 않는 이유는 그대로다 — 2026-08-07에 `"beetle"`이 `"bee"`에
                // 가려져 딱정벌레 31종이 4개 화면에서 벌로 그려진 전례가 있다(InsectPortraitRoutingTests).
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                Rect portraitRect = GUILayoutUtility.GetRect(GachaPortraitSize, GachaPortraitSize,
                    GUILayout.Width(GachaPortraitSize), GUILayout.Height(GachaPortraitSize));
                if (Event.current.type == EventType.Repaint)
                {
                    Color previousColor = GUI.color;
                    if (cachedInsectDb == null) cachedInsectDb = FindFirstObjectByType<InsectDatabase>();
                    InsectData drawn = cachedInsectDb != null ? cachedInsectDb.GetById(gachaResult.insectId) : null;
                    if (drawn != null)
                    {
                        // 가챠 결과는 샤이니 개념이 없다(GachaResult에 필드 자체가 없고
                        // AddCapturedInsect가 지급 시점에 굴린다) — 늘 false다.
                        InsectVisual.Draw(portraitRect, drawn, false, 1f);
                    }
                    else
                    {
                        // DB 미로드 등으로 조회가 빈 경우. `InsectVisual.Draw`는 data가 null이면
                        // **아무것도 그리지 않으므로**, 빈 사각형 회귀를 막으려면 여기서 직접 폴백한다.
                        // 초상은 (cx, cy) 중심에 고정 크기로 그려진다 — 상자 크기와 무관하다.
                        CapturePopupUI.DrawTypedInsectPortrait(
                            portraitRect.center.x, portraitRect.center.y,
                            gachaResult.insectId, gachaResult.rarity, 1f);
                    }
                    GUI.color = previousColor;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(14);

                GUILayout.Label($"<size=36><b>{gachaResult.displayName}</b></size>", gachaNameStyle);

                if (gachaResult.isExclusive)
                {
                    GUILayout.Label("<size=23><b>* 상자 전용 곤충!</b></size>", gachaExclusiveStyle);
                }

                GUILayout.Space(14);
                GUILayout.Label($"<size=21>보너스: 캔디 {gachaResult.bonusCandy}개</size>", gachaCandyBonusStyle);

                GUILayout.Space(26);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("확인", gachaOkStyle, GUILayout.Width(200), GUILayout.Height(56)))
                {
                    showingGachaResult = false;
                    gachaResult = null;
                    ResetTabScroll(selectedTab);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.FlexibleSpace();
            }
        }

        private Color GetRarityColor(InsectRarity rarity)
        {
            return UITheme.Instance.GetInsectRarityColor(rarity);
        }

        private void ShowFeedback(string msg)
        {
            feedbackMessage = msg;
            feedbackTimer = 1.5f;
        }
    }
}
