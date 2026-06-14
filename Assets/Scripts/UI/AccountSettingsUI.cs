using InsectGame.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InsectGame.UI
{
    /// <summary>
    /// 계정 설정 패널(로그인 시 우하단 "계정" 버튼). 로그아웃 + 계정 삭제 제공.
    /// 계정 삭제는 Play 필수 정책 — 2단계 확인 후 AuthManager.DeleteAccount(서버+로컬 영구 삭제).
    /// 삭제/로그아웃 성공 시 씬을 리로드해 로그인 화면으로 복귀. 의존성 없음(AuthManager.Instance).
    /// </summary>
    public class AccountSettingsUI : MonoBehaviour, IModalUI
    {
        private bool open;
        private bool confirmDelete;
        private bool processing;

        public bool IsOpen => open;
        public void CloseModal() { confirmDelete = false; SetOpen(false); }

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
                // 깨끗한 재시작 → 로그인 화면 (DontDestroyOnLoad 싱글턴은 로그아웃 상태 유지)
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
            if (!LoggedIn())
            {
                if (open) SetOpen(false);
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
            float w = 116f, h = 46f;
            float x = Screen.width - w - 16f - SafeArea.Right;
            float y = Screen.height - h - 16f - SafeArea.Bottom; // 제스처바 위로
            if (GUI.Button(new Rect(x, y, w, h), "계정", openBtnStyle))
            {
                confirmDelete = false;
                message = "";
                SetOpen(true);
            }
        }

        private void DrawPanel()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float pw = Mathf.Min(660f, Screen.width * 0.88f);
            float ph = confirmDelete ? 440f : 380f;
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;
            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

            GUI.Label(new Rect(px, py + 24f, pw, 46f), "계정", titleStyle);
            GUI.Label(new Rect(px + 40f, py + 80f, pw - 80f, 64f), AccountLabel(), infoStyle);

            float cx = px + 40f;
            float cw = pw - 80f;
            float y = py + 162f;

            if (!confirmDelete)
            {
                if (GUI.Button(new Rect(cx, y, cw, 56f), "로그아웃", btnGrayStyle))
                {
                    if (AuthManager.Instance != null) AuthManager.Instance.Logout();
                    ReloadScene();
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
            else
            {
                GUI.Label(new Rect(cx, y, cw, 88f),
                    "정말 계정을 삭제할까요?\n모든 진행 데이터(곤충·레벨·재화)가 영구 삭제되며 되돌릴 수 없습니다.",
                    infoStyle);
                y += 104f;
                GUI.enabled = !processing;
                if (GUI.Button(new Rect(cx, y, cw, 56f), processing ? "삭제 중..." : "영구 삭제", btnRedStyle))
                {
                    processing = true;
                    message = "";
                    if (AuthManager.Instance != null) AuthManager.Instance.DeleteAccount();
                }
                y += 68f;
                if (GUI.Button(new Rect(cx, y, cw, 50f), "취소", btnDarkStyle))
                    confirmDelete = false;
                GUI.enabled = true;
            }
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
            float w = Mathf.Min(560f, Screen.width * 0.7f);
            float h = 54f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.5f - 200f;

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
