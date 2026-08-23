using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using InsectGame.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace InsectGame.Core
{
    [Serializable]
    public class PvpSkillSnapshot
    {
        public string skillId;
        public string displayName;
        public int power;
        public int element;
        public int cooldown;
        public int effectType;
        public float effectValue;
        public int effectDuration;
    }

    [Serializable]
    public class PvpInsectSnapshot
    {
        public string instanceId;
        public string insectId;
        public string displayName;
        public int level;
        public int primaryType;
        public int secondaryType;
        public int maxHp;
        public int hp;
        public int attack;
        public int defense;
        public PvpSkillSnapshot[] skills;
    }

    [Serializable]
    public class PvpProfileSnapshot
    {
        public string uid;
        public string displayName;
        public string friendCode;
        public int level;
        public int rating;
        public string rank;
        public int wins;
        public int losses;
        public PvpInsectSnapshot[] team;
        public string activeMatchId;
    }

    [Serializable]
    public class FriendRequestSnapshot : PvpProfileSnapshot
    {
        public string requestId;
    }

    [Serializable]
    public class PvpChallengeSnapshot : PvpProfileSnapshot
    {
        public string challengeId;
    }

    [Serializable]
    public class PvpMatchState
    {
        public string matchId;
        public string mode;
        public string status;
        public PvpProfileSnapshot player1;
        public PvpProfileSnapshot player2;
        public PvpInsectSnapshot[] team1;
        public PvpInsectSnapshot[] team2;
        public int active1;
        public int active2;
        public string turnUid;
        public int turnNumber;
        public string[] log;
        public string winnerUid;
        public long createdAtMs;
        public long updatedAtMs;
    }

    [Serializable]
    public class SocialPvpState
    {
        public PvpProfileSnapshot profile;
        public PvpProfileSnapshot[] friends = Array.Empty<PvpProfileSnapshot>();
        public FriendRequestSnapshot[] incomingRequests = Array.Empty<FriendRequestSnapshot>();
        public PvpChallengeSnapshot[] incomingChallenges = Array.Empty<PvpChallengeSnapshot>();
        public PvpProfileSnapshot[] blockedUsers = Array.Empty<PvpProfileSnapshot>();
        public PvpProfileSnapshot[] leaderboard = Array.Empty<PvpProfileSnapshot>();
        public bool queued;
        public long queueStartedAtMs;
    }

    [Serializable]
    internal class SocialPvpApiRequest
    {
        public string action;
        public string displayName;
        public int level;
        public PvpInsectSnapshot[] team;
        public string friendCode;
        public string requestId;
        public bool accept;
        public string friendUid;
        public string targetUid;
        public string challengeId;
        public string matchId;
        public string clientActionId;
        public string actionType;
        public int skillIndex;
        public int slot;
    }

    [Serializable]
    internal class SocialPvpApiResponse
    {
        public bool success;
        public string error;
        public PvpProfileSnapshot profile;
        public PvpProfileSnapshot[] friends;
        public FriendRequestSnapshot[] incomingRequests;
        public PvpChallengeSnapshot[] incomingChallenges;
        public PvpProfileSnapshot[] blockedUsers;
        public PvpProfileSnapshot[] leaderboard;
        public bool queued;
        public long queueStartedAtMs;
        public string matchId;
        public PvpMatchState match;
    }

    public class SocialPvpManager : MonoBehaviour
    {
        public static SocialPvpManager Instance { get; private set; }

        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private BattleTeamManager battleTeam;
        [SerializeField] private PlayerProgressController progress;
        [SerializeField, Min(1f)] private float pollInterval = 2.5f;

        public SocialPvpState State { get; private set; } = new SocialPvpState();
        public PvpMatchState CurrentMatch { get; private set; }
        public bool IsBusy { get; private set; }
        public string LastError { get; private set; }

        public event Action StateChanged;
        public event Action<string> ErrorOccurred;

        private float nextPollTime;

        // 파기된 자신을 static에 남기면 `Instance != null`과 `Instance?.`가 서로 다른 답을 낸다
        // (`WorldChannelManager.OnDestroy`와 같은 처리). 이 오브젝트는 부모가 있어 씬 재로드 때
        // 실제로 파기된다.
        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(this);
        }

        private void Update()
        {
            bool needsPoll = State != null && State.queued;
            needsPoll |= CurrentMatch != null && CurrentMatch.status == "active";
            needsPoll |= WorldChannelManager.Instance != null && WorldChannelManager.Instance.IsJoined;
            if (!needsPoll || IsBusy || Time.unscaledTime < nextPollTime) return;
            nextPollTime = Time.unscaledTime + pollInterval;
            StartCoroutine(PollRoutine());
        }

        public void AutoWire(PlayerInsectCollection insectCollection, BattleTeamManager team,
            PlayerProgressController playerProgress)
        {
            if (collection == null) collection = insectCollection;
            if (battleTeam == null) battleTeam = team;
            if (progress == null) progress = playerProgress;
        }

        public bool HasValidTeam(out string reason)
        {
            try
            {
                BuildTeamSnapshot(true);
                reason = null;
                return true;
            }
            catch (InvalidOperationException e)
            {
                reason = e.Message;
                return false;
            }
        }

        public void RefreshAll()
        {
            if (!CanRequest() || IsBusy) return;
            StartCoroutine(RefreshAllRoutine());
        }

        public void SendFriendRequest(string friendCode)
        {
            RunMutation(new SocialPvpApiRequest
            {
                action = "sendFriendRequest",
                friendCode = (friendCode ?? string.Empty).Trim().ToUpperInvariant(),
            });
        }

        public void RespondFriendRequest(string requestId, bool accept)
        {
            RunMutation(new SocialPvpApiRequest
            {
                action = "respondFriendRequest", requestId = requestId, accept = accept,
            });
        }

        public void RemoveFriend(string friendUid)
        {
            RunMutation(new SocialPvpApiRequest { action = "removeFriend", friendUid = friendUid });
        }

        public void UnblockUser(string targetUid)
        {
            RunMutation(new SocialPvpApiRequest { action = "unblockUser", targetUid = targetUid });
        }

        public void QueueRanked()
        {
            if (!CanRequest() || IsBusy) return;
            StartCoroutine(SyncThenQueueRoutine());
        }

        public void CancelQueue()
        {
            RunMutation(new SocialPvpApiRequest { action = "cancelQueue" });
        }

        public void ChallengeFriend(string friendUid)
        {
            if (!CanRequest() || IsBusy) return;
            StartCoroutine(SyncThenMutationRoutine(new SocialPvpApiRequest
            {
                action = "challengeFriend", friendUid = friendUid,
            }));
        }

        public void RespondChallenge(string challengeId, bool accept)
        {
            var request = new SocialPvpApiRequest
            {
                action = "respondChallenge", challengeId = challengeId, accept = accept,
            };
            if (accept)
            {
                if (!CanRequest() || IsBusy) return;
                StartCoroutine(SyncThenMutationRoutine(request));
            }
            else RunMutation(request);
        }

        public void SubmitBasicAttack()
        {
            SubmitBattleAction("basic", 0, 0);
        }

        public void SubmitSkill(int skillIndex)
        {
            SubmitBattleAction("skill", skillIndex, 0);
        }

        public void SwitchInsect(int slot)
        {
            SubmitBattleAction("switch", 0, slot);
        }

        public void Surrender()
        {
            SubmitBattleAction("surrender", 0, 0);
        }

        private void SubmitBattleAction(string actionType, int skillIndex, int slot)
        {
            if (CurrentMatch == null || string.IsNullOrEmpty(CurrentMatch.matchId)) return;
            RunMutation(new SocialPvpApiRequest
            {
                action = "battleAction",
                matchId = CurrentMatch.matchId,
                clientActionId = Guid.NewGuid().ToString("N"),
                actionType = actionType,
                skillIndex = skillIndex,
                slot = slot,
            });
        }

        private void RunMutation(SocialPvpApiRequest request)
        {
            if (!CanRequest() || IsBusy) return;
            StartCoroutine(MutationRoutine(request));
        }

        private IEnumerator MutationRoutine(SocialPvpApiRequest request)
        {
            IsBusy = true;
            SocialPvpApiResponse response = null;
            yield return SendRequest(request, value => response = value);
            if (response != null && response.success)
            {
                ApplyResponse(response);
                yield return RefreshStateOnlyRoutine();
            }
            IsBusy = false;
            StateChanged?.Invoke();
        }

        private IEnumerator SyncThenMutationRoutine(SocialPvpApiRequest mutation)
        {
            IsBusy = true;
            SocialPvpApiResponse sync = null;
            SocialPvpApiRequest syncRequest;
            try { syncRequest = BuildSyncRequest(true); }
            catch (InvalidOperationException e)
            {
                SetError(e.Message);
                IsBusy = false;
                yield break;
            }
            yield return SendRequest(syncRequest, value => sync = value);
            if (sync != null && sync.success)
            {
                SocialPvpApiResponse response = null;
                yield return SendRequest(mutation, value => response = value);
                if (response != null && response.success)
                {
                    ApplyResponse(response);
                    yield return RefreshStateOnlyRoutine();
                }
            }
            IsBusy = false;
            StateChanged?.Invoke();
        }

        private IEnumerator SyncThenQueueRoutine()
        {
            IsBusy = true;
            SocialPvpApiRequest syncRequest;
            try { syncRequest = BuildSyncRequest(true); }
            catch (InvalidOperationException e)
            {
                SetError(e.Message);
                IsBusy = false;
                yield break;
            }
            SocialPvpApiResponse sync = null;
            yield return SendRequest(syncRequest, value => sync = value);
            if (sync != null && sync.success)
            {
                SocialPvpApiResponse queued = null;
                yield return SendRequest(new SocialPvpApiRequest { action = "queueRanked" }, value => queued = value);
                if (queued != null && queued.success)
                {
                    State.queued = queued.queued;
                    ApplyResponse(queued);
                }
            }
            IsBusy = false;
            nextPollTime = Time.unscaledTime + 1f;
            StateChanged?.Invoke();
        }

        private IEnumerator RefreshAllRoutine()
        {
            IsBusy = true;
            LastError = null;
            SocialPvpApiRequest syncRequest;
            try { syncRequest = BuildSyncRequest(false); }
            catch (InvalidOperationException e)
            {
                SetError(e.Message);
                IsBusy = false;
                yield break;
            }
            SocialPvpApiResponse sync = null;
            yield return SendRequest(syncRequest, value => sync = value);
            if (sync != null && sync.success)
                yield return RefreshStateOnlyRoutine();
            IsBusy = false;
            StateChanged?.Invoke();
        }

        private IEnumerator RefreshStateOnlyRoutine()
        {
            SocialPvpApiResponse social = null;
            yield return SendRequest(new SocialPvpApiRequest { action = "getSocial" }, value => social = value);
            if (social != null && social.success) ApplySocial(social);

            SocialPvpApiResponse board = null;
            yield return SendRequest(new SocialPvpApiRequest { action = "leaderboard" }, value => board = value);
            if (board != null && board.success)
                State.leaderboard = board.leaderboard ?? Array.Empty<PvpProfileSnapshot>();

            SocialPvpApiResponse match = null;
            yield return SendRequest(new SocialPvpApiRequest { action = "getMatch" }, value => match = value);
            if (match != null && match.success && match.match != null)
                CurrentMatch = match.match;
        }

        private IEnumerator PollRoutine()
        {
            IsBusy = true;
            yield return RefreshStateOnlyRoutine();
            IsBusy = false;
            StateChanged?.Invoke();
        }

        private SocialPvpApiRequest BuildSyncRequest(bool requireCompleteTeam)
        {
            AuthManager auth = AuthManager.Instance;
            return new SocialPvpApiRequest
            {
                action = "syncProfile",
                displayName = auth != null ? auth.DisplayName : "탐험가",
                level = progress != null ? progress.Level : 1,
                team = BuildTeamSnapshot(requireCompleteTeam),
            };
        }

        private PvpInsectSnapshot[] BuildTeamSnapshot(bool requireCompleteTeam)
        {
            if (collection == null || battleTeam == null)
                throw new InvalidOperationException("배틀 팀 시스템이 준비되지 않았습니다.");
            var result = new List<PvpInsectSnapshot>(3);
            foreach (string instanceId in battleTeam.GetAllSlots())
            {
                if (string.IsNullOrEmpty(instanceId)) continue;
                PlayerInsectData owned = collection.GetByInstanceId(instanceId);
                if (owned == null) continue;
                InsectData data = collection.GetInsectData(owned.insectId);
                if (data == null) continue;
                InsectSkill[] equipped = collection.GetEquippedSkills(owned);
                var skills = new List<PvpSkillSnapshot>();
                if (equipped != null)
                {
                    foreach (InsectSkill skill in equipped)
                    {
                        if (skill == null) continue;
                        skills.Add(new PvpSkillSnapshot
                        {
                            skillId = skill.skillId,
                            displayName = skill.displayName,
                            power = skill.power,
                            element = (int)skill.element,
                            cooldown = skill.cooldownTurns,
                            effectType = (int)skill.effectType,
                            effectValue = skill.effectValue,
                            effectDuration = skill.effectDurationTurns,
                        });
                    }
                }
                result.Add(new PvpInsectSnapshot
                {
                    instanceId = owned.instanceId,
                    insectId = owned.insectId,
                    displayName = data.displayName,
                    level = owned.level,
                    primaryType = (int)data.primaryType,
                    secondaryType = (int)data.secondaryType,
                    maxHp = owned.GetTotalHp(data.baseHp),
                    hp = owned.GetTotalHp(data.baseHp),
                    attack = owned.GetTotalAtk(data.baseAtk),
                    defense = owned.GetTotalDef(data.baseDef),
                    skills = skills.ToArray(),
                });
                if (result.Count == 3) break;
            }
            if (result.Count != 3 && requireCompleteTeam)
                throw new InvalidOperationException("3:3 배틀 팀에 곤충 3마리를 먼저 편성해야 합니다.");
            return result.Count == 3 ? result.ToArray() : Array.Empty<PvpInsectSnapshot>();
        }

        private bool CanRequest()
        {
            if (!FirebaseConfig.IsSocialPvpConfigured)
            {
                SetError("친구/랭크 서버가 아직 배포되지 않았습니다.");
                return false;
            }
            if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn
                || string.IsNullOrEmpty(AuthManager.Instance.IdToken))
            {
                SetError("온라인 기능을 사용하려면 로그인이 필요합니다.");
                return false;
            }
            return true;
        }

        private IEnumerator SendRequest(SocialPvpApiRequest payload,
            Action<SocialPvpApiResponse> onComplete, bool allowRetry = true)
        {
            string json = JsonUtility.ToJson(payload);
            using (var request = new UnityWebRequest(FirebaseConfig.SocialPvpApiUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + AuthManager.Instance.IdToken);
                yield return request.SendWebRequest();

                if (request.responseCode == 401 && allowRetry)
                {
                    bool refreshed = false;
                    yield return AuthManager.Instance.TryRefreshTokenForRetry(value => refreshed = value);
                    if (refreshed)
                    {
                        yield return SendRequest(payload, onComplete, false);
                        yield break;
                    }
                }

                SocialPvpApiResponse response = null;
                if (!string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    try { response = JsonUtility.FromJson<SocialPvpApiResponse>(request.downloadHandler.text); }
                    catch (Exception e) { Debug.LogWarning("[SocialPvp] 응답 파싱 실패: " + e.Message); }
                }
                if (request.result != UnityWebRequest.Result.Success || response == null || !response.success)
                {
                    string error = response != null ? response.error : request.error;
                    SetError(ToUserMessage(error));
                }
                onComplete?.Invoke(response);
            }
        }

        private void ApplyResponse(SocialPvpApiResponse response)
        {
            if (response.profile != null) State.profile = response.profile;
            if (response.match != null)
            {
                CurrentMatch = response.match;
                State.queued = false;
            }
            if (!string.IsNullOrEmpty(response.matchId) && CurrentMatch == null)
                nextPollTime = 0f;
        }

        private void ApplySocial(SocialPvpApiResponse response)
        {
            State.profile = response.profile;
            State.friends = response.friends ?? Array.Empty<PvpProfileSnapshot>();
            State.incomingRequests = response.incomingRequests ?? Array.Empty<FriendRequestSnapshot>();
            State.incomingChallenges = response.incomingChallenges ?? Array.Empty<PvpChallengeSnapshot>();
            State.blockedUsers = response.blockedUsers ?? Array.Empty<PvpProfileSnapshot>();
            State.queued = response.queued;
            State.queueStartedAtMs = response.queueStartedAtMs;
        }

        private void SetError(string message)
        {
            LastError = message;
            ErrorOccurred?.Invoke(message);
            StateChanged?.Invoke();
        }

        private static string ToUserMessage(string error)
        {
            switch (error)
            {
                case "friend_not_found": return "해당 친구 코드를 찾을 수 없습니다.";
                case "cannot_add_self": return "자기 자신은 친구로 추가할 수 없습니다.";
                case "already_friends": return "이미 친구입니다.";
                case "not_friends": return "친구 관계를 확인할 수 없습니다.";
                case "already_in_match": return "한쪽 사용자가 이미 배틀 중입니다.";
                case "not_your_turn": return "상대 턴입니다. 서버 상태를 갱신합니다.";
                case "skill_on_cooldown": return "아직 사용할 수 없는 기술입니다.";
                case "team_must_have_three": return "3마리 배틀 팀이 필요합니다.";
                case "user_blocked": return "차단 관계인 사용자와는 상호작용할 수 없습니다.";
                case "player_not_nearby": return "상대가 상호작용 거리 밖에 있습니다.";
                case "unauthenticated": return "로그인이 만료되었습니다. 다시 로그인해 주세요.";
                default: return string.IsNullOrEmpty(error) ? "서버 요청에 실패했습니다." : error;
            }
        }
    }
}
