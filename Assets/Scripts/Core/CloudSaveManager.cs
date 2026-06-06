using System;
using System.Collections;
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

        private float autoSaveTimer;
        private const float AutoSaveInterval = 120f;

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
            return !string.IsNullOrEmpty(FirebaseConfig.ApiKey)
                && FirebaseConfig.ApiKey != "YOUR_FIREBASE_API_KEY"
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
        private const string LastSaveTsKey = "InsectGame.LastSaveTs";

        public void SaveToCloud()
        {
            if (!IsFirebaseConfigured()) return;
            if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn) return;
            if (IsSaving)
            {
                // 진행 중인 저장이 끝난 직후 1회 더 저장 (보석 결제 등 사용자 액션 손실 방지)
                pendingSave = true;
                return;
            }
            StartCoroutine(SaveCoroutine());
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

            GameSaveData data = CollectSaveData();
            string firestoreJson = ConvertToFirestoreDocument(data);

            string url = FirebaseConfig.FirestoreBaseUrl
                + "/users/" + AuthManager.Instance.UserId;

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
                PlayerPrefs.SetString(LastSaveTsKey, data.lastSaveTimestamp.ToString());
                PlayerPrefs.Save();
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

        public void LoadFromCloud()
        {
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
                    ApplySaveData(data);
                    LoadCompleted?.Invoke(true);
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
                playerLevel = PlayerPrefs.GetInt("player_level", 1),
                playerXp = PlayerPrefs.GetInt("player_xp", 0),
                candies = PlayerPrefs.GetInt("player_candies", 0),
                coins = PlayerPrefs.GetInt("player_coins", 0),
                // gems는 PlayerCurrencyWallet 단일 소스. CashShopManager가 wallet 경유 노출.
                gems = CashShopManager.Instance != null ? CashShopManager.Instance.Gems : 0,
                ownedInsects = LoadLocalFile(GameConstants.SaveFiles.PlayerInsects),
                battleTeam = LoadLocalFile(GameConstants.SaveFiles.BattleTeam),
                dexData = LoadLocalFile(GameConstants.SaveFiles.DexSave),
                equippedOutfit = PlayerPrefs.GetString("InsectGame.Equipped", ""),
                ownedOutfits = PlayerPrefs.GetString("InsectGame.OwnedOutfits", ""),
                unlockedRegions = PlayerPrefs.GetString(
                    "InsectGame.UnlockedRegions", "meadow"),
                defeatedGuardians = PlayerPrefs.GetString(
                    "InsectGame.DefeatedGuardians", ""),
                questProgress = PlayerPrefs.GetString(
                    GameConstants.PrefsKeys.QuestProgress, ""),
                questCompleted = PlayerPrefs.GetString(
                    GameConstants.PrefsKeys.QuestCompleted, ""),
                activeQuest = PlayerPrefs.GetString(
                    GameConstants.PrefsKeys.ActiveQuest, ""),
                lastSaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        // ── 데이터 적용 ──

        private void ApplySaveData(GameSaveData data)
        {
            // 타임스탬프 가드: 로컬 lastSaveTimestamp가 클라우드보다 새것이면 덮어쓰기 거부.
            // 현재 LoadFromCloud는 LoginUI 로그인 직후 1회만 호출되지만, 미래 어디서든 LoadFromCloud가
            // 게임 진행 중 호출될 수 있는 케이스(계정 전환, 강제 동기화 등)에 대비.
            // long을 PlayerPrefs에 직접 저장 불가 → string으로 보관.
            long localTs = 0;
            long.TryParse(PlayerPrefs.GetString(LastSaveTsKey, "0"), out localTs);
            if (localTs > 0 && data.lastSaveTimestamp > 0 && localTs > data.lastSaveTimestamp)
            {
                Debug.LogWarning(
                    "[CloudSave] 로컬 데이터(" + localTs + ")가 클라우드(" + data.lastSaveTimestamp
                    + ")보다 새것. 덮어쓰기 거부.");
                return;
            }

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
            PlayerPrefs.SetString("InsectGame.Equipped", data.equippedOutfit ?? "");
            PlayerPrefs.SetString("InsectGame.OwnedOutfits", data.ownedOutfits ?? "");
            PlayerPrefs.SetString("InsectGame.UnlockedRegions",
                data.unlockedRegions ?? "meadow");
            PlayerPrefs.SetString("InsectGame.DefeatedGuardians",
                data.defeatedGuardians ?? "");
            PlayerPrefs.SetString(GameConstants.PrefsKeys.QuestProgress,
                data.questProgress ?? "");
            PlayerPrefs.SetString(GameConstants.PrefsKeys.QuestCompleted,
                data.questCompleted ?? "");
            PlayerPrefs.SetString(GameConstants.PrefsKeys.ActiveQuest,
                data.activeQuest ?? "");
            PlayerPrefs.Save();

            if (!string.IsNullOrEmpty(data.ownedInsects))
                SaveLocalFile(GameConstants.SaveFiles.PlayerInsects, data.ownedInsects);
            if (!string.IsNullOrEmpty(data.battleTeam))
                SaveLocalFile(GameConstants.SaveFiles.BattleTeam, data.battleTeam);
            if (!string.IsNullOrEmpty(data.dexData))
                SaveLocalFile(GameConstants.SaveFiles.DexSave, data.dexData);
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
            sb.Append(","); AppendStringField(sb, "equippedOutfit", data.equippedOutfit);
            sb.Append(","); AppendStringField(sb, "ownedOutfits", data.ownedOutfits);
            sb.Append(","); AppendStringField(sb, "unlockedRegions", data.unlockedRegions);
            sb.Append(","); AppendStringField(sb, "defeatedGuardians", data.defeatedGuardians);
            sb.Append(","); AppendStringField(sb, "questProgress", data.questProgress);
            sb.Append(","); AppendStringField(sb, "questCompleted", data.questCompleted);
            sb.Append(","); AppendStringField(sb, "activeQuest", data.activeQuest);
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
            data.equippedOutfit = ExtractStringValue(json, "equippedOutfit");
            data.ownedOutfits = ExtractStringValue(json, "ownedOutfits");
            data.unlockedRegions = ExtractStringValue(json, "unlockedRegions");
            data.defeatedGuardians = ExtractStringValue(json, "defeatedGuardians");
            data.questProgress = ExtractStringValue(json, "questProgress");
            data.questCompleted = ExtractStringValue(json, "questCompleted");
            data.activeQuest = ExtractStringValue(json, "activeQuest");
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
            string path = Path.Combine(Application.persistentDataPath, fileName);
            if (!File.Exists(path)) return "";
            return File.ReadAllText(path);
        }

        private void SaveLocalFile(string fileName, string content)
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            AtomicFileWriter.WriteAllText(path, content);
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
        public string equippedOutfit;
        public string ownedOutfits;
        public string unlockedRegions;
        public string defeatedGuardians;
        public string questProgress;
        public string questCompleted;
        public string activeQuest;
        public long lastSaveTimestamp;
    }
}
