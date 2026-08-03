using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    public class WorldLobbyUI : MonoBehaviour
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
                phase = LobbyPhase.Hidden;
                PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
                if (pm != null) pm.SetFrozen(false);
                TutorialQuestManager.Instance?.BeginTutorialForCurrentAccount();
                return;
            }

            phase = LobbyPhase.WorldSelect;
            if (manager != null)
            {
                manager.RefreshWorldList();
            }
        }

        public void HideLobby()
        {
            phase = LobbyPhase.Hidden;
            ResetAllScrolls();
        }

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
            ResetAllScrolls();
        }

        private void Update()
        {
            if (errorTimer > 0) errorTimer -= Time.deltaTime;

            // Tab key: toggle InWorld overlay
            if (Input.GetKeyDown(KeyCode.Tab) && WorldChannelManager.Instance != null && WorldChannelManager.Instance.IsJoined)
            {
                if (phase == LobbyPhase.InWorld)
                {
                    phase = LobbyPhase.Hidden;
                    ResetPlayerScroll();
                }
                else if (phase == LobbyPhase.Hidden)
                {
                    phase = LobbyPhase.InWorld;
                    ResetPlayerScroll();
                }
            }
        }

        // ── Event Handlers ──

        private void OnWorldJoined()
        {
            phase = LobbyPhase.Hidden;
            ResetAllScrolls();
            // 월드 입장 후 플레이어 이동 해금
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) pm.SetFrozen(false);
            TutorialQuestManager.Instance?.BeginTutorialForCurrentAccount();
        }

        private void OnWorldLeft()
        {
            phase = LobbyPhase.WorldSelect;
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
                phase = LobbyPhase.WorldSelect;
                ResetWorldScroll();
            }
        }

        private void OnError(string msg)
        {
            errorMsg = msg;
            errorTimer = 5f;
            if (phase == LobbyPhase.Joining)
            {
                phase = LobbyPhase.WorldSelect;
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
                phase = LobbyPhase.Joining;
                ResetWorldScroll();
                if (manager != null) manager.AutoJoinWorld();
            }

            if (GUI.Button(new Rect(cx + btnW + 10f, cy, btnW, actionH), "\uc0c8\ub85c\uace0\uce68", btnBlueStyle))
            {
                if (manager != null) manager.RefreshWorldList();
            }
            cy += actionH + 10f;

            // 서버 배포 전이나 네트워크 장애 시에도 싱글플레이 진입을 보장합니다.
            if (GUI.Button(new Rect(cx, cy, innerW, mobile ? 56f : 32f), "오프라인으로 혼자 탐험", btnGrayStyle))
            {
                phase = LobbyPhase.Hidden;
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
                if (!isFull && manager != null)
                {
                    phase = LobbyPhase.Joining;
                    ResetWorldScroll();
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
            if (GUI.Button(new Rect(cx, cy, innerW, mobile ? 60f : 34f), "\uc6d4\ub4dc \ub098\uac00\uae30", btnRedStyle))
            {
                if (manager != null) manager.LeaveWorld();
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
