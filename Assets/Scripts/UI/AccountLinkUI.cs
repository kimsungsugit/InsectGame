using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 게스트(익명) 계정을 이메일 정식 계정으로 승격하는 IMGUI 패널.
    /// 게스트일 때만 상단에 "정식 계정 전환" 배지를 띄우고, 클릭 시 연동 폼을 연다.
    /// 연동 성공 시 uid가 유지돼 기존 진행 데이터가 그대로 보존된다(AuthManager.LinkGuestWithEmail).
    /// 의존성 없음 — AuthManager.Instance 싱글턴만 사용. 부트스트랩이 EnsureComponent로 생성.
    /// </summary>
    public class AccountLinkUI : MonoBehaviour, IModalUI
    {
        private bool open;
        public bool IsOpen => open;
        public void Toggle() { SetOpen(!open); }
        public void CloseModal() { SetOpen(false); }

        // 모달 등록/해제 — 열려있는 동안 플레이어 이동(조이스틱/클릭) 차단 + ESC로 닫기.
        private void SetOpen(bool v)
        {
            open = v;
            if (v) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }

        private string emailInput = "";
        private string passwordInput = "";
        private string confirmInput = "";
        private string nicknameInput = "";
        private bool isProcessing;

        private string message = "";
        private float messageTimer;
        private bool messageIsError;

        private GUIStyle panelStyle, titleStyle, descStyle, labelStyle, fieldStyle;
        private GUIStyle btnGreenStyle, btnGrayStyle, badgeStyle, msgStyle;
        private bool stylesReady;

        private void OnEnable()
        {
            // 요청 중에 GameObject가 토글되면 OnDisable이 구독을 끊어 응답을 놓친다.
            // 그 상태로 재활성되면 isProcessing이 true로 남아 "연동하기"가 영영 비활성이므로
            // 여기서 초기화한다.
            isProcessing = false;
            if (AuthManager.Instance != null)
                AuthManager.Instance.LinkCompleted += OnLinkCompleted;
        }

        private void OnDisable()
        {
            if (AuthManager.Instance != null)
                AuthManager.Instance.LinkCompleted -= OnLinkCompleted;
            ModalUIRegistry.Unregister(this);
        }

        private void Update()
        {
            if (messageTimer > 0f) messageTimer -= Time.deltaTime;
        }

        private void OnLinkCompleted(bool success, string error)
        {
            isProcessing = false;
            if (success)
            {
                message = "정식 계정으로 전환되었습니다! 이제 어디서든 로그인할 수 있어요.";
                messageIsError = false;
                messageTimer = 5f;
                SetOpen(false);
                passwordInput = "";
                confirmInput = "";
            }
            else
            {
                message = error ?? "연동 실패 — 다시 시도해주세요";
                messageIsError = true;
                messageTimer = 5f;
            }
        }

        private static bool IsGuest()
        {
            return AuthManager.Instance != null && AuthManager.Instance.IsGuest;
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (open && IsGuest())
            {
                DrawForm();
            }
            else
            {
                if (open) SetOpen(false); // 연동 완료/비게스트 시 폼 자동 종료
                // 다른 모달이 열려 있으면 배지를 숨긴다. 안 그러면 전체화면 모달 위에
                // 배지가 그려지고 클릭까지 가로챈다 (MinimapUI/QuickAccessBarUI와 동일 관례).
                if (IsGuest() && !ModalUIRegistry.IsAnyOpen()) DrawBadge();
            }

            DrawMessage();
        }

        // 상단 중앙 배지 — 게스트에게만 표시. 누르면 연동 폼 오픈.
        private void DrawBadge()
        {
            float w = Mathf.Min(440f, Screen.width * 0.55f);
            float h = 42f;
            float x = (Screen.width - w) * 0.5f;
            float y = UISafeLayout.Px.ContentTop; // 노치/펀치홀 + 세로 마진 아래로

            if (GUI.Button(new Rect(x, y, w, h), "게스트 모드 · 정식 계정으로 전환하기", badgeStyle))
            {
                message = "";
                SetOpen(true);
            }
        }

        private void DrawForm()
        {
            // IMGUI는 depth로 렌더 순서를 정한다. 기본 0이면 DexScreenUI(-10) 같은 패널
            // 아래에 깔리면서도 모달로 등록돼 "안 보이는데 입력만 먹는" 상태가 된다.
            GUI.depth = -20;
            Rect panel = UISafeLayout.Px.CenteredPanel(760f, 660f);
            float pw = panel.width;
            float ph = panel.height;
            float px = panel.x;
            float py = panel.y;

            // 반투명 전체 딤
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

            float cx = px + 50f;
            float cy = py + 36f;
            float fieldW = pw - 100f;
            float fieldH = 58f;

            GUI.Label(new Rect(px, cy, pw, 56f), "정식 계정 전환", titleStyle);
            cy += 70f;

            GUI.Label(new Rect(cx, cy, fieldW, 56f),
                "현재 게스트 모드입니다. 이메일을 등록하면 기기를 바꿔도\n같은 데이터로 이어서 플레이할 수 있어요. (진행 데이터 유지)",
                descStyle);
            cy += 76f;

            GUI.Label(new Rect(cx, cy, fieldW, 30f), "이메일", labelStyle);
            cy += 34f;
            emailInput = GUI.TextField(new Rect(cx, cy, fieldW, fieldH), emailInput, 128, fieldStyle);
            cy += fieldH + 12f;

            GUI.Label(new Rect(cx, cy, fieldW, 30f), "비밀번호 (6자 이상)", labelStyle);
            cy += 34f;
            passwordInput = GUI.PasswordField(new Rect(cx, cy, fieldW, fieldH), passwordInput, '*', 64, fieldStyle);
            cy += fieldH + 12f;

            GUI.Label(new Rect(cx, cy, fieldW, 30f), "비밀번호 확인", labelStyle);
            cy += 34f;
            confirmInput = GUI.PasswordField(new Rect(cx, cy, fieldW, fieldH), confirmInput, '*', 64, fieldStyle);
            cy += fieldH + 12f;

            GUI.Label(new Rect(cx, cy, fieldW, 30f), "닉네임", labelStyle);
            cy += 34f;
            nicknameInput = GUI.TextField(new Rect(cx, cy, fieldW, fieldH), nicknameInput, 20, fieldStyle);
            cy += fieldH + 20f;

            float btnW = (fieldW - 16f) * 0.5f;
            bool prevEnabled = GUI.enabled;

            GUI.enabled = prevEnabled && !isProcessing;
            if (GUI.Button(new Rect(cx, cy, btnW, 60f), isProcessing ? "처리 중..." : "연동하기", btnGreenStyle))
            {
                Submit();
            }

            // 닫기는 처리 중에도 반드시 눌려야 한다. 옛 코드는 위 GUI.enabled 블록이 이
            // 버튼까지 덮어 `if (!isProcessing) SetOpen(false)`가 실행될 수 없었고
            // (GUI.enabled=false면 Button은 항상 false), 응답이 없으면 전체화면 딤 모달에
            // 갇혔다 — 탈출구가 ESC뿐이라 모바일에선 사실상 복구 불가.
            GUI.enabled = prevEnabled;
            if (GUI.Button(new Rect(cx + btnW + 16f, cy, btnW, 60f), "닫기", btnGrayStyle))
            {
                // 처리 중 닫아도 안전하다 — 응답이 오면 OnLinkCompleted가 isProcessing을
                // 내리고 결과는 DrawMessage 토스트로 표시된다.
                SetOpen(false);
            }
        }

        private void Submit()
        {
            if (isProcessing) return;
            if (passwordInput != confirmInput)
            {
                message = "비밀번호가 일치하지 않습니다.";
                messageIsError = true;
                messageTimer = 5f;
                return;
            }
            // 닉네임이 비면 AuthManager가 DisplayName을 email로 채운다 —
            // 월드/친구 목록에 이메일이 그대로 공개된다. 여기서 막는다.
            if (string.IsNullOrWhiteSpace(nicknameInput))
            {
                message = "닉네임을 입력해주세요. (비우면 이메일이 표시명으로 공개됩니다)";
                messageIsError = true;
                messageTimer = 5f;
                return;
            }
            if (AuthManager.Instance == null) return;
            isProcessing = true;
            message = "";
            AuthManager.Instance.LinkGuestWithEmail(emailInput, passwordInput, nicknameInput);
        }

        // 성공/오류 토스트 — 폼이 닫힌 뒤에도 잠깐 표시(연동 완료 알림).
        private void DrawMessage()
        {
            if (messageTimer <= 0f || string.IsNullOrEmpty(message)) return;

            float w = Mathf.Min(560f, Screen.width * 0.7f);
            float h = 56f;
            float x = (Screen.width - w) * 0.5f;
            // 폼이 열려 있으면 폼 아래(단 마진 안), 닫혀 있으면 상단 배지 아래.
            float y = open
                ? Mathf.Min(Screen.height * 0.5f + 200f, UISafeLayout.Px.ContentBottom - h)
                : UISafeLayout.Px.ContentTop + 50f;

            GUI.color = messageIsError
                ? new Color(0.35f, 0.08f, 0.08f, 0.92f)
                : new Color(0.08f, 0.25f, 0.12f, 0.92f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            msgStyle.normal.textColor = messageIsError
                ? new Color(1f, 0.6f, 0.6f) : new Color(0.7f, 1f, 0.75f);
            GUI.Label(new Rect(x + 12f, y, w - 24f, h), message, msgStyle);
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = MakeTex(new Color(0.08f, 0.09f, 0.13f, 0.97f));

            titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(1f, 0.84f, 0.2f);

            descStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.UpperCenter, wordWrap = true };
            descStyle.normal.textColor = new Color(0.82f, 0.85f, 0.9f);

            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.9f);

            fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 22 };
            fieldStyle.normal.textColor = Color.white;
            fieldStyle.normal.background = MakeTex(new Color(0.16f, 0.16f, 0.21f));
            fieldStyle.padding = new RectOffset(12, 12, 10, 10);

            btnGreenStyle = MakeButton(new Color(0.15f, 0.55f, 0.18f));
            btnGrayStyle = MakeButton(new Color(0.32f, 0.32f, 0.36f));

            badgeStyle = MakeButton(new Color(0.55f, 0.38f, 0.1f));
            badgeStyle.fontSize = 20;

            msgStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        }

        private static GUIStyle MakeButton(Color bg)
        {
            GUIStyle s = new GUIStyle(GUI.skin.button)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            // Color * float는 알파까지 곱한다 — 그대로 쓰면 눌림 상태(0.85)가 반투명해진다.
            // 명도만 조절하고 알파는 보존한다.
            s.normal.background = MakeTex(bg);
            s.hover.background = MakeTex(new Color(bg.r * 1.15f, bg.g * 1.15f, bg.b * 1.15f, bg.a));
            s.active.background = MakeTex(new Color(bg.r * 0.85f, bg.g * 0.85f, bg.b * 0.85f, bg.a));
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
