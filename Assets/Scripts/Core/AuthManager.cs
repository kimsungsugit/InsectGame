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
    // **이 매니저는 씬 스코프다 — DontDestroyOnLoad가 아니다.**
    //
    // 예전엔 Awake에 `if (transform.parent == null) DontDestroyOnLoad(gameObject);`가 있었는데,
    // 만드는 곳은 `PlaySceneBootstrap` 하나뿐이고 거기서 `World/AuthManager`로 **부모를 달아**
    // 만든다(`EnsureObject`가 경로대로 계층을 세운다). 그래서 그 가드는 **한 번도 통과한 적이
    // 없다.** 그런데 죽은 줄을 근거로 "씬을 재로드해도 살아 있다"는 주석이 두 곳에 생겼고
    // (`Logout`의 플러시, `AccountSettingsUI`의 재시작) 둘 다 틀렸다. 줄을 지우고 사실을 적는다.
    // 씬 재로드(로그아웃·계정삭제)는 이 매니저를 파기하고 부트스트랩이 새로 만든다.
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

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly object googleSignInLock = new object();
        private GoogleSignInCallback googleSignInCallback;
        private bool googleSignInInProgress;
        private bool googleSignInResultReady;
        private string pendingGoogleIdToken;
        private string pendingGoogleError;

        [UnityEngine.Scripting.Preserve]
        private sealed class GoogleSignInCallback : AndroidJavaProxy
        {
            private readonly AuthManager owner;

            public GoogleSignInCallback(AuthManager owner)
                : base("com.insectexploration.auth.GoogleSignInBridge$Callback")
            {
                this.owner = owner;
            }

            [UnityEngine.Scripting.Preserve]
            public void onSuccess(string idToken)
            {
                owner.SetPendingGoogleSignInResult(idToken, null);
            }

            [UnityEngine.Scripting.Preserve]
            public void onError(string error)
            {
                owner.SetPendingGoogleSignInResult(null, error);
            }
        }
#endif

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

            // 저장된 모드로 초기화 — 체크박스를 안 건드린 호출부가 모드를 뒤집지 않게.
            PendingMasterPlainMode = MasterPlainMode;

            TryAutoLogin();
        }

        // 파기될 때 static을 비운다. 안 그러면 `Instance != null`(UnityEngine.Object의 파괴 검사)과
        // `Instance?.`(진짜 null 검사)가 서로 다른 답을 내고, 후자는 파기된 객체로 호출이 들어간다.
        // `singleton_lint.py`가 이 짝을 강제한다.
        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
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
                // 특권 없이 모드면 건드리지 않는다 — 여기서 다시 박으면 처음부터 하던 판이 뒤집힌다.
                if (!MasterPlainMode) ApplyMasterPrivileges();
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
                // 타임아웃 미설정 시 Unity 기본값은 0(무제한)이라 모바일 네트워크가 물리면
                // OS TCP 타임아웃까지 수 분간 매달린다. 그동안 호출 UI는 "처리 중..."에
                // 갇힌다. WorldChannelManager(12초)와 같은 계열로 맞춘다.
                req.timeout = 15;

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

        // ── 마스터 "특권 없이 (처음부터)" 모드 ──────────────────────────────
        //
        // 마스터는 원래 진행을 건너뛰라고 있는 계정이라 매 로그인마다 전 지역 해금·전 수문장
        // 격파·재화 999999를 박는다(<see cref="ApplyMasterPrivileges"/>). 그 상태로는
        // **스토리를 처음부터 검증할 수 없다** — 수문장을 미리 격파로 기록해 버려서
        // `GuardianDefeated`가 뜨지 않고 `GuardianDefeat` 트리거 비트 6개(gd_*)가 통째로
        // 안 나온다. `requiredRegionId` 게이트도 전부 열린 지도 위에서는 무의미하다.
        //
        // 그래서 스위치를 둔다. **켜는 순간 한 번만** 세이브를 비우고, 켜져 있는 동안은
        // 아무것도 하지 않는다 — 로그아웃했다 들어와도 진행이 이어진다(그러지 않으면 로그인할
        // 때마다 세이브가 날아가는 함정이 된다).
        //
        // **자동 로그인도 이 값을 본다.** 안 그러면 다음 콜드 스타트에 특권이 도로 박혀
        // 처음부터 하던 판이 조용히 뒤집힌다.
        private const string MasterPlainModeKey = "InsectGame.MasterPlainMode";

        /// <summary>마스터가 특권 없이(일반 계정처럼) 플레이하는 중인가. 기기에 기억된다.</summary>
        public static bool MasterPlainMode
        {
            get => PlayerPrefs.GetInt(MasterPlainModeKey, 0) == 1;
            private set
            {
                PlayerPrefs.SetInt(MasterPlainModeKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 마스터 특권이 지금 실제로 걸려 있는가. <b>게임플레이 우회는 이걸 본다</b> —
        /// 클라우드·소셜을 끄는 판단은 여전히 <see cref="IsMasterAccount"/>다(특권을 껐다고
        /// 진짜 Firebase 계정이 생기는 건 아니다).
        /// </summary>
        public bool MasterPrivilegesActive => IsMasterAccount && !MasterPlainMode;

        /// <summary>
        /// 로그인 화면 체크박스 값. 마스터 자격 증명으로 로그인할 때만 읽힌다.
        /// Awake에서 저장값으로 초기화하므로, 설정하지 않는 호출부가 모드를 뒤집지 않는다.
        /// </summary>
        public bool PendingMasterPlainMode { get; set; }

        /// <summary>게스트(익명) 계정 여부 — 로그인됐지만 이메일이 없는 상태(마스터 제외).
        /// 정식 계정 연동(LinkGuestWithEmail)을 권유할 대상.</summary>
        public bool IsGuest => IsLoggedIn && !IsMasterAccount && string.IsNullOrEmpty(Email);

        public void LoginWithEmail(string email, string password)
        {
            // 마스터 계정 체크 — Firebase 없이 로컬 즉시 로그인 (검증 우회). 자격 증명은 소스에 없고
            // Resources/master_config.json 에서 로드되며, 프로덕션 빌드에는 이 분기가 컴파일되지 않음(MasterAccount).
            if (MasterAccount.TryMatch(email, password))
            {
                bool wasPlain = MasterPlainMode;
                bool plain = PendingMasterPlainMode;
                MasterPlainMode = plain;

                SetLoggedIn(MasterAccount.Uid, email, "마스터",
                    MasterAccount.Token, MasterAccount.RefreshToken);

                if (!plain) ApplyMasterPrivileges();
                else if (!wasPlain) BeginMasterFreshStart();   // 켠 그 로그인에서만

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

        /// <summary>
        /// PlayerPrefs 키를 현재 로그인 계정(UserId)별로 스코핑한다. 같은 기기에서 계정 간 데이터
        /// 교차 오염을 막기 위함(예: 퀘스트 진행). 비로그인/Instance 없음이면 전역 키로 폴백.
        /// </summary>
        public static string ScopedKey(string baseKey)
        {
            AuthManager inst = Instance;
            string uid = inst != null ? inst.UserId : null;
            return string.IsNullOrEmpty(uid) ? baseKey : baseKey + "." + uid;
        }

        /// <summary>
        /// 마스터를 <b>새 게임 상태로</b> 되돌린다. "특권 없이" 스위치를 켠 그 로그인에서 한 번만 돈다.
        ///
        /// 두 가지를 해야 한다 — 세이브만 지우면 부족하다. 지난 로그인이 PlayerPrefs에 박아 둔
        /// 흔적 중 <b>계정 스코프가 아닌 것</b>(재화 미러)이 남아, 새 지갑이 그걸 마이그레이션으로
        /// 빨아들여 999999로 시작한다(<c>CashShopManager</c>의 Gems 1회 이전 경로).
        /// 계정 스코프 쪽(해금·수문장·캐릭터 생성·퀘스트)은 <c>ClearCurrentAccountLocal</c>이 지우고,
        /// 지워지면 <c>RegionManager</c>가 기본값 "meadow"·수문장 없음으로 떨어진다.
        /// </summary>
        private void BeginMasterFreshStart()
        {
            // 세이브 8종(story_progress.json 포함) + 계정별 PlayerPrefs
            SaveScope.ClearCurrentAccountLocal();
            ClearLegacyProgressMirrors();
            // 레거시 전역 퀘스트 키 — 계정 스코프가 아니라 위 정리에 안 걸린다.
            PlayerPrefs.DeleteKey(GameConstants.PrefsKeys.QuestCompleted);
            PlayerPrefs.DeleteKey(GameConstants.PrefsKeys.QuestProgress);
            PlayerPrefs.DeleteKey(GameConstants.PrefsKeys.QuestUnseen);
            PlayerPrefs.Save();
            Debug.Log("[Auth] 마스터 처음부터 — 계정 로컬 세이브와 특권 흔적을 지웠다");
        }

        /// <summary>
        /// 계정 스코프가 아닌 진행/재화 미러 키. <see cref="ClearAllLocalData"/>와
        /// <see cref="BeginMasterFreshStart"/>가 공유한다 — 목록을 두 벌 들면 한쪽만 늘어난다.
        /// </summary>
        private static void ClearLegacyProgressMirrors()
        {
            PlayerPrefs.DeleteKey("player_level");
            PlayerPrefs.DeleteKey("player_xp");
            PlayerPrefs.DeleteKey("player_candies");
            PlayerPrefs.DeleteKey("player_coins");
            PlayerPrefs.DeleteKey("InsectGame.Gems");
        }

        private void ApplyMasterPrivileges()
        {
            if (!IsMasterAccount) return;

            // 모든 지역 해금 + 수문장 격파 — **RegionDefinitions에서 파생한다.**
            // 옛은 "meadow,pond,forest,swamp,mountain,garden,ruins"를 문자열로 박아 뒀는데,
            // 리전을 추가할 때마다 조용히 낡는다(2막 6지역 + ruins 수문장 신설에서 실제로 어긋났다).
            // 마스터는 RegionManager.IsRegionAccessible의 우회로 이동 자체는 되지만, 이 목록이
            // 낡으면 지도에 수문장이 미격파로 남고 필드에 스폰된다 — 진행을 건너뛰라고 있는 계정인데.
            var allRegions = RegionDefinitions.CreateAll();
            var unlocked = new System.Collections.Generic.List<string>(allRegions.Length);
            var guardians = new System.Collections.Generic.List<string>(allRegions.Length);
            foreach (var r in allRegions)
            {
                if (r == null || string.IsNullOrEmpty(r.regionId)) continue;
                unlocked.Add(r.regionId);
                // 수문장이 없는 리전은 격파 목록에 넣지 않는다(격파할 대상 자체가 없다).
                if (!string.IsNullOrEmpty(r.guardianInsectId)) guardians.Add(r.regionId);
            }
            PlayerPrefs.SetString(SaveScope.PrefsKey("InsectGame.UnlockedRegions"), string.Join(",", unlocked));
            PlayerPrefs.SetString(SaveScope.PrefsKey("InsectGame.DefeatedGuardians"), string.Join(",", guardians));

            // 무한 재화
            PlayerPrefs.SetInt("player_coins", 999999);
            PlayerPrefs.SetInt("player_candies", 999999);
            PlayerPrefs.SetInt("InsectGame.Gems", 999999);

            // 캐릭터 생성 완료 — Created는 GetInt로 판독되므로 SetInt로 기록(SetString이면 GetInt가 0 반환)
            PlayerPrefs.SetInt(SaveScope.PrefsKey("InsectGame.Character.Created"), 1);

            // 퀘스트는 자동 완료하지 않고 초기화 상태로 둔다 — 마스터 계정으로 튜토리얼을 처음부터 테스트할 수 있게.
            // (이전엔 20개 퀘스트를 전부 완료 처리해 마스터 로그인마다 튜토리얼 완료가 고정 → 재테스트/리셋 불가였음)
            // 하드닝 후 퀘스트는 계정별 키에서 읽히므로 계정별 키를 비우고, 레거시 전역 키도 함께 정리한다.
            PlayerPrefs.DeleteKey(ScopedKey(GameConstants.PrefsKeys.QuestCompleted));
            PlayerPrefs.DeleteKey(ScopedKey(GameConstants.PrefsKeys.QuestProgress));
            PlayerPrefs.SetString(ScopedKey(GameConstants.PrefsKeys.ActiveQuest), "");
            PlayerPrefs.DeleteKey(ScopedKey(GameConstants.PrefsKeys.QuestUnseen));
            PlayerPrefs.DeleteKey(GameConstants.PrefsKeys.QuestCompleted);
            PlayerPrefs.DeleteKey(GameConstants.PrefsKeys.QuestProgress);
            PlayerPrefs.DeleteKey(GameConstants.PrefsKeys.QuestUnseen);

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
                // 타임아웃 미설정 시 Unity 기본값은 0(무제한)이라 모바일 네트워크가 물리면
                // OS TCP 타임아웃까지 수 분간 매달린다. 그동안 호출 UI는 "처리 중..."에
                // 갇힌다. WorldChannelManager(12초)와 같은 계열로 맞춘다.
                req.timeout = 15;

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
            // Firebase 미설정이면 REST 호출이 무효 API키로 실패 → 명확한 사유 표시.
            if (!FirebaseConfig.IsConfigured)
            {
                LoginCompleted?.Invoke(false,
                    "서버 미설정 — Assets/Resources/firebase_config.json 설정이 필요합니다");
                yield break;
            }

            string json = "{\"returnSecureToken\":true}";

            using (UnityWebRequest req = new UnityWebRequest(
                FirebaseConfig.WithKey(FirebaseConfig.SignUpUrl), "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                // 타임아웃 미설정 시 Unity 기본값은 0(무제한)이라 모바일 네트워크가 물리면
                // OS TCP 타임아웃까지 수 분간 매달린다. 그동안 호출 UI는 "처리 중..."에
                // 갇힌다. WorldChannelManager(12초)와 같은 계열로 맞춘다.
                req.timeout = 15;

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
                    // 익명 인증 비활성/키 오류 등 실제 사유 파싱(ParseFirebaseError).
                    LoginCompleted?.Invoke(false, ParseFirebaseError(req.downloadHandler.text));
                }
            }
        }

        // ── Google 로그인 ──

        public void LoginWithGoogle()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!FirebaseConfig.IsGoogleConfigured)
            {
                LoginCompleted?.Invoke(false, "Google 로그인 설정이 없습니다.");
                return;
            }
            if (googleSignInInProgress) return;

            googleSignInInProgress = true;
            googleSignInCallback = new GoogleSignInCallback(this);
            try
            {
                using (AndroidJavaClass unityPlayer =
                    new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaClass bridge =
                    new AndroidJavaClass("com.insectexploration.auth.GoogleSignInBridge"))
                {
                    bridge.CallStatic("signIn", activity,
                        FirebaseConfig.GoogleWebClientId, googleSignInCallback);
                }
            }
            catch (Exception e)
            {
                googleSignInInProgress = false;
                googleSignInCallback = null;
                LoginCompleted?.Invoke(false, "Google 로그인 실행 실패: " + e.Message);
            }
#else
            LoginCompleted?.Invoke(false,
                "Google 로그인은 Android 기기 빌드에서 사용할 수 있습니다.");
#endif
        }

        public void LoginWithGoogleToken(string googleIdToken)
        {
            if (string.IsNullOrWhiteSpace(googleIdToken))
            {
                LoginCompleted?.Invoke(false, "Google ID 토큰을 받지 못했습니다.");
                return;
            }
            StartCoroutine(LoginWithIdpCoroutine("google.com", googleIdToken));
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void SetPendingGoogleSignInResult(string idToken, string error)
        {
            lock (googleSignInLock)
            {
                pendingGoogleIdToken = idToken;
                pendingGoogleError = error;
                googleSignInResultReady = true;
            }
        }

        private void ProcessPendingGoogleSignInResult()
        {
            string idToken;
            string error;
            lock (googleSignInLock)
            {
                if (!googleSignInResultReady) return;
                idToken = pendingGoogleIdToken;
                error = pendingGoogleError;
                pendingGoogleIdToken = null;
                pendingGoogleError = null;
                googleSignInResultReady = false;
            }

            googleSignInInProgress = false;
            googleSignInCallback = null;
            if (!string.IsNullOrEmpty(idToken))
            {
                LoginWithGoogleToken(idToken);
                return;
            }

            LoginCompleted?.Invoke(false,
                string.IsNullOrEmpty(error) ? "Google 로그인이 취소되었습니다." : error);
        }
#endif

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
                // 타임아웃 미설정 시 Unity 기본값은 0(무제한)이라 모바일 네트워크가 물리면
                // OS TCP 타임아웃까지 수 분간 매달린다. 그동안 호출 UI는 "처리 중..."에
                // 갇힌다. WorldChannelManager(12초)와 같은 계열로 맞춘다.
                req.timeout = 15;

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
                // 타임아웃 미설정 시 Unity 기본값은 0(무제한)이라 모바일 네트워크가 물리면
                // OS TCP 타임아웃까지 수 분간 매달린다. 그동안 호출 UI는 "처리 중..."에
                // 갇힌다. WorldChannelManager(12초)와 같은 계열로 맞춘다.
                req.timeout = 15;

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
                // 타임아웃 미설정 시 Unity 기본값은 0(무제한)이라 모바일 네트워크가 물리면
                // OS TCP 타임아웃까지 수 분간 매달린다. 그동안 호출 UI는 "처리 중..."에
                // 갇힌다. WorldChannelManager(12초)와 같은 계열로 맞춘다.
                req.timeout = 15;

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
                // 타임아웃 미설정 시 Unity 기본값은 0(무제한)이라 모바일 네트워크가 물리면
                // OS TCP 타임아웃까지 수 분간 매달린다. 그동안 호출 UI는 "처리 중..."에
                // 갇힌다. WorldChannelManager(12초)와 같은 계열로 맞춘다.
                req.timeout = 15;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    RefreshResponse response =
                        JsonUtility.FromJson<RefreshResponse>(req.downloadHandler.text);
                    IdToken = response.id_token;
                    RefreshToken = response.refresh_token;
                    UserId = response.user_id;
                    // 자동 로그인은 SetLoggedIn을 거치지 않으므로 여기서도 레거시→계정별 이전 보장.
                    SaveScope.MigrateLegacyIfOwned();
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
            // 로그아웃 전 마지막 클라우드 플러시 — ClearAuth가 토큰을 무효화하기 전에 시도해 마지막 진행 보존.
            // SaveToCloud는 동기적으로 코루틴을 시작해 현재 토큰으로 요청을 발사한다(첫 yield 전에 헤더 설정).
            //
            // **"CloudSaveManager는 DontDestroyOnLoad라 씬 리로드 후에도 완료된다"고 적혀 있었는데
            // 사실이 아니다.** 그 DDOL은 `if (transform.parent == null)` 가드 뒤에 있고,
            // `PlaySceneBootstrap`은 이 매니저를 `World/CloudSaveManager`로 만든다 — **부모가 있어
            // 가드가 통과하지 않는다.** 게다가 유일한 호출부(`AccountSettingsUI`)는 이 메서드 바로
            // 다음 줄에서 씬을 재로드하므로, 발사한 코루틴이 그 오브젝트와 함께 죽는다.
            // 로컬 세이브는 남으므로 잃는 것은 "이번 세션의 클라우드 사본"뿐이지만, 다른 기기에서
            // 로그인하면 한 세션 낡은 상태를 본다. 수명 자체를 고치는 건 architect 판단이라
            // audit 큐에 별건으로 올렸다(2026-08-23).
            //
            // 마스터/오프라인은 클라우드가 없으므로 생략.
            if (IsLoggedIn && !IsMasterAccount) CloudSaveManager.Instance?.SaveToCloud();
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
            // 삭제 진행 동안 자동/백그라운드 저장이 삭제된 Firestore 문서를 재생성(PII 부활)하지 않게 차단.
            CloudSaveManager.Instance?.SetDeletionInProgress(true);
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

            // 3) 성공 시에만 로컬 정리 + 로그아웃. 실패 시 로컬·세션 유지 → 사용자가 재시도 가능
            //    (서버 삭제 실패인데 로컬만 날리면 "계정 살아있는데 데이터 소실" 모순 상태).
            if (ok)
            {
                ClearAllLocalData();
                ClearAuth();
                AccountDeleted?.Invoke(true, null);
                LoggedOut?.Invoke();
            }
            else
            {
                // 삭제 실패 → 세션 유지하므로 자동저장 재개(삭제 차단 해제).
                CloudSaveManager.Instance?.SetDeletionInProgress(false);
                AccountDeleted?.Invoke(false, err ?? "계정 삭제 실패 — 다시 시도해주세요");
            }
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
            // 계정별(users/<uid>) 저장 파일 + 계정별 PlayerPrefs 키 삭제 — 위 루프는 레거시 전역 파일만 처리.
            SaveScope.ClearCurrentAccountLocal();

            // 전역(비계정) 미러/세션 키만 개별 삭제. PlayerPrefs.DeleteAll()은 같은 기기의 다른
            // 계정 스코프 키(.<otherUid>)와 기기 설정(볼륨 등)까지 파괴하므로 금지 — 공유 기기 격리 무력화.
            ClearLegacyProgressMirrors();
            PlayerPrefs.DeleteKey("InsectGame.CurrentWorldId");
            PlayerPrefs.DeleteKey("InsectGame.LastSaveTs");
            string deletedUid = UserId;
            if (!string.IsNullOrEmpty(deletedUid))
            {
                PlayerPrefs.DeleteKey("InsectGame.MigratedVer." + deletedUid);
                // 이 계정이 로컬 데이터 소유자였다면 소유권만 해제(다른 계정 스코프 데이터는 보존).
                if (PlayerPrefs.GetString("InsectGame.LocalOwnerUid", "") == deletedUid)
                    PlayerPrefs.DeleteKey("InsectGame.LocalOwnerUid");
            }
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
            // 레거시 전역 로컬 데이터를 계정별 위치로 1회 이전(이 계정이 소유자일 때만 — 데이터 보존).
            SaveScope.MigrateLegacyIfOwned();
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
#if UNITY_ANDROID && !UNITY_EDITOR
            // AndroidJavaProxy 콜백은 백그라운드 스레드에서 올 수 있으므로
            // Unity 메인 스레드인 Update에서 REST 로그인 코루틴을 시작합니다.
            ProcessPendingGoogleSignInResult();
#endif
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
                PlayerPrefs.SetString(SaveScope.PrefsKey("InsectGame.UnlockedRegions"), "meadow");
                PlayerPrefs.SetString(SaveScope.PrefsKey("InsectGame.DefeatedGuardians"), "");
                PlayerPrefs.SetInt("player_coins", 0);
                PlayerPrefs.SetInt("player_candies", 0);
                PlayerPrefs.SetInt("InsectGame.Gems", 0);
                PlayerPrefs.DeleteKey(SaveScope.PrefsKey("InsectGame.Character.Created"));
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
            // 익명(게스트) 인증 비활성 — Firebase Console에서 켜야 함
            if (responseBody.Contains("ADMIN_ONLY_OPERATION") || responseBody.Contains("OPERATION_NOT_ALLOWED"))
                return "게스트(익명) 로그인이 비활성화돼 있습니다. Firebase Console → Authentication → 익명 사용 설정";
            // API 키/프로젝트 설정 오류
            if (responseBody.Contains("API key not valid") || responseBody.Contains("API_KEY_INVALID")
                || responseBody.Contains("CONFIGURATION_NOT_FOUND"))
                return "Firebase 설정 오류 — firebase_config.json의 API 키/프로젝트 ID를 확인하세요";
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
