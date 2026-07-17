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
        private Vector2 scrollPos;

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
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }

        public void CloseModal()
        {
            isOpen = false;
            showingGachaResult = false;
            ModalUIRegistry.Unregister(this);
        }

        /// <summary>월드 상호작용(상점/가챠 건물 접근)에서 특정 탭으로 바로 연다. 0=보석충전, 1=아이템, 2=랜덤상자.</summary>
        public void OpenAtTab(int tab)
        {
            selectedTab = Mathf.Clamp(tab, 0, 2);
            showingGachaResult = false;
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
            ModalUIRegistry.Unregister(this);
        }

        private void OnBoxOpened(GachaResult result)
        {
            gachaResult = result;
            showingGachaResult = true;
            gachaAnimTimer = 0f;

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

            float panelW = Mathf.Min(1200f, vw * 0.96f);
            // 모바일 세로는 긴 화면(1920+)을 더 활용해 확대된 카드의 스크롤을 줄임(세이프에어리어 여백 유지). 데스크톱은 기존 유지.
            float panelH = UIScale.IsMobileLayout ? Mathf.Min(1560f, vh * 0.9f) : Mathf.Min(820f, vh * 0.95f);
            float px = (vw - panelW) * 0.5f;
            float py = (vh - panelH) * 0.5f;
            Rect panelRect = new Rect(px, py, panelW, panelH);

            // 패널 배경
            GUI.color = PanelBgCol;
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // ── 좌측 캐릭터 미리보기 영역 (가챠 결과 화면에서는 숨김) ──
            bool mobile = UIScale.IsMobileLayout;
            if (!showingGachaResult && !mobile)
            {
                float charAreaW = 340f;
                float charAreaH = panelH - 60f;
                Rect charArea = new Rect(px + 16, py + 50, charAreaW, charAreaH);
                GUI.color = CharAreaBgCol;
                GUI.DrawTexture(charArea, Texture2D.whiteTexture);
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
                    selectedTab = i;
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
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));

            switch (selectedTab)
            {
                case 0: DrawGemTab(); break;
                case 1: DrawItemTab(); break;
                case 2: DrawGachaTab(); break;
            }

            GUILayout.EndScrollView();

            GUILayout.EndArea();

            UIScale.End();
        }

        // ===== Tab 0: 보석 충전 =====
        private void DrawGemTab()
        {
            if (CashShopManager.Instance == null) { GUILayout.Label("상점 초기화 중..."); return; }

            CashShopItem[] gemPacks = CashShopManager.Instance.GetGemPackages();

            GUILayout.Space(20);
            bool mobile = UIScale.IsMobileLayout;
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

            GUILayout.Label($"<size=31><b>{item.displayName}</b></size>", shopCenterBoldStyle);
            // 설명은 데스크톱 좁은 카드(260px)에서 잘림 방지 위해 모바일에서만 크게.
            GUILayout.Label($"<size={(mobile ? 21 : 16)}>{item.description}</size>", yellowDescStyle);

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

            GUILayout.Label($"<size=28><b>{item.displayName}</b></size>", shopCenterBoldStyle);
            if (item.rewardCount > 1)
                GUILayout.Label($"<size=23>x{item.rewardCount}</size>", itemRewardCountStyle);
            // 긴 설명 — 데스크톱 좁은 카드에서 잘림 방지 위해 모바일에서만 크게.
            GUILayout.Label($"<size={(mobile ? 20 : 15)}>{item.description}</size>", itemDescGrayStyle);

            GUILayout.FlexibleSpace();

            bool canAfford = currentGems >= item.gemPrice;
            // itemPriceStyle 재사용 + textColor 동적 갱신.
            itemPriceStyle.normal.textColor = canAfford ? GachaPriceAffordCol : Color.red;
            GUILayout.Label($"<size=26><b>💎 {item.gemPrice}</b></size>", itemPriceStyle);

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

            GUILayout.Label($"<size=31><b>{title}</b></size>", boxCenterBoldStyle);

            GUILayout.Space(12);

            // 확률표
            GUILayout.Label(rateText, boxRateStyle);

            GUILayout.FlexibleSpace();

            bool canAfford = currentGems >= price;
            // 스타일 재사용 + textColor만 동적 갱신 (BattleScreenUI/DexScreenUI 패턴)
            boxPriceStyle.normal.textColor = canAfford ? GachaPriceAffordCol : Color.red;
            GUILayout.Label($"<size=28><b>💎 {price}</b></size>", boxPriceStyle);

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
