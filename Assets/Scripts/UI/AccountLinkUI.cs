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
                if (IsGuest()) DrawBadge();
            }

            DrawMessage();
        }

        // 상단 중앙 배지 — 게스트에게만 표시. 누르면 연동 폼 오픈.
        private void DrawBadge()
        {
            float w = Mathf.Min(440f, Screen.width * 0.55f);
            float h = 42f;
            float x = (Screen.width - w) * 0.5f;
            float y = 8f + SafeArea.Top; // 노치/펀치홀 아래로

            if (GUI.Button(new Rect(x, y, w, h), "게스트 모드 · 정식 계정으로 전환하기", badgeStyle))
            {
                message = "";
                SetOpen(true);
            }
        }

        private void DrawForm()
        {
            float pw = Mathf.Min(760f, Screen.width * 0.86f);
            float ph = Mathf.Min(660f, Screen.height * 0.9f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

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
            GUI.enabled = !isProcessing;
            if (GUI.Button(new Rect(cx, cy, btnW, 60f), isProcessing ? "처리 중..." : "연동하기", btnGreenStyle))
            {
                Submit();
            }
            if (GUI.Button(new Rect(cx + btnW + 16f, cy, btnW, 60f), "닫기", btnGrayStyle))
            {
                if (!isProcessing) SetOpen(false);
            }
            GUI.enabled = true;
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
            float y = open ? Screen.height * 0.5f + 200f : 58f;

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
