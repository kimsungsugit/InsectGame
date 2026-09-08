using InsectGame.Core;
using InsectGame.Dex;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class PlayerStatusHUD : MonoBehaviour
    {
        [SerializeField] private PlayerProgressController progress;
        [SerializeField] private PlayerCandyInventory candyInventory;
        [SerializeField] private PlayerCurrencyWallet currencyWallet;
        [SerializeField] private PlayerInsectCollection insectCollection;
        [SerializeField] private PlayerItemInventory itemInventory;
        [SerializeField] private DexController dexController;
        [SerializeField] private BattleTeamManager teamManager;
        [SerializeField] private RegionManager regionManager;

        // GUIStyle 캐싱
        private GUIStyle sectionTitleStyle;
        private GUIStyle levelBadgeLabelStyle;
        private GUIStyle levelBadgeNumStyle;
        private GUIStyle xpTitleStyle;
        private GUIStyle xpTextStyle;
        private GUIStyle xpPctStyle;
        private GUIStyle regionNameStyle;
        private GUIStyle regionSubStyle;
        private GUIStyle statBoxLblStyle;
        private GUIStyle statBoxValStyle;
        private GUIStyle toggleStyle;
        private GUIStyle alertNameStyle;
        private GUIStyle alertDescStyle;
        private GUIStyle tabLabelStyle;
        private GUIStyle tabNumStyle;
        private GUIStyle tabHintStyle;
        private bool stylesInitialized;

        /// <summary>
        /// 좌상단 상태 패널이 펼쳐져 있는가.
        ///
        /// 펼침 패널은 x[safeL+20, safeL+500] × y[ContentTop, +540]로, 그 아래 좌측 스택
        /// (미니맵 ContentTop+150, 퀘스트 칩·목표 행 ContentTop+380~)을 <b>통째로 덮는다</b>.
        /// IMGUI는 겹침으로 입력을 막지 않으므로 덮인 버튼이 여전히 히트테스트된다 —
        /// 안 보이는 버튼이 눌리는 셈이라 좌측 스택이 이 값을 보고 스스로 빠진다.
        /// (데스크톱은 기본이 펼침이라 첫 프레임부터 해당된다.)
        /// </summary>
        public bool IsExpanded => expanded;

        private bool expanded = true;
        private bool mobileLayoutInitialized;
        private float xpBarAnim;
        private float toggleAnim = 1f;

        private string subAreaAlertName;
        private string subAreaAlertDesc;
        private float subAreaAlertTimer;
        private bool subscribedSubArea;

        // OnGUI 매 프레임 new Color 회피용 (alpha/scaled 동적 값 제외).
        private static readonly Color PanelBgCol = new Color(0.03f, 0.04f, 0.08f, 0.92f);
        private static readonly Color PanelAccentBlueCol = new Color(0.3f, 0.6f, 1f);
        private static readonly Color PanelDividerCol = new Color(0.15f, 0.18f, 0.25f);
        private static readonly Color LvBadgeBgDarkCol = new Color(0.15f, 0.25f, 0.5f);
        private static readonly Color LvBadgeAccentCol = new Color(0.3f, 0.6f, 1f);
        private static readonly Color XpBarBgCol = new Color(0.08f, 0.08f, 0.12f);
        private static readonly Color XpBarFillDarkCol = new Color(0.2f, 0.5f, 0.9f);
        private static readonly Color XpBarFillLightCol = new Color(0.3f, 0.65f, 1f);
        private static readonly Color StatCandyPinkCol = new Color(1f, 0.7f, 0.85f);
        private static readonly Color StatCoinGoldCol = new Color(1f, 0.85f, 0.3f);
        private static readonly Color StatGemBlueCol = new Color(0.4f, 0.7f, 1f);
        private static readonly Color StatTeamOrangeCol = new Color(1f, 0.6f, 0.3f);
        private static readonly Color StatOwnedGreenCol = new Color(0.4f, 0.85f, 0.5f);
        private static readonly Color StatDiscoveredBlueCol = new Color(0.6f, 0.8f, 1f);
        private static readonly Color StatCapturedGoldCol = new Color(1f, 0.85f, 0.3f);
        private static readonly Color RegionDefaultCol = new Color(0.6f, 0.7f, 0.8f);

        // GetAllOwned 캐싱 — DrawCollectionSection 매 프레임 호출 회피 (CollectionUI 패턴).
        private int cachedOwnedCount;
        private bool ownedCountCacheDirty = true;

        private void HandleInsectUpdated(PlayerInsectData _) { ownedCountCacheDirty = true; }

        private bool subscribedInsects;

        /// <summary>
        /// 표시용 캐릭터 이름. <b>매 프레임 PlayerPrefs를 두드리지 않는다</b> — OnGUI에서 불리므로
        /// 여기서 1회만 읽고 캐싱한다(클라우드 복원이 값을 바꾸면 다음 활성화에서 갱신된다).
        /// 비어 있으면 옛 표기를 그대로 쓴다.
        /// </summary>
        private string cachedPlayerName;

        private string PlayerDisplayName
        {
            get
            {
                if (cachedPlayerName == null)
                {
                    string saved = PlayerPrefs.GetString(
                        InsectGame.Core.SaveScope.PrefsKey("InsectGame.Character.Name"), "");
                    cachedPlayerName = string.IsNullOrWhiteSpace(saved) ? "PLAYER" : saved;
                }
                return cachedPlayerName;
            }
        }

        private void OnEnable()
        {
            // 계정이 바뀌거나 클라우드 복원이 끝난 뒤 다시 켜지면 이름을 새로 읽는다.
            cachedPlayerName = null;

            if (!mobileLayoutInitialized)
            {
                mobileLayoutInitialized = true;
                if (UIScale.IsMobileLayout)
                {
                    expanded = false;
                    toggleAnim = 0f;
                }
            }
            if (regionManager != null && !subscribedSubArea)
            {
                regionManager.SubAreaChanged += OnSubAreaEntered;
                subscribedSubArea = true;
            }
            if (insectCollection != null && !subscribedInsects)
            {
                insectCollection.InsectUpdated += HandleInsectUpdated;
                subscribedInsects = true;
            }
            ownedCountCacheDirty = true;
        }

        private void OnDisable()
        {
            if (regionManager != null && subscribedSubArea)
                regionManager.SubAreaChanged -= OnSubAreaEntered;
            subscribedSubArea = false;
            if (insectCollection != null && subscribedInsects)
                insectCollection.InsectUpdated -= HandleInsectUpdated;
            subscribedInsects = false;
        }

        private void OnSubAreaEntered(SubAreaData subArea)
        {
            if (subArea != null)
            {
                subAreaAlertName = subArea.displayName;
                subAreaAlertDesc = subArea.description ?? "";
                subAreaAlertTimer = 3.5f;
            }
        }

        private void Update()
        {
            float target = expanded ? 1f : 0f;
            toggleAnim = Mathf.MoveTowards(toggleAnim, target, Time.deltaTime * 6f);

            if (progress != null)
            {
                float xpRatio = progress.XpToNextLevel > 0
                    ? (float)progress.CurrentXp / progress.XpToNextLevel : 0f;
                xpBarAnim = Mathf.MoveTowards(xpBarAnim, xpRatio, Time.deltaTime * 2f);
            }

            // 모달이 열려 있는 동안에는 배너 수명을 태우지 않는다 — 위 OnGUI가 그리지 않으므로
            // 그대로 두면 창을 닫았을 때 이미 사라진 뒤다(TutorialQuestUI의 완료 배너와 같은 처리).
            if (subAreaAlertTimer > 0f && !ModalUIRegistry.IsAnyOpen())
                subAreaAlertTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            // **모달 위에는 그리지 않는다.** 형제 HUD(MinimapUI·퀘스트 칩·목표 행)가 이미
            // 같은 규칙을 따르는데 이 파일만 빠져 있었다. 두 가지가 겹쳐 있었다:
            //   ① 480×540 불투명 패널이 도감·상점·배틀 위에 얹힌다(IMGUI는 그리기 순서가
            //      컴포넌트 순서에 달려 있어 어떤 날은 위, 어떤 날은 아래로 나온다).
            //   ② 더 나쁜 쪽은 입력이다 — 이 화면은 `GUI.Button`이 아니라 `Event.current`로
            //      직접 히트테스트하고 `evt.Use()`로 소비한다. IMGUI는 z-order로 히트테스트를
            //      가르지 않으므로, 모달의 좌상단 컨트롤을 누른 탭을 **이쪽이 먼저 먹고**
            //      상태 패널만 접혔다 펴진다(모바일 기본은 닫힘이라 그 자리에 탭이 서 있다).
            if (ModalUIRegistry.IsAnyOpen()) return;

            UIScale.Begin();
            DrawSubAreaAlert();

            if (progress == null) { UIScale.End(); return; }

            InitStyles();

            float panelW = 480f;
            float panelH = UISafeLayout.ClampHeight(540f);
            float margin = 20f;
            // 세이프 에어리어(노치/상태바) 안쪽으로 — 세로는 하네스의 ContentTop(인셋 + 세로 마진).
            float safeL = SafeArea.Left / UIScale.Scale;
            float py = UISafeLayout.ContentTop;

            // 닫힘 상태에서는 패널을 화면 밖으로 '완전히' 밀어 잘린 숫자가 새어 보이지 않게 한다.
            // (기존엔 50px 띠만 남겨 우측 정렬된 스탯 값이 잘린 채 노출돼 깨져 보였음 — 가로/세로 공통 버그)
            float openX = margin + safeL;
            float closedX = -(panelW + 40f);
            float px = Mathf.Lerp(closedX, openX, toggleAnim);

            // 닫힘 탭 — 패널이 닫혀 있을수록(toggleAnim↓) 진하게. 좌측 가장자리에 떠 재확장 진입점이자
            // 레벨 요약을 보여 준다. 패널보다 먼저 그려 펼칠 때 들어오는 패널이 자연스럽게 덮도록 한다.
            Rect tabRect = DrawCollapsedTab(safeL, py, 1f - toggleAnim);
            // **필드 위에 겹쳐 그리는 것은 자기 영역을 매 프레임 등록한다.** 안 하면 그 탭이
            // 월드 클릭-이동으로 새어 캐릭터가 화면 좌상단 방향으로 걸어간다 —
            // `PlayerMovement`는 `Input.GetMouseButtonDown(0)`을 Update에서 따로 폴링하므로
            // 위의 `evt.Use()`가 막아 주지 못한다(IMGUI 밖이다). rules/ui-layout.md.
            //
            // 이 파일이 앞선 전수 점검에서 빠진 이유: `GUI.Button`이 아니라 `Event.current`로
            // 직접 히트테스트해서, 버튼 문자열로 훑는 방법에 안 걸렸다. 그 검색법도 함께 고쳤다.
            if (toggleAnim < 0.999f) FieldHudInput.RegisterBlockingRect(tabRect);

            Rect panelToggleRect = default;
            bool panelVisible = toggleAnim > 0.001f;
            if (panelVisible)
            {
                FieldHudInput.RegisterBlockingRect(new Rect(px, py, panelW, panelH));

                GUI.color = PanelBgCol;
                GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

                GUI.color = PanelAccentBlueCol;
                GUI.DrawTexture(new Rect(px, py, panelW, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(px, py + panelH - 3, panelW, 3), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(px, py, 3, panelH), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(px + panelW - 3, py, 3, panelH), Texture2D.whiteTexture);

                GUI.color = Color.white;

                float cy = py + 16;

                DrawLevelSection(px, cy, panelW);
                cy += 135;

                DrawResourceSection(px, cy, panelW);
                cy += 160;

                DrawCollectionSection(px, cy, panelW);
                cy += 85;

                DrawRegionSection(px, cy, panelW);

                float toggleSize = UIScale.IsMobileLayout ? 58f : 38f;
                panelToggleRect = new Rect(px + panelW - toggleSize - 8f, py + 8f, toggleSize, toggleSize);
                GUI.color = PanelDividerCol;
                GUI.DrawTexture(panelToggleRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(panelToggleRect, "◀", toggleStyle);
            }

            // 입력 — 패널 ◀(그려질 때)와 닫힘 탭(펼침 전)을 모두 활성화해
            // 애니메이션 구간에서 '보이는 버튼이 잠깐 무반응'하는 사각지대를 없앤다.
            // 탭은 toggleAnim<0.5에서만 받아 펼침 상태의 좌상단(레벨 뱃지) 오탭을 막는다.
            Event evt = Event.current;
            if (evt != null && evt.type == EventType.MouseDown && evt.button == 0)
            {
                bool hitPanel = panelVisible && panelToggleRect.Contains(evt.mousePosition);
                bool hitTab = toggleAnim < 0.5f && tabRect.Contains(evt.mousePosition);
                if (hitPanel || hitTab)
                {
                    expanded = !expanded;
                    evt.Use();
                }
            }
            UIScale.End();
        }

        private void DrawLevelSection(float px, float cy, float pw)
        {
            int level = progress.Level;
            int xp = progress.CurrentXp;
            int xpNeeded = progress.XpToNextLevel;

            // 캐릭터 이름. 오래 리터럴 "PLAYER"였다 — 생성 화면이 이름을 받아 저장하고
            // 클라우드 동기까지 하는데 게임 어디에서도 보여주지 않아 사실상 버려지는 값이었다.
            // 한글 12자가 150px 상자를 넘길 수 있어 LabelFit으로 줄여 맞춘다(ui-layout.md).
            UIHelper.LabelFit(new Rect(px + 20, cy, 150, 28), PlayerDisplayName, sectionTitleStyle);

            float lvBadgeX = px + 20;
            float lvBadgeY = cy + 32;

            GUI.color = LvBadgeBgDarkCol;
            GUI.DrawTexture(new Rect(lvBadgeX, lvBadgeY, 84, 60), Texture2D.whiteTexture);
            GUI.color = LvBadgeAccentCol;
            GUI.DrawTexture(new Rect(lvBadgeX, lvBadgeY, 84, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(lvBadgeX, lvBadgeY + 57, 84, 3), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(lvBadgeX, lvBadgeY + 2, 84, 22), "LEVEL", levelBadgeLabelStyle);
            GUI.Label(new Rect(lvBadgeX, lvBadgeY + 20, 84, 40), level.ToString(), levelBadgeNumStyle);

            float barX = lvBadgeX + 100;
            float barW = pw - 140;
            float barH = 26f;
            float barY = lvBadgeY + 6;

            GUI.Label(new Rect(barX, lvBadgeY - 4, barW, 24), "경험치 (EXP)", xpTitleStyle);

            GUI.color = XpBarBgCol;
            GUI.DrawTexture(new Rect(barX, barY + 24, barW, barH), Texture2D.whiteTexture);

            if (xpBarAnim > 0)
            {
                GUI.color = XpBarFillDarkCol;
                GUI.DrawTexture(new Rect(barX, barY + 24 + barH / 2, barW * xpBarAnim, barH / 2), Texture2D.whiteTexture);
                GUI.color = XpBarFillLightCol;
                GUI.DrawTexture(new Rect(barX, barY + 24, barW * xpBarAnim, barH / 2), Texture2D.whiteTexture);

                float shine = Mathf.Sin(Time.time * 2f) * 0.15f;
                if (shine > 0)
                {
                    GUI.color = new Color(1f, 1f, 1f, shine);
                    GUI.DrawTexture(new Rect(barX, barY + 24, barW * xpBarAnim, barH), Texture2D.whiteTexture);
                }
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(barX, barY + 24, barW, barH), $"{xp} / {xpNeeded}", xpTextStyle);

            int percent = xpNeeded > 0 ? Mathf.RoundToInt((float)xp / xpNeeded * 100f) : 100;
            GUI.Label(new Rect(barX, barY + 52, barW, 24), $"{percent}%", xpPctStyle);
        }

        private void DrawResourceSection(float px, float cy, float pw)
        {
            GUI.Label(new Rect(px + 20, cy, 150, 26), "RESOURCES", sectionTitleStyle);

            float halfW = (pw - 56) / 2f;

            // Row 1: 캔디 + 코인
            float row1Y = cy + 32;
            int candies = candyInventory != null ? candyInventory.Candies : 0;
            DrawStatBox(px + 20, row1Y, halfW, 56, "캔디", candies.ToString(), StatCandyPinkCol);
            int coins = currencyWallet != null ? currencyWallet.Coins : 0;
            DrawStatBox(px + 20 + halfW + 14, row1Y, halfW, 56, "코인", coins.ToString(), StatCoinGoldCol);

            // Row 2: 보석 + 배틀팀
            float row2Y = row1Y + 62;
            int gems = currencyWallet != null ? currencyWallet.Gems : 0;
            DrawStatBox(px + 20, row2Y, halfW, 56, "보석", gems.ToString(), StatGemBlueCol);
            int teamCount = teamManager != null ? teamManager.FilledSlots : 0;
            DrawStatBox(px + 20 + halfW + 14, row2Y, halfW, 56, "배틀팀", $"{teamCount}/5", StatTeamOrangeCol);
        }

        private void DrawCollectionSection(float px, float cy, float pw)
        {
            GUI.Label(new Rect(px + 20, cy, 180, 26), "COLLECTION", sectionTitleStyle);

            float rowY = cy + 32;
            float thirdW = (pw - 68) / 3f;

            // GetAllOwned 캐싱 — InsectUpdated 이벤트로 invalidate (매 프레임 List 할당 회피).
            if (ownedCountCacheDirty && insectCollection != null)
            {
                cachedOwnedCount = insectCollection.GetAllOwned().Count;
                ownedCountCacheDirty = false;
            }
            DrawStatBox(px + 20, rowY, thirdW, 56, "보유", cachedOwnedCount.ToString(), StatOwnedGreenCol);

            int discovered = 0;
            int captured = 0;
            if (dexController != null)
            {
                var data = dexController.GetSaveData();
                if (data != null && data.records != null)
                {
                    discovered = data.records.Count;
                    foreach (var r in data.records)
                        if (r.capturedCount > 0) captured++;
                }
            }
            DrawStatBox(px + 20 + thirdW + 12, rowY, thirdW, 56, "발견", discovered.ToString(), StatDiscoveredBlueCol);
            DrawStatBox(px + 20 + (thirdW + 12) * 2, rowY, thirdW, 56, "포획", captured.ToString(), StatCapturedGoldCol);
        }

        private void DrawRegionSection(float px, float cy, float pw)
        {
            GUI.Label(new Rect(px + 20, cy, 150, 26), "LOCATION", sectionTitleStyle);

            string regionName = "탐험 중...";
            Color regionCol = RegionDefaultCol;
            string regionInsects = "";
            if (regionManager != null && regionManager.CurrentRegion != null)
            {
                var r = regionManager.CurrentRegion;
                regionName = r.displayName;
                regionCol = r.themeColor;
                if (r.insectIds != null && r.insectIds.Length > 0)
                    regionInsects = $"출현 곤충: {r.insectIds.Length}종";
            }

            regionNameStyle.normal.textColor = regionCol;
            GUI.Label(new Rect(px + 20, cy + 30, pw - 40, 36), regionName, regionNameStyle);

            // SubArea 안에서는 이름을 상시 표시 (▾ 표시 + 빛바랜 색)
            float subY = cy + 62;
            if (regionManager != null && regionManager.CurrentSubArea != null)
            {
                Color subCol = new Color(regionCol.r * 0.85f + 0.15f, regionCol.g * 0.85f + 0.15f, regionCol.b * 0.85f + 0.15f);
                regionSubStyle.normal.textColor = subCol;
                UIHelper.LabelFit(new Rect(px + 20, subY, pw - 40, 24), $"▾ {regionManager.CurrentSubArea.displayName}", regionSubStyle);
                subY += 22;
            }

            if (!string.IsNullOrEmpty(regionInsects))
                GUI.Label(new Rect(px + 20, subY, pw - 40, 24), regionInsects, regionSubStyle);
        }

        private void DrawStatBox(float x, float y, float w, float h, string label, string value, Color accent)
        {
            GUI.color = new Color(accent.r * 0.08f, accent.g * 0.08f, accent.b * 0.08f, 0.8f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(x, y, w, 3), Texture2D.whiteTexture);

            statBoxLblStyle.normal.textColor = new Color(accent.r * 0.7f, accent.g * 0.7f, accent.b * 0.7f);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8, y + 4, w - 16, 22), label, statBoxLblStyle);

            statBoxValStyle.normal.textColor = accent;
            GUI.Label(new Rect(x + 8, y + 20, w - 16, 34), value, statBoxValStyle);
        }

        public void AutoWire(PlayerProgressController prog, PlayerCandyInventory candy,
            PlayerInsectCollection collection, PlayerItemInventory items,
            DexController dex, BattleTeamManager team, RegionManager region)
        {
            if (progress == null) progress = prog;
            if (candyInventory == null) candyInventory = candy;
            if (insectCollection == null)
            {
                insectCollection = collection;
                // **구독까지 여기서 해야 한다.** Bootstrap이 EnsureComponent → AutoWire 순서라
                // AddComponent가 부르는 OnEnable 시점엔 insectCollection이 아직 null이고,
                // 그쪽 `insectCollection != null` 가드가 거짓이라 구독이 통째로 건너뛰어진다.
                // 그러면 ownedCountCacheDirty를 되살릴 경로가 없어져 COLLECTION의 "보유" 숫자가
                // 첫 프레임 값에서 세션 내내 고정된다(곤충을 잡아도 안 변한다).
                // 바로 아래 regionManager가 같은 이유로 이미 이 형태를 쓰고 있었는데 여기만 빠져 있었다.
                if (insectCollection != null && !subscribedInsects)
                {
                    insectCollection.InsectUpdated += HandleInsectUpdated;
                    subscribedInsects = true;
                    ownedCountCacheDirty = true;
                }
            }
            if (itemInventory == null) itemInventory = items;
            if (dexController == null) dexController = dex;
            if (teamManager == null) teamManager = team;
            if (regionManager == null)
            {
                regionManager = region;
                if (regionManager != null && !subscribedSubArea)
                {
                    regionManager.SubAreaChanged += OnSubAreaEntered;
                    subscribedSubArea = true;
                }
            }
        }

        public void AutoWire(PlayerCurrencyWallet wallet)
        {
            if (currencyWallet == null) currencyWallet = wallet;
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            sectionTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
            sectionTitleStyle.normal.textColor = new Color(0.5f, 0.6f, 0.8f);

            levelBadgeLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            levelBadgeLabelStyle.normal.textColor = new Color(0.5f, 0.7f, 1f);

            levelBadgeNumStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            levelBadgeNumStyle.normal.textColor = Color.white;

            xpTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            xpTitleStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f);

            xpTextStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            xpTextStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);

            xpPctStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleRight };
            xpPctStyle.normal.textColor = new Color(0.5f, 0.65f, 0.9f);

            regionNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };

            regionSubStyle = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            regionSubStyle.normal.textColor = new Color(0.55f, 0.6f, 0.7f);

            statBoxLblStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            statBoxValStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };

            toggleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            toggleStyle.normal.textColor = new Color(0.6f, 0.7f, 0.9f);

            alertNameStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            alertDescStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter };

            // 닫힘 탭 전용 스타일 (textColor는 흰색 기반 — 알파는 GUI.color로 곱해 페이드)
            tabLabelStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            tabLabelStyle.normal.textColor = new Color(0.5f, 0.7f, 1f);
            tabNumStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            tabNumStyle.normal.textColor = Color.white;
            tabHintStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            tabHintStyle.normal.textColor = new Color(0.6f, 0.7f, 0.9f);
        }

        // OnGUI Rect 캐싱 회피용 — 닫힘 탭은 좌표가 safeL/py에만 의존해 매 프레임 new Rect를 만들지만
        // 닫힘 상태에서만 그려지고 항목 수가 적어 영향 미미. 패널/탭 모두 IMGUI 관용 패턴 유지.
        private Rect DrawCollapsedTab(float safeL, float py, float strength)
        {
            float tabW = UIScale.IsMobileLayout ? 72f : 54f;
            float tabH = 134f;
            float tabX = safeL + 6f;
            float tabY = py;
            Rect rect = new Rect(tabX, tabY, tabW, tabH);

            float a = Mathf.Clamp01(strength);
            if (a <= 0.001f) return rect; // 완전히 열림 — 탭은 그리지 않음

            // 배경 + accent (알파는 strength로 페이드)
            GUI.color = new Color(PanelBgCol.r, PanelBgCol.g, PanelBgCol.b, PanelBgCol.a * a);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(PanelAccentBlueCol.r, PanelAccentBlueCol.g, PanelAccentBlueCol.b, a);
            GUI.DrawTexture(new Rect(tabX, tabY, tabW, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(tabX + tabW - 3, tabY, 3, tabH), Texture2D.whiteTexture);

            // LV 뱃지 (닫힘 상태에서도 레벨 요약 표시)
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(tabX, tabY + 12, tabW, 18), "LV", tabLabelStyle);
            GUI.Label(new Rect(tabX, tabY + 28, tabW, 36), progress.Level.ToString(), tabNumStyle);

            GUI.color = new Color(PanelDividerCol.r, PanelDividerCol.g, PanelDividerCol.b, a);
            GUI.DrawTexture(new Rect(tabX + 12, tabY + 74, tabW - 24, 2), Texture2D.whiteTexture);

            // ▶ 펼치기 안내
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(tabX, tabY + 84, tabW, 40), "▶", tabHintStyle);

            GUI.color = Color.white;
            return rect;
        }

        private void DrawSubAreaAlert()
        {
            if (subAreaAlertTimer <= 0f) return;

            InitStyles();

            float alpha = Mathf.Clamp01(subAreaAlertTimer / 0.5f);
            float sw = UIScale.VirtualScreenWidth;

            // 배경
            // 세로 기준을 하네스로 — 이 배너만 y가 70/74/110으로 박혀 있어 노치 기기에서
            // 상태바 뒤로 들어갔다(VirtualSafeTop이 130쯤이면 배너 전체가 가려진다).
            // +70은 상단 중앙 관례를 그대로 지킨 값이다: ContentTop(토스트) → +30(퀘스트 토스트)
            // → 여기(+70~142) → +150(가이드 코치 배너).
            float ay = UISafeLayout.ContentTop + 70f;

            GUI.color = new Color(0f, 0f, 0f, 0.75f * alpha);
            GUI.DrawTexture(new Rect(sw * 0.2f, ay, sw * 0.6f, 72), Texture2D.whiteTexture);
            // 상단 라인
            GUI.color = new Color(1f, 0.85f, 0.3f, 0.8f * alpha);
            GUI.DrawTexture(new Rect(sw * 0.2f, ay, sw * 0.6f, 3), Texture2D.whiteTexture);

            // 서브에리어 이름 (캐시된 스타일 + 알파만 변경)
            alertNameStyle.normal.textColor = new Color(1f, 0.9f, 0.4f, alpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(sw * 0.2f, ay + 4f, sw * 0.6f, 38), subAreaAlertName, alertNameStyle);

            // 설명
            alertDescStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, alpha * 0.9f);
            GUI.Label(new Rect(sw * 0.2f, ay + 40f, sw * 0.6f, 26), subAreaAlertDesc, alertDescStyle);

            GUI.color = Color.white;
        }
    }
}
