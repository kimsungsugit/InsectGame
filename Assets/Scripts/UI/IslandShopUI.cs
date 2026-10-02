using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 섬 상점 — 건물·가구·지형지물·도구를 사고(보관함으로 들어간다) 섬 크기와 곤충 자리를 넓힌다.
    /// 되팔기는 없다: 다이아 환급은 서버 규칙이 막고, 코인만 환급하면 다이아 물건과 규칙이 갈린다.
    /// </summary>
    public class IslandShopUI : MonoBehaviour, IModalUI
    {
        private const int ExpandTab = 4;
        private const float FeedbackSeconds = 2.2f;

        private static readonly string[] TabNames = { "건물", "가구", "지형지물", "도구", "확장" };

        [SerializeField] private IslandManager island;
        [SerializeField] private PlayerCurrencyWallet wallet;

        private bool isOpen;
        private int tab = 1;
        private Vector2 scroll;
        private readonly UIDirectScroll directScroll = new UIDirectScroll();
        private readonly List<IslandObjectDef> rows = new List<IslandObjectDef>();
        private int rowsTab = -1;

        private string feedback;
        private float feedbackUntil;

        // 행마다 매 프레임 문자열을 잇지 않게 물건별 문구를 한 번만 만든다. 보유 수가 바뀌면 그 줄만 다시 만든다.
        private readonly Dictionary<string, string> specLabels = new Dictionary<string, string>();
        private readonly Dictionary<string, string> ownedLabels = new Dictionary<string, string>();
        private readonly Dictionary<string, int> ownedShown = new Dictionary<string, int>();
        private string walletLabel = string.Empty;
        private int walletKey = -1;

        public bool IsOpen => isOpen;

        public void AutoWire(IslandManager islandManager, PlayerCurrencyWallet currencyWallet)
        {
            if (island == null) island = islandManager;
            if (wallet == null) wallet = currencyWallet;
        }

        public void Toggle()
        {
            if (isOpen) { CloseModal(); return; }
            if (island == null) return;
            isOpen = true;
            scroll = Vector2.zero;
            rowsTab = -1;
            directScroll.Reset();
            ModalUIRegistry.Register(this);
            island.ReportShopOpened();
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
            if (!isOpen || island == null) return;
            if (!ReferenceEquals(ModalUIRegistry.TopModal, this)) return;

            UIScale.Begin();
            UISurface.Dim(0.6f);
            bool mobile = UIScale.IsMobileLayout;
            Rect panel = UISafeLayout.CenteredPanel(mobile ? 1000f : 1080f, mobile ? 1500f : 940f);
            UISurface.Card(panel);
            if (UISurface.Header(new Rect(panel.x + 3f, panel.y + 3f, panel.width - 6f, 84f), "섬 상점", WalletLabel()))
            {
                CloseModal();
                UIScale.End();
                return;
            }

            DrawTabs(new Rect(panel.x + 20f, panel.y + 100f, panel.width - 40f, 64f));
            Rect body = new Rect(panel.x + 20f, panel.y + 176f, panel.width - 40f,
                Mathf.Max(1f, panel.height - 176f - 76f));
            if (tab == ExpandTab) DrawExpand(body);
            else DrawCatalog(body);
            DrawFeedback(new Rect(panel.x + 20f, panel.yMax - 66f, panel.width - 40f, 50f));
            UIScale.End();
        }

        private string WalletLabel()
        {
            int coins = wallet != null ? wallet.Coins : 0;
            int gems = wallet != null ? wallet.Gems : 0;
            int key = coins * 1000003 + gems;
            if (key != walletKey)
            {
                walletKey = key;
                walletLabel = "코인 " + coins + "  ·  다이아 " + gems;
            }
            return walletLabel;
        }

        private void DrawTabs(Rect area)
        {
            UITheme t = UITheme.Instance;
            const float gap = 8f;
            float w = (area.width - gap * (TabNames.Length - 1)) / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                Rect r = new Rect(area.x + i * (w + gap), area.y, w, area.height);
                if (UISurface.Button(r, TabNames[i], i == tab ? t.accentCoral : t.surfaceRaised, IslandUiKit.Button, i == tab)
                    && tab != i)
                {
                    tab = i;
                    scroll = Vector2.zero;
                    directScroll.Reset();
                }
            }
        }

        private void RebuildRows()
        {
            rows.Clear();
            IReadOnlyList<IslandObjectDef> all = IslandCatalog.All;
            for (int i = 0; i < all.Count; i++)
                if ((int)all[i].category == tab) rows.Add(all[i]);
            rowsTab = tab;
        }

        private void DrawCatalog(Rect area)
        {
            if (rowsTab != tab) RebuildRows();
            UITheme t = UITheme.Instance;
            bool mobile = UIScale.IsMobileLayout;
            float rowH = mobile ? 168f : 132f;
            const float gap = 8f;
            float contentH = rows.Count * (rowH + gap);
            Rect view = new Rect(0f, 0f, area.width - 16f, contentH);
            directScroll.Handle(ref scroll, area, contentH, rowH * 0.5f);
            scroll = GUI.BeginScrollView(area, scroll, view, GUIStyle.none, GUIStyle.none);
            for (int i = 0; i < rows.Count; i++)
            {
                float y = i * (rowH + gap);
                // 뷰포트 밖 행은 건너뛴다.
                if (y + rowH < scroll.y || y > scroll.y + area.height) continue;
                DrawRow(new Rect(0f, y, view.width, rowH), rows[i], t, mobile);
            }
            GUI.EndScrollView();
            UISurface.ScrollAffordance(area, scroll, contentH, t.accentMint);
        }

        private void DrawRow(Rect r, IslandObjectDef def, UITheme t, bool mobile)
        {
            bool locked = island.IsLockedBySize(def);
            UISurface.Card(r, t.surfaceCard, t.surfaceBorder);
            // 세로 등급 레일 — 폭이 좁아 둥근 9-slice를 쓰면 뭉개진다(Flat). 둥근 모서리를 뚫지 않게 위아래를 물린다.
            UISurface.Flat(new Rect(r.x + 3f, r.y + UITheme.Radius.Card, 6f, r.height - UITheme.Radius.Card * 2f),
                IslandUiKit.CategoryColor(def.category));

            float buyW = mobile ? 220f : 230f;
            float textW = r.width - 28f - buyW - 20f;
            IslandUiKit.Label(new Rect(r.x + 22f, r.y + 8f, textW, 40f), def.displayName, IslandUiKit.Title, t.textPrimary);
            IslandUiKit.Label(new Rect(r.x + 22f, r.y + 48f, textW, r.height - 48f - 40f), def.description,
                IslandUiKit.BodyWrap, t.textSecondary);
            IslandUiKit.Label(new Rect(r.x + 22f, r.yMax - 38f, textW * 0.62f, 32f), SpecLabel(def),
                IslandUiKit.Small, t.accentAmber);
            IslandUiKit.Label(new Rect(r.x + 22f + textW * 0.62f, r.yMax - 38f, textW * 0.38f, 32f), OwnedLabel(def),
                IslandUiKit.Small, t.textMuted);

            Rect buy = new Rect(r.xMax - buyW - 12f, r.y + (r.height - 68f) * 0.5f, buyW, 68f);
            if (locked)
            {
                UISurface.Rounded(buy, t.surfaceBase);
                IslandUiKit.Label(buy, "섬 확장 필요", IslandUiKit.ButtonSmall, t.textMuted);
                return;
            }

            bool afford = CanAfford(def.coinPrice, def.gemPrice, def.IsPremium);
            bool enabled = GUI.enabled;
            // 스크롤하다 뗀 손가락이 구매로 읽히지 않게 — 끄는 중에는 버튼을 끈다.
            GUI.enabled = enabled && afford && !directScroll.IsDragging;
            if (UISurface.Button(buy, PriceLabel(def.coinPrice, def.gemPrice, def.IsPremium),
                    def.IsPremium ? t.insectRare : t.accentMint, IslandUiKit.Button))
                Buy(def);
            GUI.enabled = enabled;
        }

        private string SpecLabel(IslandObjectDef def)
        {
            if (specLabels.TryGetValue(def.id, out string label)) return label;
            label = def.width + "×" + def.depth + "칸  ·  쾌적도 +" + def.comfort;
            string effect = IslandUiKit.EffectText(def);
            if (!string.IsNullOrEmpty(effect)) label += "  ·  " + effect;
            specLabels[def.id] = label;
            return label;
        }

        private string OwnedLabel(IslandObjectDef def)
        {
            int owned = island.GetOwnedCount(def.id);
            if (!ownedShown.TryGetValue(def.id, out int shown) || shown != owned)
            {
                ownedShown[def.id] = owned;
                ownedLabels[def.id] = owned > 0 ? "보유 " + owned : string.Empty;
            }
            return ownedLabels[def.id];
        }

        // 가격 문구는 가짓수가 적어(카탈로그 가격 + 확장 단계) 값으로 캐시한다.
        private readonly Dictionary<int, string> priceLabels = new Dictionary<int, string>();

        private string PriceLabel(int coins, int gems, bool useGems)
        {
            int key = useGems ? -gems : coins;
            if (priceLabels.TryGetValue(key, out string label)) return label;
            label = useGems ? "다이아 " + gems : "코인 " + coins;
            priceLabels[key] = label;
            return label;
        }

        private bool CanAfford(int coins, int gems, bool useGems)
        {
            if (AuthManager.Instance != null && AuthManager.Instance.MasterPrivilegesActive) return true;
            if (wallet == null) return false;
            return useGems ? wallet.Gems >= gems : wallet.Coins >= coins;
        }

        private void Buy(IslandObjectDef def)
        {
            IslandBuyResult result = island.TryBuy(def.id);
            if (result == IslandBuyResult.Ok && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SfxType.Purchase);
            ShowFeedback(result == IslandBuyResult.Ok
                ? def.displayName + " — 보관함에 넣었습니다. [꾸미기]에서 놓아 보세요."
                : IslandUiKit.BuyResultText(result));
        }

        private void DrawExpand(Rect area)
        {
            UITheme t = UITheme.Instance;
            const float cardH = 250f;
            Rect sizeCard = new Rect(area.x, area.y, area.width, cardH);
            Rect slotCard = new Rect(area.x, area.y + cardH + 16f, area.width, cardH);

            bool canSize = IslandCatalog.SizePrice(island.SizeLevel, out int sizeCoins, out int sizeGems);
            bool canSlot = IslandCatalog.SlotPrice(island.ExtraSlots, out int slotCoins, out int slotGems);

            // 단계가 바뀔 때만 문구를 다시 잇는다.
            int key = island.SizeLevel * 100 + island.ExtraSlots;
            if (key != expandKey)
            {
                expandKey = key;
                int grid = island.GridSize;
                int next = grid + GameConstants.Island.GridSizeStep;
                sizeNow = "지금 " + grid + "×" + grid + "칸" + (canSize ? "  →  " + next + "×" + next + "칸" : "  (최대)");
                slotNow = "지금 " + island.InsectSlots + "마리"
                          + (canSlot ? "  →  " + (island.InsectSlots + 1) + "마리" : "  (최대)");
            }

            DrawExpandCard(sizeCard, "섬 넓히기", "섬이 사방으로 넓어집니다. 놓아둔 물건은 그대로 있습니다.",
                sizeNow, canSize, sizeCoins, sizeGems, t, true);
            DrawExpandCard(slotCard, "곤충 자리 늘리기", "섬에 풀어놓을 수 있는 곤충이 한 마리 늘어납니다.",
                slotNow, canSlot, slotCoins, slotGems, t, false);
        }

        private int expandKey = -1;
        private string sizeNow = string.Empty;
        private string slotNow = string.Empty;

        private void DrawExpandCard(Rect r, string title, string desc, string now, bool available,
            int coins, int gems, UITheme t, bool isSize)
        {
            UISurface.Card(r, t.surfaceCard, t.surfaceBorder);
            IslandUiKit.Label(new Rect(r.x + 22f, r.y + 12f, r.width - 44f, 44f), title, IslandUiKit.Title, t.textPrimary);
            IslandUiKit.Label(new Rect(r.x + 22f, r.y + 58f, r.width - 44f, 36f), desc, IslandUiKit.Body, t.textSecondary);
            IslandUiKit.Label(new Rect(r.x + 22f, r.y + 98f, r.width - 44f, 36f), now, IslandUiKit.Body, t.accentAmber);
            if (!available) return;

            // 코인으로도 다이아로도 넓힐 수 있다 — 다이아가 유료 전용이라 코인 길을 막지 않는다.
            float w = (r.width - 44f - 12f) * 0.5f;
            Rect coinBtn = new Rect(r.x + 22f, r.yMax - 84f, w, 68f);
            Rect gemBtn = new Rect(coinBtn.xMax + 12f, coinBtn.y, w, 68f);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && CanAfford(coins, gems, false);
            if (UISurface.Button(coinBtn, PriceLabel(coins, gems, false), t.accentMint, IslandUiKit.Button))
                Expand(isSize, false);
            GUI.enabled = enabled && CanAfford(coins, gems, true);
            if (UISurface.Button(gemBtn, PriceLabel(coins, gems, true), t.insectRare, IslandUiKit.Button))
                Expand(isSize, true);
            GUI.enabled = enabled;
        }

        private void Expand(bool isSize, bool useGems)
        {
            IslandBuyResult result = isSize ? island.TryExpandSize(useGems) : island.TryBuySlot(useGems);
            if (result == IslandBuyResult.Ok && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SfxType.Purchase);
            ShowFeedback(result == IslandBuyResult.Ok
                ? (isSize ? "섬이 넓어졌습니다" : "곤충 자리가 늘었습니다")
                : IslandUiKit.BuyResultText(result));
        }

        private void DrawFeedback(Rect r)
        {
            if (string.IsNullOrEmpty(feedback) || Time.unscaledTime >= feedbackUntil) return;
            IslandUiKit.Label(r, feedback, IslandUiKit.BodyCenter, UITheme.Instance.accentAmber);
        }

        private void ShowFeedback(string message)
        {
            feedback = message;
            feedbackUntil = Time.unscaledTime + FeedbackSeconds;
        }
    }
}
