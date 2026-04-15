using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class CashShopUI : MonoBehaviour
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

        private readonly string[] tabNames = { "보석 충전", "아이템 상점", "랜덤 상자" };

        public bool IsOpen => isOpen;

        public void Toggle()
        {
            isOpen = !isOpen;
            showingGachaResult = false;
        }

        private void Update()
        {
            // F4키는 QuickAccessBarUI에서 처리
            if (showingGachaResult)
                gachaAnimTimer += Time.deltaTime;

            if (feedbackTimer > 0f)
                feedbackTimer -= Time.deltaTime;
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

            // 전체화면 반투명 배경
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float panelW = 850f;
            float panelH = 620f;
            float px = (Screen.width - panelW) * 0.5f;
            float py = (Screen.height - panelH) * 0.5f;
            Rect panelRect = new Rect(px, py, panelW, panelH);

            // 패널 배경
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(panelRect);

            // -- 헤더 --
            GUILayout.BeginHorizontal();
            GUILayout.Label("<size=22><b>  상점</b></size>", new GUIStyle(GUI.skin.label) { richText = true, normal = { textColor = Color.cyan } });
            GUILayout.FlexibleSpace();
            int gems = CashShopManager.Instance != null ? CashShopManager.Instance.Gems : 0;
            GUILayout.Label($"<size=16><b>보석: {gems}</b></size>", new GUIStyle(GUI.skin.label) { richText = true, normal = { textColor = new Color(0.4f, 0.7f, 1f) } });
            GUILayout.Space(10);
            if (GUILayout.Button("X", GUILayout.Width(30), GUILayout.Height(30)))
                Toggle();
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // -- 가챠 연출 중이면 결과 화면만 표시 --
            if (showingGachaResult && gachaResult != null)
            {
                DrawGachaResultScreen();
                GUILayout.EndArea();
                return;
            }

            // -- 탭 --
            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabNames.Length; i++)
            {
                GUI.color = (selectedTab == i) ? Color.cyan : Color.gray;
                if (GUILayout.Button(tabNames[i], GUILayout.Height(32)))
                    selectedTab = i;
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // -- 피드백 메시지 --
            if (feedbackTimer > 0f && !string.IsNullOrEmpty(feedbackMessage))
            {
                GUIStyle fbStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = Color.green } };
                GUILayout.Label($"<size=16><b>{feedbackMessage}</b></size>", fbStyle);
                GUILayout.Space(5);
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
        }

        // ===== Tab 0: 보석 충전 =====
        private void DrawGemTab()
        {
            if (CashShopManager.Instance == null) { GUILayout.Label("상점 초기화 중..."); return; }

            CashShopItem[] gemPacks = CashShopManager.Instance.GetGemPackages();

            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            foreach (var item in gemPacks)
            {
                DrawGemCard(item);
                GUILayout.Space(15);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(30);
            GUIStyle infoStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } };
            GUILayout.Label("* 현재 테스트 모드: 결제 없이 즉시 지급됩니다.", infoStyle);
        }

        private void DrawGemCard(CashShopItem item)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(220), GUILayout.Height(200));

            // 보석 아이콘 (파란색 사각형)
            GUI.color = new Color(0.3f, 0.5f, 1f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Box("", GUILayout.Width(60), GUILayout.Height(60));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUI.color = Color.white;

            GUIStyle centerBold = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };

            GUILayout.Label($"<size=18><b>{item.displayName}</b></size>", centerBold);
            GUILayout.Label($"<size=12>{item.description}</size>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.yellow } });

            GUILayout.FlexibleSpace();
            GUILayout.Label($"<size=16><b>\\{item.priceKRW:N0}</b></size>", centerBold);
            GUILayout.Space(5);

            GUI.color = new Color(0.2f, 0.8f, 0.3f);
            if (GUILayout.Button("구매", GUILayout.Height(30)))
            {
                if (CashShopManager.Instance.PurchaseWithRealMoney(item.itemId))
                    ShowFeedback("보석 충전 완료!");
            }
            GUI.color = Color.white;

            GUILayout.EndVertical();
        }

        // ===== Tab 1: 아이템 상점 =====
        private void DrawItemTab()
        {
            if (CashShopManager.Instance == null) { GUILayout.Label("상점 초기화 중..."); return; }

            CashShopItem[] items = CashShopManager.Instance.GetItemsByCategory(CashItemCategory.MinigameItem);
            int gems = CashShopManager.Instance.Gems;
            int col = 0;

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            foreach (var item in items)
            {
                if (item.gemPrice <= 0) continue; // 보석팩 제외

                DrawItemCard(item, gems);
                col++;
                if (col % 3 == 0)
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
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(220), GUILayout.Height(180));

            GUIStyle centerBold = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };

            GUILayout.Label($"<size=15><b>{item.displayName}</b></size>", centerBold);
            if (item.rewardCount > 1)
                GUILayout.Label($"x{item.rewardCount}", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Label($"<size=11>{item.description}</size>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } });

            GUILayout.FlexibleSpace();

            bool canAfford = currentGems >= item.gemPrice;
            GUILayout.Label($"<size=14><b>보석 {item.gemPrice}</b></size>",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = canAfford ? new Color(0.4f, 0.7f, 1f) : Color.red } });

            GUILayout.Space(3);

            if (!canAfford)
            {
                GUI.enabled = false;
                GUILayout.Button("보석 부족", GUILayout.Height(28));
                GUI.enabled = true;
            }
            else
            {
                GUI.color = new Color(0.2f, 0.7f, 0.9f);
                if (GUILayout.Button("구매", GUILayout.Height(28)))
                {
                    if (CashShopManager.Instance.PurchaseWithGems(item.itemId))
                        ShowFeedback("구매 완료!");
                }
                GUI.color = Color.white;
            }

            GUILayout.EndVertical();
        }

        // ===== Tab 2: 랜덤 상자 =====
        private void DrawGachaTab()
        {
            if (CashShopManager.Instance == null) { GUILayout.Label("상점 초기화 중..."); return; }

            int gems = CashShopManager.Instance.Gems;

            GUILayout.Space(15);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // 브론즈
            DrawBoxCard("box_bronze", "브론즈 상자", new Color(0.6f, 0.4f, 0.2f),
                "C:55%  U:30%  R:12%\nE:2.5%  L:0.5%", 300, gems);
            GUILayout.Space(15);

            // 실버
            DrawBoxCard("box_silver", "실버 상자", new Color(0.7f, 0.7f, 0.8f),
                "C:20%  U:35%  R:30%\nE:12%  L:3%", 500, gems);
            GUILayout.Space(15);

            // 골드
            DrawBoxCard("box_gold", "골드 상자", new Color(1f, 0.85f, 0.3f),
                "C:10%  U:25%  R:35%\nE:25%  L:5%", 700, gems);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(20);
            GUIStyle warningStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = Color.yellow } };
            GUILayout.Label("<size=13><b>* 상자 전용 곤충 10종 포함! (필드에서 만날 수 없음)</b></size>", warningStyle);
        }

        private void DrawBoxCard(string boxId, string title, Color themeColor, string rateText, int price, int currentGems)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(230), GUILayout.Height(320));

            // 상자 색상 테마
            GUI.color = themeColor;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Box("", GUILayout.Width(80), GUILayout.Height(80));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUI.color = Color.white;

            GUIStyle centerBold = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
            GUILayout.Label($"<size=17><b>{title}</b></size>", centerBold);

            GUILayout.Space(10);

            // 확률표
            GUIStyle rateStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
            GUILayout.Label(rateText, rateStyle);

            GUILayout.FlexibleSpace();

            bool canAfford = currentGems >= price;
            GUILayout.Label($"<size=15><b>보석 {price}</b></size>",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = canAfford ? new Color(0.4f, 0.7f, 1f) : Color.red } });

            GUILayout.Space(5);

            if (!canAfford)
            {
                GUI.enabled = false;
                GUILayout.Button("보석 부족", GUILayout.Height(35));
                GUI.enabled = true;
            }
            else
            {
                GUI.color = themeColor;
                if (GUILayout.Button("열기!", GUILayout.Height(35)))
                {
                    CashShopManager.Instance.PurchaseWithGems(boxId);
                }
                GUI.color = Color.white;
            }

            GUILayout.EndVertical();
        }

        // ===== 가챠 연출 화면 =====
        private void DrawGachaResultScreen()
        {
            // Phase 1 (0~1.5초): 상자 흔들림 + 깜빡임
            if (gachaAnimTimer < 1.5f)
            {
                GUILayout.FlexibleSpace();
                GUIStyle shakeStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };

                float flash = Mathf.PingPong(gachaAnimTimer * 6f, 1f);
                GUI.color = Color.Lerp(new Color(0.3f, 0.5f, 1f), new Color(1f, 0.85f, 0.3f), flash);
                GUILayout.Label("<size=40><b>???</b></size>", shakeStyle);
                GUI.color = Color.white;

                GUILayout.Label("<size=16>상자를 여는 중...</size>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
                GUILayout.FlexibleSpace();
                return;
            }

            // Phase 2 (1.5~2초): 밝은 플래시
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

                GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    richText = true,
                    normal = { textColor = rarityColor }
                };

                GUILayout.Label($"<size=28><b>{stars}{rarityLabel}!{stars}</b></size>", titleStyle);
                GUILayout.Space(15);

                // 곤충 컬러 미리보기 (등급 색 사각형)
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.color = rarityColor;
                GUILayout.Box("", GUILayout.Width(100), GUILayout.Height(100));
                GUI.color = Color.white;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                GUIStyle nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = Color.white } };
                GUILayout.Label($"<size=22><b>{gachaResult.displayName}</b></size>", nameStyle);

                if (gachaResult.isExclusive)
                {
                    GUILayout.Label("<size=14><b>* 상자 전용 곤충!</b></size>",
                        new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.yellow } });
                }

                GUILayout.Space(10);
                GUILayout.Label($"보너스: 캔디 {gachaResult.bonusCandy}개",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.6f, 0.8f) } });

                GUILayout.Space(20);

                // 확인 버튼
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("확인", GUILayout.Width(120), GUILayout.Height(35)))
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
            switch (rarity)
            {
                case InsectRarity.Common:    return new Color(0.6f, 0.6f, 0.6f);
                case InsectRarity.Uncommon:  return new Color(0.3f, 0.8f, 0.3f);
                case InsectRarity.Rare:      return new Color(0.3f, 0.5f, 1f);
                case InsectRarity.Epic:      return new Color(0.7f, 0.3f, 1f);
                case InsectRarity.Legendary: return new Color(1f, 0.85f, 0.2f);
                default: return Color.white;
            }
        }

        private void ShowFeedback(string msg)
        {
            feedbackMessage = msg;
            feedbackTimer = 1.5f;
        }
    }
}
