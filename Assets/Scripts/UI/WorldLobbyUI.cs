using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    public class WorldLobbyUI : MonoBehaviour, IModalUI
    {
        private enum LobbyPhase { WorldSelect, Joining, InWorld, Hidden }

        private LobbyPhase phase = LobbyPhase.Hidden;
        private Vector2 worldScrollPos;
        private Vector2 playerScrollPos;
        private readonly UIDirectScroll worldDirectScroll = new UIDirectScroll();
        private readonly UIDirectScroll playerDirectScroll = new UIDirectScroll();
        private string errorMsg;
        private float errorTimer;

        // 스타일 캐시
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle labelStyle;
        private GUIStyle errorStyle;
        private GUIStyle btnGreenStyle;
        private GUIStyle btnBlueStyle;
        private GUIStyle btnGrayStyle;
        private GUIStyle btnRedStyle;
        private GUIStyle worldRowStyle;
        private GUIStyle worldRowFullStyle;
        private GUIStyle playerRowStyle;
        private GUIStyle playerMeStyle;
        private GUIStyle barBgStyle;
        private GUIStyle barFillStyle;
        private bool stylesInitialized;

        // OnGUI 매 프레임 new Texture2D 회귀 차단용 정적 색 (UIHelper.GetCachedTex 키로 사용)
        private static readonly Color BgOverlayCol = new Color(0.03f, 0.03f, 0.1f, 0.85f);
        private static readonly Color RowFullCol = new Color(0.4f, 0.12f, 0.12f, 0.7f);
        private static readonly Color RowAlmostCol = new Color(0.45f, 0.4f, 0.1f, 0.6f);
        private static readonly Color RowOkCol = new Color(0.1f, 0.3f, 0.15f, 0.6f);
        private static readonly Color BarBgCol = new Color(0.15f, 0.15f, 0.2f, 1f);
        private static readonly Color FillFullCol = new Color(0.8f, 0.2f, 0.2f, 1f);
        private static readonly Color FillAlmostCol = new Color(0.85f, 0.75f, 0.15f, 1f);
        private static readonly Color FillOkCol = new Color(0.2f, 0.7f, 0.3f, 1f);
        private static readonly Color LineSepCol = new Color(0.4f, 0.4f, 0.45f, 0.6f);
        private static readonly Color MeRowBgCol = new Color(0.3f, 0.25f, 0.05f, 0.4f);

        private WorldChannelManager manager;
        private List<WorldInstance> cachedWorlds = new List<WorldInstance>();

        // ── Public API ──

        public void AutoWire(WorldChannelManager worldChannelManager)
        {
            manager = worldChannelManager;
        }

        public void ShowLobby()
        {
            ResetAllScrolls();
            // Firebase 미설정 또는 마스터 계정이면 월드 로비 건너뛰기
            bool skipLobby = false;
            if (!FirebaseConfig.IsConfigured)
                skipLobby = true;
            if (AuthManager.Instance != null && AuthManager.Instance.IsMasterAccount)
                skipLobby = true;
            if (AuthManager.Instance != null && !AuthManager.Instance.IsLoggedIn)
                skipLobby = true;
            // 마스터 토큰은 Firebase에서 유효하지 않음
            if (AuthManager.Instance != null && AuthManager.Instance.IdToken == MasterAccount.Token)
                skipLobby = true;

            if (skipLobby)
            {
                SetPhase(LobbyPhase.Hidden);
                PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
                if (pm != null) pm.SetFrozen(false);
                TutorialQuestManager.Instance?.BeginTutorialForCurrentAccount();
                return;
            }

            SetPhase(LobbyPhase.WorldSelect);
            // 로비를 띄우는 동안 이동을 잠근다. OnWorldLeft(로비 복귀)는 이미 잠그고 있었는데
            // **최초 진입 경로만 빠져 있어서**, 월드 선택 화면 뒤에서 WASD로 걸어 다닐 수 있었다.
            //
            // Bootstrap이 예전에 여기서 프리즈를 뺀 이유("월드 선택 미완료 시 frozen이 안 풀림")는
            // 지금은 해당하지 않는다 — WorldSelect를 빠져나가는 경로가 셋뿐이고 전부 푼다:
            // 입장 성공(OnWorldJoined) / 오프라인 진입 버튼 / skipLobby. HideLobby도 아래에서 푼다.
            SetPlayerFrozen(true);
            if (manager != null)
            {
                manager.RefreshWorldList();
            }
        }

        public void HideLobby()
        {
            SetPhase(LobbyPhase.Hidden);
            ResetAllScrolls();
            // 호출부가 0인 public 메서드지만 반드시 풀어 둔다 — 안 그러면 나중에 누가 부르는 순간
            // 영구 frozen이 된다(Bootstrap 주석이 경고한 바로 그 상황).
            SetPlayerFrozen(false);
        }

        // ── IModalUI ──
        // 로비가 떠 있는 동안 클릭 이동·포획 입력을 막는다(그쪽은 frozen이 아니라
        // ModalUIRegistry.IsAnyOpen()으로 게이트되므로 SetFrozen만으로는 안 막힌다).
        public bool IsOpen => phase != LobbyPhase.Hidden;

        /// <summary>
        /// ESC 처리. <b>월드 선택은 필수 단계라 ESC로 건너뛸 수 없다</b> — 여기서 오프라인으로
        /// 보내면 키 하나에 온라인을 포기하게 된다. InWorld 오버레이(Tab으로 여는 정보창)만 닫는다.
        /// </summary>
        public void CloseModal()
        {
            if (phase == LobbyPhase.InWorld)
            {
                SetPhase(LobbyPhase.Hidden);
                ResetPlayerScroll();
            }
        }

        // phase 전환의 단일 통로 — 모달 등록을 여기 한 곳에서만 관리한다.
        // 대입이 파일 곳곳에 흩어져 있어 등록/해제를 따로 붙이면 반드시 하나를 빠뜨린다.
        private void SetPhase(LobbyPhase next)
        {
            phase = next;
            if (phase == LobbyPhase.Hidden) ModalUIRegistry.Unregister(this);
            else ModalUIRegistry.Register(this);
        }

        private static void SetPlayerFrozen(bool frozen)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) pm.SetFrozen(frozen);
        }

        /// <summary>
        /// 입장 요청을 시작해도 되는지. 되면 Joining으로 넘기고 true.
        ///
        /// <b>매니저가 바쁘면 요청은 조용히 사라진다</b> — <c>CanStartRequest</c>가 <c>IsBusy</c>일 때
        /// 이벤트조차 쏘지 않고 반환한다. 그런데 로비에 있는 동안 목록 새로고침이 5초마다 자동으로
        /// 돌아 <c>IsBusy</c>가 상시 켜졌다 꺼지므로, 하필 그 구간에 누른 클릭은 요청 없이 증발한다.
        /// 예전엔 phase를 먼저 Joining으로 바꿔 놓아서, 화면만 "접속 중"으로 남고 아무 일도
        /// 일어나지 않았다. 먼저 물어보고 넘어간다.
        /// </summary>
        private bool BeginJoinRequest()
        {
            if (manager == null) return false;
            if (manager.IsBusy)
            {
                errorMsg = "잠시 후 다시 시도해 주세요.";
                errorTimer = 2f;
                return false;
            }

            SetPhase(LobbyPhase.Joining);
            ResetWorldScroll();
            joiningTimer = JoiningTimeoutSeconds;
            return true;
        }

        // Joining 안전망 — 매니저가 어떤 이유로든 완료·실패 이벤트를 안 쏘면 화면이 영구 고착한다.
        // (예: ActionCompleted만 쏘는 다른 요청이 IsBusy를 잡고 있었던 경우. 이 UI는 그 이벤트를
        //  구독하지 않는다.) 매니저 자체 타임아웃보다 길게 잡아 정상 실패가 먼저 오게 둔다.
        private const float JoiningTimeoutSeconds = 15f;
        private float joiningTimer;

        // ── Lifecycle ──

        private void OnEnable()
        {
            if (WorldChannelManager.Instance != null)
            {
                WorldChannelManager.Instance.WorldJoined += OnWorldJoined;
                WorldChannelManager.Instance.WorldLeft += OnWorldLeft;
                WorldChannelManager.Instance.WorldListUpdated += OnWorldListUpdated;
                WorldChannelManager.Instance.ErrorOccurred += OnError;
            }
            // 다시 켜졌을 때 등록도 되살린다 — OpeningReplayCoordinator가 UI 루트를 통째로
            // 껐다 켜므로(rules/ui-layout.md의 구독 회귀 계열) 아래 OnDisable이 지운 등록이
            // 살아나지 않으면 로비가 떠 있는데 입력이 뚫린다.
            if (phase != LobbyPhase.Hidden) ModalUIRegistry.Register(this);
        }

        private void OnDisable()
        {
            if (WorldChannelManager.Instance != null)
            {
                WorldChannelManager.Instance.WorldJoined -= OnWorldJoined;
                WorldChannelManager.Instance.WorldLeft -= OnWorldLeft;
                WorldChannelManager.Instance.WorldListUpdated -= OnWorldListUpdated;
                WorldChannelManager.Instance.ErrorOccurred -= OnError;
            }
            // 꺼진 컴포넌트가 스택에 남으면 그리지도 않는 모달이 ESC와 입력 차단을 계속 먹는다.
            ModalUIRegistry.Unregister(this);
            ResetAllScrolls();
        }

        private void Update()
        {
            if (errorTimer > 0) errorTimer -= Time.deltaTime;

            // Joining 고착 탈출 — 완료·실패 이벤트가 끝내 오지 않으면 목록으로 되돌린다.
            if (phase == LobbyPhase.Joining)
            {
                joiningTimer -= Time.deltaTime;
                if (joiningTimer <= 0f)
                {
                    SetPhase(LobbyPhase.WorldSelect);
                    ResetWorldScroll();
                    errorMsg = "월드 접속에 응답이 없습니다. 다시 시도해 주세요.";
                    errorTimer = 5f;
                    if (manager != null) manager.RefreshWorldList();
                }
            }

            // Tab key: toggle InWorld overlay
            if (Input.GetKeyDown(KeyCode.Tab) && WorldChannelManager.Instance != null && WorldChannelManager.Instance.IsJoined)
            {
                if (phase == LobbyPhase.InWorld)
                {
                    SetPhase(LobbyPhase.Hidden);
                    ResetPlayerScroll();
                }
                else if (phase == LobbyPhase.Hidden)
                {
                    SetPhase(LobbyPhase.InWorld);
                    ResetPlayerScroll();
                }
            }
        }

        // ── Event Handlers ──

        private void OnWorldJoined()
        {
            SetPhase(LobbyPhase.Hidden);
            ResetAllScrolls();
            // 월드 입장 후 플레이어 이동 해금
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) pm.SetFrozen(false);
            TutorialQuestManager.Instance?.BeginTutorialForCurrentAccount();
        }

        private void OnWorldLeft()
        {
            SetPhase(LobbyPhase.WorldSelect);
            ResetAllScrolls();
            // 로비로 돌아오면 이동 잠금
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) pm.SetFrozen(true);
        }

        private void OnWorldListUpdated(List<WorldInstance> worlds)
        {
            cachedWorlds = worlds ?? new List<WorldInstance>();
            if (phase == LobbyPhase.Joining)
            {
                SetPhase(LobbyPhase.WorldSelect);
                ResetWorldScroll();
            }
        }

        private void OnError(string msg)
        {
            errorMsg = msg;
            errorTimer = 5f;
            if (phase == LobbyPhase.Joining)
            {
                SetPhase(LobbyPhase.WorldSelect);
                ResetWorldScroll();
            }
        }

        private void ResetAllScrolls()
        {
            ResetWorldScroll();
            ResetPlayerScroll();
        }

        private void ResetWorldScroll()
        {
            worldScrollPos = Vector2.zero;
            worldDirectScroll.Reset();
        }

        private void ResetPlayerScroll()
        {
            playerScrollPos = Vector2.zero;
            playerDirectScroll.Reset();
        }

        // ── OnGUI ──

        private void OnGUI()
        {
            if (phase == LobbyPhase.Hidden) return;

            InitStyles();
            UIScale.Begin();

            switch (phase)
            {
                case LobbyPhase.WorldSelect:
                    DrawWorldSelectPanel();
                    break;
                case LobbyPhase.Joining:
                    DrawJoiningPanel();
                    break;
                case LobbyPhase.InWorld:
                    DrawInWorldPanel();
                    break;
            }
            UIScale.End();
        }

        // ── WorldSelect Panel ──

        private void DrawWorldSelectPanel()
        {
            // 배경 오버레이
            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;
            bool mobile = UIScale.IsMobileLayout;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), UIHelper.GetCachedTex(BgOverlayCol));

            Rect panel = UISafeLayout.CenteredPanel(mobile ? 900f : 600f, mobile ? 760f : 500f);
            float pw = panel.width;
            float ph = panel.height;
            float px = panel.x;
            float py = panel.y;

            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

            float cx = px + 30f;
            float cy = py + 20f;
            float innerW = pw - 60f;

            // 타이틀
            GUI.Label(new Rect(px, cy, pw, 40f), "\uc6d4\ub4dc \uc120\ud0dd", titleStyle);
            cy += 50f;

            // 월드 목록 스크롤
            float rowStride = mobile ? 92f : 74f;
            float listH = ph - (mobile ? 235f : 180f);
            Rect listArea = new Rect(cx, cy, innerW, listH);
            float contentH = Mathf.Max(listH, cachedWorlds.Count * rowStride);

            worldDirectScroll.Handle(
                ref worldScrollPos,
                listArea,
                contentH,
                rowStride * 0.5f);
            worldScrollPos = GUI.BeginScrollView(listArea, worldScrollPos, new Rect(0, 0, innerW - 20f, contentH));

            for (int i = 0; i < cachedWorlds.Count; i++)
            {
                DrawWorldRow(0, i * rowStride, innerW - 20f, mobile ? 84f : 68f, cachedWorlds[i]);
            }

            if (cachedWorlds.Count == 0)
            {
                GUI.Label(new Rect(0, 20f, innerW - 20f, 30f), "\uc6d4\ub4dc\ub97c \ubd88\ub7ec\uc624\ub294 \uc911...", labelStyle);
            }

            GUI.EndScrollView();
            cy += listH + 10f;

            // 버튼 행
            float btnW = (innerW - 10f) * 0.5f;
            float actionH = mobile ? 60f : 40f;

            if (GUI.Button(new Rect(cx, cy, btnW, actionH), "\uc790\ub3d9 \uc785\uc7a5", btnGreenStyle))
            {
                if (BeginJoinRequest()) manager.AutoJoinWorld();
            }

            if (GUI.Button(new Rect(cx + btnW + 10f, cy, btnW, actionH), "\uc0c8\ub85c\uace0\uce68", btnBlueStyle))
            {
                if (manager != null) manager.RefreshWorldList();
            }
            cy += actionH + 10f;

            // 서버 배포 전이나 네트워크 장애 시에도 싱글플레이 진입을 보장합니다.
            if (GUI.Button(new Rect(cx, cy, innerW, mobile ? 56f : 32f), "오프라인으로 혼자 탐험", btnGrayStyle))
            {
                SetPhase(LobbyPhase.Hidden);
                ResetAllScrolls();
                PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
                if (pm != null) pm.SetFrozen(false);
                TutorialQuestManager.Instance?.BeginTutorialForCurrentAccount();
            }

            // 에러 메시지
            if (errorTimer > 0 && !string.IsNullOrEmpty(errorMsg))
            {
                cy += 36f;
                GUI.Label(new Rect(cx, cy, innerW, 24f), errorMsg, errorStyle);
            }
        }

        private void DrawWorldRow(float x, float y, float w, float h, WorldInstance world)
        {
            bool mobile = UIScale.IsMobileLayout;
            bool isFull = world.playerCount >= world.maxPlayers;
            float ratio = (world.maxPlayers > 0) ? (float)world.playerCount / world.maxPlayers : 0f;

            // 행 배경색: 여유=초록, 거의참=노랑, 꽉참=빨강
            Color rowColor = isFull ? RowFullCol : (ratio > 0.7f ? RowAlmostCol : RowOkCol);
            GUI.DrawTexture(new Rect(x, y, w, h), UIHelper.GetCachedTex(rowColor));

            // 월드 이름
            GUI.Label(new Rect(x + 12f, y + 6f, mobile ? 280f : 200f, mobile ? 34f : 24f), world.displayName, subtitleStyle);

            // 인원 바
            float barX = x + 12f;
            float barY = y + (mobile ? 48f : 34f);
            float barW = w - (mobile ? 165f : 130f);
            float barH = mobile ? 24f : 18f;

            GUI.DrawTexture(new Rect(barX, barY, barW, barH), UIHelper.GetCachedTex(BarBgCol));

            Color fillColor = isFull ? FillFullCol : (ratio > 0.7f ? FillAlmostCol : FillOkCol);
            GUI.DrawTexture(new Rect(barX, barY, barW * ratio, barH), UIHelper.GetCachedTex(fillColor));

            // 인원 텍스트
            GUI.Label(new Rect(barX, barY - 1f, barW, barH), $"  {world.playerCount}/{world.maxPlayers}", labelStyle);

            // 입장 버튼
            float btnW = mobile ? 128f : 88f;
            float btnX = x + w - btnW - 12f;
            float btnH = mobile ? 56f : 36f;
            float btnY = y + (h - btnH) * 0.5f;
            GUI.enabled = !isFull;
            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), isFull ? "\uac00\ub4dd \ucc3c" : "\uc785\uc7a5", isFull ? btnRedStyle : btnGreenStyle))
            {
                if (!isFull && BeginJoinRequest())
                {
                    manager.JoinWorld(world.worldId);
                }
            }
            GUI.enabled = true;
        }

        // ── Joining Panel ──

        private void DrawJoiningPanel()
        {
            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), UIHelper.GetCachedTex(BgOverlayCol));

            Rect panel = UISafeLayout.CenteredPanel(
                UIScale.IsMobileLayout ? 600f : 300f,
                UIScale.IsMobileLayout ? 220f : 150f);
            float pw = panel.width;
            float ph = panel.height;
            float px = panel.x;
            float py = panel.y;

            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);
            GUI.Label(new Rect(px, py + 40f, pw, 40f), "\uc6d4\ub4dc \uc811\uc18d \uc911...", subtitleStyle);
        }

        // ── InWorld Panel (Tab overlay) ──

        private void DrawInWorldPanel()
        {
            if (WorldChannelManager.Instance == null || WorldChannelManager.Instance.CurrentWorld == null) return;

            WorldInstance cw = WorldChannelManager.Instance.CurrentWorld;

            bool mobile = UIScale.IsMobileLayout;
            // 모바일은 중앙, 데스크톱은 우측 상단 앵커.
            Rect panel = UISafeLayout.TopPanel(
                mobile ? 850f : 350f,
                mobile ? 620f : 400f,
                mobile ? UISafeLayout.HAlign.Center : UISafeLayout.HAlign.Right);
            float pw = panel.width;
            float ph = panel.height;
            float px = panel.x;
            float py = panel.y;

            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

            float cx = px + 16f;
            float cy = py + 12f;
            float innerW = pw - 32f;

            // 헤더
            GUI.Label(new Rect(px, cy, pw, 30f), $"{cw.displayName}  ({cw.playerCount}/{cw.maxPlayers})", subtitleStyle);
            cy += 38f;

            // 구분선
            GUI.DrawTexture(new Rect(cx, cy, innerW, 1f), UIHelper.GetCachedTex(LineSepCol));
            cy += 8f;

            // 플레이어 목록
            float listH = ph - 120f;
            float rowStride = mobile ? 58f : 34f;
            float contentH = Mathf.Max(listH, (cw.players != null ? cw.players.Count : 0) * rowStride);

            Rect playerListArea = new Rect(cx, cy, innerW, listH);
            playerDirectScroll.Handle(
                ref playerScrollPos,
                playerListArea,
                contentH,
                rowStride * 0.5f);
            playerScrollPos = GUI.BeginScrollView(playerListArea, playerScrollPos, new Rect(0, 0, innerW - 16f, contentH));

            if (cw.players != null)
            {
                string myUid = (AuthManager.Instance != null) ? AuthManager.Instance.UserId : "";

                for (int i = 0; i < cw.players.Count; i++)
                {
                    WorldPlayer p = cw.players[i];
                    bool isMe = p.uid == myUid;
                    float rowY = i * rowStride;

                    if (isMe)
                    {
                        GUI.DrawTexture(new Rect(0, rowY, innerW - 16f, mobile ? 52f : 30f), UIHelper.GetCachedTex(MeRowBgCol));
                    }

                    string prefix = isMe ? "\u2605 " : "  ";
                    string displayText = $"{prefix}{p.displayName}  Lv.{p.level}";
                    GUIStyle rowStyle = isMe ? playerMeStyle : playerRowStyle;
                    GUI.Label(new Rect(4f, rowY + 4f, innerW - 24f, mobile ? 44f : 24f), displayText, rowStyle);
                }
            }

            GUI.EndScrollView();
            cy += listH + 8f;

            // 월드 나가기 버튼
            // \uc694\uccad \uc911\uc5d0\ub294 \ub20c\ub9ac\uc9c0 \uc54a\uac8c \ud558\uace0 \uc9c4\ud589 \uc0c1\ud0dc\ub97c \ub77c\ubca8\ub85c \uc54c\ub9b0\ub2e4.
            bool busy = manager != null && manager.IsBusy;
            float leaveH = mobile ? 60f : 34f;
            GUI.enabled = !busy;
            if (GUI.Button(new Rect(cx, cy, innerW, leaveH),
                busy ? "\ub098\uac00\ub294 \uc911..." : "\uc6d4\ub4dc \ub098\uac00\uae30", btnRedStyle))
            {
                if (manager != null) manager.LeaveWorld();
            }
            GUI.enabled = true;
            cy += leaveH;

            // \uc5d0\ub7ec \ud45c\uc2dc \u2014 \uc608\uc804\uc5d4 \uc6d4\ub4dc \uc120\ud0dd \ud328\ub110\uc5d0\ub9cc \uc788\uc5c8\ub2e4. \uadf8\ub798\uc11c \ub098\uac00\uae30\uac00 \uc2e4\ud328\ud558\uba74
            // (\ub124\ud2b8\uc6cc\ud06c \uc624\ub958\u00b7\uc11c\ubc84 \uac70\uc808) phase\ub294 InWorld \uadf8\ub300\ub85c\ub77c **\ud654\uba74\uc5d0 \uc544\ubb34 \ubcc0\ud654\ub3c4 \uc5c6\uace0**
            // \ubc84\ud2bc\ub9cc \uacc4\uc18d \ub20c\ub9ac\ub294 \uc0c1\ud0dc\uac00 \ub410\ub2e4. \uc0ac\uc6a9\uc790\uc5d0\uac90 "\ub20c\ub7ec\ub3c4 \uc544\ubb34 \uc77c \uc5c6\ub294 \ubc84\ud2bc"\uc73c\ub85c \ubcf4\uc778\ub2e4.
            if (errorTimer > 0 && !string.IsNullOrEmpty(errorMsg))
            {
                GUI.Label(new Rect(cx, cy + 6f, innerW, mobile ? 40f : 24f), errorMsg, errorStyle);
            }
        }

        // ── Style Init ──

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            Texture2D panelTex = MakeTex(1, 1, new Color(0.06f, 0.06f, 0.12f, 0.94f));
            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTex;

            titleStyle = new GUIStyle(GUI.skin.label);
            bool mobile = UIScale.IsMobileLayout;

            titleStyle.fontSize = mobile ? 38 : 28;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(1f, 0.84f, 0f, 1f);
            titleStyle.alignment = TextAnchor.MiddleCenter;

            subtitleStyle = new GUIStyle(GUI.skin.label);
            subtitleStyle.fontSize = mobile ? 27 : 20;
            subtitleStyle.fontStyle = FontStyle.Bold;
            subtitleStyle.normal.textColor = Color.white;
            subtitleStyle.alignment = TextAnchor.MiddleCenter;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = mobile ? 20 : 14;
            labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.9f, 1f);
            labelStyle.alignment = TextAnchor.MiddleCenter;

            errorStyle = new GUIStyle(GUI.skin.label);
            errorStyle.fontSize = mobile ? 20 : 14;
            errorStyle.normal.textColor = new Color(1f, 0.3f, 0.3f, 1f);
            errorStyle.alignment = TextAnchor.MiddleCenter;
            errorStyle.wordWrap = true;

            GUIStyle MakeBtnStyle(Color bgColor, int fontSize = 18)
            {
                Texture2D tex = MakeTex(1, 1, bgColor);
                GUIStyle s = new GUIStyle(GUI.skin.button);
                s.normal.background = tex;
                s.hover.background = MakeTex(1, 1, bgColor * 1.15f);
                s.active.background = MakeTex(1, 1, bgColor * 0.85f);
                s.normal.textColor = Color.white;
                s.hover.textColor = Color.white;
                s.active.textColor = Color.white;
                s.fontSize = fontSize;
                s.fontStyle = FontStyle.Bold;
                s.alignment = TextAnchor.MiddleCenter;
                return s;
            }

            int actionFont = mobile ? 24 : 18;
            btnGreenStyle = MakeBtnStyle(new Color(0.15f, 0.55f, 0.15f, 1f), actionFont);
            btnBlueStyle = MakeBtnStyle(new Color(0.2f, 0.35f, 0.7f, 1f), actionFont);
            btnGrayStyle = MakeBtnStyle(new Color(0.35f, 0.35f, 0.38f, 1f), mobile ? 21 : 14);
            btnRedStyle = MakeBtnStyle(new Color(0.6f, 0.15f, 0.15f, 1f), mobile ? 22 : 15);

            playerRowStyle = new GUIStyle(GUI.skin.label);
            playerRowStyle.fontSize = mobile ? 22 : 15;
            playerRowStyle.normal.textColor = new Color(0.85f, 0.88f, 0.9f, 1f);

            playerMeStyle = new GUIStyle(GUI.skin.label);
            playerMeStyle.fontSize = mobile ? 22 : 15;
            playerMeStyle.fontStyle = FontStyle.Bold;
            playerMeStyle.normal.textColor = new Color(1f, 0.84f, 0f, 1f);
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
    }
}
