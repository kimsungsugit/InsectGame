using System;
using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 같은 5인 필드의 원격 탐험가를 표시하고 근거리 대화/대전/차단과 친구 초대를 제공합니다.
    /// </summary>
    public class WorldFieldMultiplayerUI : MonoBehaviour, IModalUI
    {
        private sealed class RemoteAvatar
        {
            public GameObject root;
            public Material material;
            public WorldPlayer state;
        }

        [SerializeField] private WorldChannelManager manager;
        [SerializeField] private PlayerMovement localPlayer;
        [SerializeField, Min(2f)] private float interactionRange = 5f;
        [SerializeField, Min(1f)] private float avatarLerpSpeed = 8f;

        private readonly Dictionary<string, RemoteAvatar> remoteAvatars = new Dictionary<string, RemoteAvatar>();
        private readonly List<WorldChatMessage> messages = new List<WorldChatMessage>();
        private readonly List<WorldInviteSnapshot> invites = new List<WorldInviteSnapshot>();
        private readonly List<string> removeBuffer = new List<string>();

        private WorldPlayer nearestPlayer;
        private bool chatOpen;
        private bool friendsOpen;
        private string chatInput = string.Empty;
        // 채팅 대상은 uid로 고정한다. nearestPlayer는 매 Update 재계산되므로 대상으로 쓰면
        // (a) 상대가 멀어질 때 입력창만 사라지고 모달 잠금이 남아 화면이 빈 채로 입력이
        // 전부 막히고, (b) 작성 중 다른 탐험가가 더 가까워지면 사설 메시지가 오배송된다.
        private string chatTargetUid = string.Empty;
        private string pendingBlockUid = string.Empty;
        private float blockConfirmUntil;
        private string toast = string.Empty;
        private float toastUntil;
        private Vector2 friendScroll;
        private readonly UIDirectScroll friendDirectScroll = new UIDirectScroll();

        // OnGUI는 프레임당 Layout/Repaint/입력 이벤트로 여러 번 호출된다. 아래 문자열들은
        // 서버 이벤트 시점에만 바뀌므로 그때 한 번 만들고 Draw는 재사용한다.
        private string cachedWorldTitle = string.Empty;
        private string cachedNearbyLabel = string.Empty;
        private string nearbyLabelUid = string.Empty;
        private readonly List<string> messageLines = new List<string>();

        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private GUIStyle dangerStyle;
        private GUIStyle disabledStyle;
        private GUIStyle fieldStyle;
        private bool stylesReady;

        public bool IsOpen => chatOpen || friendsOpen;

        public void AutoWire(WorldChannelManager worldManager, PlayerMovement player)
        {
            Unsubscribe();
            manager = worldManager;
            localPlayer = player;
            Subscribe();
        }

        public void CloseModal()
        {
            chatOpen = false;
            friendsOpen = false;
            chatTargetUid = string.Empty;
            chatInput = string.Empty;
            ResetFriendScroll();
            ModalUIRegistry.Unregister(this);
        }

        private void OnEnable()
        {
            if (manager == null) manager = WorldChannelManager.Instance;
            if (localPlayer == null) localPlayer = FindFirstObjectByType<PlayerMovement>();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetFriendScroll();
            ModalUIRegistry.Unregister(this);
            ClearRemoteAvatars();
        }

        private void OnDestroy()
        {
            ClearRemoteAvatars();
        }

        private void Subscribe()
        {
            if (manager == null) return;
            manager.WorldStateUpdated -= HandleWorldState;
            manager.MessagesUpdated -= HandleMessages;
            manager.InvitesUpdated -= HandleInvites;
            manager.WorldLeft -= HandleWorldLeft;
            manager.ActionCompleted -= HandleActionCompleted;
            manager.ErrorOccurred -= HandleError;
            manager.WorldStateUpdated += HandleWorldState;
            manager.MessagesUpdated += HandleMessages;
            manager.InvitesUpdated += HandleInvites;
            manager.WorldLeft += HandleWorldLeft;
            manager.ActionCompleted += HandleActionCompleted;
            manager.ErrorOccurred += HandleError;
        }

        private void Unsubscribe()
        {
            if (manager == null) return;
            manager.WorldStateUpdated -= HandleWorldState;
            manager.MessagesUpdated -= HandleMessages;
            manager.InvitesUpdated -= HandleInvites;
            manager.WorldLeft -= HandleWorldLeft;
            manager.ActionCompleted -= HandleActionCompleted;
            manager.ErrorOccurred -= HandleError;
        }

        private void Update()
        {
            nearestPlayer = null;
            if (manager == null || !manager.IsJoined || localPlayer == null) return;

            float nearestSqr = interactionRange * interactionRange;
            foreach (RemoteAvatar avatar in remoteAvatars.Values)
            {
                if (avatar.root == null || avatar.state == null) continue;
                Vector3 target = avatar.state.Position;
                avatar.root.transform.position = Vector3.Lerp(
                    avatar.root.transform.position, target, Time.deltaTime * avatarLerpSpeed);
                Quaternion rotation = Quaternion.Euler(0f, avatar.state.facing, 0f);
                avatar.root.transform.rotation = Quaternion.Slerp(
                    avatar.root.transform.rotation, rotation, Time.deltaTime * avatarLerpSpeed);

                float sqr = (target - localPlayer.transform.position).sqrMagnitude;
                if (sqr <= nearestSqr)
                {
                    nearestSqr = sqr;
                    nearestPlayer = avatar.state;
                }
            }

            if (!string.IsNullOrEmpty(pendingBlockUid) && Time.unscaledTime > blockConfirmUntil)
                pendingBlockUid = string.Empty;

            // 근처 탐험가 라벨은 대상이 바뀔 때만 다시 만든다 (OnGUI 매 호출 보간 방지).
            string uid = nearestPlayer != null ? nearestPlayer.uid : string.Empty;
            if (uid != nearbyLabelUid)
            {
                nearbyLabelUid = uid;
                cachedNearbyLabel = nearestPlayer != null
                    ? $"근처 탐험가 · {nearestPlayer.displayName}  Lv.{nearestPlayer.level}"
                    : string.Empty;
            }
        }

        /// <summary>
        /// 고정된 uid로 현재 채팅 대상을 해석한다. 상대가 접속을 끊었거나 대화 범위를
        /// 벗어났거나 차단되면 null — 호출부가 모달을 닫아 입력 잠금을 푼다.
        /// </summary>
        private WorldPlayer ResolveChatTarget()
        {
            if (string.IsNullOrEmpty(chatTargetUid) || localPlayer == null) return null;
            if (!remoteAvatars.TryGetValue(chatTargetUid, out RemoteAvatar avatar)) return null;
            if (avatar.state == null || avatar.state.blocked) return null;

            float sqr = (avatar.state.Position - localPlayer.transform.position).sqrMagnitude;
            if (sqr > interactionRange * interactionRange) return null;
            return avatar.state;
        }

        private void HandleWorldState(WorldInstance world)
        {
            if (world == null || world.players == null)
            {
                ClearRemoteAvatars();
                cachedWorldTitle = string.Empty;
                return;
            }
            cachedWorldTitle = $"{world.displayName}   {world.playerCount}/5";
            // 서버 갱신으로 displayName/level이 바뀌었을 수 있으니 근처 라벨을 재생성시킨다.
            nearbyLabelUid = string.Empty;
            string ownUid = AuthManager.Instance != null ? AuthManager.Instance.UserId : string.Empty;
            removeBuffer.Clear();
            foreach (string uid in remoteAvatars.Keys) removeBuffer.Add(uid);

            foreach (WorldPlayer player in world.players)
            {
                if (player == null || string.IsNullOrEmpty(player.uid) || player.uid == ownUid) continue;
                removeBuffer.Remove(player.uid);
                if (!remoteAvatars.TryGetValue(player.uid, out RemoteAvatar avatar))
                {
                    avatar = CreateRemoteAvatar(player);
                    remoteAvatars[player.uid] = avatar;
                }
                avatar.state = player;
                UpdateAvatarLabel(avatar, player);
            }

            foreach (string uid in removeBuffer) RemoveRemoteAvatar(uid);
        }

        private void HandleMessages(IReadOnlyList<WorldChatMessage> updated)
        {
            messages.Clear();
            messageLines.Clear();
            if (updated == null) return;
            for (int i = Mathf.Max(0, updated.Count - 8); i < updated.Count; i++)
            {
                WorldChatMessage message = updated[i];
                messages.Add(message);
                messageLines.Add($"{message.displayName}: {message.message}");
            }
        }

        private void HandleInvites(IReadOnlyList<WorldInviteSnapshot> updated)
        {
            invites.Clear();
            if (updated != null) invites.AddRange(updated);
        }

        private void HandleWorldLeft()
        {
            CloseModal();
            ClearRemoteAvatars();
            messages.Clear();
            messageLines.Clear();   // messages와 항상 같이 비운다 (인덱스 정합)
            invites.Clear();
            cachedWorldTitle = string.Empty;
            cachedNearbyLabel = string.Empty;
            nearbyLabelUid = string.Empty;
        }

        private void HandleActionCompleted(string message)
        {
            ShowToast(message);
        }

        private void HandleError(string message)
        {
            ShowToast(message);
        }

        private void ShowToast(string message)
        {
            toast = message ?? string.Empty;
            toastUntil = Time.unscaledTime + 3.5f;
        }

        private RemoteAvatar CreateRemoteAvatar(WorldPlayer player)
        {
            var root = new GameObject("RemotePlayer_" + player.uid);
            root.transform.position = player.Position;

            Color color = Color.HSVToRGB(Mathf.Abs(StableHash(player.uid) % 1000) / 1000f, 0.58f, 0.95f);
            // 빌드에서 셰이더가 스트리핑되면 Find가 둘 다 null을 낼 수 있는데, new Material(null)은
            // 예외라 아바타 생성 자체가 실패한다. null이면 프리미티브 기본 머티리얼에 색만 입힌다.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = shader != null ? new Material(shader) { color = color } : null;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            body.transform.localScale = new Vector3(0.62f, 0.78f, 0.62f);
            ApplyAvatarMaterial(body, material, color);
            DisableCollider(body);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            head.transform.localScale = Vector3.one * 0.68f;
            ApplyAvatarMaterial(head, material, color);
            DisableCollider(head);

            var labelObject = new GameObject("NameLabel");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 2.75f, 0f);
            TextMesh text = labelObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 42;
            text.characterSize = 0.055f;
            text.color = Color.white;

            var avatar = new RemoteAvatar { root = root, material = material, state = player };
            UpdateAvatarLabel(avatar, player);
            return avatar;
        }

        /// <summary>
        /// 공용 머티리얼을 입힌다. 셰이더 스트리핑으로 material이 null이면 프리미티브 기본
        /// 머티리얼 인스턴스에 색만 입혀 최소한 보이게 한다(인스턴스는 GameObject와 함께 정리).
        /// </summary>
        private static void ApplyAvatarMaterial(GameObject gameObject, Material material, Color color)
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer == null) return;
            if (material != null) renderer.sharedMaterial = material;
            else renderer.material.color = color;
        }

        private static void DisableCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider == null) return;
            collider.enabled = false;
            Destroy(collider);
        }

        private static void UpdateAvatarLabel(RemoteAvatar avatar, WorldPlayer player)
        {
            if (avatar.root == null) return;
            TextMesh label = avatar.root.GetComponentInChildren<TextMesh>();
            if (label != null)
                label.text = player.blocked ? $"{player.displayName}  [차단됨]" : $"{player.displayName}  Lv.{player.level}";
        }

        private void RemoveRemoteAvatar(string uid)
        {
            if (!remoteAvatars.TryGetValue(uid, out RemoteAvatar avatar)) return;
            if (avatar.root != null) Destroy(avatar.root);
            if (avatar.material != null) Destroy(avatar.material);
            remoteAvatars.Remove(uid);
        }

        private void ClearRemoteAvatars()
        {
            removeBuffer.Clear();
            foreach (string uid in remoteAvatars.Keys) removeBuffer.Add(uid);
            foreach (string uid in removeBuffer) RemoveRemoteAvatar(uid);
        }

        private void OnGUI()
        {
            if (manager == null) return;
            InitStyles();
            if (!manager.IsJoined)
            {
                if (invites.Count > 0) DrawInvitePopup(invites[0]);
                if (Time.unscaledTime < toastUntil) DrawToast();
                return;
            }
            DrawFieldStatus();
            DrawMessages();
            if (nearestPlayer != null) DrawNearbyInteraction(nearestPlayer);
            if (chatOpen)
            {
                // 대상은 nearestPlayer가 아니라 고정 uid로 해석한다. 대상이 사라지면
                // 입력창만 감추는 게 아니라 모달을 닫아야 입력 잠금이 풀린다.
                WorldPlayer chatTarget = ResolveChatTarget();
                if (chatTarget != null) DrawChatComposer(chatTarget);
                else CloseModal();
            }
            if (friendsOpen) DrawFriendInvitePanel();
            if (invites.Count > 0) DrawInvitePopup(invites[0]);
            if (Time.unscaledTime < toastUntil) DrawToast();
        }

        // 세이프 에어리어를 반영한 가용폭 클램프 + 중앙 정렬 X (가로 비대칭 노치 보정).
        // DrawFieldStatus가 이미 쓰던 방식을 중앙 정렬 패널 전반에 통일한다.
        /// <summary>
        /// 이 패널 위의 탭이 월드 클릭-이동으로 새지 않게 등록한다.
        ///
        /// <b>왜 필요한가.</b> <c>PlayerMovement</c>는 <c>Input.GetMouseButtonDown(0)</c>을 Update에서
        /// 따로 폴링한다. 탭한 프레임엔 아직 모달이 안 열려 <c>IsAnyOpen()</c>이 false이고, IMGUI라
        /// <c>pointerOverUI</c>도 false다. 등록이 없으면 버튼 아래 월드 지점이 클릭 목표로 잡혀
        /// "3:3 대전"을 누른 순간 캐릭터가 상대 뒤로 걸어간다. 같은 결함을 `QuickAccessBarUI`가
        /// P0으로 겪었고 `CaptureInputController`는 처음부터 등록하고 있었다.
        ///
        /// <b>좌표 변환에 주의.</b> 이 화면은 <c>UIScale.Begin()</c>을 쓰지 않는 **픽셀 좌표계**
        /// (<c>UISafeLayout.Px</c>)인데 <c>RegisterBlockingRect</c>는 **가상 좌표**를 받는다
        /// (<c>IsScreenPointOverHud</c>가 화면좌표를 <c>UIScale.Scale</c>로 나눠 비교한다).
        /// 그대로 넘기면 스케일이 1이 아닌 기기에서 엉뚱한 영역이 막힌다.
        /// </summary>
        private static void BlockFieldClicks(float x, float y, float w, float h)
        {
            float s = UIScale.Scale;
            if (s <= 0f) return;
            FieldHudInput.RegisterBlockingRect(new Rect(x / s, y / s, w / s, h / s));
        }

        private static float SafeClampW(float desired) =>
            Mathf.Min(desired, Screen.width - SafeArea.Left - SafeArea.Right - 32f);

        private static float SafeCenterX(float w) =>
            SafeArea.Left + (Screen.width - SafeArea.Left - SafeArea.Right - w) * 0.5f;

        private void DrawFieldStatus()
        {
            WorldInstance world = manager.CurrentWorld;
            float w = Mathf.Min(360f, Screen.width - SafeArea.Left - SafeArea.Right - 32f);
            float x = Screen.width - SafeArea.Right - w - 18f;
            float y = UISafeLayout.Px.ContentTop;
            BlockFieldClicks(x, y, w, 122f);   // 탭이 월드 클릭-이동으로 새지 않게
            GUI.Box(new Rect(x, y, w, 122f), "", panelStyle);
            GUI.Label(new Rect(x + 12f, y + 8f, w - 24f, 36f), cachedWorldTitle, titleStyle);
            if (GUI.Button(new Rect(x + 14f, y + 54f, w - 28f, 56f), "친구를 이 필드로 초대", buttonStyle))
            {
                bool opening = !friendsOpen;
                CloseModal();
                friendsOpen = opening;
                if (friendsOpen) ModalUIRegistry.Register(this);
            }
        }

        private void DrawNearbyInteraction(WorldPlayer player)
        {
            float w = SafeClampW(620f);
            float h = 132f;
            float x = SafeCenterX(w);
            float y = UISafeLayout.Px.BottomY(h);
            BlockFieldClicks(x, y, w, h);   // 탭이 월드 클릭-이동으로 새지 않게
            GUI.Box(new Rect(x, y, w, h), "", panelStyle);
            GUI.Label(new Rect(x + 18f, y + 10f, w - 36f, 30f), cachedNearbyLabel, titleStyle);

            float gap = 8f;
            float btnW = (w - 44f - gap * 2f) / 3f;
            float by = y + 52f;
            if (player.blocked)
            {
                GUI.enabled = false;
                GUI.Button(new Rect(x + 14f, by, btnW, 54f), "대화 차단됨", disabledStyle);
                GUI.Button(new Rect(x + 14f + btnW + gap, by, btnW, 54f), "대전 차단됨", disabledStyle);
                GUI.enabled = true;
                if (GUI.Button(new Rect(x + 14f + (btnW + gap) * 2f, by, btnW, 54f), "차단 해제", buttonStyle))
                    manager.UnblockPlayer(player.uid);
                return;
            }

            if (GUI.Button(new Rect(x + 14f, by, btnW, 54f), "대화", buttonStyle))
            {
                CloseModal();
                chatOpen = true;
                chatTargetUid = player.uid;   // 이 시점의 상대로 고정 — 이후 근접도와 무관
                ModalUIRegistry.Register(this);
            }
            if (GUI.Button(new Rect(x + 14f + btnW + gap, by, btnW, 54f), "3:3 대전", buttonStyle))
                manager.ChallengePlayer(player.uid);

            bool confirming = pendingBlockUid == player.uid && Time.unscaledTime <= blockConfirmUntil;
            if (GUI.Button(new Rect(x + 14f + (btnW + gap) * 2f, by, btnW, 54f),
                confirming ? "정말 차단" : "차단", dangerStyle))
            {
                if (confirming)
                {
                    manager.BlockPlayer(player.uid);
                    pendingBlockUid = string.Empty;
                    CloseModal();
                }
                else
                {
                    pendingBlockUid = player.uid;
                    blockConfirmUntil = Time.unscaledTime + 3f;
                }
            }
        }

        private void DrawChatComposer(WorldPlayer player)
        {
            float w = SafeClampW(620f);
            float h = 120f;
            float x = SafeCenterX(w);
            float y = UISafeLayout.Px.CenteredY(h);
            BlockFieldClicks(x, y, w, h);   // 탭이 월드 클릭-이동으로 새지 않게
            GUI.Box(new Rect(x, y, w, h), "", panelStyle);
            UIHelper.LabelFit(new Rect(x + 14f, y + 8f, w - 28f, 27f), player.displayName + "에게 말하기", titleStyle);
            chatInput = GUI.TextField(new Rect(x + 14f, y + 42f, w - 150f, 55f), chatInput, 80, fieldStyle);
            if (GUI.Button(new Rect(x + w - 126f, y + 42f, 112f, 55f), "보내기", buttonStyle))
            {
                // player는 ResolveChatTarget이 chatTargetUid로 해석한 고정 대상이다.
                manager.SendPrivateChat(player.uid, chatInput);
                CloseModal();
            }
        }

        private void DrawMessages()
        {
            if (messages.Count == 0) return;
            float w = Mathf.Min(470f, Screen.width * 0.46f);
            float h = Mathf.Min(190f, messages.Count * 34f + 20f);
            float x = SafeArea.Left + 16f;
            float y = UISafeLayout.Px.BottomY(h) - 150f;   // 하단 근접 패널 위
            GUI.Box(new Rect(x, y, w, h), "", panelStyle);
            int first = Mathf.Max(0, messageLines.Count - 5);
            for (int i = first; i < messageLines.Count; i++)
            {
                GUI.Label(new Rect(x + 12f, y + 8f + (i - first) * 34f, w - 24f, 30f),
                    messageLines[i], smallStyle);
            }
        }

        private void DrawFriendInvitePanel()
        {
            float w = SafeClampW(520f);
            float h = UISafeLayout.Px.ClampHeight(520f);
            float x = SafeCenterX(w);
            float y = UISafeLayout.Px.ContentTop;
            BlockFieldClicks(x, y, w, h);   // 탭이 월드 클릭-이동으로 새지 않게
            GUI.Box(new Rect(x, y, w, h), "", panelStyle);
            GUI.Label(new Rect(x + 16f, y + 12f, w - 100f, 36f), "친구 필드 초대", titleStyle);
            if (GUI.Button(new Rect(x + w - 72f, y + 8f, 58f, 56f), "X", dangerStyle)) CloseModal();

            PvpProfileSnapshot[] friends = SocialPvpManager.Instance != null
                ? SocialPvpManager.Instance.State.friends
                : Array.Empty<PvpProfileSnapshot>();
            Rect view = new Rect(x + 16f, y + 72f, w - 32f, h - 88f);
            float contentH = Mathf.Max(view.height, friends.Length * 76f);
            HandleScreenSpaceDirectScroll(
                ref friendScroll,
                friendDirectScroll,
                view,
                contentH,
                38f);
            friendScroll = GUI.BeginScrollView(view, friendScroll, new Rect(0f, 0f, view.width - 18f, contentH));
            if (friends.Length == 0)
                GUI.Label(new Rect(8f, 20f, view.width - 40f, 60f), "친구 목록이 비어 있습니다.\n소셜 메뉴에서 친구를 먼저 추가하세요.", labelStyle);
            for (int i = 0; i < friends.Length; i++)
            {
                PvpProfileSnapshot friend = friends[i];
                float rowY = i * 76f;
                GUI.Label(new Rect(8f, rowY + 8f, view.width - 176f, 56f),
                    $"{friend.displayName}  Lv.{friend.level}", labelStyle);
                if (GUI.Button(new Rect(view.width - 160f, rowY + 7f, 128f, 56f), "초대", buttonStyle))
                    manager.InviteFriend(friend.uid);
            }
            GUI.EndScrollView();
        }

        private void ResetFriendScroll()
        {
            friendScroll = Vector2.zero;
            friendDirectScroll.Reset();
        }

        private static void HandleScreenSpaceDirectScroll(
            ref Vector2 scrollPosition,
            UIDirectScroll directScroll,
            Rect viewport,
            float contentHeight,
            float wheelStep)
        {
            float scale = Mathf.Max(0.3f, UIScale.Scale);
            Vector2 virtualScroll = scrollPosition / scale;
            Rect virtualViewport = new Rect(
                viewport.x / scale,
                viewport.y / scale,
                viewport.width / scale,
                viewport.height / scale);
            directScroll.Handle(
                ref virtualScroll,
                virtualViewport,
                contentHeight / scale,
                wheelStep / scale);
            scrollPosition = virtualScroll * scale;
        }

        private void DrawInvitePopup(WorldInviteSnapshot invite)
        {
            float w = SafeClampW(520f);
            float h = 190f;
            float x = SafeCenterX(w);
            float y = UISafeLayout.Px.ContentTop + 100f;   // 상단 초대/친구 패널 아래
            BlockFieldClicks(x, y, w, h);   // 탭이 월드 클릭-이동으로 새지 않게
            GUI.Box(new Rect(x, y, w, h), "", panelStyle);
            GUI.Label(new Rect(x + 16f, y + 12f, w - 32f, 32f), "필드 초대", titleStyle);
            GUI.Label(new Rect(x + 16f, y + 50f, w - 32f, 40f),
                $"{invite.displayName}님이 {invite.worldName}에 초대했습니다.", labelStyle);
            float btnW = (w - 48f) * 0.5f;
            if (GUI.Button(new Rect(x + 16f, y + 112f, btnW, 58f), "함께 입장", buttonStyle))
                manager.RespondInvite(invite.inviteId, true);
            if (GUI.Button(new Rect(x + 24f + btnW, y + 112f, btnW, 58f), "거절", dangerStyle))
                manager.RespondInvite(invite.inviteId, false);
        }

        private void DrawToast()
        {
            float w = SafeClampW(560f);
            float x = SafeCenterX(w);
            float y = UISafeLayout.Px.ContentTop;
            GUI.Box(new Rect(x, y, w, 58f), "", panelStyle);
            GUI.Label(new Rect(x + 12f, y + 8f, w - 24f, 42f), toast, labelStyle);
        }

        private void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = UIHelper.GetCachedTex(new Color(0.04f, 0.08f, 0.12f, 0.94f));
            panelStyle.padding = new RectOffset(8, 8, 8, 8);

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = new Color(0.48f, 1f, 0.62f, 1f);

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17, alignment = TextAnchor.MiddleLeft, wordWrap = true
            };
            labelStyle.normal.textColor = Color.white;

            smallStyle = new GUIStyle(labelStyle) { fontSize = 15 };
            buttonStyle = MakeButtonStyle(new Color(0.12f, 0.46f, 0.3f, 1f));
            dangerStyle = MakeButtonStyle(new Color(0.55f, 0.15f, 0.18f, 1f));
            disabledStyle = MakeButtonStyle(new Color(0.25f, 0.27f, 0.3f, 1f));
            fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 19, padding = new RectOffset(12, 12, 8, 8)
            };
            fieldStyle.normal.textColor = Color.white;
            fieldStyle.normal.background = UIHelper.GetCachedTex(new Color(0.08f, 0.13f, 0.18f, 1f));
            fieldStyle.focused.background = UIHelper.GetCachedTex(new Color(0.1f, 0.2f, 0.22f, 1f));
            fieldStyle.focused.textColor = Color.white;
        }

        private static GUIStyle MakeButtonStyle(Color color)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            // Color * float는 알파까지 곱한다 — 그대로 쓰면 눌림 상태(0.82)가 반투명해진다.
            // 명도만 조절하고 알파는 보존한다.
            style.normal.background = UIHelper.GetCachedTex(color);
            style.hover.background = UIHelper.GetCachedTex(
                new Color(color.r * 1.12f, color.g * 1.12f, color.b * 1.12f, color.a));
            style.active.background = UIHelper.GetCachedTex(
                new Color(color.r * 0.82f, color.g * 0.82f, color.b * 0.82f, color.a));
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            return style;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                if (value == null) return hash;
                for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
                return hash;
            }
        }
    }
}
