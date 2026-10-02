using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace InsectGame.Core
{
    /// <summary>서버가 돌려주는 섬 공개 정보. 통신 규격은 <c>Docs/SocialPvp.md</c> 「섬 공유」.</summary>
    [Serializable]
    public class IslandInfo
    {
        public string ownerUid;
        public string ownerName;
        public string friendCode;
        public bool isPublic;
        public int likes;
        public int visits;
        public bool likedByMe;
        public long updatedAtMs;
        /// <summary>섬 스냅샷 JSON(<see cref="IslandSnapshot"/>). <c>getIsland</c>만 채워서 준다.</summary>
        public string snapshot;
    }

    [Serializable]
    internal class IslandApiRequest
    {
        public string action;
        public string island;
        public bool isPublic;
        public string friendCode;
        public string targetUid;
    }

    [Serializable]
    internal class IslandApiResponse
    {
        public bool success;
        public string error;
        public IslandInfo island;
    }

    /// <summary>
    /// 섬 공개·방문·좋아요의 서버 통신. 친구 기능과 같은 Cloud Function(<c>socialPvpApi</c>)에 액션으로 얹혀 있다 —
    /// Firestore 규칙이 타인 문서 읽기를 막아서 클라이언트가 직접 읽을 길이 없다.
    ///
    /// <b>서버가 없어도 섬은 돈다.</b> 미배포·오프라인·비로그인이면 요청을 보내지 않고 <see cref="LastError"/>만 채운다.
    /// </summary>
    public class IslandShareClient : MonoBehaviour
    {
        /// <summary>서버가 받는 스냅샷 문자열 길이 상한(functions/island.js와 같은 값). 넘으면 올리지 않는다.</summary>
        internal const int MaxSnapshotChars = 48000;

        [SerializeField] private IslandManager island;

        private bool busy;
        private bool publishQueued;

        /// <summary>내 섬의 공개 정보(좋아요·방문 수·섬 코드). 아직 못 받았으면 null.</summary>
        public IslandInfo MyInfo { get; private set; }
        public string LastError { get; private set; }
        public bool IsBusy => busy;

        /// <summary>내 정보·오류·진행 상태가 바뀌었다.</summary>
        public event Action StateChanged;

        public void AutoWire(IslandManager islandManager)
        {
            if (island == null) island = islandManager;
        }

        /// <summary>서버에 닿을 수 있는 상태인가. 아니면 이유를 <see cref="LastError"/>에 적는다.</summary>
        public bool CanRequest()
        {
            if (!FirebaseConfig.IsSocialPvpConfigured)
            {
                SetError("섬 공유 서버가 아직 준비되지 않았습니다.");
                return false;
            }
            AuthManager auth = AuthManager.Instance;
            if (auth == null || !auth.IsLoggedIn || auth.IsMasterAccount || string.IsNullOrEmpty(auth.IdToken))
            {
                SetError("섬 공유는 로그인한 계정에서만 쓸 수 있습니다.");
                return false;
            }
            return true;
        }

        public void ClearError()
        {
            if (string.IsNullOrEmpty(LastError)) return;
            LastError = null;
            StateChanged?.Invoke();
        }

        /// <summary>내 섬의 좋아요·방문 수·섬 코드를 받아 온다.</summary>
        public void RefreshMyInfo()
        {
            if (busy || !CanRequest()) return;
            StartCoroutine(Send(new IslandApiRequest { action = "getMyIsland" }, response =>
            {
                if (response != null && response.success && response.island != null) MyInfo = response.island;
            }));
        }

        /// <summary>
        /// 지금 섬 모습을 올린다(공개 설정 포함). 꾸미기 화면을 닫을 때·공개 설정을 바꿀 때 부른다.
        /// 요청 중에 또 부르면 끝난 뒤 한 번만 더 올린다 — 마지막 모습이 올라가면 된다.
        /// </summary>
        public void Publish()
        {
            if (island == null) return;
            if (busy)
            {
                publishQueued = true;
                return;
            }
            // 조용히 실패한다 — 꾸미기를 닫을 때마다 "서버가 없습니다"를 띄우면 서버 없는 환경에서 계속 거슬린다.
            if (!FirebaseConfig.IsSocialPvpConfigured) return;
            AuthManager auth = AuthManager.Instance;
            if (auth == null || !auth.IsLoggedIn || auth.IsMasterAccount || string.IsNullOrEmpty(auth.IdToken)) return;

            string json = JsonUtility.ToJson(island.BuildSnapshot(auth.DisplayName));
            if (json.Length > MaxSnapshotChars)
            {
                SetError("섬에 놓인 물건이 너무 많아 공개할 수 없습니다.");
                return;
            }
            StartCoroutine(Send(new IslandApiRequest
            {
                action = "publishIsland",
                island = json,
                isPublic = island.IsPublic,
            }, response =>
            {
                if (response != null && response.success && response.island != null) MyInfo = response.island;
            }));
        }

        /// <summary>섬 코드(친구 코드 8자리)로 남의 섬을 받아 온다.</summary>
        public void FetchByCode(string friendCode, Action<IslandInfo, IslandSnapshot> onDone)
        {
            string code = (friendCode ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
            {
                SetError("섬 코드를 입력해 주세요.");
                onDone?.Invoke(null, null);
                return;
            }
            Fetch(new IslandApiRequest { action = "getIsland", friendCode = code }, onDone);
        }

        /// <summary>uid로 남의 섬을 받아 온다(친구 목록에서 고를 때).</summary>
        public void FetchByUid(string uid, Action<IslandInfo, IslandSnapshot> onDone)
        {
            if (string.IsNullOrEmpty(uid))
            {
                onDone?.Invoke(null, null);
                return;
            }
            Fetch(new IslandApiRequest { action = "getIsland", targetUid = uid }, onDone);
        }

        private void Fetch(IslandApiRequest request, Action<IslandInfo, IslandSnapshot> onDone)
        {
            if (busy || !CanRequest())
            {
                onDone?.Invoke(null, null);
                return;
            }
            StartCoroutine(Send(request, response =>
            {
                if (response == null || !response.success || response.island == null)
                {
                    onDone?.Invoke(null, null);
                    return;
                }
                IslandSnapshot snapshot = ParseSnapshot(response.island);
                if (snapshot == null) SetError("섬을 불러오지 못했습니다.");
                onDone?.Invoke(snapshot != null ? response.island : null, snapshot);
            }));
        }

        /// <summary>
        /// 받은 섬을 읽어 정리한다. <b>남이 만든 데이터</b>라 모르는 물건·경계 밖·겹침을 여기서 버린다.
        /// 주인 이름은 서버가 정한 값으로 덮는다(스냅샷 안의 이름은 서버가 버린다).
        /// </summary>
        internal static IslandSnapshot ParseSnapshot(IslandInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.snapshot)) return null;
            IslandSnapshot snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<IslandSnapshot>(info.snapshot);
            }
            catch (Exception)
            {
                return null;
            }
            if (snapshot == null) return null;
            snapshot.ownerName = string.IsNullOrEmpty(info.ownerName) ? "탐험가" : info.ownerName;
            IslandSaveRules.SanitizeSnapshot(snapshot);
            return snapshot;
        }

        /// <summary>남의 섬에 좋아요(하루 한 번).</summary>
        public void Like(string targetUid, Action<IslandInfo> onDone)
        {
            if (busy || string.IsNullOrEmpty(targetUid) || !CanRequest())
            {
                onDone?.Invoke(null);
                return;
            }
            StartCoroutine(Send(new IslandApiRequest { action = "likeIsland", targetUid = targetUid }, response =>
            {
                onDone?.Invoke(response != null && response.success ? response.island : null);
            }));
        }

        private IEnumerator Send(IslandApiRequest payload, Action<IslandApiResponse> onComplete, bool allowRetry = true)
        {
            busy = true;
            LastError = null;
            StateChanged?.Invoke();

            IslandApiResponse response = null;
            string transportError = null;
            long status = 0;
            using (var request = new UnityWebRequest(FirebaseConfig.SocialPvpApiUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + AuthManager.Instance.IdToken);
                // 무제한 대기면 busy가 영영 안 풀려 방문 창이 멈춘다.
                request.timeout = 15;
                yield return request.SendWebRequest();

                status = request.responseCode;
                if (request.result != UnityWebRequest.Result.Success) transportError = request.error;
                string text = request.downloadHandler != null ? request.downloadHandler.text : null;
                if (!string.IsNullOrEmpty(text))
                {
                    try { response = JsonUtility.FromJson<IslandApiResponse>(text); }
                    catch (Exception e) { Debug.LogWarning("[Island] 응답 파싱 실패: " + e.Message); }
                }
            }

            if (status == 401 && allowRetry && AuthManager.Instance != null)
            {
                bool refreshed = false;
                yield return AuthManager.Instance.TryRefreshTokenForRetry(value => refreshed = value);
                if (refreshed)
                {
                    yield return Send(payload, onComplete, false);
                    yield break;
                }
            }

            busy = false;
            if (response == null || !response.success)
                LastError = ToUserMessage(response != null ? response.error : null, transportError);

            onComplete?.Invoke(response);
            StateChanged?.Invoke();

            if (publishQueued)
            {
                publishQueued = false;
                Publish();
            }
        }

        private void SetError(string message)
        {
            LastError = message;
            StateChanged?.Invoke();
        }

        private static string ToUserMessage(string error, string transportError)
        {
            switch (error)
            {
                case "island_not_found": return "그 섬을 찾을 수 없습니다. 아직 공개한 적이 없는 섬일 수 있어요.";
                case "island_private": return "주인이 공개하지 않은 섬입니다.";
                case "island_target_required": return "섬 코드를 입력해 주세요.";
                case "island_invalid": return "섬 정보를 올리지 못했습니다.";
                case "cannot_like_self": return "내 섬에는 좋아요를 누를 수 없습니다.";
                case "already_liked_today": return "오늘은 이미 좋아요를 눌렀습니다.";
                case "user_blocked": return "차단 관계인 사용자의 섬은 방문할 수 없습니다.";
                case "unauthenticated": return "로그인이 만료되었습니다. 다시 로그인해 주세요.";
                // 섬 액션을 모르는 옛 서버 — 함수가 아직 새로 배포되지 않았다.
                case "unknown_action": return "섬 공유 서버가 아직 준비되지 않았습니다.";
                default:
                    return string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(transportError)
                        ? "서버에 연결하지 못했습니다."
                        : "서버 요청에 실패했습니다.";
            }
        }
    }
}
