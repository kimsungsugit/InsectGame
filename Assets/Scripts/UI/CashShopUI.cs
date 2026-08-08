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
                DrawGachaResultScreen();
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

            // 상자 색상 테마
            GUI.color = themeColor;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Box("", GUILayout.Width(120), GUILayout.Height(120));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUI.color = Color.white;

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

        // ===== 가챠 연출 화면 =====
        private void DrawGachaResultScreen()
        {
            InitGachaStyles();

            // Phase 1 (0~1.5초): 상자 흔들림 + 깜빡임
            if (gachaAnimTimer < 1.5f)
            {
                GUILayout.FlexibleSpace();
                float flash = Mathf.PingPong(gachaAnimTimer * 6f, 1f);
                GUI.color = Color.Lerp(GachaFlashBlue, GachaFlashGold, flash);
                GUILayout.Label("<size=52><b>???</b></size>", gachaShakeStyle);
                GUI.color = Color.white;

                GUILayout.Label("<size=28>상자를 여는 중...</size>", gachaSubtitleStyle);
                GUILayout.FlexibleSpace();
                return;
            }

            // Phase 2 (1.5~2초): 밝은 플래시 (struct new Color는 stack — GC 없음, 유지)
            if (gachaAnimTimer < 2f)
            {
                float alpha = 1f - (gachaAnimTimer - 1.5f) * 2f;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(0, 0, 850, 620), Texture2D.whiteTexture);
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

                // 곤충 컬러 미리보기 (등급 색 사각형)
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.color = rarityColor;
                GUILayout.Box("", GUILayout.Width(140), GUILayout.Height(140));
                GUI.color = Color.white;
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
