using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace InsectGame.Core
{
    /// <summary>
    /// Firestore REST API 기반 클라우드 저장 매니저.
    /// 게임 데이터를 Firestore에 저장/로드하고 2분 간격 자동 저장을 수행합니다.
    /// </summary>
    public class CloudSaveManager : MonoBehaviour
    {
        public static CloudSaveManager Instance { get; private set; }

        public bool IsSaving { get; private set; }
        public bool IsLoading { get; private set; }
        public string LastError { get; private set; }

        public event Action SaveCompleted;
        public event Action<bool> LoadCompleted;
        public bool LastLoadWasNotFound { get; private set; }

        private float autoSaveTimer;
        private const float AutoSaveInterval = 120f;

        // 실제 진행도 저장소(파일 기반)와 연결 — 옛은 PlayerPrefs("player_level"/"player_candies"/
        // "player_coins")를 읽어 JSON 파일에 저장하는 실제 시스템과 어긋나 레벨/XP/캔디/코인이
        // 클라우드에 전혀 동기화되지 않았음(항상 기본값 0/1 업로드 + 로드 시 무시).
        private PlayerProgressController progressController;
        private PlayerCandyInventory candyInventory;
        private PlayerCurrencyWallet currencyWallet;

        public void AutoWire(PlayerProgressController progress,
            PlayerCandyInventory candy, PlayerCurrencyWallet wallet)
        {
            if (progressController == null) progressController = progress;
            if (candyInventory == null) candyInventory = candy;
            if (currencyWallet == null) currencyWallet = wallet;
        }

        // 클라우드 적용 후 인메모리 캐시를 다시 읽어야 하는 시스템들(곤충/팀/도감/지역/의상 등).
        // 옛은 ApplySaveData가 파일/PlayerPrefs만 덮어써서 다른 기기 첫 로그인 시 앱 재시작 전까지
        // 빈 상태로 보였음.
        private readonly List<ICloudReloadable> reloadables = new List<ICloudReloadable>();

        public void RegisterReloadable(ICloudReloadable reloadable)
        {
            if (reloadable != null && !reloadables.Contains(reloadable))
                reloadables.Add(reloadable);
        }

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
        }

        private static bool IsFirebaseConfigured()
        {
            return FirebaseConfig.IsConfigured
                && AuthManager.Instance != null
                && !AuthManager.Instance.IsMasterAccount;
        }

        private void Update()
        {
            if (!IsFirebaseConfigured()) return;
            if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn) return;

            autoSaveTimer += Time.deltaTime;
            if (autoSaveTimer >= AutoSaveInterval)
            {
                autoSaveTimer = 0f;
                SaveToCloud();
            }
        }

        // 종료 직전 강제 저장 — 자동저장 120초 사이클로 발생하는 마지막 1분 데이터 손실 방지
        private void OnApplicationQuit()
        {
            if (!IsFirebaseConfigured()) return;
            if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn) return;
            SaveToCloud();
        }

        // 모바일 백그라운드 전환 시에도 강제 저장 (앱 강제 종료 대비)
        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus) return;
            if (!IsFirebaseConfigured()) return;
            if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn) return;
            SaveToCloud();
        }

        // ── 저장 ──

        private bool pendingSave;
        private bool premiumTransactionInProgress;
        private const string LastSaveTsKey = "InsectGame.LastSaveTs";
        // 로컬 세이브가 어느 계정(uid) 소유인지 — 계정 전환 시 교차 오염 차단용.
        private const string LocalOwnerKey = "InsectGame.LocalOwnerUid";

        public void SaveToCloud()
        {
            // 계정 삭제 진행 중 — 삭제된 Firestore 문서를 자동/백그라운드 저장이 재생성(PII 부활)하지 않게 차단.
            if (deletionInProgress) return;
            if (!IsFirebaseConfigured()) return;
            if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn) return;
            if (pendingCloudData != null)
            {
                // 세이브 충돌 미해결 — ResolveConflict(로컬/클라우드 선택) 전엔 자동저장 보류.
                // 보류 중 로컬 스냅샷이 클라우드 원본을 덮으면 '클라우드 데이터 사용' 선택이 무의미해진다.
                // (충돌 해소는 ResolveConflict가 직접 저장/적용하므로 데이터 유실 없음)
                return;
            }
            if (premiumTransactionInProgress)
            {
                // 서버가 보석 잔액을 올리는 동안 오래된 로컬 스냅샷이 덮어쓰지 않도록 보류.
                pendingSave = true;
                return;
            }
            if (IsSaving)
            {
                // 진행 중인 저장이 끝난 직후 1회 더 저장 (보석 결제 등 사용자 액션 손실 방지)
                pendingSave = true;
                return;
            }
            StartCoroutine(SaveCoroutine());
        }

        private bool deletionInProgress;

        /// <summary>계정 삭제 진행 동안 모든 클라우드 저장 경로(자동저장/Pause/Quit)를 차단한다.
        /// 삭제 1단계(문서 DELETE)와 2단계(Auth DELETE) 사이에 PATCH가 삭제된 문서를 재생성하는 것을 막는다.
        /// 삭제 실패로 세션을 유지할 때만 false로 복원한다.</summary>
        public void SetDeletionInProgress(bool inProgress)
        {
            deletionInProgress = inProgress;
            if (inProgress)
            {
                autoSaveTimer = 0f;
                pendingSave = false;
            }
        }

        /// <summary>실결제 검증/지급과 일반 클라우드 저장의 gems 필드 경합을 차단한다.</summary>
        public void SetPremiumTransactionInProgress(bool inProgress)
        {
            premiumTransactionInProgress = inProgress;
            if (!inProgress && pendingSave && !IsSaving)
            {
                pendingSave = false;
                SaveToCloud();
            }
        }

        private IEnumerator SaveCoroutine()
        {
            yield return SaveCoroutineInternal(allowRetry: true);

            // 진행 중 들어온 요청 처리 (옛 SaveCoroutine 끝 로직 — 분기와 무관하게 항상 실행)
            if (pendingSave)
            {
                pendingSave = false;
                StartCoroutine(SaveCoroutine());
            }
        }

        private IEnumerator SaveCoroutineInternal(bool allowRetry)
        {
            IsSaving = true;

            // 로그아웃/계정전환 중 플러시(Logout이 SaveToCloud 직후 ClearAuth)를 대비해 대상 uid를
            // 첫 yield 전에 캡처. 네트워크 완료 후 LocalOwner/LastSaveTs를 '라이브' UserId(=null 또는
            // 전환된 계정)로 쓰면 교차계정 오염(다음 로그인 계정의 클라우드 진행 유실)이 발생.
            string targetUid = AuthManager.Instance != null ? AuthManager.Instance.UserId : null;

            GameSaveData data = CollectSaveData();
            string firestoreJson = ConvertToFirestoreDocument(data);

            string url = FirebaseConfig.FirestoreBaseUrl + "/users/" + targetUid;

            long responseCode = 0;
            bool success = false;
            string errorMsg = null;

            using (UnityWebRequest req = new UnityWebRequest(url, "PATCH"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(firestoreJson);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization",
                    "Bearer " + AuthManager.Instance.IdToken);

                yield return req.SendWebRequest();

                responseCode = req.responseCode;
                success = req.result == UnityWebRequest.Result.Success;
                errorMsg = req.error;
            }

            IsSaving = false;

            if (success)
            {
                LastError = null;
                // 인증 컨텍스트가 그대로(같은 계정 로그인 중)일 때만 전역 동기화 마커를 갱신.
                // 로그아웃/계정전환 중 도착한 stale 플러시면 전역 키를 건드리지 않는다 — 안 그러면
                // LocalOwner가 ""/타계정으로 덮여 다음 로그인 계정의 클라우드 적용이 거부됨(R2 회귀).
                // (PATCH 자체는 targetUid 문서에 정상 반영됨.)
                bool sameAuth = AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn
                    && AuthManager.Instance.UserId == targetUid;
                if (sameAuth)
                {
                    PlayerPrefs.SetString(LastSaveTsKey, data.lastSaveTimestamp.ToString());
                    PlayerPrefs.SetString(LocalOwnerKey, targetUid ?? ""); // 로컬=이 계정 소유
                    PlayerPrefs.Save();
                }
                SaveCompleted?.Invoke();
                yield break;
            }

            // 401/403: 토큰 갱신 후 1회 재시도 (보석 결제 등 사용자 액션 손실 방지)
            if ((responseCode == 401 || responseCode == 403) && allowRetry)
            {
                bool refreshed = false;
                yield return AuthManager.Instance.TryRefreshTokenForRetry(r => refreshed = r);
                if (refreshed)
                {
                    yield return SaveCoroutineInternal(allowRetry: false);
                    yield break;
                }
                LastError = "session_expired";
                Debug.LogWarning("[CloudSave] Save failed: session expired, refresh denied");
                yield break;
            }

            LastError = errorMsg;
            Debug.LogWarning("[CloudSave] Save failed: " + errorMsg);
        }

        // ── 로드 ──

        // 모든 로컬 파일 서비스를 현재 계정의 계정별 파일에서 인메모리로 재로드.
        // 서비스 Awake는 로그인 전(UserId=null)에 전역 경로를 읽으므로, 로그인+마이그레이션 후 1회 교정 필요.
        public void ReloadAllLocalFromDisk()
        {
            for (int i = 0; i < reloadables.Count; i++)
                if (reloadables[i] != null) reloadables[i].ReloadFromDisk();
            if (candyInventory != null) candyInventory.ReloadFromDisk();
            if (currencyWallet != null) currencyWallet.ReloadFromDisk();
            if (progressController != null) progressController.ReloadFromDisk();
        }

        public void LoadFromCloud()
        {
            LastLoadWasNotFound = false;
            // 로그인+마이그레이션 후 계정별 파일에서 재로드 — 부트(UserId=null)에 읽은 전역분 교정.
            // 오프라인/미설정/404 등 클라우드 적용이 없는 경로에서도 계정 격리를 보장.
            ReloadAllLocalFromDisk();
            if (!IsFirebaseConfigured())
            {
                // Firebase 미설정이면 즉시 "데이터 없음"으로 완료
                LoadCompleted?.Invoke(false);
                return;
            }
            if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn) return;
            if (IsLoading) return;
            StartCoroutine(LoadCoroutine());
        }

        private IEnumerator LoadCoroutine()
        {
            yield return LoadCoroutineInternal(allowRetry: true);
        }

        private IEnumerator LoadCoroutineInternal(bool allowRetry)
        {
            IsLoading = true;

            string url = FirebaseConfig.FirestoreBaseUrl
                + "/users/" + AuthManager.Instance.UserId;

            long responseCode = 0;
            bool success = false;
            string errorMsg = null;
            string downloadText = null;

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization",
                    "Bearer " + AuthManager.Instance.IdToken);

                yield return req.SendWebRequest();

                responseCode = req.responseCode;
                success = req.result == UnityWebRequest.Result.Success;
                errorMsg = req.error;
                if (success) downloadText = req.downloadHandler.text;
            }

            IsLoading = false;

            if (success)
            {
                GameSaveData data = ParseFirestoreDocument(downloadText);
                if (data != null)
                {
                    bool applied = ApplySaveData(data);
                    if (applied) LoadCompleted?.Invoke(true);
                    // 보류(충돌)면 SaveConflictUI 표시 후 ResolveConflict가 LoadCompleted를 발화.
                }
                else
                {
                    LoadCompleted?.Invoke(false);
                }
                yield break;
            }

            // 401/403: 토큰 만료 가능성 → 1회 갱신 후 재시도. 갱신 실패면 새 유저 취급.
            // 옛 코드는 401/403을 "Firebase 미설정"으로 단일 처리해서 정상 유저도 데이터 손실 위험.
            if ((responseCode == 401 || responseCode == 403) && allowRetry)
            {
                bool refreshed = false;
                yield return AuthManager.Instance.TryRefreshTokenForRetry(r => refreshed = r);
                if (refreshed)
                {
                    yield return LoadCoroutineInternal(allowRetry: false);
                    yield break;
                }
                // 갱신 실패 = AuthFailed 발화됨 + ClearAuth 완료. 새 유저 취급.
                LoadCompleted?.Invoke(false);
                yield break;
            }

            if (responseCode == 404 || responseCode == 401 || responseCode == 403)
            {
                // 404=새유저, 갱신 후에도 401/403=Firebase 미설정 또는 영구 거부
                LastLoadWasNotFound = responseCode == 404;
                LoadCompleted?.Invoke(false);
            }
            else
            {
                LastError = errorMsg;
                Debug.LogWarning("[CloudSave] Load failed: " + errorMsg);
                LoadCompleted?.Invoke(false);
            }
        }

        // ── 데이터 수집 ──

        private GameSaveData CollectSaveData()
        {
            return new GameSaveData
            {
                displayName = AuthManager.Instance.DisplayName,
                // 실제 시스템(파일 기반)에서 읽음 — null이면 PlayerPrefs 폴백(부트 순서 안전).
                playerLevel = progressController != null ? progressController.Level : PlayerPrefs.GetInt("player_level", 1),
                playerXp = progressController != null ? progressController.CurrentXp : PlayerPrefs.GetInt("player_xp", 0),
                candies = candyInventory != null ? candyInventory.Candies : PlayerPrefs.GetInt("player_candies", 0),
                coins = currencyWallet != null ? currencyWallet.Coins : PlayerPrefs.GetInt("player_coins", 0),
                // gems는 PlayerCurrencyWallet 단일 소스. CashShopManager가 wallet 경유 노출.
                gems = CashShopManager.Instance != null ? CashShopManager.Instance.Gems : 0,
                ownedInsects = LoadLocalFile(GameConstants.SaveFiles.PlayerInsects),
                battleTeam = LoadLocalFile(GameConstants.SaveFiles.BattleTeam),
                dexData = LoadLocalFile(GameConstants.SaveFiles.DexSave),
                playerItems = LoadLocalFile(GameConstants.SaveFiles.PlayerItems),
                equippedOutfit = PlayerPrefs.GetString(SaveScope.PrefsKey("InsectGame.Equipped"), ""),
                ownedOutfits = PlayerPrefs.GetString(SaveScope.PrefsKey("InsectGame.OwnedOutfits"), ""),
                unlockedRegions = PlayerPrefs.GetString(
                    SaveScope.PrefsKey("InsectGame.UnlockedRegions"), "meadow"),
                defeatedGuardians = PlayerPrefs.GetString(
                    SaveScope.PrefsKey("InsectGame.DefeatedGuardians"), ""),
                // 퀘스트는 계정별 키에서 읽어 현재 계정의 진행만 클라우드에 올린다(교차 오염 방지).
                questProgress = PlayerPrefs.GetString(
                    AuthManager.ScopedKey(GameConstants.PrefsKeys.QuestProgress), ""),
                questCompleted = PlayerPrefs.GetString(
                    AuthManager.ScopedKey(GameConstants.PrefsKeys.QuestCompleted), ""),
                activeQuest = PlayerPrefs.GetString(
                    AuthManager.ScopedKey(GameConstants.PrefsKeys.ActiveQuest), ""),
                // 스토리 진행은 story_progress.json 파일(StoryDirector 저작) — 퀘스트와 달리 파일 기반이라
                // dexData/playerItems처럼 LoadLocalFile로 수집(계정별 SaveScope 경로).
                storyProgress = LoadLocalFile(GameConstants.SaveFiles.StoryProgress),
                // 캐릭터 외형(LoginUI가 PlayerPrefs "InsectGame.Character.*"에 저장) — 옛은 클라우드
                // 미수집이라 다른 기기 접속 시 외형(피부/머리/표정/성별) 전부 초기화됐음.
                charCreated = PlayerPrefs.GetInt(SaveScope.PrefsKey("InsectGame.Character.Created"), 0),
                charName = PlayerPrefs.GetString(SaveScope.PrefsKey("InsectGame.Character.Name"), ""),
                charSkin = PlayerPrefs.GetInt(SaveScope.PrefsKey("InsectGame.Character.SkinColor"), 0),
                charHair = PlayerPrefs.GetInt(SaveScope.PrefsKey("InsectGame.Character.HairStyle"), 0),
                charGender = PlayerPrefs.GetInt(SaveScope.PrefsKey("InsectGame.Character.Gender"), 0),
                charHairColor = PlayerPrefs.GetInt(SaveScope.PrefsKey("InsectGame.Character.HairColor"), 0),
                charFace = PlayerPrefs.GetInt(SaveScope.PrefsKey("InsectGame.Character.FaceType"), 0),
                charOutfit = PlayerPrefs.GetInt(SaveScope.PrefsKey("InsectGame.Character.OutfitPreset"), 0),
                lastSaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        // ── 데이터 적용 ──

        // 로드된 클라우드 데이터를 적용하되, 충돌(클라우드가 더 최신 + 로컬에 의미있는 진행) 시
        // 사용자 선택을 위해 보류한다. 반환: true=적용/처리 완료(LoadCompleted 진행 가능),
        // false=충돌로 보류(ResolveConflict가 마무리).
        private bool ApplySaveData(GameSaveData data)
        {
            // 타임스탬프 가드: 로컬 lastSaveTimestamp(마지막 클라우드 푸시)가 클라우드보다 새것이면
            // 이 기기가 앞선 것 → 덮어쓰기 거부(로컬 유지). 프롬프트 없이 진행.
            // long을 PlayerPrefs에 직접 저장 불가 → string으로 보관.
            long localTs = 0;
            long.TryParse(PlayerPrefs.GetString(LastSaveTsKey, "0"), out localTs);

            // 계정 전환 안전장치: 로컬 데이터의 소유 uid가 현재 로그인 uid와 다르면(로그아웃 후 다른 계정
            // 로그인 등 잔존 로컬) 로컬유지/충돌 분기를 건너뛰고 무조건 클라우드 적용 — 교차 계정 오염 차단.
            string localOwner = PlayerPrefs.GetString(LocalOwnerKey, "");
            string curUid = AuthManager.Instance != null ? AuthManager.Instance.UserId : "";
            bool sameOwner = string.IsNullOrEmpty(localOwner) || localOwner == curUid;

            // 로컬이 더 새것이거나 동일 ts + 같은 계정 → 덮어쓰기 거부(이 기기가 앞섬 또는 동일).
            // 동일 ts를 로컬 우선으로 처리: 오프라인 변경은 로컬 파일을 앞서게 하지만 LastSaveTs는 마지막
            // 푸시 값(=클라우드 ts)에 머문다. `>`만 보면 동일 ts에서 클라우드가 오프라인 진행을 덮어써 유실됨.
            if (sameOwner && localTs > 0 && data.lastSaveTimestamp > 0 && localTs >= data.lastSaveTimestamp)
            {
                Debug.LogWarning(
                    "[CloudSave] 로컬 데이터(" + localTs + ")가 클라우드(" + data.lastSaveTimestamp
                    + ")보다 새것. 덮어쓰기 거부.");
                return true;
            }

            // 충돌 감지: 같은 계정 + 클라우드가 내 마지막 푸시보다 새것 + 로컬에 의미있는 진행 → 다른 기기 가능성.
            // 구독자(SaveConflictUI)가 있으면 적용을 보류하고 사용자에게 선택을 묻는다.
            // 구독자 없으면(데드락 방지) 기존 동작(last-write-wins)으로 클라우드 적용.
            if (sameOwner && ConflictDetected != null && data.lastSaveTimestamp > localTs && HasMeaningfulLocalProgress())
            {
                pendingCloudData = data;
                ConflictDetected.Invoke(new SaveConflictInfo
                {
                    local = BuildLocalSummary(localTs),
                    cloud = BuildCloudSummary(data)
                });
                return false;
            }

            ApplyResolved(data);
            return true;
        }

        // 실제 적용 — 충돌 없거나 사용자가 "클라우드 사용" 선택 시 호출.
        private void ApplyResolved(GameSaveData data, bool forceReplace = false)
        {
            PlayerPrefs.SetInt("player_level", data.playerLevel);
            PlayerPrefs.SetInt("player_xp", data.playerXp);
            PlayerPrefs.SetInt("player_candies", data.candies);
            PlayerPrefs.SetInt("player_coins", data.coins);
            // gems: wallet 단일 소스. data.gems가 음수(옛 클라우드)면 로컬 유지.
            // CashShopManager가 wallet에 직접 반영(여러 UI 이벤트 동기 발화 보장).
            if (data.gems >= 0 && CashShopManager.Instance != null)
            {
                int diff = data.gems - CashShopManager.Instance.Gems;
                if (diff != 0) CashShopManager.Instance.AddGems(diff);
            }
            PlayerPrefs.SetString(SaveScope.PrefsKey("InsectGame.Equipped"), data.equippedOutfit ?? "");
            PlayerPrefs.SetString(SaveScope.PrefsKey("InsectGame.OwnedOutfits"), data.ownedOutfits ?? "");
            PlayerPrefs.SetString(SaveScope.PrefsKey("InsectGame.UnlockedRegions"),
                data.unlockedRegions ?? "meadow");
            PlayerPrefs.SetString(SaveScope.PrefsKey("InsectGame.DefeatedGuardians"),
                data.defeatedGuardians ?? "");
            // 클라우드 퀘스트 데이터를 현재 계정의 계정별 키에 적용(이후 ReloadFromDisk가 인메모리 갱신).
            PlayerPrefs.SetString(AuthManager.ScopedKey(GameConstants.PrefsKeys.QuestProgress),
                data.questProgress ?? "");
            PlayerPrefs.SetString(AuthManager.ScopedKey(GameConstants.PrefsKeys.QuestCompleted),
                data.questCompleted ?? "");
            PlayerPrefs.SetString(AuthManager.ScopedKey(GameConstants.PrefsKeys.ActiveQuest),
                data.activeQuest ?? "");

            // 캐릭터 외형 — 옛 클라우드 문서엔 없을 수 있어 sentinel(-1)이면 로컬 유지(초기화 방지).
            if (data.charCreated == 1) PlayerPrefs.SetInt(SaveScope.PrefsKey("InsectGame.Character.Created"), 1);
            if (!string.IsNullOrEmpty(data.charName)) PlayerPrefs.SetString(SaveScope.PrefsKey("InsectGame.Character.Name"), data.charName);
            if (data.charSkin >= 0) PlayerPrefs.SetInt(SaveScope.PrefsKey("InsectGame.Character.SkinColor"), data.charSkin);
            if (data.charHair >= 0) PlayerPrefs.SetInt(SaveScope.PrefsKey("InsectGame.Character.HairStyle"), data.charHair);
            if (data.charGender >= 0) PlayerPrefs.SetInt(SaveScope.PrefsKey("InsectGame.Character.Gender"), data.charGender);
            if (data.charHairColor >= 0) PlayerPrefs.SetInt(SaveScope.PrefsKey("InsectGame.Character.HairColor"), data.charHairColor);
            if (data.charFace >= 0) PlayerPrefs.SetInt(SaveScope.PrefsKey("InsectGame.Character.FaceType"), data.charFace);
            if (data.charOutfit >= 0) PlayerPrefs.SetInt(SaveScope.PrefsKey("InsectGame.Character.OutfitPreset"), data.charOutfit);
            PlayerPrefs.Save();

            // 실제 진행도 시스템(파일 기반)에 반영 — PlayerPrefs 미러만으로는 게임플레이에 안 잡힘.
            // (PlayerPrefs SetInt는 WorldChannelManager 등 레거시 리더용 미러로 유지.)
            if (progressController != null) progressController.ApplyCloudProgress(data.playerLevel, data.playerXp);
            if (candyInventory != null) candyInventory.SetCandies(data.candies);
            if (currencyWallet != null) currencyWallet.SetCoins(data.coins);

            // 부트 로드 시엔 빈 필드를 로컬 보존(옛 클라우드 문서 누락 대응). 명시적 충돌 해소("클라우드 사용")
            // 시엔 forceReplace=true로 빈 클라우드 컬렉션이 로컬을 깨끗이 치환 — '클라우드 레벨 + 로컬 곤충'
            // 혼합 세이브 방지.
            ApplyCloudFile(GameConstants.SaveFiles.PlayerInsects, data.ownedInsects, forceReplace);
            ApplyCloudFile(GameConstants.SaveFiles.BattleTeam, data.battleTeam, forceReplace);
            ApplyCloudFile(GameConstants.SaveFiles.DexSave, data.dexData, forceReplace);
            ApplyCloudFile(GameConstants.SaveFiles.PlayerItems, data.playerItems, forceReplace);
            // 스토리 진행 파일 — dexData/playerItems와 동형. StoryDirector.ReloadFromDisk가 아래 reloadables 순회에서 인메모리 갱신.
            ApplyCloudFile(GameConstants.SaveFiles.StoryProgress, data.storyProgress, forceReplace);

            // 파일/PlayerPrefs 갱신 후 인메모리 캐시 리로드 — 곤충/팀/도감/지역/의상 등이
            // 다른 기기 첫 로그인에서도 즉시 반영(앱 재시작 불필요).
            for (int i = 0; i < reloadables.Count; i++)
            {
                if (reloadables[i] != null) reloadables[i].ReloadFromDisk();
            }

            // 이 클라우드 ts로 동기화 완료 표시 — 다음 로그인 시 동일/구 ts면 재충돌 프롬프트 안 함.
            // 로컬 소유자도 현재 계정으로 갱신(클라우드를 적용했으므로 이 계정 데이터가 됨).
            PlayerPrefs.SetString(LastSaveTsKey, data.lastSaveTimestamp.ToString());
            PlayerPrefs.SetString(LocalOwnerKey,
                AuthManager.Instance != null ? (AuthManager.Instance.UserId ?? "") : "");
            PlayerPrefs.Save();
        }

        // ── 충돌 해결 (사용자 선택) ──

        public event Action<SaveConflictInfo> ConflictDetected;
        private GameSaveData pendingCloudData;

        /// <summary>충돌 UI에서 사용자가 선택한 결과를 적용. useCloud=true→클라우드로 덮어씀,
        /// false→로컬 유지(클라우드에 즉시 업로드해 재충돌 방지). 어느 쪽이든 LoadCompleted(true)로 게임 진행.</summary>
        public void ResolveConflict(bool useCloud)
        {
            GameSaveData data = pendingCloudData;
            pendingCloudData = null;
            if (data == null) return;

            if (useCloud)
            {
                // 명시적 "클라우드 사용" — 빈 클라우드 컬렉션도 로컬을 치환(혼합 세이브 방지).
                ApplyResolved(data, forceReplace: true);
            }
            else
            {
                // 로컬 유지 → 클라우드를 로컬로 덮어쓰기 위해 즉시 업로드(다음 로그인 재프롬프트 방지).
                SaveToCloud();
            }
            LoadCompleted?.Invoke(true);
        }

        // 로컬에 "지킬 가치가 있는" 진행이 있는지 — 레벨>1 또는 곤충 보유. 신규 설치엔 프롬프트 안 띄움.
        private bool HasMeaningfulLocalProgress()
        {
            int level = progressController != null ? progressController.Level : PlayerPrefs.GetInt("player_level", 1);
            if (level > 1) return true;
            return CountInsects(LoadLocalFile(GameConstants.SaveFiles.PlayerInsects)) > 0;
        }

        private SaveSummary BuildLocalSummary(long localTs)
        {
            return new SaveSummary
            {
                level = progressController != null ? progressController.Level : PlayerPrefs.GetInt("player_level", 1),
                candies = candyInventory != null ? candyInventory.Candies : PlayerPrefs.GetInt("player_candies", 0),
                coins = currencyWallet != null ? currencyWallet.Coins : PlayerPrefs.GetInt("player_coins", 0),
                insectCount = CountInsects(LoadLocalFile(GameConstants.SaveFiles.PlayerInsects)),
                lastSaveUnix = localTs
            };
        }

        private SaveSummary BuildCloudSummary(GameSaveData data)
        {
            return new SaveSummary
            {
                level = data.playerLevel,
                candies = data.candies,
                coins = data.coins,
                insectCount = CountInsects(data.ownedInsects),
                lastSaveUnix = data.lastSaveTimestamp
            };
        }

        private static int CountInsects(string ownedInsectsJson)
        {
            if (string.IsNullOrEmpty(ownedInsectsJson)) return 0;
            try
            {
                PlayerInsectCollectionSave save =
                    JsonUtility.FromJson<PlayerInsectCollectionSave>(ownedInsectsJson);
                return save != null && save.insects != null ? save.insects.Count : 0;
            }
            catch
            {
                return 0;
            }
        }

        // ── Firestore JSON 변환 ──

        private string ConvertToFirestoreDocument(GameSaveData data)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("{\"fields\":{");
            AppendStringField(sb, "displayName", data.displayName);
            sb.Append(","); AppendIntField(sb, "playerLevel", data.playerLevel);
            sb.Append(","); AppendIntField(sb, "playerXp", data.playerXp);
            sb.Append(","); AppendIntField(sb, "candies", data.candies);
            sb.Append(","); AppendIntField(sb, "coins", data.coins);
            sb.Append(","); AppendIntField(sb, "gems", data.gems);
            sb.Append(","); AppendStringField(sb, "ownedInsects", data.ownedInsects);
            sb.Append(","); AppendStringField(sb, "battleTeam", data.battleTeam);
            sb.Append(","); AppendStringField(sb, "dexData", data.dexData);
            sb.Append(","); AppendStringField(sb, "playerItems", data.playerItems);
            sb.Append(","); AppendStringField(sb, "equippedOutfit", data.equippedOutfit);
            sb.Append(","); AppendStringField(sb, "ownedOutfits", data.ownedOutfits);
            sb.Append(","); AppendStringField(sb, "unlockedRegions", data.unlockedRegions);
            sb.Append(","); AppendStringField(sb, "defeatedGuardians", data.defeatedGuardians);
            sb.Append(","); AppendStringField(sb, "questProgress", data.questProgress);
            sb.Append(","); AppendStringField(sb, "questCompleted", data.questCompleted);
            sb.Append(","); AppendStringField(sb, "activeQuest", data.activeQuest);
            sb.Append(","); AppendStringField(sb, "storyProgress", data.storyProgress);
            sb.Append(","); AppendIntField(sb, "charCreated", data.charCreated);
            sb.Append(","); AppendStringField(sb, "charName", data.charName);
            sb.Append(","); AppendIntField(sb, "charSkin", data.charSkin);
            sb.Append(","); AppendIntField(sb, "charHair", data.charHair);
            sb.Append(","); AppendIntField(sb, "charGender", data.charGender);
            sb.Append(","); AppendIntField(sb, "charHairColor", data.charHairColor);
            sb.Append(","); AppendIntField(sb, "charFace", data.charFace);
            sb.Append(","); AppendIntField(sb, "charOutfit", data.charOutfit);
            sb.Append(","); AppendIntField(sb, "lastSaveTimestamp",
                (int)data.lastSaveTimestamp);
            sb.Append("}}");
            return sb.ToString();
        }

        private GameSaveData ParseFirestoreDocument(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            if (!json.Contains("\"fields\"")) return null;

            GameSaveData data = new GameSaveData();

            data.displayName = ExtractStringValue(json, "displayName");
            data.playerLevel = ExtractIntValue(json, "playerLevel");
            data.playerXp = ExtractIntValue(json, "playerXp");
            data.candies = ExtractIntValue(json, "candies");
            data.coins = ExtractIntValue(json, "coins");
            // gems는 신규 필드 — 옛 클라우드 데이터에 없으면 -1 sentinel.
            // ApplySaveData에서 음수면 PlayerPrefs 덮어쓰기 스킵(기존 로컬 보석 보존).
            data.gems = ExtractIntValueOrDefault(json, "gems", -1);
            data.ownedInsects = ExtractStringValue(json, "ownedInsects");
            data.battleTeam = ExtractStringValue(json, "battleTeam");
            data.dexData = ExtractStringValue(json, "dexData");
            data.playerItems = ExtractStringValue(json, "playerItems");
            data.equippedOutfit = ExtractStringValue(json, "equippedOutfit");
            data.ownedOutfits = ExtractStringValue(json, "ownedOutfits");
            data.unlockedRegions = ExtractStringValue(json, "unlockedRegions");
            data.defeatedGuardians = ExtractStringValue(json, "defeatedGuardians");
            data.questProgress = ExtractStringValue(json, "questProgress");
            data.questCompleted = ExtractStringValue(json, "questCompleted");
            data.activeQuest = ExtractStringValue(json, "activeQuest");
            data.storyProgress = ExtractStringValue(json, "storyProgress");
            // 캐릭터 외형 — 옛 문서엔 없을 수 있어 sentinel(-1)로 받아 ApplySaveData에서 로컬 보존.
            data.charCreated = ExtractIntValueOrDefault(json, "charCreated", 0);
            data.charName = ExtractStringValue(json, "charName");
            data.charSkin = ExtractIntValueOrDefault(json, "charSkin", -1);
            data.charHair = ExtractIntValueOrDefault(json, "charHair", -1);
            data.charGender = ExtractIntValueOrDefault(json, "charGender", -1);
            data.charHairColor = ExtractIntValueOrDefault(json, "charHairColor", -1);
            data.charFace = ExtractIntValueOrDefault(json, "charFace", -1);
            data.charOutfit = ExtractIntValueOrDefault(json, "charOutfit", -1);
            data.lastSaveTimestamp = ExtractIntValue(json, "lastSaveTimestamp");

            return data;
        }

        // ── Firestore 필드 헬퍼 ──

        private void AppendStringField(System.Text.StringBuilder sb,
            string key, string value)
        {
            sb.Append("\"");
            sb.Append(key);
            sb.Append("\":{\"stringValue\":\"");
            sb.Append(EscapeJson(value ?? ""));
            sb.Append("\"}");
        }

        private void AppendIntField(System.Text.StringBuilder sb,
            string key, int value)
        {
            sb.Append("\"");
            sb.Append(key);
            sb.Append("\":{\"integerValue\":\"");
            sb.Append(value);
            sb.Append("\"}");
        }

        private string EscapeJson(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// Firestore 응답에서 stringValue를 추출하는 간이 파서.
        /// {"fieldName":{"stringValue":"..."}} 형식에서 값을 꺼냅니다.
        /// </summary>
        private string ExtractStringValue(string json, string fieldName)
        {
            string marker = "\"" + fieldName + "\":{\"stringValue\":\"";
            int start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return "";
            start += marker.Length;
            int end = json.IndexOf("\"}", start, StringComparison.Ordinal);
            if (end < 0) return "";

            // 이스케이프된 따옴표를 건너뛰기
            while (end > 0 && json[end - 1] == '\\')
            {
                end = json.IndexOf("\"}", end + 1, StringComparison.Ordinal);
                if (end < 0) return "";
            }

            return json.Substring(start, end - start)
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t");
        }

        /// <summary>
        /// Firestore 응답에서 integerValue를 추출하는 간이 파서.
        /// {"fieldName":{"integerValue":"123"}} 형식에서 값을 꺼냅니다.
        /// </summary>
        private int ExtractIntValue(string json, string fieldName)
        {
            return ExtractIntValueOrDefault(json, fieldName, 0);
        }

        private int ExtractIntValueOrDefault(string json, string fieldName, int defaultValue)
        {
            string marker = "\"" + fieldName + "\":{\"integerValue\":\"";
            int start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return defaultValue;
            start += marker.Length;
            int end = json.IndexOf("\"", start, StringComparison.Ordinal);
            if (end < 0) return defaultValue;

            string numStr = json.Substring(start, end - start);
            if (int.TryParse(numStr, out int result))
                return result;
            return defaultValue;
        }

        // ── 로컬 파일 IO ──

        private string LoadLocalFile(string fileName)
        {
            string path = SaveScope.FilePath(fileName);
            if (!File.Exists(path)) return "";
            return File.ReadAllText(path);
        }

        private void SaveLocalFile(string fileName, string content)
        {
            string path = SaveScope.FilePath(fileName);
            AtomicFileWriter.WriteAllText(path, content);
        }

        // 내용 있으면 저장, 비었고 forceReplace면 삭제(깨끗한 치환). 부트 로드는 forceReplace=false라 보존.
        private void ApplyCloudFile(string fileName, string content, bool forceReplace)
        {
            if (!string.IsNullOrEmpty(content)) SaveLocalFile(fileName, content);
            else if (forceReplace) DeleteLocalFile(fileName);
        }

        private void DeleteLocalFile(string fileName)
        {
            try
            {
                string path = SaveScope.FilePath(fileName);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CloudSave] 로컬 파일 삭제 실패(" + fileName + "): " + e.Message);
            }
        }
    }

    // ── 클라우드 저장 데이터 모델 ──

    [Serializable]
    public class GameSaveData
    {
        public string displayName;
        public int playerLevel;
        public int playerXp;
        public int candies;
        public int coins;
        public int gems;
        public string ownedInsects;
        public string battleTeam;
        public string dexData;
        public string playerItems;
        public string equippedOutfit;
        public string ownedOutfits;
        public string unlockedRegions;
        public string defeatedGuardians;
        public string questProgress;
        public string questCompleted;
        public string activeQuest;
        // 스토리 진행(seenBeatIds) — story_progress.json 내용. 파일 기반이라 수집/적용은 dexData와 동형.
        public string storyProgress;
        // 캐릭터 외형 — int는 -1 sentinel(옛 클라우드 문서 누락 시 로컬 유지), charCreated만 0 기본.
        public int charCreated;
        public string charName;
        public int charSkin = -1;
        public int charHair = -1;
        public int charGender = -1;
        public int charHairColor = -1;
        public int charFace = -1;
        public int charOutfit = -1;
        public long lastSaveTimestamp;
    }

    // ── 동기화 충돌 요약 (UI 표시용) ──

    public class SaveSummary
    {
        public int level;
        public int insectCount;
        public int candies;
        public int coins;
        public long lastSaveUnix;
    }

    public class SaveConflictInfo
    {
        public SaveSummary local;
        public SaveSummary cloud;
    }
}
