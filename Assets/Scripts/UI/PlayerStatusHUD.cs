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
        private GUIStyle playerNameStyle;
        private GUIStyle levelBadgeLabelStyle;
        private GUIStyle levelBadgeNumStyle;
        private GUIStyle xpTitleStyle;
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

        /// <summary>
        /// 닫힘 탭의 오른쪽 끝(가상 x). 상단 가운데 리전 배너가 세로 화면에서 이 탭과 우상단 단축 바
        /// 사이에 자리를 잡으려고 읽는다. 탭 폭이 바뀌면 배너도 같이 따라간다.
        /// </summary>
        public static float CollapsedTabRight => UIScale.VirtualSafeLeft + 8f + CollapsedTabWidth;

        private static float CollapsedTabWidth => UIScale.IsMobileLayout ? 84f : 64f;

        /// <summary>닫힘 탭의 자리 — 순수 계산(전수 겹침 검사가 부른다).</summary>
        public static Rect TabRect(HudFrame f)
        {
            return new Rect(f.SafeLeft + 8f, f.ContentTop, f.Mobile ? 84f : 64f, 140f);
        }

        /// <summary>펼친 패널의 자리(다 펼쳤을 때) — 순수 계산. 가운데 무대(<see cref="HudStage.Area"/>)가 이 오른쪽에서 시작한다.</summary>
        public static Rect PanelRect(HudFrame f)
        {
            return new Rect(f.SafeLeft + 20f, f.ContentTop, PanelW, f.ClampHeight(PanelH));
        }

        private bool expanded = true;
        private bool mobileLayoutInitialized;
        private float xpBarAnim;
        private float toggleAnim = 1f;

        private string subAreaAlertName;
        private string subAreaAlertDesc;
        private float subAreaAlertTimer;
        private bool subscribedSubArea;

        // 색은 전부 UITheme 토큰에서 받는다(rules/ui-layout.md). 이 파일이 한때 자기 색 16개와
        // 각진 사각형 21개로 그려, 같은 화면의 미니맵·퀘스트 칩(HudCard)과 다른 앱처럼 보였다.
        private const float PanelW = 480f;
        private const float PanelH = 540f;
        private const float TileH = 58f;

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
                    cachedPlayerName = string.IsNullOrWhiteSpace(saved) ? "탐험가" : saved;
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
            {
                HudStage.Request(HudStageItem.PlaceAlert);   // 떠 있는 동안 무대의 다른 차례를 붙잡아 둔다
                subAreaAlertTimer -= Time.deltaTime;
            }
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
            if (ModalUIRegistry.IsAnyOpen() || DreamPrologueState.Active) return;

            UIScale.Begin();
            DrawSubAreaAlert();

            if (progress == null) { UIScale.End(); return; }

            InitStyles();

            // 자리는 PanelRect/TabRect(순수 계산) — 세이프 에어리어 안쪽, 세로는 하네스의 ContentTop(인셋 + 세로 마진).
            HudFrame frame = HudFrame.Current;
            Rect openRect = PanelRect(frame);
            float panelW = openRect.width;
            float panelH = openRect.height;
            float safeL = frame.SafeLeft;
            float py = openRect.y;

            // 닫힘 상태에서는 패널을 화면 밖으로 '완전히' 밀어 잘린 숫자가 새어 보이지 않게 한다.
            // (기존엔 50px 띠만 남겨 우측 정렬된 스탯 값이 잘린 채 노출돼 깨져 보였음 — 가로/세로 공통 버그)
            float openX = openRect.x;
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

                // 미니맵·퀘스트 칩과 같은 반투명 HUD 카드 — 월드가 비치되 글자 대비는 유지된다.
                UISurface.HudCard(new Rect(px, py, panelW, panelH));
                GUI.color = Color.white;

                DrawLevelSection(px, py + 14f, panelW);
                DrawResourceSection(px, py + 142f, panelW);
                DrawCollectionSection(px, py + 300f, panelW);
                DrawRegionSection(px, py + 402f, panelW);

                float toggleSize = UIScale.IsMobileLayout ? 58f : 44f;
                panelToggleRect = new Rect(px + panelW - toggleSize - 12f, py + 12f, toggleSize, toggleSize);
                UISurface.Rounded(panelToggleRect, UITheme.Instance.surfaceRaised, UITheme.Radius.Chip);
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

        private void DrawLevelSection(float px, float top, float pw)
        {
            UITheme t = UITheme.Instance;
            int level = progress.Level;
            int xp = progress.CurrentXp;
            int xpNeeded = progress.XpToNextLevel;

            // 캐릭터 이름. 오래 리터럴 "PLAYER"였다 — 생성 화면이 이름을 받아 저장하고
            // 클라우드 동기까지 하는데 게임 어디에서도 보여주지 않아 사실상 버려지는 값이었다.
            // 한글 12자가 상자를 넘길 수 있어 LabelFit으로 줄여 맞춘다(ui-layout.md).
            // 오른쪽 76px는 접기 버튼 자리다.
            UIHelper.LabelFit(new Rect(px + 20f, top, pw - 96f, 34f), PlayerDisplayName, playerNameStyle);

            Rect badge = new Rect(px + 20f, top + 44f, 88f, 70f);
            UISurface.Rounded(badge, Color.Lerp(t.surfaceRaised, t.accentMint, 0.45f), UITheme.Radius.Chip);
            GUI.Label(new Rect(badge.x, badge.y + 2f, badge.width, 24f), "Lv", levelBadgeLabelStyle);
            GUI.Label(new Rect(badge.x, badge.y + 22f, badge.width, 46f), level.ToString(), levelBadgeNumStyle);

            float barX = badge.xMax + 16f;
            float barW = px + pw - 20f - barX;
            int percent = xpNeeded > 0 ? Mathf.RoundToInt((float)xp / xpNeeded * 100f) : 100;
            GUI.Label(new Rect(barX, top + 46f, barW, 26f), "경험치", xpTitleStyle);
            GUI.Label(new Rect(barX, top + 46f, barW, 26f), $"{xp} / {xpNeeded} · {percent}%", xpPctStyle);

            // 20px 막대 — 둥근 9-slice는 테두리가 높이를 넘겨 뭉개지므로 각진 채로 둔다(ui-layout.md).
            Rect track = new Rect(barX, top + 80f, barW, 20f);
            UISurface.Flat(track, t.surfaceBase);
            if (xpBarAnim > 0f)
            {
                // 민트 — 보유 곤충 상세의 경험치 막대와 같은 색. 코랄은 이 테마에서 HP 위험·닫기 색이다.
                Rect fill = new Rect(track.x, track.y, track.width * xpBarAnim, track.height);
                UISurface.Flat(fill, t.accentMint);
                // 윗면 광택 + 느린 반짝임 — 막대가 살아 있다는 신호만 준다.
                float shine = 0.16f + Mathf.Max(0f, Mathf.Sin(Time.time * 2f)) * 0.12f;
                UISurface.Flat(new Rect(fill.x, fill.y, fill.width, fill.height * 0.4f), new Color(1f, 1f, 1f, shine));
            }
        }

        private void DrawResourceSection(float px, float top, float pw)
        {
            UITheme t = UITheme.Instance;
            GUI.Label(new Rect(px + 20f, top, pw - 40f, 26f), "재화", sectionTitleStyle);

            float gap = 12f;
            float halfW = (pw - 40f - gap) / 2f;
            float row1Y = top + 30f;
            float row2Y = row1Y + TileH + 8f;

            int candies = candyInventory != null ? candyInventory.Candies : 0;
            int coins = currencyWallet != null ? currencyWallet.Coins : 0;
            int gems = currencyWallet != null ? currencyWallet.Gems : 0;
            int teamCount = teamManager != null ? teamManager.FilledSlots : 0;
            DrawStatBox(px + 20f, row1Y, halfW, TileH, "캔디", candies.ToString(), t.accentCoral);
            DrawStatBox(px + 20f + halfW + gap, row1Y, halfW, TileH, "코인", coins.ToString(), t.coinColor);
            // 보석은 파랑 — accentColor는 코랄로 동기화돼 있어(UITheme.SynchronizeLegacyTokens) 캔디와 같은 색이 됐다.
            DrawStatBox(px + 20f, row2Y, halfW, TileH, "보석", gems.ToString(), t.itemRare);
            DrawStatBox(px + 20f + halfW + gap, row2Y, halfW, TileH, "배틀팀",
                $"{teamCount}/{BattleTeamManager.MaxSlots}", t.accentAmber);
        }

        private void DrawCollectionSection(float px, float top, float pw)
        {
            UITheme t = UITheme.Instance;
            GUI.Label(new Rect(px + 20f, top, pw - 40f, 26f), "수집", sectionTitleStyle);

            float gap = 10f;
            float thirdW = (pw - 40f - gap * 2f) / 3f;
            float rowY = top + 30f;

            // GetAllOwned 캐싱 — InsectUpdated 이벤트로 invalidate (매 프레임 List 할당 회피).
            if (ownedCountCacheDirty && insectCollection != null)
            {
                cachedOwnedCount = insectCollection.GetAllOwned().Count;
                ownedCountCacheDirty = false;
            }

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
            DrawStatBox(px + 20f, rowY, thirdW, TileH, "보유", cachedOwnedCount.ToString(), t.accentMint);
            DrawStatBox(px + 20f + thirdW + gap, rowY, thirdW, TileH, "발견", discovered.ToString(), t.textSecondary);
            DrawStatBox(px + 20f + (thirdW + gap) * 2f, rowY, thirdW, TileH, "포획", captured.ToString(), t.accentAmber);
        }

        private void DrawRegionSection(float px, float top, float pw)
        {
            UITheme t = UITheme.Instance;
            GUI.Label(new Rect(px + 20f, top, pw - 40f, 26f), "현재 위치", sectionTitleStyle);

            string regionName = "탐험 중...";
            Color regionCol = t.textSecondary;
            string regionInsects = "";
            if (regionManager != null && regionManager.CurrentRegion != null)
            {
                var r = regionManager.CurrentRegion;
                regionName = r.displayName;
                regionCol = r.themeColor;
                if (r.insectIds != null && r.insectIds.Length > 0)
                    regionInsects = $"출현 곤충 {r.insectIds.Length}종";
            }

            // 리전 색 점 + 이름 — 색을 글자에만 칠하면 어두운 테마색 리전은 읽기 어렵다.
            UISurface.Rounded(new Rect(px + 20f, top + 40f, 14f, 14f), regionCol, 4f);
            regionNameStyle.normal.textColor = Color.Lerp(regionCol, t.textPrimary, 0.25f);
            UIHelper.LabelFit(new Rect(px + 42f, top + 28f, pw - 62f, 38f), regionName, regionNameStyle);

            // SubArea 안에서는 이름을 상시 표시 (▾ 표시 + 빛바랜 색)
            float subY = top + 68f;
            if (regionManager != null && regionManager.CurrentSubArea != null)
            {
                regionSubStyle.normal.textColor = Color.Lerp(regionCol, t.textPrimary, 0.4f);
                UIHelper.LabelFit(new Rect(px + 42f, subY, pw - 62f, 26f), $"▾ {regionManager.CurrentSubArea.displayName}", regionSubStyle);
                subY += 26f;
            }

            if (!string.IsNullOrEmpty(regionInsects))
            {
                regionSubStyle.normal.textColor = t.textSecondary;
                GUI.Label(new Rect(px + 42f, subY, pw - 62f, 26f), regionInsects, regionSubStyle);
            }
        }

        /// <summary>
        /// 재화·수집 타일 — 둥근 표면 + 위쪽 액센트 줄. 줄은 3px라 각진 채로 두고
        /// 가로를 반경만큼 물려 둥근 모서리를 뚫지 않게 한다(ui-layout.md).
        /// </summary>
        private void DrawStatBox(float x, float y, float w, float h, string label, string value, Color accent)
        {
            UITheme t = UITheme.Instance;
            UISurface.Rounded(new Rect(x, y, w, h), t.surfaceRaised, UITheme.Radius.Chip);
            UISurface.Flat(new Rect(x + UITheme.Radius.Chip, y + 3f, w - UITheme.Radius.Chip * 2f, 3f), accent);

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12f, y + 8f, w - 24f, 22f), label, statBoxLblStyle);

            statBoxValStyle.normal.textColor = accent;
            UIHelper.LabelFit(new Rect(x + 12f, y + 22f, w - 24f, 34f), value, statBoxValStyle);
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
            UITheme t = UITheme.Instance;

            sectionTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            sectionTitleStyle.normal.textColor = t.textMuted;

            playerNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            playerNameStyle.normal.textColor = t.textPrimary;

            levelBadgeLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            levelBadgeLabelStyle.normal.textColor = t.textSecondary;

            levelBadgeNumStyle = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            levelBadgeNumStyle.normal.textColor = t.textPrimary;

            xpTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            xpTitleStyle.normal.textColor = t.textSecondary;

            xpPctStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            xpPctStyle.normal.textColor = t.textPrimary;

            regionNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };

            regionSubStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            regionSubStyle.normal.textColor = t.textSecondary;

            statBoxLblStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            statBoxLblStyle.normal.textColor = t.textSecondary;
            statBoxValStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };

            toggleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            toggleStyle.normal.textColor = t.textPrimary;

            alertNameStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            alertDescStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter };

            // 닫힘 탭 전용 스타일 (알파는 GUI.color로 곱해 페이드)
            tabLabelStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            tabLabelStyle.normal.textColor = t.textSecondary;
            tabNumStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            tabNumStyle.normal.textColor = t.textPrimary;
            tabHintStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            tabHintStyle.normal.textColor = t.textSecondary;
        }

        // OnGUI Rect 캐싱 회피용 — 닫힘 탭은 좌표가 safeL/py에만 의존해 매 프레임 new Rect를 만들지만
        // 닫힘 상태에서만 그려지고 항목 수가 적어 영향 미미. 패널/탭 모두 IMGUI 관용 패턴 유지.
        private Rect DrawCollapsedTab(float safeL, float py, float strength)
        {
            Rect rect = TabRect(HudFrame.Current);
            float tabW = rect.width;
            float tabX = rect.x;
            float tabY = rect.y;

            float a = Mathf.Clamp01(strength);
            if (a <= 0.001f) return rect; // 완전히 열림 — 탭은 그리지 않음

            // 알파는 GUI.color로 곱한다 — UISurface.Rounded가 호출부 알파를 살린다.
            GUI.color = new Color(1f, 1f, 1f, a);
            UISurface.HudCard(rect);

            // 레벨 요약 — 닫힘 상태에서도 보인다.
            GUI.Label(new Rect(tabX, tabY + 8f, tabW, 24f), "Lv", tabLabelStyle);
            GUI.Label(new Rect(tabX, tabY + 30f, tabW, 40f), progress.Level.ToString(), tabNumStyle);

            UISurface.Flat(new Rect(tabX + 14f, tabY + 80f, tabW - 28f, 2f), UITheme.Instance.surfaceBorder);

            // ▶ 펼치기 안내
            GUI.Label(new Rect(tabX, tabY + 88f, tabW, 42f), "▶", tabHintStyle);

            GUI.color = Color.white;
            return rect;
        }

        /// <summary>
        /// 서브에리어·섬에 들어선 알림의 자리 — 가운데 무대의 고정 칸(<see cref="HudStageItem.PlaceAlert"/>). 예전엔 화면 폭 60%로
        /// 위쪽 가운데(ContentTop+70)에 떠서 리전 배너·내기 점수판·퀘스트 완료 알림·동굴 출입 토스트와 겹쳤다.
        /// </summary>
        public static Rect SubAreaAlertRect(HudFrame f)
        {
            return HudStage.Place(f, HudStageItem.PlaceAlert, f.Width * 0.6f, HudStage.PlaceAlertHeight);
        }

        private void DrawSubAreaAlert()
        {
            if (subAreaAlertTimer <= 0f) return;
            // 가운데 무대의 고정 칸(리전 진입 알림)에 선다 — 퀘스트 완료 같은 다른 카드는 이게 지나갈 때까지 기다린다.
            InitStyles();
            UITheme t = UITheme.Instance;

            float alpha = Mathf.Clamp01(subAreaAlertTimer / 0.5f);
            Rect banner = SubAreaAlertRect(HudFrame.Current);
            HudStage.Request(HudStageItem.PlaceAlert, banner);
            float ay = banner.y;

            GUI.color = new Color(1f, 1f, 1f, alpha);
            UISurface.HudCard(banner);
            UISurface.Flat(new Rect(banner.x + UITheme.Radius.Card, banner.y + 3f,
                banner.width - UITheme.Radius.Card * 2f, 3f), t.accentAmber);

            // 서브에리어 이름·설명 — 스타일은 캐시하고 알파만 GUI.color로 곱한다.
            alertNameStyle.normal.textColor = t.accentAmber;
            UIHelper.LabelFit(new Rect(banner.x + 16f, ay + 6f, banner.width - 32f, 38f), subAreaAlertName, alertNameStyle);
            alertDescStyle.normal.textColor = t.textSecondary;
            UIHelper.LabelFit(new Rect(banner.x + 16f, ay + 44f, banner.width - 32f, 26f), subAreaAlertDesc, alertDescStyle);

            GUI.color = Color.white;
        }
    }
}
