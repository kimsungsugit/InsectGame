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
        // 토큰 갱신/인증 실패로 silent 로그아웃되는 경우 사용자에게 알릴 채널 (LoginUI 등 구독).
        public event Action<string> AuthFailed;

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
                if (transform.parent == null) DontDestroyOnLoad(gameObject);
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
            string validationError = ValidateCredentials(email, password);
            if (validationError != null)
            {
                // Firebase 호출 전 클라이언트 검증 — 빈 칸/짧은 비밀번호 즉시 차단
                AuthFailed?.Invoke(validationError);
                RegisterCompleted?.Invoke(false, validationError);
                return;
            }
            StartCoroutine(RegisterCoroutine(email, password, displayName));
        }

        // 이메일/비밀번호 사전 검증 — 빈 칸/형식 오류/짧은 비밀번호 즉시 차단.
        // 마스터 계정은 별도 분기에서 처리(LoginWithEmail 라인 115)되므로 검증 우회.
        private static string ValidateCredentials(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email))
                return "이메일을 입력해주세요";
            if (email.IndexOf('@') < 0 || email.IndexOf('.') < 0)
                return "이메일 형식이 올바르지 않습니다";
            if (string.IsNullOrEmpty(password))
                return "비밀번호를 입력해주세요";
            if (password.Length < 6)
                return "비밀번호는 6자 이상이어야 합니다";
            return null;
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
            // 마스터 계정 체크 — Firebase 없이 로컬 즉시 로그인 (검증 우회)
            if (email == "pride1119" && password == "qksqhf11!!")
            {
                SetLoggedIn("master_admin_001", "pride1119", "마스터", "master_token", "master_refresh");
                ApplyMasterPrivileges();
                LoginCompleted?.Invoke(true, null);
                return;
            }

            string validationError = ValidateCredentials(email, password);
            if (validationError != null)
            {
                AuthFailed?.Invoke(validationError);
                LoginCompleted?.Invoke(false, validationError);
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

        // CloudSave 등이 401 응답 받았을 때 외부에서 즉시 갱신을 트리거하기 위한 진입점.
        // 자동 갱신(AutoRefreshIfNeeded)과 동시 호출 방지 위해 refreshInProgress 가드 공유.
        // 결과: onComplete(true)=갱신 성공 후 재시도 가능, onComplete(false)=실패(AuthFailed 발화됨).
        public IEnumerator TryRefreshTokenForRetry(Action<bool> onComplete)
        {
            if (!IsLoggedIn || string.IsNullOrEmpty(RefreshToken))
            {
                onComplete?.Invoke(false);
                yield break;
            }
            // 이미 진행 중이면 끝날 때까지 대기 후 결과 판정 (IdToken 갱신 여부)
            string oldToken = IdToken;
            if (refreshInProgress)
            {
                while (refreshInProgress) yield return null;
                onComplete?.Invoke(IsLoggedIn && IdToken != oldToken);
                yield break;
            }
            refreshInProgress = true;
            yield return RefreshIdTokenCoroutine();
            refreshInProgress = false;
            onComplete?.Invoke(IsLoggedIn && IdToken != oldToken);
        }

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
                    // SetLoggedIn 경로를 거치지 않는 갱신이라 acquiredAt를 여기서 직접 갱신.
                    // 빠뜨리면 TryAutoLogin → 3000초 후 매 프레임 자동 갱신 폭주.
                    idTokenAcquiredAt = Time.realtimeSinceStartup;
                    SaveTokens();
                    LoginCompleted?.Invoke(true, null);
                }
                else
                {
                    // 토큰 갱신 실패 — silent ClearAuth 대신 사용자에게 알림 후 로그아웃
                    string reason = req.responseCode == 401 || req.responseCode == 403
                        ? "세션 만료 — 다시 로그인 해주세요"
                        : "인증 갱신 실패 — 네트워크 확인 후 다시 로그인 해주세요";
                    ClearAuth();
                    AuthFailed?.Invoke(reason);
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

        // Firebase IdToken은 발급 후 1시간 유효. 50분 경과 시 자동 갱신.
        private float idTokenAcquiredAt;
        private const float IdTokenLifetimeSeconds = 3600f;
        private const float IdTokenRefreshAheadSeconds = 600f; // 만료 10분 전 갱신
        private bool refreshInProgress;

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
            idTokenAcquiredAt = Time.realtimeSinceStartup;
            SaveTokens();
        }

        private void Update()
        {
            // 토큰 자동 갱신: 만료 임박 시 1회 RefreshIdTokenCoroutine 호출
            if (!IsLoggedIn || refreshInProgress || string.IsNullOrEmpty(RefreshToken)) return;
            float elapsed = Time.realtimeSinceStartup - idTokenAcquiredAt;
            if (elapsed >= IdTokenLifetimeSeconds - IdTokenRefreshAheadSeconds)
            {
                refreshInProgress = true;
                StartCoroutine(AutoRefreshThenClear());
            }
        }

        private IEnumerator AutoRefreshThenClear()
        {
            yield return StartCoroutine(RefreshIdTokenCoroutine());
            // RefreshIdTokenCoroutine 성공 시 SetLoggedIn 분기를 안 거치고 IdToken만 갱신하므로
            // idTokenAcquiredAt를 여기서 직접 갱신.
            idTokenAcquiredAt = Time.realtimeSinceStartup;
            refreshInProgress = false;
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
