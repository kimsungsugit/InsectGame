using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 섬 위에서만 뜨는 버튼 줄 — 꾸미기·상점·곤충·수확·방문·도움말·나가기. 남의 섬에서는 좋아요·돌아가기만 남는다.
    ///
    /// <b>모달이 아니다</b>(캐릭터가 걸어다니는 동안 떠 있다). 그래서 매 OnGUI마다 자기 자리를
    /// <see cref="FieldHudInput.RegisterBlockingRect"/>에 등록한다 — 안 하면 버튼을 누른 탭이 클릭-이동으로 새어
    /// 캐릭터가 버튼 아래 지점으로 걸어간다(rules/ui-layout.md, 같은 결함이 네 번 났다).
    /// </summary>
    public class IslandHudUI : MonoBehaviour
    {
        private const float ToastSeconds = 3f;

        [SerializeField] private IslandManager island;
        [SerializeField] private IslandWorldBuilder world;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private IslandShareClient share;
        [SerializeField] private IslandEditUI editUI;
        [SerializeField] private IslandShopUI shopUI;
        [SerializeField] private IslandInsectUI insectUI;
        [SerializeField] private IslandVisitUI visitUI;
        [SerializeField] private IslandGuideUI guideUI;

        private IslandInfo visitInfo;
        private string toast;
        private float toastUntil;

        // 매 프레임 문자열을 새로 잇지 않게, 보이는 숫자가 바뀔 때만 다시 만든다.
        private string harvestLabel = "수확";
        private int harvestCandyShown = -1;
        private int harvestCoinShown = -1;
        private string infoLine = string.Empty;
        private int infoKey = -1;
        private string visitLine = string.Empty;
        private int visitKey = -1;

        public void AutoWire(IslandManager islandManager, IslandWorldBuilder worldBuilder, PlayerMovement movement,
            IslandShareClient shareClient)
        {
            if (island == null) island = islandManager;
            if (world == null) world = worldBuilder;
            if (playerMovement == null) playerMovement = movement;
            if (share == null) share = shareClient;
        }

        public void AutoWire(IslandEditUI edit, IslandShopUI shop, IslandInsectUI insects, IslandVisitUI visit,
            IslandGuideUI guide)
        {
            if (editUI == null) editUI = edit;
            if (shopUI == null) shopUI = shop;
            if (insectUI == null) insectUI = insects;
            if (visitUI == null) visitUI = visit;
            if (guideUI == null) guideUI = guide;
        }

        /// <summary>남의 섬에 들어올 때 방문 창이 넘겨 준다 — 주인·좋아요 수 표시와 좋아요 요청에 쓴다.</summary>
        public void SetVisitInfo(IslandInfo info)
        {
            visitInfo = info;
            visitKey = -1;
        }

        public void ShowToast(string message)
        {
            toast = message;
            toastUntil = Time.unscaledTime + ToastSeconds;
        }

        private void OnGUI()
        {
            if (world == null || island == null || !world.IsOnIsland) return;
            if (ModalUIRegistry.IsAnyOpen()) return;
            if (playerMovement != null && playerMovement.IsFrozen) return;

            UIScale.Begin();
            if (world.IsVisiting) DrawVisitBar();
            else DrawOwnBar();
            DrawToast();
            UIScale.End();
        }

        private void DrawOwnBar()
        {
            island.Peek(out float pendingCandy, out float pendingCoin, out float accrued, out float cap);
            int candy = Mathf.FloorToInt(pendingCandy);
            int coin = Mathf.FloorToInt(pendingCoin);
            if (candy != harvestCandyShown || coin != harvestCoinShown)
            {
                harvestCandyShown = candy;
                harvestCoinShown = coin;
                harvestLabel = candy > 0 || coin > 0 ? "수확  캔디 " + candy + " · 코인 " + coin : "수확";
            }
            // 쾌적도·누적 시간(0.1시간 단위)·상한이 바뀔 때만 다시 잇는다.
            int key = island.Effects.comfort * 100000 + Mathf.RoundToInt(accrued * 10f) * 100 + Mathf.RoundToInt(cap);
            if (key != infoKey)
            {
                infoKey = key;
                island.GetRates(out float candyRate, out float coinRate);
                infoLine = "쾌적도 " + island.Effects.comfort
                           + "  ·  시간당 캔디 " + candyRate.ToString("0.0") + " · 코인 " + coinRate.ToString("0.0")
                           + "  ·  쌓인 시간 " + accrued.ToString("0.0") + " / " + cap.ToString("0") + "시간";
            }

            bool canHarvest = candy > 0 || coin > 0;
            UITheme t = UITheme.Instance;

            if (UIScale.IsMobileLayout)
            {
                // 모바일: 퀵바(우상단) 바로 아래에 세로로 쌓는다. 좌하단 사분면은 가상 조이스틱의 시작 영역이라
                // 거기 버튼을 두면 누를 때마다 캐릭터가 움찔한다.
                Rect quick = QuickAccessBarUI.ShortcutBarRect;
                const float rowH = 72f;
                const float gap = 8f;
                Rect column = new Rect(quick.x, quick.yMax + 10f, quick.width,
                    UISafeLayout.ClampHeight(16f + 7f * rowH + 6f * gap));
                FieldHudInput.RegisterBlockingRect(column);
                UISurface.HudCard(column);
                float y = column.y + 8f;
                Rect Row() { var r = new Rect(column.x + 8f, y, column.width - 16f, rowH); y += rowH + gap; return r; }
                DrawOwnButtons(Row(), Row(), Row(), Row(), Row(), Row(), Row(), canHarvest, t);
                return;
            }

            // 데스크톱: 퀵바(하단 중앙) 바로 위에 한 줄.
            Rect bar = QuickAccessBarUI.ShortcutBarRect;
            const float h = 64f;
            Rect strip = new Rect(bar.x, bar.y - h - 16f - 10f, bar.width, h + 16f);
            Rect info = new Rect(strip.x, strip.y - 44f, strip.width, 40f);
            FieldHudInput.RegisterBlockingRect(strip);
            UISurface.HudCard(strip);
            UISurface.HudCard(info);
            IslandUiKit.Label(new Rect(info.x + 16f, info.y, info.width - 32f, info.height), infoLine,
                IslandUiKit.SmallCenter, t.textSecondary);

            const float cellGap = 6f;
            float harvestW = 340f;
            float cellW = (strip.width - 16f - harvestW - cellGap * 6f) / 6f;
            float x = strip.x + 8f;
            Rect Cell(float w) { var r = new Rect(x, strip.y + 8f, w, h); x += w + cellGap; return r; }
            DrawOwnButtons(Cell(cellW), Cell(cellW), Cell(cellW), Cell(harvestW), Cell(cellW), Cell(cellW), Cell(cellW),
                canHarvest, t);
        }

        private void DrawOwnButtons(Rect edit, Rect shop, Rect insects, Rect harvest, Rect visit, Rect help, Rect exit,
            bool canHarvest, UITheme t)
        {
            GUIStyle style = IslandUiKit.Button;
            if (UISurface.Button(edit, "꾸미기", t.surfaceRaised, style) && editUI != null) editUI.Open();
            if (UISurface.Button(shop, "상점", t.surfaceRaised, style) && shopUI != null) shopUI.Toggle();
            if (UISurface.Button(insects, "곤충", t.surfaceRaised, style) && insectUI != null) insectUI.Toggle();

            bool enabled = GUI.enabled;
            GUI.enabled = enabled && canHarvest;
            // 받을 게 있으면 민트로 눈에 띄게 — 섬에 들른 이유가 대개 이 버튼이다.
            if (UISurface.Button(harvest, harvestLabel, canHarvest ? t.accentMint : t.surfaceCard, style))
                DoHarvest();
            GUI.enabled = enabled;

            if (UISurface.Button(visit, "방문", t.surfaceRaised, style) && visitUI != null) visitUI.Toggle();
            if (UISurface.Button(help, "도움말", t.surfaceRaised, style) && guideUI != null) guideUI.OpenHelp();
            if (UISurface.Button(exit, "나가기", t.surfaceCard, style)) world.ExitIsland();
        }

        private void DoHarvest()
        {
            if (!island.Harvest(out int candy, out int coin)) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.ItemPickup);
            ShowToast("수확!  캔디 +" + candy + " · 코인 +" + coin);
        }

        private void DrawVisitBar()
        {
            UITheme t = UITheme.Instance;
            IslandSnapshot snapshot = world.VisitSnapshot;
            int likes = visitInfo != null ? visitInfo.likes : 0;
            int key = likes * 10000 + (snapshot != null ? snapshot.comfort : 0);
            if (key != visitKey)
            {
                visitKey = key;
                visitLine = (snapshot != null ? snapshot.ownerName : "누군가") + "의 섬  ·  쾌적도 "
                            + (snapshot != null ? snapshot.comfort : 0) + "  ·  ♥ " + likes;
            }

            bool mobile = UIScale.IsMobileLayout;
            Rect quick = QuickAccessBarUI.ShortcutBarRect;
            const float h = 64f;
            Rect panel = mobile
                ? new Rect(quick.x, quick.yMax + 10f, quick.width, 16f + 48f + 3f * (h + 8f))
                : new Rect(quick.x, quick.y - (h + 16f + 48f) - 10f, quick.width, h + 16f + 48f);
            FieldHudInput.RegisterBlockingRect(panel);
            UISurface.HudCard(panel);
            IslandUiKit.Label(new Rect(panel.x + 12f, panel.y + 6f, panel.width - 24f, 40f), visitLine,
                IslandUiKit.BodyCenter, t.textPrimary);

            Rect like, home, exit;
            if (mobile)
            {
                float y = panel.y + 52f;
                like = new Rect(panel.x + 8f, y, panel.width - 16f, h);
                home = new Rect(panel.x + 8f, y + h + 8f, panel.width - 16f, h);
                exit = new Rect(panel.x + 8f, y + (h + 8f) * 2f, panel.width - 16f, h);
            }
            else
            {
                float w = (panel.width - 16f - 12f) / 3f;
                float y = panel.y + 48f;
                like = new Rect(panel.x + 8f, y, w, h);
                home = new Rect(like.xMax + 6f, y, w, h);
                exit = new Rect(home.xMax + 6f, y, w, h);
            }

            GUIStyle style = IslandUiKit.Button;
            bool canLike = visitInfo != null && !visitInfo.likedByMe && share != null && !share.IsBusy;
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && canLike;
            string likeLabel = visitInfo != null && visitInfo.likedByMe ? "좋아요 완료" : "좋아요 ♥";
            if (UISurface.Button(like, likeLabel, t.accentCoral, style)) DoLike();
            GUI.enabled = enabled;

            if (UISurface.Button(home, "내 섬으로", t.surfaceRaised, style))
            {
                visitInfo = null;
                world.ReturnToOwnIsland();
            }
            if (UISurface.Button(exit, "나가기", t.surfaceCard, style))
            {
                visitInfo = null;
                world.ExitIsland();
            }
        }

        private void DoLike()
        {
            if (visitInfo == null || share == null) return;
            IslandInfo target = visitInfo;
            share.Like(target.ownerUid, result =>
            {
                if (result == null)
                {
                    ShowToast(string.IsNullOrEmpty(share.LastError) ? "좋아요를 보내지 못했습니다" : share.LastError);
                    return;
                }
                // 응답이 오기 전에 다른 섬으로 옮겨 갔을 수 있다 — 그 섬의 숫자를 덮지 않는다.
                if (!ReferenceEquals(visitInfo, target)) return;
                visitInfo.likes = result.likes;
                visitInfo.likedByMe = true;
                visitKey = -1;
                ShowToast("좋아요를 보냈습니다");
            });
        }

        private void DrawToast()
        {
            if (string.IsNullOrEmpty(toast) || Time.unscaledTime >= toastUntil) return;
            Rect r = UISafeLayout.TopPanel(Mathf.Min(760f, UISafeLayout.ContentWidth), 60f);
            r.y += 120f;
            UISurface.HudCard(r);
            IslandUiKit.Label(new Rect(r.x + 16f, r.y + 6f, r.width - 32f, r.height - 12f), toast,
                IslandUiKit.BodyCenter, UITheme.Instance.accentAmber);
        }
    }
}
