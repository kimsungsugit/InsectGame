using InsectGame.Core;
using InsectGame.Dex;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>Field shortcuts and an exclusive, keyboard-accessible destination menu.</summary>
    public class QuickAccessBarUI : MonoBehaviour, IModalUI
    {
        public enum Destination { Dex, Team, Training, Collection, Quest, Map, Outfit, Shop, Pvp, Story, Inventory, Settings, Badges, Menu }
        [SerializeField] private DexScreenUI dexScreen;
        [SerializeField] private BattleTeamUI battleTeamUI;
        [SerializeField] private TrainingUI trainingUI;
        [SerializeField] private CollectionUI collectionUI;
        [SerializeField] private RegionMapUI regionMapUI;
        [SerializeField] private CharacterOutfitUI outfitUI;
        [SerializeField] private CashShopUI cashShopUI;
        [SerializeField] private TutorialQuestUI questUI;
        [SerializeField] private SocialPvpUI socialPvpUI;
        [SerializeField] private StoryJournalUI storyJournalUI;
        [SerializeField] private BadgeCaseUI badgeCaseUI;
        [SerializeField] private InventoryUI inventoryScreen;
        [SerializeField] private AccountSettingsUI settingsUI;
        [SerializeField] private BattleScreenUI battleScreen;
        [SerializeField] private RaidBattleUI raidScreen;
        [SerializeField] private PlayerMovement playerMovement;
        public const float BarButtonHeight = 60f;
        public const float BarReservedHeight = 88f;
        private Vector2 menuScroll;
        private readonly UIDirectScroll menuDirectScroll = new UIDirectScroll();
        private bool menuOpen;
        private int lastToggleFrame = -1;
        private GUIStyle buttonStyle;
        public bool IsOpen => menuOpen;
        private static readonly Destination[] shortcuts = {
            Destination.Dex, Destination.Collection, Destination.Team, Destination.Inventory,
            Destination.Quest, Destination.Map, Destination.Menu
        };
        private static readonly Destination[] destinations = { Destination.Dex, Destination.Collection, Destination.Team, Destination.Training, Destination.Inventory, Destination.Quest, Destination.Story, Destination.Badges, Destination.Map, Destination.Outfit, Destination.Shop, Destination.Pvp, Destination.Settings };
        public static System.Collections.Generic.IReadOnlyList<Destination> Shortcuts => System.Array.AsReadOnly(shortcuts);
        public static System.Collections.Generic.IReadOnlyList<Destination> Destinations => System.Array.AsReadOnly(destinations);

        public static KeyCode GetHotkey(Destination id)
        {
            switch (id)
            {
                case Destination.Dex: return KeyCode.N;
                case Destination.Team: return KeyCode.T;
                case Destination.Training: return KeyCode.G;
                case Destination.Collection: return KeyCode.C;
                case Destination.Quest: return KeyCode.Q;
                case Destination.Map: return KeyCode.M;
                case Destination.Outfit: return KeyCode.P;
                case Destination.Shop: return KeyCode.F4;
                case Destination.Pvp: return KeyCode.F6;
                case Destination.Story: return KeyCode.J;
                case Destination.Inventory: return KeyCode.I;
                case Destination.Badges: return KeyCode.K;
                default: return KeyCode.None;
            }
        }

        private static string Label(Destination id)
        {
            switch (id)
            {
                case Destination.Dex: return "도감";
                case Destination.Team: return "배틀팀";
                case Destination.Training: return "훈련";
                case Destination.Collection: return "보유 곤충";
                case Destination.Quest: return "퀘스트";
                case Destination.Map: return "지도";
                case Destination.Outfit: return "의상";
                case Destination.Shop: return "상점";
                case Destination.Pvp: return "PVP";
                case Destination.Story: return "이야기";
                case Destination.Inventory: return "가방";
                case Destination.Settings: return "설정 · 계정";
                case Destination.Badges: return "배지";
                default: return "메뉴";
            }
        }

        public void CloseModal()
        {
            menuOpen = false;
            menuDirectScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }
        private void OnDisable() => CloseModal();
        private bool IsInputBlocked() => (battleScreen != null && battleScreen.IsBattleActive)
            || (raidScreen != null && raidScreen.IsRaidActive)
            || (playerMovement != null && playerMovement.IsFrozen);

        private void Update()
        {
            if (IsInputBlocked()) { CloseModal(); return; }
            foreach (Destination id in destinations)
            {
                KeyCode key = GetHotkey(id);
                if (key != KeyCode.None && Input.GetKeyDown(key)) TryNavigate(id);
            }
        }

        private void OnGUI()
        {
            if (IsInputBlocked()) return;
            if (menuOpen && !ReferenceEquals(ModalUIRegistry.TopModal, this)) return;
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
                foreach (Destination id in destinations)
                    if (GetHotkey(id) != KeyCode.None && e.keyCode == GetHotkey(id) && TryNavigate(id)) { e.Use(); break; }
            if (!menuOpen && ModalUIRegistry.IsAnyOpen()) return;
            UIScale.Begin();
            if (buttonStyle == null)
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold, richText = true };
            buttonStyle.normal.textColor = UITheme.Instance.textPrimary;
            if (menuOpen) DrawMenu();
            else DrawShortcuts();
            UIScale.End();
        }

        private void DrawShortcuts()
        {
            bool mobile = UIScale.IsMobileLayout;
            int columns = mobile ? 2 : shortcuts.Length;
            int rows = (shortcuts.Length + columns - 1) / columns;
            float gap = mobile ? 8f : 6f;
            float buttonHeight = mobile ? 68f : BarButtonHeight;
            float desiredWidth = mobile ? 324f : 1130f;
            float desiredHeight = 16f + rows * buttonHeight + (rows - 1) * gap;
            Rect bar = mobile ? UISafeLayout.TopPanel(desiredWidth, desiredHeight, UISafeLayout.HAlign.Right)
                : UISafeLayout.BottomPanel(desiredWidth, desiredHeight);
            FieldHudInput.RegisterBlockingRect(bar);
            UISurface.Card(bar);
            float cellWidth = (bar.width - 16f - (columns - 1) * gap) / columns;
            for (int i = 0; i < shortcuts.Length; i++)
            {
                Rect cell = new Rect(bar.x + 8f + (i % columns) * (cellWidth + gap),
                    bar.y + 8f + (i / columns) * (buttonHeight + gap), cellWidth, buttonHeight);
                if (mobile && i == shortcuts.Length - 1)
                    cell.width = bar.width - 16f;
                DrawDestination(cell, shortcuts[i]);
            }
        }

        private void DrawMenu()
        {
            UISurface.Dim(0.65f);
            Rect panel = UISafeLayout.CenteredPanel(820f, 600f);
            UISurface.Card(panel);
            if (UISurface.Header(new Rect(panel.x + 3f, panel.y + 3f, panel.width - 6f, 78f), "탐험 메뉴", "")) { CloseModal(); return; }
            float gap = UITheme.Space.S;
            // 행 수는 목적지 수에서 센다 — 4로 박아 두면 13번째(배지)가 스크롤 밖으로 잘린다.
            int rows = (destinations.Length + 2) / 3;
            float cellW = (panel.width - 48f - gap * 2f) / 3f;
            float cellH = Mathf.Max(UIScale.MinTouchHeight, (panel.height - 120f - gap * (rows - 1)) / rows);
            Rect viewport = new Rect(panel.x + 24f, panel.y + 96f, panel.width - 48f, Mathf.Max(1f, panel.height - 112f));
            float contentH = rows * (cellH + gap);
            menuDirectScroll.Handle(ref menuScroll, viewport, contentH, cellH);
            menuScroll = GUI.BeginScrollView(viewport, menuScroll, new Rect(0f, 0f, viewport.width - 16f, contentH));
            cellW = (viewport.width - 16f - gap * 2f) / 3f;
            for (int i = 0; i < destinations.Length; i++)
                DrawDestination(new Rect((i % 3) * (cellW + gap), (i / 3) * (cellH + gap), cellW, cellH), destinations[i]);
            GUI.EndScrollView();
            UISurface.ScrollAffordance(viewport, menuScroll, contentH, UITheme.Instance.accentMint);
        }

        private void DrawDestination(Rect rect, Destination id)
        {
            KeyCode key = GetHotkey(id);
            string label = Label(id);
            if (!UIScale.IsMobileLayout && key != KeyCode.None) label += $"  <size=16>[{key}]</size>";
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && (id == Destination.Menu || Target(id) != null);
            if (UISurface.Button(rect, label, UITheme.Instance.surfaceRaised, buttonStyle)) TryNavigate(id);
            GUI.enabled = enabled;
            if (id == Destination.Menu || id == Destination.Quest)
            {
                int count = TutorialQuestManager.Instance != null ? TutorialQuestManager.Instance.UnseenCompletedCount : 0;
                if (count > 0) UISurface.Chip(new Rect(rect.xMax - 36f, rect.y + 2f, 34f, 26f), count > 9 ? "9+" : count.ToString(), UITheme.Instance.accentCoral, UITheme.Instance.textPrimary);
            }
            // 받을 이정표 보상이 있으면 점을 찍는다 — 옛 세이브는 케이스의 [받기]가 유일한 수령 경로다.
            if (id == Destination.Badges && badgeCaseUI != null && badgeCaseUI.ClaimableCount > 0)
                UISurface.Chip(new Rect(rect.xMax - 36f, rect.y + 2f, 34f, 26f), "!", UITheme.Instance.accentAmber, UITheme.Instance.surfaceBase);
        }

        private IModalUI Target(Destination id)
        {
            switch (id)
            {
                case Destination.Dex: return dexScreen;
                case Destination.Team: return battleTeamUI;
                case Destination.Training: return trainingUI;
                case Destination.Collection: return collectionUI;
                case Destination.Quest: return questUI;
                case Destination.Map: return regionMapUI;
                case Destination.Outfit: return outfitUI;
                case Destination.Shop: return cashShopUI;
                case Destination.Pvp: return socialPvpUI;
                case Destination.Story: return storyJournalUI;
                case Destination.Badges: return badgeCaseUI;
                case Destination.Inventory: return inventoryScreen;
                case Destination.Settings: return settingsUI;
                default: return null;
            }
        }

        private bool TryNavigate(Destination id)
        {
            if (IsInputBlocked()) return false;
            IModalUI target = Target(id);
            if (ModalUIRegistry.IsAnyOpenExcept(menuOpen ? typeof(QuickAccessBarUI) : null)
                && (target == null || !ReferenceEquals(ModalUIRegistry.TopModal, target))) return false;
            if (id != Destination.Menu && target == null) return false;
            if (Time.frameCount == lastToggleFrame) return true;
            lastToggleFrame = Time.frameCount;
            if (id == Destination.Menu)
            {
                if (menuOpen) CloseModal();
                else { menuOpen = true; ModalUIRegistry.Register(this); }
                return true;
            }
            CloseModal();
            switch (id)
            {
                case Destination.Dex: dexScreen.Toggle(); break;
                case Destination.Team: battleTeamUI.Toggle(); break;
                case Destination.Training: trainingUI.Toggle(); break;
                case Destination.Collection: collectionUI.Toggle(); break;
                case Destination.Quest: questUI.Toggle(); break;
                case Destination.Map: regionMapUI.Toggle(); break;
                case Destination.Outfit: outfitUI.Toggle(); break;
                case Destination.Shop: cashShopUI.Toggle(); break;
                case Destination.Pvp: socialPvpUI.Toggle(); break;
                case Destination.Story: storyJournalUI.Toggle(); break;
                case Destination.Badges: badgeCaseUI.Toggle(); break;
                case Destination.Inventory: inventoryScreen.Toggle(); break;
                case Destination.Settings: settingsUI.OpenSettings(); break;
            }
            return true;
        }

        public void AutoWire(AccountSettingsUI settings) { if (settingsUI == null) settingsUI = settings; }

        public void AutoWire(DexScreenUI dex, BattleTeamUI team, TrainingUI training,
            CollectionUI collection, RegionMapUI map)
        {
            if (dexScreen == null) dexScreen = dex;
            if (battleTeamUI == null) battleTeamUI = team;
            if (trainingUI == null) trainingUI = training;
            if (collectionUI == null) collectionUI = collection;
            if (regionMapUI == null) regionMapUI = map;
            collectionUI?.AutoWire(battleTeamUI, trainingUI);
        }

        public void AutoWire(CharacterOutfitUI outfit, CashShopUI cashShop)
        {
            if (outfitUI == null) outfitUI = outfit;
            if (cashShopUI == null) cashShopUI = cashShop;
        }

        public void AutoWire(TutorialQuestUI quest)
        {
            if (questUI == null) questUI = quest;
        }

        public void AutoWire(StoryJournalUI journal)
        {
            if (storyJournalUI == null) storyJournalUI = journal;
        }

        public void AutoWire(BadgeCaseUI badgeCase)
        {
            if (badgeCaseUI == null) badgeCaseUI = badgeCase;
        }

        public void AutoWire(SocialPvpUI social)
        {
            if (socialPvpUI == null) socialPvpUI = social;
        }

        public void AutoWire(InventoryUI inventory)
        {
            if (inventoryScreen == null) inventoryScreen = inventory;
        }

        // 전투/포획/미니게임 중 입력 가드용 신호 주입.
        public void AutoWire(BattleScreenUI battle, RaidBattleUI raid, PlayerMovement movement)
        {
            if (battleScreen == null) battleScreen = battle;
            if (raidScreen == null) raidScreen = raid;
            if (playerMovement == null) playerMovement = movement;
        }
    }
}
