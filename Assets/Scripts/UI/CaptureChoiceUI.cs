using InsectGame.Battle;
using InsectGame.Capture;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.UI
{
    public class CaptureChoiceUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private CaptureMinigameController minigame;
        [SerializeField] private InsectBattleController battleController;
        [SerializeField] private InsectBattleUIController battleUi;
        [SerializeField] private BattleTeamManager teamManager;
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private CaptureProximityTrigger proximityTrigger;
        [SerializeField] private CaptureController captureController;
        [SerializeField] private Dex.DexController dexController;
        [SerializeField] private TrainingManager trainingManager;
        [SerializeField] private PlayerItemInventory itemInventory;
        [SerializeField] private RaidBattleController raidController;
        [SerializeField] private PlayerMovement playerMovement;

        private CaptureItemData[] captureItems;

        private bool isOpen;
        public bool IsOpen => isOpen;

        /// <summary>
        /// 모달 닫기(ESC — <c>PlayerMovement</c>·<c>ModalUIRegistry.HandleEscape</c>). 습격 창에서는 <b>그냥 닫지 않고 도망 판정으로 보낸다</b>
        /// (<see cref="RequestClose"/>) — 닫기가 도망을 우회하면 습격이 무의미해진다.
        /// </summary>
        public void CloseModal() { RequestClose(); }
        private InsectEntity targetInsect;
#pragma warning disable 0414
        private int selectedTeamSlot = -1;
#pragma warning restore 0414
        private bool showTeamSelect;
        private bool showItemSelect;

        public bool IsChoiceOpen => isOpen;

        public void SetCaptureItems(CaptureItemData[] items)
        {
            captureItems = items;
        }

        public void ShowChoice(InsectEntity target)
        {
            if (target == null || target.Data == null) return;
            targetInsect = target;
            target.SetEngaged(true); // 포획 상호작용 중 — 곤충 도주 방지
            isOpen = true;
            ambushMode = false;      // 습격 창은 ShowAmbush가 이 위에 켠다
            openedFrame = Time.frameCount;
            openKeyHeld = Input.GetKey(KeyCode.E);
            EnsureLastNetLoaded();   // 창을 열 때 한 번 — 그리는 쪽(OnGUI 패스마다)에서 키 문자열을 만들지 않는다
            selectedTeamSlot = -1;
            showTeamSelect = false;
            showItemSelect = false;
            ModalUIRegistry.Register(this);
            if (playerMovement != null) playerMovement.SetFrozen(true);
        }

        public void Hide()
        {
            isOpen = false;
            if (targetInsect != null) targetInsect.SetEngaged(false); // 포획 취소 — 곤충 정상 행동 복귀
            targetInsect = null;
            showTeamSelect = false;
            showItemSelect = false;
            ambushMode = false;
            ModalUIRegistry.Unregister(this);
            if (playerMovement != null) playerMovement.SetFrozen(false);
        }

        /// <summary>
        /// 닫기 요청 — ESC가 오는 길(이 창의 키 처리 · <c>CaptureInputController</c> · <c>PlayerMovement</c>의 모달 ESC)이 전부 여기로 모인다.
        /// 보통 창은 닫고, 습격 창은 도망 판정(팀 고르기 중이면 습격 창으로 돌아가기)으로 보낸다.
        /// </summary>
        public void RequestClose()
        {
            if (!isOpen) return;
            if (!ambushMode)
            {
                Hide();
                return;
            }
            HandleAmbushEscapeKey();
        }

        private void OnDisable() { ModalUIRegistry.Unregister(this); }

        private void Update()
        {
            if (!isOpen) return;

            if (targetInsect == null || !targetInsect.gameObject.activeInHierarchy)
            {
                Hide();
                return;
            }

            // 도망에 실패했다 — 「도망치지 못했다!」를 잠깐 보여 준 뒤 싸움으로 넘어간다(한 번만. 팀 고르기에서 ‹ 뒤로를 누르면 [싸우기]만 남은 창이다).
            if (ambushMode && ambushEscapeFailed && !ambushAutoFought && !showTeamSelect
                && Time.unscaledTime - ambushFailAt >= AmbushRules.EscapeFailPauseSeconds)
            {
                ambushAutoFought = true;
                BeginAmbushFight();
                if (!isOpen) return;
            }

            HandleInputUpdate();
        }

        // 이 창을 연 프레임. 필드에서 [E]로 곤충에게 말을 걸면 **그 같은 누름**이 이 창의 [E](미니게임 포획)로도
        // 읽힌다 — 예전엔 채집망 선택 창이 한 겹 더 있어 티가 안 났지만, 이제 [E]가 곧바로 채집망을 쓰고
        // 미니게임을 열므로 포획/배틀을 고를 틈도 없이 넘어가 버린다. 연 직후의 키는 받지 않는다.
        private int openedFrame = -1;
        // 창을 연 [E]를 아직 누르고 있는가. IMGUI는 누르고 있는 키의 KeyDown을 되풀이해 보내므로(키 반복)
        // 프레임 수만으로는 못 막는다 — 손을 뗄 때까지 [E]는 받지 않는다.
        private bool openKeyHeld;

        private void HandleInput(KeyCode key)
        {
            if (!isOpen) return;
            if (Time.frameCount <= openedFrame + 1) return;
            if (ambushMode)
            {
                HandleAmbushInput(key);
                return;
            }
            if (openKeyHeld)
            {
                if (Input.GetKey(KeyCode.E)) { if (key == KeyCode.E) return; }
                else openKeyHeld = false;
            }

            if (showTeamSelect)
            {
                if (key == KeyCode.Escape || key == KeyCode.Backspace)
                    showTeamSelect = false;
            }
            else if (showItemSelect)
            {
                if (key == KeyCode.Escape || key == KeyCode.Backspace)
                    showItemSelect = false;
            }
            else
            {
                bool isRaid = IsRaidTarget();
                bool isGuardian = targetInsect != null && targetInsect.IsGuardian;
                if (!isRaid && (key == KeyCode.E || key == KeyCode.Alpha1))
                {
                    // 버튼과 같은 조건 — 수문장 포획은 키로도 우회할 수 없다(잡히면 리전 영구 잠김).
                    if (HasAnyCaptureItem() && !isGuardian)
                        BeginQuickCapture();
                }
                if (!isRaid && (key == KeyCode.B || key == KeyCode.Alpha2))
                {
                    bool hasTeam = teamManager != null && teamManager.HasAnyInsect();
                    if (hasTeam)
                        showTeamSelect = true;
                }
                if (isRaid && (key == KeyCode.R || key == KeyCode.Alpha1 || key == KeyCode.Alpha3))
                {
                    // 버튼과 같은 조건 — 키 입력으로 비활성 버튼을 우회하면 안 된다.
                    bool hasFullTeam = teamManager != null && teamManager.FilledSlots >= 5;
                    if (hasFullTeam && CountBattleReadyTeamMembers() > 0)
                        StartRaidBattle();
                }
                if (key == KeyCode.Escape)
                    Hide();
            }
        }

        // 매 프레임 새 배열을 만들지 않는다. F는 습격 창의 [도망치기]다(보통 창에서는 아무 일도 하지 않는다).
        private static readonly KeyCode[] InputKeys = { KeyCode.E, KeyCode.B, KeyCode.R, KeyCode.F, KeyCode.Escape,
                                                        KeyCode.Backspace, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };

        private void HandleInputUpdate()
        {
            for (int i = 0; i < InputKeys.Length; i++)
            {
                if (!isOpen) return;   // 앞 키가 창을 닫았다(싸우기·도망 성공)
                if (Input.GetKeyDown(InputKeys[i]))
                    HandleInput(InputKeys[i]);
            }
        }

        private void OnGUI()
        {
            if (!isOpen || targetInsect == null) return;

            Event evt = Event.current;
            if (evt != null && evt.type == EventType.KeyDown && evt.keyCode != KeyCode.None)
            {
                HandleInput(evt.keyCode);
                evt.Use();
            }

            // 키 처리가 창을 닫았을 수 있다(싸우기 → 전투, 도망 성공) — 닫힌 창을 이번 패스에 그리지 않는다.
            if (!isOpen || targetInsect == null) return;

            UIScale.Begin();
            if (showTeamSelect)
                DrawTeamSelect();
            else if (showItemSelect)
                DrawItemSelect();
            else if (ambushMode)
                DrawAmbush();
            else
                DrawChoice();
            UIScale.End();
        }

        /// <summary>
        /// 레이드로만 맞설 수 있는 등급(영웅·전설) — 포획 선택 창의 분기와 습격 판정(<c>CaptureInputController</c>의 「싸울 수 있나」)이
        /// 같은 답을 읽는다. 두 곳이 따로 적으면 습격 창이 막힌 버튼으로 뜬다.
        /// </summary>
        public static bool IsRaidRarity(InsectRarity rarity)
        {
            return rarity == InsectRarity.Epic || rarity == InsectRarity.Legendary;
        }

        private bool IsRaidTarget()
        {
            return targetInsect != null && targetInsect.Data != null && IsRaidRarity(targetInsect.Data.rarity);
        }

        // ── 스타일 캐시 ──
        // 예전엔 세 화면이 OnGUI 패스마다 GUIStyle을 20개 가까이 새로 만들었다(패스는 프레임당 여러 번이다).
        // 한 번만 만들고 동적인 것(글자색)만 매번 바꾼다(BattleScreenUI·BattleTeamUI와 같은 패턴).
        private GUIStyle titleStyle, subStyle, nameStyle, buttonStyle, cancelStyle, hintStyle;
        private GUIStyle raidHintStyle, raidDescStyle, itemNameStyle, itemDescStyle, countStyle;
        private GUIStyle slotNumStyle, slotNameStyle, slotInfoStyle, netRowStyle;
        private GUIStyle ambushTitleStyle, ambushFailStyle;
        private bool stylesReady;

        private void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            UITheme t = UITheme.Instance;

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = t.textPrimary;
            subStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            subStyle.normal.textColor = t.textSecondary;
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            buttonStyle.normal.textColor = t.textPrimary;
            cancelStyle = new GUIStyle(buttonStyle) { fontSize = 26 };
            netRowStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            netRowStyle.normal.textColor = t.textSecondary;
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            raidHintStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            raidHintStyle.normal.textColor = t.accentCoral;
            raidDescStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, alignment = TextAnchor.MiddleCenter };
            raidDescStyle.normal.textColor = t.textSecondary;
            itemNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = true };
            itemDescStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, alignment = TextAnchor.MiddleLeft };
            itemDescStyle.normal.textColor = t.textSecondary;
            countStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            slotNumStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            slotNumStyle.normal.textColor = t.textMuted;
            slotNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            slotInfoStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, alignment = TextAnchor.MiddleLeft };
            slotInfoStyle.normal.textColor = t.textSecondary;
            // 습격 창 — 머리말과 도망 실패 줄은 경고색(헤더·활성 탭에 쓰는 코랄 토큰)이다.
            ambushTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            ambushTitleStyle.normal.textColor = t.accentCoral;
            ambushFailStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            ambushFailStyle.normal.textColor = t.accentCoral;
        }

        /// <summary>데스크톱에서만 단축키를 작게 붙인다 — 모바일엔 키보드가 없다.</summary>
        private static string WithKey(string label, string key)
        {
            return UIScale.IsMobileLayout ? label : $"{label}  <size=22>[{key}]</size>";
        }

        /// <summary>모달 공통 바탕 — 가벼운 딤 + 등급색이 섞인 테두리 카드 + 윗줄 액센트.</summary>
        private static void DrawPanel(Rect panel, Color accent)
        {
            UITheme t = UITheme.Instance;
            UISurface.Dim(0.35f);
            UISurface.Card(panel, t.surfaceCard, Color.Lerp(t.surfaceBorder, accent, 0.55f));
            UISurface.Flat(new Rect(panel.x + UITheme.Radius.Card, panel.y + 3f,
                panel.width - UITheme.Radius.Card * 2f, 4f), accent);
        }

        private void DrawChoice()
        {
            if (targetInsect == null || targetInsect.Data == null) return;

            InitStyles();
            UITheme t = UITheme.Instance;
            bool isRaid = IsRaidTarget();
            bool mobile = UIScale.IsMobileLayout;
            // 레벨 차 경고 — 포획 공식(TrainerLevelGap)과 같은 캐릭터 레벨·같은 판정으로 띄운다.
            // 레이드는 승리 시 확정 포획이라, 수문장은 애초에 포획 대상이 아니라 제외한다.
            int trainerLevel = captureController != null ? captureController.TrainerLevel : 1;
            bool levelWarn = !isRaid && !targetInsect.IsGuardian
                             && TrainerLevelGap.IsCaptureRestricted(trainerLevel, targetInsect.Level);
            const float levelWarnH = 44f;
            // 포획 버튼 아래의 채집망 한 줄(NetRowH)만큼 일반 창이 길다 — 레이드 창에는 그 줄이 없다.
            Rect panel = UISafeLayout.CenteredPanel(820f,
                isRaid ? 730f : 620f + NetRowH + 8f + (levelWarn ? levelWarnH : 0f));
            float panelW = panel.width;
            float panelH = panel.height;
            float px = panel.x;
            float py = panel.y;

            Color rarityCol = t.GetInsectRarityColor(targetInsect.Data.rarity);
            DrawPanel(panel, rarityCol);
            GUI.color = Color.white;

            string titleText = isRaid ? "레이드 보스 발견!" : "어떻게 포획할까요?";
            GUI.Label(new Rect(px, py + 20f, panelW, 50f), titleText, titleStyle);

            // 미리보기 — 예전엔 96px 썸네일이라 무엇을 만났는지 알아보기 어려웠다.
            Rect frame = new Rect(px + panelW / 2f - 100f, py + 80f, 200f, 200f);
            UISurface.Rounded(frame, Color.Lerp(t.surfaceBase, rarityCol, 0.14f));
            InsectVisual.Draw(frame.center.x, frame.center.y, 188f, targetInsect.Data, targetInsect.IsShiny, 1f);

            nameStyle.normal.textColor = rarityCol;
            // 「지워진 개체」면 본명 대신 "???" — 월드에서 실루엣·"???"로 감춰 놓고 이 창이
            // 바로 알려주면 연출이 무의미해진다(`InsectEntity.DisplayNameForPlayer`가 단일 출처).
            UIHelper.LabelFit(new Rect(px + 20f, py + 288f, panelW - 40f, 50f),
                $"{targetInsect.DisplayNameForPlayer}  Lv.{targetInsect.Level}", nameStyle);

            // 배지 — 등급(한글) · 색다름 · 수문장. 가운데 정렬로 늘어놓는다.
            DrawChoiceChips(px + panelW / 2f, py + 344f, rarityCol);

            if (isRaid)
            {
                GUI.Label(new Rect(px, py + 392f, panelW, 36f), "레이드로만 포획할 수 있어요", raidHintStyle);
                GUI.Label(new Rect(px, py + 428f, panelW, 32f), "5마리 팀 전체가 힘을 합쳐 보스에 도전합니다", raidDescStyle);

                float raidBtnW = 380f;
                float raidBtnH = 88f;
                float raidBtnY = py + 474f;
                Rect raidBtn = new Rect(px + (panelW - raidBtnW) / 2f, raidBtnY, raidBtnW, raidBtnH);

                bool hasFullTeam = teamManager != null && teamManager.FilledSlots >= 5;
                // 전원 기절이면 시작해도 아무도 행동할 수 없어 레이드가 잠긴다(StartRaidBattle 주석 참조).
                int readyCount = CountBattleReadyTeamMembers();
                bool canRaid = hasFullTeam && readyCount > 0;
                GUI.enabled = canRaid;
                if (UISurface.Button(raidBtn, WithKey("레이드 시작", "R"), canRaid ? t.btnDanger : t.btnDisabled, buttonStyle))
                {
                    StartRaidBattle();
                }
                GUI.enabled = true;

                // 안내는 한 줄만 — 편성 부족 > 전원 기절 > 일부 기절 순으로 더 급한 것을 보여준다.
                string raidNotice = null;
                Color raidNoticeCol = t.accentCoral;
                if (!hasFullTeam)
                {
                    int filled = teamManager != null ? teamManager.FilledSlots : 0;
                    raidNotice = mobile
                        ? $"팀 편성 필요 ({filled}/5) · 메뉴에서 배틀팀 편성"
                        : $"팀 편성 필요 ({filled}/5) · T키로 편성";
                }
                else if (readyCount <= 0)
                {
                    raidNotice = "팀 전원이 기절했습니다 — 병원에서 치료 후 도전하세요";
                }
                else if (readyCount < BattleTeamManager.MaxSlots)
                {
                    raidNotice = $"기절 {BattleTeamManager.MaxSlots - readyCount}마리 · 전투 가능 {readyCount}/{BattleTeamManager.MaxSlots}";
                    raidNoticeCol = t.accentAmber;
                }

                if (raidNotice != null)
                {
                    hintStyle.normal.textColor = raidNoticeCol;
                    // 버튼 폭이 아니라 패널 폭을 쓴다 — "팀 전원이 기절했습니다 …" 같은 한 줄은
                    // 버튼 폭에 못 들어가고, 한국어 폰트가 커지는 모바일에서 특히 잘렸다.
                    // 2줄 높이를 주고 그래도 넘치면 LabelFit이 글자를 줄여 맞춘다(ui-layout.md).
                    UIHelper.LabelFit(new Rect(px + 24f, raidBtnY + raidBtnH + 8f, panelW - 48f, 60f),
                        raidNotice, hintStyle);
                }
            }
            else
            {
                float btnY = py + 400f;
                if (levelWarn)
                {
                    // 버튼 위 한 줄 — 패널을 그만큼 키웠으므로 버튼만 내리면 아래(힌트·취소)가 겹치지 않는다.
                    hintStyle.normal.textColor = t.accentCoral;
                    int gapLv = TrainerLevelGap.Gap(trainerLevel, targetInsect.Level);
                    UIHelper.LabelFit(new Rect(px + 24f, btnY - 6f, panelW - 48f, 40f),
                        $"캐릭터보다 {gapLv}레벨 높아 포획이 매우 어려워요", hintStyle);
                    btnY += levelWarnH;
                }
                float btnW = 330f;
                float btnH = 88f;
                float gap = 24f;
                float leftX = px + panelW / 2f - btnW - gap / 2f;

                // **수문장은 포획 대상이 아니다.** 잡아 버리면 개체가 사라지는데 격파 판정은
                // 그 개체를 이겼을 때만 서므로, 그 리전이 영구히 안 열린다(진행 정지).
                bool isGuardian = targetInsect.IsGuardian;
                bool hasAnyNet = HasAnyCaptureItem();
                bool canCapture = hasAnyNet && !isGuardian;
                GUI.enabled = canCapture;
                if (UISurface.Button(new Rect(leftX, btnY, btnW, btnH), WithKey("미니게임 포획", "E"),
                        canCapture ? t.btnPrimary : t.btnDisabled, buttonStyle))
                {
                    BeginQuickCapture();
                }
                GUI.enabled = true;

                if (!canCapture)
                {
                    hintStyle.normal.textColor = t.accentCoral;
                    // 고정 상자에 리터럴을 그리므로 LabelFit — 수문장 문구가 더 길어 잘릴 수 있다.
                    UIHelper.LabelFit(new Rect(leftX, btnY + btnH + 6f, btnW, 32f),
                        isGuardian ? "수문장은 쓰러뜨려야 한다" : "포획 아이템 없음!", hintStyle);
                }
                else
                {
                    // 이번에 쓸 채집망 — 누르면 고르는 창이 뜬다. 예전엔 포획할 때마다 그 창을 거쳤다.
                    CaptureItemData quick = ResolveQuickNet();
                    if (quick != null)
                    {
                        Rect netRow = new Rect(leftX, btnY + btnH + 8f, btnW, NetRowH);
                        if (UISurface.Button(netRow, string.Empty, t.surfaceRaised, netRowStyle))
                            showItemSelect = true;
                        Rect swatch = new Rect(netRow.x + 14f, netRow.y + (NetRowH - 24f) * 0.5f, 24f, 24f);
                        UISurface.Rounded(swatch, quick.themeColor, UITheme.Radius.Chip);
                        UIHelper.LabelFit(new Rect(swatch.xMax + 8f, netRow.y, netRow.xMax - swatch.xMax - 20f, NetRowH),
                            NetRowLabel(quick), netRowStyle);
                    }
                }

                bool hasTeam = teamManager != null && teamManager.HasAnyInsect();
                float rightX = leftX + btnW + gap;
                GUI.enabled = hasTeam;
                if (UISurface.Button(new Rect(rightX, btnY, btnW, btnH), WithKey("배틀", "B"),
                        hasTeam ? t.btnDanger : t.btnDisabled, buttonStyle))
                {
                    showTeamSelect = true;
                }
                GUI.enabled = true;

                if (!hasTeam)
                {
                    hintStyle.normal.textColor = t.textMuted;
                    UIHelper.LabelFit(new Rect(rightX, btnY + btnH + 6f, btnW, 32f),
                        mobile ? "메뉴에서 배틀팀 편성" : "T키로 팀 편성", hintStyle);
                }
            }

            float cancelW = mobile ? 220f : 200f;
            float cancelH = mobile ? 64f : 56f;
            if (UISurface.Button(new Rect(px + panelW / 2f - cancelW / 2f, py + panelH - cancelH - 18f, cancelW, cancelH),
                    WithKey("취소", "ESC"), t.surfaceRaised, cancelStyle))
                Hide();
        }

        /// <summary>등급(한글)·색다름·수문장 배지를 가운데 정렬로 한 줄에 놓는다.</summary>
        private void DrawChoiceChips(float centerX, float y, Color rarityCol)
        {
            UITheme t = UITheme.Instance;
            const float h = 36f;
            const float gap = 10f;
            const float rarityW = 96f, shinyW = 150f, guardianW = 104f;
            bool shiny = targetInsect.IsShiny;
            bool guardian = targetInsect.IsGuardian;
            float total = rarityW + (shiny ? gap + shinyW : 0f) + (guardian ? gap + guardianW : 0f);
            float x = centerX - total / 2f;

            UISurface.Chip(new Rect(x, y, rarityW, h), targetInsect.Data.rarity.Korean(),
                Color.Lerp(rarityCol, Color.black, 0.35f), t.textPrimary);
            x += rarityW + gap;
            if (shiny)
            {
                UISurface.Chip(new Rect(x, y, shinyW, h), "★ 색다른 개체", t.accentAmber, t.surfaceBase);
                x += shinyW + gap;
            }
            if (guardian)
                UISurface.Chip(new Rect(x, y, guardianW, h), "수문장", t.accentCoral, t.textPrimary);
        }

        // ── 습격 창 ─────────────────────────────────────────────────────────
        // 깨어 있는 습격형 곤충이 닿으면 CaptureInputController가 연다(규칙은 AmbushRules). 고를 것은 [싸우기]와 [도망치기]뿐이다.
        //  · 포획 버튼이 없다 — 덤벼드는 곤충을 미니게임으로 잡는 건 이상하다. 이기면 전투의 포획 기회가 있다.
        //  · 그냥 닫는 길이 없다 — ESC는 도망치기다(RequestClose). 닫기가 도망 판정을 우회하면 습격이 무의미해진다.
        //  · 도망은 전투의 도주 확률(BattleEscapeRules — 선두 출전 곤충 레벨 대 상대 레벨)을 한 번만 굴리고, 실패하면 싸운다.
        //  · 싸우기는 기존 [B] 배틀 경로(출전할 곤충 고르기)다. 영웅·전설은 기존처럼 레이드로 맞선다(IsRaidRarity).

        private bool ambushMode;
        private string ambushReason;
        private string ambushNameLabel;
        private string ambushFightLabel;
        private string ambushEscapeLabel;
        private string ambushChanceLabel;
        private float ambushOpenedAt;
        private bool ambushEscapeTried;
        private bool ambushEscapeFailed;
        private bool ambushAutoFought;
        private float ambushFailAt;
        private int ambushLeadLevel = 1;
        private int ambushActionFrame = -1;
        private const float AmbushPanelH = 640f;

        /// <summary>습격 창이 열려 있는가(팀 고르기로 넘어간 동안도 참이다).</summary>
        public bool IsAmbushOpen => isOpen && ambushMode;

        /// <summary>
        /// 「습격!」 창을 연다. <b>싸울 수 없으면 열지 않는다</b>(false) — 이 창엔 그냥 닫는 길이 없어서 싸울 수 없는 창은 갇힌 창이 된다
        /// (판정 쪽 <c>AmbushRules.CanFight</c>도 같은 조건으로 습격 자체를 막는다). 이미 다른 창이 열려 있어도 false.
        /// </summary>
        /// <param name="reason">"왜 덤벼들었나" 한 줄(<c>AmbushRules.ReasonLine</c>). 비면 기본 문구.</param>
        public bool ShowAmbush(InsectEntity target, string reason)
        {
            if (isOpen || target == null || target.Data == null) return false;
            if (!CanFight(target)) return false;

            ShowChoice(target);
            if (!isOpen) return false;
            ambushMode = true;
            ambushReason = string.IsNullOrEmpty(reason) ? AmbushRules.FallbackReason : reason;
            ambushOpenedAt = Time.unscaledTime;
            ambushEscapeTried = false;
            ambushEscapeFailed = false;
            ambushAutoFought = false;
            ambushFailAt = 0f;
            ambushActionFrame = -1;
            ambushLeadLevel = LeadFighterLevel();
            // 문구는 창을 열 때 한 번 만든다 — OnGUI 패스마다 이어 붙이지 않는다.
            ambushNameLabel = $"{target.DisplayNameForPlayer}  Lv.{target.Level}";
            ambushFightLabel = WithKey(IsRaidTarget() ? "레이드로 맞서기" : "싸우기", "B");
            ambushEscapeLabel = WithKey("도망치기", "F/ESC");
            int percent = Mathf.RoundToInt(BattleEscapeRules.Chance(ambushLeadLevel, target.Level) * 100f);
            ambushChanceLabel = $"도망칠 확률 {percent}%";
            return true;
        }

        /// <summary>
        /// 이 곤충과 지금 싸울 수 있는가 — 1v1이면 기절 안 한 출전 곤충, 레이드 대상이면 5마리 팀(그중 하나라도 싸울 수 있게).
        /// 습격 판정(<c>CaptureInputController</c>)과 같은 술어(<c>AmbushRules.CanFight</c>)다.
        /// </summary>
        public bool CanFight(InsectEntity target)
        {
            if (target == null || target.Data == null) return false;
            GetFightReadiness(out int filled, out int ready);
            return AmbushRules.CanFight(IsRaidRarity(target.Data.rarity), filled, ready);
        }

        /// <summary>배틀팀 상태 — 채운 칸 수와 그중 지금 싸울 수 있는(기절 안 한) 곤충 수. 습격 판정이 프레임마다 한 번 읽는다.</summary>
        public void GetFightReadiness(out int filledSlots, out int readySlots)
        {
            filledSlots = teamManager != null ? teamManager.FilledSlots : 0;
            readySlots = CountBattleReadyTeamMembers();
        }

        /// <summary>창이 뜬 직후에는 입력을 받지 않는다 — 걷던 손가락·누르던 키가 그대로 버튼을 누르지 않게.</summary>
        private bool AmbushInputReady => Time.unscaledTime - ambushOpenedAt >= AmbushRules.InputDelaySeconds;

        /// <summary>
        /// 한 프레임에 한 동작만. 같은 누름이 여러 길로 온다(이 창의 Update·OnGUI, <c>CaptureInputController</c>, <c>PlayerMovement</c>의 모달 ESC) —
        /// 안 막으면 팀 고르기에서 ESC 한 번이 ‹ 뒤로와 도망치기를 함께 일으킨다.
        /// </summary>
        private bool ConsumeAmbushAction()
        {
            if (ambushActionFrame == Time.frameCount) return false;
            ambushActionFrame = Time.frameCount;
            return true;
        }

        private void HandleAmbushInput(KeyCode key)
        {
            if (key == KeyCode.Escape || key == KeyCode.Backspace)
            {
                HandleAmbushEscapeKey();
                return;
            }
            if (showTeamSelect) return;   // 출전 곤충은 버튼으로 고른다(보통 창과 같다)

            bool isRaid = IsRaidTarget();
            if (key == KeyCode.B || key == KeyCode.Alpha1 || (isRaid && key == KeyCode.R))
            {
                if (AmbushInputReady && ConsumeAmbushAction()) BeginAmbushFight();
            }
            else if (key == KeyCode.F || key == KeyCode.Alpha2)
            {
                if (AmbushInputReady && ConsumeAmbushAction()) TryAmbushEscape();
            }
        }

        /// <summary>ESC — 팀 고르기 중이면 습격 창으로 돌아가고, 아니면 도망치기다. 그냥 닫는 길은 없다.</summary>
        private void HandleAmbushEscapeKey()
        {
            if (!ConsumeAmbushAction()) return;
            if (showTeamSelect)
            {
                showTeamSelect = false;
                return;
            }
            if (!AmbushInputReady) return;
            TryAmbushEscape();
        }

        /// <summary>
        /// 도망치기 — 한 번만 굴린다. 성공하면 창을 닫고(교전이 풀리며 그 몸에 쿨다운이 걸린다) 곤충은 물러난다(놓침 도주와 같은 길 —
        /// 스포너가 같은 개체를 눈 밖 다른 자리로 옮긴다). 실패하면 「도망치지 못했다!」 뒤 싸움으로 넘어간다(Update).
        /// </summary>
        private void TryAmbushEscape()
        {
            if (!ambushMode || ambushEscapeTried || targetInsect == null) return;
            ambushEscapeTried = true;
            InsectEntity target = targetInsect;
            // 안전망 — 열 때는 싸울 수 있었는데 그 수단이 사라졌다면, 갇힌 창이 되지 않게 도망은 늘 성공한다.
            bool escaped = !CanFight(target)
                           || Random.value < BattleEscapeRules.Chance(ambushLeadLevel, target.Level);
            if (escaped)
            {
                Hide();
                target.ScareAway();
                return;
            }
            ambushEscapeFailed = true;
            ambushFailAt = Time.unscaledTime;
        }

        /// <summary>싸우기 — 영웅·전설은 기존 레이드 진입, 그 밖에는 기존 [B] 배틀 경로(출전할 곤충 고르기)를 그대로 탄다.</summary>
        private void BeginAmbushFight()
        {
            if (!ambushMode || targetInsect == null) return;
            if (IsRaidTarget()) StartRaidBattle();
            else showTeamSelect = true;
        }

        /// <summary>선두 출전 곤충의 레벨 — 배틀팀 앞 칸부터 기절하지 않은 첫 곤충. 도망 확률이 이 레벨과 상대 레벨을 견준다.</summary>
        private int LeadFighterLevel()
        {
            if (teamManager == null || collection == null) return 1;
            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                string instanceId = teamManager.GetSlot(i);
                if (string.IsNullOrEmpty(instanceId)) continue;
                PlayerInsectData pid = collection.GetByInstanceId(instanceId);
                if (pid != null && !pid.IsFainted) return Mathf.Max(1, pid.level);
            }
            return 1;
        }

        private void DrawAmbush()
        {
            if (targetInsect == null || targetInsect.Data == null) return;

            InitStyles();
            UITheme t = UITheme.Instance;
            bool isRaid = IsRaidTarget();
            Rect panel = UISafeLayout.CenteredPanel(820f, AmbushPanelH);
            float panelW = panel.width;
            float px = panel.x;
            float py = panel.y;

            Color rarityCol = t.GetInsectRarityColor(targetInsect.Data.rarity);
            DrawPanel(panel, t.accentCoral);
            GUI.color = Color.white;

            UIHelper.LabelFit(new Rect(px + 24f, py + 14f, panelW - 48f, 60f), "습격!", ambushTitleStyle);
            UIHelper.LabelFit(new Rect(px + 24f, py + 74f, panelW - 48f, 36f), ambushReason, subStyle);

            // 미리보기 — 포획 선택 창과 같은 자리·크기. 바탕만 경고색이 섞인다.
            Rect frame = new Rect(px + panelW / 2f - 100f, py + 118f, 200f, 200f);
            UISurface.Rounded(frame, Color.Lerp(t.surfaceBase, t.accentCoral, 0.16f));
            InsectVisual.Draw(frame.center.x, frame.center.y, 188f, targetInsect.Data, targetInsect.IsShiny, 1f);

            nameStyle.normal.textColor = rarityCol;
            UIHelper.LabelFit(new Rect(px + 20f, py + 326f, panelW - 40f, 50f), ambushNameLabel, nameStyle);
            DrawChoiceChips(px + panelW / 2f, py + 382f, rarityCol);

            // 버튼 둘 — 크기는 포획 선택 창(330×88)과 같다. 창이 뜬 직후 잠깐은 눌리지 않는다(InputDelaySeconds).
            const float btnW = 330f;
            const float btnH = 88f;
            const float gap = 24f;
            float btnY = py + 440f;
            float leftX = px + panelW / 2f - btnW - gap / 2f;
            float rightX = leftX + btnW + gap;
            bool ready = AmbushInputReady;

            GUI.enabled = ready;
            if (UISurface.Button(new Rect(leftX, btnY, btnW, btnH), ambushFightLabel,
                    ready ? t.btnDanger : t.btnDisabled, buttonStyle) && ConsumeAmbushAction())
                BeginAmbushFight();

            bool canEscape = ready && !ambushEscapeTried;
            GUI.enabled = canEscape;
            if (UISurface.Button(new Rect(rightX, btnY, btnW, btnH), ambushEscapeLabel,
                    canEscape ? t.btnSecondary : t.btnDisabled, buttonStyle) && ConsumeAmbushAction())
                TryAmbushEscape();
            GUI.enabled = true;
            if (!isOpen || showTeamSelect) return;   // 도망에 성공해 닫혔거나 출전 곤충 고르기로 넘어갔다

            hintStyle.normal.textColor = t.textMuted;
            UIHelper.LabelFit(new Rect(leftX, btnY + btnH + 6f, btnW, 32f),
                isRaid ? "5마리 팀 레이드" : "출전할 곤충을 고른다", hintStyle);
            hintStyle.normal.textColor = ambushEscapeTried ? t.accentCoral : t.textSecondary;
            UIHelper.LabelFit(new Rect(rightX, btnY + btnH + 6f, btnW, 32f),
                ambushEscapeTried ? "도망 실패" : ambushChanceLabel, hintStyle);

            if (ambushEscapeFailed)
                UIHelper.LabelFit(new Rect(px + 24f, btnY + btnH + 46f, panelW - 48f, 48f), "도망치지 못했다!", ambushFailStyle);
        }

        private void DrawItemSelect()
        {
            if (captureItems == null || captureItems.Length == 0) { showItemSelect = false; return; }

            InitStyles();
            UITheme t = UITheme.Instance;
            bool mobile = UIScale.IsMobileLayout;
            const float rowH = 148f;
            const float rowGap = 12f;
            float backH = mobile ? 64f : 56f;
            // 아이템 수에 비례해 자라는 높이 — 안전 영역을 넘으면 clamp된다.
            Rect panel = UISafeLayout.CenteredPanel(820f,
                96f + captureItems.Length * (rowH + rowGap) + backH + 28f);
            float panelW = panel.width;
            float panelH = panel.height;
            float px = panel.x;
            float py = panel.y;

            DrawPanel(panel, t.accentMint);
            GUI.color = Color.white;
            GUI.Label(new Rect(px, py + 18f, panelW, 50f), "채집망 선택", titleStyle);

            float startY = py + 84f;
            for (int i = 0; i < captureItems.Length; i++)
            {
                CaptureItemData item = captureItems[i];
                int count = itemInventory != null ? itemInventory.GetCount(item.itemId) : 0;
                bool hasItem = count > 0;
                Rect row = new Rect(px + 24f, startY + i * (rowH + rowGap), panelW - 48f, rowH);

                UISurface.Card(row, hasItem ? t.surfaceRaised : t.surfaceBase, t.surfaceBorder);
                // 그물 색 견본 — 없는 아이템은 흐리게.
                Rect swatch = new Rect(row.x + 20f, row.y + (rowH - 64f) / 2f, 64f, 64f);
                UISurface.Rounded(swatch, hasItem ? item.themeColor : Color.Lerp(item.themeColor, t.surfaceBase, 0.7f));
                UISurface.Rounded(new Rect(swatch.x + 14f, swatch.y + 14f, 36f, 36f), t.surfaceShadow);

                float textX = swatch.xMax + 20f;
                float textW = row.xMax - 200f - textX;
                itemNameStyle.normal.textColor = hasItem ? t.textPrimary : t.textMuted;
                string name = mobile ? item.displayName : $"{item.displayName}  <size=20>[{i + 1}]</size>";
                UIHelper.LabelFit(new Rect(textX, row.y + 14f, textW, 42f), name, itemNameStyle);
                UIHelper.LabelFit(new Rect(textX, row.y + 58f, textW, 32f), item.description, itemDescStyle);

                string difficulty;
                Color diffCol;
                if (item.speedMultiplier <= 0.6f) { difficulty = "매우 쉬움"; diffCol = t.accentMint; }
                else if (item.speedMultiplier <= 0.8f) { difficulty = "쉬움"; diffCol = t.accentAmber; }
                else { difficulty = "보통"; diffCol = t.surfaceBorder; }
                // "보통"은 밝은 액센트가 아니라 테두리색 바탕이라 글자를 밝게 둔다(어두운 글자면 묻혔다).
                Color diffText = diffCol == t.surfaceBorder ? t.textPrimary : t.surfaceBase;
                UISurface.Chip(new Rect(textX, row.y + 98f, 132f, 34f), difficulty,
                    hasItem ? diffCol : t.surfaceRaised, hasItem ? diffText : t.textMuted);

                countStyle.normal.textColor = hasItem ? t.accentAmber : t.textMuted;
                UIHelper.LabelFit(new Rect(row.xMax - 180f, row.y + 12f, 160f, 40f), $"×{count}", countStyle);

                GUI.enabled = hasItem;
                if (UISurface.Button(new Rect(row.xMax - 170f, row.y + 62f, 150f, 68f), "사용",
                        hasItem ? t.btnPrimary : t.btnDisabled, buttonStyle))
                {
                    StartCaptureWith(item);
                }
                GUI.enabled = true;
            }

            if (UISurface.Button(new Rect(px + panelW / 2f - 110f, py + panelH - backH - 16f, 220f, backH),
                    "‹ 뒤로", t.surfaceRaised, cancelStyle))
                showItemSelect = false;
        }

        private void TryItemSelectByKey()
        {
            if (captureItems == null || itemInventory == null) return;
            int keyIndex = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1)) keyIndex = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) keyIndex = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) keyIndex = 2;
            else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                for (int i = 0; i < captureItems.Length; i++)
                {
                    if (itemInventory.GetCount(captureItems[i].itemId) > 0) { keyIndex = i; break; }
                }
            }
            if (keyIndex < 0 || keyIndex >= captureItems.Length) return;

            StartCaptureWith(captureItems[keyIndex]);
        }

        // ── 채집망 바로 쓰기 ──
        // 포획은 수백 번 반복하는 동작인데 매번 「채집망 선택」 창을 거쳤다(선택 → 그물 → 미니게임 → 결과).
        // 지난번에 쓴 채집망을 기억해 버튼 하나로 미니게임까지 간다. 바꾸고 싶으면 버튼 아래 줄을 누른다.

        private const float NetRowH = 52f;

        private string lastNetItemId;
        private string lastNetPrefsKey;
        // 채집망 줄 문구 캐시 — OnGUI 패스마다 이어 붙이지 않는다(종류·개수가 바뀔 때만 다시 만든다).
        private string netRowLabel;
        private string netRowLabelItemId;
        private int netRowLabelCount = -1;
        private System.Func<string, int> netCountProbe;

        // 계정마다 따로 기억한다. 로그인이 이 화면보다 늦게 끝나므로 키가 바뀔 때마다 다시 읽는다.
        private void EnsureLastNetLoaded()
        {
            string key = AuthManager.ScopedKey(GameConstants.PrefsKeys.LastCaptureNet);
            if (key == lastNetPrefsKey) return;
            lastNetPrefsKey = key;
            lastNetItemId = PlayerPrefs.GetString(key, string.Empty);
        }

        /// <summary>
        /// 이번 포획에 쓸 채집망 — 지난번에 쓴 것이 남아 있으면 그것, 아니면 가진 것 중 맨 앞.
        /// 목록은 흔한 것부터 적혀 있어(기본 → 은빛 → 황금), 귀한 채집망을 말없이 써 버리지 않는다.
        /// </summary>
        private CaptureItemData ResolveQuickNet()
        {
            if (captureItems == null || itemInventory == null) return null;
            // 대리자는 한 번만 묶는다 — 이 함수는 OnGUI 패스마다 불린다.
            if (netCountProbe == null) netCountProbe = itemInventory.GetCount;
            return CaptureNetChoice.Resolve(captureItems, lastNetItemId, netCountProbe);
        }

        private void BeginQuickCapture()
        {
            CaptureItemData net = ResolveQuickNet();
            if (net != null) StartCaptureWith(net);
        }

        private string NetRowLabel(CaptureItemData net)
        {
            int count = itemInventory != null ? itemInventory.GetCount(net.itemId) : 0;
            if (netRowLabel == null || netRowLabelItemId != net.itemId || netRowLabelCount != count)
            {
                netRowLabelItemId = net.itemId;
                netRowLabelCount = count;
                netRowLabel = $"{net.displayName} ×{count}  ·  바꾸기";
            }
            return netRowLabel;
        }

        /// <summary>채집망 하나를 쓰고 미니게임을 연다 — 버튼·숫자 키·바로 쓰기 세 경로의 단일 진입점.</summary>
        private void StartCaptureWith(CaptureItemData item)
        {
            if (item == null || itemInventory == null) return;
            if (itemInventory.GetCount(item.itemId) <= 0) return;
            if (!itemInventory.UseItem(item.itemId, 1)) return;

            EnsureLastNetLoaded();
            if (lastNetItemId != item.itemId)
            {
                lastNetItemId = item.itemId;
                PlayerPrefs.SetString(lastNetPrefsKey, lastNetItemId);
                PlayerPrefs.Save();
            }

            InsectEntity savedTarget = targetInsect;
            Hide();
            if (minigame != null && savedTarget != null)
                minigame.StartMinigame(savedTarget,
                    item.speedMultiplier, item.zoneSizeMultiplier,
                    item.timeLimitMultiplier, item.captureBonus);
        }

        private bool HasAnyCaptureItem()
        {
            if (captureItems == null || itemInventory == null) return false;
            foreach (var item in captureItems)
                if (itemInventory.GetCount(item.itemId) > 0) return true;
            return false;
        }

        private void DrawTeamSelect()
        {
            InitStyles();
            UITheme t = UITheme.Instance;
            bool mobile = UIScale.IsMobileLayout;
            Rect panel = UISafeLayout.CenteredPanel(880f, mobile ? 1100f : 860f);
            float panelW = panel.width;
            float panelH = panel.height;
            float px = panel.x;
            float py = panel.y;

            DrawPanel(panel, t.accentAmber);
            GUI.color = Color.white;
            UIHelper.LabelFit(new Rect(px + 24f, py + 18f, panelW - 48f, 48f), "출전할 곤충을 고르세요", titleStyle);
            UIHelper.LabelFit(new Rect(px + 24f, py + 66f, panelW - 48f, 34f),
                $"vs {targetInsect.DisplayNameForPlayer} Lv.{targetInsect.Level}", subStyle);

            float slotY = py + 112f;
            const float slotGap = 8f;
            float backH = mobile ? 64f : 56f;
            // 패널이 안전 영역에 맞춰 줄면 슬롯도 줄여 '뒤로' 버튼과 겹치지 않게 한다.
            float slotAvail = panelH - (slotY - py) - backH - 32f;
            float slotH = Mathf.Clamp(
                (slotAvail - (BattleTeamManager.MaxSlots - 1) * slotGap) / BattleTeamManager.MaxSlots,
                UIScale.MinTouchHeight,
                mobile ? 170f : 128f);

            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                string instanceId = teamManager != null ? teamManager.GetSlot(i) : null;
                DrawTeamSlotChoice(px + 24f, slotY + i * (slotH + slotGap), panelW - 48f, slotH, i, instanceId);
            }

            if (UISurface.Button(new Rect(px + panelW / 2f - 110f, py + panelH - backH - 16f, 220f, backH),
                    "‹ 뒤로", t.surfaceRaised, cancelStyle))
                showTeamSelect = false;
        }

        private void DrawTeamSlotChoice(float x, float y, float w, float h, int index, string instanceId)
        {
            UITheme t = UITheme.Instance;
            bool empty = string.IsNullOrEmpty(instanceId);
            Rect card = new Rect(x, y, w, h);

            UISurface.Card(card, empty ? t.surfaceBase : t.surfaceRaised, t.surfaceBorder);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f, y, 48f, h), $"{index + 1}", slotNumStyle);

            if (empty)
            {
                slotInfoStyle.normal.textColor = t.textMuted;
                GUI.Label(new Rect(x + 64f, y, w - 80f, h), "비어 있음", slotInfoStyle);
                slotInfoStyle.normal.textColor = t.textSecondary;
                return;
            }

            PlayerInsectData pid = collection != null ? collection.GetByInstanceId(instanceId) : null;
            InsectData data = pid != null && collection != null ? collection.GetInsectData(pid.insectId) : null;
            if (data == null) return;

            Color rarityCol = t.GetInsectRarityColor(data.rarity);
            // 6px 레일 — 각진 채로 두고 세로를 카드 반경만큼 물린다(ui-layout.md).
            UISurface.Flat(new Rect(x + 3f, y + 3f + UITheme.Radius.Card, 6f,
                Mathf.Max(4f, h - 6f - UITheme.Radius.Card * 2f)), rarityCol);

            float thumb = Mathf.Min(h - 16f, 104f);
            InsectVisual.Draw(x + 60f + thumb / 2f, y + h / 2f, thumb, data, pid.isShiny, pid.IsFainted ? 0.45f : 1f);

            float textX = x + 72f + thumb;
            float textW = w - (textX - x) - 176f;
            int lv = pid.level;
            int cp = PlayerInsectCombatPower.Calculate(data, pid);
            slotNameStyle.normal.textColor = rarityCol;
            UIHelper.LabelFit(new Rect(textX, y + h * 0.5f - 44f, textW, 40f), GetOwnedDisplayName(pid, data), slotNameStyle);
            GUI.Label(new Rect(textX, y + h * 0.5f - 4f, textW, 30f), $"Lv.{lv}  ·  {data.rarity.Korean()}  ·  CP {cp}", slotInfoStyle);

            // HP 막대 — 출전 전에 누가 지쳤는지 보인다(예전엔 기절만 버튼으로 알렸다).
            int maxHp = pid.GetTotalHp(data.baseHp);
            int curHp = pid.currentHp < 0 ? maxHp : pid.currentHp;
            float ratio = maxHp > 0 ? (float)curHp / maxHp : 0f;
            UISurface.Meter(new Rect(textX, y + h * 0.5f + 30f, Mathf.Min(textW, 280f), 10f), ratio, t.GetHpColor(ratio));

            // 기절(0 HP) 곤충은 출전 불가 — 병원 치료 전까지 즉사 반복 방지(지속 HP 도입에 따른 가드).
            bool fainted = pid.IsFainted;
            float btnH = Mathf.Min(h - 20f, 68f);
            Rect btn = new Rect(x + w - 164f, y + (h - btnH) / 2f, 144f, btnH);
            GUI.enabled = !fainted;
            if (UISurface.Button(btn, fainted ? "기절" : "출격!", fainted ? t.btnDisabled : t.btnDanger, buttonStyle)
                && !fainted)
                StartBattleCapture(pid, data, lv);
            GUI.enabled = true;
        }

        private void StartBattleCapture(PlayerInsectData playerPid, InsectData playerInsect, int playerLevel)
        {
            InsectEntity savedTarget = targetInsect;
            Hide();
            if (battleController == null || savedTarget == null) return;

            InsectSkill[] equippedSkills = null;
            if (playerPid != null && collection != null)
                equippedSkills = collection.GetEquippedSkills(playerPid);

            battleController.StartBattle(playerInsect, playerLevel, savedTarget, equippedSkills: equippedSkills, playerPid: playerPid);
        }

        /// <summary>
        /// 배틀팀 5슬롯 중 <b>지금 싸울 수 있는</b>(기절이 아닌) 곤충 수. 1v1의 출격 가드
        /// (<see cref="DrawTeamSlotChoice"/>의 `pid.IsFainted`)와 같은 기준을 레이드 진입에 적용한다.
        /// </summary>
        private int CountBattleReadyTeamMembers()
        {
            if (teamManager == null || collection == null) return 0;

            int ready = 0;
            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                string instanceId = teamManager.GetSlot(i);
                if (string.IsNullOrEmpty(instanceId)) continue;
                PlayerInsectData pid = collection.GetByInstanceId(instanceId);
                if (pid != null && !pid.IsFainted) ready++;
            }
            return ready;
        }

        private void StartRaidBattle()
        {
            // 전원 기절이면 시작하지 않는다 — 레이드는 팀이 행동해야 보스 턴이 오고(ResolveBossResponse)
            // 패배 판정도 그 안에만 있어서, 살아 있는 슬롯이 하나도 없으면 ActiveSlot이 -1로 남아
            // 어떤 스킬도 못 쓰고 종료도 안 되는 **영구 정지**가 된다.
            // Hide() **앞에서** 막는다 — 뒤에 두면 패널이 닫혀 안내 문구까지 사라진다.
            if (CountBattleReadyTeamMembers() <= 0) return;

            InsectEntity savedTarget = targetInsect;
            Hide();
            if (raidController == null || savedTarget == null || teamManager == null || collection == null) return;

            int count = 0;
            InsectData[] teamInsects = new InsectData[BattleTeamManager.MaxSlots];
            int[] teamLevels = new int[BattleTeamManager.MaxSlots];
            PlayerInsectData[] teamPids = new PlayerInsectData[BattleTeamManager.MaxSlots];
            InsectSkill[][] teamSkills = new InsectSkill[BattleTeamManager.MaxSlots][];

            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                string instanceId = teamManager.GetSlot(i);
                if (string.IsNullOrEmpty(instanceId)) continue;

                PlayerInsectData pid = collection.GetByInstanceId(instanceId);
                InsectData data = pid != null ? collection.GetInsectData(pid.insectId) : null;
                if (data == null) continue;

                teamInsects[i] = data;
                teamLevels[i] = pid != null ? pid.level : 1;
                teamPids[i] = pid;

                teamSkills[i] = pid != null && collection != null ? collection.GetEquippedSkills(pid) : data.skills;

                count++;
            }

            if (count < BattleTeamManager.MaxSlots) return;

            raidController.StartRaid(savedTarget, teamInsects, teamLevels, teamPids, teamSkills);
        }

        // #코드 미표시 — BattleTeamUI.GetOwnedDisplayName과 같은 이유.
        private static string GetOwnedDisplayName(PlayerInsectData pid, InsectData data)
        {
            return data != null ? data.displayName : (pid != null ? pid.insectId : "Unknown");
        }

        public void AutoWire(
            CaptureMinigameController mg,
            InsectBattleController bc,
            InsectBattleUIController bui,
            BattleTeamManager tm,
            PlayerInsectCollection col,
            CaptureProximityTrigger prox,
            CaptureController cc,
            Dex.DexController dex,
            TrainingManager trm = null,
            PlayerItemInventory items = null,
            RaidBattleController raid = null)
        {
            if (minigame == null) minigame = mg;
            if (battleController == null) battleController = bc;
            if (battleUi == null) battleUi = bui;
            if (teamManager == null) teamManager = tm;
            if (collection == null) collection = col;
            if (proximityTrigger == null) proximityTrigger = prox;
            if (captureController == null) captureController = cc;
            if (dexController == null) dexController = dex;
            if (trainingManager == null) trainingManager = trm;
            if (itemInventory == null) itemInventory = items;
            if (raidController == null) raidController = raid;
        }

        public void AutoWire(PlayerMovement pm)
        {
            if (playerMovement == null) playerMovement = pm;
        }
    }

    /// <summary>
    /// 「바로 쓸 채집망」 고르기의 <b>순수</b> 판정. 화면(IMGUI)과 떼어 테스트로 고정한다 —
    /// 틀리면 귀한 채집망이 말없이 소모된다.
    /// </summary>
    public static class CaptureNetChoice
    {
        /// <param name="items">채집망 목록. 흔한 것부터 적혀 있다는 것이 전제다.</param>
        /// <param name="lastUsedId">지난번에 쓴 채집망. 없거나 다 떨어졌으면 목록 맨 앞의 가진 것으로 물러난다.</param>
        public static CaptureItemData Resolve(CaptureItemData[] items, string lastUsedId, System.Func<string, int> countOf)
        {
            if (items == null || countOf == null) return null;

            CaptureItemData firstOwned = null;
            for (int i = 0; i < items.Length; i++)
            {
                CaptureItemData item = items[i];
                if (item == null || countOf(item.itemId) <= 0) continue;
                if (item.itemId == lastUsedId) return item;
                if (firstOwned == null) firstOwned = item;
            }
            return firstOwned;
        }
    }
}
