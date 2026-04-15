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

        // ── 저장 ──

        public void SaveToCloud()
        {
            if (!IsFirebaseConfigured()) return;
            if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn) return;
            if (IsSaving) return;
            StartCoroutine(SaveCoroutine());
        }

        private IEnumerator SaveCoroutine()
        {
            IsSaving = true;

            GameSaveData data = CollectSaveData();
            string firestoreJson = ConvertToFirestoreDocument(data);

            string url = FirebaseConfig.FirestoreBaseUrl
                + "/users/" + AuthManager.Instance.UserId;

            using (UnityWebRequest req = new UnityWebRequest(url, "PATCH"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(firestoreJson);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization",
                    "Bearer " + AuthManager.Instance.IdToken);

                yield return req.SendWebRequest();

                IsSaving = false;
                if (req.result == UnityWebRequest.Result.Success)
                {
                    LastError = null;
                    SaveCompleted?.Invoke();
                }
                else
                {
                    LastError = req.error;
                    Debug.LogWarning("[CloudSave] Save failed: " + req.error);
                }
            }
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
            IsLoading = true;

            string url = FirebaseConfig.FirestoreBaseUrl
                + "/users/" + AuthManager.Instance.UserId;

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization",
                    "Bearer " + AuthManager.Instance.IdToken);

                yield return req.SendWebRequest();

                IsLoading = false;
                if (req.result == UnityWebRequest.Result.Success)
                {
                    GameSaveData data = ParseFirestoreDocument(req.downloadHandler.text);
                    if (data != null)
                    {
                        ApplySaveData(data);
                        LoadCompleted?.Invoke(true);
                    }
                    else
                    {
                        LoadCompleted?.Invoke(false);
                    }
                }
                else
                {
                    if (req.responseCode == 404 || req.responseCode == 401 || req.responseCode == 403)
                    {
                        // 404=새유저, 401/403=Firebase 미설정 → 새 유저 취급
                        LoadCompleted?.Invoke(false);
                    }
                    else
                    {
                        LastError = req.error;
                        Debug.LogWarning("[CloudSave] Load failed: " + req.error);
                        LoadCompleted?.Invoke(false); // 에러여도 진행 가능하게
                    }
                }
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
            PlayerPrefs.SetInt("player_level", data.playerLevel);
            PlayerPrefs.SetInt("player_xp", data.playerXp);
            PlayerPrefs.SetInt("player_candies", data.candies);
            PlayerPrefs.SetInt("player_coins", data.coins);
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
            string marker = "\"" + fieldName + "\":{\"integerValue\":\"";
            int start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return 0;
            start += marker.Length;
            int end = json.IndexOf("\"", start, StringComparison.Ordinal);
            if (end < 0) return 0;

            string numStr = json.Substring(start, end - start);
            if (int.TryParse(numStr, out int result))
                return result;
            return 0;
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
            File.WriteAllText(path, content);
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
