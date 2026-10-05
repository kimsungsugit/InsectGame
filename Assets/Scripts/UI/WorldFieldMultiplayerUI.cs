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
        // 남은 표시 시간 — 가운데 무대에서 차례를 기다리거나 화면이 가려진 동안은 줄지 않는다.
        private float toastRemaining;
        private RegionManager regionManager;
        private bool regionSearched;
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
            TickToast();
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
            toastRemaining = 3.5f;
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
            // 다른 HUD와 같은 가상 캔버스(1920×1080 / 1080×1920)에 그린다 — 예전엔 이 화면만 픽셀 좌표라 스케일이 1이 아닌
            // 화면에서 혼자 크기가 달랐고, 자리를 다른 HUD의 순수 배치와 맞춰 볼 수 없었다(겹침 전수 검사가 이 좌표를 읽는다).
            UIScale.Begin();
            try
            {
                DrawScaled(HudFrame.Current);
            }
            finally
            {
                UIScale.End();
            }
        }

        private void DrawScaled(HudFrame f)
        {
            // 자기 창(대화 입력·친구 초대)이 열려 있으면 그 창만 그린다 — 필드 HUD는 다른 모달 때처럼 물러난다.
            if (IsOpen)
            {
                if (chatOpen)
                {
                    // 대상은 nearestPlayer가 아니라 고정 uid로 해석한다. 대상이 사라지면
                    // 입력창만 감추는 게 아니라 모달을 닫아야 입력 잠금이 풀린다.
                    WorldPlayer chatTarget = ResolveChatTarget();
                    if (chatTarget != null) DrawChatComposer(f, chatTarget);
                    else CloseModal();
                }
                if (friendsOpen) DrawFriendInvitePanel(f);
                // 자기 창에서 한 일(초대 보냄·실패)의 안내는 창 위에서 바로 보인다 — 창과 겹치지 않는 자리(ModalToastRect).
                if (toastRemaining > 0f && IsOpen) DrawToastAt(ModalToastRect(f, chatOpen));
                return;
            }
            if (!FieldVisible()) return;

            if (manager.IsJoined)
            {
                DrawFieldStatus(f);
                DrawMessages(f);
                // 근처 탐험가 패널은 가운데 행동 자리(대화 버튼과 같은 자리)에 선다 — 대화 버튼이 섰거나 서 있는 카드와 겹치면 비켜선다.
                if (nearestPlayer != null && !HudPresence.IsShowing(HudPresenceItem.Talk) && !HudStage.OccupiedOver(NearbyRect(f)))
                    DrawNearbyInteraction(f, nearestPlayer);
            }
            // 초대·안내는 가운데 무대(HudStage)의 차례 항목이다 — 앞 차례가 서 있으면 기다린다(안내는 시간도 멈춘다).
            if (invites.Count > 0 && HudStage.Request(HudStageItem.NetInvite)) DrawInvitePopup(f, invites[0]);
            if (toastRemaining > 0f && HudStage.Request(HudStageItem.NetToast)) DrawToast(f);
        }

        /// <summary>
        /// 필드 HUD를 그릴 수 있는가 — 다른 창·조작 잠금(포획·전투)·「챔피언의 꿈」·나의 섬에서는 물러난다.
        /// 예전엔 아무 조건 없이 그려 상점·도감 위에도, 섬 화면의 버튼 줄 위에도 이 패널이 떴다.
        /// </summary>
        private bool FieldVisible()
        {
            if (DreamPrologueState.Active) return false;
            if (ModalUIRegistry.IsAnyOpen()) return false;
            if (localPlayer != null && localPlayer.IsFrozen) return false;
            return !OnIsland;
        }

        private bool OnIsland
        {
            get
            {
                if (regionManager == null && !regionSearched)
                {
                    regionSearched = true;   // 한 번만 찾는다(없는 씬에서 매 프레임 훑지 않게)
                    regionManager = FindFirstObjectByType<RegionManager>();
                }
                InsectGame.Data.SubAreaData area = regionManager != null ? regionManager.CurrentSubArea : null;
                return area != null && area.detached;
            }
        }

        // 서 있는 동안만 줄어든다 — 무대 차례를 기다리거나 화면이 가려진 동안은 멈춘다(못 보고 지나가지 않게).
        private void TickToast()
        {
            if (toastRemaining <= 0f) return;
            if (IsOpen)
            {
                // 자기 창(대화 입력·친구 초대)이 열려 있으면 그 창 곁에 바로 띄운다 — 무대 차례와 무관하다.
                toastRemaining -= Time.unscaledDeltaTime;
                return;
            }
            if (!FieldVisible()) return;
            if (!HudStage.Request(HudStageItem.NetToast)) return;
            toastRemaining -= Time.unscaledDeltaTime;
        }

        /// <summary>
        /// 이 패널 위의 탭이 월드 클릭-이동으로 새지 않게 등록한다.
        ///
        /// <b>왜 필요한가.</b> <c>PlayerMovement</c>는 <c>Input.GetMouseButtonDown(0)</c>을 Update에서
        /// 따로 폴링한다. 탭한 프레임엔 아직 모달이 안 열려 <c>IsAnyOpen()</c>이 false이고, IMGUI라
        /// <c>pointerOverUI</c>도 false다. 등록이 없으면 버튼 아래 월드 지점이 클릭 목표로 잡혀
        /// "3:3 대전"을 누른 순간 캐릭터가 상대 뒤로 걸어간다. 같은 결함을 `QuickAccessBarUI`가
        /// P0으로 겪었고 `CaptureInputController`는 처음부터 등록하고 있었다.
        /// 이 화면은 가상 캔버스(<c>UIScale.Begin()</c>)에 그리므로 Rect를 그대로 넘긴다.
        /// </summary>
        private static void BlockFieldClicks(Rect virtualRect)
        {
            FieldHudInput.RegisterBlockingRect(virtualRect);
        }

        // ── 자리(순수 계산 — 겹침 전수 검사가 읽는다) ──

        public const float StatusWidth = 360f;
        public const float StatusHeight = 122f;
        public const float MessagesWidth = 470f;
        public const float MessagesMaxHeight = 190f;
        public const float NearbyHeight = 132f;

        /// <summary>
        /// 필드 상태 판(필드 이름·인원·초대 버튼). <b>데스크톱</b>·<b>세로 모바일</b>: 오른쪽 열의 시각 알림 자리 아래.
        /// <b>가로 모바일</b>: 우상단 단축 바 왼쪽. 예전엔 화면 오른쪽 위(ContentTop)라 포획 아이템 패널·시각 칩·단축 바를 덮었다.
        /// </summary>
        public static Rect StatusRect(HudFrame f)
        {
            float s = UITheme.Space.S;
            float w = Mathf.Min(StatusWidth, f.ContentWidth);
            Rect bar = QuickAccessBarUI.ShortcutBarRectFor(f);
            if (f.Mobile && !f.Portrait)
                return new Rect(bar.x - s - w, f.ContentTop, w, StatusHeight);
            Rect notice = WorldClockRules.NoticeBelow(f, WorldClockRules.FieldChip(f));
            float x = f.Mobile ? bar.xMax - w : f.Width - f.SafeRight - 20f - w;
            return new Rect(x, notice.yMax + s, w, StatusHeight);
        }

        /// <summary>
        /// 대화 기록 판(최대 크기 — 줄 수가 적으면 아래가 짧아진다). <b>데스크톱</b>: 왼쪽 상태 판 아래.
        /// <b>가로 모바일</b>: 필드 상태 판 바로 아래(오른쪽 맞춤) — 왼쪽은 미니맵·퀘스트 칩 아래가 곧 조이스틱 자리다.
        /// <b>세로 모바일</b>: 가운데 무대 왼쪽 위 — 무대에 카드가 서면 비켜선다.
        /// 예전엔 화면 아래(BottomY−150)라 잡기 버튼 줄·조이스틱 자리·퀘스트 칩과 겹쳤다.
        /// </summary>
        public static Rect MessagesRect(HudFrame f)
        {
            float s = UITheme.Space.S;
            float w = Mathf.Min(MessagesWidth, f.ContentWidth);
            if (f.Mobile && !f.Portrait)
            {
                Rect status = StatusRect(f);
                return new Rect(status.xMax - w, status.yMax + s, w, MessagesMaxHeight);
            }
            float x = f.SafeLeft + 16f;
            float y = f.Mobile ? HudStage.Area(f).y : PlayerStatusHUD.PanelRect(f).yMax + s;
            return new Rect(x, y, w, MessagesMaxHeight);
        }

        /// <summary>근처 탐험가 패널 — 가운데 대화 버튼 자리(<see cref="WorldInteractionController.TalkRect"/>)와 같은 폭·윗변.</summary>
        public static Rect NearbyRect(HudFrame f)
        {
            Rect talk = WorldInteractionController.TalkRect(f);
            float y = Mathf.Min(talk.y, f.ContentBottom - NearbyHeight);
            return new Rect(talk.x, y, talk.width, NearbyHeight);
        }

        /// <summary>필드 초대 — 가운데 무대의 차례 항목.</summary>
        public static Rect InviteRect(HudFrame f) => HudStage.Place(f, HudStageItem.NetInvite, 520f, 190f);

        /// <summary>필드 멀티 안내 — 가운데 무대의 차례 항목.</summary>
        public static Rect ToastRect(HudFrame f) => HudStage.Place(f, HudStageItem.NetToast, 560f, 58f);

        public const float ComposerHeight = 124f;
        public const float FriendPanelHeight = 520f;

        /// <summary>
        /// 자기 창이 열려 있을 때의 안내 자리 — 창과 겹치지 않게. 대화 입력(가운데)은 그 바로 위, 친구 초대(위쪽)는 그 바로 아래.
        /// 예전엔 화면 위 가운데(ContentTop)라 친구 초대 창의 제목 줄을 덮었다.
        /// </summary>
        public static Rect ModalToastRect(HudFrame f, bool chat)
        {
            const float h = 58f;
            float w = Mathf.Min(560f, f.ContentWidth);
            float x = f.ContentLeft + (f.ContentWidth - w) * 0.5f;
            float y = chat
                ? Mathf.Max(f.ContentTop, f.CenteredY(ComposerHeight) - UITheme.Space.S - h)
                : Mathf.Min(f.ContentTop + f.ClampHeight(FriendPanelHeight) + UITheme.Space.S, f.ContentBottom - h);
            return new Rect(x, y, w, h);
        }

        // ── 그리기 ──

        private void DrawFieldStatus(HudFrame f)
        {
            Rect r = StatusRect(f);
            float x = r.x, y = r.y, w = r.width;
            BlockFieldClicks(r);   // 탭이 월드 클릭-이동으로 새지 않게
            GUI.Box(r, "", panelStyle);
            UIHelper.LabelFit(new Rect(x + 12f, y + 8f, w - 24f, 36f), cachedWorldTitle, titleStyle);
            if (GUI.Button(new Rect(x + 14f, y + 54f, w - 28f, 56f), "친구를 이 필드로 초대", buttonStyle))
            {
                bool opening = !friendsOpen;
                CloseModal();
                friendsOpen = opening;
                if (friendsOpen) ModalUIRegistry.Register(this);
            }
        }

        private void DrawNearbyInteraction(HudFrame f, WorldPlayer player)
        {
            Rect r = NearbyRect(f);
            float x = r.x, y = r.y, w = r.width;
            BlockFieldClicks(r);   // 탭이 월드 클릭-이동으로 새지 않게
            HudPresence.Mark(HudPresenceItem.Nearby);   // 코치 배너가 겹치면 비켜 준다
            GUI.Box(r, "", panelStyle);
            UIHelper.LabelFit(new Rect(x + 18f, y + 8f, w - 36f, 36f), cachedNearbyLabel, titleStyle);

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

        private void DrawChatComposer(HudFrame f, WorldPlayer player)
        {
            float w = Mathf.Min(620f, f.ContentWidth);
            float h = ComposerHeight;
            float x = f.ContentLeft + (f.ContentWidth - w) * 0.5f;
            float y = f.CenteredY(h);
            BlockFieldClicks(new Rect(x, y, w, h));   // 탭이 월드 클릭-이동으로 새지 않게
            GUI.Box(new Rect(x, y, w, h), "", panelStyle);
            UIHelper.LabelFit(new Rect(x + 14f, y + 6f, w - 28f, 34f), player.displayName + "에게 말하기", titleStyle);
            chatInput = GUI.TextField(new Rect(x + 14f, y + 46f, w - 150f, 60f), chatInput, 80, fieldStyle);
            if (GUI.Button(new Rect(x + w - 126f, y + 46f, 112f, 60f), "보내기", buttonStyle))
            {
                // player는 ResolveChatTarget이 chatTargetUid로 해석한 고정 대상이다.
                manager.SendPrivateChat(player.uid, chatInput);
                CloseModal();
            }
        }

        private void DrawMessages(HudFrame f)
        {
            if (messages.Count == 0) return;
            Rect slot = MessagesRect(f);
            // 상태 판을 펼치면 겹치는 기록은 비켜선다(가로 모바일 — 미니맵 아래 자리를 펼친 판이 덮는다).
            if (MinimapUI.LeftStackOccluded && slot.Overlaps(PlayerStatusHUD.PanelRect(f))) return;
            // 세로 모바일에서는 무대 안이라 카드가 서면 비켜선다.
            float h = Mathf.Min(MessagesMaxHeight, messages.Count * 34f + 20f);
            Rect r = new Rect(slot.x, slot.y, slot.width, h);
            if (f.Mobile && f.Portrait && HudStage.OccupiedOver(r)) return;
            BlockFieldClicks(r);   // 불투명 판 — 위의 탭이 월드 클릭-이동으로 새지 않게
            GUI.Box(r, "", panelStyle);
            int first = Mathf.Max(0, messageLines.Count - 5);
            for (int i = first; i < messageLines.Count; i++)
            {
                UIHelper.LabelFit(new Rect(r.x + 12f, r.y + 8f + (i - first) * 34f, r.width - 24f, 32f),
                    messageLines[i], smallStyle);
            }
        }

        private void DrawFriendInvitePanel(HudFrame f)
        {
            float w = Mathf.Min(520f, f.ContentWidth);
            float h = f.ClampHeight(FriendPanelHeight);
            float x = f.ContentLeft + (f.ContentWidth - w) * 0.5f;
            float y = f.ContentTop;
            BlockFieldClicks(new Rect(x, y, w, h));   // 탭이 월드 클릭-이동으로 새지 않게
            GUI.Box(new Rect(x, y, w, h), "", panelStyle);
            GUI.Label(new Rect(x + 16f, y + 12f, w - 100f, 36f), "친구 필드 초대", titleStyle);
            if (GUI.Button(new Rect(x + w - 72f, y + 8f, 58f, 56f), "X", dangerStyle)) CloseModal();

            PvpProfileSnapshot[] friends = SocialPvpManager.Instance != null
                ? SocialPvpManager.Instance.State.friends
                : Array.Empty<PvpProfileSnapshot>();
            Rect view = new Rect(x + 16f, y + 72f, w - 32f, Mathf.Max(1f, h - 88f));
            float contentH = Mathf.Max(view.height, friends.Length * 76f);
            friendDirectScroll.Handle(ref friendScroll, view, contentH, 38f);
            friendScroll = GUI.BeginScrollView(view, friendScroll, new Rect(0f, 0f, view.width - 18f, contentH));
            if (friends.Length == 0)
                UIHelper.LabelFit(new Rect(8f, 20f, view.width - 40f, 64f), "친구 목록이 비어 있습니다.\n소셜 메뉴에서 친구를 먼저 추가하세요.", labelStyle);
            for (int i = 0; i < friends.Length; i++)
            {
                PvpProfileSnapshot friend = friends[i];
                float rowY = i * 76f;
                UIHelper.LabelFit(new Rect(8f, rowY + 8f, view.width - 176f, 56f),
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

        private void DrawInvitePopup(HudFrame f, WorldInviteSnapshot invite)
        {
            Rect r = InviteRect(f);
            HudStage.Request(HudStageItem.NetInvite, r);
            float x = r.x, y = r.y, w = r.width;
            BlockFieldClicks(r);   // 탭이 월드 클릭-이동으로 새지 않게
            GUI.Box(r, "", panelStyle);
            GUI.Label(new Rect(x + 16f, y + 10f, w - 32f, 36f), "필드 초대", titleStyle);
            UIHelper.LabelFit(new Rect(x + 16f, y + 50f, w - 32f, 54f),
                $"{invite.displayName}님이 {invite.worldName}에 초대했습니다.", labelStyle);
            float btnW = (w - 48f) * 0.5f;
            if (GUI.Button(new Rect(x + 16f, y + 116f, btnW, 58f), "함께 입장", buttonStyle))
                manager.RespondInvite(invite.inviteId, true);
            if (GUI.Button(new Rect(x + 32f + btnW, y + 116f, btnW, 58f), "거절", dangerStyle))
                manager.RespondInvite(invite.inviteId, false);
        }

        private void DrawToast(HudFrame f)
        {
            Rect r = ToastRect(f);
            HudStage.Request(HudStageItem.NetToast, r);
            DrawToastAt(r);
        }

        private void DrawToastAt(Rect r)
        {
            GUI.Box(r, "", panelStyle);
            UIHelper.LabelFit(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, r.height - 12f), toast, labelStyle);
        }

        private void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = UIHelper.GetCachedTex(new Color(0.04f, 0.08f, 0.12f, 0.94f));
            panelStyle.padding = new RectOffset(8, 8, 8, 8);

            // 글자 크기는 가상 캔버스 기준이다(다른 HUD와 같은 단위 — 1920×1080에서 예전 픽셀 크기보다 조금 크다).
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = new Color(0.48f, 1f, 0.62f, 1f);

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, alignment = TextAnchor.MiddleLeft, wordWrap = true
            };
            labelStyle.normal.textColor = Color.white;

            smallStyle = new GUIStyle(labelStyle) { fontSize = 20 };
            buttonStyle = MakeButtonStyle(new Color(0.12f, 0.46f, 0.3f, 1f));
            dangerStyle = MakeButtonStyle(new Color(0.55f, 0.15f, 0.18f, 1f));
            disabledStyle = MakeButtonStyle(new Color(0.25f, 0.27f, 0.3f, 1f));
            fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 24, padding = new RectOffset(12, 12, 8, 8)
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
                fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
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
