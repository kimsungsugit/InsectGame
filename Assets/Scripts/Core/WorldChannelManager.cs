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
        public List<WorldPlayer> players = new List<WorldPlayer>();
    }

    [Serializable]
    public class WorldPlayer
    {
        public string uid;
        public string displayName;
        public int level;
        public float x;
        public float y;
        public float z;
        public float facing;
        public long joinedAtMs;
        public long lastSeenAtMs;
        public bool blocked;

        public Vector3 Position => new Vector3(x, y, z);
    }

    [Serializable]
    public class WorldChatMessage
    {
        public string messageId;
        public string fromUid;
        public string toUid;
        public string displayName;
        public string message;
        public long sentAtMs;
    }

    [Serializable]
    public class WorldInviteSnapshot
    {
        public string inviteId;
        public string fromUid;
        public string displayName;
        public string worldId;
        public string worldName;
        public long createdAtMs;
    }

    [Serializable]
    internal class WorldApiRequest
    {
        public string action;
        public string worldId;
        public string displayName;
        public int level;
        public float x;
        public float y;
        public float z;
        public float facing;
        public string targetUid;
        public string friendUid;
        public string message;
        public string inviteId;
        public bool accept;
    }

    [Serializable]
    internal class WorldApiResponse
    {
        public bool success;
        public string error;
        public string worldId;
        public WorldInstance world;
        public WorldInstance[] worlds;
        public WorldChatMessage[] messages;
        public WorldInviteSnapshot[] invites;
    }

    /// <summary>
    /// Cloud Function 트랜잭션을 사용하는 5인 필드 세션 클라이언트입니다.
    /// 위치/채팅/초대/차단/필드 대전을 하나의 인증 API 경로로 동기화합니다.
    /// </summary>
    public class WorldChannelManager : MonoBehaviour
    {
        public static WorldChannelManager Instance { get; private set; }

        public const int MaxPlayersPerWorld = 5;
        private const float SyncInterval = 1.0f;

        public WorldInstance CurrentWorld { get; private set; }
        public List<WorldInstance> AvailableWorlds { get; private set; } = new List<WorldInstance>();
        public IReadOnlyList<WorldChatMessage> Messages => messages;
        public IReadOnlyList<WorldInviteSnapshot> Invites => invites;
        public bool IsJoined => CurrentWorld != null;
        public bool IsBusy { get; private set; }

        public event Action WorldJoined;
        public event Action WorldLeft;
        public event Action<List<WorldInstance>> WorldListUpdated;
        public event Action<WorldInstance> WorldStateUpdated;
        public event Action<IReadOnlyList<WorldChatMessage>> MessagesUpdated;
        public event Action<IReadOnlyList<WorldInviteSnapshot>> InvitesUpdated;
        public event Action<string> ActionCompleted;
        public event Action<string> ErrorOccurred;

        private readonly List<WorldChatMessage> messages = new List<WorldChatMessage>();
        private readonly List<WorldInviteSnapshot> invites = new List<WorldInviteSnapshot>();
        private PlayerMovement localPlayer;
        private float syncTimer;
        private float lobbyRefreshTimer;
        private bool syncInFlight;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!IsFirebaseReady()) return;
            if (!IsJoined)
            {
                lobbyRefreshTimer += Time.unscaledDeltaTime;
                if (lobbyRefreshTimer >= 5f && !IsBusy)
                {
                    lobbyRefreshTimer = 0f;
                    StartCoroutine(RefreshWorldListRoutine());
                }
                return;
            }
            // syncInFlight뿐 아니라 IsBusy(join/leave/chat 진행 중)도 가드 — leave in-flight 중
            // sync가 시작돼 leave 응답 뒤 stale sync 응답이 월드를 되살리는 재진입을 차단.
            if (syncInFlight || IsBusy) return;
            syncTimer += Time.unscaledDeltaTime;
            if (syncTimer < SyncInterval) return;
            syncTimer = 0f;
            StartCoroutine(SyncWorldRoutine());
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) BestEffortLeaveWorld();
        }

        private void OnApplicationQuit()
        {
            BestEffortLeaveWorld();
        }

        public void RefreshWorldList()
        {
            if (!CanStartRequest()) return;
            StartCoroutine(RefreshWorldListRoutine());
        }

        public void AutoJoinWorld()
        {
            if (!CanStartRequest()) return;
            StartCoroutine(JoinWorldRoutine(string.Empty));
        }

        public void JoinWorld(string worldId)
        {
            if (!CanStartRequest() || string.IsNullOrWhiteSpace(worldId)) return;
            StartCoroutine(JoinWorldRoutine(worldId.Trim()));
        }

        public void LeaveWorld()
        {
            if (!CanStartRequest() || CurrentWorld == null) return;
            StartCoroutine(LeaveWorldRoutine());
        }

        public void SendPrivateChat(string targetUid, string message)
        {
            if (!CanStartRequest() || CurrentWorld == null) return;
            string text = (message ?? string.Empty).Trim();
            if (text.Length == 0) return;
            StartCoroutine(MutationRoutine(new WorldApiRequest
            {
                action = "sendWorldChat",
                targetUid = targetUid,
                message = text.Length > 80 ? text.Substring(0, 80) : text,
            }, "메시지를 보냈습니다."));
        }

        public void ChallengePlayer(string targetUid)
        {
            if (!CanStartRequest() || CurrentWorld == null) return;
            StartCoroutine(MutationRoutine(new WorldApiRequest
            {
                action = "challengeWorldPlayer", targetUid = targetUid,
            }, "대전 신청을 보냈습니다."));
        }

        public void InviteFriend(string friendUid)
        {
            if (!CanStartRequest() || CurrentWorld == null) return;
            StartCoroutine(MutationRoutine(new WorldApiRequest
            {
                action = "inviteFriendToWorld", friendUid = friendUid,
            }, "같은 필드로 초대했습니다."));
        }

        public void RespondInvite(string inviteId, bool accept)
        {
            if (!CanStartRequest()) return;
            StartCoroutine(RespondInviteRoutine(inviteId, accept));
        }

        public void BlockPlayer(string targetUid)
        {
            if (!CanStartRequest()) return;
            StartCoroutine(MutationRoutine(new WorldApiRequest
            {
                action = "blockUser", targetUid = targetUid,
            }, "사용자를 차단했습니다."));
        }

        public void UnblockPlayer(string targetUid)
        {
            if (!CanStartRequest()) return;
            StartCoroutine(MutationRoutine(new WorldApiRequest
            {
                action = "unblockUser", targetUid = targetUid,
            }, "차단을 해제했습니다."));
        }

        private IEnumerator RefreshWorldListRoutine()
        {
            IsBusy = true;
            try
            {
                WorldApiResponse response = null;
                yield return SendRequest(new WorldApiRequest { action = "listWorlds" }, value => response = value);
                if (response != null && response.success)
                {
                    AvailableWorlds = response.worlds != null
                        ? new List<WorldInstance>(response.worlds)
                        : new List<WorldInstance>();
                    ApplyInvites(response.invites);
                    WorldListUpdated?.Invoke(AvailableWorlds);
                }
            }
            finally { IsBusy = false; } // 이벤트 핸들러 예외/코루틴 중단 시에도 반드시 복구
        }

        private IEnumerator JoinWorldRoutine(string worldId)
        {
            IsBusy = true;
            try
            {
                CacheLocalPlayer();
                Vector3 position = localPlayer != null ? localPlayer.transform.position : Vector3.zero;
                WorldApiResponse response = null;
                yield return SendRequest(new WorldApiRequest
                {
                    action = "joinWorld",
                    worldId = worldId,
                    displayName = AuthManager.Instance.DisplayName,
                    level = PlayerPrefs.GetInt("player_level", 1),
                    x = position.x,
                    y = position.y,
                    z = position.z,
                    facing = localPlayer != null ? localPlayer.transform.eulerAngles.y : 0f,
                }, value => response = value);
                if (response != null && response.success && response.world != null)
                {
                    CurrentWorld = response.world;
                    PlayerPrefs.SetString("InsectGame.CurrentWorldId", CurrentWorld.worldId);
                    PlayerPrefs.Save();
                    ApplyRealtimeResponse(response);
                    syncTimer = 0f;
                    WorldJoined?.Invoke();
                }
            }
            finally { IsBusy = false; }
        }

        private IEnumerator LeaveWorldRoutine()
        {
            IsBusy = true;
            try
            {
                string worldId = CurrentWorld.worldId;
                WorldApiResponse response = null;
                yield return SendRequest(new WorldApiRequest { action = "leaveWorld", worldId = worldId },
                    value => response = value);
                if (response != null && response.success) ClearWorldState();
            }
            finally { IsBusy = false; }
        }

        private IEnumerator SyncWorldRoutine()
        {
            if (CurrentWorld == null) yield break;
            syncInFlight = true;
            try
            {
                CacheLocalPlayer();
                Vector3 position = localPlayer != null ? localPlayer.transform.position : Vector3.zero;
                WorldApiResponse response = null;
                yield return SendRequest(new WorldApiRequest
                {
                    action = "syncWorld",
                    worldId = CurrentWorld.worldId,
                    level = PlayerPrefs.GetInt("player_level", 1),
                    x = position.x,
                    y = position.y,
                    z = position.z,
                    facing = localPlayer != null ? localPlayer.transform.eulerAngles.y : 0f,
                }, value => response = value, false);
                if (response != null && response.success)
                {
                    // sync in-flight 도중 leave/clear가 완료(CurrentWorld=null)됐으면 stale 응답으로
                    // 월드를 되살리지 않는다 — '나가기 눌렀는데 다시 입장' 재진입 차단. (finally가 플래그 복구)
                    if (CurrentWorld == null) yield break;
                    if (response.world == null) ClearWorldState();
                    else ApplyRealtimeResponse(response);
                }
            }
            finally { syncInFlight = false; }
        }

        private IEnumerator MutationRoutine(WorldApiRequest request, string successMessage)
        {
            IsBusy = true;
            try
            {
                WorldApiResponse response = null;
                yield return SendRequest(request, value => response = value);
                if (response != null && response.success)
                {
                    ActionCompleted?.Invoke(successMessage);
                    syncTimer = SyncInterval;
                    SocialPvpManager.Instance?.RefreshAll();
                }
            }
            finally { IsBusy = false; }
        }

        private IEnumerator RespondInviteRoutine(string inviteId, bool accept)
        {
            IsBusy = true;
            WorldApiResponse response = null;
            string joinWorldId = null;
            try
            {
                yield return SendRequest(new WorldApiRequest
                {
                    action = "respondWorldInvite", inviteId = inviteId, accept = accept,
                }, value => response = value);
                if (response != null && response.success)
                {
                    if (accept && !string.IsNullOrEmpty(response.worldId)) joinWorldId = response.worldId;
                    else syncTimer = SyncInterval;
                }
            }
            finally { IsBusy = false; } // JoinWorldRoutine이 자체 IsBusy를 다시 잡도록 먼저 복구
            if (!string.IsNullOrEmpty(joinWorldId))
                yield return JoinWorldRoutine(joinWorldId);
        }

        private void ApplyRealtimeResponse(WorldApiResponse response)
        {
            CurrentWorld = response.world;
            messages.Clear();
            if (response.messages != null) messages.AddRange(response.messages);
            invites.Clear();
            if (response.invites != null) invites.AddRange(response.invites);
            WorldStateUpdated?.Invoke(CurrentWorld);
            MessagesUpdated?.Invoke(messages);
            InvitesUpdated?.Invoke(invites);
        }

        private void ApplyInvites(WorldInviteSnapshot[] updated)
        {
            invites.Clear();
            if (updated != null) invites.AddRange(updated);
            InvitesUpdated?.Invoke(invites);
        }

        private void ClearWorldState()
        {
            CurrentWorld = null;
            messages.Clear();
            invites.Clear(); // 퇴장 후 이전 월드 초대 잔존 표시 차단
            PlayerPrefs.DeleteKey("InsectGame.CurrentWorldId");
            WorldLeft?.Invoke();
            InvitesUpdated?.Invoke(invites);
        }

        private void CacheLocalPlayer()
        {
            if (localPlayer == null) localPlayer = FindFirstObjectByType<PlayerMovement>();
        }

        private bool CanStartRequest()
        {
            if (IsBusy) return false;
            if (IsFirebaseReady()) return true;
            ErrorOccurred?.Invoke("온라인 필드 서버가 준비되지 않았거나 로그인이 필요합니다.");
            return false;
        }

        private static bool IsFirebaseReady()
        {
            return FirebaseConfig.IsSocialPvpConfigured
                && AuthManager.Instance != null
                && AuthManager.Instance.IsLoggedIn
                && !AuthManager.Instance.IsMasterAccount
                && !string.IsNullOrEmpty(AuthManager.Instance.IdToken);
        }

        private IEnumerator SendRequest(WorldApiRequest payload, Action<WorldApiResponse> onComplete,
            bool reportError = true, bool allowRetry = true)
        {
            string json = JsonUtility.ToJson(payload);
            using (UnityWebRequest request = new UnityWebRequest(FirebaseConfig.SocialPvpApiUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + AuthManager.Instance.IdToken);
                // 무한 대기 차단 — half-open 연결/서버 무응답 시 SendWebRequest가 영원히 yield하면
                // IsBusy/syncInFlight가 영구 true로 고정돼 모든 월드 기능(나가기/새로고침/동기/채팅)이 소프트락.
                request.timeout = 12;
                yield return request.SendWebRequest();

                if (request.responseCode == 401 && allowRetry)
                {
                    bool refreshed = false;
                    yield return AuthManager.Instance.TryRefreshTokenForRetry(value => refreshed = value);
                    if (refreshed)
                    {
                        yield return SendRequest(payload, onComplete, reportError, false);
                        yield break;
                    }
                }

                WorldApiResponse response = null;
                if (!string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    try { response = JsonUtility.FromJson<WorldApiResponse>(request.downloadHandler.text); }
                    catch (Exception e) { Debug.LogWarning("[WorldChannel] 응답 파싱 실패: " + e.Message); }
                }
                if (reportError && (request.result != UnityWebRequest.Result.Success
                    || response == null || !response.success))
                {
                    string error = response != null ? response.error : request.error;
                    ErrorOccurred?.Invoke(ToUserMessage(error));
                }
                onComplete?.Invoke(response);
            }
        }

        private void BestEffortLeaveWorld()
        {
            if (CurrentWorld == null || !IsFirebaseReady()) return;
            try
            {
                var payload = new WorldApiRequest { action = "leaveWorld", worldId = CurrentWorld.worldId };
                var request = new UnityWebRequest(FirebaseConfig.SocialPvpApiUrl, "POST");
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + AuthManager.Instance.IdToken);
                request.SendWebRequest();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WorldChannel] 퇴장 요청 실패: " + e.Message);
            }
        }

        private static string ToUserMessage(string error)
        {
            switch (error)
            {
                case "world_full": return "필드 정원이 5명으로 가득 찼습니다.";
                case "user_blocked": return "차단 관계인 사용자와는 상호작용할 수 없습니다.";
                case "player_not_nearby": return "상대가 상호작용 거리 밖에 있습니다.";
                case "team_must_have_three": return "대전하려면 곤충 3마리 팀이 필요합니다.";
                case "not_friends": return "친구만 같은 필드로 초대할 수 있습니다.";
                case "unauthenticated": return "로그인이 만료되었습니다.";
                default: return string.IsNullOrEmpty(error) ? "온라인 필드 요청에 실패했습니다." : error;
            }
        }
    }
}
