using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    public class CharacterViewerUI : MonoBehaviour, IModalUI
    {
        private bool isOpen;
        private float rotateAngle;

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }
        public void CloseModal()
        {
            isOpen = false;
            ModalUIRegistry.Unregister(this);
        }
        private void OnDisable() { ModalUIRegistry.Unregister(this); }

        // 스타일 캐시
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private GUIStyle infoStyle;
        private GUIStyle closeStyle;
        private GUIStyle statValueStyle;
        private GUIStyle xpBarTextStyle;
        private bool stylesInitialized;

        // 컴포넌트 캐싱 (FindFirstObjectByType 매 프레임 호출 방지)
        private PlayerProgressController cachedProgress;
        private PlayerCandyInventory cachedCandyInv;
        private PlayerCurrencyWallet cachedCurrencyWallet;

        private void Update()
        {
            // V키는 QuickAccessBarUI에서 처리
            if (isOpen)
            {
                rotateAngle += Time.deltaTime * 30f;
                if (rotateAngle >= 360f) rotateAngle -= 360f;
            }
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            InitStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            // 반투명 어둡게 덮기
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 캐릭터 미리보기 (화면 좌측-중앙). cy는 캐릭터 시각 중심.
            float charCx = sw * 0.38f;
            float charCy = sh * 0.52f;
            float charScale = Mathf.Min(sw, sh) * 0.0028f;
            DrawCharacterModel(charCx, charCy, charScale);

            // 오른쪽 버튼 패널
            float panelW = 220f;
            float panelH = 420f;
            float panelX = sw - panelW - 40f;
            float panelY = (sh - panelH) * 0.5f;

            GUI.color = new Color(0.08f, 0.08f, 0.15f, 0.92f);
            GUI.DrawTexture(new Rect(panelX - 10f, panelY - 10f, panelW + 20f, panelH + 20f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float btnW = panelW - 20f;
            float btnH = 38f;
            float btnX = panelX;
            float curY = panelY + 10f;

            // 보석 구매
            if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "보석 구매", buttonStyle))
            {
                CashShopUI cashShop = FindFirstObjectByType<CashShopUI>();
                if (cashShop != null) cashShop.Toggle();
                CloseModal();
            }
            curY += btnH + 8f;

            // 선물 상자
            if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "선물 상자", buttonStyle))
            {
                CashShopUI cashShop = FindFirstObjectByType<CashShopUI>();
                if (cashShop != null)
                {
                    if (!cashShop.IsOpen) cashShop.Toggle();
                    // selectedTab = 2 (랜덤상자)
                    var tabField = typeof(CashShopUI).GetField("selectedTab",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (tabField != null) tabField.SetValue(cashShop, 2);
                }
                CloseModal();
            }
            curY += btnH + 8f;

            // 의상 변경
            if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "의상 변경", buttonStyle))
            {
                CharacterOutfitUI outfitUi = FindFirstObjectByType<CharacterOutfitUI>();
                if (outfitUi != null)
                {
                    if (!outfitUi.IsOpen) outfitUi.Toggle();
                }
                CloseModal();
            }
            curY += btnH + 8f;

            // 악세서리
            if (GUI.Button(new Rect(btnX, curY, btnW, btnH), "악세서리", buttonStyle))
            {
                CharacterOutfitUI outfitUi = FindFirstObjectByType<CharacterOutfitUI>();
                if (outfitUi != null)
                {
                    if (!outfitUi.IsOpen) outfitUi.Toggle();
                    // selectedSlot = OutfitSlot.Accessory
                    var slotField = typeof(CharacterOutfitUI).GetField("selectedSlot",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (slotField != null) slotField.SetValue(outfitUi, OutfitSlot.Accessory);
                }
                CloseModal();
            }
            curY += btnH + 24f;

            // 구분선
            GUI.color = new Color(0.4f, 0.4f, 0.5f, 0.6f);
            GUI.DrawTexture(new Rect(btnX, curY, btnW, 1f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            curY += 12f;

            // 캐릭터 정보
            GUI.Label(new Rect(btnX, curY, btnW, 24f), "캐릭터 정보", titleStyle);
            curY += 28f;

            // 참조 캐싱 (첫 열림 시 1회)
            if (cachedProgress == null) cachedProgress = FindFirstObjectByType<PlayerProgressController>();
            if (cachedCandyInv == null) cachedCandyInv = FindFirstObjectByType<PlayerCandyInventory>();
            if (cachedCurrencyWallet == null) cachedCurrencyWallet = FindFirstObjectByType<PlayerCurrencyWallet>();

            string charName = PlayerPrefs.GetString("InsectGame.Character.Name", "탐험가");
            int level = cachedProgress != null ? cachedProgress.Level : 1;
            int xp = cachedProgress != null ? cachedProgress.CurrentXp : 0;
            int xpNext = cachedProgress != null ? cachedProgress.XpToNextLevel : 1;
            int candies = cachedCandyInv != null ? cachedCandyInv.Candies : 0;
            int gems = cachedCurrencyWallet != null ? cachedCurrencyWallet.Gems :
                       (CashShopManager.Instance != null ? CashShopManager.Instance.Gems : 0);
            int coins = cachedCurrencyWallet != null ? cachedCurrencyWallet.Coins : 0;

            // 이름 + Lv 행
            GUI.Label(new Rect(btnX, curY, btnW, 22f), $"{charName}  Lv.{level}", infoStyle);
            curY += 26f;

            // EXP 바
            float barH = 22f;
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(btnX, curY, btnW, barH), Texture2D.whiteTexture);
            float xpRatio = xpNext > 0 ? Mathf.Clamp01((float)xp / xpNext) : 0f;
            GUI.color = new Color(0.3f, 0.65f, 1f, 1f);
            GUI.DrawTexture(new Rect(btnX, curY, btnW * xpRatio, barH), Texture2D.whiteTexture);
            // 테두리
            GUI.color = new Color(0.4f, 0.5f, 0.8f, 0.7f);
            GUI.DrawTexture(new Rect(btnX, curY, btnW, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(btnX, curY + barH - 1, btnW, 1f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(btnX, curY, btnW, barH), $"EXP {xp} / {xpNext}", xpBarTextStyle);
            curY += barH + 10f;

            // 재화 박스 3개 (캔디/코인/보석)
            float resRowH = 36f;
            float resW = (btnW - 12f) / 3f;
            DrawResourceRow(btnX, curY, resW, resRowH, "캔디", candies, new Color(1f, 0.7f, 0.85f));
            DrawResourceRow(btnX + resW + 6f, curY, resW, resRowH, "코인", coins, new Color(1f, 0.85f, 0.3f));
            DrawResourceRow(btnX + (resW + 6f) * 2f, curY, resW, resRowH, "보석", gems, new Color(0.4f, 0.7f, 1f));

            // 닫기 버튼 (우하단)
            if (GUI.Button(new Rect(sw - 70f, sh - 60f, 50f, 36f), "X", closeStyle))
            {
                CloseModal();
            }
        }

        private void DrawCharacterModel(float cx, float cy, float scale)
        {
            float swayX = Mathf.Sin(rotateAngle * Mathf.Deg2Rad) * 8f * scale;
            CharacterPortraitRenderer.DrawWithOutfit(cx, cy, scale, swayX);
        }

        private Color GetEquippedColor(CharacterOutfitManager mgr, OutfitSlot slot)
        {
            if (mgr == null) return GetDefaultColor(slot);
            OutfitItem item = mgr.GetEquipped(slot);
            if (item == null) return GetDefaultColor(slot);
            return item.primaryColor;
        }

        private Color GetDefaultColor(OutfitSlot slot)
        {
            switch (slot)
            {
                case OutfitSlot.Top: return new Color(0.16f, 0.32f, 0.72f);
                case OutfitSlot.Bottom: return new Color(0.25f, 0.25f, 0.28f);
                case OutfitSlot.Shoes: return new Color(0.35f, 0.2f, 0.12f);
                case OutfitSlot.Hat: return new Color(0f, 0f, 0f, 0f); // 투명 = 모자 없음
                default: return new Color(0.5f, 0.5f, 0.5f);
            }
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private void DrawResourceRow(float x, float y, float w, float h, string label, int value, Color accent)
        {
            GUI.color = new Color(accent.r * 0.12f, accent.g * 0.12f, accent.b * 0.12f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(x, y, w, 2f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            labelStyle.normal.textColor = new Color(accent.r * 0.8f, accent.g * 0.8f, accent.b * 0.8f);
            GUI.Label(new Rect(x + 6, y + 2, w - 12, 16), label, labelStyle);
            statValueStyle.normal.textColor = accent;
            GUI.Label(new Rect(x + 6, y + 14, w - 12, 20), value.ToString(), statValueStyle);
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = MakeTex(1, 1, new Color(0.08f, 0.08f, 0.15f, 0.92f));

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 18;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(0.9f, 0.85f, 0.6f);

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 16;
            buttonStyle.fontStyle = FontStyle.Bold;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.normal.background = MakeTex(1, 1, new Color(0.2f, 0.3f, 0.55f, 0.9f));
            buttonStyle.hover.background = MakeTex(1, 1, new Color(0.3f, 0.4f, 0.7f, 0.9f));
            buttonStyle.active.background = MakeTex(1, 1, new Color(0.15f, 0.2f, 0.4f, 0.9f));

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 14;
            labelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.75f);

            infoStyle = new GUIStyle(GUI.skin.label);
            infoStyle.fontSize = 16;
            infoStyle.normal.textColor = new Color(0.85f, 0.85f, 0.9f);

            closeStyle = new GUIStyle(GUI.skin.button);
            closeStyle.fontSize = 18;
            closeStyle.fontStyle = FontStyle.Bold;
            closeStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
            closeStyle.normal.background = MakeTex(1, 1, new Color(0.15f, 0.1f, 0.1f, 0.85f));
            closeStyle.hover.background = MakeTex(1, 1, new Color(0.3f, 0.1f, 0.1f, 0.9f));

            statValueStyle = new GUIStyle(GUI.skin.label);
            statValueStyle.fontSize = 16;
            statValueStyle.fontStyle = FontStyle.Bold;
            statValueStyle.alignment = TextAnchor.MiddleRight;

            xpBarTextStyle = new GUIStyle(GUI.skin.label);
            xpBarTextStyle.fontSize = 13;
            xpBarTextStyle.fontStyle = FontStyle.Bold;
            xpBarTextStyle.alignment = TextAnchor.MiddleCenter;
            xpBarTextStyle.normal.textColor = Color.white;
        }
    }
}
