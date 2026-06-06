using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace InsectGame.Core
{
    [Serializable]
    public class WorldInstance
    {
        public string worldId;
        public string displayName;
        public int playerCount;
        public int maxPlayers;
        public List<WorldPlayer> players;
    }

    [Serializable]
    public class WorldPlayer
    {
        public string uid;
        public string displayName;
        public int level;
        public long joinedAt;
    }

    public class WorldChannelManager : MonoBehaviour
    {
        public static WorldChannelManager Instance { get; private set; }

        public const int MaxPlayersPerWorld = 10;

        public WorldInstance CurrentWorld { get; private set; }
        public List<WorldInstance> AvailableWorlds { get; private set; }
        public bool IsJoined => CurrentWorld != null;

        public event Action WorldJoined;
        public event Action WorldLeft;
        public event Action<List<WorldInstance>> WorldListUpdated;
        public event Action<string> ErrorOccurred;

        private float refreshTimer;
        private const float RefreshInterval = 30f;
        private const float PresenceInterval = 60f;
        private float presenceTimer;

        private const string WorldsCollection = "worlds";

        // ── Lifecycle ──

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            AvailableWorlds = new List<WorldInstance>();
        }

        private static bool IsFirebaseReady()
        {
            return !string.IsNullOrEmpty(FirebaseConfig.ApiKey)
                && FirebaseConfig.ApiKey != "YOUR_FIREBASE_API_KEY"
                && AuthManager.Instance != null
                && AuthManager.Instance.IsLoggedIn
                && !AuthManager.Instance.IsMasterAccount;
        }

        // 코루틴 중복 실행 차단 — 네트워크 지연으로 코루틴이 RefreshInterval(30s)/PresenceInterval 초과 시
        // Update가 매 프레임 새 코루틴 시작 → CurrentWorld 동시 접근/PATCH race. 진행 중 플래그로 차단.
        private bool isRefreshingWorld;
        private bool isUpdatingPresence;

        private void Update()
        {
            if (!IsFirebaseReady()) return;
            if (CurrentWorld == null) return;

            refreshTimer += Time.deltaTime;
            if (refreshTimer >= RefreshInterval && !isRefreshingWorld)
            {
                refreshTimer = 0f;
                StartCoroutine(RefreshCurrentWorldGuarded());
            }

            presenceTimer += Time.deltaTime;
            if (presenceTimer >= PresenceInterval && !isUpdatingPresence)
            {
                presenceTimer = 0f;
                StartCoroutine(UpdatePresenceGuarded());
            }
        }

        private System.Collections.IEnumerator RefreshCurrentWorldGuarded()
        {
            isRefreshingWorld = true;
            yield return RefreshCurrentWorldCoroutine();
            isRefreshingWorld = false;
        }

        private System.Collections.IEnumerator UpdatePresenceGuarded()
        {
            isUpdatingPresence = true;
            yield return UpdatePresenceCoroutine();
            isUpdatingPresence = false;
        }

        private void OnApplicationQuit()
        {
            BestEffortLeaveWorld();
        }

        // 모바일 백그라운드 진입 시에도 best-effort 퇴장 (앱 강제 종료 대비)
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) BestEffortLeaveWorld();
        }

        // OnApplicationQuit은 비동기 코루틴 완료 보장 안 됨 → 동기 polling으로 best-effort.
        // Fire-and-forget: 메인 스레드 블록 없음 (이전엔 Thread.Sleep 폴링으로 최대 500ms 블록 → Android ANR 위험).
        // 단점: 일부 케이스에서 요청 송신 전 앱 종료 → currentPlayers 즉시 감소 안 됨.
        // 보완: Firestore 측 lastSeenAt 기반 서버 cleanup으로 결국 정리됨 (UX 우선 trade-off).
        private void BestEffortLeaveWorld()
        {
            if (CurrentWorld == null || AuthManager.Instance == null) return;

            string url = $"{FirebaseConfig.FirestoreBaseUrl}/{WorldsCollection}/{CurrentWorld.worldId}";
            CurrentWorld.players.RemoveAll(p => p.uid == AuthManager.Instance.UserId);
            CurrentWorld.playerCount = CurrentWorld.players.Count;
            string json = BuildWorldDocument(CurrentWorld.worldId, CurrentWorld.displayName, CurrentWorld.maxPlayers, CurrentWorld.players);

            try
            {
                UnityWebRequest req = new UnityWebRequest(url, "PATCH");
                byte[] body = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {AuthManager.Instance.IdToken}");

                // SendWebRequest는 비동기 — 호출만 하고 결과 폴링 안 함. Dispose는 OS가 정리.
                req.SendWebRequest();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WorldChannel] best-effort leave 실패: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ──

        public void RefreshWorldList()
        {
            if (!IsFirebaseReady()) return;
            StartCoroutine(FetchWorldListCoroutine());
        }

        public void AutoJoinWorld()
        {
            if (!IsFirebaseReady()) return;
            StartCoroutine(AutoJoinCoroutine());
        }

        public void JoinWorld(string worldId)
        {
            if (!IsFirebaseReady()) return;
            StartCoroutine(JoinWorldCoroutine(worldId));
        }

        public void LeaveWorld()
        {
            if (!IsFirebaseReady()) return;
            StartCoroutine(LeaveWorldCoroutine());
        }

        // ── Fetch World List ──

        private IEnumerator FetchWorldListCoroutine()
        {
            string url = $"{FirebaseConfig.FirestoreBaseUrl}/{WorldsCollection}";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {AuthManager.Instance.IdToken}");
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    AvailableWorlds = ParseWorldList(req.downloadHandler.text);
                    WorldListUpdated?.Invoke(AvailableWorlds);
                }
                else
                {
                    Debug.LogWarning($"[WorldChannel] 월드 목록 조회 실패: {req.error}");
                    ErrorOccurred?.Invoke("월드 목록 조회 실패");
                }
            }
        }

        // ── Auto Join ──

        private IEnumerator AutoJoinCoroutine()
        {
            yield return FetchWorldListCoroutine();

            WorldInstance target = null;
            foreach (var world in AvailableWorlds)
            {
                if (world.playerCount < world.maxPlayers)
                {
                    target = world;
                    break;
                }
            }

            if (target == null)
            {
                yield return CreateNewWorldCoroutine();
                if (AvailableWorlds.Count > 0)
                {
                    target = AvailableWorlds[AvailableWorlds.Count - 1];
                }
            }

            if (target != null)
            {
                yield return JoinWorldCoroutine(target.worldId);
            }
        }

        // ── Create New World ──

        private IEnumerator CreateNewWorldCoroutine()
        {
            int nextNum = AvailableWorlds.Count + 1;
            string worldId = $"world_{nextNum:D3}";
            string displayName = $"\uc6d4\ub4dc {nextNum}";

            string json = BuildWorldDocument(worldId, displayName, MaxPlayersPerWorld, new List<WorldPlayer>());
            string url = $"{FirebaseConfig.FirestoreBaseUrl}/{WorldsCollection}/{worldId}";

            using (UnityWebRequest req = new UnityWebRequest(url, "PATCH"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {AuthManager.Instance.IdToken}");

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    WorldInstance newWorld = new WorldInstance
                    {
                        worldId = worldId,
                        displayName = displayName,
                        playerCount = 0,
                        maxPlayers = MaxPlayersPerWorld,
                        players = new List<WorldPlayer>()
                    };
                    AvailableWorlds.Add(newWorld);
                    Debug.Log($"[WorldChannel] \uc0c8 \uc6d4\ub4dc \uc0dd\uc131: {displayName}");
                }
                else
                {
                    Debug.LogWarning($"[WorldChannel] \uc6d4\ub4dc \uc0dd\uc131 \uc2e4\ud328: {req.error}");
                    ErrorOccurred?.Invoke($"\uc6d4\ub4dc \uc0dd\uc131 \uc2e4\ud328: {req.error}");
                }
            }
        }

        // ── Join World ──

        private IEnumerator JoinWorldCoroutine(string worldId)
        {
            if (CurrentWorld != null)
            {
                yield return LeaveWorldCoroutine();
            }

            string url = $"{FirebaseConfig.FirestoreBaseUrl}/{WorldsCollection}/{worldId}";

            using (UnityWebRequest getReq = UnityWebRequest.Get(url))
            {
                getReq.SetRequestHeader("Authorization", $"Bearer {AuthManager.Instance.IdToken}");
                yield return getReq.SendWebRequest();

                if (getReq.result != UnityWebRequest.Result.Success)
                {
                    ErrorOccurred?.Invoke("\uc6d4\ub4dc \uc870\ud68c \uc2e4\ud328");
                    yield break;
                }

                WorldInstance world = ParseSingleWorld(getReq.downloadHandler.text, worldId);
                if (world == null || world.playerCount >= world.maxPlayers)
                {
                    ErrorOccurred?.Invoke("\uc6d4\ub4dc\uac00 \uac00\ub4dd \ucc3c\uc2b5\ub2c8\ub2e4.");
                    yield break;
                }

                WorldPlayer me = new WorldPlayer
                {
                    uid = AuthManager.Instance.UserId,
                    displayName = AuthManager.Instance.DisplayName,
                    level = PlayerPrefs.GetInt("player_level", 1),
                    joinedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                if (world.players == null) world.players = new List<WorldPlayer>();
                world.players.RemoveAll(p => p.uid == me.uid);
                world.players.Add(me);
                world.playerCount = world.players.Count;

                string updateJson = BuildWorldDocument(worldId, world.displayName, world.maxPlayers, world.players);

                using (UnityWebRequest patchReq = new UnityWebRequest(url, "PATCH"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(updateJson);
                    patchReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    patchReq.downloadHandler = new DownloadHandlerBuffer();
                    patchReq.SetRequestHeader("Content-Type", "application/json");
                    patchReq.SetRequestHeader("Authorization", $"Bearer {AuthManager.Instance.IdToken}");

                    yield return patchReq.SendWebRequest();

                    if (patchReq.result == UnityWebRequest.Result.Success)
                    {
                        CurrentWorld = world;
                        PlayerPrefs.SetString("InsectGame.CurrentWorldId", worldId);
                        PlayerPrefs.Save();
                        refreshTimer = 0f;
                        presenceTimer = 0f;
                        WorldJoined?.Invoke();
                        Debug.Log($"[WorldChannel] {world.displayName} \ucc38\uac00 \uc644\ub8cc ({world.playerCount}/{world.maxPlayers})");
                    }
                    else
                    {
                        ErrorOccurred?.Invoke("\uc6d4\ub4dc \ucc38\uac00 \uc2e4\ud328");
                    }
                }
            }
        }

        // ── Leave World ──

        private IEnumerator LeaveWorldCoroutine()
        {
            if (CurrentWorld == null) yield break;

            string url = $"{FirebaseConfig.FirestoreBaseUrl}/{WorldsCollection}/{CurrentWorld.worldId}";

            CurrentWorld.players.RemoveAll(p => p.uid == AuthManager.Instance.UserId);
            CurrentWorld.playerCount = CurrentWorld.players.Count;

            string json = BuildWorldDocument(CurrentWorld.worldId, CurrentWorld.displayName, CurrentWorld.maxPlayers, CurrentWorld.players);

            using (UnityWebRequest req = new UnityWebRequest(url, "PATCH"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {AuthManager.Instance.IdToken}");

                yield return req.SendWebRequest();
            }

            string leftWorldName = CurrentWorld.displayName;
            CurrentWorld = null;
            PlayerPrefs.DeleteKey("InsectGame.CurrentWorldId");
            WorldLeft?.Invoke();
            Debug.Log($"[WorldChannel] {leftWorldName} \ud1f4\uc7a5 \uc644\ub8cc");
        }

        // ── Refresh Current World (member list update) ──

        private IEnumerator RefreshCurrentWorldCoroutine()
        {
            if (CurrentWorld == null) yield break;

            string url = $"{FirebaseConfig.FirestoreBaseUrl}/{WorldsCollection}/{CurrentWorld.worldId}";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", $"Bearer {AuthManager.Instance.IdToken}");
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    WorldInstance updated = ParseSingleWorld(req.downloadHandler.text, CurrentWorld.worldId);
                    if (updated != null)
                    {
                        CurrentWorld = updated;

                        // 180초 이상 하트비트 없는 플레이어는 비활성으로 간주
                        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        CurrentWorld.players.RemoveAll(p => (now - p.joinedAt) > 180 && p.uid != AuthManager.Instance.UserId);
                        CurrentWorld.playerCount = CurrentWorld.players.Count;
                    }
                }
            }
        }

        // ── Heartbeat (presence update) ──

        private IEnumerator UpdatePresenceCoroutine()
        {
            if (CurrentWorld == null) yield break;

            foreach (var p in CurrentWorld.players)
            {
                if (p.uid == AuthManager.Instance.UserId)
                {
                    p.joinedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    p.level = PlayerPrefs.GetInt("player_level", 1);
                    break;
                }
            }

            string url = $"{FirebaseConfig.FirestoreBaseUrl}/{WorldsCollection}/{CurrentWorld.worldId}";
            string json = BuildWorldDocument(CurrentWorld.worldId, CurrentWorld.displayName, CurrentWorld.maxPlayers, CurrentWorld.players);

            using (UnityWebRequest req = new UnityWebRequest(url, "PATCH"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {AuthManager.Instance.IdToken}");
                yield return req.SendWebRequest();
            }
        }

        // ── Firestore JSON Builder ──

        private string BuildWorldDocument(string worldId, string displayName, int maxPlayers, List<WorldPlayer> players)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"fields\":{");
            sb.Append($"\"worldId\":{{\"stringValue\":\"{EscapeJson(worldId)}\"}},");
            sb.Append($"\"displayName\":{{\"stringValue\":\"{EscapeJson(displayName)}\"}},");
            sb.Append($"\"maxPlayers\":{{\"integerValue\":\"{maxPlayers}\"}},");
            sb.Append($"\"playerCount\":{{\"integerValue\":\"{players.Count}\"}},");

            sb.Append("\"players\":{\"arrayValue\":{\"values\":[");
            for (int i = 0; i < players.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("{\"mapValue\":{\"fields\":{");
                sb.Append($"\"uid\":{{\"stringValue\":\"{EscapeJson(players[i].uid)}\"}},");
                sb.Append($"\"displayName\":{{\"stringValue\":\"{EscapeJson(players[i].displayName)}\"}},");
                sb.Append($"\"level\":{{\"integerValue\":\"{players[i].level}\"}},");
                sb.Append($"\"joinedAt\":{{\"integerValue\":\"{players[i].joinedAt}\"}}");
                sb.Append("}}}");
            }
            sb.Append("]}}");

            sb.Append("}}");
            return sb.ToString();
        }

        private string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        // ── Firestore JSON Parsers ──

        private List<WorldInstance> ParseWorldList(string responseJson)
        {
            var result = new List<WorldInstance>();
            if (string.IsNullOrEmpty(responseJson)) return result;

            // Firestore list response: {"documents":[{...},{...},...]}
            // 각 문서에서 worldId를 추출하여 ParseSingleWorld로 파싱
            int searchStart = 0;
            while (true)
            {
                int docStart = responseJson.IndexOf("\"fields\"", searchStart, StringComparison.Ordinal);
                if (docStart < 0) break;

                // 이 문서의 범위를 찾기: "fields" 앞의 { 부터 매칭되는 } 까지
                int braceStart = responseJson.LastIndexOf('{', docStart);
                if (braceStart < 0) break;

                int braceEnd = FindMatchingBrace(responseJson, braceStart);
                if (braceEnd < 0) break;

                string docJson = responseJson.Substring(braceStart, braceEnd - braceStart + 1);

                string worldId = ExtractStringValue(docJson, "worldId");
                if (!string.IsNullOrEmpty(worldId))
                {
                    WorldInstance world = ParseSingleWorld(docJson, worldId);
                    if (world != null)
                    {
                        result.Add(world);
                    }
                }

                searchStart = braceEnd + 1;
            }

            return result;
        }

        private WorldInstance ParseSingleWorld(string documentJson, string worldId)
        {
            if (string.IsNullOrEmpty(documentJson)) return null;

            WorldInstance world = new WorldInstance
            {
                worldId = worldId,
                displayName = ExtractStringValue(documentJson, "displayName") ?? worldId,
                maxPlayers = ExtractIntValue(documentJson, "maxPlayers", MaxPlayersPerWorld),
                playerCount = ExtractIntValue(documentJson, "playerCount", 0),
                players = new List<WorldPlayer>()
            };

            // players 배열 파싱
            int playersStart = documentJson.IndexOf("\"players\"", StringComparison.Ordinal);
            if (playersStart >= 0)
            {
                int arrayStart = documentJson.IndexOf("\"values\"", playersStart, StringComparison.Ordinal);
                if (arrayStart >= 0)
                {
                    int bracketStart = documentJson.IndexOf('[', arrayStart);
                    if (bracketStart >= 0)
                    {
                        int bracketEnd = FindMatchingBracket(documentJson, bracketStart);
                        if (bracketEnd >= 0)
                        {
                            string arrayJson = documentJson.Substring(bracketStart, bracketEnd - bracketStart + 1);
                            world.players = ParsePlayerArray(arrayJson);
                            world.playerCount = world.players.Count;
                        }
                    }
                }
            }

            return world;
        }

        private List<WorldPlayer> ParsePlayerArray(string arrayJson)
        {
            var players = new List<WorldPlayer>();
            int searchStart = 0;

            while (true)
            {
                int mapStart = arrayJson.IndexOf("\"mapValue\"", searchStart, StringComparison.Ordinal);
                if (mapStart < 0) break;

                int fieldsStart = arrayJson.IndexOf("\"fields\"", mapStart, StringComparison.Ordinal);
                if (fieldsStart < 0) break;

                int braceStart = arrayJson.IndexOf('{', fieldsStart + 8);
                if (braceStart < 0) break;

                int braceEnd = FindMatchingBrace(arrayJson, braceStart);
                if (braceEnd < 0) break;

                string playerJson = arrayJson.Substring(braceStart, braceEnd - braceStart + 1);

                WorldPlayer player = new WorldPlayer
                {
                    uid = ExtractStringValue(playerJson, "uid") ?? "",
                    displayName = ExtractStringValue(playerJson, "displayName") ?? "",
                    level = ExtractIntValue(playerJson, "level", 1),
                    joinedAt = ExtractLongValue(playerJson, "joinedAt", 0)
                };

                if (!string.IsNullOrEmpty(player.uid))
                {
                    players.Add(player);
                }

                searchStart = braceEnd + 1;
            }

            return players;
        }

        // ── JSON Extraction Helpers ──

        private string ExtractStringValue(string json, string fieldName)
        {
            string pattern = $"\"{fieldName}\":{{\"stringValue\":\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return null;

            int valueStart = idx + pattern.Length;
            int valueEnd = json.IndexOf('"', valueStart);
            if (valueEnd < 0) return null;

            return json.Substring(valueStart, valueEnd - valueStart);
        }

        private int ExtractIntValue(string json, string fieldName, int defaultValue)
        {
            string pattern = $"\"{fieldName}\":{{\"integerValue\":\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return defaultValue;

            int valueStart = idx + pattern.Length;
            int valueEnd = json.IndexOf('"', valueStart);
            if (valueEnd < 0) return defaultValue;

            string valueStr = json.Substring(valueStart, valueEnd - valueStart);
            if (int.TryParse(valueStr, out int result)) return result;
            return defaultValue;
        }

        private long ExtractLongValue(string json, string fieldName, long defaultValue)
        {
            string pattern = $"\"{fieldName}\":{{\"integerValue\":\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return defaultValue;

            int valueStart = idx + pattern.Length;
            int valueEnd = json.IndexOf('"', valueStart);
            if (valueEnd < 0) return defaultValue;

            string valueStr = json.Substring(valueStart, valueEnd - valueStart);
            if (long.TryParse(valueStr, out long result)) return result;
            return defaultValue;
        }

        private int FindMatchingBrace(string json, int openIndex)
        {
            int depth = 0;
            for (int i = openIndex; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') depth--;
                if (depth == 0) return i;
            }
            return -1;
        }

        private int FindMatchingBracket(string json, int openIndex)
        {
            int depth = 0;
            for (int i = openIndex; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']') depth--;
                if (depth == 0) return i;
            }
            return -1;
        }
    }
}
