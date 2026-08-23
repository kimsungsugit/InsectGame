using InsectGame.Core;
using InsectGame.Opening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InsectGame.UI
{
    /// <summary>
    /// 계정 설정 패널(로그인 시 우하단 "계정" 버튼). 오프닝 다시 보기 + 로그아웃 + 계정 삭제 제공.
    /// 계정 삭제는 Play 필수 정책 — 2단계 확인 후 AuthManager.DeleteAccount(서버+로컬 영구 삭제).
    /// 삭제/로그아웃 성공 시 씬을 리로드해 로그인 화면으로 복귀.
    /// 오프닝 서비스는 Bootstrap AutoWire, 인증은 기존 AuthManager.Instance를 사용한다.
    /// </summary>
    public class AccountSettingsUI : MonoBehaviour, IModalUI
    {
        private bool open;
        private bool confirmDelete;
        // 게스트 로그아웃은 계정 삭제와 사실상 같은 결과라 별도 확인 단계를 둔다.
        private bool confirmLogout;
        private bool processing;
        private IOpeningReplayService openingReplayService;

        public void AutoWire(IOpeningReplayService replayService)
        {
            openingReplayService = replayService;
        }

        public bool IsOpen => open;
        public void CloseModal()
        {
            confirmDelete = false;
            confirmLogout = false;
            // 삭제 요청 중 창이 닫혔다가 다시 열리면 processing이 true로 남아
            // "삭제 중..."과 "취소"가 모두 비활성인 상태로 굳는다.
            processing = false;
            SetOpen(false);
        }

        // 모달 등록/해제 — 열려있는 동안 플레이어 이동(조이스틱/클릭) 차단 + ESC로 닫기.
        private void SetOpen(bool v)
        {
            open = v;
            if (v) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }

        private string message = "";
        private float messageTimer;
        private bool messageError;

        private GUIStyle panelStyle, titleStyle, infoStyle, openBtnStyle, btnGrayStyle, btnRedStyle, btnDarkStyle, msgStyle;
        private bool stylesReady;

        private void OnEnable()
        {
            if (AuthManager.Instance != null)
                AuthManager.Instance.AccountDeleted += OnAccountDeleted;
        }

        private void OnDisable()
        {
            if (AuthManager.Instance != null)
                AuthManager.Instance.AccountDeleted -= OnAccountDeleted;
            ModalUIRegistry.Unregister(this);
        }

        private void Update()
        {
            if (messageTimer > 0f) messageTimer -= Time.deltaTime;
        }

        private void OnAccountDeleted(bool success, string error)
        {
            processing = false;
            if (success)
            {
                // 깨끗한 재시작 → 로그인 화면.
                // (예전 주석은 "DontDestroyOnLoad 싱글턴은 상태 유지"라고 적었지만, 싱글턴들이
                //  `World/` 아래 자식으로 생성돼 그 DDOL 가드가 통과하지 않는다 — 전부 함께 파기되고
                //  새로 만들어진다. 로그인 화면으로 돌아가는 결과는 같지만 기전은 다르다.)
                ReloadScene();
            }
            else
            {
                message = error ?? "계정 삭제 실패 — 잠시 후 다시 시도해주세요";
                messageError = true;
                messageTimer = 5f;
                confirmDelete = false;
            }
        }

        private static bool LoggedIn()
        {
            return AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn;
        }

        private void OnGUI()
        {
            UIScale.Begin();
            try
            {
                DrawScaledGUI();
            }
            finally
            {
                UIScale.End();
            }
        }

        private void DrawScaledGUI()
        {
            if (!LoggedIn())
            {
                if (open) SetOpen(false);
                DrawMessage();
                return;
            }
            // 다른 모달이 열려 있으면 "계정" 버튼을 숨긴다(자기 자신은 제외).
            // 안 그러면 전체화면 모달 위에 버튼이 그려지고 클릭을 가로챈다
            // (MinimapUI:52, QuickAccessBarUI:113과 동일 관례).
            if (!open && ModalUIRegistry.IsAnyOpen())
            {
                DrawMessage();
                return;
            }
            EnsureStyles();
            if (open) DrawPanel();
            else DrawOpenButton();
            DrawMessage();
        }

        private void DrawOpenButton()
        {
            // 우측 하단 앵커 — 제스처바 + 세로 마진 위로.
            Rect btn = UISafeLayout.BottomPanel(116f, 46f, UISafeLayout.HAlign.Right);
            if (GUI.Button(btn, "계정", openBtnStyle))
            {
                confirmDelete = false;
                confirmLogout = false;
                processing = false;
                message = "";
                SetOpen(true);
            }
        }

        private void DrawPanel()
        {
            // IMGUI는 depth로 렌더 순서를 정한다. 기본 0이면 DexScreenUI(-10) 같은 패널
            // 아래에 깔리면서도 모달로 등록돼 "안 보이는데 입력만 먹는" 상태가 된다.
            GUI.depth = -20;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            float screenWidth = UIScale.VirtualScreenWidth;
            float screenHeight = UIScale.VirtualScreenHeight;
            GUI.DrawTexture(new Rect(0, 0, screenWidth, screenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            Rect panel = UISafeLayout.CenteredPanel(660f, (confirmDelete || confirmLogout) ? 440f : 448f);
            float pw = panel.width;
            float ph = panel.height;
            float px = panel.x;
            float py = panel.y;
            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

            GUI.Label(new Rect(px, py + 24f, pw, 46f), "계정", titleStyle);
            GUI.Label(new Rect(px + 40f, py + 80f, pw - 80f, 64f), AccountLabel(), infoStyle);

            float cx = px + 40f;
            float cw = pw - 80f;
            float y = py + 162f;

            if (!confirmDelete && !confirmLogout)
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && openingReplayService != null && openingReplayService.CanReplay;
                if (GUI.Button(new Rect(cx, y, cw, 56f), "오프닝 다시 보기", btnGrayStyle))
                {
                    bool started = ReplayOpening();
                    GUI.enabled = wasEnabled;
                    if (started) return;
                }
                GUI.enabled = wasEnabled;
                y += 68f;

                if (GUI.Button(new Rect(cx, y, cw, 56f), "로그아웃", btnGrayStyle))
                {
                    // 게스트는 refresh token이 유일한 재진입 수단이다. 로그아웃하면 그 토큰이
                    // 지워지는데 익명 재로그인은 매번 새 uid를 발급하므로, 기존 데이터에
                    // 영영 접근할 수 없다(세이브 파일은 users/<uid>/에 남지만 고아가 된다).
                    // 파괴가 의도인 계정 삭제조차 2단계 확인을 받는데 이쪽만 무방비였다.
                    // 정식 계정은 이메일로 다시 들어올 수 있으므로 즉시 로그아웃한다.
                    if (IsGuestAccount()) confirmLogout = true;
                    else LogoutAndReload();
                }
                y += 68f;
                if (GUI.Button(new Rect(cx, y, cw, 56f), "계정 삭제", btnRedStyle))
                {
                    confirmDelete = true;
                }
                y += 68f;
                if (GUI.Button(new Rect(cx, y, cw, 50f), "닫기", btnDarkStyle))
                    SetOpen(false);
            }
            else if (confirmLogout)
            {
                GUI.Label(new Rect(cx, y, cw, 88f),
                    "게스트 계정은 로그아웃하면 다시 들어올 수 없습니다.\n" +
                    "곤충·레벨·재화가 모두 사라지며 되돌릴 수 없습니다.\n" +
                    "먼저 '정식 계정으로 전환'을 권장합니다.",
                    infoStyle);
                y += 104f;
                if (GUI.Button(new Rect(cx, y, cw, 56f), "그래도 로그아웃", btnRedStyle))
                    LogoutAndReload();
                y += 68f;
                if (GUI.Button(new Rect(cx, y, cw, 50f), "취소", btnDarkStyle))
                    confirmLogout = false;
            }
            else
            {
                GUI.Label(new Rect(cx, y, cw, 88f),
                    "정말 계정을 삭제할까요?\n모든 진행 데이터(곤충·레벨·재화)가 영구 삭제되며 되돌릴 수 없습니다.",
                    infoStyle);
                y += 104f;
                bool prevEnabled = GUI.enabled;
                GUI.enabled = prevEnabled && !processing;
                if (GUI.Button(new Rect(cx, y, cw, 56f), processing ? "삭제 중..." : "영구 삭제", btnRedStyle))
                {
                    processing = true;
                    message = "";
                    if (AuthManager.Instance != null) AuthManager.Instance.DeleteAccount();
                }
                y += 68f;

                // 취소는 처리 중에도 반드시 눌려야 한다. 옛 코드는 위 GUI.enabled 블록이 이
                // 버튼까지 덮어 `confirmDelete = false`가 실행될 수 없었고
                // (GUI.enabled=false면 Button은 항상 false), Instance가 null이거나 응답
                // 이벤트를 놓치면 "삭제 중..."에서 영영 빠져나올 수 없었다 — 계정 삭제가
                // 앱 재시작 전까지 불가능해지는 건 스토어 정책 위반이기도 하다.
                GUI.enabled = prevEnabled;
                if (GUI.Button(new Rect(cx, y, cw, 50f), "취소", btnDarkStyle))
                {
                    confirmDelete = false;
                    processing = false;
                }
            }
        }

        private bool ReplayOpening()
        {
            IOpeningReplayService replayService = openingReplayService;
            SetOpen(false);

            bool started = false;
            try
            {
                started = replayService != null && replayService.TryReplay();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AccountSettingsUI] 오프닝 다시 보기 시작 실패: {e.Message}");
            }

            if (started) return true;

            // 전환을 시작하지 못했다면 사용자가 작업을 잃지 않도록 계정 패널을 복구한다.
            if (LoggedIn()) SetOpen(true);
            message = "오프닝을 시작할 수 없습니다. 잠시 후 다시 시도해주세요.";
            messageError = true;
            messageTimer = 4f;
            return false;
        }

        private static bool IsGuestAccount()
        {
            return AuthManager.Instance != null && AuthManager.Instance.IsGuest;
        }

        private void LogoutAndReload()
        {
            if (AuthManager.Instance != null) AuthManager.Instance.Logout();
            ReloadScene();
        }

        private static string AccountLabel()
        {
            AuthManager a = AuthManager.Instance;
            if (a == null) return "";
            if (a.IsMasterAccount) return "마스터 계정";
            if (a.IsGuest) return "게스트 (이메일 미연동)\n앱 삭제 시 데이터 복구 불가 — 상단 '정식 계정 전환' 권장";
            return string.IsNullOrEmpty(a.Email) ? (a.DisplayName ?? "로그인됨") : a.Email;
        }

        private void DrawMessage()
        {
            if (messageTimer <= 0f || string.IsNullOrEmpty(message)) return;
            EnsureStyles();
            float w = Mathf.Min(560f, UIScale.ContentWidth() * 0.7f);
            float h = 54f;
            float x = UIScale.VirtualSafeLeft + (UIScale.ContentWidth(0f) - w) * 0.5f;
            // 안전 영역 중앙보다 200 위 — 마진 위로는 넘지 않는다.
            float y = Mathf.Max(UISafeLayout.ContentTop, UISafeLayout.CenteredY(h) - 200f);

            GUI.color = messageError ? new Color(0.35f, 0.08f, 0.08f, 0.92f) : new Color(0.08f, 0.25f, 0.12f, 0.92f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            msgStyle.normal.textColor = messageError ? new Color(1f, 0.6f, 0.6f) : new Color(0.7f, 1f, 0.75f);
            GUI.Label(new Rect(x + 12f, y, w - 24f, h), message, msgStyle);
        }

        private static void ReloadScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = MakeTex(new Color(0.08f, 0.09f, 0.13f, 0.98f));

            titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(0.9f, 0.85f, 0.5f);

            infoStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 19, alignment = TextAnchor.UpperCenter, wordWrap = true };
            infoStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);

            openBtnStyle = MakeButton(new Color(0.28f, 0.3f, 0.36f));
            openBtnStyle.fontSize = 20;
            btnGrayStyle = MakeButton(new Color(0.32f, 0.34f, 0.4f));
            btnRedStyle = MakeButton(new Color(0.55f, 0.16f, 0.16f));
            btnDarkStyle = MakeButton(new Color(0.2f, 0.21f, 0.26f));

            msgStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        }

        private static GUIStyle MakeButton(Color bg)
        {
            GUIStyle s = new GUIStyle(GUI.skin.button)
            { fontSize = 23, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            s.normal.background = MakeTex(bg);
            s.hover.background = MakeTex(bg * 1.15f);
            s.active.background = MakeTex(bg * 0.85f);
            s.normal.textColor = Color.white;
            s.hover.textColor = Color.white;
            s.active.textColor = Color.white;
            return s;
        }

        private static Texture2D MakeTex(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }
    }
}
