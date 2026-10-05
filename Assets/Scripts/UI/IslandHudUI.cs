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
    /// 자리는 <see cref="IslandHudLayout"/>이 정한다(데스크톱 한 줄 · 세로 모바일 열 · 가로 모바일 두 칸 판).
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
        // 남은 표시 시간 — 가운데 무대(HudStage)에서 차례를 기다리는 동안은 줄지 않는다.
        private float toastRemaining;

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
            toastRemaining = ToastSeconds;
        }

        private void Update()
        {
            // 섬 HUD가 이 화면에 선다고 알린다 — 가운데 무대(HudStage)가 그때만 섬 판을 피한다(필드에서 피하면 세로 화면의 카드가
            // 왼쪽 절반으로 밀렸다). OnGUI가 아니라 여기서 부르는 이유: Update가 모든 OnGUI보다 먼저 돌아 섬에 들어선 첫 프레임부터
            // 다른 화면의 카드가 같은 답을 보고, 모달·조작 잠금으로 판이 잠깐 숨어도 카드가 옆으로 뛰지 않는다(필드의 단축 바 등과 같다).
            // 꿈 섬은 판을 숨기므로 알리지 않는다.
            if (world != null && island != null && world.IsOnIsland && !world.DreamMode)
                HudPresence.Mark(HudPresenceItem.IslandHud);

            if (toastRemaining <= 0f) return;
            // 섬을 떠났으면 거둔다(섬 이야기다).
            if (world == null || !world.IsOnIsland) { toastRemaining = 0f; return; }
            // 무대 차례가 오고, 실제로 보이는 동안만 시간을 쓴다(창·프리즈가 덮었으면 기다린다).
            if (!HudStage.Request(HudStageItem.IslandToast)) return;
            if (ModalUIRegistry.IsAnyOpen() || (playerMovement != null && playerMovement.IsFrozen)) return;
            toastRemaining -= Time.unscaledDeltaTime;
        }

        private void OnGUI()
        {
            if (world == null || island == null || !world.IsOnIsland || world.DreamMode) return;
            if (ModalUIRegistry.IsAnyOpen()) return;
            if (playerMovement != null && playerMovement.IsFrozen) return;

            UIScale.Begin();
            HudFrame frame = HudFrame.Current;
            IslandHudForm form = IslandHudLayout.FormFor(frame);
            Rect quick = QuickAccessBarUI.ShortcutBarRectFor(frame);
            if (world.IsVisiting) DrawVisitBar(form, quick);
            else DrawOwnBar(form, quick, frame);
            DrawToast(frame);
            UIScale.End();
        }

        private Rect DrawOwnBar(IslandHudForm form, Rect quick, HudFrame frame)
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
            // 모바일은 퀵바(우상단) 곁에 선다 — 세로는 그 아래 열, 가로는 그 왼쪽 두 칸 판. 좌하단 사분면은 가상 조이스틱의
            // 시작 영역이라 거기 버튼을 두면 누를 때마다 캐릭터가 움찔한다. 데스크톱은 퀵바(하단 중앙) 바로 위에 한 줄.
            Rect area = IslandHudLayout.OwnPanel(form, quick, frame.ContentHeight);
            FieldHudInput.RegisterBlockingRect(area);
            UISurface.HudCard(area);
            if (form == IslandHudForm.Strip)
            {
                Rect info = IslandHudLayout.DesktopInfo(area);
                UISurface.HudCard(info);
                IslandUiKit.Label(new Rect(info.x + 16f, info.y, info.width - 32f, info.height), infoLine,
                    IslandUiKit.SmallCenter, t.textSecondary);
            }
            DrawOwnButtons(area, form, canHarvest, t);
            return area;
        }

        private void DrawOwnButtons(Rect area, IslandHudForm form, bool canHarvest, UITheme t)
        {
            Rect Slot(int index) => IslandHudLayout.OwnButton(area, form, index);
            GUIStyle style = IslandUiKit.Button;
            if (UISurface.Button(Slot(0), "꾸미기", t.surfaceRaised, style) && editUI != null) editUI.Open();
            if (UISurface.Button(Slot(1), "상점", t.surfaceRaised, style) && shopUI != null) shopUI.Toggle();
            if (UISurface.Button(Slot(2), "곤충", t.surfaceRaised, style) && insectUI != null) insectUI.Toggle();

            bool enabled = GUI.enabled;
            GUI.enabled = enabled && canHarvest;
            // 받을 게 있으면 민트로 눈에 띄게 — 섬에 들른 이유가 대개 이 버튼이다.
            if (UISurface.Button(Slot(IslandHudLayout.HarvestIndex), harvestLabel,
                    canHarvest ? t.accentMint : t.surfaceCard, style))
                DoHarvest();
            GUI.enabled = enabled;

            if (UISurface.Button(Slot(4), "방문", t.surfaceRaised, style) && visitUI != null) visitUI.Toggle();
            if (UISurface.Button(Slot(5), "도움말", t.surfaceRaised, style) && guideUI != null) guideUI.OpenHelp();
            if (UISurface.Button(Slot(6), "나가기", t.surfaceCard, style)) world.ExitIsland();
        }

        private void DoHarvest()
        {
            if (!island.Harvest(out int candy, out int coin)) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.ItemPickup);
            ShowToast("수확!  캔디 +" + candy + " · 코인 +" + coin);
        }

        private Rect DrawVisitBar(IslandHudForm form, Rect quick)
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

            Rect panel = IslandHudLayout.VisitPanel(form, quick);
            FieldHudInput.RegisterBlockingRect(panel);
            UISurface.HudCard(panel);
            IslandUiKit.Label(new Rect(panel.x + 12f, panel.y + 6f, panel.width - 24f, 40f), visitLine,
                IslandUiKit.BodyCenter, t.textPrimary);

            Rect like = IslandHudLayout.VisitButton(panel, form, 0);
            Rect home = IslandHudLayout.VisitButton(panel, form, 1);
            Rect exit = IslandHudLayout.VisitButton(panel, form, 2);

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
            return panel;
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

        /// <summary>
        /// 수확·좋아요 결과 한 줄의 자리 — 가운데 무대(<see cref="HudStageItem.IslandToast"/>). 예전엔 화면 위 가운데(+120)라
        /// 세로에서 단축 바를, 가로에서 섬 HUD 판을 덮었고, 그 다음엔 미니맵 오른쪽 줄에 두었는데 퀘스트 완료·코치 배너와 겹쳤다.
        /// </summary>
        public static Rect ToastRect(HudFrame f)
        {
            return HudStage.Place(f, HudStageItem.IslandToast, IslandHudLayout.ToastMaxWidth, IslandHudLayout.ToastHeight);
        }

        private void DrawToast(HudFrame frame)
        {
            if (string.IsNullOrEmpty(toast) || toastRemaining <= 0f) return;
            Rect r = ToastRect(frame);
            if (!HudStage.Request(HudStageItem.IslandToast, r)) return;   // 다른 카드가 무대에 서 있다 — 차례를 기다린다
            // 버튼은 없지만 불투명 카드다 — 그 위 탭이 클릭-이동으로 새지 않게.
            FieldHudInput.RegisterBlockingRect(r);
            UISurface.HudCard(r);
            IslandUiKit.Label(new Rect(r.x + 16f, r.y + 6f, r.width - 32f, r.height - 12f), toast,
                IslandUiKit.BodyCenter, UITheme.Instance.accentAmber);
        }
    }

    /// <summary>섬 HUD의 모양. 데스크톱 한 줄 · 세로 모바일의 세로 열 · 가로 모바일의 두 칸 판.</summary>
    public enum IslandHudForm
    {
        /// <summary>데스크톱 — 단축 바(하단 가운데) 바로 위 한 줄, 그 위에 정보 한 줄.</summary>
        Strip,
        /// <summary>세로 모바일 — 단축 바(우상단) 바로 아래 세로 열.</summary>
        Column,
        /// <summary>가로 모바일 — 단축 바 왼쪽, 윗변을 맞춘 두 칸 판(수확만 한 줄을 다 쓴다).</summary>
        Grid
    }

    /// <summary>
    /// 섬 HUD 버튼 줄·결과 토스트의 자리 — <b>순수 계산</b>. <see cref="IslandHudUI"/>가 이걸로 그리고, 그 자리를 피해야 하는 HUD
    /// (<see cref="WorldClockHUD"/>의 시각·날씨 칩)가 같은 계산을 읽는다 — 크기를 베껴 두면 줄을 늘릴 때 조용히 겹친다.
    /// 앵커는 단축 바(<see cref="QuickAccessBarUI.ShortcutBarRect"/>)다.
    /// 내 섬 버튼 순서는 꾸미기·상점·곤충·수확·방문·도움말·나가기(0~6), 남의 섬은 좋아요·내 섬으로·나가기(0~2).
    ///
    /// <b>가로 모바일이 두 칸 판인 이유:</b> 세로 열(7줄, 568)을 가로 화면 단축 바 아래에 세우면 화면 85% 높이까지 내려와
    /// 우하단 잡기 버튼(<c>CaptureInputController</c> — 지름 192, 아래 안전 가장자리에서 92/Scale 위)과 그 위 피드백 글자를 덮는다.
    /// 밤·비에 섬에도 야생 손님이 오면서 섬에서도 잡기 버튼이 뜬다. 단축 바 <b>왼쪽</b>은 리전 배너·내기 점수판이 단축 바 왼쪽 끝 기준
    /// 가운데에 서서 비어 있고, 줄 높이를 단축 바 버튼(68)과 맞추면 판 높이가 단축 바와 같아(312) 둘이 위쪽 한 띠에 나란히 선다.
    /// 그러면 단축 바 아래(시각·날씨 칩 자리)와 화면 아래쪽 절반이 비어 잡기 버튼·조이스틱과 멀다.
    /// </summary>
    public static class IslandHudLayout
    {
        public const int OwnButtonCount = 7;
        public const int VisitButtonCount = 3;
        /// <summary>내 섬 줄에서 수확 버튼의 순번 — 데스크톱 줄과 가로 판에서는 이 칸만 넓다(수확물 숫자가 들어간다).</summary>
        public const int HarvestIndex = 3;

        /// <summary>단축 바와 섬 HUD 사이.</summary>
        public const float AnchorGap = 10f;
        public const float MobileRowHeight = 72f;
        public const float MobileRowGap = 8f;
        /// <summary>가로 판의 줄 높이 — 단축 바의 모바일 버튼 높이(68)와 같다.</summary>
        public const float GridRowHeight = 68f;
        /// <summary>데스크톱 줄의 버튼, 세로 남의 섬 버튼 높이.</summary>
        public const float ButtonHeight = 64f;
        public const float CellGap = 6f;
        public const float HarvestWidth = 340f;
        public const float VisitHeaderHeight = 48f;
        /// <summary>남의 섬 판에서 머리 줄(주인·쾌적도·좋아요) 아래 첫 버튼의 윗변(모바일).</summary>
        public const float VisitButtonsTop = 52f;
        public const float ToastHeight = 60f;
        public const float ToastMaxWidth = 760f;

        /// <summary>세로 모바일 내 섬 열이 바라는 높이(버튼 7줄 + 위아래 여백).</summary>
        public const float MobileOwnColumnHeight = 16f + OwnButtonCount * MobileRowHeight + (OwnButtonCount - 1) * MobileRowGap;
        /// <summary>가로 판의 줄 수 — 수확 한 줄 + 나머지 여섯을 둘씩 세 줄.</summary>
        public const int GridOwnRows = 1 + (OwnButtonCount - 1) / 2;
        /// <summary>가로 판(내 섬) 높이 — 단축 바(2열 4줄, 312)와 같다.</summary>
        public const float GridOwnHeight = 16f + GridOwnRows * GridRowHeight + (GridOwnRows - 1) * MobileRowGap;
        /// <summary>가로 판(남의 섬) 높이 — 머리 줄 + 좋아요 한 줄 + (내 섬으로·나가기) 한 줄.</summary>
        public const float GridVisitHeight = VisitButtonsTop + 2f * GridRowHeight + MobileRowGap + 8f;

        public static IslandHudForm FormFor(bool mobileLayout, bool portrait)
        {
            if (!mobileLayout) return IslandHudForm.Strip;
            return portrait ? IslandHudForm.Column : IslandHudForm.Grid;
        }

        // ── 판 ──

        /// <summary>내 섬 판. <paramref name="maxHeight"/>는 안전 영역 높이(<c>UISafeLayout.ContentHeight</c>) — 세로 열만 쓴다.</summary>
        public static Rect OwnPanel(IslandHudForm form, Rect quick, float maxHeight)
        {
            switch (form)
            {
                case IslandHudForm.Column: return MobileOwnColumn(quick, maxHeight);
                case IslandHudForm.Grid: return LeftOfBar(quick, GridOwnHeight);
                default: return DesktopOwnStrip(quick);
            }
        }

        /// <summary>남의 섬 판.</summary>
        public static Rect VisitPanel(IslandHudForm form, Rect quick)
        {
            switch (form)
            {
                case IslandHudForm.Column: return MobileVisitPanel(quick);
                case IslandHudForm.Grid: return LeftOfBar(quick, GridVisitHeight);
                default: return DesktopVisitPanel(quick);
            }
        }

        /// <summary>지금 그려지는 판(내 섬 또는 남의 섬).</summary>
        public static Rect Panel(IslandHudForm form, Rect quick, bool visiting, float maxHeight)
        {
            return visiting ? VisitPanel(form, quick) : OwnPanel(form, quick, maxHeight);
        }

        /// <summary>세로 모바일 내 섬 — 단축 바 바로 아래 세로 열.</summary>
        public static Rect MobileOwnColumn(Rect quick, float maxHeight)
        {
            return new Rect(quick.x, quick.yMax + AnchorGap, quick.width, Mathf.Min(MobileOwnColumnHeight, maxHeight));
        }

        /// <summary>세로 모바일 남의 섬 — 단축 바 아래, 머리 줄(주인·쾌적도·좋아요) + 버튼 세 줄.</summary>
        public static Rect MobileVisitPanel(Rect quick)
        {
            return new Rect(quick.x, quick.yMax + AnchorGap, quick.width,
                16f + VisitHeaderHeight + VisitButtonCount * (ButtonHeight + 8f));
        }

        public static Rect DesktopOwnStrip(Rect bar)
        {
            return new Rect(bar.x, bar.y - ButtonHeight - 16f - AnchorGap, bar.width, ButtonHeight + 16f);
        }

        /// <summary>데스크톱 줄 위의 정보 한 줄(쾌적도·시간당 생산·쌓인 시간).</summary>
        public static Rect DesktopInfo(Rect strip)
        {
            return new Rect(strip.x, strip.y - 44f, strip.width, 40f);
        }

        public static Rect DesktopVisitPanel(Rect quick)
        {
            float h = ButtonHeight + 16f + VisitHeaderHeight;
            return new Rect(quick.x, quick.y - h - AnchorGap, quick.width, h);
        }

        // 가로 모바일 — 단축 바 왼쪽, 윗변을 맞춘다. 폭은 단축 바와 같다.
        private static Rect LeftOfBar(Rect quick, float height)
        {
            return new Rect(quick.x - AnchorGap - quick.width, quick.y, quick.width, height);
        }

        // ── 버튼 ──

        /// <summary>내 섬 <paramref name="index"/>번째 버튼(<paramref name="area"/>는 <see cref="OwnPanel"/>).</summary>
        public static Rect OwnButton(Rect area, IslandHudForm form, int index)
        {
            switch (form)
            {
                case IslandHudForm.Column:
                    return new Rect(area.x + 8f, area.y + 8f + index * (MobileRowHeight + MobileRowGap),
                        area.width - 16f, MobileRowHeight);
                case IslandHudForm.Grid:
                {
                    // 첫 줄은 수확(넓게 — 수확물 숫자가 든다), 그 아래로 [꾸미기|상점] [곤충|방문] [도움말|나가기].
                    if (index == HarvestIndex) return GridCell(area, 8f, 0, -1);
                    int slot = index < HarvestIndex ? index : index - 1;
                    return GridCell(area, 8f, 1 + slot / 2, slot % 2);
                }
                default:
                {
                    int narrow = OwnButtonCount - 1;
                    float cellW = (area.width - 16f - HarvestWidth - CellGap * narrow) / narrow;
                    float x = area.x + 8f;
                    for (int i = 0; i < index; i++) x += (i == HarvestIndex ? HarvestWidth : cellW) + CellGap;
                    return new Rect(x, area.y + 8f, index == HarvestIndex ? HarvestWidth : cellW, ButtonHeight);
                }
            }
        }

        /// <summary>남의 섬 <paramref name="index"/>번째 버튼(<paramref name="panel"/>은 <see cref="VisitPanel"/>).</summary>
        public static Rect VisitButton(Rect panel, IslandHudForm form, int index)
        {
            switch (form)
            {
                case IslandHudForm.Column:
                    return new Rect(panel.x + 8f, panel.y + VisitButtonsTop + index * (ButtonHeight + 8f),
                        panel.width - 16f, ButtonHeight);
                case IslandHudForm.Grid:
                    // 좋아요는 한 줄을 다 쓰고, 내 섬으로·나가기는 그 아래 둘로.
                    return index == 0 ? GridCell(panel, VisitButtonsTop, 0, -1) : GridCell(panel, VisitButtonsTop, 1, index - 1);
                default:
                {
                    float w = (panel.width - 16f - 12f) / VisitButtonCount;
                    return new Rect(panel.x + 8f + index * (w + 6f), panel.y + VisitHeaderHeight, w, ButtonHeight);
                }
            }
        }

        // 가로 판의 칸 — column이 음수면 그 줄을 다 쓴다.
        private static Rect GridCell(Rect area, float top, int row, int column)
        {
            float y = area.y + top + row * (GridRowHeight + MobileRowGap);
            float inner = area.width - 16f;
            if (column < 0) return new Rect(area.x + 8f, y, inner, GridRowHeight);
            float w = (inner - MobileRowGap) * 0.5f;
            return new Rect(area.x + 8f + column * (w + MobileRowGap), y, w, GridRowHeight);
        }

        // ── 화면 한 장(HudFrame)에서 ──

        public static IslandHudForm FormFor(HudFrame f) => FormFor(f.Mobile, f.Portrait);

        /// <summary>내 섬 판(화면 한 장 기준).</summary>
        public static Rect OwnPanel(HudFrame f) => OwnPanel(FormFor(f), QuickAccessBarUI.ShortcutBarRectFor(f), f.ContentHeight);

        /// <summary>남의 섬 판(화면 한 장 기준).</summary>
        public static Rect VisitPanel(HudFrame f) => VisitPanel(FormFor(f), QuickAccessBarUI.ShortcutBarRectFor(f));
    }
}
