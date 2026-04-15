using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    public class CharacterViewerUI : MonoBehaviour
    {
        private bool isOpen;
        private float rotateAngle;

        public bool IsOpen => isOpen;
        public void Toggle() { isOpen = !isOpen; }

        // 스타일 캐시
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private GUIStyle infoStyle;
        private GUIStyle closeStyle;
        private bool stylesInitialized;

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

            // 캐릭터 미리보기 (화면 좌측-중앙)
            float charCx = sw * 0.38f;
            float charCy = sh * 0.45f;
            float charScale = Mathf.Min(sw, sh) * 0.003f;
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
                isOpen = false;
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
                isOpen = false;
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
                isOpen = false;
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
                isOpen = false;
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

            string charName = PlayerPrefs.GetString("InsectGame.Character.Name", "탐험가");
            GUI.Label(new Rect(btnX, curY, btnW, 22f), $"이름: {charName}", infoStyle);
            curY += 24f;

            PlayerProgressController progress = FindFirstObjectByType<PlayerProgressController>();
            int level = (progress != null) ? progress.Level : 1;
            GUI.Label(new Rect(btnX, curY, btnW, 22f), $"Lv.{level}", infoStyle);
            curY += 24f;

            PlayerCandyInventory candyInv = FindFirstObjectByType<PlayerCandyInventory>();
            int candies = (candyInv != null) ? candyInv.Candies : 0;
            GUI.Label(new Rect(btnX, curY, btnW, 22f), $"캔디: {candies}", infoStyle);
            curY += 24f;

            int gems = (CashShopManager.Instance != null) ? CashShopManager.Instance.Gems : 0;
            GUI.Label(new Rect(btnX, curY, btnW, 22f), $"보석: {gems}", infoStyle);

            // 닫기 버튼 (우하단)
            if (GUI.Button(new Rect(sw - 70f, sh - 60f, 50f, 36f), "X", closeStyle))
            {
                isOpen = false;
            }
        }

        private void DrawCharacterModel(float cx, float cy, float scale)
        {
            // 현재 장착 의상 색상 가져오기
            CharacterOutfitManager outfitMgr = CharacterOutfitManager.Instance;
            Color hatColor = GetEquippedColor(outfitMgr, OutfitSlot.Hat);
            Color topColor = GetEquippedColor(outfitMgr, OutfitSlot.Top);
            Color bottomColor = GetEquippedColor(outfitMgr, OutfitSlot.Bottom);
            Color shoeColor = GetEquippedColor(outfitMgr, OutfitSlot.Shoes);

            // 피부색/머리색
            int skinIdx = PlayerPrefs.GetInt("InsectGame.Character.SkinColor", 0);
            int hairColorIdx = PlayerPrefs.GetInt("InsectGame.Character.HairColor", 0);
            int hairStyle = PlayerPrefs.GetInt("InsectGame.Character.HairStyle", 0);
            int gender = PlayerPrefs.GetInt("InsectGame.Character.Gender", 0);
            int faceType = PlayerPrefs.GetInt("InsectGame.Character.FaceType", 0);

            Color[] skinColors =
            {
                new Color(1.0f, 0.87f, 0.75f),
                new Color(0.9f, 0.75f, 0.6f),
                new Color(0.65f, 0.5f, 0.35f),
                new Color(0.4f, 0.28f, 0.18f)
            };
            Color skin = skinColors[Mathf.Clamp(skinIdx, 0, 3)];

            Color[] hairColors =
            {
                new Color(0.12f, 0.08f, 0.05f),
                new Color(0.35f, 0.2f, 0.1f),
                new Color(0.85f, 0.7f, 0.3f),
                new Color(0.6f, 0.15f, 0.1f),
                new Color(0.2f, 0.15f, 0.35f),
                new Color(0.15f, 0.3f, 0.5f)
            };
            Color hair = hairColors[Mathf.Clamp(hairColorIdx, 0, hairColors.Length - 1)];

            float s = scale;
            // 회전 시뮬레이션: 좌우 오프셋으로 흔들림 효과
            float swayX = Mathf.Sin(rotateAngle * Mathf.Deg2Rad) * 8f * s;

            float headW = 50f * s;
            float headH = 40f * s;
            float bodyW = (gender == 1) ? 42f * s : 48f * s;
            float bodyH = 60f * s;
            float legW = 18f * s;
            float legH = 50f * s;

            // 그림자
            GUI.color = new Color(0f, 0f, 0f, 0.2f);
            GUI.DrawTexture(new Rect(cx - bodyW * 0.6f + swayX, cy + bodyH * 0.5f + legH + 4f * s, bodyW * 1.2f, 6f * s), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 신발
            Texture2D shoeTex = MakeTex(1, 1, shoeColor);
            GUI.DrawTexture(new Rect(cx - bodyW * 0.35f - legW * 0.5f + swayX, cy + bodyH * 0.5f + legH - 2f * s, legW + 4f * s, 10f * s), shoeTex);
            GUI.DrawTexture(new Rect(cx + bodyW * 0.35f - legW * 0.5f + swayX, cy + bodyH * 0.5f + legH - 2f * s, legW + 4f * s, 10f * s), shoeTex);

            // 다리 (하의)
            Texture2D bottomTex = MakeTex(1, 1, bottomColor);
            GUI.DrawTexture(new Rect(cx - bodyW * 0.35f - legW * 0.5f + swayX, cy + bodyH * 0.5f, legW, legH), bottomTex);
            GUI.DrawTexture(new Rect(cx + bodyW * 0.35f - legW * 0.5f + swayX, cy + bodyH * 0.5f, legW, legH), bottomTex);

            // 몸통 (상의)
            Texture2D topTex = MakeTex(1, 1, topColor);
            GUI.DrawTexture(new Rect(cx - bodyW * 0.5f + swayX, cy - bodyH * 0.5f, bodyW, bodyH), topTex);

            // 팔
            float armW = 12f * s;
            float armH = 44f * s;
            GUI.DrawTexture(new Rect(cx - bodyW * 0.5f - armW + swayX, cy - bodyH * 0.4f, armW, armH), topTex);
            GUI.DrawTexture(new Rect(cx + bodyW * 0.5f + swayX, cy - bodyH * 0.4f, armW, armH), topTex);

            // 손 (피부색)
            Texture2D skinTex = MakeTex(1, 1, skin);
            GUI.DrawTexture(new Rect(cx - bodyW * 0.5f - armW + swayX, cy - bodyH * 0.4f + armH, armW, 8f * s), skinTex);
            GUI.DrawTexture(new Rect(cx + bodyW * 0.5f + swayX, cy - bodyH * 0.4f + armH, armW, 8f * s), skinTex);

            // 목
            GUI.DrawTexture(new Rect(cx - 6f * s + swayX, cy - bodyH * 0.5f - 8f * s, 12f * s, 10f * s), skinTex);

            // 머리 (얼굴)
            float headX = cx - headW * 0.5f + swayX;
            float headY = cy - bodyH * 0.5f - headH - 4f * s;
            GUI.DrawTexture(new Rect(headX, headY, headW, headH), skinTex);

            // 머리카락
            Texture2D hairTex = MakeTex(1, 1, hair);
            if (hairStyle == 3)
            {
                GUI.DrawTexture(new Rect(headX - 2f * s, headY - 4f * s, headW + 4f * s, 16f * s), hairTex);
                if (gender == 0)
                {
                    GUI.DrawTexture(new Rect(headX + headW * 0.3f, headY - 16f * s, headW * 0.15f, 14f * s), hairTex);
                }
                else
                {
                    GUI.DrawTexture(new Rect(headX + headW * 0.2f, headY - 20f * s, headW * 0.6f, 18f * s), hairTex);
                }
            }
            else if (hairStyle == 0)
            {
                GUI.DrawTexture(new Rect(headX + 2f * s, headY - 2f * s, headW - 4f * s, 14f * s), hairTex);
            }
            else if (hairStyle == 1)
            {
                GUI.DrawTexture(new Rect(headX + 2f * s, headY - 2f * s, headW - 4f * s, 16f * s), hairTex);
                GUI.DrawTexture(new Rect(headX - 2f * s, headY + 10f * s, 8f * s, 20f * s), hairTex);
                GUI.DrawTexture(new Rect(headX + headW - 6f * s, headY + 10f * s, 8f * s, 20f * s), hairTex);
            }
            else
            {
                GUI.DrawTexture(new Rect(headX + 2f * s, headY - 2f * s, headW - 4f * s, 18f * s), hairTex);
                GUI.DrawTexture(new Rect(headX - 4f * s, headY + 10f * s, 10f * s, 50f * s), hairTex);
                GUI.DrawTexture(new Rect(headX + headW - 6f * s, headY + 10f * s, 10f * s, 50f * s), hairTex);
            }

            // 눈
            Texture2D whiteTex = MakeTex(1, 1, Color.white);
            Texture2D blackTex = MakeTex(1, 1, new Color(0.1f, 0.08f, 0.05f));
            float eyeY = headY + headH * 0.3f;
            float eyeSize = 8f * s;
            float pupilSize = 4f * s;
            GUI.DrawTexture(new Rect(headX + headW * 0.2f, eyeY, eyeSize, eyeSize), whiteTex);
            GUI.DrawTexture(new Rect(headX + headW * 0.6f, eyeY, eyeSize, eyeSize), whiteTex);
            GUI.DrawTexture(new Rect(headX + headW * 0.2f + 2f * s, eyeY + 2f * s, pupilSize, pupilSize), blackTex);
            GUI.DrawTexture(new Rect(headX + headW * 0.6f + 2f * s, eyeY + 2f * s, pupilSize, pupilSize), blackTex);

            // 눈썹
            Texture2D browTex = MakeTex(1, 1, new Color(hair.r * 0.6f, hair.g * 0.6f, hair.b * 0.6f));
            GUI.DrawTexture(new Rect(headX + headW * 0.15f, eyeY - 4f * s, 12f * s, 2f * s), browTex);
            GUI.DrawTexture(new Rect(headX + headW * 0.55f, eyeY - 4f * s, 12f * s, 2f * s), browTex);

            // 코
            GUI.DrawTexture(new Rect(headX + headW * 0.45f, eyeY + eyeSize + 2f * s, 5f * s, 5f * s), skinTex);

            // 입
            Texture2D mouthTex = MakeTex(1, 1, new Color(0.8f, 0.4f, 0.35f));
            float mouthW = (8f + faceType * 2f) * s;
            GUI.DrawTexture(new Rect(headX + headW * 0.5f - mouthW * 0.5f, eyeY + eyeSize + 9f * s, mouthW, 3f * s), mouthTex);

            // 볼터치 (여자)
            if (gender == 1)
            {
                Texture2D blushTex = MakeTex(1, 1, new Color(1f, 0.6f, 0.6f, 0.5f));
                GUI.DrawTexture(new Rect(headX + 4f * s, eyeY + eyeSize + 2f * s, 8f * s, 5f * s), blushTex);
                GUI.DrawTexture(new Rect(headX + headW - 12f * s, eyeY + eyeSize + 2f * s, 8f * s, 5f * s), blushTex);
            }

            // 모자 (장착중이면)
            if (hatColor.a > 0.01f)
            {
                Texture2D hatTex = MakeTex(1, 1, hatColor);
                GUI.DrawTexture(new Rect(headX - 4f * s, headY - 10f * s, headW + 8f * s, 14f * s), hatTex);
            }
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
        }
    }
}
