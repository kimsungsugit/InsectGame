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
        // 캐시된 세션(refresh token)으로 자동 로그인 진행 중 — LoginUI가 이 동안 로그인 폼 대신
        // 로딩을 표시해 폼 깜빡임을 방지. 성공(LoginCompleted)/실패(AuthFailed) 시 false로 해제.
        public bool AutoLoginPending { get; private set; }
        public string UserId { get; private set; }
        public string Email { get; private set; }
        public string DisplayName { get; private set; }
        public string IdToken { get; private set; }
        public string RefreshToken { get; private set; }

        // ── 이벤트 ──
        public event Action<bool, string> LoginCompleted;
        public event Action<bool, string> RegisterCompleted;
        // 게스트(익명)→정식 계정 승격 결과 (linkWithCredential 상당).
        public event Action<bool, string> LinkCompleted;
        // 계정 삭제 결과 (Play 필수 정책: 인앱 계정 삭제).
        public event Action<bool, string> AccountDeleted;
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
            string savedUid = PlayerPrefs.GetString(UidKey, "");

            // 마스터 계정: refresh token이 로컬 더미라 Firebase 갱신 불가 → 네트워크 없이 즉시 로컬 재로그인.
            // 프로덕션 빌드(IsEnabled=false)에서는 마스터 자동 로그인도 비활성.
            if (MasterAccount.IsEnabled && savedUid == MasterAccount.Uid)
            {
                SetLoggedIn(MasterAccount.Uid,
                    PlayerPrefs.GetString(EmailKey, ""),
                    PlayerPrefs.GetString(NameKey, "마스터"),
                    MasterAccount.Token, MasterAccount.RefreshToken);
                ApplyMasterPrivileges();
                LoginCompleted?.Invoke(true, null);
                return;
            }

            string savedRefresh = PlayerPrefs.GetString(TokenKey, "");
            if (!string.IsNullOrEmpty(savedRefresh))
            {
                // 캐시된 refresh token으로 자동 로그인 시도(이메일/게스트 공통).
                RefreshToken = savedRefresh;
                AutoLoginPending = true;
                StartCoroutine(AutoLoginCoroutine());
            }
        }

        private IEnumerator AutoLoginCoroutine()
        {
            yield return RefreshIdTokenCoroutine();
            // 성공=LoginCompleted, 실패=ClearAuth+AuthFailed 가 RefreshIdTokenCoroutine 내부에서 발화됨.
            AutoLoginPending = false;
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
        public bool IsMasterAccount => UserId == MasterAccount.Uid;

        /// <summary>게스트(익명) 계정 여부 — 로그인됐지만 이메일이 없는 상태(마스터 제외).
        /// 정식 계정 연동(LinkGuestWithEmail)을 권유할 대상.</summary>
        public bool IsGuest => IsLoggedIn && !IsMasterAccount && string.IsNullOrEmpty(Email);

        public void LoginWithEmail(string email, string password)
        {
            // 마스터 계정 체크 — Firebase 없이 로컬 즉시 로그인 (검증 우회). 자격 증명은 소스에 없고
            // Resources/master_config.json 에서 로드되며, 프로덕션 빌드에는 이 분기가 컴파일되지 않음(MasterAccount).
            if (MasterAccount.TryMatch(email, password))
            {
                SetLoggedIn(MasterAccount.Uid, email, "마스터",
                    MasterAccount.Token, MasterAccount.RefreshToken);
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
            // Kakao는 Firebase 기본 IDP가 아님(google/apple/facebook 등만 지원). 통합 경로:
            //   1) Kakao SDK로 access token 획득
            //   2) 백엔드(Cloud Functions 등)가 Kakao 토큰 검증 → Firebase Custom Token 발급
            //   3) LoginWithCustomToken(customToken) 호출
            LoginCompleted?.Invoke(false,
                "카카오 로그인은 Kakao SDK + 백엔드 Custom Token 발급 설정이 필요합니다.");
        }

        // ── 게스트 → 정식 계정 연동 (Firebase accounts:update) ──

        /// <summary>게스트(익명) 계정을 이메일/비밀번호 정식 계정으로 승격.
        /// uid가 그대로 유지돼 기존 진행 데이터가 보존된다.</summary>
        public void LinkGuestWithEmail(string email, string password, string displayName)
        {
            if (!IsLoggedIn || string.IsNullOrEmpty(IdToken))
            {
                LinkCompleted?.Invoke(false, "로그인 상태가 아닙니다");
                return;
            }
            string validationError = ValidateCredentials(email, password);
            if (validationError != null)
            {
                LinkCompleted?.Invoke(false, validationError);
                return;
            }
            StartCoroutine(LinkEmailCoroutine(email, password, displayName));
        }

        private IEnumerator LinkEmailCoroutine(string email, string password, string displayName)
        {
            // accounts:update — 현재 idToken에 이메일/비밀번호 자격을 추가(익명→영구). uid 동일.
            // 이메일/비밀번호는 사용자 입력이라 JsonUtility로 안전하게 이스케이프.
            string json = JsonUtility.ToJson(new LinkRequest
            {
                idToken = IdToken,
                email = email,
                password = password,
                returnSecureToken = true
            });

            using (UnityWebRequest req = new UnityWebRequest(
                FirebaseConfig.WithKey(FirebaseConfig.UpdateAccountUrl), "POST"))
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
                    // uid 동일. 새 idToken/refreshToken이 오면 갱신, 비면 기존 유지.
                    Email = email;
                    DisplayName = string.IsNullOrEmpty(displayName) ? email : displayName;
                    if (!string.IsNullOrEmpty(response.idToken)) IdToken = response.idToken;
                    if (!string.IsNullOrEmpty(response.refreshToken)) RefreshToken = response.refreshToken;
                    idTokenAcquiredAt = Time.realtimeSinceStartup;
                    SaveTokens();
                    LinkCompleted?.Invoke(true, null);
                }
                else
                {
                    LinkCompleted?.Invoke(false, ParseFirebaseError(req.downloadHandler.text));
                }
            }
        }

        // ── 커스텀 토큰 로그인 (Kakao 등: 백엔드가 검증 후 Firebase Custom Token 발급) ──

        /// <summary>백엔드에서 발급한 Firebase Custom Token으로 로그인. Kakao처럼 Firebase 기본
        /// IDP가 아닌 제공자의 통합 진입점.</summary>
        public void LoginWithCustomToken(string customToken)
        {
            if (string.IsNullOrEmpty(customToken))
            {
                LoginCompleted?.Invoke(false, "유효하지 않은 토큰");
                return;
            }
            StartCoroutine(CustomTokenCoroutine(customToken));
        }

        private IEnumerator CustomTokenCoroutine(string customToken)
        {
            string json = JsonUtility.ToJson(new CustomTokenRequest
            {
                token = customToken,
                returnSecureToken = true
            });

            using (UnityWebRequest req = new UnityWebRequest(
                FirebaseConfig.WithKey(FirebaseConfig.SignInWithCustomTokenUrl), "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    // signInWithCustomToken 응답엔 localId가 없음 → refresh로 uid 확보 + 토큰 정착.
                    // RefreshIdTokenCoroutine이 user_id/IsLoggedIn 세팅 + LoginCompleted 발화.
                    CustomTokenResponse response =
                        JsonUtility.FromJson<CustomTokenResponse>(req.downloadHandler.text);
                    RefreshToken = response.refreshToken;
                    IdToken = response.idToken;
                    yield return RefreshIdTokenCoroutine();
                }
                else
                {
                    LoginCompleted?.Invoke(false, ParseFirebaseError(req.downloadHandler.text));
                }
            }
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

        // ── 계정 삭제 (Play 필수 정책) ──

        /// <summary>계정 영구 삭제: Firestore 문서 + Firebase Auth 계정 + 로컬 데이터 전부 제거.
        /// 서버 호출 실패해도 로컬은 정리하고 로그아웃한다(재진입 방지).</summary>
        public void DeleteAccount()
        {
            if (!IsLoggedIn)
            {
                AccountDeleted?.Invoke(false, "로그인 상태가 아닙니다");
                return;
            }
            if (IsMasterAccount || !FirebaseConfig.IsConfigured)
            {
                // 마스터/오프라인(Firebase 미설정)은 서버 계정이 없음 → 로컬만 정리.
                ClearAllLocalData();
                ClearAuth();
                AccountDeleted?.Invoke(true, null);
                LoggedOut?.Invoke();
                return;
            }
            StartCoroutine(DeleteAccountCoroutine());
        }

        private IEnumerator DeleteAccountCoroutine()
        {
            // 1) Firestore 사용자 문서 삭제 (토큰 유효한 동안 먼저).
            string docUrl = FirebaseConfig.FirestoreBaseUrl + "/users/" + UserId;
            using (UnityWebRequest del = UnityWebRequest.Delete(docUrl))
            {
                del.SetRequestHeader("Authorization", "Bearer " + IdToken);
                yield return del.SendWebRequest();
                // 문서 삭제 실패는 치명적 아님(없을 수도) — 계속 진행.
            }

            // 2) Firebase Auth 계정 삭제.
            string json = JsonUtility.ToJson(new DeleteRequest { idToken = IdToken });
            bool ok = false;
            string err = null;
            using (UnityWebRequest req = new UnityWebRequest(
                FirebaseConfig.WithKey(FirebaseConfig.DeleteAccountUrl), "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
                ok = req.result == UnityWebRequest.Result.Success;
                if (!ok) err = ParseFirebaseError(req.downloadHandler.text);
            }

            // 3) 서버 결과와 무관하게 로컬 정리 + 로그아웃 (재진입/잔존 방지).
            ClearAllLocalData();
            ClearAuth();
            AccountDeleted?.Invoke(ok, ok ? null : (err ?? "계정 삭제 실패"));
            LoggedOut?.Invoke();
        }

        // 로컬 세이브 파일 + PlayerPrefs 전체 삭제 (계정 삭제 시 기기 데이터 완전 초기화).
        private void ClearAllLocalData()
        {
            string[] files =
            {
                GameConstants.SaveFiles.PlayerProgress, GameConstants.SaveFiles.PlayerInsects,
                GameConstants.SaveFiles.PlayerCandies, GameConstants.SaveFiles.PlayerCurrency,
                GameConstants.SaveFiles.PlayerItems, GameConstants.SaveFiles.BattleTeam,
                GameConstants.SaveFiles.DexSave
            };
            foreach (string f in files)
            {
                try
                {
                    string p = System.IO.Path.Combine(Application.persistentDataPath, f);
                    if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Auth] 세이브 파일 삭제 실패: " + f + " — " + e.Message);
                }
            }
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
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
            if (savedUid == MasterAccount.Uid && newUid != MasterAccount.Uid)
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

        [Serializable]
        private class LinkRequest
        {
            public string idToken;
            public string email;
            public string password;
            public bool returnSecureToken;
        }

        [Serializable]
        private class CustomTokenRequest
        {
            public string token;
            public bool returnSecureToken;
        }

        [Serializable]
        private class DeleteRequest
        {
            public string idToken;
        }

        [Serializable]
        private class CustomTokenResponse
        {
            public string idToken;
            public string refreshToken;
        }
    }
}
