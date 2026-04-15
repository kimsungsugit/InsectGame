using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace InsectGame.Core
{
    /// <summary>
    /// Firebase REST API 기반 인증 매니저.
    /// 이메일/게스트/Google/Kakao 로그인과 토큰 자동 갱신을 지원합니다.
    /// </summary>
    public class AuthManager : MonoBehaviour
    {
        public static AuthManager Instance { get; private set; }

        // ── 인증 상태 ──
        public bool IsLoggedIn { get; private set; }
        public string UserId { get; private set; }
        public string Email { get; private set; }
        public string DisplayName { get; private set; }
        public string IdToken { get; private set; }
        public string RefreshToken { get; private set; }

        // ── 이벤트 ──
        public event Action<bool, string> LoginCompleted;
        public event Action<bool, string> RegisterCompleted;
        public event Action LoggedOut;

        // ── PlayerPrefs 키 ──
        private const string TokenKey = "InsectGame.Auth.RefreshToken";
        private const string UidKey = "InsectGame.Auth.Uid";
        private const string EmailKey = "InsectGame.Auth.Email";
        private const string NameKey = "InsectGame.Auth.DisplayName";

        // ── Lifecycle ──

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            TryAutoLogin();
        }

        private void TryAutoLogin()
        {
            string savedRefresh = PlayerPrefs.GetString(TokenKey, "");
            if (!string.IsNullOrEmpty(savedRefresh))
            {
                RefreshToken = savedRefresh;
                StartCoroutine(RefreshIdTokenCoroutine());
            }
        }

        // ── 이메일 회원가입 ──

        public void RegisterWithEmail(string email, string password, string displayName)
        {
            StartCoroutine(RegisterCoroutine(email, password, displayName));
        }

        private IEnumerator RegisterCoroutine(string email, string password, string displayName)
        {
            string json = JsonUtility.ToJson(new AuthRequest
            {
                email = email,
                password = password,
                returnSecureToken = true
            });

            using (UnityWebRequest req = new UnityWebRequest(
                FirebaseConfig.WithKey(FirebaseConfig.SignUpUrl), "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    AuthResponse response =
                        JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
                    SetLoggedIn(response.localId, email, displayName,
                        response.idToken, response.refreshToken);
                    RegisterCompleted?.Invoke(true, null);
                }
                else
                {
                    string error = ParseFirebaseError(req.downloadHandler.text);
                    RegisterCompleted?.Invoke(false, error);
                }
            }
        }

        // ── 이메일 로그인 ──

        /// <summary>마스터 계정 여부 (Firebase 없이 로컬 로그인됨).</summary>
        public bool IsMasterAccount => UserId == "master_admin_001";

        public void LoginWithEmail(string email, string password)
        {
            // 마스터 계정 체크 — Firebase 없이 로컬 즉시 로그인
            if (email == "pride1119" && password == "qksqhf11!!")
            {
                SetLoggedIn("master_admin_001", "pride1119", "마스터", "master_token", "master_refresh");
                ApplyMasterPrivileges();
                LoginCompleted?.Invoke(true, null);
                return;
            }

            StartCoroutine(LoginCoroutine(email, password));
        }

        private void ApplyMasterPrivileges()
        {
            if (!IsMasterAccount) return;

            // 모든 지역 해금 + 수문장 격파
            PlayerPrefs.SetString("InsectGame.UnlockedRegions", "meadow,pond,forest,swamp,mountain,garden,ruins");
            PlayerPrefs.SetString("InsectGame.DefeatedGuardians", "meadow,pond,forest,swamp,mountain,garden");

            // 무한 재화
            PlayerPrefs.SetInt("player_coins", 999999);
            PlayerPrefs.SetInt("player_candies", 999999);
            PlayerPrefs.SetInt("InsectGame.Gems", 999999);

            // 캐릭터 생성 완료
            PlayerPrefs.SetString("InsectGame.Character.Created", "1");

            // 모든 퀘스트 완료
            PlayerPrefs.SetString("InsectGame.QuestCompleted",
                "q_move,q_approach,q_collection,q_dex,q_capture3,q_levelup,q_equip,q_battle," +
                "q_item,q_training,q_team,q_battle3,q_capture_rare,q_guardian1,q_visit_pond," +
                "q_subarea,q_raid,q_capture10,q_battle10,q_complete");
            PlayerPrefs.SetString("InsectGame.ActiveQuest", "");

            PlayerPrefs.Save();
        }

        private IEnumerator LoginCoroutine(string email, string password)
        {
            string json = JsonUtility.ToJson(new AuthRequest
            {
                email = email,
                password = password,
                returnSecureToken = true
            });

            using (UnityWebRequest req = new UnityWebRequest(
                FirebaseConfig.WithKey(FirebaseConfig.SignInUrl), "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    AuthResponse response =
                        JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
                    string name = string.IsNullOrEmpty(response.displayName)
                        ? email : response.displayName;
                    SetLoggedIn(response.localId, email, name,
                        response.idToken, response.refreshToken);
                    LoginCompleted?.Invoke(true, null);
                }
                else
                {
                    string error = ParseFirebaseError(req.downloadHandler.text);
                    LoginCompleted?.Invoke(false, error);
                }
            }
        }

        // ── 게스트 로그인 (Firebase 익명 인증) ──

        public void LoginAsGuest()
        {
            StartCoroutine(GuestLoginCoroutine());
        }

        private IEnumerator GuestLoginCoroutine()
        {
            string json = "{\"returnSecureToken\":true}";

            using (UnityWebRequest req = new UnityWebRequest(
                FirebaseConfig.WithKey(FirebaseConfig.SignUpUrl), "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    AuthResponse response =
                        JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
                    SetLoggedIn(response.localId, "", "탐험가",
                        response.idToken, response.refreshToken);
                    LoginCompleted?.Invoke(true, null);
                }
                else
                {
                    LoginCompleted?.Invoke(false, "게스트 로그인 실패");
                }
            }
        }

        // ── Google 로그인 ──

        public void LoginWithGoogle()
        {
            // TODO: Google Sign-In SDK 통합 후 Google ID Token을 받아서 아래 호출
            // LoginWithGoogleToken(googleIdToken);
            LoginCompleted?.Invoke(false,
                "Google 로그인은 Google Sign-In SDK 설정이 필요합니다.");
        }

        public void LoginWithGoogleToken(string googleIdToken)
        {
            StartCoroutine(LoginWithIdpCoroutine("google.com", googleIdToken));
        }

        // ── Kakao 로그인 ──

        public void LoginWithKakao()
        {
            // TODO: Kakao SDK 통합 후 Kakao Access Token을 받아서 아래 호출
            LoginCompleted?.Invoke(false,
                "카카오 로그인은 Kakao SDK 설정이 필요합니다.");
        }

        // ── IDP 로그인 공통 (Google/Kakao -> Firebase) ──

        private IEnumerator LoginWithIdpCoroutine(string providerId, string idToken)
        {
            string postBody = "{\"postBody\":\"id_token=" + idToken
                + "&providerId=" + providerId
                + "\",\"requestUri\":\"http://localhost\""
                + ",\"returnIdpCredential\":true"
                + ",\"returnSecureToken\":true}";

            using (UnityWebRequest req = new UnityWebRequest(
                FirebaseConfig.WithKey(FirebaseConfig.SignInWithIdpUrl), "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(postBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    AuthResponse response =
                        JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
                    string email = string.IsNullOrEmpty(response.email)
                        ? "" : response.email;
                    string name = string.IsNullOrEmpty(response.displayName)
                        ? "" : response.displayName;
                    SetLoggedIn(response.localId, email, name,
                        response.idToken, response.refreshToken);
                    LoginCompleted?.Invoke(true, null);
                }
                else
                {
                    LoginCompleted?.Invoke(false,
                        ParseFirebaseError(req.downloadHandler.text));
                }
            }
        }

        // ── 토큰 갱신 ──

        private IEnumerator RefreshIdTokenCoroutine()
        {
            string json = "{\"grant_type\":\"refresh_token\",\"refresh_token\":\""
                + RefreshToken + "\"}";

            using (UnityWebRequest req = new UnityWebRequest(
                FirebaseConfig.WithKey(FirebaseConfig.RefreshTokenUrl), "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    RefreshResponse response =
                        JsonUtility.FromJson<RefreshResponse>(req.downloadHandler.text);
                    IdToken = response.id_token;
                    RefreshToken = response.refresh_token;
                    UserId = response.user_id;
                    Email = PlayerPrefs.GetString(EmailKey, "");
                    DisplayName = PlayerPrefs.GetString(NameKey, "");
                    IsLoggedIn = true;
                    SaveTokens();
                    LoginCompleted?.Invoke(true, null);
                }
                else
                {
                    ClearAuth();
                }
            }
        }

        // ── 로그아웃 ──

        public void Logout()
        {
            ClearAuth();
            LoggedOut?.Invoke();
        }

        private void ClearAuth()
        {
            IsLoggedIn = false;
            UserId = null;
            Email = null;
            DisplayName = null;
            IdToken = null;
            RefreshToken = null;
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.DeleteKey(UidKey);
            PlayerPrefs.DeleteKey(EmailKey);
            PlayerPrefs.DeleteKey(NameKey);
            PlayerPrefs.Save();
        }

        // ── 헬퍼 ──

        private void SetLoggedIn(string uid, string email, string name,
            string idToken, string refreshToken)
        {
            ClearMasterDataIfNeeded(uid);
            UserId = uid;
            Email = email;
            DisplayName = string.IsNullOrEmpty(name) ? email : name;
            IdToken = idToken;
            RefreshToken = refreshToken;
            IsLoggedIn = true;
            SaveTokens();
        }

        private void ClearMasterDataIfNeeded(string newUid)
        {
            string savedUid = PlayerPrefs.GetString(UidKey, "");
            if (savedUid == "master_admin_001" && newUid != "master_admin_001")
            {
                PlayerPrefs.SetString("InsectGame.UnlockedRegions", "meadow");
                PlayerPrefs.SetString("InsectGame.DefeatedGuardians", "");
                PlayerPrefs.SetInt("player_coins", 0);
                PlayerPrefs.SetInt("player_candies", 0);
                PlayerPrefs.SetInt("InsectGame.Gems", 0);
                PlayerPrefs.DeleteKey("InsectGame.Character.Created");
                PlayerPrefs.DeleteKey("InsectGame.QuestCompleted");
                PlayerPrefs.DeleteKey("InsectGame.QuestProgress");
                PlayerPrefs.DeleteKey("InsectGame.ActiveQuest");
                PlayerPrefs.Save();
            }
        }

        private void SaveTokens()
        {
            PlayerPrefs.SetString(TokenKey, RefreshToken ?? "");
            PlayerPrefs.SetString(UidKey, UserId ?? "");
            PlayerPrefs.SetString(EmailKey, Email ?? "");
            PlayerPrefs.SetString(NameKey, DisplayName ?? "");
            PlayerPrefs.Save();
        }

        private string ParseFirebaseError(string responseBody)
        {
            if (string.IsNullOrEmpty(responseBody))
                return "알 수 없는 오류";
            if (responseBody.Contains("EMAIL_EXISTS"))
                return "이미 등록된 이메일입니다.";
            if (responseBody.Contains("EMAIL_NOT_FOUND"))
                return "등록되지 않은 이메일입니다.";
            if (responseBody.Contains("INVALID_PASSWORD"))
                return "비밀번호가 틀렸습니다.";
            if (responseBody.Contains("WEAK_PASSWORD"))
                return "비밀번호는 6자 이상이어야 합니다.";
            if (responseBody.Contains("INVALID_EMAIL"))
                return "올바른 이메일 형식이 아닙니다.";
            if (responseBody.Contains("TOO_MANY_ATTEMPTS"))
                return "너무 많은 시도입니다. 잠시 후 다시 시도하세요.";
            return "로그인 실패. 다시 시도해주세요.";
        }

        // ── JSON 직렬화용 클래스 ──

        [Serializable]
        private class AuthRequest
        {
            public string email;
            public string password;
            public bool returnSecureToken;
        }

        [Serializable]
        private class AuthResponse
        {
            public string localId;
            public string email;
            public string displayName;
            public string idToken;
            public string refreshToken;
        }

        [Serializable]
        private class RefreshResponse
        {
            public string id_token;
            public string refresh_token;
            public string user_id;
        }
    }
}
