using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 섬 나들목 — 내 섬으로 가기/나가기, 내 섬 공개 설정과 섬 코드, 친구·섬 코드로 남의 섬 방문.
    /// 탐험 메뉴의 [내 섬], 본 마을 나루터, 섬 HUD의 [방문]이 전부 이 창을 연다.
    /// </summary>
    public class IslandVisitUI : MonoBehaviour, IModalUI
    {
        private const float FeedbackSeconds = 3f;
        private const int CodeLength = 8;

        [SerializeField] private IslandManager island;
        [SerializeField] private IslandWorldBuilder world;
        [SerializeField] private IslandShareClient share;
        [SerializeField] private SocialPvpManager social;
        [SerializeField] private IslandHudUI hud;

        private bool isOpen;
        private string codeInput = string.Empty;
        private Vector2 friendScroll;
        private readonly UIDirectScroll directScroll = new UIDirectScroll();
        private string feedback;
        private float feedbackUntil;

        // 좋아요·방문 수 문구 — 숫자가 바뀔 때만 다시 잇는다.
        private string statsLabel = string.Empty;
        private int statsKey = -1;

        public bool IsOpen => isOpen;

        /// <summary>탐험 메뉴의 [내 섬]에 점을 찍을 일이 있는가 — 열렸는데 아직 안 가 봤거나 수확물이 가득 찼다.</summary>
        public bool NeedsAttention =>
            island != null && island.IsUnlocked && (!island.HasEnteredOnce || island.IsHarvestFull);

        public void AutoWire(IslandManager islandManager, IslandWorldBuilder worldBuilder,
            IslandShareClient shareClient, SocialPvpManager socialPvp, IslandHudUI hudUi)
        {
            if (island == null) island = islandManager;
            if (world == null) world = worldBuilder;
            if (share == null) share = shareClient;
            if (social == null) social = socialPvp;
            if (hud == null) hud = hudUi;
        }

        public void Toggle()
        {
            if (isOpen) { CloseModal(); return; }
            if (island == null || world == null) return;
            isOpen = true;
            friendScroll = Vector2.zero;
            feedback = null;
            directScroll.Reset();
            ModalUIRegistry.Register(this);
            if (share != null)
            {
                share.ClearError();
                share.RefreshMyInfo();
            }
            // 친구 목록은 PVP 창을 한 번도 안 열었으면 비어 있다 — 여기서도 받아 온다(서버가 없으면 조용히 실패한다).
            if (social != null && FirebaseConfig.IsSocialPvpConfigured) social.RefreshAll();
        }

        public void CloseModal()
        {
            isOpen = false;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }

        private void OnDisable() => CloseModal();

        private void OnGUI()
        {
            if (!isOpen || island == null || world == null) return;
            if (!ReferenceEquals(ModalUIRegistry.TopModal, this)) return;

            UIScale.Begin();
            UISurface.Dim(0.6f);
            UITheme t = UITheme.Instance;
            bool mobile = UIScale.IsMobileLayout;
            Rect panel = UISafeLayout.CenteredPanel(mobile ? 1000f : 920f, mobile ? 1500f : 960f);
            UISurface.Card(panel);
            if (UISurface.Header(new Rect(panel.x + 3f, panel.y + 3f, panel.width - 6f, 84f), "내 섬", string.Empty))
            {
                CloseModal();
                UIScale.End();
                return;
            }

            float x = panel.x + 20f;
            float w = panel.width - 40f;
            float y = panel.y + 100f;

            Rect travel = new Rect(x, y, w, 168f);
            DrawTravelCard(travel, t);
            y = travel.yMax + 12f;

            Rect mine = new Rect(x, y, w, 168f);
            DrawMyIslandCard(mine, t);
            y = mine.yMax + 12f;

            Rect visit = new Rect(x, y, w, Mathf.Max(1f, panel.yMax - 70f - y));
            DrawVisitCard(visit, t);

            DrawStatus(new Rect(x, panel.yMax - 62f, w, 48f), t);
            UIScale.End();
        }

        private void DrawTravelCard(Rect r, UITheme t)
        {
            UISurface.Card(r, t.surfaceCard, t.surfaceBorder);
            bool unlocked = island.IsUnlocked;
            string line;
            if (world.Mode == IslandMode.Own) line = "지금 내 섬에 있습니다.";
            else if (world.Mode == IslandMode.Visit) line = "지금 다른 사람의 섬을 구경하고 있습니다.";
            else if (!unlocked) line = "「곤충 수집가」 퀘스트를 마치면 섬이 열립니다.";
            else line = "곤충을 풀어놓고 꾸미는 나만의 섬입니다.";
            IslandUiKit.Label(new Rect(r.x + 20f, r.y + 10f, r.width - 40f, 44f), line, IslandUiKit.Body, t.textSecondary);

            Rect left = new Rect(r.x + 20f, r.yMax - 88f, (r.width - 52f) * 0.5f, 72f);
            Rect right = new Rect(left.xMax + 12f, left.y, left.width, 72f);
            Rect full = new Rect(r.x + 20f, r.yMax - 88f, r.width - 40f, 72f);
            bool enabled = GUI.enabled;

            switch (world.Mode)
            {
                case IslandMode.None:
                    GUI.enabled = enabled && unlocked;
                    if (UISurface.Button(full, "내 섬으로 가기", t.accentMint, IslandUiKit.Button)) GoToOwnIsland();
                    break;
                case IslandMode.Own:
                    if (UISurface.Button(full, "메인 월드로 나가기", t.surfaceRaised, IslandUiKit.Button))
                    {
                        CloseModal();
                        world.ExitIsland();
                    }
                    break;
                default:
                    if (UISurface.Button(left, "내 섬으로 돌아가기", t.accentMint, IslandUiKit.Button))
                    {
                        CloseModal();
                        if (hud != null) hud.SetVisitInfo(null);
                        world.ReturnToOwnIsland();
                    }
                    if (UISurface.Button(right, "메인 월드로 나가기", t.surfaceRaised, IslandUiKit.Button))
                    {
                        CloseModal();
                        if (hud != null) hud.SetVisitInfo(null);
                        world.ExitIsland();
                    }
                    break;
            }
            GUI.enabled = enabled;
        }

        private void GoToOwnIsland()
        {
            if (!world.CanTravel(out string reason))
            {
                ShowFeedback(reason);
                return;
            }
            CloseModal();
            world.EnterOwnIsland();
        }

        private void DrawMyIslandCard(Rect r, UITheme t)
        {
            UISurface.Card(r, t.surfaceCard, t.surfaceBorder);
            IslandInfo info = share != null ? share.MyInfo : null;
            string code = info != null && !string.IsNullOrEmpty(info.friendCode) ? info.friendCode : "—";
            IslandUiKit.Label(new Rect(r.x + 20f, r.y + 10f, 200f, 40f), "내 섬 코드", IslandUiKit.Body, t.textSecondary);
            IslandUiKit.Label(new Rect(r.x + 220f, r.y + 6f, 300f, 48f), code, IslandUiKit.Title, t.accentAmber);

            int likes = info != null ? info.likes : 0;
            int visits = info != null ? info.visits : 0;
            int key = likes * 100003 + visits;
            if (key != statsKey)
            {
                statsKey = key;
                statsLabel = "♥ " + likes + "  ·  방문 " + visits;
            }
            IslandUiKit.Label(new Rect(r.xMax - 320f, r.y + 10f, 300f, 40f), statsLabel, IslandUiKit.Body, t.accentCoral);

            // 공개 설정은 서버가 없어도 저장된다(다음에 올릴 때 반영). 그래서 서버 상태와 무관하게 누를 수 있다.
            bool isPublic = island.IsPublic;
            Rect toggle = new Rect(r.x + 20f, r.yMax - 88f, r.width - 40f, 72f);
            if (UISurface.Button(toggle, isPublic ? "공개 중 — 누르면 아무도 들어올 수 없게 닫습니다"
                    : "비공개 — 누르면 다른 사람이 구경할 수 있게 엽니다",
                    isPublic ? t.surfaceRaised : t.surfaceBase, IslandUiKit.ButtonSmall))
            {
                island.SetPublic(!isPublic);
                if (share != null) share.Publish();
            }
        }

        private void DrawVisitCard(Rect r, UITheme t)
        {
            UISurface.Card(r, t.surfaceCard, t.surfaceBorder);
            IslandUiKit.Label(new Rect(r.x + 20f, r.y + 8f, r.width - 40f, 40f), "다른 섬 구경하기", IslandUiKit.Title, t.textPrimary);

            bool busy = share != null && share.IsBusy;
            const float btnW = 170f;
            Rect input = new Rect(r.x + 20f, r.y + 56f, r.width - 40f - btnW - 12f, 60f);
            codeInput = GUI.TextField(input, codeInput ?? string.Empty, CodeLength, TextFieldStyle());
            if (string.IsNullOrEmpty(codeInput))
                IslandUiKit.Label(new Rect(input.x + 14f, input.y, input.width - 28f, input.height), "섬 코드 8자리",
                    IslandUiKit.Body, t.textMuted);

            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !busy && !string.IsNullOrWhiteSpace(codeInput);
            if (UISurface.Button(new Rect(input.xMax + 12f, input.y, btnW, 60f), "방문", t.accentMint, IslandUiKit.Button))
                VisitByCode(codeInput);
            GUI.enabled = enabled;

            Rect listArea = new Rect(r.x + 20f, r.y + 128f, r.width - 40f, Mathf.Max(1f, r.height - 140f));
            PvpProfileSnapshot[] friends = social != null && social.State != null ? social.State.friends : null;
            if (friends == null || friends.Length == 0)
            {
                IslandUiKit.Label(listArea, "친구가 있으면 여기서 바로 섬을 구경할 수 있어요. 친구는 탐험 메뉴의 [PVP]에서 추가합니다.",
                    IslandUiKit.BodyWrap, t.textMuted);
                return;
            }

            const float rowH = 76f;
            const float gap = 8f;
            float contentH = friends.Length * (rowH + gap);
            Rect view = new Rect(0f, 0f, listArea.width - 16f, contentH);
            directScroll.Handle(ref friendScroll, listArea, contentH, rowH * 0.5f);
            friendScroll = GUI.BeginScrollView(listArea, friendScroll, view, GUIStyle.none, GUIStyle.none);
            for (int i = 0; i < friends.Length; i++)
            {
                PvpProfileSnapshot friend = friends[i];
                if (friend == null) continue;
                Rect row = new Rect(0f, i * (rowH + gap), view.width, rowH);
                UISurface.Rounded(row, t.surfaceRaised);
                IslandUiKit.Label(new Rect(row.x + 16f, row.y + 8f, row.width - 32f - btnW - 12f, rowH - 16f),
                    friend.displayName, IslandUiKit.Body, t.textPrimary);
                GUI.enabled = enabled && !busy && !directScroll.IsDragging;
                if (UISurface.Button(new Rect(row.xMax - btnW - 8f, row.y + 8f, btnW, rowH - 16f), "섬 구경", t.accentMint,
                        IslandUiKit.ButtonSmall))
                    VisitByUid(friend.uid);
                GUI.enabled = enabled;
            }
            GUI.EndScrollView();
            UISurface.ScrollAffordance(listArea, friendScroll, contentH, t.accentMint);
        }

        private static GUIStyle TextFieldStyle()
        {
            return UIHelper.CachedStyle("island_textfield", () => new GUIStyle(GUI.skin.textField)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 14, 8, 8),
            });
        }

        private void VisitByCode(string code)
        {
            if (share == null) return;
            if (!world.CanTravel(out string reason)) { ShowFeedback(reason); return; }
            share.FetchByCode(code, OnFetched);
        }

        private void VisitByUid(string uid)
        {
            if (share == null) return;
            if (!world.CanTravel(out string reason)) { ShowFeedback(reason); return; }
            share.FetchByUid(uid, OnFetched);
        }

        private void OnFetched(IslandInfo info, IslandSnapshot snapshot)
        {
            // 실패 문구는 share.LastError가 들고 있다(DrawStatus가 그린다).
            if (info == null || snapshot == null) return;

            // 내 코드를 넣었다면 구경이 아니라 내 섬이다 — 구경 모드로 들어가면 꾸미기·수확이 다 막힌 내 섬을 보게 된다.
            AuthManager auth = AuthManager.Instance;
            if (auth != null && !string.IsNullOrEmpty(info.ownerUid) && info.ownerUid == auth.UserId)
            {
                if (island.IsUnlocked) GoToOwnIsland();
                return;
            }

            // 응답을 기다리는 사이에 창을 닫았거나 다른 곳으로 갔을 수 있다.
            if (!world.CanTravel(out string reason))
            {
                ShowFeedback(reason);
                return;
            }
            if (hud != null) hud.SetVisitInfo(info);
            CloseModal();
            world.EnterVisit(snapshot);
        }

        private void DrawStatus(Rect r, UITheme t)
        {
            if (!string.IsNullOrEmpty(feedback) && Time.unscaledTime < feedbackUntil)
            {
                IslandUiKit.Label(r, feedback, IslandUiKit.BodyCenter, t.accentAmber);
                return;
            }
            if (share == null) return;
            if (share.IsBusy)
                IslandUiKit.Label(r, "불러오는 중…", IslandUiKit.BodyCenter, t.textSecondary);
            else if (!string.IsNullOrEmpty(share.LastError))
                IslandUiKit.Label(r, share.LastError, IslandUiKit.BodyCenter, t.textMuted);
        }

        private void ShowFeedback(string message)
        {
            feedback = message;
            feedbackUntil = Time.unscaledTime + FeedbackSeconds;
        }
    }
}
