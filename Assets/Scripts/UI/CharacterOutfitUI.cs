using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class CharacterOutfitUI : MonoBehaviour
    {
        [SerializeField] private CharacterOutfitManager outfitManager;
        [SerializeField] private OutfitBonusProvider bonusProvider;

        private bool isOpen;
        private OutfitSlot selectedSlot = OutfitSlot.Hat;
        private Vector2 scrollPos;

        // 장착 피드백
        private float equipFlashTimer;
        private string lastEquippedId;
        private float setCompleteFlashTimer;
        private readonly System.Collections.Generic.Dictionary<string, bool> prevSetStates =
            new System.Collections.Generic.Dictionary<string, bool>();

        // 패널 페이드
        private TweenHandle openFade;
        private bool wasOpen;

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
        private bool stylesInitialized;

        public void Toggle()
        {
            isOpen = !isOpen;
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

        // P키는 QuickAccessBarUI에서 처리

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

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
            tabNormalStyle.fontSize = 14;
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
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.wordWrap = true;

            coinStyle = new GUIStyle(GUI.skin.label);
            coinStyle.fontSize = 16;
            coinStyle.fontStyle = FontStyle.Bold;
            coinStyle.normal.textColor = new Color(1f, 0.84f, 0f, 1f);
            coinStyle.alignment = TextAnchor.MiddleLeft;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.background = btnTex;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.fontSize = 11;
            buttonStyle.fontStyle = FontStyle.Bold;

            closeStyle = new GUIStyle(GUI.skin.button);
            closeStyle.normal.background = closeTex;
            closeStyle.normal.textColor = Color.white;
            closeStyle.fontSize = 16;
            closeStyle.fontStyle = FontStyle.Bold;

            bonusStyle = new GUIStyle(GUI.skin.label);
            bonusStyle.fontSize = 10;
            bonusStyle.normal.textColor = new Color(0.4f, 0.9f, 0.4f);
            bonusStyle.alignment = TextAnchor.MiddleCenter;

            setStyle = new GUIStyle(GUI.skin.label);
            setStyle.fontSize = 11;
            setStyle.normal.textColor = new Color(0.6f, 0.6f, 0.7f);
            setStyle.alignment = TextAnchor.MiddleLeft;
            setStyle.wordWrap = true;

            setActiveStyle = new GUIStyle(setStyle);
            setActiveStyle.fontStyle = FontStyle.Bold;
        }

        private void OnGUI()
        {
            // 패널 페이드
            float panelAlpha = UIHelper.AnimatePanelOpen(ref openFade, isOpen, ref wasOpen);
            if (!isOpen && panelAlpha <= 0.01f) return;
            if (outfitManager == null) return;

            GUI.color = new Color(1f, 1f, 1f, panelAlpha);

            // 타이머 감소 (OnGUI는 프레임당 여러번 호출되므로 Repaint에서만)
            if (Event.current.type == EventType.Repaint)
            {
                if (equipFlashTimer > 0f) equipFlashTimer -= Time.deltaTime;
                if (setCompleteFlashTimer > 0f) setCompleteFlashTimer -= Time.deltaTime;
            }

            InitStyles();

            float panelW = 800f;
            float panelH = 600f;
            float x = (Screen.width - panelW) * 0.5f;
            float y = (Screen.height - panelH) * 0.5f;
            Rect panelRect = new Rect(x, y, panelW, panelH);

            GUI.Box(panelRect, "", panelStyle);

            // 제목
            GUI.Label(new Rect(x + 16, y + 10, 300, 36), "캐릭터 꾸미기", titleStyle);

            // 닫기 버튼
            if (GUI.Button(new Rect(x + panelW - 44, y + 8, 32, 32), "X", closeStyle))
            {
                isOpen = false;
            }

            // ── 왼쪽 슬롯 탭 ──
            float tabX = x + 10;
            float tabY = y + 52;
            float tabW = 100;
            float tabH = 38;
            float tabGap = 4;

            OutfitSlot[] slots = (OutfitSlot[])System.Enum.GetValues(typeof(OutfitSlot));
            for (int i = 0; i < slots.Length; i++)
            {
                Rect tabRect = new Rect(tabX, tabY + i * (tabH + tabGap), tabW, tabH);
                GUIStyle style = (slots[i] == selectedSlot) ? tabSelectedStyle : tabNormalStyle;
                if (GUI.Button(tabRect, slotLabels[i], style))
                {
                    selectedSlot = slots[i];
                    scrollPos = Vector2.zero;
                }
            }

            // ── 세트 정보 패널 ──
            if (bonusProvider != null)
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
                    string stars = "";
                    for (int s = 0; s < total; s++)
                        stars += s < setInfo.equippedCount ? "\u2605" : "\u2606";

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
            float gridX = x + tabW + 20;
            float gridY = y + 52;
            float gridW = panelW - tabW - 34;
            float gridH = panelH - 100;

            OutfitItem[] items = outfitManager.GetItemsForSlot(selectedSlot);

            float cardW = 120f;
            float cardH = 160f;
            float cardGap = 10f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((gridW - 10) / (cardW + cardGap)));
            int rows = Mathf.CeilToInt((float)items.Length / cols);
            float contentH = rows * (cardH + cardGap) + 10;

            Rect viewRect = new Rect(gridX, gridY, gridW, gridH);
            Rect contentRect = new Rect(0, 0, gridW - 20, contentH);
            scrollPos = GUI.BeginScrollView(viewRect, scrollPos, contentRect);

            for (int i = 0; i < items.Length; i++)
            {
                OutfitItem item = items[i];
                int col = i % cols;
                int row = i / cols;
                float cx = col * (cardW + cardGap) + 5;
                float cy = row * (cardH + cardGap) + 5;
                Rect cardRect = new Rect(cx, cy, cardW, cardH);

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

                // 색상 미리보기
                float previewSize = 50f;
                Rect previewRect = new Rect(cx + (cardW - previewSize) * 0.5f, cy + 10, previewSize, previewSize);
                if (item.primaryColor.a > 0.01f)
                {
                    GUI.DrawTexture(previewRect, UIHelper.GetCachedTex(item.primaryColor));
                }
                else
                {
                    // 투명 표시 (체크 패턴 대신 회색 X)
                    GUI.Label(previewRect, "--", labelStyle);
                }

                // 이름
                Rect nameRect = new Rect(cx + 2, cy + 65, cardW - 4, 30);
                GUI.Label(nameRect, item.displayName, labelStyle);

                // 보너스 표시
                string bonusText = item.statBonus.GetPrimaryBonusText();
                if (!string.IsNullOrEmpty(bonusText))
                {
                    Rect bonusRect = new Rect(cx + 2, cy + 90, cardW - 4, 16);
                    GUI.Label(bonusRect, bonusText, bonusStyle);
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
                Rect btnRect = new Rect(cx + 10, cy + cardH - 36, cardW - 20, 26);

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
                        Rect premRect = new Rect(cx + 2, cy + 95, cardW - 4, 16);
                        GUIStyle premStyle = UIHelper.CachedStyle("outfit_prem", () =>
                        {
                            GUIStyle s = new GUIStyle(GUI.skin.label);
                            s.fontSize = 10;
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

                        Rect hintRect = new Rect(cx + 2, cy + 95, cardW - 4, 20);
                        GUIStyle hintStyle = UIHelper.CachedStyle("outfit_hint", () =>
                        {
                            GUIStyle s = new GUIStyle(GUI.skin.label);
                            s.fontSize = 10;
                            s.normal.textColor = new Color(0.7f, 0.6f, 0.3f);
                            s.alignment = TextAnchor.MiddleCenter;
                            return s;
                        });
                        GUI.Label(hintRect, item.unlockCondition, hintStyle);
                    }
                }
            }

            GUI.EndScrollView();

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
                float tipH = lineCount * 14 + 8;
                Rect tipRect = new Rect(
                    hoveredCardScreenRect.x,
                    hoveredCardScreenRect.y - tipH - 4,
                    hoveredCardScreenRect.width,
                    tipH);
                GUI.DrawTexture(tipRect, UIHelper.GetCachedTex(new Color(0.05f, 0.07f, 0.15f, 0.92f)));
                GUIStyle tipStyle = UIHelper.CachedStyle("outfit_tip", () =>
                {
                    GUIStyle s = new GUIStyle(GUI.skin.label);
                    s.fontSize = 10;
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

                    Rect summaryRect = new Rect(x + 16, y + panelH - 60, panelW - 32, 20);
                    GUIStyle summaryStyle = UIHelper.CachedStyle("outfit_summary", () =>
                    {
                        GUIStyle s = new GUIStyle(GUI.skin.label);
                        s.fontSize = 11;
                        s.normal.textColor = new Color(0.4f, 0.9f, 0.4f);
                        s.alignment = TextAnchor.MiddleLeft;
                        return s;
                    });
                    GUI.Label(summaryRect, summary, summaryStyle);
                }
            }

            Rect coinRect = new Rect(x + 16, y + panelH - 38, 500, 30);
            PlayerCurrencyWallet w = outfitManager.GetComponent<PlayerCurrencyWallet>() ??
                FindFirstObjectByType<PlayerCurrencyWallet>();
            int coinCount = (w != null) ? w.Coins : 0;
            int gemCount = CashShopManager.Instance != null ? CashShopManager.Instance.Gems : 0;
            GUI.Label(coinRect, $"보유 캔디: 🍬{coinCount}    보석: 💎{gemCount}", coinStyle);

            // GUI.color 복원
            GUI.color = Color.white;
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
    }
}
